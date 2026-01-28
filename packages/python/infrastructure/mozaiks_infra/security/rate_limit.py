# backend/security/rate_limit.py
from __future__ import annotations

import time
from collections import deque
from dataclasses import dataclass

from fastapi import HTTPException, Request, status


@dataclass(frozen=True)
class RateLimitPolicy:
    limit: int
    window_s: int


class InMemoryRateLimiter:
    """Process-local, best-effort rate limiter.

    Not a substitute for a distributed limiter (e.g., Redis) in multi-worker deployments.
    """

    def __init__(self) -> None:
        self._hits: dict[str, deque[float]] = {}

    def allow(self, key: str, *, limit: int, window_s: int) -> bool:
        now = time.monotonic()
        window_start = now - float(window_s)

        bucket = self._hits.get(key)
        if bucket is None:
            bucket = deque()
            self._hits[key] = bucket

        while bucket and bucket[0] <= window_start:
            bucket.popleft()

        if len(bucket) >= int(limit):
            return False

        bucket.append(now)
        return True


rate_limiter = InMemoryRateLimiter()


def _client_ip(request: Request) -> str:
    # Uvicorn's proxy_headers=True will populate request.client from X-Forwarded-For.
    if request.client and request.client.host:
        return request.client.host
    forwarded = request.headers.get("x-forwarded-for")
    if forwarded:
        return forwarded.split(",")[0].strip()
    return "unknown"


def enforce_rate_limit(
    request: Request,
    *,
    bucket: str,
    policy: RateLimitPolicy,
    identity: str | None = None,
) -> None:
    client = _client_ip(request)
    suffix = identity or "-"
    key = f"{bucket}:{client}:{suffix}"

    if rate_limiter.allow(key, limit=policy.limit, window_s=policy.window_s):
        return

    raise HTTPException(
        status_code=status.HTTP_429_TOO_MANY_REQUESTS,
        detail="Too many requests",
        headers={"Retry-After": str(int(policy.window_s))},
    )

