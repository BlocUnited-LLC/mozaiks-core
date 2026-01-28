# backend/core/notifications/broadcast.py
"""
Admin Broadcast Notification Service

Allows administrators to send notifications to multiple users at once.
Supports targeting by subscription tier, plugin access, or custom queries.

Access Control:
    Requires X-Mozaiks-App-Admin-Key header or platform admin JWT
    
Environment Variables:
    MOZAIKS_APP_ADMIN_KEY: Admin API key for broadcast operations
"""

import os
import logging
from datetime import datetime
from typing import Dict, List, Any, Optional

from core.config.database import db
from .channels import get_enabled_channels, CHANNELS

logger = logging.getLogger("mozaiks_core.notifications.broadcast")


class BroadcastService:
    """
    Handles admin broadcast notifications to multiple users.
    
    Features:
    - Target by subscription tier
    - Target by plugin access
    - Target all users
    - Custom MongoDB query targeting
    - Progress tracking for large broadcasts
    """
    
    def __init__(self):
        self.admin_key = os.getenv("MOZAIKS_APP_ADMIN_KEY", "")
    
    def verify_admin_access(self, provided_key: str) -> bool:
        """Verify admin key for broadcast access."""
        if not self.admin_key:
            logger.warning("MOZAIKS_APP_ADMIN_KEY not configured")
            return False
        return provided_key == self.admin_key
    
    async def broadcast(
        self,
        notification_type: str,
        title: str,
        message: str,
        target: Dict[str, Any],
        channels: Optional[List[str]] = None,
        metadata: Optional[Dict[str, Any]] = None,
        sender_id: Optional[str] = None
    ) -> Dict[str, Any]:
        """
        Broadcast notification to multiple users.
        
        Args:
            notification_type: Type of notification
            title: Notification title
            message: Notification message
            target: Targeting criteria (see below)
            channels: Channels to use (defaults to ["in_app"])
            metadata: Additional notification data
            sender_id: Admin user ID (for audit)
            
        Target options:
            {"type": "all"} - All users
            {"type": "subscription", "tier": "premium"} - Users with subscription
            {"type": "plugin", "plugin_name": "notes_manager"} - Users with plugin access
            {"type": "query", "filter": {...}} - Custom MongoDB filter
            {"type": "user_ids", "ids": ["id1", "id2"]} - Specific users
            
        Returns:
            Dict with broadcast results
        """
        start_time = datetime.utcnow()
        
        # Build user query from target
        user_query = self._build_user_query(target)
        
        # Get target users
        users_collection = db["users"]
        cursor = users_collection.find(user_query, {"_id": 1, "email": 1})
        users = await cursor.to_list(length=10000)  # Cap at 10k per broadcast
        
        if not users:
            return {
                "success": True,
                "sent_count": 0,
                "failed_count": 0,
                "message": "No users matched target criteria"
            }
        
        # Use enabled channels or default to in_app
        delivery_channels = channels or ["in_app"]
        enabled = get_enabled_channels()
        delivery_channels = [c for c in delivery_channels if c in enabled]
        
        if not delivery_channels:
            return {
                "success": False,
                "error": "No enabled channels for broadcast"
            }
        
        # Create broadcast record
        broadcast_id = await self._create_broadcast_record(
            notification_type=notification_type,
            title=title,
            message=message,
            target=target,
            channels=delivery_channels,
            user_count=len(users),
            sender_id=sender_id
        )
        
        # Send to all users
        sent_count = 0
        failed_count = 0
        
        for user in users:
            user_id = str(user["_id"])
            success = await self._send_to_user(
                user_id=user_id,
                notification_type=notification_type,
                title=title,
                message=message,
                channels=delivery_channels,
                metadata={
                    **(metadata or {}),
                    "broadcast_id": broadcast_id,
                    "email": user.get("email")
                }
            )
            
            if success:
                sent_count += 1
            else:
                failed_count += 1
        
        # Update broadcast record
        await self._update_broadcast_record(
            broadcast_id=broadcast_id,
            sent_count=sent_count,
            failed_count=failed_count
        )
        
        duration = (datetime.utcnow() - start_time).total_seconds()
        
        logger.info(f"Broadcast {broadcast_id} complete: {sent_count} sent, {failed_count} failed, {duration:.2f}s")
        
        return {
            "success": True,
            "broadcast_id": broadcast_id,
            "sent_count": sent_count,
            "failed_count": failed_count,
            "duration_seconds": duration
        }
    
    def _build_user_query(self, target: Dict[str, Any]) -> Dict[str, Any]:
        """Build MongoDB query from target specification."""
        target_type = target.get("type", "all")
        
        if target_type == "all":
            return {}
        
        elif target_type == "subscription":
            tier = target.get("tier")
            if tier:
                return {"subscription.tier": tier}
            return {}
        
        elif target_type == "plugin":
            plugin_name = target.get("plugin_name")
            if plugin_name:
                # Users who have access to this plugin
                return {
                    "$or": [
                        {"subscription.plugins_unlocked": plugin_name},
                        {"admin": True}
                    ]
                }
            return {}
        
        elif target_type == "query":
            return target.get("filter", {})
        
        elif target_type == "user_ids":
            from bson import ObjectId
            ids = target.get("ids", [])
            return {"_id": {"$in": [ObjectId(id) for id in ids]}}
        
        return {}
    
    async def _send_to_user(
        self,
        user_id: str,
        notification_type: str,
        title: str,
        message: str,
        channels: List[str],
        metadata: Dict[str, Any]
    ) -> bool:
        """Send notification to a single user via specified channels."""
        success = False
        
        for channel_id in channels:
            channel = CHANNELS.get(channel_id)
            if channel and channel.is_enabled():
                try:
                    result = await channel.send(
                        user_id=user_id,
                        notification_type=notification_type,
                        title=title,
                        message=message,
                        metadata=metadata
                    )
                    if result:
                        success = True
                except Exception as e:
                    logger.error(f"Channel {channel_id} failed for user {user_id}: {e}")
        
        return success
    
    async def _create_broadcast_record(
        self,
        notification_type: str,
        title: str,
        message: str,
        target: Dict[str, Any],
        channels: List[str],
        user_count: int,
        sender_id: Optional[str]
    ) -> str:
        """Create a record of the broadcast for audit/tracking."""
        collection = db["notification_broadcasts"]
        
        doc = {
            "notification_type": notification_type,
            "title": title,
            "message": message,
            "target": target,
            "channels": channels,
            "user_count": user_count,
            "sent_count": 0,
            "failed_count": 0,
            "sender_id": sender_id,
            "status": "in_progress",
            "created_at": datetime.utcnow()
        }
        
        result = await collection.insert_one(doc)
        return str(result.inserted_id)
    
    async def _update_broadcast_record(
        self,
        broadcast_id: str,
        sent_count: int,
        failed_count: int
    ):
        """Update broadcast record with results."""
        from bson import ObjectId
        collection = db["notification_broadcasts"]
        
        await collection.update_one(
            {"_id": ObjectId(broadcast_id)},
            {
                "$set": {
                    "sent_count": sent_count,
                    "failed_count": failed_count,
                    "status": "completed",
                    "completed_at": datetime.utcnow()
                }
            }
        )
    
    async def get_broadcast_history(
        self,
        limit: int = 50,
        skip: int = 0
    ) -> List[Dict[str, Any]]:
        """Get broadcast history for admin dashboard."""
        collection = db["notification_broadcasts"]
        
        cursor = collection.find().sort("created_at", -1).skip(skip).limit(limit)
        broadcasts = await cursor.to_list(length=limit)
        
        for b in broadcasts:
            b["_id"] = str(b["_id"])
            if "created_at" in b:
                b["created_at"] = b["created_at"].isoformat()
            if "completed_at" in b:
                b["completed_at"] = b["completed_at"].isoformat()
        
        return broadcasts
    
    async def get_broadcast_details(self, broadcast_id: str) -> Optional[Dict[str, Any]]:
        """Get details of a specific broadcast."""
        from bson import ObjectId
        collection = db["notification_broadcasts"]
        
        broadcast = await collection.find_one({"_id": ObjectId(broadcast_id)})
        if broadcast:
            broadcast["_id"] = str(broadcast["_id"])
            if "created_at" in broadcast:
                broadcast["created_at"] = broadcast["created_at"].isoformat()
            if "completed_at" in broadcast:
                broadcast["completed_at"] = broadcast["completed_at"].isoformat()
        
        return broadcast


# Singleton instance
broadcast_service = BroadcastService()
