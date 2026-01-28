# backend/core/analytics/app_kpi_snapshot_service.py
import logging
import os
from dataclasses import dataclass
from datetime import date, datetime, timedelta
from typing import Any, Dict, Optional, Tuple

from mozaiks_infra.config.database import db
from mozaiks_infra.metrics.computation import cohort_retention, count_events, distinct_users, safe_div

logger = logging.getLogger("mozaiks_core.analytics.app_kpi_snapshot")

user_events = db["user_events"]
app_snapshots = db["app_kpi_snapshots"]


def _utcnow() -> datetime:
    return datetime.utcnow()


def _app_id(app_id: Optional[str]) -> str:
    return app_id or os.getenv("MOZAIKS_APP_ID") or "unknown-app"


def _parse_date_yyyy_mm_dd(value: str) -> date:
    return date.fromisoformat(value)


def _day_bounds(d: date) -> Tuple[datetime, datetime]:
    start = datetime(d.year, d.month, d.day)
    end = start + timedelta(days=1)
    return start, end


@dataclass
class AppKpiSnapshotService:
    async def compute_snapshot(self, *, app_id: Optional[str], day: date) -> Dict[str, Any]:
        app = _app_id(app_id)
        day_start, day_end = _day_bounds(day)

        # Rolling windows are computed up to day_end (exclusive)
        w7_start = day_end - timedelta(days=7)
        w30_start = day_end - timedelta(days=30)

        total_users = await distinct_users(user_events, {"appId": app, "type": "UserSignedUp"})
        new_users_7d = await count_events(
            user_events,
            {"appId": app, "type": "UserSignedUp", "timestamp": {"$gte": w7_start, "$lt": day_end}},
        )
        new_users_30d = await count_events(
            user_events,
            {"appId": app, "type": "UserSignedUp", "timestamp": {"$gte": w30_start, "$lt": day_end}},
        )
        active_users_30d = await distinct_users(
            user_events,
            {"appId": app, "type": "UserActive", "timestamp": {"$gte": w30_start, "$lt": day_end}},
        )

        growth_rate_30d = safe_div(new_users_30d, max(total_users, 1))

        # Cohort retention: cohort is signups on (day - N), active on (day)
        retention_7d = await cohort_retention(
            user_events,
            app_id=app,
            cohort_days_ago=7,
            active_start=day_start,
            active_end=day_end,
            cohort_anchor_date=day,
        )
        retention_30d = await cohort_retention(
            user_events,
            app_id=app,
            cohort_days_ago=30,
            active_start=day_start,
            active_end=day_end,
            cohort_anchor_date=day,
        )
        churn_rate_30d = max(0.0, 1.0 - retention_30d)

        # Momentum is intra-app and doesn’t require cross-app normalization.
        prev_day = day - timedelta(days=1)
        prev = await app_snapshots.find_one({"appId": app, "date": prev_day.isoformat()})
        prev_new_users_7d = int(prev.get("newUsers7d") or 0) if prev else 0
        momentum_score = safe_div((new_users_7d - prev_new_users_7d), max(prev_new_users_7d, 1))

        return {
            "appId": app,
            "date": day.isoformat(),
            "totalUsers": total_users,
            "newUsers7d": new_users_7d,
            "newUsers30d": new_users_30d,
            "activeUsers30d": active_users_30d,
            "growthRate30d": growth_rate_30d,
            "retention7d": retention_7d,
            "retention30d": retention_30d,
            "churnRate30d": churn_rate_30d,
            # Cross-app normalized scores are MozaiksPlatform responsibility.
            "validationScore": None,
            "momentumScore": momentum_score,
            "computedAt": _utcnow().isoformat(),
            "schemaVersion": 2,
        }

    async def upsert_snapshot(self, *, app_id: Optional[str], day: date) -> Dict[str, Any]:
        payload = await self.compute_snapshot(app_id=app_id, day=day)
        await app_snapshots.update_one(
            {"appId": payload["appId"], "date": payload["date"]},
            {"$set": payload},
            upsert=True,
        )
        return payload

    async def get_or_compute(self, *, app_id: Optional[str], day: date, recompute: bool = False) -> Dict[str, Any]:
        app = _app_id(app_id)
        if not recompute:
            existing = await app_snapshots.find_one({"appId": app, "date": day.isoformat()})
            if existing:
                existing.pop("_id", None)
                return existing
        return await self.upsert_snapshot(app_id=app, day=day)


app_kpi_snapshot_service = AppKpiSnapshotService()
