# core/entitlements/usage.py
"""
Entitlement Usage Tracking

Handles storage and retrieval of usage data for consumable limits.
Implements lazy period reset (resets on first access of new period).

MongoDB Collection: entitlement_usage
Schema:
{
    "_id": ObjectId,
    "app_id": str,           # App instance ID
    "user_id": str,          # User identifier
    "plugin": str,           # Plugin name
    "limit_key": str,        # Limit identifier (e.g., "videos_per_month")
    "period_type": str,      # "monthly", "daily", "weekly", "never"
    "period": str,           # Reset boundary key (e.g., "2026-01" for monthly)
    "used": int,             # Current usage count
    "limit": int,            # Snapshot of limit at period start
    "first_use": datetime,   # When first consumption happened this period
    "last_use": datetime,    # Most recent consumption
    "updated_at": datetime
}

Index: { app_id, user_id, plugin, limit_key, period } unique

Contract Version: 1.0
"""
import os
import logging
from datetime import datetime, timezone, timedelta
from typing import Optional, Tuple, Dict, Any

logger = logging.getLogger("mozaiks_core.entitlements.usage")

# App ID from environment
APP_ID = os.getenv("MOZAIKS_APP_ID", "dev_app")

# Will be set after database module loads
_usage_collection = None


def set_usage_collection(collection) -> None:
    """Set the MongoDB collection for usage tracking."""
    global _usage_collection
    _usage_collection = collection
    logger.info("Entitlement usage collection initialized")


def get_usage_collection():
    """Get the usage collection, with lazy initialization."""
    global _usage_collection
    if _usage_collection is None:
        try:
            from mozaiks_infra.config.database import entitlement_usage_collection
            _usage_collection = entitlement_usage_collection
        except ImportError:
            logger.warning("entitlement_usage_collection not available in database module")
            return None
    return _usage_collection


def get_period_key(period_type: str, reference_date: datetime = None) -> str:
    """
    Get the period key for a given period type.

    Args:
        period_type: "monthly", "daily", "weekly", "never"
        reference_date: Date to calculate period for (default: now)

    Returns:
        Period key string (e.g., "2026-01" for monthly)
    """
    if reference_date is None:
        reference_date = datetime.now(timezone.utc)

    if period_type == "monthly":
        return reference_date.strftime("%Y-%m")
    elif period_type == "daily":
        return reference_date.strftime("%Y-%m-%d")
    elif period_type == "weekly":
        # ISO week
        return reference_date.strftime("%Y-W%V")
    elif period_type == "never":
        return "lifetime"
    else:
        # Default to monthly
        return reference_date.strftime("%Y-%m")


def get_next_reset_date(period_type: str, reference_date: datetime = None) -> Optional[datetime]:
    """
    Get the next reset date for a period type.

    Args:
        period_type: "monthly", "daily", "weekly", "never"
        reference_date: Date to calculate from (default: now)

    Returns:
        Next reset datetime, or None for "never"
    """
    if reference_date is None:
        reference_date = datetime.now(timezone.utc)

    if period_type == "monthly":
        # First of next month
        if reference_date.month == 12:
            return datetime(reference_date.year + 1, 1, 1, tzinfo=timezone.utc)
        return datetime(reference_date.year, reference_date.month + 1, 1, tzinfo=timezone.utc)

    elif period_type == "daily":
        # Start of next day
        return (reference_date + timedelta(days=1)).replace(
            hour=0, minute=0, second=0, microsecond=0
        )

    elif period_type == "weekly":
        # Start of next week (Monday)
        days_until_monday = (7 - reference_date.weekday()) % 7 or 7
        return (reference_date + timedelta(days=days_until_monday)).replace(
            hour=0, minute=0, second=0, microsecond=0
        )

    elif period_type == "never":
        return None

    return None


async def get_usage(
    user_id: str,
    plugin: str,
    limit_key: str,
    period_type: str = "monthly",
    limit_value: int = 0
) -> Dict[str, Any]:
    """
    Get current usage for a limit, with lazy period reset.

    If the period has rolled over since last use, resets the counter.

    Args:
        user_id: User identifier
        plugin: Plugin name
        limit_key: Limit key (e.g., "videos_per_month")
        period_type: Reset period type
        limit_value: Current limit value (for snapshot)

    Returns:
        {
            "allowed": int,      # Current limit
            "used": int,         # Used this period
            "remaining": int,    # Remaining this period
            "period": str,       # Current period key
            "resets_at": str     # ISO datetime of next reset
        }
    """
    collection = get_usage_collection()
    if collection is None:
        # No collection = unlimited (OSS mode)
        return {
            "allowed": -1,
            "used": 0,
            "remaining": -1,
            "period": "unlimited",
            "resets_at": None
        }

    current_period = get_period_key(period_type)
    next_reset = get_next_reset_date(period_type)

    # Find existing usage record
    query = {
        "app_id": APP_ID,
        "user_id": user_id,
        "plugin": plugin,
        "limit_key": limit_key
    }

    usage = await collection.find_one(query)

    now = datetime.now(timezone.utc)

    if not usage or usage.get("period") != current_period:
        # Period rolled over or first use - emit reset event if needed
        if usage and usage.get("period") != current_period:
            # Import here to avoid circular import
            from .events import emit_period_reset_event
            await emit_period_reset_event(
                user_id=user_id,
                plugin=plugin,
                limit_key=limit_key,
                previous_period=usage.get("period"),
                new_period=current_period,
                previous_used=usage.get("used", 0)
            )

        # Upsert new period record
        await collection.update_one(
            query,
            {
                "$set": {
                    "period": current_period,
                    "period_type": period_type,
                    "used": 0,
                    "limit": limit_value,
                    "updated_at": now.isoformat()
                },
                "$setOnInsert": {
                    "app_id": APP_ID,
                    "user_id": user_id,
                    "plugin": plugin,
                    "limit_key": limit_key,
                    "first_use": None
                }
            },
            upsert=True
        )

        return {
            "allowed": limit_value,
            "used": 0,
            "remaining": limit_value,
            "period": current_period,
            "resets_at": next_reset.isoformat() if next_reset else None
        }

    # Return current usage
    used = usage.get("used", 0)
    allowed = usage.get("limit", limit_value)

    return {
        "allowed": allowed,
        "used": used,
        "remaining": max(0, allowed - used),
        "period": current_period,
        "resets_at": next_reset.isoformat() if next_reset else None
    }


async def get_all_usage(
    user_id: str,
    plugin: str
) -> Dict[str, Dict[str, Any]]:
    """
    Get all usage data for a user/plugin combination.

    Args:
        user_id: User identifier
        plugin: Plugin name

    Returns:
        Dict of limit_key -> usage data
    """
    collection = get_usage_collection()
    if collection is None:
        return {}

    query = {
        "app_id": APP_ID,
        "user_id": user_id,
        "plugin": plugin
    }

    cursor = collection.find(query)
    result = {}

    async for usage in cursor:
        limit_key = usage.get("limit_key")
        if limit_key:
            result[limit_key] = {
                "allowed": usage.get("limit", 0),
                "used": usage.get("used", 0),
                "remaining": max(0, usage.get("limit", 0) - usage.get("used", 0)),
                "period": usage.get("period"),
                "period_type": usage.get("period_type")
            }

    return result


async def check_limit(
    user_id: str,
    plugin: str,
    limit_key: str,
    needed: int = 1,
    period_type: str = "monthly",
    limit_value: int = 0
) -> Tuple[bool, int]:
    """
    Check if user has enough of a limit.

    Args:
        user_id: User identifier
        plugin: Plugin name
        limit_key: Limit key
        needed: Amount needed
        period_type: Reset period type
        limit_value: Current limit value

    Returns:
        (can_proceed, remaining) tuple
    """
    usage = await get_usage(user_id, plugin, limit_key, period_type, limit_value)

    remaining = usage.get("remaining", float("inf"))

    # -1 = unlimited
    if remaining == -1:
        return True, -1

    can_proceed = remaining >= needed
    return can_proceed, remaining


async def consume_limit(
    user_id: str,
    plugin: str,
    limit_key: str,
    amount: int = 1,
    period_type: str = "monthly",
    limit_value: int = 0
) -> int:
    """
    Consume from a limit.

    Args:
        user_id: User identifier
        plugin: Plugin name
        limit_key: Limit key
        amount: Amount to consume
        period_type: Reset period type
        limit_value: Current limit value

    Returns:
        New remaining amount after consumption

    Raises:
        ValueError: If insufficient remaining
    """
    collection = get_usage_collection()
    if collection is None:
        # No collection = unlimited, return -1 to indicate unlimited
        return -1

    # First ensure we have current period record
    current_usage = await get_usage(user_id, plugin, limit_key, period_type, limit_value)

    if current_usage["remaining"] != -1 and current_usage["remaining"] < amount:
        raise ValueError(
            f"Insufficient {limit_key}: need {amount}, have {current_usage['remaining']}"
        )

    now = datetime.now(timezone.utc)
    current_period = get_period_key(period_type)

    # Atomic increment
    result = await collection.find_one_and_update(
        {
            "app_id": APP_ID,
            "user_id": user_id,
            "plugin": plugin,
            "limit_key": limit_key,
            "period": current_period
        },
        {
            "$inc": {"used": amount},
            "$set": {
                "last_use": now.isoformat(),
                "updated_at": now.isoformat()
            },
            "$setOnInsert": {
                "first_use": now.isoformat()
            }
        },
        upsert=True,
        return_document=True  # Return updated document
    )

    new_used = result.get("used", amount)
    allowed = result.get("limit", limit_value)
    new_remaining = max(0, allowed - new_used)

    # Emit consumed event
    from .events import emit_consumed_event
    await emit_consumed_event(
        user_id=user_id,
        plugin=plugin,
        limit_key=limit_key,
        amount=amount,
        used=new_used,
        remaining=new_remaining,
        period=current_period
    )

    return new_remaining


async def reset_usage_for_period(
    user_id: str,
    plugin: str,
    limit_key: str,
    new_period: str,
    new_limit: int
) -> None:
    """
    Manually reset usage for a new period.

    This is called when period rolls over.

    Args:
        user_id: User identifier
        plugin: Plugin name
        limit_key: Limit key
        new_period: New period key
        new_limit: New limit value
    """
    collection = get_usage_collection()
    if collection is None:
        return

    now = datetime.now(timezone.utc)

    await collection.update_one(
        {
            "app_id": APP_ID,
            "user_id": user_id,
            "plugin": plugin,
            "limit_key": limit_key
        },
        {
            "$set": {
                "period": new_period,
                "used": 0,
                "limit": new_limit,
                "first_use": None,
                "last_use": None,
                "updated_at": now.isoformat()
            }
        },
        upsert=True
    )


async def set_usage(
    user_id: str,
    plugin: str,
    limit_key: str,
    used: int,
    period_type: str = "monthly",
    limit_value: int = 0
) -> None:
    """
    Set usage to a specific value (admin/sync use).

    Args:
        user_id: User identifier
        plugin: Plugin name
        limit_key: Limit key
        used: New used value
        period_type: Period type
        limit_value: Limit value
    """
    collection = get_usage_collection()
    if collection is None:
        return

    now = datetime.now(timezone.utc)
    current_period = get_period_key(period_type)

    await collection.update_one(
        {
            "app_id": APP_ID,
            "user_id": user_id,
            "plugin": plugin,
            "limit_key": limit_key
        },
        {
            "$set": {
                "period": current_period,
                "period_type": period_type,
                "used": used,
                "limit": limit_value,
                "updated_at": now.isoformat()
            }
        },
        upsert=True
    )
