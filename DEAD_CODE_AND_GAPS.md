# mozaiks-core: Dead Code & Gaps Report (Updated)

**Updated:** 2026-01-30  
**Scope:** Current repository layout (packages/python/*, packages/dotnet/*)

---

## ✅ Closed / Integrated

### 1) Token Budget Enforcement (App-Level)
**Status:** Implemented  
**Changes:**
- Token usage tracking added (Mongo-backed, in-memory fallback).
- Runtime now blocks workflows when HARD token budgets are exceeded.
- Usage events update local counters and optionally report upstream.

**Code:**
- `packages/python/platform/mozaiks_platform/billing/token_usage.py`
- `packages/python/platform/mozaiks_platform/billing/token_budget.py`
- `packages/python/ai-runtime/mozaiks_ai/runtime/events/usage_accounting.py`
- `packages/python/ai-runtime/mozaiks_ai/runtime/transport/simple_transport.py`

---

### 2) Subscription Manager AI Workflow
**Status:** Implemented  
**Notes:** A built-in workflow is now shipped with the runtime (no external app required).

**Code:**
- `packages/python/ai-runtime/workflows/subscription_manager/`

This workflow exposes tools to:
- Read current subscription (`get_subscription`)
- List plans (`list_plans`)
- Check plugin access (`check_plugin_access`)

---

### 3) Dead/Disconnected Billing Usage Reporter
**Status:** Integrated  
**Notes:** Usage events now flow to the reporter (if configured), removing the dead-code path.

---

## 🟡 Still Out of Core Scope (By Design)

### AI-Driven Subscription Logic
The AI-driven pricing and optimization layer is platform territory. Core remains the execution substrate and does not implement pricing intelligence.

---

## Notes on Platform Integration

- PlatformPaymentProvider exists in both Python and .NET and remains optional.
- Core remains push-only for entitlements (Platform → Core sync endpoint).
- Usage reporting is optional and only activates when platform credentials are configured.

