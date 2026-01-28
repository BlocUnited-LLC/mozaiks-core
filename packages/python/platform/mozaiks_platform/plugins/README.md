# MozaiksCore Plugins

This directory contains plugins that extend MozaiksCore functionality.

## Plugin Architecture

MozaiksCore is designed to be **app-agnostic**. All app-specific functionality
should be implemented via plugins, NOT hardcoded in the core.

## Plugin Structure

Each plugin should be a directory with this structure:

```
plugins/
└── my_plugin/
    ├── __init__.py           # Plugin initialization
    ├── logic.py              # Required: Main plugin logic
    ├── backend/              # Alternative: Backend code
    │   └── logic.py
    └── frontend/             # Optional: Frontend components
        └── register.js
```

## Creating a Plugin

1. Create a directory under `plugins/`
2. Add a `logic.py` file with your plugin logic
3. The plugin will be auto-discovered and registered

### Example Plugin (logic.py)

```python
# plugins/my_plugin/logic.py

from fastapi import APIRouter

router = APIRouter(prefix="/api/my-plugin", tags=["my-plugin"])

@router.get("/hello")
async def hello():
    return {"message": "Hello from my plugin!"}

# Plugin metadata (optional)
PLUGIN_CONFIG = {
    "name": "my_plugin",
    "display_name": "My Plugin",
    "description": "Does something useful",
    "version": "1.0.0"
}
```

## Plugin Capabilities

Plugins can:
- Register API routes
- Register AI capabilities
- Add notification handlers
- Extend the WebSocket system
- Access the MongoDB database
- Integrate with external services

## Configuration

Plugins can be configured via:
- Environment variables
- Plugin-specific config files
- The `plugin_registry.json` file

## For Mozaiks Platform

Mozaiks Platform uses the plugin system to add:
- Platform-specific AI workflows
- Revenue share calculations
- Hosting integrations
- etc.

This keeps MozaiksCore clean and reusable for the open-source community.
