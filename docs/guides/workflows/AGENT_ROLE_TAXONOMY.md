# Agent Role Taxonomy

> **Last Updated**: 2025-06-27  
> **Status**: Implemented  
> **Source of Truth**: `update_agent_state_pattern.py`

To eliminate ambiguity in agent identification, we use an explicit `agent_type` field. This allows downstream agents (ContextVariables, AgentsAgent) to deterministically identify agent responsibilities without guessing based on names.

## The Field: `agent_type`

**Values** (5 types):

1.  **`intake`**
    *   **Function**: Entry point for user interaction. Greets the user, gathers context, asks clarifying questions.
    *   **Behaviors**: Conversational first contact, intent gathering, context capture.
    *   **Context**: Needs access to `user_input`, initial request data.
    *   **Human Interaction**: Typically `single-llm` or `interview` mode.
    *   **Example**: A triage agent that asks "What would you like help with today?"

2.  **`router`**
    *   **Function**: Determines control flow. Decides "Where do we go next?"
    *   **Behaviors**: Emits routing decisions, selects next phase/agent based on classification.
    *   **Context**: Needs access to state variables (`current_domain`, `classification_result`).
    *   **Human Interaction**: Typically `none` (silent dispatcher).
    *   **Example**: A classifier that routes technical vs billing questions.

3.  **`worker`**
    *   **Function**: Executes core tasks. Uses tools to generate value.
    *   **Behaviors**: Calls APIs, generates content, runs analysis, processes data.
    *   **Context**: Needs access to data variables (`user_input`, `search_results`).
    *   **Human Interaction**: `none` (autonomous) or `single-llm` (co-pilot asking for parameters).
    *   **Example**: A research agent that calls search APIs and summarizes findings.

4.  **`evaluator`**
    *   **Function**: Quality assurance and decision making.
    *   **Behaviors**: Reviews worker output, approves/rejects, requests revisions, provides feedback.
    *   **Context**: Needs access to `approval_status`, `feedback_history`, `iteration_count`.
    *   **Human Interaction**: Typically `approval` or `iterative` mode.
    *   **Example**: A reviewer that checks generated content before publishing.

5.  **`orchestrator`**
    *   **Function**: Manages a sub-team or coordinates complex workflows.
    *   **Behaviors**: Delegates tasks to workers, synthesizes results, tracks sub-task completion.
    *   **Context**: Needs access to `team_status`, `sub_task_list`, specialist completion flags.
    *   **Human Interaction**: Typically `none` (background coordination).
    *   **Example**: A hub coordinator in the Star pattern that delegates to specialists.

---

## Mapping Agent Types to AG2 Patterns

| Pattern | Primary Agent Types Used |
|---------|-------------------------|
| 1. Context-Aware Routing | `router` (classifier) + `worker` (specialists) |
| 2. Escalation | `intake` (triage) + `worker` (tiered agents) |
| 3. Feedback Loop | `intake` (entry) + `worker` (planner, drafter, reviser) + `evaluator` (reviewer) |
| 4. Hierarchical | `orchestrator` (executive) + `orchestrator` (managers) + `worker` (specialists) |
| 5. Organic | `worker` (all contributors) - GroupChatManager handles routing |
| 6. Pipeline | `intake` (entry) + `worker` (stage agents) |
| 7. Redundant | `orchestrator` (taskmaster) + `worker` (parallel workers) + `evaluator` (evaluator) |
| 8. Star | `orchestrator` (hub/coordinator) + `worker` (spoke specialists) |
| 9. Triage with Tasks | `intake` (triage) + `orchestrator` (task manager) + `worker` (research/writing agents) |

---

### Interaction Modes (orthogonal to `agent_type`)

`human_interaction` remains the channel switch across all agent types:

| `human_interaction` | Meaning | Typical usage |
| --- | --- | --- |
| `none` | Agent never surfaces messages directly to the user. | Background routers, batch processors, orchestrators. |
| `single-llm` | Agent can converse with the user for context/clarification. | Intake agents, L1 support, co-pilot workers. |
| `interview` | Multi-turn context gathering with structured data collection. | Onboarding wizards, complex data capture. |
| `iterative` | Agent engages in feedback loops with user refinement. | Creative content workers, iterative evaluators. |
| `approval` | Agent requires explicit sign-off or provides review feedback. | Evaluators, compliance reviewers, gatekeepers. |

### Router Interaction Decision Logic

WorkflowImplementationAgent decides whether a router behaves like a silent dispatcher or an interactive intake by deriving `human_interaction` from upstream context:

1. **Read WorkflowStrategy metadata**: `phase.human_in_loop=false` ⇒ router is silent (`human_interaction="none"`).
2. **Check TechnicalBlueprint.ui_components**: if the phase has an inline/inline-like component mapped to the router, mirror its `ui_pattern`.
    * `single_step` / `context_capture` → `human_interaction="context"` (router greets the user, asks clarifying questions, then routes).
    * `two_step_confirmation` / `multi_step` → `human_interaction="approval"` (router must pause for confirmation before routing).
3. **Fallback when no component exists**: if `phase.human_in_loop=true` but there is no component, treat the router as conversational intake by default (`context`).

By keeping this logic in WorkflowImplementationAgent we ensure routers stay declarative: the workflow defines *when* humans enter the loop, and the runtime simply toggles `human_interaction` to create either a silent dispatcher or an intake-style conversational router.

---

## Implementation Flow

### 1. WorkflowImplementationAgent (The Assigner)
*   **Input**: `WorkflowStrategy` (Pattern, Module Purpose) + `TechnicalBlueprint` (UI Contracts).
*   **Logic**:
    *   If first agent + user-facing entry point → Assign `intake`.
    *   If Pattern has routing logic + no user interaction → Assign `router`.
    *   If `human_interaction`="approval" → Assign `evaluator`.
    *   If hub/coordinator in Star/Hierarchical → Assign `orchestrator`.
    *   Default → Assign `worker`.
*   **Output**: Adds `"agent_type": "intake|router|worker|evaluator|orchestrator"` to the agent definition.

### 2. ContextVariablesAgent (The Consumer)
*   **Logic**: Reads `agent_type` to determine trigger assignments.
    *   `router` agents get routing-related context variable triggers.
    *   `evaluator` agents get approval/feedback context variable triggers.

### 3. AgentsAgent (The Prompter)
*   **Logic**:
    *   If `agent_type` == 'intake': Inject "You are the first point of contact. Greet the user..."
    *   If `agent_type` == 'router': Inject "You are a Router. Analyze and route based on..."
    *   If `agent_type` == 'worker': Inject "You are a Worker. Execute your assigned task..."
    *   If `agent_type` == 'evaluator': Inject "You are a Reviewer. Evaluate quality..."
    *   If `agent_type` == 'orchestrator': Inject "You are a Coordinator. Manage your team..."

## Benefits
1.  **Deterministic**: No ambiguity in agent roles.
2.  **Scalable**: All 9 AG2 patterns map to these 5 types.
3.  **Reliable**: Prompts are tailored to the specific architectural role.
4.  **Semantic Clarity**: `intake` explicitly marks user-facing entry points.
