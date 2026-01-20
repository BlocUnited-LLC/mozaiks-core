# backend/core/routes/push_subscriptions.py
"""
Web Push Subscription Routes

Provides endpoints for managing browser push notification subscriptions.
Users can subscribe/unsubscribe their devices for push notifications.

These routes are authenticated via JWT.
"""

import logging
from typing import Optional

from fastapi import APIRouter, HTTPException, Depends, Request
from pydantic import BaseModel

from security.auth import get_current_user
from core.notifications.channels.web_push import web_push_channel

logger = logging.getLogger("mozaiks_core.routes.push_subscriptions")

router = APIRouter(prefix="/api/push", tags=["push-notifications"])


# === Request Models ===

class PushSubscription(BaseModel):
    """Browser push subscription object."""
    endpoint: str
    keys: dict  # Contains p256dh and auth keys
    expirationTime: Optional[int] = None


class SubscribeRequest(BaseModel):
    """Request to subscribe to push notifications."""
    subscription: PushSubscription


# === Routes ===

@router.get("/vapid-public-key")
async def get_vapid_public_key():
    """
    Get the VAPID public key for push subscription.
    
    The client needs this key to subscribe to push notifications.
    Returns null if push notifications are not configured.
    """
    public_key = web_push_channel.get_public_key()
    
    return {
        "publicKey": public_key,
        "enabled": web_push_channel.is_enabled()
    }


@router.post("/subscribe")
async def subscribe_to_push(
    request: SubscribeRequest,
    req: Request,
    current_user: dict = Depends(get_current_user)
):
    """
    Subscribe the current device to push notifications.
    
    The subscription object comes from the browser's PushManager.subscribe()
    """
    if not web_push_channel.is_enabled():
        raise HTTPException(
            status_code=503,
            detail="Push notifications are not configured on this server"
        )
    
    user_id = str(current_user.get("_id", current_user.get("user_id")))
    user_agent = req.headers.get("user-agent", "")
    
    success = await web_push_channel.save_subscription(
        user_id=user_id,
        subscription=request.subscription.model_dump(),
        user_agent=user_agent
    )
    
    if not success:
        raise HTTPException(status_code=500, detail="Failed to save subscription")
    
    return {"success": True, "message": "Successfully subscribed to push notifications"}


@router.post("/unsubscribe")
async def unsubscribe_from_push(
    request: SubscribeRequest,
    current_user: dict = Depends(get_current_user)
):
    """
    Unsubscribe the current device from push notifications.
    """
    user_id = str(current_user.get("_id", current_user.get("user_id")))
    
    success = await web_push_channel.remove_subscription(
        user_id=user_id,
        endpoint=request.subscription.endpoint
    )
    
    return {"success": success, "message": "Unsubscribed from push notifications" if success else "Subscription not found"}


@router.get("/status")
async def get_push_status(current_user: dict = Depends(get_current_user)):
    """
    Get the push notification status for the current user.
    
    Returns whether push is enabled and how many devices are subscribed.
    """
    from core.config.database import db
    
    user_id = str(current_user.get("_id", current_user.get("user_id")))
    
    # Count subscriptions for this user
    collection = db["push_subscriptions"]
    count = await collection.count_documents({"user_id": user_id})
    
    return {
        "enabled": web_push_channel.is_enabled(),
        "subscribed_devices": count,
        "vapid_public_key": web_push_channel.get_public_key()
    }


@router.delete("/devices")
async def remove_all_subscriptions(current_user: dict = Depends(get_current_user)):
    """
    Remove all push subscriptions for the current user (all devices).
    """
    from core.config.database import db
    
    user_id = str(current_user.get("_id", current_user.get("user_id")))
    
    collection = db["push_subscriptions"]
    result = await collection.delete_many({"user_id": user_id})
    
    return {
        "success": True,
        "removed_count": result.deleted_count
    }
