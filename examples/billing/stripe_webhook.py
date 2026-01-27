"""
Stripe Webhook Handler for mozaiks Subscription Sync

This script receives Stripe webhook events and syncs subscription
changes to your mozaiks deployment.

Setup:
1. pip install flask stripe requests
2. Set environment variables (see below)
3. Run: python stripe_webhook.py
4. Point Stripe webhooks to: https://your-domain.com/webhooks/stripe

Environment Variables:
- STRIPE_SECRET_KEY: Your Stripe secret key (sk_...)
- STRIPE_WEBHOOK_SECRET: Webhook signing secret (whsec_...)
- MOZAIKS_API_URL: Your mozaiks API URL (http://localhost:8000)
- INTERNAL_API_KEY: Same key configured in mozaiks (X-Internal-API-Key)
"""

import os
import logging
from flask import Flask, request, jsonify
import stripe
import requests

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = Flask(__name__)

# Configuration from environment
stripe.api_key = os.environ.get("STRIPE_SECRET_KEY")
WEBHOOK_SECRET = os.environ.get("STRIPE_WEBHOOK_SECRET")
MOZAIKS_API_URL = os.environ.get("MOZAIKS_API_URL", "http://localhost:8000")
MOZAIKS_ADMIN_TOKEN = os.environ.get("INTERNAL_API_KEY")  # Same key used by mozaiks

# Map your Stripe price IDs to mozaiks plan IDs
# Update these to match your Stripe products
PRICE_TO_PLAN = {
    "price_free_monthly": "free",
    "price_starter_monthly": "starter",
    "price_pro_monthly": "pro",
    "price_enterprise_monthly": "enterprise",
    # Add yearly prices too
    "price_starter_yearly": "starter",
    "price_pro_yearly": "pro",
    "price_enterprise_yearly": "enterprise",
}


def sync_to_mozaiks(user_id: str, plan_id: str, status: str, metadata: dict = None):
    """
    Sync subscription state to mozaiks.
    """
    if not MOZAIKS_ADMIN_TOKEN:
        logger.error("MOZAIKS_ADMIN_TOKEN (INTERNAL_API_KEY) not set, cannot sync")
        return False
    
    payload = {
        "userId": user_id,
        "plan": plan_id,
        "status": status,
    }
    
    # Add optional fields from metadata
    if metadata:
        if metadata.get("stripe_subscription_id"):
            payload["stripeSubscriptionId"] = metadata["stripe_subscription_id"]
        if metadata.get("current_period_end"):
            from datetime import datetime
            payload["nextBillingDate"] = datetime.fromtimestamp(
                metadata["current_period_end"]
            ).isoformat()
    
    try:
        response = requests.post(
            f"{MOZAIKS_API_URL}/api/internal/subscription/sync",
            headers={
                "X-Internal-API-Key": MOZAIKS_ADMIN_TOKEN,
                "Content-Type": "application/json",
            },
            json=payload,
            timeout=10,
        )
        response.raise_for_status()
        logger.info(f"Synced subscription for user {user_id}: plan={plan_id}, status={status}")
        return True
    except requests.RequestException as e:
        logger.error(f"Failed to sync to mozaiks: {e}")
        return False


def get_user_id_from_subscription(subscription: dict) -> str | None:
    """
    Extract user ID from Stripe subscription metadata.
    
    You should set metadata.user_id when creating the checkout session.
    """
    # Try metadata first
    user_id = subscription.get("metadata", {}).get("user_id")
    if user_id:
        return user_id
    
    # Fallback to customer ID
    customer_id = subscription.get("customer")
    if customer_id:
        # Optionally look up your user by Stripe customer ID
        logger.warning(f"No user_id in metadata, using customer_id: {customer_id}")
        return customer_id
    
    return None


def get_plan_from_subscription(subscription: dict) -> str:
    """
    Extract plan ID from Stripe subscription.
    """
    items = subscription.get("items", {}).get("data", [])
    if not items:
        return "free"
    
    price_id = items[0].get("price", {}).get("id")
    return PRICE_TO_PLAN.get(price_id, "free")


@app.route("/webhooks/stripe", methods=["POST"])
def stripe_webhook():
    """
    Handle Stripe webhook events.
    """
    payload = request.data
    sig_header = request.headers.get("Stripe-Signature")
    
    if not WEBHOOK_SECRET:
        logger.error("STRIPE_WEBHOOK_SECRET not set")
        return jsonify({"error": "Webhook not configured"}), 500
    
    try:
        event = stripe.Webhook.construct_event(
            payload, sig_header, WEBHOOK_SECRET
        )
    except ValueError as e:
        logger.error(f"Invalid payload: {e}")
        return jsonify({"error": "Invalid payload"}), 400
    except stripe.error.SignatureVerificationError as e:
        logger.error(f"Invalid signature: {e}")
        return jsonify({"error": "Invalid signature"}), 400
    
    event_type = event["type"]
    logger.info(f"Received Stripe event: {event_type}")
    
    # Handle subscription events
    if event_type in [
        "customer.subscription.created",
        "customer.subscription.updated",
    ]:
        subscription = event["data"]["object"]
        user_id = get_user_id_from_subscription(subscription)
        
        if not user_id:
            logger.error("No user_id found in subscription metadata")
            return jsonify({"error": "No user_id"}), 400
        
        plan_id = get_plan_from_subscription(subscription)
        status = subscription.get("status", "active")
        
        # Map Stripe status to mozaiks status
        status_map = {
            "active": "active",
            "trialing": "active",
            "past_due": "past_due",
            "canceled": "canceled",
            "unpaid": "past_due",
        }
        mozaiks_status = status_map.get(status, "active")
        
        sync_to_mozaiks(
            user_id=user_id,
            plan_id=plan_id,
            status=mozaiks_status,
            metadata={
                "stripe_subscription_id": subscription.get("id"),
                "stripe_customer_id": subscription.get("customer"),
                "current_period_end": subscription.get("current_period_end"),
            }
        )
    
    elif event_type == "customer.subscription.deleted":
        subscription = event["data"]["object"]
        user_id = get_user_id_from_subscription(subscription)
        
        if user_id:
            # Downgrade to free plan
            sync_to_mozaiks(
                user_id=user_id,
                plan_id="free",
                status="canceled",
                metadata={
                    "stripe_subscription_id": subscription.get("id"),
                    "canceled_at": subscription.get("canceled_at"),
                }
            )
    
    elif event_type == "invoice.payment_failed":
        invoice = event["data"]["object"]
        subscription_id = invoice.get("subscription")
        
        if subscription_id:
            # Fetch subscription to get user_id
            try:
                subscription = stripe.Subscription.retrieve(subscription_id)
                user_id = get_user_id_from_subscription(subscription)
                
                if user_id:
                    sync_to_mozaiks(
                        user_id=user_id,
                        plan_id=get_plan_from_subscription(subscription),
                        status="past_due",
                        metadata={
                            "stripe_subscription_id": subscription_id,
                            "payment_failed_at": invoice.get("created"),
                        }
                    )
            except stripe.error.StripeError as e:
                logger.error(f"Failed to fetch subscription: {e}")
    
    return jsonify({"status": "ok"}), 200


@app.route("/health", methods=["GET"])
def health():
    """Health check endpoint."""
    return jsonify({"status": "healthy"}), 200


if __name__ == "__main__":
    # Validate configuration
    if not stripe.api_key:
        logger.warning("STRIPE_SECRET_KEY not set")
    if not WEBHOOK_SECRET:
        logger.warning("STRIPE_WEBHOOK_SECRET not set")
    if not MOZAIKS_ADMIN_TOKEN:
        logger.warning("INTERNAL_API_KEY not set - sync to mozaiks will fail")
    
    port = int(os.environ.get("PORT", 4242))
    logger.info(f"Starting Stripe webhook handler on port {port}")
    app.run(host="0.0.0.0", port=port)
