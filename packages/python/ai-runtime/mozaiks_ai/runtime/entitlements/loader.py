"""
Entitlement Loader
==================
Loads entitlement manifests from files or API responses.

Supports:
- File-based manifests (for generated apps, self-hosted)
- API-injected entitlements (for SaaS platform)
- Default/stub entitlements (for development/OSS)
"""

import json
import logging
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, List, Optional, Union

logger = logging.getLogger(__name__)


@dataclass
class EntitlementManifest:
    """
    Parsed entitlement manifest with typed access.
    
    Attributes:
        app_id: Application identifier
        user_id: User identifier (optional)
        capabilities: List of capability IDs
        limits: Dict of limit_id -> value
        source: Where entitlements came from
        raw: Original manifest dict
    """
    app_id: str
    user_id: Optional[str] = None
    capabilities: List[str] = field(default_factory=list)
    limits: Dict[str, Union[int, float]] = field(default_factory=dict)
    source: str = "unknown"
    raw: Dict[str, Any] = field(default_factory=dict)
    
    def has(self, capability_id: str) -> bool:
        """Check if capability is present."""
        return capability_id in self.capabilities
    
    def has_all(self, capability_ids: List[str]) -> bool:
        """Check if all capabilities are present."""
        return all(cap in self.capabilities for cap in capability_ids)
    
    def has_any(self, capability_ids: List[str]) -> bool:
        """Check if any capability is present."""
        return any(cap in self.capabilities for cap in capability_ids)
    
    def get_limit(self, limit_id: str) -> Optional[Union[int, float]]:
        """Get a limit value."""
        return self.limits.get(limit_id)
    
    def to_dict(self) -> Dict[str, Any]:
        """Convert to dict for serialization."""
        return {
            "app_id": self.app_id,
            "user_id": self.user_id,
            "metadata": {
                "capabilities": self.capabilities,
                "limits": self.limits,
                "capability_source": self.source,
            }
        }


class EntitlementLoader:
    """
    Loads entitlement manifests from various sources.
    
    Priority order:
    1. Explicit entitlements passed at runtime (API injection)
    2. File-based manifest in app bundle (entitlement_manifest.json)
    3. Default/stub entitlements (development mode)
    
    Usage:
        loader = EntitlementLoader()
        
        # Load from file
        manifest = loader.load_from_file(app_path / "entitlement_manifest.json")
        
        # Load from dict (API response)
        manifest = loader.load_from_dict(api_response)
        
        # Load with fallback chain
        manifest = loader.load(
            app_id="myapp",
            app_path=Path("/apps/myapp"),
            runtime_entitlements=request.entitlements  # Optional override
        )
    """
    
    # Default capabilities for OSS/development (generous defaults)
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
    
    def __init__(self, default_capabilities: Optional[List[str]] = None):
        """
        Initialize loader with optional default capabilities.
        
        Args:
            default_capabilities: Override default capabilities for stub mode
        """
        self.default_capabilities = default_capabilities or self.DEFAULT_CAPABILITIES
    
    def load(
        self,
        app_id: str,
        app_path: Optional[Path] = None,
        runtime_entitlements: Optional[Dict[str, Any]] = None,
        user_id: Optional[str] = None,
    ) -> EntitlementManifest:
        """
        Load entitlements with fallback chain.
        
        Priority:
        1. runtime_entitlements (if provided)
        2. File at app_path/entitlement_manifest.json
        3. Default stub entitlements
        
        Args:
            app_id: Application identifier
            app_path: Path to app bundle (optional)
            runtime_entitlements: Entitlements from API/request (optional)
            user_id: User identifier (optional)
            
        Returns:
            EntitlementManifest
        """
        # Priority 1: Runtime entitlements (API injection)
        if runtime_entitlements:
            logger.debug(f"[ENTITLEMENTS] Using runtime entitlements for app={app_id}")
            return self.load_from_dict(
                runtime_entitlements,
                app_id=app_id,
                user_id=user_id,
                source="runtime"
            )
        
        # Priority 2: File-based manifest
        if app_path:
            manifest_path = Path(app_path) / "entitlement_manifest.json"
            if manifest_path.exists():
                logger.debug(f"[ENTITLEMENTS] Loading from file: {manifest_path}")
                return self.load_from_file(
                    manifest_path,
                    app_id=app_id,
                    user_id=user_id
                )
        
        # Priority 3: Default stub entitlements
        logger.debug(f"[ENTITLEMENTS] Using default entitlements for app={app_id}")
        return self.load_default(app_id=app_id, user_id=user_id)
    
    def load_from_file(
        self,
        path: Path,
        app_id: Optional[str] = None,
        user_id: Optional[str] = None
    ) -> EntitlementManifest:
        """
        Load entitlements from a JSON file.
        
        Args:
            path: Path to entitlement_manifest.json
            app_id: Override app_id (uses file value if not provided)
            user_id: User identifier
            
        Returns:
            EntitlementManifest
        """
        try:
            with open(path, "r", encoding="utf-8") as f:
                data = json.load(f)
            
            return self.load_from_dict(
                data,
                app_id=app_id or data.get("app_id", "unknown"),
                user_id=user_id,
                source=f"file:{path}"
            )
        except Exception as e:
            logger.warning(f"[ENTITLEMENTS] Failed to load from {path}: {e}")
            return self.load_default(app_id=app_id or "unknown", user_id=user_id)
    
    def load_from_dict(
        self,
        data: Dict[str, Any],
        app_id: Optional[str] = None,
        user_id: Optional[str] = None,
        source: str = "dict"
    ) -> EntitlementManifest:
        """
        Load entitlements from a dict (API response or parsed JSON).
        
        Supports both formats:
        - metadata.capabilities + metadata.limits
        - Top-level capabilities + limits
        
        Args:
            data: Entitlement dict
            app_id: Application identifier
            user_id: User identifier
            source: Source identifier for logging
            
        Returns:
            EntitlementManifest
        """
        # Extract capabilities (both formats)
        capabilities = []
        metadata = data.get("metadata", {})
        if isinstance(metadata, dict):
            caps = metadata.get("capabilities", [])
            if isinstance(caps, list):
                capabilities.extend(caps)
        
        top_caps = data.get("capabilities", [])
        if isinstance(top_caps, list):
            capabilities.extend(top_caps)
        
        # Dedupe
        capabilities = list(set(capabilities))
        
        # Extract limits (both formats)
        limits = {}
        if isinstance(metadata, dict):
            meta_limits = metadata.get("limits", {})
            if isinstance(meta_limits, dict):
                limits.update(meta_limits)
        
        top_limits = data.get("limits", {})
        if isinstance(top_limits, dict):
            limits.update(top_limits)
        
        return EntitlementManifest(
            app_id=app_id or data.get("app_id", "unknown"),
            user_id=user_id or data.get("user_id"),
            capabilities=capabilities,
            limits=limits,
            source=source,
            raw=data,
        )
    
    def load_default(
        self,
        app_id: str,
        user_id: Optional[str] = None
    ) -> EntitlementManifest:
        """
        Load default/stub entitlements for development/OSS.
        
        Args:
            app_id: Application identifier
            user_id: User identifier
            
        Returns:
            EntitlementManifest with default capabilities
        """
        return EntitlementManifest(
            app_id=app_id,
            user_id=user_id,
            capabilities=list(self.default_capabilities),
            limits=dict(self.DEFAULT_LIMITS),
            source="default",
            raw={},
        )


# ============================================================================
# Module-level convenience function
# ============================================================================

_default_loader: Optional[EntitlementLoader] = None


def load_entitlements(
    app_id: str,
    app_path: Optional[Path] = None,
    runtime_entitlements: Optional[Dict[str, Any]] = None,
    user_id: Optional[str] = None,
) -> EntitlementManifest:
    """
    Load entitlements using the default loader.
    
    Convenience function for simple use cases.
    
    Args:
        app_id: Application identifier
        app_path: Path to app bundle (optional)
        runtime_entitlements: Entitlements from API (optional)
        user_id: User identifier (optional)
        
    Returns:
        EntitlementManifest
    """
    global _default_loader
    if _default_loader is None:
        _default_loader = EntitlementLoader()
    
    return _default_loader.load(
        app_id=app_id,
        app_path=app_path,
        runtime_entitlements=runtime_entitlements,
        user_id=user_id,
    )
