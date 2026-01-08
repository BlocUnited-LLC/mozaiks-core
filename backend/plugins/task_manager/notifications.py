# /backend/plugins/task_manager/notifications.py
import logging
import asyncio
from core.event_bus import event_bus

logger = logging.getLogger("mozaiks_core.plugins.task_manager.notifications")

# The plugin name should match your plugin directory name
PLUGIN_NAME = "task_manager"

def send_notification(user_id, title, message, metadata=None):
    """
    Send a notification to a user from this plugin
    """
    from core.notifications_manager import notifications_manager
    
    # The notification_type will be automatically derived from the plugin name
    notification_type = f"{PLUGIN_NAME}_notifications"
    
    # Log that we're attempting to send a notification
    logger.info(f"Plugin {PLUGIN_NAME} attempting to send notification: {title} to user {user_id}")
    
    # This is an async function but we'll handle it properly
    async def _send():
        try:
            result = await notifications_manager.create_notification(
                user_id=user_id,
                notification_type=notification_type,
                title=title,
                message=message,
                metadata=metadata or {}
            )
            if result:
                logger.info(f"Successfully created notification for user {user_id}")
            else:
                logger.warning(f"Failed to create notification for user {user_id}")
            return result
        except Exception as e:
            logger.error(f"Error sending notification: {e}")
            return None
    
    # Create task to send notification
    asyncio.create_task(_send())
# Event handlers
def handle_task_reminder(event_data):
    """
    Handle task reminder events
    """
    user_id = event_data.get("user_id")
    task = event_data.get("task")
    
    if user_id and task:
        send_notification(
            user_id=user_id,
            title="Task Due Soon",
            message=f"Your task '{task['title']}' is due soon!",
            metadata={"task": task, "event_type": "reminder"}
        )

# Register event handlers
def register_event_handlers():
    """
    Register event handlers with the event bus
    """
    event_bus.subscribe("task_reminder", handle_task_reminder)
    logger.info(f"Registered event handlers for {PLUGIN_NAME} plugin")