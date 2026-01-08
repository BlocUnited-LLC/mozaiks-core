# Auth Module

## Overview

The Auth module provides **identity-provider agnostic** authentication for the Mozaiks shell by validating JWTs (either externally issued or locally minted) and normalizing the current user context for all protected routes.

- No provider-specific SDK dependencies (Azure/MSAL/etc.)
- **Default auth mode is OIDC/JWKS** (`MOZAIKS_AUTH_MODE=external`)
- Local username/password is available only in `MOZAIKS_AUTH_MODE=local`

## Core Responsibilities

- Extract bearer token from `Authorization: Bearer <token>`
- Validate tokens based on `MOZAIKS_AUTH_MODE`
- Optionally exchange external tokens for short-lived app tokens
- Auto-provision/update a local user record in MongoDB on first valid token (external/platform/local)
- Provide a single FastAPI dependency for protected routes: `get_current_user`

## Backend API Reference

### `GET /api/auth/me`

Returns the current user's profile (from MongoDB) based on the validated token.

### `GET /api/auth/validate-token`

Returns a minimal `{ valid, user_id, username }` payload if the provided token is valid.

### `POST /api/auth/token-exchange`

Exchanges an externally issued JWT for a short-lived **app-scoped** JWT. Used when `MOZAIKS_TOKEN_EXCHANGE=true`.

## Backend Implementation

### `get_current_user` (FastAPI dependency)

Implemented in `backend/security/authentication.py`.

Behavior:

- Reads `MOZAIKS_AUTH_MODE` (`external` default)
- In `external`/`platform` mode:
  - Validates external JWTs via JWKS (`MOZAIKS_JWKS_URL`, `MOZAIKS_ISSUER`, `MOZAIKS_AUDIENCE`)
  - If `MOZAIKS_TOKEN_EXCHANGE=true`, rejects external JWTs for app APIs and requires an app-scoped token minted by `/api/auth/token-exchange`
- In `local` mode: validates shared-secret JWTs (`JWT_SECRET`, `JWT_ALGORITHM`)
- Ensures a local user record exists
- Returns a normalized user context including:
  - `username`, `user_id` (MongoDB `_id` string)
  - `roles`, `is_superadmin` (claim-driven; see env vars below)

### User provisioning

The backend auto-provisions users so the rest of the app can continue to rely on the local `users_collection` without any registration endpoint.

- `external`/`platform`: users are keyed by `external_user_id` (claim-configurable via `MOZAIKS_USER_ID_CLAIM`)
- `local`: users are keyed by `username` (`sub`)

## Admin Authorization

- `backend/security/authentication.py:is_superadmin(current_user)` computes superadmin status from configured roles/claims
- `backend/security/authentication.py:require_superadmin` is a dependency helper for admin-only endpoints

## Frontend Abstraction

The React context in `src/auth/AuthContext.jsx` exposes:

- `user`, `isAuthenticated`, `isLoading`, `error`
- `authMode`
- `tokenExchangeEnabled`
- `refreshUser()`
- `getAccessToken()`
- `authFetch(input, init)` (fetch wrapper that attaches `Authorization` and handles token exchange refresh)
- `logout()`

## Configuration (Environment Variables)

### Auth mode

```env
MOZAIKS_AUTH_MODE=external    # backend: platform | external | local
VITE_MOZAIKS_AUTH_MODE=external  # frontend: platform | external | local
```

### OIDC login (frontend)

```env
VITE_AUTH_AUTHORITY=
VITE_AUTH_CLIENT_ID=
VITE_AUTH_REDIRECT_URI=http://localhost:5173/auth/callback
VITE_AUTH_POST_LOGOUT_REDIRECT_URI=http://localhost:5173/
VITE_AUTH_SCOPE=openid profile email
VITE_AUTH_API_SCOPE=
```

### JWKS validation (backend)

```env
MOZAIKS_JWKS_URL=https://issuer.example.com/.well-known/jwks.json
MOZAIKS_ISSUER=https://issuer.example.com/
MOZAIKS_AUDIENCE=your-api-audience
MOZAIKS_JWT_ALGORITHMS=RS256
```

### Claim mappings + superadmin (backend)

```env
MOZAIKS_USER_ID_CLAIM=sub
MOZAIKS_EMAIL_CLAIM=email
MOZAIKS_USERNAME_CLAIM=
MOZAIKS_ROLES_CLAIM=roles
MOZAIKS_SUPERADMIN_ROLE=SuperAdmin
MOZAIKS_SUPERADMIN_CLAIM=
```

### Token exchange (optional)

```env
MOZAIKS_TOKEN_EXCHANGE=false
VITE_MOZAIKS_TOKEN_EXCHANGE=false
MOZAIKS_APP_TOKEN_EXPIRE_MINUTES=15
```

## Token Storage Notes

- OIDC session state is stored in `sessionStorage` by the OIDC client.
- App-scoped tokens (token exchange) are stored in `sessionStorage` (`mozaiks.auth.appToken`).
- Local-mode tokens are stored in `sessionStorage` by default and can be persisted to `localStorage` only when explicitly chosen by the user (dev/offline).
