# MozaiksAI Runtime Boundary Contract

> **Status**: LOCKED  
> **Effective**: 2026-01-06  
> **Authority**: Architecture decision — do not modify without explicit review.

---

## Core Identity

**MozaiksAI is a stateless execution engine.**

It runs workflows. It does not make decisions about whether workflows *should* run.

---

## What the Runtime Does

| Responsibility | Description |
|----------------|-------------|
| **Authenticate transport** | Validate JWT signature, issuer, audience, expiration, and scope. |
| **Execute workflows** | Run declarative workflows blindly within the scope of a server-issued launch token. |
| **Measure and emit usage** | Produce usage events as informational signals. Never interpret them. |

---

## What the Runtime Never Does

| Prohibited | Rationale |
|------------|-----------|
| **Authorize execution** | Authorization is a control-plane decision made *before* launch. |
| **Enforce billing, limits, trials, or entitlements** | Business policy belongs in MozaiksCore, not the execution layer. |
| **Pause or terminate execution for policy reasons** | The runtime executes; it does not judge. |
| **Call Mozaiks backend APIs** | The runtime is downstream-only. It receives context; it does not fetch or validate it. |
| **Recompute, widen, or reinterpret scope** | Scope is server-owned. Runtime validates shape + signature only. |

---

## ExecutionContext Contract

1. **Server-owned**: The `ExecutionContext` (launch token payload) is issued by the control plane.
2. **Shape + signature validation only**: The runtime confirms the token is well-formed and authentically signed.
3. **No recomputation**: The runtime must not derive, expand, or second-guess any field in the context.
4. **Opaque execution**: The runtime treats context values as opaque inputs to workflow execution.

---

## Usage Events

- Usage events are **informational signals only**.
- They are emitted for observability, analytics, and downstream billing reconciliation.
- The runtime **never acts on usage data** (no throttling, no gating, no enforcement).
- Any future "gating" logic must live in the **control plane before runtime launch**.

---

## Decision Rule

> **If anything in the runtime ever feels like policy, enforcement, or business logic — STOP and ASK.**

Examples that should trigger review:

- "Check if the user has credits remaining"
- "Block execution if subscription is expired"
- "Limit requests per minute"
- "Verify the user is allowed to use this workflow"
- "Call an API to get current entitlements"

All of the above belong in the control plane, not here.

---

## Architectural Rationale

| Principle | Benefit |
|-----------|---------|
| **Stateless** | Horizontal scaling, zero warm-up, predictable behavior. |
| **Separation of concerns** | Control plane owns policy; runtime owns execution. |
| **Open-source ready** | Runtime can be published without exposing business logic. |
| **Testable** | Execution behavior is deterministic given inputs. |
| **Auditable** | Clear boundary makes compliance and security review tractable. |

---

## Revision History

| Date | Change | Author |
|------|--------|--------|
| 2026-01-06 | Initial lock of runtime boundary contract | Architecture |
