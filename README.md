# Mozaiks Core

**The open-source, self-hostable runtime for the Mozaiks platform.**

[![CI](https://github.com/YOUR_ORG/mozaiks-core/actions/workflows/ci.yml/badge.svg)](https://github.com/YOUR_ORG/mozaiks-core/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Overview

Mozaiks Core provides the essential services needed to run a Mozaiks-powered application:

- **Identity Service** - User authentication, app registry, API keys
- **Billing Service** - Stripe integration, subscriptions, revenue tracking
- **User Service** - User profiles and social features
- **Insights Service** - KPI ingestion and analytics
- **Notification Service** - Email/push notifications via SendGrid

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

| Service | Port | Description |
|---------|------|-------------|
| Identity API | 8020 | Auth, app registry, JWT tokens |
| Billing API | 8002 | Stripe, subscriptions, ledger |
| User API | 8001 | User profiles |
| Insights API | 8060 | KPI analytics |
| Notification API | 8070 | Email/push via SendGrid |

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
│   │   ├── User.API/            # User Profiles
│   │   ├── Insights.API/        # Analytics
│   │   └── Notification.API/    # Notifications
│   └── MozaiksCore.sln
├── shell/                        # React web shell
├── ai-runtime/                   # Python AI wizard runtime
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

Each service exposes Swagger UI:
- Identity: http://localhost:8020/swagger
- Billing: http://localhost:8002/swagger
- User: http://localhost:8001/swagger

## License

MIT License - See [LICENSE](LICENSE) for details.

## Related Repositories

- [mozaiks-platform](https://github.com/YOUR_ORG/mozaiks-platform) - Proprietary platform services
