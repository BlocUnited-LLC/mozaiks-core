from __future__ import annotations

import asyncio
import logging
import os
import uuid
from datetime import datetime, timezone
from typing import Any

from bson import ObjectId

from core.analytics.kpi_service import kpi_service
from core.analytics.raw_events import user_events_collection
from core.config.settings import settings
from core.insights.client import InsightsClient, InsightsClientConfig, InsightsRequestError
from core.insights.health import record_failure, record_heartbeat, record_success
from core.insights.payloads import (
    KPIValue,
    build_sdk_heartbeat_event,
    build_events_payload,
    build_kpi_payload,
    floor_to_minute,
    lookback_object_id_time,
    stable_user_event_id,
    to_iso_z,
    utcnow,
)
from core.insights.state import get_checkpoint, init_insights_state_indexes, save_checkpoint

logger = logging.getLogger("mozaiks_core.insights.pusher")

_EVENT_TYPES = ("UserSignedUp", "UserActive")


def _app_id() -> str:
    return (settings.mozaiks_app_id or os.getenv("MOZAIKS_APP_ID") or "unknown-app").strip()


def _env_name() -> str:
    return (settings.env or "development").strip().lower()


def _object_id_from_datetime(dt: datetime) -> ObjectId:
    if dt.tzinfo is not None:
        dt = dt.astimezone(timezone.utc).replace(tzinfo=None)
    return ObjectId.from_datetime(dt)


def _kpi_values_from_dashboard_summary(summary: dict[str, Any]) -> list[KPIValue]:
    engagement: dict[str, Any] = summary.get("engagement") or {}
    trending: dict[str, Any] = engagement.get("trending") or {}
    retention: dict[str, Any] = engagement.get("retention") or {}

    out: list[KPIValue] = []

    def add(metric: str, value: Any, unit: str) -> None:
        if value is None:
            return
        if unit == "count":
            try:
                value = int(value)
            except Exception:
                return
        else:
            try:
                value = float(value)
            except Exception:
                return
        out.append(KPIValue(metric=metric, value=value, unit=unit))

    add("total_users", engagement.get("total_users"), "count")
    add("dau", engagement.get("dau"), "count")
    add("mau", engagement.get("mau"), "count")
    add("active_users_7d", engagement.get("active_users_7d"), "count")
    add("new_users_7d", engagement.get("new_users_7d"), "count")
    add("stickiness_dau_mau", engagement.get("stickiness_dau_mau"), "ratio")

    add("active_users_7d_trend_pct", trending.get("active_users_7d_trend_pct"), "ratio")
    add("new_users_7d_trend_pct", trending.get("new_users_7d_trend_pct"), "ratio")

    add("retention_7d", retention.get("retention_7d"), "ratio")
    add("retention_30d", retention.get("retention_30d"), "ratio")
    add("churn_30d", retention.get("churn_30d"), "ratio")

    return out


def _event_message(event_type: str) -> str:
    if event_type == "UserSignedUp":
        return "User signed up"
    if event_type == "UserActive":
        return "User active"
    return event_type


async def _push_kpis(client: InsightsClient, *, app_id: str, env: str, bucket: str, correlation_id: str) -> None:
    summary = await kpi_service.get_dashboard_summary()
    values = _kpi_values_from_dashboard_summary(summary)
    if not values:
        return

    point_time = floor_to_minute(utcnow())
    payload = build_kpi_payload(
        app_id=app_id,
        env=env,
        bucket=bucket,
        point_time=point_time,
        kpis=values,
        tags={"source": "mozaikscore"},
    )
    await client.post_json(
        path="/api/insights/ingest/kpis",
        correlation_id=correlation_id,
        payload=payload,
        retry=True,
        log_context={"kind": "kpis", "appId": app_id, "env": env},
    )


async def _push_heartbeat(
    client: InsightsClient, *, app_id: str, env: str, sdk_version: str, correlation_id: str
) -> None:
    point_time = floor_to_minute(utcnow())
    event = build_sdk_heartbeat_event(app_id=app_id, env=env, sdk_version=sdk_version, point_time=point_time)
    payload = build_events_payload(app_id=app_id, env=env, events=[event])
    await client.post_json(
        path="/api/insights/ingest/events",
        correlation_id=correlation_id,
        payload=payload,
        retry=True,
        log_context={"kind": "heartbeat", "appId": app_id, "env": env},
    )


async def _push_events(
    client: InsightsClient,
    *,
    app_id: str,
    env: str,
    correlation_id: str,
    batch_size: int,
    initial_lookback_s: int,
) -> None:
    kind = "user_events"

    checkpoint = await get_checkpoint(app_id=app_id, env=env, kind=kind)
    last_id = checkpoint.last_object_id if checkpoint else _object_id_from_datetime(lookback_object_id_time(lookback_s=initial_lookback_s))

    while True:
        cursor = (
            user_events_collection.find(
                {
                    "appId": app_id,
                    "type": {"$in": list(_EVENT_TYPES)},
                    "_id": {"$gt": last_id},
                },
                projection={"_id": 1, "type": 1, "userId": 1, "timestamp": 1, "day": 1},
            )
            .sort("_id", 1)
            .limit(int(batch_size))
        )

        docs = await cursor.to_list(length=int(batch_size))
        if not docs:
            return

        events: list[dict[str, Any]] = []
        for doc in docs:
            event_type = str(doc.get("type") or "").strip()
            user_id = str(doc.get("userId") or "").strip()
            day = str(doc.get("day") or "").strip()
            ts = doc.get("timestamp")

            if not (event_type and user_id and day and isinstance(ts, datetime)):
                continue

            event_id = stable_user_event_id(event_type=event_type, user_id=user_id, day=day)
            events.append(
                {
                    "eventId": event_id,
                    "t": to_iso_z(ts),
                    "type": event_type,
                    "severity": "info",
                    "message": _event_message(event_type),
                    "data": {"userId": user_id},
                }
            )

        if not events:
            last_id = docs[-1]["_id"]
            await save_checkpoint(app_id=app_id, env=env, kind=kind, last_object_id=last_id)
            continue

        payload = build_events_payload(app_id=app_id, env=env, events=events)
        await client.post_json(
            path="/api/insights/ingest/events",
            correlation_id=correlation_id,
            payload=payload,
            retry=True,
            log_context={"kind": "events", "appId": app_id, "env": env, "count": len(events)},
        )

        last_id = docs[-1]["_id"]
        await save_checkpoint(app_id=app_id, env=env, kind=kind, last_object_id=last_id)


async def run_insights_push_loop(*, shutdown_event: asyncio.Event | None = None) -> None:
    if not settings.insights_push_enabled:
        logger.info("Insights push disabled (INSIGHTS_PUSH_ENABLED=0)")
        return

    configured_app_id = (settings.mozaiks_app_id or os.getenv("MOZAIKS_APP_ID") or "").strip()
    if not configured_app_id:
        logger.warning("Insights push enabled but MOZAIKS_APP_ID is not configured; skipping telemetry push.")
        return

    app_id = configured_app_id
    env = _env_name()
    base_url = (settings.insights_base_url or "").strip()
    per_app_api_key = (settings.mozaiks_api_key or "").strip()
    fallback_internal_key = (settings.insights_internal_api_key or "").strip()
    sdk_version = (settings.mozaiks_sdk_version or "1.0.0").strip()

    if not base_url:
        logger.warning(
            "Insights push enabled but INSIGHTS_API_BASE_URL/INSIGHTS_BASE_URL is not configured; skipping telemetry push."
        )
        return

    if per_app_api_key:
        key_prefix = per_app_api_key[:12] + "..." if len(per_app_api_key) > 12 else "***"
        logger.info(f"Telemetry configured with per-app API key: {key_prefix}")
    elif fallback_internal_key:
        logger.warning(
            "Telemetry using deprecated INSIGHTS_INTERNAL_API_KEY (or INTERNAL_API_KEY alias). "
            "Configure MOZAIKS_API_KEY for per-app authentication."
        )
    else:
        logger.error("Telemetry enabled but no MOZAIKS_API_KEY or INSIGHTS_INTERNAL_API_KEY configured; skipping push loop.")
        return

    config = InsightsClientConfig(
        base_url=base_url,
        sdk_version=sdk_version,
        mozaiks_app_id=app_id,
        mozaiks_api_key=per_app_api_key or None,
        internal_api_key=(fallback_internal_key or None),
    )
    client = InsightsClient(config)

    await init_insights_state_indexes()

    interval_s = float(settings.insights_push_interval_s)
    bucket = settings.insights_push_bucket
    heartbeat_enabled = bool(settings.insights_heartbeat_enabled)
    heartbeat_interval_s = float(settings.insights_heartbeat_interval_s)
    if heartbeat_interval_s <= 0:
        heartbeat_enabled = False
    heartbeat_interval_s = max(60.0, heartbeat_interval_s) if heartbeat_enabled else 0.0

    batch_size = int(settings.insights_events_batch_size)
    initial_lookback_s = int(settings.insights_events_initial_lookback_s)

    logger.info(
        "Insights push loop started",
        extra={
            "appId": app_id,
            "env": env,
            "baseUrl": base_url,
            "interval_s": interval_s,
            "bucket": bucket,
            "heartbeat_enabled": heartbeat_enabled,
            "heartbeat_interval_s": heartbeat_interval_s,
            "batch_size": batch_size,
        },
    )

    consecutive_failures = 0
    max_backoff_s = 300.0
    last_heartbeat_sent_at: datetime | None = None

    while True:
        if shutdown_event and shutdown_event.is_set():
            return

        correlation_id = str(uuid.uuid4())
        failed = False
        last_failure_status: int | None = None
        last_failure_message: str | None = None
        auth_failed = False

        if heartbeat_enabled:
            now = utcnow()
            should_send_heartbeat = last_heartbeat_sent_at is None or (now - last_heartbeat_sent_at).total_seconds() >= heartbeat_interval_s
            if should_send_heartbeat:
                try:
                    await _push_heartbeat(
                        client,
                        app_id=app_id,
                        env=env,
                        sdk_version=sdk_version,
                        correlation_id=correlation_id,
                    )
                    record_heartbeat(at=now)
                    last_heartbeat_sent_at = now
                except asyncio.CancelledError:
                    raise
                except InsightsRequestError as e:
                    failed = True
                    last_failure_status = getattr(e, "status_code", None)
                    last_failure_message = str(e)
                    logger.warning(f"Insights heartbeat push failed: {e} (status={last_failure_status})")
                    if last_failure_status in {401, 403}:
                        auth_failed = True
                except Exception as e:
                    failed = True
                    last_failure_message = str(e)
                    logger.warning(f"Insights heartbeat push failed: {e}")

        if not auth_failed:
            try:
                await _push_kpis(client, app_id=app_id, env=env, bucket=bucket, correlation_id=correlation_id)
            except asyncio.CancelledError:
                raise
            except InsightsRequestError as e:
                failed = True
                last_failure_status = getattr(e, "status_code", None)
                last_failure_message = str(e)
                logger.warning(f"Insights KPI push failed: {e} (status={last_failure_status})")
                if last_failure_status in {401, 403}:
                    auth_failed = True
            except Exception as e:
                failed = True
                last_failure_message = str(e)
                logger.warning(f"Insights KPI push failed: {e}")

        if not auth_failed:
            try:
                await _push_events(
                    client,
                    app_id=app_id,
                    env=env,
                    correlation_id=correlation_id,
                    batch_size=batch_size,
                    initial_lookback_s=initial_lookback_s,
                )
            except asyncio.CancelledError:
                raise
            except InsightsRequestError as e:
                failed = True
                last_failure_status = getattr(e, "status_code", None)
                last_failure_message = str(e)
                logger.warning(f"Insights event push failed: {e} (status={last_failure_status})")
                if last_failure_status in {401, 403}:
                    auth_failed = True
            except Exception as e:
                failed = True
                last_failure_message = str(e)
                logger.warning(f"Insights event push failed: {e}")

        try:
            if failed:
                record_failure(
                    at=utcnow(),
                    status_code=last_failure_status,
                    message=last_failure_message,
                )
            else:
                record_success(at=utcnow())

            if failed:
                consecutive_failures += 1
                backoff = min(interval_s * (2 ** consecutive_failures), max_backoff_s)
                logger.warning(f"Insights push failed; backing off for {backoff:.0f}s (failures={consecutive_failures})")
                await asyncio.sleep(backoff)
            else:
                consecutive_failures = 0
                await asyncio.sleep(interval_s)
        except asyncio.CancelledError:
            raise
