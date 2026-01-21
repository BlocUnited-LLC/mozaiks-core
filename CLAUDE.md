# CLAUDE.md - MozaiksCore AI Agent Instructions

> **Architecture Principle**: Plugins should be **self-contained** and integrate via configuration files. The core system handles orchestration — plugins should not require core modifications to function.

Your open-source **"tenant runtime core"** with **integrated AI runtime**.  

**Contains:**

- App shell runtime (frontend + backend skeletons)  
- Plugin runtime contract + loaders  
- **AI Runtime** (`backend/core/ai_runtime/`) — AG2-based agent orchestration  
- **ChatUI** (`src/chat/`) — Full chat interface for AI interactions  
- **AI Bridge** (`backend/core/ai_bridge/`) — Core ↔ AI runtime integration  
- Keycloak assets (already migrated)  
- UI theme rendering system (reads `ui-theme.yaml`)  

This is what **“apps are built on.”**

---

## Project Overview

**MozaiksCore** is a production-grade application scaffold with authentication, routing, subscriptions, plugin infrastructure, and **integrated AI agent runtime**. Features are added through **sandboxed plugins** that register via JSON configurations.

**Tech Stack**: FastAPI + MongoDB (backend) | React + Tailwind + Vite (frontend) | JWT auth | WebSockets | AG2 (AI agents)

---

## 🏗️ Core System Files (Reference)

These files handle the core orchestration. Plugins integrate with them but shouldn't need to modify them:

```
/backend/main.py              # FastAPI entry point
/backend/core/director.py     # Orchestration & routing
/backend/core/plugin_manager.py  # Plugin loading & execution
/backend/core/event_bus.py    # Event pub/sub system
/backend/core/state_manager.py   # State management
/backend/core/ai_bridge/      # Core ↔ AI runtime bridge
/backend/core/ai_runtime/     # AI Runtime (workflow, transport, data, auth)
/backend/shared_app.py        # AI runtime FastAPI app
/src/App.jsx                  # Main React app (with ChatUI integration)
/src/main.jsx                 # React entry point
/src/core/plugins/*           # Plugin system components
/src/core/theme/*             # Theme system components
/src/chat/                    # ChatUI components
/src/chat/adapters/MozaiksCoreAuthAdapter.js  # Auth bridge for ChatUI
/src/chat/components/layout/GlobalChatWidgetWrapper.js  # Global widget renderer
/src/chat/hooks/useWidgetMode.js  # Widget mode hook for pages
/src/ai/runtimeBridge.js      # Frontend auth bridge for AI
```

---

## 💬 Chat Widget Integration

The ChatUI system provides a persistent chat widget that appears on all non-chat pages.

### Enabling Widget Mode on a Page

When creating a page that should have chat widget access:

```javascript
import { useWidgetMode } from '../chat/hooks/useWidgetMode';

function MyPage() {
  useWidgetMode(); // ← Add this line
  return <div>My page content</div>;
}
```

The page will now have:
- Floating chat widget in bottom-left corner
- 🧠 brain toggle to return to active workflow
- Automatic state management during navigation

**See [docs/WIDGET_MODE.md](docs/WIDGET_MODE.md) for full documentation.**

---

## ✅ Plugin Development (Primary Workflow)

| Task | Location |
|------|----------|
| Create plugin backend | `/backend/plugins/{plugin_name}/` |
| Create plugin frontend | `/src/plugins/{plugin_name}/` |
| Register plugin | `/backend/core/config/plugin_registry.json` |
| Add navigation menu item | `/backend/core/config/navigation_config.json` |
| Set subscription access | `/backend/core/config/subscription_config.json` |
| Add user settings | `/backend/core/config/settings_config.json` |
| Add notification types | `/backend/core/config/notifications_config.json` |
| Update theme/branding | `/backend/core/config/theme_config.json` |

---

## Creating a New Plugin - Complete Guide

### Step 1: Backend Files

Create `/backend/plugins/your_plugin/` with:

**`__init__.py`** (empty file, required)

**`logic.py`** (required):
```python
import logging
from core.config.database import db
from bson import ObjectId

logger = logging.getLogger("mozaiks_plugins.your_plugin")

async def execute(data):
    """Main entry point - ALL plugin requests come here."""
    action = data.get("action", "")
    user_id = data.get("user_id", "")
    
    if action == "list":
        collection = db["your_plugin_items"]
        items = await collection.find({"user_id": user_id}).to_list(100)
        # Convert ObjectId to string for JSON serialization
        for item in items:
            item["_id"] = str(item["_id"])
        return {"items": items, "count": len(items)}
    
    elif action == "create":
        collection = db["your_plugin_items"]
        doc = {
            "user_id": user_id,
            "title": data.get("title", ""),
            "content": data.get("content", "")
        }
        result = await collection.insert_one(doc)
        return {"success": True, "id": str(result.inserted_id)}
    
    elif action == "delete":
        collection = db["your_plugin_items"]
        item_id = data.get("item_id")
        await collection.delete_one({"_id": ObjectId(item_id), "user_id": user_id})
        return {"success": True}
    
    elif action == "get_settings":
        from . import settings
        return await settings.get_settings(user_id, data)
    
    elif action == "save_settings":
        from . import settings
        return await settings.save_settings(user_id, data)
    
    return {"error": "Unknown action", "action": action}
```

**`settings.py`** (optional):
```python
from typing import Dict, Any

async def get_default_settings() -> Dict[str, Any]:
    return {"enabled": True, "notifications_enabled": True}

async def get_settings(user_id: str, data: Dict[str, Any]) -> Dict[str, Any]:
    settings_manager = data.get("settings_manager")
    if settings_manager:
        saved = await settings_manager.get_plugin_settings(user_id, "your_plugin")
        return saved if saved else await get_default_settings()
    return await get_default_settings()

async def save_settings(user_id: str, data: Dict[str, Any]) -> Dict[str, Any]:
    settings_manager = data.get("settings_manager")
    new_settings = data.get("settings", {})
    if settings_manager:
        await settings_manager.save_plugin_settings(user_id, "your_plugin", new_settings)
        return {"success": True}
    return {"success": False, "message": "Settings manager not available"}
```

### Step 2: Frontend Files

Create `/src/plugins/your_plugin/` with:

**`register.js`**:
```javascript
export default {
  name: "your_plugin",
  displayName: "Your Plugin",
  description: "Description of what this plugin does",
  version: "1.0.0",
  icon: "puzzle"
};
```

**`index.js`**:
```jsx
import React, { useState, useEffect } from 'react';
import { useAuth } from '../../auth/AuthContext';

const YourPlugin = () => {
  const { user, token } = useAuth();
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    fetchItems();
  }, []);

  const fetchItems = async () => {
    try {
      const response = await fetch('/api/execute/your_plugin', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${token}`
        },
        body: JSON.stringify({ action: 'list' })
      });
      const result = await response.json();
      setItems(result.items || []);
    } catch (error) {
      console.error('Error:', error);
    } finally {
      setLoading(false);
    }
  };

  const createItem = async (title, content) => {
    const response = await fetch('/api/execute/your_plugin', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`
      },
      body: JSON.stringify({ action: 'create', title, content })
    });
    if (response.ok) fetchItems();
  };

  const deleteItem = async (itemId) => {
    await fetch('/api/execute/your_plugin', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`
      },
      body: JSON.stringify({ action: 'delete', item_id: itemId })
    });
    fetchItems();
  };

  if (loading) return <div className="p-6">Loading...</div>;

  return (
    <div className="p-6">
      <h1 className="text-2xl font-bold mb-4">Your Plugin</h1>
      <div className="space-y-4">
        {items.map(item => (
          <div key={item._id} className="p-4 bg-secondary rounded-lg">
            <h3 className="font-semibold">{item.title}</h3>
            <p className="text-text-secondary">{item.content}</p>
            <button 
              onClick={() => deleteItem(item._id)}
              className="mt-2 text-red-500 hover:text-red-700"
            >
              Delete
            </button>
          </div>
        ))}
      </div>
    </div>
  );
};

export default YourPlugin;
```

**`settings/SettingsPanel.jsx`** (optional):
```jsx
import React, { useState, useEffect } from 'react';

const SettingsPanel = ({ currentSettings, onSettingsChange }) => {
  const [settings, setSettings] = useState({ enabled: true });
  
  useEffect(() => {
    if (currentSettings) setSettings(currentSettings);
  }, [currentSettings]);

  const handleSubmit = (e) => {
    e.preventDefault();
    onSettingsChange(settings);
  };

  return (
    <form onSubmit={handleSubmit} className="p-4">
      <label className="flex items-center gap-2">
        <input
          type="checkbox"
          checked={settings.enabled}
          onChange={(e) => setSettings({...settings, enabled: e.target.checked})}
        />
        Enable Plugin
      </label>
      <button type="submit" className="mt-4 px-4 py-2 bg-accent text-white rounded">
        Save Settings
      </button>
    </form>
  );
};

export default SettingsPanel;
```

### Step 3: Update Configuration Files

**`/backend/core/config/plugin_registry.json`** - Add:
```json
{
  "name": "your_plugin",
  "display_name": "Your Plugin",
  "description": "Description here",
  "version": "1.0.0",
  "enabled": true,
  "backend": "plugins.your_plugin.logic"
}
```

**`/backend/core/config/navigation_config.json`** - Add to "plugins" array:
```json
{
  "plugin_name": "your_plugin",
  "label": "Your Plugin",
  "path": "/plugins/your_plugin",
  "icon": "puzzle"
}
```

**`/backend/core/config/subscription_config.json`** - Add plugin to appropriate tier:
```json
{
  "name": "premium",
  "plugins_unlocked": ["your_plugin"]
}
```

---

## Core Integrations

### Database Access
```python
from core.config.database import db

collection = db["your_collection"]
await collection.insert_one({"data": "value"})
await collection.find({"user_id": user_id}).to_list(100)
await collection.update_one({"_id": ObjectId(id)}, {"$set": {"field": "value"}})
await collection.delete_one({"_id": ObjectId(id)})
```

### Event Bus
```python
from core.event_bus import event_bus, on_event

# Publish events
await event_bus.publish("your_plugin:item_created", {"user_id": user_id, "item_id": item_id})

# Subscribe to events (in your logic.py)
@on_event("user:registered")
async def handle_user_registered(data):
    # React to user registration
    pass
```

### Notifications
```python
from core.notifications_manager import notifications_manager

await notifications_manager.create_notification(
    user_id=user_id,
    notification_type="your_plugin_event",
    title="Action Complete",
    message="Your action was completed successfully.",
    metadata={"item_id": item_id}
)
```

### Settings Manager
```python
from core.settings_manager import settings_manager

# Get settings
settings = await settings_manager.get_plugin_settings(user_id, "your_plugin")

# Save settings
await settings_manager.save_plugin_settings(user_id, "your_plugin", {"key": "value"})
```

### WebSocket (real-time updates)
```python
from core.websocket_manager import websocket_manager

# Send to specific user
await websocket_manager.send_to_user(user_id, {
    "type": "your_plugin_update",
    "data": {"item_id": item_id}
})
```

---

## Environment Variables

```env
MONGODB_URI=mongodb+srv://...        # Preferred: MongoDB connection (Mozaiks-injected)
DATABASE_URI=mongodb+srv://...       # Legacy fallback (manual/local)
JWT_SECRET=your_secret_key           # Required: JWT signing key
ENV=development                      # development | production
MONETIZATION=0                       # 0 = disabled, 1 = enabled
MOZAIKS_MANAGED=false                # false = self-hosted, true = platform
OPENAI_API_KEY=your_key              # Optional: for AI features
```

---

## Quick Commands

```bash
# Backend
cd backend
python -m venv venv && venv\Scripts\activate  # Windows
pip install -r requirements.txt
uvicorn main:app --reload --port 8080

# Frontend
npm install
npm run dev
```

---

## Code Quality Rules

1. **Always async/await** - All database and external calls must be async
2. **Validate input** - Check data exists before using: `data.get("field", "")`
3. **Handle errors** - Return meaningful error messages: `{"error": "message"}`
4. **ObjectId conversion** - Always convert `_id` to string for JSON: `str(item["_id"])`
5. **Self-contained plugins** - No dependencies between plugins
6. **Use logging** - `logger.info()`, `logger.error()` for debugging

---

## Testing Checklist

- [ ] Plugin loads without errors
- [ ] CRUD operations work (create, read, update, delete)
- [ ] Settings save and load correctly
- [ ] Navigation item appears in sidebar
- [ ] Works with different subscription tiers (if monetization enabled)
- [ ] Error states handled gracefully
- [ ] Loading states shown to user

---

## 📬 Notification System

MozaiksCore includes a multi-channel notification system with templates, scheduling, and admin broadcast capabilities.

### Available Channels

| Channel | Environment Variables | Description |
|---------|----------------------|-------------|
| `in_app` | Always enabled | Database + WebSocket real-time |
| `email` | `EMAIL_SERVICE_URL` | External email service |
| `sms` | `SMS_ACCOUNT_SID`, `SMS_AUTH_TOKEN`, `SMS_FROM_NUMBER` | Twilio SMS |
| `web_push` | `PUSH_VAPID_PUBLIC_KEY`, `PUSH_VAPID_PRIVATE_KEY` | Browser push |

### Sending Notifications

```python
# Simple notification (uses existing notifications_manager)
from core.notifications_manager import notifications_manager

await notifications_manager.create_notification(
    user_id=user_id,
    notification_type="your_plugin_event",
    title="Action Complete",
    message="Your action was completed successfully.",
    metadata={"item_id": item_id}
)

# Direct channel access (for specific channels)
from core.notifications import in_app_channel, email_channel

await in_app_channel.send(
    user_id=user_id,
    notification_type="reminder",
    title="Task Due",
    message="Your task is due tomorrow"
)
```

### Admin Broadcast

```python
from core.notifications import broadcast_service

# Broadcast to all users
await broadcast_service.broadcast(
    notification_type="announcement",
    title="System Update",
    message="We've added new features!",
    target={"type": "all"},
    channels=["in_app", "email"]
)

# Target specific subscription tier
await broadcast_service.broadcast(
    notification_type="premium_feature",
    title="New Premium Feature",
    message="Check out your new premium features!",
    target={"type": "subscription", "tier": "premium"},
    channels=["in_app"]
)
```

### Scheduled Notifications

```python
from core.notifications import digest_scheduler
from datetime import datetime, timedelta

# Schedule a notification for later
await digest_scheduler.schedule_notification(
    user_id="user123",
    notification_type="reminder",
    title="Task Due",
    message="Your task is due now!",
    scheduled_for=datetime.utcnow() + timedelta(hours=24)
)
```

### Adding Notification Templates

Edit `/backend/core/config/notification_templates.json`:

```json
{
  "templates": {
    "your_notification_type": {
      "in_app": {
        "title": "{{title}}",
        "body": "{{message}}"
      },
      "email": {
        "subject": "{{title}} - {{app_name}}",
        "text_body": "Hi {{username}},\n\n{{message}}\n\nBest,\n{{app_name}}"
      },
      "sms": {
        "body": "{{app_name}}: {{message}}"
      },
      "push": {
        "title": "{{title}}",
        "body": "{{message}}"
      }
    }
  }
}
```

### Admin API Endpoints

All require `X-Mozaiks-App-Admin-Key` header:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/__mozaiks/admin/notifications/broadcast` | POST | Send broadcast notification |
| `/__mozaiks/admin/notifications/broadcasts` | GET | Get broadcast history |
| `/__mozaiks/admin/notifications/schedule` | POST | Schedule notification |
| `/__mozaiks/admin/notifications/channels` | GET | Get channel status |
| `/__mozaiks/admin/notifications/templates` | GET | List templates |

### Push Subscription API (User-facing)

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/push/vapid-public-key` | GET | Get VAPID public key |
| `/api/push/subscribe` | POST | Subscribe device to push |
| `/api/push/unsubscribe` | POST | Unsubscribe device |
| `/api/push/status` | GET | Get push status for user |

### File Locations

```
/backend/core/notifications/
  __init__.py                    # Module exports
  templates.py                   # Template rendering
  scheduler.py                   # Digest/scheduled notifications
  broadcast.py                   # Admin broadcast service
  channels/
    __init__.py                  # Channel registry
    base.py                      # Base channel interface
    in_app.py                    # In-app + WebSocket
    email_channel.py             # Email delivery
    sms.py                       # SMS via Twilio
    web_push.py                  # Browser push (VAPID)

/backend/core/config/
  notification_templates.json    # Template definitions
  notifications_config.json      # Category/type definitions
```

---

## 🤖 AI Runtime Integration

MozaiksCore includes an integrated AI runtime for agent orchestration and chat-based AI interactions.

### Architecture

The AI runtime lives in `backend/core/ai_runtime/` with module aliasing for `mozaiksai.*` imports. Communication between core and AI runtime goes through the **bridge layer** (`backend/core/ai_bridge/`).

```
┌─────────────────────────────────────────────────────┐
│                  mozaiks-core                        │
│  ┌─────────────┐          ┌──────────────────────┐  │
│  │ core/       │ ◄──────► │ core/ai_runtime/     │  │
│  │ (plugins,   │ ai_bridge│ (AI runtime,         │  │
│  │  auth, ws)  │          │  chat, workflows)    │  │
│  └─────────────┘          └──────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

### Backend Structure

```
/backend/core/ai_runtime/        # AI Runtime
  auth/                          # JWT/OIDC validation
  transport/                     # WebSocket session management
  workflow/                      # Agent factory, orchestration
  events/                        # Event dispatcher
  data/                          # MongoDB persistence
  observability/                 # Logging, metrics

/backend/core/ai_bridge/         # Bridge Layer
  runtime_proxy.py               # Facade for core → runtime calls
  auth_bridge.py                 # Auth context translation
  event_bridge.py                # AI events → notifications
  websocket_bridge.py            # WebSocket routing

/backend/shared_app.py           # AI runtime FastAPI app
```

### Frontend Structure

```
/src/chat/                       # ChatUI
  components/                    # Chat interface components
  context/                       # ChatUIContext state management
  hooks/                         # useChat, useStreaming
  services/                      # WebSocket/REST adapters
  workflows/                     # Per-workflow UI components

/src/ai/
  AICapabilitiesPage.jsx         # Capability browser
  runtimeBridge.js               # Auth bridge for ChatUI
```

### WebSocket Routing

| Path | Handler | Purpose |
|------|---------|---------|
| `/ws/notifications/{user_id}` | `core/websocket_manager.py` | Notifications |
| `/ws/{workflow}/{app}/{chat}/{user}` | `mozaiksai` runtime | Chat streaming |

### Environment Variables

```env
# AI Runtime
MOZAIKS_AI_ENABLED=true
OPENAI_API_KEY=sk-...            # Required for AI features

# Auth (shared with core)
AUTH_ENABLED=true
MOZAIKS_OIDC_AUTHORITY=https://mozaiks.ciamlogin.com
AUTH_AUDIENCE=api://mozaiks-auth
```

### MongoDB Collections (AI)

| Collection | Purpose |
|------------|---------|
| `chat_sessions` | Conversation metadata |
| `chat_messages` | Message history |
| `tool_executions` | Tool call audit log |
| `agent_traces` | Execution traces |
| `artifacts` | Generated outputs |

### Deployment Options

**Same Process** (Simple):
```python
# main.py
from mozaiksai.shared_app import app as runtime_app
director_app.mount("/ai", runtime_app)
```

**Separate Processes** (Production):
```bash
# Core
uvicorn backend.main:app --port 8080

# AI Runtime
uvicorn backend.shared_app:app --port 8080
```
