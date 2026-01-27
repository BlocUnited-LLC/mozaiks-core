# 🔧 Troubleshooting Guide

> Common issues and solutions for MozaiksCore.

---

## 🔍 Quick Diagnosis

```mermaid
graph TD
    START[Issue] --> Q1{Where?}
    Q1 -->|Backend| B1[Check logs]
    Q1 -->|Frontend| F1[Check console]
    Q1 -->|Database| D1[Check connection]
    
    B1 --> B2{Error type?}
    B2 -->|Auth| AUTH[Auth Issues]
    B2 -->|Plugin| PLUG[Plugin Issues]
    B2 -->|Database| DB[Database Issues]
    
    F1 --> F2{Error type?}
    F2 -->|Network| NET[Network Issues]
    F2 -->|Render| RENDER[UI Issues]
    
    style START fill:#f44336,color:#fff
```

---

## 🔐 Authentication Issues

### "Token expired" or "Unauthorized"

**Symptoms:**
- 401 errors on API calls
- Redirect to login loop

**Solutions:**

1. **Check JWT secret matches**
```bash
# Backend .env
JWT_SECRET=your-secret-key

# Must be same in all services
```

2. **Check token expiration**
```python
# In auth middleware, verify exp claim
import jwt
decoded = jwt.decode(token, options={"verify_exp": True})
```

3. **Clear browser storage**
```javascript
localStorage.removeItem('token');
sessionStorage.clear();
```

### "OIDC configuration error"

**Symptoms:**
- Login fails with OIDC error
- "Unable to fetch JWKS"

**Solutions:**

1. **Verify OIDC authority URL**
```env
MOZAIKS_OIDC_AUTHORITY=https://keycloak.example.com/realms/mozaiks
```

2. **Check Keycloak realm settings**
- Realm is enabled
- Client is public or confidential
- Redirect URIs configured

3. **Test OIDC endpoint**
```bash
curl https://keycloak.example.com/realms/mozaiks/.well-known/openid-configuration
```

---

## 🔌 Plugin Issues

### Plugin not loading

**Symptoms:**
- Plugin doesn't appear in navigation
- 404 on plugin route

**Checklist:**

1. **Verify registry entry**
```json
// runtime/ai/core/config/plugin_registry.json
{
  "name": "my_plugin",
  "enabled": true,
  "backend": "plugins.my_plugin.logic"
}
```

2. **Check backend module exists**
```
runtime/ai/plugins/
└── my_plugin/
    ├── __init__.py   ← Must exist (can be empty)
    └── logic.py      ← Must have execute() function
```

3. **Verify execute function signature**
```python
# runtime/ai/plugins/my_plugin/logic.py
async def execute(data: dict) -> dict:  # ← Must be async
    action = data.get("action")
    # ...
```

4. **Check navigation config**
```json
// runtime/ai/core/config/navigation_config.json
{
  "plugin_name": "my_plugin",
  "path": "/plugins/my_plugin"
}
```

### "Unknown action" error

**Symptoms:**
- Plugin loads but actions fail
- Returns `{"error": "Unknown action"}`

**Solution:**
```python
# Verify action routing
async def execute(data: dict) -> dict:
    action = data.get("action", "")  # ← Check this value
    
    print(f"Received action: {action}")  # Debug
    
    if action == "list":  # ← Exact match
        return await handle_list(data)
```

### Plugin permission denied

**Symptoms:**
- Plugin works for some users
- Others get "Access denied"

**Solution:**
```json
// backend/core/config/subscription_config.json
{
  "name": "free",
  "plugins_unlocked": ["my_plugin"]  // ← Add plugin here
}
```

---

## 🗄️ Database Issues

### "Connection refused"

**Symptoms:**
- Backend fails to start
- "MongoServerError: connection refused"

**Solutions:**

1. **Check MongoDB is running**
```bash
# Docker
docker ps | grep mongo

# Local
mongosh --eval "db.adminCommand('ping')"
```

2. **Verify connection string**
```env
# Local
MONGODB_URI=mongodb://localhost:27017/mozaikscore

# Atlas
MONGODB_URI=mongodb+srv://user:pass@cluster.mongodb.net/mozaikscore
```

3. **Check network/firewall**
```bash
# Test connection
telnet localhost 27017
```

### "ObjectId is not JSON serializable"

**Symptoms:**
- API returns 500 error
- Serialization error in logs

**Solution:**
```python
# Always convert ObjectId to string
items = await collection.find({}).to_list(100)
for item in items:
    item["_id"] = str(item["_id"])  # ← Required

return {"items": items}
```

### Slow queries

**Symptoms:**
- API calls timeout
- High database CPU

**Solutions:**

1. **Add indexes**
```python
# In plugin initialization
await collection.create_index("user_id")
await collection.create_index([("user_id", 1), ("created_at", -1)])
```

2. **Limit results**
```python
# Always use limit
items = await collection.find(query).limit(100).to_list(100)
```

3. **Check query with explain**
```python
explanation = await collection.find(query).explain()
print(explanation["executionStats"])
```

---

## 🌐 Network Issues

### CORS errors

**Symptoms:**
- "Access-Control-Allow-Origin" error
- Preflight request fails

**Solutions:**

1. **Configure backend CORS**
```python
# main.py
from fastapi.middleware.cors import CORSMiddleware

app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:5173"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)
```

2. **For production**
```env
CORS_ORIGINS=https://app.yourdomain.com,https://admin.yourdomain.com
```

### WebSocket connection fails

**Symptoms:**
- "WebSocket connection failed"
- Notifications don't arrive

**Solutions:**

1. **Check WebSocket URL**
```javascript
// Frontend should use correct protocol
const wsUrl = window.location.protocol === 'https:' 
  ? 'wss://...' 
  : 'ws://...';
```

2. **Verify proxy configuration**
```nginx
# nginx.conf
location /ws/ {
    proxy_pass http://backend:8080;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
}
```

---

## 🤖 AI Runtime Issues

### "Model not found"

**Symptoms:**
- Chat fails to start
- "Invalid model" error

**Solution:**
```env
# Verify API key
OPENAI_API_KEY=sk-...

# Check model name
# In workflow config
model="gpt-4o-mini"  # Valid model name
```

### Tool execution fails

**Symptoms:**
- Agent mentions tool but it fails
- "Tool not found" in logs

**Checklist:**

1. **Tool is registered**
```python
# Check tools/__init__.py
from .my_tool import MyTool
TOOLS = [MyTool, ...]
```

2. **Tool name matches**
```python
class MyTool(BaseTool):
    name = "my_tool"  # ← Must match workflow config
```

3. **Context is passed**
```python
@tool_function
async def execute(self, arg1: str, context: dict = None):
    user_id = context.get("user_id")  # ← Available if passed
```

### Streaming not working

**Symptoms:**
- Response appears all at once
- No typing indicator

**Solution:**
```javascript
// Frontend WebSocket handler
ws.onmessage = (event) => {
  const data = JSON.parse(event.data);
  if (data.type === 'token') {
    // Append token to display
    appendToken(data.content);
  } else if (data.type === 'complete') {
    // Message finished
    finishMessage(data.content);
  }
};
```

---

## 📋 Common Error Reference

| Error | Cause | Solution |
|-------|-------|----------|
| `401 Unauthorized` | Invalid/expired token | Re-authenticate |
| `403 Forbidden` | Insufficient permissions | Check subscription tier |
| `404 Not Found` | Plugin/endpoint missing | Check registry |
| `422 Validation Error` | Invalid request data | Check request body |
| `500 Internal Error` | Backend exception | Check logs |
| `502 Bad Gateway` | Backend not running | Start backend service |
| `503 Service Unavailable` | Overloaded | Scale or rate limit |

---

## 🔍 Debug Mode

### Enable verbose logging

```python
# main.py
import logging
logging.basicConfig(level=logging.DEBUG)
```

### Frontend debugging

```javascript
// Enable debug mode
localStorage.setItem('DEBUG', 'true');

// View state
console.log(window.__MOZAIKS_STATE__);
```

### Database query logging

```python
# Enable MongoDB command logging
import logging
logging.getLogger('pymongo').setLevel(logging.DEBUG)
```

---

## 🆘 Getting Help

1. **Check logs first**
```bash
# Backend
uvicorn main:app --log-level debug

# Docker
docker-compose logs -f backend
```

2. **Search existing issues**
- GitHub Issues

3. **Provide context when reporting**
- Error message
- Steps to reproduce
- Environment (OS, versions)
- Relevant logs

---

## 🔗 Related

- 🚀 [Quickstart](./quickstart.md)
- 🚢 [Deployment](./deployment.md)
- 📖 [Architecture](../core/architecture.md)
