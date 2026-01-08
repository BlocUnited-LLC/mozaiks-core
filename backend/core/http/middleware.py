# backend/core/http/middleware.py
from __future__ import annotations

import time
import uuid
from typing import Callable

from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
from starlette.responses import JSONResponse, Response


class CorrelationIdMiddleware(BaseHTTPMiddleware):
    def __init__(self, app, header_name: str = "x-correlation-id") -> None:
        super().__init__(app)
        self._header_name = header_name.lower()

    async def dispatch(self, request: Request, call_next: Callable) -> Response:
        incoming = request.headers.get(self._header_name)
        correlation_id = (incoming or "").strip() or str(uuid.uuid4())
        request.state.correlation_id = correlation_id
        response = await call_next(request)
        response.headers[self._header_name] = correlation_id
        return response


class RequestSizeLimitMiddleware(BaseHTTPMiddleware):
    def __init__(self, app, *, max_body_bytes: int) -> None:
        super().__init__(app)
        self._max = int(max_body_bytes)

    async def dispatch(self, request: Request, call_next: Callable) -> Response:
        content_length = request.headers.get("content-length")
        if content_length:
            try:
                if int(content_length) > self._max:
                    return JSONResponse(
                        status_code=413,
                        content={"message": "Request too large"},
                    )
            except Exception:
                # Malformed Content-Length - treat as bad request.
                return JSONResponse(status_code=400, content={"message": "Invalid Content-Length"})
        return await call_next(request)


class SecurityHeadersMiddleware(BaseHTTPMiddleware):
    def __init__(self, app, *, hsts: bool = False) -> None:
        super().__init__(app)
        self._hsts = hsts

    async def dispatch(self, request: Request, call_next: Callable) -> Response:
        start = time.time()
        response = await call_next(request)

        # Basic hardening headers (safe defaults for APIs).
        response.headers.setdefault("x-content-type-options", "nosniff")
        response.headers.setdefault("x-frame-options", "DENY")
        response.headers.setdefault("referrer-policy", "no-referrer")
        response.headers.setdefault("permissions-policy", "geolocation=(), microphone=(), camera=()")
        response.headers.setdefault("cross-origin-resource-policy", "same-site")

        if self._hsts and request.url.scheme == "https":
            response.headers.setdefault("strict-transport-security", "max-age=63072000; includeSubDomains")

        # Minimal timing signal for clients/observability.
        response.headers.setdefault("x-response-time-ms", str(int((time.time() - start) * 1000)))
        return response

