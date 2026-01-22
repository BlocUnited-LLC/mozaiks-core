# AG2 Pattern Guidance

> **Last Verified Against**: AG2 Pattern Cookbook (docs.ag2.ai/latest/docs/user-guide/advanced-concepts/pattern-cookbook/)
> **Verification Date**: 2025-06-28

This document provides comprehensive guidance on the 9 AG2 orchestration patterns used in MozaiksAI workflow generation. Each pattern defines how agents coordinate, communicate, and hand off work while remaining modular, workflow-agnostic, and as stateless as possible.

**Where to find runnable examples**
- The generator’s `update_agent_state_pattern.py` hook should load guidance from this file plus the examples above to keep prompts hot-swappable and declarative.

---

## Pattern Selection Matrix

| ID | Pattern | Best For | Agent Count | Complexity | Routing |
|----|---------|----------|-------------|------------|---------|
| 1 | Context-Aware Routing | Multi-domain request handling | 4-6 | Medium | `OnContextCondition` |
| 2 | Escalation | Tiered support with confidence thresholds | 3-5 | Medium | Confidence < threshold |
| 3 | Feedback Loop | Iterative refinement with quality gates | 4-6 | Medium | `iteration_needed` flag |
| 4 | Hierarchical | Executive→Manager→Specialist delegation | 5-10 | High | `AfterWork` to supervisor |
| 5 | Organic | Emergent, description-based collaboration | 3-6 | Low | AutoPattern + GroupManagerTarget |
| 6 | Pipeline | Sequential stage-by-stage processing | 4-8 | Low | Linear `AgentNameTarget` |
| 7 | Redundant | Parallel execution + evaluation | 3-5 | Medium | NestedChatTarget isolation |
| 8 | Star | Hub-and-spoke coordination | 4-8 | Medium | Hub→Spoke→Hub cycle |
| 9 | Triage with Tasks | Task decomposition + sequential execution | 4-8 | High | TaskManager routing |

---

## 1. Context-Aware Routing

### When to Use
- User requests vary widely in type or domain (tech, finance, healthcare, etc.)
- Need intelligent classification before processing
- Different specialists handle different request types

### Agent Roles (AG2 Official)
| Role | Name Example | Purpose |
|------|--------------|---------|
| Router | `router_agent` | Analyzes content, determines domain, routes to specialists |
| Specialist | `tech_specialist` | Handles technology domain requests |
| Specialist | `finance_specialist` | Handles financial domain requests |
| Specialist | `healthcare_specialist` | Handles medical domain requests |
| Fallback | `general_specialist` | Handles unclassified or multi-domain requests |

### Typical Handoff Structure
```
User → router_agent → OnContextCondition(current_domain=="technology") → tech_specialist → User
                    → OnContextCondition(current_domain=="finance") → finance_specialist → User
                    → OnContextCondition(current_domain=="healthcare") → healthcare_specialist → User
                    → general_specialist (fallback) → User
```

### Context Variables (AG2 Official)
- `current_domain`: The classified domain (technology, finance, healthcare, general)
- `previous_domains`: List of previously handled domains (for multi-turn context)
- `domain_confidence`: Confidence score for routing decision
- `question_answered`: Boolean - has the question been fully answered

### Common Tools (AG2 Official)
- `analyze_request`: Analyzes input, sets `current_domain` in cozntext
- `route_to_tech_specialist`: Handoff to tech domain
- `route_to_finance_specialist`: Handoff to finance domain
- `provide_*_response`: Domain-specific response tool

---

## 2. Escalation

### When to Use
- Tiered support or incident response
- Issues that may need higher-level intervention
- Confidence-threshold-driven escalation

### Agent Roles (AG2 Official)
| Role | Name Example | Purpose |
|------|--------------|---------|
| Triage | `triage_agent` | Receives new questions, routes to basic |
| Basic | `basic_agent` | First-level response, escalates if confidence < 8/10 |
| Intermediate | `intermediate_agent` | Second-level, escalates if confidence < 8/10 |
| Advanced | `advanced_agent` | Expert-level resolution |

### Typical Handoff Structure
```
User → triage_agent → basic_agent → (confidence < 8?) → intermediate_agent → (confidence < 8?) → advanced_agent → User
```

### Context Variables (AG2 Official)
- `basic_agent_confidence`: 1-10 confidence score from basic tier
- `intermediate_agent_confidence`: 1-10 confidence score from intermediate tier
- `advanced_agent_confidence`: 1-10 confidence score from advanced tier
- `escalation_count`: Number of escalations that have occurred
- `last_escalation_reason`: Why the last escalation happened (complexity, unknown, policy)

### Common Tools (AG2 Official)
- `new_question_asked`: Marks a new question, resets escalation tracking
- `answer_question_basic`: Returns `ConsideredResponse(confidence, answer)`
- `answer_question_intermediate`: Returns `ConsideredResponse(confidence, answer)`
- `answer_question_advanced`: Returns `ConsideredResponse(confidence, answer)`

### Key Mechanism
Uses Pydantic `ConsideredResponse` model with confidence scoring. If `confidence < 8`, the tool triggers escalation to the next tier automatically via `ReplyResult(target=AgentTarget(next_tier_agent))`.

---

## 3. Feedback Loop

### When to Use
- Content that needs iterative refinement
- Quality assurance workflows requiring multiple review/revision cycles
- Documents, proposals, or creative content with quality gates

### Agent Roles (AG2 Official)
| Role | Name Example | Purpose |
|------|--------------|---------|
| Entry | `entry_agent` | Receives initial request, starts workflow |
| Planner | `planning_agent` | Creates document plan/outline |
| Drafter | `drafting_agent` | Produces initial content |
| Reviewer | `review_agent` | Evaluates quality, provides feedback |
| Reviser | `revision_agent` | Incorporates feedback into revision |
| Finalizer | `finalization_agent` | Polishes and delivers final output |

### Typical Handoff Structure
```
User → entry_agent → planning_agent → drafting_agent → review_agent 
                                                    ↓
                     ← revision_agent ← (iteration_needed=True)
                                                    ↓
                     → finalization_agent → User (iteration_needed=False)
```

### Context Variables (AG2 Official)
- `current_stage`: One of: planning, drafting, review, revision, finalization
- `current_iteration`: Number of review/revision cycles completed
- `max_iterations`: Maximum allowed iterations (default 3)
- `iteration_needed`: Boolean - does the draft need another revision?
- `document_draft`: Current version of the document
- `feedback_collection`: Accumulated feedback from reviewers

### Common Tools (AG2 Official)
- `start_document_creation`: Initiates workflow, sets `current_stage`
- `submit_document_plan`: Planner submits outline
- `submit_document_draft`: Drafter submits content
- `submit_feedback`: Reviewer provides critique + sets `iteration_needed`
- `submit_revised_document`: Reviser submits updated draft
- `finalize_document`: Final polish and delivery

---

## 4. Hierarchical

### When to Use
- Large, complex tasks requiring decomposition
- Need multiple levels of oversight (Executive → Manager → Specialist)
- Organizational workflows with delegation and aggregation

### Agent Roles (AG2 Official)
| Level | Name Example | Purpose |
|-------|--------------|---------|
| Executive | `executive_agent` | Top-level planning, delegates to managers, synthesizes final output |
| Manager | `renewable_manager` | Manages domain specialists, aggregates section results |
| Manager | `storage_manager` | Manages domain specialists, aggregates section results |
| Manager | `alternative_manager` | Manages domain specialists, aggregates section results |
| Specialist | `solar_specialist` | Deep expertise in narrow area |
| Specialist | `wind_specialist` | Deep expertise in narrow area |
| Specialist | `hydro_specialist` | Deep expertise in narrow area |

### Typical Handoff Structure
```
User → executive_agent 
           → renewable_manager → solar_specialist → renewable_manager
                              → wind_specialist → renewable_manager
           → storage_manager → hydro_specialist → storage_manager
                             → geothermal_specialist → storage_manager
           → alternative_manager → biofuel_specialist → alternative_manager
       ← executive_agent (compile_final_report) → User
```

### Context Variables (AG2 Official)
- `task_started`: Boolean - has the work begun
- `task_completed`: Boolean - is all work complete
- `manager_a_completed`: Boolean - has manager A finished
- `specialist_a1_completed`: Boolean - has specialist A1 finished
- `{domain}_research`: String - stored research content from specialists
- `report_sections`: Dict - compiled sections by manager
- `final_report`: String - synthesized final output

### Common Tools (AG2 Official)
- `initiate_research`: Executive starts the process
- `complete_solar_research`: Specialist submits findings + returns to manager
- `compile_renewable_section`: Manager aggregates specialist work
- `compile_final_report`: Executive synthesizes all sections

### Key Mechanism
Uses `OnContextCondition` with `ExpressionContextCondition` for deterministic routing. Each specialist uses `AfterWork(AgentTarget(manager))` to return to their supervisor. Managers use `AfterWork(AgentTarget(executive))` to bubble up.

---

## 5. Organic

### When to Use
- Exploratory or creative tasks
- Agents should self-organize based on descriptions
- Minimal structure, maximum flexibility
- Unpredictable conversation flow

### Agent Roles (AG2 Official)
| Role | Name Example | Purpose |
|------|--------------|---------|
| Initial | `project_manager` | Starts the process, coordinates |
| Contributor | `developer` | Code implementation, technical solutions |
| Contributor | `qa_engineer` | Testing strategies, quality assurance |
| Contributor | `ui_ux_designer` | Design decisions, user experience |
| Contributor | `technical_writer` | Documentation, technical specifications |

### Typical Handoff Structure
```
User → project_manager ↔ (GroupChatManager selects based on descriptions) → User
```

### Context Variables
- Minimal - routing is LLM-based, not context-variable-based
- Shared context can still be used but doesn't drive routing

### Common Tools
- No explicit handoff tools - GroupChatManager uses agent `description` fields
- Each agent has domain-specific tools for their expertise

### Key Mechanism
Uses `AutoPattern` with `GroupManagerTarget`. The Group Chat Manager uses agent `description` fields (not `system_message`) to intelligently route based on conversation content. **No handoffs are defined** - all routing is LLM-based.

---

## 6. Pipeline

### When to Use
- Well-defined sequential stages
- Each stage transforms input and passes to next
- Clear dependencies between steps (e.g., e-commerce order processing)

### Agent Roles (AG2 Official)
| Stage | Name Example | Purpose |
|-------|--------------|---------|
| Entry | `entry_agent` | Receives order, starts pipeline |
| Stage 1 | `validation_agent` | Validates order correctness |
| Stage 2 | `inventory_agent` | Checks item availability |
| Stage 3 | `payment_agent` | Processes payment |
| Stage 4 | `fulfillment_agent` | Creates shipping instructions |
| Stage 5 | `notification_agent` | Sends customer confirmation |

### Typical Handoff Structure
```
User → entry_agent → validation_agent → inventory_agent → payment_agent → fulfillment_agent → notification_agent → User
```

### Context Variables (AG2 Official)
- `pipeline_started`: Boolean - has processing begun
- `pipeline_completed`: Boolean - all stages complete
- `validation_completed`: Boolean - stage 1 done
- `inventory_completed`: Boolean - stage 2 done
- `payment_completed`: Boolean - stage 3 done
- `order_details`: Dict - the order being processed
- `validation_results`: Stage output
- `has_error`: Boolean - did any stage fail
- `error_stage`: Which stage failed

### Common Tools (AG2 Official)
- `start_order_processing`: Entry receives order JSON
- `run_validation_check`: Validate order
- `complete_validation(ValidationResult)`: Submit validation, handoff to inventory
- `run_inventory_check`: Check stock
- `complete_inventory_check(InventoryResult)`: Submit, handoff to payment
- `complete_payment_processing(PaymentResult)`: Submit, handoff to fulfillment
- `complete_fulfillment(FulfillmentResult)`: Submit, handoff to notification
- `complete_notification(NotificationResult)`: Finish pipeline

### Key Mechanism
Uses Pydantic models (ValidationResult, InventoryResult, etc.) for structured outputs. Each tool returns `ReplyResult(target=AgentNameTarget("next_agent"))` for linear progression. On error, returns `RevertToUserTarget()`.

---

## 7. Redundant

### When to Use
- Need consensus or validation
- Critical decisions requiring multiple independent opinions
- Want to compare different methodologies (analytical vs creative vs comprehensive)

### Agent Roles (AG2 Official)
| Role | Name Example | Purpose |
|------|--------------|---------|
| Taskmaster | `taskmaster_agent` | Distributes task to multiple workers |
| Worker A | `agent_a` | Analytical/structured approach |
| Worker B | `agent_b` | Creative/lateral thinking approach |
| Worker C | `agent_c` | Comprehensive/multi-perspective approach |
| Evaluator | `evaluator_agent` | Scores all approaches, selects or synthesizes best |

### Typical Handoff Structure
```
User → taskmaster_agent → NestedChat([agent_a, agent_b, agent_c]) → evaluator_agent → User
```

### Context Variables (AG2 Official)
- `task_initiated`: Boolean - has the task started
- `task_completed`: Boolean - all workers have responded
- `evaluation_complete`: Boolean - evaluator has selected
- `current_task`: The task being processed
- `task_type`: creative, problem_solving, or factual
- `agent_a_result`: Worker A's output
- `agent_b_result`: Worker B's output
- `agent_c_result`: Worker C's output
- `evaluation_scores`: Dict with score per worker
- `final_result`: Selected or synthesized output
- `selected_approach`: Which worker won

### Common Tools (AG2 Official)
- `initiate_task(task, task_type)`: Starts parallel processing
- `evaluate_and_select(notes, score_a, score_b, score_c, result, rationale)`: Evaluates and selects winner

### Key Mechanism
Uses `NestedChatTarget` to isolate each worker - they only see the task, not each other's responses. Workers run in sequence in nested chat, results collected in context vars, then evaluator scores 1-10 and selects best.

---

## 8. Star

### When to Use
- Central coordinator with specialized workers
- Hub-and-spoke communication
- Need single point of control for task delegation and result synthesis

### Agent Roles (AG2 Official)
| Role | Name Example | Purpose |
|------|--------------|---------|
| Hub | `coordinator_agent` | Central orchestrator, delegates and synthesizes |
| Spoke | `weather_specialist` | Weather forecasts |
| Spoke | `events_specialist` | Local events and attractions |
| Spoke | `traffic_specialist` | Transportation and traffic info |
| Spoke | `food_specialist` | Dining recommendations |

### Typical Handoff Structure
```
User → coordinator_agent (analyze_query) 
           → weather_specialist → coordinator_agent
           → events_specialist → coordinator_agent
           → traffic_specialist → coordinator_agent
           → food_specialist → coordinator_agent
       ← coordinator_agent (compile_final_response) → User
```

### Context Variables (AG2 Official)
- `query_analyzed`: Boolean - has coordinator analyzed the request
- `query_completed`: Boolean - is the response ready
- `weather_info_needed`: Boolean - do we need weather
- `weather_info_completed`: Boolean - weather gathered
- `events_info_needed`: Boolean
- `events_info_completed`: Boolean
- `city`: The city being queried
- `date_range`: The date range
- `weather_info`: Gathered weather content
- `events_info`: Gathered events content
- `final_response`: Synthesized output

### Common Tools (AG2 Official)
- `analyze_query(city, date_range, needs_*)`: Coordinator analyzes and sets flags
- `provide_weather_info(content)`: Specialist submits + returns to coordinator
- `provide_events_info(content)`: Specialist submits + returns to coordinator
- `compile_final_response(content)`: Coordinator synthesizes all

### Key Mechanism
Hub uses `OnContextCondition` to route to specialists based on `*_info_needed` flags. Each specialist has `AfterWork(AgentTarget(coordinator))` to always return to hub. Coordinator uses `OnCondition` (LLM-based) as fallback for complex routing decisions.

---

## 9. Triage with Tasks

### When to Use
- Intake workflows with task decomposition
- Need to classify then execute tasks in sequence (research before writing)
- Customer service, document creation, or structured task processing

### Agent Roles (AG2 Official)
| Role | Name Example | Purpose |
|------|--------------|---------|
| Triage | `triage_agent` | Analyzes request, decomposes into typed tasks |
| Task Manager | `TaskManagerAgent` | Routes tasks, enforces sequence, tracks completion |
| Research Worker | `ResearchAgent` | Handles research-type tasks |
| Writing Worker | `WritingAgent` | Handles writing-type tasks |
| Summary | `SummaryAgent` | Consolidates all results |

### Typical Handoff Structure
```
User → triage_agent (structured output: research_tasks, writing_tasks) 
     → TaskManagerAgent (initiate_tasks) 
     → ResearchAgent (complete ALL research first) 
     → TaskManagerAgent 
     → WritingAgent (complete ALL writing second) 
     → TaskManagerAgent 
     → SummaryAgent → User
```

### Context Variables (AG2 Official)
- `TaskInitiated`: Boolean - has processing started
- `ResearchTasks`: List of research task dicts with index, topic, status, output
- `WritingTasks`: List of writing task dicts
- `CurrentResearchTaskIndex`: Current research task (-1 if done)
- `CurrentWritingTaskIndex`: Current writing task (-1 if done)
- `ResearchTasksDone`: Boolean - all research complete
- `WritingTasksDone`: Boolean - all writing complete
- `ResearchTasksCompleted`: List of completed research tasks
- `WritingTasksCompleted`: List of completed writing tasks

### Common Tools (AG2 Official)
- `initiate_tasks(research_tasks, writing_tasks)`: TaskManager initializes processing
- `complete_research_task(index, topic, findings)`: Research agent submits
- `complete_writing_task(index, topic, findings)`: Writing agent submits

### Key Mechanism
Uses Pydantic models (`ResearchTask`, `WritingTask`) for structured task decomposition. Triage agent uses `response_format=TaskAssignment` for structured output. TaskManager uses `OnContextCondition` to route to appropriate worker based on `CurrentResearchTaskIndex >= 0` or `CurrentWritingTaskIndex >= 0`. Uses `UpdateSystemMessage` hook to inject current task into worker prompts.

---

## Context Variables
## Context Variable Types

Standard context variable categories (AG2 patterns use these consistently):

| Type | Purpose | Example |
|------|---------|---------|
| `state` | Boolean workflow state tracking | `task_started`, `pipeline_completed`, `evaluation_complete` |
| `index` | Current position in sequence | `CurrentResearchTaskIndex`, `current_iteration` |
| `needed_flag` | Boolean - is this step required | `weather_info_needed`, `needs_approval` |
| `completed_flag` | Boolean - is this step done | `weather_info_completed`, `validation_completed` |
| `content` | String content for downstream | `weather_info`, `document_draft`, `final_report` |
| `collection` | Dict/List - accumulated results | `report_sections`, `ResearchTasksCompleted`, `evaluation_scores` |

---

## Handoff Primitives (AG2 Official)

| Primitive | Description | Use Case |
|-----------|-------------|----------|
| `AgentTarget(agent)` | Direct handoff to specific agent | Explicit routing |
| `AgentNameTarget("name")` | Handoff by agent name string | Pipeline stages |
| `RevertToUserTarget()` | Return control to user | Terminal states, errors |
| `TerminateTarget()` | End the workflow | Completion |
| `StayTarget()` | Agent retains control | Retry logic |
| `NestedChatTarget(config)` | Isolated sub-conversation | Redundant pattern workers |
| `GroupManagerTarget()` | Let GroupChatManager decide | Organic pattern |

---

## Condition Types (AG2 Official)

| Condition | Description | Use Case |
|-----------|-------------|----------|
| `OnContextCondition` | Deterministic, context-variable-based | Most patterns |
| `ExpressionContextCondition` | Uses `ContextExpression` strings | Complex boolean logic |
| `OnCondition` | LLM-based decision | Fallback routing |
| `StringLLMCondition` | LLM interprets a string | Nuanced decisions |
| `ExpressionAvailableCondition` | Controls when handoff is visible | Gating based on state |

---

## AG2 Pattern Implementation Classes

| Pattern | AG2 Class | Description |
|---------|-----------|-------------|
| 1-4, 6-9 | `DefaultPattern` | Most patterns use this with explicit handoffs |
| 5 (Organic) | `AutoPattern` | LLM-based routing via GroupChatManager |

---

## Pattern ID Quick Reference

```python
PATTERN_ID_BY_NAME = {
    "contextawarerouting": 1,
    "escalation": 2,
    "feedbackloop": 3,
    "hierarchical": 4,
    "organic": 5,
    "pipeline": 6,
    "redundant": 7,
    "star": 8,
    "triagewithtasks": 9,
}

PATTERN_DISPLAY_NAME_BY_ID = {
    1: "Context-Aware Routing",
    2: "Escalation",
    3: "Feedback Loop",
    4: "Hierarchical",
    5: "Organic",
    6: "Pipeline",
    7: "Redundant",
    8: "Star",
    9: "Triage with Tasks",
}
```

---

## Usage in Generation

The `update_agent_state_pattern.py` hook:
1. Loads the selected pattern's example from `docs/pattern_examples/`
2. Injects it as context for downstream agents
3. Agents use the example structure to guide their output

This ensures generated workflows follow proven patterns while adapting to specific use cases.

---

## Mapping to ACTION_PLAN_SCHEMA_V2

Each pattern example in `docs/pattern_examples/` shows a complete single-module workflow with:

| Schema Section | Pattern-Specific Content |
|----------------|-------------------------|
| `modules[].agents` | Agent roles from pattern (name, system_message, description) |
| `modules[].context_variables` | Pattern context vars with types |
| `modules[].handoffs` | `OnContextCondition` / `OnCondition` definitions |
| `modules[].tools` | Pattern-typical tools with Pydantic models |
| `modules[].hooks` | `update_agent_state_before_reply` for dynamic prompts |
| `modules[].orchestrator` | `DefaultPattern` or `AutoPattern` config |

---

## Pattern Combination (V2 Multi-Module)

In V2 multi-module workflows, patterns can be combined:

```
Module 1 (Triage with Tasks) → Module 2 (Pipeline) → Module 3 (Feedback Loop)
```

Each module uses one pattern internally, but modules chain together for complex workflows.

