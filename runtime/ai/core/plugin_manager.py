# /backend/core/plugin_manager.py
import os
import json
import importlib
import importlib.util
import sys
import logging
import inspect
import asyncio
import time
from functools import lru_cache

from .config.config_loader import (
    get_plugin_registry,
    get_settings_config,
    get_notifications_config,
    get_config_path,
    reload_configs,
)
from core.utils.log_sanitizer import sanitize_for_log

logger = logging.getLogger("mozaiks_core.plugin_manager")


# ============================================================================
# PLUGIN PATH CONFIGURATION
# ============================================================================
# MOZAIKS_PLUGINS_PATH: Path to backend plugins directory (optional in dev).
# MOZAIKS_FRONTEND_PLUGINS_PATH: Path to frontend plugins directory (optional in dev).
# ============================================================================
PLUGIN_DIR_ENV = (os.getenv("MOZAIKS_PLUGINS_PATH") or "").strip()
FRONTEND_PLUGIN_DIR_ENV = (os.getenv("MOZAIKS_FRONTEND_PLUGINS_PATH") or "").strip()

# Fallback to local plugins directory for standalone/dev mode
if PLUGIN_DIR_ENV:
    PLUGIN_DIR = os.path.abspath(PLUGIN_DIR_ENV)
    logger.info(f"Using plugins path: {PLUGIN_DIR}")
else:
    PLUGIN_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "plugins"))
    logger.warning(f"MOZAIKS_PLUGINS_PATH not set, using local plugins: {PLUGIN_DIR}")

if FRONTEND_PLUGIN_DIR_ENV:
    FRONTEND_PLUGIN_DIR = os.path.abspath(FRONTEND_PLUGIN_DIR_ENV)
    logger.info(f"Using frontend plugins path: {FRONTEND_PLUGIN_DIR}")
else:
    # Frontend plugins are optional in standalone mode
    FRONTEND_PLUGIN_DIR = ""
    logger.warning("MOZAIKS_FRONTEND_PLUGINS_PATH not set, frontend plugins disabled")

# Add the plugins directory (and its parent) to sys.path for dynamic imports
PLUGIN_IMPORT_ROOT = os.path.abspath(os.path.join(PLUGIN_DIR, "..")) if PLUGIN_DIR else ""
for path in (PLUGIN_DIR, PLUGIN_IMPORT_ROOT):
    if path not in sys.path:
        sys.path.append(path)

class PluginManager:
    def __init__(self):
        self.plugins = {}
        self._registry_cache = None
        self._registry_last_refresh = 0
        self._registry_refresh_interval = 300  # 5 minutes
        self._refresh_lock = asyncio.Lock()  # Async lock for concurrent refresh operations
        # Initialize registry but don't load plugins yet (will be done in async init)
        self.update_registry()
    
    async def init_async(self):
        """Asynchronous initialization that should be called after instantiation"""
        await self.load_plugins()
        return self

    @lru_cache(maxsize=128)
    def get_plugin_metadata(self, plugin_name):
        """
        Get plugin metadata from registry. Cached for performance.
        """
        if not self._registry_cache:
            self._registry_cache = get_plugin_registry()
        
        for plugin in self._registry_cache.get("plugins", []):
            if plugin.get("name") == plugin_name:
                return plugin
        return None

    def update_registry(self):
        """
        Scans the plugins directory and dynamically updates the registry
        based on the available plugins. Writes to MOZAIKS_CONFIGS_PATH/plugin_registry.json
        """
        plugin_registry_path = get_config_path() / "plugin_registry.json"
        
        try:
            # Get all subdirectories in the plugins folder, excluding ones you don't consider plugins
            plugin_dirs = [
                d for d in os.listdir(PLUGIN_DIR)
                if os.path.isdir(os.path.join(PLUGIN_DIR, d))
                and not d.startswith('_')
                and d.lower() not in ["registry"]  # Exclude 'registry' folder if present
            ]
            
            registry = {"plugins": []}
            
            for plugin_name in plugin_dirs:
                # Check for both possible locations of the logic file
                backend_logic_path = os.path.join(PLUGIN_DIR, plugin_name, "backend", "logic.py")
                direct_logic_path = os.path.join(PLUGIN_DIR, plugin_name, "logic.py")
                
                # Determine the appropriate backend import path
                if os.path.exists(backend_logic_path):
                    backend_path = f"plugins.{plugin_name}.backend.logic"
                    logger.debug(f"Found logic.py in backend folder for {plugin_name}")
                elif os.path.exists(direct_logic_path):
                    backend_path = f"plugins.{plugin_name}.logic"
                    logger.debug(f"Found logic.py directly in plugin folder for {plugin_name}")
                else:
                    logger.warning(f"Skipping {plugin_name}: Missing logic.py in either location")
                    continue
                
                # Create metadata for the plugin
                metadata = {
                    "name": plugin_name,
                    "display_name": plugin_name.title().replace('_', ' '),
                    "description": f"Plugin for {plugin_name}",
                    "version": "1.0.0",
                    "enabled": True,  # Set plugin enabled by default
                    "backend": backend_path
                }
                
                # Try to extract more detailed metadata from frontend register.js if it exists
                frontend_dir = os.path.join(FRONTEND_PLUGIN_DIR, plugin_name)
                register_file = os.path.join(frontend_dir, "register.js")
                
                if os.path.exists(register_file):
                    try:
                        with open(register_file, 'r') as f:
                            content = f.read()
                            # Extract displayName if available
                            display_name_match = content.find('displayName:')
                            if display_name_match != -1:
                                line = content[display_name_match:].split('\n')[0]
                                display_name = line.split(':')[1].strip().strip(',').strip("'").strip('"')
                                metadata["display_name"] = display_name
                            
                            # Extract description if available
                            desc_match = content.find('description:')
                            if desc_match != -1:
                                line = content[desc_match:].split('\n')[0]
                                desc = line.split(':')[1].strip().strip(',').strip("'").strip('"')
                                metadata["description"] = desc
                            
                            # Extract version if available
                            version_match = content.find('version:')
                            if version_match != -1:
                                line = content[version_match:].split('\n')[0]
                                version = line.split(':')[1].strip().strip(',').strip("'").strip('"')
                                metadata["version"] = version
                    except Exception as e:
                        logger.warning(f"Couldn't extract metadata from register.js for {plugin_name}: {e}")
                
                registry["plugins"].append(metadata)
            
            # Save the updated registry
            with open(plugin_registry_path, "w") as f:
                json.dump(registry, f, indent=4)
            
            # Update cache and clear config loader cache
            self._registry_cache = registry
            self._registry_last_refresh = time.time()
            reload_configs()  # Clear cached configs
            
            logger.info(f"✅ Updated plugin registry with {len(registry['plugins'])} plugins")
            return registry
            
        except Exception as e:
            logger.error(f"❌ Error updating plugin registry: {e}")
            # Try to load from config loader
            registry = get_plugin_registry()
            if registry:
                self._registry_cache = registry
                return registry
            else:
                logger.info("Using empty registry")
                empty_registry = {"plugins": []}
                self._registry_cache = empty_registry
                return empty_registry

    async def register_plugin_notifications(self, plugin_name):
        """
        Register notification settings for a plugin by updating settings_config.json
        based on the notifications defined in notifications_config.json.
        
        Note: This modifies the settings_config.json in MOZAIKS_CONFIGS_PATH.
        """
        try:
            # Load configs via config_loader
            settings_config = get_settings_config()
            notifications_config = get_notifications_config()
            
            if not settings_config:
                logger.warning("Settings config not loaded, skipping notification registration")
                return False
            
            # Find the notifications section in settings config
            notifications_section = next(
                (section for section in settings_config.get("profile_sections", []) 
                if section.get("id") == "notifications"), 
                None
            )
            
            if not notifications_section:
                logger.warning(f"No notifications section found in settings config, skipping registration for {plugin_name}")
                return False
            
            # Ensure plugin_notification_fields exists
            if "plugin_notification_fields" not in notifications_section:
                notifications_section["plugin_notification_fields"] = []
            
            # Get existing plugin notification fields
            plugin_notification_fields = notifications_section["plugin_notification_fields"]
            
            # Get plugin notifications from config
            plugin_config = notifications_config.get("plugins", {}).get(plugin_name, {})
            plugin_notifications = plugin_config.get("notifications", [])
            
            if not plugin_notifications:
                # If no specific notifications defined, create a generic one
                plugin_display_name = plugin_name.replace('_', ' ').title()
                
                # Try to get better name from plugin config
                if plugin_name in self.plugins:
                    plugin_module_config = self.plugins[plugin_name].get("config", {})
                    plugin_display_name = plugin_module_config.get("display_name", plugin_display_name)
                elif plugin_config.get("display_name"):
                    plugin_display_name = plugin_config.get("display_name")
                
                # Add generic notification toggle
                generic_notification_id = f"{plugin_name}_notifications"
                
                # Check if generic notification already exists
                existing_field = next(
                    (field for field in plugin_notification_fields 
                    if field.get("id") == generic_notification_id),
                    None
                )
                
                if not existing_field:
                    # Create generic notification field
                    new_notification_field = {
                        "id": generic_notification_id,
                        "plugin": plugin_name,
                        "label": f"{plugin_display_name} Notifications",
                        "type": "toggle",
                        "category": "plugins",
                        "description": f"Receive notifications from {plugin_display_name}",
                        "required": False,
                        "editable": True
                    }
                    plugin_notification_fields.append(new_notification_field)
                    logger.info(f"✅ Registered generic notification for plugin: {plugin_name}")
            else:
                # Register each specific notification type
                for notification in plugin_notifications:
                    notification_id = notification.get("id")
                    
                    # Skip if notification doesn't have an ID
                    if not notification_id:
                        continue
                    
                    # Check if this notification already exists
                    existing_field = next(
                        (field for field in plugin_notification_fields 
                        if field.get("id") == notification_id),
                        None
                    )
                    
                    # Skip if already exists
                    if existing_field:
                        continue
                    
                    # Create notification field
                    new_notification_field = {
                        "id": notification_id,
                        "plugin": plugin_name,
                        "label": notification.get("label", notification_id),
                        "type": "toggle",
                        "category": notification.get("category", "plugins"),
                        "description": notification.get("description", ""),
                        "required": False,
                        "editable": True,
                        "channels": notification.get("channels", ["in_app"]),
                        "default_enabled": notification.get("default_enabled", True)
                    }
                    
                    plugin_notification_fields.append(new_notification_field)
                    logger.info(f"✅ Registered notification '{notification_id}' for plugin: {plugin_name}")
            
            # Save updated config to external config path
            settings_config_path = get_config_path() / "settings_config.json"
            with open(settings_config_path, "w") as f:
                json.dump(settings_config, f, indent=2)
            reload_configs()  # Clear cached configs
            
            return True
            
        except Exception as e:
            logger.error(f"❌ Error registering notification settings for plugin {plugin_name}: {e}")
            return False

    async def load_plugins(self):
        """
        Loads plugins based on the registry.
        """
        # Use cached registry if available, otherwise load from config
        if self._registry_cache:
            registry = self._registry_cache
        else:
            registry = get_plugin_registry()
            self._registry_cache = registry
            logger.info(f"Loading plugins from registry: {registry}")
        
        for plugin in registry.get("plugins", []):
            if not plugin.get("enabled", False):
                logger.info(f"Skipping disabled plugin: {plugin.get('name')}")
                continue

            plugin_name = plugin["name"]
            backend_path = plugin.get("backend")

            if not backend_path:
                logger.error(f"Skipping {plugin_name}: No backend specified")
                continue
            
            # Skip if plugin is already loaded
            if plugin_name in self.plugins:
                logger.debug(f"Plugin {plugin_name} already loaded, skipping")
                continue
            
            try:
                logger.info(f"Attempting to import {backend_path} for plugin {plugin_name}")
                module = importlib.import_module(backend_path)
                self.plugins[plugin_name] = {
                    "module": module,
                    "config": plugin,
                    "load_time": time.time()
                }
                
                # Register notification settings for this plugin
                await self.register_plugin_notifications(plugin_name)
                
                logger.info(f"✅ Loaded plugin: {plugin_name}")
            except Exception as e:
                logger.error(f"⚠️ Error loading plugin {plugin_name}: {e}")
                # Print traceback for more detailed error information
                import traceback
                logger.error(traceback.format_exc())

    async def execute_plugin(self, plugin_name, data):
        """
        Execute a plugin with the given data.
        Access control is now handled by the director.py before calling this method.
        
        Contract v1.0.0: Enforces MOZAIKS_PLUGIN_TIMEOUT_SECONDS (default 30s).
        """
        # Get timeout from settings
        try:
            from core.config.settings import settings
            timeout_seconds = settings.plugin_exec_timeout_s
        except Exception:
            timeout_seconds = 30.0  # Contract v1.0.0 default
        
        # Sanitize plugin name for safe logging (prevent log injection)
        safe_plugin_name = sanitize_for_log(plugin_name)
        
        if plugin_name in self.plugins:
            try:
                module = self.plugins[plugin_name]["module"]
                
                # Check if execute function exists
                if hasattr(module, "execute"):
                    execute_func = module.execute
                    
                    # Check if it's an async function
                    if inspect.iscoroutinefunction(execute_func):
                        # If async, await it with timeout enforcement
                        return await asyncio.wait_for(execute_func(data), timeout=timeout_seconds)
                    else:
                        # If not async, just call it (no timeout for sync)
                        return execute_func(data)
                        
                # Fall back to run method if execute doesn't exist
                elif hasattr(module, "run"):
                    run_func = module.run
                    
                    # Check if it's an async function
                    if inspect.iscoroutinefunction(run_func):
                        # If async, await it with timeout enforcement
                        return await asyncio.wait_for(run_func(data), timeout=timeout_seconds)
                    else:
                        # If not async, just call it (no timeout for sync)
                        return run_func(data)
                        
                else:
                    return {"error": f"Plugin {plugin_name} has no execute() or run() method"}
            
            except asyncio.TimeoutError:
                logger.error(f"Plugin {safe_plugin_name} execution timed out after {timeout_seconds}s")
                return {"error": f"Plugin execution timed out after {timeout_seconds} seconds"}
                    
            except Exception as e:
                logger.error(f"Error executing plugin {safe_plugin_name}: {str(e)}")
                # Include stack trace for better debugging
                import traceback
                logger.error(traceback.format_exc())
                return {"error": f"Error executing plugin: {str(e)}"}
        else:
            logger.error(f"Plugin {safe_plugin_name} not found in loaded plugins. Available plugins: {list(self.plugins.keys())}")
            return {"error": f"Plugin not found"}

    async def refresh_plugins(self):
        """
        Updates the registry and reloads all plugins.
        Useful for runtime updates without restarting the server.
        Uses an async lock to prevent concurrent refreshes.
        """
        # Use a lock to prevent multiple simultaneous refreshes
        async with self._refresh_lock:
            # Check if refresh is needed based on time interval
            current_time = time.time()
            if (current_time - self._registry_last_refresh) < self._registry_refresh_interval:
                logger.info(f"Skipping plugin refresh, last refresh was {int(current_time - self._registry_last_refresh)} seconds ago")
                return {"message": "Refresh skipped, plugins were refreshed recently"}
            
            logger.info("Refreshing plugins...")
            # Keep track of previously loaded plugins for comparison
            previous_plugins = set(self.plugins.keys())
            
            # Update registry first
            self.update_registry()
            
            # Load new plugins (already loaded ones will be skipped)
            await self.load_plugins()
            
            # Calculate which plugins were added
            current_plugins = set(self.plugins.keys())
            new_plugins = current_plugins - previous_plugins
            
            logger.info(f"Refreshed plugins. Loaded plugins: {list(self.plugins.keys())}")
            logger.info(f"Newly added plugins: {list(new_plugins)}")
            
            return {
                "message": f"Refreshed plugins", 
                "total_plugins": len(self.plugins),
                "new_plugins": list(new_plugins)
            }

    async def check_plugin_exists(self, plugin_name):
        """Check if a plugin exists in the registry regardless of whether it's loaded"""
        # Reload registry if needed
        if not self._registry_cache or (time.time() - self._registry_last_refresh > self._registry_refresh_interval):
            self.update_registry()
        
        for plugin in self._registry_cache.get("plugins", []):
            if plugin.get("name") == plugin_name and plugin.get("enabled", True):
                return True
        
        return False

    async def ensure_plugin_loaded(self, plugin_name):
        """
        Ensure a specific plugin is loaded without refreshing all plugins
        """
        # If plugin is already loaded, return immediately
        if plugin_name in self.plugins:
            return True
            
        # Check if plugin exists in registry
        plugin_exists = await self.check_plugin_exists(plugin_name)
        if not plugin_exists:
            logger.warning(f"Plugin {plugin_name} not found in registry")
            return False
            
        # Get plugin config from registry
        plugin_config = None
        for plugin in self._registry_cache.get("plugins", []):
            if plugin.get("name") == plugin_name:
                plugin_config = plugin
                break
                
        if not plugin_config:
            logger.warning(f"Plugin {plugin_name} config not found")
            return False
            
        # Load just this specific plugin
        backend_path = plugin_config.get("backend")
        if not backend_path:
            logger.error(f"Plugin {plugin_name} has no backend path")
            return False
            
        try:
            module = importlib.import_module(backend_path)
            self.plugins[plugin_name] = {
                "module": module,
                "config": plugin_config,
                "load_time": time.time()
            }
            
            # Register notification settings for this plugin
            await self.register_plugin_notifications(plugin_name)
            
            logger.info(f"✅ Loaded plugin: {plugin_name}")
            return True
        except Exception as e:
            logger.error(f"⚠️ Error loading plugin {plugin_name}: {e}")
            import traceback
            logger.error(traceback.format_exc())
            return False

# Create plugin manager instance but don't initialize it yet
plugin_manager = PluginManager()

# This function will be imported by main.py or another initialization script
async def register_websockets(app):
    """Register plugin-based WebSockets"""
    # Register plugin-based WebSockets only
    for plugin_name, plugin_data in plugin_manager.plugins.items():
        try:
            plugin_routes_path = f"plugins.{plugin_name}.routes"
            spec = importlib.util.find_spec(plugin_routes_path)
            if spec is None:
                continue

            module = importlib.import_module(plugin_routes_path)
            if hasattr(module, "register_routes"):
                await module.register_routes(app)
                logger.info(f"✅ Registered WebSocket routes for plugin: {plugin_name}")
        except Exception as e:
            logger.warning(f"⚠️ Failed to load WebSocket routes for plugin '{plugin_name}': {e}")
