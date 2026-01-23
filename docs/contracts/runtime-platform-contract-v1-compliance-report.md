# Runtime ↔ Platform Contract v1.0.0 — Implementation Compliance Report

**Date**: 2026-01-23  
**Repository**: mozaiks-core  
**Contract Version**: 1.0.0  
**Status**: ✅ **IMPLEMENTED**

---

## Implementation Summary

All v1.0.0 contract requirements have been implemented:

| Requirement | Status | Implementation |
|-------------|--------|----------------|
| `X-Mozaiks-Runtime-Version: 1.0.0` header | ✅ Done | `main.py` middleware |
| `plugins_loaded` in `/health` | ✅ Done | `main.py` health endpoint |
| `user_jwt` in plugin context | ✅ Done | `director.py` inject_request_context |
| `GET /api/plugins` endpoint | ✅ Done | `director.py` alias route |
| `MOZAIKS_PLUGIN_TIMEOUT_SECONDS` | ✅ Done | `settings.py` with backward compat |

---

## 1. Changes Made

| Contract Requirement | Current Implementation | Conflict? |
|---------------------|------------------------|-----------|
| Plugin execute: `POST /api/execute/{plugin_name}` | ✅ Exists at `director.py:503` | **No conflict** |
| Plugin discovery: `GET /api/plugins` | ⚠️ Director has `GET /api/available-plugins`, plugin-host has `GET /api/plugins` | **Path mismatch in director** |
| Health: `GET /health` returns `plugins_loaded` | ❌ `main.py:68-84` returns `status`, `app_id`, `app_tier` only | **Missing field** |
| Version header: `X-Mozaiks-Runtime-Version: 1.0.x` | ❌ Not implemented anywhere | **Missing entirely** |
| `user_jwt` injection into plugin context | ❌ `director.py:133-148` injects context but NOT `user_jwt` | **Missing field** |
| Timeout: `MOZAIKS_PLUGIN_TIMEOUT_SECONDS` (default 30s) | ⚠️ `PLUGIN_EXEC_TIMEOUT_S` exists at `settings.py:359`, default 15s | **Name/default mismatch** |

---

## 2. Required Changes to Meet v1.0.0

### High Priority (Breaking Contract)

#### 2.1 Add `X-Mozaiks-Runtime-Version` header

Create middleware in `main.py` that adds this header to all responses:

```python
@app.middleware("http")
async def add_runtime_version_header(request: Request, call_next):
    response = await call_next(request)
    response.headers["X-Mozaiks-Runtime-Version"] = "1.0.0"
    return response
```

**File**: `runtime/ai/main.py`

#### 2.2 Inject `user_jwt` into plugin context

Modify `inject_request_context()` in `director.py:133` to include the original bearer token:

```python
def inject_request_context(user: dict, data: dict, raw_token: str = None) -> dict:
    # ... existing code ...
    if raw_token:
        data["user_jwt"] = raw_token
    return data
```

The execute endpoint must pass the token from the `Authorization` header.

**File**: `runtime/ai/core/director.py`

#### 2.3 Add `plugins_loaded` to `/health` response

Update `main.py:68-84`:

```python
return JSONResponse(
    status_code=200,
    content={
        "status": "healthy",
        "app_id": APP_ID,
        "app_tier": APP_TIER,
        "plugins_loaded": len(plugin_manager.plugins),  # ADD THIS
    }
)
```

**File**: `runtime/ai/main.py`

### Medium Priority (Alignment)

#### 2.4 Align plugin discovery endpoint

**Option A** (Recommended): Add alias route `GET /api/plugins` in `director.py` that mirrors `/api/available-plugins`

**Option B**: Document that `GET /api/available-plugins` is the canonical endpoint (requires contract amendment)

**File**: `runtime/ai/core/director.py`

#### 2.5 Rename timeout env var

Change `PLUGIN_EXEC_TIMEOUT_S` to `MOZAIKS_PLUGIN_TIMEOUT_SECONDS` and update default to 30s. Add backwards compatibility:

```python
plugin_exec_timeout_s=_env_float(
    "MOZAIKS_PLUGIN_TIMEOUT_SECONDS",
    default=_env_float("PLUGIN_EXEC_TIMEOUT_S", default=30.0)
),
```

**File**: `runtime/ai/core/config/settings.py`

### Low Priority (Forward Compatibility)

#### 2.6 Structure `_context` for future `runtime_user_id`

Current structure is already extensible. When adding `runtime_user_id` later, plugins that only use `user_id` won't break:

```python
data["_context"] = {
    "app_id": APP_ID,
    "user_id": user["user_id"],       # v1: from JWT sub
    # "runtime_user_id": "...",        # v2: add here later
    "username": user.get("username"),
    "roles": user.get("roles", []),
    "is_superadmin": user.get("is_superadmin", False),
}
```

**Status**: ✅ Already forward-compatible, no changes needed.

---

## 3. Migration Plan

| Phase | Action | Risk | Files Changed |
|-------|--------|------|---------------|
| **1** | Add `X-Mozaiks-Runtime-Version` middleware | Low | `main.py` |
| **2** | Add `plugins_loaded` to `/health` | Low | `main.py` |
| **3** | Add `GET /api/plugins` alias route | Low | `director.py` |
| **4** | Inject `user_jwt` into context | Medium | `director.py` (2 locations) |
| **5** | Rename/alias timeout env var | Low | `settings.py` |

**Recommended Execution Order**: 1 → 2 → 3 → 5 → 4

### Phase 4 Risk Assessment

Phase 4 (user_jwt injection) is medium risk because:

- Requires modifying function signature
- Must extract bearer token from Authorization header
- Should handle cases where token isn't present (local auth mode)

---

## 4. Summary

| Category | Count |
|----------|-------|
| ✅ Already compliant | 2 (plugin execute path, context structure) |
| ⚠️ Partial compliance | 2 (timeout exists but named differently, plugin discovery path differs) |
| ❌ Not implemented | 3 (version header, user_jwt injection, plugins_loaded in health) |

**Estimated effort**: 2-3 hours for all changes, including testing.

---

## 5. File Reference

| File | Line(s) | Description |
|------|---------|-------------|
| `runtime/ai/main.py` | 68-84 | Health endpoint |
| `runtime/ai/core/director.py` | 133-148 | `inject_request_context()` |
| `runtime/ai/core/director.py` | 440 | `/api/available-plugins` endpoint |
| `runtime/ai/core/director.py` | 503 | `/api/execute/{plugin_name}` endpoint |
| `runtime/ai/core/config/settings.py` | 359 | `PLUGIN_EXEC_TIMEOUT_S` setting |
| `runtime/plugin-host/main.py` | 48-54 | Health with `plugins_loaded`, `/api/plugins` |
