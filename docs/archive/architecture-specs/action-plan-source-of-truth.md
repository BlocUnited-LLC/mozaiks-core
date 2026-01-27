# Action Plan Source of Truth

> **Status**: Canonical Reference  
> **Last Updated**: 2025-11-28  
> **Authoritative Schema**: `workflows/Generator/structured_outputs.json`

This document is the **single source of truth** for the Action Plan data model, agent outputs, and data flow in the Generator workflow. All prompt refactoring, schema changes, and documentation updates MUST reference this document.

---

## 1. Executive Summary

The Generator workflow produces workflow specifications through a pipeline of specialized agents. Each agent outputs a structured JSON payload that flows to downstream agents via **context variables** and **conversation history**.

### Key Principles

1. **Semantic Wrapper Keys**: Agents output data wrapped in PascalCase semantic keys (e.g., `WorkflowStrategy`, `TechnicalBlueprint`, `ModuleAgents`) to decouple agent names from data contracts.
2. **Module-Based Architecture**: Workflows are composed of `modules` (not "phases"). Each module has a pattern, agents, and tools.
3. **Separation of Concerns**: Strategy defines WHAT, Architect defines SHARED infrastructure, Implementation defines WHO does the work.

---

## 2. Agent Registry (Source of Truth)

From `structured_outputs.json` → `registry`:

| Agent | Structured Output Model | Semantic Wrapper Key |
|-------|------------------------|---------------------|
| InterviewAgent | `null` | N/A (conversational) |
| PatternAgent | `PatternSelectionOutput` | `PatternSelection` |
| WorkflowStrategyAgent | `WorkflowStrategyOutput` | `WorkflowStrategy` |
| WorkflowArchitectAgent | `TechnicalBlueprintOutput` | `TechnicalBlueprint` |
| WorkflowImplementationAgent | `ModuleAgentsOutput` | `ModuleAgents` |
| ProjectOverviewAgent | `MermaidSequenceDiagramOutput` | `MermaidSequenceDiagram` |
| ToolsManagerAgent | `ToolsManifestOutput` | `tools` + `lifecycle_tools` |
| UIFileGenerator | `UIToolsFilesOutput` | `tools` (CodeFile[]) |
| AgentToolsFileGenerator | `AgentToolsFilesOutput` | `tools` (CodeFile[]) |
| HookAgent | `HookFilesOutput` | `hook_files` |
| AgentsAgent | `RuntimeAgentsOutput` | `agents` |
| ContextVariablesAgent | `ContextVariablesPlanOutput` | `ContextVariablesPlan` |
| OrchestratorAgent | `OrchestrationConfigOutput` | (direct fields) |
| HandoffsAgent | `HandoffRulesOutput` | `handoff_rules` |
| StructuredOutputsAgent | `StructuredModelsOutput` | `models` + `registry` |
| DownloadAgent | `DownloadRequestOutput` | `agent_message` |

---

## 3. Action Plan Agents (Design Phase)

These agents produce the **design-time Action Plan** that users approve before file generation.

### 3.1 PatternAgent → `PatternSelectionOutput`

**Purpose**: Select the AG2 orchestration pattern for the workflow.

```json
{
  "PatternSelection": {
    "is_multi_workflow": false,
    "decomposition_reason": null,
    "pack_name": "Customer Support Router",
    "workflows": [
      {
        "name": "CustomerSupportRouter",
        "role": "primary",
        "description": "Routes support requests to the right specialists",
        "pattern_id": 1,
        "pattern_name": "Context-Aware Routing"
      }
    ]
  }
}
```

**Pattern ID Legend**:
- 1 = Context-Aware Routing
- 2 = Escalation
- 3 = Feedback Loop
- 4 = Hierarchical
- 5 = Organic
- 6 = Pipeline
- 7 = Redundant
- 8 = Star
- 9 = Triage with Tasks

---

### 3.2 WorkflowStrategyAgent → `WorkflowStrategyOutput`

**Purpose**: Define high-level workflow architecture (WHAT the workflow does).

**Context Variable Written**: `workflow_strategy`

```json
{
  "WorkflowStrategy": {
    "workflow_name": "Customer Support Router",
    "workflow_description": "When [TRIGGER], workflow [ACTIONS], resulting in [VALUE]",
    "human_in_loop": true,
    "pattern": ["ContextAwareRouting"],  // list of pattern names
    "trigger": "chat",                    // chat | form_submit | schedule | database_condition | webhook
    "initiated_by": "user",               // user | system | external_event
    "modules": [
      {
        "module_name": "Module 1: Request Classification",
        "module_index": 0,
        "module_description": "Analyze incoming requests and classify by domain",
        "pattern_id": 1,
        "pattern_name": "Context-Aware Routing",
        "agents_needed": ["RouterAgent", "TechSpecialist", "FinanceSpecialist"]
      }
    ]
  }
}
```

**WorkflowStrategyModule Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `module_name` | str | Format: "Module N: Purpose" |
| `module_index` | int | Zero-based (0 = Module 1) |
| `module_description` | str | What this module accomplishes |
| `pattern_id` | int | AG2 pattern ID (1-9) |
| `pattern_name` | str | Human-readable pattern name |
| `agents_needed` | str[] | Agent names (guidance only) |

---

### 3.3 WorkflowArchitectAgent → `TechnicalBlueprintOutput`

**Purpose**: Define workflow-wide infrastructure (context variables, UI components, lifecycle hooks).

**Context Variable Written**: `technical_blueprint`

**Reads From**: `workflow_strategy`

```json
{
  "TechnicalBlueprint": {
    "global_context_variables": [
      {
        "name": "current_domain",
        "type": "state",
        "purpose": "Tracks which domain specialist is handling the request",
        "trigger_hint": "Set by RouterAgent when routing decision is made"
      }
    ],
    "ui_components": [
      {
        "module_name": "Module 1: Request Classification",
        "agent": "RouterAgent",
        "tool": "request_clarification",
        "label": "Need more information",
        "component": "ClarificationRequest",
        "display": "inline",
        "summary": "Asks user for clarification when query is ambiguous"
      }
    ],
    "before_chat_lifecycle": {
      "name": "initialize_context",
      "purpose": "Load user profile and preferences",
      "trigger": "before_chat",
      "integration": null
    },
    "after_chat_lifecycle": null,
    "workflow_dependencies": {
      "required_workflows": []
    }
  }
}
```

**RequiredContextVariable Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `name` | str | Variable name (snake_case) |
| `type` | str | config \| data_reference \| data_entity \| computed \| state \| external |
| `purpose` | str | Why this variable exists |
| `trigger_hint` | str \| null | Natural language cue for triggers |

**WorkflowUIComponent Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `module_name` | str | Module where UI appears |
| `agent` | str | Agent that emits the tool |
| `tool` | str | Tool function name (snake_case) |
| `label` | str | User-facing CTA text |
| `component` | str | React component name (PascalCase) |
| `display` | str | inline \| artifact |
| `summary` | str | ≤200 char narrative |

---

### 3.4 WorkflowImplementationAgent → `ModuleAgentsOutput`

**Purpose**: Define WHO does the work in each module (agent specifications with tools).

**Reads From**: `workflow_strategy`, `technical_blueprint`

```json
{
  "ModuleAgents": [
    {
      "module_index": 0,
      "agents": [
        {
          "agent_name": "RouterAgent",
          "agent_type": "router",
          "objective": "Analyze user requests and route to appropriate specialists",
          "human_interaction": "context",
          "generation_mode": null,
          "max_consecutive_auto_reply": 20,
          "agent_tools": [
            {
              "name": "analyze_request",
              "integration": null,
              "purpose": "Parse user request to determine domain",
              "interaction_mode": "none"
            }
          ],
          "lifecycle_tools": [],
          "system_hooks": []
        }
      ]
    }
  ]
}
```

**WorkflowAgent Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `agent_name` | str | PascalCase identifier |
| `agent_type` | enum | router \| worker \| evaluator \| orchestrator \| intake \| generator |
| `objective` | str | Short description of responsibilities |
| `human_interaction` | enum | none \| context \| approval \| feedback \| single |
| `generation_mode` | str \| null | text \| image \| video \| audio \| null |
| `max_consecutive_auto_reply` | int | Turn limit based on autonomy |
| `agent_tools` | AgentTool[] | Tools this agent can call |
| `lifecycle_tools` | LifecycleTool[] | Lifecycle operations |
| `system_hooks` | SystemHook[] | Runtime behavior modifications |

**AgentTool Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `name` | str | Tool function name (snake_case) |
| `integration` | str \| null | Third-party service (PascalCase) or null |
| `purpose` | str | What the tool does (≤140 chars) |
| `interaction_mode` | enum | inline \| artifact \| none |

**SystemHook Fields**:
| Field | Type | Description |
|-------|------|-------------|
| `name` | str | Hook function name (snake_case). Examples: update_agent_state, process_message_before_send |
| `purpose` | str | Brief explanation of why the agent needs this hook (≤140 chars) |

---

### 3.5 ProjectOverviewAgent → `MermaidSequenceDiagramOutput`

**Purpose**: Generate visual diagram for user approval.

**Reads From**: `workflow_strategy`, `technical_blueprint`, `ModuleAgents`

```json
{
  "MermaidSequenceDiagram": {
    "workflow_name": "Customer Support Router",
    "mermaid_diagram": "sequenceDiagram\n    participant User\n    ...",
    "legend": ["M1: Request Classification"]
  },
  "agent_message": "Here's the workflow diagram. Review and approve to continue."
}
```

---

## 4. Data Flow Between Agents

### 4.1 Context Variables (Written)

| Context Variable | Written By | Read By |
|-----------------|------------|---------|
| `workflow_strategy` | WorkflowStrategyAgent | WorkflowArchitectAgent, WorkflowImplementationAgent, ProjectOverviewAgent |
| `technical_blueprint` | WorkflowArchitectAgent | WorkflowImplementationAgent, ProjectOverviewAgent |
| `action_plan` | WorkflowImplementationAgent | ProjectOverviewAgent |
| `action_plan_acceptance` | UI (mermaid_sequence_diagram) | Downstream file generators |

### 4.2 Semantic Key References

Downstream agents find upstream data by searching conversation history for semantic wrapper keys:

```
WorkflowStrategy    → workflow_strategy (context var) or message with "WorkflowStrategy" key
TechnicalBlueprint  → technical_blueprint (context var) or message with "TechnicalBlueprint" key
ModuleAgents        → message with "ModuleAgents" key (searched in conversation history)
```

**Critical Rule**: Prompts MUST reference semantic keys (e.g., "TechnicalBlueprint"), NOT agent names (e.g., "WorkflowArchitectAgent output").

---

## 5. Action Plan Composition

### 5.1 How the UI Shows "Action Plan"

The **Action Plan** displayed to users is composed from multiple agent outputs:

1. `WorkflowStrategy` - modules, trigger, pattern
2. `TechnicalBlueprint` - ui_components for display hints
3. `ModuleAgents` - agent details per module
4. `MermaidSequenceDiagram` - visual diagram

The React component `ActionPlan` in the UI receives these as separate payloads and composes them at render time. There is **no single merged "ActionPlan" data model**.

### 5.2 Legacy Models (DELETED)

The following models were removed from `structured_outputs.json` on 2025-11-28:

| Deleted Model | Reason |
|---------------|--------|
| `ActionPlan` | Never used by any agent in registry |
| `ActionPlanCall` | Legacy UI tool wrapper |
| `WorkflowSpec` | Replaced by `WorkflowStrategy` |
| `WorkflowModule` | Replaced by `WorkflowStrategyModule` |

> Note: `DesignActionPlan` and `MinimalActionPlan` were referenced in old docs but never existed in the schema.

---

## 6. Taxonomy Reference

### 6.1 Agent Types (`agent_type`)

| Value | Role | Typical Patterns |
|-------|------|-----------------|
| `intake` | User-facing entry, gathers context | Pipeline, Escalation |
| `router` | Control flow, determines routing | Context-Aware Routing |
| `worker` | Task execution | All patterns |
| `evaluator` | Quality gate, approval decisions | Feedback Loop, Redundant |
| `orchestrator` | Manages sub-teams | Hierarchical, Star |
| `generator` | Single-shot content generation | Any with media output |

### 6.2 Human Interaction Modes (`human_interaction`)

| Value | Behavior | Turn Limit | UI Required |
|-------|----------|------------|-------------|
| `none` | Fully autonomous | 30 | No |
| `context` | Conversational data collection | 20 | Optional |
| `approval` | Explicit sign-off required | 5 | Yes |
| `feedback` | Non-UI iterative refinement | 5 | No |
| `single` | One-shot invocation | 1 | No |

### 6.3 Trigger Types (`trigger`)

| Value | Mechanism | `human_in_loop` |
|-------|-----------|-----------------|
| `chat` | User initiates chat | true |
| `form_submit` | User submits form | false (post-submit) |
| `schedule` | Cron-based | false |
| `database_condition` | DB state change | false |
| `webhook` | External HTTP POST | false |

### 6.4 Initiated By (`initiated_by`)

| Value | Actor |
|-------|-------|
| `user` | Human explicitly starts |
| `system` | Platform automatically starts |
| `external_event` | Third-party triggers |

---

## 7. Prompt Refactoring Guidelines

When updating Generator agent prompts:

### 7.1 Input References

✅ **DO**: Reference semantic wrapper keys
```
"Read WorkflowStrategy from context variables"
"Extract ModuleAgents from conversation history"
```

❌ **DON'T**: Reference agent names
```
"Read WorkflowStrategyAgent output"
"Get data from WorkflowArchitectAgent"
```

### 7.2 Output Contracts

Each agent MUST output its exact structured output model:
- PatternAgent → `PatternSelectionOutput`
- WorkflowStrategyAgent → `WorkflowStrategyOutput`
- WorkflowArchitectAgent → `TechnicalBlueprintOutput`
- WorkflowImplementationAgent → `ModuleAgentsOutput`
- ProjectOverviewAgent → `MermaidSequenceDiagramOutput`

### 7.3 Field Naming

- Module names: "Module N: Purpose" (MUST include prefix)
- Agent names: PascalCase (e.g., `RouterAgent`)
- Tool names: snake_case (e.g., `analyze_request`)
- Context variables: snake_case (e.g., `current_domain`)

---

## 8. Obsolete Documentation

The following documents have been marked with deprecation notices (updated 2025-11-28):

| Document | Status | Issue |
|----------|--------|-------|
| `docs/workflows/ACTION_PLAN_OWNERSHIP.md` | ⚠️ Deprecated | Uses `DesignActionPlan`/`MinimalActionPlan` which don't exist |
| `docs/ACTION_PLAN_SCHEMA_V2.md` | ⚠️ Deprecated | Partially aligned but has field discrepancies |
| `docs/workflows/WORKFLOW_SEMANTIC_MODEL.md` | ⚠️ Partially Outdated | References `ActionPlanArchitect` agent |
| `docs/workflows/workflow_authoring.md` | ⚠️ Partially Outdated | References legacy agent names |
| `docs/workflows/THIRD_PARTY_API_FLOW.md` | ⚠️ Outdated | References old ActionPlan schema |
| `docs/workflows/DISPLAY_MODE_AUTO_RESOLUTION.md` | ⚠️ Partially Outdated | Examples use legacy agent names |
| `docs/workflows/CONTEXT_VARIABLES_STATELESS_STRATEGY.md` | ⚠️ Partially Outdated | Examples use legacy schemas |
| `TASK_DECOMPOSITION_ARCHITECTURE.md` | ❌ Removed | Consolidated into `docs/source_of_truth/ORCHESTRATION_AND_DECOMPOSITION.md` |

All marked documents now include notices pointing to this source of truth document.

---

## 9. Quick Reference: Agent Execution Order

```
1. InterviewAgent        → Gather requirements (conversational, no output)
2. PatternAgent          → Select pattern (PatternSelectionOutput)
3. WorkflowStrategyAgent → Define modules (WorkflowStrategyOutput)
4. WorkflowArchitectAgent → Define infrastructure (TechnicalBlueprintOutput)
5. WorkflowImplementationAgent → Define agents per module (ModuleAgentsOutput)
6. ProjectOverviewAgent  → Generate diagram + user approval (MermaidSequenceDiagramOutput)

--- USER APPROVAL GATE ---

7. ToolsManagerAgent     → Generate tools manifest
8. ContextVariablesAgent → Generate context variables plan
9. HandoffsAgent         → Generate handoff rules
10. AgentsAgent          → Generate runtime agent definitions
11. StructuredOutputsAgent → Generate structured output models
12. OrchestratorAgent    → Generate orchestration config
13. UIFileGenerator      → Generate UI tool files
14. AgentToolsFileGenerator → Generate backend tool files
15. HookAgent            → Generate lifecycle hook files
16. DownloadAgent        → Package and download
```

---

## 10. Verification Checklist

When making schema or prompt changes, verify:

- [ ] `structured_outputs.json` registry is updated
- [ ] Agent prompts reference correct semantic wrapper keys
- [ ] Field names match schema exactly (case-sensitive)
- [ ] `modules` terminology used (not "phases")
- [ ] Context variable names match between agents
- [ ] UI component tool names match agent tool names
- [ ] This document is updated if schema changes

---

## 11. Core Files & Data Flow

### Authoritative Files

| File | Purpose |
|------|---------|
| `workflows/Generator/structured_outputs.json` | Schema definitions (source of truth) |
| `workflows/Generator/agents.json` | Agent prompts |
| `workflows/Generator/tools/workflow_strategy.py` | Caches `workflow_strategy` context var |
| `workflows/Generator/tools/technical_blueprint.py` | Caches `technical_blueprint` context var |
| `workflows/Generator/tools/action_plan.py` | Merges strategy + agents + blueprint → UI |
| `ChatUI/src/workflows/Generator/components/ActionPlan.js` | Renders Action Plan UI |

### Data Flow

```
PatternAgent → PatternSelection → ctx: PatternSelection
WorkflowStrategyAgent → WorkflowStrategy → ctx: workflow_strategy
WorkflowArchitectAgent → TechnicalBlueprint → ctx: technical_blueprint
WorkflowImplementationAgent → ModuleAgents → action_plan.py (merge)
action_plan.py → merged payload → ActionPlan.js
```

---

## 12. Action Plan Variables for Downstream File Generation

The approved Action Plan provides the following context variables that downstream agents use to generate runtime files:

### From `workflow_strategy`:
| Variable | Type | Used By |
|----------|------|---------|
| `workflow_name` | str | All downstream agents (file naming, references) |
| `workflow_description` | str | OrchestratorAgent, AgentsAgent |
| `trigger` | literal | OrchestratorAgent (startup mode) |
| `initiated_by` | literal | OrchestratorAgent |
| `human_in_loop` | bool | OrchestratorAgent, AgentsAgent |
| `pattern` | list[str] | HandoffsAgent, OrchestratorAgent |
| `modules` | list[WorkflowStrategyModule] | All file generators |

### From `technical_blueprint`:
| Variable | Type | Used By |
|----------|------|---------|
| `global_context_variables` | list[RequiredContextVariable] | ContextVariablesAgent |
| `ui_components` | list[WorkflowUIComponent] | ToolsManagerAgent, UIFileGenerator |
| `before_chat_lifecycle` | WorkflowLifecycleToolRef | HookAgent |
| `after_chat_lifecycle` | WorkflowLifecycleToolRef | HookAgent |
| `workflow_dependencies` | WorkflowDependencies | OrchestratorAgent |

### From `ModuleAgents` (per module):
| Variable | Type | Used By |
|----------|------|---------|
| `module_index` | int | All file generators (ordering) |
| `agents` | list[WorkflowAgent] | AgentsAgent, HandoffsAgent, ToolsManagerAgent |

### Per `WorkflowAgent`:
| Variable | Type | Used By |
|----------|------|---------|
| `agent_name` | str | AgentsAgent, HandoffsAgent |
| `agent_type` | literal | AgentsAgent (role classification) |
| `objective` | str | AgentsAgent (system message) |
| `agent_tools` | list[AgentTool] | ToolsManagerAgent, AgentToolsFileGenerator |
| `lifecycle_tools` | list[LifecycleTool] | HookAgent, AgentToolsFileGenerator |
| `human_interaction` | literal | AgentsAgent (turn limits, UI behavior) |
| `generation_mode` | str\|null | AgentsAgent (generator agents only) |
| `max_consecutive_auto_reply` | int | AgentsAgent |

---

## 13. Deep Dive: Downstream Agent Data Flow

This section provides complete traceability from Action Plan variables → each downstream agent → runtime output files.

### 13.1 ToolsManagerAgent

**Purpose**: Consolidate all tools from ModuleAgents into a single manifest for runtime discovery.

**Structured Output**: `ToolsManifestOutput`

**Input Wrapper Keys Read**:
| Key | Source | Fields Used |
|-----|--------|-------------|
| `ActionPlan.workflow` | WorkflowStrategyOutput | modules[].agents_needed |
| `ModuleAgents` | ModuleAgentsOutput | agents[].agent_tools[], agents[].lifecycle_tools[] |
| `TechnicalBlueprint.ui_components` | TechnicalBlueprintOutput | component, display|
| `ContextVariablesPlan` | ContextVariablesPlanOutput | definitions[] (for state variable triggers) |

**Output Schema**:
```json
{
  "tools": [
    {
      "agent": "PascalCaseAgent",
      "file": "snake_case.py",
      "function": "snake_case",
      "description": "<=140 chars",
      "tool_type": "UI_Tool | Agent_Tool",
      "auto_invoke": true | false | null,
      "ui": {
        "component": "ReactComponent | null",
        "mode": "inline | artifact | null"
      }
    }
  ],
  "lifecycle_tools": [
    {
      "agent": "PascalCaseAgent | null",
      "file": "snake_case.py",
      "function": "snake_case",
      "description": "string",
      "tool_type": "Lifecycle_Tool",
      "auto_invoke": null,
      "ui": { "component": null, "mode": null },
      "trigger": "before_chat | after_chat | before_agent | after_agent",
      "integration": "PascalCase | null"
    }
  ]
}
```

**Data Transformation**:
- Iterates ModuleAgents[].agents[].agent_tools[] → extracts to ToolSpec
- Cross-references TechnicalBlueprint.ui_components for UI metadata enrichment
- Groups lifecycle_tools by trigger type
- Sets tool_type based on interaction_mode (none → Agent_Tool, inline/artifact → UI_Tool)

**Runtime File Written**: `tools.json`

---

### 13.2 ContextVariablesAgent

**Purpose**: Define all context variables with their source types, triggers, and agent exposure mappings.

**Structured Output**: `ContextVariablesPlanOutput`

**Input Wrapper Keys Read**:
| Key | Source | Fields Used |
|-----|--------|-------------|
| `ActionPlan.workflow` | WorkflowStrategyOutput | modules[], workflow_name |
| `ModuleAgents` | ModuleAgentsOutput | agents[].agent_tools[], agents[].objective |
| `Tools` | ToolsManifestOutput | tools[] for state variable ui_response triggers |
| `TechnicalBlueprint` | TechnicalBlueprintOutput | global_context_variables[] |

**Output Schema**:
```json
{
  "ContextVariablesPlan": {
    "definitions": {
      "variable_name": {
        "type": "str | int | list | dict | bool",
        "description": "Purpose of this variable",
        "source": {
          "type": "config | data_reference | data_entity | computed | state | external",
          // Type-specific fields (see 6-type taxonomy below)
        }
      }
    },
    "agents": {
      "AgentName": ["var1", "var2"]
    }
  }
}
```

**Six Context Variable Source Types**:

| Type | Required Fields | Purpose |
|------|----------------|---------|
| `config` | `env_var`, `default_value`, `required` | Environment configuration |
| `data_reference` | `database`, `collection`, `query_template`, `fields`, `refresh_strategy` | Read-only DB lookups |
| `data_entity` | `database`, `collection`, `search_by`, `schema`, `write_strategy` | Writable DB entities |
| `computed` | `computation`, `inputs`, `persist_to` | Derived values |
| `state` | `initial`, `transitions`, `trigger`, `persist` | Workflow state machines |
| `external` | `service`, `operation`, `url`, `auth`, `cache`, `retry` | External API calls |

**Data Transformation**:
- Expands TechnicalBlueprint.global_context_variables[] into full ContextVariableDefinitionEntry
- Maps each variable to agents that need access based on ModuleAgents objectives
- Sets trigger.type for state variables (tool_call, ui_response, handoff, lifecycle)

**Runtime File Written**: `context_variables.json`

---

### 13.3 AgentsAgent

**Purpose**: Generate runtime agent definitions with system prompts and tool bindings.

**Structured Output**: `RuntimeAgentsOutput`

**Input Wrapper Keys Read**:
| Key | Source | Fields Used |
|-----|--------|-------------|
| `WorkflowStrategy` | WorkflowStrategyOutput | workflow_name, modules[].module_name |
| `module_agents` | ModuleAgentsOutput | agents[] (all fields) |
| `tools` | ToolsManifestOutput | tools[] for binding |
| `ContextVariablesPlan` | ContextVariablesPlanOutput | agents{} mapping |
| `models + registry` | StructuredModelsOutput | For structured_output binding |

**Output Schema**:
```json
{
  "agents": [
    {
      "agent_name": "PascalCaseAgent",
      "prompt_sections": [
        {
          "id": "role",
          "heading": "[ROLE]",
          "content": "You are..."
        },
        {
          "id": "objective",
          "heading": "[OBJECTIVE]",
          "content": "..."
        },
        {
          "id": "context",
          "heading": "[CONTEXT]",
          "content": "..."
        },
        {
          "id": "instructions",
          "heading": "[INSTRUCTIONS]",
          "content": "..."
        }
      ],
      "max_consecutive_auto_reply": 20,
      "auto_tool_mode": true | false,
      "structured_outputs_required": true | false
    }
  ]
}
```

**Data Transformation**:
- Maps WorkflowAgent.objective → prompt_sections with id="objective"
- Maps agent_type → role section content
- Maps human_interaction → max_consecutive_auto_reply (none=30, context=20, approval/feedback=5, single=1)
- Binds tools from ToolsManifest based on agent ownership
- Sets auto_tool_mode based on agent_type (router=true, worker=depends)

**Runtime File Written**: `agents.json`

---

### 13.4 HandoffsAgent

**Purpose**: Generate routing rules for agent-to-agent transitions.

**Structured Output**: `HandoffRulesOutput`

**Input Wrapper Keys Read**:
| Key | Source | Fields Used |
|-----|--------|-------------|
| `ActionPlan.workflow` | WorkflowStrategyOutput | pattern[], modules[] |
| `ModuleAgents` | ModuleAgentsOutput | agents[].agent_name, agent_type |
| `Tools` | ToolsManifestOutput | tools[] for condition references |
| `ContextVariablesPlan` | ContextVariablesPlanOutput | triggers (state variable conditions) |

**Output Schema**:
```json
{
  "handoff_rules": [
    {
      "source_agent": "SourceAgent",
      "target_agent": "TargetAgent",
      "condition": "natural language or expression",
      "condition_scope": "pre | post",
      "priority": 1
    }
  ]
}
```

**Data Transformation**:
- Derives routing from pattern semantics (Context-Aware Routing → router→specialists)
- Uses ContextVariablesPlan state triggers as conditions
- Sets condition_scope based on trigger type:
  - `pre`: Evaluate before agent response (ui_response triggers)
  - `post`: Evaluate after agent response (tool_call, lifecycle triggers)
- Orders by priority for pattern-appropriate fallbacks

**Runtime File Written**: `handoffs.json`

---

### 13.5 OrchestratorAgent

**Purpose**: Generate the orchestration configuration for runtime AG2 GroupChat.

**Structured Output**: `OrchestrationConfigOutput`

**Input Wrapper Keys Read**:
| Key | Source | Fields Used |
|-----|--------|-------------|
| `ActionPlan.workflow` | WorkflowStrategyOutput | workflow_name, trigger, initiated_by, human_in_loop |
| `ModuleAgents` | ModuleAgentsOutput | agents[].agent_name |
| `Tools` | ToolsManifestOutput | lifecycle_tools[] for hooks |
| `Handoffs` | HandoffRulesOutput | For visual_agents derivation |

**Output Schema**:
```json
{
  "workflow_name": "CustomerSupportRouter",
  "max_turns": 30,
  "human_in_the_loop": true,
  "startup_mode": "AgentDriven | UserDriven | BackendOnly",
  "orchestration_pattern": "DefaultPattern",
  "initial_message_to_user": "string | null",
  "initial_message": "string | null",
  "initial_agent": "FirstAgentName",
  "visual_agents": ["Agent1", "Agent2"],
  "runtime_extensions": [
    {"kind": "api_router", "entrypoint": "workflows.CustomerSupportRouter.tools.api:get_router"},
    {"kind": "startup_service", "entrypoint": "workflows.CustomerSupportRouter.tools.services:OutboxProcessor"}
  ]
}
```

**Data Transformation**:
- Maps trigger → startup_mode:
  - `chat` + `initiated_by=user` → UserDriven
  - `chat` + `initiated_by=system` → AgentDriven
  - Others (schedule, webhook, etc.) → BackendOnly
- Derives initial_agent from first agent in module[0]
- Sets visual_agents to all user-facing agents (human_interaction != none)
- Defaults runtime_extensions to []
- Adds runtime_extensions only when the workflow requires runtime-hosted capabilities:
  - `api_router`: inbound HTTP/WebSocket endpoints (webhooks, callbacks, OAuth redirects)
  - `startup_service`: background services started/stopped with the runtime lifecycle

**Runtime File Written**: `orchestrator.yaml`

---

### 13.6 StructuredOutputsAgent

**Purpose**: Generate data models that agents use for structured responses.

**Structured Output**: `StructuredModelsOutput`

**Input Wrapper Keys Read**:
| Key | Source | Fields Used |
|-----|--------|-------------|
| `WorkflowStrategy` | WorkflowStrategyOutput | modules[].module_description |
| `ModuleAgents` | ModuleAgentsOutput | agents[].agent_tools[] (for tool outputs) |
| `ContextVariablesPlan` | ContextVariablesPlanOutput | definitions[] (for data_entity schemas) |

**Output Schema**:
```json
{
  "models": [
    {
      "name": "ModelName",
      "type": "model",
      "fields": {
        "field_name": {
          "type": "str | int | list | dict | bool",
          "description": "Field purpose"
        }
      }
    }
  ],
  "registry": [
    {
      "agent": "AgentName",
      "model": "ModelName"
    }
  ]
}
```

**Data Transformation**:
- Creates models from data_entity schemas in ContextVariablesPlan
- Creates output models for agents that need structured responses
- Binds agents to models in registry

**Runtime File Written**: `structured_outputs.json`

---

### 13.7 UIFileGenerator

**Purpose**: Generate React components and Python async tool stubs for UI_Tools.

**Structured Output**: `UIToolsFilesOutput`

**Input Wrapper Keys Read**:
| Key | Source | Fields Used |
|-----|--------|-------------|
| `Tools` | ToolsManifestOutput | tools[] where tool_type="UI_Tool" |
| `TechnicalBlueprint.ui_components` | TechnicalBlueprintOutput | display, summary |
| `ActionPlan/ModuleAgents` | Advisory | Module naming, agent attribution |
| `ContextVariablesPlan` | ContextVariablesPlanOutput | state variables with ui_response triggers |

**Output Schema**:
```json
{
  "tools": [
    {
      "filename": "tools/ComponentName.jsx",
      "content": "// Full React component code",
      "installRequirements": ["package1", "package2"]
    },
    {
      "filename": "tools/tool_name.py",
      "content": "# Full Python async function code",
      "installRequirements": []
    }
  ]
}
```

**Data Transformation**:
- Filters tools[] for UI_Tool entries
- Generates React component matching ui.component name
- Generates Python stub with `use_ui_tool()` for frontend transport
- Includes response_key in onResponse for state variable ui_response triggers

**Runtime Files Written**: `tools/*.jsx`, `tools/*.py`

---

### 13.8 AgentToolsFileGenerator

**Purpose**: Generate Python tool implementations for Agent_Tools.

**Structured Output**: `AgentToolsFilesOutput`

**Input Wrapper Keys Read**:
| Key | Source | Fields Used |
|-----|--------|-------------|
| `Tools` | ToolsManifestOutput | tools[] where tool_type="Agent_Tool" |
| `ModuleAgents` | ModuleAgentsOutput | agents[].agent_name (ownership) |
| `TechnicalBlueprint` | TechnicalBlueprintOutput | Integration hints |
| `ContextVariablesPlan` | ContextVariablesPlanOutput | source types for access patterns |

**Output Schema**:
```json
{
  "tools": [
    {
      "filename": "tools/tool_name.py",
      "content": "# Full async Python function with imports",
      "installRequirements": ["httpx", "etc"]
    }
  ]
}
```

**Six-Type Context Variable Access Patterns in Generated Code**:

| Type | Code Pattern |
|------|-------------|
| `config` | `os.getenv("VAR", default)` |
| `data_reference` | `db[collection].find_one(query_template)` |
| `data_entity` | `db[collection].update_one(filter, update, upsert=True)` |
| `computed` | `result = computation_function(*inputs)` |
| `state` | `get_context_variable(context, "var")` / `set_context_variable()` |
| `external` | `async with httpx.AsyncClient()` |

**Runtime Files Written**: `tools/*.py`

---

### 13.9 HookAgent

**Purpose**: Generate lifecycle hook files for workflow-level events.

**Structured Output**: `HookFilesOutput`

**Input Wrapper Keys Read**:
| Key | Source | Fields Used |
|-----|--------|-------------|
| `WorkflowStrategy` | WorkflowStrategyOutput | workflow_name |
| `TechnicalBlueprint` | TechnicalBlueprintOutput | before_chat_lifecycle, after_chat_lifecycle |
| `ModuleAgents` | ModuleAgentsOutput | lifecycle_tools[] per agent |
| `ContextVariablesPlan` | ContextVariablesPlanOutput | triggers for lifecycle events |

**Output Schema**:
```json
{
  "hook_files": [
    {
      "filename": "hooks/before_chat.py",
      "content": "# Python hook implementation",
      "trigger": "before_chat",
      "integration": "PascalCase | null"
    }
  ]
}
```

**Data Transformation**:
- Creates hook files from TechnicalBlueprint lifecycle definitions
- Includes agent-level lifecycle_tools from ModuleAgents
- Sets trigger and integration from source LifecycleTool specs

**Runtime Files Written**: `hooks/*.py`

---

### 13.10 DownloadAgent

**Purpose**: Package all generated files for download.

**Structured Output**: `DownloadRequestOutput`

**Input Wrapper Keys Read**:
| Key | Source | Fields Used |
|-----|--------|-------------|
| All prior outputs | All generators | Complete file set |

**Output Schema**:
```json
{
  "agent_message": "Your workflow bundle is ready for download!"
}
```

**Action**: Triggers UI download mechanism for the packaged workflow.

---

## 14. Complete Variable → Agent → File Traceability Matrix

| Action Plan Variable | Downstream Agents | Runtime Files |
|---------------------|-------------------|---------------|
| `workflow_name` | All | All config files |
| `modules[].module_name` | AgentsAgent, OrchestratorAgent | agents.json, orchestrator.json |
| `modules[].pattern_id` | HandoffsAgent | handoffs.json |
| `agents[].agent_name` | AgentsAgent, HandoffsAgent, ToolsManagerAgent | agents.json, handoffs.json, tools.json |
| `agents[].agent_type` | AgentsAgent | agents.json (role section) |
| `agents[].objective` | AgentsAgent | agents.json (prompt_sections) |
| `agents[].agent_tools[]` | ToolsManagerAgent, AgentToolsFileGenerator | tools.json, tools/*.py |
| `agents[].lifecycle_tools[]` | HookAgent | hooks/*.py |
| `agents[].human_interaction` | AgentsAgent, HandoffsAgent | agents.json, handoffs.json |
| `global_context_variables[]` | ContextVariablesAgent | context_variables.json |
| `ui_components[]` | ToolsManagerAgent, UIFileGenerator | tools.json, tools/*.jsx |
| `before_chat_lifecycle` | HookAgent | hooks/before_chat.py |
| `after_chat_lifecycle` | HookAgent | hooks/after_chat.py |
| `trigger` | OrchestratorAgent | orchestrator.json (startup_mode) |
| `initiated_by` | OrchestratorAgent | orchestrator.json |
| `human_in_loop` | OrchestratorAgent, AgentsAgent | orchestrator.json, agents.json |

---

## 15. Runtime File Set (Complete)

After all generators execute, the runtime expects these files:

```
workflows/<WorkflowName>/
├── agents.json              # Agent definitions with prompts
├── context_variables.json   # Variable definitions and exposure
├── handoffs.json            # Routing rules
├── hooks.json               # Lifecycle hook registry (optional)
├── orchestrator.json        # GroupChat configuration
├── structured_outputs.json  # Data models
├── tools.json               # Tool registry
├── tools/
│   ├── *.py                 # Backend tool implementations
│   └── *.jsx                # UI tool components (if any)
└── hooks/
    └── *.py                 # Lifecycle hook implementations (if any)
```

This complete file set enables the MozaiksAI runtime to:
1. Load and configure AG2 agents with proper prompts
2. Register tools on agents
3. Apply context variables to runtime context
4. Enforce handoff rules for agent transitions
5. Execute lifecycle hooks at workflow boundaries
6. Render UI components for interactive tools

---

## 16. Real-Time Automation at Scale

This section explains how the Action Plan → Runtime pipeline enables production automation.

### 16.1 From Design to Execution

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         DESIGN TIME (Generator)                         │
├─────────────────────────────────────────────────────────────────────────┤
│  User Prompt → Interview → Pattern → Strategy → Architect → Impl        │
│                                          ↓                              │
│                              ACTION PLAN (approval gate)                │
│                                          ↓                              │
│  ToolsManager → ContextVars → Handoffs → Agents → Orchestrator → ...   │
│                                          ↓                              │
│                               RUNTIME FILES (.json + .py/.jsx)          │
└─────────────────────────────────────────────────────────────────────────┘
                                          ↓
┌─────────────────────────────────────────────────────────────────────────┐
│                        RUNTIME (MozaiksAI Core)                         │
├─────────────────────────────────────────────────────────────────────────┤
│  1. Workflow Loader discovers workflows/<name>/ directory               │
│  2. AG2 agents instantiated from agents.json with prompts               │
│  3. Tools imported and registered from tools.json + tools/*.py          │
│  4. Context variables initialized from context_variables.json           │
│  5. Handoff rules loaded for routing decisions                          │
│  6. GroupChat configured from orchestrator.json                         │
│  7. Lifecycle hooks attached at workflow boundaries                     │
│                                          ↓                              │
│                          READY TO ACCEPT TRIGGERS                       │
└─────────────────────────────────────────────────────────────────────────┘
```

### 16.2 Multi-Tenant Execution

Each workflow execution is isolated by:

| Dimension | Mechanism |
|-----------|-----------|
| `app_id` | Tenant isolation (database, token accounting) |
| `user_id` | User-specific context and permissions |
| `chat_id` | Session state isolation |
| `cache_seed` | AG2 conversation cache separation |

### 16.3 Token Accounting Integration

The runtime tracks token usage at execution time:

```python
# Token flow per turn
User Message → Agent Processing → LLM Call → Token Counter → MozaiksPay
                                                    ↓
                              { app_id, user_id, workflow, tokens_used }
```

This enables:
- Per-workflow cost analytics
- App billing rollups
- Usage-based throttling

### 16.4 Trigger Types → Runtime Behavior

| Trigger | Runtime Entry Point | Human Loop |
|---------|---------------------|------------|
| `chat` | WebSocket message → GroupChat.run() | Yes |
| `form_submit` | HTTP POST → workflow.execute() | No |
| `schedule` | Cron job → workflow.execute() | No |
| `database_condition` | Change stream → workflow.execute() | No |
| `webhook` | HTTP endpoint → workflow.execute() | No |

### 16.5 Scalability Primitives

| Primitive | Implementation |
|-----------|---------------|
| **Horizontal scaling** | Stateless FastAPI workers behind load balancer |
| **State persistence** | MongoDB for session state, context variables |
| **Event streaming** | WebSocket transport with correlation IDs |
| **Async I/O** | All tool calls, DB operations non-blocking |
| **Hot reload** | Workflow files discoverable without restart |

### 16.6 Key Runtime Files Explained

| File | Scale Impact |
|------|-------------|
| `orchestrator.json` | Controls max_turns, startup mode, visual agents |
| `context_variables.json` | Pre-loads config, external service connections |
| `handoffs.json` | Determines routing efficiency (fewer hops = faster) |
| `tools.json` | Registry for dynamic tool discovery |
| `agents.json` | Prompts cached per agent instance |

### 16.7 Critical Data Contracts for Automation

For real-time automation to work, these contracts MUST hold:

1. **Tool Registration**: Every tool in `tools.json` MUST have a corresponding implementation in `tools/*.py`

2. **Agent-Tool Binding**: `agents.json` agent names MUST match `tools.json` agent ownership fields

3. **Handoff Completeness**: All agent transitions MUST have corresponding handoff rules (or default pattern fallback)

4. **Context Variable Exposure**: Every variable an agent needs MUST appear in its `agents{}` mapping in `context_variables.json`

5. **State Machine Validity**: State variables MUST have valid `transitions[]` (no orphan states)

6. **Lifecycle Hook Binding**: Hooks in `hooks.json` MUST have corresponding `hooks/*.py` implementations

### 16.8 Observability for Scale

The runtime provides:

| Metric | Source |
|--------|--------|
| Agent turn counts | orchestrator.json max_turns |
| Tool invocation latency | Runtime instrumentation |
| Handoff frequency | Handoff rule hit tracking |
| Token usage per agent | MozaiksPay integration |
| Error rates by tool | Exception tracking |

---

## 17. Summary: Action Plan as the Contract

The **Action Plan** is the approval gate between design and execution. It contains:

1. **WorkflowStrategy**: What the workflow accomplishes (modules, pattern, trigger)
2. **TechnicalBlueprint**: Shared infrastructure (context variables, UI components, lifecycle hooks)
3. **ModuleAgents**: Who does the work (agents, tools, interactions)

Once approved, downstream agents transform this into **runtime-ready files**:

- **ToolsManagerAgent** → `tools.json`
- **ContextVariablesAgent** → `context_variables.json`
- **HandoffsAgent** → `handoffs.json`
- **AgentsAgent** → `agents.json`
- **OrchestratorAgent** → `orchestrator.json`
- **StructuredOutputsAgent** → `structured_outputs.json`
- **UIFileGenerator** → `tools/*.jsx`
- **AgentToolsFileGenerator** → `tools/*.py`
- **HookAgent** → `hooks/*.py`

These files form a **complete, self-contained workflow package** that the MozaiksAI runtime can load and execute at scale, with:

- Multi-tenant isolation
- Token accounting
- Event streaming
- Horizontal scaling
- Hot-swappable workflows
