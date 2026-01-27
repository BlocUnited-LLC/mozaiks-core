# core/billing/__init__.py
"""
Billing Module for MozaiksCore

Architecture:
- Entitlements: What an app can do (features, limits, quotas)
- Usage: What an app has done (tokens used, API calls)

Self-hosters:
- Configure entitlements via YAML file (entitlements.yaml)
- Usage is logged locally (no Platform connection needed)
- Everything works standalone

Platform users:
- Entitlements pushed via /api/v1/entitlements/{app_id}/sync
- Usage reported to Platform for billing

Core NEVER pulls from Platform. Push-only model ensures Core works
100% standalone without any Platform dependency.

Usage (v2 - Recommended):
    from core.billing import get_entitlements
    
    ent = get_entitlements(app_id)
    if ent.has_feature("code_execution"):
        # allow
    
    allowed, msg = ent.check_token_budget(tokens_needed=500)
    if not allowed:
        raise QuotaExceeded(msg)

Contract Version: 2.0
"""

# NEW: Primary API (v2) - Simple, push-only
from .entitlements import (
    Entitlements,
    EntitlementsManager,
    TokenBudget,
    EnforcementMode,
    get_entitlements,
    get_entitlements_manager,
)

# Usage reporting (optional - only if Platform URL configured)
from .usage_reporter import UsageReporter, UsageEvent

# Sync handler (Platform -> Core push)
from .sync import (
    EntitlementSyncHandler,
    EntitlementSyncRequest,
    EntitlementSyncResult,
    get_sync_handler,
    validate_service_key,
)

# LEGACY: Payment provider API (v1) - kept for backwards compatibility
from .base import (
    IPaymentProvider,
    SubscriptionStatus,
    CheckoutRequest,
    CheckoutResult,
    CancelResult,
)
from .noop_provider import NoOpPaymentProvider
from .manual_provider import ManualEntitlementsProvider
from .platform_provider import PlatformPaymentProvider
from .factory import get_payment_provider, configure_provider

__all__ = [
    # NEW: Primary API (v2)
    "Entitlements",
    "EntitlementsManager",
    "TokenBudget",
    "EnforcementMode",
    "get_entitlements",
    "get_entitlements_manager",
    
    # Usage reporting
    "UsageReporter",
    "UsageEvent",
    
    # Sync (Platform -> Core)
    "EntitlementSyncHandler",
    "EntitlementSyncRequest",
    "EntitlementSyncResult",
    "get_sync_handler",
    "validate_service_key",
    
    # LEGACY: Payment provider API (v1)
    "IPaymentProvider",
    "SubscriptionStatus",
    "CheckoutRequest",
    "CheckoutResult",
    "CancelResult",
    "NoOpPaymentProvider",
    "ManualEntitlementsProvider",
    "PlatformPaymentProvider",
    "get_payment_provider",
    "configure_provider",
]

__version__ = "2.0.0"
__contract_version__ = "2.0"
