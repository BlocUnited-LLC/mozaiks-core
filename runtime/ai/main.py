# backend/main.py
"""
MozaiksCore Main Entry Point

SECURITY: This file defines the WebSocket routes with proper JWT authentication.
The user_id in the path is a ROUTING HINT only - identity is derived from JWT.

CLOUD NATIVE:
- Health check at GET /health (Azure liveness probe)
- Readiness check at GET /ready (Azure readiness probe)
- Listens on port 8080 (Azure Container Apps default)
- Configuration via environment variables (no appsettings.json)
- APP_ID and APP_TIER injected by Provisioning Agent
"""
import uvicorn
from fastapi import FastAPI, WebSocket, WebSocketDisconnect, HTTPException, status
from fastapi.responses import JSONResponse
from fastapi.middleware.cors import CORSMiddleware
from core.director import app as director_app, load_config, APP_ID
from core.config.database import (
    verify_connection,
    create_enterprise_index,
    ensure_enterprise_exists,
    users_collection
)
from core.http_utils.websocket_auth import get_ws_auth, WS_SUBPROTOCOL
import logging
import asyncio
from core.plugin_manager import plugin_manager, register_websockets
from core.websocket_manager import websocket_manager
from jose import jwt, JWTError
from security.constants import SECRET_KEY, ALGORITHM
import os

# ============================================================================
# CLOUD NATIVE CONFIGURATION
# ============================================================================
# These are injected by the Provisioning Agent in Azure Container Apps
APP_TIER = os.getenv("APP_TIER", "free")  # Subscription tier: free, pro, enterprise
PORT = int(os.getenv("PORT", "8080"))  # Azure Container Apps expects 8080

# Startup state tracking for readiness probe
_startup_complete = False
_startup_error: str | None = None

# 🔥 Load app name from branding config
try:
    theme_config = load_config("theme_config.json")
    branding = theme_config.get("branding", {})
    APP_NAME = branding.get("app_name", "Mozaiks")
except Exception as e:
    APP_NAME = "Mozaiks"
    logging.error(f"Failed to load branding config: {e}")

logger = logging.getLogger("mozaiks")

app = FastAPI(
    title=f"{APP_NAME} API ({APP_ID})",
    description="MozaiksCore Runtime - Cloud Native",
    version="1.0.0",
)


# ============================================================================
# HEALTH CHECKS (Required for Azure Container Apps)
# ============================================================================

@app.get("/health", tags=["health"])
async def health_check():
    """
    Liveness probe endpoint for Azure Container Apps.
    
    Returns 200 OK if the process is running.
    This endpoint should NOT check dependencies (that's what /ready is for).
    
    Azure will restart the container if this fails repeatedly.
    
    Contract v1.0.0: Includes plugins_loaded count.
    """
    return JSONResponse(
        status_code=200,
        content={
            "status": "healthy",
            "app_id": APP_ID,
            "app_tier": APP_TIER,
            "plugins_loaded": len(plugin_manager.plugins),
        }
    )


@app.get("/ready", tags=["health"])
async def readiness_check():
    """
    Readiness probe endpoint for Azure Container Apps.
    
    Returns 200 OK only when the app is ready to serve traffic.
    Checks that startup completed and critical services are available.
    
    Azure will stop sending traffic if this fails.
    """
    if not _startup_complete:
        return JSONResponse(
            status_code=503,
            content={
                "status": "not_ready",
                "reason": _startup_error or "startup_in_progress",
                "app_id": APP_ID,
            }
        )
    
    return JSONResponse(
        status_code=200,
        content={
            "status": "ready",
            "app_id": APP_ID,
            "app_tier": APP_TIER,
        }
    )


@app.get("/info", tags=["health"])
async def app_info():
    """
    Returns app configuration info (non-sensitive).
    Useful for debugging and Provisioning Agent verification.
    """
    return JSONResponse(
        status_code=200,
        content={
            "app_id": APP_ID,
            "app_tier": APP_TIER,
            "app_name": APP_NAME,
            "port": PORT,
            "ready": _startup_complete,
        }
    )


# Configure CORS based on environment (matches director.py pattern)
frontend_url = os.getenv("FRONTEND_URL", "http://localhost:5173")
cors_origins = [frontend_url]
if os.getenv("ADDITIONAL_CORS_ORIGINS"):
    additional_origins = os.getenv("ADDITIONAL_CORS_ORIGINS", "").split(",")
    cors_origins.extend([origin.strip() for origin in additional_origins if origin.strip()])

app.add_middleware(
    CORSMiddleware,
    allow_origins=cors_origins,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# ============================================================================
# CONTRACT v1.0.0: Runtime Version Header
# ============================================================================
RUNTIME_VERSION = "1.0.0"

@app.middleware("http")
async def add_runtime_version_header(request, call_next):
    """
    Add X-Mozaiks-Runtime-Version header to all HTTP responses.
    Required by Runtime ↔ Platform Contract v1.0.0.
    """
    response = await call_next(request)
    response.headers["X-Mozaiks-Runtime-Version"] = RUNTIME_VERSION
    return response


async def validate_ws_token(token: str) -> dict:
    """
    Validate JWT token for WebSocket connections.
    
    SECURITY: This extracts identity from the token, NOT from path params.
    """
    if not token:
        raise HTTPException(status_code=401, detail="No token provided")
    
    try:
        # Determine auth mode
        auth_mode = os.getenv("MOZAIKS_AUTH_MODE", "external").strip().lower()
        
        if auth_mode == "local":
            # Local mode: HS256 validation
            payload = jwt.decode(token, SECRET_KEY, algorithms=[ALGORITHM])
            user_id = payload.get("user_id") or payload.get("sub")
            username = payload.get("sub") or payload.get("username")
        else:
            # External/platform mode: need JWKS validation
            # For now, fall back to HS256 for app-scoped tokens
            try:
                payload = jwt.decode(token, SECRET_KEY, algorithms=[ALGORITHM])
                user_id = payload.get("user_id") or payload.get("sub")
                username = payload.get("sub") or payload.get("username")
            except JWTError:
                # Token might be external OIDC - reject for now
                # In production, this should validate against JWKS
                raise HTTPException(status_code=401, detail="Invalid token")
        
        if not user_id:
            raise HTTPException(status_code=401, detail="Token missing user_id")
        
        return {"user_id": user_id, "username": username}
    
    except JWTError as e:
        logger.warning(f"WebSocket JWT validation failed: {e}")
        raise HTTPException(status_code=401, detail="Invalid token")


# Core WebSocket route with proper auth
@app.websocket("/ws/notifications/{user_id_hint}")
async def notifications_websocket(websocket: WebSocket, user_id_hint: str):
    """
    WebSocket endpoint for real-time notifications.
    
    SECURITY:
    - user_id_hint in path is for ROUTING only
    - Actual identity is derived from JWT in Sec-WebSocket-Protocol
    - Connection is rejected if JWT user_id doesn't match path hint
    """
    # Extract token from WebSocket protocol header
    token, subprotocol = get_ws_auth(websocket)
    
    if not token:
        # Check query param as fallback (less secure, but browser-compatible)
        token = websocket.query_params.get("access_token")
        if not token:
            logger.warning(f"WebSocket connection rejected: no token provided")
            await websocket.close(code=4001, reason="Authentication required")
            return
    
    try:
        # Validate token and extract user identity
        user = await validate_ws_token(token)
        actual_user_id = user["user_id"]
        
        # SECURITY: Verify path hint matches JWT identity
        # Allow "_" as wildcard for older clients
        if user_id_hint != "_" and user_id_hint != actual_user_id:
            logger.warning(f"WebSocket user_id mismatch: path={user_id_hint} jwt={actual_user_id}")
            await websocket.close(code=4003, reason="User ID mismatch")
            return
        
        # Accept connection with subprotocol if provided
        if subprotocol:
            await websocket.accept(subprotocol=subprotocol)
        else:
            await websocket.accept()
        
        # Register connection using JWT-derived user_id
        await websocket_manager.connect(actual_user_id, websocket)
        logger.info(f"WebSocket connected: user={actual_user_id} app={APP_ID}")
        
        try:
            while True:
                # Keep connection alive, handle incoming messages if needed
                data = await websocket.receive_text()
                # Could handle client-to-server messages here
        except WebSocketDisconnect:
            websocket_manager.disconnect(actual_user_id, websocket)
            logger.info(f"WebSocket disconnected: user={actual_user_id}")
            
    except HTTPException as e:
        logger.warning(f"WebSocket auth failed: {e.detail}")
        await websocket.close(code=4001, reason=e.detail)
    except Exception as e:
        logger.error(f"WebSocket error: {e}")
        await websocket.close(code=4000, reason="Internal error")
        
# Mount core app
app.mount("/", director_app)

@app.on_event("startup")
async def startup_event():
    """Initialize services and mark app as ready for traffic."""
    global _startup_complete, _startup_error, plugin_manager
    
    logger.info(f"🚀 Starting {APP_NAME} API (app_id={APP_ID}, tier={APP_TIER}, port={PORT})")
    
    try:
        # Initialize plugin system
        plugin_manager = await plugin_manager.init_async()

        # Verify database connection
        await verify_connection()
        await create_enterprise_index()
        await ensure_enterprise_exists()

        # Register plugin WebSocket routes
        await register_websockets(app)
        
        # Mark as ready for traffic
        _startup_complete = True
        logger.info(f"✅ {APP_NAME} API ready on port {PORT}")
        
    except Exception as e:
        _startup_error = str(e)
        logger.critical(f"❌ Startup failed: {e}")
        # Don't raise - let health checks report the failure
        # Azure will handle the restart based on probe failures

@app.on_event("shutdown")
async def shutdown_event():
    """Graceful shutdown handler."""
    global _startup_complete
    _startup_complete = False
    logger.info(f"👋 Shutting down {APP_NAME} API")


if __name__ == "__main__":
    # Cloud Native: Listen on 0.0.0.0:8080 (Azure Container Apps default)
    # No HTTPS redirect - SSL is terminated at the Azure ingress
    uvicorn.run(
        "main:app",
        host="0.0.0.0",
        port=PORT,
        reload=os.getenv("ENV", "development").lower() != "production",
    )
