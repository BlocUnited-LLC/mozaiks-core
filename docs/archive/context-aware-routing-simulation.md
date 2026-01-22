# Context-Aware Routing - Generator Simulation

This document simulates the step-by-step execution of the MozaiksAI Generator agents when building a **Context-Aware Routing** workflow. It analyzes the logic, output, and potential improvements for each agent.

---

## 1. WorkflowStrategyAgent

**Role**: Strategic Architect (Layer 1)
**Input**: User Interview ("I need a bot that routes tech, finance, and health questions to experts") + Pattern Selection ("Context-Aware Routing").
**Goal**: Define the high-level blueprint (Phases, Trigger, Intent).

### Simulated Output

```json
{
  "WorkflowStrategy": {
    "workflow_name": "Context-Aware Domain Router",
    "workflow_description": "A dynamic routing system that intelligently dispatches user queries to specialized agents (Technology, Finance, Healthcare, General) based on content analysis and confidence scores.",
    "human_in_loop": true,
    "trigger": "chat",
    "initiated_by": "user",
    "pattern": [
      "Context-Aware Routing"
    ],
    "phases": [
      {
        "phase_index": 0,
        "phase_name": "Intelligent Dispatch & Resolution",
        "phase_description": "The Router Agent analyzes the user's input to detect intent and domain. It then routes the task to the appropriate specialist (Tech, Finance, Health, or General). The specialist generates a response, and the Router ensures the user's query is fully resolved or asks for clarification if ambiguous.",
        "human_in_loop": true,
        "agents_needed": "nested"
      }
    ]
  }
}
```

### Logic Analysis

1.  **`human_in_loop: true`**:
    *   **Why**: The workflow is triggered by `chat` (user initiated). Even though the *routing* is automated, the *interaction* is conversational. The pattern also explicitly mentions "Ambiguity Resolution" (asking the user for clarification), which necessitates a human-in-the-loop.
    *   **Constraint**: The prompt correctly identifies that if the trigger is "chat", `human_in_loop` is almost always true.

2.  **`agents_needed: "nested"`**:
    *   **Why**: This is the most critical decision. A "Context-Aware Router" is not a linear pipeline (A -> B -> C). It is a hub-and-spoke model.
    *   **Mapping**: In MozaiksAI, `nested` maps to an AG2 GroupChat or a Coordinator + Specialists topology. This allows the Router to dynamically select the next speaker (Tech, Finance, etc.) rather than following a fixed sequence.

3.  **Single Phase (`phase_index: 0`)**:
    *   **Why**: The entire routing logic happens within a single conversation loop. Splitting it into "Phase 1: Analyze" and "Phase 2: Route" would force a linear handoff, breaking the dynamic nature of AG2's `GroupChat`. By keeping it in one phase, we allow the AG2 runtime to handle the turn-taking dynamically.

### Recommendations & Improvements

*   **Risk of Linearization**: There is a risk that a less capable LLM might try to split this into sequential phases (e.g., "Phase 1: Router", "Phase 2: Specialist").
    *   **Fix**: Ensure the `PatternAgent` explicitly passes a hint to `WorkflowStrategyAgent` that "Routing patterns should typically be a single 'nested' phase."
*   **Description Specificity**: The `phase_description` is doing a lot of heavy lifting here. It lists the specific domains (Tech, Finance, etc.). If the user didn't explicitly name these domains in the interview, the agent might hallucinate them or be too generic.
    *   **Fix**: The `InterviewAgent` must ensure it captures the *specific* domains the user wants to route between.

---

## 2. WorkflowArchitectAgent

**Role**: System Designer (Layer 2)
**Input**: `WorkflowStrategyOutput` (Phases, Global Human-in-Loop).
**Goal**: Define the technical infrastructure (Context Variables, UI Components, Lifecycle Hooks) required to support the strategy.
**Output Schema**: `TechnicalBlueprintOutput`

### Simulated Output

```json
{
  "TechnicalBlueprint": {
    "global_context_variables": [
      {
        "name": "domain_classification",
        "type": "computed",
        "purpose": "Stores the identified domain (Technology, Finance, Healthcare, General) to control routing logic.",
        "trigger_hint": "Set by the Router Agent after analyzing the user's input."
      },
      {
        "name": "routing_confidence",
        "type": "computed",
        "purpose": "Confidence score (0.0-1.0) of the classification to determine if clarification is needed.",
        "trigger_hint": "Set by the Router Agent alongside classification."
      },
      {
        "name": "clarification_needed",
        "type": "state",
        "purpose": "Boolean flag indicating if the Router needs to ask the user for more details before routing.",
        "trigger_hint": "Set to true if routing_confidence is below threshold."
      }
    ],
    "ui_components": [],
    "before_chat_lifecycle": {
      "name": "initialize_routing_context",
      "purpose": "Resets domain classification and confidence scores to ensure a clean state for new sessions.",
      "trigger": "before_chat",
      "integration": null
    },
    "after_chat_lifecycle": null,
    "workflow_dependencies": null
  }
}
```

### Instruction Analysis (Stateless Generation)

*   **Input Source**: The agent receives `WorkflowStrategyOutput`. It sees `pattern=["Context-Aware Routing"]` and `phases=[{...agents_needed="nested"}]`.
*   **Stateless Logic**:
    1.  **Data Requirement Analysis**: It knows "Context-Aware Routing" relies on *decision-making* data. It defines `domain_classification` (the decision) and `routing_confidence` (the quality check) as `computed` variables because they come from LLM analysis, not a database.
    2.  **State Management**: It adds `clarification_needed` to handle the "Ambiguity Resolution" requirement mentioned in the strategy description.
    3.  **Lifecycle**: It adds a `before_chat` hook to clear these variables, preventing old routing decisions from sticking to new conversations.
    4.  **UI Decision**: It sees `human_in_loop=true` but determines that standard chat is sufficient for clarification questions. No custom React components (`ui_components`) are needed for this specific pattern unless the user explicitly asked for a "Dashboard" or "Form".
*   **Constraint Check**: It strictly follows the `TechnicalBlueprint` schema. It does *not* define the agents (Router, Specialist) here; it only defines the *variables* they will use to communicate.

### Logic Analysis

1.  **Variable-Driven Routing**:
    *   **Why**: The Architect correctly identifies that the *routing mechanism* in AG2 is driven by context variables. By defining `domain_classification`, it paves the way for the Implementation Agent to write a `handoff` rule like: `if domain_classification == 'Finance' -> goto FinanceAgent`.
    *   **Missing Piece**: It assumes the domains are fixed (Tech, Finance, Health). If the user wanted *dynamic* domains (e.g., from a database), the Architect would need to define a `data_reference` variable to load those valid domains.

2.  **No UI Components**:
    *   **Correctness**: This is correct for a chat-based router. Adding a UI component for "Selecting a Domain" would defeat the purpose of an *AI* router. The AI should do the work.

---

## 3. WorkflowImplementationAgent

**Role**: Runtime Engineer (Layer 3)
**Input**: `WorkflowStrategyOutput` (Phases) + `TechnicalBlueprintOutput` (Context Variables).
**Goal**: Define the specific agents, their system messages, and the handoff logic that connects them.
**Output Schema**: `PhaseAgentsOutput`

### Simulated Output

```json
{
  "PhaseAgents": [
    {
      "phase_index": 0,
      "agents": [
        {
          "agent_name": "router_agent",
          "agent_type": "router",
          "description": "Analyzes user input to determine the correct domain specialist.",
          "human_interaction": "context",
          "agent_tools": [
            {
              "name": "update_routing_context",
              "purpose": "Updates the domain_classification and routing_confidence variables based on analysis.",
              "integration": null
            }
          ],
          "lifecycle_tools": [],
          "system_hooks": [],
          "max_consecutive_auto_reply": 10
        },
        {
          "agent_name": "tech_specialist",
          "agent_type": "worker",
          "description": "Handles technology-related queries.",
          "human_interaction": "none",
          "agent_tools": [
            {
              "name": "submit_specialist_response",
              "purpose": "Submits the final answer for the technology domain.",
              "integration": null
            }
          ],
          "lifecycle_tools": [],
          "system_hooks": [],
          "max_consecutive_auto_reply": 10
        },
        {
          "agent_name": "finance_specialist",
          "agent_type": "worker",
          "description": "Handles finance-related queries.",
          "human_interaction": "none",
          "agent_tools": [
            {
              "name": "submit_specialist_response",
              "purpose": "Submits the final answer for the finance domain.",
              "integration": null
            }
          ],
          "lifecycle_tools": [],
          "system_hooks": [],
          "max_consecutive_auto_reply": 10
        },
        {
          "agent_name": "health_specialist",
          "agent_type": "worker",
          "description": "Handles healthcare-related queries.",
          "human_interaction": "none",
          "agent_tools": [
            {
              "name": "submit_specialist_response",
              "purpose": "Submits the final answer for the healthcare domain.",
              "integration": null
            }
          ],
          "lifecycle_tools": [],
          "system_hooks": [],
          "max_consecutive_auto_reply": 10
        },
        {
          "agent_name": "general_specialist",
          "agent_type": "worker",
          "description": "Handles general queries that do not fit other categories.",
          "human_interaction": "none",
          "agent_tools": [
            {
              "name": "submit_specialist_response",
              "purpose": "Submits the final answer for the general domain.",
              "integration": null
            }
          ],
          "lifecycle_tools": [],
          "system_hooks": [],
          "max_consecutive_auto_reply": 10
        }
      ]
    }
  ]
}
```

### Instruction Analysis (Stateless Generation)

*   **Input Source**: The agent receives `WorkflowStrategyOutput` (Phase 0: "Intelligent Dispatch", `agents_needed="nested"`) and `TechnicalBlueprintOutput` (Variables: `domain_classification`).
*   **Stateless Logic**:
    1.  **Role Expansion**: It sees `agents_needed="nested"` and the description mentioning "Tech, Finance, Health". It expands this into a Hub-and-Spoke topology.
    2.  **Modular Tool Generation (The "State-Writer" Rule)**:
        *   It sees the Architect defined `domain_classification` as a `computed` variable.
        *   It knows the `router_agent` is responsible for this decision.
        *   **Therefore**: It assigns a generic tool `update_routing_context` to the Router. This tool will take arguments like `domain` and `confidence`. This is cleaner than generating 5 separate tools (`route_to_tech`, `route_to_finance`, etc.).
    3.  **Worker Tools**: It assigns `submit_specialist_response` to the workers. This ensures they don't just "talk" but actually "commit" their work, which is useful for tracking progress or triggering downstream hooks.

### Logic Analysis

1.  **Correct Topology**:
    *   **Why**: The agent correctly translated the "Context-Aware Routing" pattern into a concrete set of agents. It didn't just create one "Assistant"; it created the *team* required to execute the logic.
    *   **Tool-Driven State**: By giving the Router a tool to update state, we ensure the `HandoffsAgent` (downstream) has reliable data to work with. The Handoff rule will simply be: `IF domain_classification == 'tech' THEN Handoff(TechSpecialist)`.

2.  **Missing System Messages**:
    *   **Note**: The `PhaseAgentsOutput` schema *does not* include the full system message text. That is generated by the `AgentsAgent` (RuntimeAgentsOutput) later. This agent focuses on the *structural definition* (names, types, roles, tools).

### Recommendations & Improvements

*   **Domain Hardcoding**: The agent inferred "Tech, Finance, Health" from the Strategy description.
    *   **Risk**: If the Strategy description was vague ("Route to appropriate experts"), this agent might hallucinate domains or default to generic ones.
    *   **Fix**: Ensure the `WorkflowStrategyAgent` is *very* specific in its `phase_description` about exactly which domains exist.
