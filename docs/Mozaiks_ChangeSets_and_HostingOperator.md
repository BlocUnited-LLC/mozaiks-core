# Mozaiks Change Sets and Hosting Operator

This document describes:

- a **Change Set** as the unit of proposed changes to a MozaiksCore app, and
- a **Hosting Operator** as the policy layer that decides whether a Change Set can be deployed (based on app entitlements and safety checks).

MozaiksCore runs as a single-process, in-application runtime. The operator can tune limits (env vars) and deploy gates, but it does not switch the app into a distributed execution topology.

---

## 1) Change Set (example payload)

```json
{
  "schema_version": "v1",
  "app_id": "app_123",
  "change_set_id": "cs_456",
  "summary": "Add a new plugin and settings screen",
  "requirements": {
    "tenancy_mode": "single_user",
    "realtime_mode": "direct_websockets",
    "jobs_mode": "inline",
    "state_backend": "memory",
    "rate_limit_backend": "memory"
  },
  "checks": {
    "build": "pass",
    "tests": "pass"
  },
  "status": "awaiting_user_approval"
}
```

---

## 2) Requirements schema (what agents must declare)

The agent generator should output requirements with conservative defaults.

### Core capability flags

- `tenancy_mode`: `single_user` | `workspace`
- `realtime_mode`: `direct_websockets`
- `jobs_mode`: `inline`
- `state_backend`: `memory`
- `rate_limit_backend`: `memory`

### Optional resource hints (for quotas and tiering)

- `expected_concurrent_ws_per_app`
- `expected_concurrent_users`
- `expected_background_jobs_per_minute`

These are not strict requirements, but help the operator recommend upgrades or apply safer defaults.

---

## 3) Hosting Operator (deploy gate)

Given:

- current app profile/entitlements, and
- Change Set requirements + checks

Return:

- `allow_deploy: true|false`
- `reason` (human-readable)
- `recommended_upgrade` (if blocked)
- `estimated_changes` (what changes the platform will apply on upgrade)

### Example decisions

- If `checks.tests != pass` → block deploy.
- If expected load is above profile thresholds → block deploy or require upgrade.
- If requirements indicate `tenancy_mode=workspace` → allow, but may recommend an upgrade depending on expected usage.

---

## 4) Upgrade flow (platform-managed)

When a user upgrades:

- update app environment configuration (limits, quotas, feature availability)
- redeploy the app

After upgrade, the previously blocked Change Set can be approved and deployed.

---

## 5) Self-hosted apps (no Mozaiks platform)

Self-hosters won't have:

- the hosted sandbox,
- a hosted operator, or
- an “Agree then deploy” workflow.

But the same structure still helps:

- requirements exist as documentation/metadata
- checks exist as local CI steps
- deploy gates exist as internal policy in their own pipeline
