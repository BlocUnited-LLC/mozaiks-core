# 🔁 Questions for mozaiks-platform Team

**From:** mozaiks-core Agent  
**Subject:** Payment, Subscription & Entitlement Integration  
**Date:** January 2026

---

## Context

I've audited both repos. Here's what exists:

### Platform Has (Real Code):
- **Payment.API** (39 files) - Full Stripe integration with:
  - `CreatePaymentIntentAsync()` → Stripe PaymentIntents
  - Webhook handling for `payment_intent.succeeded`
  - `DistributeFundsAsync()` with revenue share logic (3.5% platform fee)
  - Wallet system with `StripeConnectAccountId` for payouts
  - Transaction types: `PlatformSubscription`, `InvestorFundsCreator`, `EndUserPrivateApp`, `EndUserPublicApp`

- **Monetization.API** (7 files) - Aggregation layer with:
  - `DistributionService` - Investor revenue share calculations
  - `PayoutService` - Creator withdrawal requests
  - `MonetizationAggregator` - Combines Payment, AdEngine, Governance, Hosting data
  - `IHostingClient` - Calls `/api/hosting/subscriptions/{appId}/summary`

- **Hosting.API** - `EntitlementsClient` that calls:
  - `/api/internal/entitlements?userId=X&appId=Y`
  - Returns: `AllowHosting`, `AllowExportRepo`, `HostingLevel`, `Tokens.MonthlyLimit`, `Fees.TransactionFeeBps`, etc.

### Core Has (Interfaces):
- **`IPaymentProvider`** - Abstract payment interface with:
  - `CreateCheckoutAsync()`
  - `GetSubscriptionStatusAsync()`
  - `CancelSubscriptionAsync()`
  - `ProcessWebhookAsync()`
  - Expected implementations: `PlatformPaymentProvider`, `NoOpPaymentProvider`, `Custom`

- **`EntitlementManifest`** (Python) - Token budget tracking:
  - `plan: {tier, name, billing_period}`
  - `token_budget: {total_tokens: {limit, enforcement}}`
  - `features: {feature_name: bool}`
  - `rate_limits: {requests_per_minute: int}`

---

## Questions

### 1. Entitlement Sync: Who Is the Source of Truth?

**Situation:** Platform has `EntitlementsResponse` with `Tokens.MonthlyLimit`. Core has `EntitlementManifest` with `token_budget.total_tokens.limit`.

**Question:** When a user upgrades their plan on Platform:
1. Does Platform call Core to update the manifest?
2. Does Core call Platform to fetch the new limits?
3. Or should both subscribe to the same event?

**Proposal:** Platform is source of truth for billing/plans. Platform pushes updates to Core via:
```
POST /api/v1/entitlements/{app_id}/sync
{
  "plan_tier": "pro",
  "token_budget": {"limit": 5000000, "period": "month"},
  "features": {"function_calling": true, "vision": true}
}
```

Does this match Platform's expectations?

---

### 2. Revenue Share at Runtime

**Situation:** Platform's `StripePaymentService.DistributeFundsAsync()` handles investor revenue splits when payments succeed. But this is for payments flowing *into* the platform.

**Question:** How do we handle **token usage costs** for apps that charge end-users?

Example: User pays $10/month for an AI app. App uses $3 of tokens. Who pays for the tokens?
- Option A: Platform deducts token cost from creator's payout
- Option B: Creator pre-funds a wallet, Core draws down
- Option C: Post-pay reconciliation

What's the intended model?

---

### 3. PlatformPaymentProvider Implementation

**Situation:** Core's `IPaymentProvider` expects a `PlatformPaymentProvider` that delegates to Platform.

**Question:** Should this live in:
- **Core** - as an HTTP client calling Platform's Payment.API?
- **Platform** - as a package that Core installs?
- **Shared** - as a NuGet package both can reference?

If Core should implement it, what endpoints should it call?
```
POST /api/Payment/create-intent
GET  /api/Payment/subscription/{userId}?scope=app&appId=X
POST /api/Payment/cancel-subscription
POST /api/Payment/webhooks/relay
```

---

### 4. Subscription Webhook Flow

**Situation:** Stripe sends webhooks to Platform. Core needs to know when subscriptions change.

**Question:** What's the event propagation path?

```
Stripe → Platform Payment.API → ??? → Core Entitlement Sync
```

Options:
- A) Platform calls Core's `/api/v1/entitlements/{app_id}/sync` endpoint directly
- B) Platform publishes to message bus, Core subscribes
- C) Core polls Platform's entitlements endpoint

Which is preferred?

---

### 5. Transaction Types Alignment

**Situation:** Platform has these transaction types:
- `PlatformSubscription` - User subscribes to mozaiks platform
- `InvestorFundsCreator` - Investor sends money to creator
- `EndUserPrivateApp` - End user pays for private app (100% to creator minus fee)
- `EndUserPublicApp` - End user pays for public app (splits to investors + creator)

**Question:** Should Core understand these types? Or should Core just see:
- `subscription_payment`
- `usage_topup`
- `one_time_purchase`

And Platform handles the revenue distribution logic internally?

---

### 6. Wallet Model Exposure

**Situation:** Platform's `WalletModel` has `StripeConnectAccountId` for direct payouts.

**Question:** Should Core know about wallets at all? Or is wallet management entirely Platform territory?

If Core should track balances (for pre-paid token budgets), what's the contract?

---

### 7. AI-Driven Subscription Logic (Future)

**Your Vision:** "Users define subscription logic" in an "AI-driven declarative world."

**Question:** What does this look like concretely?

Ideas I've seen in the code:
- `SubscriptionManagerWorkflow` (was dead code, but good concept) - AI that explains usage, suggests upgrades
- Declarative pricing rules? (e.g., "If usage > X, recommend plan Y")
- Dynamic tier creation by app creators?

Is this:
- A) Platform-only feature (Core just executes, Platform decides)
- B) Core runtime feature (declarative rules engine in manifest)
- C) Hybrid (Core has primitives, Platform adds intelligence)

---

### 8. Cap Table / Revenue Share on Core Manifests

**Situation:** Platform's `TransactionMetadata` has `InvestorShares` with `SharePercentage`.

**Question:** Should Core's `EntitlementManifest` include revenue share info?

```yaml
# Possible manifest extension
revenue_share:
  investors:
    - investor_id: "inv_123"
      share_bps: 2000  # 20%
    - investor_id: "inv_456" 
      share_bps: 500   # 5%
  platform_fee_bps: 350  # 3.5%
```

Or is this purely Platform data that Core shouldn't see?

---

### 9. Self-Hosted Billing Story

**Situation:** Core must work without Platform. Self-hosters need some billing capability.

**Question:** What's the minimum viable self-hosted billing story?

Options:
- A) `NoOpPaymentProvider` - Everything is free/unlimited
- B) `StripeDirectProvider` - Self-hosters configure their own Stripe
- C) `UsageOnlyProvider` - Track usage, no actual billing (reporting only)

Current Core assumption is (A). Is that correct?

---

### 10. Missing Endpoint List

Based on my analysis, Core would need to call these Platform endpoints:

| Core Needs | Platform Endpoint | Status |
|------------|-------------------|--------|
| Get subscription status | `/api/Payment/subscription/{userId}` | ❓ Exists? |
| Get entitlements | `/api/internal/entitlements` | ✅ Exists |
| Report usage event | `/api/Payment/usage` | ❓ Exists? |
| Create checkout | `/api/Payment/create-intent` | ✅ Exists |
| Sync entitlement changes | Core exposes, Platform calls | ❓ Contract needed |

**Question:** Can you confirm which endpoints exist and share the contracts?

---

## Proposed Integration Architecture

```
┌─────────────────┐         ┌──────────────────┐
│  mozaiks-core   │         │ mozaiks-platform │
│                 │         │                  │
│ IPaymentProvider│◄────────┤ Payment.API      │
│     ▲           │  HTTP   │    │             │
│     │           │         │    ▼             │
│ PlatformPayment │─────────► Stripe           │
│   Provider      │  calls  │                  │
│                 │         │                  │
│ EntitlementSync ◄─────────┤ Webhook Handler  │
│   Endpoint      │  POST   │ (subscription    │
│                 │         │  changes)        │
└─────────────────┘         └──────────────────┘
```

Does this match Platform's architecture expectations?

---

## Summary: What Core Needs From Platform

1. **Confirmation** of existing endpoint contracts
2. **Decision** on who implements `PlatformPaymentProvider`
3. **Event contract** for subscription/entitlement changes
4. **Guidance** on revenue share visibility in Core
5. **Input** on AI-driven subscription vision

Please reply with answers or schedule a sync to discuss.

---

*This document generated by mozaiks-core agent. Located at:*  
`docs/contracts/PLATFORM_COLLABORATION_QUESTIONS.md`
