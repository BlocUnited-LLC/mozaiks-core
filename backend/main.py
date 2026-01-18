# backend/main.py
"""
MozaiksCore Main Entry Point

SECURITY: This file defines the WebSocket routes with proper JWT authentication.
The user_id in the path is a ROUTING HINT only - identity is derived from JWT.
"""
import uvicorn
from fastapi import FastAPI, WebSocket, WebSocketDisconnect, HTTPException, status
from fastapi.middleware.cors import CORSMiddleware
from core.director import app as director_app, load_config, APP_ID
from core.config.database import (
    verify_connection,
    create_enterprise_index,
    ensure_enterprise_exists,
    users_collection
)
from core.http.websocket_auth import get_ws_auth, WS_SUBPROTOCOL
import logging
import asyncio
from core.plugin_manager import plugin_manager, register_websockets
from core.websocket_manager import websocket_manager
from jose import jwt, JWTError
from security.constants import SECRET_KEY, ALGORITHM
import os

# 🔥 Load app name from branding config
try:
    theme_config = load_config("theme_config.json")
    branding = theme_config.get("branding", {})
    APP_NAME = branding.get("app_name", "Mozaiks")
except Exception as e:
    APP_NAME = "Mozaiks"
    logging.error(f"Failed to load branding config: {e}")

logger = logging.getLogger("mozaiks")

app = FastAPI(title=f"{APP_NAME} API ({APP_ID})")

# Add CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:5173"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


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
            # TODO: Add full JWKS validation for external tokens
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
        # Allow "_" as wildcard for legacy clients
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
    logger.info(f"🚀 Starting {APP_NAME} API")
    global plugin_manager
    plugin_manager = await plugin_manager.init_async()

    await verify_connection()
    await create_enterprise_index()
    await ensure_enterprise_exists()

    # 👇 Register only plugin WebSocket routes, not core chat (we defined it directly above)
    await register_websockets(app)

if __name__ == "__main__":
    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)
