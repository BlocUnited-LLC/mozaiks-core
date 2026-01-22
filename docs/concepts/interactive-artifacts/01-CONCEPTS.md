# Core Concepts & Terminology

This document explains the fundamental building blocks of the Interactive Artifacts system in the context of **MozaiksAI** — an AI-driven app development + investment platform.

---

## 🧱 Core Components

### 1. WorkflowSession

**What it is**: A chat conversation within a specific context (building an app, checking revenue, browsing investments, tracking a challenge, etc.)

**Think of it as**: A browser tab for a specific task. You can have unlimited tabs open (building 5 apps, checking revenue, browsing marketplace) and switch between them freely.

```python
{
  "_id": "chat_appbuilder_taskapp",  # Unique chat ID
  "app_id": "ent-001",        # User's account
  "user_id": "user-456",             # Which user
  "workflow_name": "AppBuilder",     # AppBuilder | RevenueDashboard | InvestmentMarketplace | ChallengeTracker
  "status": "IN_PROGRESS",           # Current state (see lifecycle below)
  "artifact_instance_id": "artifact_taskapp",  # Links to the persistent app artifact
  "created_at": 1699564800.0,
  "updated_at": 1699568400.0
}
```

**Real Platform Examples**:
- `workflow_name: "AppBuilder"` - User is actively building a SaaS app
- `workflow_name: "RevenueDashboard"` - User is checking earnings from their app portfolio
- `workflow_name: "InvestmentMarketplace"` - User is browsing apps to invest in
- `workflow_name: "InvestmentPortfolio"` - User is tracking their investment returns
- `workflow_name: "ChallengeTracker"` - User is participating in a coding challenge

**Lifecycle States**:
- `IN_PROGRESS` - User can interact with this context anytime (you can have unlimited IN_PROGRESS sessions)
- `COMPLETED` - Workflow finished (app fully deployed, challenge submitted, etc.)

**Key Operations**:
```python
# User says: "Build me a task management app"
# → Create AppBuilder session
session = await create_workflow_session("ent-001", "user-456", "AppBuilder")

# User clicks "View Revenue" button in app artifact
# → Create RevenueDashboard session (AppBuilder stays IN_PROGRESS)
revenue_session = await create_workflow_session("ent-001", "user-456", "RevenueDashboard")

# User says: "Back to building my app"
# → AI automatically navigates to chat_appbuilder_taskapp (still IN_PROGRESS)

# App fully deployed and live
await complete_workflow_session("chat_appbuilder_taskapp", "ent-001")
```

---

### 2. ArtifactInstance

**What it is**: A persistent app/UI component with state that lives permanently (until user deletes it).

**Think of it as**: The actual app itself. Your app builder shows code generation progress, your revenue dashboard shows real-time earnings, your investment portfolio shows your stakes and returns. Each maintains its own state.

```python
{
  "_id": "artifact_taskapp",
  "app_id": "ent-001",
  "workflow_name": "AppBuilder",
  "artifact_type": "AppBuilderArtifact",  # Type determines which React component renders it
  "state": {                              # Arbitrary JSON — whatever your artifact needs
    "app_name": "TaskMaster SaaS",
    "architecture": "Next.js + Supabase",
    "features": ["auth", "tasks", "teams", "billing"],
    "build_progress": 67,  # Percentage
    "deployment_status": "staging",
    "repository_url": "github.com/user/taskmaster",
    "revenue_to_date": 47.32,
    "buttons": [
      {"label": "View Revenue", "action": "launch_workflow", "workflow": "RevenueDashboard"},
      {"label": "Deploy to Production", "action": "deploy_app"}
    ]
  },
  "last_active_chat_id": "chat_appbuilder_taskapp",  # Which chat last used this
  "created_at": 1699564800.0,
  "updated_at": 1699568400.0
}
```

**Artifact Types** (MozaiksAI platform examples):
- `AppBuilderArtifact` - Shows code generation progress, architecture, deployment status, revenue button
- `RevenueDashboard` - Real-time earnings, costs, ROI charts across all your apps
- `InvestmentPortfolio` - Apps you've invested in, your stakes, returns, portfolio performance
- `InvestmentMarketplace` - Browseable list of apps built by others, invest buttons, filtering
- `ChallengeTracker` - Competition progress, leaderboard, submission deadline, prize pool
- `AppDetailsView` - Deep dive into a specific app (your own or one you invested in) — metrics, code, logs

**Key Operations**:
```python
# User starts building a new app
artifact = await create_artifact_instance(
    "ent-001",
    "AppBuilder",
    "AppBuilderArtifact",
    initial_state={
        "app_name": "TaskMaster SaaS",
        "build_progress": 0,
        "features": []
    }
)

# Link artifact to AppBuilder session
await attach_artifact_to_session("chat_appbuilder_taskapp", artifact["_id"], "ent-001")

# AI generates more code → update progress
await update_artifact_state(
    artifact["_id"],
    "ent-001",
    {"build_progress": 45, "features": ["auth", "tasks"]}  # Partial update
)

# User navigates to revenue dashboard, then back
# → Artifact state preserved: still shows 45% progress

# Retrieve current state
artifact = await get_artifact_instance(artifact["_id"], "ent-001")
print(artifact["state"]["build_progress"])  # 45
```

---

### 3. Artifact Actions

**What they are**: Events sent from frontend to backend to trigger workflow changes or state updates.

**Think of them as**: Button clicks or user interactions that can do more than just update UI — they can launch new workflows, validate dependencies, and orchestrate backend operations.

```javascript
// Frontend sends this
{
  type: "chat.artifact_action",
  data: {
    action: "launch_workflow",  // What to do
    artifact_id: "artifact_xyz789",
    payload: {
      workflow_name: "Build",
      artifact_type: "BuilderDashboard"
    }
  },
  chat_id: "chat_abc123",
  correlation_id: "req_456"
}
```

**Standard Actions**:

#### `launch_workflow`
Starts a new workflow from current artifact.

**Example: Viewing Revenue Dashboard (NO dependencies)**
```javascript
// User clicks "View Revenue" in AppBuilder artifact
sendArtifactAction({
  action: "launch_workflow",
  payload: {
    workflow_name: "RevenueDashboard",
    artifact_type: "RevenueDashboard"
  }
})
```

**Backend behavior**:
1. ✅ Validates dependencies (RevenueDashboard has none - allowed anytime)
2. Creates new RevenueDashboard session (AppBuilder session stays IN_PROGRESS)
3. Creates RevenueDashboard artifact
4. Sends `chat.navigate` event → frontend switches to revenue view

**Example: Launching Marketing Automation (HAS dependencies)**
```javascript
// User clicks "Launch Marketing" while Generator is IN_PROGRESS
sendArtifactAction({
  action: "launch_workflow",
  payload: {
    workflow_name: "MarketingAutomation",
    artifact_type: "MarketingDashboard"
  }
})
```

**Backend behavior**:
1. ❌ Validates dependencies (Marketing requires Generator COMPLETED)
2. Finds Generator session: status = IN_PROGRESS (not COMPLETED)
3. Sends `chat.dependency_blocked` event with error message
4. Frontend shows warning: "Please complete the Generator workflow first"
5. NO session created (validation happens before any state changes)

#### `update_state`
Updates artifact state without triggering workflow.

```javascript
sendArtifactAction({
  action: "update_state",
  artifact_id: "artifact_xyz789",
  payload: {
    state_updates: {
      currentStep: 3,
      completedSteps: [0, 1, 2]
    }
  }
})
```

**Backend behavior**:
1. Updates ArtifactInstances collection
2. Broadcasts `artifact.state.updated` to all connected clients

#### Custom Actions
You can define workflow-specific actions:

```javascript
sendArtifactAction({
  action: "add_player_to_roster",
  artifact_id: "artifact_fantasy123",
  payload: {
    player_id: "player_789",
    position: "QB"
  }
})
```

**Backend behavior**:
- Custom handler in your workflow
- Can update state, trigger tool calls, or start sub-workflows

---

### 4. Workflow Dependencies

**What they are**: Rules that define which workflows can run based on what's already completed.

**Think of them as**: Prerequisites or unlock conditions — "You must complete Generator before you can run Build."

```python
{
  "_id": "ent-001",
  "app_id": "ent-001",
  "workflows": {
    "Build": {
      "dependencies": {
        "required_workflows": [
          {"workflow": "Generator", "status": "COMPLETED"}
        ],
        "required_context_vars": ["generated_files"],
        "required_artifacts": [
          {"artifact_type": "ActionPlan", "source_workflow": "Generator"}
        ]
      },
      "provides": {
        "context_vars": ["build_output", "package_url"],
        "artifacts": ["BuilderDashboard"]
      }
    }
  }
}
```

**Validation Logic**:
```python
from core.workflow.pack.gating import validate_pack_prereqs

is_valid, error_msg = await validate_pack_prereqs(
    app_id="ent-001",
    user_id="user-456",
    workflow_name="Build",
)

if not is_valid:
    # Show user: "Please complete the Generator workflow first"
    # Block navigation
```

**Types of Dependencies**:

1. **`required_workflows`**: Other workflows that must reach a certain status
   ```python
   {"workflow": "Generator", "status": "COMPLETED"}
   ```

2. **`required_context_vars`**: Variables that must exist in context
   ```python
   ["generated_files", "project_name"]
   ```

3. **`required_artifacts`**: Artifacts that must exist
   ```python
   {"artifact_type": "ActionPlan", "source_workflow": "Generator"}
   ```

**Platform Examples (MozaiksAI)**:

| Workflow | Dependencies | Why |
|----------|--------------|-----|
| **Generator** | None | First workflow - can start anytime |
| **InvestmentMarketplace** | None | Users can browse/invest anytime, even mid-Generator |
| **RevenueDashboard** | App deployed | Can't show revenue if no app exists |
| **MarketingAutomation** | Generator COMPLETED | Needs generated app to market |
| **AppBuilder** | None | Users can build unlimited apps anytime |
| **ChallengeTracker** | None | Can join challenges anytime |

**User Journey Example**:
```
User starts: Generator (IN_PROGRESS) → building task management app
User clicks: "View Investments" → ✅ ALLOWED (no dependencies)
User asks: "Should I invest in this e-commerce app?" → ✅ Chat responds
User clicks: "Launch Marketing" → ❌ BLOCKED (Generator not COMPLETED)
   └─► Toast: "Please complete the Generator workflow first"
User completes: Generator → status = COMPLETED
User clicks: "Launch Marketing" → ✅ ALLOWED (dependencies now met)
```

---

## 🔄 State Transitions

### Session Lifecycle

```
┌─────────────┐
│   Created   │ (status: IN_PROGRESS)
└──────┬──────┘
       │
       │ User sends messages, AI responds
       │
       ▼
┌─────────────┐
│   Active    │ (status: IN_PROGRESS)
└──────┬──────┘
       │
       │ Multiple sessions can be IN_PROGRESS simultaneously
       │ User can switch between them freely
       │
       ├─────────► User navigates to another workflow
       │           └──► (this session stays IN_PROGRESS, just not actively viewed)
       │
       ├─────────► User returns to this workflow
       │           └──► (resume automatically - messages replay, state restored)
       │
       └─────────► Workflow completes
                   └──► (status: COMPLETED)
```

### Artifact Lifecycle

```
┌─────────────┐
│   Created   │ (when workflow starts)
└──────┬──────┘
       │
       ▼
┌─────────────┐
│   Active    │ (user interacts, state changes)
└──────┬──────┘
       │
       ├─────────► User closes chat
       │           └──► Artifact persists in DB
       │
       ├─────────► User reopens chat
       │           └──► Artifact state restored
       │
       └─────────► User deletes (optional)
                   └──► Artifact removed from DB
```

---

## 📡 Event Flow

### Frontend → Backend

```javascript
// User clicks button in artifact
websocket.send(JSON.stringify({
  type: "chat.artifact_action",
  data: {
    action: "launch_workflow",
    payload: { workflow_name: "Build" }
  },
  chat_id: "chat_abc123"
}))
```

### Backend → Frontend

```javascript
// Backend notifies frontend to navigate
{
  type: "chat.navigate",
  data: {
    chat_id: "chat_new456",  // New chat ID
    workflow_name: "Build",
    artifact_instance_id: "artifact_new789",
    app_id: "ent-001"
  },
  timestamp: "2025-11-10T12:00:00Z"
}
```

```javascript
// Backend notifies state change
{
  type: "artifact.state.updated",
  data: {
    artifact_id: "artifact_xyz789",
    state_delta: { currentStep: 3 }  // What changed
  },
  chat_id: "chat_abc123",
  timestamp: "2025-11-10T12:01:00Z"
}
```

---

## 🎯 Design Principles

### 1. **Artifacts are Persistent**
Once created, artifacts live in the database until explicitly deleted. They're not tied to a single session.

### 2. **Sessions are Lightweight**
Sessions are cheap to create. Users can have many IN_PROGRESS sessions simultaneously (multi-tasking). No need to "pause" — just switch between them.

### 3. **Navigation is Explicit**
Backend sends `chat.navigate` events. Frontend decides how to render (new tab, modal, replace current view, etc.).

### 4. **State is Eventual**
Artifact state updates are async. Multiple users can view the same artifact; state broadcasts keep everyone in sync.

### 5. **Dependencies are Hidden**
Users never see "workflow X depends on Y" — they just see natural error messages: "Please complete the Generator workflow first."

---

## 🔗 Key Relationships

```
App (App/Company)
  └─► WorkflowDependencies (1 per app)
       └─► workflows.{WorkflowName} (dependency graph)

User
  └─► WorkflowSessions (many)
       └─► One active artifact per session

ArtifactInstance
  ├─► Can be shared across sessions (same user)
  └─► Can be linked to multiple WorkflowSessions (over time)
```

---

## 📚 Real-World Analogy

**MozaiksAI Platform = Multi-App Startup Studio**

Think of MozaiksAI like running multiple startup companies simultaneously:

**Without Interactive Artifacts:**
```
You: "Build me a task management SaaS"
AI: [Sends you code as text]
You: [Manually create files, set up deployment]
You: "How much revenue am I making?"
AI: "I don't know, you'd need to check your analytics platform"
You: "Show me apps to invest in"
AI: "I can't browse that data"
```
**Problem**: AI has amnesia. You're constantly switching contexts manually. No state persistence.

**With Interactive Artifacts:**
```
You: "Build me a task management SaaS"
AI: [Creates AppBuilderArtifact showing live code generation progress at 23%]
     [Chat says: "I'm building TaskMaster SaaS — Next.js + Supabase architecture"]

You: [Clicks "View Revenue" button in artifact]
AI: [Navigates to RevenueDashboard artifact]
     [Chat says: "You're viewing revenue dashboard — TaskMaster made $47 today"]

You: "Show me apps to invest in"
AI: [Navigates to InvestmentMarketplace artifact]
     [Chat says: "Browsing 247 apps. Here are trending SaaS apps..."]

You: [Invests $100 in someone's e-commerce app]
AI: [Updates InvestmentPortfolio artifact — now shows 2 investments]

You: "Back to building my task app"
AI: [Navigates to AppBuilderArtifact — still at 23% progress, exactly where you left it]
     [Chat says: "Back to TaskMaster. Shall we continue with the authentication system?"]
```

**The Magic**:
- **AI knows where you are**: "you're in revenue dashboard" vs "you're building an app"
- **Apps persist**: Your app builder doesn't reset when you check investments
- **Seamless navigation**: Click buttons or just tell AI where to go
- **Multi-tasking**: Build 5 apps, check revenue, browse marketplace — all simultaneously
- **State everywhere**: Revenue updates in real-time, challenge progress saves automatically

---

## ✅ Next Steps

Now that you understand the concepts:
1. **See the architecture** → [`02-ARCHITECTURE.md`](./02-ARCHITECTURE.md) - How components connect
2. **Learn backend integration** → [`03-BACKEND-INTEGRATION.md`](./03-BACKEND-INTEGRATION.md) - Use session_manager
3. **Learn frontend integration** → [`04-FRONTEND-INTEGRATION.md`](./04-FRONTEND-INTEGRATION.md) - Wire React components
