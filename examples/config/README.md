# Example Configuration Files

These are example configuration files for mozaiks-core. Copy them to your config directory and customize as needed.

## Setup

1. Copy files to your config directory:
   ```bash
   mkdir -p /path/to/your/config
   cp subscription_config.json /path/to/your/config/
   ```

2. Set the config path:
   ```bash
   export MOZAIKS_CONFIGS_PATH="/path/to/your/config"
   ```

3. Start mozaiks

## Files

### subscription_config.json

Defines your subscription plans and which plugins each plan can access.

**Key concepts:**
- `subscription_plans` - Array of plan definitions
- `plugins_unlocked` - Which plugins this plan grants access to
  - List specific plugins: `["basic_chat", "file_manager"]`
  - Or allow all: `["*"]`
- `settings.trial_plan` - What plan new users get during trial
- `settings.trial_period_days` - How long trials last

**Example customization:**

```json
{
  "subscription_plans": [
    {
      "name": "free",
      "price": 0,
      "plugins_unlocked": ["basic_chat"]
    },
    {
      "name": "pro", 
      "price": 19.99,
      "plugins_unlocked": ["*"]
    }
  ],
  "settings": {
    "trial_plan": "pro",
    "trial_period_days": 7
  }
}
```

## Other Config Files

mozaiks-core also supports these config files (create as needed):

- `plugin_registry.json` - Plugin definitions
- `navigation_config.json` - UI navigation structure  
- `theme_config.json` - Theme customization
- `settings_config.json` - Application settings
- `ai_capabilities.json` - AI capability definitions

See the documentation for details on each file format.
