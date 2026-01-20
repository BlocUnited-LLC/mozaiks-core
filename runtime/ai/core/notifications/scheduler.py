# backend/core/notifications/scheduler.py
"""
Notification Scheduler Service

Handles scheduled notifications including daily/weekly digests and delayed delivery.

This service runs as a background task and processes:
- Daily digest emails (aggregated notifications)
- Weekly digest emails
- Scheduled notifications (send at specific time)
- Retry failed notifications

Configuration:
    Digest preferences are stored per-user in user_preferences.notification_digest
    
Environment Variables:
    NOTIFICATION_DIGEST_ENABLED: Enable digest processing (default: true)
    NOTIFICATION_DIGEST_DAILY_HOUR: Hour to send daily digest (0-23, default: 9)
    NOTIFICATION_DIGEST_WEEKLY_DAY: Day of week for weekly digest (0=Mon, default: 0)
"""

import os
import json
import logging
import asyncio
from datetime import datetime, timedelta
from typing import Dict, List, Any, Optional

from core.config.database import db
from .templates import template_renderer
from .channels import email_channel

logger = logging.getLogger("mozaiks_core.notifications.scheduler")


class DigestScheduler:
    """
    Manages scheduled notification delivery including digests.
    
    Features:
    - Daily notification digest
    - Weekly notification summary
    - Configurable delivery times
    - User preference respect
    """
    
    def __init__(self):
        self.enabled = os.getenv("NOTIFICATION_DIGEST_ENABLED", "true").lower() == "true"
        self.daily_hour = int(os.getenv("NOTIFICATION_DIGEST_DAILY_HOUR", "9"))
        self.weekly_day = int(os.getenv("NOTIFICATION_DIGEST_WEEKLY_DAY", "0"))  # Monday
        
        self._running = False
        self._task = None
        
        logger.info(f"DigestScheduler initialized: enabled={self.enabled}, daily_hour={self.daily_hour}")
    
    async def start(self):
        """Start the scheduler background task."""
        if not self.enabled:
            logger.info("Digest scheduler disabled")
            return
        
        if self._running:
            logger.warning("Scheduler already running")
            return
        
        self._running = True
        self._task = asyncio.create_task(self._run_loop())
        logger.info("Digest scheduler started")
    
    async def stop(self):
        """Stop the scheduler."""
        self._running = False
        if self._task:
            self._task.cancel()
            try:
                await self._task
            except asyncio.CancelledError:
                pass
        logger.info("Digest scheduler stopped")
    
    async def _run_loop(self):
        """Main scheduler loop - checks every minute for scheduled tasks."""
        while self._running:
            try:
                now = datetime.utcnow()
                
                # Check for daily digest (at configured hour)
                if now.hour == self.daily_hour and now.minute == 0:
                    await self._process_daily_digests()
                
                # Check for weekly digest (at configured day and hour)
                if now.weekday() == self.weekly_day and now.hour == self.daily_hour and now.minute == 0:
                    await self._process_weekly_digests()
                
                # Process any scheduled notifications
                await self._process_scheduled_notifications()
                
                # Wait until next minute
                await asyncio.sleep(60)
                
            except asyncio.CancelledError:
                break
            except Exception as e:
                logger.error(f"Scheduler error: {e}")
                await asyncio.sleep(60)
    
    async def _process_daily_digests(self):
        """Process daily digest emails for all subscribed users."""
        logger.info("Processing daily digests")
        
        try:
            # Find users who want daily digests
            users_collection = db["users"]
            cursor = users_collection.find({
                "notification_preferences.digest_frequency": "daily"
            })
            
            users = await cursor.to_list(length=1000)
            
            for user in users:
                try:
                    await self._send_digest_for_user(
                        user_id=str(user["_id"]),
                        digest_type="daily",
                        user_data=user
                    )
                except Exception as e:
                    logger.error(f"Error sending daily digest to user {user['_id']}: {e}")
            
            logger.info(f"Processed daily digests for {len(users)} users")
            
        except Exception as e:
            logger.error(f"Error processing daily digests: {e}")
    
    async def _process_weekly_digests(self):
        """Process weekly digest emails for all subscribed users."""
        logger.info("Processing weekly digests")
        
        try:
            users_collection = db["users"]
            cursor = users_collection.find({
                "notification_preferences.digest_frequency": "weekly"
            })
            
            users = await cursor.to_list(length=1000)
            
            for user in users:
                try:
                    await self._send_digest_for_user(
                        user_id=str(user["_id"]),
                        digest_type="weekly",
                        user_data=user
                    )
                except Exception as e:
                    logger.error(f"Error sending weekly digest to user {user['_id']}: {e}")
            
            logger.info(f"Processed weekly digests for {len(users)} users")
            
        except Exception as e:
            logger.error(f"Error processing weekly digests: {e}")
    
    async def _send_digest_for_user(
        self,
        user_id: str,
        digest_type: str,
        user_data: Dict[str, Any]
    ):
        """Send a digest email to a specific user."""
        # Calculate date range
        now = datetime.utcnow()
        if digest_type == "daily":
            start_date = now - timedelta(days=1)
        else:  # weekly
            start_date = now - timedelta(weeks=1)
        
        # Get unread notifications for the period
        notifications_collection = db["notifications"]
        cursor = notifications_collection.find({
            "user_id": user_id,
            "created_at": {"$gte": start_date},
            "read": False,
            "included_in_digest": {"$ne": True}
        }).sort("created_at", -1).limit(50)
        
        notifications = await cursor.to_list(length=50)
        
        if not notifications:
            logger.debug(f"No notifications for {digest_type} digest for user {user_id}")
            return
        
        # Render digest content
        rendered = await template_renderer.render_digest(
            digest_type=digest_type,
            notifications=notifications,
            user_data={
                "name": user_data.get("name", user_data.get("username", "User")),
                "email": user_data.get("email")
            }
        )
        
        # Send email
        success = await email_channel.send(
            user_id=user_id,
            notification_type=f"{digest_type}_digest",
            title=rendered.get("subject", f"Your {digest_type} notification digest"),
            message=rendered.get("body_text", ""),
            metadata={"email": user_data.get("email")},
            template_data={"body_html": rendered.get("body_html")}
        )
        
        if success:
            # Mark notifications as included in digest
            notification_ids = [n["_id"] for n in notifications]
            await notifications_collection.update_many(
                {"_id": {"$in": notification_ids}},
                {"$set": {"included_in_digest": True, "digest_sent_at": now}}
            )
            logger.info(f"Sent {digest_type} digest with {len(notifications)} notifications to user {user_id}")
    
    async def _process_scheduled_notifications(self):
        """Process notifications scheduled for delivery."""
        try:
            scheduled_collection = db["scheduled_notifications"]
            now = datetime.utcnow()
            
            # Find due notifications
            cursor = scheduled_collection.find({
                "scheduled_for": {"$lte": now},
                "status": "pending"
            })
            
            scheduled = await cursor.to_list(length=100)
            
            for item in scheduled:
                try:
                    # Import here to avoid circular imports
                    from core.notifications_manager import notifications_manager
                    
                    await notifications_manager.create_notification(
                        user_id=item["user_id"],
                        notification_type=item["type"],
                        title=item["title"],
                        message=item["message"],
                        metadata=item.get("metadata", {})
                    )
                    
                    # Mark as sent
                    await scheduled_collection.update_one(
                        {"_id": item["_id"]},
                        {"$set": {"status": "sent", "sent_at": now}}
                    )
                    
                except Exception as e:
                    logger.error(f"Error processing scheduled notification {item['_id']}: {e}")
                    # Mark as failed
                    await scheduled_collection.update_one(
                        {"_id": item["_id"]},
                        {"$set": {"status": "failed", "error": str(e)}}
                    )
            
            if scheduled:
                logger.info(f"Processed {len(scheduled)} scheduled notifications")
                
        except Exception as e:
            logger.error(f"Error processing scheduled notifications: {e}")
    
    # --- API Methods ---
    
    async def schedule_notification(
        self,
        user_id: str,
        notification_type: str,
        title: str,
        message: str,
        scheduled_for: datetime,
        metadata: Optional[Dict[str, Any]] = None
    ) -> str:
        """
        Schedule a notification for future delivery.
        
        Args:
            user_id: Target user ID
            notification_type: Type of notification
            title: Notification title
            message: Notification message
            scheduled_for: When to send
            metadata: Additional data
            
        Returns:
            str: Scheduled notification ID
        """
        collection = db["scheduled_notifications"]
        
        doc = {
            "user_id": user_id,
            "type": notification_type,
            "title": title,
            "message": message,
            "scheduled_for": scheduled_for,
            "metadata": metadata or {},
            "status": "pending",
            "created_at": datetime.utcnow()
        }
        
        result = await collection.insert_one(doc)
        return str(result.inserted_id)
    
    async def cancel_scheduled(self, notification_id: str) -> bool:
        """Cancel a scheduled notification."""
        from bson import ObjectId
        collection = db["scheduled_notifications"]
        result = await collection.update_one(
            {"_id": ObjectId(notification_id), "status": "pending"},
            {"$set": {"status": "cancelled"}}
        )
        return result.modified_count > 0
    
    async def get_user_digest_preference(self, user_id: str) -> Optional[str]:
        """Get a user's digest preference (none, daily, weekly)."""
        users_collection = db["users"]
        user = await users_collection.find_one(
            {"_id": user_id},
            {"notification_preferences.digest_frequency": 1}
        )
        if user:
            return user.get("notification_preferences", {}).get("digest_frequency", "none")
        return "none"
    
    async def set_user_digest_preference(
        self,
        user_id: str,
        frequency: str  # "none", "daily", "weekly"
    ) -> bool:
        """Set a user's digest preference."""
        if frequency not in ["none", "daily", "weekly"]:
            return False
        
        from bson import ObjectId
        users_collection = db["users"]
        result = await users_collection.update_one(
            {"_id": ObjectId(user_id)},
            {"$set": {"notification_preferences.digest_frequency": frequency}}
        )
        return result.modified_count > 0


# Singleton instance
digest_scheduler = DigestScheduler()
