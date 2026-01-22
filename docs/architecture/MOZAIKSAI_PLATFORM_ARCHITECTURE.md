# MozaiksAI Platform Architecture

> **Document Purpose**: Comprehensive definition of what MozaiksAI must become to support a production agentic platform.
> 
> **Last Updated**: January 17, 2026

---

## Table of Contents

1. [Core Purpose](#1-core-purpose)
2. [Agentic Runtime Design](#2-agentic-runtime-design)
3. [Output Formats](#3-output-formats)
4. [Validation Loop](#4-validation-loop)
5. [Relationship to Control-Plane](#5-relationship-to-control-plane)
6. [Multi-Tenancy Model](#6-multi-tenancy-model)
7. [Folder Structure](#7-folder-structure)
8. [Implementation Status](#8-implementation-status)

---

## 1. Core Purpose

### MozaiksAI is BOTH Runtime AND Generator(s)

```
┌──────────────────────────────────────────────────────────────────┐
│                        MozaiksAI                                  │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │                    RUNTIME LAYER                            │  │
│  │  • AG2 Workflow Execution Engine                           │  │
│  │  • WebSocket Transport / Event Streaming                   │  │
│  │  • Session Persistence (MongoDB)                           │  │
│  │  • Tool Registration & Invocation                          │  │
│  │  • Multi-tenant Isolation                                  │  │
│  └────────────────────────────────────────────────────────────┘  │
│                              │                                    │
│                              ▼                                    │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │                   GENERATOR WORKFLOWS                       │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐  │  │
│  │  │ AppGenerator │  │ AdGenerator  │  │ PluginGenerator  │  │  │
│  │  │ (12 agents)  │  │ (5 agents)   │  │ (6 agents)       │  │  │
│  │  └──────────────┘  └──────────────┘  └──────────────────┘  │  │
│  │                                                             │  │
│  │  Each workflow is a declarative YAML config that the       │  │
│  │  runtime loads and executes. Generators ARE workflows.     │  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

### Answer: ALL OF THE ABOVE

- **Runtime**: Stateless execution engine that can run ANY declarative workflow
- **Generators**: Specialized workflows (AppGenerator, AdGenerator, PluginGenerator) that produce artifacts
- **Not Mutually Exclusive**: The runtime executes generator workflows. Generators are first-class workflows.

---

## 2. Agentic Runtime Design

### 2.1 Agent Lifecycle

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        AGENT LIFECYCLE STATE MACHINE                     │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│    ┌──────────┐    spawn    ┌──────────┐    invoke    ┌──────────┐      │
│    │  IDLE    │ ──────────► │  READY   │ ──────────► │ RUNNING  │      │
│    └──────────┘             └──────────┘             └────┬─────┘      │
│         ▲                        ▲                        │            │
│         │                        │                        ▼            │
│         │                        │              ┌─────────────────┐    │
│         │                        │              │ AWAITING_TOOL   │    │
│         │                        │              │ (tool execution)│    │
│         │                        │              └────────┬────────┘    │
│         │                        │                       │             │
│         │                        │    tool_result        ▼             │
│         │                        ◄───────────────────────┘             │
│         │                                                              │
│         │    terminate       ┌──────────┐                              │
│         ◄─────────────────── │ COMPLETE │                              │
│                              └──────────┘                              │
│                                   ▲                                    │
│                                   │ handoff_complete                   │
│                              ┌────┴─────┐                              │
│                              │ HANDOFF  │ (transition to next agent)   │
│                              └──────────┘                              │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.2 Agent Responsibilities

| State | Runtime Action | Agent Action |
|-------|----------------|--------------|
| IDLE | Agent definition loaded, not instantiated | None |
| READY | AG2 agent instantiated with system prompt | Awaiting first message |
| RUNNING | Processing user/system message | Generating response or tool call |
| AWAITING_TOOL | Tool invoked, waiting for result | Paused |
| HANDOFF | Transition message sent | Passing context to next agent |
| COMPLETE | Persist final state, cleanup | Output delivered |

### 2.3 Memory Systems

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           MEMORY ARCHITECTURE                            │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │                    SHORT-TERM (Session)                          │    │
│  │  • Current conversation messages                                 │    │
│  │  • Active workflow state                                         │    │
│  │  • Tool execution context                                        │    │
│  │  • Scope: Single chat session                                    │    │
│  │  • Storage: In-memory + MongoDB ChatSessions                     │    │
│  └─────────────────────────────────────────────────────────────────┘    │
│                              │                                           │
│                              ▼                                           │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │                    LONG-TERM (Enterprise)                        │    │
│  │  • Previous generation outputs (per app_id)                      │    │
│  │  • User preferences and patterns                                 │    │
│  │  • Historical decisions and their outcomes                       │    │
│  │  • Scope: All sessions for an app_id                             │    │
│  │  • Storage: MongoDB Releases, AppHistory                         │    │
│  └─────────────────────────────────────────────────────────────────┘    │
│                              │                                           │
│                              ▼                                           │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │                    SEMANTIC (Cross-Enterprise)                   │    │
│  │  • Learned patterns from successful generations                  │    │
│  │  • Common failure modes and fixes                                │    │
│  │  • Best practices extracted from feedback                        │    │
│  │  • Scope: Platform-wide (anonymized)                             │    │
│  │  • Storage: Vector DB (patterns) + MongoDB (learnings)           │    │
│  └─────────────────────────────────────────────────────────────────┘    │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.4 Tool Registration & Security

```python
# Tool Categories
TOOL_TYPES = {
    "READ_ONLY": {
        "risk": "low",
        "examples": ["get_schema", "list_files", "analyze_code"],
        "approval": "auto"
    },
    "GENERATIVE": {
        "risk": "medium", 
        "examples": ["generate_code", "create_config", "write_file"],
        "approval": "auto"
    },
    "EXTERNAL_API": {
        "risk": "medium",
        "examples": ["github_create_pr", "send_webhook", "query_db"],
        "approval": "auto_with_audit"
    },
    "DESTRUCTIVE": {
        "risk": "high",
        "examples": ["delete_file", "drop_table", "revoke_access"],
        "approval": "human_required"
    },
    "FINANCIAL": {
        "risk": "critical",
        "examples": ["process_payment", "allocate_tokens", "bill_user"],
        "approval": "human_required + audit"
    }
}
```

**Tool Execution Contract:**

```yaml
tool_execution:
  pre_conditions:
    - validate_input_schema
    - check_permission_boundary
    - verify_tenant_isolation
    
  execution:
    - timeout: 30s (default), 300s (sandbox)
    - retry: 3 attempts with exponential backoff
    - isolation: per-app_id execution context
    
  post_conditions:
    - validate_output_schema
    - audit_log_execution
    - update_token_accounting
```

### 2.5 Multi-Agent Coordination

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     COORDINATION PATTERNS                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  SEQUENTIAL (AppGenerator default)                                       │
│  ┌────┐    ┌────┐    ┌────┐    ┌────┐                                   │
│  │ A1 │───►│ A2 │───►│ A3 │───►│ A4 │                                   │
│  └────┘    └────┘    └────┘    └────┘                                   │
│  • Strict ordering, each agent completes before next starts             │
│  • Context accumulates through handoff messages                          │
│                                                                          │
│  PARALLEL (Independent subtasks)                                         │
│            ┌────┐                                                        │
│        ┌──►│ A2 │──┐                                                    │
│  ┌────┐│   └────┘  │   ┌────┐                                           │
│  │ A1 │┤           ├──►│ A4 │                                           │
│  └────┘│   ┌────┐  │   └────┘                                           │
│        └──►│ A3 │──┘                                                    │
│            └────┘                                                        │
│  • Fan-out when tasks are independent                                    │
│  • Fan-in to aggregate results                                           │
│                                                                          │
│  CONDITIONAL (Branching)                                                 │
│                    ┌────┐                                                │
│              ┌────►│ A2 │ (if validation passes)                        │
│  ┌────┐     │     └────┘                                                │
│  │ A1 │─────┤                                                           │
│  └────┘     │     ┌────┐                                                │
│              └────►│ A3 │ (if validation fails)                         │
│                    └────┘                                                │
│  • Runtime evaluates condition                                           │
│  • Routes to appropriate next agent                                      │
│                                                                          │
│  ITERATIVE (Retry/Refine)                                                │
│  ┌────┐    ┌────┐    ┌────────────┐                                     │
│  │ A1 │───►│ A2 │───►│ Validator  │──┐                                  │
│  └────┘    └────┘    └────────────┘  │                                  │
│              ▲                        │ (retry if invalid)              │
│              └────────────────────────┘                                  │
│  • Bounded retries (max 3)                                               │
│  • Each retry includes error context                                     │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.6 Verification & Quality

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        VERIFICATION PIPELINE                             │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Stage 1: SYNTAX VALIDATION                                              │
│  ├── Tree-sitter parse (language-specific)                              │
│  ├── JSON/YAML schema validation                                        │
│  └── Import resolution check                                            │
│                                                                          │
│  Stage 2: SEMANTIC VALIDATION                                            │
│  ├── Type checking (where applicable)                                   │
│  ├── Reference integrity (all imports exist)                            │
│  └── Contract compliance (interfaces match)                             │
│                                                                          │
│  Stage 3: SANDBOX EXECUTION (E2B)                                        │
│  ├── Install dependencies                                               │
│  ├── Run test suite                                                     │
│  ├── Check for runtime errors                                           │
│  └── Capture stdout/stderr                                              │
│                                                                          │
│  Stage 4: INTEGRATION CHECK                                              │
│  ├── API endpoint reachability                                          │
│  ├── Database connection verification                                   │
│  └── External service mock validation                                   │
│                                                                          │
│  Stage 5: ARTIFACT PACKAGING                                             │
│  ├── Bundle all files                                                   │
│  ├── Generate manifest.json                                             │
│  ├── Compute checksums                                                  │
│  └── Tag with version                                                   │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Output Formats

### 3.1 Template vs Dynamic Generation

| Output Type | Strategy | Rationale |
|-------------|----------|-----------|
| **plugin_registry.json** | Template + fill | Predictable structure, only names/routes vary |
| **navigation_config.json** | Template + fill | Standard patterns, AI picks which to include |
| **CI/CD files** | Template library | Security-sensitive, versioned templates safer |
| **MongoDB schemas** | Template + customize | Base structure standard, fields vary |
| **Business logic code** | Full generation | Too variable for templates |
| **UI components** | Hybrid | Template structure, generated content |

### 3.2 Output Package Structure

```
output_package/
├── manifest.json              # Package metadata + checksums
├── src/
│   ├── backend/
│   │   ├── models/           # Generated from schema
│   │   ├── services/         # Full generation
│   │   ├── routes/           # Template + fill
│   │   └── config/           # Template + env vars
│   └── frontend/
│       ├── components/       # Hybrid
│       ├── pages/            # Full generation
│       └── config/           # Template + fill
├── infra/
│   ├── docker-compose.yml    # Template library
│   ├── Dockerfile            # Template library
│   └── .github/workflows/    # Template library (versioned)
├── config/
│   ├── plugin_registry.json  # Template + fill
│   ├── navigation.json       # Template + fill
│   └── features.json         # Template + fill
└── validation/
    ├── test_results.json     # E2B output
    └── lint_results.json     # Static analysis
```

### 3.3 JSON Schemas for Configs

```json
// plugin_registry.json schema
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "required": ["version", "plugins"],
  "properties": {
    "version": { "type": "string", "pattern": "^\\d+\\.\\d+\\.\\d+$" },
    "plugins": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["id", "name", "entry_point", "enabled"],
        "properties": {
          "id": { "type": "string" },
          "name": { "type": "string" },
          "entry_point": { "type": "string" },
          "enabled": { "type": "boolean" },
          "config": { "type": "object" },
          "permissions": { 
            "type": "array",
            "items": { "type": "string" }
          }
        }
      }
    }
  }
}
```

---

## 4. Validation Loop

### 4.1 Complete Validation Pipeline

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    VALIDATION & LEARNING LOOP                            │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│                         ┌─────────────────┐                              │
│                         │  User Request   │                              │
│                         └────────┬────────┘                              │
│                                  │                                       │
│                                  ▼                                       │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    GENERATION PHASE                                │  │
│  │  1. Load relevant learnings (semantic search)                      │  │
│  │  2. Execute workflow agents                                        │  │
│  │  3. Produce artifact package                                       │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                  │                                       │
│                                  ▼                                       │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    VALIDATION PHASE                                │  │
│  │  1. Syntax validation (Tree-sitter)                                │  │
│  │  2. Schema validation (JSON Schema)                                │  │
│  │  3. Sandbox execution (E2B)                                        │  │
│  │  4. Test suite run                                                 │  │
│  │  5. Integration checks                                             │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                  │                                       │
│                    ┌─────────────┴─────────────┐                        │
│                    ▼                           ▼                        │
│            ┌──────────────┐           ┌──────────────┐                  │
│            │   PASSED     │           │   FAILED     │                  │
│            └──────┬───────┘           └──────┬───────┘                  │
│                   │                          │                          │
│                   ▼                          ▼                          │
│  ┌────────────────────────┐    ┌────────────────────────────┐          │
│  │ Store as "successful"  │    │ Retry with error context   │          │
│  │ Extract patterns       │    │ (max 3 attempts)           │          │
│  │ Webhook to Control-Plane│    │ Log failure patterns       │          │
│  └────────────────────────┘    └────────────────────────────┘          │
│                   │                          │                          │
│                   └──────────┬───────────────┘                          │
│                              ▼                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    DEPLOYMENT PHASE                                │  │
│  │  (via Control-Plane → Provisioning-Agent)                         │  │
│  │  1. Control-Plane receives artifact                                │  │
│  │  2. Triggers Provisioning-Agent                                    │  │
│  │  3. Provisioning-Agent deploys to infrastructure                   │  │
│  │  4. Returns deployment status                                      │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                              │                                          │
│                              ▼                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    FEEDBACK PHASE                                  │  │
│  │  1. Collect user feedback (explicit)                               │  │
│  │  2. Collect runtime metrics (implicit)                             │  │
│  │  3. Extract learning patterns                                      │  │
│  │  4. Update semantic memory                                         │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                              │                                          │
│                              ▼                                          │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │                    LEARNING STORAGE                                │  │
│  │  MongoDB: Learnings collection                                     │  │
│  │  {                                                                 │  │
│  │    pattern_id, workflow_type, context_hash,                        │  │
│  │    input_pattern, output_pattern, success_rate,                    │  │
│  │    failure_modes[], refinements[], timestamp                       │  │
│  │  }                                                                 │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 4.2 App Generator Validation & Learning

```yaml
app_generator_validation:
  pre_generation:
    - query_similar_past_generations:
        method: semantic_search
        collection: learnings
        filter: { workflow_type: "app_generator" }
        limit: 5
    
    - inject_learned_patterns:
        into: agent_system_prompts
        format: "Previously successful pattern: {pattern}"
  
  per_agent_validation:
    DatabaseAgent:
      - validate: JSON schema for database config
      - check: All referenced collections exist in spec
    
    ModelAgent:
      - validate: Each model has required fields
      - check: Relationships reference existing models
    
    ServiceAgent:
      - validate: Tree-sitter parse passes
      - check: All model imports resolve
    
    # ... continues for all 12 agents
  
  post_generation:
    - bundle_artifacts:
        include: [src/, config/, infra/]
        manifest: true
        checksums: sha256
    
    - sandbox_validation:
        runtime: E2B
        timeout: 300s
        tests:
          - npm install
          - npm run build
          - npm run test
    
    - store_result:
        collection: releases
        fields: [app_id, version, artifacts, validation_results, timestamp]
```

### 4.3 Ad Generator Learning Loop

```yaml
ad_generator_learning:
  experiment_tracking:
    - generate_with_variants:
        control: "standard generation"
        treatment: "pattern-enhanced generation"
        assignment: user_id % 2
    
    - tag_outputs:
        experiment_id: "exp_{uuid}"
        variant: "control|treatment"
        generated_at: timestamp
  
  performance_collection:
    metrics:
      - click_through_rate
      - conversion_rate
      - engagement_time
      - bounce_rate
    
    collection_window: 7_days
    minimum_sample: 100_impressions
  
  pattern_extraction:
    triggers:
      - treatment_wins: confidence > 0.95
      - significant_difference: p_value < 0.05
    
    actions:
      - extract_winning_pattern
      - store_in_learnings
      - promote_to_default
  
  hypothesis_generation:
    schedule: weekly
    process:
      - analyze_recent_failures
      - identify_common_context
      - generate_hypothesis
      - create_experiment_config
```

---

## 5. Relationship to Control-Plane

### 5.1 Separation of Concerns

```
┌─────────────────────────────────────────────────────────────────────────┐
│                      SYSTEM BOUNDARIES                                   │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │                    CONTROL-PLANE                                 │    │
│  │  Owns:                                                           │    │
│  │  • User authentication & authorization                           │    │
│  │  • Subscription management & billing                             │    │
│  │  • App (enterprise) provisioning                                 │    │
│  │  • Secrets management (all credentials)                          │    │
│  │  • Cross-system orchestration                                    │    │
│  │  • Platform analytics & dashboards                               │    │
│  │                                                                  │    │
│  │  Communicates:                                                   │    │
│  │  • → MozaiksAI: Workflow triggers, context injection             │    │
│  │  • ← MozaiksAI: Artifacts, status updates, token usage           │    │
│  │  • → Provisioning-Agent: Deploy commands, secrets                │    │
│  │  • ← Provisioning-Agent: Deployment status, health checks        │    │
│  └─────────────────────────────────────────────────────────────────┘    │
│                              │                                           │
│                              │ webhooks / REST API                       │
│                              ▼                                           │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │                    MOZAIKSAI RUNTIME                             │    │
│  │  Owns:                                                           │    │
│  │  • Workflow execution (AG2)                                      │    │
│  │  • Chat session persistence                                      │    │
│  │  • Tool invocation & sandboxing                                  │    │
│  │  • Artifact generation & validation                              │    │
│  │  • Learning pattern storage & retrieval                          │    │
│  │                                                                  │    │
│  │  Does NOT:                                                       │    │
│  │  • Handle authentication (trusts JWT from Control-Plane)         │    │
│  │  • Store secrets (receives only what's needed per-request)       │    │
│  │  • Know about billing (just reports token usage)                 │    │
│  │  • Deploy anything (sends artifacts to Control-Plane)            │    │
│  └─────────────────────────────────────────────────────────────────┘    │
│                              │                                           │
│                              │ (via Control-Plane only)                  │
│                              ▼                                           │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │                    PROVISIONING-AGENT                            │    │
│  │  Owns:                                                           │    │
│  │  • GitHub repository management                                  │    │
│  │  • CI/CD pipeline execution                                      │    │
│  │  • Cloud resource provisioning (Azure/Vercel)                    │    │
│  │  • Database instance creation                                    │    │
│  │  • DNS & SSL configuration                                       │    │
│  │                                                                  │    │
│  │  Does NOT:                                                       │    │
│  │  • Generate code (receives artifacts)                            │    │
│  │  • Make decisions (follows Control-Plane commands)               │    │
│  │  • Talk to MozaiksAI directly (always via Control-Plane)         │    │
│  └─────────────────────────────────────────────────────────────────┘    │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 5.2 Communication Protocols

```yaml
# Control-Plane → MozaiksAI
workflow_trigger:
  endpoint: POST /api/v1/workflow/execute
  headers:
    Authorization: "Bearer {jwt}"
    X-App-ID: "{app_id}"
    X-User-ID: "{user_id}"
  body:
    workflow_id: "app_generator"
    input: { ... }
    context:
      subscription_tier: "pro"
      feature_flags: ["sandbox_enabled"]

# MozaiksAI → Control-Plane
artifact_delivery:
  endpoint: POST {control_plane}/api/v1/artifacts/receive
  headers:
    X-Runtime-Secret: "{shared_secret}"
  body:
    app_id: "..."
    workflow_id: "app_generator"
    version: "1.2.3"
    artifacts: { ... }
    validation_results: { ... }
    token_usage:
      prompt_tokens: 50000
      completion_tokens: 30000
      model: "gpt-4o"

# Control-Plane → Provisioning-Agent
deploy_command:
  endpoint: POST {provisioning}/api/v1/deploy
  headers:
    Authorization: "Bearer {service_token}"
  body:
    app_id: "..."
    artifacts_url: "https://storage.../artifacts.zip"
    secrets:
      MONGODB_URI: "..."
      API_KEYS: { ... }
    config:
      domain: "myapp.mozaiks.io"
      region: "eastus"
```

---

## 6. Multi-Tenancy Model

### 6.1 Isolation Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    MULTI-TENANCY MODEL                                   │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  RUNTIME INSTANCE: Global / Shared                                       │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  MozaiksAI Runtime (single deployment)                             │  │
│  │  • Shared AG2 engine                                               │  │
│  │  • Shared workflow definitions                                     │  │
│  │  • Shared tool implementations                                     │  │
│  │  • Shared WebSocket server                                         │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                              │                                           │
│                              │ per-request isolation                     │
│                              ▼                                           │
│  EXECUTION CONTEXT: Per-Request / Isolated                               │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  Request Context                                                   │  │
│  │  ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐      │  │
│  │  │ app_id: "abc"   │ │ app_id: "def"   │ │ app_id: "ghi"   │      │  │
│  │  │ user_id: "u1"   │ │ user_id: "u2"   │ │ user_id: "u3"   │      │  │
│  │  │ chat_id: "c1"   │ │ chat_id: "c2"   │ │ chat_id: "c3"   │      │  │
│  │  │ ─────────────── │ │ ─────────────── │ │ ─────────────── │      │  │
│  │  │ Own session     │ │ Own session     │ │ Own session     │      │  │
│  │  │ Own memory      │ │ Own memory      │ │ Own memory      │      │  │
│  │  │ Own artifacts   │ │ Own artifacts   │ │ Own artifacts   │      │  │
│  │  └─────────────────┘ └─────────────────┘ └─────────────────┘      │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  DATA ISOLATION:                                                         │
│  ┌───────────────────────────────────────────────────────────────────┐  │
│  │  MongoDB Collections (all queries MUST filter by app_id)          │  │
│  │  ├── ChatSessions: { app_id, user_id, chat_id, messages, ... }    │  │
│  │  ├── Releases: { app_id, version, artifacts, ... }                │  │
│  │  ├── Learnings: { app_id (optional), pattern, ... }               │  │
│  │  └── TokenUsage: { app_id, user_id, tokens, ... }                 │  │
│  └───────────────────────────────────────────────────────────────────┘  │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### 6.2 Isolation Enforcement

```python
# Every database query MUST include app_id filter
class TenantIsolation:
    @staticmethod
    def query_filter(app_id: str, additional_filter: dict = None) -> dict:
        """Enforce tenant isolation on every query."""
        base = {"app_id": app_id}
        if additional_filter:
            base.update(additional_filter)
        return base
    
    @staticmethod
    def validate_access(request_app_id: str, resource_app_id: str) -> bool:
        """Verify request can access resource."""
        if request_app_id != resource_app_id:
            raise TenantViolationError(
                f"App {request_app_id} cannot access resource belonging to {resource_app_id}"
            )
        return True
```

---

## 7. Folder Structure

### 7.1 Proposed Directory Layout

```
MozaiksAI/
├── core/
│   ├── runtime/
│   │   ├── engine.py              # AG2 execution engine
│   │   ├── lifecycle.py           # Agent lifecycle management
│   │   └── context.py             # Request context (app_id, user_id)
│   │
│   ├── workflow/
│   │   ├── loader.py              # YAML workflow loading
│   │   ├── validator.py           # Workflow schema validation
│   │   ├── orchestration_patterns.py  # Coordination patterns
│   │   └── handoffs.py            # Agent handoff logic
│   │
│   ├── memory/
│   │   ├── session.py             # Short-term (chat session)
│   │   ├── enterprise.py          # Long-term (per app_id)
│   │   └── semantic/
│   │       ├── embeddings.py      # Vector encoding
│   │       └── retrieval.py       # Pattern matching
│   │
│   ├── tools/
│   │   ├── registry.py            # Tool registration
│   │   ├── executor.py            # Sandboxed execution
│   │   └── builtin/               # Platform tools
│   │       ├── code_gen.py
│   │       ├── file_ops.py
│   │       └── validation.py
│   │
│   ├── validation/
│   │   ├── syntax.py              # Tree-sitter validation
│   │   ├── schema.py              # JSON Schema validation
│   │   ├── sandbox.py             # E2B integration
│   │   └── pipeline.py            # Full validation pipeline
│   │
│   ├── transport/
│   │   ├── websocket.py           # WebSocket handler
│   │   ├── events.py              # Event definitions
│   │   └── streaming.py           # Response streaming
│   │
│   ├── persistence/
│   │   ├── mongo.py               # MongoDB operations
│   │   ├── sessions.py            # Chat session CRUD
│   │   └── artifacts.py           # Artifact storage
│   │
│   └── observability/
│       ├── logging.py             # Structured logging
│       ├── metrics.py             # Performance metrics
│       └── tracing.py             # Request tracing
│
├── evaluation/                    # NEW: Learning system
│   ├── feedback_collector.py      # Gather user/system feedback
│   ├── pattern_extractor.py       # Extract learnings from results
│   ├── experiment_analyzer.py     # A/B test analysis
│   └── hypothesis_generator.py    # Auto-generate experiments
│
├── workflows/
│   ├── AppGenerator/
│   │   ├── workflow.yaml          # Main config
│   │   ├── agents.yaml            # Agent definitions
│   │   ├── handoffs.yaml          # Transition rules
│   │   ├── tools.yaml             # Tool bindings
│   │   └── hooks.py               # Lifecycle hooks
│   │
│   ├── AdGenerator/
│   │   └── ...
│   │
│   ├── PluginGenerator/           # NEW
│   │   └── ...
│   │
│   └── _templates/                # Reusable patterns
│       ├── ci_cd/
│       ├── docker/
│       └── configs/
│
├── templates/                     # Output templates
│   ├── plugin_registry.template.json
│   ├── navigation_config.template.json
│   └── features.template.json
│
├── schemas/                       # JSON Schemas
│   ├── workflow.schema.json
│   ├── plugin_registry.schema.json
│   └── output_manifest.schema.json
│
└── shared_app.py                  # FastAPI entrypoint
```

---

## 8. Implementation Status

### 8.1 What Exists (Keep/Enhance)

| Component | Location | Status |
|-----------|----------|--------|
| AG2 Orchestration | `core/workflow/orchestration_patterns.py` | ✅ Complete |
| Workflow Loading | `core/workflow/workflow_manager.py` | ✅ Complete |
| WebSocket Transport | `core/transport/` | ✅ Complete |
| Session Persistence | `core/persistence/` | ✅ Complete |
| AppGenerator Workflow | `workflows/AppGenerator/` | ✅ Complete |
| Token Tracking | `core/tokens/` | ✅ Complete |
| E2B Integration | `core/capabilities/e2b_sandbox.py` | ✅ Complete |

### 8.2 What's Missing (To Create)

| Component | Proposed Location | Priority |
|-----------|-------------------|----------|
| Semantic Memory | `core/memory/semantic/` | High |
| Feedback Collector | `evaluation/feedback_collector.py` | High |
| Pattern Extractor | `evaluation/pattern_extractor.py` | High |
| Experiment Framework | `evaluation/experiment_analyzer.py` | Medium |
| PluginGenerator Workflow | `workflows/PluginGenerator/` | Medium |
| AdGenerator Learning | `workflows/AdGenerator/learning.yaml` | Medium |
| Output Templates | `templates/` | Medium |
| JSON Schemas | `schemas/` | Low |

### 8.3 What to Move Out

| Component | Current Location | Destination |
|-----------|------------------|-------------|
| GitHub Operations | (from project-aid-v2) | provisioning-agent |
| Deployment Manager | (from project-aid-v2) | provisioning-agent |
| Secrets Management | (from project-aid-v2) | control-plane |
| DB Instance Provisioning | (from project-aid-v2) | control-plane |
| User/Auth Management | (from project-aid-v2) | control-plane |

---

## Appendix A: Learning Data Schema

```javascript
// MongoDB: Learnings Collection
{
  "_id": ObjectId,
  "pattern_id": "pat_uuid",
  "workflow_type": "app_generator | ad_generator | plugin_generator",
  "context_hash": "sha256_of_input_context",
  
  // What triggered this learning
  "source": {
    "type": "validation_success | validation_failure | user_feedback | metric_analysis",
    "generation_id": "gen_uuid",
    "timestamp": ISODate
  },
  
  // The pattern itself
  "input_pattern": {
    "description": "User requested CRUD app with auth",
    "features": ["authentication", "crud", "dashboard"],
    "constraints": ["mongodb", "react"]
  },
  
  "output_pattern": {
    "successful_approach": "...",
    "code_snippets": [...],
    "configuration": {...}
  },
  
  // Effectiveness tracking
  "metrics": {
    "times_applied": 47,
    "success_rate": 0.89,
    "avg_validation_time_ms": 2340,
    "user_satisfaction_avg": 4.2
  },
  
  // Evolution
  "refinements": [
    {
      "date": ISODate,
      "reason": "Failed on edge case with nested relations",
      "change": "Added explicit foreign key handling"
    }
  ],
  
  // Scope
  "app_id": null,  // null = platform-wide, string = app-specific
  "visibility": "global | enterprise_only"
}
```

---

## Appendix B: API Endpoints

### Runtime Endpoints

```yaml
/api/v1/workflow/execute:
  POST:
    description: Execute a workflow
    auth: JWT (from Control-Plane)
    body:
      workflow_id: string
      input: object
    response:
      execution_id: string
      status: "started"

/api/v1/workflow/{execution_id}/status:
  GET:
    description: Get execution status
    auth: JWT
    response:
      status: "running | completed | failed"
      progress: object
      artifacts: object (if completed)

/api/v1/ws/{chat_id}:
  WebSocket:
    description: Real-time event streaming
    auth: JWT in query param
    events:
      - agent_message
      - tool_call
      - tool_result
      - handoff
      - completion
      - error

/api/v1/feedback:
  POST:
    description: Submit feedback on generation
    auth: JWT
    body:
      generation_id: string
      rating: 1-5
      comments: string (optional)
      issues: array (optional)
```

---

## Document Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-01-17 | Engineering Agent | Initial comprehensive architecture |
