# 📖 CANONICAL ARCHITECTURE TRUTH

> **Last Updated:** January 27, 2026  
> **Status:** AUTHORITATIVE - Only FINISHED items listed as canonical

This document is the single source of truth for **completed** mozaiks architecture decisions.

---

## ✅ CANONICAL (FINISHED & WORKING)

### 1. Auth: Keycloak OIDC ✅

| Aspect | Decision |
|--------|----------|
| **Identity Provider** | Keycloak OIDC |
| **Realm** | `mozaiks` (shared by Core + Platform) |
| **Token Format** | JWT via OIDC discovery |
| **Validation** | `builder.AddMozaiksAuth()` in all services |
| **Authority URL** | `http://localhost:8080/realms/mozaiks` |

**Status:** ✅ FINISHED - All Platform services migrated  
**Owner:** Platform  
**See:** [AUTH_MIGRATION_REPORT.md](architecture/AUTH_MIGRATION_REPORT.md)

---

### 2. AI ChatUI: Core Owns ✅

| Aspect | Decision |
|--------|----------|
| **ChatPage** | Core owns |
| **ArtifactPanel** | Core owns |
| **Message handling** | Core owns |
| **Conversation modes** | Ask mode + Workflow mode |

**Status:** ✅ FINISHED - Built and working in Core  
**See:** [AI_ChatUI_Interface.md](architecture\APP_AGNOSTIC_AI_PATTERN.md)
**Owner:** mozaiks-core (`runtime/packages/shell/`)  
**Platform does NOT:** Create alternative chat UIs (other than artifacts and inline ui components during workflow creations)

---

### 3. AI Runtime: Core Owns ✅

| Aspect | Decision |
|--------|----------|
| **Workflow execution** | AG2 runtime in Core |
| **Transport** | WebSockets |
| **Workflow definitions** | YAML declaratives (created by the platform)|
| **Dynamic UI rendering** | WorkflowUIRouter in Core |

**Status:** ✅ FINISHED - Working in Core  
**Owner:** mozaiks-core (`runtime/ai/`)

---

### 4. Chat Widget: Core Owns ✅

| Aspect | Decision |
|--------|----------|
| **Widget Component** | `PersistentChatWidget.jsx` |
| **Widget Wrapper** | `GlobalChatWidgetWrapper.jsx` |
| **State Management** | `ChatUIContext` with `isInWidgetMode` |
| **Behavior** | Auto-appears on non-ChatPage routes |

**Status:** ✅ FINISHED - Built in Core  
**Owner:** mozaiks-core  
**Agent work:** ZERO - widget auto-appears

---

### 5. Plugin System: Platform Owns ✅

| Aspect | Decision |
|--------|----------|
| **Plugin Discovery** | `plugin_discovery.py` |
| **Plugin Layout** | `plugins/<name>/` with `logic.py` |
| **Plugin Generation** | AppGenerator workflow + plugin_compiler.py |

**Status:** ✅ FINISHED  
**Owner:** mozaiks-platform  
**See:** [CANONICAL_PLUGIN_LAYOUT.md](architecture/CANONICAL_PLUGIN_LAYOUT.md)

---

## 🔶 IN PROGRESS (Not Canonical Yet)

### Entitlements & Subscription Plans

| Component | Status | Notes |
|-----------|--------|-------|
| Billing Endpoints | 🔶 Code exists | `BillingController`, `BillingService` |
| Subscription Plans | 🔶 Hardcoded | 4 plans in code, NOT in DB |
| Usage Events | 🔶 Endpoint exists | `POST /api/billing/usage-events` |
| Entitlement Sync | 🔶 Code exists | `CoreSyncService` |
| Stripe Webhooks | 🔶 Handlers exist | Not tested live |

**NOT CANONICAL BECAUSE:**
- Plans are hardcoded (not DB-backed, no admin UI)
- Core↔Platform sync not tested end-to-end
- No live Stripe testing done

---

### Payment UI

| Component | Status | Notes |
|-----------|--------|-------|
| MozaikPricingPage | 🔶 Exists | `/subscriptions` route |
| WalletPage | 🔶 Exists | Basic display |
| Checkout Flow | 🔶 Partial | Stripe session created, frontend incomplete |

**NOT CANONICAL BECAUSE:**
- Checkout flow not complete end-to-end
- No successful payment test

---

### Shell Integration (Platform + Core)

| Component | Status | Notes |
|-----------|--------|-------|
| Route mounting | 🔶 Decided | Option A (Platform in Core's shell) |
| Widget on Platform pages | 🔶 Pending | Awaiting Core confirmation |

**NOT CANONICAL BECAUSE:**
- Core hasn't confirmed route mounting mechanism

---

## 📁 Key Files

### Canonical (Finished)
```
src/BuildingBlocks/Mozaiks.Auth/              # Auth building block ✅
plugins/_shared/frontend/AppRoutes.js         # Route definitions ✅
platform_runtime/plugin_discovery.py          # Plugin system ✅
```

### In Progress
```
src/Services/Payment/Payment.API/             # Billing (in progress)
plugins/_shared/frontend/pages/MozaikPricingPage.js  # Payment UI (in progress)
```

---

## ❌ DON'T DO THIS

| Anti-Pattern | Why |
|--------------|-----|
| Create Platform chat UI | Core owns ChatPage |
| Add widget code to pages | Widget auto-appears |
| Custom JWT validation | Use `AddMozaiksAuth()` |
| Run AG2 in Platform | Core runtime only |
