from __future__ import annotations

from datetime import datetime, timezone
from typing import Any


_last_heartbeat_at_utc: datetime | None = None
_last_success_at_utc: datetime | None = None
_last_failure_at_utc: datetime | None = None
_last_failure_status_code: int | None = None
_last_failure_message: str | None = None


def _to_iso(dt: datetime | None) -> str | None:
    if dt is None:
        return None
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def record_heartbeat(*, at: datetime) -> None:
    global _last_heartbeat_at_utc
    _last_heartbeat_at_utc = at.astimezone(timezone.utc) if at.tzinfo else at.replace(tzinfo=timezone.utc)


def record_success(*, at: datetime) -> None:
    global _last_success_at_utc, _last_failure_at_utc, _last_failure_status_code, _last_failure_message
    _last_success_at_utc = at.astimezone(timezone.utc) if at.tzinfo else at.replace(tzinfo=timezone.utc)
    _last_failure_at_utc = None
    _last_failure_status_code = None
    _last_failure_message = None


def record_failure(*, at: datetime, status_code: int | None = None, message: str | None = None) -> None:
    global _last_failure_at_utc, _last_failure_status_code, _last_failure_message
    _last_failure_at_utc = at.astimezone(timezone.utc) if at.tzinfo else at.replace(tzinfo=timezone.utc)
    _last_failure_status_code = int(status_code) if status_code is not None else None
    _last_failure_message = (message or "").strip() or None


def snapshot() -> dict[str, Any]:
    return {
        "lastHeartbeatAtUtc": _to_iso(_last_heartbeat_at_utc),
        "lastSuccessAtUtc": _to_iso(_last_success_at_utc),
        "lastFailureAtUtc": _to_iso(_last_failure_at_utc),
        "lastFailureStatusCode": _last_failure_status_code,
        "lastFailureMessage": _last_failure_message,
    }


def sdk_connected(*, within_s: float, now: datetime | None = None) -> bool:
    if _last_heartbeat_at_utc is None:
        return False
    base = now or datetime.now(tz=timezone.utc)
    if base.tzinfo is None:
        base = base.replace(tzinfo=timezone.utc)
    return (base - _last_heartbeat_at_utc).total_seconds() <= float(within_s)


def push_connected(*, within_s: float, now: datetime | None = None) -> bool:
    if _last_success_at_utc is None:
        return False
    base = now or datetime.now(tz=timezone.utc)
    if base.tzinfo is None:
        base = base.replace(tzinfo=timezone.utc)
    return (base - _last_success_at_utc).total_seconds() <= float(within_s)
