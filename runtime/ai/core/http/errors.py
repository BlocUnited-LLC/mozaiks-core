# backend/core/http/errors.py
from __future__ import annotations

from core.config.settings import settings


def safe_error_detail(public_message: str, exc: Exception) -> str:
    """Return a client-facing error detail without leaking internals in production."""
    if settings.is_production:
        return public_message
    return f"{public_message}: {exc}"

