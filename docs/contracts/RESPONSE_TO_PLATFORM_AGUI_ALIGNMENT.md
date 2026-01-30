# RESPONSE FROM CORE: AG-UI Alignment + Architecture Evolution

> **From**: mozaiks-core
> **To**: mozaiks-platform
> **Date**: January 29, 2026
> **Type**: Architecture Assessment + Technical Response
> **In Reply To**: `VALUE_PROPOSITION.md` (Architecture Evolution + AG-UI Alignment)
> **Contract Version**: Extends `runtime-platform-contract-v1.md`

---

## TL;DR

Core has completed a comprehensive audit. Here's the bottom line:

1. **AG-UI alignment is very close.** Our event system already covers ~80% of AG-UI semantics. Renaming is feasible via aliasing (non-breaking).
2. **Artifact actions (buttons -> tool calls) do NOT exist today.** The artifact system is render-only. This is the biggest gap and requires new work.
3. **View Mode is feasible.** The frontend already has the primitives (`layoutMode`, `FluidChatLayout`, `MobileArtifactDrawer`). Adding `'view'` is a moderate lift.
4. **Rich artifact primitives (grid, table, form, chart) do NOT exist in core.** Core currently delegates all rendering to workflow-specific React components via `UIToolRenderer`. Introducing core-owned primitives is a significant architectural choice that needs careful scoping.
5. **Navigation-as-workflow-triggers is architecturally sound** and can be done without breaking existing route-based navigation.

**Architecture concerns exist.** Details in Part 7 below.

---

## Part 0: Current Infrastructure Inventory

### 0.1 Complete Event Type Inventory

Core emits events across **three layers**. Here is the exhaustive inventory:

#### Layer 1: AG2 Runtime Events (via `event_serialization.py`)

These are AG2 engine events transformed into WebSocket payloads.

| Internal `kind` | WebSocket `type` | Payload Shape | AG-UI Equivalent |
|-----------------|-------------------|---------------|------------------|
| `text` | `chat.text` | `{ content, agent, source, structured_output?, structured_schema? }` | `TextMessageStart` + `TextMessageContent` + `TextMessageEnd` |
| `print` | `chat.print` | `{ content, agent }` | `TextMessageContent` (streaming delta) |
| `input_request` | `chat.input_request` | `{ agent, request_id, prompt, password?, component_type?, raw_payload? }` | No direct equivalent (custom) |
| `input_ack` | `chat.input_ack` | `{ request_id, corr }` | No direct equivalent (custom) |
| `input_timeout` | `chat.input_timeout` | `{ request_id }` | No direct equivalent (custom) |
| `tool_call` | `chat.tool_call` | `{ call_id, name, agent, arguments? }` | `ToolCallStart` + `ToolCallArgs` |
| `tool_response` | `chat.tool_response` | `{ call_id, name, agent, result?, status, error? }` | `ToolCallResult` |
| `tool_progress` | `chat.tool_progress` | `{ tool_name, progress_percent, status_message, corr? }` | No direct equivalent (extension) |
| `select_speaker` | `chat.select_speaker` | `{ selected, previous_agent }` | No direct equivalent (multi-agent specific) |
| `handoff` | `chat.handoff` | `{ handoff_type, agent, source_agent, target, target_type }` | No direct equivalent (multi-agent specific) |
| `resume_boundary` | `chat.resume_boundary` | `{ agent, reason }` | No direct equivalent (persistence specific) |
| `usage_delta` | `chat.usage_delta` | `{ chat_id, tokens, model }` | No direct equivalent (metering) |
| `usage_summary` | `chat.usage_summary` | `{ chat_id, usage }` | No direct equivalent (metering) |
| `run_start` | `chat.run_start` | `{ chat_id, workflow_name }` | `RunStarted` |
| `run_complete` | `chat.run_complete` | `{ chat_id, status }` | `RunFinished` |
| `error` | `chat.error` | `{ message, error_code }` | `RunError` |
| `structured_output_ready` | `chat.structured_output_ready` | `{ agent, model_name, structured_data, auto_tool_mode, context }` | `StateSnapshot` (closest) |
| `ui_tool_dismiss` | `chat.ui_tool_dismiss` | `{ event_id, ui_tool_id }` | No direct equivalent |
| `attachment_uploaded` | `chat.attachment_uploaded` | `{ filename, intent }` | No direct equivalent |
| `unknown` | `chat.unknown` | `{ raw_type }` | `Raw` |

#### Layer 1a: Orchestration Events (v1.1)

| Internal `kind` | WebSocket `type` | Payload Shape | AG-UI Equivalent |
|-----------------|-------------------|---------------|------------------|
| `orchestration.run_started` | `chat.orchestration.run_started` | `{ run_id, status, agent, timestamp, message }` | `RunStarted` |
| `orchestration.run_completed` | `chat.orchestration.run_completed` | `{ run_id, status, agent, timestamp, summary?, cost?, total_tokens? }` | `RunFinished` |
| `orchestration.run_failed` | `chat.orchestration.run_failed` | `{ run_id, status, agent, timestamp, message, error, code }` | `RunError` |
| `orchestration.agent_started` | `chat.orchestration.agent_started` | `{ run_id, status, agent, timestamp, previous_agent, selected_speaker }` | `StepStarted` |
| `orchestration.agent_completed` | `chat.orchestration.agent_completed` | `{ run_id, status, agent, timestamp }` | `StepFinished` |
| `orchestration.tool_started` | `chat.orchestration.tool_started` | `{ run_id, status, tool_name, timestamp }` | `ToolCallStart` |
| `orchestration.tool_completed` | `chat.orchestration.tool_completed` | `{ run_id, status, tool_name, timestamp }` | `ToolCallEnd` |

#### Layer 2: UI Tool Events (via `unified_event_dispatcher.py`)

| Event Type | Payload Shape | Purpose |
|------------|---------------|---------|
| `UIToolEvent` | `{ ui_tool_id, payload, workflow_name, display, chat_id }` | Render interactive UI component |
| `chat.ui_tool_complete` | `{ eventId, ui_tool_id, display, status, summary }` | Signal UI tool interaction completed |
| `chat.tool_call` (UI variant) | `{ kind, tool_name, component_type, payload, corr, awaiting_response, display }` | Full UI tool render request with correlation |

#### Layer 3: Business Events (via `unified_event_dispatcher.py`)

| Event Type | Fields | Purpose |
|------------|--------|---------|
| `BusinessLogEvent` | `{ log_event_type, description, context, level, timestamp }` | Operational telemetry (not sent to UI) |

**Custom dispatcher events** (registered handlers):
- `chat.structured_output_ready` -> AutoToolEventHandler, WorkflowPackCoordinator
- `chat.run_complete` -> WorkflowPackCoordinator, JourneyOrchestrator
- `chat.usage_summary` -> UsageIngestClient

### 0.2 UI Tools System

**Backend:** `packages/python/ai-runtime/mozaiks_ai/runtime/workflow/outputs/ui_tools.py`

**Core functions:**
| Function | Purpose |
|----------|---------|
| `use_ui_tool(tool_id, payload, *, chat_id, workflow_name, display?, timeout?)` | Emit UI event + wait for user response (blocking) |
| `emit_tool_progress_event(tool_name, progress_percent, status_message, ...)` | Non-blocking progress update |
| `handle_tool_call_for_ui_interaction(tool_call_event, chat_id)` | Orchestrator-managed UI tool handling |

**Display modes:** `"inline"` (embedded in chat) or `"artifact"` (full panel)

**What UI tools CAN render today:**
- Any React component registered in `workflows/{WorkflowName}/components/index.js`
- Components receive: `{ payload, onResponse, ui_tool_id, eventId }`
- Components return responses correlated via `ui_tool_id` + `eventId`

**What UI tools CANNOT do today:**
- Core does NOT own any rendering primitives (grid, table, form, etc.)
- All UI components are **workflow-specific** React components
- Artifacts are **render-only** -- there is no built-in mechanism for artifact buttons to trigger tool calls back to the agent
- No composite artifact support (multiple components in one view is workflow-component responsibility)
- No optimistic update mechanism

**Current UI tool lifecycle:**
```
1. Agent calls tool function (Python)
2. Tool function calls use_ui_tool(tool_id, payload)
3. use_ui_tool() -> SimpleTransport.send_ui_tool_event() -> WebSocket
4. Frontend UIToolRenderer renders workflow-specific React component
5. User interacts, component calls onResponse()
6. Response flows back via WebSocket -> SimpleTransport correlation
7. use_ui_tool() returns response to Python tool function
8. Agent continues
```

### 0.3 Transport & Streaming Patterns

**Transport:** `SimpleTransport` (singleton, async, thread-safe)

**WebSocket route:** `/ws/{workflow_name}/{app_id}/{chat_id}/{user_id}`

**Capabilities:**
| Capability | Status |
|------------|--------|
| Bidirectional WebSocket | Yes |
| Pre-connection event buffering | Yes (max 200 events) |
| Correlation-based response matching | Yes (pending_ui_tool_responses) |
| Agent message filtering (visual_agents) | Yes |
| Token streaming (print chunks) | Yes |
| Heartbeat/keepalive | Yes (120s interval) |
| Message sequence counters | Yes |
| Secret redaction in traces | Yes |
| Orchestration input registry | Yes |
| Background workflow spawning | Yes (semaphore-bounded) |
| Usage event fan-out | Yes |

**Inbound message types (client -> server):**
| Type | Purpose |
|------|---------|
| `user.input.submit` | Free-form user text |
| `chat.tool_response` | Correlated UI tool response |
| `ping` | Keepalive |

**Current limitations:**
- No SSE fallback (WebSocket only)
- No explicit AG-UI protocol framing
- No `RunStarted`/`RunFinished` envelope with `runId` + `threadId` as AG-UI expects

---

## Part 1: AG-UI Alignment Assessment

### 1.1 Detailed Mapping

| AG-UI Event | Our Current Event | Gap | Effort |
|-------------|-------------------|-----|--------|
| `RunStarted` | `chat.orchestration.run_started` | **Name only.** Semantics match. Missing `threadId`. | Alias + add field |
| `RunFinished` | `chat.orchestration.run_completed` | **Name only.** Semantics match. | Alias |
| `RunError` | `chat.orchestration.run_failed` | **Name only.** Semantics match. | Alias |
| `StepStarted` | `chat.orchestration.agent_started` | **Close.** Our "steps" are agent turns. | Alias + docs |
| `StepFinished` | `chat.orchestration.agent_completed` | **Close.** | Alias + docs |
| `TextMessageStart` | No separate event | **Gap.** We emit `chat.text` as a single complete message. | New event or wrapper |
| `TextMessageContent` | `chat.print` | **Very close.** Our streaming chunks. | Alias |
| `TextMessageEnd` | No separate event | **Gap.** We rely on `chat.text` arriving after prints. | New event |
| `TextMessageChunk` | N/A (convenience) | N/A | N/A |
| `ToolCallStart` | `chat.tool_call` | **Close.** We send name + args together. AG-UI streams args separately. | Split or alias |
| `ToolCallArgs` | (embedded in `chat.tool_call`) | **Gap.** We send args in one shot, AG-UI streams them. | New event if streaming args desired |
| `ToolCallEnd` | No separate event | **Gap.** We go straight to result. | New event |
| `ToolCallResult` | `chat.tool_response` | **Very close.** | Alias |
| `StateSnapshot` | `chat.structured_output_ready` | **Partial.** Different semantics. SO is agent output, not app state. | New event or reinterpret |
| `StateDelta` | No equivalent | **Gap.** We have no incremental state patching. | New capability |
| `MessagesSnapshot` | No equivalent (resume replays from DB) | **Gap.** Could be synthesized from persistence. | New event |
| `ActivitySnapshot` | No equivalent | **Gap.** | New event |
| `ActivityDelta` | No equivalent | **Gap.** | New event |
| `Raw` | `chat.unknown` | **Close.** | Alias |
| `Custom` | Dispatcher custom events | **Close.** Our `dispatcher.emit()` is similar. | Alias envelope |

### 1.2 Coverage Summary

| Category | AG-UI Events | We Have | Gap |
|----------|-------------|---------|-----|
| Lifecycle | 5 | 5 (via orchestration v1.1) | Names only |
| Text Messages | 4 | 2 (`chat.text`, `chat.print`) | Missing Start/End framing |
| Tool Calls | 5 | 2 (`chat.tool_call`, `chat.tool_response`) | Missing streaming args, End |
| State | 3 | 1 (partial: `structured_output_ready`) | Missing StateDelta, MessagesSnapshot |
| Activity | 2 | 0 | Entirely new |
| Special | 2 | 1 (partial: `chat.unknown`) | Custom wrapper |

**Overall: ~60% semantic coverage, ~80% if you count partial matches.**

### 1.3 Recommendation: Dual-Emit Strategy (Non-Breaking)

We recommend a **dual-emit approach** rather than hard renaming:

```python
# In build_outbound_event_envelope():
# 1. Continue emitting current event names (backward compat)
# 2. ALSO emit AG-UI aliased names for clients that opt in

# Example: When we emit chat.orchestration.run_started,
# ALSO emit an AG-UI-framed event:
{
    "type": "agui.lifecycle.RunStarted",
    "runId": chat_id,
    "threadId": f"{app_id}:{chat_id}",
    "timestamp": "...",
    // ... AG-UI spec fields
}
```

**Why dual-emit:**
- Zero breaking changes for existing consumers (platform, OSS users)
- AG-UI clients can subscribe to `agui.*` namespace
- Gradual migration: platform can switch to `agui.*` events at its own pace
- Community gets AG-UI compliance for free

**Implementation: New module `event_agui_adapter.py`** that wraps existing events into AG-UI envelopes. Transport sends both.

### 1.4 New Events Needed for Full AG-UI Compliance

| New Event | Source | Purpose |
|-----------|--------|---------|
| `agui.TextMessageStart` | Wrap `chat.print` first chunk | Opens message stream |
| `agui.TextMessageEnd` | Synthesize after `chat.text` | Closes message stream |
| `agui.ToolCallEnd` | Synthesize before `chat.tool_response` | Explicit tool call boundary |
| `agui.StateSnapshot` | New emission point | Full artifact/app state sync |
| `agui.StateDelta` | New emission point | Incremental state patch (JSON Patch) |
| `agui.MessagesSnapshot` | Synthesize from persistence on reconnect | Full conversation replay |

---

## Part 2: Artifact Action System Assessment

### 2.1 Current State: Render-Only

The artifact system today is **render-only**. Here's exactly what happens:

```
Agent -> use_ui_tool(tool_id, payload) -> WebSocket -> UIToolRenderer -> React Component
                                                                              |
                                                                              v
                                                                    User sees rendered UI
                                                                    User interacts (form, click)
                                                                              |
                                                                              v
                                                                    onResponse(data) -> WebSocket -> Agent
```

The `onResponse` callback is the **only** bidirectional channel. It:
- Is correlated to a **specific** `use_ui_tool()` call
- Returns data to the **blocking** Python function that emitted the UI tool
- Is a **one-shot** mechanism (respond once, tool function continues)

**What this means for actions:**
- A button in an artifact that should trigger a NEW tool call has no mechanism to do so
- The current `onResponse` is designed for "agent asks question, user answers" not "artifact rendered, user clicks button at any time"
- There is no way for a rendered artifact to initiate a new tool call after the original `use_ui_tool()` has already returned

### 2.2 What's Needed: Action System Architecture

```
New Flow:
                                                        ┌─────────────────────┐
                                                        │  Rendered Artifact   │
                                                        │                     │
                                                        │  [Invest Button]    │ <- User clicks
                                                        │  [Vote Button]      │
                                                        └────────┬────────────┘
                                                                 │
                                                      ┌──────────▼──────────────┐
                                                      │ Action Event (new type) │
                                                      │ {                       │
                                                      │   type: "artifact.action" │
                                                      │   tool: "platform.invest" │
                                                      │   params: {app_id: "123"} │
                                                      │   artifact_id: "card_abc" │
                                                      │ }                       │
                                                      └──────────┬──────────────┘
                                                                 │
                                                      WebSocket (client -> server)
                                                                 │
                                                      ┌──────────▼──────────────┐
                                                      │  SimpleTransport        │
                                                      │  Route to tool executor │
                                                      └──────────┬──────────────┘
                                                                 │
                                                      ┌──────────▼──────────────┐
                                                      │  Tool Execution         │
                                                      │  (outside agent loop)   │
                                                      └──────────┬──────────────┘
                                                                 │
                                                      ┌──────────▼──────────────┐
                                                      │  Result -> StateDelta   │
                                                      │  or new artifact render │
                                                      └─────────────────────────┘
```

**Key design decisions needed:**

1. **Who executes the tool?** The action triggers a tool call, but the original agent may not still be running. Options:
   - (a) Route to a standing "action handler" agent
   - (b) Route directly to tool function (bypass agent, just execute)
   - (c) Start a new mini-workflow for the action
   - **(b) is recommended** for core: simple, predictable, stateless

2. **How does the result update the artifact?** Options:
   - (a) Full re-render: tool returns new artifact payload, replace entire artifact
   - (b) StateDelta: tool returns JSON Patch, apply to existing artifact state
   - (c) Both: tool chooses
   - **(c) is recommended**

3. **Optimistic updates?** The `optimistic` field in the Platform proposal.
   - Core can support this at the **frontend level**: apply optimistic delta immediately, revert if tool fails
   - Backend doesn't need to know about optimistic updates
   - Frontend needs: `actions[].optimistic` field -> apply delta -> on error, rollback

### 2.3 Proposed Contract: Artifact Actions

**New inbound message type (client -> server):**

```json
{
    "type": "artifact.action",
    "action_id": "uuid",
    "artifact_id": "card_abc",
    "tool": "platform.invest",
    "params": { "app_id": "123" },
    "context": {
        "chat_id": "chat_abc",
        "app_id": "app_001",
        "user_id": "user_123"
    }
}
```

**New outbound event types (server -> client):**

```json
// Action acknowledged
{
    "type": "artifact.action.started",
    "data": { "action_id": "uuid", "tool": "platform.invest" },
    "timestamp": "..."
}

// Action result
{
    "type": "artifact.action.completed",
    "data": {
        "action_id": "uuid",
        "tool": "platform.invest",
        "result": { /* tool result */ },
        "artifact_update": {
            "mode": "replace" | "patch",
            "payload": { /* new artifact data or JSON Patch */ }
        }
    },
    "timestamp": "..."
}

// Action failed
{
    "type": "artifact.action.failed",
    "data": {
        "action_id": "uuid",
        "tool": "platform.invest",
        "error": "Insufficient funds",
        "rollback": true
    },
    "timestamp": "..."
}
```

### 2.4 Status: Not Implemented, Requires New Work

| Component | Work Required |
|-----------|--------------|
| Backend: Action routing in SimpleTransport | New: Parse `artifact.action`, route to tool executor |
| Backend: Stateless tool executor | New: Execute tool function outside agent loop |
| Backend: Action result -> event emission | New: Emit `artifact.action.completed/failed` |
| Frontend: Action button rendering | New: Parse `actions[]` from artifact payload |
| Frontend: Action event emission | New: Send `artifact.action` on click |
| Frontend: Optimistic update engine | New: Apply/rollback optimistic deltas |
| Frontend: Action result handling | New: Apply artifact updates from action results |
| Contract: New WebSocket message types | New: Document in contract v1.1 |

---

## Part 3: View Mode Feasibility Assessment

### 3.1 Current Mode Architecture

**Two dimensions control the UI:**

1. **`conversationMode`**: `'ask'` | `'workflow'` (stored in localStorage)
   - Ask: General chat, persistent history, no artifacts
   - Workflow: Task-oriented, artifact panel available

2. **`layoutMode`**: `'full'` | `'split'` | `'minimized'`
   - Full: Chat 100%, artifact 0%
   - Split: Chat 50%, artifact 50%
   - Minimized: Chat 10%, artifact 90%

These live in `ChatUIContext.jsx` and drive `FluidChatLayout.jsx` for desktop and `MobileArtifactDrawer.jsx` for mobile.

### 3.2 View Mode: Feasible via layoutMode Extension

**Recommended approach:** Add `'view'` to `layoutMode` enum.

```
layoutMode = 'view':
  Chat: 0% (hidden)
  Artifact: 100% (full screen)
  Widget: Floating chat button (bottom-right)
```

**Files to modify:**

| File | Change |
|------|--------|
| `packages/frontend/chat-ui/src/context/ChatUIContext.jsx` | Add `'view'` to layoutMode, add widgetOverlay state |
| `packages/frontend/chat-ui/src/components/chat/FluidChatLayout.jsx` | Add `'view'` case: chat width 0, artifact width 100 |
| `packages/frontend/shell/src/chat/pages/ChatPage.jsx` | Add view mode render path, widget button, overlay logic |
| `packages/frontend/chat-ui/src/components/chat/ArtifactPanel.jsx` | Add `viewMode` prop, exit button |
| `packages/frontend/chat-ui/src/components/chat/ChatInterface.jsx` | Update toggle button logic for view mode |
| `packages/frontend/chat-ui/src/components/chat/MobileArtifactDrawer.jsx` | Support full-screen mode |

**Widget overlay (chat in View Mode):**
- When `layoutMode === 'view'`: render a floating button (bottom-right)
- Click button -> overlay modal with ChatInterface
- ChatInterface in overlay receives current artifact context via `ChatUIContext`
- Close overlay -> back to clean view mode

**The `GlobalChatWidgetWrapper` pattern already exists** for non-ChatPage routes. We can adapt this pattern for View Mode on the ChatPage itself.

### 3.3 Artifact Context for Widget Chat

**Question from Platform:** Can the widget chat receive artifact context?

**Yes.** The `ChatUIContext` already holds:
- Current workflow name
- Current chat_id
- Current artifact messages

When the widget overlay opens, it has full access to this context. We can additionally inject:
- `currentArtifactType` (which UI tool is rendered)
- `currentArtifactPayload` (the data being displayed)
- `currentArtifactId` (for action reference)

This allows the chat to say: "You're viewing [artifact type] with [this data]. How can I help?"

### 3.4 View Mode Summary

| Aspect | Assessment |
|--------|------------|
| Feasibility | **High** -- frontend primitives exist |
| Breaking changes | **None** -- additive layoutMode value |
| Backend changes | **None** -- purely frontend |
| Mobile support | **Moderate** -- MobileArtifactDrawer needs full-screen state |
| Widget overlay | **Moderate** -- adapt GlobalChatWidgetWrapper pattern |

---

## Part 4: Rich Artifact Primitives Assessment

### 4.1 Current State: No Core Primitives

Core currently has **zero** built-in artifact rendering primitives. All rendering is delegated:

```
Core: "Here's a JSON payload and a component name"
Frontend: UIToolRenderer dynamically imports workflow-specific React component
Component: Renders whatever it wants
```

This is maximally flexible but means:
- Every workflow must ship its own React components
- No consistency between workflows
- No reusability across the ecosystem

### 4.2 The Proposal: Core-Owned Primitives

Platform proposes core own: `grid`, `list`, `table`, `form`, `card`, `chart`, `search`, `filters`, `markdown`, `composite`.

**Core's position:**

This is a significant scope expansion. Core should own **a primitive rendering system** but with important constraints:

1. **Primitives must be data-driven.** The agent sends JSON describing what to render. The frontend interprets it. No React component shipping from workflows.

2. **Primitives must be a vocabulary, not a framework.** Simple, composable building blocks. Not a full design system.

3. **Start small.** Core should ship:
   - `markdown` (already exists via chat messages)
   - `table` (rows + columns + optional actions)
   - `form` (fields + submit action)
   - `card` (title + body + optional actions)
   - `list` (items + optional actions per item)
   - `composite` (array of the above)

4. **Templates (dashboard, marketplace, etc.) belong in platform.** These are opinionated layouts. Core provides primitives; platform composes them.

5. **Card templates (investment, profile, metric) belong in platform.** These encode domain-specific styling. Core provides a generic `card` primitive; platform provides themed wrappers.

### 4.3 Proposed Primitive Schema

```json
{
    "artifact_type": "core.table",
    "artifact_id": "results_table",
    "title": "Search Results",
    "data": {
        "columns": [
            { "key": "name", "label": "Name", "type": "text" },
            { "key": "status", "label": "Status", "type": "badge" },
            { "key": "votes", "label": "Votes", "type": "number" }
        ],
        "rows": [
            { "id": "1", "name": "App Alpha", "status": "active", "votes": 24 }
        ]
    },
    "actions": [
        {
            "label": "Vote",
            "tool": "platform.vote",
            "params_template": { "app_id": "{{id}}" },
            "style": "secondary",
            "scope": "row"
        }
    ]
}
```

Core renders the table. Actions trigger tool calls via the action system (Part 2). Platform provides the tool implementations.

### 4.4 Boundary: What Core Owns vs Platform Owns

| Layer | Owner | Examples |
|-------|-------|---------|
| Primitive types | **Core** | table, form, card, list, composite, markdown |
| Primitive rendering | **Core** | React components for each primitive type |
| Action routing | **Core** | artifact.action -> tool executor -> result |
| Layout templates | **Platform** | dashboard = [metrics_row, chart, activity_feed] |
| Card templates | **Platform** | investment_card = card + funding_bar + invest_action |
| Domain tools | **Platform** | platform.invest, platform.vote, etc. |
| Workflow components | **Workflows** | Custom components via UIToolRenderer (unchanged) |

---

## Part 5: Navigation as Workflow Triggers

### 5.1 Current Navigation Model

```json
// GET /api/navigation returns:
{
    "navigation": [
        { "label": "Dashboard", "path": "/dashboard", "icon": "home" }
    ]
}
```

Frontend routes to traditional pages.

### 5.2 Proposed Extension: Workflow Triggers

We can extend the navigation contract **additively**:

```json
{
    "navigation": [
        // Traditional route (backward compat)
        { "label": "Settings", "path": "/settings", "icon": "gear" },

        // Workflow trigger (new)
        {
            "label": "Dashboard",
            "icon": "dashboard",
            "trigger": {
                "type": "workflow",
                "workflow": "platform.dashboard",
                "input": {},
                "mode": "view",
                "cache_ttl": 300
            }
        },

        // Workflow trigger with ask mode
        {
            "label": "Build New App",
            "icon": "add",
            "trigger": {
                "type": "workflow",
                "workflow": "platform.app_builder",
                "input": {},
                "mode": "workflow"
            }
        }
    ]
}
```

**Navigation item click flow:**
1. If `trigger` is absent: traditional route (existing behavior)
2. If `trigger.type === "workflow"`:
   a. Call `POST /api/chats/{app_id}/{workflow_name}/start`
   b. Connect WebSocket
   c. Set `layoutMode` based on `trigger.mode` (`"view"` | `"workflow"` | `"ask"`)
   d. If `cache_ttl` set and cached result exists: render from cache, skip workflow

**Backend changes:** None for routing. The `/api/navigation` endpoint just returns richer JSON. Frontend interprets the `trigger` field.

**Frontend changes:** Navigation component checks for `trigger` field and calls workflow start API instead of `router.push()`.

### 5.3 Assessment

| Aspect | Assessment |
|--------|------------|
| Feasibility | **High** -- additive change to navigation contract |
| Breaking changes | **None** -- items without `trigger` work as before |
| Backend | **Minimal** -- navigation config already supports arbitrary JSON |
| Frontend | **Moderate** -- navigation click handler needs workflow dispatch logic |
| Cache | **Future** -- requires artifact state persistence (see Part 6 of Platform proposal) |

---

## Part 6: Composite Artifacts

### 6.1 Current State

Not natively supported. A workflow component can internally render multiple sub-components, but core has no concept of "multiple artifacts in one view."

### 6.2 Proposed Approach

A `composite` artifact type that contains an array of child artifacts:

```json
{
    "artifact_type": "core.composite",
    "artifact_id": "dashboard_view",
    "layout": "grid",
    "grid_template": "2x2",
    "children": [
        {
            "artifact_type": "core.card",
            "data": { "title": "Revenue", "value": "$12,450", "trend": "+5.2%" }
        },
        {
            "artifact_type": "core.card",
            "data": { "title": "Users", "value": "1,234", "trend": "+12%" }
        },
        {
            "artifact_type": "core.table",
            "data": { "columns": [...], "rows": [...] }
        },
        {
            "artifact_type": "core.list",
            "data": { "items": [...] }
        }
    ]
}
```

Core renders the grid layout and each child artifact. This is how platform would build "dashboard template" without core needing to know about dashboards.

---

## Part 7: Architecture Concerns

### 7.1 Concern: Scope Creep into UI Framework

Platform's proposal moves core toward being a **UI framework** (primitives, templates, card types, action system, optimistic updates). This risks:

- **Maintenance burden**: Core becomes responsible for UI component bugs
- **Design coupling**: Primitive aesthetics become a contract surface
- **Innovation friction**: Every new UI pattern requires a core change

**Mitigation:** Keep primitives truly primitive. Data-driven JSON -> render. No opinions on color, spacing, or animation. Platform provides CSS themes that style core primitives.

### 7.2 Concern: Action System Complexity

Tool execution outside the agent loop is new territory. Concerns:

- **Authentication**: Actions need user context. Who validates the JWT for an action?
- **Authorization**: Can any user trigger any tool via an action button?
- **State**: If the agent has finished, actions are stateless. Is that always correct?
- **Transactions**: If an action fails after optimistic update, rollback is complex

**Mitigation:** Start with stateless action execution (option (b) from Part 2). Actions are just tool calls with user context. Authorization is the tool's responsibility. Optimistic updates are frontend-only and rollback on failure.

### 7.3 Concern: AG-UI Compliance vs. Our Strengths

AG-UI is designed for single-agent, single-turn interactions. Our system supports:
- Multi-agent orchestration (GroupChat, handoffs)
- Persistent sessions (resume, replay)
- Workflow packs (multi-workflow journeys)
- Human-in-the-loop with complex input types

**Some of our events have no AG-UI equivalent** (handoff, select_speaker, resume_boundary, usage tracking). We should:
- Emit AG-UI events as a compatibility layer
- Keep our proprietary events for features AG-UI doesn't cover
- Market this as "AG-UI compatible with production extensions"

### 7.4 Concern: Navigation as Workflow Triggers

If every page is a workflow, then:
- Cold start for every navigation click (workflow start + agent init + LLM call)
- No static content possible without workflow overhead
- Cache invalidation becomes critical

**Mitigation:**
- Support both traditional routes AND workflow triggers in navigation
- Artifact caching with TTL
- "Instant" mode: pre-computed artifacts served from cache, workflow re-runs in background
- Not all pages should be agent-rendered. Static content (settings, profile) should remain routes.

---

## Part 8: Recommended Implementation Sequence

Based on impact, dependencies, and risk:

### Phase 1: AG-UI Event Adapter (Non-Breaking)

**Scope:**
- New module: `event_agui_adapter.py`
- Wraps existing events into AG-UI envelopes
- Dual-emit: existing `chat.*` + new `agui.*` namespace
- Add `threadId` (= `{app_id}:{chat_id}`) to lifecycle events
- Add `TextMessageStart` / `TextMessageEnd` framing around print streams
- Update contract v1.1 with AG-UI event documentation

**Dependencies:** None
**Risk:** Low (additive, non-breaking)

### Phase 2: View Mode (Frontend Only)

**Scope:**
- Add `'view'` to `layoutMode` in `ChatUIContext`
- Update `FluidChatLayout` with view case
- Update `ChatPage` with view mode rendering
- Add widget button + overlay (adapt `GlobalChatWidgetWrapper`)
- Update `ArtifactPanel` with viewMode prop

**Dependencies:** None
**Risk:** Low (frontend only, additive)

### Phase 3: Artifact Action System

**Scope:**
- New inbound message type: `artifact.action`
- Action routing in `SimpleTransport`
- Stateless tool executor (reuse existing tool infrastructure)
- New outbound events: `artifact.action.started/completed/failed`
- Frontend action button rendering
- Frontend action event emission

**Dependencies:** None, but benefits from Phase 2 (view mode for full-screen artifact actions)
**Risk:** Medium (new execution path, auth/authz considerations)

### Phase 4: Core Artifact Primitives

**Scope:**
- Define primitive schema (table, form, card, list, composite, markdown)
- Implement React components for each primitive
- Integrate with action system (Phase 3)
- Primitive component library in `packages/frontend/chat-ui/src/primitives/`

**Dependencies:** Phase 3 (actions needed for interactive primitives)
**Risk:** Medium (design decisions, scope management)

### Phase 5: Navigation Workflow Triggers

**Scope:**
- Extend `/api/navigation` response with `trigger` field
- Frontend navigation handler for workflow triggers
- Mode specification per navigation item

**Dependencies:** Phase 2 (view mode), Phase 4 (primitives for rendering results)
**Risk:** Low (additive contract change)

### Phase 6: State Management (StateDelta, Caching)

**Scope:**
- Implement `agui.StateSnapshot` and `agui.StateDelta` events
- Artifact state storage (MongoDB)
- Cache TTL for navigation-triggered artifacts
- Optimistic update engine (frontend)

**Dependencies:** Phase 1 (AG-UI events), Phase 3 (actions), Phase 4 (primitives)
**Risk:** Medium-High (state management is complex)

---

## Part 9: Answers to Platform's Direct Questions

| # | Question | Answer |
|---|----------|--------|
| 1 | How close are our current events to AG-UI? | **~60-80% semantic coverage.** Lifecycle and tool events are very close. Text message framing and state management are gaps. See Part 1. |
| 2 | What's the effort to rename events? | **Low, via dual-emit.** We can alias without breaking existing consumers. New `event_agui_adapter.py` module. |
| 3 | Does the current artifact system support actions that call tools? | **No.** Artifacts are render-only. `onResponse` is one-shot and blocks the emitting tool. See Part 2. |
| 4 | Is composite artifact pattern supported? | **No.** Not natively. Workflow components can internally compose, but core has no composite concept. |
| 5 | Can artifacts declare optimistic updates? | **No.** No optimistic update mechanism exists. Would be a frontend-only feature. |
| 6 | Can we add View Mode? | **Yes.** Feasible by extending `layoutMode`. Frontend-only change. See Part 3. |
| 7 | Can the widget chat receive artifact context? | **Yes.** `ChatUIContext` already holds all needed state. See Part 3.3. |
| 8 | Can navigation items trigger workflows instead of routes? | **Yes.** Additive extension to `/api/navigation` contract. See Part 5. |
| 9 | Can we specify which mode to render in? | **Yes.** Navigation trigger can include `mode` field. Frontend sets `layoutMode` accordingly. |

---

## Part 10: What Core Will NOT Do

To maintain design philosophy (generality, isolation, predictability):

1. **Core will NOT own layout templates** (dashboard, marketplace, detail, feed, settings). These are opinionated compositions that belong in platform or workflow code.

2. **Core will NOT own card templates** (investment, profile, metric). These encode domain-specific styling. Core provides a generic `card` primitive.

3. **Core will NOT embed revenue or monetization logic** in primitives or actions. The action system routes tool calls; it doesn't know what "invest" means.

4. **Core will NOT make AG-UI the only event protocol.** Existing `chat.*` events remain the primary protocol. AG-UI events are an additional compatibility layer.

5. **Core will NOT require all pages to be workflow-rendered.** Traditional routes remain supported alongside workflow triggers.

---

## Coordination Status

| # | Requirement | Core Response | Status |
|---|-------------|---------------|--------|
| 1 | AG-UI event alignment | Dual-emit adapter approach | **APPROVED** -- will implement |
| 2 | Artifact action system | New capability needed | **APPROVED** -- will implement |
| 3 | View Mode | Frontend layoutMode extension | **APPROVED** -- will implement |
| 4 | Widget overlay | Adapt GlobalChatWidgetWrapper | **APPROVED** -- will implement |
| 5 | Composite artifacts | New `core.composite` primitive | **APPROVED** -- Phase 4 |
| 6 | Layout templates | **DECLINED for core** -- platform owns | Platform responsibility |
| 7 | Navigation workflow triggers | Additive contract change | **APPROVED** -- Phase 5 |
| 8 | Pin/persist | Future phase | **ACKNOWLEDGED** -- Phase 6+ |
| 9 | Optimistic updates | Frontend-only mechanism | **APPROVED** -- Phase 6 |

---

## Next Steps

Core requests:

1. **Platform confirms** this response addresses the requirements
2. **Platform identifies** which phases are blocking for platform roadmap
3. **Core begins** Phase 1 (AG-UI adapter) and Phase 2 (View Mode) in parallel
4. **Coordination checkpoint** after Phase 2 before starting Phase 3 (action system)

---

*Core is ready to execute. Awaiting platform confirmation.*
