# backend/core/analytics/event_logger.py
import logging
from core.event_bus import event_bus

logger = logging.getLogger("mozaiks_core.analytics.event_logger")

_initialized = False


def init_event_logging() -> None:
    """Idempotently initialize analytics event logging.

    MozaiksCore v2 public metrics intentionally exclude AI/plugin/revenue/growth metrics.
    Event persistence for those signals is disabled.
    """
    global _initialized
    if _initialized:
        return

    # Kept for compatibility: callers may still invoke this.
    # We intentionally do not subscribe/persist plugin/theme/subscription/AI analytics.
    event_bus  # referenced to avoid unused-import warnings if tooling is strict.

    _initialized = True
    logger.info("ℹ️ Analytics event logging disabled (user/activity KPIs only)")
