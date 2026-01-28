# backend/core/routes/status.py
import os

from fastapi import APIRouter, Depends

from core.config.settings import settings
from core.ops.signals import snapshot as ops_snapshot
from core.ai_runtime.auth.dependencies import require_admin_or_internal


router = APIRouter()


@router.get("/status", response_model=dict)
async def get_status(current_user: dict = Depends(require_admin_or_internal)):
    """Operational status endpoint (admin/internal).

    This is intended for platform/admin dashboards (not end-user KPIs).
    """
    app_id = (settings.mozaiks_app_id or os.getenv("MOZAIKS_APP_ID") or "unknown-app").strip()
    return {
        "appId": app_id,
        "env": (settings.env or "development").strip(),
        "ops": ops_snapshot(),
    }

