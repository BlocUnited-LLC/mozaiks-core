# backend/core/routes/notifications_admin.py
"""
Admin Notifications API Routes

Provides admin endpoints for:
- Broadcast notifications to multiple users
- View broadcast history
- Manage notification templates
- View notification statistics

All routes are protected by X-Internal-API-Key or superadmin JWT.
Uses the shared require_admin_or_internal dependency for consistent auth.
"""

import os
import logging
from typing import Optional, List
from datetime import datetime

from fastapi import APIRouter, Depends, HTTPException, Header, Query
from pydantic import BaseModel, Field

from core.notifications.broadcast import broadcast_service
from core.notifications.scheduler import digest_scheduler
from core.notifications.templates import template_renderer
from core.notifications.channels import get_all_channels_config
from security.authentication import require_admin_or_internal

logger = logging.getLogger("mozaiks_core.routes.notifications_admin")

router = APIRouter(prefix="/__mozaiks/admin/notifications", tags=["admin-notifications"])


# === Request Models ===

class BroadcastTarget(BaseModel):
    """Target specification for broadcast."""
    type: str = Field(..., description="all, subscription, plugin, query, user_ids")
    tier: Optional[str] = None
    plugin_name: Optional[str] = None
    filter: Optional[dict] = None
    ids: Optional[List[str]] = None


class BroadcastRequest(BaseModel):
    """Broadcast notification request."""
    notification_type: str = Field(default="admin_broadcast")
    title: str
    message: str
    target: BroadcastTarget
    channels: Optional[List[str]] = Field(default=["in_app"])
    metadata: Optional[dict] = None


class ScheduledNotificationRequest(BaseModel):
    """Request to schedule a notification."""
    user_id: str
    notification_type: str
    title: str
    message: str
    scheduled_for: datetime
    metadata: Optional[dict] = None


# === Routes ===

@router.post("/broadcast")
async def send_broadcast(
    request: BroadcastRequest,
    current_user: dict = Depends(require_admin_or_internal)
):
    """
    Send a broadcast notification to multiple users.
    
    Target types:
    - all: All users
    - subscription: Users with specific subscription tier
    - plugin: Users with access to specific plugin
    - query: Custom MongoDB filter
    - user_ids: List of specific user IDs
    """
    result = await broadcast_service.broadcast(
        notification_type=request.notification_type,
        title=request.title,
        message=request.message,
        target=request.target.model_dump(),
        channels=request.channels,
        metadata=request.metadata
    )
    
    return result


@router.get("/broadcasts")
async def get_broadcast_history(
    limit: int = Query(50, ge=1, le=200),
    skip: int = Query(0, ge=0),
    current_user: dict = Depends(require_admin_or_internal)
):
    """Get broadcast history."""
    broadcasts = await broadcast_service.get_broadcast_history(limit=limit, skip=skip)
    return {"broadcasts": broadcasts, "count": len(broadcasts)}


@router.get("/broadcasts/{broadcast_id}")
async def get_broadcast_details(
    broadcast_id: str,
    current_user: dict = Depends(require_admin_or_internal)
):
    """Get details of a specific broadcast."""
    broadcast = await broadcast_service.get_broadcast_details(broadcast_id)
    if not broadcast:
        raise HTTPException(status_code=404, detail="Broadcast not found")
    
    return broadcast


@router.post("/schedule")
async def schedule_notification(
    request: ScheduledNotificationRequest,
    current_user: dict = Depends(require_admin_or_internal)
):
    """Schedule a notification for future delivery."""
    notification_id = await digest_scheduler.schedule_notification(
        user_id=request.user_id,
        notification_type=request.notification_type,
        title=request.title,
        message=request.message,
        scheduled_for=request.scheduled_for,
        metadata=request.metadata
    )
    
    return {"success": True, "notification_id": notification_id}


@router.delete("/schedule/{notification_id}")
async def cancel_scheduled(
    notification_id: str,
    current_user: dict = Depends(require_admin_or_internal)
):
    """Cancel a scheduled notification."""
    success = await digest_scheduler.cancel_scheduled(notification_id)
    if not success:
        raise HTTPException(status_code=404, detail="Scheduled notification not found or already sent")
    
    return {"success": True}


@router.get("/channels")
async def get_channels(
    current_user: dict = Depends(require_admin_or_internal)
):
    """Get all notification channels and their status."""
    return {"channels": get_all_channels_config()}


@router.get("/templates")
async def get_templates(
    current_user: dict = Depends(require_admin_or_internal)
):
    """Get all notification templates."""
    return {
        "templates": template_renderer.list_templates(),
        "digest_templates": list(template_renderer.digest_templates.keys())
    }


@router.post("/templates/reload")
async def reload_templates(
    current_user: dict = Depends(require_admin_or_internal)
):
    """Reload notification templates from file."""
    template_renderer.reload_templates()
    return {"success": True, "templates_count": len(template_renderer.list_templates())}


@router.get("/schema")
async def get_admin_schema(
    current_user: dict = Depends(require_admin_or_internal)
):
    """
    Get schema for admin notification operations.
    Useful for LLM agents to understand available operations.
    """
    return {
        "broadcast": {
            "endpoint": "POST /__mozaiks/admin/notifications/broadcast",
            "description": "Send notification to multiple users",
            "target_types": {
                "all": "All users",
                "subscription": "Users with subscription tier (requires 'tier' field)",
                "plugin": "Users with plugin access (requires 'plugin_name' field)",
                "query": "Custom MongoDB filter (requires 'filter' field)",
                "user_ids": "Specific users (requires 'ids' field)"
            },
            "channels": ["in_app", "email", "sms", "web_push"]
        },
        "schedule": {
            "endpoint": "POST /__mozaiks/admin/notifications/schedule",
            "description": "Schedule notification for future delivery",
            "fields": ["user_id", "notification_type", "title", "message", "scheduled_for"]
        },
        "templates": {
            "endpoint": "GET /__mozaiks/admin/notifications/templates",
            "reload_endpoint": "POST /__mozaiks/admin/notifications/templates/reload"
        }
    }
