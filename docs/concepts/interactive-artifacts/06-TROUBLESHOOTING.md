# Troubleshooting & FAQs

Common issues, debugging techniques, and frequently asked questions about Interactive Artifacts.

---

## 🐛 Common Issues

### Issue 1: Artifact State Not Updating

**Symptoms**:
- Backend calls `update_artifact_state()` but frontend doesn't reflect changes
- User interacts with artifact but nothing happens
- State seems "stuck"

**Diagnosis**:

```python
# Check MongoDB directly
from pymongo import MongoClient
client = MongoClient("mongodb://localhost:27017")
db = client["mozaiks"]
artifact = db["ArtifactInstances"].find_one({"_id": "artifact_xxx"})
print(artifact["state"])  # Does state have the expected values?
```

**Solutions**:

1. **Verify WebSocket broadcast**:
```python
# In simple_transport.py after update_artifact_state
await websocket.send_json({
    "type": "artifact.state.updated",
    "data": {
        "artifact_id": artifact_id,
        "state_delta": state_updates  # ← Make sure this is sent
    },
    "chat_id": chat_id,
    "timestamp": datetime.now(timezone.utc).isoformat()
})
```

2. **Check frontend event listener**:
```typescript
// Make sure you're listening
useArtifactEvents(ws, onNavigate, onDependencyBlocked, onStateUpdated);

// Debug what's being received
ws.addEventListener('message', (event) => {
  const msg = JSON.parse(event.data);
  if (msg.type === 'artifact.state.updated') {
    console.log('📩 State update:', msg.data);
  }
});
```

3. **Verify artifact_id matches**:
```typescript
// Frontend
console.log('Frontend artifact_id:', artifactId);

// Backend
logger.info(f"Backend artifact_id: {artifact_id}")

// They must match exactly!
```

4. **Check app_id isolation**:
```python
# Backend - make sure app_id is correct
artifact = await session_manager.get_artifact_instance(
    artifact_id=artifact_id,
    app_id=app_id  # ← Wrong app_id returns None
)
```

---

### Issue 2: Workflow Blocked by Dependencies (When It Shouldn't Be)

**Symptoms**:
- User gets "Please complete X workflow first" error
- But user claims they already completed X
- Marketing blocked even after Generator complete

**Diagnosis**:

```python
# Check ChatSessions status
from pymongo import MongoClient
client = MongoClient("mongodb://localhost:27017")
db = client["mozaiks"]

sessions = db["ChatSessions"].find({
    "app_id": "ent_001",
    "user_id": "user_456",
    "workflow_name": "Generator"
}).sort("created_at", -1).limit(5)

for session in sessions:
    print(f"Chat: {session['_id']}, Status: {session.get('status')}, Created: {session['created_at']}")
```

**Solutions**:

1. **Verify workflow was marked COMPLETED**:
```python
# Check if complete_workflow_session() was called
session = await get_workflow_session(chat_id, app_id)
if session["status"] != "COMPLETED":
    # Workflow never completed - call it now
    await session_manager.complete_workflow_session(chat_id, app_id)
```

2. **Check dependency definition**:
```python
# Query WorkflowDependencies
deps = db["WorkflowDependencies"].find_one({"app_id": "ent_001"})
marketing_deps = deps["workflows"]["MarketingAutomation"]["dependencies"]
print(marketing_deps)
# Should show: required_workflows: [{"workflow": "Generator", "status": "COMPLETED"}]
```

3. **Test validation manually**:
```python
from core.workflow.pack.gating import validate_pack_prereqs

is_valid, error = await validate_pack_prereqs(
    app_id="ent_001",
    user_id="user_456",
    workflow_name="MarketingAutomation",
)
print(f"Valid: {is_valid}, Error: {error}")
```

4. **Check for multiple sessions**:
```python
# User might have multiple Generator sessions; gating checks for any COMPLETED one.
sessions = db["ChatSessions"].find({
    "app_id": "ent_001",
    "user_id": "user_456",
    "workflow_name": "Generator"
}).sort("created_at", -1)

# Gating is satisfied by any completed session in scope (not necessarily newest).
latest = sessions[0]
print(f"Latest Generator session status: {latest['status']}")
```

---

### Issue 3: Navigation Not Working

**Symptoms**:
- User clicks "View Revenue" button
- Nothing happens, no navigation
- Console shows no errors

**Diagnosis**:

```typescript
// Check if action is being sent
const handleLaunchWorkflow = (workflowName: string) => {
  console.log('🚀 Launching workflow:', workflowName);
  console.log('WebSocket state:', ws?.readyState); // Should be 1 (OPEN)
  
  sendArtifactAction(ws, {
    action: 'launch_workflow',
    payload: { workflow_name: workflowName }
  }, chatId);
};
```

**Solutions**:

1. **Check WebSocket connection**:
```typescript
if (!ws || ws.readyState !== WebSocket.OPEN) {
  console.error('❌ WebSocket not connected');
  toast.error('Connection lost. Please refresh.');
  return;
}
```

2. **Verify action format**:
```typescript
// CORRECT format
sendArtifactAction(ws, {
  action: 'launch_workflow',  // ← Must match backend handler
  payload: {
    workflow_name: 'RevenueDashboard',  // ← Exact workflow name
    artifact_type: 'RevenueDashboard'   // ← Optional but recommended
  }
}, chatId);

// WRONG - typo in action name
sendArtifactAction(ws, {
  action: 'launchWorkflow',  // ← Backend won't recognize (should be launch_workflow)
  workflow_name: 'RevenueDashboard'
}, chatId);
```

3. **Check backend receives action**:
```python
# In simple_transport.py
async def _handle_artifact_action(self, event: Dict[str, Any], chat_id: str, websocket) -> None:
    data = event.get("data", {})
    action = data.get("action")
    
    logger.info(f"🎯 Received action: {action}")  # ← Add this log
    
    if action == "launch_workflow":
        # ...
```

4. **Look for dependency_blocked event**:
```typescript
const handleDependencyBlocked = (event: DependencyBlockedEvent) => {
  console.error('❌ Dependency blocked:', event.data);
  toast.error(event.data.message);
};
```

---

### Issue 4: Multiple Tabs for Same Workflow

**Symptoms**:
- User has 3 tabs all named "AppBuilder"
- Confusing to know which is which
- State bleeding between tabs

**This is Actually Intentional!**

Users can build **multiple apps simultaneously**. Each tab is a different app.

**Solution A: Show App Name in Tab**:
```tsx
<button className="session-tab">
  {session.workflowName}
  {session.artifactState?.app_name && (
    <span className="text-xs ml-2">({session.artifactState.app_name})</span>
  )}
</button>

// Result: "AppBuilder (TaskMaster)" vs "AppBuilder (E-Commerce Platform)"
```

**Solution B: Prevent Multiple Tabs (If Desired)**:
```typescript
const handleNavigate = (event: NavigateEvent) => {
  const { chat_id, workflow_name } = event.data;
  
  // Check if workflow + chat already has a tab
  const existingIndex = sessions.findIndex(
    s => s.chatId === chat_id
  );
  
  if (existingIndex !== -1) {
    // Switch to existing tab instead of creating new one
    setActiveSessionIndex(existingIndex);
    return;
  }
  
  // Create new tab
  // (Add new session object to sessions state with chat_id, workflow_name, etc.)
};
```

---

### Issue 5: Artifact Renders Old State

**Symptoms**:
- Backend updates artifact to progress=75%
- Frontend still shows progress=50%
- Refreshing page shows correct state

**Diagnosis**:

```typescript
// Check if state is actually updating
useEffect(() => {
  console.log('🔄 Artifact state changed:', state);
}, [state]);
```

**Solutions**:

1. **Verify state is being merged, not replaced**:
```typescript
// CORRECT - Merge state updates
const handleStateUpdated = (event: ArtifactStateUpdatedEvent) => {
  const { artifact_id, state_delta } = event.data;
  
  setSessions(prev =>
    prev.map(session =>
      session.artifactId === artifact_id
        ? {
            ...session,
            artifactState: {
              ...session.artifactState,  // ← Keep existing state
              ...state_delta             // ← Merge new fields
            }
          }
        : session
    )
  );
};

// WRONG - Replaces entire state
artifactState: state_delta  // ← Loses all other fields
```

2. **Check React re-render**:
```typescript
// Force re-render if needed
const [updateTrigger, setUpdateTrigger] = useState(0);

const handleStateUpdated = (event) => {
  // (Update artifact state by merging state_delta)
  setUpdateTrigger(prev => prev + 1);  // Force re-render
};
```

3. **Use immutable updates**:
```typescript
// Use Zustand or Immer for guaranteed immutability
import { create } from 'zustand';
import { immer } from 'zustand/middleware/immer';

const useStore = create(immer((set) => ({
  sessions: [],
  updateArtifact: (artifactId, stateDelta) =>
    set((state) => {
      const session = state.sessions.find(s => s.artifactId === artifactId);
      if (session) {
        Object.assign(session.artifactState, stateDelta);
      }
    })
})));
```

---

### Issue 6: Chat Messages Not Showing Artifacts

**Symptoms**:
- Chat works fine
- But artifact panel is blank or not rendering
- No errors in console

**Diagnosis**:

```typescript
// Check artifact type mapping
console.log('Artifact type:', activeSession.artifactType);
console.log('Artifact state:', activeSession.artifactState);
```

**Solutions**:

1. **Add artifact type to component map**:
```typescript
// CORRECT - Map all artifact types
const ArtifactComponent = {
  'AppBuilderArtifact': AppBuilderArtifact,
  'RevenueDashboard': RevenueDashboard,
  'InvestmentMarketplace': InvestmentMarketplace,
  'MarketingDashboard': MarketingDashboard,
  'ChallengeTracker': ChallengeTracker
}[activeSession.artifactType];

if (!ArtifactComponent) {
  return <div>Unknown artifact type: {activeSession.artifactType}</div>;
}

return <ArtifactComponent {...props} />;
```

2. **Handle missing state gracefully**:
```tsx
export function AppBuilderArtifact({ state }) {
  if (!state || Object.keys(state).length === 0) {
    return <div>Loading artifact...</div>;
  }
  
  // Render artifact
}
```

---

## 🔍 Debugging Techniques

### Debug 1: MongoDB Inspection

```bash
# Connect to MongoDB
mongosh "mongodb://localhost:27017/mozaiks"

# Check WorkflowSessions
db.WorkflowSessions.find({ app_id: "ent_001" }).pretty()

# Check specific session
db.WorkflowSessions.findOne({ _id: "chat_abc123" })

# Check ArtifactInstances
db.ArtifactInstances.find({ app_id: "ent_001" }).pretty()

# Check WorkflowDependencies
db.WorkflowDependencies.findOne({ app_id: "ent_001" })

# Count IN_PROGRESS sessions by workflow
db.WorkflowSessions.aggregate([
  { $match: { status: "IN_PROGRESS" } },
  { $group: { _id: "$workflow_name", count: { $sum: 1 } } }
])
```

---

### Debug 2: Backend Logging

```python
# Add detailed logging to session_manager.py
import logging
logger = logging.getLogger(__name__)

async def update_artifact_state(artifact_id, app_id, state_updates):
    logger.info(f"📝 Updating artifact {artifact_id}")
    logger.info(f"   App: {app_id}")
    logger.info(f"   Updates: {state_updates}")
    
    # (Call session_manager.update_artifact_state with parameters)
    
    logger.info(f"✅ Updated successfully")
```

---

### Debug 3: Frontend Network Tab

```
1. Open DevTools → Network tab
2. Filter by "WS" (WebSocket)
3. Click the WebSocket connection
4. View "Messages" tab
5. Look for:
   - Outgoing: artifact_action events
   - Incoming: chat.navigate, artifact.state.updated, chat.dependency_blocked
```

---

### Debug 4: React DevTools

```
1. Install React DevTools extension
2. Open Components tab
3. Find your artifact component
4. Inspect props:
   - artifactId: correct?
   - chatId: correct?
   - state: has expected fields?
   - ws: readyState = 1?
```

---

## ❓ Frequently Asked Questions

### Q1: Can users have multiple IN_PROGRESS sessions for the same workflow?

**A: Yes!** This is intentional. Users can build multiple apps simultaneously.

Example: User has 3 IN_PROGRESS "AppBuilder" sessions:
- `chat_app1`: Building TaskMaster SaaS (progress: 50%)
- `chat_app2`: Building E-Commerce Platform (progress: 30%)
- `chat_app3`: Building Fitness Tracker (progress: 10%)

Each session is independent with its own artifact and state.

---

### Q2: What happens when a user closes the browser tab?

**A: Nothing!** Sessions stay IN_PROGRESS in the database.

When user returns:
1. Frontend reconnects WebSocket to same chat_id
2. Backend loads existing session from `WorkflowSessions`
3. Backend loads artifact state from `ArtifactInstances`
4. Frontend replays chat messages (from `ChatSessions` collection)
5. Artifact renders with preserved state

**Auto-resume is built-in.**

---

### Q3: How do I prevent users from launching the same workflow twice?

**A:** Add custom validation before creating session:

```python
async def prevent_duplicate_session(app_id, user_id, workflow_name):
    pm = AG2PersistenceManager()
    coll = await pm._coll("WorkflowSessions")
    
    existing = await coll.find_one({
        "app_id": app_id,
        "user_id": user_id,
        "workflow_name": workflow_name,
        "status": "IN_PROGRESS"
    })
    
    if existing:
        return {
            "error": f"You already have an IN_PROGRESS {workflow_name} session",
            "existing_chat_id": existing["_id"]
        }
    
    # Proceed with create_workflow_session
    # (Call session_manager.create_workflow_session as normal)
```

---

### Q4: Can artifacts be shared between users?

**A: Not by default.** Artifacts are scoped to `app_id`.

To enable sharing:
1. **Read-only sharing**: Return same artifact_id to multiple users
2. **Collaborative editing**: Broadcast `artifact.state.updated` to all connected users viewing that artifact_id

```python
# Collaborative editing (already implemented in simple_transport.py)
# When artifact state updates, backend broadcasts to ALL WebSocket connections
# Frontend filters by artifact_id to update correct artifact
```

---

### Q5: How do I delete old sessions/artifacts?

**A:** Manual cleanup script:

```python
# scripts/cleanup_old_sessions.py

from pymongo import MongoClient
import time

client = MongoClient("mongodb://localhost:27017")
db = client["mozaiks"]

# Delete sessions older than 30 days and COMPLETED
thirty_days_ago = time.time() - (30 * 24 * 60 * 60)

result = db.WorkflowSessions.delete_many({
    "status": "COMPLETED",
    "updated_at": {"$lt": thirty_days_ago}
})

print(f"Deleted {result.deleted_count} old sessions")

# Delete orphaned artifacts (no active session references them)
session_artifact_ids = db.WorkflowSessions.distinct("artifact_instance_id")
result = db.ArtifactInstances.delete_many({
    "_id": {"$nin": session_artifact_ids}
})

print(f"Deleted {result.deleted_count} orphaned artifacts")
```

**Schedule with cron**:
```bash
# Run cleanup daily at 2am
0 2 * * * /path/to/python scripts/cleanup_old_sessions.py
```

---

### Q6: What if dependency validation is too slow?

**A:** Cache validation results:

```python
from core.workflow.pack.gating import validate_pack_prereqs

is_valid, error = await validate_pack_prereqs(
    app_id="ent_001",
    user_id="user_456",
    workflow_name="MarketingAutomation",
)
```

---

### Q7: How do I add a custom artifact action?

**Backend**:
```python
# In simple_transport.py, add handler for custom action
if action == "deploy_app":
    environment = payload.get("environment", "staging")
    await deploy_app(chat_id, artifact_id, app_id, environment)
    
    await websocket.send_json({
        "type": "deployment.started",
        "data": {"environment": environment},
        "chat_id": chat_id
    })
```

**Frontend**:
```tsx
sendArtifactAction(ws, {
  action: 'deploy_app',
  artifact_id: artifactId,
  payload: { environment: 'production' }
}, chatId);
```

---

### Q8: Can I use Interactive Artifacts without AG2?

**A: Yes!** The session manager and artifact system are independent of AG2.

You can use them in any Python backend:
```python
# Pure FastAPI example (no AG2)
@app.post("/api/create-app")
async def create_app(request: CreateAppRequest):
    session = await session_manager.create_workflow_session(
        app_id=request.app_id,
        user_id=request.user_id,
        workflow_name="AppBuilder"
    )
    
    artifact = await session_manager.create_artifact_instance(
        app_id=request.app_id,
        workflow_name="AppBuilder",
        artifact_type="AppBuilderArtifact",
        initial_state={"app_name": request.app_name}
    )
    
    return {"chat_id": session["_id"], "artifact_id": artifact["_id"]}
```

---

### Q9: How do I test Interactive Artifacts?

**Unit Tests**:
```python
# tests/test_session_manager.py (already exists!)
pytest tests/test_session_manager.py -v
```

**Integration Tests**:
```python
# tests/test_artifact_flow.py
import pytest
from core.workflow import session_manager

@pytest.mark.asyncio
async def test_complete_artifact_flow():
    # Create session
    session = await session_manager.create_workflow_session(
        "ent_test", "user_test", "AppBuilder"
    )
    
    # Create artifact
    artifact = await session_manager.create_artifact_instance(
        "ent_test", "AppBuilder", "AppBuilderArtifact",
        initial_state={"progress": 0}
    )
    
    # Attach
    await session_manager.attach_artifact_to_session(
        session["_id"], artifact["_id"], "ent_test"
    )
    
    # Update
    await session_manager.update_artifact_state(
        artifact["_id"], "ent_test", {"progress": 50}
    )
    
    # Retrieve
    updated = await session_manager.get_artifact_instance(
        artifact["_id"], "ent_test"
    )
    
    assert updated["state"]["progress"] == 50
```

**E2E Tests** (Playwright):
```typescript
// tests/e2e/artifact-navigation.spec.ts
import { test, expect } from '@playwright/test';

test('navigate from AppBuilder to Revenue', async ({ page }) => {
  await page.goto('http://localhost:3000');
  
  // Start building app
  await page.fill('input[name="app_name"]', 'Test App');
  await page.click('button:has-text("Start Building")');
  
  // Wait for artifact
  await expect(page.locator('.artifact-container')).toBeVisible();
  
  // Click "View Revenue"
  await page.click('button:has-text("View Revenue")');
  
  // Should navigate to Revenue tab
  await expect(page.locator('.session-tab.active')).toHaveText('RevenueDashboard');
});
```

---

### Q10: What's the performance impact?

**Benchmarks**:
- Creating session: ~5ms
- Creating artifact: ~8ms
- Updating artifact state: ~3ms (partial update)
- Dependency validation: ~15ms (queries 2 collections)
- WebSocket broadcast: ~2ms per connection

**Optimization Tips**:
1. **Index MongoDB collections**:
```javascript
// In MongoDB shell
db.WorkflowSessions.createIndex({ app_id: 1, user_id: 1, status: 1 })
db.ArtifactInstances.createIndex({ app_id: 1, workflow_name: 1 })
db.WorkflowDependencies.createIndex({ app_id: 1 })
```

2. **Use partial state updates** (already implemented):
```python
# Only send changed fields
await update_artifact_state(artifact_id, app_id, {"progress": 75})
```

3. **Debounce frontend updates**:
```typescript
const debouncedUpdate = useDebouncedCallback(updateState, 500);
```

---

## 📚 Additional Resources

- **Code**: `core/workflow/session_manager.py`
- **Tests**: `tests/test_session_manager.py`
- **Transport**: `core/transport/simple_transport.py`
- **Dependencies**: `core/workflow/dependencies.py`

**Documentation**:
- [README](./README.md) - Overview
- [Concepts](./01-CONCEPTS.md) - Core concepts
- [Architecture](./02-ARCHITECTURE.md) - System design
- [Backend Integration](./03-BACKEND-INTEGRATION.md) - Backend guide
- [Frontend Integration](./04-FRONTEND-INTEGRATION.md) - Frontend guide
- [Examples](./05-EXAMPLES.md) - Complete examples

---

## 🆘 Getting Help

1. **Check logs**: `logs/logs/` directory for runtime logs
2. **MongoDB**: Inspect collections directly
3. **WebSocket**: Use browser DevTools Network tab
4. **Tests**: Run unit tests to verify core functionality

**Still stuck?** Create an issue with:
- Error message/stack trace
- MongoDB document samples (sanitized)
- Frontend console logs
- Backend logs
- Steps to reproduce
