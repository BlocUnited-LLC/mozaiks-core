# backend/app/routes/mozaiks.py
from __future__ import annotations

import os
import uuid

from fastapi import APIRouter, Depends, Header, HTTPException, Query, Request, Response, status

from app.connectors.base import (
    CancelRequest,
    CheckoutRequest,
    CheckoutResponse,
    OkResponse,
    PaymentStatus,
)
from app.runtime.connector_loader import ConnectorBundle, load_connectors
from mozaiks_ai.runtime.auth.dependencies import get_current_user
from security.rate_limit import RateLimitPolicy, enforce_rate_limit

router = APIRouter()


def _parse_bearer_token(authorization: str | None) -> str | None:
    if not authorization:
        return None
    parts = authorization.strip().split()
    if len(parts) == 2 and parts[0].lower() == "bearer":
        return parts[1]
    return authorization.strip()


def _corr_id(response: Response, incoming: str | None) -> str:
    correlation_id = (incoming or "").strip() or str(uuid.uuid4())
    response.headers["x-correlation-id"] = correlation_id
    return correlation_id


def _connectors() -> ConnectorBundle:
    return load_connectors()


def _configured_app_id() -> str | None:
    app_id = (os.getenv("MOZAIKS_APP_ID") or "").strip()
    return app_id or None


def _validate_app_id(provided: str | None) -> str | None:
    configured = _configured_app_id()
    if configured:
        if provided and provided != configured:
            raise HTTPException(status_code=status.HTTP_403_FORBIDDEN, detail="Invalid appId")
        return configured
    return (provided or "").strip() or None


def _require_app_id(provided: str | None) -> str:
    resolved = _validate_app_id(provided)
    if not resolved:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="appId is required for scope=app")
    return resolved


def _normalize_scope(scope: str | None) -> str:
    normalized = (scope or "").strip().lower()
    if normalized in {"app", "platform"}:
        return normalized
    raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Invalid scope (expected platform|app)")


@router.post("/pay/checkout", response_model=CheckoutResponse, response_model_exclude_none=True)
async def pay_checkout(
    request: Request,
    response: Response,
    payload: CheckoutRequest,
    authorization: str | None = Header(default=None),
    x_correlation_id: str | None = Header(default=None, alias="x-correlation-id"),
    user: dict = Depends(get_current_user),
):
    enforce_rate_limit(request, bucket="mozaiks.pay.checkout", policy=RateLimitPolicy(limit=30, window_s=60))
    correlation_id = _corr_id(response, x_correlation_id)
    token = _parse_bearer_token(authorization)
    connectors = _connectors()

    # Default scope: "app" if an appId is available, else "platform".
    scope = _normalize_scope(payload.scope) if payload.scope else ("app" if (_validate_app_id(payload.appId)) else "platform")
    resolved_app_id = _require_app_id(payload.appId) if scope == "app" else None

    secured_payload = payload.model_copy(update={"scope": scope, "appId": resolved_app_id})
    return await connectors.payment.checkout(payload=secured_payload, correlation_id=correlation_id, user_jwt=token)


@router.get("/pay/subscription-status", response_model=PaymentStatus, response_model_exclude_none=True)
async def pay_subscription_status(
    request: Request,
    response: Response,
    scope: str = Query(...),
    appId: str | None = Query(None),
    authorization: str | None = Header(default=None),
    x_correlation_id: str | None = Header(default=None, alias="x-correlation-id"),
    user: dict = Depends(get_current_user),
):
    enforce_rate_limit(request, bucket="mozaiks.pay.subscription_status", policy=RateLimitPolicy(limit=60, window_s=60))
    correlation_id = _corr_id(response, x_correlation_id)
    token = _parse_bearer_token(authorization)
    connectors = _connectors()

    normalized_scope = _normalize_scope(scope)
    resolved_app_id = _require_app_id(appId) if normalized_scope == "app" else _validate_app_id(appId)

    return await connectors.payment.subscription_status(
        scope=normalized_scope,
        appId=resolved_app_id,
        correlation_id=correlation_id,
        user_jwt=token,
    )


@router.post("/pay/cancel", response_model=OkResponse, response_model_exclude_none=True)
async def pay_cancel(
    request: Request,
    response: Response,
    payload: CancelRequest,
    authorization: str | None = Header(default=None),
    x_correlation_id: str | None = Header(default=None, alias="x-correlation-id"),
    user: dict = Depends(get_current_user),
):
    enforce_rate_limit(request, bucket="mozaiks.pay.cancel", policy=RateLimitPolicy(limit=30, window_s=60))
    correlation_id = _corr_id(response, x_correlation_id)
    token = _parse_bearer_token(authorization)
    connectors = _connectors()

    normalized_scope = _normalize_scope(payload.scope)
    resolved_app_id = _require_app_id(payload.appId) if normalized_scope == "app" else _validate_app_id(payload.appId)

    secured_payload = payload.model_copy(update={"scope": normalized_scope, "appId": resolved_app_id})
    return await connectors.payment.cancel(payload=secured_payload, correlation_id=correlation_id, user_jwt=token)
