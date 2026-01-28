# backend/app/runtime/connector_loader.py
from __future__ import annotations

import os
from dataclasses import dataclass
from typing import Literal

from app.connectors.base import PaymentConnector
from app.connectors.managed import ManagedGatewayConfig, ManagedHttpClient, ManagedPaymentConnector
from app.connectors.mock import MockPaymentConnector


ConnectorMode = Literal["managed", "self_hosted"]


@dataclass(frozen=True)
class ConnectorBundle:
    """Bundle of all platform service connectors."""
    mode: ConnectorMode
    payment: PaymentConnector


def _env_truthy(value: str | None) -> bool:
    if value is None:
        return False
    return value.strip().lower() in {"1", "true", "yes", "y", "on"}


def is_managed_mode() -> bool:
    """Managed mode is enabled by env vars (not a security boundary)."""
    hosting_mode = (os.getenv("MOZAIKS_HOSTING_MODE") or "").strip().lower()
    hosted = hosting_mode == "hosted"
    # Compatibility alias: MOZAIKS_MANAGED=true implies hosted operation.
    if not hosted and not _env_truthy(os.getenv("MOZAIKS_MANAGED")):
        return False

    base_url = (os.getenv("MOZAIKS_GATEWAY_BASE_URL") or "").strip()
    return bool(base_url)


def load_connectors() -> ConnectorBundle:
    """Return managed HTTP connectors or self-hosted mocks."""
    if is_managed_mode():
        config = ManagedGatewayConfig(
            base_url=(os.getenv("MOZAIKS_GATEWAY_BASE_URL") or "").strip(),
            api_key=(os.getenv("MOZAIKS_GATEWAY_API_KEY") or "").strip() or None,
        )
        http = ManagedHttpClient(config)
        return ConnectorBundle(
            mode="managed",
            payment=ManagedPaymentConnector(http),
        )

    return ConnectorBundle(
        mode="self_hosted",
        payment=MockPaymentConnector(),
    )
