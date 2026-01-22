# MozaiksAI - Build -> Deploy -> Runtime Contract (MozaiksCore Integration) (2026-01-02)

This is a **fresh handoff spec** for MozaiksAI (the build/runtime system) so MozaiksCore (shell app) + Mozi UI can reliably:
- identify an "app/project",
- detect "build completed",
- gate deploy behind subscription (paywall at deploy step), and
- call the Runtime for workflow discovery/execution.

---

## 0) Current reality in this repo (important)

In MozaiksCore frontend today:
- Routes: `/profile`, `/plugins/:pluginName`, and `/subscriptions` (only when monetization enabled).
- There is **no** "Create App", "Projects", "Build", or "Deploy" page implemented here.
- The only "assistant/build" UI will be the **MozaiksAI chat overlay** (WebSocket chat).

---

## 1) Terminology

- `app_id` / `appId`: stable identifier for a user's generated project (database id / GUID). MozaiksAI uses `app_id` in runtime routes; MozaiksCore/.NET uses `appId` in platform APIs. Treat these as the same logical identifier.
- `build_id`: identifier for a build run.
- `owner_user_id`: the authenticated user who owns the app.

Note: MozaiksAI runtime already has `chat_id` (a single workflow run instance). For "build" workflows, you may use `build_id = chat_id` (or store both if the platform mints a separate `build_id`).

---

## 2) Required contract: app identity lifecycle

### 2.1 Create App (source of truth)

Decision (locked):
- The .NET platform is the system-of-record and mints `appId` at create-time.
- MozaiksAI must treat `appId` as authoritative and must not mint a competing id.

Confirmed (from MozUI app):
- "Create App" is a dedicated form page in Moz UI, routing to `/create-app`.
- `AppService.createApp` returns the full app object immediately and includes `appId`.
- Existing UI routes are already set up to use `:appId` (e.g., `/course/:appId`, `/conceptverification/chat/:appId`).

### 2.2 Minimal "app record" fields

MozaiksAI must be able to read/write at least:
- `app_id`
- `owner_user_id`
- `name`
- `status`: `draft | building | built | deployable | deployed | error`
- `created_at`, `updated_at`

---

## 3) Required contract: build completion

MozUI needs to show a "Build complete" / "Next steps" screen.

Decision (locked): build completion is signaled by MozaiksAI to the platform (push), with a pull fallback.

### Required (push): build status event into platform

`POST /api/apps/{appId}/build-events`

Payload (example):
```json
{
  "event_type": "build_completed",
  "appId": "app_123",
  "buildId": "bld_456",
  "status": "built",
  "completedAt": "2026-01-02T00:00:00Z",
  "artifacts": {
    "previewUrl": "https://...",
    "exportDownloadUrl": "https://..."
  }
}
```

### Fallback (pull): Build status endpoint

`GET /api/apps/{appId}/build-latest`

Response (example):
```json
{
  "app_id": "app_123",
  "build_id": "bld_456",
  "status": "built",
  "completed_at": "2026-01-02T00:00:00Z",
  "preview": { "available": true, "url": "https://..." },
  "export": { "available": true, "download_url": "https://..." },
  "logs": { "url": "https://..." }
}
```

### Platform UX behavior (required)

- Platform persists build status.
- Platform emits a real-time WebSocket event to the user (and optionally creates an in-app notification).
- UI auto-redirects the user to the Deploy step.

Canonical navigation decisions:
- After Create App: route to `/course/:appId` (primary).
- After Build Completed: route to `/deploy/:appId`.

---

## 4) Required contract: deploy gating (subscription / paywall)

Decision (locked): deploy gating is a **platform/MozaiksCore responsibility** (entitlements + billing). MozaiksAI runtime must not decide who can deploy.

Required behavior:
- The Deploy page (`/deploy/:appId`) must check whether the authenticated user is allowed to deploy for that `appId`.
- If not allowed, the UX must block deploy behind subscription (paywall) and route the user to upgrade (e.g., `/subscriptions`).
- If allowed, the platform proceeds with the deploy pipeline (platform-owned) and updates `status` to `deployed` on success (or `error` on failure).

MozaiksAI role:
- Provide "build completed" signals and artifact references (preview/export) so the platform can render the deploy step.
- Remain agnostic to subscription/plan logic.

---

## 5) Required contract: Runtime workflow discovery + execution (Core -> Runtime)

MozaiksCore should treat MozaiksAI runtime as the source of truth for workflow discovery/execution.

### 5.1 Workflow discovery (pack-aware)
Runtime endpoint:
- `GET /api/workflows/{app_id}/available?user_id={user_id}`

Expected response shape:
- `{ "workflows": [...] }` (recommended), or `[...]` (list directly).

### 5.2 Start a workflow run (creates a chat/session)
Runtime endpoint:
- `POST /api/chats/{app_id}/{workflow_name}/start`

Response includes a `chat_id` and a runtime websocket URL for streaming.

### 5.3 Stream workflow events (WebSocket)
Runtime endpoint:
- `GET /ws/{workflow_name}/{app_id}/{chat_id}/{user_id}`

MozaiksCore/Mozi UI responsibilities:
- Provide authoritative `app_id` and `user_id` from authenticated session context (do not accept these as user-typed inputs).
- Never cache or reuse workflow availability across different `app_id` or `user_id` (multi-tenant boundary).

