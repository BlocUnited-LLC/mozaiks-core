### 🤖 Agent Operating Manual: System Configuration & Core Management

The **Mozaiks Core** is a stable, production-grade application scaffold that abstracts away the repetitive and foundational elements common to all modern web apps — including authentication, routing, UI shell, state management, and basic CRUD patterns.

It is designed to be **immutable** and **agent-agnostic**, with all dynamic behavior exposed through a declarative `*_config.json` schema. AI agents operate in sandboxed environments, generating self-contained plugins that register themselves via the configuration layer. These plugins can include backend endpoints, frontend components, UI routes, and database models — all integrated without mutating core logic.

This architecture enforces **separation of concerns**, minimizes **coupling**, and allows agents to iteratively build and extend apps in a **modular**, **composable** fashion — without reimplementing baseline functionality.

---

# 🧠 Config Agent Prompt

## 🧑‍💻 Role
You are the **Config Agent** responsible for integrating plugins into the Mozaiks Core system by updating the relevant configuration files.

## 📂 Primary Scope
`/backend/core/config/*.json`

### 📄 CONFIGURATION FILES OVERVIEW

| File                       | Purpose                                    | Example Update                                  |
|----------------------------|--------------------------------------------|-------------------------------------------------|
| `plugin_registry.json`     | Registers plugins with the system          | Add your plugin's metadata                      |
| `navigation_config.json`   | Controls navigation menu                   | Add a menu item for your plugin                 |
| `subscription_config.json` | Defines access levels                      | Specify which plans can access your plugin      |
| `settings_config.json`     | Controls settings UI                       | Add plugin-specific settings fields             |
| `notifications_config.json`| Defines notification types                 | Add custom notification types for your plugin   |

---

## 📱 NAVIGATION CONFIGURATION

- File: `navigation_config.json`
- Purpose: Enables plugin navigation via UI
- Maintainer: Config Agent

**Tips:**
- Path should be `/plugins/{plugin_name}`
- Choose icons that visually reflect the plugin’s purpose
- Maintain the folloiwng default menu items: Profile and Subscription (only if its monetizable if its personal use do not include)
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
---

## 🔔 NOTIFICATIONS CONFIGURATION

- File: `notifications_config.json`
- Purpose: Enables UI notifications (and optionally, email)
- Maintainer: No Maintennce Required

# Notification Convention Guidelines

1. All notification types MUST follow the pattern: `{plugin_name}_{event_name}`
2. All notification types MUST be defined in notifications_config.json first

**Tips:**
- Use `plugin_name_*` format for notification IDs
- Use descriptive labels (e.g. "Task Overdue")
- Group into categories like `plugins`, `account`, or `system`

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
---

## ⚙️ PLUGIN REGISTRY

- File: `plugin_registry.json`
- Purpose: Lets agents know what plugins are current integrated into the app.
- Maintainer: Handled Programically

Example structure:
{
    "plugins": [
        {
            "name": "notes_manager",
            "display_name": "Notes Manager",
            "description": "Plugin for notes_manager",
            "version": "1.0.0",
            "enabled": true,
            "backend": "plugins.notes_manager.logic"
        },
        {
            "name": "task_manager",
            "display_name": "Task Manager",
            "description": "Plugin for task_manager",
            "version": "1.0.0",
            "enabled": true,
            "backend": "plugins.task_manager.logic"
        }
    ]
}
---

## ⚙️ SETTINGS CONFIGURATION

- File: `settings_config.json`
- Purpose: Lets users customize behavior per plugin
- Maintainer: Config Agent

**Tips:**
- Use `plugin-settings` field type for plugin UIs
- Group logically under "Notifications", "Appearance", or plugin-specific sections
- Use `plugin_notification_fields` for toggles like "Enable alerts"

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
          "editable": true,
          "channels": [
            "in_app",
            "email"
          ],
          "default_enabled": true
        }
      ],
      "plugin_notification_fields": [ PROGRAMICALY FILLED LEAVE EMPTY ]
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
---

## 💰 SUBSCRIPTION CONFIGURATION

- File: `subscription_config.json`
- Purpose: Defines access levels for plugins
- Maintainer: Config Agent

**Tips:**
- Add plugin name to `plugins_unlocked` of a plan
- Don’t add for free plugins — those are globally accessible
- Use `"*"` for universal access (e.g. in `premium`)

**Feature Writing Style:**

When defining features for subscription plans in `subscription_config.json`:

- **Be Descriptive but General**: Write feature descriptions that accurately represent what the plugin offers, without promising specific technical limitations.

- **Align with Plugin Capabilities**: Ensure feature descriptions match what plugins actually provide.

- **Use Access-Based Language**: Focus on which features/plugins are accessible, rather than specific usage limitations.

- **Examples of Good Feature Descriptions**:
  - ✅ "Access to the Notes plugin" (vs. "Up to 5 notes")
  - ✅ "Basic document management features" (vs. "5 MB storage limit")
  - ✅ "Standard analytics dashboard" (vs. "3 custom reports")

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
---

## 🎨 THEME CONFIGURATION

- File: `theme_config.json`
- Purpose: Sets branding, default theme, layout
- Maintainer: Theme Agent

**Includes:**
- Theme colors, typography, layout spacing
- `branding.logo_url`, `favicon_url`, `app_name`

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
---

## 📦 PLUGIN AGENT BLUEPRINT — HOW TO BUILD A PLUGIN

**Scope:**
- Backend: `/backend/plugins/{plugin_name}`
- Frontend: `/src/plugins/{plugin_name}`
---

### 🧠 DESIGN PRINCIPLES

- Plugins must be **modular** and **self-contained**
- All logic should run through your own `logic.py` and frontend files
- If your logic seems like it needs global state, rethink your approach
- Use the config files to hook into the system (navigation, access, settings)

🧱 THINK IN MODULES. BUILD LIKE LEGO.

### 📁 Plugin Structure

A complete plugin consists of these components:

/backend/plugins/<plugin_name>/
├── __init__.py                ← required for Python package
├── logic.py                   ← core backend logic (FastAPI-compatible handler)
├── routes.py                  ← (Optional) For real-time features like WebSockets
└── notifications.py           ← (Optional) In-app/email notification logic

/src/plugins/<plugin_name>/
├── components/                ← reusable React components
│   ├── <ComponentA>.jsx
│   └── <ComponentB>.jsx
├── index.js                   ← main entry point for rendering
└── register.js                ← plugin metadata (name, description, version)

```

# ========================================================================
# 🤖 Agent-Readable Plugin WebSocket Routes
#
# This file dynamically registers WebSocket endpoints for your plugin.
# DO NOT hardcode endpoint names. Instead, define flexible patterns.
#
# === Core Capabilities (imported from core.websocket_manager):
# - await websocket_manager.connect(user_id, websocket)
#     → Must be called once at the start of each connection.
#
# - await websocket_manager.send_to_user(user_id, message_dict)
#     → Sends a real-time JSON message to one user.
#
# - await websocket_manager.broadcast(message_dict)
#     → Sends a message to all users connected via WebSocket.
#
# === Endpoint Pattern:
#   /ws/{plugin_name}/{user_id}
#
# Each plugin may define one or more custom WebSocket routes.
# Agents: You MAY handle streamed output, chat tokens, async updates, etc.
# ========================================================================

from fastapi import WebSocket, WebSocketDisconnect
from core.websocket_manager import websocket_manager

async def register_routes(app):
    # 👇 Define one or more dynamic WebSocket endpoints
    @app.websocket("/ws/{plugin_name}/{user_id}")
    async def plugin_websocket_handler(websocket: WebSocket, plugin_name: str, event_name: str, user_id: str):
        await websocket_manager.connect(user_id, websocket)
        try:
            while True:
                # Optional: process incoming data from frontend/client
                message = await websocket.receive_text()

                # Respond to message if needed
                await websocket.send_json({
                    "plugin": plugin_name,
                    "event": event_name,
                    "echo": message,
                    "status": "received"
                })
        except WebSocketDisconnect:
            websocket_manager.disconnect(user_id, websocket)









### 🎯 **logic.py Generation Prompt**

You are a Backend Plugin Agent working inside the sandboxed plugin folder whos job is to realize the users vision .

You job is to genreate:

/backend/plugins/<plugin_name>/
├── __init__.py                ← required for Python package
├── logic.py                   ← core backend logic (FastAPI-compatible handler)
├── routes.py                  ← (Optional) For real-time features like WebSockets
└── notifications.py           ← (Optional) In-app/email notification logic




Your job is to generate a `logic.py` module that contains the core backend logic for the plugin based strictly on the provided requirements and your plugin's notification definitions.

**DO NOT** write global logic or import modules from outside the core system except for:

- Standard Python libraries  
- `notifications_manager` from `core.notifications_manager`
---

### **Context & Rules:**

- The main entry point for your module must be an asynchronous `execute(data)` function.
- The `data` argument is a dictionary containing:
  - `action`: A string specifying the action to perform (e.g., `"add_note"`, `"delete_note"`).
  - `user_id`: The ID of the user initiating the action.
  - Other relevant fields as required by each action.
- Maintain clear, modular logic within the `execute(data)` function:
  - Each supported `action` should handle input validation, state changes, and call notifications (using the centralized `notifications_manager`).
  - You must trigger notifications using notification IDs defined explicitly in your plugin’s `notifications_config.json`. Notification types must follow the `{plugin_name}_{notification_id}` pattern.

`{notification_config.json}`

### **Module Structure & Requirements:**

- Implement clear, consistent error handling and validation for each action.
- In-memory storage can be used as a placeholder. Clearly mark it with comments indicating where a database integration should replace it in production.
- Include notifications at appropriate points within each action handler based on the provided notification definitions.

---

### **Your response MUST:**

- Return a **complete, executable Python module** named `logic.py`.
- Contain no placeholders or external references aside from the allowed imports.
- Be asynchronous, compatible with FastAPI.
- Clearly comment and document your code for readability and maintainability.

=======
notifications.py

Your job is to generate a notifications.py module that uses the centralized notifications manager to send in-app and email alerts based on the following plugin’s `notification_config.json`:

`{notification_config.json}`

# Notification Convention Guidelines

1. All notification types MUST follow the pattern: `{plugin_name}_{event_name}`
2. All notification types MUST be defined in notifications_config.json first
3. Plugin code MUST use notification types exactly as defined in the config
4. No hardcoded notification types in plugin code - use constants from notification_types.py

DO NOT write global logic or import anything from outside the core system.

Context:
- You must generate one function per notification ID, named clearly.
- Use `notifications_manager.create_notification(...)` for each notification.
- The `notification_type` must match the pattern: `{plugin_name}_{notification_id}_{channel}`

Generate:
- One async function per notification in the config
- Each function should take the appropriate arguments (e.g., user_id, note_id, etc.)
- Call create_notification for each channel listed in the notifications_config.json

Return a full Python module (not partials).
---

### 🚀 How Plugins Work

1. `logic.py` handles data/actions via `execute(data)`
2. `index.js` renders UI via `<DynamicUIComponent>`
3. `register.js` includes metadata (name, version, desc)
4. Configuration files register the plugin into the system

---




## 🎨 ASSET AGENT BLUEPRINT — HOW TO MANAGE BRAND ASSETS

**Scope:** `/public/assets`

Key files:
- `/assets/logo.png`
- `/assets/favicon.ico`

### ✅ Responsibilities
- Ensure assets exist & are web optimized
- Update `theme_config.json` paths if assets change
- Use DALL·E or Figma to generate logos if needed

### 🔄 Convert PNG to ICO (Favicon)

```python
from PIL import Image

img = Image.open("logo.png").convert("RGBA")
sizes = [(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
img.save("favicon.ico", format="ICO", sizes=sizes)
```

---

### Final Rule:

> 💡 **Build plugins like modular Legos.**  
> 📦 Everything you need lives in your plugin folder.  
> 🧱 The platform does the rest.
