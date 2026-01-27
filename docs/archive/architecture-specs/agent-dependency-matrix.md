# Agent Dependency Matrix & Derivation Source of Truth

**Status**: Active
**Last Updated**: December 8, 2025
**Purpose**: Defines the canonical data flow, upstream dependencies, and field-level derivation logic for every agent in the AgentGenerator workflow. This document is the governing specification for Agent Prompts.

**Schema for Derivation Logic**:
Each output variable uses the following structure:
- *Semantic Upstream Reference*: The exact upstream field(s) this value depends on.
- *Rule/Taxonomy*: The mapping rule, lookup, or transformation applied.
- *How to Obtain (Pattern-Aware)*: Step-by-step procedural instructions for deriving this value, including pattern-specific variations.

---

## 1. InterviewAgent
**Role**: Conversational Intake
**Output**: Conversation Transcript (Unstructured)

### Upstream Dependencies
- **`concept_overview`** (from `ContextVariables`)
  - *Purpose*: Seed the initial question with project context.
- **`monetization_enabled`** (from `ContextVariables`)
  - *Purpose*: Tailor questions towards business vs personal use.
- **`context_aware`** (from `ContextVariables`)
  - *Purpose*: Determine if "Mission Control" framing is used.

### Derivation Logic
- **`Questions`**
  - *Semantic Upstream Reference*: `concept_overview`
  - *Rule/Taxonomy*: If present, ask for confirmation/expansion. If missing, ask for vision.
  - *How to Obtain (Pattern-Aware)*:
    1. Check if `concept_overview` is non-empty.
    2. If YES: Generate a question asking the user to confirm or expand on the provided concept.
    3. If NO: Generate an open-ended question asking the user to describe their vision.
    4. Append probing questions based on `monetization_enabled` (business model) and `context_aware` (platform framing).

---

## 2. PatternAgent
**Role**: Orchestration Pattern Selection
**Output Model**: `PatternSelectionOutput`
**Wrapper Key**: `PatternSelection`

### Upstream Dependencies
- **User phrases** (from `InterviewTranscript`)
  - *Purpose*: Identify structural signals ("step-by-step", "review").
- **`monetization_enabled`** (from `ContextVariables`)
  - *Purpose*: Bias towards robust patterns (Pipeline, Feedback Loop).
- **`concept_overview`** (from `ContextVariables`)
  - *Purpose*: Detect complexity signals.

### Derivation Logic
- **`workflows[].pattern_id`**
  - *Semantic Upstream Reference*: `InterviewTranscript` (User phrases, structural signals)
  - *Rule/Taxonomy*: Map to 1-9 Pattern ID. See `pattern_guidance.md` for the Pattern Selection Matrix.
  - *How to Obtain (Pattern-Aware)*:
    1. For each workflow you declare in `PatternSelection.workflows[]`, scan `InterviewTranscript` for structural keywords:
       - "step-by-step", "stages", "sequential" → **Pipeline (6)**
       - "route", "classify", "different types" → **Context-Aware Routing (1)**
       - "review", "refine", "iterate" → **Feedback Loop (3)**
       - "escalate", "tiers", "levels" → **Escalation (2)**
       - "delegate", "managers", "hierarchy" → **Hierarchical (4)**
       - "collaborate", "flexible", "organic" → **Organic (5)**
       - "parallel", "compare", "evaluate" → **Redundant (7)**
       - "hub", "coordinator", "central" → **Star (8)**
       - "triage", "tasks", "decompose" → **Triage with Tasks (9)**
    2. If `monetization_enabled` = true, bias towards patterns with more structure (6, 3, 4).
    3. If ambiguous, default to **Pipeline (6)** for simplicity.
    4. Emit the integer Pattern ID (1-9).
- **`workflows[].pattern_name`**
  - *Semantic Upstream Reference*: `workflows[].pattern_id`
  - *Rule/Taxonomy*: Use the exact display name for the chosen ID (see `pattern_guidance.md`).

---

## 3. WorkflowStrategyAgent (Layer 1)
**Role**: Strategic Blueprinting
**Output Model**: `WorkflowStrategyOutput`
**Wrapper Key**: `WorkflowStrategy`

### Upstream Dependencies
- **`workflows[current_workflow_index].pattern_name`** (from `PatternSelection`)
  - *Purpose*: Defines topology and module structure.
- **User goals, startup signals** (from `InterviewTranscript`)
  - *Purpose*: Defines workflow name, description, and startup_mode.

### Derivation Logic
- **`workflow_name`**
  - *Semantic Upstream Reference*: `InterviewTranscript` (User goals)
  - *Rule/Taxonomy*: Title Case summary of automation goal (2-4 words).
  - *How to Obtain (Pattern-Aware)*:
    1. Extract the primary automation goal from `InterviewTranscript`.
    2. Summarize into 2-4 words using Title Case.
- **`workflow_description`**
  - *Semantic Upstream Reference*: `InterviewTranscript`, `ContextVariables.concept_overview`
  - *Rule/Taxonomy*: Template: "When [TRIGGER], workflow [ACTIONS], resulting in [VALUE]."
  - *How to Obtain (Pattern-Aware)*:
    1. Identify the TRIGGER (what starts the workflow).
    2. Identify the ACTIONS (what the workflow does).
    3. Identify the VALUE (outcome for the user).
    4. Compose using the template.
- **`startup_mode`**
  - *Semantic Upstream Reference*: `InterviewTranscript`, `ContextVariables.concept_overview`
  - *Rule/Taxonomy*: Detect signals → `UserDriven`, `AgentDriven`, or `BackendOnly`.
  - *How to Obtain (Pattern-Aware)*:
    1. Scan for phrases: "chatbot", "users ask", "on-demand" → `UserDriven`.
    2. Scan for phrases: "wizard", "bot greets", "guided" → `AgentDriven`.
    3. Scan for phrases: "scheduled", "webhook", "automated", "cron" → `BackendOnly`.
    4. Default to `UserDriven` if ambiguous.
    5. **Pattern Variation**: Organic (5) typically uses `UserDriven`; Pipeline (6) can be any.
- **`human_in_loop`**
  - *Semantic Upstream Reference*: `startup_mode`
  - *Rule/Taxonomy*: User/AgentDriven → `true`; BackendOnly → `false`.
  - *How to Obtain (Pattern-Aware)*:
    1. If `startup_mode` is `UserDriven` or `AgentDriven`, set `true`.
    2. If `startup_mode` is `BackendOnly`, set `false`.
    3. **Pattern Variation**: Feedback Loop (3) almost always has `human_in_loop=true` for review gates.
- **`pattern`**
  - *Semantic Upstream Reference*: `PatternSelection.workflows[current_workflow_index].pattern_name`
  - *Rule/Taxonomy*: Start array with the selected workflow's `pattern_name`; append supplementary patterns if requirements demand.
  - *How to Obtain (Pattern-Aware)*:
    1. Initialize array with `[pattern_name]` from `PatternSelection.workflows[current_workflow_index]`.
    2. If requirements suggest a secondary pattern, append it.
    3. Multi-module workflows may have one pattern per module.
- **`modules`**
  - *Semantic Upstream Reference*: `PatternSelection` (Guidance), `InterviewTranscript`
  - *Rule/Taxonomy*: Map pattern steps to modules. `module_index` 0-based. Include `agents_needed` guidance.
  - *How to Obtain (Pattern-Aware)*:
    1. Load the pattern's typical agent roles from `pattern_guidance.md`.
    2. Group roles into logical modules based on workflow phases.
    3. Assign `module_index` starting at 0.
    4. Populate `agents_needed` with the list of agent roles required for this module (e.g., ["Researcher", "Writer"]).
    5. **Pattern Variation**:
       - **Pipeline (6)**: One module per stage (Validation → Inventory → Payment → ...).
       - **Feedback Loop (3)**: Modules for Drafting, Review, Revision, Finalization.
       - **Hierarchical (4)**: Module for Executive, modules for each Manager/Specialist group.
       - **Triage with Tasks (9)**: Triage module, then sequential task execution modules.

---

## 4. StateArchitectAgent (Layer 2a)
**Role**: State & Data Architecture
**Output Model**: `StateArchitectureOutput`
**Wrapper Key**: `StateArchitecture`

### Upstream Dependencies
- **`modules`** (from `WorkflowStrategy`)
  - *Purpose*: Scoping state needs per module.
- **`startup_mode`** (from `WorkflowStrategy`)
  - *Purpose*: Determining lifecycle hooks.
- **`workflows[current_workflow_index].pattern_name`** (from `PatternSelection`)
  - *Purpose*: Identifying pattern-specific context variables (e.g., Pipeline needs stage flags, Feedback Loop needs iteration tracking).

### Derivation Logic
- **`global_context_variables`**
  - *Semantic Upstream Reference*: `WorkflowStrategy.modules`, `PatternSelection`
  - *Rule/Taxonomy*: Extract state/config needs. Map to 6-type taxonomy (state, index, needed_flag, completed_flag, content, collection).
  - *How to Obtain (Pattern-Aware)*:
    1. Load the pattern's typical context variables from `pattern_guidance.md`.
    2. For each module, identify state tracking needs (`*_completed`, `*_started`).
    3. Identify content variables (`document_draft`, `final_report`).
    4. Identify collection variables (`feedback_collection`, `evaluation_scores`).
    5. **Pattern Variation**:
       - **Pipeline (6)**: `pipeline_started`, `pipeline_completed`, `validation_completed`, `inventory_completed`, etc.
       - **Feedback Loop (3)**: `current_stage`, `current_iteration`, `max_iterations`, `iteration_needed`, `document_draft`.
       - **Escalation (2)**: `*_agent_confidence`, `escalation_count`, `last_escalation_reason`.
       - **Redundant (7)**: `agent_a_result`, `agent_b_result`, `evaluation_scores`, `selected_approach`.
- **`lifecycle_requirements.before_chat`**
  - *Semantic Upstream Reference*: `WorkflowStrategy.startup_mode`
  - *Rule/Taxonomy*: BackendOnly → Init hook required; user-facing modes → optional.
  - *How to Obtain (Pattern-Aware)*:
    1. If `startup_mode` is `BackendOnly`, create a `before_chat` hook for initialization.
    2. Otherwise, only add if pre-loading external context is needed.
- **`lifecycle_requirements.after_chat`**
  - *Semantic Upstream Reference*: `WorkflowStrategy.modules` (Final module)
  - *Rule/Taxonomy*: Persistence/Reporting needs → Cleanup hook.
  - *How to Obtain (Pattern-Aware)*:
    1. If the final module requires persistence or reporting, create an `after_chat` hook.
    2. Otherwise, optional.
- **`workflow_dependencies`**
  - *Semantic Upstream Reference*: `InterviewTranscript` (Requirements)
  - *Rule/Taxonomy*: Identify if this workflow depends on artifacts from another workflow.
  - *How to Obtain (Pattern-Aware)*:
    1. Scan requirements for references to outputs from other workflows.
    2. If found, list the dependency workflow name.

---

## 5. UXArchitectAgent (Layer 2b)
**Role**: User Experience Architecture
**Output Model**: `UXArchitectureOutput`
**Wrapper Key**: `UXArchitecture`

### Upstream Dependencies
- **`modules`, `human_in_loop`** (from `WorkflowStrategy`)
  - *Purpose*: Scoping UI needs per module.
- **`workflows[current_workflow_index].pattern_name`** (from `PatternSelection`)
  - *Purpose*: Pattern-specific UI guidance (e.g., Feedback Loop needs review UI, Context-Aware Routing has minimal UI).

### Derivation Logic
- **`ui_requirements`**
  - *Semantic Upstream Reference*: `WorkflowStrategy.human_in_loop`, `WorkflowStrategy.modules`
  - *Rule/Taxonomy*: If `human_in_loop`=true, create UI contracts for interaction points.
  - *How to Obtain (Pattern-Aware)*:
    1. If `human_in_loop` is false, set `ui_requirements` to empty array.
    2. For each module with user interaction, define a UI requirement.
    3. Assign semantic labels based on module purpose.
    4. **Pattern Variation**:
       - **Feedback Loop (3)**: UI for review feedback submission.
       - **Escalation (2)**: UI for showing escalation status.
       - **Context-Aware Routing (1)**: Minimal UI; routing is automatic.
- **`ui_requirements.display`**
  - *Semantic Upstream Reference*: Interaction Complexity
  - *Rule/Taxonomy*: Simple → `"inline"`; Complex/Form → `"artifact"`.
  - *How to Obtain (Pattern-Aware)*:
    1. Assess the interaction: single button/toggle → `"inline"`.
    2. Multi-field form, rich content → `"artifact"`.
---

## 6. AgentRosterAgent (Layer 3a)
**Role**: Agent Roster Definition
**Output Model**: `AgentRosterOutput`
**Wrapper Key**: `AgentRoster`

### Upstream Dependencies
- **`modules`** (from `WorkflowStrategy`)
  - *Purpose*: One agent roster per module.
- **`ui_requirements`** (from `UXArchitecture`)
  - *Purpose*: Determines `human_interaction` mode.
- **`workflows[current_workflow_index].pattern_name`** (from `PatternSelection`)
  - *Purpose*: Pattern-specific agent naming conventions and role assignments.

### Derivation Logic
- **`agents[].agent_name`**
  - *Semantic Upstream Reference*: `WorkflowStrategy.modules`
  - *Rule/Taxonomy*: Unique PascalCase identifier.
  - *How to Obtain (Pattern-Aware)*:
    1. Use the module role as a base to construct a PascalCase agent name.
    2. Ensure uniqueness across all modules.
    3. **Pattern Variation**: Agent naming conventions vary by pattern—refer to `pattern_guidance.md` for pattern-specific agent roles.
- **`agents[].agent_type`**
  - *Semantic Upstream Reference*: `WorkflowStrategy.modules` (Role)
  - *Rule/Taxonomy*: Descriptive role string (e.g., "Research Specialist", "Code Reviewer"). Not a strict enum.
  - *How to Obtain (Pattern-Aware)*:
    1. Match the agent's purpose to a descriptive role:
       - Routes requests → `Router`
       - Performs domain work → `Worker` / `Specialist`
       - Evaluates/scores outputs → `Evaluator` / `Reviewer`
       - Coordinates other agents → `Orchestrator`
       - Gathers input → `Intake Specialist`
       - Produces content → `AgentGenerator`
    2. **Pattern Variation**:
       - **Context-Aware Routing (1)**: First agent is typically a `Router`.
       - **Redundant (7)**: Final agent is an `Evaluator`.
       - **Hierarchical (4)**: Managers are `Orchestrator`; Specialists are `Worker`.
- **`agents[].objective`**
  - *Semantic Upstream Reference*: `WorkflowStrategy.modules` (Role)
  - *Rule/Taxonomy*: Short human-readable description.
  - *How to Obtain (Pattern-Aware)*:
    1. Summarize the agent's responsibility in one sentence.
- **`agents[].human_interaction`**
  - *Semantic Upstream Reference*: `UXArchitecture.ui_requirements`
  - *Rule/Taxonomy*: Map UI pattern to interaction mode.
- **`agents[].max_consecutive_auto_reply`**
  - *Semantic Upstream Reference*: `human_interaction`
  - *Rule/Taxonomy*: `none`→30; `context`→20; `approval`/`feedback`→5; `single`→1.
  - *How to Obtain (Pattern-Aware)*:
    1. Lookup the `human_interaction` value.
    2. Apply the canonical mapping.

---

## 7. ToolPlanningAgent (Layer 3b)
**Role**: Tool & Hook Planning
**Output Model**: `ToolPlanningOutput`
**Wrapper Key**: `ToolPlanning`

### Upstream Dependencies
- **`agents`** (from `AgentRoster`)
  - *Purpose*: Tool ownership.
- **`ui_requirements`** (from `UXArchitecture`)
  - *Purpose*: UI tool definitions.
- **`lifecycle_requirements`** (from `StateArchitecture`)
  - *Purpose*: Lifecycle tool definitions.

### Derivation Logic
- **`agent_tools`**
  - *Semantic Upstream Reference*: `UXArchitecture.ui_requirements` + Requirements
  - *Rule/Taxonomy*: Create `UI_Tool` entries for each UI requirement; add `Agent_Tool` entries for non-UI capabilities. For third-party integrations, generate custom stubs.
  - *How to Obtain (Pattern-Aware)*:
    1. For each `ui_requirement` bound to an agent, create a `UI_Tool` with `interaction_mode` (`inline` or `artifact`).
    2. For each capability needed (API calls, data processing), create an `Agent_Tool` with `interaction_mode: none`.
    3. **Pattern Variation**: Refer to `pattern_guidance.md` for pattern-specific tool conventions.
  - *Integration Examples*:
    | Integration | env_required |
    |-------------|---------------|
    | Slack | SLACK_BOT_TOKEN |
    | PostgreSQL | DATABASE_URL |
    | GitHub | GITHUB_PERSONAL_ACCESS_TOKEN |
    | MongoDB | MONGODB_URI |
    | Stripe | STRIPE_SECRET_KEY |
    | AWS | AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY |
- **`lifecycle_tools`**
  - *Semantic Upstream Reference*: `StateArchitecture.lifecycle_requirements`
  - *Rule/Taxonomy*: Attach lifecycle hooks to the owning agent.
  - *How to Obtain (Pattern-Aware)*:
    1. If `before_chat` hook exists, add lifecycle tool.
    2. Same for `after_chat`.
- **`system_hooks`**
  - *Semantic Upstream Reference*: Requirements (State transitions, Security, Formatting)
  - *Rule/Taxonomy*: Define hooks with `name`, `agent`, `trigger` (enum), and `purpose`.
  - *How to Obtain (Pattern-Aware)*:
    1. Identify the need for runtime behavior modification.
    2. Select the appropriate trigger from the enum:
       - `update_agent_state`: For dynamic context injection (e.g., RAG) before replying.
       - `process_message_before_send`: For redaction or guardrails before sending.
       - `process_last_received_message`: For modifying incoming message content.
       - `process_all_messages_before_reply`: For analyzing full history before replying.
    3. Assign to specific agent (or null for global).
    4. **Pattern Variation**: Refer to `pattern_guidance.md` for pattern-specific hook requirements.

---

## 8. SignalArchitectAgent (Layer 3c)
**Role**: Signal Architecture
**Output Model**: `SignalsPlanOutput`
**Wrapper Key**: `SignalsPlan`

### Upstream Dependencies
- **`agents`** (from `AgentRoster`)
  - *Purpose*: Identify human gates.
- **`pattern`** (from `WorkflowStrategy`)
  - *Purpose*: Identify decomposition opportunities.

### Derivation Logic
- **`signals`**
  - *Semantic Upstream Reference*: `AgentRoster.agents`, `WorkflowStrategy.pattern`
  - *Rule/Taxonomy*: Create `Checkpoint` for human gates; `DecompositionTrigger` for complex patterns.
  - *How to Obtain (Pattern-Aware)*:
    1. **Checkpoints**: Scan `AgentRoster` for `human_interaction` in [`approval`, `feedback`].
       - Create signal: `trigger_agent` = agent before gate, `resume_agent` = agent after gate.
    2. **Decomposition**: If pattern is Hierarchical (4), Star (8), Redundant (7), or Triage (9):
       - Identify orchestrator/router agents.
       - Create `DecompositionTrigger` signal.
    3. **Simple Workflows**: If Pipeline (6) with no gates, return empty list.

---

## 9. ProjectOverviewAgent
**Role**: Visualization
**Output Model**: `MermaidSequenceDiagramOutput`
**Wrapper Key**: `MermaidSequenceDiagram`

### Upstream Dependencies
- **`modules`, `pattern`** (from `WorkflowStrategy`)
  - *Purpose*: Sequence order and topology.
- **`agents`** (from `AgentRoster`)
  - *Purpose*: Participants list.
- **`ui_requirements`** (from `UXArchitecture`)
  - *Purpose*: Interaction notes and alt blocks (approval).
- **`signals`** (from `SignalsPlan`)
  - *Purpose*: Checkpoint markers.

### Derivation Logic
- **`workflow_name`**
  - *Semantic Upstream Reference*: `WorkflowStrategy`
  - *Rule/Taxonomy*: Copy from strategy.
  - *How to Obtain (Pattern-Aware)*:
    1. Copy `WorkflowStrategy.workflow_name` directly.
- **`mermaid_diagram`**
  - *Semantic Upstream Reference*: `AgentRoster`, `WorkflowStrategy`, `SignalsPlan`
  - *Rule/Taxonomy*: Generate sequence diagram. Participants = Agents. Notes = UI Requirements + Signals.
  - *How to Obtain (Pattern-Aware)*:
    1. List all agents as `participant` entries.
    2. Draw arrows based on handoff sequence.
    3. Add `Note` blocks for UI requirements.
    4. Add `alt` blocks for conditional paths.
    5. **Visualize Signals**: For each signal in `SignalsPlan`:
       - If `Checkpoint`: Add `Note over Trigger,Resume: ⏸ CHECKPOINT`.
       - If `DecompositionTrigger`: Add `Note over Agent: ⚡ DECOMPOSITION`.
    6. **Pattern Variation**: Diagram structure varies by pattern—refer to `pattern_guidance.md` for pattern-specific topology.
- **`legend`**
  - *Semantic Upstream Reference*: Modules
  - *Rule/Taxonomy*: Create legend entries mapping module index to module name.
  - *How to Obtain (Pattern-Aware)*:
    1. For each module, create a legend entry with `module_index` and `module_name`.
- **`agent_message`**
  - *Semantic Upstream Reference*: Validation status
  - *Rule/Taxonomy*: Summary + Approval Request.
  - *How to Obtain (Pattern-Aware)*:
    1. Summarize the workflow visually.
    2. Ask the user for approval to proceed.

---

## 10. ContextVariablesAgent
**Role**: Taxonomy Planner
**Output Model**: `ContextVariablesPlanOutput`
**Wrapper Key**: `ContextVariablesPlan`

### Upstream Dependencies
- **`global_context_variables`** (from `StateArchitecture`)
  - *Purpose*: Seed definitions.
- **`agents`** (from `AgentRoster`)
  - *Purpose*: Exposure mapping (who reads what).
- **`agent_tools` (UI_Tool entries)** (from `ToolPlanning`)
  - *Purpose*: Trigger definitions (`ui_response`) for variables set by UI tool responses.

### Derivation Logic
- **`definitions`**
  - *Semantic Upstream Reference*: `StateArchitecture.global_context_variables`
  - *Rule/Taxonomy*: Define all vars with `type` (enum), `source`, and `triggers`.
  - *How to Obtain (Pattern-Aware)*:
    1. For each variable in `global_context_variables`, create a definition entry.
    2. Set `type` to one of the strict enum values: `config`, `data_reference`, `data_entity`, `computed`, `state`, `external`.
    3. Set `source` (init, ui_response, agent_text, derived).
    4. **Pattern Variation**: Refer to `pattern_guidance.md` for pattern-specific context variables.
- **`definitions.trigger`**
  - *Semantic Upstream Reference*: Tools/State
  - *Rule/Taxonomy*: `ui_response` for UI tools; `agent_text` for coordination tokens.
  - *How to Obtain (Pattern-Aware)*:
    1. If variable is set by a UI tool response → `ui_response`.
    2. If variable is set by an agent emitting a coordination token → `agent_text` with `DerivedTrigger`.
    3. If variable is computed from other variables → `derived`.
- **`agents`**
  - *Semantic Upstream Reference*: `AgentRoster`
  - *Rule/Taxonomy*: Map every agent to the list of variables it needs to READ.
  - *How to Obtain (Pattern-Aware)*:
    1. For each agent, identify which context variables appear in its prompt or tool logic.
    2. List those variable names under the agent's entry.

---

## 11. ToolsManagerAgent
**Role**: Tool Manifest Normalization
**Output Model**: `ToolsManifestOutput`
**Wrapper Keys**: `tools`, `lifecycle_tools`

### Upstream Dependencies
- **`agent_tools`** (from `ToolPlanning`)
  - *Purpose*: Source of tool definitions.
- **`ui_requirements`** (from `UXArchitecture`)
  - *Purpose*: UI metadata.
- **`lifecycle_requirements`** (from `StateArchitecture`)
  - *Purpose*: Lifecycle tool definitions.
- **`definitions`** (from `ContextVariablesPlan`)
  - *Purpose*: State variable trigger hints.

### Derivation Logic
- **`tools[].agent`**
  - *Semantic Upstream Reference*: `ToolPlanning`
  - *Rule/Taxonomy*: Owning agent name.
  - *How to Obtain (Pattern-Aware)*:
    1. Copy the `agent_name` from the source `agent_tools` entry.
- **`tools[].file`**
  - *Semantic Upstream Reference*: Convention
  - *Rule/Taxonomy*: `tools/<name>.py` (backend) or `ChatUI/...` (frontend).
  - *How to Obtain (Pattern-Aware)*:
    1. If `tool_type` is `Agent_Tool` → `tools/<name>.py`.
    2. If `tool_type` is `UI_Tool` → `ChatUI/src/components/<Component>.jsx`.
- **`tools[].function`**
  - *Semantic Upstream Reference*: `name`
  - *Rule/Taxonomy*: Same as tool name (snake_case).
  - *How to Obtain (Pattern-Aware)*:
    1. Use the tool's `name` field.
- **`tools[].tool_type`**
  - *Semantic Upstream Reference*: `interaction_mode`
  - *Rule/Taxonomy*: `inline`/`artifact` → `UI_Tool`; otherwise → `Agent_Tool`.
  - *How to Obtain (Pattern-Aware)*:
    1. Check `interaction_mode` from `agent_tools`.
    2. If `inline` or `artifact` → `UI_Tool`.
    3. If `none` or missing → `Agent_Tool`.
- **`tools[].ui`**
  - *Semantic Upstream Reference*: `ui_requirements`
  - *Rule/Taxonomy*: For `UI_Tool`, set `ui.component` + `ui.mode`; else `null`.
  - *How to Obtain (Pattern-Aware)*:
    1. If `UI_Tool`, lookup the matching `ui_requirement` from UXArchitecture.
    2. Set `ui.component` to the component name and `ui.mode` to the display mode.
- **`tools[].description`**
  - *Semantic Upstream Reference*: `ToolPlanning.agent_tools.purpose`
  - *Rule/Taxonomy*: Copy purpose into `description` (<=140 chars).
  - *How to Obtain (Pattern-Aware)*:
    1. Copy the `purpose` field, truncating to 140 characters.
- **`tools[].auto_invoke`**
  - *Semantic Upstream Reference*: Interaction Type + Persistence Needs
  - *Rule/Taxonomy*: Default true for `UI_Tool`; for `Agent_Tool`, true only when auto-persistence is required.
  - *How to Obtain (Pattern-Aware)*:
    1. If `UI_Tool` → `true`.
    2. If `Agent_Tool` with auto-persistence flag → `true`.
    3. Otherwise → `false` or `null`.
- **`lifecycle_tools[]`**
  - *Semantic Upstream Reference*: Lifecycle refs
  - *Rule/Taxonomy*: Include `trigger` + optional `agent` scope; set `tool_type` appropriately.
  - *How to Obtain (Pattern-Aware)*:
    1. For each lifecycle hook in StateArchitecture, create an entry.
    2. Set `trigger` (`before_chat`, `after_chat`, `before_agent`, `after_agent`).
    3. If hook surfaces UI, set `tool_type: UI_Tool` with `ui` metadata.
    4. Otherwise, `tool_type: Agent_Tool`.

---

## 12. UIFileGenerator
**Role**: Frontend Artifact Generation
**Output Model**: `UIToolsFilesOutput`
**Wrapper Key**: `tools`

### Upstream Dependencies
- **`tools` (UI_Tool)** (from `ToolsManifest`)
  - *Purpose*: List of files to generate.
- **`ui_requirements`** (from `UXArchitecture`)
  - *Purpose*: Component names, display hints.
- **`definitions.triggers`** (from `ContextVariablesPlan`)
  - *Purpose*: Response keys for UI state updates.

### Derivation Logic
- **`tools[].filename`**
  - *Semantic Upstream Reference*: Manifest entry
  - *Rule/Taxonomy*: `ChatUI/src/components/<Component>.jsx`.
  - *How to Obtain (Pattern-Aware)*:
    1. Use the component name from `ui_requirements`.
    2. Construct path: `ChatUI/src/components/<ComponentName>.jsx`.
- **`tools[].content`**
  - *Semantic Upstream Reference*: Manifest + UXArchitecture
  - *Rule/Taxonomy*: Generate full React component code.
  - *How to Obtain (Pattern-Aware)*:
    1. Generate a React functional component.
    2. Include imports, props, state, handlers, and JSX.
    3. Use the `ui.mode` to determine styling (`inline` vs `artifact`).
    4. Wire `onSubmit` to emit the trigger key from `ContextVariablesPlan`.
- **`tools[].installRequirements`**
  - *Semantic Upstream Reference*: Manifest
  - *Rule/Taxonomy*: List required packages.
  - *How to Obtain (Pattern-Aware)*:
    1. Analyze the component's imports.
    2. List required packages.

---

## 13. AgentToolsFileGenerator
**Role**: Backend Tool Generation
**Output Model**: `AgentToolsFilesOutput`
**Wrapper Key**: `tools`

### Upstream Dependencies
- **`tools` (Agent_Tool)** (from `ToolsManifest`)
  - *Purpose*: List of files to generate.
- **`agents.agent_name`** (from `AgentRoster`)
  - *Purpose*: Ownership metadata.
- **`global_context_variables`, `workflow_dependencies`** (from `StateArchitecture`)
  - *Purpose*: Access patterns and integration hints.
- **`definitions`** (from `ContextVariablesPlan`)
  - *Purpose*: Data source metadata per tool.

### Derivation Logic
- **`tools[].filename`**
  - *Semantic Upstream Reference*: Manifest entry
  - *Rule/Taxonomy*: `tools/<name>.py`.
  - *How to Obtain (Pattern-Aware)*:
    1. Use the tool's `function` name.
    2. Construct path: `tools/<function_name>.py`.
- **`tools[].content`**
  - *Semantic Upstream Reference*: `integration` + Context Vars
  - *Rule/Taxonomy*: Generate async Python function with proper imports.
  - *How to Obtain (Pattern-Aware)*:
    1. Generate an `async def <function_name>(...)` function.
    2. Include imports for required libraries.
    3. Access context variables using the accessor pattern.
    4. **Pattern Variation**:
       - **Pipeline (6)**: Return `ReplyResult(target=AgentNameTarget("next_agent"))`.
       - **Escalation (2)**: Return `ConsideredResponse(confidence, answer)`.
       - **Star (8)**: Return `AfterWork(AgentTarget(coordinator))`.
- **`tools[].installRequirements`**
  - *Semantic Upstream Reference*: `integration`
  - *Rule/Taxonomy*: Map service to pip package.
  - *How to Obtain (Pattern-Aware)*:
    1. Map integration service names to their corresponding pip package names.

---

## 14. StructuredOutputsAgent
**Role**: Schema Definition
**Output Model**: `StructuredModelsOutput`
**Wrapper Keys**: `models`, `registry`

### Upstream Dependencies
- **`agents`** (from `AgentRoster`)
  - *Purpose*: Registry mapping.
- **`modules`** (from `WorkflowStrategy`)
  - *Purpose*: Schema requirements.
- **`definitions`** (from `ContextVariablesPlan`)
  - *Purpose*: Data entity schemas.

### Derivation Logic
- **`registry[].agent`**
  - *Semantic Upstream Reference*: `AgentRoster.agents`
  - *Rule/Taxonomy*: Agent name (PascalCase).
  - *How to Obtain (Pattern-Aware)*:
    1. List every agent that produces structured output.
- **`registry[].agent_definition`**
  - *Semantic Upstream Reference*: Schema Mapping
  - *Rule/Taxonomy*: Wrapper key name for the agent's output model, or `null`.
  - *How to Obtain (Pattern-Aware)*:
    1. If agent produces structured output, set to the wrapper key name.
    2. If agent produces free text, set to `null`.
- **`models[].model_name`**
  - *Semantic Upstream Reference*: Standards + Context Vars
  - *Rule/Taxonomy*: PascalCase model name.
  - *How to Obtain (Pattern-Aware)*:
    1. Use the convention `<Purpose>Output` for naming.
- **`models[].fields`**
  - *Semantic Upstream Reference*: Schema Requirements
  - *Rule/Taxonomy*: Array of `{name, type, description}`.
  - *How to Obtain (Pattern-Aware)*:
    1. For each required field, define name, type, and description.
    2. **Pattern Variation**: Field schemas vary by pattern—refer to `pattern_guidance.md` for pattern-specific Pydantic models.

---

## 15. AgentsAgent
**Role**: Runtime Definition
**Output Model**: `RuntimeAgentsOutput`
**Wrapper Key**: `agents`

### Upstream Dependencies
- **`agents`** (from `AgentRoster`)
  - *Purpose*: Base agent specs.
- **`agents`** (from `ContextVariablesPlan`)
  - *Purpose*: Context variable exposure.
- **`tools`** (from `ToolsManifest`)
  - *Purpose*: Tool bindings.

### Derivation Logic
- **`name`**
  - *Semantic Upstream Reference*: `AgentRoster`
  - *Rule/Taxonomy*: Agent variable name (snake_case).
  - *How to Obtain (Pattern-Aware)*:
    1. Convert `agent_name` from PascalCase to snake_case.
- **`display_name`**
  - *Semantic Upstream Reference*: `AgentRoster`
  - *Rule/Taxonomy*: Agent display name (Title Case).
  - *How to Obtain (Pattern-Aware)*:
    1. Convert `agent_name` to Title Case with spaces.
- **`prompt_sections`**
  - *Semantic Upstream Reference*: All Upstream
  - *Rule/Taxonomy*: Generate `PromptSections` array with strict 6-section format.
  - *How to Obtain (Pattern-Aware)*:
    1. Create `[ROLE]` section from `objective`.
    2. Create `[OBJECTIVE]` section detailing specific goals.
    3. Create `[CONTEXT]` section listing upstream dependencies.
    4. Create `[INSTRUCTIONS]` section with embedded context variables and coordination tokens.
    5. Create `[EXAMPLES]` section (can be null/empty if not needed).
    6. Create `[OUTPUT FORMAT]` section defining the expected output structure.
    7. **Pattern Variation**: Inject pattern-specific instructions from `pattern_guidance.md`.
- **`auto_tool_mode`**
  - *Semantic Upstream Reference*: `ToolsManifest`
  - *Rule/Taxonomy*: True if agent owns any `UI_Tool`.
  - *How to Obtain (Pattern-Aware)*:
    1. Check if any tool bound to this agent has `tool_type: UI_Tool`.
    2. If yes → `true`; otherwise → `false`.
- **`max_consecutive_auto_reply`**
  - *Semantic Upstream Reference*: `human_interaction`
  - *Rule/Taxonomy*: Apply canonical turn limits.
  - *How to Obtain (Pattern-Aware)*:
    1. Lookup from the mapping: `none`→30, `context`→20, `approval`/`feedback`→5, `single`→1.
- **`structured_outputs_required`**
  - *Semantic Upstream Reference*: `StructuredModels`
  - *Rule/Taxonomy*: True if agent is in the Registry.
  - *How to Obtain (Pattern-Aware)*:
    1. Check if `agent_name` appears in `registry`.
    2. If yes → `true`; otherwise → `false`.

---

## 16. HookAgent
**Role**: Lifecycle Implementation
**Output Model**: `HookFilesOutput`
**Wrapper Key**: `hook_files`

### Upstream Dependencies
- **`lifecycle_requirements.before_chat`, `lifecycle_requirements.after_chat`** (from `StateArchitecture`)
  - *Purpose*: Chat-level hooks.
- **`lifecycle_tools`** (from `ToolPlanning`)
  - *Purpose*: Agent-level hooks.
- **`system_hooks`** (from `ToolPlanning`)
  - *Purpose*: System-level hooks.

### Derivation Logic
- **`hook_files`**
  - *Semantic Upstream Reference*: Hooks
  - *Rule/Taxonomy*: Generate Python files for each hook.
  - *How to Obtain (Pattern-Aware)*:
    1. For `before_chat_lifecycle`, generate `hooks/before_chat.py`.
    2. For `after_chat_lifecycle`, generate `hooks/after_chat.py`.
    3. For `system_hooks`, generate files based on the trigger:
       - `update_agent_state` → `hooks/update_agent_state.py`
       - `process_message_before_send` → `hooks/process_message_before_send.py`
       - `process_last_received_message` → `hooks/process_last_received_message.py`
       - `process_all_messages_before_reply` → `hooks/process_all_messages_before_reply.py`
    4. **Pattern Variation**:
       - **Triage with Tasks (9)**: `update_agent_state` injects current task into worker prompts.
       - **Feedback Loop (3)**: `after_chat` may persist final document.

---

## 17. HandoffsAgent
**Role**: Routing Logic
**Output Model**: `HandoffRulesOutput`
**Wrapper Key**: `handoff_rules`

### Upstream Dependencies
- **`module_index`, `agents`** (from `AgentRoster`)
  - *Purpose*: Default sequence and participants.
- **`definitions.triggers`** (from `ContextVariablesPlan`)
  - *Purpose*: Gating conditions.
- **`tools`** (from `ToolsManifest`)
  - *Purpose*: UI-triggered transitions.

### Derivation Logic
- **`source_agent`**
  - *Semantic Upstream Reference*: Sequence
  - *Rule/Taxonomy*: Current agent in the flow.
  - *How to Obtain (Pattern-Aware)*:
    1. For each agent, create handoff rules where it is the source.
- **`handoff_type`**
  - *Semantic Upstream Reference*: Pattern Semantics
  - *Rule/Taxonomy*: `condition` for router decisions; `after_work` for sequential flow.
  - *How to Obtain (Pattern-Aware)*:
    1. **Pipeline (6)**: `after_work` for linear progression.
    2. **Context-Aware Routing (1)**: `condition` with `OnContextCondition`.
    3. **Hierarchical (4)**: `after_work` with `AfterWork(AgentTarget(supervisor))`.
    4. **Organic (5)**: No explicit handoffs; use `GroupManagerTarget`.
- **`target_agent`**
  - *Semantic Upstream Reference*: Sequence
  - *Rule/Taxonomy*: Next agent or `user` for approvals.
  - *How to Obtain (Pattern-Aware)*:
    1. Lookup the next agent in the sequence.
    2. If handoff returns to user, set `target_agent: user`.
    3. If terminating, set `target_agent: terminate`.
- **`transition_target`**
  - *Semantic Upstream Reference*: `target_agent`
  - *Rule/Taxonomy*: Emit appropriate target type.
  - *How to Obtain (Pattern-Aware)*:
    1. If `target_agent` is an agent name → `AgentTarget` or `AgentNameTarget`.
    2. If `target_agent` is `user` → `RevertToUserTarget`.
    3. If `target_agent` is `terminate` → `TerminateTarget`.
    4. **Pattern Variation**:
       - **Star (8)**: Spokes return with `AfterWork(AgentTarget(coordinator))`.
       - **Redundant (7)**: Workers use `NestedChatTarget` for isolation.
- **`condition`**
  - *Semantic Upstream Reference*: `triggers`
  - *Rule/Taxonomy*: Map triggers to expressions or `string_llm` checks.
  - *How to Obtain (Pattern-Aware)*:
    1. If trigger is `ui_response`, create an expression checking the response value.
    2. If trigger is `agent_text`, use `string_llm` for semantic matching.
    3. Set `condition_scope: pre` when reacting to UI responses.
    4. **Pattern Variation**:
       - **Context-Aware Routing (1)**: `OnContextCondition(current_domain=="...")`.
       - **Escalation (2)**: `OnContextCondition(confidence < 8)`.
       - **Feedback Loop (3)**: `OnContextCondition(iteration_needed==True)`.
- **`condition_type`**
  - *Semantic Upstream Reference*: `condition`
  - *Rule/Taxonomy*: `expression`, `string_llm`, or `null`.
  - *How to Obtain (Pattern-Aware)*:
    1. If condition uses `${var}` syntax → `expression`.
    2. If condition uses natural language → `string_llm`.
    3. If no explicit condition → `null`.

---

## 18. OrchestratorAgent
**Role**: Runtime Configuration
**Output Model**: `OrchestrationConfigOutput`
**Wrapper Key**: (direct fields)

### Upstream Dependencies
- **`workflow_name`, `startup_mode`, `human_in_loop`** (from `WorkflowStrategy`)
  - *Purpose*: Direct copy to output fields (`workflow_name`, `startup_mode`, `human_in_the_loop`).
- **`workflows[current_workflow_index].pattern_name`** (from `PatternSelection`)
  - *Purpose*: `orchestration_pattern` field.
- **`agents`** (from `AgentRoster`)
  - *Purpose*: `initial_agent` (first agent of first module).
- **`tools`** (from `ToolsManifest`)
  - *Purpose*: `visual_agents` (agents owning UI_Tools).

### Derivation Logic
- **`workflow_name`**
  - *Semantic Upstream Reference*: `WorkflowStrategy`
  - *Rule/Taxonomy*: Copy from strategy.
  - *How to Obtain (Pattern-Aware)*:
    1. Copy `WorkflowStrategy.workflow_name` directly.
- **`max_turns`**
  - *Semantic Upstream Reference*: `human_in_the_loop`
  - *Rule/Taxonomy*: 50 if human in loop, 100 if backend only.
  - *How to Obtain (Pattern-Aware)*:
    1. If `human_in_the_loop` is true → `50`.
    2. If false → `100`.
- **`human_in_the_loop`**
  - *Semantic Upstream Reference*: `WorkflowStrategy`
  - *Rule/Taxonomy*: Copy `human_in_loop` value.
  - *How to Obtain (Pattern-Aware)*:
    1. Copy `WorkflowStrategy.human_in_loop` (note: field name translation to `human_in_the_loop`).
- **`startup_mode`**
  - *Semantic Upstream Reference*: `WorkflowStrategy`
  - *Rule/Taxonomy*: Copy exactly.
  - *How to Obtain (Pattern-Aware)*:
    1. Copy `WorkflowStrategy.startup_mode` directly.
- **`orchestration_pattern`**
  - *Semantic Upstream Reference*: `PatternSelection`
  - *Rule/Taxonomy*: Copy the selected workflow's `pattern_name`.
  - *How to Obtain (Pattern-Aware)*:
    1. Copy `PatternSelection.workflows[current_workflow_index].pattern_name`.
    2. This determines whether to use `DefaultPattern` or `AutoPattern` (Organic only).
- **`initial_agent`**
  - *Semantic Upstream Reference*: `AgentRoster`
  - *Rule/Taxonomy*: First agent of first module.
  - *How to Obtain (Pattern-Aware)*:
    1. Find the agent with `module_index: 0` and lowest sequence position.
    2. Return its `agent_name`.
- **`initial_message` / `initial_message_to_user`**
  - *Semantic Upstream Reference*: `WorkflowStrategy`
  - *Rule/Taxonomy*: Seed based on `startup_mode`.
  - *How to Obtain (Pattern-Aware)*:
    1. If `UserDriven` → `initial_message_to_user` prompts user to start.
    2. If `AgentDriven` → `initial_message` contains the agent's greeting.
    3. If `BackendOnly` → `initial_message` contains the task context.
- **`visual_agents`**
  - *Semantic Upstream Reference*: `AgentRoster` + `ToolsManifest`
  - *Rule/Taxonomy*: List every agent that owns a UI_Tool.
  - *How to Obtain (Pattern-Aware)*:
    1. Filter agents where at least one bound tool has `tool_type: UI_Tool`.
    2. Return list of `agent_name` values.

---

## 19. DownloadAgent
**Role**: Delivery
**Output Model**: `DownloadRequestOutput`
**Wrapper Key**: `agent_message`

### Upstream Dependencies
- **Presence** (from All Artifacts)
  - *Purpose*: Validation gate.

### Derivation Logic
- **`agent_message`**
  - *Semantic Upstream Reference*: Completion Status
  - *Rule/Taxonomy*: Emit invitation only when all upstream artifacts exist.
  - *How to Obtain (Pattern-Aware)*:
    1. Check that all required artifacts are present (agents, tools, hooks, handoffs, etc.).
    2. If all present → emit download invitation message.
    3. If any missing → emit error message listing missing artifacts.
