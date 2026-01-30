from __future__ import annotations

import logging
from typing import Any, Dict, Optional

from mozaiks_infra.logs.logging_config import get_core_logger

logger = get_core_logger("usage_accounting")


def _extract_int(payload: Dict[str, Any], key: str) -> int:
    try:
        return max(0, int(payload.get(key) or 0))
    except Exception:
        return 0


def _extract_str(payload: Dict[str, Any], key: str) -> str:
    value = payload.get(key)
    if value is None:
        return ""
    return str(value).strip()


async def handle_usage_delta(payload: Dict[str, Any]) -> None:
    """Update local token usage and optionally report to Platform.

    This handler is best-effort and must never block runtime execution.
    """
    if not isinstance(payload, dict):
        return

    app_id = _extract_str(payload, "app_id")
    user_id = _extract_str(payload, "user_id")
    if not app_id or not user_id:
        return

    total_tokens = _extract_int(payload, "total_tokens")
    if total_tokens <= 0:
        return

    # Local tracking for self-hosted enforcement.
    try:
        from mozaiks_platform.billing.token_budget import record_token_usage

        await record_token_usage(app_id, total_tokens)
    except Exception as exc:
        logger.debug("usage_delta_local_record_failed", extra={"error": str(exc)})

    # Optional Platform reporting (batching handled internally).
    try:
        from mozaiks_platform.billing.usage_reporter import get_usage_reporter

        reporter = get_usage_reporter()
        try:
            stats = reporter.stats
            if stats.get("enabled") and not stats.get("running"):
                await reporter.start()
        except Exception:
            pass
        model_name = _extract_str(payload, "model_name") or "unknown"
        workflow_name = _extract_str(payload, "workflow_name") or None
        prompt_tokens = _extract_int(payload, "prompt_tokens")
        completion_tokens = _extract_int(payload, "completion_tokens")

        await reporter.report_token_usage(
            app_id=app_id,
            user_id=user_id,
            model=model_name,
            input_tokens=prompt_tokens,
            output_tokens=completion_tokens,
            workflow_id=workflow_name,
            invocation_id=_extract_str(payload, "invocation_id") or None,
        )
    except Exception as exc:
        logger.debug("usage_delta_platform_report_failed", extra={"error": str(exc)})
