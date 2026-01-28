from __future__ import annotations

import threading
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any

from core.event_bus import event_bus


def _utcnow() -> datetime:
    return datetime.now(tz=timezone.utc)


def _to_iso(dt: datetime | None) -> str | None:
    if dt is None:
        return None
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def _normalize_name(value: str) -> str:
    return (value or "").strip()


def _stable_tags(tags: dict[str, str] | None) -> dict[str, str]:
    if not tags:
        return {}
    out: dict[str, str] = {}
    for key, value in tags.items():
        k = _normalize_name(key)
        v = _normalize_name(value)
        if k and v:
            out[k] = v
    return dict(sorted(out.items()))


def _metric_key(metric: str, tags: dict[str, str]) -> str:
    if not tags:
        return metric
    parts = [metric]
    for k, v in tags.items():
        parts.append(f"{k}={v}")
    return "|".join(parts)


@dataclass
class ModuleStatus:
    name: str
    enabled: bool
    ok: bool
    last_run_utc: datetime | None
    error: str | None
    details: dict[str, Any]
    updated_at_utc: datetime


@dataclass
class ModuleMetric:
    module: str
    metric: str
    tags: dict[str, str]
    value: int
    updated_at_utc: datetime


_lock = threading.Lock()
_module_status: dict[str, ModuleStatus] = {}
_module_metrics: dict[str, dict[str, ModuleMetric]] = {}


def emit_module_status(
    module: str,
    *,
    enabled: bool = True,
    last_run_utc: datetime | None = None,
    error: str | None = None,
    ok: bool | None = None,
    details: dict[str, Any] | None = None,
) -> None:
    """Record a module status signal and publish a best-effort event-bus notification.

    This is intended for operational/admin surfaces, not user KPIs.
    """
    name = _normalize_name(module)
    if not name:
        return

    err = _normalize_name(error) or None
    resolved_ok = bool(ok) if ok is not None else (err is None)
    now = _utcnow()

    record = ModuleStatus(
        name=name,
        enabled=bool(enabled),
        ok=resolved_ok,
        last_run_utc=last_run_utc,
        error=err,
        details=details or {},
        updated_at_utc=now,
    )

    with _lock:
        _module_status[name] = record

    # Event bus payload is explicitly marked as ops/debug.
    event_bus.publish(
        "module_status",
        {
            "category": "ops",
            "severity": "debug",
            "t": _to_iso(now),
            "module": name,
            "enabled": bool(enabled),
            "ok": resolved_ok,
            "lastRunUtc": _to_iso(last_run_utc),
            "error": err,
            "details": details or {},
        },
    )


def inc_module_metric(
    module: str,
    metric: str,
    *,
    value: int = 1,
    tags: dict[str, str] | None = None,
) -> None:
    """Increment an in-memory counter for a module metric and publish an ops/debug event.

    Use this for operational counters (e.g. jobs started, retries, cache hits).
    """
    module_name = _normalize_name(module)
    metric_name = _normalize_name(metric)
    if not (module_name and metric_name):
        return

    delta = int(value)
    normalized_tags = _stable_tags(tags)
    key = _metric_key(metric_name, normalized_tags)
    now = _utcnow()

    with _lock:
        bucket = _module_metrics.setdefault(module_name, {})
        existing = bucket.get(key)
        total = int(existing.value) if existing else 0
        total += delta
        bucket[key] = ModuleMetric(
            module=module_name,
            metric=metric_name,
            tags=normalized_tags,
            value=total,
            updated_at_utc=now,
        )

    event_bus.publish(
        "module_metric",
        {
            "category": "ops",
            "severity": "debug",
            "t": _to_iso(now),
            "module": module_name,
            "metric": metric_name,
            "delta": delta,
            "value": total,
            "tags": normalized_tags,
        },
    )


def snapshot() -> dict[str, Any]:
    """Return a stable, dashboard-friendly snapshot of module status + counters."""
    now = _utcnow()

    with _lock:
        statuses = list(_module_status.values())
        metrics_by_module = {k: dict(v) for k, v in _module_metrics.items()}

    modules = []
    for status in sorted(statuses, key=lambda s: s.name):
        metrics = []
        for metric in sorted(metrics_by_module.get(status.name, {}).values(), key=lambda m: (m.metric, _metric_key(m.metric, m.tags))):
            metrics.append(
                {
                    "metric": metric.metric,
                    "value": int(metric.value),
                    "tags": metric.tags,
                    "updatedAtUtc": _to_iso(metric.updated_at_utc),
                }
            )

        modules.append(
            {
                "name": status.name,
                "enabled": bool(status.enabled),
                "ok": bool(status.ok),
                "lastRunUtc": _to_iso(status.last_run_utc),
                "error": status.error,
                "details": status.details,
                "updatedAtUtc": _to_iso(status.updated_at_utc),
                "metrics": metrics,
            }
        )

    return {
        "schemaVersion": 1,
        "generatedAtUtc": _to_iso(now),
        "modules": modules,
    }

