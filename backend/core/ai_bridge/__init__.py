"""
AI Runtime Bridge

This module bridges mozaiks-core's plugin/notification system with 
the MozaiksAI runtime (mozaiksai/) for chat and workflow execution.

Architecture:
- mozaiks-core owns: notifications, plugins, settings, subscription gating
- mozaiksai owns: chat streaming, workflow execution, agent orchestration

The bridge provides:
1. Unified auth - reuse core's JWT validation for AI runtime
2. Event forwarding - AI events can trigger core notifications
3. Subscription gating - check entitlements before workflow execution
4. WebSocket routing - direct chat traffic to runtime, notifications to core
"""

from .runtime_proxy import AIRuntimeProxy, get_ai_runtime
from .auth_bridge import bridge_auth_to_runtime, validate_runtime_access
from .event_bridge import AIEventBridge, get_event_bridge, AIEventType
from .websocket_bridge import WebSocketBridge, get_websocket_bridge

__all__ = [
    "AIRuntimeProxy",
    "get_ai_runtime",
    "bridge_auth_to_runtime",
    "validate_runtime_access",
    "AIEventBridge",
    "get_event_bridge",
    "AIEventType",
    "WebSocketBridge",
    "get_websocket_bridge",
]
