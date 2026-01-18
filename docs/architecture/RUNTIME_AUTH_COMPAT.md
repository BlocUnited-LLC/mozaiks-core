# Runtime Auth Compatibility (v1)

This document captures current runtime auth behavior and compatibility
constraints. It reflects the existing code paths and does not redesign
auth or monetization.

## 1) Supported End-User Auth Modes (Today)

Runtime supports three mutually exclusive modes for app end-users:

- `external` (default): OIDC/JWT via JWKS, issuer, audience validation.
- `platform`: same as `external`, using platform-injected settings.
- `local`: username/password with HS256 JWTs issued by the runtime.

References:
`backend/security/authentication.py`
`backend/security/auth.py`
`backend/core/config/settings.py`

## 2) Required JWT Claims (OIDC) and Role Parsing

### Required env configuration (external/platform)

- `MOZAIKS_JWKS_URL` (or `MOZAIKS_PLATFORM_JWKS_URL`)
- `MOZAIKS_ISSUER` (or `MOZAIKS_PLATFORM_ISSUER`)
- `MOZAIKS_AUDIENCE` (or `MOZAIKS_PLATFORM_AUDIENCE`)
- `MOZAIKS_JWT_ALGORITHMS` (default `RS256`)
- `MOZAIKS_USER_ID_CLAIM` (default `sub`)
- `MOZAIKS_EMAIL_CLAIM` (default `email`)
- `MOZAIKS_ROLES_CLAIM` (default `roles`)
- `MOZAIKS_SUPERADMIN_ROLE` and/or `MOZAIKS_SUPERADMIN_CLAIM`

`backend/core/config/settings.py`
`backend/security/authentication.py`

### Required JWT claims (external/platform)

- User identifier: `MOZAIKS_USER_ID_CLAIM` (falls back to `sub`)
- Email: `MOZAIKS_EMAIL_CLAIM` (optional in practice, used for profile)
- Roles: `MOZAIKS_ROLES_CLAIM` (used for superadmin checks)

User provisioning stores `external_user_id` from the user id claim and
derives a username from `MOZAIKS_USERNAME_CLAIM` or email.  
`backend/security/authentication.py`

### Roles parsing behavior

Roles are parsed from a single, top-level claim:

- If the claim is a list, each string item is accepted.
- If it is a string, it is split on commas or spaces.
- Nested claims (for example `realm_access.roles`) are not parsed.

`backend/security/authentication.py`

### Local mode claims

Runtime-issued tokens use HS256 (`JWT_SECRET`) and include:

- `sub` (username)
- `user_id` (Mongo user id)

`backend/security/auth.py`
`backend/security/constants.py`

## 3) WorkOS Policy

Runtime does NOT implement WorkOS logic and must not do so.
There are no WorkOS-specific SDKs or handlers in the codebase.  
`backend/security/authentication.py`

## 4) Compatibility Gaps for Enterprise SSO

Known gaps that can block upstream IdPs without configuration changes:

- Nested roles claims are not supported (`realm_access.roles`, `groups`, etc.).
  Only a top-level claim key is read, so dotted paths are ignored.
  `backend/security/authentication.py`
- WebSocket auth is HS256-only: in external/platform mode it still decodes
  with `JWT_SECRET`, so direct OIDC tokens fail unless token exchange is used.
  `backend/main.py`
- Optional claims like `email` and `preferred_username` are not required
  by the decoder, but missing values can reduce profile fidelity.
  `backend/security/authentication.py`

## 5) Recommended Minimal Changes (Optional)

These changes preserve current behavior but improve compatibility.
They are not required for v1 sync and are not implemented in this task.

1) Support nested claim paths  
   - Add a helper to resolve dotted claim paths in
     `backend/security/authentication.py` and use it in `_claim_roles`,
     `_claim_str`, and `_claim_email`.
   - This enables Keycloak `realm_access.roles` or IdP group claims without
     custom token mapping.

2) Fix WebSocket auth mismatch  
   - Reuse the HTTP auth path for WS tokens:
     call `_decode_external_jwt` for external/platform modes (or require
     app-scoped tokens when `MOZAIKS_TOKEN_EXCHANGE=true`).
   - File: `backend/main.py`

3) Optional `app_id`/audience enforcement  
   - Add an opt-in check to validate `aud` or `app_id` claim when present,
     without changing defaults.  
   - File: `backend/security/authentication.py`

These are minimal, backward-compatible improvements and should be scoped
behind explicit configuration flags if introduced.
