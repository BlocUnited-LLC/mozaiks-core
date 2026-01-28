# 🔐 Auth Architecture Migration Plan

**Date:** January 27, 2026  
**Status:** IN PROGRESS (Platform complete; Core boundary aligned; pending end-to-end verification)  
**Repos Affected:** mozaiks-core, mozaiks-platform, control-plane

---

## Executive Summary (Canonical Decision)

You requested **one concise solution** (no backwards compatibility) that supports **self-hosting** and aligns **both repos** under the same local root.

**Chosen solution:**
- **Identity Provider:** **Keycloak (self-hosted OIDC)**
- **Token format:** **JWT (RS256) validated via JWKS**
- **Core (mozaiks-core):** validates JWTs and enforces app/runtime authorization (Python)
- **Platform (mozaiks-platform):** validates the SAME JWTs for protected platform endpoints (\.NET)

**Hard rule:** there is exactly **one** IdP issuing JWTs (Keycloak). No Supabase auth, no WorkOS auth, no duplicate Identity services.

## Platform Status Report

See `docs/contracts/AUTH_PLATFORM_MIGRATION_REPORT.md` for the platform-side migration status.

Current working contract:
- Platform no longer accepts API keys as bearer tokens for internal endpoints.
- Platform requires Keycloak JWTs with role `internal_service` for service-to-service endpoints.
- Core accepts Keycloak JWTs for subscription/entitlements sync and uses client-credentials JWTs for usage events.

---

## 🚑 Panic Mode (Do This First — 30 to 60 minutes)

If you’re overwhelmed, do **not** try to “understand all the .NET”. The goal is simply to get to a **single working, scalable loop** with minimal moving parts.

### Panic Step A: Freeze the architecture (already decided)

We are standardizing on **Keycloak** as the only issuer of JWTs for both Core + Platform.

### Panic Step B: Back up before deleting anything (required)

Run Phase 1 exactly as written. If Phase 1 isn’t done, **stop**.

### Panic Step C: Prove you still have a working system (smoke test)

1. Start **Core** (Python runtime) and confirm it responds on its port.
2. Start **Platform Payment** and confirm `/health` works.
3. Only after both are green: proceed with cleanup/migration steps below.

### Panic Step D: Remove duplication (one owner)

Rule: **Identity code lives in exactly one place**.
- For this canonical plan: **Keycloak config + theme live in mozaiks-core**, and **no Identity service exists in either repo**.
- Platform is only a JWT *consumer* (validation), not an issuer.

---

## ✅ Canonical Execution Checklist (Paste This Into Claude Code)

**Root folder (both repos live here):**

`C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\`

### Phase 0 — Backups (required)

```powershell
$timestamp = Get-Date -Format "yyyy-MM-dd-HHmm"
$root = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code"
$backupRoot = Join-Path $root "BACKUP-AUTH-CANONICAL-$timestamp"
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null

Copy-Item -Recurse -Force "$root\mozaiks-core"     "$backupRoot\mozaiks-core"
Copy-Item -Recurse -Force "$root\mozaiks-platform" "$backupRoot\mozaiks-platform"
if (Test-Path "$root\control-plane") { Copy-Item -Recurse -Force "$root\control-plane" "$backupRoot\control-plane" }

Write-Host "✅ Backups created: $backupRoot"
```

### Phase 1 — Make Keycloak the single IdP (Core owns it)

**Source of truth:** Keycloak is already present in Core compose: mozaiks-core/docker-compose.yml.

Key paths in Core (already exist):
- Keycloak theme: mozaiks-core/runtime/ai/keycloak/theme/mozaiks/login/resources/img/bg.png
- Keycloak logo:  mozaiks-core/runtime/ai/keycloak/theme/mozaiks/login/resources/img/logo.svg

**Keycloak endpoints (local default):**
- Issuer: `http://localhost:8080/realms/mozaiks`
- JWKS: `http://localhost:8080/realms/mozaiks/protocol/openid-connect/certs`

#### Phase 1A — Remove the legacy Identity.API dependency from Core compose (required)

Core’s current `docker-compose.yml` still wires an `identity-api` service and points other services at it.
That is incompatible with the canonical decision (**Keycloak is the issuer**).

**Edit file:** `mozaiks-core/docker-compose.yml`

1) Remove the `identity-api:` service entirely.

2) Remove all `depends_on: identity-api` references:
- `shell` currently depends on `identity-api`.
- `plugin-runtime` currently depends on `identity-api`.

3) Remove the plugin-host legacy JWKS env vars and replace with the canonical Core runtime vars:
- Remove: `JWT_ISSUER`, `JWKS_URL`, `JWT_AUDIENCE`
- Add (matching Phase 2C.5):
  - `AUTH_ISSUER=http://keycloak:8080/realms/mozaiks`
  - `AUTH_JWKS_URL=http://keycloak:8080/realms/mozaiks/protocol/openid-connect/certs`
  - `AUTH_AUDIENCE=<your-client-id-or-audience>`

4) Remove `SKIP_AUTH=true` from `plugin-runtime` (dev-only bypass).

5) Wire Core runtime to Keycloak (required)

In the `ai-runtime` service `environment:` section, set the canonical auth vars (Phase 2):
- `MOZAIKS_OIDC_DISCOVERY_URL=http://keycloak:8080/realms/mozaiks/.well-known/openid-configuration`
- `AUTH_AUDIENCE=<your-client-id-or-audience>`
- `AUTH_ENABLED=true`

#### Phase 1B — Ensure Keycloak uses the Mozaiks theme (required for “aesthetics”)

The repo already contains a theme and a Dockerfile that bakes it into a Keycloak image:
- `mozaiks-core/runtime/ai/keycloak/Dockerfile`
- `mozaiks-core/runtime/ai/keycloak/theme/mozaiks/...`

**Edit file:** `mozaiks-core/docker-compose.yml`

Replace the `keycloak` service image to use a custom build so the theme is actually available:

- Replace:
  - `image: quay.io/keycloak/keycloak:23.0`
- With:
  - `build:`
    - `context: ./runtime/ai/keycloak`
    - `dockerfile: Dockerfile`

Then keep:
- `ports: "8080:8080"`
- `KEYCLOAK_ADMIN`, `KEYCLOAK_ADMIN_PASSWORD`

Important: `runtime/ai/keycloak/Dockerfile` currently uses Keycloak `24.0`.
Either update compose to the same version or change the Dockerfile base image so they match.

#### Phase 1C — Keycloak bootstrap checklist (must be true before Phase 2/3 verification)

In Keycloak Admin UI (`http://localhost:8080/admin`):

1) Create realm: `mozaiks`

2) Create clients:
- SPA client (frontend): `mozaiks-shell`
  - Access Type: Public
  - Valid redirect URIs:
    - `http://localhost:5173/*`
  - Web origins:
    - `http://localhost:5173`

- API audience client (backend validation target): `mozaiks-api`
  - Access Type: Confidential (recommended)
  - Service accounts: Off (unless you explicitly need it)

3) Ensure the access token contains an audience that matches what services validate:
- If Platform/Core validate `Audience=mozaiks-api`, configure Keycloak to include `mozaiks-api` in the `aud` claim.
  (Typical way: add an “Audience” mapper via Client Scopes.)

4) Create realm roles (exact strings; used by both Core + Platform):
- `superadmin`
- `platform_admin`
- `internal_service`

### Phase 2 — Core auth: enforce ONE configuration (no aliases)

Core currently has **multiple** auth implementations (FastAPI deps under `core.ai_runtime.auth`, and older `security/authentication.py`, plus plugin-host auth). This phase forces **one** implementation and **one** env var scheme.

#### Phase 2A — Canonical auth module (winner)

**Winner (the only JWT validation code that remains authoritative):**
- `mozaiks-core/runtime/ai/core/ai_runtime/auth/*`

Everything else becomes either:
- a thin wrapper that calls the winner, or
- deleted.

#### Phase 2B — Canonical configuration (the ONLY env vars)

Stop using `MOZAIKS_AUTH_MODE`, `MOZAIKS_ISSUER`, `MOZAIKS_JWKS_URL`, `MOZAIKS_*AUDIENCE*` for Core runtime auth.

**Use OIDC discovery (recommended):**
- `MOZAIKS_OIDC_DISCOVERY_URL=http://localhost:8080/realms/mozaiks/.well-known/openid-configuration`
- `AUTH_AUDIENCE=<your-client-id-or-audience>`
- `AUTH_REQUIRED_SCOPE=` (empty for Keycloak unless you explicitly mint `scp`)
- `AUTH_ENABLED=true`

**OR use explicit overrides (no discovery):**
- `AUTH_ISSUER=http://localhost:8080/realms/mozaiks`
- `AUTH_JWKS_URL=http://localhost:8080/realms/mozaiks/protocol/openid-connect/certs`
- `AUTH_AUDIENCE=<your-client-id-or-audience>`
- `AUTH_REQUIRED_SCOPE=`
- `AUTH_ENABLED=true`

#### Phase 2C — Exact code changes Claude must perform (Core)

1) DELETE duplicated .NET identity service from Core (hard delete)
- Delete folder: `mozaiks-core/backend/src/Identity.API`

2) Make Keycloak roles work everywhere (required)

Right now `core.ai_runtime.auth.jwt_validator.JWTValidator` pulls roles from `roles` claim by default, but Keycloak commonly stores roles under `realm_access.roles` and/or `resource_access[client].roles`.

**Update** `mozaiks-core/runtime/ai/core/ai_runtime/auth/jwt_validator.py`:
- If the configured roles claim is missing or not a list, fallback to:
  - `realm_access.roles` (list)
  - `resource_access.*.roles` (flatten lists)

3) Delete the “old auth modes” (local auth, token exchange, internal API key)

We want one solution: Keycloak JWT validation.

**Rewrite** these files to become thin wrappers around `core.ai_runtime.auth.dependencies` and remove old modes:
- `mozaiks-core/runtime/ai/security/authentication.py`
- `mozaiks-core/runtime/ai/security/auth.py`

Rules for the rewrite:
- No `MOZAIKS_AUTH_MODE`.
- No HS256 local tokens.
- No `/api/auth/token`, `/api/auth/register`, `/api/auth/token-exchange`.
- No `X-Internal-API-Key`.

Keep only what the app needs:
- `get_current_user` equivalent that validates bearer JWT via the canonical validator.
- Optional helper(s) for admin/internal authorization based on **roles**.

4) Update the handful of Core route imports that still point at the legacy auth layer

These files currently import `security.authentication` and must be updated to use the canonical dependency layer:
- `mozaiks-core/runtime/ai/core/routes/ai.py`
- `mozaiks-core/runtime/ai/core/routes/notifications.py`
- `mozaiks-core/runtime/ai/core/routes/notifications_admin.py`
- `mozaiks-core/runtime/ai/core/routes/analytics.py`
- `mozaiks-core/runtime/ai/core/routes/status.py`
- `mozaiks-core/runtime/ai/core/routes/events.py`
- `mozaiks-core/runtime/ai/core/routes/app_metadata.py`
- `mozaiks-core/runtime/ai/core/routes/subscription_sync.py` (replace internal API key auth with role-based auth)
- `mozaiks-core/runtime/ai/app/routes/mozaiks.py`
- `mozaiks-core/runtime/ai/core/director.py`

5) Plugin-host alignment (optional but recommended so you don’t chase bugs later)

`mozaiks-core/runtime/plugin-host/auth.py` currently validates JWTs via JWKS with env vars like `JWT_ISSUER`, `JWKS_URL`, `JWT_AUDIENCE`.

**Change plugin-host to use the same env vars as Core runtime**:
- read `AUTH_ISSUER`, `AUTH_JWKS_URL`, `AUTH_AUDIENCE`
- (optional) also accept `MOZAIKS_OIDC_DISCOVERY_URL` if you want discovery there too

After this Phase 2, there should be exactly **one** place to debug JWT validation: `core.ai_runtime.auth.*`.

### Phase 3 — Platform auth: validate Keycloak JWTs (remove Supabase JWT auth)

Platform already has a single “right way” to do auth:
- `mozaiks-platform/src/BuildingBlocks/Mozaiks.Auth/MozaiksAuthExtensions.cs` (`builder.AddMozaiksAuth()`)

This phase is about **removing every Supabase JWT validation path** and replacing it with **Keycloak OIDC discovery** (Authority/Audience).

#### Non-negotiable rules (Platform)

1) Do not edit build outputs
- Ignore any hits under `**/bin/**` and `**/obj/**`.

2) No Supabase JWTs anywhere
- Delete/replace code that reads `SUPABASE_*` env vars for JWT validation.
- Delete/replace code that uses `Supabase:JwtSecret` HS256 validation.
- Delete/replace any `IssuerSigningKeyResolver` JWKS fetch that points at Supabase.

3) All services use the same config keys
- `Jwt:Authority = http://localhost:8080/realms/mozaiks`
- `Jwt:Audience = <your-client-id-or-audience>`
- `Jwt:RequireHttpsMetadata = false` (local dev)

#### Phase 3A — Replace Supabase JWT auth in these exact entrypoints

1) API Gateway: `MozaiksRestApi`
- File to change: `C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform\src\ApiGateways\MozaiksRestApi\Program.cs`
- Delete the entire Supabase JWT block (currently lines 28–107):
  - `SUPABASE_URL`, `SUPABASE_JWKS_URL`, `SUPABASE_ISSUER`, `SUPABASE_AUDIENCE`
  - manual JWKS caching + `IssuerSigningKeyResolver`
- Replace it with:
  - `using Mozaiks.Auth;`
  - `builder.AddMozaiksAuth();`
- Keep the existing `app.UseAuthentication()` / `app.UseAuthorization()` wiring.
- Optional cleanup (recommended for “no Supabase” clarity): change the exception text at line ~114 so it no longer says “Supabase Postgres”.

2) Service: `MozDiscoveryService.Api`
- File to change: `C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform\src\Services\Discovery\MozDiscovery.API\MozDiscoveryService.Api\Program.cs`
- Delete the entire Supabase JWT auth block (currently lines 69–103):
  - the `Supabase:ProjectUrl` + `Supabase:JwtSecret` required check
  - HS256 validation via `IssuerSigningKey = new SymmetricSecurityKey(...)`
- Replace it with:
  - `using Mozaiks.Auth;`
  - `builder.AddMozaiksAuth();`
- Keep `builder.Services.AddAuthorization();` if already present (it is included by `AddMozaiksAuth` anyway).

3) Service: `Payment.API`
- File to change: `C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform\src\Services\Payment\Payment.API\Program.cs`
- Delete the entire Supabase JWT auth block:
  - configuration variables (currently lines 30–42)
  - `AddAuthentication().AddJwtBearer(...)` with `IssuerSigningKeyResolver` (currently lines 88–107)
- Replace it with:
  - `using Mozaiks.Auth;`
  - `builder.AddMozaiksAuth();`

#### Phase 3B — Remove Supabase JWT configuration from appsettings

For the services above, remove any of these keys if they exist:
- Env-var style: `SUPABASE_URL`, `SUPABASE_JWKS_URL`, `SUPABASE_ISSUER`, `SUPABASE_AUDIENCE`
- JSON style: `Supabase:Url`, `Supabase:JwksUrl`, `Supabase:Issuer`, `Supabase:Audience`, `Supabase:JwtSecret`, `Supabase:JwtIssuer`, `Supabase:JwtAudience`

Add these keys instead (service-local `appsettings.Development.json` is fine):
```json
{
  "Jwt": {
    "Authority": "http://localhost:8080/realms/mozaiks",
    "Audience": "<your-client-id-or-audience>",
    "RequireHttpsMetadata": false
  }
}
```

#### Phase 3C — Verification commands (Platform)

Run these from the Platform repo root:
```powershell
$root = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform"

# No Supabase JWT auth remains (ignore bin/obj)
Select-String -Path "$root\src\**\*.cs" -Pattern "SUPABASE_URL|SUPABASE_JWKS_URL|SUPABASE_ISSUER|SUPABASE_AUDIENCE|Supabase:JwtSecret|IssuerSigningKeyResolver" -CaseSensitive:$false |
  Where-Object { $_.Path -notmatch "\\\\(bin|obj)\\\\" }

# Keycloak OIDC config is present
Select-String -Path "$root\src\**\*.json" -Pattern "\"Jwt\"\s*:\s*\{" -CaseSensitive:$false |
  Where-Object { $_.Path -notmatch "\\\\(bin|obj)\\\\" }
```

Then build:
```powershell
cd "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform"
dotnet build
```

### Phase 4 — Frontend auth: one login flow (Keycloak) + aesthetics

This phase is about making the frontend do **one** thing:
- redirect-based OIDC login to Keycloak
- send `Authorization: Bearer <access_token>` to Core + Platform APIs

#### Phase 4A — Canonical shell env vars (exact keys + local-dev values)

Edit: `mozaiks-core/runtime/packages/shell/.env.example`

1) Remove / stop using these (canonical plan has no “modes” and no token exchange):
- `MOZAIKS_AUTH_MODE`
- `VITE_MOZAIKS_AUTH_MODE`
- `VITE_MOZAIKS_TOKEN_EXCHANGE`
- `MOZAIKS_TOKEN_EXCHANGE`
- all `MOZAIKS_*` JWKS/issuer/audience keys in the shell env file

2) Use ONLY these frontend keys:
- `VITE_AUTH_AUTHORITY=http://localhost:8080/realms/mozaiks`
- `VITE_AUTH_CLIENT_ID=mozaiks-shell`
- `VITE_AUTH_REDIRECT_URI=http://localhost:5173/auth/callback`
- `VITE_AUTH_POST_LOGOUT_REDIRECT_URI=http://localhost:5173/`
- `VITE_AUTH_SCOPE=openid profile email`

3) Ensure the shell points at Core runtime (do not guess env var names):
- Use the keys already present in `mozaiks-core/docker-compose.yml` for the shell container (`REACT_APP_API_BASE_URL`, `REACT_APP_WS_URL`), and point them at the Core runtime.

Then copy:
- `mozaiks-core/runtime/packages/shell/.env.example` → `mozaiks-core/runtime/packages/shell/.env`

#### Phase 4B — Hard requirement check

After Phase 4, the frontend must:
1) Successfully redirect to Keycloak login
2) Return to `/auth/callback`
3) Call APIs with `Authorization: Bearer <token>`

### Phase 5 — AppGenerator: add `AssetsGenerator` to produce app branding + Keycloak theme assets

You want the app generation workflow to produce *all assets necessary* for the app, including Keycloak aesthetics.

#### Important constraint (so we don’t build something that can’t work)

The current AppGenerator bundler tool only writes files from structured `code_files` into a safe output folder.
It will not (and should not) allow `code_files` to escape into arbitrary paths like the Core repo.

Therefore AssetsGenerator must do **two** things:
1) Output app assets into the generated app bundle (via `code_files`)
2) Optionally mirror Keycloak theme assets into the Core repo via an **explicit allowlisted copy** (not via `code_files`), gated by an env var pointing at the Core root.

#### Phase 5A — Where to implement AssetsGenerator (exact files)

The AppGenerator workflow is in the Platform repo:
- `C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform\ai-models\workflows\AppGenerator`

Add tool implementation:
- Create: `...\tools\assets_generator.py`

Register the tool:
- Edit: `...\tools.yaml`
- Add a new entry (match existing schema):
  - `agent: ConfigMiddlewareAgent` (or `UtilitiesAgent`)
  - `file: assets_generator.py`
  - `function: generate_assets`
  - `tool_type: Agent_Tool`
  - `auto_invoke: false`

#### Phase 5B — AssetsGenerator outputs (minimum, deterministic)

1) App UI assets (via `code_files`, fixed names)
- `frontend/public/logo.svg`
- `frontend/public/favicon.ico`
- `frontend/public/manifest.json`
- `frontend/public/social-card.png`
- `frontend/public/app-bg.png`

2) Keycloak theme overrides (mirrored into Core, fixed names)

Core target paths:
- `mozaiks-core/runtime/ai/keycloak/theme/mozaiks/login/resources/img/logo.svg`
- `mozaiks-core/runtime/ai/keycloak/theme/mozaiks/login/resources/img/bg.png`

Mirror rule:
- Only mirror if:
  - `MOZAIKS_CORE_ROOT=C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-core`
- Only write to those **two allowlisted files** under `MOZAIKS_CORE_ROOT`.
- No other writes outside the generated app bundle.

#### Phase 5C — Deterministic inputs

Inputs:
- app name
- primary color
- secondary color
- typography
- icon style

Outputs:
- fixed filenames above
- no random naming

### Phase 6 — Delete old repos/duplicates (no backward compatibility)

**Claude tasks:**
1) Core: delete the legacy issuer
- Ensure `mozaiks-core/backend/src/Identity.API` is deleted (Phase 2C.1).
- Ensure `identity-api` is removed from `mozaiks-core/docker-compose.yml` (Phase 1A).

2) Core: remove references to deleted identity issuer
- In `mozaiks-core/docker-compose.yml`, remove:
  - `JWKS_URL=http://identity-api:8080/.well-known/jwks.json`
  - all `depends_on: identity-api`

3) Docs: remove conflicts

Some repo docs currently state Keycloak is not used for platform auth.
That conflicts with this canonical plan.

Update or clearly mark as legacy:
- `mozaiks-core/runtime/ai/keycloak/README.md`

4) If `control-plane` exists under the root
- Archive or delete it after the backup.

5) Platform: no other issuers
- After Phase 3 searches pass, there should be no Supabase JWT validation and no other IdP assumptions.

### Phase 7 — Verification (must pass)

```powershell
$root = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code"

Write-Host "\n== Core: Identity.API deleted =="
Test-Path "$root\mozaiks-core\backend\src\Identity.API"  # expect: False

Write-Host "\n== Core: Keycloak theme assets exist =="
Test-Path "$root\mozaiks-core\runtime\ai\keycloak\theme\mozaiks\login\resources\img\logo.svg"  # expect: True
Test-Path "$root\mozaiks-core\runtime\ai\keycloak\theme\mozaiks\login\resources\img\bg.png"    # expect: True

Write-Host "\n== Platform builds =="
cd "$root\mozaiks-platform"
dotnet build
```

Add these checks:
```powershell
$root = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code"

Write-Host "\n== Keycloak OIDC discovery responds =="
curl.exe -s "http://localhost:8080/realms/mozaiks/.well-known/openid-configuration" | Out-String

Write-Host "\n== Keycloak JWKS responds =="
curl.exe -s "http://localhost:8080/realms/mozaiks/protocol/openid-connect/certs" | Out-String

Write-Host "\n== Core compose has no identity-api =="
Select-String -Path "$root\mozaiks-core\docker-compose.yml" -Pattern "identity-api" -CaseSensitive:$false
```

---

## Appendix

Removed. This plan is intentionally **canonical-only** (no backwards compatibility, no legacy execution paths).

---

## Phase 1: Backup (REQUIRED FIRST)

### Step 1.1: Create backups before any destructive operations

```powershell
$timestamp = Get-Date -Format "yyyy-MM-dd-HHmm"
$backupRoot = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\BACKUP-AUTH-MIGRATION-$timestamp"

# Create backup directory
New-Item -ItemType Directory -Path $backupRoot -Force

# Backup Core's Identity.API (before deletion)
Copy-Item -Recurse `
  "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-core\backend\src\Identity.API" `
  "$backupRoot\core-Identity.API-backup"

# Backup control-plane AuthServer (source of truth)
Copy-Item -Recurse `
  "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\control-plane\src\Services\AuthServer" `
  "$backupRoot\control-plane-AuthServer-backup"

# Backup Platform's current state
Copy-Item -Recurse `
  "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform\src\Services" `
  "$backupRoot\platform-Services-backup"

Write-Host "✅ Backups created at: $backupRoot"
```

---

## Phase 2: Clean Up mozaiks-core

### Step 2.1: Delete Identity.API from Core

**Reason:** Core should NOT have a full identity service. It violates the open-source/self-hostable principle.

```powershell
# DELETE the entire Identity.API folder from Core
Remove-Item -Recurse -Force `
  "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-core\backend\src\Identity.API"

Write-Host "✅ Deleted mozaiks-core/backend/src/Identity.API"
```

### Step 2.2: Update Core's solution file (if it references Identity.API)

```powershell
$slnPath = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-core\MozaiksCore.sln"
$content = Get-Content $slnPath -Raw

# Remove any references to Identity.API project
$content = $content -replace '(?ms)Project\([^)]+\)\s*=\s*"Identity\.API".*?EndProject\r?\n', ''

Set-Content $slnPath $content
Write-Host "✅ Updated MozaiksCore.sln"
```

### Step 2.3: Verify Core's existing auth modules are sufficient

**Check these files exist in Core (Python runtime):**

```
mozaiks-core/runtime/ai/core/
├── auth/
│   ├── __init__.py
│   ├── api_keys.py      # Simple API key validation
│   └── jwt_validator.py # Generic OIDC JWT validation (if exists)
├── billing/
│   ├── entitlements.py  # Already implemented
│   └── ...
```

**If `jwt_validator.py` doesn't exist, CREATE IT:**

```python
# File: mozaiks-core/runtime/ai/core/auth/jwt_validator.py
"""
Generic OIDC JWT Validator for MozaiksCore.

Works with ANY OIDC-compliant provider:
- Keycloak (Platform default)
- Auth0
- Okta
- Azure AD
- Google Identity

Self-hosters configure their own IdP via environment variables.
"""

import os
from typing import Optional, Dict, Any
from dataclasses import dataclass
import httpx
import jwt
from jwt import PyJWKClient
import logging

logger = logging.getLogger(__name__)


@dataclass
class TokenClaims:
    """Extracted claims from a validated JWT."""
    sub: str  # Subject (user ID)
    app_id: Optional[str] = None
    tenant_id: Optional[str] = None
    roles: list[str] = None
    exp: int = 0
    iat: int = 0
    raw_claims: Dict[str, Any] = None

    def __post_init__(self):
        if self.roles is None:
            self.roles = []
        if self.raw_claims is None:
            self.raw_claims = {}


class JWTValidatorError(Exception):
    """Base exception for JWT validation errors."""
    pass


class JWTValidator:
    """
    Generic OIDC JWT Validator.
    
    Configuration via environment variables:
    - MOZAIKS_OIDC_ISSUER: Token issuer URL
    - MOZAIKS_OIDC_JWKS_URL: JWKS endpoint (auto-derived if not set)
    - MOZAIKS_OIDC_AUDIENCE: Expected audience claim
    - MOZAIKS_OIDC_CLAIM_APP_ID: Claim name for app_id (default: "azp")
    - MOZAIKS_OIDC_CLAIM_TENANT_ID: Claim name for tenant_id (default: "tenant_id")
    """

    def __init__(
        self,
        issuer: Optional[str] = None,
        jwks_url: Optional[str] = None,
        audience: Optional[str] = None,
    ):
        self.issuer = issuer or os.getenv("MOZAIKS_OIDC_ISSUER")
        self.audience = audience or os.getenv("MOZAIKS_OIDC_AUDIENCE")
        
        # Derive JWKS URL from issuer if not provided
        jwks = jwks_url or os.getenv("MOZAIKS_OIDC_JWKS_URL")
        if not jwks and self.issuer:
            # Standard OIDC well-known endpoint
            jwks = f"{self.issuer.rstrip('/')}/.well-known/jwks.json"
        self.jwks_url = jwks
        
        # Claim mapping (configurable for different IdPs)
        self.claim_app_id = os.getenv("MOZAIKS_OIDC_CLAIM_APP_ID", "azp")
        self.claim_tenant_id = os.getenv("MOZAIKS_OIDC_CLAIM_TENANT_ID", "tenant_id")
        
        # JWKS client (lazy loaded)
        self._jwks_client: Optional[PyJWKClient] = None
        
        # Disabled mode check
        self._disabled = not self.issuer
        if self._disabled:
            logger.warning("JWT validation disabled: MOZAIKS_OIDC_ISSUER not configured")

    @property
    def jwks_client(self) -> PyJWKClient:
        if self._jwks_client is None:
            if not self.jwks_url:
                raise JWTValidatorError("JWKS URL not configured")
            self._jwks_client = PyJWKClient(self.jwks_url)
        return self._jwks_client

    def validate(self, token: str) -> TokenClaims:
        """
        Validate a JWT and extract claims.
        
        Args:
            token: The JWT string (without "Bearer " prefix)
            
        Returns:
            TokenClaims with extracted and validated claims
            
        Raises:
            JWTValidatorError: If validation fails
        """
        if self._disabled:
            raise JWTValidatorError("JWT validation not configured")
        
        try:
            # Get signing key from JWKS
            signing_key = self.jwks_client.get_signing_key_from_jwt(token)
            
            # Decode and validate
            options = {
                "verify_signature": True,
                "verify_exp": True,
                "verify_iat": True,
                "require": ["exp", "iat", "sub"],
            }
            
            # Add issuer validation if configured
            if self.issuer:
                options["verify_iss"] = True
            
            # Add audience validation if configured  
            if self.audience:
                options["verify_aud"] = True
            
            payload = jwt.decode(
                token,
                signing_key.key,
                algorithms=["RS256", "ES256"],
                issuer=self.issuer,
                audience=self.audience,
                options=options,
            )
            
            # Extract claims with configurable mapping
            return TokenClaims(
                sub=payload.get("sub"),
                app_id=payload.get(self.claim_app_id),
                tenant_id=payload.get(self.claim_tenant_id),
                roles=payload.get("roles", []),
                exp=payload.get("exp", 0),
                iat=payload.get("iat", 0),
                raw_claims=payload,
            )
            
        except jwt.ExpiredSignatureError:
            raise JWTValidatorError("Token expired")
        except jwt.InvalidAudienceError:
            raise JWTValidatorError("Invalid audience")
        except jwt.InvalidIssuerError:
            raise JWTValidatorError("Invalid issuer")
        except jwt.InvalidSignatureError:
            raise JWTValidatorError("Invalid signature")
        except Exception as e:
            logger.error(f"JWT validation error: {e}")
            raise JWTValidatorError(f"Token validation failed: {e}")

    def is_configured(self) -> bool:
        """Check if JWT validation is properly configured."""
        return not self._disabled and self.jwks_url is not None


# Singleton instance
_validator: Optional[JWTValidator] = None


def get_jwt_validator() -> JWTValidator:
    """Get the global JWT validator instance."""
    global _validator
    if _validator is None:
        _validator = JWTValidator()
    return _validator


def validate_token(token: str) -> TokenClaims:
    """Convenience function to validate a token."""
    return get_jwt_validator().validate(token)
```

### Step 2.4: Update Core's auth __init__.py

```python
# File: mozaiks-core/runtime/ai/core/auth/__init__.py
"""
MozaiksCore Authentication Module.

Supports multiple auth modes:
1. OIDC JWT validation (for Platform or self-hosted IdP)
2. API key validation (for simple self-hosted setups)
3. No auth (for local development)

Configuration via environment variables:
- MOZAIKS_AUTH_MODE: "oidc", "api_key", or "none"
- MOZAIKS_OIDC_*: OIDC configuration (see jwt_validator.py)
- MOZAIKS_ALLOWED_SERVICE_KEYS: Comma-separated API keys
"""

import os
from typing import Optional
from .api_keys import validate_api_key, APIKeyAuth
from .jwt_validator import (
    JWTValidator,
    TokenClaims,
    JWTValidatorError,
    get_jwt_validator,
    validate_token,
)

__all__ = [
    "validate_api_key",
    "APIKeyAuth",
    "JWTValidator",
    "TokenClaims", 
    "JWTValidatorError",
    "get_jwt_validator",
    "validate_token",
    "get_auth_mode",
]


def get_auth_mode() -> str:
    """Get the configured authentication mode."""
    return os.getenv("MOZAIKS_AUTH_MODE", "api_key").lower()
```

---

## Phase 3: Migrate AuthServer to mozaiks-platform

### Step 3.1: Create Identity service directory in Platform

```powershell
$targetDir = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform\src\Services\Identity"
New-Item -ItemType Directory -Path $targetDir -Force
Write-Host "✅ Created $targetDir"
```

### Step 3.2: Copy AuthServer from control-plane to Platform

```powershell
$source = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\control-plane\src\Services\AuthServer\*"
$target = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform\src\Services\Identity"

Copy-Item -Recurse $source $target
Write-Host "✅ Copied AuthServer to Platform/Identity"
```

### Step 3.3: Rename project files

```powershell
$identityDir = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform\src\Services\Identity"

# Find and rename AuthServer.Api.csproj to Identity.API.csproj
$oldCsproj = Get-ChildItem -Path $identityDir -Recurse -Filter "AuthServer.Api.csproj" | Select-Object -First 1
if ($oldCsproj) {
    $newPath = Join-Path $oldCsproj.DirectoryName "Identity.API.csproj"
    Rename-Item $oldCsproj.FullName $newPath
    Write-Host "✅ Renamed csproj to Identity.API.csproj"
}

# Update namespace references in all .cs files
Get-ChildItem -Path $identityDir -Recurse -Filter "*.cs" | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $content = $content -replace 'namespace AuthServer\.Api', 'namespace Identity.API'
    $content = $content -replace 'using AuthServer\.Api', 'using Identity.API'
    Set-Content $_.FullName $content
}
Write-Host "✅ Updated namespaces in .cs files"
```

### Step 3.4: Update Platform's solution file

```powershell
$slnPath = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform\MozaiksPlatform.sln"

# Add Identity.API project reference (manually or via dotnet sln add)
$csprojPath = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform\src\Services\Identity\Identity.API\Identity.API.csproj"

if (Test-Path $csprojPath) {
    dotnet sln $slnPath add $csprojPath
    Write-Host "✅ Added Identity.API to Platform solution"
} else {
    Write-Host "⚠️ Could not find Identity.API.csproj - manual addition required"
}
```

### Step 3.5: Add Identity service to Platform's docker-compose.yml

Add this service definition to `mozaiks-platform/docker-compose.yml`:

```yaml
  # ============================================
  # IDENTITY SERVICE (Keycloak Management)
  # ============================================
  identity-api:
    build:
      context: ./src/Services/Identity/Identity.API
      dockerfile: Dockerfile
    ports:
      - "8001:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - MongoDbSettings__ConnectionString=mongodb://mongodb:27017
      - MongoDbSettings__DatabaseName=MozaiksIdentity
      - Keycloak__Authority=http://keycloak:8080/realms/mozaiks
      - Keycloak__AdminUrl=http://keycloak:8080/admin/realms/mozaiks
      - Jwt__Authority=http://keycloak:8080/realms/mozaiks
    depends_on:
      - mongodb
      - keycloak
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  # ============================================
  # KEYCLOAK (Identity Provider)
  # ============================================
  keycloak:
    image: quay.io/keycloak/keycloak:23.0
    ports:
      - "8080:8080"
    environment:
      - KEYCLOAK_ADMIN=admin
      - KEYCLOAK_ADMIN_PASSWORD=admin
      - KC_DB=postgres
      - KC_DB_URL=jdbc:postgresql://postgres:5432/keycloak
      - KC_DB_USERNAME=keycloak
      - KC_DB_PASSWORD=keycloak
    command: start-dev
    depends_on:
      - postgres
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 5

  postgres:
    image: postgres:15
    ports:
      - "5432:5432"
    environment:
      - POSTGRES_DB=keycloak
      - POSTGRES_USER=keycloak
      - POSTGRES_PASSWORD=keycloak
    volumes:
      - postgres_data:/var/lib/postgresql/data
```

Add to volumes section:
```yaml
volumes:
  mongodb_data:
  rabbitmq_data:
  redis_data:
  postgres_data:  # ADD THIS
```

---

## Phase 4: Update Cross-Service References

### Step 4.1: Update Platform services to reference Identity

Services that need identity/auth should depend on `identity-api`:

```yaml
# In docker-compose.yml, update payment-api
payment-api:
  # ... existing config ...
  environment:
    # ... existing env vars ...
    - Jwt__Authority=http://identity-api:8080  # Or keycloak directly
  depends_on:
    - mongodb
    - identity-api  # ADD THIS
```

### Step 4.2: Update service-to-service auth config

Create a shared config for services to validate JWTs:

```json
// Platform services appsettings.json pattern
{
  "Jwt": {
    "Authority": "http://keycloak:8080/realms/mozaiks",
    "Audience": "mozaiks-platform"
  }
}
```

---

## Phase 5: Create Self-Hosting Documentation

### Step 5.1: Create self-hosting auth guide in Core

Create file: `mozaiks-core/docs/guides/self-hosting-auth.md`

```markdown
# Self-Hosting Authentication Guide

MozaiksCore supports multiple authentication modes for self-hosters.

## Option 1: Bring Your Own OIDC Provider (Recommended)

Configure Core to validate JWTs from your IdP:

```bash
# .env
MOZAIKS_AUTH_MODE=oidc
MOZAIKS_OIDC_ISSUER=https://your-keycloak.example.com/realms/your-realm
MOZAIKS_OIDC_AUDIENCE=mozaiks-core
```

### Supported Providers
- Keycloak
- Auth0
- Okta
- Azure AD
- Google Identity Platform
- Any OIDC-compliant provider

## Option 2: Simple API Keys

For internal/development use without an IdP:

```bash
# .env
MOZAIKS_AUTH_MODE=api_key
MOZAIKS_ALLOWED_SERVICE_KEYS=sk_your_key_1,sk_your_key_2
```

## Option 3: No Authentication

For local development only (NOT for production):

```bash
# .env
MOZAIKS_AUTH_MODE=none
```

## Configuring Entitlements

See [Self-Hosting Guide](self-hosting.md) for entitlements configuration.
```

---

## Phase 6: Verification Checklist

After completing all phases, verify:

### Core Verification
```powershell
# 1. Identity.API is deleted
Test-Path "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-core\backend\src\Identity.API"
# Expected: False

# 2. Core auth modules exist
Test-Path "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-core\runtime\ai\core\auth\__init__.py"
# Expected: True

Test-Path "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-core\runtime\ai\core\auth\jwt_validator.py"
# Expected: True

# 3. Core solution builds
cd "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-core"
dotnet build MozaiksCore.sln
# Expected: Success (or no .NET projects if fully Python)

# 4. Core Python imports work
cd "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-core\runtime\ai"
python -c "from core.auth import get_auth_mode, JWTValidator; print('✅ Core auth imports OK')"
```

### Platform Verification
```powershell
# 1. Identity service exists
Test-Path "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform\src\Services\Identity"
# Expected: True

# 2. Platform solution builds
cd "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform"
dotnet build MozaiksPlatform.sln
# Expected: Success (may have warnings)

# 3. Docker compose validates
docker-compose config
# Expected: Valid YAML output
```

---

## Rollback Plan

If something goes wrong:

```powershell
$backupRoot = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\BACKUP-AUTH-MIGRATION-*"
$latestBackup = Get-ChildItem $backupRoot | Sort-Object LastWriteTime -Descending | Select-Object -First 1

# Restore Core's Identity.API (if needed)
Copy-Item -Recurse "$($latestBackup.FullName)\core-Identity.API-backup" `
  "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-core\backend\src\Identity.API"

# Restore Platform Services (if needed)
Remove-Item -Recurse "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform\src\Services"
Copy-Item -Recurse "$($latestBackup.FullName)\platform-Services-backup" `
  "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform\src\Services"

Write-Host "✅ Rollback complete from $($latestBackup.Name)"
```

---

## Summary Checklist

| Phase | Task | Status |
|-------|------|--------|
| 1.1 | Create backups | ⬜ |
| 2.1 | Delete Core's Identity.API | ⬜ |
| 2.2 | Update Core's solution file | ⬜ |
| 2.3 | Verify/create Core jwt_validator.py | ⬜ |
| 2.4 | Update Core auth __init__.py | ⬜ |
| 3.1 | Create Platform Identity directory | ⬜ |
| 3.2 | Copy AuthServer to Platform | ⬜ |
| 3.3 | Rename project files | ⬜ |
| 3.4 | Update Platform solution | ⬜ |
| 3.5 | Add to docker-compose | ⬜ |
| 4.1 | Update service dependencies | ⬜ |
| 4.2 | Update auth config | ⬜ |
| 5.1 | Create self-hosting docs | ⬜ |
| 6 | Verification | ⬜ |

---

## Notes for Executing Agent

1. **Execute phases in order** - backups first!
2. **Stop if any step fails** - do not continue blindly
3. **Check file paths** - paths are absolute, verify they exist
4. **Test after each phase** - run verification commands
5. **Commit after each phase** - use git to track changes

```powershell
# Recommended git workflow
git add -A
git commit -m "Phase X: [description]"
```

---

**Created by:** mozaiks-core agent  
**For execution by:** mozaiks-platform agent or manual  
**Estimated time:** 30-60 minutes
