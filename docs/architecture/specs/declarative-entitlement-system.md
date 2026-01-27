# Declarative Entitlement System

> **Status**: Design Spec  
> **Scope**: mozaiks-core (OSS) + mozaiks-platform (Proprietary)  
> **Primary Focus**: Token management, usage tracking, observability  

---

## Executive Summary

A **declarative entitlement manifest** that:
- **Core reads** and optionally enforces
- **Platform writes** and definitely enforces
- **Self-hosters configure** freely (no artificial limits)
- **Apps customize** per their business model

The manifest is the **single source of truth** for what an app/user can do at runtime.

---

## Design Principles

| Principle | Core (OSS) | Platform (Proprietary) |
|-----------|------------|------------------------|
| Manifest authorship | User/admin creates | Platform manages |
| Enforcement mode | Advisory (default) | Mandatory |
| Token tracking | Local metrics | Aggregated + billed |
| Observability | Full access | Full + analytics |
| Upgrades/limits | User decides | Platform controls |

**Key Insight**: The manifest schema is **identical** in both modes. Only the **source** and **enforcement** differ.

---

## The Entitlement Manifest

### Schema: `entitlement_manifest.schema.json`

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "Mozaiks Entitlement Manifest",
  "description": "Declarative subscription and usage entitlements for an app",
  "type": "object",
  "required": ["version", "app_id", "plan", "token_budget", "features"],
  "properties": {
    "version": {
      "type": "string",
      "enum": ["1.0"],
      "description": "Manifest schema version"
    },
    "app_id": {
      "type": "string",
      "description": "Unique application identifier"
    },
    "tenant_id": {
      "type": "string",
      "description": "Multi-tenant identifier (platform mode)"
    },
    "plan": {
      "$ref": "#/definitions/Plan"
    },
    "token_budget": {
      "$ref": "#/definitions/TokenBudget"
    },
    "features": {
      "$ref": "#/definitions/Features"
    },
    "rate_limits": {
      "$ref": "#/definitions/RateLimits"
    },
    "observability": {
      "$ref": "#/definitions/Observability"
    },
    "plugins": {
      "$ref": "#/definitions/PluginEntitlements"
    },
    "overrides": {
      "$ref": "#/definitions/Overrides"
    },
    "metadata": {
      "type": "object",
      "description": "Arbitrary metadata for app-specific logic"
    }
  },
  "definitions": {
    "Plan": {
      "type": "object",
      "required": ["id", "name"],
      "properties": {
        "id": { "type": "string" },
        "name": { "type": "string" },
        "tier": { 
          "type": "string", 
          "enum": ["free", "starter", "pro", "enterprise", "unlimited"] 
        },
        "billing_period": { 
          "type": "string", 
          "enum": ["none", "monthly", "annual"] 
        },
        "expires_at": { 
          "type": "string", 
          "format": "date-time",
          "description": "null = never expires (self-hosted default)"
        }
      }
    },
    "TokenBudget": {
      "type": "object",
      "description": "Token allocation and tracking",
      "properties": {
        "period": {
          "type": "string",
          "enum": ["hourly", "daily", "monthly", "lifetime", "unlimited"]
        },
        "input_tokens": {
          "$ref": "#/definitions/TokenAllocation"
        },
        "output_tokens": {
          "$ref": "#/definitions/TokenAllocation"
        },
        "total_tokens": {
          "$ref": "#/definitions/TokenAllocation"
        },
        "per_model_limits": {
          "type": "object",
          "additionalProperties": {
            "$ref": "#/definitions/TokenAllocation"
          },
          "description": "Model-specific limits (e.g., gpt-4 vs gpt-3.5)"
        },
        "rollover": {
          "type": "boolean",
          "default": false,
          "description": "Unused tokens roll to next period"
        },
        "burst_allowance": {
          "type": "number",
          "description": "Percentage over limit allowed temporarily"
        }
      }
    },
    "TokenAllocation": {
      "type": "object",
      "properties": {
        "limit": { 
          "type": "integer",
          "description": "-1 = unlimited"
        },
        "used": { 
          "type": "integer",
          "default": 0
        },
        "reserved": {
          "type": "integer",
          "default": 0,
          "description": "Tokens reserved for in-flight requests"
        },
        "enforcement": {
          "type": "string",
          "enum": ["none", "warn", "soft", "hard"],
          "default": "none",
          "description": "none=no limit, warn=log only, soft=allow with warning, hard=reject"
        }
      }
    },
    "Features": {
      "type": "object",
      "description": "Feature flags and capabilities",
      "properties": {
        "workflow_execution": { "type": "boolean", "default": true },
        "multi_agent": { "type": "boolean", "default": true },
        "streaming": { "type": "boolean", "default": true },
        "function_calling": { "type": "boolean", "default": true },
        "code_execution": { "type": "boolean", "default": false },
        "file_uploads": { "type": "boolean", "default": true },
        "memory_persistence": { "type": "boolean", "default": true },
        "custom_models": { "type": "boolean", "default": true },
        "priority_queue": { "type": "boolean", "default": false },
        "dedicated_resources": { "type": "boolean", "default": false }
      },
      "additionalProperties": { "type": "boolean" }
    },
    "RateLimits": {
      "type": "object",
      "properties": {
        "requests_per_minute": { "type": "integer", "default": -1 },
        "requests_per_hour": { "type": "integer", "default": -1 },
        "concurrent_workflows": { "type": "integer", "default": -1 },
        "max_workflow_duration_seconds": { "type": "integer", "default": -1 },
        "max_agents_per_workflow": { "type": "integer", "default": -1 },
        "max_turns_per_workflow": { "type": "integer", "default": -1 }
      }
    },
    "Observability": {
      "type": "object",
      "description": "What telemetry is available to the app",
      "properties": {
        "level": {
          "type": "string",
          "enum": ["minimal", "standard", "detailed", "full"],
          "default": "full"
        },
        "token_tracking": { "type": "boolean", "default": true },
        "cost_tracking": { "type": "boolean", "default": true },
        "latency_metrics": { "type": "boolean", "default": true },
        "agent_traces": { "type": "boolean", "default": true },
        "tool_call_logs": { "type": "boolean", "default": true },
        "retention_days": { "type": "integer", "default": -1 },
        "export_enabled": { "type": "boolean", "default": true }
      }
    },
    "PluginEntitlements": {
      "type": "object",
      "properties": {
        "allowed": {
          "type": "array",
          "items": { "type": "string" },
          "description": "Plugin IDs allowed, empty = all allowed"
        },
        "blocked": {
          "type": "array",
          "items": { "type": "string" },
          "description": "Plugin IDs explicitly blocked"
        },
        "per_plugin_limits": {
          "type": "object",
          "additionalProperties": {
            "type": "object",
            "properties": {
              "calls_per_period": { "type": "integer" },
              "period": { "type": "string" }
            }
          }
        }
      }
    },
    "Overrides": {
      "type": "object",
      "description": "Temporary or contextual overrides",
      "properties": {
        "trial_extension": {
          "type": "object",
          "properties": {
            "until": { "type": "string", "format": "date-time" },
            "reason": { "type": "string" }
          }
        },
        "promotional_tokens": {
          "type": "integer",
          "description": "Bonus tokens added to budget"
        },
        "feature_previews": {
          "type": "array",
          "items": { "type": "string" },
          "description": "Features enabled for beta testing"
        }
      }
    }
  }
}
```

---

## Example Manifests

### Self-Hosted (OSS) - Unlimited

```json
{
  "version": "1.0",
  "app_id": "my-self-hosted-app",
  "plan": {
    "id": "self-hosted",
    "name": "Self-Hosted",
    "tier": "unlimited",
    "billing_period": "none",
    "expires_at": null
  },
  "token_budget": {
    "period": "unlimited",
    "total_tokens": {
      "limit": -1,
      "enforcement": "none"
    }
  },
  "features": {
    "workflow_execution": true,
    "multi_agent": true,
    "streaming": true,
    "function_calling": true,
    "code_execution": true,
    "file_uploads": true,
    "memory_persistence": true,
    "custom_models": true,
    "priority_queue": true,
    "dedicated_resources": true
  },
  "rate_limits": {
    "requests_per_minute": -1,
    "concurrent_workflows": -1
  },
  "observability": {
    "level": "full",
    "token_tracking": true,
    "cost_tracking": true,
    "retention_days": -1,
    "export_enabled": true
  },
  "plugins": {
    "allowed": [],
    "blocked": []
  }
}
```

### Platform - Starter Plan

```json
{
  "version": "1.0",
  "app_id": "app_abc123",
  "tenant_id": "tenant_xyz",
  "plan": {
    "id": "starter_monthly",
    "name": "Starter",
    "tier": "starter",
    "billing_period": "monthly",
    "expires_at": "2026-02-25T00:00:00Z"
  },
  "token_budget": {
    "period": "monthly",
    "total_tokens": {
      "limit": 1000000,
      "used": 245000,
      "reserved": 5000,
      "enforcement": "soft"
    },
    "per_model_limits": {
      "gpt-4": {
        "limit": 100000,
        "used": 45000,
        "enforcement": "hard"
      },
      "gpt-3.5-turbo": {
        "limit": 900000,
        "used": 200000,
        "enforcement": "soft"
      }
    },
    "rollover": false,
    "burst_allowance": 10
  },
  "features": {
    "workflow_execution": true,
    "multi_agent": true,
    "streaming": true,
    "function_calling": true,
    "code_execution": false,
    "priority_queue": false
  },
  "rate_limits": {
    "requests_per_minute": 60,
    "concurrent_workflows": 5,
    "max_workflow_duration_seconds": 300
  },
  "observability": {
    "level": "standard",
    "retention_days": 30,
    "export_enabled": false
  }
}
```

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           MOZAIKS ECOSYSTEM                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    ENTITLEMENT MANIFEST                              │    │
│  │                  (Single Source of Truth)                            │    │
│  │                                                                      │    │
│  │   ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐  │    │
│  │   │ Token Budget │  │   Features   │  │    Observability Level   │  │    │
│  │   │  per period  │  │    flags     │  │   retention, metrics     │  │    │
│  │   └──────────────┘  └──────────────┘  └──────────────────────────┘  │    │
│  │   ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐  │    │
│  │   │ Rate Limits  │  │   Plugins    │  │       Overrides          │  │    │
│  │   │  RPM, conc.  │  │  allow/block │  │   trials, promotions     │  │    │
│  │   └──────────────┘  └──────────────┘  └──────────────────────────┘  │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                                    │                                         │
│                    ┌───────────────┴───────────────┐                        │
│                    ▼                               ▼                        │
│  ┌─────────────────────────────┐   ┌─────────────────────────────────────┐  │
│  │      SELF-HOSTED (OSS)      │   │       PLATFORM (PROPRIETARY)        │  │
│  ├─────────────────────────────┤   ├─────────────────────────────────────┤  │
│  │                             │   │                                     │  │
│  │  Manifest Source:           │   │  Manifest Source:                   │  │
│  │  • Local file               │   │  • Control Plane API                │  │
│  │  • Environment vars         │   │  • Synced on auth                   │  │
│  │  • Admin UI                 │   │  • Real-time updates                │  │
│  │                             │   │                                     │  │
│  │  Enforcement:               │   │  Enforcement:                       │  │
│  │  • Default: NONE            │   │  • Default: HARD                    │  │
│  │  • User chooses             │   │  • Platform controls                │  │
│  │  • No phone-home            │   │  • Usage → Billing                  │  │
│  │                             │   │                                     │  │
│  │  Token Tracking:            │   │  Token Tracking:                    │  │
│  │  • Local metrics            │   │  • Aggregated to platform           │  │
│  │  • Full visibility          │   │  • Per-request billing              │  │
│  │  • Export anywhere          │   │  • Cost attribution                 │  │
│  │                             │   │                                     │  │
│  │  Value:                     │   │  Value:                             │  │
│  │  ✓ Full runtime             │   │  ✓ Managed infrastructure           │  │
│  │  ✓ No artificial limits     │   │  ✓ Billing automation               │  │
│  │  ✓ Complete observability   │   │  ✓ Multi-tenant isolation           │  │
│  │  ✓ Self-sovereign           │   │  ✓ Usage analytics                  │  │
│  │                             │   │  ✓ Upgrade/downgrade flows          │  │
│  └─────────────────────────────┘   │  ✓ Revenue sharing                  │  │
│                                    └─────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Core Components (mozaiks-core)

### 1. EntitlementReader

Reads and caches the manifest. **Never writes**.

```python
# runtime/ai/src/core/entitlements/reader.py

from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Optional
import json

@dataclass(frozen=True)
class EntitlementManifest:
    """Immutable entitlement state"""
    version: str
    app_id: str
    plan: dict
    token_budget: dict
    features: dict
    rate_limits: dict
    observability: dict
    plugins: dict
    overrides: dict
    metadata: dict
    
    @classmethod
    def from_dict(cls, data: dict) -> "EntitlementManifest":
        return cls(
            version=data.get("version", "1.0"),
            app_id=data["app_id"],
            plan=data.get("plan", {}),
            token_budget=data.get("token_budget", {}),
            features=data.get("features", {}),
            rate_limits=data.get("rate_limits", {}),
            observability=data.get("observability", {}),
            plugins=data.get("plugins", {}),
            overrides=data.get("overrides", {}),
            metadata=data.get("metadata", {}),
        )
    
    @classmethod
    def unlimited(cls, app_id: str) -> "EntitlementManifest":
        """Default manifest for self-hosted: everything unlimited"""
        return cls(
            version="1.0",
            app_id=app_id,
            plan={"id": "self-hosted", "tier": "unlimited"},
            token_budget={"period": "unlimited", "total_tokens": {"limit": -1, "enforcement": "none"}},
            features={
                "workflow_execution": True,
                "multi_agent": True,
                "streaming": True,
                "function_calling": True,
                "code_execution": True,
                "file_uploads": True,
                "memory_persistence": True,
                "custom_models": True,
            },
            rate_limits={},
            observability={"level": "full", "retention_days": -1},
            plugins={"allowed": [], "blocked": []},
            overrides={},
            metadata={},
        )


class EntitlementSource(ABC):
    """Abstract source for entitlement manifests"""
    
    @abstractmethod
    async def fetch(self, app_id: str) -> EntitlementManifest:
        """Fetch the current manifest for an app"""
        pass
    
    @abstractmethod
    async def subscribe(self, app_id: str, callback) -> None:
        """Subscribe to manifest changes (optional)"""
        pass


class LocalFileSource(EntitlementSource):
    """Read manifest from local file (self-hosted)"""
    
    def __init__(self, manifest_path: str):
        self.manifest_path = manifest_path
        self._cached: Optional[EntitlementManifest] = None
    
    async def fetch(self, app_id: str) -> EntitlementManifest:
        try:
            with open(self.manifest_path) as f:
                data = json.load(f)
            self._cached = EntitlementManifest.from_dict(data)
            return self._cached
        except FileNotFoundError:
            # No manifest = unlimited (OSS default)
            return EntitlementManifest.unlimited(app_id)
    
    async def subscribe(self, app_id: str, callback) -> None:
        # Could use file watcher for hot reload
        pass


class EnvironmentSource(EntitlementSource):
    """Read manifest from environment (container-friendly)"""
    
    def __init__(self):
        import os
        self.manifest_json = os.environ.get("MOZAIKS_ENTITLEMENT_MANIFEST")
    
    async def fetch(self, app_id: str) -> EntitlementManifest:
        if not self.manifest_json:
            return EntitlementManifest.unlimited(app_id)
        data = json.loads(self.manifest_json)
        return EntitlementManifest.from_dict(data)
    
    async def subscribe(self, app_id: str, callback) -> None:
        pass  # Environment doesn't support live updates


class ControlPlaneSource(EntitlementSource):
    """Fetch manifest from platform control plane (platform mode)"""
    
    def __init__(self, control_plane_url: str, api_key: str):
        self.control_plane_url = control_plane_url
        self.api_key = api_key
        self._cache: dict[str, EntitlementManifest] = {}
    
    async def fetch(self, app_id: str) -> EntitlementManifest:
        import httpx
        
        async with httpx.AsyncClient() as client:
            response = await client.get(
                f"{self.control_plane_url}/api/v1/entitlements/{app_id}",
                headers={"Authorization": f"Bearer {self.api_key}"}
            )
            response.raise_for_status()
            data = response.json()
        
        manifest = EntitlementManifest.from_dict(data)
        self._cache[app_id] = manifest
        return manifest
    
    async def subscribe(self, app_id: str, callback) -> None:
        # WebSocket or SSE subscription for real-time updates
        pass
```

### 2. TokenBudgetTracker

Tracks token usage against the manifest.

```python
# runtime/ai/src/core/entitlements/token_tracker.py

from dataclasses import dataclass, field
from datetime import datetime, timezone
from enum import Enum
from typing import Optional
import logging

logger = logging.getLogger(__name__)


class EnforcementAction(Enum):
    ALLOW = "allow"
    WARN = "warn"
    SOFT_REJECT = "soft_reject"  # Allow but flag for billing
    HARD_REJECT = "hard_reject"  # Block request


@dataclass
class TokenUsageEvent:
    """Emitted after each token-consuming operation"""
    timestamp: datetime
    app_id: str
    workflow_id: str
    model: str
    input_tokens: int
    output_tokens: int
    total_tokens: int
    budget_remaining: int
    enforcement_action: EnforcementAction
    cost_estimate: Optional[float] = None


@dataclass
class TokenBudgetTracker:
    """
    Tracks token usage against entitlement budget.
    
    Core responsibility: Track and report.
    Enforcement policy is configurable.
    """
    
    manifest: "EntitlementManifest"
    enforce: bool = False  # OSS default: no enforcement
    
    # In-memory tracking (reset on restart, platform syncs to persistent store)
    _period_start: datetime = field(default_factory=lambda: datetime.now(timezone.utc))
    _input_used: int = 0
    _output_used: int = 0
    _total_used: int = 0
    _per_model_used: dict = field(default_factory=dict)
    
    def check_budget(
        self, 
        model: str, 
        estimated_tokens: int
    ) -> tuple[EnforcementAction, str]:
        """
        Pre-flight check before making an LLM call.
        Returns action to take and reason.
        """
        budget = self.manifest.token_budget
        
        # No budget defined = unlimited
        if not budget or budget.get("period") == "unlimited":
            return EnforcementAction.ALLOW, "unlimited budget"
        
        total_config = budget.get("total_tokens", {})
        limit = total_config.get("limit", -1)
        enforcement = total_config.get("enforcement", "none")
        
        # -1 means unlimited
        if limit == -1:
            return EnforcementAction.ALLOW, "unlimited"
        
        remaining = limit - self._total_used
        
        if remaining >= estimated_tokens:
            return EnforcementAction.ALLOW, f"{remaining} tokens remaining"
        
        # Over budget - what's the enforcement?
        if not self.enforce or enforcement == "none":
            return EnforcementAction.ALLOW, "enforcement disabled"
        
        if enforcement == "warn":
            logger.warning(
                f"Token budget exceeded: {self._total_used}/{limit} "
                f"(+{estimated_tokens} requested)"
            )
            return EnforcementAction.WARN, "over budget, warning only"
        
        if enforcement == "soft":
            return EnforcementAction.SOFT_REJECT, "over budget, soft limit"
        
        if enforcement == "hard":
            return EnforcementAction.HARD_REJECT, f"budget exhausted: {remaining} remaining"
        
        return EnforcementAction.ALLOW, "unknown enforcement"
    
    def record_usage(
        self,
        workflow_id: str,
        model: str,
        input_tokens: int,
        output_tokens: int,
    ) -> TokenUsageEvent:
        """
        Record actual token usage after LLM call.
        Returns event for telemetry pipeline.
        """
        total = input_tokens + output_tokens
        
        self._input_used += input_tokens
        self._output_used += output_tokens
        self._total_used += total
        self._per_model_used[model] = self._per_model_used.get(model, 0) + total
        
        budget = self.manifest.token_budget
        total_config = budget.get("total_tokens", {})
        limit = total_config.get("limit", -1)
        remaining = limit - self._total_used if limit != -1 else -1
        
        # Determine if this pushed us over
        action = EnforcementAction.ALLOW
        if limit != -1 and self._total_used > limit:
            enforcement = total_config.get("enforcement", "none")
            if enforcement == "hard":
                action = EnforcementAction.HARD_REJECT
            elif enforcement == "soft":
                action = EnforcementAction.SOFT_REJECT
            elif enforcement == "warn":
                action = EnforcementAction.WARN
        
        event = TokenUsageEvent(
            timestamp=datetime.now(timezone.utc),
            app_id=self.manifest.app_id,
            workflow_id=workflow_id,
            model=model,
            input_tokens=input_tokens,
            output_tokens=output_tokens,
            total_tokens=total,
            budget_remaining=remaining,
            enforcement_action=action,
        )
        
        return event
    
    def get_usage_summary(self) -> dict:
        """Current period usage summary"""
        budget = self.manifest.token_budget
        total_config = budget.get("total_tokens", {})
        limit = total_config.get("limit", -1)
        
        return {
            "period_start": self._period_start.isoformat(),
            "input_tokens_used": self._input_used,
            "output_tokens_used": self._output_used,
            "total_tokens_used": self._total_used,
            "total_tokens_limit": limit,
            "tokens_remaining": limit - self._total_used if limit != -1 else None,
            "utilization_percent": (
                round(self._total_used / limit * 100, 2) if limit > 0 else None
            ),
            "per_model": self._per_model_used.copy(),
        }
```

### 3. FeatureGate

Simple feature flag checking.

```python
# runtime/ai/src/core/entitlements/feature_gate.py

from typing import Optional
import logging

logger = logging.getLogger(__name__)


class FeatureGate:
    """
    Check if features are enabled in the manifest.
    
    OSS default: Everything enabled.
    Platform: Controlled by subscription tier.
    """
    
    def __init__(self, manifest: "EntitlementManifest", enforce: bool = False):
        self.manifest = manifest
        self.enforce = enforce
    
    def is_enabled(self, feature: str, default: bool = True) -> bool:
        """
        Check if a feature is enabled.
        
        Args:
            feature: Feature key (e.g., "code_execution")
            default: Default if not specified in manifest (OSS default: True)
        
        Returns:
            True if enabled, False if disabled
        """
        features = self.manifest.features
        
        # No features defined = use default (True for OSS)
        if not features:
            return default
        
        enabled = features.get(feature, default)
        
        if not enabled and self.enforce:
            logger.info(f"Feature '{feature}' is disabled by entitlement")
        
        return enabled if self.enforce else default
    
    def require(self, feature: str) -> None:
        """
        Raise if feature is disabled (only in enforce mode).
        """
        if not self.is_enabled(feature):
            if self.enforce:
                raise FeatureDisabledError(
                    f"Feature '{feature}' is not available in your plan. "
                    f"Current plan: {self.manifest.plan.get('name', 'unknown')}"
                )
    
    def get_all_features(self) -> dict[str, bool]:
        """Return all feature flags"""
        return dict(self.manifest.features)


class FeatureDisabledError(Exception):
    """Raised when a feature is not available in the current plan"""
    pass
```

### 4. EntitlementMiddleware

Integrates with the workflow runtime.

```python
# runtime/ai/src/core/entitlements/middleware.py

from typing import Optional, Callable, Awaitable
import os


class EntitlementMiddleware:
    """
    Middleware that injects entitlement checking into workflow execution.
    
    Self-hosted: Pass-through with tracking only
    Platform: Enforce limits and report usage
    """
    
    def __init__(
        self,
        source: "EntitlementSource",
        enforce: Optional[bool] = None,
        on_usage: Optional[Callable[["TokenUsageEvent"], Awaitable[None]]] = None,
    ):
        self.source = source
        # Default: enforce only if MOZAIKS_ENFORCE_ENTITLEMENTS=true
        self.enforce = enforce if enforce is not None else (
            os.environ.get("MOZAIKS_ENFORCE_ENTITLEMENTS", "false").lower() == "true"
        )
        self.on_usage = on_usage
        
        self._manifests: dict[str, "EntitlementManifest"] = {}
        self._trackers: dict[str, "TokenBudgetTracker"] = {}
        self._gates: dict[str, "FeatureGate"] = {}
    
    async def get_manifest(self, app_id: str) -> "EntitlementManifest":
        """Get or fetch manifest for app"""
        if app_id not in self._manifests:
            self._manifests[app_id] = await self.source.fetch(app_id)
        return self._manifests[app_id]
    
    async def get_tracker(self, app_id: str) -> "TokenBudgetTracker":
        """Get or create token tracker for app"""
        if app_id not in self._trackers:
            manifest = await self.get_manifest(app_id)
            self._trackers[app_id] = TokenBudgetTracker(
                manifest=manifest,
                enforce=self.enforce,
            )
        return self._trackers[app_id]
    
    async def get_gate(self, app_id: str) -> "FeatureGate":
        """Get or create feature gate for app"""
        if app_id not in self._gates:
            manifest = await self.get_manifest(app_id)
            self._gates[app_id] = FeatureGate(
                manifest=manifest,
                enforce=self.enforce,
            )
        return self._gates[app_id]
    
    async def check_can_execute(
        self, 
        app_id: str, 
        model: str, 
        estimated_tokens: int
    ) -> tuple[bool, str]:
        """
        Pre-flight check before workflow execution.
        Returns (can_proceed, reason).
        """
        tracker = await self.get_tracker(app_id)
        action, reason = tracker.check_budget(model, estimated_tokens)
        
        if action == EnforcementAction.HARD_REJECT:
            return False, reason
        
        return True, reason
    
    async def record_usage(
        self,
        app_id: str,
        workflow_id: str,
        model: str,
        input_tokens: int,
        output_tokens: int,
    ) -> "TokenUsageEvent":
        """Record token usage and emit event"""
        tracker = await self.get_tracker(app_id)
        event = tracker.record_usage(
            workflow_id=workflow_id,
            model=model,
            input_tokens=input_tokens,
            output_tokens=output_tokens,
        )
        
        if self.on_usage:
            await self.on_usage(event)
        
        return event
    
    async def refresh_manifest(self, app_id: str) -> None:
        """Force refresh manifest from source"""
        self._manifests[app_id] = await self.source.fetch(app_id)
        # Reset tracker with new manifest
        if app_id in self._trackers:
            self._trackers[app_id].manifest = self._manifests[app_id]
        if app_id in self._gates:
            self._gates[app_id].manifest = self._manifests[app_id]
```

---

## Platform Components (mozaiks-platform)

These live in the proprietary repository, not in core.

### 1. EntitlementManager (Platform Only)

```python
# mozaiks-platform: entitlements/manager.py

class EntitlementManager:
    """
    PLATFORM ONLY: Manages entitlement manifests.
    
    - Creates manifests from subscription state
    - Updates manifests on plan changes
    - Syncs to core runtime via API
    - Handles upgrades/downgrades
    """
    
    async def create_manifest(
        self, 
        app_id: str, 
        subscription: "Subscription"
    ) -> EntitlementManifest:
        """Create manifest from subscription"""
        pass
    
    async def apply_plan_change(
        self, 
        app_id: str, 
        new_plan: "Plan"
    ) -> EntitlementManifest:
        """Update manifest when subscription changes"""
        pass
    
    async def sync_to_runtime(
        self, 
        app_id: str, 
        manifest: EntitlementManifest
    ) -> None:
        """Push updated manifest to running runtime"""
        pass
```

### 2. UsageBillingPipeline (Platform Only)

```python
# mozaiks-platform: billing/usage_pipeline.py

class UsageBillingPipeline:
    """
    PLATFORM ONLY: Converts usage events to billing records.
    
    - Receives TokenUsageEvents from core
    - Applies pricing tiers
    - Creates billing line items
    - Handles 3.5% platform fee calculation
    """
    
    async def process_usage_event(self, event: TokenUsageEvent) -> None:
        """Process usage event for billing"""
        pass
    
    async def calculate_period_invoice(self, app_id: str) -> "Invoice":
        """Calculate invoice for billing period"""
        pass
```

---

## Subscription Management AI Workflow

An optional workflow that handles subscription-specific logic per app.

```python
# runtime/ai/examples/workflows/subscription_manager_workflow.py

"""
Subscription Manager Workflow

A modular AI workflow that handles subscription-related interactions.
Can be customized per app for their specific business logic.

Use cases:
- Explain current usage to users
- Handle upgrade requests
- Provide cost optimization suggestions
- Alert on approaching limits
"""

from ag2 import ConversableAgent, UserProxyAgent

# System prompt for subscription assistant
SUBSCRIPTION_ASSISTANT_PROMPT = """You are a helpful subscription and usage assistant.

You have access to the user's entitlement manifest which shows:
- Their current plan and features
- Token budget and usage
- Rate limits
- Available plugins

Your job is to:
1. Answer questions about their subscription
2. Explain usage patterns
3. Suggest optimizations
4. Help with upgrade decisions (if in platform mode)

Current Entitlement Manifest:
{manifest_json}

Current Usage Summary:
{usage_summary}

Guidelines:
- Be helpful and transparent about limits
- If in self-hosted mode, all features are available
- If in platform mode, explain upgrade paths when relevant
- Focus on value, not upselling
"""


def create_subscription_manager_workflow(
    manifest: "EntitlementManifest",
    usage_summary: dict,
    llm_config: dict,
):
    """
    Create a subscription manager workflow.
    
    This workflow can be embedded in any app to provide
    subscription-aware assistance.
    """
    import json
    
    assistant = ConversableAgent(
        name="subscription_assistant",
        system_message=SUBSCRIPTION_ASSISTANT_PROMPT.format(
            manifest_json=json.dumps(manifest.__dict__, indent=2, default=str),
            usage_summary=json.dumps(usage_summary, indent=2),
        ),
        llm_config=llm_config,
    )
    
    user_proxy = UserProxyAgent(
        name="user",
        human_input_mode="ALWAYS",
        code_execution_config=False,
    )
    
    return assistant, user_proxy


# Example tool functions the assistant can use

async def get_usage_breakdown(app_id: str, period: str = "current") -> dict:
    """Get detailed usage breakdown by model, workflow, etc."""
    pass

async def estimate_cost(
    model: str, 
    estimated_tokens: int,
    manifest: "EntitlementManifest"
) -> dict:
    """Estimate cost for a planned operation"""
    pass

async def suggest_plan(usage_pattern: dict) -> dict:
    """Suggest optimal plan based on usage patterns"""
    # Only meaningful in platform mode
    pass

async def request_upgrade(app_id: str, target_plan: str) -> dict:
    """Initiate upgrade request (platform mode only)"""
    pass
```

---

## Integration Points

### 1. Workflow Execution Hook

```python
# runtime/ai/src/runtime/workflow_runner.py

class WorkflowRunner:
    def __init__(self, entitlement_middleware: EntitlementMiddleware):
        self.entitlements = entitlement_middleware
    
    async def run_workflow(
        self,
        app_id: str,
        workflow_config: dict,
        **kwargs
    ):
        # Pre-flight entitlement check
        gate = await self.entitlements.get_gate(app_id)
        
        # Check required features
        if workflow_config.get("uses_code_execution"):
            gate.require("code_execution")
        
        if workflow_config.get("multi_agent"):
            gate.require("multi_agent")
        
        # Check token budget (estimate)
        can_proceed, reason = await self.entitlements.check_can_execute(
            app_id=app_id,
            model=workflow_config.get("model", "gpt-4"),
            estimated_tokens=workflow_config.get("estimated_tokens", 1000),
        )
        
        if not can_proceed:
            raise BudgetExhaustedError(reason)
        
        # Run workflow...
        result = await self._execute(workflow_config, **kwargs)
        
        # Record actual usage
        await self.entitlements.record_usage(
            app_id=app_id,
            workflow_id=result.workflow_id,
            model=result.model_used,
            input_tokens=result.input_tokens,
            output_tokens=result.output_tokens,
        )
        
        return result
```

### 2. Telemetry Export

```python
# runtime/ai/src/core/entitlements/telemetry.py

class EntitlementTelemetryExporter:
    """
    Exports entitlement/usage events to configurable destinations.
    
    Self-hosted: Local metrics, files, any OTEL collector
    Platform: Platform's usage ingestion API
    """
    
    def __init__(self, exporters: list["UsageExporter"]):
        self.exporters = exporters
    
    async def export(self, event: TokenUsageEvent) -> None:
        for exporter in self.exporters:
            await exporter.export(event)


class LocalMetricsExporter:
    """Export to local Prometheus/OTEL"""
    pass

class FileExporter:
    """Export to local file (CSV, JSON)"""
    pass

class PlatformUsageExporter:
    """Export to platform API (platform mode only)"""
    pass
```

---

## Configuration

### Environment Variables

```bash
# Core (OSS)
MOZAIKS_ENTITLEMENT_SOURCE=local          # local, env, control_plane
MOZAIKS_ENTITLEMENT_MANIFEST_PATH=./entitlements.json
MOZAIKS_ENFORCE_ENTITLEMENTS=false        # OSS default: false
MOZAIKS_USAGE_EXPORT=local                # local, otel, platform

# Platform mode
MOZAIKS_ENTITLEMENT_SOURCE=control_plane
MOZAIKS_CONTROL_PLANE_URL=https://api.mozaiks.io
MOZAIKS_ENFORCE_ENTITLEMENTS=true
MOZAIKS_USAGE_EXPORT=platform
```

### Default Manifest (OSS)

When no manifest is provided, core assumes unlimited:

```json
{
  "version": "1.0",
  "app_id": "default",
  "plan": { "tier": "unlimited" },
  "token_budget": { "period": "unlimited" },
  "features": {},
  "rate_limits": {},
  "observability": { "level": "full" }
}
```

---

## Value Proposition Summary

| Capability | Self-Hosted (OSS) | Platform (SaaS) |
|------------|-------------------|-----------------|
| **Entitlement System** | ✅ Full system | ✅ Full system |
| **Token Tracking** | ✅ Local metrics | ✅ Aggregated + billed |
| **Feature Gates** | ✅ All enabled | ✅ Tier-controlled |
| **Enforcement** | ⚪ Optional (off by default) | ✅ Mandatory |
| **Observability** | ✅ Full export anywhere | ✅ Full + analytics |
| **Usage Export** | ✅ OTEL, files, custom | ✅ Platform pipeline |
| **Subscription AI** | ✅ Self-service | ✅ Managed |
| **Cost Control** | ✅ User decides | ✅ Plan-based |
| **Manifest Source** | File / Env | Control Plane API |
| **Manifest Authoring** | User | Platform |

**Self-hosters get:**
- Complete token tracking and observability
- Full feature access
- Choose their own limits (or none)
- Export data anywhere
- No phone-home

**Platform provides:**
- Managed manifests synced from subscriptions
- Automatic enforcement
- Usage → Billing pipeline
- Multi-tenant isolation
- Revenue sharing
- Upgrade/downgrade automation

---

## Implementation Roadmap

### Phase 1: Core Foundation
1. [ ] Create `entitlements/` package structure
2. [ ] Implement `EntitlementManifest` dataclass
3. [ ] Implement `LocalFileSource` and `EnvironmentSource`
4. [ ] Implement `TokenBudgetTracker`
5. [ ] Implement `FeatureGate`
6. [ ] Add schema validation

### Phase 2: Integration
1. [ ] Create `EntitlementMiddleware`
2. [ ] Hook into workflow execution
3. [ ] Add telemetry export
4. [ ] Create CLI for manifest management

### Phase 3: Observability
1. [ ] Prometheus metrics for usage
2. [ ] OTEL trace attributes
3. [ ] Usage dashboard example
4. [ ] Alert templates

### Phase 4: Platform Extensions (mozaiks-platform)
1. [ ] `ControlPlaneSource` implementation
2. [ ] `EntitlementManager` for manifest lifecycle
3. [ ] `UsageBillingPipeline` integration
4. [ ] Real-time manifest sync

### Phase 5: AI Workflows
1. [ ] Subscription manager workflow template
2. [ ] Usage optimization suggestions
3. [ ] Upgrade flow automation

---

## FAQ

**Q: Can self-hosters use entitlement features?**
A: Yes! They get the full system but with enforcement off by default. They can optionally enable enforcement for their own cost control.

**Q: Does core phone home?**
A: No. Unless explicitly configured to export to platform, all data stays local.

**Q: Can apps have custom entitlement logic?**
A: Yes. The `metadata` field in manifests allows app-specific data. The subscription manager workflow can interpret this.

**Q: How does platform sync manifests to core?**
A: Via the `ControlPlaneSource` which polls or subscribes to the platform API. Core never initiates writes.

**Q: What happens if manifest sync fails?**
A: Core uses the last known manifest. In platform mode, this is a critical alert. In self-hosted mode, it falls back to unlimited.
