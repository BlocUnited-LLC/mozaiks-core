# Action Plan Agent Outputs - Source of Truth

This document extracts the **actual structured outputs** from `structured_outputs.json` for the 4 agents that produce the Action Plan data. These are the definitive schemas.

---

## Agent Output Chain

```
PatternAgent (selects pattern 1-9)
     ↓
WorkflowStrategyAgent → WorkflowStrategy
     ↓
WorkflowArchitectAgent → TechnicalBlueprint
     ↓
WorkflowImplementationAgent → PhaseAgents[]
     ↓
ProjectOverviewAgent → MermaidSequenceDiagram
```

---

## 1. WorkflowStrategyAgent Output

**Output Model:** `WorkflowStrategyOutput`

```json
{
  "WorkflowStrategy": {
    "workflow_name": "string - Human-readable workflow name in Title Case With Spaces",
    "workflow_description": "string - Description: When [TRIGGER], workflow [ACTIONS], resulting in [VALUE]",
    "human_in_loop": "boolean - Global flag: Does this workflow involve ANY human interaction?",
    "pattern": ["string[] - List of AG2 orchestration patterns (e.g., ['sequential', 'nested_chats'])"],
    "trigger": "literal: 'chat' | 'form_submit' | 'schedule' | 'database_condition' | 'webhook'",
    "initiated_by": "literal: 'user' | 'system' | 'external_event'",
    "phases": [
      {
        "phase_name": "string - Phase name in format 'Phase N: Strategic Purpose'",
        "phase_index": "int - Zero-based index (0 = Phase 1, 1 = Phase 2, etc.)",
        "phase_description": "string - Strategic description of what happens in this phase and why",
        "agents_needed": "string - Agent coordination pattern: 'single' | 'parallel' | 'sequential'"
      }
    ]
  }
}
```

### Issues Identified:
1. **`pattern` field is a list of strings** - but downstream it expects pattern names from PatternAgent (1-9). This is confusing.
2. **`agents_needed` per phase** - redundant with pattern selection. If pattern is "Pipeline", why do we need "sequential" per phase?

---

## 2. WorkflowArchitectAgent Output

**Output Model:** `TechnicalBlueprintOutput`

```json
{
  "TechnicalBlueprint": {
    "global_context_variables": [
      {
        "name": "string - Variable name (snake_case, e.g., 'routing_started')",
        "type": "literal: 'config' | 'data_reference' | 'data_entity' | 'computed' | 'state' | 'external'",
        "purpose": "string - What this variable tracks and why it's needed (1-2 sentences)",
        "trigger_hint": "string | null - When/how this variable gets set"
      }
    ],
    "ui_components": [
      {
        "phase_name": "string - Phase name where this UI tool appears",
        "agent": "string - PascalCase agent that emits the UI tool",
        "tool": "string - snake_case tool function name",
        "label": "string - User-facing label or CTA text",
        "component": "string - React component name (PascalCase)",
        "display": "string - 'inline' | 'artifact'",
        "ui_pattern": "string - 'single_step' | 'two_step_confirmation' | 'multi_step'",
        "summary": "string - <=200 char narrative explaining user interaction"
      }
    ],
    "before_chat_lifecycle": {
      "name": "string - Lifecycle tool name",
      "purpose": "string - What it accomplishes",
      "trigger": "'before_chat'",
      "integration": "string | null - Third-party service"
    } | null,
    "after_chat_lifecycle": {
      "name": "string - Lifecycle tool name", 
      "purpose": "string - What it accomplishes",
      "trigger": "'after_chat'",
      "integration": "string | null - Third-party service"
    } | null,
    "workflow_dependencies": {
      "required_workflows": [
        { "workflow": "string", "status": "string" }
      ],
      "required_context_vars": ["string[]"],
      "required_artifacts": [
        { "artifact_type": "string", "source_workflow": "string" }
      ]
    } | null
  }
}
```

### Notes:
- `global_context_variables` uses `RequiredContextVariable` which is a **simplified** version (no full source details)
- `ui_components` uses `WorkflowUIComponent` - comprehensive UI planning
- Lifecycle tools are optional (null when not needed)

---

## 3. WorkflowImplementationAgent Output

**Output Model:** `PhaseAgentsOutput`

```json
{
  "PhaseAgents": [
    {
      "phase_index": "int - Zero-based index matching WorkflowStrategy.phases",
      "agents": [
        {
          "agent_name": "string - Agent identifier (unique within workflow)",
          "agent_type": "literal: 'router' | 'worker' | 'evaluator' | 'orchestrator'",
          "description": "string - Short human-readable description",
          "agent_tools": [
            {
              "name": "string - Tool function name (snake_case)",
              "integration": "string | null - Third-party service (PascalCase)",
              "purpose": "string - What tool does (<=140 chars)"
            }
          ],
          "lifecycle_tools": [
            {
              "name": "string - Lifecycle tool function name",
              "integration": "string | null - Third-party service",
              "purpose": "string - What it does (<=140 chars)",
              "trigger": "'before_chat' | 'after_chat' | 'before_agent' | 'after_agent'"
            }
          ],
          "system_hooks": [
            {
              "name": "string - Hook identifier (e.g., 'update_agent_state')",
              "purpose": "string - Why agent needs this hook"
            }
          ],
          "human_interaction": "literal: 'none' | 'context' | 'approval'",
          "max_consecutive_auto_reply": "int - Derived from human_interaction"
        }
      ]
    }
  ]
}
```

### Notes:
- Array length MUST match `WorkflowStrategy.phases` count
- Each phase has at least one agent
- `agent_type` values: router (control flow), worker (execution), evaluator (QA/decision), orchestrator (sub-team lead)

---

## 4. ProjectOverviewAgent Output

**Output Model:** `MermaidSequenceDiagramOutput`

```json
{
  "MermaidSequenceDiagram": {
    "workflow_name": "string - Workflow name this diagram represents",
    "mermaid_diagram": "string - Complete Mermaid sequence diagram text, starting with 'sequenceDiagram'",
    "legend": ["string[] - Optional phase legend entries (P1: Phase Name)"]
  },
  "agent_message": "string - Concise invitation (<=140 chars) asking user to review diagram"
}
```

---

## Critical Observations

### 1. `agents_needed` is Redundant/Confusing

| Pattern | Expected Phase Coordination | `agents_needed` Adds? |
|---------|----------------------------|----------------------|
| Pipeline | All phases are sequential | Already implied by pattern |
| Star | Hub + parallel spokes | "nested" doesn't capture this |
| Hierarchical | Manager→Worker nesting | "nested" is too vague |
| Feedback Loop | Loop until approved | Phase count is dynamic |

**Recommendation:** Remove `agents_needed` entirely. The **pattern** already defines coordination style.

### 2. `pattern` Field in WorkflowStrategy is Wrong Type

Current: `"pattern": ["string[]"]` - a list of strings like `['sequential', 'nested_chats']`

But PatternAgent outputs a pack-aware `PatternSelection` with per-workflow `pattern_id`/`pattern_name` (and `is_multi_workflow`, `pack_name`, etc.).

**These don't match!** 

The `pattern` field in WorkflowStrategy should be:
```json
"pattern": "literal: 'ContextAwareRouting' | 'Escalation' | 'FeedbackLoop' | 'Hierarchical' | 'Organic' | 'Pipeline' | 'Redundant' | 'Star' | 'TriageWithTasks'"
```

### 3. What Actually Flows Downstream?

For downstream agents (ContextVariablesAgent, HandoffsAgent, AgentsAgent, etc.), they need:

| From | They Need |
|------|-----------|
| WorkflowStrategy | `workflow_name`, `phases[]`, `trigger`, `human_in_loop` |
| TechnicalBlueprint | `global_context_variables[]`, `ui_components[]`, lifecycle hooks |
| PhaseAgents | Per-phase agent specs with tools and hooks |

### 4. What's Missing from Current Structure?

1. **Pattern-specific topology rules** - How agents connect (routing table, escalation ladder, feedback loop)
2. **Handoff hints** - Which agent hands to which
3. **Context variable flow** - Which vars flow to which agents

---

## Proposed Simplifications

### Option A: Remove `agents_needed` Entirely
Let the pattern (1-9) define coordination. Each pattern has known topology:
- Pipeline: A→B→C→D (sequential)
- Star: Hub↔Spokes (hub coordinates)
- Feedback Loop: Creator↔Reviewer (loop until done)

### Option B: Replace with `topology` Object
```json
"phases": [
  {
    "phase_name": "Phase 1: Intake",
    "phase_index": 0,
    "phase_description": "...",
    "topology": {
      "entry_agent": "IntakeAgent",
      "exit_agent": "IntakeAgent",
      "internal_coordination": null
    }
  }
]
```

### Option C: Move Coordination to TechnicalBlueprint
Let WorkflowArchitect figure out the actual wiring based on pattern.

---

## Next Steps

1. **Decide on `agents_needed`** - Remove, replace, or keep?
2. **Align `pattern` field** - Match PatternAgent output format
3. **Create centralized pattern examples** - One JSON per pattern with all agent guidance
4. **Update structured_outputs.json** - Implement chosen simplifications
5. **Update agents.json prompts** - Reflect new schema
