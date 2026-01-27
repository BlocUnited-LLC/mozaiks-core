# core/entitlements/loader.py
"""
Entitlements YAML Loader

Loads and validates plugin entitlements.yaml files with support for:
- Schema validation
- Tier inheritance (via 'inherits' keyword)
- Circular inheritance detection
- Graceful degradation on invalid YAML

Contract Version: 1.0
"""
import os
import logging
from pathlib import Path
from typing import Optional, Dict, Any, Set
from functools import lru_cache

logger = logging.getLogger("mozaiks_core.entitlements.loader")

# Try to import yaml, with fallback
try:
    import yaml
    YAML_AVAILABLE = True
except ImportError:
    YAML_AVAILABLE = False
    logger.warning("PyYAML not installed. Entitlements YAML loading disabled.")

# Plugin directory - will be set by plugin_manager
_PLUGIN_DIR: Optional[Path] = None

# Cache for loaded entitlements configs
_entitlements_cache: Dict[str, Optional[Dict[str, Any]]] = {}


def set_plugin_directory(plugin_dir: str | Path) -> None:
    """Set the plugin directory for entitlements loading."""
    global _PLUGIN_DIR
    _PLUGIN_DIR = Path(plugin_dir)
    logger.info(f"Entitlements loader plugin directory set to: {_PLUGIN_DIR}")


def get_plugin_directory() -> Path:
    """Get the plugin directory, with fallback to environment variable."""
    global _PLUGIN_DIR
    if _PLUGIN_DIR is None:
        # Try to get from environment or use default
        env_dir = os.getenv("MOZAIKS_PLUGIN_DIR")
        if env_dir:
            _PLUGIN_DIR = Path(env_dir)
        else:
            # Default relative path from core
            _PLUGIN_DIR = Path(__file__).parent.parent.parent / "plugins"
    return _PLUGIN_DIR


def has_entitlements_yaml(plugin_name: str) -> bool:
    """
    Check if a plugin has an entitlements.yaml file.

    Args:
        plugin_name: Name of the plugin

    Returns:
        True if entitlements.yaml exists, False otherwise
    """
    if not YAML_AVAILABLE:
        return False

    plugin_dir = get_plugin_directory()
    yaml_path = plugin_dir / plugin_name / "entitlements.yaml"
    return yaml_path.exists()


def load_plugin_entitlements(plugin_name: str, force_reload: bool = False) -> Optional[Dict[str, Any]]:
    """
    Load entitlements.yaml for a plugin.

    Args:
        plugin_name: Name of the plugin
        force_reload: If True, bypass cache and reload from disk

    Returns:
        Parsed and validated entitlements config, or None if:
        - YAML file doesn't exist
        - YAML is invalid
        - Schema validation fails
    """
    global _entitlements_cache

    if not YAML_AVAILABLE:
        logger.debug(f"YAML not available, skipping entitlements for {plugin_name}")
        return None

    # Check cache
    if not force_reload and plugin_name in _entitlements_cache:
        return _entitlements_cache[plugin_name]

    plugin_dir = get_plugin_directory()
    yaml_path = plugin_dir / plugin_name / "entitlements.yaml"

    if not yaml_path.exists():
        logger.debug(f"No entitlements.yaml for plugin {plugin_name}")
        _entitlements_cache[plugin_name] = None
        return None

    try:
        with open(yaml_path, "r", encoding="utf-8") as f:
            config = yaml.safe_load(f)

        if not config:
            logger.warning(f"Empty entitlements.yaml for {plugin_name}")
            _entitlements_cache[plugin_name] = None
            return None

        # Validate schema
        validation_errors = validate_entitlements_schema(config)
        if validation_errors:
            logger.warning(f"Invalid entitlements.yaml for {plugin_name}:")
            for error in validation_errors:
                logger.warning(f"  - {error}")
            logger.warning(f"Entitlement enforcement disabled for {plugin_name}")
            _entitlements_cache[plugin_name] = None
            return None

        # Validate no circular inheritance
        if not validate_tier_inheritance(config):
            logger.warning(f"Circular tier inheritance detected in {plugin_name}")
            logger.warning(f"Entitlement enforcement disabled for {plugin_name}")
            _entitlements_cache[plugin_name] = None
            return None

        logger.info(f"Loaded entitlements.yaml for {plugin_name}")
        _entitlements_cache[plugin_name] = config
        return config

    except yaml.YAMLError as e:
        logger.warning(f"YAML parse error in {yaml_path}: {e}")
        logger.warning(f"Entitlement enforcement disabled for {plugin_name}")
        _entitlements_cache[plugin_name] = None
        return None
    except Exception as e:
        logger.error(f"Error loading entitlements.yaml for {plugin_name}: {e}")
        _entitlements_cache[plugin_name] = None
        return None


def validate_entitlements_schema(config: Dict[str, Any]) -> list[str]:
    """
    Validate entitlements config against the schema.

    Args:
        config: Parsed YAML config

    Returns:
        List of validation error messages (empty if valid)
    """
    errors = []

    # Required fields
    if "schema_version" not in config:
        errors.append("Missing required field: schema_version")
    elif config["schema_version"] not in ["1.0"]:
        errors.append(f"Unsupported schema_version: {config['schema_version']}")

    if "plugin" not in config:
        errors.append("Missing required field: plugin")

    # Features validation
    if "features" in config:
        features = config["features"]
        if not isinstance(features, dict):
            errors.append("'features' must be a dict")
        else:
            for key, value in features.items():
                if not isinstance(value, dict):
                    errors.append(f"Feature '{key}' must be a dict")
                elif "default" in value and not isinstance(value["default"], bool):
                    errors.append(f"Feature '{key}.default' must be boolean")

    # Limits validation
    if "limits" in config:
        limits = config["limits"]
        if not isinstance(limits, dict):
            errors.append("'limits' must be a dict")
        else:
            valid_types = ["consumable", "cap"]
            valid_resets = ["monthly", "daily", "weekly", "never"]

            for key, value in limits.items():
                if not isinstance(value, dict):
                    errors.append(f"Limit '{key}' must be a dict")
                    continue

                if "type" in value and value["type"] not in valid_types:
                    errors.append(f"Limit '{key}.type' must be one of {valid_types}")

                if "reset" in value and value["reset"] not in valid_resets:
                    errors.append(f"Limit '{key}.reset' must be one of {valid_resets}")

                if "default" in value and not isinstance(value["default"], (int, float)):
                    errors.append(f"Limit '{key}.default' must be a number")

    # Tiers validation
    if "tiers" in config:
        tiers = config["tiers"]
        if not isinstance(tiers, dict):
            errors.append("'tiers' must be a dict")
        else:
            for tier_name, tier_config in tiers.items():
                if not isinstance(tier_config, dict):
                    errors.append(f"Tier '{tier_name}' must be a dict")
                    continue

                # Check inherits references valid tier
                if "inherits" in tier_config:
                    parent = tier_config["inherits"]
                    if parent not in tiers:
                        errors.append(f"Tier '{tier_name}' inherits from unknown tier '{parent}'")

    # Actions validation
    if "actions" in config:
        actions = config["actions"]
        if not isinstance(actions, dict):
            errors.append("'actions' must be a dict")
        else:
            features = config.get("features", {})
            limits = config.get("limits", {})

            for action_name, action_config in actions.items():
                if not isinstance(action_config, dict):
                    errors.append(f"Action '{action_name}' must be a dict")
                    continue

                # Check requires_features reference valid features
                for feature in action_config.get("requires_features", []):
                    if feature not in features:
                        errors.append(f"Action '{action_name}' requires unknown feature '{feature}'")

                # Check consumes reference valid limits
                for limit_key in action_config.get("consumes", {}).keys():
                    if limit_key not in limits:
                        errors.append(f"Action '{action_name}' consumes unknown limit '{limit_key}'")

    return errors


def validate_tier_inheritance(config: Dict[str, Any]) -> bool:
    """
    Validate that tier inheritance has no circular references.

    Args:
        config: Entitlements config

    Returns:
        True if inheritance is valid, False if circular
    """
    tiers = config.get("tiers", {})

    def has_cycle(tier_name: str, visited: Set[str]) -> bool:
        if tier_name in visited:
            return True
        if tier_name not in tiers:
            return False

        visited.add(tier_name)
        tier_config = tiers[tier_name]
        parent = tier_config.get("inherits")

        if parent:
            return has_cycle(parent, visited)
        return False

    for tier_name in tiers:
        if has_cycle(tier_name, set()):
            return False

    return True


def get_tier_config(config: Dict[str, Any], tier_name: str) -> Dict[str, Any]:
    """
    Get tier configuration with inheritance resolved.

    Args:
        config: Full entitlements config
        tier_name: Name of the tier to resolve

    Returns:
        Resolved tier config with inherited values merged
    """
    tiers = config.get("tiers", {})

    if tier_name not in tiers:
        # Unknown tier - return defaults from feature/limit definitions
        return _build_default_tier(config)

    return _resolve_tier(tiers, tier_name, set())


def _resolve_tier(tiers: Dict[str, Any], tier_name: str, visited: Set[str]) -> Dict[str, Any]:
    """
    Recursively resolve tier inheritance.

    Args:
        tiers: All tiers from config
        tier_name: Tier to resolve
        visited: Set of already visited tiers (for cycle detection)

    Returns:
        Resolved tier config
    """
    if tier_name in visited or tier_name not in tiers:
        return {}

    visited.add(tier_name)
    tier_config = tiers[tier_name].copy()
    parent_name = tier_config.pop("inherits", None)

    if parent_name:
        parent_config = _resolve_tier(tiers, parent_name, visited)
        # Deep merge: parent first, then tier overrides
        return _deep_merge(parent_config, tier_config)

    return tier_config


def _deep_merge(base: Dict[str, Any], override: Dict[str, Any]) -> Dict[str, Any]:
    """
    Deep merge two dicts, with override taking precedence.

    Args:
        base: Base dict
        override: Override dict

    Returns:
        Merged dict
    """
    result = base.copy()

    for key, value in override.items():
        if key in result and isinstance(result[key], dict) and isinstance(value, dict):
            result[key] = _deep_merge(result[key], value)
        else:
            result[key] = value

    return result


def _build_default_tier(config: Dict[str, Any]) -> Dict[str, Any]:
    """
    Build a default tier from feature/limit defaults.

    Used when user's tier is not found in config.

    Args:
        config: Full entitlements config

    Returns:
        Tier config with all defaults
    """
    features = {}
    for name, definition in config.get("features", {}).items():
        features[name] = definition.get("default", True)  # Default True for OSS

    limits = {}
    for name, definition in config.get("limits", {}).items():
        limits[name] = definition.get("default", 0)

    return {
        "features": features,
        "limits": limits
    }


def clear_cache(plugin_name: str = None) -> None:
    """
    Clear entitlements cache.

    Args:
        plugin_name: Specific plugin to clear, or None for all
    """
    global _entitlements_cache
    if plugin_name:
        _entitlements_cache.pop(plugin_name, None)
    else:
        _entitlements_cache.clear()
