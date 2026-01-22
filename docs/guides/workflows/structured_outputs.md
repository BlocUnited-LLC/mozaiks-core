# Structured Outputs Guide

## Purpose

This document explains how to define, configure, and use structured outputs in MozaiksAI workflows. Structured outputs enable agents to emit validated, schema-conforming JSON that the runtime can automatically transform into UI tool invocations, ensuring predictable agent behavior and type-safe agent-to-frontend communication.

## Overview

Structured outputs are **Pydantic-based schemas** that constrain agent responses to specific JSON structures. When combined with `auto_tool_mode`, structured outputs enable the **auto-tool pattern**: agents emit JSON, runtime validates it, and automatically invokes corresponding UI tools without manual function calling.

**Key Benefits:**
- **Type Safety**: Runtime validates agent outputs against schemas before processing
- **Predictable Behavior**: Agents produce consistent, parseable responses
- **Auto-Tool Execution**: Structured outputs trigger automatic UI tool invocation
- **Developer Experience**: Clear contracts between agents, tools, and UI components
- **Error Prevention**: Schema validation catches malformed outputs before they reach frontend

## Core Concepts

### Structured Outputs vs. Free-Form Output

**Free-Form Output (Default):**
```python
# Agent produces natural language text
Agent: "I've analyzed the data and found 3 key insights: performance is up 12%, 
        costs are down 8%, and user satisfaction improved..."
```

**Structured Output (Schema-Constrained):**
```python
# Agent produces validated JSON matching schema
{
  "AnalysisReport": {
    "metrics": [
      {"name": "performance", "change": 12, "direction": "up"},
      {"name": "costs", "change": -8, "direction": "down"},
      {"name": "satisfaction", "change": 15, "direction": "up"}
    ],
    "summary": "Overall positive trend across all metrics"
  },
  "agent_message": "Analysis complete—review key metrics"
}
```

### The Auto-Tool Pattern

**Standard Tool Invocation (Manual):**
```
Agent → Explicitly calls tool via AG2 function calling
     → Runtime executes tool
     → Tool returns result
     → Agent receives result
```

**Auto-Tool Pattern (Structured Outputs):**
```
Agent (auto_tool_mode: true, structured_outputs_required: true)
     → Emits structured JSON matching registered schema
     → Runtime validates against Pydantic model
     → Runtime automatically invokes corresponding UI tool
     → Tool emits UI event (use_ui_tool)
     → Frontend receives event and renders component
     → User interacts
     → Response flows back to agent
```

**Configuration Requirements:**

1. **Agent Config** (`agents.json`):
   - `auto_tool_mode: true`
   - `structured_outputs_required: true`

2. **Tool Registry** (`tools.json`):
   - Tool owned by agent
   - `tool_type: "UI_Tool"`
   - `ui` object with component and mode

3. **Structured Outputs** (`structured_outputs.json`):
   - Model definition for agent output
   - Registry entry mapping agent to model

## File Structure

The `structured_outputs.json` manifest has two main sections:

```json
{
  "structured_outputs": {
    "models": {
      "ModelName1": { /* model definition */ },
      "ModelName2": { /* model definition */ }
    },
    "registry": {
      "AgentName1": "ModelName1",
      "AgentName2": null
    }
  }
}
```

### Models Section

Defines Pydantic model schemas with field types and descriptions.

**Example:**
```json
{
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
        "trigger": { 
          "type": "literal", 
          "values": ["form", "chatbot", "scheduled", "api"],
          "description": "Primary entry point for this workflow"
        },
        "description": { "type": "str", "description": "Short summary of workflow goals" },
        "phases": { 
          "type": "list", 
          "items": "WorkflowPhase",
          "description": "Ordered workflow phases"
        }
      }
    }
  }
}
```

### Registry Section

Maps agent names to their output schemas.

**Example:**
```json
{
  "registry": {
    "InterviewAgent": null,
    "ContextAgent": "ActionPlanCall",
    "APIKeyAgent": "APIKeyRequest",
    "DownloadAgent": null
  }
}
```

- `null` value: Agent produces free-form output (no schema constraint)
- Model name: Agent output must conform to specified schema

## Field Types

MozaiksAI supports a comprehensive set of field types for model definitions:

### Primitive Types

| Type | Python Equivalent | Description | Example |
|------|-------------------|-------------|---------|
| `str` | `str` | String value | `"Hello world"` |
| `int` | `int` | Integer number | `42` |
| `bool` | `bool` | Boolean flag | `true`, `false` |
| `float` | `float` | Floating point number | `3.14` |
| `optional_str` | `Optional[str]` | String or null | `"value"` or `null` |

### Collection Types

**List (Homogeneous Array):**
```json
{
  "agents": {
    "type": "list",
    "items": "AgentDefinition",
    "description": "List of agent configurations"
  }
}
```

Translates to: `List[AgentDefinition]`

### Constrained Types

**Literal (Enum-Like):**
```json
{
  "startup_mode": {
    "type": "literal",
    "values": ["AgentDriven", "UserDriven", "BackendOnly"],
    "description": "How workflow initiates"
  }
}
```

Translates to: `Literal["AgentDriven", "UserDriven", "BackendOnly"]`

**Union (Multiple Allowed Types):**
```json
{
  "default": {
    "type": "union",
    "variants": ["null", "str", "bool", "int"],
    "description": "Fallback value of any type or null"
  }
}
```

Translates to: `Union[None, str, bool, int]`

### Nested Models

Reference other models by name:

```json
{
  "models": {
    "Parent": {
      "type": "model",
      "fields": {
        "child": {
          "type": "Child",
          "description": "Nested child model"
        }
      }
    },
    "Child": {
      "type": "model",
      "fields": {
        "value": { "type": "str", "description": "Child value" }
      }
    }
  }
}
```

## Model Definition Patterns

### Simple Model

**Use Case:** Flat structure with primitive fields

```json
{
  "APIKeyRequest": {
    "type": "model",
    "fields": {
      "service": {
        "type": "str",
        "description": "Lowercase service identifier (e.g., 'openai', 'anthropic')"
      },
      "description": {
        "type": "str",
        "description": "Purpose of this API key"
      },
      "mask_input": {
        "type": "bool",
        "description": "Always true to hide sensitive input"
      },
      "agent_message": {
        "type": "str",
        "description": "Message prompting the user to provide API Key"
      },
      "required": {
        "type": "union",
        "variants": ["bool", "null"],
        "description": "Whether credential must be collected before continuing"
      }
    }
  }
}
```

**Agent Output Example:**
```json
{
  "service": "openai",
  "description": "Used for content generation in marketing workflow",
  "mask_input": true,
  "agent_message": "Please provide your OpenAI API key to continue",
  "required": true
}
```

### Nested Model Hierarchy

**Use Case:** Complex structures with multiple levels

```json
{
  "models": {
    "ActionPlan": {
      "type": "model",
      "fields": {
        "workflow": {
          "type": "WorkflowSpec",
          "description": "Workflow definition"
        }
      }
    },
    "WorkflowSpec": {
      "type": "model",
      "fields": {
        "name": { "type": "str", "description": "Workflow name" },
        "phases": {
          "type": "list",
          "items": "WorkflowPhase",
          "description": "Ordered phases"
        }
      }
    },
    "WorkflowPhase": {
      "type": "model",
      "fields": {
        "name": { "type": "str", "description": "Phase name" },
        "agents": {
          "type": "list",
          "items": "WorkflowAgent",
          "description": "Agents in this phase"
        }
      }
    },
    "WorkflowAgent": {
      "type": "model",
      "fields": {
        "name": { "type": "str", "description": "Agent identifier" },
        "description": { "type": "str", "description": "Agent responsibilities" },
        "humanInLoop": { "type": "bool", "description": "Requires human approval" },
        "connectedTools": {
          "type": "list",
          "items": "ToolConnection",
          "description": "Tools this agent uses"
        }
      }
    },
    "ToolConnection": {
      "type": "model",
      "fields": {
        "name": { "type": "str", "description": "Tool name" },
        "purpose": { "type": "str", "description": "How tool is used" }
      }
    }
  }
}
```

**Agent Output Example:**
```json
{
  "ActionPlan": {
    "workflow": {
      "name": "Content Marketing Automation",
      "phases": [
        {
          "name": "Planning",
          "agents": [
            {
              "name": "StrategyAgent",
              "description": "Develops content strategy based on goals",
              "humanInLoop": false,
              "connectedTools": [
                {"name": "analyze_trends", "purpose": "Identify trending topics"}
              ]
            }
          ]
        },
        {
          "name": "Execution",
          "agents": [
            {
              "name": "WriterAgent",
              "description": "Generates blog posts and social content",
              "humanInLoop": true,
              "connectedTools": [
                {"name": "generate_content", "purpose": "Create blog drafts"},
                {"name": "check_grammar", "purpose": "Validate writing quality"}
              ]
            }
          ]
        }
      ]
    }
  },
  "agent_message": "Strategy complete—review the proposed workflow"
}
```

### Wrapper Model for UI Tools

**Use Case:** UI tool requiring both data payload and agent message

```json
{
  "ActionPlanCall": {
    "type": "model",
    "fields": {
      "ActionPlan": {
        "type": "ActionPlan",
        "description": "Workflow container (PascalCase to match tool arg)"
      },
      "agent_message": {
        "type": "str",
        "description": "Single friendly review prompt (≤140 chars)"
      }
    }
  }
}
```

**Why Wrapper Models?**

UI tools often need **both**:
1. **Structured data** (the artifact to display)
2. **Agent message** (context for user)

The wrapper pattern encapsulates both in a single schema:

```python
# Tool signature expects both
async def action_plan(
    ActionPlan: dict,  # The workflow structure
    agent_message: str,  # User-facing message
    context_variables: Any = None,
) -> dict:
    payload = {
        "ActionPlan": ActionPlan,
        "agent_message": agent_message,
        # ... additional fields
    }
    return await use_ui_tool("ActionPlan", payload, ...)
```

### Context Variables Model

**Use Case:** Defining workflow context variable schemas

```json
{
  "ContextVariablesPlan": {
    "type": "model",
    "fields": {
      "database_variables": {
        "type": "list",
        "items": "DatabaseVariable",
        "description": "Variables loaded from database"
      },
      "environment_variables": {
        "type": "list",
        "items": "EnvironmentVariable",
        "description": "Variables sourced from environment"
      },
      "declarative_variables": {
        "type": "list",
        "items": "DeclarativeVariable",
        "description": "Static declared values"
      },
      "derived_variables": {
        "type": "list",
        "items": "DerivedVariable",
        "description": "Computed state flags triggered by agent outputs"
      }
    }
  },
  "DatabaseVariable": {
    "type": "model",
    "fields": {
      "name": { "type": "str", "description": "Variable name" },
      "description": { "type": "str", "description": "Purpose in workflow" },
      "type": { "type": "optional_str", "description": "Type hint" },
      "database": {
        "type": "DatabaseRef",
        "description": "Database reference info"
      }
    }
  },
  "DatabaseRef": {
    "type": "model",
    "fields": {
      "database_name": { "type": "str", "description": "Database name" },
      "collection": { "type": "str", "description": "Collection name" },
      "search_by": { "type": "str", "description": "Query field" },
      "field": { "type": "str", "description": "Field to extract" }
    }
  },
  "DerivedVariable": {
    "type": "model",
    "fields": {
      "name": { "type": "str", "description": "Derived variable name" },
      "trigger_type": {
        "type": "literal",
        "values": ["agent_text_equals"],
        "description": "Trigger rule type"
      },
      "source_agent": { "type": "str", "description": "Agent whose output activates variable" },
      "trigger_value": { "type": "str", "description": "Exact value required to trigger" },
      "description": { "type": "str", "description": "Effect when true" },
      "ui_hidden": {
        "type": "union",
        "variants": ["bool", "null"],
        "description": "Hide trigger message from UI (for internal tokens)"
      }
    }
  }
}
```

## Registry Configuration

### Registry Entry Types

**Registered with Model:**
```json
{
  "registry": {
    "ContextAgent": "ActionPlanCall"
  }
}
```

- Agent outputs must conform to `ActionPlanCall` schema
- Runtime validates before processing
- Auto-tool invocation enabled (if `auto_tool_mode: true`)

**Registered as Free-Form:**
```json
{
  "registry": {
    "InterviewAgent": null
  }
}
```

- Agent produces natural language text
- No schema validation
- Auto-tool invocation disabled

### Matching Agents to Models

**1:1 Mapping (Most Common):**
```json
{
  "registry": {
    "APIKeyAgent": "APIKeyRequest",
    "DownloadAgent": "FileDeliveryRequest"
  }
}
```

Each agent has exactly one output schema.

**Reusable Models:**
```json
{
  "models": {
    "GenericConfirmation": {
      "type": "model",
      "fields": {
        "message": { "type": "str", "description": "Confirmation message" },
        "action": { "type": "str", "description": "Action to confirm" }
      }
    }
  },
  "registry": {
    "ApprovalAgent": "GenericConfirmation",
    "VerificationAgent": "GenericConfirmation"
  }
}
```

Multiple agents can share the same model when outputs are structurally identical.

### Conditional Models

**Problem:** Agent sometimes produces structured output, sometimes free-form

**Solution:** Use free-form mode (`null` registry) and explicit tool calling:

```json
{
  "registry": {
    "AdaptiveAgent": null
  }
}
```

```python
# Agent system message includes conditional logic
[INSTRUCTIONS]
1. If user asks for structured report, call generate_report tool
2. Otherwise, respond with natural language explanation
```

Agent explicitly calls tools when needed rather than auto-tool pattern.

## Integration with Auto-Tool Mode

### Full Auto-Tool Configuration

**Step 1: Define Model**

```json
// structured_outputs.json
{
  "models": {
    "ActionPlanCall": {
      "type": "model",
      "fields": {
        "ActionPlan": { "type": "ActionPlan", "description": "Workflow structure" },
        "agent_message": { "type": "str", "description": "Review prompt" }
      }
    }
  }
}
```

**Step 2: Register Agent**

```json
// structured_outputs.json
{
  "registry": {
    "ContextAgent": "ActionPlanCall"
  }
}
```

**Step 3: Configure Agent**

```json
// agents.json
{
  "agents": {
    "ContextAgent": {
      "system_message": "...",
      "auto_tool_mode": true,
      "structured_outputs_required": true
    }
  }
}
```

**Step 4: Register Tool**

```json
// tools.json
{
  "tools": [
    {
      "agent": "ContextAgent",
      "file": "action_plan.py",
      "function": "action_plan",
      "tool_type": "UI_Tool",
      "ui": {
        "component": "ActionPlan",
        "mode": "artifact"
      }
    }
  ]
}
```

**Execution Flow:**

1. `ContextAgent` completes turn and emits JSON:
   ```json
   {
     "ActionPlan": { /* workflow structure */ },
     "agent_message": "Review the proposed workflow"
   }
   ```

2. Runtime validates against `ActionPlanCall` schema
3. Validation succeeds → Runtime invokes `action_plan` tool
4. Tool calls `use_ui_tool("ActionPlan", payload, ...)`
5. Frontend receives event and renders `ActionPlan` component
6. User reviews and approves/rejects
7. Response flows back to agent

### Agent System Message Pattern

When `auto_tool_mode: true`, the agent's system message should **NOT** include manual tool calling instructions. Instead:

```
[GUIDELINES]
- Do NOT manually call the action_plan tool; runtime invokes UI automatically.
- Provide BOTH: ActionPlan (with nested workflow) AND agent_message.
- agent_message is MANDATORY: one concise (<=140 chars) review invitation.

[INSTRUCTIONS]
1) Analyze user requirements and design workflow phases
2) Derive agents for each capability cluster
3) Compose mermaid_flow diagram
4) Write compelling agent_message inviting review
5) Emit JSON exactly per STRUCTURED OUTPUTS schema

[OUTPUT FORMAT]
{
  "ActionPlan": {
    "workflow": { /* ... */ }
  },
  "agent_message": "Blueprint ready: phases & agents decomposed—approve or request refinements."
}
```

**Key Points:**
- ✅ Document the expected JSON structure
- ✅ Require `agent_message` field
- ✅ Reference schema name ("STRUCTURED OUTPUTS schema")
- ❌ Don't include tool calling syntax
- ❌ Don't mention runtime mechanics

## Validation and Error Handling

### Runtime Validation

When agent emits structured output:

```python
# Simplified runtime validation flow
agent_output = agent.generate_reply(messages)

if agent_config["structured_outputs_required"]:
    model_name = structured_outputs_registry[agent.name]
    
    try:
        # Validate against Pydantic model
        validated = validate_structured_output(agent_output, model_name)
    except ValidationError as e:
        # Log validation failure
        logger.error(f"Agent {agent.name} output failed schema validation: {e}")
        
        # Inject error feedback into conversation
        error_message = f"Your output did not match the required schema. Error: {e}"
        messages.append({"role": "user", "content": error_message})
        
        # Allow agent to retry
        agent_output = agent.generate_reply(messages)
```

### Common Validation Errors

**Missing Required Field:**

```json
// Agent emits (INVALID - missing agent_message)
{
  "ActionPlan": { /* workflow */ }
}

// Error
ValidationError: Field 'agent_message' is required
```

**Fix:** Include all required fields in agent output.

**Type Mismatch:**

```json
// Agent emits (INVALID - bool as string)
{
  "mask_input": "true"  // Should be boolean, not string
}

// Error
ValidationError: Field 'mask_input' expected bool, got str
```

**Fix:** Ensure field types match schema exactly.

**Invalid Literal Value:**

```json
// Model defines literal with allowed values
{
  "startup_mode": {
    "type": "literal",
    "values": ["AgentDriven", "UserDriven", "BackendOnly"]
  }
}

// Agent emits (INVALID)
{
  "startup_mode": "AutoStart"  // Not in allowed values
}

// Error
ValidationError: 'startup_mode' must be one of ['AgentDriven', 'UserDriven', 'BackendOnly']
```

**Fix:** Use only values defined in `values` array.

**Nested Model Validation Failure:**

```json
// Agent emits (INVALID - missing required field in nested model)
{
  "ActionPlan": {
    "workflow": {
      "name": "MyWorkflow"
      // Missing required 'phases' field
    }
  }
}

// Error
ValidationError: Field 'phases' is required in model 'WorkflowSpec'
```

**Fix:** Validate nested structures match their schemas.

## Field Naming Conventions

**Critical Rule:** All field names must use **snake_case** for backend-frontend consistency.

### Correct Naming

```json
{
  "fields": {
    "agent_message": { "type": "str" },      // ✅ snake_case
    "ui_tool_id": { "type": "str" },         // ✅ snake_case
    "workflow_name": { "type": "str" },      // ✅ snake_case
    "max_consecutive_auto_reply": { "type": "int" }  // ✅ snake_case
  }
}
```

### Incorrect Naming

```json
{
  "fields": {
    "agentMessage": { "type": "str" },       // ❌ camelCase
    "UIToolId": { "type": "str" },           // ❌ PascalCase
    "Workflow_Name": { "type": "str" },      // ❌ Mixed case
    "maxConsecutiveAutoReply": { "type": "int" }  // ❌ camelCase
  }
}
```

**Why This Matters:**

1. **Python→JSON Serialization**: Pydantic uses field names as JSON keys
2. **Frontend Access**: React components access `payload.agent_message` (snake_case)
3. **Consistency**: Mixed casing causes `undefined` errors when frontend expects different case
4. **Debugging**: Case mismatches are hard to spot in large payloads

**Exception:** Model names use PascalCase (e.g., `ActionPlan`, `WorkflowSpec`)

## Advanced Patterns

### Optional Fields with Defaults

```json
{
  "AgentDefinition": {
    "type": "model",
    "fields": {
      "name": { "type": "str", "description": "Agent name" },
      "max_consecutive_auto_reply": {
        "type": "int",
        "default": 10,
        "description": "Maximum turns before yielding control"
      }
    }
  }
}
```

**Agent Output (Explicit):**
```json
{
  "name": "CustomAgent",
  "max_consecutive_auto_reply": 5
}
```

**Agent Output (Using Default):**
```json
{
  "name": "CustomAgent"
  // max_consecutive_auto_reply defaults to 10
}
```

### Polymorphic Fields with Union

**Use Case:** Field can be multiple types

```json
{
  "default": {
    "type": "union",
    "variants": ["null", "str", "bool", "int"],
    "description": "Default value of any supported type"
  }
}
```

**Valid Outputs:**
```json
{"default": null}
{"default": "fallback_value"}
{"default": true}
{"default": 42}
```

### List with Complex Items

```json
{
  "connectedTools": {
    "type": "list",
    "items": "ToolConnection",
    "description": "List of tool connections"
  }
}
```

Where `ToolConnection` is another model:

```json
{
  "ToolConnection": {
    "type": "model",
    "fields": {
      "name": { "type": "str", "description": "Tool name" },
      "purpose": { "type": "str", "description": "Usage description" }
    }
  }
}
```

**Agent Output:**
```json
{
  "connectedTools": [
    {"name": "fetch_data", "purpose": "Retrieve user analytics"},
    {"name": "generate_report", "purpose": "Create PDF summary"}
  ]
}
```

### Deeply Nested Structures

```json
{
  "models": {
    "Level1": {
      "type": "model",
      "fields": {
        "level2": { "type": "Level2", "description": "Nested level 2" }
      }
    },
    "Level2": {
      "type": "model",
      "fields": {
        "level3": { "type": "Level3", "description": "Nested level 3" }
      }
    },
    "Level3": {
      "type": "model",
      "fields": {
        "value": { "type": "str", "description": "Final value" }
      }
    }
  }
}
```

**Agent Output:**
```json
{
  "level2": {
    "level3": {
      "value": "deeply_nested_data"
    }
  }
}
```

**Best Practice:** Limit nesting to 3-4 levels for maintainability; deeper structures are hard to debug.

### Conditional Fields

**Problem:** Field required only when another field has specific value

**Solution:** Use union types and validation in tool implementation:

```json
{
  "condition_type": {
    "type": "union",
    "variants": ["str", "null"],
    "description": "Required when handoff_type is 'condition', null otherwise"
  }
}
```

Schema allows `null`, but tool validates conditionally:

```python
async def process_handoff(handoff_data: dict, **runtime) -> dict:
    if handoff_data["handoff_type"] == "condition":
        if not handoff_data.get("condition_type"):
            return {"status": "error", "message": "condition_type required for conditional handoffs"}
    # ... process
```

## Model Reusability

### Shared Models Across Workflows

Models can be reused across multiple workflows by defining common structures:

**Common Models (Hypothetical Shared Library):**
```json
{
  "models": {
    "StatusResponse": {
      "type": "model",
      "fields": {
        "status": { "type": "str", "description": "Operation status" },
        "message": { "type": "str", "description": "Human-readable message" }
      }
    },
    "PaginatedList": {
      "type": "model",
      "fields": {
        "items": { "type": "list", "items": "dict", "description": "List items" },
        "total": { "type": "int", "description": "Total count" },
        "page": { "type": "int", "description": "Current page" },
        "page_size": { "type": "int", "description": "Items per page" }
      }
    }
  }
}
```

**Workflow-Specific Usage:**
```json
{
  "models": {
    "UserListResponse": {
      "type": "model",
      "fields": {
        "pagination": { "type": "PaginatedList", "description": "User list with pagination" },
        "status": { "type": "StatusResponse", "description": "Operation result" }
      }
    }
  }
}
```

### Inheritance-Like Patterns

While Pydantic inheritance isn't directly supported in manifest, you can **compose** models:

```json
{
  "BaseToolSpec": {
    "type": "model",
    "fields": {
      "agent": { "type": "str", "description": "Owner agent" },
      "file": { "type": "str", "description": "Python file" },
      "function": { "type": "str", "description": "Function name" }
    }
  },
  "UIToolSpec": {
    "type": "model",
    "fields": {
      "agent": { "type": "str", "description": "Owner agent" },
      "file": { "type": "str", "description": "Python file" },
      "function": { "type": "str", "description": "Function name" },
      "ui": { "type": "UIConfig", "description": "UI metadata" }
    }
  }
}
```

**Note:** This duplicates fields. Future enhancement could support `extends` keyword.

## Troubleshooting

### Schema Validation Fails

**Symptom:** Agent output rejected with validation error

**Debug Steps:**

1. **Check logs** for exact validation error message
2. **Compare agent output** to model definition field-by-field
3. **Verify field names** match exactly (case-sensitive)
4. **Check nested structures** for missing required fields
5. **Validate types** match schema (bool not string, int not string)

**Common Fixes:**

```python
# Agent system message should document exact schema
[OUTPUT FORMAT]
{
  "service": "string (lowercase)",
  "mask_input": boolean (true/false, NOT "true"/"false"),
  "required": boolean or null
}
```

### Auto-Tool Not Invoking

**Symptom:** Agent emits JSON but UI tool doesn't execute

**Checks:**

1. Agent has `auto_tool_mode: true`
2. Agent has `structured_outputs_required: true`
3. Agent registered in `structured_outputs.json` registry (not `null`)
4. Tool exists with `tool_type: "UI_Tool"` owned by agent
5. Schema validation passes (check logs)

**Debug:**

```bash
# Check runtime logs for validation errors
Select-String "schema validation" logs\runtime_*.log

# Verify agent configuration
python -c "import json; print(json.load(open('workflows/YourWorkflow/agents.json'))['agents']['YourAgent'])"
```

### Field Type Mismatch

**Symptom:** Frontend receives unexpected data types

**Root Cause:** Schema allows union but frontend expects specific type

**Example:**

```json
// Schema allows union
{
  "required": {
    "type": "union",
    "variants": ["bool", "null"]
  }
}

// Agent emits
{"required": null}

// Frontend expects boolean
const isRequired = payload.required;  // null, not false
if (isRequired) { /* ... */ }  // Falsy, but not explicit false
```

**Fix:** Frontend should handle all union variants:

```javascript
const isRequired = payload.required ?? false;  // Default to false if null
```

Or narrow schema if `null` shouldn't be allowed:

```json
{
  "required": { "type": "bool" }
}
```

### Model Not Found Error

**Symptom:** Runtime error: "Model 'XYZ' not defined"

**Checks:**

1. Model name in registry matches model name in models section (case-sensitive)
2. All referenced nested models are defined
3. No circular references (Model A references Model B which references Model A)

**Fix:**

```json
// ❌ WRONG - model name mismatch
{
  "models": {
    "ActionPlanCall": { /* ... */ }
  },
  "registry": {
    "ContextAgent": "ActionPlan"  // Should be "ActionPlanCall"
  }
}

// ✅ CORRECT
{
  "models": {
    "ActionPlanCall": { /* ... */ }
  },
  "registry": {
    "ContextAgent": "ActionPlanCall"
  }
}
```

## Best Practices

### Schema Design

1. **Start Simple**: Begin with flat models; add nesting only when needed
2. **Clear Descriptions**: Write descriptions for LLM understanding and developer docs
3. **Consistent Types**: Use same type for semantically similar fields across models
4. **Explicit Nullability**: Use `union` with `"null"` for optional fields rather than `optional_str`
5. **Validate Early**: Test schema with sample JSON before implementing agent logic

### Field Naming

1. **snake_case Always**: Never use camelCase in field names
2. **Descriptive Names**: `agent_message` not `msg`, `workflow_name` not `wf`
3. **Consistent Terminology**: If one model uses `name`, others should too (not `title`, `label`, etc.)
4. **Avoid Abbreviations**: Spell out words unless universally understood (id, url okay; msg, cfg not okay)

### Model Organization

1. **Bottom-Up Definition**: Define leaf models before parent models in manifest
2. **Group Related Models**: Keep models for same feature together in file
3. **Reuse Common Patterns**: Extract shared structures into base models
4. **Version Models**: Consider timestamped backups before schema changes

### Agent System Messages

1. **Document Schema**: Include exact JSON structure in `[OUTPUT FORMAT]` section
2. **Type Examples**: Show example values for each field type
3. **Required vs Optional**: Clearly mark which fields are mandatory
4. **No Manual Calls**: For auto-tool agents, don't include tool calling instructions

### Validation Strategy

1. **Fail Fast**: Validate outputs immediately; don't wait for tool execution
2. **Informative Errors**: Provide specific field-level error messages
3. **Retry Logic**: Allow agent to retry on validation failure with error feedback
4. **Logging**: Log all validation failures with full agent output for debugging

## Performance Considerations

### Schema Complexity

- **Simple schemas** (5-10 fields): Negligible validation overhead
- **Complex nested schemas** (50+ fields): ~10-50ms validation time
- **Very large outputs** (1MB+ JSON): Consider streaming or chunking

**Optimization:** If validation becomes bottleneck, cache compiled Pydantic models.

### Field Count Impact

Each field adds minimal overhead, but deeply nested structures increase traversal time:

- **Flat model** (10 fields, 1 level): ~1ms validation
- **Nested model** (50 fields, 3 levels): ~5-10ms validation
- **Deep nested** (100+ fields, 5+ levels): ~20-50ms validation

**Best Practice:** Limit nesting depth to 3-4 levels for optimal performance.

## Next Steps

- **[Tool Manifest Reference](./tool_manifest.md)**: Implement tools that consume structured outputs
- **[UI Tool Pipeline](./ui_tool_pipeline.md)**: Build React components that render structured data
- **[Auto-Tool Execution](./auto_tool_execution.md)**: Deep dive into automatic tool invocation mechanics
- **[Workflow Authoring Guide](./workflow_authoring.md)**: Complete workflow creation workflow
- **[Event Pipeline](../runtime/event_pipeline.md)**: Understand event routing for UI tools
- **[Configuration Reference](../runtime/configuration_reference.md)**: Runtime settings for structured output validation
