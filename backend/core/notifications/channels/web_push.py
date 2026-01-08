# backend/core/notifications/channels/web_push.py
"""
Web Push Notification Channel

Sends browser push notifications using the Web Push protocol.

Required Environment Variables:
    PUSH_VAPID_PUBLIC_KEY: VAPID public key for push authentication
    PUSH_VAPID_PRIVATE_KEY: VAPID private key for push authentication
    PUSH_VAPID_CLAIMS_EMAIL: Contact email for VAPID claims (e.g., mailto:admin@example.com)

To generate VAPID keys, run:
    python -c "from py_vapid import Vapid; v = Vapid(); v.generate_keys(); print('Public:', v.public_key_str); print('Private:', v.private_key_str)"
"""

import os
import logging
import json
from typing import Dict, Any, Optional, List

from .base import NotificationChannel

logger = logging.getLogger("mozaiks_core.notifications.channels.web_push")


class WebPushChannel(NotificationChannel):
    """
    Web Push notification delivery channel.
    
    Features:
    - Browser push notifications via VAPID
    - Subscription management
    - Action buttons support
    - Icon and badge customization
    """
    
    channel_id = "web_push"
    channel_name = "Browser Push Notifications"
    
    def __init__(self):
        self.vapid_public_key = os.getenv("PUSH_VAPID_PUBLIC_KEY", "")
        self.vapid_private_key = os.getenv("PUSH_VAPID_PRIVATE_KEY", "")
        self.vapid_claims_email = os.getenv("PUSH_VAPID_CLAIMS_EMAIL", "")
        
        self.default_icon = "/assets/icon-192.png"
        self.default_badge = "/assets/badge-72.png"
    
    def is_enabled(self) -> bool:
        """
        Web Push is enabled if VAPID keys are configured.
        """
        return bool(
            self.vapid_public_key and 
            self.vapid_private_key and 
            self.vapid_claims_email
        )
    
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
        Send web push notification to all user's subscribed devices.
        
        Args:
            user_id: Target user ID
            notification_type: Type for categorization
            title: Notification title
            message: Notification body
            metadata: Additional data (icon, badge, actions, url)
            template_data: Variables for templates (not typically used for push)
            
        Returns:
            bool: True if sent to at least one device
        """
        if not self.is_enabled():
            logger.warning("Web Push channel not enabled - missing VAPID keys")
            return False
        
        try:
            # Get user's push subscriptions
            subscriptions = await self._get_user_subscriptions(user_id)
            if not subscriptions:
                logger.info(f"No push subscriptions for user {user_id}")
                return False
            
            # Build push payload
            payload = self._build_payload(title, message, metadata)
            
            # Send to all subscriptions
            success_count = 0
            for subscription in subscriptions:
                try:
                    sent = await self._send_push(subscription, payload)
                    if sent:
                        success_count += 1
                except Exception as e:
                    logger.error(f"Failed to send to subscription: {e}")
                    # Mark subscription as invalid if it failed
                    await self._handle_push_failure(user_id, subscription, e)
            
            logger.info(f"Web push sent to {success_count}/{len(subscriptions)} devices for user {user_id}")
            return success_count > 0
            
        except Exception as e:
            logger.error(f"Failed to send web push: {e}")
            return False
    
    def _build_payload(
        self,
        title: str,
        message: str,
        metadata: Optional[Dict[str, Any]]
    ) -> str:
        """Build the push notification payload."""
        payload = {
            "title": title,
            "body": message,
            "icon": (metadata or {}).get("icon", self.default_icon),
            "badge": (metadata or {}).get("badge", self.default_badge),
            "tag": (metadata or {}).get("tag", "mozaiks"),
            "renotify": True,
            "requireInteraction": (metadata or {}).get("require_interaction", False),
            "data": {
                "url": (metadata or {}).get("url", "/"),
                "type": (metadata or {}).get("type", "default")
            }
        }
        
        # Add actions if provided
        if metadata and metadata.get("actions"):
            payload["actions"] = metadata["actions"]
        
        return json.dumps(payload)
    
    async def _send_push(self, subscription: Dict[str, Any], payload: str) -> bool:
        """Send push notification to a single subscription."""
        try:
            from pywebpush import webpush, WebPushException
        except ImportError:
            logger.error("pywebpush package not installed. Run: pip install pywebpush")
            return False
        
        try:
            webpush(
                subscription_info=subscription,
                data=payload,
                vapid_private_key=self.vapid_private_key,
                vapid_claims={"sub": self.vapid_claims_email}
            )
            return True
        except WebPushException as e:
            logger.error(f"WebPush error: {e}")
            raise e
    
    async def _get_user_subscriptions(self, user_id: str) -> List[Dict[str, Any]]:
        """Get all push subscriptions for a user."""
        try:
            from core.config.database import db
            collection = db["push_subscriptions"]
            subscriptions = await collection.find({"user_id": user_id}).to_list(20)
            return [s["subscription"] for s in subscriptions if "subscription" in s]
        except Exception as e:
            logger.error(f"Error fetching subscriptions: {e}")
            return []
    
    async def _handle_push_failure(
        self,
        user_id: str,
        subscription: Dict[str, Any],
        error: Exception
    ):
        """Handle push notification failure - remove invalid subscriptions."""
        try:
            # Check if this is a permanent failure (subscription expired/invalid)
            error_str = str(error)
            if "410" in error_str or "404" in error_str:
                from core.config.database import db
                collection = db["push_subscriptions"]
                await collection.delete_one({
                    "user_id": user_id,
                    "subscription.endpoint": subscription.get("endpoint")
                })
                logger.info(f"Removed invalid subscription for user {user_id}")
        except Exception as e:
            logger.error(f"Error handling push failure: {e}")
    
    # --- Subscription Management ---
    
    async def save_subscription(
        self,
        user_id: str,
        subscription: Dict[str, Any],
        user_agent: Optional[str] = None
    ) -> bool:
        """
        Save a push subscription for a user.
        
        Args:
            user_id: User ID
            subscription: Push subscription object from browser
            user_agent: Browser user agent for device identification
            
        Returns:
            bool: True if saved successfully
        """
        try:
            from core.config.database import db
            from datetime import datetime
            
            collection = db["push_subscriptions"]
            
            # Upsert based on endpoint (one subscription per endpoint)
            await collection.update_one(
                {
                    "user_id": user_id,
                    "subscription.endpoint": subscription.get("endpoint")
                },
                {
                    "$set": {
                        "user_id": user_id,
                        "subscription": subscription,
                        "user_agent": user_agent,
                        "updated_at": datetime.utcnow()
                    },
                    "$setOnInsert": {
                        "created_at": datetime.utcnow()
                    }
                },
                upsert=True
            )
            return True
        except Exception as e:
            logger.error(f"Error saving subscription: {e}")
            return False
    
    async def remove_subscription(self, user_id: str, endpoint: str) -> bool:
        """Remove a push subscription."""
        try:
            from core.config.database import db
            collection = db["push_subscriptions"]
            result = await collection.delete_one({
                "user_id": user_id,
                "subscription.endpoint": endpoint
            })
            return result.deleted_count > 0
        except Exception as e:
            logger.error(f"Error removing subscription: {e}")
            return False
    
    def get_public_key(self) -> Optional[str]:
        """Get VAPID public key for client subscription."""
        return self.vapid_public_key if self.is_enabled() else None


# Singleton instance
web_push_channel = WebPushChannel()
