# backend/core/routes/notifications.py
from fastapi import APIRouter, Depends, HTTPException, Request
from core.notifications_manager import notifications_manager
from core.ai_runtime.auth.dependencies import get_current_user
from core.event_bus import event_bus
import os
import logging
import time

router = APIRouter()
logger = logging.getLogger("mozaiks_core.notifications_routes")

# Get monetization status from environment variables
MONETIZATION = os.getenv("MONETIZATION", "0") == "1"

@router.get("")
async def get_notifications(unread_only: bool = False, limit: int = 20, offset: int = 0, user: dict = Depends(get_current_user)):
    """
    Get notifications for the current user
    """
    try:
        # Get notifications 
        notifications = await notifications_manager.get_user_notifications(
            user_id=user["user_id"],
            unread_only=unread_only,
            limit=limit,
            offset=offset
        )
        
        # Get unread count for all notifications
        unread_notifications = await notifications_manager.get_user_notifications(
            user_id=user["user_id"],
            unread_only=True,
            limit=100,  # Large enough to get all unread
            offset=0
        )
        
        return {
            "notifications": notifications,
            "count": len(notifications),
            "unread_count": len(unread_notifications),
            "unread_only": unread_only,
            "timestamp": int(time.time())
        }
    except Exception as e:
        logger.error(f"Error fetching notifications: {e}")
        raise HTTPException(status_code=500, detail=f"Error fetching notifications: {str(e)}")

@router.get("/config")
async def get_notifications_config(user: dict = Depends(get_current_user)):
    """
    Get notification configuration and user preferences
    """
    try:
        config = await notifications_manager.get_notification_config(
            user_id=user["user_id"],
            monetization_enabled=MONETIZATION
        )
        preferences = await notifications_manager.get_user_notification_preferences(user["user_id"])
        
        return {
            "config": config,
            "preferences": preferences
        }
    except Exception as e:
        logger.error(f"Error fetching notification config: {e}")
        raise HTTPException(status_code=500, detail=f"Error fetching notification config: {str(e)}")

@router.post("/preferences")
async def update_notification_preferences(preferences: dict, user: dict = Depends(get_current_user)):
    """
    Update notification preferences for the current user
    """
    try:
        updated_prefs = await notifications_manager.update_notification_preferences(
            user_id=user["user_id"],
            preferences=preferences
        )
        
        # Publish event for notification preferences updated
        event_bus.publish("notification_preferences_updated", {
            "user_id": user["user_id"]
        })
        
        return {
            "message": "Notification preferences updated successfully",
            "preferences": updated_prefs
        }
    except Exception as e:
        logger.error(f"Error updating notification preferences: {e}")
        raise HTTPException(status_code=500, detail=f"Error updating notification preferences: {str(e)}")

@router.post("/{notification_id}/read")
async def mark_notification_read(notification_id: str, user: dict = Depends(get_current_user)):
    """
    Mark a notification as read
    """
    try:
        success = await notifications_manager.mark_notification_read(
            user_id=user["user_id"],
            notification_id=notification_id
        )
        
        if not success:
            raise HTTPException(status_code=404, detail="Notification not found")
        
        return {"message": "Notification marked as read"}
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error marking notification as read: {e}")
        raise HTTPException(status_code=500, detail=f"Error marking notification as read: {str(e)}")

@router.post("/{notification_id}/unread")
async def mark_notification_unread(notification_id: str, user: dict = Depends(get_current_user)):
    """
    Mark a notification as unread
    If your notification manager doesn't support this, you'll need to modify it
    """
    try:
        # This assumes your notifications_manager has or can be extended with this method
        success = await notifications_manager.mark_notification_read(
            user_id=user["user_id"],
            notification_id=notification_id,
            read=False  # Set read flag to False
        )
        
        if not success:
            raise HTTPException(status_code=404, detail="Notification not found")
        
        return {"message": "Notification marked as unread"}
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error marking notification as unread: {e}")
        raise HTTPException(status_code=500, detail=f"Error marking notification as unread: {str(e)}")

@router.post("/mark-all-read")
async def mark_all_notifications_read(user: dict = Depends(get_current_user)):
    """
    Mark all notifications as read
    """
    try:
        success = await notifications_manager.mark_all_notifications_read(user["user_id"])
        
        # Publish an event
        event_bus.publish("all_notifications_read", {"user_id": user["user_id"]})
        
        return {"message": "All notifications marked as read"}
    except Exception as e:
        logger.error(f"Error marking all notifications as read: {e}")
        raise HTTPException(status_code=500, detail=f"Error marking all notifications as read: {str(e)}")

@router.delete("/{notification_id}")
async def delete_notification(notification_id: str, user: dict = Depends(get_current_user)):
    """
    Delete a notification
    """
    try:
        success = await notifications_manager.delete_notification(
            user_id=user["user_id"],
            notification_id=notification_id
        )
        
        if not success:
            raise HTTPException(status_code=404, detail="Notification not found")
        
        return {"message": "Notification deleted"}
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error deleting notification: {e}")
        raise HTTPException(status_code=500, detail=f"Error deleting notification: {str(e)}")

@router.get("/count")
async def get_unread_notification_count(user: dict = Depends(get_current_user)):
    """
    Get count of unread notifications for the current user
    """
    try:
        # Get unread notifications for counting
        unread_notifications = await notifications_manager.get_user_notifications(
            user_id=user["user_id"],
            unread_only=True,
            limit=100,  # Large enough to get all unread
            offset=0
        )
        
        return {"unread_count": len(unread_notifications)}
    except Exception as e:
        logger.error(f"Error fetching unread notification count: {e}")
        raise HTTPException(status_code=500, detail=f"Error fetching unread count: {str(e)}")

