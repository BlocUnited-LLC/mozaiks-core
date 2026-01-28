from __future__ import annotations

import logging
import os
import uuid
from dataclasses import dataclass
from typing import Any

import httpx

from mozaiks_infra.config.settings import settings

logger = logging.getLogger("mozaiks_core.runtime.manager")


def _env_str(name: str, default: str | None = None) -> str | None:
    value = os.getenv(name)
    if value is None:
        return default
    value = value.strip()
    return value if value else default


def _rstrip_slash(value: str | None) -> str | None:
    if not value:
        return None
    trimmed = value.strip()
    return trimmed.rstrip("/") if trimmed else None


@dataclass(frozen=True)
class RuntimeUiConfig:
    runtime_api_base_url: str | None
    runtime_ui_base_url: str | None
    chatui_url_template: str


class RuntimeManager:
    """
    Control-plane bridge to an external MozaiksAI runtime.

    IMPORTANT (Source of Truth / Repo Boundaries):
    - This module must NOT execute workflows or implement ChatUI.
    - It only brokers session metadata and optionally proxies runtime discovery APIs.
    """

    def __init__(self) -> None:
        self._workflow_cache: dict[str, tuple[list[Any], float]] = {}
        self._cache_ttl_s = 300.0

    def ui_config(self) -> RuntimeUiConfig:
        runtime_api_base_url = _rstrip_slash(settings.runtime_base_url)
        runtime_ui_base_url = _rstrip_slash(_env_str("MOZAIKS_RUNTIME_UI_BASE_URL") or runtime_api_base_url)

        # Template supports: {runtime_ui_base_url} {app_id} {capability_id} {chat_id} {token}
        # NOTE: Execution scope is carried by the signed {token} (ExecutionContext claims); avoid trusting query params for scope.
        chatui_url_template = (
            _env_str("MOZAIKS_CHATUI_URL_TEMPLATE")
            or "{runtime_ui_base_url}/chat?app_id={app_id}&chat_id={chat_id}&token={token}"
        )

        return RuntimeUiConfig(
            runtime_api_base_url=runtime_api_base_url,
            runtime_ui_base_url=runtime_ui_base_url,
            chatui_url_template=chatui_url_template,
        )

    def new_chat_id(self) -> str:
        return f"chat_{uuid.uuid4().hex}"

    def build_chatui_url(
        self,
        *,
        app_id: str,
        workflow_id: str,
        capability_id: str,
        chat_id: str,
        token: str,
    ) -> str:
        config = self.ui_config()
        if not config.runtime_ui_base_url:
            raise RuntimeError("Runtime UI base URL is not configured (set MOZAIKS_RUNTIME_UI_BASE_URL or RUNTIME_BASE_URL)")

        return config.chatui_url_template.format(
            runtime_ui_base_url=config.runtime_ui_base_url,
            app_id=app_id,
            workflow_id=workflow_id,
            capability_id=capability_id,
            chat_id=chat_id,
            token=token,
        )

    async def fetch_runtime_workflows(self, *, app_id: str, user_id: str, token: str | None = None) -> list[Any]:
        """
        Optional helper: fetch workflows from the runtime "Pack Loader" API.

        NOTE: Control planes should not rely on dynamic discovery for gating; keep a capability registry.
        This is provided only as a debugging/configuration aid.
        """
        config = self.ui_config()
        runtime_api_base_url = config.runtime_api_base_url
        if not runtime_api_base_url:
            logger.info("Runtime API base URL is not set; returning empty runtime workflow list")
            return []

        url = f"{runtime_api_base_url}/api/workflows/{app_id}/available"
        headers = {"Authorization": f"Bearer {token}"} if token else {}
        params = {"user_id": user_id}

        try:
            async with httpx.AsyncClient(timeout=10.0) as client:
                resp = await client.get(url, headers=headers, params=params)
                resp.raise_for_status()
                payload = resp.json()
        except Exception as e:
            logger.warning(f"Failed to fetch runtime workflows from {url}: {e}")
            return []

        if isinstance(payload, dict) and isinstance(payload.get("workflows"), list):
            return payload["workflows"]
        if isinstance(payload, list):
            return payload

        logger.warning(f"Unexpected runtime workflows payload shape: {type(payload)}")
        return []


runtime_manager = RuntimeManager()
