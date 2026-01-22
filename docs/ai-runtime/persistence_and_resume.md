# Persistence & Resume Deep Dive

**Purpose:** Document MongoDB persistence architecture, chat session storage, message handling, and AG2 resume patterns that enable seamless conversation continuity.

---

## Persistence Architecture

**Module:** `core/data/persistence_manager.py`

**Key Classes:**
1. **PersistenceManager** - App validation, wallet token accounting
2. **AG2PersistenceManager** - Chat session storage, message persistence, workflow stats

**MongoDB Collections:**
- `MozaiksDB.apps` - App registry
- `MozaiksDB.Wallets` - User token balances
- `MozaiksAI.chat_sessions` - Chat sessions with embedded messages (canonical transcript)
- `MozaiksAI.workflow_stats_{app}_{workflow}` - Real-time metrics rollup per workflow

---

## ChatSessions Collection Schema

**Collection:** `chat_sessions`

**Document Structure:**
```javascript
{
  "_id": "chat_abc123def456",  // chat_id
  "app_id": "6507f1b2e4b0c8a9d2f3e4a5",  // ObjectId or string
  "workflow_name": "Generator",
  "user_id": "user_alice_456",
  "cache_seed": 2847561923,  // Deterministic 32-bit seed
  "status": 1,  // 1=RUNNING, 2=PAUSED, 3=COMPLETED, 4=FAILED
  "created_at": ISODate("2025-10-02T10:00:00.000Z"),
  "last_updated_at": ISODate("2025-10-02T10:15:00.000Z"),
  "completed_at": null,  // Set when workflow completes
  "duration_sec": 0.0,  // Updated on completion
  "last_sequence": 15,  // Atomic counter for message ordering
  "last_artifact": {  // Latest artifact UI tool context
    "ui_tool_id": "code_editor",
    "event_id": "evt_xyz789",
    "display": "artifact",
    "workflow_name": "Generator",
    "payload": { /* ... */ },
    "updated_at": ISODate("2025-10-02T10:14:00.000Z")
  },
  "messages": [
    {
      "role": "user",
      "name": "user",
      "content": "Create a todo app",
      "sequence": 0,
      "timestamp": "2025-10-02T10:00:00.000Z",
      "event_id": "init_abc123"
    },
    {
      "role": "assistant",
      "name": "interviewer",
      "content": "What features should it include?",
      "sequence": 1,
      "timestamp": "2025-10-02T10:00:05.000Z",
      "event_id": "evt_msg_001"
    },
    // ... more messages
  ],
  "context_snapshot": {  // ConversableContext state for resume
    "interview_complete": true,
    "user_requirements": "Todo app with priority tags",
    // ... other context variables
  }
}
```

---

## AG2PersistenceManager API

### Create Chat Session

```python
from core.data.persistence_manager import AG2PersistenceManager

persistence = AG2PersistenceManager()

await persistence.create_chat_session(
    chat_id="chat_abc123",
    app_id="acme_corp",
    workflow_name="Generator",
    user_id="user_456",
    cache_seed=2847561923
)
```

**Side Effects:**
1. Inserts document into `chat_sessions` collection
2. Creates rollup document in `workflow_stats_{app}_{workflow}` collection
3. Initializes empty `messages` array and `context_snapshot`

---

### Append Message

**Real-Time Persistence:** Messages appended immediately as AG2 emits TextEvents (no batching).

```python
await persistence.append_message(
    chat_id="chat_abc123",
    message={
        "role": "assistant",
        "name": "planner",
        "content": "Here's the plan...",
        "sequence": 5,
        "timestamp": datetime.now(UTC).isoformat(),
        "event_id": "evt_msg_005"
    }
)
```

**MongoDB Operation:**
```javascript
db.chat_sessions.update_one(
  {"_id": "chat_abc123"},
  {
    "$push": {"messages": {...}},
    "$set": {"last_updated_at": new Date()},
    "$inc": {"last_sequence": 1}
  }
)
```

**Sequence Counter:** `last_sequence` ensures deterministic message ordering, even with concurrent appends.

---

### Load Chat History (Resume)

```python
messages = await persistence.load_chat_history(
    chat_id="chat_abc123",
    app_id="acme_corp"
)

# Returns:
# [
#   {"role": "user", "name": "user", "content": "Create a todo app"},
#   {"role": "assistant", "name": "interviewer", "content": "What features?"},
#   ...
# ]
```

**Normalization:** Messages converted to strict AG2 format:
- `role`: "user" or "assistant"
- `name`: Exact agent name (required for AG2 resume)
- `content`: String, dict, or list (AG2-compatible)

**Resume Flow:**
```python
# Load persisted messages
persisted_messages = await persistence.load_chat_history(chat_id, app_id)

# Normalize to strict AG2 format
normalized = _normalize_to_strict_ag2(
    persisted_messages,
    default_user_name="user"
)

# Restore AG2 GroupChat state
group_chat.messages = normalized

# Run orchestration (continues from last message)
await run_workflow_orchestration(
    chat_id=chat_id,
    workflow_name=workflow_name,
    initial_message=None,  # Resume mode (no new message)
    ...
)
```

---

### Persist Initial Messages

**Problem:** User's initial message (e.g., "Create a todo app") is consumed by AG2 but not re-emitted as TextEvent, leaving `messages` array empty until first agent reply.

**Solution:**
```python
await persistence.persist_initial_messages(
    chat_id="chat_abc123",
    app_id="acme_corp",
    messages=[
        {"role": "user", "name": "user", "content": "Create a todo app"}
    ]
)
```

**Duplicate Guard:** Checks last message in array; skips if identical `(role, content)` pair already exists.

---

### Mark Chat Completed

```python
await persistence.mark_chat_completed(
    chat_id="chat_abc123",
    app_id="acme_corp"
)
```

**Updates:**
1. `status` → `WorkflowStatus.COMPLETED` (3)
2. `completed_at` → Current timestamp
3. `duration_sec` → `(completed_at - created_at).total_seconds()`
4. Triggers async rollup refresh for `workflow_stats_{app}_{workflow}`

---

### Update Last Artifact

**Purpose:** Store latest artifact/tool panel context for multi-user resume (e.g., code editor, file browser).

```python
await persistence.update_last_artifact(
    chat_id="chat_abc123",
    app_id="acme_corp",
    artifact={
        "ui_tool_id": "code_editor",
        "event_id": "evt_xyz789",
        "display": "artifact",
        "workflow_name": "Generator",
        "payload": {
            "language": "python",
            "code": "def hello():\n    print('Hello')"
        }
    }
)
```

**Storage:** Overwrites `last_artifact` field (only most recent artifact kept).

**Resume:** Frontend fetches `last_artifact` via `/api/chats/{app_id}/{chat_id}/meta` and restores artifact panel.

---

## Context Snapshot (ConversableContext Persistence)

**Purpose:** Persist AG2 `ConversableContext` state for workflow resume.

**When Saved:**
- After orchestration completes (success or failure)
- Manually via `update_context_snapshot()`

**Storage:**
```python
await persistence.update_context_snapshot(
    chat_id="chat_abc123",
    app_id="acme_corp",
    context_data={
        "interview_complete": True,
        "user_requirements": "Todo app with priority tags",
        "architect_decisions": {"framework": "React", "backend": "FastAPI"}
    }
)
```

**MongoDB Update:**
```javascript
db.chat_sessions.update_one(
  {"_id": "chat_abc123"},
  {"$set": {"context_snapshot": {...}, "last_updated_at": new Date()}}
)
```

**Resume:**
```python
# Load context snapshot
snapshot = await persistence.load_context_snapshot(
    chat_id="chat_abc123",
    app_id="acme_corp"
)

# Restore ConversableContext
context = ConversableContext()
for key, value in snapshot.items():
    context.set(key, value)

# Pass to orchestration
await run_workflow_orchestration(
    chat_id=chat_id,
    context=context,
    ...
)
```

---

## Message Normalization (_normalize_to_strict_ag2)

**Purpose:** Ensure all messages conform to AG2's strict format requirements.

**AG2 Requirements:**
- `role`: Must be "user" or "assistant" (no "system", "function", etc.)
- `name`: Must be exact agent name (required field)
- `content`: Can be string, dict, or list (but not None)

**Normalization Rules:**
```python
def _normalize_to_strict_ag2(
    raw_msgs: Optional[List[Any]],
    *,
    default_user_name: str = "user"
) -> List[Dict[str, Any]]:
    """
    1. Reject non-dict messages
    2. Require role in ("user", "assistant")
    3. Require name (string, non-empty)
    4. Require content (not None)
    5. Fix-up: If role="user" but no name, set name="user"
    6. Drop malformed messages silently (strict mode)
    """
```

**Example Transformation:**
```python
# Input (persisted)
[
    {"role": "user", "content": "Hello"},  # Missing name
    {"role": "assistant", "name": "planner", "content": "Hi there"},
    {"role": "system", "content": "System msg"}  # Invalid role
]

# Output (normalized)
[
    {"role": "user", "name": "user", "content": "Hello"},
    {"role": "assistant", "name": "planner", "content": "Hi there"}
    # System message dropped
]
```

**Rationale:** AG2 resume fails with cryptic errors if messages don't match expected format. Strict normalization prevents resume issues.

---

## WorkflowStats Rollup

**Collection:** `workflow_stats_{app}_{workflow}`

**Example:** `workflow_stats_acme_corp_Generator`

**Document Structure:**
```javascript
{
  "_id": "mon_acme_corp_Generator",
  "app_id": "acme_corp",
  "workflow_name": "Generator",
  "last_updated_at": ISODate("2025-10-02T10:30:00.000Z"),
  "overall_avg": {
    "avg_duration_sec": 245.5,
    "avg_prompt_tokens": 3200,
    "avg_completion_tokens": 1500,
    "avg_total_tokens": 4700,
    "avg_cost_total_usd": 0.15
  },
  "chat_sessions": {
    "chat_abc123": {
      "duration_sec": 300.0,
      "prompt_tokens": 3500,
      "completion_tokens": 1600,
      "total_tokens": 5100,
      "cost_total_usd": 0.18
    },
    "chat_def456": {
      "duration_sec": 191.0,
      "prompt_tokens": 2900,
      "completion_tokens": 1400,
      "total_tokens": 4300,
      "cost_total_usd": 0.12
    }
  },
  "agents": {
    "interviewer": {
      "total_prompt_tokens": 1500,
      "total_completion_tokens": 800,
      "total_cost_usd": 0.06
    },
    "planner": {
      "total_prompt_tokens": 2100,
      "total_completion_tokens": 1200,
      "total_cost_usd": 0.09
    }
  }
}
```

**Real-Time Updates:** Updated live as PerformanceManager flushes metrics during workflow execution.

**Rollup Refresh:** Triggered asynchronously on `mark_chat_completed()` to recompute `overall_avg` from all chat sessions.

---

## AG2 Resume Patterns

### Pattern 1: Resume Existing Chat

**Scenario:** User refreshes browser, returns to chat, or second user joins.

**Steps:**
1. Load persisted messages from MongoDB
2. Normalize messages to strict AG2 format
3. Restore `ConversableContext` from `context_snapshot`
4. Seed AG2 `GroupChat.messages` with persisted messages
5. Run orchestration (AG2 continues from last turn)

**Code:**
```python
# Check if chat exists
existing = await persistence.load_chat_history(chat_id, app_id)

if existing:
    # Resume mode
    normalized = _normalize_to_strict_ag2(existing)
    group_chat.messages = normalized
    
    # Restore context
    context_data = await persistence.load_context_snapshot(chat_id, app_id)
    for key, value in context_data.items():
        context.set(key, value)
    
    # Run orchestration (no new initial message)
    await run_workflow_orchestration(
        chat_id=chat_id,
        workflow_name=workflow_name,
        initial_message=None,  # Signal resume mode
        ...
    )
else:
    # New chat mode
    await persistence.create_chat_session(chat_id, app_id, ...)
    await run_workflow_orchestration(
        chat_id=chat_id,
        initial_message="User's first message",
        ...
    )
```

---

### Pattern 2: Pause and Resume with User Input

**Scenario:** Workflow pauses for user input, user submits response, workflow resumes.

**Pause:**
```python
# Agent calls user_input tool
user_response = await user_input_tool(prompt="What features?")

# Tool internally:
# 1. Sends input_request event to frontend
# 2. Waits for response via transport.wait_for_response()
# 3. Returns user's input to agent
```

**Resume:**
```python
# Frontend submits input via WebSocket
{
  "type": "user.input.response",
  "request_id": "input_abc123",
  "value": "User's answer"
}

# Transport resolves future
# Agent resumes execution with user's input
```

**Persistence:**
```python
# User message appended to chat_sessions
await persistence.append_message(
    chat_id=chat_id,
    message={
        "role": "user",
        "name": "user",
        "content": "User's answer",
        ...
    }
)
```

**If browser refreshes during pause:**
1. Frontend reconnects WebSocket
2. Backend re-emits last `input_request` event (from orchestration state, not persisted)
3. User submits input (same flow)

---

### Pattern 3: Multi-User Chat Resume

**Scenario:** User A starts chat on desktop, User B joins on mobile.

**User A:**
1. Starts chat, sends messages
2. Messages persisted to MongoDB in real-time
3. Last artifact (e.g., code editor) stored via `update_last_artifact()`

**User B Joins:**
1. Opens chat URL: `/chat/{app_id}/{chat_id}`
2. Frontend fetches metadata: `GET /api/chats/{app_id}/{chat_id}/meta`
   ```json
   {
     "chat_id": "chat_abc123",
     "workflow_name": "Generator",
     "cache_seed": 2847561923,
     "last_artifact": {
       "ui_tool_id": "code_editor",
       "payload": { /* code content */ }
     }
   }
   ```
3. Frontend loads chat history: `GET /api/chats/{app_id}/{chat_id}/history`
4. Frontend restores artifact panel using `last_artifact`
5. WebSocket connects, receives new events in real-time

**Backend:**
- No special handling required (messages already persisted)
- Both users receive same event stream via WebSocket

---

## Indexes & Performance

**ChatSessions Indexes:**
```javascript
// Composite index for app queries
db.chat_sessions.createIndex(
  {"app_id": 1, "workflow_name": 1, "created_at": -1},
  {"name": "cs_ent_wf_created"}
)

// Status filter index
db.chat_sessions.createIndex(
  {"status": 1},
  {"name": "idx_status"}
)
```

**Query Performance:**
```javascript
// Fast: Uses cs_ent_wf_created index
db.chat_sessions.find({
  "app_id": "acme_corp",
  "workflow_name": "Generator"
}).sort({"created_at": -1}).limit(20)

// Fast: Direct _id lookup
db.chat_sessions.find_one({"_id": "chat_abc123"})

// Slow: Full collection scan (avoid in production)
db.chat_sessions.find({"user_id": "user_456"})
```

**Optimization Tips:**
1. Always include `app_id` in queries (indexed)
2. Use `_id` (chat_id) for single-document lookups
3. Limit returned fields: `{"messages": {"$slice": -50}}`
4. Archive completed chats older than 90 days (future enhancement)

---

## Error Handling

### Invalid App ID

```python
from core.data.persistence_manager import InvalidAppIdError

try:
    await persistence.create_chat_session(
        chat_id="chat_abc123",
        app_id="invalid_id",  # Not 24-char ObjectId
        ...
    )
except InvalidAppIdError as e:
    logger.error(f"Invalid app: {e}")
    # Return 400 Bad Request to client
```

---

### Duplicate Chat Session

```python
# MongoDB raises duplicate key error if chat_id already exists
try:
    await persistence.create_chat_session(chat_id="chat_abc123", ...)
except Exception as e:
    if "duplicate key" in str(e).lower():
        logger.warning(f"Chat {chat_id} already exists, resuming...")
        # Load existing chat and resume
    else:
        raise
```

---

### Message Append Failure

```python
try:
    await persistence.append_message(chat_id, message)
except Exception as e:
    logger.error(f"Failed to append message to {chat_id}: {e}")
    # Retry with exponential backoff (if transient failure)
    # Or emit business event for monitoring
```

---

## Best Practices

### 1. Always Persist Initial Messages

```python
# ✅ Correct
await persistence.create_chat_session(chat_id, ...)
await persistence.persist_initial_messages(
    chat_id=chat_id,
    messages=[{"role": "user", "name": "user", "content": initial_message}]
)

# ❌ Incorrect (initial message lost on resume)
await persistence.create_chat_session(chat_id, ...)
# Run orchestration without persisting initial message
```

---

### 2. Normalize Before Resume

```python
# ✅ Correct
raw_msgs = await persistence.load_chat_history(chat_id, app_id)
normalized = _normalize_to_strict_ag2(raw_msgs)
group_chat.messages = normalized

# ❌ Incorrect (AG2 resume fails with malformed messages)
raw_msgs = await persistence.load_chat_history(chat_id, app_id)
group_chat.messages = raw_msgs  # May contain invalid formats
```

---

### 3. Save Context Snapshot on Completion

```python
# ✅ Correct
try:
    result = await run_workflow_orchestration(...)
finally:
    await persistence.update_context_snapshot(
        chat_id=chat_id,
        context_data=context.to_dict()
    )
    await persistence.mark_chat_completed(chat_id, app_id)

# ❌ Incorrect (context lost on resume)
result = await run_workflow_orchestration(...)
await persistence.mark_chat_completed(chat_id, app_id)
# Context snapshot not saved
```

---

## Troubleshooting

### Issue: Chat resume loads incorrect messages

**Check:**
1. Verify `app_id` matches (cross-app queries blocked by filter)
2. Inspect `messages` array ordering: Should be sorted by `sequence` field
3. Check for duplicate messages: Look for identical `event_id` values

---

### Issue: AG2 resume fails with "Invalid message format"

**Solution:** Enable strict normalization and check logs for dropped messages:
```python
normalized = _normalize_to_strict_ag2(raw_msgs, default_user_name="user")
logger.info(f"Normalized {len(raw_msgs)} → {len(normalized)} messages")
# If counts differ, some messages were dropped due to invalid format
```

---

### Issue: Last artifact not restored on multi-user join

**Check:**
1. Verify `update_last_artifact()` called after artifact event emission
2. Inspect `/api/chats/{app_id}/{chat_id}/meta` response
3. Frontend: Check `last_artifact` field parsing and component mounting

---

## Next Steps

- **[Runtime Overview](runtime_overview.md)** - High-level runtime architecture
- **[Transport & Streaming](transport_and_streaming.md)** - WebSocket event delivery
- **[Observability](observability.md)** - Metrics tracking and logging
- **[MongoDB Schema Reference](../reference/mongodb_schema.md)** - Complete collection schemas

---

**Questions?** See [Troubleshooting Guide](../operations/troubleshooting.md) or [API Endpoints](../reference/api_endpoints.md).
