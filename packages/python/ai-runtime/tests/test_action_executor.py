import sys
from pathlib import Path

# Ensure local package root is importable when running pytest directly.
ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = Path(__file__).resolve().parents[4]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))
infra_root = REPO_ROOT / "packages" / "python" / "infrastructure"
if str(infra_root) not in sys.path:
    sys.path.insert(0, str(infra_root))

import pytest

from mozaiks_ai.runtime.action_executor import ActionExecutionError, execute_action
from mozaiks_ai.runtime.action_registry import clear_action_tools, register_action_tool


@pytest.mark.asyncio
async def test_execute_action_sync_uses_context():
    clear_action_tools()

    def sample_tool(foo, context_variables=None, chat_id=None):
        return {
            "foo": foo,
            "chat_id": chat_id,
            "app_id": context_variables.get("app_id") if context_variables else None,
        }

    register_action_tool("test.sync", sample_tool, overwrite=True)
    result = await execute_action(
        "test.sync",
        {"foo": 42},
        {"chat_id": "chat_1", "app_id": "app_1", "user_id": "user_1"},
        workflow_name="wf",
    )
    assert result["foo"] == 42
    assert result["chat_id"] == "chat_1"
    assert result["app_id"] == "app_1"


@pytest.mark.asyncio
async def test_execute_action_async_returns_result():
    clear_action_tools()

    async def sample_async(bar, context_variables=None):
        return {"bar": bar, "user_id": context_variables.get("user_id")}

    register_action_tool("test.async", sample_async, overwrite=True)
    result = await execute_action(
        "test.async",
        {"bar": "ok"},
        {"chat_id": "chat_2", "app_id": "app_2", "user_id": "user_2"},
    )
    assert result["bar"] == "ok"
    assert result["user_id"] == "user_2"


@pytest.mark.asyncio
async def test_execute_action_missing_tool():
    clear_action_tools()
    with pytest.raises(ActionExecutionError):
        await execute_action("missing.tool", {}, {"chat_id": "chat_3"})
