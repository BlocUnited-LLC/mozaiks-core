# Subscription Model Proposal: Declarative Runtime

This document clarifies the subscription model architecture and proposes changes
to keep runtime purely declarative and enforcement-only.

---

## Current State Summary

| Component | Location | Purpose |
|-----------|----------|---------|
| `subscription_config.json` | Runtime config | Plan definitions + `plugins_unlocked` mapping |
| `SubscriptionManager` | Runtime | Reads user subscription state, enforces plugin access |
| `SubscriptionStub` | Runtime | Grants unlimited access when `MONETIZATION=0` |
| `/api/internal/subscription/sync` | Runtime endpoint | Control-plane pushes user subscription state |

**No `AppMonetizationSpec` exists in the codebase.** The term may refer to a future
control-plane concept. Currently, `subscription_config.json` is the only plan
definition file.

---

## 1. Source of Truth: Two-Layer Model

### Proposal: Split Concerns

| Layer | Source of Truth | Owner | Contents |
|-------|-----------------|-------|----------|
| **Plan Definitions** | `subscription_config.json` | Build-time / App Developer | Plan names, display names, prices, `plugins_unlocked` |
| **User Entitlements** | Control-Plane sync → MongoDB | Control-Plane | User's current plan, status, billing dates |

### Rationale

- **Plan definitions** are static per app version. They define *what* each plan unlocks.
- **User entitlements** are dynamic. They reflect *which* plan a user has purchased.
- Runtime only needs to:
  1. Read plan definitions (static config)
  2. Read user's current plan (from MongoDB, synced by control-plane)
  3. Enforce: `user.plan` ∈ `plugins_unlocked[plugin]` → allow

---

## 2. subscription_config.json: Static vs Dynamic?

### Recommendation: **Static at Build Time**

```
┌─────────────────────────────────────────────────────────────────┐
│  subscription_config.json should be STATIC at build time        │
│                                                                 │
│  Reasons:                                                       │
│  • Plugin code expects specific plan names to exist             │
│  • Frontend displays plan features (must match backend)         │
│  • Changing plans mid-flight creates upgrade/downgrade chaos    │
│  • Version control: plans are part of the app contract          │
└─────────────────────────────────────────────────────────────────┘
```

### When Plans Change

Plan changes require a **new app deployment**, not a runtime hot-reload.

| Scenario | Solution |
|----------|----------|
| Add new plan | Deploy new version with updated `subscription_config.json` |
| Change plan pricing | Update config, redeploy. Control-plane handles Stripe pricing. |
| Add plugin to plan | Update `plugins_unlocked`, redeploy |
| Remove/rename plan | Deprecation cycle: keep old plan in config, migrate users via control-plane |

### What Control-Plane Syncs

Control-plane should **never** push plan definitions. It only pushes:

```json
{
  "user_id": "user_123",
  "plan": "premium",       // Must match a plan name in subscription_config.json
  "status": "active",
  "billing_cycle": "monthly",
  "next_billing_date": "2024-02-01T00:00:00Z"
}
```

Runtime validates that `plan` exists in its static config. If not, it should:
1. Log a warning
2. Fall back to `default_plan` (usually "free")

---

## 3. Runtime Updates When Plans Change

### Recommendation: **No Hot-Reload Endpoint**

Do **not** add a `/api/internal/plans/sync` endpoint. Plans are code artifacts.

### Extending `/api/internal/subscription/sync`

The existing endpoint is already correct. No changes needed for user-level sync.

### Optional Enhancement: Plan Validation

Add validation to `sync_subscription_from_control_plane`:

```python
async def sync_subscription_from_control_plane(self, user_id: str, subscription_data: dict, *, _internal_call: bool = False):
    _require_internal_call("sync_from_control_plane", _internal_call)
    
    plan = subscription_data.get("plan", "free")
    
    # Validate plan exists in config
    valid_plans = {p["name"] for p in self.get_available_plans()}
    if plan not in valid_plans:
        logger.warning(f"⚠️ Unknown plan '{plan}' for user {user_id}. Falling back to default.")
        settings = self.subscription_config.get("settings", {})
        plan = settings.get("default_plan", "free")
    
    # ... rest of sync logic
```

---

## 4. Two Worlds: Mozaiks vs User App Entitlements

### Current Problem

The system conflates two different entitlement domains:

| Domain | Examples | Who Enforces? |
|--------|----------|---------------|
| **Mozaiks Platform** | Hosting tier, custom domains, email quotas, AI tokens | Control-Plane / Gateway |
| **User App** | Plugin access, app features, app-specific AI capabilities | Runtime |

### Proposed Separation

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                         ENTITLEMENT DOMAINS                                  │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌─────────────────────────────────┐  ┌─────────────────────────────────┐   │
│  │   MOZAIKS PLATFORM (upstream)   │  │   USER APP (runtime)            │   │
│  │                                 │  │                                 │   │
│  │  • Hosting tier (scale limits)  │  │  • Plugin access                │   │
│  │  • Custom domains               │  │  • App features (feature flags) │   │
│  │  • Email sending quotas         │  │  • App-level AI capabilities    │   │
│  │  • Platform AI token budget     │  │  • Subscription tiers           │   │
│  │  • SSL certificates             │  │  • Trial status                 │   │
│  │  • Storage limits               │  │                                 │   │
│  │                                 │  │                                 │   │
│  │  Enforced by: Gateway/Infra     │  │  Enforced by: Runtime           │   │
│  │  Configured: Platform UI        │  │  Configured: subscription_config│   │
│  └─────────────────────────────────┘  └─────────────────────────────────┘   │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

### Implementation

#### 1. Keep Existing Sync for User Entitlements

`/api/internal/subscription/sync` continues to handle **user-level** entitlements.

#### 2. Add New Endpoint for Platform Entitlements (Optional)

If runtime needs to know platform limits (e.g., to display "X tokens remaining"):

```python
# /backend/core/routes/platform_entitlements.py

router = APIRouter(prefix="/api/internal/platform", tags=["internal-platform"])

class PlatformEntitlementsSync(BaseModel):
    app_id: str
    hosting_tier: str  # "starter" | "growth" | "enterprise"
    monthly_email_limit: int
    monthly_ai_token_budget: int
    custom_domains_allowed: int
    # ... etc

@router.post("/entitlements/sync")
async def sync_platform_entitlements(
    payload: PlatformEntitlementsSync,
    _internal: dict = Depends(require_internal_api_key),
):
    # Store in a separate collection, not subscriptions
    await platform_entitlements_collection.update_one(
        {"app_id": payload.app_id},
        {"$set": payload.model_dump()},
        upsert=True
    )
    return {"success": True}
```

#### 3. Gating Strategy

| What | Where to Gate | How |
|------|---------------|-----|
| Plugin execution | Runtime (`director.py`) | `is_plugin_accessible(user_id, plugin)` |
| App features | Runtime (plugin logic) | Feature flags in subscription_config |
| Email sending | Gateway/Email Service | Platform entitlements check upstream |
| AI token usage | Gateway/AI Proxy | Token budget enforced upstream |
| Custom domains | DNS/Infra layer | Platform entitlements in control-plane |

**Runtime should NOT enforce:**
- Email quotas (email service responsibility)
- AI token hard caps (gateway/proxy responsibility)
- Hosting limits (infra/orchestrator responsibility)
- Domain SSL (infra responsibility)

---

## 5. Self-Hosting Mode

### What Must Work

| Feature | Works? | How |
|---------|--------|-----|
| Authentication | ✅ | Local auth or Keycloak |
| Plugin execution | ✅ | `SubscriptionStub` grants all access |
| Database (MongoDB) | ✅ | Self-hosted or Atlas |
| Subscription UI | ✅ | Shows "unlimited" plan |
| Notifications (in-app) | ✅ | WebSocket + MongoDB |
| Settings management | ✅ | Local storage |

### What Breaks (Acceptable)

| Feature | Breaks? | Reason | Acceptable? |
|---------|---------|--------|-------------|
| Stripe payments | ❌ | No control-plane | ✅ (self-host = no billing) |
| Email notifications | ⚠️ | Needs `EMAIL_SERVICE_URL` | ✅ (optional) |
| SMS notifications | ⚠️ | Needs Twilio config | ✅ (optional) |
| Platform AI tokens | ❌ | No token budget | ✅ (use own OpenAI key) |
| Custom domains | ❌ | No infra management | ✅ (manual DNS) |
| Subscription sync | ❌ | No control-plane | ✅ (stub handles it) |

### Self-Host Contract

```dotenv
# Minimal self-hosted config
MONETIZATION=0               # Disables subscription gating
MOZAIKS_MANAGED=false        # No platform integration
DATABASE_URI=mongodb://...   # Required
JWT_SECRET=...               # Required

# Optional
OPENAI_API_KEY=...           # For AI features
EMAIL_SERVICE_URL=...        # For email notifications
```

When `MONETIZATION=0`:
- `SubscriptionStub` is used instead of `SubscriptionManager`
- All plugins are accessible
- `/api/internal/subscription/sync` is ignored (no-op or 404)
- Subscription UI shows "Unlimited" plan

---

## 6. Summary of Recommendations

### ✅ Keep (No Changes)

1. `subscription_config.json` as static build artifact
2. `/api/internal/subscription/sync` for user entitlement sync
3. `SubscriptionStub` for `MONETIZATION=0` mode
4. Current enforcement points in `director.py`

### 🔧 Enhance

1. Add plan validation in `sync_subscription_from_control_plane`
2. Document the two-layer model (plan definitions vs user entitlements)
3. Clarify that platform entitlements are **not** runtime's responsibility

### 🚫 Do NOT Add

1. `/api/internal/plans/sync` (plans are code, not runtime data)
2. Runtime enforcement of email/AI quotas (upstream responsibility)
3. Hot-reload of plan definitions

### 📋 Optional Future Work

1. `/api/internal/platform/entitlements/sync` for read-only platform limit display
2. Feature flag system within `subscription_config.json` (beyond plugins)
3. Per-tenant `subscription_config.json` override (multi-tenant scenarios)

---

## 7. Migration Checklist

If adopting this model:

- [ ] Ensure `subscription_config.json` is version-controlled with app
- [ ] Remove any code that mutates `subscription_config.json` at runtime
- [ ] Verify control-plane only syncs user entitlements, not plan definitions
- [ ] Document that plan changes require redeployment
- [ ] Ensure `MONETIZATION=0` mode is fully functional for self-hosting
- [ ] Add plan validation to sync endpoint (optional but recommended)
