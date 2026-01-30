from __future__ import annotations

import logging
import os
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Dict, Optional, Tuple

try:
    from pymongo import ReturnDocument
except Exception:  # pragma: no cover - pymongo not installed in minimal envs
    ReturnDocument = None  # type: ignore

from mozaiks_infra.config.database import token_usage_collection

logger = logging.getLogger("mozaiks_core.billing.token_usage")


@dataclass
class TokenUsageSnapshot:
    app_id: str
    period_key: str
    period_type: str
    used: int
    updated_at: datetime


def _normalize_period(period_type: str) -> str:
    raw = (period_type or "").strip().lower()
    if raw in {"daily", "weekly", "monthly", "lifetime"}:
        return raw
    if raw in {"none", "unlimited"}:
        return "lifetime"
    return "monthly"


def _period_key(period_type: str, now: datetime) -> Tuple[str, datetime]:
    period = _normalize_period(period_type)
    if period == "daily":
        key = now.strftime("%Y-%m-%d")
        start = datetime(now.year, now.month, now.day, tzinfo=timezone.utc)
        return key, start
    if period == "weekly":
        iso_year, iso_week, _ = now.isocalendar()
        key = f"{iso_year}-W{iso_week:02d}"
        # Use Monday as week start for ISO weeks
        start = datetime.fromisocalendar(iso_year, iso_week, 1).replace(tzinfo=timezone.utc)
        return key, start
    if period == "lifetime":
        return "lifetime", datetime(1970, 1, 1, tzinfo=timezone.utc)
    # default monthly
    key = now.strftime("%Y-%m")
    start = datetime(now.year, now.month, 1, tzinfo=timezone.utc)
    return key, start


class TokenUsageStore:
    """Track app-level token usage per period.

    Uses MongoDB when available; falls back to an in-memory store when DB is unavailable.
    """

    def __init__(self) -> None:
        self._memory: Dict[Tuple[str, str], TokenUsageSnapshot] = {}

    async def get_usage(self, app_id: str, period_type: str) -> TokenUsageSnapshot:
        now = datetime.now(timezone.utc)
        period_key, period_start = _period_key(period_type, now)

        if token_usage_collection is None:
            key = (app_id, period_key)
            snap = self._memory.get(key)
            if snap is None:
                snap = TokenUsageSnapshot(
                    app_id=app_id,
                    period_key=period_key,
                    period_type=_normalize_period(period_type),
                    used=0,
                    updated_at=now,
                )
                self._memory[key] = snap
            return snap

        doc = await token_usage_collection.find_one(
            {"app_id": app_id, "period_key": period_key},
            projection={"_id": 0, "app_id": 1, "period_key": 1, "period_type": 1, "used": 1, "updated_at": 1},
        )
        if doc:
            return TokenUsageSnapshot(
                app_id=doc.get("app_id", app_id),
                period_key=doc.get("period_key", period_key),
                period_type=doc.get("period_type", _normalize_period(period_type)),
                used=int(doc.get("used", 0)),
                updated_at=doc.get("updated_at") or now,
            )

        # Initialize empty record on first access
        await token_usage_collection.update_one(
            {"app_id": app_id, "period_key": period_key},
            {
                "$setOnInsert": {
                    "app_id": app_id,
                    "period_key": period_key,
                    "period_type": _normalize_period(period_type),
                    "period_start": period_start,
                    "used": 0,
                    "created_at": now,
                },
                "$set": {"updated_at": now},
            },
            upsert=True,
        )

        return TokenUsageSnapshot(
            app_id=app_id,
            period_key=period_key,
            period_type=_normalize_period(period_type),
            used=0,
            updated_at=now,
        )

    async def increment_usage(self, app_id: str, delta: int, period_type: str) -> TokenUsageSnapshot:
        now = datetime.now(timezone.utc)
        period_key, period_start = _period_key(period_type, now)
        delta = max(0, int(delta or 0))

        if token_usage_collection is None:
            key = (app_id, period_key)
            snap = self._memory.get(key)
            if snap is None:
                snap = TokenUsageSnapshot(
                    app_id=app_id,
                    period_key=period_key,
                    period_type=_normalize_period(period_type),
                    used=0,
                    updated_at=now,
                )
            snap.used += delta
            snap.updated_at = now
            self._memory[key] = snap
            return snap

        update = {
            "$inc": {"used": delta},
            "$set": {"updated_at": now, "period_type": _normalize_period(period_type)},
            "$setOnInsert": {
                "app_id": app_id,
                "period_key": period_key,
                "period_start": period_start,
                "created_at": now,
            },
        }

        if ReturnDocument is not None:
            doc = await token_usage_collection.find_one_and_update(
                {"app_id": app_id, "period_key": period_key},
                update,
                upsert=True,
                return_document=ReturnDocument.AFTER,
                projection={"_id": 0, "app_id": 1, "period_key": 1, "period_type": 1, "used": 1, "updated_at": 1},
            )
            if doc:
                return TokenUsageSnapshot(
                    app_id=doc.get("app_id", app_id),
                    period_key=doc.get("period_key", period_key),
                    period_type=doc.get("period_type", _normalize_period(period_type)),
                    used=int(doc.get("used", 0)),
                    updated_at=doc.get("updated_at") or now,
                )

        await token_usage_collection.update_one(
            {"app_id": app_id, "period_key": period_key},
            update,
            upsert=True,
        )
        doc = await token_usage_collection.find_one(
            {"app_id": app_id, "period_key": period_key},
            projection={"_id": 0, "app_id": 1, "period_key": 1, "period_type": 1, "used": 1, "updated_at": 1},
        )
        if doc:
            return TokenUsageSnapshot(
                app_id=doc.get("app_id", app_id),
                period_key=doc.get("period_key", period_key),
                period_type=doc.get("period_type", _normalize_period(period_type)),
                used=int(doc.get("used", 0)),
                updated_at=doc.get("updated_at") or now,
            )

        return TokenUsageSnapshot(
            app_id=app_id,
            period_key=period_key,
            period_type=_normalize_period(period_type),
            used=delta,
            updated_at=now,
        )


_store: Optional[TokenUsageStore] = None


def get_token_usage_store() -> TokenUsageStore:
    global _store
    if _store is None:
        _store = TokenUsageStore()
    return _store


def should_track_locally(source: str) -> bool:
    """Decide whether Core should mutate local token usage.

    If Platform is authoritative, Core should only report usage upstream and
    wait for sync pushes (avoid double counting).
    """
    mode = os.getenv("MOZAIKS_TOKEN_USAGE_MODE", "").strip().lower()
    if mode in {"local", "hybrid"}:
        return True
    if mode in {"platform", "remote"}:
        return False
    return source != "platform"

