# Mozaiks as a Hosting Provider (Hostinger-style Product Model)

**Goal**: Make Mozaiks operate like a hosting provider: many customers, many apps, clear plan tiers, and a predictable rule for when something is shared vs dedicated.

This doc is **product + operations** guidance. It does **not** require “one VM per app” by default.

---

## 1) The 3 things Hostinger sells (translated)

Hostinger’s pricing menu is basically three businesses:

1) **Shared hosting / Cloud / VPS** (compute + routing + uptime)
2) **Domains / Email** (identity + deliverability add-ons)
3) **Horizons** (AI app builder that can publish a project)

### Mozaiks equivalents

- **Mozaiks Horizons (your AI builder)**
  - This is your “build and publish from a prompt” product.
  - In Mozaiks terms: workflow generation + artifact creation + publish.

- **Mozaiks Hosting (run the published app)**
  - You host the app runtime for end-users.
  - In Mozaiks terms: the Data Plane for app execution + websockets + persistence.

- **Mozaiks Domains/Email**
  - Add-ons that make publishing feel “real”: custom domain, email boxes.
  - You can resell these via partners at first (don’t build registrars/email servers yourself).

---

## 2) Control Plane vs Data Plane (who does what)

### Control Plane: MozaiksCore (platform)
MozaiksCore should own:
- Accounts/orgs/users, billing, entitlements, tier limits
- App lifecycle (create app, delete app, upgrade tier)
- Session brokering: mint short-lived runtime tokens and return the correct `ws_url`
- Routing decision: **where does `app_id` run** (which runtime cluster)

### Data Plane: MozaiksAI Runtime (execution)
MozaiksAI Runtime should own:
- Websocket transport for ChatUI
- Workflow execution (AG2)
- Chat/session persistence (Mongo)
- Streaming events, buffering, retries, timeouts

### Important: who owns load balancing?
**Neither MozaiksCore nor MozaiksAI “is the load balancer.”**
- Load balancing is infrastructure (Azure/AWS/Cloudflare/NGINX/Kubernetes ingress).
- MozaiksCore **chooses the destination runtime URL**, and the infrastructure balances across replicas within that runtime cluster.

---

## 3) The default operating model (how real hosting providers do it)

### Default: shared infrastructure, tenant isolation in software
- Many customers share one runtime fleet.
- Isolation is by **tenant keys** (`app_id`, `user_id`) + quotas/limits.
- This is the only way you can profitably host “lots of apps” early.

### Dedicated is an upgrade, not the default
Dedicated runtime/VM is only for:
- compliance / strict isolation
- predictable reserved capacity
- extremely noisy workloads

---

## 4) Mozaiks SKUs (simple, Hostinger-like)

Think of your SKUs as *isolation + limits + support level*, not “how many apps exist.”

### A) Mozaiks Horizons (AI builder) — like Hostinger Horizons
What the customer buys:
- “AI credits” (token budget)
- ability to create projects/apps
- publishing capability

What you run:
- Control Plane (auth/billing) + a shared Runtime

How you price:
- monthly base + included credits
- overages/top-ups

### B) Hosted Apps — Shared Runtime (Starter)
Best for: most customers.

Isolation:
- Shared runtime cluster
- Shared Mongo cluster (single DB), isolated by `app_id`

Ops:
- 1 runtime deployment can serve many apps
- Scale up first; later add replicas with sticky sessions

How you price:
- base + usage (tokens, runs, storage, bandwidth)

### C) Hosted Apps — Shared Runtime + Dedicated DB (Business)
Best for: “serious” customers who want isolation but not a dedicated stack.

Isolation:
- Shared runtime cluster
- Dedicated Mongo database (or separate cluster) for that customer

Ops:
- Control Plane routes `app_id -> storage` via `resolve_storage(app_id)`

How you price:
- higher base + usage

### D) Hosted Apps — Dedicated Runtime (Cloud/VPS analog)
Best for: enterprise / regulated / very high load.

Isolation:
- Dedicated runtime deployment for that customer (separate replicas + separate scaling)
- Usually dedicated DB too

Ops:
- Still one Control Plane
- Control Plane routes `app_id` to a dedicated runtime URL

How you price:
- “per environment” base (this is where “a VM per customer” becomes a real line item)

### E) Export / Self-host (VPS DIY analog)
Best for: customers who want ownership/control.

What you provide:
- export bundle, docs, optionally paid support

What you don’t do:
- you don’t run their runtime (or you do it only for onboarding)

---

## 5) The decision rule: when does someone get a dedicated VM/runtime?

Use a rule you can enforce, so you’re not guessing:

- Shared Runtime is default.
- Upgrade to Dedicated DB when either:
  - retention/PII requirements demand it, or
  - the customer is consistently “hot” (high volume) and causing noisy-neighbor risk.
- Upgrade to Dedicated Runtime when either:
  - customer needs strict isolation, or
  - you need reserved capacity / predictable performance, or
  - websocket concurrency + workflow load is high enough that shared scaling is risky.

This is the same path hosting companies take: shared hosting → “cloud” → VPS/dedicated.

---

## 6) What “hosting lots of apps” looks like in practice

### One app does NOT equal one server
In shared hosting models:
- 10,000 small apps can run on a small fleet
- the isolation boundary is tenancy + quotas, not hardware

### What you actually isolate as you grow
In order, cheapest-first:
1) **Rate limits** per `app_id` and per `user_id`
2) **Storage routing** (move one customer to a dedicated DB)
3) **Dedicated runtime** for big customers

---

## 7) “Domains/Email” without building a registrar

If you want Hostinger’s “menu” feeling, treat these as add-ons:
- Domains: reseller/registrar integration (later)
- DNS: managed DNS provider integration
- Email: use a managed provider or resell an existing email service

MozaiksCore can sell/attach these to an app, but the runtime should stay focused on execution.

---

## 8) The minimum you need to launch (so you don’t drown)

Launch with only:
- Mozaiks Horizons (builder)
- Hosted Apps: Shared Runtime tier
- Optional: custom domain mapping (CNAME -> your ingress)

Add later:
- Dedicated DB tier
- Dedicated Runtime tier
- Export/Self-host

---

## 9) Operator Playbook (for non-DevOps)

This section is intentionally blunt: it’s what you run, what you watch, and how you avoid “one VM per app.”

### 9.1 Your Day-1 deployment (one shared fleet)

Run exactly this:

- **MozaiksCore (Control Plane)**
  - Does auth/billing/entitlements
  - Issues `{chat_id, ws_url, runtime_token}`
  - Decides which runtime URL serves an `app_id`
- **MozaiksAI Runtime (Data Plane)**
  - Executes workflows/tools
  - Serves WebSockets for ChatUI
  - Writes sessions to Mongo
- **MongoDB**
  - Single cluster, single database to start
  - Every document includes `app_id` (hard boundary)

That's it. No message bus. No Kubernetes required. No per-app servers.

### 9.2 Your Day-1 hosting rule (the one that keeps you sane)

- **Every app is just a tenant slice** inside the same runtime fleet.
- Apps are isolated by:
  - `app_id` on every persisted record
  - rate limits / quotas per `app_id`
  - workflow allowlists per `app_id`

This is the shared-hosting model.

### 9.3 What you “manage” day-to-day

You are managing a small number of things, not thousands:

- **One runtime fleet**: is it up? is it slow?
- **One database**: is it healthy? are queries fast?
- **Usage limits**: is one app blowing up?

If you can answer those three, you can operate Mozaiks.

### 9.4 The 5 dashboards/metrics you need (minimum viable ops)

Track these at minimum:

1) **Active WebSocket connections** (total + per `app_id`)
2) **Concurrent workflow runs** (total + per `app_id`)
3) **Error rate** (5xx + tool failures)
4) **Latency** (p95 for “start session” + p95 for key Mongo calls)
5) **Cost/usage** (tokens by `app_id` + storage by `app_id`)

If you only do one thing: alert when one `app_id` consumes a big % of concurrency.

If you want an agent to monitor these and take safe actions, see:
- `docs/source_of_truth/OPS_AUTOMATION_AGENT.md`

### 9.5 The upgrade path (exactly like hosting companies)

When things get busy, upgrade in this order:

1) **Add limits** (cheapest): cap concurrent runs, cap parallel child workflows, cap websocket count per app
2) **Scale up** the runtime VM/container (bigger box)
3) **Scale out** runtime replicas behind a load balancer
   - Use **sticky sessions** first for websockets
4) **Dedicated DB** for a hot/noisy customer
5) **Dedicated runtime** (your “VPS/Dedicated” tier)

#### Where 9.5 happens (in practice)

This is mostly **deployment + infrastructure config**, not agent workflow YAML.

- **Dockerfile changes?** Usually **no**. The Dockerfile is for packaging. Scaling is almost always outside the image.
- **Agent/workflow YAML changes?** Only if you’re changing *workflow behavior*. Most scaling steps do not require touching workflow definitions.

Mapping each step:

1) **Add limits** → runtime/control-plane config + (sometimes) small code changes
  - Examples: caps per `app_id` (max concurrent runs, max websockets), timeouts, retention
2) **Scale up** → change the machine/container resources
  - Bigger VM size or higher container CPU/RAM limits (compose/k8s/cloud settings)
3) **Scale out** → run multiple runtime replicas behind a load balancer
  - Set `replicas` (k8s) / multiple instances (cloud)
  - Configure **sticky sessions** at the load balancer/ingress (not inside the runtime)
4) **Dedicated DB** → control plane routing + secrets/config
  - Implement/operate `resolve_storage(app_id)` and point that app at a new Mongo URI/DB
5) **Dedicated runtime** → provision a separate runtime deployment for that customer/app
  - Control plane routes that `app_id` to a dedicated runtime URL

In this repo specifically:
- See `infra/DEPLOYMENT.md` for how the runtime is containerized and deployed.
- Compose examples live in `infra/compose/docker-compose.yml` and `infra/compose/docker-compose.prod.yml`.
- Image packaging is `infra/docker/Dockerfile`.

---

## 10) How to stay modular while hosting anything

You said: “I need to be modular for anything” and you already have agents that declare/flag behavior via context variables.

The correct modularity strategy is:

- Keep **runtime generic** (executes declarative workflows, tools, artifacts)
- Keep **per-app behavior in data/config**, not custom deployments

### 10.1 The modular contract per app (what changes between apps)

Each `app_id` should have (at minimum):

- Enabled workflow packs (allowlist)
- Limits/quotas (max runs, retention days, max parallelism)
- Context variables (feature flags / routing hints)

Your agents can read these context variables to:
- enable/disable tools
- choose safer prompts
- select “cheap mode” vs “high quality mode”
- enforce compliance modes

That lets you host many very different apps on the same runtime fleet.

### 10.2 The one abstraction that prevents chaos

Keep this concept (even if it’s simple at first):

- `resolve_runtime(app_id) -> base_url`
- `resolve_storage(app_id) -> {mongo_uri, db_name}`

Phase 1: both return a shared default.
Later: you route premium/noisy apps to dedicated runtime/DB without rewriting the platform.

---

## 11) The simplest way to think about “hosting other peoples apps”

Stop imagining thousands of VMs.

Instead:

- Mozaiks is a **multi-tenant app platform**.
- “Apps” are configurations + data + workflows.
- You only create dedicated infrastructure when:
  - the customer pays for isolation, or
  - you must for reliability/compliance.

If you follow that, you can run Mozaiks like a hosting provider without needing to become a deep DevOps expert on day 1.
