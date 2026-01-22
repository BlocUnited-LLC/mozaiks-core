# 🗄️ Database

> MozaiksCore uses **MongoDB** for all data persistence. This doc covers collections, patterns, and best practices.

---

## 📊 Database Architecture

```mermaid
graph TB
    subgraph "MozaiksCore Database"
        USERS[(users)]
        SETTINGS[(settings)]
        NOTIF[(notifications)]
        PLUGIN[(plugin_*)]
    end
    
    subgraph "MozaiksAI Database"
        CHAT[(chat_sessions)]
        STATS[(workflow_stats)]
    end
    
    subgraph "MozaiksDB (Enterprise)"
        ENT[(Enterprises)]
        WALLET[(Wallets)]
    end
    
    APP[App Runtime] --> USERS
    APP --> SETTINGS
    APP --> NOTIF
    APP --> PLUGIN
    AI[AI Runtime] --> CHAT
    AI --> STATS
    
    style USERS fill:#4caf50,color:#fff
    style CHAT fill:#2196f3,color:#fff
    style ENT fill:#9c27b0,color:#fff
```

---

## 🔌 Connection Setup

```python
from core.config.database import db
```

**Environment Variables:**
```env
DATABASE_URI=mongodb+srv://user:pass@cluster.mongodb.net/MozaiksCore
```

**Database Selection:**
- `ENV=production` → Uses `client` database
- `ENV=development` → Uses `MozaiksCore` database

---

## 📚 Core Collections

### 👤 `users`

User accounts and profiles.

```javascript
{
    "_id": ObjectId("..."),
    "email": "user@example.com",
    "username": "john_doe",
    "password_hash": "...",           // bcrypt hashed
    "roles": ["user"],                // ["user", "admin", "superadmin"]
    "subscription": {
        "tier": "premium",
        "expires_at": "2026-01-01T00:00:00Z"
    },
    "profile": {
        "display_name": "John Doe",
        "avatar_url": "...",
        "bio": "..."
    },
    "created_at": "2025-01-01T00:00:00Z",
    "updated_at": "2025-06-15T12:30:00Z",
    "last_login": "2025-10-15T08:00:00Z"
}
```

**Indexes:**
```javascript
{ "email": 1 }        // unique
{ "username": 1 }     // unique
{ "roles": 1 }
```

### ⚙️ `settings`

Per-user settings for core and plugins.

```javascript
{
    "_id": ObjectId("..."),
    "user_id": "user_123",
    "plugin_settings": {
        "task_manager": {
            "default_view": "kanban",
            "notifications": true
        },
        "notes": {
            "theme": "dark"
        }
    },
    "notification_preferences": {
        "email_enabled": true,
        "push_enabled": false,
        "digest_frequency": "daily"
    }
}
```

**Indexes:**
```javascript
{ "user_id": 1 }      // unique
```

### 🔔 `notifications`

User notifications (in-app).

```javascript
{
    "_id": ObjectId("..."),
    "user_id": "user_123",
    "type": "task_reminder",
    "title": "Task Due",
    "message": "Your task 'Review PR' is due tomorrow",
    "read": false,
    "metadata": {
        "task_id": "task_456",
        "plugin": "task_manager"
    },
    "created_at": "2025-10-15T10:00:00Z"
}
```

**Indexes:**
```javascript
{ "user_id": 1, "created_at": -1 }
{ "user_id": 1, "read": 1 }
{ "created_at": 1, "expireAfterSeconds": 2592000 }  // TTL: 30 days
```

---

## 🔌 Plugin Collections

Plugins create their own collections. **Naming convention:** `{plugin_name}_*`

### Example: Task Manager

```javascript
// Collection: task_manager_tasks
{
    "_id": ObjectId("..."),
    "user_id": "user_123",           // ⚠️ REQUIRED for security
    "title": "Review PR #42",
    "description": "Check the new feature",
    "status": "in_progress",         // todo, in_progress, done
    "priority": "high",
    "due_date": "2025-10-20T17:00:00Z",
    "tags": ["work", "urgent"],
    "created_at": "2025-10-15T10:00:00Z",
    "updated_at": "2025-10-15T14:30:00Z"
}
```

**Required Index:**
```javascript
{ "user_id": 1 }                     // ⚠️ Every plugin collection needs this
{ "user_id": 1, "status": 1 }
{ "user_id": 1, "due_date": 1 }
```

---

## 📝 Query Patterns

### ✅ Correct: Always Scope by user_id

```python
from core.config.database import db
from bson import ObjectId

collection = db["task_manager_tasks"]

# ✅ List items for user
items = await collection.find({"user_id": user_id}).to_list(100)

# ✅ Get single item (verify ownership)
item = await collection.find_one({
    "_id": ObjectId(item_id),
    "user_id": user_id              # ⚠️ Critical security filter
})

# ✅ Update (verify ownership)
await collection.update_one(
    {"_id": ObjectId(item_id), "user_id": user_id},
    {"$set": {"title": "New Title"}}
)

# ✅ Delete (verify ownership)
await collection.delete_one({
    "_id": ObjectId(item_id),
    "user_id": user_id
})
```

### ❌ Wrong: Missing user_id Filter

```python
# ❌ DANGEROUS: Returns ALL items from ALL users
items = await collection.find({}).to_list(100)

# ❌ DANGEROUS: Anyone can access any item
item = await collection.find_one({"_id": ObjectId(item_id)})

# ❌ DANGEROUS: Anyone can delete any item
await collection.delete_one({"_id": ObjectId(item_id)})
```

---

## 🔄 Common Operations

### Pagination

```python
async def list_with_pagination(user_id: str, skip: int = 0, limit: int = 50):
    collection = db["my_collection"]
    
    # Cap limit to prevent abuse
    limit = min(limit, 100)
    
    items = await collection.find(
        {"user_id": user_id}
    ).sort("created_at", -1).skip(skip).limit(limit).to_list(limit)
    
    # Convert ObjectIds
    for item in items:
        item["_id"] = str(item["_id"])
    
    # Get total count
    total = await collection.count_documents({"user_id": user_id})
    
    return {
        "items": items,
        "count": len(items),
        "total": total,
        "skip": skip,
        "limit": limit,
        "has_more": skip + len(items) < total
    }
```

### Upsert

```python
async def save_settings(user_id: str, settings: dict):
    collection = db["settings"]
    
    await collection.update_one(
        {"user_id": user_id},
        {"$set": {"plugin_settings.my_plugin": settings}},
        upsert=True
    )
```

### Aggregation

```python
async def get_task_stats(user_id: str):
    collection = db["task_manager_tasks"]
    
    pipeline = [
        {"$match": {"user_id": user_id}},
        {"$group": {
            "_id": "$status",
            "count": {"$sum": 1}
        }}
    ]
    
    results = await collection.aggregate(pipeline).to_list(10)
    return {r["_id"]: r["count"] for r in results}
```

### Bulk Operations

```python
from pymongo import UpdateOne

async def bulk_update_status(user_id: str, item_ids: list, new_status: str):
    collection = db["task_manager_tasks"]
    
    operations = [
        UpdateOne(
            {"_id": ObjectId(id), "user_id": user_id},
            {"$set": {"status": new_status}}
        )
        for id in item_ids
    ]
    
    result = await collection.bulk_write(operations)
    return {"modified": result.modified_count}
```

---

## 🏎️ Performance Tips

### 1. Create Indexes

```python
# Run once at startup or via migration
async def ensure_indexes():
    collection = db["my_collection"]
    
    await collection.create_index("user_id")
    await collection.create_index([("user_id", 1), ("created_at", -1)])
```

### 2. Project Only Needed Fields

```python
# ✅ Only fetch needed fields
items = await collection.find(
    {"user_id": user_id},
    {"_id": 1, "title": 1, "status": 1}  # Projection
).to_list(100)

# ❌ Fetches entire documents
items = await collection.find({"user_id": user_id}).to_list(100)
```

### 3. Use Covered Queries

```python
# If you have index on {user_id: 1, status: 1}
# This query is "covered" (reads from index only)
count = await collection.count_documents({
    "user_id": user_id,
    "status": "done"
})
```

### 4. Batch Operations

```python
# ✅ Single round-trip
await collection.insert_many([doc1, doc2, doc3])

# ❌ Multiple round-trips
await collection.insert_one(doc1)
await collection.insert_one(doc2)
await collection.insert_one(doc3)
```

---

## 🔒 Security Checklist

- [ ] Every query includes `user_id` filter
- [ ] ObjectIds validated before use
- [ ] Input sanitized (no injection)
- [ ] Limits enforced on pagination
- [ ] No sensitive data in logs

---

## 🔗 Related

- 🔌 [Plugin System](./plugins.md)
- � [Authentication](./authentication.md)
- 📡 [WebSockets](./websockets.md)
