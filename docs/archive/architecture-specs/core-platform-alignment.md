# Core ↔ Platform Alignment Specification

> **Status**: � Core Implementation Complete  
> **Date**: January 25, 2026  
> **Scope**: mozaiks-core + mozaiks-platform unified architecture  
> **Next**: Platform-side implementation (see [PLATFORM_COORDINATION.md](./PLATFORM_COORDINATION.md))

---

## Executive Summary

This document aligns two independently-designed systems:

| System | Focus | Language | Key Abstraction |
|--------|-------|----------|-----------------|
| **mozaiks-core** | Entitlements, token tracking, observability | Python + C# | `EntitlementManifest` |
| **mozaiks-platform** | Payments, revenue distribution, billing | C# | `IPaymentProvider` |

**The unified architecture:**
1. Platform **creates** EntitlementManifests from subscription state
2. Core **reads** manifests and **enforces** (optionally)
3. Core **emits** usage events
4. Platform **receives** events and **bills**
5. Revenue **flows** through platform's distribution engine

---

## Architecture: The Complete Picture

```
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│                                    END USER / APP                                         │
└────────────────────────────────────────┬─────────────────────────────────────────────────┘
                                         │
                    ┌────────────────────┼────────────────────┐
                    ▼                    ▼                    ▼
           ┌───────────────┐    ┌───────────────┐    ┌───────────────┐
           │  Subscribe    │    │  Use AI       │    │  Invest       │
           │  (Payment)    │    │  (Runtime)    │    │  (Governance) │
           └───────┬───────┘    └───────┬───────┘    └───────┬───────┘
                   │                    │                    │
═══════════════════╪════════════════════╪════════════════════╪══════════════════════════════
                   │     PLATFORM → CORE CONTRACT BOUNDARY   │
═══════════════════╪════════════════════╪════════════════════╪══════════════════════════════
                   │                    │                    │
                   ▼                    │                    ▼
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│                              mozaiks-platform (PROPRIETARY)                               │
│                                                                                           │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────┐ │
│  │  Payment.API    │  │ Monetization.API│  │  AdEngine.API   │  │   Governance.API    │ │
│  │                 │  │                 │  │                 │  │                     │ │
│  │ • Stripe        │  │ • Aggregation   │  │ • Ad Revenue    │  │ • Investments       │ │
│  │ • Subscriptions │  │ • Distribution  │  │ • Attribution   │  │ • Cap Tables        │ │
│  │ • Wallets       │  │ • Analytics     │  │ • Campaigns     │  │ • Revenue Share     │ │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘  └──────────┬──────────┘ │
│           │                    │                    │                      │            │
│           └────────────────────┴────────────────────┴──────────────────────┘            │
│                                         │                                                │
│                          ┌──────────────▼──────────────┐                                │
│                          │   ENTITLEMENT MANAGER       │                                │
│                          │   (Platform Component)      │                                │
│                          │                             │                                │
│                          │ • Creates manifests from    │                                │
│                          │   subscription state        │                                │
│                          │ • Syncs to core runtime     │                                │
│                          │ • Updates on plan changes   │                                │
│                          └──────────────┬──────────────┘                                │
│                                         │                                                │
│  ═══════════════════════════════════════╪═══════════════════════════════════════════   │
│              MANIFEST SYNC API (Platform → Core)                                        │
│  ═══════════════════════════════════════╪═══════════════════════════════════════════   │
└─────────────────────────────────────────┼────────────────────────────────────────────────┘
                                          │
                                          ▼
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│                              mozaiks-core (OPEN SOURCE)                                   │
│                                                                                           │
│  ┌─────────────────────────────────────────────────────────────────────────────────────┐ │
│  │                         BILLING.API (C# Backend)                                     │ │
│  │                                                                                      │ │
│  │  ┌────────────────────┐    ┌────────────────────┐    ┌────────────────────────────┐ │ │
│  │  │  IPaymentProvider  │    │  MozaiksPay Facade │    │  EntitlementSyncEndpoint   │ │ │
│  │  │  (Abstraction)     │    │  (Unified API)     │    │  (Receives from Platform)  │ │ │
│  │  │                    │    │                    │    │                            │ │ │
│  │  │ • CreateCheckout() │    │ • /checkout        │    │ • POST /entitlements/sync  │ │ │
│  │  │ • GetStatus()      │    │ • /status          │    │ • Stores locally           │ │ │
│  │  │ • Cancel()         │    │ • /cancel          │    │ • Notifies runtime         │ │ │
│  │  └────────────────────┘    └────────────────────┘    └────────────────────────────┘ │ │
│  └─────────────────────────────────────────────────────────────────────────────────────┘ │
│                                          │                                               │
│                                          ▼                                               │
│  ┌─────────────────────────────────────────────────────────────────────────────────────┐ │
│  │                         AI RUNTIME (Python)                                          │ │
│  │                                                                                      │ │
│  │  ┌────────────────────┐    ┌────────────────────┐    ┌────────────────────────────┐ │ │
│  │  │ EntitlementManifest│    │ TokenBudgetTracker │    │   UnifiedEventDispatcher   │ │ │
│  │  │ (Declarative State)│    │ (Usage Tracking)   │    │   (Emits Usage Events)     │ │ │
│  │  │                    │    │                    │    │                            │ │ │
│  │  │ • token_budget     │───▶│ • check_budget()   │───▶│ • chat.usage_delta         │ │ │
│  │  │ • features         │    │ • record_usage()   │    │ • chat.usage_summary       │ │ │
│  │  │ • rate_limits      │    │ • get_summary()    │    │ • workflow.completed       │ │ │
│  │  └────────────────────┘    └────────────────────┘    └──────────────┬─────────────┘ │ │
│  │                                                                     │               │ │
│  │  ┌────────────────────┐    ┌────────────────────┐                   │               │ │
│  │  │    FeatureGate     │    │ EntitlementSource  │                   │               │ │
│  │  │ (Feature Checks)   │    │ (Manifest Loader)  │                   │               │ │
│  │  │                    │    │                    │                   │               │ │
│  │  │ • is_enabled()     │    │ • LocalFileSource  │                   │               │ │
│  │  │ • require()        │    │ • EnvironmentSource│                   │               │ │
│  │  │                    │    │ • ControlPlaneSource│                  │               │ │
│  │  └────────────────────┘    └────────────────────┘                   │               │ │
│  └─────────────────────────────────────────────────────────────────────┼───────────────┘ │
│                                                                        │                 │
│  ══════════════════════════════════════════════════════════════════════╪═════════════   │
│                    USAGE EVENT BUS (Core → Platform)                   │                 │
│  ══════════════════════════════════════════════════════════════════════╪═════════════   │
└────────────────────────────────────────────────────────────────────────┼─────────────────┘
                                                                         │
                                                                         ▼
                                                          ┌──────────────────────────┐
                                                          │ Platform Usage Pipeline  │
                                                          │ (Receives usage events)  │
                                                          │                          │
                                                          │ • Applies pricing        │
                                                          │ • Updates meters         │
                                                          │ • Triggers billing       │
                                                          │ • Distributes revenue    │
                                                          └──────────────────────────┘
```

---

## The Two Contracts

### Contract 1: IPaymentProvider (C# - Payment Operations)

**Purpose**: Abstract payment operations so Core doesn't touch Stripe directly.

**Lives in**: `mozaiks-core/backend/src/Billing.API/Contracts/`

```csharp
namespace MozaiksBilling.Contracts;

/// <summary>
/// Payment operations contract.
/// Platform implements with Stripe. Self-hosters can BYO.
/// </summary>
public interface IPaymentProvider
{
    string ProviderId { get; }
    
    // Checkout
    Task<CheckoutResult> CreateCheckoutAsync(CheckoutRequest request, CancellationToken ct);
    
    // Status
    Task<SubscriptionStatus> GetSubscriptionStatusAsync(
        string userId, string scope, string? appId, CancellationToken ct);
    
    // Cancel
    Task<CancelResult> CancelSubscriptionAsync(
        string userId, string scope, string? appId, CancellationToken ct);
    
    // Webhooks
    Task<WebhookResult> ProcessWebhookAsync(
        string payload, string signature, CancellationToken ct);
}
```

**Implementations**:
| Provider | Location | Use Case |
|----------|----------|----------|
| `PlatformPaymentProvider` | Core | Delegates to Platform's Payment.API |
| `NoOpPaymentProvider` | Core | Dev/free tier (always returns active) |
| `BYOPaymentProvider` | User's code | Self-hosted with custom payment |

### Contract 2: EntitlementManifest (Python/JSON - Runtime State)

**Purpose**: Declarative state that controls what an app/user can do at runtime.

**Lives in**: `mozaiks-core/runtime/ai/src/core/entitlements/`

```python
@dataclass(frozen=True)
class EntitlementManifest:
    version: str
    app_id: str
    tenant_id: Optional[str]
    plan: dict           # {id, name, tier, billing_period, expires_at}
    token_budget: dict   # {period, total_tokens: {limit, used, enforcement}}
    features: dict       # {workflow_execution: bool, multi_agent: bool, ...}
    rate_limits: dict    # {requests_per_minute, concurrent_workflows, ...}
    observability: dict  # {level, retention_days, export_enabled}
    plugins: dict        # {allowed: [], blocked: []}
    overrides: dict      # {trial_extension, promotional_tokens, ...}
    metadata: dict       # App-specific data
```

**Sources**:
| Source | Location | Use Case |
|--------|----------|----------|
| `LocalFileSource` | Core | Self-hosted, reads from file |
| `EnvironmentSource` | Core | Container-friendly, reads from env |
| `ControlPlaneSource` | Core | Platform mode, fetches from API |

---

## How They Connect: The Sync Flow

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                              SUBSCRIPTION LIFECYCLE                                      │
└─────────────────────────────────────────────────────────────────────────────────────────┘

1. USER SUBSCRIBES
   ─────────────────
   
   User → Frontend → Core MozaiksPay → IPaymentProvider.CreateCheckout()
                                              │
                                              ▼
                            ┌─────────────────────────────────┐
                            │ PlatformPaymentProvider         │
                            │ (delegates to Platform)         │
                            └─────────────────┬───────────────┘
                                              │
                                              ▼
                            ┌─────────────────────────────────┐
                            │ Platform Payment.API            │
                            │ • Creates Stripe Checkout       │
                            │ • Returns session URL           │
                            └─────────────────────────────────┘


2. STRIPE WEBHOOK → SUBSCRIPTION CREATED
   ────────────────────────────────────────
   
   Stripe → Platform Payment.API/webhooks/stripe
                     │
                     ▼
            ┌─────────────────────────────────┐
            │ Platform processes webhook:      │
            │ • Updates subscription state     │
            │ • Creates EntitlementManifest    │
            │ • Syncs to Core                  │
            └─────────────────┬───────────────┘
                              │
                              ▼
            ┌─────────────────────────────────┐
            │ POST Core/api/entitlements/sync │
            │ {                               │
            │   "app_id": "app_123",          │
            │   "manifest": { ... }           │
            │ }                               │
            └─────────────────┬───────────────┘
                              │
                              ▼
            ┌─────────────────────────────────┐
            │ Core stores manifest:            │
            │ • Redis cache (hot)              │
            │ • MongoDB (persistent)           │
            │ • Notifies runtime               │
            └─────────────────────────────────┘


3. RUNTIME ENFORCES ENTITLEMENTS
   ─────────────────────────────────
   
   AI Runtime receives workflow request
                     │
                     ▼
            ┌─────────────────────────────────┐
            │ EntitlementMiddleware           │
            │ • Loads manifest for app_id     │
            │ • Checks token budget           │
            │ • Checks feature gates          │
            │ • Allows/rejects request        │
            └─────────────────┬───────────────┘
                              │
                              ▼
            ┌─────────────────────────────────┐
            │ Workflow executes               │
            │ • Tracks token usage            │
            │ • Emits usage events            │
            └─────────────────┬───────────────┘
                              │
                              ▼
            ┌─────────────────────────────────┐
            │ Usage Event:                     │
            │ {                               │
            │   "type": "chat.usage_delta",   │
            │   "app_id": "app_123",          │
            │   "tokens": 1500,               │
            │   "model": "gpt-4"              │
            │ }                               │
            └─────────────────────────────────┘


4. PLATFORM BILLS USAGE
   ───────────────────────
   
   Usage Event → Platform UsageBillingPipeline
                              │
                              ▼
            ┌─────────────────────────────────┐
            │ Platform processes usage:        │
            │ • Looks up pricing tier          │
            │ • Calculates cost                │
            │ • Updates meter                  │
            │ • Checks overage                 │
            └─────────────────┬───────────────┘
                              │
                              ▼
            ┌─────────────────────────────────┐
            │ If overage:                      │
            │ • Update manifest enforcement   │
            │ • Sync to Core (hard reject)     │
            │ • Notify user                    │
            └─────────────────────────────────┘


5. REVENUE DISTRIBUTION
   ───────────────────────
   
   End of billing period
                     │
                     ▼
            ┌─────────────────────────────────┐
            │ Platform Distribution Engine:    │
            │ • Calculate gross revenue        │
            │ • Deduct platform fee (3.5%)     │
            │ • Look up cap table              │
            │ • Calculate investor shares      │
            │ • Transfer to wallets            │
            │ • Pay out via Stripe Connect     │
            └─────────────────────────────────┘
```

---

## API Contracts

### Core → Platform (Payment Operations)

**Platform Payment.API must expose**:

| Endpoint | Purpose | Called By |
|----------|---------|-----------|
| `POST /api/payment/checkout` | Create Stripe checkout | Core PlatformPaymentProvider |
| `GET /api/payment/subscription/{userId}/{appId}` | Get subscription status | Core PlatformPaymentProvider |
| `POST /api/payment/cancel` | Cancel subscription | Core PlatformPaymentProvider |
| `POST /api/payment/webhook/stripe` | Stripe webhooks | Stripe |

### Platform → Core (Entitlement Sync)

**Core must expose**:

| Endpoint | Purpose | Called By |
|----------|---------|-----------|
| `POST /api/entitlements/sync` | Receive manifest update | Platform EntitlementManager |
| `GET /api/entitlements/{appId}` | Get current manifest | Platform (for verification) |
| `DELETE /api/entitlements/{appId}` | Remove manifest (cancelled) | Platform |

### Core → Platform (Usage Events)

**Platform must receive**:

| Event | Purpose | Transport |
|-------|---------|-----------|
| `chat.usage_delta` | Per-request token usage | Message queue (RabbitMQ) |
| `chat.usage_summary` | Aggregated usage | Message queue |
| `workflow.completed` | Workflow completion | Message queue |
| `entitlement.violation` | Budget/feature violation | Message queue |

---

## Implementation Plan

### Phase 1: Core Foundation (Week 1-2)

#### mozaiks-core Tasks

| Task | File | Priority | Status |
|------|------|----------|--------|
| Create IPaymentProvider interface | `Billing.API/Contracts/IPaymentProvider.cs` | P0 | ✅ DONE |
| Create NoOpPaymentProvider | `Billing.API/Providers/NoOpPaymentProvider.cs` | P0 | ✅ DONE |
| Create PlatformPaymentProvider | `Billing.API/Providers/PlatformPaymentProvider.cs` | P0 | ✅ DONE |
| Create entitlements Python package | `runtime/ai/src/core/entitlements/` | P0 | ✅ DONE |
| Implement EntitlementManifest dataclass | `entitlements/manifest.py` | P0 | ✅ DONE |
| Implement EntitlementSource ABC | `entitlements/sources.py` | P0 | ✅ DONE |
| Implement TokenBudgetTracker | `entitlements/token_tracker.py` | P0 | ✅ DONE |
| Implement FeatureGate | `entitlements/feature_gate.py` | P0 | ✅ DONE |
| Implement EntitlementMiddleware | `entitlements/middleware.py` | P0 | ✅ DONE |
| Create SubscriptionManagerWorkflow | `workflows/subscription_manager.py` | P0 | ✅ DONE |
| Create EntitlementSyncController | `Payment.API/Controllers/EntitlementSyncController.cs` | P0 | ✅ DONE |
| Create EntitlementManifestRepository | `Payment.API/Repository/EntitlementManifestRepository.cs` | P0 | ✅ DONE |
| Create UsageEventController | `Payment.API/Controllers/UsageEventController.cs` | P0 | ✅ DONE |
| Create UsageEventRepository | `Payment.API/Repository/UsageEventRepository.cs` | P0 | ✅ DONE |

#### mozaiks-platform Tasks

| Task | File | Priority |
|------|------|----------|
| Create EntitlementManager service | `Payment.API/Services/EntitlementManager.cs` | P0 |
| Add manifest creation from subscription | `EntitlementManager.CreateFromSubscription()` | P0 |
| Add manifest sync to Core | `EntitlementManager.SyncToCore()` | P0 |
| Update webhook handler to trigger sync | `StripeWebhookHandler.cs` | P0 |
| Persist cap table to MongoDB | `CapTableRepository.cs` | P0 |

### Phase 2: Usage Pipeline (Week 3-4)

#### mozaiks-core Tasks

| Task | File | Priority |
|------|------|----------|
| Emit usage events from runtime | `UnifiedEventDispatcher` | P1 |
| Add usage event schema | `events/schemas/usage_events.py` | P1 |
| Configure RabbitMQ producer | `event_bus/usage_publisher.py` | P1 |

#### mozaiks-platform Tasks

| Task | File | Priority |
|------|------|----------|
| Create UsageBillingPipeline | `Monetization.API/Services/UsageBillingPipeline.cs` | P1 |
| Create usage event consumer | `Monetization.API/Consumers/UsageEventConsumer.cs` | P1 |
| Implement pricing engine | `Monetization.API/Services/PricingEngine.cs` | P1 |
| Connect to Monetization.API aggregation | Wire HTTP clients | P1 |

### Phase 3: Revenue Distribution (Week 5-6)

#### mozaiks-platform Tasks

| Task | File | Priority |
|------|------|----------|
| Create DistributionScheduler | `Payment.API/Workers/DistributionScheduler.cs` | P2 |
| Implement cap table lookup in distribution | `StripePaymentService.DistributeFundsAsync()` | P2 |
| Create ad attribution service | `AdEngine.API/Services/AttributionService.cs` | P2 |
| Wire Monetization.API to all data sources | HTTP clients | P2 |

### Phase 4: Subscription Manager Workflow (Week 7-8)

#### mozaiks-core Tasks

| Task | File | Priority |
|------|------|----------|
| Create SubscriptionManagerWorkflow | `examples/workflows/subscription_manager.py` | P3 |
| Add subscription tools | `tools/subscription_tools.py` | P3 |
| Create usage analysis tools | `tools/usage_analysis_tools.py` | P3 |

---

## The Subscription Manager AI Workflow

A modular AI workflow that apps can embed for subscription-related interactions:

```python
# runtime/ai/src/workflows/subscription_manager.py

"""
Subscription Manager AI Workflow

Provides intelligent subscription assistance:
- Explains current usage and limits
- Answers billing questions
- Suggests plan optimizations
- Handles upgrade requests (platform mode)
- Alerts on approaching limits
"""

from dataclasses import dataclass
from typing import Optional
from ag2 import ConversableAgent, register_function

from core.entitlements import EntitlementManifest, TokenBudgetTracker


@dataclass
class SubscriptionContext:
    """Context for subscription-aware AI interactions"""
    manifest: EntitlementManifest
    tracker: TokenBudgetTracker
    is_platform_mode: bool
    user_id: str
    app_id: str


SYSTEM_PROMPT = """You are a helpful subscription and usage assistant for {app_name}.

## Current Subscription
- Plan: {plan_name} ({plan_tier})
- Billing Period: {billing_period}
- Expires: {expires_at}

## Token Budget
- Period: {token_period}
- Used: {tokens_used:,} / {tokens_limit:,} ({utilization:.1f}%)
- Remaining: {tokens_remaining:,}
- Enforcement: {enforcement}

## Features Enabled
{features_list}

## Rate Limits
{rate_limits_list}

## Guidelines
- Be helpful and transparent about limits
- Explain usage patterns clearly
- If approaching limits, proactively suggest solutions
{platform_guidelines}

## Available Tools
- get_usage_breakdown: Detailed usage by model, workflow, time
- estimate_cost: Estimate tokens/cost for a planned operation
- get_billing_history: Past invoices and payments
{platform_tools}
"""

PLATFORM_GUIDELINES = """
- Explain upgrade paths when relevant
- Help with plan selection based on usage
- Facilitate upgrade/downgrade requests
- Never pressure users - focus on value
"""

PLATFORM_TOOLS = """
- suggest_plan: Recommend optimal plan based on usage
- request_upgrade: Initiate plan upgrade
- request_downgrade: Initiate plan downgrade
- contact_support: Escalate to human support
"""


def create_subscription_manager(
    context: SubscriptionContext,
    llm_config: dict,
) -> ConversableAgent:
    """
    Create a subscription manager agent.
    
    Can be embedded in any app's workflow for subscription-aware assistance.
    """
    manifest = context.manifest
    tracker = context.tracker
    usage = tracker.get_usage_summary()
    
    # Build features list
    features_list = "\n".join([
        f"- {feat}: {'✅ Enabled' if enabled else '❌ Disabled'}"
        for feat, enabled in manifest.features.items()
    ])
    
    # Build rate limits list
    rate_limits_list = "\n".join([
        f"- {limit}: {value if value != -1 else 'Unlimited'}"
        for limit, value in manifest.rate_limits.items()
    ])
    
    # Build system prompt
    system_message = SYSTEM_PROMPT.format(
        app_name=manifest.metadata.get("app_name", manifest.app_id),
        plan_name=manifest.plan.get("name", "Unknown"),
        plan_tier=manifest.plan.get("tier", "unknown"),
        billing_period=manifest.plan.get("billing_period", "none"),
        expires_at=manifest.plan.get("expires_at", "Never"),
        token_period=manifest.token_budget.get("period", "unlimited"),
        tokens_used=usage.get("total_tokens_used", 0),
        tokens_limit=usage.get("total_tokens_limit", -1),
        utilization=usage.get("utilization_percent", 0) or 0,
        tokens_remaining=usage.get("tokens_remaining", "Unlimited"),
        enforcement=manifest.token_budget.get("total_tokens", {}).get("enforcement", "none"),
        features_list=features_list,
        rate_limits_list=rate_limits_list,
        platform_guidelines=PLATFORM_GUIDELINES if context.is_platform_mode else "",
        platform_tools=PLATFORM_TOOLS if context.is_platform_mode else "",
    )
    
    agent = ConversableAgent(
        name="subscription_assistant",
        system_message=system_message,
        llm_config=llm_config,
    )
    
    # Register tools
    _register_core_tools(agent, context)
    
    if context.is_platform_mode:
        _register_platform_tools(agent, context)
    
    return agent


def _register_core_tools(agent: ConversableAgent, context: SubscriptionContext):
    """Register tools available in both OSS and platform mode"""
    
    @register_function(agent)
    def get_usage_breakdown(
        period: str = "current",
        group_by: str = "model"
    ) -> dict:
        """
        Get detailed usage breakdown.
        
        Args:
            period: "current", "last_hour", "last_day", "last_week", "last_month"
            group_by: "model", "workflow", "time"
        
        Returns:
            Detailed usage breakdown
        """
        summary = context.tracker.get_usage_summary()
        
        if group_by == "model":
            return {
                "period": period,
                "by_model": summary.get("per_model", {}),
                "total": summary.get("total_tokens_used", 0),
            }
        
        # Add more groupings as needed
        return summary
    
    @register_function(agent)
    def estimate_cost(
        model: str,
        estimated_input_tokens: int,
        estimated_output_tokens: int,
    ) -> dict:
        """
        Estimate cost for a planned operation.
        
        Args:
            model: Model to use (e.g., "gpt-4", "gpt-3.5-turbo")
            estimated_input_tokens: Estimated input tokens
            estimated_output_tokens: Estimated output tokens
        
        Returns:
            Cost estimate and budget impact
        """
        total = estimated_input_tokens + estimated_output_tokens
        
        # Check against budget
        action, reason = context.tracker.check_budget(model, total)
        
        budget = context.manifest.token_budget
        limit = budget.get("total_tokens", {}).get("limit", -1)
        used = context.tracker._total_used
        
        return {
            "model": model,
            "estimated_tokens": total,
            "can_proceed": action.value in ["allow", "warn"],
            "reason": reason,
            "budget_before": used,
            "budget_after": used + total,
            "budget_limit": limit,
            "utilization_after": (
                round((used + total) / limit * 100, 2) if limit > 0 else None
            ),
        }
    
    @register_function(agent)
    def get_feature_status(feature: str) -> dict:
        """
        Check if a feature is enabled.
        
        Args:
            feature: Feature name (e.g., "code_execution", "multi_agent")
        
        Returns:
            Feature status and plan info
        """
        enabled = context.manifest.features.get(feature, False)
        
        return {
            "feature": feature,
            "enabled": enabled,
            "plan": context.manifest.plan.get("name"),
            "tier": context.manifest.plan.get("tier"),
        }


def _register_platform_tools(agent: ConversableAgent, context: SubscriptionContext):
    """Register tools only available in platform mode"""
    
    @register_function(agent)
    def suggest_plan(based_on: str = "usage") -> dict:
        """
        Suggest optimal plan based on usage patterns.
        
        Args:
            based_on: "usage", "features", "budget"
        
        Returns:
            Plan recommendation with reasoning
        """
        usage = context.tracker.get_usage_summary()
        current_tier = context.manifest.plan.get("tier", "free")
        
        # Simple logic - platform would have more sophisticated
        monthly_tokens = usage.get("total_tokens_used", 0)
        
        if monthly_tokens > 5_000_000:
            suggested = "enterprise"
            reason = "High token usage would benefit from enterprise pricing"
        elif monthly_tokens > 1_000_000:
            suggested = "pro"
            reason = "Usage level is optimal for Pro plan"
        elif monthly_tokens > 100_000:
            suggested = "starter"
            reason = "Starter plan covers your current usage"
        else:
            suggested = "free"
            reason = "Current usage fits within free tier"
        
        return {
            "current_plan": current_tier,
            "suggested_plan": suggested,
            "reason": reason,
            "monthly_tokens": monthly_tokens,
            "would_save": suggested != current_tier,
        }
    
    @register_function(agent)
    def request_upgrade(target_plan: str) -> dict:
        """
        Request plan upgrade.
        
        Args:
            target_plan: Target plan ID (e.g., "starter", "pro", "enterprise")
        
        Returns:
            Upgrade request status and next steps
        """
        # In platform mode, this would call the Platform API
        return {
            "status": "pending",
            "message": f"Upgrade request to {target_plan} initiated",
            "next_step": "You'll be redirected to checkout to confirm the upgrade",
            "checkout_url": f"/checkout?plan={target_plan}&app={context.app_id}",
        }
    
    @register_function(agent)
    def contact_support(issue: str, priority: str = "normal") -> dict:
        """
        Escalate to human support.
        
        Args:
            issue: Description of the issue
            priority: "low", "normal", "high", "urgent"
        
        Returns:
            Support ticket info
        """
        return {
            "status": "created",
            "ticket_id": f"SUP-{context.app_id[:8]}-{hash(issue) % 10000:04d}",
            "priority": priority,
            "message": "A support agent will respond within 24 hours",
            "issue_summary": issue[:200],
        }
```

---

## Environment Configuration

### mozaiks-core (Self-Hosted Mode)

```bash
# No platform connection - full local control
MOZAIKS_DEPLOYMENT_MODE=self-hosted
MOZAIKS_PAYMENT_PROVIDER=noop                    # or "custom"
MOZAIKS_ENTITLEMENT_SOURCE=local                 # local file
MOZAIKS_ENTITLEMENT_MANIFEST_PATH=./entitlements.json
MOZAIKS_ENFORCE_ENTITLEMENTS=false               # optional enforcement
MOZAIKS_USAGE_EXPORT=local                       # export to local metrics
```

### mozaiks-core (Platform Mode)

```bash
# Connected to mozaiks-platform
MOZAIKS_DEPLOYMENT_MODE=platform
MOZAIKS_PAYMENT_PROVIDER=platform                # delegates to platform
MOZAIKS_PLATFORM_API_URL=https://api.mozaiks.io
MOZAIKS_PLATFORM_API_KEY=${PLATFORM_API_KEY}
MOZAIKS_ENTITLEMENT_SOURCE=control_plane         # fetched from platform
MOZAIKS_ENFORCE_ENTITLEMENTS=true                # platform controls
MOZAIKS_USAGE_EXPORT=platform                    # sent to platform
```

### mozaiks-platform

```bash
# Platform services configuration
STRIPE_SECRET_KEY=${STRIPE_SECRET_KEY}
STRIPE_WEBHOOK_SECRET=${STRIPE_WEBHOOK_SECRET}
STRIPE_CONNECT_CLIENT_ID=${STRIPE_CONNECT_CLIENT_ID}
CORE_API_URL=http://core-api:8080                # Core's entitlement sync endpoint
RABBITMQ_URL=amqp://rabbitmq:5672                # Usage event queue
MONGODB_URL=mongodb://mongo:27017                # Persistent storage
```

---

## Data Models Alignment

### Subscription Status (Shared)

Both systems must agree on subscription status shape:

```typescript
// TypeScript for clarity - implement in both C# and Python
interface SubscriptionStatus {
  isActive: boolean;
  planId: string | null;
  planName: string | null;
  tier: "free" | "starter" | "pro" | "enterprise" | "unlimited";
  expiresAt: string | null;  // ISO datetime
  subscriptionId: string | null;  // Stripe subscription ID
  features: string[];  // Enabled features
  tokenBudget: {
    period: "monthly" | "unlimited";
    limit: number;  // -1 = unlimited
    used: number;
    enforcement: "none" | "warn" | "soft" | "hard";
  };
}
```

### Usage Event (Core → Platform)

```typescript
interface UsageEvent {
  eventType: "chat.usage_delta" | "chat.usage_summary" | "workflow.completed";
  timestamp: string;  // ISO datetime
  appId: string;
  userId: string;
  tenantId?: string;
  workflowId: string;
  workflowName: string;
  model: string;
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
  durationMs: number;
  metadata?: Record<string, any>;
}
```

### Entitlement Manifest (Platform → Core)

```typescript
interface EntitlementManifest {
  version: "1.0";
  appId: string;
  tenantId?: string;
  plan: {
    id: string;
    name: string;
    tier: string;
    billingPeriod: "none" | "monthly" | "annual";
    expiresAt: string | null;
  };
  tokenBudget: {
    period: "hourly" | "daily" | "monthly" | "lifetime" | "unlimited";
    totalTokens: {
      limit: number;
      used: number;
      reserved: number;
      enforcement: "none" | "warn" | "soft" | "hard";
    };
    perModelLimits?: Record<string, {
      limit: number;
      used: number;
      enforcement: string;
    }>;
    rollover: boolean;
    burstAllowance?: number;
  };
  features: Record<string, boolean>;
  rateLimits: Record<string, number>;
  observability: {
    level: "minimal" | "standard" | "detailed" | "full";
    tokenTracking: boolean;
    costTracking: boolean;
    retentionDays: number;
    exportEnabled: boolean;
  };
  plugins: {
    allowed: string[];
    blocked: string[];
  };
  overrides: {
    trialExtension?: { until: string; reason: string };
    promotionalTokens?: number;
    featurePreviews?: string[];
  };
  metadata: Record<string, any>;
}
```

---

## Success Criteria

### Phase 1 Complete When:

- [ ] IPaymentProvider interface exists in Core
- [ ] NoOpProvider works for self-hosted
- [ ] PlatformPaymentProvider delegates to Platform
- [ ] EntitlementManifest is a validated dataclass
- [ ] Core has `/api/entitlements/sync` endpoint
- [ ] Platform creates manifests from subscriptions
- [ ] Platform syncs manifests to Core on subscription events

### Phase 2 Complete When:

- [ ] Core runtime emits usage events
- [ ] Platform receives and processes usage events
- [ ] Usage billing pipeline calculates costs
- [ ] Meters update in real-time
- [ ] Overage triggers manifest update

### Phase 3 Complete When:

- [ ] Cap table persisted in MongoDB
- [ ] Distribution engine calculates investor shares
- [ ] Payouts scheduled automatically
- [ ] Monetization.API returns real data

### Phase 4 Complete When:

- [ ] SubscriptionManagerWorkflow is functional
- [ ] Self-hosters can use it for usage awareness
- [ ] Platform users get upgrade assistance
- [ ] Tools work in both modes

---

## Risk Mitigation

| Risk | Impact | Mitigation |
|------|--------|------------|
| Manifest sync fails | Runtime allows/blocks incorrectly | Use cached manifest + alert |
| Usage events lost | Revenue leakage | Persistent queue + retry |
| Cap table inconsistent | Investor disputes | Event sourcing + audit log |
| Platform unreachable | Self-hosters blocked | Core never requires platform |
| Schema version mismatch | Parsing errors | Version field + migration path |

---

## Questions for Platform Team

1. **Webhook forwarding**: Should subscription webhooks go to Platform first, then Platform notifies Core? Or both receive?

2. **Usage event transport**: RabbitMQ vs HTTP webhook vs gRPC streaming for usage events?

3. **Cap table authority**: Should Core ever have a copy of cap table, or always query Platform?

4. **Manifest TTL**: How long should Core cache manifests before requiring refresh?

5. **Overage behavior**: When budget exceeded, should Platform update manifest enforcement and sync, or should Core query Platform?

---

## Appendix: Core Implementation Status

### mozaiks-core (✅ COMPLETE)

```
backend/src/Billing.API/
├── Contracts/
│   └── IPaymentProvider.cs              ✅
├── Providers/
│   ├── NoOpPaymentProvider.cs           ✅
│   └── PlatformPaymentProvider.cs       ✅
└── Payment.API/
    ├── Controllers/
    │   ├── EntitlementSyncController.cs ✅
    │   └── UsageEventController.cs      ✅
    └── Repository/
        ├── EntitlementManifestRepository.cs ✅
        └── UsageEventRepository.cs      ✅

runtime/ai/src/core/entitlements/
├── __init__.py                          ✅
├── manifest.py                          ✅
├── sources.py                           ✅
├── token_tracker.py                     ✅
├── feature_gate.py                      ✅
└── middleware.py                        ✅

runtime/ai/src/workflows/
└── subscription_manager.py              ✅
```

### mozaiks-platform (⏳ TODO)

See [PLATFORM_COORDINATION.md](./PLATFORM_COORDINATION.md) for required implementations:

```
src/Services/
├── Monetization/
│   ├── EntitlementManager.cs            ⏳ TODO
│   ├── UsageBillingPipeline.cs          ⏳ TODO
│   └── CapTableRepository.cs            ⏳ TODO
└── Payment/
    └── CoreEntitlementClient.cs         ⏳ TODO
```
