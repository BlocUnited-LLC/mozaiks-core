# Auto Tool Mode Automation Implementation

## Overview
This document describes the automated `auto_tool_mode` determination system implemented in the Generator workflow to eliminate human error and ensure UI_Tools always use `auto_tool_mode=true`.

## Problem Solved
**Original Issue**: File generators (UIFileGenerator, AgentToolsFileGenerator) needed to know an agent's `auto_tool_mode` setting to generate correct async/sync code, but `auto_tool_mode` was determined by AgentsAgent AFTER file generators had already run (execution order mismatch).

**Solution**: Automated `auto_tool_mode` determination based on tool ownership, with ToolsManagerAgent setting the value early (step 5) so file generators can read it (steps 6-7).

## Architecture Decision: Option 1 (Automation)

### Rule
```
auto_tool_mode = (agent owns ≥1 UI_Tool)
```

- **If agent owns ANY UI_Tool** → `auto_tool_mode = true` (REQUIRED for async UI tools)
- **If agent owns ONLY Agent_Tools** → `auto_tool_mode = false` (default for sync backend tools)
- **If agent owns NO tools** → `auto_tool_mode = false` (no tools to invoke)

### Why This Works
1. **UI_Tool technical requirement**: ALL UI_Tool functions are async (they use `await use_ui_tool(...)`)
2. **AG2 limitation**: AG2's native calling (auto_tool_mode=false) does NOT await async functions
3. **AutoToolEventHandler solution**: auto_tool_mode=true properly awaits async UI tools
4. **99% coverage**: Most workflows don't need async Agent_Tools, so this optimizes for the common case
5. **Single source of truth**: tools.json becomes authoritative for auto_tool_mode (no duplication in agents.json)

## Implementation Details

### 1. ToolsManagerAgent (Step 5 - Determines auto_tool_mode)

**NEW SECTION ADDED**: `[AUTO_TOOL_MODE DETERMINATION]`

```markdown
[AUTO_TOOL_MODE DETERMINATION] (CRITICAL - AUTOMATED RULE)
You MUST determine auto_tool_mode for each agent automatically based on tool ownership:

**RULE: auto_tool_mode = (agent owns ≥1 UI_Tool)**

- If agent owns ANY tool with tool_type="UI_Tool" → auto_tool_mode MUST be true
- If agent owns ONLY tools with tool_type="Agent_Tool" → auto_tool_mode = false
- If agent owns NO tools → auto_tool_mode = false

**OUTPUT REQUIREMENT:**
Your tools.json manifest MUST include an `agent_modes` object:
```json
"agent_modes": {
  "ActionPlanArchitect": true,  // Has UI_Tool(s)
  "ToolsManagerAgent": false     // Has only Agent_Tool(s) or no tools
}
```
```

**What ToolsManagerAgent Does**:
1. Scans all tools in the manifest
2. Groups tools by owning agent
3. For each agent, checks if ANY tool has `tool_type="UI_Tool"`
4. Outputs `agent_modes` object mapping agent name → boolean auto_tool_mode

### 2. UIFileGenerator & AgentToolsFileGenerator (Steps 6-7 - Read auto_tool_mode)

**UPDATED SECTION**: `[ASYNC/SYNC DESIGN RULES]` now includes reading instructions

```markdown
**[CRITICAL - READ AUTO_TOOL_MODE FROM TOOLS.JSON]**
BEFORE generating ANY UI_Tool code:
1. Read the `agent_modes` object from tools.json
2. Look up the target agent's auto_tool_mode value
3. **UI_Tools REQUIRE auto_tool_mode=true** - verify this before generation
4. If mismatch detected, raise error (UI_Tool with auto_tool_mode=false is invalid)
```

**What File Generators Do**:
1. Locate tools.json manifest in conversation (output from ToolsManagerAgent)
2. Extract `agent_modes` object
3. For each tool they're generating, look up the owning agent's auto_tool_mode
4. Generate async code if auto_tool_mode=true, sync code if auto_tool_mode=false
5. Validate UI_Tools always have auto_tool_mode=true (error if mismatch)

### 3. AgentsAgent (Step 8 - Reads auto_tool_mode from tools.json)

**NEW SECTION ADDED**: `[AUTO_TOOL_MODE READING]`

```markdown
[AUTO_TOOL_MODE READING] (CRITICAL - READ FROM TOOLS.JSON)
You MUST read auto_tool_mode from the tools manifest agent_modes object:

**READING agent_modes:**
1. Locate tools.json manifest in conversation (output from ToolsManagerAgent)
2. Find the `agent_modes` object
3. For EACH agent you configure, look up their auto_tool_mode from this mapping
4. If agent not in agent_modes → default to auto_tool_mode=false

**WHY YOU READ (not determine):**
- ToolsManagerAgent ALREADY determined auto_tool_mode based on tool ownership
- Single source of truth prevents inconsistencies
- Never re-calculate auto_tool_mode yourself - always read from agent_modes
```

**UPDATED GUIDELINES**:
- Line 9: Changed from "Determine auto_tool_mode strictly from the Tool Registry" to "Read auto_tool_mode from tools.json agent_modes object"

**UPDATED INSTRUCTIONS**:
- Step 2: Now includes "Extract agent_modes object mapping agent names to auto_tool_mode boolean values"
- Step 4: Changed from "auto_tool_mode: true if agent owns UI_Tool" to "auto_tool_mode: Read from agent_modes object"

**What AgentsAgent Does**:
1. Locates tools.json in conversation
2. Extracts `agent_modes` object
3. For each agent definition being generated, looks up auto_tool_mode from agent_modes
4. Uses that value directly in the output agents.json (no re-calculation)

## Execution Flow

```
Step 4: ContextVariablesAgent
  ↓
Step 5: ToolsManagerAgent ✅ NEW: Determines auto_tool_mode → outputs agent_modes
  ↓
Step 6: UIFileGenerator ✅ UPDATED: Reads agent_modes → generates correct async/sync UI tool code
  ↓
Step 7: AgentToolsFileGenerator ✅ UPDATED: Reads agent_modes → generates correct async/sync agent tool code
  ↓
Step 8: AgentsAgent ✅ UPDATED: Reads agent_modes → uses value in agents.json output
  ↓
Step 9-13: Remaining agents (StructuredOutputsAgent, HookAgent, HandoffsAgent, OrchestratorAgent, DownloadAgent)
```

## Example Output Format

### ToolsManagerAgent Output (tools.json)
```json
{
  "tools": [
    {
      "agent": "ActionPlanArchitect",
      "file": "action_plan.py",
      "function": "action_plan",
      "description": "Render the Action Plan artifact for user review",
      "tool_type": "UI_Tool",
      "ui": {
        "component": "ActionPlan",
        "mode": "artifact"
      }
    },
    {
      "agent": "ToolsManagerAgent",
      "file": "generate_tools_manifest.py",
      "function": "generate_tools_manifest",
      "description": "Generate tools manifest from action plan",
      "tool_type": "Agent_Tool",
      "ui": null
    }
  ],
  "agent_modes": {
    "ActionPlanArchitect": true,     // Has UI_Tool (action_plan.py)
    "ProjectOverviewAgent": true,    // Has UI_Tool (mermaid_sequence_diagram.py)
    "ToolsManagerAgent": false,      // Has only Agent_Tool
    "UIFileGenerator": false,        // No tools
    "AgentToolsFileGenerator": false, // No tools
    "AgentsAgent": false,            // No tools
    "DownloadAgent": true            // Has UI_Tool (generate_and_download.py)
  }
}
```

### UIFileGenerator Reads agent_modes
```python
# UIFileGenerator internally:
# 1. Read tools.json from conversation
# 2. Extract agent_modes
agent_modes = {
    "ActionPlanArchitect": True,
    "ToolsManagerAgent": False,
    ...
}

# 3. For each UI_Tool being generated:
tool = {"agent": "ActionPlanArchitect", "tool_type": "UI_Tool", ...}
auto_tool_mode = agent_modes.get(tool["agent"], False)

# 4. Validate UI_Tool requires auto_tool_mode=true
if tool["tool_type"] == "UI_Tool" and not auto_tool_mode:
    raise ValueError(f"UI_Tool {tool['function']} requires auto_tool_mode=true")

# 5. Generate async code (UI_Tools are always async)
py_content = f"""
async def {tool['function']}(*, payload: dict, **runtime) -> dict:
    return await use_ui_tool("{tool['ui']['component']}", payload, ...)
"""
```

### AgentsAgent Reads agent_modes
```python
# AgentsAgent internally:
# 1. Read tools.json from conversation
# 2. Extract agent_modes
agent_modes = {
    "ActionPlanArchitect": True,
    "ToolsManagerAgent": False,
    ...
}

# 3. For each agent definition:
for agent_name in action_plan_agents:
    auto_tool_mode = agent_modes.get(agent_name, False)
    
    agent_definition = {
        "name": agent_name,
        "display_name": ...,
        "system_message": ...,
        "max_consecutive_auto_reply": ...,
        "auto_tool_mode": auto_tool_mode,  # ✅ Read from agent_modes
        "structured_outputs_required": ...
    }
```

## Benefits

### 1. **Eliminates Human Error**
- No manual configuration of auto_tool_mode
- UI_Tool → auto_tool_mode=true is enforced automatically
- Impossible to misconfigure (system prevents invalid states)

### 2. **Single Source of Truth**
- tools.json `agent_modes` object is authoritative
- No duplication between tools.json and agents.json
- All consumers read from same source

### 3. **Early Availability**
- auto_tool_mode determined in step 5 (ToolsManagerAgent)
- Available to file generators in steps 6-7
- Available to AgentsAgent in step 8
- Fixes sequencing problem (was determined in step 8, needed in steps 6-7)

### 4. **Validation Built-In**
- UIFileGenerator validates UI_Tools have auto_tool_mode=true
- Runtime errors if mismatch detected
- Prevents async/sync bugs before code generation

### 5. **Future-Proof**
- If new UI_Tool added → automatically gets auto_tool_mode=true
- If async Agent_Tool needed → can manually set in future (rare <1% case)
- System handles 99% of cases automatically

## Edge Cases

### Case 1: Agent with BOTH UI_Tool and Agent_Tool
```json
"agent_modes": {
  "HybridAgent": true  // Has UI_Tool → auto_tool_mode=true
}
```
**Result**: ALL tools (UI_Tool AND Agent_Tool) can be async because AutoToolEventHandler handles both.

### Case 2: Agent with NO tools
```json
"agent_modes": {
  "PureConversationalAgent": false  // No tools → auto_tool_mode=false
}
```
**Result**: Agent doesn't invoke any tools, auto_tool_mode doesn't matter (defaults to false).

### Case 3: Future async Agent_Tool requirement (rare <1%)
If a workflow needs an async Agent_Tool for an agent WITHOUT UI_Tools:
- **Current system**: auto_tool_mode=false (sync Agent_Tools)
- **Override needed**: Manual modification to agent_modes in tools.json
- **Future enhancement**: Add `force_async` field to tool manifest for explicit async Agent_Tools

## Testing Checklist

- [ ] ToolsManagerAgent outputs `agent_modes` object in tools.json
- [ ] agent_modes correctly maps agents with UI_Tools to true
- [ ] agent_modes correctly maps agents without UI_Tools to false
- [ ] UIFileGenerator reads agent_modes from tools.json
- [ ] UIFileGenerator generates async code for UI_Tools with auto_tool_mode=true
- [ ] UIFileGenerator raises error if UI_Tool has auto_tool_mode=false
- [ ] AgentToolsFileGenerator reads agent_modes from tools.json
- [ ] AgentToolsFileGenerator generates sync code for Agent_Tools with auto_tool_mode=false
- [ ] AgentToolsFileGenerator generates async code for Agent_Tools with auto_tool_mode=true (if applicable)
- [ ] AgentsAgent reads agent_modes from tools.json
- [ ] AgentsAgent uses auto_tool_mode values directly (no re-calculation)
- [ ] Generated agents.json has correct auto_tool_mode for all agents
- [ ] Workflow executes successfully with automated auto_tool_mode

## Migration Notes

### Before This Change
- AgentsAgent determined auto_tool_mode in step 8 (too late)
- File generators (steps 6-7) couldn't know auto_tool_mode
- File generators had guidance but couldn't act on it
- Potential for sync/async mismatches

### After This Change
- ToolsManagerAgent determines auto_tool_mode in step 5 (early)
- File generators read agent_modes from tools.json
- File generators generate correct async/sync code automatically
- AgentsAgent reads agent_modes (no re-calculation)
- Single source of truth prevents inconsistencies

## Related Documentation
- `ASYNC_SYNC_TOOL_PATTERNS.md` - Comprehensive async/sync guidance
- `DOWNLOAD_AGENT_AUTO_TOOL_MIGRATION.md` - Case study of async UI tool migration
- `workflows/Generator/agents.json` - Updated agent configurations
- `workflows/Generator/tools/*.py` - Tool implementations (action_plan.py, mermaid_sequence_diagram.py, generate_and_download.py)

## References
- AG2 ConversableAgent.register_for_llm() - Native tool calling (doesn't await async)
- AutoToolEventHandler - Event-driven tool handler (properly awaits async)
- use_ui_tool primitive - Async WebSocket UI interaction helper
