# Repo Packaging Guide (Source of Truth)

## MozaiksAI Building Blocks (Runtime Engine + Workflow Packs)

These are the building blocks for MozaiksAI.

- **Runtime Engine** (The Base)
  - `core/` (transport, orchestration, persistence, observability, tokens)
  - `ChatUI/` (generic React chat + artifact surfaces)
  - `shared_app.py`, `run_server.py`
  - `scripts/` (`start-dev.ps1`, `start-app.ps1`, etc.)

- **Workflows** (Modular Capabilities)
  - **AG2 Workflow** (`workflows/{workflow_name}/`):
    - An atomic, functional group chat (e.g., "FrontendGenerator", "ValueEngine").
    - **Configuration manifests**
      - `agents.json` — Agent definitions and roles
      - `context_variables.json` — Workflow-scoped context settings
      - `handoffs.json` — Inter-agent handoff rules (Standard Edges)
      - `orchestrator.json` — Orchestration behavior and termination conditions
      - `structured_outputs.json` — Output schemas for agents
      - `tools.json` — Tool manifests with argument schemas
      - `ui_config.json` — UI surface declarations
    - **Micro-Orchestrator** (`workflows/{workflow_name}/_pack/`)
      - `workflow_graph.json` — Defines nested GroupChats and decomposition logic (replaces signals.json)

    **Security note (file-backed context variables)**
    - The runtime supports `context_variables.json` entries with `source.type="file"`.
    - For safety, file reads are restricted to paths inside the repo/workspace root by default.
    - To explicitly allow outside-root file reads (trusted self-host/dev only), set:
      - `CONTEXT_FILE_ALLOW_OUTSIDE_ROOT=true`
    - **Tool implementations**
      - `workflows/{workflow_name}/tools/` — Python stub files (`.py`) implementing tool callables
    - **UI components** (optional)
      - `ChatUI/src/workflows/{workflow_name}/components/` — JavaScript stub files (`.js`) for custom UI components tied to the tool callables
  - **Workflow Pack** (`workflows/_pack/`):
    - The "Meta" layer that orchestrates multiple AG2 Workflows.
    - **Macro-Orchestrator**
      - `workflow_graph.json` — Defines the **macro dependency graph** between workflows
      - Persisted state (ChatSessions) is the source of truth for per-user workflow status (chat_id/status)

      **Macro Dependency Graph Semantics**

      - **Nodes**: Workflow IDs (e.g., `ValueEngine`, `AgentGenerator`, `AppGenerator`, `Governance`, `InvestmentAdvice`).
      - **Edges**: Directed dependencies `{ "from": "ValueEngine", "to": "AgentGenerator", "kind": "control" }`.
      - **Gating Rule** (conceptual):
        - For a given workflow `W`, let `parents(W)` be all nodes with edges `X -> W`.
        - `W` is **eligible to start** when every `X ∈ parents(W)` has at least one chat run whose status is `completed`.
      - **Example (Foundry Pack)**:
        - `ValueEngine -> AgentGenerator -> AppGenerator -> Governance` (sequential chain).
        - `InvestmentAdvice` has **no incoming edges** (can run at any time).
      - **PackRun (runtime view, not a config file)**:
        - At runtime, the orchestrator can maintain a `PackRun` record per app/app:

          ```json
          {
            "pack_run_id": "...",
            "app_id": "...",
            "workflows": {
              "ValueEngine":      { "chat_id": "ce1", "status": "completed" },
              "AgentGenerator":   { "chat_id": "ce2", "status": "completed" },
              "AppGenerator":     { "chat_id": "ce3", "status": "running" },
              "Governance":       { "chat_id": null,  "status": "not_started" },
              "InvestmentAdvice": { "chat_id": "ce4", "status": "completed" }
            }
          }
          ```

        - **Scheduler loop** (future runtime behavior): for each workflow in `not_started`, if the gating rule passes given this `PackRun`, the orchestrator creates a new chat session and marks it `running`.
      
---

## MozaiksAI Consumers (Two Distinct Paths)

MozaiksAI has two different consumers, and they are **not required to be symmetric**.

### 1️⃣ Mozaiks (Our Own Product)

```
React Frontend (Mozaiks)
   → Mozaiks Backend (.NET)
       → MozaiksAI Runtime
```

- Uses MozaiksAI **directly**
- Has its own React frontend
- Has its own .NET backend
- **Does not use MozaiksCore at all**

The Mozaiks frontend:
- Opens WebSocket connections directly to MozaiksAI
- Passes workflow ID (`AgentGenerator`, `AppGenerator`, etc.)
- Passes auth token (issued by the .NET backend)
- Passes app/org context

### 2️⃣ Customer Apps (Generated Apps)

```
Customer UI
   → MozaiksCore
       → MozaiksAI Runtime
```

- Use **MozaiksCore** as their product foundation
- MozaiksCore embeds/connects to MozaiksAI
- MozaiksCore provides the product shell (auth, billing, plugins)

**Critical distinction:**

> **MozaiksCore is not a dependency of Mozaiks.**
> It is a product foundation we ship to others.

This means:
- Mozaiks (our product) talks to MozaiksAI directly
- Customer apps talk to MozaiksAI through MozaiksCore
- Both are valid; the runtime doesn't care who the caller is

---

- `docs/source_of_truth/ECONOMIC_PROTOCOL.md`

For authentication vs authorization boundaries:

- `docs/source_of_truth/04_AUTH_BOUNDARY.md`

**Key principle:** MozaiksAI authenticates requests but does not authorize behavior. Authorization is delegated to the host control plane (Mozaiks backend, MozaiksCore, or customer app).

MozaiksAI is packaged once, but deployed in two primary modes.

### 1) Hosted Runtime SaaS (Mozaiks Cloud)

- **Mozaiks (SaaS)** uses MozaiksAI directly for Foundry workflows (ValueEngine -> AgentGenerator -> AppGenerator -> etc.).
- **MozaiksCore-based apps** consume a hosted runtime, so creators don't have to run the runtime themselves.
- The runtime (FastAPI + WebSocket) is operated as a managed service; apps point to it for agent execution and realtime events.

### 2) Self-Hosted Runtime (Customer-Operated)

- `mozaiksai-clean` is the base “runtime OS” for teams that want to run MozaiksAI themselves.
- Customers bring their own workflow packs (`workflows/_pack/` + workflow folders) and optional UI components.

---

## ChatUI Contract (Core Chat vs Host App Pages)

For a deeper breakdown of what UI belongs inside MozaiksAI (chat/artifacts) vs what belongs in the host product (MozaiksCore / marketplace / admin pages), see:

- `docs/source_of_truth/UI_OWNERSHIP_BOUNDARIES.md`

ChatUI is intentionally split so the **chat surface stays stable** while the host app can add its own pages.

- **Core chat route (stable)**: `ChatUI/src/pages/ChatPage.js`
  - This is the consistent “agent runtime UI” surface (messages, artifacts, widget, mode switching).
- **Host app pages (variable)**: `ChatUI/src/pages/*` (everything except `ChatPage.js`)
  - These pages belong to the hosting product/app (Mozaiks, MozaiksCore apps, or a 3rd-party integrator).
  - To keep the persistent chat widget available on non-chat routes, pages call `useWidgetMode()` (see `ChatUI/src/hooks/useWidgetMode.js`).
  - Reference: `ChatUI/src/pages/MyWorkflowsPage.js` calls `useWidgetMode()` and routes back to `/chat`.

### Theming (Multi-Tenant)

- **Runtime serves tenant theme**: `GET /api/themes/{app_id}`.
- **ChatUI loads/applies theme**:
  - `ChatUI/src/styles/themeProvider.js` fetches + applies CSS variables.
  - `ChatUI/src/styles/useTheme.js` is the React hook for theme access.
- **Workflow-local theme configs (optional)**: workflow UI folders may include their own theme tokens (example: `ChatUI/src/workflows/AgentGenerator/theme_config.json`).

### Workflow-Specific UI (Artifacts / Inline Components)

Some workflows ship workflow-specific UI (artifacts / inline components) as JavaScript stubs under:

- `ChatUI/src/workflows/{workflow_name}/components/*`

This does **not** change the runtime split (FastAPI runtime can still be a hosted SaaS or self-hosted), but it **does** change how ChatUI is distributed:

- **Current ChatUI behavior (build-time UI)**: `ChatUI/src/core/WorkflowUIRouter.js` loads workflow UI via dynamic import:
  - `import(\`../workflows/${workflow}/components/index.js\`)`
  - Practical implication: the workflow UI components must exist in the ChatUI build output to render.

- **Packaging implication**
  - **Fixed workflow packs** (e.g., `ag2-groupchat-generator`, Mozaiks Foundry): ship those workflow UI components in the ChatUI source and deploy that ChatUI build once.
  - **User-generated workflows** (e.g., `mozaikscore` apps): either
    - bundle/build ChatUI per generated app (so the generated workflow UI stubs ship with that app), or
    - constrain workflows to generic UI surfaces (schema-driven artifacts) unless/until a remote plugin-loader approach is introduced.

## Target Repos

### 1. `mozaiksai-clean` (clean for additional Workflows)
**Goal**: Runtime engine that anyone can embed into their app and point at their own declarative Workflow Packs.

- **Include**
  - Runtime Engine: `core/`, `ChatUI/`, `shared_app.py`, `run_server.py`
  - Workflows: EMPTY (Ready for `_pack/` and workflow folders)

- **Deployment**
  - Self-host baseline distribution (users author workflows and optional UI stubs).

---

### 2. `ag2-groupchat-generator` (Current Repo)
**Goal**: Ship the 'AgentGenerator' as a standalone product for AG2.ai or other vendors: "Design declarative AG2 groupchats here, then download artifacts."

- **Include**
  - Runtime Engine: `core/`, `ChatUI/`, `shared_app.py`, `run_server.py`
  - Workflow Pack: **AgentGenerator Pack**
    - `workflows/_pack/` (Manifest pointing to AgentGenerator workflow)
    - `workflows/AgentGenerator/` (The AgentGenerator workflow itself)
    - `ChatUI/src/workflows/AgentGenerator/components/`

- **Deployment**
  - Can be offered as Hosted SaaS (managed runtime) and/or Self-host (pack on top of `mozaiksai-clean`).

---

### 3. `mozaiks` (planned)
**Goal**: The Mozaiks "foundry" backend to go from idea -> workflows -> app integration plan.

- **Include**
  - Runtime Engine: `core/`, `ChatUI/`, `shared_app.py`, `run_server.py`
  - Workflow Pack: **Foundry Pack** (Meta-Pack)
    - `workflows/_pack/` (Defines the graph: Value -> Agent Workflows -> App)
    - `workflows/ValueEngine/` (Step 1: Concept & Value Prop)
    - `workflows/AgentGenerator/` (Step 2: Agent Architecture - Dependent on ValueEngine)
    - `workflows/AppGenerator/` (Step 3: Integration Plan - Dependent on AgentGenerator)
      - *Note: May spawn child flows like Frontend/Backend/DB*
    - `workflows/Governance/` (Independent/Parallel: Audit & Compliance)

- **Deployment**
  - Hosted SaaS (Mozaiks Cloud) uses the runtime internally.

---

### 4. `mozaikscore` (planned)
**Goal**: The base app every Mozaiks-built app generated by the user runs on: user accounts, subscriptions, core pages, plugin system, and embedded runtime.

- **Include**
  - Runtime Engine: `core/`, `ChatUI/`, `shared_app.py`, `run_server.py`
  - Workflow Pack: **User's Generated Pack**
    - `workflows/_pack/` (Generated manifest)
    - `workflows/{generated_workflow_1}/`
    - `workflows/{generated_workflow_2}/`
    - `ChatUI/src/workflows/...`

- **Deployment**
  - Hosted Runtime SaaS by default (Mozaiks-managed runtime per app/app).
  - Optional self-host path pairs `mozaikscore` with `mozaiksai-clean` when teams want to run the runtime themselves.

---

# UI Ownership Boundaries (Source of Truth)

This document defines which UI belongs in **AI Plugins** (ChatUI + artifacts) vs what should live in **Function Plugins** (product pages + APIs).

**Key Entities:**
- **MozaiksCore** = Pre-built plugin foundation (auth, billing, plugin host, theming, etc.)
- **MozaiksAI** = Embeddable runtime + ChatUI (workflows, agents, artifacts) which is embedded into MozaiksCore
- **AgentGenerator** = Agentic workflow that produces MozaiksCore **AI Plugins** (workflows, tools, artifacts)
- **AppGenerator** = Agentic workflow that produces MozaiksCore **Function Plugins** (pages, APIs, components)

```
┌─────────────────────────────────────────────────────────────────────┐
│  MozaiksCore (Pre-Built Foundation)                                 │
│  • Auth, Billing, Plugin Host, Theming, Notifications               │
│  • MozaiksAI Runtime embedded (ChatUI, WebSocket, Workflow Engine)  │
│                                                                     │
│  ┌─────────────────────────────┐  ┌─────────────────────────────┐  │
│  │  Function Plugin A          │  │  AI Plugin A                │  │
│  │  (generated by AppGenerator)│  │  (generated by AgentGen)    │  │
│  │  • Pages, APIs, Components  │  │  • Workflows, Tools, Artifacts│ │
│  └─────────────────────────────┘  └─────────────────────────────┘  │
│                                                                     │
│  ┌─────────────────────────────┐  ┌─────────────────────────────┐  │
│  │  Function Plugin B          │  │  AI Plugin B                │  │
│  │  (generated by AppGenerator)│  │  (generated by AgentGen)    │  │
│  │  • Pages, APIs, Components  │  │  • Workflows, Tools, Artifacts│ │
│  └─────────────────────────────┘  └─────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

The intent is to keep MozaiksAI **embeddable**: it provides the runtime and ChatUI that MozaiksCore uses to execute AI plugins.

---

## Core Principle: AI Plugin vs Function Plugin UI

### AI Plugin UI (Chat-Native) — Created by AgentGenerator
UI that is **best expressed as an agent conversation + tool-driven workspace**.

- The user is collaborating with agents.
- The UI state is driven by workflow/tool events.
- The "page" can change shape as the agent progresses.
- The UX is naturally tied to a **chat_id** and a workflow run.

In practice: these are **Artifacts / Inline Components** rendered by ChatUI, attached to a workflow.

### Function Plugin UI (Product-Native) — Created by AppGenerator
UI that is **part of a normal product**.

- Navigation, routing, global search, lists, settings pages.
- Account/team/org management, permissions, billing.
- Marketplace browsing, installation flows, approvals.
- Stable dashboards with deterministic interactions.

These surfaces belong in Function Plugins so they can conform to standard product UX patterns, security models, and platform conventions.

---

## What MozaiksCore Is (Pre-Built Foundation)

MozaiksCore is the **plugin host framework** — it is NOT generated, it is pre-built. It provides:

- Authentication/authorization (SSO/OIDC, RBAC/ABAC)
- User/team/org management
- Billing/subscriptions/entitlements
- Plugin host and module loading
- Theming and branding system
- Notification system (in-app + email)
- Settings management
- Auditing and admin tooling
- Session Broker (auth gateway to MozaiksAI runtime)

> **Implementation Note:** For the Mozaiks platform itself, MozaiksCore is implemented in .NET. Future apps built on the platform will have AppGenerator produce Python/JS backends.

### Session Broker vs Session Management

| Concept | Owner | Responsibility |
|---------|-------|----------------|
| **Session Broker** | MozaiksCore | Auth gateway — validates JWT, checks subscriptions, authorizes access to runtime |
| **Session Management** | MozaiksAI Runtime | Workflow state — tracks active workflows, pauses/resumes, artifact persistence |

```
Browser → MozaiksCore (auth/proxy) → MozaiksAI Runtime (workflow execution)
            ↑                              ↑
        Session Broker              Session Management
```

---

## What AI Plugins Contain (AgentGenerator Output)

### 1) Chat Shell + Overlay (always MozaiksAI)
MozaiksAI ships a stable UI surface that can be embedded as an overlay/widget in any app.

- Persistent chat widget / bubble / overlay entry points
- Chat session rendering + streaming
- Artifact host surfaces (side panel, inline components)
- Mode switching (workflow mode vs ask mode)

MozaiksAI should **not** attempt to own product navigation or pages. It should be a drop-in "agent surface".

### 2) Artifact UIs for Agent-Heavy Workspaces
Workflow-specific UIs that are strongly coupled to tool calls belong under:

- `ChatUI/src/workflows/{workflow_name}/components/`

Examples of "artifact-native" UIs:
- AgentGenerator workspace (designing agents, validating JSON, previewing outputs)
- A "campaign builder" canvas that agents keep updating as they reason
- An AI-assisted onboarding interview with a generated summary workspace

### 3) Workflow Orchestration + Real-Time Transport
The runtime owns:
- WebSocket transport and session correlation
- Workflow execution + persistence (Mongo)
- Pack gating / journey sequencing
- Tool boundary enforcement (argument schema validation)

The runtime should remain **product-agnostic**: it executes declarative workflows and emits events; it does not implement product pages.

### AI Plugin Structure (Generated by AgentGenerator)

| Output | Location | Purpose |
|--------|----------|---------|
| Workflow JSON | `workflows/{name}/orchestrator.json` | Workflow configuration |
| Agent definitions | `workflows/{name}/agents.json` | Agent roles and prompts |
| Tool stubs | `workflows/{name}/tools/` | Python callables for agent actions |
| Artifact components | `ChatUI/src/workflows/{name}/components/` | React components for chat workspaces |
| Handoff rules | `workflows/{name}/handoffs.json` | Agent-to-agent transitions |

---

## What Function Plugins Contain (AppGenerator Output)

AppGenerator is an **agentic workflow** that generates Function Plugin files. These plugins run on MozaiksCore alongside AI Plugins.

### Function Plugin Structure (Generated by AppGenerator)

```
plugins/{plugin-name}/
├── manifest.json              # Plugin registration with MozaiksCore
├── api/                       # Backend routes (CRUD, queries, webhooks)
├── pages/                     # Product pages (list, detail, dashboard)
├── components/                # UI components (cards, forms, widgets)
├── hooks/                     # Data fetching, state management
├── services/                  # Backend business logic
└── types/                     # TypeScript interfaces
```

### What Function Plugins Contain

| Output | Purpose |
|--------|---------|
| Plugin manifest | Registration, permissions, navigation entries |
| API routes | CRUD operations, analytics queries, integration webhooks |
| Pages | List, detail, dashboard, settings views |
| Components | Cards, tables, forms, widgets |
| Hooks | Data fetching, real-time subscriptions |
| Services | OAuth flows, external API calls, domain logic |
| Types | TypeScript/Python interfaces |

### Function Plugin Capabilities

Function Plugins can:
- Add navigation entries to MozaiksCore sidebar
- Define product pages (lists, dashboards, settings)
- Expose backend APIs
- Handle OAuth flows for 3rd-party platforms
- Call external vendor APIs
- Emit events to the economic protocol
- **Launch AI Plugin workflows** (the key integration point)

Key rule: Function Plugin pages can *launch* AI Plugin experiences (open chat overlay, deep link into a workflow), but they should not re-implement the chat surface.

---

## Marketplace / App Store Example

Marketplace browsing and purchasing is product-native.

- The marketplace UI should live in MozaiksCore.
- Installing an "agent app" should result in:
  - pack/workflow availability changes (server-side entitlement)
  - the ability to launch a chat/workflow from the marketplace item

MozaiksAI should not try to be a marketplace shell; it should be the consistent agent surface that the marketplace launches.

## Summary: Three Distinct Things

| Thing | What It Is | Created By |
|-------|------------|------------|
| **MozaiksCore** | Pre-built foundation (auth, billing, plugin host) + embedded MozaiksAI runtime | Built once, not generated |
| **Function Plugin** | Product pages, APIs, components for a specific app | AppGenerator workflow |
| **AI Plugin** | Workflows, agents, tools, artifacts for chat-native experiences | AgentGenerator workflow |

---

## Practical Decision Rules (Use This When Unsure)

Put UI **inside an AI Plugin** when:
- The UI is primarily a workspace that agents continuously update.
- The user expects to converse, refine, and iterate.
- The state belongs to a specific chat/workflow run.
- The UI is tightly coupled to tool calls and agent steps.

Put UI **inside a Function Plugin** when:
- It's navigation, browsing, search, or inventory (marketplace, lists).
- It's security/permissions/billing/approvals.
- It needs stable, deterministic CRUD with strict validation.
- It's a global dashboard spanning many chats/workflows.

A good default is:
- MozaiksCore provides the *foundation* and *runtime*.
- Function Plugins provide *entry points* and *product pages*.
- AI Plugins provide the *agent workspaces*.

---

## Generator Mapping Summary

| Question | Answer | Generator |
|----------|--------|-----------|
| Does the user converse with AI to build/refine it? | Yes → AI Plugin | AgentGenerator |
| Is it a list, dashboard, settings, or admin page? | Yes → Function Plugin | AppGenerator |
| Does it need real-time agent updates during a session? | Yes → AI Plugin | AgentGenerator |
| Is it permissioned, auditable, and exists outside any chat? | Yes → Function Plugin | AppGenerator |
| Does clicking something launch an AI workflow? | Button in Function Plugin, workflow in AI Plugin | Both |

---

## Packaging Implication (Why This Split Helps)

This boundary makes the architecture clear:

- **MozaiksCore** is built once and hosts all plugins (with MozaiksAI runtime embedded)
- **AppGenerator** produces Function Plugins (product pages, APIs)
- **AgentGenerator** produces AI Plugins (workflows, agents, artifacts)
- Both generators are themselves AI Plugins running on MozaiksAI

For enterprise customers:
- Customer can use MozaiksCore as their product shell
- Or embed MozaiksAI runtime into their existing product
- Either way, they get both Function Plugins and AI Plugins working together
