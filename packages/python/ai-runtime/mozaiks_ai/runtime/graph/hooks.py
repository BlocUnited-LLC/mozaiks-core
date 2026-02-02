"""
Graph Injection Hooks
=====================
Before-turn and after-event hooks for graph-based context injection and mutation.
"""

import json
import logging
import re
from typing import Any, Dict, List, Optional

from .loader import (
    GraphInjectionConfig,
    GraphInjectionLoader,
    InjectionRule,
    MutationRule,
    QueryConfig,
)
from .client import FalkorDBClient, QueryResult, get_falkordb_client

logger = logging.getLogger(__name__)


class ParameterResolver:
    """
    Resolves parameter values from context, event data, and workflow metadata.
    
    Supports:
    - $context.foo       -> context_variables["foo"]
    - $context.foo.bar   -> context_variables["foo"]["bar"]
    - $event.field       -> event_data["field"]
    - $workflow.name     -> workflow metadata
    - "literal"          -> literal string
    - 123                -> literal number
    """
    
    PARAM_PATTERN = re.compile(r"^\$(\w+)\.(.+)$")
    
    def __init__(
        self,
        context: Dict[str, Any],
        event_data: Optional[Dict[str, Any]] = None,
        workflow_metadata: Optional[Dict[str, Any]] = None
    ):
        self.context = context or {}
        self.event_data = event_data or {}
        self.workflow_metadata = workflow_metadata or {}
    
    def resolve(self, value: Any) -> Any:
        """
        Resolve a parameter value.
        
        Args:
            value: The parameter value (may be a resolution expression)
            
        Returns:
            Resolved value
        """
        if not isinstance(value, str):
            return value
        
        if not value.startswith("$"):
            return value
        
        match = self.PARAM_PATTERN.match(value)
        if not match:
            logger.warning(f"Invalid parameter expression: {value}")
            return None
        
        source_name = match.group(1)
        path = match.group(2)
        
        if source_name == "context":
            return self._get_nested(self.context, path)
        elif source_name == "event":
            return self._get_nested(self.event_data, path)
        elif source_name == "workflow":
            return self._get_nested(self.workflow_metadata, path)
        else:
            logger.warning(f"Unknown parameter source: {source_name}")
            return None
    
    def resolve_params(self, params: Dict[str, Any]) -> Dict[str, Any]:
        """Resolve all parameters in a dict."""
        return {k: self.resolve(v) for k, v in params.items()}
    
    def _get_nested(self, data: Dict[str, Any], path: str) -> Any:
        """Get a nested value from a dict using dot notation."""
        parts = path.split(".")
        current = data
        for part in parts:
            if isinstance(current, dict):
                current = current.get(part)
            else:
                return None
            if current is None:
                return None
        return current


class ResultFormatter:
    """Formats query results according to the specified format."""
    
    @staticmethod
    def format(result: QueryResult, format_type: str, max_results: Optional[int] = None) -> Any:
        """
        Format query results.
        
        Args:
            result: Query result to format
            format_type: One of "list", "single", "json", "markdown"
            max_results: Optional limit on results
            
        Returns:
            Formatted result
        """
        rows = result.rows
        if max_results:
            rows = rows[:max_results]
        
        if format_type == "single":
            return rows[0] if rows else None
        elif format_type == "json":
            return json.dumps(rows, indent=2, default=str)
        elif format_type == "markdown":
            return ResultFormatter._to_markdown(rows)
        else:  # list (default)
            return rows
    
    @staticmethod
    def _to_markdown(rows: List[Dict[str, Any]]) -> str:
        """Convert result rows to markdown list."""
        if not rows:
            return "_No results_"
        
        lines = []
        for row in rows:
            if len(row) == 1:
                # Single column: just the value
                lines.append(f"- {list(row.values())[0]}")
            else:
                # Multiple columns: key-value pairs
                parts = [f"**{k}**: {v}" for k, v in row.items()]
                lines.append(f"- {', '.join(parts)}")
        return "\n".join(lines)


class ConditionEvaluator:
    """Evaluates condition expressions for rules."""
    
    @staticmethod
    def evaluate(
        condition: Optional[str],
        context: Dict[str, Any],
        event_data: Optional[Dict[str, Any]] = None
    ) -> bool:
        """
        Evaluate a condition expression.
        
        Supports simple equality checks like:
        - "$context.phase == 'planning'"
        - "$event.success == true"
        
        Args:
            condition: Condition expression or None
            context: Context variables
            event_data: Event data
            
        Returns:
            True if condition is met or no condition
        """
        if not condition:
            return True
        
        try:
            # Parse simple equality: $source.path == value
            match = re.match(
                r"^\$(\w+)\.(\S+)\s*==\s*(.+)$",
                condition.strip()
            )
            if not match:
                logger.warning(f"Unsupported condition syntax: {condition}")
                return True  # Default to allowing
            
            source_name = match.group(1)
            path = match.group(2)
            expected_raw = match.group(3).strip()
            
            # Resolve the actual value
            resolver = ParameterResolver(context, event_data)
            actual = resolver.resolve(f"${source_name}.{path}")
            
            # Parse expected value
            expected = ConditionEvaluator._parse_value(expected_raw)
            
            return actual == expected
            
        except Exception as e:
            logger.warning(f"Condition evaluation failed: {e}")
            return True  # Default to allowing on error
    
    @staticmethod
    def _parse_value(raw: str) -> Any:
        """Parse a literal value from condition."""
        raw = raw.strip()
        
        # String literals
        if (raw.startswith("'") and raw.endswith("'")) or \
           (raw.startswith('"') and raw.endswith('"')):
            return raw[1:-1]
        
        # Boolean
        if raw.lower() == "true":
            return True
        if raw.lower() == "false":
            return False
        
        # Number
        try:
            if "." in raw:
                return float(raw)
            return int(raw)
        except ValueError:
            pass
        
        return raw


class GraphInjectionHooks:
    """
    Before-turn and after-event hooks for graph injection.
    
    Integrates with the workflow orchestration engine to provide:
    - Pre-turn context injection from graph queries
    - Post-event graph mutations for learning
    
    Usage:
        hooks = GraphInjectionHooks(workflow_path)
        
        # Before agent turn
        injections = await hooks.before_agent_turn("PatternAgent", context_vars)
        # Add injections to agent's system message
        
        # After event
        await hooks.on_event("agent.turn_complete", context_vars, event_data, "PatternAgent")
    """
    
    def __init__(
        self,
        workflow_path: str,
        client: Optional[FalkorDBClient] = None,
        app_id: Optional[str] = None
    ):
        """
        Initialize graph injection hooks.
        
        Args:
            workflow_path: Path to the workflow directory
            client: Optional FalkorDB client (uses singleton if not provided)
            app_id: Optional app ID for multi-tenant graph isolation
        """
        self.workflow_path = workflow_path
        self.client = client or get_falkordb_client()
        self.app_id = app_id
        self.loader = GraphInjectionLoader()
        self._config: Optional[GraphInjectionConfig] = None
        self._loaded = False
    
    @property
    def config(self) -> Optional[GraphInjectionConfig]:
        """Lazy-load configuration."""
        if not self._loaded:
            self._config = self.loader.load(self.workflow_path)
            self._loaded = True
        return self._config
    
    @property
    def enabled(self) -> bool:
        """Check if graph injection is enabled for this workflow."""
        return self.config is not None and self.client.available
    
    async def before_agent_turn(
        self,
        agent_name: str,
        context: Dict[str, Any],
        workflow_metadata: Optional[Dict[str, Any]] = None
    ) -> Dict[str, Any]:
        """
        Execute injection queries before an agent turn.
        
        Args:
            agent_name: Name of the agent about to process
            context: Current context variables
            workflow_metadata: Optional workflow metadata (name, chat_id, etc.)
            
        Returns:
            Dict of injection_name -> formatted_result
        """
        if not self.enabled:
            return {}
        
        # Config guaranteed non-None due to enabled check
        config = self.config
        assert config is not None
        
        injections: Dict[str, Any] = {}
        rules = self.loader.get_injection_rules(config, agent_name)
        
        resolver = ParameterResolver(context, None, workflow_metadata)
        
        for rule in rules:
            # Check condition
            if not ConditionEvaluator.evaluate(rule.condition, context):
                logger.debug(f"Skipping rule '{rule.name}': condition not met")
                continue
            
            for query_cfg in rule.queries:
                try:
                    result = await self._execute_injection_query(query_cfg, resolver)
                    if result is not None and query_cfg.inject_as:
                        formatted = ResultFormatter.format(
                            result,
                            query_cfg.format,
                            query_cfg.max_results
                        )
                        injections[query_cfg.inject_as] = formatted
                        logger.debug(
                            f"Injected '{query_cfg.inject_as}' for {agent_name}: "
                            f"{len(result.rows)} results"
                        )
                except Exception as e:
                    logger.warning(f"Injection query '{query_cfg.id}' failed: {e}")
        
        return injections
    
    async def on_event(
        self,
        event: str,
        context: Dict[str, Any],
        event_data: Dict[str, Any],
        agent_name: Optional[str] = None,
        workflow_metadata: Optional[Dict[str, Any]] = None
    ) -> None:
        """
        Execute mutation queries after a lifecycle event.
        
        Args:
            event: Event type (e.g., "agent.turn_complete")
            context: Current context variables
            event_data: Event payload
            agent_name: Optional agent name for filtering
            workflow_metadata: Optional workflow metadata
        """
        if not self.enabled:
            return
        
        # Config guaranteed non-None due to enabled check
        config = self.config
        assert config is not None
        
        rules = self.loader.get_mutation_rules(config, event, agent_name)
        
        resolver = ParameterResolver(context, event_data, workflow_metadata)
        
        for rule in rules:
            # Check condition
            if not ConditionEvaluator.evaluate(rule.condition, context, event_data):
                logger.debug(f"Skipping mutation rule '{rule.name}': condition not met")
                continue
            
            for mutation in rule.mutations:
                try:
                    await self._execute_mutation(mutation, resolver)
                    logger.debug(f"Executed mutation '{mutation.id}' for event '{event}'")
                except Exception as e:
                    logger.warning(f"Mutation '{mutation.id}' failed: {e}")
    
    async def _execute_injection_query(
        self,
        query_cfg: QueryConfig,
        resolver: ParameterResolver
    ) -> Optional[QueryResult]:
        """Execute a single injection query."""
        params = resolver.resolve_params(query_cfg.params)
        
        # Filter out None values from params
        params = {k: v for k, v in params.items() if v is not None}
        
        return await self.client.query(
            cypher=query_cfg.cypher,
            params=params,
            app_id=self.app_id
        )
    
    async def _execute_mutation(
        self,
        mutation,
        resolver: ParameterResolver
    ) -> bool:
        """Execute a single mutation."""
        params = resolver.resolve_params(mutation.params)
        
        # Filter out None values from params
        params = {k: v for k, v in params.items() if v is not None}
        
        return await self.client.execute(
            cypher=mutation.cypher,
            params=params,
            app_id=self.app_id
        )
    
    def build_injection_prompt(self, injections: Dict[str, Any]) -> str:
        """
        Build a prompt section from injections.
        
        Args:
            injections: Dict of injection_name -> formatted_result
            
        Returns:
            Formatted string to append to system message
        """
        if not injections:
            return ""
        
        sections = []
        for name, content in injections.items():
            title = name.replace("_", " ").title()
            if isinstance(content, str):
                sections.append(f"## {title}\n{content}")
            elif isinstance(content, list):
                if content:
                    md = ResultFormatter._to_markdown([
                        c if isinstance(c, dict) else {"value": c}
                        for c in content
                    ])
                    sections.append(f"## {title}\n{md}")
            elif isinstance(content, dict):
                md = ResultFormatter._to_markdown([content])
                sections.append(f"## {title}\n{md}")
        
        if sections:
            return "\n\n---\n**Graph Context:**\n\n" + "\n\n".join(sections)
        return ""
