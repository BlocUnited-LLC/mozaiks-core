# Mozaiks-Core Runtime Capabilities Analysis

**Research Date:** January 31, 2026  
**Source:** `C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-core\packages\python\ai-runtime\`

---

## Executive Summary

The mozaiks-core AI runtime is built on top of **AG2 (AutoGen)** and provides a complete workflow orchestration system with:
- Multi-agent conversation management via AG2's `ConversableAgent`
- Structured output validation via Pydantic models
- Auto-tool invocation triggered by structured outputs
- Lifecycle hooks for workflow events
- WebSocket-based real-time event transport
- AG-UI protocol compatibility for frontend communication

---

## 1. Runtime Architecture

### 1.1 Core Components

| Component | File | Purpose |
|-----------|------|---------|
| `WorkflowManager` | `workflow/workflow_manager.py` | Loads/manages workflow configs, tools, hooks |
| `OrchestrationPatterns` | `workflow/orchestration_patterns.py` | AG2 pattern execution (SwarmPattern, GroupChat) |
| `SimpleTransport` | `transport/simple_transport.py` | WebSocket communication with frontend |
| `UnifiedEventDispatcher` | `events/unified_event_dispatcher.py` | Event routing and emission |
| `AGUIEventAdapter` | `event_agui_adapter.py` | Transforms chat.* events to agui.* events |
| `ActionExecutor` | `action_executor.py` | Stateless artifact action execution |

### 1.2 Workflow Execution Flow

```
1. WebSocket connects → SimpleTransport registers connection
2. Frontend triggers workflow → orchestration_patterns.run_workflow_orchestration()
3. Config loads from YAML → WorkflowManager.get_config()
4. Agents created → agents/factory.create_agents()
5. Tools bound to agents → agents/tools.load_agent_tool_functions()
6. AG2 pattern executes → _stream_events() with SwarmPattern or GroupChat
7. Events stream to frontend → UnifiedEventDispatcher → SimpleTransport
8. Tool calls execute → AutoToolEventHandler or manual invocation
```

### 1.3 Configuration Structure

Workflows are configured via YAML files in `workflows/<workflow_name>/`:

```
workflows/<workflow>/
├── orchestrator.yaml      # Main config: pattern, max_turns, startup_mode
├── agents.yaml           # Agent definitions with prompt_sections
├── handoffs.yaml         # Inter-agent handoff rules (for swarm patterns)
├── tools.yaml            # Tool definitions (Agent_Tool, UI_Tool, lifecycle_tools)
├── structured_outputs.yaml # Pydantic model definitions for agents
├── context_variables.yaml # Context variable definitions
├── hooks.yaml            # AG2 hook registrations
└── tools/                # Python tool implementations
    └── *.py
```

---

## 2. Event System

### 2.1 Event Types Emitted

The runtime emits three categories of events:

#### AG2 Runtime Events (via `kind` field)
```python
# Text/Chat Events
"chat.text"          # Full text message
"chat.print"         # Streaming text chunk

# Tool Events
"chat.tool_call"     # Tool invocation started
"chat.tool_response" # Tool result returned

# Workflow Events
"chat.run_start"     # Workflow started
"chat.run_complete"  # Workflow completed
"chat.error"         # Error occurred

# Structured Output Events
"chat.structured_output_ready"  # Agent produced structured output

# UI Events
"chat.input_request" # Awaiting user input
"chat.input_ack"     # User input acknowledged
"chat.ui_tool_dismiss" # UI component dismissed

# Usage Events
"chat.usage_delta"   # Token usage delta
"chat.usage_summary" # Total usage summary

# Orchestration Events (v1.1)
"chat.orchestration.run_started"
"chat.orchestration.run_completed"
"chat.orchestration.run_failed"
"chat.orchestration.agent_started"
"chat.orchestration.agent_completed"
"chat.orchestration.tool_started"
"chat.orchestration.tool_completed"
```

#### AG-UI Protocol Events (dual emission)
```python
# Lifecycle Events
"agui.lifecycle.RunStarted"
"agui.lifecycle.RunFinished"
"agui.lifecycle.RunError"
"agui.lifecycle.StepStarted"
"agui.lifecycle.StepFinished"

# Text Streaming Events
"agui.text.TextMessageStart"
"agui.text.TextMessageContent"
"agui.text.TextMessageEnd"

# Tool Events
"agui.tool.ToolCallStart"
"agui.tool.ToolCallEnd"
"agui.tool.ToolCallResult"

# State Events
"agui.state.StateSnapshot"
"agui.state.StateDelta"
"agui.state.MessagesSnapshot"
```

### 2.2 Event Envelope Structure

```python
{
    "type": "chat.tool_call",  # Namespaced event type
    "data": {
        "kind": "tool_call",
        "agent": "AgentName",
        "tool_name": "my_tool",
        "tool_call_id": "corr_123",
        "corr": "corr_123",
        "awaiting_response": true,
        "component_type": "ComponentName",  # For UI tools
        "payload": {
            "tool_args": {...},
            "agent_message": "...",
            "interaction_type": "auto_tool"
        }
    },
    "timestamp": "2026-01-31T12:00:00.000Z"
}
```

### 2.3 Event Emission Code Reference

From `unified_event_dispatcher.py`:
```python
# Namespace mapping: internal 'kind' -> WebSocket 'type'
ns_map = {
    'print': 'chat.print',
    'text': 'chat.text',
    'input_request': 'chat.input_request',
    'tool_call': 'chat.tool_call',
    'tool_response': 'chat.tool_response',
    'structured_output_ready': 'chat.structured_output_ready',
    'run_complete': 'chat.run_complete',
    'error': 'chat.error',
    # ... etc
}
```

---

## 3. Tool Calling System

### 3.1 Tool Registration

Tools are registered via `tools.yaml` in each workflow:

```yaml
tools:
  - name: my_tool
    file: my_tools.py
    function: my_function
    agent: AgentName  # Can be string or list of agent names
    tool_type: Agent_Tool  # or UI_Tool
    auto_invoke: true  # Triggers automatic execution from structured outputs
    ui:
      component: MyComponent
      mode: inline  # or artifact
```

### 3.2 Tool Loading Process

From `agents/tools.py`:
```python
def load_agent_tool_functions(workflow_name: str) -> Dict[str, List[Callable]]:
    """
    1. Reads workflows/<workflow>/tools.yaml
    2. For each tool entry:
       - Loads Python module from file path
       - Gets function by name
       - Wraps with validation and logging
       - Adds to agent's function list
    3. Returns mapping: agent_name -> list[callable]
    """
```

### 3.3 Tool Execution Flow

**Manual Tool Execution** (LLM decides to call):
```
1. AG2 LLM generates tool call in response
2. AG2 executes registered function via ConversableAgent.functions
3. Wrapper logs execution to tools.log
4. Result returned to AG2 for next turn
```

**Auto-Tool Execution** (triggered by structured output):
```
1. Agent with auto_tool_mode=true produces structured output
2. structured_output_ready event emitted
3. AutoToolEventHandler.handle_structured_output_ready() triggered
4. Validates output against Pydantic model
5. Finds matching tool binding by model_name
6. Builds kwargs from structured output fields
7. Invokes tool function
8. Emits chat.tool_call and chat.tool_response events
```

### 3.4 Tool Invocation Code Reference

From `auto_tool_handler.py`:
```python
async def _invoke_tool(
    self, binding: AutoToolBinding, kwargs: Dict[str, Any]
) -> tuple[Any, str]:
    try:
        result = binding.function(**kwargs)
        if inspect.isawaitable(result):
            result = await result
        return result, "ok"
    except Exception as exc:
        logger.exception("[AUTO_TOOL] Tool execution failed")
        return {"status": "error", "message": str(exc)}, "error"
```

### 3.5 Tool Error Handling

From `agents/tools.py` wrapper:
```python
try:
    result = await func(*args, **kwargs)
    log_tool_event(tool_logger, action="complete", status="success")
    return result
except Exception as e:
    log_tool_event(
        tool_logger,
        action="error",
        status="error",
        message=f"Tool '{tool_name}' failed: {str(e)}",
        error_type=type(e).__name__
    )
    raise
```

---

## 4. Structured Outputs

### 4.1 Structured Output Support

**YES - The runtime fully supports structured outputs.**

Structured outputs are defined in `structured_outputs.yaml`:

```yaml
models:
  MyOutputModel:
    type: model
    fields:
      field1:
        type: str
        description: "Description"
      field2:
        type: list
        items: str
      field3:
        type: optional_str
        default: null

registry:
  AgentName: MyOutputModel  # Maps agent to output model
```

### 4.2 Supported Field Types

From `outputs/structured.py`:
```python
TYPE_MAP = {
    'str': str,
    'string': str,
    'int': int,
    'bool': bool,
    'optional_str': Optional[str],
    'Optional[str]': Optional[str],
    'list': list,
    'List': list,
    'dict': Dict[str, Any],
    'Dict': Dict[str, Any],
    'float': float,
}
```

Plus:
- `literal` - Creates Enum from values list
- `union` - Union of multiple types
- `list[ModelName]` - List of nested models
- Direct model references

### 4.3 Structured Output Validation

From `outputs/structured.py`:
```python
def build_models_from_config(models_config: Dict[str, Any]) -> Dict[str, type]:
    """
    1. Iterates model definitions
    2. Resolves field types using TYPE_MAP
    3. Creates Pydantic models via create_model()
    4. Patches JSON schema for OpenAI strict mode
    5. Returns mapping: model_name -> Pydantic class
    """
```

### 4.4 Structured Output Events

When an agent produces a structured output:

```python
{
    "kind": "structured_output_ready",
    "agent_name": "AgentName",
    "model_name": "MyOutputModel",
    "structured_data": {...},  # Validated model data
    "auto_tool_mode": true,    # Whether to auto-invoke tool
    "turn_idempotency_key": "...",
    "context": {
        "chat_id": "...",
        "app_id": "...",
        "workflow_name": "..."
    }
}
```

### 4.5 LLM Config with Structured Output

From `outputs/structured.py`:
```python
async def get_llm_for_workflow(
    workflow_name: str,
    flow: str = "base",
    agent_name: Optional[str] = None,
) -> tuple:
    """
    If agent has structured output registered:
    - Returns LLM config with response_format=model_cls
    - OpenAI will constrain output to match schema
    """
    structured_registry = get_structured_outputs_for_workflow(workflow_name)
    if agent_name in structured_registry:
        model_cls = structured_registry[agent_name]
        return await get_llm_config(response_format=model_cls, stream=should_stream)
```

---

## 5. Workflow Execution

### 5.1 Starting a Workflow

Workflows are started via `run_workflow_orchestration()`:

```python
async def run_workflow_orchestration(
    workflow_name: str,
    app_id: str,
    chat_id: str,
    user_id: Optional[str] = None,
    initial_message: Optional[str] = None,
    initial_agent_name_override: Optional[str] = None,
    **kwargs
) -> Any:
    # 1. Load configuration
    cfg = _load_workflow_config(workflow_name)
    
    # 2. Resume or initialize chat
    resumed_messages, initial_messages = await _resume_or_initialize_chat(...)
    
    # 3. Load LLM config
    llm_config = await _load_llm_config(workflow_name)
    
    # 4. Preload structured outputs
    load_workflow_structured_outputs(workflow_name)
    
    # 5. Build context
    context = await _build_context_blocking(...)
    
    # 6. Create agents
    agents = await _create_agents(workflow_name, context_variables=context)
    
    # 7. Create AG2 pattern (Swarm or GroupChat)
    pattern, ag2_context = await _create_ag2_pattern(...)
    
    # 8. Execute lifecycle: before_chat
    await lifecycle_manager.trigger_before_chat(context_variables=ag2_context)
    
    # 9. Stream events
    stream_state = await _stream_events(pattern, ...)
    
    # 10. Execute lifecycle: after_chat
    await lifecycle_manager.trigger_after_chat(context_variables=ag2_context)
    
    return result_payload
```

### 5.2 Agent Communication

Agents communicate via AG2's native patterns:

**SwarmPattern (Default):**
- Agents hand off to each other via `AfterWorkOption.SWARM_MANAGER`
- Handoffs defined in `handoffs.yaml`
- Context passed via AG2's `ContextVariables`

**GroupChat:**
- Agents selected by manager
- Round-robin or selector-based speaker selection

### 5.3 State Management

**Context Variables (AG2 Native):**
```python
# Injected automatically to tools
def my_tool(context_variables):
    chat_id = context_variables.get("chat_id")
    app_id = context_variables.get("app_id")
    # ... tool logic
```

**Session Persistence:**
- Messages persisted to MongoDB via `AG2PersistenceManager`
- Session status tracked (IN_PROGRESS, COMPLETED)
- Resume capability via `resume_groupchat.py`

**Artifact State:**
- UI artifacts can persist state via `_persist_artifact_state()`
- Supports JSON patch operations for incremental updates
- Emits `agui.state.StateSnapshot` and `agui.state.StateDelta` events

---

## 6. Lifecycle Hooks

### 6.1 Available Triggers

From `execution/lifecycle.py`:
```python
class LifecycleTrigger(Enum):
    BEFORE_CHAT = "before_chat"    # Before AG2 run starts
    AFTER_CHAT = "after_chat"      # After AG2 run completes
    BEFORE_AGENT = "before_agent"  # Before specific agent speaks
    AFTER_AGENT = "after_agent"    # After specific agent speaks
```

### 6.2 Lifecycle Tool Definition

In `tools.yaml`:
```yaml
lifecycle_tools:
  - trigger: before_chat
    file: setup.py
    function: initialize_context
    description: "Initialize workflow context"
  
  - trigger: after_agent
    agent: DataAgent
    file: sync.py
    function: sync_results
    description: "Sync results after DataAgent completes"
```

### 6.3 Lifecycle Execution

```python
async def _execute_tools(self, trigger: LifecycleTrigger, ...):
    """
    1. Filter tools by trigger (and agent if applicable)
    2. Execute all matching tools in parallel
    3. Each tool receives context_variables if it accepts them
    4. Errors logged but don't halt workflow
    """
```

---

## 7. Key Integration Points for Platform

### 7.1 Tool Registration from Platform

Platform tools should:
1. Define in `tools.yaml` with `file`, `function`, `agent`
2. Implement as Python functions accepting `context_variables`
3. Return dict with results
4. For UI tools, set `tool_type: UI_Tool` and configure `ui.component`

### 7.2 Structured Output Integration

For auto-invoked tools:
1. Define Pydantic model in `structured_outputs.yaml`
2. Register agent in `registry` section
3. Set `auto_tool_mode: true` in agent config
4. Tool receives validated structured data as kwargs

### 7.3 Event Handling

Platform can listen for:
- `chat.structured_output_ready` - React to agent structured outputs
- `chat.tool_call` / `chat.tool_response` - Track tool execution
- `chat.run_complete` - Handle workflow completion
- `agui.state.*` - Sync artifact state

### 7.4 Context Variables Available

```python
# Auto-injected by orchestrator
"workflow_name"
"app_id"
"chat_id"
"user_id"

# Loaded from context_variables.yaml
# Custom workflow-specific variables
```

---

## 8. Constraints and Limitations

### 8.1 Tool Execution Constraints

- Tools must be Python callables (sync or async)
- Tool files loaded fresh each time (no caching)
- Auto-tool validation requires matching Pydantic model
- UI tool responses require explicit `submit_ui_tool_response()`

### 8.2 Structured Output Constraints

- Must define models in `structured_outputs.yaml`
- Only one model per agent in registry
- Nested models must be defined before referencing
- OpenAI strict mode requires `additionalProperties: false`

### 8.3 Event Constraints

- Events are fire-and-forget (no acknowledgment)
- WebSocket must be connected for event delivery
- Sequence numbers per-chat for ordering
- Resume capability requires persisted messages

### 8.4 Workflow Constraints

- Single active pattern per workflow execution
- AG2 handles agent orchestration (not customizable without pattern changes)
- Human-in-the-loop requires explicit `human_input_mode` configuration

---

## 9. Code References Summary

| Capability | File | Key Function/Class |
|------------|------|-------------------|
| Workflow loading | `workflow/workflow_manager.py` | `UnifiedWorkflowManager` |
| Workflow execution | `workflow/orchestration_patterns.py` | `run_workflow_orchestration()` |
| Agent creation | `workflow/agents/factory.py` | `create_agents()` |
| Tool loading | `workflow/agents/tools.py` | `load_agent_tool_functions()` |
| Structured outputs | `workflow/outputs/structured.py` | `load_workflow_structured_outputs()` |
| Auto-tool handling | `events/auto_tool_handler.py` | `AutoToolEventHandler` |
| Event dispatch | `events/unified_event_dispatcher.py` | `UnifiedEventDispatcher` |
| WebSocket transport | `transport/simple_transport.py` | `SimpleTransport` |
| AG-UI events | `event_agui_adapter.py` | `AGUIEventAdapter` |
| Action execution | `action_executor.py` | `execute_action()` |
| Lifecycle hooks | `workflow/execution/lifecycle.py` | `LifecycleToolManager` |
| Session persistence | `data/persistence/persistence_manager.py` | `AG2PersistenceManager` |

---

## 10. Platform Integration Recommendations

1. **Use `tools.yaml`** for all tool definitions - runtime reads this directly
2. **Define structured outputs** when agents need validated JSON responses
3. **Enable `auto_tool_mode`** for tools that should trigger automatically from agent outputs
4. **Use lifecycle hooks** for setup/teardown logic
5. **Subscribe to events** via the dispatcher for reactive behavior
6. **Persist artifact state** for resumable UI components
7. **Pass context** via `context_variables` parameter in tools
