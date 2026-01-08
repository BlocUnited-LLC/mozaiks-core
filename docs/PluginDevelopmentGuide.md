# Plugin Development Guide

## Overview
This guide provides comprehensive instructions for developing plugins for the Mozaiks platform. Plugins extend the platform's functionality without modifying the core codebase, allowing for modular and maintainable feature additions.

## Plugin Architecture
Mozaiks plugins consist of two main components:
1. **Backend Component**: Python code that integrates with the core server
2. **Frontend Component**: React components for the user interface

These components work together to provide seamless integration with the platform while maintaining separation from the core system.

## Directory Structure
Create the following structure for your plugin:

```
backend/
└── plugins/
    └── your_plugin_name/
        ├── __init__.py
        ├── logic.py          # Main plugin logic
        ├── routes.py         # Optional: WebSocket routes
        └── models.py         # Optional: Data models

src/
└── plugins/
    └── your_plugin_name/
        ├── index.js          # Main React component
        ├── register.js       # Plugin metadata
        ├── components/       # Additional React components
        └── settings/         # Optional: Settings components
            └── SettingsPanel.jsx
```

## Backend Development

### 1. Create the Plugin Logic

The main entry point for your plugin's backend is the `logic.py` file:

```python
# backend/plugins/your_plugin_name/logic.py

import logging
from core.event_bus import on_event

logger = logging.getLogger("mozaiks_plugins.your_plugin_name")

async def execute(data):
    """
    Main entry point for plugin execution.
    This function is called when the plugin is executed from the frontend.
    
    Args:
        data (dict): Data passed from the frontend
        
    Returns:
        dict: Response data
    """
    user_id = data.get("user_id")
    
    # Your plugin logic here
    logger.info(f"Plugin executed for user {user_id}")
    
    # Return response data
    return {
        "success": True,
        "message": "Plugin executed successfully",
        "data": {
            # Your response data here
        }
    }

# Optional: Event handlers
@on_event("some_system_event")
async def handle_system_event(event_data):
    """
    Handle system events.
    This function is called when a system event occurs.
    
    Args:
        event_data (dict): Event data
    """
    logger.info(f"System event received: {event_data}")
    # Handle the event
```

### 2. Add WebSocket Support (Optional)

If your plugin needs real-time communication, create a `routes.py` file:

```python
# backend/plugins/your_plugin_name/routes.py

from fastapi import WebSocket, WebSocketDisconnect
from core.websocket_manager import websocket_manager

async def register_routes(app):
    """
    Register WebSocket routes for this plugin.
    
    Args:
        app (FastAPI): FastAPI application instance
    """
    @app.websocket(f"/ws/your_plugin_name/{{user_id}}")
    async def websocket_endpoint(websocket: WebSocket, user_id: str):
        await websocket_manager.connect(user_id, websocket)
        try:
            while True:
                data = await websocket.receive_json()
                # Process data
                await websocket_manager.send_to_user(user_id, {
                    "type": "your_plugin_name",
                    "data": {
                        # Response data
                    }
                })
        except WebSocketDisconnect:
            websocket_manager.disconnect(user_id, websocket)
```

### 3. Define Data Models (Optional)

For complex data structures, create a `models.py` file:

```python
# backend/plugins/your_plugin_name/models.py

from pydantic import BaseModel
from typing import List, Optional
from datetime import datetime

class YourDataModel(BaseModel):
    """
    Your data model.
    """
    id: str
    name: str
    created_at: datetime
    updated_at: Optional[datetime] = None
    metadata: Optional[dict] = None
```

### 4. Register Notifications (Optional)

Create a `notifications.json` file to define your plugin's notification types:

```json
{
  "notifications": [
    {
      "id": "your_plugin_name_event_occurred",
      "label": "Event Occurred",
      "description": "Notifies you when a specific event happens",
      "category": "plugins",
      "channels": ["in_app", "email"],
      "default_enabled": true
    }
  ]
}
```

## Frontend Development

### 1. Create Plugin Metadata

Define your plugin metadata in `register.js`:

```javascript
// src/plugins/your_plugin_name/register.js

export default {
  name: "your_plugin_name",
  displayName: "Your Plugin Name",
  description: "Brief description of your plugin",
  version: "1.0.0",
  author: "Your Name",
  icon: "plugin" // Icon identifier
};
```

### 2. Implement Main Component

Create your main UI component in `index.js`:

```jsx
// src/plugins/your_plugin_name/index.js

import React, { useState, useEffect } from 'react';
import { usePlugins } from '../../core/plugins/usePlugins';
import { useAuth } from '../../auth/AuthContext';

const YourPlugin = () => {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  
  const { executePlugin } = usePlugins();
  const { user } = useAuth();
  
  useEffect(() => {
    const fetchData = async () => {
      try {
        setLoading(true);
        
        // Execute plugin logic
        const result = await executePlugin('your_plugin_name', {
          // Your request data
        });
        
        setData(result.data);
        setError(null);
      } catch (err) {
        console.error('Error executing plugin:', err);
        setError(err.message || 'An error occurred');
      } finally {
        setLoading(false);
      }
    };
    
    fetchData();
  }, [executePlugin, user]);
  
  if (loading) return <div>Loading...</div>;
  if (error) return <div>Error: {error}</div>;
  
  return (
    <div className="plugin-container">
      <h2 className="plugin-header">Your Plugin Name</h2>
      
      {/* Your plugin UI */}
      <div className="p-4">
        {/* Content goes here */}
      </div>
    </div>
  );
};

export default YourPlugin;
```

### 3. Add Settings Support (Optional)

Create a settings panel component:

```jsx
// src/plugins/your_plugin_name/settings/SettingsPanel.jsx

import React, { useState } from 'react';

const SettingsPanel = ({ currentSettings, onSettingsChange }) => {
  const [settings, setSettings] = useState(currentSettings || {});
  
  const handleChange = (key, value) => {
    const newSettings = { ...settings, [key]: value };
    setSettings(newSettings);
  };
  
  const handleSave = () => {
    onSettingsChange(settings);
  };
  
  return (
    <div className="p-4">
      <h3 className="text-lg font-medium mb-4">Plugin Settings</h3>
      
      <div className="space-y-4">
        {/* Settings fields */}
        <div className="mb-4">
          <label className="block text-sm font-medium mb-1">
            Example Setting
          </label>
          <input
            type="text"
            value={settings.exampleSetting || ''}
            onChange={(e) => handleChange('exampleSetting', e.target.value)}
            className="w-full px-3 py-2 border rounded"
          />
        </div>
      </div>
      
      <button
        onClick={handleSave}
        className="px-4 py-2 bg-accent text-white rounded mt-4"
      >
        Save Settings
      </button>
    </div>
  );
};

export default SettingsPanel;
```

### 4. WebSocket Integration (Optional)

For real-time features, integrate with WebSocket:

```jsx
// In your component file

import { useWebSocket } from '../../websockets/WebSocketProvider';

const YourPluginWithWebSocket = () => {
  const { subscribe, sendMessage, status } = useWebSocket('your_plugin_name');
  
  useEffect(() => {
    const unsubscribe = subscribe((data) => {
      if (data.type === 'your_plugin_name') {
        // Handle WebSocket message
        console.log('Received data:', data);
      }
    });
    
    return unsubscribe; // Clean up subscription
  }, [subscribe]);
  
  const handleAction = () => {
    sendMessage({
      type: 'action',
      data: {
        // Action data
      }
    });
  };
  
  return (
    <div className="plugin-container">
      <h2 className="plugin-header">Your Plugin Name</h2>
      <div className="p-4">
        <p>WebSocket status: {status}</p>
        <button onClick={handleAction}>Send Action</button>
      </div>
    </div>
  );
};
```

## Integration with Core Services

### 1. Database Access

Access MongoDB collections for your plugin:

```python
# In your logic.py

from core.config.database import db

async def execute(data):
    user_id = data.get("user_id")
    
    # Create a collection for your plugin
    collection = db[f"plugin_{your_plugin_name}"]
    
    # Insert document
    result = await collection.insert_one({
        "user_id": user_id,
        "data": data.get("data"),
        "created_at": datetime.utcnow().isoformat()
    })
    
    return {"success": True, "id": str(result.inserted_id)}
```

### 2. Event Bus

Publish and subscribe to events:

```python
# In your logic.py

from core.event_bus import event_bus, on_event

async def execute(data):
    # Your logic here
    
    # Publish an event
    event_bus.publish("your_plugin_event", {
        "user_id": data.get("user_id"),
        "action": "something_happened"
    })
    
    return {"success": True}

# Event subscription
@on_event("some_other_event")
async def handle_event(event_data):
    # Handle the event
    pass
```

### 3. Notifications

Send notifications to users:

```python
# In your logic.py

from core.notifications_manager import notifications_manager

async def execute(data):
    user_id = data.get("user_id")
    
    # Create a notification
    await notifications_manager.create_notification(
        user_id=user_id,
        notification_type="your_plugin_name_event_occurred",
        title="Something Happened",
        message="An important event occurred in your plugin.",
        metadata={"event_id": "123"}
    )
    
    return {"success": True}
```

### 4. Subscription Integration

Define access requirements in `subscription_config.json`:

```json
{
  "subscription_plans": [
    {
      "name": "free",
      "plugins_unlocked": []
    },
    {
      "name": "basic",
      "plugins_unlocked": ["your_plugin_name"]
    },
    {
      "name": "premium",
      "plugins_unlocked": ["*"]
    }
  ]
}
```

## Testing Your Plugin

### 1. Backend Testing

Test your plugin backend logic:

```python
# tests/plugins/your_plugin_name/test_logic.py

import pytest
from plugins.your_plugin_name.logic import execute

@pytest.mark.asyncio
async def test_execute():
    # Test data
    data = {
        "user_id": "test_user_id",
        "data": {
            "key": "value"
        }
    }
    
    # Execute plugin
    result = await execute(data)
    
    # Assert results
    assert result["success"] is True
    assert "message" in result
```

### 2. Frontend Testing

Test your React components:

```jsx
// src/plugins/your_plugin_name/__tests__/index.test.js

import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import YourPlugin from '../index';
import { PluginProvider } from '../../../core/plugins/PluginProvider';
import { AuthProvider } from '../../../auth/AuthContext';

// Mock the plugin execution
jest.mock('../../../core/plugins/usePlugins', () => ({
  usePlugins: () => ({
    executePlugin: jest.fn().mockResolvedValue({
      success: true,
      data: {
        // Mock data
      }
    })
  })
}));

describe('YourPlugin', () => {
  test('renders plugin component', async () => {
    render(
      <AuthProvider>
        <PluginProvider>
          <YourPlugin />
        </PluginProvider>
      </AuthProvider>
    );
    
    // Wait for data to load
    await waitFor(() => {
      expect(screen.getByText('Your Plugin Name')).toBeInTheDocument();
    });
    
    // Add more assertions
  });
});
```

## Deployment

### 1. Building Your Plugin

Ensure your plugin is properly structured and follows the platform guidelines.

### 2. Installation Steps

1. Place backend code in `backend/plugins/your_plugin_name/`
2. Place frontend code in `src/plugins/your_plugin_name/`
3. Update `plugin_registry.json` to include your plugin:
   ```json
   {
     "plugins": [
       {
         "name": "your_plugin_name",
         "display_name": "Your Plugin Name",
         "description": "Brief description of your plugin",
         "version": "1.0.0",
         "enabled": true,
         "backend": "plugins.your_plugin_name.logic"
       }
     ]
   }
   ```
4. Update `navigation_config.json` to add navigation entry (optional):
   ```json
   {
     "plugins": [
       {
         "plugin_name": "your_plugin_name",
         "label": "Your Plugin",
         "path": "/plugins/your_plugin_name",
         "icon": "plugin-icon"
       }
     ]
   }
   ```
5. Restart the server to load your plugin

## Best Practices

### 1. Error Handling

Always implement proper error handling in both backend and frontend:

```python
# Backend error handling
try:
    # Your code here
    result = await some_operation()
    return {"success": True, "data": result}
except Exception as e:
    logger.error(f"Error in plugin execution: {e}")
    return {"success": False, "error": str(e)}
```

```jsx
// Frontend error handling
try {
  const result = await executePlugin('your_plugin_name', data);
  // Process result
} catch (err) {
  console.error('Error executing plugin:', err);
  // Handle error gracefully
}
```

### 2. Security Considerations

- Never trust user input without validation
- Use parameterized queries for database operations
- Follow the principle of least privilege
- Don't store sensitive data in client-side storage
- Implement proper authentication checks

### 3. Performance Optimization

- Use pagination for large datasets
- Implement caching for expensive operations
- Optimize database queries with proper indexing
- Minimize API calls and batch operations when possible
- Use WebSockets for real-time updates instead of polling

## Troubleshooting

### Common Issues

1. **Plugin not loading**
   - Check plugin directory structure
   - Verify plugin is registered in `plugin_registry.json`
   - Look for import errors in server logs

2. **Frontend component not rendering**
   - Verify component is properly exported in `index.js`
   - Check for React errors in browser console
   - Ensure plugin name matches exactly in registry and component usage

3. **Access control issues**
   - Check subscription configuration for plugin access
   - Verify user has the correct subscription level
   - Look for authentication errors in logs

4. **WebSocket connection problems**
   - Ensure WebSocket route is properly registered
   - Check for connection errors in browser console
   - Verify authentication token is valid

## Examples

### Example Plugin: Task Manager

See the `task_manager` plugin included with Mozaiks for a complete example:

- Backend: `backend/plugins/task_manager/`
- Frontend: `src/plugins/task_manager/`

This plugin demonstrates:
- CRUD operations with MongoDB
- Real-time updates via WebSockets
- User interface with React
- Settings panel integration
- Notification integration
- Subscription access control