# Human Interaction Conceptual Model
## Alignment across WorkflowStrategyAgent, WorkflowArchitectAgent, and WorkflowImplementationAgent

This document clarifies the conceptual model for human participation in workflows and aligns terminology across three Generator agents.

---

## Three Levels of Human Interaction Definition

### Level 1: Strategic Intent (WorkflowStrategyAgent)
**Field**: `human_in_loop` (boolean per phase)
**Scope**: Phase-level strategic decision
**Meaning**: "Does this phase require ANY human participation at some point?"

**Decision Logic**:
- `true` → Phase involves human context (input), review, approval, or decision-making
- `false` → Phase is fully automated (agents work backend without human involvement)

**Examples**:
- Phase 1: User submits form → `human_in_loop=true`
- Phase 2: Agent processes data → `human_in_loop=false`  
- Phase 3: User approves results → `human_in_loop=true`

**Output**: `WorkflowStrategy.phases[].human_in_loop`

---

### Level 2: Interaction Design (WorkflowArchitectAgent)
**Field**: `ui_components[]` (array of interaction specifications)
**Scope**: Workflow-wide UI interaction catalog
**Meaning**: "WHAT specific interactions happen, WHERE they appear (inline vs artifact), and HOW complex they are"

#### Renamed from "UI Components" → "UI Components"
**Rationale**: "UI Components" implied visual components only. "UI Components" better captures the full spectrum of human-agent coordination points.

**Schema**:
```json
{
  "phase_name": "Phase 1: Planning",
  "agent": "PlanningAgent",
  "tool": "collect_user_requirements",
  "label": "Provide Requirements",
  "component": "RequirementsForm",
  "display": "inline|artifact",
  "ui_pattern": "single_step|two_step_confirmation|multi_step",
  "summary": "User provides project requirements via inline form"
}
```

**Field Meanings**:

**`display`** (WHERE interaction appears):
- `inline` → Embedded in chat flow (lightweight, contextual, doesn't interrupt conversation)
- `artifact` → Separate panel/tray delivery (rich content, reviewed asynchronously)

**`ui_pattern`** (HOW complex the interaction is):
- `single_step` → User acts immediately (submit form, click button, provide input)
- `two_step_confirmation` → User previews content THEN confirms/rejects (approval workflow)
- `multi_step` → Sequential wizard or iterative feedback loop (3+ steps)

**Key Insight**: 
- **Chat interface itself is NOT an ui component** - it's the transport mechanism
- **UI Components are elements WITHIN the chat** (forms, approval cards, result displays)
- **trigger="chat" + human_in_loop=true does NOT automatically require an ui component**
  - Example: User types message → Agent responds with text → No ui component needed
  - UI Components only when agents need STRUCTURED data collection or RICH displays beyond text

**Output**: `TechnicalBlueprint.UI_Components[]` (renamed from ui_components)

---

### Level 3: Agent Implementation (WorkflowImplementationAgent)
**Field**: `human_interaction` (enum per agent)
**Scope**: Agent-level execution contract
**Meaning**: "HOW this specific agent engages with humans during execution"

**Values**:
- `"context"` → Agent collects data from user as part of a Q&A session
- `"approval"` → Agent presents content for user review/decision (approval gates)
- `"none"` → Agent operates autonomously (no human involvement)

**Decision Logic** (based on TechnicalBlueprint.UI_Components):

```
For EACH agent:
  1. Check: Does an UI_Component exist for this phase + agent?
  
  2. If YES:
     - ui_pattern="single_step" → human_interaction="context"
       * User provides data through UI (inline or artifact display)
       * Include operations for data validation and processing
     
     - ui_pattern="two_step_confirmation" → human_interaction="approval"
       * User reviews content then confirms/rejects
       * Include operations for presenting content and handling decisions
     
     - ui_pattern="multi_step" → human_interaction="approval"
       * Multi-step flows typically involve iterative review
       * Include operations for stage progression and revision cycles
  
  3. If NO UI_Component exists:
     - human_interaction="none"
       * Fully automated agent
       * Include operations for automated processing and API integrations
```

**Output**: `PhaseAgents.phase_agents[].agents[].human_interaction`

---

## How the Three Levels Work Together

### Example Flow: Content Review Workflow

#### WorkflowStrategyAgent (Strategic Intent)
```json
{
  "phases": [
    {
      "phase_index": 0,
      "phase_name": "Phase 1: Content Generation",
      "human_in_loop": false,  // ← Fully automated
      "agents_needed": "single"
    },
    {
      "phase_index": 1,
      "phase_name": "Phase 2: Review",
      "human_in_loop": true,  // ← Needs human participation
      "agents_needed": "single"
    }
  ]
}
```

#### WorkflowArchitectAgent (Interaction Design)
```json
{
  "UI_Components": [
    {
      "phase_name": "Phase 2: Review",
      "agent": "ReviewAgent",
      "tool": "submit_approval",
      "label": "Review Draft",
      "component": "ApprovalCard",
      "display": "artifact",  // ← Appears in side panel
      "ui_pattern": "two_step_confirmation",  // ← Preview then approve/reject
      "summary": "User reviews generated content in artifact tray and approves or requests revisions"
    }
  ]
}
```

#### WorkflowImplementationAgent (Agent Implementation)
```json
{
  "phase_agents": [
    {
      "phase_index": 0,
      "agents": [
        {
          "agent_name": "ContentGenerator",
          "human_interaction": "none",  // ← No ui component for this agent
          "agent_tools": [
            {
              "name": "generate_draft",
              "interaction_mode": "none"  // ← Automated tool
            }
          ]
        }
      ]
    },
    {
      "phase_index": 1,
      "agents": [
        {
          "agent_name": "ReviewAgent",
          "human_interaction": "approval",  // ← ui_pattern="two_step_confirmation"
          "agent_tools": [
            {
              "name": "submit_approval",
              "interaction_mode": "artifact"  // ← display="artifact"
            }
          ]
        }
      ]
    }
  ]
}
```

---

## Terminology Alignment Matrix

| Concept | WorkflowStrategy | WorkflowArchitect | WorkflowImplementation |
|---------|------------------|-------------------|------------------------|
| **Does phase need human?** | `human_in_loop: bool` | *(not represented)* | *(not represented)* |
| **What interaction happens?** | *(not represented)* | `UI_Components[]` | *(reads from TechnicalBlueprint)* |
| **Where UI renders?** | *(not represented)* | `display: "inline"\|"artifact"` | `interaction_mode: "inline"\|"artifact"\|"none"` |
| **How complex?** | *(not represented)* | `ui_pattern: "single_step"\|"two_step"\|"multi_step"` | *(informs human_interaction type)* |
| **How agent engages humans?** | *(not represented)* | *(not represented)* | `human_interaction: "context"\|"approval"\|"none"` |

---

## Runtime Responsibilities

**Runtime automatically manages** (agents do NOT implement these):
- `conversation_history` variable (auto-managed for all chat workflows)
- Agent instantiation and tool registration
- Handoff routing and phase transitions
- Context variable lifecycle (initialization, updates, cleanup)
- WebSocket transport for chat interface
- UI component rendering (inline vs artifact)
- Approval gate state management

**Agents define** (declarative specifications only):
- WorkflowStrategy: Which phases need human participation (`human_in_loop`)
- WorkflowArchitect: What interactions happen and where/how they appear (`UI_Components`)
- WorkflowImplementation: How individual agents engage with humans (`human_interaction`)

---

## Common Pitfalls (Now Resolved)

### ❌ Confusing
- "UI Components" implied visual components only
- `human_in_loop` vs `human_interaction` terminology overlap was unclear
- Chat interface treated as a UI Component when it's the transport
- Decision logic based on keywords in interview instead of LLM judgment
- Assumed trigger="chat" + human_in_loop=true always needs ui component

### ✅ Clarity
- "UI Components" captures full human-agent coordination spectrum
- Clear three-level model: strategic intent → interaction design → agent implementation
- Chat interface is transport; UI Components are elements WITHIN chat
- LLM determines need for derived variables based on phase design and pattern
- UI Components only when agents need structured data or rich displays beyond text

---

## Decision Trees

### When to set human_in_loop=true (WorkflowStrategy)
```
Does phase involve:
- "review", "approve", "decide", "context", "feedback" → YES
- "analyze", "process", "generate", "send", "update" (automation) → NO
- monetization_enabled=true AND phase delivers value to end user → YES
```

### When to create UI_Component (WorkflowArchitect)
```
Phase has human_in_loop=true AND:
- User needs to provide STRUCTURED data (not just chat text) → YES (single_step, inline/artifact)
- User needs to review/approve content → YES (two_step_confirmation, artifact preferred)
- User needs multi-step wizard or feedback loop → YES (multi_step, artifact)
- User just types in chat and agent responds with text → NO (no component needed)
```

### How to set human_interaction (WorkflowImplementation)
```
Check TechnicalBlueprint.UI_Components for this phase + agent:

IF UI_Component exists:
  - ui_pattern="single_step" → human_interaction="context"
  - ui_pattern="two_step_confirmation" → human_interaction="approval"
  - ui_pattern="multi_step" → human_interaction="approval"

IF NO UI_Component:
  - human_interaction="none"
```

---

## Summary

**Three agents, three concerns, one cohesive model**:

1. **WorkflowStrategyAgent**: Strategic intent (does phase need ANY human participation?)
2. **WorkflowArchitectAgent**: Interaction design (WHAT interactions, WHERE they appear, HOW complex)
3. **WorkflowImplementationAgent**: Agent implementation (HOW each agent engages humans)

**Key rename**: `ui_components` → `UI_Components` (better captures human-agent coordination spectrum)

**Core principle**: Chat interface = transport. UI Components = structured coordination points WITHIN chat.
