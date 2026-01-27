# Core ↔ Platform API Alignment Audit

> **Date**: January 26, 2026  
> **Purpose**: Systematic comparison of mozaiks-core implementation vs mozaiks-platform expectations  
> **Status**: 🔍 Audit Complete - Action Required

---

## Executive Summary

| Area | Status | Severity |
|------|--------|----------|
| Runtime Contract v1.0.0 | ✅ Aligned | - |
| Plugin Context Injection | ✅ Aligned | - |
| Entitlement v1.1 Sync Endpoint | ⚠️ Missing | HIGH |
| Test Suite API Drift | 🔴 Broken | HIGH |
| Usage Events Ingest | ⚠️ Partial | MEDIUM |
| Orchestration Events | ✅ Aligned | - |

---

## Detailed Findings

### 1. Runtime Contract v1.0.0 ✅ ALIGNED

**Platform Expects** (RUNTIME_PLATFORM_CONTRACT_V1.md):
- `X-Mozaiks-Runtime-Version: 1.0.x` header ✅ Implemented
- `GET /api/plugins` endpoint ✅ Implemented (director.py alias)
- `GET /health` with `plugins_loaded` ✅ Implemented
- `POST /api/execute/{plugin_name}` ✅ Implemented
- `user_jwt` in plugin context ✅ Implemented
- `MOZAIKS_PLUGIN_TIMEOUT_SECONDS` ✅ Implemented

**Files**: `runtime/ai/main.py`, `runtime/ai/core/director.py`

---

### 2. Plugin Context Injection ✅ ALIGNED

**Platform Expects** (contracts.py):
```python
PluginContext = {
    "app_id": str,
    "user_id": str,
    "username": str | None,
    "roles": list[str],
    "is_superadmin": bool
}
```

**Core Provides** (director.py:147-155):
```python
data["_context"] = {
    "app_id": APP_ID,
    "user_id": user["user_id"],
    "username": user.get("username"),
    "roles": user.get("roles", []),
    "is_superadmin": user.get("is_superadmin", False),
}
```

✅ Perfect match

---

### 3. Entitlement Manifest Sync ⚠️ MISALIGNED

**Platform Expects** (ENTITLEMENT_CONTRACT_V1.1.md):
```
POST /api/internal/entitlement/sync
Authorization: X-Internal-API-Key: {shared_secret}
```

Schema:
```json
{
  "version": "1.0",
  "app_id": "string",
  "user_id": "string (optional)",
  "tenant_id": "string (optional)",
  "plan": {
    "id": "string",
    "name": "string", 
    "tier": "free|starter|pro|enterprise|unlimited",
    "status": "active|trialing|past_due|canceled",
    "expires_at": "ISO8601 or null"
  },
  "token_budget": {
    "period": "monthly|unlimited",
    "total_tokens": {
      "limit": "integer (-1 = unlimited)",
      "used": "integer",
      "enforcement": "none|warn|soft|hard"
    }
  },
  "features": { ... },
  "rate_limits": { ... },
  "source": "platform"
}
```

**Core Has** (subscription_sync.py):
```
POST /api/internal/subscription/sync
```

Schema:
```json
{
  "userId": "string",
  "plan": "free|pro|enterprise",
  "status": "active|canceled|past_due",
  "billingCycle": "monthly|yearly",
  "nextBillingDate": "YYYY-MM-DD",
  "appId": "string (optional)"
}
```

**Gap Analysis**:
| Field | Platform Expects | Core Has |
|-------|------------------|----------|
| Endpoint | `/api/internal/entitlement/sync` | `/api/internal/subscription/sync` |
| version | Yes | No |
| tenant_id | Yes | No |
| plan object | Nested with tier, expires_at | Flat string |
| token_budget | Full object with enforcement | Not present |
| features | Dict of booleans | Not present |
| rate_limits | Dict of integers | Not present |

**Action Required**: Either:
1. Add `/api/internal/entitlement/sync` endpoint with v1.1 schema, OR
2. Update Platform to use existing `/api/internal/subscription/sync`, OR
3. Extend existing endpoint to accept both schemas

---

### 4. Test Suite API Drift 🔴 BROKEN

**File**: `tests/test_entitlements.py`

Tests expect old API from `src/core/entitlements/`:
```python
EntitlementManifest(app_id="test-app", tier="free", features=[...], enforcement="none")
```

**Actual API** (manifest.py):
```python
EntitlementManifest(version="1.0", app_id="test-app", plan={...}, token_budget={...}, features={...})
```

**Key Mismatches**:
- Constructor requires `version` as first positional arg
- `tier` is now inside `plan.tier` property
- `features` is now a dict, not a list
- `enforcement` is now inside `token_budget.total_tokens.enforcement`
- Sources use async `fetch()` not sync `load()`

**Action Required**: Rewrite `test_entitlements.py` to match actual implementation

---

### 5. Two Entitlement Systems Coexisting ⚠️ CONFUSION

**System 1**: App-level entitlements (src/core/entitlements/)
- `EntitlementManifest` dataclass
- `TokenBudgetTracker`
- `FeatureGate`
- `EntitlementMiddleware`
- Designed for AI workflow token tracking

**System 2**: Plugin-level entitlements (core/entitlements/)
- YAML-based per-plugin config
- `check_feature()`, `check_limit()`, `consume_limit()`
- MongoDB usage tracking
- Designed for plugin feature/limit gates

**Platform Integration Points**:
- Platform sends manifest via `/api/internal/subscription/sync` → System 2
- No clear integration with System 1 token tracking

**Action Required**: Clarify which system Platform should integrate with, or merge them

---

### 6. Usage Events Ingest ⚠️ PARTIAL

**Platform Expects** (ENTITLEMENT_CONTRACT_V1.1.md):
```
POST /api/usage/ingest
```

Request:
```json
{
  "events": [
    {
      "event_id": "uuid",
      "timestamp": "ISO8601",
      "app_id": "string",
      "user_id": "string",
      "workflow_id": "string",
      "model": "gpt-4",
      "input_tokens": 1500,
      "output_tokens": 500,
      "total_tokens": 2000
    }
  ],
  "batch_id": "uuid"
}
```

**Core Has** (USAGE_EVENTS_CONTRACT.md):
- `chat.usage_delta` WebSocket events
- `chat.usage_summary` WebSocket events
- Storage in `ChatSessions.usage_*_final` fields

**Gap**: No HTTP endpoint for Platform to ingest batched usage events

**Note**: Platform doc says "Control-plane usage ingest currently listens to `chat.usage_summary` only" - may be handled via WebSocket, not HTTP

---

### 7. Orchestration Events ✅ ALIGNED

**Platform Expects** (WORKFLOW_LIFECYCLE_MARKERS.md):
```json
{
  "kind": "orchestration.run_started",
  "run_id": "run_123",
  "status": "in_progress|completed|failed|cancelled",
  "timestamp": "ISO-8601"
}
```

**Core Provides** (event_serialization.py):
```python
OrchestrationStatus.IN_PROGRESS = "in_progress"
OrchestrationStatus.COMPLETED = "completed"
OrchestrationStatus.FAILED = "failed"
OrchestrationStatus.CANCELLED = "cancelled"

# Events emitted:
# orchestration.run_started
# orchestration.run_completed
# orchestration.run_failed
# orchestration.agent_started
```

✅ Schema matches Platform expectations

---

## Recommended Actions (Priority Order)

### HIGH Priority

1. **Fix test_entitlements.py** (broken tests block CI)
   - Rewrite tests to match actual `src/core/entitlements/` API
   - Consider adding tests for `core/entitlements/` (plugin-level) too

2. **Decide entitlement sync strategy**:
   - Option A: Add `/api/internal/entitlement/sync` with v1.1 schema
   - Option B: Platform adapts to use `/api/internal/subscription/sync`
   - Option C: Core accepts both schemas at existing endpoint

### MEDIUM Priority

3. **Clarify two entitlement systems**:
   - Document when to use `src/core/entitlements/` vs `core/entitlements/`
   - Consider merging if they serve same purpose
   - Update Platform integration docs

4. **Verify usage event flow**:
   - Confirm Platform receives `chat.usage_delta` via WebSocket
   - If HTTP endpoint needed, add `/api/usage/ingest`

### LOW Priority

5. **Documentation sync**:
   - Update `CORE_CLI_PHASE2_COMPLETE.md` with test status
   - Archive stale coordination docs

---

## File Reference

### Core Files Audited:
- `runtime/ai/main.py` - Runtime version header
- `runtime/ai/core/director.py` - Context injection, plugin execution
- `runtime/ai/core/routes/subscription_sync.py` - Sync endpoint
- `runtime/ai/core/entitlements/` - Plugin-level entitlements
- `runtime/ai/src/core/entitlements/` - App-level entitlements
- `runtime/ai/core/ai_runtime/events/event_serialization.py` - Orchestration events
- `runtime/ai/tests/test_entitlements.py` - Broken tests

### Platform Files Referenced:
- `docs/contracts/RUNTIME_PLATFORM_CONTRACT_V1.md`
- `docs/contracts/ENTITLEMENT_CONTRACT_V1.1.md`
- `docs/architecture/WORKFLOW_LIFECYCLE_MARKERS.md`
- `docs/architecture/USAGE_EVENTS_CONTRACT.md`
- `platform_runtime/contracts.py`
