# Architecture Overview

MozaiksAI is a stateless execution engine for declarative AI workflows. This page explains the core architecture and how components interact.

## System Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Your Platform                         │
│  (Auth, Billing, User Management, Workflow Generation)  │
└────────────────────┬────────────────────────────────────┘
                     │ JWT Token
                     ▼
┌─────────────────────────────────────────────────────────┐
│                  MozaiksAI Runtime                       │
│                                                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │
│  │   WebSocket  │  │   Workflow   │  │     AG2      │ │
│  │   Transport  │──│    Manager   │──│   Engine     │ │
│  └──────────────┘  └──────────────┘  └──────────────┘ │
│         │                  │                  │         │
│         ▼                  ▼                  ▼         │
│  ┌──────────────────────────────────────────────────┐  │
│  │          Persistence (MongoDB)                   │  │
│  └──────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
         │
         ▼ Usage Events
┌─────────────────────────────────────────────────────────┐
│              Your Billing System                         │
└─────────────────────────────────────────────────────────┘
```

## Core Principles

### 1. Stateless Execution

The runtime doesn't store user profiles, subscriptions, or business logic. It:
- ✅ Validates JWT signatures (not authorization claims)
- ✅ Executes workflows as defined in YAML
- ✅ Persists session/message history
- ✅ Emits usage metrics

Your platform handles:
- User authentication and authorization
- Subscription management and billing
- Rate limiting and quotas
- Content moderation
- Workflow generation/editing

### 2. Declarative Workflows

Workflows are defined in YAML files with 8 config types:

| File | Purpose |
|------|---------|
| `orchestrator.yaml` | Workflow metadata, max turns, strategy |
| `agents.yaml` | Agent definitions, roles, LLM configs |
| `handoffs.yaml` | Inter-agent handoff conditions |
| `tools.yaml` | Available tool functions |
| `context_variables.yaml` | Workflow-level state schema |
| `structured_outputs.yaml` | Required output formats |
| `hooks.yaml` | Lifecycle event handlers |
| `ui_config.yaml` | UI hints and metadata |

### 3. Multi-Tenant Isolation

Each workflow execution is isolated by:
- **app_id**: Your application's identifier
- **user_id**: End-user identifier
- **chat_id**: Conversation session
- **cache_seed**: AG2 cache isolation

State never leaks across tenants. MongoDB queries filter by these IDs.

### 4. Event-Driven Transport

WebSocket events stream workflow execution:

```
user_message → workflow_start → agent_start → tool_call → 
tool_result → agent_response → workflow_complete
```

Your frontend can render these events in real-time.

## Component Deep Dive

### WebSocket Transport (`core/transport/`)

- Accepts WebSocket connections at `/ws`
- Validates JWT tokens (signature, issuer, expiry)
- Routes messages to workflow execution
- Streams events back to client
- Handles reconnection and message replay

### Workflow Manager (`core/workflow/`)

- Discovers workflows in `workflows/` directory
- Loads YAML configs into memory
- Validates schema and dependencies
- Provides workflow metadata to router

### AG2 Engine (`core/workflow/execution/`)

- Initializes AG2 agents from config
- Configures GroupChat with orchestration strategy
- Registers tools and handoff functions
- Executes turns with max turn limits
- Captures messages and events

### Persistence (`core/multitenant/`)

- Stores messages in MongoDB per chat_id
- Retrieves history for context window
- Indexes by app_id, user_id, workflow_name
- Tracks token usage per execution

### Observability (`core/observability/`)

- Structured logging (JSON format)
- Performance metrics endpoints
- Runtime error tracking
- Token usage aggregation

## Data Flow

### Inbound Message

1. Client sends WebSocket message with JWT
2. Transport validates token structure
3. Message routed to workflow execution
4. AG2 engine loads history from MongoDB
5. GroupChat executes agent turns
6. Events streamed back to client
7. Final state persisted to MongoDB
8. Usage metrics emitted

### Workflow Loading

1. Runtime scans `workflows/` directory
2. For each folder with `orchestrator.yaml`:
   - Load 8 config files
   - Validate schema
   - Register as available workflow
3. Workflow metadata cached in memory
4. Hot-reload on file changes (dev mode)

## Extensibility

### Custom Tools

Add Python functions in `workflows/YourWorkflow/tools/`:

```python
def your_tool(param: str) -> str:
    """Tool description for LLM"""
    # Implementation
    return result
```

Reference in `tools.yaml`:
```yaml
tools:
  - name: your_tool
    module: tools.your_module
    description: What this tool does
```

### Lifecycle Hooks

Define functions in `workflows/YourWorkflow/hooks/`:

```python
async def on_workflow_start(context):
    """Called before first agent turn"""
    # Custom initialization
```

Reference in `hooks.yaml`:
```yaml
hooks:
  on_workflow_start: hooks.startup.on_workflow_start
```

### Backend Integration

Use the backend client for platform APIs:

```python
from workflows._shared.backend_client import BackendClient

async def my_tool(app_id: str, user_id: str):
    client = BackendClient(app_id, user_id)
    result = await client.call_platform_api("endpoint", data)
    return result
```

This keeps platform-specific logic out of the runtime core.

## Security Boundaries

### What Runtime Validates

- JWT signature (using `JWT_SECRET`)
- Token issuer (must match `ALLOWED_ISSUERS`)
- Token expiry
- Required claims: `app_id`, `user_id`, `scope`

### What Runtime Doesn't Validate

- User permissions/roles
- Subscription status
- Rate limits
- Feature flags
- Content policies

Your platform must enforce these **before** issuing the JWT.

## Performance Characteristics

- **Latency**: 50-200ms first token (cold start), 10-50ms streaming
- **Throughput**: 100+ concurrent workflows per instance
- **Memory**: ~100MB base + ~50MB per active workflow
- **Storage**: ~1KB per message in MongoDB

Scale horizontally for more throughput. MongoDB is the shared state layer.

## Next Steps

- [Runtime Boundaries](runtime-boundaries.md) - What the runtime does/doesn't do
- [Workflow System](workflow-system.md) - How workflows are discovered and executed
- [Multi-Tenancy](multi-tenancy.md) - Tenant isolation deep dive
