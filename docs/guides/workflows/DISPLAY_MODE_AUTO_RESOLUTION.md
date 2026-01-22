# Display Mode Auto-Resolution

> ⚠️ **PARTIALLY OUTDATED**: Examples reference legacy agent name "ActionPlanArchitect".
> For current agent registry, see: `workflows/Generator/structured_outputs.json` (registry section)
> The display mode auto-resolution concept and implementation remain valid.

## Overview

The MozaiksAI runtime now supports **automatic display mode resolution** for UI tools, eliminating redundancy between tool configuration (`tools.json`) and tool function implementations.

## Problem Solved

Previously, display mode information was duplicated in two places:

1. **In `tools.json`**: Each UI tool declared `"mode": "inline"` or `"mode": "artifact"`
2. **In tool functions**: Each call to `use_ui_tool()` explicitly passed `display="inline"` or `display="artifact"`

This redundancy:
- Increased cognitive load on agent generators (UIFileGenerator)
- Created potential for configuration mismatches
- Added unnecessary boilerplate to tool implementations

## Solution

The runtime now automatically resolves the display mode from `tools.json` configuration when not explicitly provided.

### Configuration-Driven Display Mode

**Before:**
```python
# Tool function had to know and specify display mode
response = await use_ui_tool(
    "AgentAPIKeyInput",
    payload,
    chat_id=chat_id,
    workflow_name=workflow_name,
    display="inline",  # ← Redundant with tools.json
)
```

**After:**
```python
# Display mode auto-resolved from tools.json
response = await use_ui_tool(
    "AgentAPIKeyInput",
    payload,
    chat_id=chat_id,
    workflow_name=workflow_name,
    # display parameter omitted - resolved automatically
)
```

### Tools.json Configuration

The `tools.json` file remains the single source of truth for display mode:

```json
{
  "tools": [
    {
      "agent": "ActionPlanArchitect",
      "file": "action_plan.py",
      "function": "action_plan",
      "description": "Display action plan in artifact panel",
      "tool_type": "UI_Tool",
      "ui": {
        "component": "ActionPlan",
        "mode": "artifact"  // ← Single source of truth
      }
    }
  ],
  "lifecycle_tools": [
    {
      "trigger": "before_agent",
      "agent": "ContextVariablesAgent",
      "file": "collect_api_keys.py",
      "function": "collect_api_keys_from_action_plan",
      "description": "Collect API keys for third-party services",
      "tool_type": "UI_Tool",
      "ui": {
        "component": "AgentAPIKeyInput",
        "mode": "inline"  // ← Lifecycle tools also support UI config
      }
    }
  ]
}
```

## Implementation Details

### Runtime Changes

**1. `use_ui_tool()` function** (`core/workflow/ui_tools.py`):
- `display` parameter is now **Optional[str]** (defaults to `None`)
- When `None`, the function queries `workflow_manager` for the tool's configured mode
- Falls back to `"inline"` if no configuration is found
- Explicit `display` values still work (backward compatible)

**2. `workflow_manager`** (`core/workflow/workflow_manager.py`):
- Now processes both `tools` and `lifecycle_tools` sections
- Extracts and caches UI configuration (`component` and `mode`)
- `get_ui_tool_record()` provides lookup by tool_id or function name

### Resolution Logic

```python
# Auto-resolve display mode from workflow_manager if not explicitly provided
resolved_display = display
if resolved_display is None:
    try:
        tool_record = workflow_manager.get_ui_tool_record(tool_id)
        if tool_record:
            resolved_display = tool_record.get('mode', 'inline')
            logger.debug(f"🔍 Auto-resolved display mode for '{tool_id}': {resolved_display}")
        else:
            resolved_display = 'inline'  # fallback default
    except Exception as e:
        resolved_display = 'inline'  # fallback on error
        logger.warning(f"⚠️ Failed to resolve display mode: {e}")
```

## Benefits

### For Agent Generators (UIFileGenerator)

✅ **Less code to generate**: Tool stubs no longer need to include `display` parameter  
✅ **Reduced cognitive load**: No need to track display mode in multiple places  
✅ **Single source of truth**: `tools.json` is the definitive configuration  
✅ **Fewer errors**: No risk of mismatched display modes between config and code

### For Runtime

✅ **Declarative configuration**: Display mode is part of tool metadata  
✅ **Backward compatible**: Explicit `display` parameters still work  
✅ **Centralized management**: Tool registry handles all UI configuration  
✅ **Lifecycle tool support**: Even lifecycle hooks can have UI components

### For Workflow Authors

✅ **Configuration-driven**: Change display mode by editing `tools.json` only  
✅ **Clear contracts**: Tool behavior defined in one place  
✅ **Easier maintenance**: No need to update multiple files for display changes

## Migration Guide

### For Existing Tools

**Option 1: Remove explicit display (recommended)**
```python
# Before
response = await use_ui_tool(tool_id, payload, chat_id=id, workflow_name=wf, display="inline")

# After
response = await use_ui_tool(tool_id, payload, chat_id=id, workflow_name=wf)
```

**Option 2: Keep explicit display (backward compatible)**
```python
# Still works - explicit value overrides auto-resolution
response = await use_ui_tool(tool_id, payload, chat_id=id, workflow_name=wf, display="artifact")
```

### For New Tools

1. **Configure in `tools.json`:**
```json
{
  "agent": "YourAgent",
  "file": "your_tool.py",
  "function": "your_function",
  "tool_type": "UI_Tool",
  "ui": {
    "component": "YourComponent",
    "mode": "inline"  // or "artifact"
  }
}
```

2. **Call `use_ui_tool()` without display:**
```python
response = await use_ui_tool(
    "YourComponent",
    payload,
    chat_id=chat_id,
    workflow_name=workflow_name
)
```

## Testing

All Generator workflow tools have been updated:
- ✅ `request_api_key.py` - inline mode auto-resolved
- ✅ `generate_and_download.py` - inline mode auto-resolved
- ✅ `action_plan.py` - artifact mode auto-resolved
- ✅ `mermaid_sequence_diagram.py` - artifact mode auto-resolved

Lifecycle tools now support UI configuration:
- ✅ `collect_api_keys.py` - AgentAPIKeyInput component, inline mode

## Future Enhancements

This change opens up possibilities for:
- **Component-level configuration**: Other UI properties in `tools.json`
- **Dynamic mode switching**: Runtime could adjust display based on context
- **Theme/styling configuration**: Extend `ui` block with appearance settings
- **Conditional display**: Rule-based display mode selection

## Summary

By making display mode resolution automatic and configuration-driven, we've:
- Reduced boilerplate in tool implementations
- Eliminated redundancy between config and code
- Made the system more maintainable and less error-prone
- Simplified the job of agent generators

This is a **non-breaking change** that makes the platform more declarative and easier to work with while maintaining full backward compatibility.
