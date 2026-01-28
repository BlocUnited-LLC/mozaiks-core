## Auth Architecture Migration Report (Platform)

**Date:** January 27, 2026  
**Author:** mozaiks-platform Agent  
**Status:** Platform Phase Complete - Awaiting Core Alignment

This document is a status report intended for the mozaiks-core team.

---

## Executive Summary

mozaiks-platform has been fully migrated to the canonical Keycloak OIDC auth architecture.
All services validate JWTs via OIDC discovery using the shared `Mozaiks.Auth` building block.
Supabase JWT auth has been removed from all code paths.

This report exists to keep the Core↔Platform auth boundary explicit and stable.

---

## Target Auth Configuration (Platform)

All Platform services should converge on:

```json
{
  "Jwt": {
    "Authority": "http://localhost:8080/realms/mozaiks",
    "Audience": "mozaiks-api",
    "RequireHttpsMetadata": false
  }
}
```

---

## What Was Done (mozaiks-platform)

### 1. Auth Configuration Standardized

All Platform services now use identical auth configuration (see above).

### 2. Services Updated

| Service | File | Change |
|---------|------|--------|
| **MozaiksRestApi** | `Program.cs` | Uses `builder.AddMozaiksAuth()` |
| **MozDiscoveryService.Api** | `Program.cs` | Uses `builder.AddMozaiksAuth()` |
| **Payment.API** | `Program.cs` | Uses `builder.AddMozaiksAuth()` |
| **Monetization.API** | `Program.cs` | Fixed to use `builder.AddMozaiksAuth()` |

### 3. Supabase References Removed

- Deleted `SupabaseSettings` configuration class
- Renamed `GetSupabaseUserId()` → `GetUserId()` (IdP-agnostic)
- Updated all error messages to be IdP-agnostic
- Removed `SUPABASE_*` env vars from appsettings files
- Archived `Supabase-Migration.md` as deprecated

### 4. Documentation Updated

- `MozaiksRestApi/README.md` rewritten for Keycloak OIDC
- `Payment.API/readme.md` rewritten for Keycloak OIDC

### 5. Build Verification

All key auth-related projects compile successfully:
- ✅ `Mozaiks.Auth`
- ✅ `MozaiksRestApi`
- ✅ `MozDiscoveryService.Api`
- ✅ `Monetization.API`

---

## Auth Boundary Contract (Canonical)

### Platform Responsibilities (Consumer)

Platform is a JWT consumer only:
- Validates JWTs issued by Keycloak
- Extracts sub as user identity
- Extracts roles from Keycloak-compatible claim shapes
- Does not issue tokens
- Does not manage user sessions

### Core Responsibilities (Runtime)

Core validates the same JWTs from the same Keycloak instance:
- Supports OIDC discovery and/or explicit JWKS
- Enforces runtime authorization via roles/scopes

Core additionally must support service-to-service auth:
- Platform → Core internal endpoints: **Keycloak client-credentials JWT** with role `internal_service`
- Core → Platform internal endpoints (usage events): **Keycloak client-credentials JWT** with role `internal_service`

Core must not accept legacy API keys as bearer tokens.

---

## Core Alignment Status (as of Jan 27, 2026)

Core changes have been applied to align with the Keycloak-only contract:

- **Platform → Core entitlements sync** now requires a Keycloak app-only JWT with role `internal_service` (no `MOZAIKS_ALLOWED_SERVICE_KEYS`).
- **Platform → Core subscription sync** now requires a Keycloak app-only JWT with role `internal_service`.
- **Core → Platform usage events** now uses OAuth2 client-credentials to fetch an access token and sends `Authorization: Bearer <jwt>`.

Environment variables used by Core for Core → Platform S2S:

```bash
MOZAIKS_PLATFORM_URL=http://localhost:5000
MOZAIKS_PLATFORM_CLIENT_ID=<core-service-client>
MOZAIKS_PLATFORM_CLIENT_SECRET=<core-service-secret>
MOZAIKS_PLATFORM_TOKEN_SCOPE=
```

---

## Recommended Core Alignment Steps

See the canonical plan for core-side items:
- Ensure Keycloak is the single IdP
- Standardize env vars
- Ensure Keycloak role parsing is supported in core validator
- Remove legacy auth modes
- Align plugin-host JWT validation env var names
