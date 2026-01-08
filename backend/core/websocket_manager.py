# backend/core/websocket_manager.py
from fastapi import WebSocket
import logging
from typing import Dict, List

logger = logging.getLogger("mozaiks_core.websocket_manager")

class WebSocketManager:
    def __init__(self):
        # Mapping from user_id to a list of active WebSocket connections
        self.active_connections: Dict[str, List[WebSocket]] = {}

    async def connect(self, user_id: str, websocket: WebSocket):
        """Accept the connection and add it to active connections for this user."""
        await websocket.accept()
        if user_id in self.active_connections:
            self.active_connections[user_id].append(websocket)
        else:
            self.active_connections[user_id] = [websocket]
        logger.info(f"User {user_id} connected via WebSocket.")

    def disconnect(self, user_id: str, websocket: WebSocket):
        """Remove the connection for the given user_id."""
        if user_id in self.active_connections:
            try:
                self.active_connections[user_id].remove(websocket)
            except ValueError:
                pass
            if not self.active_connections[user_id]:
                del self.active_connections[user_id]
        logger.info(f"User {user_id} disconnected from WebSocket.")

    async def send_to_user(self, user_id: str, message: dict):
        """Send a JSON message to all active WebSocket connections for a user."""
        connections = self.active_connections.get(user_id, [])
        for connection in connections:
            try:
                await connection.send_json(message)
                logger.debug(f"Sent message to {user_id}: {message}")
            except Exception as e:
                logger.error(f"Error sending message to {user_id}: {e}")

    async def broadcast(self, message: dict):
        """Broadcast a JSON message to all connected users."""
        for user_id, connections in self.active_connections.items():
            for connection in connections:
                try:
                    await connection.send_json(message)
                except Exception as e:
                    logger.error(f"Error broadcasting message to {user_id}: {e}")

# Create a singleton instance
websocket_manager = WebSocketManager()
