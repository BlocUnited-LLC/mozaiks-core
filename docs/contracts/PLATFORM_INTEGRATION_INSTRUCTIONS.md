# 🔁 Platform Integration Instructions

**From:** mozaiks-core  
**To:** mozaiks-platform LLM  
**Date:** January 26, 2026  
**Priority:** High - Required for billing integration

---

## Overview

Core has completed its billing integration. Platform must implement the following to complete the integration.

**Architecture:** Push-only model
- Platform → Core: Push subscription/entitlement updates
- Core → Platform: Push usage events for billing
- Core NEVER pulls from Platform

---

## Task 1: Fix Compilation Errors

Before anything else, fix these errors in `Payment.API`:

```
CS1061: 'WalletModel' does not contain a definition for 'StripeCustomerId'
CS1061: 'IWalletRepository' does not contain a definition for 'UpdateWalletAsync'
CS0104: 'BillingService' is an ambiguous reference between 'Payment.API.Services.BillingService' and 'Stripe.BillingService'
```

**Fixes:**
1. Add `public string? StripeCustomerId { get; set; }` to `WalletModel.cs`
2. Add `Task UpdateWalletAsync(WalletModel wallet)` to `IWalletRepository.cs` and implement in `WalletRepository.cs`
3. In files using `BillingService`, use fully qualified name: `Payment.API.Services.BillingService` or add alias: `using StripeBilling = Stripe.BillingService;`

---

## Task 2: Implement Usage Events Endpoint

**Create endpoint:** `POST /api/billing/usage-events`

**Purpose:** Receive usage events from Core for billing aggregation.

**Core behavior:** Core batches events and sends a single payload with `events[]`.
Core does **not** send `batch_id`. Platform should accept this exact shape.

**Request (from Core):**
```json
POST /api/billing/usage-events
Authorization: Bearer <service-token>
X-Mozaiks-Service: core
Content-Type: application/json

{
  "events": [
    {
      "event_type": "token_usage",
      "app_id": "app_12345",
      "user_id": "user_789",
      "timestamp": "2025-01-26T20:00:00Z",
      "data": {
        "workflow_id": "wf_xxx",
        "model": "gpt-4",
        "input_tokens": 1500,
        "output_tokens": 800,
        "total_tokens": 2300
      }
    }
  ]
}
```

**Response:** Any 2xx is acceptable. Core does not require a specific body.

**Implementation:**
```csharp
// Controllers/BillingController.cs

[HttpPost("usage-events")]
public async Task<IActionResult> ReceiveUsageEvents(
    [FromBody] UsageEventBatch batch,
    [FromHeader(Name = "Authorization")] string authHeader)
{
    // Validate service key
    if (!ValidateServiceKey(authHeader, "sk_c2p_"))
    {
        return Unauthorized();
    }
    
    // Store events for billing aggregation
    foreach (var evt in batch.Events)
    {
        await _usageRepository.InsertAsync(new UsageEvent
        {
            EventId = evt.EventId,
            EventType = evt.EventType,
            AppId = evt.AppId,
            UserId = evt.UserId,
            Timestamp = evt.Timestamp,
            Data = evt.Data,
            BatchId = batch.BatchId,
            ReceivedAt = DateTime.UtcNow
        });
    }

    return Accepted(new { Accepted = batch.Events.Count, Rejected = 0 });
}

private bool ValidateServiceKey(string authHeader, string expectedPrefix)
{
    if (string.IsNullOrEmpty(authHeader)) return false;
    var parts = authHeader.Split(' ');
    if (parts.Length != 2 || parts[0] != "Bearer") return false;
    var key = parts[1];
    // Check against configured allowed keys
    var allowedKeys = _config.GetValue<string>("AllowedCoreServiceKeys")?.Split(',') ?? Array.Empty<string>();
    return allowedKeys.Any(k => k.Trim() == key);
}
```

---

## Task 3: Implement Stripe Webhook → Core Sync

When Stripe sends a subscription webhook, Platform must call Core's sync endpoints.

**Stripe Events to Handle:**
- `customer.subscription.created`
- `customer.subscription.updated`
- `customer.subscription.deleted`
- `invoice.paid`
- `invoice.payment_failed`

**For each event, call BOTH Core endpoints:**

### 3a. User Subscription Sync

```
POST {CORE_URL}/api/internal/subscription/sync
X-Internal-API-Key: {your-internal-key}   # must match INTERNAL_API_KEY in core
Content-Type: application/json

{
  "userId": "user_123",
  "plan": "pro",
  "status": "active",
  "billingCycle": "monthly",
  "nextBillingDate": "2025-02-26T00:00:00Z",
  "trialEndDate": null,
  "stripeSubscriptionId": "sub_xxx",
  "appId": "app_456"
}
```

### 3b. App Entitlements Sync

```
POST {CORE_URL}/api/v1/entitlements/{app_id}/sync
Authorization: Bearer sk_p2c_live_xxx     # must be in MOZAIKS_ALLOWED_SERVICE_KEYS
Content-Type: application/json

{
  "version": "1.0",
  "app_id": "app_456",
  "tenant_id": "tenant_xyz",
  "plan": {
    "id": "plan_pro_monthly",
    "name": "Pro Plan",
    "tier": "pro",
    "billing_period": "monthly",
    "expires_at": "2025-12-31T23:59:59Z"
  },
  "token_budget": {
    "period": "monthly",
    "total_tokens": {
      "limit": 100000,
      "used": 0,
      "enforcement": "soft"
    }
  },
  "features": {
    "workflow_execution": true,
    "multi_agent": true,
    "code_execution": true,
    "function_calling": true,
    "vision": true,
    "mcp_tools": true
  },
  "rate_limits": {
    "requests_per_minute": 60,
    "concurrent_workflows": 5
  },
  "correlation_id": "stripe_evt_xxx"
}
```

**Implementation:**
```csharp
// Services/CoreSyncService.cs

public class CoreSyncService : ICoreSyncService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    
    public CoreSyncService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }
    
    public async Task SyncSubscriptionToCore(string userId, SubscriptionData sub)
    {
        var coreUrl = _config["CoreUrl"]; // http://localhost:8000
        var internalKey = _config["CoreInternalApiKey"];
        
        // 1. Sync user subscription
        var userSyncRequest = new HttpRequestMessage(HttpMethod.Post, 
            $"{coreUrl}/api/internal/subscription/sync");
        userSyncRequest.Headers.Add("X-Internal-API-Key", internalKey);
        userSyncRequest.Content = JsonContent.Create(new
        {
            userId = userId,
            plan = sub.PlanName,
            status = MapStatus(sub.Status),
            billingCycle = sub.BillingInterval,
            nextBillingDate = sub.CurrentPeriodEnd,
            stripeSubscriptionId = sub.StripeSubscriptionId,
            appId = sub.AppId
        });
        await _httpClient.SendAsync(userSyncRequest);
        
        // 2. Sync app entitlements
        var serviceKey = _config["CoreServiceKey"]; // sk_p2c_live_xxx
        var entitlementRequest = new HttpRequestMessage(HttpMethod.Post,
            $"{coreUrl}/api/v1/entitlements/{sub.AppId}/sync");
        entitlementRequest.Headers.Add("Authorization", $"Bearer {serviceKey}");
        entitlementRequest.Content = JsonContent.Create(new
        {
            version = "1.0",
            app_id = sub.AppId,
            plan = new
            {
                id = sub.StripePriceId,
                name = sub.PlanName,
                tier = GetTierFromPlan(sub.PlanName),
                billing_period = sub.BillingInterval
            },
            token_budget = new
            {
                period = "monthly",
                total_tokens = new
                {
                    limit = GetTokenLimitForPlan(sub.PlanName),
                    used = 0,
                    enforcement = "soft"
                }
            },
            features = GetFeaturesForPlan(sub.PlanName),
            rate_limits = GetRateLimitsForPlan(sub.PlanName)
        });
        await _httpClient.SendAsync(entitlementRequest);
    }
    
    private string GetTierFromPlan(string planName) => planName.ToLower() switch
    {
        "free" => "free",
        "starter" => "starter",
        "pro" => "pro",
        "enterprise" => "enterprise",
        _ => "free"
    };
    
    private int GetTokenLimitForPlan(string planName) => planName.ToLower() switch
    {
        "free" => 10000,
        "starter" => 100000,
        "pro" => 500000,
        "enterprise" => -1, // unlimited
        _ => 10000
    };
    
    private Dictionary<string, bool> GetFeaturesForPlan(string planName)
    {
        var features = new Dictionary<string, bool>
        {
            ["workflow_execution"] = true,
            ["function_calling"] = true
        };
        
        if (planName.ToLower() is "pro" or "enterprise")
        {
            features["multi_agent"] = true;
            features["code_execution"] = true;
            features["vision"] = true;
            features["mcp_tools"] = true;
        }
        
        return features;
    }
    
    private Dictionary<string, int> GetRateLimitsForPlan(string planName) => planName.ToLower() switch
    {
        "free" => new() { ["requests_per_minute"] = 10, ["concurrent_workflows"] = 1 },
        "starter" => new() { ["requests_per_minute"] = 30, ["concurrent_workflows"] = 3 },
        "pro" => new() { ["requests_per_minute"] = 60, ["concurrent_workflows"] = 10 },
        "enterprise" => new() { ["requests_per_minute"] = -1, ["concurrent_workflows"] = -1 },
        _ => new() { ["requests_per_minute"] = 10, ["concurrent_workflows"] = 1 }
    };
}
```

**In Stripe Webhook Handler:**
```csharp
// Controllers/StripeWebhookController.cs

[HttpPost("webhook")]
public async Task<IActionResult> HandleStripeWebhook()
{
    var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
    var stripeEvent = EventUtility.ConstructEvent(json, 
        Request.Headers["Stripe-Signature"], 
        _config["Stripe:WebhookSecret"]);
    
    switch (stripeEvent.Type)
    {
        case "customer.subscription.created":
        case "customer.subscription.updated":
            var subscription = stripeEvent.Data.Object as Subscription;
            await _coreSyncService.SyncSubscriptionToCore(
                GetUserIdFromCustomer(subscription.CustomerId),
                MapToSubscriptionData(subscription));
            break;
            
        case "customer.subscription.deleted":
            var canceledSub = stripeEvent.Data.Object as Subscription;
            await _coreSyncService.SyncSubscriptionToCore(
                GetUserIdFromCustomer(canceledSub.CustomerId),
                new SubscriptionData 
                { 
                    PlanName = "free", 
                    Status = "canceled",
                    AppId = GetAppIdFromMetadata(canceledSub)
                });
            break;
    }
    
    return Ok();
}
```

---

## Task 4: Configuration

Add to `appsettings.json`:
```json
{
  "CoreIntegration": {
    "CoreUrl": "http://localhost:8000",
    "CoreInternalApiKey": "your-internal-api-key",
    "CoreServiceKey": "sk_p2c_dev_test_key_67890"
  },
  "AllowedCoreServiceKeys": "sk_c2p_dev_test_key_12345"
}
```

Add to `Program.cs`:
```csharp
builder.Services.AddHttpClient<ICoreSyncService, CoreSyncService>();
```

---

## Test Configuration

**For local testing, use these keys:**

| Key | Value | Purpose |
|-----|-------|---------|
| Core → Platform | `sk_c2p_dev_test_key_12345` | Core sends usage events |
| Platform → Core | `sk_p2c_dev_test_key_67890` | Platform syncs entitlements |
| Internal API Key | (your choice) | Platform syncs user subscriptions |

**Core's `.env` is already configured with:**
```
MOZAIKS_PLATFORM_URL=http://localhost:5000
MOZAIKS_PLATFORM_API_KEY=sk_c2p_dev_test_key_12345
MOZAIKS_ALLOWED_SERVICE_KEYS=sk_p2c_dev_test_key_67890
```

---

## Testing Steps

Once Platform is ready:

1. **Start Platform:**
   ```bash
   cd mozaiks-platform/src/Services/Payment/Payment.API
   dotnet run --urls "http://localhost:5000"
   ```

2. **Start Core:**
   ```bash
   cd mozaiks-core/runtime/ai
   python -m uvicorn core.director:app --port 8000
   ```

3. **Test Platform → Core sync:**
   ```bash
   curl -X POST http://localhost:8000/api/v1/entitlements/test-app/sync \
     -H "Authorization: Bearer sk_p2c_dev_test_key_67890" \
     -H "Content-Type: application/json" \
     -d '{"version":"1.0","app_id":"test-app","plan":{"tier":"pro","name":"Pro"},"token_budget":{"total_tokens":{"limit":100000}},"features":{"code_execution":true}}'
   ```
   
   **Expected:** `{"status":"synced","app_id":"test-app",...}`

4. **Test Core → Platform usage:**
   ```bash
   curl -X POST http://localhost:5000/api/billing/usage-events \
     -H "Authorization: Bearer sk_c2p_dev_test_key_12345" \
     -H "Content-Type: application/json" \
     -d '{"event_type":"token_usage","app_id":"test-app","user_id":"user-1","timestamp":"2025-01-26T20:00:00Z","data":{"total_tokens":500,"model":"gpt-4"}}'
   ```
   
   **Expected:** `{"status":"accepted"}` or similar

5. **Run integration test:**
   ```bash
   cd mozaiks-core/runtime/ai
   python tests/integration/test_billing_integration.py
   ```

---

## Summary Checklist

- [ ] Fix `WalletModel.StripeCustomerId` compilation error
- [ ] Fix `IWalletRepository.UpdateWalletAsync` compilation error  
- [ ] Fix `BillingService` ambiguous reference error
- [ ] Create `POST /api/billing/usage-events` endpoint
- [ ] Create `ICoreSyncService` to call Core's sync endpoints
- [ ] Update Stripe webhook handler to call Core sync
- [ ] Add configuration for Core URLs and keys
- [ ] Test locally with provided keys

---

## Questions?

If anything is unclear, reply with specific questions. Core is ready and waiting.

**Port:** Platform should run on `5000` (or update Core's `.env`)

