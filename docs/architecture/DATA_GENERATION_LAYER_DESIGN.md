# Data Generation Layer Design for MozaiksAI Validation Loop

> **Version**: 1.0  
> **Status**: Proposed  
> **Target**: mozaiks-core runtime layer  
> **Purpose**: Enable agentic validation loops driven by MozaiksAI and the Control Plane

---

## Executive Summary

This document defines a comprehensive data generation layer for mozaiks-core that supports:
- **Attribution capture** for understanding user acquisition channels
- **Event telemetry** for behavior analysis and experiment validation
- **Experiment/feature flag integration** for A/B testing
- **Error surfacing** for debugging and reliability monitoring

The design maintains mozaiks-core's boundary contract: the runtime captures and stores data, while the Control Plane consumes and analyzes it.

---

# PART 1: BACKEND DATA GENERATION LAYER

---

## 1. Attribution Capture in Core

### 1.1 Where to Capture First-Touch Attribution

Attribution should be captured at **three strategic points**:

| Location | Purpose | Implementation File |
|----------|---------|---------------------|
| **Registration endpoint** | Capture UTM/experiment data at signup | [security/auth.py](../../backend/security/auth.py) |
| **Token validation middleware** | Persist attribution across sessions | [security/authentication.py](../../backend/security/authentication.py) |
| **First authenticated request** | Fallback capture for external auth | [core/director.py](../../backend/core/director.py) |

### 1.2 User Document Schema (MongoDB)

Extend the `users` collection with attribution fields:

```python
# User document schema with attribution
{
    "_id": ObjectId,
    "username": str,
    "email": str,
    "full_name": str,
    "hashed_password": str,  # Only for local auth
    "disabled": bool,
    
    # Timestamps
    "created_at": datetime,
    "updated_at": datetime,
    "last_login": datetime,
    "last_active": datetime,
    
    # === NEW: Attribution Fields ===
    "attribution": {
        "first_touch": {
            "utm_source": str | None,       # e.g., "google", "facebook", "twitter"
            "utm_medium": str | None,       # e.g., "cpc", "organic", "email"
            "utm_campaign": str | None,     # Campaign identifier
            "utm_content": str | None,      # Ad/content variant
            "utm_term": str | None,         # Search term (if applicable)
            "referrer": str | None,         # HTTP referrer URL
            "landing_page": str | None,     # First page visited
            "experiment_id": str | None,    # MozaiksAI experiment ID
            "variant": str | None,          # Experiment variant assigned
            "captured_at": datetime,        # When attribution was captured
        },
        "last_touch": {
            # Same structure as first_touch, updated on each session
        },
        "registration_context": {
            "app_id": str,                  # MOZAIKS_APP_ID
            "release_id": str | None,       # MozaiksAI release version
            "user_agent": str | None,       # Browser/client info
            "ip_country": str | None,       # Geo-location (if enabled)
            "registration_method": str,     # "local", "oidc", "platform"
        }
    },
    
    # === NEW: Experiment Participation ===
    "experiments": {
        "{experiment_id}": {
            "variant": str,
            "enrolled_at": datetime,
            "first_exposure_at": datetime | None,
            "converted": bool,
            "converted_at": datetime | None,
        }
    },
    
    # === NEW: Cohort Tags ===
    "cohorts": [str],  # e.g., ["ad_acquired", "premium_trial", "q1_2026_signup"]
}
```

### 1.3 Persistence Strategy

**Session Persistence Flow:**

```
┌─────────────────────────────────────────────────────────────────────┐
│                        ATTRIBUTION FLOW                              │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  Browser Landing ──► Store UTM in localStorage ──► Send to Backend  │
│                                                                      │
│  ┌──────────────────┐    ┌──────────────────┐    ┌───────────────┐ │
│  │ First Page Load  │───►│ /api/auth/       │───►│ users         │ │
│  │ (capture UTMs)   │    │ register|login   │    │ collection    │ │
│  └──────────────────┘    └──────────────────┘    └───────────────┘ │
│                                                                      │
│  Anonymous ──────────────► JWT Token ──────────────► User Document  │
│  (localStorage)            (contains user_id)        (MongoDB)      │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

**Key Persistence Rules:**

1. **First-touch is immutable** - Once set, never overwrite
2. **Last-touch is always updated** - On each new session with attribution
3. **Anonymous attribution** - Store in localStorage until user authenticates
4. **Cross-device handling** - First device to register wins first-touch

---

## 2. Event Telemetry Model

### 2.1 MongoDB Schema: `telemetry_events` Collection

```python
# Collection: telemetry_events
# Indexes defined below

{
    "_id": ObjectId,
    
    # === Core Identity ===
    "app_id": str,              # MOZAIKS_APP_ID (required)
    "user_id": str | None,      # Null for anonymous events
    "session_id": str | None,   # Client-generated session UUID
    
    # === Event Classification ===
    "event_type": str,          # Hierarchical: "plugin.executed", "auth.login", etc.
    "event_category": str,      # "engagement", "error", "conversion", "system"
    
    # === Temporal ===
    "timestamp": datetime,      # Server-received time (UTC)
    "client_timestamp": datetime | None,  # Client-reported time
    "day": str,                 # "2026-01-17" (for daily aggregation)
    
    # === Attribution Context ===
    "attribution": {
        "utm_source": str | None,
        "utm_medium": str | None,
        "utm_campaign": str | None,
        "experiment_id": str | None,
        "variant": str | None,
    },
    
    # === Release/Experiment Context ===
    "context": {
        "release_id": str | None,       # MozaiksAI release version
        "experiment_id": str | None,    # Active experiment
        "variant": str | None,          # Assigned variant
        "feature_flags": dict | None,   # Active flags at event time
    },
    
    # === Event-Specific Data ===
    "data": {
        # Varies by event_type - examples below
    },
    
    # === Error Context (for error events) ===
    "error": {
        "message": str | None,
        "stack": str | None,
        "code": str | None,
        "component": str | None,
    } | None,
    
    # === Device/Client Context ===
    "client": {
        "user_agent": str | None,
        "platform": str | None,     # "web", "mobile", "api"
        "version": str | None,      # Frontend version
    },
    
    # === Processing Metadata ===
    "processed": bool,              # For streaming/ETL markers
    "exported_at": datetime | None, # When sent to Control Plane
}
```

### 2.2 Event Type Taxonomy

```python
EVENT_TYPES = {
    # Authentication Events
    "auth.login": {"category": "engagement"},
    "auth.logout": {"category": "engagement"},
    "auth.register": {"category": "conversion"},
    "auth.token_refresh": {"category": "system"},
    "auth.failed": {"category": "error"},
    
    # Plugin Events
    "plugin.executed": {"category": "engagement"},
    "plugin.error": {"category": "error"},
    "plugin.access_denied": {"category": "engagement"},
    
    # Navigation Events
    "navigation.page_view": {"category": "engagement"},
    "navigation.click": {"category": "engagement"},
    "navigation.rendered": {"category": "system"},
    
    # Subscription Events
    "subscription.trial_started": {"category": "conversion"},
    "subscription.upgraded": {"category": "conversion"},
    "subscription.downgraded": {"category": "conversion"},
    "subscription.canceled": {"category": "conversion"},
    "subscription.renewed": {"category": "conversion"},
    
    # Settings Events
    "settings.viewed": {"category": "engagement"},
    "settings.changed": {"category": "engagement"},
    "settings.plugin_settings_changed": {"category": "engagement"},
    
    # AI Capability Events
    "ai.capability_viewed": {"category": "engagement"},
    "ai.capability_launched": {"category": "engagement"},
    "ai.workflow_completed": {"category": "conversion"},
    "ai.workflow_error": {"category": "error"},
    
    # Error Events
    "error.frontend": {"category": "error"},
    "error.backend": {"category": "error"},
    "error.plugin": {"category": "error"},
    
    # Experiment Events
    "experiment.enrolled": {"category": "system"},
    "experiment.exposed": {"category": "system"},
    "experiment.converted": {"category": "conversion"},
}
```

### 2.3 Indexes for `telemetry_events`

```python
# backend/core/analytics/telemetry_indexes.py

TELEMETRY_INDEXES = [
    # Primary query patterns
    {"keys": [("app_id", 1), ("event_type", 1), ("timestamp", -1)]},
    {"keys": [("app_id", 1), ("user_id", 1), ("timestamp", -1)]},
    {"keys": [("app_id", 1), ("day", 1), ("event_type", 1)]},
    
    # Attribution queries
    {"keys": [("app_id", 1), ("attribution.experiment_id", 1), ("event_type", 1)]},
    {"keys": [("app_id", 1), ("attribution.utm_source", 1), ("day", 1)]},
    
    # Experiment queries
    {"keys": [("app_id", 1), ("context.experiment_id", 1), ("context.variant", 1), ("event_type", 1)]},
    
    # Export/streaming cursor
    {"keys": [("app_id", 1), ("processed", 1), ("_id", 1)]},
    
    # Error aggregation
    {"keys": [("app_id", 1), ("event_category", 1), ("timestamp", -1)], 
     "partialFilterExpression": {"event_category": "error"}},
    
    # TTL index (optional - auto-delete after 90 days)
    {"keys": [("timestamp", 1)], "expireAfterSeconds": 7776000},
]
```

---

## 3. Instrumentation Points

### 3.1 Backend Instrumentation Map

| System | Event Type | File Location | Insertion Point |
|--------|------------|---------------|-----------------|
| **Authentication** | `auth.login` | [security/auth.py#L108-L130](../../backend/security/auth.py) | After successful `login_for_access_token` |
| **Authentication** | `auth.register` | [security/auth.py#L132-L166](../../backend/security/auth.py) | After successful `register_user` |
| **Authentication** | `auth.failed` | [security/auth.py#L108-L115](../../backend/security/auth.py) | In exception handler |
| **Plugin Execution** | `plugin.executed` | [core/director.py#L555-L565](../../backend/core/director.py) | After successful plugin execution |
| **Plugin Execution** | `plugin.error` | [core/director.py#L567-L575](../../backend/core/director.py) | In exception handler |
| **Plugin Access** | `plugin.access_denied` | [core/director.py#L544-L546](../../backend/core/director.py) | When access check fails |
| **Navigation** | `navigation.rendered` | [core/director.py#L259-L329](../../backend/core/director.py) | After navigation generation |
| **Subscription** | `subscription.*` | [core/director.py#L640-L700](../../backend/core/director.py) | After subscription mutations |
| **Settings** | `settings.changed` | [core/director.py#L418-L430](../../backend/core/director.py) | After profile update |
| **AI Capabilities** | `ai.capability_launched` | [core/routes/ai.py](../../backend/core/routes/ai.py) | After launch endpoint |

### 3.2 Telemetry Service Implementation

```python
# backend/core/analytics/telemetry_service.py

import logging
import os
from datetime import datetime
from typing import Any, Dict, Optional

from core.config.database import db
from core.event_bus import event_bus

logger = logging.getLogger("mozaiks_core.analytics.telemetry")

telemetry_events = db["telemetry_events"]


class TelemetryService:
    """
    Central service for capturing telemetry events.
    
    DESIGN PRINCIPLES:
    - Non-blocking: Never fail the parent operation
    - Enriched: Automatically add app/release context
    - Idempotent: Safe to call multiple times
    """
    
    def __init__(self):
        self.app_id = os.getenv("MOZAIKS_APP_ID", "unknown-app")
        self.release_id = os.getenv("MOZAIKS_RELEASE_ID")
        
    async def log_event(
        self,
        event_type: str,
        *,
        user_id: Optional[str] = None,
        session_id: Optional[str] = None,
        data: Optional[Dict[str, Any]] = None,
        attribution: Optional[Dict[str, Any]] = None,
        experiment_context: Optional[Dict[str, Any]] = None,
        error: Optional[Dict[str, Any]] = None,
        client: Optional[Dict[str, Any]] = None,
        client_timestamp: Optional[datetime] = None,
    ) -> Optional[str]:
        """
        Log a telemetry event to MongoDB.
        
        Returns the inserted event ID, or None if logging failed.
        """
        try:
            now = datetime.utcnow()
            
            event = {
                "app_id": self.app_id,
                "user_id": user_id,
                "session_id": session_id,
                "event_type": event_type,
                "event_category": self._get_category(event_type),
                "timestamp": now,
                "client_timestamp": client_timestamp,
                "day": now.date().isoformat(),
                "attribution": attribution or {},
                "context": {
                    "release_id": self.release_id,
                    **(experiment_context or {}),
                },
                "data": data or {},
                "error": error,
                "client": client or {},
                "processed": False,
                "exported_at": None,
            }
            
            result = await telemetry_events.insert_one(event)
            
            # Also publish to event bus for real-time subscribers
            event_bus.publish(f"telemetry:{event_type}", {
                "event_id": str(result.inserted_id),
                "user_id": user_id,
                "data": data,
            })
            
            return str(result.inserted_id)
            
        except Exception as e:
            logger.error(f"Failed to log telemetry event {event_type}: {e}")
            return None
    
    def _get_category(self, event_type: str) -> str:
        """Map event type to category."""
        EVENT_CATEGORIES = {
            "auth.": "engagement",
            "plugin.executed": "engagement",
            "plugin.error": "error",
            "navigation.": "engagement",
            "subscription.": "conversion",
            "settings.": "engagement",
            "ai.": "engagement",
            "error.": "error",
            "experiment.": "system",
        }
        
        for prefix, category in EVENT_CATEGORIES.items():
            if event_type.startswith(prefix):
                return category
        return "system"
    
    async def log_error(
        self,
        component: str,
        error_message: str,
        *,
        user_id: Optional[str] = None,
        stack: Optional[str] = None,
        error_code: Optional[str] = None,
        context: Optional[Dict[str, Any]] = None,
    ) -> Optional[str]:
        """
        Convenience method for logging errors.
        """
        return await self.log_event(
            f"error.{component}",
            user_id=user_id,
            error={
                "message": error_message,
                "stack": stack,
                "code": error_code,
                "component": component,
            },
            data=context,
        )


# Singleton instance
telemetry_service = TelemetryService()
```

### 3.3 Example Instrumentation (Plugin Execution)

```python
# Modification to backend/core/director.py

from core.analytics.telemetry_service import telemetry_service

@app.post("/api/execute/{plugin_name}")
async def execute_plugin(plugin_name: str, request: Request, user: dict = Depends(get_current_user)):
    # ... existing validation code ...
    
    execution_start = time.time()
    
    try:
        result = await plugin_manager.execute_plugin(plugin_name, data)
        execution_time = time.time() - execution_start
        
        # === NEW: Telemetry logging ===
        await telemetry_service.log_event(
            "plugin.executed",
            user_id=user["user_id"],
            data={
                "plugin_name": plugin_name,
                "action": data.get("action"),
                "execution_time_ms": int(execution_time * 1000),
            },
            attribution=data.get("_attribution"),  # Passed from frontend
            experiment_context=data.get("_experiment_context"),
        )
        
        # ... rest of existing code ...
        
    except Exception as e:
        execution_time = time.time() - execution_start
        
        # === NEW: Error telemetry ===
        await telemetry_service.log_error(
            "plugin",
            str(e),
            user_id=user["user_id"],
            stack=traceback.format_exc(),
            context={
                "plugin_name": plugin_name,
                "action": data.get("action"),
                "execution_time_ms": int(execution_time * 1000),
            },
        )
        
        raise HTTPException(status_code=500, detail=f"Error executing plugin: {str(e)}")
```

---

## 4. Data Flow to Control Plane

### 4.1 Recommended Approach: Hybrid (API + Streaming)

```
┌─────────────────────────────────────────────────────────────────────┐
│                    DATA FLOW TO CONTROL PLANE                        │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌──────────────┐                      ┌───────────────────────┐    │
│  │ mozaiks-core │                      │   Control Plane       │    │
│  │   Runtime    │                      │   (MozaiksAI)         │    │
│  └──────┬───────┘                      └───────────┬───────────┘    │
│         │                                          │                 │
│         │                                          │                 │
│  ┌──────▼───────┐     PULL (API)      ┌───────────▼───────────┐    │
│  │ telemetry_   │◄────────────────────│  GET /__mozaiks/      │    │
│  │ events       │     Daily KPIs      │  admin/analytics/*    │    │
│  │ collection   │                     │                       │    │
│  └──────┬───────┘                     └───────────────────────┘    │
│         │                                                           │
│         │  PUSH (Webhook/Stream)                                    │
│         │  Real-time critical events                                │
│         ▼                                                           │
│  ┌──────────────┐     POST            ┌───────────────────────┐    │
│  │ Event Bus    │────────────────────►│  Control Plane        │    │
│  │ (optional)   │   Subscription/     │  Webhook Endpoint     │    │
│  │              │   Error events      │                       │    │
│  └──────────────┘                     └───────────────────────┘    │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

### 4.2 API Endpoints for Control Plane

Extend existing admin routes with telemetry endpoints:

```python
# backend/core/routes/telemetry_admin.py

from fastapi import APIRouter, Depends, Query, HTTPException
from datetime import datetime, date, timedelta
from typing import Optional, List

from security.authentication import require_admin_or_internal
from core.config.database import db

router = APIRouter()
telemetry_events = db["telemetry_events"]


@router.get("/events/stream")
async def stream_events(
    since_id: Optional[str] = Query(None, description="Resume from this event ID"),
    event_types: Optional[List[str]] = Query(None, description="Filter by event types"),
    limit: int = Query(1000, le=10000),
    current_user: dict = Depends(require_admin_or_internal),
):
    """
    Stream events for Control Plane ingestion.
    
    Uses cursor-based pagination for efficient streaming.
    """
    from bson import ObjectId
    
    query = {"app_id": os.getenv("MOZAIKS_APP_ID")}
    
    if since_id:
        query["_id"] = {"$gt": ObjectId(since_id)}
    
    if event_types:
        query["event_type"] = {"$in": event_types}
    
    cursor = telemetry_events.find(query).sort("_id", 1).limit(limit)
    
    events = []
    last_id = None
    async for event in cursor:
        last_id = str(event["_id"])
        event["_id"] = last_id
        events.append(event)
    
    return {
        "events": events,
        "count": len(events),
        "last_id": last_id,
        "has_more": len(events) == limit,
    }


@router.get("/events/aggregate")
async def aggregate_events(
    event_type: str,
    from_date: str = Query(..., alias="from"),
    to_date: str = Query(..., alias="to"),
    group_by: str = Query("day", regex="^(day|hour|attribution.utm_source|context.variant)$"),
    current_user: dict = Depends(require_admin_or_internal),
):
    """
    Aggregate events for dashboards.
    """
    start = datetime.fromisoformat(from_date)
    end = datetime.fromisoformat(to_date)
    
    pipeline = [
        {
            "$match": {
                "app_id": os.getenv("MOZAIKS_APP_ID"),
                "event_type": event_type,
                "timestamp": {"$gte": start, "$lte": end},
            }
        },
        {
            "$group": {
                "_id": f"${group_by}",
                "count": {"$sum": 1},
                "unique_users": {"$addToSet": "$user_id"},
            }
        },
        {
            "$project": {
                "group": "$_id",
                "count": 1,
                "unique_users": {"$size": "$unique_users"},
            }
        },
        {"$sort": {"group": 1}},
    ]
    
    results = await telemetry_events.aggregate(pipeline).to_list(1000)
    
    return {
        "event_type": event_type,
        "from": from_date,
        "to": to_date,
        "group_by": group_by,
        "results": results,
    }


@router.get("/experiments/{experiment_id}/metrics")
async def get_experiment_metrics(
    experiment_id: str,
    current_user: dict = Depends(require_admin_or_internal),
):
    """
    Get experiment-specific metrics for A/B test analysis.
    """
    pipeline = [
        {
            "$match": {
                "app_id": os.getenv("MOZAIKS_APP_ID"),
                "context.experiment_id": experiment_id,
            }
        },
        {
            "$group": {
                "_id": {
                    "variant": "$context.variant",
                    "event_type": "$event_type",
                },
                "count": {"$sum": 1},
                "unique_users": {"$addToSet": "$user_id"},
            }
        },
        {
            "$group": {
                "_id": "$_id.variant",
                "events": {
                    "$push": {
                        "event_type": "$_id.event_type",
                        "count": "$count",
                        "unique_users": {"$size": "$unique_users"},
                    }
                },
                "total_events": {"$sum": "$count"},
            }
        },
    ]
    
    results = await telemetry_events.aggregate(pipeline).to_list(100)
    
    return {
        "experiment_id": experiment_id,
        "variants": results,
    }
```

### 4.3 Optional: Real-time Event Webhook

For critical events (errors, conversions), optionally push to Control Plane:

```python
# backend/core/analytics/webhook_publisher.py

import httpx
import os
import logging

logger = logging.getLogger("mozaiks_core.analytics.webhook")

CONTROL_PLANE_WEBHOOK_URL = os.getenv("MOZAIKS_CONTROL_PLANE_WEBHOOK_URL")
CONTROL_PLANE_API_KEY = os.getenv("MOZAIKS_CONTROL_PLANE_API_KEY")

CRITICAL_EVENT_TYPES = {
    "auth.register",
    "subscription.upgraded",
    "subscription.canceled",
    "error.backend",
    "error.plugin",
}


async def maybe_push_to_control_plane(event_type: str, event_data: dict) -> None:
    """
    Optionally push critical events to Control Plane in real-time.
    """
    if not CONTROL_PLANE_WEBHOOK_URL:
        return  # Webhook not configured
    
    if event_type not in CRITICAL_EVENT_TYPES:
        return  # Not a critical event
    
    try:
        async with httpx.AsyncClient() as client:
            await client.post(
                CONTROL_PLANE_WEBHOOK_URL,
                json={
                    "event_type": event_type,
                    "app_id": os.getenv("MOZAIKS_APP_ID"),
                    "data": event_data,
                },
                headers={
                    "X-API-Key": CONTROL_PLANE_API_KEY,
                    "Content-Type": "application/json",
                },
                timeout=5.0,
            )
    except Exception as e:
        logger.warning(f"Failed to push event to Control Plane: {e}")
        # Non-blocking - don't fail parent operation
```

---

## 5. Minimum Viable Implementation

### 5.1 Phase 1: Core Telemetry (Week 1)

**Files to Create:**

```
backend/core/analytics/
├── __init__.py              # Export telemetry_service
├── telemetry_service.py     # Main service (from section 3.2)
├── telemetry_indexes.py     # Index definitions
└── telemetry_admin.py       # Admin API routes
```

**Files to Modify:**

| File | Change |
|------|--------|
| [backend/main.py](../../backend/main.py) | Add telemetry index initialization |
| [backend/core/director.py](../../backend/core/director.py) | Import and use `telemetry_service` |
| [backend/security/auth.py](../../backend/security/auth.py) | Log auth events |

**Minimal Implementation:**

```python
# backend/core/analytics/__init__.py

from .telemetry_service import telemetry_service
from .raw_events import (
    append_user_signed_up,
    append_user_active,
    init_raw_event_indexes,
)

__all__ = [
    "telemetry_service",
    "append_user_signed_up",
    "append_user_active",
    "init_raw_event_indexes",
]
```

### 5.2 Phase 2: Attribution (Week 2)

**Changes:**

1. Add `attribution` field to user documents
2. Modify registration to capture UTM params
3. Add middleware to persist attribution from headers

### 5.3 Phase 3: Control Plane API (Week 3)

**Changes:**

1. Add telemetry admin routes
2. Integrate with existing `/__mozaiks/admin/` prefix
3. Document API for Control Plane consumption

### 5.4 Backward Compatibility

**Existing Systems Preserved:**

- `user_events` collection (for DAU/MAU) - unchanged
- `app_kpi_snapshots` collection - unchanged
- Event bus `publish()` calls - preserved, telemetry is additive
- All existing routes - no breaking changes

**New Addition:**

- `telemetry_events` collection - net new, opt-in
- Admin API routes - behind `require_admin_or_internal`

---

# PART 2: FRONTEND DATA GENERATION LAYER

---

## 1. Instrumentation Layer (Telemetry in the UI)

### 1.1 Where to Capture Analytics

| Location | Events | File |
|----------|--------|------|
| **App.jsx** | Page views, navigation | [src/App.jsx](../../src/App.jsx) |
| **AuthContext.jsx** | Login, logout, register | [src/auth/AuthContext.jsx](../../src/auth/AuthContext.jsx) |
| **DynamicUIComponent.jsx** | Plugin entry/exit, errors | [src/core/plugins/DynamicUIComponent.jsx](../../src/core/plugins/DynamicUIComponent.jsx) |
| **Global error boundary** | Rendering failures | New component |
| **Settings pages** | Settings changes | [src/profile/ProfilePage.jsx](../../src/profile/ProfilePage.jsx) |
| **Subscription page** | Subscription interactions | [src/subscription/SubscriptionPage.jsx](../../src/subscription/SubscriptionPage.jsx) |

### 1.2 Event Schema (Client-Side)

```typescript
// src/core/analytics/types.ts

interface TelemetryEvent {
  // Core identity
  event_type: string;
  timestamp: string;  // ISO 8601
  session_id: string;
  
  // Attribution (from localStorage)
  attribution?: {
    utm_source?: string;
    utm_medium?: string;
    utm_campaign?: string;
    experiment_id?: string;
    variant?: string;
  };
  
  // Context
  context?: {
    release_id?: string;
    experiment_id?: string;
    variant?: string;
    feature_flags?: Record<string, boolean>;
  };
  
  // Event-specific data
  data?: Record<string, any>;
  
  // Error context
  error?: {
    message: string;
    stack?: string;
    component?: string;
  };
  
  // Client info
  client?: {
    url: string;
    referrer?: string;
    viewport?: { width: number; height: number };
  };
}
```

### 1.3 Telemetry Service Implementation

```jsx
// src/core/analytics/TelemetryService.js

const SESSION_KEY = 'mozaiks_session_id';
const ATTRIBUTION_KEY = 'mozaiks_attribution';
const QUEUE_KEY = 'mozaiks_telemetry_queue';

class TelemetryService {
  constructor() {
    this.sessionId = this.getOrCreateSessionId();
    this.queue = [];
    this.flushInterval = null;
    this.attribution = this.loadAttribution();
    this.experimentContext = null;
  }

  // === Session Management ===
  
  getOrCreateSessionId() {
    let sessionId = sessionStorage.getItem(SESSION_KEY);
    if (!sessionId) {
      sessionId = crypto.randomUUID();
      sessionStorage.setItem(SESSION_KEY, sessionId);
    }
    return sessionId;
  }

  // === Attribution ===
  
  loadAttribution() {
    try {
      return JSON.parse(localStorage.getItem(ATTRIBUTION_KEY)) || {};
    } catch {
      return {};
    }
  }

  captureAttribution() {
    const params = new URLSearchParams(window.location.search);
    const attribution = {
      utm_source: params.get('utm_source'),
      utm_medium: params.get('utm_medium'),
      utm_campaign: params.get('utm_campaign'),
      utm_content: params.get('utm_content'),
      utm_term: params.get('utm_term'),
      referrer: document.referrer || null,
      landing_page: window.location.pathname,
      experiment_id: params.get('exp_id') || params.get('experiment_id'),
      variant: params.get('variant'),
      captured_at: new Date().toISOString(),
    };

    // Only save if we have meaningful attribution data
    const hasAttribution = attribution.utm_source || attribution.experiment_id || attribution.referrer;
    
    if (hasAttribution) {
      const existing = this.loadAttribution();
      
      // First-touch: only set if not already present
      if (!existing.first_touch) {
        existing.first_touch = attribution;
      }
      
      // Last-touch: always update
      existing.last_touch = attribution;
      
      localStorage.setItem(ATTRIBUTION_KEY, JSON.stringify(existing));
      this.attribution = existing;
    }

    return this.attribution;
  }

  // === Experiment Context ===
  
  setExperimentContext(context) {
    this.experimentContext = context;
  }

  // === Event Logging ===
  
  track(eventType, data = {}) {
    const event = {
      event_type: eventType,
      timestamp: new Date().toISOString(),
      session_id: this.sessionId,
      attribution: this.attribution.first_touch || {},
      context: {
        ...this.experimentContext,
      },
      data,
      client: {
        url: window.location.href,
        referrer: document.referrer,
        viewport: {
          width: window.innerWidth,
          height: window.innerHeight,
        },
      },
    };

    this.queue.push(event);
    this.maybeFlush();
  }

  trackError(error, component = 'unknown') {
    this.track('error.frontend', {
      error: {
        message: error.message,
        stack: error.stack,
        component,
      },
    });
  }

  trackPageView(path, title) {
    this.track('navigation.page_view', {
      path,
      title,
    });
  }

  trackPluginEntry(pluginName) {
    this.track('plugin.entered', {
      plugin_name: pluginName,
    });
  }

  trackPluginExit(pluginName, durationMs) {
    this.track('plugin.exited', {
      plugin_name: pluginName,
      duration_ms: durationMs,
    });
  }

  // === Queue Management ===
  
  maybeFlush() {
    if (this.queue.length >= 10) {
      this.flush();
    } else if (!this.flushInterval) {
      this.flushInterval = setTimeout(() => this.flush(), 5000);
    }
  }

  async flush() {
    if (this.flushInterval) {
      clearTimeout(this.flushInterval);
      this.flushInterval = null;
    }

    if (this.queue.length === 0) return;

    const events = [...this.queue];
    this.queue = [];

    try {
      const token = await this.getAuthToken();
      
      await fetch('/api/telemetry/batch', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          ...(token ? { 'Authorization': `Bearer ${token}` } : {}),
        },
        body: JSON.stringify({ events }),
      });
    } catch (error) {
      // Re-queue events on failure
      this.queue = [...events, ...this.queue];
      
      // Persist to localStorage as backup
      try {
        const existing = JSON.parse(localStorage.getItem(QUEUE_KEY) || '[]');
        localStorage.setItem(QUEUE_KEY, JSON.stringify([...existing, ...events].slice(-100)));
      } catch {
        // Ignore localStorage errors
      }
    }
  }

  async getAuthToken() {
    // Will be set by AuthContext
    return this._authToken;
  }

  setAuthToken(token) {
    this._authToken = token;
  }

  // === Lifecycle ===
  
  initialize() {
    // Capture attribution on first load
    this.captureAttribution();

    // Flush on page unload
    window.addEventListener('beforeunload', () => this.flush());

    // Retry any queued events from localStorage
    try {
      const stored = JSON.parse(localStorage.getItem(QUEUE_KEY) || '[]');
      if (stored.length > 0) {
        this.queue = [...stored, ...this.queue];
        localStorage.removeItem(QUEUE_KEY);
        this.maybeFlush();
      }
    } catch {
      // Ignore
    }
  }
}

export const telemetryService = new TelemetryService();
export default telemetryService;
```

### 1.4 React Integration Hook

```jsx
// src/core/analytics/useTelemetry.js

import { useEffect, useRef } from 'react';
import { useLocation } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import telemetryService from './TelemetryService';

export function useTelemetry() {
  const location = useLocation();
  const { getAccessToken } = useAuth();
  const initialized = useRef(false);

  // Initialize on mount
  useEffect(() => {
    if (!initialized.current) {
      telemetryService.initialize();
      initialized.current = true;
    }
  }, []);

  // Update auth token
  useEffect(() => {
    const updateToken = async () => {
      const token = await getAccessToken();
      telemetryService.setAuthToken(token);
    };
    updateToken();
  }, [getAccessToken]);

  // Track page views
  useEffect(() => {
    telemetryService.trackPageView(location.pathname, document.title);
  }, [location.pathname]);

  return telemetryService;
}

export function usePluginTelemetry(pluginName) {
  const entryTime = useRef(null);

  useEffect(() => {
    entryTime.current = Date.now();
    telemetryService.trackPluginEntry(pluginName);

    return () => {
      const duration = Date.now() - entryTime.current;
      telemetryService.trackPluginExit(pluginName, duration);
    };
  }, [pluginName]);
}
```

---

## 2. Experiment & Feature Flag System

### 2.1 Feature Flag Schema

```json
// Schema for feature flags (returned by backend)
{
  "flags": {
    "new_onboarding_flow": {
      "enabled": true,
      "variant": "treatment_b",
      "experiment_id": "exp_onboarding_2026q1",
      "payload": {
        "steps": ["welcome", "profile", "plugins"],
        "skip_button": true
      }
    },
    "simplified_navigation": {
      "enabled": false,
      "variant": null,
      "experiment_id": null,
      "payload": null
    },
    "premium_banner": {
      "enabled": true,
      "variant": "control",
      "experiment_id": "exp_premium_upsell",
      "payload": {
        "message": "Upgrade to Premium for unlimited access",
        "cta": "Upgrade Now"
      }
    }
  },
  "user_cohorts": ["organic", "registered_2026q1"],
  "evaluated_at": "2026-01-17T10:30:00Z"
}
```

### 2.2 Feature Flag Provider

```jsx
// src/core/experiments/FeatureFlagProvider.jsx

import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../auth/AuthContext';
import telemetryService from '../analytics/TelemetryService';

const FeatureFlagContext = createContext(null);

export function FeatureFlagProvider({ children }) {
  const { isAuthenticated, authFetch } = useAuth();
  const [flags, setFlags] = useState({});
  const [cohorts, setCohorts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const fetchFlags = useCallback(async () => {
    try {
      // Include attribution for experiment bucketing
      const attribution = telemetryService.attribution?.first_touch || {};
      
      const response = await authFetch('/api/feature-flags', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ attribution }),
      });

      if (!response.ok) throw new Error('Failed to fetch feature flags');

      const data = await response.json();
      setFlags(data.flags || {});
      setCohorts(data.user_cohorts || []);
      
      // Update telemetry context with experiment info
      const experimentContext = {};
      for (const [key, value] of Object.entries(data.flags || {})) {
        if (value.experiment_id) {
          experimentContext.experiment_id = value.experiment_id;
          experimentContext.variant = value.variant;
          break; // Use first active experiment
        }
      }
      telemetryService.setExperimentContext(experimentContext);
      
    } catch (err) {
      setError(err.message);
      console.error('Feature flag fetch error:', err);
    } finally {
      setLoading(false);
    }
  }, [authFetch]);

  useEffect(() => {
    if (isAuthenticated) {
      fetchFlags();
    } else {
      setLoading(false);
    }
  }, [isAuthenticated, fetchFlags]);

  const isEnabled = useCallback((flagName) => {
    return flags[flagName]?.enabled || false;
  }, [flags]);

  const getVariant = useCallback((flagName) => {
    return flags[flagName]?.variant || null;
  }, [flags]);

  const getPayload = useCallback((flagName) => {
    return flags[flagName]?.payload || null;
  }, [flags]);

  const isInCohort = useCallback((cohortName) => {
    return cohorts.includes(cohortName);
  }, [cohorts]);

  const value = {
    flags,
    cohorts,
    loading,
    error,
    isEnabled,
    getVariant,
    getPayload,
    isInCohort,
    refresh: fetchFlags,
  };

  return (
    <FeatureFlagContext.Provider value={value}>
      {children}
    </FeatureFlagContext.Provider>
  );
}

export function useFeatureFlags() {
  const context = useContext(FeatureFlagContext);
  if (!context) {
    throw new Error('useFeatureFlags must be used within FeatureFlagProvider');
  }
  return context;
}

// Convenience hooks
export function useFeatureFlag(flagName) {
  const { isEnabled, getVariant, getPayload } = useFeatureFlags();
  return {
    enabled: isEnabled(flagName),
    variant: getVariant(flagName),
    payload: getPayload(flagName),
  };
}
```

### 2.3 Backend Feature Flag Endpoint

```python
# backend/core/routes/feature_flags.py

from fastapi import APIRouter, Depends
from typing import Dict, Any, Optional
from pydantic import BaseModel
import os

from security.authentication import get_current_user
from core.config.database import db

router = APIRouter()

# Feature flag configuration (can be moved to MongoDB for dynamic control)
DEFAULT_FLAGS = {
    "new_onboarding_flow": {"enabled": False, "experiment_id": None, "variant": None, "payload": None},
    "simplified_navigation": {"enabled": False, "experiment_id": None, "variant": None, "payload": None},
    "premium_banner": {"enabled": False, "experiment_id": None, "variant": None, "payload": None},
    "ai_chat_v2": {"enabled": False, "experiment_id": None, "variant": None, "payload": None},
}


class AttributionContext(BaseModel):
    utm_source: Optional[str] = None
    utm_medium: Optional[str] = None
    utm_campaign: Optional[str] = None
    experiment_id: Optional[str] = None
    variant: Optional[str] = None


class FeatureFlagRequest(BaseModel):
    attribution: Optional[AttributionContext] = None


@router.post("/feature-flags")
async def get_feature_flags(
    request: FeatureFlagRequest,
    user: dict = Depends(get_current_user),
):
    """
    Get feature flags for the current user.
    
    Flags can be controlled by:
    - User attributes (subscription, signup date, etc.)
    - Attribution (utm_source for ad cohorts)
    - Explicit experiment assignment
    - MozaiksAI dynamic configuration
    """
    user_id = user["user_id"]
    attribution = request.attribution
    
    # Load user data for cohort assignment
    users_collection = db["users"]
    user_doc = await users_collection.find_one({"_id": user_id}) or {}
    
    # Determine user cohorts
    cohorts = []
    
    if attribution:
        if attribution.utm_source:
            cohorts.append(f"utm_{attribution.utm_source}")
        if attribution.utm_medium == "cpc":
            cohorts.append("ad_acquired")
    
    if user_doc.get("created_at"):
        # Add signup cohort (e.g., "registered_2026q1")
        from datetime import datetime
        try:
            created = datetime.fromisoformat(user_doc["created_at"].replace("Z", "+00:00"))
            quarter = (created.month - 1) // 3 + 1
            cohorts.append(f"registered_{created.year}q{quarter}")
        except:
            pass
    
    # Load flags from config or database
    # In production, this would query a feature_flags collection controlled by MozaiksAI
    flags = DEFAULT_FLAGS.copy()
    
    # Example: Enable new onboarding for ad-acquired users
    if "ad_acquired" in cohorts:
        flags["new_onboarding_flow"] = {
            "enabled": True,
            "experiment_id": "exp_onboarding_ad_users",
            "variant": "treatment",
            "payload": {"steps": ["welcome", "value_prop", "plugins"]},
        }
    
    return {
        "flags": flags,
        "user_cohorts": cohorts,
        "evaluated_at": datetime.utcnow().isoformat(),
    }
```

---

## 3. Dynamic Navigation & Layout

### 3.1 Navigation with Feature Flags

```jsx
// Modification to src/App.jsx

import { useFeatureFlags } from './core/experiments/FeatureFlagProvider';

const App = () => {
  const { isEnabled, getPayload, isInCohort } = useFeatureFlags();
  const [navigation, setNavigation] = useState([]);

  useEffect(() => {
    async function fetchNavigation() {
      if (!isAuthenticated) return;

      try {
        const response = await authFetch("/api/navigation");
        if (!response.ok) throw new Error("Failed to load navigation");

        let data = await response.json();
        let filteredNavigation = data.navigation?.filter(item => item.path !== '/profile') || [];
        
        // === NEW: Apply feature flag modifications ===
        
        // Simplified navigation experiment
        if (isEnabled('simplified_navigation')) {
          filteredNavigation = filteredNavigation.filter(item => 
            ['/', '/profile', '/plugins/task_manager'].includes(item.path)
          );
        }
        
        // Add experiment-specific nav items
        if (isEnabled('ai_chat_v2') && getPayload('ai_chat_v2')?.show_in_nav) {
          filteredNavigation.push({
            label: 'AI Chat',
            path: '/ai/chat-v2',
            icon: 'message-circle',
            badge: 'NEW',
          });
        }
        
        setNavigation(filteredNavigation);
      } catch (error) {
        console.error("⚠️ Navigation fetch error:", error);
      }
    }

    fetchNavigation();
  }, [isAuthenticated, authFetch, isEnabled, getPayload]);

  // ... rest of component
};
```

### 3.2 Graceful Plugin Degradation

```jsx
// Enhanced src/core/plugins/DynamicUIComponent.jsx

import { useFeatureFlags } from '../experiments/FeatureFlagProvider';
import telemetryService from '../analytics/TelemetryService';

const DynamicUIComponent = ({ 
  pluginName, 
  componentName = "default", 
  pluginProps = {},
  fallback = null 
}) => {
  const { isEnabled } = useFeatureFlags();
  const [Component, setComponent] = useState(null);
  const [isAccessible, setIsAccessible] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [loadAttempts, setLoadAttempts] = useState(0);

  // ... existing access check logic ...

  // Enhanced error handling with telemetry
  useEffect(() => {
    if (error) {
      telemetryService.trackError(
        new Error(error),
        `DynamicUIComponent:${pluginName}`
      );
    }
  }, [error, pluginName]);

  // Render with graceful degradation
  if (loading) {
    return fallback || <LoadingSkeleton />;
  }

  if (error || !isAccessible) {
    // Check if we should show a friendly fallback
    if (isEnabled('graceful_plugin_fallback')) {
      return (
        <div className="bg-yellow-50 border border-yellow-200 p-4 rounded">
          <p className="font-medium text-yellow-800">Plugin temporarily unavailable</p>
          <p className="text-sm text-yellow-600 mt-1">
            {pluginName} is being updated. Please try again later.
          </p>
          <button 
            onClick={() => setLoadAttempts(prev => prev + 1)}
            className="mt-2 text-sm text-yellow-700 underline"
          >
            Retry
          </button>
        </div>
      );
    }

    // Default error display
    return (
      <div className="bg-red-50 border border-red-200 p-4 rounded text-red-600">
        <p className="font-medium">Error</p>
        <p className="text-sm">{error || "Access denied"}</p>
      </div>
    );
  }

  // ... rest of component
};
```

---

## 4. Attribution Capture in the Browser

### 4.1 Attribution Capture on First Load

```jsx
// src/core/analytics/AttributionCapture.jsx

import { useEffect } from 'react';
import telemetryService from './TelemetryService';

/**
 * Component that captures attribution data on first load.
 * Should be placed high in the component tree.
 */
export function AttributionCapture() {
  useEffect(() => {
    // Capture attribution from URL params
    telemetryService.captureAttribution();
    
    // Track initial page view with attribution
    telemetryService.track('navigation.landing', {
      path: window.location.pathname,
      has_attribution: !!telemetryService.attribution.first_touch,
    });
  }, []);

  return null;
}
```

### 4.2 Attribution Transfer on Sign-Up

```jsx
// Modification to src/auth/AuthContext.jsx

const register = useCallback(
  async (username, password, email, fullName, { remember = false } = {}) => {
    if (authMode !== 'local') return false;
    setIsLoading(true);
    setError(null);
    try {
      // === NEW: Include attribution in registration ===
      const attribution = telemetryService.attribution || {};
      
      const response = await fetch('/api/auth/register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ 
          username, 
          password, 
          email, 
          full_name: fullName,
          // Send attribution to backend for permanent storage
          attribution: attribution.first_touch || null,
        }),
      });

      const data = await response.json();
      if (!response.ok) throw new Error(data.detail || 'Registration failed');

      localTokenStore.set(data.access_token, remember);
      
      // === NEW: Track registration with attribution ===
      telemetryService.track('auth.register', {
        user_id: data.user_id,
        has_attribution: !!attribution.first_touch,
      });
      
      const resolvedUser = await validateToken(data.access_token);
      setUser(resolvedUser);
      return true;
    } catch (err) {
      setError(err.message || 'Registration failed');
      return false;
    } finally {
      setIsLoading(false);
    }
  },
  [authMode, validateToken]
);
```

---

## 5. Error Surfacing for the Validation Loop

### 5.1 Global Error Boundary

```jsx
// src/core/errors/ErrorBoundary.jsx

import React from 'react';
import telemetryService from '../analytics/TelemetryService';

export class ErrorBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false, error: null, errorInfo: null };
  }

  static getDerivedStateFromError(error) {
    return { hasError: true, error };
  }

  componentDidCatch(error, errorInfo) {
    this.setState({ errorInfo });
    
    // Report to telemetry
    telemetryService.track('error.frontend', {
      error: {
        message: error.message,
        stack: error.stack,
        component: this.props.name || 'Unknown',
        componentStack: errorInfo?.componentStack,
      },
      route: window.location.pathname,
      context: {
        plugin: this.props.pluginName,
        component: this.props.name,
      },
    });
  }

  render() {
    if (this.state.hasError) {
      return this.props.fallback || (
        <div className="p-6 bg-red-50 border border-red-200 rounded-lg">
          <h2 className="text-lg font-semibold text-red-800">Something went wrong</h2>
          <p className="text-red-600 mt-2">
            {this.props.userMessage || 'An unexpected error occurred. Please refresh the page.'}
          </p>
          {process.env.NODE_ENV === 'development' && (
            <details className="mt-4">
              <summary className="cursor-pointer text-red-700">Error details</summary>
              <pre className="mt-2 text-xs overflow-auto p-2 bg-red-100 rounded">
                {this.state.error?.toString()}
                {this.state.errorInfo?.componentStack}
              </pre>
            </details>
          )}
          <button
            onClick={() => window.location.reload()}
            className="mt-4 px-4 py-2 bg-red-600 text-white rounded hover:bg-red-700"
          >
            Refresh Page
          </button>
        </div>
      );
    }

    return this.props.children;
  }
}

// HOC for wrapping components
export function withErrorBoundary(Component, options = {}) {
  return function WrappedComponent(props) {
    return (
      <ErrorBoundary {...options}>
        <Component {...props} />
      </ErrorBoundary>
    );
  };
}
```

### 5.2 Error Report Schema for MozaiksAI

```typescript
// Error report sent to backend
interface ErrorReport {
  // Error identification
  error_id: string;  // UUID generated client-side
  
  // Error details
  message: string;
  stack?: string;
  component_stack?: string;
  
  // Context
  route: string;
  plugin_name?: string;
  component_name?: string;
  
  // User context
  user_id?: string;
  session_id: string;
  
  // Environment
  release_id?: string;
  experiment_id?: string;
  variant?: string;
  
  // Client info
  user_agent: string;
  viewport: { width: number; height: number };
  timestamp: string;
  
  // Attribution (for debugging acquisition-related issues)
  attribution?: {
    utm_source?: string;
    utm_campaign?: string;
  };
}
```

---

## 6. Minimum Viable Implementation

### 6.1 Phase 1: Core Telemetry (Files to Create)

```
src/core/analytics/
├── TelemetryService.js       # Main service (section 1.3)
├── useTelemetry.js           # React hooks (section 1.4)
├── AttributionCapture.jsx    # Attribution component (section 4.1)
└── index.js                  # Exports

src/core/experiments/
├── FeatureFlagProvider.jsx   # Feature flag context (section 2.2)
└── index.js                  # Exports

src/core/errors/
├── ErrorBoundary.jsx         # Error boundary (section 5.1)
└── index.js                  # Exports
```

### 6.2 Phase 1: Integration Points (Files to Modify)

| File | Change |
|------|--------|
| [src/App.jsx](../../src/App.jsx) | Add providers, telemetry hook |
| [src/auth/AuthContext.jsx](../../src/auth/AuthContext.jsx) | Track auth events |
| [src/core/plugins/DynamicUIComponent.jsx](../../src/core/plugins/DynamicUIComponent.jsx) | Track plugin events |
| [src/main.jsx](../../src/main.jsx) | Wrap with ErrorBoundary |

### 6.3 Minimal App.jsx Integration

```jsx
// Modified src/App.jsx

import { AttributionCapture, useTelemetry } from './core/analytics';
import { FeatureFlagProvider } from './core/experiments';
import { ErrorBoundary } from './core/errors';

const App = () => {
  // Initialize telemetry
  useTelemetry();
  
  // ... existing code ...

  return (
    <ErrorBoundary name="App">
      <FeatureFlagProvider>
        <Router>
          <AttributionCapture />
          {/* ... rest of app ... */}
        </Router>
      </FeatureFlagProvider>
    </ErrorBoundary>
  );
};
```

### 6.4 Backend Endpoint for Batch Telemetry

```python
# backend/core/routes/telemetry.py

from fastapi import APIRouter, Depends, HTTPException
from typing import List, Optional
from pydantic import BaseModel
from datetime import datetime

from security.authentication import get_current_user_optional
from core.analytics.telemetry_service import telemetry_service

router = APIRouter()


class ClientInfo(BaseModel):
    url: Optional[str] = None
    referrer: Optional[str] = None
    viewport: Optional[dict] = None


class Attribution(BaseModel):
    utm_source: Optional[str] = None
    utm_medium: Optional[str] = None
    utm_campaign: Optional[str] = None
    experiment_id: Optional[str] = None
    variant: Optional[str] = None


class TelemetryEvent(BaseModel):
    event_type: str
    timestamp: str
    session_id: Optional[str] = None
    attribution: Optional[Attribution] = None
    context: Optional[dict] = None
    data: Optional[dict] = None
    error: Optional[dict] = None
    client: Optional[ClientInfo] = None


class BatchRequest(BaseModel):
    events: List[TelemetryEvent]


@router.post("/telemetry/batch")
async def batch_telemetry(
    request: BatchRequest,
    user: dict = Depends(get_current_user_optional),
):
    """
    Receive batch telemetry events from the frontend.
    """
    user_id = user.get("user_id") if user else None
    
    results = []
    for event in request.events:
        try:
            client_timestamp = datetime.fromisoformat(event.timestamp.replace("Z", "+00:00"))
        except:
            client_timestamp = None
        
        event_id = await telemetry_service.log_event(
            event.event_type,
            user_id=user_id,
            session_id=event.session_id,
            data=event.data,
            attribution=event.attribution.dict() if event.attribution else None,
            experiment_context=event.context,
            error=event.error,
            client=event.client.dict() if event.client else None,
            client_timestamp=client_timestamp,
        )
        results.append({"event_type": event.event_type, "logged": event_id is not None})
    
    return {"processed": len(results), "results": results}
```

---

## Summary: Complete File Structure

### Backend (New Files)

```
backend/core/analytics/
├── __init__.py                    # Module exports
├── telemetry_service.py           # Main telemetry service
├── telemetry_indexes.py           # MongoDB index definitions
├── webhook_publisher.py           # Optional Control Plane webhook
├── raw_events.py                  # (Existing) User activity events
├── kpi_service.py                 # (Existing) KPI aggregation
└── app_kpi_snapshot_service.py    # (Existing) Daily snapshots

backend/core/routes/
├── telemetry.py                   # POST /api/telemetry/batch
├── telemetry_admin.py             # GET /__mozaiks/admin/telemetry/*
└── feature_flags.py               # POST /api/feature-flags
```

### Frontend (New Files)

```
src/core/analytics/
├── index.js                       # Module exports
├── TelemetryService.js            # Main telemetry service
├── useTelemetry.js                # React hooks
└── AttributionCapture.jsx         # Attribution capture component

src/core/experiments/
├── index.js                       # Module exports
└── FeatureFlagProvider.jsx        # Feature flag context & hooks

src/core/errors/
├── index.js                       # Module exports
└── ErrorBoundary.jsx              # Global error boundary
```

### Configuration

```
backend/core/config/
├── feature_flags.json             # Default feature flag configuration
└── experiment_config.json         # Experiment definitions
```

---

## Security Considerations

1. **No PII in telemetry data** - Never log passwords, emails, or sensitive user data
2. **Rate limiting** - Batch endpoint should be rate-limited per user/IP
3. **Admin routes protected** - All `/__mozaiks/admin/*` routes require internal API key
4. **Attribution privacy** - UTM parameters are aggregated, not exposed individually
5. **Error sanitization** - Stack traces are only stored, never exposed to users

---

## Next Steps

1. **Review this design** with the team
2. **Prioritize phases** based on Control Plane readiness
3. **Create implementation tickets** for each phase
4. **Set up monitoring** for telemetry pipeline health
