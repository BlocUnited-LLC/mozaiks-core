from __future__ import annotations

import os
from datetime import datetime, timedelta
from typing import Any, Optional, Tuple

from jose import jwt


def _env_str(name: str, default: Optional[str] = None) -> Optional[str]:
    value = os.getenv(name)
    if value is None:
        return default
    value = value.strip()
    return value if value else default


def _env_int(name: str, default: int) -> int:
    raw = _env_str(name)
    if raw is None:
        return default
    try:
        return int(raw)
    except Exception:
        return default


def _load_signing_key(algorithm: str) -> str:
    alg = (algorithm or "").strip().upper()
    if alg.startswith("HS"):
        return (_env_str("MOZAIKS_EXECUTION_TOKEN_SECRET") or _env_str("JWT_SECRET") or "supersecretkey").strip()

    pem = _env_str("MOZAIKS_EXECUTION_TOKEN_PRIVATE_KEY")
    if pem:
        return pem.replace("\\n", "\n")

    path = _env_str("MOZAIKS_EXECUTION_TOKEN_PRIVATE_KEY_PATH")
    if path:
        with open(path, "r", encoding="utf-8") as f:
            return f.read()

    raise RuntimeError(
        "Execution token signing key is not configured (set MOZAIKS_EXECUTION_TOKEN_PRIVATE_KEY or MOZAIKS_EXECUTION_TOKEN_PRIVATE_KEY_PATH)"
    )


def mint_execution_token(*, claims: dict[str, Any]) -> Tuple[str, int]:
    """
    Mint a short-lived, runtime-facing execution token that includes an ExecutionContext.

    The token is issued by the control plane (MozaiksCore) and is intended to be validated by MozaiksAI.
    """
    algorithm = (_env_str("MOZAIKS_EXECUTION_TOKEN_ALGORITHM") or _env_str("JWT_ALGORITHM") or "HS256").strip() or "HS256"
    issuer = (_env_str("MOZAIKS_EXECUTION_TOKEN_ISSUER") or _env_str("MOZAIKS_EXECUTION_ISSUER") or "mozaikscore").strip()
    audience = (_env_str("MOZAIKS_EXECUTION_TOKEN_AUDIENCE") or _env_str("MOZAIKS_EXECUTION_AUDIENCE") or "").strip()

    expires_minutes = _env_int("MOZAIKS_EXECUTION_TOKEN_EXPIRE_MINUTES", 10)
    expires_minutes = max(1, min(expires_minutes, 60))

    now = datetime.utcnow()
    expires = now + timedelta(minutes=expires_minutes)

    payload: dict[str, Any] = {
        "iss": issuer,
        "iat": int(now.timestamp()),
        "exp": int(expires.timestamp()),
        "mozaiks_token_use": "execution",
    }
    if audience:
        payload["aud"] = audience
    payload.update(claims)

    key = _load_signing_key(algorithm)
    token = jwt.encode(payload, key, algorithm=algorithm)
    return token, expires_minutes * 60

