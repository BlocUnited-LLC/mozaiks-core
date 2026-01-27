# core/billing/entitlements.py
"""
Entitlements Manager - Central entitlement state for Core runtime.

Architecture:
- Self-hosters: Load from local config (entitlements.yaml)
- Platform users: Receive pushes via /api/v1/entitlements/{app_id}/sync

Core NEVER pulls entitlements. It either:
1. Uses local config (self-hosted)
2. Uses cache populated by Platform pushes (SaaS)

This ensures Core works 100% standalone without Platform.
"""

import logging
import os
from dataclasses import dataclass, field
from datetime import datetime
from enum import Enum
from pathlib import Path
from typing import Any, Dict, Optional, Set

import yaml

logger = logging.getLogger("mozaiks_core.billing.entitlements")


class EnforcementMode(str, Enum):
    """How to enforce limits."""
    HARD = "hard"      # Block when exceeded
    SOFT = "soft"      # Allow but warn
    WARN = "warn"      # Log warning only
    NONE = "none"      # No enforcement


@dataclass
class TokenBudget:
    """Token usage limits."""
    limit: int = -1  # -1 = unlimited
    used: int = 0
    period: str = "monthly"
    enforcement: EnforcementMode = EnforcementMode.SOFT


@dataclass
class Entitlements:
    """
    Entitlements for an app.
    
    This is the core data structure that determines what an app can do.
    """
    app_id: str
    tier: str = "free"
    plan_name: str = "Free"
    
    # Token budget
    token_budget: TokenBudget = field(default_factory=TokenBudget)
    
    # Feature flags
    features: Dict[str, bool] = field(default_factory=dict)
    
    # Rate limits
    rate_limits: Dict[str, int] = field(default_factory=dict)
    
    # Metadata
    synced_at: Optional[datetime] = None
    source: str = "default"  # "local", "platform", "default"
    
    def has_feature(self, feature: str, default: bool = True) -> bool:
        """Check if feature is enabled."""
        return self.features.get(feature, default)
    
    def get_rate_limit(self, key: str, default: int = -1) -> int:
        """Get rate limit value (-1 = unlimited)."""
        return self.rate_limits.get(key, default)
    
    def check_token_budget(self, tokens_needed: int = 0) -> tuple[bool, str]:
        """
        Check if token usage is within budget.
        
        Returns:
            (allowed, message) tuple
        """
        if self.token_budget.limit < 0:
            return True, "unlimited"
        
        remaining = self.token_budget.limit - self.token_budget.used
        
        if tokens_needed > remaining:
            if self.token_budget.enforcement == EnforcementMode.HARD:
                return False, f"Token limit exceeded: {self.token_budget.used}/{self.token_budget.limit}"
            elif self.token_budget.enforcement == EnforcementMode.SOFT:
                logger.warning(
                    "Token budget exceeded (soft): app=%s used=%d limit=%d",
                    self.app_id, self.token_budget.used, self.token_budget.limit
                )
                return True, f"soft_limit_exceeded"
            elif self.token_budget.enforcement == EnforcementMode.WARN:
                logger.warning(
                    "Token budget warning: app=%s used=%d limit=%d",
                    self.app_id, self.token_budget.used, self.token_budget.limit
                )
                return True, "warn_limit_exceeded"
        
        return True, "ok"
    
    def consume_tokens(self, count: int) -> None:
        """Record token consumption."""
        self.token_budget.used += count


def _default_entitlements(app_id: str) -> Entitlements:
    """
    Default entitlements when no config exists.
    
    For OSS mode: Everything unlimited, all features enabled.
    """
    return Entitlements(
        app_id=app_id,
        tier="unlimited",
        plan_name="Open Source",
        token_budget=TokenBudget(limit=-1, enforcement=EnforcementMode.NONE),
        features={},  # Empty = all features enabled by default
        rate_limits={},  # Empty = no rate limits
        source="default",
    )


class EntitlementsManager:
    """
    Central entitlements manager for Core runtime.
    
    Sources (in priority order):
    1. Platform sync cache (if Platform has pushed)
    2. Local config file (entitlements.yaml)
    3. Environment defaults
    4. OSS defaults (everything unlimited)
    
    Usage:
        mgr = get_entitlements_manager()
        ent = mgr.get(app_id)
        if ent.has_feature("code_execution"):
            # allow code execution
    """
    
    def __init__(self):
        self._cache: Dict[str, Entitlements] = {}
        self._local_config: Dict[str, Entitlements] = {}
        self._config_loaded = False
    
    def get(self, app_id: str) -> Entitlements:
        """
        Get entitlements for an app.
        
        Priority:
        1. Platform-synced cache
        2. Local config
        3. Defaults
        """
        # Check platform cache first (most recent sync)
        if app_id in self._cache:
            return self._cache[app_id]
        
        # Check local config
        self._ensure_config_loaded()
        if app_id in self._local_config:
            return self._local_config[app_id]
        
        # Check for wildcard/default in local config
        if "*" in self._local_config:
            default = self._local_config["*"]
            # Create a copy with the actual app_id
            return Entitlements(
                app_id=app_id,
                tier=default.tier,
                plan_name=default.plan_name,
                token_budget=default.token_budget,
                features=default.features.copy(),
                rate_limits=default.rate_limits.copy(),
                source="local",
            )
        
        # Return OSS defaults
        return _default_entitlements(app_id)
    
    def update_from_sync(self, app_id: str, data: Dict[str, Any]) -> Entitlements:
        """
        Update entitlements from Platform sync.
        
        Called by the /api/v1/entitlements/{app_id}/sync endpoint.
        """
        plan = data.get("plan", {})
        token_budget_data = data.get("token_budget", {})
        total_tokens = token_budget_data.get("total_tokens", {})
        
        ent = Entitlements(
            app_id=app_id,
            tier=plan.get("tier", "free"),
            plan_name=plan.get("name", "Unknown"),
            token_budget=TokenBudget(
                limit=total_tokens.get("limit", -1),
                used=total_tokens.get("used", 0),
                period=token_budget_data.get("period", "monthly"),
                enforcement=EnforcementMode(total_tokens.get("enforcement", "soft")),
            ),
            features=data.get("features", {}),
            rate_limits=data.get("rate_limits", {}),
            synced_at=datetime.utcnow(),
            source="platform",
        )
        
        self._cache[app_id] = ent
        logger.info("Entitlements synced: app=%s tier=%s", app_id, ent.tier)
        
        return ent
    
    def _ensure_config_loaded(self) -> None:
        """Load local config if not already loaded."""
        if self._config_loaded:
            return
        
        self._config_loaded = True
        
        # Find config file
        config_path = os.getenv("MOZAIKS_ENTITLEMENTS_FILE")
        if not config_path:
            # Check common locations
            candidates = [
                Path("entitlements.yaml"),
                Path("config/entitlements.yaml"),
                Path("/etc/mozaiks/entitlements.yaml"),
            ]
            for path in candidates:
                if path.exists():
                    config_path = str(path)
                    break
        
        if not config_path or not Path(config_path).exists():
            logger.debug("No local entitlements config found")
            return
        
        self._load_config(config_path)
    
    def _load_config(self, path: str) -> None:
        """Load entitlements from YAML file."""
        try:
            with open(path, "r") as f:
                data = yaml.safe_load(f) or {}
            
            apps = data.get("apps", {})
            defaults = data.get("defaults", {})
            
            # Load defaults as wildcard
            if defaults:
                self._local_config["*"] = self._parse_config_entry("*", defaults)
            
            # Load app-specific configs
            for app_id, config in apps.items():
                self._local_config[app_id] = self._parse_config_entry(app_id, config)
            
            logger.info("Loaded entitlements from %s: %d apps", path, len(apps))
            
        except Exception as e:
            logger.error("Failed to load entitlements config: %s", e)
    
    def _parse_config_entry(self, app_id: str, config: Dict[str, Any]) -> Entitlements:
        """Parse a config entry into Entitlements."""
        token_budget_data = config.get("token_budget", {})
        
        return Entitlements(
            app_id=app_id,
            tier=config.get("tier", "custom"),
            plan_name=config.get("plan_name", "Custom"),
            token_budget=TokenBudget(
                limit=token_budget_data.get("limit", -1),
                used=0,
                period=token_budget_data.get("period", "monthly"),
                enforcement=EnforcementMode(token_budget_data.get("enforcement", "soft")),
            ),
            features=config.get("features", {}),
            rate_limits=config.get("rate_limits", {}),
            source="local",
        )
    
    def list_apps(self) -> Set[str]:
        """List all apps with entitlements (cached or local)."""
        self._ensure_config_loaded()
        return set(self._cache.keys()) | set(self._local_config.keys()) - {"*"}
    
    def clear_cache(self, app_id: Optional[str] = None) -> None:
        """Clear platform cache (for testing)."""
        if app_id:
            self._cache.pop(app_id, None)
        else:
            self._cache.clear()


# Global instance
_manager: Optional[EntitlementsManager] = None


def get_entitlements_manager() -> EntitlementsManager:
    """Get the global entitlements manager."""
    global _manager
    if _manager is None:
        _manager = EntitlementsManager()
    return _manager


def get_entitlements(app_id: str) -> Entitlements:
    """Shortcut to get entitlements for an app."""
    return get_entitlements_manager().get(app_id)
