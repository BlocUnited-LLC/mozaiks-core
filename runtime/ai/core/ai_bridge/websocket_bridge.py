"""
WebSocket Bridge

Routes WebSocket connections appropriately:
- /ws/notifications/* → core's websocket_manager (notifications, plugin events)
- /ws/{workflow}/*    → mozaiksai runtime (chat, workflow execution)

This allows a unified WebSocket endpoint while keeping concerns separated.
"""

import logging
from typing import Optional, Callable, Awaitable
from fastapi import WebSocket, WebSocketDisconnect, HTTPException
import asyncio

logger = logging.getLogger("mozaiks_core.ai_bridge.websocket_bridge")


class WebSocketBridge:
    """
    Routes WebSocket connections to the appropriate handler.
    
    Core handles: notifications, plugin real-time events
    Runtime handles: chat streaming, workflow execution, tool interactions
    """
    
    def __init__(self):
        self._core_ws_manager = None
        self._runtime_available = False
        
    def initialize(self, core_ws_manager):
        """Initialize with core's WebSocket manager."""
        self._core_ws_manager = core_ws_manager
        
        # Check if runtime is available
        try:
            from core.ai_runtime.transport import SimpleTransport
            self._runtime_available = True
            logger.info("WebSocket bridge: AI runtime transport available")
        except ImportError:
            logger.warning("WebSocket bridge: AI runtime not available")
            self._runtime_available = False
    
    def is_runtime_route(self, path: str) -> bool:
        """
        Determine if a WebSocket path should go to the AI runtime.
        
        Runtime routes:
        - /ws/{workflow_name}/{app_id}/{chat_id}/{user_id}
        
        Core routes:
        - /ws/notifications/{user_id}
        - /ws/plugins/{user_id}
        """
        parts = path.strip("/").split("/")
        if len(parts) < 2:
            return False
            
        # If the second part is a known core route, it's not runtime
        core_routes = {"notifications", "plugins", "events", "system"}
        if len(parts) >= 2 and parts[1] in core_routes:
            return False
            
        # 4+ path segments with format /ws/{workflow}/{app}/{chat}/{user} = runtime
        if len(parts) >= 5:
            return True
            
        return False
    
    async def proxy_to_runtime(
        self,
        websocket: WebSocket,
        workflow_name: str,
        app_id: str,
        chat_id: str,
        user_id: str,
        token: Optional[str] = None,
    ):
        """
        Proxy a WebSocket connection to the AI runtime.
        
        This handles the handoff from core's routing to runtime's
        actual WebSocket handling.
        """
        if not self._runtime_available:
            await websocket.close(code=1011, reason="AI runtime not available")
            return
            
        try:
            # Import runtime components
            from core.ai_runtime.transport import SimpleTransport
            from core.ai_runtime.data.persistence import AG2PersistenceManager
            from core.ai_runtime.workflow.workflow_manager import workflow_manager
            
            # Validate workflow exists
            if not workflow_manager.get_workflow_info(workflow_name):
                await websocket.close(code=4004, reason=f"Unknown workflow: {workflow_name}")
                return
            
            # The runtime's websocket handler expects to manage the full lifecycle
            # We create a transport and let it handle the connection
            transport = SimpleTransport()
            persistence = AG2PersistenceManager()
            
            # Accept the WebSocket
            await websocket.accept()
            logger.info(f"WebSocket bridge: proxying to runtime for {workflow_name}/{chat_id}")
            
            # Hand off to runtime - this is a simplified version
            # The actual implementation would use shared_app's websocket_endpoint logic
            await self._handle_runtime_websocket(
                websocket=websocket,
                transport=transport,
                persistence=persistence,
                workflow_name=workflow_name,
                app_id=app_id,
                chat_id=chat_id,
                user_id=user_id,
            )
            
        except ImportError as e:
            logger.error(f"Runtime import failed: {e}")
            await websocket.close(code=1011, reason="Runtime initialization failed")
        except Exception as e:
            logger.error(f"Runtime proxy error: {e}")
            await websocket.close(code=1011, reason="Internal error")
    
    async def _handle_runtime_websocket(
        self,
        websocket: WebSocket,
        transport,
        persistence,
        workflow_name: str,
        app_id: str,
        chat_id: str,
        user_id: str,
    ):
        """
        Handle the runtime WebSocket connection lifecycle.
        
        This is a bridge implementation - in production you might
        want to directly mount shared_app's routes instead.
        """
        try:
            # Register the connection with transport
            await transport.register(chat_id, websocket)
            
            # Message loop
            while True:
                try:
                    data = await websocket.receive_json()
                    
                    # Forward to runtime for processing
                    # The actual processing happens in workflow execution
                    message_type = data.get("type", "message")
                    
                    if message_type == "ping":
                        await websocket.send_json({"type": "pong"})
                    elif message_type == "message":
                        # This would trigger workflow execution
                        # For the bridge, we acknowledge and the runtime handles it
                        await websocket.send_json({
                            "type": "ack",
                            "chat_id": chat_id,
                        })
                        
                except WebSocketDisconnect:
                    break
                except Exception as e:
                    logger.error(f"WebSocket message error: {e}")
                    break
                    
        finally:
            await transport.unregister(chat_id)
            logger.info(f"WebSocket bridge: disconnected {chat_id}")


# Singleton
_bridge: Optional[WebSocketBridge] = None


def get_websocket_bridge() -> WebSocketBridge:
    """Get the singleton WebSocket bridge."""
    global _bridge
    if _bridge is None:
        _bridge = WebSocketBridge()
    return _bridge
