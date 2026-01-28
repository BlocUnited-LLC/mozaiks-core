# core/billing/factory.py
"""
Payment Provider Factory - Creates the appropriate provider based on configuration.

Determines which payment provider to use based on environment configuration.
"""

import logging
import os
from typing import Optional

from .base import IPaymentProvider
from .noop_provider import NoOpPaymentProvider
from .manual_provider import ManualEntitlementsProvider
from .platform_provider import PlatformPaymentProvider

logger = logging.getLogger("mozaiks_core.billing.factory")

# Global configured provider
_provider: Optional[IPaymentProvider] = None


def get_payment_provider() -> IPaymentProvider:
    """
    Get the configured payment provider.
    
    Provider selection (in order of precedence):
    1. Explicitly configured via configure_provider()
    2. MOZAIKS_BILLING_PROVIDER environment variable:
       - "platform": Use Platform Payment.API
       - "manual": Use local YAML config
       - "noop": No billing (everything free)
    3. Auto-detect based on environment:
       - If MOZAIKS_PLATFORM_URL is set: Use Platform
       - If MOZAIKS_ENTITLEMENTS_FILE is set: Use Manual
       - Otherwise: Use NoOp
    
    Returns:
        Configured payment provider instance
    """
    global _provider
    
    if _provider is not None:
        return _provider
    
    # Check explicit provider setting
    provider_type = os.getenv("MOZAIKS_BILLING_PROVIDER", "").lower()
    
    if provider_type == "platform":
        _provider = _create_platform_provider()
    elif provider_type == "manual":
        _provider = _create_manual_provider()
    elif provider_type == "noop":
        _provider = NoOpPaymentProvider()
    else:
        # Auto-detect
        _provider = _auto_detect_provider()
    
    logger.info("Using payment provider: %s", _provider.provider_id)
    return _provider


def configure_provider(provider: IPaymentProvider) -> None:
    """
    Explicitly configure the payment provider.
    
    Use this for testing or custom provider implementations.
    
    Args:
        provider: Provider instance to use
    """
    global _provider
    _provider = provider
    logger.info("Payment provider configured: %s", provider.provider_id)


def reset_provider() -> None:
    """
    Reset the provider to force re-detection.
    
    Primarily for testing.
    """
    global _provider
    _provider = None


def _auto_detect_provider() -> IPaymentProvider:
    """Auto-detect the appropriate provider."""
    
    # Check for Platform configuration
    platform_url = os.getenv("MOZAIKS_PLATFORM_URL")
    platform_client_id = os.getenv("MOZAIKS_PLATFORM_CLIENT_ID")
    platform_client_secret = os.getenv("MOZAIKS_PLATFORM_CLIENT_SECRET")
    
    if platform_url and platform_client_id and platform_client_secret:
        logger.debug("Auto-detected Platform provider (URL and client credentials present)")
        return _create_platform_provider()
    
    # Check for manual entitlements file
    entitlements_file = os.getenv("MOZAIKS_ENTITLEMENTS_FILE")
    if entitlements_file:
        logger.debug("Auto-detected Manual provider (entitlements file specified)")
        return _create_manual_provider()
    
    # Check for local entitlements.yaml
    local_paths = [
        "entitlements.yaml",
        "config/entitlements.yaml",
        "/etc/mozaiks/entitlements.yaml",
    ]
    
    for path in local_paths:
        if os.path.exists(path):
            logger.debug("Auto-detected Manual provider (found %s)", path)
            return ManualEntitlementsProvider(config_path=path)
    
    # Default to NoOp
    logger.debug("Auto-detected NoOp provider (no configuration found)")
    return NoOpPaymentProvider()


def _create_platform_provider() -> PlatformPaymentProvider:
    """Create Platform provider with environment config."""
    return PlatformPaymentProvider(
        platform_url=os.getenv("MOZAIKS_PLATFORM_URL"),
        client_id=os.getenv("MOZAIKS_PLATFORM_CLIENT_ID"),
        client_secret=os.getenv("MOZAIKS_PLATFORM_CLIENT_SECRET"),
        token_scope=os.getenv("MOZAIKS_PLATFORM_TOKEN_SCOPE"),
    )


def _create_manual_provider() -> ManualEntitlementsProvider:
    """Create Manual provider with environment config."""
    config_path = os.getenv("MOZAIKS_ENTITLEMENTS_FILE", "entitlements.yaml")
    default_tier = os.getenv("MOZAIKS_DEFAULT_TIER", "free")
    
    return ManualEntitlementsProvider(
        config_path=config_path,
        default_tier=default_tier,
    )
