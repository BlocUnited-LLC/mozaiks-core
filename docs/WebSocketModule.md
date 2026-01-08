# WebSocket Module

## Overview
The WebSocket module enables real-time, bi-directional communication between clients and the server. In MozaiksCore, WebSockets are used for notifications and plugin-specific real-time updates. ChatUI and workflow streaming are runtime-owned concerns in MozaiksAI.

## Core Responsibilities
- Managing WebSocket connections by user
- Providing real-time communication between server and clients
- Enabling multiple simultaneous connections per user
- Secure message delivery to specific users
- Broadcasting capabilities for system-wide messages
- Connection tracking and cleanup
- Error handling and connection recovery
- Supporting notification streaming

## ⚠️ Boundary Policy: Plugin WebSocket Usage

**This policy is non-negotiable.**

### Plugins MAY:
- Register WebSockets for **notifications**
- Register WebSockets for **non-execution UI events** (e.g., real-time status updates, collaboration signals)

### Plugins MUST NOT:
- Stream LLM output
- Execute workflows
- Proxy runtime traffic
- Implement ChatUI semantics
- Handle any execution-layer transport

**Rationale**: MozaiksCore is a control plane. Execution and streaming belong to MozaiksAI (the runtime). This separation ensures that authorization decisions remain in MozaiksCore while execution remains stateless in MozaiksAI.

## Dependencies

### Internal Dependencies
- **Orchestration**: Integration with main application via `main.py`
- **Auth**: Authentication verification for secure connections
- **Notifications**: Real-time notification delivery
- **Plugins**: Plugin-specific WebSocket routes

### External Dependencies
- **fastapi**: WebSocket protocol implementation
- **React**: Frontend WebSocket client and context
- **logging**: Connection and message logging

## API Reference

### Backend Methods

#### `websocket_manager.connect(user_id, websocket)`
Accept and register a new WebSocket connection for a user.
- **Parameters**:
  - `user_id` (str): User ID to associate with connection
  - `websocket` (WebSocket): WebSocket connection object
- **Returns**: None

#### `websocket_manager.disconnect(user_id, websocket)`
Remove a WebSocket connection for a user.
- **Parameters**:
  - `user_id` (str): User ID associated with connection
  - `websocket` (WebSocket): WebSocket connection to remove
- **Returns**: None

#### `websocket_manager.send_to_user(user_id, message)`
Send a JSON message to all active WebSocket connections for a user.
- **Parameters**:
  - `user_id` (str): User ID to send message to
  - `message` (dict): JSON-serializable message
- **Returns**: None

#### `websocket_manager.broadcast(message)`
Broadcast a JSON message to all connected users.
- **Parameters**:
  - `message` (dict): JSON-serializable message
- **Returns**: None

### Frontend Methods

#### `useWebSocket()` Hook
Custom React hook to access WebSocket functionality.
- **Returns**:
  - `status` (string): Connection status ("connected", "disconnected", "error")
  - `subscribe(callback)` (function): Subscribe to WebSocket messages
  - `sendMessage(message)` (function): Send message through WebSocket

#### `WebSocketProvider` Component
React context provider for WebSocket functionality.
- **Props**:
  - `children` (ReactNode): Child components
  - `path` (string, default="notifications"): WebSocket endpoint path

## Configuration

### WebSocket Endpoint Paths
- `/ws/notifications/{user_id}`: For notification delivery
- `/ws/{plugin_name}/{user_id}`: For plugin-specific communications

### Message Types
WebSocket messages follow this general format:
```json
{
  "type": "message_type",
  "data": {
    // Message-specific data
  }
}
```

Common message types:
- `notification`: New notification
- `event`: System or plugin event

## Data Models

### WebSocket Connection
```python
class WebSocketManager:
    active_connections: Dict[str, List[WebSocket]] = {}
```

Each user can have multiple active connections, which are tracked in the `active_connections` dictionary.

### WebSocket Message
```typescript
interface WebSocketMessage {
  type: string;           // Message type identifier
  [key: string]: any;     // Additional message data
}
```

## Integration Points

### Backend Integration
Core WebSocket routes are registered in `main.py`:

```python
@app.websocket("/ws/notifications/{user_id}")
async def notifications_websocket(websocket: WebSocket, user_id: str):
    await websocket_manager.connect(user_id, websocket)
    try:
        while True:
            await websocket.receive_text()
    except WebSocketDisconnect:
        websocket_manager.disconnect(user_id, websocket)
```

Plugin WebSocket routes are registered dynamically:

```python
async def register_websockets(app):
    for plugin_name, plugin_data in plugin_manager.plugins.items():
        # Load and register plugin WebSocket routes
        # ...
```

### Frontend Integration
WebSocket providers wrap components that need real-time communication:

```jsx
// Notifications with WebSocket
<WebSocketProvider path="notifications">
  <NotificationsProvider>
    {children}
  </NotificationsProvider>
</WebSocketProvider>
```

Components can access WebSocket functionality using the hook:

```jsx
const MyComponent = () => {
  const { status, subscribe, sendMessage } = useWebSocket();
  
  useEffect(() => {
    const unsubscribe = subscribe((data) => {
      // Handle incoming WebSocket message
      console.log('Received message:', data);
    });
    
    return unsubscribe; // Clean up subscription
  }, [subscribe]);
  
  const handleAction = () => {
    sendMessage({
      type: 'custom_action',
      data: { /* ... */ }
    });
  };
  
  return (
    <div>
      <p>WebSocket status: {status}</p>
      <button onClick={handleAction}>Send Message</button>
    </div>
  );
};
```

## WebSocket Flow

### Connection Establishment
1. **Frontend Initialization**:
   - WebSocketProvider is rendered with a specific path
   - A WebSocket connection is attempted to `/ws/{path}/{user_id}`
   - Authentication token is included in the connection

2. **Backend Acceptance**:
   - WebSocket endpoint validates authentication
   - `websocket_manager.connect()` registers the connection
   - Connection is added to user's active connections

3. **Connection Management**:
   - Multiple connections per user are supported
   - Connections are tracked in the `active_connections` dictionary
   - Disconnections are handled and cleaned up

### Message Delivery
1. **Server to Client**:
   - Server calls `websocket_manager.send_to_user(user_id, message)`
   - Message is sent to all active connections for that user
   - Frontend receives message via the subscription callback

2. **Client to Server**:
   - Frontend calls `sendMessage(message)`
   - Message is sent through the WebSocket connection
   - Backend receives message in the WebSocket endpoint handler

### Broadcast Pattern
1. **System-wide Messages**:
   - Server calls `websocket_manager.broadcast(message)`
   - Message is sent to all connected users
   - All connected clients receive the message

## Common Issues & Troubleshooting

### Connection Issues
- Check that WebSocket URL is correct
- Verify authentication token is valid
- Look for CORS issues in browser console
- Ensure the WebSocket endpoint is registered

### Message Delivery Problems
- Check that user_id is correct for targeted messages
- Verify message format is JSON-serializable
- Look for connection errors or timeouts
- Check exception handling in WebSocket loops

### Multiple Tab Management
- WebSocketProvider handles connections per tab
- Messages are received in all open tabs for the user
- Check that state updates work properly across tabs

### Performance Considerations
- Multiple connections per user can impact server resources
- Consider implementing connection limits per user
- Monitor WebSocket traffic for bandwidth issues

## Related Files
- `/backend/core/websocket_manager.py`
- `/backend/main.py` (WebSocket route registration)
- `/src/websockets/WebSocketProvider.jsx`
- `/src/notifications/NotificationsContext.jsx` (WebSocket consumer)
- `/backend/core/plugin_manager.py` (plugin WebSocket registration)
