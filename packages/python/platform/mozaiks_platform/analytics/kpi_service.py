# backend/core/analytics/kpi_service.py
import logging
from datetime import datetime, timedelta
import os

from mozaiks_infra.config.database import db
from mozaiks_infra.metrics.computation import (
    cohort_retention,
    count_events_by_event,
    distinct_users_by_event,
    safe_div,
)

logger = logging.getLogger("mozaiks_core.analytics")

user_events_collection = db["user_events"]


def _utcnow() -> datetime:
    return datetime.utcnow()


def _app_id(app_id: str | None = None) -> str:
    return app_id or os.getenv("MOZAIKS_APP_ID") or "unknown-app"

class KPIService:
    """
    Service to aggregate Key Performance Indicators (KPIs) for the dashboard.
    """

    async def get_app_activity_kpis(self) -> dict:
        """Return app-native user/activity KPIs (no AI/plugin/revenue/growth fields)."""

        app = _app_id()
        end = _utcnow()

        w1_start = end - timedelta(days=1)
        w7_start = end - timedelta(days=7)
        w30_start = end - timedelta(days=30)

        total_users = await distinct_users_by_event(user_events_collection, app_id=app, event_type="UserSignedUp")
        dau = await distinct_users_by_event(user_events_collection, app_id=app, event_type="UserActive", start=w1_start, end=end)
        mau = await distinct_users_by_event(user_events_collection, app_id=app, event_type="UserActive", start=w30_start, end=end)
        active_users_7d = await distinct_users_by_event(user_events_collection, app_id=app, event_type="UserActive", start=w7_start, end=end)
        new_users_7d = await count_events_by_event(user_events_collection, app_id=app, event_type="UserSignedUp", start=w7_start, end=end)

        stickiness_dau_mau = safe_div(dau, max(mau, 1))

        prev_w7_start = w7_start - timedelta(days=7)
        prev_w7_end = w7_start
        prev_active_users_7d = await distinct_users_by_event(user_events_collection, app_id=app, event_type="UserActive", start=prev_w7_start, end=prev_w7_end)
        prev_new_users_7d = await count_events_by_event(user_events_collection, app_id=app, event_type="UserSignedUp", start=prev_w7_start, end=prev_w7_end)

        active_users_7d_trend_pct = None if prev_active_users_7d == 0 else safe_div((active_users_7d - prev_active_users_7d), prev_active_users_7d)
        new_users_7d_trend_pct = None if prev_new_users_7d == 0 else safe_div((new_users_7d - prev_new_users_7d), prev_new_users_7d)

        retention_7d = await cohort_retention(user_events_collection, app_id=app, cohort_days_ago=7, active_start=w1_start, active_end=end)
        retention_30d = await cohort_retention(user_events_collection, app_id=app, cohort_days_ago=30, active_start=w1_start, active_end=end)
        churn_30d = max(0.0, 1.0 - retention_30d)

        return {
            "schema_version": 2,
            "app_id": app,
            "generated_at": end.isoformat(),
            "engagement": {
                "total_users": total_users,
                "dau": dau,
                "mau": mau,
                "active_users_7d": active_users_7d,
                "new_users_7d": new_users_7d,
                "stickiness_dau_mau": stickiness_dau_mau,
                "trending": {
                    "active_users_7d_trend_pct": active_users_7d_trend_pct,
                    "new_users_7d_trend_pct": new_users_7d_trend_pct,
                },
                "retention": {
                    "retention_7d": retention_7d,
                    "retention_30d": retention_30d,
                    "churn_30d": churn_30d,
                },
            },
        }

    async def get_dashboard_summary(self):
        """
        Aggregate all metrics for the dashboard.
        """
        return await self.get_app_activity_kpis()

kpi_service = KPIService()
