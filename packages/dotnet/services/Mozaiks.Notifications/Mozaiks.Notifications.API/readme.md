---

## 📚 Table of Contents
1. [Email Notifications](#email-notifications)  
2. [Push Notifications](#push-notifications)  

---

## Email Notifications  
**Who & Why**: The system or an admin sends transactional or alert emails to users—and users/admins can track delivery and status.  

### 🔌 Endpoints  
- `POST /api/EmailNotification/SendEmailNotification`  
- `GET  /api/EmailNotification/GetEmailNotificationById/{id}`  
- `GET  /api/EmailNotification/GetEmailNotifications/{userId}/{appId}`  
- `PUT  /api/EmailNotification/UpdateStatus/{id}`  
- `DELETE /api/EmailNotification/{id}`  

### 📨 Flow: Send → Track → Update  
1. **Send Email**  
   - **Trigger**: A new event (e.g. “Welcome!” or “Password Reset”) 🔔  
   - **API Call**: `POST SendEmailNotification`  
     - **Payload** (plain English):  
       - **recepientId**: user’s unique ID  
      - **appId**: app’s ID  
       - **type**: 0=Transactional, 1=Promotional  
       - **email**: user@example.com  
       - **subject**: “Welcome to Our Platform”  
       - **message**: HTML or plain text body  
       - **status**: 0=Pending, 1=Sent, 2=Failed  
   - **Next**: Returns 200 “Success.” Email is enqueued or sent.  
   - **UI Change**: “Email queued” toast in admin console.

2. **View a Single Email**  
   - **User Action**: Click a notification in history to inspect details 📬  
   - **API Call**: `GET GetEmailNotificationById/{id}`  
     - **Param**: notification’s ID  
   - **Returns**: Full NotificationModel (timestamps, status).  
   - **UI**: Show subject, message preview, send date, status.

3. **List All Emails**  
   - **User Action**: Open “Email History” for a user & app 🗄️  
    - **API Call**: `GET GetEmailNotifications/{userId}/{appId}`  
       - **Params**: userId, appId  
   - **Returns**: Array of NotificationModel.  
   - **UI**: Paginated table with subject, date, status badges.

4. **Update Status**  
   - **Trigger**: Automated retry or manual override (e.g. mark as failed/resend) 🔄  
   - **API Call**: `PUT UpdateStatus/{id}`  
     - **Param**: notification ID  
     - **Payload**: Updated NotificationModel (e.g. `status`: 1=Sent)  
   - **Side Effects**: Status badge updates; if ‘Failed’, triggers retry logic.

5. **Delete Notification**  
   - **User Action**: Admin cleans up old logs 🗑️  
   - **API Call**: `DELETE /api/EmailNotification/{id}`  
   - **UI Change**: Row removed from history table.

---

## Push Notifications  
**Who & Why**: The system or an admin pushes real-time alerts to user devices (web/mobile), and can track delivery/status.  

### 🔌 Endpoints  
- `POST /api/PushNotification/SendPushNotification`  
- `GET  /api/PushNotification/GetPushNotificationById/{id}`  
- `GET  /api/PushNotification/GetPushNotifications/{userId}/{appId}`  
- `PUT  /api/PushNotification/UpdateStatus/{id}`  
- `DELETE /api/PushNotification/{id}`  

### 📲 Flow: Broadcast → Monitor → Manage  
1. **Send Push**  
   - **Trigger**: New event (e.g. “New Message” or “Task Assigned”) 📣  
   - **API Call**: `POST SendPushNotification`  
     - **Payload** (plain English):  
       - **recepientId**: target user ID  
      - **appId**: app’s ID  
       - **type**: 0=Alert, 1=Reminder  
       - **message**: “You have a new task assigned”  
       - **status**: 0=Pending, 1=Delivered, 2=Failed  
   - **Next**: Returns 200 “Success.” Push is sent via FCM/APNs or similar.  
   - **UI Change**: Admin sees “Push sent” confirmation.

2. **Inspect a Push**  
   - **Action**: Click a push record in dashboard 🔍  
   - **API Call**: `GET GetPushNotificationById/{id}`  
   - **Returns**: NotificationModel with timestamps and status.  
   - **UI**: Show full payload, delivery status.

3. **List Push History**  
   - **Action**: Open “Push History” for a user & app 📊  
   - **API Call**: `GET GetPushNotifications/{userId}/{appId}`  
   - **UI**: Table with message snippet, send date, status icon.

4. **Update Status**  
   - **Trigger**: Retry failed push or mark manual status 🔄  
   - **API Call**: `PUT UpdateStatus/{id}`  
     - **Payload**: NotificationModel with new `status`  
   - **Side Effects**: Status badge refreshes; errors logged if still failed.

5. **Delete Push Record**  
   - **Action**: Clean up old push logs 🗑️  
   - **API Call**: `DELETE /api/PushNotification/{id}`  
   - **UI Change**: Record disappears from history.

---
