> **Doc Status:** reference

# Platform Confirmation + Implementation Checklist (Mozaiks Core)

**Source:** mozaiks-platform → mozaiks-core  
**Date:** January 29, 2026  
**Status:** ✅ Approved — Proceed with Implementation  
**Last updated (core):** January 31, 2026

This is a **living coordination checklist** mirrored inside mozaiks-core so the
core team can track progress and update status as work lands. It mirrors the
platform confirmation document and is updated in core as phases are completed.

---

## Mozaiks-Core Requests (Open)

Use this section to request clarifications, decisions, or resources from Platform.
Copy/paste this document to Platform as-is and update statuses below.

- [ ] Confirm Phase 6 checkpoint after platform smoke-test of state management features.

---

## MESSAGE TO mozaiks-platform (VIA HUMAN)

Phase 6 acknowledgment received. Core has **implemented Phase 6: State Management**. Summary for your review (stateless context below):

**Phase 5 status:** ✅ Approved (2026-01-30)

**Phase 6 scope (stateless context so you don’t need other docs):**
State synchronization + caching polish for artifacts and chat.

Implemented event types:
- `agui.state.StateSnapshot` — full artifact state on initial render
- `agui.state.StateDelta` — JSON Patch (RFC 6902) deltas for updates
- `agui.state.MessagesSnapshot` — full conversation history on reconnect

Backend storage (implemented):
- Artifact state storage keyed by `{ artifact_id, chat_id, app_id }` with TTL support
- Cache API: `GET /api/artifacts/{artifact_id}/cached`

Frontend behavior:
- Apply `StateSnapshot` + `StateDelta` to keep artifact state consistent
- Messages snapshot handler on reconnect
- Optimistic update engine for action results (confirm/rollback)
- Local cache for navigation triggers (with updates on state changes)

**Request:** Please confirm **Checkpoint 6** after a quick smoke-test of state management features.

Open items left unchecked in the checklist:

Cache invalidation on relevant actions
“Optimistic update patterns” doc section
Phase 6 test cases
If you want me to continue, I suggest:

Run a quick smoke test: artifact render → action update (patch) → verify StateDelta applies + cache endpoint returns updated state.
Decide whether to add explicit cache invalidation (or keep “update-in-place”).
When ready, I can batch a commit + push.


---

## Platform Response (Received)

**Date:** 2026-01-30  
**Phase 6:** Acknowledged — Proceed when ready (non-blocking for MVP).

Platform confirmed:
- ✅ `agui.state.StateSnapshot`, `StateDelta`, `MessagesSnapshot`
- ✅ JSON Patch (RFC 6902) for deltas
- ✅ Artifact state storage with TTL
- ✅ Cache API `GET /api/artifacts/{artifact_id}/cached`
- ✅ Frontend optimistic update engine

Platform reminder:
- Phase 6 is polish; MVP can ship with Phases 1–5.
- Checkpoint 6 will be confirmed after smoke test; no rush.

---

## 🚦 Checkpoint Approvals (Platform)

| Phase | Status | Date | Notes |
|-------|--------|------|-------|
| Phase 2 | ✅ Approved | 2026-01-30 | Proceed to Phase 3 |
| Phase 3 | ✅ Approved | 2026-01-30 | Proceed to Phase 4 |
| Phase 4 | ✅ Approved | 2026-01-30 | Proceed to Phase 5 |
| Phase 5 | ✅ Approved | 2026-01-30 | Proceed to Phase 6 |

---

## TL;DR

Platform confirms Core's response fully addresses requirements. Approved:
- ✅ Dual-emit AG-UI adapter approach (non-breaking)
- ✅ Artifact action system architecture
- ✅ View Mode via `layoutMode` extension
- ✅ Core-owned primitives (table, form, card, list, composite, markdown)
- ✅ Navigation workflow triggers (additive)
- ✅ Phase sequencing as proposed

Platform owns:
- ✅ Layout templates (dashboard, marketplace, detail, feed, settings)
- ✅ Card templates (investment_card, profile_card, metric_card, etc.)
- ✅ Domain tools (platform.invest, platform.vote, etc.)
- ✅ Theming/CSS for core primitives

Critical path: **Phase 2 → Phase 3 → Phase 4 → Phase 5** (complete) → Phase 6

---

## Phase 1: AG-UI Event Adapter

**Goal:** Dual-emit existing events + AG-UI formatted events (non-breaking)

### Backend Tasks
- [x] Create `event_agui_adapter.py` module in `packages/python/ai-runtime/mozaiks_ai/runtime/`
- [x] Implement wrapper function that transforms internal events to AG-UI format
- [x] Add `threadId` field to lifecycle events (format: `{app_id}:{chat_id}`)
- [x] Add `runId` field to all events within a workflow run
- [x] Implement `agui.lifecycle.RunStarted` wrapper for `chat.orchestration.run_started`
- [x] Implement `agui.lifecycle.RunFinished` wrapper for `chat.orchestration.run_completed`
- [x] Implement `agui.lifecycle.RunError` wrapper for `chat.orchestration.run_failed`
- [x] Implement `agui.lifecycle.StepStarted` wrapper for `chat.orchestration.agent_started`
- [x] Implement `agui.lifecycle.StepFinished` wrapper for `chat.orchestration.agent_completed`
- [x] Implement `agui.text.TextMessageStart` (new event, emit before first `chat.print`)
- [x] Implement `agui.text.TextMessageContent` wrapper for `chat.print`
- [x] Implement `agui.text.TextMessageEnd` (new event, emit after `chat.text`)
- [x] Implement `agui.tool.ToolCallStart` wrapper for `chat.tool_call`
- [x] Implement `agui.tool.ToolCallEnd` (new event, emit before `chat.tool_response`)
- [x] Implement `agui.tool.ToolCallResult` wrapper for `chat.tool_response`
- [x] Update `SimpleTransport.send()` to dual-emit both `chat.*` and `agui.*` events
- [x] Add configuration flag to enable/disable AG-UI emission (default: enabled)

### Documentation Tasks
- [x] Update `runtime-platform-contract-v1.md` with AG-UI event mappings
- [x] Document AG-UI event namespace (`agui.*`) in API docs
- [x] Add migration guide for consumers switching from `chat.*` to `agui.*`

### Testing Tasks
- [x] Unit tests for `event_agui_adapter.py` transformations
- [ ] Integration test: verify dual-emit produces both event namespaces
- [ ] Verify existing `chat.*` consumers still work (backward compat)

---

## Phase 2: View Mode

**Goal:** Add `'view'` to `layoutMode`, full-screen artifact with chat widget overlay

### Frontend Tasks — ChatUIContext
- [x] Add `'view'` to `layoutMode` type in `ChatUIContext.jsx`
- [x] Add `widgetOverlayOpen` state (boolean)
- [x] Add `setWidgetOverlayOpen` action
- [x] Add `currentArtifactContext` state (type, payload, id for widget context)
- [x] Update `setCurrentArtifact` to populate `currentArtifactContext`

### Frontend Tasks — FluidChatLayout
- [x] Add `'view'` case to layout width calculations
  - Chat width: 0%
  - Artifact width: 100%
- [x] Conditionally hide chat container when `layoutMode === 'view'`
- [x] Ensure artifact panel receives full width

### Frontend Tasks — ChatPage
- [x] Add View Mode render path
- [x] Render widget button (bottom-right) when `layoutMode === 'view'`
- [x] Widget button click → `setWidgetOverlayOpen(true)`
- [x] Render overlay modal when `widgetOverlayOpen === true`
- [x] Overlay contains `ChatInterface` component
- [x] Overlay close button → `setWidgetOverlayOpen(false)`
- [x] Pass `currentArtifactContext` to ChatInterface in overlay

### Frontend Tasks — ArtifactPanel
- [x] Add `viewMode` prop
- [x] When `viewMode === true`: hide close/minimize buttons, show exit-to-workflow button
- [x] Exit button → `setLayoutMode('workflow')` or `setLayoutMode('split')`

### Frontend Tasks — ChatInterface
- [x] Accept `artifactContext` prop for contextual awareness
- [x] When in overlay mode: show close button, constrained height
- [x] Inject artifact context into system message or agent context

### Frontend Tasks — MobileArtifactDrawer
- [x] Support full-screen state for View Mode on mobile
- [x] Add widget button for mobile View Mode
- [x] Add mobile-optimized overlay chat

### Frontend Tasks — Mode Toggle
- [x] Update mode toggle UI to support three modes
- [x] Add View Mode icon/option to toggle
- [ ] Keyboard shortcut for mode cycling (optional)

### Testing Tasks
- [ ] Visual test: View Mode renders artifact full-screen
- [ ] Visual test: Widget button appears in correct position
- [ ] Visual test: Overlay chat opens/closes correctly
- [ ] Functional test: Overlay chat has artifact context
- [ ] Mobile test: View Mode works on mobile viewports

---

## Phase 3: Artifact Action System

**Goal:** Artifact buttons can trigger tool calls outside agent loop

### Backend Tasks — Message Types
- [x] Define `artifact.action` inbound message schema
- [x] Define `artifact.action.started` outbound event schema
- [x] Define `artifact.action.completed` outbound event schema
- [x] Define `artifact.action.failed` outbound event schema

### Backend Tasks — SimpleTransport
- [x] Add handler for `artifact.action` message type in WebSocket handler
- [x] Parse action message, extract tool name and params
- [x] Validate user context (auth/authz from WebSocket session)
- [x] Route to stateless tool executor

### Backend Tasks — Stateless Tool Executor
- [x] Create `action_executor.py` module
- [x] Implement `execute_action(tool_name, params, user_context)` function
- [x] Resolve tool function from tool registry (reuse existing tool loading)
- [x] Execute tool function with params
- [x] Return result or error

### Backend Tasks — Action Result Handling
- [x] On success: emit `artifact.action.completed` with result
- [x] Include `artifact_update` in result
- [x] On failure: emit `artifact.action.failed` with error message
- [x] Include `rollback: true` hint for frontend optimistic update rollback

### Frontend Tasks — Action Rendering
- [x] Parse `actions[]` array from artifact payload
- [x] Render action buttons based on action config
- [x] Interpolate `{{field}}` references from artifact data
- [x] Support `scope: "row"` for per-row actions

### Frontend Tasks — Action Event Emission
- [x] On button click: generate `action_id` (uuid)
- [x] Build `artifact.action` message
- [x] Send via WebSocket
- [x] Set button to loading state

### Frontend Tasks — Optimistic Updates
- [x] Apply optimistic delta immediately (JSON Patch or object merge)
- [x] Store pre-optimistic state for potential rollback
- [x] On `artifact.action.completed`: apply final state
- [x] On `artifact.action.failed` with `rollback: true`: revert to pre-optimistic state
- [x] Clear loading state

### Frontend Tasks — Action Result Handling
- [x] Listen for `artifact.action.completed` events
- [x] Match by `action_id`
- [x] Apply replace/patch updates
- [x] Listen for `artifact.action.failed` events
- [ ] Show error toast/notification (UI polish)

### Documentation Tasks
- [x] Document `artifact.action` message format in contract
- [x] Document action schema in artifact payload spec
- [x] Document optimistic update pattern
- [x] Add examples for common action patterns (vote, delete, submit form)

### Testing Tasks
- [ ] Unit test: action routing in SimpleTransport
- [x] Unit test: stateless tool executor
- [ ] Integration test: full action flow (click → tool → result → update)
- [ ] Integration test: optimistic update + rollback on failure
- [ ] E2E test: action button in rendered artifact

---

### Phase 4: Core Artifact Primitives

**Goal:** Core-owned data-driven artifact components

#### Schema Definition Tasks

- [x] Define `core.markdown` schema (already exists, formalize)
- [x] Define `core.card` schema:
  ```json
  {
    "artifact_type": "core.card",
    "artifact_id": "string",
    "title": "string",
    "subtitle": "string?",
    "body": "string | markdown?",
    "image": "url?",
    "metadata": [{ "label": "string", "value": "string" }],
    "actions": [ActionSchema]
  }
  ```
- [x] Define `core.list` schema:
  ```json
  {
    "artifact_type": "core.list",
    "artifact_id": "string",
    "title": "string?",
    "items": [{ "id": "string", "title": "string", "subtitle": "string?", "icon": "string?", "actions": [ActionSchema] }]
  }
  ```
- [x] Define `core.table` schema:
  ```json
  {
    "artifact_type": "core.table",
    "artifact_id": "string",
    "title": "string?",
    "columns": [{ "key": "string", "label": "string", "type": "text|number|badge|date|actions" }],
    "rows": [{ "id": "string", ...data }],
    "actions": [ActionSchema],
    "row_actions": [ActionSchema]
  }
  ```
- [x] Define `core.form` schema:
  ```json
  {
    "artifact_type": "core.form",
    "artifact_id": "string",
    "title": "string?",
    "fields": [{ "name": "string", "type": "text|number|select|checkbox|textarea|date", "label": "string", "required": "boolean?", "options": []? }],
    "submit_action": ActionSchema,
    "cancel_action": ActionSchema?
  }
  ```
- [x] Define `core.composite` schema:
  ```json
  {
    "artifact_type": "core.composite",
    "artifact_id": "string",
    "layout": "stack|grid|columns",
    "grid_template": "string? (e.g., '2x2', '1-2-1')",
    "children": [ArtifactSchema]
  }
  ```
- [x] Define `ActionSchema`:
  ```json
  {
    "label": "string",
    "icon": "string?",
    "tool": "string",
    "params": {},
    "style": "primary|secondary|ghost|danger",
    "confirm": "string? (confirmation message)",
    "optimistic": {}?
  }
  ```

#### Frontend Tasks — Primitive Components

- [x] Create `packages/frontend/chat-ui/src/primitives/` directory
- [x] Implement `CoreCard.jsx` component
- [x] Implement `CoreList.jsx` component
- [x] Implement `CoreTable.jsx` component
- [x] Implement `CoreForm.jsx` component
- [x] Implement `CoreComposite.jsx` component
- [x] Implement `CoreMarkdown.jsx` component (formalize existing)
- [x] Create `PrimitiveRenderer.jsx` that routes `artifact_type` to component

#### Frontend Tasks — Integration

- [x] Update `UIToolRenderer` to check for `core.*` artifact types
- [x] Route `core.*` artifacts to `PrimitiveRenderer`
- [x] Continue routing workflow-specific artifacts to workflow components
- [x] Integrate action buttons with Phase 3 action system

#### Frontend Tasks — Styling

- [x] Create CSS variables for primitive theming
- [x] Ensure primitives inherit from global theme
- [x] Document CSS variable API for Platform customization

#### Documentation Tasks

- [x] Document all primitive schemas
- [x] Provide usage examples for each primitive
- [x] Document theming/CSS customization

#### Testing Tasks

- [ ] Visual test: each primitive renders correctly
- [ ] Functional test: form submission triggers action
- [ ] Functional test: table row actions work
- [ ] Functional test: composite renders children correctly
- [ ] Snapshot tests for primitive components

---

### Phase 5: Navigation Workflow Triggers

**Goal:** Navigation items can trigger workflows instead of routes

#### Backend Tasks

- [x] Extend `/api/navigation` response schema to support `trigger` field:
  ```json
  {
    "label": "string",
    "icon": "string",
    "path": "string?",  // Traditional route (optional)
    "trigger": {        // Workflow trigger (optional)
      "type": "workflow",
      "workflow": "string",
      "input": {},
      "mode": "view|workflow|ask",
      "cache_ttl": "number?"
    }
  }
  ```
- [x] Update navigation config loader to support new schema
- [x] No changes to workflow start API (already exists)

#### Frontend Tasks — Navigation Component

- [x] Update navigation item click handler
- [x] If `trigger` field exists:
  - Extract workflow name, input, mode
  - Call `POST /api/chats/{app_id}/{workflow_name}/start` (or reuse existing)
  - Connect WebSocket to workflow
  - Set `layoutMode` based on `trigger.mode`
- [x] If `trigger` field absent: traditional `router.push(path)`

#### Frontend Tasks — Mode Setting

- [x] On workflow trigger, set `layoutMode` from `trigger.mode`:
  - `"view"` → `setLayoutMode('view')`
  - `"workflow"` → `setLayoutMode('split')` or `setLayoutMode('workflow')`
  - `"ask"` → `setLayoutMode('full')`

#### Frontend Tasks — Cache (Basic)

- [x] If `trigger.cache_ttl` is set and cached artifact exists:
  - Render cached artifact immediately
  - Optionally refresh in background
- [x] Store last artifact result per workflow in memory/localStorage
- [x] Check cache before starting workflow

#### Documentation Tasks

- [x] Document navigation trigger schema in contract
- [x] Provide migration guide from route-based to trigger-based navigation
- [x] Document cache behavior

#### Testing Tasks

- [ ] Functional test: navigation item with trigger starts workflow
- [ ] Functional test: mode is set correctly based on trigger.mode
- [ ] Functional test: traditional route items still work
- [ ] Integration test: cache hit serves cached artifact

**Phase 5 status:** ✅ Approved by Platform (2026-01-30) — proceed to Phase 6.

---

### Phase 6: State Management (StateDelta, Caching, Optimistic)

**Goal:** Full state synchronization, caching, and optimistic updates

#### Backend Tasks — State Events

- [x] Implement `agui.state.StateSnapshot` event emission
  - Emit on initial artifact render
  - Include full artifact state
- [x] Implement `agui.state.StateDelta` event emission
  - Emit on artifact updates (from actions, tool results)
  - Use JSON Patch format (RFC 6902)
- [x] Implement `agui.state.MessagesSnapshot` event
  - Emit on reconnect/resume
  - Include full conversation history

#### Backend Tasks — Artifact State Storage

- [x] Create artifact state storage in MongoDB (or extend ChatSessions)
- [x] Schema: `{ artifact_id, chat_id, app_id, state, updated_at, ttl }`
- [x] Save artifact state after each render/update
- [x] Load artifact state for cache retrieval

#### Backend Tasks — Cache API

- [x] Implement `GET /api/artifacts/{artifact_id}/cached`
- [x] Return cached artifact state if exists and not expired
- [x] Return 404 if no cache or expired

#### Frontend Tasks — State Synchronization

- [x] Listen for `agui.state.StateSnapshot` events
- [x] Replace local artifact state with snapshot
- [x] Listen for `agui.state.StateDelta` events
- [x] Apply JSON Patch to local artifact state
- [x] Implement JSON Patch library integration (e.g., `fast-json-patch`)

#### Frontend Tasks — Optimistic Update Engine

- [x] Create optimistic update state manager
- [x] On action with `optimistic` field:
  - Apply optimistic delta to local state
  - Store undo operation
- [x] On action success: confirm optimistic state (clear undo)
- [x] On action failure: apply undo operation

#### Frontend Tasks — Cache Layer

- [x] Implement artifact cache in localStorage or IndexedDB
- [x] Cache key: `artifact:{workflow}:{input_hash}`
- [x] On navigation trigger with cache_ttl:
  - Check cache first
  - If hit: render immediately, optionally refresh
  - If miss: start workflow normally
- [ ] Invalidate cache on relevant actions

#### Documentation Tasks

- [x] Document StateDelta JSON Patch format
- [x] Document caching behavior and TTL
- [ ] Document optimistic update patterns

#### Testing Tasks

- [ ] Unit test: JSON Patch application
- [ ] Integration test: StateDelta updates artifact correctly
- [ ] Integration test: Optimistic update + confirmation
- [ ] Integration test: Optimistic update + rollback
- [ ] E2E test: Cache hit serves cached artifact

---

---

## Coordination Checkpoints

| Checkpoint | When | What Platform Needs |
|------------|------|---------------------|
| Checkpoint 1 | After Phase 1 | AG-UI event samples for Platform to test consumption |
| Checkpoint 2 | After Phase 2 | View Mode available for Platform to build dashboard views |
| Checkpoint 3 | After Phase 3 | Action system available for Platform to wire up domain tools |
| Checkpoint 4 | After Phase 4 | Primitives available for Platform to build templates on top |
| Checkpoint 5 | After Phase 5 | Nav triggers available for Platform to migrate navigation |
| Checkpoint 6 | After Phase 6 | Full state management for production polish |

---

## Platform Questions (Clarifications)

1. **Auth for actions:** ✅ Confirmed — actions inherit auth from WebSocket session (JWT validated on connect). No per-action auth headers required. Platform handles authorization inside tool implementations.

2. **Tool registry for actions:** ✅ Confirmed — use the same registry as agent tools. Actions call tools outside the agent loop (no new registry).

3. **Primitive theming:** ✅ Platform confirmed base CSS variable list is sufficient for MVP. Nice-to-have additions (non-blocking): `--core-primitive-hover`, `--core-primitive-focus`, `--core-primitive-error`, `--core-primitive-success`, `--core-primitive-spacing`.

4. **AG-UI namespace:** ✅ Confirmed — nested namespaces: `agui.lifecycle.*`, `agui.text.*`, `agui.tool.*`, `agui.state.*`.

---

## Mozaiks-Core Requests (Resolved)

- [x] Confirm artifact action auth model (WS session auth only vs per-action auth headers).
- [x] Confirm whether action tools use the same registry as agent tools (no new registry).
- [x] Provide final AG-UI namespace decision.
- [x] Confirm core primitive CSS variable list is sufficient for MVP.
- [x] Confirm Phase 4 checkpoint after platform smoke-test of core primitives.
- [x] Confirm Phase 5 checkpoint after platform smoke-test of navigation workflow triggers.
