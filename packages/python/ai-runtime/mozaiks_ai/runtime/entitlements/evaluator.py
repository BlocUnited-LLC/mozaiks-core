"""
Capability Evaluator
====================
Plan-agnostic capability evaluation against entitlement manifests.

Core never sees "plans" - only capabilities. Platform resolves plan → capabilities
before passing entitlements to the runtime.
"""

import logging
from typing import Any, Dict, List, Optional, Union

logger = logging.getLogger(__name__)


class CapabilityEvaluator:
    """
    Evaluates capabilities against an entitlement manifest.
    
    Supports both entitlement formats:
    - `metadata.capabilities` (preferred)
    - Top-level `capabilities` array (fallback)
    
    Usage:
        evaluator = CapabilityEvaluator(entitlements)
        
        if evaluator.has("cap.workflow.analytics"):
            # Allow analytics workflow
            
        limit = evaluator.get_limit("cap.limit.tokens_monthly")
        if limit and usage < limit:
            # Allow token usage
    """
    
    def __init__(self, entitlements: Dict[str, Any]):
        """
        Initialize evaluator with entitlement manifest.
        
        Args:
            entitlements: Entitlement manifest dict (from JSON or API)
        """
        self.entitlements = entitlements or {}
        self._capabilities = self._extract_capabilities()
        self._limits = self._extract_limits()
    
    def _extract_capabilities(self) -> set:
        """Extract capability IDs from entitlements (supports both formats)."""
        caps = set()
        
        # Try metadata.capabilities first (preferred)
        metadata = self.entitlements.get("metadata", {})
        if isinstance(metadata, dict):
            metadata_caps = metadata.get("capabilities", [])
            if isinstance(metadata_caps, list):
                caps.update(metadata_caps)
        
        # Also check top-level capabilities (fallback)
        top_level_caps = self.entitlements.get("capabilities", [])
        if isinstance(top_level_caps, list):
            caps.update(top_level_caps)
        
        return caps
    
    def _extract_limits(self) -> Dict[str, Union[int, float]]:
        """Extract capability limits from entitlements."""
        limits = {}
        
        # Check metadata.limits
        metadata = self.entitlements.get("metadata", {})
        if isinstance(metadata, dict):
            metadata_limits = metadata.get("limits", {})
            if isinstance(metadata_limits, dict):
                limits.update(metadata_limits)
        
        # Check top-level limits
        top_level_limits = self.entitlements.get("limits", {})
        if isinstance(top_level_limits, dict):
            limits.update(top_level_limits)
        
        return limits
    
    def has(self, capability_id: str) -> bool:
        """
        Check if a capability is present.
        
        Args:
            capability_id: Capability ID (e.g., "cap.workflow.analytics")
            
        Returns:
            True if capability is present
        """
        return capability_id in self._capabilities
    
    def has_all(self, capability_ids: List[str]) -> bool:
        """
        Check if ALL capabilities are present.
        
        Args:
            capability_ids: List of capability IDs
            
        Returns:
            True if all capabilities are present
        """
        return all(cap in self._capabilities for cap in capability_ids)
    
    def has_any(self, capability_ids: List[str]) -> bool:
        """
        Check if ANY capability is present.
        
        Args:
            capability_ids: List of capability IDs
            
        Returns:
            True if at least one capability is present
        """
        return any(cap in self._capabilities for cap in capability_ids)
    
    def get_limit(self, limit_id: str) -> Optional[Union[int, float]]:
        """
        Get a capability limit value.
        
        Args:
            limit_id: Limit ID (e.g., "cap.limit.tokens_monthly")
            
        Returns:
            Limit value or None if not set
        """
        return self._limits.get(limit_id)
    
    def check_limit(self, limit_id: str, current_usage: Union[int, float]) -> bool:
        """
        Check if current usage is within limit.
        
        Args:
            limit_id: Limit ID
            current_usage: Current usage value
            
        Returns:
            True if within limit (or no limit set)
        """
        limit = self.get_limit(limit_id)
        if limit is None:
            return True  # No limit = unlimited
        return current_usage < limit
    
    @property
    def all_capabilities(self) -> List[str]:
        """Get all capability IDs."""
        return list(self._capabilities)
    
    @property
    def all_limits(self) -> Dict[str, Union[int, float]]:
        """Get all limits."""
        return dict(self._limits)
    
    def __contains__(self, capability_id: str) -> bool:
        """Support 'in' operator: 'cap.x' in evaluator"""
        return self.has(capability_id)


# ============================================================================
# Module-level convenience functions
# ============================================================================

def has_capability(entitlements: Dict[str, Any], capability_id: str) -> bool:
    """
    Check if entitlements include a capability.
    
    Simple helper for one-off checks without creating an evaluator.
    
    Args:
        entitlements: Entitlement manifest dict
        capability_id: Capability ID to check
        
    Returns:
        True if capability is present
    """
    evaluator = CapabilityEvaluator(entitlements)
    return evaluator.has(capability_id)


def has_all_capabilities(entitlements: Dict[str, Any], capability_ids: List[str]) -> bool:
    """
    Check if entitlements include ALL capabilities.
    
    Args:
        entitlements: Entitlement manifest dict
        capability_ids: List of capability IDs
        
    Returns:
        True if all capabilities are present
    """
    evaluator = CapabilityEvaluator(entitlements)
    return evaluator.has_all(capability_ids)


def has_any_capability(entitlements: Dict[str, Any], capability_ids: List[str]) -> bool:
    """
    Check if entitlements include ANY capability.
    
    Args:
        entitlements: Entitlement manifest dict
        capability_ids: List of capability IDs
        
    Returns:
        True if at least one capability is present
    """
    evaluator = CapabilityEvaluator(entitlements)
    return evaluator.has_any(capability_ids)


def get_capability_limit(
    entitlements: Dict[str, Any],
    limit_id: str
) -> Optional[Union[int, float]]:
    """
    Get a capability limit from entitlements.
    
    Args:
        entitlements: Entitlement manifest dict
        limit_id: Limit ID
        
    Returns:
        Limit value or None
    """
    evaluator = CapabilityEvaluator(entitlements)
    return evaluator.get_limit(limit_id)
