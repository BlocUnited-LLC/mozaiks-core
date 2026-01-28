# core/billing/sync.py
"""
Entitlement Sync Handler - Receives manifest updates from Platform.

This module handles the POST /api/v1/entitlements/{app_id}/sync endpoint
that Platform calls when subscriptions change.

Auth is enforced at the API routing layer via Keycloak app-only JWTs with role internal_service.
"""

import logging
from dataclasses import dataclass
from datetime import datetime
from typing import Optional, Dict, Any, List, Set

logger = logging.getLogger("mozaiks_core.billing.sync")

@dataclass
class EntitlementSyncRequest:
    """
    Request from Platform to sync entitlements.
    
    This is the contract for POST /api/v1/entitlements/{app_id}/sync
    """
    version: str  # Contract version, e.g., "1.0"
    app_id: str
    tenant_id: Optional[str] = None  # For multi-tenant Platform
    
    # Plan information
    plan_id: Optional[str] = None
    plan_name: Optional[str] = None
    plan_tier: str = "free"
    billing_period: str = "monthly"
    expires_at: Optional[datetime] = None
    
    # Token budget
    token_limit: int = -1  # -1 for unlimited
    token_used: int = 0
    token_period: str = "monthly"
    token_enforcement: str = "soft"  # hard, soft, warn, none
    
    # Features
    features: Dict[str, bool] = None
    
    # Rate limits
    rate_limits: Dict[str, int] = None
    
    # Correlation
    correlation_id: Optional[str] = None
    
    def __post_init__(self):
        if self.features is None:
            self.features = {}
        if self.rate_limits is None:
            self.rate_limits = {}
    
    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "EntitlementSyncRequest":
        """Parse from request JSON."""
        plan = data.get("plan", {})
        token_budget = data.get("token_budget", {})
        total_tokens = token_budget.get("total_tokens", {})
        
        # Parse expires_at
        expires_at = None
        if plan.get("expires_at"):
            try:
                expires_str = plan["expires_at"]
                if isinstance(expires_str, str):
                    expires_at = datetime.fromisoformat(expires_str.replace("Z", "+00:00"))
            except (ValueError, TypeError):
                pass
        
        return cls(
            version=data.get("version", "1.0"),
            app_id=data.get("app_id", ""),
            tenant_id=data.get("tenant_id"),
            plan_id=plan.get("id"),
            plan_name=plan.get("name"),
            plan_tier=plan.get("tier", "free"),
            billing_period=plan.get("billing_period", "monthly"),
            expires_at=expires_at,
            token_limit=total_tokens.get("limit", -1),
            token_used=total_tokens.get("used", 0),
            token_period=token_budget.get("period", "monthly"),
            token_enforcement=total_tokens.get("enforcement", "soft"),
            features=data.get("features", {}),
            rate_limits=data.get("rate_limits", {}),
            correlation_id=data.get("correlation_id"),
        )


@dataclass
class EntitlementSyncResult:
    """Result of entitlement sync operation."""
    status: str  # "synced", "error"
    app_id: str
    synced_at: datetime
    previous_tier: Optional[str] = None
    new_tier: str = "free"
    error: Optional[str] = None
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert to response JSON."""
        return {
            "status": self.status,
            "app_id": self.app_id,
            "synced_at": self.synced_at.isoformat() + "Z",
            "previous_tier": self.previous_tier,
            "new_tier": self.new_tier,
            "error": self.error,
        }


class EntitlementSyncHandler:
    """
    Handler for entitlement sync operations.
    
    Stores synced entitlements and provides lookup.
    Uses in-memory storage by default, can be extended with persistence.
    """
    
    def __init__(self):
        # In-memory storage: app_id -> entitlement data
        self._entitlements: Dict[str, Dict[str, Any]] = {}
        self._sync_history: List[Dict[str, Any]] = []
    
    async def handle_sync(
        self,
        request: EntitlementSyncRequest,
    ) -> EntitlementSyncResult:
        """
        Handle entitlement sync from Platform.
        
        Args:
            request: Sync request from Platform
            
        Returns:
            Sync result
        """
        app_id = request.app_id
        
        if not app_id:
            return EntitlementSyncResult(
                status="error",
                app_id="",
                synced_at=datetime.utcnow(),
                error="app_id is required",
            )
        
        # Get previous state
        previous = self._entitlements.get(app_id)
        previous_tier = previous.get("plan_tier") if previous else None
        
        # Build new entitlement state
        new_state = {
            "version": request.version,
            "app_id": request.app_id,
            "tenant_id": request.tenant_id,
            "plan": {
                "id": request.plan_id,
                "name": request.plan_name,
                "tier": request.plan_tier,
                "billing_period": request.billing_period,
                "expires_at": request.expires_at.isoformat() if request.expires_at else None,
            },
            "token_budget": {
                "period": request.token_period,
                "total_tokens": {
                    "limit": request.token_limit,
                    "used": request.token_used,
                    "enforcement": request.token_enforcement,
                },
            },
            "features": request.features,
            "rate_limits": request.rate_limits,
            "synced_at": datetime.utcnow().isoformat(),
        }
        
        # Store in sync handler
        self._entitlements[app_id] = new_state
        
        # Also update the EntitlementsManager (v2 API)
        try:
            from .entitlements import get_entitlements_manager
            mgr = get_entitlements_manager()
            mgr.update_from_sync(app_id, new_state)
        except ImportError:
            pass  # EntitlementsManager not available
        
        # Log history
        self._sync_history.append({
            "app_id": app_id,
            "previous_tier": previous_tier,
            "new_tier": request.plan_tier,
            "synced_at": datetime.utcnow().isoformat(),
            "correlation_id": request.correlation_id,
        })
        
        # Trim history
        if len(self._sync_history) > 1000:
            self._sync_history = self._sync_history[-500:]
        
        logger.info(
            "Entitlement synced: app=%s tier=%s->%s tokens=%d",
            app_id, previous_tier, request.plan_tier, request.token_limit
        )
        
        return EntitlementSyncResult(
            status="synced",
            app_id=app_id,
            synced_at=datetime.utcnow(),
            previous_tier=previous_tier,
            new_tier=request.plan_tier,
        )
    
    def get_entitlements(self, app_id: str) -> Optional[Dict[str, Any]]:
        """
        Get current entitlements for an app.
        
        Args:
            app_id: Application identifier
            
        Returns:
            Entitlement data or None if not found
        """
        return self._entitlements.get(app_id)
    
    def get_token_limit(self, app_id: str) -> int:
        """Get token limit for app (-1 if unlimited or not found)."""
        ent = self._entitlements.get(app_id)
        if not ent:
            return -1
        return ent.get("token_budget", {}).get("total_tokens", {}).get("limit", -1)
    
    def has_feature(self, app_id: str, feature: str) -> bool:
        """Check if app has a feature enabled."""
        ent = self._entitlements.get(app_id)
        if not ent:
            return True  # No config = all features enabled (OSS mode)
        return ent.get("features", {}).get(feature, True)
    
    def get_tier(self, app_id: str) -> str:
        """Get tier for app."""
        ent = self._entitlements.get(app_id)
        if not ent:
            return "unlimited"  # No config = OSS mode
        return ent.get("plan", {}).get("tier", "free")
    
    def list_apps(self) -> List[str]:
        """List all apps with entitlements."""
        return list(self._entitlements.keys())
    
    def get_sync_history(self, limit: int = 100) -> List[Dict[str, Any]]:
        """Get recent sync history."""
        return self._sync_history[-limit:]


# Global handler instance
_sync_handler: Optional[EntitlementSyncHandler] = None


def get_sync_handler() -> EntitlementSyncHandler:
    """Get the global sync handler instance."""
    global _sync_handler
    if _sync_handler is None:
        _sync_handler = EntitlementSyncHandler()
    return _sync_handler
