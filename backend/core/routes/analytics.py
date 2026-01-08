# backend/core/routes/analytics.py
from datetime import date, timedelta

from fastapi import APIRouter, Depends, HTTPException, Query, status
from security.authentication import get_current_admin_user, require_admin_or_internal
from core.analytics.kpi_service import kpi_service
from core.analytics.app_kpi_snapshot_service import app_kpi_snapshot_service
import logging

router = APIRouter()
logger = logging.getLogger("mozaiks_core.routes.analytics")

@router.get("/kpis", response_model=dict)
async def get_dashboard_kpis(current_user: dict = Depends(get_current_admin_user)):
    """
    Get Key Performance Indicators for the dashboard.
    
    Requires admin privileges.
    """
    try:
        kpis = await kpi_service.get_dashboard_summary()
        return kpis
    except Exception as e:
        logger.error(f"Error fetching KPIs: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Could not fetch analytics data"
        )


@router.get("/app-kpi-snapshot", response_model=dict)
async def get_app_kpi_snapshot(
    snapshot_date: str = Query(default_factory=lambda: date.today().isoformat(), alias="date"),
    recompute: bool = Query(False),
    current_user: dict = Depends(require_admin_or_internal),
):
    """Compute or return the daily App KPI snapshot.

    KPIs are derived from immutable raw events (user_events).
    """
    try:
        day = date.fromisoformat(snapshot_date)
    except Exception:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Invalid date; expected YYYY-MM-DD")

    try:
        snapshot = await app_kpi_snapshot_service.get_or_compute(app_id=None, day=day, recompute=recompute)
        return snapshot
    except Exception as e:
        logger.error(f"Error computing app KPI snapshot: {e}")
        raise HTTPException(status_code=status.HTTP_503_SERVICE_UNAVAILABLE, detail="Snapshot unavailable")


@router.get("/app-kpi-snapshots", response_model=dict)
async def get_app_kpi_snapshots(
    from_date: str = Query(..., alias="from"),
    to_date: str = Query(..., alias="to"),
    recompute: bool = Query(False),
    current_user: dict = Depends(require_admin_or_internal),
):
    """Return a date-range series of daily App KPI snapshots.

    This is cache-friendly and intended for dashboards in external services.
    """
    try:
        start = date.fromisoformat(from_date)
        end = date.fromisoformat(to_date)
    except Exception:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Invalid from/to; expected YYYY-MM-DD")

    if end < start:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Invalid range: to < from")

    # Guardrail to prevent accidental heavy scans.
    max_days = 366
    days = (end - start).days + 1
    if days > max_days:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Range too large (max {max_days} days)",
        )

    series = []
    d = start
    while d <= end:
        snapshot = await app_kpi_snapshot_service.get_or_compute(app_id=None, day=d, recompute=recompute)
        series.append(snapshot)
        d += timedelta(days=1)

    return {"from": start.isoformat(), "to": end.isoformat(), "series": series}
