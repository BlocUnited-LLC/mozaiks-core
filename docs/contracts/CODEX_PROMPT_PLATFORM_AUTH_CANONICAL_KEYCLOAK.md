# Codex Prompt — mozaiks-platform Canonical Auth (Keycloak Only, No Legacy)

Use this prompt in VS Code Codex **while opened in the mozaiks-platform repo**.

## Goal (non-negotiable)

Make mozaiks-platform a **pure JWT consumer** that validates **only Keycloak OIDC** tokens via the shared building block **`Mozaiks.Auth`**.

- **No backward compatibility**
- **No deprecated paths kept**
- **No Supabase JWT validation anywhere**
- **No API-key “Bearer token” middleware anywhere**
- **Exactly one auth configuration scheme** across services: `Jwt:{Authority,Audience,RequireHttpsMetadata}`

## Canonical Decision

- **IdP:** Keycloak (OIDC)
- **JWT validation:** OIDC discovery (JWKS via discovery)
- **Authority:** `http://localhost:8080/realms/mozaiks`
- **Audience:** `mozaiks-api` (or whatever the platform uses consistently, but it must be one value)

## Where the canonical implementation lives

Platform must use:
- `src/BuildingBlocks/Mozaiks.Auth/MozaiksAuthExtensions.cs` via `builder.AddMozaiksAuth()`

Do **not** implement custom JWT validation in each service.

## Scope (what to change)

### A) Replace *all* custom/Supabase JWT auth with `AddMozaiksAuth()`

Update each impacted service entrypoint `Program.cs` to:

1) Add:
- `using Mozaiks.Auth;`

2) Replace any `AddAuthentication().AddJwtBearer(...)` blocks that:
- reference Supabase
- manually fetch JWKS via `IssuerSigningKeyResolver`
- validate HS256 (`SymmetricSecurityKey`)

…with:
- `builder.AddMozaiksAuth();`

3) Ensure pipeline includes:
- `app.UseAuthentication();`
- `app.UseAuthorization();`

Targets to check first (based on prior scans):
- `src/Services/Payment/Payment.API/Program.cs`
- `src/ApiGateways/MozaiksRestApi/Program.cs`
- `src/Services/Discovery/MozDiscovery.API/MozDiscoveryService.Api/Program.cs`
- Any other service entrypoints still containing Supabase/JWKS resolver logic

### B) Delete the service-to-service API key middleware pattern (no “Bearer <apiKey>”)

Remove the Payment API middleware that treats `Authorization: Bearer <apiKey>` as a service key.

1) Delete the middleware registration in Payment API:
- Remove `app.UseMiddleware<ServiceKeyAuthMiddleware>();` from `src/Services/Payment/Payment.API/Program.cs`

2) Delete the middleware file entirely if present:
- `src/Services/Payment/Payment.API/Middlewares/ServiceKeyAuthMiddleware.cs`

3) Replace the middleware’s protection model with **JWT authorization**:
- Identify the endpoints it was protecting (e.g. `/api/billing/internal/*` etc.)
- Put them behind **authorization policies/roles** backed by Keycloak

Canonical approach:
- Use an `internal_service` role (or policy) enforced via `[Authorize(Policy = "internal_service")]` or equivalent.
- Service-to-service calls must use **Keycloak client credentials** (service account) to obtain a JWT with `internal_service`.

There must be **zero** code paths where a random string API key is accepted as a bearer token.

### C) Standardize config: only `Jwt:*`

Remove all service configs/env usage like:
- `SUPABASE_URL`
- `SUPABASE_JWKS_URL`
- `SUPABASE_ISSUER`
- `SUPABASE_AUDIENCE`
- `Supabase:JwtSecret`
- any `Supabase:*` values used for JWT validation

Add (or ensure present) in each service’s `appsettings.Development.json` (or shared config):

```json
{
  "Jwt": {
    "Authority": "http://localhost:8080/realms/mozaiks",
    "Audience": "mozaiks-api",
    "RequireHttpsMetadata": false
  }
}
```

### D) Ensure Keycloak roles work with ASP.NET authorization

Make sure role extraction works for Keycloak tokens.

Keycloak commonly puts roles under:
- `realm_access.roles`
- `resource_access.{client}.roles`

If `Mozaiks.Auth` currently assumes a different claim type (e.g. plain `roles`), update **only `Mozaiks.Auth`** (the shared building block) to map Keycloak roles into `ClaimTypes.Role` (or whatever the policies use).

Acceptance criteria:
- A JWT containing Keycloak roles results in `User.IsInRole("internal_service") == true` when appropriate.

## Global “no legacy” search checks

After edits, run searches across platform source (excluding `bin/obj`) and ensure **zero hits** for the following patterns:

- `SUPABASE_URL`
- `SUPABASE_JWKS_URL`
- `SUPABASE_ISSUER`
- `SUPABASE_AUDIENCE`
- `Supabase:JwtSecret`
- `IssuerSigningKeyResolver`
- `SymmetricSecurityKey(` (if it was used for Supabase JWT auth)
- `ServiceKeyAuthMiddleware`
- `AllowedCoreServiceKeys`
- `Mozaiks:AllowedServiceKeys`

If you find any, remove the logic entirely (no wrappers, no fallbacks).

## Build + smoke verification

1) Build:
- `dotnet build`

2) For Payment.API, run locally and confirm:
- `/health` works unauthenticated if intended
- a protected endpoint returns 401 without a JWT
- the same endpoint succeeds with a valid Keycloak JWT

3) Confirm the system never accepts an API key as a bearer token.

## Output required from Codex

When done, produce:
- A concise list of edited files
- A brief explanation of how internal endpoints are now protected
- The exact grep/search commands you ran and their results summary

## Important constraints

- Do not add any alternative IdP support.
- Do not add “temporary” flags like `SKIP_AUTH`.
- Do not keep legacy configs “just in case”. Delete them.
- Keep changes focused and consistent; don’t refactor unrelated code.
