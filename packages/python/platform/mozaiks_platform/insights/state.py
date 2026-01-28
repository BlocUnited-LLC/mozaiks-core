from __future__ import annotations

import logging
from dataclasses import dataclass
from datetime import datetime, timezone

from bson import ObjectId

from mozaiks_infra.config.database import db

logger = logging.getLogger("mozaiks_core.insights.state")

_state_collection = db["insights_push_state"]


@dataclass(frozen=True)
class InsightsCheckpoint:
    last_object_id: ObjectId


async def init_insights_state_indexes() -> None:
    try:
        await _state_collection.create_index([("appId", 1), ("env", 1), ("kind", 1)], unique=True)
    except Exception as e:
        logger.debug(f"Insights state index creation skipped/failed (non-fatal): {e}")


async def get_checkpoint(*, app_id: str, env: str, kind: str) -> InsightsCheckpoint | None:
    doc = await _state_collection.find_one({"appId": app_id, "env": env, "kind": kind})
    if not doc:
        return None
    last_id = doc.get("lastObjectId")
    if isinstance(last_id, ObjectId):
        return InsightsCheckpoint(last_object_id=last_id)
    if isinstance(last_id, str):
        try:
            return InsightsCheckpoint(last_object_id=ObjectId(last_id))
        except Exception:
            return None
    return None


async def save_checkpoint(*, app_id: str, env: str, kind: str, last_object_id: ObjectId) -> None:
    now = datetime.now(tz=timezone.utc)
    await _state_collection.update_one(
        {"appId": app_id, "env": env, "kind": kind},
        {"$set": {"lastObjectId": last_object_id, "updatedAt": now}},
        upsert=True,
    )

