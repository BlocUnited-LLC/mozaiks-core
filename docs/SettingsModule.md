# Settings Module

## Overview
The Settings module manages global and user-specific configuration settings throughout the Mozaiks platform. It handles user profiles, notification preferences, appearance settings, and plugin-specific configurations, ensuring that all customizable aspects of the platform can be dynamically updated and that changes propagate properly across the system.

## Core Responsibilities
- Loading and parsing settings configuration
- Managing user profile settings
- Handling notification preferences
- Supporting plugin-specific settings
- Dynamic settings visibility based on subscription level
- Settings validation and error handling
- Settings UI generation and management
- Settings caching and refreshing

## Dependencies

### Internal Dependencies
- **Orchestration**: Integration with director for API endpoints
- **Database**: MongoDB collections for settings storage
- **Event Bus**: Event publishing for settings changes
- **Subscriptions**: Access control for settings visibility
- **Plugins**: Plugin-specific settings handling

### External Dependencies
- **fastapi**: API endpoint definition
- **pymongo**: Database interaction
- **react**: Frontend components for settings UI

## API Reference

### Backend Endpoints

#### `GET /api/settings-config`
Returns the settings configuration structure.
- **Parameters**:
  - Requires authentication
- **Returns**: Complete settings configuration object with sections and fields

#### `GET /api/user-profile`
Returns user profile information.
- **Parameters**:
  - Requires authentication
- **Returns**: User profile details

#### `POST /api/update-profile`
Updates user profile information.
- **Parameters**:
  - Request body with fields to update
  - Requires authentication
- **Returns**: Success message

#### `GET /api/plugin-settings/{plugin_name}`
Get settings for a specific plugin.
- **Parameters**:
  - `plugin_name`: Name of the plugin
  - Requires authentication
- **Returns**: Plugin settings object

#### `POST /api/plugin-settings/{plugin_name}`
Save settings for a specific plugin.
- **Parameters**:
  - `plugin_name`: Name of the plugin
  - Request body with settings data
  - Requires authentication
- **Returns**: Success message

#### `POST /api/notification-preferences`
Update notification preferences.
- **Parameters**:
  - Request body with notification preferences
  - Requires authentication
- **Returns**: Success message with updated preferences

### Backend Methods

#### `settings_manager.load_settings_config()`
Load settings configuration from file.
- **Returns**: Settings configuration object

#### `settings_manager.refresh_settings_config()`
Reload settings configuration from file.
- **Returns**: Updated settings configuration object

#### `settings_manager.get_user_settings(user_id)`
Get all settings for a user.
- **Parameters**:
  - `user_id` (str): User ID
- **Returns**: User settings object

#### `settings_manager.get_plugin_settings(user_id, plugin_name)`
Get settings for a specific plugin.
- **Parameters**:
  - `user_id` (str): User ID
  - `plugin_name` (str): Plugin name
- **Returns**: Plugin settings object

#### `settings_manager.save_plugin_settings(user_id, plugin_name, settings_data)`
Save settings for a specific plugin.
- **Parameters**:
  - `user_id` (str): User ID
  - `plugin_name` (str): Plugin name
  - `settings_data` (dict): Settings to save
- **Returns**: Success message

#### `settings_manager.get_notification_preferences(user_id)`
Get notification preferences for a user.
- **Parameters**:
  - `user_id` (str): User ID
- **Returns**: Notification preferences object

#### `settings_manager.save_notification_preferences(user_id, preferences)`
Save notification preferences for a user.
- **Parameters**:
  - `user_id` (str): User ID
  - `preferences` (dict): Notification preferences
- **Returns**: Updated preferences object

#### `settings_manager.update_settings_visibility(monetization_enabled, user_id)`
Update settings visibility based on subscription status.
- **Parameters**:
  - `monetization_enabled` (bool): Whether monetization is enabled
  - `user_id` (str): User ID
- **Returns**: Updated settings configuration

### Frontend Methods

#### `useProfile()` Hook
Custom React hook to access profile and settings functionality.
- **Returns**:
  - `profile` (object): User profile data
  - `settingsConfig` (array): Settings configuration
  - `pluginSettings` (object): Plugin-specific settings
  - `isLoading` (boolean): Loading state
  - `error` (string): Error message
  - `isSaving` (boolean): Save in progress
  - `updateProfile(data)` (function): Update profile
  - `getPluginSettings(pluginName)` (function): Get plugin settings
  - `updatePluginSettings(pluginName, settings)` (function): Update plugin settings

## Configuration

### Settings Configuration
Located at `/backend/core/config/settings_config.json`.

Example structure:
```json
{
  "profile_sections": [
    {
      "id": "personal",
      "title": "Personal Information",
      "icon": "user",
      "order": 1,
      "visible": true,
      "editable": true,
      "fields": [
        {
          "id": "full_name",
          "label": "Full Name",
          "type": "text",
          "required": true,
          "editable": true
        },
        {
          "id": "email",
          "label": "Email Address",
          "type": "email",
          "required": true,
          "editable": false
        }
      ]
    },
    {
      "id": "notifications",
      "title": "Notifications",
      "icon": "bell",
      "order": 3,
      "visible": true,
      "editable": true,
      "fields": [
        {
          "id": "email_notifications",
          "label": "Email Notifications",
          "type": "toggle",
          "category": "account",
          "description": "Receive notifications via email",
          "required": false,
          "editable": true
        }
      ],
      "plugin_notification_fields": []
    }
  ]
}
```

## Data Models

### Settings Object
```typescript
interface UserSettings {
  user_id: string;               // User ID
  plugin_settings: {             // Plugin-specific settings
    [plugin_name: string]: any;  // Plugin settings objects
  };
  notification_preferences: {    // Notification preferences
    [notification_id: string]: {
      enabled: boolean;          // Whether enabled
      frequency: string;         // Delivery frequency
    }
  };
}
```

### Settings Field Types
The settings module supports these field types:
- `text`: Text input field
- `email`: Email input field
- `textarea`: Multi-line text input
- `password-change`: Password change functionality
- `toggle`: Boolean toggle switch
- `theme-selector`: Theme selection control
- `select`: Dropdown selection
- `image`: Image upload field
- `plugin-settings`: Custom plugin settings panel

## Integration Points

### Plugin Settings Integration
Plugins can integrate with the settings system by providing a settings panel component:

```jsx
// In plugin's settings/SettingsPanel.jsx
const SettingsPanel = ({ currentSettings, onSettingsChange }) => {
  // Settings form UI
  return (
    <div className="p-4">
      <h3>Plugin Settings</h3>
      <form>
        {/* Settings form fields */}
        <button onClick={() => onSettingsChange(newSettings)}>
          Save Settings
        </button>
      </form>
    </div>
  );
};

export default SettingsPanel;
```

### Profile Page Integration
The profile page integrates with the settings system for user settings:

```jsx
// Using the settings in ProfilePage.jsx
const ProfilePage = () => {
  const { profile, settingsConfig, updateProfile } = useProfile();
  
  // Render settings UI based on settingsConfig
  // ...
};
```

### Notification Preferences
Notification preferences are managed through the settings system:

```jsx
// In NotificationsContext.jsx
const { authFetch } = useAuth();

const updatePreferences = async (newPreferences) => {
  try {
    const response = await authFetch('/api/notifications/preferences', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(newPreferences)
    });
    
    if (!response.ok) throw new Error('Failed to update preferences');
    
    const data = await response.json();
    setPreferences(data.preferences);
    return true;
  } catch (err) {
    console.error('Error updating preferences:', err);
    return false;
  }
};
```

## Events

### Events Published
- `settings_updated`: When any settings are updated
- `profile_updated`: When profile information is updated
- `plugin_settings_updated`: When plugin settings are updated
- `notification_preferences_updated`: When notification preferences are updated

### Events Subscribed To
- None directly, but affected by subscription events for access control

## Common Issues & Troubleshooting

### Settings Not Saving
- Check database connection and permissions
- Verify user authentication
- Look for validation errors in settings format
- Check event bus for errors in event handling

### Settings Not Appearing
- Verify settings_config.json is valid JSON
- Check subscription access for plugin settings
- Ensure settings fields are properly defined
- Look for visibility and editable flags

### Plugin Settings Issues
- Verify plugin has registered its settings correctly
- Check plugin access control
- Ensure plugin settings component is properly exported
- Verify settings schema matches between frontend and backend

### Profile Updates Not Reflected
- Check protected fields list in update_profile handler
- Verify events are being published correctly
- Check frontend caching logic
- Look for database write errors

## Dynamic Settings UI

The ProfilePage component dynamically renders UI based on the settings configuration:

```jsx
// Rendering field based on type
const renderFormField = (field, value, error, handleChange) => {
  switch (field.type) {
    case 'text':
      return (
        <div key={field.id} className="mb-4">
          <label htmlFor={field.id} className="block text-sm font-medium text-text-secondary mb-1">
            {field.label} {field.required && <span className="text-red-500">*</span>}
          </label>
          <input
            type="text"
            value={value}
            onChange={e => handleChange && handleChange(field.id, e.target.value)}
            // ...more props
          />
          {error && <p className="mt-1 text-sm text-red-600">{error}</p>}
        </div>
      );
    
    // Other field types...
  }
};
```

## Related Files
- `/backend/core/settings_manager.py`
- `/backend/core/config/settings_config.json`
- `/backend/core/director.py` (settings endpoints)
- `/src/profile/ProfileContext.jsx`
- `/src/profile/ProfilePage.jsx`
