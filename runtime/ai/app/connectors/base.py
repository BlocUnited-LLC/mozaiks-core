# backend/app/connectors/base.py
"""
Payment connector interfaces for MozaiksCore.

BOUNDARY CONTRACT:
- MozaiksCore does NOT process payments directly
- PaymentConnector is an abstraction for DELEGATING to external services
- In hosted mode: delegates to Mozaiks Gateway (Control Plane)
- In self-host mode: uses mock connector (no real payments)
- Direct Stripe/PayPal integration is FORBIDDEN in this repo

The Control Plane (.NET) owns billing. This runtime only:
1. Checks subscription status (via gateway or config)
2. Redirects to hosted checkout (via gateway)
3. Enforces feature gating based on subscription state
"""
from __future__ import annotations

from abc import ABC, abstractmethod
from typing import Any, Literal

from pydantic import BaseModel, ConfigDict, Field


class SDKModel(BaseModel):
    model_config = ConfigDict(extra="allow")


class OkResponse(SDKModel):
    ok: bool = True


SubscriptionScope = Literal["platform", "app"]


class CheckoutRequest(SDKModel):
    # "platform" = platform-wide subscription, "app" = specific app subscription.
    scope: SubscriptionScope | None = None
    appId: str | None = None
    plan: str | None = None
    metadata: dict[str, Any] = Field(default_factory=dict)


class CheckoutResponse(SDKModel):
    checkoutUrl: str
    sessionId: str


class PaymentStatus(SDKModel):
    scope: SubscriptionScope | None = None
    appId: str | None = None
    active: bool = True
    status: str | None = "active"
    plan: str | None = None
    currentPeriodEnd: str | None = None
    cancelAtPeriodEnd: bool | None = None
    unavailableReason: str | None = None


class CancelRequest(SDKModel):
    scope: SubscriptionScope
    appId: str | None = None


class PaymentConnector(ABC):
    @abstractmethod
    async def checkout(
        self,
        *,
        payload: CheckoutRequest,
        correlation_id: str,
        user_jwt: str | None = None,
    ) -> CheckoutResponse: ...

    @abstractmethod
    async def subscription_status(
        self,
        *,
        scope: SubscriptionScope,
        appId: str | None,
        correlation_id: str,
        user_jwt: str | None = None,
    ) -> PaymentStatus: ...

    @abstractmethod
    async def cancel(
        self,
        *,
        payload: CancelRequest,
        correlation_id: str,
        user_jwt: str | None = None,
    ) -> OkResponse: ...
