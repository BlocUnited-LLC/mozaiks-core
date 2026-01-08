# Notifications Module

## Overview
The Notifications module coordinates the creation, delivery, and display of alerts and messages (both in-app and email) that inform users of account activities, subscription updates, system events, and plugin alerts. It leverages real-time event processing via WebSocket and the event bus to ensure timely delivery of important information to users.

## Core Responsibilities
- In-app notification generation and delivery
- Email notification integration (when hosting service enabled)
- Notification preferences management
- Real-time notification updates via WebSocket
- Notification categorization and filtering
- Notification history tracking and management
- Plugin-specific notification registration and handling
- Background notification processing with queuing

## Dependencies

### Internal Dependencies
- **Orchestration**: API route registration via `director.py`
- **WebSocket**: Real-time message delivery via `websocket_manager`
- **Database**: MongoDB collections for user data
- **Event Bus**: Event subscription and publishing
- **Settings**: User notification preferences
- **Plugins**: Plugin-specific notification registration

### External Dependencies
- **aiohttp**: Async HTTP client for email service integration
- **asyncio**: Asynchronous operations and queuing
- **fastapi**: API route definition
- **react**: Frontend components for notification display

## API Reference

### Backend Endpoints

#### `GET /api/notifications`
Get notifications for the current user.
- **Parameters**:
  - `unread_only` (bool, optional): Only return unread notifications
  - `limit` (int, optional): Maximum number of notifications to return
  - `offset` (int, optional): Offset for pagination
  - Requires authentication
- **Returns**: Notifications array with count and unread count

#### `GET /api/notifications/config`
Get notification configuration and user preferences.
- **Parameters**:
  - Requires authentication
- **Returns**: Notification config and user preferences

#### `POST /api/notifications/preferences`
Update notification preferences for the current user.
- **Parameters**:
  - Preferences object in request body
  - Requires authentication
- **Returns**: Updated preferences

#### `POST /api/notifications/{notification_id}/read`
Mark a notification as read.
- **Parameters**:
  - `notification_id`: ID of notification to mark as read
  - Requires authentication
- **Returns**: Success message

#### `POST /api/notifications/{notification_id}/unread`
Mark a notification as unread.
- **Parameters**:
  - `notification_id`: ID of notification to mark as unread
  - Requires authentication
- **Returns**: Success message

#### `POST /api/notifications/mark-all-read`
Mark all notifications as read.
- **Parameters**:
  - Requires authentication
- **Returns**: Success message

#### `DELETE /api/notifications/{notification_id}`
Delete a notification.
- **Parameters**:
  - `notification_id`: ID of notification to delete
  - Requires authentication
- **Returns**: Success message

#### `GET /api/notifications/count`
Get count of unread notifications.
- **Parameters**:
  - Requires authentication
- **Returns**: Unread notification count

### Backend Methods

#### `notifications_manager.create_notification(user_id, notification_type, title, message, metadata=None)`
Create a new notification for a user.
- **Parameters**:
  - `user_id` (str): User ID to create notification for
  - `notification_type` (str): Type of notification
  - `title` (str): Notification title
  - `message` (str): Notification message
  - `metadata` (dict, optional): Additional metadata
- **Returns**: Notification ID object

#### `notifications_manager.create_bulk_notifications(user_ids, notification_type, title, message, metadata=None)`
Create notifications for multiple users at once.
- **Parameters**:
  - `user_ids` (list): List of user IDs
  - `notification_type` (str): Type of notification
  - `title` (str): Notification title
  - `message` (str): Notification message
  - `metadata` (dict, optional): Additional metadata
- **Returns**: List of notification IDs

#### `notifications_manager.get_user_notifications(user_id, unread_only=False, limit=20, offset=0)`
Get notifications for a user.
- **Parameters**:
  - `user_id` (str): User ID to get notifications for
  - `unread_only` (bool, optional): Only return unread notifications
  - `limit` (int, optional): Maximum number of notifications to return
  - `offset` (int, optional): Offset for pagination
- **Returns**: List of notifications

#### `notifications_manager.mark_notification_read(user_id, notification_id)`
Mark a notification as read.
- **Parameters**:
  - `user_id` (str): User ID
  - `notification_id` (str): Notification ID
- **Returns**: Boolean indicating success

#### `notifications_manager.mark_all_notifications_read(user_id)`
Mark all notifications as read for a user.
- **Parameters**:
  - `user_id` (str): User ID
- **Returns**: Boolean indicating success

#### `notifications_manager.delete_notification(user_id, notification_id)`
Delete a notification.
- **Parameters**:
  - `user_id` (str): User ID
  - `notification_id` (str): Notification ID
- **Returns**: Boolean indicating success

#### `notifications_manager.send_email(recipient, subject, message, notification_type=None)`
Send an email notification.
- **Parameters**:
  - `recipient` (str): Email recipient
  - `subject` (str): Email subject
  - `message` (str): Email body
  - `notification_type` (str, optional): Type of notification
- **Returns**: Boolean indicating success

### Frontend Methods

#### `useNotifications()` Hook
Custom React hook to access notification state and functions.
- **Returns**:
  - `notifications` (array): Notifications list
  - `unreadCount` (number): Count of unread notifications
  - `config` (object): Notification configuration
  - `preferences` (object): User notification preferences
  - `isLoading` (boolean): Loading state
  - `error` (string): Error message
  - `markAsRead(id)` (function): Mark notification as read
  - `markAllAsRead()` (function): Mark all as read
  - `deleteNotification(id)` (function): Delete notification
  - `updatePreferences(prefs)` (function): Update preferences

## Configuration

### Notification Config
Located at `/backend/core/config/notifications_config.json`.

Example structure:
```json
{
  "settings": {
    "default_email_frequency": "immediate",
    "default_enabled": true
  },
  "categories": [
    {
      "id": "account",
      "name": "Account",
      "description": "Notifications related to your account activity",
      "icon": "user"
    },
    {
      "id": "subscription",
      "name": "Subscription",
      "description": "Updates about your subscription and billing",
      "icon": "credit-card"
    },
    {
      "id": "system",
      "name": "System",
      "description": "System alerts and important information",
      "icon": "bell"
    },
    {
      "id": "plugins",
      "name": "Plugins",
      "description": "Notifications from installed plugins",
      "icon": "puzzle"
    }
  ],
  "core": {
    "display_name": "Core System",
    "notifications": [
      {
        "id": "subscription_updates",
        "label": "Subscription Updates",
        "description": "Receive updates about your subscription status",
        "category": "subscription",
        "channels": ["in_app", "email"],
        "default_enabled": true
      },
      {
        "id": "security_alerts",
        "label": "Security Alerts",
        "description": "Important security-related notifications",
        "category": "account",
        "channels": ["in_app", "email"],
        "default_enabled": true
      }
    ]
  },
  "plugins": {
    "plugin_name": {
      "display_name": "Plugin Display Name",
      "notifications": [
        {
          "id": "plugin_name_event_type",
          "label": "Human Readable Label",
          "description": "Description of the notification",
          "category": "plugins",
          "channels": ["in_app", "email"],
          "default_enabled": true
        }
      ]
    }
  }
}
```

### Environment Variables
- `HOSTING_SERVICE`: Set to "1" to enable email notifications
- `EMAIL_SERVICE_URL`: URL of the email service
- `EMAIL_SERVICE_API_KEY`: API key for the email service

## Data Models

### Notification Object
```typescript
interface Notification {
  id: string;             // Unique identifier
  user_id: string;        // User ID this notification belongs to
  type: string;           // Notification type identifier
  title: string;          // Notification title
  message: string;        // Notification message
  created_at: string;     // ISO timestamp
  read: boolean;          // Whether notification has been read
  metadata?: object;      // Optional additional data
}
```

### Notification Preference
```typescript
interface NotificationPreference {
  enabled: boolean;       // Whether this notification type is enabled
  frequency: string;      // Delivery frequency (e.g., "immediate", "daily")
}
```

### Notification Category
```typescript
interface NotificationCategory {
  id: string;             // Category identifier
  name: string;           // Display name
  description: string;    // Category description
  icon: string;           // Icon identifier
}
```

## Integration Points

### Plugin Notification Registration
Plugins can register their own notification types by defining a `notifications.json` file:

```json
{
  "notifications": [
    {
      "id": "my_plugin_event_occurred",
      "label": "Event Occurred",
      "description": "Notifies you when a specific event happens",
      "category": "plugins",
      "channels": ["in_app", "email"],
      "default_enabled": true
    }
  ]
}
```

### Triggering Notifications From Plugins
Plugins can trigger notifications using the notifications_manager:

```python
from core.notifications_manager import notifications_manager

# In your plugin logic
async def execute(data):
    # ... plugin logic ...
    
    # Trigger notification
    await notifications_manager.create_notification(
        user_id=data["user_id"],
        notification_type="my_plugin_event_occurred",
        title="Event Occurred",
        message="Something important happened in your plugin.",
        metadata={"event_id": "123"}
    )
    
    return {"result": "success"}
```

### Event Handling
The notifications module subscribes to several events to create notifications automatically:

```python
@on_event("subscription_updated")
async def handle_subscription_update(event_data):
    user_id = event_data.get("user_id")
    plan = event_data.get("plan")
    
    if user_id and plan:
        await create_notification(
            user_id=user_id,
            notification_type="subscription_updates",
            title="Subscription Updated",
            message=f"Your subscription has been updated to the {plan} plan.",
            metadata={"plan": plan}
        )
```

### Frontend Integration
Display notifications in React components:

```jsx
import { useNotifications } from './notifications/NotificationsContext';

const NotificationBadge = () => {
  const { unreadCount } = useNotifications();
  
  if (unreadCount === 0) return null;
  
  return (
    <span className="bg-red-500 text-white text-xs rounded-full h-5 w-5 flex items-center justify-center">
      {unreadCount > 9 ? '9+' : unreadCount}
    </span>
  );
};
```

## Events

### Events Published
- `notification_created`: When a notification is created
- `notification_read`: When a notification is marked as read
- `all_notifications_read`: When all notifications are marked as read
- `notification_deleted`: When a notification is deleted
- `notification_preferences_updated`: When notification preferences are updated

### Events Subscribed To
- `subscription_updated`: Creates subscription update notification
- `subscription_canceled`: Creates subscription canceled notification
- `plugin_settings_updated`: Creates settings updated notification

## Email Integration

When `HOSTING_SERVICE` is enabled, the module can send email notifications through an external email service. The email service should implement an API endpoint that accepts:

```json
{
  "recipient": "user@example.com",
  "subject": "Notification Subject",
  "message": "Notification message content",
  "notification_type": "notification_type_id"
}
```

## Notification Flow

1. **Creation Phase**:
   - Notification is created via direct call or event handler
   - Notification is added to background processing queue

2. **Processing Phase**:
   - Background worker processes the notification queue
   - User preferences are checked for notification type
   - Appropriate channels (in-app, email) are determined
   - In-app notification is saved to database
   - Email notification is sent if enabled

3. **Delivery Phase**:
   - Real-time in-app notification is sent via WebSocket
   - Email is delivered via email service
   - Notification appears in notification menu

4. **Management Phase**:
   - User can mark notifications as read/unread
   - User can delete notifications
   - Old notifications are automatically cleaned up

## Common Issues & Troubleshooting

### Notifications Not Appearing
- Check WebSocket connection status
- Verify user has notification type enabled in preferences
- Check notification queue is being processed
- Look for errors in notification processing logs

### Email Notifications Not Sending
- Verify `HOSTING_SERVICE` is set to "1"
- Check `EMAIL_SERVICE_URL` is correct
- Verify `EMAIL_SERVICE_API_KEY` is valid
- Look for connection errors to email service
- Check user's email notification preferences

### Missing Plugin Notifications
- Verify plugin has registered notification types
- Check plugin is triggering notifications correctly
- Ensure user has access to the plugin

### Performance Issues
- Check notification queue size
- Verify notification indexes are created
- Consider increasing background worker count
- Implement notification cleanup for old notifications

## Related Files
- `/backend/core/notifications_manager.py`
- `/backend/core/routes/notifications.py`
- `/backend/core/config/notifications_config.json`
- `/src/notifications/NotificationsContext.jsx`
- `/src/notifications/NotificationsMenu.jsx`