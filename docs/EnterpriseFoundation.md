# MozaiksCore Enterprise Foundation Notes

MozaiksCore is already shaped like an opinionated SaaS scaffold: auth, routing, subscriptions, notifications, WebSockets, and a “feature module” system (currently called plugins). This document captures what’s **already strong**, what’s **not yet production/enterprise ready**, and the **recommended direction** for an open-source, self-hostable foundation that can also support a hosted “agent/app generator” product.

## Scope & Terms

- **Foundation (this repo)**: must be secure-by-default for self-hosters (no dependence on your .NET platform for runtime security).
- **Platform layer (your .NET side)**: can add marketplace-like conveniences (agent PR generation, CI integrations, deployments, revenue share automation), but cannot be required for core safety.
- **Modules/features**: first-party business logic units for an app. If they are trusted (authored/reviewed by the app team), they can run in-process. If you ever allow untrusted third-party code, you need hard isolation (separate service/container/WASM).
- **Two kinds of multi-tenancy**:
  - **Platform multi-app**: one creator/account owns many apps (each app is a separate product/deployment). This is usually handled by the platform/orchestrator.
  - **In-app workspaces/orgs**: one app supports many customer organizations/workspaces inside it (Slack-style). This must be handled inside the foundation/app runtime.

## What's Already Solid

- **Prod validation for secrets/hosts**: production requires strong `JWT_SECRET` and `ALLOWED_HOSTS` (`backend/core/config/settings.py`).
- **WebSocket token policy**: JWTs are accepted via `Sec-WebSocket-Protocol` and not via URL/query params (`backend/core/http/websocket_auth.py`).
- **Baseline HTTP hardening**: security headers, request size limits, CORS allowlists (`backend/core/http/setup.py`).
- **Plugin execution guardrails**: name validation, concurrency caps, timeouts (for async entrypoints) (`backend/core/plugin_manager.py`).

## "Enterprise / Production Ready" (Architecture & Technical)

Security matters, but “enterprise-ready” is also about **operability, scalability, determinism, maintainability, and upgradeability**. A useful mental model is: can a team run this reliably with multiple engineers, multiple environments, multiple deployments per week, and predictable upgrades?

### 1) Deterministic runtime & clean lifecycle

- **No runtime mutation of source/config**: configs are immutable artifacts per release; derived state goes to DB/Redis.
- **Single clear startup path**: validate config → connect dependencies → load modules → register routes → start background workers.
- **Graceful shutdown**: stop background loops, drain/close DB clients, and stop accepting new work.
- **Health endpoints**: add liveness/readiness endpoints so orchestrators can manage rollouts and autoscaling.

### 2) Stateless API and defined scaling model

- **Stateless HTTP**: anything important can’t live only in process memory (caches may exist but must be optional).
- **Multi-replica support**: ensure behavior is correct under multiple workers/instances (rate limits, sessions, WebSockets, caches).
- **WebSockets at scale**: this repo targets single-instance delivery; scaling beyond one instance requires additional infrastructure outside MozaiksCore.

### 3) Data layer contracts, indexes, and migrations

- **Schema/contracts**: define Pydantic models per API and per persisted document shape (even with Mongo).
- **Index management**: indexes are part of app boot/migration, not tribal knowledge.
- **Migration story**: versioned migration scripts for Mongo collections (rename fields, backfills, new indexes, etc.).
- **Multi-tenancy (if needed)**: make tenant scoping a first-class part of every query (org/workspace id).

### 3.1) Handling “a creator has multiple apps” (platform multi-app)

If creators can have multiple apps on Mozaiks, the simplest enterprise architecture is:

- **One deployment per app** (one MozaiksCore instance per app), with `MOZAIKS_APP_ID` set for that deployment.
- **Isolation by default**: each app uses its own database (or at least its own `DATABASE_NAME`) inside the same Mongo cluster.
- **The platform routes users to the right app** (domain/subdomain/app selection) and provisions the deployment.

This keeps MozaiksCore app-agnostic and avoids “one runtime serving many apps” complexity.

If you instead want **one MozaiksCore runtime to serve many apps** (multi-app in one deployment), then MozaiksCore must become app-aware everywhere:

- every collection/document needs an `app_id`
- every query must include `app_id`
- every config (theme/nav/settings/modules) becomes “per app”

That is possible, but it’s a much larger foundation change and is usually done later.

### 4) Background work, workflows, and idempotency

- **Queues/workers**: long-running work leaves the request/response path (email, AI calls, billing sync, analytics aggregation).
- **Retries + idempotency**: job semantics need stable IDs and safe retries (at-least-once delivery).
- **Scheduling**: recurring work belongs in a scheduler/worker, not request handlers.

### 5) API design maturity (contracts & compatibility)

- **Typed request/response models** everywhere (consistent error shape, pagination, filtering).
- **Backwards compatibility** rules: versioning or explicit compatibility guarantees for core APIs and module interfaces.
- **Contract tests** for key endpoints (especially module execution contracts).

### 6) Observability as a first-class feature

- **Structured logs** (JSON), correlation IDs, and consistent log fields.
- **Metrics** and basic SLO-oriented dashboards (latency/error rates, queue depth, WS connections, DB health).
- **Tracing** (OpenTelemetry) to follow a request across async boundaries and external calls.

### 7) Developer experience (DX) like JHipster

JHipster’s “enterprise feel” comes from consistency and generation:

- **A generator/CLI** that creates modules/features with the right folder structure + config entries + stubs.
- **Standard patterns** baked in (CRUD, settings, notifications, navigation, subscription gating).
- **Upgrade path**: the generator can update/patch scaffolding over time without breaking apps.

### 8) Frontend architecture consistency

- **Module routing and lazy loading** should follow the registry (avoid “bypass” imports).
- **Error boundaries + resilience** for dynamic modules (bad module shouldn’t crash the shell).
- **Typed API client** (even lightweight) so UI ↔ API contracts don’t drift.

## P0 (Must Fix Before Calling It "Production Ready")

### 1) Prevent privilege escalation in profile updates

If any “update profile” endpoint accepts arbitrary keys, users can attempt to set sensitive fields (e.g., `is_admin`, `disabled`, etc.).

- Fixed by restricting updates to an allowlist derived from `settings_config.json` (`backend/core/director.py`).

### 2) Decide what “modules” mean (trusted vs untrusted)

Right now, “plugins” run **in-process** with full server privileges. That’s fine for trusted modules, but it is not a sandbox.

- Recommendation:
  - Rename the concept to **modules/features** in docs and mental model.
  - Make runtime loading deterministic: load from registry at boot; avoid auto-refresh in production.
  - If you ever plan to run untrusted code, move execution out-of-process (separate container/service) with explicit capability permissions.

### 3) Remove “access bypass” behavior in the frontend

Any UI path that loads modules even when they aren’t listed/accessible increases accidental exposure.

- Current example: direct-import bypass logic in `src/core/plugins/DynamicUIComponent.jsx`.

### 4) Replace in-memory “state” in any multi-replica deployment

In-process state breaks when you scale beyond a single instance:

- rate limiting is process-local (`backend/security/rate_limit.py`)
- `state_manager` is process-local (`backend/core/state_manager.py`)
- websocket connection tracking is process-local (`backend/core/websocket_manager.py`)

Recommendation:
- Use Redis (or equivalent) for distributed rate limiting and shared state.
- If you need multi-instance WebSockets, you will need additional infrastructure; MozaiksCore does not ship with cross-instance delivery.

### 5) Don't write configuration files at runtime

Avoid mutating config files on disk at runtime (concurrent writes, container filesystems, non-deterministic behavior).

Current direction:
- Treat JSON configs as **build-time** inputs only (immutable at runtime).
- If you need “derived config” (like plugin notification toggles), compute/merge it **in memory** when serving config (or store derived state in MongoDB), but don’t rewrite the source file.

### 6) Token storage (XSS blast radius)

The frontend uses `localStorage` for the auth token (`src/auth/AuthContext.jsx`). If you ever get an XSS, tokens are trivial to exfiltrate.

Recommendation:
- Prefer httpOnly cookies (CSRF-protected) for browser sessions, or implement a strict CSP + additional XSS mitigations if you keep bearer tokens.

### 7) Dependency hygiene

- Pin backend dependencies (exact versions) and run automated scanning (Dependabot + CodeQL + SCA).
- Remove duplicate JWT stacks if possible (use one library consistently).

## P1 (Enterprise-Grade Scale & Operations)

- **Background jobs**: offload long-running work (email, AI calls, billing sync) to a queue/worker (Celery/RQ/Arq).
- **Audit logging**: immutable audit events for auth changes, role changes, billing changes, settings changes.
- **Multi-tenancy** (if required): first-class org/workspace model, tenant-aware DB queries, tenant-level RBAC.
- **Observability**: structured logs + tracing + metrics; define SLOs and error budgets.
- **API hardening**: request schemas (Pydantic models), strict input validation, consistent error formats, and response contracts.

## Agent/App-Generator Safety Model (High Level)

Even with mandatory human review, assume the “agent PR creator” can be targeted. Design so it cannot ship to prod.

Minimum posture:
- agent can **only open PRs**, never merge
- protected branches require approvals and required checks
- PR CI runs with **no deploy secrets**
- deploy runs only on merge/tag with protected environments

## Recommended Direction for MozaiksCore (Open Source)

- Keep MozaiksCore as the **secure, self-hostable scaffold**.
- Treat feature modules as first-party code (trusted, reviewed, versioned).
- Make the .NET platform optional value-add: repo provisioning, agent-to-PR, CI templates, hosted deployments, managed auth/billing/observability, and revenue share automation (when used).

## MozaiksCore-Specific Architecture To-Dos (Non-Security)

These are “engineering maturity” items that make the foundation feel enterprise:

- **Unify runtime configuration**: avoid mixing `os.getenv(...)` and `settings`; pick one source of truth.
- **Add health endpoints**: `/healthz` (liveness) and `/readyz` (dependency readiness).
- **Clarify module loading rules**: disable auto-refresh in production, document when modules load, and remove unused/duplicated routing code paths.
- **Define a migration mechanism** for Mongo (including index changes) and add a standard place for migrations.
- **Avoid process-local primitives** for durable/shared state; persist state in Mongo or an external store as needed.
- **Introduce a worker** for async jobs and a clear pattern for “request enqueues job → job updates state → client notified”.

## Suggested Roadmap (Architecture First)

1) Deterministic runtime + health/ready endpoints + config immutability
2) Data contracts + migrations + index management
3) Background jobs + event semantics + idempotency
4) Scaling model (Redis, WS strategy) + observability
5) Generator/CLI for module scaffolding (JHipster-style DX)

## Operational Profiles (Starter / Scale / Enterprise)

Most apps start small. “Enterprise-ready” means they can grow without rewriting the foundation. The way to do that is to ship MozaiksCore with **profiles** (modes) that you can switch per app as it grows.

### Starter (single instance)

Use when: early-stage apps, low traffic, low realtime fanout.

- Infra: one container, one Mongo database.
- Realtime: direct WebSockets from the API container.
- Jobs: in-process only (keep request/response fast; defer heavy work).
- State/rate limits: in-memory only (acceptable because there’s only one instance).

MozaiksCore settings that already exist:
- `ENV=production`
- `ALLOWED_HOSTS=...`
- `FRONTEND_URL=...` and `ADDITIONAL_CORS_ORIGINS=...`
- `WEBSOCKET_MAX_CONNECTIONS_PER_USER=...`
- `MAX_REQUEST_BODY_BYTES=...`
- `PLUGIN_EXEC_TIMEOUT_S=...` and `PLUGIN_EXEC_MAX_CONCURRENCY=...`
- `MOZAIKS_AUTO_REFRESH_PLUGINS=false` (recommended for production determinism)

### Optional: In-app workspaces/orgs (multi-tenancy inside one app)

Not every app needs this. The enterprise way is to make it a **mode**:

- Default: `single_user` (simpler apps).
- Optional: `workspace` (Slack-style apps with orgs/members/roles).

Recommended MozaiksCore switch to support (future/proposed):
- `MOZAIKS_TENANCY_MODE=single_user|workspace`
