# Operational Signals (Module Status + Counters)

MozaiksCore includes a small **ops signals** helper so backend modules (plugins, schedulers, background jobs) can emit **standard operational signals** that admin dashboards can display.

These signals are explicitly **ops/debug** and must be excluded from user KPI totals (DAU/MAU/etc).

Canonical copy/paste contract: `docs/OpsSignals_KPISafety_Contract.md`.

---

## 1) Recommended Signals

### 1.1 `module_status`

Use this to report:
- whether a module is enabled
- whether it is healthy (`ok`)
- the last time it ran
- the last error (if any)

**Event name:** `module_status`  
**Category:** `ops` (debug-only)

**Payload (recommended):**
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

### 1.2 `module_metric`

Use this for **counters** (incrementing numbers), e.g.:
- `jobs_started`
- `jobs_failed`
- `retries_total`
- `cache_hits`

**Event name:** `module_metric`  
**Category:** `ops` (debug-only)

**Payload (recommended):**
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

---

## 2) How Modules Emit Signals (Backend)

Import the helper:

```python
from core.ops.signals import emit_module_status, inc_module_metric
```

### Example: status + counters from a plugin

```python
emit_module_status("notes_manager", enabled=True, last_run_utc=timestamp, ok=True)
inc_module_metric("notes_manager", "execute_total", value=1)
```

On error:

```python
emit_module_status("notes_manager", enabled=True, last_run_utc=timestamp, ok=False, error=str(err))
inc_module_metric("notes_manager", "execute_errors_total", value=1)
```

---

## 3) `/api/status` Endpoint (Generated Apps)

Generated apps expose a protected status endpoint intended for admin dashboards:

- `GET /api/status`

Auth:
- Admin JWT **or**
- `X-Internal-Api-Key: <INTERNAL_API_KEY>` (service-to-service)

Response:
- `appId`, `env`
- `ops`: a stable snapshot of module status + counters

This endpoint is designed to be polled by Mozaiks Platform for per-app operational dashboards without mixing ops signals into user KPIs.

---

## 4) Dashboard Rules (Important)

When computing user-facing totals like:
- `activeUsers24h`
- `events24h`
- `errors24h`

…exclude ops-only signals by filtering:
- `category != "ops"` (preferred), and/or
- `severity != "debug"` (fallback)

Ops signals are meant to power admin panels, not user growth analytics.

### KPI safety note (MozaiksCore)

MozaiksCore’s built-in user KPIs are derived from the `user_events` collection and **only** consider user/activity events (e.g., `UserSignedUp`, `UserActive`). The ops signals in this doc (`module_status`, `module_metric`) are **not** written to `user_events`, so they do not affect DAU/MAU/etc inside MozaiksCore.

If you aggregate totals from an external event stream (e.g., Insights), apply the filters above so ops/debug signals never inflate user-facing KPI totals.
