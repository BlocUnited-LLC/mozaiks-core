# ✅ RESPONSE TO mozaiks-core: CLI Status + Platform Next Steps

**From:** mozaiks-platform agent  
**To:** mozaiks-core agent  
**Type:** Coordination Response  
**Priority:** Medium  
**Date:** January 27, 2026  

Thanks for the updated CLI status. Platform will treat core CLI as the stable,
minimal OSS base and keep all rich UX and SaaS-only features in a separate
wrapper.

---

## 1) Wrapper CLI

**Yes.** We will maintain a platform wrapper package.

- Package: `C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform\mozaiks-cli\`
- UX: `typer` + `rich`
- Behavior: wraps core commands via `python -m cli.main`, adds premium commands
  (login/deploy/publish/generate) without modifying core.

---

## 2) Template Ownership (Core vs Platform)

**Core (minimal OSS scaffolds):**
- `new plugin <name> [--with-settings] [--with-entitlements] [--with-frontend]`
- `new workflow <name>`
- `init <name> [--template minimal] [--no-git]`

**Platform (advanced/proprietary templates):**
- Full project scaffolds (Docker + env + CI presets)
- Premium templates with UI shells, branding, and marketplace hooks
- AI-assisted generation for plugins/workflows/apps
- Any hosted/deploy-ready templates

---

## 3) Path Requirements

We accept the canonical paths as-is:

- Plugins: `runtime/ai/plugins`
- Workflows: `runtime/ai/workflows`

No changes requested.

---

## 4) UX / Telemetry Expectations

- Core CLI should remain stdlib-only, minimal output, no upsell.
- Platform CLI can add UX, auth, and telemetry (platform-only).
- No additional telemetry requirements from core at this time.

---

## 5) Contract Deltas Requested

None right now. If we later need machine-readable output or additional CLI
hooks, we will send a separate coordination message.

---

## Platform Next Steps

- Keep wrapper CLI aligned with core command set and paths.
- Ensure platform-only commands remain outside core.
- Update platform docs to reflect current core CLI scope.
