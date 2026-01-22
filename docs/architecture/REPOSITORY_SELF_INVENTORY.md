# MozaiksAI Repository Self-Inventory

> **Document Purpose**: Architectural self-inventory of this repository as it exists today, with no assumptions about external repositories.
>
> **Last Updated**: January 17, 2026

---

## Part 1 — What This Repo Currently Is

### Ownership Summary (JSON)

```json
{
  "current_ownership": {
    "owns": [
      "AG2/Autogen workflow execution engine",
      "WebSocket transport and event streaming",
      "Chat session persistence (MongoDB)",
      "Workflow loading from declarative YAML configs",
      "Multi-agent orchestration patterns",
      "Tool registration and invocation",
      "JWT-based authentication middleware",
      "Token usage measurement and emission (not enforcement)",
      "Multi-tenant isolation (app_id scoping)",
      "UI tools registry for frontend interactions",
      "Chat UI React application (ChatUI/)",
      "E2B sandbox integration for code validation",
      "Code context extraction (Tree-sitter based)",
      "Structured output parsing",
      "Performance and observability logging",
      "AppGenerator workflow (12-agent full-stack app generation)",
      "AgentGenerator workflow (AI agent/workflow creation)",
      "SubscriptionAdvisor workflow (billing/plan guidance)",
      "ValueEngine workflow (value proposition generation)",
      "App code versioning (snapshots/patchsets)"
    ],
    "does_not_own": [
      "User authentication provider (delegates to external JWKS)",
      "User/account database (reads JWT claims, doesn't store users)",
      "Subscription/billing enforcement (emits usage, doesn't gate)",
      "Payment processing",
      "GitHub repository provisioning",
      "Cloud infrastructure deployment (Azure, Vercel)",
      "CI/CD pipeline execution",
      "DNS/SSL certificate management",
      "Secrets storage (uses Azure Key Vault via env)",
      "App instance database provisioning",
      "Enterprise/app creation lifecycle",
      "User registration flows",
      "Generated app hosting",
      "Production app monitoring"
    ]
  }
}
```

### Detailed Breakdown

#### What This Repo OWNS

| Component | Location | Evidence |
|-----------|----------|----------|
| **AG2 Runtime Engine** | `core/workflow/orchestration_patterns.py` | Imports and orchestrates `autogen` GroupChat |
| **Workflow Manager** | `core/workflow/workflow_manager.py` | Loads YAML configs, registers tools |
| **WebSocket Transport** | `core/transport/websocket.py`, `simple_transport.py` | Manages WS connections, message queues |
| **Session Persistence** | `core/data/persistence/persistence_manager.py` | MongoDB CRUD for chat_sessions |
| **JWT Auth Middleware** | `core/auth/` | Validates tokens, extracts user_id/app_id |
| **Token Usage Measurement** | `core/tokens/manager.py` | Emits usage_delta events (explicitly: "NEVER enforce") |
| **Multi-tenant Isolation** | `core/multitenant/` | `build_app_scope_filter()`, `coalesce_app_id()` |
| **Event System** | `core/events/` | Unified dispatcher, serialization, handoffs |
| **Observability** | `core/observability/` | AG2 runtime logger, performance manager |
| **Chat UI** | `ChatUI/src/` | React app with pages, components, hooks |
| **E2B Sandbox** | `workflows/AppGenerator/tools/e2b_sandbox.py` | Code execution/validation |
| **Code Context** | `workflows/AppGenerator/tools/code_context/` | Tree-sitter extraction |

#### What This Repo Does NOT Own

| Capability | Evidence |
|------------|----------|
| **User Auth Provider** | `core/auth/jwks.py` fetches external JWKS, doesn't issue tokens |
| **Billing Enforcement** | `core/tokens/manager.py` header: "NEVER contain enforcement logic" |
| **GitHub Ops** | `workflows/_shared/backend_client.py` calls external `MOZAIKS_BACKEND_URL` |
| **Deployment** | Env var `MOZAIKS_BACKEND_URL` points to external backend |
| **Secrets Storage** | Env var `AZURE_KEY_VAULT_NAME` delegates to Azure |
| **App DB Provisioning** | No DB provisioning code exists in repo |

---

## Part 2 — Generic vs App-Specific Code

### Classification Key

| Category | Definition |
|----------|------------|
| **GENERIC_RUNTIME** | Could serve any AG2-based multi-agent application |
| **APP_SPECIFIC_WORKFLOWS** | Tied to Mozaiks product use cases |

### Full Classification

```
MozaiksAI/
├── core/                                    # GENERIC_RUNTIME
│   ├── __init__.py                          # GENERIC_RUNTIME - Package marker
│   ├── core_config.py                       # GENERIC_RUNTIME - Mongo client, env loading
│   │
│   ├── artifacts/                           # GENERIC_RUNTIME
│   │   └── attachments.py                   # GENERIC_RUNTIME - File upload handling
│   │
│   ├── auth/                                # GENERIC_RUNTIME
│   │   ├── config.py                        # GENERIC_RUNTIME - Auth config loading
│   │   ├── dependencies.py                  # GENERIC_RUNTIME - FastAPI auth deps
│   │   ├── discovery.py                     # GENERIC_RUNTIME - OIDC discovery
│   │   ├── jwks.py                          # GENERIC_RUNTIME - JWKS fetching
│   │   ├── jwt_validator.py                 # GENERIC_RUNTIME - Token validation
│   │   └── websocket_auth.py                # GENERIC_RUNTIME - WS auth
│   │
│   ├── capabilities/                        # GENERIC_RUNTIME
│   │   └── simple_llm.py                    # GENERIC_RUNTIME - LLM wrapper
│   │
│   ├── data/                                # GENERIC_RUNTIME
│   │   ├── models.py                        # GENERIC_RUNTIME - WorkflowStatus enum
│   │   ├── persistence/                     # GENERIC_RUNTIME
│   │   │   ├── db_manager.py                # GENERIC_RUNTIME - MongoDB utilities
│   │   │   └── persistence_manager.py       # GENERIC_RUNTIME - Chat session CRUD
│   │   └── themes/                          # APP_SPECIFIC - Mozaiks theme system
│   │       └── theme_manager.py             # APP_SPECIFIC - Theme CRUD for apps
│   │
│   ├── events/                              # GENERIC_RUNTIME
│   │   ├── auto_tool_handler.py             # GENERIC_RUNTIME - Tool event handling
│   │   ├── event_payload_builder.py         # GENERIC_RUNTIME - Event construction
│   │   ├── event_serialization.py           # GENERIC_RUNTIME - JSON serialization
│   │   ├── handoff_events.py                # GENERIC_RUNTIME - Agent transitions
│   │   ├── unified_event_dispatcher.py      # GENERIC_RUNTIME - Event routing
│   │   └── usage_ingest.py                  # GENERIC_RUNTIME - Usage event handling
│   │
│   ├── multitenant/                         # GENERIC_RUNTIME
│   │   └── app_ids.py                       # GENERIC_RUNTIME - Tenant scoping
│   │
│   ├── observability/                       # GENERIC_RUNTIME
│   │   ├── ag2_runtime_logger.py            # GENERIC_RUNTIME - AG2 logging
│   │   ├── performance_manager.py           # GENERIC_RUNTIME - Perf metrics
│   │   └── realtime_token_logger.py         # GENERIC_RUNTIME - Token logging
│   │
│   ├── runtime/                             # GENERIC_RUNTIME
│   │   └── extensions.py                    # GENERIC_RUNTIME - Router mounting
│   │
│   ├── tokens/                              # GENERIC_RUNTIME
│   │   └── manager.py                       # GENERIC_RUNTIME - Usage emission (not gating)
│   │
│   ├── transport/                           # GENERIC_RUNTIME
│   │   ├── resume_groupchat.py              # GENERIC_RUNTIME - Session resume
│   │   ├── session_registry.py              # GENERIC_RUNTIME - Connection tracking
│   │   ├── simple_transport.py              # GENERIC_RUNTIME - Message transport
│   │   └── websocket.py                     # GENERIC_RUNTIME - WS manager
│   │
│   └── workflow/                            # GENERIC_RUNTIME
│       ├── agent_tools.py                   # GENERIC_RUNTIME - Tool binding
│       ├── agents/                          # GENERIC_RUNTIME - Agent creation
│       ├── context/                         # GENERIC_RUNTIME - Context management
│       ├── execution/                       # GENERIC_RUNTIME - GroupChat execution
│       ├── handoffs.py                      # GENERIC_RUNTIME - Handoff logic
│       ├── llm_config.py                    # GENERIC_RUNTIME - LLM config loading
│       ├── messages/                        # GENERIC_RUNTIME - Message handling
│       ├── orchestration_patterns.py        # GENERIC_RUNTIME - AG2 orchestration
│       ├── outputs/                         # GENERIC_RUNTIME - Structured outputs
│       ├── pack/                            # GENERIC_RUNTIME - Workflow packing
│       ├── session_manager.py               # GENERIC_RUNTIME - Session lifecycle
│       ├── ui_tools.py                      # GENERIC_RUNTIME - UI tool registry
│       ├── validation/                      # GENERIC_RUNTIME - Schema validation
│       └── workflow_manager.py              # GENERIC_RUNTIME - Workflow loading
│
├── workflows/                               # APP_SPECIFIC_WORKFLOWS
│   ├── __init__.py                          # GENERIC_RUNTIME - Discovery mechanism
│   │
│   ├── AppGenerator/                        # APP_SPECIFIC - Full-stack app generation
│   │   ├── agents.yaml                      # APP_SPECIFIC - 12 specialized agents
│   │   ├── handoffs.yaml                    # APP_SPECIFIC - Agent transitions
│   │   ├── orchestrator.yaml                # APP_SPECIFIC - GroupChat config
│   │   ├── tools.yaml                       # APP_SPECIFIC - Tool bindings
│   │   ├── tools/                           # APP_SPECIFIC - Generation tools
│   │   │   ├── e2b_sandbox.py               # REUSABLE - Could be generic
│   │   │   ├── code_context/                # REUSABLE - Could be generic
│   │   │   ├── backend_client.py            # APP_SPECIFIC - Mozaiks backend
│   │   │   └── export_app_code.py           # APP_SPECIFIC - Code export
│   │   └── ...                              # APP_SPECIFIC
│   │
│   ├── AgentGenerator/                      # APP_SPECIFIC - Agent workflow creation
│   │   ├── agents.yaml                      # APP_SPECIFIC - Specialized agents
│   │   ├── tools/                           # APP_SPECIFIC - Generation tools
│   │   │   ├── action_plan.py               # APP_SPECIFIC - Plan generation
│   │   │   ├── workflow_converter.py        # APP_SPECIFIC - YAML conversion
│   │   │   └── ...                          # APP_SPECIFIC
│   │   └── ...                              # APP_SPECIFIC
│   │
│   ├── SubscriptionAdvisor/                 # APP_SPECIFIC - Billing guidance
│   │   └── ...                              # APP_SPECIFIC
│   │
│   ├── ValueEngine/                         # APP_SPECIFIC - Value proposition
│   │   └── ...                              # APP_SPECIFIC
│   │
│   ├── _shared/                             # MIXED
│   │   ├── agent_endpoints.py               # GENERIC_RUNTIME - Agent API helpers
│   │   ├── app_code_versions.py             # APP_SPECIFIC - Code versioning
│   │   ├── backend_client.py                # APP_SPECIFIC - Mozaiks backend HTTP
│   │   ├── pattern_context_schema.yaml      # APP_SPECIFIC - Pattern schema
│   │   └── workflow_exports.py              # APP_SPECIFIC - Export logic
│   │
│   ├── _examples/                           # GENERIC_RUNTIME - Reference patterns
│   ├── _pack/                               # GENERIC_RUNTIME - Pack utilities
│   └── DesignDocs/                          # APP_SPECIFIC - Design doc workflow
│
├── ChatUI/                                  # APP_SPECIFIC_UI
│   └── src/
│       ├── components/                      # MIXED
│       │   ├── chat/                        # GENERIC_RUNTIME - Reusable chat UI
│       │   └── layout/                      # APP_SPECIFIC - Mozaiks layout
│       ├── context/                         # GENERIC_RUNTIME - React context
│       ├── hooks/                           # GENERIC_RUNTIME - Reusable hooks
│       ├── pages/                           # APP_SPECIFIC - Mozaiks pages
│       ├── services/                        # GENERIC_RUNTIME - API services
│       ├── workflows/                       # APP_SPECIFIC - Workflow UI components
│       │   ├── AgentGenerator/              # APP_SPECIFIC
│       │   ├── AppGenerator/                # APP_SPECIFIC
│       │   └── ValueEngine/                 # APP_SPECIFIC
│       └── ...
│
├── shared_app.py                            # GENERIC_RUNTIME - FastAPI entrypoint
├── run_server.py                            # GENERIC_RUNTIME - Server launcher
│
├── logs/                                    # GENERIC_RUNTIME - Logging config
│   ├── logging_config.py                    # GENERIC_RUNTIME
│   ├── runtime_sanitizer.py                 # GENERIC_RUNTIME
│   └── tools_logs.py                        # GENERIC_RUNTIME
│
├── infra/                                   # GENERIC_RUNTIME - Deployment configs
│   ├── compose/                             # GENERIC_RUNTIME - Docker compose
│   └── docker/                              # GENERIC_RUNTIME - Dockerfiles
│
├── tests/                                   # MIXED
│   ├── conftest.py                          # GENERIC_RUNTIME - Test fixtures
│   ├── core/                                # GENERIC_RUNTIME - Core tests
│   ├── test_e2b_sandbox.py                  # REUSABLE
│   ├── test_subscription_advisor.py         # APP_SPECIFIC
│   └── ...                                  # MIXED
│
├── scripts/                                 # GENERIC_RUNTIME - Utility scripts
└── docs/                                    # MIXED - Documentation
```

### Summary Statistics

| Category | Folder Count | Description |
|----------|--------------|-------------|
| **GENERIC_RUNTIME** | ~70% | Core runtime that could serve any AG2 app |
| **APP_SPECIFIC_WORKFLOWS** | ~25% | Mozaiks-specific workflows and tools |
| **REUSABLE** | ~5% | Currently in workflows but could be promoted to core |

---

## Part 3 — Minimal Requirements to Run

### Required Services Checklist

- [ ] **MongoDB** (version 4.4+)
  - Chat session persistence
  - Workflow exports
  - App code snapshots/patchsets
  - Performance telemetry

- [ ] **OpenAI API** (or compatible LLM API)
  - AG2/Autogen requires LLM backend
  - Via `OPENAI_API_KEY` or Azure Key Vault

- [ ] **E2B Sandbox** (optional but recommended)
  - Code validation for AppGenerator
  - Via `E2B_API_KEY`

### Required Environment Variables

#### Critical (App won't start without)
```bash
# At least ONE of these must be set
OPENAI_API_KEY=sk-...                    # Direct API key
# OR
AZURE_KEY_VAULT_NAME=my-vault            # To fetch from Key Vault
AZURE_TENANT_ID=...
AZURE_CLIENT_ID=...
AZURE_CLIENT_SECRET=...

# Database
MONGO_URI=mongodb://localhost:27017      # Required for persistence
```

#### Important (Affects functionality)
```bash
# Environment
ENVIRONMENT=development|production       # Affects logging

# Auth (for JWT validation)
# If these aren't set, auth is effectively disabled
# See core/auth/config.py for defaults

# Backend Integration (for deployment features)
MOZAIKS_BACKEND_URL=http://localhost:3000
INTERNAL_API_KEY=...

# E2B (for code validation)
E2B_API_KEY=...
```

#### Optional (Have defaults)
```bash
# Logging
LOGS_BASE_DIR=logs/logs
LOGS_AS_JSON=false
CLEAR_LOGS_ON_START=1

# Caching
LLM_DEFAULT_CACHE_SEED=178
LLM_CONFIG_CACHE_TTL=0

# Features
CONTEXT_AWARE=true
MONETIZATION_ENABLED=true
```

### Required Dependencies

#### Python (from requirements.txt)
```
# Core Runtime
ag2[openai,lmm]          # AG2/Autogen - the execution engine
fastapi>=0.100.0         # HTTP API framework
uvicorn[standard]        # ASGI server
websockets>=11.0         # WebSocket support
motor>=3.3.0             # Async MongoDB driver
pymongo>=4.5.0           # MongoDB driver
pydantic>=2.5.0          # Data validation
pyyaml>=6.0              # YAML parsing for workflows

# Auth
PyJWT[crypto]>=2.8.0     # JWT validation
azure-identity>=1.15.0   # Azure auth (for Key Vault)
azure-keyvault-secrets   # Secrets from Key Vault

# Tools
e2b-code-interpreter     # Sandbox execution
tree-sitter>=0.20.0      # Code parsing
tree-sitter-python       # Python grammar
tree-sitter-javascript   # JavaScript grammar
aiohttp>=3.8.0           # Async HTTP client
httpx                    # HTTP client
jinja2                   # Templating
```

#### Node.js (for ChatUI)
```json
// Key dependencies from ChatUI/package.json
{
  "react": "^18.x",
  "react-router-dom": "^6.x",
  "tailwindcss": "^3.x"
}
```

### Required Data Stores

| Store | Purpose | Collections/Tables |
|-------|---------|-------------------|
| **MongoDB** | Primary persistence | `chat_sessions`, `AppCodeSnapshots`, `AppCodePatchSets`, `WorkflowExports`, `performance_metrics`, `themes` |

### Minimal Startup Command

```bash
# Backend
python run_server.py
# OR
uvicorn shared_app:app --host 0.0.0.0 --port 8000 --reload

# Frontend (separate terminal)
cd ChatUI && npm start
```

---

## Part 4 — Relocation Analysis

### Folders That Could Move Elsewhere (Self-Contained)

| Folder | Reason It Could Move |
|--------|---------------------|
| `workflows/AppGenerator/` | Self-contained workflow with own tools; only needs core runtime API |
| `workflows/AgentGenerator/` | Self-contained workflow; could be a separate package |
| `workflows/SubscriptionAdvisor/` | Self-contained; minimal dependencies |
| `workflows/ValueEngine/` | Self-contained workflow |
| `workflows/_shared/backend_client.py` | HTTP client to external backend; not runtime-dependent |
| `workflows/_shared/app_code_versions.py` | Code versioning logic; could be external |
| `ChatUI/src/workflows/` | Frontend components for specific workflows |
| `docs/` | Documentation; no code dependencies |
| `infra/` | Deployment configs; infrastructure-only |

### Folders That MUST Stay Together

| Folder Group | Reason |
|--------------|--------|
| `core/workflow/` + `core/transport/` + `core/events/` | Tightly coupled execution engine |
| `core/auth/` + `core/multitenant/` | Auth feeds tenant context everywhere |
| `core/data/persistence/` + `core/observability/` | Persistence depends on logging |
| `shared_app.py` + `core/` | FastAPI app imports all core modules |
| `logs/` + `core/` | Logging is imported by every module |
| `workflows/__init__.py` + `core/workflow/workflow_manager.py` | Discovery mechanism |

### Dependency Graph (What Imports What)

```
shared_app.py
    ├── core/core_config.py
    ├── core/transport/*
    ├── core/workflow/*
    ├── core/data/persistence/*
    ├── core/auth/*
    ├── core/runtime/*
    └── logs/*

core/workflow/workflow_manager.py
    ├── core/workflow/orchestration_patterns.py
    ├── core/workflow/agents/*
    └── logs/*

workflows/AppGenerator/*
    ├── core/workflow/* (via tool registration)
    ├── workflows/_shared/*
    └── External: E2B, OpenAI

workflows/_shared/backend_client.py
    └── External: MOZAIKS_BACKEND_URL (no core/ deps)
```

### Relocation Summary

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     RELOCATION SAFETY MATRIX                             │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  SAFE TO MOVE (No internal dependencies beyond API)                      │
│  ├── workflows/AppGenerator/                                             │
│  ├── workflows/AgentGenerator/                                           │
│  ├── workflows/SubscriptionAdvisor/                                      │
│  ├── workflows/ValueEngine/                                              │
│  ├── workflows/_shared/backend_client.py                                 │
│  ├── ChatUI/src/workflows/*                                              │
│  ├── docs/                                                               │
│  └── infra/                                                              │
│                                                                          │
│  MUST STAY (Core runtime interdependencies)                              │
│  ├── shared_app.py                                                       │
│  ├── core/* (entire directory)                                           │
│  ├── logs/*                                                              │
│  ├── workflows/__init__.py (discovery only)                              │
│  └── ChatUI/src/{components,context,hooks,services} (UI runtime)         │
│                                                                          │
│  COULD BE PROMOTED TO CORE (Currently in workflows)                      │
│  ├── workflows/AppGenerator/tools/e2b_sandbox.py → core/capabilities/    │
│  └── workflows/AppGenerator/tools/code_context/ → core/capabilities/     │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Appendix: File Count by Category

| Category | Files | Lines (approx) |
|----------|-------|----------------|
| `mozaiksai/core/` | 45 | ~12,000 |
| `workflows/` | 80 | ~15,000 |
| `ChatUI/src/` | 100+ | ~20,000 |
| `logs/` | 3 | ~800 |
| `tests/` | 15 | ~2,000 |
| `scripts/` | 11 | ~1,000 |
| **Total** | ~250 | ~50,000 |

---

## Appendix B: Namespace Migration (Completed 2026-01-17)

### What Changed

The `core/` directory was moved into a `mozaiksai/` namespace package:

```
BEFORE:                          AFTER:
MozaiksAI/                       MozaiksAI/
├── core/                        ├── mozaiksai/
│   ├── auth/                    │   ├── __init__.py
│   ├── workflow/                │   └── core/
│   └── ...                      │       ├── auth/
                                 │       ├── workflow/
                                 │       └── ...
```

### Import Changes

| Before | After |
|--------|-------|
| `from core.auth import ...` | `from mozaiksai.core.auth import ...` |
| `from core.workflow import ...` | `from mozaiksai.core.workflow import ...` |
| `from core.transport import ...` | `from mozaiksai.core.transport import ...` |

### Migration Statistics

- **246 import statements** updated across **67 files**
- Migration script: `scripts/migrate_core_namespace.py`

### Why This Matters

1. **Namespace collision avoided**: Can now coexist with other `core/` directories
2. **Portable**: `mozaiksai/` can be copied to other repos without renaming
3. **PEP 420 compliant**: Follows Python namespace package standards
4. **Future-proof**: Ready for PyPI distribution if needed

---

## Document Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-01-17 | Engineering Agent | Initial self-inventory |
