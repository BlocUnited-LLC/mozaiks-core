# backend/main.py
import uvicorn
from fastapi import FastAPI, WebSocket, WebSocketDisconnect
from fastapi.middleware.cors import CORSMiddleware
from core.director import app as director_app, load_config
from core.config.database import (
    verify_connection,
    create_enterprise_index,
    ensure_enterprise_exists,
    users_collection
)
import logging
import asyncio
from core.plugin_manager import plugin_manager, register_websockets
from core.websocket_manager import websocket_manager

# 🔥 Load app name from branding config
try:
    theme_config = load_config("theme_config.json")
    branding = theme_config.get("branding", {})
    APP_NAME = branding.get("app_name", "Mozaiks")
except Exception as e:
    APP_NAME = "Mozaiks"
    logging.error(f"Failed to load branding config: {e}")

logger = logging.getLogger("mozaiks")

app = FastAPI(title=f"{APP_NAME} API")

# Add CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:5173"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Core WebSocket route
@app.websocket("/ws/notifications/{user_id}")
async def notifications_websocket(websocket: WebSocket, user_id: str):
    await websocket_manager.connect(user_id, websocket)
    try:
        while True:
            await websocket.receive_text()
    except WebSocketDisconnect:
        websocket_manager.disconnect(user_id, websocket)
        
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
