# Mozaiks DevOps Plan (Platform + Hosted Apps)

This plan covers two systems you will run:

1) **Mozaiks Platform** (control plane): builders chatting + generating apps.
2) **Hosted Customer Apps** (data plane): each app is deployed as its own MozaiksCore container + database.

The goal is a deployment approach that scales operationally without turning into day-to-day firefighting for every app.

---

## 1) Key Idea: Control Plane vs Data Plane

- **Control plane (platform layer)**:
  - accounts, billing, app creation, deployments, and dashboards
  - owns “which app exists”, “where it runs”, and “who can deploy it”

- **Data plane (app runtimes)**:
  - running app containers that serve API/UI/WebSockets for one app
  - should be stateless and replaceable (config + persisted state live outside the process)

---

## 2) Recommended Deployment Model (Practical Default)

### Default: one deployment per app

When a creator has multiple apps, each app is deployed separately:

- App A -> its own container + its own `MOZAIKS_APP_ID`
- App B -> its own container + its own `MOZAIKS_APP_ID`

This keeps app isolation simple and avoids a “multi-app runtime” inside MozaiksCore.

### Where to host each app runtime (example)

If you’re using Docker + Azure Web Apps + ACR + Mongo:

- **Azure Container Registry (ACR)** stores images per app version.
- **Azure App Service (Web App for Containers)** runs each app container.

Cost/scaling:
- Small apps can share an App Service Plan.
- Big/active apps get their own dedicated plan.

---

## 3) Real-time Chat (Platform + Customer Apps)

You effectively have two chat experiences:

- **Builder chat** (inside the platform)
- **End-user chat** (inside customer apps)

MozaiksCore uses WebSockets directly from the app container. Realtime delivery is designed for a single running app instance.

If you run multiple instances for the same app, WebSocket routing and cross-instance broadcast require additional infrastructure that is outside the scope of this repository.

### Message storage and background work

For chat features, keep these concerns separate:

- **Realtime delivery**: WebSockets
- **Persistence**: Mongo collections for messages/events
- **Background processing**: moderation, notifications, summarization, analytics

---

## 4) Data Strategy (Mongo) for “Many Apps”

### Recommended isolation

For hosted apps, use one of:

- **Database-per-app** (strong isolation, easiest reasoning)
- **Collection-per-app with `app_id` field** (cheaper, but every query must include `app_id`)

Prefer database-per-app unless cost is a blocker.

---

## 10) Phased Rollout (so you don’t get overwhelmed)

### Phase 1: Ship reliably (few apps)

- One deployment per app
- Container registry + web app hosting
- Mongo
- Basic logs/metrics

### Phase 2: Scale responsibly (many apps, some big)

- Dedicated plans for noisy neighbors
- Tighter per-app limits/quotas
- Clear incident playbooks and rollback paths

### Phase 3: Enterprise operations

- Multi-region strategy
- Disaster recovery exercises
- SLOs + on-call rotation
- Per-app cost controls and quotas

---

## 11) Decisions you still need to make (later is fine)

- Do customer apps need in-app workspaces/orgs (Slack-style), or mostly single-user?
- Do you want “multi-app in one runtime” (hard) or keep “one deployment per app” (simple)?

---

## 12) What we should implement next (foundation-facing)

- Health probes (`/healthz`, `/readyz`) so hosting can restart unhealthy instances.
- Deterministic module loading at startup (deploy-time), not runtime hot-load in production.
- A migration mechanism for Mongo (indexes + backfills).
