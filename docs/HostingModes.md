# Hosting Modes (MozaiksPay-Only)

MozaiksCore Shell supports two practical billing deployment modes:

- **Self-hosted (default)**: no external platform calls; MozaiksPay endpoints return a safe mock "active" state for local development.
- **Managed (MozaiksPay via Gateway)**: the shell proxies MozaiksPay via a gateway using the end-user JWT + `x-correlation-id`.

This repo intentionally does **not** include investor network integration, challenges/leagues connectors, or platform analytics proxy routes.

---

## Self-Hosted

```dotenv
MOZAIKS_HOSTING_MODE=self_host
MOZAIKS_AUTH_MODE=local
```

- `/api/mozaiks/pay/*` does **not** call MozaiksPlatform.
- `POST /api/auth/token` + `POST /api/auth/register` are available for local auth (if you use them).

---

## Managed (MozaiksPay via Gateway)

```dotenv
MOZAIKS_HOSTING_MODE=hosted
MOZAIKS_AUTH_MODE=platform

# Backwards-compatible alias (deprecated): hosted + platform auth
MOZAIKS_MANAGED=true

MOZAIKS_GATEWAY_BASE_URL=https://gateway.example.com
MOZAIKS_APP_ID=your-app-id   # required for scope=app calls

# Platform JWT verification
MOZAIKS_PLATFORM_JWKS_URL=https://auth.example.com/.well-known/jwks.json
MOZAIKS_PLATFORM_JWT_ISSUER=https://auth.example.com/
MOZAIKS_PLATFORM_JWT_AUDIENCE=mozaiks-core
```

In managed mode:

- The shell **verifies platform-issued JWTs** (no shared signing keys with the core).
- `POST /api/auth/token` and `POST /api/auth/register` are **disabled** (platform owns login).
- Clients should call `POST /api/auth/token-exchange` to obtain a short-lived **app-scoped** JWT for this `app_id`.
- WebSockets authenticate via `Sec-WebSocket-Protocol: mozaiks,<jwt>` (no JWT-in-URL).

### MozaiksPay proxy endpoints

- `POST /api/mozaiks/pay/checkout`
- `GET /api/mozaiks/pay/subscription-status?scope=platform|app&appId=`
- `POST /api/mozaiks/pay/cancel`

### Correlation IDs

- Clients may pass `x-correlation-id`; otherwise the shell generates one and returns it.
- The same ID is forwarded to the gateway on proxied MozaiksPay calls.
