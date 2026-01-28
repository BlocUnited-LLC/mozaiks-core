from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Dict, Optional
import asyncio
import logging
import time

import httpx
from jose import JWTError, jwk, jwt

from config import Settings

logger = logging.getLogger("plugin_host.auth")


class AuthError(Exception):
    def __init__(self, message: str, status_code: int = 401) -> None:
        super().__init__(message)
        self.status_code = status_code


@dataclass(frozen=True)
class UserContext:
    user_id: str
    username: Optional[str]
    roles: list[str]
    is_superadmin: bool
    app_id: Optional[str]
    claims: Dict[str, Any]


class JWKSClient:
    def __init__(self, jwks_url: str, cache_ttl_seconds: int = 300) -> None:
        self._jwks_url = jwks_url
        self._cache_ttl_seconds = cache_ttl_seconds
        self._lock = asyncio.Lock()
        self._jwks: Optional[Dict[str, Any]] = None
        self._fetched_at = 0.0

    async def get_jwks(self) -> Dict[str, Any]:
        if self._jwks and (time.time() - self._fetched_at) < self._cache_ttl_seconds:
            return self._jwks
        async with self._lock:
            if self._jwks and (time.time() - self._fetched_at) < self._cache_ttl_seconds:
                return self._jwks
            async with httpx.AsyncClient(timeout=10.0) as client:
                response = await client.get(self._jwks_url)
                response.raise_for_status()
                data = response.json()
            if not isinstance(data, dict) or "keys" not in data:
                raise AuthError("Invalid JWKS response", status_code=401)
            self._jwks = data
            self._fetched_at = time.time()
            return data


def get_bearer_token(authorization_header: Optional[str]) -> str:
    if not authorization_header:
        raise AuthError("Missing Authorization header")
    parts = authorization_header.strip().split()
    if len(parts) != 2 or parts[0].lower() != "bearer":
        raise AuthError("Invalid Authorization header")
    return parts[1]


async def decode_token(token: str, settings: Settings, jwks_client: JWKSClient) -> Dict[str, Any]:
    try:
        header = jwt.get_unverified_header(token)
    except JWTError as exc:
        raise AuthError(f"Invalid token header: {exc}") from exc

    alg = header.get("alg")
    if alg and alg not in settings.jwt_algorithms:
        raise AuthError("Token algorithm not allowed")

    jwks = await jwks_client.get_jwks()
    key_data = _select_jwk(jwks, header.get("kid"))
    key = jwk.construct(key_data)

    options = {
        "verify_aud": bool(settings.jwt_audience),
        "verify_iss": bool(settings.jwt_issuer),
    }
    kwargs: Dict[str, Any] = {
        "key": key,
        "algorithms": list(settings.jwt_algorithms),
        "options": options,
    }
    if settings.jwt_audience:
        kwargs["audience"] = settings.jwt_audience
    if settings.jwt_issuer:
        kwargs["issuer"] = settings.jwt_issuer

    try:
        return jwt.decode(token, **kwargs)
    except JWTError as exc:
        raise AuthError(f"Token validation failed: {exc}") from exc


def extract_identity(claims: Dict[str, Any]) -> UserContext:
    user_id = claims.get("user_id") or claims.get("sub") or claims.get("uid")
    if not user_id:
        raise AuthError("Token missing user_id")

    username = claims.get("username") or claims.get("preferred_username") or claims.get("email")
    roles = _normalize_roles(claims.get("roles"))
    if not roles:
        realm_access = claims.get("realm_access")
        if isinstance(realm_access, dict):
            roles = _normalize_roles(realm_access.get("roles"))

    is_superadmin = bool(claims.get("is_superadmin")) or ("superadmin" in roles)
    app_id = claims.get("app_id")

    return UserContext(
        user_id=str(user_id),
        username=str(username) if username else None,
        roles=roles,
        is_superadmin=is_superadmin,
        app_id=str(app_id) if app_id else None,
        claims=claims,
    )


async def authenticate_token(
    token: str, settings: Settings, jwks_client: JWKSClient
) -> UserContext:
    claims = await decode_token(token, settings, jwks_client)
    return extract_identity(claims)


def _select_jwk(jwks: Dict[str, Any], kid: Optional[str]) -> Dict[str, Any]:
    keys = jwks.get("keys", [])
    if not isinstance(keys, list):
        raise AuthError("Invalid JWKS format", status_code=401)
    if kid:
        for key in keys:
            if key.get("kid") == kid:
                return key
    if len(keys) == 1:
        return keys[0]
    raise AuthError("Signing key not found", status_code=401)


def _normalize_roles(value: Any) -> list[str]:
    if isinstance(value, list):
        return [str(role) for role in value if role]
    if isinstance(value, str) and value.strip():
        return [value.strip()]
    return []
