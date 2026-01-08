# Authentication (Identity-Provider Agnostic)

MozaiksCore is designed to run with **any** OIDC/JWT issuer (self-hosted) or with a hosted platform that injects the same configuration at runtime. The code does not depend on any specific identity provider SDK.

## Auth Modes (Required)

Configure the mode using environment variables only:

- `MOZAIKS_AUTH_MODE=external` (default)
- `MOZAIKS_AUTH_MODE=platform`
- `MOZAIKS_AUTH_MODE=local` (optional/offline only)

| Mode | Frontend login | Backend token validation | Password storage |
|---|---|---|---|
| `external` | OIDC redirect (`/auth/login` → `/auth/callback`) | JWKS (`MOZAIKS_JWKS_URL`, issuer, audience) | No |
| `platform` | Same as `external` | Same as `external` | No |
| `local` | Username/password pages (`/login`, `/register`) | Shared secret JWT (`JWT_SECRET`) | Yes (hashed) |

## OIDC (Web / SPA)

Frontend (Vite) settings:

```env
VITE_AUTH_AUTHORITY=https://issuer.example.com
VITE_AUTH_CLIENT_ID=your-client-id
VITE_AUTH_REDIRECT_URI=https://your-app.example.com/auth/callback
VITE_AUTH_POST_LOGOUT_REDIRECT_URI=https://your-app.example.com/
VITE_AUTH_SCOPE=openid profile email
VITE_AUTH_API_SCOPE=
```

Backend JWKS validation:

```env
MOZAIKS_JWKS_URL=https://issuer.example.com/.well-known/jwks.json
MOZAIKS_ISSUER=https://issuer.example.com/
MOZAIKS_AUDIENCE=your-api-audience
MOZAIKS_JWT_ALGORITHMS=RS256
```

## Token Exchange (Optional)

When enabled, the frontend exchanges the external OIDC token for a short-lived **app-scoped** token:

- Backend: `MOZAIKS_TOKEN_EXCHANGE=1`
- Frontend: `VITE_MOZAIKS_TOKEN_EXCHANGE=1`

Flow:

1. User completes OIDC login (`/auth/callback`)
2. Frontend calls `POST /api/auth/token-exchange` with the external token in `Authorization`
3. Backend returns an app token (`access_token`)
4. Frontend uses the app token for subsequent API calls

If `MOZAIKS_TOKEN_EXCHANGE=0` (default), the backend accepts the external OIDC token directly.

## User Provisioning + Claims

The backend auto-provisions a local MongoDB user record for any valid token.

Configure claim mappings:

```env
MOZAIKS_USER_ID_CLAIM=sub
MOZAIKS_EMAIL_CLAIM=email
MOZAIKS_USERNAME_CLAIM=
MOZAIKS_ROLES_CLAIM=roles
```

## Superadmin Authorization

Superadmin is derived from roles and/or a boolean claim:

```env
MOZAIKS_SUPERADMIN_ROLE=SuperAdmin
MOZAIKS_SUPERADMIN_CLAIM=
```

Use `backend/security/authentication.py:require_superadmin` as a dependency on admin-only endpoints.

## Hosted vs Self-Hosted (No Code Changes)

- **Self-hosted**: set `MOZAIKS_AUTH_MODE=external` and provide your own issuer/JWKS env vars.
- **Hosted**: set `MOZAIKS_AUTH_MODE=platform`. A hosting platform can inject the same env vars; no code changes are required.

## Mobile / API Clients

Mobile/native clients can call the backend directly with `Authorization: Bearer <token>`.

- `external/platform`: backend validates via JWKS
- `local`: backend validates via `JWT_SECRET`

