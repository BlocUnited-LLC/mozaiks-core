# 💳 Billing API Specification

> For **mozaiks-platform** connectors to call MozaiksCore's Billing.API.

---

## 📍 Base URL

```
Production: https://core.mozaiks.com
Development: http://localhost:8080
```

---

## 🔑 Authentication

```http
Authorization: Bearer {user_jwt}
# OR
X-Mozaiks-App-Key: {app_api_key}
```

---

## 📡 Endpoints

### Get Subscription Status

```http
GET /api/billing/subscription
Authorization: Bearer {token}
```

**Query Parameters:**
| Param | Type | Description |
|-------|------|-------------|
| `scope` | string | `platform` or `app` |
| `app_id` | string | Required if scope=app |

**Response:**
```json
{
    "user_id": "abc123",
    "scope": "platform",
    "status": "active",
    "tier": "premium",
    "current_period_start": "2025-10-01T00:00:00Z",
    "current_period_end": "2025-11-01T00:00:00Z",
    "cancel_at_period_end": false,
    "features": [
        "unlimited_apps",
        "custom_domains",
        "priority_support"
    ]
}
```

**Status Values:**
| Status | Description |
|--------|-------------|
| `active` | Subscription is active |
| `trialing` | In trial period |
| `past_due` | Payment failed, grace period |
| `canceled` | Canceled, access until period end |
| `inactive` | No active subscription |

---

### Check Feature Access

```http
GET /api/billing/features/{feature_id}
Authorization: Bearer {token}
```

**Response:**
```json
{
    "feature": "custom_domains",
    "allowed": true,
    "limit": null,
    "usage": null
}
```

**With usage limits:**
```json
{
    "feature": "api_calls",
    "allowed": true,
    "limit": 10000,
    "usage": 3500,
    "reset_at": "2025-11-01T00:00:00Z"
}
```

---

### Batch Check Features

```http
POST /api/billing/features/check
Authorization: Bearer {token}
Content-Type: application/json

{
    "features": ["custom_domains", "api_calls", "team_members"]
}
```

**Response:**
```json
{
    "results": {
        "custom_domains": {
            "allowed": true,
            "limit": null
        },
        "api_calls": {
            "allowed": true,
            "limit": 10000,
            "usage": 3500
        },
        "team_members": {
            "allowed": false,
            "reason": "Requires Business tier"
        }
    }
}
```

---

### Get Usage Metrics

```http
GET /api/billing/usage
Authorization: Bearer {token}
```

**Query Parameters:**
| Param | Type | Description |
|-------|------|-------------|
| `period` | string | `current`, `last_month`, `all_time` |

**Response:**
```json
{
    "user_id": "abc123",
    "period": "current",
    "period_start": "2025-10-01T00:00:00Z",
    "period_end": "2025-11-01T00:00:00Z",
    "metrics": {
        "api_calls": {
            "used": 3500,
            "limit": 10000,
            "percentage": 35
        },
        "storage_mb": {
            "used": 250,
            "limit": 1000,
            "percentage": 25
        },
        "ai_tokens": {
            "used": 50000,
            "limit": 100000,
            "percentage": 50
        }
    }
}
```

---

### Create Checkout Session

```http
POST /api/billing/checkout
Authorization: Bearer {token}
Content-Type: application/json

{
    "scope": "platform",
    "plan": "premium",
    "success_url": "https://app.example.com/billing/success",
    "cancel_url": "https://app.example.com/billing/cancel"
}
```

**Response:**
```json
{
    "checkout_url": "https://checkout.stripe.com/...",
    "session_id": "cs_xxx"
}
```

---

### Cancel Subscription

```http
POST /api/billing/cancel
Authorization: Bearer {token}
Content-Type: application/json

{
    "scope": "platform",
    "reason": "Too expensive"
}
```

**Response:**
```json
{
    "success": true,
    "cancel_at": "2025-11-01T00:00:00Z",
    "message": "Your subscription will remain active until the end of the current billing period."
}
```

---

### Get Invoice History

```http
GET /api/billing/invoices
Authorization: Bearer {token}
```

**Response:**
```json
{
    "invoices": [
        {
            "id": "inv_xxx",
            "amount": 2900,
            "currency": "usd",
            "status": "paid",
            "created_at": "2025-10-01T00:00:00Z",
            "pdf_url": "https://..."
        }
    ]
}
```

---

## 🐍 Python Connector

```python
# runtime/connectors/billing.py
from __future__ import annotations
from dataclasses import dataclass, field
from typing import Protocol, Any, Literal
from .base import PlatformHttpClient


SubscriptionStatus = Literal["active", "trialing", "past_due", "canceled", "inactive"]
Scope = Literal["platform", "app"]


@dataclass
class Subscription:
    user_id: str
    scope: Scope
    status: SubscriptionStatus
    tier: str | None = None
    current_period_start: str | None = None
    current_period_end: str | None = None
    cancel_at_period_end: bool = False
    features: list[str] = field(default_factory=list)


@dataclass
class FeatureAccess:
    feature: str
    allowed: bool
    limit: int | None = None
    usage: int | None = None
    reason: str | None = None


@dataclass
class UsageMetric:
    used: int
    limit: int
    percentage: float


@dataclass
class UsageMetrics:
    user_id: str
    period: str
    period_start: str
    period_end: str
    metrics: dict[str, UsageMetric]


@dataclass
class CheckoutResponse:
    checkout_url: str
    session_id: str


class BillingConnector(Protocol):
    """Billing API connector interface."""
    
    async def get_subscription_status(
        self,
        *,
        user_jwt: str,
        scope: Scope = "platform",
        app_id: str | None = None
    ) -> Subscription: ...
    
    async def check_feature_access(
        self,
        feature: str,
        *,
        user_jwt: str
    ) -> FeatureAccess: ...
    
    async def check_features(
        self,
        features: list[str],
        *,
        user_jwt: str
    ) -> dict[str, FeatureAccess]: ...
    
    async def get_usage(
        self,
        *,
        user_jwt: str,
        period: str = "current"
    ) -> UsageMetrics: ...
    
    async def create_checkout(
        self,
        *,
        user_jwt: str,
        scope: Scope,
        plan: str,
        success_url: str,
        cancel_url: str
    ) -> CheckoutResponse: ...


class ManagedBillingConnector:
    """Calls MozaiksCore Billing.API."""
    
    def __init__(self, http: PlatformHttpClient):
        self._http = http
    
    async def get_subscription_status(
        self,
        *,
        user_jwt: str,
        scope: Scope = "platform",
        app_id: str | None = None
    ) -> Subscription:
        params = {"scope": scope}
        if app_id:
            params["app_id"] = app_id
        
        response = await self._http.get(
            "/api/billing/subscription",
            params=params,
            user_jwt=user_jwt
        )
        return Subscription(**response)
    
    async def check_feature_access(
        self,
        feature: str,
        *,
        user_jwt: str
    ) -> FeatureAccess:
        response = await self._http.get(
            f"/api/billing/features/{feature}",
            user_jwt=user_jwt
        )
        return FeatureAccess(**response)
    
    async def check_features(
        self,
        features: list[str],
        *,
        user_jwt: str
    ) -> dict[str, FeatureAccess]:
        response = await self._http.post(
            "/api/billing/features/check",
            json_body={"features": features},
            user_jwt=user_jwt
        )
        return {
            k: FeatureAccess(feature=k, **v)
            for k, v in response["results"].items()
        }
    
    async def get_usage(
        self,
        *,
        user_jwt: str,
        period: str = "current"
    ) -> UsageMetrics:
        response = await self._http.get(
            "/api/billing/usage",
            params={"period": period},
            user_jwt=user_jwt
        )
        metrics = {
            k: UsageMetric(**v)
            for k, v in response["metrics"].items()
        }
        return UsageMetrics(
            user_id=response["user_id"],
            period=response["period"],
            period_start=response["period_start"],
            period_end=response["period_end"],
            metrics=metrics
        )
    
    async def create_checkout(
        self,
        *,
        user_jwt: str,
        scope: Scope,
        plan: str,
        success_url: str,
        cancel_url: str
    ) -> CheckoutResponse:
        response = await self._http.post(
            "/api/billing/checkout",
            json_body={
                "scope": scope,
                "plan": plan,
                "success_url": success_url,
                "cancel_url": cancel_url
            },
            user_jwt=user_jwt
        )
        return CheckoutResponse(**response)


class MockBillingConnector:
    """Mock connector for self-hosted mode."""
    
    async def get_subscription_status(
        self, *, user_jwt: str, scope: Scope = "platform", app_id: str | None = None
    ) -> Subscription:
        return Subscription(
            user_id="mock_user",
            scope=scope,
            status="active",
            tier="premium",
            features=["all_features"]
        )
    
    async def check_feature_access(
        self, feature: str, *, user_jwt: str
    ) -> FeatureAccess:
        return FeatureAccess(feature=feature, allowed=True)
    
    async def check_features(
        self, features: list[str], *, user_jwt: str
    ) -> dict[str, FeatureAccess]:
        return {f: FeatureAccess(feature=f, allowed=True) for f in features}
    
    async def get_usage(
        self, *, user_jwt: str, period: str = "current"
    ) -> UsageMetrics:
        return UsageMetrics(
            user_id="mock_user",
            period=period,
            period_start="2025-01-01T00:00:00Z",
            period_end="2025-02-01T00:00:00Z",
            metrics={}
        )
    
    async def create_checkout(
        self, *, user_jwt: str, scope: Scope, plan: str, success_url: str, cancel_url: str
    ) -> CheckoutResponse:
        return CheckoutResponse(
            checkout_url=success_url,
            session_id="mock_session"
        )
```

---

## 📊 Subscription Tiers

| Tier | Features |
|------|----------|
| `free` | Basic features, limited usage |
| `starter` | More limits, email support |
| `premium` | Higher limits, custom domains |
| `business` | Team features, priority support |
| `enterprise` | Unlimited, SLA, dedicated support |

---

## 🔗 Related

- 📖 [Platform Integration Overview](./overview.md)
- 📖 [Identity API Spec](./identity-api.md)
- 🔐 [Core Authentication](../core/authentication.md)
