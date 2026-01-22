# Tool Manifest Reference

## Purpose

This document provides comprehensive guidance on authoring `tools.json` manifests and implementing tool functions that agents can invoke during workflow execution. It covers tool registration patterns, UI vs. backend tool distinctions, implementation signatures, and integration with the AG2 runtime.

## Overview

Tools are the executable actions agents perform during workflow execution. The `tools.json` manifest serves as the **single source of truth** for tool discovery, registration, and metadata that the runtime uses to:

1. **Load Python implementations** from `workflows/{workflow}/tools/`
2. **Register tools with AG2 agents** based on ownership mapping
3. **Route UI tool invocations** to frontend React components
4. **Validate tool parameters** and return values
5. **Enable auto-tool execution** for agents with `auto_tool_mode: true`

## Tool Types

MozaiksAI supports two distinct tool types with different execution models:

### UI_Tool: Interactive Agent-to-Frontend Tools

**Purpose:** Enable agents to request user input, display artifacts, or trigger frontend interactions.

**Characteristics:**
- **Agent emits structured output**; runtime auto-invokes tool (no manual tool call)
- **Requires `auto_tool_mode: true`** on owning agent
- **Bidirectional**: Tool emits UI event → User interacts → Response flows back to agent
- **Display modes**: `artifact` (full-screen overlay) or `inline` (embedded in chat)
- **Frontend component required**: React component in `ChatUI/src/workflows/{workflow}/components/`

**Execution Flow:**
```
Agent (auto_tool_mode: true)
  → Emits structured JSON output
  → Runtime validates against structured_outputs.json
  → Runtime invokes UI tool implementation
  → Python tool calls use_ui_tool(...)
  → Frontend receives chat.tool_call event
  → React component renders with payload
  → User interacts (submit/cancel)
  → Frontend sends chat.tool_response
  → Runtime correlates via ui_tool_id
  → Agent receives response in next turn
```

**Example Use Cases:**
- Displaying interactive artifacts (action plans, dashboards, diagrams)
- Collecting sensitive credentials (API keys, tokens)
- Requesting user approvals or confirmations
- File upload/download interfaces
- Multi-step forms with validation

### Agent_Tool: Backend-Only Tools

**Purpose:** Enable agents to perform server-side operations without user interaction.

**Characteristics:**
- **Agent explicitly calls tool** via AG2 function calling
- **No frontend component** (`ui: null` in manifest)
- **Synchronous or async** execution
- **Returns structured data** to calling agent
- **No display mode** required

**Execution Flow:**
```
Agent
  → Invokes tool via AG2 function call syntax
  → Runtime executes Python tool implementation
  → Tool performs backend operation (API call, DB query, computation)
  → Tool returns dict result
  → Agent receives result in next message
```

**Example Use Cases:**
- External API calls (fetching data, posting updates)
- Database queries and updates
- File system operations (reading, writing, processing)
- Data transformations and computations
- Internal workflow coordination (context variable injection)

## Manifest Structure

### Full Example

```json
{
  "tools": [
    {
      "agent": "ContextAgent",
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
      "agent": "APIKeyAgent",
      "file": "request_api_key.py",
      "function": "request_api_key",
      "description": "Collect API key credentials securely from the user",
      "tool_type": "UI_Tool",
      "ui": {
        "component": "AgentAPIKeyInput",
        "mode": "inline"
      }
    },
    {
      "agent": "DownloadAgent",
      "file": "generate_and_download.py",
      "function": "generate_and_download",
      "description": "Generate workflow files and deliver as downloadable ZIP",
      "tool_type": "UI_Tool",
      "ui": {
        "component": "FileDownloadCenter",
        "mode": "inline"
      }
    },
    {
      "agent": "System",
      "file": "echo.py",
      "function": "echo",
      "description": "Echo test tool for debugging agent communication",
      "tool_type": "Agent_Tool",
      "ui": null
    },
    {
      "agent": "System",
      "file": "runtime_context_manager.py",
      "function": "runtime_context_manager",
      "description": "Runtime-managed context variable injection (auto-attached)",
      "tool_type": "Agent_Tool",
      "ui": null
    }
  ]
}
```

### Field Definitions

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `agent` | string | **Yes** | Owner agent name (PascalCase, must exist in `agents.json`) |
| `file` | string | **Yes** | Python filename under `workflows/{workflow}/tools/` (snake_case, include `.py`) |
| `function` | string | **Yes** | Callable function name within file (snake_case, must match file stem) |
| `description` | string | **Yes** | Purpose statement shown to LLM and users (≤140 chars, plain language) |
| `tool_type` | string | **Yes** | `"UI_Tool"` or `"Agent_Tool"` (with underscore) |
| `ui` | object\|null | **Yes** | UI metadata for UI_Tool, `null` for Agent_Tool |
| `ui.component` | string | Conditional | React component name (PascalCase, required if `tool_type = "UI_Tool"`) |
| `ui.mode` | string | Conditional | Display mode: `"artifact"` or `"inline"` (required if `tool_type = "UI_Tool"`) |

**Validation Rules:**
- `file` must match actual filename in `workflows/{workflow}/tools/`
- `function` must be defined in the referenced file
- `agent` must be declared in `agents.json`
- `tool_type = "UI_Tool"` requires `ui` object with both `component` and `mode`
- `tool_type = "Agent_Tool"` requires `ui = null`
- All names must follow naming conventions (see below)

### Display Modes

**`artifact` (Full-Screen Overlay):**
- Renders component in modal overlay covering entire viewport
- Suitable for complex, multi-section artifacts (plans, reports, visualizations)
- User must interact (approve/reject/download) before dismissing
- Pauses conversation flow until user responds
- **Example:** `ActionPlan` component displaying multi-phase workflow blueprint

**`inline` (Embedded in Chat):**
- Renders component inline within chat message flow
- Suitable for lightweight inputs or confirmations
- Conversation continues around the component
- Less visually disruptive than artifact mode
- **Example:** `AgentAPIKeyInput` for credential collection

**Choosing Display Mode:**
- Use `artifact` when content requires full attention and extended interaction
- Use `inline` for quick inputs, confirmations, or supplementary information
- Consider mobile UX: `artifact` better for complex layouts, `inline` for simple forms

## Tool Implementation Patterns

### UI_Tool Implementation

**Signature:**

```python
async def tool_name(
    param_one: Annotated[str, "Description for LLM"],
    param_two: Annotated[Optional[int], "Optional parameter"] = None,
    agent_message: Annotated[Optional[str], "Message displayed alongside UI"] = None,
    context_variables: Annotated[Optional[Any], "AG2 context injection"] = None,
) -> Dict[str, Any]:
    """Brief tool description.

    Behavior:
      1. Extract runtime context from context_variables
      2. Build payload dict for React component
      3. Call use_ui_tool(...) to emit UI event
      4. Wait for user response (handled by use_ui_tool)
      5. Return sanitized result

    Payload Contract (keys sent to frontend):
      field_one       | str  | Description of what this field contains
      field_two       | bool | Description
      agent_message   | str  | User-facing context message
      agent_message_id| str  | Correlation ID for message threading

    Returns:
      {'status': 'success', 'ui_event_id': '...', 'agent_message_id': '...', 'data': {...}}
      or {'status': 'error', 'message': '...'}

    Errors:
      Raises ValueError if required fields missing
    """
```

**Key Elements:**

1. **Type Annotations with `Annotated`**: Provides descriptions for LLM function calling
2. **`context_variables` parameter**: AG2 injects runtime context (chat_id, workflow_name, app_id, etc.)
3. **`agent_message` parameter**: Optional message displayed alongside UI component
4. **Payload Contract docstring**: Documents fields sent to frontend (critical for UI component development)
5. **`use_ui_tool` import**: `from core.workflow.ui_tools import use_ui_tool`

**Full Example (`request_api_key.py`):**

```python
from typing import Any, Dict, Optional, Annotated
from core.workflow.ui_tools import use_ui_tool
from logs.logging_config import get_workflow_logger
import uuid

async def request_api_key(
    service: Annotated[str, "Lowercase service identifier (e.g. 'openai', 'anthropic')"],
    agent_message: Annotated[Optional[str], "Message displayed in chat alongside artifact"] = None,
    description: Optional[str] = None,
    required: Annotated[bool, "Whether key is required to proceed"] = True,
    mask_input: Annotated[bool, "Whether to mask characters in UI input field"] = True,
    context_variables: Annotated[Optional[Any], "Context variables provided by AG2"] = None,
) -> Dict[str, Any]:
    """Emit a UI interaction prompting the user to input an API key.

    Behavior:
      1. Builds a UI payload for the React component `AgentAPIKeyInput`.
      2. Emits the UI tool event via `use_ui_tool`.
      3. Waits for the correlated frontend response.
      4. Returns a sanitized result (never includes the secret itself).

    SECURITY:
      - Does NOT log the provided key.
      - Does NOT return raw or masked fragments of the key.
      - Only metadata (length, status) is returned.

    Payload Contract:
      service         | str  | Lowercase service identifier
      label           | str  | Display label (e.g., "OpenAI API Key")
      agent_message   | str  | Message displayed in chat
      description     | str  | Purpose explanation
      placeholder     | str  | Input field placeholder text
      required        | bool | Whether key is mandatory
      maskInput       | bool | Whether to hide input characters
      agent_message_id| str  | Correlation ID for threading
    """
    # Extract runtime context
    chat_id = context_variables.get('chat_id') if context_variables else None
    workflow_name = context_variables.get('workflow_name') if context_variables else None

    if not workflow_name:
        return {"status": "error", "message": "workflow_name is required"}

    # Validate required fields
    if not isinstance(service, str) or not service.strip():
        return {"status": "error", "message": "service is required"}

    service_norm = service.strip().lower().replace(" ", "_")
    agent_message_id = f"msg_{uuid.uuid4().hex[:10]}"

    # Build payload for React component
    payload = {
        "service": service_norm,
        "label": f"{service_norm.replace('_', ' ').title()} API Key",
        "agent_message": agent_message or f"Please provide your {service_norm} API key to continue.",
        "description": description or f"Enter your {service_norm} API key to continue",
        "placeholder": f"Enter your {service_norm.upper()} API key...",
        "required": required,
        "maskInput": mask_input,
        "agent_message_id": agent_message_id,
    }

    # Emit UI tool and wait for response
    response = await use_ui_tool(
        "AgentAPIKeyInput",
        payload,
        chat_id=chat_id,
        workflow_name=str(workflow_name),
        display="inline",
    )

    # Return sanitized result (never include actual API key)
    if response.get("status") == "success":
        return {
            "status": "success",
            "ui_event_id": response.get("ui_event_id"),
            "agent_message_id": agent_message_id,
            "data": {
                "service": service_norm,
                "key_length": response.get("data", {}).get("keyLength", 0),
                "timestamp": response.get("data", {}).get("submissionTime"),
            }
        }
    
    return response
```

**Critical Patterns:**

1. **Context Extraction**: Always extract `chat_id` and `workflow_name` from `context_variables`
2. **Validation**: Check required fields early; return `{"status": "error", "message": "..."}` on failure
3. **Correlation IDs**: Generate `agent_message_id` for message threading
4. **Security**: Never log or return secrets; return only metadata
5. **Payload Keys**: Use snake_case for all payload keys (frontend expects this)
6. **Display Mode**: Choose `"artifact"` or `"inline"` based on UI complexity
7. **Response Passthrough**: Return `use_ui_tool` result with minimal transformation

### Agent_Tool Implementation

**Signature:**

```python
async def tool_name(
    param_one: str,
    param_two: Optional[int] = None,
    **runtime
) -> Dict[str, Any]:
    """Brief tool description.

    Args:
        param_one: Description of parameter
        param_two: Optional parameter description
        **runtime: AG2-injected context (chat_id, app_id, workflow_name, context_variables)

    Returns:
        {'status': 'success', 'result': {...}}
        or {'status': 'error', 'message': '...'}

    Raises:
        ValueError: When required parameters missing or invalid
    """
```

**Key Elements:**

1. **`**runtime` parameter**: Captures AG2-injected context without explicit parameter list
2. **Type annotations**: For LLM function calling (can omit `Annotated` for simpler tools)
3. **Structured return**: Always return dict with `status` key
4. **Error handling**: Use try/except and return error dicts (don't raise unless critical)

**Full Example (`echo.py`):**

```python
from typing import Any, Dict, Optional
import logging

logger = logging.getLogger(__name__)

async def echo(
    message: str,
    repeat_count: Optional[int] = 1,
    **runtime
) -> Dict[str, Any]:
    """Echo test tool for debugging agent communication.

    Repeats the provided message the specified number of times. Useful for
    validating tool registration and AG2 function calling mechanics.

    Args:
        message: Message to echo back
        repeat_count: Number of times to repeat (default 1)
        **runtime: AG2-injected context

    Returns:
        {'status': 'success', 'echoed': repeated_message}
        or {'status': 'error', 'message': error_description}
    """
    # Extract runtime context if needed
    chat_id = runtime.get('chat_id')
    workflow_name = runtime.get('workflow_name')

    logger.info(f"Echo tool invoked in workflow={workflow_name}, chat={chat_id}")

    # Validate inputs
    if not message:
        return {"status": "error", "message": "message parameter is required"}

    if repeat_count < 1 or repeat_count > 10:
        return {"status": "error", "message": "repeat_count must be between 1 and 10"}

    # Perform operation
    try:
        echoed = " ".join([message] * repeat_count)
        logger.debug(f"Echoed message: {echoed[:100]}...")
        
        return {
            "status": "success",
            "echoed": echoed,
            "original_length": len(message),
            "repeated_length": len(echoed),
        }
    except Exception as e:
        logger.error(f"Echo tool failed: {e}")
        return {"status": "error", "message": str(e)}
```

**Critical Patterns:**

1. **`**runtime` unpacking**: Access context via `runtime.get('key')`
2. **Logging**: Use workflow-scoped logger for debugging
3. **Validation**: Check parameters before processing
4. **Error dictionaries**: Return `{"status": "error", "message": "..."}` instead of raising
5. **Structured success**: Include meaningful result data beyond just `status`

### use_ui_tool Helper

**Import:**
```python
from core.workflow.ui_tools import use_ui_tool
```

**Signature:**
```python
async def use_ui_tool(
    component_type: str,
    payload: dict,
    chat_id: str,
    workflow_name: str,
    display: str = 'inline',
    awaiting_response: bool = True,
) -> dict:
    """Emit UI tool event and optionally wait for user response.

    Args:
        component_type: React component name (PascalCase)
        payload: Data sent to component (all keys snake_case)
        chat_id: Current chat session ID
        workflow_name: Current workflow name
        display: 'artifact' or 'inline'
        awaiting_response: If True, wait for user interaction

    Returns:
        {'status': 'success', 'ui_event_id': '...', 'agent_message_id': '...', 'data': {...}}
        or {'status': 'error', 'message': '...'}
    """
```

**Behavior:**
1. Generates unique `ui_tool_id` for correlation
2. Emits `chat.tool_call` WebSocket event
3. If `awaiting_response=True`, blocks until `chat.tool_response` received
4. Correlates response via `ui_tool_id`
5. Returns response data to calling tool
6. Handles timeouts and cancellations

**Response Contract:**

**Success Response:**
```json
{
  "status": "success",
  "ui_event_id": "ui_12345abc",
  "agent_message_id": "msg_abc123",
  "data": {
    "action": "submit",
    "custom_field": "user_input_value",
    "workflowName": "Generator",
    "sourceWorkflowName": "Generator",
    "generatedWorkflowName": null,
    "ui_tool_id": "ui_12345abc",
    "eventId": "evt_xyz789"
  }
}
```

**Error Response:**
```json
{
  "status": "error",
  "message": "User cancelled operation",
  "code": "user_cancelled"
}
```

## Integration with agents.json

Tools are assigned to agents via the `agent` field in `tools.json`. This determines:

1. **Tool availability**: Only tools owned by an agent are registered with that agent
2. **Auto-tool behavior**: If agent has `auto_tool_mode: true` and owns a UI_Tool, runtime auto-invokes it
3. **Function calling**: AG2 includes only owned tools in agent's function list

### Auto-Tool Mode Configuration

**In `agents.json`:**
```json
{
  "agents": {
    "ContextAgent": {
      "system_message": "...",
      "max_consecutive_auto_reply": 5,
      "auto_tool_mode": true,
      "structured_outputs_required": true
    }
  }
}
```

**In `tools.json`:**
```json
{
  "tools": [
    {
      "agent": "ContextAgent",
      "file": "action_plan.py",
      "function": "action_plan",
      "description": "Render the Action Plan artifact for user review",
      "tool_type": "UI_Tool",
      "ui": {
        "component": "ActionPlan",
        "mode": "artifact"
      }
    }
  ]
}
```

**In `structured_outputs.json`:**
```json
{
  "structured_outputs": {
    "models": {
      "ActionPlanCall": {
        "type": "model",
        "fields": {
          "ActionPlan": { "type": "ActionPlan", "description": "Workflow container" },
          "agent_message": { "type": "str", "description": "Review prompt" }
        }
      }
    },
    "registry": {
      "ContextAgent": "ActionPlanCall"
    }
  }
}
```

**Execution Flow:**
1. `ContextAgent` completes turn
2. AG2 validates output against `ActionPlanCall` schema
3. Runtime detects agent has `auto_tool_mode: true`
4. Runtime finds tool owned by `ContextAgent` with `tool_type: "UI_Tool"`
5. Runtime invokes `action_plan` tool implementation
6. Tool calls `use_ui_tool("ActionPlan", payload, ...)`
7. Frontend receives event and renders `ActionPlan` component
8. User interacts and submits response
9. Response flows back to agent in next turn

### Multiple Tools per Agent

Agents can own multiple tools:

```json
{
  "tools": [
    {
      "agent": "DataAgent",
      "file": "fetch_data.py",
      "function": "fetch_data",
      "description": "Fetch data from external API",
      "tool_type": "Agent_Tool",
      "ui": null
    },
    {
      "agent": "DataAgent",
      "file": "transform_data.py",
      "function": "transform_data",
      "description": "Transform raw data into structured format",
      "tool_type": "Agent_Tool",
      "ui": null
    },
    {
      "agent": "DataAgent",
      "file": "display_results.py",
      "function": "display_results",
      "description": "Display processed results to user",
      "tool_type": "UI_Tool",
      "ui": {
        "component": "ResultsViewer",
        "mode": "artifact"
      }
    }
  ]
}
```

**Agent can explicitly call backend tools:**
```
Agent internal reasoning:
"I need to fetch data, transform it, then display results."

Tool calls:
1. fetch_data(source="api.example.com", endpoint="/data")
2. transform_data(raw_data=<result_from_step_1>)
3. (Agent emits structured output for display_results)
   → Runtime auto-invokes display_results UI_Tool
```

## Naming Conventions

**Strict adherence prevents runtime errors and maintains consistency:**

| Element | Convention | Example | Rationale |
|---------|-----------|---------|-----------|
| Tool names | snake_case | `action_plan`, `request_api_key` | Python function naming |
| Tool files | snake_case | `action_plan.py`, `request_api_key.py` | Python module naming |
| Functions | snake_case | `def action_plan(...)` | Python function naming |
| Agent names | PascalCase | `ContextAgent`, `APIKeyAgent` | AG2 agent identifier convention |
| React components | PascalCase | `ActionPlan`, `AgentAPIKeyInput` | React component convention |
| Payload keys (backend) | snake_case | `agent_message`, `ui_tool_id` | Backend-frontend consistency |
| Payload keys (frontend) | snake_case | `payload.agent_message` | Must match backend keys |
| Display modes | lowercase | `artifact`, `inline` | Enum-style string literals |
| Tool types | Mixed_Snake | `UI_Tool`, `Agent_Tool` | Legacy convention |

**Critical Rules:**

1. **File stem = function name**: `request_api_key.py` must define `async def request_api_key(...)`
2. **No camelCase in payloads**: Always `agent_message` not `agentMessage` (Python → JS boundary)
3. **Component name matches manifest**: `"component": "ActionPlan"` must match `export default ActionPlan`
4. **Tool name matches file**: `"function": "action_plan"` must match `action_plan.py`

## Common Patterns

### Credential Collection Pattern

**Use Case:** Securely collect API keys or tokens from users

**Implementation:**
```python
# tools/request_api_key.py
async def request_api_key(
    service: Annotated[str, "Service identifier"],
    agent_message: Annotated[Optional[str], "Context message"] = None,
    mask_input: Annotated[bool, "Mask input characters"] = True,
    context_variables: Annotated[Optional[Any], "AG2 context"] = None,
) -> Dict[str, Any]:
    """Securely collect API key from user."""
    chat_id = context_variables.get('chat_id')
    workflow_name = context_variables.get('workflow_name')

    payload = {
        "service": service.lower(),
        "agent_message": agent_message or f"Please provide your {service} API key",
        "maskInput": mask_input,
        "agent_message_id": f"msg_{uuid.uuid4().hex[:10]}",
    }

    response = await use_ui_tool(
        "AgentAPIKeyInput",
        payload,
        chat_id=chat_id,
        workflow_name=workflow_name,
        display="inline",
    )

    # NEVER return the actual key; return only metadata
    if response.get("status") == "success":
        return {
            "status": "success",
            "service": service,
            "key_received": True,
            "key_length": response.get("data", {}).get("keyLength", 0),
        }

    return response
```

**Security Checklist:**
- ✅ Mask input by default
- ✅ Never log API key
- ✅ Never return API key in response
- ✅ Only return metadata (length, timestamp)
- ✅ Use correlation ID for threading

### File Delivery Pattern

**Use Case:** Generate and deliver downloadable files to user

**Implementation:**
```python
# tools/generate_and_download.py
async def generate_and_download(
    confirmation_only: Annotated[bool, "If true, only request confirmation"] = False,
    agent_message: Annotated[Optional[str], "Context message"] = None,
    context_variables: Annotated[Optional[Any], "AG2 context"] = None,
) -> Dict[str, Any]:
    """Generate workflow files and deliver as downloadable ZIP."""
    chat_id = context_variables.get('chat_id')
    workflow_name = context_variables.get('workflow_name')

    if confirmation_only:
        # First call: just request user confirmation
        payload = {
            "mode": "confirmation",
            "agent_message": agent_message or "Ready to build bundle?",
            "agent_message_id": f"msg_{uuid.uuid4().hex[:10]}",
        }
    else:
        # Generate files
        files_data = await _generate_workflow_files(context_variables)
        
        payload = {
            "mode": "download",
            "files": files_data,
            "agent_message": agent_message or "Your workflow bundle is ready",
            "agent_message_id": f"msg_{uuid.uuid4().hex[:10]}",
        }

    response = await use_ui_tool(
        "FileDownloadCenter",
        payload,
        chat_id=chat_id,
        workflow_name=workflow_name,
        display="inline",
    )

    return response
```

### Artifact Display Pattern

**Use Case:** Display complex structured data for review/approval

**Implementation:**
```python
# tools/action_plan.py
async def action_plan(
    ActionPlan: Annotated[dict, "Workflow structure with phases and agents"],
    agent_message: Annotated[str, "Review invitation message"],
    context_variables: Annotated[Optional[Any], "AG2 context"] = None,
) -> Dict[str, Any]:
    """Render the Action Plan artifact for user review."""
    chat_id = context_variables.get('chat_id')
    workflow_name = context_variables.get('workflow_name')

    payload = {
        "ActionPlan": ActionPlan,  # Structured workflow data
        "agent_message": agent_message,
        "agent_message_id": f"msg_{uuid.uuid4().hex[:10]}",
    }

    response = await use_ui_tool(
        "ActionPlan",
        payload,
        chat_id=chat_id,
        workflow_name=workflow_name,
        display="artifact",  # Full-screen for complex artifact
    )

    return response
```

### External API Integration Pattern

**Use Case:** Call third-party APIs from backend

**Implementation:**
```python
# tools/post_to_social.py
import httpx

async def post_to_social(
    platform: str,
    content: str,
    **runtime
) -> Dict[str, Any]:
    """Post content to social media platform."""
    chat_id = runtime.get('chat_id')
    app_id = runtime.get('app_id')
    context_vars = runtime.get('context_variables', {})

    # Retrieve API key from context variables (injected by runtime_context_manager)
    api_key = context_vars.get(f'{platform}_api_key')
    
    if not api_key:
        return {
            "status": "error",
            "message": f"API key for {platform} not configured"
        }

    try:
        async with httpx.AsyncClient() as client:
            response = await client.post(
                f"https://api.{platform}.com/v1/posts",
                headers={"Authorization": f"Bearer {api_key}"},
                json={"content": content},
                timeout=30.0,
            )
            response.raise_for_status()
            
            result = response.json()
            return {
                "status": "success",
                "post_id": result.get("id"),
                "url": result.get("url"),
                "platform": platform,
            }
    except httpx.HTTPError as e:
        return {
            "status": "error",
            "message": f"API call failed: {str(e)}",
            "code": "api_error"
        }
```

**Best Practices:**
- ✅ Use async HTTP client (httpx, aiohttp)
- ✅ Set reasonable timeouts
- ✅ Handle HTTP errors gracefully
- ✅ Never log API keys or tokens
- ✅ Return structured error responses
- ✅ Include retry logic for transient failures (optional)

## Tool Registration Lifecycle

### 1. Discovery Phase (Runtime Startup)

```python
# Simplified runtime workflow loading
workflow_dir = Path("workflows") / workflow_name
tools_manifest_path = workflow_dir / "tools.json"

with open(tools_manifest_path) as f:
    tools_manifest = json.load(f)

for tool_entry in tools_manifest["tools"]:
    tool_name = tool_entry["function"]
    tool_file = tool_entry["file"]
    tool_path = workflow_dir / "tools" / tool_file
    
    # Dynamically import tool module
    spec = importlib.util.spec_from_file_location(tool_name, tool_path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    
    # Get callable function
    tool_func = getattr(module, tool_name)
    
    # Store in registry
    tools_registry[tool_name] = {
        "callable": tool_func,
        "metadata": tool_entry,
    }
```

### 2. Agent Registration Phase

```python
# For each agent, register only tools it owns
for agent_name, agent_config in agents_manifest["agents"].items():
    owned_tools = [
        tool for tool in tools_manifest["tools"]
        if tool["agent"] == agent_name
    ]
    
    # Convert to AG2 function definitions
    ag2_tools = []
    for tool in owned_tools:
        if tool["tool_type"] == "Agent_Tool":
            # Register as callable AG2 function
            ag2_tools.append(tools_registry[tool["function"]]["callable"])
    
    # Create AG2 agent with registered tools
    agent = AssistantAgent(
        name=agent_name,
        system_message=agent_config["system_message"],
        llm_config=llm_config,
        max_consecutive_auto_reply=agent_config["max_consecutive_auto_reply"],
        function_map={t["function"]: tools_registry[t["function"]]["callable"] for t in owned_tools}
    )
```

### 3. Runtime Execution Phase

**Agent_Tool Execution:**
```
1. Agent invokes tool via AG2 function calling
2. AG2 validates parameters against function signature
3. AG2 calls registered tool callable
4. Tool executes and returns dict
5. AG2 includes result in next agent message
```

**UI_Tool Execution (Auto-Tool Mode):**
```
1. Agent emits structured JSON output
2. Runtime validates against structured_outputs.json
3. Runtime detects auto_tool_mode: true
4. Runtime finds UI_Tool owned by agent
5. Runtime invokes tool callable
6. Tool calls use_ui_tool(...)
7. Runtime emits chat.tool_call event
8. Frontend renders component
9. User interacts and submits
10. Frontend emits chat.tool_response
11. Runtime correlates response
12. Runtime passes response to agent
```

## Troubleshooting

### Tool Not Found Error

**Symptom:** `ToolNotFoundError: Tool 'example_tool' not registered`

**Checks:**
1. Verify `tools.json` includes entry for `example_tool`
2. Check `file` field matches actual filename in `workflows/{workflow}/tools/`
3. Ensure function name matches file stem
4. Verify no syntax errors in tool file
5. Check `workflows/{workflow}/tools/__init__.py` exists

**Fix:**
```powershell
# Verify file exists
ls workflows\YourWorkflow\tools\example_tool.py

# Check function defined
Select-String "async def example_tool" workflows\YourWorkflow\tools\example_tool.py

# Validate JSON syntax
python -m json.tool workflows\YourWorkflow\tools.json
```

### UI Component Not Rendering

**Symptom:** Tool called but frontend shows error or blank

**Checks:**
1. React component exported in `ChatUI/src/workflows/{workflow}/components/index.js`
2. `tools.json` `ui.component` matches component name exactly (case-sensitive)
3. Component implements required props
4. Check browser console for JavaScript errors

**Fix:**
```javascript
// Verify export in index.js
export { default as ExampleComponent } from './ExampleComponent';

// Check component signature
const ExampleComponent = ({ payload, onResponse, onCancel, ...props }) => {
  // Must implement these props
};
```

### Auto-Tool Not Invoking

**Symptom:** Agent emits JSON but UI tool never executes

**Checks:**
1. Agent has `auto_tool_mode: true` in `agents.json`
2. Agent has `structured_outputs_required: true`
3. Tool owned by agent has `tool_type: "UI_Tool"`
4. Agent registered in `structured_outputs.json` registry
5. Agent output JSON matches registered schema

**Debug:**
```json
// agents.json
{
  "AgentName": {
    "auto_tool_mode": true,  // ✅ Must be true
    "structured_outputs_required": true  // ✅ Must be true
  }
}

// tools.json
{
  "agent": "AgentName",  // ✅ Must match
  "tool_type": "UI_Tool",  // ✅ Must be UI_Tool
  "ui": { "component": "...", "mode": "..." }  // ✅ Must have ui object
}

// structured_outputs.json
{
  "registry": {
    "AgentName": "ModelName"  // ✅ Must be registered
  }
}
```

### Payload Key Mismatch

**Symptom:** Frontend component receives `undefined` for payload fields

**Root Cause:** Backend emits camelCase but frontend expects snake_case (or vice versa)

**Fix:** Always use snake_case for payload keys in Python tools:
```python
# ❌ WRONG
payload = {
    "agentMessage": "Hello",  # camelCase
    "serviceId": 123
}

# ✅ CORRECT
payload = {
    "agent_message": "Hello",  # snake_case
    "service_id": 123
}
```

Frontend accesses with snake_case:
```javascript
// ✅ CORRECT
const message = payload.agent_message;
const serviceId = payload.service_id;
```

### Tool Timeout Error

**Symptom:** `UIToolError: Tool response timeout after 60s`

**Checks:**
1. User might not have interacted with UI component
2. Component might have JavaScript error preventing response
3. WebSocket connection might be broken
4. Response not properly calling `onResponse` callback

**Fix:**
- Check browser console for errors
- Verify component calls `onResponse` on submit
- Ensure response includes required fields (`status`, `data`)
- Consider increasing timeout for complex workflows (not recommended; fix root cause)

## Best Practices

### Tool Design

1. **Single Responsibility**: Each tool should do one thing well
2. **Minimal Parameters**: Only include parameters agent needs to provide
3. **Clear Descriptions**: Write descriptions for LLM understanding (≤140 chars)
4. **Defensive Validation**: Check required fields; return errors gracefully
5. **Structured Returns**: Always return dict with `status` key

### Security

1. **Never Log Secrets**: No API keys, tokens, passwords in logs
2. **Never Return Secrets**: Return metadata only (length, status)
3. **Mask Sensitive Inputs**: Use `maskInput: true` for credentials
4. **Sanitize Errors**: Don't leak internal paths or stack traces to frontend
5. **Validate Inputs**: Check parameter types and ranges before processing

### Performance

1. **Async Operations**: Use `async/await` for I/O operations
2. **Timeouts**: Set reasonable timeouts for external calls (30s max)
3. **Connection Pooling**: Reuse HTTP clients when making multiple requests
4. **Lazy Loading**: Import heavy dependencies inside functions if only sometimes needed
5. **Caching**: Cache expensive computations when safe (context-aware)

### Maintainability

1. **Consistent Naming**: Follow snake_case for all tool names
2. **Comprehensive Docstrings**: Document behavior, payload, returns, errors
3. **Type Annotations**: Use `Annotated` for LLM-visible parameters
4. **Error Messages**: Provide actionable error messages for debugging
5. **Logging**: Log at appropriate levels (info for normal, warning for retries, error for failures)

## Next Steps

- **[Structured Outputs Guide](./structured_outputs.md)**: Define Pydantic schemas for auto-tool agents
- **[UI Tool Pipeline](./ui_tool_pipeline.md)**: Deep dive into agent-to-frontend interaction flow
- **[Auto-Tool Execution](./auto_tool_execution.md)**: Understand automatic tool invocation and deduplication
- **[Workflow Authoring Guide](./workflow_authoring.md)**: Comprehensive workflow creation guide
- **[Event Pipeline](../runtime/event_pipeline.md)**: Event routing and correlation mechanics
- **[Transport and Streaming](../runtime/transport_and_streaming.md)**: WebSocket communication layer
