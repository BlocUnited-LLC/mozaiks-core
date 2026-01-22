# Inline Component Persistence Analysis

## Problem Statement

**User Scenario**: User interacts with inline UI components (Yes/No options, form inputs) during a chat session. When the user completes the interaction, the component disappears and shows a "✓ Completed" chip. However, when the user leaves and reconnects (WebSocket resume), the inline component state is lost:

1. **If user completed interaction before leaving**: On resume, the interactive component reappears instead of showing "Completed" chip
2. **If user left mid-interaction**: On resume, the interactive component should reappear (this part may already work)

**Why This Matters**: Inline components create critical decision points in the conversation flow. Users expect the chat to look identical after reconnection—completed tools should stay completed, in-progress tools should remain interactive.

---

## Current Architecture

### Frontend Flow (ChatPage.js + ChatInterface.js)

**1. UI Tool Event Arrives** (`chat.ui_tool` event):
```javascript
// ChatPage.js line ~1240
setMessagesWithLogging((prev) => [
  ...prev,
  {
    id: `ui-${eventId}`,
    sender: 'agent',
    content: payload.agent_message || '',
    uiToolEvent: {
      ui_tool_id,
      payload,
      eventId,
      workflow_name,
      display: displayMode || 'inline',  // 'inline' or 'artifact'
    },
  },
]);
```

**2. User Interacts and Submits Response**:
```javascript
// UIToolEventRenderer (ChatInterface.js line ~85)
const handleResponse = async (resp) => {
  await onResponse(resp);
  if (displayMode === 'inline') {
    setCompleted(true);  // Local state only - not persisted
  }
};
```

**3. Backend Sends Completion Event** (`chat.ui_tool_complete`):
```javascript
// ChatPage.js line ~658
case 'ui_tool_complete': {
  setMessagesWithLogging((prev) =>
    prev.map((msg) => {
      if (msg?.metadata?.eventId === completedId && msg?.metadata?.display === 'inline') {
        return {
          ...msg,
          ui_tool_completed: true,         // Mark as completed
          ui_tool_status: status,
          ui_tool_summary: `${completedTool} completed`
        };
      }
      return msg;
    })
  );
}
```

**4. UIToolEventRenderer Renders Completion State**:
```javascript
// ChatInterface.js line ~103
{completed && displayMode === 'inline' && (
  <span className="...">
    ✓ {uiToolEvent?.ui_tool_id || 'UI Tool'} completed
  </span>
)}
{!completed && (
  <UIToolRenderer event={uiToolEvent} onResponse={handleResponse} />
)}
```

### Backend Flow

**1. UI Tool Invocation** (ui_tools.py line ~160):
```python
async def request_user_input_ui_tool(...):
    # Send chat.ui_tool event to frontend
    await transport.send_event_to_ui({
        "type": "chat.ui_tool",
        "data": {
            "ui_tool_id": tool_id,
            "eventId": event_id,
            "payload": payload,
            "display": resolved_display  # 'inline' or 'artifact'
        }
    }, chat_id=chat_id)
    
    # Wait for user response (blocks until user interacts)
    resp = await _wait_for_ui_tool_response_internal(event_id, timeout=None)
```

**2. Auto-Vanish After Completion** (ui_tools.py line ~183):
```python
# After receiving response, send completion event for inline tools
if resolved_display == 'inline':
    completion_event = {
        "type": "chat.ui_tool_complete",
        "data": {
            "eventId": event_id,
            "ui_tool_id": tool_id,
            "display": "inline",
            "status": resp.get("status", "completed"),
            "summary": f"{tool_id} completed"
        }
    }
    await transport.send_event_to_ui(completion_event, chat_id=chat_id)
```

**3. Message Persistence** (persistence_manager.py line ~622):
```python
async def save_event(self, event: BaseEvent, chat_id: str, app_id: str):
    # Only TextEvent messages are persisted
    if not isinstance(event, TextEvent):
        return
    
    msg = {
        "role": role,
        "content": content_str,
        "timestamp": evt_ts,
        "event_type": "message.created",
        "event_id": event_id,
        "sequence": seq,
        "agent_name": raw_name,
        # ❌ PROBLEM: No metadata field to store ui_tool_completed flag
    }
    await coll.update_one(
        {"_id": chat_id, "app_id": app_id},
        {"$push": {"messages": msg}}
    )
```

**4. Resume Flow** (resume_groupchat.py line ~207):
```python
def _build_text_event(self, *, message: Dict[str, Any], index: int, chat_id: str):
    normalized = {
        "kind": "text",
        "agent": agent_name,
        "content": message.get("content", ""),
        "index": index,
        "replay": True,
    }
    metadata = message.get("metadata")
    if metadata:
        normalized["metadata"] = metadata  # Metadata is restored if present
    return normalized
```

---

## Root Cause: Missing Metadata Persistence

### Issue 1: UI Tool Completion Metadata Not Persisted

**Problem**: When `chat.ui_tool_complete` event updates the frontend message state with `ui_tool_completed: true`, this flag is only stored in React state (`setMessagesWithLogging`). It is **never sent back to the backend** for persistence.

**Flow Breakdown**:
1. Backend sends `chat.ui_tool` event → Frontend creates message with `uiToolEvent` object
2. User interacts → Backend receives response → Backend sends `chat.ui_tool_complete` event
3. Frontend updates message: `msg.ui_tool_completed = true`
4. **MISSING**: Frontend never persists this state to MongoDB
5. User disconnects and reconnects
6. Backend replays messages from MongoDB
7. **PROBLEM**: Replayed message lacks `ui_tool_completed` flag → Component renders as interactive again

### Issue 2: UI Tool Event Not Represented as Persistent Message

**Problem**: The `chat.ui_tool` event that creates the interactive component is **not a TextEvent** from AG2's perspective. It's a runtime coordination event. Therefore:

- `persistence_manager.save_event()` only persists `TextEvent` instances (line ~513)
- `chat.ui_tool` events are never stored in MongoDB `messages` array
- On resume, there's no record that a UI tool was ever presented

**Current Workaround**: The agent message that triggered the UI tool (e.g., "Let me get your confirmation on this...") might be stored as a TextEvent, but it doesn't carry the `uiToolEvent` metadata.

---

## Solution Design

### Approach 1: Store UI Tool Metadata in Agent Messages ✅ RECOMMENDED

**Concept**: When an agent invokes a UI tool, attach metadata to the agent's TextEvent message that includes:
- `ui_tool_id`: Tool identifier
- `event_id`: Correlation ID for responses
- `display`: "inline" or "artifact"
- `ui_tool_completed`: Boolean completion flag (updated after completion event)
- `ui_tool_status`: "completed", "dismissed", etc.
- `payload`: Tool configuration (options, title, description)

**Implementation Steps**:

#### Step 1: Attach UI Tool Metadata to Agent Messages

**File**: `core/workflow/outputs/ui_tools.py`

```python
async def request_user_input_ui_tool(...):
    # Generate event_id
    event_id = str(uuid4())
    
    # Send ui_tool event to frontend (existing)
    await transport.send_event_to_ui({
        "type": "chat.ui_tool",
        "data": {...}
    }, chat_id=chat_id)
    
    # NEW: Also store metadata in the most recent agent message
    try:
        from core.data.persistence import persistence_manager as pm
        await pm.attach_ui_tool_metadata(
            chat_id=chat_id,
            app_id=app_id,
            event_id=event_id,
            metadata={
                "ui_tool_id": tool_id,
                "event_id": event_id,
                "display": resolved_display,
                "ui_tool_completed": False,
                "payload": payload,
                "timestamp": datetime.now(UTC).isoformat()
            }
        )
    except Exception as meta_err:
        wf_logger.warning(f"Failed to attach UI tool metadata: {meta_err}")
    
    # Wait for response (existing)
    resp = await _wait_for_ui_tool_response_internal(event_id, timeout=None)
    
    # After completion, update metadata (existing completion event + NEW persistence)
    if resolved_display == 'inline':
        # Send completion event (existing)
        await transport.send_event_to_ui({
            "type": "chat.ui_tool_complete",
            ...
        }, chat_id=chat_id)
        
        # NEW: Update persisted metadata
        try:
            await pm.update_ui_tool_completion(
                chat_id=chat_id,
                app_id=app_id,
                event_id=event_id,
                completed=True,
                status=resp.get("status", "completed")
            )
        except Exception as update_err:
            wf_logger.warning(f"Failed to update UI tool completion: {update_err}")
```

#### Step 2: Add Persistence Methods

**File**: `core/data/persistence/persistence_manager.py`

```python
async def attach_ui_tool_metadata(
    self,
    *,
    chat_id: str,
    app_id: str,
    event_id: str,
    metadata: Dict[str, Any]
) -> None:
    """Attach UI tool metadata to the most recent agent message.
    
    This enables UI tool state to persist across reconnections.
    When a UI tool is invoked, we store its configuration and state
    in the last agent message's metadata field.
    """
    try:
        coll = await self._coll()
        
        # Find the most recent assistant message and add ui_tool_metadata
        result = await coll.update_one(
            {
                "_id": chat_id,
                "app_id": app_id,
            },
            {
                "$set": {
                    "messages.$[lastMsg].metadata": {
                        "ui_tool": metadata
                    }
                }
            },
            array_filters=[
                {"lastMsg.role": "assistant"}
            ],
            # Update only the last matching message
        )
        
        if result.modified_count > 0:
            logger.debug(f"[UI_TOOL_METADATA] Attached to last message in {chat_id}")
        else:
            # Fallback: append as separate metadata message
            logger.warning(f"[UI_TOOL_METADATA] No recent assistant message found, storing separately")
            await coll.update_one(
                {"_id": chat_id, "app_id": app_id},
                {
                    "$push": {
                        "messages": {
                            "role": "system",
                            "content": "",
                            "event_type": "ui_tool_metadata",
                            "event_id": event_id,
                            "timestamp": datetime.now(UTC),
                            "metadata": {"ui_tool": metadata},
                            "sequence": -1  # System messages don't need sequence
                        }
                    }
                }
            )
    except Exception as e:
        logger.error(f"[UI_TOOL_METADATA] Failed to attach metadata: {e}", exc_info=True)


async def update_ui_tool_completion(
    self,
    *,
    chat_id: str,
    app_id: str,
    event_id: str,
    completed: bool,
    status: str
) -> None:
    """Update UI tool completion status in persisted message metadata.
    
    Called after a UI tool interaction completes to mark the tool as done.
    """
    try:
        coll = await self._coll()
        
        result = await coll.update_one(
            {
                "_id": chat_id,
                "app_id": app_id,
                "messages.metadata.ui_tool.event_id": event_id
            },
            {
                "$set": {
                    "messages.$[elem].metadata.ui_tool.ui_tool_completed": completed,
                    "messages.$[elem].metadata.ui_tool.ui_tool_status": status,
                    "messages.$[elem].metadata.ui_tool.completed_at": datetime.now(UTC).isoformat()
                }
            },
            array_filters=[
                {"elem.metadata.ui_tool.event_id": event_id}
            ]
        )
        
        if result.modified_count > 0:
            logger.info(f"[UI_TOOL_COMPLETE] Updated completion for {event_id} in {chat_id}")
        else:
            logger.warning(f"[UI_TOOL_COMPLETE] No message found with event_id={event_id}")
    except Exception as e:
        logger.error(f"[UI_TOOL_COMPLETE] Failed to update: {e}", exc_info=True)
```

#### Step 3: Restore UI Tool State on Resume

**File**: `core/transport/resume_groupchat.py`

```python
def _build_text_event(self, *, message: Dict[str, Any], index: int, chat_id: str) -> Dict[str, Any]:
    role = message.get("role")
    agent_name = message.get("agent_name") or message.get("name") or "assistant"
    
    normalized = {
        "kind": "text",
        "agent": agent_name,
        "role": role or "user",
        "content": message.get("content", ""),
        "index": index,
        "chat_id": chat_id,
        "replay": True,
        "timestamp": message.get("timestamp") or datetime.now(timezone.utc).isoformat(),
    }
    
    # Restore metadata (existing)
    metadata = message.get("metadata")
    if metadata:
        normalized["metadata"] = metadata
        
        # NEW: If message has ui_tool metadata, reconstruct uiToolEvent
        ui_tool_meta = metadata.get("ui_tool")
        if ui_tool_meta:
            normalized["uiToolEvent"] = {
                "ui_tool_id": ui_tool_meta.get("ui_tool_id"),
                "eventId": ui_tool_meta.get("event_id"),
                "payload": ui_tool_meta.get("payload", {}),
                "display": ui_tool_meta.get("display", "inline"),
                "workflow_name": message.get("workflow_name"),  # May be in top-level
            }
            # CRITICAL: Surface completion state to frontend
            normalized["ui_tool_completed"] = ui_tool_meta.get("ui_tool_completed", False)
            normalized["ui_tool_status"] = ui_tool_meta.get("ui_tool_status", "pending")
    
    return normalized
```

#### Step 4: Frontend Handles Restored UI Tool Events

**File**: `ChatUI/src/components/chat/ChatInterface.js`

**No changes needed!** The `UIToolEventRenderer` already accepts `isCompleted` prop:

```javascript
<UIToolEventRenderer
  uiToolEvent={chat.uiToolEvent}
  onResponse={handleUIResponse}
  submitInputRequest={submitInputRequest}
  isCompleted={chat.ui_tool_completed || false}  // Already implemented!
/>
```

The component checks `isCompleted` and renders the "✓ Completed" chip instead of the interactive component.

---

### Approach 2: Separate UI Tool Event Collection (Not Recommended)

**Concept**: Create a separate MongoDB collection `UIToolEvents` to track inline component state independently from chat messages.

**Pros**:
- Clean separation of concerns
- Easier to query all pending UI tools
- Doesn't pollute message metadata

**Cons**:
- Requires additional queries during resume (performance impact)
- Complicates correlation between messages and UI state
- More complex to maintain consistency across two collections
- Doesn't align with current architecture (messages are the source of truth)

**Verdict**: Not recommended. Metadata approach is simpler and aligns with existing patterns.

---

## Testing Checklist

### Scenario 1: Inline Component Completed Before Disconnect

1. Start Generator workflow
2. Agent presents ActionPlanApprovalForm (inline component)
3. User clicks "Approve" → Component disappears, shows "✓ ActionPlanApprovalForm completed"
4. User closes browser
5. User returns, reconnects to same chat
6. **Expected**: Chat shows "✓ ActionPlanApprovalForm completed" chip, no interactive component
7. **Verify MongoDB**: Check `messages` array has metadata.ui_tool.ui_tool_completed: true

### Scenario 2: Inline Component In-Progress During Disconnect

1. Start Generator workflow
2. Agent presents RevisionRequestForm (inline component)
3. User types in text field but doesn't submit
4. User closes browser (form still visible)
5. User returns, reconnects to same chat
6. **Expected**: RevisionRequestForm reappears in same state (empty or partially filled, depending on local storage)
7. **Note**: Text field values are client-side only; backend only tracks completion state

### Scenario 3: Multiple Inline Components

1. Agent presents inline component #1 (Yes/No confirmation)
2. User clicks "Yes" → Shows "✓ completed"
3. Agent presents inline component #2 (Form input)
4. User disconnects
5. User reconnects
6. **Expected**:
   - Component #1: Shows "✓ completed" chip
   - Component #2: Shows interactive form (in-progress)

### Scenario 4: Artifact vs Inline Display Modes

1. Agent presents artifact (display: "artifact") → Opens side panel
2. Agent presents inline component (display: "inline") → Shows in chat
3. User completes inline component
4. User disconnects and reconnects
5. **Expected**:
   - Artifact: Restored in side panel (existing artifact persistence handles this)
   - Inline: Shows "✓ completed" chip in chat (new persistence)

---

## MongoDB Schema Example

### Before Implementation (Current State)
```json
{
  "_id": "chat-123",
  "app_id": "ent-456",
  "messages": [
    {
      "role": "assistant",
      "agent_name": "ActionPlanArchitect",
      "content": "Here's your action plan. Do you approve?",
      "event_id": "evt-789",
      "sequence": 5,
      "timestamp": "2025-01-15T10:30:00Z"
    }
  ]
}
```
**Problem**: No record that a UI tool was presented, no completion state.

### After Implementation (With Metadata)
```json
{
  "_id": "chat-123",
  "app_id": "ent-456",
  "messages": [
    {
      "role": "assistant",
      "agent_name": "ActionPlanArchitect",
      "content": "Here's your action plan. Do you approve?",
      "event_id": "evt-789",
      "sequence": 5,
      "timestamp": "2025-01-15T10:30:00Z",
      "metadata": {
        "ui_tool": {
          "ui_tool_id": "ActionPlanApprovalForm",
          "event_id": "tool-evt-abc",
          "display": "inline",
          "ui_tool_completed": true,
          "ui_tool_status": "completed",
          "completed_at": "2025-01-15T10:32:00Z",
          "payload": {
            "title": "Approve Action Plan",
            "options": ["Approve", "Request Revisions"],
            "agent_message": "Review the plan and choose an option."
          }
        }
      }
    }
  ]
}
```
**Solution**: Full UI tool state preserved in message metadata.

---

## Implementation Priority

1. **High Priority**: Add `attach_ui_tool_metadata` and `update_ui_tool_completion` to persistence_manager.py
2. **High Priority**: Modify `request_user_input_ui_tool` to call these methods
3. **High Priority**: Update `_build_text_event` in resume_groupchat.py to reconstruct uiToolEvent
4. **Medium Priority**: Add logging for UI tool persistence operations
5. **Low Priority**: Create admin endpoint to view all pending UI tools (for debugging)

---

## Alternative Considerations

### Should UI Tool Response Data Be Persisted?

**Question**: When a user submits a form (e.g., "Request revisions: Please add more details"), should we store the response data?

**Current State**: The response is sent to the agent via `_wait_for_ui_tool_response_internal`, which returns the data. The agent may then send a message like "Thank you, I'll revise the plan..." which gets persisted as a normal TextEvent.

**Recommendation**: No need to persist raw UI tool responses separately. The agent's follow-up message captures the outcome, and structured outputs (if used) will store the parsed data. Persisting completion state (`ui_tool_completed: true`) is sufficient for UX consistency on reconnect.

---

## Edge Cases

### Case 1: User Responds After Reconnection

**Scenario**: User disconnects mid-interaction, reconnects, sees the interactive component (because not completed), then submits response.

**Expected Behavior**: Should work correctly because:
- Backend still has `_wait_for_ui_tool_response_internal` waiting for response
- Response futures are keyed by `event_id` which is stable
- Frontend will send response with same `event_id`

**Verify**: Test that backend doesn't timeout waiting for response after reconnect.

### Case 2: Backend Crashes Before Completion Event

**Scenario**: User submits response → Backend receives it → Backend crashes before sending `chat.ui_tool_complete` event.

**Expected Behavior**: 
- On backend restart, the message won't have `ui_tool_completed: true`
- On reconnect, the interactive component will reappear (user can resubmit)
- Agent workflow will have processed the response already (idempotency concern)

**Recommendation**: Add idempotency check in agent workflows to handle duplicate UI tool responses gracefully.

### Case 3: Multiple Reconnections

**Scenario**: User reconnects multiple times during a session.

**Expected Behavior**: Each reconnection should replay messages with consistent state. Completion chips should appear for all completed tools, interactive components for in-progress tools.

**Verify**: Test rapid disconnect/reconnect cycles.

---

## Success Criteria

- ✅ User completes inline component → Disconnects → Reconnects → Sees "✓ Completed" chip (not interactive component)
- ✅ User leaves mid-interaction → Reconnects → Sees interactive component (can complete interaction)
- ✅ Multiple inline components persist independently
- ✅ Artifact display mode persistence unaffected (side panel continues working)
- ✅ MongoDB messages array includes ui_tool metadata for all UI tool invocations
- ✅ No performance degradation during resume (metadata adds minimal overhead)
- ✅ Logs clearly show UI tool persistence operations for debugging

---

## Open Questions

1. **Timeout Handling**: What happens if a UI tool times out (user never responds)? Should we mark it as "expired" in metadata?
   - **Current**: Backend waits indefinitely (`timeout=None` in `_wait_for_ui_tool_response_internal`)
   - **Recommendation**: Add timeout parameter and mark expired tools in metadata

2. **Dismissal Events**: Should `chat.ui_tool_dismiss` also update metadata?
   - **Current**: Dismissal removes the message from frontend state
   - **Recommendation**: Store dismissal state in metadata (`ui_tool_dismissed: true`) for audit trail

3. **Artifact UI Tools**: Do artifact-mode UI tools (display: "artifact") need the same persistence?
   - **Current**: Artifacts have separate persistence via ArtifactInstances collection
   - **Recommendation**: Yes, add metadata for consistency, but artifact state is primary for artifact-mode tools

---

## Next Steps

1. Implement `attach_ui_tool_metadata` and `update_ui_tool_completion` in persistence_manager.py
2. Integrate calls in ui_tools.py (around line 160-200)
3. Update `_build_text_event` in resume_groupchat.py to reconstruct uiToolEvent from metadata
4. Test all scenarios in testing checklist
5. Add structured logging for UI tool lifecycle (invoked → completed → restored on resume)
6. Document in runtime docs (add INLINE_COMPONENT_PERSISTENCE.md)
