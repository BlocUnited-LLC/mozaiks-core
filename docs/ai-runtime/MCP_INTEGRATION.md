# MCP Integration Guide

## Overview

This document describes the integration of Model Context Protocol (MCP) into the MozaiksAI runtime. MCP provides a standardized interface for agents to access third-party APIs (Slack, Google Sheets, etc.) without requiring custom API wrapper code for each integration.

**Key Principle:** MCP is an **optimization for third-party API access**, not an architecture replacement. The core AG2 runtime, orchestration layer, and UI tools remain unchanged.

Decision Criteria:
Adopt MCP if: MCP tool "just works" with minimal debugging, credential flow is smooth, community MCP servers cover 60%+ of common integrations
Defer MCP if: MCP requires heavy SDK customization, credential handling is fragile, custom stubs are faster to maintain

---

## What MCP Is (and Isn't)

### ✅ What MCP Provides

- **Standardized protocol** for LLM-to-API communication
- **Pre-built servers** for common integrations (Slack, Google, GitHub, etc.)
- **Runtime-configurable** tool loading (add/remove integrations without code changes)
- **Community ecosystem** of MCP servers

### ❌ What MCP Does NOT Provide

- **Not an orchestration layer** — AG2 GroupChat still coordinates agents
- **Not a credential manager** — API keys still required and collected via `AgentAPIKeyInput`
- **Not a UI framework** — ChatUI and `use_ui_tool` primitive unchanged
- **Not business logic** — Custom operations still need Python stubs

---

## Architecture: Composite Stub Pattern

### The Problem with Agent Orchestration

**Anti-Pattern (Expensive):**
```
Agent decides: send_message → $0.002 + 2s latency
Agent sees error, decides: retry → $0.002 + 2s latency
Agent decides: retry again → $0.002 + 2s latency
Agent decides: verify_delivery → $0.002 + 2s latency
---
Total: 4 LLM calls, ~$0.008, ~8 seconds
```

### The Solution: Composite Stubs

**Recommended Pattern (Efficient):**
```
Agent decides: notify_support_team → $0.002 + 2s latency
  ↳ Python handles: send → retry loop → verify → log → fallback
---
Total: 1 LLM call, $0.002, ~2 seconds
```

**Cost reduction: 75%**  
**Latency reduction: 75%**

### Composite Stub Architecture

```
┌─────────────────────────────────────────────────────────────┐
│ AG2 Agent (Single Tool Call Decision)                       │
│ agent.call_tool("notify_support_team", ...)                │
└─────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│ Composite Stub (Python: notify_support_team.py)            │
│ ┌─────────────────────────────────────────────────────┐   │
│ │ 1. Deterministic logic (channel selection)          │   │
│ │ 2. MCP client initialization (lazy-loaded)          │   │
│ │ 3. Retry loop (exponential backoff)                 │   │
│ │ 4. MCP tool call (send_message)                     │   │
│ │ 5. Verification (get_message status)                │   │
│ │ 6. Fallback (email via second MCP server)           │   │
│ │ 7. Logging (runtime_logging)                        │   │
│ └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│ MCPClientManager (Per-Chat Session Management)             │
│ - Lazy initialization of MCP servers                        │
│ - Credential injection from context_variables               │
│ - Connection pooling (one client per chat_id:server_id)     │
└─────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│ MCP Server (External Process)                               │
│ - Protocol handler (stdin/stdout communication)             │
│ - Third-party API wrapper (Slack SDK, Google APIs, etc.)    │
│ - Authenticated with credentials from context_variables     │
└─────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│ Third-Party API (Slack, Google Sheets, Sendgrid, etc.)     │
└─────────────────────────────────────────────────────────────┘
```

---

## Integration Points

### 1. Generator Workflow Pipeline

#### Current Pipeline (Pre-MCP)

```
ActionPlanArchitect
  ↓ (creates ActionPlan with integrations list)
collect_api_keys.py
  ↓ (collects API credentials via AgentAPIKeyInput)
ToolsManagerAgent
  ↓ (generates tools.json with tool declarations)
UIFileGenerator
  ↓ (generates UI_Tools: .py + .js for user interaction)
AgentToolsFileGenerator
  ↓ (generates Agent_Tools: .py API wrappers)
```

#### Updated Pipeline (With MCP)

```
ActionPlanArchitect
  ↓ (creates ActionPlan with integrations list) ✅ UNCHANGED
collect_api_keys.py
  ↓ (collects API credentials) ✅ UNCHANGED (MCP still needs credentials!)
ToolsManagerAgent
  ↓ (generates tools.json with "uses_mcp" metadata) 🔧 MINOR CHANGE
UIFileGenerator
  ↓ (generates UI_Tools) ✅ UNCHANGED
AgentToolsFileGenerator
  ↓ (generates composite stubs using MCP templates) 🔧 TEMPLATE CHANGE
```

**Summary:** 80% of pipeline unchanged. Only `ToolsManagerAgent` (metadata) and `AgentToolsFileGenerator` (templates) affected.

---

### 2. MCP Registry (New Component)

**Location:** `core/mcp/registry.py`

**Purpose:** Authoritative source of available MCP servers for both Generator agents and runtime.

**Structure:**

```python
@dataclass
class MCPServerSpec:
    server_id: str              # "slack", "google_sheets"
    display_name: str           # "Slack", "GoogleSheets"
    package: str                # "@modelcontextprotocol/server-slack"
    capabilities: List[str]     # ["send_message", "list_channels"]
    category: str               # "communication", "storage", "analytics"
    requires_credentials: bool
    credential_env_vars: List[str]  # ["SLACK_BOT_TOKEN"]
    description: str            # For ActionPlanArchitect decision-making

MCP_REGISTRY: Dict[str, MCPServerSpec] = {
    "slack": MCPServerSpec(...),
    "google_sheets": MCPServerSpec(...),
    # ...
}
```

**Integration Points:**

- **ActionPlanArchitect:** Reads registry to know which integrations have MCP support
- **ToolsManagerAgent:** Checks `is_mcp_available(integration_name)` when generating tools.json
- **AgentToolsFileGenerator:** Uses registry metadata to generate composite stubs
- **Runtime (MCPClientManager):** Uses registry to initialize MCP servers

---

### 3. ToolsManagerAgent Updates

**File:** `workflows/Generator/agents.json` → `ToolsManagerAgent` system message

**Changes:**

#### Add to [CONTEXT] Section:

```markdown
[MCP INTEGRATION HANDLING]
- Check core.mcp.registry to determine if integration has MCP server support.
- MCP-eligible integrations:
  * Do NOT generate traditional Agent_Tool stub
  * Emit composite stub entry with "uses_mcp" metadata
  * Example: {
      "agent": "NotifierAgent",
      "file": "notify_support_team.py",
      "function": "notify_support_team",
      "tool_type": "Agent_Tool",
      "uses_mcp": ["slack", "sendgrid"],
      "description": "Send notification with retry and fallback"
    }
- Non-MCP integrations:
  * Generate standard Agent_Tool stub entry
  * file="<tool_name>.py", function="<tool_name>"
```

#### Add to [INSTRUCTIONS] Section:

```markdown
Step 2b - MCP Integration Detection:
   - For each agent's integrations array:
     a) Import registry: from core.mcp.registry import is_mcp_available
     b) For each integration_name (PascalCase):
        - If is_mcp_available(integration_name):
          → Emit composite stub entry with "uses_mcp" field
        - Else:
          → Emit standard Agent_Tool entry for custom stub generation
   - Operations array (workflow-internal logic) ALWAYS become standard Agent_Tool entries
```

**Output Example (tools.json):**

```json
{
  "agent_tools": [
    {
      "agent": "NotifierAgent",
      "file": "notify_support_team.py",
      "function": "notify_support_team",
      "tool_type": "Agent_Tool",
      "uses_mcp": ["slack", "sendgrid"],
      "description": "Send notification with retry and email fallback"
    }
  ]
}
```

---

### 4. AgentToolsFileGenerator Updates

**File:** `workflows/Generator/agents.json` → `AgentToolsFileGenerator` system message

**Changes:**

#### Add to [CODE GENERATION] Section:

```markdown
[CODE GENERATION - MCP COMPOSITE TEMPLATES]

When generating Agent_Tool stubs from tools.json entries:

1. Check if tool_spec has "uses_mcp" field
2. If present:
   → Use COMPOSITE_STUB_TEMPLATE
   → Template includes:
     * MCPClientManager.get_client() calls for each MCP server
     * Retry loop with exponential backoff
     * Fallback logic if multiple MCP servers listed
     * log_tool_execution() calls
     * Async timeout enforcement (asyncio.wait_for)

3. If "uses_mcp" absent:
   → Use STANDARD_STUB_TEMPLATE (existing logic)
   → Generate traditional API wrapper code
```

**Generated Composite Stub Example:**

```python
"""
Generated by AgentToolsFileGenerator
Composite tool: Wraps MCP servers for Slack + Sendgrid
"""

from core.mcp.client import MCPClientManager
from core.runtime_logging import log_tool_execution
import asyncio

async def notify_support_team(
    channel: str,
    message: str,
    priority: str = "medium",
    **runtime
) -> dict:
    """
    Send notification via Slack with email fallback.
    
    Uses MCP servers internally: slack, sendgrid
    
    Args:
        channel: Slack channel ID
        message: Notification content
        priority: "high" | "medium" | "low"
        **runtime: chat_id, workflow_name, app_id, user_id
    
    Returns:
        {"status": "success" | "fallback", "delivery_method": "slack" | "email"}
    """
    chat_id = runtime["chat_id"]
    workflow_name = runtime["workflow_name"]
    
    # Get MCP client for Slack
    slack_client = await MCPClientManager.get_client(
        server_id="slack",
        chat_id=chat_id,
        workflow_name=workflow_name
    )
    
    # Retry logic
    max_retries = 3
    for attempt in range(max_retries):
        try:
            result = await asyncio.wait_for(
                slack_client.call_tool(
                    "send_message",
                    channel=channel,
                    text=f"[{priority.upper()}] {message}"
                ),
                timeout=10.0
            )
            
            await log_tool_execution(
                chat_id=chat_id,
                tool_name="notify_support_team",
                result={"status": "success", "method": "slack"}
            )
            
            return {
                "status": "success",
                "delivery_method": "slack",
                "message_id": result.get("message_id")
            }
        
        except Exception as e:
            if attempt < max_retries - 1:
                await asyncio.sleep(2 ** attempt)
                continue
    
    # Fallback to email (via Sendgrid MCP)
    email_client = await MCPClientManager.get_client(
        server_id="sendgrid",
        chat_id=chat_id,
        workflow_name=workflow_name
    )
    
    email_result = await email_client.call_tool(
        "send_email",
        to="support@example.com",
        subject=f"[{priority.upper()}] Alert",
        body=message
    )
    
    await log_tool_execution(
        chat_id=chat_id,
        tool_name="notify_support_team",
        result={"status": "fallback", "method": "email"}
    )
    
    return {
        "status": "fallback",
        "delivery_method": "email",
        "email_id": email_result.get("message_id")
    }
```

---

### 5. Runtime MCP Client Manager (New Component)

**Location:** `core/mcp/client.py`

**Purpose:** Manages MCP server lifecycle and tool invocation for chat sessions.

**Key Classes:**

#### MCPClientManager (Singleton Pool)

```python
class MCPClientManager:
    """
    Manages MCP server client instances per chat session.
    
    Design:
    - One MCPClient instance per (chat_id, server_id) pair
    - Lazy initialization (start server on first tool call)
    - Credentials loaded from context_variables
    """
    
    _clients: Dict[str, "MCPClient"] = {}  # Key: f"{chat_id}:{server_id}"
    
    @classmethod
    async def get_client(
        cls,
        server_id: str,
        chat_id: str,
        workflow_name: str
    ) -> "MCPClient":
        """Get or create MCP client for chat session."""
        key = f"{chat_id}:{server_id}"
        
        if key not in cls._clients:
            server_spec = get_mcp_server(server_id)
            if not server_spec:
                raise ValueError(f"Unknown MCP server: {server_id}")
            
            client = MCPClient(
                server_spec=server_spec,
                chat_id=chat_id,
                workflow_name=workflow_name
            )
            await client.start()
            cls._clients[key] = client
        
        return cls._clients[key]
```

#### MCPClient (Per-Session Connection)

```python
class MCPClient:
    """
    Single MCP server connection for a chat session.
    
    Wraps @modelcontextprotocol/sdk client library.
    """
    
    def __init__(self, server_spec: MCPServerSpec, chat_id: str, workflow_name: str):
        self.server_spec = server_spec
        self.chat_id = chat_id
        self.workflow_name = workflow_name
        self._connection = None  # MCP SDK connection object
    
    async def start(self):
        """Start MCP server process and establish connection."""
        credentials = await self._load_credentials()
        
        # Start MCP server subprocess (via MCP SDK)
        # Set environment variables from credentials
        # Establish stdin/stdout protocol connection
        pass
    
    async def call_tool(self, capability: str, **kwargs) -> Dict[str, Any]:
        """
        Invoke MCP tool via protocol.
        
        Args:
            capability: Tool name (e.g., "send_message")
            **kwargs: Tool-specific parameters
        
        Returns:
            Tool execution result (normalized to AG2 format)
        """
        # Call MCP tool via SDK protocol
        # Handle protocol errors (timeout, invalid response, etc.)
        # Normalize response to AG2 tool return format
        pass
    
    async def _load_credentials(self) -> Dict[str, str]:
        """Load credentials from context_variables."""
        from core.workflow.context_manager import get_context_variables
        
        context_vars = get_context_variables(self.chat_id)
        credentials = {}
        
        for env_var in self.server_spec.credential_env_vars:
            var_name = env_var.lower()  # SLACK_BOT_TOKEN → slack_bot_token
            value = context_vars.get(var_name)
            
            if not value:
                raise ValueError(
                    f"Missing credential for {self.server_spec.display_name}: {env_var}"
                )
            
            credentials[env_var] = value
        
        return credentials
```

**Integration with Workflow Loader:**

```python
# In core/workflow/workflow_manager.py (or similar)

async def _load_tool_callable(tool_spec: dict, chat_id: str, workflow_name: str):
    """
    Load tool callable from tool spec.
    
    Supports:
    - Standard Python stubs (file="<name>.py")
    - Composite stubs (file="<name>.py" with uses_mcp metadata)
    """
    
    file_path = tool_spec.get("file", "")
    
    # All Python stubs (including composites) load the same way
    module = _import_tool_module(workflow_name, file_path)
    func = getattr(module, tool_spec["function"])
    
    # Inject runtime context
    async def wrapped(**kwargs):
        return await func(
            **kwargs,
            chat_id=chat_id,
            workflow_name=workflow_name,
            app_id=runtime_context.get("app_id"),
            user_id=runtime_context.get("user_id")
        )
    
    return wrapped
```

**Key Point:** Runtime doesn't distinguish between composite stubs and regular stubs. Composite stubs are just Python functions that happen to call `MCPClientManager` internally.

---

### 6. Credential Flow (Unchanged)

**MCP servers require API credentials.** The existing credential collection flow remains identical:

```
ActionPlanArchitect (identifies integrations: ["Slack", "Sendgrid"])
    ↓
collect_api_keys.py (lifecycle tool)
    ↓ triggers UI_Tool
AgentAPIKeyInput (React component collects credentials from user)
    ↓ WebSocket event
Backend receives credentials
    ↓
store_in_context_variables(chat_id, {"slack_bot_token": "xoxb-...", "sendgrid_api_key": "SG..."})
    ↓
MCPClient._load_credentials() reads from context_variables
    ↓
MCP server starts with environment variables set
```

**No changes needed to:**
- `collect_api_keys.py` (lifecycle tool)
- `AgentAPIKeyInput` (UI_Tool)
- `context_variables` storage (existing persistence)

---

## Decision Matrix: When to Use MCP

### ✅ Use Composite Stub (Wrapping MCP)

| Scenario | Reason |
|----------|--------|
| Third-party API with MCP server available | Avoid writing custom API wrapper |
| Multi-step process with deterministic logic | 1 LLM call vs. N calls (cost/latency) |
| Retry/fallback/verification needed | Python handles edge cases |
| Performance-critical (batch operations) | Avoid LLM round-trips |
| Error-prone external APIs | Centralized error handling |

**Examples:**
- "Send Slack notification with 3 retries and email fallback"
- "Upload 100 files to Google Drive in batches"
- "Post to social media with delivery verification"

### ❌ Use Agent Orchestration (Multiple Tool Calls)

| Scenario | Reason |
|----------|--------|
| Decision tree with conditional branching | LLM reasoning needed |
| Multi-service coordination (Slack + DB + Email) | Cross-tool dependencies |
| User approval checkpoints | Human-in-the-loop required |

**Examples:**
- "If customer is app tier AND system is down, escalate to PagerDuty"
- "Query database, calculate score, then notify if score > threshold"

### ⚠️ Do NOT Use MCP For

| Scenario | Use Instead |
|----------|-------------|
| Custom business logic | Standard Python stub |
| User interaction required | UI_Tool (Python + React) |
| Platform infrastructure | Standard Python stub |
| Database queries | Standard Python stub |
| Complex data transformations | Standard Python stub |

---

## Implementation Phases

### Phase 1: MCP Registry (Week 1)
**Goal:** Static registry with 3-5 common MCP servers  
**Deliverables:**
- `core/mcp/registry.py` with `MCPServerSpec` dataclass
- Initial registry entries: Slack, Google Sheets, Sendgrid
- Helper functions: `get_mcp_server()`, `is_mcp_available()`

**Testing:**
```python
from core.mcp.registry import is_mcp_available, get_mcp_server

assert is_mcp_available("Slack") == True
assert is_mcp_available("CustomAPI") == False

spec = get_mcp_server("slack")
assert spec.server_id == "slack"
assert "SLACK_BOT_TOKEN" in spec.credential_env_vars
```

**Risk:** Low. Registry is documentation-only; no runtime changes.

---

### Phase 2: Generator Awareness (Week 2)
**Goal:** Generator agents produce MCP-aware tool manifests  
**Deliverables:**
- Update `ToolsManagerAgent` system message (MCP detection logic)
- Update `AgentToolsFileGenerator` system message (composite templates)
- Test workflow generation: `integrations: ["Slack"]` → `uses_mcp: ["slack"]` in tools.json

**Testing:**
1. Run Generator workflow with prompt: "Create automation that sends Slack notifications"
2. Verify `tools.json` contains:
   ```json
   {
     "agent": "NotifierAgent",
     "file": "notify_team.py",
     "uses_mcp": ["slack"]
   }
   ```
3. Verify generated `notify_team.py` includes `MCPClientManager.get_client()` calls

**Risk:** Medium. Generator may produce invalid tool specs if logic is incorrect.

---

### Phase 3: MCP Client Manager (Week 3-4)
**Goal:** Runtime can load and execute composite stubs with MCP  
**Deliverables:**
- `core/mcp/client.py` with `MCPClientManager` and `MCPClient` classes
- MCP SDK integration (stdio transport)
- Credential loading from `context_variables`
- Connection pooling (one client per chat_id:server_id)

**Testing:**
1. Manually create test workflow with composite stub
2. Set credentials in `context_variables`: `{"slack_bot_token": "test-token"}`
3. Execute workflow
4. Verify:
   - MCP server process starts
   - Credentials injected as environment variables
   - Tool call succeeds (or fails with clear error)
   - MCP client reused for subsequent calls

**Risk:** High. MCP SDK integration, process management, and protocol handling are complex.

---

### Phase 4: End-to-End Validation (Week 5)
**Goal:** Full Generator → Runtime flow with real MCP servers  
**Deliverables:**
- Integration test: Generate workflow → Collect API keys → Execute with MCP
- Observability: Runtime logging for MCP tool calls
- Error handling: MCP server crash recovery, timeout handling
- Documentation updates

**Testing:**
1. User prompt: "Create automation that posts to Slack when Google Sheet is updated"
2. Generator produces workflow with `uses_mcp: ["slack", "google_sheets"]`
3. User provides real API keys via `AgentAPIKeyInput`
4. Runtime executes workflow
5. Verify:
   - Slack message sent successfully
   - Google Sheets polling works
   - Token tracking includes MCP tool calls
   - Runtime logs show MCP server lifecycle events

**Risk:** Medium. Real API keys and external services introduce flakiness.

---

### Phase 5: Production Hardening (Week 6+)
**Goal:** MCP integration production-ready  
**Deliverables:**
- MCP server crash detection and restart
- Credential rotation support
- MCP registry expansion (10+ servers)
- Performance benchmarking (latency, token cost)
- Multi-tenant isolation validation (no credential leakage)

**Risk:** Low. Incremental improvements to stable foundation.

---

## Observability & Monitoring

### Runtime Logging

**MCP-specific log events:**

```python
# In core/observability/ag2_runtime_logger.py

# MCP server lifecycle
log_event(
    chat_id=chat_id,
    event_type="mcp_server_start",
    details={"server_id": "slack", "chat_id": chat_id, "pid": process_id}
)

log_event(
    chat_id=chat_id,
    event_type="mcp_server_stop",
    details={"server_id": "slack", "reason": "chat_ended"}
)

# MCP tool invocation
log_event(
    chat_id=chat_id,
    event_type="mcp_tool_call",
    details={
        "server_id": "slack",
        "tool": "send_message",
        "params": {"channel": "#support"},  # Sanitized (no secrets)
        "duration_ms": 234,
        "status": "success"
    }
)
```

### Token Tracking

**MCP tool calls do NOT generate LLM tokens** (it's Python code execution). However:

- Composite stub invocation counts as **1 tool call token event** (same as any AG2 tool)
- MozaiksPay tracks tool calls for billing purposes
- No changes needed to `realtime_token_logger.py`

### Performance Metrics

**Key metrics to track:**

```python
# In core/observability/performance_manager.py

# MCP client initialization latency
metrics.record("mcp_client_init_ms", duration, tags={"server_id": "slack"})

# MCP tool call latency
metrics.record("mcp_tool_call_ms", duration, tags={"server": "slack", "tool": "send_message"})

# MCP server crash rate
metrics.increment("mcp_server_crashes", tags={"server_id": "slack", "reason": "timeout"})

# Composite stub cost savings (vs agent orchestration)
metrics.record("tool_calls_saved", count=3, tags={"stub": "notify_support_team"})
```

---

## Multi-Tenant Isolation

### Credential Isolation (Critical)

**MCP servers MUST NOT leak credentials across chat sessions.**

**Enforcement:**

1. **MCPClientManager:** One client instance per `(chat_id, server_id)` pair
   ```python
   key = f"{chat_id}:{server_id}"  # Isolation boundary
   ```

2. **Credential loading:** Always scoped to `chat_id`
   ```python
   context_vars = get_context_variables(chat_id)  # Per-chat context
   ```

3. **MCP server process isolation:** Each `chat_id` gets its own MCP server subprocess
   - No shared MCP server process across chats
   - Credentials passed as environment variables (subprocess-scoped)

4. **Connection cleanup:** When chat ends, terminate MCP client
   ```python
   async def cleanup_chat(chat_id: str):
       for key in list(MCPClientManager._clients.keys()):
           if key.startswith(f"{chat_id}:"):
               client = MCPClientManager._clients.pop(key)
               await client.stop()
   ```

### Token Tracking (Multi-Tenant)

**MCP tool calls must be attributed to `app_id` and `user_id`:**

```python
# In composite stub
await log_tool_execution(
    chat_id=chat_id,
    app_id=runtime["app_id"],  # Platform-controlled
    user_id=runtime["user_id"],              # Platform-controlled
    tool_name="notify_support_team",
    result={"status": "success"}
)
```

**MozaiksPay** tracks tool call costs per app_id for billing.

---

## Error Handling

### MCP Server Failures

**Failure modes:**

1. **Server crash during startup** (invalid credentials, missing package)
   - **Handling:** Raise ValueError with clear message, halt workflow execution
   - **User-facing:** "Slack integration failed: Invalid bot token"

2. **Server crash during tool call** (transient network error)
   - **Handling:** Composite stub retry loop (up to 3 attempts)
   - **User-facing:** (Silent retry, or "Retrying Slack notification...")

3. **Server timeout** (slow API response)
   - **Handling:** `asyncio.wait_for(timeout=10.0)` in composite stub
   - **User-facing:** Fallback to secondary method (email)

4. **Protocol error** (malformed MCP response)
   - **Handling:** Log error, raise exception, halt workflow
   - **User-facing:** "Slack integration error: Protocol mismatch"

### Fallback Strategies

**Composite stubs should implement graceful degradation:**

```python
# Primary: Slack MCP
try:
    slack_result = await slack_client.call_tool("send_message", ...)
    return {"status": "success", "method": "slack"}
except Exception:
    # Fallback: Email MCP
    email_result = await email_client.call_tool("send_email", ...)
    return {"status": "fallback", "method": "email"}
```

**Guidelines:**
- Always have at least one fallback for critical notifications
- Log fallback events for observability
- Return status indicating which method succeeded

---

## Security Considerations

### Credential Storage

**Existing pattern (unchanged):**
- Credentials collected via `AgentAPIKeyInput` UI_Tool
- Stored in `context_variables` (MongoDB, encrypted at rest)
- Scoped to `chat_id` (multi-tenant isolation)

**MCP-specific:**
- MCP servers receive credentials as **environment variables** (subprocess-scoped)
- Credentials **never logged** (sanitized in runtime_logging)
- MCP server process terminated when chat ends (credential lifecycle)

### Subprocess Sandboxing

**MCP servers run as child processes:**
- Use `asyncio.create_subprocess_exec()` for process management
- Set resource limits (CPU, memory) if available
- Monitor for runaway processes (kill if exceeds timeout)

**Security boundary:**
- MCP server = untrusted code (community-maintained packages)
- Runtime validates MCP responses before passing to agents
- No direct file system access from MCP servers (stdio transport only)

---

## Testing Strategy

### Unit Tests

**Test `MCPClientManager`:**
```python
async def test_mcp_client_manager_pooling():
    """Verify one client per chat_id:server_id."""
    client1 = await MCPClientManager.get_client("slack", "chat1", "TestWorkflow")
    client2 = await MCPClientManager.get_client("slack", "chat1", "TestWorkflow")
    assert client1 is client2  # Same instance
    
    client3 = await MCPClientManager.get_client("slack", "chat2", "TestWorkflow")
    assert client3 is not client1  # Different chat_id = different instance
```

**Test credential loading:**
```python
async def test_mcp_credential_loading():
    """Verify credentials loaded from context_variables."""
    set_context_variables("chat1", {"slack_bot_token": "test-token"})
    
    client = MCPClient(get_mcp_server("slack"), "chat1", "TestWorkflow")
    credentials = await client._load_credentials()
    
    assert credentials["SLACK_BOT_TOKEN"] == "test-token"
```

### Integration Tests

**Test composite stub generation:**
```python
async def test_generator_produces_composite_stubs():
    """Verify Generator creates composite stubs for MCP integrations."""
    action_plan = {
        "workflow": {
            "phases": [{
                "agents": [{
                    "name": "NotifierAgent",
                    "integrations": ["Slack"],
                    "operations": ["send_notification"]
                }]
            }]
        }
    }
    
    # Run ToolsManagerAgent
    tools_json = await generate_tools_manifest(action_plan)
    
    # Verify MCP-aware tool spec
    assert any(
        tool["uses_mcp"] == ["slack"]
        for tool in tools_json["agent_tools"]
    )
```

**Test end-to-end workflow execution:**
```python
async def test_e2e_mcp_workflow():
    """Verify workflow with MCP composite stub executes successfully."""
    # Setup: Create test workflow with composite stub
    workflow_name = "TestMCPWorkflow"
    create_test_workflow(workflow_name, uses_mcp=["slack"])
    
    # Setup: Mock Slack MCP server (or use test credentials)
    set_context_variables("test_chat", {"slack_bot_token": "test-token"})
    
    # Execute workflow
    result = await execute_workflow(workflow_name, "test_chat", "Send test message")
    
    # Verify
    assert result["status"] == "success"
    assert "message_id" in result
```

---

## Migration Path

### Backwards Compatibility

**Existing workflows (pre-MCP) continue to work:**
- Old workflows use standard Python stubs (no `uses_mcp` field in tools.json)
- Runtime loads them identically (no code changes needed)
- No forced migration required

**Forward compatibility:**
- New workflows can use MCP composite stubs
- Mixed workflows (some MCP, some standard stubs) supported

### Incremental Adoption

**Phase 1:** New workflows only
- Generator produces MCP composite stubs for new user requests
- Existing workflows unchanged

**Phase 2:** Optional re-generation
- Users can re-generate workflows to use MCP (performance optimization)
- Platform provides "Upgrade to MCP" button (triggers re-generation)

**Phase 3:** Gradual deprecation (long-term)
- Standard API wrapper stubs marked as "legacy" in Generator
- MCP becomes default for all third-party integrations

---

## Open Source Considerations

### Modularity Requirements

**MCP integration MUST be:**
- **Feature-flagged:** `ENABLE_MCP=true` environment variable
- **Optional dependency:** MCP SDK not required for basic runtime
- **Pluggable:** Runtime works without MCP (falls back to standard stubs)

### Configuration

```python
# In core/core_config.py

ENABLE_MCP = os.getenv("ENABLE_MCP", "false").lower() == "true"

if ENABLE_MCP:
    from core.mcp.client import MCPClientManager
else:
    MCPClientManager = None  # Stub for type hints
```

### Documentation for External Contributors

**For open-source users:**
- Document MCP registry extension process
- Provide template for adding new MCP servers
- Explain when to use MCP vs. custom stubs

**For AG2 upstreaming:**
- MCP integration is **runtime-agnostic** (not MozaiksAI-specific)
- Can be extracted as standalone module: `ag2-mcp-integration`
- Aligns with AG2's pluggable architecture

---

## FAQ

### Q: Do I still need API keys with MCP?
**A:** Yes. MCP servers authenticate with third-party APIs using credentials. The credential collection flow (`collect_api_keys` → `AgentAPIKeyInput`) remains unchanged.

### Q: Does MCP replace AG2 GroupChat orchestration?
**A:** No. AG2 GroupChat still coordinates agents. MCP just provides a standardized way to call third-party APIs.

### Q: Can I use MCP for UI interactions?
**A:** No. MCP has no concept of awaiting user responses. Use `UI_Tool` (Python + React + `use_ui_tool` primitive) for user interaction.

### Q: What happens if an MCP server crashes?
**A:** Composite stubs implement retry logic (up to 3 attempts). If all retries fail, they fall back to a secondary method (e.g., email) or return an error.

### Q: How do I add a new MCP server to the registry?
**A:** Add an entry to `core/mcp/registry.py`:
```python
MCP_REGISTRY["hubspot"] = MCPServerSpec(
    server_id="hubspot",
    display_name="HubSpot",
    package="@modelcontextprotocol/server-hubspot",
    capabilities=["create_contact", "get_deals"],
    category="crm",
    requires_credentials=True,
    credential_env_vars=["HUBSPOT_API_KEY"],
    description="Manage contacts and deals in HubSpot CRM"
)
```

### Q: Does MCP increase costs?
**A:** No, it **reduces** costs. Composite stubs replace multi-turn agent orchestration (5 LLM calls → 1 LLM call), saving 75% in token costs and latency.

### Q: Is MCP production-ready?
**A:** The MCP protocol is stable. However, individual MCP servers vary in quality. Test thoroughly with real API keys before production use.

---

## References

- [Model Context Protocol Specification](https://modelcontextprotocol.io/)
- [MCP SDK Documentation](https://github.com/modelcontextprotocol/sdk)
- [Available MCP Servers](https://github.com/modelcontextprotocol/servers)
- [AG2 (Autogen) Documentation](https://ag2.ai/)

---

## Appendix: Complete Example

### Example: Slack Notification Workflow with MCP

#### 1. User Prompt
```
"Create automation that sends urgent Slack notifications when customer support tickets 
have priority = high, with email fallback if Slack fails."
```

#### 2. ActionPlanArchitect Output
```json
{
  "workflow": {
    "name": "UrgentTicketNotifier",
    "phases": [{
      "agents": [{
        "name": "NotifierAgent",
        "integrations": ["Slack", "Sendgrid"],
        "operations": ["check_ticket_priority", "notify_team"]
      }]
    }]
  }
}
```

#### 3. collect_api_keys.py (Lifecycle Tool)
```python
# Collects credentials via AgentAPIKeyInput
# Stores in context_variables:
# {
#   "slack_bot_token": "xoxb-...",
#   "sendgrid_api_key": "SG..."
# }
```

#### 4. ToolsManagerAgent Output (tools.json)
```json
{
  "agent_tools": [
    {
      "agent": "NotifierAgent",
      "file": "check_ticket_priority.py",
      "function": "check_ticket_priority",
      "tool_type": "Agent_Tool",
      "description": "Query ticket priority from database"
    },
    {
      "agent": "NotifierAgent",
      "file": "notify_team.py",
      "function": "notify_team",
      "tool_type": "Agent_Tool",
      "uses_mcp": ["slack", "sendgrid"],
      "description": "Send Slack notification with email fallback"
    }
  ]
}
```

#### 5. AgentToolsFileGenerator Output (notify_team.py)
```python
"""
Generated by AgentToolsFileGenerator
Composite tool: Slack notification with email fallback
"""

from core.mcp.client import MCPClientManager
from core.runtime_logging import log_tool_execution
import asyncio

async def notify_team(ticket_id: str, priority: str, message: str, **runtime) -> dict:
    """Send urgent notification via Slack with email fallback."""
    chat_id = runtime["chat_id"]
    workflow_name = runtime["workflow_name"]
    
    # Try Slack first
    try:
        slack_client = await MCPClientManager.get_client("slack", chat_id, workflow_name)
        result = await asyncio.wait_for(
            slack_client.call_tool(
                "send_message",
                channel="#support-urgent",
                text=f"[URGENT] Ticket {ticket_id}: {message}"
            ),
            timeout=10.0
        )
        return {"status": "success", "method": "slack", "message_id": result["message_id"]}
    
    except Exception:
        # Fallback to email
        email_client = await MCPClientManager.get_client("sendgrid", chat_id, workflow_name)
        result = await email_client.call_tool(
            "send_email",
            to="support@example.com",
            subject=f"[URGENT] Ticket {ticket_id}",
            body=message
        )
        return {"status": "fallback", "method": "email", "email_id": result["message_id"]}
```

#### 6. Runtime Execution
```
User message: "Check ticket #12345"
  ↓
GroupChat: NotifierAgent calls check_ticket_priority(ticket_id="12345")
  ↓ Result: {"priority": "high"}
  ↓
GroupChat: NotifierAgent calls notify_team(ticket_id="12345", priority="high", message="...")
  ↓
notify_team.py: MCPClientManager.get_client("slack", ...)
  ↓
MCPClient: Load credentials from context_variables
  ↓
MCPClient: Start Slack MCP server subprocess with SLACK_BOT_TOKEN env var
  ↓
MCPClient: Call send_message tool via MCP protocol
  ↓
Slack MCP Server: Post message to Slack API
  ↓
notify_team.py: Return {"status": "success", "method": "slack", "message_id": "..."}
  ↓
GroupChat: "Notification sent successfully via Slack"
```

---

**Document Version:** 1.0  
**Last Updated:** 2025-10-13  
**Status:** Design Complete - Ready for Implementation
