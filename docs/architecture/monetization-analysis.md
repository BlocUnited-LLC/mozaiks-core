# Monetization Framework Analysis

## Executive Summary

mozaiks-core has a **sophisticated but disconnected** monetization architecture. The pieces exist, but critical integration points are missing or incomplete.

| Component | Status | Location |
|-----------|--------|----------|
| **Payment.API (.NET)** | ✅ Well-designed | `backend/src/Billing.API/Payment.API/` |
| **Wallet/Ledger System** | ✅ Implemented | WalletService, LedgerService |
| **Stripe Integration** | ✅ Implemented | MozaiksPayService |
| **Python Subscription Manager** | ⚠️ Read-only stub | `runtime/ai/core/subscription_manager.py` |
| **Payment Connectors (Python)** | ⚠️ Abstraction only | `runtime/ai/app/connectors/` |
| **Usage Tracking → Billing** | ❌ Gap | UsageIngestClient is advisory-only |
| **Token Metering → Cost** | ❌ Gap | Tracked but not billed |
| **Subscription Config** | ❌ Minimal | Only `{"free": {"name": "Free"}}` |

---

## 🔴 CRITICAL: Core vs Platform Boundary

This section defines what belongs in **mozaiks-core** (open source, self-hostable, no platform fees) versus **mozaiks-platform** (proprietary SaaS, 3.5% platform fee).

### Guiding Principles

| Principle | Core (OSS) | Platform (SaaS) |
|-----------|------------|-----------------|
| **Revenue logic** | ❌ Never | ✅ Owns it |
| **Platform fee (3.5%)** | ❌ Never | ✅ Enforced here |
| **Payment processing** | Abstraction only | Implementation |
| **Billing mutations** | ❌ Forbidden | ✅ Authoritative |
| **Usage enforcement** | Optional/configurable | Mandatory |
| **Multi-tenancy** | App-level isolation | Tenant management |

---

### mozaiks-core (Open Source) — MUST Include

These capabilities are **required** for self-hosters to have a useful, complete runtime:

| Capability | Description | Current Status |
|------------|-------------|----------------|
| **Subscription State Reader** | Read subscription status, check access | ✅ `subscription_manager.py` |
| **Usage Tracking** | Measure tokens, costs, durations | ✅ `usage_ingest.py`, events |
| **Payment Connector Interface** | Abstraction for payment delegation | ✅ `PaymentConnector` ABC |
| **Mock Payment Connector** | Self-host mode (always active) | ✅ `MockPaymentConnector` |
| **Plugin Access Control** | Gate features by subscription state | ⚠️ Partial |
| **Wallet Balance Read** | Display balance to users | ⚠️ Not wired |
| **Subscription Sync Endpoint** | Receive state from external source | ✅ `/api/internal/subscription/sync` |
| **Configurable Quotas** | Optional limits (self-hosters can disable) | ❌ Missing |

**What Core Provides to Self-Hosters:**
```
┌─────────────────────────────────────────────────────────────┐
│                    SELF-HOSTED DEPLOYMENT                    │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │              mozaiks-core Runtime                       │ │
│  │                                                          │ │
│  │  • Full AI workflow execution                            │ │
│  │  • Plugin system                                         │ │
│  │  • Usage tracking (optional)                             │ │
│  │  • Subscription state (local or external)                │ │
│  │  • NO platform fees                                      │ │
│  │  • NO revenue sharing                                    │ │
│  │  • Self-hosters keep 100%                                │ │
│  └────────────────────────────────────────────────────────┘ │
│                           │                                  │
│                           ▼                                  │
│  ┌────────────────────────────────────────────────────────┐ │
│  │         Self-Hoster's Own Payment System                │ │
│  │                                                          │ │
│  │  • Their Stripe account (optional)                       │ │
│  │  • Their billing logic                                   │ │
│  │  • Their pricing                                         │ │
│  │  • Calls sync endpoint to update subscription state      │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

---

### mozaiks-core (Open Source) — MUST NOT Include

These are **forbidden** in the open source repository:

| Forbidden | Reason | Where It Belongs |
|-----------|--------|------------------|
| **Platform fee calculation (3.5%)** | Revenue logic | mozaiks-platform |
| **Stripe API keys / secrets** | Platform-specific | mozaiks-platform |
| **Revenue split logic** | Business model | mozaiks-platform |
| **Investor payout calculations** | Business model | mozaiks-platform |
| **Billing mutations** | Authoritative state | mozaiks-platform |
| **Subscription plan pricing** | Platform pricing | mozaiks-platform |
| **Multi-tenant billing isolation** | SaaS concern | mozaiks-platform |
| **Payment webhooks (Stripe)** | Platform integration | mozaiks-platform |
| **Usage-based billing enforcement** | Monetization | mozaiks-platform |
| **Entitlement enforcement** | Monetization | mozaiks-platform |

---

### mozaiks-platform (Proprietary) — Owns These

| Capability | Description | Integration Point |
|------------|-------------|-------------------|
| **Stripe Integration** | Real payment processing | Direct Stripe SDK |
| **3.5% Platform Fee** | Revenue on app purchases | Payment intent creation |
| **Revenue Splits** | Creator (96.5%) + Platform (3.5%) | Settlement service |
| **Investor Payouts** | Split public app revenue | Settlement service |
| **Subscription Management** | Create, update, cancel | Stripe Billing |
| **Billing Webhooks** | Handle Stripe events | Webhook controller |
| **Usage Billing** | Convert tokens → charges | Aggregation + invoicing |
| **Entitlement Enforcement** | Hard limits on usage | Pre-flight checks |
| **Subscription Sync to Core** | Push state to runtime | Internal API call |

**Platform Architecture:**
```
┌─────────────────────────────────────────────────────────────┐
│                    MOZAIKS PLATFORM (SaaS)                   │
│                                                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │              mozaiks-platform Services                   │ │
│  │                                                          │ │
│  │  • Stripe Connect (destination charges)                  │ │
│  │  • 3.5% platform fee on all transactions                 │ │
│  │  • Subscription management                               │ │
│  │  • Usage aggregation → invoicing                         │ │
│  │  • Investor revenue tracking                             │ │
│  │  • Creator payout scheduling                             │ │
│  └────────────────────────────────────────────────────────┘ │
│                           │                                  │
│                           │ Sync subscription state          │
│                           ▼                                  │
│  ┌────────────────────────────────────────────────────────┐ │
│  │              mozaiks-core Runtime                       │ │
│  │                                                          │ │
│  │  • Receives subscription state (read-only)               │ │
│  │  • Reports usage (advisory)                              │ │
│  │  • Enforces access based on state                        │ │
│  │  • DOES NOT process payments                             │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

---

### Contract Between Core and Platform

| Direction | Contract | Implementation |
|-----------|----------|----------------|
| **Platform → Core** | Subscription state sync | `POST /api/internal/subscription/sync` |
| **Core → Platform** | Usage telemetry | `UsageIngestClient` (advisory) |
| **Core → Platform** | Checkout redirect | `PaymentConnector.checkout()` returns URL |
| **Core → Platform** | Subscription status query | `PaymentConnector.subscription_status()` |

**The contract is explicit and versioned. Core never assumes platform behavior.**

---

### Decision Matrix: "Does This Belong in Core?"

Ask these questions:

| Question | Yes → Core | No → Platform |
|----------|------------|---------------|
| Does it work without internet? | ✅ | |
| Does it work without Stripe? | ✅ | |
| Is it useful for self-hosters? | ✅ | |
| Does it involve money transfer? | | ❌ |
| Does it calculate fees/splits? | | ❌ |
| Does it require platform API keys? | | ❌ |
| Is it a billing mutation? | | ❌ |

---

### Current Violations to Fix

| File | Issue | Resolution |
|------|-------|------------|
| `Payment.API/` | Contains Stripe integration | ⚠️ Move to platform OR make it platform-only deployment |
| `MozaiksPayService.cs` | Stripe SDK, fee logic | Move to mozaiks-platform |
| `SettlementService.cs` | Revenue split logic | Move to mozaiks-platform |
| `subscription_config.json` | Empty plans | Define free tier for OSS users |

**Recommended Action:**
1. `Payment.API/` becomes **platform-only** (not deployed in self-host)
2. Core runtime uses `MockPaymentConnector` or external sync
3. Platform deploys Payment.API and syncs state to core

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              FRONTEND                                        │
│                                                                              │
│  ┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐      │
│  │  Checkout UI    │      │  Wallet UI      │      │  Subscription   │      │
│  │  (redirects)    │      │  (balance view) │      │  Settings UI    │      │
│  └────────┬────────┘      └────────┬────────┘      └────────┬────────┘      │
└───────────┼─────────────────────────┼─────────────────────────┼─────────────┘
            │                         │                         │
            ▼                         ▼                         ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         Python AI Runtime                                    │
│                                                                              │
│  ┌────────────────────────────────────────────────────────────────────┐     │
│  │                    Payment Connector Layer                          │     │
│  │                                                                      │     │
│  │  ┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐   │     │
│  │  │ PaymentConnector│   │ ManagedPayment  │   │ MockPayment     │   │     │
│  │  │ (ABC Interface) │──▶│ Connector       │   │ Connector       │   │     │
│  │  └─────────────────┘   │ (→ Gateway)     │   │ (self-host)     │   │     │
│  │                        └────────┬────────┘   └─────────────────┘   │     │
│  └─────────────────────────────────┼────────────────────────────────────┘     │
│                                    │                                         │
│  ┌────────────────────────────────────────────────────────────────────┐     │
│  │              Subscription Manager (READ-ONLY)                        │     │
│  │                                                                      │     │
│  │  • get_user_subscription(user_id)                                    │     │
│  │  • is_plugin_accessible(user_id, plugin_name)                        │     │
│  │  • sync_subscription_from_control_plane() ← Internal API only        │     │
│  └────────────────────────────────────────────────────────────────────┘     │
│                                                                              │
│  ┌────────────────────────────────────────────────────────────────────┐     │
│  │              Usage Tracking (Advisory Only)                          │     │
│  │                                                                      │     │
│  │  • chat.usage_summary events                                         │     │
│  │  • UsageIngestClient → Control Plane (if enabled)                    │     │
│  │  • NO billing enforcement                                            │     │
│  └────────────────────────────────────────────────────────────────────┘     │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    │ HTTP (ManagedHttpClient)
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         Payment.API (.NET)                                   │
│                                                                              │
│  ┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐           │
│  │ MozaiksPay      │   │ PaymentService  │   │ WalletService   │           │
│  │ Controller      │   │ (Stripe wrap)   │   │ (internal bal)  │           │
│  └────────┬────────┘   └────────┬────────┘   └────────┬────────┘           │
│           │                     │                     │                     │
│           ▼                     ▼                     ▼                     │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                    Data Layer                                         │   │
│  │                                                                        │   │
│  │  ┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐     │   │
│  │  │ LedgerService   │   │ TransactionSvc  │   │ EconomicEvent   │     │   │
│  │  │ (double-entry)  │   │ (audit trail)   │   │ Appender        │     │   │
│  │  └─────────────────┘   └─────────────────┘   └─────────────────┘     │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
                              ┌──────────┐
                              │  Stripe  │
                              │   API    │
                              └──────────┘
```

---

## Component Analysis

### 1. Payment.API (.NET) - **Well Designed** ✅

**Location:** `backend/src/Billing.API/Payment.API/`

The .NET billing service is the most complete piece:

#### Services
| Service | Purpose | Status |
|---------|---------|--------|
| `MozaiksPayService` | Stripe checkout, subscription status | ✅ Complete |
| `PaymentService` | Payment intent creation | ✅ Complete |
| `WalletService` | Internal balance management | ✅ Complete |
| `LedgerService` | Double-entry accounting | ✅ Complete |
| `TransactionService` | Transaction history | ✅ Complete |
| `SettlementService` | Revenue split settlements | ✅ Complete |
| `EconomicEventAppender` | Append-only audit log | ✅ Complete |

#### Transaction Types Supported
From [readme.md](backend/src/Billing.API/Payment.API/readme.md):

| Type | Payer | Receiver | Mozaiks Fee |
|------|-------|----------|-------------|
| `platform_subscription` | User | Mozaiks | 100% |
| `investor_funding` | Investor | App Creator | 3.5% |
| `private_app_purchase` | User | App Creator | 3.5% |
| `public_app_purchase` | User | Creator + Investors | 3.5% |

#### Blockchain Ready
The codebase has explicit `BLOCKCHAIN:` comments marking integration points:
- `WalletModel.cs` - Ready for `WalletType`, `BlockchainAddress`, `ChainId`
- `WalletService.cs` - Ready for `IBlockchainWalletProvider` injection
- `WalletTransaction` - Ready for `TxHash`, `Confirmations`

### 2. Python Payment Connectors - **Abstraction Only** ⚠️

**Location:** `runtime/ai/app/connectors/`

Clean abstraction but **no direct payment processing**:

```python
# base.py - Interface only
class PaymentConnector(ABC):
    async def checkout(...) -> CheckoutResponse
    async def subscription_status(...) -> PaymentStatus
    async def cancel(...) -> OkResponse
```

**Implementations:**
| Connector | Mode | Behavior |
|-----------|------|----------|
| `ManagedPaymentConnector` | Hosted | Delegates to .NET Payment.API |
| `MockPaymentConnector` | Self-host | Returns mock "active" status |

**Gap:** Self-hosters have NO real payment path. The mock connector always returns active.

### 3. Subscription Manager - **Read-Only Stub** ⚠️

**Location:** `runtime/ai/core/subscription_manager.py`

The subscription manager explicitly states it's **read-only**:

```python
# BOUNDARY CONTRACT: Subscription Manager (READ-ONLY for App Users)
# 
# READ (available to all authenticated users):
#   - get_user_subscription(user_id)
#   - is_plugin_accessible(user_id, plugin_name)
# 
# WRITE (Control Plane only, via X-Internal-API-Key):
#   - sync_subscription_from_control_plane(user_id, subscription_data)
```

**Gap:** No subscription plans defined. Config only has:
```json
{"plans":{"free":{"name":"Free"}}}
```

### 4. Usage Tracking - **Advisory Only** ❌

**Location:** `runtime/ai/core/ai_runtime/events/usage_ingest.py`

Token usage is tracked but **never enforced**:

```python
class UsageIngestClient:
    """Best-effort control-plane usage ingest (measurement only)."""
    
    async def handle_usage_summary(self, payload: Dict[str, Any]) -> None:
        if not self.enabled():
            return  # Silent no-op if not configured
```

**Gap:** 
- Usage data goes to control plane (if enabled) but doesn't affect billing
- No token quotas or limits enforced
- No usage-based billing integration

---

## Critical Gaps

### Gap 1: No Token Metering → Billing Pipeline

```
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ AG2 Runtime  │───▶│ UsageIngest  │───▶│  ??? Gap ??? │───▶ Billing
│ (tokens)     │    │ (advisory)   │    │              │
└──────────────┘    └──────────────┘    └──────────────┘
```

**Current State:**
- Token counts are captured per chat/workflow
- `chat.usage_summary` events are emitted
- `UsageIngestClient` sends to control plane (if enabled)
- **No billing system consumes this data**

**Needed:**
1. Usage aggregation service in Payment.API
2. Billing rule engine (free tier limits, overage charges)
3. Subscription enforcement at runtime

### Gap 2: No Self-Host Payment Option

**Current State:**
- `MockPaymentConnector` always returns `active=True`
- Self-hosters cannot monetize their apps

**Options for OSS Value:**
1. **Stripe Connect integration** - Allow self-hosters to use their own Stripe account
2. **LemonSqueezy adapter** - Alternative payment processor
3. **Plugin payment hooks** - Let plugins bring their own payment

### Gap 3: Subscription Plans Not Defined

**Current State:**
```json
// subscription_config.json
{"plans":{"free":{"name":"Free"}}}
```

**Needed:**
```json
{
  "plans": {
    "free": {
      "name": "Free",
      "tokens_per_month": 10000,
      "plugins": ["basic_*"],
      "price": 0
    },
    "pro": {
      "name": "Pro",
      "tokens_per_month": 100000,
      "plugins": ["*"],
      "price": 2900
    }
  }
}
```

### Gap 4: Python ↔ .NET Subscription Sync

**Current State:**
- `/api/internal/subscription/sync` endpoint exists
- Control plane can push subscription updates
- **No automatic sync on checkout completion**

**Needed:**
1. Webhook handler in Payment.API to call Python sync endpoint
2. Or: Python polls Payment.API for subscription status
3. Or: Shared database (current partial solution)

### Gap 5: Wallet Balance Not Used in Runtime

**Current State:**
- WalletService can debit/credit balances
- **No integration with AI runtime token spending**

**Needed:**
1. Pre-flight balance check before expensive operations
2. Real-time balance deduction during workflow execution
3. Balance alerts and limits

---

## Recommendations

### Immediate (Before Launch)

#### For mozaiks-core (OSS)

1. **Define Free Tier in `subscription_config.json`**
   ```json
   {
     "plans": {
       "free": {
         "name": "Free (Self-Hosted)",
         "tokens_per_month": null,  // unlimited for self-hosters
         "plugins": ["*"],
         "description": "Full access for self-hosted deployments"
       }
     },
     "enforcement": {
       "enabled": false,  // self-hosters can enable if desired
       "mode": "advisory"  // log warnings, don't block
     }
   }
   ```

2. **Make Quota Enforcement Optional**
   - Add `MOZAIKS_ENFORCE_QUOTAS=false` (default for OSS)
   - Log warnings when exceeded, don't hard-block
   - Self-hosters can enable if they want limits

3. **Document Self-Host Payment Integration**
   - How to call `/api/internal/subscription/sync`
   - How to bring your own Stripe account
   - Example webhook handler

#### For mozaiks-platform (Proprietary)

1. **Move Payment.API to Platform Repo**
   - Or: Mark as `platform-only` in deployment docs
   - Self-host deployments skip this service

2. **Implement Usage → Billing Pipeline**
   - Consume `chat.usage_summary` events
   - Aggregate per billing period
   - Generate invoices for overages

3. **Wire Stripe Webhooks → Core Sync**
   - `customer.subscription.created` → POST to core sync
   - `customer.subscription.updated` → POST to core sync
   - `invoice.paid` → update billing history

### Medium-Term

#### For mozaiks-core (OSS)

4. **Self-Host Stripe Connect Adapter**
   - Interface for bringing your own Stripe account
   - Environment variable: `STRIPE_SECRET_KEY` (self-hoster provides)
   - **No platform fee** - self-hosters keep 100%
   - Checkout redirects to their Stripe

5. **Usage Dashboard (Read-Only)**
   - Display token usage per workflow
   - Show costs (estimated from model pricing)
   - No billing integration in core

#### For mozaiks-platform (Proprietary)

6. **Entitlement Enforcement**
   - Hard limits on platform-hosted users
   - 402 Payment Required when quota exceeded
   - Upgrade prompts

7. **Revenue Share Automation**
   - Automatic 96.5% / 3.5% splits
   - Creator payout scheduling
   - Investor distribution for public apps

### Long-Term (Platform Differentiation)

8. **Blockchain Integration** (Platform only)
   - Follow the `BLOCKCHAIN:` markers in Payment.API
   - Token deposits/withdrawals
   - On-chain revenue tracking

9. **Marketplace Economics** (Platform only)
   - App store with revenue sharing
   - Investment rounds
   - Creator earnings dashboard

---

## Deployment Modes

### Mode 1: Self-Hosted (Core Only)

```yaml
# docker-compose.self-host.yml
services:
  ai-runtime:
    image: mozaiks/core-runtime
    environment:
      - MOZAIKS_MODE=self-hosted
      - MOZAIKS_ENFORCE_QUOTAS=false
      - STRIPE_SECRET_KEY=${YOUR_STRIPE_KEY}  # optional
  
  # NO Payment.API - self-hosters handle billing externally
```

**Self-hosters get:**
- Full AI runtime
- All plugins
- Usage tracking (optional)
- No platform fees
- Bring your own payment (optional)

### Mode 2: Platform-Hosted (Core + Platform)

```yaml
# docker-compose.platform.yml (in mozaiks-platform repo)
services:
  ai-runtime:
    image: mozaiks/core-runtime
    environment:
      - MOZAIKS_MODE=platform
      - MOZAIKS_ENFORCE_QUOTAS=true
      - CONTROL_PLANE_USAGE_INGEST_ENABLED=true
  
  payment-api:
    image: mozaiks/payment-api
    environment:
      - STRIPE_SECRET_KEY=${PLATFORM_STRIPE_KEY}
      - PLATFORM_FEE_PERCENT=3.5
```

**Platform provides:**
- Managed hosting
- Stripe integration
- 3.5% platform fee
- Usage-based billing
- Revenue sharing

---

## Quick Reference: Core vs Platform

| Feature | Core (OSS) | Platform (SaaS) |
|---------|------------|-----------------|
| AI workflow execution | ✅ | ✅ |
| Plugin system | ✅ | ✅ |
| Usage tracking | ✅ (optional) | ✅ (mandatory) |
| Subscription state read | ✅ | ✅ |
| Subscription state write | ❌ | ✅ |
| Payment processing | ❌ | ✅ |
| Stripe integration | ❌ (bring your own) | ✅ |
| Platform fee (3.5%) | ❌ Never | ✅ Always |
| Revenue splits | ❌ | ✅ |
| Usage enforcement | ⚪ Optional | ✅ Mandatory |
| Billing mutations | ❌ | ✅ |
| Multi-tenant billing | ❌ | ✅ |
| Self-hostable | ✅ | ❌ |
| Source available | ✅ Open | ❌ Proprietary |

---

## File Reference

### Python Runtime
| File | Purpose |
|------|---------|
| [connectors/base.py](runtime/ai/app/connectors/base.py) | Payment connector interface |
| [connectors/managed.py](runtime/ai/app/connectors/managed.py) | Hosted mode connector |
| [connectors/mock.py](runtime/ai/app/connectors/mock.py) | Self-host mock |
| [subscription_manager.py](runtime/ai/core/subscription_manager.py) | Read-only subscription state |
| [subscription_sync.py](runtime/ai/core/routes/subscription_sync.py) | Internal sync endpoint |
| [usage_ingest.py](runtime/ai/core/ai_runtime/events/usage_ingest.py) | Control plane usage reporting |
| [subscription_config.json](runtime/ai/core/config/subscription_config.json) | Plan definitions (minimal) |

### .NET Payment.API
| File | Purpose |
|------|---------|
| [MozaiksPayService.cs](backend/src/Billing.API/Payment.API/Services/MozaiksPayService.cs) | Stripe orchestration |
| [WalletService.cs](backend/src/Billing.API/Payment.API/Services/WalletService.cs) | Internal balance |
| [LedgerService.cs](backend/src/Billing.API/Payment.API/Services/LedgerService.cs) | Double-entry accounting |
| [WalletModel.cs](backend/src/Billing.API/Payment.API/Models/WalletModel.cs) | Wallet data model |
| [EconomicEventDocument.cs](backend/src/Billing.API/Payment.API/Models/EconomicEventDocument.cs) | Audit log |
| [readme.md](backend/src/Billing.API/Payment.API/readme.md) | Transaction type documentation |

---

## Summary

**What's Good:**
- Clean separation between runtime and billing
- Double-entry ledger for financial integrity
- Stripe integration is complete
- Blockchain integration points are pre-planned
- Read-only subscription enforcement is correct pattern

**What's Missing:**
- Token metering → billing pipeline
- Subscription plan definitions
- Usage enforcement in runtime
- Self-host payment option
- Webhook → Python sync automation

**Priority Order:**
1. Define subscription plans (1 day)
2. Implement token quota checks (2-3 days)
3. Wire Stripe webhooks to Python sync (1-2 days)
4. Usage dashboard for users (1 week)
5. Self-host Stripe Connect (2-3 weeks)
