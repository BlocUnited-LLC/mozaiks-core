# Inline Component Persistence — Implementation Guide

## Overview

**Problem Solved**: Inline UI components (Yes/No buttons, approval forms, input fields) now persist their completion state across WebSocket reconnections. When a user completes an interaction and disconnects, the chat history will show a "✓ Completed" chip instead of re-rendering the interactive component.

**Key Principle**: UI tool metadata is stored in MongoDB alongside agent messages, enabling the frontend to reconstruct the exact visual state on resume.

---

## Architecture Flow

### 1. UI Tool Invocation (Agent → Backend → Frontend)

```
Agent invokes UI tool
  ↓
ui_tools.py: request_user_input_ui_tool()
  ↓
_emit_ui_tool_event_core() → Sends chat.ui_tool event to frontend
  ↓
persistence_manager.attach_ui_tool_metadata() → Stores metadata in MongoDB
  ↓
Frontend receives chat.ui_tool → Renders interactive component
```

**MongoDB State After Invocation**:
```json
{
  "messages": [
    {
      "role": "assistant",
      "agent_name": "ActionPlanArchitect",
      "content": "Please review and approve the action plan.",
      "metadata": {
        "ui_tool": {
          "ui_tool_id": "ActionPlanApprovalForm",
          "event_id": "tool-evt-abc123",
          "display": "inline",
          "ui_tool_completed": false,  // Not yet completed
          "payload": {
            "title": "Approve Action Plan",
            "options": ["Approve", "Request Revisions"]
          },
          "timestamp": "2025-01-15T10:30:00Z"
        }
      }
    }
  ]
}
```

---

### 2. User Interaction & Completion (Frontend → Backend)

```
User clicks button / submits form
  ↓
Frontend: UIToolRenderer calls onResponse(response)
  ↓
Backend: _wait_for_ui_tool_response_internal() receives response
  ↓
ui_tools.py: Sends chat.ui_tool_complete event to frontend
  ↓
persistence_manager.update_ui_tool_completion() → Updates MongoDB
  ↓
Frontend: Updates message state, shows "✓ Completed" chip
```

**MongoDB State After Completion**:
```json
{
  "messages": [
    {
      "role": "assistant",
      "agent_name": "ActionPlanArchitect",
      "content": "Please review and approve the action plan.",
      "metadata": {
        "ui_tool": {
          "ui_tool_id": "ActionPlanApprovalForm",
          "event_id": "tool-evt-abc123",
          "display": "inline",
          "ui_tool_completed": true,  // ✅ Completed!
          "ui_tool_status": "completed",
          "completed_at": "2025-01-15T10:32:00Z",
          "payload": { ... }
        }
      }
    }
  ]
}
```

---

### 3. Resume Flow (Reconnect → Restore State)

```
User disconnects and reconnects
  ↓
WebSocket: auto_resume_if_needed() triggered
  ↓
resume_groupchat.py: _replay_messages() → Fetches messages from MongoDB
  ↓
_build_text_event() → Checks for metadata.ui_tool
  ↓
If ui_tool metadata exists:
  - Reconstruct uiToolEvent object
  - Set ui_tool_completed flag from metadata
  ↓
Frontend: UIToolEventRenderer receives isCompleted=true
  ↓
Renders "✓ ActionPlanApprovalForm completed" chip (no interactive component)
```

**Resume Event Payload**:
```json
{
  "kind": "text",
  "agent": "ActionPlanArchitect",
  "content": "Please review and approve the action plan.",
  "metadata": {
    "ui_tool": { ... }
  },
  "uiToolEvent": {
    "ui_tool_id": "ActionPlanApprovalForm",
    "eventId": "tool-evt-abc123",
    "payload": { ... },
    "display": "inline"
  },
  "ui_tool_completed": true,  // Frontend checks this flag
  "ui_tool_status": "completed",
  "replay": true
}
```

---

## Code Implementation

### Backend: persistence_manager.py

**New Methods**:

1. **`attach_ui_tool_metadata()`** — Called when UI tool is invoked
   - Finds the most recent assistant message
   - Attaches `metadata.ui_tool` with tool configuration
   - Initial state: `ui_tool_completed: false`

2. **`update_ui_tool_completion()`** — Called after user responds
   - Finds message by `event_id`
   - Updates `ui_tool_completed: true` and `ui_tool_status`
   - Adds `completed_at` timestamp

**Key Design Decision**: Store metadata in the **last agent message** before the UI tool event, not as a separate system message. This keeps the chat history clean and avoids confusion about message ordering.

---

### Backend: ui_tools.py

**Modified**: `request_user_input_ui_tool()`

**Added Persistence Hooks**:

```python
# After emitting UI tool event to frontend
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

# After receiving user response (inline tools only)
if resolved_display == 'inline':
    # Send completion event to frontend (existing)
    await transport.send_event_to_ui(completion_event, chat_id=chat_id)
    
    # NEW: Persist completion state to MongoDB
    await pm.update_ui_tool_completion(
        chat_id=chat_id,
        app_id=app_id,
        event_id=event_id,
        completed=True,
        status=resp.get("status", "completed")
    )
```

**Why Two Separate Calls?**:
- `attach_ui_tool_metadata()`: Initial state (tool invoked, not yet completed)
- `update_ui_tool_completion()`: Final state (user responded, tool completed)

This enables proper state tracking if the user disconnects mid-interaction.

---

### Backend: resume_groupchat.py

**Modified**: `_build_text_event()`

**Added UI Tool State Restoration**:

```python
metadata = message.get("metadata")
if metadata:
    normalized["metadata"] = metadata
    
    # Check for ui_tool metadata
    ui_tool_meta = metadata.get("ui_tool")
    if ui_tool_meta and isinstance(ui_tool_meta, dict):
        # Reconstruct uiToolEvent for frontend
        normalized["uiToolEvent"] = {
            "ui_tool_id": ui_tool_meta.get("ui_tool_id"),
            "eventId": ui_tool_meta.get("event_id"),
            "payload": ui_tool_meta.get("payload", {}),
            "display": ui_tool_meta.get("display", "inline"),
        }
        # CRITICAL: Surface completion state
        normalized["ui_tool_completed"] = ui_tool_meta.get("ui_tool_completed", False)
        normalized["ui_tool_status"] = ui_tool_meta.get("ui_tool_status", "pending")
```

**Why This Works**: The frontend `UIToolEventRenderer` already checks `isCompleted` prop:

```javascript
// ChatInterface.js line ~103
{completed && displayMode === 'inline' && (
  <span className="...">✓ {uiToolEvent?.ui_tool_id} completed</span>
)}
{!completed && (
  <UIToolRenderer event={uiToolEvent} onResponse={handleResponse} />
)}
```

No frontend changes needed! The existing code already handles the `ui_tool_completed` flag.

---

## Display Modes: Inline vs Artifact

### Inline Components (`display: "inline"`)

**Examples**: Yes/No buttons, short forms, confirmations

**Behavior**:
- Rendered directly in chat message flow
- Auto-vanish on completion → Shows "✓ Completed" chip
- Completion event sent immediately after response
- Metadata persisted for resume state restoration

**Use Cases**:
- ActionPlanApprovalForm (Approve / Request Revisions)
- ConfirmDeletionDialog (Yes / No)
- QuickFeedbackForm (1-2 fields)

---

### Artifact Components (`display: "artifact"`)

**Examples**: Full dashboards, multi-step forms, visualizations

**Behavior**:
- Rendered in side panel (not in chat)
- Persisted via `ArtifactInstances` collection (separate from chat messages)
- Side panel stays open until user closes it
- No "Completed" chip in chat (artifact state managed independently)

**Use Cases**:
- RevenueDashboard
- MultiStepOnboardingWizard
- DataVisualizationCanvas

**Note**: This implementation focuses on **inline components only**. Artifact persistence is already handled by the existing `session_manager.create_artifact_instance()` flow.

---

## Testing Guide

### Test Scenario 1: Inline Component Completed Before Disconnect

**Steps**:
1. Start Generator workflow
2. Agent presents inline approval form: "Do you approve this action plan?"
3. User clicks "Approve" button
4. **Expected**: Component disappears, shows "✓ ActionPlanApprovalForm completed"
5. User closes browser tab
6. User returns to chat page (WebSocket reconnects)
7. **Expected**: Chat shows "✓ ActionPlanApprovalForm completed" chip (no interactive form)

**Verification**:
- Check MongoDB: `messages[N].metadata.ui_tool.ui_tool_completed === true`
- Check frontend console: Look for `[RESUME] Restored UI tool state: completed=true`
- Check frontend rendering: "✓ Completed" chip visible, no form inputs

---

### Test Scenario 2: Inline Component In-Progress During Disconnect

**Steps**:
1. Start Generator workflow
2. Agent presents inline form: "Please provide revision feedback"
3. User sees the form (doesn't submit yet)
4. User closes browser tab
5. User returns to chat page
6. **Expected**: Form reappears in same state (interactive, not completed)

**Verification**:
- Check MongoDB: `messages[N].metadata.ui_tool.ui_tool_completed === false`
- Check frontend rendering: Interactive form visible (not "Completed" chip)

---

### Test Scenario 3: Multiple Inline Components (Sequential)

**Steps**:
1. Agent presents component #1: "Approve action plan?" (inline)
2. User clicks "Approve" → Shows "✓ completed"
3. Agent presents component #2: "Enter project name" (inline)
4. User types but doesn't submit
5. User disconnects
6. User reconnects

**Expected**:
- Component #1: "✓ ActionPlanApprovalForm completed" chip
- Component #2: Interactive text input (in-progress)

**Verification**:
- MongoDB should have 2 messages with `metadata.ui_tool`, one completed, one pending
- Frontend should render 1 chip + 1 interactive component

---

### Test Scenario 4: Artifact vs Inline (Mixed Display Modes)

**Steps**:
1. Agent presents artifact: "Here's your revenue dashboard" (display: artifact)
2. Agent presents inline: "Approve the dashboard?" (display: inline)
3. User opens artifact in side panel
4. User approves inline component
5. User disconnects

**Expected on Reconnect**:
- Artifact: Side panel shows dashboard (restored via `artifact_instance_id` from `WorkflowSessions`)
- Inline: Chat shows "✓ completed" chip

**Verification**:
- Check `/api/chats/meta` response includes `artifact_instance_id`
- Check MongoDB: `messages[N].metadata.ui_tool.display === "inline"` for inline component
- Frontend should restore both artifact and inline completion state

---

## MongoDB Queries for Debugging

### Find All UI Tool Events in a Chat

```javascript
db.ChatSessions.findOne(
  { _id: "chat-123" },
  { 
    "messages": { 
      $elemMatch: { "metadata.ui_tool": { $exists: true } } 
    } 
  }
)
```

### Find Completed Inline Tools

```javascript
db.ChatSessions.aggregate([
  { $match: { app_id: "ent-456" } },
  { $unwind: "$messages" },
  { $match: { "messages.metadata.ui_tool.ui_tool_completed": true } },
  { $project: {
      chat_id: "$_id",
      tool_id: "$messages.metadata.ui_tool.ui_tool_id",
      completed_at: "$messages.metadata.ui_tool.completed_at"
  }}
])
```

### Find Pending Inline Tools (User Never Responded)

```javascript
db.ChatSessions.aggregate([
  { $match: { status: 1 } },  // IN_PROGRESS chats only
  { $unwind: "$messages" },
  { $match: { 
      "messages.metadata.ui_tool.ui_tool_completed": false,
      "messages.metadata.ui_tool.display": "inline"
  }},
  { $project: {
      chat_id: "$_id",
      tool_id: "$messages.metadata.ui_tool.ui_tool_id",
      event_id: "$messages.metadata.ui_tool.event_id",
      timestamp: "$messages.metadata.ui_tool.timestamp"
  }}
])
```

---

## Edge Cases & Considerations

### Case 1: User Responds After Reconnection

**Scenario**: User disconnects mid-interaction, reconnects, then submits response.

**Expected Behavior**: Should work correctly because:
- Backend still has `_wait_for_ui_tool_response_internal()` waiting (response futures keyed by `event_id`)
- Frontend will send response with same `event_id`
- Backend will process response and update completion state

**Potential Issue**: If backend crashes before setting up the response future, frontend will send response to a dead handler.

**Mitigation**: Consider adding response timeout + retry logic (future work).

---

### Case 2: Backend Crashes Before Completion Event

**Scenario**: User submits response → Backend receives it → Backend crashes before sending `chat.ui_tool_complete` event or updating MongoDB.

**Current State**: Message will have `ui_tool_completed: false` in MongoDB.

**Impact on Resume**: Interactive component will reappear (user sees form again).

**Recommendation**: This is acceptable behavior. If the backend crashed, we can't guarantee the agent processed the response. User can resubmit.

**Future Enhancement**: Add transaction-like semantics: persist completion state in the same operation as processing the response.

---

### Case 3: Multiple Reconnections (Rapid Disconnect/Reconnect)

**Scenario**: User has unstable connection, disconnects and reconnects multiple times.

**Expected Behavior**: Each reconnection should replay messages with consistent state.

**Verification**: Test rapid F5 refreshes (browser reconnect) and check that:
- Completed tools always show "✓ Completed" chip
- In-progress tools always show interactive component
- No flickering or state inconsistency

---

### Case 4: Timeout Handling (User Never Responds)

**Current**: Backend waits indefinitely (`timeout=None` in `_wait_for_ui_tool_response_internal()`).

**Issue**: If user abandons the chat, the response future will never resolve.

**Recommendation** (future work):
1. Add timeout parameter to UI tool invocation
2. On timeout, mark tool as "expired" in metadata:
   ```json
   {
     "ui_tool_completed": false,
     "ui_tool_status": "expired",
     "expired_at": "2025-01-15T11:00:00Z"
   }
   ```
3. On resume, show a different chip: "⏰ Tool expired (no response)"

---

## Performance Considerations

### MongoDB Update Overhead

**Concern**: Two MongoDB writes per UI tool interaction:
1. `attach_ui_tool_metadata()` — Initial state
2. `update_ui_tool_completion()` — Completion state

**Impact**: Minimal. These are single-document updates with array filters (efficient).

**Optimization**: Could batch multiple UI tool completions if multiple tools complete in quick succession (future enhancement).

---

### Resume Performance

**Concern**: Does adding `uiToolEvent` reconstruction slow down resume?

**Impact**: Negligible. The `_build_text_event()` method already processes metadata; adding a simple dict check is O(1).

**Measurement**: Test resume with 100+ messages including 10+ UI tools. Measure time to send all replayed events to frontend.

---

## Backward Compatibility

### What Happens to Old Chats (Before This Implementation)?

**Scenario**: User has an existing IN_PROGRESS chat from before this feature was deployed. The messages lack `metadata.ui_tool`.

**Behavior on Resume**:
- Old messages without `metadata.ui_tool` → No `uiToolEvent` reconstructed
- Frontend won't render any UI tool component (correct behavior — tool state was never persisted)
- Agent messages will appear as normal text messages

**Verdict**: Backward compatible. No breaking changes for old chats.

---

### What If Frontend Code Doesn't Check `isCompleted`?

**Scenario**: Frontend hasn't been updated to handle `ui_tool_completed` flag.

**Behavior**:
- Backend still persists metadata correctly
- Resume still sends `ui_tool_completed` flag in replayed events
- Frontend might ignore the flag and render interactive component anyway

**Mitigation**: Verify that `ChatInterface.js` line ~16-19 already handles this:

```javascript
React.useEffect(() => {
  if (isCompleted && !completed) {
    setCompleted(true);
  }
}, [isCompleted, completed]);
```

**Verdict**: Frontend already has the necessary logic. No changes needed.

---

## Logging & Observability

### Persistence Logs

**What to Watch**:
```
[UI_TOOL_METADATA] Attached ui_tool metadata to message[5] in chat-123 (tool=ActionPlanApprovalForm, event=tool-evt-abc)
[UI_TOOL_COMPLETE] Updated completion for event=tool-evt-abc in chat-123 (completed=true, status=completed)
```

**Warning Signs**:
```
[UI_TOOL_METADATA] Failed to update message in chat-123  ← Check MongoDB connectivity
[UI_TOOL_COMPLETE] No message found with ui_tool.event_id=tool-evt-abc  ← Metadata attach failed or event_id mismatch
```

---

### Resume Logs

**What to Watch**:
```
[RESUME] Restored UI tool state: tool=ActionPlanApprovalForm event=tool-evt-abc completed=true display=inline
```

**Use Case**: Verify that UI tool state is correctly restored on reconnect.

---

### Frontend Console Logs

**What to Watch**:
```
✓ [UI_COMPLETE] Inline tool completed: { completedId: 'tool-evt-abc', completedTool: 'ActionPlanApprovalForm', status: 'completed' }
```

**Expected Flow**:
1. User submits response → Backend sends `chat.ui_tool_complete` event
2. Frontend updates message state: `ui_tool_completed: true`
3. User disconnects
4. User reconnects → Backend replays messages with `ui_tool_completed: true`
5. Frontend renders "✓ Completed" chip

---

## Future Enhancements

### 1. UI Tool Dismissal Persistence

**Current**: `chat.ui_tool_dismiss` event only updates frontend state.

**Enhancement**: Store dismissal state in metadata:
```json
{
  "ui_tool_dismissed": true,
  "dismissed_at": "2025-01-15T10:35:00Z",
  "dismissed_reason": "user_closed"
}
```

**Use Case**: On resume, don't show dismissed tools.

---

### 2. Timeout & Expiration Tracking

**Enhancement**: Mark tools as "expired" if user doesn't respond within timeout period.

**Implementation**:
```python
# In ui_tools.py
resp = await _wait_for_ui_tool_response_internal(event_id, timeout=300)  # 5 min timeout
if resp is None:
    await pm.update_ui_tool_expiration(chat_id, app_id, event_id)
```

**Frontend Rendering**:
```javascript
{expired && <span className="...">⏰ Tool expired (no response)</span>}
```

---

### 3. Admin Dashboard: Pending UI Tools

**Enhancement**: Create endpoint to query all pending UI tools across all chats (for monitoring).

**Endpoint**: `GET /api/admin/ui-tools/pending`

**Response**:
```json
[
  {
    "chat_id": "chat-123",
    "app_id": "ent-456",
    "tool_id": "ActionPlanApprovalForm",
    "event_id": "tool-evt-abc",
    "timestamp": "2025-01-15T10:30:00Z",
    "age_minutes": 15
  }
]
```

**Use Case**: Identify stuck workflows waiting for user input.

---

## Success Criteria (Recap)

✅ **Completed inline components persist across reconnections** — Shows "✓ Completed" chip, not interactive form  
✅ **In-progress inline components remain interactive after reconnect** — User can complete interaction  
✅ **Multiple inline components persist independently** — Each component has its own completion state  
✅ **Artifact display mode unaffected** — Side panel restoration continues working  
✅ **MongoDB stores ui_tool metadata** — All UI tool invocations tracked in `messages[].metadata.ui_tool`  
✅ **No performance degradation** — Metadata adds minimal overhead to persistence and resume  
✅ **Structured logging** — Clear logs for debugging UI tool lifecycle  

---

## Questions & Answers

**Q: Why store UI tool metadata in message metadata instead of a separate collection?**

A: Keeps messages as the single source of truth for chat history. Avoids cross-collection queries during resume. Aligns with existing architecture (messages array is authoritative).

---

**Q: What if the agent sends multiple messages before invoking a UI tool?**

A: The `attach_ui_tool_metadata()` method finds the **last assistant message** and attaches metadata there. This assumes the UI tool is presented immediately after the agent's message. If the agent sends multiple messages, the metadata will be attached to the most recent one (which is correct — the last message prompts the user to interact with the tool).

---

**Q: Can we store form field values (partial input) in metadata?**

A: Not recommended. Form field values are client-side state (controlled by React state). Persisting them would require:
1. Frontend sending periodic updates to backend
2. Backend storing arbitrary key-value pairs in metadata
3. Frontend reconstructing form state on resume

**Verdict**: Too complex for the current scope. If needed, consider using browser `localStorage` for client-side persistence (but this won't work across devices).

---

**Q: What happens if the agent presents the same UI tool twice?**

A: Each invocation gets a unique `event_id` (UUID). MongoDB will have two messages with `metadata.ui_tool`, each with a different `event_id`. Frontend will render two separate components (or two "Completed" chips if both were completed). This is correct behavior — each interaction is independent.

---

## Related Documentation

- **RESUME_INITIAL_MESSAGE_FIX.md** — Explains `startup_mode` filtering during resume
- **MULTI_WORKFLOW_ARTIFACT_PERSISTENCE_ANALYSIS.md** — Explains artifact persistence for multi-workflow navigation
- **docs/interactive-artifacts/06-TROUBLESHOOTING.md** — General troubleshooting for UI tools

---

## Conclusion

This implementation enables inline UI components to persist their completion state across WebSocket reconnections, ensuring a consistent UX. The solution leverages existing MongoDB message persistence and frontend rendering logic, requiring minimal changes to the codebase.

**Key Takeaway**: UI tool state is now **part of the message history**, not ephemeral runtime state. This aligns with the declarative, persistence-first philosophy of the MozaiksAI runtime.
