# backend/core/notifications/channels/base.py
"""
Base class for notification channels.

All notification channels must inherit from this class and implement the required methods.
This ensures consistent behavior across all delivery channels.
"""

import logging
from abc import ABC, abstractmethod
from typing import Dict, List, Any, Optional

logger = logging.getLogger("mozaiks_core.notifications.channels")


class NotificationChannel(ABC):
    """
    Abstract base class for notification delivery channels.
    
    Implement this interface to add new notification channels (e.g., Slack, Discord, etc.)
    
    Required environment variables should be documented in the channel implementation.
    """
    
    channel_id: str = "base"
    channel_name: str = "Base Channel"
    
    @abstractmethod
    def is_enabled(self) -> bool:
        """
        Check if this channel is enabled via environment variables.
        
        Returns:
            bool: True if channel is configured and enabled
        """
        pass
    
    @abstractmethod
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
        Send a notification to a single user.
        
        Args:
            user_id: The user's ID
            notification_type: Type of notification (e.g., "security_alerts")
            title: Notification title
            message: Notification message/body
            metadata: Additional data for the notification
            template_data: Variables for template rendering
            
        Returns:
            bool: True if sent successfully
        """
        pass
    
    async def send_bulk(
        self,
        user_ids: List[str],
        notification_type: str,
        title: str,
        message: str,
        metadata: Optional[Dict[str, Any]] = None,
        template_data: Optional[Dict[str, Any]] = None
    ) -> Dict[str, bool]:
        """
        Send a notification to multiple users.
        
        Default implementation sends individually. Override for batch optimization.
        
        Args:
            user_ids: List of user IDs
            notification_type: Type of notification
            title: Notification title
            message: Notification message/body
            metadata: Additional data
            template_data: Variables for template rendering
            
        Returns:
            Dict mapping user_id to success status
        """
        results = {}
        for user_id in user_ids:
            try:
                results[user_id] = await self.send(
                    user_id=user_id,
                    notification_type=notification_type,
                    title=title,
                    message=message,
                    metadata=metadata,
                    template_data=template_data
                )
            except Exception as e:
                logger.error(f"Failed to send to user {user_id}: {e}")
                results[user_id] = False
        return results
    
    def get_config(self) -> Dict[str, Any]:
        """
        Get channel configuration for API responses.
        
        Returns:
            Dict with channel info
        """
        return {
            "id": self.channel_id,
            "name": self.channel_name,
            "enabled": self.is_enabled()
        }
