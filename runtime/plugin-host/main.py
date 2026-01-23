from __future__ import annotations

import asyncio
import inspect
import json
import logging
from typing import Any, Dict

from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import JSONResponse

from auth import AuthError, JWKSClient, authenticate_token, get_bearer_token
from config import Settings, load_settings
from plugin_loader import PluginLoader

settings = load_settings()

logging.basicConfig(level=settings.log_level)
logger = logging.getLogger("plugin_host")

app = FastAPI(title="Mozaiks Plugin Runtime Host")

plugin_loader = PluginLoader(settings.plugins_dir)
jwks_client = JWKSClient(settings.jwks_url, settings.jwks_cache_ttl_seconds)


@app.on_event("startup")
async def startup() -> None:
    try:
        plugin_loader.scan_plugins()
    except Exception:
        logger.exception("Initial plugin scan failed")

    if settings.watch_plugins:
        asyncio.create_task(_watch_plugins())


async def _watch_plugins() -> None:
    while True:
        await asyncio.sleep(settings.plugin_scan_interval_seconds)
        try:
            plugin_loader.scan_plugins()
        except Exception:
            logger.exception("Plugin scan failed")


@app.get("/health")
async def health() -> Dict[str, Any]:
    return {"status": "ok", "plugins_loaded": plugin_loader.count_loaded()}


@app.get("/api/plugins")
async def list_plugins() -> Dict[str, Any]:
    plugin_loader.scan_plugins()
    return {"plugins": plugin_loader.list_plugins()}


@app.post("/api/plugins/{plugin_name}/execute")
async def execute_plugin(plugin_name: str, request: Request) -> Any:
    # Dev mode: skip JWT validation and inject mock user context.
    if settings.skip_auth:
        logger.warning("SKIP_AUTH enabled - using mock user context (dev mode only)")
        user = None
    else:
        try:
            token = get_bearer_token(request.headers.get("Authorization"))
            user = await authenticate_token(token, settings, jwks_client)
        except AuthError as exc:
            raise HTTPException(status_code=exc.status_code, detail=str(exc)) from exc

    raw_body = await request.body()
    if raw_body:
        try:
            payload = json.loads(raw_body)
        except json.JSONDecodeError as exc:
            raise HTTPException(status_code=400, detail="Invalid JSON body") from exc
    else:
        payload = {}

    if not isinstance(payload, dict):
        raise HTTPException(status_code=400, detail="Request body must be a JSON object")

    record = plugin_loader.ensure_loaded(plugin_name)
    if record is None:
        return JSONResponse(
            status_code=404,
            content={"error": "Plugin not found", "plugin": plugin_name},
        )
    if record.status != "loaded" or not record.entrypoint:
        return JSONResponse(
            status_code=500,
            content={
                "error": "Plugin load failed",
                "details": record.error or "Unknown error",
                "plugin": plugin_name,
            },
        )

    data = dict(payload)

    # Inject user context (mock for dev mode, real for production).
    if settings.skip_auth:
        data["user_id"] = "dev-user-001"
        data["_context"] = {
            "user_id": "dev-user-001",
            "username": "dev-user",
            "roles": ["user"],
            "is_superadmin": False,
        }
    else:
        data["user_id"] = user.user_id
        data["_context"] = {
            "user_id": user.user_id,
            "username": user.username,
            "roles": user.roles,
            "is_superadmin": user.is_superadmin,
        }

    try:
        result = record.entrypoint(data)
        if inspect.isawaitable(result):
            result = await result
    except Exception as exc:
        logger.exception("Plugin execution failed: %s", plugin_name)
        return JSONResponse(
            status_code=500,
            content={"error": "Plugin execution failed", "details": str(exc)},
        )

    if not isinstance(result, dict):
        return JSONResponse(
            status_code=500,
            content={
                "error": "Plugin returned invalid response",
                "details": f"Expected dict, got {type(result).__name__}",
            },
        )

    return result
