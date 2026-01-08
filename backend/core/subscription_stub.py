# /backend/core/subscription_stub.py
import logging
from datetime import datetime

logger = logging.getLogger("mozaiks_core.subscription_stub")

class SubscriptionStub:
    """
    Simplified subscription manager that always grants access when MONETIZATION=0.
    Provides the same interface as the full SubscriptionManager but with simplified implementations.
    """
    
    async def is_plugin_accessible(self, user_id, plugin_name):
        """
        Always grant access to plugins when MONETIZATION=0
        """
        logger.debug(f"SubscriptionStub: Granting access to {plugin_name} for user {user_id}")
        return True
    
    async def get_user_subscription(self, user_id):
        """
        Return a default 'unlimited' subscription
        """
        return {
            "user_id": user_id,
            "plan": "unlimited",
            "status": "active",
            "billing_cycle": None,
            "next_billing_date": None,
            "updated_at": None,
            "is_trial": False,  # Added to match the full implementation
            "trial_info": None  # Added to match the full implementation
        }
    
    async def change_user_subscription(self, user_id, new_plan):
        """
        Pretend to update subscription but always return success
        """
        logger.info(f"SubscriptionStub: Ignoring subscription change request for user {user_id}")
        return {"message": "Subscription updated successfully", "new_plan": new_plan}
    
    async def cancel_user_subscription(self, user_id):
        """
        Pretend to cancel subscription but always return success
        """
        logger.info(f"SubscriptionStub: Ignoring subscription cancel request for user {user_id}")
        return {"message": "Subscription canceled successfully"}
    
    async def log_billing_event(self, user_id, amount, event_type, status, metadata=None):
        """
        Log billing events (no-op in stub)
        """
        logger.info(f"SubscriptionStub: Ignoring billing event for user {user_id}")
        return True
    
    def get_available_plans(self):
        """
        Return a single unlimited plan
        """
        return [{
            "name": "unlimited",
            "display_name": "Unlimited",
            "price": 0,
            "billing_cycle": "none",
            "features": ["Full access to all plugins and features"],
            "plugins_unlocked": ["*"]
        }]
    
    async def start_user_trial(self, user_id):
        """
        No trials in stub version, just return success
        Added to match the full implementation
        """
        logger.info(f"SubscriptionStub: Ignoring trial start request for user {user_id}")
        return {
            "plan": "unlimited",
            "trial_end_date": None,
            "trial_days": 0
        }
    
    async def check_trial_status(self, user_id):
        """
        No trials to check in stub version
        Added to match the full implementation
        """
        return {"expired": False, "days_remaining": 0}
    
    def calculate_next_billing_date(self, current_date):
        """
        No billing dates in non-monetized mode
        """
        return None