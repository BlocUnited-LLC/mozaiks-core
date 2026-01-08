# Migration Notes (Auth)

This repo moved from **local username/password by default** to a **provider-agnostic OIDC/JWKS default**.

## What Changed

- Default auth mode is now `MOZAIKS_AUTH_MODE=external`
- OIDC redirect flow is implemented in the frontend:
  - `/auth/login` starts login
  - `/auth/callback` completes login
  - `/auth/logout` logs out
- Local username/password endpoints are only exposed when `MOZAIKS_AUTH_MODE=local`
- Optional token exchange (`/api/auth/token-exchange`) can mint short-lived app tokens

## Switching Hosted vs Self-Hosted (Env Vars Only)

No code changes are required.

### Self-hosted (recommended)

Set:

- `MOZAIKS_AUTH_MODE=external`
- `VITE_MOZAIKS_AUTH_MODE=external`

Provide:

- Frontend OIDC: `VITE_AUTH_AUTHORITY`, `VITE_AUTH_CLIENT_ID`, `VITE_AUTH_REDIRECT_URI`, `VITE_AUTH_POST_LOGOUT_REDIRECT_URI`, `VITE_AUTH_SCOPE`
- Backend JWKS: `MOZAIKS_JWKS_URL`, `MOZAIKS_ISSUER`, `MOZAIKS_AUDIENCE`, `MOZAIKS_JWT_ALGORITHMS`

### Hosted

Set:

- `MOZAIKS_AUTH_MODE=platform`
- `VITE_MOZAIKS_AUTH_MODE=platform`

In hosted deployments, the hosting platform can inject the same OIDC/JWKS env vars. The shell does not hard-code any identity provider details.

## Optional: Token Exchange

Enable only if you want app-scoped tokens:

- Backend: `MOZAIKS_TOKEN_EXCHANGE=1`
- Frontend: `VITE_MOZAIKS_TOKEN_EXCHANGE=1`
- Recommended: set `MOZAIKS_APP_ID` (required if `MOZAIKS_TOKEN_EXCHANGE=1` in production)

Behavior:

- `MOZAIKS_TOKEN_EXCHANGE=0`: backend accepts the external OIDC token directly
- `MOZAIKS_TOKEN_EXCHANGE=1`: backend requires the app token for app APIs (frontend exchanges automatically)

## Local Mode (Offline Only)

Set:

- `MOZAIKS_AUTH_MODE=local`
- `VITE_MOZAIKS_AUTH_MODE=local`
- `JWT_SECRET=<strong secret>`

Local-only endpoints become available:

- `POST /api/auth/token`
- `POST /api/auth/register`

## Verification Checklist

- Login redirect: visit `/auth/login` and confirm your provider redirects back to `/auth/callback`
- Token validation: `GET /api/auth/validate-token` returns `{ valid: true }` after login
- Token exchange off: `MOZAIKS_TOKEN_EXCHANGE=0` allows direct calls with the OIDC access token
- Token exchange on: `MOZAIKS_TOKEN_EXCHANGE=1` returns an app token from `POST /api/auth/token-exchange` and app APIs accept that token
- Admin role check: configure `MOZAIKS_SUPERADMIN_ROLE` and/or `MOZAIKS_SUPERADMIN_CLAIM`, then verify `require_superadmin` blocks non-superadmins

