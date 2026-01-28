# /backend/core/subscription_manager.py
"""
BOUNDARY CONTRACT: Subscription Manager (READ-ONLY for App Users)

This manager enforces subscription-based access control for plugins.
It is READ-ONLY for app users - subscription mutations are only allowed
via the MozaiksAI Control Plane using X-Internal-API-Key authentication.

READ (available to all authenticated users):
  - get_user_subscription(user_id)
  - is_plugin_accessible(user_id, plugin_name)
  - get_available_plans()

WRITE (Control Plane only, via X-Internal-API-Key):
  - change_user_subscription(user_id, new_plan, *, _internal_call=False)
  - cancel_user_subscription(user_id, *, _internal_call=False)
  - start_user_trial(user_id, *, _internal_call=False)
  - sync_subscription_from_control_plane(user_id, subscription_data)

NOTE: Payment processing is handled by MozaiksAI Control Plane (Stripe),
not by this runtime. This module only stores/reads subscription state.
"""
import os
import json
import logging
from fastapi import HTTPException
from datetime import datetime, timezone, timedelta
from dateutil.relativedelta import relativedelta  # Ensure you have this installed
from mozaiks_infra.config.database import (
    subscriptions_collection,
    subscription_history_collection,
    billing_history_collection,
)
from mozaiks_infra.utils.log_sanitizer import sanitize_for_log, sanitize_dict_for_log

logger = logging.getLogger("mozaiks_core.subscription_manager")

# Load environment variables
SUBSCRIPTION_API_URL = os.getenv("SUBSCRIPTION_API_URL", "")

# Control whether subscription writes are allowed (for local dev only)
ALLOW_LOCAL_SUBSCRIPTION_WRITES = os.getenv("ALLOW_LOCAL_SUBSCRIPTION_WRITES", "false").lower() in ("1", "true", "yes")

# Default config when no subscription_config.json exists.
# NOTE: Payment integration is handled by Mozaiks Control Plane, not MozaiksCore.
# This runtime only enforces subscription state — it does NOT process payments.
DEFAULT_SUBSCRIPTION_CONFIG = {
    "subscription_plans": []
}


def _require_internal_call(operation: str, _internal_call: bool) -> None:
    """
    Guard function to ensure write operations are only called internally.
    In production, subscription mutations must come from Control Plane.
    """
    if _internal_call:
        return  # Allowed - internal service call
    
    if ALLOW_LOCAL_SUBSCRIPTION_WRITES:
        logger.warning(f"⚠️ {operation}: Allowing local write (ALLOW_LOCAL_SUBSCRIPTION_WRITES=true)")
        return  # Allowed - local dev mode
    
    raise HTTPException(
        status_code=403,
        detail=f"Subscription {operation} is not allowed. Subscription changes must come from Control Plane."
    )

class SubscriptionManager:
    def __init__(self):
        self.subscription_service_url = SUBSCRIPTION_API_URL
        self.subscription_config = self._load_subscription_config()

    def _load_subscription_config(self):
        """
        Loads the subscription config via the central config loader.
        """
        from .config.config_loader import get_subscription_config
        
        config = get_subscription_config()
        if not config:
            logger.warning("Subscription config not found, using default")
            return DEFAULT_SUBSCRIPTION_CONFIG
        return config

    def get_available_plans(self):
        """
        Returns all available subscription plans.
        """
        return self.subscription_config.get("subscription_plans", [])

    async def get_user_subscription(self, user_id: str):
        """Retrieves the user's current subscription from MongoDB."""
        if subscriptions_collection is None:
            logger.error("❌ Database connection is unavailable!")
            raise HTTPException(status_code=500, detail="Database error")
        
        subscription = await subscriptions_collection.find_one({"user_id": user_id})
        if not subscription:
            return {"user_id": user_id, "plan": "free", "status": "inactive"}
        
        # Always provide trial_info if status is trialing
        trial_info = None
        if subscription.get("status") == "trialing":
            # Get settings for trial duration
            settings = self.subscription_config.get("settings", {})
            trial_period_days = settings.get("trial_period_days", 14)
            
            # Calculate trial end date - assume trial started when subscription was created
            start_date_str = subscription.get("created_at") or subscription.get("updated_at")
            
            if start_date_str:
                try:
                    start_date = datetime.fromisoformat(start_date_str)
                except:
                    # If date parsing fails, use a fallback approach
                    start_date = datetime.now(timezone.utc) - timedelta(days=1)
            else:
                # No dates available, assume trial started yesterday
                start_date = datetime.now(timezone.utc) - timedelta(days=1)
                
            # Calculate end date and days remaining
            end_date = start_date + timedelta(days=trial_period_days)
            now = datetime.now(timezone.utc)
            days_remaining = max(0, (end_date - now).days)
            
            # Create trial info
            trial_info = {
                "days_remaining": days_remaining,
                "end_date": end_date.isoformat()
            }
            
            # Log the trial info for debugging
            logger.info(f"Created trial info for user {sanitize_for_log(user_id)}: {sanitize_dict_for_log(trial_info)}")
            
            # Update subscription with trial end date if it's not already set
            if not subscription.get("trial_end_date"):
                await subscriptions_collection.update_one(
                    {"user_id": user_id},
                    {"$set": {"trial_end_date": end_date.isoformat()}}
                )

        # Defensive fallback: if trial status is set but trial_info could not be derived
        # from persisted data (e.g., missing trial_end_date), provide a temporary default.
        # NOTE: This is NOT the intended steady state. Trial info should always be computed
        # from source-of-truth data when available. This guard handles corrupted/missing data.
        if subscription.get("status") == "trialing" and not trial_info:
            trial_info = {
                "days_remaining": 14,
                "end_date": (datetime.now(timezone.utc) + timedelta(days=14)).isoformat()
            }
            logger.warning(f"Defensive fallback: synthesized trial_info for user {sanitize_for_log(user_id)} (missing persisted data)")

        # Return the subscription with is_trial flag and trial_info
        return {
            "user_id": user_id,
            "plan": subscription["plan"],
            "billing_cycle": subscription.get("billing_cycle", "monthly"),
            "status": subscription["status"],
            "is_trial": subscription.get("status") == "trialing",
            "trial_info": trial_info,
            "next_billing_date": subscription.get("next_billing_date"),
            "updated_at": subscription["updated_at"],
        }

    async def change_user_subscription(self, user_id: str, new_plan: str, *, _internal_call: bool = False):
        """
        Updates the user's subscription plan in MongoDB and logs the change.
        
        WRITE OPERATION - Control Plane only.
        Set _internal_call=True when called from admin routes with X-Internal-API-Key.
        """
        _require_internal_call("change", _internal_call)
        available_plans = self.get_available_plans()
        valid_plans = [plan["name"].lower() for plan in available_plans]
        if new_plan.lower() not in valid_plans:
            raise HTTPException(status_code=400, detail="Invalid subscription plan")
        now = datetime.now(timezone.utc)
        result = await subscriptions_collection.update_one(
            {"user_id": user_id},
            {
                "$set": {
                    "plan": new_plan,
                    "status": "active",
                    "updated_at": now.isoformat(),
                    "next_billing_date": self.calculate_next_billing_date(now).isoformat(),
                }
            },
            upsert=True
        )
        previous_subscription = await self.get_user_subscription(user_id)
        if result.modified_count > 0 or result.upserted_id:
            logger.info(f"✅ Subscription updated for user {user_id}: {new_plan}")
            await subscription_history_collection.insert_one({
                "user_id": user_id,
                "previous_plan": previous_subscription.get("plan", "free"),
                "new_plan": new_plan,
                "timestamp": now.isoformat(),
            })
            return {"message": "Subscription updated successfully", "new_plan": new_plan}
        else:
            raise HTTPException(status_code=500, detail="Failed to update subscription")

    async def cancel_user_subscription(self, user_id: str, *, _internal_call: bool = False):
        """
        Cancels the user's subscription, downgrades to free, and logs the change.
        
        WRITE OPERATION - Control Plane only.
        Set _internal_call=True when called from admin routes with X-Internal-API-Key.
        """
        _require_internal_call("cancel", _internal_call)
        now = datetime.now(timezone.utc)
        subscription = await self.get_user_subscription(user_id)
        previous_plan = subscription["plan"]
        result = await subscriptions_collection.update_one(
            {"user_id": user_id},
            {
                "$set": {
                    "plan": "free",
                    "status": "inactive",
                    "updated_at": now.isoformat(),
                    "next_billing_date": None,
                }
            }
        )
        if result.modified_count > 0:
            logger.info(f"🚫 Subscription canceled for user {user_id}")
            await subscription_history_collection.insert_one({
                "user_id": user_id,
                "previous_plan": previous_plan,
                "new_plan": "free",
                "timestamp": now.isoformat(),
            })
            return {"message": "Subscription canceled successfully"}
        else:
            raise HTTPException(status_code=500, detail="Failed to cancel subscription")

    async def start_user_trial(self, user_id: str, *, _internal_call: bool = False):
        """
        Start a trial subscription for a new user.
        
        WRITE OPERATION - Control Plane only.
        Set _internal_call=True when called from admin routes with X-Internal-API-Key.
        """
        _require_internal_call("start_trial", _internal_call)
        # Get trial plan from config, with fallbacks
        settings = self.subscription_config.get("settings", {})
        trial_plan = settings.get("trial_plan")
        
        # If no trial plan is specified in settings, use highest paid plan
        if not trial_plan:
            highest_price = 0
            available_plans = self.get_available_plans()
            for plan in available_plans:
                if plan.get("name") != "admin" and plan.get("price", 0) > highest_price:
                    highest_price = plan.get("price", 0)
                    trial_plan = plan.get("name")
            
            # If still no plan (maybe all free?), default to premium
            if not trial_plan:
                trial_plan = "premium"
        
        # Get trial duration from config
        trial_days = settings.get("trial_period_days", 14)
        
        # Calculate trial end date
        now = datetime.now(timezone.utc)
        trial_end = now + relativedelta(days=trial_days)
        
        # Create subscription record
        await subscriptions_collection.update_one(
            {"user_id": user_id},
            {
                "$set": {
                    "user_id": user_id,
                    "plan": trial_plan,
                    "status": "trialing",
                    "is_trial": True,
                    "trial_start_date": now.isoformat(),
                    "trial_end_date": trial_end.isoformat(),
                    "updated_at": now.isoformat(),
                }
            },
            upsert=True
        )
        
        logger.info(f"Trial started for user {user_id}: Plan={trial_plan}, Days={trial_days}")
        
        return {
            "plan": trial_plan,
            "trial_end_date": trial_end.isoformat(),
            "trial_days": trial_days
        }

    async def check_trial_status(self, user_id: str):
        """Check if a trial has expired and update status if needed"""
        subscription = await subscriptions_collection.find_one({"user_id": user_id})
        if not subscription:
            return False
        
        # If it's not a trial, no need to check
        if subscription.get("status") != "trialing" or not subscription.get("is_trial"):
            return False
        
        # Check if trial has expired
        trial_end = datetime.fromisoformat(subscription.get("trial_end_date"))
        now = datetime.now(timezone.utc)
        
        if now > trial_end:
            # Trial expired, downgrade to free
            await subscriptions_collection.update_one(
                {"user_id": user_id},
                {
                    "$set": {
                        "plan": "free",
                        "status": "inactive",
                        "is_trial": False,
                        "updated_at": now.isoformat(),
                    }
                }
            )
            return {"expired": True, "downgraded": True}
        
        # Trial still active
        return {
            "expired": False, 
            "days_remaining": (trial_end - now).days
        }

    async def log_billing_event(self, user_id: str, amount: float, event_type: str, status: str, metadata=None, *, _internal_call: bool = False):
        """
        Logs billing events such as payments, refunds, and invoices.
        
        WRITE OPERATION - Control Plane only.
        Set _internal_call=True when called from admin routes with X-Internal-API-Key.
        """
        _require_internal_call("log_billing", _internal_call)
        now = datetime.now(timezone.utc)
        billing_event = {
            "user_id": user_id,
            "amount": amount,
            "event_type": event_type,  # "payment", "refund", "invoice"
            "status": status,          # "successful", "failed", "pending"
            "timestamp": now.isoformat(),
            "metadata": metadata or {},
        }
        await billing_history_collection.insert_one(billing_event)
        logger.info(f"💰 Billing event logged: {billing_event}")

    async def is_plugin_accessible(self, user_id: str, plugin_name: str):
        """Checks whether a user has access to a given plugin based on their subscription plan."""
        # First check if trial has expired
        await self.check_trial_status(user_id)

        # Then get current subscription (which will be updated if trial expired)
        subscription = await self.get_user_subscription(user_id)
        user_plan = subscription["plan"]

        # Find what plugins are unlocked for this plan
        unlocked_plugins = []
        for plan in self.subscription_config.get("subscription_plans", []):
            if plan["name"].lower() == user_plan.lower():
                unlocked_plugins = plan["plugins_unlocked"]

        return "*" in unlocked_plugins or plugin_name in unlocked_plugins

    async def get_user_plugin_tier(self, user_id: str, plugin_name: str) -> str:
        """
        Get the user's tier for a specific plugin.

        Contract v1.0: Plugin-level entitlements support per-plugin tier assignments.

        Resolution order:
        1. subscription.plugin_tiers[plugin_name] (per-plugin override)
        2. subscription_config.plans[plan].plugins[plugin_name] (plan-level mapping)
        3. subscription.plan (global plan name)
        4. "free" (default fallback)

        Args:
            user_id: User identifier
            plugin_name: Plugin name

        Returns:
            Tier name (e.g., "free", "basic", "pro")
        """
        # First check if trial has expired
        await self.check_trial_status(user_id)

        # Get current subscription
        subscription = await subscriptions_collection.find_one({"user_id": user_id})

        if not subscription:
            return "free"

        # Check for per-plugin tier override (set by Control Plane)
        plugin_tiers = subscription.get("plugin_tiers", {})
        if plugin_name in plugin_tiers:
            return plugin_tiers[plugin_name]

        # Check subscription_config for plan→plugin tier mapping
        user_plan = subscription.get("plan", "free")
        for plan_config in self.subscription_config.get("subscription_plans", []):
            if plan_config.get("name", "").lower() == user_plan.lower():
                # Check if plan defines plugin-specific tiers
                plugins_mapping = plan_config.get("plugins", {})
                if plugin_name in plugins_mapping:
                    return plugins_mapping[plugin_name]

        # Fallback to global plan name as tier
        return user_plan

    async def set_user_plugin_tier(
        self,
        user_id: str,
        plugin_name: str,
        tier: str,
        *,
        _internal_call: bool = False
    ):
        """
        Set a user's tier for a specific plugin.

        WRITE OPERATION - Control Plane only.

        Args:
            user_id: User identifier
            plugin_name: Plugin name
            tier: Tier to assign
            _internal_call: Must be True (called from admin routes)
        """
        _require_internal_call("set_plugin_tier", _internal_call)

        now = datetime.now(timezone.utc)

        await subscriptions_collection.update_one(
            {"user_id": user_id},
            {
                "$set": {
                    f"plugin_tiers.{plugin_name}": tier,
                    "updated_at": now.isoformat()
                }
            },
            upsert=True
        )

        logger.info(f"✅ Set plugin tier for user {user_id}: {plugin_name}={tier}")

        return {"success": True, "user_id": user_id, "plugin": plugin_name, "tier": tier}

    def calculate_next_billing_date(self, current_date):
        """
        Calculates the next billing date based on the user's plan cycle.
        """
        return current_date + relativedelta(months=1)

    async def sync_subscription_from_control_plane(self, user_id: str, subscription_data: dict, *, _internal_call: bool = False):
        """
        Sync subscription state from MozaiksAI Control Plane.
        
        This is the PRIMARY method for subscription updates in production.
        Control Plane pushes subscription state after payment events.
        
        Expected subscription_data:
        {
            "plan": "premium",
            "status": "active" | "trialing" | "inactive" | "canceled",
            "billing_cycle": "monthly" | "yearly",
            "next_billing_date": "2024-02-01T00:00:00Z",
            "trial_end_date": "2024-01-15T00:00:00Z",  # optional
            "stripe_subscription_id": "sub_xxx",  # optional, for reference
        }
        
        WRITE OPERATION - Control Plane only.
        """
        _require_internal_call("sync_from_control_plane", _internal_call)
        
        now = datetime.now(timezone.utc)
        
        # Build update document
        update_doc = {
            "user_id": user_id,
            "plan": subscription_data.get("plan", "free"),
            "status": subscription_data.get("status", "inactive"),
            "billing_cycle": subscription_data.get("billing_cycle", "monthly"),
            "updated_at": now.isoformat(),
            "synced_from_control_plane": True,
            "last_sync_at": now.isoformat(),
        }

        app_id = subscription_data.get("app_id") or subscription_data.get("appId")
        if app_id:
            update_doc["app_id"] = app_id
        
        # Optional fields
        if subscription_data.get("next_billing_date"):
            update_doc["next_billing_date"] = subscription_data["next_billing_date"]
        if subscription_data.get("trial_end_date"):
            update_doc["trial_end_date"] = subscription_data["trial_end_date"]
            update_doc["is_trial"] = subscription_data.get("status") == "trialing"
        if subscription_data.get("stripe_subscription_id"):
            update_doc["external_subscription_id"] = subscription_data["stripe_subscription_id"]
        
        # Upsert subscription
        result = await subscriptions_collection.update_one(
            {"user_id": user_id},
            {"$set": update_doc},
            upsert=True
        )
        
        logger.info(f"✅ Subscription synced from Control Plane for user {user_id}: plan={update_doc['plan']}, status={update_doc['status']}")
        
        return {
            "success": True,
            "user_id": user_id,
            "plan": update_doc["plan"],
            "status": update_doc["status"],
        }


# Initialize the subscription manager
subscription_manager = SubscriptionManager()
