# MozaiksCore Foundation Audit (8-Part)

This is a self-contained audit of what the MozaiksCore foundation/boilerplate provides out-of-the-box.

It includes the key behaviors, APIs, config formats, and environment variables without requiring you to open other files to understand how it works.

---

## 1) What’s Included

### 1.1 Backend (FastAPI) capabilities

Out of the box the backend provides:

- Auth modes via env (`external`/`platform` = OIDC/JWKS, `local` = username/password + JWT)
- Plugin execution (business-logic modules) via a single execute entrypoint per plugin
- Declarative config for navigation/theme/settings/subscriptions/plugins via JSON
- Notifications subsystem + WebSocket notification channel
- Subscription gating (optional via `MONETIZATION=1`)
- Event logging for user activity + KPIs + optional outbound “Insights” push
- Basic request hardening: correlation IDs, request-size limits, security headers, host allowlist, CORS
- Health probes: `/healthz` and `/readyz`

### 1.2 Frontend (React) capabilities

Out of the box the frontend provides:

- Login/register screens (local mode only)
- Profile page + settings UI driven by backend-provided config
- Subscription page (only shown when monetization is enabled)
- Notifications UI
- AI capability launcher (`/ai`) that opens runtime-owned ChatUI
- Plugin pages routed as `/plugins/:pluginName` and loaded dynamically

### 1.3 Main folders/modules (purpose)

Backend:

- `backend/main.py` - FastAPI entrypoint; mounts the core app and registers the notifications WebSocket.
- `backend/core/` - core orchestration, routing, plugin manager, settings/notifications/subscriptions, analytics, websocket manager, HTTP middleware.
- `backend/security/` - auth routes, token validation (self-hosted + managed), rate limiting.
- `backend/plugins/` — example “business logic modules” (plugins) such as notes/tasks.
- `backend/app/` — “connector” system and `/api/mozaiks/pay/*` proxy routes (managed vs self-hosted mock).

Frontend:

- `src/App.jsx` - app routes and layout.
- `src/ai/` - AI capability launcher UI.
- `src/auth/` - AuthContext and login/register UI.
- `src/core/plugins/` - plugin UI loader and plugin context.
- `src/plugins/` - frontend plugin UIs.
- `src/core/theme/` - theme/branding.
- `src/notifications/` - notification UI.
- `src/websockets/` - WebSocket integration helpers.

### 1.4 Language(s) and framework(s)

- Backend: Python + FastAPI + Uvicorn + MongoDB (Motor)
  - `backend/requirements.txt` includes:
    ```txt
    fastapi
    uvicorn[standard]
    motor
    pymongo
    ```
- Frontend: React + Vite + Tailwind
  - `package.json` includes:
    ```json
    "scripts": { "dev": "vite --host", "build": "vite build" },
    "dependencies": { "react": "...", "react-router-dom": "..." }
    ```

---

## 2) Authentication System

MozaiksCore has **two auth modes** controlled by environment variables:

1) **Self-hosted mode** (default): the app itself handles username/password and issues JWTs.
2) **Managed mode**: the Mozaiks Platform is the identity provider; the app validates platform JWTs via JWKS.

### 2.1 Self-hosted mode (username/password + JWT)

Key endpoints:

- `POST /api/auth/token` (login)
- `POST /api/auth/register` (register)
- `GET /api/auth/me` (user info)
- `GET /api/auth/validate-token` (token validity)

Login issues a JWT containing `sub` and `user_id`:

```py
# backend/security/auth.py
@router.post("/token")
async def login_for_access_token(...):
    ...
    access_token = create_access_token(
        data={"sub": user["username"], "user_id": str(user["_id"])}
    )
```

Frontend stores `access_token` in localStorage and uses it as `Authorization: Bearer <token>`:

```jsx
// src/auth/AuthContext.jsx
const response = await fetch('/api/auth/token', { method: 'POST', ... });
localStorage.setItem('authToken', data.access_token);
```

### 2.2 Managed mode (platform-issued JWT via JWKS)

When `MOZAIKS_MANAGED=true`, MozaiksCore:

- disables self-hosted login/register endpoints (returns 404)
- validates platform JWTs via JWKS
- creates/updates a local user record linked to `platform_user_id`
- determines admin privileges from platform claims (`roles` and/or `is_admin`)

Platform JWT validation (JWKS):

```py
# backend/security/platform_jwt.py
async def decode_platform_jwt(token: str) -> dict:
    jwks = await _get_jwks()
    return jwt.decode(token, jwks, algorithms=list(settings.platform_jwt_algorithms), ...)
```

Managed-mode user resolution:

```py
# backend/security/authentication.py
if settings.mozaiks_managed:
    claims = await decode_platform_jwt(token)
    user = await _get_or_create_platform_user(claims)
    return user
```

Note (important for product): the current frontend login flow calls `/api/auth/token` and `/api/auth/register`. In managed mode those endpoints are disabled, so the platform must supply a JWT to the app frontend (or you adjust the frontend auth flow for managed hosting).

### 2.3 Roles pre-defined

There is one built-in role concept:

- `is_admin: true|false` stored on the user document.

Admin-only endpoints use this check:

```py
# backend/security/authentication.py
async def get_current_admin_user(...):
    if not bool(user.get("is_admin")):
        raise HTTPException(status_code=403, detail="Admin privileges required")
```

There is also an “internal service” auth mechanism:

- `X-Internal-Api-Key: <INTERNAL_API_KEY>` can be used for specific endpoints intended for service-to-service calls.

```py
# backend/security/authentication.py
async def require_admin_or_internal(x_internal_api_key: str | None = Header(...), admin_user: dict | None = Depends(...)):
    if admin_user is not None:
        return {"auth": "admin", **admin_user}
    if expected and provided and _safe_equals(expected, provided):
        return {"auth": "internal"}
    raise HTTPException(status_code=401, detail="Unauthorized")
```

### 2.4 Is there a default admin account?

No. Nothing is created by default.

You can bootstrap an initial admin **only if you explicitly enable it** via env vars:

```py
# backend/security/auth.py
@router.on_event("startup")
async def bootstrap_admin_user():
    if not _env_truthy("MOZAIKS_BOOTSTRAP_ADMIN"):
        return
    ...
    await users_collection.insert_one({ ..., "is_admin": True })
```

---

## 3) Admin Dashboard

### 3.1 Is there a pre-built admin panel UI?

No dedicated `/admin` UI exists in the frontend. The current UI is primarily:

- `/login`, `/register`
- `/profile`
- `/subscriptions` (if monetization enabled)
- `/plugins/:pluginName` (plugin UI pages)

Example route config:

```jsx
// src/App.jsx
<Route path="/profile" element={<ProtectedRoute><ProfilePage /></ProtectedRoute>} />
<Route path="/plugins/:pluginName" element={<ProtectedRoute><PluginPage /></ProtectedRoute>} />
```

### 3.2 What “admin functionality” exists out-of-the-box (API)

Admin/internal endpoints exist for dashboards and debugging:

Analytics (admin or internal):

```py
# backend/core/routes/analytics.py
@router.get("/kpis")
async def get_dashboard_kpis(current_user: dict = Depends(get_current_admin_user)):
    ...
```

App metrics (admin or internal):

```py
# backend/core/routes/app_metadata.py
@router.get("/metrics")
async def get_app_metrics(..., current_user: dict = Depends(require_admin_or_internal)):
    ...
```

Debug plugin status (admin or internal, and only if debug endpoints enabled):

```py
# backend/core/director.py
@app.get("/api/debug/plugin-status")
async def debug_plugin_status(current_user: dict = Depends(require_admin_or_internal)):
    if not settings.debug_endpoints_enabled:
        raise HTTPException(status_code=404, detail="Not found")
```

### 3.3 What can admins “manage” right now?

Out of the box, admins can:

- call admin metrics endpoints (KPIs, app metrics, snapshots)
- call debug endpoints (plugin status)

User management (create/disable users, role management UI, etc.) is not provided as a full admin product UI here; that’s typically a Mozaiks Platform control-plane feature.

---

## 4) Mozaiks Platform Integration

### 4.1 App identity (`appId`)

MozaiksCore identifies the running app primarily by `MOZAIKS_APP_ID` (env var).

Metadata endpoint:

```py
# backend/core/routes/app_metadata.py
@router.get("/metadata")
async def get_app_metadata():
    app_id = os.getenv("MOZAIKS_APP_ID") or "unknown-app"
    return {"appId": app_id}
```

The same `appId` is also used when storing analytics events:

```py
# backend/core/analytics/raw_events.py
def _app_id(app_id):
    return app_id or os.getenv("MOZAIKS_APP_ID") or "unknown-app"
```

### 4.2 Managed auth integration

Managed mode relies on platform JWKS and optional issuer/audience checks:

```py
# backend/security/platform_jwt.py
options = {"verify_aud": bool(settings.platform_jwt_audience)}
return jwt.decode(token, jwks, algorithms=list(settings.platform_jwt_algorithms), issuer=..., audience=...)
```

### 4.3 MozaiksPay / Gateway integration

The app provides `/api/mozaiks/pay/*` endpoints. In managed mode it proxies those calls to a gateway configured by env vars:

```py
# backend/app/runtime/connector_loader.py
def is_managed_mode() -> bool:
    if not _env_truthy(os.getenv("MOZAIKS_MANAGED")):
        return False
    base_url = (os.getenv("MOZAIKS_GATEWAY_BASE_URL") or "").strip()
    return bool(base_url)
```

### 4.4 Telemetry / analytics reporting (“Insights push”)

MozaiksCore can periodically push KPIs and raw events to an external Insights API when enabled.

Startup hook that starts the push loop:

```py
# backend/main.py
if settings.insights_push_enabled:
    _insights_task = asyncio.create_task(run_insights_push_loop(...))
```

The push loop computes KPIs and POSTs to endpoints like `/api/insights/ingest/kpis`.

---

## 5) Customization Points (what AI agents can safely extend)

### 5.1 Plugins = business logic modules (backend)

Plugin pattern (one entrypoint function). Example:

```py
# backend/plugins/notes_manager/logic.py
async def execute(data):
    action = data.get("action", "")
    user_id = data.get("user_id", "")
    collection = db["notes"]
    ...
```

Plugins are registered declaratively in JSON:

```json
// backend/core/config/plugin_registry.json
{
  "name": "notes_manager",
  "enabled": true,
  "backend": "plugins.notes_manager.logic"
}
```

Plugins are executed through a single API:

```py
# backend/core/director.py
@app.post("/api/execute/{plugin_name}")
async def execute_plugin(plugin_name: str, request: Request, user: dict = Depends(get_current_user)):
    data = await request.json()
    data["user_id"] = user["user_id"]
    return await plugin_manager.execute_plugin(plugin_name, data)
```

Plugin imports are restricted to the plugin’s own namespace:

```py
# backend/core/plugin_manager.py
if not backend_path.startswith(f"plugins.{plugin_name}."):
    return False
```

### 5.2 Plugins = business logic UI (frontend)

Each plugin has a `register.js` metadata file, e.g.:

```js
// src/plugins/notes_manager/register.js
export default {
  name: "notes_manager",
  displayName: "Notes Manager",
  version: "1.0.0"
};
```

And a UI entrypoint file (`index.js`) loaded dynamically by the plugin UI system.

### 5.3 Declarative “configuration points” (JSON)

These are the main JSON configs that define app behavior without changing core code:

- `navigation_config.json` — what menu items exist
- `plugin_registry.json` — what backend plugins exist and how to import them
- `subscription_config.json` — plans + which plugins are unlocked
- `settings_config.json` — profile/settings UI schema (drives what users can edit)
- `notifications_config.json` — notification types and settings fields
- `theme_config.json` — branding and theme values

### 5.4 What should NOT be modified by AI agents (core stability)

To keep the foundation “app-agnostic” and stable, agents should avoid modifying the core orchestration/security boundary files and instead add plugins/config:

- `backend/main.py` (entrypoint and global wiring)
- `backend/core/director.py` (core API and routing)
- `backend/core/plugin_manager.py` (plugin execution sandbox rules)
- `backend/security/authentication.py` and `backend/security/platform_jwt.py` (auth trust boundary)
- `src/core/*` (plugin loader and shared UI infrastructure)

### 5.5 Stubs/placeholders for agent-generated logic

There is a workflow stub intended to be extended:

```py
# backend/core/workflows/ag2_workflow_stub.py
class AG2Workflow:
    async def execute_step(self, step_name: str, input_data: Any) -> Dict[str, Any]:
        return { "status": "completed", "step": step_name, "output": f"Processed {input_data}" }
```

### 5.6 Is there a single “manifest” that declares features?

Not yet. Today the “manifest” is effectively:

- a set of JSON configs (plugins/navigation/settings/subscriptions/theme/notifications), plus
- environment flags (managed mode, monetization, limits)

---

## 6) Database & Models

### 6.1 Database default

MongoDB is the default database (Motor async driver):

```py
# backend/core/config/database.py
mongo_client = AsyncIOMotorClient(MONGO_URI, **mongo_client_options)
db = mongo_client[DATABASE_NAME]
```

### 6.2 Core collections provided

Core collections:

```py
# backend/core/config/database.py
users_collection = db["users"]
subscriptions_collection = db["subscriptions"]
settings_collection = db["settings"]
```

Analytics/event collections:

- `user_events` stores immutable facts like `UserSignedUp` and `UserActive`:
  ```py
  # backend/core/analytics/raw_events.py
  user_events_collection = db["user_events"]
  ```

### 6.3 What does a “User” look like?

This is schemaless (Mongo), but users typically include:

- `username`, `email`, `full_name`
- `hashed_password` (self-hosted only)
- `disabled`
- `created_at`, `updated_at`, `last_login`, `last_active`
- `is_admin` (optional)
- `platform_user_id` (managed mode)

Example self-hosted user creation:

```py
# backend/security/auth.py
new_user = {
  "username": user_data.username,
  "email": user_data.email,
  "hashed_password": hashed_password,
  "disabled": False
}
```

### 6.4 How do custom models (agent-generated) integrate?

Recommended pattern (as used by plugins):

- each plugin owns its own Mongo collection(s)
- each document stores `user_id` and optionally `app_id` for scoping

Example:

```py
# backend/plugins/notes_manager/logic.py
collection = db["notes"]
new_note = { "user_id": user_id, "title": ..., "created_at": ... }
```

---

## 7) Environment Setup

### 7.1 Core env vars (most important)

These env vars control core behavior:

- `ENV` — `development|production`
- `MONGODB_URI` (preferred) or `DATABASE_URI` (legacy fallback) and optional `DATABASE_NAME`
- `ALLOWED_HOSTS` — required in production
- `FRONTEND_URL` / `ADDITIONAL_CORS_ORIGINS` — CORS allowlist
- `JWT_SECRET` / `JWT_ALGORITHM` / `ACCESS_TOKEN_EXPIRE_MINUTES` — self-hosted auth
- `MOZAIKS_MANAGED` — enable platform-auth mode
- `MOZAIKS_APP_ID` — app identity
- `MOZAIKS_PLATFORM_JWKS_URL` (+ issuer/audience/claims mapping) — managed JWT verification

Example managed-mode minimum:

```env
ENV=production
MOZAIKS_MANAGED=true
MOZAIKS_APP_ID=app_123
MOZAIKS_PLATFORM_JWKS_URL=https://platform.example.com/.well-known/jwks.json
ALLOWED_HOSTS=app123.example.com
```

### 7.2 Runtime limits (ops knobs)

- `PLUGIN_EXEC_TIMEOUT_S` and `PLUGIN_EXEC_MAX_CONCURRENCY`
- `WEBSOCKET_MAX_CONNECTIONS_PER_USER`
- `MAX_REQUEST_BODY_BYTES`

Example:

```env
PLUGIN_EXEC_TIMEOUT_S=15
PLUGIN_EXEC_MAX_CONCURRENCY=8
WEBSOCKET_MAX_CONNECTIONS_PER_USER=5
MAX_REQUEST_BODY_BYTES=1000000
```

### 7.3 Is there a setup script?

There is no single “init” script; initialization happens on startup:

- plugin manager init
- DB connection verification + index init
- optional Insights push loop

---

## 8) Deployment

### 8.1 How it runs (backend)

MozaiksCore runs as an ASGI service (Uvicorn):

```py
# backend/main.py
if __name__ == "__main__":
    uvicorn.run("main:app", host=host, port=port, ...)
```

It exposes health endpoints for container orchestrators:

- `GET /healthz` (liveness)
- `GET /readyz` (readiness; checks Mongo unless `MOZAIKS_ALLOW_NO_DB=1`)

### 8.2 Frontend build/run

- Dev: `npm run dev`
- Prod build: `npm run build` (static assets)

### 8.3 Docker/container support

This repository does not currently include a `Dockerfile` or `docker-compose.yml`.

If you deploy with Docker/Azure Web Apps, you will typically add:

- a backend Dockerfile that runs Uvicorn
- a frontend build step that outputs static assets (served by a CDN, Nginx, or separate hosting)

### 8.4 “Mozaiks hosting service” integration

The integration points for Mozaiks-managed hosting are environment-driven:

- Managed auth via platform JWKS (`MOZAIKS_MANAGED=true`, `MOZAIKS_PLATFORM_JWKS_URL=...`)
- App identity via `MOZAIKS_APP_ID`
- Optional gateway proxy for `/api/mozaiks/pay/*` via `MOZAIKS_GATEWAY_BASE_URL`
- Optional outbound Insights push via `INSIGHTS_PUSH_ENABLED=1` and `INSIGHTS_*` vars
