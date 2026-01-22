# MozaiksAI Runtime — AI Agent Codebase Audit (8 Parts)

**Date:** 2025-12-25

This document is intentionally **self-contained**: it does **not** assume you can browse the repo. It embeds the critical contracts, execution flow, and representative excerpts so you can understand the runtime end-to-end from scratch.

Scope notes:
- This is the **MozaiksAI Runtime** repo (runtime/data-plane): FastAPI + WebSocket transport + AG2 (AutoGen) execution + Mongo persistence.
- “MozaiksCore” (control-plane) concepts exist in docs/specs here, but platform services (auth/billing/plugin-host/etc.) are not implemented in this repo.

---

## 1) Agent Architecture (how agents are defined, what the contract is)

### 1.1 Declarative workflow folder contract
A workflow is declared as a folder under `workflows/<WorkflowName>/`.

Canonical manifests (runtime loads these):
- `orchestrator.json` — run-level settings (startup mode, max turns, initial agent)
- `agents.json` — agent definitions, prompts, and behavior flags (auto-tool mode, turn limits)
- `handoffs.json` — agent-to-agent routing rules (conditions, after-work transitions)
- `context_variables.json` — env/db/file/state-driven variables and UI-hidden triggers
- `structured_outputs.json` — structured output models and registry mapping agent → model
- `tools.json` — tool registry (UI tools + backend tools), including auto-invoke flags
- `hooks.json` (optional) — runtime hooks registered onto agents

### 1.2 Concrete workflow present in this repo
The most complete concrete generator workflow currently present is:
- `workflows/AgentGenerator/…`

It defines a generator pipeline where:
- `InterviewAgent` does intake via natural language and emits the control token `NEXT`.
- `PatternAgent` chooses orchestration pattern + whether this becomes a multi-workflow pack.
- Downstream agents create strategy, state, UX, roster, tools, and finally a download bundle.

### 1.3 Example: “Prompt sections” structure (agents.json)
Agent prompts are composed from multiple sections (role/objective/context/instructions/output-format) rather than a single monolithic string.

Representative excerpt (simplified):

```json
{
  "name": "InterviewAgent",
  "prompt_sections": [
    {"heading": "[ROLE]", "content": "You are an expert conversational intake specialist..."},
    {"heading": "[OBJECTIVE]", "content": "Guide a light conversation..."},
    {"heading": "[INSTRUCTIONS]", "content": "..."},
    {"heading": "[OUTPUT FORMAT]", "content": "Turn output is either a question or EXACTLY NEXT"}
  ],
  "auto_tool_mode": false,
  "structured_outputs_required": true
}
```

### 1.4 Example: state trigger from agent text (context_variables.json)
The runtime supports “derived variables” that flip based on agent text. That’s how `InterviewAgent` emits `NEXT` without showing it to the user.

Representative excerpt:

```json
{
  "name": "interview_complete",
  "type": "boolean",
  "source": {
    "type": "state",
    "default": false,
    "triggers": [
      {
        "type": "agent_text",
        "agent": "InterviewAgent",
        "ui_hidden": true,
        "match": {"equals": "NEXT"}
      }
    ]
  }
}
```

### 1.5 Example: handoffs chain (handoffs.json)
Handoffs are declarative routing rules (some are conditional expressions).

Representative excerpt:

```json
{
  "handoff_rules": [
    {
      "source_agent": "InterviewAgent",
      "target_agent": "PatternAgent",
      "handoff_type": "condition",
      "condition_type": "expression",
      "condition": "${interview_complete} == True"
    },
    {
      "source_agent": "PatternAgent",
      "target_agent": "WorkflowStrategyAgent",
      "handoff_type": "after_work"
    }
  ]
}
```

---

## 2) Orchestration & Execution (how workflows run in AG2, turn-by-turn)

### 2.1 Workflow discovery and config loading
The runtime discovers workflows by scanning the `workflows/` directory.

Key behavior:
- A folder is considered a workflow if it has `orchestrator.json` **or** has `tools.json`/`tools/`.
- Config is built by merging modular JSON files.

Representative excerpt (simplified):

```python
# UnifiedWorkflowManager.discover_workflows()
for item in workflows_base_path.iterdir():
    if (item / "orchestrator.json").exists():
        workflows.append(item.name)
    elif (item / "tools.json").exists() or (item / "tools").exists():
        workflows.append(item.name)

# _load_modular_workflow_config()
config.update(load_json('orchestrator.json'))
config['agents'] = load_json('agents.json')
config['handoffs'] = load_json('handoffs.json')
config['context_variables'] = load_json('context_variables.json')
config['structured_outputs'] = load_json('structured_outputs.json')
config['tools'] = load_json('tools.json').get('tools', [])
config.update(load_json('ui_config.json'))
```

### 2.2 Dynamic handler resolution
Workflows can be registered with custom handlers, but by default the runtime creates a dynamic handler that calls the orchestration engine.

Representative excerpt:

```python
# get_workflow_handler(workflow_name)
if workflow_name.lower() not in handlers:
    async def dynamic_handler(app_id, chat_id, user_id=None, initial_message=None, **kwargs):
        return await run_workflow_orchestration(
            workflow_name=workflow_name,
            app_id=app_id,
            chat_id=chat_id,
            user_id=user_id,
            initial_message=initial_message,
            **kwargs,
        )
```

### 2.3 Agent construction: ConversableAgent factory
Agents are instantiated as AG2 `ConversableAgent` objects. The factory enforces:
- base LLM config resolution
- tool binding for the workflow
- structured output model presence when `auto_tool_mode=true`

Representative excerpt (simplified):

```python
workflow_config = workflow_manager.get_config(workflow_name)
agent_configs = workflow_config.get('agents', {})

# Load tool callables from workflows/<flow>/tools.json
agent_tool_functions = load_agent_tool_functions(workflow_name)

# Load structured output registry for this workflow
structured_registry = get_structured_outputs_for_workflow(workflow_name)

for agent_name, agent_config in agent_configs.items():
    auto_tool_mode = bool(agent_config.get('auto_tool_mode'))
    structured_model_cls = structured_registry.get(agent_name)
    if auto_tool_mode and structured_model_cls is None:
        raise ValueError("auto_tool_mode enabled but no structured model registered")

    # Compose prompt from prompt_sections and construct ConversableAgent
```

### 2.4 Context enforcement: app_id/chat_id/workflow_name in AG2 context
Before the pattern runs, the orchestration layer ensures critical routing keys exist in AG2 context:

Representative excerpt:

```python
if not ag2_context.get('workflow_name'):
    ag2_context.set('workflow_name', workflow_name)
if not ag2_context.get('app_id'):
    ag2_context.set('app_id', app_id)
if not ag2_context.get('chat_id'):
    ag2_context.set('chat_id', chat_id)
if user_id and not ag2_context.get('user_id'):
    ag2_context.set('user_id', user_id)
```

### 2.5 Streaming loop: persist TextEvents + forward to UI
During execution, AG2 emits events. Text events are:
- persisted to Mongo
- optionally fed into derived-context triggers
- forwarded to UI via WebSocket
- optionally intercepted for auto-tool conversion

Representative excerpt (simplified):

```python
async for ev in response.events:
    if isinstance(ev, TextEvent):
        await persistence_manager.save_event(ev, chat_id, app_id)
        if derived_context_manager:
            derived_context_manager.handle_event(ev)

        transport = await SimpleTransport.get_instance()
        await transport.send_event_to_ui({...}, chat_id)

        if sender_name in auto_tool_agents:
            structured_blob = extract_json(message_content)
            # convert to UI tool call / structured-output-ready
```

---

## 3) Platform Integration & Ownership Boundaries (runtime vs control-plane)

### 3.1 What this repo (runtime) owns
- Workflow discovery + loading from declarative JSON
- AG2 agent construction and orchestration
- Tool invocation boundary (UI tools + backend tools)
- WebSocket event transport to/from ChatUI
- Mongo persistence of chat transcripts + workflow metadata
- Multi-tenant scoping enforcement at the DB boundary (by app_id)

### 3.2 What this repo does NOT implement
(These are typically MozaiksCore / platform services responsibilities.)
- Authentication / identity / org membership
- Subscription and billing enforcement (other than basic token hooks inside runtime)
- Plugin host/module loading system for product-native UI
- GitHub repo creation / commit / push

### 3.3 AppGenerator is currently a spec (not runtime code)
There is a detailed spec describing product-native UI + plugin output, but it is not implemented as a concrete workflow folder in this repo at the same maturity as AgentGenerator.

---

## 4) Backend Communication: WebSocket, UI events, and input correlation

### 4.1 WebSocket transport is centralized
The runtime uses a singleton transport (`SimpleTransport`) to:
- track connections keyed by `chat_id`
- buffer messages pre-connection
- correlate user input requests (AG2 → UI → API → AG2 continuation)
- manage background tasks for child workflows

Representative excerpt (simplified):

```python
class SimpleTransport:
    self.connections: Dict[str, Dict[str, Any]] = {}
    self._pre_connection_buffers: Dict[str, List[Dict[str, Any]]] = {}
    self._input_request_registries: Dict[str, Dict[str, Any]] = {}
    self._background_tasks: Dict[str, asyncio.Task] = {}

async def submit_user_input(input_request_id: str, user_input: str) -> bool:
    # find respond_cb stored by orchestration, invoke it, then emit input_ack
```

### 4.2 UI Tools: “auto-tool mode” contract
A UI tool is declared in `tools.json` with:
- `tool_type: "UI_Tool"`
- `auto_invoke: true`
- a UI block: `{component, mode}`

Example entries:

```json
{
  "agent": "ProjectOverviewAgent",
  "file": "mermaid_sequence_diagram.py",
  "function": "mermaid_sequence_diagram",
  "tool_type": "UI_Tool",
  "auto_invoke": true,
  "ui": {"component": "ActionPlan", "mode": "artifact"}
}
```

### 4.3 How auto-invoke is executed
There’s a runtime handler that resolves:
- which Pydantic model validates the structured data (from structured output registry)
- which tool function to call (from tool loader)
- idempotency keys to avoid double execution

Representative excerpt (simplified):

```python
# AutoToolEventHandler.handle_structured_output_ready(event)
if not event.get('auto_tool_mode'):
    return
binding = resolve_binding(workflow_name, model_name, agent_name)
validated = binding.model_cls.model_validate(structured_data)
kwargs = build_tool_kwargs(binding, validated, context, turn_key)
emit_tool_call(...)
result_payload, status = invoke_tool(binding, kwargs)
emit_tool_result(...)
```

---

## 5) Persistence & Multi-Tenancy (Mongo, resume, and app isolation)

### 5.1 Canonical scope key: app_id
The tenancy helper creates Mongo filters that enforce app isolation.

Representative excerpt:

```python
def build_app_scope_filter(app_id: str) -> Dict[str, Any]:
    normalized = normalize_app_id(app_id)
    if not normalized:
        return {'app_id': '__invalid__'}
    return {'app_id': normalized}
```

### 5.2 ChatSessions is the core transcript store
The persistence layer stores:
- one document per chat workflow
- embedded messages for replay/resume
- additional workflow metadata and state

Core claims (as documented in code comments):
- Replay/resume relies on `ChatSessions.messages`.
- Per-event normalized rows were intentionally reduced to avoid collection noise.

### 5.3 Resume behavior
Resume reads the chat doc for `chat_id + app_id` and returns messages only if status is IN_PROGRESS.

Representative excerpt (simplified):

```python
async def resume_chat(chat_id: str, app_id: Optional[str] = None) -> Optional[List[Dict[str, Any]]]:
    doc = await coll.find_one({'_id': chat_id, **build_app_scope_filter(app_id)})
    if not doc:
        return None
    if doc.get('status') != IN_PROGRESS:
        return None
    return doc.get('messages', [])
```

### 5.4 Event persistence
During streaming, TextEvents are saved via `save_event(ev, chat_id, app_id)` and appended to the transcript.

---

## 6) Packs & Multi-Workflow Orchestration (child workflows + journeys)

This repo has two complementary multi-workflow mechanisms:

### 6.1 Nested child workflows (pack coordinator)
If a parent workflow emits a structured output indicating `is_multi_workflow=true`, the coordinator can:
- pause the parent
- spawn child chats (new `chat_id`s)
- run children in background tasks
- resume the parent when children complete

Representative excerpt (simplified):

```python
# WorkflowPackCoordinator.handle_structured_output_ready(event)
pack_cfg = load_pack_graph(parent_workflow)
plan = extract_pack_plan(structured_data)

await transport.pause_background_workflow(parent_chat_id)
for child in spawnable:
    new_chat_id = f"chat_{child_workflow_name}_{uuid4().hex[:8]}"
    await pm.create_chat_session(chat_id=new_chat_id, app_id=app_id, workflow_name=child_workflow_name, user_id=user_id)
    transport._background_tasks[new_chat_id] = asyncio.create_task(
        transport._run_workflow_background(chat_id=new_chat_id, workflow_name=child_workflow_name, ...)
    )

transport.send_event_to_ui({"type": "chat.workflow_batch_started", ...}, parent_chat_id)
```

### 6.2 Journeys (wizard chains) from workflows/_pack/workflow_graph.json
A journey defines an ordered chain of workflows (e.g., ValueEngine → AgentGenerator → AppGenerator) with gating.

Representative excerpt (from the pack config file):

```json
{
  "journeys": [
    {
      "id": "build",
      "auto_attach_on_start": true,
      "auto_advance": true,
      "steps": ["ValueEngine", "AgentGenerator", "AppGenerator"]
    }
  ]
}
```

When a workflow run completes, JourneyOrchestrator:
- checks if auto-advance applies
- enforces prerequisite gates for the next step
- creates a new chat session for the next workflow (or reuses an existing one)
- aliases the websocket connection so the user experiences a continuous “wizard”

---

## 7) Code Generation & GitHub Ops (what exists here, what’s missing)

### 7.1 Generation that exists here: workflow bundle creation for download
AgentGenerator includes an implemented tool `generate_and_download` that:
- gathers latest agent JSON outputs from persisted chat messages
- converts outputs into workflow files
- packages them (zip)
- optionally injects bundled attachments

Representative excerpt (simplified):

```python
pm = AG2PersistenceManager()
collected = await pm.gather_latest_agent_jsons(chat_id=chat_id, app_id=app_id)

# create_workflow_files(...) converts gathered JSON into workflow folder files
files = create_workflow_files(payload)

# zip + present download UI
```

### 7.2 GitHub operations are not implemented in runtime code
This repo does not include a GitHub client or automation to:
- create repos
- commit/push
- open PRs

Practical integration recommendation (runtime-safe):
- Add an Agent_Tool called something like `export_to_github`.
- Implement it as a stub that calls your control-plane service (MozaiksCore / a microservice) which performs GitHub ops.
- Keep runtime stateless: runtime provides the bundle (zip path/bytes) and metadata (app_id/user_id), control-plane does the irreversible write.

---

## 8) End-to-End Example + Customization Flow (concrete, minimal, runtime-safe)

### 8.1 “User prompt → running workflow → UI tool → download bundle”
1) User starts a chat session for workflow `AgentGenerator`.
2) Runtime loads workflow config from JSON manifests.
3) Runtime instantiates AG2 agents.
4) AG2 emits events; runtime persists and forwards to UI.
5) When `ProjectOverviewAgent` emits a structured output, runtime auto-invokes UI tool `mermaid_sequence_diagram`.
6) When `DownloadAgent` emits the download request, runtime auto-invokes `generate_and_download` and presents a download UI.

### 8.2 Minimal customization: add a new UI tool without changing runtime
Goal: add an “Approve Plan” UI tool that sets a context variable.

Steps:
1) Add a `UI_Tool` entry to `workflows/AgentGenerator/tools.json`:

```json
{
  "agent": "ProjectOverviewAgent",
  "file": "approve_plan.py",
  "function": "approve_plan",
  "description": "Collect user approval for the plan.",
  "tool_type": "UI_Tool",
  "auto_invoke": true,
  "ui": {"component": "PlanApproval", "mode": "inline"}
}
```

2) Implement the tool function in `workflows/AgentGenerator/tools/approve_plan.py`:

```python
from typing import Any, Dict, Optional, Annotated

async def approve_plan(
    PlanApproval: Annotated[Dict[str, Any], "UI response payload"],
    agent_message: Annotated[str, "Short message"],
    context_variables: Annotated[Optional[Any], "AG2 ContextVariables"] = None,
) -> Dict[str, Any]:
    approved = bool((PlanApproval or {}).get("approved"))
    if context_variables and hasattr(context_variables, "set"):
        context_variables.set("plan_approved", approved)
    return {"status": "ok", "approved": approved}
```

3) Add a `context_variables.json` trigger (if you want it derived from UI response):

```json
{
  "name": "plan_approved",
  "type": "boolean",
  "source": {
    "type": "state",
    "default": false,
    "triggers": [
      {"type": "ui_response", "tool": "approve_plan", "response_key": "approved"}
    ]
  }
}
```

4) Ensure the owning agent is `auto_tool_mode=true` and has a structured output model (if the agent emits structured JSON that auto-triggers tools). If this tool is invoked by runtime directly on `structured_output_ready`, it must be part of the tool registry and the model registry must validate the structured payload.

Outcome:
- No runtime code changes.
- Workflow remains hot-swappable.
- UI tool wiring stays declarative.

---

## Appendix: Key runtime invariants (short)
- All DB reads/writes that touch chat sessions must include app scope filtering.
- Tool invocation is the only intended boundary where agent logic causes side effects.
- Packs/journeys must never bypass app_id/user_id scoping or leak state across chats.
