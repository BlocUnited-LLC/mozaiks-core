# MozaiksCore Self-Inventory: AI Runtime Readiness Analysis

> **Scope**: This document analyzes mozaiks-core **as it exists today**, with no assumptions about external repositories. The goal is to determine what would be required for this repo to host an AI agent runtime.

---

## Part 1: Current Ownership

```json
{
  "current_ownership": {
    "owns": [
      {
        "capability": "per_app_runtime_shell",
        "description": "FastAPI app with single MOZAIKS_APP_ID per deployment",
        "files": ["backend/core/director.py"],
        "evidence": "APP_ID enforced in production, injected into all requests"
      },
      {
        "capability": "plugin_orchestration",
        "description": "Discovery, loading, and execution of sandboxed plugins",
        "files": ["backend/core/plugin_manager.py", "backend/plugins/*/logic.py"],
        "evidence": "500-line plugin_manager with registry, async loading, metadata caching"
      },
      {
        "capability": "event_bus_pubsub",
        "description": "Internal publish/subscribe for decoupled event handling",
        "files": ["backend/core/event_bus.py"],
        "evidence": "248-line implementation with sync/async callbacks, history, statistics"
      },
      {
        "capability": "websocket_transport",
        "description": "Per-user WebSocket connections for real-time messaging",
        "files": ["backend/core/websocket_manager.py"],
        "evidence": "Singleton manager with connect/disconnect/send_to_user/broadcast"
      },
      {
        "capability": "subscription_enforcement",
        "description": "READ-ONLY subscription state enforcement for plugin access",
        "files": ["backend/core/subscription_manager.py", "backend/core/subscription_stub.py"],
        "evidence": "is_plugin_accessible() checks, Control Plane sync only"
      },
      {
        "capability": "notifications_multichannel",
        "description": "In-app, email, SMS, web-push notification delivery",
        "files": ["backend/core/notifications/", "backend/core/notifications_manager.py"],
        "evidence": "Channels: in_app.py, email_channel.py, sms.py, web_push.py"
      },
      {
        "capability": "user_settings_storage",
        "description": "Per-user, per-plugin settings persistence",
        "files": ["backend/core/settings_manager.py"],
        "evidence": "get_plugin_settings(), save_plugin_settings() in MongoDB"
      },
      {
        "capability": "state_management",
        "description": "In-memory state tracking for runtime operations",
        "files": ["backend/core/state_manager.py"],
        "evidence": "state_manager singleton used across director.py"
      },
      {
        "capability": "analytics_event_logging",
        "description": "Raw event capture and KPI aggregation",
        "files": ["backend/core/analytics/event_logger.py", "backend/core/analytics/kpi_service.py"],
        "evidence": "event_logger.py, app_kpi_snapshot_service.py exist"
      },
      {
        "capability": "jwt_authentication",
        "description": "JWT-based auth with OIDC support",
        "files": ["backend/security/authentication.py", "backend/security/auth.py"],
        "evidence": "get_current_user() dependency, auth_router mounted"
      },
      {
        "capability": "config_driven_behavior",
        "description": "JSON-based configuration for plugins, nav, subscriptions, themes",
        "files": [
          "backend/core/config/plugin_registry.json",
          "backend/core/config/navigation_config.json",
          "backend/core/config/subscription_config.json",
          "backend/core/config/theme_config.json"
        ],
        "evidence": "All major behavior controlled via JSON configs"
      },
      {
        "capability": "ai_capability_registry",
        "description": "Config-driven AI capability definitions with workflow_id mappings",
        "files": ["backend/core/config/ai_capabilities.json", "backend/core/routes/ai.py"],
        "evidence": "capabilities[] array with workflow_id, allowed_plans"
      },
      {
        "capability": "execution_token_minting",
        "description": "Signed JWT tokens for external runtime sessions",
        "files": ["backend/core/runtime/execution_tokens.py"],
        "evidence": "mint_execution_token() called from ai.py routes"
      },
      {
        "capability": "runtime_session_brokering",
        "description": "ChatUI URL generation and session metadata management",
        "files": ["backend/core/runtime/manager.py"],
        "evidence": "build_chatui_url() with template substitution, new_chat_id()"
      },
      {
        "capability": "admin_internal_routes",
        "description": "/__mozaiks/admin/* endpoints for Control Plane integration",
        "files": [
          "backend/core/routes/admin_users.py",
          "backend/core/routes/analytics.py",
          "backend/core/routes/status.py",
          "backend/core/routes/subscription_sync.py"
        ],
        "evidence": "X-Internal-API-Key protected routes mounted in director.py"
      },
      {
        "capability": "hosting_operator_analysis",
        "description": "Metrics-to-action policy evaluation (no execution)",
        "files": ["backend/core/hosting_operator.py"],
        "evidence": "_severity_for(), _annotate_actions() - analysis only"
      },
      {
        "capability": "frontend_theme_system",
        "description": "React ThemeProvider with config-driven styling",
        "files": ["src/core/theme/ThemeProvider.jsx", "src/core/theme/useTheme.js"],
        "evidence": "Theme context with useTheme hook"
      },
      {
        "capability": "frontend_plugin_loader",
        "description": "Dynamic plugin discovery and rendering",
        "files": ["src/core/plugins/PluginProvider.jsx", "src/core/plugins/usePlugins.js"],
        "evidence": "PluginProvider context, DynamicUIComponent.jsx"
      }
    ],
    "does_not_own": [
      {
        "capability": "workflow_execution",
        "description": "Actual execution of AI workflows/agent steps",
        "evidence": "backend/core/workflows/ contains only mozaiksai_context.json (empty)",
        "current_pattern": "Delegates to external runtime via chatui_url_template"
      },
      {
        "capability": "chat_ui",
        "description": "Chat interface for AI conversations",
        "evidence": "runtime/manager.py comment: 'must NOT implement ChatUI'",
        "current_pattern": "Generates URLs pointing to external ChatUI"
      },
      {
        "capability": "llm_integration",
        "description": "Direct OpenAI/Anthropic/LLM API calls",
        "evidence": "No LLM client code in codebase",
        "current_pattern": "OPENAI_API_KEY documented but unused in core"
      },
      {
        "capability": "tool_calling",
        "description": "MCP-style tool definitions and execution",
        "evidence": "No tool registry or execution engine",
        "current_pattern": "Would need new subsystem"
      },
      {
        "capability": "conversation_memory",
        "description": "Persistent chat history and context management",
        "evidence": "No chat history collections in database.py",
        "current_pattern": "External runtime responsibility"
      },
      {
        "capability": "agent_orchestration",
        "description": "Multi-step agent planning and execution",
        "evidence": "No agent loop or planning code",
        "current_pattern": "Would need new subsystem"
      },
      {
        "capability": "subscription_mutations",
        "description": "Creating/updating subscription states",
        "evidence": "_require_internal_call() guard in subscription_manager.py",
        "current_pattern": "Write operations require Control Plane X-Internal-API-Key"
      },
      {
        "capability": "payment_processing",
        "description": "Stripe/payment gateway integration",
        "evidence": "Comments: 'Payment processing handled by Control Plane'",
        "current_pattern": "Subscription sync from external source only"
      },
      {
        "capability": "container_orchestration",
        "description": "Docker/K8s deployment decisions",
        "evidence": "hosting_operator.py does analysis, not execution",
        "current_pattern": "Outputs recommendations, external system acts"
      },
      {
        "capability": "cross_tenant_analytics",
        "description": "Aggregated analytics across multiple app deployments",
        "evidence": "All analytics scoped to single APP_ID",
        "current_pattern": "Control Plane would aggregate"
      }
    ]
  }
}
```

---

## Part 2: Extensibility Points

```json
{
  "extensibility_points": [
    {
      "name": "plugin_system",
      "type": "primary_extension_model",
      "location": "backend/plugins/{name}/logic.py",
      "interface": "async def execute(data: dict) -> dict",
      "registration": "backend/core/config/plugin_registry.json",
      "capabilities": [
        "Custom backend logic via execute() entry point",
        "Settings panel via settings.py",
        "Navigation registration via navigation_config.json",
        "Subscription gating via subscription_config.json"
      ],
      "ai_runtime_fit": "HIGH - Could wrap AI agent logic as a plugin"
    },
    {
      "name": "event_bus",
      "type": "internal_pubsub",
      "location": "backend/core/event_bus.py",
      "interface": "@on_event(event_type) decorator, event_bus.publish()",
      "capabilities": [
        "Decouple plugin communication",
        "Audit trail via event history",
        "Async and sync callback support"
      ],
      "ai_runtime_fit": "HIGH - Could publish agent lifecycle events"
    },
    {
      "name": "websocket_transport",
      "type": "realtime_channel",
      "location": "backend/core/websocket_manager.py",
      "interface": "websocket_manager.send_to_user(user_id, message)",
      "capabilities": [
        "Per-user message delivery",
        "Broadcast to all users",
        "Real-time notifications"
      ],
      "ai_runtime_fit": "HIGH - Streaming chat responses would use this"
    },
    {
      "name": "ai_capabilities_config",
      "type": "config_driven",
      "location": "backend/core/config/ai_capabilities.json",
      "interface": "JSON array with workflow_id, allowed_plans",
      "capabilities": [
        "Define AI features without code changes",
        "Subscription-gated capabilities",
        "Icon and display metadata"
      ],
      "ai_runtime_fit": "MEDIUM - Would need expansion for agent tools"
    },
    {
      "name": "capability_specs_folder",
      "type": "declarative_config",
      "location": "backend/core/config/capability_specs/",
      "interface": "JSON files loaded by _load_capability_specs()",
      "capabilities": [
        "External capability definitions",
        "Environment variable override for location"
      ],
      "ai_runtime_fit": "HIGH - Could hold agent workflow definitions"
    },
    {
      "name": "execution_token_minting",
      "type": "security_boundary",
      "location": "backend/core/runtime/execution_tokens.py",
      "interface": "mint_execution_token(claims: dict) -> str",
      "capabilities": [
        "Signed JWT for external runtime auth",
        "Carries ExecutionContext claims"
      ],
      "ai_runtime_fit": "HIGH - Would authenticate agent sessions"
    },
    {
      "name": "admin_routes_pattern",
      "type": "internal_api",
      "location": "backend/core/routes/__mozaiks/admin/*",
      "interface": "X-Internal-API-Key header required",
      "capabilities": [
        "Control Plane integration",
        "Analytics retrieval",
        "Subscription sync"
      ],
      "ai_runtime_fit": "MEDIUM - Could add agent management endpoints"
    },
    {
      "name": "notification_channels",
      "type": "delivery_abstraction",
      "location": "backend/core/notifications/channels/",
      "interface": "class Channel(base.BaseChannel)",
      "capabilities": [
        "Add new delivery channels (Slack, Discord, etc.)",
        "Template-based message formatting"
      ],
      "ai_runtime_fit": "LOW - Not directly related to AI runtime"
    },
    {
      "name": "frontend_plugin_components",
      "type": "ui_extension",
      "location": "src/plugins/{name}/index.js",
      "interface": "React component default export",
      "capabilities": [
        "Custom UI per plugin",
        "Settings panel integration",
        "Route registration"
      ],
      "ai_runtime_fit": "HIGH - Could host ChatUI component"
    },
    {
      "name": "middleware_chain",
      "type": "request_pipeline",
      "location": "backend/core/director.py (FastAPI middleware)",
      "interface": "app.add_middleware()",
      "capabilities": [
        "Request/response transformation",
        "CORS handling",
        "Rate limiting potential"
      ],
      "ai_runtime_fit": "MEDIUM - Could add agent context injection"
    }
  ]
}
```

---

## Part 3: Proposed Structure for AI Runtime Hosting

If mozaiks-core were to **host an AI agent runtime locally** (not delegate to external service), the following additions would be needed:

```
backend/
├── core/
│   ├── ai_runtime/                      # NEW: AI execution engine
│   │   ├── __init__.py
│   │   ├── agent_loop.py                # Main agent execution loop
│   │   ├── context_manager.py           # Conversation context / memory
│   │   ├── tool_registry.py             # Tool definitions and dispatch
│   │   ├── llm_client.py                # LLM API abstraction (OpenAI, Anthropic, etc.)
│   │   ├── streaming.py                 # SSE/WebSocket streaming handlers
│   │   ├── models.py                    # Pydantic models for agent state
│   │   └── config.py                    # AI runtime configuration
│   │
│   ├── tools/                           # NEW: Tool implementations
│   │   ├── __init__.py
│   │   ├── base.py                      # Base tool interface
│   │   ├── code_execution.py            # Sandboxed code runner (if needed)
│   │   ├── web_search.py                # Search tool
│   │   ├── database_query.py            # MongoDB tool for plugin data
│   │   └── plugin_bridge.py             # Bridge to execute plugin actions
│   │
│   ├── chat/                            # NEW: Chat management
│   │   ├── __init__.py
│   │   ├── session_manager.py           # Chat session lifecycle
│   │   ├── history_store.py             # MongoDB chat history
│   │   └── models.py                    # ChatMessage, ChatSession models
│   │
│   ├── routes/
│   │   ├── chat.py                      # NEW: /api/chat/* endpoints
│   │   └── tools.py                     # NEW: /api/tools/* endpoints (if exposed)
│   │
│   ├── config/
│   │   ├── ai_runtime_config.json       # NEW: Agent behavior settings
│   │   ├── tool_registry.json           # NEW: Available tools per capability
│   │   └── llm_providers.json           # NEW: LLM provider configurations
│   │
│   └── workflows/
│       ├── executor.py                  # NEW: Workflow step execution
│       ├── planner.py                   # NEW: Multi-step planning
│       └── definitions/                 # NEW: Workflow YAML/JSON definitions
│           └── chat.yaml
│
src/
├── chat/                                # NEW: Chat UI components
│   ├── ChatContainer.jsx                # Main chat interface
│   ├── MessageList.jsx                  # Message display
│   ├── InputArea.jsx                    # User input with streaming
│   ├── ToolResultCard.jsx               # Display tool execution results
│   └── hooks/
│       ├── useChat.js                   # Chat state management
│       └── useStreaming.js              # SSE/WebSocket handling
│
├── ai/
│   ├── AICapabilitiesPage.jsx           # EXISTS
│   └── ChatPage.jsx                     # NEW: Route to chat UI
```

### New Dependencies Required

```txt
# requirements.txt additions
openai>=1.0.0                # OpenAI client (or langchain for multi-provider)
anthropic>=0.20.0            # Anthropic client (optional)
tiktoken>=0.5.0              # Token counting
pydantic>=2.0                # Already present, ensure v2
sse-starlette>=1.0.0         # Server-sent events for streaming
redis>=5.0.0                 # Optional: session state cache
```

### New Environment Variables

```env
# AI Runtime Configuration
MOZAIKS_AI_PROVIDER=openai               # openai | anthropic | azure_openai
MOZAIKS_AI_MODEL=gpt-4o                  # Model identifier
MOZAIKS_AI_MAX_TOKENS=4096               # Max response tokens
MOZAIKS_AI_TEMPERATURE=0.7               # Sampling temperature

# Provider Keys
OPENAI_API_KEY=sk-...                    # OpenAI API key
ANTHROPIC_API_KEY=sk-ant-...             # Anthropic API key (if used)
AZURE_OPENAI_ENDPOINT=https://...        # Azure OpenAI endpoint (if used)
AZURE_OPENAI_API_KEY=...                 # Azure OpenAI key (if used)

# Feature Flags
MOZAIKS_AI_RUNTIME_ENABLED=true          # Enable local AI runtime
MOZAIKS_AI_TOOL_CALLING_ENABLED=true     # Enable tool calling
MOZAIKS_AI_STREAMING_ENABLED=true        # Enable response streaming

# Optional: External Services
REDIS_URL=redis://localhost:6379         # Session cache (optional)
```

### New MongoDB Collections

```javascript
// Database collections needed
db.chat_sessions       // { session_id, user_id, app_id, capability_id, created_at, status }
db.chat_messages       // { session_id, role, content, tool_calls, tool_results, timestamp }
db.tool_executions     // { execution_id, session_id, tool_name, input, output, duration_ms }
db.agent_traces        // { trace_id, session_id, steps[], tokens_used, latency_ms }
```

### Azure Dependencies (if cloud-native)

| Service | Purpose | Required? |
|---------|---------|-----------|
| Azure OpenAI | LLM inference | Optional (can use OpenAI direct) |
| Azure Cosmos DB | Chat history at scale | Optional (MongoDB works) |
| Azure Redis Cache | Session state | Optional (in-memory works for single instance) |
| Azure Container Apps | Scaling AI workloads | Optional (Docker Compose works locally) |
| Azure Application Insights | Telemetry | Recommended for production |

---

## Part 4: Architectural Boundaries That Must Remain True

```json
{
  "architectural_invariants": [
    {
      "boundary": "single_app_identity",
      "statement": "One MOZAIKS_APP_ID per runtime deployment",
      "rationale": "Enables tenant isolation, simplifies auth, prevents cross-tenant data leaks",
      "enforcement": "director.py raises RuntimeError if APP_ID missing in production"
    },
    {
      "boundary": "plugins_are_sandboxed",
      "statement": "Plugins execute through execute(data) interface only",
      "rationale": "Prevents plugins from bypassing auth, accessing other plugins' data, or modifying core",
      "enforcement": "plugin_manager.py only calls execute(), data includes user context"
    },
    {
      "boundary": "subscription_writes_external",
      "statement": "Subscription mutations require X-Internal-API-Key (Control Plane only)",
      "rationale": "Prevents users from self-upgrading, ensures billing integrity",
      "enforcement": "_require_internal_call() guard in subscription_manager.py"
    },
    {
      "boundary": "auth_as_single_source",
      "statement": "User identity comes from JWT only, never from request body",
      "rationale": "Prevents user impersonation, ensures audit trail integrity",
      "enforcement": "get_current_user() dependency injected, inject_request_context() server-side"
    },
    {
      "boundary": "config_driven_behavior",
      "statement": "Features controlled via JSON configs, not hardcoded",
      "rationale": "Enables non-code customization, simplifies deployment",
      "enforcement": "plugin_registry.json, navigation_config.json, etc. loaded at startup"
    },
    {
      "boundary": "event_bus_for_decoupling",
      "statement": "Cross-plugin communication uses event_bus, not direct imports",
      "rationale": "Prevents plugin coupling, enables async processing, provides audit trail",
      "enforcement": "event_bus.publish() and @on_event() pattern"
    },
    {
      "boundary": "core_does_not_execute_workflows",
      "statement": "Core brokers sessions to external runtime, does not run agent loops",
      "rationale": "Separation of concerns, allows runtime scaling independently",
      "enforcement": "runtime/manager.py comment, empty workflows/ folder",
      "IF_CHANGED": "This boundary would be crossed if AI runtime is hosted locally"
    },
    {
      "boundary": "mongodb_as_primary_store",
      "statement": "All persistent state in MongoDB, no separate SQL/Redis required",
      "rationale": "Simplifies deployment, single database to manage",
      "enforcement": "core/config/database.py provides all collections"
    },
    {
      "boundary": "websocket_for_realtime_only",
      "statement": "WebSocket used for notifications/streaming, not RPC",
      "rationale": "REST for CRUD, WebSocket for push - clear responsibility",
      "enforcement": "websocket_manager only has send_to_user/broadcast"
    },
    {
      "boundary": "admin_routes_internal_only",
      "statement": "/__mozaiks/admin/* routes require X-Internal-API-Key",
      "rationale": "Prevents user access to admin operations, enables Control Plane integration",
      "enforcement": "X-Internal-API-Key middleware on admin routers"
    }
  ]
}
```

---

## Summary: AI Runtime Hosting Decision Matrix

| Approach | Pros | Cons | Recommended When |
|----------|------|------|------------------|
| **Keep Delegation** | Clean separation, runtime scales independently, existing pattern works | Extra latency, external dependency, two repos to maintain | You have/will have a dedicated AI runtime repo |
| **Host Locally** | Single deployment, lower latency, simpler architecture | Crosses core boundary, increases core complexity, tighter coupling | Small-scale or all-in-one deployment |
| **Hybrid** | ChatUI local, heavy inference external | Best of both worlds | Production at scale with local UX |

### If You Choose to Host AI Runtime Locally

1. **Accept the boundary change**: Document that `core_does_not_execute_workflows` is intentionally relaxed
2. **Create clear internal boundaries**: `backend/core/ai_runtime/` as a self-contained module
3. **Feature flag everything**: `MOZAIKS_AI_RUNTIME_ENABLED` to allow fallback to delegation
4. **Maintain the plugin pattern**: AI tools could be plugins themselves
5. **Use event bus for observability**: Publish `ai:message_received`, `ai:tool_called`, `ai:response_complete`

---

*Document generated from codebase analysis on: $(date)*
*No external repository assumptions made.*
