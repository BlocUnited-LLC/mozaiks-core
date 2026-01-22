# Context Variables Stateless Strategy

**Status**: Implementation Guide  
**Created**: November 19, 2025  
**Pattern**: Stateless agent coordination via semantic wrappers  
**Authoritative Source**: `docs/workflows/CONTEXT_VARIABLES_COMPLETE.md`

> ⚠️ **PARTIALLY OUTDATED**: Some examples reference legacy schemas (ActionPlan, ActionPlanArchitect).
> For current schema definitions, see:
> - **Source of Truth**: `docs/ACTION_PLAN_SOURCE_OF_TRUTH.md`
> - **Schema Definitions**: `workflows/AgentGenerator/structured_outputs.json`
> 
> The semantic wrapper patterns and stateless coordination concepts remain valid.

---

## Executive Summary

Context variables are **the single most critical input** for stateless LLM prompt engineering. Unlike stateful systems where agents maintain memory, our AgentGenerator workflow operates in **pure stateless mode** - each agent receives ONLY:

1. **System Message** (role, instructions, guidelines)
2. **Conversation History** (prior agent outputs as semantic wrappers)
3. **User Intent** (original request context)

**No agent remembers what it previously generated.** Every output must be reconstructible from conversation history alone.

This document defines the **semantic wrapper contracts** and **extraction patterns** that enable deterministic context variable generation across the entire AgentGenerator agent chain.

---

## Core Principles

### 1. **Semantic Wrappers = Versioned APIs**

Every agent output is wrapped in a **PascalCase semantic key** that acts as a stable API contract:

```json
{
  "TechnicalBlueprint": { ... },      // WorkflowArchitectAgent output
  "PhaseAgents": [ ... ],              // WorkflowImplementationAgent output
  "ContextVariablesPlan": { ... },    // ContextVariablesAgent output
  "tools": [ ... ],                    // ToolsManagerAgent output
  "ActionPlan": { ... }                // ActionPlanArchitect output
}
```

**Why this matters**:
- ✅ Agents reference **"TechnicalBlueprint" wrapper**, not "WorkflowArchitectAgent"
- ✅ If we rename WorkflowArchitectAgent → BlueprintAgent, no prompts break
- ✅ Wrapper structure defines contract; implementation can evolve independently
- ✅ Conversation history becomes a **typed message bus** with named schemas

### 2. **Explicit Extraction Instructions**

Every agent prompt MUST include:
- **What to extract** (field paths with dot notation)
- **How to access** (semantic wrapper → navigation path)
- **Why it matters** (what the agent uses this data for)

**Anti-Pattern** ❌:
```
"Use context variables from the blueprint"
```

**Correct Pattern** ✅:
```
**UPSTREAM OUTPUT EXTRACTION**:
1. Scan conversation history for message containing 'TechnicalBlueprint' key
2. Navigate to: message.content['TechnicalBlueprint']['global_context_variables']
3. Extract: name, type (six-type taxonomy), trigger_hint, purpose
4. Use for: Generating full ContextVariableDefinition with source-specific fields
```

### 3. **Zero Implicit State**

Agents MUST NOT assume:
- ❌ "The workflow has 3 phases" (must extract from ActionPlan)
- ❌ "There are 5 agents" (must extract from PhaseAgents)
- ❌ "This is a routing pattern" (must extract from PatternSelection)
- ❌ "User wants approval gates" (must extract from phase.human_in_loop)

Every decision MUST be traceable to conversation history or system message.

---

## Database-Aware Context Variable Generation

### The Problem: Database Schema Visibility

**Challenge**: Agents need to make intelligent decisions about data_reference and data_entity variables, but they're stateless - they don't "know" what collections exist in the user's database.

**Solution**: Runtime provides **schema_overview context variable** when `CONTEXT_AWARE=true`:

```python
# Runtime automatically loads schema when enabled
if os.getenv("CONTEXT_AWARE", "").lower() in {"1", "true"}:
    schema_info = await _get_database_schema_async(database_name)
    context.set("schema_overview", schema_info["schema_overview"])
    context.set("database_schema_available", True)
```

**What Agents Receive** (via UpdateSystemMessage injection):

```
DATABASE: customer_db
TOTAL COLLECTIONS: 8

COLLECTIONS:
1. Users
   - Fields: user_id (str), email (str), subscription_tier (str), created_at (datetime)
   - Indexes: user_id (unique)

2. Orders
   - Fields: order_id (str), user_id (str), amount (float), status (str)
   - Indexes: order_id (unique), user_id

3. Products
   - Fields: product_id (str), name (str), price (float), stock (int)
   - Indexes: product_id (unique)
```

---

### Three Database Scenarios

#### Scenario 1: Existing Collection (data_reference)

**When to Use**:
- Collection ALREADY EXISTS in schema_overview
- Workflow needs to READ existing data
- No writes back to collection

**Agent Decision Logic** (ContextVariablesAgent):

```
STEP 1: Check if variable.purpose mentions "existing", "query", "lookup", "reference"
STEP 2: Extract collection name from purpose (e.g., "Query Users collection for tier")
STEP 3: Validate collection exists in schema_overview
STEP 4: IF collection exists:
   → source.type = "data_reference"
   → Set database_name, collection from schema_overview
   → Design query_template based on available fields
   → Set refresh_strategy = "once" (default)
STEP 5: IF collection missing:
   → Log warning in agent_message
   → Create data_entity instead (workflow will create collection)
```

**Example Output**:

```json
{
  "customer_tier": {
    "type": "string",
    "description": "User's subscription tier from existing Users collection",
    "source": {
      "type": "data_reference",
      "database_name": "customer_db",
      "collection": "Users",
      "query_template": {"user_id": "${user_id}"},
      "field": "subscription_tier",
      "refresh_strategy": "once"
    }
  }
}
```

**Runtime Behavior** (from `variables.py`):

```python
async def _load_data_reference_value(name, definition, *, default_database_name, app_id, context):
    source = definition.source
    collection = source.collection
    db_name = source.database_name or default_database_name
    
    # Query existing collection
    client = get_mongo_client()
    query = _materialize_query_template(source.query_template, context, app_id)
    projection = {field: 1 for field in (source.fields or [])} or None
    doc = await client[db_name][collection].find_one(query, projection)
    
    # Extract field or return whole document
    if source.field:
        return doc.get(source.field) if doc else None
    return doc
```

---

#### Scenario 2: Workflow-Owned Collection (data_entity)

**When to Use**:
- Workflow CREATES and OWNS this collection
- Collection may or may not exist yet
- Workflow writes to collection (create/update documents)

**Agent Decision Logic** (ContextVariablesAgent):

```
STEP 1: Check if variable.purpose mentions "create", "persist", "save", "write", "audit", "workflow output"
STEP 2: Extract collection intent (e.g., "Create WorkflowAudits for tracking")
STEP 3: Check if collection exists in schema_overview
STEP 4: IF collection exists:
   → Inspect existing schema from schema_overview
   → Match schema fields to workflow requirements
   → Add any missing fields to schema definition
STEP 5: IF collection missing:
   → Design schema from scratch based on workflow needs
   → source.type = "data_entity"
   → Set write_strategy based on urgency (immediate vs deferred)
STEP 6: Always set search_by to primary identifier field
```

**Example Output** (Collection Exists):

```json
{
  "order_status": {
    "type": "object",
    "description": "Order status tracking (existing Orders collection)",
    "source": {
      "type": "data_entity",
      "database_name": "customer_db",
      "collection": "Orders",
      "search_by": "order_id",
      "schema": {
        "order_id": "string",
        "user_id": "string",
        "amount": "float",
        "status": "string",
        "workflow_updated_at": "datetime"
      },
      "write_strategy": "immediate"
    }
  }
}
```

**Example Output** (Collection Missing - Will Be Created):

```json
{
  "workflow_audit": {
    "type": "object",
    "description": "Audit trail for workflow execution (new collection)",
    "source": {
      "type": "data_entity",
      "collection": "WorkflowAudits",
      "search_by": "workflow_id",
      "schema": {
        "workflow_id": "string",
        "user_id": "string",
        "started_at": "datetime",
        "status": "string",
        "phases_completed": "array"
      },
      "indexes": [
        {"workflow_id": 1},
        {"user_id": 1, "started_at": -1}
      ],
      "write_strategy": "on_workflow_end"
    }
  }
}
```

**Runtime Behavior** (from `data_entity.py`):

```python
class DataEntityManager:
    """Runtime helper for creating and updating workflow-owned collections."""
    
    async def create(self, data: Dict[str, Any]) -> Dict[str, Any]:
        """Insert a new document honoring schema validation and write strategy."""
        doc = self._validate_payload(data)
        
        if self._write_strategy == "immediate":
            # Write immediately to database
            await self._collection.insert_one(doc)
        else:
            # Buffer write for later flush (on_phase_transition or on_workflow_end)
            self._pending.append(_PendingWrite("insert", doc))
        
        return doc
    
    async def flush(self):
        """Persist pending writes for deferred strategies."""
        for item in self._pending:
            if item.operation == "insert":
                await self._collection.insert_one(item.payload)
            elif item.operation == "update":
                await self._collection.update_one(
                    {self._search_by: item.search_value},
                    {"$set": item.payload},
                    upsert=False
                )
```

---

#### Scenario 3: Mixed Collections (data_reference + data_entity)

**When to Use**:
- Workflow reads from existing collection
- AND writes to different workflow-owned collection
- Common pattern: Read user data → Process → Write results

**Agent Decision Logic** (ContextVariablesAgent):

```
STEP 1: Identify read operations → data_reference variables
STEP 2: Identify write operations → data_entity variables
STEP 3: Link variables via computation logic
STEP 4: Ensure query_template in data_reference uses fields available in schema_overview
STEP 5: Ensure data_entity schema includes fields needed for downstream queries
```

**Example: Order Processing Workflow**

```json
{
  "definitions": {
    "customer_tier": {
      "type": "string",
      "description": "Read customer tier from existing Users collection",
      "source": {
        "type": "data_reference",
        "database_name": "customer_db",
        "collection": "Users",
        "query_template": {"user_id": "${user_id}"},
        "field": "subscription_tier",
        "refresh_strategy": "once"
      }
    },
    "discount_amount": {
      "type": "float",
      "description": "Calculate discount based on customer tier",
      "source": {
        "type": "computed",
        "computation": "calculate_tier_discount",
        "inputs": ["customer_tier", "order_amount"],
        "output_type": "float",
        "persist_to": null
      }
    },
    "order_summary": {
      "type": "object",
      "description": "Write processed order summary to workflow collection",
      "source": {
        "type": "data_entity",
        "collection": "ProcessedOrders",
        "search_by": "order_id",
        "schema": {
          "order_id": "string",
          "user_id": "string",
          "customer_tier": "string",
          "original_amount": "float",
          "discount_amount": "float",
          "final_amount": "float",
          "processed_at": "datetime"
        },
        "write_strategy": "immediate"
      }
    }
  }
}
```

**Data Flow**:
```
1. Runtime loads customer_tier (data_reference → query Users collection)
2. Agent calculates discount_amount (computed → uses customer_tier input)
3. Tool writes order_summary (data_entity → DataEntityManager.create())
4. Result persisted to ProcessedOrders collection
```

---

### Schema-Aware Agent Prompts

#### WorkflowArchitectAgent Enhancement

**Current [CONTEXT] Section** includes:
```
- **Database Schema (if CONTEXT_AWARE=true)**:
   - Available via schema_overview context variable
   - Lists collections, fields, indexes from user's MongoDB
   - Use to validate data_reference queries and data_entity writes
```

**Enhanced Extraction Instructions**:

```
**Step 3.5 - Analyze Database Schema (if available)**:

IF database_schema_available == true:
  - Review schema_overview for existing collections
  - For EACH phase that needs database access:
    * Check if required collection exists
    * Verify required fields are present
    * Design context variables accordingly

For READ operations (query existing data):
  - Collection exists → Use data_reference type
  - Collection missing → Warn in blueprint OR use data_entity (workflow creates it)

For WRITE operations (persist workflow data):
  - Collection exists → Use data_entity type, match existing schema
  - Collection missing → Use data_entity type, design schema from scratch
  - Always specify search_by field for updates

For MIXED operations:
  - Separate read (data_reference) from write (data_entity) variables
  - Link via computed variables if calculation needed
```

#### ContextVariablesAgent Enhancement

**Add to [INSTRUCTIONS] Step 2B**:

```
**STEP 2B-EXTENDED - Database Schema Validation**:

For data_reference type:
  1. Extract collection name from variable.purpose
  2. Check if schema_overview context variable is available
  3. IF schema_overview available:
     a. Search schema_overview for collection name
     b. IF collection found:
        - Extract available fields from schema
        - Validate query_template uses valid fields
        - Set database_name from schema
        - Set refresh_strategy = "once" (default)
     c. IF collection NOT found:
        - Log warning: "Collection {name} not found in schema"
        - Consider switching to data_entity (workflow will create)
        - Include note in agent_message
  4. IF schema_overview NOT available:
     - Use database_name from TechnicalBlueprint
     - Trust collection/field names without validation
     - Set refresh_strategy = "once"

For data_entity type:
  1. Extract collection name from variable.purpose
  2. Check if schema_overview context variable is available
  3. IF schema_overview available:
     a. Search schema_overview for collection name
     b. IF collection found:
        - Extract existing schema fields
        - Merge with workflow-required fields
        - Preserve existing indexes
        - Note: "Extending existing collection"
     c. IF collection NOT found:
        - Design schema from scratch
        - Create indexes for search_by and common queries
        - Note: "Creating new collection"
  4. IF schema_overview NOT available:
     - Design schema based on workflow requirements
     - Create standard indexes (primary key)
  5. Always set search_by to primary identifier field
  6. Choose write_strategy:
     - "immediate" for critical updates (payment status, errors)
     - "on_phase_transition" for phase summaries
     - "on_workflow_end" for audit logs
```

---

### Practical Examples

#### Example 1: Customer Support Ticket Workflow

**schema_overview Input**:
```
DATABASE: support_db
COLLECTIONS:
1. Users - user_id, email, tier, created_at
2. Tickets - ticket_id, user_id, subject, status, created_at
```

**Agent Output**:
```json
{
  "definitions": {
    "customer_tier": {
      "type": "string",
      "source": {
        "type": "data_reference",
        "database_name": "support_db",
        "collection": "Users",
        "query_template": {"user_id": "${user_id}"},
        "field": "tier"
      }
    },
    "ticket_status": {
      "type": "object",
      "source": {
        "type": "data_entity",
        "collection": "Tickets",
        "search_by": "ticket_id",
        "schema": {
          "ticket_id": "string",
          "user_id": "string",
          "subject": "string",
          "status": "string",
          "priority": "string",
          "workflow_stage": "string",
          "updated_at": "datetime"
        },
        "write_strategy": "immediate"
      }
    },
    "agent_notes": {
      "type": "object",
      "source": {
        "type": "data_entity",
        "collection": "TicketNotes",
        "search_by": "note_id",
        "schema": {
          "note_id": "string",
          "ticket_id": "string",
          "agent_name": "string",
          "note_text": "string",
          "created_at": "datetime"
        },
        "write_strategy": "immediate"
      }
    }
  }
}
```

**Rationale**:
- `customer_tier`: Existing Users collection → data_reference (read-only)
- `ticket_status`: Existing Tickets collection → data_entity (workflow updates status)
- `agent_notes`: New TicketNotes collection → data_entity (workflow creates notes)

---

#### Example 2: Report Generation Workflow (No Existing Collections)

**schema_overview Input**:
```
DATABASE: reports_db
COLLECTIONS: (empty - new database)
```

**Agent Output**:
```json
{
  "definitions": {
    "report_config": {
      "type": "object",
      "source": {
        "type": "config",
        "env_var": "REPORT_CONFIG",
        "default": {"format": "pdf", "include_charts": true}
      }
    },
    "report_data": {
      "type": "object",
      "source": {
        "type": "data_entity",
        "collection": "Reports",
        "search_by": "report_id",
        "schema": {
          "report_id": "string",
          "user_id": "string",
          "report_type": "string",
          "generated_at": "datetime",
          "file_url": "string",
          "status": "string"
        },
        "indexes": [
          {"report_id": 1},
          {"user_id": 1, "generated_at": -1}
        ],
        "write_strategy": "immediate"
      }
    },
    "report_audit": {
      "type": "object",
      "source": {
        "type": "data_entity",
        "collection": "ReportAudits",
        "search_by": "audit_id",
        "schema": {
          "audit_id": "string",
          "report_id": "string",
          "action": "string",
          "timestamp": "datetime"
        },
        "write_strategy": "on_workflow_end"
      }
    }
  }
}
```

**Rationale**:
- No existing collections → All data_entity (workflow creates everything)
- `report_data`: Immediate writes (users see status updates)
- `report_audit`: Deferred writes (batch at end for efficiency)

---

### Validation Rules

#### Rule 4: Schema Consistency Check

**Check**: data_reference variables MUST reference existing collections (if schema available)

**Validation**:
```python
def validate_data_reference_collections(definitions, schema_overview):
    if not schema_overview:
        return  # Skip validation if schema not available
    
    available_collections = extract_collection_names(schema_overview)
    
    for name, defn in definitions.items():
        if defn["source"]["type"] == "data_reference":
            collection = defn["source"]["collection"]
            
            if collection not in available_collections:
                raise ValueError(
                    f"data_reference variable '{name}' references non-existent "
                    f"collection '{collection}'. Available: {available_collections}"
                )
```

#### Rule 5: data_entity Schema Completeness

**Check**: data_entity variables MUST have schema and search_by defined

**Validation**:
```python
def validate_data_entity_schema(definitions):
    for name, defn in definitions.items():
        if defn["source"]["type"] == "data_entity":
            source = defn["source"]
            
            if not source.get("search_by"):
                raise ValueError(
                    f"data_entity variable '{name}' missing required field: search_by"
                )
            
            schema = source.get("schema", {})
            if not schema:
                raise ValueError(
                    f"data_entity variable '{name}' missing schema definition"
                )
            
            # Ensure search_by field is in schema
            if source["search_by"] not in schema:
                raise ValueError(
                    f"data_entity variable '{name}': search_by field "
                    f"'{source['search_by']}' not in schema"
                )
```

---

## Semantic Wrapper Contracts

### 1. ActionPlan (ActionPlanArchitect Output)

**Wrapper Key**: `ActionPlan.workflow`

**Schema Contract**:
```json
{
  "ActionPlan": {
    "workflow": {
      "phases": [
        {
          "phase_index": 0,
          "phase_name": "Phase 1: Discovery",
          "phase_description": "...",
          "human_in_loop": true,
          "agents_needed": "sequential"
        }
      ],
      "pattern": ["Pipeline"],
      "trigger": "chat",
      "initiated_by": "user"
    }
  }
}
```

**What Context Variable Agents Extract**:
- `phases.length` → Number of phase state variables needed (current_phase_index, phase_0_completed, etc.)
- `phases[].human_in_loop=true` → Need approval_status, approval_feedback variables
- `pattern[]` → Pattern-specific coordination variables (routing_confidence, escalation_tier, iteration_count)
- `trigger` + `initiated_by` → Determines if workflow needs trigger-related context variables

**Extraction Pattern**:
```
Step 1: Scan conversation history for 'ActionPlan' key
Step 2: Navigate to message.content['ActionPlan']['workflow']
Step 3: Extract phases[], pattern[], trigger, initiated_by
Step 4: Design state variables based on extracted structure
```

---

### 2. TechnicalBlueprint (WorkflowArchitectAgent Output)

**Wrapper Key**: `TechnicalBlueprint.global_context_variables`

**Schema Contract**:
```json
{
  "TechnicalBlueprint": {
    "global_context_variables": [
      {
        "name": "approval_status",
        "type": "state",
        "trigger_hint": "Set when user approves action plan",
        "purpose": "Tracks approval gate decisions for routing"
      }
    ],
    "ui_components": [ ... ],
    "before_chat_lifecycle": { ... },
    "after_chat_lifecycle": { ... }
  }
}
```

**What Context Variable Agents Extract**:
- `global_context_variables[].name` → Variable identifier
- `global_context_variables[].type` → Six-type taxonomy (config, data_reference, data_entity, computed, state, external)
- `global_context_variables[].trigger_hint` → When/how variable gets set (used to design triggers)
- `global_context_variables[].purpose` → Why variable exists (used for description field)

**Critical**: WorkflowArchitectAgent outputs **high-level specifications**. ContextVariablesAgent adds **implementation details** (source fields, query templates, transitions).

**Extraction Pattern**:
```
Step 1: Scan conversation history for 'TechnicalBlueprint' key
Step 2: Navigate to message.content['TechnicalBlueprint']['global_context_variables']
Step 3: For EACH variable:
   - Extract: name, type (six-type), trigger_hint, purpose
   - Apply decision algorithm to determine source.type and populate type-specific fields
   - Design triggers based on trigger_hint (agent_text or ui_response)
```

---

### 3. PhaseAgents (WorkflowImplementationAgent Output)

**Wrapper Key**: `PhaseAgents.phase_agents`

**Schema Contract**:
```json
{
  "PhaseAgents": [
    {
      "phase_index": 0,
      "agents": [
        {
          "agent_name": "InterviewAgent",
          "description": "...",
          "human_interaction": "context",
          "agent_tools": [ ... ],
          "lifecycle_tools": [ ... ],
          "system_hooks": [ ... ]
        }
      ]
    }
  ]
}
```

**What Context Variable Agents Extract**:
- Complete agent roster (EVERY `agents[].agent_name`) → Used to build agents[] exposure mapping
- `human_interaction` values → Determines which agents need approval/context variables
- `agent_tools[]` → Used to map which tools might set context variables
- `lifecycle_tools[]` → Used to identify state transition points

**Extraction Pattern**:
```
Step 1: Scan conversation history for 'PhaseAgents' key
Step 2: Navigate to message.content['PhaseAgents']
Step 3: For EACH phase_agents[] entry:
   - Extract phase_index and agents[] array
   - For EACH agent:
     * Extract agent_name (REQUIRED for exposure mapping)
     * Extract human_interaction (determines variable access needs)
     * Extract agent_tools[] (may set context variables)
Step 4: Build agents[] object mapping agent names to variable arrays
```

---

### 4. ToolsManifest (Tools + Lifecycle Tools)

**Wrapper Key**: `tools` and `lifecycle_tools` (root-level arrays)

**Schema Contract**:
```json
{
  "tools": [
    {
      "agent": "InterviewAgent",
      "function": "action_plan_approval",
      "tool_type": "UI_Tool",
      "auto_invoke": true,
      "ui": {
        "component": "ApprovalGate",
        "display": "artifact"
      }
    }
  ],
  "lifecycle_tools": [
    {
      "function": "initialize_context",
      "trigger": "before_chat"
    }
  ]
}
```

**What Context Variable Agents Extract**:
- `tools[].function` → Tool names for ui_response triggers
- `tools[].tool_type=UI_Tool` → Identifies tools that may set context variables
- `tools[].auto_invoke=true` → Tool automatically called when structured output emitted
- `tools[].ui.component` → React component type (determines UI pattern)

**Extraction Pattern**:
```
Step 1: Scan conversation history for message with 'tools' array at root level
Step 2: For EACH tools[] entry:
   - If tool_type == "UI_Tool" AND auto_invoke == true:
     * This tool likely sets a context variable
     * Extract function name → becomes trigger.tool in ui_response trigger
     * Create corresponding state variable with ui_response trigger
```

---

### 5. PatternSelection (PatternAgent Output)

**Wrapper Key**: `PatternSelection`

**Schema Contract**:
```json
{
  "PatternSelection": {
    "is_multi_workflow": false,
    "decomposition_reason": null,
    "pack_name": "Customer Support Router",
    "workflows": [
      {
        "name": "CustomerSupportRouter",
        "role": "primary",
        "description": "Routes support requests to the right specialists",
        "pattern_id": 1,
        "pattern_name": "Context-Aware Routing"
      }
    ]
  }
}
```

**What Context Variable Agents Extract**:
- `workflows[current_workflow_index].pattern_name` (or `.pattern_id`) → Determines pattern-specific coordination variables:
  - **Pipeline**: stage trackers (current_stage, stage_X_completed)
  - **Context-Aware Routing**: routing metadata (current_domain, routing_confidence, routed_specialist)
  - **Escalation**: tier tracking (active_tier, escalation_count, recovery_confidence)
  - **Feedback Loop**: iteration tracking (iteration_count, max_iterations, approval_gate_status)
  - **Hierarchical**: delegation tracking (workstream_assignments, manager_status_updates)

**Extraction Pattern**:
```
Step 1: Scan conversation history for 'PatternSelection' key
Step 2: Select workflow = workflows[current_workflow_index] (default 0 / primary fallback)
Step 3: Extract workflow.pattern_name (or workflow.pattern_id)
Step 4: Apply pattern-specific variable templates from CONTEXT_VARIABLES_COMPLETE.md
```

---

## Agent-by-Agent Extraction Logic

### WorkflowArchitectAgent → Generates TechnicalBlueprint

**Inputs**:
- ✅ ActionPlan.workflow (phases, pattern, trigger)
- ✅ Interview transcript (user requirements)
- ✅ Pattern guidance (injected via system hooks)

**Context Variable Logic**:

1. **Count phases** → Determine if phase state variables needed:
   ```
   IF phases.length > 1:
     - Add: current_phase_index (state, default=0)
     - Add: phase_0_completed, phase_1_completed, ... (state, default=false)
   ```

2. **Identify approval gates** → Check for human_in_loop:
   ```
   IF ANY phase has human_in_loop=true:
     - Add: approval_status (state, default="pending")
     - Add: approval_feedback (state, default=null)
     - Add: approved_by (data_reference from Users collection)
   ```

3. **Apply pattern templates** → Add pattern-specific variables:
   ```
   IF pattern contains "Context-Aware Routing":
     - Add: current_domain (state)
     - Add: routing_confidence (computed)
     - Add: routed_specialist (state)
   ```

4. **Output specification** (NOT full implementation):
   ```json
   {
     "name": "approval_status",
     "type": "state",
     "trigger_hint": "Set when user approves action plan",
     "purpose": "Tracks approval gate decisions for routing"
   }
   ```

**Key**: WorkflowArchitectAgent outputs **WHAT variables exist**, not HOW they're implemented.

---

### ContextVariablesAgent → Generates ContextVariablesPlan

**Inputs**:
- ✅ TechnicalBlueprint.global_context_variables (high-level specs)
- ✅ PhaseAgents (complete agent roster)
- ✅ ToolsManifest.tools (UI tool names)
- ✅ ActionPlan.workflow (phase structure, pattern)

**Context Variable Logic**:

**STEP 1 - Extract TechnicalBlueprint Variables**:
```
For EACH global_context_variables[] entry:
  - Read: name, type (six-type), trigger_hint, purpose
  - Store for processing in Step 2
```

**STEP 2 - Apply Decision Algorithm** (per CONTEXT_VARIABLES_SIX_TYPE_ALIGNMENT.md):

```
For EACH variable from Step 1:
  
  STEP 2A - Analyze Purpose:
    - Read variable.purpose and variable.trigger_hint
    - Identify keywords:
      * "environment", "feature flag" → config candidate
      * "existing", "query", "lookup" → data_reference candidate
      * "create", "persist", "save" → data_entity candidate
      * "calculate", "compute", "derive" → computed candidate
      * "phase", "approval", "routing" → state candidate
      * "API", "Stripe", "external" → external candidate
  
  STEP 2B - Choose Source Type:
    IF keywords match config pattern:
      → source.type = "config"
      → Populate: env_var (UPPER_SNAKE_CASE), default, required
    
    ELSE IF keywords match data_reference pattern:
      → source.type = "data_reference"
      → Populate: database_name, collection, query_template, field, refresh_strategy
      → Validate collection exists in schema_overview (if CONTEXT_AWARE=true)
    
    ELSE IF keywords match data_entity pattern:
      → source.type = "data_entity"
      → Populate: collection, search_by, schema, write_strategy
    
    ELSE IF keywords match computed pattern:
      → source.type = "computed"
      → Populate: computation, inputs, output_type, persist_to
    
    ELSE IF keywords match state pattern:
      → source.type = "state"
      → Populate: default, transitions[], persist
      → Design triggers from trigger_hint
    
    ELSE IF keywords match external pattern:
      → source.type = "external"
      → Populate: service, operation, params, auth, cache, retry
  
  STEP 2C - Design Triggers (state variables only):
    IF trigger_hint mentions "agent emits" OR "agent says":
      → Add agent_text trigger:
         * agent: AgentName (from PhaseAgents)
         * match: {equals: "TOKEN"} or {contains: "keyword"}
    
    IF trigger_hint mentions "user clicks" OR "UI response":
      → Add ui_response trigger:
         * tool: function_name (from ToolsManifest)
         * response_key: variable_name
```

**STEP 3 - Extract Agent Roster**:
```
Scan PhaseAgents for ALL agent_name values:
  - Build complete list: ["InterviewAgent", "PlannerAgent", "ExecutorAgent", ...]
  - This is the canonical agent roster
```

**STEP 4 - Map Agent Exposure**:
```
For EACH agent from Step 3:
  - Determine which variables this agent needs to READ
  - Rules:
    * Phase management agents → current_phase_index, phase_X_completed
    * Approval agents → approval_status, approval_feedback
    * Routing agents → current_domain, routing_confidence
    * All agents → platform flags (context_aware, monetization_enabled)
  - Populate agents[AgentName].variables = [...]
```

**STEP 5 - Validate Completeness**:
```
Check:
  ✓ All variables have source.type from six-type taxonomy
  ✓ Type-specific required fields present
  ✓ State variables have triggers[] if applicable
  ✓ agents[] object contains ALL agents from PhaseAgents
  ✓ No duplicate variable names
```

**Output**:
```json
{
  "ContextVariablesPlan": {
    "definitions": {
      "approval_status": {
        "type": "string",
        "description": "Tracks approval gate decisions for routing",
        "source": {
          "type": "state",
          "default": "pending",
          "transitions": [
            {
              "from": "pending",
              "to": "approved",
              "trigger": {
                "type": "ui_response",
                "tool": "action_plan_approval",
                "response_key": "approval_status"
              }
            }
          ],
          "persist": false
        }
      }
    },
    "agents": {
      "InterviewAgent": {
        "variables": ["context_aware", "monetization_enabled"]
      },
      "PlannerAgent": {
        "variables": ["approval_status", "current_phase_index"]
      }
    }
  }
}
```

**Key**: ContextVariablesAgent transforms **specifications into full implementations** with source fields, triggers, and agent exposure.

---

### AgentToolsFileGenerator → Generates Tool Code

**Inputs**:
- ✅ ContextVariablesPlan.definitions (with six-type source)
- ✅ ToolsManifest.tools (tool specs)
- ✅ PhaseAgents (agent tool ownership)

**Context Variable Logic**:

**STEP 1 - Identify Variable Types**:
```
For EACH tool being generated:
  - Check ToolSpec.parameters for context variable references
  - Look up variable in ContextVariablesPlan.definitions
  - Extract source.type (config, data_reference, data_entity, computed, state, external)
```

**STEP 2 - Choose Access Pattern**:
```
IF source.type == "config":
  → Generate: import os; value = os.getenv("VAR_NAME", default)

IF source.type == "data_reference":
  → Generate: from core.db import get_db
              db = get_db()[database_name][collection]
              doc = db.find_one(query_template)
              value = doc.get(field)

IF source.type == "data_entity":
  → Generate: from core.db import get_db
              db = get_db()[collection]
              db.update_one(filter, update, upsert=True)

IF source.type == "computed":
  → Generate: value = computation_function(*inputs)
              # Optionally persist to database if persist_to specified

IF source.type == "state":
  → Generate: from core.workflow.context import get_context_variable, set_context_variable
              value = get_context_variable(workflow_context, "var_name")
              set_context_variable(workflow_context, "var_name", new_value)

IF source.type == "external":
  → Generate: import httpx
              async with httpx.AsyncClient() as client:
                  response = await client.get(url, headers=auth)
              # Cache result per TTL
```

**STEP 3 - Generate Code**:
```python
# Example: UI tool that sets state variable
async def approval_gate_tool(workflow_context: dict, plan_data: dict) -> dict:
    """Approval gate for action plan review."""
    
    # Variable: approval_status (type=state, source.type=state)
    # Access pattern: set_context_variable
    from core.workflow.context import set_context_variable
    
    response = await use_ui_tool(
        tool_id="approval_gate",
        component="ApprovalGate",
        payload=plan_data,
        wait_for_response=True
    )
    
    approval_status = response.get("approval_status")
    
    if approval_status:
        set_context_variable(workflow_context, "approval_status", approval_status)
    
    return {"status": "complete", "decision": approval_status}
```

**Key**: AgentToolsFileGenerator uses **source.type to determine code generation pattern** - NO hardcoding.

---

### HandoffsAgent → Generates Handoff Rules

**Inputs**:
- ✅ ActionPlan.workflow (phase structure, flow)
- ✅ ContextVariablesPlan (state variables with triggers)
- ✅ PhaseAgents (agent roster, handoff order)

**Context Variable Logic**:

**STEP 1 - Extract Phase Flow**:
```
Read ActionPlan.workflow.phases[] to determine linear phase progression
```

**STEP 2 - Extract State Variables**:
```
Scan ContextVariablesPlan.definitions for:
  - Variables with source.type == "state"
  - Variables with transitions[] array populated
```

**STEP 3 - Generate Conditional Handoffs**:
```
For EACH state variable with transitions:
  
  For EACH transition:
    
    IF trigger.type == "agent_text":
      → Create handoff:
         * handoff_type: "condition"
         * condition_type: "expression"
         * condition_scope: null (post-reply evaluation)
         * condition: "${variable_name} == '<to_value>'"
      
      Pattern: Agent emits text → DerivedContextManager updates variable → 
               Post-reply check evaluates condition → Routes to target
    
    IF trigger.type == "ui_response":
      → Create handoff:
         * handoff_type: "condition"
         * condition_type: "expression"
         * condition_scope: "pre" (pre-reply evaluation, re-checks every turn)
         * condition: "${variable_name} == '<to_value>'"
      
      Pattern: User interacts with UI → Tool sets variable → 
               Pre-reply check catches it immediately → Routes to target
```

**Example Output**:
```json
{
  "handoff_rules": [
    {
      "source_agent": "InterviewAgent",
      "target_agent": "PlannerAgent",
      "handoff_type": "condition",
      "condition": "${interview_complete} == true",
      "condition_type": "expression",
      "condition_scope": null
    },
    {
      "source_agent": "PlannerAgent",
      "target_agent": "ExecutorAgent",
      "handoff_type": "condition",
      "condition": "${approval_status} == 'approved'",
      "condition_type": "expression",
      "condition_scope": "pre"
    }
  ]
}
```

**Key**: HandoffsAgent uses **trigger.type to determine condition_scope** (agent_text → null, ui_response → "pre").

---

## Critical Extraction Patterns

### Pattern 1: Agent Roster Extraction

**Purpose**: Build complete agent exposure mapping

**Source**: PhaseAgents wrapper

**Extraction**:
```
Step 1: Locate 'PhaseAgents' key in conversation history
Step 2: Navigate to message.content['PhaseAgents']
Step 3: For EACH phase_agents[] entry:
   - For EACH agents[] sub-entry:
     * Extract agent_name (PascalCase)
     * Add to canonical agent list
Step 4: Validate EVERY agent appears in ContextVariablesPlan.agents[] object
```

**Why Critical**: Missing an agent from exposure mapping breaks UpdateSystemMessage injection.

---

### Pattern 2: UI Tool → Context Variable Mapping

**Purpose**: Identify which UI tools set which state variables

**Sources**: 
- ToolsManifest.tools (tool specs)
- TechnicalBlueprint.global_context_variables (variable specs with trigger_hint)

**Extraction**:
```
Step 1: Find all UI_Tool entries with auto_invoke=true
Step 2: For EACH UI_Tool:
   - Extract function name (snake_case)
Step 3: Find corresponding state variable in TechnicalBlueprint:
   - Match trigger_hint containing tool context
   - Example: trigger_hint="Set when user approves action plan" 
     → Look for UI_Tool with approval/accept in name
Step 4: Create ui_response trigger:
   - trigger.type = "ui_response"
   - trigger.tool = function_name
   - trigger.response_key = variable_name
```

**Why Critical**: ui_response triggers enable UI-driven state transitions without hardcoding.

---

### Pattern 3: Pattern-Specific Variable Templates

**Purpose**: Add coordination variables based on orchestration pattern

**Source**: PatternSelection.pattern_name OR ActionPlan.workflow.pattern[]

**Extraction**:
```
Step 1: Extract pattern_name from PatternSelection OR pattern[] from ActionPlan
Step 2: Apply pattern-specific templates:

IF pattern == "Context-Aware Routing":
  Add variables:
    - current_domain (state, default=null)
    - routing_confidence (computed, inputs=[...])
    - routed_specialist (state, default=null)

IF pattern == "Escalation":
  Add variables:
    - active_tier (state, default="tier_1")
    - escalation_count (state, default=0)
    - recovery_confidence (computed)

IF pattern == "Feedback Loop":
  Add variables:
    - iteration_count (state, default=0)
    - max_iterations (config, env_var="MAX_ITERATIONS", default=3)
    - approval_gate_status (state, default="pending")

IF pattern == "Pipeline":
  Add variables:
    - current_stage (state, default="stage_1")
    - stage_1_completed, stage_2_completed, ... (state, default=false)

IF pattern == "Hierarchical":
  Add variables:
    - workstream_assignments (data_entity, collection="Workstreams")
    - manager_status_updates (data_entity, collection="StatusUpdates")
```

**Why Critical**: Patterns have coordination contracts that require specific context variables.

---

## Validation Rules

### Rule 1: Agent Roster Completeness

**Check**: `ContextVariablesPlan.agents` MUST contain EVERY agent from `PhaseAgents`

**Validation**:
```python
def validate_agent_roster(context_plan, phase_agents):
    plan_agents = set(context_plan["agents"].keys())
    phase_agent_names = set()
    
    for phase in phase_agents:
        for agent in phase["agents"]:
            phase_agent_names.add(agent["agent_name"])
    
    missing = phase_agent_names - plan_agents
    if missing:
        raise ValueError(f"Missing agents in exposure mapping: {missing}")
```

---

### Rule 2: Trigger Consistency

**Check**: State variables with transitions MUST have valid triggers

**Validation**:
```python
def validate_state_triggers(definitions):
    for name, defn in definitions.items():
        if defn["source"]["type"] == "state":
            transitions = defn["source"].get("transitions", [])
            
            for transition in transitions:
                trigger = transition["trigger"]
                
                if trigger["type"] == "agent_text":
                    assert trigger.get("agent") is not None
                    assert trigger.get("match") is not None
                    assert trigger.get("tool") is None
                    assert trigger.get("response_key") is None
                
                elif trigger["type"] == "ui_response":
                    assert trigger.get("tool") is not None
                    assert trigger.get("response_key") is not None
                    assert trigger.get("agent") is None
                    assert trigger.get("match") is None
```

---

### Rule 3: Type-Specific Field Presence

**Check**: Each source.type has required fields populated

**Validation**:
```python
REQUIRED_FIELDS = {
    "config": ["env_var", "default"],
    "data_reference": ["database_name", "collection", "query_template"],
    "data_entity": ["collection", "search_by", "write_strategy"],
    "computed": ["computation", "inputs", "output_type"],
    "state": ["default"],
    "external": ["service", "operation", "params", "auth"]
}

def validate_source_fields(definitions):
    for name, defn in definitions.items():
        source_type = defn["source"]["type"]
        required = REQUIRED_FIELDS[source_type]
        
        for field in required:
            if field not in defn["source"]:
                raise ValueError(
                    f"Variable '{name}' (type={source_type}) missing required field: {field}"
                )
```

---

## Anti-Patterns to Avoid

### ❌ Anti-Pattern 1: Hardcoded Agent Lists

**Wrong**:
```json
"agents": {
  "InterviewAgent": {"variables": [...]},
  "PlannerAgent": {"variables": [...]}
}
// What if WorkflowImplementationAgent added ExecutorAgent?
```

**Right**:
```
STEP 1: Extract ALL agent names from PhaseAgents
STEP 2: For EACH extracted agent, create agents[] entry
STEP 3: Populate variables[] based on agent role
```

---

### ❌ Anti-Pattern 2: Implicit Type Selection

**Wrong**:
```
"If variable tracks phases, use state type"
// No explicit extraction or decision logic
```

**Right**:
```
STEP 1: Read variable.purpose from TechnicalBlueprint
STEP 2: Check for keywords: "phase", "tracking", "coordination"
STEP 3: IF keywords match state pattern → source.type = "state"
STEP 4: Populate default value and transitions[] array
```

---

### ❌ Anti-Pattern 3: Assuming Variable Existence

**Wrong**:
```
"Use the approval_status variable for routing"
// What if WorkflowArchitectAgent didn't create it?
```

**Right**:
```
STEP 1: Check if 'approval_status' exists in TechnicalBlueprint.global_context_variables
STEP 2: IF exists → Extract specs and implement
STEP 3: IF missing → Check if ANY phase has human_in_loop=true
STEP 4: IF human_in_loop=true but no approval variable → Create it
```

---

## Implementation Checklist

### Phase 1: Schema Foundation ✅
- [x] Update structured_outputs.json with six-type literals
- [x] Add type-specific fields (refresh_strategy, write_strategy, etc.)
- [x] Validate JSON syntax

### Phase 2: ContextVariablesAgent Prompt ✅
- [x] Update [CONTEXT] with six-type taxonomy table (✅ Complete with database schema validation)
- [x] Add decision matrix and semantic clarifications (✅ Six-type table in [GUIDELINES])
- [x] Update [INSTRUCTIONS] with 9-step decision algorithm (✅ Database-aware logic included)
- [x] Replace [OUTPUT FORMAT] with six-type examples (✅ All 6 types with field requirements matrix)
- [x] Add field requirements matrix (✅ Complete table in [OUTPUT FORMAT])

### Phase 3: Downstream Consumers ✅
- [x] Update AgentToolsFileGenerator [RUNTIME INTEGRATION] with six-type access patterns (✅ Complete table)
- [x] Update HandoffsAgent [CONTEXT] with state/computed terminology (✅ Line 688 updated)
- [x] Update HandoffsAgent [INSTRUCTIONS] trigger logic (✅ Step 3 updated to state/computed)

### Phase 4: Upstream Verification ✅
- [x] Verify WorkflowArchitectAgent outputs six-type values (✅ [OUTPUT FORMAT] line 195 uses six-type union)
- [x] Check pattern guidance examples use six types (✅ COMPLETE - All 45 legacy 'derived' instances migrated)
- [x] Validate UIFileGenerator doesn't need changes (✅ State variable integration already documented in [RUNTIME INTEGRATION])

### Phase 5: End-to-End Validation
- [ ] Test complete chain: ActionPlan → TechnicalBlueprint → ContextVariablesPlan → Tools → Handoffs
- [ ] Verify agent roster completeness
- [ ] Validate trigger consistency
- [ ] Confirm alignment with CONTEXT_VARIABLES_COMPLETE.md

---
