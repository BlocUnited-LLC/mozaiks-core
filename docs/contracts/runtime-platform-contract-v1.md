# Runtime ↔ Platform Contract v1.0
> **Doc Status:** authoritative (platform depends on this doc)

> **Status**: DRAFT — Pending confirmation from mozaiks-platform agent  
> **Version**: 1.3.0  
> **Last Updated**: 2026-01-31  
> **Owner**: mozaiks-core

This document defines the authoritative external contracts exposed by **mozaiks-core** for consumption by external control planes (e.g., mozaiks-platform).

---

## Table of Contents

1. [Identity Model](#1-identity-model)
2. [Authentication Contract](#2-authentication-contract)
3. [HTTP API Contract](#3-http-api-contract)
4. [WebSocket Contract](#4-websocket-contract)
5. [Internal/Admin API Contract](#5-internaladmin-api-contract)
6. [Plugin Execution Contract](#6-plugin-execution-contract)
7. [Event Contract](#7-event-contract)
8. [Versioning & Compatibility](#8-versioning--compatibility)
9. [Environment Configuration](#9-environment-configuration)
10. [Open Questions](#10-open-questions)

---

## 1. Identity Model

Every request to mozaiks-core is scoped by these identifiers:

| Identifier | Description | Source of Truth | Format |
|------------|-------------|-----------------|--------|
| `app_id` | Tenant/app boundary | **Platform mints this** | String (e.g., `app_123`) |
| `user_id` | End-user identity | **Derived from JWT** (platform issues tokens) | String |
| `chat_id` | Workflow run instance | **Runtime mints this** on start | String (e.g., `chat_abc123`) |
| `workflow_id` | Workflow template identifier | **Runtime owns** | String |
| `capability_id` | User-facing capability name | **Runtime owns** (platform maps to workflows) | String |

### Invariants

- **Platform is authoritative** for `app_id` and user identity (JWT issuance)
- **Runtime is authoritative** for `chat_id` and workflow execution state
- **Never mix state** across `(app_id, user_id, chat_id)` boundaries
- All persistence queries are scoped by `app_id` (and often `user_id`)

---

## 2. Authentication Contract

### 2.1 Supported Auth Modes

| Mode | Use Case | Configuration |
|------|----------|---------------|
| `external` (default) | Platform issues OIDC JWT | `MOZAIKS_JWKS_URL`, `MOZAIKS_ISSUER`, `MOZAIKS_AUDIENCE` |
| `local` | Standalone/dev mode | `JWT_SECRET`, `JWT_ALGORITHM` |

Set via `MOZAIKS_AUTH_MODE` environment variable.

### 2.2 JWT Requirements

#### Required Claims

| Claim | Required | Description |
|-------|----------|-------------|
| `sub` | ✅ | User identifier (maps to `user_id`) |
| `exp` | ✅ | Expiration timestamp |
| `iat` | ✅ | Issued-at timestamp |

#### Optional Claims

| Claim | Description | Configurable Via |
|-------|-------------|------------------|
| `email` | User email | `MOZAIKS_EMAIL_CLAIM` |
| `roles` | User roles array | `MOZAIKS_ROLES_CLAIM` |
| Custom user_id | Override `sub` as user_id | `MOZAIKS_USER_ID_CLAIM` |

### 2.3 Execution Tokens

For launching AI capabilities, the runtime mints **execution tokens** via `/api/ai/launch`. These are short-lived, runtime-facing tokens that carry the full authorization context.

**Execution Token Claims**:

```json
{
  "sub": "<user_id>",
  "app_id": "<app_id>",
  "chat_id": "<chat_id>",
  "capability_id": "<capability_id>",
  "workflow_id": "<workflow_id>",
  "plan": "free|pro|enterprise",
  "roles": ["user", "admin"],
  "is_superadmin": false,
  "mozaiks_token_use": "execution",
  "iss": "mozaikscore",
  "iat": 1737561600,
  "exp": 1737562200
}
```

**Properties**:
- Default TTL: 10 minutes (configurable via `MOZAIKS_EXECUTION_TOKEN_EXPIRE_MINUTES`)
- Algorithm: HS256 by default (configurable via `MOZAIKS_EXECUTION_TOKEN_ALGORITHM`)
- Signing key: `MOZAIKS_EXECUTION_TOKEN_SECRET` or `JWT_SECRET`

### 2.4 WebSocket Authentication

WebSocket connections authenticate via:

1. **Preferred**: `Sec-WebSocket-Protocol: access_token.<JWT>`
2. **Fallback**: `?access_token=<JWT>` query parameter

---

## 3. HTTP API Contract

### 3.1 Health & Operations (Unauthenticated)

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `GET /health` | GET | Liveness probe |
| `GET /ready` | GET | Readiness probe |
| `GET /info` | GET | App metadata |

#### Response Schemas

**`GET /health`** — Always returns 200 if process is running:
```json
{
  "status": "healthy",
  "app_id": "app_123",
  "app_tier": "free"
}
```

**`GET /ready`** — Returns 200 when ready, 503 when not:
```json
// 200 OK
{
  "status": "ready",
  "app_id": "app_123",
  "app_tier": "free"
}

// 503 Service Unavailable
{
  "status": "not_ready",
  "reason": "startup_in_progress|database_unavailable|...",
  "app_id": "app_123"
}
```

**`GET /info`**:
```json
{
  "app_id": "app_123",
  "app_tier": "free",
  "app_name": "My App",
  "port": 8080,
  "ready": true
}
```

---

### 3.2 AI Capabilities API

These endpoints enable the platform to launch AI capabilities without knowing workflow internals.

#### List Capabilities

**`GET /api/ai/capabilities`**

**Auth**: User JWT

**Response**:
```json
{
  "capabilities": [
    {
      "id": "write_blog",
      "display_name": "Blog Writer",
      "description": "Generate blog posts with AI",
      "icon": "pencil",
      "visibility": "user",
      "enabled": true,
      "allowed": true
    }
  ],
  "plan": "free"
}
```

#### Launch Capability

**`POST /api/ai/launch`**

**Auth**: User JWT

**Request**:
```json
{
  "capability_id": "write_blog"
}
```

**Response**:
```json
{
  "app_id": "app_123",
  "capability_id": "write_blog",
  "chat_id": "chat_abc123",
  "launch_token": "<JWT>",
  "expires_in": 600,
  "runtime": {
    "runtime_api_base_url": "https://runtime.mozaiks.io",
    "runtime_ui_base_url": "https://chat.mozaiks.io",
    "chatui_url_template": "{runtime_ui_base_url}/chat?app_id={app_id}&chat_id={chat_id}&token={token}"
  }
}
```

**Error Responses**:
| Status | Condition |
|--------|-----------|
| 400 | Missing `capability_id` |
| 403 | Capability disabled, requires superadmin, or not in plan |
| 404 | Unknown capability |
| 500 | Missing `MOZAIKS_APP_ID` or `workflow_id` mapping |

---

### 3.3 Workflow/Chat API

#### Start Workflow Chat

**`POST /api/chats/{app_id}/{workflow_name}/start`**

**Auth**: User JWT or Execution Token

**Request**:
```json
{
  "user_id": "user_123",
  "client_request_id": "uuid-for-idempotency",
  "force_new": false,
  "required_min_tokens": 0
}
```

**Response**:
```json
{
  "success": true,
  "chat_id": "chat_abc123",
  "workflow_name": "AgentGenerator",
  "app_id": "app_001",
  "user_id": "user_123",
  "remaining_balance": 0,
  "websocket_url": "/ws/AgentGenerator/app_001/chat_abc123/user_123",
  "message": "Chat session initialized; connect to websocket to start.",
  "reused": false,
  "cache_seed": 1234567890
}
```

**Error Responses**:
| Status | Condition |
|--------|-----------|
| 400 | Missing/invalid `user_id` |
| 402 | Insufficient tokens (if token gating enabled) |
| 409 | Prerequisites not met (pack gating) |
| 500 | Unexpected runtime error |

**Idempotency**: By default, the runtime may reuse a recent in-progress chat to prevent duplicate starts. Set `force_new=true` to always create a new `chat_id`.

#### Get Chat Metadata

**`GET /api/chats/meta/{app_id}/{workflow_name}/{chat_id}`**

**Auth**: User JWT

**Response**:
```json
{
  "chat_id": "chat_abc123",
  "workflow_name": "AgentGenerator",
  "app_id": "app_001",
  "user_id": "user_123",
  "status": "in_progress|completed|error",
  "created_at": "2026-01-23T12:00:00Z",
  "updated_at": "2026-01-23T12:34:56Z"
}
```

#### Get Cached Artifact State

**`GET /api/artifacts/{artifact_id}/cached`**

**Auth**: User JWT (or any valid token)

**Query Params**:
- `app_id` (required if token does not carry `app_id`)
- `chat_id` (optional)

**Response**:
```json
{
  "artifact_id": "artifact_123",
  "chat_id": "chat_abc123",
  "workflow_name": "AgentGenerator",
  "app_id": "app_001",
  "state": { "artifact_type": "core.card", "title": "Revenue", "body": "$12,450" },
  "updated_at": "2026-01-31T12:34:56Z",
  "expires_at": "2026-01-31T13:04:56Z"
}
```

**Error Responses**:
| Status | Condition |
|--------|-----------|
| 400 | Missing `app_id` or `artifact_id` |
| 403 | Token `app_id` mismatch |
| 404 | Cache miss / expired |
| 500 | Unexpected runtime error |

#### List User Sessions

**`GET /api/sessions/list/{app_id}/{user_id}`**

**Auth**: User JWT

**Response**:
```json
{
  "sessions": [
    {
      "chat_id": "chat_abc123",
      "workflow_name": "AgentGenerator",
      "status": "completed",
      "created_at": "2026-01-23T12:00:00Z"
    }
  ]
}
```

#### List Available Workflows

**`GET /api/workflows/{app_id}/available`**

**Auth**: User JWT

**Query Params**: `?user_id=...`

**Response**:
```json
{
  "workflows": [
    {
      "id": "AgentGenerator",
      "display_name": "Agent Generator",
      "available": true,
      "locked_reason": null
    },
    {
      "id": "ValidationEngine",
      "display_name": "Validation Engine",
      "available": false,
      "locked_reason": "Requires AppGenerator completion"
    }
  ]
}
```

---

### 3.4 App Configuration API

#### Get App Config

**`GET /api/app-config`**

**Auth**: None

**Response**:
```json
{
  "monetization_enabled": false,
  "app_name": "My App",
  "app_version": "1.0.0",
  "env": "development"
}
```

#### Get Navigation

**`GET /api/navigation`**

**Auth**: User JWT

**Response**:
```json
{
  "navigation": [
    {
      "label": "Dashboard",
      "path": "/dashboard",
      "icon": "home"
    },
    {
      "label": "Discovery",
      "icon": "compass",
      "trigger": {
        "type": "workflow",
        "workflow": "DiscoveryDashboard",
        "input": {
          "view": "marketplace"
        },
        "mode": "view",
        "cache_ttl": 300
      }
    },
    {
      "plugin_name": "blog",
      "label": "Blog",
      "path": "/plugins/blog",
      "icon": "pencil"
    }
  ]
}
```

**Navigation Trigger Schema (additive):**
```json
{
  "label": "string",
  "icon": "string",
  "path": "string?",   // optional traditional route
  "trigger": {         // optional workflow trigger
    "type": "workflow",
    "workflow": "string",
    "input": {},
    "mode": "view|workflow|ask",
    "cache_ttl": "number?"
  }
}
```

**Notes:**
- `cache_ttl` is in seconds (optional).

**Client behavior (when `trigger` is present):**
- Start workflow via `POST /api/chats/{app_id}/{workflow_name}/start`.
- Connect WebSocket to returned `chat_id`.
- Set layout mode from `trigger.mode` (`view` → `view`, `workflow` → `split`, `ask` → `full`).
- If `cache_ttl` is set, clients may render a cached artifact immediately using key
  `artifact:{workflow}:{input_hash}` and refresh in the background.

#### Get Theme Config

**`GET /api/theme-config`**

**Auth**: None

**Response**:
```json
{
  "branding": {
    "app_name": "My App",
    "logo_url": "https://..."
  },
  "colors": {
    "primary": "#007bff",
    "secondary": "#6c757d"
  }
}
```

---

## 4. WebSocket Contract

### 4.1 Chat Streaming

**Route**: `/ws/{workflow_name}/{app_id}/{chat_id}/{user_id}`

**Auth**: Token in `Sec-WebSocket-Protocol: access_token.<JWT>` or `?access_token=<JWT>`

#### Outbound Event Envelope

All server-to-client messages follow this envelope:

```json
{
  "type": "<event_type>",
  "data": { /* event-specific payload */ },
  "timestamp": "2026-01-23T12:34:56.000000+00:00"
}
```

#### Event Types (Server → Client)

| Type | Purpose | Data Shape |
|------|---------|------------|
| `chat.text` | Agent/assistant text message | `{ "content": "...", "agent": "..." }` |
| `chat.print` | Streaming text chunk | `{ "content": "..." }` |
| `chat.run_start` | Workflow started | `{ "chat_id": "...", "workflow_name": "..." }` |
| `chat.run_complete` | Workflow completed | `{ "chat_id": "...", "status": "completed" }` |
| `chat.error` | Error occurred | `{ "message": "...", "error_code": "..." }` |
| `chat.input_request` | HITL: requesting user input | `{ "prompt": "...", "input_type": "text|choice" }` |
| `chat.tool_call` | Tool invocation started | `{ "tool_name": "...", "args": {...} }` |
| `chat.tool_response` | Tool invocation completed | `{ "tool_name": "...", "result": {...} }` |
| `chat.context_switched` | Journey auto-advance | `{ "next_workflow": "...", "next_chat_id": "..." }` |
| `chat.ui_tool` | UI tool event (artifact) | `{ "event_type": "...", "payload": {...} }` |
| `artifact.action.started` | Artifact action accepted | `{ "action_id": "...", "artifact_id": "...", "tool": "..." }` |
| `artifact.action.completed` | Artifact action completed | `{ "action_id": "...", "artifact_id": "...", "tool": "...", "result": {...}, "artifact_update": {...} }` |
| `artifact.action.failed` | Artifact action failed | `{ "action_id": "...", "artifact_id": "...", "tool": "...", "error": "..." }` |

#### Artifact Action System

Artifact actions allow frontend-rendered artifacts to trigger stateless tool calls outside
the agent loop. Actions are declared inside artifact payloads under `actions[]` and executed
via WebSocket messages of type `artifact.action`.

**ActionSchema**
```json
{
  "label": "string",
  "icon": "string?",
  "tool": "string",
  "params": {},
  "scope": "artifact|row",
  "style": "primary|secondary|ghost|danger",
  "confirm": "string?",
  "optimistic": {}
}
```

**Action result update**
```json
{
  "mode": "replace|patch",
  "payload": {}
}
```
When `mode` is `"patch"`, `payload` must be a JSON Patch array (RFC 6902).

**Optimistic updates**

If an action includes an `optimistic` block, the frontend applies it immediately and
stores a rollback snapshot. If the backend emits `artifact.action.failed` with
`rollback: true`, the frontend reverts to the snapshot.

**Examples**

Vote:
```json
{
  "label": "Vote",
  "tool": "platform.vote",
  "style": "primary",
  "params": { "target_id": "{{id}}", "direction": "up" },
  "optimistic": [{ "op": "replace", "path": "/vote_status", "value": "pending" }]
}
```

Delete:
```json
{
  "label": "Delete",
  "tool": "platform.delete",
  "style": "danger",
  "confirm": "Delete this item?",
  "params": { "id": "{{id}}" }
}
```

Submit form:
```json
{
  "label": "Submit",
  "tool": "platform.submit_form",
  "style": "primary",
  "params": { "payload": "{{form_data}}" }
}
```

#### Core Artifact Primitives (`core.*`)

Core provides built-in rendering for a small set of **data-driven primitives**.  
These are sent as **UI tool payloads** (via `chat.ui_tool` or `ui_tool` events) and
selected by `artifact_type`.

**Notes:**
- `artifact_type` is required and must start with `core.`.
- Core reads fields at the **top level**. For convenience, it will also accept
  a `{ "data": { ... } }` wrapper with the same fields.
- `actions[]` uses the **ActionSchema** above.
- Row-level actions can be declared via `row_actions` or `actions[]` with `scope: "row"`.

**`core.markdown`**
```json
{
  "artifact_type": "core.markdown",
  "artifact_id": "md_123",
  "title": "Optional title",
  "body": "Markdown **content**",
  "actions": [ActionSchema]
}
```

**`core.card`**
```json
{
  "artifact_type": "core.card",
  "artifact_id": "card_123",
  "title": "Card title",
  "subtitle": "Optional subtitle",
  "body": "Body text or markdown",
  "image": "https://...",
  "metadata": [{ "label": "Status", "value": "Active" }],
  "actions": [ActionSchema]
}
```

**`core.list`**
```json
{
  "artifact_type": "core.list",
  "artifact_id": "list_123",
  "title": "Optional title",
  "items": [
    {
      "id": "item_1",
      "title": "Item title",
      "subtitle": "Optional subtitle",
      "icon": "✅",
      "actions": [ActionSchema]
    }
  ],
  "actions": [ActionSchema]
}
```

**`core.table`**
```json
{
  "artifact_type": "core.table",
  "artifact_id": "table_123",
  "title": "Optional title",
  "columns": [
    { "key": "name", "label": "Name", "type": "text" },
    { "key": "status", "label": "Status", "type": "badge" },
    { "key": "votes", "label": "Votes", "type": "number" },
    { "key": "actions", "label": "Actions", "type": "actions" }
  ],
  "rows": [
    { "id": "row_1", "name": "App Alpha", "status": "active", "votes": 24 }
  ],
  "actions": [ActionSchema],
  "row_actions": [ActionSchema]
}
```

**`core.form`**
```json
{
  "artifact_type": "core.form",
  "artifact_id": "form_123",
  "title": "Optional title",
  "fields": [
    { "name": "email", "type": "text", "label": "Email", "required": true },
    { "name": "plan", "type": "select", "label": "Plan", "options": ["Free", "Pro"] }
  ],
  "submit_action": ActionSchema,
  "cancel_action": ActionSchema
}
```

**`core.composite`**
```json
{
  "artifact_type": "core.composite",
  "artifact_id": "composite_123",
  "layout": "stack|grid|columns",
  "grid_template": "2x2",
  "children": [
    { "artifact_type": "core.card", "title": "Revenue", "body": "$12,450" },
    { "artifact_type": "core.table", "columns": [...], "rows": [...] }
  ]
}
```

**Primitive theming (CSS variables)**

Core primitives read the following CSS variables (platform can override them):

- `--core-primitive-surface`
- `--core-primitive-surface-alt`
- `--core-primitive-border`
- `--core-primitive-text`
- `--core-primitive-muted`
- `--core-primitive-accent`
- `--core-primitive-shadow`
- `--core-primitive-radius`

Defaults map to the active theme (e.g., `--color-surface`, `--color-text-primary`, etc.).

#### AG-UI Compatibility Events (`agui.*`)

Mozaiks Core dual-emits **AG-UI compatible events** alongside existing `chat.*` events.  
This is **additive** and opt-out via `MOZAIKS_AGUI_ENABLED=false`.

For the formal AG-UI specification and implementation associations, see:
`docs/contracts/AG-UI_CONTRACT.md`.

All AG-UI events use the same envelope:
```json
{
  "type": "agui.*",
  "data": { "runId": "...", "threadId": "...", "...": "..." },
  "timestamp": "..."
}
```

**Thread ID format:** `{app_id}:{chat_id}`  
**Run ID:** `chat_id` (unless otherwise specified in event payload)

**Lifecycle mappings (AG-UI):**
| AG-UI Type | Source |
|------------|--------|
| `agui.lifecycle.RunStarted` | `chat.orchestration.run_started` |
| `agui.lifecycle.RunFinished` | `chat.orchestration.run_completed` |
| `agui.lifecycle.RunError` | `chat.orchestration.run_failed` |
| `agui.lifecycle.StepStarted` | `chat.orchestration.agent_started` |
| `agui.lifecycle.StepFinished` | `chat.orchestration.agent_completed` |

**Text mappings (AG-UI):**
| AG-UI Type | Source |
|------------|--------|
| `agui.text.TextMessageStart` | emitted before first `chat.print` (or before `chat.text` if no stream) |
| `agui.text.TextMessageContent` | `chat.print` |
| `agui.text.TextMessageEnd` | emitted after `chat.text` |

**Tool mappings (AG-UI):**
| AG-UI Type | Source |
|------------|--------|
| `agui.tool.ToolCallStart` | `chat.tool_call` |
| `agui.tool.ToolCallEnd` | emitted before `chat.tool_response` |
| `agui.tool.ToolCallResult` | `chat.tool_response` |

**State events (AG-UI):**
| AG-UI Type | Emitted When | Payload (data) |
|------------|--------------|----------------|
| `agui.state.StateSnapshot` | Initial artifact render | `{ artifact_id, state, workflow_name? }` |
| `agui.state.StateDelta` | Artifact updates (actions / patches) | `{ artifact_id, patch: [RFC6902 ops], workflow_name? }` |
| `agui.state.MessagesSnapshot` | Reconnect/resume | `{ messages: [...], mode: "auto|client", total_messages }` |

**Notes:**
- `StateDelta` uses JSON Patch (RFC 6902). Root replacement uses `path: ""`.
- `StateSnapshot` and `StateDelta` include `runId` and `threadId` like other AG-UI events.

#### Inbound Message Types (Client → Server)

```json
// User text input
{
  "type": "user.input.submit",
  "text": "Hello, can you help me?"
}

// UI tool response (artifact interaction)
{
  "type": "ui.tool.response",
  "event_id": "evt_123",
  "response_data": { /* tool-specific */ }
}

// Artifact action (stateless tool invocation)
{
  "type": "artifact.action",
  "action_id": "uuid",
  "artifact_id": "artifact_123",
  "tool": "platform.invest",
  "params": { "amount": 25 },
  "context": { "chat_id": "...", "app_id": "...", "user_id": "..." }
}

// Cancel current operation
{
  "type": "user.cancel"
}
```

#### Connection Lifecycle

1. Client connects with auth token
2. Server validates token, accepts connection
3. Server sends `chat.run_start` if resuming
4. Bidirectional message exchange
5. Server sends `chat.run_complete` or `chat.error` on termination
6. Either party may close connection

#### Close Codes

| Code | Meaning |
|------|---------|
| 1000 | Normal closure |
| 4001 | Authentication required |
| 4003 | User ID mismatch (path vs JWT) |
| 4009 | Prerequisites not met |

---

### 4.2 Notifications WebSocket

**Route**: `/ws/notifications/{user_id_hint}`

**Auth**: Same as chat WebSocket

**Note**: `user_id_hint` is for routing only; actual identity is derived from JWT.

#### Event Types

```json
{
  "type": "notification",
  "data": {
    "id": "notif_123",
    "type": "info|warning|error|success",
    "title": "Action Complete",
    "message": "Your export is ready",
    "metadata": { /* notification-specific */ }
  }
}
```

---

## 5. Internal/Admin API Contract

These endpoints are for platform-to-runtime integration and require privileged authentication.

### Authentication

All internal/admin endpoints require one of:
- `X-Mozaiks-App-Admin-Key` header
- `X-Internal-API-Key` header

### 5.1 Subscription Sync

**`POST /api/internal/subscription/sync`**

Allows platform to push subscription state updates to runtime.

**Request**:
```json
{
  "userId": "user_123",
  "plan": "free|pro|enterprise",
  "status": "active|canceled|past_due|trialing",
  "billingCycle": "monthly|yearly",
  "nextBillingDate": "2026-02-22",
  "trialEndDate": "2026-02-01",
  "stripeSubscriptionId": "sub_xxx",
  "appId": "app_001"
}
```

**Response**:
```json
{
  "success": true,
  "user_id": "user_123",
  "plan": "pro"
}
```

### 5.2 User Administration

#### List Users

**`GET /__mozaiks/admin/users`**

**Query Params**: `?page=1&limit=20&search=...`

**Response**:
```json
{
  "items": [
    {
      "id": "user_123",
      "username": "john_doe",
      "email": "john@example.com",
      "disabled": false,
      "createdAt": "2026-01-01T00:00:00Z",
      "lastLoginAt": "2026-01-23T12:00:00Z"
    }
  ],
  "page": 1,
  "limit": 20,
  "total": 100,
  "pages": 5
}
```

#### User Actions

**`POST /__mozaiks/admin/users/action`**

**Request**:
```json
{
  "action": "suspendUser|unsuspendUser|resetPassword",
  "targetIds": ["user_123", "user_456"],
  "params": { /* action-specific */ }
}
```

### 5.3 Notification Administration

#### Broadcast Notification

**`POST /__mozaiks/admin/notifications/broadcast`**

**Request**:
```json
{
  "notification_type": "announcement",
  "title": "System Update",
  "message": "New features available!",
  "target": {
    "type": "all|subscription|user_ids",
    "tier": "premium",
    "user_ids": ["user_123"]
  },
  "channels": ["in_app", "email"]
}
```

### 5.4 Status & Analytics

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `GET /__mozaiks/admin/status` | GET | Runtime status and health details |
| `GET /__mozaiks/admin/analytics` | GET | Usage analytics |

---

## 6. Plugin Execution Contract

### Entry Point

Every plugin must implement:

```python
async def execute(data: dict) -> dict:
    """Main entry point - ALL requests come here."""
    pass
```

### Request Flow

**`POST /api/execute/{plugin_name}`**

**Auth**: User JWT

**Request** (client-provided):
```json
{
  "action": "list|create|update|delete|get_settings|save_settings|...",
  // ... action-specific fields
}
```

### Injected Context

The runtime injects these fields (server-derived, cannot be spoofed by client):

```python
data = {
    # Always present
    "user_id": "user_123",      # From JWT
    "app_id": "app_001",        # From runtime config
    
    # Full context object
    "_context": {
        "app_id": "app_001",
        "user_id": "user_123",
        "username": "john_doe",
        "roles": ["user", "admin"],
        "is_superadmin": False
    },
    
    # Client-provided fields
    "action": "list",
    # ... other client fields
}
```

### Response Contract

**Success**: Return action-specific dict
```json
{
  "items": [...],
  "count": 10
}
```

**Error**: Return dict with `error` key
```json
{
  "error": "Item not found",
  "error_code": "NOT_FOUND"
}
```

### Plugin Isolation Rules

1. Plugins must not depend on each other
2. Plugins must scope all DB queries by `user_id` and/or `app_id`
3. Plugins must handle errors gracefully (return dict, never raise)
4. All database operations must be async

---

## 7. Event Contract

### Internal Event Bus

Plugins and core components communicate via the event bus.

#### Publishing Events

```python
from core.event_bus import event_bus

await event_bus.publish("plugin_name:item_created", {
    "user_id": user_id,
    "item_id": item_id
})
```

#### Subscribing to Events

```python
from core.event_bus import on_event

@on_event("user:registered")
async def handle_user_registered(data):
    user_id = data["user_id"]
    # ...
```

### Standard Event Types

| Event | Payload |
|-------|---------|
| `user:registered` | `{ "user_id": "...", "username": "..." }` |
| `user:profile_updated` | `{ "user_id": "...", "changes": {...} }` |
| `subscription:changed` | `{ "user_id": "...", "plan": "...", "status": "..." }` |
| `{plugin}:item_created` | `{ "user_id": "...", "item_id": "..." }` |
| `{plugin}:item_deleted` | `{ "user_id": "...", "item_id": "..." }` |

---

## 8. Versioning & Compatibility

### Contract Version

- **Current Version**: `1.0.0`
- **Header**: All HTTP responses include `X-Mozaiks-Runtime-Version: 1.0`

### Versioning Rules

| Change Type | Version Bump | Notice Period |
|-------------|--------------|---------------|
| Breaking change | Major (1.x → 2.0) | 6 months deprecation |
| New endpoint/field | Minor (1.0 → 1.1) | None required |
| Bug fix | Patch (1.0.0 → 1.0.1) | None required |

### Breaking Change Definition

A breaking change is any of:
- Removing an endpoint
- Removing a required response field
- Adding a required request field
- Changing the type of an existing field
- Changing error response codes for existing conditions

### Backward Compatibility Guarantees

1. Existing endpoints will not be removed without deprecation
2. Response schemas will only have additive changes (new optional fields)
3. Error codes will remain stable
4. Auth mechanisms will remain backward compatible

### Deprecation Process

1. Announce deprecation in release notes
2. Add `Deprecation` header to affected endpoints
3. Document migration path
4. Remove after notice period

---

## 9. Environment Configuration

### Required Variables

| Variable | Purpose | Required When |
|----------|---------|---------------|
| `MOZAIKS_APP_ID` | App identifier | Production |
| `MONGODB_URI` or `DATABASE_URI` | Database connection | Always |
| `JWT_SECRET` | JWT signing key | `local` auth mode |
| `MOZAIKS_JWKS_URL` | JWKS endpoint | `external` auth mode |

### Optional Variables

| Variable | Purpose | Default |
|----------|---------|---------|
| `APP_TIER` | Subscription tier | `free` |
| `MOZAIKS_AUTH_MODE` | Auth mode | `external` |
| `MOZAIKS_ISSUER` | Expected JWT issuer | None (skip validation) |
| `MOZAIKS_AUDIENCE` | Expected JWT audience | None (skip validation) |
| `MOZAIKS_APP_ADMIN_KEY` | Admin API key | None |
| `INTERNAL_API_KEY` | Internal API key | None |
| `MONETIZATION` | Enable monetization | `0` |
| `ENV` | Environment name | `development` |
| `PORT` | HTTP port | `8080` |
| `FRONTEND_URL` | CORS origin | `http://localhost:5173` |
| `MOZAIKS_AGUI_ENABLED` | Enable AG-UI dual-emit (`true`/`false`) | `true` |
| `MOZAIKS_ARTIFACT_STATE_TTL_SECONDS` | TTL (seconds) for cached artifact state | None (no expiry) |

### Auth Mode Configuration

#### External Mode (OIDC)
```env
MOZAIKS_AUTH_MODE=external
MOZAIKS_JWKS_URL=https://auth.example.com/.well-known/jwks.json
MOZAIKS_ISSUER=https://auth.example.com
MOZAIKS_AUDIENCE=api://mozaiks
```

#### Local Mode (Development)
```env
MOZAIKS_AUTH_MODE=local
JWT_SECRET=your-secret-key-min-32-chars
JWT_ALGORITHM=HS256
```

---

## 10. Open Questions

These items require confirmation from mozaiks-platform before finalizing the contract:

### Identity Model
- [ ] Confirm platform is authoritative for `app_id`
- [ ] Confirm platform issues JWTs for user authentication

### Authentication
- [ ] Confirm platform will use OIDC/JWKS
- [ ] Confirm JWT claim mapping (`sub` → `user_id`)

### Subscription Sync
- [ ] Confirm platform pushes to runtime (vs. runtime polling)
- [ ] Confirm subscription sync payload schema

### Build Events
- [ ] Should runtime POST build events to platform?
- [ ] Or should platform poll runtime for build status?
- [ ] Define build event endpoint location

### Versioning
- [ ] Accept semver + 6-month deprecation windows?
- [ ] Confirm `X-Mozaiks-Runtime-Version` header approach

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.1.0 | 2026-01-30 | Add core artifact primitives + theming variables |
| 1.0.0 | 2026-01-23 | Initial contract proposal |

---

## Appendix A: Error Response Schema

All error responses follow this schema:

```json
{
  "detail": "Human-readable error message",
  "error_code": "MACHINE_READABLE_CODE",
  "status_code": 400
}
```

Standard error codes:
| Code | HTTP Status | Meaning |
|------|-------------|---------|
| `UNAUTHORIZED` | 401 | Missing or invalid auth |
| `FORBIDDEN` | 403 | Insufficient permissions |
| `NOT_FOUND` | 404 | Resource not found |
| `CONFLICT` | 409 | Prerequisites not met |
| `INTERNAL_ERROR` | 500 | Unexpected error |

---

## Appendix B: WebSocket Message Schema

### Server → Client Envelope
```json
{
  "type": "string (required)",
  "data": "object (required)",
  "timestamp": "string ISO8601 (required)"
}
```

### Client → Server Envelope
```json
{
  "type": "string (required)",
  // ... type-specific fields
}
```

