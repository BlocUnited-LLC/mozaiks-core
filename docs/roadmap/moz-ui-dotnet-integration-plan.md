# Moz-UI + .NET Microservices Integration Plan (ChatUI + MozaiksAI Runtime)

## What you asked for (goal)

- **Ship ChatUI as a plugin-like capability** inside your Moz-UI React app.
- **Chat is always available** (widget mode), and can **expand to a full-screen overlay** (no route change required).
- Support **two conversation modes**:
  - **Workflow mode** (artifact-capable, runs declarative workflows)
  - **Ask Mozaiks mode** (general assistant, persistent across the product)
- Integrate with **your real platform backend** (your .NET microservices), not the demo auth/backend assumptions in this repo.

This doc is a handoff-style plan you can give to:
- The **.NET microservices team** (identity, tenancy, tokens, gateway)
- The **Moz-UI frontend team** (embed + configuration + auth wiring)

---

## Quick clarification: “bubble click”

In widget mode, when the chat is **minimized**, the UI becomes a **single round-ish icon/button** (Mozaiks logo) anchored near the bottom-right.

- That minimized icon is what I called the **“bubble”**.
- **“Bubble click”** means: clicking that minimized icon to reopen the widget UI.

With the new overlay work, we already added a **“full screen” button** when the widget is expanded; optionally we can make the minimized bubble open the overlay directly.

---

## System boundary (what lives where)

### Moz-UI (React app)
Owns:
- Your application shell, navigation, theming, and global state
- Deciding where chat should be available (ideally globally)
- Auth token acquisition (from .NET services) and token refresh UX

Integrates:
- The **ChatUI bundle** (from this repo, eventually packaged)

### .NET microservices (platform backend)
Owns:
- **Identity/authentication** (JWT issuance, refresh tokens, session mgmt)
- **Authorization** (who can access which `app_id` / org / tenant)
- **Billing/entitlements** (subscription, usage limits)
- **API gateway** policies (rate limits, CORS, WAF rules)

### MozaiksAI Runtime (this repo’s FastAPI runtime)
Owns:
- Workflow execution (AG2/autogen group chat)
- WebSocket transport + event stream
- Mongo persistence (chat logs, session state)
- Observability hooks

Non-goals:
- It should **not** become your payment/auth backend.

---

## Key identifiers (multi-tenant contract)

These IDs must be treated as first-class and carried end-to-end:

- `app_id`: which app/workspace in Mozaiks (tenant boundary)
- `user_id`: end user identity (also boundary)
- `chat_id`: specific chat session (state/persistence)
- `workflow_name`: selected workflow (workflow mode only)

Rule: **Never mix state across different (`app_id`, `user_id`, `chat_id`)**.

---

## Transport contract (Moz-UI/ChatUI ↔ Runtime)

### WebSocket endpoint (runtime)
- The runtime exposes a WS path shaped like:
  - `/ws/{workflow_name}/{app_id}/{chat_id}/{user_id}`

### Event protocol
- The UI and runtime exchange JSON events (the `chat.*` family and tool events).
- Workflow mode produces artifact-capable UI tool events that render in the Artifact Panel.

Reference docs in this repo:
- `docs/reference/event_reference.md`
- `docs/frontend/chatui_architecture.md`

---

## ChatUI modes (what the UX means)

### Workflow mode
- Runs a declarative workflow (e.g., “Generator”, “ValidationEngine”, etc.).
- Can emit **artifact UI** which shows in the artifact panel (like canvas/workspace).
- The runtime orchestrates agents via AG2.

### Ask Mozaiks mode
- General assistant mode.
- Persists chat sessions and supports transcript lists (Ask history).
- No artifacts (by default), unless you choose to enable them.

---

## Widget + overlay behavior (current implementation)

### The 3-surface contract (what to build against)

- **Bubble (minimized)**: a small Mozaiks icon/button. Clicking it opens the widget.
- **Widget (small)**: always-available surface meant for **Ask-only** usage and quick access.
- **Full-screen overlay**: the “deep work” surface. **All workflows should run here** (artifacts, split layout, mobile drawer).

### Widget mode
- When the user is on non-chat pages, ChatUI can render as a **persistent widget**.
- Widget can be **minimized** into the “bubble”.

### Full-screen overlay
- In widget mode, the user can expand into a **full-screen overlay**.
- Overlay reuses the same layouts as the normal chat page:
  - Desktop split layout with artifact panel
  - Mobile drawer layout
- Overlay supports:
  - `Esc` to close
  - body scroll lock while open

### Host app can trigger overlay (Moz-UI integration)

ChatUI exposes a small “host control” API so your creators’ app UI can trigger chat surfaces.

**Event-based API (recommended)**

Open Ask overlay:

```js
window.dispatchEvent(
   new CustomEvent('mozaiks:chat:open', { detail: { mode: 'ask' } })
);
```

Open Workflow overlay for an existing workflow chat session (preferred):

```js
window.dispatchEvent(
   new CustomEvent('mozaiks:chat:open', {
      detail: {
         mode: 'workflow',
         chat_id: 'abc123',
         workflow_name: 'AgentGenerator',
      },
   })
);
```

Close overlay:

```js
window.dispatchEvent(new Event('mozaiks:chat:close'));
```

**Imperative helper (convenience)**

ChatUI also registers `window.mozaiksChat`:

```js
window.mozaiksChat.open({ mode: 'ask' });
window.mozaiksChat.open({ mode: 'workflow', chat_id: 'abc123', workflow_name: 'AgentGenerator' });
window.mozaiksChat.close();
```

**Important**: for workflow mode, your host UI should ideally provide a `chat_id` (e.g., returned from your workflow-start endpoint). If no `chat_id` is provided, ChatUI falls back to “resume oldest in-progress workflow”.

Implementation entry point:
- The overlay is rendered inside ChatPage (global orchestrator), controlled by `isChatOverlayOpen`.

---

## Integration options (how Moz-UI consumes ChatUI)

### Option A (recommended for Moz-UI): “internal package”
- Pull ChatUI in as a dependency (monorepo workspace, git submodule, or private npm package).
- Pros: best DX, best theming integration, same-origin cookies possible.
- Cons: your bundling pipeline must accept it.

### Option B: iframe embed
- Host ChatUI as its own web app and embed via iframe.
- Pros: easiest for external customers; strongest isolation.
- Cons: auth and event bridging becomes more complex; theming less native.

For Moz-UI itself, Option A is the normal path.

---

## Auth strategy (what .NET needs to provide)

ChatUI should not invent auth. It should consume auth from Moz-UI.

### What Moz-UI provides to ChatUI
- A **short-lived access token** (JWT) for runtime calls.
- Optionally a refresh mechanism (silent refresh) owned by Moz-UI.

### What runtime should validate
- WS handshake and REST requests must validate `Authorization: Bearer <token>`.
- Token should include:
  - `sub` (user)
  - tenant/org claims
  - allowed `app_id` scopes (or a separate ACL lookup)

If you want a gateway-managed approach:
- Put runtime behind an API gateway that enforces auth, and forward identity headers to runtime.

---

## Concrete tasks by team

### 1) Moz-UI frontend tasks

1. **Decide embed shape**
   - Global widget always present.
   - Full-screen overlay for deep workflow runs.

2. **Provide ChatUI config at runtime**
   - WS base URL
   - HTTP base URL
   - default `app_id`
   - current user identity (`user_id`) from Moz-UI session

3. **Auth adapter wiring**
   - Ensure ChatUI sends `Authorization` for HTTP and WS.
   - Ensure token refresh is handled by Moz-UI, not the runtime.

4. **State ownership**
   - Moz-UI decides when widget mode is enabled.
   - ChatUI owns chat-level state and transcripts.

5. **Observability**
   - Optionally forward correlation IDs to your logging (e.g., `chat_id`).

### 2) .NET microservices tasks

1. **Identity / Token issuance**
   - Mint JWT used by ChatUI to connect to runtime.

2. **Entitlements**
   - Provide a claim or a lookup endpoint that determines:
     - which workflows a user can run
     - token usage quotas / gating (platform-level)

3. **Gateway policies**
   - CORS for Moz-UI origin(s)
   - WS upgrade support
   - Rate limiting and abuse protection

4. **Optional: “Session broker” endpoint**
   - A platform endpoint that returns (app_id, chat_id, workflow) and a token
   - Makes the frontend simpler and reduces runtime coupling

### 3) Runtime tasks (this repo)

1. **Auth validation**
   - Enforce JWT validation consistently for WS + REST.

2. **Tenancy enforcement**
   - Strictly scope persistence and reads by `app_id`, `user_id`, `chat_id`.

3. **Workflow discovery stays hot-swappable**
   - No hardcoded Moz-specific routing.

4. **Token accounting hooks remain intact**
   - Ensure all agent work is attributable to (`app_id`, `user_id`).

---

## Suggested rollout sequence (lowest risk)

1. **Moz-UI embeds ChatUI widget** (existing behavior), no overlay required.
2. Enable **overlay expansion** for workflows (already implemented in this repo).
3. Wire **real auth** from .NET to runtime.
4. Turn on **workflow mode + artifacts** for a single workflow.
5. Expand to more workflows and add customer app embedding patterns.

---

## Open decisions (quick list)

- Should the minimized bubble click:
  - (A) reopen the widget, or
  - (B) open the full-screen overlay directly?
- Should Ask Mozaiks be allowed to open artifacts (future), or stay text-only?
- Where should token/billing enforcement live:
  - gateway only, runtime only, or both?

---

## Pointers in this repo

- ChatUI orchestrator: `ChatUI/src/pages/ChatPage.js`
- Global state: `ChatUI/src/context/ChatUIContext.js`
- Widget behaviors: `ChatUI/src/hooks/useWidgetMode.js`
- Transport adapter: `ChatUI/src/adapters/api.js`
- Frontend architecture guide: `docs/frontend/chatui_architecture.md`
- Event reference: `docs/reference/event_reference.md`
