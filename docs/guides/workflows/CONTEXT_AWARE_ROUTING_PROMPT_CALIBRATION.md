# Context-Aware Routing: Prompt Calibration Guide

This document summarizes prompt calibration strategies for `workflows/Generator/agents.json` to robustly support the **Context-Aware Routing** pattern. These adjustments ensure that the Generator agents produce consistent, executable schemas for complex routing scenarios.

## 1. WorkflowArchitectAgent
**Target Section**: `[INSTRUCTIONS]` -> `Step 3 - Create Global Context Variables`

**Calibration Goal**: Ensure the architect defines the necessary state variables that drive the routing logic, distinguishing them from static configuration.

**Suggestions**:
- **Explicit Variable Mandate**: When `pattern` includes "Context-Aware Routing", the prompt should explicitly require these `state` variables:
  - `current_domain` (Type: `state`): Tracks the identified user intent (e.g., "technical", "billing").
  - `routing_confidence` (Type: `state`): A float (0.0-1.0) indicating the Router's certainty.
  - `routed_specialist` (Type: `state`): The name of the selected target agent.
- **Config vs. State**: Ensure `routing_threshold` (e.g., 0.7) is defined as `config` with `trigger_hint: null`, while `routing_confidence` is `state` with a valid `trigger_hint` (e.g., "Set when TriageAgent outputs confidence score").

## 2. ContextVariablesAgent
**Target Section**: `[INSTRUCTIONS]` -> `Step 7 - Design Triggers`

**Calibration Goal**: Ensure the agent designs `agent_text` triggers that accurately detect the Router's output signal.

**Suggestions**:
- **Standardize Routing Signals**: The prompt should encourage a standard output pattern for Router agents to make detection reliable.
- **Trigger Pattern**:
  - **Type**: `agent_text`
  - **Agent**: The Router/Triage Agent.
  - **Match Logic**: Instead of vague "when agent decides," use specific string matching like `{"contains": "ROUTING_DECISION:"}`.
- **Avoid UI Triggers for Backend Routing**: Explicitly instruct that routing decisions made by an LLM (Router) are `agent_text` triggers, NOT `ui_response` triggers (unless a human is manually selecting the route).

## 3. WorkflowImplementationAgent
**Target Section**: `[INSTRUCTIONS]` -> `Step 5 - Build Complete Agent Specifications`

**Calibration Goal**: Ensure the Router Agent is explicitly instructed *how* to signal its decision so the Runtime can detect it.

**Suggestions**:
- **Instruction Injection**: For the agent identified as the "Router" or "Triage" agent, the prompt should force a specific instruction into the `description` field.
- **Example Description**: "Analyze the user's request against available domains. You MUST output your decision in this format: 'ROUTING_DECISION: [domain] (Confidence: [0.0-1.0])'."
- **Tooling**: Ensure the Router agent is NOT given tools to directly call the specialist agents. It must set state (via text output) and let the Runtime handle the handoff.

## 4. HandoffLogicAgent
**Target Section**: `[INSTRUCTIONS]` -> `Step 3 - Design Handoffs`

**Calibration Goal**: Ensure handoffs are conditional based on the state variables defined by the Architect.

**Suggestions**:
- **Conditional Routing**: Explicitly forbid `condition: "always"` for the Router agent's outgoing edges.
- **Logic Mapping**:
  - **From**: Router Agent
  - **To**: Specialist Agent (e.g., BillingAgent)
  - **Condition**: `context.current_domain == 'billing' && context.routing_confidence >= context.routing_threshold`
- **Fallback Route**: Ensure a "Low Confidence" fallback route exists (e.g., to a HumanHandoff agent) if confidence is below threshold.
- **TerminateTarget**: Ensure the final handoff in the flow (or the fallback) correctly specifies a `TerminateTarget` if it ends the session.

## 5. General: `trigger_hint` Discipline
**Target Section**: All Agents handling Context Variables

**Calibration Goal**: Prevent hallucinated triggers for static data.

**Suggestions**:
- **Strict Nulls**: Reinforce the rule that `trigger_hint` MUST be `null` for `config`, `data_reference`, and `external` types.
- **Why**: In routing, we often have static maps (e.g., `domain_map`) or thresholds (`confidence_min`). If the LLM hallucinates a trigger for these, the Runtime might try to "listen" for an event that never happens, potentially blocking the workflow.
