# Core ↔ Platform Integration: Coordination Message

> 🔁 **MESSAGE TO mozaiks-platform (VIA HUMAN)**
> 
> This document captures the required Platform-side implementations to complete
> the Core ↔ Platform entitlement alignment. Core-side implementation is complete.

---

## Summary

mozaiks-core has implemented the following components as part of the unified
entitlement and billing alignment:

### ✅ Core-Side Completed Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `IPaymentProvider` | `Billing.API/Contracts/IPaymentProvider.cs` | Abstract payment interface |
| `NoOpPaymentProvider` | `Billing.API/Providers/NoOpPaymentProvider.cs` | Self-hosted/dev mode |
| `PlatformPaymentProvider` | `Billing.API/Providers/PlatformPaymentProvider.cs` | Platform delegation |
| `EntitlementSyncController` | `Payment.API/Controllers/EntitlementSyncController.cs` | Receives manifests from Platform |
| `EntitlementManifestRepository` | `Payment.API/Repository/EntitlementManifestRepository.cs` | MongoDB storage for manifests |
| `UsageEventController` | `Payment.API/Controllers/UsageEventController.cs` | Receives usage events from runtime |
| `UsageEventRepository` | `Payment.API/Repository/UsageEventRepository.cs` | MongoDB storage for usage events |
| Entitlements Python package | `runtime/ai/src/core/entitlements/` | Full entitlement enforcement in Python |
| `SubscriptionManagerWorkflow` | `runtime/ai/src/workflows/subscription_manager.py` | AI workflow for subscription help |

---

## Platform-Side Required Implementations

### 1. EntitlementManager Service

**Location:** `src/Services/Monetization/` (or new `Entitlements/` service)

**Purpose:** Manages the mapping from Platform subscriptions to Core entitlement manifests.

**Required Methods:**

```csharp
public interface IEntitlementManager
{
    /// <summary>
    /// Called when subscription status changes. Creates manifest and syncs to Core.
    /// </summary>
    Task OnSubscriptionChangedAsync(SubscriptionChangedEvent evt, CancellationToken ct);
    
    /// <summary>
    /// Builds an EntitlementManifest from current subscription state.
    /// </summary>
    Task<EntitlementManifest> BuildManifestForAppAsync(string appId, CancellationToken ct);
    
    /// <summary>
    /// Pushes manifest to Core's EntitlementSyncController.
    /// </summary>
    Task SyncManifestToCoreAsync(EntitlementManifest manifest, CancellationToken ct);
}
```

**Key Integration Points:**
- Subscribe to `SubscriptionCreated`, `SubscriptionUpgraded`, `SubscriptionCancelled` events
- Call Core's `POST /api/v1/entitlements/sync` endpoint
- Use existing subscription plan definitions to determine features/token limits

### 2. UsageBillingPipeline

**Location:** `src/Services/Monetization/Billing/`

**Purpose:** Processes usage events from Core and generates billing records.

**Required Methods:**

```csharp
public interface IUsageBillingPipeline
{
    /// <summary>
    /// Receives usage events from Core (via message queue or HTTP).
    /// </summary>
    Task ProcessUsageEventAsync(TokenUsageEvent evt, CancellationToken ct);
    
    /// <summary>
    /// Aggregates usage for billing period and creates invoice line items.
    /// </summary>
    Task GenerateUsageInvoiceItemsAsync(string appId, DateRange period, CancellationToken ct);
}
```

**Key Integration Points:**
- Listen for usage events from Core (message queue preferred)
- Aggregate by billing period
- Create Stripe metered billing records if using usage-based pricing

### 3. CapTableRepository (Persistence)

**Location:** `src/Services/Monetization/Repository/`

**Purpose:** Current in-memory cap_table needs MongoDB persistence.

**Current State (from platform docs):**
> "Cap table is in-memory only. Monthly resets are not persisted."

**Required:**
- MongoDB collection `cap_table_entries`
- Track `app_id`, `token_limit`, `tokens_used`, `reset_at_utc`, `updated_at_utc`
- Support atomic increment operations
- Support monthly reset via background job

### 4. Core Sync HTTP Client

**Location:** `src/BuildingBlocks/` or `src/Services/Monetization/`

**Purpose:** HTTP client for calling Core's entitlement APIs.

```csharp
public class CoreEntitlementClient
{
    private readonly HttpClient _http;
    
    public Task SyncManifestAsync(EntitlementManifestDto manifest, CancellationToken ct);
    public Task<EntitlementManifestDto> GetManifestAsync(string appId, CancellationToken ct);
}
```

---

## API Contract

### Core Endpoint: POST /api/v1/entitlements/sync

**Request Body:**
```json
{
  "appId": "string",
  "tier": "free|starter|pro|enterprise",
  "features": ["rag", "streaming", "code_interpreter"],
  "tokenBudget": {
    "limit": 100000,
    "used": 5000,
    "resetAtUtc": "2025-02-01T00:00:00Z"
  },
  "enforcement": "none|warn|soft|hard",
  "version": 1,
  "syncedAtUtc": "2025-01-15T10:30:00Z",
  "metadata": {}
}
```

**Response:**
```json
{
  "status": "synced",
  "appId": "...",
  "version": 1
}
```

### Core Endpoint: GET /api/v1/entitlements/{appId}

Returns current manifest for an app (or default "free" manifest if not found).

---

## Sequence Diagram

```
┌──────────┐         ┌──────────┐         ┌────────────┐
│ Platform │         │   Core   │         │ AI Runtime │
└────┬─────┘         └────┬─────┘         └─────┬──────┘
     │                    │                     │
     │ SubscriptionCreated│                     │
     │ (internal event)   │                     │
     │                    │                     │
     │ BuildManifest()    │                     │
     │──────────────────> │                     │
     │                    │                     │
     │ POST /entitlements/sync                  │
     │───────────────────>│                     │
     │                    │ Store manifest      │
     │                    │──────────────────>  │
     │                    │                     │
     │ {"status":"synced"}│                     │
     │<───────────────────│                     │
     │                    │                     │
     │                    │ GET /entitlements/{appId}
     │                    │<────────────────────│
     │                    │                     │
     │                    │ Return manifest     │
     │                    │────────────────────>│
     │                    │                     │
     │                    │ (Workflow executes) │
     │                    │                     │
     │                    │ POST /usage/tokens  │
     │                    │<────────────────────│
     │                    │                     │
     │ Forward usage event│                     │
     │<───────────────────│                     │
     │                    │                     │
     │ ProcessUsageEvent()│                     │
     │──────────────────> │                     │
```

---

## Configuration Required

### Core appsettings.json

```json
{
  "Platform": {
    "Enabled": true,
    "EntitlementSyncEndpoint": "http://platform-api/api/v1/entitlements"
  }
}
```

### Platform appsettings.json

```json
{
  "Core": {
    "EntitlementSyncEndpoint": "http://core-billing-api/api/v1/entitlements/sync",
    "UsageEventEndpoint": "http://core-billing-api/api/v1/usage/tokens",
    "ServiceApiKey": "..."
  }
}
```

---

## Timeline Suggestion

| Week | Task |
|------|------|
| Week 1 | Implement CapTableRepository with MongoDB persistence |
| Week 1 | Implement CoreEntitlementClient |
| Week 2 | Implement EntitlementManager with subscription event handlers |
| Week 2 | Wire up to existing Payment.API subscription flow |
| Week 3 | Implement UsageBillingPipeline |
| Week 3 | Integration testing with Core |

---

## Questions for Platform Team

1. **Subscription event bus:** What message broker/event system does Platform use?
   - Core can forward usage events via HTTP or message queue

2. **Service auth:** What authentication mechanism should Core use for service-to-service calls?
   - Core's `EntitlementSyncController` expects "PlatformService" policy

3. **Feature mapping:** Where is the canonical list of features per tier defined?
   - Core needs this to validate manifest contents

---

*Generated: 2025-01-15*
*Spec Version: 1.0*
*Core Implementation Status: COMPLETE*
