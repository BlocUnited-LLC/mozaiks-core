from __future__ import annotations

import asyncio
import json
import logging
import random
from dataclasses import dataclass
from typing import Any
from urllib.parse import urljoin

import aiohttp

logger = logging.getLogger("mozaiks_core.insights")


def _log_json(level: int, payload: dict[str, Any]) -> None:
    logger.log(level, json.dumps(payload, separators=(",", ":"), default=str))


class InsightsRequestError(RuntimeError):
    def __init__(self, message: str, *, status_code: int | None = None) -> None:
        super().__init__(message)
        self.status_code = status_code


@dataclass(frozen=True)
class InsightsClientConfig:
    base_url: str
    sdk_version: str
    mozaiks_app_id: str | None = None
    mozaiks_api_key: str | None = None
    internal_api_key: str | None = None
    total_timeout_s: float = 10.0
    connect_timeout_s: float = 5.0
    max_retries: int = 2
    backoff_initial_s: float = 0.25
    backoff_max_s: float = 2.0


class InsightsClient:
    def __init__(self, config: InsightsClientConfig) -> None:
        self._config = config

    def _url(self, path: str) -> str:
        base = self._config.base_url.rstrip("/") + "/"
        return urljoin(base, path.lstrip("/"))

    def _headers(self, *, correlation_id: str) -> dict[str, str]:
        headers: dict[str, str] = {
            "X-Correlation-Id": correlation_id,
            "X-Mozaiks-Sdk-Version": self._config.sdk_version,
        }

        mozaiks_api_key = (self._config.mozaiks_api_key or "").strip()
        mozaiks_app_id = (self._config.mozaiks_app_id or "").strip()
        internal_api_key = (self._config.internal_api_key or "").strip()

        if mozaiks_api_key:
            if mozaiks_app_id:
                headers["X-Mozaiks-App-Id"] = mozaiks_app_id
            headers["X-Mozaiks-Api-Key"] = mozaiks_api_key
            return headers

        if internal_api_key:
            headers["X-Internal-Api-Key"] = internal_api_key

        return headers

    async def post_json(
        self,
        *,
        path: str,
        correlation_id: str,
        payload: Any,
        retry: bool = True,
        log_context: dict[str, Any] | None = None,
    ) -> dict[str, Any]:
        url = self._url(path)

        timeout = aiohttp.ClientTimeout(
            total=self._config.total_timeout_s,
            connect=self._config.connect_timeout_s,
        )

        ctx: dict[str, Any] = {}
        if log_context:
            ctx.update(log_context)

        attempts = 1 + (self._config.max_retries if retry else 0)
        backoff = self._config.backoff_initial_s

        for attempt in range(1, attempts + 1):
            _log_json(
                logging.INFO,
                {
                    "event": "insights.request",
                    "method": "POST",
                    "path": path,
                    "attempt": attempt,
                    "correlationId": correlation_id,
                    **ctx,
                },
            )

            try:
                headers = self._headers(correlation_id=correlation_id)
                async with aiohttp.ClientSession(timeout=timeout) as session:
                    async with session.post(url, json=payload, headers=headers) as resp:
                        text = await resp.text()
                        content_type = resp.headers.get("Content-Type", "")

                        _log_json(
                            logging.INFO,
                            {
                                "event": "insights.response",
                                "method": "POST",
                                "path": path,
                                "status": resp.status,
                                "correlationId": correlation_id,
                                **ctx,
                            },
                        )

                        retryable_status = resp.status in {429, 502, 503, 504} or resp.status >= 500
                        if retry and retryable_status and attempt < attempts:
                            await asyncio.sleep(min(backoff, self._config.backoff_max_s) + random.random() * 0.1)  # nosec B311
                            backoff = min(backoff * 2, self._config.backoff_max_s)
                            continue

                        if resp.status >= 400:
                            raise InsightsRequestError(
                                f"Insights request failed: POST {path} ({resp.status})",
                                status_code=resp.status,
                            )

                        if "application/json" in content_type.lower():
                            return json.loads(text) if text else {}

                        return {"raw": text} if text else {}

            except (aiohttp.ClientError, asyncio.TimeoutError) as e:
                _log_json(
                    logging.WARNING,
                    {
                        "event": "insights.error",
                        "method": "POST",
                        "path": path,
                        "error": str(e),
                        "attempt": attempt,
                        "correlationId": correlation_id,
                        **ctx,
                    },
                )

                if retry and attempt < attempts:
                    await asyncio.sleep(min(backoff, self._config.backoff_max_s) + random.random() * 0.1)  # nosec B311
                    backoff = min(backoff * 2, self._config.backoff_max_s)
                    continue

                raise InsightsRequestError(f"Insights request error: POST {path}") from e

        raise InsightsRequestError(f"Insights request exhausted retries: POST {path}")
