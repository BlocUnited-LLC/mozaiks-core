"""
AI Runtime Proxy

Provides a facade for mozaiks-core to interact with the MozaiksAI runtime
without tight coupling. This allows the runtime to be swapped or upgraded
independently.
"""

import logging
import os
from typing import Optional, Dict, Any, List

logger = logging.getLogger("mozaiks_core.ai_bridge.runtime_proxy")

# Lazy import to avoid circular dependencies
_runtime_instance: Optional["AIRuntimeProxy"] = None


class AIRuntimeProxy:
    """
    Proxy for interacting with the MozaiksAI runtime.
    
    This class abstracts the runtime so mozaiks-core doesn't need to know
    implementation details of workflow execution, chat sessions, etc.
    """
    
    def __init__(self):
        self._initialized = False
        self._workflow_manager = None
        self._persistence_manager = None
        
    def initialize(self) -> bool:
        """Initialize connection to the AI runtime."""
        if self._initialized:
            return True
            
        try:
            # Import from the mozaiksai namespace
            from mozaiks_ai.runtime.workflow.workflow_manager import workflow_manager
            from mozaiks_ai.runtime.data.persistence import AG2PersistenceManager
            
            self._workflow_manager = workflow_manager
            self._persistence_manager = AG2PersistenceManager()
            self._initialized = True
            logger.info("AI Runtime proxy initialized successfully")
            return True
            
        except ImportError as e:
            logger.warning(f"AI Runtime not available: {e}")
            return False
        except Exception as e:
            logger.error(f"Failed to initialize AI Runtime: {e}")
            return False
    
    @property
    def is_available(self) -> bool:
        """Check if the AI runtime is available."""
        return self._initialized
    
    def list_workflows(self) -> List[str]:
        """Get list of available workflow names."""
        if not self._initialized:
            return []
        try:
            return self._workflow_manager.get_all_workflow_names()
        except Exception as e:
            logger.error(f"Failed to list workflows: {e}")
            return []
    
    def get_workflow_info(self, workflow_name: str) -> Optional[Dict[str, Any]]:
        """Get metadata about a specific workflow."""
        if not self._initialized:
            return None
        try:
            return self._workflow_manager.get_workflow_info(workflow_name)
        except Exception as e:
            logger.error(f"Failed to get workflow info: {e}")
            return None
    
    async def list_user_sessions(
        self, 
        app_id: str, 
        user_id: str, 
        workflow_name: Optional[str] = None
    ) -> List[Dict[str, Any]]:
        """Get chat sessions for a user."""
        if not self._initialized:
            return []
        try:
            return await self._persistence_manager.list_sessions(
                app_id=app_id,
                user_id=user_id,
                workflow_name=workflow_name
            )
        except Exception as e:
            logger.error(f"Failed to list sessions: {e}")
            return []
    
    def get_websocket_url(
        self,
        workflow_name: str,
        app_id: str,
        chat_id: str,
        user_id: str,
        base_url: Optional[str] = None
    ) -> str:
        """
        Build the WebSocket URL for connecting to the AI runtime.
        
        The AI runtime handles chat at:
        /ws/{workflow_name}/{app_id}/{chat_id}/{user_id}
        """
        base = base_url or os.getenv("MOZAIKSAI_RUNTIME_URL", "")
        if not base:
            # Same-origin fallback
            base = ""
        return f"{base}/ws/{workflow_name}/{app_id}/{chat_id}/{user_id}"
    
    def get_http_base_url(self) -> str:
        """Get the base URL for AI runtime HTTP endpoints."""
        return os.getenv("MOZAIKSAI_RUNTIME_URL", "")


def get_ai_runtime() -> AIRuntimeProxy:
    """Get the singleton AI runtime proxy instance."""
    global _runtime_instance
    if _runtime_instance is None:
        _runtime_instance = AIRuntimeProxy()
        _runtime_instance.initialize()
    return _runtime_instance
