# MozaiksCore Boundary Contract

> **Last Updated**: 2026-01-12  
> **Status**: Authoritative Source of Truth  
> **Audience**: Platform engineers, plugin developers, integration partners

---

## 1. System Identity

**MozaiksCore is:**
- A **multi-tenant application runtime shell**
- A **plugin host** for app-specific business logic
- An **AI session broker** for MozaiksAI workflows
- An **end-user identity boundary** for app users only

**MozaiksCore is NOT:**
- A SaaS application with its own business logic
- A platform control plane
- A billing/payment system
- A hosting orchestrator

---

## 2. Identity Boundaries

### 2.1 WHO Authenticates Here

| Identity Type | Allowed | Notes |
|---------------|---------|-------|
| **App end-users** | ✅ YES | The only identity type MozaiksCore handles |
| Platform founders | ❌ NO | Authenticate via Mozaiks Control Plane (.NET) |
| Platform admins | ❌ NO | Authenticate via Mozaiks Control Plane (.NET) |
| Service accounts | ⚠️ Limited | Only for admin endpoints with `MOZAIKS_APP_ADMIN_KEY` |

### 2.2 Authentication Modes

MozaiksCore supports three mutually exclusive auth modes for **app users only**:

| Mode | Use Case | Identity Source |
|------|----------|-----------------|
| `external` (DEFAULT) | Self-hosted apps with customer's IdP | OIDC (Keycloak, Auth0, Okta, etc.) |
| `platform` | Mozaiks-hosted apps | Mozaiks CIAM (settings injected) |
| `local` | Development/air-gapped only | Username/password (HS256 JWT) |

**Security Invariants:**
- Platform/external modes derive identity **exclusively** from JWT `sub` claim (server-side)
- Platform/external modes **NEVER** use local identity code paths
- Local mode is **explicitly isolated** and requires opt-in
- User provisioning auto-creates app-local user records from external identity

### 2.3 Request Scoping

Every authenticated request MUST be scoped by:

```
┌─────────────────────────────────────────────────┐
│  Request Scope (Mandatory)                      │
├─────────────────────────────────────────────────┤
│  app_id   → From MOZAIKS_APP_ID env var         │
│  user_id  → From JWT sub claim (server-derived) │
└─────────────────────────────────────────────────┘
```

**Implementation:**
- `app_id` is injected into execution tokens and telemetry
- `user_id` is extracted from validated JWT and injected into plugin `data` dict
- Plugins receive `data["user_id"]` — they do NOT extract identity themselves

---

## 3. Ownership Matrix

### 3.1 What MozaiksCore OWNS

| Domain | Responsibility | Implementation |
|--------|----------------|----------------|
| **App user authentication** | Validate OIDC tokens, provision local user records | `security/authentication.py` |
| **App user sessions** | WebSocket connections, session state | `core/websocket_manager.py` |
| **Plugin loading** | Dynamic import of `plugins/{name}/logic.py` | `core/plugin_manager.py` |
| **Plugin execution** | Route `/api/execute/{plugin}` to plugin's `execute()` | `core/director.py` |
| **Feature gating** | Check subscription tier before plugin access | `core/subscription_manager.py` |
| **In-app notifications** | Create, store, deliver via WebSocket | `core/notifications_manager.py` |
| **AI session brokering** | Map capability → workflow, mint execution tokens | `core/routes/ai.py` |
| **Configuration serving** | Theme, navigation, settings (JSON configs) | `core/config/*.json` |
| **Event bus** | Internal pub/sub for decoupled plugin communication | `core/event_bus.py` |

### 3.2 What MozaiksCore DELEGATES

| Domain | Delegated To | Contract |
|--------|--------------|----------|
| **Platform user auth** | Mozaiks Control Plane (.NET) | Never handled here |
| **Billing/payments** | MozaiksPay (via Control Plane) | Subscription state pushed to runtime |
| **Workflow execution** | MozaiksAI Runtime | Execution token + `RUNTIME_BASE_URL` |
| **App provisioning** | Mozaiks Control Plane | `MOZAIKS_APP_ID` injected at deploy |
| **Domain management** | Mozaiks Control Plane | Not handled here |
| **Hosting orchestration** | Mozaiks Control Plane | Not handled here |
| **Telemetry aggregation** | Insights.API | Push KPIs via `INSIGHTS_API_BASE_URL` |

### 3.3 What MozaiksCore REFUSES

| Forbidden Domain | Reason |
|------------------|--------|
| **Stripe integration** | Billing is Control Plane responsibility |
| **PayPal integration** | Billing is Control Plane responsibility |
| **GitHub OAuth** | Platform auth, not app user auth |
| **Domain provisioning** | Hosting is Control Plane responsibility |
| **SSL certificate management** | Hosting is Control Plane responsibility |
| **Platform user CRUD** | Platform users don't exist here |
| **App creation/deletion** | Control Plane responsibility |
| **Multi-app routing** | Each runtime instance = one app |

---

## 4. Subscription Model

### 4.1 Enforcement Model

Subscriptions are **externally enforced** by the Mozaiks Control Plane:

```
┌───────────────────┐     Pushes subscription state     ┌──────────────────┐
│  Mozaiks Control  │ ─────────────────────────────────▶│  MozaiksCore     │
│  Plane (.NET)     │     (plan, features, limits)      │  Runtime         │
└───────────────────┘                                   └──────────────────┘
         │                                                       │
         │ Handles:                                              │ Handles:
         │ • Billing                                             │ • Feature gating
         │ • Plan changes                                        │ • Plugin access
         │ • Payment processing                                  │ • Capability launch
         │ • Invoices                                            │
```

### 4.2 Local Configuration (Fallback)

When `MONETIZATION=0` (default), subscription config is read from `subscription_config.json`:
- All plugins unlocked
- No payment integration
- For development/self-hosted deployments only

When `MONETIZATION=1`:
- Subscription state checked against `subscriptions` collection
- Plugin access gated by plan's `plugins_unlocked` array
- Trial logic enforced locally

### 4.3 What Subscription Manager Does NOT Do

- ❌ Process payments
- ❌ Communicate with Stripe/PayPal
- ❌ Generate invoices
- ❌ Handle plan upgrades (receives state, doesn't initiate)

---

## 5. Plugin Boundary

### 5.1 Plugin Contract

Plugins are the **only place business logic lives** in MozaiksCore:

```python
# /backend/plugins/{plugin_name}/logic.py

async def execute(data: dict) -> dict:
    """
    Entry point for all plugin actions.
    
    Receives:
        data["action"]   - The action to perform
        data["user_id"]  - Injected by director (from JWT)
        data[...]        - Action-specific payload
    
    Returns:
        dict with results or {"error": "message"}
    """
    pass
```

### 5.2 Plugin Capabilities

| Capability | Allowed | Access Pattern |
|------------|---------|----------------|
| Database access | ✅ | `from core.config.database import db` |
| Event publishing | ✅ | `event_bus.publish("event_name", data)` |
| Notifications | ✅ | `notifications_manager.create_notification(...)` |
| Settings storage | ✅ | `settings_manager.get_plugin_settings(...)` |
| WebSocket routes | ✅ | `plugins/{name}/routes.py` with `register_routes()` |
| External HTTP | ✅ | Plugin's responsibility (use `aiohttp`) |
| Direct auth | ❌ | Must use injected `user_id` |
| Cross-plugin calls | ❌ | Use event bus for decoupling |

### 5.3 Plugin Isolation

Plugins are **NOT sandboxed**:
- Full Python runtime access
- Shared MongoDB connection
- Can import any installed package

**Security implication**: Plugin code must be trusted. In platform deployments, plugins are generated/vetted by AppGenerator.

---

## 6. MozaiksAI Integration

### 6.1 Role Separation

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              MozaiksCore                                 │
│  ┌────────────────────────────────────────────────────────────────┐     │
│  │  /api/ai/capabilities  →  List available AI capabilities       │     │
│  │  /api/ai/launch        →  Authorize + mint execution token     │     │
│  └────────────────────────────────────────────────────────────────┘     │
│                                    │                                     │
│                                    │ Returns:                            │
│                                    │ • launch_token (signed JWT)         │
│                                    │ • chat_id                           │
│                                    │ • runtime URLs                      │
└────────────────────────────────────┼─────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                              MozaiksAI                                   │
│  ┌────────────────────────────────────────────────────────────────┐     │
│  │  ChatUI       →  User-facing conversation interface            │     │
│  │  Workflows    →  Actual AI/LLM execution                       │     │
│  │  Pack Loader  →  Workflow discovery (optional)                 │     │
│  └────────────────────────────────────────────────────────────────┘     │
│                                                                          │
│  Validates: launch_token signature, iss, aud, exp                       │
│  Uses: ExecutionContext claims (user_id, app_id, workflow_id, plan)     │
└─────────────────────────────────────────────────────────────────────────┘
```

### 6.2 Execution Token Claims

MozaiksCore mints execution tokens containing:

```json
{
  "iss": "mozaikscore",
  "aud": "<configured audience>",
  "exp": "<10 min from now>",
  "mozaiks_token_use": "execution",
  "sub": "<identity_user_id>",
  "app_id": "<MOZAIKS_APP_ID>",
  "user_id": "<local user_id>",
  "chat_id": "<generated chat_id>",
  "capability_id": "<requested capability>",
  "workflow_id": "<mapped workflow>",
  "plan": "<user's subscription plan>",
  "roles": ["<user roles>"],
  "is_superadmin": false
}
```

### 6.3 What MozaiksCore Does NOT Do

- ❌ Execute workflows
- ❌ Host ChatUI
- ❌ Run LLM inference
- ❌ Store conversation history
- ❌ Implement AI agents

---

## 7. Data Flow Guarantees

### 7.1 User Data Isolation

All user data is scoped by `user_id`:
- Plugin data queries MUST include `user_id` filter
- Notifications are per-user
- Settings are per-user-per-plugin
- WebSocket connections are per-user

### 7.2 App Data Isolation

In multi-tenant deployments:
- Each app instance has its own MongoDB database
- `MOZAIKS_APP_ID` identifies the app for telemetry/tokens
- No cross-app data access within MozaiksCore

### 7.3 Request Tracing

Every request should be traceable via:
- `app_id` (from env)
- `user_id` (from JWT)
- `request_id` (generated per request)
- Event bus publishes for audit trail

---

## 8. Environment Variable Contract

### 8.1 Required for Production

| Variable | Purpose |
|----------|---------|
| `MOZAIKS_APP_ID` | App identity for scoping |
| `DATABASE_URI` | MongoDB connection |
| `MOZAIKS_AUTH_MODE` | `external` or `platform` |
| `MOZAIKS_JWKS_URL` | JWKS endpoint for token validation |
| `JWT_SECRET` | For execution token signing (HS256) |

### 8.2 Optional External Integrations

| Variable | Integration |
|----------|-------------|
| `RUNTIME_BASE_URL` | MozaiksAI runtime |
| `INSIGHTS_API_BASE_URL` | Telemetry push |
| `EMAIL_SERVICE_URL` | Email notifications |

### 8.3 NEVER Configure Here

| Variable | Reason |
|----------|--------|
| `STRIPE_*` | Billing is Control Plane |
| `GITHUB_*` | Platform auth |
| `DOMAIN_*` | Hosting is Control Plane |

---

## 9. API Surface

### 9.1 Public Endpoints (Authenticated App Users)

| Endpoint | Purpose |
|----------|---------|
| `POST /api/execute/{plugin}` | Execute plugin action |
| `GET /api/navigation` | Get nav items for user |
| `GET /api/available-plugins` | List accessible plugins |
| `GET /api/user-profile` | Get user profile |
| `GET /api/ai/capabilities` | List AI capabilities |
| `POST /api/ai/launch` | Launch AI capability |
| `WS /ws/notifications/{user_id}` | Real-time notifications |

### 9.2 Admin Endpoints (API Key Auth)

| Endpoint | Purpose |
|----------|---------|
| `POST /__mozaiks/admin/notifications/broadcast` | Broadcast notification |
| `GET /__mozaiks/admin/notifications/channels` | Channel status |

### 9.3 Health/Internal

| Endpoint | Purpose |
|----------|---------|
| `GET /` | Health check |
| `GET /api/app-config` | Public app config |
| `GET /api/theme-config` | Theme configuration |

---

## 10. Change Log

| Date | Change | Author |
|------|--------|--------|
| 2026-01-12 | Initial boundary contract | Forensic Audit |

---

## Appendix: Forbidden Integrations Checklist

Before adding any new integration, verify it does NOT:

- [ ] Handle platform user authentication
- [ ] Process payments directly
- [ ] Manage domains or SSL
- [ ] Create/delete apps
- [ ] Communicate with Stripe, PayPal, or payment processors
- [ ] Implement GitHub OAuth for platform access
- [ ] Route between multiple apps
- [ ] Store platform-level configuration

If any box is checked, the integration belongs in the **Mozaiks Control Plane**, not MozaiksCore.
