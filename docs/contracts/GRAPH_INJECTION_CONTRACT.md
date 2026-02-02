# Graph Injection Runtime Contract

**Version:** 1.0.0  
**Status:** Active  
**Last Updated:** 2026-02-01

---

## Overview

Graph Injection enables **dynamic context injection** for AI agents using a graph database (FalkorDB). Each workflow can declaratively specify:

1. **Injection Rules** - What to query from the graph before each agent turn
2. **Mutation Rules** - What to write to the graph after lifecycle events

This creates **stateful agents in a stateless system** - agents gain memory and pattern recognition without hardcoding knowledge paths in Python.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         Workflow Bundle                         │
├─────────────────────────────────────────────────────────────────┤
│  agents.yaml  │  tools.yaml  │  graph_injection.yaml           │
└───────────────┴──────────────┴──────────────────────────────────┘
                                        │
                                        ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Graph Injection Loader                       │
│  - Loads YAML at workflow start                                 │
│  - Resolves `extends` inheritance                               │
│  - Validates schema                                             │
└─────────────────────────────────────────────────────────────────┘
                                        │
                    ┌───────────────────┴───────────────────┐
                    ▼                                       ▼
        ┌───────────────────┐                   ┌───────────────────┐
        │  Before-Turn Hook │                   │  After-Event Hook │
        │  (Injection)      │                   │  (Mutation)       │
        └─────────┬─────────┘                   └─────────┬─────────┘
                  │                                       │
                  ▼                                       ▼
        ┌───────────────────────────────────────────────────────────┐
        │                      FalkorDB                             │
        │  - Graph storage for patterns, entities, relationships    │
        │  - Multi-tenant via graph namespacing                     │
        └───────────────────────────────────────────────────────────┘
```

---

## YAML Schema

### File Location

```
workflows/
  <WorkflowName>/
    agents.yaml
    tools.yaml
    graph_injection.yaml    # <-- This file
```

### Full Schema

```yaml
# graph_injection.yaml

# Optional: inherit rules from a base config
extends: "../_shared/graph_injection_base.yaml"

# Schema version for future compatibility
version: "1.0"

# Injection rules: executed BEFORE agent turns
injection_rules:
  - name: "rule_name"                    # Human-readable identifier
    agents: ["AgentA", "AgentB"]         # Which agents receive injection
    condition: "$context.phase == 'planning'"  # Optional: when to apply
    queries:
      - id: "query_id"                   # Unique within rule
        cypher: |
          MATCH (n:NodeType {prop: $param})
          RETURN n.field AS result
        params:
          param: "$context.some_value"   # Parameter resolution
        inject_as: "injection_name"      # Name in agent's context
        format: "list"                   # list | single | json | markdown
        max_results: 10                  # Optional: limit results

# Mutation rules: executed AFTER lifecycle events
mutation_rules:
  - name: "rule_name"
    events: ["agent.turn_complete", "workflow.phase_complete"]
    agents: ["AgentA"]                   # Optional: filter by agent
    condition: "$event.success == true"  # Optional: when to apply
    mutations:
      - id: "mutation_id"
        cypher: |
          MERGE (n:NodeType {id: $id})
          SET n.updated = datetime()
        params:
          id: "$context.entity_id"
```

---

## Parameter Resolution

Parameters in `params` blocks support these formats:

| Format | Resolves To | Example |
|--------|-------------|---------|
| `$context.foo` | Context variable value | `$context.user_id` → `"user_123"` |
| `$context.foo.bar` | Nested context value | `$context.app.name` → `"MyApp"` |
| `$event.field` | Event payload field | `$event.agent_name` → `"PatternAgent"` |
| `$workflow.name` | Workflow metadata | `$workflow.name` → `"AgentGenerator"` |
| `$workflow.chat_id` | Current chat ID | `$workflow.chat_id` → `"chat_abc123"` |
| `"literal"` | Literal string | `"active"` → `"active"` |
| `123` | Literal number | `123` → `123` |

### Nested Resolution

```yaml
params:
  user_id: "$context.user.id"           # Resolves user.id from context
  phase: "$context.current_phase"       # Simple field
  timestamp: "$event.timestamp"         # From event payload
```

---

## Injection Rules

### Execution Flow

1. **Before each agent turn**, the runtime:
   - Finds matching injection rules (by agent name)
   - Evaluates conditions (if present)
   - Executes Cypher queries with resolved parameters
   - Formats results according to `format` spec
   - Injects into agent's system message context

### Format Options

| Format | Description | Output |
|--------|-------------|--------|
| `list` | Array of result rows | `[{name: "A"}, {name: "B"}]` |
| `single` | First result only | `{name: "A"}` |
| `json` | Pretty-printed JSON string | `"[{\"name\": \"A\"}]"` |
| `markdown` | Markdown-formatted list | `"- A\n- B"` |

### Example

```yaml
injection_rules:
  - name: "inject_successful_patterns"
    agents: ["PatternAgent"]
    queries:
      - id: "proven_patterns"
        cypher: |
          MATCH (p:Pattern)-[:USED_IN]->(a:App)
          WHERE a.success_rate > 0.8
          RETURN p.name, p.description, a.success_rate
          ORDER BY a.success_rate DESC
          LIMIT 5
        params: {}
        inject_as: "successful_patterns"
        format: "markdown"
```

**Injected into PatternAgent's context:**
```
## Successful Patterns
- **CRM Pattern**: Customer management workflow (92% success)
- **Legal Brief**: Document generation (89% success)
```

---

## Mutation Rules

### Supported Events

| Event | Trigger | Available Data |
|-------|---------|----------------|
| `agent.turn_start` | Before agent processes | `agent_name`, `turn_number` |
| `agent.turn_complete` | After agent responds | `agent_name`, `response`, `success` |
| `workflow.phase_complete` | Phase transition | `phase_name`, `next_phase` |
| `workflow.complete` | Workflow finished | `status`, `duration`, `outputs` |
| `workflow.error` | Error occurred | `error_type`, `error_message` |
| `tool.call_complete` | Tool execution done | `tool_name`, `result`, `success` |

### Example

```yaml
mutation_rules:
  - name: "track_pattern_usage"
    events: ["agent.turn_complete"]
    agents: ["PatternAgent"]
    condition: "$event.success == true"
    mutations:
      - id: "record_pattern"
        cypher: |
          MERGE (p:Pattern {name: $pattern_name})
          MERGE (j:Journey {id: $chat_id})
          MERGE (p)-[r:SELECTED_IN]->(j)
          SET r.timestamp = datetime(),
              r.context = $context_summary
        params:
          pattern_name: "$context.selected_pattern"
          chat_id: "$workflow.chat_id"
          context_summary: "$context.brief_summary"
```

---

## Inheritance (extends)

Workflows can inherit from base configurations:

```yaml
# _shared/graph_injection_base.yaml
version: "1.0"
injection_rules:
  - name: "common_user_context"
    agents: ["*"]  # All agents
    queries:
      - id: "user_history"
        cypher: |
          MATCH (u:User {id: $user_id})-[:COMPLETED]->(w:Workflow)
          RETURN w.name, w.completed_at
          ORDER BY w.completed_at DESC
          LIMIT 3
        params:
          user_id: "$context.user_id"
        inject_as: "recent_workflows"
        format: "list"

mutation_rules:
  - name: "track_journey"
    events: ["workflow.complete"]
    mutations:
      - id: "complete_journey"
        cypher: |
          MATCH (j:Journey {id: $chat_id})
          SET j.status = 'COMPLETE',
              j.completed_at = datetime()
        params:
          chat_id: "$workflow.chat_id"
```

```yaml
# AgentGenerator/graph_injection.yaml
extends: "../_shared/graph_injection_base.yaml"

injection_rules:
  # Inherits common_user_context from base
  - name: "generator_specific"
    agents: ["BlueprintAgent"]
    queries:
      - id: "similar_apps"
        cypher: |
          MATCH (a:App {category: $category})
          RETURN a.name, a.architecture
        params:
          category: "$context.app_category"
        inject_as: "reference_apps"
        format: "json"
```

**Merge behavior:**
- Rules with same `name` are replaced (child overrides parent)
- Rules with different `name` are combined
- Child rules are processed after parent rules

---

## FalkorDB Graph Schema

### Core Node Types

```cypher
// User and identity
(:User {id, email, created_at})

// Workflow execution tracking
(:Journey {id, workflow_name, app_id, user_id, status, started_at, completed_at})
(:Phase {name, journey_id, started_at, completed_at, status})

// Knowledge and patterns
(:Pattern {name, description, category, created_at})
(:Entity {id, type, name, properties})
(:Tool {name, version, schema})

// Generated artifacts
(:App {id, name, category, architecture, success_rate, created_at})
(:Workflow {name, version, agents, tools})
```

### Core Relationships

```cypher
(:User)-[:STARTED]->(:Journey)
(:Journey)-[:HAS_PHASE]->(:Phase)
(:Journey)-[:SELECTED]->(:Pattern)
(:Pattern)-[:USED_IN]->(:App)
(:App)-[:GENERATED_BY]->(:Workflow)
(:Entity)-[:RELATED_TO]->(:Entity)
```

---

## Runtime Integration

### Loader Interface

```python
class GraphInjectionLoader:
    """Loads and validates graph_injection.yaml for a workflow."""
    
    def load(self, workflow_path: Path) -> Optional[GraphInjectionConfig]:
        """Load config, resolving extends inheritance."""
        ...
    
    def get_injection_rules(self, agent_name: str) -> List[InjectionRule]:
        """Get rules applicable to a specific agent."""
        ...
    
    def get_mutation_rules(self, event: str, agent_name: str = None) -> List[MutationRule]:
        """Get mutation rules for an event, optionally filtered by agent."""
        ...
```

### Hook Interfaces

```python
class GraphInjectionHooks:
    """Before-turn and after-event hooks for graph injection."""
    
    async def before_agent_turn(
        self, 
        agent_name: str, 
        context: Dict[str, Any]
    ) -> Dict[str, Any]:
        """Execute injection queries, return injected context."""
        ...
    
    async def on_event(
        self,
        event: str,
        context: Dict[str, Any],
        event_data: Dict[str, Any],
        agent_name: str = None
    ) -> None:
        """Execute mutation queries for the event."""
        ...
```

---

## Multi-Tenancy

Graph isolation is achieved via **graph namespacing**:

```python
# Each app_id gets its own graph namespace
graph_name = f"mozaiks_{app_id}"
graph = falkordb.select_graph(graph_name)
```

This ensures:
- Complete data isolation between tenants
- Independent schema evolution per tenant
- Easy per-tenant backup/restore

---

## Error Handling

| Error | Behavior | Recovery |
|-------|----------|----------|
| YAML parse error | Workflow fails to start | Fix YAML syntax |
| Cypher syntax error | Query skipped, logged | Fix Cypher in YAML |
| Missing parameter | Query skipped, warned | Add to context |
| FalkorDB unavailable | Queries skipped, workflow continues | Graceful degradation |
| Timeout (>5s) | Query aborted, logged | Optimize Cypher |

**Graceful Degradation:** Graph injection is non-blocking. If FalkorDB is unavailable, workflows continue without graph context.

---

## Performance Guidelines

1. **Index key properties** used in MATCH clauses
2. **Limit result sets** with `max_results` or Cypher `LIMIT`
3. **Use parameters** instead of string interpolation
4. **Batch mutations** when possible (multiple SETs in one query)
5. **Cache injection results** within a session (same query, same params)

---

## Changelog

### v1.0.0 (2026-02-01)
- Initial contract definition
- Injection and mutation rule schemas
- Parameter resolution specification
- FalkorDB integration pattern
- Multi-tenancy via graph namespacing

---

## Coordination Status

### ✅ Core Implementation Complete (2026-02-01)

| Component | Location |
|-----------|----------|
| Python Module | `mozaiks_ai.runtime.graph` |
| Loader | `loader.py` - YAML parsing + Pydantic validation |
| Client | `client.py` - Async FalkorDB with graceful degradation |
| Hooks | `hooks.py` - Before-turn injection + after-event mutation |
| Integration | `integration.py` - Orchestration-level API |
| Infrastructure | `docker-compose.yml` - FalkorDB on port 6380 |

### ✅ Platform Integration Confirmed (2026-02-01)![alt text](image.png)

**Confirmed Decisions:**

1. **File Location:** `graph_injection.yaml` lives in workflow directories (not centralized)
2. **Wiring:** Opt-in, Platform controls orchestration integration
3. **Inheritance:** Workflows extend `_shared/graph_injection_base.yaml`

**Platform Status:**
- Integration layer created (`graph_integration.py`)
- Hook factories ready (`create_graph_injection_hook()`, `create_graph_mutation_hook()`)
- AgentGenerator outputs `graph_injection.yaml` for every generated app
- Wiring into `orchestration_patterns.py` in progress

**Expected Full Integration:** 1 week from 2026-02-01
