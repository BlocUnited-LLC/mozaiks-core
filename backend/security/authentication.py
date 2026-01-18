# backend/security/authentication.py

from __future__ import annotations

import asyncio
from datetime import datetime
import hmac
import logging
import os
import time
from typing import Any, Optional

import aiohttp
from bson import ObjectId
from fastapi import Depends, Header, HTTPException, Request, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from jose import JWTError, jwt

from core.config.database import users_collection
from security.constants import ALGORITHM, SECRET_KEY

logger = logging.getLogger("mozaiks_core.authentication")

bearer_scheme = HTTPBearer(auto_error=False)

_JWKS_LOCK = asyncio.Lock()
_JWKS_CACHE: dict[str, Any] = {"jwks": None, "fetched_at": 0.0}
_JWKS_TTL_S = 300.0


def _auth_mode() -> str:
    raw = (os.getenv("MOZAIKS_AUTH_MODE") or "external").strip().lower()
    if raw in {"external", "platform", "local"}:
        return raw
    # Backwards-compatible aliases (deprecated).
    if raw == "oidc":
        return "external"
    if raw == "jwt":
        return "local"
    raise RuntimeError(f"Invalid MOZAIKS_AUTH_MODE: '{raw}' (expected: platform, external, local)")


def _env_str(name: str, default: Optional[str] = None) -> Optional[str]:
    value = os.getenv(name)
    if value is None:
        return default
    value = value.strip()
    return value if value else default


def _env_bool(name: str, default: bool = False) -> bool:
    value = _env_str(name)
    if value is None:
        return default
    return value.lower() in {"1", "true", "yes", "y", "on"}


def _split_csv(value: Optional[str]) -> list[str]:
    if not value:
        return []
    return [v.strip() for v in value.split(",") if v.strip()]


def _token_exchange_enabled() -> bool:
    value = _env_str("MOZAIKS_TOKEN_EXCHANGE")
    if value is not None:
        return _env_bool("MOZAIKS_TOKEN_EXCHANGE", default=False)

    # Deprecated compatibility: MOZAIKS_PLATFORM_JWT_DIRECT_ENABLED=true implies token exchange is off.
    direct_env = _env_str("MOZAIKS_PLATFORM_JWT_DIRECT_ENABLED")
    if direct_env is not None:
        return not _env_bool("MOZAIKS_PLATFORM_JWT_DIRECT_ENABLED", default=True)

    return False


async def _fetch_json(url: str) -> dict[str, Any]:
    timeout = aiohttp.ClientTimeout(total=8, connect=4)
    async with aiohttp.ClientSession(timeout=timeout) as session:
        async with session.get(url) as resp:
            if resp.status >= 400:
                raise RuntimeError(f"JWKS fetch failed ({resp.status})")
            return await resp.json()


async def _get_jwks() -> dict[str, Any]:
    url = (_env_str("MOZAIKS_JWKS_URL") or _env_str("MOZAIKS_PLATFORM_JWKS_URL") or "").strip()
    if not url:
        raise RuntimeError("MOZAIKS_JWKS_URL is not configured")

    now = time.monotonic()
    cached = _JWKS_CACHE.get("jwks")
    fetched_at = float(_JWKS_CACHE.get("fetched_at") or 0.0)
    if cached and (now - fetched_at) < _JWKS_TTL_S:
        return cached

    async with _JWKS_LOCK:
        now = time.monotonic()
        cached = _JWKS_CACHE.get("jwks")
        fetched_at = float(_JWKS_CACHE.get("fetched_at") or 0.0)
        if cached and (now - fetched_at) < _JWKS_TTL_S:
            return cached

        jwks = await _fetch_json(url)
        if not isinstance(jwks, dict) or "keys" not in jwks:
            raise RuntimeError("Invalid JWKS payload (missing keys)")

        _JWKS_CACHE["jwks"] = jwks
        _JWKS_CACHE["fetched_at"] = time.monotonic()
        return jwks


async def _decode_external_jwt(token: str) -> dict[str, Any]:
    jwks = await _get_jwks()

    issuer = _env_str("MOZAIKS_ISSUER") or _env_str("MOZAIKS_PLATFORM_ISSUER")
    audience = _env_str("MOZAIKS_AUDIENCE") or _env_str("MOZAIKS_PLATFORM_AUDIENCE")
    algorithms = (
        _split_csv(_env_str("MOZAIKS_JWT_ALGORITHMS")) or _split_csv(_env_str("MOZAIKS_PLATFORM_JWT_ALGORITHMS")) or ["RS256"]
    )

    options = {"verify_aud": bool(audience)}
    kwargs: dict[str, Any] = {"algorithms": algorithms, "options": options}
    if issuer:
        kwargs["issuer"] = issuer
    if audience:
        kwargs["audience"] = audience

    return jwt.decode(token, jwks, **kwargs)


def _decode_hs_jwt(token: str) -> dict[str, Any]:
    algorithm = _env_str("JWT_ALGORITHM", ALGORITHM) or ALGORITHM
    return jwt.decode(token, SECRET_KEY, algorithms=[algorithm])


def _claim_str(payload: dict[str, Any], name: str) -> Optional[str]:
    value = payload.get(name)
    if isinstance(value, str) and value.strip():
        return value.strip()
    return None


def _claim_email(payload: dict[str, Any], name: str) -> Optional[str]:
    value = payload.get(name)
    if isinstance(value, str) and value.strip():
        return value.strip().lower()
    if isinstance(value, list):
        for item in value:
            if isinstance(item, str) and item.strip():
                return item.strip().lower()
    return None


def _claim_roles(payload: dict[str, Any], name: str) -> list[str]:
    value = payload.get(name)
    if isinstance(value, list):
        roles: list[str] = []
        for item in value:
            if isinstance(item, str) and item.strip():
                roles.append(item.strip())
        return roles
    if isinstance(value, str) and value.strip():
        raw = value.strip()
        if "," in raw:
            return [v.strip() for v in raw.split(",") if v.strip()]
        if " " in raw:
            return [v.strip() for v in raw.split(" ") if v.strip()]
        return [raw]
    return []


def _is_truthy(value: Any) -> bool:
    if value is True:
        return True
    if value is False or value is None:
        return False
    if isinstance(value, (int, float)):
        return value != 0
    if isinstance(value, str):
        return value.strip().lower() in {"1", "true", "yes", "y", "on"}
    return bool(value)


def _compute_is_superadmin(payload: dict[str, Any], roles: list[str]) -> bool:
    superadmin_claim = (_env_str("MOZAIKS_SUPERADMIN_CLAIM") or "").strip()
    if superadmin_claim and _is_truthy(payload.get(superadmin_claim)):
        return True

    superadmin_role = (
        (_env_str("MOZAIKS_SUPERADMIN_ROLE") or _env_str("MOZAIKS_PLATFORM_SUPERADMIN_ROLE") or "").strip()
    )
    if not superadmin_role:
        return False

    wanted = superadmin_role.lower()
    return any((r or "").strip().lower() == wanted for r in roles)


async def _ensure_unique_username(candidate: str, identity_user_id: Optional[str] = None) -> str:
    base = (candidate or "").strip()
    if not base:
        base = f"user_{(identity_user_id or 'unknown')[:8]}"

    existing = await users_collection.find_one({"username": base})
    if not existing:
        return base

    if identity_user_id:
        suffixed = f"{base}_{identity_user_id[:8]}"
        existing = await users_collection.find_one({"username": suffixed})
        if not existing:
            return suffixed

    # Last-resort: append monotonic timestamp fragment.
    return f"{base}_{int(time.time())}"


async def _provision_external_user(payload: dict[str, Any]) -> dict[str, Any]:
    if users_collection is None:
        raise HTTPException(status_code=500, detail="Database is unavailable")

    user_id_claim = _env_str("MOZAIKS_USER_ID_CLAIM") or _env_str("MOZAIKS_PLATFORM_USER_ID_CLAIM") or "sub"
    email_claim = _env_str("MOZAIKS_EMAIL_CLAIM") or _env_str("MOZAIKS_PLATFORM_EMAIL_CLAIM") or "email"
    username_claim = _env_str("MOZAIKS_USERNAME_CLAIM") or _env_str("MOZAIKS_PLATFORM_USERNAME_CLAIM") or ""

    identity_user_id = _claim_str(payload, user_id_claim) or _claim_str(payload, "sub")
    if not identity_user_id:
        raise HTTPException(status_code=401, detail="Token missing user identifier")

    email = _claim_email(payload, email_claim)
    preferred_username = _claim_str(payload, username_claim) if username_claim else None
    full_name = _claim_str(payload, "name") or _claim_str(payload, "full_name")

    user = await users_collection.find_one({"external_user_id": identity_user_id})
    if not user:
        user = await users_collection.find_one({"platform_user_id": identity_user_id})
    now_iso = datetime.utcnow().isoformat()

    if not user:
        base_username = (
            preferred_username or (email.split("@", 1)[0] if email else None) or f"user_{identity_user_id[:8]}"
        )
        username = await _ensure_unique_username(base_username, identity_user_id)
        doc = {
            "username": username,
            "full_name": full_name or username,
            "email": email or "",
            "external_user_id": identity_user_id,
            "disabled": False,
            "created_at": now_iso,
            "updated_at": now_iso,
            "last_active": now_iso,
        }
        result = await users_collection.insert_one(doc)
        user = {**doc, "_id": result.inserted_id}
        logger.info(f"Provisioned external user: {username} ({identity_user_id})")
        return user

    update: dict[str, Any] = {"updated_at": now_iso, "last_active": now_iso}
    if not user.get("external_user_id"):
        update["external_user_id"] = identity_user_id
    if email:
        update["email"] = email
    if full_name:
        update["full_name"] = full_name

    try:
        await users_collection.update_one({"_id": user["_id"]}, {"$set": update})
    except Exception as e:
        logger.debug(f"Failed to update platform user last_active: {e}")

    user.update(update)
    return user


async def _resolve_local_user(payload: dict[str, Any]) -> dict[str, Any]:
    if users_collection is None:
        raise HTTPException(status_code=500, detail="Database is unavailable")

    now_iso = datetime.utcnow().isoformat()

    token_user_id = _claim_str(payload, "user_id")
    if token_user_id:
        try:
            user = await users_collection.find_one({"_id": ObjectId(token_user_id)})
            if user:
                await users_collection.update_one(
                    {"_id": user["_id"]}, {"$set": {"last_active": now_iso, "updated_at": now_iso}}
                )
                user.update({"last_active": now_iso, "updated_at": now_iso})
                return user
        except Exception:
            pass

    username = _claim_str(payload, "sub") or _claim_str(payload, "username")
    if not username:
        raise HTTPException(status_code=401, detail="Token missing username")

    user = await users_collection.find_one({"username": username})
    if not user:
        email = _claim_email(payload, "email")
        full_name = _claim_str(payload, "full_name") or _claim_str(payload, "name") or username
        doc = {
            "username": username,
            "full_name": full_name,
            "email": email or "",
            "disabled": False,
            "created_at": now_iso,
            "updated_at": now_iso,
            "last_active": now_iso,
        }
        result = await users_collection.insert_one(doc)
        user = {**doc, "_id": result.inserted_id}
        logger.info(f"Provisioned local user: {username}")
        return user

    await users_collection.update_one({"_id": user["_id"]}, {"$set": {"last_active": now_iso, "updated_at": now_iso}})
    user.update({"last_active": now_iso, "updated_at": now_iso})
    return user


def is_superadmin(current_user: dict[str, Any]) -> bool:
    return bool(current_user.get("is_superadmin"))

async def get_current_user(request: Request, credentials: Optional[HTTPAuthorizationCredentials] = Depends(bearer_scheme)):
    if not credentials or (credentials.scheme or "").lower() != "bearer":
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Not authenticated",
            headers={"WWW-Authenticate": "Bearer"},
        )

    token = (credentials.credentials or "").strip()
    if not token:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Not authenticated",
            headers={"WWW-Authenticate": "Bearer"},
        )

    mode = _auth_mode()
    token_exchange = _token_exchange_enabled()

    allow_platform_token = request.url.path in {
        "/api/auth/token-exchange",
        "/api/auth/validate-token",
        "/api/auth/me",
    }

    try:
        if mode in {"external", "platform"}:
            token_kind = "external"
            try:
                unverified_claims = jwt.get_unverified_claims(token)
            except Exception:
                unverified_claims = None

            if isinstance(unverified_claims, dict) and unverified_claims.get("mozaiks_token_use") == "app":
                token_kind = "app"

            if token_exchange and token_kind == "external" and not allow_platform_token:
                raise HTTPException(
                    status_code=status.HTTP_401_UNAUTHORIZED,
                    detail="Token exchange required",
                    headers={"WWW-Authenticate": "Bearer"},
                )

            if token_kind == "app":
                payload = _decode_hs_jwt(token)
                if payload.get("mozaiks_token_use") != "app":
                    raise HTTPException(
                        status_code=status.HTTP_401_UNAUTHORIZED,
                        detail="Invalid app token",
                        headers={"WWW-Authenticate": "Bearer"},
                    )
                user = await _resolve_local_user(payload)
            else:
                payload = await _decode_external_jwt(token)
                user = await _provision_external_user(payload)

            roles_claim = _env_str("MOZAIKS_ROLES_CLAIM") or _env_str("MOZAIKS_PLATFORM_ROLES_CLAIM") or "roles"
            roles = _claim_roles(payload, roles_claim)
            identity_user_id = None
            user_id_claim = _env_str("MOZAIKS_USER_ID_CLAIM") or _env_str("MOZAIKS_PLATFORM_USER_ID_CLAIM") or "sub"
            identity_user_id = _claim_str(payload, user_id_claim) or _claim_str(payload, "sub")
            email_claim = _env_str("MOZAIKS_EMAIL_CLAIM") or _env_str("MOZAIKS_PLATFORM_EMAIL_CLAIM") or "email"
            identity_email = _claim_email(payload, email_claim)
            computed_superadmin = _compute_is_superadmin(payload, roles)
        elif mode == "local":
            payload = _decode_hs_jwt(token)
            user = await _resolve_local_user(payload)
            roles_claim = _env_str("MOZAIKS_ROLES_CLAIM") or "roles"
            roles = _claim_roles(payload, roles_claim)
            identity_user_id = _claim_str(payload, "sub") or _claim_str(payload, "username")
            identity_email = _claim_email(payload, "email")
            computed_superadmin = _compute_is_superadmin(payload, roles)
        else:
            raise HTTPException(status_code=500, detail=f"Unsupported auth mode: {mode}")
    except HTTPException:
        raise
    except RuntimeError as e:
        raise HTTPException(status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e))
    except JWTError:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Could not validate credentials",
            headers={"WWW-Authenticate": "Bearer"},
        )
    except Exception as e:
        logger.warning(f"Auth error: {e}")
        raise HTTPException(status_code=status.HTTP_401_UNAUTHORIZED, detail="Could not validate credentials")

    if user.get("disabled"):
        raise HTTPException(status_code=400, detail="Inactive user")

    return {
        "username": user.get("username"),
        "user_id": str(user.get("_id")),
        "email": user.get("email") or identity_email or "",
        "auth_mode": mode,
        "identity_user_id": identity_user_id,
        "roles": roles,
        "is_superadmin": bool(computed_superadmin),
    }


# Now that get_current_user exists, define the real require_superadmin dependency.
async def require_superadmin(current_user: dict = Depends(get_current_user)):
    if not is_superadmin(current_user):
        raise HTTPException(status_code=status.HTTP_403_FORBIDDEN, detail="Superadmin access required")
    return current_user


async def get_current_active_user(current_user: dict = Depends(get_current_user)):
    user = await users_collection.find_one({"username": current_user["username"]})
    if user is None:
        raise HTTPException(status_code=404, detail="User not found")
    if user.get("disabled"):
        raise HTTPException(status_code=400, detail="Inactive user")
    return current_user


# ---------------------------------------------------------------------------
# Admin Auth Helpers (for /__mozaiks/admin/* routes)
# ---------------------------------------------------------------------------

def _get_internal_api_key() -> Optional[str]:
    """Get the internal API key from environment or settings."""
    from core.config.settings import settings
    return os.getenv("INTERNAL_API_KEY") or getattr(settings, "internal_api_key", None)


def _safe_compare(a: str, b: str) -> bool:
    """Constant-time comparison to prevent timing attacks."""
    return hmac.compare_digest(a.encode(), b.encode())


async def require_internal_api_key(
    x_internal_api_key: Optional[str] = Header(None, alias="X-Internal-API-Key"),
) -> dict[str, Any]:
    """Dependency that validates X-Internal-API-Key only (no user JWT)."""
    internal_key = _get_internal_api_key()

    if not internal_key:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Internal API not configured (no INTERNAL_API_KEY set)",
        )

    if not x_internal_api_key:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Missing X-Internal-API-Key header",
        )

    if not _safe_compare(internal_key, x_internal_api_key):
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Invalid internal API key",
        )

    return {
        "username": "_internal_service",
        "user_id": "_internal",
        "auth_mode": "internal_api_key",
        "is_internal_service": True,
    }


async def get_current_admin_user(
    request: Request,
    credentials: Optional[HTTPAuthorizationCredentials] = Depends(bearer_scheme),
) -> dict[str, Any]:
    """
    Get the current user and verify they have admin (superadmin) privileges.
    
    This is for admin dashboard endpoints that require a logged-in superadmin user.
    Use require_admin_or_internal for endpoints that can also be called by internal services.
    """
    current_user = await get_current_user(request, credentials)
    
    if not is_superadmin(current_user):
        raise HTTPException(
            status_code=status.HTTP_403_FORBIDDEN,
            detail="Admin access required (superadmin role not found)"
        )
    
    return current_user


async def require_admin_or_internal(
    request: Request,
    x_internal_api_key: Optional[str] = Header(None, alias="X-Internal-API-Key"),
    credentials: Optional[HTTPAuthorizationCredentials] = Depends(bearer_scheme),
) -> dict[str, Any]:
    """
    Dependency that allows access via:
    1. X-Internal-API-Key header (for MozaiksAI/Control Plane service-to-service calls)
    2. Bearer token with superadmin role (for admin dashboard)
    
    Returns a user dict (real user for JWT, synthetic for API key).
    """
    internal_key = _get_internal_api_key()
    
    # Check internal API key first (service-to-service)
    if x_internal_api_key and internal_key:
        if _safe_compare(internal_key, x_internal_api_key):
            return {
                "username": "_internal_service",
                "user_id": "_internal",
                "email": "",
                "auth_mode": "internal_api_key",
                "identity_user_id": "_internal",
                "roles": ["internal_service"],
                "is_superadmin": True,
                "is_internal_service": True,
            }
    
    # Fall back to JWT-based admin auth
    if credentials and (credentials.scheme or "").lower() == "bearer":
        try:
            current_user = await get_current_user(request, credentials)
            if is_superadmin(current_user):
                return current_user
        except HTTPException:
            pass  # Fall through to final error
    
    # Neither method succeeded
    raise HTTPException(
        status_code=status.HTTP_401_UNAUTHORIZED,
        detail="Admin access required (provide X-Internal-API-Key or superadmin Bearer token)",
        headers={"WWW-Authenticate": "Bearer"},
    )
