# core/billing/platform_provider.py
"""
Platform Payment Provider - Delegates to mozaiks-platform Payment.API.

This is the provider for hosted deployments on mozaiks.ai.
All billing operations are delegated to the Platform via HTTP.

Requires:
- MOZAIKS_PLATFORM_URL: Base URL for Platform API
- MOZAIKS_PLATFORM_CLIENT_ID: Service client ID
- MOZAIKS_PLATFORM_CLIENT_SECRET: Service client secret
"""

import logging
import os
from datetime import datetime
from typing import Optional, Dict, Any

import httpx

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
    WebhookEvent,
)

logger = logging.getLogger("mozaiks_core.billing.platform")

# Default Platform URL
DEFAULT_PLATFORM_URL = "https://api.mozaiks.ai"


class PlatformPaymentProvider(IPaymentProvider):
    """
    Payment provider that delegates to mozaiks-platform.
    
    All billing operations are sent to Platform's Payment.API via HTTP.
    This is the production provider for hosted deployments.
    
    Configuration via environment:
        MOZAIKS_PLATFORM_URL: Platform API base URL
        MOZAIKS_PLATFORM_CLIENT_ID: Service client ID
        MOZAIKS_PLATFORM_CLIENT_SECRET: Service client secret
        MOZAIKS_PLATFORM_TOKEN_SCOPE: Optional OAuth2 scope string
    
    Example:
        provider = PlatformPaymentProvider()
        status = await provider.get_subscription_status("user_123", "app_456")
    """
    
    def __init__(
        self,
        platform_url: Optional[str] = None,
        client_id: Optional[str] = None,
        client_secret: Optional[str] = None,
        token_scope: Optional[str] = None,
        timeout: float = 30.0,
    ):
        """
        Initialize Platform provider.
        
        Args:
            platform_url: Platform API base URL (or from env)
            client_id: Service client id (or from env)
            client_secret: Service client secret (or from env)
            token_scope: Optional OAuth2 scope (or from env)
            timeout: HTTP request timeout in seconds
        """
        self._platform_url = (
            platform_url 
            or os.getenv("MOZAIKS_PLATFORM_URL") 
            or DEFAULT_PLATFORM_URL
        ).rstrip("/")

        self._client_id = client_id or os.getenv("MOZAIKS_PLATFORM_CLIENT_ID")
        self._client_secret = client_secret or os.getenv("MOZAIKS_PLATFORM_CLIENT_SECRET")
        self._token_scope = token_scope or os.getenv("MOZAIKS_PLATFORM_TOKEN_SCOPE")
        self._timeout = timeout

        self._token_provider = None
        
        # HTTP client (lazy init)
        self._client: Optional[httpx.AsyncClient] = None
    
    @property
    def provider_id(self) -> str:
        return "platform"
    
    async def _get_client(self) -> httpx.AsyncClient:
        """Get or create HTTP client."""
        if self._client is None:
            self._client = httpx.AsyncClient(
                base_url=self._platform_url,
                timeout=self._timeout,
            )
        return self._client

    async def _get_service_token(self) -> str:
        """Get a client-credentials access token for service-to-service requests."""
        if self._token_provider is None:
            from core.ai_runtime.auth.client_credentials import ClientCredentialsTokenProvider

            self._token_provider = ClientCredentialsTokenProvider(
                client_id=self._client_id or "",
                client_secret=self._client_secret or "",
                scope=self._token_scope,
            )

        return await self._token_provider.get_access_token()
    
    async def _request(
        self,
        method: str,
        path: str,
        user_token: Optional[str] = None,
        correlation_id: Optional[str] = None,
        **kwargs,
    ) -> httpx.Response:
        """Make authenticated request to Platform."""
        client = await self._get_client()
        
        headers = kwargs.pop("headers", {})
        
        if user_token:
            # Use user's token for user-scoped operations
            headers["Authorization"] = f"Bearer {user_token}"
        else:
            # Service-to-service call: use client-credentials JWT
            access_token = await self._get_service_token()
            headers["Authorization"] = f"Bearer {access_token}"
        
        headers["X-Mozaiks-Service"] = "core"
        if correlation_id:
            headers["X-Mozaiks-Correlation-Id"] = correlation_id
        
        response = await client.request(method, path, headers=headers, **kwargs)
        
        if response.status_code >= 400:
            logger.warning(
                "Platform API error: %s %s -> %d: %s",
                method, path, response.status_code, response.text[:200]
            )
        
        return response
    
    async def get_subscription_status(
        self,
        user_id: str,
        app_id: str,
    ) -> SubscriptionStatus:
        """
        Get subscription status from Platform.
        
        Args:
            user_id: User identifier
            app_id: Application identifier
            
        Returns:
            Subscription status from Platform
        """
        try:
            response = await self._request(
                "GET",
                f"/api/billing/subscription",
                params={"user_id": user_id, "app_id": app_id},
            )
            
            if response.status_code == 404:
                # No subscription = free tier
                logger.debug("No subscription found for user=%s app=%s, returning free tier", user_id, app_id)
                return self._default_free_status(user_id, app_id)
            
            response.raise_for_status()
            data = response.json()
            
            return self._parse_subscription_response(data, user_id, app_id)
            
        except httpx.HTTPError as e:
            logger.error("Failed to get subscription from Platform: %s", e)
            # Fail open with limited free tier
            return self._default_free_status(user_id, app_id)
    
    def _default_free_status(self, user_id: str, app_id: str) -> SubscriptionStatus:
        """Return default free tier when Platform unavailable."""
        return SubscriptionStatus(
            tier="free",
            state=SubscriptionState.ACTIVE,
            app_id=app_id,
            user_id=user_id,
            plan_name="Free",
            billing_period="none",
            token_budget=TokenBudget(
                limit=10000,
                used=0,
                period="monthly",
                enforcement=EnforcementMode.SOFT,
            ),
            features={
                "workflow_execution": True,
                "multi_agent": False,
                "code_execution": False,
                "function_calling": True,
                "vision": False,
            },
            rate_limits={
                "requests_per_minute": 10,
                "concurrent_workflows": 1,
            },
            metadata={
                "provider": "platform",
                "fallback": True,
            },
        )
    
    def _parse_subscription_response(
        self,
        data: Dict[str, Any],
        user_id: str,
        app_id: str,
    ) -> SubscriptionStatus:
        """Parse Platform subscription response into SubscriptionStatus."""
        plan = data.get("plan", {})
        token_budget_data = data.get("token_budget", {})
        
        # Parse token budget
        total_tokens = token_budget_data.get("total_tokens", {})
        token_budget = TokenBudget(
            limit=total_tokens.get("limit", -1),
            used=total_tokens.get("used", 0),
            period=token_budget_data.get("period", "monthly"),
            enforcement=EnforcementMode(total_tokens.get("enforcement", "soft")),
        )
        
        # Parse expires_at
        expires_at = None
        if plan.get("expires_at"):
            try:
                expires_at = datetime.fromisoformat(plan["expires_at"].replace("Z", "+00:00"))
            except (ValueError, AttributeError):
                pass
        
        # Parse state
        state_str = data.get("state", "active").lower()
        state_map = {
            "active": SubscriptionState.ACTIVE,
            "trialing": SubscriptionState.TRIALING,
            "past_due": SubscriptionState.PAST_DUE,
            "canceled": SubscriptionState.CANCELED,
            "expired": SubscriptionState.EXPIRED,
        }
        state = state_map.get(state_str, SubscriptionState.ACTIVE)
        
        return SubscriptionStatus(
            tier=plan.get("tier", "free"),
            state=state,
            app_id=app_id,
            user_id=user_id,
            plan_name=plan.get("name", "Unknown"),
            billing_period=plan.get("billing_period", "monthly"),
            expires_at=expires_at,
            token_budget=token_budget,
            features=data.get("features", {}),
            rate_limits=data.get("rate_limits", {}),
            metadata={
                "provider": "platform",
                "plan_id": plan.get("id"),
                "subscription_id": data.get("subscription_id"),
            },
        )
    
    async def create_checkout(
        self,
        request: CheckoutRequest,
    ) -> CheckoutResult:
        """
        Create checkout session via Platform.
        
        Args:
            request: Checkout request details
            
        Returns:
            Checkout session with Stripe URL
        """
        try:
            response = await self._request(
                "POST",
                "/api/billing/checkout",
                json={
                    "user_id": request.user_id,
                    "app_id": request.app_id,
                    "plan_id": request.plan_id,
                    "billing_period": request.billing_period,
                    "success_url": request.success_url,
                    "cancel_url": request.cancel_url,
                    "metadata": request.metadata,
                },
            )
            response.raise_for_status()
            data = response.json()
            
            expires_at = None
            if data.get("expires_at"):
                try:
                    expires_at = datetime.fromisoformat(data["expires_at"].replace("Z", "+00:00"))
                except (ValueError, AttributeError):
                    pass
            
            return CheckoutResult(
                checkout_id=data.get("checkout_id", ""),
                checkout_url=data.get("checkout_url", ""),
                expires_at=expires_at,
                client_secret=data.get("client_secret"),
            )
            
        except httpx.HTTPError as e:
            logger.error("Failed to create checkout: %s", e)
            return CheckoutResult(
                checkout_id="",
                checkout_url="",
                client_secret=None,
            )
    
    async def cancel_subscription(
        self,
        user_id: str,
        app_id: str,
    ) -> CancelResult:
        """
        Cancel subscription via Platform.
        
        Args:
            user_id: User identifier
            app_id: Application identifier
            
        Returns:
            Cancellation result
        """
        try:
            response = await self._request(
                "POST",
                "/api/billing/subscription/cancel",
                json={
                    "user_id": user_id,
                    "app_id": app_id,
                },
            )
            response.raise_for_status()
            data = response.json()
            
            canceled_at = None
            effective_at = None
            
            if data.get("canceled_at"):
                try:
                    canceled_at = datetime.fromisoformat(data["canceled_at"].replace("Z", "+00:00"))
                except (ValueError, AttributeError):
                    pass
            
            if data.get("effective_at"):
                try:
                    effective_at = datetime.fromisoformat(data["effective_at"].replace("Z", "+00:00"))
                except (ValueError, AttributeError):
                    pass
            
            return CancelResult(
                success=data.get("success", True),
                canceled_at=canceled_at,
                effective_at=effective_at,
                error=data.get("error"),
            )
            
        except httpx.HTTPError as e:
            logger.error("Failed to cancel subscription: %s", e)
            return CancelResult(
                success=False,
                error=str(e),
            )
    
    async def process_webhook(
        self,
        payload: str,
        signature: str,
    ) -> WebhookResult:
        """
        Process webhook from Platform.
        
        Note: Platform handles Stripe webhooks directly.
        This is for Platform → Core notifications.
        
        Args:
            payload: Raw webhook payload
            signature: HMAC signature for validation
            
        Returns:
            Parsed webhook events
        """
        # TODO: Implement webhook validation and parsing
        # Platform will call Core's /api/v1/entitlements/{app_id}/sync endpoint directly
        # This method is for future use if we need webhook-based notifications
        
        logger.debug("Platform webhook received, length=%d", len(payload))
        
        return WebhookResult(
            success=True,
            events=[],
            error=None,
        )
    
    async def health_check(self) -> bool:
        """Check if Platform is reachable."""
        try:
            client = await self._get_client()
            response = await client.get("/health", timeout=5.0)
            return response.status_code == 200
        except Exception as e:
            logger.warning("Platform health check failed: %s", e)
            return False
    
    async def close(self) -> None:
        """Close HTTP client."""
        if self._client:
            await self._client.aclose()
            self._client = None
