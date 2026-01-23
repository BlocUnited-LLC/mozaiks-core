# Migration Plan: Creating mozaiks-core Repository

> **Purpose:** This document provides the detailed execution plan for migrating from the current `control-plane` repository to the new `mozaiks-core` combined repository structure.
> **Date:** 2026-01-19
> **Status:** Strategic Migration Plan

---

## ⚠️ Prerequisites

**BEFORE executing this migration, complete the pre-work defined in:**

📄 **[TECHNICAL_GAPS_RESOLUTIONS.md](TECHNICAL_GAPS_RESOLUTIONS.md)**

| Pre-Work Item | Est. Time |
|--------------|----------|
| Define Build Strategy (NPM packages) | 1 day |
| Write DB Split Script | 3 days |
| Implement Frontend Event Bus | 2 days |
| Create Dev Orchestrator | 1 day |
| Define Role Boundaries (Core vs Platform) | 1 day |
| Document Cross-Repo API Contract | 1 day |

**Total Pre-Work: ~9 days**

---

## 1. Executive Summary

### Goal
Combine `mozaiks-core` (runtime + AI) and `control-plane-core` (backend) into a single open-source repository called `mozaiks-core`, while extracting Mozaiks-specific platform features into a separate `mozaiks-platform` repository.

### Outcome
```
BEFORE:
├── control-plane/          (mixed: core + platform)
├── mozaiks-core-pubilc/    (runtime only)
└── mozaiks-ai/             (AI runtime - separate)

AFTER:
├── mozaiks-core/            (runtime + AI + core backend, OPEN SOURCE)
└── mozaiks-platform/       (platform features + trained models/packs, PROPRIETARY)
```

> **Key Change:** MozaiksAI runtime is now part of mozaiks-core (combined as mozaiks-core-public), and is included in the open source offering. Everyone gets AI capabilities. The proprietary value is in the trained models, curated packs, and platform services.

---

## 2. Target Repository Structure

### 2.1 mozaiks-core (Open Source)

```
mozaiks-core/
├── .github/
│   ├── workflows/
│   │   ├── ci.yml                        # Build + test all components
│   │   ├── release.yml                   # Create releases
│   │   └── docker-publish.yml            # Publish Docker images
│   ├── ISSUE_TEMPLATE/
│   ├── PULL_REQUEST_TEMPLATE.md
│   └── CODEOWNERS
│
├── docs/
│   ├── getting-started/
│   │   ├── quickstart.md                 # 5-minute getting started
│   │   ├── self-hosting.md               # Full self-hosting guide
│   │   └── configuration.md              # All config options
│   ├── architecture/
│   │   ├── overview.md                   # System architecture
│   │   ├── plugin-system.md              # Plugin architecture
│   │   └── api-reference.md              # API documentation
│   ├── guides/
│   │   ├── creating-plugins.md           # Plugin development guide
│   │   ├── authentication.md             # Auth integration
│   │   └── payments.md                   # Payment setup
│   └── contributing/
│       ├── CONTRIBUTING.md               # Contribution guidelines
│       ├── CODE_OF_CONDUCT.md
│       └── development-setup.md          # Dev environment setup
│
├── runtime/                              # MozaiksCore Runtime (TypeScript/Node)
│   ├── packages/
│   │   ├── shell/                        # UI shell and plugin loader
│   │   │   ├── src/
│   │   │   │   ├── App.tsx               # Root application
│   │   │   │   ├── PluginLoader.ts       # Dynamic plugin loading
│   │   │   │   ├── Router.tsx            # Dynamic route construction
│   │   │   │   ├── Navigation.tsx        # Dynamic navigation
│   │   │   │   ├── Dashboard.tsx         # Widget slot management
│   │   │   │   └── contexts/
│   │   │   │       ├── RuntimeContext.tsx
│   │   │   │       └── AuthContext.tsx
│   │   │   ├── package.json
│   │   │   └── tsconfig.json
│   │   │
│   │   ├── sdk/                          # Developer SDK
│   │   │   ├── src/
│   │   │   │   ├── hooks/
│   │   │   │   │   ├── useRuntime.ts
│   │   │   │   │   ├── usePluginApi.ts
│   │   │   │   │   ├── usePluginSettings.ts
│   │   │   │   │   └── useAuth.ts
│   │   │   │   ├── components/
│   │   │   │   │   ├── ProtectedRoute.tsx
│   │   │   │   │   └── WidgetSlot.tsx
│   │   │   │   └── index.ts
│   │   │   ├── package.json
│   │   │   └── tsconfig.json
│   │   │
│   │   ├── server/                       # Backend runtime (Node/Express)
│   │   │   ├── src/
│   │   │   │   ├── index.ts              # Server entry point
│   │   │   │   ├── pluginHost.ts         # Plugin backend loader
│   │   │   │   ├── middleware/
│   │   │   │   │   ├── auth.ts           # JWT validation
│   │   │   │   │   └── pluginContext.ts  # Plugin context injection
│   │   │   │   └── services/
│   │   │   │       ├── DatabaseService.ts
│   │   │   │       ├── StorageService.ts
│   │   │   │       └── ControlPlaneClient.ts
│   │   │   └── package.json
│   │   │
│   │   └── cli/                          # Developer CLI
│   │       ├── src/
│   │       │   ├── commands/
│   │       │   │   ├── create.ts         # mozaiks create <app>
│   │       │   │   ├── dev.ts            # mozaiks dev
│   │       │   │   ├── plugin.ts         # mozaiks plugin create <name>
│   │       │   │   └── deploy.ts         # mozaiks deploy (cloud only)
│   │       │   └── index.ts
│   │       └── package.json
│   │
│   ├── ai/                               # AI Runtime (Open Source)
│   │   ├── packages/
│   │   │   ├── core/                     # AI orchestration engine
│   │   │   │   ├── src/
│   │   │   │   │   ├── Agent.ts          # Main AI agent class
│   │   │   │   │   ├── ToolExecutor.ts   # Tool/function execution
│   │   │   │   │   ├── ArtifactHandler.ts # Code artifact management
│   │   │   │   │   ├── ChatSession.ts    # Conversation management
│   │   │   │   │   └── providers/
│   │   │   │   │       ├── OpenAIProvider.ts
│   │   │   │   │       ├── AnthropicProvider.ts
│   │   │   │   │       └── LocalProvider.ts   # Ollama, etc.
│   │   │   │   └── package.json
│   │   │   │
│   │   │   ├── tools/                    # Built-in AI tools
│   │   │   │   ├── src/
│   │   │   │   │   ├── PluginGenerator.ts    # Generate plugin code
│   │   │   │   │   ├── DatabaseTool.ts       # Query/modify DB
│   │   │   │   │   ├── FileTool.ts           # Read/write files
│   │   │   │   │   └── DeployTool.ts         # Trigger deployments
│   │   │   │   └── package.json
│   │   │   │
│   │   │   └── ui/                       # Chat UI components
│   │   │       ├── src/
│   │   │       │   ├── ChatWindow.tsx
│   │   │       │   ├── MessageList.tsx
│   │   │       │   ├── ArtifactPreview.tsx
│   │   │       │   └── ToolStatus.tsx
│   │   │       └── package.json
│   │   │
│   │   └── package.json
│   │
│   ├── package.json                      # Workspace root
│   ├── turbo.json                        # Turborepo config
│   └── tsconfig.base.json
│
├── backend/                              # Control Plane Core (C#/.NET)
│   ├── src/
│   │   ├── Gateway/
│   │   │   ├── Gateway.csproj
│   │   │   ├── Program.cs
│   │   │   └── ocelot.json               # Routing config
│   │   │
│   │   ├── Identity.API/                 # Was AuthServer + User
│   │   │   ├── Identity.API.csproj
│   │   │   ├── Program.cs
│   │   │   ├── Controllers/
│   │   │   │   ├── AppsController.cs
│   │   │   │   ├── UsersController.cs
│   │   │   │   ├── TeamsController.cs
│   │   │   │   ├── EntitlementsController.cs
│   │   │   │   └── AuthController.cs
│   │   │   ├── Services/
│   │   │   │   ├── AppService.cs
│   │   │   │   ├── UserService.cs
│   │   │   │   ├── TeamService.cs
│   │   │   │   ├── EntitlementService.cs
│   │   │   │   └── TokenService.cs
│   │   │   ├── Models/
│   │   │   │   ├── App.cs
│   │   │   │   ├── User.cs
│   │   │   │   ├── Team.cs
│   │   │   │   └── Entitlement.cs
│   │   │   └── Data/
│   │   │       └── IdentityDbContext.cs
│   │   │
│   │   ├── Billing.API/                  # Was Payment
│   │   │   ├── Billing.API.csproj
│   │   │   ├── Program.cs
│   │   │   ├── Controllers/
│   │   │   │   ├── SubscriptionsController.cs
│   │   │   │   ├── CheckoutController.cs
│   │   │   │   ├── WebhooksController.cs
│   │   │   │   ├── UsageController.cs
│   │   │   │   └── MozaiksPayController.cs
│   │   │   ├── Services/
│   │   │   │   ├── StripeService.cs
│   │   │   │   ├── SubscriptionService.cs
│   │   │   │   ├── LedgerService.cs
│   │   │   │   └── MozaiksPayService.cs
│   │   │   └── Models/
│   │   │
│   │   ├── Insights.API/
│   │   │   ├── Insights.API.csproj
│   │   │   ├── Program.cs
│   │   │   ├── Controllers/
│   │   │   │   ├── TelemetryController.cs
│   │   │   │   ├── KpiController.cs
│   │   │   │   └── MetricsController.cs
│   │   │   └── Services/
│   │   │
│   │   ├── Plugins.API/                  # NEW
│   │   │   ├── Plugins.API.csproj
│   │   │   ├── Program.cs
│   │   │   ├── Controllers/
│   │   │   │   ├── CatalogController.cs
│   │   │   │   ├── ManifestsController.cs
│   │   │   │   └── InstallationsController.cs
│   │   │   └── Services/
│   │   │       └── PluginRegistryService.cs
│   │   │
│   │   └── BuildingBlocks/               # Shared libraries
│   │       ├── Mozaiks.Auth/
│   │       ├── Mozaiks.Events/
│   │       └── Mozaiks.Testing/
│   │
│   ├── tests/
│   │   ├── Identity.API.Tests/
│   │   ├── Billing.API.Tests/
│   │   ├── Insights.API.Tests/
│   │   └── Plugins.API.Tests/
│   │
│   ├── MozaiksCore.sln
│   └── Directory.Build.props
│
├── examples/                             # Example apps and plugins
│   ├── hello-world/                      # Minimal app
│   ├── blog/                             # Blog with posts/comments
│   └── store/                            # E-commerce example
│
├── plugins/                              # Core plugins (open source)
│   ├── moz.app.blog/
│   │   ├── plugin.json
│   │   ├── backend/
│   │   └── frontend/
│   ├── moz.app.newsletter/
│   └── moz.app.forms/
│
├── docker/
│   ├── Dockerfile.runtime                # Runtime container
│   ├── Dockerfile.gateway                # Gateway container
│   ├── Dockerfile.identity               # Identity API container
│   ├── Dockerfile.billing                # Billing API container
│   ├── Dockerfile.insights               # Insights API container
│   └── Dockerfile.plugins                # Plugins API container
│
├── docker-compose.yml                    # Full stack for self-hosting
├── docker-compose.dev.yml                # Development overrides
├── docker-compose.test.yml               # Testing config
│
├── scripts/
│   ├── dev/
│   │   ├── start.ps1                     # Start dev environment
│   │   ├── start.sh
│   │   └── seed-data.js                  # Seed sample data
│   └── release/
│       └── publish-images.sh             # Publish Docker images
│
├── .env.example                          # Example environment variables
├── LICENSE                               # MIT or Apache 2.0
├── README.md                             # Main documentation
└── CHANGELOG.md                          # Version history
```

---

### 2.2 mozaiks-platform (Proprietary)

```
mozaiks-platform/
├── .github/
│   └── workflows/
│       ├── ci.yml
│       └── cd.yml                        # Deploy to Mozaiks Cloud
│
├── src/
│   ├── Gateway/                          # Extended gateway config
│   │   └── ocelot.platform.json          # Platform route additions
│   │
│   ├── Hosting.API/
│   │   ├── Controllers/
│   │   │   ├── DeploymentsController.cs
│   │   │   ├── DomainsController.cs
│   │   │   └── JobsController.cs
│   │   └── Services/
│   │       ├── ProvisioningOrchestrator.cs
│   │       └── ContainerService.cs
│   │
│   ├── Funding.API/                      # Was Governance
│   │   ├── Controllers/
│   │   │   ├── RoundsController.cs
│   │   │   ├── InvestmentsController.cs
│   │   │   └── PositionsController.cs
│   │   └── Services/
│   │       ├── FundingService.cs
│   │       └── RevenueDistributionService.cs
│   │
│   ├── Growth.API/                       # Was GrowthEngine
│   │   ├── Controllers/
│   │   │   ├── CampaignsController.cs
│   │   │   ├── OAuthController.cs
│   │   │   └── BudgetController.cs
│   │   └── Services/
│   │       ├── MetaAdsService.cs
│   │       └── AttributionService.cs
│   │
│   ├── Discovery.API/                    # Was Admin
│   │   ├── Controllers/
│   │   │   ├── MarketplaceController.cs
│   │   │   ├── FeaturedController.cs
│   │   │   └── ChallengesController.cs
│   │   └── Services/
│   │
│   └── Community.API/                    # Was CommunicationService + App
│       ├── Controllers/
│       │   ├── MessagesController.cs
│       │   └── PostsController.cs
│       ├── Hubs/
│       │   └── CommunityHub.cs
│       └── Services/
│
├── provisioning-agent/                   # Provisioning Worker (separate deployable)
│   ├── src/
│   │   ├── Provisioning.Agent/           # Main agent service
│   │   │   ├── Program.cs
│   │   │   ├── Workers/
│   │   │   │   └── ProvisioningWorker.cs
│   │   │   ├── Handlers/
│   │   │   │   ├── ContainerAppsHandler.cs
│   │   │   │   ├── DomainHandler.cs
│   │   │   │   ├── DnsHandler.cs
│   │   │   │   └── TlsHandler.cs
│   │   │   └── Services/
│   │   │       ├── AzureContainerAppsService.cs
│   │   │       ├── OpenSrsService.cs
│   │   │       └── AzureDnsService.cs
│   │   └── Provisioning.Agent.Core/      # Shared models
│   │       └── Models/
│   │           ├── ProvisioningRequest.cs
│   │           ├── ExperimentContext.cs  # For Optimization Loop
│   │           ├── ReleaseTarget.cs
│   │           └── TelemetryConfig.cs
│   ├── Dockerfile
│   └── README.md
│
├── ai-models/                            # Trained Models & Packs (Proprietary)
│   ├── models/
│   │   ├── plugin-generator-v1/          # Fine-tuned model weights/configs
│   │   └── code-reviewer-v1/
│   ├── packs/                            # Curated prompt packs
│   │   ├── ecommerce-pack/
│   │   │   ├── pack.json
│   │   │   ├── prompts/
│   │   │   └── templates/
│   │   ├── saas-pack/
│   │   ├── marketplace-pack/
│   │   └── community-pack/
│   └── quality/                          # Quality assurance layers
│       ├── validators/
│       └── reviewers/
│
├── plugins/                              # Platform plugins (proprietary)
│   ├── moz.platform.hosting/
│   │   ├── plugin.json
│   │   ├── backend/                      # Calls Hosting.API
│   │   └── frontend/
│   │       ├── pages/
│   │       │   ├── DeploymentsPage.jsx
│   │       │   └── DomainsPage.jsx
│   │       └── widgets/
│   │
│   ├── moz.platform.funding/
│   │   ├── plugin.json
│   │   └── frontend/
│   │       └── pages/
│   │           ├── RoundsPage.jsx
│   │           └── CapTablePage.jsx
│   │
│   ├── moz.platform.growth/
│   ├── moz.platform.discovery/
│   └── moz.platform.community/
│
├── infra/                                # Infrastructure as Code
│   ├── bicep/
│   │   ├── main.bicep
│   │   └── modules/
│   └── scripts/
│
├── tests/
│
├── docker-compose.yml                    # Includes core + platform
├── MozaiksPlatform.sln
└── README.md
```

---

## 3. Migration Steps

### Phase 1: Preparation (Week 1)

#### 1.1 Create New Repositories
```bash
# Create mozaiks-core repo
gh repo create mozaiks/mozaiks-core --public --description "Open source runtime and core services for Mozaiks platform"

# Create mozaiks-platform repo
gh repo create mozaiks/mozaiks-platform --private --description "Proprietary Mozaiks platform services"
```

#### 1.2 Set Up Repository Structure
```bash
# Clone and initialize mozaiks-core
git clone https://github.com/mozaiks/mozaiks-core
cd mozaiks-core

# Create directory structure
mkdir -p runtime/packages/{shell,sdk,server,cli}
mkdir -p backend/src/{Gateway,Identity.API,Billing.API,Insights.API,Plugins.API,BuildingBlocks}
mkdir -p docs/{getting-started,architecture,guides,contributing}
mkdir -p examples/{hello-world,blog,store}
mkdir -p plugins/{moz.app.blog,moz.app.newsletter,moz.app.forms}
mkdir -p docker scripts/{dev,release}
```

#### 1.3 Initialize Package Management
```bash
# Backend (C#)
cd backend
dotnet new sln -n MozaiksCore
dotnet new webapi -n Gateway -o src/Gateway
dotnet new webapi -n Identity.API -o src/Identity.API
dotnet new webapi -n Billing.API -o src/Billing.API
dotnet new webapi -n Insights.API -o src/Insights.API
dotnet new webapi -n Plugins.API -o src/Plugins.API

# Add to solution
dotnet sln add src/Gateway/Gateway.csproj
dotnet sln add src/Identity.API/Identity.API.csproj
dotnet sln add src/Billing.API/Billing.API.csproj
dotnet sln add src/Insights.API/Insights.API.csproj
dotnet sln add src/Plugins.API/Plugins.API.csproj

cd ..

# Runtime (TypeScript)
cd runtime
npm init -y
npm install turbo -D
# Initialize workspace packages...
```

---

### Phase 2: Core Backend Migration (Week 2-3)

#### 2.1 Migrate Identity.API

**Source files from `control-plane`:**
```
AuthServer.Api/Controllers/AppController.cs      → Identity.API/Controllers/AppsController.cs
AuthServer.Api/Controllers/AuthController.cs     → Identity.API/Controllers/AuthController.cs
AuthServer.Api/Models/MozaiksAppModel.cs         → Identity.API/Models/App.cs
AuthServer.Api/Models/AppUserModel.cs            → Identity.API/Models/User.cs
AuthServer.Api/Services/AuthService.cs           → Identity.API/Services/AuthService.cs
User.API/Controllers/UserController.cs           → Identity.API/Controllers/UsersController.cs
User.API/Services/UserService.cs                 → Identity.API/Services/UserService.cs
```

**Refactoring required:**
1. Merge `AuthServer` and `User.API` into single service
2. Rename models for clarity (remove "Model" suffix)
3. Update namespace from `AuthServer.Api` to `Mozaiks.Identity`
4. Update API routes to use `/api/core/identity/*` prefix
5. Remove Mozaiks-specific references (funding, hosting status)

#### 2.2 Migrate Billing.API

**Source files:**
```
Payment.API/Controllers/MozaiksPayController.cs  → Billing.API/Controllers/MozaiksPayController.cs
Payment.API/Controllers/StripeWebhookController.cs → Billing.API/Controllers/WebhooksController.cs
Payment.API/Services/StripeService.cs            → Billing.API/Services/StripeService.cs
Payment.API/Services/LedgerService.cs            → Billing.API/Services/LedgerService.cs
Payment.API/Models/*                             → Billing.API/Models/*
```

**Refactoring required:**
1. Rename from `Payment.API` to `Billing.API`
2. Update namespace to `Mozaiks.Billing`
3. Remove funding-specific ledger entries (move to platform)
4. Update API routes to `/api/core/billing/*`

#### 2.3 Migrate Insights.API

**Source files:**
```
Insights.API/* → backend/src/Insights.API/*
```

**Refactoring required:**
1. Update namespace to `Mozaiks.Insights`
2. Update API routes to `/api/core/insights/*`
3. Implement actual telemetry ingestion (currently stub)

#### 2.4 Create Plugins.API (New)

**New service for plugin registry:**
```csharp
// Controllers/CatalogController.cs
[ApiController]
[Route("api/core/plugins")]
public class CatalogController : ControllerBase
{
    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog() { }
    
    [HttpGet("{pluginId}")]
    public async Task<IActionResult> GetPlugin(string pluginId) { }
    
    [HttpGet("{pluginId}/manifest")]
    public async Task<IActionResult> GetManifest(string pluginId) { }
}

// Controllers/InstallationsController.cs
[ApiController]
[Route("api/core/plugins/apps/{appId}")]
public class InstallationsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetInstalledPlugins(string appId) { }
    
    [HttpPost("install")]
    public async Task<IActionResult> InstallPlugin(string appId, [FromBody] InstallRequest request) { }
    
    [HttpDelete("{pluginId}")]
    public async Task<IActionResult> UninstallPlugin(string appId, string pluginId) { }
}
```

#### 2.5 Migrate BuildingBlocks

**Source:**
```
BuildingBlocks/EventBus.Messages  → backend/src/BuildingBlocks/Mozaiks.Events
BuildingBlocks/Mozaiks.Auth       → backend/src/BuildingBlocks/Mozaiks.Auth
BuildingBlocks/Mozaiks.Auditing   → backend/src/BuildingBlocks/Mozaiks.Auditing
```

---

### Phase 3: Runtime Migration (Week 3-4)

#### 3.1 Migrate from mozaiks-core Repository

**Shell package:**
```
mozaiks-core/src/App.tsx           → runtime/packages/shell/src/App.tsx
mozaiks-core/src/PluginLoader.ts   → runtime/packages/shell/src/PluginLoader.ts
mozaiks-core/src/Router.tsx        → runtime/packages/shell/src/Router.tsx
```

**SDK package:**
```
mozaiks-core/src/hooks/*           → runtime/packages/sdk/src/hooks/*
mozaiks-core/src/components/*      → runtime/packages/sdk/src/components/*
```

**Server package:**
```
mozaiks-core/server/*              → runtime/packages/server/src/*
```

#### 3.2 Create CLI Package

New CLI for developer experience:
```typescript
// runtime/packages/cli/src/commands/create.ts
export async function create(appName: string) {
  // Scaffold new app directory
  // Copy template files
  // Initialize package.json
  // Set up connection to control plane
}

// runtime/packages/cli/src/commands/dev.ts
export async function dev() {
  // Start runtime in dev mode
  // Connect to local or cloud control plane
  // Hot reload on changes
}

// runtime/packages/cli/src/commands/plugin.ts
export async function createPlugin(pluginName: string) {
  // Scaffold plugin directory structure
  // Create plugin.json manifest
  // Create template backend/frontend files
}
```

---

### Phase 4: Platform Repository Setup (Week 4-5)

#### 4.1 Extract Platform Services

**Move to mozaiks-platform:**
```
Hosting.API/*           → mozaiks-platform/src/Hosting.API/
Governance.API/*        → mozaiks-platform/src/Funding.API/
GrowthEngine.API/*  → mozaiks-platform/src/Growth.API/
Admin.API/*             → mozaiks-platform/src/Discovery.API/
CommunicationService/*  → mozaiks-platform/src/Community.API/
App.API/*               → mozaiks-platform/src/Community.API/ (merge)
```

#### 4.2 Create Platform Plugins

Each platform feature becomes a plugin that the Mozaiks app installs:

```json
// plugins/moz.platform.hosting/plugin.json
{
  "id": "moz.platform.hosting",
  "name": "Mozaiks Hosting",
  "description": "App deployment and hosting management",
  "permissions": [],
  "api": {
    "external": true,
    "baseUrl": "${PLATFORM_API_URL}/api/platform/hosting"
  },
  "ui": {
    "pages": [
      { "path": "/deployments", "component": "frontend/pages/DeploymentsPage.jsx" },
      { "path": "/deployments/:appId", "component": "frontend/pages/DeploymentDetailPage.jsx" },
      { "path": "/domains", "component": "frontend/pages/DomainsPage.jsx" }
    ],
    "navigation": [
      { "label": "Hosting", "path": "/deployments", "icon": "Cloud", "section": "main" }
    ],
    "widgets": [
      { "slot": "dashboard_main", "component": "frontend/widgets/DeploymentStatusWidget.jsx" }
    ]
  }
}
```

---

### Phase 5: Docker & DevOps (Week 5-6)

#### 5.1 Create Dockerfiles

```dockerfile
# docker/Dockerfile.identity
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Identity.API/Identity.API.csproj", "Identity.API/"]
COPY ["src/BuildingBlocks/", "BuildingBlocks/"]
RUN dotnet restore "Identity.API/Identity.API.csproj"
COPY src/ .
RUN dotnet build "Identity.API/Identity.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Identity.API/Identity.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Identity.API.dll"]
```

#### 5.2 Create docker-compose.yml

```yaml
# docker-compose.yml
version: '3.8'

services:
  # ══════════════════════════════════════════════════════════════════════
  # INFRASTRUCTURE
  # ══════════════════════════════════════════════════════════════════════
  mongodb:
    image: mongo:7.0
    ports:
      - "27017:27017"
    volumes:
      - mongodb_data:/data/db
    environment:
      MONGO_INITDB_DATABASE: MozaiksCoreDB

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"

  rabbitmq:
    image: rabbitmq:3-management-alpine
    ports:
      - "5672:5672"
      - "15672:15672"

  # ══════════════════════════════════════════════════════════════════════
  # CORE SERVICES
  # ══════════════════════════════════════════════════════════════════════
  gateway:
    build:
      context: ./backend
      dockerfile: ../docker/Dockerfile.gateway
    ports:
      - "8010:8010"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
    depends_on:
      - identity-api
      - billing-api
      - insights-api
      - plugins-api

  identity-api:
    build:
      context: ./backend
      dockerfile: ../docker/Dockerfile.identity
    ports:
      - "8001:8001"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - MongoDB__ConnectionString=mongodb://mongodb:27017
      - MongoDB__DatabaseName=MozaiksCoreDB
    depends_on:
      - mongodb
      - redis

  billing-api:
    build:
      context: ./backend
      dockerfile: ../docker/Dockerfile.billing
    ports:
      - "8002:8002"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - MongoDB__ConnectionString=mongodb://mongodb:27017
      - Stripe__SecretKey=${STRIPE_SECRET_KEY}
    depends_on:
      - mongodb

  insights-api:
    build:
      context: ./backend
      dockerfile: ../docker/Dockerfile.insights
    ports:
      - "8003:8003"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - MongoDB__ConnectionString=mongodb://mongodb:27017
    depends_on:
      - mongodb

  plugins-api:
    build:
      context: ./backend
      dockerfile: ../docker/Dockerfile.plugins
    ports:
      - "8004:8004"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - MongoDB__ConnectionString=mongodb://mongodb:27017
    depends_on:
      - mongodb

  # ══════════════════════════════════════════════════════════════════════
  # RUNTIME
  # ══════════════════════════════════════════════════════════════════════
  runtime:
    build:
      context: ./runtime
      dockerfile: ../docker/Dockerfile.runtime
    ports:
      - "3000:3000"
    environment:
      - NODE_ENV=development
      - CONTROL_PLANE_URL=http://gateway:8010
    depends_on:
      - gateway

volumes:
  mongodb_data:
```

---

### Phase 6: Documentation & Release (Week 6)

#### 6.1 Create README.md

```markdown
# Mozaiks OSS

The open-source runtime and core services for building multi-tenant SaaS applications.

## Quick Start

```bash
# Clone the repository
git clone https://github.com/mozaiks/mozaiks-core
cd mozaiks-core

# Start the stack
docker-compose up -d

# Open the runtime
open http://localhost:3000
```

## What's Included

- **Runtime**: Plugin-based application shell
- **Identity API**: User and app management
- **Billing API**: Subscription and payment processing
- **Insights API**: Telemetry and analytics
- **Plugins API**: Plugin registry and management

## Self-Hosting

See [Self-Hosting Guide](docs/getting-started/self-hosting.md) for detailed instructions.

## Creating Plugins

See [Plugin Development Guide](docs/guides/creating-plugins.md) to build your own plugins.

## License

MIT License - see [LICENSE](LICENSE) for details.
```

#### 6.2 Create CONTRIBUTING.md

```markdown
# Contributing to Mozaiks OSS

We welcome contributions! Here's how to get started.

## Development Setup

1. Prerequisites
   - .NET 8 SDK
   - Node.js 20+
   - Docker & Docker Compose
   - MongoDB (or use Docker)

2. Clone and Setup
   ```bash
   git clone https://github.com/mozaiks/mozaiks-core
   cd mozaiks-core
   ./scripts/dev/start.sh
   ```

3. Run Tests
   ```bash
   # Backend
   cd backend && dotnet test

   # Runtime
   cd runtime && npm test
   ```

## Pull Request Process

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests
5. Submit PR against `main`

## Code Style

- C#: Follow Microsoft conventions
- TypeScript: ESLint + Prettier

## Questions?

Open an issue or join our Discord.
```

---

## 4. Data Migration

### 4.1 Collection Mapping

| Old Collection | New Location | Notes |
|----------------|--------------|-------|
| `MozaiksApps` | MozaiksCoreDB.Apps | Rename |
| `AppUsers` | MozaiksCoreDB.Users | Rename |
| `Teams` | MozaiksCoreDB.Teams | Keep |
| `RefreshTokens` | MozaiksCoreDB.RefreshTokens | Keep |
| `SubscriptionPlans` | MozaiksCoreDB.SubscriptionPlans | Keep |
| `Transactions` | MozaiksCoreDB.Transactions | Keep |
| `LedgerEntries` | MozaiksCoreDB.LedgerEntries | Core only (non-funding) |
| `FundingRounds` | MozaiksPlatformDB.FundingRounds | Move to platform |
| `Investments` | MozaiksPlatformDB.Investments | Move to platform |
| `HostedApps` | MozaiksPlatformDB.HostedApps | Move to platform |
| `AdCampaigns` | MozaiksPlatformDB.AdCampaigns | Move to platform |

### 4.2 Migration Script

```javascript
// scripts/migrations/migrate-to-split.js

const { MongoClient } = require('mongodb');

async function migrate() {
  const sourceClient = new MongoClient(process.env.SOURCE_MONGO_URI);
  const coreClient = new MongoClient(process.env.CORE_MONGO_URI);
  const platformClient = new MongoClient(process.env.PLATFORM_MONGO_URI);

  await sourceClient.connect();
  await coreClient.connect();
  await platformClient.connect();

  const source = sourceClient.db('MozaiksDB');
  const core = coreClient.db('MozaiksCoreDB');
  const platform = platformClient.db('MozaiksPlatformDB');

  // Core collections
  console.log('Migrating core collections...');
  await migrateCollection(source, core, 'MozaiksApps', 'Apps');
  await migrateCollection(source, core, 'AppUsers', 'Users');
  await migrateCollection(source, core, 'Teams', 'Teams');
  await migrateCollection(source, core, 'RefreshTokens', 'RefreshTokens');
  await migrateCollection(source, core, 'SubscriptionPlans', 'SubscriptionPlans');
  await migrateCollection(source, core, 'Transactions', 'Transactions');

  // Platform collections
  console.log('Migrating platform collections...');
  await migrateCollection(source, platform, 'FundingRounds', 'FundingRounds');
  await migrateCollection(source, platform, 'Investments', 'Investments');
  await migrateCollection(source, platform, 'HostedApps', 'HostedApps');
  await migrateCollection(source, platform, 'ProvisioningJobs', 'ProvisioningJobs');
  await migrateCollection(source, platform, 'AdCampaigns', 'AdCampaigns');

  console.log('Migration complete!');
}

async function migrateCollection(source, target, sourceCollection, targetCollection) {
  const docs = await source.collection(sourceCollection).find({}).toArray();
  if (docs.length > 0) {
    await target.collection(targetCollection).insertMany(docs);
    console.log(`  ${sourceCollection} → ${targetCollection}: ${docs.length} documents`);
  }
}

migrate().catch(console.error);
```

---

## 5. Timeline Summary

| Week | Phase | Key Deliverables |
|------|-------|------------------|
| 1 | Preparation | Repos created, structure initialized |
| 2 | Backend Migration | Identity.API migrated |
| 3 | Backend Migration | Billing.API, Insights.API migrated |
| 4 | Runtime Migration | Shell, SDK, Server migrated |
| 5 | Platform Setup | Platform services extracted |
| 6 | DevOps & Docs | Docker, CI/CD, documentation |

---

## 6. Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Breaking existing deployments | Keep old repo functional during migration |
| Data loss | Run migration scripts in test env first |
| Missing functionality | Feature parity checklist before go-live |
| Community confusion | Clear announcement, migration guide |

---

## 7. Success Criteria

- [ ] `docker-compose up` starts full stack in < 2 minutes
- [ ] All existing API endpoints work under new paths
- [ ] Test coverage > 80% on core services
- [ ] Documentation covers all self-hosting scenarios
- [ ] Example app runs successfully
- [ ] CI/CD pipeline passes for all services
