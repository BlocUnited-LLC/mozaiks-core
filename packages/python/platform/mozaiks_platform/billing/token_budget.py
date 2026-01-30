from __future__ import annotations

import logging
from dataclasses import dataclass
from typing import Optional, Tuple

from .entitlements import get_entitlements, EnforcementMode
from .token_usage import get_token_usage_store, should_track_locally

logger = logging.getLogger("mozaiks_core.billing.token_budget")


@dataclass
class TokenBudgetState:
    app_id: str
    period: str
    limit: int
    used: int
    remaining: int
    enforcement: EnforcementMode
    source: str


def _normalize_tokens_needed(tokens_needed: int) -> int:
    try:
        return max(0, int(tokens_needed or 0))
    except Exception:
        return 0


async def _get_effective_used(app_id: str, period: str, source: str, fallback_used: int) -> int:
    if should_track_locally(source):
        store = get_token_usage_store()
        snap = await store.get_usage(app_id, period)
        return int(snap.used)
    return int(fallback_used or 0)


async def get_budget_state(app_id: str) -> TokenBudgetState:
    ent = get_entitlements(app_id)
    period = ent.token_budget.period or "monthly"
    used = await _get_effective_used(app_id, period, ent.source, ent.token_budget.used)
    limit = int(ent.token_budget.limit)
    remaining = -1 if limit < 0 else max(0, limit - used)
    return TokenBudgetState(
        app_id=app_id,
        period=period,
        limit=limit,
        used=used,
        remaining=remaining,
        enforcement=ent.token_budget.enforcement,
        source=ent.source,
    )


async def record_token_usage(app_id: str, total_tokens: int) -> Optional[TokenBudgetState]:
    ent = get_entitlements(app_id)
    period = ent.token_budget.period or "monthly"
    if not should_track_locally(ent.source):
        return None
    store = get_token_usage_store()
    await store.increment_usage(app_id, total_tokens, period)
    return await get_budget_state(app_id)


async def check_token_budget(app_id: str, tokens_needed: int = 0) -> Tuple[bool, str, TokenBudgetState]:
    state = await get_budget_state(app_id)
    needed = _normalize_tokens_needed(tokens_needed)

    if state.limit < 0 or state.enforcement == EnforcementMode.NONE:
        return True, "unlimited", state

    if state.remaining < 0:
        return True, "unlimited", state

    if needed > state.remaining or state.used >= state.limit:
        if state.enforcement == EnforcementMode.HARD:
            return False, "token_budget_exceeded", state
        if state.enforcement == EnforcementMode.WARN:
            logger.warning(
                "Token budget warning: app=%s used=%d limit=%d",
                state.app_id,
                state.used,
                state.limit,
            )
            return True, "token_budget_warn", state
        # SOFT: allow but mark overage
        logger.warning(
            "Token budget exceeded (soft): app=%s used=%d limit=%d",
            state.app_id,
            state.used,
            state.limit,
        )
        return True, "token_budget_soft", state

    return True, "ok", state

