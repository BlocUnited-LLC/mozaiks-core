# Capability Specs

This directory contains app-specific AI capability definitions.

## How Capabilities Work

MozaiksCore is an **app-agnostic runtime**. AI capabilities are NOT hardcoded
into the core - they are loaded dynamically from:

1. **This folder** (`capability_specs/*.json`) - App-specific capability definitions
2. **Plugins** - Plugins can register their own capabilities
3. **Workflows** - Auto-discovered from `MOZAIKS_WORKFLOWS_PATH`
4. **Platform API** - External capability registry

## File Format

Each `.json` file in this folder defines one capability:

```json
{
  "capability": {
    "id": "my-capability",
    "display_name": "My Capability",
    "description": "What this capability does",
    "icon": "icon-name",
    "workflow_id": "MyWorkflow",
    "enabled": true,
    "visibility": "user",
    "allowed_plans": ["*"]
  }
}
```

## For Mozaiks Platform

Mozaiks-platform should inject its capabilities (AgentGenerator, AppGenerator,
ValueEngine, etc.) via:

1. Setting `MOZAIKS_AI_CAPABILITY_SPECS_DIR` to point to platform-specific specs
2. Or copying capability specs to this folder during deployment
3. Or using the plugin system to register capabilities

## Template

See `_capability.template.json` for the full schema.
