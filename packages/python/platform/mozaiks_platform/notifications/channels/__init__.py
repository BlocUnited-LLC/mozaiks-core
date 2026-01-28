# backend/core/notifications/channels/__init__.py
"""
Notification delivery channels.

Each channel is a self-contained module that can be enabled/disabled via environment variables.
Channels are loaded dynamically based on configuration.

To add a new channel:
1. Create a new file in this directory (e.g., my_channel.py)
2. Implement the NotificationChannel interface
3. Add environment variables to .env.example
4. Add channel config to notifications_config.json

LLM Instructions:
- Each channel must implement: is_enabled(), send(), send_bulk()
- Use async/await for all I/O operations
- Handle failures gracefully and log errors
- Respect user preferences before sending
"""

from .base import NotificationChannel
from .in_app import InAppChannel, in_app_channel
from .email_channel import EmailChannel, email_channel
from .sms import SMSChannel, sms_channel
from .web_push import WebPushChannel, web_push_channel

# Channel registry for dynamic lookup
CHANNELS = {
    "in_app": in_app_channel,
    "email": email_channel,
    "sms": sms_channel,
    "web_push": web_push_channel
}

def get_channel(channel_id: str):
    """Get a channel instance by ID."""
    return CHANNELS.get(channel_id)

def get_enabled_channels():
    """Get all enabled channels."""
    return {k: v for k, v in CHANNELS.items() if v.is_enabled()}

def get_all_channels_config():
    """Get configuration info for all channels."""
    return [channel.get_config() for channel in CHANNELS.values()]

__all__ = [
    "NotificationChannel",
    "InAppChannel", "in_app_channel",
    "EmailChannel", "email_channel", 
    "SMSChannel", "sms_channel",
    "WebPushChannel", "web_push_channel",
    "CHANNELS", "get_channel", "get_enabled_channels", "get_all_channels_config"
]
