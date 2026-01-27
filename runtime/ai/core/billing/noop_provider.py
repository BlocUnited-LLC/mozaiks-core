# core/billing/noop_provider.py
"""
NoOp Payment Provider - Default for self-hosted deployments.

Everything is free and unlimited. No actual payment processing.
Use this for development, testing, and self-hosted OSS deployments.
"""

import logging
from datetime import datetime
from typing import Optional

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

logger = logging.getLogger("mozaiks_core.billing.noop")


class NoOpPaymentProvider(IPaymentProvider):
    """
    No-operation payment provider.
    
    Returns unlimited access for all users. No actual payment processing.
    
    Use cases:
    - Development and testing
    - Self-hosted deployments with no billing
    - Free tier with no limits
    
    Example:
        provider = NoOpPaymentProvider()
        status = await provider.get_subscription_status("user_123", "app_456")
        # status.tier = "unlimited"
        # status.token_budget.limit = -1 (unlimited)
    """
    
    @property
    def provider_id(self) -> str:
        return "noop"
    
    async def get_subscription_status(
        self,
        user_id: str,
        app_id: str,
    ) -> SubscriptionStatus:
        """
        Always returns unlimited access.
        
        Args:
            user_id: User identifier (not used)
            app_id: Application identifier
            
        Returns:
            Subscription with unlimited tier, no limits
        """
        logger.debug(
            "NoOp: returning unlimited subscription for user=%s app=%s",
            user_id, app_id
        )
        
        return SubscriptionStatus(
            tier="unlimited",
            state=SubscriptionState.ACTIVE,
            app_id=app_id,
            user_id=user_id,
            plan_name="Self-Hosted Unlimited",
            billing_period="none",
            expires_at=None,  # Never expires
            token_budget=TokenBudget(
                limit=-1,  # Unlimited
                used=0,
                period="unlimited",
                enforcement=EnforcementMode.NONE,
            ),
            features={
                # All features enabled
                "workflow_execution": True,
                "multi_agent": True,
                "code_execution": True,
                "function_calling": True,
                "vision": True,
                "file_upload": True,
                "streaming": True,
            },
            rate_limits={
                # No rate limits
                "requests_per_minute": -1,
                "concurrent_workflows": -1,
            },
            metadata={
                "provider": "noop",
                "mode": "self-hosted",
            },
        )
    
    async def create_checkout(
        self,
        request: CheckoutRequest,
    ) -> CheckoutResult:
        """
        NoOp: No checkout needed, everything is free.
        
        Returns a fake checkout that immediately activates.
        """
        logger.info(
            "NoOp: checkout requested but not needed (user=%s, app=%s)",
            request.user_id, request.app_id
        )
        
        return CheckoutResult(
            checkout_id="noop_checkout_free",
            checkout_url="",  # No URL needed
            expires_at=None,
            client_secret=None,
        )
    
    async def cancel_subscription(
        self,
        user_id: str,
        app_id: str,
    ) -> CancelResult:
        """
        NoOp: Nothing to cancel.
        
        Returns success but access continues (it's free).
        """
        logger.info(
            "NoOp: cancel requested but no effect (user=%s, app=%s)",
            user_id, app_id
        )
        
        return CancelResult(
            success=True,
            canceled_at=datetime.utcnow(),
            effective_at=None,  # Never effective, still unlimited
            error=None,
        )
    
    async def process_webhook(
        self,
        payload: str,
        signature: str,
    ) -> WebhookResult:
        """
        NoOp: No webhooks to process.
        """
        logger.debug("NoOp: webhook received but ignored")
        
        return WebhookResult(
            success=True,
            events=[],
            error=None,
        )
    
    async def health_check(self) -> bool:
        """NoOp is always healthy."""
        return True
