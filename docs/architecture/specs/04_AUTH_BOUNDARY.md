# Auth Boundary (Source of Truth)

## The One-Sentence Rule

> **MozaiksAI authenticates requests but does not authorize behavior.**
> Authorization is delegated to the host control plane (Mozaiks backend, MozaiksCore, or a customer app).

---

## Core Mental Model

| Concern | Owner | Examples |
|---------|-------|----------|
| **Authentication** (who is calling?) | MozaiksAI Runtime | JWT validation, signature verification, issuer/audience checks |
| **Authorization** (what can they do?) | MozaiksCore / Host App | Workflow access, subscription checks, feature gates, billing |

**MozaiksAI is an execution engine, not a product.**

Execution engines must authenticate who is calling them, but they must not decide who is allowed to do what.

---

## What MozaiksAI DOES Own

### 1. Transport-Level Authentication (Mandatory)

MozaiksAI verifies that every request comes from a trusted caller:

| Check | Purpose |
|-------|---------|
| JWT signature | Cryptographic proof of token integrity |
| Issuer (`iss`) | Token came from trusted identity provider |
| Audience (`aud`) | Token was issued for this runtime |
| Expiration (`exp`) | Token is not stale |
| Token structure | Expected claims are present |

This applies to:
- HTTP requests (`Authorization: Bearer <token>`)
- WebSocket connections (`?token=<token>` or subprotocol)
- Tool callbacks (if exposed externally)
- Streaming events (session correlation)

**Without transport auth, MozaiksAI is an open execution surface — unacceptable.**

### 2. Trust Boundary Enforcement (Mandatory)

MozaiksAI answers exactly one question:

> "Is this request coming from a trusted control plane or host app?"

That's it.

### 3. ExecutionContext Validation

MozaiksAI validates that requests include a well-formed execution context:

```python
# What MozaiksAI validates (authentication)
{
    "sub": "user-id-from-token",      # Who is the caller?
    "iss": "https://trusted-issuer",  # Where did the token come from?
    "aud": "api://mozaiks-auth",      # Is this token for us?
    "app_id": "app-123",              # Which app context? (passed through, not enforced)
    "chat_id": "chat-456"             # Which session? (passed through, not enforced)
}
```

**MozaiksAI does NOT decide** whether `user-id` is allowed to access `app-123`. That's MozaiksCore's job.

---

## What MozaiksAI MUST NOT Own

MozaiksAI must not own:

| Concern | Why Not |
|---------|---------|
| User accounts | Product concern, not runtime |
| App ownership | Business logic, not execution |
| Plans / subscriptions | Billing is control-plane |
| Entitlements | Feature access is policy |
| Pricing / metering decisions | Economic protocol is separate |
| Tenant policy | Host app defines policy |

If MozaiksAI starts owning these, you lose:
- **Statelessness** — runtime becomes coupled to business state
- **Embeddability** — can't drop into arbitrary host apps
- **Clean self-host support** — customers can't bring their own IdP

---

## Deployment Scenarios

### Hosted SaaS (Mozaiks Cloud)

```
Browser
  → MozaiksCore (auth, billing, broker)
      → MozaiksAI Runtime
```

MozaiksAI sees:
- A validated JWT (already authenticated by MozaiksCore)
- An ExecutionContext issued by MozaiksCore

MozaiksAI verifies:
- Token signature (JWKS)
- Token issuer (MozaiksCore's IdP)
- Context integrity

MozaiksCore has already decided:
- User is logged in
- User has access to this app
- App subscription is active
- Workflow is entitled

### Self-Hosted Runtime

```
Customer App
  → Embedded MozaiksAI Runtime
```

Here, the customer app becomes the control plane.

MozaiksAI still:
- Validates JWTs
- Verifies issuer/audience

But:
- The customer chooses the IdP
- The customer defines authorization
- MozaiksAI stays generic

---

## Auth Configuration Contract

MozaiksAI startup config includes:

| Config | Purpose | Env Var |
|--------|---------|---------|
| Accepted issuers | Trust boundary | `MOZAIKS_OIDC_AUTHORITY` |
| Accepted audiences | Token targeting | `AUTH_AUDIENCE` |
| JWKS endpoint | Public key discovery | Via OIDC discovery or `AUTH_JWKS_URL` |
| Required scope | Token type validation | `AUTH_REQUIRED_SCOPE` |

**No user tables. No plans table. No entitlements table.**

---

## Request Flow (Conceptual)

```python
# On every request:

1. Extract token from request
   → Missing? Reject 401

2. Validate token (MozaiksAI auth module)
   → Invalid signature? Reject 401
   → Wrong issuer? Reject 401
   → Wrong audience? Reject 401
   → Expired? Reject 401
   → Missing required scope? Reject 401

3. Extract identity claims
   → sub, iss, aud, scopes

4. Accept request, proceed to execution
   → Pass identity to workflow context
   → Runtime does NOT check "is user allowed to run workflow X"

# Authorization happens BEFORE the request reaches MozaiksAI
# (in MozaiksCore or customer app)
```

---

## What "Scope" Means in This Context

The `AUTH_REQUIRED_SCOPE` (default: `access_as_user`) is **authentication**, not authorization.

It verifies:
> "This token was issued with the intent to access MozaiksAI as a user-delegated token."

It does NOT verify:
> "This user is allowed to run this specific workflow."

The scope distinguishes token types (user-delegated vs. service-to-service), not business permissions.

---

## Implementation Notes

### Current Auth Module (`core/auth/`)

| Component | Purpose | Auth/Authz |
|-----------|---------|------------|
| `discovery.py` | OIDC discovery document fetch | Auth infra |
| `jwks.py` | Public key caching | Auth infra |
| `jwt_validator.py` | Token validation | Authentication |
| `dependencies.py` | FastAPI request guards | Authentication |
| `websocket.py` | WebSocket connection auth | Authentication |

**All components are authentication. None are authorization.**

### What We Don't Have (By Design)

- No `permissions.py`
- No `entitlements.py`
- No `subscriptions.py`
- No database tables for users/apps/plans
- No "is user allowed to X" checks

### Extension Point for Host Apps

Host apps that embed MozaiksAI can:

1. **Pre-authorize** requests before they reach MozaiksAI
2. **Pass context** via ExecutionContext claims
3. **Intercept** responses for post-processing

MozaiksAI provides hooks but does not implement policy.

---

## Summary

| Question | MozaiksAI Answer |
|----------|------------------|
| Is this token valid? | ✅ Yes (signature, expiry, issuer) |
| Is this token for me? | ✅ Yes (audience check) |
| Who is calling? | ✅ Extracted from `sub` claim |
| Is this user allowed to run this workflow? | ❌ Not my concern |
| Is the app subscribed? | ❌ Not my concern |
| Is billing enabled? | ❌ Not my concern |
| What features can this user access? | ❌ Not my concern |

**MozaiksAI authenticates. MozaiksCore authorizes.**
