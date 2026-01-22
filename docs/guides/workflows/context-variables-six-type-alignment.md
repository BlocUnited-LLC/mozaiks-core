# Context Variables Six-Type Taxonomy Alignment Strategy

**Status**: Implementation Plan  
**Created**: November 19, 2025  
**Pattern**: Same stateless approach as human_interaction alignment  
**Authoritative Source**: `docs/workflows/CONTEXT_VARIABLES_COMPLETE.md`

---

## Executive Summary

This document outlines the **stateless alignment strategy** for migrating from the legacy four-type context variable taxonomy (database, environment, static, derived) to the production-ready six-type taxonomy (config, data_reference, data_entity, computed, state, external).

**Migration follows the same pattern used for human_interaction alignment**:
1. Update schema definitions (structured_outputs.json)
2. Update agent decision logic (ContextVariablesAgent)
3. Update downstream consumers (AgentToolsFileGenerator, HandoffsAgent)
4. Verify upstream producers (WorkflowArchitectAgent)
5. Validate end-to-end alignment

---

## Current State Assessment

### Four-Type Legacy Taxonomy (DEPRECATED)

| Type | Purpose | Issues |
|------|---------|--------|
| `database` | MongoDB queries | No read/write distinction; ambiguous lifecycle |
| `environment` | Environment variables | Conflates deployment config with static values |
| `static` | Literal config values | Overlaps with environment; unclear persistence |
| `derived` | Runtime-updated variables | Conflates state machines with calculations |

### Six-Type Production Taxonomy (TARGET)

| Type | Purpose | Persistence | Lifecycle |
|------|---------|-------------|-----------|
| `config` | Deployment configuration | Never | Load once from env vars |
| `data_reference` | Existing database data (READ) | External | Query existing collection |
| `data_entity` | New database data (WRITE) | Yes | Create/update in workflow-owned collection |
| `computed` | Business logic outputs | Optional | Calculate during workflow |
| `state` | Workflow orchestration state | Optional | Initialize with default, transition via triggers |
| `external` | External API data | Cached | Fetch on-demand with retry/cache |

### Migration Mapping

```
environment → config (with env_var)
static → config (with value or default)
database (read) → data_reference (with query_template)
database (write) → data_entity (with write_strategy)
derived (calculations) → computed (with computation)
derived (state machines) → state (with transitions)
(new) → external (with service, auth, cache)
```

---

## Alignment Strategy (9 Steps)

### Step 1: Update structured_outputs.json Schema ✅ FOUNDATION

**File**: `workflows/Generator/structured_outputs.json`  
**Lines**: 440-530 (ContextVariableSource definition)

**Changes Required**:

1. **Update type literals** (lines 445-453):
```json
"type": {
  "type": "literal",
  "values": [
    "config",
    "data_reference",
    "data_entity",
    "computed",
    "state",
    "external"
  ]
}
```

2. **Add type-specific fields**:

```json
"refresh_strategy": {
  "type": "literal",
  "values": ["once", "per_phase", "per_turn"],
  "description": "For data_reference: when to re-query"
},
"write_strategy": {
  "type": "literal",
  "values": ["immediate", "on_phase_transition", "on_workflow_end"],
  "description": "For data_entity: when to persist"
},
"computation": {
  "type": "string",
  "description": "For computed: function name"
},
"inputs": {
  "type": "array",
  "items": {"type": "string"},
  "description": "For computed: input variable names"
},
"output_type": {
  "type": "string",
  "description": "For computed: return type (string, integer, float, boolean, object)"
},
"persist_to": {
  "type": "object",
  "description": "For computed: optional database persistence config"
},
"transitions": {
  "type": "array",
  "items": {
    "type": "object",
    "properties": {
      "from": {"type": "string"},
      "to": {"type": "string"},
      "trigger": {"type": "object"}
    }
  },
  "description": "For state: state machine transitions"
},
"service": {
  "type": "string",
  "description": "For external: API service name (stripe, salesforce, weather)"
},
"operation": {
  "type": "string",
  "description": "For external: API operation (customers.retrieve, orders.list)"
},
"params": {
  "type": "object",
  "description": "For external: API parameters with ${variable} interpolation"
},
"auth": {
  "type": "object",
  "description": "For external: authentication config"
},
"cache": {
  "type": "object",
  "properties": {
    "ttl": {"type": "integer"},
    "key_template": {"type": "string"}
  },
  "description": "For external: caching strategy"
},
"retry": {
  "type": "object",
  "properties": {
    "max_attempts": {"type": "integer"},
    "backoff": {"type": "literal", "values": ["linear", "exponential"]}
  },
  "description": "For external: retry policy"
}
```

**Validation**: Run `python -c "import json; json.load(open('workflows/Generator/structured_outputs.json'))"`

---

### Step 2: Update ContextVariablesAgent [CONTEXT] Section

**File**: `workflows/Generator/agents.json`  
**Lines**: 316-366 (ContextVariablesAgent configuration)  
**Section**: Line 341 area (GUIDELINES → [CONTEXT])

**Changes Required**:

Replace four-type source descriptions with six-type taxonomy:

```json
{
  "id": "context",
  "heading": "[CONTEXT]",
  "content": "As you perform your objective, you will leverage the following upstream outputs:\n\n1. **TechnicalBlueprint**:\n   - Scan conversation history for message containing 'TechnicalBlueprint' semantic wrapper\n   - Navigate to: message.content['TechnicalBlueprint']['global_context_variables']\n   - Array of RequiredContextVariable objects with: name, type (six-type taxonomy), trigger_hint, purpose\n   - You transform these into complete ContextVariableDefinition objects with source-specific fields\n\n2. **Database Schema (if CONTEXT_AWARE=true)**:\n   - Available via schema_overview context variable\n   - Lists collections, fields, indexes from user's MongoDB\n   - Use to validate data_reference queries and data_entity writes\n\n**Six-Type Context Variable Taxonomy**:\n\n| Type | Purpose | Persistence | Key Fields | When to Use |\n|------|---------|-------------|------------|-------------|\n| **config** | Deployment configuration (env vars, feature flags) | Never | env_var, default, required | Settings that vary by environment (dev/staging/prod) |\n| **data_reference** | Existing database data (READ only) | External | database_name, collection, query_template, refresh_strategy | Query collections that already exist in user's database |\n| **data_entity** | New database data (WRITE) | Yes | collection, search_by, schema, write_strategy | Data this workflow creates and persists |\n| **computed** | Business logic outputs | Optional | computation, inputs, output_type, persist_to | Calculated values (may or may not persist) |\n| **state** | Workflow orchestration state | Optional | default, transitions, persist | Phase tracking, approval gates, routing flags |\n| **external** | External API data | Cached | service, operation, params, auth, cache, retry | Third-party API calls (Stripe, Salesforce, etc.) |\n\n**Semantic Clarifications**:\n- **Read vs Write**: data_reference = read existing data; data_entity = write new data\n- **Existing vs New**: data_reference = collection exists; data_entity = workflow creates collection\n- **Ephemeral vs Persisted**: state/computed can be ephemeral (persist=false) or persisted (persist=true)\n- **Deployment vs Business**: config = ops-controlled; computed = business logic\n\n**Decision Matrix**:\n\n```\nDoes data come from outside the automation?\n├─ YES → Is it from user's database?\n│         ├─ YES → Does collection already exist?\n│         │         ├─ YES → Does workflow WRITE to it?\n│         │         │         ├─ YES → data_entity\n│         │         │         └─ NO → data_reference\n│         │         └─ NO → data_entity (workflow creates it)\n│         └─ NO → Is it from external API?\n│                   ├─ YES → external\n│                   └─ NO → config\n│\n└─ NO → Is it calculated by automation logic?\n          ├─ YES → Does it need to persist?\n          │         ├─ YES → computed (with persist_to)\n          │         └─ NO → computed (persist_to=null)\n          └─ NO → Does it change during workflow?\n                    ├─ YES → state\n                    └─ NO → config (static value)\n```"
}
```

---

### Step 3: Update ContextVariablesAgent [INSTRUCTIONS] Section

**File**: `workflows/Generator/agents.json`  
**Lines**: 350-365 area (ContextVariablesAgent [INSTRUCTIONS])

**Changes Required**:

Add comprehensive 5-step decision algorithm (mirroring human_UI pattern):

```json
{
  "id": "instructions",
  "heading": "[INSTRUCTIONS]",
  "content": "**Step 1 - Read TechnicalBlueprint Context Variables**:\n- Locate TechnicalBlueprint.global_context_variables[] from conversation history\n- Extract: name, type (six types), trigger_hint, purpose for each variable\n- These are high-level specifications; you add implementation details\n\n**Step 2 - Choose Source Type (Use Decision Algorithm)**:\n\nFor EACH global_context_variable entry, apply this 5-step algorithm:\n\n**STEP 2A - Analyze Variable Purpose**:\n- Read variable.purpose and variable.trigger_hint\n- Identify keywords:\n  * \"environment\", \"feature flag\", \"deployment\" → config candidate\n  * \"existing\", \"query\", \"lookup\", \"reference\" → data_reference candidate\n  * \"create\", \"persist\", \"save\", \"write\" → data_entity candidate\n  * \"calculate\", \"compute\", \"derive\", \"format\" → computed candidate\n  * \"phase\", \"approval\", \"routing\", \"coordination\" → state candidate\n  * \"API\", \"Stripe\", \"external\", \"third-party\" → external candidate\n\n**STEP 2B - Apply Decision Matrix**:\n\nIF purpose mentions \"environment variable\" OR \"feature flag\" OR \"deployment config\":\n  → source.type = \"config\"\n  → Set: env_var (UPPER_SNAKE_CASE), default, required\n\nELSE IF purpose mentions \"query\" OR \"lookup\" OR \"existing collection\":\n  → source.type = \"data_reference\"\n  → Set: database_name, collection, query_template, field, refresh_strategy\n  → Validate collection exists in schema_overview (if CONTEXT_AWARE=true)\n\nELSE IF purpose mentions \"create\" OR \"persist\" OR \"audit log\" OR \"workflow output\":\n  → source.type = \"data_entity\"\n  → Set: collection, search_by, schema, write_strategy\n  → Note: Collection will be created by Build workflow if doesn't exist\n\nELSE IF purpose mentions \"calculate\" OR \"compute\" OR \"derive\" OR \"format\":\n  → source.type = \"computed\"\n  → Set: computation (function name), inputs, output_type\n  → IF purpose mentions \"persist\" OR \"save\" → Add persist_to config\n  → ELSE → persist_to = null (ephemeral)\n\nELSE IF purpose mentions \"phase\" OR \"approval\" OR \"routing\" OR \"coordination token\":\n  → source.type = \"state\"\n  → Set: default (initial value), transitions[] (if applicable)\n  → IF trigger_hint mentions agent text emission → Add agent_text trigger\n  → IF trigger_hint mentions UI interaction → Add ui_response trigger\n  → persist = false (unless audit/resume needed)\n\nELSE IF purpose mentions \"API\" OR \"Stripe\" OR \"Salesforce\" OR \"external service\":\n  → source.type = \"external\"\n  → Set: service, operation, params, auth, cache, retry\n\n**STEP 2C - Populate Type-Specific Fields**:\n\nFor config:\n- env_var: UPPER_SNAKE_CASE environment variable name\n- default: Fallback value if env var not set\n- required: true if workflow fails without it\n\nFor data_reference:\n- database_name: MongoDB database name\n- collection: Collection to query\n- query_template: Query object with ${variable} interpolation (e.g., {\"user_id\": \"${user_id}\"})\n- field: Single field name to extract (or omit to return whole document)\n- refresh_strategy: \"once\" (default), \"per_phase\", or \"per_turn\"\n\nFor data_entity:\n- collection: Collection name (workflow creates if doesn't exist)\n- search_by: Primary key field (e.g., \"workflow_id\")\n- schema: Field definitions for collection (name: type)\n- write_strategy: \"immediate\", \"on_phase_transition\", or \"on_workflow_end\"\n\nFor computed:\n- computation: Function name that performs calculation\n- inputs: Array of input variable names\n- output_type: \"string\", \"integer\", \"float\", \"boolean\", or \"object\"\n- persist_to: {collection, search_by, field} or null\n\nFor state:\n- default: Initial value\n- transitions: Array of state transition objects:\n  * from: Source state (or null for initial transition)\n  * to: Target state\n  * trigger: {type: \"agent_text\"|\"ui_response\", agent/tool, match/response_key}\n- persist: false (default) or true (for audit/resume)\n\nFor external:\n- service: \"stripe\", \"salesforce\", \"sendgrid\", \"weather\", etc.\n- operation: API method (e.g., \"customers.retrieve\")\n- params: Parameter object with ${variable} interpolation\n- auth: {type: \"api_key\"|\"oauth\", env_var: \"KEY_NAME\"}\n- cache: {ttl: seconds, key_template: \"cache_key_${var}\"}\n- retry: {max_attempts: 3, backoff: \"exponential\"}\n\n**Step 3 - Design Triggers (State Variables Only)**:\n\nFor state variables with transitions:\n\nIF trigger_hint mentions \"agent emits token\" OR \"agent says X\":\n- trigger.type = \"agent_text\"\n- trigger.agent = AgentName (PascalCase)\n- trigger.match = {equals: \"TOKEN\"} OR {contains: \"keyword\"} OR {regex: \"pattern\"}\n\nIF trigger_hint mentions \"user clicks\" OR \"UI response\" OR \"form submission\":\n- trigger.type = \"ui_response\"\n- trigger.tool = tool_function_name (snake_case)\n- trigger.response_key = variable_name (matches tool payload field)\n\n**Step 4 - Build Agent Exposure Map**:\n\n- Create agents object mapping agent names to variable arrays\n- For EACH agent in workflow:\n  * List variables that agent needs to read\n  * Variables are injected into agent prompts via UpdateSystemMessage\n  * UI tools receive context indirectly via tool payload (not direct access)\n\n**Step 5 - Validate Completeness**:\n\n- Ensure all variables have correct source.type from six-type taxonomy\n- Ensure type-specific required fields are present:\n  * config: env_var or default\n  * data_reference: database_name, collection, query_template\n  * data_entity: collection, search_by, write_strategy\n  * computed: computation, inputs, output_type\n  * state: default, transitions (if applicable)\n  * external: service, operation, params, auth\n- Ensure no legacy types (database, environment, static, derived)\n\n**Step 6 - Output ContextVariablesPlan JSON**:\n- Emit ContextVariablesPlan as valid JSON matching schema\n- NO markdown fences, NO explanatory text, ONLY the JSON object"
}
```

---

### Step 4: Update ContextVariablesAgent [OUTPUT FORMAT] Section

**File**: `workflows/Generator/agents.json`  
**Lines**: 361-365 area (ContextVariablesAgent [OUTPUT FORMAT])

**Changes Required**:

Replace four-type example with six-type schema and examples:

```json
{
  "id": "output_format",
  "heading": "[OUTPUT FORMAT]",
  "content": "Output MUST be a valid JSON object with the following structure:\n\n```json\n{\n  \"definitions\": {\n    \"<variable_name>\": {\n      \"type\": \"string|integer|float|boolean|object\",\n      \"description\": \"<purpose>\",\n      \"source\": {\n        \"type\": \"config|data_reference|data_entity|computed|state|external\",\n        ... (type-specific fields)\n      }\n    }\n  },\n  \"agents\": {\n    \"<AgentName>\": {\n      \"variables\": [\"var1\", \"var2\"]\n    }\n  },\n  \"agent_message\": \"<summary>\"\n}\n```\n\n**Complete Examples for Each Type**:\n\n**config Example**:\n```json\n\"max_retry_attempts\": {\n  \"type\": \"integer\",\n  \"description\": \"Maximum API retry attempts before failure\",\n  \"source\": {\n    \"type\": \"config\",\n    \"env_var\": \"MAX_RETRY_ATTEMPTS\",\n    \"default\": 3,\n    \"required\": false\n  }\n}\n```\n\n**data_reference Example**:\n```json\n\"customer_tier\": {\n  \"type\": \"string\",\n  \"description\": \"User's subscription tier from existing Users collection\",\n  \"source\": {\n    \"type\": \"data_reference\",\n    \"database_name\": \"user_database\",\n    \"collection\": \"Users\",\n    \"query_template\": {\"user_id\": \"${user_id}\"},\n    \"field\": \"subscription_tier\",\n    \"refresh_strategy\": \"once\"\n  }\n}\n```\n\n**data_entity Example**:\n```json\n\"workflow_audit\": {\n  \"type\": \"object\",\n  \"description\": \"Audit record created by workflow execution\",\n  \"source\": {\n    \"type\": \"data_entity\",\n    \"collection\": \"WorkflowAudits\",\n    \"search_by\": \"workflow_id\",\n    \"schema\": {\n      \"workflow_id\": \"string\",\n      \"user_id\": \"string\",\n      \"started_at\": \"datetime\",\n      \"status\": \"string\"\n    },\n    \"write_strategy\": \"on_workflow_end\"\n  }\n}\n```\n\n**computed Example**:\n```json\n\"escalation_score\": {\n  \"type\": \"float\",\n  \"description\": \"Calculated priority score for ticket escalation\",\n  \"source\": {\n    \"type\": \"computed\",\n    \"computation\": \"calculate_escalation_score\",\n    \"inputs\": [\"ticket_priority\", \"customer_tier\", \"response_time_hours\"],\n    \"output_type\": \"float\",\n    \"persist_to\": {\n      \"collection\": \"Tickets\",\n      \"search_by\": \"ticket_id\",\n      \"field\": \"escalation_score\"\n    }\n  }\n}\n```\n\n**state Example**:\n```json\n\"current_phase\": {\n  \"type\": \"string\",\n  \"description\": \"Current workflow phase for routing decisions\",\n  \"source\": {\n    \"type\": \"state\",\n    \"default\": \"interview\",\n    \"transitions\": [\n      {\n        \"from\": \"interview\",\n        \"to\": \"planning\",\n        \"trigger\": {\n          \"type\": \"agent_text\",\n          \"agent\": \"InterviewAgent\",\n          \"match\": {\"equals\": \"NEXT\"}\n        }\n      },\n      {\n        \"from\": \"planning\",\n        \"to\": \"execution\",\n        \"trigger\": {\n          \"type\": \"ui_response\",\n          \"tool\": \"approval_gate\",\n          \"response_key\": \"approved\"\n        }\n      }\n    ],\n    \"persist\": false\n  }\n}\n```\n\n**external Example**:\n```json\n\"stripe_customer\": {\n  \"type\": \"object\",\n  \"description\": \"Customer data from Stripe API\",\n  \"source\": {\n    \"type\": \"external\",\n    \"service\": \"stripe\",\n    \"operation\": \"customers.retrieve\",\n    \"params\": {\"customer_id\": \"${customer_id}\"},\n    \"auth\": {\n      \"type\": \"api_key\",\n      \"env_var\": \"STRIPE_API_KEY\"\n    },\n    \"cache\": {\n      \"ttl\": 300,\n      \"key_template\": \"stripe_customer_${customer_id}\"\n    },\n    \"retry\": {\n      \"max_attempts\": 3,\n      \"backoff\": \"exponential\"\n    }\n  }\n}\n```\n\n**Field Requirements Matrix**:\n\n| Type | Required Fields | Optional Fields |\n|------|----------------|----------------|\n| config | env_var OR default | required |\n| data_reference | database_name, collection, query_template | field, refresh_strategy |\n| data_entity | collection, search_by, write_strategy | schema, indexes |\n| computed | computation, inputs, output_type | persist_to |\n| state | default | transitions, persist |\n| external | service, operation, params, auth | cache, retry |\n\n**CRITICAL**: Output ONLY the raw JSON object. NO markdown fences, NO explanatory text."
}
```

---

### Step 5: Update AgentToolsFileGenerator [CONTEXT] Section

**File**: `workflows/Generator/agents.json`  
**Lines**: 467-520 (AgentToolsFileGenerator configuration)  
**Section**: Line 482 area ([CONTEXT])

**Changes Required**:

Add six-type taxonomy with code generation patterns:

```json
{
  "id": "context",
  "heading": "[CONTEXT]",
  "content": "As you perform your objective, you will leverage:\n\n1. **ToolsManifest**:\n   - Contains agent_tools[] and ui_tools[] from upstream agents\n   - Each tool has: name, purpose, parameters, return_type, interaction_mode\n   - You generate Python implementation code for each tool\n\n2. **ContextVariablesPlan**:\n   - Contains definitions[] with six-type taxonomy context variables\n   - Tools may need to READ or WRITE context variables\n   - **CRITICAL**: Access pattern varies by variable type\n\n**Six-Type Context Variable Access Patterns**:\n\n| Type | Access Pattern | Import/Code Template |\n|------|---------------|----------------------|\n| **config** | Read from environment | `import os`<br>`value = os.getenv(\"VAR_NAME\", default)` |\n| **data_reference** | Query existing collection | `from core.db import get_db`<br>`db = get_db()[database_name][collection]`<br>`doc = db.find_one(query)` |\n| **data_entity** | Write to collection | `from core.db import get_db`<br>`db = get_db()[collection]`<br>`db.update_one(filter, update, upsert=True)` |\n| **computed** | Calculate + optional persist | `result = computation_function(*inputs)`<br>`# Optionally persist to database` |\n| **state** | Read/write workflow state | `from core.workflow.context import get_context_variable, set_context_variable`<br>`value = get_context_variable(workflow_context, \"var_name\")`<br>`set_context_variable(workflow_context, \"var_name\", new_value)` |\n| **external** | Call external API | `import httpx`<br>`async with httpx.AsyncClient() as client:`<br>`    response = await client.get(url, headers=auth)`<br>`# Cache result per TTL` |\n\n**When to Use Each Pattern**:\n\n**config**: Tool needs deployment-specific settings (feature flags, limits)  \n→ Use `os.getenv()` to read environment variable\n\n**data_reference**: Tool needs to query existing user data  \n→ Use `get_db()[database][collection].find_one(query)`\n\n**data_entity**: Tool creates/updates workflow-owned data  \n→ Use `get_db()[collection].update_one()` or `insert_one()`\n\n**computed**: Tool performs calculation  \n→ Call computation function, optionally persist result to database\n\n**state**: Tool updates workflow orchestration state (most common for UI tools)  \n→ Use `get_context_variable()` / `set_context_variable()` helpers\n\n**external**: Tool calls third-party API  \n→ Use `httpx` with authentication, caching, and retry logic\n\n**Code Generation Rules**:\n\n1. **Identify variable types** from ContextVariablesPlan.definitions[].source.type\n2. **Choose correct import** based on variable type\n3. **Generate type-appropriate access code**:\n   - config → `os.getenv()`\n   - data_reference → `db.find_one()`\n   - data_entity → `db.update_one()` or `insert_one()`\n   - computed → function call + optional persist\n   - state → `get/set_context_variable()`\n   - external → `httpx` with auth/cache/retry\n4. **Add error handling** for database queries and API calls\n5. **Respect write_strategy** for data_entity (immediate vs deferred)\n\n**Example: UI Tool that updates state variable**:\n\n```python\nfrom core.workflow.context import set_context_variable\n\nasync def approval_gate_tool(\n    workflow_context: dict,\n    plan_data: dict\n) -> dict:\n    \"\"\"Approval gate for action plan review.\"\"\"\n    \n    # Render UI and wait for user response\n    response = await use_ui_tool(\n        tool_id=\"approval_gate\",\n        component=\"ApprovalGate\",\n        payload=plan_data,\n        wait_for_response=True\n    )\n    \n    # Extract approval status from UI response\n    approval_status = response.get(\"approval_status\")  # \"accepted\" or \"rejected\"\n    \n    # Update state variable (state type → use set_context_variable)\n    if approval_status:\n        set_context_variable(\n            workflow_context,\n            \"action_plan_acceptance\",\n            approval_status\n        )\n    \n    return {\n        \"status\": \"complete\",\n        \"decision\": approval_status\n    }\n```\n\n**Example: Agent tool that queries existing data**:\n\n```python\nfrom core.db import get_db\n\nasync def fetch_customer_tier(\n    workflow_context: dict,\n    user_id: str\n) -> dict:\n    \"\"\"Fetch customer tier from existing Users collection.\"\"\"\n    \n    # Query existing collection (data_reference type → use db.find_one)\n    db = get_db()[\"user_database\"]\n    user_doc = await db[\"Users\"].find_one({\"user_id\": user_id})\n    \n    if not user_doc:\n        return {\"error\": \"User not found\"}\n    \n    tier = user_doc.get(\"subscription_tier\", \"free\")\n    \n    return {\n        \"customer_tier\": tier,\n        \"user_id\": user_id\n    }\n```"
}
```

---

### Step 6: Update HandoffsAgent [CONTEXT] Section

**File**: `workflows/Generator/agents.json`  
**Lines**: 665-750 (HandoffsAgent configuration)  
**Section**: Line 680 area ([CONTEXT])

**Changes Required**:

Update terminology from "derived variables" to "state/computed variables with triggers":

```json
{
  "id": "context",
  "heading": "[CONTEXT]",
  "content": "As you perform your objective, you will leverage the following upstream outputs when generating handoff rules:\n\n1. **ActionPlan**:\n   - Contains: workflow.phases[] (with phase sequencing, agents per phase, transitions), flow_type, approval gates\n   - The action plan's phase structure informs agent-to-agent handoff order and workflow completion semantics\n   - Approval gates and transitions determine conditional routing logic\n\n2. **ContextVariablesPlan**:\n   - Contains: definitions[] (with state/computed variables and triggers), agents[] (canonical agent roster)\n   - **State variables** may have transitions with agent_text or ui_response triggers (coordination tokens, approval gates)\n   - **Computed variables** may have triggers if calculations affect routing decisions (rare)\n   - **Other types** (config, data_reference, data_entity, external) do not have triggers\n   - Trigger types (agent_text vs ui_response) determine condition_scope in handoff rules:\n     * agent_text triggers → condition_scope=null (evaluated after agent's turn)\n     * ui_response triggers → condition_scope=\"pre\" (evaluated before next turn, re-checks until true)"
}
```

Update Step 3 in [INSTRUCTIONS]:

```json
{
  "id": "instructions",
  "heading": "[INSTRUCTIONS]",
  "content": "**Step 1 - Read Action Plan and Context Variables**:\n...\n\n**Step 3 - Add Conditional Handoffs for State Variables**:\n- Scan ContextVariablesPlan.definitions for state/computed variables with triggers\n- For EACH agent_text trigger (agent emits coordination token):\n  * Create handoff with handoff_type=\"condition\", condition_type=\"expression\", condition_scope=null\n  * Set condition=\"${variable_name} == true\" or match trigger.match.equals value\n  * Pattern: Agent emits token → runtime updates variable → post-reply check routes to next agent\n- For EACH ui_response trigger (UI tool updates variable):\n  * Create handoff with handoff_type=\"condition\", condition_type=\"expression\", condition_scope=\"pre\"\n  * Set condition=\"${variable_name} == <expected_value>\"\n  * Pattern: User interacts with UI → tool sets variable → pre-reply check catches it → routes to next agent\n\n..."
}
```

---

### Step 7: Verify WorkflowArchitectAgent Consistency

**File**: `workflows/Generator/agents.json`  
**Lines**: 170-210 (WorkflowArchitectAgent configuration)

**Verification Tasks**:

1. **Confirm OUTPUT FORMAT uses six-type taxonomy** (line 205):
```json
"type": "config|data_reference|data_entity|computed|state|external"
```
✅ Already correct

2. **Verify [INSTRUCTIONS] generate correct six-type values**:
- Check Step where global_context_variables[] are created
- Ensure examples and guidance use six-type values
- Validate decision logic aligns with ContextVariablesAgent

3. **Check pattern guidance examples**:
- Ensure pattern-specific context variable examples use six types
- Update any legacy four-type examples

**Action**: Read full WorkflowArchitectAgent prompt and verify consistency

---

### Step 8: Verify UIFileGenerator Documentation

**File**: `workflows/Generator/agents.json`  
**Lines**: 420-465 (UIFileGenerator configuration)

**Verification Tasks**:

1. **Review [CONTEXT] section** for context variable references
2. **Confirm UI tools receive context indirectly** via payload (not direct access)
3. **Verify no documentation changes needed** (UI layer doesn't access context variables directly)
4. **Document payload vs context variable distinction** if unclear

**Expected Outcome**: No changes needed (UI tools don't directly access context variables)

---

### Step 9: Final Verification and Validation

**Validation Checklist**:

1. **JSON Syntax Validation**:
```bash
python -c "import json; json.load(open('workflows/Generator/structured_outputs.json')); print('✅ Valid JSON')"
python -c "import json; json.load(open('workflows/Generator/agents.json')); print('✅ Valid JSON')"
```

2. **Six-Type Mapping Completeness**:
- [ ] All six types have required fields defined in schema
- [ ] ContextVariablesAgent decision matrix covers all six types
- [ ] AgentToolsFileGenerator documents access patterns for all six types
- [ ] HandoffsAgent recognizes state/computed triggers

3. **Decision Logic Gaps**:
- [ ] Every scenario in decision tree leads to deterministic type selection
- [ ] No ambiguous cases (clear IF-THEN logic)
- [ ] Keywords and criteria documented for each type

4. **Alignment with CONTEXT_VARIABLES_COMPLETE.md**:
- [ ] Type definitions match authoritative documentation
- [ ] Field names and structures align
- [ ] Decision matrix consistent
- [ ] Examples use correct syntax

5. **Code Generation Patterns**:
- [ ] config → `os.getenv()` documented
- [ ] data_reference → `db.find_one()` documented
- [ ] data_entity → `db.update_one()` documented
- [ ] computed → function call + persist documented
- [ ] state → `get/set_context_variable()` documented
- [ ] external → `httpx` with auth/cache/retry documented

6. **Handoff Chain Validation**:
```
WorkflowArchitect (creates global_context_variables[] with six types)
    ↓
ContextVariables (transforms to ContextVariablesPlan with six types)
    ↓
AgentToolsFileGenerator (generates tools with six-type access patterns)
    ↓
HandoffsAgent (creates conditions for state/computed triggers)
    ↓
Runtime (executes with six-type awareness)
```

7. **Backward Compatibility**:
- [ ] No legacy four-type values remain in examples
- [ ] All documentation updated to six-type terminology
- [ ] Migration mapping documented (environment→config, etc.)

---

## Implementation Order (Sequential Dependencies)

### Phase 1: Foundation (Step 1)
**Must complete first** - Schema defines contract for all other components

1. Update structured_outputs.json ContextVariableSource
2. Validate JSON syntax
3. Test Pydantic schema loading (if applicable)

### Phase 2: Producer Agents (Steps 2-4)
**Can parallelize** - These agents produce ContextVariablesPlan

1. Update ContextVariablesAgent [CONTEXT]
2. Update ContextVariablesAgent [INSTRUCTIONS]
3. Update ContextVariablesAgent [OUTPUT FORMAT]

### Phase 3: Consumer Agents (Steps 5-6)
**Depends on Phase 2** - These agents consume ContextVariablesPlan

1. Update AgentToolsFileGenerator [CONTEXT]
2. Update HandoffsAgent [CONTEXT] and [INSTRUCTIONS]

### Phase 4: Verification (Steps 7-9)
**Depends on Phases 1-3** - Validate end-to-end alignment

1. Verify WorkflowArchitectAgent consistency
2. Verify UIFileGenerator documentation
3. Run final validation suite

---

## Success Criteria (ALL MUST PASS)

✅ **Schema Alignment**:
- structured_outputs.json ContextVariableSource has six literal types
- All six types have required type-specific fields
- JSON syntax valid

✅ **Agent Alignment**:
- ContextVariablesAgent [CONTEXT] documents six-type taxonomy with decision matrix
- ContextVariablesAgent [INSTRUCTIONS] has 5-step decision algorithm
- ContextVariablesAgent [OUTPUT FORMAT] shows six-type examples
- AgentToolsFileGenerator [CONTEXT] documents six-type access patterns
- HandoffsAgent [CONTEXT] uses state/computed terminology (not "derived")

✅ **Decision Logic**:
- No ambiguous cases (every scenario covered)
- Deterministic type selection (clear IF-THEN logic)
- Keywords and criteria documented

✅ **Code Generation**:
- All six types have documented access patterns
- Import statements correct for each type
- Error handling patterns documented

✅ **Documentation**:
- Complete alignment with CONTEXT_VARIABLES_COMPLETE.md
- Migration mapping from four-type to six-type
- No legacy terminology remains

✅ **Validation**:
- JSON parses without errors
- No decision logic gaps
- Handoff chain complete (WorkflowArchitect → ContextVariables → AgentToolsFileGenerator → HandoffsAgent)

---

## Rollback Strategy

If issues arise during implementation:

1. **Schema Issues**: Revert structured_outputs.json to four-type (git checkout)
2. **Agent Issues**: Fix one agent at a time, test incrementally
3. **Validation Failures**: Use git diff to identify breaking changes
4. **Runtime Issues**: Runtime already supports both taxonomies via adapter layer

**Safety Net**: Runtime's `core/workflow/context/` adapters handle both taxonomies during transition period

---

## Next Steps

1. **Review this strategy document** - Confirm approach aligns with requirements
2. **Begin Phase 1** - Update structured_outputs.json schema (foundation)
3. **Test Phase 1** - Validate JSON syntax and Pydantic loading
4. **Proceed to Phase 2** - Update ContextVariablesAgent (producer)
5. **Continue sequentially** through Phases 3-4

**Estimated Effort**: 2-3 hours (similar to human_interaction alignment)

---

## Appendix: Quick Reference

### Six-Type Cheat Sheet

| Type | One-Liner | Example |
|------|-----------|---------|
| config | Deployment settings from env vars | `MAX_RETRY_ATTEMPTS=3` |
| data_reference | Query existing user data | `Users.find_one({"user_id": ...})` |
| data_entity | Create workflow-owned data | `WorkflowAudits.insert_one({...})` |
| computed | Calculate derived values | `escalation_score = calculate(...)` |
| state | Workflow orchestration state | `current_phase = "planning"` |
| external | Third-party API calls | `stripe.Customer.retrieve(...)` |

### Access Pattern Quick Lookup

```python
# config
import os
value = os.getenv("VAR_NAME", default)

# data_reference
from core.db import get_db
doc = get_db()[db_name][collection].find_one(query)

# data_entity
from core.db import get_db
get_db()[collection].update_one(filter, update, upsert=True)

# computed
result = computation_function(*inputs)

# state
from core.workflow.context import get_context_variable, set_context_variable
value = get_context_variable(context, "var_name")
set_context_variable(context, "var_name", new_value)

# external
import httpx
async with httpx.AsyncClient() as client:
    response = await client.get(url, headers=auth)
```
