# AG-UI Contract (Mozaiks Core)
> **Doc Status:** authoritative (AG-UI compatibility layer)
> **Version:** 1.0.0  
> **Last Updated:** 2026-01-31  
> **Owner:** mozaiks-core

This document specifies the **AG-UI compatibility events** emitted by mozaiks-core.
It defines the outbound AG-UI envelope, namespaces, mappings from Mozaiks events,
and how those mappings are produced by the current runtime and AG2 integrations.

AG-UI compatibility in mozaiks-core is **additive**. Mozaiks continues to emit its
native `chat.*` contract; AG-UI events are derived from those and can be disabled.

---

## 1. Scope

- **Outbound only**: this contract covers server-to-client AG-UI events.
- **Inbound remains Mozaiks-native**: client-to-server messages use the Mozaiks
  WebSocket contract (`user.input.submit`, `ui.tool.response`, `artifact.action`, etc.).
- **Additive**: disabling AG-UI emission does not affect the `chat.*` stream.

---

## 2. AG-UI Envelope

All AG-UI events use the same envelope:

```json
{
  "type": "agui.*",
  "data": { /* event-specific fields */ },
  "timestamp": "2026-01-31T12:34:56.000000+00:00"
}
```

### Identity fields

`runId` and `threadId` are injected when available:

- `runId`: defaults to `chat_id` unless already present in the source payload.
- `threadId`: format is `{app_id}:{chat_id}` when both are known; otherwise falls back to `chat_id`.

Mozaiks also includes `chat_id` and `app_id` in AG-UI `data` for state events
where available.

---

## 3. Namespaces Implemented

Mozaiks emits the following AG-UI namespaces:

- `agui.lifecycle.*`
- `agui.text.*`
- `agui.tool.*`
- `agui.state.*`

These follow the nested namespace decision approved by platform.

---

## 4. Lifecycle Events

Lifecycle events are derived from Mozaiks `chat.orchestration.*` events:

| AG-UI Type | Source Event |
|-----------|--------------|
| `agui.lifecycle.RunStarted` | `chat.orchestration.run_started` |
| `agui.lifecycle.RunFinished` | `chat.orchestration.run_completed` |
| `agui.lifecycle.RunError` | `chat.orchestration.run_failed` |
| `agui.lifecycle.StepStarted` | `chat.orchestration.agent_started` |
| `agui.lifecycle.StepFinished` | `chat.orchestration.agent_completed` |

**Data payload**: the source `chat.*` payload is forwarded, with `runId` and `threadId`
injected if missing.

---

## 5. Text Streaming Events

Mozaiks produces AG-UI text streaming events from its existing message stream:

| AG-UI Type | Source Event |
|-----------|--------------|
| `agui.text.TextMessageStart` | emitted before the first `chat.print` |
| `agui.text.TextMessageContent` | `chat.print` |
| `agui.text.TextMessageEnd` | emitted after `chat.text` |

### Behavior

- The first `chat.print` for a chat creates a **message stream** with a generated `messageId`.
- Each `chat.print` yields `TextMessageContent` with the same `messageId`.
- When `chat.text` arrives, `TextMessageEnd` is emitted for the current stream.
- If `chat.text` arrives without prior `chat.print`, the adapter emits
  `TextMessageStart`, `TextMessageContent`, and `TextMessageEnd` in sequence.

### Data fields

```json
{
  "messageId": "uuid",
  "agent": "AgentName",
  "content": "streamed chunk (content events only)",
  "runId": "...",
  "threadId": "..."
}
```

---

## 6. Tool Events

Tool events are derived from Mozaiks `chat.tool_call` and `chat.tool_response`:

| AG-UI Type | Source Event |
|-----------|--------------|
| `agui.tool.ToolCallStart` | `chat.tool_call` |
| `agui.tool.ToolCallEnd` | emitted before `chat.tool_response` |
| `agui.tool.ToolCallResult` | `chat.tool_response` |

### Data normalization

Mozaiks maps tool identifiers using these fields (first available wins):

- `callId`: `call_id`, `callId`, or `id`
- `tool`: `name`, `tool_name`, or `tool`

The source payload is forwarded with `callId` and `tool` injected if missing.

---

## 7. State Events

State events are emitted directly by the transport layer (not via the adapter).
These events provide artifact state snapshots and deltas for AG-UI consumers.

### 7.1 StateSnapshot

Emitted on initial artifact render.

```json
{
  "type": "agui.state.StateSnapshot",
  "data": {
    "artifact_id": "artifact_123",
    "state": { /* full artifact payload */ },
    "workflow_name": "Generator",
    "source": "ui_tool",
    "runId": "chat_abc123",
    "threadId": "app_001:chat_abc123"
  }
}
```

### 7.2 StateDelta

Emitted after artifact updates (actions or patches). Uses JSON Patch (RFC 6902).

```json
{
  "type": "agui.state.StateDelta",
  "data": {
    "artifact_id": "artifact_123",
    "patch": [
      { "op": "replace", "path": "/title", "value": "Updated" }
    ],
    "workflow_name": "Generator",
    "source": "action",
    "runId": "chat_abc123",
    "threadId": "app_001:chat_abc123"
  }
}
```

**Note:** Root replacement uses `path: ""`.

### 7.3 MessagesSnapshot

Emitted on resume/reconnect (auto or client-initiated).

```json
{
  "type": "agui.state.MessagesSnapshot",
  "data": {
    "messages": [
      { "role": "assistant", "agent": "Planner", "content": "..." }
    ],
    "mode": "auto|client",
    "total_messages": 42,
    "runId": "chat_abc123",
    "threadId": "app_001:chat_abc123"
  }
}
```

---

## 8. Ordering and Delivery

- AG-UI events preserve the **relative order** of their source `chat.*` events.
- Text streams are **per chat** and are closed on `chat.text`.
- AG-UI emission is **best-effort**; failures do not block Mozaiks native events.

---

## 9. Compatibility Controls

AG-UI emission can be disabled by environment variable:

```
MOZAIKS_AGUI_ENABLED=false
```

When disabled, only `chat.*` events are emitted.

---

## 10. Implementation Associations (Mozaiks and AG2)

This section ties the AG-UI contract to the current runtime and AG2 integrations.

### Event Sources (AG2 -> Mozaiks)

- AG2 runtime events are serialized into Mozaiks `chat.*` envelopes by:
  - `mozaiks_ai/runtime/events/event_serialization.py`
- Examples include `chat.text`, `chat.print`, `chat.tool_call`, `chat.tool_response`,
  and `chat.orchestration.*`.

### Auto-Tool System (Mozaiks-native)

- `AutoToolEventHandler` listens for `chat.structured_output_ready`, resolves the
  tool binding, and emits:
  - `chat.tool_call`
  - `chat.tool_response`
- These tool events are then mapped into:
  - `agui.tool.ToolCallStart`
  - `agui.tool.ToolCallEnd`
  - `agui.tool.ToolCallResult`

Module: `mozaiks_ai/runtime/events/auto_tool_handler.py`

### AG-UI Adapter (Mozaiks -> AG-UI)

- `AGUIEventAdapter` maps Mozaiks `chat.*` envelopes into AG-UI envelopes for:
  - lifecycle
  - text
  - tool events

Module: `mozaiks_ai/runtime/event_agui_adapter.py`

### Transport Dual-Emission and State Events

- `SimpleTransport` broadcasts `chat.*` events and then **dual-emits** AG-UI
  envelopes (if enabled).
- It also emits:
  - `agui.state.StateSnapshot`
  - `agui.state.StateDelta`
  - `agui.state.MessagesSnapshot`

Module: `mozaiks_ai/runtime/transport/simple_transport.py`

---

## 11. Example Mapping

**Source (Mozaiks)**

```json
{
  "type": "chat.tool_call",
  "data": {
    "tool_name": "platform.vote",
    "call_id": "turn_123",
    "args": { "target_id": "x1" }
  },
  "timestamp": "2026-01-31T12:00:00Z"
}
```

**Derived (AG-UI)**

```json
{
  "type": "agui.tool.ToolCallStart",
  "data": {
    "tool_name": "platform.vote",
    "call_id": "turn_123",
    "args": { "target_id": "x1" },
    "callId": "turn_123",
    "tool": "platform.vote",
    "runId": "chat_abc123",
    "threadId": "app_001:chat_abc123"
  },
  "timestamp": "2026-01-31T12:00:00Z"
}
```

---

## 12. Versioning

This contract is versioned independently from the Mozaiks runtime contract.
Changes follow additive semantics:

- **Minor**: additive fields or new event types
- **Major**: breaking changes (removals or type changes)

---

## 13. References

- `docs/contracts/runtime-platform-contract-v1.md` (Mozaiks runtime contract)
- `docs/contracts/agui-migration-guide.md`
- `mozaiks_ai/runtime/event_agui_adapter.py`
- `mozaiks_ai/runtime/transport/simple_transport.py`
- `mozaiks_ai/runtime/events/auto_tool_handler.py`
