"""
Capability Enforcer
===================
Runtime enforcement points for capability-gated workflows and tools.

Enforcement happens at:
1. Workflow start - deny if required capability missing
2. Tool execution - deny if required capability missing
3. Data access - enforce scopes (tenant/app/user)

UI only hides/shows; it does not enforce. This module IS the enforcement.
"""

import functools
import logging
from typing import Any, Callable, Dict, List, Optional, TypeVar, Union

from .evaluator import CapabilityEvaluator
from .loader import EntitlementManifest

logger = logging.getLogger(__name__)

F = TypeVar("F", bound=Callable[..., Any])


class CapabilityDeniedError(Exception):
    """
    Raised when a capability check fails.
    
    Attributes:
        capability_id: The capability that was required
        available_capabilities: What capabilities are available
        context: Additional context about the denial
    """
    
    def __init__(
        self,
        capability_id: str,
        message: Optional[str] = None,
        available_capabilities: Optional[List[str]] = None,
        context: Optional[Dict[str, Any]] = None
    ):
        self.capability_id = capability_id
        self.available_capabilities = available_capabilities or []
        self.context = context or {}
        
        default_message = f"Capability denied: {capability_id}"
        super().__init__(message or default_message)


class LimitExceededError(CapabilityDeniedError):
    """
    Raised when a capability limit is exceeded.
    
    Attributes:
        limit_id: The limit that was exceeded
        limit_value: The configured limit
        current_usage: Current usage value
    """
    
    def __init__(
        self,
        limit_id: str,
        limit_value: Union[int, float],
        current_usage: Union[int, float],
        message: Optional[str] = None
    ):
        self.limit_id = limit_id
        self.limit_value = limit_value
        self.current_usage = current_usage
        
        default_message = (
            f"Limit exceeded: {limit_id} "
            f"(limit={limit_value}, usage={current_usage})"
        )
        super().__init__(
            capability_id=limit_id,
            message=message or default_message,
            context={
                "limit_value": limit_value,
                "current_usage": current_usage,
            }
        )


class CapabilityEnforcer:
    """
    Centralized enforcement for capability checks.
    
    Usage:
        enforcer = CapabilityEnforcer(entitlements)
        
        # Check before workflow start
        enforcer.require_workflow("cap.workflow.analytics")
        
        # Check before tool execution
        enforcer.require_tool("cap.tool.generate_report")
        
        # Check limit
        enforcer.require_within_limit("cap.limit.tokens_monthly", current_tokens)
    """
    
    def __init__(
        self,
        entitlements: Union[Dict[str, Any], EntitlementManifest],
        strict: bool = True
    ):
        """
        Initialize enforcer with entitlements.
        
        Args:
            entitlements: Entitlement manifest (dict or EntitlementManifest)
            strict: If True, raise on denial. If False, log warning and allow.
        """
        if isinstance(entitlements, EntitlementManifest):
            self._manifest = entitlements
            self._evaluator = CapabilityEvaluator(entitlements.to_dict())
        else:
            self._manifest = None
            self._evaluator = CapabilityEvaluator(entitlements)
        
        self.strict = strict
    
    def require(self, capability_id: str, context: Optional[Dict[str, Any]] = None) -> bool:
        """
        Require a capability, raising if not present.
        
        Args:
            capability_id: Capability ID to check
            context: Additional context for logging/error
            
        Returns:
            True if allowed
            
        Raises:
            CapabilityDeniedError: If capability missing and strict=True
        """
        if self._evaluator.has(capability_id):
            logger.debug(f"[CAPABILITY] Allowed: {capability_id}")
            return True
        
        logger.warning(
            f"[CAPABILITY] Denied: {capability_id} "
            f"(available: {self._evaluator.all_capabilities})"
        )
        
        if self.strict:
            raise CapabilityDeniedError(
                capability_id=capability_id,
                available_capabilities=self._evaluator.all_capabilities,
                context=context or {}
            )
        
        return False
    
    def require_all(
        self,
        capability_ids: List[str],
        context: Optional[Dict[str, Any]] = None
    ) -> bool:
        """
        Require ALL capabilities.
        
        Args:
            capability_ids: List of capability IDs
            context: Additional context
            
        Returns:
            True if all allowed
            
        Raises:
            CapabilityDeniedError: If any missing and strict=True
        """
        missing = [cap for cap in capability_ids if not self._evaluator.has(cap)]
        
        if not missing:
            return True
        
        logger.warning(
            f"[CAPABILITY] Denied (missing): {missing} "
            f"(required: {capability_ids})"
        )
        
        if self.strict:
            raise CapabilityDeniedError(
                capability_id=missing[0],
                message=f"Missing capabilities: {missing}",
                available_capabilities=self._evaluator.all_capabilities,
                context=context or {}
            )
        
        return False
    
    def require_any(
        self,
        capability_ids: List[str],
        context: Optional[Dict[str, Any]] = None
    ) -> bool:
        """
        Require ANY capability (at least one).
        
        Args:
            capability_ids: List of capability IDs
            context: Additional context
            
        Returns:
            True if at least one allowed
            
        Raises:
            CapabilityDeniedError: If none present and strict=True
        """
        if self._evaluator.has_any(capability_ids):
            return True
        
        logger.warning(
            f"[CAPABILITY] Denied (need one of): {capability_ids}"
        )
        
        if self.strict:
            raise CapabilityDeniedError(
                capability_id=capability_ids[0],
                message=f"Requires one of: {capability_ids}",
                available_capabilities=self._evaluator.all_capabilities,
                context=context or {}
            )
        
        return False
    
    def require_within_limit(
        self,
        limit_id: str,
        current_usage: Union[int, float]
    ) -> bool:
        """
        Require usage is within limit.
        
        Args:
            limit_id: Limit ID
            current_usage: Current usage value
            
        Returns:
            True if within limit
            
        Raises:
            LimitExceededError: If over limit and strict=True
        """
        limit_value = self._evaluator.get_limit(limit_id)
        
        # No limit = unlimited
        if limit_value is None:
            return True
        
        if current_usage < limit_value:
            return True
        
        logger.warning(
            f"[CAPABILITY] Limit exceeded: {limit_id} "
            f"(limit={limit_value}, usage={current_usage})"
        )
        
        if self.strict:
            raise LimitExceededError(
                limit_id=limit_id,
                limit_value=limit_value,
                current_usage=current_usage
            )
        
        return False
    
    # Convenience methods with semantic names
    def require_workflow(self, workflow_capability: str) -> bool:
        """Require capability for workflow execution."""
        return self.require(workflow_capability, context={"type": "workflow"})
    
    def require_tool(self, tool_capability: str) -> bool:
        """Require capability for tool execution."""
        return self.require(tool_capability, context={"type": "tool"})
    
    def require_artifact(self, artifact_capability: str) -> bool:
        """Require capability for artifact access."""
        return self.require(artifact_capability, context={"type": "artifact"})


# ============================================================================
# Decorators for capability-gated functions
# ============================================================================

def require_capability(
    capability_id: str,
    entitlements_arg: str = "entitlements"
) -> Callable[[F], F]:
    """
    Decorator to require a capability for function execution.
    
    The decorated function must receive entitlements via the specified argument.
    
    Usage:
        @require_capability("cap.workflow.analytics")
        async def run_analytics(entitlements: dict, data: dict):
            # Only runs if cap.workflow.analytics is present
            ...
    
    Args:
        capability_id: Required capability ID
        entitlements_arg: Name of the entitlements argument
        
    Returns:
        Decorated function
    """
    def decorator(func: F) -> F:
        @functools.wraps(func)
        def wrapper(*args, **kwargs):
            # Try to get entitlements from kwargs
            entitlements = kwargs.get(entitlements_arg)
            
            if entitlements is None:
                logger.warning(
                    f"[CAPABILITY] No entitlements provided to {func.__name__}, "
                    f"denying by default"
                )
                raise CapabilityDeniedError(
                    capability_id=capability_id,
                    message=f"No entitlements provided for capability check"
                )
            
            enforcer = CapabilityEnforcer(entitlements)
            enforcer.require(capability_id)
            
            return func(*args, **kwargs)
        
        @functools.wraps(func)
        async def async_wrapper(*args, **kwargs):
            entitlements = kwargs.get(entitlements_arg)
            
            if entitlements is None:
                logger.warning(
                    f"[CAPABILITY] No entitlements provided to {func.__name__}, "
                    f"denying by default"
                )
                raise CapabilityDeniedError(
                    capability_id=capability_id,
                    message=f"No entitlements provided for capability check"
                )
            
            enforcer = CapabilityEnforcer(entitlements)
            enforcer.require(capability_id)
            
            return await func(*args, **kwargs)
        
        # Return appropriate wrapper based on function type
        import asyncio
        if asyncio.iscoroutinefunction(func):
            return async_wrapper  # type: ignore
        return wrapper  # type: ignore
    
    return decorator


def require_any_capability(
    capability_ids: List[str],
    entitlements_arg: str = "entitlements"
) -> Callable[[F], F]:
    """
    Decorator to require any of the specified capabilities.
    
    Usage:
        @require_any_capability(["cap.workflow.basic", "cap.workflow.premium"])
        async def run_workflow(entitlements: dict, workflow_name: str):
            ...
    
    Args:
        capability_ids: List of capability IDs (any must be present)
        entitlements_arg: Name of the entitlements argument
        
    Returns:
        Decorated function
    """
    def decorator(func: F) -> F:
        @functools.wraps(func)
        def wrapper(*args, **kwargs):
            entitlements = kwargs.get(entitlements_arg)
            
            if entitlements is None:
                raise CapabilityDeniedError(
                    capability_id=capability_ids[0],
                    message=f"No entitlements provided for capability check"
                )
            
            enforcer = CapabilityEnforcer(entitlements)
            enforcer.require_any(capability_ids)
            
            return func(*args, **kwargs)
        
        @functools.wraps(func)
        async def async_wrapper(*args, **kwargs):
            entitlements = kwargs.get(entitlements_arg)
            
            if entitlements is None:
                raise CapabilityDeniedError(
                    capability_id=capability_ids[0],
                    message=f"No entitlements provided for capability check"
                )
            
            enforcer = CapabilityEnforcer(entitlements)
            enforcer.require_any(capability_ids)
            
            return await func(*args, **kwargs)
        
        import asyncio
        if asyncio.iscoroutinefunction(func):
            return async_wrapper  # type: ignore
        return wrapper  # type: ignore
    
    return decorator
