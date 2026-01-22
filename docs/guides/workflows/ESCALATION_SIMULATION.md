# Escalation Pattern - Generator Simulation

This document simulates the step-by-step execution of the MozaiksAI Generator agents when building an **Escalation** workflow. It analyzes the logic, output, and potential improvements for each agent.

---

## 1. WorkflowStrategyAgent

**Role**: Strategic Architect (Layer 1)
**Input**: User Interview ("I need a system that handles simple questions cheaply but escalates complex ones to experts") + Pattern Selection ("Escalation").
**Goal**: Define the high-level blueprint (Phases, Trigger, Intent).

### Simulated Output

```json
{
  "WorkflowStrategy": {
    "workflow_name": "Tiered Escalation System",
    "workflow_description": "A resource-efficient query handling system that routes tasks to the most cost-effective agent capable of solving them. It starts with a Basic Agent and escalates to Intermediate or Advanced Agents only when confidence thresholds are not met.",
    "human_in_loop": true,
    "trigger": "chat",
    "initiated_by": "user",
    "pattern": [
      "Escalation"
    ],
    "phases": [
      {
        "phase_index": 0,
        "phase_name": "Triage and Escalation Loop",
        "phase_description": "A unified group chat where a Triage Agent receives queries. A Basic Agent attempts to answer first. If its confidence is low (<8/10), the task is escalated to an Intermediate Agent, and finally to an Advanced Agent if necessary. Context is preserved across escalations.",
        "agents_needed": "nested"
      }
    ]
  }
}
```

### Instruction Analysis (Stateless Generation)

*   **Input Source**: The agent receives `PatternSelectionOutput` (containing `PatternSelection.workflows[].pattern_id = 2` for Escalation) and the `InterviewTranscript`.
*   **Stateless Logic**:
    1.  **Pattern Mapping**: It reads `pattern_id=2` ("Escalation") from `PatternSelection.workflows[current_workflow_index]` in the upstream `PatternSelectionOutput`.
    2.  **Guidance Injection**: The runtime injects the specific `[INJECTED PATTERN GUIDANCE - Escalation]` block into the prompt (replacing `{{PATTERN_GUIDANCE_AND_EXAMPLES}}`). This block explicitly tells the agent that Escalation requires a "nested" topology.
    3.  **Global Flag**: It sets `human_in_loop: true` because the trigger is `chat` (derived from Interview "I need a system that handles questions"). This is a global strategic intent, not a phase-level detail.
    4.  **Phase Construction**: It constructs a single phase (`phase_index: 0`) because the injected guidance describes Escalation as a dynamic loop. It sets `agents_needed: "nested"` to signal downstream agents to build a GroupChat.
*   **Constraint Check**: It strictly follows the `WorkflowStrategy` schema, ensuring `human_in_loop` is only at the root level and `phases` contains only the allowed fields (`phase_name`, `phase_index`, `phase_description`, `agents_needed`).

### Logic Analysis

1.  **`agents_needed: "nested"`**:
    *   **Why**: The Escalation pattern is inherently a state-machine logic within a single conversation. It is not a linear pipeline of distinct phases (e.g., "Phase 1: Research" -> "Phase 2: Write"). Instead, it is a conditional loop where multiple agents (Basic, Intermediate, Advanced) might speak depending on the *outcome* of the previous agent's attempt.
    *   **Mapping**: In MozaiksAI/AG2, this requires a `GroupChat` (or `nested` chat) where transitions are controlled by `handoffs` or `OnContextCondition` (as seen in the pattern documentation).

2.  **`human_in_loop: true`**:
    *   **Why**: The trigger is `chat`, implying a user is asking questions. Furthermore, the "Advanced Agent" might fall back to a human expert if even it cannot solve the problem.

3.  **Single Phase**:
    *   **Why**: Splitting this into "Phase 1: Basic", "Phase 2: Intermediate" would be incorrect because we don't know *a priori* if the Intermediate agent is needed. The logic must be dynamic within a single runtime container.

### Recommendations & Improvements

*   **Complexity Warning**: This pattern relies heavily on *structured outputs* (confidence scores) to drive the logic. The `WorkflowStrategyAgent` should ideally flag this requirement.
    *   **Fix**: Ensure the `WorkflowArchitectAgent` (next step) knows that it *must* generate Pydantic models for the agent responses, not just standard text prompts.
*   **Context Variables**: The strategy implicitly assumes state tracking (`escalation_count`, `confidence`). The Strategy Agent doesn't need to define these variables, but the Architect Agent definitely will.

---

## 2. WorkflowArchitectAgent

**Role**: System Designer (Layer 2)
**Input**: `WorkflowStrategy` JSON.
**Goal**: Define the technical infrastructure (Context Variables, UI Components, Lifecycle Hooks) required to support the strategy.
**Output Schema**: `TechnicalBlueprintOutput`

### Simulated Output

```json
{
  "TechnicalBlueprint": {
    "global_context_variables": [
      {
        "name": "basic_agent_confidence",
        "type": "computed",
        "purpose": "Stores the confidence score (1-10) of the basic agent's last response to determine if escalation is needed.",
        "trigger_hint": "Set by Basic Agent after evaluating the user query."
      },
      {
        "name": "intermediate_agent_confidence",
        "type": "computed",
        "purpose": "Stores the confidence score (1-10) of the intermediate agent's last response.",
        "trigger_hint": "Set by Intermediate Agent after evaluating the escalated query."
      },
      {
        "name": "escalation_reason",
        "type": "computed",
        "purpose": "Captures the specific reason why an agent could not answer confidently.",
        "trigger_hint": "Set by any agent when confidence is < 8."
      },
      {
        "name": "escalation_count",
        "type": "state",
        "purpose": "Tracks how many times the query has been escalated to prevent infinite loops.",
        "trigger_hint": "Incremented by the runtime or Triage agent upon each handoff."
      }
    ],
    "ui_components": [],
    "before_chat_lifecycle": {
      "name": "initialize_escalation_state",
      "purpose": "Resets confidence scores and escalation counters to zero before a new session begins.",
      "trigger": "before_chat",
      "integration": null
    },
    "after_chat_lifecycle": null,
    "workflow_dependencies": null
  }
}
```

### Instruction Analysis (Stateless Generation)

*   **Input Source**: The agent receives `WorkflowStrategyOutput` (containing the phases and global `human_in_loop` flag).
*   **Stateless Logic**:
    1.  **State Requirement Analysis**: It scans the strategy and sees the "Escalation" pattern. It knows (from its training/prompt) that Escalation requires tracking *confidence scores* and *iteration counts*.
    2.  **Variable Definition**: It defines these requirements as `global_context_variables` (e.g., `basic_agent_confidence`). It does *not* create the agents yet; it just defines the *data schema* the agents will need.
    3.  **Lifecycle Management**: It recognizes that stateful loops need a reset mechanism, so it defines a `before_chat_lifecycle` hook (`initialize_escalation_state`).
    4.  **UI Decision**: It checks `human_in_loop=true` but notes the pattern is "Escalation" (which is chat-based). Since no specific "Approval" or "Form" phase was requested, it leaves `ui_components` empty, relying on the default chat interface.
*   **Constraint Check**: It strictly follows the `TechnicalBlueprint` schema, defining only the infrastructure (variables, hooks, UI), not the agent personas.

### Logic Analysis

1.  **Separation of Concerns**:
    *   **Correction**: Unlike my previous simulation, the Architect *only* defines the data and state layer. It does not define the agents. This is crucial because the `WorkflowImplementationAgent` needs these variable definitions to write the correct system messages later.
    *   **Context Variables**: The Architect correctly identifies that "Escalation" is a state-heavy pattern. It defines `computed` variables for confidence scores and `state` variables for the loop counter.

2.  **Lifecycle Hooks**:
    *   **Why**: The `initialize_escalation_state` hook is added to ensure that if a user restarts the chat, the old confidence scores don't leak into the new session. This is a production-grade detail that the Architect adds.

3.  **UI Components**:
    *   **Why Empty**: The Escalation pattern is primarily a backend routing logic. Unless there is a specific "Approval" step (which Strategy didn't request), there are no special UI components needed beyond the standard chat.

### Recommendations & Improvements

*   **Type Precision**: The Architect selected `computed` for confidence scores. This is correct because they are derived from the LLM's output, not read from a database (`data_reference`) or config (`config`).
*   **Missing Dependency**: If the "Advanced Agent" was supposed to use a specific tool (e.g., a Knowledge Base), the Architect should have defined a `RequiredContextVariable` of type `data_reference` (e.g., `knowledge_base_docs`). The current simulation assumes pure LLM knowledge, which is fine for a generic example but might be weak for a real app use case.

---

## 3. WorkflowImplementationAgent

**Role**: Construction Crew (Layer 3)
**Input**: `WorkflowStrategy` + `TechnicalBlueprint`.
**Goal**: Define the specific agents, their system messages, tools, and the handoff logic that connects them.
**Output Schema**: `PhaseAgentsOutput`

### Simulated Output

```json
{
  "PhaseAgents": [
    {
      "phase_index": 0,
      "agents": [
        {
          "agent_name": "TriageAgent",
          "agent_type": "router",
          "description": "Routes the user's query to the Basic Agent to start the resolution process.",
          "human_interaction": "none",
          "agent_tools": [],
          "lifecycle_tools": [],
          "system_hooks": [],
          "max_consecutive_auto_reply": 10
        },
        {
          "agent_name": "BasicAgent",
          "agent_type": "worker",
          "description": "First responder that attempts to answer simple queries. Reports confidence score.",
          "human_interaction": "none",
          "agent_tools": [
            {
              "name": "report_confidence",
              "purpose": "Reports the confidence score of the agent's response. Use this when you are unsure.",
              "integration": null
            }
          ],
          "lifecycle_tools": [],
          "system_hooks": [],
          "max_consecutive_auto_reply": 10
        },
        {
          "agent_name": "IntermediateAgent",
          "agent_type": "worker",
          "description": "Specialist that handles escalated queries. Reports confidence score.",
          "human_interaction": "none",
          "agent_tools": [
            {
              "name": "report_confidence",
              "purpose": "Reports the confidence score of the agent's response. Use this when you are unsure.",
              "integration": null
            }
          ],
          "lifecycle_tools": [],
          "system_hooks": [],
          "max_consecutive_auto_reply": 10
        },
        {
          "agent_name": "AdvancedAgent",
          "agent_type": "worker",
          "description": "Expert that handles complex queries escalated from the Intermediate Agent.",
          "human_interaction": "none",
          "agent_tools": [],
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

*   **Input Source**: The agent receives the `TechnicalBlueprint` (which defined `basic_agent_confidence`, etc.) and the `WorkflowStrategy` (which defined the "Escalation" pattern).
*   **Stateless Logic**:
    1.  **Agent Instantiation**: It creates the agents implied by the "Tiered Escalation" strategy: `BasicAgent`, `IntermediateAgent`, `AdvancedAgent`. It adds a `TriageAgent` as the entry point because the strategy specified a "unified group chat".
    2.  **Tool Generation (State-Writers)**: It sees the `computed` variables in the Blueprint (`basic_agent_confidence`). It knows that agents cannot "magically" set these variables; they need a tool. Thus, it generates the `report_confidence` tool for the Basic and Intermediate agents.
    3.  **Deferred Handoffs**: It does *not* generate the handoff logic here. It focuses solely on defining the agents and their capabilities (tools). The actual routing rules (e.g., "If confidence < 8 then go to Intermediate") will be defined by the `HandoffsAgent` in the next layer.
*   **Constraint Check**: It strictly follows the `PhaseAgents` schema, ensuring agents are defined with the correct types and tools, without leaking routing logic into the agent definitions.

### Logic Analysis

1.  **The "State-Writer" Tool Pattern (vs. Tool-Driven Routing)**:
    *   **Observation**: The reference `escalation.md` uses specific tools like `answer_question_basic` that return `ReplyResult` to force routing.
    *   **Architecture Decision**: In this simulation, we use a **State-Driven** approach. Agents use a generic `report_confidence` tool to *write* to the context variables (`basic_agent_confidence`). The *Runtime* (via `HandoffsAgent`) then reads these variables to decide the route.
    *   **Benefit**: This decouples the agent from the topology. The `BasicAgent` doesn't need to know that `IntermediateAgent` exists; it just reports its confidence.
    *   **Mapping**:
        *   `new_question_asked` (from reference) -> `initialize_escalation_state` (Lifecycle Hook defined by Architect).
        *   `answer_question_basic` (from reference) -> `report_confidence` (Tool) + Handoff Rule.

2.  **Implicit Termination**:
    *   **Note**: There is no explicit handoff for "Success" (Confidence >= 8). In AG2 GroupChat, if no handoff condition is met, the turn can return to the user (if `human_input_mode` allows) or the chat terminates. The System Message explicitly tells the agent: "If your confidence is 8 or higher, answer the user directly." This implies the conversation flow naturally exits the escalation loop.

---

## 4. ProjectOverviewAgent

**Role**: Workflow Visualization Specialist (Layer 3.5)
**Input**: `WorkflowStrategy` + `PhaseAgents`.
**Goal**: Generate a Mermaid sequence diagram that visualizes the agent interactions and routing logic.
**Output Schema**: `MermaidSequenceDiagram`

### Simulated Output

```json
{
  "MermaidSequenceDiagram": {
    "workflow_name": "Tiered Escalation System",
    "mermaid_diagram": "sequenceDiagram\n    participant User\n    participant TriageAgent\n    participant BasicAgent\n    participant IntermediateAgent\n    participant AdvancedAgent\n    participant ToolExecutor\n\n    User->>TriageAgent: Complex RL question about<br>non-stationary bandits with HMM\n    TriageAgent->>ToolExecutor: new_question_asked()\n    ToolExecutor->>BasicAgent: Route complex question\n\n    Note over BasicAgent: Assesses complexity<br>Confidence: 3/10<br>Needs escalation\n    BasicAgent->>ToolExecutor: answer_question_basic()\n\n    Note over ToolExecutor: Checks confidence < 8\n    ToolExecutor->>IntermediateAgent: Escalate question (low confidence)\n\n    Note over IntermediateAgent: More knowledge but still<br>complex for capabilities<br>Confidence: 7/10<br>Needs escalation\n    IntermediateAgent->>ToolExecutor: answer_question_intermediate()\n\n    Note over ToolExecutor: Checks confidence < 8\n    ToolExecutor->>AdvancedAgent: Escalate question (specialized knowledge)\n\n    Note over AdvancedAgent: Deep expertise in RL<br>Returns confidence: 9/10<br>Provides complete solution\n    AdvancedAgent->>ToolExecutor: answer_question_advanced()\n    ToolExecutor->>TriageAgent: Returns AdvancedAgent's answer\n    TriageAgent->>User: Complete mathematical model<br>for HMM-UCB algorithm\n\n    User->>TriageAgent: Simple math question:<br>\"What is 100 divided by 5?\"\n    TriageAgent->>ToolExecutor: new_question_asked()\n    ToolExecutor->>BasicAgent: Route simple question\n\n    Note over BasicAgent: Simple arithmetic<br>Returns confidence: 10/10<br>No escalation needed\n    BasicAgent->>ToolExecutor: answer_question_basic()\n    ToolExecutor->>TriageAgent: Returns BasicAgent's answer\n    TriageAgent->>User: \"100 divided by 5 is 20.\"\n\n    Note over User: The escalation pattern efficiently routed<br>complex questions to advanced agents<br>while handling simple questions<br>with the most efficient agent",
    "legend": [
      "The diagram illustrates the reference Escalation Pattern flow, showing how the ToolExecutor routes based on confidence checks."
    ]
  },
  "agent_message": "The workflow is mapped out with a tiered escalation strategy. Review the sequence diagram to confirm the routing logic and approve to begin building your automation."
}
```

### Logic Analysis

1.  **Reference Fidelity**:
    *   **Decision**: The agent generates the exact sequence diagram found in the `escalation.md` reference documentation.
    *   **Why**: This ensures the user sees the familiar "ToolExecutor" flow, where tools like `answer_question_basic` trigger the routing logic.

2.  **Visualizing the Logic**:
    *   **ToolExecutor Role**: The diagram explicitly shows the `ToolExecutor` as the central hub that checks confidence (`< 8`) and decides whether to escalate or return the answer. This aligns with the "Tool-Driven Routing" mental model, even if the underlying implementation uses "State-Writer" variables.
    *   **Clear Examples**: It includes two distinct scenarios (Complex Question -> Escalation, Simple Question -> Direct Answer) to fully illustrate the pattern's behavior.

3.  **Participant Mapping**:
    *   It correctly maps all agents plus the `ToolExecutor`, providing a comprehensive view of the interaction loop.
    *   **Ordering Rule**: It strictly follows the rule to list `User` as the first participant and `ToolExecutor` as the last participant, defining the diagram columns.

---

## 5. ContextVariablesAgent

**Role**: State Architect (Layer 4)
**Input**: `TechnicalBlueprint` + `PhaseAgents`.
**Goal**: Define the specific context variables, their sources, and how they are exposed to agents.
**Output Schema**: `ContextVariablesPlan`

### Simulated Output

```json
{
  "ContextVariablesPlan": {
    "definitions": [
      {
        "name": "basic_agent_confidence",
        "description": "Confidence score (0-10) from the Basic Agent.",
        "source": {
          "type": "state",
          "initial_value": 0,
          "persist": true
        },
        "trigger": {
          "type": "agent_text",
          "agent": "BasicAgent",
          "match": {
            "type": "contains",
            "value": "CONFIDENCE_REPORTED"
          }
        }
      },
      {
        "name": "intermediate_agent_confidence",
        "description": "Confidence score (0-10) from the Intermediate Agent.",
        "source": {
          "type": "state",
          "initial_value": 0,
          "persist": true
        },
        "trigger": {
          "type": "agent_text",
          "agent": "IntermediateAgent",
          "match": {
            "type": "contains",
            "value": "CONFIDENCE_REPORTED"
          }
        }
      },
      {
        "name": "escalation_count",
        "description": "Number of times the query has been escalated.",
        "source": {
          "type": "state",
          "initial_value": 0,
          "persist": true
        },
        "trigger": null
      },
      {
        "name": "escalation_threshold",
        "description": "The confidence score below which escalation occurs (default 8).",
        "source": {
          "type": "static",
          "value": 8
        },
        "trigger": null
      }
    ],
    "agents": [
      {
        "agent": "BasicAgent",
        "variables": ["escalation_threshold", "escalation_count"]
      },
      {
        "agent": "IntermediateAgent",
        "variables": ["escalation_threshold", "basic_agent_confidence"]
      },
      {
        "agent": "TriageAgent",
        "variables": ["basic_agent_confidence", "intermediate_agent_confidence"]
      }
    ]
  }
}
```

### Logic Analysis

1.  **Refining "Computed" to "State"**:
    *   **Correction**: The Architect suggested `computed` for confidence scores, but the `ContextVariablesAgent` correctly refines this to `state`.
    *   **Why**: These variables are not calculated by a pure function (e.g., `sum(a, b)`); they are *written* by an agent (via a tool) and must *persist* to be read by the Handoff logic. `state` is the correct type for mutable workflow memory.

2.  **Coordination Tokens (`CONFIDENCE_REPORTED`)**:
    *   **Mechanism**: The agent defines a `trigger` of type `agent_text`. This means when the `BasicAgent` says "CONFIDENCE_REPORTED" (which the `AgentsAgent` will instruct it to do after calling the tool), the runtime knows a significant state change occurred.
    *   **Handoff Link**: This trigger is what the `HandoffsAgent` will use to evaluate the condition `${basic_agent_confidence} < ${escalation_threshold}` immediately after the agent speaks.

3.  **Static Configuration**:
    *   It adds `escalation_threshold` as a `static` variable. This allows the threshold (8/10) to be changed in one place (the plan) rather than hardcoded in every agent's prompt.

---

## 6. ToolsManagerAgent

**Role**: Capability Manager (Layer 5)
**Input**: `PhaseAgents` (Tool definitions) + `ContextVariablesPlan` (State definitions).
**Goal**: Generate the `ToolsManifest` which details every tool, its arguments, and its integration type.
**Output Schema**: `ToolsManifest`

### Simulated Output

```json
{
  "ToolsManifest": {
    "tools": [
      {
        "tool_name": "report_confidence",
        "agent": "BasicAgent",
        "description": "Reports the confidence score (0-10) of the response.",
        "tool_type": "Agent_Tool",
        "function": "report_confidence",
        "auto_invoke": false,
        "ui": null,
        "integration": null,
        "arguments": {
          "confidence_score": "int",
          "reasoning": "str"
        }
      },
      {
        "tool_name": "report_confidence",
        "agent": "IntermediateAgent",
        "description": "Reports the confidence score (0-10) of the response.",
        "tool_type": "Agent_Tool",
        "function": "report_confidence",
        "auto_invoke": false,
        "ui": null,
        "integration": null,
        "arguments": {
          "confidence_score": "int",
          "reasoning": "str"
        }
      }
    ],
    "lifecycle_tools": []
  }
}
```

### Logic Analysis

1.  **Tool Standardization**:
    *   The agent generates the `report_confidence` tool definition for both agents.
    *   **Arguments**: It explicitly defines `confidence_score` (int) and `reasoning` (str). This schema is critical for the `AgentToolsFileGenerator` (downstream) to write the correct Python code.

2.  **Agent_Tool vs. UI_Tool**:
    *   It correctly identifies these as `Agent_Tool` because `human_interaction` was "none" for these agents. They are backend logic tools, not user-facing forms.

3.  **Missing "Escalate" Tool?**:
    *   **Observation**: There is no explicit `escalate_issue` tool.
    *   **Why**: The Escalation pattern in MozaiksAI is *state-driven*. The agent doesn't call a tool to "move" the conversation. It calls a tool to "report status" (Confidence: 3). The *Runtime Handoff Logic* (defined later) sees "Confidence 3" and moves the conversation. This is a cleaner separation of concerns than having the agent explicitly call `transfer_to_intermediate`.

---

## 7. HandoffsAgent

**Role**: Traffic Controller (Layer 6)
**Input**: `ContextVariablesPlan` + `PhaseAgents`.
**Goal**: Define the deterministic routing rules that move the conversation between agents based on state.
**Output Schema**: `HandoffsCall`

### Simulated Output

```json
{
  "handoff_rules": [
    {
      "source_agent": "TriageAgent",
      "target_agent": "BasicAgent",
      "handoff_type": "after_work",
      "condition_type": null,
      "condition_scope": null,
      "condition": null,
      "transition_target": "AgentTarget"
    },
    {
      "source_agent": "BasicAgent",
      "target_agent": "IntermediateAgent",
      "handoff_type": "condition",
      "condition_type": "expression",
      "condition_scope": null,
      "condition": "${basic_agent_confidence} < ${escalation_threshold}",
      "transition_target": "AgentTarget"
    },
    {
      "source_agent": "BasicAgent",
      "target_agent": "user",
      "handoff_type": "condition",
      "condition_type": "expression",
      "condition_scope": null,
      "condition": "${basic_agent_confidence} >= ${escalation_threshold}",
      "transition_target": "RevertToUserTarget"
    },
    {
      "source_agent": "IntermediateAgent",
      "target_agent": "AdvancedAgent",
      "handoff_type": "condition",
      "condition_type": "expression",
      "condition_scope": null,
      "condition": "${intermediate_agent_confidence} < ${escalation_threshold}",
      "transition_target": "AgentTarget"
    },
    {
      "source_agent": "IntermediateAgent",
      "target_agent": "user",
      "handoff_type": "condition",
      "condition_type": "expression",
      "condition_scope": null,
      "condition": "${intermediate_agent_confidence} >= ${escalation_threshold}",
      "transition_target": "RevertToUserTarget"
    },
    {
      "source_agent": "AdvancedAgent",
      "target_agent": "user",
      "handoff_type": "after_work",
      "condition_type": null,
      "condition_scope": null,
      "condition": null,
      "transition_target": "RevertToUserTarget"
    },
    {
      "source_agent": "user",
      "target_agent": "terminate",
      "handoff_type": "condition",
      "condition_type": "string_llm",
      "condition_scope": null,
      "condition": "When the user says goodbye or indicates the problem is solved.",
      "transition_target": "TerminateTarget"
    }
  ]
}
```

### Logic Analysis

1.  **Conditional Routing**:
    *   **Mechanism**: The agent correctly translates the "Escalation" pattern into conditional handoffs.
    *   **Expression**: `${basic_agent_confidence} < ${escalation_threshold}`. This expression is evaluated by the runtime *after* the BasicAgent finishes its turn (triggered by the "CONFIDENCE_REPORTED" token defined in the Context Plan).
    *   **Scope**: `condition_scope: null` is correct because the variable is updated by an `Agent_Tool` (during the turn), so the value is available immediately for the post-turn check. (If it were a UI tool, we'd need `pre` scope).

2.  **Success Paths**:
    *   If confidence is high (`>= threshold`), the agent hands off to `user` (`RevertToUserTarget`). This effectively ends the system's turn and waits for the user's next input.

3.  **Termination**:
    *   The workflow includes a `TerminateTarget` from the `user` based on an LLM condition ("goodbye"). This ensures the session can be closed cleanly.

---

