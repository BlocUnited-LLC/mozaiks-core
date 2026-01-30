# backend/app/connectors/managed.py
from __future__ import annotations

import asyncio
import json
import logging
import random
from dataclasses import dataclass
from typing import Any
from urllib.parse import urljoin

import aiohttp

from mozaiks_app.connectors.base import (
    CancelRequest,
    CheckoutRequest,
    CheckoutResponse,
    OkResponse,
    PaymentConnector,
    PaymentStatus,
    SubscriptionScope,
)

logger = logging.getLogger("mozaiks_sdk.connectors.managed")


def _log_json(level: int, payload: dict[str, Any]) -> None:
    logger.log(level, json.dumps(payload, separators=(",", ":"), default=str))


class GatewayRequestError(RuntimeError):
    def __init__(self, message: str, *, status_code: int | None = None) -> None:
        super().__init__(message)
        self.status_code = status_code


@dataclass(frozen=True)
class ManagedGatewayConfig:
    base_url: str
    api_key: str | None = None
    total_timeout_s: float = 10.0
    connect_timeout_s: float = 5.0
    max_retries: int = 2
    backoff_initial_s: float = 0.25
    backoff_max_s: float = 2.0


class ManagedHttpClient:
    def __init__(self, config: ManagedGatewayConfig) -> None:
        self._config = config

    def _make_headers(
        self,
        *,
        correlation_id: str,
        user_jwt: str | None,
        extra: dict[str, str] | None,
    ) -> dict[str, str]:
        headers: dict[str, str] = {"x-correlation-id": correlation_id}

        if user_jwt:
            headers["Authorization"] = f"Bearer {user_jwt}"

        # Optional scoped server-to-server key (if the gateway requires it).
        if self._config.api_key:
            headers["x-mozaiks-app-key"] = self._config.api_key

        if extra:
            headers.update(extra)

        return headers

    def _url(self, path: str) -> str:
        base = self._config.base_url.rstrip("/") + "/"
        return urljoin(base, path.lstrip("/"))

    async def request_json(
        self,
        *,
        method: str,
        path: str,
        correlation_id: str,
        user_jwt: str | None,
        params: dict[str, Any] | None = None,
        json_body: Any | None = None,
        extra_headers: dict[str, str] | None = None,
        retry: bool,
        log_context: dict[str, Any] | None = None,
    ) -> dict[str, Any]:
        url = self._url(path)

        timeout = aiohttp.ClientTimeout(
            total=self._config.total_timeout_s,
            connect=self._config.connect_timeout_s,
        )

        ctx: dict[str, Any] = {"scope": None, "appId": None}
        if log_context:
            ctx.update(log_context)

        attempts = 1 + (self._config.max_retries if retry else 0)
        backoff = self._config.backoff_initial_s

        for attempt in range(1, attempts + 1):
            _log_json(
                logging.INFO,
                {
                    "event": "gateway.request",
                    "method": method,
                    "path": path,
                    "attempt": attempt,
                    "correlationId": correlation_id,
                    **ctx,
                },
            )

            try:
                headers = self._make_headers(
                    correlation_id=correlation_id,
                    user_jwt=user_jwt,
                    extra=extra_headers,
                )

                async with aiohttp.ClientSession(timeout=timeout) as session:
                    async with session.request(method, url, params=params, json=json_body, headers=headers) as resp:
                        content_type = resp.headers.get("Content-Type", "")
                        text = await resp.text()

                        _log_json(
                            logging.INFO,
                            {
                                "event": "gateway.response",
                                "method": method,
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
                            raise GatewayRequestError(
                                f"Gateway request failed: {method} {path} ({resp.status})",
                                status_code=resp.status,
                            )

                        if "application/json" in content_type.lower():
                            return json.loads(text) if text else {}

                        return {"raw": text} if text else {}

            except (aiohttp.ClientError, asyncio.TimeoutError) as e:
                _log_json(
                    logging.WARNING,
                    {
                        "event": "gateway.error",
                        "method": method,
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

                raise GatewayRequestError(f"Gateway request error: {method} {path}") from e

        raise GatewayRequestError(f"Gateway request exhausted retries: {method} {path}")


class ManagedPaymentConnector(PaymentConnector):
    def __init__(self, http: ManagedHttpClient) -> None:
        self._http = http

    async def checkout(
        self,
        *,
        payload: CheckoutRequest,
        correlation_id: str,
        user_jwt: str | None = None,
    ) -> CheckoutResponse:
        data = await self._http.request_json(
            method="POST",
            path="/api/mozaiks/pay/checkout",
            correlation_id=correlation_id,
            user_jwt=user_jwt,
            json_body=payload.model_dump(exclude_none=True),
            retry=False,
            log_context={"scope": payload.scope, "appId": payload.appId},
        )
        return CheckoutResponse.model_validate(data)

    async def subscription_status(
        self,
        *,
        scope: SubscriptionScope,
        appId: str | None,
        correlation_id: str,
        user_jwt: str | None = None,
    ) -> PaymentStatus:
        params: dict[str, Any] = {"scope": scope}
        if appId:
            params["appId"] = appId

        try:
            data = await self._http.request_json(
                method="GET",
                path="/api/mozaiks/pay/subscription-status",
                correlation_id=correlation_id,
                user_jwt=user_jwt,
                params=params,
                retry=True,
                log_context={"scope": scope, "appId": appId},
            )
            status = PaymentStatus.model_validate(data)
            status.scope = status.scope or scope
            status.appId = status.appId or appId
            return status
        except GatewayRequestError as e:
            _log_json(
                logging.ERROR,
                {
                    "event": "payment.subscription_status_error",
                    "correlationId": correlation_id,
                    "scope": scope,
                    "appId": appId,
                    "error": str(e),
                    "status": getattr(e, "status_code", None),
                },
            )
            return PaymentStatus(
                scope=scope,
                appId=appId,
                active=False,
                status="unavailable",
                unavailableReason="gateway_unreachable",
            )

    async def cancel(
        self,
        *,
        payload: CancelRequest,
        correlation_id: str,
        user_jwt: str | None = None,
    ) -> OkResponse:
        try:
            data = await self._http.request_json(
                method="POST",
                path="/api/mozaiks/pay/cancel",
                correlation_id=correlation_id,
                user_jwt=user_jwt,
                json_body=payload.model_dump(exclude_none=True),
                retry=False,
                log_context={"scope": payload.scope, "appId": payload.appId},
            )
            return OkResponse.model_validate(data) if data else OkResponse(ok=True)
        except GatewayRequestError as e:
            _log_json(
                logging.ERROR,
                {
                    "event": "payment.cancel_error",
                    "correlationId": correlation_id,
                    "scope": payload.scope,
                    "appId": payload.appId,
                    "error": str(e),
                    "status": getattr(e, "status_code", None),
                },
            )
            return OkResponse(ok=False)
