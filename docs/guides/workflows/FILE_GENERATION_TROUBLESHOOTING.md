5# File Generation Troubleshooting Guide

## Issue Detected
Files are not being generated during the Generator workflow due to JSON parsing errors.

## Root Causes

### 1. **Over-Escaping in JSON Output** (CRITICAL)
**Symptom**: Agents generate output but JSON parsing fails

**Log Evidence**:
```
[WARNING] 🧩 mozaiks.workflow - [SAVE_EVENT] ✗ Failed to parse JSON for UIFileGenerator
[WARNING] 🧩 mozaiks.workflow - [SAVE_EVENT] ✗ Failed to parse JSON for AgentToolsFileGenerator
```

**Content Preview**:
```json
{"code_files":[{"filename":"content_strategist.py","content":"\\"\\"\\"\\nContent Strategist Module...
```

**Problem**: Triple quotes are being escaped as `\\"\\"\\"` instead of `"""`, breaking JSON parsing.

**Why It Happened**: Old agent prompts didn't have explicit anti-escaping instructions.

**Solution Applied**: ✅ Updated agent prompts with explicit "Critical Output Compliance Requirements" section that prohibits over-escaping.

### 2. **Runtime Using Cached agents.json**
**Symptom**: Prompts were updated but runtime still uses old prompts

**Why**: The runtime loads `workflows/Generator/agents.json` at startup and caches it in memory.

**Solution**: Restart the MozaiksAI server to load updated agents.json

### 3. **HookAgent Returning Empty**
**Symptom**: `{"code_files":[]}`

**Why**: HookAgent only generates custom lifecycle hook implementations when needed. If the workflow uses built-in lifecycle operations (like collect_api_keys), no custom hook files are required.

**Expected Behavior**: This is actually CORRECT if no custom hooks are needed.

## Solution Steps

### Step 1: Restart the MozaiksAI Server ⚠️ REQUIRED
The updated agents.json prompts will only take effect after restarting:

```bash
# Stop the current server (Ctrl+C if running in terminal)
# Or kill the process

# Restart the server
cd "c:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\MozaiksAI"
# Run your normal start command (e.g., python main.py or docker-compose up)
```

### Step 2: Run a New Test
After restarting, create a new workflow to test the file generators.

**Test Workflow**: Create agents with both integrations and operations:
```json
{
  "name": "TestAgent",
  "integrations": ["Slack", "OpenAI"],
  "operations": ["calculate_total", "validate_input"]
}
```

**Expected Results**:
- ✅ UIFileGenerator produces valid JSON
- ✅ AgentToolsFileGenerator produces valid JSON
- ✅ Files contain complete implementations (not stubs)
- ✅ installRequirements include necessary packages
- ✅ No over-escaping in code content

### Step 3: Monitor the Logs
Watch for these indicators:

**✅ Success Indicators**:
```
[INFO] 🧩 mozaiks.workflow -  [WORKFLOW_SETUP] Generator: agents=['...UIFileGenerator', 'AgentToolsFileGenerator'...]
[INFO] Writing file: content_strategist.py
[INFO] Writing file: ai_content_generator.py
```

**❌ Failure Indicators**:
```
[WARNING] 🧩 mozaiks.workflow - [SAVE_EVENT] ✗ Failed to parse JSON for UIFileGenerator
[WARNING] 🧩 mozaiks.workflow - [SAVE_EVENT] ✗ Failed to parse JSON for AgentToolsFileGenerator
```

### Step 4: Verify Generated Files
After the workflow completes, check the download bundle:

**File Quality Checklist**:
- [ ] Files have proper line breaks (not `\\n`)
- [ ] Docstrings are clean (`"""` not `\\"\\"\\"`)
- [ ] Imports are complete
- [ ] Integration code uses actual SDK (e.g., `from slack_sdk import WebClient`)
- [ ] Operations have complete business logic (not `# TODO`)
- [ ] installRequirements lists all packages

## Updated Agent Prompts Summary

### What Was Added

#### 1. **Best Practices Block**
- Code quality guidelines
- Logging requirements
- Documentation standards
- Import completeness rules
- Error prevention guidance

#### 2. **Integration Implementation Requirements** (NEW!)
- Research API documentation guidance
- Complete SDK integration instructions
- Examples for common integrations (Slack, Stripe, etc.)
- Never use placeholders rule
- Integration code structure templates

#### 3. **Operations Implementation Requirements** (NEW!)
- Understand operation name guidance
- Complete business logic instructions
- Examples for common operations (calculate_taxes, validate_email)
- Never use placeholders rule
- Operation code structure templates

#### 4. **Critical Output Compliance Requirements**
- Output Format: JSON only, no markdown
- Real Line Breaks: Use `\n`, not `\\n`
- No Markdown: Don't wrap in ```json
- Escaped Characters: Proper JSON escaping only
  - **For JavaScript: use `"` not `'`**
  - **Don't use `\'` (invalid escape)**
- installRequirements: Complete dependency listing
- No Placeholders: Production-ready code only

## Verification Commands

### Check Current Prompt Version
```bash
cd "c:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\MozaiksAI"
python -c "import json; data = json.load(open('workflows/Generator/agents.json', encoding='utf-8')); print('UIFileGenerator prompt length:', len(data['agents']['UIFileGenerator']['system_message'])); print('Contains Integration Implementation:', 'Integration Implementation Requirements' in data['agents']['UIFileGenerator']['system_message'])"
```

**Expected Output**:
```
UIFileGenerator prompt length: 13574
Contains Integration Implementation: True
```

### Check Runtime Loaded Version
Look at startup logs:
```bash
# Search for this in your server logs:
grep "WORKFLOW_SETUP" logs/logs/mozaiks.log | tail -1
```

**Expected**: Should show `agents=['...UIFileGenerator', 'AgentToolsFileGenerator', 'HookAgent'...]`

## Common Issues & Fixes

### Issue: Still Seeing Over-Escaping After Restart
**Cause**: Workflow JSON cache or browser cache

**Fix**:
1. Clear browser cache/hard refresh (Ctrl+Shift+R)
2. Delete workflow output directory
3. Start a completely new workflow (don't retry old ones)

### Issue: HookAgent Always Returns Empty
**Explanation**: This is normal! HookAgent only generates custom lifecycle hook files when the workflow needs custom implementations.

**Built-in hooks** (don't need files):
- collect_api_keys_from_action_plan
- initialize_workflow_state
- cleanup_resources

**Custom hooks** (need files):
- Custom validation logic
- Special pre-processing
- Custom post-processing

If your workflow only uses built-in hooks, HookAgent will correctly return `{"code_files":[]}`.

### Issue: Files Generated But Still Have Placeholders
**Cause**: LLM didn't follow instructions (needs prompt strengthening)

**Fix**: The prompts now have emphatic language:
- ❌ BAD examples showing placeholders
- ✅ GOOD examples showing complete implementations
- "MUST", "NEVER", "NO" in capitals for emphasis

If this persists after restart, the LLM may need additional guidance or examples.

### Issue: Missing Integrations in Generated Code
**Cause**: Integration instructions not clear enough

**Current Guidance** (added in update):
- **Research the Integration**: ALWAYS research official SDK documentation
- **Use Official SDKs**: Install and import proper packages
- **Complete API Integration**: Real authentication, API calls, error handling
- **Examples Provided**: Slack, Stripe, GoogleAnalytics, Twilio, SendGrid, MozaiksPay

If integrations are still missing after restart, check that:
1. The `integrations` field in the action plan is populated
2. The agent has the integration listed (e.g., `"integrations": ["Slack"]`)

## Testing Checklist

After restarting, verify each of these:

### Basic Functionality
- [ ] Runtime starts without errors
- [ ] Generator workflow loads successfully
- [ ] All 14 agents are present

### File Generation (UIFileGenerator)
- [ ] Produces valid JSON output
- [ ] JSON parses successfully (no warnings in logs)
- [ ] Files have proper line breaks
- [ ] React components are complete
- [ ] No over-escaping in code

### File Generation (AgentToolsFileGenerator)
- [ ] Produces valid JSON output
- [ ] JSON parses successfully
- [ ] Integration code uses real SDKs
- [ ] Operations have complete business logic
- [ ] installRequirements includes all packages
- [ ] No placeholders or TODOs

### File Generation (HookAgent)
- [ ] Returns `{"code_files":[]}` if no custom hooks needed ✅
- [ ] OR generates custom hook files if needed
- [ ] Hook files implement correct lifecycle triggers

### Download/Bundle
- [ ] generate_and_download tool executes
- [ ] workflow_converter processes successfully
- [ ] Bundle contains all generated files
- [ ] Files are readable and properly formatted

## Success Metrics

After the fix, you should see:

**Before (Broken)**:
```
❌ Failed to parse JSON for UIFileGenerator
❌ Failed to parse JSON for AgentToolsFileGenerator
❌ Files with over-escaped content: "\\"\\"\\"
❌ Placeholder code: # TODO: Implement
❌ Missing imports
❌ Empty installRequirements
```

**After (Working)**:
```
✅ JSON parsed successfully for UIFileGenerator
✅ JSON parsed successfully for AgentToolsFileGenerator
✅ Files with proper formatting: """
✅ Complete implementations with real logic
✅ All imports included
✅ installRequirements: ["slack-sdk", "stripe", "openai"]
```

## Quick Reference

### Updated Files
- `workflows/Generator/agents.json` (UIFileGenerator, AgentToolsFileGenerator, HookAgent)
- `scripts/update_file_generator_prompts.py` (reusable script)
- `workflows/Generator/agents.json.backup` (backup of original)

### Documentation
- `FILE_GENERATOR_PROMPT_UPDATE_SUMMARY.md` - Initial output format update
- `INTEGRATION_OPERATIONS_UPDATE_SUMMARY.md` - Integration/operations guidance
- `FILE_GENERATION_TROUBLESHOOTING.md` - This document

### Key Prompt Sections
1. **[ROLE]** - Agent identity
2. **[ASYNC/SYNC DESIGN RULES]** - UI_Tool vs Agent_Tool patterns
3. **[INSTRUCTIONS]**
   - Best Practices
   - Integration Implementation Requirements (NEW)
   - Critical Output Compliance Requirements

### Contact Points
If issues persist after restart:
1. Check `logs/logs/mozaiks.log` for specific errors
2. Verify agents.json was actually reloaded (check startup logs)
3. Try a fresh workflow (not a retry of a failed one)
4. Check that the LLM API is responding correctly

---

**Critical Action Required**: **RESTART THE MOZAIKS SERVER** to load the updated prompts!

**Date**: 2025-10-28
**Issue**: File generation failures due to JSON over-escaping
**Status**: Fixed (pending restart)
