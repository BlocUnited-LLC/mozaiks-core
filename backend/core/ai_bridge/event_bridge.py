"""
Event Bridge

Bridges AI runtime events to mozaiks-core's notification and event systems.
This allows workflow completions, errors, and milestones to trigger
core notifications and plugin actions.
"""

import logging
from typing import Optional, Dict, Any, Callable, List
from enum import Enum

logger = logging.getLogger("mozaiks_core.ai_bridge.event_bridge")


class AIEventType(str, Enum):
    """AI runtime event types that core cares about."""
    WORKFLOW_STARTED = "workflow_started"
    WORKFLOW_COMPLETED = "workflow_completed"
    WORKFLOW_FAILED = "workflow_failed"
    CHAT_MESSAGE = "chat_message"
    TOOL_EXECUTED = "tool_executed"
    ARTIFACT_CREATED = "artifact_created"
    USER_INPUT_REQUIRED = "user_input_required"


class AIEventBridge:
    """
    Bridges AI runtime events to mozaiks-core systems.
    
    When the AI runtime emits events (workflow complete, chat message, etc.),
    this bridge routes them to:
    - Notification system (for user alerts)
    - Event bus (for plugin reactions)
    - Analytics (for usage tracking)
    """
    
    def __init__(self):
        self._handlers: Dict[AIEventType, List[Callable]] = {
            event_type: [] for event_type in AIEventType
        }
        self._notification_manager = None
        self._event_bus = None
        
    def initialize(self, notification_manager=None, event_bus=None):
        """Initialize with core system references."""
        self._notification_manager = notification_manager
        self._event_bus = event_bus
        logger.info("AI Event Bridge initialized")
    
    def register_handler(
        self, 
        event_type: AIEventType, 
        handler: Callable[[Dict[str, Any]], None]
    ):
        """Register a handler for a specific AI event type."""
        self._handlers[event_type].append(handler)
    
    async def handle_event(self, event_type: str, payload: Dict[str, Any]):
        """
        Handle an event from the AI runtime.
        
        This is called by the runtime (or a webhook) when events occur.
        """
        try:
            evt = AIEventType(event_type)
        except ValueError:
            logger.debug(f"Unknown AI event type: {event_type}")
            return
            
        # Call registered handlers
        for handler in self._handlers[evt]:
            try:
                await handler(payload)
            except Exception as e:
                logger.error(f"Handler error for {event_type}: {e}")
        
        # Route to core systems based on event type
        await self._route_to_core_systems(evt, payload)
    
    async def _route_to_core_systems(self, event_type: AIEventType, payload: Dict[str, Any]):
        """Route events to appropriate core systems."""
        
        user_id = payload.get("user_id")
        app_id = payload.get("app_id")
        
        if event_type == AIEventType.WORKFLOW_COMPLETED:
            await self._notify_workflow_complete(user_id, app_id, payload)
            
        elif event_type == AIEventType.WORKFLOW_FAILED:
            await self._notify_workflow_failed(user_id, app_id, payload)
            
        elif event_type == AIEventType.ARTIFACT_CREATED:
            await self._publish_artifact_event(user_id, app_id, payload)
    
    async def _notify_workflow_complete(
        self, 
        user_id: str, 
        app_id: str, 
        payload: Dict[str, Any]
    ):
        """Send notification when a workflow completes."""
        if not self._notification_manager:
            return
            
        workflow_name = payload.get("workflow_name", "Workflow")
        try:
            await self._notification_manager.send_notification(
                user_id=user_id,
                title=f"{workflow_name} Complete",
                message=f"Your {workflow_name} workflow has completed successfully.",
                notification_type="ai_workflow_complete",
                data={
                    "workflow_name": workflow_name,
                    "chat_id": payload.get("chat_id"),
                    "app_id": app_id,
                }
            )
        except Exception as e:
            logger.error(f"Failed to send workflow complete notification: {e}")
    
    async def _notify_workflow_failed(
        self, 
        user_id: str, 
        app_id: str, 
        payload: Dict[str, Any]
    ):
        """Send notification when a workflow fails."""
        if not self._notification_manager:
            return
            
        workflow_name = payload.get("workflow_name", "Workflow")
        error = payload.get("error", "Unknown error")
        
        try:
            await self._notification_manager.send_notification(
                user_id=user_id,
                title=f"{workflow_name} Failed",
                message=f"Your {workflow_name} workflow encountered an error: {error}",
                notification_type="ai_workflow_failed",
                data={
                    "workflow_name": workflow_name,
                    "chat_id": payload.get("chat_id"),
                    "error": error,
                    "app_id": app_id,
                }
            )
        except Exception as e:
            logger.error(f"Failed to send workflow failed notification: {e}")
    
    async def _publish_artifact_event(
        self, 
        user_id: str, 
        app_id: str, 
        payload: Dict[str, Any]
    ):
        """Publish artifact creation to event bus for plugins."""
        if not self._event_bus:
            return
            
        try:
            await self._event_bus.publish(
                event_type="ai.artifact.created",
                data={
                    "user_id": user_id,
                    "app_id": app_id,
                    "artifact_type": payload.get("artifact_type"),
                    "artifact_id": payload.get("artifact_id"),
                    "workflow_name": payload.get("workflow_name"),
                }
            )
        except Exception as e:
            logger.error(f"Failed to publish artifact event: {e}")


# Singleton instance
_event_bridge: Optional[AIEventBridge] = None


def get_event_bridge() -> AIEventBridge:
    """Get the singleton event bridge instance."""
    global _event_bridge
    if _event_bridge is None:
        _event_bridge = AIEventBridge()
    return _event_bridge
