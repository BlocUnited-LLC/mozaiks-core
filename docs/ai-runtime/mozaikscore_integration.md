# MozaiksAI Runtime — Stateless Integration Description (for MozaiksCore)

This document is **self-contained**: it assumes the reader (human or LLM) has **no access** to the MozaiksAI repo, source files, or other markdown docs. Treat what’s below as the **integration contract**: what MozaiksAI is, what MozaiksCore owns, and the concrete HTTP + WebSocket protocol to implement.

MozaiksAI is the **agentic runtime layer**: an event-driven execution engine that runs **declarative, multi-agent workflows** (AG2/Autogen) and streams results to a chat/artifact UI. MozaiksCore is the **platform layer** (auth, tenancy, entitlements, product UI, billing) that embeds or integrates with the runtime.

---

## 1) What MozaiksAI is (and is not)

### MozaiksAI **is**
- **A workflow execution engine** for agentic apps: loads workflow manifests from `workflows/`, configures AG2 agents/tools, runs orchestration patterns, and persists transcripts/artifacts.
- **A transport + event router**: real-time WebSocket streaming for chat messages, tool calls, UI-tool (artifact) events, and lifecycle events.
- **A persistence + resume layer**: Mongo-backed chat sessions, session metadata, and artifact state so conversations can resume safely.
- **A pack orchestrator**: optional macro graph (`workflows/_pack/workflow_graph.json`) that expresses prerequisites and journeys (wizard-like auto-advance).
- **A modular runtime**: workflows and tools are pluggable; the runtime should not hardcode business logic.

### MozaiksAI **is not**
- An auth/billing system (MozaiksCore owns identity, entitlements, subscriptions).
- A domain-specific backend (MozaiksCore services own “real” product data; MozaiksAI tools call those services).
- A UI schema authority (the runtime emits events; the UI decides how to render).

---

## 2) Core concepts (shared language)

### Tenancy identifiers (non-negotiable)
- `app_id`: **tenant boundary** (Mozaiks “app” / workspace / product instance).
- `user_id`: end-user boundary (also required for access control decisions).
- `workflow_name`: workflow template id (must match `workflows/<workflow_name>/` for implemented workflows).
- `chat_id`: a single run instance of a workflow (transcript + state).

Rule: **never mix state across (`app_id`, `user_id`, `chat_id`)**.

### Workflow (template)
A workflow is a folder under `workflows/<workflow_name>/` with JSON manifests (agents/tools/orchestrator/etc.). It defines **WHAT should run**.

### Runtime (execution)
The runtime wires the workflow into AG2 and executes it. It defines **HOW it runs**, with:
- tool registration
- orchestration pattern selection
- message/event streaming
- persistence and resume

### UI Tools / Artifacts
Workflows can emit UI-tool events (e.g., “render artifact workspace”) for the frontend to display in an artifact panel. This lets workflows produce **structured interactive UI**, not just text.

### Pack graph (macro orchestration)
`workflows/_pack/workflow_graph.json` can define:
- **gates**: prerequisites between workflows (required/optional)
- **journeys**: ordered steps that can auto-advance

Runtime enforcement is defense-in-depth: prerequisite checks happen on workflow start and on WS connect.
Pack graph schema + semantics are included in this document (Section 5).

---

## 3) Responsibilities split: MozaiksCore vs MozaiksAI

### MozaiksCore owns
- Identity + auth (JWT/session issuance; gateway auth; user/org claims)
- Entitlements and gating (what workflows a user can run)
- Billing + token economics (optionally via MozaiksPay)
- Domain APIs + persistent product data (campaigns, apps, settings, etc.)
- Host product UX (routes, navigation, pages, plugin system)

### MozaiksAI runtime owns
- Executing workflows (AG2 group chat orchestration)
- WebSocket streaming + event protocol
- Workflow session persistence and resume (Mongo)
- Macro pack gating/journeys (if configured)
- Observability hooks (logs/metrics/perf tracing)

---

## 4) Integration contract (HTTP + WebSocket)

All integration revolves around four stable IDs: `app_id`, `user_id`, `workflow_name`, `chat_id`.

### 4.1 Start a workflow chat (HTTP)
`POST /api/chats/{app_id}/{workflow_name}/start`

Body (minimum):
```json
{
  "user_id": "user_123"
}
```

Optional fields (supported by runtime):
```json
{
  "user_id": "user_123",
  "client_request_id": "uuid-for-idempotency",
  "force_new": false,
  "required_min_tokens": 0
}
```

Response includes:
- `chat_id`
- `websocket_url` (relative)
- `cache_seed` (per-chat deterministic seed for caching/resume correctness)
- `reused` (boolean) - true when the runtime reused a very recent in-progress chat to prevent duplicate starts

Example response:
```json
{
  "success": true,
  "chat_id": "chat_abc123",
  "workflow_name": "AgentGenerator",
  "app_id": "app_001",
  "user_id": "user_123",
  "remaining_balance": 0,
  "websocket_url": "/ws/AgentGenerator/app_001/chat_abc123/user_123",
  "message": "Chat session initialized; connect to websocket to start.",
  "reused": false,
  "cache_seed": 1234567890
}
```

Idempotency note: by default the runtime may reuse a very recent in-progress chat to prevent duplicate starts; set `force_new=true` to always create a new `chat_id`.

Common error status codes:
- `400`: missing/invalid `user_id`
- `402`: insufficient tokens to start workflow (if token gating enabled)
- `409`: prerequisites not met (pack gating)
- `500`: unexpected runtime error

### 4.2 Connect for real-time events (WebSocket)
`/ws/{workflow_name}/{app_id}/{chat_id}/{user_id}`

The runtime emits `chat.*` events (text, tool calls, UI-tool events, run lifecycle, errors). The event contract is summarized in Section 4.5 below.

### 4.3 Send user messages
Preferred: send via the established WebSocket (ChatUI does this).

Fallback HTTP endpoint:
`POST /chat/{app_id}/{chat_id}/{user_id}/input`

Body:
```json
{
  "workflow_name": "AgentGenerator",
  "message": "Hello"
}
```

### 4.4 Query workflow/session metadata (optional but common)
- List workflows/configs: `GET /api/workflows` (or `/api/workflows/config`)
- Workflow tools: `GET /api/workflows/{workflow_name}/tools` and `/ui-tools`
- Session tabs/list: `GET /api/sessions/list/{app_id}/{user_id}`
- Recent sessions: `GET /api/sessions/recent/{app_id}/{user_id}`
- Chat metadata (artifact restore, cache seed, status): `GET /api/chats/meta/{app_id}/{workflow_name}/{chat_id}`
- Availability with pack gating applied: `GET /api/workflows/{app_id}/available?user_id=<user_id>`

Example response: `GET /api/workflows/{app_id}/available?user_id=user_123`
```json
{
  "workflows": [
    {
      "workflow_name": "ValidationEngine",
      "available": false,
      "reason": "ValidationEngine requires the app to be built first.",
      "type": "independent",
      "description": "Validation execution for an app",
      "required_gates": [
        {
          "from": "AppGenerator",
          "to": "ValidationEngine",
          "gating": "required",
          "scope": "app",
          "reason": "ValidationEngine requires the app to be built first."
        }
      ]
    }
  ]
}
```

Example response: `GET /api/chats/meta/{app_id}/{workflow_name}/{chat_id}`
```json
{
  "exists": true,
  "chat_id": "chat_abc123",
  "workflow_name": "AgentGenerator",
  "status": 0,
  "cache_seed": 1234567890,
  "has_children": false,
  "last_sequence": 42,
  "last_artifact": null,
  "artifact_instance_id": null,
  "artifact_state": null,
  "app_id": "app_001"
}
```

Status mapping (current runtime): `status=0` means in progress; `status=1` means completed.

---

## 4.5 WebSocket event contract (outbound)

### Envelope
Every message sent by the runtime to the client is a JSON object:

```json
{
  "type": "chat.text",
  "data": { "kind": "text", "agent": "assistant", "content": "Hello", "sequence": 12 },
  "timestamp": "2026-01-02T12:34:56.000000+00:00"
}
```

Notes:
- The WebSocket connection is already scoped to one `chat_id` (it is part of the URL), so events usually omit `chat_id`.
- Many events include `data.sequence` (a monotonically increasing integer per chat) for ordering/resume.
- Some events may include `_mozaiks_hide: true` (UI should suppress) or `ui_visibility: "trace"` (UI can hide behind a debug toggle).

### Required `type` values to support
- `chat.text` / `chat.print`: normal assistant/agent messages
- `chat.run_start` / `chat.run_complete`: lifecycle
- `chat.error`: errors (contains `data.message` and often `data.error_code`)
- `chat.input_request`: runtime is requesting a human reply (HITL)
- `chat.input_ack` / `chat.input_timeout`: input lifecycle
- `chat.tool_call` / `chat.tool_response`: tool execution signals (also used for UI tools)
- `chat.ui_tool_dismiss`: dismiss an artifact-mode UI tool after it resolves
- `chat.resume_boundary`: indicates resume replay completed
- `chat.select_speaker`: speaker selection hint (optional UI affordance)
- `chat.usage_summary`: token/cost summary (if enabled)
- `chat.context_switched`: the active chat context changed (journey auto-advance or manual switch)

### `chat.tool_call` (includes UI tools)
`chat.tool_call` events carry a correlation id (`corr`) and payload:

```json
{
  "type": "chat.tool_call",
  "data": {
    "kind": "tool_call",
    "agent": "SomeAgent",
    "tool_name": "request_api_key",
    "corr": "evt_123",
    "awaiting_response": true,
    "component_type": "AgentAPIKeyInput",
    "display": "artifact",
    "payload": { "any": "json" }
  },
  "timestamp": "2026-01-02T12:34:56.000000+00:00"
}
```

If `component_type` is present, this is a **UI tool request** and the UI must submit a response (Section 4.7).

---

## 4.6 WebSocket message contract (inbound)

If you implement a custom client (instead of embedding the reference ChatUI), these are the minimum inbound messages to support:

### User message / input submit
```json
{ "type": "user.input.submit", "text": "Hello" }
```

When responding to a prior `chat.input_request`, include the request id:
```json
{ "type": "user.input.submit", "input_request_id": "req_123", "text": "Yes" }
```

### UI tool response (WebSocket alternative to HTTP)
```json
{
  "type": "ui.tool.response",
  "event_id": "evt_123",
  "response_data": { "status": "success", "data": { "any": "json" } }
}
```

---

## 4.7 UI tool response contract (HTTP)

UI tools are resolved by submitting a response to:
`POST /api/ui-tool/submit`

```json
{
  "event_id": "evt_123",
  "response_data": { "status": "success", "data": { "any": "json" } }
}
```

For HITL `chat.input_request` responses (if you are not sending `user.input.submit` over WebSocket), you can submit via:
`POST /api/user-input/submit`

```json
{
  "input_request_id": "req_123",
  "user_input": "Yes"
}
```

---

## 5) Pack orchestration (journeys + gates)

MozaiksAI can coordinate multiple workflows as a pack:
- Example “build journey”: `ValueEngine → AgentGenerator → AppGenerator` (auto-advance)
- Example gate: `AppGenerator` required before `ValidationEngine` can start

### Pack graph schema (v2)
Pack orchestration is configured by a JSON document with this shape:

```json
{
  "pack_name": "DefaultPack",
  "version": 2,
  "description": "Optional",
  "workflows": [
    { "id": "ValueEngine", "type": "primary", "description": "Optional" },
    { "id": "ValidationEngine", "type": "independent", "description": "Optional" }
  ],
  "journeys": [
    {
      "id": "build",
      "label": "Build App",
      "scope": "app",
      "enforce_step_gating": true,
      "auto_attach_on_start": true,
      "auto_advance": true,
      "steps": ["ValueEngine", "AgentGenerator", "AppGenerator"]
    }
  ],
  "gates": [
    {
      "from": "AppGenerator",
      "to": "ValidationEngine",
      "gating": "required",
      "scope": "app",
      "reason": "ValidationEngine requires the app to be built first."
    }
  ]
}
```

### Semantics (what MozaiksCore should assume)
- **Required gates** block starting/resuming workflow `to` until `from` has at least one **completed** run in the same `app_id`.
- **Journey step order** can be enforced when `enforce_step_gating=true`.
- **Auto-advance** (`auto_advance=true`) can emit `chat.context_switched` so the UI can navigate to the next workflow’s `chat_id`.

Important (current runtime behavior): gate checks are enforced at **app scope** (they do not currently differentiate `scope:"user"` vs `scope:"app"`; both behave as app-scoped prerequisites).

Runtime enforcement:
- `POST /api/chats/{app_id}/{workflow}/start` returns `409` if prerequisites are not met.
- WebSocket connect rejects and emits `chat.error` then closes if prerequisites are not met.

Why MozaiksCore should care:
- It can surface “locked” workflows in UI (with reasons).
- It can build wizard-like experiences without hardcoding workflow routing.

---

## 6) Persistence + resume model (high level)

MozaiksAI persists:
- chat transcripts (agent/user messages)
- workflow run status (in progress / completed)
- workflow/session metadata (e.g., `cache_seed`, journey fields)
- artifact/workspace state (when workflows emit UI tools)

MozaiksCore can treat the runtime as the **source of truth** for chat state and only store:
- references to `chat_id` (and optionally last-known status) in its own DB for fast UX.

---

## 7) Security & tenancy (MozaiksCore-facing)

MozaiksAI assumes a strict isolation model:
- every persistence query is scoped by `app_id` (+ often `user_id`)
- pack gating is evaluated inside that scope

MozaiksCore should:
- ensure the caller is authorized for (`app_id`, `user_id`)
- ensure the UI never “spoofs” identifiers (treat them as trusted claims, not user input)
- avoid logging secrets (API keys, tokens) into runtime logs

Environment-variable feature toggles should encapsulate platform-specific behavior; keep runtime modular and open-source friendly.

---

## 8) Minimal implementation checklist for MozaiksCore

1. **Choose UX integration**
   - Embed ChatUI (reference implementation), or build a custom UI on the same `chat.*` event contract.
2. **Broker IDs**
   - Provide `app_id` and `user_id` from MozaiksCore auth/session context.
3. **Start workflows**
   - Call `/api/chats/{app_id}/{workflow_name}/start` and store the returned `chat_id`.
4. **Stream events**
   - Connect to `/ws/{workflow_name}/{app_id}/{chat_id}/{user_id}` and handle `chat.*` events.
5. **Enforce entitlements**
   - Either via gateway policy or via MozaiksCore-side gating logic before calling runtime start.
6. **Surface pack gating**
   - Use `/api/workflows/{app_id}/available?user_id=...` to show “locked/unlocked” workflows with reasons.
7. **Observe**
   - Route logs/metrics to your platform stack; monitor WS disconnects, start failures (409), and run completion.

---

## 9) Minimal runtime configuration (deployment)

At minimum, MozaiksAI needs:
- `MONGO_URI`: MongoDB connection string
- `OPENAI_API_KEY`: LLM key for AG2 agents (or an equivalent provider key if configured)
- `ENVIRONMENT`: `development|staging|production` (affects logging/caching defaults)

Common optional knobs:
- `PACK_GRAPH_PATH`: override where the pack graph JSON is loaded from
- `CHAT_START_IDEMPOTENCY_SEC`: start-request reuse window (default `15` seconds)
- `LOG_LEVEL`: `DEBUG|INFO|WARNING|ERROR`
