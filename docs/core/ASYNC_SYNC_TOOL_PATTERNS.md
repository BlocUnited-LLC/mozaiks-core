# Async/Sync Tool Patterns in MozaiksAI

## Executive Summary

MozaiksAI runtime supports **two distinct tool invocation patterns** with different async/sync requirements based on `auto_tool_mode` configuration. This document defines the architectural rules and provides guidance for all file generator agents.

---

## Core Architectural Rule

**All agents with UI_Tools MUST have `auto_tool_mode: true`**

This is NOT optional—it's an AG2 architectural requirement:
- AG2's native tool calling (`auto_tool_mode: false`) does **NOT** await async functions
- All UI tools are async (they use `await use_ui_tool(...)` for WebSocket interaction)
- AutoToolEventHandler (`auto_tool_mode: true`) has explicit async awaiting logic

---

## Two Tool Patterns

### 1. UI_Tool Pattern (User Interface Tools)

**Configuration:**
```json
{
  "agent": "SomeAgent",
  "auto_tool_mode": true,
  "structured_outputs_required": true
}
```

**Tool Specification:**
```json
{
  "tool_type": "UI_Tool",
  "ui": {
    "component": "ComponentName",
    "mode": "artifact" // or "inline"
  }
}
```

**Code Pattern:**
```python
# ALWAYS async - no exceptions
async def tool_name(
    StructuredOutput: Dict[str, Any],
    agent_message: str,
    **runtime
) -> Dict[str, Any]:
    """
    UI tools are ALWAYS async because they:
    1. Use await use_ui_tool(...) for WebSocket communication
    2. Wait for user interaction (button clicks, form submissions)
    3. Return asynchronous responses from frontend
    """
    from core.workflow.ui_tools import use_ui_tool
    
    payload = {
        "field": StructuredOutput.get("field"),
        "agent_message": agent_message
    }
    
    response = await use_ui_tool(
        tool_id="component_name",
        payload=payload,
        chat_id=runtime.get('chat_id'),
        workflow_name=runtime.get('workflow_name'),
        display='inline'
    )
    
    return response
```

**Runtime Flow:**
1. Agent emits structured output JSON matching registered schema
2. Runtime validates against Pydantic model (e.g., `DownloadRequestCall`)
3. AutoToolEventHandler intercepts, extracts parameters
4. **AutoToolEventHandler properly awaits async tool function**
5. Tool result returned to agent

**Examples:**
- `action_plan.py` (ActionPlanArchitect → ActionPlan component)
- `mermaid_sequence_diagram.py` (ProjectOverviewAgent → MermaidSequenceDiagram component)
- `generate_and_download.py` (DownloadAgent → FileDownloadCenter component)

---

### 2. Agent_Tool Pattern (Backend/Business Logic Tools)

**Configuration Options:**

#### Option A: Manual Calling (auto_tool_mode: false)
```json
{
  "agent": "DataAgent",
  "auto_tool_mode": false,
  "structured_outputs_required": false
}
```

**Tool Specification:**
```json
{
  "tool_type": "Agent_Tool",
  "ui": null
}
```

**Code Pattern (MUST be synchronous):**
```python
# MUST be sync when auto_tool_mode: false
def tool_name(
    param: str,
    **runtime
) -> dict:
    """
    Agent_Tool with auto_tool_mode: false MUST be synchronous.
    
    Why: AG2's ConversableAgent.register_for_llm() calls
    functions without awaiting. Async functions would return
    coroutine objects instead of results.
    """
    context_vars = runtime.get('context_variables', {})
    
    # Synchronous business logic
    result = process_data(param)
    
    return {
        'status': 'success',
        'result': result
    }
```

**Runtime Flow:**
1. Agent decides to call tool based on conversation context
2. Agent emits manual tool call: `call tool_name(param="value")`
3. AG2 native tool execution invokes function synchronously
4. Result returned to agent

---

#### Option B: Auto-Invocation (auto_tool_mode: true)
```json
{
  "agent": "ProcessorAgent",
  "auto_tool_mode": true,
  "structured_outputs_required": true
}
```

**Code Pattern (CAN be async if needed):**
```python
# CAN be async when auto_tool_mode: true
async def async_agent_tool(
    StructuredInput: Dict[str, Any],
    agent_message: str,
    **runtime
) -> dict:
    """
    Agent_Tool with auto_tool_mode: true CAN be async.
    
    AutoToolEventHandler properly awaits async functions.
    Use this when tool needs async operations (HTTP requests,
    database queries with async drivers, etc.)
    """
    context_vars = runtime.get('context_variables', {})
    
    # Async business logic
    result = await fetch_external_api(StructuredInput.get('query'))
    
    return {
        'status': 'success',
        'data': result
    }
```

**Runtime Flow:**
1. Agent emits structured output JSON
2. AutoToolEventHandler intercepts, validates schema
3. **AutoToolEventHandler properly awaits async tool function**
4. Result returned to agent

---

## Decision Matrix

| Scenario | Tool Type | auto_tool_mode | Sync/Async | Why |
|----------|-----------|----------------|------------|-----|
| Tool shows UI component | `UI_Tool` | `true` (required) | **MUST be async** | Uses `await use_ui_tool(...)` |
| Agent uses any UI tool | Any | `true` (required) | N/A | AutoToolEventHandler awaits |
| Backend tool, manual calling | `Agent_Tool` | `false` | **MUST be sync** | AG2 doesn't await |
| Backend tool, auto-invoked | `Agent_Tool` | `true` | **CAN be async** | AutoToolEventHandler awaits |
| Tool needs async I/O (HTTP, DB) | `Agent_Tool` | `true` | **Should be async** | Natural async pattern |
| Simple calculation/validation | `Agent_Tool` | `false` | **Should be sync** | No async needed |

---

## Current Generator Workflow State

### Agents with UI_Tools (auto_tool_mode: true ✅)

1. **ActionPlanArchitect**
   - Tool: `action_plan.py` (UI_Tool)
   - Component: ActionPlan (artifact)
   - Config: ✅ `auto_tool_mode: true`, `structured_outputs_required: true`

2. **ProjectOverviewAgent**
   - Tool: `mermaid_sequence_diagram.py` (UI_Tool)
   - Component: ActionPlan (artifact, diagram enrichment)
   - Config: ✅ `auto_tool_mode: true`, `structured_outputs_required: true`

3. **DownloadAgent**
   - Tool: `generate_and_download.py` (UI_Tool)
   - Component: FileDownloadCenter (inline)
   - Config: ✅ `auto_tool_mode: true`, `structured_outputs_required: true`

### Agents without UI_Tools (auto_tool_mode: false ✅)

4. **InterviewAgent**
   - Tools: None (pure conversation)
   - Config: ✅ `auto_tool_mode: false`

5. **ContextVariablesAgent**
   - Tools: None (emits structured output for validation)
   - Config: ✅ `auto_tool_mode: false`

6. **ToolsManagerAgent**
   - Tools: None (emits structured output for validation)
   - Config: ✅ `auto_tool_mode: false`

7. **UIFileGenerator**
   - Tools: None (emits structured output for validation)
   - Config: ✅ `auto_tool_mode: false`

8. **AgentToolsFileGenerator**
   - Tools: None (emits structured output for validation)
   - Config: ✅ `auto_tool_mode: false`

9. **AgentsAgent**
   - Tools: None (emits structured output for validation)
   - Config: ✅ `auto_tool_mode: false`

10. **StructuredOutputsAgent**
    - Tools: None (emits structured output for validation)
    - Config: ✅ `auto_tool_mode: false`

11. **HookAgent**
    - Tools: None (emits structured output for validation)
    - Config: ✅ `auto_tool_mode: false`

12. **HandoffsAgent**
    - Tools: None (emits structured output for validation)
    - Config: ✅ `auto_tool_mode: false`

13. **OrchestratorAgent**
    - Tools: None (emits structured output for validation)
    - Config: ✅ `auto_tool_mode: false`

---

## File Generator Agent Guidance

All three file generator agents now include standardized async/sync guidance:

### UIFileGenerator
- **Responsibility:** Generate UI_Tool Python/React code
- **Rule:** ALL UI tools MUST be async (use `async def` + `await use_ui_tool(...)`)
- **Agent Requirement:** Owner agent MUST have `auto_tool_mode: true`

### AgentToolsFileGenerator
- **Responsibility:** Generate Agent_Tool Python code
- **Rule:** Check owner agent's `auto_tool_mode` setting:
  - If `false` → Generate **synchronous** function (`def`)
  - If `true` → CAN generate **async** function (`async def`) if business logic requires it
- **Default:** Prefer synchronous unless async I/O needed (HTTP, async DB drivers)

### HookAgent
- **Responsibility:** Generate lifecycle hook code
- **Rule:** Hooks are AG2 message processing callbacks
- **Pattern:** Usually **synchronous** (message dict transformations)
- **Exception:** Can be async if hook needs async I/O (rare)

---

## Common Mistakes to Avoid

❌ **WRONG:** UI_Tool with auto_tool_mode: false
```json
{
  "agent": "DownloadAgent",
  "auto_tool_mode": false  // ❌ Agent has UI tool!
}
```
**Error:** Agent emits manual call → AG2 invokes async function without await → Returns coroutine object

---

❌ **WRONG:** Agent_Tool async function with auto_tool_mode: false
```python
# Agent config: auto_tool_mode: false
async def backend_tool(param: str, **runtime) -> dict:  # ❌ async with false!
    return {'result': await fetch_data(param)}
```
**Error:** AG2 calls without await → Returns coroutine object

---

❌ **WRONG:** Synchronous UI tool
```python
def action_plan(ActionPlan: Dict, agent_message: str, **runtime) -> Dict:  # ❌ Not async!
    response = use_ui_tool(...)  # ❌ Missing await!
    return response
```
**Error:** `use_ui_tool` is async, returns coroutine object without await

---

## Testing Checklist

When validating async/sync patterns:

1. ✅ **Check agent config:**
   - Does agent use UI tools? → `auto_tool_mode: true` required
   - Does agent use only backend tools? → Can use `false` if tools are sync

2. ✅ **Check tool implementation:**
   - Is it UI_Tool? → Must be `async def` + `await use_ui_tool(...)`
   - Is it Agent_Tool with `auto_tool_mode: false`? → Must be `def` (sync)
   - Is it Agent_Tool with `auto_tool_mode: true`? → Can be `async def` if needed

3. ✅ **Check runtime behavior:**
   - Does tool return actual result? ✅
   - Does tool return `<coroutine object ...>`? ❌ (async/sync mismatch)

4. ✅ **Check logs:**
   - AutoToolEventHandler logs show tool invocation? ✅ (auto_tool_mode: true)
   - AG2 native tool logs show execution? ✅ (auto_tool_mode: false)

---

## Migration Example: DownloadAgent

See `DOWNLOAD_AGENT_AUTO_TOOL_MIGRATION.md` for complete case study.

**Problem:**
- DownloadAgent had `auto_tool_mode: false` (manual calling)
- `generate_and_download` was async (used `await use_ui_tool(...)`)
- AG2 called it without awaiting → returned coroutine object

**Solution:**
- Changed DownloadAgent to `auto_tool_mode: true`
- Created `DownloadRequest` structured output schema
- Rewrote agent system message to emit JSON instead of manual calls
- Updated tool signature to accept structured output format
- Result: AutoToolEventHandler properly awaits async function ✅

---

## References

- **AG2 Documentation:** `C:\Users\Owner\Desktop\BlocUnited\BlocUnited Code\MozaiksAI\.venv\Lib\site-packages\autogen`
- **AutoToolEventHandler:** `core/events/auto_tool_handler.py`
- **Working UI Tools:** `workflows/Generator/tools/{action_plan,mermaid_sequence_diagram,generate_and_download}.py`
- **Migration Guide:** `DOWNLOAD_AGENT_AUTO_TOOL_MIGRATION.md`

---

## Quick Reference

```
UI_Tool          → ALWAYS async  → auto_tool_mode: true  (required)
Agent_Tool + false → MUST be sync  → auto_tool_mode: false (AG2 doesn't await)
Agent_Tool + true  → CAN be async  → auto_tool_mode: true  (AutoToolEventHandler awaits)
```

**Golden Rule:** If your tool uses `await use_ui_tool(...)`, the owner agent MUST have `auto_tool_mode: true`. No exceptions.
