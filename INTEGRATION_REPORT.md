# MozaiksAI Integration Report

**Date:** January 17, 2026  
**Purpose:** Document the AI runtime integration into mozaiks-core  
**Repos Affected:** mozaiks-core, mozaiks-app

---

## Executive Summary

We have integrated the MozaiksAI runtime (chat/workflow execution engine) into mozaiks-core while preserving separation of concerns. The AI runtime lives in a dedicated `mozaiksai/` namespace to avoid collisions with existing `core/` code.

**Key Changes:**
1. Added `backend/mozaiksai/` — the complete AI runtime (AG2-based agent orchestration)
2. Added `backend/core/ai_bridge/` — bridge layer connecting core systems to AI runtime
3. Added `src/chat/` — ChatUI components for AI interactions
4. Added `src/ai/runtimeBridge.js` — frontend bridge for auth integration

---

## New Directory Structure

```
mozaiks-core/
├── backend/
│   ├── core/                    # EXISTING - Plugin system, notifications, settings
│   │   ├── ai_bridge/           # NEW - Bridge to AI runtime
│   │   │   ├── __init__.py
│   │   │   ├── runtime_proxy.py
│   │   │   ├── auth_bridge.py
│   │   │   ├── event_bridge.py
│   │   │   └── websocket_bridge.py
│   │   ├── websocket_manager.py # UNCHANGED - Notifications only
│   │   ├── plugin_manager.py    # UNCHANGED
│   │   └── ...
│   │
│   ├── mozaiksai/               # NEW - AI Runtime (namespaced)
│   │   ├── __init__.py
│   │   └── core/
│   │       ├── auth/            # JWT/OIDC validation
│   │       ├── transport/       # WebSocket chat streaming
│   │       ├── workflow/        # Agent orchestration, tools
│   │       ├── events/          # Event dispatcher
│   │       ├── data/            # Persistence, themes
│   │       └── observability/   # Logging, performance
│   │
│   ├── shared_app.py            # NEW - AI runtime FastAPI app
│   ├── run_server.py            # NEW - AI runtime entry point
│   └── .env.example             # NEW - Runtime config template
│
├── src/
│   ├── ai/                      # NEW - AI frontend bridge
│   │   ├── AICapabilitiesPage.jsx  # EXISTING
│   │   └── runtimeBridge.js     # NEW - Auth/WebSocket bridge
│   │
│   ├── chat/                    # NEW - ChatUI components
│   │   ├── components/
│   │   ├── context/
│   │   ├── hooks/
│   │   ├── services/
│   │   ├── workflows/           # UI components per workflow
│   │   └── App.js
│   │
│   └── websockets/              # UNCHANGED - Notifications only
│
└── docs/
    └── AI_RUNTIME_INTEGRATION.md  # NEW - Integration guide
```

---

## What Each Component Does

### Backend: `mozaiksai/` (AI Runtime)

| Directory | Purpose |
|-----------|---------|
| `mozaiksai/core/auth/` | JWT validation, OIDC discovery, WebSocket auth |
| `mozaiksai/core/transport/` | WebSocket session management, chat streaming |
| `mozaiksai/core/workflow/` | Agent factory, tool registration, orchestration |
| `mozaiksai/core/events/` | Unified event dispatcher for UI/tool events |
| `mozaiksai/core/data/` | MongoDB persistence, chat sessions, themes |
| `mozaiksai/core/observability/` | Logging, performance metrics |

**Entry Points:**
- `shared_app.py` — FastAPI app with all AI endpoints
- `run_server.py` — Uvicorn launcher

### Backend: `core/ai_bridge/` (Bridge Layer)

| File | Purpose |
|------|---------|
| `runtime_proxy.py` | Facade for core to call AI runtime methods |
| `auth_bridge.py` | Translate core auth context → runtime format |
| `event_bridge.py` | Route AI events → notifications + event bus |
| `websocket_bridge.py` | Route WebSocket traffic to correct handler |

### Frontend: `src/chat/` (ChatUI)

| Directory | Purpose |
|-----------|---------|
| `components/chat/` | ChatInterface, ChatMessage, ArtifactPanel |
| `context/` | ChatUIContext for state management |
| `services/` | WebSocket/REST adapters |
| `workflows/` | Per-workflow UI components (ActionPlan, CodeEditor, etc.) |

### Frontend: `src/ai/runtimeBridge.js`

Bridges ChatUI with core's auth system:
- `buildRuntimeWebSocketUrl()` — Build authenticated WebSocket URLs
- `CoreAuthAdapter` — Adapt core's AuthContext for ChatUI
- `getRuntimeAuthHeaders()` — Headers for API calls

---

## WebSocket Architecture

### Routing Rules

| Path Pattern | Handler | Purpose |
|--------------|---------|---------|
| `/ws/notifications/{user_id}` | `core/websocket_manager.py` | Real-time notifications |
| `/ws/plugins/{user_id}` | `core/websocket_manager.py` | Plugin events |
| `/ws/{workflow}/{app}/{chat}/{user}` | `mozaiksai` runtime | Chat streaming |

### Connection Flow (Chat)

```
Frontend                          Backend
────────                          ───────
ChatUI
  │
  │ buildRuntimeWebSocketUrl()
  │ ws://host:8000/ws/AppGenerator/app123/chat456/user789?access_token=xxx
  │
  ▼
┌─────────────────────────────────────────────────────────────┐
│ mozaiksai/core/transport/                                    │
│   SimpleTransport.register(chat_id, websocket)              │
│   ↓                                                         │
│ mozaiksai/core/workflow/                                    │
│   WorkflowManager.run_chat(workflow, context)               │
│   ↓                                                         │
│ AG2 GroupChat execution                                     │
│   ↓                                                         │
│ Stream responses back via WebSocket                         │
└─────────────────────────────────────────────────────────────┘
```

---

## Import Patterns

### Why `mozaiksai/` Namespace?

The AI runtime was originally in a separate repo (`MozaiksAI/`) with `from core.*` imports. Moving it into mozaiks-core would conflict with the existing `backend/core/` (plugin system).

**Solution:** Namespace the runtime under `mozaiksai/`:
- Old: `from core.workflow import workflow_manager`
- New: `from mozaiksai.core.workflow import workflow_manager`

This allows both systems to coexist:
```python
# Plugin system
from core.plugin_manager import plugin_manager

# AI runtime
from mozaiksai.core.workflow import workflow_manager
```

---

## Environment Variables

### AI Runtime Config (add to existing `.env`)

```bash
# AI Runtime
MOZAIKS_AI_ENABLED=true
MOZAIKSAI_RUNTIME_URL=http://localhost:8000  # If separate process

# Workflow Restrictions
MOZAIKS_RESTRICTED_WORKFLOWS=  # Comma-separated premium-only workflows

# MongoDB (shared with core)
MONGO_URI=mongodb://localhost:27017

# OpenAI (runtime needs this)
OPENAI_API_KEY=sk-...

# Auth (runtime uses same OIDC as core)
AUTH_ENABLED=true
MOZAIKS_OIDC_AUTHORITY=https://mozaiks.ciamlogin.com
MOZAIKS_OIDC_TENANT_ID=9d0073d5-42e8-46f0-a325-5b4be7b1a38d
AUTH_AUDIENCE=api://mozaiks-auth
```

### Frontend Config (add to Vite env)

```bash
VITE_AI_ENABLED=true
VITE_AI_RUNTIME_PORT=8000
```

---

## Deployment Options

### Option A: Same Process (Simple)

Run both core and runtime in one FastAPI app:

```python
# main.py
from core.director import app as director_app
from mozaiksai.shared_app import app as runtime_app

# Mount runtime under /ai prefix
director_app.mount("/ai", runtime_app)
```

### Option B: Separate Processes (Recommended for Production)

```bash
# Terminal 1: Core
uvicorn backend.main:app --port 5000

# Terminal 2: AI Runtime  
uvicorn backend.shared_app:app --port 8000
```

Frontend proxies to both via Vite config or nginx.

### Option C: Kubernetes

```yaml
# core-deployment.yaml
spec:
  containers:
    - name: core
      image: mozaiks-core:latest
      ports:
        - containerPort: 5000

# runtime-deployment.yaml
spec:
  containers:
    - name: runtime
      image: mozaiks-runtime:latest
      ports:
        - containerPort: 8000
```

---

## Integration Checklist

### Backend Tasks

- [ ] Ensure `mozaiksai/` is in Python path
- [ ] Configure MongoDB connection string
- [ ] Set OpenAI API key
- [ ] Configure OIDC settings (can share with core)
- [ ] Test `/api/health` on runtime port
- [ ] Test `/api/workflows` returns workflow list

### Frontend Tasks

- [ ] Add `VITE_AI_RUNTIME_PORT` to env
- [ ] Import ChatUI components where needed
- [ ] Wire `CoreAuthAdapter` to existing AuthContext
- [ ] Test WebSocket connection to runtime

### Bridge Tasks

- [ ] Initialize `AIEventBridge` with notification_manager
- [ ] Configure `WebSocketBridge` routing
- [ ] Test event flow: AI completion → notification appears

---

## Files Added (Summary)

| Path | Lines | Description |
|------|-------|-------------|
| `backend/mozaiksai/**` | ~8,000 | Full AI runtime |
| `backend/core/ai_bridge/**` | ~400 | Bridge layer |
| `backend/shared_app.py` | ~2,300 | Runtime FastAPI app |
| `backend/run_server.py` | ~35 | Entry point |
| `backend/.env.example` | ~120 | Config template |
| `backend/infra/**` | ~100 | Docker/compose files |
| `src/chat/**` | ~3,500 | ChatUI components |
| `src/ai/runtimeBridge.js` | ~120 | Frontend bridge |
| `docs/AI_RUNTIME_INTEGRATION.md` | ~150 | Integration guide |

**Total: ~15,000 lines of new code**

---

## Related Repository: mozaiks-app

A separate repo `mozaiks-app` was created for product-specific code:

```
mozaiks-app/
├── plugins/
│   ├── ad_marketplace/
│   ├── investor_portal/
│   ├── provisioning_bridge/
│   └── control_plane_bridge/
│
└── workflows/
    ├── AgentGenerator/
    ├── AppGenerator/
    ├── ValueEngine/
    └── SubscriptionAdvisor/
```

**GitHub:** https://github.com/BlocUnited-LLC/mozaiks-app

These workflows can be loaded by the runtime via environment config or mounted at runtime.

---

## Questions for Core Team

1. **Auth Strategy:** Should runtime use core's JWT validation directly, or validate independently with same OIDC config?

2. **Process Model:** Same process or separate? Affects how we configure routing.

3. **Notification Integration:** Want AI events (workflow complete, failure) to appear in core's notification system?

4. **Subscription Gating:** Should certain workflows require premium tier? If so, how does core expose entitlements?

5. **Plugin Bridge:** Any plugins that need to call AI tools? (We can add tool→plugin bridge)

---

## Contact

For questions about the AI runtime integration:
- **Runtime Architecture:** Reference `AGENTS.md` in MozaiksAI repo
- **ChatUI:** Reference `src/chat/README.md` (if exists) or component JSDoc
- **Bridge Layer:** See `backend/core/ai_bridge/__init__.py` docstrings
