"""Client credentials token acquisition for S2S calls.

This module lets MozaiksCore obtain a Keycloak (or any OIDC provider) access token
via the OAuth2 client-credentials flow, using the same OIDC discovery configuration
as the JWT validator.

Intended use:
- Core -> Platform internal endpoints (usage events)
- Platform -> Core internal endpoints (client credentials token on the Platform side)

MozaiksCore remains a JWT consumer; this module does not mint/issue JWTs.
It requests an access token from the configured IdP.
"""

from __future__ import annotations

import asyncio
import time
from dataclasses import dataclass
from typing import Optional

import httpx

from mozaiks_ai.runtime.auth.discovery import get_discovery_client
from logs.logging_config import get_core_logger

logger = get_core_logger("auth.client_credentials")


@dataclass
class CachedToken:
    access_token: str
    expires_at_epoch: float

    def is_valid(self, skew_seconds: int = 30) -> bool:
        return bool(self.access_token) and time.time() < (self.expires_at_epoch - skew_seconds)


class ClientCredentialsTokenProvider:
    """Fetch and cache an access token using client credentials."""

    def __init__(
        self,
        client_id: str,
        client_secret: str,
        scope: Optional[str] = None,
        timeout_seconds: float = 10.0,
    ):
        self._client_id = (client_id or "").strip()
        self._client_secret = (client_secret or "").strip()
        self._scope = (scope or "").strip() or None
        self._timeout_seconds = timeout_seconds

        self._lock = asyncio.Lock()
        self._cache: Optional[CachedToken] = None

    def is_configured(self) -> bool:
        return bool(self._client_id and self._client_secret)

    async def get_access_token(self) -> str:
        if not self.is_configured():
            raise RuntimeError("Client credentials not configured (missing client_id/client_secret)")

        if self._cache is not None and self._cache.is_valid():
            return self._cache.access_token

        return await self._refresh()

    async def _refresh(self) -> str:
        async with self._lock:
            if self._cache is not None and self._cache.is_valid():
                return self._cache.access_token

            discovery = await get_discovery_client().get_discovery()
            token_endpoint = discovery.document.get("token_endpoint")

            if not token_endpoint:
                issuer = discovery.issuer
                if issuer:
                    token_endpoint = issuer.rstrip("/") + "/protocol/openid-connect/token"

            if not token_endpoint:
                raise RuntimeError("OIDC discovery did not provide token_endpoint")

            data = {
                "grant_type": "client_credentials",
                "client_id": self._client_id,
                "client_secret": self._client_secret,
            }
            if self._scope:
                data["scope"] = self._scope

            logger.info(f"Requesting client-credentials token from {token_endpoint}")

            async with httpx.AsyncClient(timeout=self._timeout_seconds) as client:
                resp = await client.post(token_endpoint, data=data)

            if resp.status_code != 200:
                raise RuntimeError(f"Token endpoint returned {resp.status_code}: {resp.text[:300]}")

            body = resp.json()
            access_token = body.get("access_token")
            expires_in = body.get("expires_in")

            if not access_token:
                raise RuntimeError("Token endpoint response missing access_token")

            # Default to 5 minutes if expires_in absent.
            try:
                expires_in_seconds = int(expires_in) if expires_in is not None else 300
            except (TypeError, ValueError):
                expires_in_seconds = 300

            self._cache = CachedToken(
                access_token=access_token,
                expires_at_epoch=time.time() + expires_in_seconds,
            )

            return access_token
