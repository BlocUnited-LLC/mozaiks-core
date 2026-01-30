from __future__ import annotations

import os
import uuid
from dataclasses import dataclass
from datetime import datetime, UTC
from typing import Any, Dict, List, Optional

from mozaiks_infra.logs.logging_config import get_core_logger

logger = get_core_logger("agui_adapter")


def is_agui_enabled() -> bool:
    """Return True if AG-UI dual-emission is enabled (default: enabled)."""
    raw = os.getenv("MOZAIKS_AGUI_ENABLED")
    if raw is None:
        return True
    return raw.strip().lower() in {"1", "true", "yes", "y", "on"}


@dataclass
class _TextStreamState:
    message_id: str


class AGUIEventAdapter:
    """Adapter that maps chat.* envelopes to agui.* envelopes (additive)."""

    def __init__(self) -> None:
        self._text_streams: Dict[str, _TextStreamState] = {}

    def _now_iso(self) -> str:
        return datetime.now(UTC).isoformat()

    def _build_envelope(self, event_type: str, data: Dict[str, Any], timestamp: Optional[str]) -> Dict[str, Any]:
        return {
            "type": event_type,
            "data": data,
            "timestamp": timestamp or self._now_iso(),
        }

    def _base_payload(
        self,
        raw_payload: Any,
        *,
        run_id: Optional[str],
        thread_id: Optional[str],
    ) -> Dict[str, Any]:
        payload: Dict[str, Any] = {}
        if isinstance(raw_payload, dict):
            payload.update(raw_payload)
        if run_id and "runId" not in payload:
            payload["runId"] = run_id
        if thread_id and "threadId" not in payload:
            payload["threadId"] = thread_id
        return payload

    def _resolve_run_id(self, payload: Any, chat_id: Optional[str]) -> Optional[str]:
        if isinstance(payload, dict):
            for key in ("runId", "run_id"):
                value = payload.get(key)
                if isinstance(value, str) and value:
                    return value
        return chat_id

    def _resolve_thread_id(self, app_id: Optional[str], chat_id: Optional[str]) -> Optional[str]:
        if app_id and chat_id:
            return f"{app_id}:{chat_id}"
        if chat_id:
            return chat_id
        return None

    def _get_or_create_text_stream(self, chat_id: str) -> tuple[_TextStreamState, bool]:
        state = self._text_streams.get(chat_id)
        if state:
            return state, False
        state = _TextStreamState(message_id=str(uuid.uuid4()))
        self._text_streams[chat_id] = state
        return state, True

    def _close_text_stream(self, chat_id: str) -> Optional[_TextStreamState]:
        return self._text_streams.pop(chat_id, None)

    def build_agui_events(
        self,
        envelope: Dict[str, Any],
        *,
        chat_id: Optional[str],
        app_id: Optional[str],
        workflow_name: Optional[str] = None,
    ) -> List[Dict[str, Any]]:
        if not isinstance(envelope, dict):
            return []
        event_type = envelope.get("type")
        if not isinstance(event_type, str):
            return []
        if event_type.startswith("agui."):
            return []

        payload = envelope.get("data")
        timestamp = envelope.get("timestamp")
        run_id = self._resolve_run_id(payload, chat_id)
        thread_id = self._resolve_thread_id(app_id, chat_id)

        def _payload_with_meta(base: Any) -> Dict[str, Any]:
            return self._base_payload(base, run_id=run_id, thread_id=thread_id)

        events: List[Dict[str, Any]] = []

        # Lifecycle events
        lifecycle_map = {
            "chat.orchestration.run_started": "agui.lifecycle.RunStarted",
            "chat.orchestration.run_completed": "agui.lifecycle.RunFinished",
            "chat.orchestration.run_failed": "agui.lifecycle.RunError",
            "chat.orchestration.agent_started": "agui.lifecycle.StepStarted",
            "chat.orchestration.agent_completed": "agui.lifecycle.StepFinished",
        }
        if event_type in lifecycle_map:
            events.append(self._build_envelope(lifecycle_map[event_type], _payload_with_meta(payload), timestamp))
            return events

        # Text streaming events
        if event_type == "chat.print" and chat_id:
            stream, created = self._get_or_create_text_stream(chat_id)
            if created:
                start_payload = _payload_with_meta({
                    "messageId": stream.message_id,
                    "agent": payload.get("agent") if isinstance(payload, dict) else None,
                })
                events.append(self._build_envelope("agui.text.TextMessageStart", start_payload, timestamp))
            content_payload = _payload_with_meta({
                "messageId": stream.message_id,
                "agent": payload.get("agent") if isinstance(payload, dict) else None,
                "content": payload.get("content") if isinstance(payload, dict) else None,
            })
            events.append(self._build_envelope("agui.text.TextMessageContent", content_payload, timestamp))
            return events

        if event_type == "chat.text" and chat_id:
            stream = self._text_streams.get(chat_id)
            if stream is None:
                stream, _ = self._get_or_create_text_stream(chat_id)
                start_payload = _payload_with_meta({
                    "messageId": stream.message_id,
                    "agent": payload.get("agent") if isinstance(payload, dict) else None,
                })
                content_payload = _payload_with_meta({
                    "messageId": stream.message_id,
                    "agent": payload.get("agent") if isinstance(payload, dict) else None,
                    "content": payload.get("content") if isinstance(payload, dict) else None,
                })
                events.append(self._build_envelope("agui.text.TextMessageStart", start_payload, timestamp))
                events.append(self._build_envelope("agui.text.TextMessageContent", content_payload, timestamp))
            end_payload = _payload_with_meta({
                "messageId": stream.message_id,
                "agent": payload.get("agent") if isinstance(payload, dict) else None,
            })
            events.append(self._build_envelope("agui.text.TextMessageEnd", end_payload, timestamp))
            self._close_text_stream(chat_id)
            return events

        # Tool call events
        if event_type == "chat.tool_call":
            call_id = None
            tool_name = None
            if isinstance(payload, dict):
                call_id = payload.get("call_id") or payload.get("callId") or payload.get("id")
                tool_name = payload.get("name") or payload.get("tool_name") or payload.get("tool")
            tool_payload = _payload_with_meta(payload)
            if call_id and "callId" not in tool_payload:
                tool_payload["callId"] = call_id
            if tool_name and "tool" not in tool_payload:
                tool_payload["tool"] = tool_name
            events.append(self._build_envelope("agui.tool.ToolCallStart", tool_payload, timestamp))
            return events

        if event_type == "chat.tool_response":
            call_id = None
            tool_name = None
            if isinstance(payload, dict):
                call_id = payload.get("call_id") or payload.get("callId") or payload.get("id")
                tool_name = payload.get("name") or payload.get("tool_name") or payload.get("tool")
            end_payload = _payload_with_meta(payload)
            if call_id and "callId" not in end_payload:
                end_payload["callId"] = call_id
            if tool_name and "tool" not in end_payload:
                end_payload["tool"] = tool_name
            events.append(self._build_envelope("agui.tool.ToolCallEnd", dict(end_payload), timestamp))
            events.append(self._build_envelope("agui.tool.ToolCallResult", dict(end_payload), timestamp))
            return events

        return events


_global_adapter: Optional[AGUIEventAdapter] = None


def get_agui_adapter() -> AGUIEventAdapter:
    global _global_adapter
    if _global_adapter is None:
        _global_adapter = AGUIEventAdapter()
    return _global_adapter
