# Insights Integration (Outbound Push)

MozaiksCore can periodically push **app-level KPIs** and **user activity events** into `Insights.API` using service-to-service auth.

Canonical copy/paste contract: `docs/OpsSignals_KPISafety_Contract.md`.

## Endpoints

MozaiksCore POSTs to:

- `/api/insights/ingest/kpis`
- `/api/insights/ingest/events`

Configure the base URL via `INSIGHTS_API_BASE_URL` (preferred, with `INSIGHTS_BASE_URL` as a deprecated fallback):

- Docker-to-docker example: `http://insights.api:8080`
- Host calling a locally published port: `http://localhost:8060`

## Auth

Authentication priority:

1) **Per-app API key (preferred)**:
   - `X-Mozaiks-App-Id: <MOZAIKS_APP_ID>`
   - `X-Mozaiks-Api-Key: <MOZAIKS_API_KEY>`
   - `X-Mozaiks-Sdk-Version: <MOZAIKS_SDK_VERSION>` (default `1.0.0`)

2) **Legacy internal key (deprecated)**:
   - `X-Internal-Api-Key: <INSIGHTS_INTERNAL_API_KEY>` (or `INTERNAL_API_KEY` fallback)

MozaiksCore always includes `X-Correlation-Id` for tracing.

## Base URL

Preferred:

- `INSIGHTS_API_BASE_URL`

Fallback (deprecated):

- `INSIGHTS_BASE_URL`

## Identity Mapping

- `appId`: `MOZAIKS_APP_ID` (opaque string)
- `env`: `ENV` (e.g. `development`, `production`)
- `X-Correlation-Id`: generated per push cycle (UUIDv4)

## What Gets Pushed

### KPIs

MozaiksCore pushes user/activity KPIs already computed from `user_events`:

- counts: `total_users`, `dau`, `mau`, `active_users_7d`, `new_users_7d`
- ratios: `retention_7d`, `retention_30d`, `churn_30d`, `stickiness_dau_mau`, trend pct

### Events

MozaiksCore pushes discrete events from `user_events`:

- `UserSignedUp`
- `UserActive`

MozaiksCore also emits a lightweight, periodic heartbeat event directly to Insights:

- `SDKHeartbeat` (severity: `debug`)

Heartbeat controls:

- `INSIGHTS_HEARTBEAT_ENABLED=1` (set `0` to disable in dev/test)
- `INSIGHTS_HEARTBEAT_INTERVAL_S=60` (default: `max(60, INSIGHTS_PUSH_INTERVAL_S)`; clamped to >=60s)

Dashboard note: when aggregating `events24h` / `errors24h`, ignore events where `type == "SDKHeartbeat"` (and/or `severity == "debug"`).

If your event pipeline includes ops signals (e.g., `module_status`, `module_metric`), also exclude any events where `category == "ops"` from user-facing KPI totals.

Event idempotency:

- `eventId = "{type}:{userId}:{day}"`

## Configuration

See `.env.example` for all knobs. Core settings:

- `INSIGHTS_PUSH_ENABLED=1`
- `MOZAIKS_APP_ID=...`
- `MOZAIKS_API_KEY=...` (preferred)
- `MOZAIKS_SDK_VERSION=1.0.0`
- `INSIGHTS_API_BASE_URL=http://insights.api:8080`
- `INSIGHTS_INTERNAL_API_KEY=...` (deprecated fallback)
- `INSIGHTS_PUSH_INTERVAL_S=60`
- `INSIGHTS_PUSH_BUCKET=1m`
- `INSIGHTS_HEARTBEAT_ENABLED=1`
- `INSIGHTS_HEARTBEAT_INTERVAL_S=60`

Events batching/checkpointing:

- `INSIGHTS_EVENTS_BATCH_SIZE=250`
- `INSIGHTS_EVENTS_INITIAL_LOOKBACK_S=0` (set >0 to backfill on first boot)

MozaiksCore stores an ingestion cursor in Mongo: `insights_push_state` (per `appId` + `env`).

## Debugging

Admin/internal endpoint (does not expose full secrets):

- `GET /api/telemetry/status`

Example (internal key):

```bash
curl -sS http://localhost:8000/api/telemetry/status \
  -H "X-Internal-Api-Key: $INTERNAL_API_KEY" | jq
```

## Notes (Managed vs Self-Hosted)

- Self-hosted: `UserSignedUp` is recorded on `POST /api/auth/register`; `UserActive` is recorded on login and then at most once/day on subsequent authenticated requests.
- Managed: the first time a platform user is seen in the app, MozaiksCore records `UserSignedUp`; `UserActive` is recorded at most once/day on authenticated requests.

## Multi-Instance Deployments

In a multi-replica deployment, idempotency makes duplicates safe, but it’s typically best to enable `INSIGHTS_PUSH_ENABLED=1` on only **one** replica per app/environment to reduce outbound traffic.
