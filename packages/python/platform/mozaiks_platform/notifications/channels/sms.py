# backend/core/notifications/channels/sms.py
"""
SMS Notification Channel

Sends notifications via SMS using Twilio or compatible SMS service.

Required Environment Variables:
    SMS_PROVIDER: SMS provider (currently supports "twilio")
    SMS_ACCOUNT_SID: Twilio Account SID
    SMS_AUTH_TOKEN: Twilio Auth Token
    SMS_FROM_NUMBER: Phone number to send from (e.g., +15551234567)
    
Optional:
    SMS_MESSAGING_SERVICE_SID: Twilio Messaging Service SID (alternative to FROM_NUMBER)
"""

import os
import logging
from typing import Dict, Any, Optional

from .base import NotificationChannel
from ..templates import template_renderer

logger = logging.getLogger("mozaiks_core.notifications.channels.sms")


class SMSChannel(NotificationChannel):
    """
    SMS notification delivery channel.
    
    Features:
    - Twilio integration (default)
    - Template-based message rendering
    - Phone number lookup from user profile
    - Character count optimization
    """
    
    channel_id = "sms"
    channel_name = "SMS Notifications"
    
    def __init__(self):
        self.provider = os.getenv("SMS_PROVIDER", "twilio")
        self.account_sid = os.getenv("SMS_ACCOUNT_SID", "")
        self.auth_token = os.getenv("SMS_AUTH_TOKEN", "")
        self.from_number = os.getenv("SMS_FROM_NUMBER", "")
        self.messaging_service_sid = os.getenv("SMS_MESSAGING_SERVICE_SID", "")
        
        self._client = None
    
    def is_enabled(self) -> bool:
        """
        SMS is enabled if provider credentials are configured.
        """
        if self.provider == "twilio":
            return bool(self.account_sid and self.auth_token and (self.from_number or self.messaging_service_sid))
        return False
    
    def _get_client(self):
        """Get or create Twilio client."""
        if self._client is None and self.is_enabled():
            try:
                from twilio.rest import Client
                self._client = Client(self.account_sid, self.auth_token)
            except ImportError:
                logger.error("Twilio package not installed. Run: pip install twilio")
                return None
        return self._client
    
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
        Send SMS notification.
        
        Args:
            user_id: Target user ID (used to look up phone number)
            notification_type: Type for template selection
            title: Not used for SMS (included in message)
            message: SMS body text
            metadata: Additional data including 'phone' key for direct number
            template_data: Variables for SMS template
            
        Returns:
            bool: True if sent successfully
        """
        if not self.is_enabled():
            logger.warning("SMS channel not enabled - missing credentials")
            return False
        
        try:
            # Get recipient phone number
            phone_number = await self._get_user_phone(user_id, metadata)
            if not phone_number:
                logger.warning(f"No phone number for user {user_id}")
                return False
            
            # Render SMS content using templates
            rendered = await template_renderer.render(
                notification_type=notification_type,
                channel="sms",
                title=title,
                message=message,
                data=template_data or {}
            )
            
            # SMS combines title and message
            sms_body = rendered.get("body_text", f"{title}: {message}")
            
            # Truncate if needed (SMS limit is 160 chars for single message)
            if len(sms_body) > 1600:  # Allow for concatenated SMS
                sms_body = sms_body[:1597] + "..."
            
            # Send via Twilio
            if self.provider == "twilio":
                return await self._send_twilio(phone_number, sms_body)
            
            return False
            
        except Exception as e:
            logger.error(f"Failed to send SMS: {e}")
            return False
    
    async def _send_twilio(self, to_number: str, body: str) -> bool:
        """Send SMS via Twilio."""
        client = self._get_client()
        if not client:
            return False
        
        try:
            # Run Twilio client in thread pool (it's synchronous)
            import asyncio
            loop = asyncio.get_event_loop()
            
            def send_sync():
                msg_kwargs = {"to": to_number, "body": body}
                if self.messaging_service_sid:
                    msg_kwargs["messaging_service_sid"] = self.messaging_service_sid
                else:
                    msg_kwargs["from_"] = self.from_number
                return client.messages.create(**msg_kwargs)
            
            message = await loop.run_in_executor(None, send_sync)
            logger.info(f"SMS sent: {message.sid}")
            return True
            
        except Exception as e:
            logger.error(f"Twilio error: {e}")
            return False
    
    async def _get_user_phone(
        self,
        user_id: str,
        metadata: Optional[Dict[str, Any]]
    ) -> Optional[str]:
        """
        Get phone number for user.
        
        Priority:
        1. metadata['phone'] - direct override
        2. Database lookup from users collection
        """
        # Check for direct phone in metadata
        if metadata and metadata.get("phone"):
            return metadata["phone"]
        
        # Look up from database
        try:
            from mozaiks_infra.config.database import db
            user = await db["users"].find_one({"_id": user_id})
            if user:
                return user.get("phone") or user.get("phone_number")
            
            # Try with string ID
            user = await db["users"].find_one({"user_id": user_id})
            if user:
                return user.get("phone") or user.get("phone_number")
        except Exception as e:
            logger.error(f"Error looking up user phone: {e}")
        
        return None


# Singleton instance
sms_channel = SMSChannel()
