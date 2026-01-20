# backend/core/config/settings.py
from __future__ import annotations

import os
import re
import secrets
import logging
from dataclasses import dataclass
from typing import Literal

from dotenv import load_dotenv

logger = logging.getLogger("mozaiks_core.settings")


def _dotenv_enabled() -> bool:
    value = os.getenv("MOZAIKS_LOAD_DOTENV")
    if value is None:
        return True
    return value.strip().lower() not in {"0", "false", "no", "off"}


# Load environment variables from a local .env for dev/test (never override process env).
if _dotenv_enabled():
    load_dotenv(override=False)


def _env_str(name: str, default: str | None = None) -> str | None:
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


def _env_int(name: str, default: int) -> int:
    value = _env_str(name)
    if value is None:
        return default
    try:
        return int(value)
    except Exception:
        return default


def _env_float(name: str, default: float) -> float:
    value = _env_str(name)
    if value is None:
        return default
    try:
        return float(value)
    except Exception:
        return default


def _split_csv(value: str | None) -> list[str]:
    if not value:
        return []
    return [v.strip() for v in value.split(",") if v.strip()]


def _normalize_origin(origin: str) -> str:
    return origin.rstrip("/")


def _normalize_host(host: str) -> str:
    return host.strip()


def _looks_like_dev_secret(secret: str) -> bool:
    if not secret:
        return True
    lowered = secret.strip().lower()
    return lowered in {"supersecretkey", "dev", "development", "changeme", "change-me", "secret"}


def _is_strong_secret(secret: str) -> bool:
    if not secret:
        return False
    if len(secret) < 32:
        return False
    # Require some variety to avoid trivial patterns.
    return bool(re.search(r"[A-Z]", secret)) and bool(re.search(r"[a-z]", secret)) and bool(re.search(r"\d", secret))


HostingMode = Literal["hosted", "self_host"]
AuthMode = Literal["platform", "external", "local"]


def _normalize_choice(name: str, value: str | None, allowed: set[str], default: str) -> str:
    resolved = (value or "").strip().lower()
    if not resolved:
        return default
    if resolved not in allowed:
        raise RuntimeError(f"Invalid {name}: '{resolved}' (expected one of: {', '.join(sorted(allowed))})")
    return resolved


@dataclass(frozen=True)
class Settings:
    env: str
    cors_origins: tuple[str, ...]
    cors_allow_credentials: bool
    allowed_hosts: tuple[str, ...]
    mozaiks_managed: bool
    mozaiks_hosting_mode: HostingMode
    mozaiks_auth_mode: AuthMode
    token_exchange_enabled: bool
    mozaiks_gateway_base_url: str | None
    mozaiks_app_id: str | None
    mozaiks_api_key: str | None
    mozaiks_sdk_version: str
    platform_jwks_url: str | None
    platform_jwt_issuer: str | None
    platform_jwt_audience: str | None
    platform_jwt_algorithms: tuple[str, ...]
    platform_user_id_claim: str
    platform_email_claim: str
    platform_username_claim: str | None
    platform_roles_claim: str
    platform_admin_role: str
    platform_superadmin_role: str
    platform_superadmin_claim: str | None
    jwt_secret: str
    jwt_algorithm: str
    access_token_expire_minutes: int
    app_token_expire_minutes: int
    internal_api_key: str | None
    auto_refresh_plugins: bool
    max_request_body_bytes: int
    plugin_exec_timeout_s: float
    plugin_exec_max_concurrency: int
    websocket_max_connections_per_user: int
    openapi_enabled: bool
    debug_endpoints_enabled: bool
    insights_push_enabled: bool
    insights_base_url: str | None
    insights_internal_api_key: str | None
    insights_push_interval_s: float
    insights_push_bucket: str
    insights_heartbeat_enabled: bool
    insights_heartbeat_interval_s: float
    insights_events_batch_size: int
    insights_events_initial_lookback_s: int

    # Runtime / Pack Loader
    runtime_base_url: str | None

    @property
    def is_production(self) -> bool:
        return self.env.lower() == "production"

    def validate(self) -> None:
        if self.mozaiks_managed and not self.mozaiks_app_id:
            raise RuntimeError("MOZAIKS_MANAGED=true requires MOZAIKS_APP_ID")

        if self.mozaiks_auth_mode not in {"platform", "external", "local"}:
            raise RuntimeError("MOZAIKS_AUTH_MODE must be one of: platform, external, local")
        if self.mozaiks_hosting_mode not in {"hosted", "self_host"}:
            raise RuntimeError("MOZAIKS_HOSTING_MODE must be one of: hosted, self_host")

        # Platform/external auth requires external JWT verification (JWKS).
        if self.mozaiks_auth_mode in {"platform", "external"}:
            if not self.platform_jwks_url:
                raise RuntimeError("MOZAIKS_AUTH_MODE=platform|external requires MOZAIKS_JWKS_URL (or legacy MOZAIKS_PLATFORM_JWKS_URL)")
            if not self.platform_jwt_issuer:
                raise RuntimeError("MOZAIKS_AUTH_MODE=platform|external requires MOZAIKS_ISSUER (or legacy MOZAIKS_PLATFORM_ISSUER)")
            if not self.platform_jwt_audience:
                raise RuntimeError("MOZAIKS_AUTH_MODE=platform|external requires MOZAIKS_AUDIENCE (or legacy MOZAIKS_PLATFORM_AUDIENCE)")
            if not (self.platform_user_id_claim or "").strip():
                raise RuntimeError("MOZAIKS_USER_ID_CLAIM is required (or legacy MOZAIKS_PLATFORM_USER_ID_CLAIM)")
            if not (self.platform_roles_claim or "").strip():
                raise RuntimeError("MOZAIKS_ROLES_CLAIM is required (or legacy MOZAIKS_PLATFORM_ROLES_CLAIM)")
            if not (self.platform_email_claim or "").strip():
                raise RuntimeError("MOZAIKS_EMAIL_CLAIM is required (or legacy MOZAIKS_PLATFORM_EMAIL_CLAIM)")
            if not (self.platform_superadmin_role or "").strip() and not (self.platform_superadmin_claim or "").strip():
                raise RuntimeError("Configure MOZAIKS_SUPERADMIN_ROLE and/or MOZAIKS_SUPERADMIN_CLAIM")

            if self.token_exchange_enabled and not self.mozaiks_app_id:
                raise RuntimeError("MOZAIKS_TOKEN_EXCHANGE=true requires MOZAIKS_APP_ID")

        if self.app_token_expire_minutes <= 0:
            raise RuntimeError("MOZAIKS_APP_TOKEN_EXPIRE_MINUTES must be > 0")

        if self.is_production:
            if self.mozaiks_auth_mode == "local" or (
                self.mozaiks_auth_mode in {"platform", "external"} and self.token_exchange_enabled
            ):
                if _looks_like_dev_secret(self.jwt_secret) or not _is_strong_secret(self.jwt_secret):
                    raise RuntimeError(
                        "JWT_SECRET is missing/weak. In production, set JWT_SECRET to a strong secret (>=32 chars, mixed)."
                    )
            if not self.mozaiks_app_id:
                raise RuntimeError("MOZAIKS_APP_ID is required in production.")
            if not self.allowed_hosts:
                raise RuntimeError(
                    "ALLOWED_HOSTS is not set. In production, set ALLOWED_HOSTS to a comma-separated allowlist."
                )

            if self.mozaiks_hosting_mode == "hosted":
                if not self.mozaiks_gateway_base_url:
                    raise RuntimeError("MOZAIKS_HOSTING_MODE=hosted requires MOZAIKS_GATEWAY_BASE_URL in production.")

        # Insights/telemetry should not prevent the app from starting.
        # Configuration is checked by the insights push loop at runtime.

        # Basic sanity checks that apply everywhere.
        if self.max_request_body_bytes < 1024:
            raise RuntimeError("MAX_REQUEST_BODY_BYTES is too small (<1024).")
        if self.plugin_exec_timeout_s <= 0:
            raise RuntimeError("PLUGIN_EXEC_TIMEOUT_S must be > 0.")
        if self.plugin_exec_max_concurrency <= 0:
            raise RuntimeError("PLUGIN_EXEC_MAX_CONCURRENCY must be > 0.")
        if self.websocket_max_connections_per_user <= 0:
            raise RuntimeError("WEBSOCKET_MAX_CONNECTIONS_PER_USER must be > 0.")
        if self.insights_push_interval_s <= 0:
            raise RuntimeError("INSIGHTS_PUSH_INTERVAL_S must be > 0.")
        if self.insights_events_batch_size <= 0:
            raise RuntimeError("INSIGHTS_EVENTS_BATCH_SIZE must be > 0.")
        if self.insights_events_initial_lookback_s < 0:
            raise RuntimeError("INSIGHTS_EVENTS_INITIAL_LOOKBACK_S must be >= 0.")


def load_settings() -> Settings:
    env = (_env_str("ENV", "development") or "development").strip().lower()

    frontend_url = _env_str("FRONTEND_URL", "http://localhost:5173")
    additional_origins = _split_csv(_env_str("ADDITIONAL_CORS_ORIGINS"))
    cors_origins = tuple({_normalize_origin(o) for o in ([frontend_url] + additional_origins) if o})

    allowed_hosts = tuple(_normalize_host(h) for h in _split_csv(_env_str("ALLOWED_HOSTS")) if h)

    legacy_managed = _env_bool("MOZAIKS_MANAGED", default=False)

    hosting_mode = _normalize_choice(
        "MOZAIKS_HOSTING_MODE",
        _env_str("MOZAIKS_HOSTING_MODE"),
        {"hosted", "self_host"},
        "hosted" if legacy_managed else "self_host",
    )
    raw_auth_mode = _env_str("MOZAIKS_AUTH_MODE")
    normalized_auth_mode = (raw_auth_mode or "").strip().lower() or None
    if normalized_auth_mode == "oidc":
        normalized_auth_mode = "external"
    if normalized_auth_mode == "jwt":
        normalized_auth_mode = "local"

    auth_mode = _normalize_choice(
        "MOZAIKS_AUTH_MODE",
        normalized_auth_mode,
        {"platform", "external", "local"},
        "platform" if legacy_managed else "external",
    )

    # Backwards-compatible alias (deprecated): hosted + platform auth.
    mozaiks_managed = bool(legacy_managed or (hosting_mode == "hosted" and auth_mode == "platform"))

    # Token exchange (platform/external -> app-scoped JWT) is optional.
    # - MOZAIKS_TOKEN_EXCHANGE=true: app endpoints require app-scoped tokens (minted via /api/auth/token-exchange)
    # - MOZAIKS_TOKEN_EXCHANGE=false: app endpoints accept platform/OIDC tokens directly
    token_exchange_env = _env_str("MOZAIKS_TOKEN_EXCHANGE")
    if token_exchange_env is not None:
        token_exchange_enabled = _env_bool("MOZAIKS_TOKEN_EXCHANGE", default=False)
    else:
        # Deprecated compatibility: MOZAIKS_PLATFORM_JWT_DIRECT_ENABLED=true implies token exchange is off.
        direct_env = _env_str("MOZAIKS_PLATFORM_JWT_DIRECT_ENABLED")
        if direct_env is not None:
            token_exchange_enabled = not _env_bool("MOZAIKS_PLATFORM_JWT_DIRECT_ENABLED", default=True)
        else:
            token_exchange_enabled = False

    mozaiks_gateway_base_url = _env_str("MOZAIKS_GATEWAY_BASE_URL")
    mozaiks_app_id = _env_str("MOZAIKS_APP_ID")
    mozaiks_api_key = _env_str("MOZAIKS_API_KEY")
    mozaiks_sdk_version = (_env_str("MOZAIKS_SDK_VERSION", "1.0.0") or "1.0.0").strip()

    platform_jwks_url = _env_str("MOZAIKS_JWKS_URL") or _env_str("MOZAIKS_PLATFORM_JWKS_URL")
    platform_jwt_issuer = (
        _env_str("MOZAIKS_ISSUER")
        or _env_str("MOZAIKS_PLATFORM_ISSUER")
        or _env_str("MOZAIKS_PLATFORM_JWT_ISSUER")
    )
    platform_jwt_audience = (
        _env_str("MOZAIKS_AUDIENCE")
        or _env_str("MOZAIKS_PLATFORM_AUDIENCE")
        or _env_str("MOZAIKS_PLATFORM_JWT_AUDIENCE")
    )
    platform_jwt_algorithms = tuple(
        _split_csv(_env_str("MOZAIKS_JWT_ALGORITHMS")) or _split_csv(_env_str("MOZAIKS_PLATFORM_JWT_ALGORITHMS")) or ["RS256"]
    )
    platform_user_id_claim = _env_str("MOZAIKS_USER_ID_CLAIM") or _env_str("MOZAIKS_PLATFORM_USER_ID_CLAIM", "sub") or "sub"
    platform_email_claim = _env_str("MOZAIKS_EMAIL_CLAIM") or _env_str("MOZAIKS_PLATFORM_EMAIL_CLAIM", "email") or "email"
    platform_username_claim = _env_str("MOZAIKS_USERNAME_CLAIM") or _env_str("MOZAIKS_PLATFORM_USERNAME_CLAIM") or None
    platform_roles_claim = _env_str("MOZAIKS_ROLES_CLAIM") or _env_str("MOZAIKS_PLATFORM_ROLES_CLAIM", "roles") or "roles"
    platform_admin_role = _env_str("MOZAIKS_ADMIN_ROLE") or _env_str("MOZAIKS_PLATFORM_ADMIN_ROLE", "admin") or "admin"
    platform_superadmin_role = _env_str("MOZAIKS_PLATFORM_SUPERADMIN_ROLE") or _env_str("MOZAIKS_SUPERADMIN_ROLE") or "SuperAdmin"
    platform_superadmin_claim = _env_str("MOZAIKS_SUPERADMIN_CLAIM") or None

    # Secrets
    jwt_secret = _env_str("JWT_SECRET") or _env_str("JWT_SECRET_KEY") or ""
    if not jwt_secret:
        # Development/test-friendly fallback: ephemeral secret (NOT for production).
        jwt_secret = secrets.token_urlsafe(48)
        logger.warning("JWT_SECRET not set; generated an ephemeral dev secret (set JWT_SECRET for stable tokens)")

    insights_push_enabled = _env_bool("INSIGHTS_PUSH_ENABLED", default=False)
    insights_base_url = _env_str("INSIGHTS_API_BASE_URL") or _env_str("INSIGHTS_BASE_URL")
    insights_internal_api_key = _env_str("INSIGHTS_INTERNAL_API_KEY") or _env_str("INTERNAL_API_KEY")
    insights_push_interval_s = _env_float("INSIGHTS_PUSH_INTERVAL_S", default=60.0)
    insights_push_bucket = (_env_str("INSIGHTS_PUSH_BUCKET", "1m") or "1m").strip()

    # Heartbeat is used to infer "sdkConnected"/last ping in dashboards.
    # It is a best-effort signal and should be safe to disable in dev/test.
    insights_heartbeat_enabled = _env_bool("INSIGHTS_HEARTBEAT_ENABLED", default=True)
    default_heartbeat_interval_s = max(60.0, float(insights_push_interval_s))
    insights_heartbeat_interval_s = _env_float("INSIGHTS_HEARTBEAT_INTERVAL_S", default=default_heartbeat_interval_s)

    insights_events_batch_size = _env_int("INSIGHTS_EVENTS_BATCH_SIZE", default=250)
    insights_events_initial_lookback_s = _env_int("INSIGHTS_EVENTS_INITIAL_LOOKBACK_S", default=0)

    settings = Settings(
        env=env,
        cors_origins=cors_origins,
        cors_allow_credentials=_env_bool("CORS_ALLOW_CREDENTIALS", default=False),
        allowed_hosts=allowed_hosts,
        mozaiks_managed=mozaiks_managed,
        mozaiks_hosting_mode=hosting_mode,  # type: ignore[arg-type]
        mozaiks_auth_mode=auth_mode,  # type: ignore[arg-type]
        token_exchange_enabled=token_exchange_enabled,
        mozaiks_gateway_base_url=mozaiks_gateway_base_url,
        mozaiks_app_id=mozaiks_app_id,
        mozaiks_api_key=mozaiks_api_key,
        mozaiks_sdk_version=mozaiks_sdk_version,
        platform_jwks_url=platform_jwks_url,
        platform_jwt_issuer=platform_jwt_issuer,
        platform_jwt_audience=platform_jwt_audience,
        platform_jwt_algorithms=platform_jwt_algorithms,
        platform_user_id_claim=platform_user_id_claim,
        platform_email_claim=platform_email_claim,
        platform_username_claim=platform_username_claim,
        platform_roles_claim=platform_roles_claim,
        platform_admin_role=platform_admin_role,
        platform_superadmin_role=platform_superadmin_role,
        platform_superadmin_claim=platform_superadmin_claim,
        jwt_secret=jwt_secret,
        jwt_algorithm=_env_str("JWT_ALGORITHM", "HS256") or "HS256",
        access_token_expire_minutes=_env_int("ACCESS_TOKEN_EXPIRE_MINUTES", default=60 * 24),
        app_token_expire_minutes=_env_int("MOZAIKS_APP_TOKEN_EXPIRE_MINUTES", default=15),
        internal_api_key=_env_str("INTERNAL_API_KEY"),
        auto_refresh_plugins=_env_bool("MOZAIKS_AUTO_REFRESH_PLUGINS", default=env != "production"),
        max_request_body_bytes=_env_int("MAX_REQUEST_BODY_BYTES", default=1_000_000),
        plugin_exec_timeout_s=_env_float("PLUGIN_EXEC_TIMEOUT_S", default=15.0),
        plugin_exec_max_concurrency=_env_int("PLUGIN_EXEC_MAX_CONCURRENCY", default=8),
        websocket_max_connections_per_user=_env_int("WEBSOCKET_MAX_CONNECTIONS_PER_USER", default=5),
        openapi_enabled=_env_bool("OPENAPI_ENABLED", default=env != "production"),
        debug_endpoints_enabled=_env_bool("DEBUG_ENDPOINTS_ENABLED", default=env != "production"),
        insights_push_enabled=insights_push_enabled,
        insights_base_url=insights_base_url,
        insights_internal_api_key=insights_internal_api_key,
        insights_push_interval_s=insights_push_interval_s,
        insights_push_bucket=insights_push_bucket,
        insights_heartbeat_enabled=insights_heartbeat_enabled,
        insights_heartbeat_interval_s=insights_heartbeat_interval_s,
        insights_events_batch_size=insights_events_batch_size,
        insights_events_initial_lookback_s=insights_events_initial_lookback_s,

        # Runtime / Pack Loader
        runtime_base_url=_env_str("RUNTIME_BASE_URL"),
    )
    settings.validate()
    return settings


settings = load_settings()
