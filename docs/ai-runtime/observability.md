# Observability Deep Dive

**Purpose:** Document MozaiksAI's observability stack—metrics collection, performance tracking, logging configuration, and monitoring endpoints.

---

## Observability Architecture

**Components:**

1. **PerformanceManager** (`core/observability/performance_manager.py`)
   - In-memory metrics tracking
   - HTTP endpoints for metrics aggregation
   - Custom metrics for chat sessions

2. **AG2RuntimeLogger** (`core/observability/ag2_runtime_logger.py`)
   - Shim around AG2's native file/sqlite logger
   - Capture start/end timestamps, token usage
   - Enable AG2 built-in logging without custom persistence

3. **Logging Configuration** (`logs/logging_config.py`)
   - Unified structured logging with emoji-enhanced console output
   - File sinks with JSON/pretty text toggle
   - Secret redaction and safe_extra helpers

```mermaid
graph TD
    A[Orchestration Engine] -->|record_agent_turn| B[PerformanceManager]
    A -->|AG2 Events| C[AG2RuntimeLogger]
    B -->|In-Memory State| D[ChatPerfState]
    C -->|File/SQLite| E[AG2 Native Logs]
    B -->|HTTP Endpoints| F[Metrics Endpoints]
    A -->|Structured Logs| G[LoggingConfig]
    G -->|File Sink| H[mozaiks.log]
    G -->|Console| I[Emoji-Enhanced Output]
```

---

## PerformanceManager

**Module:** `core/observability/performance_manager.py`

### Purpose

Track real-time metrics for chat sessions:
- Agent turns and tool calls
- Token usage (prompt, completion)
- Cost (USD)
- Errors and duration
- Last turn performance

**Singleton Access:**
```python
from core.observability.performance_manager import get_performance_manager

perf_mgr = get_performance_manager()
```

---

### In-Memory State (ChatPerfState)

**Dataclass:**
```python
@dataclass
class ChatPerfState:
    chat_id: str
    app_id: str
    workflow_name: str
    user_id: str
    started_at: datetime
    ended_at: Optional[datetime] = None
    agent_turns: int = 0
    tool_calls: int = 0
    errors: int = 0
    last_turn_duration_sec: Optional[float] = None
    total_prompt_tokens: int = 0
    total_completion_tokens: int = 0
    total_cost: float = 0.0
```

**State Lifecycle:**
1. **Start:** Created when workflow execution begins
2. **Update:** Incremented as agents turn, tools execute, tokens consumed
3. **End:** Marked complete when workflow finishes
4. **Persist:** Flushed to MongoDB `workflow_stats_{app}_{workflow}` collection

---

### API Reference

#### Initialize
```python
await perf_mgr.initialize()
```

**Side Effects:**
- Starts periodic flush task (if `flush_interval_sec > 0`)
- Sets `initialized = True`

---

#### Record Workflow Start
```python
await perf_mgr.record_workflow_start(
    chat_id="chat_abc123",
    app_id="acme_corp",
    workflow_name="Generator",
    user_id="user_456"
)
```

**Creates:**
- In-memory `ChatPerfState` instance
- MongoDB `chat_sessions` document (via `AG2PersistenceManager`)

---

#### Record Agent Turn
```python
await perf_mgr.record_agent_turn(
    chat_id="chat_abc123",
    agent_name="planner",
    duration_sec=2.5,
    model="gpt-4",
    prompt_tokens=1500,
    completion_tokens=800,
    cost=0.05
)
```

**Updates:**
- `agent_turns += 1`
- `last_turn_duration_sec = duration_sec`
- Emits structured log: `perf_logger.info("agent_turn", ...)`
- Delegates usage delta to `record_usage_delta()`

---

#### Record Usage Delta
```python
await perf_mgr.record_usage_delta(
    chat_id="chat_abc123",
    agent_name="planner",
    model="gpt-4",
    prompt_tokens=1500,
    completion_tokens=800,
    cost=0.05,
    duration_sec=2.5
)
```

**Updates:**
- `total_prompt_tokens += prompt_tokens`
- `total_completion_tokens += completion_tokens`
- `total_cost += cost`

---

#### Record Tool Call
```python
await perf_mgr.record_tool_call(
    chat_id="chat_abc123",
    tool_name="user_input",
    success=True
)
```

**Updates:**
- `tool_calls += 1` (if success)
- `errors += 1` (if not success)

---

#### Record Workflow End
```python
await perf_mgr.record_workflow_end(
    chat_id="chat_abc123",
    success=True
)
```

**Updates:**
- `ended_at = datetime.now(UTC)`
- Flushes metrics to MongoDB
- Marks chat session completed (via `AG2PersistenceManager`)

---

#### Snapshot Chat
```python
snapshot = await perf_mgr.snapshot_chat("chat_abc123")

# Returns:
# {
#     "chat_id": "chat_abc123",
#     "app_id": "acme_corp",
#     "workflow_name": "Generator",
#     "user_id": "user_456",
#     "started_at": "2025-10-02T10:00:00Z",
#     "ended_at": "2025-10-02T10:15:00Z",
#     "runtime_sec": 900.0,
#     "agent_turns": 15,
#     "tool_calls": 8,
#     "errors": 0,
#     "last_turn_duration_sec": 2.5,
#     "prompt_tokens": 22500,
#     "completion_tokens": 12000,
#     "cost": 0.75
# }
```

---

#### Snapshot All
```python
snapshots = await perf_mgr.snapshot_all()

# Returns: List[Dict[str, Any]]
```

---

#### Aggregate Metrics
```python
aggregate = await perf_mgr.aggregate()

# Returns:
# {
#     "active_chats": 3,
#     "tracked_chats": 10,
#     "total_agent_turns": 150,
#     "total_tool_calls": 80,
#     "total_errors": 2,
#     "total_prompt_tokens": 225000,
#     "total_completion_tokens": 120000,
#     "total_cost": 7.50,
#     "chats": [...]  # Array of chat snapshots
# }
```

---

### HTTP Endpoints

#### Aggregate Metrics
```http
GET /metrics/perf/aggregate
```

**Response:**
```json
{
  "active_chats": 3,
  "tracked_chats": 10,
  "total_agent_turns": 150,
  "total_tool_calls": 80,
  "total_errors": 2,
  "total_prompt_tokens": 225000,
  "total_completion_tokens": 120000,
  "total_cost": 7.50,
  "chats": [
    {
      "chat_id": "chat_abc123",
      "workflow_name": "Generator",
      "runtime_sec": 900.0,
      "agent_turns": 15,
      "cost": 0.75
    }
  ]
}
```

---

#### All Chat Snapshots
```http
GET /metrics/perf/chats
```

**Response:** Array of chat snapshots (same structure as `snapshot_all()`)

---

#### Single Chat Snapshot
```http
GET /metrics/perf/chats/{chat_id}
```

**Response:**
```json
{
  "chat_id": "chat_abc123",
  "app_id": "acme_corp",
  "workflow_name": "Generator",
  "started_at": "2025-10-02T10:00:00Z",
  "runtime_sec": 900.0,
  "agent_turns": 15,
  "cost": 0.75
}
```

---

## AG2RuntimeLogger

**Module:** `core/observability/ag2_runtime_logger.py`

### Purpose

Enable AG2's native file/sqlite logging without custom persistence implementation. Provides:
- Start/end timestamps for agent runs
- Token usage tracking per agent
- Runtime duration metrics
- SQLite database for queryable logs

**Integration:**
```python
from core.observability.ag2_runtime_logger import ag2_logging_session

async with ag2_logging_session(
    session_id=chat_id,
    config={"sqlite_db_path": "logs/ag2_runtime.db"}
):
    # Run AG2 orchestration
    await run_workflow_orchestration(...)
```

**Log Files:**
- SQLite: `logs/ag2_runtime.db` (queryable with `sqlite3`)
- JSON: `logs/ag2_runtime.log` (append-only JSON lines)

**Environment Variable:**
```bash
export AG2_RUNTIME_LOG_FILE=/path/to/custom.log
```

---

## Logging Configuration

**Module:** `logs/logging_config.py`

### Log Format Modes

#### Pretty Text (Default)
```
2025-10-02 12:34:56 💬 INFO [orchestration] shared_app.py:123 start_chat
  Workflow started workflow=Generator chat_id=chat_abc123
```

#### JSON Lines (Production)
```json
{
  "ts": "2025-10-02T12:34:56.789Z",
  "level": "INFO",
  "logger": "orchestration",
  "msg": "Workflow started",
  "mod": "shared_app",
  "fn": "start_chat",
  "line": 123,
  "emoji": "💬",
  "extra": {
    "workflow": "Generator",
    "chat_id": "chat_abc123"
  }
}
```

**Toggle via Environment:**
```bash
export LOGS_AS_JSON=true
python run_server.py
```

---

### Log Files

**Location:** `logs/logs/` (override via `LOGS_BASE_DIR`)

**Files:**
- `mozaiks.log` - Operational workflow/runtime logs (no chat transcripts)

**Rotation:** Not implemented (future enhancement: logrotate integration)

---

### Emoji Legend

| Emoji | Category | Examples |
|-------|----------|----------|
| 💬 | Chat/Conversation | Agent messages, user input |
| ⏱️ | Performance | Duration tracking, benchmarks |
| 🪙 | Tokens | Token usage, cost updates |
| 🔌 | Transport | WebSocket connect/disconnect |
| 🧩 | Workflow | Workflow start/end, status |
| 🎯 | Events | Event dispatcher operations |
| 🐛 | Debug | Verbose debugging output |
| ℹ️ | Info | General information |
| ⚠️ | Warning | Warnings, deprecations |
| ❌ | Error | Errors, failures |
| 🚨 | Critical | Critical failures |

---

### Secret Redaction

**Purpose:** Prevent API keys, passwords, and tokens from appearing in logs.

**Redaction Patterns:**
```python
# Environment variables
OPENAI_API_KEY=sk-proj1234...abcd  →  sk-p***abcd

# MongoDB connection strings
mongodb+srv://user:pass@cluster  →  mongodb+srv://user:***@cluster

# Bearer tokens
Authorization: Bearer abc123...xyz  →  Authorization: Bearer abc1***xyz

# Generic key/value pairs
{"api_key": "secret123"}  →  {"api_key": "***"}
```

**Implementation:**
```python
# Automatic in structured logging
logger.info("API call", extra={"api_key": "sk-abc123"})
# Logged as: {"extra": {"api_key": "***"}}

# Manual redaction
from logs.logging_config import _maybe_redact_mapping
safe_data = _maybe_redact_mapping({"api_key": "sk-abc123"})
```

---

### Structured Logging API

#### Get Logger
```python
from logs.logging_config import get_workflow_logger

logger = get_workflow_logger("my_module")
```

**Logger Hierarchy:**
- `chat.*` - Chat/conversation logs (emoji: 💬)
- `performance.*` - Performance metrics (emoji: ⏱️)
- `token.*` - Token usage logs (emoji: 🪙)
- `orchestration` - Workflow orchestration (emoji: 🧩)
- `event_dispatcher` - Event routing (emoji: 🎯)

---

#### Log with Extra Fields
```python
logger.info(
    "Agent completed turn",
    extra={
        "agent": "planner",
        "duration_sec": 2.5,
        "tokens": 2300,
        "cost": 0.05
    }
)
```

**Output (JSON):**
```json
{
  "msg": "Agent completed turn",
  "extra": {
    "agent": "planner",
    "duration_sec": 2.5,
    "tokens": 2300,
    "cost": 0.05
  }
}
```

---

#### Performance Timing
```python
from logs.logging_config import log_timing

with log_timing(logger, "expensive_operation"):
    # Do work
    result = await expensive_function()

# Logs: "expensive_operation completed in 2.345s"
```

---

### Setup Functions

#### Development Logging
```python
from logs.logging_config import setup_development_logging

setup_development_logging()
```

**Configuration:**
- Console: Pretty emoji-enhanced output with colors
- File: `mozaiks.log` (pretty text or JSON based on `LOGS_AS_JSON`)
- Level: `DEBUG` for all loggers

---

#### Production Logging
```python
from logs.logging_config import setup_production_logging

setup_production_logging()
```

**Configuration:**
- Console: Minimal JSON output (errors only)
- File: `mozaiks.log` (JSON lines if `LOGS_AS_JSON=true`)
- Level: `INFO` for application, `WARNING` for libraries

---

## Performance Characteristics

| Operation | Latency | Notes |
|-----------|---------|-------|
| Record agent turn | <5ms | In-memory counter update |
| Record usage delta | <5ms | In-memory aggregation |
| Snapshot chat | <1ms | In-memory state access |
| Aggregate all chats | <10ms | Iterates all in-memory states |
| Flush to MongoDB | 20-50ms | Async write, non-blocking |
| Log write (file) | <5ms | Async file handler |
| Log write (JSON) | <10ms | JSON serialization + file write |

---

## Monitoring & Alerts

### Grafana Dashboard (Example)

**Metrics to Track:**
1. **Active Chats:** `mozaiks_active_chats`
2. **Token Usage:** `mozaiks_chat_prompt_tokens_total`, `mozaiks_chat_completion_tokens_total`
3. **Cost:** `mozaiks_chat_cost_total`
4. **Agent Turns:** `mozaiks_chat_agent_turns_total`
5. **Tool Calls:** `mozaiks_chat_tool_calls_total`
6. **Errors:** `mozaiks_chat_errors_total`

**Sample PromQL Queries:**
```promql
# Total cost across all chats
sum(mozaiks_chat_cost_total)

# Average tokens per chat
avg(mozaiks_chat_prompt_tokens_total + mozaiks_chat_completion_tokens_total)

# Error rate
rate(mozaiks_chat_errors_total[5m])
```

---

### Alert Rules (Example)

Custom alerting rules can be configured based on metrics endpoints:

```yaml
# Example alerting configuration
alerts:
groups:
  - name: mozaiks_alerts
    rules:
      - alert: HighCostChat
        expr: mozaiks_chat_cost_total > 1.0
        for: 1m
        annotations:
          summary: "Chat {{ $labels.chat_id }} exceeded $1 cost"
      
      - alert: ChatStalled
        expr: time() - mozaiks_chat_last_update_time > 600
        for: 5m
        annotations:
          summary: "Chat {{ $labels.chat_id }} stalled for 10 minutes"
```

---

## Troubleshooting

### Issue: Metrics not updating

**Check:**
1. PerformanceManager initialized: `await perf_mgr.initialize()`
2. Workflow calling `record_agent_turn()`, `record_usage_delta()`
3. In-memory state created: `await perf_mgr.record_workflow_start()`

**Debug:**
```python
# Check tracked chats
snapshots = await perf_mgr.snapshot_all()
print(f"Tracking {len(snapshots)} chats")
```

---

### Issue: Logs not written to file

**Check:**
1. `LOGS_BASE_DIR` environment variable (if custom path)
2. File permissions on `logs/logs/` directory
3. Disk space available

**Debug:**
```python
from logs.logging_config import LOGS_DIR, MAIN_LOG_FILE
print(f"Logs directory: {LOGS_DIR}")
print(f"Main log file: {MAIN_LOG_FILE}")
print(f"Exists: {MAIN_LOG_FILE.exists()}")
```

---

### Issue: Secrets appearing in logs

**Check:**
1. Using `extra` kwarg in log calls (automatically redacted)
2. Not logging `os.environ` directly
3. Redaction patterns cover secret format

**Fix:**
```python
# ❌ Bad (secret exposed)
logger.info(f"Using API key: {api_key}")

# ✅ Good (secret redacted)
logger.info("Using API key", extra={"api_key": api_key})
```

---

## Configuration Reference

**Environment Variables:**

| Variable | Purpose | Default |
|----------|---------|---------|
| `LOGS_AS_JSON` | Enable JSON logging | `false` |
| `LOGS_BASE_DIR` | Custom log directory | `logs/logs` |
| `AG2_RUNTIME_LOG_FILE` | AG2 runtime log path | `logs/ag2_runtime.log` |
| `ENVIRONMENT` | `development` or `production` | `development` |

**PerformanceConfig:**
```python
from core.observability.performance_manager import PerformanceConfig

config = PerformanceConfig(
    flush_interval_sec=60,  # Flush to MongoDB every 60s (0 = disabled)
    enabled=True  # Enable performance tracking
)
```

---

## Next Steps

- **[Runtime Overview](runtime_overview.md)** - High-level runtime architecture
- **[Event Pipeline](event_pipeline.md)** - Event routing and handlers
- **[Configuration Reference](configuration_reference.md)** - All environment variables
- **[Operations: Monitoring](../operations/metrics_and_monitoring.md)** - Production monitoring setup

---

**Questions?** See [Troubleshooting Guide](../operations/troubleshooting.md) or [Logging & Debugging](../operations/logging_and_debugging.md).
