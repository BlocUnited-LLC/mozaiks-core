# Capability Mapping Runtime Contract

**Version:** 1.0.0  
**Status:** Active  
**Last Updated:** 2026-02-02

---

## Overview

Capability Mapping provides a **plan-agnostic entitlement layer** for the mozaiks runtime.
Instead of scattered plan checks, all access control flows through capabilities:

```
Plan → Capabilities → Workflows/Tools/Artifacts/Limits
```

**Core only sees capabilities, never plans.** Platform resolves plan → capabilities
before passing entitlements to the runtime.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        PLATFORM                                  │
├─────────────────────────────────────────────────────────────────┤
│  Plan Selection  →  plan_capabilities.json  →  Entitlements     │
│  (Stripe/Hosting)    (plan → caps mapping)     (per-request)    │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼ entitlement_manifest.json or API
┌─────────────────────────────────────────────────────────────────┐
│                         CORE RUNTIME                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─────────────────┐    ┌─────────────────┐                    │
│  │ EntitlementLoader│    │CapabilityEval. │                    │
│  │  (file/API/stub)│───▶│ (has/limit)    │                    │
│  └─────────────────┘    └────────┬────────┘                    │
│                                   │                              │
│                                   ▼                              │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │              CapabilityEnforcer                          │   │
│  │                                                          │   │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │   │
│  │  │  Workflow    │  │    Tool      │  │   Artifact   │  │   │
│  │  │  Enforcement │  │ Enforcement  │  │  Enforcement │  │   │
│  │  └──────────────┘  └──────────────┘  └──────────────┘  │   │
│  │                                                          │   │
│  │  ┌──────────────────────────────────────────────────┐  │   │
│  │  │              Limit Enforcement                     │  │   │
│  │  │  (tokens_monthly, workflows_daily, etc.)          │  │   │
│  │  └──────────────────────────────────────────────────┘  │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Entitlement Manifest Schema

Core accepts entitlements in this format (supports both structures):

### Preferred Format (metadata block)

```json
{
  "app_id": "myapp",
  "user_id": "user_123",
  "metadata": {
    "capabilities": [
      "cap.workflow.analytics",
      "cap.workflow.basic",
      "cap.tool.generate_report",
      "cap.artifact.view"
    ],
    "limits": {
      "cap.limit.tokens_monthly": 100000,
      "cap.limit.workflows_daily": 50
    },
    "capability_source": "plan_capabilities.json"
  }
}
```

### Alternative Format (top-level)

```json
{
  "app_id": "myapp",
  "capabilities": ["cap.workflow.basic", "cap.tool.basic"],
  "limits": {
    "cap.limit.tokens_monthly": 50000
  }
}
```

**Core supports both formats for flexibility.**

---

## Capability Naming Convention

```
cap.<type>.<name>

Types:
  - workflow   → cap.workflow.analytics
  - tool       → cap.tool.generate_report
  - artifact   → cap.artifact.export
  - limit      → cap.limit.tokens_monthly
  - scope      → cap.scope.tenant (future)
```

---

## Enforcement Points

### 1. Workflow Start

Before a workflow begins execution:

```python
from mozaiks_ai.runtime.entitlements import CapabilityEnforcer

enforcer = CapabilityEnforcer(entitlements)

# Check workflow capability
enforcer.require_workflow(f"cap.workflow.{workflow_name}")
```

### 2. Tool Execution

Before a tool is invoked:

```python
# Check tool capability
enforcer.require_tool(f"cap.tool.{tool_name}")
```

### 3. Artifact Access

Before artifact data is returned:

```python
# Check artifact capability
enforcer.require_artifact("cap.artifact.export")
```

### 4. Limit Enforcement

Before consuming a limited resource:

```python
# Check token limit
current_tokens = get_monthly_token_usage(user_id)
enforcer.require_within_limit("cap.limit.tokens_monthly", current_tokens)
```

---

## Runtime API

### Loading Entitlements

```python
from mozaiks_ai.runtime.entitlements import load_entitlements

# Priority: runtime_entitlements > file > defaults
manifest = load_entitlements(
    app_id="myapp",
    app_path=Path("/apps/myapp"),
    runtime_entitlements=request.entitlements,  # From API
    user_id="user_123"
)
```

### Evaluating Capabilities

```python
from mozaiks_ai.runtime.entitlements import CapabilityEvaluator

evaluator = CapabilityEvaluator(entitlements)

# Simple checks
if evaluator.has("cap.workflow.analytics"):
    # Allow analytics

# Multiple capabilities
if evaluator.has_all(["cap.workflow.basic", "cap.tool.basic"]):
    # Allow workflow with tools

# Limit checks
monthly_limit = evaluator.get_limit("cap.limit.tokens_monthly")
if evaluator.check_limit("cap.limit.tokens_monthly", current_usage):
    # Within limit
```

### Enforcing Capabilities

```python
from mozaiks_ai.runtime.entitlements import (
    CapabilityEnforcer,
    CapabilityDeniedError,
    LimitExceededError
)

enforcer = CapabilityEnforcer(entitlements, strict=True)

try:
    enforcer.require("cap.workflow.analytics")
    enforcer.require_within_limit("cap.limit.tokens_monthly", usage)
except CapabilityDeniedError as e:
    # Handle denial (return 403, show upgrade prompt, etc.)
    logger.warning(f"Denied: {e.capability_id}")
except LimitExceededError as e:
    # Handle limit exceeded
    logger.warning(f"Limit exceeded: {e.limit_id} ({e.current_usage}/{e.limit_value})")
```

### Decorator-Based Enforcement

```python
from mozaiks_ai.runtime.entitlements import require_capability

@require_capability("cap.workflow.analytics")
async def run_analytics_workflow(entitlements: dict, data: dict):
    """Only runs if capability is present."""
    ...
```

---

## Graceful Degradation

### Self-Hosted / OSS Mode

When no entitlements are provided, Core uses generous defaults:

```python
DEFAULT_CAPABILITIES = [
    "cap.workflow.basic",
    "cap.tool.basic",
    "cap.artifact.view",
]

DEFAULT_LIMITS = {
    "cap.limit.tokens_monthly": 100_000,
    "cap.limit.workflows_daily": 100,
    "cap.limit.tools_per_workflow": 20,
}
```

This ensures:
- Self-hosters can use Core without entitlement setup
- OSS users have reasonable access
- Platform can override with real entitlements

### Strict vs Non-Strict Mode

```python
# Strict (default): raises on denial
enforcer = CapabilityEnforcer(entitlements, strict=True)

# Non-strict: logs warning, returns False
enforcer = CapabilityEnforcer(entitlements, strict=False)
allowed = enforcer.require("cap.x")  # Returns False if denied
```

---

## Integration with Platform

### Platform Responsibilities

1. **Map plans to capabilities** in `plan_capabilities.json`
2. **Generate `entitlement_manifest.json`** for each app bundle
3. **Pass runtime entitlements** via API for dynamic enforcement
4. **Update SubscriptionManager** to include capabilities in bundles

### Core Responsibilities

1. **Load entitlements** from file or runtime injection
2. **Evaluate capabilities** (has, has_all, has_any, limits)
3. **Enforce at control points** (workflow, tool, artifact)
4. **Provide defaults** for OSS/development mode

---

## Token Management Integration

Token budgets are first-class limits:

```python
# In workflow execution
enforcer = CapabilityEnforcer(entitlements)

# Before making LLM call
current_monthly_tokens = await get_token_usage(user_id, app_id)
enforcer.require_within_limit("cap.limit.tokens_monthly", current_monthly_tokens)

# After LLM call
await record_token_usage(user_id, app_id, tokens_used)
```

---

## Error Responses

### CapabilityDeniedError

```python
{
    "error": "capability_denied",
    "capability_id": "cap.workflow.analytics",
    "available_capabilities": ["cap.workflow.basic"],
    "message": "Upgrade required for analytics workflows"
}
```

### LimitExceededError

```python
{
    "error": "limit_exceeded",
    "limit_id": "cap.limit.tokens_monthly",
    "limit_value": 100000,
    "current_usage": 105234,
    "message": "Monthly token limit exceeded"
}
```

---

## Security Hardening

### 1. Signature Verification

Entitlement manifests can be cryptographically signed to prevent tampering:

```python
from mozaiks_ai.runtime.entitlements import EntitlementSigner, SecureEntitlementContext

# Platform signs manifests before sending
signer = EntitlementSigner(signing_key="secret")
signed_manifest = signer.sign_manifest(manifest)

# Core verifies signatures
ctx = SecureEntitlementContext(
    app_id="myapp",
    entitlements=signed_manifest,
    strict=True  # Raises if invalid
)
ctx.verify_signature()
```

**Enable in production:** Set `MOZAIKS_ENTITLEMENT_SIGNING_KEY` environment variable.

### 2. Audit Logging

All capability checks are logged to a dedicated security audit logger:

```python
# Audit events logged automatically
ctx.has_capability("cap.workflow.analytics")
# Logs: [AUDIT] {"event_type": "capability_check", "result": "allowed", ...}
```

Audit log categories:
- `capability_check` - Individual capability evaluations
- `limit_check` - Limit evaluations with utilization %
- `signature_verification` - Manifest signature checks
- `tenant_isolation` - Cross-tenant access attempts
- `anomaly_detected` - Suspicious patterns

### 3. Tenant Isolation

Every operation validates tenant isolation:

```python
from mozaiks_ai.runtime.entitlements import TenantIsolationValidator, TenantIsolationError

validator = TenantIsolationValidator(app_id="myapp")

# Validate resource access
try:
    validator.validate_resource(
        resource_app_id="other_app",  # BLOCKED
        resource_type="workflow",
        resource_id="wf_123"
    )
except TenantIsolationError:
    # Cross-tenant access denied
    ...
```

### 4. Anomaly Detection

Detects suspicious capability usage patterns:

| Pattern | Threshold | Action |
|---------|-----------|--------|
| Rapid denials | >10/minute | Log anomaly |
| Session denials | >50 total | Log anomaly |
| Repeated limit hits | 3x in 5 min | Log anomaly |

```python
# Anomalies logged automatically when using SecureEntitlementContext
ctx.has_capability("cap.premium")  # If denied, anomaly detector tracks
```

### 5. Secure Context (Recommended)

Use `SecureEntitlementContext` for full security:

```python
from mozaiks_ai.runtime.entitlements import SecureEntitlementContext

ctx = SecureEntitlementContext(
    app_id="myapp",
    user_id="user_123",
    entitlements=manifest,
    strict=True
)

# Verify signature first
ctx.verify_signature()

# Validate tenant isolation
ctx.validate_tenant(requested_app_id)

# Check capabilities with audit logging
if ctx.has_capability("cap.workflow.analytics"):
    ...

# Check limits with audit logging
if ctx.check_limit("cap.limit.tokens_monthly", current_usage):
    ...

# Get audit trail
events = ctx.audit_events
```

---

## Changelog

### v1.1.0 (2026-02-02)
- Added signature verification for entitlement manifests
- Added audit logging for all capability checks
- Added tenant isolation validation
- Added anomaly detection for suspicious patterns
- Added SecureEntitlementContext wrapper

### v1.0.0 (2026-02-02)
- Initial contract definition
- Entitlement manifest schema (both formats)
- Capability evaluator + enforcer
- Limit checking
- Decorator support
- Default capabilities for OSS

---

## Coordination Status

### ✅ Core Implementation Complete (2026-02-02)

| Component | Location |
|-----------|----------|
| Python Module | `mozaiks_ai.runtime.entitlements` |
| Evaluator | `evaluator.py` - has/has_all/has_any/limits |
| Loader | `loader.py` - file/API/default entitlements |
| Enforcer | `enforcer.py` - require/require_within_limit |

### ✅ Platform Integration Confirmed (2026-02-02)

**Platform has implemented:**
- `config/capabilities.json` (registry)
- `config/plan_capabilities.json` (plan → capability mapping)
- SubscriptionManager generates entitlement files in app bundles
- Token limits as first-class capabilities (`cap.limit.tokens_monthly`)

**Confirmed Decisions:**

1. **Where capability checks live:** Both workflow router AND tool dispatcher (defense-in-depth)
2. **Entitlement field location:** Core supports both `metadata.capabilities` AND top-level `capabilities`
3. **Plan resolution:** Platform resolves plan → capabilities; Core only sees capabilities (plan-agnostic)

**Integration Model:**
- Platform passes entitlements via `entitlement_manifest.json` or API injection
- UI hides/shows based on capabilities, **Core enforces**
- Self-hosters get generous defaults without entitlement setup
- Token budgets treated as capability limits

---

🤝 **INTEGRATION COMPLETE** — Both Core and Platform are aligned.

