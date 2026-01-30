"""Action tool registry for artifact-driven tool calls.

This is intentionally lightweight and runtime-agnostic:
- No platform dependencies
- In-memory registry for explicit tool registration
- Optional fallback to workflow tool lookup handled by action_executor
"""

from __future__ import annotations

from typing import Callable, Dict, Optional

from mozaiks_infra.logs.logging_config import get_core_logger

logger = get_core_logger("action_registry")

_ACTION_TOOL_REGISTRY: Dict[str, Callable[..., object]] = {}


def register_action_tool(name: str, func: Callable[..., object], *, overwrite: bool = False) -> None:
    """Register a callable for artifact action execution."""
    if not isinstance(name, str) or not name.strip():
        raise ValueError("Action tool name must be a non-empty string")
    if not callable(func):
        raise ValueError("Action tool function must be callable")
    key = name.strip()
    if key in _ACTION_TOOL_REGISTRY and not overwrite:
        logger.warning("Action tool already registered: %s (overwrite=False)", key)
        return
    _ACTION_TOOL_REGISTRY[key] = func
    logger.info("Registered action tool: %s", key)


def get_action_tool(name: str) -> Optional[Callable[..., object]]:
    """Retrieve a registered action tool by name."""
    if not isinstance(name, str):
        return None
    return _ACTION_TOOL_REGISTRY.get(name.strip())


def list_action_tools() -> Dict[str, Callable[..., object]]:
    """Return a shallow copy of the action tool registry."""
    return dict(_ACTION_TOOL_REGISTRY)


def clear_action_tools() -> None:
    """Clear all registered action tools (mainly for tests)."""
    _ACTION_TOOL_REGISTRY.clear()
    logger.debug("Cleared action tool registry")


__all__ = [
    "register_action_tool",
    "get_action_tool",
    "list_action_tools",
    "clear_action_tools",
]
