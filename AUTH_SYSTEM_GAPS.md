# Auth Gaps and Dependencies (MozaiksCore)

Scope: code-only review of this repo for building a shared auth system with MozaiksAI.

## Auth dependencies in this repo

Backend (FastAPI):
- Auth mode is selected by `MOZAIKS_AUTH_MODE` (external | platform | local) and validated in `backend/core/config/settings.py`.
- External/platform JWT verification requires:
  - `MOZAIKS_JWKS_URL` (or legacy `MOZAIKS_PLATFORM_JWKS_URL`)
  - `MOZAIKS_ISSUER` (or legacy `MOZAIKS_PLATFORM_ISSUER`)
  - `MOZAIKS_AUDIENCE` (or legacy `MOZAIKS_PLATFORM_AUDIENCE`)
  - Claim names: `MOZAIKS_USER_ID_CLAIM`, `MOZAIKS_EMAIL_CLAIM`, `MOZAIKS_ROLES_CLAIM`
  - Superadmin indicator: `MOZAIKS_SUPERADMIN_ROLE` and/or `MOZAIKS_SUPERADMIN_CLAIM`
  - Algorithms: `MOZAIKS_JWT_ALGORITHMS` (default RS256)
- Local/app tokens use HS signing:
  - `JWT_SECRET` and optional `JWT_ALGORITHM` (defaults to HS256)
  - Local login/register uses `passlib` bcrypt (`backend/security/auth.py`).
- Token exchange (external/platform -> app token) is gated by `MOZAIKS_TOKEN_EXCHANGE=true` and requires `MOZAIKS_APP_ID` (`backend/security/auth.py`).
- User identity is always derived from JWT claims in `backend/security/authentication.py` and persisted in Mongo via `users_collection` (`backend/core/config/database.py`).

Frontend (React):
- OIDC config is provided via runtime globals or Vite env:
  - `__MOZAIKS_AUTH_MODE` or `VITE_MOZAIKS_AUTH_MODE`
  - `__MOZAIKS_AUTH_CONFIG` or `VITE_AUTH_AUTHORITY`, `VITE_AUTH_CLIENT_ID`, `VITE_AUTH_REDIRECT_URI`
- Token storage:
  - Local mode tokens stored in session/local storage (`src/auth/runtime/localTokenStore.ts`).
  - App-scoped tokens stored in session storage (`src/auth/runtime/appTokenStore.ts`).
- All API calls go through `authFetch` which attaches a Bearer token and clears state on 401/403 (`src/auth/AuthContext.jsx`).

## Cross-repo auth contracts implied for MozaiksAI

These are the explicit integration points in this repo that MozaiksAI needs to honor.

Launch to runtime:
- `/api/ai/launch` returns a `launch_token` for the runtime UI and opens a URL using `chatui_url_template` (`src/ai/AICapabilitiesPage.jsx`, `backend/core/routes/ai.py`).
- The launch token is minted in `backend/core/runtime/execution_tokens.py`:
  - Signing key: `MOZAIKS_EXECUTION_TOKEN_SECRET` (HS*) or `MOZAIKS_EXECUTION_TOKEN_PRIVATE_KEY(_PATH)` (RS*)
  - Algorithm: `MOZAIKS_EXECUTION_TOKEN_ALGORITHM` (fallback `JWT_ALGORITHM`)
  - Optional `iss`/`aud`: `MOZAIKS_EXECUTION_TOKEN_ISSUER`, `MOZAIKS_EXECUTION_TOKEN_AUDIENCE`
  - Claims include `mozaiks_token_use=execution`, `sub`, `app_id`, `chat_id`, `capability_id`, `workflow_id`,
    `user_id`, `roles`, `is_superadmin`, and `plan` (`backend/core/routes/ai.py`).
- MozaiksAI must validate this token with the same key/issuer/audience and enforce `mozaiks_token_use=execution`.

Runtime API calls (optional workflow discovery):
- `backend/core/runtime/manager.py` calls the runtime Pack Loader with `Authorization: Bearer <token>` if provided.
- MozaiksAI runtime must accept the same user tokens that MozaiksCore issues or forwards
  (external/platform OIDC tokens, and optionally app-scoped tokens if you choose to trust them).

Frontend runtime token access:
- `getRuntimeAccessToken()` in `src/auth/AuthContext.jsx` prefers the external OIDC access token and only falls back
  to app-scoped tokens; this is intended for MozaiksAI usage but is not used in this repo yet.

## Gaps and inconsistencies to resolve for a unified auth system

WebSocket auth mismatch:
- Frontend sends `access_token` in the query string and uses `/ws/notifications/{userIdHint}`
  (`src/websockets/WebSocketProvider.jsx`).
- Backend WebSocket route accepts connections without token verification and trusts the path user_id
  (`backend/main.py`, `backend/core/websocket_manager.py`).
- There is a separate WS auth helper that expects JWTs in `Sec-WebSocket-Protocol` and explicitly rejects URL tokens,
  but it is not wired to the WS route (`backend/core/http/websocket_auth.py`).

Missing admin auth helpers:
- `backend/core/routes/analytics.py`, `backend/core/routes/status.py`, and `backend/core/routes/app_metadata.py`
  import `get_current_admin_user` / `require_admin_or_internal`, but those functions are not defined in
  `backend/security/authentication.py`.

Admin surface consistency:
- Admin routes use different checks: `admin_users` uses constant-time compare and supports
  `MOZAIKS_APP_ADMIN_KEY` or `INTERNAL_API_KEY`, while `notifications_admin` uses direct equality with
  `MOZAIKS_APP_ADMIN_KEY` only (`backend/core/routes/admin_users.py`, `backend/core/routes/notifications_admin.py`).

JWT secret inconsistency:
- `backend/security/constants.py` falls back to `JWT_SECRET="supersecretkey"` while `backend/core/config/settings.py`
  generates an ephemeral secret if `JWT_SECRET` is missing, but authentication uses the constant file.
  This can lead to different secrets between startup validation and token mint/verify.

Role/admin gating not wired:
- `platform_admin_role` is loaded in settings but not used for authorization.
- `require_superadmin` is defined but not applied to any route in this repo.

Router wiring gaps (auth coverage depends on routing):
- Only `/api/auth`, `/api/notifications`, and `/api/ai` are included in `backend/core/director.py`.
  Other auth-protected routers (events, status, analytics, admin users, admin notifications, push subscriptions,
  pay proxy) are defined but not mounted in the app.

## Dependencies to align across MozaiksCore and MozaiksAI

Signing keys and algorithms:
- Decide whether MozaiksAI will accept external OIDC tokens, app-scoped tokens, or both.
- Ensure MozaiksAI uses the same JWT verification settings for:
  - OIDC tokens (JWKS URL, issuer, audience, algorithms)
  - App tokens (HS secret + algorithm, `mozaiks_token_use=app`)
  - Execution tokens (separate signing key + `mozaiks_token_use=execution`)

Claim mapping:
- MozaiksCore maps identity via `MOZAIKS_USER_ID_CLAIM`, `MOZAIKS_EMAIL_CLAIM`, and `MOZAIKS_ROLES_CLAIM`.
  MozaiksAI should rely on the same claim names for cross-repo consistency.

Token exchange behavior:
- If `MOZAIKS_TOKEN_EXCHANGE=true`, MozaiksCore rejects external tokens for most API routes.
  Align MozaiksAI expectations with whether the client should use app tokens or OIDC tokens for runtime calls.

Runtime UI launch:
- Ensure `MOZAIKS_RUNTIME_UI_BASE_URL` and `MOZAIKS_CHATUI_URL_TEMPLATE` produce a URL
  that MozaiksAI accepts and validates (token in query string by default).
