"""
Graph Injection Integration
===========================
Bridges the graph injection system with the orchestration engine.

This module provides the runtime integration layer that:
1. Initializes GraphInjectionHooks for workflows that have graph_injection.yaml
2. Integrates before_agent_turn injections into agent context
3. Triggers mutations on workflow events
"""

import logging
from typing import Any, Dict, Optional

from mozaiks_infra.logs.logging_config import get_workflow_logger

logger = logging.getLogger(__name__)


class GraphInjectionIntegration:
    """
    Orchestration-level integration for graph injection.
    
    This class is initialized per-workflow and provides methods called by
    the orchestration engine at appropriate lifecycle points.
    
    Usage in orchestration_patterns.py:
        graph_integration = GraphInjectionIntegration(workflow_name, workflow_path)
        
        # Before agent turn:
        injections = await graph_integration.inject_context(
            agent_name="PatternAgent",
            context=ag2_context,
            workflow_metadata={"workflow_name": ..., "chat_id": ...}
        )
        # Merge injections into agent's system message or context
        
        # After events:
        await graph_integration.handle_event(
            event="agent.turn_complete",
            context=ag2_context,
            event_data={"output": ..., "duration": ...},
            agent_name="PatternAgent"
        )
    """
    
    def __init__(
        self,
        workflow_name: str,
        workflow_path: str,
        app_id: Optional[str] = None
    ):
        """
        Initialize graph injection integration for a workflow.
        
        Args:
            workflow_name: Name of the workflow
            workflow_path: Full path to the workflow directory
            app_id: Optional app ID for multi-tenant graph isolation
        """
        self.workflow_name = workflow_name
        self.workflow_path = workflow_path
        self.app_id = app_id
        self._hooks = None
        self._initialized = False
        self._wf_logger = get_workflow_logger(workflow_name)
    
    def _ensure_initialized(self) -> bool:
        """Lazy initialization of hooks (only when first used)."""
        if self._initialized:
            return self._hooks is not None
        
        self._initialized = True
        
        try:
            from mozaiks_ai.runtime.graph import GraphInjectionHooks
            
            self._hooks = GraphInjectionHooks(
                workflow_path=self.workflow_path,
                app_id=self.app_id
            )
            
            if self._hooks.enabled:
                self._wf_logger.info(
                    f"[GRAPH] Graph injection enabled for workflow '{self.workflow_name}'"
                )
                return True
            else:
                self._wf_logger.debug(
                    f"[GRAPH] Graph injection not configured for '{self.workflow_name}' "
                    "(no graph_injection.yaml or FalkorDB unavailable)"
                )
                self._hooks = None
                return False
                
        except ImportError as e:
            self._wf_logger.debug(f"[GRAPH] Graph module not available: {e}")
            return False
        except Exception as e:
            self._wf_logger.warning(f"[GRAPH] Failed to initialize graph injection: {e}")
            return False
    
    @property
    def enabled(self) -> bool:
        """Check if graph injection is enabled and ready."""
        return self._ensure_initialized() and self._hooks is not None
    
    async def inject_context(
        self,
        agent_name: str,
        context: Dict[str, Any],
        workflow_metadata: Optional[Dict[str, Any]] = None
    ) -> Dict[str, Any]:
        """
        Execute injection queries and return context augmentation.
        
        This should be called before each agent turn to inject relevant
        graph data into the agent's context.
        
        Args:
            agent_name: Name of the agent about to process
            context: Current AG2 context variables (dict form)
            workflow_metadata: Workflow metadata (name, chat_id, etc.)
            
        Returns:
            Dict of injection_name -> formatted_result, empty if disabled
        """
        if not self._ensure_initialized() or not self._hooks:
            return {}
        
        try:
            injections = await self._hooks.before_agent_turn(
                agent_name=agent_name,
                context=context,
                workflow_metadata=workflow_metadata
            )
            
            if injections:
                self._wf_logger.debug(
                    f"[GRAPH] Injected {len(injections)} context items for {agent_name}: "
                    f"{list(injections.keys())}"
                )
            
            return injections
            
        except Exception as e:
            self._wf_logger.warning(
                f"[GRAPH] Context injection failed for {agent_name}: {e}"
            )
            return {}
    
    async def handle_event(
        self,
        event: str,
        context: Dict[str, Any],
        event_data: Dict[str, Any],
        agent_name: Optional[str] = None,
        workflow_metadata: Optional[Dict[str, Any]] = None
    ) -> None:
        """
        Handle a workflow event for potential graph mutations.
        
        This should be called after significant events (turn complete,
        tool calls, etc.) to allow the graph to be updated.
        
        Args:
            event: Event type (e.g., "agent.turn_complete")
            context: Current AG2 context variables
            event_data: Event-specific payload
            agent_name: Optional agent name for filtering
            workflow_metadata: Workflow metadata
        """
        if not self._ensure_initialized() or not self._hooks:
            return
        
        try:
            await self._hooks.on_event(
                event=event,
                context=context,
                event_data=event_data,
                agent_name=agent_name,
                workflow_metadata=workflow_metadata
            )
        except Exception as e:
            self._wf_logger.warning(
                f"[GRAPH] Event handling failed for {event}: {e}"
            )
    
    def build_prompt_injection(self, injections: Dict[str, Any]) -> str:
        """
        Convert injections dict to a prompt string.
        
        Args:
            injections: Dict from inject_context()
            
        Returns:
            Formatted string to append to system message
        """
        if not self._hooks or not injections:
            return ""
        
        return self._hooks.build_injection_prompt(injections)


# Module-level factory for getting integration instances
_integration_cache: Dict[str, GraphInjectionIntegration] = {}


def get_graph_integration(
    workflow_name: str,
    workflow_path: str,
    app_id: Optional[str] = None,
    use_cache: bool = True
) -> GraphInjectionIntegration:
    """
    Factory function to get a graph injection integration for a workflow.
    
    Args:
        workflow_name: Name of the workflow
        workflow_path: Path to workflow directory
        app_id: Optional app ID for multi-tenancy
        use_cache: Whether to cache and reuse integration instances
        
    Returns:
        GraphInjectionIntegration instance
    """
    cache_key = f"{workflow_name}:{app_id or 'default'}"
    
    if use_cache and cache_key in _integration_cache:
        return _integration_cache[cache_key]
    
    integration = GraphInjectionIntegration(
        workflow_name=workflow_name,
        workflow_path=workflow_path,
        app_id=app_id
    )
    
    if use_cache:
        _integration_cache[cache_key] = integration
    
    return integration


def clear_graph_integration_cache() -> None:
    """Clear the integration cache (useful for testing or config reload)."""
    _integration_cache.clear()


__all__ = [
    "GraphInjectionIntegration",
    "get_graph_integration",
    "clear_graph_integration_cache",
]
