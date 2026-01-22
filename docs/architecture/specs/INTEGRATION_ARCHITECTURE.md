# MozaiksAI Integration Architecture (Source of Truth)

## TL;DR — The Two Integration Points

Every app built with Mozaiks has two integration points:

| Layer | What | How It's Integrated | Lives Where |
|-------|------|---------------------|-------------|
| **Frontend** | ChatUI (widget/overlay) | Embedded as npm package | Inside the app's React code |
| **Backend** | MozaiksAI Runtime | Called as an API service | Separate service (hosted or self-host) |

**Key insight**: ChatUI is *code you ship inside your app*. Runtime is a *service your app talks to*.

---

## Visual: The Full Stack

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         USER'S BROWSER                                      │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │                      MOZ-UI (React App)                               │  │
│  │  ┌─────────────────┐  ┌─────────────────────────────────────────────┐ │  │
│  │  │   App Pages     │  │           ChatUI (npm package)              │ │  │
│  │  │   (your UI)     │  │  ┌─────────┐  ┌─────────┐  ┌─────────────┐  │ │  │
│  │  │                 │  │  │ Widget  │  │ Overlay │  │  Artifacts  │  │ │  │
│  │  │                 │  │  └─────────┘  └─────────┘  └─────────────┘  │ │  │
│  │  └─────────────────┘  └───────────────────┬───────────────────────┘ │  │
│  └───────────────────────────────────────────┼───────────────────────────┘  │
└──────────────────────────────────────────────┼──────────────────────────────┘
                                               │ WebSocket + HTTP
                                               ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         YOUR INFRASTRUCTURE                                 │
│                                                                             │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │                     API Gateway / Load Balancer                       │  │
│  │                (CORS, rate limiting, auth validation)                 │  │
│  └───────────────────┬───────────────────────────────────┬───────────────┘  │
│                      │                                   │                  │
│                      ▼                                   ▼                  │
│  ┌───────────────────────────────┐   ┌───────────────────────────────────┐  │
│  │   MOZ-Microservices (.NET)    │   │   MozaiksAI Runtime (Python)      │  │
│  │                               │   │                                   │  │
│  │  • Identity / Auth            │   │  • Workflow execution (AG2)       │  │
│  │  • Billing / Subscriptions    │   │  • WebSocket transport            │  │
│  │  • Entitlements (who can run  │   │  • Chat persistence (Mongo)       │  │
│  │    which workflows)           │   │  • Token accounting               │  │
│  │  • Session Broker endpoint    │◄──┤  • Artifact events                │  │
│  │    (returns chat_id + token)  │   │                                   │  │
│  │                               │   │  Validates tokens from .NET       │  │
│  └───────────────────────────────┘   └───────────────────────────────────┘  │
│                      │                                   │                  │
│                      └───────────────┬───────────────────┘                  │
│                                      ▼                                      │
│                        ┌─────────────────────────┐                          │
│                        │       MongoDB           │                          │
│                        │  (chat logs, sessions)  │                          │
│                        └─────────────────────────┘                          │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Integration Pattern 1: Frontend (ChatUI into MOZ-UI)

### What is ChatUI?
- A **React component library** (widget, overlay, artifact panels, mode switching)
- Ships as an **npm package** (or internal monorepo package)
- Runs **inside the user's browser** as part of MOZ-UI

### How to integrate (for your agents)

**Step 1: Add ChatUI as a dependency**
```bash
# In MOZ-UI repo
npm install @mozaiks/chatui
# or for monorepo: add as workspace dependency
```

**Step 2: Wrap your app with the ChatUI provider**
```jsx
// MOZ-UI: src/App.jsx
import { ChatUIProvider } from '@mozaiks/chatui';

function App() {
  return (
    <ChatUIProvider
      config={{
        api: { baseUrl: 'https://runtime.mozaiks.io' },
        ws: { baseUrl: 'wss://runtime.mozaiks.io' },
        chat: { defaultAppId: 'your-app-id' }
      }}
      authAdapter={yourAuthAdapter}  // Gets tokens from MOZ-Microservices
    >
      <YourAppRoutes />
    </ChatUIProvider>
  );
}
```

**Step 3: ChatUI surfaces appear automatically**
- Widget/bubble shows on all pages (if enabled)
- Host app can trigger overlay via:
  ```js
  window.mozaiksChat.open({ mode: 'workflow', chat_id: 'abc123' });
  ```

### What ChatUI needs from MOZ-UI
| Need | Who Provides | How |
|------|--------------|-----|
| Auth token | MOZ-Microservices | `authAdapter.getToken()` |
| User identity | MOZ-Microservices | Token claims (`sub`, `tenant`) |
| Runtime URL | Config | Environment variable |
| App ID | Config | Per-app setting |

---

## Integration Pattern 2: Backend (MOZ-Microservices ↔ MozaiksAI Runtime)

### What is MozaiksAI Runtime?
- A **Python service** (FastAPI + WebSocket)
- Runs **on a server** (not in browser, not embedded in .NET)
- Your apps **call it** over HTTP/WS; they don't embed it

### The "Session Broker" pattern (recommended)

Your .NET microservices should expose a **session broker endpoint** that:
1. Validates the user's request (auth, entitlements)
2. Calls the MozaiksAI runtime to create/resume a chat session
3. Returns the `chat_id` and any tokens the frontend needs

```
┌──────────────┐         ┌─────────────────────┐         ┌──────────────────┐
│   MOZ-UI     │         │  MOZ-Microservices  │         │ MozaiksAI Runtime│
│  (browser)   │         │      (.NET)         │         │    (Python)      │
└──────┬───────┘         └──────────┬──────────┘         └────────┬─────────┘
       │                            │                             │
       │  1. "Start workflow X"     │                             │
       │ ─────────────────────────► │                             │
       │                            │                             │
       │                            │  2. Check entitlements      │
       │                            │     (can user run X?)       │
       │                            │                             │
       │                            │  3. Create/resume session   │
       │                            │ ────────────────────────────►
       │                            │                             │
       │                            │  4. { chat_id, status }     │
       │                            │ ◄────────────────────────────
       │                            │                             │
       │  5. { chat_id, ws_url,     │                             │
       │       token }              │                             │
       │ ◄───────────────────────── │                             │
       │                            │                             │
       │  6. Connect WebSocket directly to runtime                │
       │ ─────────────────────────────────────────────────────────►
       │                            │                             │
```

### .NET Session Broker endpoint (example shape)

```csharp
// POST /api/workflows/start
public class StartWorkflowRequest {
    public string WorkflowName { get; set; }
    public string? ChatId { get; set; }  // null = new, value = resume
}

public class StartWorkflowResponse {
    public string ChatId { get; set; }
    public string WorkflowName { get; set; }
    public string RuntimeWsUrl { get; set; }  // e.g., wss://runtime.mozaiks.io/ws/...
    public string RuntimeToken { get; set; }  // Short-lived token for runtime auth
}
```

### What .NET needs from MozaiksAI Runtime
| Operation | Runtime Endpoint | Purpose |
|-----------|------------------|---------|
| Start new session | `POST /api/sessions` | Create chat_id |
| Resume session | `GET /api/sessions/{chat_id}` | Get session state |
| List user sessions | `GET /api/sessions?app_id=X&user_id=Y` | Show history |
| Check workflow eligibility | `GET /api/workflows/{name}/eligible` | Gating logic |

### What MozaiksAI Runtime needs from .NET
| Need | How |
|------|-----|
| Token validation | Runtime calls .NET's `/api/auth/validate` OR validates JWT signature directly |
| User identity | Passed in token claims or headers |
| Tenant/app context | `app_id` in URL or header |

---

## Deployment Options

### Option A: Hosted Runtime (Recommended for Mozaiks + User Apps)

```
┌─────────────────────────────────────────────────────────────────┐
│                    MOZAIKS CLOUD                                │
│                                                                 │
│   ┌─────────────────────┐    ┌─────────────────────────────┐   │
│   │  MOZ-Microservices  │    │  MozaiksAI Runtime          │   │
│   │  (.NET)             │    │  (Python, multi-tenant)     │   │
│   │                     │    │                             │   │
│   │  Scales per tenant  │    │  Scales per workflow load   │   │
│   └─────────────────────┘    └─────────────────────────────┘   │
│                                                                 │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │                    MongoDB Atlas                        │   │
│   │              (shared, isolated by app_id)               │   │
│   └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘

User apps connect to Mozaiks Cloud:
  - ChatUI (in their browser) → wss://runtime.mozaiks.io
  - Their backend (optional) → https://api.mozaiks.io
```

**Why this is scalable:**
- One runtime cluster serves all user apps
- Isolation via `app_id` + `user_id` (multi-tenant)
- You scale the runtime based on total workflow load, not per-app

**Why this is secure:**
- Users never see runtime internals
- All auth flows through your .NET gateway
- Token accounting tracks usage per app

### Option B: Self-Hosted Runtime (For power users / enterprise)

```
┌─────────────────────────────────────────────────────────────────┐
│                 CUSTOMER'S INFRASTRUCTURE                       │
│                                                                 │
│   ┌─────────────────────┐    ┌─────────────────────────────┐   │
│   │  Their Backend      │    │  MozaiksAI Runtime          │   │
│   │  (any language)     │    │  (self-hosted container)    │   │
│   └─────────────────────┘    └─────────────────────────────┘   │
│                                                                 │
│   ┌─────────────────────────────────────────────────────────┐   │
│   │                 Their MongoDB                           │   │
│   └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

**What you ship:**
- Docker image: `mozaiks/runtime:latest`
- Docker Compose for easy local setup
- Helm chart for Kubernetes (optional)

**What they provide:**
- MongoDB instance
- OpenAI API key (or compatible LLM endpoint)
- Their own auth integration

---

## Summary: What Goes Where

| Component | Embedded or Called? | Ships As | Lives In |
|-----------|---------------------|----------|----------|
| ChatUI | **Embedded** | npm package | User's React app |
| MozaiksAI Runtime | **Called** | Docker image / hosted API | Separate service |
| Workflow JSONs | **Config** | Files in runtime | Runtime's `workflows/` dir |
| MOZ-Microservices | **Embedded** (your code) | .NET service | Your infrastructure |

---

## For Your Agents: Integration Checklist

When an agent adds MozaiksAI to any app:

### Frontend Integration (ChatUI)
- [ ] Add `@mozaiks/chatui` to package.json
- [ ] Wrap app in `<ChatUIProvider>` with config
- [ ] Pass auth adapter that gets tokens from the platform backend
- [ ] Configure runtime URLs (ws + http)
- [ ] Decide widget behavior (always visible? which pages?)

### Backend Integration (Runtime)
- [ ] Deploy runtime OR point to hosted runtime URL
- [ ] Implement session broker endpoint in platform backend
- [ ] Configure auth validation (JWT or callback)
- [ ] Set up MongoDB (or use hosted)
- [ ] Configure `app_id` for multi-tenant isolation

### Workflow Integration (if custom workflows)
- [ ] Add workflow JSON files to runtime's `workflows/` directory
- [ ] Add workflow UI components to ChatUI's `workflows/` directory (if needed)
- [ ] Register workflow in `_pack/workflow_graph.json`

---

## Quick Reference: URLs and Endpoints

### Hosted Runtime (Mozaiks Cloud)
```
WebSocket: wss://runtime.mozaiks.io/ws/{workflow}/{app_id}/{chat_id}/{user_id}
HTTP API:  https://runtime.mozaiks.io/api/...
```

### Self-Hosted Runtime
```
WebSocket: wss://{customer-domain}/ws/{workflow}/{app_id}/{chat_id}/{user_id}
HTTP API:  https://{customer-domain}/api/...
```

### MOZ-Microservices (your .NET backend)
```
Session Broker: POST /api/workflows/start
Auth:           POST /api/auth/token
Entitlements:   GET  /api/users/{id}/entitlements
```

---

## Why This Pattern Works

1. **Scalable**: Runtime is a service, not embedded code. One runtime serves many apps.
2. **Secure**: Auth lives in your .NET layer. Runtime just validates tokens.
3. **Pluggable**: ChatUI is a package. Drop it into any React app.
4. **Self-host friendly**: Ship a Docker image. Users bring their own infra.
5. **Agent-friendly**: Clear boundaries = clear instructions for automation.

---

## Next Steps (Recommended Order)

1. **Package ChatUI as npm** (or keep as monorepo workspace package)
2. **Containerize MozaiksAI Runtime** (Dockerfile already exists?)
3. **Implement Session Broker in .NET** (the glue endpoint)
4. **Document runtime API** (OpenAPI spec for your agents)
5. **Set up hosted runtime** (single deployment for all Mozaiks apps)



# MozaiksAI Integration Checklist (Working Document)

> **Purpose**: Track progress on making MozaiksAI plug-and-play for Moz-UI, MOZ-Microservices, and future customer apps.
> 
> **Last Updated**: 2024-12-20
> 
> **Related Docs**:
> - [INTEGRATION_ARCHITECTURE.md](./INTEGRATION_ARCHITECTURE.md) — How everything connects
> - [REPO_PACKAGING.md](./REPO_PACKAGING.md) — What goes in which repo

---

## Phase 1: Frontend (ChatUI) — MOSTLY DONE ✅

### Core UX
- [x] Widget mode (small persistent chat)
- [x] Full-screen overlay mode
- [x] 3-surface contract (bubble → widget → overlay)
- [x] Ask Mozaiks mode (general chat)
- [x] Workflow mode (artifact-capable)
- [x] Mode switching without route changes
- [x] Artifact panel (desktop split + mobile drawer)

### Host App Integration API
- [x] `window.mozaiksChat.open({ mode, chat_id, workflow_name })`
- [x] `window.mozaiksChat.close()`
- [x] Event-based: `mozaiks:chat:open`, `mozaiks:chat:close`
- [x] Widget calls host API (not legacy navigation)

### Build Quality
- [x] Production build passes
- [x] No ESLint warnings (duplicate keys fixed, hook deps fixed)

### Packaging (NOT STARTED)
- [ ] Create `package.json` for ChatUI as publishable package
- [ ] Export `ChatUIProvider`, `useChatUI`, key components
- [ ] Create `index.js` entry point for package consumers
- [ ] Test: can another React app `import { ChatUIProvider } from '@mozaiks/chatui'`?
- [ ] Decide: npm publish vs monorepo workspace vs git submodule

### Documentation
- [x] Integration plan doc (`MOZ_UI_DOTNET_INTEGRATION_PLAN.md`)
- [x] 3-surface contract documented
- [ ] ChatUI README for package consumers
- [ ] Props/config reference for `<ChatUIProvider>`

---

## Phase 2: Backend (Runtime) — PARTIALLY DONE

### Docker / Deployment
- [x] Multi-stage Dockerfile (smaller images)
- [x] Production docker-compose
- [x] Non-root user in container
- [x] Health check endpoint (`/api/health`)
- [x] Deployment guide (`infra/DEPLOYMENT.md`)

### Configuration
- [ ] Create `.env.example` with all required/optional vars
- [ ] Document which env vars are required vs optional
- [ ] Validate env vars on startup (fail fast if missing)

### API Endpoints (Session Broker Support)
- [x] `GET /api/health` — Health check
- [x] `GET /api/sessions?app_id=X&user_id=Y` — List sessions (exists?)
- [ ] Verify: `POST /api/sessions` — Create session (does it exist?)
- [ ] Verify: `GET /api/sessions/{chat_id}` — Get session state
- [ ] Verify: Endpoint to check workflow eligibility

### OpenAPI / Documentation
- [ ] Generate OpenAPI spec from FastAPI
- [ ] Export to `docs/api/openapi.json`
- [ ] Runtime API README for consumers

### Auth / Security (NOT STARTED)
- [ ] Define auth strategy: JWT validation vs gateway-forwarded headers
- [ ] Implement JWT validation middleware
- [ ] Token claims: `sub`, `tenant`, `app_id` scopes
- [ ] Reject requests without valid auth (except health check)
- [ ] Rate limiting (or document gateway requirement)

### Multi-Tenant Isolation
- [x] `app_id` / `user_id` / `chat_id` enforced in persistence
- [ ] Audit: can one user access another user's chat?
- [ ] Audit: can one app access another app's data?

---

## Phase 3: .NET Integration (NOT STARTED)

> This work happens in MOZ-Microservices repo, not MozaiksAI.

### Session Broker Endpoint
- [ ] `POST /api/workflows/start` — Create/resume workflow session
  - Input: `{ workflow_name, chat_id? }`
  - Output: `{ chat_id, workflow_name, runtime_ws_url, runtime_token }`
- [ ] Calls MozaiksAI runtime to create session
- [ ] Returns short-lived token for runtime auth

### Entitlements
- [ ] `GET /api/users/{id}/entitlements` — What workflows can user run?
- [ ] Enforce entitlements before starting workflow

### Token Issuance
- [ ] Mint JWT for runtime auth
- [ ] Include claims: `sub`, `app_id`, `tenant`
- [ ] Short expiry (e.g., 15 min) for runtime tokens

---

## Phase 4: End-to-End Testing (NOT STARTED)

### Integration Tests
- [ ] Moz-UI can open ChatUI overlay via host API
- [ ] ChatUI connects to runtime WebSocket
- [ ] Workflow runs and artifacts display
- [ ] Ask mode works with chat history
- [ ] Token accounting logs correctly

### Security Tests
- [ ] Unauthenticated request rejected
- [ ] User A cannot access User B's chat
- [ ] App A cannot access App B's data

### Load Tests
- [ ] Multiple concurrent WebSocket connections
- [ ] Multiple concurrent workflows
- [ ] Token accounting under load

---

## Quick Reference: File Locations

| What | Where |
|------|-------|
| ChatUI source | `ChatUI/src/` |
| ChatUI context/provider | `ChatUI/src/context/ChatUIContext.js` |
| ChatUI widget | `ChatUI/src/components/chat/PersistentChatWidget.js` |
| ChatUI main page | `ChatUI/src/pages/ChatPage.js` |
| Runtime entry | `shared_app.py` |
| Runtime core | `core/` |
| Workflows | `workflows/` |
| Docker infra | `infra/docker/`, `infra/compose/` |
| Integration docs | `docs/source_of_truth/` |

---

## Next Session Starting Point

**If context is lost, start here:**

1. Read `docs/source_of_truth/INTEGRATION_ARCHITECTURE.md` for the big picture
2. Read this checklist for what's done vs pending
3. Pick the next unchecked item and continue

**Current priority order:**
1. `.env.example` (5 min, quick win)
2. Package ChatUI as npm (30 min, high value)
3. JWT validation in runtime (1 hr, security requirement)
4. Session broker endpoints in .NET (separate repo)

---

## Notes / Decisions Log

| Date | Decision |
|------|----------|
| 2024-12-20 | Chose "Runtime as a Service" model (not embedded) |
| 2024-12-20 | ChatUI is embedded code; Runtime is called service |
| 2024-12-20 | Session broker lives in .NET, not runtime |
| 2024-12-20 | 3-surface UX: bubble → widget (Ask only) → overlay (Ask/Workflow) |
