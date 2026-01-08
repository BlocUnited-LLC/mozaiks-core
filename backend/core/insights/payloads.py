from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from typing import Any


def utcnow() -> datetime:
    return datetime.now(tz=timezone.utc)


def to_iso_z(dt: datetime) -> str:
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def floor_to_minute(dt: datetime) -> datetime:
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    dt = dt.astimezone(timezone.utc)
    return dt.replace(second=0, microsecond=0)


def stable_user_event_id(*, event_type: str, user_id: str, day: str) -> str:
    return f"{event_type}:{user_id}:{day}"


def stable_sdk_heartbeat_event_id(*, app_id: str, env: str, point_time: datetime) -> str:
    point = floor_to_minute(point_time)
    stamp = point.astimezone(timezone.utc).strftime("%Y%m%dT%H%M")
    return f"SDKHeartbeat:{app_id}:{env}:{stamp}"


def build_sdk_heartbeat_event(
    *,
    app_id: str,
    env: str,
    sdk_version: str,
    point_time: datetime,
) -> dict[str, Any]:
    point = floor_to_minute(point_time)
    return {
        "eventId": stable_sdk_heartbeat_event_id(app_id=app_id, env=env, point_time=point),
        "t": to_iso_z(point),
        "type": "SDKHeartbeat",
        "severity": "debug",
        "message": "SDK heartbeat",
        "data": {"sdkVersion": (sdk_version or "").strip() or "unknown"},
    }


@dataclass(frozen=True)
class KPIValue:
    metric: str
    value: float | int
    unit: str


def build_kpi_payload(
    *,
    app_id: str,
    env: str,
    bucket: str,
    point_time: datetime,
    kpis: list[KPIValue],
    tags: dict[str, str] | None = None,
    sent_at: datetime | None = None,
) -> dict[str, Any]:
    sent = sent_at or utcnow()
    t = to_iso_z(point_time)
    return {
        "appId": app_id,
        "env": env,
        "sentAtUtc": to_iso_z(sent),
        "bucket": bucket,
        "points": [{"metric": k.metric, "t": t, "v": k.value, "unit": k.unit} for k in kpis],
        "tags": tags or {},
    }


def build_events_payload(
    *,
    app_id: str,
    env: str,
    events: list[dict[str, Any]],
    sent_at: datetime | None = None,
) -> dict[str, Any]:
    sent = sent_at or utcnow()
    return {
        "appId": app_id,
        "env": env,
        "sentAtUtc": to_iso_z(sent),
        "events": events,
    }


def lookback_object_id_time(*, lookback_s: int, now: datetime | None = None) -> datetime:
    base = now or utcnow()
    return base - timedelta(seconds=int(max(0, lookback_s)))
