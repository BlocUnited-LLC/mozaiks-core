# MozaiksCore

<div align="center">

<img src="https://raw.githubusercontent.com/BlocUnited-LLC/mozaiks-core/main/runtime/packages/shell/public/mozaik_logo.svg" alt="MozaiksCore Logo" width="180"/>

**Open-source multi-tenant runtime for AI-powered applications**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Python](https://img.shields.io/badge/Python-3.11+-blue?logo=python)](https://www.python.org/)
[![.NET](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![AG2](https://img.shields.io/badge/AG2-Autogen-green)](https://github.com/ag2ai/ag2)

</div>

---

## What is MozaiksCore?

MozaiksCore is a **self-hostable application runtime** that provides:

- 🔌 **Plugin System** — Add features via isolated Python plugins
- 🤖 **AI Workflows** — Build AI agents with AG2 (Microsoft Autogen)
- 🏢 **Multi-Tenancy** — Built-in app isolation via `app_id`
- 🔐 **Auth & Billing** — JWT auth, Stripe subscriptions out of the box
- 💬 **Real-time Chat** — WebSocket streaming for AI conversations

> **Note**: This is the open-source core. BlocUnited offers a managed platform with app generation tools at [mozaiks.ai](https://mozaiks.ai), but you're welcome to self-host and build everything yourself.

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
- **MongoDB** — Database
- **Identity API** — Auth & app registry (port 5001)
- **Plugin Runtime** — Plugin execution (port 8000)
- **AI Runtime** — AG2 workflows (port 8080)

### 3. Run Frontend

```bash
cd runtime/packages/shell
npm install
npm run dev
```

Open http://localhost:5173

---

## Creating Plugins

Plugins are self-contained Python modules that add features to your app.

### Plugin Structure

```
runtime/plugins/
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

async def execute(data: dict, context: dict) -> dict:
    """
    Main entry point. Receives:
    - data: Request payload with 'action' field
    - context: Contains user_id, app_id, etc.
    """
    action = data.get("action")
    user_id = context.get("user_id")
    
    # Get a MongoDB collection (auto-scoped to your app)
    items = get_collection("my_items", context)
    
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

The plugin SDK provides multi-tenant database access:

```python
from sdk import get_collection

# Collections are automatically scoped by app_id
tasks = get_collection("tasks", context)

# All operations include app_id filter automatically
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
| `JWT_SECRET` | Secret for JWT signing | Required |
| `OPENAI_API_KEY` | OpenAI API key for AI workflows | Required for AI |
| `SKIP_AUTH` | Bypass auth for development | `false` |
| `PLATFORM_FEE_BPS` | Stripe Connect fee (basis points) | `0` |

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
