from __future__ import annotations

import logging
from typing import Any, Dict, Optional

from mozaiks_platform.subscription_manager import subscription_manager
from mozaiks_infra.utils.log_sanitizer import sanitize_for_log

logger = logging.getLogger("mozaiks_core.workflows.subscription_manager")


def _context_to_dict(context_variables: Any) -> Dict[str, Any]:
    if context_variables is None:
        return {}
    if isinstance(context_variables, dict):
        return context_variables
    if hasattr(context_variables, "to_dict"):
        try:
            return context_variables.to_dict()
        except Exception:
            return {}
    if hasattr(context_variables, "data") and isinstance(getattr(context_variables, "data", None), dict):
        return getattr(context_variables, "data")
    return {}


def _resolve_user_id(context_variables: Any) -> Optional[str]:
    context = _context_to_dict(context_variables)
    user_id = context.get("user_id") or context.get("userId")
    if user_id is None:
        return None
    user_id = str(user_id).strip()
    return user_id or None


async def get_subscription(context_variables: Any = None) -> Dict[str, Any]:
    """Return the current user's subscription status."""
    user_id = _resolve_user_id(context_variables)
    if not user_id:
        return {"error": "user_id missing from context"}

    logger.info("Fetching subscription for user %s", sanitize_for_log(user_id))
    return await subscription_manager.get_user_subscription(user_id)


async def list_plans() -> Dict[str, Any]:
    """Return available subscription plans."""
    plans = subscription_manager.get_available_plans()
    return {"plans": plans}


async def check_plugin_access(plugin_name: str, context_variables: Any = None) -> Dict[str, Any]:
    """Check if the current user can access a specific plugin."""
    user_id = _resolve_user_id(context_variables)
    plugin = (plugin_name or "").strip()
    if not user_id:
        return {"error": "user_id missing from context"}
    if not plugin:
        return {"error": "plugin_name is required"}

    has_access = await subscription_manager.is_plugin_accessible(user_id, plugin)
    return {"plugin": plugin, "access": bool(has_access)}
