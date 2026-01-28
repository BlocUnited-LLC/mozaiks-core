# Repo Jobs + Stateless LLM Operating Model

**Date:** January 27, 2026

This document defines the **two-job** workflow for managing auth + runtime evolution across:
- `C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-core`
- `C:\Users\mbari\OneDrive\Desktop\BlocUnited\BlocUnited Code\mozaiks-platform`

It is designed for **stateless LLMs** working across repos.

---

## North Star

- **mozaiks-core** is OSS runtime infrastructure.
- **mozaiks-platform** is a consumer/product that *uses* mozaiks-core.
- Platform can be the first “reference consumer”, but **core must remain app-agnostic**.

---

## Job A — mozaiks-core Job (OSS Runtime Owner)

**Primary responsibilities**
- Own runtime behavior, plugin execution semantics, auth validation semantics, and stable contracts.
- Ensure “self-hostable” remains true by default.
- Prevent any SaaS/control-plane coupling from leaking into core.

**Auth boundary**
- Core validates JWTs issued by a self-hostable OIDC provider (canonical: Keycloak).
- Core never contains platform billing/entitlement logic.

**Deliverables**
- Versioned docs under `mozaiks-core/docs/contracts/*`.
- Runtime configuration reference.
- Minimal, explicit env var scheme.

**Hard rule**
- If a change alters a public contract or requires platform behavior assumptions, emit:

🔁 MESSAGE TO mozaiks-platform (VIA HUMAN)

…and stop until confirmed.

---

## Job B — mozaiks-platform Job (Consumer + Reference Implementation)

**Primary responsibilities**
- Consume core contracts without special-casing.
- Provide the first real-world use case that proves core works.
- Keep platform auth, policies, and service wiring consistent.

**Auth boundary**
- Platform validates the same OIDC JWTs as core (Authority/Audience).
- Platform does not implement a second issuer.

---

## Cross-Repo Reality: AppGenerator workflows

Platform currently owns workflows under:
- `mozaiks-platform/ai-models/workflows/*`

Those workflows can generate app scaffolds that may resemble “platform-like” code, but:
- Any reusable runtime capability must migrate into **mozaiks-core** behind explicit contracts.
- Generated app templates should remain **consumer templates**, not runtime source-of-truth.

**Rule of thumb**
- If it’s a runtime capability: it belongs in core.
- If it’s a product template or opinionated starter: it belongs in platform.

---

## Stateless LLM Handoff Rules

When an LLM takes over:

1) Always paste the absolute repo root you’re operating in.
2) Identify which job you are performing: **Core Job** or **Platform Job**.
3) List the “winner modules” (single source of truth) for the current effort.
4) Add verification commands that prove the change is complete.
5) Avoid mixing instructions for both repos in the same patch unless it’s purely documentation.

---

## Canonical Auth “Winner” Modules

- Core winner: `mozaiks-core/runtime/ai/core/ai_runtime/auth/*`
- Platform winner: `mozaiks-platform/src/BuildingBlocks/Mozaiks.Auth` (`builder.AddMozaiksAuth()`)

---

## Canonical Workflow Output Safety

Workflow-generated `code_files` must be treated as **sandboxed output**.
They must not write arbitrary paths outside the generated bundle.

If cross-repo mirroring is required (e.g., Keycloak theme overrides), it must be:
- allowlisted
- explicitly opt-in (e.g., `MOZAIKS_CORE_ROOT=...`)
- limited to specific files
