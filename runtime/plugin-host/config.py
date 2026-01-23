from __future__ import annotations

from dataclasses import dataclass
import os
from typing import Optional, Tuple


def _env_str(key: str, default: Optional[str] = None) -> Optional[str]:
    value = os.getenv(key)
    if value is None:
        return default
    value = value.strip()
    return value if value else default


def _env_bool(key: str, default: bool = False) -> bool:
    value = os.getenv(key)
    if value is None:
        return default
    return value.strip().lower() in {"1", "true", "yes", "on"}


def _env_int(key: str, default: int) -> int:
    value = os.getenv(key)
    if value is None or not value.strip():
        return default
    try:
        return int(value)
    except ValueError:
        return default


def _parse_algorithms(value: Optional[str]) -> Tuple[str, ...]:
    if not value:
        return ("RS256",)
    parts = [item.strip() for item in value.split(",")]
    algs = [item for item in parts if item]
    return tuple(algs) if algs else ("RS256",)


@dataclass(frozen=True)
class Settings:
    plugins_dir: str
    jwt_issuer: str
    jwt_audience: str
    jwks_url: str
    jwt_algorithms: Tuple[str, ...]
    jwks_cache_ttl_seconds: int
    watch_plugins: bool
    plugin_scan_interval_seconds: int
    app_id: Optional[str]
    log_level: str
    skip_auth: bool


def load_settings() -> Settings:
    env = os.getenv("ENV", "development").strip().lower()
    skip_auth = _env_bool("SKIP_AUTH", default=False)
    plugins_dir = _env_str("PLUGINS_DIR", "./plugins") or "./plugins"
    jwt_issuer = _env_str("JWT_ISSUER") or ""
    jwt_audience = _env_str("JWT_AUDIENCE") or ""
    jwks_url = _env_str("JWKS_URL") or ""
    jwt_algorithms = _parse_algorithms(_env_str("JWT_ALGORITHMS") or _env_str("JWT_ALGORITHM"))
    jwks_cache_ttl_seconds = _env_int("JWKS_CACHE_TTL_SECONDS", 300)
    watch_plugins = _env_bool("PLUGIN_WATCH", default=env != "production")
    plugin_scan_interval_seconds = _env_int("PLUGIN_SCAN_INTERVAL_SECONDS", 5)
    app_id = _env_str("MOZAIKS_APP_ID") or _env_str("APP_ID")
    log_level = (_env_str("LOG_LEVEL", "INFO") or "INFO").upper()

    # Only require JWT settings if auth is not skipped.
    if not skip_auth:
        missing = [
            name
            for name, value in {
                "JWT_ISSUER": jwt_issuer,
                "JWT_AUDIENCE": jwt_audience,
                "JWKS_URL": jwks_url,
            }.items()
            if not value
        ]
        if missing:
            raise RuntimeError(f"Missing required settings: {', '.join(missing)}")

    return Settings(
        plugins_dir=plugins_dir,
        jwt_issuer=jwt_issuer,
        jwt_audience=jwt_audience,
        jwks_url=jwks_url,
        jwt_algorithms=jwt_algorithms,
        jwks_cache_ttl_seconds=jwks_cache_ttl_seconds,
        watch_plugins=watch_plugins,
        plugin_scan_interval_seconds=plugin_scan_interval_seconds,
        app_id=app_id,
        log_level=log_level,
        skip_auth=skip_auth,
    )
