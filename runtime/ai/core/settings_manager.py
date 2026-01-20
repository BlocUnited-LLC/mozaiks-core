# backend/core/settings_manager.py
import json
import logging
from fastapi import HTTPException
from core.config.database import settings_collection
from core.config.config_loader import get_config_path
from core.event_bus import event_bus

logger = logging.getLogger("mozaiks_core.settings_manager")

class SettingsManager:
    def __init__(self):
        self.settings_config_path = get_config_path() / "settings_config.json"
        self.settings_config = self.load_settings_config()
    
    def load_settings_config(self):
        """Load settings configuration from file"""
        try:
            with open(self.settings_config_path, "r") as f:
                return json.load(f)
        except (FileNotFoundError, json.JSONDecodeError) as e:
            logger.error(f"Error loading settings configuration: {e}")
            return {"profile_sections": []}
    
    def refresh_settings_config(self):
        """Reload settings configuration from file"""
        self.settings_config = self.load_settings_config()
        return self.settings_config
    
    async def get_user_settings(self, user_id):
        """Get all settings for a user"""
        user_settings = await settings_collection.find_one({"user_id": user_id})
        if not user_settings:
            # Initialize empty settings if not found
            user_settings = {
                "user_id": user_id,
                "plugin_settings": {},
                "notification_preferences": {}
            }
        
        return user_settings
    
    async def get_plugin_settings(self, user_id, plugin_name):
        """Get settings for a specific plugin"""
        user_settings = await self.get_user_settings(user_id)
        
        # Get plugin settings or empty dict if not found
        plugin_settings = user_settings.get("plugin_settings", {}).get(plugin_name, {})
        
        logger.info(f"Retrieved settings for plugin {plugin_name} for user {user_id}")
        return plugin_settings
    
    async def save_plugin_settings(self, user_id, plugin_name, settings_data):
        """Save settings for a specific plugin"""
        # Validate plugin name
        if not plugin_name or not isinstance(plugin_name, str):
            logger.error(f"Invalid plugin name: {plugin_name}")
            raise HTTPException(status_code=400, detail="Invalid plugin name")
        
        # Get current settings
        user_settings = await self.get_user_settings(user_id)
        
        # Initialize plugin_settings if not exists
        if "plugin_settings" not in user_settings:
            user_settings["plugin_settings"] = {}
        
        # Update plugin settings
        user_settings["plugin_settings"][plugin_name] = settings_data
        
        # Save to database
        await settings_collection.update_one(
            {"user_id": user_id},
            {"$set": user_settings},
            upsert=True
        )
        
        # Publish event for settings update
        event_bus.publish("settings_updated", {
            "user_id": user_id,
            "plugin": plugin_name
        })
        
        logger.info(f"Saved settings for plugin {plugin_name} for user {user_id}")
        return {"success": True, "message": "Settings saved successfully"}
    
    async def get_notification_preferences(self, user_id):
        """Get notification preferences for a user"""
        user_settings = await self.get_user_settings(user_id)
        
        # Get notification preferences or empty dict if not found
        notification_prefs = user_settings.get("notification_preferences", {})
        
        # If no notification preferences exist, initialize with defaults
        if not notification_prefs:
            notification_prefs = self._get_default_notification_preferences()
            
        return notification_prefs
    
    def _get_default_notification_preferences(self):
        """Create default notification preferences based on settings config"""
        default_prefs = {}
        
        # Find notifications section
        notifications_section = next(
            (section for section in self.settings_config.get("profile_sections", []) 
            if section.get("id") == "notifications"), 
            None
        )
        
        if not notifications_section:
            return default_prefs
            
        # Get all notification fields (regular + plugin)
        notification_fields = notifications_section.get("fields", [])
        plugin_fields = notifications_section.get("plugin_notification_fields", [])
        
        # Combine all fields
        all_fields = notification_fields + plugin_fields
        
        # Create default preferences for each field
        for field in all_fields:
            field_id = field.get("id")
            if field_id and field.get("type") == "toggle":
                default_prefs[field_id] = {
                    "enabled": True,
                    "frequency": "daily"
                }
            
        return default_prefs
    
    async def save_notification_preferences(self, user_id, preferences):
        """Save notification preferences for a user"""
        try:
            # Load settings config to get valid notification fields
            settings_config = self.refresh_settings_config()
                
            # Find notifications section
            notifications_section = next(
                (section for section in settings_config.get("profile_sections", []) 
                if section.get("id") == "notifications"), 
                None
            )
            
            if not notifications_section:
                logger.error("No notifications section found in settings config")
                return False
                
            # Get all notification fields (regular + plugin)
            notification_fields = notifications_section.get("fields", [])
            plugin_fields = notifications_section.get("plugin_notification_fields", [])
            
            # Get valid field IDs
            valid_field_ids = [field.get("id") for field in notification_fields + plugin_fields 
                              if field.get("id") and field.get("type") == "toggle"]
            
            # Filter out invalid notification types
            valid_prefs = {}
            for pref_id, pref_data in preferences.items():
                if pref_id in valid_field_ids:
                    valid_prefs[pref_id] = {
                        "enabled": bool(pref_data.get("enabled", True)),
                        "frequency": pref_data.get("frequency", "daily")
                    }
            
            # Get current settings
            user_settings = await self.get_user_settings(user_id)
            
            # Update notification preferences
            user_settings["notification_preferences"] = valid_prefs
            
            # Save to database
            result = await settings_collection.update_one(
                {"user_id": user_id},
                {"$set": user_settings},
                upsert=True
            )
            
            # Publish event when notification preferences are updated
            event_bus.publish("notification_preferences_updated", {
                "user_id": user_id
            })
            
            if result.modified_count == 0 and not result.upserted_id:
                logger.warning(f"No changes made to notification preferences for user {user_id}")
                
            return valid_prefs
        except Exception as e:
            logger.error(f"Error updating notification preferences for user {user_id}: {e}")
            raise HTTPException(status_code=500, detail=f"Failed to update notification preferences: {str(e)}")
    
    async def update_settings_visibility(self, monetization_enabled, user_id):
        """
        Update settings visibility based on subscription status
        This is called when checking access to ensure only accessible plugin settings are shown
        """
        sections = self.settings_config.get("profile_sections", [])
        updated_sections = []
        
        for section in sections:
            # Handle notifications section specially to filter plugin notifications
            if section.get("id") == "notifications":
                section_copy = section.copy()
                
                # If monetization is enabled, filter plugin notification fields
                if monetization_enabled:
                    # Get available plugin notification fields
                    plugin_notification_fields = section.get("plugin_notification_fields", [])
                    filtered_fields = []
                    
                    # Include only notification fields for plugins the user has access to
                    for field in plugin_notification_fields:
                        plugin_name = field.get("plugin")
                        if plugin_name:
                            # Import needed here to avoid circular imports
                            from core.subscription_manager import subscription_manager
                            has_access = await subscription_manager.is_plugin_accessible(user_id, plugin_name)
                            if has_access:
                                filtered_fields.append(field)
                    
                    # Update the section with filtered plugin notification fields
                    section_copy["plugin_notification_fields"] = filtered_fields
                
                updated_sections.append(section_copy)
            else:
                # For other sections, just include them as is
                updated_sections.append(section)
        
        # Create updated config with filtered sections
        updated_config = {
            "profile_sections": updated_sections
        }
        
        return updated_config

# Initialize the settings manager
settings_manager = SettingsManager()
