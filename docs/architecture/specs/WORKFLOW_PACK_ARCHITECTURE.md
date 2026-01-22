# Workflow Pack Architecture

**Status**: Active  
**Date**: December 9, 2025  
**Scope**: ag2-groupchat-generator (Layer 2)  
**Goal**: Define how the AgentGenerator produces workflow packs—single or multi-workflow outputs that work together.

---

## 1. Layer Model (Context)

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│  LAYER 0: mozaiksai-runtime (standalone, zero dependencies)                     │
│  ──────────────────────────────────────────────────────────────────────────────│
│  • /core (transport, persistence, orchestration)                                │
│  • /ChatUI (React chat interface)                                               │
│  • NO workflows - just the engine                                               │
│  • This IS an app - just empty. Like a browser with no tabs.                    │
└─────────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  LAYER 1: Single Workflow (simple requests)                                     │
│  ──────────────────────────────────────────────────────────────────────────────│
│  • Runtime + ONE workflow folder                                                │
│  • Self-contained, no cross-workflow dependencies                               │
│  • Example: "IT support bot" → /workflows/ITSupportBot/                         │
│  • Still outputs a pack (for future extensibility)                              │
└─────────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│  LAYER 2: Workflow Pack (complex requests) ← WE ARE HERE                        │
│  ──────────────────────────────────────────────────────────────────────────────│
│  • Runtime + MULTIPLE workflow folders + pack metadata                          │
│  • Workflows can depend on each other                                           │
│  • Example: "IT support with KB and reporting" → 3 workflows + _pack/           │
│  • Cross-workflow state via shared context variables in DB                      │
└─────────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼ (FUTURE - not in scope for ag2-groupchat-generator)
┌─────────────────────────────────────────────────────────────────────────────────┐
│  LAYER 3-4: mozaiks / usersapp (full app generation)                            │
│  ──────────────────────────────────────────────────────────────────────────────│
│  • Requires ValueEngine + AppGenerator (not built yet)                          │
│  • App-level dependencies (auth, billing, DB schemas)                           │
│  • OUT OF SCOPE for current work                                                │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. What the AgentGenerator Produces

### Output Structure (Always a Pack)

```
/workflows/
├── ITSupportBot/           ← Primary workflow (entry point)
│   ├── agents.json
│   ├── tools.json
│   ├── context_variables.json
│   ├── structured_outputs.json
│   ├── handoffs.json
│   ├── hooks.json
│   ├── orchestrator.json
│   └── tools/
│       ├── analyze_request.py
│       └── hardware_diagnostics.py
│
├── KnowledgeBase/          ← Supporting workflow (optional, if decomposed)
│   ├── agents.json
│   ├── tools.json
│   └── ...
│
├── ReportGenerator/        ← Supporting workflow (optional, if decomposed)
│   ├── agents.json
│   └── ...
│
└── _pack/                  ← Pack-level metadata (ALWAYS present)
  └── workflow_graph.json ← How workflows connect (dependencies)
```

### Even Simple Requests Get a Pack

```
User: "IT support bot"

Output:
/workflows/
├── ITSupportBot/
│   └── ... (single workflow files)
└── _pack/
  └── workflow_graph.json ← version:2, journeys:[], gates:[] (no prerequisites)
```

This ensures:
- Consistent output format
- Future extensibility (user can add workflows later)
- Runtime can always look for `_pack/workflow_graph.json`

---

## 3. Pack Metadata Schemas

### 3.1 workflow_graph.json

**Important (current runtime behavior)**

The runtime uses two different "pack" concepts:

1) **Global pack routing (v2)**: `workflows/_pack/workflow_graph.json`
  - Schema: `workflows[]`, `journeys[]`, `gates[]`
  - Purpose: app-scoped prerequisite gating + journey auto-advance

2) **Nested/multi-workflow spawning (per-workflow)**: `workflows/<workflow_name>/_pack/workflow_graph.json`
  - Schema: coordinator-specific (e.g. `nested_chats` triggers)
  - Purpose: a parent workflow spawns child workflows based on structured output

The older `nodes/edges` trigger graph shown below is historical and is **not** the global pack routing contract in the runtime.

`manifest.json` and `shared_context.json` are not part of the runtime contract.
Packs are defined by their graph (`workflow_graph.json`) plus persisted per-user workflow state (chat_id/status).

### 3.1.1 Example

```json
{
  "pack_name": "ExamplePack",
  "version": 2,
  "workflows": [
    { "id": "ITSupportBot", "type": "primary" },
    { "id": "KnowledgeBase", "type": "supporting" },
    { "id": "ReportGenerator", "type": "supporting" }
  ],
  "journeys": [],
  "gates": []
}
```

---

## 4. Runtime Behavior

### 4.1 Workflow Handoffs (via Decomposition Logic)

The runtime handles cross-workflow transitions using `workflow_graph.json`:

```
┌─────────────────────────────────────────────────────────────────┐
│  User Chat Session                                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  User: "My laptop won't connect to WiFi"                        │
│                                                                 │
│  [ITSupportBot - NetworkSpecialist]                             │
│  Agent: "Let me check our knowledge base for WiFi issues..."    │
│                                                                 │
│  ─── Switching to Knowledge Base ───────────────────────────    │
│  (dropdown shows: ITSupportBot → KnowledgeBase)                 │
│                                                                 │
│  [KnowledgeBase - SearchAgent]                                  │
│  Agent: "Found 3 relevant articles..."                          │
│                                                                 │
│  ─── Returning to IT Support ───────────────────────────────    │
│                                                                 │
│  [ITSupportBot - NetworkSpecialist]                             │
│  Agent: "Based on KB article #123, try these steps..."          │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 4.2 Shared State (via MongoDB Context Variables)

```python
# Runtime pseudo-code for context variable watching

class SharedContextWatcher:
    """
    Watches MongoDB for context variable changes.
    When a variable owned by Workflow A changes,
    notify consuming Workflow B if it's active.
    """
    
    def __init__(self, shared_context_config):
        self.variables = shared_context_config["variables"]
        self.collection = shared_context_config["db_collection"]
    
    async def watch_changes(self, app_id, chat_id):
        # MongoDB change stream on context_variables collection
        async for change in self.db.watch(
            pipeline=[{"$match": {"app_id": app_id}}]
        ):
            variable_name = change["fullDocument"]["name"]
            
            # Find consumers of this variable
            for var in self.variables:
                if var["name"] == variable_name:
                    for consumer in var["consumers"]:
                        await self.notify_workflow(consumer, change)
```

### 4.3 UI Transition Display (Declarative)

From `manifest.json`:
```json
{
  "ui_config": {
    "show_workflow_transitions": true,   // Show "Switching to X" messages
    "transition_style": "dropdown"        // dropdown | inline | hidden
  }
}
```

| Style | User Sees |
|-------|-----------|
| `dropdown` | Visual dropdown showing workflow stack (can expand/collapse) |
| `inline` | "── Switching to Knowledge Base ──" inline message |
| `hidden` | No indication (seamless, single-chat feel) |

---

## 5. AgentGenerator Agent Flow (Updated for Layer 2)

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                      PHASE 1: PLANNING (Pack-Aware)                             │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────┐                                                                │
│  │ Interview   │ ─── Captures user intent (may be complex)                      │
│  │   Agent     │                                                                │
│  └──────┬──────┘                                                                │
│         │                                                                       │
│         ▼                                                                       │
│  ┌─────────────┐                                                                │
│  │  Pattern    │ ─── NEW: Decides single vs multi-workflow                      │
│  │   Agent     │     - Simple request → 1 workflow                              │
│  │             │     - Complex request → decompose into N workflows             │
│  │             │     - Assigns pattern to EACH workflow                         │
│  └──────┬──────┘                                                                │
│         │                                                                       │
│         ▼                                                                       │
│  ┌─────────────────────────────────────────────────────────────────────────┐   │
│  │  Pack Planning (NEW)                                                     │   │
│  │  ─────────────────────────────────────────────────────────────────────   │   │
│  │  • PackStrategyAgent: Define pack structure, entry point, workflow list  │   │
│  │  • SharedContextAgent: Define cross-workflow state contracts             │   │
│  │  • WorkflowGraphAgent: Define journeys + gates (macro routing)          │   │
│  └──────┬──────────────────────────────────────────────────────────────────┘   │
│         │                                                                       │
│         ▼                                                                       │
│  ┌─────────────────────────────────────────────────────────────────────────┐   │
│  │  Per-Workflow Planning (loop for each workflow in pack)                  │   │
│  │  ─────────────────────────────────────────────────────────────────────   │   │
│  │  For workflow in pack.workflows:                                         │   │
│  │    • StateArchitectAgent (workflow-specific context vars)                │   │
│  │    • AgentRosterAgent (agents for this workflow)                         │   │
│  │    • ToolPlanningAgent (tools for this workflow)                         │   │
│  │    • UXArchitectAgent (UI for this workflow)                             │   │
│  └──────┬──────────────────────────────────────────────────────────────────┘   │
│         │                                                                       │
│         ▼                                                                       │
│  ┌─────────────┐                                                                │
│  │  Signal     │ ─── NOW: Cross-workflow signals + intra-workflow signals       │
│  │  Architect  │                                                                │
│  └──────┬──────┘                                                                │
│         │                                                                       │
│         ▼                                                                       │
│  ┌─────────────┐                                                                │
│  │ Presenter   │ ─── Shows pack overview + per-workflow visuals                 │
│  │   Agent     │                                                                │
│  └─────────────┘                                                                │
│                                                                                 │
│                        ▼ USER APPROVAL ▼                                        │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────┐
│                      PHASE 2: BUILD (Per-Workflow + Pack)                       │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  For each workflow in pack:                                                     │
│    • ContextVariablesAgent → context_variables.json                             │
│    • ToolsManagerAgent → tools.json                                             │
│    • UIFileGenerator → ui_config.json                                           │
│    • AgentToolsFileGenerator → tools/*.py                                       │
│    • StructuredOutputsAgent → structured_outputs.json                           │
│    • AgentsAgent → agents.json                                                  │
│    • HookAgent → hooks.json                                                     │
│    • HandoffsAgent → handoffs.json                                              │
│    • OrchestratorAgent → orchestrator.json                                      │
│                                                                                 │
│  Then pack-level:                                                               │
│    • ManifestGenerator → _pack/manifest.json                                    │
│    • WorkflowGraphGenerator → _pack/workflow_graph.json                         │
│    • SharedContextGenerator → _pack/shared_context.json                         │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────────┐
│                      PHASE 3: DELIVERY                                          │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                 │
│  ┌─────────────┐                                                                │
│  │  Download   │ ─── Package all workflow folders + _pack/ into ZIP             │
│  │   Agent     │                                                                │
│  └─────────────┘                                                                │
│                                                                                 │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## 6. PatternAgent Decomposition Logic

PatternAgent now has two responsibilities:

### 6.1 Decomposition Decision

```yaml
# PatternAgent Output (updated)
PatternDecision:
  is_multi_workflow: true
  decomposition_reason: "Request involves distinct functional domains: support handling, knowledge retrieval, and reporting"
  workflows:
    - name: "ITSupportBot"
      role: "primary"
      description: "Handles IT support requests and routes to specialists"
      pattern_id: 1
      pattern_name: "Context-Aware Routing"
    - name: "KnowledgeBase"
      role: "supporting"
      description: "Searches and retrieves relevant KB articles"
      pattern_id: 6
      pattern_name: "Pipeline"
    - name: "ReportGenerator"
      role: "supporting"
      description: "Generates resolution reports and analytics"
      pattern_id: 6
      pattern_name: "Pipeline"
```

### 6.2 Decomposition Heuristics

| Signal in User Request | Decomposition? | Reason |
|------------------------|----------------|--------|
| "IT support bot" | No | Single domain, self-contained |
| "IT support with knowledge base" | Yes | KB is a distinct retrieval system |
| "content pipeline with review" | No | Feedback loop is ONE workflow |
| "content pipeline with publishing and analytics" | Yes | Publishing + analytics are separate concerns |
| "multi-step approval process" | No | Hierarchical is ONE workflow |
| "approval process with audit trail and notifications" | Yes | Audit + notifications are supporting workflows |

---

## 7. Presenter Agent (Pack-Aware)

### 7.1 Pack Overview Visual

```
┌─────────────────────────────────────────────────────────────────┐
│  IT Support System (3 workflows)                                │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │              PACK OVERVIEW                               │   │
│  │                                                          │   │
│  │       ┌───────────────┐                                  │   │
│  │       │ ITSupportBot  │◀──── Entry Point                 │   │
│  │       │   (Primary)   │                                  │   │
│  │       └───────┬───────┘                                  │   │
│  │               │                                          │   │
│  │       ┌───────┴───────┐                                  │   │
│  │       ▼               ▼                                  │   │
│  │  ┌──────────┐   ┌──────────────┐                         │   │
│  │  │Knowledge │   │Report        │                         │   │
│  │  │Base      │   │AgentGenerator     │                         │   │
│  │  │(Support) │   │(Support)     │                         │   │
│  │  └──────────┘   └──────────────┘                         │   │
│  │                                                          │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
│  ▼ Click workflow to see details                                │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  ITSupportBot (Context-Aware Routing)                    │   │
│  │  ────────────────────────────────────────────────────    │   │
│  │  👤 → 🔀 Router → 🔧 Hardware                            │   │
│  │                 → 💻 Software                            │   │
│  │                 → 🌐 Network                             │   │
│  │                 → 🔒 Security                            │   │
│  └─────────────────────────────────────────────────────────┘   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 7.2 Journey (Pack-Level)

```yaml
journey:
  - step: "Describe your IT issue"
    workflow: "ITSupportBot"
    detail: "Router classifies your request"
    
  - step: "Get specialist help"
    workflow: "ITSupportBot"
    detail: "Expert walks you through troubleshooting"
    
  - step: "Knowledge lookup (if needed)"
    workflow: "KnowledgeBase"
    detail: "System searches for relevant articles"
    transition: "Switching to Knowledge Base..."
    
  - step: "Confirm resolution"
    workflow: "ITSupportBot"
    detail: "Mark issue as resolved"
    
  - step: "Report generated"
    workflow: "ReportGenerator"
    detail: "Summary report created automatically"
    transition: "Generating report..."
```

---

## 8. Next Steps

### Immediate (This Week)

- [x] **1. Update PatternAgent prompt** to include decomposition decision
  - ✅ Added `is_multi_workflow` output field
  - ✅ Added decomposition heuristics to instructions
  - ✅ Added per-workflow pattern assignment
  - ✅ Added examples for single vs multi-workflow packs

- [x] **2. Create Pack Planning agents** (or merge into existing)
  - ✅ Option B chosen: Added `PackMetadataAgent` that generates all three pack files
  - ✅ Generates manifest.json, workflow_graph.json, shared_context.json in one pass
  - ✅ Added to handoff chain (OrchestratorAgent → PackMetadataAgent → DownloadAgent)

- [x] **3. Update structured_outputs.json** with pack schemas
  - ✅ `PackManifest` model
  - ✅ `WorkflowGraph` v2 model (workflows + journeys + gates)
  - ✅ `SharedContext` model
  - ✅ `PackMetadataOutput` combined model
  - ✅ `WorkflowInPack` model for PatternSelection.workflows[]
  - ✅ Registry updated with PackMetadataAgent

- [x] **4. Update existing agents for pack awareness**
  - ✅ WorkflowStrategyAgent: Reads from `workflows[current_workflow_index]`, role-based startup_mode
  - ✅ StateArchitectAgent: Added `workflow_dependencies` derivation
  - ✅ UXArchitectAgent: Role-based constraint (supporting workflows = empty UI)

- [ ] **5. Update Build phase** to loop over workflows
  - ⚠️ Per-workflow file generation needs runtime loop logic
  - ⚠️ Currently agents process one workflow; runtime needs to iterate for multi-workflow packs
  - Pack-level file generation is handled by PackMetadataAgent

### Short Term (Next 2 Weeks)

- [ ] **6. Runtime support for packs**
  - [ ] Load `_pack/manifest.json` on startup
  - [ ] Register all workflows from pack
  - [ ] Implement workflow handoff via `workflow_graph.json`
  - [ ] Add `current_workflow_index` context variable management

- [ ] **7. Shared context watcher**
  - [ ] MongoDB change stream for context variables
  - [ ] Notify consuming workflows on change

- [ ] **8. UI workflow transitions**
  - [ ] Implement dropdown/inline/hidden styles
  - [ ] Show current workflow in chat header

### Medium Term (Next Month)

- [ ] **9. Testing infrastructure**
  - [ ] Pack-level integration tests
  - [ ] Cross-workflow handoff tests
  - [ ] Shared state consistency tests

- [ ] **10. Documentation**
  - [ ] Update user docs for pack output
  - [ ] Runtime API docs for multi-workflow
  - [ ] Example packs (IT Support, Content Pipeline, etc.)

---

## 8.1 Refined Next Steps (Prioritized)

### Priority 1: Complete AgentGenerator Changes

| Task | Status | Notes |
|------|--------|-------|
| PatternAgent decomposition | ✅ Done | Full rewrite with heuristics |
| WorkflowStrategyAgent pack-aware | ✅ Done | Reads workflows[index] |
| StateArchitectAgent dependencies | ✅ Done | workflow_dependencies field |
| UXArchitectAgent role constraint | ✅ Done | Supporting = no UI |
| PackMetadataAgent | ✅ Done | Generates _pack/ files |
| Handoffs updated | ✅ Done | Added PackMetadataAgent to chain |
| Schema updates | ✅ Done | All pack models in structured_outputs |
| Signal system cleanup | ✅ Done | Deleted signals.json, signal_watcher.py, cleaned tools.json |
| Decomposition CVARs cleanup | ✅ Done | Removed 12 obsolete variables, added 5 pack-aware variables |
| Runtime loop implementation | ✅ Done | pack_loop_controller.py + conditional handoffs |

### Priority 2: Runtime Multi-Workflow Loop (COMPLETE)

The AgentGenerator agents are now pack-aware, and the runtime loop has been implemented using:

**Implementation approach: Lifecycle tool + conditional handoffs**

1. **`pack_loop_controller.py`**: Lifecycle tool that runs `after_agent` for `OrchestratorAgent`
   - Stores completed workflow in `generated_workflows[]`
   - Increments `current_workflow_index`
   - Sets `pack_generation_complete` flag
   - Emits `pack_progress` UI events for user feedback

2. **Conditional handoffs** from `OrchestratorAgent`:
   - If `${pack_generation_complete} == False` → loop back to `WorkflowStrategyAgent`
   - If `${pack_generation_complete} == True` → proceed to `PackMetadataAgent`

3. **Context variables** for loop state:
   - `current_workflow_index`: 0-based index updated each iteration
   - `generated_workflows`: array accumulating completed workflows
   - `pack_generation_complete`: flag for handoff decision

4. **UX progress events**:
   - "Building workflow 1 of 3: ITSupportBot..."
   - "Completed workflow 1 of 3: ITSupportBot"
   - Sent via `pack_progress` event kind to ChatUI

### Priority 3: Test with Real Request

Before runtime changes, manually test the AgentGenerator with:
- Simple request: "IT support bot" → Should produce single-workflow pack
- Complex request: "IT support with knowledge base and reporting" → Should produce 3-workflow pack

### Priority 4: Runtime Pack Loading

Once AgentGenerator outputs correct packs, update runtime to:
- Read `_pack/manifest.json` for workflow list
- Load each workflow folder
- Handle cross-workflow state via shared_context.json

---

## 9. Design Decisions Log

| Decision | Choice | Rationale |
|----------|--------|-----------|
| How do workflows call each other? | Via runtime decomposition logic (`workflow_graph.json`) | Keeps workflows decoupled; runtime handles orchestration |
| Shared state mechanism? | MongoDB context variables with change watching | Leverages existing CV infrastructure; async-friendly |
| User sees workflow transitions? | Declarative (show/hide via `ui_config`) | Flexibility per use case; some want transparency, some want seamless |
| Simple requests get a pack? | Yes, always | Consistent output format; future extensibility |
| Pack metadata location? | `_pack/` folder | Clear separation from workflow folders; easy to find |

---

## 10. Open Questions

1. **Circular dependencies** - What if Workflow A depends on B and B depends on A?
   - Current thinking: Disallow at generation time (PackStrategyAgent validates DAG)

2. **Error handling** - What if a supporting workflow fails?
   - Options: Retry, fallback to primary, surface error to user

3. **Pack versioning** - How do we handle updates to one workflow in a pack?
   - Need migration strategy for shared_context changes

4. **Async workflows** - Some workflows might be fire-and-forget (e.g., ReportGenerator)
   - If auto-advance is enabled, journeys may need an `async: true|false` step flag to support fire-and-forget flows.
