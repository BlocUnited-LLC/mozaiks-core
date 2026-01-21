# Mozaiks Core

# 🎯 MozaikCore Runtime

<div align="center">

![MozaiksAI Logo](runtime\packages\shell\public\mozaik_logo.svg)

**OWN THE AGENTIC ERA**  
*Event-Driven • Declarative • Multi-Tenant • Production-Ready*

[![AG2 Framework](https://img.shields.io/badge/AG2-Autogen-green?style=flat&logo=microsoft)](https://microsoft.github.io/autogen/)
[![Python](https://img.shields.io/badge/Python-3.9+-blue?style=flat&logo=python)](https://www.python.org/)
[![FastAPI](https://img.shields.io/badge/FastAPI-Latest-009688?style=flat&logo=fastapi)](https://fastapi.tiangolo.com/)
[![MongoDB](https://img.shields.io/badge/MongoDB-Persistence-47A248?style=flat&logo=mongodb)](https://www.mongodb.com/)
[![C#](https://img.shields.io/badge/C%23-.NET%208-512BD4?style=flat&logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)

**Production-grade runtime for multi-agent AI workflows built on Microsoft's AG2 framework.**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

[Quick Start](#-quick-start) • [Documentation](#-documentation) • [Architecture](#-architecture) • [Features](#-features)

</div>

---

# 🎯 What is MozaiksCore?

The **MozaiksCore** is a production-ready orchestration engine that transforms AG2 (Microsoft Autogen) into an app-grade platform with:

## Mozaiks AI

### ⚡ Real-Time Event Streaming
Every agent message, tool call, and state change flows through WebSocket to your frontend.

- **Dual Protocol Support** → WebSocket with SSE fallback
- **Message Filtering** → Show only relevant agents to end users
- **Event Correlation** → Track request/response flows with unique IDs
- **Bi-Directional** → Frontend can trigger backend handlers

### 💾 Persistent State Management
Never lose context—every workflow execution is fully persisted and resumable.

- **AG2 State Serialization** → Complete groupchat state to MongoDB
- **Message History** → Full chat transcripts with metadata
- **Session Resume** → Pick up any conversation exactly where it left off
- **Token Tracking** → Real-time cost metrics per chat/agent/workflow

### 🔐 Multi-Tenant by Design
app-grade isolation and security built from the ground up.

- **App Isolation** → Separate MongoDB collections per `app_id`
- **Cache Seed Propagation** → Deterministic per-chat seeds prevent state bleed
- **Secret Management** → Secure credential collection and storage
- **Context Boundaries** → No data leakage across tenants

### 📊 App Observability
Comprehensive monitoring, metrics, and analytics out of the box.

- **Performance Metrics** → `/metrics/perf/*` endpoints for monitoring
- **Structured Logging** → JSON Lines or pretty text format
- **AG2 Runtime Logger** → SQLite-backed execution traces
- **Real-Time Analytics** → Live token usage and cost tracking

### 🎯 Dynamic UI Integration
Agents can invoke React components dynamically during workflow execution.

- **UI Tools** → Agents call `display_action_plan()` → frontend renders artifact
- **Auto-Tool Mode** → Execute tools without asking permission
- **Context Sync** → Shared state between agents and UI components
- **Theme System** → Per-app design system customization

---

### 🏗️ Architecture

MozaiksAI follows a **clean, modular architecture** where every component has a single responsibility.

```
┌─────────────────────────────────────────────────────────┐
│              ChatUI (React Frontend)                    │
│  • WebSocket Client                                     │
│  • Dynamic Component Renderer                           │
│  • Artifact Design System                               │
└──────────────────┬──────────────────────────────────────┘
                   │ WebSocket/HTTP
┌──────────────────▼──────────────────────────────────────┐
│         MozaiksAI Runtime (FastAPI + AG2)               │
│                                                         │
│  ┌────────────────────────────────────────────────┐     │
│  │  Transport Layer (WebSocket)                   │     │
│  │  • Connection lifecycle                        │     │
│  │  • Message filtering (visual_agents)           │     │
│  └────────────────────────────────────────────────┘     │
│  ┌────────────────────────────────────────────────┐     │
│  │  Unified Event Dispatcher                      │     │
│  │  • Business Events → Logging                   │     │
│  │  • UI Tool Events → WebSocket                  │     │
│  │  • AG2 Events → Serialization                  │     │
│  └────────────────────────────────────────────────┘     │
│  ┌────────────────────────────────────────────────┐     │
│  │  Orchestration Engine                          │     │
│  │  • Workflow discovery & loading                │     │
│  │  • AG2 pattern execution                       │     │
│  │  • Tool registry & binding                     │     │
│  └────────────────────────────────────────────────┘     │
│  ┌────────────────────────────────────────────────┐     │
│  │  Persistence Layer (MongoDB)                   │     │
│  │  • Chat sessions & message history             │     │
│  │  • Token & cost tracking                       │     │
│  └───────────────┬────────────────────────────────┘     │
└──────────────────│──────────────────────────────────────┘
                   │ MongoDB Protocol
┌──────────────────▼──────────────────────────────────────┐
│              MongoDB (Atlas / Local)                    │
│  • chat_sessions                                        │
│  • workflow_stats_{app}_{workflow}                      │
└─────────────────────────────────────────────────────────┘
```
## 📚 Documentation

Comprehensive documentation organized by use case:

👉 **[Documentation Portal](docs/README.md)** 👈

### Quick Links

| Topic | Document |
|-------|----------|
| **Core Architecture** | [Core Architecture](docs/core/architecture.md) |
| **Plugins** | [Plugin Runtime](docs/core/plugins.md) |
| **Authentication** | [Auth & Identity](docs/core/authentication.md) |
| **Events & WebSockets** | [Events](docs/core/events.md) |
| **Database** | [MongoDB & Persistence](docs/core/database.md) |
| **AI Runtime** | [AI Runtime Architecture](docs/ai-runtime/architecture.md) |
| **Quickstart** | [Getting Started](docs/guides/quickstart.md) |
| **Creating Plugins** | [Plugin Guide](docs/guides/creating-plugins.md) |
| **Deployment** | [Deployment Guide](docs/guides/deployment.md) |
| **Platform Integration** | [Connector Specs](docs/platform-integration/overview.md) |


### 🏢 Core Platform Services

| Service | Purpose |
|---------|---------|
| **🔐 Identity** | Authentication, app registry, API keys, JWT tokens |
| **💳 Billing** | Stripe integration, subscriptions, usage metering |
| **🧩 Plugins** | Plugin catalog, manifests, installations |
| **📊 Insights** | KPI ingestion, analytics dashboards |
| **🔔 Notifications** | Multi-channel delivery (email, push, in-app) |


**MozaikCore = AG2 + Production Infrastructure + Event-Driven Core**

## Quick Start

### Prerequisites

- .NET 8 SDK
- Docker & Docker Compose
- Node.js 20+ (for shell)
- Python 3.11+ (for AI runtime)

### Using Docker Compose (Recommended)

```bash
# Copy environment template
cp .env.example .env

# Start all services
docker-compose up -d

# View logs
docker-compose logs -f
```

### Manual Development

```bash
# Build backend
dotnet build MozaiksCore.sln

# Run services individually
cd backend/src/Identity.API/AuthServer.Api
dotnet run
```

## Services

| Service | Description |
|---------|-------------|
| Identity API | Auth, app registry, JWT tokens |
| Billing API | Stripe, subscriptions, ledger |
| Plugins API | Plugin catalog + installations |
| Insights API | KPI analytics |
| Notification API | Email/push/in-app delivery |

## Configuration

Configure via environment variables or `.env` file:

```bash
# MongoDB
MONGODB_CONNECTION_STRING=mongodb://localhost:27017
MONGODB_DATABASE_NAME=MozaiksDB

# Auth
JWT_SECRET=your-secret-key
JWT_ISSUER=https://your-domain.com

# Stripe
STRIPE_API_KEY=sk_test_xxx
```

## Project Structure

```
mozaiks-core/
├── backend/
│   ├── BuildingBlocks/          # Shared libraries
│   ├── src/
│   │   ├── Identity.API/        # Auth + App Registry
│   │   ├── Billing.API/         # Payments + Subscriptions
│   │   ├── Plugins.API/         # Plugin catalog + installs
│   │   ├── Insights.API/        # Analytics
│   │   └── Notification.API/    # Notifications
│   └── MozaiksCore.sln
├── runtime/ai/                   # Python AI runtime
├── runtime/packages/shell/       # React web shell
├── runtime/packages/sdk/         # (Placeholder) future public SDK
├── docker-compose.yml
└── .env.example
```

## Multi-Tenancy

All entities use `appId` for tenant isolation:

```csharp
// All queries filter by appId
var users = await _collection.Find(u => u.AppId == appId).ToListAsync();
```

## API Documentation

Each service exposes Swagger UI; see docker-compose.yml for ports in your environment.

## License

MIT License - See [LICENSE](LICENSE) for details.

## Related Repositories

- [mozaiks-platform](https://github.com/BlocUnited-LLC/mozaiks-platform) - Proprietary platform services
