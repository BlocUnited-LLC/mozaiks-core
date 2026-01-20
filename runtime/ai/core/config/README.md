# Core Config Directory

This directory contains **core infrastructure** files only:

- `database.py` - MongoDB connection and database utilities
- `settings.py` - Environment settings and configuration loader
- `config_loader.py` - Central loader for app-specific JSON configs

## App-Specific Configs

All app-specific JSON configuration files are loaded from `MOZAIKS_CONFIGS_PATH`.

| Config File | Purpose |
|-------------|---------|
| `plugin_registry.json` | Registered plugins |
| `navigation_config.json` | Navigation menu structure |
| `subscription_config.json` | Subscription plans and features |
| `theme_config.json` | Branding and theme settings |
| `settings_config.json` | User settings UI schema |
| `notifications_config.json` | Notification types |
| `notification_templates.json` | Notification message templates |
| `ai_capabilities.json` | AI capability definitions |
| `capability_specs/` | AI capability spec files |

## Required Environment Variables

```bash
MOZAIKS_CONFIGS_PATH=/path/to/mozaiks-app/backend/core/config
```

See `config_loader.py` for the config loading API.
