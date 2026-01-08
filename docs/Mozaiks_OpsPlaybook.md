# Mozaiks Ops Playbook (Dashboards -> Actions -> What To Change)

This playbook answers: “An alert fired. What do I *do* next, and where do I change things?”

Mozaiks has **two runtimes**:

- **Mozaiks Platform** (control plane): builder chat, agent/app generation, sandbox, deployments, plan enforcement.
- **Customer Apps** (data plane): each app is its own deployed MozaiksCore container + its own DB.

Before doing anything, decide the alert **scope**:

- **Platform-scope**: "Mozaiks itself is having trouble" (your control plane).
- **App-scope**: "One customer app is having trouble" (a specific `app_id`).

Then decide the alert **dimension** (how you drill down):

- On *your* platform dashboard: drill down primarily by `app_id` (and by service name: sandbox, generator, deploy, billing, etc.).
- On a *customer app* admin dashboard: drill down primarily by `user_id` (and optionally `workspace_id`, `plugin_name`, `route`), because they only control one `app_id`.

Practical rule: every alert should carry `{ scope, app_id?, user_id? }` so you can answer “is this my platform, this app, or one user inside this app?”

---

## 0) Quick rule: most alerts do NOT require code changes

Most production incidents are solved by one of these:

1) **Scale/upgrade** the app (more instances, dedicated plan, more capacity)
2) **Throttle/limit** the app (cap concurrency, cap WebSockets per user, queue jobs)
3) **Rollback** to last known-good deploy
4) **Then** do a code fix if it’s a bug/performance issue

So: you usually do **not** edit `Dockerfile`/`deploy.yml` for an alert. You change:
- the app’s **hosting profile** (Starter/Scale/Enterprise), and/or
- the app’s **environment variables** (Azure App Settings), and/or
- the app’s **scale settings** (instances/plan)

Code changes come after you’ve stabilized production.

---

## 1) Alert: one `app_id` consumes a big % of concurrency (“noisy neighbor”)

This typically means: one app is using a lot of **CPU/memory**, **WebSocket connections**, or **agent workflow capacity**.

### Immediate actions (stabilize)

1) Confirm whether the impact is:
   - only that one app, or
   - multiple apps sharing the same App Service Plan (shared CPU)
2) If multiple apps are impacted: **isolate the noisy app**
   - move that app to a **dedicated App Service Plan**
3) If only that app is impacted:
   - **scale out** that app (more instances), or
   - **scale up** (bigger plan)

### Platform actions (what changes)

- Update that app’s **profile** (Starter -> Scale -> Enterprise).
  - This is a control-plane action (platform DB/config), not a code change.
  - See `docs/EnterpriseFoundation.md` -> "Operational Profiles".

### App config actions (what changes)

If you need to reduce load without upgrading immediately, lower per-user fanout:

- `WEBSOCKET_MAX_CONNECTIONS_PER_USER` (already supported)
  - Used by `backend/core/websocket_manager.py`
  - Loaded by `backend/core/config/settings.py`

If plugin execution is saturating CPU, cap execution concurrency:

- `PLUGIN_EXEC_MAX_CONCURRENCY` (already supported)
  - Used by `backend/core/plugin_manager.py`
  - Loaded by `backend/core/config/settings.py`

---

## 2) Dashboard -> What it means -> What to do

Below are the 5 “minimum ops metrics” and the response playbooks.

### 2.1 Active WebSocket connections (total + per `app_id`)

What it usually means:
- growth (good), or
- reconnect storm (bad), or
- a chat-heavy app that outgrew Starter.

Immediate actions:
- If CPU/memory is high: scale out the app (more instances) or move it to a bigger plan.
- For reliable real-time delivery, run a single instance; multi-instance delivery requires additional infrastructure outside this repo.

What you change:
- Hosting/profile (Platform): upgrade plan/tier or increase capacity.
- App settings (Azure env vars): `WEBSOCKET_MAX_CONNECTIONS_PER_USER` (tighten/loosen per plan).

### 2.2 Concurrent workflow runs (total + per `app_id`)

Define “workflow” clearly in your platform:
- Platform workflows = “agent/app generation runs” + sandbox jobs
- App workflows = “module execution” / background jobs

Immediate actions:
- Queue work (don’t start everything immediately).
- Enforce per-app concurrency quotas (so one app can’t starve the system).

What you change:
- Platform: worker pool size + queue + per-app quotas (control-plane code/config).
- App: `PLUGIN_EXEC_MAX_CONCURRENCY` if “workflow” refers to module execution.

Code changes (foundation):
- Add a real job/worker pattern (queue mode):
  - `docs/EnterpriseFoundation.md` roadmap item “worker”

### 2.3 Error rate (5xx + tool failures)

Immediate actions:
1) Roll back the app to last known-good (fastest way to stop the bleed).
2) Check logs for correlation IDs.
3) If it’s an external dependency outage (LLM provider, DB): degrade gracefully.

What you change:
- Usually nothing in infra first: rollback + investigate.
- If persistent: bug fix in code and redeploy.

Relevant foundation files (common suspects):
- `backend/core/director.py` (API endpoints, error handling)
- `backend/core/plugin_manager.py` (module execution/timeouts)
- `backend/core/config/database.py` (DB connectivity/timeouts/pools)
- `backend/core/chat/*` (LLM client/retries if failures are AI-related)

### 2.4 Latency (p95 for key routes + p95 Mongo calls)

Immediate actions:
- If CPU bound: scale out/up.
- If DB bound: check indexes and query shapes; reduce payload sizes.

What you change:
- Mongo indexes + query fixes (usually code + migration).
- If needed: tune DB pool/timeouts.

Relevant foundation files:
- `backend/core/config/database.py` (pool/timeouts + index init)
- `backend/core/director.py` (endpoints + caching patterns)
- `backend/core/settings_manager.py` / module code (chatty queries)

### 2.5 Cost/usage (tokens by `app_id` + storage by `app_id`)

Immediate actions:
- Cap or pause the cost driver (per app) if it’s runaway.
- Tell the user to upgrade if they exceed their plan.

What you change:
- Platform: enforce token budgets and quotas per plan (control plane).
- App env vars (if needed): model choice, timeouts, feature flags.

Relevant foundation knobs:
- `.env.example` (AI model selection/envs)
- `backend/core/chat/mozaiks_ai_client.py` (model/base URL selection; add usage metering if needed)

---

## 3) Where changes happen (so you’re not lost)

When an alert fires, changes land in one of these places:

1) **Azure settings (no code)**:
   - scale out/in (instances)
   - scale up/down (plan size)
   - move app to dedicated plan
   - set env vars

2) **Mozaiks Platform (control plane)**:
   - change the app’s profile (Starter/Scale/Enterprise)
   - enforce quotas / show upgrade prompts
   - schedule migrations and deploys

3) **Customer app repo (code)**:
   - bug fixes / performance improvements
   - adding new capabilities (new endpoints or internal features)

If you want the platform to “upgrade apps automatically”, you’re mostly changing **(1)** and **(2)**, not rewriting Dockerfiles.

---

## 4) How this ties into Change Sets

When a change requires more infrastructure, the Hosting Operator should block deploy and recommend an upgrade.

Reference:
- `docs/Mozaiks_ChangeSets_and_HostingOperator.md`

---

## 5) Can this be done programmatically? (Yes)

Yes. This is what the **Hosting Operator** is for: automated, policy-based actions driven by metrics.

### What changes during automation (usually not code)

1) **Azure (no code)**:
   - scale out/in the app (instance count)
   - scale up/down (App Service Plan size)
   - move an app to a dedicated plan (noisy neighbor isolation)
   - update environment variables (App Settings)

2) **Mozaiks Platform (control plane)**:
   - update app profile (Starter/Scale/Enterprise)
   - enforce per-app quotas (workflows, tokens, concurrency)
   - block Change Set deploys when requirements exceed the current profile

### How it works (simple event flow)

1) Metrics land in Azure Monitor / Application Insights per app.
2) Alert fires (or operator polls metrics every N seconds).
3) Operator applies an action if allowed by policy (with cooldowns and max limits).
4) If action requires an upgrade, operator notifies user + blocks incompatible deploys until upgrade.

### Where “the policy” lives

- Store per-plan thresholds and allowed actions in the platform (DB or a JSON policy file).
- Example policy file: `docs/HostingOperatorPolicy.example.json`
- Example metrics snapshot: `docs/HostingOperatorMetricsSnapshot.example.json`
- Example evaluator (reference implementation): `backend/core/hosting_operator.py`
- Hosting Monitor Agent prompts/tools: `docs/HostingMonitorAgent.md`

### Quick demo (local)

From the repo root:

- `python backend/core/hosting_operator.py --snapshot docs/HostingOperatorMetricsSnapshot.example.json --pretty`
