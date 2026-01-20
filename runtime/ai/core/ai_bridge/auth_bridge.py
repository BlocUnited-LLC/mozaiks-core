"""
Auth Bridge

Bridges mozaiks-core's authentication with the MozaiksAI runtime.
This allows the runtime to leverage core's JWT validation and user context.
"""

import logging
import os
from typing import Optional, Dict, Any

logger = logging.getLogger("mozaiks_core.ai_bridge.auth_bridge")


def bridge_auth_to_runtime(
    user_id: str,
    app_id: str,
    *,
    token: Optional[str] = None,
    roles: Optional[list] = None,
    subscription_tier: Optional[str] = None,
) -> Dict[str, Any]:
    """
    Create a runtime-compatible auth context from core's auth data.
    
    This bridges the gap between:
    - MozaiksCore's JWT/OIDC auth (via security/authentication.py)
    - MozaiksAI runtime's auth expectations (mozaiksai.core.auth)
    
    Args:
        user_id: The authenticated user's ID (from JWT sub claim)
        app_id: The app context (MOZAIKS_APP_ID)
        token: Optional JWT token for forwarding
        roles: User roles from JWT
        subscription_tier: User's subscription level
        
    Returns:
        Dict with runtime-compatible auth context
    """
    return {
        "user_id": user_id,
        "app_id": app_id,
        "token": token,
        "roles": roles or [],
        "subscription_tier": subscription_tier or "free",
        "auth_source": "mozaiks_core",
    }


def validate_runtime_access(
    user_id: str,
    app_id: str,
    workflow_name: str,
    *,
    subscription_tier: Optional[str] = None,
) -> tuple[bool, Optional[str]]:
    """
    Check if a user can access a specific AI workflow.
    
    This integrates with core's subscription gating to ensure
    users have appropriate entitlements for AI features.
    
    Args:
        user_id: The user requesting access
        app_id: The app context
        workflow_name: The workflow to access
        subscription_tier: User's current tier
        
    Returns:
        Tuple of (allowed, denial_reason)
    """
    # Check if AI features are enabled globally
    ai_enabled = os.getenv("MOZAIKS_AI_ENABLED", "true").lower() == "true"
    if not ai_enabled:
        return False, "AI features are disabled"
    
    # TODO: Integrate with subscription_manager for tier-based gating
    # For now, allow access to all authenticated users
    
    # Check workflow-specific restrictions
    restricted_workflows = os.getenv("MOZAIKS_RESTRICTED_WORKFLOWS", "").split(",")
    restricted_workflows = [w.strip() for w in restricted_workflows if w.strip()]
    
    if workflow_name in restricted_workflows:
        # Check if user has elevated access
        premium_tiers = ["premium", "enterprise", "admin"]
        if subscription_tier not in premium_tiers:
            return False, f"Workflow '{workflow_name}' requires premium subscription"
    
    return True, None


def get_runtime_auth_headers(token: str) -> Dict[str, str]:
    """
    Build headers for authenticating with the AI runtime.
    
    When core makes HTTP calls to the runtime, these headers
    establish the authenticated context.
    """
    return {
        "Authorization": f"Bearer {token}",
        "X-Auth-Source": "mozaiks-core",
    }
