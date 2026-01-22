# Backend Integration Guide

This guide shows you how to integrate the Interactive Artifacts system into your workflow tools and handlers.

---

## 🎯 Overview

The session manager provides 7 core functions for managing workflows and artifacts:

| Function | Purpose | When to Use |
|----------|---------|-------------|
| `create_workflow_session()` | Start new workflow | When workflow initializes |
| `complete_workflow_session()` | Mark workflow done | When workflow finishes successfully |
| `create_artifact_instance()` | Create persistent artifact | When generating UI component |
| `attach_artifact_to_session()` | Link artifact to session | After creating both |
| `update_artifact_state()` | Update artifact state | When state changes (progress, data, etc.) |
| `get_artifact_instance()` | Retrieve artifact | When resuming or checking state |
| `get_workflow_session()` | Retrieve session | When validating or resuming |

---

## 🚀 Quick Start Pattern

### Pattern 1: Starting a New Workflow

**Scenario**: User starts the AppBuilder workflow to create a new SaaS app.

```python
from core.workflow import session_manager

async def start_appbuilder_workflow(app_id: str, user_id: str, app_name: str):
    """
    Initialize AppBuilder workflow with artifact.
    """
    # Step 1: Create new workflow session
    session = await session_manager.create_workflow_session(
        app_id=app_id,
        user_id=user_id,
        workflow_name="AppBuilder"
    )
    chat_id = session["_id"]  # e.g., "chat_a1b2c3d4e5f6"
    
    # Step 2: Create artifact with initial state
    artifact = await session_manager.create_artifact_instance(
        app_id=app_id,
        workflow_name="AppBuilder",
        artifact_type="AppBuilderArtifact",
        initial_state={
            "app_name": app_name,
            "architecture": None,
            "features": [],
            "build_progress": 0,
            "deployment_status": "not_started",
            "revenue_to_date": 0.0,
            "buttons": [
                {
                    "label": "View Revenue",
                    "action": "launch_workflow",
                    "workflow": "RevenueDashboard"
                },
                {
                    "label": "Deploy to Production",
                    "action": "deploy_app"
                }
            ]
        }
    )
    artifact_id = artifact["_id"]
    
    # Step 3: Link artifact to session
    await session_manager.attach_artifact_to_session(
        chat_id=chat_id,
        artifact_id=artifact_id,
        app_id=app_id
    )
    
    return chat_id, artifact_id
```

---

### Pattern 2: Updating Artifact State During Workflow

**Scenario**: AI generates more code, update build progress in the artifact.

```python
async def update_build_progress(
    artifact_id: str,
    app_id: str,
    progress: int,
    new_features: list
):
    """
    Update AppBuilder artifact as code generation progresses.
    """
    await session_manager.update_artifact_state(
        artifact_id=artifact_id,
        app_id=app_id,
        state_updates={
            "build_progress": progress,
            "features": new_features,
            "last_updated": time.time()
        }
    )
    
    # Note: This triggers artifact.state.updated event to all connected clients
    # Frontend will automatically re-render the artifact with new data
```

**Key Point**: Use partial updates — only send changed fields, not entire state.

---

### Pattern 3: Completing a Workflow

**Scenario**: User finishes generating their app, mark Generator workflow complete.

```python
async def finalize_app_generation(chat_id: str, app_id: str, artifact_id: str):
    """
    Complete the AppBuilder workflow and finalize artifact state.
    """
    # Step 1: Update artifact to final state
    await session_manager.update_artifact_state(
        artifact_id=artifact_id,
        app_id=app_id,
        state_updates={
            "build_progress": 100,
            "deployment_status": "ready_to_deploy",
            "completed_at": time.time()
        }
    )
    
    # Step 2: Mark workflow session as COMPLETED
    await session_manager.complete_workflow_session(
        chat_id=chat_id,
        app_id=app_id
    )
    
    # Now other workflows that depend on AppBuilder can launch
    # (e.g., MarketingAutomation requires AppBuilder COMPLETED)
```

---

## 📋 Common Integration Patterns

### Pattern 4: Resuming an Existing Workflow

**Scenario**: User returns to an IN_PROGRESS AppBuilder session.

```python
async def resume_workflow(chat_id: str, app_id: str):
    """
    Resume an existing workflow session.
    """
    # Retrieve session
    session = await session_manager.get_workflow_session(
        chat_id=chat_id,
        app_id=app_id
    )
    
    if not session:
        raise ValueError(f"Session {chat_id} not found")
    
    if session["status"] == "COMPLETED":
        # Workflow already done, maybe redirect to results
        return {"status": "already_completed"}
    
    # Retrieve artifact state
    artifact_id = session.get("artifact_instance_id")
    if artifact_id:
        artifact = await session_manager.get_artifact_instance(
            artifact_id=artifact_id,
            app_id=app_id
        )
        current_state = artifact["state"]
        
        # Continue from where user left off
        return {
            "status": "resumed",
            "workflow_name": session["workflow_name"],
            "artifact_state": current_state
        }
    
    return {"status": "no_artifact"}
```

---

### Pattern 5: Handling Multi-Step Progress

**Scenario**: User building app with multiple stages (design → code → test → deploy).

```python
async def advance_to_next_stage(
    artifact_id: str,
    app_id: str,
    current_stage: str,
    completed_stages: list
):
    """
    Update artifact state when moving to next build stage.
    """
    stage_order = ["design", "code", "test", "deploy"]
    current_index = stage_order.index(current_stage)
    
    if current_index < len(stage_order) - 1:
        next_stage = stage_order[current_index + 1]
        completed_stages.append(current_stage)
        
        await session_manager.update_artifact_state(
            artifact_id=artifact_id,
            app_id=app_id,
            state_updates={
                "current_stage": next_stage,
                "completed_stages": completed_stages,
                "build_progress": (len(completed_stages) / len(stage_order)) * 100
            }
        )
        
        return next_stage
    else:
        # All stages complete
        return "finished"
```

---

### Pattern 6: Creating Revenue Dashboard Artifact

**Scenario**: User clicks "View Revenue" button in AppBuilder artifact.

```python
async def create_revenue_dashboard(
    app_id: str,
    user_id: str,
    app_ids: list
):
    """
    Create RevenueDashboard workflow and artifact.
    
    Note: This is typically called by simple_transport.py when handling
    artifact_action with action="launch_workflow", but shown here for clarity.
    """
    # Create session
    session = await session_manager.create_workflow_session(
        app_id=app_id,
        user_id=user_id,
        workflow_name="RevenueDashboard"
    )
    
    # Fetch revenue data for user's apps
    total_revenue = await calculate_total_revenue(app_id, app_ids)
    revenue_by_app = await get_revenue_breakdown(app_id, app_ids)
    
    # Create artifact with revenue data
    artifact = await session_manager.create_artifact_instance(
        app_id=app_id,
        workflow_name="RevenueDashboard",
        artifact_type="RevenueDashboard",
        initial_state={
            "total_revenue": total_revenue,
            "apps": revenue_by_app,
            "last_updated": time.time(),
            "chart_data": await generate_chart_data(revenue_by_app)
        }
    )
    
    # Link them
    await session_manager.attach_artifact_to_session(
        chat_id=session["_id"],
        artifact_id=artifact["_id"],
        app_id=app_id
    )
    
    return session["_id"], artifact["_id"]
```

---

## 🔧 Integration with AG2 Workflows

### Registering Tools that Use Session Manager

**Example: Tool to update app build progress**

```python
from autogen import register_function

async def update_app_progress_tool(
    artifact_id: str,
    progress: int,
    status_message: str,
    context: dict
) -> str:
    """
    AG2 tool that updates app builder artifact state.
    
    Args:
        artifact_id: Artifact instance ID (from context)
        progress: Build progress percentage (0-100)
        status_message: Human-readable status
        context: AG2 context with app_id
    """
    app_id = context.get("app_id")
    
    if not app_id:
        return "Error: Missing app_id in context"
    
    try:
        await session_manager.update_artifact_state(
            artifact_id=artifact_id,
            app_id=app_id,
            state_updates={
                "build_progress": progress,
                "status_message": status_message,
                "last_updated": time.time()
            }
        )
        return f"✅ Updated build progress to {progress}%"
    except Exception as e:
        return f"❌ Failed to update progress: {str(e)}"


# Register with AG2
register_function(
    update_app_progress_tool,
    caller=assistant_agent,
    executor=user_proxy,
    name="update_app_progress",
    description="Update the build progress of the current app in the artifact UI"
)
```

---

### Accessing Artifact ID from AG2 Context

**Pattern**: Store artifact_id in AG2 chat context when workflow starts.

```python
# When starting workflow (in orchestration_patterns.py or similar)
async def start_appbuilder_with_ag2(app_id: str, user_id: str, app_name: str):
    # Create session and artifact
    chat_id, artifact_id = await start_appbuilder_workflow(
        app_id, user_id, app_name
    )
    
    # Store in AG2 context
    ag2_context = {
        "app_id": app_id,
        "user_id": user_id,
        "artifact_id": artifact_id,  # Tools can access this
        "workflow_name": "AppBuilder",
        "app_name": app_name
    }
    
    # Pass to AG2 GroupChat
    # (Initialize AG2 agents, GroupChat, and start conversation with context)
    # See AG2 documentation for agent initialization patterns
    
    return chat_id, ag2_context
```

---

## 🎨 Platform-Specific Examples

### Example 1: Investment Marketplace (No Dependencies)

```python
async def create_investment_marketplace(app_id: str, user_id: str):
    """
    Create Investment Marketplace workflow.
    No dependencies - can be launched anytime.
    """
    session = await session_manager.create_workflow_session(
        app_id=app_id,
        user_id=user_id,
        workflow_name="InvestmentMarketplace"
    )
    
    # Fetch available apps to invest in
    available_apps = await fetch_investable_apps(app_id)
    
    artifact = await session_manager.create_artifact_instance(
        app_id=app_id,
        workflow_name="InvestmentMarketplace",
        artifact_type="InvestmentMarketplace",
        initial_state={
            "apps": available_apps,
            "filters": {"category": "all", "min_revenue": 0},
            "selected_app": None,
            "investment_amount": 0
        }
    )
    
    await session_manager.attach_artifact_to_session(
        chat_id=session["_id"],
        artifact_id=artifact["_id"],
        app_id=app_id
    )
    
    return session["_id"], artifact["_id"]
```

---

### Example 2: Marketing Automation (Has Dependencies)

**Note**: Validation happens automatically in `simple_transport.py` before this is called.

```python
async def create_marketing_automation(
    app_id: str,
    user_id: str,
    app_id: str
):
    """
    Create Marketing Automation workflow.
    
    PREREQUISITE: Generator workflow must be COMPLETED
    (Validation already done by pack v2 prerequisite gating before calling this)
    """
    # Fetch app details
    app_info = await get_app_info(app_id, app_id)
    
    session = await session_manager.create_workflow_session(
        app_id=app_id,
        user_id=user_id,
        workflow_name="MarketingAutomation"
    )
    
    artifact = await session_manager.create_artifact_instance(
        app_id=app_id,
        workflow_name="MarketingAutomation",
        artifact_type="MarketingDashboard",
        initial_state={
            "app_id": app_id,
            "app_name": app_info["name"],
            "campaigns": [],
            "target_audience": None,
            "budget": 0,
            "channels": ["email", "social", "seo"]
        }
    )
    
    await session_manager.attach_artifact_to_session(
        chat_id=session["_id"],
        artifact_id=artifact["_id"],
        app_id=app_id
    )
    
    return session["_id"], artifact["_id"]
```

---

### Example 3: Challenge Tracker (Incremental Progress)

```python
async def update_challenge_progress(
    artifact_id: str,
    app_id: str,
    step_completed: int,
    total_steps: int
):
    """
    Update challenge tracker as user completes steps.
    """
    progress_percentage = (step_completed / total_steps) * 100
    
    await session_manager.update_artifact_state(
        artifact_id=artifact_id,
        app_id=app_id,
        state_updates={
            "completed_steps": step_completed,
            "total_steps": total_steps,
            "progress": progress_percentage,
            "last_activity": time.time()
        }
    )
    
    # Check if challenge complete
    if step_completed >= total_steps:
        await session_manager.update_artifact_state(
            artifact_id=artifact_id,
            app_id=app_id,
            state_updates={
                "status": "completed",
                "completion_time": time.time()
            }
        )
        
        # Maybe mark workflow session as COMPLETED too
        # (depends on your business logic)
```

---

## ⚠️ Best Practices

### 1. Always Include App ID
```python
# ✅ CORRECT
await session_manager.create_workflow_session(
    app_id=app_id,  # Required for multi-tenancy
    user_id=user_id,
    workflow_name="AppBuilder"
)

# ❌ WRONG - Missing app_id
await session_manager.create_workflow_session(
    user_id=user_id,
    workflow_name="AppBuilder"
)
```

---

### 2. Use Partial Updates for Artifact State
```python
# ✅ CORRECT - Only update changed fields
await session_manager.update_artifact_state(
    artifact_id=artifact_id,
    app_id=app_id,
    state_updates={"build_progress": 75}
)

# ❌ WRONG - Don't replace entire state
artifact = await session_manager.get_artifact_instance(artifact_id, app_id)
artifact["state"]["build_progress"] = 75
await session_manager.update_artifact_state(
    artifact_id=artifact_id,
    app_id=app_id,
    state_updates=artifact["state"]  # Overwrites everything
)
```

---

### 3. Handle Session Not Found
```python
# ✅ CORRECT
session = await session_manager.get_workflow_session(chat_id, app_id)
if not session:
    # Handle gracefully
    return {"error": "Session not found", "chat_id": chat_id}

# ❌ WRONG - Assumes session exists
session = await session_manager.get_workflow_session(chat_id, app_id)
workflow_name = session["workflow_name"]  # Crashes if session is None
```

---

### 4. Complete Workflows When Appropriate
```python
# ✅ CORRECT - Mark as COMPLETED when workflow finishes
await session_manager.complete_workflow_session(chat_id, app_id)

# Note: Don't mark as COMPLETED prematurely
# Other workflows may depend on this being COMPLETED
```

---

### 5. Store Artifact ID in Context
```python
# ✅ CORRECT - Make artifact_id available to tools
ag2_context = {
    "app_id": app_id,
    "artifact_id": artifact_id,  # Tools can access this
    "chat_id": chat_id
}

# Pass to AG2 agents
# Tools can then call session_manager.update_artifact_state()
```

---

## 🐛 Common Issues

### Issue 1: Artifact State Not Updating
**Symptom**: You call `update_artifact_state()` but frontend doesn't show changes.

**Solutions**:
1. Verify `artifact_id` and `app_id` are correct
2. Check that `simple_transport.py` broadcasts `artifact.state.updated` event
3. Confirm frontend is listening for the event
4. Check MongoDB collection directly: `db.ArtifactInstances.find({_id: "artifact_xxx"})`

---

### Issue 2: Workflow Blocked by Dependencies
**Symptom**: User can't launch workflow, sees "Please complete X first" error.

**Solutions**:
1. Check `workflows/_pack/workflow_graph.json` for `gates[]` / `journeys[]` prerequisites
2. Verify the required workflow has a `COMPLETED` chat session in `ChatSessions`
3. See gating logic in `core/workflow/pack_gating.py`
4. Use `validate_pack_prereqs()` to debug

---

### Issue 3: Multiple Sessions Created for Same Workflow
**Symptom**: User has 2+ IN_PROGRESS sessions for AppBuilder.

**Explanation**: This is **intentional**! Users can build multiple apps simultaneously.

**Solution**: If you want to prevent this, add custom logic:
```python
# Check if user already has an IN_PROGRESS AppBuilder session
existing = await pm._coll("WorkflowSessions").find_one({
    "app_id": app_id,
    "user_id": user_id,
    "workflow_name": "AppBuilder",
    "status": "IN_PROGRESS"
})

if existing:
    return {"error": "You already have an app in progress", "chat_id": existing["_id"]}
```

---

## 📚 Next Steps

Now that you understand backend integration:
1. **Frontend Integration** → [`04-FRONTEND-INTEGRATION.md`](./04-FRONTEND-INTEGRATION.md) - Wire React components
2. **Complete Examples** → [`05-EXAMPLES.md`](./05-EXAMPLES.md) - Full end-to-end code
3. **Troubleshooting** → [`06-TROUBLESHOOTING.md`](./06-TROUBLESHOOTING.md) - Debug common issues

---

## 🔗 Reference

**Session Manager Functions**:
- Located in `core/workflow/session_manager.py`
- All functions are async
- All require `app_id` for multi-tenancy

**Collections**:
- `WorkflowSessions` - Chat conversations per workflow
- `ArtifactInstances` - Persistent UI state
- `ChatSessions` - Lean workflow runs (status + transcript)

**Transport Integration**:
- `simple_transport.py` handles `artifact_action` events
- Calls session_manager functions
- Broadcasts events to frontend

**Dependency Validation**:
- `core/workflow/pack_gating.py`
- `validate_pack_prereqs(app_id=..., user_id=..., workflow_name=...)`
- Returns `(bool, error_message)` tuple
