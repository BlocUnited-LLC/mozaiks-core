# MozaiksCore Runtime API Contract

> **Version**: 1.0.0  
> **Last Updated**: 2026-01-12  
> **Status**: Authoritative Contract for MozaiksAI Integration  
> **Audience**: MozaiksAI runtime, generated apps, integration partners

---

## 1. System Identity

**MozaiksCore is a per-app runtime container:**

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        ONE DEPLOYMENT = ONE APP                              │
├─────────────────────────────────────────────────────────────────────────────┤
│  • One app_id (from MOZAIKS_APP_ID env)                                     │
│  • One identity namespace (user records scoped to this app)                 │
│  • One user directory (MongoDB instance/database)                           │
│  • One plugin set (loaded from /backend/plugins/)                           │
│  • One subscription config (feature flags from Control Plane)               │
└─────────────────────────────────────────────────────────────────────────────┘
```

**MozaiksCore is NOT:**
- A multi-tenant platform (that's the Control Plane)
- A billing system (that's MozaiksPay)
- A workflow executor (that's MozaiksAI)
- A platform admin interface (that's the Control Plane UI)

---

## 2. Authentication Contract

### 2.1 Who Authenticates Here

| Identity | Allowed | Auth Method |
|----------|---------|-------------|
| **App end-users** | ✅ YES | External OIDC (Keycloak, WorkOS, Auth0, Okta) |
| Platform founders | ❌ NEVER | Control Plane only |
| Platform admins | ❌ NEVER | Control Plane only |
| Service accounts | ⚠️ Limited | `MOZAIKS_APP_ADMIN_KEY` for admin endpoints |

### 2.2 Supported Identity Providers

MozaiksCore validates JWTs from enterprise IdPs:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    EXTERNAL OIDC PROVIDERS (SUPPORTED)                       │
├─────────────────────────────────────────────────────────────────────────────┤
│  • Keycloak (self-hosted or cloud)                                          │
│  • WorkOS (enterprise SSO aggregator)                                       │
│  • Auth0 / Okta / Azure AD B2C                                              │
│  • Any OIDC-compliant IdP with JWKS endpoint                                │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 2.3 JWT Validation Requirements

```yaml
# Required environment variables for external/platform auth
MOZAIKS_AUTH_MODE: external  # or 'platform' for hosted
MOZAIKS_JWKS_URL: https://idp.example.com/.well-known/jwks.json
MOZAIKS_ISSUER: https://idp.example.com/
MOZAIKS_AUDIENCE: your-api-audience
MOZAIKS_JWT_ALGORITHMS: RS256  # comma-separated if multiple

# Claim mapping
MOZAIKS_USER_ID_CLAIM: sub     # maps to identity_user_id
MOZAIKS_EMAIL_CLAIM: email
MOZAIKS_ROLES_CLAIM: roles     # or realm_access.roles for Keycloak
MOZAIKS_SUPERADMIN_ROLE: SuperAdmin
```

### 2.4 Request Scoping Invariants

**EVERY authenticated request MUST be scoped by:**

```python
# Enforced by MozaiksCore on every request
{
    "app_id": os.getenv("MOZAIKS_APP_ID"),  # From environment
    "user_id": jwt_payload["sub"],           # From validated JWT (server-derived)
}
```

**Security guarantees:**
- `app_id` is injected by the runtime, never from client
- `user_id` is extracted from validated JWT `sub` claim
- Client cannot override either value
- All database queries MUST include `user_id` filter

---

## 3. App Runtime API

### 3.1 API Surface Overview

| Category | Endpoints | Auth Required |
|----------|-----------|---------------|
| **Plugin Execution** | `/api/execute/{plugin}` | ✅ User JWT |
| **Plugin Discovery** | `/api/available-plugins` | ✅ User JWT |
| **AI Session** | `/api/ai/capabilities`, `/api/ai/launch` | ✅ User JWT |
| **User Profile** | `/api/user-profile`, `/api/update-profile` | ✅ User JWT |
| **Navigation** | `/api/navigation` | ✅ User JWT |
| **Notifications** | `/api/notifications/*` | ✅ User JWT |
| **WebSocket** | `/ws/notifications/{user_id}` | ✅ JWT in protocol |
| **Config (public)** | `/api/app-config`, `/api/theme-config` | ❌ Public |
| **Admin** | `/__mozaiks/admin/*` | ✅ API Key |

### 3.2 Plugin Execution API

**Endpoint:** `POST /api/execute/{plugin_name}`

```typescript
// Request
{
  "action": string,      // Plugin-defined action name
  "...": any             // Action-specific payload
}

// Response (success)
{
  "...": any             // Plugin-defined response
}

// Response (error)
{
  "error": string,
  "detail"?: string
}
```

**Server-side injection:**
```python
# Director injects before calling plugin.execute()
data["user_id"] = current_user["user_id"]  # From JWT
data["app_id"] = os.getenv("MOZAIKS_APP_ID")  # From env
```

**Authorization flow:**
1. Validate JWT → extract `user_id`
2. Check subscription tier → plugin in `plugins_unlocked`?
3. Inject `user_id` + `app_id` into request data
4. Call `plugin.execute(data)`
5. Return result (plugin MUST scope queries by `user_id`)

### 3.3 Plugin Discovery API

**Endpoint:** `GET /api/available-plugins`

```typescript
// Response
{
  "plugins": [
    {
      "name": string,
      "display_name": string,
      "description": string,
      "version": string,
      "enabled": boolean
    }
  ]
}
```

**Authorization:** Only returns plugins the user's subscription tier unlocks.

### 3.4 AI Session API

#### List Capabilities

**Endpoint:** `GET /api/ai/capabilities`

```typescript
// Response
{
  "capabilities": [
    {
      "id": string,
      "display_name": string,
      "description": string,
      "icon": string,
      "visibility": "user" | "admin",
      "enabled": boolean,
      "allowed": boolean  // Based on user's plan
    }
  ],
  "plan": string  // User's current subscription plan
}
```

#### Launch AI Session

**Endpoint:** `POST /api/ai/launch`

```typescript
// Request
{
  "capability_id": string
}

// Response
{
  "app_id": string,
  "capability_id": string,
  "chat_id": string,           // Unique session ID
  "launch_token": string,      // Signed JWT for MozaiksAI
  "expires_in": number,        // Token TTL in seconds
  "runtime": {
    "runtime_api_base_url": string | null,
    "runtime_ui_base_url": string | null,
    "chatui_url_template": string
  }
}
```

**Launch token claims (for MozaiksAI validation):**
```json
{
  "iss": "mozaikscore",
  "aud": "<MOZAIKS_EXECUTION_TOKEN_AUDIENCE>",
  "exp": "<10 minutes from now>",
  "mozaiks_token_use": "execution",
  "sub": "<identity_user_id from OIDC>",
  "app_id": "<MOZAIKS_APP_ID>",
  "user_id": "<local user_id>",
  "chat_id": "<generated chat_id>",
  "capability_id": "<requested capability>",
  "workflow_id": "<mapped workflow from config>",
  "plan": "<user's subscription plan>",
  "roles": ["<user roles from JWT>"],
  "is_superadmin": false
}
```

---

## 4. MozaiksAI Integration Contract

### 4.1 Token Validation Requirements

MozaiksAI MUST validate execution tokens:

```python
# MozaiksAI validation pseudocode
def validate_execution_token(token: str) -> ExecutionContext:
    # 1. Verify signature
    payload = jwt.decode(
        token,
        key=get_signing_key(),  # Same key as MOZAIKS_EXECUTION_TOKEN_SECRET
        algorithms=["HS256"],   # Or RS256 if asymmetric
        issuer="mozaikscore",
        audience=EXPECTED_AUDIENCE,
    )
    
    # 2. Verify token type
    assert payload["mozaiks_token_use"] == "execution"
    
    # 3. Extract execution context
    return ExecutionContext(
        app_id=payload["app_id"],
        user_id=payload["user_id"],
        chat_id=payload["chat_id"],
        workflow_id=payload["workflow_id"],
        plan=payload["plan"],
        roles=payload["roles"],
    )
```

### 4.2 Execution Context (Source of Truth)

MozaiksAI uses the execution token claims as the **sole source of truth** for:

| Claim | Usage |
|-------|-------|
| `app_id` | Scope all operations to this app |
| `user_id` | Scope all user data to this user |
| `chat_id` | Unique conversation session |
| `workflow_id` | Which workflow to execute |
| `plan` | Feature gating within workflow |
| `roles` | Role-based access within workflow |

**Security invariants:**
- MozaiksAI MUST NOT accept these values from query params
- MozaiksAI MUST NOT trust client-supplied user_id
- All values come from the signed token

### 4.3 Communication Flow

```
┌──────────────┐     1. /api/ai/launch     ┌──────────────┐
│   Frontend   │ ─────────────────────────▶│  MozaiksCore │
│   (Browser)  │                           │   Runtime    │
└──────────────┘                           └──────────────┘
       │                                          │
       │                              2. Validate user JWT
       │                              3. Check capability access
       │                              4. Map capability → workflow
       │                              5. Mint execution token
       │                                          │
       │◀──────────────────────────────────────────
       │     { launch_token, chat_id, runtime_urls }
       │
       │     6. Open ChatUI with token
       ▼
┌──────────────┐     7. Validate token     ┌──────────────┐
│   ChatUI     │ ─────────────────────────▶│   MozaiksAI  │
│  (iframe/    │                           │   Runtime    │
│   new tab)   │◀───────────────────────────│              │
└──────────────┘     8. Stream responses   └──────────────┘
```

### 4.4 API Calls from MozaiksAI to MozaiksCore

MozaiksAI MAY call back to MozaiksCore for:

| Action | Endpoint | Auth |
|--------|----------|------|
| Execute plugin | `POST /api/execute/{plugin}` | User JWT or execution token |
| Get user profile | `GET /api/user-profile` | User JWT |
| Create notification | Internal event bus | Service token |

**Token forwarding:**
- MozaiksAI can forward the user's original OIDC token
- OR use the execution token if MozaiksCore is configured to accept it

---

## 5. Subscription Enforcement

### 5.1 Read-Only Model

MozaiksCore enforces subscriptions but **NEVER modifies them**:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    SUBSCRIPTION FLOW (READ-ONLY)                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  Control Plane                MozaiksCore                   Plugin           │
│  (MozaiksPay)                 (This Runtime)                                │
│       │                            │                          │             │
│       │  1. Push subscription      │                          │             │
│       │     state to DB            │                          │             │
│       │ ─────────────────────────▶ │                          │             │
│       │                            │                          │             │
│       │                            │  2. User requests        │             │
│       │                            │◀──────────────────────────│             │
│       │                            │                          │             │
│       │                            │  3. Check subscription   │             │
│       │                            │     (read from DB)       │             │
│       │                            │                          │             │
│       │                            │  4. Allow/deny based     │             │
│       │                            │     on plugins_unlocked  │             │
│       │                            │ ─────────────────────────▶│             │
│       │                            │                          │             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 5.2 What MozaiksCore Does

- ✅ Read subscription state from `subscriptions` collection
- ✅ Check if plugin is in `plugins_unlocked` for user's plan
- ✅ Return 403 if access denied
- ✅ Include `plan` in execution tokens

### 5.3 What MozaiksCore Does NOT Do

- ❌ Create subscriptions
- ❌ Process payments
- ❌ Modify subscription state
- ❌ Communicate with Stripe/PayPal
- ❌ Decide pricing or plans

### 5.4 Feature Flag Structure

```json
// Subscription document (written by Control Plane)
{
  "user_id": "user_123",
  "plan": "premium",
  "status": "active",
  "plugins_unlocked": ["*"],  // or ["notes_manager", "task_manager"]
  "features": {
    "ai_enabled": true,
    "max_workflows": 10,
    "priority_support": true
  },
  "updated_at": "2026-01-12T00:00:00Z",
  "managed_by": "control_plane"  // Indicates external management
}
```

---

## 6. WebSocket Contract

### 6.1 Authentication

WebSocket connections MUST be authenticated via JWT in `Sec-WebSocket-Protocol`:

```javascript
// Client-side (correct)
const protocols = ['mozaiks', jwtToken];
const ws = new WebSocket(url, protocols);

// Server-side validation
const token = extract_jwt_from_protocol(websocket);
const user = await validate_jwt(token);
```

**Security requirements:**
- ❌ NEVER accept tokens in URL query params (logged in proxies)
- ❌ NEVER trust path-based user_id without JWT validation
- ✅ MUST validate JWT signature and claims
- ✅ MUST reject if token user_id ≠ path user_id

### 6.2 Message Types

```typescript
// Server → Client
{
  "type": "notification" | "plugin_event" | "system",
  "payload": {
    "id": string,
    "title": string,
    "message": string,
    "metadata"?: object
  },
  "timestamp": string
}
```

### 6.3 Scope Boundary

MozaiksCore WebSockets are for:
- ✅ In-app notifications
- ✅ Plugin UI events
- ✅ System status updates

MozaiksCore WebSockets are NOT for:
- ❌ Chat streaming (MozaiksAI owns this)
- ❌ Workflow execution updates (MozaiksAI owns this)
- ❌ Cross-app communication

---

## 7. Admin API Contract

### 7.1 Authentication

Admin endpoints use API key authentication:

```http
POST /__mozaiks/admin/notifications/broadcast
X-Mozaiks-App-Admin-Key: <MOZAIKS_APP_ADMIN_KEY>
Content-Type: application/json
```

### 7.2 Admin Endpoints

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/__mozaiks/admin/notifications/broadcast` | POST | Broadcast notification |
| `/__mozaiks/admin/notifications/channels` | GET | Channel status |
| `/__mozaiks/admin/users` | GET | List users (paginated) |
| `/__mozaiks/admin/users/{user_id}` | GET | Get user details |
| `/__mozaiks/admin/users/{user_id}/disable` | POST | Disable user |

### 7.3 Security

- API key is compared using constant-time comparison
- Admin endpoints are NOT exposed via public routes
- Audit logging for all admin actions

---

## 8. Error Responses

### 8.1 Standard Error Format

```typescript
{
  "detail": string,          // Human-readable message
  "error_code"?: string,     // Machine-readable code
  "status_code": number      // HTTP status code
}
```

### 8.2 Error Codes

| Code | HTTP | Meaning |
|------|------|---------|
| `AUTH_REQUIRED` | 401 | No valid JWT provided |
| `AUTH_EXPIRED` | 401 | JWT has expired |
| `AUTH_INVALID` | 401 | JWT signature invalid |
| `ACCESS_DENIED` | 403 | User lacks permission |
| `SUBSCRIPTION_REQUIRED` | 403 | Plugin not in user's plan |
| `PLUGIN_NOT_FOUND` | 404 | Plugin doesn't exist |
| `PLUGIN_DISABLED` | 404 | Plugin is disabled |
| `CAPABILITY_NOT_FOUND` | 404 | AI capability doesn't exist |
| `INTERNAL_ERROR` | 500 | Server error |

---

## 9. Environment Variables Reference

### 9.1 Required for Production

```bash
# App Identity
MOZAIKS_APP_ID=app_abc123              # Unique app identifier

# Database
DATABASE_URI=mongodb://...              # MongoDB connection

# Authentication (external/platform mode)
MOZAIKS_AUTH_MODE=external
MOZAIKS_JWKS_URL=https://.../.well-known/jwks.json
MOZAIKS_ISSUER=https://...
MOZAIKS_AUDIENCE=your-api
MOZAIKS_JWT_ALGORITHMS=RS256

# Claim Mapping
MOZAIKS_USER_ID_CLAIM=sub
MOZAIKS_EMAIL_CLAIM=email
MOZAIKS_ROLES_CLAIM=roles
MOZAIKS_SUPERADMIN_ROLE=SuperAdmin

# Token Signing (for execution tokens)
JWT_SECRET=<strong-random-secret>
```

### 9.2 Optional Integrations

```bash
# MozaiksAI Runtime
RUNTIME_BASE_URL=https://ai.mozaiks.io
MOZAIKS_EXECUTION_TOKEN_AUDIENCE=mozaiksai

# Telemetry
INSIGHTS_PUSH_ENABLED=1
INSIGHTS_API_BASE_URL=https://insights.mozaiks.io

# Admin
MOZAIKS_APP_ADMIN_KEY=<admin-api-key>
```

---

## 10. Versioning & Compatibility

### 10.1 API Versioning

Current version: **v1** (implicit in all endpoints)

Future versions will use path prefix: `/api/v2/...`

### 10.2 Breaking Changes Policy

- Execution token claims: additive only, never remove
- Plugin API: `execute(data)` signature is stable
- WebSocket messages: `type` field is stable

### 10.3 Deprecation Process

1. Announce deprecation in release notes
2. Add `X-Deprecated` header to deprecated endpoints
3. Maintain deprecated endpoints for 6 months
4. Remove in next major version

---

## Appendix A: Security Checklist

Before deploying MozaiksCore:

- [ ] `MOZAIKS_APP_ID` is set and unique
- [ ] `JWT_SECRET` is strong (≥32 chars, mixed case + digits)
- [ ] `MOZAIKS_AUTH_MODE` is `external` or `platform` (not `local`)
- [ ] `MOZAIKS_JWKS_URL` points to valid JWKS endpoint
- [ ] `MOZAIKS_ISSUER` matches IdP issuer exactly
- [ ] `MOZAIKS_AUDIENCE` is configured in IdP
- [ ] MongoDB connection uses authentication
- [ ] HTTPS is enforced in production
- [ ] `MOZAIKS_APP_ADMIN_KEY` is strong and secret

---

## Appendix B: Plugin Contract

Plugins MUST:

```python
# /backend/plugins/{name}/logic.py

async def execute(data: dict) -> dict:
    """
    REQUIRED: Main entry point
    
    data["user_id"]  - Always present (from JWT)
    data["app_id"]   - Always present (from env)
    data["action"]   - Client-specified action
    
    MUST: Scope all DB queries by user_id
    MUST: Return dict (success) or {"error": "message"}
    MUST NOT: Extract identity from request headers
    MUST NOT: Access other users' data
    """
    user_id = data["user_id"]  # Trust this, it's server-injected
    action = data.get("action")
    
    # All queries MUST include user_id
    items = await db["items"].find({"user_id": user_id}).to_list(100)
    
    return {"items": items}
```

---

## Appendix C: Generated App Integration

Apps generated by MozaiksAI should:

1. **Use the plugin API** for all business logic
2. **Call `/api/ai/launch`** for AI features
3. **Trust server-injected identity** (never read from client)
4. **Handle 403 responses** gracefully (show upgrade prompt)
5. **Use WebSocket** for real-time notifications only
