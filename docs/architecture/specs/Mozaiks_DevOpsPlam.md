# Mozaiks DevOps Plan (Control Plane + Hosted Apps)

**Purpose**: A practical, budget-aware DevOps plan for Mozaiks where:
1) **Mozaiks (you)** hosts a multi-tenant “foundry” app where users chat to build apps.
2) **Mozaiks (you)** can also host the apps those users create (as a hosting provider / Azure middleman).

This plan is designed to be **cheap initially**, **safe by default**, and **not paint you into a corner** if usage spikes.

---

## 1) Core Terms (in plain English)

- **Control Plane**: Your Mozaiks product where builders create apps (the “foundry”).
- **Data Plane**: The customer apps created by builders (the apps end-users use).
- **Tenant**: A customer/org.
- **App**: A specific app created inside a tenant (many per tenant).
- **Workflow**: A template stored on disk under `workflows/<workflow_name>/`.
- **Chat Session**: A run instance stored in Mongo keyed by `chat_id` and scoped by `app_id`.

---

## 2) Target Architecture (what you’re building)

### 2.1 Control Plane (Mozaiks Foundry)
- **MozaiksCore** (auth, billing, entitlements, admin)
- **MozaiksAI Runtime + ChatUI** embedded in MozaiksCore
- **Mongo** for chat/session persistence
- **Object storage** for file uploads/exports (optional but recommended early)

### 2.2 Data Plane (Hosted Customer Apps)
For apps you host:
- Each customer app runs the same runtime components (MozaiksCore + MozaiksAI) OR a simplified hosted package.
- Apps are isolated by **app_id**, and optionally by **database/cluster** for higher tiers.

---

## 3) Separation of Concerns (Mozaiks vs Users’ Apps)

This is the cleanest mental model:

- **Mozaiks (Control Plane)** is where builders sign up, create apps, manage billing, and generate/install workflows.
- **Users’ Apps (Data Plane)** are the runtimes/end-user experiences that run those workflows.

### 3.1 What Mozaiks (Control Plane) owns

Mozaiks should own the things that must be consistent across all apps and all customers:

- **Identity**: user auth, orgs/teams, roles.
- **Entitlements + billing**: who can create apps, run workflows, limits.
- **App lifecycle**: create app, delete app, rename app, tier changes.
- **Workflow/pack lifecycle**: publish workflow packs, enable/disable packs per app.
- **Session brokering**: mint short-lived runtime tokens and return `ws_url` for ChatUI.
- **Routing to the right data plane**: given an `app_id`, decide which runtime cluster/URL handles it.

### 3.2 What a User’s App (Data Plane) owns

Each app is where end-users actually “chat and do work.” The data plane owns:

- **Workflow execution**: running AG2 GroupChats, tools, artifacts.
- **Chat persistence**: chat sessions/transcripts/artifacts state.
- **App-scoped configuration**: enabled workflows, per-app settings, per-app secrets (if you support that).
- **Real-time streaming**: WebSocket streaming and buffering.

### 3.3 What MozaiksAI Runtime is (and why it appears in both)

MozaiksAI Runtime is the **execution engine**. It can be deployed:

- **Once (shared)** and serve many apps via `app_id` scoping (cheapest), or
- **Many times** (dedicated) for isolation (enterprise).

The key is that *Control Plane decides where an app runs*, and *Runtime executes it*.

### 3.4 Boundary contract (the only 3 things the UI needs)

From the browser’s perspective, the control plane provides a single capability:

1) **Start/resume** a session for `{app_id, workflow_name, user_id}`
2) Get back `{chat_id, ws_url, runtime_token}`
3) Connect ChatUI to `ws_url` using `runtime_token`

Everything else is “data plane implementation detail.”

### 3.5 Recommended cheapest default topology

- **One Control Plane deployment** (MozaiksCore)
- **One Data Plane deployment** (MozaiksAI Runtime)
- **One Mongo cluster**
- Multi-tenant isolation via `app_id`

When an app becomes large/noisy:
- Keep the control plane the same.
- Move that app (or that customer) to a dedicated data plane + DB by changing the routing decision.

---

## 4) Hosting Model Options (start cheap, keep escape hatches)

If you want to operate like a hosting provider (Hostinger-style menu of offerings), use:
- `docs/source_of_truth/HOSTINGER_STYLE_PRODUCT_MODEL.md`
  - Defines the default shared model
  - Defines when “Dedicated DB” or “Dedicated Runtime/VM” is an upgrade
  - Separates load balancing (infra) from routing (control plane) and execution (runtime)

### Option A — Cheapest: Shared Cluster, Many Apps (Recommended Phase 1)
- **One Mozaiks runtime deployment** serves many apps.
- **One Mongo cluster** (single DB) stores all apps, with `app_id` on every document.
- Scale by increasing VM size or adding replicas.

**Pros**: lowest cost, simplest ops.
**Cons**: noisy-neighbor risk if one app explodes.

### Option B — Mid: Shared Cluster, Dedicated DB per Customer (Phase 2)
- Still one runtime cluster.
- Move “big customers” to separate DBs (or separate Mongo clusters).

**Pros**: isolation for large customers without managing thousands of DBs.
**Cons**: adds routing/ops complexity.

### Option C — Full Isolation: Dedicated Stack per Customer/App (Enterprise)
- Separate runtime + DB per customer (or per app for very large/regulated).

**Pros**: strongest isolation.
**Cons**: expensive; lots of deployments.

---

## 5) The One Decision That Saves You Later: Storage Routing

Implement a single concept early:

- `resolve_storage(app_id) -> {mongo_uri, db_name}`

Phase 1 implementation returns the same `{mongo_uri, db_name}` for all apps.
Later, this function can route premium/high-load apps to dedicated DBs.

**Why this matters**: it gives you the ability to migrate one hot customer without rewriting your runtime.

---

## 6) Database Plan

### 5.1 Do NOT create a Mongo database per app by default
Per-app DB creation adds operational overhead and cost. It’s rarely needed early.

### 5.2 Do this instead (Phase 1)
- One Mongo cluster
- One runtime DB (example: `mozaiks_runtime`)
- Collections scoped by fields:
  - `app_id` (hard isolation boundary)
  - `user_id` (within-app isolation)
  - `workflow_name`, `chat_id`, `status`

### 5.3 Indexes (must-have)
Add indexes to keep performance stable as usage grows:
- `chat_sessions`: `(app_id, user_id, updated_at)` for “recent chats”
- `chat_sessions`: `(app_id, workflow_name, status, updated_at)` for gating/journeys
- `chat_sessions`: `(app_id, chat_id)` if you frequently lookup by chat id

### 5.4 Data retention (cheap safety)
- Keep transcripts forever only if you must.
- Otherwise, implement retention by tier (e.g. 30/90/365 days) and archive old sessions to object storage.

---

## 7) Repo Strategy (avoid repo-per-app as an internal default)

### 6.1 Recommended: “Export creates a repo”
- Internally, store generated workflow packs + artifacts in:
  - Mongo (metadata + small configs)
  - Object storage for larger files
- Only create a GitHub repo when the user explicitly:
  - exports,
  - self-hosts,
  - or is on a paid tier requiring repo integration.

### 6.2 If you absolutely need repos early
- Use a single “apps” monorepo with folder-per-app:
  - `apps/<app_id>/...`
- This is simpler than managing thousands of repos.

---

## 8) Deployment + Scaling Plan

### 7.1 Phase 1 (0–3 months): Single Deployment, Scale Up First
- 1 runtime deployment
- 1 Mongo cluster
- 1 VM or 1 small k8s cluster

Scale approach:
1) **Scale up** (bigger VM) before you scale out.
2) Add **one replica** only when you need high availability.

### 7.2 Phase 2 (3–6 months): Scale Out + “Sticky sessions” for WebSockets
If you add multiple replicas behind a load balancer:
- WebSockets need either:
  - **sticky sessions** (client stays on the same replica), or
  - a message bus to forward events across replicas.

Phase 2 recommendation (budget-friendly):
- Start with sticky sessions.
- Add bus only when you have proven multi-replica pain.

### 7.3 Phase 3 (6–12 months): Worker Pool
If workflows/tool calls become heavy:
- Split into:
  - **Gateway nodes**: WebSockets + HTTP only
  - **Worker nodes**: run workflows

This is where a durable broker/queue becomes genuinely useful (job queue + event fanout).

---

## 9) WebSocket Failure + Parallel Workflows (current runtime implications)

Your runtime already:
- spawns child workflows as background tasks
- buffers events while WS is disconnected (in-memory)
- persists chat sessions to Mongo

To be robust at scale:
- Make parent-child relationships durable in Mongo (pack run record)
- Provide reconciliation: if parent exists and all children completed, resume parent

This reduces the need for a message bus early.

---

## 10) Observability (must-have early)

Minimum:
- Structured logs
- Per-request correlation id (chat_id + app_id)
- Dashboards for:
  - active websocket connections
  - active runs
  - error rates
  - p95 latency
  - Mongo query latency

Cheap options:
- Azure Application Insights
- Grafana + Loki (if you can run it)

---

## 11) Security + Isolation (non-negotiable)

- Every read/write to chat/session data MUST include `app_id`.
- Session Broker enforces:
  - user auth
  - entitlement checks
  - short-lived runtime tokens scoped to `{app_id, user_id, chat_id}`

---

## 12) Cost Controls (because you’re broke)

- Avoid per-app DBs and per-app repos.
- Use one shared cluster and strict scoping.
- Add limits:
  - max concurrent runs per app/user
  - max parallel workflows
  - retention + archiving
- Make “dedicated DB/cluster” a paid tier.

---

## 13) Concrete Next Steps (do these now)

1) Ensure all persistence queries are `app_id` scoped.
2) Add/verify Mongo indexes for the main queries.
3) Add `resolve_storage(app_id)` abstraction (even if it always returns the same DB today).
4) Decide Phase 1 deployment target:
   - single VM (cheapest) or small managed container service
5) Treat GitHub repo creation as an **export feature**, not the internal storage plan.

---

## 14) When to adopt a message bus (simple trigger list)

Adopt a message bus when:
- you run multiple runtime replicas without sticky sessions, OR
- you need durable job queue semantics for long-running tasks, OR
- you want workers separate from websocket gateways.

Until then, your current architecture can scale surprisingly far with:
- good indexes
- strict `app_id` scoping
- sticky sessions if you add a second replica


# Ops Automation Agent (LLM-assisted, rules-first)

**Purpose**: Answer “can an LLM monitor the 5 dashboards/metrics and have an agent act?” in a way that is safe, cheap, and compatible with Mozaiks’s modular, multi-tenant runtime.

**Short answer**: Yes — but the safe architecture is **rules-first automation** with the LLM doing **summarization + routing + playbook selection**, not directly making risky infra changes.

---

## 1) What you already have (in this repo)

The runtime already exposes polling-friendly endpoints:

- `GET /api/health` (liveness)
- `GET /health/active-runs` (in-memory active run registry)
- `GET /metrics/perf/aggregate` (in-memory aggregate counters)
- `GET /metrics/perf/chats` (per-chat snapshots)

These are enough to build “minimum viable ops” without Prometheus on day 1.

---

## 2) The safe pattern (LLM-assisted, not LLM-driven)

### Components

1) **Metrics collector** (pull)
   - A small service/script that polls the runtime endpoints every N seconds.

2) **Rule engine** (deterministic)
   - If/then thresholds that decide when something is an incident.
   - Outputs a structured incident object.

3) **Action executor** (restricted)
   - Performs only safe, reversible actions.
   - Anything expensive/destructive requires human approval.

4) **LLM “Ops agent”** (optional)
   - Summarizes the incident.
   - Picks a playbook.
   - Produces a human-readable explanation.
   - Can open a ticket / send a notification.

### Why this is the right split

- Metrics and thresholds are objective.
- LLMs are good at: explanation, prioritization, pattern matching, and writing a plan.
- LLMs are risky at: changing infra or deleting/altering customer data.

---

## 3) What actions you should automate vs keep manual

### Safe to automate (recommended)

- **Throttle** an `app_id` (reduce concurrency limits)
- **Pause** new runs for an `app_id` (temporary backpressure)
- **Disable** a single expensive workflow pack for an `app_id` (feature gating)
- **Notify** you (Slack/email) with a clean summary and recommended next steps

### Keep manual (until you’re mature)

- **Scaling out** replicas (step 3 in the Hostinger model)
- **Moving to dedicated DB** (step 4)
- **Provisioning dedicated runtime** (step 5)

Those are “platform changes” that affect cost, routing, and blast radius.

---

## 4) Example: the 5 metrics as rules

You said “5 dashboards/metrics.” Here’s how to convert them into rules.

### Inputs (what you measure)

1) Active websockets: approximate via runtime connection registry (or per-chat active)
2) Concurrent runs: `/health/active-runs`
3) Error rate: total errors deltas from `/metrics/perf/aggregate` (plus logs)
4) Latency: p95 is best from real tracing; early-stage use “turn durations” from perf snapshots
5) Cost/usage: token/cost deltas are in perf aggregate + runtime logging

### Rules (simple examples)

- If one `app_id` accounts for > 40% of active runs for > 5 minutes → throttle that `app_id`
- If total active runs > X and error rate rising → stop accepting new runs temporarily
- If token burn rate for one `app_id` spikes → notify + require approval for any action

---

## 5) How to wire it in Mozaiks terms (modular + multi-tenant)

### A) Where the “policy” lives

Store an **Ops Policy** per `app_id` and a default global policy:

- `ops_policy.default`
- `ops_policy.{app_id}`

The policy is just thresholds + allowed actions.

### B) Where it runs

Best practice: run the monitor outside the runtime process:

- a cheap cron job / container
- an Azure Function / Cloud Run job

Reason: if the runtime is unhealthy, you still want monitoring to work.

---

## 6) Minimal curl checks (manual today, automated tomorrow)

- `curl http://localhost:8000/api/health`
- `curl http://localhost:8000/health/active-runs`
- `curl http://localhost:8000/metrics/perf/aggregate`

---

## 7) Practical recommendation for you (low stress)

Start with:

1) Poll `/api/health` + `/health/active-runs` + `/metrics/perf/aggregate`
2) Hardcode 3 rules (one noisy-neighbor rule, one error-spike rule, one cost-spike rule)
3) Only automate: notify + throttle
4) Everything else becomes a “suggested action” written by the LLM

This gives you “you are my devops” outcomes without letting the LLM break production.
