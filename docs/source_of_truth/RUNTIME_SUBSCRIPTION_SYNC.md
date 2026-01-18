# Runtime Subscription Sync (v1)

This document describes the current runtime contract for control-plane
subscription sync based on existing mozaiks-core behavior.

## Endpoint

`POST /api/internal/subscription/sync`

### Auth

Requires `X-Internal-API-Key` header. The runtime compares the header
to `INTERNAL_API_KEY` (or settings fallback). No user JWT is accepted.

## Request (v1)

Fields required for gating:

- `user_id` (string)
- `plan` (string)
- `status` (string)

Optional fields (stored but not used for gating):

- `billing_cycle` (string)
- `next_billing_date` (string, ISO)
- `trial_end_date` (string, ISO)
- `stripe_subscription_id` (string)
- `app_id` (string) - stored for future app-scoped entitlements

## Behavior

- Calls `SubscriptionManager.sync_subscription_from_control_plane(...)`
  with `_internal_call=True`.
- Upserts the user subscription record into Mongo `subscriptions`.
- Does not call the control plane per request; runtime enforces the
  last synced state.
- Gating logic remains unchanged: plugin access uses
  `subscription_config.json` + `plugins_unlocked`, and AI capability
  gating uses `ai_capabilities.json` `allowed_plans`.

## Staleness / Missing Sync

- If no subscription record exists, `get_user_subscription` returns
  `{plan: "free", status: "inactive"}` and access is restricted by
  the local plan config.
- If sync is stale, runtime enforces the last stored plan/status.

## Non-Goals

- No Stripe calls or pricing decisions in mozaiks-core.
- No per-request dependency on control plane availability.
