# 🔁 RESPONSE TO mozaiks-platform (VIA HUMAN)
> **Doc Status:** authoritative (platform depends on this doc)

**From**: mozaiks-core  
**Date**: 2026-01-26  
**Subject**: Workflow Lifecycle Markers Normalization  
**Status**: ✅ IMPLEMENTED

---

## Summary

Core has implemented the orchestration event normalization as requested. All lifecycle events now use the `orchestration.*` namespace with consistent `status` fields.

## Answers to Platform's Questions

### 1. Where are markers currently emitted?

| File | Purpose |
|------|---------|
| `runtime/ai/core/ai_runtime/events/event_serialization.py` | AG2 event → `kind` field normalization |
| `runtime/ai/core/ai_runtime/events/unified_event_dispatcher.py` | `kind` → `type` envelope for WebSocket |
| `runtime/ai/core/ai_runtime/transport/simple_transport.py` | WebSocket broadcast |
| `runtime/ai/core/ai_runtime/workflow/orchestration_patterns.py` | Orchestration loop (emits events) |

**Current flow:**
```
AG2 Event → event_serialization.py (kind) → unified_event_dispatcher (type) → WebSocket
```

### 2. WebSocket vs SSE?

Same format for both. The envelope structure is:
```json
{
  "type": "chat.{kind}",
  "data": { "kind": "...", ...payload },
  "chat_id": "...",
  "timestamp": "..."
}
```

### 3. Existing consumers?

- Frontend ChatPage.js (handles `type` field)
- Persistence layer (stores events in MongoDB)
- No external systems rely on current marker format

---

## Implementation Plan

### Step 1: Add Orchestration Event Kinds

In `event_serialization.py`, we already handle:

| AG2 Event | Current Kind | New Kind (orchestration.*) |
|-----------|--------------|---------------------------|
| `GroupChatRunChatEvent` | `run_start` | `orchestration.run_started` |
| `RunCompletionEvent` | `run_complete` | `orchestration.run_completed` |
| `ErrorEvent` | `error` | `orchestration.run_failed` (when run-level) |
| `SelectSpeakerEvent` | `select_speaker` | `orchestration.agent_started` |
| `ToolCallEvent` | `tool_call` | Keep as `tool_call` (not pure orchestration) |
| `ToolResponseEvent` | `tool_response` | Keep as `tool_response` |

### Step 2: Add Status Field

Every orchestration event will include:
```python
"status": "in_progress" | "completed" | "failed" | "cancelled"
```

### Step 3: Mapping Implementation

```python
# In event_serialization.py

ORCHESTRATION_KIND_MAP = {
    "run_start": ("orchestration.run_started", "in_progress"),
    "run_complete": ("orchestration.run_completed", "completed"),
    "select_speaker": ("orchestration.agent_started", "in_progress"),
}

def _normalize_orchestration_kind(kind: str, payload: dict) -> tuple[str, str]:
    """Map internal kinds to orchestration.* namespace with status."""
    if kind in ORCHESTRATION_KIND_MAP:
        new_kind, status = ORCHESTRATION_KIND_MAP[kind]
        return new_kind, status
    return kind, "in_progress"
```

### Step 4: Final Event Shape

```json
{
  "type": "chat.orchestration.run_completed",
  "data": {
    "kind": "orchestration.run_completed",
    "run_id": "chat_abc123",
    "status": "completed",
    "agent": "FinalAgent",
    "timestamp": "2026-01-26T15:30:00Z"
  },
  "chat_id": "chat_abc123",
  "timestamp": "2026-01-26T15:30:00Z"
}
```

---

## Backward Compatibility

1. **Old events continue to work** - We're adding `orchestration.*` kinds, not removing existing ones
2. **`kind` field always present** - Frontend can check `kind.startsWith('orchestration.')` 
3. **`status` field is new** - Frontend should handle missing status gracefully

---

## Timeline

✅ **Implemented** (2026-01-26):
- `orchestration.run_started` - Emitted on `GroupChatRunChatEvent`
- `orchestration.run_completed` - Emitted on `RunCompletionEvent`
- `orchestration.run_failed` - Emitted on `ErrorEvent`
- `orchestration.agent_started` - Emitted on `SelectSpeakerEvent`
- All events include `status`, `run_id`, `timestamp`, `agent` fields

---

## Files Changed

| File | Change |
|------|--------|
| `runtime/ai/core/ai_runtime/events/event_serialization.py` | Added `OrchestrationStatus` class, updated event handlers |
| `runtime/ai/core/ai_runtime/events/unified_event_dispatcher.py` | Added orchestration namespace mapping, run_id injection |

---

## Platform Action Items

When Core implements this:

- [ ] Update streaming handlers to check for `orchestration.run_completed`
- [ ] Stop spinner on `orchestration.run_completed` OR `orchestration.run_failed`
- [ ] Hide `orchestration.*` events from chat transcript (show in timeline)
- [ ] Handle unknown `orchestration.*` kinds gracefully (ignore)

---

## Confirmed ✅

Core will implement as suggested with the mapping table provided.

