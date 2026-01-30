"""Artifact action executor (stateless tool invocation outside agent loop)."""

from __future__ import annotations

import inspect
import os
import uuid
import importlib.util
from pathlib import Path
from typing import Any, Callable, Dict, Optional

from mozaiks_infra.logs.logging_config import get_core_logger
from mozaiks_ai.runtime.action_registry import get_action_tool

try:
    import yaml  # type: ignore
except Exception:  # pragma: no cover
    yaml = None  # type: ignore

try:
    from mozaiks_ai.runtime.workflow.workflow_manager import WORKFLOWS_ROOT
except Exception:  # pragma: no cover
    WORKFLOWS_ROOT = None  # type: ignore

logger = get_core_logger("action_executor")


class ActionExecutionError(RuntimeError):
    """Raised when an artifact action tool cannot be resolved or executed."""


def _normalize_tool_name(tool_name: str) -> str:
    return tool_name.strip()


def _load_tool_from_workflow(workflow_name: Optional[str], tool_name: str) -> Optional[Callable[..., Any]]:
    if not workflow_name or WORKFLOWS_ROOT is None or yaml is None:
        return None

    workflow_dir = Path(WORKFLOWS_ROOT) / workflow_name
    tools_yaml_path = workflow_dir / "tools.yaml"
    if not tools_yaml_path.exists():
        return None

    try:
        data = yaml.safe_load(tools_yaml_path.read_text(encoding="utf-8")) or {}
    except Exception as exc:
        logger.warning("Failed to parse tools.yaml for %s: %s", workflow_name, exc)
        return None

    entries = data.get("tools", []) or []
    if not isinstance(entries, list):
        return None

    for entry in entries:
        if not isinstance(entry, dict):
            continue
        name = entry.get("name")
        func_name = entry.get("function")
        if name != tool_name and func_name != tool_name:
            continue
        file_name = entry.get("file")
        if not file_name or not func_name:
            continue

        # Resolve file path (root or tools/ subdir)
        candidates = [workflow_dir / file_name, workflow_dir / "tools" / file_name]
        file_path = next((p for p in candidates if p.exists()), None)
        if not file_path:
            continue

        module_name = f"mozaiks_action_{workflow_name}_{file_path.stem}_{uuid.uuid4().hex[:8]}"
        try:
            spec = importlib.util.spec_from_file_location(module_name, file_path)
            if not spec or not spec.loader:
                continue
            module = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(module)  # type: ignore[attr-defined]
        except Exception as exc:
            logger.warning("Failed to import action tool module %s: %s", file_path, exc)
            continue

        func = getattr(module, func_name, None)
        if callable(func):
            return func
    return None


def _build_tool_kwargs(
    func: Callable[..., Any],
    params: Optional[Dict[str, Any]],
    user_context: Dict[str, Any],
) -> Dict[str, Any]:
    payload = dict(params or {})

    try:
        sig = inspect.signature(func)
    except Exception:
        # If signature introspection fails, pass params only.
        return payload

    has_kwargs = any(p.kind == inspect.Parameter.VAR_KEYWORD for p in sig.parameters.values())

    # Preferred context injection
    if "context_variables" in sig.parameters or has_kwargs:
        payload.setdefault("context_variables", user_context)
    if "context" in sig.parameters or has_kwargs:
        payload.setdefault("context", user_context)
    if "user_context" in sig.parameters or has_kwargs:
        payload.setdefault("user_context", user_context)

    # Direct fields (override only if absent in params)
    for field in ("chat_id", "app_id", "user_id", "workflow_name", "artifact_id", "action_id"):
        if field in sig.parameters or has_kwargs:
            if field not in payload and field in user_context:
                payload[field] = user_context[field]

    return payload


async def execute_action(
    tool_name: str,
    params: Optional[Dict[str, Any]],
    user_context: Dict[str, Any],
    *,
    workflow_name: Optional[str] = None,
) -> Dict[str, Any]:
    """Execute an artifact action tool and return a structured result."""
    if not isinstance(tool_name, str) or not tool_name.strip():
        raise ActionExecutionError("Tool name is required")

    normalized = _normalize_tool_name(tool_name)
    func = get_action_tool(normalized)
    if not func:
        func = _load_tool_from_workflow(workflow_name, normalized)

    if not callable(func):
        raise ActionExecutionError(f"Action tool not found: {normalized}")

    kwargs = _build_tool_kwargs(func, params, user_context)

    logger.info(
        "Executing action tool: %s (chat_id=%s, app_id=%s, user_id=%s)",
        normalized,
        user_context.get("chat_id"),
        user_context.get("app_id"),
        user_context.get("user_id"),
    )

    try:
        if inspect.iscoroutinefunction(func):
            result = await func(**kwargs)
        else:
            result = func(**kwargs)
    except Exception as exc:
        logger.error("Action tool failed: %s (%s)", normalized, exc, exc_info=True)
        raise ActionExecutionError(str(exc)) from exc

    if isinstance(result, dict):
        return result
    return {"result": result}


__all__ = [
    "ActionExecutionError",
    "execute_action",
]
