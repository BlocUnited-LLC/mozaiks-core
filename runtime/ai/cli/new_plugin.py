#!/usr/bin/env python
"""
Plugin Scaffolding CLI

Creates new plugins with proper structure following PLUGIN_CONTRACT.md.

Usage:
    python -m cli.main new plugin <name>
    python -m cli.main new plugin <name> --with-settings
    python -m cli.main new plugin <name> --with-entitlements

Contract Version: 1.0
"""

import os
import re
import json
from pathlib import Path
from datetime import datetime

# Templates directory
TEMPLATES_DIR = Path(__file__).parent / "templates"


def to_snake_case(name: str) -> str:
    """Convert name to snake_case."""
    # Handle camelCase
    s1 = re.sub('(.)([A-Z][a-z]+)', r'\1_\2', name)
    # Handle acronyms
    s2 = re.sub('([a-z0-9])([A-Z])', r'\1_\2', s1)
    # Handle spaces and hyphens
    s3 = s2.replace('-', '_').replace(' ', '_')
    return s3.lower()


def to_title_case(name: str) -> str:
    """Convert snake_case to Title Case."""
    return ' '.join(word.capitalize() for word in name.split('_'))


def get_plugins_dir() -> Path:
    """Get the canonical plugins directory (runtime/ai/plugins)."""
    return Path(__file__).parent.parent / "plugins"


def create_plugin(
    name: str,
    with_settings: bool = False,
    with_entitlements: bool = False,
    with_frontend: bool = False,
) -> int:
    """
    Create a new plugin with proper structure.

    Args:
        name: Plugin name (will be converted to snake_case)
        with_settings: Include settings.py
        with_entitlements: Include entitlements.yaml template
        with_frontend: Note about frontend (not implemented in core)

    Returns:
        0 on success, 1 on failure
    """
    plugin_name = to_snake_case(name)
    display_name = to_title_case(plugin_name)

    print(f"\n🔧 Creating plugin: {plugin_name}\n")

    # Get plugins directory
    plugins_dir = get_plugins_dir()
    plugin_dir = plugins_dir / plugin_name

    # Check if plugin already exists
    if plugin_dir.exists():
        print(f"❌ Plugin '{plugin_name}' already exists at {plugin_dir}")
        return 1

    # Create plugin directory
    print(f"   [1/4] Creating directory: {plugin_dir}")
    plugin_dir.mkdir(parents=True, exist_ok=True)

    # Create __init__.py
    print(f"   [2/4] Creating __init__.py")
    init_content = f'''# {display_name} Plugin
# Created with: python -m cli.main new plugin {name}
'''
    (plugin_dir / "__init__.py").write_text(init_content)

    # Create manifest.json
    print(f"   [3/4] Creating manifest.json")
    manifest = {
        "name": plugin_name,
        "version": "1.0.0",
        "display_name": display_name,
        "description": f"A {display_name} plugin",
        "entry_point": "logic.execute",
        "created_at": datetime.utcnow().isoformat(),
        "contract_version": "1.0"
    }
    if with_settings:
        manifest["settings_entry_point"] = "settings.execute"

    (plugin_dir / "manifest.json").write_text(
        json.dumps(manifest, indent=2)
    )

    # Create logic.py
    print(f"   [4/4] Creating logic.py")
    logic_content = generate_logic_template(plugin_name, display_name)
    (plugin_dir / "logic.py").write_text(logic_content)

    # Optional: Create settings.py
    if with_settings:
        print(f"   [+] Creating settings.py")
        settings_content = generate_settings_template(plugin_name, display_name)
        (plugin_dir / "settings.py").write_text(settings_content)

    # Optional: Create entitlements.yaml
    if with_entitlements:
        print(f"   [+] Creating entitlements.yaml")
        entitlements_content = generate_entitlements_template(plugin_name, display_name)
        (plugin_dir / "entitlements.yaml").write_text(entitlements_content)

    # Frontend note
    if with_frontend:
        print(f"\n   ⚠️  Frontend scaffolding is not provided by core.")
        print("      Create UI under runtime/packages/shell/src/plugins or your app frontend.")

    # Success message
    print(f"\n{'=' * 50}")
    print(f"✨ Plugin '{plugin_name}' created successfully!")
    print(f"{'=' * 50}")
    print(f"\nLocation: {plugin_dir}")
    print(f"\nFiles created:")
    print(f"  - __init__.py")
    print(f"  - manifest.json")
    print(f"  - logic.py")
    if with_settings:
        print(f"  - settings.py")
    if with_entitlements:
        print(f"  - entitlements.yaml")

    print(f"\nNext steps:")
    print(f"  1. Edit {plugin_dir / 'logic.py'} to add your plugin logic")
    print(f"  2. Restart the runtime to load your plugin")
    print(f"  3. Test with: POST /api/execute/{plugin_name}")

    if with_entitlements:
        print(f"\nEntitlements:")
        print(f"  Edit {plugin_dir / 'entitlements.yaml'} to configure tiers and limits")
        print(f"  See docs/contracts/ENTITLEMENT_CONTRACT_V1.md for schema")

    return 0


def generate_logic_template(plugin_name: str, display_name: str) -> str:
    """Generate the logic.py template."""
    return f'''"""
{display_name} Plugin
{'=' * len(display_name + ' Plugin')}

Created with: mozaiks new plugin {plugin_name}

Actions:
  - list: List all items for the current user
  - create: Create a new item
  - get: Get a single item by ID
  - update: Update an existing item
  - delete: Delete an item
  - get_settings: Get plugin settings (if enabled)
  - save_settings: Save plugin settings (if enabled)

Contract: PLUGIN_CONTRACT.md v1.0
"""
import logging
from datetime import datetime
from typing import Any, Dict

from bson import ObjectId
from core.config.database import db

logger = logging.getLogger("mozaiks_plugins.{plugin_name}")

# Collection for this plugin's data
COLLECTION_NAME = "{plugin_name}_items"


def get_collection():
    """Get the plugin's MongoDB collection."""
    return db[COLLECTION_NAME]


async def execute(data: dict) -> dict:
    """
    Main entry point for all plugin requests.

    Args:
        data: Request data containing:
            - action: str - The operation to perform
            - user_id: str - Current user (injected by runtime)
            - app_id: str - Current app (injected by runtime)
            - _context: dict - Full context (roles, etc.)
            - _entitlements: dict - User's entitlements (if configured)

    Returns:
        dict: Response data
    """
    action = data.get("action", "")
    user_id = data["user_id"]  # Always present, injected by runtime
    app_id = data["app_id"]    # Always present, injected by runtime

    logger.info(f"Action '{{action}}' for user={{user_id}} app={{app_id}}")

    try:
        if action == "list":
            return await handle_list(user_id, data)
        elif action == "create":
            return await handle_create(user_id, data)
        elif action == "get":
            return await handle_get(user_id, data)
        elif action == "update":
            return await handle_update(user_id, data)
        elif action == "delete":
            return await handle_delete(user_id, data)
        else:
            return {{"error": f"Unknown action: {{action}}"}}
    except Exception as e:
        logger.error(f"Error in action '{{action}}': {{e}}", exc_info=True)
        return {{"error": "An unexpected error occurred"}}


async def handle_list(user_id: str, data: dict) -> dict:
    """List all items for the current user."""
    collection = get_collection()

    # Optional pagination
    skip = data.get("skip", 0)
    limit = data.get("limit", 50)

    items = await collection.find(
        {{"user_id": user_id}}
    ).skip(skip).limit(limit).to_list(limit)

    # Convert ObjectId to string for JSON serialization
    for item in items:
        item["_id"] = str(item["_id"])

    total = await collection.count_documents({{"user_id": user_id}})

    return {{
        "items": items,
        "count": len(items),
        "total": total,
        "skip": skip,
        "limit": limit
    }}


async def handle_create(user_id: str, data: dict) -> dict:
    """Create a new item."""
    collection = get_collection()

    # Validate required fields
    title = data.get("title", "").strip()
    if not title:
        return {{"error": "Title is required", "error_code": "VALIDATION_ERROR"}}

    # Create document
    doc = {{
        "user_id": user_id,
        "title": title,
        "description": data.get("description", ""),
        "created_at": datetime.utcnow().isoformat(),
        "updated_at": datetime.utcnow().isoformat()
    }}

    result = await collection.insert_one(doc)
    item_id = str(result.inserted_id)

    logger.info(f"Created item {{item_id}} for user {{user_id}}")

    return {{"success": True, "id": item_id}}


async def handle_get(user_id: str, data: dict) -> dict:
    """Get a single item by ID."""
    collection = get_collection()

    item_id = data.get("item_id", "")
    if not item_id:
        return {{"error": "item_id is required"}}

    try:
        item = await collection.find_one({{
            "_id": ObjectId(item_id),
            "user_id": user_id  # Security: ensure user owns item
        }})
    except Exception:
        return {{"error": "Invalid item_id format"}}

    if not item:
        return {{"error": "Item not found", "error_code": "NOT_FOUND"}}

    item["_id"] = str(item["_id"])
    return {{"item": item}}


async def handle_update(user_id: str, data: dict) -> dict:
    """Update an existing item."""
    collection = get_collection()

    item_id = data.get("item_id", "")
    if not item_id:
        return {{"error": "item_id is required"}}

    # Build update document
    update_fields = {{}}
    if "title" in data:
        update_fields["title"] = data["title"].strip()
    if "description" in data:
        update_fields["description"] = data["description"]

    if not update_fields:
        return {{"error": "No fields to update"}}

    update_fields["updated_at"] = datetime.utcnow().isoformat()

    try:
        result = await collection.update_one(
            {{"_id": ObjectId(item_id), "user_id": user_id}},
            {{"$set": update_fields}}
        )
    except Exception:
        return {{"error": "Invalid item_id format"}}

    if result.matched_count == 0:
        return {{"error": "Item not found", "error_code": "NOT_FOUND"}}

    return {{"success": True, "modified": result.modified_count > 0}}


async def handle_delete(user_id: str, data: dict) -> dict:
    """Delete an item."""
    collection = get_collection()

    item_id = data.get("item_id", "")
    if not item_id:
        return {{"error": "item_id is required"}}

    try:
        result = await collection.delete_one({{
            "_id": ObjectId(item_id),
            "user_id": user_id
        }})
    except Exception:
        return {{"error": "Invalid item_id format"}}

    if result.deleted_count == 0:
        return {{"error": "Item not found", "error_code": "NOT_FOUND"}}

    return {{"success": True}}
'''


def generate_settings_template(plugin_name: str, display_name: str) -> str:
    """Generate the settings.py template."""
    return f'''"""
{display_name} Plugin Settings
{'=' * len(display_name + ' Plugin Settings')}

Handles user-specific settings for the {display_name} plugin.
"""
import logging
from core.settings_manager import settings_manager

logger = logging.getLogger("mozaiks_plugins.{plugin_name}.settings")

# Default settings for new users
DEFAULT_SETTINGS = {{
    "enabled": True,
    "notifications_enabled": True,
    "default_view": "list",
}}


async def execute(data: dict) -> dict:
    """
    Settings entry point.

    Actions:
      - get_settings: Get current settings
      - save_settings: Save new settings
    """
    action = data.get("action", "")
    user_id = data["user_id"]

    if action == "get_settings":
        return await handle_get_settings(user_id)
    elif action == "save_settings":
        return await handle_save_settings(user_id, data)
    else:
        return {{"error": f"Unknown settings action: {{action}}"}}


async def handle_get_settings(user_id: str) -> dict:
    """Get plugin settings for the user."""
    settings = await settings_manager.get_plugin_settings(user_id, "{plugin_name}")

    # Apply defaults for missing keys
    merged = {{**DEFAULT_SETTINGS, **(settings or {{}})}}

    return {{"settings": merged}}


async def handle_save_settings(user_id: str, data: dict) -> dict:
    """Save plugin settings for the user."""
    new_settings = data.get("settings", {{}})

    if not isinstance(new_settings, dict):
        return {{"error": "Invalid settings format"}}

    # Validate settings
    for key in new_settings:
        if key not in DEFAULT_SETTINGS:
            logger.warning(f"Unknown setting key: {{key}}")

    await settings_manager.save_plugin_settings(user_id, "{plugin_name}", new_settings)

    return {{"success": True, "message": "Settings saved"}}
'''


def generate_entitlements_template(plugin_name: str, display_name: str) -> str:
    """Generate the entitlements.yaml template."""
    return f'''# {display_name} Plugin Entitlements
# ===================================
#
# This file defines feature flags and usage limits for different subscription tiers.
# See docs/contracts/ENTITLEMENT_CONTRACT_V1.md for full schema.
#
# Contract Version: 1.0

schema_version: "1.0"
plugin: "{plugin_name}"

# ============================================================================
# FEATURES: Boolean capabilities (user has it or doesn't)
# ============================================================================
features:
  basic_access:
    description: "Basic access to {display_name} features"
    default: false

  advanced_features:
    description: "Advanced {display_name} capabilities"
    default: false

# ============================================================================
# LIMITS: Consumable or capped values
# ============================================================================
limits:
  items_per_month:
    description: "Items that can be created per month"
    type: consumable
    reset: monthly
    default: 0

  max_items:
    description: "Maximum total items allowed"
    type: cap
    default: 0

# ============================================================================
# TIERS: What each subscription level gets
# Supports inheritance via "inherits" keyword
# ============================================================================
tiers:
  free:
    features:
      basic_access: false
      advanced_features: false
    limits:
      items_per_month: 0
      max_items: 0

  basic:
    inherits: free
    features:
      basic_access: true
    limits:
      items_per_month: 10
      max_items: 100

  pro:
    inherits: basic
    features:
      advanced_features: true
    limits:
      items_per_month: 100
      max_items: 1000

# ============================================================================
# ACTIONS: Map plugin actions to entitlement checks (OPTIONAL)
# If specified, runtime auto-enforces BEFORE calling execute()
# ============================================================================
actions:
  create:
    requires_features:
      - basic_access
    consumes:
      items_per_month: 1

  # Add more action mappings as needed
  # update:
  #   requires_features:
  #     - basic_access

  # delete:
  #   requires_features:
  #     - basic_access
'''
