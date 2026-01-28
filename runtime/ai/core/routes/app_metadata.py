# backend/core/routes/app_metadata.py
import os
from datetime import date, timedelta

from fastapi import APIRouter, Depends, HTTPException, Query, status

from core.config.database import db
from core.analytics.app_kpi_snapshot_service import app_kpi_snapshot_service
from core.ai_runtime.auth.dependencies import require_admin_or_internal


router = APIRouter()

user_events = db["user_events"]


@router.get("/metadata", response_model=dict)
async def get_app_metadata():
    """Minimal app identity for discovery (e.g., open-source instances migrating to hosted).

    mozaiksbackend already knows everything else about the app from its catalog.
    """
    app_id = os.getenv("MOZAIKS_APP_ID") or "unknown-app"
    return {"appId": app_id}


async def _count_events(query: dict) -> int:
    return int(await user_events.count_documents(query))


async def _count_distinct_users(match: dict) -> int:
    cursor = user_events.aggregate(
        [
            {"$match": match},
            {"$group": {"_id": "$userId"}},
            {"$count": "count"},
        ]
    )
    docs = await cursor.to_list(length=1)
    return int(docs[0]["count"]) if docs else 0


@router.get("/metrics", response_model=dict)
async def get_app_metrics(
    from_date: str = Query(..., alias="from"),
    to_date: str = Query(..., alias="to"),
    granularity: str = Query("day"),
    include_retention: bool = Query(True),
    current_user: dict = Depends(require_admin_or_internal),
):
    """Dashboard-friendly app metrics.

    This is a read-only, cache-friendly endpoint intended for service-to-service
    consumption (e.g., mozaiksbackend) and for UI dashboards via your backend.

    Output format:
      - summary: totals for the requested range (and DAU/WAU/MAU as-of `to`)
      - series: daily points [{ date, metrics: { dau, new_users, total_users } }]
    """
    try:
        start_day = date.fromisoformat(from_date)
        end_day = date.fromisoformat(to_date)
    except Exception:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Invalid from/to; expected YYYY-MM-DD")

    if end_day < start_day:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Invalid range: to < from")

    if granularity != "day":
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Unsupported granularity; only 'day'")

    max_days = 366
    days = (end_day - start_day).days + 1
    if days > max_days:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail=f"Range too large (max {max_days} days)")

    app_id = os.getenv("MOZAIKS_APP_ID") or "unknown-app"

    # Compute base total users before the range starts.
    # NOTE: UserSignedUp is unique per (appId,userId) by index, so count_documents is correct.
    base_total_users = await _count_events(
        {"appId": app_id, "type": "UserSignedUp", "day": {"$lt": start_day.isoformat()}}
    )

    series = []
    cumulative_new_users = 0

    # DAU is computed per day; new_users is count of signup events in that day.
    d = start_day
    last_day_dau = 0
    while d <= end_day:

        day_key = d.isoformat()

        new_users_day = await _count_events({"appId": app_id, "type": "UserSignedUp", "day": day_key})
        cumulative_new_users += int(new_users_day)

        # UserActive is unique per user per day, so DAU is just the count of daily markers.
        dau_day = await _count_events({"appId": app_id, "type": "UserActive", "day": day_key})
        last_day_dau = int(dau_day)

        series.append(
            {
                "date": d.isoformat(),
                "metrics": {
                    "dau": int(dau_day),
                    "new_users": int(new_users_day),
                    "total_users": int(base_total_users + cumulative_new_users),
                },
            }
        )

        d += timedelta(days=1)

    new_users_range = int(cumulative_new_users)
    total_users_end = int(base_total_users + cumulative_new_users)

    active_users_range = await _count_distinct_users(
        {
            "appId": app_id,
            "type": "UserActive",
            "day": {"$gte": start_day.isoformat(), "$lte": end_day.isoformat()},
        }
    )

    # WAU/MAU are as-of the end date.
    wau_start_day = end_day - timedelta(days=6)
    mau_start_day = end_day - timedelta(days=29)
    wau = await _count_distinct_users(
        {
            "appId": app_id,
            "type": "UserActive",
            "day": {"$gte": wau_start_day.isoformat(), "$lte": end_day.isoformat()},
        }
    )
    mau = await _count_distinct_users(
        {
            "appId": app_id,
            "type": "UserActive",
            "day": {"$gte": mau_start_day.isoformat(), "$lte": end_day.isoformat()},
        }
    )
    stickiness = float(last_day_dau) / float(mau if mau else 1)

    summary = {
        "total_users": total_users_end,
        "new_users": new_users_range,
        "active_users": int(active_users_range),
        "dau": int(last_day_dau),
        "wau": int(wau),
        "mau": int(mau),
        "stickiness_dau_mau": float(stickiness),
    }

    if include_retention:
        snapshot = await app_kpi_snapshot_service.get_or_compute(app_id=None, day=end_day, recompute=False)
        summary.update(
            {
                "retention_7d": snapshot.get("retention7d"),
                "retention_30d": snapshot.get("retention30d"),
                "churn_30d": snapshot.get("churnRate30d"),
            }
        )

    return {
        "appId": app_id,
        "from": start_day.isoformat(),
        "to": end_day.isoformat(),
        "granularity": "day",
        "summary": summary,
        "series": series,
    }

