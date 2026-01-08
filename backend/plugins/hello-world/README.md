# Hello World Plugin

A minimal example plugin demonstrating the MozaiksCore plugin architecture.

## What This Plugin Does

- Shows the basic structure of a MozaiksCore plugin
- Demonstrates plugin lifecycle methods
- Provides a simple greeting API

## Usage

```python
from mozaiks_core_runtime.plugins import load_plugin

# Load the plugin
hello_plugin = load_plugin("hello-world", config={
    "greeting": "Hello"
})

# Use it
message = hello_plugin.greet("World")  # "Hello, World!"
```

## Plugin Structure

```
hello-world/
├── __init__.py       # Plugin exports
├── logic.py          # Core plugin logic
└── README.md         # This file
```

## Creating Your Own Plugin

1. **Copy this folder** as a starting point
2. **Rename** it to your plugin name
3. **Implement** your plugin logic in `logic.py`
4. **Export** your plugin class in `__init__.py`
5. **Document** usage in README.md

## Plugin Interface

All plugins should implement these lifecycle methods:

- `on_init(app_context)` - Called when app starts
- `on_request(request_data)` - Called on each request (optional)
- `on_shutdown()` - Called when app stops

## Configuration

Plugins receive configuration via constructor:

```python
class MyPlugin:
    def __init__(self, config: Dict[str, Any] = None):
        self.config = config or {}
```

## Learn More

See the [Plugin Development Guide](../../docs/plugin-development.md) for more details.
