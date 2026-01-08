# MozaiksCore — User App Control Plane (LOCKED)

## Purpose
MozaiksCore is the **control plane for user applications**.
It authorizes execution and constructs ExecutionContext.

---

## Responsibilities (MUST)

MozaiksCore MUST:

1. Authenticate users (OIDC/JWKS or configured mode)
2. Authorize execution BEFORE runtime launch
3. Resolve capability_id → workflow_id
4. Compute entitlements, plans, limits, trials
5. Construct ExecutionContext
6. Mint and sign launch tokens
7. Return runtime launch metadata to UI

---

## Prohibited Responsibilities (MUST NEVER)

MozaiksCore MUST NEVER:

- Execute workflows
- Stream chat responses
- Perform runtime orchestration
- Interpret usage telemetry
- Act as an execution engine

---

## Client Contract

Clients:
- Send capability_id ONLY
- Never send workflow_id
- Never send entitlements
- Never send scope

All execution scope is server-owned.

---