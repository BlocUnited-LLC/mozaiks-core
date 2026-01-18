# MozaiksCore Architectural Coherence Analysis

> **Date**: January 17, 2026  
> **Author**: Claude (Architectural Analysis)  
> **Status**: Opinionated Recommendation  
> **Purpose**: Determine if mozaiks-core can serve as a tenant-agnostic runtime

---

## Executive Summary

**Verdict**: mozaiks-core is **architecturally coherent** as a general-purpose app runtime, but it currently sits **somewhere between (a) and (b)** — a general runtime with product-specific leakage.

The vision of "Mozaiks runs on Mozaiks" is **technically valid** but requires **cleaner separation** and **explicit bootstrapping** to work correctly.

---

# 1. CORE RESPONSIBILITY CLARIFICATION

## 1.1 What mozaiks-core SHOULD be responsible for

In a modern SaaS + agentic system, a runtime core should provide:

| Capability | Description | Current State |
|------------|-------------|---------------|
| **Identity boundary** | Authenticate app users (not platform users) | ✅ Correct |
| **Plugin execution** | Load and execute business logic modules | ✅ Correct |
| **Feature gating** | Check entitlements before allowing actions | ✅ Correct |
| **Configuration serving** | Theme, navigation, settings | ✅ Correct |
| **Real-time transport** | WebSocket for notifications/updates | ✅ Correct |
| **AI session brokering** | Map capabilities → external workflows | ✅ Correct |
| **Event bus** | Internal pub/sub for decoupling | ✅ Correct |
| **Telemetry emission** | Generate events for upstream analysis | ⚠️ Partial |

## 1.2 What mozaiks-core should NOT be responsible for

| Domain | Should NOT Own | Current State |
|--------|----------------|---------------|
| **Billing/payments** | Stripe, PayPal, invoices | ✅ Correctly excluded |
| **Platform user management** | Investors, admins, founders | ✅ Correctly excluded |
| **Workflow execution** | Agent orchestration, LLM calls | ✅ Correctly excluded |
| **App provisioning** | Domain setup, SSL, deployment | ✅ Correctly excluded |
| **Cross-app analytics** | Aggregation across tenants | ✅ Correctly excluded |
| **Hosting orchestration** | Scale decisions, infra management | ⚠️ `hosting_operator.py` is ambiguous |

## 1.3 Current Position Assessment

**mozaiks-core is currently: (c) Something in between**

Evidence for "general-purpose runtime":
- Clean plugin architecture
- External auth modes (OIDC support)
- Config-driven navigation/theme
- AI brokering (not execution)
- Proper `app_id` scoping

Evidence for "product-specific backend":
- `backend/core/hosting_operator.py` — Azure-specific logic doesn't belong here
- `backend/core/config/ai_capabilities.json` — Hardcoded capability list
- `backend/app/` directory — Contains connectors/runtime that duplicate core
- Example plugins (`task_manager`, `notes_manager`) shipped in core
- `MozaiksDB` enterprise database coupling in `database.py`

**Recommendation**: Lean fully into (a) — general-purpose runtime. Remove product-specific code.

---

# 2. "MOZAIKS RUNS ON MOZAIKS" EVALUATION

## 2.1 Is This Technically Coherent?

**YES**, but with caveats.

The pattern "product X runs on its own infrastructure" is valid and common:
- GitHub runs on GitHub Actions
- Kubernetes runs on Kubernetes
- Next.js runs on Vercel (which uses Next.js)

## 2.2 What Must Be True for This to Work

For Mozaiks (the product) to run on mozaiks-core (the runtime):

### Requirement 1: Clear Bootstrap Separation

```
┌─────────────────────────────────────────────────────────────────┐
│                    BOOTSTRAP ARCHITECTURE                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────────┐                                             │
│  │ Control Plane  │  ◄─── Runs FIRST (standalone bootstrap)     │
│  │ (.NET)         │       Has its own auth, not mozaiks-core    │
│  └───────┬────────┘                                             │
│          │                                                       │
│          │ Creates & configures                                  │
│          ▼                                                       │
│  ┌────────────────┐                                             │
│  │ mozaiks-core   │  ◄─── Instance for "Mozaiks Platform App"   │
│  │ (App: Mozaiks) │       app_id = "mozaiks_platform"           │
│  └───────┬────────┘                                             │
│          │                                                       │
│          │ Also provisions                                       │
│          ▼                                                       │
│  ┌────────────────┐                                             │
│  │ mozaiks-core   │  ◄─── Instance for customer apps            │
│  │ (App: Customer)│       app_id = "customer_abc"               │
│  └────────────────┘                                             │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

**Key insight**: The Control Plane must bootstrap FIRST without depending on mozaiks-core. Then it can provision a mozaiks-core instance to host the Mozaiks product UI.

### Requirement 2: No Circular Dependencies

```python
# WRONG: Control Plane depends on mozaiks-core which depends on Control Plane
control_plane.start()  # Needs mozaiks-core running
mozaiks_core.start()   # Needs control_plane running for auth
# DEADLOCK

# CORRECT: Control Plane is self-contained for bootstrap
control_plane.start()  # Standalone auth, admin API
mozaiks_core_instance = control_plane.provision_app("mozaiks_platform")
mozaiks_core_instance.start()  # Uses control_plane for auth validation only
```

### Requirement 3: mozaiks-core Must Be Truly Generic

Current blockers to genericity:

| Blocker | Location | Resolution |
|---------|----------|------------|
| Hardcoded enterprise DB | `database.py` line 42-43 | Move to Control Plane |
| Hosting operator | `hosting_operator.py` | Move to Control Plane |
| AI capability config | `ai_capabilities.json` | Make fully dynamic |
| Example plugins | `backend/plugins/*` | Move to separate repo |

### Requirement 4: Identity Isolation

The "Mozaiks Platform" app running on mozaiks-core should have:
- Its own `app_id` ("mozaiks_platform")
- Its own user base (platform users, but treated as app users)
- Its own subscription config (platform plans, not customer plans)

**This means**: The ".NET Control Plane" is for **infrastructure** operations. The "Mozaiks Platform App" is for **user-facing** operations and could run on mozaiks-core.

## 2.3 What Would Be Flawed

The assumption becomes flawed if:

1. **mozaiks-core requires Control Plane to start** — Creates circular dependency
2. **Platform auth is hardcoded in mozaiks-core** — Breaks genericity
3. **mozaiks-core contains Mozaiks-specific business logic** — Breaks tenant isolation

**Current state**: mozaiks-core is ~80% ready. The 20% that needs fixing is identified below.

---

# 3. PLUGINS VS PLATFORM (Hard-Coded vs Extensible)

## 3.1 Decision Framework

```
┌─────────────────────────────────────────────────────────────────┐
│              PLUGIN vs PLATFORM DECISION TREE                    │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Is it required for ALL apps to function?                        │
│  │                                                               │
│  ├── YES ──► CORE (hard-coded)                                  │
│  │           Examples: Auth, WebSocket, Plugin Loading           │
│  │                                                               │
│  └── NO ───► Is it a common pattern with variations?            │
│              │                                                   │
│              ├── YES ──► CORE + EXTENSION POINT                 │
│              │           Examples: Notifications, Subscriptions  │
│              │                                                   │
│              └── NO ───► PLUGIN                                 │
│                          Examples: Task Manager, Notes           │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## 3.2 Component Classification

### CORE (Hard-Coded in Core)

| Component | Rationale | Current State |
|-----------|-----------|---------------|
| **Identity/Auth** | Every app needs user authentication | ✅ Correct |
| **Plugin Loader** | Defines the extension contract | ✅ Correct |
| **WebSocket Transport** | Universal real-time requirement | ✅ Correct |
| **Event Bus** | Universal decoupling pattern | ✅ Correct |
| **Config Serving** | Every app needs theme/nav/settings | ✅ Correct |
| **State Manager** | In-memory caching for all apps | ✅ Correct |

### CORE + EXTENSION POINT (Configurable Core)

| Component | Extension Mechanism | Current State |
|-----------|---------------------|---------------|
| **Notifications** | `notifications_config.json` + plugin handlers | ✅ Correct |
| **Navigation** | `navigation_config.json` | ✅ Correct |
| **Settings** | `settings_config.json` + plugin settings | ✅ Correct |
| **Subscriptions** | `subscription_config.json` + Control Plane sync | ✅ Correct |
| **Feature Gating** | `subscription_config.json` plugins_unlocked | ✅ Correct |
| **AI Capabilities** | `ai_capabilities.json` + capability_specs/ | ✅ Correct |

### SHOULD BE PLUGINS (Remove from Core)

| Component | Current Location | Should Be |
|-----------|------------------|-----------|
| `task_manager` | `backend/plugins/task_manager/` | Separate demo repo |
| `notes_manager` | `backend/plugins/notes_manager/` | Separate demo repo |
| `hello-world` | `backend/plugins/hello-world/` | Keep as minimal example |

### SHOULD NOT BE IN THIS REPO AT ALL

| Component | Current Location | Should Move To |
|-----------|------------------|----------------|
| `hosting_operator.py` | `backend/core/` | `control-plane` repo |
| `backend/app/` | Entire directory | Remove (duplicates core) |
| Enterprise DB coupling | `database.py` L42-43 | Make configurable |

---

# 4. DATA OWNERSHIP MAP

## 4.1 MongoDB Collections Owned by mozaiks-core

| Collection | Owner | Purpose | Scope |
|------------|-------|---------|-------|
| `users` | ✅ mozaiks-core | App user profiles | Per-app instance |
| `notifications` | ✅ mozaiks-core | User notifications | Per-app instance |
| `user_events` | ✅ mozaiks-core | Raw telemetry events | Per-app instance |
| `app_kpi_snapshots` | ✅ mozaiks-core | Daily KPI snapshots | Per-app instance |
| `subscriptions` | ✅ mozaiks-core | Local subscription state (sync from Control Plane) | Per-app instance |
| `subscription_history` | ✅ mozaiks-core | Subscription changes log | Per-app instance |
| `plugin_settings` | ✅ mozaiks-core | User plugin preferences | Per-app instance |

## 4.2 Data That Should NOT Be in mozaiks-core MongoDB

| Data | Current Location | Should Be In |
|------|------------------|--------------|
| `Enterprises` collection | `MozaiksDB` hardcoded | **control-plane** database |
| Cross-app user data | N/A | **control-plane** database |
| Platform billing records | N/A | **control-plane** database |
| App registry | N/A | **control-plane** database |

## 4.3 Complete Data Ownership Matrix

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        DATA OWNERSHIP MATRIX                             │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  MOZAIKS-CORE (Per-App Runtime)                                         │
│  ├── users              → App user profiles                             │
│  ├── notifications      → User notifications                            │
│  ├── subscriptions      → Local subscription cache                      │
│  ├── user_events        → Telemetry events                             │
│  ├── app_kpi_snapshots  → Daily metrics                                │
│  └── plugin_settings    → User preferences                             │
│                                                                          │
│  CONTROL-PLANE (.NET Backend)                                           │
│  ├── Apps               → App registry (app_id, domain, config)        │
│  ├── PlatformUsers      → Platform admins, founders                    │
│  ├── Subscriptions      → Billing state (source of truth)              │
│  ├── BillingHistory     → Payment records                              │
│  ├── Enterprises        → Organization data                            │
│  └── Deployments        → Hosting state                                │
│                                                                          │
│  MOZAIKS-AI (Execution Runtime)                                         │
│  ├── ChatSessions       → Conversation history                         │
│  ├── WorkflowState      → Execution state                              │
│  ├── Artifacts          → Generated outputs                            │
│  └── ToolExecutions     → Tool call logs                               │
│                                                                          │
│  INSIGHTS-API (Analytics Service)                                        │
│  ├── AggregatedKPIs     → Cross-app metrics                            │
│  ├── ExperimentResults  → A/B test outcomes                            │
│  └── ValidationScores   → MozaiksAI feedback                           │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

# 5. VALIDATION LOOP ROLE

## 5.1 How mozaiks-core Participates in the Validation Loop

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     VALIDATION LOOP ARCHITECTURE                         │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌────────────────┐                                                     │
│  │ MozaiksAI      │  Generates/modifies apps                            │
│  │ (Generator)    │                                                     │
│  └───────┬────────┘                                                     │
│          │                                                               │
│          ▼                                                               │
│  ┌────────────────┐                                                     │
│  │ mozaiks-core   │  Runs the generated app                             │
│  │ (Runtime)      │  ─────────────────────────────────────────────┐    │
│  └───────┬────────┘                                                │    │
│          │                                                          │    │
│          │ Emits telemetry                                         │    │
│          ▼                                                          │    │
│  ┌────────────────┐                                                │    │
│  │ Insights API   │  Aggregates signals                            │    │
│  │ (Analytics)    │                                                 │    │
│  └───────┬────────┘                                                │    │
│          │                                                          │    │
│          │ Validation signals                                       │    │
│          ▼                                                          │    │
│  ┌────────────────┐                                                │    │
│  │ Control Plane  │  Decides: keep, rollback, iterate              │    │
│  │ (Orchestrator) │◄────────────────────────────────────────────────┘   │
│  └───────┬────────┘  (receives raw events via admin API)                │
│          │                                                               │
│          │ Triggers next iteration                                       │
│          └──────────────────────────────────────────────────────────────┘
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

## 5.2 Telemetry mozaiks-core Should Emit

### Category 1: User Engagement Events

| Event Type | Trigger | Data |
|------------|---------|------|
| `auth.register` | User signup | `user_id`, `attribution`, `timestamp` |
| `auth.login` | User login | `user_id`, `session_id`, `timestamp` |
| `navigation.page_view` | Page load | `path`, `user_id`, `session_id` |
| `plugin.executed` | Plugin action | `plugin_name`, `action`, `user_id`, `duration_ms` |
| `plugin.error` | Plugin failure | `plugin_name`, `error`, `stack`, `user_id` |

### Category 2: Conversion Events

| Event Type | Trigger | Data |
|------------|---------|------|
| `subscription.trial_started` | Trial begins | `user_id`, `plan`, `attribution` |
| `subscription.upgraded` | User upgrades | `user_id`, `from_plan`, `to_plan` |
| `subscription.canceled` | User cancels | `user_id`, `plan`, `reason` |
| `ai.capability_launched` | AI feature used | `capability_id`, `user_id`, `workflow_id` |

### Category 3: Error/Health Events

| Event Type | Trigger | Data |
|------------|---------|------|
| `error.frontend` | JS error | `message`, `stack`, `route`, `user_id` |
| `error.backend` | API error | `endpoint`, `status_code`, `message` |
| `error.plugin` | Plugin crash | `plugin_name`, `error`, `user_id` |

### Category 4: Experiment Events

| Event Type | Trigger | Data |
|------------|---------|------|
| `experiment.enrolled` | User enters experiment | `experiment_id`, `variant`, `user_id` |
| `experiment.exposed` | User sees variant | `experiment_id`, `variant`, `feature` |
| `experiment.converted` | User completes goal | `experiment_id`, `variant`, `goal` |

## 5.3 Where Telemetry Should Go

```
┌────────────────────────────────────────────────────────────────┐
│                   TELEMETRY FLOW                                │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│  mozaiks-core                                                   │
│  ├── Writes to: telemetry_events (MongoDB)                     │
│  ├── Writes to: user_events (MongoDB)                          │
│  └── Exposes via: /__mozaiks/admin/telemetry/* (API)           │
│                                                                 │
│  Control Plane                                                  │
│  ├── PULLS from: mozaiks-core admin API                        │
│  ├── Aggregates across apps                                    │
│  └── Writes to: Insights API                                   │
│                                                                 │
│  Real-time (optional)                                          │
│  ├── mozaiks-core publishes critical events                    │
│  └── Control Plane webhook receives them                       │
│                                                                 │
└────────────────────────────────────────────────────────────────┘
```

---

# 6. REAL-TIME DYNAMIC BEHAVIOR ASSESSMENT

## 6.1 Is mozaiks-core sufficient for a "living, breathing app"?

**Partially yes, but missing key pieces.**

### What EXISTS for Dynamic Behavior

| Capability | Implementation | Status |
|------------|----------------|--------|
| Config hot-reload | `load_config()` with TTL cache | ✅ Works |
| Plugin refresh | `ensure_plugins_up_to_date()` | ✅ Works |
| WebSocket notifications | `websocket_manager.py` | ✅ Works |
| Event bus | `event_bus.py` | ✅ Works |
| Subscription sync | `POST /api/internal/subscription/sync` | ✅ Works |

### What's MISSING for Agent-Driven Updates

| Missing Capability | Impact | Recommendation |
|--------------------|--------|----------------|
| **Hot plugin deployment** | Can't add plugins without restart | Add plugin reload endpoint |
| **Config push from Control Plane** | Can't update theme/nav remotely | Add config sync endpoint |
| **Feature flag real-time toggle** | Experiments require restart | Add feature flag service |
| **Schema migration** | DB changes require manual intervention | Add migration runner |
| **Frontend hot update** | UI changes require redeploy | Add SSR or microfrontend support |

## 6.2 What Would Make It Fully Dynamic

```python
# PROPOSED: New admin endpoints for agent-driven updates

# 1. Hot config update (theme, navigation, settings)
POST /__mozaiks/admin/config/sync
{
  "config_type": "theme_config",
  "data": { /* new theme */ },
  "version": "2.0.1"
}

# 2. Hot plugin deployment
POST /__mozaiks/admin/plugins/deploy
{
  "plugin_name": "new_feature",
  "source": "s3://bucket/new_feature.zip",
  "version": "1.0.0"
}

# 3. Feature flag toggle
POST /__mozaiks/admin/flags/toggle
{
  "flag_name": "new_onboarding",
  "enabled": true,
  "variant": "treatment_b"
}

# 4. Schema migration
POST /__mozaiks/admin/migrations/run
{
  "migration_id": "add_user_attribution",
  "direction": "up"
}
```

## 6.3 Gap Summary

| Gap | Severity | Effort |
|-----|----------|--------|
| No hot plugin deployment | High | Medium |
| No config push API | Medium | Low |
| No feature flag service | High | Medium |
| No migration runner | Low | Low |
| No frontend hot update | Low | High |

---

# 7. RECOMMENDED FOLDER STRUCTURE

## 7.1 Current vs Proposed Structure

### Current Structure (Problematic Areas Highlighted)

```
mozaiks-core-public/
├── backend/
│   ├── main.py
│   ├── requirements.txt
│   ├── app/                    # ❌ REMOVE - duplicates core
│   │   ├── connectors/
│   │   ├── routes/
│   │   └── runtime/
│   ├── core/
│   │   ├── analytics/
│   │   ├── config/
│   │   │   ├── ai_capabilities.json
│   │   │   ├── capability_specs/   # ⚠️ Should be empty by default
│   │   │   └── ...
│   │   ├── director.py
│   │   ├── event_bus.py
│   │   ├── hosting_operator.py    # ❌ MOVE to control-plane
│   │   ├── insights/              # ⚠️ Clarify purpose
│   │   ├── metrics/
│   │   ├── notifications/
│   │   ├── notifications_manager.py
│   │   ├── ops/                   # ⚠️ Clarify purpose
│   │   ├── plugin_manager.py
│   │   ├── public_metrics/        # ⚠️ Clarify purpose
│   │   ├── routes/
│   │   ├── runtime/
│   │   ├── settings_manager.py
│   │   ├── state_manager.py
│   │   ├── subscription_manager.py
│   │   ├── subscription_stub.py
│   │   ├── telemetry/             # ⚠️ Empty or minimal
│   │   ├── websocket_manager.py
│   │   └── workflows/             # ⚠️ Should be empty (runtime owns)
│   ├── keycloak/                  # ✅ Keep (self-hosted auth)
│   ├── plugins/                   # ⚠️ Only hello-world should stay
│   │   ├── hello-world/
│   │   ├── notes_manager/         # ❌ MOVE to demo repo
│   │   └── task_manager/          # ❌ MOVE to demo repo
│   ├── security/
│   └── tests/
├── docs/
├── public/
└── src/
    ├── auth/
    ├── components/
    ├── core/
    │   ├── plugins/
    │   └── theme/
    ├── notifications/
    ├── plugins/                   # ⚠️ Only examples should stay
    │   ├── notes_manager/         # ❌ MOVE to demo repo
    │   └── task_manager/          # ❌ MOVE to demo repo
    ├── profile/
    └── subscription/
```

### Proposed Structure (Clean)

```
mozaiks-core/
├── backend/
│   ├── main.py                        # Entry point
│   ├── requirements.txt
│   │
│   ├── core/                          # ✅ CORE RUNTIME LOGIC
│   │   ├── __init__.py
│   │   ├── director.py                # FastAPI app, routing
│   │   ├── plugin_manager.py          # Plugin loading/execution
│   │   ├── event_bus.py               # Internal pub/sub
│   │   ├── state_manager.py           # In-memory state
│   │   ├── websocket_manager.py       # Real-time transport
│   │   │
│   │   ├── auth/                      # ✅ NEW: Auth module
│   │   │   ├── __init__.py
│   │   │   ├── providers.py           # OIDC, local, platform
│   │   │   └── middleware.py          # JWT validation
│   │   │
│   │   ├── subscriptions/             # ✅ Subscription logic
│   │   │   ├── __init__.py
│   │   │   ├── manager.py             # Entitlement enforcement
│   │   │   ├── stub.py                # Free-mode stub
│   │   │   └── sync.py                # Control plane sync
│   │   │
│   │   ├── notifications/             # ✅ Notification system
│   │   │   ├── __init__.py
│   │   │   ├── manager.py
│   │   │   ├── channels/
│   │   │   └── templates.py
│   │   │
│   │   ├── settings/                  # ✅ Settings system
│   │   │   ├── __init__.py
│   │   │   └── manager.py
│   │   │
│   │   ├── telemetry/                 # ✅ Telemetry (NEW)
│   │   │   ├── __init__.py
│   │   │   ├── service.py             # Event logging
│   │   │   ├── indexes.py             # MongoDB indexes
│   │   │   └── exporters.py           # Push to Control Plane
│   │   │
│   │   ├── ai/                        # ✅ AI session broker
│   │   │   ├── __init__.py
│   │   │   ├── capabilities.py        # Capability registry
│   │   │   ├── broker.py              # Session management
│   │   │   └── tokens.py              # Execution tokens
│   │   │
│   │   ├── config/                    # ✅ Configuration
│   │   │   ├── __init__.py
│   │   │   ├── database.py            # MongoDB setup
│   │   │   ├── settings.py            # Environment config
│   │   │   ├── loader.py              # JSON config loader
│   │   │   │
│   │   │   └── defaults/              # ✅ Default configs
│   │   │       ├── navigation_config.json
│   │   │       ├── settings_config.json
│   │   │       ├── subscription_config.json
│   │   │       ├── notifications_config.json
│   │   │       ├── theme_config.json
│   │   │       └── ai_capabilities.json
│   │   │
│   │   └── routes/                    # ✅ API routes
│   │       ├── __init__.py
│   │       ├── auth.py
│   │       ├── plugins.py
│   │       ├── navigation.py
│   │       ├── settings.py
│   │       ├── notifications.py
│   │       ├── subscriptions.py
│   │       ├── ai.py
│   │       ├── telemetry.py
│   │       └── admin/                 # Admin-only routes
│   │           ├── __init__.py
│   │           ├── users.py
│   │           ├── analytics.py
│   │           ├── config_sync.py     # ✅ NEW: Hot config update
│   │           └── status.py
│   │
│   ├── security/                      # ✅ Security utilities
│   │   ├── __init__.py
│   │   ├── constants.py
│   │   ├── authentication.py          # JWT validation
│   │   └── platform_jwt.py            # OIDC/Platform JWT
│   │
│   ├── plugins/                       # ✅ Plugin directory
│   │   └── _example/                  # Minimal example only
│   │       ├── __init__.py
│   │       ├── logic.py
│   │       └── README.md
│   │
│   └── tests/
│       ├── unit/
│       ├── integration/
│       └── conftest.py
│
├── frontend/                          # ✅ RENAMED from src/
│   ├── index.html
│   ├── package.json
│   ├── vite.config.js
│   │
│   ├── src/
│   │   ├── main.jsx                   # Entry point
│   │   ├── App.jsx                    # Main app
│   │   │
│   │   ├── core/                      # ✅ Core UI components
│   │   │   ├── plugins/
│   │   │   │   ├── PluginProvider.jsx
│   │   │   │   ├── DynamicUIComponent.jsx
│   │   │   │   └── usePlugins.js
│   │   │   │
│   │   │   ├── theme/
│   │   │   │   ├── ThemeProvider.jsx
│   │   │   │   └── useTheme.js
│   │   │   │
│   │   │   ├── analytics/             # ✅ NEW: Telemetry
│   │   │   │   ├── TelemetryService.js
│   │   │   │   ├── useTelemetry.js
│   │   │   │   └── AttributionCapture.jsx
│   │   │   │
│   │   │   ├── experiments/           # ✅ NEW: Feature flags
│   │   │   │   ├── FeatureFlagProvider.jsx
│   │   │   │   └── useFeatureFlags.js
│   │   │   │
│   │   │   └── errors/                # ✅ NEW: Error handling
│   │   │       └── ErrorBoundary.jsx
│   │   │
│   │   ├── auth/
│   │   │   ├── AuthContext.jsx
│   │   │   ├── LoginPage.jsx
│   │   │   └── ...
│   │   │
│   │   ├── shell/                     # ✅ RENAMED: App shell
│   │   │   ├── Navigation.jsx
│   │   │   ├── Header.jsx
│   │   │   └── Layout.jsx
│   │   │
│   │   ├── notifications/
│   │   ├── profile/
│   │   ├── subscription/
│   │   └── websockets/
│   │
│   └── plugins/                       # Plugin UI directory
│       └── _example/
│           ├── index.js
│           └── register.js
│
├── keycloak/                          # ✅ Self-hosted auth assets
│   ├── Dockerfile
│   ├── theme/
│   └── scripts/
│
├── docs/
│   ├── architecture/
│   ├── source_of_truth/
│   ├── guides/
│   │   ├── GETTING_STARTED.md
│   │   ├── PLUGIN_DEVELOPMENT.md
│   │   └── DEPLOYMENT.md
│   └── api/
│
├── scripts/                           # ✅ NEW: Utility scripts
│   ├── setup.sh
│   ├── dev.sh
│   └── build.sh
│
├── config/                            # ✅ NEW: App config (overrides defaults)
│   └── .gitkeep                       # Empty by default
│
├── .env.example
├── docker-compose.yml
├── Dockerfile
├── README.md
└── LICENSE
```

---

# 8. CONCRETE OUTPUT

## 8.1 Proposed Folder Tree

See Section 7.1 above for the complete tree.

## 8.2 Files MISSING Today

| File | Purpose | Priority |
|------|---------|----------|
| `backend/core/telemetry/service.py` | Telemetry event logging | HIGH |
| `backend/core/telemetry/indexes.py` | MongoDB index definitions | HIGH |
| `backend/core/routes/telemetry.py` | Batch telemetry endpoint | HIGH |
| `backend/core/routes/admin/config_sync.py` | Hot config update API | MEDIUM |
| `backend/core/routes/feature_flags.py` | Feature flag endpoint | MEDIUM |
| `frontend/src/core/analytics/TelemetryService.js` | Client telemetry | HIGH |
| `frontend/src/core/analytics/useTelemetry.js` | React hook | HIGH |
| `frontend/src/core/experiments/FeatureFlagProvider.jsx` | Feature flags | MEDIUM |
| `frontend/src/core/errors/ErrorBoundary.jsx` | Error boundary | MEDIUM |
| `scripts/setup.sh` | Dev setup script | LOW |
| `docker-compose.yml` | Local dev stack | LOW |

## 8.3 Files to REFACTOR or MOVE OUT

| File | Current Location | Action | Destination |
|------|------------------|--------|-------------|
| `hosting_operator.py` | `backend/core/` | **MOVE** | `control-plane` repo |
| `backend/app/` | Entire directory | **DELETE** | N/A (duplicates core) |
| `notes_manager` plugin | `backend/plugins/` | **MOVE** | `mozaiks-examples` repo |
| `task_manager` plugin | `backend/plugins/` | **MOVE** | `mozaiks-examples` repo |
| `notes_manager` UI | `frontend/plugins/` | **MOVE** | `mozaiks-examples` repo |
| `task_manager` UI | `frontend/plugins/` | **MOVE** | `mozaiks-examples` repo |
| `MozaiksDB` coupling | `database.py` L42-43 | **REFACTOR** | Make optional via env var |
| `backend/core/insights/` | Entire directory | **CLARIFY** | Document or remove |
| `backend/core/ops/` | Entire directory | **CLARIFY** | Document or remove |
| `backend/core/public_metrics/` | Entire directory | **CLARIFY** | Document or remove |
| `backend/core/workflows/` | Entire directory | **DELETE** | MozaiksAI owns workflows |

## 8.4 Configuration File Changes

| File | Change |
|------|--------|
| `ai_capabilities.json` | Empty `capabilities: []` by default |
| `capability_specs/` | Remove all example specs (keep directory) |
| `plugin_registry.json` | Only `_example` plugin by default |

## 8.5 Environment Variable Additions

```bash
# NEW: Required for validation loop
MOZAIKS_RELEASE_ID=               # MozaiksAI release version
MOZAIKS_INSIGHTS_API_URL=         # Where to push aggregated telemetry
MOZAIKS_CONTROL_PLANE_WEBHOOK_URL= # Real-time critical events

# NEW: Optional feature flags
MOZAIKS_FEATURE_FLAGS_ENABLED=false
MOZAIKS_FEATURE_FLAGS_REFRESH_S=300

# EXISTING: Clarify documentation
MOZAIKS_APP_ID=                   # Required in production
MONETIZATION=0                    # 0=free, 1=enforced
DATABASE_URI=                     # MongoDB connection
```

---

## Summary Recommendations

1. **Accept the "Mozaiks on Mozaiks" vision** — It's technically sound if bootstrap is handled correctly.

2. **Remove product-specific code** — Move `hosting_operator.py`, example plugins, and `backend/app/` out.

3. **Add telemetry infrastructure** — Critical for validation loop support.

4. **Add feature flag system** — Required for dynamic experiments.

5. **Clarify data boundaries** — mozaiks-core owns app data; Control Plane owns platform data.

6. **Keep extension points** — Subscriptions, notifications, and settings should remain configurable core features (not plugins).

7. **Document the bootstrap pattern** — Control Plane starts first; mozaiks-core instances are provisioned.

---

**mozaiks-core is 80% of the way to being a true tenant-agnostic runtime. The 20% gap is:**
- Telemetry system (not built)
- Feature flags (not built)
- Product-specific code (needs removal)
- Documentation (needs clarity)

This is achievable in 2-3 focused sprints.
