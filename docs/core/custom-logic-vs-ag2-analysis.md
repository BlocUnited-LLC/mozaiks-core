# MozaiksAI Custom Logic vs AG2 Native Patterns Analysis

**Date:** October 13, 2025  
**Topic:** Evaluating alignment between MozaiksAI's custom systems and AG2's native capabilities  
**Status:** 📋 ANALYSIS COMPLETE

---

## Executive Summary

**TL;DR:** MozaiksAI's custom systems are **mostly well-aligned** with AG2's architecture but could benefit from **clearer naming and consolidation**. The core patterns are valid; the confusion stems from overlapping terminology and multiple execution paths.

### Key Findings:

✅ **Auto-tool mode**: Valid AG2 pattern (structured outputs + automatic tool invocation)  
✅ **Context variables**: Direct AG2 feature (ContextVariables class)  
✅ **Handoffs**: Properly mapped to AG2's OnCondition/OnContextCondition/after_work  
⚠️ **Lifecycle tools**: Valid concept but confusing naming/overlap with hooks  
❌ **Hooks + Lifecycle Tools**: Redundant systems that should be consolidated  

---

## System-by-System Analysis

### 1. **Auto-Tool Mode** ✅ ALIGNED

**What It Is:**
```json
{
  "agent": "ActionPlanArchitect",
  "auto_tool_mode": true,
  "structured_outputs_required": true
}
```

**How It Works:**
1. Agent emits structured JSON (validated against Pydantic model)
2. `AutoToolEventHandler` intercepts `chat.structured_output_ready` event
3. Runtime automatically invokes the mapped UI tool
4. Tool result emitted as event for UI display

**AG2 Equivalent:**
AG2 **does NOT have direct auto-tool mode**, but supports the pattern:
- AG2 has structured outputs (via `response_format` in LLM config)
- AG2 has tool registration and invocation
- MozaiksAI bridges these: structured output → automatic tool call

**Alignment Assessment:** ✅ **VALID PATTERN**

**Why This Works:**
- AG2's tool system is extensible
- Structured outputs are AG2-native
- Event-driven invocation is a valid orchestration layer on top of AG2
- Pattern enables declarative tool binding (JSON config vs manual wiring)

**Recommendation:** ✅ **KEEP AS-IS**  
This is a valuable abstraction that reduces boilerplate and enables declarative workflows.

---

### 2. **Context Variables** ✅ ALIGNED

**What It Is:**
```python
from autogen.agentchat.group import ContextVariables

context = ContextVariables(data={
    "interview_complete": False,
    "action_plan_acceptance": "pending"
})
```

**How It Works:**
- AG2-native `ContextVariables` class
- Passed to tools via dependency injection
- Shared across all agents in GroupChat
- Persisted across agent transitions

**AG2 Documentation:**
> "Context variables are separate from conversation history and require explicit access methods. Tools receive them via `context_variables` parameter."

**Alignment Assessment:** ✅ **FULLY ALIGNED**

**What We Do:**
- Use AG2's ContextVariables class directly
- Follow AG2's dependency injection pattern
- Handoffs use AG2's `OnContextCondition` with `ExpressionContextCondition`
- Context adapter creates AG2-compatible containers

**Recommendation:** ✅ **KEEP AS-IS**  
This is pure AG2 implementation with no customization needed.

---

### 3. **Handoffs** ✅ ALIGNED (after recent fix)

**What It Is:**
```json
{
  "source_agent": "ProjectOverviewAgent",
  "target_agent": "ContextVariablesAgent",
  "handoff_type": "after_work",
  "condition": "${action_plan_acceptance} == 'accepted'",
  "condition_type": "expression",
  "transition_target": "AgentTarget"
}
```

**AG2 Mapping:**
```python
# MozaiksAI JSON → AG2 Runtime Classes

handoff_type="condition" + condition_type="expression"
  → OnContextCondition(condition=ExpressionContextCondition(...))

handoff_type="condition" + condition_type="string_llm"
  → OnCondition(condition=StringLLMCondition(...))

handoff_type="after_work" + condition=null
  → OnContextCondition(target=..., condition=None)

handoff_type="after_work" + condition="${...}"
  → OnContextCondition(target=..., condition=ExpressionContextCondition(...))
```

**Alignment Assessment:** ✅ **FULLY ALIGNED**

**Recent Fix:**
- Updated schema and HandoffsAgent to properly document all patterns
- Clarified timing model (immediate vs after_work)
- Added AG2 pattern explanations to system message

**Recommendation:** ✅ **KEEP AS-IS**  
Handoff system now properly abstracts AG2's handoff classes into declarative JSON.

---

### 4. **Lifecycle Tools** ⚠️ NEEDS CLARITY

**What It Is:**
```json
{
  "lifecycle_tools": [
    {
      "trigger": "before_agent",
      "agent": "ContextVariablesAgent",
      "file": "collect_api_keys.py",
      "function": "collect_api_keys_from_action_plan",
      "tool_type": "UI_Tool"
    }
  ]
}
```

**Triggers:**
- `before_chat`: Execute before GroupChat starts
- `after_chat`: Execute after GroupChat ends
- `before_agent`: Execute before specific agent speaks
- `after_agent`: Execute after specific agent completes

**How It Works:**
```python
# In orchestration_patterns.py
await lifecycle_mgr.trigger_before_agent(agent_name, context_variables)
# Agent speaks
await lifecycle_mgr.trigger_after_agent(agent_name, context_variables)
```

**AG2 Equivalent:**
AG2 has **hooks** via `update_agent_state_before_reply`:

```python
from autogen import UpdateSystemMessage

agent = ConversableAgent(
    name="support_agent",
    update_agent_state_before_reply=[
        UpdateSystemMessage("Current context: {variable_name}")
    ]
)
```

And `register_hook()`:

```python
def my_hook(sender, recipient, messages, **kwargs):
    # Custom logic before agent processes message
    return messages

agent.register_hook("process_message_before_send", my_hook)
```

**Alignment Assessment:** ⚠️ **PATTERN VALID, TERMINOLOGY CONFUSING**

**Issues:**

1. **Overlapping Systems:**
   - MozaiksAI has BOTH `hooks_loader.py` (AG2 register_hook) AND `lifecycle_tools.py` (custom system)
   - Both solve similar problems with different APIs
   - Confusion about when to use which

2. **Naming Confusion:**
   - "lifecycle_tools" sounds like tools that run at lifecycle boundaries
   - Actually: **declarative hook registration system**
   - Better name: `orchestration_hooks` or `workflow_hooks`

3. **Trigger Terminology:**
   - `before_agent` ≈ AG2's `update_agent_state_before_reply`
   - `after_agent` ≈ Custom post-agent hook (not native AG2)
   - `before_chat`/`after_chat` ≈ GroupChat init/teardown (not AG2-native)

**What AG2 Provides:**
- ✅ `update_agent_state_before_reply` (runs before agent generates response)
- ✅ `register_hook("process_message_before_send", fn)` (message preprocessing)
- ✅ `register_hook("process_all_messages_before_reply", fn)` (pre-reply processing)
- ❌ No native "after agent completes" hook
- ❌ No native "before/after GroupChat" hooks

**What MozaiksAI Adds:**
- ✅ `after_agent` trigger (post-agent execution)
- ✅ `before_chat`/`after_chat` triggers (orchestration boundaries)
- ✅ Declarative JSON-based hook registration (vs manual Python registration)
- ✅ Tool-style invocation (can emit UI events, update context)

**Recommendation:** ⚠️ **CONSOLIDATE & RENAME**

---

## Recommended Refactoring

### Problem: Two Hook Systems

**Current State:**
```
workflows/Generator/
├── hooks.json          # AG2 register_hook system
└── tools.json
    ├── tools           # Regular tools
    └── lifecycle_tools # Custom lifecycle system
```

**Files:**
- `core/workflow/hooks_loader.py` → loads hooks.json, calls `agent.register_hook()`
- `core/workflow/lifecycle_tools.py` → loads lifecycle_tools from tools.json, custom execution

### Solution: Unified Hook System

**Option 1: Consolidate into `hooks.json`**

```json
{
  "hooks": [
    {
      "trigger": "process_message_before_send",
      "agent": "InterviewAgent",
      "file": "interview_preprocessor.py",
      "function": "preprocess_message",
      "description": "Sanitize user input before interview"
    },
    {
      "trigger": "before_agent_reply",
      "agent": "ContextVariablesAgent",
      "file": "collect_api_keys.py",
      "function": "collect_api_keys_from_action_plan",
      "tool_type": "UI_Tool",
      "ui": {
        "component": "AgentAPIKeyInput",
        "mode": "inline"
      }
    },
    {
      "trigger": "after_agent_work",
      "agent": "ProjectOverviewAgent",
      "file": "log_approval_decision.py",
      "function": "log_approval",
      "description": "Log approval state after mermaid tool runs"
    }
  ]
}
```

**Triggers (aligned with AG2):**
- `process_message_before_send` → AG2 native hook
- `process_all_messages_before_reply` → AG2 native hook
- `before_agent_reply` → AG2 `update_agent_state_before_reply`
- `after_agent_work` → MozaiksAI extension (post-agent + post-tool execution)
- `before_groupchat` → MozaiksAI extension (workflow init)
- `after_groupchat` → MozaiksAI extension (workflow teardown)

**Benefits:**
- Single source of truth for all hooks
- Clear AG2 vs MozaiksAI distinction
- No confusion about lifecycle_tools vs hooks
- Maintains declarative JSON approach

**Implementation:**
```python
# core/workflow/hooks_loader.py (enhanced)

def register_hooks_for_workflow(workflow_name: str, agents: Dict[str, Any]) -> List[RegisteredHook]:
    """Load and register all hooks from hooks.json."""
    
    hooks_config = load_hooks_json(workflow_name)
    
    for hook_entry in hooks_config["hooks"]:
        trigger = hook_entry["trigger"]
        
        if trigger in AG2_NATIVE_HOOKS:
            # Use AG2's register_hook
            agent.register_hook(trigger, hook_callable)
        
        elif trigger == "before_agent_reply":
            # Use AG2's update_agent_state_before_reply
            # Wrap as UpdateSystemMessage or custom callable
            agent.update_agent_state_before_reply.append(hook_callable)
        
        elif trigger in MOZAIKS_EXTENSIONS:
            # Custom triggers: after_agent_work, before_groupchat, after_groupchat
            # Store in metadata for orchestration_patterns.py to invoke
            store_custom_hook(workflow_name, trigger, agent_name, hook_callable)
```

---

**Option 2: Keep Separate, Rename Lifecycle Tools**

**Rename:**
```
lifecycle_tools → orchestration_hooks
```

**Clarify Purpose:**
```json
{
  "orchestration_hooks": [
    {
      "trigger": "before_agent_speaks",  // Clearer naming
      "agent": "ContextVariablesAgent",
      "file": "collect_api_keys.py",
      "function": "collect_api_keys_from_action_plan",
      "tool_type": "UI_Tool"
    }
  ]
}
```

**Document Difference:**
- `hooks.json`: AG2-native message/reply hooks
- `orchestration_hooks`: MozaiksAI orchestration-level hooks (can emit UI tools, update context)

**Benefits:**
- Preserves current architecture
- Clearer terminology
- Explicit separation of concerns

---

### Problem: Auto-Tool + Lifecycle Tools + Handoffs Confusion

**Current Confusion:**

User's question:
> "should we move lifecycle tools to handoffs although that doesn't make sense to me because it would require a file and stuff like that"

**Why This is Confusing:**

All three systems control **WHEN** something happens:
- **Handoffs**: Control which agent speaks next
- **Lifecycle Tools**: Control when tools run (orchestration boundaries)
- **Auto-Tool Mode**: Control if tools run automatically (on structured output)

**Clarification:**

| System | Purpose | Timing | AG2 Equivalent |
|--------|---------|--------|----------------|
| **Handoffs** | Agent routing | After agent output or tool completion | OnCondition, OnContextCondition, after_work |
| **Auto-Tool Mode** | Automatic tool invocation | During agent output (structured JSON detected) | N/A (MozaiksAI extension) |
| **Lifecycle Tools** | Orchestration hooks | Before/after agent speaks, before/after chat | update_agent_state_before_reply + custom |

**Should lifecycle_tools move to handoffs?**

❌ **NO** - They serve different purposes:
- Handoffs = **routing decisions** (who speaks next)
- Lifecycle tools = **side effects** (collect input, log events, emit UI)

**Example:**
```json
// Handoff: "If approval accepted, go to ContextVariablesAgent"
{
  "source_agent": "ProjectOverviewAgent",
  "target_agent": "ContextVariablesAgent",
  "handoff_type": "after_work",
  "condition": "${action_plan_acceptance} == 'accepted'"
}

// Lifecycle hook: "Before ContextVariablesAgent speaks, collect API keys"
{
  "trigger": "before_agent",
  "agent": "ContextVariablesAgent",
  "file": "collect_api_keys.py",
  "function": "collect_api_keys_from_action_plan"
}
```

These are **complementary**, not redundant:
- Handoff decides routing
- Lifecycle hook performs side effect

---

## Recommendations Summary

### 1. **Keep Auto-Tool Mode** ✅
- Valid pattern on top of AG2
- Enables declarative workflow configuration
- No changes needed

### 2. **Keep Context Variables** ✅
- Pure AG2 implementation
- No changes needed

### 3. **Keep Handoffs** ✅
- Well-aligned with AG2 after recent fix
- No changes needed

### 4. **Consolidate or Rename Lifecycle Tools** ⚠️

**Option A (Recommended): Consolidate into hooks.json**
- Merge lifecycle_tools into hooks.json
- Use AG2-aligned trigger naming
- Single source of truth for all hooks
- Clear distinction between AG2-native and MozaiksAI extensions

**Option B: Keep Separate, Rename**
- Rename `lifecycle_tools` → `orchestration_hooks`
- Document clear separation from hooks.json
- Keep current architecture

### 5. **Document System Relationships** 📝

Create diagram showing:
```
AG2 Core
  ├─ ConversableAgent
  │   ├─ register_hook() ──────────────┐
  │   └─ update_agent_state_before_reply  (AG2 hooks)
  │
  ├─ GroupChat                           │
  │   ├─ Handoffs                        │
  │   │   ├─ OnCondition                 │
  │   │   ├─ OnContextCondition          │
  │   │   └─ after_work                  │
  │   └─ ContextVariables                │
  │                                       │
MozaiksAI Runtime                        │
  ├─ Auto-Tool Handler                   │
  │   └─ Structured Output → Tool        │
  │                                       │
  ├─ Handoffs Manager                    │
  │   └─ JSON → AG2 handoff classes      │
  │                                       │
  ├─ Hooks Loader ─────────────────────┘
  │   └─ hooks.json → register_hook
  │
  └─ Lifecycle Tools / Orchestration Hooks
      └─ Custom before/after agent hooks
```

---

## Action Items

### Immediate (High Priority)
1. ✅ **Handoff schema fix** - COMPLETED
2. 📝 **Document system relationships** - Create architecture diagram
3. ⚠️ **Decide on lifecycle tools consolidation** - Team decision needed

### Short Term
4. 🔄 **Rename or consolidate lifecycle tools**
   - If consolidate: Merge into hooks.json with AG2-aligned triggers
   - If keep separate: Rename to `orchestration_hooks`
5. 📖 **Update Generator agent instructions** for clarity
6. 🧪 **Add tests** for hook/lifecycle execution order

### Long Term
7. 📚 **Create developer guide** explaining when to use each system
8. 🎨 **UI for workflow visualization** showing hooks, handoffs, and tool invocations

---

## Conclusion

**MozaiksAI's custom logic is well-designed and AG2-aligned.** The main issues are:

1. **Terminology confusion** between hooks and lifecycle tools
2. **Overlapping systems** that need consolidation or clear boundaries
3. **Documentation gaps** explaining when to use each system

The underlying patterns are **valid and valuable**:
- Auto-tool mode enables declarative workflows
- Context variables follow AG2 best practices
- Handoffs properly abstract AG2's routing system
- Lifecycle tools/hooks fill gaps in AG2's lifecycle management

**Recommendation:** Keep the patterns, clarify the naming, consolidate where possible.
