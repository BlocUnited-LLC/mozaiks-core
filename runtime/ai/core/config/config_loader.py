# backend/core/config/config_loader.py
"""
Central configuration loader for app-specific JSON configs.

All app-specific configuration files are loaded from MOZAIKS_CONFIGS_PATH.
This enables mozaiks-core to be an app-agnostic shell while mozaiks-app
provides all application-specific configurations.

Required Environment Variables:
    MOZAIKS_CONFIGS_PATH: Path to the config directory containing:
        - plugin_registry.json
        - navigation_config.json
        - subscription_config.json
        - theme_config.json
        - settings_config.json
        - notifications_config.json
        - notification_templates.json
        - ai_capabilities.json
        - capability_specs/ (folder)
"""

import os
import json
import logging
from pathlib import Path
from functools import lru_cache
from typing import Dict, Any, Optional

logger = logging.getLogger("mozaiks_core.config_loader")

# ============================================================================
# CONFIG PATH CONFIGURATION
# ============================================================================
CONFIG_PATH_ENV = (os.getenv("MOZAIKS_CONFIGS_PATH") or "").strip()
if CONFIG_PATH_ENV:
    CONFIG_PATH = Path(CONFIG_PATH_ENV).resolve()
    logger.info(f"Using config path: {CONFIG_PATH}")
else:
    # Fallback to local config directory for standalone/dev mode
    CONFIG_PATH = Path(__file__).parent.resolve()
    logger.warning(f"MOZAIKS_CONFIGS_PATH not set, using local config: {CONFIG_PATH}")


def _load_json(filename: str) -> Dict[str, Any]:
    """Load a JSON config file from the config directory."""
    filepath = CONFIG_PATH / filename
    if not filepath.exists():
        logger.warning(f"Config file not found: {filepath}")
        return {}
    try:
        with open(filepath, "r", encoding="utf-8") as f:
            return json.load(f)
    except json.JSONDecodeError as e:
        logger.error(f"Invalid JSON in {filepath}: {e}")
        return {}
    except Exception as e:
        logger.error(f"Error loading {filepath}: {e}")
        return {}


@lru_cache(maxsize=1)
def get_plugin_registry() -> Dict[str, Any]:
    """Load plugin_registry.json"""
    return _load_json("plugin_registry.json")


@lru_cache(maxsize=1)
def get_navigation_config() -> Dict[str, Any]:
    """Load navigation_config.json"""
    return _load_json("navigation_config.json")


@lru_cache(maxsize=1)
def get_subscription_config() -> Dict[str, Any]:
    """Load subscription_config.json"""
    return _load_json("subscription_config.json")


@lru_cache(maxsize=1)
def get_theme_config() -> Dict[str, Any]:
    """Load theme_config.json"""
    return _load_json("theme_config.json")


@lru_cache(maxsize=1)
def get_settings_config() -> Dict[str, Any]:
    """Load settings_config.json"""
    return _load_json("settings_config.json")


@lru_cache(maxsize=1)
def get_notifications_config() -> Dict[str, Any]:
    """Load notifications_config.json"""
    return _load_json("notifications_config.json")


@lru_cache(maxsize=1)
def get_notification_templates() -> Dict[str, Any]:
    """Load notification_templates.json"""
    return _load_json("notification_templates.json")


@lru_cache(maxsize=1)
def get_ai_capabilities() -> Dict[str, Any]:
    """Load ai_capabilities.json"""
    return _load_json("ai_capabilities.json")


def get_capability_spec(capability_id: str) -> Optional[Dict[str, Any]]:
    """Load a specific capability spec from capability_specs/ folder."""
    spec_path = CONFIG_PATH / "capability_specs" / f"{capability_id}.json"
    if not spec_path.exists():
        return None
    try:
        with open(spec_path, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception as e:
        logger.error(f"Error loading capability spec {capability_id}: {e}")
        return None


def get_config_path() -> Path:
    """Return the config path for direct file access."""
    return CONFIG_PATH


def reload_configs():
    """Clear cached configs to force reload on next access."""
    get_plugin_registry.cache_clear()
    get_navigation_config.cache_clear()
    get_subscription_config.cache_clear()
    get_theme_config.cache_clear()
    get_settings_config.cache_clear()
    get_notifications_config.cache_clear()
    get_notification_templates.cache_clear()
    get_ai_capabilities.cache_clear()
    logger.info("Config caches cleared")
