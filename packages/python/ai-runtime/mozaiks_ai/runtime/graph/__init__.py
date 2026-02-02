"""
Graph Injection Module
======================
Provides declarative graph-based context injection and mutation for AI workflows.

This module enables "stateful agents in a stateless system" by allowing workflows
to declare what graph data to inject before agent turns and what mutations to
perform after lifecycle events.

Exports:
    - GraphInjectionLoader: Loads and validates graph_injection.yaml
    - GraphInjectionHooks: Before-turn and after-event hook implementations
    - FalkorDBClient: FalkorDB connection and query execution
    - GraphInjectionConfig: Pydantic model for configuration
    - GraphInjectionIntegration: Orchestration-level integration
    - get_graph_integration: Factory function for integration instances
"""

from .loader import GraphInjectionLoader, GraphInjectionConfig
from .client import FalkorDBClient
from .hooks import GraphInjectionHooks
from .integration import (
    GraphInjectionIntegration,
    get_graph_integration,
    clear_graph_integration_cache,
)
from .service import FalkorDBStartupService

__all__ = [
    "GraphInjectionLoader",
    "GraphInjectionConfig",
    "FalkorDBClient",
    "GraphInjectionHooks",
    "GraphInjectionIntegration",
    "get_graph_integration",
    "clear_graph_integration_cache",
    "FalkorDBStartupService",
]
