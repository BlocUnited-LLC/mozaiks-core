# Action Plan Data Audit - ProjectOverviewAgent Output Analysis

**Date**: November 7, 2025  
**Purpose**: Comprehensive audit of all data produced by ProjectOverviewAgent and upstream agents, compared against what's consumed by ActionPlan.js UI component.

---

## 🎯 Executive Summary

**Question**: Do we have all available information from ProjectOverviewAgent in the ActionPlan UI?

**Answer**: **NO** - We're missing several critical pieces of data in the UI that are available from upstream agents.

### Missing Data in UI:
1. ✅ **WorkflowAgent-level details** (lifecycle_tools, system_hooks) - **AVAILABLE in normalized data but NOT DISPLAYED**
2. ✅ **Agent descriptions** - **AVAILABLE but displayed only in collapsed accordion**
3. ⚠️ **UI Component summaries** - **PARTIALLY DISPLAYED** (showing label, component, tool, but NOT summary narrative)
4. ❌ **Phase-level lifecycle operations from WorkflowImplementationAgent** - **NOT CAPTURED in action_plan.py**
5. ❌ **Third-party integrations at tool level** - **NOT DISPLAYED in UI**
6. ❌ **Context variable trigger_hint** - **NOT DISPLAYED** (only showing name/type/purpose)

---

## 📊 Complete Data Model - What's Available by ProjectOverviewAgent

By the time ProjectOverviewAgent completes, the following data structures are available in context:

### 1. **WorkflowStrategy** (from WorkflowStrategyAgent)
```json
{
  "workflow_name": "string",
  "workflow_description": "string (trigger → actions → value)",
  "pattern": ["string"],  // e.g., ["Pipeline", "NestedChats"]
  "trigger": "chat|form_submit|schedule|database_condition|webhook",
  "initiated_by": "user|system|external_event",
  "phases": [
    {
      "phase_name": "string (e.g., Phase 1: Discovery)",
      "phase_description": "string",
      "approval_required": "boolean",
      "specialist_domains": ["string"],  // e.g., ["content_writing", "email"]
      "handoff_criteria": "string"
    }
  ]
}
```

### 2. **TechnicalBlueprint** (from WorkflowArchitectAgent)
```json
{
  "global_context_variables": [
    {
      "name": "string (snake_case)",
      "type": "static|derived|environment|database",
      "trigger_hint": "string|null",  // ⚠️ NOT DISPLAYED IN UI
      "purpose": "string"
    }
  ],
  "ui_components": [
    {
      "phase_name": "string",
      "agent": "string (PascalCase)",
      "tool": "string (snake_case)",
      "label": "string (user-facing CTA)",
      "component": "string (React component PascalCase)",
      "display": "inline|artifact",
      "ui_pattern": "single_step|two_step_confirmation|multi_step",
      "summary": "string (<=200 chars)"  // ⚠️ NOT DISPLAYED IN UI
    }
  ],
  "before_chat_lifecycle": {
    "name": "string",
    "purpose": "string",
    "trigger": "before_chat",
    "integration": "string|null"
  } | null,
  "after_chat_lifecycle": {
    "name": "string",
    "purpose": "string",
    "trigger": "after_chat",
    "integration": "string|null"
  } | null
}
```

### 3. **PhaseAgents** (from WorkflowImplementationAgent)
```json
{
  "phase_index": "int (0-based)",
  "agents": [
    {
      "agent_name": "string (PascalCase)",
      "description": "string",
      "agent_tools": [
        {
          "name": "string (snake_case)",
          "integration": "string (PascalCase)|null",  // ⚠️ NOT DISPLAYED
          "purpose": "string"
        }
      ],
      "lifecycle_tools": [
        {
          "name": "string (snake_case)",
          "integration": "string (PascalCase)|null",  // ⚠️ NOT DISPLAYED
          "purpose": "string",
          "trigger": "before_agent|after_agent"
        }
      ],
      "system_hooks": [
        {
          "name": "string (snake_case)",
          "purpose": "string"
        }
      ],
      "human_interaction": "none|context|approval"
    }
  ]
}
```

### 4. **MermaidSequenceDiagram** (from ProjectOverviewAgent)
```json
{
  "workflow_name": "string",
  "mermaid_diagram": "string (starts with 'sequenceDiagram')",
  "legend": ["string"]  // Optional phase legend
}
```

---

## 🔍 What action_plan.py Normalizes

The `action_plan.py` tool processes and normalizes:

### ✅ **CAPTURED:**
1. Workflow metadata (name, description, trigger, initiated_by, pattern)
2. Phases array with agents
3. Agent tools (name, purpose) - **integration field IS normalized but NOT used in UI**
4. Lifecycle tools (name, purpose, trigger) - **integration field IS normalized but NOT used in UI**
5. System hooks (name, purpose)
6. Global context variables (name, type, purpose) - **trigger_hint NOT captured**
7. UI components (label, component, tool, phase_name, agent, display, ui_pattern) - **summary NOT captured**
8. Workflow-level lifecycle operations
9. Mermaid diagram + legend
10. TechnicalBlueprint (global_context_variables, ui_components, before_chat_lifecycle, after_chat_lifecycle)

### ❌ **NOT CAPTURED:**
1. **trigger_hint** from RequiredContextVariable (TechnicalBlueprint)
2. **summary** from WorkflowUIComponent (TechnicalBlueprint)
3. **Phase-level specialist_domains** from WorkflowStrategy
4. **Phase-level handoff_criteria** from WorkflowStrategy

---

## 📺 What ActionPlan.js Displays

### ✅ **CURRENTLY DISPLAYED:**

#### **Navigator Tab:**
- Phase names
- Phase descriptions
- Agent count per phase
- Approval gate count per phase
- Context input count per phase
- "Fully automated" vs user interaction indicators
- Agent names list per phase
- Summary cards (total phases, agents, tools, UI touchpoints)
- Lifecycle hooks summary (before_chat, after_chat)

#### **Technical Tab:**
- **Workflow metadata**: name, initiated_by, trigger_type, pattern, description
- **Lifecycle hooks**: before_chat, after_chat operations (name, purpose, integration if present)
- **Context variables**: name, type, purpose
- **UI components**: label, component, tool, phase_name, agent, display, ui_pattern

#### **Per-Phase Accordions:**
- Phase name/description
- **Per-Agent Cards**:
  - Agent name
  - Agent description
  - Human interaction mode (none/context/approval)
  - Agent tools: name, purpose
  - Lifecycle tools: name, trigger, purpose
  - System hooks: name, purpose

#### **Diagram Tab:**
- Mermaid sequence diagram
- Legend (if provided)

### ❌ **NOT DISPLAYED:**

1. **Integration field for tools** (e.g., "Stripe", "OpenAI", "GitHub")
   - Available: `agent_tools[].integration`, `lifecycle_tools[].integration`
   - Impact: Users don't see which third-party services are being used

2. **UI Component summary narrative** (<=200 chars)
   - Available: `ui_components[].summary`
   - Impact: Users don't see context about what they'll confirm/see at each UI touchpoint

3. **Context variable trigger_hint**
   - Available: `global_context_variables[].trigger_hint`
   - Impact: Users don't understand WHEN/HOW a variable gets set (critical for derived variables)

4. **Phase specialist_domains** (from WorkflowStrategy)
   - Available: `phases[].specialist_domains[]`
   - Impact: Users don't see what expertise areas each phase requires

5. **Phase handoff_criteria** (from WorkflowStrategy)
   - Available: `phases[].handoff_criteria`
   - Impact: Users don't understand what conditions trigger phase transitions

---

## 🎨 Normalized Data Structure in action_plan.py

Here's what gets stored in context variables after normalization:

```python
plan_workflow = {
    "name": "string",
    "initiated_by": "user|system|external_event",
    "trigger": "string (raw trigger text)",
    "trigger_type": "form_submit|chat_start|cron_schedule|webhook|database_condition",
    "pattern": ["string"],  # or single string for backwards compat
    "description": "string",
    "phases": [
        {
            "phase_name": "string",
            "phase_description": "string",
            "agents": [
                {
                    "agent_name": "string",
                    "description": "string",
                    "human_interaction": "none|context|approval",
                    "integrations": ["string"],  # Third-party services (PascalCase)
                    "operations": ["string"],  # Internal logic (snake_case)
                    "lifecycle_tools": [
                        {
                            "name": "string",
                            "trigger": "before_agent|after_agent",
                            "description": "string",
                            "integration": "string|null"  # ⚠️ normalized but not displayed
                        }
                    ],
                    "system_hooks": [
                        {
                            "name": "string",
                            "purpose": "string"
                        }
                    ]
                }
            ]
        }
    ],
    "lifecycle_operations": [
        {
            "name": "string",
            "trigger": "before_chat|after_chat|before_agent|after_agent",
            "target": "AgentName|null",
            "description": "string",
            "integration": "string|null"  # ⚠️ normalized but not displayed
        }
    ],
    "mermaid_flow": "string (diagram text)",
    
    # TechnicalBlueprint fields (merged)
    "global_context_variables": [
        {
            "name": "string",
            "type": "static|derived|environment|database",
            "purpose": "string"
            # ❌ trigger_hint NOT included in normalization
        }
    ],
    "ui_components": [
        {
            "label": "string",
            "component": "string",
            "tool": "string",
            "phase_name": "string",
            "agent": "string",
            "display": "inline|artifact",
            "ui_pattern": "single_step|two_step_confirmation|multi_step"
            # ❌ summary NOT included in normalization
        }
    ],
    "before_chat_lifecycle": {
        "name": "string",
        "purpose": "string",
        "trigger": "before_chat",
        "integration": "string|null"
    } | null,
    "after_chat_lifecycle": {
        "name": "string",
        "purpose": "string",
        "trigger": "after_chat",
        "integration": "string|null"
    } | null
}
```

---

## 🔧 Required Fixes

### 1. **Add integration display for tools** (UI enhancement)
**Where**: ActionPlan.js → ToolSection component  
**What**: Show integration badges next to tool names  
**Example**: `send_email (Sendgrid)`, `charge_payment (Stripe)`

### 2. **Add UI component summary display** (action_plan.py + UI)
**Where**: 
- `action_plan.py` → `_normalize_ui_components()` to capture `summary` field
- ActionPlan.js → ComponentCard to display summary text  
**What**: Show the narrative about what user will see/confirm

### 3. **Add context variable trigger_hint** (action_plan.py + UI)
**Where**:
- `action_plan.py` → `_normalize_global_context_variables()` to capture `trigger_hint`
- ActionPlan.js → ContextVariableCard to display trigger hint  
**What**: Show when/how derived variables get set (e.g., "Set when user approves draft")

### 4. **Consider adding phase metadata** (optional)
**Where**: ActionPlan.js → Phase header cards  
**What**: Show `specialist_domains` and `handoff_criteria` if useful for user understanding

---

## 📝 Example: Missing Data Impact

### **Scenario**: User reviewing a workflow with Stripe payment collection

#### **Current UI Shows**:
```
Phase 2: Payment Collection
  └─ PaymentAgent
      ├─ Tools: charge_customer, send_receipt
      └─ Human Interaction: Approval
```

#### **What's Available But NOT Shown**:
```
Phase 2: Payment Collection
  └─ PaymentAgent
      ├─ Tools: 
          • charge_customer (Stripe) - "Process payment via Stripe API"
          • send_receipt (Sendgrid) - "Email payment confirmation to customer"
      ├─ UI Components:
          • "Confirm Payment" (PaymentConfirmation)
            Summary: "Review payment amount and customer details before charging card"
      ├─ Context Variables:
          • payment_confirmed (derived)
            Trigger: "Set when user approves payment in PaymentConfirmation component"
      └─ Human Interaction: Approval
```

**Impact**: User doesn't know:
1. That Stripe integration is required (cost implications, API setup)
2. That Sendgrid is needed for receipts (additional third-party dependency)
3. What they'll see in the "Confirm Payment" UI (the summary narrative)
4. When the `payment_confirmed` variable gets set (understanding workflow logic)

---

## ✅ Recommendations

### **Priority 1 - Critical for User Understanding:**
1. ✅ Add `integration` badges to all tools (agent_tools, lifecycle_tools)
2. ✅ Add `summary` to UI component cards (normalize in action_plan.py + display in UI)
3. ✅ Add `trigger_hint` to context variable cards (normalize + display)

### **Priority 2 - Nice to Have:**
4. Consider displaying `specialist_domains` in phase headers (helps understand phase expertise)
5. Consider showing `handoff_criteria` between phases (helps understand transitions)

### **Priority 3 - Future Enhancement:**
6. Add "Dependencies" section showing all third-party integrations at workflow level
7. Add expandable "Technical Details" per agent showing system_hooks

---

## 🎯 Next Steps

1. **Audit Complete**: We now have a comprehensive understanding of what's available vs displayed
2. **Decide on Priorities**: Which missing data points are most valuable for users?
3. **Implementation Plan**: 
   - Phase 1: Add integration badges (easiest, high value)
   - Phase 2: Add UI component summaries (requires action_plan.py normalization)
   - Phase 3: Add trigger hints for context variables (requires action_plan.py normalization)

---

## 📚 Reference Links

- **Structured Outputs Schema**: `workflows/Generator/structured_outputs.json`
- **Action Plan Normalization**: `workflows/Generator/tools/action_plan.py`
- **UI Component**: `ChatUI/src/workflows/Generator/components/ActionPlan.js`
- **Agents Config**: `workflows/Generator/agents.json`
