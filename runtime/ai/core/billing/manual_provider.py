# core/billing/manual_provider.py
"""
Manual Entitlements Provider - Self-hosted with tier management.

Reads user tier assignments from a local YAML file.
No actual payment processing - admin manually manages who gets what tier.

Use this for:
- Self-hosted deployments with internal billing
- Testing different tier configurations
- Organizations with existing billing systems
"""

import logging
from datetime import datetime
from pathlib import Path
from typing import Optional, Dict, Any

import yaml

from .base import (
    IPaymentProvider,
    SubscriptionStatus,
    SubscriptionState,
    TokenBudget,
    EnforcementMode,
    CheckoutRequest,
    CheckoutResult,
    CancelResult,
    WebhookResult,
)

logger = logging.getLogger("mozaiks_core.billing.manual")

# Default tier definitions
DEFAULT_TIERS = {
    "free": {
        "name": "Free",
        "token_limit": 10000,
        "features": {
            "workflow_execution": True,
            "multi_agent": False,
            "code_execution": False,
            "function_calling": True,
            "vision": False,
        },
        "rate_limits": {
            "requests_per_minute": 10,
            "concurrent_workflows": 1,
        },
    },
    "basic": {
        "name": "Basic",
        "token_limit": 100000,
        "features": {
            "workflow_execution": True,
            "multi_agent": True,
            "code_execution": False,
            "function_calling": True,
            "vision": False,
        },
        "rate_limits": {
            "requests_per_minute": 30,
            "concurrent_workflows": 3,
        },
    },
    "pro": {
        "name": "Pro",
        "token_limit": 1000000,
        "features": {
            "workflow_execution": True,
            "multi_agent": True,
            "code_execution": True,
            "function_calling": True,
            "vision": True,
        },
        "rate_limits": {
            "requests_per_minute": 60,
            "concurrent_workflows": 10,
        },
    },
    "unlimited": {
        "name": "Unlimited",
        "token_limit": -1,
        "features": {
            "workflow_execution": True,
            "multi_agent": True,
            "code_execution": True,
            "function_calling": True,
            "vision": True,
            "file_upload": True,
            "streaming": True,
        },
        "rate_limits": {
            "requests_per_minute": -1,
            "concurrent_workflows": -1,
        },
    },
}


class ManualEntitlementsProvider(IPaymentProvider):
    """
    Manual entitlements provider for self-hosted deployments.
    
    Reads tier assignments from a YAML file:
    
    ```yaml
    # entitlements.yaml
    tiers:
      free: { ... }  # Optional tier overrides
      
    users:
      user_123:
        tier: pro
        expires_at: 2026-12-31
      user_456:
        tier: basic
        
    apps:
      app_789:
        default_tier: pro
        user_overrides:
          user_999: unlimited
    ```
    
    If no config exists, defaults to 'free' tier.
    
    Example:
        provider = ManualEntitlementsProvider("/config/entitlements.yaml")
        status = await provider.get_subscription_status("user_123", "app_456")
    """
    
    def __init__(
        self,
        config_path: Optional[str] = None,
        default_tier: str = "free",
    ):
        """
        Initialize the manual provider.
        
        Args:
            config_path: Path to entitlements YAML file
            default_tier: Default tier if user not found
        """
        self._config_path = Path(config_path) if config_path else None
        self._default_tier = default_tier
        self._config: Dict[str, Any] = {}
        self._config_mtime: float = 0
        
        self._load_config()
    
    @property
    def provider_id(self) -> str:
        return "manual"
    
    def _load_config(self) -> None:
        """Load or reload config from YAML file."""
        if not self._config_path or not self._config_path.exists():
            logger.debug("No config file, using defaults")
            self._config = {"tiers": DEFAULT_TIERS, "users": {}, "apps": {}}
            return
        
        try:
            mtime = self._config_path.stat().st_mtime
            if mtime > self._config_mtime:
                with open(self._config_path, "r") as f:
                    self._config = yaml.safe_load(f) or {}
                self._config_mtime = mtime
                
                # Merge with defaults
                self._config.setdefault("tiers", {})
                for tier_id, tier_def in DEFAULT_TIERS.items():
                    if tier_id not in self._config["tiers"]:
                        self._config["tiers"][tier_id] = tier_def
                
                self._config.setdefault("users", {})
                self._config.setdefault("apps", {})
                
                logger.info("Loaded entitlements config from %s", self._config_path)
                
        except Exception as e:
            logger.error("Failed to load config: %s", e)
            self._config = {"tiers": DEFAULT_TIERS, "users": {}, "apps": {}}
    
    def _get_user_tier(self, user_id: str, app_id: str) -> str:
        """Determine user's tier for an app."""
        self._load_config()  # Check for updates
        
        # Check app-specific user override
        app_config = self._config.get("apps", {}).get(app_id, {})
        user_overrides = app_config.get("user_overrides", {})
        if user_id in user_overrides:
            return user_overrides[user_id]
        
        # Check global user tier
        user_config = self._config.get("users", {}).get(user_id, {})
        if isinstance(user_config, dict) and "tier" in user_config:
            # Check expiration
            expires_at = user_config.get("expires_at")
            if expires_at:
                if isinstance(expires_at, str):
                    expires_at = datetime.fromisoformat(expires_at)
                if datetime.utcnow() > expires_at:
                    logger.info("User %s tier expired, reverting to default", user_id)
                    return self._default_tier
            return user_config["tier"]
        elif isinstance(user_config, str):
            return user_config
        
        # Check app default tier
        if "default_tier" in app_config:
            return app_config["default_tier"]
        
        return self._default_tier
    
    def _get_tier_config(self, tier_id: str) -> Dict[str, Any]:
        """Get tier configuration."""
        tiers = self._config.get("tiers", DEFAULT_TIERS)
        return tiers.get(tier_id, tiers.get(self._default_tier, DEFAULT_TIERS["free"]))
    
    async def get_subscription_status(
        self,
        user_id: str,
        app_id: str,
    ) -> SubscriptionStatus:
        """
        Get subscription status from manual config.
        
        Args:
            user_id: User identifier
            app_id: Application identifier
            
        Returns:
            Subscription status based on config file
        """
        tier_id = self._get_user_tier(user_id, app_id)
        tier_config = self._get_tier_config(tier_id)
        
        logger.debug(
            "Manual: user=%s app=%s tier=%s",
            user_id, app_id, tier_id
        )
        
        token_limit = tier_config.get("token_limit", 10000)
        
        return SubscriptionStatus(
            tier=tier_id,
            state=SubscriptionState.ACTIVE,
            app_id=app_id,
            user_id=user_id,
            plan_name=tier_config.get("name", tier_id.title()),
            billing_period="none",  # No billing
            expires_at=None,
            token_budget=TokenBudget(
                limit=token_limit,
                used=0,  # Usage tracked separately
                period="monthly",
                enforcement=EnforcementMode.SOFT,
            ),
            features=tier_config.get("features", {}),
            rate_limits=tier_config.get("rate_limits", {}),
            metadata={
                "provider": "manual",
                "config_path": str(self._config_path) if self._config_path else None,
            },
        )
    
    async def create_checkout(
        self,
        request: CheckoutRequest,
    ) -> CheckoutResult:
        """
        Manual: No checkout, admin assigns tiers.
        
        Returns instructions to contact admin.
        """
        logger.info(
            "Manual: checkout requested - admin must assign tier (user=%s, plan=%s)",
            request.user_id, request.plan_id
        )
        
        return CheckoutResult(
            checkout_id="manual_contact_admin",
            checkout_url="",  # No URL
            expires_at=None,
            client_secret=None,
        )
    
    async def cancel_subscription(
        self,
        user_id: str,
        app_id: str,
    ) -> CancelResult:
        """
        Manual: Admin must remove tier assignment.
        """
        logger.info(
            "Manual: cancel requested - admin must update config (user=%s)",
            user_id
        )
        
        return CancelResult(
            success=False,
            error="Contact administrator to change subscription",
        )
    
    async def process_webhook(
        self,
        payload: str,
        signature: str,
    ) -> WebhookResult:
        """Manual: No webhooks."""
        return WebhookResult(success=True, events=[])
    
    async def health_check(self) -> bool:
        """Check if config file is readable."""
        if self._config_path and self._config_path.exists():
            try:
                self._load_config()
                return True
            except Exception:
                return False
        return True  # No config file is fine
