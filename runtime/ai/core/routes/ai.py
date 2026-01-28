from __future__ import annotations

import json
import logging
import os
from typing import Any, Optional

from fastapi import APIRouter, Depends, HTTPException, status
from pydantic import BaseModel, Field

from core.config.settings import settings
from core.config.config_loader import get_ai_capabilities, get_config_path
from core.runtime.manager import runtime_manager
from core.runtime.execution_tokens import mint_execution_token
from core.state_manager import state_manager
from core.subscription_manager import subscription_manager
from core.ai_runtime.auth.dependencies import get_current_user

logger = logging.getLogger("mozaiks_core.routes.ai")

router = APIRouter(prefix="/api/ai", tags=["ai-control-plane"])


def _load_capabilities_config() -> dict[str, Any]:
    """Load AI capabilities from the central config loader."""
    payload = get_ai_capabilities()
    
    if not payload:
        logger.warning("ai_capabilities.json not found; returning empty capabilities list")
        return {"capabilities": []}

    if not isinstance(payload, dict) or not isinstance(payload.get("capabilities"), list):
        raise HTTPException(status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail="Invalid ai capabilities config shape")

    return payload


def _resolve_path(value: str, *, base_dir: str) -> str:
    expanded = os.path.expandvars(os.path.expanduser(value.strip()))
    if os.path.isabs(expanded):
        return os.path.normpath(expanded)
    return os.path.normpath(os.path.join(base_dir, expanded))


def _load_capability_specs() -> list[dict[str, Any]]:
    """
    Load declarative capability specs (generated upstream) from disk.

    Spec files are optional and should be treated as app-owned configuration.
    """
    config_dir = str(get_config_path())
    specs_dir_env = (os.getenv("MOZAIKS_AI_CAPABILITY_SPECS_DIR") or "").strip()
    specs_dir = _resolve_path(specs_dir_env, base_dir=config_dir) if specs_dir_env else os.path.join(config_dir, "capability_specs")
    specs_dir = os.path.normpath(specs_dir)

    if not os.path.isdir(specs_dir):
        return []

    specs: list[dict[str, Any]] = []
    for entry in os.listdir(specs_dir):
        if not entry.lower().endswith(".json"):
            continue
        if entry.lower().endswith(".example.json") or entry.lower().endswith(".template.json"):
            continue

        path = os.path.join(specs_dir, entry)
        try:
            with open(path, "r", encoding="utf-8") as f:
                payload = json.load(f)
        except Exception as e:
            logger.warning(f"Skipping invalid capability spec '{path}': {e}")
            continue

        if isinstance(payload, dict) and isinstance(payload.get("capability"), dict):
            payload = payload["capability"]

        if not isinstance(payload, dict):
            logger.warning(f"Skipping capability spec '{path}' (expected object)")
            continue

        capability_id = (payload.get("id") or "").strip()
        if not capability_id:
            logger.warning(f"Skipping capability spec '{path}' (missing id)")
            continue

        payload.setdefault("enabled", True)
        payload.setdefault("visibility", "user")
        specs.append(payload)

    return specs


def _merge_capabilities(base: list[Any], specs: list[dict[str, Any]]) -> list[dict[str, Any]]:
    merged: list[dict[str, Any]] = []
    index_by_id: dict[str, int] = {}

    for cap in base:
        if not isinstance(cap, dict):
            continue
        cap_id = (cap.get("id") or "").strip()
        if not cap_id:
            continue
        index_by_id[cap_id] = len(merged)
        merged.append(cap)

    for cap in specs:
        cap_id = (cap.get("id") or "").strip()
        if not cap_id:
            continue
        if cap_id in index_by_id:
            merged[index_by_id[cap_id]] = cap
        else:
            index_by_id[cap_id] = len(merged)
            merged.append(cap)

    return merged


def _load_all_capabilities() -> list[dict[str, Any]]:
    config = _load_capabilities_config()
    base_capabilities = config.get("capabilities", [])
    specs = _load_capability_specs()
    return _merge_capabilities(base_capabilities, specs)


def _normalize_plan(plan: str | None) -> str:
    return (plan or "").strip().lower() or "free"


def _monetization_enabled() -> bool:
    return (os.getenv("MONETIZATION") or "0").strip() == "1"


def _is_allowed_for_plan(allowed_plans: Any, plan: str) -> bool:
    if allowed_plans is None:
        return True
    if isinstance(allowed_plans, str):
        allowed_plans = [allowed_plans]
    if not isinstance(allowed_plans, list):
        return False
    normalized = {_normalize_plan(p) for p in allowed_plans if isinstance(p, str)}
    return "*" in normalized or plan in normalized


def _public_capability(cap: dict[str, Any], *, allowed: bool) -> dict[str, Any]:
    return {
        "id": cap.get("id", ""),
        "display_name": cap.get("display_name", cap.get("name", "")),
        "description": cap.get("description", ""),
        "icon": cap.get("icon", ""),
        "visibility": cap.get("visibility", "user"),
        "enabled": bool(cap.get("enabled", True)),
        "allowed": allowed,
    }


class LaunchRequest(BaseModel):
    capability_id: str = Field(..., min_length=1)


@router.get("/capabilities")
async def list_capabilities(current_user: dict = Depends(get_current_user)):
    """
    List AI capabilities available to the current user.

    IMPORTANT: Clients request capabilities, not workflow IDs.
    """
    capabilities = _load_all_capabilities()

    user_plan = "free"
    if _monetization_enabled():
        try:
            subscription = await subscription_manager.get_user_subscription(current_user["user_id"])
            user_plan = _normalize_plan(subscription.get("plan"))
        except Exception:
            user_plan = "free"

    is_superadmin = bool(current_user.get("is_superadmin"))

    visible = []
    for cap in capabilities:
        if not isinstance(cap, dict):
            continue

        enabled = bool(cap.get("enabled", True))
        visibility = (cap.get("visibility") or "user").strip().lower() or "user"
        requires_superadmin = bool(cap.get("requires_superadmin", False)) or visibility == "admin"
        allowed_plans = cap.get("allowed_plans")

        allowed = enabled and (is_superadmin or not requires_superadmin) and _is_allowed_for_plan(allowed_plans, user_plan)
        if allowed:
            visible.append(_public_capability(cap, allowed=True))

    return {"capabilities": visible, "plan": user_plan}


@router.post("/launch")
async def launch_capability(request: LaunchRequest, current_user: dict = Depends(get_current_user)):
    """
    Authorize and launch an AI capability into the external MozaiksAI ChatUI.

    Returns metadata needed for the client to open ChatUI; this service does NOT execute workflows.
    """
    capability_id = request.capability_id.strip()
    if not capability_id:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="capability_id is required")

    capabilities = _load_all_capabilities()
    match = next((c for c in capabilities if isinstance(c, dict) and (c.get("id") or "").strip() == capability_id), None)
    if not match:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Unknown capability")

    enabled = bool(match.get("enabled", True))
    if not enabled:
        raise HTTPException(status_code=status.HTTP_403_FORBIDDEN, detail="Capability is disabled")

    visibility = (match.get("visibility") or "user").strip().lower() or "user"
    requires_superadmin = bool(match.get("requires_superadmin", False)) or visibility == "admin"
    if requires_superadmin and not bool(current_user.get("is_superadmin")):
        raise HTTPException(status_code=status.HTTP_403_FORBIDDEN, detail="Superadmin access required")

    user_plan = "free"
    if _monetization_enabled():
        try:
            subscription = await subscription_manager.get_user_subscription(current_user["user_id"])
            user_plan = _normalize_plan(subscription.get("plan"))
        except Exception:
            user_plan = "free"

    allowed_plans = match.get("allowed_plans")
    if _monetization_enabled() and not _is_allowed_for_plan(allowed_plans, user_plan):
        raise HTTPException(status_code=status.HTTP_403_FORBIDDEN, detail="Capability not available for current plan")

    app_id = settings.mozaiks_app_id
    if not app_id:
        raise HTTPException(status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail="MOZAIKS_APP_ID is not configured")

    workflow_id = (match.get("workflow_id") or "").strip()
    if not workflow_id:
        raise HTTPException(status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail="Capability is missing workflow_id mapping")

    chat_id = runtime_manager.new_chat_id()
    ui_config = runtime_manager.ui_config()

    # ExecutionContext ownership:
    # - MozaiksCore authorizes capability -> workflow mapping.
    # - The client never supplies workflow_id or entitlements.
    # - A short-lived launch token carries the ExecutionContext to the runtime.
    execution_claims: dict[str, Any] = {
        "sub": current_user.get("identity_user_id") or current_user.get("user_id"),
        "app_id": app_id,
        "chat_id": chat_id,
        "capability_id": capability_id,
        "workflow_id": workflow_id,
        "user_id": current_user.get("user_id"),
        "roles": current_user.get("roles") or [],
        "is_superadmin": bool(current_user.get("is_superadmin")),
        "plan": user_plan,
    }

    capability_spec = match.get("spec")
    if isinstance(capability_spec, dict) and capability_spec:
        execution_claims["capability_spec"] = capability_spec

    launch_token, expires_in = mint_execution_token(claims=execution_claims)
    try:
        state_manager.set(f"ai_execution_context:{chat_id}", execution_claims, expire_in=expires_in)
    except Exception:
        pass

    return {
        "app_id": app_id,
        "capability_id": capability_id,
        "chat_id": chat_id,
        "launch_token": launch_token,
        "expires_in": expires_in,
        "runtime": {
            "runtime_api_base_url": ui_config.runtime_api_base_url,
            "runtime_ui_base_url": ui_config.runtime_ui_base_url,
            "chatui_url_template": ui_config.chatui_url_template,
        },
    }
