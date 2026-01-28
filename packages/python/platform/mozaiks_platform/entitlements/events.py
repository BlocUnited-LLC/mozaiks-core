# core/entitlements/events.py
"""
Entitlement Event Emission

Emits entitlement events to the event bus and optionally to a webhook.

Events:
- entitlement.consumed: After successful consumption
- entitlement.limit_reached: When consumption is blocked
- entitlement.feature_blocked: When feature check fails
- entitlement.period_reset: When period rolls over

Webhook Support:
Set ENTITLEMENT_WEBHOOK_URL environment variable to receive events.
Webhooks are fire-and-forget (non-blocking).

Contract Version: 1.0
"""
import os
import logging
import asyncio
from datetime import datetime, timezone
from typing import Optional, Dict, Any

logger = logging.getLogger("mozaiks_core.entitlements.events")

# Webhook URL (opt-in)
ENTITLEMENT_WEBHOOK_URL = os.getenv("ENTITLEMENT_WEBHOOK_URL")

# App ID
APP_ID = os.getenv("MOZAIKS_APP_ID", "dev_app")

# Event bus reference (set lazily)
_event_bus = None


def get_event_bus():
    """Get event bus with lazy import to avoid circular dependency."""
    global _event_bus
    if _event_bus is None:
        try:
            from mozaiks_infra.event_bus import event_bus
            _event_bus = event_bus
        except ImportError:
            logger.warning("Event bus not available")
            return None
    return _event_bus


async def send_webhook(event_type: str, payload: Dict[str, Any]) -> None:
    """
    Send event to webhook (fire-and-forget).

    Args:
        event_type: Event type string
        payload: Event payload
    """
    if not ENTITLEMENT_WEBHOOK_URL:
        return

    try:
        import httpx

        webhook_payload = {
            "event": event_type,
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "app_id": APP_ID,
            **payload
        }

        async with httpx.AsyncClient(timeout=10.0) as client:
            response = await client.post(
                ENTITLEMENT_WEBHOOK_URL,
                json=webhook_payload,
                headers={"Content-Type": "application/json"}
            )

            if response.status_code >= 400:
                logger.warning(
                    f"Webhook returned {response.status_code} for {event_type}"
                )
            else:
                logger.debug(f"Webhook sent successfully for {event_type}")

    except ImportError:
        logger.debug("httpx not available, skipping webhook")
    except Exception as e:
        logger.warning(f"Webhook send failed for {event_type}: {e}")


def emit_event(event_type: str, payload: Dict[str, Any], send_to_webhook: bool = True) -> None:
    """
    Emit an event to the event bus and optionally to webhook.

    Args:
        event_type: Event type string
        payload: Event payload
        send_to_webhook: Whether to also send to webhook
    """
    # Publish to local event bus
    event_bus = get_event_bus()
    if event_bus:
        event_bus.publish(event_type, payload)

    # Send to webhook (async, non-blocking)
    if send_to_webhook and ENTITLEMENT_WEBHOOK_URL:
        try:
            loop = asyncio.get_event_loop()
            if loop.is_running():
                asyncio.create_task(send_webhook(event_type, payload))
            else:
                # Sync context - just skip webhook
                pass
        except RuntimeError:
            # No event loop - skip webhook
            pass


async def emit_consumed_event(
    user_id: str,
    plugin: str,
    limit_key: str,
    amount: int,
    used: int,
    remaining: int,
    period: str
) -> None:
    """
    Emit entitlement.consumed event.

    Args:
        user_id: User who consumed
        plugin: Plugin name
        limit_key: Which limit was consumed
        amount: How much was consumed
        used: Total used after consumption
        remaining: Remaining after consumption
        period: Current period key
    """
    payload = {
        "user_id": user_id,
        "plugin": plugin,
        "limit_key": limit_key,
        "amount": amount,
        "used": used,
        "remaining": remaining,
        "period": period
    }

    emit_event("entitlement.consumed", payload, send_to_webhook=True)
    logger.info(f"Entitlement consumed: {user_id} used {amount} {limit_key} ({remaining} remaining)")


async def emit_limit_reached_event(
    user_id: str,
    plugin: str,
    limit_key: str,
    attempted: int,
    limit: int,
    period: str
) -> None:
    """
    Emit entitlement.limit_reached event.

    Args:
        user_id: User who hit limit
        plugin: Plugin name
        limit_key: Which limit was reached
        attempted: How much was attempted
        limit: What the limit is
        period: Current period key
    """
    payload = {
        "user_id": user_id,
        "plugin": plugin,
        "limit_key": limit_key,
        "attempted": attempted,
        "limit": limit,
        "period": period
    }

    emit_event("entitlement.limit_reached", payload, send_to_webhook=True)
    logger.info(f"Entitlement limit reached: {user_id} attempted {attempted} {limit_key} (limit: {limit})")


async def emit_feature_blocked_event(
    user_id: str,
    plugin: str,
    feature_key: str,
    tier: str
) -> None:
    """
    Emit entitlement.feature_blocked event.

    Args:
        user_id: User who was blocked
        plugin: Plugin name
        feature_key: Which feature was blocked
        tier: User's current tier
    """
    payload = {
        "user_id": user_id,
        "plugin": plugin,
        "feature_key": feature_key,
        "tier": tier
    }

    emit_event("entitlement.feature_blocked", payload, send_to_webhook=True)
    logger.info(f"Entitlement feature blocked: {user_id} denied {feature_key} on {tier} tier")


async def emit_period_reset_event(
    user_id: str,
    plugin: str,
    limit_key: str,
    previous_period: str,
    new_period: str,
    previous_used: int
) -> None:
    """
    Emit entitlement.period_reset event.

    This is NOT sent to webhook (internal only).

    Args:
        user_id: User whose period reset
        plugin: Plugin name
        limit_key: Which limit reset
        previous_period: Previous period key
        new_period: New period key
        previous_used: How much was used in previous period
    """
    payload = {
        "user_id": user_id,
        "plugin": plugin,
        "limit_key": limit_key,
        "previous_period": previous_period,
        "new_period": new_period,
        "previous_used": previous_used
    }

    # Period reset is internal only - no webhook
    emit_event("entitlement.period_reset", payload, send_to_webhook=False)
    logger.info(f"Entitlement period reset: {user_id} {limit_key} from {previous_period} to {new_period}")


# Event handler registration helpers

def register_entitlement_handlers():
    """
    Register default handlers for entitlement events.

    Called during startup if needed.
    """
    event_bus = get_event_bus()
    if not event_bus:
        return

    # Default handlers are just logging (already done in emit functions)
    # Apps can subscribe to these events for custom handling

    def log_consumed(data):
        # Already logged in emit function
        pass

    def log_limit_reached(data):
        # Already logged in emit function
        pass

    def log_feature_blocked(data):
        # Already logged in emit function
        pass

    # Handlers are optional - events are already emitted via publish()
    # Apps can add their own handlers via event_bus.subscribe()
