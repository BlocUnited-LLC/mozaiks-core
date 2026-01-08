# /backend/plugins/notes_manager/notifications.py

from core.notifications_manager import notifications_manager
import logging

logger = logging.getLogger("plugin.notes_manager.notifications")

async def notify_note_shared(user_id, note_id, shared_by_username):
    """
    Notify a user that a note was shared with them.
    """
    # Use the exact notification type defined in notifications_config.json
    await notifications_manager.create_notification(
        user_id=user_id,
        notification_type="notes_manager_shared",  # Matches the config
        title="A Note Was Shared With You",
        message=f"{shared_by_username} shared a note with you.",
        metadata={"note_id": note_id}
    )

async def notify_note_deleted(user_id, note_title):
    """
    Notify a user that a shared note was deleted.
    """
    # Use the exact notification type defined in notifications_config.json
    await notifications_manager.create_notification(
        user_id=user_id,
        notification_type="notes_manager_note_deleted",  # Matches the config
        title="Shared Note Deleted",
        message=f"The note '{note_title}' has been removed.",
        metadata={"note_title": note_title}
    )

# Testing function that can be called directly
async def send_test_notification(user_id):
    """
    Send a test notification to verify the notification system is working.
    """
    return await notifications_manager.create_notification(
        user_id=user_id,
        notification_type="notes_manager_note_created",
        title="Test Notification",
        message="This is a test notification from Notes Manager.",
        metadata={"test": True}
    )