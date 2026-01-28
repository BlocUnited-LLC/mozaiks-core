# core/routes/billing.py
"""
Billing API Routes - Platform ↔ Core billing integration.

These endpoints handle:
- POST /api/v1/entitlements/{app_id}/sync - Receive entitlement updates from Platform
- GET /api/v1/entitlements/{app_id} - Get current entitlements for an app (internal)

Auth: Platform calls use Keycloak app-only JWTs with role internal_service.
"""

from __future__ import annotations

from typing import Any, Dict, Optional

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, ConfigDict, Field

from mozaiks_ai.runtime.auth.dependencies import require_internal_service
from mozaiks_platform.billing.sync import EntitlementSyncRequest, get_sync_handler

router = APIRouter(
    prefix="/api/v1/entitlements",
    tags=["billing"],
    dependencies=[Depends(require_internal_service)],
)


# ---------------------------------------------------------------------------
# Request/Response Models
# ---------------------------------------------------------------------------


class PlanInfo(BaseModel):
    """Plan information from Platform."""
    model_config = ConfigDict(extra="ignore")

    id: Optional[str] = None
    name: Optional[str] = None
    tier: str = "free"
    billing_period: str = "monthly"
    expires_at: Optional[str] = None


class TokenBudgetTotal(BaseModel):
    """Token budget totals."""
    model_config = ConfigDict(extra="ignore")

    limit: int = -1
    used: int = 0
    enforcement: str = "soft"


class TokenBudget(BaseModel):
    """Token budget configuration."""
    model_config = ConfigDict(extra="ignore")

    period: str = "monthly"
    total_tokens: TokenBudgetTotal = Field(default_factory=TokenBudgetTotal)


class EntitlementSyncPayload(BaseModel):
    """
    Payload for POST /api/v1/entitlements/{app_id}/sync.
    
    This matches the contract defined in sync.py
    """
    model_config = ConfigDict(extra="ignore")

    version: str = "1.0"
    app_id: str
    tenant_id: Optional[str] = None
    plan: PlanInfo = Field(default_factory=PlanInfo)
    token_budget: TokenBudget = Field(default_factory=TokenBudget)
    features: Dict[str, bool] = Field(default_factory=dict)
    rate_limits: Dict[str, int] = Field(default_factory=dict)
    correlation_id: Optional[str] = None


class EntitlementSyncResponse(BaseModel):
    """Response for sync endpoint."""
    model_config = ConfigDict(extra="ignore")

    status: str
    app_id: str
    synced_at: str
    previous_tier: Optional[str] = None
    new_tier: str = "free"
    error: Optional[str] = None


class EntitlementResponse(BaseModel):
    """Response for GET entitlements endpoint."""
    model_config = ConfigDict(extra="ignore")

    app_id: str
    plan: Dict[str, Any]
    token_budget: Dict[str, Any]
    features: Dict[str, bool]
    rate_limits: Dict[str, int]
    synced_at: Optional[str] = None


# ---------------------------------------------------------------------------
# Routes
# ---------------------------------------------------------------------------


@router.post(
    "/{app_id}/sync",
    response_model=EntitlementSyncResponse,
    summary="Sync entitlements from Platform",
    description=(
        "Called by mozaiks-platform when a subscription changes. "
        "Updates Core's entitlement cache for the given app_id."
    ),
)
async def sync_entitlements(
    app_id: str,
    payload: EntitlementSyncPayload,
):
    """
    Receive entitlement sync from Platform.
    
    Platform calls this endpoint when:
    - User subscribes to a plan
    - User upgrades/downgrades
    - Subscription expires or is cancelled
    - Usage resets at billing period start
    """
    # Auth enforced via APIRouter dependency.
    
    # Validate app_id consistency
    if payload.app_id != app_id:
        raise HTTPException(
            status_code=400,
            detail=f"app_id mismatch: URL has '{app_id}', body has '{payload.app_id}'",
        )
    
    # Convert payload to domain request
    sync_request = EntitlementSyncRequest.from_dict(payload.model_dump())
    
    # Handle sync
    handler = get_sync_handler()
    result = await handler.handle_sync(sync_request)
    
    # Return response
    return EntitlementSyncResponse(
        status=result.status,
        app_id=result.app_id,
        synced_at=result.synced_at.isoformat() + "Z",
        previous_tier=result.previous_tier,
        new_tier=result.new_tier,
        error=result.error,
    )


@router.get(
    "/{app_id}",
    response_model=EntitlementResponse,
    summary="Get entitlements for an app",
    description="Get current cached entitlements for an app_id. Internal use.",
)
async def get_entitlements(
    app_id: str,
):
    """
    Get current entitlements for an app.
    
    Returns cached entitlements from last sync.
    Returns defaults if app has never been synced (OSS mode).
    """
    # Auth enforced via APIRouter dependency.
    
    handler = get_sync_handler()
    ent = handler.get_entitlements(app_id)
    
    if not ent:
        # Return OSS defaults - unlimited everything
        return EntitlementResponse(
            app_id=app_id,
            plan={"tier": "unlimited", "name": "Open Source"},
            token_budget={
                "period": "unlimited",
                "total_tokens": {"limit": -1, "used": 0, "enforcement": "none"},
            },
            features={},
            rate_limits={},
            synced_at=None,
        )
    
    return EntitlementResponse(
        app_id=app_id,
        plan=ent.get("plan", {}),
        token_budget=ent.get("token_budget", {}),
        features=ent.get("features", {}),
        rate_limits=ent.get("rate_limits", {}),
        synced_at=ent.get("synced_at"),
    )


@router.get(
    "",
    summary="List all apps with entitlements",
    description="Get list of all app_ids that have synced entitlements.",
)
async def list_apps(
):
    """List all apps with cached entitlements."""
    # Auth enforced via APIRouter dependency.
    
    handler = get_sync_handler()
    apps = handler.list_apps()
    
    return {"apps": apps, "count": len(apps)}
