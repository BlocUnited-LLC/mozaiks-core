# Complete Multi-Repo Migration Analysis

> **Purpose:** Map components from ALL source repos to proper locations in the final split architecture.
> **Date:** 2026-01-20
> **Status:** ✅ MIGRATION EXECUTED - 2026-01-20

## Execution Summary

| Phase | Description | Status |
|-------|-------------|--------|
| 1 | Create mozaiks-core directories | ✅ Complete |
| 2 | Create mozaiks-platform directories | ✅ Complete |
| 3 | Copy mozaiks-core-public → mozaiks-core | ✅ Complete |
| 4 | Copy MozaiksAI (open source) → mozaiks-core | ✅ Complete |
| 5 | Copy MozaiksAI (proprietary) → mozaiks-platform | ✅ Complete |
| 6 | Convert MOZ-UI modules → plugins | ✅ Complete |

### Files Migrated
- **mozaiks-core**: Runtime shell, AI core, ChatUI (thousands of files)
- **mozaiks-platform**: 13 plugins from MOZ-UI, AI models/workflows

---

## Visual Summary

```
┌─────────────────────────────────────────────────────────────────────────────────────────────┐
│                              SOURCE REPOS (TO BE CONSOLIDATED)                               │
├─────────────────────────────────────────────────────────────────────────────────────────────┤
│                                                                                             │
│  ┌─────────────────────┐  ┌─────────────────────┐  ┌─────────────────────┐                  │
│  │   control-plane     │  │ mozaiks-core-public │  │     MozaiksAI       │                  │
│  │   (.NET monolith)   │  │ (React + Python)    │  │   (AI + ChatUI)     │                  │
│  │                     │  │                     │  │                     │                  │
│  │ • AuthServer.Api    │  │ • src/ (React)      │  │ • mozaiksai/core/   │                  │
│  │ • User.API          │  │ • backend/ (Python) │  │ • ChatUI/           │                  │
│  │ • Payment.API       │  │ • keycloak/         │  │ • workflows/        │                  │
│  │ • Insights.API      │  │ • docs/             │  │   (proprietary)     │                  │
│  │ • Notification.API  │  │                     │  │ • tests/            │                  │
│  │ • Governance.API    │  │                     │  │                     │                  │
│  │ • Admin.API         │  │                     │  │                     │                  │
│  │ • App.API           │  │                     │  │                     │                  │
│  │ • Hosting.API       │  │                     │  │                     │                  │
│  │ • CommunicationSvc  │  │                     │  │                     │                  │
│  └─────────────────────┘  └─────────────────────┘  └─────────────────────┘                  │
│                                                                                             │
│  ┌─────────────────────┐  ┌─────────────────────┐                                           │
│  │      MOZ-UI         │  │    mozaiks-app      │                                           │
│  │  (Platform React)   │  │  (App template)     │                                           │
│  │                     │  │                     │                                           │
│  │ • modules/Admin     │  │ • backend/plugins   │ ← KEEP as reference                       │
│  │ • modules/Funding   │  │ • backend/workflows │                                           │
│  │ • modules/Community │  │ • frontend/plugins  │                                           │
│  │ • modules/app       │  │                     │                                           │
│  │ • shared/services   │  │                     │                                           │
│  └─────────────────────┘  └─────────────────────┘                                           │
│                                                                                             │
└─────────────────────────────────────────────────────────────────────────────────────────────┘
                                          │
                                          │ MIGRATION
                                          ▼
┌─────────────────────────────────────────────────────────────────────────────────────────────┐
│                                  TARGET REPOS (FINAL STATE)                                  │
├──────────────────────────────────────────┬──────────────────────────────────────────────────┤
│                                          │                                                  │
│         mozaiks-core (Open Source)       │       mozaiks-platform (Proprietary)             │
│                                          │                                                  │
│  ┌────────────────────────────────────┐  │  ┌────────────────────────────────────────────┐  │
│  │ backend/ (.NET Core APIs)          │  │  │ src/Services/ (Platform APIs)              │  │
│  │ ├── Identity.API (Auth+User)       │  │  │ ├── Hosting.API                            │  │
│  │ ├── Billing.API (Payment)          │  │  │ ├── Funding/Funding.API                    │  │
│  │ ├── Insights.API                   │  │  │ ├── Growth.API/AdEngine.API                │  │
│  │ ├── Notification.API               │  │  │ ├── Growth.API/GrowthEngine.API            │  │
│  │ ├── Plugins.API (NEW)              │  │  │ ├── Discovery/Admin.API                    │  │
│  │ └── Gateway                        │  │  │ ├── Community/CommunicationService.API     │  │
│  └────────────────────────────────────┘  │  │ └── Social/App.API                         │  │
│                                          │  └────────────────────────────────────────────┘  │
│  ┌────────────────────────────────────┐  │                                                  │
│  │ runtime/ (from mozaiks-core-public │  │  ┌────────────────────────────────────────────┐  │
│  │          + MozaiksAI open parts)   │  │  │ ai-models/ (Proprietary AI)                │  │
│  │ ├── packages/shell/ (React UI)     │  │  │ ├── workflows/AgentGenerator               │  │
│  │ ├── packages/sdk/                  │  │  │ ├── workflows/AppGenerator                 │  │
│  │ ├── backend/ (Python FastAPI)      │  │  │ ├── workflows/DesignDocs                   │  │
│  │ └── ai/                            │  │  │ ├── workflows/SubscriptionAdvisor          │  │
│  │     ├── packages/core/             │  │  │ ├── workflows/ValueEngine                  │  │
│  │     ├── packages/ui/ (ChatUI)      │  │  │ └── packs/                                 │  │
│  │     └── packages/tools/            │  │  └────────────────────────────────────────────┘  │
│  └────────────────────────────────────┘  │                                                  │
│                                          │  ┌────────────────────────────────────────────┐  │
│  ┌────────────────────────────────────┐  │  │ plugins/ (from MOZ-UI)                     │  │
│  │ plugins/ (Core OSS plugins)        │  │  │ ├── moz.platform.dashboard                 │  │
│  │ ├── moz.core.profile               │  │  │ ├── moz.platform.discovery                 │  │
│  │ ├── moz.core.settings              │  │  │ ├── moz.platform.funding                   │  │
│  │ └── moz.core.notifications         │  │  │ ├── moz.platform.hosting                   │  │
│  └────────────────────────────────────┘  │  │ ├── moz.platform.growth                    │  │
│                                          │  │ ├── moz.platform.community                 │  │
│  ┌────────────────────────────────────┐  │  │ ├── moz.platform.admin                     │  │
│  │ docs/, examples/, docker/          │  │  │ ├── moz.platform.teams                     │  │
│  │ LICENSE: MIT                       │  │  │ ├── moz.platform.governance                │  │
│  └────────────────────────────────────┘  │  │ ├── moz.platform.monetization              │  │
│                                          │  │ ├── moz.platform.ai                        │  │
│                                          │  │ └── moz.platform.messaging                 │  │
│                                          │  └────────────────────────────────────────────┘  │
│                                          │                                                  │
└──────────────────────────────────────────┴──────────────────────────────────────────────────┘
```

---

## Executive Summary

This document analyzes **5 source repositories** and maps them to the **2 target repositories**:

### Source Repos (TO BE CONSOLIDATED)
| Repo | Purpose | Status |
|------|---------|--------|
| **control-plane** | Legacy .NET monolith (all services) | → Split into mozaiks-core + mozaiks-platform |
| **mozaiks-core-public** | React frontend + Python runtime | → mozaiks-core/runtime |
| **MozaiksAI** | AI runtime + ChatUI + Workflows | → mozaiks-core/runtime/ai + mozaiks-platform/ai-models |
| **MOZ-UI** | Mozaiks Platform React UI | → mozaiks-platform/plugins |
| **mozaiks-app** | App template (plugins/workflows) | → Reference implementation |

### Target Repos (FINAL STATE)
| Repo | Contains | License |
|------|----------|---------|
| **mozaiks-core** | Runtime, AI engine, core backend APIs | **Open Source (MIT)** |
| **mozaiks-platform** | Platform services, plugins, trained models | **Proprietary** |

### High-Level Mapping

| Source | Content Type | Target |
|--------|--------------|--------|
| **control-plane** | Identity/User/Auth APIs | `mozaiks-core/backend/Identity.API/` |
| **control-plane** | Payment/Billing APIs | `mozaiks-core/backend/Billing.API/` |
| **control-plane** | Insights/Analytics APIs | `mozaiks-core/backend/Insights.API/` |
| **control-plane** | Notification APIs | `mozaiks-core/backend/Notification.API/` |
| **control-plane** | Hosting/Funding/Growth/etc | `mozaiks-platform/src/Services/` |
| **mozaiks-core-public** | React shell + Python backend | `mozaiks-core/runtime/` |
| **MozaiksAI** | AI core engine | `mozaiks-core/runtime/ai/packages/core/` |
| **MozaiksAI** | ChatUI React | `mozaiks-core/runtime/ai/packages/ui/` |
| **MozaiksAI** | Trained workflows | `mozaiks-platform/ai-models/workflows/` |
| **MOZ-UI** | Platform dashboard | `mozaiks-platform/plugins/moz.platform.dashboard/` |
| **MOZ-UI** | Funding pages | `mozaiks-platform/plugins/moz.platform.funding/` |
| **MOZ-UI** | Admin pages | `mozaiks-platform/plugins/moz.platform.admin/` |
| **MOZ-UI** | Discovery pages | `mozaiks-platform/plugins/moz.platform.discovery/` |
| **MOZ-UI** | Community pages | `mozaiks-platform/plugins/moz.platform.community/` |
| **MOZ-UI** | App management | `mozaiks-platform/plugins/moz.platform.app/` |
| **MOZ-UI** | Shared services | `mozaiks-platform/plugins/_shared/` |

---

## 1. Current State Analysis

### 1.1 mozaiks-core-public Structure

```
mozaiks-core-public/
├── backend/                    # Python FastAPI backend
│   ├── core/                   # Core modules
│   │   ├── ai_bridge/          # AI integration layer
│   │   ├── ai_runtime/         # Local AI runtime
│   │   ├── analytics/          # Analytics tracking
│   │   ├── config/             # Configuration
│   │   ├── events/             # Event system
│   │   ├── http/               # HTTP utilities
│   │   ├── insights/           # Insights integration
│   │   ├── metrics/            # Metrics collection
│   │   ├── notifications/      # Push notifications
│   │   ├── ops/                # Operations
│   │   ├── public_metrics/     # Public metrics
│   │   ├── routes/             # API routes
│   │   ├── runtime/            # Runtime utilities
│   │   └── telemetry/          # Telemetry
│   ├── app/                    # Application layer
│   │   ├── connectors/         # External connectors
│   │   ├── routes/             # Route definitions
│   │   └── runtime/            # Runtime modules
│   ├── keycloak/               # Keycloak integration
│   └── plugins/                # Plugin system
│
├── src/                        # React frontend
│   ├── ai/                     # AI components
│   ├── auth/                   # Authentication
│   ├── chat/                   # Chat components
│   ├── components/             # Shared components
│   ├── core/                   # Core modules
│   ├── notifications/          # Notification UI
│   ├── plugins_stub/           # Plugin stubs
│   ├── profile/                # User profile
│   ├── subscription/           # Subscription UI
│   ├── websockets/             # WebSocket handling
│   └── workflows_stub/         # Workflow stubs
│
├── docs/                       # Documentation
│   ├── architecture/
│   └── source_of_truth/
│
└── public/                     # Static assets
```

### 1.2 MozaiksAI Structure

```
MozaiksAI/
├── mozaiksai/                  # Python AI runtime package
│   ├── core/
│   │   ├── artifacts/          # Artifact handling
│   │   ├── auth/               # AI auth
│   │   ├── capabilities/       # AI capabilities
│   │   ├── data/               # Data management
│   │   ├── events/             # Event system
│   │   ├── multitenant/        # Multi-tenancy
│   │   ├── observability/      # Observability
│   │   ├── runtime/            # Runtime core
│   │   ├── tokens/             # Token management
│   │   ├── transport/          # Transport layer
│   │   └── workflow/           # Workflow execution
│   └── __init__.py
│
├── ChatUI/                     # React chat interface
│   ├── src/
│   │   ├── adapters/           # Backend adapters
│   │   ├── components/         # UI components
│   │   ├── config/             # Configuration
│   │   ├── context/            # React contexts
│   │   ├── core/               # Core modules
│   │   ├── hooks/              # React hooks
│   │   ├── pages/              # Page components
│   │   ├── services/           # Service layer
│   │   ├── styles/             # Styling
│   │   ├── utils/              # Utilities
│   │   └── workflows/          # Workflow UI
│   └── public/
│
├── workflows/                  # Trained workflow packs (PROPRIETARY)
│   ├── _examples/              # Example workflows
│   ├── _pack/                  # Core packs
│   ├── _shared/                # Shared utilities
│   ├── AgentGenerator/         # Agent generation workflow
│   ├── AppGenerator/           # App generation workflow
│   ├── DesignDocs/             # Design docs workflow
│   ├── SubscriptionAdvisor/    # Subscription workflow
│   └── ValueEngine/            # Value engine workflow
│
├── docs/                       # Documentation
├── infra/                      # Infrastructure configs
│   ├── compose/
│   └── docker/
│
├── tests/                      # Test suite
└── scripts/                    # Utility scripts
```

### 1.3 Current mozaiks-core Structure

```
mozaiks-core/                   # Partially migrated
├── backend/                    # .NET Backend APIs
│   ├── Billing.API/            # Payment/Billing
│   ├── Identity.API/           # Auth + User management
│   ├── Insights.API/           # Analytics/KPIs
│   ├── Notification.API/       # Email/Push notifications
│   ├── User.API/               # User profiles (legacy?)
│   ├── src/                    # Source code
│   ├── AspireAdmin/            # Admin dashboard
│   ├── BuildingBlocks/         # Shared libraries
│   └── Shared/                 # Shared models
│
├── runtime/                    # Runtime (needs Python/Node content)
│   ├── ai/                     # AI runtime (empty?)
│   ├── packages/               # Package structure (empty?)
│   └── (Python backend content from mozaiks-core-public?)
│
└── (other root files)
```

### 1.4 MOZ-UI Structure (Platform UI)

```
MOZ-UI/
├── src/
│   ├── modules/                    # Feature modules (become plugins)
│   │   ├── Admin/                  # Platform administration
│   │   │   └── pages/
│   │   │       ├── AdminDashboardPage.js
│   │   │       ├── AdminUsersPage.js
│   │   │       ├── AdminAppsPage.js
│   │   │       ├── AdminApprovalsPage.js
│   │   │       ├── AdminAuditLogPage.js
│   │   │       ├── AdminDiscoveryPage.js
│   │   │       ├── AdminFundingPage.js
│   │   │       ├── AdminGovernancePage.js
│   │   │       ├── AdminInventoryPage.js
│   │   │       ├── AdminSettingsPage.js
│   │   │       ├── AdminStripeHealthPage.js
│   │   │       └── AdminSubscriptionsPage.js
│   │   │
│   │   ├── Funding/                # Investment & funding
│   │   │   └── pages/
│   │   │       ├── CreateFundingRoundPage.js
│   │   │       ├── DiscoveryMarketplacePage.js
│   │   │       ├── FundingDashboardPage.js
│   │   │       ├── InvestorDashboardPage.js
│   │   │       └── PublicRoundPage.js
│   │   │
│   │   ├── Community/              # Social/community features
│   │   │   ├── pages/
│   │   │   │   ├── CommunityDashboardPage.js
│   │   │   │   ├── CommunityPage.js
│   │   │   │   ├── CreateCommunityPage.js
│   │   │   │   └── DashboardPage.js
│   │   │   ├── components/
│   │   │   ├── context/
│   │   │   └── services/
│   │   │
│   │   └── app/                    # App management (50+ pages!)
│   │       ├── pages/
│   │       │   ├── DashboardPage.js
│   │       │   ├── AppProfile.js
│   │       │   ├── CreateApp.js
│   │       │   ├── AppHealthDashboardPage.js
│   │       │   ├── AppPermissionsPage.js
│   │       │   ├── AppUpdatePage.js
│   │       │   ├── TeamsPage.js
│   │       │   ├── EditTeamMemberPage.js
│   │       │   ├── GovernancePage.js
│   │       │   ├── TreasuryPage.js
│   │       │   ├── MonetizationDashboardPage.js
│   │       │   ├── MozaikPricingPage.js
│   │       │   ├── MessagesPage.js
│   │       │   ├── ContentCalendarPage.js
│   │       │   ├── GrowthPage.js
│   │       │   ├── SocialMediaManagerPage.js
│   │       │   ├── ManagedHostingAppsPage.js
│   │       │   ├── ManagedHostingAppDetailPage.js
│   │       │   ├── CommandCenterPage.js
│   │       │   ├── AutoDevPage.js
│   │       │   ├── DesignDocumentPage.js
│   │       │   ├── ConceptVerificationChatPage.js
│   │       │   └── ... (30+ more pages)
│   │       ├── Components/
│   │       ├── context/
│   │       ├── services/
│   │       └── utils/
│   │
│   ├── shared/                     # Shared utilities & components
│   │   ├── components/             # Reusable UI components
│   │   ├── context/                # React contexts
│   │   ├── hooks/                  # Custom hooks
│   │   ├── models/                 # TypeScript models
│   │   ├── pages/                  # Shared pages
│   │   ├── payments/               # Payment components
│   │   ├── registry/               # Registry utilities
│   │   ├── services/               # API services
│   │   │   ├── AdminService.js
│   │   │   ├── DiscoveryService.js
│   │   │   ├── FundingService.js
│   │   │   ├── HostingService.js
│   │   │   ├── InsightsService.js
│   │   │   ├── InvestmentService.js
│   │   │   ├── MozaiksPayService.js
│   │   │   ├── PaymentService.js
│   │   │   ├── SocialService.js
│   │   │   ├── SubscriptionService.js
│   │   │   ├── mozaiksAIService.js
│   │   │   ├── directMessageService.js
│   │   │   ├── governanceChatService.js
│   │   │   └── userService.js
│   │   └── utils/                  # Utility functions
│   │
│   └── auth/                       # Authentication
│
├── auth-service/                   # Local auth service (dev)
├── docs/                           # Documentation
├── public/                         # Static assets
└── scripts/                        # Build/dev scripts
```

### 1.5 mozaiks-platform Structure (Current)

```
mozaiks-platform/
├── src/
│   ├── ApiGateways/
│   │   └── OcelotApiGateway/       # API routing
│   │
│   └── Services/
│       ├── Hosting.API/            # App hosting & deployment
│       │   └── Hosting.API/
│       │
│       ├── Funding/
│       │   └── Funding.API/        # Was Governance.API
│       │
│       ├── Growth.API/
│       │   ├── AdEngine.API/       # Ad campaigns (was ValidationEngine)
│       │   └── GrowthEngine.API/   # Campaign orchestration
│       │
│       ├── Discovery/
│       │   └── Admin.API/          # Discovery + admin aggregation
│       │
│       ├── Community/
│       │   └── CommunicationService.API/  # Messaging
│       │
│       └── Social/
│           └── App.API/            # App social feed
│
├── provisioning-agent/             # Deployment agent
└── docker-compose.yml
```

### 1.6 mozaiks-app Structure (App Template)

```
mozaiks-app/
├── backend/
│   ├── plugins/                    # Backend plugins for apps
│   ├── workflows/                  # Workflow definitions
│   └── core/
│       └── config/                 # App-level configs
│
└── frontend/
    └── src/
        ├── plugins/                # Frontend plugins
        └── chat/
            └── workflows/          # Workflow-specific chat UI
```

---

## 2. Complete Migration Mapping

### 2.1 control-plane → mozaiks-core (Backend APIs)

| Source Path | Target Path | Notes |
|------------|-------------|-------|
| `Services/AuthServer/AuthServer.Api/` | `backend/src/Identity.API/` | Combined with User |
| `Services/User/User.API/` | `backend/src/Identity.API/` | Merged into Identity |
| `Services/Payment/Payment.API/` | `backend/src/Billing.API/` | Renamed |
| `Services/Insights/Insights.API/` | `backend/src/Insights.API/` | Direct move |
| `Services/Notification/Notification.API/` | `backend/src/Notification.API/` | Direct move |
| `BuildingBlocks/` | `backend/BuildingBlocks/` | Shared libraries |

### 2.2 control-plane → mozaiks-platform (Platform Services)

| Source Path | Target Path | Notes |
|------------|-------------|-------|
| `Services/Governance/Governance.API/` | `src/Services/Funding/Funding.API/` | Renamed |
| `Services/Admin/Admin.API/` | `src/Services/Discovery/Admin.API/` | Discovery aggregator |
| `Services/CommunicationService/` | `src/Services/Community/CommunicationService.API/` | Direct move |
| `Services/App/App.API/` | `src/Services/Social/App.API/` | Social feed |
| `Services/AdEngine/AdEngine.API/` | `src/Services/Growth.API/AdEngine.API/` | Was ValidationEngine |
| `Services/GrowthEngine/GrowthEngine.API/` | `src/Services/Growth.API/GrowthEngine.API/` | Campaign orchestration |
| `Services/Hosting/Hosting.API/` | `src/Services/Hosting.API/Hosting.API/` | Direct move |

### 2.3 mozaiks-core-public → mozaiks-core (Runtime)

| Source Path | Target Path | Notes |
|------------|-------------|-------|
| `backend/` | `runtime/backend/` | Python FastAPI server |
| `backend/core/ai_bridge/` | `runtime/ai/packages/bridge/` | AI bridge layer |
| `backend/core/ai_runtime/` | `runtime/ai/packages/local/` | Local AI runtime |
| `backend/core/runtime/` | `runtime/backend/core/runtime/` | Keep in Python backend |
| `backend/plugins/` | `runtime/backend/plugins/` | Plugin system |
| `backend/keycloak/` | `runtime/backend/keycloak/` | Keycloak integration |
| `src/` | `runtime/packages/shell/src/` | React UI shell |
| `src/ai/` | `runtime/ai/packages/ui/src/` | AI UI components |
| `src/chat/` | `runtime/ai/packages/ui/src/chat/` | Chat components |
| `src/auth/` | `runtime/packages/shell/src/auth/` | Auth components |
| `src/plugins_stub/` | `runtime/packages/shell/src/plugins/` | Plugin UI |
| `docs/` | `docs/runtime/` | Runtime documentation |
| `public/` | `runtime/packages/shell/public/` | Static assets |

### 2.4 MozaiksAI → mozaiks-core (Open Source Parts)

| Source Path | Target Path | Notes |
|------------|-------------|-------|
| `mozaiksai/core/` | `runtime/ai/packages/core/` | AI orchestration engine |
| `mozaiksai/core/artifacts/` | `runtime/ai/packages/core/artifacts/` | Artifact handling |
| `mozaiksai/core/events/` | `runtime/ai/packages/core/events/` | Event system |
| `mozaiksai/core/runtime/` | `runtime/ai/packages/core/runtime/` | Runtime core |
| `mozaiksai/core/transport/` | `runtime/ai/packages/core/transport/` | Transport layer |
| `mozaiksai/core/workflow/` | `runtime/ai/packages/core/workflow/` | Workflow execution |
| `mozaiksai/core/tokens/` | `runtime/ai/packages/core/tokens/` | Token management |
| `mozaiksai/core/observability/` | `runtime/ai/packages/core/observability/` | Observability |
| `ChatUI/src/` | `runtime/ai/packages/ui/src/` | Chat UI components |
| `docs/` | `docs/ai/` | AI documentation |
| `tests/` | `runtime/ai/tests/` | AI tests |

### 2.5 MozaiksAI → mozaiks-platform (Proprietary Parts)

| Source Path | Target Path | Notes |
|------------|-------------|-------|
| `workflows/AgentGenerator/` | `ai-models/workflows/AgentGenerator/` | **PROPRIETARY** |
| `workflows/AppGenerator/` | `ai-models/workflows/AppGenerator/` | **PROPRIETARY** |
| `workflows/DesignDocs/` | `ai-models/workflows/DesignDocs/` | **PROPRIETARY** |
| `workflows/SubscriptionAdvisor/` | `ai-models/workflows/SubscriptionAdvisor/` | **PROPRIETARY** |
| `workflows/ValueEngine/` | `ai-models/workflows/ValueEngine/` | **PROPRIETARY** |
| `workflows/_pack/` | `ai-models/packs/` | Curated packs **PROPRIETARY** |
| `workflows/_examples/` | `examples/workflows/` | Could be open source |
| `workflows/_shared/` | `ai-models/shared/` | Shared workflow utils |

### 2.6 MOZ-UI → mozaiks-platform (Platform Plugins)

MOZ-UI becomes a collection of **platform plugins** that load into the mozaiks-core shell.

#### Admin Module → `moz.platform.admin`
| Source Path | Target Path |
|------------|-------------|
| `modules/Admin/pages/AdminDashboardPage.js` | `plugins/moz.platform.admin/frontend/pages/DashboardPage.tsx` |
| `modules/Admin/pages/AdminUsersPage.js` | `plugins/moz.platform.admin/frontend/pages/UsersPage.tsx` |
| `modules/Admin/pages/AdminAppsPage.js` | `plugins/moz.platform.admin/frontend/pages/AppsPage.tsx` |
| `modules/Admin/pages/AdminApprovalsPage.js` | `plugins/moz.platform.admin/frontend/pages/ApprovalsPage.tsx` |
| `modules/Admin/pages/AdminAuditLogPage.js` | `plugins/moz.platform.admin/frontend/pages/AuditLogPage.tsx` |
| `modules/Admin/pages/AdminDiscoveryPage.js` | `plugins/moz.platform.admin/frontend/pages/DiscoveryPage.tsx` |
| `modules/Admin/pages/AdminFundingPage.js` | `plugins/moz.platform.admin/frontend/pages/FundingPage.tsx` |
| `modules/Admin/pages/AdminGovernancePage.js` | `plugins/moz.platform.admin/frontend/pages/GovernancePage.tsx` |
| `modules/Admin/pages/AdminStripeHealthPage.js` | `plugins/moz.platform.admin/frontend/pages/StripeHealthPage.tsx` |
| `modules/Admin/pages/AdminSubscriptionsPage.js` | `plugins/moz.platform.admin/frontend/pages/SubscriptionsPage.tsx` |

#### Funding Module → `moz.platform.funding`
| Source Path | Target Path |
|------------|-------------|
| `modules/Funding/pages/FundingDashboardPage.js` | `plugins/moz.platform.funding/frontend/pages/DashboardPage.tsx` |
| `modules/Funding/pages/CreateFundingRoundPage.js` | `plugins/moz.platform.funding/frontend/pages/CreateRoundPage.tsx` |
| `modules/Funding/pages/DiscoveryMarketplacePage.js` | `plugins/moz.platform.discovery/frontend/pages/MarketplacePage.tsx` |
| `modules/Funding/pages/InvestorDashboardPage.js` | `plugins/moz.platform.funding/frontend/pages/InvestorPage.tsx` |
| `modules/Funding/pages/PublicRoundPage.js` | `plugins/moz.platform.funding/frontend/pages/RoundDetailPage.tsx` |

#### Community Module → `moz.platform.community`
| Source Path | Target Path |
|------------|-------------|
| `modules/Community/pages/CommunityDashboardPage.js` | `plugins/moz.platform.community/frontend/pages/DashboardPage.tsx` |
| `modules/Community/pages/CommunityPage.js` | `plugins/moz.platform.community/frontend/pages/FeedPage.tsx` |
| `modules/Community/pages/CreateCommunityPage.js` | `plugins/moz.platform.community/frontend/pages/CreatePage.tsx` |
| `modules/Community/components/` | `plugins/moz.platform.community/frontend/components/` |
| `modules/Community/services/` | `plugins/moz.platform.community/frontend/services/` |

#### App Module → Multiple Plugins

The large `app` module gets split across multiple plugins:

**`moz.platform.dashboard`** (Main app dashboard)
| Source Path | Target Path |
|------------|-------------|
| `modules/app/pages/DashboardPage.js` | `plugins/moz.platform.dashboard/frontend/pages/DashboardPage.tsx` |
| `modules/app/pages/ModernDashboardPage.js` | `plugins/moz.platform.dashboard/frontend/pages/ModernDashboardPage.tsx` |
| `modules/app/pages/AppProfile.js` | `plugins/moz.platform.dashboard/frontend/pages/AppProfilePage.tsx` |
| `modules/app/pages/CreateApp.js` | `plugins/moz.platform.dashboard/frontend/pages/CreateAppPage.tsx` |
| `modules/app/pages/AppUpdatePage.js` | `plugins/moz.platform.dashboard/frontend/pages/UpdateAppPage.tsx` |

**`moz.platform.teams`** (Team management)
| Source Path | Target Path |
|------------|-------------|
| `modules/app/pages/TeamsPage.js` | `plugins/moz.platform.teams/frontend/pages/TeamsPage.tsx` |
| `modules/app/pages/EditTeamMemberPage.js` | `plugins/moz.platform.teams/frontend/pages/EditMemberPage.tsx` |
| `modules/app/pages/InvitationPage.js` | `plugins/moz.platform.teams/frontend/pages/InvitationPage.tsx` |

**`moz.platform.governance`** (Governance & voting)
| Source Path | Target Path |
|------------|-------------|
| `modules/app/pages/GovernancePage.js` | `plugins/moz.platform.governance/frontend/pages/GovernancePage.tsx` |
| `modules/app/pages/TreasuryPage.js` | `plugins/moz.platform.governance/frontend/pages/TreasuryPage.tsx` |
| `modules/app/pages/CommandCenterPage.js` | `plugins/moz.platform.governance/frontend/pages/CommandCenterPage.tsx` |

**`moz.platform.monetization`** (Payments & pricing)
| Source Path | Target Path |
|------------|-------------|
| `modules/app/pages/MonetizationDashboardPage.js` | `plugins/moz.platform.monetization/frontend/pages/DashboardPage.tsx` |
| `modules/app/pages/MozaikPricingPage.js` | `plugins/moz.platform.monetization/frontend/pages/PricingPage.tsx` |
| `modules/app/pages/WalletPage.js` | `plugins/moz.platform.monetization/frontend/pages/WalletPage.tsx` |

**`moz.platform.hosting`** (Hosting & deployment)
| Source Path | Target Path |
|------------|-------------|
| `modules/app/pages/ManagedHostingAppsPage.js` | `plugins/moz.platform.hosting/frontend/pages/AppsPage.tsx` |
| `modules/app/pages/ManagedHostingAppDetailPage.js` | `plugins/moz.platform.hosting/frontend/pages/AppDetailPage.tsx` |

**`moz.platform.growth`** (Marketing & growth)
| Source Path | Target Path |
|------------|-------------|
| `modules/app/pages/GrowthPage.js` | `plugins/moz.platform.growth/frontend/pages/GrowthPage.tsx` |
| `modules/app/pages/ContentCalendarPage.js` | `plugins/moz.platform.growth/frontend/pages/ContentCalendarPage.tsx` |
| `modules/app/pages/SocialMediaManagerPage.js` | `plugins/moz.platform.growth/frontend/pages/SocialMediaPage.tsx` |
| `modules/app/pages/MarketingContentPage.js` | `plugins/moz.platform.growth/frontend/pages/ContentPage.tsx` |

**`moz.platform.ai`** (AI wizard integration)
| Source Path | Target Path |
|------------|-------------|
| `modules/app/pages/AutoDevPage.js` | `plugins/moz.platform.ai/frontend/pages/AutoDevPage.tsx` |
| `modules/app/pages/DesignDocumentPage.js` | `plugins/moz.platform.ai/frontend/pages/DesignDocPage.tsx` |
| `modules/app/pages/ConceptVerificationChatPage.js` | `plugins/moz.platform.ai/frontend/pages/ConceptChatPage.tsx` |
| `modules/app/pages/CodingJourneyInitPage.js` | `plugins/moz.platform.ai/frontend/pages/JourneyInitPage.tsx` |
| `modules/app/pages/CodingJourneySelectionPage.js` | `plugins/moz.platform.ai/frontend/pages/JourneySelectPage.tsx` |

**`moz.platform.messaging`** (Direct messaging)
| Source Path | Target Path |
|------------|-------------|
| `modules/app/pages/MessagesPage.js` | `plugins/moz.platform.messaging/frontend/pages/MessagesPage.tsx` |
| `modules/app/pages/ModernMessagesPage.js` | `plugins/moz.platform.messaging/frontend/pages/ModernMessagesPage.tsx` |

#### Shared Module → `_shared` utilities
| Source Path | Target Path |
|------------|-------------|
| `shared/components/` | `plugins/_shared/components/` |
| `shared/context/` | `plugins/_shared/context/` |
| `shared/hooks/` | `plugins/_shared/hooks/` |
| `shared/models/` | `plugins/_shared/models/` |
| `shared/utils/` | `plugins/_shared/utils/` |
| `shared/services/AdminService.js` | `plugins/moz.platform.admin/frontend/services/AdminService.ts` |
| `shared/services/DiscoveryService.js` | `plugins/moz.platform.discovery/frontend/services/DiscoveryService.ts` |
| `shared/services/FundingService.js` | `plugins/moz.platform.funding/frontend/services/FundingService.ts` |
| `shared/services/HostingService.js` | `plugins/moz.platform.hosting/frontend/services/HostingService.ts` |
| `shared/services/InsightsService.js` | `plugins/moz.platform.dashboard/frontend/services/InsightsService.ts` |
| `shared/services/MozaiksPayService.js` | `plugins/moz.platform.monetization/frontend/services/PayService.ts` |
| `shared/services/SocialService.js` | `plugins/moz.platform.community/frontend/services/SocialService.ts` |
| `shared/services/mozaiksAIService.js` | `plugins/moz.platform.ai/frontend/services/AIService.ts` |

---

## 3. Final Target Repository Structures

### 3.1 mozaiks-core (Open Source - FINAL)

```
mozaiks-core/
├── .github/
│   └── workflows/                      # CI/CD
│
├── backend/                            # .NET Control Plane Core
│   ├── src/
│   │   ├── Identity.API/               # Auth + User (from AuthServer + User.API)
│   │   ├── Billing.API/                # Payment/Subscriptions (from Payment.API)
│   │   ├── Insights.API/               # Analytics/KPIs
│   │   ├── Notification.API/           # Email/Push
│   │   ├── Plugins.API/                # Plugin registry (NEW)
│   │   └── Gateway/                    # API Gateway (Ocelot)
│   ├── BuildingBlocks/                 # Shared libraries
│   └── tests/
│
├── runtime/                            # MozaiksCore Runtime
│   ├── packages/
│   │   ├── shell/                      # UI Shell (from mozaiks-core-public/src)
│   │   │   ├── src/
│   │   │   │   ├── App.tsx
│   │   │   │   ├── PluginLoader.ts
│   │   │   │   ├── Router.tsx
│   │   │   │   ├── Navigation.tsx
│   │   │   │   ├── Dashboard.tsx
│   │   │   │   ├── auth/
│   │   │   │   ├── components/
│   │   │   │   ├── core/
│   │   │   │   ├── notifications/
│   │   │   │   ├── plugins/
│   │   │   │   ├── profile/
│   │   │   │   └── subscription/
│   │   │   ├── public/
│   │   │   └── package.json
│   │   │
│   │   └── sdk/                        # Developer SDK
│   │       ├── src/
│   │       │   ├── hooks/
│   │       │   └── components/
│   │       └── package.json
│   │
│   ├── backend/                        # Python Backend (from mozaiks-core-public)
│   │   ├── core/
│   │   ├── app/
│   │   ├── keycloak/
│   │   ├── plugins/
│   │   ├── main.py
│   │   └── shared_app.py
│   │
│   ├── ai/                             # AI Runtime (from MozaiksAI)
│   │   ├── packages/
│   │   │   ├── core/                   # AI orchestration engine
│   │   │   ├── tools/                  # Built-in AI tools
│   │   │   ├── ui/                     # ChatUI (from MozaiksAI/ChatUI)
│   │   │   └── bridge/                 # AI bridge layer
│   │   └── tests/
│   │
│   └── package.json                    # Monorepo workspace root
│
├── docs/
├── examples/
├── plugins/                            # Core open source plugins
├── docker/
├── docker-compose.yml
├── .env.example
├── LICENSE                             # MIT
└── README.md
```

### 3.2 mozaiks-platform (Proprietary - FINAL)

```
mozaiks-platform/
├── src/
│   ├── ApiGateways/
│   │   └── OcelotApiGateway/
│   │
│   └── Services/
│       ├── Hosting.API/                # App provisioning & deployment
│       ├── Funding/
│       │   └── Funding.API/            # Revenue-share rounds
│       ├── Growth.API/
│       │   ├── AdEngine.API/           # Ad campaigns
│       │   └── GrowthEngine.API/       # Campaign orchestration
│       ├── Discovery/
│       │   └── Admin.API/              # Discovery + Admin aggregator
│       ├── Community/
│       │   └── CommunicationService.API/
│       └── Social/
│           └── App.API/
│
├── ai-models/                          # PROPRIETARY AI Assets
│   ├── workflows/
│   │   ├── AgentGenerator/
│   │   ├── AppGenerator/
│   │   ├── DesignDocs/
│   │   ├── SubscriptionAdvisor/
│   │   └── ValueEngine/
│   ├── packs/
│   └── shared/
│
├── plugins/                            # Platform Plugins (from MOZ-UI)
│   ├── _shared/                        # Shared plugin utilities
│   ├── moz.platform.dashboard/         # Main dashboard
│   ├── moz.platform.discovery/         # Marketplace & quests
│   ├── moz.platform.funding/           # Investment & funding
│   ├── moz.platform.hosting/           # Hosting & deployment
│   ├── moz.platform.growth/            # Marketing & campaigns
│   ├── moz.platform.community/         # Social & messaging
│   ├── moz.platform.admin/             # Platform administration
│   ├── moz.platform.teams/             # Team management
│   ├── moz.platform.governance/        # Governance & voting
│   ├── moz.platform.monetization/      # Payments & pricing
│   ├── moz.platform.ai/                # AI wizard integration
│   └── moz.platform.messaging/         # Direct messaging
│
├── provisioning-agent/
├── docker/
├── docker-compose.yml
└── README.md
```

---

## 4. Repo Status Summary

### Source Repos to Consolidate

| Repo | Purpose | Final State |
|------|---------|-------------|
| **control-plane** | Legacy monolith | DEPRECATED - content split to mozaiks-core + mozaiks-platform |
| **mozaiks-core-public** | React shell + Python runtime | DEPRECATED - merged into mozaiks-core/runtime |
| **MozaiksAI** | AI runtime + ChatUI + workflows | DEPRECATED - split across mozaiks-core/runtime/ai + mozaiks-platform/ai-models |
| **MOZ-UI** | Platform React UI | DEPRECATED - converted to plugins in mozaiks-platform/plugins |
| **mozaiks-app** | App template | KEEP - reference implementation for app developers |

### Target Repos (Final)

| Repo | Content | License | Status |
|------|---------|---------|--------|
| **mozaiks-core** | Runtime + AI + Core APIs | Open Source (MIT) | Target for OSS |
| **mozaiks-platform** | Platform services + Plugins + AI models | Proprietary | Target for proprietary |

---

## 5. Migration Execution Plan

### Phase 1: Directory Structure Setup (Day 1)

```powershell
# In mozaiks-core
$coreRoot = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-core"
New-Item -ItemType Directory -Force -Path "$coreRoot\runtime\packages\shell\src"
New-Item -ItemType Directory -Force -Path "$coreRoot\runtime\packages\sdk\src"
New-Item -ItemType Directory -Force -Path "$coreRoot\runtime\backend"
New-Item -ItemType Directory -Force -Path "$coreRoot\runtime\ai\packages\core"
New-Item -ItemType Directory -Force -Path "$coreRoot\runtime\ai\packages\tools"
New-Item -ItemType Directory -Force -Path "$coreRoot\runtime\ai\packages\ui"
New-Item -ItemType Directory -Force -Path "$coreRoot\runtime\ai\packages\bridge"
New-Item -ItemType Directory -Force -Path "$coreRoot\docs\runtime"
New-Item -ItemType Directory -Force -Path "$coreRoot\docs\ai"
New-Item -ItemType Directory -Force -Path "$coreRoot\examples\workflows"

# In mozaiks-platform
$platformRoot = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform"
New-Item -ItemType Directory -Force -Path "$platformRoot\ai-models\workflows"
New-Item -ItemType Directory -Force -Path "$platformRoot\ai-models\packs"
New-Item -ItemType Directory -Force -Path "$platformRoot\ai-models\shared"
New-Item -ItemType Directory -Force -Path "$platformRoot\plugins\_shared"
New-Item -ItemType Directory -Force -Path "$platformRoot\plugins\moz.platform.dashboard\frontend\pages"
New-Item -ItemType Directory -Force -Path "$platformRoot\plugins\moz.platform.discovery\frontend\pages"
New-Item -ItemType Directory -Force -Path "$platformRoot\plugins\moz.platform.funding\frontend\pages"
New-Item -ItemType Directory -Force -Path "$platformRoot\plugins\moz.platform.hosting\frontend\pages"
New-Item -ItemType Directory -Force -Path "$platformRoot\plugins\moz.platform.growth\frontend\pages"
New-Item -ItemType Directory -Force -Path "$platformRoot\plugins\moz.platform.community\frontend\pages"
New-Item -ItemType Directory -Force -Path "$platformRoot\plugins\moz.platform.admin\frontend\pages"
New-Item -ItemType Directory -Force -Path "$platformRoot\plugins\moz.platform.teams\frontend\pages"
New-Item -ItemType Directory -Force -Path "$platformRoot\plugins\moz.platform.governance\frontend\pages"
New-Item -ItemType Directory -Force -Path "$platformRoot\plugins\moz.platform.monetization\frontend\pages"
New-Item -ItemType Directory -Force -Path "$platformRoot\plugins\moz.platform.ai\frontend\pages"
New-Item -ItemType Directory -Force -Path "$platformRoot\plugins\moz.platform.messaging\frontend\pages"
```

### Phase 2: Copy mozaiks-core-public → mozaiks-core (Day 2)

```powershell
$srcRoot = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-core-public"
$coreRoot = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-core"

# Copy React frontend to shell
Copy-Item -Recurse "$srcRoot\src\*" "$coreRoot\runtime\packages\shell\src\"
Copy-Item -Recurse "$srcRoot\public\*" "$coreRoot\runtime\packages\shell\public\"
Copy-Item "$srcRoot\package.json" "$coreRoot\runtime\packages\shell\"
Copy-Item "$srcRoot\vite.config.js" "$coreRoot\runtime\packages\shell\"
Copy-Item "$srcRoot\tailwind.config.js" "$coreRoot\runtime\packages\shell\"

# Copy Python backend
Copy-Item -Recurse "$srcRoot\backend\*" "$coreRoot\runtime\backend\"

# Copy docs
Copy-Item -Recurse "$srcRoot\docs\*" "$coreRoot\docs\runtime\"
```

### Phase 3: Copy MozaiksAI → Split (Day 2-3)

```powershell
$aiRoot = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\MozaiksAI"
$coreRoot = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-core"
$platformRoot = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform"

# Open Source → mozaiks-core
Copy-Item -Recurse "$aiRoot\mozaiksai\core\*" "$coreRoot\runtime\ai\packages\core\"
Copy-Item -Recurse "$aiRoot\ChatUI\src\*" "$coreRoot\runtime\ai\packages\ui\src\"
Copy-Item -Recurse "$aiRoot\ChatUI\public\*" "$coreRoot\runtime\ai\packages\ui\public\"
Copy-Item "$aiRoot\ChatUI\package.json" "$coreRoot\runtime\ai\packages\ui\"
Copy-Item -Recurse "$aiRoot\docs\*" "$coreRoot\docs\ai\"
Copy-Item -Recurse "$aiRoot\tests\*" "$coreRoot\runtime\ai\tests\"
Copy-Item -Recurse "$aiRoot\workflows\_examples\*" "$coreRoot\examples\workflows\"

# Proprietary → mozaiks-platform
Copy-Item -Recurse "$aiRoot\workflows\AgentGenerator" "$platformRoot\ai-models\workflows\"
Copy-Item -Recurse "$aiRoot\workflows\AppGenerator" "$platformRoot\ai-models\workflows\"
Copy-Item -Recurse "$aiRoot\workflows\DesignDocs" "$platformRoot\ai-models\workflows\"
Copy-Item -Recurse "$aiRoot\workflows\SubscriptionAdvisor" "$platformRoot\ai-models\workflows\"
Copy-Item -Recurse "$aiRoot\workflows\ValueEngine" "$platformRoot\ai-models\workflows\"
Copy-Item -Recurse "$aiRoot\workflows\_pack\*" "$platformRoot\ai-models\packs\"
Copy-Item -Recurse "$aiRoot\workflows\_shared\*" "$platformRoot\ai-models\shared\"
```

### Phase 4: Convert MOZ-UI → Platform Plugins (Day 3-5)

```powershell
$mozuiRoot = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\MOZ-UI\src"
$platformRoot = "C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform"

# Copy shared utilities
Copy-Item -Recurse "$mozuiRoot\shared\components\*" "$platformRoot\plugins\_shared\components\"
Copy-Item -Recurse "$mozuiRoot\shared\context\*" "$platformRoot\plugins\_shared\context\"
Copy-Item -Recurse "$mozuiRoot\shared\hooks\*" "$platformRoot\plugins\_shared\hooks\"
Copy-Item -Recurse "$mozuiRoot\shared\utils\*" "$platformRoot\plugins\_shared\utils\"

# Copy Admin module
Copy-Item -Recurse "$mozuiRoot\modules\Admin\pages\*" "$platformRoot\plugins\moz.platform.admin\frontend\pages\"

# Copy Funding module
Copy-Item -Recurse "$mozuiRoot\modules\Funding\pages\*" "$platformRoot\plugins\moz.platform.funding\frontend\pages\"

# Copy Community module
Copy-Item -Recurse "$mozuiRoot\modules\Community\*" "$platformRoot\plugins\moz.platform.community\frontend\"

# Copy App module pages (distributed across multiple plugins)
# This requires manual file-by-file mapping based on Section 2.6
```

### Phase 5: Update Configurations (Day 5-6)

1. Create plugin.json manifests for each plugin
2. Update import paths in moved files
3. Create workspace package.json files
4. Update docker-compose files
5. Update CI/CD pipelines

---

## 6. Key Decisions

### 6.1 Python vs Node.js Backend
**Decision: Keep Python** - The FastAPI backend is production-ready. Rewriting to Node.js is a future consideration.

### 6.2 ChatUI Canonical Source
**Decision: MozaiksAI/ChatUI** - Use this as the canonical implementation. The mozaiks-core-public chat components are integration stubs.

### 6.3 Plugin Loading Strategy
**Decision: Dynamic Loading** - Plugins loaded at runtime via plugin.json manifests. Shell discovers and loads plugins dynamically.

### 6.4 MOZ-UI Conversion Strategy
**Decision: Module → Plugin** - Each MOZ-UI module becomes a self-contained plugin with its own plugin.json, pages, components, and services.

---

## 7. References

- [CORE_MIGRATION_PLAN.md](./CORE_MIGRATION_PLAN.md) - Original migration plan
- [MOZ_UI_MIGRATION_PLAN.md](./MOZ_UI_MIGRATION_PLAN.md) - MOZ-UI to plugins conversion
- [DATA_MOAT_ARCHITECTURE.md](../architecture/DATA_MOAT_ARCHITECTURE.md) - AdEngine + GrowthEngine architecture
- mozaiks-core-public/CLAUDE.md - Runtime agent instructions
- MozaiksAI/CLAUDE.md - AI runtime agent instructions
- MOZ-UI/CLAUDE.md - Platform UI instructions
