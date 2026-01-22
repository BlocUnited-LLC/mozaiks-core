# Ask Mozaiks General Agent Plan

## Purpose

Create a persistent "Ask Mozaiks" companion that rides along with every workflow, gives users instant answers, and keeps context about where they are in the runtime. This doc scopes the agent, maps dependencies to existing runtime plumbing, and outlines a staged plan so we do not bolt random logic onto the stack.

---

## Current Runtime Baseline (Nov 2025)

- **Manual Workflow Switching** (`core/transport/session_registry.py`, `core/transport/simple_transport.py`, `docs/interactive-artifacts/MANUAL_WORKFLOW_SWITCHING.md`)
  - UI can start/switch/pause workflows and enter "general" mode via explicit WebSocket events.
  - SessionRegistry tracks active vs paused workflows per WebSocket (`ws_id`).
- **Transport + FastAPI** (`shared_app.py`)
  - `/ws/{workflow_name}/{app_id}/{chat_id}/{user_id}` wires clients into AG2 workflows and now carries `ws_id` metadata for switching.
  - `/api/sessions/list/{app_id}/{user_id}` lets the UI fetch tabs to render "Generator", "Investor", etc.
- **Generator Workflow Simulation** (`scripts/start-app.ps1` → Compose stack)
  - Launches FastAPI + supporting services so we can reproduce the scenario: begin Generator, move to Discover, click Ask Mozaiks.
- **Context Awareness Today**
  - The runtime knows app_id, user_id, workflow_name, and existing chat_ids, but it does not derive a higher-level "user journey" summary.

---

## Desired UX Walkthrough

1. **Start the app** using `./scripts/start-app.ps1` and open the Workflow Generator.
2. While mid-run, switch to the Discover view; the UI shows two workflow tabs via the existing session list endpoint.
3. User clicks an **"Ask Mozaiks" control** (tab, toggle, or CTA in the Discover view).
4. Runtime enters general mode (`chat.enter_general_mode`) and routes messages to a **general companion agent** (LLM call, not AG2).
5. The agent already knows:
   - Which workflow(s) are running and their status.
   - Which UI surface is active (Generator vs Discover vs Marketing) so it can provide relevant guidance.
   - App/user metadata for tenancy enforcement.
6. The user can return to a workflow tab at any time; the conversation history with Ask Mozaiks stays available during the session.

---

## Scope Options

| Option | Description | Pros | Cons |
| ------ | ----------- | ---- | ---- |
| **A. Generic Companion (MVP)** | Single knowledge pack (runtime + product help) shipped with the platform. | Fastest; no generator integration required. | Lacks app-specific knowledge beyond workflow metadata. |
| **B. Generator-Defined KB** | Generator workflow produces declarative JSON describing the user-facing knowledge base (e.g., glossary, portal descriptions). | Keeps agent grounded in each app's semantics. | Requires extending generator outputs + storage, higher complexity. |
| **C. Hybrid** | Ship generic pack now, add generator hooks later. | Allows immediate UX win while staying upgradeable. | Must keep KB schema backward-compatible.

Recommendation: implement **Option C**—start with the generic companion but design the surface so future workflows can push KB entries.

---

## Architecture Proposal

### Components

1. **Ask Mozaiks Service Layer** (new module, e.g., `core/assistant/ask_mozaiks.py`)
   - Stateless async helper that accepts a context payload and proxies the request to the configured LLM (OpenAI, Azure OpenAI, etc.).
   - Handles rate limiting, retries, telemetry, and redaction of secrets.
2. **Context Package Builder**
   - Pulls data from: session registry (active workflows, statuses), persistence (recent artifacts or progress percent), UI-supplied surface identifier (e.g., `view=discover`).
   - Optional future hook: generator-provided KB stub stored per app/workflow.
3. **Transport Hooks**
   - When the UI sends `chat.enter_general_mode`, SimpleTransport should flag the connection as "general" and route subsequent `user.message` events to the Ask Mozaiks helper instead of AG2.
   - Add a new WebSocket event, e.g., `chat.ask_mozaiks`, that carries optional hints (`view`, `selected_workflow`, `custom_prompt`).
4. **Persistence (Optional MVP)**
   - Store Ask Mozaiks exchanges in the same Mongo chat document with role `assistant` + `source=general_agent` so transcripts stay in sync.
5. **Observability**
   - Emit structured logs and metrics (`general_agent_call_duration`, `general_agent_tokens_used`) for MozaiksPay billing alignment.

### Data Flow

```
UI (Ask Mozaiks CTA)
  └─ ws.send({ type: "chat.ask_mozaiks", message, view })
      └─ SimpleTransport detects general mode, builds context package
          └─ AskMozaiksService → OpenAI (LLM call)
              └─ Response streamed back as `chat.general_response`
```

### Context Fields to Include

- `app_id`, `user_id`, `ws_id` (for tenancy + logging).
- Active workflow summaries: name, status, percent complete, last artifact headline.
- UI surface metadata (`view`, `route`, `selected_app_id`).
- Optional KB excerpt (paragraph describing the app/portal or user persona).

---

## Implementation Phases

1. **Phase 0 – Planning & Instrumentation (this doc)**
   - Document requirements, dependencies, and UX contract.
   - Confirm that manual switching + session registry cover the needed control flow (done).

2. **Phase 1 – Generic Ask Mozaiks MVP**
   - Add transport support for `chat.ask_mozaiks` + message routing when in general mode.
   - Implement AskMozaiksService with pluggable LLM adapter.
   - Persist responses to Mongo and broadcast to UI (`chat.general_response`).
   - Provide a minimal prompt template ("You are Mozaiks, a companion that knows..."), seeded with runtime context.

3. **Phase 2 – Generator Knowledge Pack (Optional)**
   - Extend generator outputs (e.g., `workflows/<app>/knowledge_pack.json`) so creators can declare:
     - User personas / skill level.
     - Portal descriptions, guardrails, FAQ entries.
   - Runtime loads the pack per app_id and merges it into the context package.

4. **Phase 3 – Advanced Features**
   - Inline citations / deep links into the active workflow.
   - Ability for Ask Mozaiks to trigger lightweight actions (e.g., open a portal) via existing WebSocket events.
   - LLM intent detection fallback: if user types natural language without clicking a button, Ask Mozaiks can suggest switching workflows.

---

## Dependencies & Integration Points

- **Existing Runtime Hooks**
  - Reuse `session_registry.enter_general_mode` to keep workflows paused while Ask Mozaiks runs.
  - `SimpleTransport` already handles user messages; add a branch when `session_registry.is_in_general_mode(ws_id)` is true.
- **LLM Configuration**
  - Use the same environment variables as workflows (e.g., `OPENAI_API_KEY`) but keep calls outside AG2 to avoid double orchestration.
  - Provide switches for app-specific providers if needed.
- **Generator Workflow Alignment**
  - When we extend generator outputs, treat them as declarative JSON to stay within runtime constraints (no custom code per app).
- **Frontend Work**
  - UI needs an "Ask Mozaiks" CTA plus a surface for the chat log (either a dedicated tab or inline overlay).
  - Provide event mapping reference similar to `MANUAL_WORKFLOW_SWITCHING.md`.

---

## Testing & Validation Plan

1. **Local Simulation**
   - Run `./scripts/start-app.ps1`.
   - Start the Generator workflow, reach an intermediate step.
   - Switch to Discover, trigger `chat.enter_general_mode`, then send `chat.ask_mozaiks` with sample prompts.
   - Ensure responses stream back while the workflow remains paused.
2. **Regression**
   - Verify workflow switching still works (Generator ↔ Investor) before and after Ask Mozaiks is invoked.
   - Confirm Mongo documents capture Ask Mozaiks messages with proper role/source fields.
3. **Observability**
   - Check logs for structured `general_agent_call` entries.
   - Validate MozaiksPay token accounting hooks receive per-call usage data.

---

## Open Questions / Follow-Ups

1. **UX Placement** – Tab vs floating CTA vs omnipresent footer? Needs design input.
2. **Conversation Persistence** – Do we keep a single Ask Mozaiks chat per ws_id, or persist per app/user for long-term knowledge?
3. **Knowledge Pack Ownership** – Should generator outputs live in `workflows/<app>/metadata/ask_mozaiks.json`, or a shared `knowledge-packs/` directory keyed by app_id?
4. **Actionability** – Can Ask Mozaiks trigger workflow events (e.g., auto-start Investor) or is it strictly informational for MVP?
5. **Authentication** – When exposing this as an overlay for third-party apps, do we need additional scopes/keys beyond app_id & user_id?

---

## Next Steps

- Align with design on the UI control for "Ask Mozaiks".
- Decide whether to persist Ask Mozaiks messages inside the existing chat documents or a separate collection.
- Begin Phase 1 implementation by extending `SimpleTransport` and introducing the Ask Mozaiks service helper.
