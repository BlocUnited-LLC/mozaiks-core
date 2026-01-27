# backend/core/director.py
"""
MozaiksCore Director - Per-App Runtime Container

BOUNDARY CONTRACT:
- This is a PER-APP runtime (one deployment = one app)
- All requests MUST be scoped by app_id (from env) + user_id (from JWT)
- Platform founders/admins NEVER authenticate here
- Subscriptions are READ-ONLY (enforced, not managed)
- Business logic lives in plugins ONLY
"""
from fastapi import FastAPI, Request, HTTPException, Depends, Response, status
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
import os
import json
import time
import logging
import asyncio
import traceback
from datetime import datetime, timedelta
from typing import Dict, Any, Optional, List, Union

from .plugin_manager import PLUGIN_DIR, plugin_manager
from core.subscription_manager import subscription_manager
from core.subscription_stub import SubscriptionStub
from .event_bus import event_bus
from .state_manager import state_manager
from security.auth import router as auth_router
from security.authentication import get_current_user

from core.config.database import users_collection, db_cache, get_cached_document
from core.config.config_loader import get_config_path
from core.routes.notifications import router as notifications_router
from core.routes.ai import router as ai_router
from core.settings_manager import settings_manager

# Admin and internal routes (require X-Internal-API-Key or superadmin JWT)
from core.routes.admin_users import router as admin_users_router
from core.routes.notifications_admin import router as notifications_admin_router
from core.routes.analytics import router as analytics_router
from core.routes.status import router as status_router
from core.routes.app_metadata import router as app_metadata_router
from core.routes.push_subscriptions import router as push_subscriptions_router
from core.routes.events import router as events_router
from core.routes.subscription_sync import router as subscription_sync_router
from core.routes.billing import router as billing_router

logger = logging.getLogger("mozaiks_core")
logging.basicConfig(level=logging.INFO)

# ============================================================================
# APP IDENTITY (Per-App Runtime)
# ============================================================================
# MOZAIKS_APP_ID is the unique identifier for this app instance.
# It MUST be set in production and is injected into all requests.
APP_ID = os.getenv("MOZAIKS_APP_ID", "").strip()
if not APP_ID and os.getenv("ENV", "development").lower() == "production":
    logger.critical("❌ MOZAIKS_APP_ID is required in production")
    raise RuntimeError("MOZAIKS_APP_ID must be set in production")
elif not APP_ID:
    APP_ID = "dev_app"
    logger.warning(f"⚠️ MOZAIKS_APP_ID not set, using default: {APP_ID}")

logger.info(f"🏷️ App ID: {APP_ID}")

# Create FastAPI app
app = FastAPI(title=f"MozaiksCore Runtime ({APP_ID})")

# Configure CORS based on environment
frontend_url = os.getenv("FRONTEND_URL", "http://localhost:5173")
cors_origins = [frontend_url]
if os.getenv("ADDITIONAL_CORS_ORIGINS"):
    additional_origins = os.getenv("ADDITIONAL_CORS_ORIGINS").split(",")
    cors_origins.extend([origin.strip() for origin in additional_origins])

# Add CORS middleware to allow requests from the frontend
app.add_middleware(
    CORSMiddleware,
    allow_origins=cors_origins,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Register API Routers
app.include_router(auth_router, prefix="/api/auth")
app.include_router(notifications_router, prefix="/api/notifications")
app.include_router(ai_router)

# Admin and internal routes (require X-Internal-API-Key or superadmin JWT)
# These are Control Plane integration points for MozaiksAI
app.include_router(admin_users_router)  # Has prefix /__mozaiks/admin/users
app.include_router(notifications_admin_router)  # Has prefix /__mozaiks/admin/notifications
app.include_router(analytics_router, prefix="/__mozaiks/admin/analytics", tags=["admin-analytics"])
app.include_router(status_router, prefix="/__mozaiks/admin/status", tags=["admin-status"])
app.include_router(app_metadata_router, prefix="/__mozaiks/admin/app", tags=["admin-app-metadata"])
app.include_router(push_subscriptions_router)  # Has prefix /api/push
app.include_router(events_router, prefix="/api/events", tags=["events"])
app.include_router(subscription_sync_router)
app.include_router(billing_router)  # Platform <-> Core billing integration (/api/v1/entitlements)

# Determine if monetization is enabled
MONETIZATION = os.getenv("MONETIZATION", "0") == "1"

# Set up the appropriate subscription manager
if MONETIZATION:
    try:
        # Use the core subscription_manager instead of the plugin version
        from core.subscription_manager import subscription_manager
        logger.info("✅ Monetization enabled: Using SubscriptionManager")
    except ImportError:
        # If the module isn't available, use the stub
        subscription_manager = SubscriptionStub()
        logger.warning("⚠️ Monetization enabled but subscription module not found. Using fallback.")
else:
    # For non-monetized apps, always use the stub
    subscription_manager = SubscriptionStub()
    logger.info("🆓 Monetization disabled: Using SubscriptionStub with unlimited access")

# Track if a plugin refresh is already in progress to avoid multiple simultaneous refreshes
_plugin_refresh_in_progress = False
_last_config_refresh = {}  # Cache timestamps for config reloads

# Cache for config files
config_cache = {}
CONFIG_CACHE_TTL = 300  # 5 minutes in seconds


def get_app_id() -> str:
    """Return the app_id for this runtime instance."""
    return APP_ID


async def inject_request_context(
    user: dict,
    data: dict,
    plugin_name: str,
    user_jwt: Optional[str] = None
) -> dict:
    """
    Inject app_id, user_id, entitlements and optional user_jwt into request data.

    SECURITY: These values are server-derived and cannot be overridden by client.

    Contract v1.0.0: Includes user_jwt for plugin-to-service calls.
    Contract v1.1.0: Includes _entitlements for feature/limit checks.
    """
    data["app_id"] = APP_ID
    data["user_id"] = user["user_id"]
    data["_context"] = {
        "app_id": APP_ID,
        "user_id": user["user_id"],
        "username": user.get("username"),
        "roles": user.get("roles", []),
        "is_superadmin": user.get("is_superadmin", False),
    }

    # Contract v1.1.0: Inject entitlements for plugin feature/limit checks
    # Empty dict = self-hosted mode, plugins use defaults (no restrictions)
    data["_entitlements"] = await _build_entitlements_context(user, plugin_name)

    # Contract v1.0.0: Inject bearer token for plugin-to-service authentication
    if user_jwt:
        data["user_jwt"] = user_jwt
    return data


async def _build_entitlements_context(user: dict, plugin_name: str = None) -> dict:
    """
    Build entitlements context from user's subscription state and plugin config.

    Contract v1.1.0: Loads plugin-level entitlements.yaml and populates context.

    Returns empty dict in self-hosted mode (no restrictions).
    Returns populated dict in platform mode (feature/limit gates).

    Context structure:
    {
        "tier": str,              # User's tier for this plugin
        "features": {             # Feature flags
            "feature_key": bool,
            ...
        },
        "limits": {               # Consumable/cap limits with usage
            "limit_key": {
                "allowed": int,
                "used": int,
                "remaining": int
            },
            ...
        },
        "enforce": bool           # Whether to enforce (platform mode)
    }
    """
    if not MONETIZATION:
        # Self-hosted: No entitlements = no restrictions
        return {}

    if not plugin_name:
        # No plugin specified - return basic context
        return {
            "enforce": MONETIZATION,
            "features": {},
            "limits": {},
        }

    try:
        # Import entitlements module (lazy to avoid circular imports)
        from core.entitlements.loader import load_plugin_entitlements, has_entitlements_yaml
        from core.entitlements.usage import get_all_usage
        from core.entitlements import build_entitlements_context

        # Check if plugin has entitlements.yaml
        if not has_entitlements_yaml(plugin_name):
            # No entitlements.yaml = features default true, limits unlimited
            return {
                "enforce": MONETIZATION,
                "features": {},
                "limits": {},
            }

        # Load plugin entitlements config
        entitlements_config = load_plugin_entitlements(plugin_name)
        if not entitlements_config:
            # Invalid YAML or load error - enforcement disabled
            return {
                "enforce": False,
                "features": {},
                "limits": {},
            }

        # Get user's tier for this plugin
        user_tier = await subscription_manager.get_user_plugin_tier(
            user["user_id"],
            plugin_name
        )

        # Get current usage data
        usage_data = await get_all_usage(user["user_id"], plugin_name)

        # Build full entitlements context
        context = build_entitlements_context(
            user_tier=user_tier,
            plugin_name=plugin_name,
            entitlements_config=entitlements_config,
            usage_data=usage_data
        )

        # Add enforcement flag
        context["enforce"] = MONETIZATION

        return context

    except ImportError as e:
        logger.warning(f"Entitlements module not available: {e}")
        return {
            "enforce": MONETIZATION,
            "features": {},
            "limits": {},
        }
    except Exception as e:
        logger.error(f"Error building entitlements context for {plugin_name}: {e}")
        return {
            "enforce": MONETIZATION,
            "features": {},
            "limits": {},
        }


async def _auto_enforce_entitlements(
    user: dict,
    plugin_name: str,
    action: str,
    data: dict
) -> Optional[Dict[str, Any]]:
    """
    Auto-enforce entitlements before plugin execution.

    Contract v1.1.0: If plugin's entitlements.yaml has an 'actions' section,
    automatically check required features and limits.

    Args:
        user: User dict
        plugin_name: Plugin name
        action: Action being performed
        data: Request data (with _entitlements injected)

    Returns:
        None if allowed, or error dict if blocked
    """
    if not MONETIZATION:
        return None

    try:
        from core.entitlements.loader import load_plugin_entitlements
        from core.entitlements import check_action
        from core.entitlements.events import emit_feature_blocked_event, emit_limit_reached_event

        entitlements_config = load_plugin_entitlements(plugin_name)
        if not entitlements_config:
            return None

        # Check if actions section exists and defines this action
        actions_config = entitlements_config.get("actions", {})
        if action not in actions_config:
            return None  # No auto-enforcement for this action

        # Check if dry-run mode
        dry_run = data.get("_entitlement_dry_run", False)

        # Perform the check
        entitlements = data.get("_entitlements", {})
        result = await check_action(
            user_id=user["user_id"],
            plugin=plugin_name,
            action=action,
            entitlements=entitlements,
            entitlements_config=entitlements_config
        )

        if not result["allowed"]:
            # Emit appropriate event
            if result["blocking_type"] == "feature":
                await emit_feature_blocked_event(
                    user_id=user["user_id"],
                    plugin=plugin_name,
                    feature_key=result.get("blocking_reason", "unknown"),
                    tier=entitlements.get("tier", "unknown")
                )
                return {
                    "error": result["blocking_reason"],
                    "error_code": "FEATURE_GATED",
                    "tier": entitlements.get("tier"),
                }
            elif result["blocking_type"] == "limit":
                # Extract limit details for event
                await emit_limit_reached_event(
                    user_id=user["user_id"],
                    plugin=plugin_name,
                    limit_key="unknown",  # Extracted from blocking_reason if needed
                    attempted=1,
                    limit=0,
                    period=""
                )
                return {
                    "error": result["blocking_reason"],
                    "error_code": "LIMIT_EXCEEDED",
                }

        # If dry-run, return the check result without proceeding
        if dry_run:
            return {
                "dry_run": True,
                "allowed": True,
                "would_consume": result.get("would_consume", {})
            }

        return None  # Allowed

    except ImportError:
        return None
    except Exception as e:
        logger.error(f"Error in auto-enforcement for {plugin_name}/{action}: {e}")
        return None  # Fail open


async def _auto_consume_entitlements(
    user: dict,
    plugin_name: str,
    action: str,
    result: Any
) -> None:
    """
    Auto-consume entitlements after successful plugin execution.

    Contract v1.1.0: If action defines 'consumes', decrement limits.

    Args:
        user: User dict
        plugin_name: Plugin name
        action: Action that was performed
        result: Plugin execution result
    """
    if not MONETIZATION:
        return

    # Don't consume if result indicates error
    if isinstance(result, dict) and result.get("error"):
        return

    try:
        from core.entitlements.loader import load_plugin_entitlements
        from core.entitlements.usage import consume_limit

        entitlements_config = load_plugin_entitlements(plugin_name)
        if not entitlements_config:
            return

        actions_config = entitlements_config.get("actions", {})
        action_config = actions_config.get(action)
        if not action_config:
            return

        consumes = action_config.get("consumes", {})
        limits_config = entitlements_config.get("limits", {})

        for limit_key, amount in consumes.items():
            limit_def = limits_config.get(limit_key, {})
            period_type = limit_def.get("reset", "monthly")

            # Get user's limit value for this tier
            user_tier = await subscription_manager.get_user_plugin_tier(
                user["user_id"],
                plugin_name
            )
            from core.entitlements.loader import get_tier_config
            tier_config = get_tier_config(entitlements_config, user_tier)
            tier_limits = tier_config.get("limits", {})
            limit_value = tier_limits.get(limit_key, 0)

            await consume_limit(
                user_id=user["user_id"],
                plugin=plugin_name,
                limit_key=limit_key,
                amount=amount,
                period_type=period_type,
                limit_value=limit_value
            )

    except ImportError:
        pass
    except Exception as e:
        logger.error(f"Error in auto-consume for {plugin_name}/{action}: {e}")


async def ensure_plugins_up_to_date():
    """
    Checks if plugins need refreshing based on time since last refresh.
    This ensures new plugins are detected automatically without manual intervention.
    Uses a longer refresh interval (5 minutes) to reduce overhead.
    """
    global _plugin_refresh_in_progress
    
    # If a refresh is already in progress, skip
    if _plugin_refresh_in_progress:
        return
    
    last_refresh = state_manager.get("last_plugin_refresh_time")
    current_time = time.time()
    
    # Only refresh if it's been more than 5 minutes (300 seconds) since last refresh
    if not last_refresh or (current_time - last_refresh > 300):
        # Update timestamp first to prevent multiple refreshes
        state_manager.set("last_plugin_refresh_time", current_time)
        
        # Create a background task for the refresh to not block current request
        asyncio.create_task(async_refresh_plugins())

async def async_refresh_plugins():
    """
    Refreshes plugins asynchronously in the background
    """
    global _plugin_refresh_in_progress
    
    if _plugin_refresh_in_progress:
        return
        
    try:
        _plugin_refresh_in_progress = True
        await plugin_manager.refresh_plugins()
        logger.info("Completed background plugin refresh")
    except Exception as e:
        logger.error(f"Error in background plugin refresh: {e}")
    finally:
        _plugin_refresh_in_progress = False

def load_config(filename):
    """
    Loads a JSON config file via the central config loader.
    
    Args:
        filename (str): The config filename to load (e.g., "theme_config.json")
        
    Returns:
        dict: The config data
        
    Raises:
        HTTPException: If the config file cannot be loaded
    """
    from .config.config_loader import (
        get_plugin_registry,
        get_navigation_config,
        get_subscription_config,
        get_theme_config,
        get_settings_config,
        get_notifications_config,
        get_notification_templates,
        get_ai_capabilities,
    )
    
    # Map filenames to config loader functions
    config_loaders = {
        "plugin_registry.json": get_plugin_registry,
        "navigation_config.json": get_navigation_config,
        "subscription_config.json": get_subscription_config,
        "theme_config.json": get_theme_config,
        "settings_config.json": get_settings_config,
        "notifications_config.json": get_notifications_config,
        "notification_templates.json": get_notification_templates,
        "ai_capabilities.json": get_ai_capabilities,
    }
    
    loader = config_loaders.get(filename)
    if not loader:
        logger.error(f"Unknown config file: {filename}")
        raise HTTPException(status_code=404, detail=f"Configuration file {filename} not found.")
    
    try:
        config = loader()
        if not config:
            logger.warning(f"Config {filename} returned empty, using empty dict")
            return {}
        return config
    except Exception as e:
        logger.error(f"Error loading {filename}: {e}")
        raise HTTPException(status_code=500, detail=f"Error loading {filename}: {str(e)}")

@app.get("/api/app-config")
async def get_app_config():
    """
    API to provide application configuration including monetization status.
    Used by the frontend to determine which features to enable.
    """
    try:
        theme_config = load_config("theme_config.json")
        branding = theme_config.get("branding", {})
        
        return {
            "monetization_enabled": MONETIZATION,
            "app_name": branding.get("app_name", "Mozaiks"),
            "app_version": "1.0.0",
            "env": os.getenv("ENV", "development")
        }
    except Exception as e:
        logger.error(f"Error loading app configuration: {e}")
        return {
            "monetization_enabled": MONETIZATION,
            "app_name": "Mozaiks",
            "app_version": "1.0.0",
            "env": os.getenv("ENV", "development")
        }

@app.get("/api/navigation")
async def get_navigation(user: dict = Depends(get_current_user)):
    """
    Get navigation items based on user permissions and installed plugins
    """
    # Use a cache key specific to this user
    cache_key = f"navigation:{user['user_id']}"
    cached_nav = state_manager.get(cache_key)
    
    # Return cached navigation if available (except during development)
    if cached_nav and os.getenv("ENV") != "development":
        return {"navigation": cached_nav}
    
    # Auto-detect new plugins - essential for navigation
    await ensure_plugins_up_to_date()
    
    try:
        # Load navigation config
        nav_config = load_config("navigation_config.json")
        
        # Get plugin registry
        plugin_registry_path = get_config_path() / "plugin_registry.json"
        try:
            with open(plugin_registry_path, "r") as f:
                registry = json.load(f)
                installed_plugins = registry.get("plugins", [])
        except (FileNotFoundError, json.JSONDecodeError) as e:
            logger.error(f"Error reading plugin registry: {e}")
            installed_plugins = []

        # Start with default navigation items
        final_navigation = []
        for item in nav_config.get("default", []):
            # Include subscription page only if monetization is enabled
            if item.get("path") == "/subscriptions":
                if MONETIZATION:
                    final_navigation.append(item)
                    logger.debug("Including subscription page in navigation (monetization enabled)")
                else:
                    logger.debug("Skipping subscription page in navigation (monetization disabled)")
            else:
                final_navigation.append(item)

        # Process plugins - but skip subscription_manager as it's now a core feature
        for plugin in nav_config.get("plugins", []):
            plugin_name = plugin.get("plugin_name")
            if not plugin_name:
                continue

            # Skip subscription_manager since it's now a core feature
            if plugin_name == "subscription_manager":
                continue

            # When monetization is disabled, include all enabled plugins
            # When monetization is enabled, check subscription access
            plugin_is_enabled = any(p.get("name") == plugin_name and p.get("enabled", True) for p in installed_plugins)
            
            if plugin_is_enabled:
                if not MONETIZATION or await subscription_manager.is_plugin_accessible(user["user_id"], plugin_name):
                    final_navigation.append(plugin)

        # Cache navigation for this user
        cache_ttl = 60 if os.getenv("ENV") == "development" else 300  # 1 minute in dev, 5 minutes in prod
        state_manager.set(cache_key, final_navigation, expire_in=cache_ttl)
        
        return {"navigation": final_navigation}

    except Exception as e:
        logger.error(f"Error generating navigation: {e}")
        raise HTTPException(status_code=500, detail=f"Error generating navigation: {str(e)}")
    
@app.get("/api/theme-config")
async def get_theme_config():
    """
    API to fetch theme configuration dynamically.
    """
    return load_config("theme_config.json")

@app.get("/api/settings-config")
async def get_settings_config(user: dict = Depends(get_current_user)):
    """
    API to fetch user settings configuration dynamically.
    This includes filtering plugin settings based on subscription access.
    """
    try:
        # First load the base settings config
        base_config = settings_manager.refresh_settings_config()
        
        # Then filter the config based on subscription status
        filtered_config = await settings_manager.update_settings_visibility(
            MONETIZATION,
            user["user_id"]
        )
        
        return filtered_config
    except Exception as e:
        logger.error(f"Error loading settings configuration: {e}")
        raise HTTPException(status_code=500, detail=f"Error loading settings configuration: {str(e)}")

@app.get("/api/user-profile")
async def get_user_profile(user: dict = Depends(get_current_user)):
    """
    API to fetch user profile information.
    """
    cache_key = f"user_profile:{user['user_id']}"
    cached_profile = state_manager.get(cache_key)
    
    # Return cached profile if available (except in development)
    if cached_profile and os.getenv("ENV") != "development":
        return cached_profile
        
    # Use the optimized cached document retrieval
    user_data = await get_cached_document(
        users_collection, 
        {"username": user["username"]},
        cache_key=f"user:{user['username']}"
    )
    
    if not user_data:
        raise HTTPException(status_code=404, detail="User not found")
    
    # Remove sensitive information
    user_data.pop("hashed_password", None)
    user_data["_id"] = str(user_data["_id"])
    
    # Cache for 5 minutes (shorter in development)
    cache_ttl = 60 if os.getenv("ENV") == "development" else 300
    state_manager.set(cache_key, user_data, expire_in=cache_ttl)
    
    return user_data

@app.post("/api/update-profile")
async def update_user_profile(request: Request, user: dict = Depends(get_current_user)):
    """
    API to update user profile information.
    """
    try:
        data = await request.json()
        
        # Fields that can't be updated
        protected_fields = ["_id", "username", "email", "hashed_password", "user_id"]
        update_data = {k: v for k, v in data.items() if k not in protected_fields}
        
        if not update_data:
            raise HTTPException(status_code=400, detail="No valid fields to update")
        
        # Add updated_at timestamp
        update_data["updated_at"] = datetime.utcnow().isoformat()
        
        result = await users_collection.update_one(
            {"username": user["username"]},
            {"$set": update_data}
        )
        
        if result.modified_count == 0:
            raise HTTPException(status_code=404, detail="User not found or no changes made")
        
        # Invalidate profile cache
        cache_key = f"user_profile:{user['user_id']}"
        state_manager.delete(cache_key)
        db_cache.invalidate(f"user:{user['username']}")
        
        # Publish profile update event
        event_bus.publish("profile_updated", {"user_id": user["user_id"]})
        
        return {"message": "Profile updated successfully"}
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error updating profile for user {user['username']}: {e}")
        raise HTTPException(status_code=500, detail="Internal server error")

@app.get("/api/available-plugins")
async def get_available_plugins(user: dict = Depends(get_current_user)):
    """
    API to fetch the list of installed and enabled plugins that the user has access to.
    """
    # Use caching for plugin list
    cache_key = f"available_plugins:{user['user_id']}"
    cached_plugins = state_manager.get(cache_key)
    
    # Return cached result if available (except in development)
    if cached_plugins and os.getenv("ENV") != "development":
        return {"plugins": cached_plugins}
    
    # Auto-detect new plugins - essential for this endpoint
    await ensure_plugins_up_to_date()
    
    plugin_registry_path = get_config_path() / "plugin_registry.json"

    try:
        with open(plugin_registry_path, "r") as f:
            registry = json.load(f)
            # Filter plugins that are enabled
            enabled_plugins = [plugin for plugin in registry.get("plugins", []) 
                              if plugin.get("enabled", True)]
            
            # If monetization is disabled, return all enabled plugins
            if not MONETIZATION:
                # Cache for 5 minutes (shorter in development)
                cache_ttl = 60 if os.getenv("ENV") == "development" else 300
                state_manager.set(cache_key, enabled_plugins, expire_in=cache_ttl)
                return {"plugins": enabled_plugins}
                
            # Filter plugins that the user has access to when monetization is enabled
            accessible_plugins = []
            for plugin in enabled_plugins:
                # Regular plugin access check
                if await subscription_manager.is_plugin_accessible(user["user_id"], plugin["name"]):
                    accessible_plugins.append(plugin)
            
            # Cache for 5 minutes (shorter in development)
            cache_ttl = 60 if os.getenv("ENV") == "development" else 300
            state_manager.set(cache_key, accessible_plugins, expire_in=cache_ttl)
            
            return {"plugins": accessible_plugins}

    except FileNotFoundError:
        logger.error("⚠️ Plugin registry file not found.")
        raise HTTPException(status_code=404, detail="Plugin registry file not found.")

    except json.JSONDecodeError:
        logger.error("❌ Invalid JSON in plugin registry.")
        raise HTTPException(status_code=500, detail="Invalid JSON in plugin registry.")

    except Exception as e:
        logger.error(f"⚠️ Error loading plugin registry: {e}")
        raise HTTPException(status_code=500, detail="Unexpected error loading plugin registry.")


# Contract v1.0.0: Alias endpoint for plugin discovery
@app.get("/api/plugins")
async def get_plugins(user: dict = Depends(get_current_user)):
    """
    Contract v1.0.0 compliant alias for /api/available-plugins.
    Returns list of plugins accessible to the authenticated user.
    """
    return await get_available_plugins(user)

@app.get("/")
async def read_root():
    # Auto-detect new plugins on homepage access
    asyncio.create_task(ensure_plugins_up_to_date())
    return {"message": "Welcome to Mozaiks Core", "version": "1.0.0"}

@app.post("/api/execute/{plugin_name}")
async def execute_plugin(plugin_name: str, request: Request, user: dict = Depends(get_current_user)):
    """
    API to execute a plugin after validating user authentication and access.
    Access control is enforced only when MONETIZATION=1.
    Special case for subscription_manager.
    
    This endpoint no longer triggers plugin refresh on every call.
    """
    # Check if plugin exists in registry
    plugin_registry_path = get_config_path() / "plugin_registry.json"
    try:
        with open(plugin_registry_path, "r") as f:
            registry = json.load(f)
            installed_plugins = registry.get("plugins", [])
            plugin_exists = any(p.get("name") == plugin_name and p.get("enabled", True) for p in installed_plugins)
            if not plugin_exists:
                logger.warning(f"Plugin {plugin_name} not found in registry or disabled")
                raise HTTPException(status_code=404, detail=f"Plugin '{plugin_name}' not found or disabled")
    except (FileNotFoundError, json.JSONDecodeError) as e:
        logger.error(f"Error reading plugin registry: {e}")
        raise HTTPException(status_code=500, detail="Error reading plugin configuration")
    
    # Check if plugin is loaded in plugin manager
    if plugin_name not in plugin_manager.plugins:
        logger.warning(f"Plugin {plugin_name} not loaded in plugin manager. Available plugins: {list(plugin_manager.plugins.keys())}")
        # Try to reload all plugins - for compatibility with original code
        await plugin_manager.refresh_plugins()
        
        # Check again after refresh
        if plugin_name not in plugin_manager.plugins:
            logger.warning(f"Plugin {plugin_name} still not loaded after refresh")
            raise HTTPException(status_code=404, detail=f"Plugin '{plugin_name}' not found.")

    try:
        data = await request.json()
    except Exception as e:
        logger.error(f"Error parsing request JSON: {e}")
        raise HTTPException(status_code=400, detail="Invalid JSON payload")

    # Contract v1.0.0: Extract bearer token for plugin context
    user_jwt = None
    auth_header = request.headers.get("Authorization") or request.headers.get("authorization")
    if auth_header and auth_header.lower().startswith("bearer "):
        user_jwt = auth_header[7:].strip()  # Strip "Bearer " prefix

    # SECURITY: Inject server-derived context (app_id + user_id + user_jwt)
    # Contract v1.1.0: Also injects _entitlements from plugin's entitlements.yaml
    # Client cannot override these values
    data = await inject_request_context(user, data, plugin_name, user_jwt=user_jwt)

    # Validate user access to the plugin (if monetization is enabled)
    # Skip access check if monetization is disabled
    if MONETIZATION and plugin_name != "subscription_manager" and not await subscription_manager.is_plugin_accessible(user["user_id"], plugin_name):
        logger.warning(f"Access denied for User {user['user_id']} to plugin '{plugin_name}'")
        raise HTTPException(status_code=403, detail=f"Access denied: Subscription does not allow '{plugin_name}'.")

    # Contract v1.1.0: Auto-enforce entitlements if action is defined in entitlements.yaml
    action = data.get("action", "")
    enforcement_result = await _auto_enforce_entitlements(user, plugin_name, action, data)
    if enforcement_result:
        # Check if this is a dry-run response
        if enforcement_result.get("dry_run"):
            return enforcement_result

        # Otherwise it's an error - blocked by entitlements
        error_code = enforcement_result.get("error_code", "ENTITLEMENT_ERROR")
        if error_code == "FEATURE_GATED":
            raise HTTPException(status_code=403, detail=enforcement_result["error"])
        elif error_code == "LIMIT_EXCEEDED":
            raise HTTPException(status_code=429, detail=enforcement_result["error"])
        else:
            raise HTTPException(status_code=403, detail=enforcement_result.get("error", "Entitlement check failed"))

    execution_start = time.time()

    try:
        result = await plugin_manager.execute_plugin(plugin_name, data)

        execution_time = time.time() - execution_start
        logger.info(f"Plugin {plugin_name} executed in {execution_time:.2f}s for app={APP_ID} user={user['user_id']}")

        if isinstance(result, dict) and "error" in result:
            logger.error(f"Execution error for plugin {plugin_name}: {result['error']}")
            raise HTTPException(status_code=500, detail=result["error"])

        # Contract v1.1.0: Auto-consume entitlements on success
        await _auto_consume_entitlements(user, plugin_name, action, result)

        # Publish event when a plugin is successfully executed
        event_bus.publish("plugin_executed", {
            "app_id": APP_ID,
            "plugin": plugin_name,
            "user": user["user_id"],
            "execution_time": execution_time
        })

        return result
    except HTTPException:
        raise
    except Exception as e:
        execution_time = time.time() - execution_start
        logger.error(f"Unexpected error executing plugin {plugin_name} in {execution_time:.2f}s: {str(e)}")
        
        # Publish error event for monitoring
        event_bus.publish("plugin_execution_error", {
            "plugin": plugin_name, 
            "user": user["user_id"],
            "error": str(e),
            "execution_time": execution_time
        })
        
        raise HTTPException(status_code=500, detail=f"Error executing plugin: {str(e)}")

@app.get("/api/check-plugin-access/{plugin_name}")
async def check_plugin_access(plugin_name: str, user: dict = Depends(get_current_user)):
    """
    API to check if the authenticated user has access to a given plugin.
    When MONETIZATION=0, always returns access=true.
    """
    # Use cache for frequent access checks - 5 seconds for immediate feedback
    cache_key = f"plugin_access:{user['user_id']}:{plugin_name}"
    cached_access = state_manager.get(cache_key)
    
    if cached_access is not None:
        return {"plugin": plugin_name, "access": cached_access}
    
    # Always grant access when monetization is disabled
    if not MONETIZATION:
        state_manager.set(cache_key, True, expire_in=60)  # Cache for 1 minute
        return {"plugin": plugin_name, "access": True}
    
    # Regular plugin access check
    access = await subscription_manager.is_plugin_accessible(user["user_id"], plugin_name)
    
    # Cache result for 1 minute for compatibility with original code
    state_manager.set(cache_key, access, expire_in=60)
    
    return {"plugin": plugin_name, "access": access}

# Only include subscription-related endpoints when MONETIZATION=1
if MONETIZATION:
    @app.get("/api/subscription-plans")
    async def get_subscription_plans():
        """
        API to fetch available subscription plans with settings.
        Only available when MONETIZATION=1.
        """
        return load_config("subscription_config.json")  # Return the entire config

    @app.get("/api/user-subscription")
    async def get_user_subscription(user: dict = Depends(get_current_user)):
        """
        Get current user subscription details
        """
        cache_key = f"user_subscription:{user['user_id']}"
        cached_subscription = state_manager.get(cache_key)
        
        if cached_subscription is not None:
            return cached_subscription
            
        subscription = await subscription_manager.get_user_subscription(user["user_id"])
        
        # Cache subscription for 5 minutes
        state_manager.set(cache_key, subscription, expire_in=300)
        
        return subscription

    @app.post("/api/update-subscription")
    async def update_subscription(request: Request, user: dict = Depends(get_current_user)):
        """
        API to update a user's subscription.
        Only available when MONETIZATION=1.
        """
        try:
            data = await request.json()
            new_plan = data.get("new_plan")

            if not new_plan:
                raise HTTPException(status_code=400, detail="New plan is required")

            response = await subscription_manager.change_user_subscription(user["user_id"], new_plan)

            # Invalidate subscription cache
            state_manager.delete(f"user_subscription:{user['user_id']}")
            
            # Invalidate navigation and plugin access caches for this user
            state_manager.delete(f"navigation:{user['user_id']}")
            
            # Clear all plugin access caches for this user
            for key in list(state_manager.state.keys()):
                if key.startswith(f"plugin_access:{user['user_id']}:"):
                    state_manager.delete(key)

            # Publish event when a user changes their subscription
            event_bus.publish("subscription_updated", {"user_id": user["user_id"], "plan": new_plan})

            return response

        except HTTPException as http_err:
            raise http_err
        except Exception as e:
            logger.error(f"Error updating subscription for user {user['user_id']}: {e}")
            raise HTTPException(status_code=500, detail=f"Internal server error: {str(e)}")

    @app.post("/api/cancel-subscription")
    async def cancel_subscription(user: dict = Depends(get_current_user)):
        """
        API to cancel a user's subscription.
        Only available when MONETIZATION=1.
        """
        try:
            response = await subscription_manager.cancel_user_subscription(user["user_id"])

            # Invalidate subscription cache
            state_manager.delete(f"user_subscription:{user['user_id']}")
            
            # Invalidate navigation cache for this user
            state_manager.delete(f"navigation:{user['user_id']}")
            
            # Clear all plugin access caches for this user
            for key in list(state_manager.state.keys()):
                if key.startswith(f"plugin_access:{user['user_id']}:"):
                    state_manager.delete(key)

            # Publish event when a user cancels their subscription
            event_bus.publish("subscription_canceled", {"user_id": user["user_id"]})

            return response

        except HTTPException as http_err:
            raise http_err
        except Exception as e:
            logger.error(f"Error canceling subscription for user {user['user_id']}: {e}")
            raise HTTPException(status_code=500, detail=f"Internal server error: {str(e)}")

@app.post("/api/change-theme")
async def change_theme(request: Request, user: dict = Depends(get_current_user)):
    """
    API to change the theme dynamically.
    """
    try:
        data = await request.json()
        new_theme = data.get("theme_name")

        if not new_theme:
            raise HTTPException(status_code=400, detail="Theme name is required")

        # Store the selected theme in session/state
        state_manager.set(f"theme_{user['user_id']}", new_theme)

        # Publish event when the theme is changed
        event_bus.publish("theme_changed", {"user_id": user["user_id"], "theme": new_theme})

        return {"message": f"Theme changed to {new_theme}", "theme": new_theme}

    except HTTPException as http_err:
        raise http_err
    except Exception as e:
        logger.error(f"Error changing theme for user {user['user_id']}: {e}")
        raise HTTPException(status_code=500, detail=f"Internal server error: {str(e)}")

@app.get("/api/current-theme")
async def get_current_theme(user: dict = Depends(get_current_user)):
    """
    API to get the user's current theme setting.
    """
    theme = state_manager.get(f"theme_{user['user_id']}")
    if not theme:
        # Get default theme from config
        theme_config = load_config("theme_config.json")
        theme = theme_config.get("default_theme", "light")
    
    return {"theme": theme}

@app.get("/api/debug/plugin-status")
async def debug_plugin_status():
    """
    Debug API to check the status of plugin loading.
    """
    # Force a refresh
    await plugin_manager.refresh_plugins()
    
    # Define plugin directory path
    plugins_dir = PLUGIN_DIR
    
    # Get plugin registry
    plugin_registry_path = get_config_path() / "plugin_registry.json"
    try:
        with open(plugin_registry_path, "r") as f:
            registry = json.load(f)
    except Exception as e:
        registry = {"error": str(e)}
    
    # Return detailed status
    return {
        "plugin_directory": plugins_dir,
        "monetization_enabled": MONETIZATION,
        "loaded_plugins": list(plugin_manager.plugins.keys()),
        "registry": registry,
        "plugins_on_disk": [d for d in os.listdir(plugins_dir) 
                           if os.path.isdir(os.path.join(plugins_dir, d))
                           and not d.startswith('_')],
        "navigation_config": load_config("navigation_config.json"),
        "db_cache_stats": {
            "size": getattr(db_cache, "cache", {}).get("size", 0),
            "ttl": getattr(db_cache, "ttl", 0)
        }
    }

@app.get("/api/plugin-settings/{plugin_name}")
async def get_plugin_settings(plugin_name: str, user: dict = Depends(get_current_user)):
    """
    API to fetch plugin-specific settings for the current user.
    """
    try:
        # Check if user has access to this plugin (if monetization is enabled)
        if MONETIZATION and plugin_name != "subscription_manager":
            has_access = await subscription_manager.is_plugin_accessible(user["user_id"], plugin_name)
            if not has_access:
                raise HTTPException(status_code=403, detail="Access denied to plugin settings")
        
        # Get plugin settings
        settings = await settings_manager.get_plugin_settings(user["user_id"], plugin_name)
        return settings
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error fetching plugin settings: {e}")
        raise HTTPException(status_code=500, detail=f"Error fetching plugin settings: {str(e)}")

@app.post("/api/plugin-settings/{plugin_name}")
async def save_plugin_settings(plugin_name: str, request: Request, user: dict = Depends(get_current_user)):
    """
    API to save plugin-specific settings for the current user.
    """
    try:
        # Check if user has access to this plugin (if monetization is enabled)
        if MONETIZATION and plugin_name != "subscription_manager":
            has_access = await subscription_manager.is_plugin_accessible(user["user_id"], plugin_name)
            if not has_access:
                raise HTTPException(status_code=403, detail="Access denied to plugin settings")
        
        # Parse request data
        data = await request.json()
        
        # Save plugin settings
        result = await settings_manager.save_plugin_settings(user["user_id"], plugin_name, data)
        
        # Publish event for plugin settings updated
        event_bus.publish("plugin_settings_updated", {
            "user_id": user["user_id"],
            "plugin": plugin_name
        })
        
        return result
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error saving plugin settings: {e}")
        raise HTTPException(status_code=500, detail=f"Error saving plugin settings: {str(e)}")

@app.post("/api/notification-preferences")
async def update_notification_preferences(request: Request, user: dict = Depends(get_current_user)):
    """
    API to update user notification preferences.
    """
    try:
        data = await request.json()
        
        # Save notification preferences
        result = await settings_manager.save_notification_preferences(user["user_id"], data)
        
        # Publish event for notification preferences updated
        event_bus.publish("notification_preferences_updated", {
            "user_id": user["user_id"]
        })
        
        return result
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error updating notification preferences: {e}")
        raise HTTPException(status_code=500, detail=f"Error updating notification preferences: {str(e)}")

# Middleware for request logging
@app.middleware("http")
async def log_requests(request: Request, call_next):
    """
    Middleware to log all requests with timing information
    """
    start_time = time.time()
    
    # Generate request ID
    request_id = str(hash(f"{start_time}:{request.client.host}"))[0:8]
    
    # Get route path - extract from raw_path to handle URL params correctly
    path = request.url.path
    method = request.method
    
    # Log request start
    logger.info(f"[{request_id}] Request started: {method} {path}")
    
    # Process the request
    try:
        response = await call_next(request)
        
        # Calculate processing time
        process_time = time.time() - start_time
        
        # Add custom header with processing time
        response.headers["X-Process-Time"] = str(process_time)
        
        # Log completion with status code and timing
        logger.info(f"[{request_id}] Request completed: {method} {path} - Status: {response.status_code} - Time: {process_time:.3f}s")
        
        return response
    except Exception as e:
        # Log exceptions
        logger.error(f"[{request_id}] Request error: {method} {path} - Error: {str(e)}")
        process_time = time.time() - start_time
        logger.error(f"[{request_id}] Failed request processing time: {process_time:.3f}s")
        
        # Re-raise the exception to be handled by FastAPI
        raise

# Error handler for uncaught exceptions
@app.exception_handler(Exception)
async def global_exception_handler(request: Request, exc: Exception):
    """Global exception handler to provide consistent error responses"""
    logger.error(f"Unhandled exception: {str(exc)}")
    logger.error(traceback.format_exc())
    
    # Publish error event
    event_bus.publish("api_error", {
        "path": str(request.url),
        "method": request.method,
        "error": str(exc)
    })
    
    return JSONResponse(
        status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
        content={"detail": "An internal server error occurred. Our team has been notified."}
    )

@app.on_event("startup")
async def startup_event():
    """
    Startup event handler to initialize services
    """
    logger.info("🚀 Starting Mozaiks API")
    
    # Initialize the plugin manager asynchronously
    global plugin_manager
    plugin_manager = await plugin_manager.init_async()
    
    # Register all websocket routes after plugins are loaded
    from core.plugin_manager import register_websockets
    await register_websockets(app)
    
    # Set up database
    from core.config.database import verify_connection, initialize_database
    await verify_connection()
    await initialize_database()
    
    # Log startup complete with total plugins loaded
    logger.info(f"✅ Startup complete - {len(plugin_manager.plugins)} plugins loaded")

@app.on_event("shutdown")
async def shutdown_event():
    """
    Shutdown event handler to clean up resources
    """
    logger.info("🛑 Shutting down Mozaiks API")
    
    # Clear caches
    state_manager.clear()
    if hasattr(db_cache, "clear"):
        db_cache.clear()
    config_cache.clear()
    
    logger.info("✅ Shutdown complete")
