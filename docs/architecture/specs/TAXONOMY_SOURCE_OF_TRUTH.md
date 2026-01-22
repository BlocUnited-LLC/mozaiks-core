# MozaiksAI Taxonomy Source of Truth

> **Last Updated**: 2025-11-27  
> **Status**: Canonical Reference  
> **Purpose**: Single source of truth for ALL enums, literals, and taxonomies used across the platform.

---

## Executive Summary

This document consolidates **every taxonomy** used in MozaiksAI. When making changes to prompts, schemas, or code, reference this document to ensure consistency.

**Authoritative File**: `workflows/Generator/structured_outputs.json` is the schema source of truth. This document explains and organizes those definitions for human consumption.

---

## 1. Agent Taxonomies

### 1.1 Agent Type (`agent_type`)

**Purpose**: Classifies the architectural role of an agent within a workflow.

| Value | Role | Behavior | Typical Patterns |
|-------|------|----------|------------------|
| `intake` | Entry point | Greets user, gathers context, asks clarifying questions | Pipeline (first), Escalation (triage), Triage with Tasks |
| `router` | Control flow | Determines "where next?", classifies, dispatches | Context-Aware Routing |
| `worker` | Task execution | Uses tools, generates content, processes data | All patterns (default) |
| `evaluator` | Quality gate | Reviews output, approves/rejects, provides feedback | Feedback Loop, Redundant |
| `orchestrator` | Team coordination | Manages sub-teams, delegates tasks, synthesizes results | Hierarchical, Star, Redundant |
| `generator` | Content creation | Single-shot content generation (text/image/video/audio) | Any pattern with media output |

**Schema Location**: `structured_outputs.json` → `WorkflowAgent.agent_type`

**Derivation**: Set by `WorkflowImplementationAgent` based on pattern + module purpose.

---

### 1.2 Human Interaction Mode (`human_interaction`)

**Purpose**: Controls how an agent involves humans during execution (Layer 3 of the Three-Layer Model).

| Value | Behavior | UI Impact | Turn Limit |
|-------|----------|-----------|------------|
| `none` | Fully autonomous | No user interaction | 30 |
| `context` | Conversational data collection | Collects info, continues | 20 |
| `approval` | Explicit sign-off required | Pause for approve/reject | 5 |
| `feedback` | Non-UI iterative refinement | Internal review loops | 5 |
| `single` | One-shot invocation | Single interaction, done | 1 |

**Schema Location**: `structured_outputs.json` → `WorkflowAgent.human_interaction`

**Derivation**: Derived from `ui_pattern` (Layer 2):
- `single_step` → `context`
- `two_step_confirmation` → `approval`
- `multi_step` → `approval`
- No UI component → `none`

---

### 1.3 Generation Mode (`generation_mode`)

**Purpose**: Specifies output modality for generator-type agents.

| Value | Output Type |
|-------|-------------|
| `text` | Text/markdown content |
| `image` | Image generation (DALL-E, etc.) |
| `video` | Video generation |
| `audio` | Audio generation |
| `null` | Not a generator agent |

**Schema Location**: `structured_outputs.json` → `WorkflowAgent.generation_mode`

**Constraint**: Only used when `agent_type = "generator"`, else null.

---

## 2. Workflow Taxonomies

### 2.1 Trigger Type (`trigger`)

**Purpose**: Specifies how a workflow is initiated.

| Value | Mechanism | Example |
|-------|-----------|---------|
| `chat` | User sends chat message | Conversational chatbot |
| `form_submit` | User submits web form | Contact form, onboarding form |
| `schedule` | Time-based cron trigger | Daily report, weekly sync |
| `database_condition` | DB state change | New order, status update |
| `webhook` | External HTTP POST | Stripe payment, Slack event |

**Schema Location**: `structured_outputs.json` → `WorkflowStrategy.trigger`

**Impact on `human_in_loop`**:
- `chat` → `human_in_loop = true`
- `form_submit` → `human_in_loop = false` (post-submit automation)
- `schedule`, `database_condition`, `webhook` → `human_in_loop = false`

---

### 2.2 Initiated By (`initiated_by`)

**Purpose**: Identifies WHO/WHAT starts the workflow.

| Value | Actor | Examples |
|-------|-------|----------|
| `user` | Human explicitly starts | Chat message, form submit, button click |
| `system` | Platform automatically starts | Cron schedule, database condition |
| `external_event` | Third-party service triggers | Webhook from Stripe, Slack, GitHub |

**Schema Location**: `structured_outputs.json` → `WorkflowStrategy.initiated_by`

---

### 2.3 Orchestration Pattern (`pattern`)

**Purpose**: The 9 AG2 orchestration patterns defining agent topology.

| ID | Name | Core Value |
|----|------|------------|
| 1 | Context-Aware Routing | Different expert handlers based on content |
| 2 | Escalation | Try simple first, escalate when needed |
| 3 | Feedback Loop | ONE artifact iteratively refined |
| 4 | Hierarchical | Managers coordinating specialist workers |
| 5 | Organic | Exploration-first, no fixed sequence |
| 6 | Pipeline | Linear step-by-step process |
| 7 | Redundant | Multiple viewpoints for quality |
| 8 | Star | Central coordinator + independent specialists |
| 9 | Triage with Tasks | Many tasks with strict sequencing/dependencies |

**Schema Location**: `structured_outputs.json` → `PatternSelection.workflows[].pattern_id` (int 1-9)

---

### 2.4 Startup Mode (`startup_mode`)

**Purpose**: How the runtime initializes the workflow.

| Value | Behavior | Use When |
|-------|----------|----------|
| `AgentDriven` | Agent speaks first | `trigger = chat` |
| `UserDriven` | User action precedes agents | `trigger = form_submit` |
| `BackendOnly` | Fully automated, no UI | `trigger = schedule/webhook/database_condition` |

**Schema Location**: `structured_outputs.json` → `OrchestrationConfigOutput.startup_mode`

---

## 3. UI Taxonomies

### 3.1 Display Mode (`display`)

**Purpose**: WHERE a UI component renders.

| Value | Rendering Location |
|-------|-------------------|
| `inline` | Embedded in chat flow |
| `artifact` | Side panel/tray (separate from chat) |

**Schema Location**: `structured_outputs.json` → `WorkflowUIComponent.display`

---

### 3.2 UI Pattern (`ui_pattern`)

**Purpose**: DEPTH of user interaction (Layer 2).

| Value | Interaction Style | Downstream `human_interaction` |
|-------|-------------------|-------------------------------|
| `single_step` | User provides data once, agent continues | `context` |
| `two_step_confirmation` | User reviews, then approves/rejects | `approval` |
| `multi_step` | Progressive wizard, iterative refinement | `approval` |

**Schema Location**: `structured_outputs.json` → `WorkflowUIComponent.ui_pattern`

---

### 3.3 Interaction Mode (`interaction_mode`) - Tool Level

**Purpose**: UI presentation mode for a specific tool.

| Value | Behavior |
|-------|----------|
| `inline` | Chat-embedded UI tool |
| `artifact` | Side panel/tray UI tool |
| `none` | Backend-only (no UI) |

**Schema Location**: `structured_outputs.json` → `AgentTool.interaction_mode`

---

## 4. Tool Taxonomies

### 4.1 Tool Type (`tool_type`)

**Purpose**: Classifies tool behavior and invocation pattern.

| Value | Behavior | Auto-Invoke |
|-------|----------|-------------|
| `UI_Tool` | Renders UI component | `true` (default) |
| `Agent_Tool` | Backend execution | `false` (default) |
| `Lifecycle_Tool` | Runs at lifecycle boundaries | N/A |

**Schema Location**: `structured_outputs.json` → `ToolSpec.tool_type`

---

### 4.2 Lifecycle Trigger (`trigger`) - Lifecycle Tools

**Purpose**: When a lifecycle operation executes.

| Value | Timing |
|-------|--------|
| `before_chat` | Before first agent message |
| `after_chat` | After workflow completes |
| `before_agent` | Before specific agent speaks |
| `after_agent` | After specific agent completes |

**Schema Location**: `structured_outputs.json` → `LifecycleTool.trigger`, `LifecycleOperation.trigger`

---

## 5. Context Variable Taxonomies

### 5.1 Source Type (`source.type`)

**Purpose**: The six-type taxonomy for context variable origins.

| Type | Purpose | Key Fields |
|------|---------|------------|
| `config` | Deployment configuration | `env_var`, `default`, `required` |
| `data_reference` | Read existing MongoDB data | `collection`, `query_template`, `fields`, `refresh_strategy` |
| `data_entity` | Create new MongoDB data | `collection`, `schema`, `indexes`, `write_strategy`, `search_by` |
| `computed` | Business logic outputs | `computation`, `inputs`, `output_type`, `persist_to` |
| `state` | Workflow orchestration state | `default`, `transitions`, `persist`, `triggers` |
| `external` | Third-party API data | `service`, `operation`, `params`, `auth`, `cache`, `retry` |

**Schema Location**: `structured_outputs.json` → `ContextVariableSource.type`

---

### 5.2 Trigger Type - Context Variable (`trigger.type`)

**Purpose**: How a state variable gets updated.

| Value | Mechanism | Fields |
|-------|-----------|--------|
| `agent_text` | Passive detection of agent output | `agent`, `match` (equals/contains/regex) |
| `ui_response` | Active UI tool response | `tool`, `response_key` |

**Schema Location**: `structured_outputs.json` → `DerivedTrigger.type`

---

### 5.3 Refresh Strategy (`refresh_strategy`) - Data Reference

**Purpose**: When to refresh cached data.

| Value | Behavior |
|-------|----------|
| `once` | Cache indefinitely |
| `per_module` | Refresh each module transition |
| `on_demand` | Refresh when explicitly requested |

**Schema Location**: `structured_outputs.json` → `ContextVariableSource.refresh_strategy`

---

### 5.4 Write Strategy (`write_strategy`) - Data Entity

**Purpose**: When to persist data entities.

| Value | Behavior |
|-------|----------|
| `immediate` | Persist on creation |
| `on_module_transition` | Persist at module boundary |
| `on_workflow_end` | Persist when workflow completes |

**Schema Location**: `structured_outputs.json` → `ContextVariableSource.write_strategy`

---

## 6. Handoff Taxonomies

### 6.1 Handoff Type (`handoff_type`)

**Purpose**: Type of agent-to-agent transition.

| Value | Behavior |
|-------|----------|
| `condition` | Immediate evaluation (context or LLM-based) |
| `after_work` | Post-agent-completion evaluation |

**Schema Location**: `structured_outputs.json` → `HandoffRule.handoff_type`

---

### 6.2 Condition Type (`condition_type`)

**Purpose**: How handoff conditions are evaluated.

| Value | Evaluation Method |
|-------|-------------------|
| `expression` | Context variable expressions (`${var}`) |
| `string_llm` | LLM natural language evaluation |
| `null` | Auto-detect (`${...}` → expression, else LLM) |

**Schema Location**: `structured_outputs.json` → `HandoffRule.condition_type`

---

### 6.3 Condition Scope (`condition_scope`)

**Purpose**: Timing for condition evaluation.

| Value | Timing |
|-------|--------|
| `pre` | Pre-reply evaluation (re-checks every turn) |
| `null` | Default (Post-Reply) |

**Schema Location**: `structured_outputs.json` → `HandoffRule.condition_scope`

---

### 6.4 Transition Target (`transition_target`)

**Purpose**: Destination of a handoff.

| Value | Behavior |
|-------|----------|
| `AgentTarget` | Hand off to another agent |
| `RevertToUserTarget` | Return control to user |
| `TerminateTarget` | End the workflow |

**Schema Location**: `structured_outputs.json` → `HandoffRule.transition_target`

---

## 8. Three-Layer Interaction Model

This is not a taxonomy per se, but a critical conceptual framework that ties taxonomies together.

| Layer | Owner | Field | Purpose |
|-------|-------|-------|---------|
| 1 - Strategic Intent | WorkflowStrategyAgent | `human_in_loop` (global bool) | Does workflow involve ANY human? |
| 2 - UI Surface Contracts | WorkflowArchitectAgent | `ui_components[].ui_pattern` | WHAT UI surfaces exist, WHERE, HOW |
| 3 - Agent Execution Mode | WorkflowImplementationAgent | `agent.human_interaction` | HOW does this agent involve humans? |

**Critical Clarification - human_in_loop vs ui_components**:

`human_in_loop = true` means the workflow involves human participation, but this does NOT always require `ui_components`:

| Scenario | human_in_loop | ui_components Required? |
|----------|---------------|------------------------|
| Conversational chatbot (plain text chat) | `true` | **NO** - Runtime provides chat UI |
| Multi-agent LLM backbone (chat-based) | `true` | **NO** - Runtime provides chat UI |
| Approval workflow with review gate | `true` | **YES** - Need ApprovalCard component |
| Form-based data collection | `true` | **YES** - Need InputForm component |
| Rich content review (artifacts) | `true` | **YES** - Need artifact display |
| Fully automated background job | `false` | **NO** - No human interaction |

**The Runtime Provides**:
- Chat interface (WebSocket transport, message rendering)
- Conversation history display
- Basic text input/output

**ui_components Are Needed For**:
- Structured data collection beyond plain text (forms, selectors)
- Approval/rejection gates with explicit buttons
- Rich content display (artifacts, dashboards, side panels)
- Multi-step wizards or progressive forms

**Flow**:
```
human_in_loop=true + needs structured interaction → Architect creates ui_components
human_in_loop=true + plain chat suffices → No ui_components needed (runtime handles it)
human_in_loop=false → No ui_components (fully automated)
```

---

## 9. Pattern-to-Agent-Type Mapping

Reference mapping for which agent types typically appear in each pattern.

| Pattern | Primary Agent Types |
|---------|---------------------|
| 1. Context-Aware Routing | `router` + `worker` (specialists) |
| 2. Escalation | `intake` + `worker` (tiered) |
| 3. Feedback Loop | `intake` + `worker` + `evaluator` |
| 4. Hierarchical | `orchestrator` (executive/managers) + `worker` (specialists) |
| 5. Organic | `worker` (all) - GroupChatManager handles routing |
| 6. Pipeline | `intake` + `worker` (stage agents) |
| 7. Redundant | `orchestrator` + `worker` (parallel) + `evaluator` |
| 8. Star | `orchestrator` (hub) + `worker` (spokes) |
| 9. Triage with Tasks | `intake` + `orchestrator` + `worker` |

---

## 10. Validation Rules

When creating or updating agents/schemas, validate:

1. **Agent Type Consistency**: `agent_type` must be one of the 6 allowed values
2. **Human Interaction Derivation**: `human_interaction` must be derived from `ui_pattern`
3. **Trigger-Initiated Consistency**: `trigger` and `initiated_by` must be compatible
4. **Startup Mode Alignment**: `startup_mode` must match `trigger`
5. **UI Pattern Flow**: `ui_pattern` → `human_interaction` → `max_consecutive_auto_reply`
6. **Context Variable Type Fields**: Each `source.type` requires specific sibling fields

---

## Appendix A: Change Log

| Date | Change | Author |
|------|--------|--------|
| 2025-11-27 | Initial creation - consolidated all taxonomies | System |

---

## Appendix B: Files Referencing These Taxonomies

| Taxonomy | Files |
|----------|-------|
| `agent_type` | `structured_outputs.json`, `agents.json` (all agents), `AGENT_ROLE_TAXONOMY.md` |
| `human_interaction` | `structured_outputs.json`, `agents.json`, `ACTION_PLAN_SCHEMA_V2.md` |
| `trigger` | `structured_outputs.json`, `agents.json` (WorkflowStrategyAgent) |
| `ui_pattern` | `structured_outputs.json`, `agents.json` (WorkflowArchitectAgent) |
| `source.type` | `structured_outputs.json`, `agents.json` (ContextVariablesAgent) |
| `tool_type` | `structured_outputs.json`, `agents.json` (ToolsManagerAgent) |

---

## 11. Deprecated/Removed Concepts

### 11.1 `agents_needed` as Enum (REMOVED)

> ⚠️ **HISTORICAL NOTE**: Early prompts incorrectly documented `agents_needed` as an enum with values `"single" | "sequential" | "nested"`. This has been removed.

**Correct Usage**: `agents_needed` is `list[str]` - an array of agent names that hints at which agents a module needs. The pattern guidance itself defines the coordination topology (how many agents, their relationships).

**Why Removed**: The enum was redundant with pattern guidance. Each pattern already defines:
- How many agents are typical for each module
- Agent roles (coordinator, specialists, etc.)
- Coordination topology (hub-spoke, linear, etc.)

**Migration**: Remove all references to `"single" | "sequential" | "nested"` from prompts. Instead, prompts should say: "Refer to pattern guidance for agent count and coordination topology."

---

**END OF DOCUMENT**
