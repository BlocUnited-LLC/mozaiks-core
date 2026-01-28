# backend/core/notifications/channels/in_app.py
"""
In-App Notification Channel

Stores notifications in MongoDB and delivers via WebSocket for real-time updates.
This is the default channel and is always enabled.

No additional environment variables required.
"""

import logging
from datetime import datetime
from typing import Dict, List, Any, Optional
from bson import ObjectId

from mozaiks_infra.config.database import db
from mozaiks_infra.websocket_manager import websocket_manager
from .base import NotificationChannel

logger = logging.getLogger("mozaiks_core.notifications.channels.in_app")


class InAppChannel(NotificationChannel):
    """
    In-app notification delivery via database storage + WebSocket.
    
    Features:
    - Persistent storage in MongoDB
    - Real-time delivery via WebSocket
    - Read/unread tracking
    - Notification history
    """
    
    channel_id = "in_app"
    channel_name = "In-App Notifications"
    
    def __init__(self):
        self.collection_name = "notifications"
    
    def is_enabled(self) -> bool:
        """Always enabled - this is the core notification system."""
        return True
    
    async def send(
        self,
        user_id: str,
        notification_type: str,
        title: str,
        message: str,
        metadata: Optional[Dict[str, Any]] = None,
        template_data: Optional[Dict[str, Any]] = None
    ) -> bool:
        """
        Store notification in database and send via WebSocket.
        
        Args:
            user_id: Target user ID
            notification_type: Type of notification
            title: Notification title
            message: Notification message
            metadata: Additional data (links, action buttons, etc.)
            template_data: Template variables (not used for in-app)
            
        Returns:
            bool: True if stored successfully
        """
        try:
            collection = db[self.collection_name]
            
            notification_doc = {
                "user_id": user_id,
                "type": notification_type,
                "title": title,
                "message": message,
                "metadata": metadata or {},
                "read": False,
                "created_at": datetime.utcnow(),
                "channel": self.channel_id
            }
            
            result = await collection.insert_one(notification_doc)
            notification_id = str(result.inserted_id)
            
            # Send real-time update via WebSocket
            await self._send_websocket(user_id, {
                "id": notification_id,
                "type": notification_type,
                "title": title,
                "message": message,
                "metadata": metadata or {},
                "read": False,
                "created_at": notification_doc["created_at"].isoformat()
            })
            
            logger.info(f"In-app notification sent to user {user_id}: {notification_id}")
            return True
            
        except Exception as e:
            logger.error(f"Failed to send in-app notification: {e}")
            return False
    
    async def _send_websocket(self, user_id: str, notification_data: Dict[str, Any]):
        """Send notification via WebSocket for real-time updates."""
        try:
            await websocket_manager.send_to_user(user_id, {
                "type": "notification",
                "data": notification_data
            })
        except Exception as e:
            logger.warning(f"WebSocket delivery failed (user may be offline): {e}")
    
    async def get_user_notifications(
        self,
        user_id: str,
        limit: int = 50,
        unread_only: bool = False
    ) -> List[Dict[str, Any]]:
        """
        Get notifications for a user.
        
        Args:
            user_id: Target user ID
            limit: Maximum notifications to return
            unread_only: Only return unread notifications
            
        Returns:
            List of notification documents
        """
        collection = db[self.collection_name]
        query = {"user_id": user_id}
        if unread_only:
            query["read"] = False
        
        cursor = collection.find(query).sort("created_at", -1).limit(limit)
        notifications = await cursor.to_list(length=limit)
        
        # Convert ObjectId to string
        for n in notifications:
            n["_id"] = str(n["_id"])
            if "created_at" in n:
                n["created_at"] = n["created_at"].isoformat()
        
        return notifications
    
    async def mark_read(self, user_id: str, notification_id: str) -> bool:
        """Mark a notification as read."""
        collection = db[self.collection_name]
        result = await collection.update_one(
            {"_id": ObjectId(notification_id), "user_id": user_id},
            {"$set": {"read": True, "read_at": datetime.utcnow()}}
        )
        return result.modified_count > 0
    
    async def mark_all_read(self, user_id: str) -> int:
        """Mark all notifications as read for a user."""
        collection = db[self.collection_name]
        result = await collection.update_many(
            {"user_id": user_id, "read": False},
            {"$set": {"read": True, "read_at": datetime.utcnow()}}
        )
        return result.modified_count
    
    async def get_unread_count(self, user_id: str) -> int:
        """Get count of unread notifications."""
        collection = db[self.collection_name]
        return await collection.count_documents({"user_id": user_id, "read": False})
    
    async def delete_notification(self, user_id: str, notification_id: str) -> bool:
        """Delete a specific notification."""
        collection = db[self.collection_name]
        result = await collection.delete_one(
            {"_id": ObjectId(notification_id), "user_id": user_id}
        )
        return result.deleted_count > 0


# Singleton instance
in_app_channel = InAppChannel()
