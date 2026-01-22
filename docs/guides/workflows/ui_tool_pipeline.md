# UI Tool Pipeline Guide

## Purpose

This document explains the complete lifecycle of UI tools in MozaiksAI, from agent invocation through frontend rendering to user response handling. Understanding this pipeline is essential for building interactive workflows that seamlessly bridge AG2 agents and React components.

## Overview

**UI Tools** enable agents to request user input, display complex artifacts, and trigger interactive frontend experiences. Unlike backend-only Agent_Tools, UI Tools are **bidirectional**: they emit events to the frontend, wait for user interaction, and receive responses that agents can process.

**Key Characteristics:**
- **Event-Driven**: Communication via WebSocket events (`chat.tool_call` and `chat.tool_response`)
- **Correlation-Based**: Event IDs ensure responses match requests across async boundaries
- **Type-Safe**: Structured outputs validate agent emissions; payload contracts define component props
- **Display-Aware**: Support `artifact` (full-screen) and `inline` (embedded) rendering modes
- **Auto-Invocable**: Runtime can automatically invoke UI tools when agents emit structured outputs

## Pipeline Architecture

```
┌──────────────────────────────────────────────────────────────────────────┐
│                          UI Tool Pipeline                                 │
└──────────────────────────────────────────────────────────────────────────┘

Agent Layer (Python)
     │
     ├─> 1. Agent emits structured JSON (auto_tool_mode: true)
     │      OR explicitly calls UI tool (auto_tool_mode: false)
     │
     └─> 2. Runtime validates structured output (if required)
             │
Runtime Layer (FastAPI + AG2)             
     │
     ├─> 3. Runtime invokes UI tool implementation
     │      workflows/{workflow}/tools/{tool_name}.py
     │
     ├─> 4. Tool calls use_ui_tool(component, payload, ...)
     │      core/workflow/ui_tools.py
     │
     ├─> 5. use_ui_tool emits chat.tool_call event
     │      Via SimpleTransport WebSocket
     │
     ├─> 6. Runtime waits for response (await transport.wait_for_ui_tool_response)
     │      Correlation via ui_tool_id
     │
Transport Layer (WebSocket)
     │
     └─> 7. Frontend receives chat.tool_call event
             │
Frontend Layer (React)
     │
     ├─> 8. WorkflowUIRouter extracts component metadata
     │      From event.data.component_type
     │
     ├─> 9. Dynamic component import
     │      import(`./workflows/${workflow}/components/${component}`)
     │
     ├─> 10. Component renders with payload
     │       <Component payload={event.data.payload} onResponse={...} />
     │
     ├─> 11. User interacts (submit, cancel, approve, etc.)
     │
     └─> 12. Component calls onResponse callback
              │
Transport Layer (WebSocket)
     │
     └─> 13. Frontend emits chat.tool_response event
              Via WebSocket with correlation ui_tool_id
              │
Runtime Layer
     │
     ├─> 14. SimpleTransport correlates response
     │       Matches ui_tool_id to pending request
     │
     ├─> 15. Runtime resolves awaiting tool function
     │       Returns response dict to tool
     │
     └─> 16. Tool returns response to agent
              │
Agent Layer
     │
     └─> 17. Agent receives user response
             Continues workflow with user input
```

## Pipeline Phases

### Phase 1: Agent Emission

**Trigger:** Agent completes turn and needs user interaction

**Auto-Tool Mode (Recommended):**

```python
# agents.json
{
  "ContextAgent": {
    "auto_tool_mode": true,
    "structured_outputs_required": true
  }
}

# Agent emits structured JSON
{
  "ActionPlan": {
    "workflow": { /* workflow structure */ }
  },
  "agent_message": "Review the proposed workflow blueprint"
}
```

**Manual Tool Call (Alternative):**

```python
# agents.json
{
  "CustomAgent": {
    "auto_tool_mode": false
  }
}

# Agent explicitly calls tool
Agent: "I need user confirmation. Calling display_confirmation tool..."
[Tool Call: display_confirmation(message="Proceed with deployment?")]
```

**What Happens:**
- Agent output captured by AG2 runtime
- If `auto_tool_mode: true`, runtime validates against schema
- Runtime determines which tool to invoke
- Tool invocation prepared with agent output as parameters

### Phase 2: Tool Invocation

**Entry Point:** Runtime calls tool function

```python
# workflows/Generator/tools/action_plan.py
async def action_plan(
    ActionPlan: dict,
    agent_message: str,
    context_variables: Any = None,
) -> dict:
    """Render the Action Plan artifact for user review."""
    
    # Extract runtime context
    chat_id = context_variables.get('chat_id')
    workflow_name = context_variables.get('workflow_name')
    
    # Build payload for React component
    payload = {
        "ActionPlan": ActionPlan,
        "agent_message": agent_message,
        "agent_message_id": f"msg_{uuid.uuid4().hex[:10]}",
    }
    
    # Emit UI tool event and wait for response
    response = await use_ui_tool(
        "ActionPlan",           # React component name
        payload,                 # Props sent to component
        chat_id=chat_id,
        workflow_name=workflow_name,
        display="artifact",     # Full-screen overlay
    )
    
    return response
```

**Key Operations:**
1. **Context Extraction**: Get `chat_id` and `workflow_name` from `context_variables`
2. **Payload Construction**: Build dict with all data component needs
3. **Correlation ID**: Generate unique `agent_message_id` for threading
4. **use_ui_tool Call**: Emit event and enter wait state
5. **Response Return**: Pass user response back to agent

### Phase 3: Event Emission

**Function:** `core/workflow/ui_tools.py::use_ui_tool`

```python
async def use_ui_tool(
    tool_id: str,                  # React component name (PascalCase)
    payload: Dict[str, Any],       # Component props (all snake_case keys)
    *,
    chat_id: Optional[str],        # Chat session ID
    workflow_name: str,            # Workflow name
    display: str = "inline",       # "artifact" or "inline"
    timeout: float | None = None,  # Response timeout (default: None = wait indefinitely)
) -> Dict[str, Any]:
    """Single-call convenience: emit then wait for a UI tool response."""
    
    # Step 1: Generate unique event ID
    event_id = f"{tool_id}_{uuid.uuid4().hex[:8]}"
    
    # Step 2: Emit chat.tool_call event via SimpleTransport
    await transport.send_ui_tool_event(
        event_id=event_id,
        chat_id=chat_id,
        tool_name=tool_id,
        component_name=tool_id,
        display_type=display,
        payload=payload,
    )
    
    # Step 3: Wait for correlated response
    response = await transport.wait_for_ui_tool_response(event_id, timeout=None)
    
    # Step 4: Augment response with event ID
    if 'ui_event_id' not in response:
        response['ui_event_id'] = event_id
    
    return response
```

**Event Structure (chat.tool_call):**

```json
{
  "type": "chat.tool_call",
  "data": {
    "kind": "tool_call",
    "tool_name": "ActionPlan",
    "component_type": "ActionPlan",
    "payload": {
      "ActionPlan": { /* workflow structure */ },
      "agent_message": "Review the proposed workflow blueprint",
      "agent_message_id": "msg_abc123",
      "workflow_name": "Generator",
      "display": "artifact"
    },
    "corr": "ui_tool_abc12345",
    "awaiting_response": true,
    "display": "artifact"
  },
  "timestamp": "2025-10-02T14:32:15.123Z"
}
```

**Key Fields:**
- `type`: Always `"chat.tool_call"` for UI tool events
- `data.kind`: Always `"tool_call"` (distinguishes from other event types)
- `data.tool_name`: Tool identifier (matches `tools.json`)
- `data.component_type`: React component name to render
- `data.payload`: Props passed to component (snake_case keys)
- `data.corr`: Correlation ID (`ui_tool_id`) for response matching
- `data.awaiting_response`: `true` when runtime is waiting for user input
- `data.display`: Rendering mode (`"artifact"` or `"inline"`)

### Phase 4: Transport Layer

**Component:** `core/transport/simple_transport.py`

**Responsibilities:**
1. **WebSocket Management**: Maintain persistent connection per chat session
2. **Event Routing**: Send events to correct frontend clients
3. **Response Correlation**: Match incoming `chat.tool_response` to pending requests
4. **Timeout Handling**: (Optional) Cancel waits after timeout period

**Key Methods:**

```python
class SimpleTransport:
    async def send_ui_tool_event(
        self,
        event_id: str,
        chat_id: str,
        tool_name: str,
        component_name: str,
        display_type: str,
        payload: dict,
        agent_name: Optional[str] = None,
    ):
        """Emit chat.tool_call event to frontend."""
        event = {
            "type": "chat.tool_call",
            "data": {
                "kind": "tool_call",
                "tool_name": tool_name,
                "component_type": component_name,
                "payload": payload,
                "corr": f"ui_tool_{event_id}",
                "awaiting_response": True,
                "display": display_type,
            },
            "timestamp": datetime.now(timezone.utc).isoformat(),
        }
        await self.send_event_to_ui(event, chat_id)
    
    def wait_for_ui_tool_response(
        self,
        event_id: str,
        timeout: Optional[float] = None
    ) -> asyncio.Future:
        """Create awaitable future that resolves when response received."""
        correlation_id = f"ui_tool_{event_id}"
        future = asyncio.Future()
        
        # Store in pending requests map
        self._pending_ui_responses[correlation_id] = future
        
        return future
    
    async def handle_ui_tool_response(self, response_data: dict):
        """Process incoming chat.tool_response event."""
        correlation_id = response_data.get("corr") or response_data.get("ui_tool_id")
        
        if correlation_id in self._pending_ui_responses:
            future = self._pending_ui_responses.pop(correlation_id)
            
            if not future.done():
                future.set_result(response_data.get("data", {}))
```

**Correlation Mechanism:**

```
Emit Event:
  event_id = "abc12345"
  correlation_id = "ui_tool_abc12345"
  
  Store:
    _pending_ui_responses["ui_tool_abc12345"] = Future()
  
  Emit:
    { "corr": "ui_tool_abc12345", ... }

Receive Response:
  { "corr": "ui_tool_abc12345", "data": {...} }
  
  Match:
    future = _pending_ui_responses.pop("ui_tool_abc12345")
    future.set_result(response_data["data"])
  
  Awaiting tool function receives result and continues
```

### Phase 5: Frontend Reception

**Component:** `ChatUI/src/core/WorkflowUIRouter.jsx`

**Responsibilities:**
1. **Event Filtering**: Extract `chat.tool_call` events from WebSocket stream
2. **Component Resolution**: Dynamically import workflow-specific components
3. **Props Injection**: Pass payload and callbacks to component
4. **Rendering**: Display component in correct mode (artifact/inline)

**Key Logic:**

```javascript
const WorkflowUIRouter = ({ event, onResponse, onCancel }) => {
  const { component_type, payload, display } = event.data;
  const workflow_name = payload.workflow_name || payload.sourceWorkflowName;
  
  // Step 1: Resolve component path
  const componentPath = `./workflows/${workflow_name}/components/${component_type}`;
  
  // Step 2: Dynamically import component
  const [Component, setComponent] = useState(null);
  
  useEffect(() => {
    import(componentPath)
      .then(module => setComponent(() => module.default))
      .catch(err => console.error(`Failed to load ${component_type}:`, err));
  }, [componentPath]);
  
  if (!Component) return <LoadingSpinner />;
  
  // Step 3: Render component with props
  return (
    <Component
      payload={payload}
      onResponse={onResponse}
      onCancel={onCancel}
      ui_tool_id={event.data.corr}
      eventId={event.data.corr}
      workflowName={workflow_name}
      sourceWorkflowName={payload.sourceWorkflowName}
      generatedWorkflowName={payload.generatedWorkflowName}
    />
  );
};
```

**Display Mode Handling:**

```javascript
// Artifact mode: Full-screen overlay
if (display === 'artifact') {
  return (
    <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center">
      <WorkflowUIRouter event={event} onResponse={handleResponse} onCancel={handleCancel} />
    </div>
  );
}

// Inline mode: Embedded in chat
return (
  <div className="inline-component-container">
    <WorkflowUIRouter event={event} onResponse={handleResponse} onCancel={handleCancel} />
  </div>
);
```

### Phase 6: Component Rendering

**Example Component:** `ChatUI/src/workflows/Generator/components/AgentAPIKeyInput.js`

```javascript
import React, { useState } from 'react';
import { typography, components, spacing, layouts } from '../../../styles/artifactDesignSystem';

const AgentAPIKeyInput = ({ 
  payload = {},
  onResponse,
  onCancel,
  ui_tool_id,
  eventId,
  workflowName,
  sourceWorkflowName,
  generatedWorkflowName,
}) => {
  // Extract configuration from payload
  const {
    service = "openai",
    label = "API Key",
    agent_message = "Please provide your API key",
    description = "Enter your API key to continue",
    placeholder = "Enter API key...",
    required = true,
    maskInput = true,
    agent_message_id,
  } = payload;
  
  // Resolve workflow name (prefer generated for user-created workflows)
  const resolvedWorkflowName = generatedWorkflowName || workflowName || sourceWorkflowName;
  
  // Component state
  const [apiKey, setApiKey] = useState('');
  const [isVisible, setIsVisible] = useState(!maskInput);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');
  
  // Validation
  const validateApiKey = (key) => {
    if (!key.trim() && required) {
      return 'API key is required';
    }
    return null;
  };
  
  // Submit handler
  const handleSubmit = async (e) => {
    e.preventDefault();
    
    const validationError = validateApiKey(apiKey);
    if (validationError) {
      setError(validationError);
      return;
    }
    
    setIsSubmitting(true);
    
    // Build response matching contract
    const response = {
      status: 'success',
      action: 'submit',
      data: {
        service,
        apiKey: apiKey.trim(),        // Secure transmission
        hasApiKey: true,
        keyLength: apiKey.length,
        submissionTime: new Date().toISOString(),
        ui_tool_id,                   // Correlation ID
        eventId,                       // Correlation ID
        workflowName: resolvedWorkflowName,
        sourceWorkflowName,
        generatedWorkflowName,
        agent_message_id,              // Message threading
      }
    };
    
    onResponse(response);
  };
  
  // Cancel handler
  const handleCancel = () => {
    const response = {
      status: 'error',
      action: 'cancel',
      message: 'User cancelled API key input',
      code: 'user_cancelled',
      data: {
        ui_tool_id,
        eventId,
        workflowName: resolvedWorkflowName,
      }
    };
    onCancel(response);
  };
  
  return (
    <div className={layouts.artifactContainer} data-agent-message-id={agent_message_id}>
      <div className={components.card.primary}>
        {/* Agent Message (Context) */}
        {agent_message && (
          <p className={typography.body.md + " mb-4"}>{agent_message}</p>
        )}
        
        {/* Input Form */}
        <form onSubmit={handleSubmit} className={spacing.section}>
          <label className={typography.label.md}>
            {label}
          </label>
          
          {description && (
            <p className={typography.body.sm + " text-gray-600 mb-2"}>
              {description}
            </p>
          )}
          
          <div className="relative">
            <input
              type={isVisible ? "text" : "password"}
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              placeholder={placeholder}
              className={components.input.primary}
              required={required}
            />
            
            {maskInput && (
              <button
                type="button"
                onClick={() => setIsVisible(!isVisible)}
                className="absolute right-2 top-2"
              >
                {isVisible ? "🙈" : "👁️"}
              </button>
            )}
          </div>
          
          {error && (
            <p className="text-red-500 text-sm mt-2">{error}</p>
          )}
          
          {/* Action Buttons */}
          <div className="flex gap-4 mt-4">
            <button
              type="submit"
              disabled={isSubmitting}
              className={components.button.primary}
            >
              {isSubmitting ? 'Submitting...' : 'Submit'}
            </button>
            
            <button
              type="button"
              onClick={handleCancel}
              disabled={isSubmitting}
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

export default AgentAPIKeyInput;
```

**Component Contract (Required Props):**

| Prop | Type | Description |
|------|------|-------------|
| `payload` | object | Tool-specific data from backend (snake_case keys) |
| `onResponse` | function | Callback to submit user response |
| `onCancel` | function | Callback to cancel interaction |
| `ui_tool_id` | string | Correlation ID for response matching |
| `eventId` | string | Event ID (often same as ui_tool_id) |
| `workflowName` | string | Source workflow name (legacy) |
| `sourceWorkflowName` | string | Original workflow name |
| `generatedWorkflowName` | string | User-created workflow name (if applicable) |

**Response Contract:**

**Success Response:**
```javascript
{
  status: 'success',
  action: 'submit',  // or 'confirm', 'approve', etc.
  data: {
    // Tool-specific result fields
    custom_field: 'user_input_value',
    
    // Required correlation fields
    ui_tool_id: 'ui_tool_abc12345',
    eventId: 'ui_tool_abc12345',
    workflowName: 'Generator',
    sourceWorkflowName: 'Generator',
    generatedWorkflowName: null,
    agent_message_id: 'msg_abc123',
  }
}
```

**Error/Cancel Response:**
```javascript
{
  status: 'error',
  action: 'cancel',
  message: 'User cancelled operation',
  code: 'user_cancelled',  // Optional error code
  data: {
    ui_tool_id: 'ui_tool_abc12345',
    eventId: 'ui_tool_abc12345',
    workflowName: 'Generator',
  }
}
```

### Phase 7: Response Emission

**Trigger:** User clicks submit/cancel button

**Frontend Action:**

```javascript
const handleResponse = (response) => {
  // Emit chat.tool_response event
  const event = {
    type: 'chat.tool_response',
    data: {
      corr: response.data.ui_tool_id,  // Match request correlation ID
      ...response,
    },
    timestamp: new Date().toISOString(),
  };
  
  websocket.send(JSON.stringify(event));
};
```

**WebSocket Message:**

```json
{
  "type": "chat.tool_response",
  "data": {
    "corr": "ui_tool_abc12345",
    "status": "success",
    "action": "submit",
    "data": {
      "service": "openai",
      "hasApiKey": true,
      "keyLength": 48,
      "ui_tool_id": "ui_tool_abc12345",
      "workflowName": "Generator"
    }
  },
  "timestamp": "2025-10-02T14:35:42.789Z"
}
```

### Phase 8: Response Correlation

**Backend Reception:** `SimpleTransport.handle_ui_tool_response`

```python
async def handle_ui_tool_response(self, response_data: dict):
    """Process incoming chat.tool_response event."""
    correlation_id = response_data.get("corr") or response_data.get("ui_tool_id")
    
    if not correlation_id:
        logger.warning("Received UI tool response without correlation ID")
        return
    
    # Find pending request
    if correlation_id in self._pending_ui_responses:
        future = self._pending_ui_responses.pop(correlation_id)
        
        if not future.done():
            # Resolve awaiting use_ui_tool call
            future.set_result(response_data.get("data", {}))
            logger.info(f"✅ Resolved UI tool response: {correlation_id}")
        else:
            logger.warning(f"Future already resolved for: {correlation_id}")
    else:
        logger.warning(f"No pending request found for: {correlation_id}")
```

**Flow:**

1. Frontend sends `chat.tool_response` with `corr: "ui_tool_abc12345"`
2. Backend receives WebSocket message
3. SimpleTransport extracts correlation ID
4. Looks up `_pending_ui_responses["ui_tool_abc12345"]`
5. Finds Future object created by `wait_for_ui_tool_response`
6. Resolves Future with response data
7. Awaiting `use_ui_tool` function receives result
8. Tool function returns response to agent

### Phase 9: Agent Continuation

**Agent Receives Response:**

```python
# Agent's perspective (simplified)
Agent: "I requested the user's API key. Waiting for response..."

# use_ui_tool completes
response = {
  "status": "success",
  "data": {
    "service": "openai",
    "hasApiKey": True,
    "keyLength": 48
  }
}

Agent: "User provided API key (length: 48). Proceeding with OpenAI integration..."
```

**Agent System Message Guidance:**

```
[INSTRUCTIONS]
...
Step 3 - After receiving the API key:
- Acknowledge the credential collection (never echo the key itself)
- Proceed to the next workflow phase
- Reference the service name when describing next steps

[EXAMPLE RESPONSE]
"OpenAI API key received. Moving forward with content generation setup..."
```

## Display Modes

### Artifact Mode (Full-Screen)

**When to Use:**
- Complex, multi-section visualizations (workflow blueprints, dashboards)
- Large data tables or detailed reports
- Multi-step forms requiring user's full attention
- Any UI that would be cramped in inline mode

**Configuration:**

```python
# Python tool
response = await use_ui_tool(
    "ActionPlan",
    payload,
    chat_id=chat_id,
    workflow_name=workflow_name,
    display="artifact",  # Full-screen overlay
)
```

```json
// tools.json
{
  "ui": {
    "component": "ActionPlan",
    "mode": "artifact"
  }
}
```

**Frontend Rendering:**

```javascript
// Renders in full-screen overlay
<div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center">
  <div className="max-w-6xl w-full max-h-screen overflow-auto bg-white rounded-lg">
    <ActionPlan payload={payload} onResponse={onResponse} onCancel={onCancel} />
  </div>
</div>
```

**User Experience:**
- ✅ Blocks conversation until interaction complete
- ✅ Forces focus on artifact
- ✅ Suitable for critical decisions (approvals, large inputs)
- ❌ More disruptive than inline mode
- ❌ Not mobile-friendly for complex layouts

### Inline Mode (Embedded)

**When to Use:**
- Simple inputs (text fields, dropdowns, checkboxes)
- Lightweight confirmations
- Progress indicators or status updates
- Quick actions that don't require full attention

**Configuration:**

```python
# Python tool
response = await use_ui_tool(
    "AgentAPIKeyInput",
    payload,
    chat_id=chat_id,
    workflow_name=workflow_name,
    display="inline",  # Embedded in chat
)
```

```json
// tools.json
{
  "ui": {
    "component": "AgentAPIKeyInput",
    "mode": "inline"
  }
}
```

**Frontend Rendering:**

```javascript
// Renders inline within chat message flow
<div className="inline-component-container max-w-2xl">
  <AgentAPIKeyInput payload={payload} onResponse={onResponse} onCancel={onCancel} />
</div>
```

**User Experience:**
- ✅ Less disruptive; conversation flows around component
- ✅ Mobile-friendly for simple interactions
- ✅ Suitable for non-critical inputs
- ❌ Limited space for complex UIs
- ❌ May get lost in long conversation threads

## Error Handling

### Tool Execution Errors

**Scenario:** Tool implementation throws exception

```python
async def example_tool(param: str, context_variables: Any = None) -> dict:
    try:
        # Business logic
        result = await some_operation(param)
        
        payload = {"result": result}
        response = await use_ui_tool("ExampleComponent", payload, ...)
        
        return response
    except Exception as e:
        logger.error(f"Tool execution failed: {e}")
        return {
            "status": "error",
            "message": f"Tool execution failed: {str(e)}",
            "code": "tool_error"
        }
```

**Agent Receives:**
```python
{
  "status": "error",
  "message": "Tool execution failed: Connection timeout",
  "code": "tool_error"
}
```

**Agent Can:**
- Retry with different parameters
- Skip optional operation
- Inform user of failure
- Terminate workflow

### Frontend Errors

**Scenario:** Component fails to load or render

```javascript
// WorkflowUIRouter error boundary
componentDidCatch(error, errorInfo) {
  console.error("Component render error:", error);
  
  // Send error response back to agent
  this.props.onResponse({
    status: 'error',
    message: `Component render failed: ${error.message}`,
    code: 'component_error',
    data: {
      ui_tool_id: this.props.event.data.corr,
      error_details: error.toString(),
    }
  });
}
```

**Backend Receives:**
```python
{
  "status": "error",
  "message": "Component render failed: Cannot read property 'workflow' of undefined",
  "code": "component_error"
}
```

### User Cancellation

**Scenario:** User clicks cancel button

```javascript
const handleCancel = () => {
  const response = {
    status: 'error',
    action: 'cancel',
    message: 'User cancelled operation',
    code: 'user_cancelled',
    data: {
      ui_tool_id,
      eventId,
      workflowName,
    }
  };
  
  onCancel(response);
};
```

**Agent System Message Handling:**

```
[INSTRUCTIONS]
...
Step 5 - If user cancels:
- Acknowledge the cancellation politely
- Offer alternative paths or ask if user wants to retry
- Do not repeat the same UI tool request immediately

[EXAMPLE RESPONSE]
"Understood. Would you like to try a different approach, or shall we skip this step?"
```

### WebSocket Disconnection

**Scenario:** Connection drops during UI interaction

**SimpleTransport Handling:**

```python
async def handle_disconnect(self, chat_id: str):
    """Clean up pending requests when connection lost."""
    # Find all pending requests for this chat
    to_cancel = [
        corr_id for corr_id, future in self._pending_ui_responses.items()
        if chat_id in corr_id
    ]
    
    for corr_id in to_cancel:
        future = self._pending_ui_responses.pop(corr_id)
        if not future.done():
            future.set_exception(
                UIToolError("WebSocket connection lost during UI interaction")
            )
```

**Tool Error:**
```python
try:
    response = await use_ui_tool(...)
except UIToolError as e:
    logger.error(f"UI tool failed: {e}")
    return {
        "status": "error",
        "message": "Connection lost. Please refresh and try again.",
        "code": "connection_lost"
    }
```

## Debugging

### Enable Debug Logging

**Backend:**

```bash
# Set environment variable
export LOGS_AS_JSON=false  # Human-readable logs
export LOG_LEVEL=DEBUG     # Verbose logging

# Check logs for UI tool flow
tail -f logs/runtime_*.log | grep "UI_TOOLS"
```

**Log Output:**

```
🎯 UI tool event: ActionPlan (event=ui_tool_abc12345, display=artifact, payload_keys=['ActionPlan', 'agent_message', ...])
✅ Emitted UI tool event: ui_tool_abc12345
⏳ Waiting for user interaction on UI tool 'ActionPlan'
✅ Received user response for tool 'ActionPlan'
⏱️ Round-trip tool_id=ActionPlan event=ui_tool_abc12345 duration_ms=15234.56
```

**Frontend:**

```javascript
// Enable verbose logging
localStorage.setItem('debug_ui_tools', 'true');

// Check console for event flow
// Output:
// 🔵 Received chat.tool_call: ActionPlan
// 🟢 Component loaded: ActionPlan
// 🟡 User submitted response
// 🟣 Emitted chat.tool_response: ui_tool_abc12345
```

### Common Issues

**Component Not Rendering:**

1. Check component export:
   ```javascript
   // components/index.js
   export { default as ActionPlan } from './ActionPlan';
   ```

2. Verify component name matches:
   ```json
   // tools.json: "component": "ActionPlan"
   // File: ActionPlan.js
   // Export: export default ActionPlan
   ```

3. Check browser console for import errors

**Response Not Received:**

1. Verify correlation ID matches:
   ```javascript
   // Request: corr: "ui_tool_abc12345"
   // Response: corr: "ui_tool_abc12345" (must match exactly)
   ```

2. Check WebSocket connection:
   ```javascript
   console.log(websocket.readyState);  // Should be 1 (OPEN)
   ```

3. Inspect response payload structure:
   ```javascript
   // Must include status field
   { status: 'success', data: { ... } }
   ```

**Payload Mismatch:**

1. Backend sends snake_case keys:
   ```python
   {"agent_message": "Hello"}
   ```

2. Frontend accesses snake_case:
   ```javascript
   payload.agent_message  // ✅ CORRECT
   payload.agentMessage   // ❌ WRONG (undefined)
   ```

## Best Practices

### Tool Implementation

1. **Always Extract Context**: Get `chat_id` and `workflow_name` from `context_variables`
2. **Validate Payload**: Check required fields before calling `use_ui_tool`
3. **Generate Correlation IDs**: Create unique `agent_message_id` for threading
4. **Handle Errors Gracefully**: Return error dicts; don't raise exceptions
5. **Security First**: Never log or return secrets in payloads

### Component Design

1. **Destructure Payload**: Extract fields with defaults at top of component
2. **Resolve Workflow Name**: Use `generatedWorkflowName || workflowName || sourceWorkflowName`
3. **Include Correlation Fields**: Always include `ui_tool_id`, `eventId` in responses
4. **Validate User Input**: Check required fields before submission
5. **Provide Feedback**: Show loading states, validation errors, success messages

### Event Handling

1. **Use Correlation IDs**: Ensure requests and responses match via `corr` field
2. **Set Awaiting Response**: Mark events with `awaiting_response: true` when expecting user input
3. **Handle Timeouts**: Consider user abandonment; don't wait indefinitely (unless intentional)
4. **Clean Up Pending**: Remove resolved/cancelled requests from pending maps

### Display Mode Selection

1. **Artifact for Complexity**: Use full-screen for multi-section UIs
2. **Inline for Speed**: Use embedded mode for quick interactions
3. **Mobile Consideration**: Test artifact mode on small screens
4. **User Context**: Preserve conversation context around inline components

## Next Steps

- **[Auto-Tool Execution](./auto_tool_execution.md)**: Understand automatic tool invocation mechanics
- **[Tool Manifest Reference](./tool_manifest.md)**: Complete tool registration guide
- **[Structured Outputs Guide](./structured_outputs.md)**: Schema design for auto-tool agents
- **[Workflow Authoring Guide](./workflow_authoring.md)**: Complete workflow creation workflow
- **[Event Pipeline](../runtime/event_pipeline.md)**: Deep dive into event routing and correlation
- **[Transport and Streaming](../runtime/transport_and_streaming.md)**: WebSocket communication layer
- **[UI Components Guide](../frontend/ui_components.md)**: React component development patterns
