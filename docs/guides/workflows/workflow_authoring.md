# Workflow Authoring Guide

> ⚠️ **PARTIALLY OUTDATED**: This guide references legacy agent names (e.g., ActionPlanArchitect) that no longer exist.
> For current agent registry and schema definitions, see:
> - **Source of Truth**: `docs/ACTION_PLAN_SOURCE_OF_TRUTH.md`
> - **Schema Definitions**: `workflows/Generator/structured_outputs.json` (registry section)
> 
> The structural concepts (directory layout, manifest files, tool patterns) remain valid.

## Purpose

This guide explains how to create, structure, and configure multi-agent workflows in MozaiksAI. Workflows are the fundamental execution units that combine AG2 agents, tools, context variables, and UI integrations into cohesive automation pipelines.

## Overview

MozaiksAI workflows are **declarative, modular, and hot-swappable**. Each workflow lives in its own directory under `workflows/` with a collection of JSON manifests and Python tool implementations that the runtime loads dynamically.

**Core Principles:**
- **Declarative Configuration**: Workflows define *what* should run via JSON manifests; the runtime handles *how* to execute them
- **Modular Architecture**: Each workflow is self-contained with its own agents, tools, context variables, and UI components
- **Hot-Swappable**: Runtime discovers and loads workflows at startup without code changes to the runtime layer
- **Multi-Tenant Safe**: Workflows execute with app-scoped isolation; state never leaks across tenants
- **UI-First Design**: Workflows can expose interactive UI tools for agent-to-frontend collaboration

## Workflow Directory Structure

```
workflows/
├── __init__.py
├── Generator/                          # Example: workflow that generates other workflows
│   ├── __init__.py
│   ├── agents.json                     # Agent definitions with system messages
│   ├── tools.json                      # Tool registry (UI_Tool and Agent_Tool)
│   ├── structured_outputs.json         # Pydantic schema definitions
│   ├── context_variables.json          # Variable definitions (DB, env, derived)
│   ├── handoffs.json                   # Agent-to-agent routing rules
│   ├── orchestrator.json               # Runtime configuration (startup, max_turns)
│   ├── ui_config.json                  # (Optional) UI-specific settings
│   ├── hooks.json                      # (Optional) Custom lifecycle hooks
│   ├── tools/                          # Python tool implementations
│   │   ├── __init__.py
│   │   ├── action_plan.py              # UI_Tool: render ActionPlan artifact
│   │   ├── request_api_key.py          # UI_Tool: secure credential collection
│   │   ├── generate_and_download.py    # UI_Tool: file delivery
│   │   └── echo.py                     # Agent_Tool: backend-only example
│   └── InterviewAgentPrompt.json       # (Optional) Extended prompts
└── YourWorkflow/
    ├── agents.json
    ├── tools.json
    ├── structured_outputs.json
    └── ...
```

### File Purposes

| File | Required | Purpose |
|------|----------|---------|
| `agents.json` | **Yes** | Defines agent system messages, auto-tool modes, turn limits |
| `tools.json` | **Yes** | Registry of UI and backend tools with metadata |
| `structured_outputs.json` | **Yes** | Pydantic schema models for structured agent outputs |
| `context_variables.json` | **Yes** | Variable definitions (database, environment, derived) |
| `handoffs.json` | Recommended | Routing rules between agents (defaults to sequential) |
| `orchestrator.json` | **Yes** | Runtime config: startup mode, max_turns, visual agents |
| `ui_config.json` | Optional | UI-specific theming or display preferences |
| `hooks.json` | Optional | Custom message processing or state synchronization |
| `tools/` directory | **Yes** | Python tool implementations (`<tool_name>.py`) |

**Note:** `workflow.json` is **not used** in current implementations; `orchestrator.json` replaces it with extended runtime configuration.

## Manifest Files Deep Dive

### agents.json

Defines each agent's system message, behavior, and auto-tool configuration.

**Structure:**
```json
{
  "agents": {
    "InterviewAgent": {
      "system_message": "[ROLE] You are an expert conversational intake specialist...\n\n[OBJECTIVE]...\n\n[GUIDELINES]...\n\n[INSTRUCTIONS]...\n\n[OUTPUT FORMAT]...",
      "max_consecutive_auto_reply": 20,
      "auto_tool_mode": false,
      "structured_outputs_required": false
    },
    "ActionPlanArchitect": {
      "system_message": "[ROLE] You are a senior automation solutions architect...",
      "max_consecutive_auto_reply": 5,
      "auto_tool_mode": true,
      "structured_outputs_required": true
    }
  }
}
```

**Key Fields:**
- `system_message`: Multi-section prompt with standardized structure (see System Message Patterns below)
- `max_consecutive_auto_reply`: Maximum turns this agent can take before yielding control
- `auto_tool_mode`: When `true`, agent automatically emits structured outputs for UI tools (no manual `tool_call` invocation)
- `structured_outputs_required`: When `true`, agent output must conform to schema in `structured_outputs.json`

**System Message Patterns:**

All system messages follow this standardized structure:
1. **[ROLE]**: Agent identity and primary responsibility
2. **[OBJECTIVE]**: High-level goal and expected outcomes
3. **[CONTEXT]**: Sequential position in workflow, input discovery patterns, available data
4. **[GUIDELINES]**: Legal compliance reminder, output format compliance, strict rules
5. **[INSTRUCTIONS]**: Step-by-step execution algorithm
6. **[OUTPUT FORMAT]**: Exact expected output structure (JSON, text, or tool call)

**Guidelines Section Template:**
```
[GUIDELINES]
You must follow these guidelines strictly for legal reasons. Do not stray from them.
Output Compliance: You must adhere to the specified "Output Format" and its instructions. Do not include any additional commentary in your output.
- Rule 1...
- Rule 2...
```

**Derived Variable Triggers (Coordination Tokens):**

For agents that emit internal coordination tokens (e.g., `NEXT`, `PROCEED`) to trigger derived variables with `ui_hidden: true`, the system message **must include strict output constraints**:

```
[INSTRUCTIONS]
...
Step 2 - After the user's reply:
- Emit only NEXT on its own line to signal the downstream handoff.
Step 3 - If the user refuses to continue or types "exit":
- Still emit NEXT so downstream logic can determine the next action.

[OUTPUT FORMAT]
Turn 1:
What would you like to automate?

Context Variables:
...

Turn 2:
NEXT
```

This prevents LLM creativity from breaking exact string matching for derived variable triggers and message filtering.

### tools.json

Registry of all tools available to agents, with metadata for runtime loading and UI integration.

**Structure:**
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
      "agent": "ProjectOverviewAgent",
      "file": "mermaid_sequence_diagram.py",
      "function": "mermaid_sequence_diagram",
      "description": "Display the Mermaid sequence diagram after plan approval",
      "tool_type": "UI_Tool",
      "ui": {
        "component": "MermaidSequenceDiagram",
        "mode": "artifact"
      }
    },
    {
      "agent": "System",
      "file": "runtime_context_manager.py",
      "function": "runtime_context_manager",
      "description": "Runtime-managed context variable injection",
      "tool_type": "Agent_Tool",
      "ui": null
    }
  ]
}
```

**Tool Types:**

1. **UI_Tool**: Interactive agent-to-frontend tools
   - `ui.component`: React component name (PascalCase)
   - `ui.mode`: Display mode (`artifact` for full-screen, `inline` for embedded)
   - Requires `auto_tool_mode: true` on owning agent
   - Agent emits structured output; runtime auto-invokes tool

2. **Agent_Tool**: Backend-only tools
   - `ui`: Always `null`
   - Agent explicitly calls tool via AG2 function calling
   - No frontend interaction

**Key Fields:**
- `agent`: Owner agent (PascalCase)
- `file`: Python filename under `workflows/{workflow}/tools/` (snake_case)
- `function`: Callable function name (must match `file` stem)
- `description`: Purpose statement (≤140 chars)
- `tool_type`: `"UI_Tool"` or `"Agent_Tool"`
- `ui`: UI integration metadata or `null`

### structured_outputs.json

Defines Pydantic models for structured agent outputs and maps agents to their schemas.

**Structure:**
```json
{
  "structured_outputs": {
    "models": {
      "ActionPlan": {
        "type": "model",
        "fields": {
          "workflow": {
            "type": "WorkflowSpec",
            "description": "Workflow definition produced by the ContextAgent"
          }
        }
      },
      "WorkflowSpec": {
        "type": "model",
        "fields": {
          "name": { "type": "str", "description": "Human-readable workflow name" },
          "trigger": { "type": "literal", "values": ["form", "chatbot", "scheduled", "api"], "description": "Primary entry point" },
          "description": { "type": "str", "description": "Short summary of workflow goals" },
          "mermaid_flow": { "type": "str", "description": "Mermaid flowchart linking phases" },
          "phases": { "type": "list", "items": "WorkflowPhase", "description": "Ordered workflow phases" }
        }
      },
      "APIKeyRequest": {
        "type": "model",
        "fields": {
          "service": { "type": "str", "description": "Lowercase service identifier" },
          "description": { "type": "str", "description": "Purpose of this API key" },
          "mask_input": { "type": "bool", "description": "Always true to hide sensitive input" },
          "agent_message": { "type": "str", "description": "Message prompting user to provide key" },
          "required": { "type": "union", "variants": ["bool", "null"], "description": "Whether credential is required" }
        }
      }
    },
    "registry": {
      "ContextAgent": "ActionPlanCall",
      "APIKeyAgent": "APIKeyRequest",
      "FreeFormAgent": null
    }
  }
}
```

**Key Sections:**
- `models`: Pydantic model definitions with field schemas
- `registry`: Maps agent name to model name (or `null` for free-form output)

**Field Types:**
- Primitives: `str`, `int`, `bool`, `float`
- Collections: `list` (with `items` for element type)
- Unions: `union` (with `variants` array)
- Literals: `literal` (with `values` array for enum-like constraints)
- Nested: Reference another model name

**Auto-Tool Pattern:**

When `agent.auto_tool_mode = true` and `structured_outputs_required = true`:
1. Agent emits structured JSON matching registered model
2. Runtime validates against Pydantic schema
3. Runtime auto-invokes corresponding UI tool (matched by tool name convention)
4. Frontend receives `chat.tool_call` event with component metadata
5. User interaction response flows back to agent

### context_variables.json

Defines all variables available to agents during execution, with sources and triggers.

**Structure:**
```json
{
  "context_variables": {
    "definitions": {
      "context_aware": {
        "type": "boolean",
        "description": "Flag indicating if application is opensource or part of existing product",
        "source": {
          "type": "environment",
          "env_var": "CONTEXT_AWARE",
          "default": true
        }
      },
      "concept_overview": {
        "type": "string",
        "description": "Main project description from database",
        "source": {
          "type": "database",
          "database_name": "autogen_ai_agents",
          "collection": "Concepts",
          "search_by": "app_id",
          "field": "ConceptOverview"
        }
      },
      "interview_complete": {
        "type": "boolean",
        "description": "True once InterviewAgent has enough context to proceed",
        "source": {
          "type": "state",
          "default": false,
          "transitions": [
            {
              "from_state": false,
              "to_state": true,
              "trigger": {
                "type": "agent_text",
                "agent": "InterviewAgent",
                "match": { "equals": "NEXT" },
                "ui_hidden": true
              }
            }
          ]
        }
      }
    }
  }
}
```

**Variable Source Types:**

1. **Environment Variables**
   - `type: "environment"`
   - `env_var`: Environment variable name (UPPER_SNAKE_CASE)
   - `default`: Fallback value if env var not set

2. **Database Variables**
   - `type: "database"`
   - `database_name`: MongoDB database name
   - `collection`: Collection name
   - `search_by`: Query field (often `app_id` or `user_id`)
   - `field`: Field to extract from matched document

3. **Derived Variables**
   - `type: "derived"`
   - `default`: Initial value before triggers
   - `triggers`: Array of trigger conditions

**Derived Variable Triggers:**

Currently supported trigger type: `agent_text`
- `agent`: Source agent name
- `match.equals`: Exact string match for agent message content
- `ui_hidden`: When `true`, runtime suppresses rendering of trigger message in frontend (for internal coordination tokens like `NEXT`, `PROCEED`)

**UI Hidden Pattern:**

The `ui_hidden` flag coordinates with agent system messages to enable internal state management without confusing users:

1. Agent system message constrains output to exact token (e.g., `"Emit only NEXT on its own line"`)
2. Context variable trigger matches exact token (`"match": {"equals": "NEXT"}`)
3. `ui_hidden: true` tells frontend to suppress rendering that message
4. Derived variable flips to `true`, enabling conditional handoffs
5. User sees seamless transition without internal coordination tokens

### handoffs.json

Defines routing rules between agents, enabling conditional workflows and dynamic agent transitions.

**Example Structure:**
```json
{
  "handoff_rules": [
    {
      "source_agent": "InterviewAgent",
      "target_agent": "ContextAgent",
      "handoff_type": "after_work",
      "condition": null,
      "condition_type": null,
      "transition_target": "AgentTarget"
    },
    {
      "source_agent": "ContextAgent",
      "target_agent": "APIKeyAgent",
      "handoff_type": "condition",
      "condition": "${interview_complete} == True",
      "condition_type": "expression",
      "transition_target": "AgentTarget"
    }
  ]
}
```

**Handoff Types:**
- `after_work`: Unconditional transition after agent completes turn
- `condition`: Conditional transition based on expression evaluation

**Transition Targets:**
- `AgentTarget`: Hand off to another agent
- `RevertToUserTarget`: Return control to user for input
- `TerminateTarget`: End workflow execution

**Condition Expressions:**

Use template syntax to reference context variables:
- `${variable_name} == True`
- `${status} == "approved"`
- Evaluated against current context state

### orchestrator.json

Runtime configuration controlling workflow startup, execution limits, and visual agent filtering.

**Structure:**
```json
{
  "workflow_name": "Generator",
  "max_turns": 25,
  "human_in_the_loop": true,
  "startup_mode": "AgentDriven",
  "orchestration_pattern": "DefaultPattern",
  "initial_message_to_user": null,
  "initial_message": "You are part of a collaborative effort...",
  "initial_agent": "InterviewAgent"
}
```

**Key Fields:**

- `workflow_name`: PascalCase workflow identifier (must match directory name)
- `max_turns`: Maximum conversation turns before automatic termination
- `human_in_the_loop`: When `true`, workflow pauses for user approval at key checkpoints
- `startup_mode`: Controls who initiates conversation
  - `AgentDriven`: Agent speaks first
  - `UserDriven`: User speaks first
  - `BackendOnly`: No initial UI message
- `orchestration_pattern`: Execution pattern (currently always `"DefaultPattern"`)
- `initial_message_to_user`: Message shown to user (when `startup_mode = "UserDriven"`)
- `initial_message`: Message sent to agents (when `startup_mode = "AgentDriven"`)
- `initial_agent`: First agent to receive control (PascalCase agent name)

**Visual Agents (Deprecated in orchestrator.json):**

Earlier workflows included `visual_agents` arrays. Current implementations derive these from:
- **Agents whose text appears in UI (default: all agents)** 
- **Agents that can emit UI tools (derived from `tools.json` where `tool_type = "UI_Tool"`)**

Runtime calculates these dynamically from manifests; no need to declare in `orchestrator.json`.

## Workflow Creation Workflow

### 1. Design Phase

**Define workflow purpose:**
- What user problem does this solve?
- What are the major execution phases?
- Which phases require user interaction?
- What external services or data sources are needed?

**Map agent responsibilities:**
- One agent per discrete responsibility (intake, planning, execution, delivery)
- Identify which agents need UI tools vs. backend-only tools
- Determine handoff points and conditional routing

### 2. Directory Setup

```powershell
# Create workflow directory
mkdir workflows\YourWorkflow

# Create subdirectories
mkdir workflows\YourWorkflow\tools

# Create __init__.py files
New-Item workflows\YourWorkflow\__init__.py
New-Item workflows\YourWorkflow\tools\__init__.py
```

### 3. Manifest Authoring

**Order matters for first-time authoring:**

1. **Start with `context_variables.json`**: Define data dependencies first
2. **Create `agents.json`**: Define agents with system messages
3. **Build `tools.json`**: Register tools each agent will use
4. **Define `structured_outputs.json`**: Create schemas for auto-tool agents
5. **Author `handoffs.json`**: Wire agent transitions
6. **Configure `orchestrator.json`**: Set runtime parameters

**Manifest Validation Checklist:**

- [ ] All agent names consistent across files (PascalCase)
- [ ] All tool names consistent across files (snake_case)
- [ ] Every `UI_Tool` has corresponding React component in `ChatUI/src/workflows/{workflow}/components/`
- [ ] Every `UI_Tool` has corresponding Python implementation in `workflows/{workflow}/tools/`
- [ ] Every agent with `auto_tool_mode: true` has entry in `structured_outputs.json` registry
- [ ] All context variable references in handoff conditions are defined in `context_variables.json`
- [ ] Initial agent specified in `orchestrator.json` exists in `agents.json`

### 4. Tool Implementation

**Python Tool Pattern (UI_Tool):**

```python
# workflows/YourWorkflow/tools/example_ui_tool.py
from typing import Any, Dict, Optional, Annotated
from core.workflow.ui_tools import use_ui_tool

async def example_ui_tool(
    param_one: Annotated[str, "Description of param_one"],
    agent_message: Annotated[Optional[str], "Message displayed alongside UI"] = None,
    context_variables: Annotated[Optional[Any], "AG2 context injection"] = None,
) -> Dict[str, Any]:
    """Brief tool description.

    Behavior:
      1. Build payload for React component
      2. Emit UI tool event via use_ui_tool
      3. Wait for user response
      4. Return sanitized result

    Payload Contract:
      field_one       | str  | Description
      field_two       | bool | Description
      agent_message   | str  | User-facing context message

    Returns:
      {'status': 'success', 'ui_event_id': '...', 'data': {...}}
      or {'status': 'error', 'message': '...'}
    """
    # Extract runtime context
    chat_id = context_variables.get('chat_id') if context_variables else None
    workflow_name = context_variables.get('workflow_name') if context_variables else None

    # Build payload
    payload = {
        "field_one": param_one,
        "field_two": True,
        "agent_message": agent_message or "Default message",
        "agent_message_id": f"msg_{uuid.uuid4().hex[:10]}",
    }

    # Emit UI tool and wait for response
    response = await use_ui_tool(
        "ExampleComponent",
        payload,
        chat_id=chat_id,
        workflow_name=workflow_name,
        display="inline",  # or "artifact"
    )

    return response
```

**Python Tool Pattern (Agent_Tool):**

```python
# workflows/YourWorkflow/tools/example_agent_tool.py
from typing import Dict, Any
import logging

logger = logging.getLogger(__name__)

async def example_agent_tool(
    input_data: str,
    **runtime
) -> Dict[str, Any]:
    """Backend-only tool for data processing.

    Args:
        input_data: Input to process
        **runtime: AG2-injected context (chat_id, app_id, etc.)

    Returns:
        {'status': 'success', 'result': {...}}
        or {'status': 'error', 'message': '...'}
    """
    logger.info(f"Processing input: {input_data[:50]}...")

    try:
        # Business logic here
        result = process_data(input_data)
        return {'status': 'success', 'result': result}
    except Exception as e:
        logger.error(f"Processing failed: {e}")
        return {'status': 'error', 'message': str(e)}
```

### 5. React Component Implementation

**Component Pattern:**

```javascript
// ChatUI/src/workflows/YourWorkflow/components/ExampleComponent.js
import React, { useState } from 'react';
import { typography, components, spacing, layouts } from '../../../styles/artifactDesignSystem';

const ExampleComponent = ({ 
  payload = {},
  onResponse,
  onCancel,
  ui_tool_id,
  eventId,
  workflowName,
  sourceWorkflowName,
  generatedWorkflowName,
  componentId = "ExampleComponent"
}) => {
  const resolvedWorkflowName = generatedWorkflowName || workflowName || sourceWorkflowName || null;
  const [state, setState] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setIsSubmitting(true);

    const response = {
      status: 'success',
      action: 'submit',
      data: {
        result: state,
        ui_tool_id,
        eventId,
        workflowName: resolvedWorkflowName,
        sourceWorkflowName,
        generatedWorkflowName,
        agent_message_id: payload?.agent_message_id,
      }
    };

    onResponse(response);
  };

  return (
    <div className={layouts.artifactContainer}>
      <div className={components.card.primary}>
        <h2 className={typography.heading.lg}>{payload.title || "Example Component"}</h2>
        <p className={typography.body.md}>{payload.agent_message}</p>
        
        <form onSubmit={handleSubmit} className={spacing.section}>
          <input
            type="text"
            value={state}
            onChange={(e) => setState(e.target.value)}
            placeholder={payload.placeholder}
          />
          
          <div className="flex gap-4 mt-4">
            <button 
              type="submit" 
              disabled={isSubmitting}
              className={components.button.primary}
            >
              Submit
            </button>
            <button 
              type="button" 
              onClick={onCancel}
              className={components.button.secondary}
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default ExampleComponent;
```

**Component Registration:**

```javascript
// ChatUI/src/workflows/YourWorkflow/components/index.js
export { default as ExampleComponent } from './ExampleComponent';
export { default as AnotherComponent } from './AnotherComponent';
```

### 6. Testing and Validation

**Runtime Loading Check:**

```powershell
# Start server and check logs for workflow discovery
python run_server.py
# Look for: "✓ Discovered workflow: YourWorkflow"
```

**Common Issues:**

1. **Workflow not discovered**
   - Ensure `workflows/YourWorkflow/__init__.py` exists
   - Check workflow directory name matches `orchestrator.json` `workflow_name`

2. **Tool not loading**
   - Verify `tools.json` `file` field matches actual filename
   - Check `workflows/YourWorkflow/tools/__init__.py` exists
   - Ensure function name matches file stem

3. **Structured output validation fails**
   - Validate JSON schema in `structured_outputs.json`
   - Check agent output matches registered model fields
   - Verify snake_case consistency in field names

4. **UI component not rendering**
   - Check React component exported in `components/index.js`
   - Verify `tools.json` `ui.component` matches React component name (PascalCase)
   - Ensure `display` mode matches component design (artifact vs. inline)

## Best Practices

### Agent Design

1. **Single Responsibility**: Each agent should have one clear purpose
2. **Concise System Messages**: Keep prompts focused; avoid redundant instructions
3. **Deterministic Outputs**: Use structured outputs for predictable agent behavior
4. **Turn Limits**: Set `max_consecutive_auto_reply` conservatively to prevent runaway loops

### Tool Design

1. **Minimal Payload**: Only include fields required for UI or business logic
2. **Defensive Validation**: Check required fields; return meaningful errors
3. **Security First**: Never log or return secrets (API keys, tokens)
4. **Idempotent Operations**: Tools should be safe to retry

### Context Variables

1. **Explicit Dependencies**: Declare all database and environment dependencies
2. **Derived Variables for State**: Use triggers to track workflow progress
3. **UI Hidden for Coordination**: Hide internal tokens; show user-meaningful state
4. **Naming Conventions**: snake_case for all variable names

### Handoffs

1. **Linear Flows First**: Start with sequential `after_work` handoffs
2. **Conditional Sparingly**: Only add conditions when workflow truly branches
3. **Explicit Termination**: Always define when workflow completes
4. **Revert for Input**: Use `RevertToUserTarget` when user input required mid-flow

### File Organization

1. **Flat Tool Directory**: Keep all tool implementations in `workflows/{workflow}/tools/`
2. **No Subdirectories**: Avoid nested tool organization (runtime expects flat structure)
3. **Descriptive Names**: Tool file names should indicate purpose (`request_api_key.py`, not `tool1.py`)
4. **Version Manifests**: Consider timestamped backups when making breaking changes

## Naming Conventions

**Strict adherence to naming conventions prevents runtime errors:**

| Element | Convention | Example | Rationale |
|---------|-----------|---------|-----------|
| Workflow names | PascalCase | `Generator`, `DataPipeline` | Matches directory names, class naming |
| Agent names | PascalCase | `InterviewAgent`, `ContextAgent` | AG2 convention for agent identifiers |
| Tool names | snake_case | `action_plan`, `request_api_key` | Python function naming standard |
| Tool files | snake_case | `action_plan.py`, `request_api_key.py` | Python module naming standard |
| Context variables | snake_case | `interview_complete`, `concept_overview` | Python variable naming standard |
| React components | PascalCase | `ActionPlan`, `AgentAPIKeyInput` | React component convention |
| JSON payload keys | snake_case | `agent_message`, `ui_tool_id` | Backend-frontend consistency |
| Environment vars (in code) | snake_case | `context_aware`, `monetization_enabled` | Context variable consistency |
| Environment vars (actual) | UPPER_SNAKE_CASE | `CONTEXT_AWARE`, `MONETIZATION_ENABLED` | Shell environment convention |
| UI display modes | lowercase | `artifact`, `inline` | Enum-style string literals |
| Tool types | Mixed_Snake | `UI_Tool`, `Agent_Tool` | Legacy convention, kept for compatibility |

**Critical Rule:** Never mix casing within the same payload or interface. Python tools emit snake_case keys; React components access snake_case keys. This prevents `payload.agentMessage` vs. `payload.agent_message` mismatches.

## Advanced Patterns

### Multi-Phase Workflows

For complex workflows with distinct stages (intake → planning → execution → delivery):

1. **Phase-Scoped Agents**: Name agents with phase prefix (`IntakeAgent`, `PlanningAgent`)
2. **Phase Transition Variables**: Use derived variables to track phase completion
3. **Phase Handoff Rules**: Condition handoffs on phase completion variables
4. **Phase Logging**: Include phase context in tool logging for debugging

### Human-in-the-Loop Workflows

When `human_in_the_loop: true`:

1. **Explicit Approval Points**: Use UI tools at critical decision checkpoints
2. **Rejection Handling**: Include handoff rules for user rejection scenarios
3. **State Preservation**: Ensure context variables capture approval state
4. **Timeout Handling**: Consider max_turns to prevent indefinite waiting

### Dynamic Routing

For workflows with conditional paths:

1. **Decision Agents**: Create agents whose sole purpose is routing logic
2. **Multiple Handoffs**: Define multiple handoff rules from decision agent
3. **Clear Conditions**: Use simple boolean expressions for readability
4. **Fallback Paths**: Always include default handoff if conditions fail

### External Integrations

For workflows calling external APIs or services:

1. **Credential Collection**: Use `request_api_key` pattern for secure credential gathering
2. **Environment Fallback**: Support both env vars and runtime credential collection
3. **Error Boundaries**: Wrap external calls with try/except and meaningful error messages
4. **Rate Limiting**: Implement backoff and retry in tool implementations
5. **Logging Sanitization**: Never log API keys or tokens; log only metadata (service name, status)

## Troubleshooting

### Workflow Not Loading

**Symptom:** Workflow directory exists but runtime doesn't discover it

**Checks:**
1. Verify `workflows/{workflow}/__init__.py` exists (even if empty)
2. Check `orchestrator.json` `workflow_name` matches directory name exactly (case-sensitive)
3. Ensure no syntax errors in any JSON manifest (use JSON validator)
4. Check server logs for specific parsing errors

**Fix:**
```powershell
# Validate JSON syntax
python -m json.tool workflows\YourWorkflow\orchestrator.json
```

### Agent Not Executing

**Symptom:** Agent defined but never receives control

**Checks:**
1. Verify agent listed in `handoffs.json` as target
2. Check `orchestrator.json` `initial_agent` matches first agent name
3. Ensure handoff conditions evaluate to true (check derived variables)
4. Review `max_consecutive_auto_reply` – may be 0

**Fix:** Add explicit handoff rule or increase turn limit

### Tool Not Found

**Symptom:** Runtime error: "Tool {name} not found"

**Checks:**
1. `tools.json` `file` field matches actual filename under `workflows/{workflow}/tools/`
2. Function name matches file stem (e.g., `request_api_key.py` → `def request_api_key(...)`)
3. Tool file has no syntax errors
4. `workflows/{workflow}/tools/__init__.py` exists

**Fix:**
```powershell
# Verify tool file exists
ls workflows\YourWorkflow\tools\example_tool.py

# Check function definition
Select-String "async def example_tool" workflows\YourWorkflow\tools\example_tool.py
```

### UI Component Not Rendering

**Symptom:** Tool called but frontend shows error or blank space

**Checks:**
1. React component exported in `ChatUI/src/workflows/{workflow}/components/index.js`
2. `tools.json` `ui.component` matches React component name exactly (case-sensitive)
3. Component implements required props (`payload`, `onResponse`, `onCancel`, etc.)
4. No JavaScript console errors (check browser devtools)

**Fix:**
```javascript
// Verify export
export { default as ExampleComponent } from './ExampleComponent';

// Check component signature
const ExampleComponent = ({ payload, onResponse, onCancel, ...props }) => { ... }
```

### Structured Output Validation Fails

**Symptom:** Agent output rejected with schema validation error

**Checks:**
1. Agent output JSON matches model definition in `structured_outputs.json`
2. All required fields present
3. Field types match (e.g., `bool` not string `"true"`)
4. Nested models defined and referenced correctly
5. Agent `structured_outputs_required: true` in `agents.json`

**Fix:** Review agent system message `[OUTPUT FORMAT]` section; ensure exact JSON structure documented

## Next Steps

- **[Tool Manifest Reference](./tool_manifest.md)**: Deep dive into tool registration and implementation patterns
- **[Structured Outputs Guide](./structured_outputs.md)**: Comprehensive schema design and auto-tool configuration
- **[UI Tool Pipeline](./ui_tool_pipeline.md)**: Agent-to-frontend interaction architecture
- **[Auto-Tool Execution](./auto_tool_execution.md)**: Automatic tool invocation and deduplication mechanics
- **[Runtime Overview](../runtime/runtime_overview.md)**: How the runtime loads and executes workflows
- **[Event Pipeline](../runtime/event_pipeline.md)**: Event routing and correlation for UI tools
