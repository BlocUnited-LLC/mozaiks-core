# AG-UI Migration Guide (chat.* → agui.*)
> **Status:** Draft  
> **Owner:** mozaiks-core  
> **Last Updated:** 2026-01-31

This guide explains how to migrate consumers from the legacy `chat.*` event namespace to the AG-UI compatible `agui.*` namespace.

---

## 1) Overview

Mozaiks Core **dual-emits** events:
- Existing `chat.*` events (current consumers)
- New `agui.*` events (AG-UI compatible)

**Opt-out:** set `MOZAIKS_AGUI_ENABLED=false` to disable AG-UI emission.

**Thread ID:** `{app_id}:{chat_id}`  
**Run ID:** `chat_id`

---

## 2) Mapping Summary

### Lifecycle
| chat.* | agui.* |
|--------|--------|
| `chat.orchestration.run_started` | `agui.lifecycle.RunStarted` |
| `chat.orchestration.run_completed` | `agui.lifecycle.RunFinished` |
| `chat.orchestration.run_failed` | `agui.lifecycle.RunError` |
| `chat.orchestration.agent_started` | `agui.lifecycle.StepStarted` |
| `chat.orchestration.agent_completed` | `agui.lifecycle.StepFinished` |

### Text
| chat.* | agui.* |
|--------|--------|
| `chat.print` | `agui.text.TextMessageContent` |
| `chat.text` | `agui.text.TextMessageEnd` |
| *(synthetic)* | `agui.text.TextMessageStart` |

**Notes:**
- `TextMessageStart` is emitted before the first `chat.print`, or before `chat.text` if no streaming occurred.
- `TextMessageEnd` is emitted after `chat.text`.

### Tools
| chat.* | agui.* |
|--------|--------|
| `chat.tool_call` | `agui.tool.ToolCallStart` |
| `chat.tool_response` | `agui.tool.ToolCallResult` |
| *(synthetic)* | `agui.tool.ToolCallEnd` |

### State
| chat.* | agui.* |
|--------|--------|
| *(none)* | `agui.state.StateSnapshot` |
| *(none)* | `agui.state.StateDelta` |
| *(none)* | `agui.state.MessagesSnapshot` |

**Notes:**
- State events are new in core and do not have `chat.*` equivalents.
- `StateDelta` uses JSON Patch (RFC 6902) ops; root replacement uses `path: ""`.

---

## 3) Recommended Migration Steps

1. **Subscribe to `agui.*`** in parallel with existing handlers.
2. **Verify lifecycle + text + tool flows** match current UX.
3. **Switch internal renderers** to use AG-UI events only.
4. **Remove dependency on `chat.*`** once fully validated.

---

## 4) Example Payload (AG-UI)

```json
{
  "type": "agui.lifecycle.RunStarted",
  "data": {
    "runId": "chat_abc123",
    "threadId": "app_001:chat_abc123",
    "status": "running",
    "agent": "Orchestrator"
  },
  "timestamp": "2026-01-30T12:00:00Z"
}
```

---

## 5) FAQ

**Q: Do AG-UI events replace `chat.*` events?**  
A: Not yet. Both are emitted for compatibility.

**Q: How do I disable AG-UI events?**  
A: `MOZAIKS_AGUI_ENABLED=false`

**Q: Are there AG-UI equivalents for every `chat.*` event?**  
A: No. Some runtime-specific events (usage, resume, select_speaker, etc.) remain `chat.*` only.
