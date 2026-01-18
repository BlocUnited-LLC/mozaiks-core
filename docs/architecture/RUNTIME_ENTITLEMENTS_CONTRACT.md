# Runtime Entitlements Contract (v1)

This document locks runtime responsibilities for entitlements enforcement.
It reflects current mozaiks-core behavior and does not redesign monetization.

## Responsibilities (Runtime vs Control Plane)

- Runtime never calls Stripe or Payment.API directly.
- Runtime enforces only locally materialized subscription state (Mongo `subscriptions` when `MONETIZATION=1`; SubscriptionStub when `MONETIZATION=0`).
- Control plane pushes subscription state via `POST /api/internal/subscription/sync`.
- Gating is limited to:
  - plugin execution + plugin settings
  - AI capability visibility/launch
  - navigation and settings visibility

## Enforcement Points (Current)

Runtime uses `MONETIZATION` to decide whether subscription gating is active.

- `MONETIZATION=1`: reads subscription data from Mongo `subscriptions` and enforces plan-based access.
- `MONETIZATION=0`: `SubscriptionStub` grants plugin access (plan `unlimited`); navigation/settings are not filtered by plan.
- `GET /api/ai/capabilities` always applies `allowed_plans` (uses `plan=free` when `MONETIZATION=0`).
- `POST /api/ai/launch` enforces `allowed_plans` only when `MONETIZATION=1`.

Routes/services that enforce gating:

- `POST /api/execute/{plugin}`: denies execution when the plugin is disabled or subscription does not unlock the plugin.
  `backend/core/director.py`
- `GET /api/available-plugins`: includes only enabled plugins, then filters by subscription when `MONETIZATION=1`.
  `backend/core/director.py`
- `GET /api/check-plugin-access/{plugin}`: subscription access check.
  `backend/core/director.py`
- `GET /api/navigation`: filters plugin navigation items by access and plugin enabled flag.
  `backend/core/director.py`
- `GET /api/settings-config`: filters plugin notification fields by access (only when `MONETIZATION=1`).
  `backend/core/settings_manager.py`
- `GET|POST /api/plugin-settings/{plugin}`: denies settings access if locked.
  `backend/core/director.py`
- `GET /api/ai/capabilities`: filters by `enabled`, `visibility`/`requires_superadmin`, and `allowed_plans`.
- `POST /api/ai/launch`: enforces `enabled`, `visibility`/`requires_superadmin`, and `allowed_plans` (only when `MONETIZATION=1`).
  `backend/core/routes/ai.py`

Core enforcement service:

- `SubscriptionManager.is_plugin_accessible(...)` reads `subscriptions` and
  applies `plugins_unlocked` from config.
  `backend/core/subscription_manager.py`
- `SubscriptionStub.is_plugin_accessible(...)` always allows access when `MONETIZATION=0`.
  `backend/core/subscription_stub.py`

## Authoritative Config Files

- `backend/core/config/subscription_config.json`
  - plan names
  - `plugins_unlocked` used for plugin access
- `backend/core/config/ai_capabilities.json`
  - `allowed_plans` used for AI capability gating
- `backend/core/config/plugin_registry.json`
  - plugin `enabled` flags
- `backend/core/config/navigation_config.json`
  - plugin navigation entries (filtered by access)
- `backend/core/config/settings_config.json`
  - plugin notification fields (filtered by access)

## Subscription Sync Contract

Control plane pushes subscription state to:

- `POST /api/internal/subscription/sync`
  - Guarded by `X-Internal-API-Key`.
  - Calls `SubscriptionManager.sync_subscription_from_control_plane(...)`.
  - Upserts into Mongo `subscriptions`.
  - Stores optional `app_id` for future app-scoped entitlements.
  - `backend/core/routes/subscription_sync.py`
  - `backend/core/subscription_manager.py`

Required fields for gating:

- `user_id`
- `plan`
- `status`

If sync is missing or stale, runtime enforces the last stored state.
When no record exists, `get_user_subscription` falls back to
`plan=free` and `status=inactive`.

## Not Enforced by Runtime

The runtime does not enforce:

- domains
- hosting tier limits
- email limits
- token hard caps (no usage accounting; only request rate limits exist)

## Mutations in Production

- `sync_subscription_from_control_plane` is the only supported mutation path
  in production.
- `ALLOW_LOCAL_SUBSCRIPTION_WRITES=false` is expected for hosted mode.
  Local mutation endpoints are for dev only and are blocked without internal
  authorization.
  `backend/core/subscription_manager.py`
