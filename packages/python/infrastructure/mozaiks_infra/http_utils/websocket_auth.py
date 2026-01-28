# backend/core/http/websocket_auth.py
from __future__ import annotations

import re

from starlette.websockets import WebSocket

WS_SUBPROTOCOL = "mozaiks"

_JWT_RE = re.compile(r"^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$")


def ws_protocols(websocket: WebSocket) -> list[str]:
    raw = websocket.headers.get("sec-websocket-protocol") or ""
    return [p.strip() for p in raw.split(",") if p.strip()]


def ws_select_subprotocol(protocols: list[str]) -> str | None:
    return WS_SUBPROTOCOL if any(p.lower() == WS_SUBPROTOCOL for p in protocols) else None


def extract_bearer(token: str | None) -> str | None:
    if not token:
        return None
    token = token.strip()
    if token.lower().startswith("bearer "):
        return token.split(" ", 1)[1].strip()
    return token or None


def ws_extract_token(websocket: WebSocket, protocols: list[str]) -> str | None:
    for candidate in protocols:
        if candidate.lower() == WS_SUBPROTOCOL:
            continue
        if _JWT_RE.fullmatch(candidate):
            return candidate

    # Security policy: never accept JWTs in the URL (query params) or via headers.
    # Browsers cannot set custom headers for WS; Sec-WebSocket-Protocol is the supported path.
    return None


def get_ws_auth(websocket: WebSocket) -> tuple[str | None, str | None]:
    """Return (token, selected_subprotocol) for this WebSocket handshake."""
    protocols = ws_protocols(websocket)
    return ws_extract_token(websocket, protocols), ws_select_subprotocol(protocols)
