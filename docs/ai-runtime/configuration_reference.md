# Configuration Reference

**Purpose:** Complete reference for all environment variables, startup options, and feature toggles in the MozaiksAI runtime.

---

## Environment Variables

### Core Platform

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `ENVIRONMENT` | string | `development` | Environment mode: `development` or `production` |
| `MONGODB_URI` | string | **Required** | MongoDB connection string (e.g., `mongodb+srv://user:pass@cluster/db`) |
| `OPENAI_API_KEY` | string | **Required** | OpenAI API key for LLM provider |
| `ANTHROPIC_API_KEY` | string | Optional | Anthropic API key (if using Claude models) |
| `AZURE_KEYVAULT_URL` | string | Optional | Azure Key Vault URL for secret management |

---

### Logging

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `LOGS_AS_JSON` | boolean | `false` | Write structured JSON lines to log files (vs. pretty text) |
| `LOGS_BASE_DIR` | string | `logs/logs` | Base directory for log files (absolute or relative to repo root) |
| `AG2_RUNTIME_LOG_FILE` | string | `logs/ag2_runtime.log` | AG2 native runtime log path (file or sqlite) |
| `CLEAR_LOGS_ON_START` | boolean | `false` | Clear log files on server startup (dev mode only) |
| `NO_COLOR` | boolean | `false` | Disable ANSI colors in console output |

**Examples:**
```powershell
# Enable JSON logging
$env:LOGS_AS_JSON = "true"

# Custom log directory
$env:LOGS_BASE_DIR = "C:\MozaiksLogs"

# Clear logs on startup (dev only)
$env:CLEAR_LOGS_ON_START = "1"
```

---

### LLM Configuration

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `DEFAULT_LLM_MODEL` | string | `gpt-4o-mini` | Default model if not specified in workflow config |
| `LLM_DEFAULT_CACHE_SEED` | integer | `None` | Hard override for LLM cache seed (bypass per-chat derivation) |
| `RANDOMIZE_DEFAULT_CACHE_SEED` | boolean | `false` | Randomize process-wide default seed (does not affect per-chat seeds) |
| `LLM_CONFIG_CACHE_TTL` | integer | `300` | Cache TTL in seconds for workflow LLM config resolution |

**Important:**
- **Never set `LLM_DEFAULT_CACHE_SEED` in production** unless you understand the reproducibility implications.
- Per-chat cache seeds (derived from `app_id:chat_id`) always override the default.

**Examples:**
```powershell
# Use GPT-4 as default model
$env:DEFAULT_LLM_MODEL = "gpt-4"

# Randomize process default seed (dev testing)
$env:RANDOMIZE_DEFAULT_CACHE_SEED = "true"

# Extend LLM config cache TTL to 10 minutes
$env:LLM_CONFIG_CACHE_TTL = "600"
```

---

### Workflow & Tools

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `CLEAR_TOOL_CACHE_ON_START` | boolean | `true` (dev) | Reload tools from manifests on startup (hot-reload) |
| `CHAT_START_IDEMPOTENCY_SEC` | integer | `15` | Idempotency window for duplicate `/start` requests (seconds) |

**Examples:**
```powershell
# Disable tool hot-reload (production)
$env:CLEAR_TOOL_CACHE_ON_START = "false"

# Extend idempotency window to 30 seconds
$env:CHAT_START_IDEMPOTENCY_SEC = "30"
```

---

### Free Trial & Billing

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `FREE_TRIAL_ENABLED` | boolean | `true` | Enable free trial token allocation for new users |
| `FREE_TRIAL_TOKENS` | integer | `10000` | Token allocation for free trial accounts |

**Note:** Token billing is platform-controlled (MozaiksPay integration). These settings only apply to internal token tracking.

**Examples:**
```powershell
# Disable free trial
$env:FREE_TRIAL_ENABLED = "false"

# Increase free trial tokens
$env:FREE_TRIAL_TOKENS = "50000"
```

---

### Transport & WebSocket

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `WS_PING_INTERVAL` | integer | `30` | WebSocket ping interval (seconds) |
| `WS_TIMEOUT` | integer | `300` | WebSocket connection timeout (seconds) |
| `MAX_PRECONNECTION_BUFFER_SIZE` | integer | `100` | Max events to buffer before WebSocket connection |

**Examples:**
```powershell
# Increase WebSocket timeout to 10 minutes
$env:WS_TIMEOUT = "600"

# Reduce ping interval to 15 seconds
$env:WS_PING_INTERVAL = "15"
```

---

### Performance & Observability

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `PERF_FLUSH_INTERVAL_SEC` | integer | `0` | Performance metrics flush interval (0 = disabled, flush on completion only) |

**Examples:**
```powershell
# Enable periodic metrics flush every 60 seconds
$env:PERF_FLUSH_INTERVAL_SEC = "60"
```

---

### Docker & Deployment

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `PORT` | integer | `8000` | HTTP server port |
| `HOST` | string | `0.0.0.0` | HTTP server bind address |
| `RELOAD` | boolean | `false` | Enable Uvicorn auto-reload (dev only) |

**Examples:**
```powershell
# Run on custom port
$env:PORT = "8080"

# Enable auto-reload for development
$env:RELOAD = "true"
```

---

## Configuration Files

### Workflow Configuration (`workflow.json`)

**Location:** `workflows/{workflow_name}/workflow.json`

**Schema:**
```json
{
  "visual_agents": ["interviewer", "planner", "architect"],
  "max_turns": 20,
  "orchestration_pattern": "auto",
  "human_input_mode": "NEVER",
  "ui_tool_display_mode": "artifact"
}
```

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `visual_agents` | array | List of agent names visible to frontend (message filtering) |
| `max_turns` | integer | Maximum agent turns before termination |
| `orchestration_pattern` | string | AG2 pattern: `auto`, `default`, `round_robin`, `random` |
| `human_input_mode` | string | AG2 input mode: `NEVER`, `ALWAYS`, `TERMINATE` |
| `ui_tool_display_mode` | string | UI tool rendering mode: `artifact`, `inline`, `modal` |

---

### Agent Configuration (`agents.json`)

**Location:** `workflows/{workflow_name}/agents.json`

**Schema:**
```json
{
  "interviewer": {
    "system_message": "You are an interviewer...",
    "llm_config": {
      "model": "gpt-4",
      "temperature": 0.7
    },
    "max_consecutive_auto_reply": 5,
    "structured_output": {
      "ui_tool_id": "user_input",
      "trigger": "completion"
    }
  }
}
```

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `system_message` | string | Agent system prompt |
| `llm_config` | object | LLM configuration (model, temperature, etc.) |
| `max_consecutive_auto_reply` | integer | Max turns before requesting user input |
| `structured_output` | object | UI tool integration config (auto-tool pattern) |

---

### Tool Manifest (`tools.json`)

**Location:** `workflows/{workflow_name}/tools.json`

**Schema:**
```json
{
  "tools": [
    {
      "name": "user_input",
      "description": "Request input from user",
      "module_path": "workflows.Generator.tools.user_input",
      "function_name": "user_input",
      "parameters": {
        "type": "object",
        "properties": {
          "prompt": {"type": "string"},
          "ui_tool_id": {"type": "string", "default": "user_input"}
        },
        "required": ["prompt"]
      }
    }
  ]
}
```

**Key Fields:**

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Tool name (callable by agents) |
| `description` | string | Tool description (for LLM understanding) |
| `module_path` | string | Python import path to tool implementation |
| `function_name` | string | Function name within module |
| `parameters` | object | JSON Schema for tool parameters |

---

## Startup Options

### Run Server

**Command:**
```powershell
python run_server.py
```

**Environment-Based Behavior:**

| Env | Console Logs | File Logs | Auto-Reload | Level |
|-----|-------------|-----------|-------------|-------|
| `development` | Pretty + Emoji | Pretty/JSON* | Optional | DEBUG |
| `production` | JSON (errors) | JSON | Disabled | INFO |

*File format controlled by `LOGS_AS_JSON` toggle.

---

### Development Mode

**Recommended Settings:**
```powershell
$env:ENVIRONMENT = "development"
$env:LOGS_AS_JSON = "false"
$env:CLEAR_TOOL_CACHE_ON_START = "true"
$env:RELOAD = "true"

python run_server.py
```

**Features:**
- Emoji-enhanced console logs
- Pretty text file logs
- Tool hot-reload on startup
- Uvicorn auto-reload on code changes
- DEBUG level logging

---

### Production Mode

**Recommended Settings:**
```powershell
$env:ENVIRONMENT = "production"
$env:LOGS_AS_JSON = "true"
$env:CLEAR_TOOL_CACHE_ON_START = "false"
$env:RELOAD = "false"
$env:PERF_FLUSH_INTERVAL_SEC = "60"

python run_server.py
```

**Features:**
- JSON-only file logs (structured)
- Minimal console output (errors only)
- No tool hot-reload (stability)
- INFO level logging
- Periodic metrics flush

---

## Docker Configuration

### docker-compose.yml

**Location:** `infra/compose/docker-compose.yml`

**Example:**
```yaml
version: "3.8"

services:
  mozaiks:
    build:
      context: ../..
      dockerfile: infra/docker/Dockerfile
    ports:
      - "8000:8000"
    environment:
      - MONGODB_URI=${MONGODB_URI}
      - OPENAI_API_KEY=${OPENAI_API_KEY}
      - ENVIRONMENT=production
      - LOGS_AS_JSON=true
      - LOGS_BASE_DIR=/app/logs
    volumes:
      - ./logs:/app/logs  # Persist logs to host
      - ./workflows:/app/workflows  # Hot-reload workflows (dev)
    restart: unless-stopped
```

**Volume Mounts:**
- `./logs:/app/logs` - Persist logs to host filesystem
- `./workflows:/app/workflows` - Enable workflow hot-reload (dev only)

---

## Feature Toggles

### Cache Seed Behavior

**Per-Chat Deterministic (Default):**
```powershell
# No env vars needed
# Cache seed automatically derived from app_id:chat_id
```

**Randomized Process Default:**
```powershell
$env:RANDOMIZE_DEFAULT_CACHE_SEED = "true"
# Process-wide default seed randomized on startup
# Per-chat seeds still deterministic
```

**Hard Override (Testing Only):**
```powershell
$env:LLM_DEFAULT_CACHE_SEED = "42"
# ALL chats use seed=42 (breaks multi-chat isolation)
# DO NOT USE IN PRODUCTION
```

---

### Visual Agents Filtering

**Enable (Default):**
```json
// workflow.json
{
  "visual_agents": ["interviewer", "planner"]
}
```

**Disable (Show All Agents):**
```json
// workflow.json
{
  "visual_agents": []  // Empty array = show all
}
```

**No Filtering (Omit Field):**
```json
// workflow.json
{
  // visual_agents not defined = show all
}
```

---

### Tool Hot-Reload

**Enable (Development):**
```powershell
$env:CLEAR_TOOL_CACHE_ON_START = "true"
python run_server.py
# Tools reloaded from manifests on every startup
```

**Disable (Production):**
```powershell
$env:CLEAR_TOOL_CACHE_ON_START = "false"
python run_server.py
# Tools loaded once, cached for process lifetime
```

---

## Validation & Debugging

### Check Active Configuration

**Python REPL:**
```python
import os

# Check environment mode
print(f"Environment: {os.getenv('ENVIRONMENT', 'development')}")

# Check MongoDB URI (redacted)
mongo_uri = os.getenv('MONGODB_URI', 'NOT_SET')
print(f"MongoDB: {mongo_uri[:20]}***")

# Check logging mode
print(f"JSON Logs: {os.getenv('LOGS_AS_JSON', 'false')}")

# Check cache seed config
print(f"Default Seed: {os.getenv('LLM_DEFAULT_CACHE_SEED', 'per-chat')}")
print(f"Randomize: {os.getenv('RANDOMIZE_DEFAULT_CACHE_SEED', 'false')}")
```

---

### Verify Workflow Configuration

**HTTP Endpoint:**
```http
GET /api/workflows/{workflow_name}/config
```

**Response:**
```json
{
  "workflow_name": "Generator",
  "visual_agents": ["interviewer", "planner"],
  "max_turns": 20,
  "orchestration_pattern": "auto"
}
```

---

### Inspect Metrics Configuration

**HTTP Endpoint:**
```http
GET /metrics/perf/aggregate
```

**Check:**
- `active_chats` - Number of tracked chat sessions
- `tracked_chats` - Total chats in memory
- Performance data availability

---

## Common Configuration Patterns

### Pattern 1: Local Development

```powershell
# .env file
ENVIRONMENT=development
MONGODB_URI=mongodb://localhost:27017/mozaiksai
OPENAI_API_KEY=sk-...
LOGS_AS_JSON=false
CLEAR_TOOL_CACHE_ON_START=true
RELOAD=true
```

**Use Case:** Rapid iteration with pretty logs and hot-reload.

---

### Pattern 2: Docker Development

```yaml
# docker-compose.yml
environment:
  - ENVIRONMENT=development
  - MONGODB_URI=mongodb://mongo:27017/mozaiksai
  - OPENAI_API_KEY=${OPENAI_API_KEY}
  - LOGS_AS_JSON=false
  - CLEAR_TOOL_CACHE_ON_START=true
volumes:
  - ./workflows:/app/workflows  # Hot-reload workflows
  - ./logs:/app/logs  # Inspect logs on host
```

**Use Case:** Containerized dev environment with file mounting.

---

### Pattern 3: Production Deployment

```yaml
# docker-compose.yml
environment:
  - ENVIRONMENT=production
  - MONGODB_URI=${MONGODB_URI}  # From secrets
  - OPENAI_API_KEY=${OPENAI_API_KEY}  # From secrets
  - LOGS_AS_JSON=true
  - CLEAR_TOOL_CACHE_ON_START=false
  - PERF_FLUSH_INTERVAL_SEC=60
volumes:
  - /var/log/mozaiks:/app/logs  # Host log directory
restart: unless-stopped
```

**Use Case:** Production deployment with structured logging and stability.

---

## Troubleshooting

### Issue: Configuration not applied

**Check:**
1. Environment variables set in correct shell session
2. Server restarted after env var changes
3. `.env` file loaded (if using)

**Debug:**
```python
import os
print(os.environ.get('LOGS_AS_JSON'))  # Should print 'true' or 'false'
```

---

### Issue: Logs not in expected format

**Check:**
1. `LOGS_AS_JSON` environment variable
2. `ENVIRONMENT` mode (production forces JSON on console)
3. File path correct: `LOGS_BASE_DIR`

**Debug:**
```powershell
# Check log file directly
Get-Content logs\logs\mozaiks.log -Tail 10
```

---

### Issue: Cache seed not deterministic

**Check:**
1. `LLM_DEFAULT_CACHE_SEED` not set (removes determinism)
2. `RANDOMIZE_DEFAULT_CACHE_SEED` not enabled
3. Per-chat seed stored in MongoDB `cache_seed` field

**Debug:**
```javascript
// MongoDB shell
db.chat_sessions.findOne({"_id": "chat_abc123"}, {"cache_seed": 1})
```

---

## Next Steps

- **[Runtime Overview](runtime_overview.md)** - High-level runtime architecture
- **[Observability](observability.md)** - Metrics, logging, monitoring
- **[Deployment Guide](../operations/deployment.md)** - Production deployment patterns
- **[Environment Variables Reference](../reference/environment_variables.md)** - Complete env var listing

---

**Questions?** See [Troubleshooting Guide](../operations/troubleshooting.md) or open a GitHub issue.
