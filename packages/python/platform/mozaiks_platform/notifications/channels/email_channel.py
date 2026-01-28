# backend/core/notifications/channels/email_channel.py
"""
Email Notification Channel

Sends notifications via email using external email service.

Required Environment Variables:
    EMAIL_SERVICE_URL: URL of email service endpoint (required to enable)
    EMAIL_FROM_ADDRESS: Default sender email address (optional, defaults to noreply@mozaiks.io)
    EMAIL_FROM_NAME: Default sender name (optional, defaults to "Mozaiks")
"""

import os
import logging
import httpx
from typing import Dict, Any, Optional

from .base import NotificationChannel
from ..templates import template_renderer

logger = logging.getLogger("mozaiks_core.notifications.channels.email")


class EmailChannel(NotificationChannel):
    """
    Email notification delivery channel.
    
    Features:
    - Template-based email rendering
    - HTML and plain text support
    - External email service integration
    - Configurable sender details
    """
    
    channel_id = "email"
    channel_name = "Email Notifications"
    
    def __init__(self):
        self.service_url = os.getenv("EMAIL_SERVICE_URL", "")
        self.from_address = os.getenv("EMAIL_FROM_ADDRESS", "noreply@mozaiks.io")
        self.from_name = os.getenv("EMAIL_FROM_NAME", "Mozaiks")
    
    def is_enabled(self) -> bool:
        """
        Email is enabled if EMAIL_SERVICE_URL is configured.
        """
        return bool(self.service_url)
    
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
        Send email notification.
        
        Args:
            user_id: Target user ID (used to look up email address)
            notification_type: Type for template selection
            title: Email subject line
            message: Email body (plain text)
            metadata: Additional data including 'email' key for direct address
            template_data: Variables for email template
            
        Returns:
            bool: True if sent successfully
        """
        if not self.is_enabled():
            logger.warning("Email channel not enabled - missing EMAIL_SERVICE_URL")
            return False
        
        try:
            # Get recipient email
            email_address = await self._get_user_email(user_id, metadata)
            if not email_address:
                logger.warning(f"No email address for user {user_id}")
                return False
            
            # Render email content using templates (optional; falls back to title/message)
            rendered = await template_renderer.render(
                notification_type=notification_type,
                channel="email",
                title=title,
                message=message,
                data=template_data or {}
            )

            # Allow caller to pass HTML directly (e.g., digest renderer)
            if template_data and template_data.get("body_html"):
                rendered["body_html"] = template_data["body_html"]
            
            # Send via email service
            payload = {
                "to": email_address,
                "from_email": self.from_address,
                "from_name": self.from_name,
                "subject": rendered["subject"],
                "body_text": rendered["body_text"],
                "body_html": rendered.get("body_html")
            }
            
            async with httpx.AsyncClient() as client:
                response = await client.post(
                    self.service_url,
                    json=payload,
                    timeout=30.0
                )
                response.raise_for_status()
            
            logger.info(f"Email sent to {email_address} for user {user_id}")
            return True
            
        except httpx.HTTPError as e:
            logger.error(f"Email service error: {e}")
            return False
        except Exception as e:
            logger.error(f"Failed to send email: {e}")
            return False
    
    async def _get_user_email(
        self,
        user_id: str,
        metadata: Optional[Dict[str, Any]]
    ) -> Optional[str]:
        """
        Get email address for user.
        
        Priority:
        1. metadata['email'] - direct override
        2. Database lookup from users collection
        """
        # Check for direct email in metadata
        if metadata and metadata.get("email"):
            return metadata["email"]
        
        # Look up from database
        try:
            from mozaiks_infra.config.database import db
            user = await db["users"].find_one({"_id": user_id})
            if user:
                return user.get("email")
            
            # Try with string ID
            user = await db["users"].find_one({"user_id": user_id})
            if user:
                return user.get("email")
        except Exception as e:
            logger.error(f"Error looking up user email: {e}")
        
        return None


# Singleton instance
email_channel = EmailChannel()
