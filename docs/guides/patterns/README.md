# Pattern Examples (AgentGenerator)

This folder contains **pattern teaching examples** for the AgentGenerator workflow.

Each file is a **multi-document YAML** stream (documents separated by `---`). Each document’s top-level key is the **semantic wrapper key** produced by a Generator agent (e.g., `PatternSelection`, `WorkflowStrategy`, `StateArchitecture`, …).

These examples serve two purposes:
1. Human reference when authoring/validating patterns
2. Prompt injection source for `workflows/AgentGenerator/tools/update_agent_state_pattern.py` (it extracts specific sections like `WorkflowStrategy` for the active pattern)

---

## File Naming

- `pattern_<id>_<slug>.yaml`
  - Example: `pattern_6_pipeline.yaml`

`<id>` MUST match the AG2 pattern ID (1–9) in `docs/pattern_guidance.md`.

---

## Required Sections (minimum)

At minimum, pattern examples should include:

- `PatternSelection` (PatternAgent output)
- `WorkflowStrategy` (WorkflowStrategyAgent output)

Most examples in this repo also include:
- `StateArchitecture`
- `UXArchitecture`
- `AgentRoster`
- (optionally) tools/handoffs/ui/orchestrator outputs depending on the generator pipeline

---

## How PatternAgent Decides “Single Workflow vs Pack”

PatternAgent’s decision is **design-time** (generator-time), not runtime:

- Inputs (context variables):
  - `concept_overview` (seeded project context)
  - `monetization_enabled`
  - `macro_workflow_graph` (loaded from `workflows/_pack/workflow_graph.json`)
- Input (conversation):
  - InterviewAgent transcript (user goals + structural signals)

Outputs (structured):
- `PatternSelection.is_multi_workflow`
- `PatternSelection.pack_name`
- `PatternSelection.workflows[]` (each with `pattern_id` + `pattern_name`)

PatternAgent should set `is_multi_workflow: true` only when workflows have **distinct lifecycles/interaction models/HITL** that justify separate chat sessions. Otherwise, keep a single workflow and represent internal stages as `modules` in `WorkflowStrategy`.

---

## How PatternAgent Uses `macro_workflow_graph`

`macro_workflow_graph` is a **read-only config** injected into PatternAgent. It is used to:
- align decomposition choices with the runtime’s **journeys + gates** policy
- avoid proposing packs that contradict required gating

Important:
- PatternAgent does **not** enforce gates.
- The runtime enforces gates when starting/resuming workflows; the graph is the contract.

---

## Multi-Workflow Packs and `current_workflow_index`

When `is_multi_workflow: true`, downstream generation loops over `PatternSelection.workflows[]`.

During generation, the runtime sets `current_workflow_index` so downstream agents (and pattern guidance injection) use the **correct per-workflow pattern**:
- `PatternSelection.workflows[current_workflow_index].pattern_id`
- `PatternSelection.workflows[current_workflow_index].pattern_name`

