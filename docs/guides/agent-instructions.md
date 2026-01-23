### 🤖 Agent Operating Manual: System Configuration & Core Management

The Mozaiks Core is a stable, production-grade application scaffold that abstracts away the repetitive and foundational elements common to all modern web apps — including authentication, routing, UI shell, state management, and basic CRUD patterns. It is designed to be immutable and agent-agnostic, with all dynamic behavior exposed through a declarative config.json schema. AI agents operate in sandboxed environments, generating self-contained plugins that register themselves via the configuration layer. These plugins can include backend endpoints, frontend components, UI routes, and database models — all integrated without mutating core logic. This architecture enforces separation of concerns, minimizes coupling, and allows agents to iteratively build and extend apps in a modular, composable fashion without reimplementing baseline functionality.

######################################################
⚙️ CONFIG AGENT BLUEPRINT — HOW TO MANAGE CONFIGURATIONS
######################################################

🔍 **PRIMARY SCOPE:**  
`/backend/core/config/*.json`  
As the Config Agent, you're responsible for analyzing and updating configuration files that integrate plugins with the core system.

---

## 📄 CONFIGURATION FILES OVERVIEW

These configuration files are what integrate your plugin into the core system:

| File                       | Purpose                                    | Example Update                                  |
|----------------------------|--------------------------------------------|-------------------------------------------------|
| `plugin_registry.json`     | Registers plugins with the system          | Add your plugin's metadata                      |
| `navigation_config.json`   | Controls navigation menu                   | Add a menu item for your plugin                 |
| `subscription_config.json` | Defines access levels                      | Specify which plans can access your plugin      |
| `settings_config.json`     | Controls settings UI                       | Add plugin-specific settings fields             |
| `notifications_config.json`| Defines notification types                 | Add custom notification types for your plugin   |

---

## 📱 NAVIGATION CONFIGURATION

The NAVIGATION CONFIGURATION defines the app’s navigational structure, making plugins accessible through the UI.

When updating `navigation_config.json`:

- Use clear, human-readable names for label
- Choose icons that visually reflect the plugin’s purpose
- Define path as /plugins/{plugin_name}
- Maintain default menu items like Profile and Subscription
- Do not modify unrelated menu entries

Example structure:
{
  "default": [
    {
      "label": "Profile",
      "path": "/profile",
      "icon": "user"
    },
    {
      "label": "Subscription",
      "path": "/subscriptions",
      "icon": "credit-card"
    }
  ],
  "plugins": [
    {
      "plugin_name": "task_manager",
      "label": "Task Manager",
      "path": "/plugins/task_manager",
      "icon": "check-square"
    },
    {
      "plugin_name": "notes_manager",
      "label": "Notes Manager",
      "path": "/plugins/notes_manager",
      "icon": "file-text"
    }
  ]
}

## 🔔 NOTIFICATIONS CONFIGURATION

The NOTIFICATIONS CONFIGURATION defines how your plugin communicates important events to users.

When updating `notifications_config.json`:

- Group events into existing or new categories
- Use plugin-prefixed IDs (e.g., task_manager_deadline_passed)
- Provide intuitive names and descriptions
- Set smart defaults for frequency and delivery channels
- Avoid over-notifying — stick to meaningful events
- Categorize carefully for user filtering and clarity

Example structure:
{
  "settings": {
    "default_email_frequency": "daily",
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
  ]
}

## ⚙️ SETTINGS CONFIGURATION

The SETTINGS CONFIGURATION defines what plugin settings appear in the user’s profile settings panel.

When updating `settings_config.json`:

- Use plugin-settings as the field type
- Assign plugin-specific sections or attach to relevant existing ones
- Maintain structure and user flow (defaults include: Personal, Security, Notifications, Appearance)
- Place settings where users intuitively expect them
- Naming Style: Use human-friendly section titles (e.g., "Notifications", not "plugin_xyz_config")

Example structure:
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
        },
        {
          "id": "bio",
          "label": "Bio",
          "type": "textarea",
          "required": false,
          "editable": true
        },
        {
          "id": "profile_picture",
          "label": "Profile Picture",
          "type": "image",
          "required": false,
          "editable": true
        }
      ]
    },
    {
      "id": "security",
      "title": "Security",
      "icon": "lock",
      "order": 2,
      "visible": true,
      "editable": true,
      "fields": [
        {
          "id": "password",
          "label": "Password",
          "type": "password-change",
          "required": false,
          "editable": true
        },
        {
          "id": "two_factor",
          "label": "Two-Factor Authentication",
          "type": "toggle",
          "required": false,
          "editable": true
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
        },
        {
          "id": "subscription_updates",
          "label": "Subscription Updates",
          "type": "toggle",
          "category": "subscription",
          "description": "Receive updates about your subscription status",
          "required": false,
          "editable": true
        }
      ],
      "plugin_notification_fields": [
        {
          "id": "notes_manager_notifications",
          "plugin": "notes_manager",
          "label": "Notes Manager Notifications",
          "type": "toggle",
          "category": "plugins",
          "description": "Receive notifications from Notes Manager",
          "required": false,
          "editable": true
        },
        {
          "id": "task_manager_notifications",
          "plugin": "task_manager",
          "label": "Task Manager Notifications",
          "type": "toggle",
          "category": "plugins",
          "description": "Receive notifications from Task Manager",
          "required": false,
          "editable": true
        }
      ]
    },
    {
      "id": "appearance",
      "title": "Appearance",
      "icon": "palette",
      "order": 4,
      "visible": true,
      "editable": true,
      "fields": [
        {
          "id": "theme",
          "label": "Theme",
          "type": "theme-selector",
          "required": false,
          "editable": true
        }
      ]
    }
  ]
}

## 💰 SUBSCRIPTION CONFIGURATION

The SUBSCRIPTION CONFIGURATION controls plugin availability based on the user's subscription tier.

When updating `subscription_config.json`:

- Determine the appropriate subscription tier for the plugin  
- Add the plugin name to the `plugins_unlocked` array for that tier  
- Consider the plugin's value and complexity when deciding tier placement  
- For premium plugins, add to both premium and higher tiers  
- For basic plugins, add to basic and higher tiers  
- For free plugins, add to all tiers or don’t add at all (they’re accessible by default)
- Maintain the existing structure without disrupting other subscription items.  

When defining features for subscription plans in `subscription_config.json`:

- **Be Descriptive but General**: Write feature descriptions that accurately represent what the plugin offers, without promising specific technical limitations.

- **Align with Plugin Capabilities**: Ensure feature descriptions match what plugins actually provide.

- **Use Access-Based Language**: Focus on which features/plugins are accessible, rather than specific usage limitations.

- **Examples of Good Feature Descriptions**:
  - ✅ "Access to the Notes plugin" (vs. "Up to 5 notes")
  - ✅ "Basic document management features" (vs. "5 MB storage limit")
  - ✅ "Standard analytics dashboard" (vs. "3 custom reports")

- **Avoid Quantitative Limits Unless Implemented**: Don't specify numerical limits (like "5 notes") unless the plugin explicitly implements and enforces this logic.

Example structure:
{
    "settings": {
      "show_subscription_status": true,
      "show_cancel_button": true,
      "trial_period_days": 14,
      "trial_plan": "premium",
      "default_plan": "free"
    },
    "subscription_plans": [
      {
        "name": "free",
        "display_name": "Free",
        "price": 0,
        "billing_cycle": "monthly",
        "features": [
          "Basic access to the platform",
          "Limited number of plugins",
          "Up to 5 notes"
        ],
        "plugins_unlocked": [
        ]
      },
      {
        "name": "basic",
        "display_name": "Basic",
        "price": 9.99,
        "billing_cycle": "monthly",
        "features": [
          "Full access to the platform",
          "Access to basic plugins",
          "Unlimited notes",
          "Email support"
        ],
        "plugins_unlocked": [
          "notes_manager"
        ]
      },
      {
        "name": "premium",
        "display_name": "Premium",
        "price": 19.99,
        "billing_cycle": "monthly",
        "features": [
          "Everything in Basic",
          "Access to all plugins",
          "Priority support",
          "Advanced features"
        ],
        "plugins_unlocked": [
          "*"
        ]
      }
    ]
  }


## 💰 THEME CONFIGURATION

The THEME CONFIGURATION governs the visual identity and theming options for the app.

When updating `theme_config.json`:

- Define color palettes, logos, typography, and layout spacing
- Add new themes that follow accessible contrast and aesthetic standards
- Update branding assets (logo, favicon, name) carefully
- Set a default_theme to ensure consistent first impressions

Example structure:
{
    "branding": {
        "logo_url": "/assets/logo.png",
        "favicon_url": "/assets/favicon.ico",
        "app_name": "Plutus"
    },
    "available_themes": [
        {
            "name": "Day Light",
            "id": "light",
            "colors": {
                "primary": "#FFFFFF",
                "secondary": "#F5F7FA",
                "accent": "#0047AB",
                "background": "#FFFFFF",
                "text_primary": "#000000",
                "text_secondary": "#555555"
            }
        },
        {
            "name": "Midnight Dark",
            "id": "dark",
            "colors": {
                "primary": "#1E1E1E",
                "secondary": "#252526",
                "accent": "#007ACC",
                "background": "#1E1E1E",
                "text_primary": "#FFFFFF",
                "text_secondary": "#A1A1A1"
            }
        },
        {
            "name": "Purple Dream",
            "id": "purple",
            "colors": {
                "primary": "#2D1B4E",
                "secondary": "#513D7F",
                "accent": "#8367C7",
                "background": "#1A0E2E",
                "text_primary": "#FFFFFF",
                "text_secondary": "#C8B6E2"
            }
        }
    ],
    "default_theme": "light",
    "typography": {
        "font_family": "Inter, sans-serif",
        "base_font_size": "16px",
        "heading_weight": "600",
        "body_weight": "400"
    },
    "layout": {
        "border_radius": "8px",
        "spacing_unit": "10px",
        "max_width": "1200px",
        "container_padding": "20px"
    }
}


######################################################
📦 PLUGIN AGENT BLUEPRINT — HOW TO BUILD A PLUGIN
######################################################

🔍 PRIMARY SCOPE:
  /backend/plugins/{plugin_name}/
  /src/plugins/{plugin_name}/

🧼 You may ONLY work inside your own plugin’s folder.
❌ Do NOT create logic that depends on or requires changes to core system files.

------------------------------------------------------
🧠 DESIGN PRINCIPLES
------------------------------------------------------

- Plugins must be **modular** and **self-contained**
- All logic should run through your own `logic.py` and frontend files
- If your logic seems like it needs global state, rethink your approach
- Use the config files to hook into the system (navigation, access, settings)

🧱 THINK IN MODULES. BUILD LIKE LEGO.

------------------------------------------------------
📁 DIRECTORY STRUCTURE (EXPANDED EXAMPLE)
------------------------------------------------------

## Directory Structure
A complete plugin consists of these components:

/backend/plugins/<plugin_name>/
├── __init__.py                ← required for Python package
├── logic.py                   ← core backend logic (FastAPI-compatible handler)
└── notifications.py           ← (optional) notification-specific handlers

/src/plugins/<plugin_name>/
├── components/                ← reusable React components
│   ├── <page>.jsx
│   └── <page>.jsx
├── index.js                   ← main entry point for rendering
└── register.js                ← plugin metadata (name, description, version)

------------------------------------------------------
🚀 BUILDING A COMPLETE PLUGIN
------------------------------------------------------

1️⃣ BACKEND FILES

Create a folder in `/backend/plugins/{plugin_name}/` containing:

- `__init__.py`: Required for Python package structure
- `logic.py`: Main entry point implementing:
  - `async def execute(data):` function that handles all plugin actions
  - Parse data["action"] to determine operation type
  - Return JSON-serializable responses
- `notifications.py` (optional): For notification-specific handlers
  - Use notifications_manager.create_notification() to send notifications 
- `settings.py` (optional): For settings-specific handlers
  - Handle getting and saving plugin settings

2️⃣ FRONTEND FILES

Create a folder in `/src/plugins/{plugin_name}/` containing:

- `components/`: Directory for main UI components
- `settings/` (optional): For plugin settings UI
  - SettingsPanel.jsx: Component that accepts currentSettings and onSettingsChange props
- `index.js`: Main entry point exporting your React component
- `register.js`: Plugin metadata including name, description, version

3️⃣ COMMUNICATION & INTEGRATION

- Events: Use event_bus.publish("event:name", { ... }) for cross-system messaging
- Database: Use existing database connections with proper async/await patterns
- Settings: Handle "get_settings" and "save_settings" actions in your logic.py
- Notifications: Call notifications_manager.create_notification() when needed

------------------------------------------------------
🚫 CORE SYSTEM — DO NOT DEPEND ON THESE FILES
------------------------------------------------------

Plugins should NEVER rely on the following core-managed files:

❌ BACKEND SYSTEM CORE
  - /backend/core/director.py
  - /backend/core/plugin_manager.py
  - /backend/core/event_bus.py
  - /backend/core/state_manager.py
  - /backend/main.py

❌ FRONTEND SYSTEM CORE
  - /src/App.jsx
  - /src/main.jsx

❌ PLUGIN ENGINE & THEME SYSTEM
  - /src/core/plugins/PluginProvider.jsx
  - /src/core/plugins/usePlugins.js
  - /src/core/plugins/DynamicUIComponent.jsx
  - /src/core/theme/ThemeProvider.jsx
  - /src/core/theme/useTheme.js

These files are maintained by the platform team and are **not accessible to you**. Build plugins that function without needing global changes.

#########################################################
🎨 ASSET AGENT BLUEPRINT — HOW TO MANAGE BRAND ASSETS
#########################################################

**Primary Scope**: `/public/assets`

**Key Files:**
- `/public/assets/logo.png`
- `/public/assets/favicon.ico`

**Duties:**

- Ensure assets exist and are properly formatted
- Generate placeholders if missing
- Optimize for web delivery
- Ensure paths match `theme_config.json`
- Generate assets using Dall-E:

---------------------------------------------------------
🔄 AUTOMATION: PNG ➜ ICO CONVERSION
---------------------------------------------------------

To programmatically convert a `.png` into a multi-resolution `.ico` favicon using Pillow:

```python
from PIL import Image

img = Image.open("logo.png").convert("RGBA")
sizes = [(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
img.save("favicon.ico", format="ICO", sizes=sizes)

------------------------------------------------------
✅ NOTES
------------------------------------------------------

✅ Make sure your PNG has transparency (RGBA) if you want a transparent background in the favicon.
✅ Browsers usually expect 16x16 or 32x32 for favicons, but including multiple sizes helps support high-resolution displays.
✅ You can also use .convert("RGBA") on the image before saving if you want to ensure it supports transparency.











# Plugin Settings Integration Guide

This guide explains how to integrate your Mozaiks plugin with the settings system.

## Overview

Mozaiks plugins can provide their own settings UI that will be displayed in the user's profile settings page. These settings are stored in the database and can be accessed by your plugin when it runs.

## Backend Setup

### 1. Create a `settings.py` file

In your plugin directory, create a `settings.py` file with the following functions:

```python
# /backend/plugins/your_plugin/settings.py
import logging
from typing import Dict, Any, Optional

# Get the logger
logger = logging.getLogger("mozaiks_core.plugins.your_plugin.settings")

async def get_default_settings() -> Dict[str, Any]:
    """Return default settings for this plugin."""
    return {
        "enabled": True,
        "notifications_enabled": True,
        # Add your plugin-specific settings here
    }

async def validate_settings(settings: Dict[str, Any]) -> Dict[str, str]:
    """Validate settings and return errors dictionary."""
    errors = {}
    # Your validation logic here
    return errors

async def get_settings(user_id: str, data: Optional[Dict[str, Any]] = None) -> Dict[str, Any]:
    """Get settings for this plugin."""
    settings_manager = data.get("settings_manager") if data else None
    
    if settings_manager:
        try:
            saved_settings = await settings_manager.get_plugin_settings(user_id, "your_plugin")
            if not saved_settings:
                return await get_default_settings()
            return saved_settings
        except Exception as e:
            logger.error(f"Error getting settings: {e}")
            return await get_default_settings()
    else:
        return await get_default_settings()

async def save_settings(user_id: str, data: Dict[str, Any]) -> Dict[str, Any]:
    """Save settings for this plugin."""
    settings_manager = data.get("settings_manager")
    new_settings = data.get("settings", {})
    
    # Validate settings
    validation_errors = await validate_settings(new_settings)
    if validation_errors:
        return {
            "success": False,
            "errors": validation_errors,
            "message": "Settings validation failed"
        }
    
    try:
        if settings_manager:
            await settings_manager.save_plugin_settings(user_id, "your_plugin", new_settings)
            return {"success": True, "message": "Settings saved successfully"}
        else:
            return {"success": False, "message": "Settings manager not available"}
    except Exception as e:
        logger.error(f"Error saving settings: {e}")
        return {"success": False, "message": f"Error saving settings: {str(e)}"}
```

### 2. Update your `logic.py` to handle settings requests

In your plugin's `logic.py` file, add settings handlers:

```python
# Inside your execute() function in logic.py
async def execute(data):
    # Get the action from the request
    action = data.get("action", "")
    user_id = data.get("user_id", "")
    
    # Import settings module
    from . import settings as plugin_settings
    
    # Handle settings requests
    if action == "get_settings":
        # Add settings_manager to data
        from core.settings_manager import settings_manager
        data["settings_manager"] = settings_manager
        return await plugin_settings.get_settings(user_id, data)
    
    elif action == "save_settings":
        # Add settings_manager to data
        from core.settings_manager import settings_manager
        data["settings_manager"] = settings_manager
        return await plugin_settings.save_settings(user_id, data)
    
    # Handle other plugin actions...
```

## Frontend Setup

### 1. Create a `SettingsPanel.jsx` component

Create a `settings` folder in your plugin directory and add a `SettingsPanel.jsx` file:

```jsx
// /src/plugins/your_plugin/settings/SettingsPanel.jsx
import React, { useState, useEffect } from 'react';

const SettingsPanel = ({ currentSettings, onSettingsChange }) => {
  // Initialize with default settings
  const [settings, setSettings] = useState({
    enabled: true,
    // Your plugin-specific settings here
  });
  
  // Initialize form with current settings when available
  useEffect(() => {
    if (currentSettings && Object.keys(currentSettings).length > 0) {
      setSettings(currentSettings);
    }
  }, [currentSettings]);
  
  // Handle input changes
  const handleChange = (field, value) => {
    setSettings(prev => ({ ...prev, [field]: value }));
  };
  
  // Handle checkbox toggle
  const handleToggle = (field) => {
    setSettings(prev => ({ ...prev, [field]: !prev[field] }));
  };
  
  // Handle form submission
  const handleSubmit = (e) => {
    e.preventDefault();
    onSettingsChange(settings);
  };
  
  return (
    <div className="p-4">
      <form onSubmit={handleSubmit}>
        {/* Your settings form fields here */}
        
        <div className="flex justify-end mt-4">
          <button
            type="submit"
            className="px-4 py-2 bg-accent text-white rounded hover:bg-opacity-90"
          >
            Save Settings
          </button>
        </div>
      </form>
    </div>
  );
};

export default SettingsPanel;
```

### 2. Export your SettingsPanel component

Make sure your SettingsPanel component is properly exported and can be imported by the DynamicUIComponent.

## Settings Configuration

To make your plugin's settings appear in the user profile page, you need to add a field to the settings configuration:

```json
{
  "id": "your_plugin",
  "label": "Your Plugin Name",
  "type": "plugin-settings",
  "plugin": "your_plugin",
  "editable": true
}
```

This field should be added to the appropriate section in the `settings_config.json` file. The `plugin` property should match your plugin's name in the registry.

## Accessing Settings in Your Plugin

When your plugin executes, you can access the user's settings:

```python
# In your plugin's logic.py
async def execute(data):
    user_id = data.get("user_id", "")
    
    # Get the user's settings
    from core.settings_manager import settings_manager
    from . import settings as plugin_settings
    
    # You can use your own settings module to get settings
    user_settings = await plugin_settings.get_settings(user_id, {"settings_manager": settings_manager})
    
    # Now use these settings in your plugin logic
    if user_settings.get("enabled", False):
        # Do something when enabled
        pass
    
    # Rest of your plugin logic...
```

## Dynamic Settings Visibility

The settings system automatically handles visibility based on subscription access. If monetization is enabled, plugin settings will only be visible to users who have access to your plugin based on their subscription plan.

## Best Practices

1. **Default Values**: Always provide sensible default values in `get_default_settings()`
2. **Validation**: Validate user input in both frontend and backend
3. **Error Handling**: Handle errors gracefully and provide meaningful error messages
4. **Responsive UI**: Make your settings UI responsive and user-friendly
5. **Testing**: Test your settings with different subscription tiers if using monetization

## Complete Example

See the template files for a complete working example of settings integration.



# Plugin Notifications Implementation Guide

This guide explains how to integrate your Mozaiks plugin with the notification system.

## Overview

Plugins can define their own notification types, create notifications for users, and respond to system events. The notification system is fully integrated with the core user interface, allowing plugin notifications to appear alongside system notifications.

## Steps to Implement Notifications

### 1. Create a `notifications.py` file

In your plugin directory, create a `notifications.py` file with the following functions:

```python
# /backend/plugins/your_plugin/notifications.py
import logging
from typing import Dict, List, Any, Optional

logger = logging.getLogger("mozaiks_core.plugins.your_plugin.notifications")

async def get_notification_types() -> List[Dict[str, Any]]:
    """
    Return notification types this plugin provides.
    These will be merged into the core notification system.
    """
    return [
        {
            "id": "your_notification_type",
            "category": "plugins",  # Use the standard category or your custom one
            "name": "User-friendly name",
            "description": "Description of when this notification is sent",
            "default": True,  # Whether enabled by default
            "channels": ["email", "in_app"],  # Notification channels
            "frequencies": ["immediate", "daily", "never"]  # Frequency options
        }
        # Add more notification types as needed
    ]

async def get_notification_category() -> Optional[Dict[str, Any]]:
    """
    Return a plugin-specific notification category if needed.
    If None is returned, notifications will use the standard "plugins" category.
    """
    # If your plugin has many notification types, you might want a dedicated category
    return {
        "id": "your_plugin",
        "name": "Your Plugin Name",
        "description": "Notifications from Your Plugin",
        "icon": "puzzle"  # Use any icon available in the system
    }
    
    # Or return None to use the default "plugins" category
    # return None

async def handle_notification_event(event_type: str, data: Dict[str, Any]) -> bool:
    """
    Handle notification events from the system.
    Return True if handled, False otherwise.
    
    This function is called for system events like:
    - plugin_executed: When your plugin is executed
    - subscription_updated: When a user's subscription changes
    - and other events the plugin might be interested in
    """
    logger.info(f"Received notification event: {event_type} with data: {data}")
    
    # Get the notifications manager from the data
    notifications_manager = data.get("notifications_manager")
    if not notifications_manager:
        return False
        
    # Handle specific events
    if event_type == "plugin_executed":
        user_id = data.get("user")
        
        # Determine if a notification should be sent
        # For example, maybe only for certain actions
        
        # Create notification if needed
        await notifications_manager.create_notification(
            user_id=user_id,
            notification_type="your_notification_type",
            title="Action Completed",
            message="Your plugin action was completed successfully.",
            metadata={"event_data": data}
        )
        return True
        
    return False
```

### 2. Update your `logic.py` file to create notifications

In your plugin's `logic.py` file, add code to create notifications when appropriate:

```python
# In your logic.py file
from core.notifications_manager import notifications_manager

async def execute(data):
    action = data.get("action")
    user_id = data.get("user_id")
    
    # Handle your plugin's actions
    if action == "important_action":
        result = await perform_important_action(data)
        
        # If successful, create a notification
        if result.get("success") and user_id:
            await notifications_manager.create_notification(
                user_id=user_id,
                notification_type="your_plugin_your_notification_type",
                title="Important Action Completed",
                message="The important action has been complete

notification_type="your_plugin_your_notification_type",
                title="Important Action Completed",
                message="The important action has been completed successfully.",
                metadata={
                    "action_id": result.get("id"),
                    "details": result.get("details", {})
                }
            )
        
        return result
        
    # Handle other actions...
    return {"error": "Unknown action"}

# Helper function to create notifications
async def create_plugin_notification(user_id, title, message, metadata=None):
    """Helper function to create notifications from your plugin"""
    try:
        from . import notifications as plugin_notifications
        
        # Get notification types
        notification_types = await plugin_notifications.get_notification_types()
        if not notification_types:
            return False
            
        # Use the first notification type or a specific one
        notification_type = notification_types[0]["id"]
        
        # Create the notification
        notification = await notifications_manager.create_notification(
            user_id=user_id,
            notification_type=notification_type,
            title=title,
            message=message,
            metadata=metadata or {}
        )
        
        return notification is not None
    except Exception as e:
        logger.error(f"Error creating notification: {e}")
        return False
```

## How the Notification System Works

### Notification Registration

1. When your plugin is loaded, the system calls `get_notification_types()` to register your notification types
2. These notification types appear in the user's notification preferences
3. If you define a custom category with `get_notification_category()`, it's also registered

### Notification Creation

Notifications can be created in three ways:

1. **Direct creation**: Call `notifications_manager.create_notification()` from your plugin code
2. **Event handling**: Implement `handle_notification_event()` to respond to system events
3. **Core system**: The core system may create notifications related to your plugin (e.g., for subscription changes)

### User Preferences

- Users can enable/disable your plugin's notifications in their profile settings
- They can choose notification channels (email, in-app) and frequencies
- These preferences are automatically respected by the notification syste