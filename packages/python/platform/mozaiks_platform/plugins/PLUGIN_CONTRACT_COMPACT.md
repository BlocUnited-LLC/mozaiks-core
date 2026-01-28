# MozaiksCore Plugin Contract (Compact)

## Structure
```
plugins/{plugin_name}/
├── __init__.py        # Required (empty)
└── logic.py           # Required: execute() function
```

## Execute Function (REQUIRED)

```python
async def execute(data: dict) -> dict:
    # INJECTED BY RUNTIME (always present, never trust client):
    user_id = data["user_id"]      # str
    app_id = data["app_id"]        # str
    # context = data["_context"]   # {app_id, user_id, username, roles, is_superadmin}
    
    # CLIENT PROVIDED:
    action = data.get("action", "")
    
    if action == "list":
        return await handle_list(user_id, data)
    elif action == "create":
        return await handle_create(user_id, data)
    # ... more actions
    return {"error": f"Unknown action: {action}"}
```

## Core Imports

```python
from core.config.database import db                        # MongoDB
from core.event_bus import event_bus                       # Events
from core.notifications_manager import notifications_manager
from core.settings_manager import settings_manager
from core.websocket_manager import websocket_manager
from bson import ObjectId
import logging
logger = logging.getLogger("mozaiks_plugins.{plugin_name}")
```

## Database Pattern

```python
collection = db["plugin_items"]

# READ (always scope by user_id!)
items = await collection.find({"user_id": user_id}).to_list(100)
item = await collection.find_one({"_id": ObjectId(id), "user_id": user_id})

# CREATE
result = await collection.insert_one({"user_id": user_id, ...})
new_id = str(result.inserted_id)

# UPDATE
await collection.update_one(
    {"_id": ObjectId(id), "user_id": user_id},
    {"$set": {"field": "value"}}
)

# DELETE
await collection.delete_one({"_id": ObjectId(id), "user_id": user_id})

# ALWAYS convert ObjectId for JSON response
item["_id"] = str(item["_id"])
```

## Event Bus

```python
event_bus.publish("{plugin}:{action}", {"user_id": user_id, "item_id": id})
```

## WebSocket (real-time)

```python
await websocket_manager.send_to_user(user_id, {
    "type": "{plugin}_update",
    "action": "created",
    "data": {...}
})
```

## Settings

```python
# Get
settings = await settings_manager.get_plugin_settings(user_id, "plugin_name")

# Save
await settings_manager.save_plugin_settings(user_id, "plugin_name", {...})
```

## Notifications

```python
await notifications_manager.create_notification(
    user_id=user_id,
    notification_type="plugin_event",
    title="Title",
    message="Message",
    metadata={"key": "value"}
)
```

## Rules

1. **ASYNC**: `execute` must be `async def`
2. **RETURN DICT**: Never raise exceptions → `return {"error": "msg"}`
3. **SCOPE BY USER**: All DB queries MUST include `user_id`
4. **STRINGIFY IDS**: `item["_id"] = str(item["_id"])`
5. **VALIDATE INPUT**: Check required fields before DB ops

## Minimal Template

```python
# plugins/{name}/logic.py
import logging
from datetime import datetime
from bson import ObjectId
from core.config.database import db

logger = logging.getLogger("mozaiks_plugins.{name}")
COLLECTION = "{name}_items"

async def execute(data: dict) -> dict:
    action = data.get("action", "")
    user_id = data["user_id"]
    
    if action == "list":
        items = await db[COLLECTION].find({"user_id": user_id}).to_list(100)
        for i in items: i["_id"] = str(i["_id"])
        return {"items": items, "count": len(items)}
    
    elif action == "create":
        title = data.get("title", "").strip()
        if not title: return {"error": "title required"}
        result = await db[COLLECTION].insert_one({
            "user_id": user_id,
            "title": title,
            "created_at": datetime.utcnow().isoformat()
        })
        return {"success": True, "id": str(result.inserted_id)}
    
    elif action == "get":
        item_id = data.get("item_id")
        if not item_id: return {"error": "item_id required"}
        item = await db[COLLECTION].find_one({"_id": ObjectId(item_id), "user_id": user_id})
        if not item: return {"error": "not found"}
        item["_id"] = str(item["_id"])
        return {"item": item}
    
    elif action == "delete":
        item_id = data.get("item_id")
        await db[COLLECTION].delete_one({"_id": ObjectId(item_id), "user_id": user_id})
        return {"success": True}
    
    return {"error": f"Unknown action: {action}"}
```
