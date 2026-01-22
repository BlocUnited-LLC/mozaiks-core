# MozaiksAI Runtime System Capabilities

**Purpose**: This document defines what capabilities are built into the MozaiksAI platform runtime vs what needs to be generated as custom tools. Generator agents MUST reference this to avoid creating redundant tools.

---

## 1. NATIVE PLATFORM CAPABILITIES (DO NOT GENERATE TOOLS)

These capabilities are provided by the MozaiksAI runtime system and require NO tool generation:

### 1.1 AG2 Native Capabilities

**Image Generation** (when `image_generation_enabled: true`):
- **What it does**: Agents describe images conversationally → AG2's ImageGeneration capability automatically generates via DALL-E
- **How agents use it**: Agent emits natural language description (e.g., "Generate a vibrant thumbnail showing sunset over mountains")
- **What happens**: TextAnalyzerAgent detects intent → DalleImageGenerator calls DALL-E API → Image inserted into conversation history
- **DO NOT CREATE**: "generate_image", "generate_thumbnail", "create_visualization" tools
- **DO CREATE**: Tools that extract/save generated images (e.g., "save_thumbnail", "save_to_storage")
- **Pattern**: `image_generation_enabled: true` + `operations: ["save_thumbnail"]` (NOT ["generate_thumbnail", "save_thumbnail"])

**Code Execution** (when `code_execution_enabled: true`):
- **What it does**: Agents write and execute Python/JavaScript code in sandboxed environment
- **How agents use it**: Agent emits code blocks → AG2 executes in Docker container → Returns output
- **DO NOT CREATE**: "execute_code", "run_python", "eval_javascript" tools
- **DO CREATE**: Tools that process execution results or manage execution context

**Web Search** (when `web_search_enabled: true`):
- **What it does**: Agents search the web conversationally via Google/Bing APIs
- **How agents use it**: Agent describes query → AG2 searches → Returns results
- **DO NOT CREATE**: "search_web", "google_search", "find_information" tools
- **DO CREATE**: Tools that process/filter search results or save findings

### 1.2 Context Variable System

**Runtime Context Management**:
- **What it does**: Manages shared workflow state across agents and phases
- **Lifecycle**: Variables loaded from DB/env/static sources at workflow start, updated by agents/tools during execution
- **DO NOT CREATE**: "get_context", "set_context", "update_variables" tools
- **Platform provides**: `runtime['context_variables'].get()`, `.set()`, `.remove()` in all tool functions
- **DO CREATE**: Domain-specific tools that READ from and WRITE to context variables

**Derived Context Triggers**:
- **What it does**: Automatically updates context variables when agents emit coordination tokens (e.g., "NEXT", "PROCEED")
- **How it works**: DerivedContextManager detects agent text output → Updates context flags → Triggers handoffs
- **DO NOT CREATE**: Tools to detect "NEXT" or set workflow state flags
- **Platform provides**: Automatic detection via agent_text triggers in context schema

**UI Response Context**:
- **What it does**: Updates context variables when UI tools receive user interactions (button clicks, form submissions)
- **How it works**: UI tool code explicitly sets context → Pre-reply evaluation triggers next agent
- **DO NOT CREATE**: Generic approval/rejection tools
- **DO CREATE**: Domain-specific UI tools that capture user input and update context

### 1.3 Handoff System

**Agent Routing**:
- **What it does**: Routes conversation between agents based on context variable conditions
- **How it works**: handoffs.json defines conditional transitions → Runtime evaluates → Transfers control
- **DO NOT CREATE**: "route_to_agent", "determine_next_agent", "workflow_controller" tools
- **Platform provides**: Automatic routing via handoffs.json expressions

**Human Interaction Checkpoints**:
- **What it does**: Pauses workflow for context collection or approval based on `human_interaction` field
- **How it works**: Agent with `human_interaction: "context"` → Workflow pauses → User provides input → Continues
- **DO NOT CREATE**: Generic "ask_user", "get_approval" tools
- **DO CREATE**: Domain-specific UI tools for specific approval/input scenarios (e.g., "invoice_approval", "campaign_review")

### 1.4 Lifecycle Hooks

**Workflow Lifecycle**:
- **What it does**: Executes functions at workflow boundaries (before_chat, after_chat)
- **How it works**: lifecycle_tools in tools.json → Runtime invokes at appropriate times
- **DO NOT CREATE**: "initialize_workflow", "cleanup_workflow" tools (unless domain-specific)
- **DO CREATE**: Domain-specific lifecycle hooks (e.g., "load_user_preferences", "archive_session_data")

**Agent Lifecycle**:
- **What it does**: Executes functions before/after each agent's turn
- **How it works**: Agent-specific lifecycle_tools → Runtime invokes per agent
- **DO NOT CREATE**: Generic "log_agent_start", "log_agent_end" tools
- **Platform provides**: Built-in observability and logging
- **DO CREATE**: Domain-specific agent hooks (e.g., "cache_analysis_results", "save_checkpoint")

### 1.5 Storage & Persistence

**Conversation State**:
- **What it does**: Persists conversation history, context variables, and workflow state to MongoDB
- **How it works**: Automatic persistence after each agent turn
- **DO NOT CREATE**: "save_conversation", "persist_state" tools
- **Platform provides**: Automatic via PersistenceManager
- **DO CREATE**: Domain-specific data persistence (e.g., "save_invoice_draft", "cache_analysis")

**Resume Capability**:
- **What it does**: Resumes workflows from last checkpoint after interruption
- **How it works**: Runtime loads conversation state → Reconstructs agent group → Continues
- **DO NOT CREATE**: "resume_workflow", "load_checkpoint" tools
- **Platform provides**: Automatic via resume_groupchat module

### 1.6 Observability & Monitoring

**AG2 Runtime Logging**:
- **What it does**: Captures all agent events, tool calls, and LLM interactions
- **How it works**: Automatic instrumentation via AG2RuntimeLogger
- **DO NOT CREATE**: "log_agent_action", "track_llm_call" tools
- **Platform provides**: Comprehensive logging infrastructure

**Performance Metrics**:
- **What it does**: Tracks latency, token usage, error rates per workflow/agent
- **How it works**: Automatic via PerformanceManager
- **DO NOT CREATE**: "track_performance", "measure_latency" tools
- **Platform provides**: Built-in metrics collection

---

## 2. WHEN TO GENERATE TOOLS

Generate custom tools for:

### 2.1 Third-Party Integration Logic

**API Interactions**:
- **Example**: `send_slack_message`, `create_hubspot_contact`, `charge_mozaikspay_payment`
- **Pattern**: Tool wraps external API call, handles auth/errors, returns result
- **Description format**: "[Action] via [Service]" (e.g., "Post message via Slack")

**Data Transformation**:
- **Example**: `format_invoice_pdf`, `parse_csv_upload`, `validate_email_format`
- **Pattern**: Tool takes input → Applies business logic → Returns output
- **Description format**: "[Action] [object]" (e.g., "Format invoice as PDF")

### 2.2 Domain-Specific Business Logic

**Calculations**:
- **Example**: `calculate_tax`, `compute_discount`, `estimate_shipping`
- **Pattern**: Tool reads context → Applies domain rules → Updates context
- **Description format**: "[Calculation] [domain concept]" (e.g., "Calculate tax based on location")

**Validations**:
- **Example**: `validate_credit_card`, `check_inventory`, `verify_requirements`
- **Pattern**: Tool reads input → Validates against rules → Returns pass/fail + errors
- **Description format**: "Validate [concept] against [criteria]"

**Orchestration**:
- **Example**: `compile_report`, `build_prompt`, `merge_documents`
- **Pattern**: Tool coordinates multiple data sources → Produces unified output
- **Description format**: "[Action] [artifact] from [sources]"

### 2.3 UI Interactions (Agent-Specific)

**Approval Gates**:
- **Example**: `invoice_approval`, `campaign_review`, `contract_sign_off`
- **Pattern**: UI_Tool shows preview → User clicks Approve/Reject → Updates context
- **Description format**: "Show [artifact] and capture [decision]"

**Data Collection**:
- **Example**: `project_requirements_form`, `payment_method_selector`, `date_range_picker`
- **Pattern**: UI_Tool renders form → User provides input → Saves to context
- **Description format**: "Collect [data] from user"

**Visualization**:
- **Example**: `display_workflow_diagram`, `show_analytics_dashboard`, `render_preview`
- **Pattern**: UI_Tool renders artifact → User reviews (no decision required)
- **Description format**: "Display [artifact] for review"

### 2.4 Post-Processing AG2 Capabilities

**Image Generation Results**:
- **Example**: `save_thumbnail`, `save_to_storage`, `attach_to_document`
- **Pattern**: Tool extracts images from conversation → Saves to storage → Returns URL
- **Uses**: `extract_images_from_conversation()` utility from factory.py

**Code Execution Results**:
- **Example**: `save_analysis_output`, `cache_computation`, `export_dataset`
- **Pattern**: Tool processes execution results → Persists to DB/storage

**Search Results**:
- **Example**: `filter_search_results`, `save_research_findings`, `extract_citations`
- **Pattern**: Tool processes search output → Applies domain logic

---

## 3. TOOL NAMING CONVENTIONS

### 3.1 Operations vs Integrations

**CRITICAL DISTINCTION**:
- **operations**: Workflow-internal logic (snake_case functions) → THESE BECOME TOOLS
- **integrations**: Third-party APIs (PascalCase service names) → NEVER TOOL NAMES

**WRONG**:
```json
{
  "operations": ["Slack", "HubSpot"],
  "integrations": ["send_message", "create_contact"]
}
```

**RIGHT**:
```json
{
  "operations": ["send_message", "create_contact"],
  "integrations": ["Slack", "HubSpot"]
}
```

### 3.2 Tool Description Pattern

**Format**: "[Verb] [object] [optional: via Service]"

**Examples**:
- Agent_Tool: "Calculate tax based on location and product type"
- Agent_Tool with integration: "Post message via Slack API"
- UI_Tool: "Show invoice preview and capture approval decision"

**DO NOT**:
- Mention internal implementation details
- Include TODO markers or placeholders
- Leak credentials or API keys
- Use generic names like "process", "handle", "manage"

### 3.3 Tool Type Decision Matrix

| Responsibility | Tool Type | Reason |
|----------------|-----------|--------|
| Call external API | Agent_Tool | Backend processing, no UI |
| Transform/validate data | Agent_Tool | Pure logic, no UI |
| Calculate business metric | Agent_Tool | Computation, no UI |
| Show preview + approval | UI_Tool | User interaction required |
| Collect form data | UI_Tool | User input required |
| Display visualization | UI_Tool | User review (may skip if conversational) |
| Extract AG2-generated images | Agent_Tool | Post-processing, no UI |

---

## 4. AUTOMATION-AGNOSTIC GUIDELINES

**Challenge**: MozaiksAI supports infinite automation types (story creator, invoice generator, coding simulator, data pipeline, etc.)

**Solution**: Generator agents must be domain-agnostic while understanding RUNTIME INTEGRATION

### 4.1 General Principles

1. **Read the Action Plan domain** → Understand user's automation goal
2. **Map to RUNTIME INTEGRATION** → What does platform already do?
3. **Identify gaps** → What domain logic is missing?
4. **Generate only gap-filling tools** → Don't duplicate platform features

### 4.2 Example: Coding Simulator

**User Request**: "Create a coding simulator to train AI coding agents"

**ActionPlanArchitect Output**:
```json
{
  "agents": [
    {
      "name": "CodeChallengeAgent",
      "operations": ["generate_challenge", "define_test_cases"],
      "integrations": [],
      "code_execution_enabled": true
    },
    {
      "name": "SolutionEvaluatorAgent",
      "operations": ["run_tests", "score_solution", "provide_feedback"],
      "integrations": [],
      "code_execution_enabled": true
    }
  ]
}
```

**ToolsManagerAgent Analysis**:
- `code_execution_enabled: true` → AG2 handles code execution (NO "execute_code" tool)
- `generate_challenge` → Domain logic (YES, generate tool)
- `define_test_cases` → Domain logic (YES, generate tool)
- `run_tests` → Domain logic using execution results (YES, generate tool)
- `score_solution` → Domain logic (YES, generate tool)
- `provide_feedback` → Domain logic (YES, generate tool)

**Tools Manifest**:
```json
{
  "tools": [
    {
      "agent": "CodeChallengeAgent",
      "function": "generate_challenge",
      "description": "Generate coding challenge with difficulty level and constraints",
      "tool_type": "Agent_Tool"
    },
    {
      "agent": "CodeChallengeAgent",
      "function": "define_test_cases",
      "description": "Define test cases with inputs and expected outputs",
      "tool_type": "Agent_Tool"
    },
    {
      "agent": "SolutionEvaluatorAgent",
      "function": "run_tests",
      "description": "Execute test cases against solution using AG2 code execution",
      "tool_type": "Agent_Tool"
    }
  ]
}
```

**Key Insight**: Platform provides code execution; tools provide domain logic (challenge generation, test validation, scoring)

### 4.3 Example: Story Creator (from our actual workflow)

**User Request**: "Create visual stories with AI-generated video and thumbnails"

**ActionPlanArchitect Output**:
```json
{
  "agents": [
    {
      "name": "ThumbnailAgent",
      "operations": ["save_thumbnail"],
      "integrations": ["MongoDB"],
      "image_generation_enabled": true
    }
  ]
}
```

**ToolsManagerAgent Analysis**:
- `image_generation_enabled: true` → AG2 handles image generation (NO "generate_thumbnail" tool)
- `save_thumbnail` → Post-processing AG2 output (YES, generate tool)
- `integrations: ["MongoDB"]` → Third-party integration used by `save_thumbnail` tool

**Tools Manifest**:
```json
{
  "tools": [
    {
      "agent": "ThumbnailAgent",
      "function": "save_thumbnail",
      "description": "Extract AG2-generated thumbnail from conversation and save to MongoDB",
      "tool_type": "Agent_Tool"
    }
  ]
}
```

**Key Insight**: Platform provides image generation; tool provides storage/persistence

---

## 5. DECISION TREE FOR GENERATOR AGENTS

**CRITICAL**: Each Generator agent has a SPECIFIC responsibility in the chain. Don't mix concerns.

### 5.1 ActionPlanArchitect Decisions

```
For EACH agent in the workflow plan:

1. Does agent need AG2 capability (image gen, code exec, web search)?
   → YES: Set capability flag (image_generation_enabled: true)
   → Add POST-PROCESSING operations: ["save_thumbnail"] (NOT ["generate_thumbnail"])
   → NO: Continue

2. Does agent need runtime system feature (context, handoffs)?
   → YES: NO operations needed (runtime provides automatically)
   → Include in description/responsibilities for downstream documentation
   → NO: Continue

3. Does agent need third-party API interaction?
   → YES: Add to operations array: ["send_slack_message", "create_hubspot_contact"]
   → Add to integrations array: ["Slack", "HubSpot"]
   → NO: Continue

4. Does agent need domain-specific logic?
   → YES: Add to operations array: ["calculate_tax", "validate_email"]
   → NO: Continue

5. Does agent need user interaction?
   → YES: Set human_interaction: "context" or "approval"
   → Add operations for UI tools if custom approval needed
   → NO: Set human_interaction: "none"
```

**ActionPlanArchitect Output**: agents.json with correct flags + operations arrays

---

### 5.2 ToolsManagerAgent Decisions

**Receives**: ActionPlan with operations arrays per agent

```
For EACH operation in ActionPlan:

1. Is operation for AG2 capability generation (generate_image, execute_code)?
   → SHOULD NEVER HAPPEN (ActionPlanArchitect shouldn't create these)
   → If seen: SKIP (don't generate tool)

2. Is operation for runtime system (set_context, route_agent)?
   → SHOULD NEVER HAPPEN (ActionPlanArchitect shouldn't create these)
   → If seen: SKIP (don't generate tool)

3. Is operation for third-party API or domain logic?
   → YES: Create Agent_Tool entry in tools manifest
   → Set tool_name = operation name (snake_case)
   → Reference integration in description (if applicable)

4. Is operation for post-processing AG2 output (save_thumbnail)?
   → YES: Create Agent_Tool entry
   → Description mentions extraction pattern
```

**ToolsManagerAgent Output**: tools.json manifest mapping operations → tool specs

---

### 5.3 AgentToolsFileGenerator Decisions

**Receives**: tools.json manifest with Agent_Tool entries

```
For EACH Agent_Tool in manifest:

1. Does tool description mention "extract", "save", or "post-process" AG2 output?
   → YES: Use extract_images_from_conversation() utility
   → Import from core.workflow.agents.factory
   → Generate code that extracts and saves

2. Does tool description mention third-party integration?
   → YES: Generate API integration code
   → Use environment variables for credentials
   → Include error handling for API calls

3. Does tool description mention domain logic (calculate, validate, transform)?
   → YES: Generate business logic code
   → Include input validation
   → Return structured results

4. Does operation need workflow state?
   → YES: Use runtime['context_variables'] API
   → Read state, process, update flags
```

**AgentToolsFileGenerator Output**: Python tool implementations

---

### 5.4 AgentsAgent Decisions

**Receives**: ActionPlan + tools.json + context schema

```
For EACH agent in ActionPlan:

1. Does agent have capability flags (image_generation_enabled: true)?
   → YES: Add capability usage instructions to system message
   → Teach: Describe images conversationally (AG2 handles generation)
   → Teach: Call post-processing tool to save results

2. Does agent have operations that became tools?
   → YES: Add tool calling instructions to system message
   → Specify when to call each tool
   → Include expected parameters and return values

3. Does agent have human_interaction checkpoints?
   → YES: Add interaction instructions to system message
   → "context" → teach how to ask questions and collect info
   → "approval" → teach how to present and capture decisions

4. Does agent need context variables?
   → YES: Add context reading instructions to system message
   → List specific variables agent can access
   → Teach defensive access patterns
```

**AgentsAgent Output**: Complete runtime agent system messages

---

## 5.5 Responsibility Summary

| Agent | Decides What | Outputs What | Does NOT Decide |
|-------|--------------|--------------|-----------------|
| **ActionPlanArchitect** | Which agents, what capabilities, what operations | agents.json with flags + operations | How to implement tools |
| **ToolsManagerAgent** | operation → tool mapping, auto_tool_mode | tools.json manifest | Tool code implementation |
| **AgentToolsFileGenerator** | Tool code patterns, utility usage | Python tool files | Agent system messages |
| **AgentsAgent** | How runtime agents use capabilities/tools | System messages | What capabilities/operations exist |

**Key Principle**: ActionPlanArchitect designs the "what", downstream agents implement the "how"

---

## 6. VALIDATION CHECKLIST

Before emitting tools.json, verify:

- [ ] No tools named after integrations (Slack, HubSpot, MozaiksPay)
- [ ] No tools duplicating AG2 capabilities (generate_image, execute_code, search_web)
- [ ] No tools duplicating runtime features (set_context, route_agent, log_event)
- [ ] All operations from Action Plan have corresponding tools
- [ ] All UI_Tool entries have valid component/mode
- [ ] All descriptions are domain-specific and ≤140 chars
- [ ] auto_tool_mode correctly determined (true if agent owns ≥1 UI_Tool)

---

## 7. QUICK REFERENCE: COMMON PATTERNS

| User Says | Platform Provides | Generate Tool For |
|-----------|-------------------|-------------------|
| "Generate thumbnail" | AG2 ImageGeneration | save_thumbnail |
| "Execute Python code" | AG2 CodeExecution | process_results |
| "Search for research" | AG2 WebSearch | filter_results |
| "Get user approval" | Handoff system | domain_approval_ui |
| "Track workflow state" | Context variables | domain_state_update |
| "Send to Slack" | ❌ (integration) | send_slack_message |
| "Calculate tax" | ❌ (domain logic) | calculate_tax |
| "Show invoice preview" | ❌ (domain UI) | invoice_preview |

---

**Last Updated**: 2025-10-24  
**Maintained By**: Platform Architecture Team  
**Referenced By**: ActionPlanArchitect, ToolsManagerAgent, AgentToolsFileGenerator, UIFileGenerator
