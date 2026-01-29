# MozaiksCore

<div align="center">

<img src="./packages/frontend/shell/public/mozaik_logo.svg" alt="MozaiksCore Logo" width="180"/>

**Open-source multi-tenant runtime for AI-powered applications**

[![Version](https://img.shields.io/badge/version-1.0.0-blue)](CHANGELOG.md)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Python](https://img.shields.io/badge/Python-3.11+-blue?logo=python)](https://www.python.org/)
[![.NET](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![AG2](https://img.shields.io/badge/AG2-Autogen-green)](https://github.com/ag2ai/ag2)

</div>

> **Note**: This is the open-source core. BlocUnited offers a managed platform with app generation tools at [mozaiks.ai](https://mozaiks.ai), but you're welcome to self-host and build everything yourself.

---

## Mozaiks Core

Mozaiks Core is a self-hostable application runtime for building AI-driven, multi-tenant web applications with native real-time interfaces, authentication, and optional billing.

Instead of wiring together UI, auth, subscriptions, and agent infrastructure from scratch, Mozaiks Core provides these primitives out of the box — so teams can focus on application logic and agentic workflows.

Applications are designed through a config-first, stub-driven model: declare behavior in YAML/JSON where supported, then implement tools and integrations in lightweight Python/JavaScript stubs.

## 🚀 What Mozaiks Core Provides Natively

- Multi-tenant runtime with tenant isolation (app_id namespaces)
- Real-time web interfaces (chat + streaming UI)
- Authentication and authorization (JWT, OIDC)
- Subscription billing integrations (Stripe, optional)
- AI runtime and orchestration layer (AG2 / Autogen)
- Plugin system for isolated Python extensions

## 🧠 How You Build Applications

Mozaiks Core follows a config-first, stub-driven development model:

| Layer | Purpose |
|---|---|
| YAML / JSON | Define agents, workflows, permissions, UI wiring (where supported) |
| Python stubs | Tools, actions, backend integrations |
| JS stubs | Frontend behaviors and widgets (when needed) |

This approach enables complex agentic applications with minimal boilerplate while remaining fully extensible.

## ⚠️ OSS Project Status

Mozaiks Core is early-stage and under active development. I’m building it as a solo developer, and open sourcing the core felt like the right way to invite feedback, collaboration, and collective improvement as it grows.

---

## 🎨 See It In Action

<div align="center">

### 🔀 Dual-Mode Interface

| Workflow Mode | Ask Mode |
|:---:|:---:|
| ![Workflow Mode](./docs/assets/ArtifactLayout.png) | ![Ask Mode](./docs/assets/AskMozaiks.png) |
| *Chat + Artifact split view* | *Full chat with history sidebar* |

---

### 💬 Embeddable Floating Widget

<div align="center">

https://github.com/user-attachments/assets/32bc7ec8-f550-42f7-b287-3b015c5df235

*Drop a floating assistant anywhere in your app — click the button to expand/collapse the chat interface*

</div>

</div>

---

## v1.0.0 Runtime Contract

MozaiksCore v1.0.0 implements a stable runtime contract:

| Capability | Endpoint/Feature |
|------------|------------------|
| Plugin Execution | `POST /api/execute/{plugin_name}` |
| Plugin Discovery | `GET /api/plugins` |
| Health Check | `GET /health` (includes `plugins_loaded`) |
| Runtime Version | `X-Mozaiks-Runtime-Version: 1.0.0` header |
| Plugin Timeout | `MOZAIKS_PLUGIN_TIMEOUT_SECONDS` (default: 30s) |

### Auth Modes

| Mode | Env Var | Description |
|------|---------|-------------|
| **External** | `MOZAIKS_AUTH_MODE=external` | OIDC/JWKS validation (default) |
| **Local** | `MOZAIKS_AUTH_MODE=local` | HS256 JWT with `JWT_SECRET` |
| **Platform** | `MOZAIKS_AUTH_MODE=platform` | BlocUnited platform integration |

### Plugin Execution Context

Plugins receive server-injected context that cannot be overridden by clients:

```python
async def execute(data: dict) -> dict:
    app_id = data["app_id"]       # Execution namespace
    user_id = data["user_id"]     # From JWT sub claim
    user_jwt = data.get("user_jwt")  # Bearer token (for service calls)
    context = data["_context"]    # Full context object
```

📚 **Full contract**: [docs/contracts/runtime-platform-contract-v1.md](docs/contracts/runtime-platform-contract-v1.md)

---

## Quick Start

### Prerequisites

- Docker & Docker Compose
- Python 3.11+
- Node.js 20+
- .NET 8 SDK (for backend services)

### 1. Clone & Configure

```bash
git clone https://github.com/BlocUnited-LLC/mozaiks-core.git
cd mozaiks-core
cp .env.example .env
```

Edit `.env` with your settings:

```bash
MONGODB_URI=mongodb://localhost:27017/MozaiksDB
JWT_SECRET=your-secret-key
OPENAI_API_KEY=sk-your-key  # For AI workflows
```

### 2. Start Services

```bash
docker-compose up -d
```

This starts:
- **MongoDB** — Database (port 27017)
- **Redis** — Cache (port 6379)
- **RabbitMQ** — Message broker (ports 5672/15672)
- **Keycloak** — Local OIDC (port 8080)
- **Identity API** — Auth & app registry (port 8020)
- **Billing API** — Payments (port 8002)
- **Insights API** — Telemetry (port 8060)
- **Notification API** — Messaging (port 8007)
- **AI Runtime** — AG2 workflows (port 8000)
- **Plugin Runtime Host** — Plugin execution (port 8090)

### 3. Run Frontend

```bash
cd runtime/packages/shell
npm install
npm start
```

Open http://localhost:3000

---

## CLI (optional)

Mozaiks-core ships a small developer CLI for scaffolding and environment checks.

```bash
cd runtime/ai
python -m cli.main --help
```

Common commands:
- `python -m cli.main doctor`
- `python -m cli.main db --check-db`
- `python -m cli.main new plugin todo`
- `python -m cli.main new workflow assistant`

📚 **Full guide**: [docs/guides/cli.md](docs/guides/cli.md)

---

## Creating Plugins

Plugins are self-contained Python modules that add features to your app.

### Plugin Structure

```
runtime/ai/plugins/
└── my_plugin/
    ├── __init__.py
    ├── manifest.json
    └── logic.py
```

### manifest.json

```json
{
  "name": "my_plugin",
  "version": "1.0.0",
  "display_name": "My Plugin",
  "description": "Does something useful",
  "entry_point": "logic.execute"
}
```

### logic.py

```python
from sdk import get_collection

async def execute(data: dict) -> dict:
    """
    Main entry point. Receives:
    - data: Request payload with 'action' and user context
    """
    action = data.get("action")
    user_id = data.get("user_id")  # Injected by runtime
    
    # Get a MongoDB collection
    items = get_collection("my_items")
    
    if action == "list":
        results = await items.find({"user_id": user_id}).to_list(100)
        return {"items": results}
    
    elif action == "create":
        doc = {"user_id": user_id, "title": data.get("title")}
        result = await items.insert_one(doc)
        return {"id": str(result.inserted_id)}
    
    return {"error": f"Unknown action: {action}"}
```

### Using the SDK

The plugin SDK provides easy MongoDB access:

```python
from sdk import get_collection

tasks = get_collection("tasks")

await tasks.find({"status": "pending"}).to_list(10)
await tasks.insert_one({"title": "New task"})
await tasks.update_one({"_id": id}, {"$set": {"done": True}})
```

📚 **Full guide**: [docs/guides/creating-plugins.md](docs/guides/creating-plugins.md)

---

## Creating AI Workflows

AI workflows let you build conversational agents powered by AG2 (Autogen).

### Workflow Structure

```
runtime/ai/workflows/
└── my_assistant/
    ├── __init__.py
    ├── workflow.py
    └── tools.py
```

### workflow.py

```python
from core.workflow.base import BaseWorkflow, WorkflowConfig

class MyAssistantWorkflow(BaseWorkflow):
    
    @classmethod
    def get_config(cls) -> WorkflowConfig:
        return WorkflowConfig(
            name="my_assistant",
            display_name="My Assistant",
            description="Helpful AI assistant",
            model="gpt-4o-mini",
            system_prompt="""You are a helpful assistant.
            Be concise and friendly.""",
            tools=["search_items", "create_item"],
        )
```

### tools.py

```python
from core.workflow.tools import tool

@tool
async def search_items(query: str, context: dict) -> list:
    """Search for items matching the query."""
    from sdk import get_collection
    items = get_collection("items", context)
    return await items.find({"$text": {"$search": query}}).to_list(10)

@tool
async def create_item(title: str, context: dict) -> dict:
    """Create a new item."""
    from sdk import get_collection
    items = get_collection("items", context)
    result = await items.insert_one({
        "title": title,
        "user_id": context["user_id"]
    })
    return {"id": str(result.inserted_id), "title": title}
```

### How It Works

1. User sends message via WebSocket
2. AI Runtime routes to your workflow
3. AG2 agent processes with your tools
4. Responses stream back in real-time

📚 **Full guide**: [docs/guides/creating-workflows.md](docs/guides/creating-workflows.md)

---

## Project Structure

```
mozaiks-core/
├── backend/                      # .NET microservices
│   └── src/
│       ├── Identity.API/         # Auth, JWT, app registry
│       ├── Billing.API/          # Stripe, subscriptions
│       ├── Plugins.API/          # Plugin catalog
│       └── Notification.API/     # Email, push, in-app
├── runtime/
│   ├── ai/                       # Python AI runtime (AG2)
│   ├── plugin-host/              # Plugin execution service
│   └── packages/
│       └── shell/                # React frontend
├── docs/                         # Documentation
├── docker-compose.yml
└── .env.example
```

---

## Development Mode

For local development without full auth:

```bash
# In docker-compose.yml or .env
SKIP_AUTH=true
```

This injects a mock user context so you can test plugins and workflows without setting up identity services.

---

## Configuration

| Variable | Description | Default |
|----------|-------------|---------|
| `MONGODB_URI` | MongoDB connection string | `mongodb://localhost:27017` |
| `JWT_SECRET` | Secret for JWT signing (local auth mode) | Required for local |
| `OPENAI_API_KEY` | OpenAI API key for AI workflows | Required for AI |
| `MOZAIKS_AUTH_MODE` | Auth mode: `external`, `local`, `platform` | `external` |
| `MOZAIKS_PLUGIN_TIMEOUT_SECONDS` | Plugin execution timeout | `30` |
| `SKIP_AUTH` | Bypass auth for development | `false` |
| `APP_ID` | Application namespace identifier | `default` |
| `APP_TIER` | Subscription tier: `free`, `pro`, `enterprise` | `free` |

---

## Documentation

| Topic | Link |
|-------|------|
| Creating Plugins | [docs/guides/creating-plugins.md](docs/guides/creating-plugins.md) |
| Creating AI Workflows | [docs/guides/creating-workflows.md](docs/guides/creating-workflows.md) |
| Architecture | [docs/core/architecture.md](docs/core/architecture.md) |
| Authentication | [docs/core/authentication.md](docs/core/authentication.md) |
| Deployment | [docs/guides/deployment.md](docs/guides/deployment.md) |

---

## License

MIT License — See [LICENSE](LICENSE) for details.

---

<div align="center">

**Built by [BlocUnited](https://blocunited.com)** · [mozaiks.ai](https://mozaiks.ai)

</div>
