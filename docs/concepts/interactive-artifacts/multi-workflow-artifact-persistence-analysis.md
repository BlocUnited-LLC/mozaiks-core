# Multi-Workflow Navigation & Artifact Persistence Analysis

## Scenario: User Navigation Flow

**User Journey**:
1. User starts **Generator** workflow → receives **ActionPlan** artifact
2. User navigates to **MozaiksCapital** (investor portal) → starts new chat asking about investments → receives **InvestmentData** artifact
3. User wants to return to **Generator** workflow → expects to see ActionPlan artifact and previous chat history

## Question: Does persistence work correctly?

### Answer: **YES, BUT with important caveats** ✅⚠️

---

## How It Currently Works

### 1. **Multiple IN_PROGRESS Sessions** ✅

From `core/workflow/session_manager.py`:
```python
# Users can have multiple IN_PROGRESS sessions simultaneously
# All sessions stay IN_PROGRESS until completed
# Resume is automatic when reconnecting to an existing chat_id
```

**What this means**:
- Generator session (`chat_123`) remains **IN_PROGRESS** with ActionPlan artifact attached
- MozaiksCapital session (`chat_456`) is created as **separate** IN_PROGRESS session with InvestmentData artifact
- Both sessions persist independently in `ChatSessions` collection
- Both artifacts persist independently in `ArtifactInstances` collection (if using session_manager)

### 2. **Artifact Persistence** ✅

Two persistence mechanisms exist:

#### A. **UI Tool Artifacts** (via `last_artifact`)
From `persistence_manager.py`:
```python
async def update_last_artifact(
    self, *, chat_id: str, app_id: str, artifact: Dict[str, Any]
) -> None:
    """Persist latest artifact/tool panel context for multi-user resume.
    
    - Only the most recent artifact-mode UI tool event is stored (overwrite strategy).
    - Frontend uses /api/chats/meta and websocket chat_meta (last_artifact field) 
      to restore the panel when a second user joins or a browser refresh occurs.
    """
```

**Location**: Stored directly in `ChatSessions` document under `last_artifact` field

**Retrieval**: 
- HTTP: `GET /api/chats/meta/{app_id}/{workflow_name}/{chat_id}`
- Returns: `{exists: true, chat_id, workflow_name, cache_seed, last_artifact: {...}}`

#### B. **Workflow Session Artifacts** (via `ArtifactInstances` collection)
From `session_manager.py`:
```python
async def create_artifact_instance(
    app_id: str,
    workflow_name: str,
    artifact_type: str,
    initial_state: Optional[Dict[str, Any]] = None
) -> Dict[str, Any]:
    """Create a persistent ArtifactInstance storing artifact state (JSON blob)."""
```

**Location**: Separate `ArtifactInstances` collection with full state object

**Retrieval**: Via `get_artifact_instance(artifact_id, app_id)`

### 3. **Chat History Persistence** ✅

From `resume_groupchat.py` and `simple_transport.py`:
- All messages stored in `ChatSessions.messages[]` array
- When user reconnects to `chat_123` (Generator), `_auto_resume_if_needed()` replays all messages
- Messages include proper `replay: true` flag during restore

### 4. **Frontend Artifact Restoration** ✅

From `ChatPage.js`:
```javascript
// Backend sends chat_meta event with last_artifact on connection
if (data.last_artifact && data.last_artifact.ui_tool_id) {
  const key = `mozaiks.last_artifact.${currentChatId}`;
  localStorage.setItem(key, JSON.stringify({
    ui_tool_id: data.last_artifact.ui_tool_id,
    eventId: data.last_artifact.event_id || null,
    workflow_name: data.last_artifact.workflow_name || currentWorkflowName,
    payload: data.last_artifact.payload || {},
    display: data.last_artifact.display || 'artifact',
    ts: Date.now(),
  }));
}
```

---

## Potential Issues & Edge Cases ⚠️

### Issue 1: **Artifact Type Matters**

#### If Generator uses `update_last_artifact()` (UI tool approach):
✅ **Works**: ActionPlan will be restored from `last_artifact` field
- Stored: `ChatSessions[chat_123].last_artifact = {ui_tool_id, payload: {...actionplan...}}`
- Retrieved: Automatically sent in `chat_meta` event on reconnect

#### If Generator uses `ArtifactInstances` (session_manager approach):
⚠️ **Partial**: Artifact state persists but frontend must explicitly request it
- Stored: `ArtifactInstances[artifact_abc] = {state: {...actionplan...}, last_active_chat_id: chat_123}`
- Retrieved: Requires explicit call to backend to fetch artifact by ID
- **Problem**: Frontend doesn't automatically know the artifact_id when reconnecting to chat_123

### Issue 2: **Frontend Doesn't Track chat_id → artifact_id Mapping**

When user returns to Generator workflow:

1. Frontend knows `chat_id = chat_123`
2. Frontend calls `/api/chats/meta/{app_id}/Generator/{chat_123}`
3. Backend returns `last_artifact` (if it exists)
4. **BUT**: If artifact is stored in `ArtifactInstances` collection, the `last_artifact` field might be null/outdated

**Current Flow**:
```
User returns to chat_123
  ↓
Frontend: GET /api/chats/meta/.../chat_123
  ↓
Backend: Returns {last_artifact: {...}} ← Only if update_last_artifact() was called
  ↓
Frontend: Restores artifact panel
```

**Missing Flow**:
```
User returns to chat_123
  ↓
Frontend needs: What artifact_id is associated with this chat?
  ↓
Backend: Need to check WorkflowSessions[chat_123].artifact_instance_id
  ↓
Backend: Then fetch ArtifactInstances[artifact_abc]
  ↓
Frontend: Restore artifact panel
```

### Issue 3: **chat_meta Doesn't Include WorkflowSession Info**

From `shared_app.py`:
```python
@app.get("/api/chats/meta/{app_id}/{workflow_name}/{chat_id}")
async def chat_meta(app_id: str, workflow_name: str, chat_id: str):
    coll = await _chat_coll()  # ← Gets ChatSessions
    projection = {"cache_seed": 1, "last_artifact": 1, "_id": 1, "workflow_name": 1}
    doc = await coll.find_one({"_id": chat_id, ...}, projection)
    return {
        "exists": True,
        "chat_id": chat_id,
        "workflow_name": workflow_name,
        "cache_seed": doc.get("cache_seed"),
        "last_artifact": doc.get("last_artifact"),  # ← Only this
        # ❌ Missing: artifact_instance_id from WorkflowSessions
    }
```

**Problem**: If Generator workflow uses `WorkflowSessions` + `ArtifactInstances` pattern (which it should for multi-app scenarios), the artifact_instance_id link is NOT returned in chat_meta.

---

## Recommended Fixes

### Fix 1: **Update chat_meta endpoint to include artifact_instance_id**

```python
@app.get("/api/chats/meta/{app_id}/{workflow_name}/{chat_id}")
async def chat_meta(app_id: str, workflow_name: str, chat_id: str):
    try:
        # Get from ChatSessions
        coll = await _chat_coll()
        projection = {"cache_seed": 1, "last_artifact": 1, "_id": 1, "workflow_name": 1}
        doc = await coll.find_one({"_id": chat_id, "app_id": eid, "workflow_name": workflow_name}, projection)
        
        # ALSO get from WorkflowSessions
        from core.workflow import session_manager
        workflow_session = await session_manager.get_workflow_session(chat_id, str(eid))
        artifact_instance_id = None
        artifact_state = None
        
        if workflow_session and workflow_session.get("artifact_instance_id"):
            artifact_instance_id = workflow_session["artifact_instance_id"]
            # Optionally fetch full artifact state
            artifact_doc = await session_manager.get_artifact_instance(artifact_instance_id, str(eid))
            if artifact_doc:
                artifact_state = artifact_doc.get("state")
        
        return {
            "exists": True,
            "chat_id": chat_id,
            "workflow_name": workflow_name,
            "cache_seed": doc.get("cache_seed"),
            "last_artifact": doc.get("last_artifact"),  # UI tool artifacts
            "artifact_instance_id": artifact_instance_id,  # ← NEW
            "artifact_state": artifact_state,  # ← NEW (optional, can be large)
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to load chat meta: {e}")
```

### Fix 2: **Update Frontend to Request Artifact State**

In `ChatPage.js`:
```javascript
// After connecting to chat_123
const metaResponse = await fetch(`/api/chats/meta/${appId}/${workflowName}/${chatId}`);
const meta = await metaResponse.json();

if (meta.exists) {
  // Restore UI tool artifact (existing logic)
  if (meta.last_artifact) {
    localStorage.setItem(`mozaiks.last_artifact.${chatId}`, JSON.stringify(meta.last_artifact));
  }
  
  // NEW: Restore WorkflowSession artifact
  if (meta.artifact_instance_id) {
    // Option A: State already in meta response
    if (meta.artifact_state) {
      restoreArtifactFromState(meta.artifact_state, meta.artifact_instance_id);
    }
    // Option B: Fetch separately (if state too large for meta)
    else {
      const artifactResponse = await fetch(`/api/artifacts/${appId}/${meta.artifact_instance_id}`);
      const artifact = await artifactResponse.json();
      restoreArtifactFromState(artifact.state, meta.artifact_instance_id);
    }
  }
}
```

### Fix 3: **Ensure Generator Uses Dual Persistence**

Generator workflow should:
1. Use `session_manager.create_artifact_instance()` for persistent ActionPlan state
2. ALSO call `update_last_artifact()` for quick UI restoration

```python
# In Generator workflow tool
from core.workflow import session_manager

# Create persistent artifact instance
artifact = await session_manager.create_artifact_instance(
    app_id=app_id,
    workflow_name="Generator",
    artifact_type="ActionPlan",
    initial_state={"workflow": action_plan_data}
)

# Attach to workflow session
await session_manager.attach_artifact_to_session(
    chat_id=chat_id,
    artifact_id=artifact["_id"],
    app_id=app_id
)

# ALSO persist as last_artifact for quick UI restore
await persistence_manager.update_last_artifact(
    chat_id=chat_id,
    app_id=app_id,
    artifact={
        "ui_tool_id": "ActionPlan",
        "event_id": event_id,
        "display": "artifact",
        "workflow_name": "Generator",
        "payload": action_plan_data  # Can be simplified/summarized version
    }
)
```

---

## Current State Assessment

### ✅ **What Works**:
1. Chat history persists across navigation
2. UI tool artifacts (via `last_artifact`) persist and restore
3. Multiple IN_PROGRESS sessions work correctly
4. Resume flow filters AgentDriven initial messages (after our fix)

### ⚠️ **What Needs Fixing**:
1. `chat_meta` endpoint doesn't return `artifact_instance_id` from WorkflowSessions
2. Frontend doesn't have logic to restore artifacts from ArtifactInstances collection
3. Generator workflow may not be using dual persistence (both `last_artifact` AND `ArtifactInstances`)

### ❌ **What Doesn't Work** (if using ArtifactInstances only):
- User navigates away from Generator → returns → ActionPlan artifact is NOT automatically restored because:
  - `chat_meta` doesn't include `artifact_instance_id`
  - Frontend doesn't know to fetch from ArtifactInstances
  - Only `last_artifact` (UI tool approach) auto-restores

---

## Testing Checklist

- [ ] Start Generator workflow → see ActionPlan artifact
- [ ] Navigate to MozaiksCapital → start new chat → see InvestmentData artifact
- [ ] Return to Generator chat_id → **Verify ActionPlan artifact restores**
- [ ] Check browser console: Does `chat_meta` response include `artifact_instance_id`?
- [ ] Check MongoDB: Does `WorkflowSessions[chat_123]` have `artifact_instance_id` populated?
- [ ] Check MongoDB: Does `ArtifactInstances[artifact_abc]` exist with ActionPlan state?
- [ ] Verify chat history messages restored correctly (no duplicate UserProxy initial message)
- [ ] Verify structured outputs match across sessions

---

## Recommendation

**Implement Fix 1 (chat_meta enhancement) immediately** - this is critical for multi-workflow navigation with artifacts to work correctly.

Without this, users will see their chat history when returning to Generator, but the ActionPlan artifact panel will NOT restore unless Generator also calls `update_last_artifact()` (which may not be the pattern for all workflows).

---

## Frontend Alignment (Nov 2025)

Recent ChatUI changes keep the UI in sync with these persistence rules:

- `ChatPage.js` now fetches `/api/sessions/list/{app_id}/{user_id}` on load and whenever modes change, storing the ordered (oldest-first) list of `IN_PROGRESS` workflow sessions inside `ChatUIContext.workflowSessions`.
- Toggling from Ask Mozaiks back to workflow mode uses the cached session list when no `currentChatId` is present, defaulting to the oldest active workflow to guarantee deterministic restoration.
- The persistent chat widget follows the same logic when the user toggles from `/workflows` or other routes: it navigates back to `/chat`, sends `chat.switch_workflow` for the resolved chat_id, and updates context so artifacts/UI snapshots can replay.
- Ask-mode activation now refreshes both general chat sessions and workflow sessions, ensuring the frontend never points at a stale chat_id (critical when users complete or delete workflows outside the active tab).

**Operational guardrails**

- Backend must mark sessions `status = IN_PROGRESS` until workflows are explicitly completed so that the resolver can discover them.
- `list_user_sessions` should keep `last_updated_at` (or `created_at`) populated; the frontend normalizes timestamps but relies on monotonic values to pick the “oldest” session when multiple are active.
- When multiple dependencies exist (e.g., Generator → Investor), workflows should expose `last_artifact` metadata or artifact-instance references so returning to the oldest session still surfaces the correct artifact resume point.

These changes keep persistence, transport, and UI orchestration aligned without adding workflow-specific logic to the runtime.
