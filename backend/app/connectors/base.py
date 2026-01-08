# backend/app/connectors/base.py
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
