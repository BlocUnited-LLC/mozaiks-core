# Runtime Layer Overview

**Purpose:** Introduce the MozaiksAI runtime layer—the execution engine that orchestrates AG2 workflows, manages transport, persists state, and provides observability.

---

## What is the Runtime?

The **runtime layer** is the backend execution environment for MozaiksAI workflows. It sits between:

1. **Declarative Workflows** (JSON manifests in `workflows/`) that define WHAT should run
2. **AG2 (Autogen) Engine** that executes the multi-agent orchestration
3. **Frontend (ChatUI)** that renders events and collects user input

**Core Responsibility:** Load workflow configs, wire up agents/tools, execute AG2 patterns, stream events to the frontend, persist state, and track metrics—all while maintaining multi-tenant isolation.

---

## Runtime Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                       RUNTIME LAYER                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐       │
│  │  Transport   │   │   Event      │   │ Orchestration│       │
│  │  (WebSocket/ │◄──┤  Dispatcher  │◄──┤  (AG2 Exec)  │       │
│  │  WebSocket) │   │              │   │              │       │
│  └──────────────┘   └──────┬───────┘   └──────────────┘       │
│                             │                                    │
│                             ▼                                    │
│  ┌──────────────┐   ┌──────────────┐   ┌──────────────┐       │
│  │ Persistence  │   │ Observability│   │  Workflow    │       │
│  │ (MongoDB)    │   │ (Metrics)    │   │  Manager     │       │
│  └──────────────┘   └──────────────┘   └──────────────┘       │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
         ▲                                              │
         │                                              ▼
   ┌─────────────┐                            ┌──────────────────┐
   │  ChatUI     │                            │ Workflow Configs │
   │  (Frontend) │                            │ (JSON Manifests) │
   └─────────────┘                            └──────────────────┘
```

---

## Core Subsystems

### 1. **Workflow Manager** (`core/workflow/workflow_manager.py`)

**Responsibilities:**
- Discover and load workflow JSON manifests from `workflows/` directory
- Validate workflow structure (agents.json, tools.json, workflow.json, etc.)
- Register tools dynamically with AG2 agents
- Provide workflow metadata to HTTP endpoints

**Key Operations:**
```python
# Load workflow configuration
workflow_config = await workflow_manager.load_workflow("Generator")

# Get registered tools for workflow
tools = get_workflow_tools("Generator")

# Get transport instance for workflow
transport = get_workflow_transport("Generator")
```

**Hot-Reload:** Changes to workflow JSON files are detected automatically (in dev mode with `CLEAR_TOOL_CACHE_ON_START=true`).

---

### 2. **Orchestration Engine** (`core/workflow/orchestration_patterns.py`)

**Responsibilities:**
- Execute AG2 orchestration patterns (Default, Auto, RoundRobin, Random)
- Configure agents from `agents.json` with system messages, LLM configs, and tool bindings
- Handle termination conditions and max turns
- Stream events in real-time during execution
- Capture structured outputs when agents complete

**Entry Point:**
```python
async def run_workflow_orchestration(
    app_id: str,
    chat_id: str,
    workflow_name: str,
    user_id: str,
    initial_message: str,
    *,
    transport: SimpleTransport,
    workflow_config: Dict[str, Any],
    cache_seed: int,
    ...
) -> Dict[str, Any]
```

**AG2 Pattern Factory:**
```python
pattern = create_ag2_pattern(
    pattern_name="auto",  # or "default", "round_robin", "random"
    agents=[agent1, agent2, ...],
    max_turns=20,
    cache_seed=cache_seed,
    ...
)
```

**Execution Flow:**
1. Load persisted messages (if resuming)
2. Normalize messages to strict AG2 format
3. Create AG2 agents with tool bindings
4. Start AG2 runtime logger session
5. Run pattern with event streaming
6. Capture structured outputs
7. Mark session complete in MongoDB

---

### 3. **Event Dispatcher** (`core/events/unified_event_dispatcher.py`)

**Responsibilities:**
- Route **three event categories**: Business, UI_Tool, AG2 Runtime
- Deduplicate auto-tool events
- Suppress UI_HIDDEN triggers from reaching frontend
- Track dispatcher metrics (events processed, failed, categorized)

**Event Flow:**
```python
dispatcher = get_event_dispatcher()

# Emit business log event
await dispatcher.emit_business_event(
    log_event_type="WORKFLOW_STARTED",
    level="info",
    message="Workflow execution started",
    ...
)

# Emit UI tool event
await dispatcher.emit_ui_tool_event(
    ui_tool_id="user_input",
    action="request_input",
    ...
)

# AG2 runtime events handled automatically via event serialization
```

**See:** [Event Pipeline Deep Dive](event_pipeline.md)

---

### 4. **Transport Layer** (`core/transport/simple_transport.py`)

**Responsibilities:**
- Manage WebSocket connections (SSE planned for future headless workflows)
- Buffer events before connection established
- Filter messages based on `visual_agents` configuration
- Forward events to frontend with correlation IDs

**Transport API:**
```python
transport = SimpleTransport(
    app_id="acme_corp",
    chat_id="chat_abc123",
    workflow_name="Generator"
)

# Send event to frontend
await transport.send_event_to_ui({
    "kind": "agent_message",
    "agent": "planner",
    "content": "Here's the plan...",
    ...
})

# Wait for UI response
response = await transport.wait_for_response(
    event_id="evt_123",
    timeout=60.0
)
```

**See:** [Transport & Streaming Deep Dive](transport_and_streaming.md)

---

### 5. **Persistence Manager** (`core/data/persistence_manager.py`)

**Responsibilities:**
- Store chat sessions in MongoDB `chat_sessions` collection
- Append messages in real-time (no batching)
- Track workflow stats in `workflow_stats_{app}_{workflow}` collections
- Provide resume capability by loading persisted messages
- Enforce app isolation in all queries

**Key Operations:**
```python
persistence = AG2PersistenceManager()

# Create new chat session
session = await persistence.create_chat_session(
    app_id="acme_corp",
    chat_id="chat_abc123",
    workflow_name="Generator",
    user_id="user_456",
    cache_seed=2847561923
)

# Append message
await persistence.append_message(
    chat_id="chat_abc123",
    message={
        "role": "assistant",
        "name": "planner",
        "content": "Here's the plan..."
    }
)

# Resume chat (load persisted messages)
messages = await persistence.load_chat_history(
    chat_id="chat_abc123",
    app_id="acme_corp"
)
```

**See:** [Persistence & Resume Deep Dive](persistence_and_resume.md)

---

### 6. **Observability Layer** (`core/observability/`)

**Components:**

1. **PerformanceManager** (`performance_manager.py`)
   - Track in-memory metrics: agent turns, tool calls, tokens, cost, errors
   - Provide HTTP endpoints: `/metrics/perf/aggregate`, `/metrics/perf/chats`, `/metrics/prometheus`
   - Async flush to MongoDB workflow stats

2. **AG2RuntimeLogger** (`ag2_runtime_logger.py`)
   - Shim around AG2's native file/sqlite logger
   - Capture start/end timestamps, token usage, agent performance
   - Enable AG2's built-in logging without custom persistence

3. **Logging Configuration** (`logs/logging_config.py`)
   - Unified structured logging with emoji-enhanced console output
   - File sinks: `mozaiks.log` (operational logs)
   - Toggle between pretty text and JSON Lines via `LOGS_AS_JSON`

**Metrics Endpoints:**
```bash
# Aggregate metrics across all chats
curl http://localhost:8000/metrics/perf/aggregate

# Per-chat snapshot
curl http://localhost:8000/metrics/perf/chats/chat_abc123

# Prometheus exposition
curl http://localhost:8000/metrics/prometheus
```

**See:** [Observability Deep Dive](observability.md)

---

## Request-to-Response Lifecycle

**High-Level Flow:**

1. **HTTP Request:** `POST /api/chats/{app_id}/{workflow_name}/start`
2. **Validation:** Check app exists, create/resume session
3. **WebSocket Connect:** Frontend establishes transport connection
4. **Orchestration:** Run AG2 pattern with event streaming
5. **Event Streaming:** UnifiedEventDispatcher routes events to transport
6. **Persistence:** Messages appended to MongoDB in real-time
7. **Metrics:** PerformanceManager tracks tokens/cost/duration
8. **Completion:** Session marked complete, final metrics flushed

**Detailed Trace:** See [Request Lifecycle](../overview/lifecycle.md)

---

## Multi-Tenancy Enforcement

**Every runtime operation is scoped by `app_id`:**

- **HTTP Endpoints:** App ID in path (`/api/chats/{app_id}/...`)
- **MongoDB Queries:** Always include `{"app_id": ...}` filter
- **Workflow Stats:** Per-app collections (`workflow_stats_{app}_{workflow}`)
- **Transport Instances:** One SimpleTransport per chat (isolated state)
- **Cache Seed:** Deterministic per-chat seed prevents cross-chat bleed

**See:** [Multi-Tenancy & Security](../overview/tenancy_and_security.md)

---

## Configuration & Environment Variables

**Key Environment Variables:**

| Variable | Purpose | Default |
|----------|---------|---------|
| `MONGODB_URI` | MongoDB connection string | Required |
| `OPENAI_API_KEY` | OpenAI LLM API key | Required |
| `LOGS_AS_JSON` | Structured JSON logging | `false` |
| `LOGS_BASE_DIR` | Custom log directory | `logs/logs` |
| `CLEAR_TOOL_CACHE_ON_START` | Reload tools on startup | `true` (dev) |
| `RANDOMIZE_DEFAULT_CACHE_SEED` | Randomize process default seed | `false` |
| `LLM_DEFAULT_CACHE_SEED` | Hard override for LLM seed | None |

**See:** [Configuration Reference](configuration_reference.md)

---

## Runtime vs. Application Code

**Critical Distinction:**

| Concern | Runtime Layer | Application/Workflow Layer |
|---------|--------------|---------------------------|
| **Transport** | SimpleTransport, event routing | N/A |
| **Orchestration** | AG2 pattern execution | N/A |
| **Persistence** | MongoDB save/load | N/A |
| **Observability** | Metrics, logging | N/A |
| **Tool Definitions** | Tool registration (via manifests) | Tool implementation (in workflow stubs) |
| **Agent System Messages** | Agent config loading | Agent behavior (defined in agents.json) |
| **UI Components** | Event forwarding | Component rendering (React) |

**Runtime = HOW workflows run**  
**Application = WHAT workflows do**

This separation enables the runtime to be open-sourced or contributed upstream to AG2 without coupling to proprietary business logic.

---

## Development Workflow

### Local Setup

```powershell
# Install dependencies
pip install -r requirements.txt

# Set environment variables
$env:MONGODB_URI = "mongodb://localhost:27017/mozaiksai"
$env:OPENAI_API_KEY = "sk-..."

# Run development server
python run_server.py
```

### Hot-Reload Tools

```powershell
# Force tool reload on next request
$env:CLEAR_TOOL_CACHE_ON_START = "true"
python run_server.py
```

### View Logs

```powershell
# Pretty text logs (default)
tail -f logs/logs/mozaiks.log

# JSON Lines logs
$env:LOGS_AS_JSON = "true"
python run_server.py
tail -f logs/logs/mozaiks.log | jq .
```

---

## Testing & Validation

**Runtime Health Check:**
```bash
curl http://localhost:8000/health
```

**Metrics Validation:**
```bash
# Check in-memory metrics
curl http://localhost:8000/metrics/perf/aggregate

# Verify Prometheus format
curl http://localhost:8000/metrics/prometheus
```

**MongoDB Inspection:**
```javascript
// MongoDB shell
use MozaiksAI
db.chat_sessions.find({"app_id": "acme_corp"})
db.workflow_stats_acme_corp_Generator.find()
```

---

## Common Patterns

### Creating a New Workflow

1. Create workflow directory: `workflows/MyWorkflow/`
2. Add manifests: `workflow.json`, `agents.json`, `tools.json`
3. (Optional) Add tool stubs: `workflows/MyWorkflow/tools/my_tool.py`
4. Restart server (or wait for hot-reload)
5. Workflow auto-discovered at `/api/chats/{app_id}/MyWorkflow/start`

### Adding a Tool to Existing Workflow

1. Update `workflows/{workflow}/tools.json` with new tool entry
2. Add implementation in `workflows/{workflow}/tools/` (if needed)
3. Restart or wait for tool cache refresh
4. Tool auto-registered with agents defined in workflow

### Debugging Event Flow

1. Enable verbose logging: Set `LOGS_AS_JSON=false` for pretty output
2. Check `logs/logs/mozaiks.log` for event dispatcher logs
3. Inspect WebSocket frames in browser DevTools (Network tab)
4. Query `chat_sessions` collection for persisted events

---

## Performance Characteristics

| Operation | Latency | Notes |
|-----------|---------|-------|
| Load workflow config | <10ms | Cached after first load |
| Create chat session | 20-50ms | Single MongoDB insert |
| Append message | 5-15ms | Async append, no blocking |
| Event dispatch | <5ms | In-memory routing |
| WebSocket event send | <10ms | Buffered if connection pending |
| Resume chat (100 messages) | 50-100ms | MongoDB query + normalization |
| Metrics flush | 10-30ms | Async MongoDB update |

---

## Next Steps

**Deep Dive Documentation:**

1. **[Event Pipeline](event_pipeline.md)** - UnifiedEventDispatcher internals, event types, handlers
2. **[Transport & Streaming](transport_and_streaming.md)** - SimpleTransport, WebSocket, message filtering
3. **[Persistence & Resume](persistence_and_resume.md)** - MongoDB schema, AG2 resume patterns, message normalization
4. **[Observability](observability.md)** - PerformanceManager, metrics endpoints, logging configuration
5. **[Configuration Reference](configuration_reference.md)** - All environment variables, startup options, feature toggles

**Related Documentation:**

- [Platform Architecture](../overview/architecture.md) - High-level system design
- [Request Lifecycle](../overview/lifecycle.md) - End-to-end request trace
- [Multi-Tenancy & Security](../overview/tenancy_and_security.md) - Isolation and secret handling

---

**Questions?** See [Troubleshooting](../operations/troubleshooting.md) or review the [API Reference](../reference/api_endpoints.md).
