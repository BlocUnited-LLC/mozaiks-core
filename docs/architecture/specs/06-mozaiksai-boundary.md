# MozaiksAI Runtime — Boundary & Execution Contract (LOCKED)

## Purpose
This document is a **hard architectural contract** for the MozaiksAI repository.
Any change that violates this document is considered a **breaking architectural regression**.

---

## Responsibilities (MUST)

MozaiksAI MUST:

1. Authenticate all inbound traffic
   - Validate JWT signature, issuer, audience, expiration, and required scope
   - Apply to HTTP, WebSocket, streaming, and callback paths

2. Validate ExecutionContext structure and signature
   - Accept only server-issued, signed launch tokens
   - Validate shape + signature only
   - Treat all fields as opaque

3. Execute workflows exactly as specified
   - No policy interpretation
   - No scope widening
   - No runtime decisions

4. Measure and emit usage
   - Token counts
   - Tool calls
   - Step execution
   - Emission only (never interpretation)

---

## Prohibited Responsibilities (MUST NEVER)

MozaiksAI MUST NEVER:

- Decide whether execution is allowed
- Enforce billing, trials, quotas, or limits
- Pause, throttle, or terminate execution for policy reasons
- Call Mozaiks backend APIs
- Fetch entitlements or plans
- Store long-lived user, app, or billing state
- Accept workflow_id, entitlements, or scope from the client

If any feature request requires the above → STOP and escalate.

---

## Execution Flow (MANDATORY)

1. Receive request + JWT
2. Authenticate transport
3. Validate ExecutionContext signature
4. Execute workflow blindly
5. Emit usage telemetry
6. Stream outputs

There is no alternative flow.

---

## Decision Rule

If logic feels like:
- policy
- enforcement
- billing
- entitlement
- authorization

It does not belong in this repo.

---