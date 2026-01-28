# backend/app/connectors/__init__.py
"""
Connector interfaces and implementations for platform services.

USAGE IN PLUGINS:
    from app.connectors import get_connectors
    
    async def execute(data):
        connectors = get_connectors()
        
        # Call Payment API
        result = await connectors.payment.create_checkout(
            correlation_id=data.get("correlation_id", ""),
            user_jwt=data.get("user_jwt"),
            ...
        )
"""
from __future__ import annotations

from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from app.runtime.connector_loader import ConnectorBundle

# Singleton connector bundle (loaded once at startup)
_connectors: "ConnectorBundle | None" = None


def get_connectors() -> "ConnectorBundle":
    """Get the connector bundle (lazy-loaded singleton)."""
    global _connectors
    if _connectors is None:
        # Lazy import to avoid circular dependency
        from app.runtime.connector_loader import load_connectors
        _connectors = load_connectors()
    return _connectors


def reset_connectors() -> None:
    """Reset connectors (useful for testing)."""
    global _connectors
    _connectors = None


# Re-export key types for convenience (lazy to avoid circular import)
def __getattr__(name: str):
    if name == "PaymentConnector":
        from app.connectors.base import PaymentConnector
        return PaymentConnector
    if name == "ConnectorBundle":
        from app.runtime.connector_loader import ConnectorBundle
        return ConnectorBundle
    raise AttributeError(f"module {__name__!r} has no attribute {name!r}")


__all__ = [
    "get_connectors",
    "reset_connectors",
    "ConnectorBundle",
    "PaymentConnector",
]
