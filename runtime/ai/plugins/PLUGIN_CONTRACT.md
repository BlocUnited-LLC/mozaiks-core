# MozaiksCore Plugin Contract

> **For AI Agent Code Generation**: This document defines the exact contracts that generated plugin code MUST follow to work with the MozaiksCore runtime.

---

## Quick Reference

```python
# MINIMAL VALID PLUGIN - logic.py
from typing import Any, Dict

async def execute(data: dict) -> dict:
    """Main entry point - ALL plugin requests come here."""
    action = data.get("action", "")
    user_id = data["user_id"]  # Always present, injected by runtime
    app_id = data["app_id"]    # Always present, injected by runtime
    
    if action == "list":
        return {"items": [], "count": 0}
    
    return {"error": f"Unknown action: {action}"}
```

---

## 1. Directory Structure Contract

```
plugins/
└── {plugin_name}/
    ├── __init__.py           # Required (can be empty)
    ├── logic.py              # REQUIRED: Main entry point
    └── backend/              # Alternative location
        └── logic.py          # If not in root, must be here
```

**Rules:**
- Plugin name = directory name (snake_case)
- `logic.py` must exist in either root OR `backend/` folder
- Runtime searches root first, then `backend/`

---

## 2. Execute Function Contract

### Signature (REQUIRED)

```python
async def execute(data: dict) -> dict:
    """
    Main entry point for ALL plugin requests.
    
    Args:
        data: Request payload with runtime-injected context
        
    Returns:
        dict: Response payload (JSON-serializable)
    """
```

**CRITICAL RULES:**
1. Function MUST be named `execute` (fallback: `run`)
2. Function MUST be `async` (recommended) or sync
3. Function MUST accept a single `dict` parameter
4. Function MUST return a `dict`
5. NEVER raise exceptions - return `{"error": "message"}` instead

### Runtime-Injected Fields (ALWAYS PRESENT)

```python
async def execute(data: dict) -> dict:
    # These fields are ALWAYS injected by runtime - NEVER trust client values
    user_id = data["user_id"]      # str: Authenticated user's ID
    app_id = data["app_id"]        # str: App instance ID
    
    # Full context object (for advanced use)
    context = data["_context"]     # dict with:
    # {
    #     "app_id": str,
    #     "user_id": str,
    #     "username": str | None,
    #     "roles": list[str],
    #     "is_superadmin": bool
    # }
    
    # Client-provided fields (action pattern)
    action = data.get("action", "")  # str: Action to perform
    # ... other client fields ...
```

### Action Pattern (RECOMMENDED)

```python
async def execute(data: dict) -> dict:
    action = data.get("action", "")
    user_id = data["user_id"]
    
    # Route to action handlers
    if action == "list":
        return await handle_list(user_id, data)
    elif action == "create":
        return await handle_create(user_id, data)
    elif action == "get":
        return await handle_get(user_id, data)
    elif action == "update":
        return await handle_update(user_id, data)
    elif action == "delete":
        return await handle_delete(user_id, data)
    elif action == "get_settings":
        return await handle_get_settings(user_id, data)
    elif action == "save_settings":
        return await handle_save_settings(user_id, data)
    else:
        return {"error": f"Unknown action: {action}"}
```

---

## 3. Database Access Contract

### Import

```python
from core.config.database import db
from bson import ObjectId
```

### Available Objects

| Import | Type | Description |
|--------|------|-------------|
| `db` | `AsyncIOMotorDatabase` | MongoDB database instance |
| `users_collection` | Collection | Pre-configured users collection |
| `settings_collection` | Collection | Pre-configured settings collection |

### Collection Access

```python
from core.config.database import db
from bson import ObjectId

async def execute(data: dict) -> dict:
    user_id = data["user_id"]
    
    # Get your plugin's collection
    collection = db["your_plugin_items"]
    
    # CRUD operations (all async)
    items = await collection.find({"user_id": user_id}).to_list(100)
    
    doc = await collection.find_one({"_id": ObjectId(item_id)})
    
    result = await collection.insert_one({"user_id": user_id, "title": "..."})
    new_id = str(result.inserted_id)
    
    await collection.update_one(
        {"_id": ObjectId(item_id), "user_id": user_id},
        {"$set": {"title": "new title"}}
    )
    
    await collection.delete_one({"_id": ObjectId(item_id), "user_id": user_id})
```

### ObjectId Rules

```python
# ALWAYS convert ObjectId to string for JSON responses
for item in items:
    item["_id"] = str(item["_id"])

# ALWAYS scope queries by user_id (security!)
await collection.find({"user_id": user_id}).to_list(100)  # ✅ CORRECT
await collection.find({}).to_list(100)                     # ❌ WRONG - leaks data
```

---

## 4. Event Bus Contract

### Import

```python
from core.event_bus import event_bus
```

### Publishing Events

```python
from core.event_bus import event_bus

async def execute(data: dict) -> dict:
    user_id = data["user_id"]
    
    # After successful action, publish event
    await collection.insert_one(doc)
    
    event_bus.publish("your_plugin:item_created", {
        "user_id": user_id,
        "item_id": str(result.inserted_id),
        "timestamp": datetime.utcnow().isoformat()
    })
    
    return {"success": True, "id": str(result.inserted_id)}
```

### Event Naming Convention

```
{plugin_name}:{action_past_tense}
```

Examples:
- `task_manager:task_created`
- `task_manager:task_completed`
- `notes:note_deleted`

### Subscribing to Events (Advanced)

```python
# In your logic.py module level
from core.event_bus import event_bus

def handle_user_registered(data):
    """React to user registration."""
    user_id = data.get("user_id")
    # Initialize plugin data for new user...

# Subscribe at module load
event_bus.subscribe("user:registered", handle_user_registered)
```

---

## 5. Notifications Contract

### Import

```python
from core.notifications_manager import notifications_manager
```

### Creating Notifications

```python
from core.notifications_manager import notifications_manager

async def execute(data: dict) -> dict:
    user_id = data["user_id"]
    
    # After action completes, notify user
    await notifications_manager.create_notification(
        user_id=user_id,
        notification_type="your_plugin_event",  # Must match notifications_config.json
        title="Action Complete",
        message="Your action was completed successfully.",
        metadata={
            "item_id": item_id,
            "action": "created"
        }
    )
    
    return {"success": True}
```

### Notification Types

Register in `config/notifications_config.json`:

```json
{
  "plugins": {
    "your_plugin": {
      "display_name": "Your Plugin",
      "notifications": [
        {
          "id": "your_plugin_event",
          "label": "Plugin Events",
          "description": "Notifications for plugin events",
          "default_enabled": true,
          "channels": ["in_app", "email"]
        }
      ]
    }
  }
}
```

---

## 6. Settings Manager Contract

### Import

```python
from core.settings_manager import settings_manager
```

### Getting Plugin Settings

```python
from core.settings_manager import settings_manager

async def handle_get_settings(user_id: str, data: dict) -> dict:
    settings = await settings_manager.get_plugin_settings(user_id, "your_plugin")
    
    # Apply defaults if empty
    if not settings:
        settings = {
            "enabled": True,
            "notifications_enabled": True,
            "default_view": "list"
        }
    
    return {"settings": settings}
```

### Saving Plugin Settings

```python
async def handle_save_settings(user_id: str, data: dict) -> dict:
    new_settings = data.get("settings", {})
    
    # Validate settings before saving
    if not isinstance(new_settings, dict):
        return {"error": "Invalid settings format"}
    
    await settings_manager.save_plugin_settings(
        user_id,
        "your_plugin",
        new_settings
    )
    
    return {"success": True, "message": "Settings saved"}
```

---

## 7. WebSocket Contract

### Import

```python
from core.websocket_manager import websocket_manager
```

### Sending Real-time Updates

```python
from core.websocket_manager import websocket_manager

async def execute(data: dict) -> dict:
    user_id = data["user_id"]
    
    # After creating/updating data, push to client
    await websocket_manager.send_to_user(user_id, {
        "type": "your_plugin_update",
        "action": "item_created",
        "data": {
            "item_id": item_id,
            "title": title
        }
    })
    
    return {"success": True}
```

### WebSocket Message Format

```python
{
    "type": "{plugin_name}_{event_type}",  # e.g., "task_manager_update"
    "action": "created|updated|deleted",
    "data": { ... }  # Event-specific payload
}
```

---

## 8. Logging Contract

### Import

```python
import logging

logger = logging.getLogger(f"mozaiks_plugins.{plugin_name}")
```

### Usage

```python
import logging

logger = logging.getLogger("mozaiks_plugins.your_plugin")

async def execute(data: dict) -> dict:
    user_id = data["user_id"]
    action = data.get("action", "")
    
    logger.info(f"Executing action '{action}' for user {user_id}")
    
    try:
        result = await do_something()
        logger.debug(f"Action result: {result}")
        return result
    except Exception as e:
        logger.error(f"Error in action '{action}': {e}", exc_info=True)
        return {"error": str(e)}
```

### Log Levels

| Level | Use For |
|-------|---------|
| `logger.debug()` | Detailed debugging info |
| `logger.info()` | Normal operations |
| `logger.warning()` | Unexpected but handled situations |
| `logger.error()` | Errors that should be investigated |

---

## 9. Error Handling Contract

### NEVER Raise Exceptions

```python
# ❌ WRONG - will cause 500 error
async def execute(data: dict) -> dict:
    raise ValueError("Something went wrong")

# ✅ CORRECT - return error dict
async def execute(data: dict) -> dict:
    return {"error": "Something went wrong"}
```

### Error Response Format

```python
# Simple error
return {"error": "Description of what went wrong"}

# Error with code (for frontend handling)
return {
    "error": "Item not found",
    "error_code": "NOT_FOUND"
}

# Error with details
return {
    "error": "Validation failed",
    "error_code": "VALIDATION_ERROR",
    "details": {
        "field": "title",
        "message": "Title is required"
    }
}
```

### Try-Except Pattern

```python
async def execute(data: dict) -> dict:
    try:
        # Your logic here
        return {"success": True, "data": result}
    except ValueError as e:
        logger.warning(f"Validation error: {e}")
        return {"error": str(e), "error_code": "VALIDATION_ERROR"}
    except Exception as e:
        logger.error(f"Unexpected error: {e}", exc_info=True)
        return {"error": "An unexpected error occurred"}
```

---

## 10. Complete Plugin Template

```python
# plugins/your_plugin/logic.py
"""
Your Plugin - Description of what this plugin does.

Actions:
  - list: List all items for the user
  - create: Create a new item
  - get: Get a single item by ID
  - update: Update an existing item
  - delete: Delete an item
  - get_settings: Get plugin settings
  - save_settings: Save plugin settings
"""
import logging
from datetime import datetime
from typing import Any, Dict, List, Optional

from bson import ObjectId
from core.config.database import db
from core.event_bus import event_bus
from core.notifications_manager import notifications_manager
from core.settings_manager import settings_manager
from core.websocket_manager import websocket_manager

logger = logging.getLogger("mozaiks_plugins.your_plugin")

# Collection for this plugin's data
COLLECTION_NAME = "your_plugin_items"


def get_collection():
    """Get the plugin's MongoDB collection."""
    return db[COLLECTION_NAME]


async def execute(data: dict) -> dict:
    """Main entry point - routes to action handlers."""
    action = data.get("action", "")
    user_id = data["user_id"]
    app_id = data["app_id"]
    
    logger.info(f"Action '{action}' for user={user_id} app={app_id}")
    
    try:
        if action == "list":
            return await handle_list(user_id, data)
        elif action == "create":
            return await handle_create(user_id, data)
        elif action == "get":
            return await handle_get(user_id, data)
        elif action == "update":
            return await handle_update(user_id, data)
        elif action == "delete":
            return await handle_delete(user_id, data)
        elif action == "get_settings":
            return await handle_get_settings(user_id, data)
        elif action == "save_settings":
            return await handle_save_settings(user_id, data)
        else:
            return {"error": f"Unknown action: {action}"}
    except Exception as e:
        logger.error(f"Error in action '{action}': {e}", exc_info=True)
        return {"error": "An unexpected error occurred"}


async def handle_list(user_id: str, data: dict) -> dict:
    """List all items for the user."""
    collection = get_collection()
    
    # Optional pagination
    skip = data.get("skip", 0)
    limit = data.get("limit", 50)
    
    items = await collection.find(
        {"user_id": user_id}
    ).skip(skip).limit(limit).to_list(limit)
    
    # Convert ObjectId to string
    for item in items:
        item["_id"] = str(item["_id"])
    
    total = await collection.count_documents({"user_id": user_id})
    
    return {
        "items": items,
        "count": len(items),
        "total": total,
        "skip": skip,
        "limit": limit
    }


async def handle_create(user_id: str, data: dict) -> dict:
    """Create a new item."""
    collection = get_collection()
    
    # Validate required fields
    title = data.get("title", "").strip()
    if not title:
        return {"error": "Title is required", "error_code": "VALIDATION_ERROR"}
    
    # Create document
    doc = {
        "user_id": user_id,
        "title": title,
        "description": data.get("description", ""),
        "created_at": datetime.utcnow().isoformat(),
        "updated_at": datetime.utcnow().isoformat()
    }
    
    result = await collection.insert_one(doc)
    item_id = str(result.inserted_id)
    
    # Publish event
    event_bus.publish("your_plugin:item_created", {
        "user_id": user_id,
        "item_id": item_id
    })
    
    # Send real-time update
    await websocket_manager.send_to_user(user_id, {
        "type": "your_plugin_update",
        "action": "created",
        "data": {"item_id": item_id, "title": title}
    })
    
    return {"success": True, "id": item_id}


async def handle_get(user_id: str, data: dict) -> dict:
    """Get a single item by ID."""
    collection = get_collection()
    
    item_id = data.get("item_id", "")
    if not item_id:
        return {"error": "item_id is required"}
    
    try:
        item = await collection.find_one({
            "_id": ObjectId(item_id),
            "user_id": user_id  # Security: ensure user owns item
        })
    except Exception:
        return {"error": "Invalid item_id format"}
    
    if not item:
        return {"error": "Item not found", "error_code": "NOT_FOUND"}
    
    item["_id"] = str(item["_id"])
    return {"item": item}


async def handle_update(user_id: str, data: dict) -> dict:
    """Update an existing item."""
    collection = get_collection()
    
    item_id = data.get("item_id", "")
    if not item_id:
        return {"error": "item_id is required"}
    
    # Build update document
    update_fields = {}
    if "title" in data:
        update_fields["title"] = data["title"].strip()
    if "description" in data:
        update_fields["description"] = data["description"]
    
    if not update_fields:
        return {"error": "No fields to update"}
    
    update_fields["updated_at"] = datetime.utcnow().isoformat()
    
    try:
        result = await collection.update_one(
            {"_id": ObjectId(item_id), "user_id": user_id},
            {"$set": update_fields}
        )
    except Exception:
        return {"error": "Invalid item_id format"}
    
    if result.matched_count == 0:
        return {"error": "Item not found", "error_code": "NOT_FOUND"}
    
    # Publish event
    event_bus.publish("your_plugin:item_updated", {
        "user_id": user_id,
        "item_id": item_id
    })
    
    return {"success": True, "modified": result.modified_count > 0}


async def handle_delete(user_id: str, data: dict) -> dict:
    """Delete an item."""
    collection = get_collection()
    
    item_id = data.get("item_id", "")
    if not item_id:
        return {"error": "item_id is required"}
    
    try:
        result = await collection.delete_one({
            "_id": ObjectId(item_id),
            "user_id": user_id
        })
    except Exception:
        return {"error": "Invalid item_id format"}
    
    if result.deleted_count == 0:
        return {"error": "Item not found", "error_code": "NOT_FOUND"}
    
    # Publish event
    event_bus.publish("your_plugin:item_deleted", {
        "user_id": user_id,
        "item_id": item_id
    })
    
    return {"success": True}


async def handle_get_settings(user_id: str, data: dict) -> dict:
    """Get plugin settings for the user."""
    settings = await settings_manager.get_plugin_settings(user_id, "your_plugin")
    
    # Apply defaults
    defaults = {
        "enabled": True,
        "notifications_enabled": True,
        "default_view": "list"
    }
    
    return {"settings": {**defaults, **(settings or {})}}


async def handle_save_settings(user_id: str, data: dict) -> dict:
    """Save plugin settings for the user."""
    new_settings = data.get("settings", {})
    
    if not isinstance(new_settings, dict):
        return {"error": "Invalid settings format"}
    
    await settings_manager.save_plugin_settings(user_id, "your_plugin", new_settings)
    
    return {"success": True, "message": "Settings saved"}
```

---

## 11. API Endpoint Reference

### Execute Plugin

```
POST /api/execute/{plugin_name}
Authorization: Bearer {jwt_token}
Content-Type: application/json

{
  "action": "list",
  ... action-specific fields ...
}
```

### Response Format

```json
// Success
{
  "items": [...],
  "count": 10
}

// Error
{
  "error": "Error message",
  "error_code": "ERROR_CODE"
}
```

---

## 12. Security Rules

1. **ALWAYS scope database queries by `user_id`**
2. **NEVER trust client-provided `user_id` or `app_id`** - use injected values
3. **NEVER expose sensitive data** in error messages
4. **ALWAYS validate input** before database operations
5. **NEVER store secrets** in plugin code - use environment variables

---

## 13. Testing Your Plugin

```python
# Test locally
import asyncio
from plugins.your_plugin.logic import execute

async def test():
    result = await execute({
        "action": "list",
        "user_id": "test_user_123",
        "app_id": "test_app",
        "_context": {
            "app_id": "test_app",
            "user_id": "test_user_123",
            "username": "testuser",
            "roles": [],
            "is_superadmin": False
        }
    })
    print(result)

asyncio.run(test())
```

---

## Summary Checklist

- [ ] `logic.py` exists in plugin root or `backend/` folder
- [ ] `execute(data: dict) -> dict` function exists
- [ ] Function is `async`
- [ ] Uses `data["user_id"]` and `data["app_id"]` from injected context
- [ ] Routes actions via `data.get("action", "")`
- [ ] Returns dict (never raises exceptions)
- [ ] Database queries scoped by `user_id`
- [ ] ObjectIds converted to strings in responses
- [ ] Proper logging with `mozaiks_plugins.{plugin_name}` logger
