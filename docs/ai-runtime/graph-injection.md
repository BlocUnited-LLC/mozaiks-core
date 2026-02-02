# Graph Injection
> **Doc Status:** authoritative (new runtime capability)

**Purpose:** Enable declarative, graph-based context injection and mutation for AI workflows, allowing "stateful agents in a stateless system."

---

## Overview

Graph Injection provides a YAML-based declarative system for:

1. **Context Injection:** Query graph data and inject it into agent context before each turn
2. **Graph Mutation:** Update the graph based on workflow events (turn completion, tool calls, etc.)

This enables patterns like:
- Injecting user preferences, session context, or domain knowledge before agent turns
- Learning from agent outputs by creating/updating graph nodes
- Building cross-session memory and knowledge graphs
- Maintaining relational context (entities, relationships, hierarchies)

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    WORKFLOW EXECUTION                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│   ┌─────────────┐                                               │
│   │  BEFORE     │──────► Graph Queries ──────► Inject Context   │
│   │  TURN       │              │                     │          │
│   └─────────────┘              │                     ▼          │
│         │                      │            Agent System Message │
│         ▼                      │                                 │
│   ┌─────────────┐              │                                 │
│   │   AGENT     │              │                                 │
│   │   TURN      │◄─────────────┘                                │
│   └─────────────┘                                                │
│         │                                                        │
│         ▼                                                        │
│   ┌─────────────┐                                               │
│   │  AFTER      │──────► Graph Mutations ──────► Update Graph   │
│   │  EVENT      │                                               │
│   └─────────────┘                                               │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
                    ┌──────────────────┐
                    │    FalkorDB      │
                    │  (Graph Store)   │
                    │                  │
                    │  mozaiks_{app}   │
                    └──────────────────┘
```

---

## Quick Start

### 1. Add FalkorDB to your infrastructure

```yaml
# docker-compose.yml
services:
  falkordb:
    image: falkordb/falkordb:latest
    ports:
      - "6380:6379"
    volumes:
      - falkordb_data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 5s
      retries: 5

volumes:
  falkordb_data:
```

### 2. Create `graph_injection.yaml` in your workflow

```yaml
# workflows/MyWorkflow/graph_injection.yaml
version: "1.0"
schema: "mozaiks/graph_injection"

injections:
  - name: "session_context"
    agents: ["*"]  # All agents
    queries:
      - id: "user_preferences"
        cypher: |
          MATCH (u:User {id: $user_id})-[:PREFERS]->(p:Preference)
          RETURN p.name AS preference, p.value AS value
          LIMIT 10
        params:
          user_id: "$context.user_id"
        inject_as: "user_preferences"
        format: "list"

mutations:
  - name: "learn_from_output"
    on_event: "agent.turn_complete"
    agents: ["PatternAgent"]
    mutations:
      - id: "store_pattern"
        cypher: |
          MERGE (p:Pattern {name: $pattern_name})
          SET p.updated_at = datetime()
          SET p.confidence = $confidence
        params:
          pattern_name: "$event.pattern_name"
          confidence: "$event.confidence"
```

### 3. The runtime automatically loads and executes injections

No code changes required! The orchestration engine:
1. Detects `graph_injection.yaml` in the workflow directory
2. Executes injection queries before each agent turn
3. Formats results and adds them to the agent's context
4. Executes mutation queries after matching events

---

## Configuration Reference

### Top-Level Structure

```yaml
version: "1.0"                    # Required: Schema version
schema: "mozaiks/graph_injection" # Required: Schema identifier
inherits: "base_injection"        # Optional: Inherit from another config

injections: []   # Before-turn context injections
mutations: []    # After-event graph mutations
```

### Injection Rules

```yaml
injections:
  - name: "rule_name"          # Unique identifier
    agents:                    # Agent targeting
      - "AgentName"            # Specific agent
      - "*"                    # All agents
    condition: "$context.phase == 'planning'"  # Optional filter
    
    queries:
      - id: "query_id"         # Unique within rule
        cypher: |              # Cypher query
          MATCH (n:Node)
          WHERE n.id = $param
          RETURN n.value
        params:                # Parameters (resolved at runtime)
          param: "$context.some_value"
        inject_as: "context_key"  # Key in injected context
        format: "list"         # Output format: list, single, json, markdown
        max_results: 10        # Optional limit
```

### Mutation Rules

```yaml
mutations:
  - name: "rule_name"
    on_event: "agent.turn_complete"  # Event trigger
    agents: ["AgentName"]            # Optional agent filter
    condition: "$event.success == true"  # Optional condition
    
    mutations:
      - id: "mutation_id"
        cypher: |
          MERGE (n:Node {id: $id})
          SET n.value = $value
        params:
          id: "$context.entity_id"
          value: "$event.output"
```

---

## Parameter Resolution

Parameters in queries and mutations are resolved at runtime:

| Expression | Resolves To |
|------------|-------------|
| `$context.foo` | `context_variables["foo"]` |
| `$context.foo.bar` | `context_variables["foo"]["bar"]` |
| `$event.field` | Current event data `["field"]` |
| `$workflow.name` | Workflow metadata |
| `"literal"` | Literal string value |
| `123` | Literal number |

---

## Output Formats

| Format | Description | Use Case |
|--------|-------------|----------|
| `list` | Array of result objects | Multiple items |
| `single` | First result object only | Single lookup |
| `json` | Pretty-printed JSON string | Detailed context |
| `markdown` | Bulleted markdown list | Human-readable injection |

---

## Supported Events

Events that can trigger mutations:

| Event | Description | Event Data |
|-------|-------------|------------|
| `agent.turn_complete` | Agent finished turn | `output`, `duration_ms`, `agent_name` |
| `tool.call_complete` | Tool execution finished | `tool_name`, `result`, `success` |
| `workflow.complete` | Workflow finished | `final_output`, `turns`, `duration_ms` |
| `input.received` | User input received | `content`, `timestamp` |

---

## Multi-Tenancy

Graph data is isolated by `app_id`:

- Each app gets its own FalkorDB graph: `mozaiks_{app_id}`
- Queries and mutations are scoped to the app's graph
- No cross-app data leakage is possible

---

## Graceful Degradation

If FalkorDB is unavailable:
- Injection queries return empty results
- Mutations are skipped
- Workflow continues normally
- Warnings are logged

This ensures graph injection is an **enhancement**, not a dependency.

---

## Example: Cross-Session Memory

```yaml
# Inject conversation history from previous sessions
injections:
  - name: "conversation_memory"
    agents: ["*"]
    queries:
      - id: "recent_topics"
        cypher: |
          MATCH (u:User {id: $user_id})-[:DISCUSSED]->(t:Topic)
          WHERE t.timestamp > datetime() - duration('P7D')
          RETURN t.name AS topic, t.summary AS summary
          ORDER BY t.timestamp DESC
          LIMIT 5
        params:
          user_id: "$context.user_id"
        inject_as: "recent_topics"
        format: "markdown"

# Learn new topics from conversations
mutations:
  - name: "learn_topics"
    on_event: "workflow.complete"
    mutations:
      - id: "create_topic"
        cypher: |
          MATCH (u:User {id: $user_id})
          MERGE (t:Topic {name: $topic_name})
          SET t.summary = $summary
          SET t.timestamp = datetime()
          MERGE (u)-[:DISCUSSED]->(t)
        params:
          user_id: "$context.user_id"
          topic_name: "$event.detected_topic"
          summary: "$event.summary"
```

---

## See Also

- [Lifecycle Tools](lifecycle-tools.md) - Other hook points in the runtime
- [Persistence](persistence-and-resume.md) - Message and state persistence
- [Configuration Reference](configuration-reference.md) - Workflow configuration options
