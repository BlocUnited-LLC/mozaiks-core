# Alignment Audit: Structured Outputs ↔ Agent Prompts ↔ Pattern Examples

> **Created**: 2025-11-30
> **Status**: IN PROGRESS → PLANNING COMPLETE
> **Purpose**: Verify all Generator outputs align with `structured_outputs.json` (the source of truth)

---

## 1. Problem Statement

We have three representations that MUST align:
1. **`structured_outputs.json`** - THE source of truth for all data models
2. **`agents.json`** - Agent prompts with `output_format` sections
3. **`docs/pattern_examples/pattern_<id>_*.yaml`** - Examples used by `update_agent_state_pattern.py`

**STATUS**: 
- ✅ `structured_outputs.json` - Authoritative, defines all models
- ✅ `agents.json` - Generally aligned (output_format sections reference correct wrapper keys)
- ✅ `docs/pattern_examples/pattern_<id>_*.yaml` - Authoritative multi-doc YAML examples (one doc per semantic wrapper key)

---

## 2. What Each Agent Outputs (from structured_outputs.json registry)

These are the AUTHORITATIVE output schemas. Agent prompts and examples MUST match these.

| Agent | Structured Output Model | Semantic Wrapper Key |
|-------|------------------------|---------------------|
| InterviewAgent | null | N/A (conversational, outputs "NEXT") |
| PatternAgent | PatternSelectionOutput | `PatternSelection` |
| WorkflowStrategyAgent | WorkflowStrategyOutput | `WorkflowStrategy` |
| WorkflowArchitectAgent | TechnicalBlueprintOutput | `TechnicalBlueprint` |
| WorkflowImplementationAgent | ModuleAgentsOutput | `ModuleAgents` |
| ProjectOverviewAgent | MermaidSequenceDiagramOutput | `MermaidSequenceDiagram` |
| ContextVariablesAgent | ContextVariablesPlanOutput | `ContextVariablesPlan` |
| ToolsManagerAgent | ToolsManifestOutput | `tools` + `lifecycle_tools` |
| UIFileGenerator | UIToolsFilesOutput | `tools` (CodeFile[]) |
| AgentToolsFileGenerator | AgentToolsFilesOutput | `tools` (CodeFile[]) |
| HookAgent | HookFilesOutput | `hook_files` |
| AgentsAgent | RuntimeAgentsOutput | `agents` |
| HandoffsAgent | HandoffRulesOutput | `handoff_rules` |
| OrchestratorAgent | OrchestrationConfigOutput | (direct fields) |
| StructuredOutputsAgent | StructuredModelsOutput | `models` + `registry` |
| DownloadAgent | DownloadRequestOutput | `agent_message` |

---

## 3. Pattern Example Design

### 3.1 What Are Pattern Examples FOR?

Pattern examples are **teaching material** injected into agent prompts via `update_agent_state_pattern.py`. They show agents:
- "Here's what a complete workflow looks like for this pattern"
- Concrete examples of the outputs they need to produce

### 3.2 Which Agents Need Pattern Examples?

| Agent | Needs Examples? | What Examples Show |
|-------|-----------------|-------------------|
| PatternAgent | NO | Already has hardcoded guidance in prompt |
| WorkflowStrategyAgent | YES | `WorkflowStrategy` with modules for this pattern |
| WorkflowArchitectAgent | YES | `TechnicalBlueprint` with variables, UI components |
| WorkflowImplementationAgent | YES | `ModuleAgents` with agent specs, tools |
| ProjectOverviewAgent | YES | Mermaid diagram for this pattern |
| ContextVariablesAgent | YES | `ContextVariablesPlan` definitions |
| ToolsManagerAgent | YES | `ToolsManifestOutput` with tools |
| HandoffsAgent | YES | `handoff_rules` for this pattern |
| OrchestratorAgent | YES | Orchestration config |
| AgentsAgent | YES | Runtime agent definitions with prompt_sections |
| HookAgent | YES | Hook file examples |
| File Generators | MAYBE | Could benefit from code examples |
| DownloadAgent | NO | Simple output, no pattern-specific logic |

### 3.3 New Pattern Example Structure

Each pattern example file is a **comprehensive teaching document** that shows outputs from ALL relevant agents, organized by semantic wrapper key as a **multi-doc YAML** stream:

```yaml
# LAYER 0: PatternAgent Output
PatternSelection:
  is_multi_workflow: false
  decomposition_reason: null
  pack_name: "Example Pack"
  workflows:
    - name: "ExampleWorkflow"
      role: "primary"
      description: "…"
      pattern_id: 6
      pattern_name: "Pipeline"
---
# LAYER 1: WorkflowStrategyAgent Output
WorkflowStrategy:
  workflow_name: "ExampleWorkflow"
  workflow_description: "…"
  startup_mode: "UserDriven"
  human_in_loop: true
  modules: []
---
# LAYER 2a: StateArchitectAgent Output
StateArchitecture:
  global_context_variables: []
  lifecycle_requirements:
    before_chat: null
    after_chat: null
```

**Key Principle**: One file per pattern, containing ALL outputs needed by ALL agents that need pattern-specific guidance.

---

## 4. What update_agent_state_pattern.py Needs to Do

The injection hook should:

1. **Load pattern example** from `docs/pattern_examples/pattern_<id>_*.yaml`
2. **Extract relevant section** based on which agent is being injected:
   - WorkflowStrategyAgent → Extract `WorkflowStrategy` section
   - WorkflowArchitectAgent → Extract `TechnicalBlueprint` section
   - WorkflowImplementationAgent → Extract `ModuleAgents` section
   - etc.
3. **Format as guidance** with clear headers and JSON examples
4. **Replace `{{PATTERN_GUIDANCE_AND_EXAMPLES}}` placeholder** in agent prompt

---

## 5. Action Plan

### Phase 1: Verify agents.json Alignment (Quick Check)
- [ ] Verify each agent's output_format matches structured_outputs.json schema
- [ ] Check wrapper key names are consistent
- [ ] Ensure field names match exactly

### Phase 2: Verify Pattern Examples (YAML)
- [ ] Ensure `docs/pattern_examples/pattern_<id>_*.yaml` exists for all 9 patterns
- [ ] Ensure each YAML contains documents for required wrapper keys (at minimum: `PatternSelection`, `WorkflowStrategy`)
- [ ] Build one complete example (Pipeline pattern) as reference
- [ ] Build remaining 8 pattern examples

### Phase 3: Update update_agent_state_pattern.py
- [ ] Refactor to load from new pattern example location
- [ ] Update extraction logic to pull relevant sections per agent
- [ ] Test injection with at least one pattern

### Phase 4: Validation
- [ ] Run Generator workflow with each pattern
- [ ] Verify outputs match structured_outputs.json schemas
- [ ] Confirm prompt sections render correctly

---

## 6. Files to Update

| File | Change Needed |
|------|---------------|
| `docs/pattern_examples/pattern_<id>_*.yaml` | **AUTHORITATIVE** - Multi-doc pattern examples |
| `workflows/AgentGenerator/tools/update_agent_state_pattern.py` | **LOAD/EXTRACT** - Pull per-section examples from YAML |
| `workflows/AgentGenerator/agents.json` | **VERIFY** - Output_format alignment |

---

## 7. Next Immediate Step

**Use the Pipeline pattern example** (pattern_id=6) as the reference implementation. This will:
1. Establish the correct structure
2. Validate all fields against structured_outputs.json
3. Serve as template for remaining 8 patterns
