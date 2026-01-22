# Pattern Use Cases (Source of Truth)

> **Purpose**: Concrete use cases for each AG2 pattern to guide PatternAgent selection and provide consistent test scenarios.
> **Date**: December 14, 2025

---

## Pattern Selection Signals

Before diving into use cases, here are the **user language signals** that suggest each pattern:

| Pattern | User Says Things Like... |
|---------|--------------------------|
| 1. Context-Aware Routing | "route to", "different types of", "categorize", "specialist for each" |
| 2. Escalation | "try first then escalate", "tiers", "levels of support", "if it can't handle" |
| 3. Feedback Loop | "review and revise", "iterate until", "approval cycle", "quality check" |
| 4. Hierarchical | "manager oversees", "delegate to team", "executive summary", "coordinate specialists" |
| 5. Organic | "brainstorm", "explore ideas", "creative session", "let them figure it out" |
| 6. Pipeline | "step by step", "stages", "first X then Y then Z", "sequential process" |
| 7. Redundant | "compare approaches", "multiple opinions", "pick the best", "consensus" |
| 8. Star | "gather info from", "central coordinator", "collect from specialists", "hub" |
| 9. Triage with Tasks | "break down into tasks", "research first then write", "decompose", "task list" |

---

## 1. Context-Aware Routing

### Primary Use Case: IT Support Bot

**User Prompt Example:**
> "I want a support bot that can handle different types of IT requests - some are about hardware, some are software issues, some are network problems, and some are account/access issues. Each type should go to a specialist."

**Why This Pattern:**
- Requests vary by domain (hardware, software, network, access)
- Need classification before processing
- Different specialists handle different request types
- Single entry point, multiple exit paths

**Key Agents:**
| Agent | Purpose |
|-------|---------|
| RouterAgent | Analyzes request, sets `current_domain`, routes |
| HardwareSpecialist | Handles hardware issues (printers, monitors, etc.) |
| SoftwareSpecialist | Handles software issues (apps, crashes, etc.) |
| NetworkSpecialist | Handles network issues (WiFi, VPN, connectivity) |
| AccessSpecialist | Handles account/access issues (passwords, permissions) |
| GeneralSpecialist | Fallback for unclear requests |

**Expected Context Variables:**
- `current_domain`: hardware | software | network | access | general
- `request_summary`: Brief description of the issue
- `question_answered`: Boolean - resolution complete

**Test Scenario:**
```
User: "My laptop won't connect to the office WiFi"
Expected: Router → NetworkSpecialist → Response about WiFi troubleshooting
```

### Alternative Use Cases:
- **Customer Service Center**: Routes to billing, technical support, returns, general inquiries
- **Medical Intake**: Routes to primary care, specialist referral, emergency, pharmacy
- **Legal Help Desk**: Routes to contracts, employment, IP, general counsel

---

## 2. Escalation

### Primary Use Case: Technical Support Tiers

**User Prompt Example:**
> "I need a support system where basic questions are handled by a first-level agent, but if they're not confident, it escalates to a senior agent, and if still not resolved, to an expert."

**Why This Pattern:**
- Tiered confidence-based escalation
- Try simpler/cheaper resources first
- Complex issues bubble up to experts
- Clear escalation path

**Key Agents:**
| Agent | Purpose |
|-------|---------|
| TriageAgent | Receives new questions, starts at Level 1 |
| Level1Agent | Basic support, escalates if confidence < 8 |
| Level2Agent | Intermediate support, escalates if confidence < 8 |
| Level3Agent | Expert support, final tier |

**Expected Context Variables:**
- `level1_confidence`: 1-10 score
- `level2_confidence`: 1-10 score
- `level3_confidence`: 1-10 score
- `escalation_count`: Number of escalations
- `escalation_reason`: Why escalated (complexity, policy, unknown)

**Test Scenario:**
```
User: "How do I reset my password?"
Expected: Triage → Level1 → Confident answer (no escalation)

User: "I'm getting error 0x80070005 when trying to install updates on a domain-joined machine with WSUS configured"
Expected: Triage → Level1 → Level2 → Level3 → Expert resolution
```

### Alternative Use Cases:
- **Medical Triage**: Nurse → Doctor → Specialist
- **Financial Advisory**: Junior Advisor → Senior Advisor → Portfolio Manager
- **Bug Triage**: Auto-response → Developer → Senior Engineer

---

## 3. Feedback Loop

### Primary Use Case: Blog Post Creation

**User Prompt Example:**
> "I want a workflow that creates blog posts with a review cycle - draft it, review it, revise based on feedback, and keep iterating until it's approved."

**Why This Pattern:**
- Content requires iterative refinement
- Quality gates between stages
- Multiple review/revision cycles possible
- Clear approval checkpoint

**Key Agents:**
| Agent | Purpose |
|-------|---------|
| EntryAgent | Receives topic, starts workflow |
| PlanningAgent | Creates outline/structure |
| DraftingAgent | Writes initial content |
| ReviewAgent | Evaluates quality, provides feedback |
| RevisionAgent | Incorporates feedback |
| FinalizationAgent | Final polish, SEO, formatting |

**Expected Context Variables:**
- `current_stage`: planning | drafting | review | revision | finalization
- `current_iteration`: Number of review cycles (max 3)
- `iteration_needed`: Boolean - needs another pass?
- `document_draft`: Current version
- `feedback_collection`: Accumulated feedback

**Test Scenario:**
```
User: "Write a blog post about remote work productivity tips"
Expected: Entry → Planning (outline) → Drafting → Review (feedback) → Revision → Review (approved) → Finalization → Final post
```

### Alternative Use Cases:
- **Contract Review**: Draft → Legal Review → Revise → Approval
- **Marketing Copy**: Draft → Brand Review → Revise → Approval
- **Code Review**: Write → Review → Revise → Merge

---

## 4. Hierarchical

### Primary Use Case: Research Report Generation

**User Prompt Example:**
> "I need a system where an executive agent coordinates multiple research managers, each managing their own specialists, to produce a comprehensive report on renewable energy."

**Why This Pattern:**
- Large, complex task requiring decomposition
- Multiple levels of oversight
- Delegation and aggregation flow
- Organizational structure needed

**Key Agents:**
| Agent | Purpose |
|-------|---------|
| ExecutiveAgent | Top-level planning, delegates, synthesizes final report |
| RenewableManager | Manages solar, wind specialists |
| StorageManager | Manages battery, grid specialists |
| PolicyManager | Manages regulation, incentive specialists |
| SolarSpecialist | Deep expertise in solar energy |
| WindSpecialist | Deep expertise in wind energy |
| BatterySpecialist | Deep expertise in energy storage |
| RegulationSpecialist | Deep expertise in energy policy |

**Expected Context Variables:**
- `task_started`: Boolean
- `task_completed`: Boolean
- `renewable_section_completed`: Boolean
- `storage_section_completed`: Boolean
- `solar_research`: String content
- `wind_research`: String content
- `report_sections`: Dict of compiled sections
- `final_report`: Synthesized output

**Test Scenario:**
```
User: "Create a comprehensive report on the state of renewable energy in 2025"
Expected: Executive → RenewableManager → Solar/Wind Specialists → Manager compiles → Executive → StorageManager → ... → Final synthesized report
```

### Alternative Use Cases:
- **Product Launch**: Executive → Marketing/Engineering/Sales Managers → Specialists
- **Due Diligence Report**: Lead → Financial/Legal/Technical Managers → Analysts
- **Strategic Plan**: CEO Agent → Department Heads → Team Leads

---

## 5. Organic

### Primary Use Case: Startup Idea Brainstorm

**User Prompt Example:**
> "I want a creative brainstorming session where different perspectives - business, technical, design, marketing - can freely discuss and build on each other's ideas."

**Why This Pattern:**
- Exploratory/creative task
- No predetermined flow
- Agents self-organize based on conversation
- Maximum flexibility needed

**Key Agents:**
| Agent | Purpose |
|-------|---------|
| FacilitatorAgent | Starts discussion, keeps it productive |
| BusinessStrategist | Business model, market fit perspective |
| TechnicalArchitect | Technical feasibility, implementation |
| DesignThinker | User experience, product design |
| MarketingExpert | Go-to-market, positioning |

**Expected Context Variables:**
- Minimal - routing is LLM-based
- Optional: `discussion_topic`, `key_ideas_collected`

**Test Scenario:**
```
User: "Let's brainstorm ideas for an AI-powered fitness app"
Expected: Facilitator kicks off → Agents naturally jump in based on conversation relevance → Emergent discussion → Ideas synthesized
```

### Alternative Use Cases:
- **Design Sprint**: Cross-functional team explores solutions
- **Problem Solving Session**: Multiple perspectives on a challenge
- **Creative Writing Room**: Characters/plot developed collaboratively

---

## 6. Pipeline

### Primary Use Case: E-commerce Order Processing

**User Prompt Example:**
> "I need an order processing system that goes through validation, inventory check, payment processing, fulfillment, and notification - in that exact order."

**Why This Pattern:**
- Well-defined sequential stages
- Each stage transforms and passes to next
- Clear dependencies (can't ship before payment)
- Linear flow

**Key Agents:**
| Agent | Purpose |
|-------|---------|
| EntryAgent | Receives order, starts pipeline |
| ValidationAgent | Validates order correctness |
| InventoryAgent | Checks stock availability |
| PaymentAgent | Processes payment |
| FulfillmentAgent | Creates shipping instructions |
| NotificationAgent | Sends confirmation |

**Expected Context Variables:**
- `pipeline_started`: Boolean
- `pipeline_completed`: Boolean
- `validation_completed`: Boolean
- `inventory_completed`: Boolean
- `payment_completed`: Boolean
- `fulfillment_completed`: Boolean
- `order_details`: Dict
- `has_error`: Boolean
- `error_stage`: Which stage failed

**Test Scenario:**
```
User: "Process order #12345 for 2x Widget A"
Expected: Entry → Validation ✓ → Inventory ✓ → Payment ✓ → Fulfillment ✓ → Notification → "Order confirmed, shipping in 2 days"
```

### Alternative Use Cases:
- **Loan Application**: Application → Credit Check → Underwriting → Approval → Disbursement
- **Document Processing**: Ingest → Extract → Transform → Validate → Load
- **Onboarding Flow**: Registration → Verification → Profile Setup → Welcome

---

## 7. Redundant

### Primary Use Case: Investment Analysis

**User Prompt Example:**
> "I want to get investment analysis from three different approaches - a conservative analyst, an aggressive growth analyst, and a balanced analyst - then have an evaluator pick the best recommendation."

**Why This Pattern:**
- Need multiple independent perspectives
- Compare different methodologies
- Select or synthesize best approach
- Critical decision requiring validation

**Key Agents:**
| Agent | Purpose |
|-------|---------|
| TaskmasterAgent | Distributes analysis task to all analysts |
| ConservativeAnalyst | Risk-averse, capital preservation approach |
| GrowthAnalyst | Aggressive, high-return focus |
| BalancedAnalyst | Moderate risk/reward approach |
| EvaluatorAgent | Scores all approaches, selects best |

**Expected Context Variables:**
- `task_initiated`: Boolean
- `task_completed`: Boolean
- `evaluation_complete`: Boolean
- `current_task`: The investment to analyze
- `conservative_result`: Analysis output
- `growth_result`: Analysis output
- `balanced_result`: Analysis output
- `evaluation_scores`: Dict with scores
- `selected_approach`: Winner
- `final_recommendation`: Selected/synthesized output

**Test Scenario:**
```
User: "Analyze whether to invest $10,000 in NVIDIA stock"
Expected: Taskmaster → [Conservative: "Hold, wait for dip", Growth: "Buy aggressively", Balanced: "DCA over 3 months"] → Evaluator scores → "Recommended: Balanced approach - DCA over 3 months"
```

### Alternative Use Cases:
- **Design Competition**: Multiple designers, pick best
- **Solution Comparison**: Multiple approaches to a problem
- **Code Review Panel**: Multiple reviewers, consensus

---

## 8. Star

### Primary Use Case: Travel Planning Assistant

**User Prompt Example:**
> "I want a travel assistant where a coordinator gathers information from weather, events, transportation, and dining specialists to give me a complete trip recommendation."

**Why This Pattern:**
- Central hub coordinates specialists
- Hub-and-spoke communication
- Specialists return to coordinator
- Single synthesis point

**Key Agents:**
| Agent | Purpose |
|-------|---------|
| CoordinatorAgent | Analyzes query, delegates, synthesizes |
| WeatherSpecialist | Weather forecasts for destination |
| EventsSpecialist | Local events and attractions |
| TransportSpecialist | Flights, trains, car rentals |
| DiningSpecialist | Restaurant recommendations |

**Expected Context Variables:**
- `query_analyzed`: Boolean
- `query_completed`: Boolean
- `weather_info_needed`: Boolean
- `weather_info_completed`: Boolean
- `events_info_needed`: Boolean
- `events_info_completed`: Boolean
- `destination`: City/region
- `date_range`: Travel dates
- `weather_info`: Gathered content
- `events_info`: Gathered content
- `transport_info`: Gathered content
- `dining_info`: Gathered content
- `final_response`: Synthesized recommendation

**Test Scenario:**
```
User: "Plan a weekend trip to Austin, Texas next month"
Expected: Coordinator (analyzes: needs weather, events, dining) → Weather → Coordinator → Events → Coordinator → Dining → Coordinator → "Here's your Austin weekend plan: Weather will be 75°F, check out SXSW events, try Franklin BBQ..."
```

### Alternative Use Cases:
- **Event Planning**: Coordinator + Venue/Catering/Entertainment/Logistics specialists
- **Home Buying**: Coordinator + Mortgage/Inspection/Legal/Insurance specialists
- **Health Assessment**: Coordinator + Lab/Imaging/Specialist/Pharmacy consultants

---

## 9. Triage with Tasks

### Primary Use Case: Research Paper Assistant

**User Prompt Example:**
> "I need a system that takes a research topic, breaks it down into research tasks and writing tasks, completes all research first, then writes each section."

**Why This Pattern:**
- Intake requires task decomposition
- Tasks have type-based sequencing (research before writing)
- Need to track task completion
- Structured task processing

**Key Agents:**
| Agent | Purpose |
|-------|---------|
| TriageAgent | Analyzes topic, decomposes into typed tasks |
| TaskManagerAgent | Routes tasks, enforces sequence, tracks completion |
| ResearchAgent | Handles research-type tasks |
| WritingAgent | Handles writing-type tasks |
| SummaryAgent | Consolidates all results |

**Expected Context Variables:**
- `TaskInitiated`: Boolean
- `ResearchTasks`: List of {index, topic, status, output}
- `WritingTasks`: List of {index, topic, status, output}
- `CurrentResearchTaskIndex`: Current research task (-1 if done)
- `CurrentWritingTaskIndex`: Current writing task (-1 if done)
- `ResearchTasksDone`: Boolean
- `WritingTasksDone`: Boolean

**Test Scenario:**
```
User: "Write a paper on the impact of AI on healthcare"
Expected: 
  Triage decomposes:
    Research: [1. AI diagnostics, 2. AI drug discovery, 3. AI patient care]
    Writing: [1. Introduction, 2. Diagnostics section, 3. Drug discovery section, 4. Patient care section, 5. Conclusion]
  TaskManager → Research 1 → Research 2 → Research 3 → Writing 1 → Writing 2 → ... → Summary
```

### Alternative Use Cases:
- **Project Planning**: Decompose project → Research phase → Execution phase
- **Due Diligence**: Decompose into investigation areas → Research → Report
- **Content Calendar**: Decompose topics → Research each → Write each

---

## Pattern Selection Decision Tree

```
START
  │
  ├─ Does request involve routing to different specialists by content type?
  │   └─ YES → Pattern 1: Context-Aware Routing
  │
  ├─ Does request involve try-first-then-escalate logic?
  │   └─ YES → Pattern 2: Escalation
  │
  ├─ Does request involve iterative review/revision cycles?
  │   └─ YES → Pattern 3: Feedback Loop
  │
  ├─ Does request involve multiple management levels (exec → manager → specialist)?
  │   └─ YES → Pattern 4: Hierarchical
  │
  ├─ Does request involve free-form brainstorming/exploration?
  │   └─ YES → Pattern 5: Organic
  │
  ├─ Does request involve strict sequential stages?
  │   └─ YES → Pattern 6: Pipeline
  │
  ├─ Does request involve comparing multiple parallel approaches?
  │   └─ YES → Pattern 7: Redundant
  │
  ├─ Does request involve a central coordinator gathering from specialists?
  │   └─ YES → Pattern 8: Star
  │
  ├─ Does request involve decomposing into typed tasks with sequencing?
  │   └─ YES → Pattern 9: Triage with Tasks
  │
  └─ UNCLEAR → Default to Pattern 1 (Context-Aware Routing) or Pattern 6 (Pipeline)
```

---

## Test Matrix

Use this matrix to validate PatternAgent selection:

| Test Prompt | Expected Pattern |
|-------------|------------------|
| "Route support tickets to the right team" | 1 - Context-Aware Routing |
| "Handle easy questions first, escalate hard ones" | 2 - Escalation |
| "Write and review until approved" | 3 - Feedback Loop |
| "Executive coordinates managers who have specialists" | 4 - Hierarchical |
| "Brainstorm ideas freely" | 5 - Organic |
| "Process orders step by step" | 6 - Pipeline |
| "Get three opinions and pick the best" | 7 - Redundant |
| "Coordinator gathers info from specialists" | 8 - Star |
| "Break down into research tasks then writing tasks" | 9 - Triage with Tasks |

---

## Version History

| Date | Change |
|------|--------|
| 2025-12-14 | Initial creation with all 9 patterns |
