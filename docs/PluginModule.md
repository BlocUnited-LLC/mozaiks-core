# Plugins Module

## Overview
The Plugins module enables dynamic extension of platform features without modifying the core. It supports a sandboxed, modular architecture where plugins can be loaded dynamically on the backend and integrated seamlessly on the frontend.

## Core Responsibilities
- Dynamic discovery and loading of plugins
- Plugin registry management
- Secure execution of plugin code
- Plugin lifecycle management (init, execute, cleanup)
- Frontend integration of plugin UI components
- Access control based on user permissions
- Notification integration for plugin activities

## Dependencies

### Internal Dependencies
- **Orchestration**: Uses `event_bus` for plugin events, `state_manager` for caching
- **Auth**: For permission checking via `get_current_user`
- **Subscriptions**: For feature access control via `subscription_manager`
- **Settings**: For plugin-specific settings via `settings_manager`
- **Notifications**: For plugin-generated notifications via `notifications_manager`
- **Security**: For plugin code scanning via `security_scanner`

### External Dependencies
- **importlib**: For dynamic module loading
- **fastapi**: For API route registration
- **React**: For frontend plugin components
- **logging**: For centralized plugin operation logging

## API Reference

### Backend Methods

#### `plugin_manager.init_async()`
Initializes the plugin manager and loads plugins.
- **Returns**: Initialized plugin manager instance

#### `plugin_manager.execute_plugin(plugin_name, data)`
Executes a plugin with the given data.
- **Parameters**:
  - `plugin_name` (str): Name of the plugin to execute
  - `data` (dict): Data to pass to the plugin
- **Returns**: Result from the plugin execution

#### `plugin_manager.refresh_plugins()`
Updates the registry and reloads all plugins.
- **Returns**: Dict with refresh status and newly added plugins

#### `plugin_manager.check_plugin_exists(plugin_name)`
Checks if a plugin exists in the registry.
- **Parameters**:
  - `plugin_name` (str): Name of the plugin to check
- **Returns**: Boolean indicating whether the plugin exists

#### `plugin_manager.ensure_plugin_loaded(plugin_name)`
Ensures a specific plugin is loaded without refreshing all plugins.
- **Parameters**:
  - `plugin_name` (str): Name of the plugin to load
- **Returns**: Boolean indicating success

#### `plugin_manager.register_plugin_notifications(plugin_name)`
Register notification settings for a plugin.
- **Parameters**:
  - `plugin_name` (str): Name of the plugin
- **Returns**: Boolean indicating success

### Frontend Methods

#### `usePlugins()` Hook
Custom React hook to access plugin functionality.
- **Returns**:
  - `plugins` (array): List of available plugins
  - `isLoading` (boolean): Loading state
  - `hasPlugin(name)` (function): Check if plugin exists
  - `executePlugin(name, data)` (function): Execute plugin
  - `getPluginSettings(name)` (function): Get plugin settings
  - `savePluginSettings(name, settings)` (function): Save settings

#### `DynamicUIComponent` Component
React component that dynamically loads plugin components.
- **Props**:
  - `pluginName` (string): Name of the plugin
  - `componentName` (string, default="default"): Name of the component
  - `pluginProps` (object): Props to pass to the plugin component
  - `fallback` (ReactNode): Component to show while loading

## Configuration

### Plugin Registry
Located at `/backend/core/config/plugin_registry.json`.

Example structure:
```json
{
  "plugins": [
    {
      "name": "task_manager",
      "display_name": "Task Manager",
      "description": "Manage tasks and assignments",
      "version": "1.0.0",
      "enabled": true,
      "backend": "plugins.task_manager.logic"
    }
  ]
}
```

## Data Models

### Plugin Metadata
```typescript
interface PluginMetadata {
  name: string;           // Unique identifier
  display_name: string;   // Human-readable name 
  description: string;    // Brief description
  version: string;        // Semantic version
  enabled: boolean;       // Whether plugin is active
  backend: string;        // Import path for backend logic
}
```

## Integration Points

### Backend Integration
Plugins should export the following functions:
- `execute(data)` or `run(data)`: Main entry point for plugin execution
- `register_routes(app)` (optional): Register custom routes

### Frontend Integration
Plugins should export React components that can be loaded via:
```jsx
<DynamicUIComponent 
  pluginName="plugin_name" 
  componentName="default" 
  pluginProps={{}}
/>
```

## Plugin Development Guide

### Creating a New Plugin

1. **Create directory structure**:
   ```
   backend/plugins/my_plugin/
   ├── __init__.py
   ├── logic.py          # Required
   ├── routes.py         # Optional (for custom routes)
   └── models.py         # Optional (for data models)
   
   src/plugins/my_plugin/
   ├── index.js          # Main component
   ├── register.js       # Plugin metadata
   └── components/       # Additional components
   ```

2. **Implement required interfaces**:

   **Backend (logic.py)**:
   ```python
   async def execute(data):
     """
     Main entry point for plugin execution
     """
     # Your logic here
     return {"result": "success", "data": processed_data}
   
   # Optional event handlers
   from core.event_bus import on_event
   
   @on_event("some_event")
   async def handle_event(event_data):
     # Handle event
     pass
   ```

   **Backend (routes.py, optional)**:
   ```python
   async def register_routes(app):
     """
     Register WebSocket routes for this plugin
     """
     @app.websocket(f"/ws/{your_plugin_name}/{{user_id}}")
     async def websocket_endpoint(websocket: WebSocket, user_id: str):
         # WebSocket implementation
         pass
   ```

   **Frontend (register.js)**:
   ```javascript
   export default {
     name: "my_plugin",
     displayName: "My Plugin",
     description: "Description of my plugin",
     version: "1.0.0"
   };
   ```

   **Frontend (index.js)**:
   ```jsx
   import React from 'react';
   
   const MyPlugin = (props) => {
     return (
       <div className="plugin-container">
         <h2 className="plugin-header">My Plugin</h2>
         {/* Plugin UI here */}
       </div>
     );
   };
   
   export default MyPlugin;
   ```

3. **Settings Integration (optional)**:
   
   Create a `settings/SettingsPanel.jsx` component to provide a settings UI:
   
   ```jsx
   const SettingsPanel = ({ currentSettings, onSettingsChange }) => {
     // Settings form UI
     return (
       <div className="p-4">
         <h3>Plugin Settings</h3>
         {/* Settings form */}
       </div>
     );
   };
   
   export default SettingsPanel;
   ```

4. **Notification Integration (optional)**:

   Define notification types in your plugin's folder:
   ```
   backend/plugins/my_plugin/notifications.json
   ```
   
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

   Then trigger notifications in your code:
   ```python
   from core.notifications_manager import notifications_manager
   
   await notifications_manager.create_notification(
       user_id=user_id,
       notification_type="my_plugin_event_occurred",
       title="Event Occurred",
       message="Something important happened in the plugin.",
       metadata={"event_id": "123"}
   )
   ```

## Events

### Events Published
- `plugin_loaded`: When a plugin is successfully loaded
- `plugin_executed`: When a plugin is executed
- `plugin_settings_updated`: When plugin settings are updated
- `plugin_execution_error`: When a plugin execution fails

### Events Subscribed To
- `subscription_updated`: To refresh plugin access
- `settings_updated`: To refresh plugin settings

## Common Issues & Troubleshooting

### Plugin Not Loading
- Verify the plugin directory structure follows the convention
- Check that the plugin is registered in plugin_registry.json
- Ensure all required dependencies are installed
- Look for import errors in the server logs

### Access Control Issues
- Plugins require subscription access configuration in subscription_config.json
- Check the user's subscription tier and plugin access in director.py

### Frontend Component Not Rendering
- Verify the component is properly exported from index.js
- Check for JavaScript errors in the browser console
- Ensure the plugin name matches exactly in the registry and DynamicUIComponent

### WebSocket Connection Problems
- Check that WebSocket routes are properly registered
- Verify authentication token is valid
- Look for connection errors in the browser console

## Related Files
- `/backend/core/plugin_manager.py`
- `/backend/core/config/plugin_registry.json`
- `/backend/core/director.py` (API endpoints for plugin execution)
- `/src/core/plugins/PluginProvider.jsx`
- `/src/core/plugins/DynamicUIComponent.jsx`
- `/src/core/plugins/usePlugins.js`