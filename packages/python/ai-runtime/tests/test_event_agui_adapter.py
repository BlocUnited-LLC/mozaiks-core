import os
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

from mozaiks_ai.runtime.event_agui_adapter import AGUIEventAdapter
from mozaiks_ai.runtime.transport.simple_transport import SimpleTransport


def _make_envelope(event_type, data=None):
    return {"type": event_type, "data": data or {}, "timestamp": "2026-01-29T00:00:00Z"}


def test_text_streaming_start_content_end():
    adapter = AGUIEventAdapter()
    print_env = _make_envelope("chat.print", {"content": "Hello", "agent": "Agent"})
    events = adapter.build_agui_events(print_env, chat_id="chat_1", app_id="app_1")
    assert [e["type"] for e in events] == [
        "agui.text.TextMessageStart",
        "agui.text.TextMessageContent",
    ]
    message_id = events[0]["data"]["messageId"]
    assert events[1]["data"]["messageId"] == message_id

    text_env = _make_envelope("chat.text", {"content": "Hello", "agent": "Agent"})
    end_events = adapter.build_agui_events(text_env, chat_id="chat_1", app_id="app_1")
    assert [e["type"] for e in end_events] == ["agui.text.TextMessageEnd"]
    assert end_events[0]["data"]["messageId"] == message_id


def test_text_non_streaming_emits_full_cycle():
    adapter = AGUIEventAdapter()
    text_env = _make_envelope("chat.text", {"content": "Hi", "agent": "Agent"})
    events = adapter.build_agui_events(text_env, chat_id="chat_2", app_id="app_1")
    assert [e["type"] for e in events] == [
        "agui.text.TextMessageStart",
        "agui.text.TextMessageContent",
        "agui.text.TextMessageEnd",
    ]


def test_tool_response_emits_end_then_result():
    adapter = AGUIEventAdapter()
    tool_env = _make_envelope("chat.tool_response", {"call_id": "call_1", "name": "tool"})
    events = adapter.build_agui_events(tool_env, chat_id="chat_3", app_id="app_1")
    assert [e["type"] for e in events] == [
        "agui.tool.ToolCallEnd",
        "agui.tool.ToolCallResult",
    ]
    assert events[0]["data"]["callId"] == "call_1"


def test_lifecycle_event_has_thread_id():
    adapter = AGUIEventAdapter()
    env = _make_envelope("chat.orchestration.run_started", {"status": "running"})
    events = adapter.build_agui_events(env, chat_id="chat_4", app_id="app_2")
    assert events[0]["type"] == "agui.lifecycle.RunStarted"
    assert events[0]["data"]["threadId"] == "app_2:chat_4"


@pytest.mark.asyncio
async def test_transport_dual_emit_agui(monkeypatch):
    os.environ["MOZAIKS_AGUI_ENABLED"] = "true"
    transport = SimpleTransport()
    captured = []

    async def _capture(event_data, target_chat_id=None):
        captured.append(event_data)

    monkeypatch.setattr(transport, "_broadcast_to_websockets", _capture)
    transport.connections["chat_5"] = {"app_id": "app_5", "workflow_name": "wf"}

    await transport.send_event_to_ui({"kind": "print", "agent": "Agent", "content": "Hello"}, "chat_5")

    types = [evt.get("type") for evt in captured if isinstance(evt, dict)]
    assert "chat.print" in types
    assert "agui.text.TextMessageStart" in types
    assert "agui.text.TextMessageContent" in types
