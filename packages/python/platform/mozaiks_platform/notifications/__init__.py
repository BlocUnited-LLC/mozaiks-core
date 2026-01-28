# backend/core/notifications/__init__.py
"""
Notifications Module

Provides multi-channel notification delivery with templates, preferences, and scheduling.

Components:
- channels/: Delivery channel implementations (in_app, email, sms, web_push)
- templates.py: Template rendering with variable substitution
- scheduler.py: Digest and scheduled notification service
- broadcast.py: Admin broadcast service

Usage:
    from mozaiks_platform.notifications import in_app_channel, email_channel
    
    # Send via specific channel
    await in_app_channel.send(
        user_id="user123",
        notification_type="welcome",
        title="Welcome!",
        message="Thanks for joining"
    )
    
    # Broadcast to multiple users (admin only)
    from mozaiks_platform.notifications import broadcast_service
    await broadcast_service.broadcast(
        notification_type="announcement",
        title="System Update",
        message="We've added new features!",
        target={"type": "all"},
        channels=["in_app", "email"]
    )
    
    # Schedule a notification
    from mozaiks_platform.notifications import digest_scheduler
    await digest_scheduler.schedule_notification(
        user_id="user123",
        notification_type="reminder",
        title="Task Due",
        message="Your task is due tomorrow",
        scheduled_for=datetime.utcnow() + timedelta(hours=24)
    )
"""

from .templates import template_renderer
from .channels import (
    CHANNELS,
    get_channel,
    get_enabled_channels,
    get_all_channels_config,
    in_app_channel,
    email_channel,
    sms_channel,
    web_push_channel
)
from .scheduler import digest_scheduler
from .broadcast import broadcast_service

__all__ = [
    # Templates
    "template_renderer",
    # Channel registry
    "CHANNELS",
    "get_channel",
    "get_enabled_channels",
    "get_all_channels_config",
    # Channel instances
    "in_app_channel",
    "email_channel",
    "sms_channel",
    "web_push_channel",
    # Services
    "digest_scheduler",
    "broadcast_service"
]
