# Ops Signals + KPI Safety Contract (Canonical)

Date: Dec 28, 2025

This document is the canonical, copy/pasteable contract for:

- **Ops signals** emitted by modules (`module_status`, `module_metric`)
- **SDK heartbeat** emitted by telemetry (`SDKHeartbeat`)
- **KPI safety rules** to ensure user-facing totals are not polluted by ops/debug signals

---

## 1) Event Categories (How to Think About Them)

MozaiksCore produces (or may participate in) three different “classes” of signals:

1) **User KPI events** (user-facing)
   - Examples: `UserSignedUp`, `UserActive`
   - Used for DAU/MAU/retention metrics

2) **Ops/admin signals** (admin-facing only)
   - Examples: `module_status`, `module_metric`
   - Must not count toward user KPI totals

3) **Telemetry heartbeat** (admin-facing connectivity only)
   - Example: `SDKHeartbeat`
   - Used to infer `sdkConnected` / last ping

---

## 2) Canonical Payloads (Exact Examples)

### 2.1 `module_status` (ops signal)

Required keys:
- `category`, `severity`, `t`, `module`, `enabled`, `ok`

Recommended keys:
- `lastRunUtc`, `error`, `details`

```json
{
  "category": "ops",
  "severity": "debug",
  "t": "2025-12-28T12:00:00Z",
  "module": "notes_manager",
  "enabled": true,
  "ok": true,
  "lastRunUtc": "2025-12-28T12:00:00Z",
  "error": null,
  "details": { "info": "optional module-specific fields" }
}
```

### 2.2 `module_metric` (ops signal)

Required keys:
- `category`, `severity`, `t`, `module`, `metric`, `delta`, `value`

Recommended keys:
- `tags`

```json
{
  "category": "ops",
  "severity": "debug",
  "t": "2025-12-28T12:00:00Z",
  "module": "notes_manager",
  "metric": "jobs_started",
  "delta": 1,
  "value": 42,
  "tags": { "queue": "default" }
}
```

### 2.3 `SDKHeartbeat` (telemetry connectivity event)

Required keys:
- `eventId`, `t`, `type`, `severity`

Recommended keys:
- `message`, `data.sdkVersion`

```json
{
  "eventId": "SDKHeartbeat:app_123:production:20251228T1200",
  "t": "2025-12-28T12:00:00Z",
  "type": "SDKHeartbeat",
  "severity": "debug",
  "message": "SDK heartbeat",
  "data": { "sdkVersion": "1.0.0" }
}
```

---

## 3) Heartbeat Controls (Environment Variables)

Heartbeat is used for admin surfaces to infer “SDK connected / last ping”.

Env vars:
- `INSIGHTS_HEARTBEAT_ENABLED=1|0`
- `INSIGHTS_HEARTBEAT_INTERVAL_S=<seconds>`

Defaults / safety:
- Default interval: `max(60, INSIGHTS_PUSH_INTERVAL_S)`
- Minimum clamp: **60 seconds**
- Disable in dev/test:
  - `INSIGHTS_HEARTBEAT_ENABLED=0` (or set `INSIGHTS_HEARTBEAT_INTERVAL_S<=0`)

---

## 4) Canonical KPI Safety Rule (Downstream Aggregators)

If you compute user-facing totals from a **mixed telemetry stream**, apply:

1) Exclude ops/admin signals: `category == "ops"`
2) Exclude debug signals from user-facing totals: `severity == "debug"`
3) Exclude heartbeat from user-facing totals: `type == "SDKHeartbeat"`

This keeps metrics like `events24h`, `errors24h`, `activeUsers24h` clean.

---

## 5) KPI Aggregation Ownership (MozaiksCore)

MozaiksCore’s built-in user KPIs (DAU/MAU/retention/etc) are computed from Mongo `user_events` and only use:
- `UserSignedUp`
- `UserActive`

Ops signals (`module_status`, `module_metric`) are **not written** to `user_events`, so they do not affect MozaiksCore’s DAU/MAU.

---

## 6) Admin Endpoints (Reference)

- `GET /api/status` (admin/internal): module health + counters snapshot
- `GET /api/telemetry/status` (admin/internal): telemetry auth/target + `sdkConnected` + last heartbeat/success/failure

