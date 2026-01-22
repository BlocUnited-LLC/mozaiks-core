# Interaction & Taxonomy Matrix

> **Last Updated**: 2025-06-27  
> **Status**: Implemented Source of Truth  
> **Purpose**: To align Agent Types, Strategic Intent, and UI patterns into a single deterministic matrix.

## The Unified Matrix

This matrix defines the valid combinations of Agent Type, Human Interaction, and UI Patterns. Agents MUST fall into one of these rows.

### INTAKE (User-Facing Entry)
*   **Human in Loop**: `true`
*   **Human Interaction**: `single-llm` or `interview`
*   **UI Pattern**: `single_step` (`inline`) or `multi_step` (`artifact`)
*   **Use Case**: Initial user greeting, clarifying questions, intent gathering, onboarding wizards.

### ROUTER (Silent Dispatcher)
*   **Human in Loop**: `false`
*   **Human Interaction**: `none`
*   **UI Pattern**: N/A
*   **Use Case**: Backend routing based on data/classification. No user contact.

### WORKER (Autonomous)
*   **Human in Loop**: `false`
*   **Human Interaction**: `none`
*   **UI Pattern**: N/A
*   **Use Case**: Backend processing, API calls, data transformation.

### WORKER (Co-Pilot)
*   **Human in Loop**: `true`
*   **Human Interaction**: `single-llm`
*   **UI Pattern**: `single_step` (`inline`)
*   **Use Case**: Asking for missing parameters during execution.

### WORKER (Wizard)
*   **Human in Loop**: `true`
*   **Human Interaction**: `interview`
*   **UI Pattern**: `multi_step` (`artifact`)
*   **Use Case**: Complex data gathering, multi-stage forms.

### EVALUATOR (Reviewer)
*   **Human in Loop**: `true`
*   **Human Interaction**: `approval`
*   **UI Pattern**: `two_step_confirmation` (`artifact`)
*   **Use Case**: Reviewing generated content (drafts, code) before finalization.

### EVALUATOR (Iterative)
*   **Human in Loop**: `true`
*   **Human Interaction**: `iterative`
*   **UI Pattern**: `multi_step` (`artifact`)
*   **Use Case**: Feedback loops where user refines output over multiple turns.

### EVALUATOR (Gatekeeper)
*   **Human in Loop**: `true`
*   **Human Interaction**: `approval`
*   **UI Pattern**: `single_step` (`inline`)
*   **Use Case**: Simple Yes/No permission gates (e.g., "Proceed to deploy?").

### ORCHESTRATOR (Manager)
*   **Human in Loop**: `false`
*   **Human Interaction**: `none`
*   **UI Pattern**: N/A
*   **Use Case**: Managing sub-teams, coordinating specialists, hub in Star pattern.

---

## Definitions & Flow

### 1. Agent Type (The "Who")
Defined in `AGENT_ROLE_TAXONOMY.md`. 5 types:
- **Intake**: User-facing entry point, context gathering.
- **Router**: Traffic control, classification, routing decisions.
- **Worker**: Task execution, tool usage.
- **Evaluator**: Quality/Decision control, approval gates.
- **Orchestrator**: Team management, coordination.

### 2. Strategic Intent (The "Why")
Defined by `WorkflowStrategyAgent`.
- **`human_in_loop = true`**: The workflow involves human interaction (Global setting).
- **`human_in_loop = false`**: The workflow is fully automated (Global setting).

### 3. Interaction Mode (The "How")
Defined by `WorkflowImplementationAgent`. 5 modes:
- **`none`**: No user contact. Background processing.
- **`single-llm`**: Simple back-and-forth conversation.
- **`interview`**: Structured multi-turn data collection.
- **`iterative`**: Feedback loops with refinement cycles.
- **`approval`**: Blocking gate requiring explicit sign-off.

### 4. UI Pattern (The "Interface")
Defined by `WorkflowArchitectAgent`.
- **`single_step`**: Input -> Action. (Chat, Forms).
- **`two_step_confirmation`**: Preview -> Decision. (Cards, Artifacts).
- **`multi_step`**: Iterative/Wizard -> Action. (Complex Forms, Feedback Loops).

---

## Decision Logic for Agents

### WorkflowStrategyAgent (Global Definition)
*   **IF** workflow involves "Intake", "Review", or "Clarification" -> Set `human_in_loop = true` (Global).
*   **ELSE** -> Set `human_in_loop = false` (Global).

### WorkflowArchitectAgent (UI Definition)
*   **IF** module purpose implies interaction:
    *   **IF** purpose is "Intake/Clarification" -> Create Component (`ui_pattern="single_step"`, `display="inline"`).
    *   **IF** purpose is "Review/Approval" -> Create Component (`ui_pattern="two_step_confirmation"`, `display="artifact"`).
    *   **IF** purpose is "Complex Data Gathering" or "Iterative Feedback" -> Create Component (`ui_pattern="multi_step"`, `display="artifact"`).

### WorkflowImplementationAgent (Agent Definition)
*   **Derive `human_interaction` from Intent**:
    *   **IF** Agent is entry point + needs context -> Set `agent_type="intake"`, `human_interaction="single-llm"`.
    *   **IF** Agent routes silently -> Set `agent_type="router"`, `human_interaction="none"`.
    *   **IF** Agent executes tasks -> Set `agent_type="worker"`, `human_interaction="none"` or `"single-llm"`.
    *   **IF** Agent reviews/approves -> Set `agent_type="evaluator"`, `human_interaction="approval"` or `"iterative"`.
    *   **IF** Agent coordinates team -> Set `agent_type="orchestrator"`, `human_interaction="none"`.
