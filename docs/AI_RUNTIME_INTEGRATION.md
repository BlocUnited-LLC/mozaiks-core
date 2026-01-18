# AI Runtime Integration

This document describes how mozaiks-core integrates with the MozaiksAI runtime for chat and workflow execution.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                         mozaiks-core                                 │
├─────────────────────────────────────────────────────────────────────┤
│  Frontend (src/)                    │  Backend (backend/)           │
│  ├── auth/         (JWT/OIDC)       │  ├── core/                    │
│  ├── websockets/   (notifications)  │  │   ├── plugin_manager.py    │
│  ├── ai/           (bridge)         │  │   ├── websocket_manager.py │
│  └── chat/         (ChatUI)         │  │   ├── ai_bridge/  ◀── NEW  │
│                                      │  │   └── ...                  │
│                                      │  └── mozaiksai/   ◀── RUNTIME │
└─────────────────────────────────────────────────────────────────────┘
```

## Separation of Concerns

### mozaiks-core owns:
- **Authentication**: JWT validation, OIDC, user sessions
- **Notifications**: Real-time alerts via WebSocket
- **Plugins**: Business features, settings, navigation
- **Subscription**: Tier gating, entitlements

### mozaiksai (AI Runtime) owns:
- **Chat streaming**: Real-time agent responses
- **Workflow execution**: AG2 orchestration
- **Tool execution**: Agent capabilities
- **Persistence**: Chat sessions, context

## Bridge Components

### Backend Bridge (`backend/core/ai_bridge/`)

| File | Purpose |
|------|---------|
| `runtime_proxy.py` | Facade for interacting with AI runtime |
| `auth_bridge.py` | Translates core auth to runtime format |
| `event_bridge.py` | Routes AI events to notifications/plugins |
| `websocket_bridge.py` | Routes WebSocket traffic appropriately |

### Frontend Bridge (`src/ai/runtimeBridge.js`)

- `buildRuntimeWebSocketUrl()` - Build chat WebSocket URLs
- `CoreAuthAdapter` - Adapt core's auth for ChatUI
- `isAIEnabled()` - Check if AI features are available

## WebSocket Routing

```
/ws/notifications/{user_id}           → core's websocket_manager
/ws/plugins/{user_id}                 → core's websocket_manager
/ws/{workflow}/{app}/{chat}/{user}    → mozaiksai runtime
```

## Usage

### Backend: Check AI availability

```python
from core.ai_bridge import get_ai_runtime

runtime = get_ai_runtime()
if runtime.is_available:
    workflows = runtime.list_workflows()
```

### Backend: Validate access

```python
from core.ai_bridge import validate_runtime_access

allowed, reason = validate_runtime_access(
    user_id=user.id,
    app_id=APP_ID,
    workflow_name="AppGenerator",
    subscription_tier=user.tier,
)

if not allowed:
    raise HTTPException(403, reason)
```

### Frontend: Connect to chat

```javascript
import { buildRuntimeWebSocketUrl, CoreAuthAdapter } from '../ai/runtimeBridge';

const wsUrl = buildRuntimeWebSocketUrl({
  workflowName: 'AppGenerator',
  appId: APP_ID,
  chatId: chatSession.id,
  userId: user.user_id,
  token: await getAccessToken(),
});

const ws = new WebSocket(wsUrl);
```

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `MOZAIKS_AI_ENABLED` | `true` | Enable/disable AI features |
| `MOZAIKSAI_RUNTIME_URL` | (same-origin) | AI runtime base URL |
| `MOZAIKS_RESTRICTED_WORKFLOWS` | `` | Comma-separated premium-only workflows |
| `VITE_AI_RUNTIME_PORT` | `8000` | Frontend: AI runtime port |
| `VITE_AI_ENABLED` | `true` | Frontend: show AI features |

## Event Flow

```
User sends chat message
        │
        ▼
┌───────────────────┐
│ ChatUI (frontend) │
└────────┬──────────┘
         │ WebSocket
         ▼
┌───────────────────┐
│ mozaiksai runtime │ ◀── Handles workflow execution
└────────┬──────────┘
         │ Event
         ▼
┌───────────────────┐
│ AI Event Bridge   │ ◀── Routes to core systems
└────────┬──────────┘
         │
    ┌────┴────┐
    ▼         ▼
Notifications  Event Bus
(user alerts)  (plugin hooks)
```

## Deployment Options

### Option A: Same Process (Development)
Both core and runtime in one Python process.
WebSockets route internally.

### Option B: Separate Processes (Production)
- Core: FastAPI on port 5000
- Runtime: FastAPI on port 8000
- Frontend proxies to both

### Option C: Kubernetes
- Core: core-service
- Runtime: runtime-service
- Ingress routes by path
