# core/entitlements/__init__.py
"""
Plugin-Level Entitlement System for MozaiksCore

This module provides the runtime infrastructure for plugin-level feature gating
and usage limit tracking. It works alongside the app-level entitlement manifest
defined in declarative-entitlement-system.md.

Two-Layer Architecture:
  Layer 1: App-level (entitlement_manifest.json) - token budgets, rate limits
  Layer 2: Plugin-level (entitlements.yaml) - feature flags, consumable limits

Usage Modes:
  1. MONETIZATION=0: No restrictions (OSS default)
  2. MONETIZATION=1, no YAML: Features default true, limits unlimited
  3. MONETIZATION=1, with YAML: Full enforcement

SDK Functions (all async):
  - check_feature(user_id, plugin, feature) -> bool
  - check_limit(user_id, plugin, limit_key, needed) -> (bool, int)
  - consume_limit(user_id, plugin, limit_key, amount) -> int
  - get_usage(user_id, plugin, limit_key) -> dict
  - check_action(user_id, plugin, action) -> dict

Contract Version: 1.0
"""
import logging
from typing import Optional, Tuple, Dict, Any

logger = logging.getLogger("mozaiks_core.entitlements")

# Re-export public SDK functions
from .loader import (
    load_plugin_entitlements,
    get_tier_config,
    has_entitlements_yaml,
    validate_entitlements_schema,
)

from .usage import (
    check_limit,
    consume_limit,
    get_usage,
    get_all_usage,
    reset_usage_for_period,
)

from .events import (
    emit_consumed_event,
    emit_limit_reached_event,
    emit_feature_blocked_event,
    emit_period_reset_event,
)

# Version info
__version__ = "1.0.0"
__contract_version__ = "1.0"


async def check_feature(
    user_id: str,
    plugin: str,
    feature: str,
    entitlements: Optional[Dict[str, Any]] = None
) -> bool:
    """
    Check if user has access to a feature.

    Args:
        user_id: User identifier
        plugin: Plugin name
        feature: Feature key to check
        entitlements: Optional pre-loaded entitlements dict (from _entitlements)

    Returns:
        True if user has access to the feature, False otherwise.
        In OSS mode (empty entitlements), always returns True.
    """
    # No entitlements = OSS mode = all features enabled
    if not entitlements:
        return True

    features = entitlements.get("features", {})

    # Feature not in config = default to True (OSS-friendly)
    return features.get(feature, True)


async def check_action(
    user_id: str,
    plugin: str,
    action: str,
    entitlements: Optional[Dict[str, Any]] = None,
    entitlements_config: Optional[Dict[str, Any]] = None
) -> Dict[str, Any]:
    """
    Pre-flight check for an action without consuming.

    This is the dry-run mode for UI pre-flight validation.

    Args:
        user_id: User identifier
        plugin: Plugin name
        action: Action to check
        entitlements: User's entitlements context (from _entitlements)
        entitlements_config: Plugin's entitlements.yaml config

    Returns:
        {
            "allowed": bool,
            "would_consume": dict,  # What would be consumed if action proceeds
            "blocking_reason": str | None,
            "blocking_type": "feature" | "limit" | None
        }
    """
    result = {
        "allowed": True,
        "would_consume": {},
        "blocking_reason": None,
        "blocking_type": None
    }

    # No config = no enforcement
    if not entitlements_config:
        return result

    actions_config = entitlements_config.get("actions", {})
    action_config = actions_config.get(action)

    # Action not defined in config = no enforcement for this action
    if not action_config:
        return result

    # Check required features
    required_features = action_config.get("requires_features", [])
    for feature in required_features:
        has_feature = await check_feature(user_id, plugin, feature, entitlements)
        if not has_feature:
            tier = entitlements.get("tier", "unknown") if entitlements else "unknown"
            result["allowed"] = False
            result["blocking_reason"] = f"Feature '{feature}' not available on {tier} tier"
            result["blocking_type"] = "feature"
            return result

    # Check limits
    consumes = action_config.get("consumes", {})
    limits = entitlements.get("limits", {}) if entitlements else {}

    for limit_key, amount in consumes.items():
        limit_info = limits.get(limit_key, {})
        remaining = limit_info.get("remaining", float("inf"))

        if remaining < amount:
            result["allowed"] = False
            result["blocking_reason"] = f"Insufficient {limit_key}: need {amount}, have {remaining}"
            result["blocking_type"] = "limit"
            return result

        result["would_consume"][limit_key] = amount

    return result


def build_entitlements_context(
    user_tier: str,
    plugin_name: str,
    entitlements_config: Dict[str, Any],
    usage_data: Dict[str, Dict[str, Any]]
) -> Dict[str, Any]:
    """
    Build the _entitlements context dict to inject into plugin data.

    Args:
        user_tier: User's tier for this plugin (e.g., "basic", "pro")
        plugin_name: Plugin name
        entitlements_config: Loaded entitlements.yaml config
        usage_data: Current usage data from MongoDB

    Returns:
        {
            "tier": str,
            "features": { feature_key: bool, ... },
            "limits": {
                limit_key: { "allowed": int, "used": int, "remaining": int },
                ...
            }
        }
    """
    # Get tier config (with inheritance resolved)
    tier_config = get_tier_config(entitlements_config, user_tier)

    # Build features dict
    features = tier_config.get("features", {})

    # Build limits dict with usage
    limits_config = tier_config.get("limits", {})
    limits_definitions = entitlements_config.get("limits", {})
    limits = {}

    for limit_key, allowed in limits_config.items():
        limit_def = limits_definitions.get(limit_key, {})
        limit_type = limit_def.get("type", "consumable")

        if limit_type == "cap":
            # Caps don't have usage tracking
            limits[limit_key] = {"allowed": allowed}
        else:
            # Consumables have usage tracking
            usage = usage_data.get(limit_key, {})
            used = usage.get("used", 0)
            limits[limit_key] = {
                "allowed": allowed,
                "used": used,
                "remaining": max(0, allowed - used)
            }

    return {
        "tier": user_tier,
        "features": features,
        "limits": limits
    }
