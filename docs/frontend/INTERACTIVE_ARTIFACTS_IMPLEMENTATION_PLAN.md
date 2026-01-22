# Interactive Artifacts Implementation Plan

## Executive Summary

This plan outlines the implementation of **interactive artifacts** in MozaiksAI, enabling artifacts to send user actions back to the runtime while maintaining full modularity and reusability. We leverage existing event infrastructure (WebSocket, UnifiedEventDispatcher, UI_Tool patterns) with minimal extensions.

**Immediate Use Case:** Database configuration button in ActionPlan artifact  
**Future Vision:** Fully interactive app surfaces (fantasy football example) navigable via chat

---

## Architecture Overview

### Current State (Artifacts = Read-Only)
```
Agent → emit tool → Artifact renders → User views
                                     (no interaction)
```

### Target State (Artifacts = Interactive)
```
Agent → emit tool → Artifact renders → User clicks button
                                            ↓
                                    sendMessage() or sendArtifactAction()
                                            ↓
                                    WebSocket → Backend
                                            ↓
                                    Handoff to Agent or Context Update
                                            ↓
                                    Agent processes → emit response
                                            ↓
                                    Artifact updates or new UI_Tool renders
```

---

## Implementation Strategy

### Phase 1: Simple Pattern (Chat Message Trigger)
**Timeline:** Immediate  
**Complexity:** Low  
**Use Case:** DatabaseConfig button

Artifacts can trigger agent interactions by sending chat messages through existing `sendMessage()` function. This requires **zero new infrastructure** and works immediately.

**Flow:**
1. Artifact renders with interactive button
2. User clicks button → `sendMessage("Configure my database")`
3. Existing handoff system routes to appropriate agent
4. Agent emits standard UI_Tool (wizard, form, etc.)
5. User completes interaction via UI_Tool
6. Result persists to context variables
7. Next artifact render shows updated state

**Advantages:**
- ✅ Works with existing infrastructure
- ✅ No new event types needed
- ✅ Familiar pattern for agents
- ✅ Simple to document

**Limitations:**
- Natural language trigger (less precise than structured action)
- User sees message in chat history
- Less suitable for high-frequency interactions (drag/drop, live editing)

### Phase 2: Advanced Pattern (Artifact Action Events)
**Timeline:** Future (when needed for fantasy football use case)  
**Complexity:** Medium  
**Use Case:** In-artifact navigation, drag/drop, live updates

Artifacts can emit structured action events that bypass chat history and enable programmatic routing.

**Flow:**
1. Artifact renders with interactive UI (buttons, forms, navigation)
2. User interacts → `sendArtifactAction({ action: 'swap_player', payload: {...} })`
3. New backend handler routes to agent or updates context directly
4. Agent processes action → emits state update event
5. Artifact receives state update → re-renders with new data
6. Chat remains open, no message clutter

**Advantages:**
- ✅ Structured actions (type-safe, deterministic routing)
- ✅ No chat message clutter
- ✅ Supports high-frequency interactions
- ✅ Enables true "app in artifact" pattern

**Requirements:**
- New event type: `chat.artifact_action`
- New event type: `chat.artifact_state_update`
- Backend artifact action handler
- Frontend `sendArtifactAction()` helper
- Artifact state persistence mechanism

---

## Files to Create/Modify

### Phase 1: Simple Pattern (DatabaseConfig)

#### **NEW FILES**

1. **`workflows/Generator/tools/configure_database.py`**
   - Python async function for database configuration
   - Multi-step wizard: test connection → input URI → specify DB → confirm
   - Uses existing `use_ui_tool()` pattern
   - Saves config to `.env.local` or updates existing `.env`

2. **`ChatUI/src/workflows/Generator/components/ConfigureDatabaseWizard.js`**
   - React component for multi-step database configuration
   - Steps: Connection Test, URI Input, Database Name, Confirmation
   - Uses shared design system tokens
   - Exports `componentMetadata` with ui_pattern: "multi_step"

3. **`docs/runtime/INTERACTIVE_ARTIFACTS.md`** (NEW)
   - Comprehensive guide for interactive artifacts
   - Documents both Simple and Advanced patterns
   - Shows how to add buttons/interactions to artifacts
   - Provides code examples for common patterns
   - Explains when to use each approach

#### **MODIFIED FILES**

4. **`workflows/Generator/agents.json`**
   - Add `DatabaseConfigAgent` definition
   - 6-section prompt structure (universal sections hook-injected)
   - Role: Database configuration specialist
   - Tool: configure_database
   - auto_tool_mode: true
   - structured_outputs_required: false (conversational agent)

5. **`workflows/Generator/tools.json`**
   - Add configure_database UI_Tool entry
   - tool_type: "UI_Tool"
   - ui.component: "ConfigureDatabaseWizard"
   - ui.ui_pattern: "multi_step"
   - ui.display: "inline"
   - ui.label: "Configure MongoDB Connection"

6. **`workflows/Generator/handoffs.json`**
   - Add handoff rule: user → DatabaseConfigAgent
   - Condition: mentions "configure" + "database"/"mongodb"/"connection"
   - Add handoff rule: DatabaseConfigAgent → user (after_work)

7. **`ChatUI/src/workflows/Generator/components/ActionPlan.js`**
   - Add "Configure Database" button in "Not Connected" state (line ~997)
   - Button calls `sendMessage("I want to configure my database connection")`
   - Uses existing sendMessage from ChatPage context
   - Styled with design system button tokens

8. **`ChatUI/src/workflows/Generator/components/index.js`**
   - Add ConfigureDatabaseWizard to exports
   - Ensure proper component registration

#### **DOCUMENTATION UPDATES**

9. **`docs/workflows/ui_tool_pipeline.md`**
   - Add section: "Triggering UI Tools from Artifacts"
   - Document artifact button → sendMessage() pattern
   - Show handoff routing example
   - Explain when artifacts should trigger tools vs. direct actions

10. **`docs/workflows/UI_ui_patternS.md`**
    - Add Pattern 4: "Artifact-Triggered Interaction"
    - Document flow: Artifact button → Chat message → Handoff → UI_Tool
    - Show code examples for both artifact side and agent side
    - Explain coordination between artifact state and tool results

11. **`docs/frontend/ui_components.md`**
    - Add section: "Interactive Artifacts"
    - Document how artifacts can access sendMessage()
    - Show button implementation patterns
    - Explain state synchronization after tool completion

12. **`docs/frontend/unified_ui_tools_and_design.md`**
    - Add Rule 11: "Artifacts can trigger UI tools via sendMessage()"
    - Document button/link patterns for artifact interactions
    - Show design system usage for interactive elements in artifacts
    - Add examples of artifact action triggers

13. **`docs/frontend/chatui_architecture.md`**
    - Update "Artifact Components" section
    - Document that artifacts receive sendMessage via props/context
    - Show how artifacts coordinate with chat state
    - Explain message-triggered agent interactions

---

### Phase 2: Advanced Pattern (Future)

#### **NEW FILES**

14. **`core/events/artifact_actions.py`** (FUTURE)
    - Event handler for chat.artifact_action events
    - Routes actions to agents or context updates
    - Uses existing correlation ID pattern
    - Integrates with UnifiedEventDispatcher

15. **`core/events/artifact_state.py`** (FUTURE)
    - Manages artifact state persistence
    - Stores state in context variables or dedicated collection
    - Provides state update diffing/merging
    - Emits chat.artifact_state_update events

#### **MODIFIED FILES (FUTURE)**

16. **`core/events/unified_event_dispatcher.py`**
    - Add handler registration for chat.artifact_action
    - Add handler registration for chat.artifact_state_update
    - Ensure proper event routing and correlation

17. **`core/transport/websocket.py`**
    - Handle incoming chat.artifact_action from frontend
    - Route to artifact action handler
    - Maintain existing session management

18. **`ChatUI/src/contexts/ChatUIContext.js`**
    - Add `sendArtifactAction()` helper function
    - Provide artifact action sending to all components
    - Handle correlation and response tracking

19. **`ChatUI/src/pages/ChatPage.js`**
    - Add handler for chat.artifact_state_update events
    - Update artifact state in component tree
    - Ensure proper re-rendering on state changes

#### **DOCUMENTATION (FUTURE)**

20. **`docs/runtime/INTERACTIVE_ARTIFACTS.md`** (UPDATE)
    - Add Phase 2 Advanced Pattern documentation
    - Document artifact action event contracts
    - Show state management patterns
    - Provide complex interaction examples

21. **`docs/reference/event_reference.md`**
    - Add chat.artifact_action event schema
    - Add chat.artifact_state_update event schema
    - Document payload contracts and examples
    - Show event sequence diagrams

---

## Agent Context Updates

### Files That Inform Generator Agents

These files must be updated to ensure agents (UIFileGenerator, AgentToolsGenerator, etc.) understand interactive artifact patterns:

1. **`docs/frontend/unified_ui_tools_and_design.md`** ⭐
   - **Primary prompt source for UIFileGenerator**
   - Must document artifact UI patterns
   - Must show sendMessage() usage in artifacts
   - Must provide code templates

2. **`docs/workflows/ui_tool_pipeline.md`**
   - Explains full lifecycle of UI tool emission
   - Must show artifact-triggered flows
   - UIFileGenerator reads this for context

3. **`docs/workflows/UI_ui_patternS.md`**
   - Pattern library for tool/UI coordination
   - Must include Artifact-Triggered pattern
   - Agents reference this for interaction design

4. **`workflows/Generator/structured_outputs.json`**
   - Schema definitions for agent outputs
   - No changes needed (ui_pattern already supports multi_step)
   - Agents use this to validate structured outputs

### Agent Prompt Updates Needed

**UIFileGenerator:**
- Must understand artifacts can trigger tools via sendMessage()
- Must know when to use inline vs artifact display modes
- Must generate proper button/link elements in artifact components

**AgentToolsGenerator:**
- Must understand tools can be triggered by artifact interactions
- Must generate appropriate response handling for artifact-triggered flows
- Must document tool usage in artifact context

**AgentsAgent:**
- Must know when to create agents that respond to artifact triggers
- Must understand handoff patterns for artifact-initiated interactions
- Must configure agents for artifact-driven workflows

---

## Implementation Checklist

### Phase 1: Simple Pattern (DatabaseConfig) ✅

**Backend:**
- [ ] Create DatabaseConfigAgent in agents.json
- [ ] Add configure_database tool to tools.json
- [ ] Add handoff rules in handoffs.json
- [ ] Implement configure_database.py (Python async function)
- [ ] Test MongoDB connection validation
- [ ] Test .env file writing
- [ ] Test multi-step wizard flow

**Frontend:**
- [ ] Create ConfigureDatabaseWizard.js component
- [ ] Add component to Generator/components/index.js
- [ ] Update ActionPlan.js with Configure Database button
- [ ] Test button triggers sendMessage()
- [ ] Test handoff to DatabaseConfigAgent
- [ ] Test wizard renders and completes
- [ ] Test schema appears in ActionPlan after config

**Documentation:**
- [ ] Create docs/runtime/INTERACTIVE_ARTIFACTS.md
- [ ] Update docs/workflows/ui_tool_pipeline.md
- [ ] Update docs/workflows/UI_ui_patternS.md
- [ ] Update docs/frontend/ui_components.md
- [ ] Update docs/frontend/unified_ui_tools_and_design.md
- [ ] Update docs/frontend/chatui_architecture.md

**Testing:**
- [ ] User clicks Configure Database button
- [ ] Chat message sent and visible
- [ ] DatabaseConfigAgent receives handoff
- [ ] Wizard UI renders inline
- [ ] User completes steps successfully
- [ ] Config saved to .env
- [ ] Server restart prompt shown
- [ ] Next workflow generation shows schema

### Phase 2: Advanced Pattern (Future) 🔮

**Backend:**
- [ ] Create core/events/artifact_actions.py
- [ ] Create core/events/artifact_state.py
- [ ] Update unified_event_dispatcher.py
- [ ] Add artifact action routing logic
- [ ] Implement state persistence mechanism
- [ ] Add chat.artifact_action handler
- [ ] Add chat.artifact_state_update emitter

**Frontend:**
- [ ] Add sendArtifactAction() to ChatUIContext
- [ ] Add artifact state tracking to ChatPage
- [ ] Update artifact components to receive state
- [ ] Implement state update handling
- [ ] Test artifact action emission
- [ ] Test state update reception
- [ ] Test re-rendering on state changes

**Documentation:**
- [ ] Update INTERACTIVE_ARTIFACTS.md with Phase 2
- [ ] Update event_reference.md with new events
- [ ] Add advanced examples to ui_components.md
- [ ] Document state management patterns
- [ ] Provide complex interaction examples

---

## Modularity & Extensibility

### Design Principles

1. **Transport Agnostic**
   - Artifact actions work via WebSocket (primary) or REST (fallback)
   - No hardcoded transport assumptions in artifacts
   - sendMessage() and sendArtifactAction() abstract transport

2. **Agent Agnostic**
   - Any agent can respond to artifact actions
   - Handoff rules define routing (declarative)
   - No artifact-specific agent coupling

3. **Workflow Agnostic**
   - Pattern works for Generator and any future workflow
   - Components access sendMessage() via props/context
   - No workflow-specific hardcoding in runtime

4. **Display Mode Agnostic**
   - Works for artifact and inline components
   - Both can trigger interactions
   - UI_Tool responses work regardless of trigger source

5. **State Agnostic**
   - Artifacts don't manage their own persistence
   - State lives in context variables or dedicated store
   - Runtime provides state, artifacts consume/render

### Extension Points

**For New Interactive Artifacts:**
```javascript
// Any artifact component can trigger interactions
const MyArtifact = ({ data, sendMessage }) => {
  const handleAction = () => {
    // Phase 1: Simple pattern
    sendMessage("Trigger my action");
    
    // Phase 2: Advanced pattern (future)
    sendArtifactAction({
      artifactId: 'my_artifact',
      action: 'do_something',
      payload: { key: 'value' }
    });
  };
  
  return (
    <div>
      <button onClick={handleAction}>Do Something</button>
    </div>
  );
};
```

**For New Agents Responding to Artifacts:**
```json
// handoffs.json
{
  "source_agent": "user",
  "target_agent": "MyCustomAgent",
  "handoff_type": "condition",
  "condition": "user mentions 'my trigger phrase'",
  "transition_target": "AgentTarget"
}
```

**For New UI Tools Triggered by Artifacts:**
```json
// tools.json
{
  "agent": "MyCustomAgent",
  "file": "my_custom_tool.py",
  "function": "my_custom_tool",
  "tool_type": "UI_Tool",
  "ui": {
    "component": "MyCustomComponent",
    "ui_pattern": "single_step",
    "display": "inline"
  }
}
```

---

## Risk Mitigation

### Potential Issues & Solutions

1. **Issue:** Artifact button sends message → creates chat clutter
   - **Mitigation:** Phase 2 uses structured actions (no chat history)
   - **Workaround:** Use system messages or suppress trigger message in UI

2. **Issue:** State updates between artifact renders
   - **Mitigation:** Context variables persist state across turns
   - **Pattern:** Tool completion updates context → next artifact render sees new state

3. **Issue:** Agents don't understand artifact triggers
   - **Mitigation:** Comprehensive documentation updates
   - **Pattern:** Agent prompts include artifact interaction context

4. **Issue:** Handoff rules too broad (false positives)
   - **Mitigation:** Specific condition matching with keywords
   - **Pattern:** Use multiple keywords (AND logic) for precise matching

5. **Issue:** User confusion about where to interact (chat vs artifact)
   - **Mitigation:** Clear UI affordances (buttons, forms, CTAs)
   - **Pattern:** Artifacts guide user toward intended interaction points

---

## Success Criteria

### Phase 1 (DatabaseConfig)

✅ **Functional:**
- [ ] User can click "Configure Database" button in ActionPlan
- [ ] Button triggers DatabaseConfigAgent handoff
- [ ] Multi-step wizard renders and completes successfully
- [ ] MongoDB connection validated before saving
- [ ] Configuration saved to .env file
- [ ] User prompted to restart server
- [ ] Subsequent workflow generation shows database schema
- [ ] No errors in browser console or backend logs

✅ **Quality:**
- [ ] Code follows existing patterns and conventions
- [ ] Design system tokens used consistently
- [ ] Component metadata properly defined
- [ ] Tool properly registered in tools.json
- [ ] Agent properly defined in agents.json
- [ ] Handoff rules properly configured
- [ ] All documentation updated and accurate

✅ **User Experience:**
- [ ] Flow feels natural and intuitive
- [ ] Error messages are clear and actionable
- [ ] Loading states shown during async operations
- [ ] Success confirmation provided
- [ ] No unnecessary steps or friction

### Phase 2 (Advanced Pattern - Future)

✅ **Functional:**
- [ ] Artifacts can emit structured action events
- [ ] Backend routes actions to appropriate handlers
- [ ] Agents receive and process artifact actions
- [ ] State updates propagate back to artifacts
- [ ] Artifacts re-render with updated state
- [ ] Chat remains usable during artifact interactions
- [ ] No race conditions or state corruption

✅ **Quality:**
- [ ] Event contracts well-defined and versioned
- [ ] State persistence mechanism robust
- [ ] Correlation IDs tracked properly
- [ ] Error handling comprehensive
- [ ] Performance acceptable (< 100ms round-trip)
- [ ] Documentation complete and accurate

---

## Timeline Estimate

### Phase 1: Simple Pattern (DatabaseConfig)
- **Planning & Architecture Review:** 1 hour (DONE)
- **Backend Implementation:** 2 hours
  - DatabaseConfigAgent definition (30 min)
  - configure_database.py tool (60 min)
  - Handoff rules (15 min)
  - Testing (15 min)
- **Frontend Implementation:** 1.5 hours
  - ConfigureDatabaseWizard.js (60 min)
  - ActionPlan.js button (15 min)
  - Component registration (15 min)
- **Documentation Updates:** 1.5 hours
  - New INTERACTIVE_ARTIFACTS.md (45 min)
  - Update 5 existing docs (45 min)
- **End-to-End Testing:** 1 hour
- **Total: ~7 hours**

### Phase 2: Advanced Pattern (Future)
- **Backend Event Infrastructure:** 3 hours
- **Frontend Integration:** 2 hours
- **State Management:** 2 hours
- **Documentation:** 2 hours
- **Testing & Refinement:** 2 hours
- **Total: ~11 hours**

---

## Multi-Workflow Navigation (ARCHITECTURE REVISION)

### Problem Statement (Corrected Analysis)

**Initial Oversimplification:**
Original plan suggested hardcoding dependencies in `orchestrator.json`. User correctly identified this won't scale:
- orchestrator.json is static per-workflow config (doesn't know about application's other workflows)
- Dependencies are **application-level relationships** between workflows
- WorkflowArchitectAgent must **reason** about dependencies during generation, not copy from template
- Requires persistent storage of workflow dependency graph
- Database context becomes mandatory (can't orchestrate without data layer)

**Reality:**
Multi-workflow navigation requires:
1. **New Collection**: `WorkflowDependencies` (stores application-level workflow graph)
2. **Agent Intelligence**: WorkflowArchitectAgent determines dependencies based on workflow purpose
3. **Runtime Validation**: Check dependencies before allowing workflow start/resume
4. **Hidden Orchestration**: User never sees dependency logic (internal concern, not ActionPlan feature)

### Current State Analysis ✅

**Good News: Resume Logic Already Works**

```python
# persistence_manager.py
async def resume_chat(self, chat_id: str, app_id: str):
    """Resume IN_PROGRESS chat for specific workflow."""
    # Each workflow has its own chat_id
    # Status check ensures only IN_PROGRESS chats resume
    # Messages replay from last position
```

**WebSocket endpoint is workflow-scoped:**
```python
@app.websocket("/ws/{workflow_name}/{app_id}/{chat_id}/{user_id}")
```

**What This Means:**
- ✅ Generator chat: `gen_abc123` (IN_PROGRESS)
- ✅ Build chat: `build_xyz789` (IN_PROGRESS)
- ✅ Both can be IN_PROGRESS simultaneously
- ✅ Auto-resume works per-workflow when user returns
- ✅ No data corruption or cross-contamination

### What's Missing ⚠️

#### 1. WorkflowDependencies Collection (NEW DATABASE LAYER)

**Problem**: Workflows don't know about each other at design time. Dependencies are **runtime application structure**, not static config.

**Solution**: Create `WorkflowDependencies` collection to store application-level workflow graph:

**Collection Schema:**
```json
{
  "_id": "ObjectId(...)",
  "app_id": "ent_abc123",
  "application_name": "Content Marketing Platform",
  "workflows": {
    "Generator": {
      "workflow_name": "Generator",
      "created_at": "2025-11-08T10:00:00Z",
      "status": "active",
      "dependencies": {
        "required_workflows": [],
        "required_context_vars": [],
        "required_artifacts": []
      },
      "provides": {
        "context_vars": ["concept_overview", "database_schema", "action_plan_approval"],
        "artifacts": ["ActionPlan"]
      }
    },
    "Build": {
      "workflow_name": "Build",
      "created_at": "2025-11-08T12:30:00Z",
      "status": "active",
      "dependencies": {
        "required_workflows": [
          {
            "workflow": "Generator",
            "status": "completed",
            "reason": "Build workflow requires a completed Generator workflow to have files to build"
          }
        ],
        "required_context_vars": [
          {
            "variable": "action_plan_approval",
            "source_workflow": "Generator",
            "reason": "Build needs approved action plan from Generator output"
          }
        ],
        "required_artifacts": [
          {
            "artifact_type": "ActionPlan",
            "workflow": "Generator",
            "reason": "User must have approved an action plan before building"
          }
        ]
      },
      "provides": {
        "context_vars": ["built_workflow_name", "build_status"],
        "artifacts": []
      }
    }
  }
}
```

**Key Insights:**
- **One document per app** (application-level graph)
- **workflows object** keyed by workflow_name
- **dependencies** declared per workflow (what it needs)
- **provides** declared per workflow (what it outputs)
- **WorkflowArchitectAgent determines dependencies** based on interview context and workflow purpose
- **Updated when new workflows generated** (Generator adds new workflow to graph)

#### 2. WorkflowArchitectAgent Dependency Intelligence (AGENT PROMPT UPDATE)

**Problem**: Agent can't hardcode dependencies—must **reason** about them during workflow generation.

**Solution**: Update WorkflowArchitectAgent prompt with dependency reasoning logic:

**Add to `workflows/Generator/agents.json` → WorkflowArchitectAgent → instructions:**

```text
**Step 2.5 - Analyze Workflow Dependencies (NEW - BEFORE Step 3)**:

Review the interview context and workflow purpose to determine if this workflow depends on other workflows:

**Dependency Analysis Questions**:
1. Does this workflow need outputs from another workflow to function?
   - Example: "Build" workflow needs approved action plan from "Generator"
   - Example: "Deploy" workflow needs built files from "Build"

2. Does this workflow reference data that only exists after another workflow completes?
   - Example: Workflow mentions "use the generated schema" → Depends on Generator
   - Example: Workflow mentions "deploy the built workflow" → Depends on Build

3. What context variables or artifacts does this workflow consume?
   - If consuming variables from another workflow → Add to required_context_vars
   - If consuming artifacts (ActionPlan, BuildManifest) → Add to required_artifacts

**Natural Dependency Patterns**:
- Workflow involves "building" or "implementing" → Likely needs Generator outputs
- Workflow involves "deploying" or "launching" → Likely needs Build outputs  
- Workflow processes domain data → Likely needs database schema awareness
- Workflow is first/foundational → Usually has no dependencies

**Output Format**: Populate these new TechnicalBlueprint fields:
- workflow_dependencies: {required_workflows[], required_context_vars[], required_artifacts[]}

Note: If this is the first workflow being generated (no existing workflows in this app), dependencies will typically be empty.
```

**Why This Works**:
- ✅ Agent reasons about dependencies based on workflow semantics
- ✅ No hardcoded dependency maps
- ✅ Works for ANY workflow sequence user creates
- ✅ Learns from interview context + existing workflow graph

#### 3. Dependency Validation on WebSocket Connect

**Extend shared_app.py handle_websocket():**

```python
@app.websocket("/ws/{workflow_name}/{app_id}/{chat_id}/{user_id}")
async def handle_websocket(...):
    await websocket.accept()
    
  # NEW: Validate workflow prerequisites BEFORE starting/resuming
  prereqs_ok, error_details = await validate_pack_prereqs(
    workflow_name=workflow_name,
    app_id=app_id,
    user_id=user_id,
  )
    
  if not prereqs_ok:
        await simple_transport.send_error(
            error_message=f"Cannot start {workflow_name}: {error_details}",
      error_code="WORKFLOW_PREREQS_NOT_MET",
            chat_id=chat_id
        )
    await websocket.close(code=1008, reason="Prerequisites not met")
        return
    
    # Continue with normal workflow startup/resume
    ...
```

#### 4. Workflow Dependency Checker (Database-Backed)

**Create core/workflow/dependencies.py:**

```python
from typing import Optional, Tuple, List, Dict, Any
from core.data.models import WorkflowStatus
from core.data.persistence.persistence_manager import AG2PersistenceManager, PersistenceManager
from motor.motor_asyncio import AsyncIOMotorClient

class WorkflowDependencyManager:
    """Manages application-level workflow dependency graph."""
    
    def __init__(self):
        self.pm = PersistenceManager()
        self.ag2_pm = AG2PersistenceManager()
    
    async def _get_dependencies_collection(self):
        """Get WorkflowDependencies collection."""
        client = AsyncIOMotorClient(self.pm.mongodb_uri)
        db = client[self.pm.database_name]
        return db["WorkflowDependencies"]
    
    async def get_workflow_graph(self, app_id: str) -> Optional[Dict[str, Any]]:
        """Get complete workflow dependency graph for app."""
        coll = await self._get_dependencies_collection()
        doc = await coll.find_one({"app_id": app_id})
        return doc
    
    async def update_workflow_graph(
        self, 
        app_id: str, 
        workflow_name: str,
        dependencies: Dict[str, Any]
    ):
        """Add or update workflow in dependency graph."""
        coll = await self._get_dependencies_collection()
        
        # Upsert workflow entry
        await coll.update_one(
            {"app_id": app_id},
            {
                "$set": {
                    f"workflows.{workflow_name}": {
                        "workflow_name": workflow_name,
                        "created_at": datetime.utcnow().isoformat(),
                        "status": "active",
                        "dependencies": dependencies
                    }
                }
            },
            upsert=True
        )
    
    async def validate_workflow_dependencies(
        self,
        workflow_name: str,
        app_id: str,
        user_id: str
    ) -> Tuple[bool, Optional[str]]:
        """Check if workflow dependencies are satisfied.
        
        Returns:
            (True, None) if all dependencies met
            (False, "error message") if dependencies not met
        """
        # Get workflow graph
        graph = await self.get_workflow_graph(app_id)
        if not graph or "workflows" not in graph:
            return True, None  # No graph = first workflow, allow
        
        # Get workflow entry
        workflow_entry = graph["workflows"].get(workflow_name)
        if not workflow_entry:
            return True, None  # Workflow not in graph = allow (will be added)
        
        dependencies = workflow_entry.get("dependencies", {})
        if not dependencies:
            return True, None  # No dependencies = allow
        
        # Get ChatSessions collection for validation
        coll = await self.ag2_pm._coll()
        
        # Check required_workflows
        for req_wf in dependencies.get("required_workflows", []):
            wf_name = req_wf["workflow"]
            required_status = req_wf.get("status", "completed")
            reason = req_wf.get("reason", "")
            
            # Find most recent chat for that workflow
            doc = await coll.find_one(
                {
                    "app_id": app_id,
                    "user_id": user_id,
                    "workflow_name": wf_name
                },
                sort=[("created_at", -1)]
            )
            
            if not doc:
                return False, f"Please complete the {wf_name} workflow first. {reason}"
            
            if required_status == "completed":
                if doc.get("status") != WorkflowStatus.COMPLETED:
                    return False, f"The {wf_name} workflow must be completed before starting {workflow_name}. {reason}"
        
        # Check required_context_vars
        for req_var in dependencies.get("required_context_vars", []):
            var_name = req_var["variable"]
            source_wf = req_var.get("source_workflow")
            reason = req_var.get("reason", "")
            
            if source_wf:
                doc = await coll.find_one(
                    {
                        "app_id": app_id,
                        "user_id": user_id,
                        "workflow_name": source_wf
                    },
                    sort=[("created_at", -1)]
                )
                
                if not doc or var_name not in doc.get("context", {}):
                    return False, f"Missing required context from {source_wf}: {var_name}. {reason}"
        
        # Check required_artifacts
        for req_artifact in dependencies.get("required_artifacts", []):
            artifact_type = req_artifact["artifact_type"]
            source_wf = req_artifact.get("workflow")
            reason = req_artifact.get("reason", "")
            
            doc = await coll.find_one(
                {
                    "app_id": app_id,
                    "user_id": user_id,
                    "workflow_name": source_wf
                },
                sort=[("created_at", -1)]
            )
            
            if not doc or "last_artifact" not in doc:
                return False, f"No {artifact_type} found from {source_wf}. {reason}"
            
            artifact = doc.get("last_artifact", {})
            if artifact.get("type") != artifact_type:
                return False, f"Expected {artifact_type} artifact from {source_wf}. {reason}"
        
        return True, None
    
    async def list_available_workflows(
        self, 
        app_id: str, 
        user_id: str
    ) -> List[Dict[str, Any]]:
        """List all workflows and their availability status for a user."""
        graph = await self.get_workflow_graph(app_id)
        if not graph or "workflows" not in graph:
            return []
        
        workflows = []
        for wf_name, wf_entry in graph["workflows"].items():
            is_available, reason = await self.validate_workflow_dependencies(
                workflow_name=wf_name,
                app_id=app_id,
                user_id=user_id
            )
            
            workflows.append({
                "workflow_name": wf_name,
                "available": is_available,
                "reason": reason or "All dependencies met",
                "dependencies": wf_entry.get("dependencies", {})
            })
        
        return workflows


# Global instance
dependency_manager = WorkflowDependencyManager()
```

**Why This Works**:
- ✅ Persistent dependency graph per app
- ✅ Validation queries both WorkflowDependencies (graph) and ChatSessions (runtime state)
- ✅ Supports adding new workflows dynamically
- ✅ Provides "what this workflow outputs" for downstream discovery

#### 4. Artifact Navigation Contract

**Extend ChatUIContext to provide navigation:**

```javascript
// ChatUI/src/contexts/ChatUIContext.js
export const ChatUIProvider = ({ children }) => {
  const [currentWorkflow, setCurrentWorkflow] = useState('Generator');
  const [workflowHistory, setWorkflowHistory] = useState([]);
  
  const navigateToWorkflow = async (targetWorkflow, context = {}) => {
    // 1. Check if target workflow dependencies are met
    const available = await checkWorkflowAvailable(targetWorkflow);
    if (!available.canStart) {
      toast.error(`Cannot start ${targetWorkflow}: ${available.reason}`);
      return;
    }
    
    // 2. Store navigation context (for passing data between workflows)
    sessionStorage.setItem(`workflow_context_${targetWorkflow}`, JSON.stringify(context));
    
    // 3. Update history
    setWorkflowHistory(prev => [...prev, { 
      workflow: currentWorkflow, 
      timestamp: Date.now() 
    }]);
    
    // 4. Navigate (router will handle WebSocket reconnection)
    navigate(`/app/${appId}/${targetWorkflow}`);
  };
  
  const value = {
    currentWorkflow,
    navigateToWorkflow,
    workflowHistory,
    // ... existing context values
  };
  
  return <ChatUIContext.Provider value={value}>{children}</ChatUIContext.Provider>;
};
```

**Artifacts receive navigation capability:**

```javascript
// ActionPlan.js
const ActionPlan = ({ data, sendMessage }) => {
  const { navigateToWorkflow } = useChatUI();
  
  const handleBuildWorkflow = () => {
    navigateToWorkflow('Build', {
      source: 'Generator',
      generatedFiles: data.workflow.files,
      actionPlanId: data.workflow.id
    });
  };
  
  return (
    <div>
      {/* ... existing UI ... */}
      
      {data.workflow?.status === 'approved' && (
        <button onClick={handleBuildWorkflow} className="...">
          <Hammer className="h-4 w-4" />
          Build This Workflow
        </button>
      )}
    </div>
  );
};
```

### Implementation Checklist (Multi-Workflow)

**Database Layer:**
- [ ] Create `WorkflowDependencies` collection schema in MongoDB
- [ ] Add index on `app_id` for fast graph lookups
- [ ] Test collection creation and document structure

**Agent Intelligence (WorkflowArchitectAgent):**
- [ ] Update `workflows/Generator/agents.json` → WorkflowArchitectAgent prompt
- [ ] Add Step 2.5: "Analyze Workflow Dependencies" (natural reasoning, not rules)
- [ ] Add dependency analysis questions (consumption patterns, not hardcoded tree)
- [ ] Update structured_outputs.json with `TechnicalBlueprint.workflow_dependencies` field
- [ ] **Implementation Note** (not in prompt): Database context becomes implicit requirement
  * When workflow has dependencies → Agent will naturally need database context to query validation state
  * No explicit "database is mandatory" rule—emerges from dependency reasoning
  * Runtime validates database availability when dependencies present (graceful error if missing)

**Backend Validation:**
- [ ] Create `core/workflow/dependencies.py` with WorkflowDependencyManager class
- [ ] Implement `get_workflow_graph()` - queries WorkflowDependencies collection
- [ ] Implement `update_workflow_graph()` - upserts workflow entry when generated
- [ ] Implement `validate_workflow_dependencies()` - checks prerequisites before start/resume
- [ ] Implement `list_available_workflows()` - returns filtered workflow list with availability
- [ ] Update `shared_app.py` WebSocket handler with dependency validation call
- [ ] Add GET `/api/workflows/{app_id}/available` endpoint
- [ ] Add POST `/api/workflows/{app_id}/dependencies` endpoint (for manual overrides)

**Generator Integration:**
- [ ] Update `generate_and_download` tool to call `dependency_manager.update_workflow_graph()`
- [ ] After generating workflow files, persist dependencies to WorkflowDependencies collection
- [ ] Use WorkflowArchitectAgent's TechnicalBlueprint.workflow_dependencies output

**Testing:**
- [ ] Generate first workflow (Generator) - should have no dependencies
- [ ] Generate second workflow (Build) - WorkflowArchitectAgent should infer Generator dependency
- [ ] Verify WorkflowDependencies collection updated with Build→Generator relationship
- [ ] Attempt to start Build before Generator completes - should block with clear error
- [ ] Complete Generator workflow, attempt Build again - should succeed
- [ ] Navigate between workflows while both IN_PROGRESS - should work independently

**Frontend:**
- [ ] Add navigateToWorkflow() to ChatUIContext
- [ ] Add workflow availability checking to navigation
- [ ] Show dependency errors clearly in UI
- [ ] Add workflow history tracking (breadcrumbs)
- [ ] Pass navigation context between workflows
- [ ] Test multi-workflow navigation end-to-end

**Documentation:**
- [ ] Add "Multi-Workflow Navigation" section to INTERACTIVE_ARTIFACTS.md
- [ ] Document dependency configuration in workflow setup docs
- [ ] Provide examples of common dependency patterns
- [ ] Explain resume behavior across workflows
- [ ] Show artifact navigation implementation patterns

### What User NEVER Sees (Hidden Orchestration)

**CRITICAL DESIGN PRINCIPLE**: Workflow dependencies are **internal orchestration concerns**, NOT user-facing features.

**User NEVER sees in ActionPlan:**
- ❌ "Dependencies" section listing prerequisite workflows
- ❌ Workflow dependency graph visualization
- ❌ "Provides" section showing outputs for downstream workflows
- ❌ Technical dependency validation logic

**User DOES see:**
- ✅ Natural error messages: "Please complete the Generator workflow before starting Build"
- ✅ Disabled/grayed workflow buttons when dependencies not met
- ✅ Seamless navigation between allowed workflows
- ✅ Clear instructions when prerequisites missing

**Why This Matters:**
- User thinks in terms of their app functionality, not dependency graphs
- ActionPlan shows "what the workflow does", not "what it depends on"
- Dependency reasoning happens in WorkflowArchitectAgent (hidden intelligence)
- Database stores dependency matrix (hidden persistence layer)
- Runtime validates and enforces (hidden orchestration)

**Example User Experience:**
1. User generates Marketing Automation workflow
2. Clicks "Build This Workflow" button
3. If Generator not approved yet → Toast: "Approve the action plan first"
4. If Generator approved → Smoothly navigates to Build workflow
5. User NEVER knows about WorkflowDependencies collection or validation logic

### Example: Generator → Build Workflow Flow

**User Journey (What They See):**
1. User starts Generator workflow
2. Generator creates action plan artifact
3. User clicks "Build This Workflow" button in artifact
4. ✅ **Silent check**: Dependencies met (Generator approved)
5. Navigate to Build workflow (Generator chat stays IN_PROGRESS)
6. Build workflow starts, receives context from Generator
7. User completes Build workflow (COMPLETED)
8. User returns to Generator (auto-resumes from where they left off)

**Behind the Scenes (Hidden):**
```python
# WorkflowArchitectAgent reasons during generation:
# "This is a Build workflow → requires Generator completion"

# Database stores dependency graph:
WorkflowDependencies {
  "app_id": "ent_123",
  "workflows": {
    "Generator": {"dependencies": {}, "provides": {...}},
    "Build": {
      "dependencies": {
        "required_workflows": [{"workflow": "Generator", "status": "completed"}]
      }
    }
  }
}

# shared_app.py WebSocket handler validates:
is_valid, error = await dependency_manager.validate_workflow_dependencies(
    workflow_name="Build",
    app_id="ent_123",
    user_id="user_456"
)
if not is_valid:
    return error_event("Please complete Generator first")
```

**Validation Flow (Internal):**
```
User clicks "Build" button
  ↓
Frontend: navigateToWorkflow('Build')
  ↓
Backend WebSocket connect: /ws/Build/...
  ↓
dependency_manager.validate_workflow_dependencies()
  ↓
Query WorkflowDependencies collection → Build requires Generator
  ↓
Query ChatSessions collection → Generator status = COMPLETED?
  ↓
YES → Allow WebSocket, start Build
NO → Send error event, close WebSocket
```

---

## Next Steps

1. **Review This Plan** - Confirm approach and scope (including multi-workflow)
2. **Begin Phase 1 Implementation** - DatabaseConfig feature
3. **Add Multi-Workflow Foundation** - Dependency system
4. **Test & Validate** - Ensure pattern works end-to-end
5. **Document Learnings** - Update docs with real examples
6. **Plan Phase 2** - Schedule advanced pattern when needed

---

## Approval

**Ready to proceed with Phase 1 implementation?**

This plan ensures:
- ✅ Minimal infrastructure changes (leverage existing systems)
- ✅ Full modularity (any artifact, any agent, any workflow)
- ✅ Comprehensive documentation (agents have proper context)
- ✅ Clear extension path (Phase 2 when needed)
- ✅ Production-ready quality (no placeholders or shortcuts)
- ✅ **Multi-workflow navigation** (with dependency validation)
- ✅ **Resume logic** (works per-workflow automatically)

**Approval Status:** [ ] APPROVED  [ ] NEEDS REVISION

**Next Action:** Begin Phase 1 Backend Implementation (DatabaseConfigAgent) + Workflow Dependency System
