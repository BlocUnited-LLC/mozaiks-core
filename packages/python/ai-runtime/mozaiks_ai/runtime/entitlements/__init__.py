"""
Entitlements Runtime Module
===========================
Provides runtime capability evaluation and enforcement for mozaiks workflows.

This module enables plan-agnostic capability checking:
- Workflows declare required capabilities
- Runtime checks entitlements before execution
- Tools can be gated by capabilities
- Self-hosters can stub entitlements locally

The capability layer abstracts subscription plans:
    Plan → Capabilities → Workflows/Tools/Artifacts/Limits

Core only sees capabilities (e.g., "cap.workflow.analytics"), not plans.
Platform resolves plan → capabilities before passing entitlements to runtime.

Security Features:
- Signature verification for entitlement manifests
- Audit logging for all capability checks
- Tenant isolation validation
- Anomaly detection for suspicious patterns

Exports:
    - CapabilityEvaluator: Evaluates capabilities against entitlements
    - EntitlementLoader: Loads entitlement manifests
    - CapabilityEnforcer: Enforcement points for workflows/tools
    - SecureEntitlementContext: Security-hardened entitlement wrapper
    - has_capability: Simple capability check function
    - require_capability: Decorator for capability-gated functions
"""

from .evaluator import (
    CapabilityEvaluator,
    has_capability,
    has_all_capabilities,
    has_any_capability,
    get_capability_limit,
)
from .loader import (
    EntitlementLoader,
    EntitlementManifest,
    load_entitlements,
)
from .enforcer import (
    CapabilityEnforcer,
    CapabilityDeniedError,
    LimitExceededError,
    require_capability,
    require_any_capability,
)
from .security import (
    SecureEntitlementContext,
    EntitlementSigner,
    EntitlementSignatureError,
    SecurityAuditLogger,
    TenantIsolationValidator,
    TenantIsolationError,
    AnomalyDetector,
    get_entitlement_signer,
)

__all__ = [
    # Evaluator
    "CapabilityEvaluator",
    "has_capability",
    "has_all_capabilities",
    "has_any_capability",
    "get_capability_limit",
    # Loader
    "EntitlementLoader",
    "EntitlementManifest",
    "load_entitlements",
    # Enforcer
    "CapabilityEnforcer",
    "CapabilityDeniedError",
    "LimitExceededError",
    "require_capability",
    "require_any_capability",
    # Security
    "SecureEntitlementContext",
    "EntitlementSigner",
    "EntitlementSignatureError",
    "SecurityAuditLogger",
    "TenantIsolationValidator",
    "TenantIsolationError",
    "AnomalyDetector",
    "get_entitlement_signer",
]
