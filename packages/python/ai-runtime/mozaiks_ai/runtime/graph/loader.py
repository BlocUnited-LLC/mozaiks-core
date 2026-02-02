"""
Graph Injection Configuration Loader
=====================================
Loads, validates, and merges graph_injection.yaml configurations.
"""

from pathlib import Path
from typing import Any, Dict, List, Optional, Union
import logging
import yaml
from pydantic import BaseModel, Field, field_validator

logger = logging.getLogger(__name__)


class QueryConfig(BaseModel):
    """Configuration for a single Cypher query."""
    id: str = Field(..., description="Unique identifier for this query")
    cypher: str = Field(..., description="Cypher query to execute")
    params: Dict[str, Any] = Field(default_factory=dict, description="Query parameters with resolution syntax")
    inject_as: Optional[str] = Field(None, description="Name to inject results under (injection queries only)")
    format: str = Field("list", description="Output format: list, single, json, markdown")
    max_results: Optional[int] = Field(None, description="Maximum results to return")

    @field_validator("format")
    @classmethod
    def validate_format(cls, v: str) -> str:
        allowed = {"list", "single", "json", "markdown"}
        if v not in allowed:
            raise ValueError(f"format must be one of {allowed}, got {v}")
        return v


class MutationConfig(BaseModel):
    """Configuration for a single mutation query."""
    id: str = Field(..., description="Unique identifier for this mutation")
    cypher: str = Field(..., description="Cypher mutation to execute")
    params: Dict[str, Any] = Field(default_factory=dict, description="Mutation parameters")


class InjectionRule(BaseModel):
    """Rule for injecting graph data before agent turns."""
    name: str = Field(..., description="Human-readable rule name")
    agents: List[str] = Field(..., description="Agent names this rule applies to, or ['*'] for all")
    condition: Optional[str] = Field(None, description="Optional condition expression")
    queries: List[QueryConfig] = Field(default_factory=list, description="Queries to execute")


class MutationRule(BaseModel):
    """Rule for mutating graph data after events."""
    name: str = Field(..., description="Human-readable rule name")
    events: List[str] = Field(..., description="Event types that trigger this rule")
    agents: Optional[List[str]] = Field(None, description="Optional agent filter")
    condition: Optional[str] = Field(None, description="Optional condition expression")
    mutations: List[MutationConfig] = Field(default_factory=list, description="Mutations to execute")


class GraphInjectionConfig(BaseModel):
    """Complete graph injection configuration for a workflow."""
    version: str = Field("1.0", description="Schema version")
    extends: Optional[str] = Field(None, description="Base config to inherit from")
    injection_rules: List[InjectionRule] = Field(default_factory=list, description="Injection rules")
    mutation_rules: List[MutationRule] = Field(default_factory=list, description="Mutation rules")


class GraphInjectionLoader:
    """
    Loads and validates graph_injection.yaml configurations.
    
    Features:
    - YAML loading with schema validation via Pydantic
    - Inheritance via `extends` directive
    - Rule merging (child overrides parent by name)
    """

    def __init__(self):
        self._cache: Dict[str, GraphInjectionConfig] = {}

    def load(self, workflow_path: Union[str, Path]) -> Optional[GraphInjectionConfig]:
        """
        Load graph_injection.yaml from a workflow directory.
        
        Args:
            workflow_path: Path to the workflow directory
            
        Returns:
            GraphInjectionConfig if file exists and is valid, None otherwise
        """
        workflow_path = Path(workflow_path)
        yaml_path = workflow_path / "graph_injection.yaml"
        
        if not yaml_path.exists():
            logger.debug(f"No graph_injection.yaml found at {yaml_path}")
            return None
        
        cache_key = str(yaml_path.resolve())
        if cache_key in self._cache:
            return self._cache[cache_key]
        
        try:
            raw_config = yaml.safe_load(yaml_path.read_text(encoding="utf-8"))
            if raw_config is None:
                logger.warning(f"Empty graph_injection.yaml at {yaml_path}")
                return None
            
            # Handle inheritance
            if "extends" in raw_config and raw_config["extends"]:
                base_path = (yaml_path.parent / raw_config["extends"]).resolve()
                base_config = self._load_base(base_path)
                if base_config:
                    raw_config = self._merge_configs(base_config, raw_config)
            
            config = GraphInjectionConfig(**raw_config)
            self._cache[cache_key] = config
            
            logger.info(
                f"Loaded graph_injection.yaml: {len(config.injection_rules)} injection rules, "
                f"{len(config.mutation_rules)} mutation rules"
            )
            return config
            
        except yaml.YAMLError as e:
            logger.error(f"YAML parse error in {yaml_path}: {e}")
            return None
        except Exception as e:
            logger.error(f"Failed to load graph_injection.yaml from {yaml_path}: {e}")
            return None

    def _load_base(self, base_path: Path) -> Optional[Dict[str, Any]]:
        """Load a base configuration file for inheritance."""
        if not base_path.exists():
            logger.warning(f"Base config not found: {base_path}")
            return None
        
        try:
            return yaml.safe_load(base_path.read_text(encoding="utf-8"))
        except Exception as e:
            logger.error(f"Failed to load base config {base_path}: {e}")
            return None

    def _merge_configs(self, base: Dict[str, Any], child: Dict[str, Any]) -> Dict[str, Any]:
        """
        Merge child config into base config.
        
        Rules with the same name are replaced; different names are combined.
        """
        merged = {
            "version": child.get("version", base.get("version", "1.0")),
            "injection_rules": [],
            "mutation_rules": [],
        }
        
        # Merge injection rules
        base_injection = {r["name"]: r for r in base.get("injection_rules", [])}
        for rule in child.get("injection_rules", []):
            base_injection[rule["name"]] = rule  # Override or add
        merged["injection_rules"] = list(base_injection.values())
        
        # Merge mutation rules
        base_mutation = {r["name"]: r for r in base.get("mutation_rules", [])}
        for rule in child.get("mutation_rules", []):
            base_mutation[rule["name"]] = rule  # Override or add
        merged["mutation_rules"] = list(base_mutation.values())
        
        return merged

    def get_injection_rules(
        self, 
        config: GraphInjectionConfig, 
        agent_name: str
    ) -> List[InjectionRule]:
        """
        Get injection rules applicable to a specific agent.
        
        Args:
            config: The loaded configuration
            agent_name: Name of the agent
            
        Returns:
            List of matching injection rules
        """
        matching = []
        for rule in config.injection_rules:
            if "*" in rule.agents or agent_name in rule.agents:
                matching.append(rule)
        return matching

    def get_mutation_rules(
        self,
        config: GraphInjectionConfig,
        event: str,
        agent_name: Optional[str] = None
    ) -> List[MutationRule]:
        """
        Get mutation rules for an event, optionally filtered by agent.
        
        Args:
            config: The loaded configuration
            event: Event type (e.g., "agent.turn_complete")
            agent_name: Optional agent name filter
            
        Returns:
            List of matching mutation rules
        """
        matching = []
        for rule in config.mutation_rules:
            if event not in rule.events:
                continue
            if rule.agents is not None and agent_name is not None:
                if agent_name not in rule.agents and "*" not in rule.agents:
                    continue
            matching.append(rule)
        return matching

    def clear_cache(self) -> None:
        """Clear the configuration cache."""
        self._cache.clear()
