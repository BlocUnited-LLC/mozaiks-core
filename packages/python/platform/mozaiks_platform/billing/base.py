# core/billing/base.py
"""
Base types and interfaces for the billing module.

These define the contract that all payment providers must implement.
"""

from abc import ABC, abstractmethod
from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum
from typing import Optional, Dict, Any, List


class SubscriptionTier(str, Enum):
    """Standard subscription tiers."""
    FREE = "free"
    BASIC = "basic"
    PRO = "pro"
    ENTERPRISE = "enterprise"
    UNLIMITED = "unlimited"  # For self-hosted NoOp


class SubscriptionState(str, Enum):
    """Subscription lifecycle states."""
    ACTIVE = "active"
    TRIALING = "trialing"
    PAST_DUE = "past_due"
    CANCELED = "canceled"
    EXPIRED = "expired"


class EnforcementMode(str, Enum):
    """How to enforce limits when exceeded."""
    HARD = "hard"      # Block the request
    SOFT = "soft"      # Allow but mark as overage
    WARN = "warn"      # Allow with warning
    NONE = "none"      # No enforcement


@dataclass
class TokenBudget:
    """Token budget configuration."""
    limit: int  # -1 for unlimited
    used: int = 0
    period: str = "monthly"  # monthly, daily, lifetime
    enforcement: EnforcementMode = EnforcementMode.SOFT
    
    @property
    def remaining(self) -> int:
        if self.limit == -1:
            return -1  # Unlimited
        return max(0, self.limit - self.used)
    
    @property
    def is_unlimited(self) -> bool:
        return self.limit == -1
    
    @property
    def is_exceeded(self) -> bool:
        if self.is_unlimited:
            return False
        return self.used >= self.limit


@dataclass
class SubscriptionStatus:
    """Current subscription status for a user/app."""
    tier: str
    state: SubscriptionState
    app_id: str
    user_id: str
    plan_name: str = ""
    billing_period: str = "monthly"
    expires_at: Optional[datetime] = None
    token_budget: Optional[TokenBudget] = None
    features: Dict[str, bool] = field(default_factory=dict)
    rate_limits: Dict[str, int] = field(default_factory=dict)
    metadata: Dict[str, Any] = field(default_factory=dict)
    
    def has_feature(self, feature: str) -> bool:
        """Check if feature is enabled. Defaults to True for unlisted features."""
        return self.features.get(feature, True)
    
    def get_rate_limit(self, key: str, default: int = -1) -> int:
        """Get rate limit value. -1 means unlimited."""
        return self.rate_limits.get(key, default)


@dataclass
class CheckoutRequest:
    """Request to create a checkout session."""
    user_id: str
    app_id: str
    plan_id: str
    billing_period: str = "monthly"  # monthly, yearly
    success_url: Optional[str] = None
    cancel_url: Optional[str] = None
    metadata: Dict[str, Any] = field(default_factory=dict)


@dataclass
class CheckoutResult:
    """Result of creating a checkout session."""
    checkout_id: str
    checkout_url: str
    expires_at: Optional[datetime] = None
    client_secret: Optional[str] = None  # For Stripe Elements


@dataclass
class CancelResult:
    """Result of canceling a subscription."""
    success: bool
    canceled_at: Optional[datetime] = None
    effective_at: Optional[datetime] = None  # When access ends
    error: Optional[str] = None


@dataclass
class WebhookEvent:
    """Parsed webhook event from payment provider."""
    event_type: str  # subscription.created, subscription.updated, etc.
    subscription_id: str
    user_id: str
    app_id: Optional[str] = None
    data: Dict[str, Any] = field(default_factory=dict)
    timestamp: datetime = field(default_factory=datetime.utcnow)


@dataclass
class WebhookResult:
    """Result of processing a webhook."""
    success: bool
    events: List[WebhookEvent] = field(default_factory=list)
    error: Optional[str] = None


class IPaymentProvider(ABC):
    """
    Abstract payment provider interface.
    
    Implementations:
    - NoOpPaymentProvider: Everything unlimited (self-hosted default)
    - ManualEntitlementsProvider: Manual tier management via YAML
    - PlatformPaymentProvider: Delegates to mozaiks-platform
    """
    
    @property
    @abstractmethod
    def provider_id(self) -> str:
        """Unique identifier for this provider (e.g., 'noop', 'platform')."""
        pass
    
    @abstractmethod
    async def get_subscription_status(
        self,
        user_id: str,
        app_id: str,
    ) -> SubscriptionStatus:
        """
        Get current subscription status for a user/app.
        
        Args:
            user_id: User identifier
            app_id: Application identifier
            
        Returns:
            Current subscription status including tier, limits, features
        """
        pass
    
    @abstractmethod
    async def create_checkout(
        self,
        request: CheckoutRequest,
    ) -> CheckoutResult:
        """
        Create a checkout session for subscription.
        
        Args:
            request: Checkout request details
            
        Returns:
            Checkout session with URL or client secret
        """
        pass
    
    @abstractmethod
    async def cancel_subscription(
        self,
        user_id: str,
        app_id: str,
    ) -> CancelResult:
        """
        Cancel an active subscription.
        
        Args:
            user_id: User identifier
            app_id: Application identifier
            
        Returns:
            Cancellation result
        """
        pass
    
    @abstractmethod
    async def process_webhook(
        self,
        payload: str,
        signature: str,
    ) -> WebhookResult:
        """
        Process a webhook from the payment provider.
        
        Args:
            payload: Raw webhook payload
            signature: Signature header for validation
            
        Returns:
            Parsed webhook events
        """
        pass
    
    async def health_check(self) -> bool:
        """Check if the provider is healthy and reachable."""
        return True
