# backend/core/analytics/raw_events.py
import logging
import os
from datetime import datetime
from typing import Any, Dict, Optional

from core.config.database import db

logger = logging.getLogger("mozaiks_core.analytics.raw_events")

user_events_collection = db["user_events"]


def _utcnow() -> datetime:
    return datetime.utcnow()


def _day_bucket(ts: datetime) -> str:
    return ts.date().isoformat()


def _app_id(app_id: Optional[str]) -> str:
    return app_id or os.getenv("MOZAIKS_APP_ID") or "unknown-app"


async def init_raw_event_indexes() -> None:
    """Best-effort index creation for raw events.

    Safe to call multiple times.
    """
    try:
        await user_events_collection.create_index([("appId", 1), ("type", 1), ("timestamp", -1)])
        await user_events_collection.create_index([("appId", 1), ("userId", 1), ("type", 1), ("day", 1)])
        # Efficient range scans for outbound ingestion push (appId+type, ordered by insertion time).
        await user_events_collection.create_index([("appId", 1), ("type", 1), ("_id", 1)])

        # Prevent duplicate signup marker per (appId,userId)
        await user_events_collection.create_index(
            [("appId", 1), ("userId", 1), ("type", 1)],
            unique=True,
            partialFilterExpression={"type": "UserSignedUp"},
        )

        # Daily active marker: at most one per user per day.
        await user_events_collection.create_index(
            [("appId", 1), ("userId", 1), ("type", 1), ("day", 1)],
            unique=True,
            partialFilterExpression={"type": "UserActive"},
        )
    except Exception as e:
        logger.debug(f"Index creation skipped/failed (non-fatal): {e}")


async def append_user_signed_up(*, user_id: str, app_id: Optional[str] = None, timestamp: Optional[datetime] = None) -> None:
    ts = timestamp or _utcnow()
    app = _app_id(app_id)
    day = _day_bucket(ts)

    # Idempotent: one signup marker per (appId,userId).
    await user_events_collection.update_one(
        {"appId": app, "type": "UserSignedUp", "userId": user_id},
        {
            "$setOnInsert": {
                "type": "UserSignedUp",
                "userId": user_id,
                "appId": app,
                "timestamp": ts,
                "day": day,
            }
        },
        upsert=True,
    )


async def append_user_active(*, user_id: str, app_id: Optional[str] = None, timestamp: Optional[datetime] = None) -> None:
    ts = timestamp or _utcnow()
    app = _app_id(app_id)
    day = _day_bucket(ts)

    # Idempotent: at most one daily active marker per (appId,userId,day).
    await user_events_collection.update_one(
        {"appId": app, "type": "UserActive", "userId": user_id, "day": day},
        {
            "$setOnInsert": {
                "type": "UserActive",
                "userId": user_id,
                "appId": app,
                "timestamp": ts,
                "day": day,
            }
        },
        upsert=True,
    )
