# mozaiks-core: Dead Code & Gaps Report

**Date:** Generated for current session  
**Status:** Actionable summary for next steps

---

## 🗑️ DEAD CODE - Safe to Delete

### Location: `runtime/ai/src/` (13 files)

This entire folder is **NOT IMPORTED ANYWHERE**. Confirmed via grep search.

| File | What It Was | Why It's Dead |
|------|-------------|---------------|
| `src/core/entitlements/__init__.py` | Module exports | Imports broken `src.core.entitlements` path |
| `src/core/entitlements/manifest.py` | EntitlementManifest class | Duplicate of `core/entitlements/manifest.py` |
| `src/core/entitlements/token_tracker.py` | Token tracking | Never integrated |
| `src/core/entitlements/feature_gate.py` | Feature gates | Never integrated |
| `src/core/entitlements/middleware.py` | Middleware class | Never integrated |
| `src/core/entitlements/sources.py` | Data sources | Never integrated |
| `src/workflows/subscription_manager.py` | **SAVE IDEAS** | Good AI workflow concept, wrong imports |

### Recommendation:

```powershell
# 1. Copy subscription_manager.py ideas first (it has good code)
# 2. Then delete the entire src/ folder
Remove-Item -Recurse -Force "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-core\runtime\ai\src"
```

---

## ✅ WHAT ACTUALLY WORKS

### Core has these WORKING pieces:

| Component | Location | Status |
|-----------|----------|--------|
| **Python Runtime** | `runtime/ai/` (main.py) | ✅ Imports clean |
| **Director** | `core/director.py` | ✅ Runs AI workflows |
| **Active Entitlements** | `core/entitlements/` | ✅ Imported & used |
| **CLI** | `cli/` | ✅ init, new, doctor work |
| **.NET Billing API** | `backend/src/Billing.API/` | ✅ Compiles (0 errors) |
| **React Shell** | `runtime/packages/shell/` | ✅ 51 components |

---

## 🔌 PAYMENT/BILLING ARCHITECTURE

### What Core Has (IPaymentProvider interface):

```
IPaymentProvider
├── CreateCheckoutAsync()     # Create checkout session
├── GetSubscriptionStatusAsync()  # Check status
├── CancelSubscriptionAsync() # Cancel
└── ProcessWebhookAsync()     # Handle provider webhooks
```

**Implementations pattern:**
- `PlatformPaymentProvider` → Calls mozaiks-platform
- `NoOpPaymentProvider` → Always returns active (self-hosted free)
- `Custom` → Self-hosters implement their own

### What Platform Has (Real Stripe code):

```
Payment.API (39 files)
├── PaymentController.cs
│   ├── CreatePaymentIntent() → Stripe.PaymentIntent
│   ├── StripeWebhook() → Webhook handling
│   └── payment-confirmed endpoint
├── WalletController.cs → User wallets
├── LedgerController.cs → Transaction history
├── SubscriptionController.cs → Subscription management
├── IWalletService.cs → Balance management
├── ILedgerService.cs → Transaction logging
└── StripeService.cs → Stripe SDK wrapper
```

---

## 🚨 WHAT'S MISSING (The Gaps)

### Gap 1: Subscription Manager AI Workflow

The dead `subscription_manager.py` had the RIGHT IDEA:
- AI that understands user's subscription/usage
- Can explain limits, suggest upgrades
- Works in both self-hosted and platform mode

**What to do:** Resurrect this code with correct imports from `core.entitlements`

### Gap 2: Platform Billing Integration

Core has the `IPaymentProvider` interface but:
- No actual `PlatformPaymentProvider` implementation
- No HTTP client to call Platform's Payment.API
- No webhook relay setup

**What to do:** Platform needs to implement or Core needs to expose HTTP client pattern

### Gap 3: Token Budget Enforcement at Runtime

The `core/entitlements/` system tracks budgets but:
- Director doesn't actually check budgets before LLM calls
- No middleware in request pipeline to enforce limits
- Usage tracking fires events but doesn't block

**What to do:** Add enforcement hook in `core/director.py`

### Gap 4: AI-Driven Subscription Logic

Your vision: "users define subscription logic in an AI-driven declarative world"

**What exists:**
- Static manifest format (`EntitlementManifest`)
- Python API for checking features/budgets

**What's missing:**
- No declarative subscription DSL
- No AI-driven pricing optimization
- No "smart" tier recommendations based on usage patterns

**This is Platform territory** - Core provides the runtime, Platform provides the intelligence

---

## 🔁 MESSAGE TO mozaiks-platform (VIA HUMAN)

### Required Collaboration:

**From Platform → Core needs:**
1. `PlatformPaymentProvider` implementation (or HTTP contract for Core to call)
2. Webhook forwarding setup (Stripe → Platform → Core entitlement sync)
3. Subscription status endpoint that Core's IPaymentProvider can call

**From Core → Platform gets:**
1. `IPaymentProvider` interface (stable contract)
2. `EntitlementManifest` format (token budgets, features, limits)
3. Usage events stream for billing aggregation

### Contract Question:
> Should Core include a reference `PlatformPaymentProvider` that calls Platform's HTTP APIs?
> Or should Platform provide this as a plugin/package?

---

## 📋 Immediate Actions

1. **Delete dead code:** `runtime/ai/src/` folder (after saving subscription_manager.py ideas)
2. **Resurrect subscription workflow:** Move good ideas to `workflows/subscription_manager.py`
3. **Document IPaymentProvider:** Add to `docs/contracts/`
4. **Create Platform integration ticket:** Define HTTP contract for payment delegation

---

## What You Actually Have

**Core is a working runtime** that can:
- Run AI apps with plugins and workflows
- Track token usage per app/user
- Check feature availability
- Define subscription tiers via manifest

**Core is NOT YET:**
- Integrated with real payment (needs Platform)
- Enforcing budgets at runtime (easy to add)
- Providing AI-driven subscription management (ideas exist, not wired up)

Your "capital layer for AI-native startups" vision is the **Platform layer**, not Core.
Core provides the **execution substrate** that Platform monetizes.
