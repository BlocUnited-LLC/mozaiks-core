# Mozi (Frontend/UI) — Subscriptions UI + Post-Build “Deploy Step” Page (2026-01-02)

This is a **fresh UI handoff doc** for Mozi.

It covers:
- what UI exists today in this repo
- what the Deploy step must do (pay-gated)
- what we still need to clarify about “Create App” + “Build completion” UX

---

## 1) What exists today (MozaiksCore frontend)
Routes in [src/App.jsx](src/App.jsx):
- `/profile` (Profile + settings + plugin settings)
- `/plugins/:pluginName` (plugin UI)
- `/subscriptions` (only when monetization enabled)

There is **no** Create App page, Projects page, Build page, or Deploy page implemented here.

The only “assistant UI” is the chatbot overlay (floating button + window). It can emit navigation actions (e.g. “go to subscriptions”).

Important clarification (from Mozi app):
- “Create App” is already a dedicated page/form in the Mozi UI (not a chat flow).
- Multiple entry points route to `/create-app`, which renders `CreateApp.js → CreateAppComponent.js`.
- On create, the backend returns the full app object immediately, including a stable `appId`.

---

## 2) Subscriptions UI (already implemented)
The subscription management page exists:
- [src/subscription/SubscriptionPage.jsx](src/subscription/SubscriptionPage.jsx)
- Data comes from [src/subscription/SubscriptionContext.jsx](src/subscription/SubscriptionContext.jsx)

Backend endpoints (only when `MONETIZATION=1`) are defined in [backend/core/director.py](backend/core/director.py):
- `GET /api/subscription-plans`
- `GET /api/user-subscription`
- `POST /api/update-subscription`
- `POST /api/cancel-subscription`

So: Mozi does **not** need to invent subscription UI — it exists and is wired.

---

## 3) What you need to build next: a Post-Build “Deploy Step” screen
Product constraint:
- build/test/preview is free
- payment happens at the Deploy step

We need a simple screen shown after the user completes the build stage.

### 3.1 Minimal UI states
The Deploy step must handle three cases:

A) No paid plan:
- show paywall / plan selector / still can access their app dashboard
- block deploy

B) Paid plan, tokens but no hosting (build/token-only):
- primary CTA: “Export Repo” (enabled once build is complete)
- optional secondary CTA: “BYO-Cloud” (disabled; “Coming soon”)
- show note: “Hosting not included on this plan.”
- update users token amount
- block deploy

C) Paid hosting plan via Mozaiks:
- show hosting plan details
- CTA: “Deploy” (enabled)

---

## 4) The missing piece: where does this page live?
Decision (based on current product direction):
- The Deploy step screen lives in the Mozaiks .NET / Mozi UI.
- MozaiksAI handles the build workflow, but it must **signal build completion** back to the platform so the UI can transition to Deploy.

Build-complete notification process (required):
- Canonical push: MozaiksAI calls `POST /api/apps/{appId}/build-events`.
- Platform persists the build status and emits a real-time signal to the user:
   - preferred: WebSocket event (so the UI can auto-redirect)
   - optional: create an in-app notification (so the user can click later)
- Pull fallback: UI can still poll `GET /api/apps/{appId}/build-latest` on refresh.

---

## 5) Confirmed routing + identifiers (from Mozi app)
1) “Create App” is a dedicated page/form.
   - Entry points include landing cards and dashboard CTAs.
   - These route to `/create-app`.

2) After app creation (today):
   - There is no active build process yet.
   - The create flow has intent to route to a “Plot Course” page (commented-out navigate).
   - Decision: next screen is **Plot Course** (`/course/:appId`) as the primary path.

3) Stable identifier:
   - Use `appId` (database ID / GUID) returned immediately on create.
   - Routes are already modeled around `:appId`.

4) Decision: Deploy step route
   - Add/route to `/deploy/:appId` when build is complete (auto-redirect on WS event).

---

## 6) Inputs Mozi can assume (from backend)
For the Deploy step screen, Mozi should assume these are available (exact endpoint names TBD):
- `currentPlan: { name, includesHosting:boolean }`
- `hostingStatus: { supported:boolean, ready:boolean, lastError?:string }` (Mozaiks-hosted)
- `byoCloudStatus: { supported:boolean }` (expected `false` initially; “Coming soon”)

