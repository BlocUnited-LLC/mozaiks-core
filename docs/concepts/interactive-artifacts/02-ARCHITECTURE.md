# Architecture & Data Flow

This document explains how all the pieces fit together and how data flows through the **MozaiksAI** platform — enabling users to build multiple apps, track revenue, invest in others' apps, and participate in challenges with AI as an intelligent navigator.

---

## 🏗️ System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         FRONTEND (React)                        │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│  │   Chat UI    │  │   Artifact   │  │   Session    │         │
│  │  (Messages)  │  │  Canvas      │  │  Switcher    │         │
│  └──────┬───────┘  └─────-─┬───────┘  └──────┬───────┘         │
│         │                  │                  │                  │
│         └──────────────────┼──────────────────┘                  │
│                            │                                     │
│                     WebSocket Connection                         │
└────────────────────────────┼────────────────────────────────────┘
                             │
┌────────────────────────────┼────────────────────────────────────┐
│                     BACKEND (FastAPI + AG2)                     │
├────────────────────────────┼────────────────────────────────────┤
│                            │                                     │
│  ┌─────────────────────────▼────────────────────────┐          │
│  │      simple_transport.py                          │          │
│  │  ┌──────────────────────────────────────────┐    │          │
│  │  │  _handle_artifact_action()               │    │          │
│  │  │  ├─ launch_workflow                      │    │          │
│  │  │  ├─ update_state                         │    │          │
│  │  │  └─ custom actions                       │    │          │
│  │  └──────────┬───────────────────────────────┘    │          │
│  └─────────────┼────────────────────────────────────┘          │
│                │                                                 │
│  ┌─────────────▼────────────────┬──────────────────────┐       │
│  │   session_manager.py         │  dependencies.py     │       │
│  │  ├─ create_workflow_session  │  └─ validate_deps    │       │
│  │  ├─ pause_workflow_session   │                      │       │
│  │  ├─ create_artifact_instance │                      │       │
│  │  └─ update_artifact_state    │                      │       │
│  └──────────────┬───────────────┴──────────────────────┘       │
│                 │                                                │
└─────────────────┼────────────────────────────────────────────────┘
                  │
┌─────────────────▼────────────────────────────────────────────────┐
│                     DATABASE (MongoDB)                           │
├──────────────────────────────────────────────────────────────────┤
│  ┌──────────────────┐  ┌──────────────────┐  ┌───────────────┐ │
│  │  WorkflowSessions│  │ ArtifactInstances│  │  Workflow     │ │
│  │                  │  │                  │  │  Dependencies │ │
│  │  chat_id         │  │  artifact_id     │  │               │ │
│  │  workflow_name   │  │  artifact_type   │  │  app   │ │
│  │  status          │  │  state: {...}    │  │  graph        │ │
│  │  artifact_id ────┼──► _id              │  │               │ │
│  └──────────────────┘  └──────────────────┘  └───────────────┘ │
└──────────────────────────────────────────────────────────────────┘
```

---

## 🔄 Complete Flow: User Navigates Between Apps

### Scenario: User clicks "View Revenue" button in AppBuilder artifact (switching contexts)

```
┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐
│ Frontend │     │ WebSocket│     │ Session  │     │ Database │
│   UI     │     │ Handler  │     │ Manager  │     │ (Mongo)  │
└────┬─────┘     └────┬─────┘     └────┬─────┘     └────┬─────┘
     │                 │                │                │
     │ 1. User clicks  │                │                │
     │ "View Revenue"  │                │                │
     │ button in       │                │                │
     │ AppBuilder      │                │                │
     │─────────────────►                │                │
     │ artifact_action │                │                │
     │ {launch_workflow│                │                │
     │  RevenueDashboard}               │                │
     │                 │                │                │
     │                 │ 2. Validate    │                │
     │                 │ dependencies   │                │
     │                 │────────────────►                │
     │                 │                │ 3. Query       │
     │                 │                │ Workflow       │
     │                 │                │ Dependencies   │
     │                 │                │────────────────►
     │                 │                │                │
     │                 │                │ 4. Check if    │
     │                 │                │ app deployed   │
     │                 │                │────────────────►
     │                 │                │◄───────────────│
     │                 │                │ App deployed:✅│
     │                 │◄───────────────│                │
     │                 │ Dependencies OK│                │
     │                 │                │                │
     │                 │ 5. Create      │                │
     │                 │ RevenueDashboard│               │
     │                 │ session        │                │
     │                 │────────────────►                │
     │                 │                │ 6. INSERT      │
     │                 │                │ WorkflowSessions
     │                 │                │ chat_revenue_01│
     │                 │                │ (IN_PROGRESS)  │
     │                 │                │────────────────►
     │                 │                │                │
     │                 │ 7. Create      │                │
     │                 │ RevenueDashboard│               │
     │                 │ artifact       │                │
     │                 │────────────────►                │
     │                 │                │ 8. INSERT      │
     │                 │                │ ArtifactInstances
     │                 │                │ artifact_rev_01│
     │                 │                │ (earnings: $47)│
     │                 │                │────────────────►
     │                 │                │                │
     │                 │ 9. Attach      │                │
     │                 │ artifact to    │                │
     │                 │ session        │                │
     │                 │────────────────►                │
     │                 │                │ 10. UPDATE both│
     │                 │                │ collections    │
     │                 │                │────────────────►
     │                 │                │                │
     │ 11. Send        │                │                │
     │ chat.navigate   │                │                │
     │◄────────────────│                │                │
     │ {chat_revenue_01│                │                │
     │  artifact_rev_01}                │                │
     │                 │                │                │
     │ Note: AppBuilder session stays IN_PROGRESS        │
     │ User can switch back anytime, state preserved     │
     │                 │                │                │
     │ 12. Connect     │                │                │
     │ new WebSocket   │                │                │
     │─────────────────►                │                │
     │                 │ 13. handle_    │                │
     │                 │ websocket()    │                │
     │                 │────────────────►                │
     │                 │                │ 14. Load       │
     │                 │                │ artifact state │
     │                 │                │────────────────►
     │                 │                │◄───────────────│
     │                 │◄───────────────│                │
     │◄────────────────│                │                │
     │ Stream messages │                │                │
     │ + artifact data │                │                │
     │                 │                │                │
```

---

## 📊 Data Model Relationships

```
┌─────────────────────────────────────────────────────────────┐
│  App: "ent-001"                                       │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌─────────────────────────────────────────────────┐        │
│  │  WorkflowDependencies (1 per app)        │        │
│  │  _id: "ent-001"                                  │        │
│  │  workflows: {                                    │        │
│  │    Generator: { dependencies: null, ... },      │        │
│  │    Build: { dependencies: {...}, ... }          │        │
│  │  }                                               │        │
│  └─────────────────────────────────────────────────┘        │
│                                                               │
│  User: "user-456"                                            │
│  ├─────────────────────────────────────┐                    │
│  │                                      │                    │
│  ▼                                      ▼                    │
│  WorkflowSession                        WorkflowSession     │
│  ┌──────────────────────┐              ┌──────────────────┐ │
│  │ _id: "chat_abc123"   │              │ _id: "chat_def456"│ │
│  │ workflow: "Generator"│              │ workflow: "Build" │ │
│  │ status: PAUSED       │              │ status: IN_PROGRESS│ │
│  │ artifact_id: ────────┼──┐           │ artifact_id: ─────┼─┐
│  └──────────────────────┘  │           └──────────────────┘ │
│                             │                                │ │
│  ┌──────────────────────────▼──────┐  ┌────────────────────▼──┐
│  │ ArtifactInstance               │  │ ArtifactInstance      │ │
│  │ _id: "artifact_xyz789"         │  │ _id: "artifact_pqr123"│ │
│  │ type: "ActionPlan"             │  │ type: "BuildDashboard"│ │
│  │ state: {                       │  │ state: {              │ │
│  │   steps: [...],                │  │   buildStatus: "...", │ │
│  │   currentStep: 3               │  │   logs: [...]         │ │
│  │ }                               │  │ }                     │ │
│  │ last_active_chat_id:           │  │ last_active_chat_id:  │ │
│  │   "chat_abc123"                │  │   "chat_def456"       │ │
│  └────────────────────────────────┘  └───────────────────────┘ │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

**Key Points**:
- One `WorkflowDependencies` document per app (contains graph for all workflows)
- Many `WorkflowSessions` per user (one per active/paused workflow chat)
- Many `ArtifactInstances` per user (one per artifact, can outlive sessions)
- Sessions and artifacts are linked bidirectionally via IDs

---

## 🔀 State Machine: Session Status

```
                    ┌──────────────────┐
                    │  Session Created │
                    │ status: IN_PROGRESS
                    └────────┬─────────┘
                             │
                ┌────────────▼────────────┐
                │   IN_PROGRESS           │
                │ ┌─────────────────────┐ │
                │ │ User sends messages │ │
                │ │ AI responds         │ │
                │ │ Artifact updates    │ │
                │ │                     │ │
                │ │ Multiple sessions   │ │
                │ │ can coexist in      │ │
                │ │ IN_PROGRESS state   │ │
                │ └─────────────────────┘ │
                └────────┬───────┬────────┘
                         │       │
         ┌───────────────┘       └───────────────┐
         │                                        │
         │ User switches to another workflow      │ User completes
         │ (this session stays IN_PROGRESS,       │ workflow
         │  persisted in DB, resumable anytime)   │
         │                                        ▼
         │                              ┌────────────────┐
         │                              │   COMPLETED    │
         │                              │                │
         │                              │ ┌────────────┐ │
         │                              │ │ Final      │ │
         │                              │ │ artifact   │ │
         │                              │ │ state      │ │
         │                              │ │ saved      │ │
         │                              │ └────────────┘ │
         │                              └────────────────┘
         │ User returns                            │
         │ (auto-resume from IN_PROGRESS)          │
         │                                         │
         └─────────────┐                           │
                       │                           │
                       ▼                           ▼
              ┌────────────────┐           ┌─────────────┐
              │   IN_PROGRESS  │           │  (Terminal) │
              │ (messages replay)          └─────────────┘
              └────────────────┘
```

---

## 🌊 Event Stream Patterns

### Pattern 1: Launch Workflow (Navigation)

```
Frontend                Backend                 Database
   │                       │                       │
   │  artifact_action      │                       │
   │ ─────────────────────►│                       │
   │                       │  validate_deps        │
   │                       │─────────────────────► │
   │                       │◄───────────────────── │
   │                       │  pause_session        │
   │                       │─────────────────────► │
   │                       │  create_session       │
   │                       │─────────────────────► │
   │                       │  create_artifact      │
   │                       │─────────────────────► │
   │  chat.navigate        │                       │
   │◄─────────────────────│                       │
   │                       │                       │
   │  new WebSocket        │                       │
   │─────────────────────► │                       │
   │                       │  load_session         │
   │                       │─────────────────────► │
   │  artifact.state       │                       │
   │◄─────────────────────│                       │
```

### Pattern 2: Update Artifact State

```
Frontend                Backend                 Database
   │                       │                       │
   │  artifact_action      │                       │
   │ {update_state}        │                       │
   │─────────────────────► │                       │
   │                       │  update_artifact_state│
   │                       │─────────────────────► │
   │  artifact.state.updated                      │
   │◄─────────────────────│                       │
   │                       │                       │
   │                       │  broadcast to all     │
   │                       │  connected clients    │
   │◄─────────────────────┤                       │
   │◄─────────────────────┤                       │
```

### Pattern 3: Multi-User Collaboration

```
User A                  Backend                 User B
  │                       │                       │
  │  update_state         │                       │
  │──────────────────────►│                       │
  │                       │  save to DB           │
  │                       │─────────────────────► │
  │                       │                       │
  │  ack                  │  artifact.state.      │
  │◄──────────────────────│  updated              │
  │                       │──────────────────────►│
  │                       │                       │
```

---

## 🔐 Security & Multi-Tenancy

### Isolation Boundaries

```
┌────────────────────────────────────────────────┐
│  App: "ent-001"                         │
│  ┌──────────────────────────────────────────┐ │
│  │  User: "user-456"                         │ │
│  │  ├─ WorkflowSessions (filtered)           │ │
│  │  └─ ArtifactInstances (filtered)          │ │
│  └──────────────────────────────────────────┘ │
│                                                 │
│  ┌──────────────────────────────────────────┐ │
│  │  User: "user-789"                         │ │
│  │  ├─ WorkflowSessions (isolated)           │ │
│  │  └─ ArtifactInstances (isolated)          │ │
│  └──────────────────────────────────────────┘ │
└────────────────────────────────────────────────┘

┌────────────────────────────────────────────────┐
│  App: "ent-002" (completely isolated)   │
│  ...                                            │
└────────────────────────────────────────────────┘
```

**Enforcement**:
- All queries include `app_id` filter
- WebSocket connections validate `app_id` at accept time
- Dependency validation scoped to `app_id` + `user_id`
- Artifact state updates validate ownership before applying

---

## 🚦 Dependency Validation Flow

### Scenario: User Tries to Access Marketing Automation (requires completed Generator workflow)

**Setup**:
- User is in the middle of Generator workflow (status: `IN_PROGRESS`)
- User browses Investment Marketplace (no dependencies - allowed anytime)
- User clicks "Launch Marketing Automation" button
- Marketing Automation requires Generator workflow to be **COMPLETED**

```
┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐
│ Frontend │     │ WebSocket│     │Dependency│     │ Database │
│   UI     │     │ Handler  │     │ Manager  │     │ (Mongo)  │
└────┬─────┘     └────┬─────┘     └────┬─────┘     └────┬─────┘
     │                 │                │                │
     │ 1. User clicks  │                │                │
     │ "Launch         │                │                │
     │  Marketing      │                │                │
     │  Automation"    │                │                │
     │─────────────────►                │                │
     │ artifact_action │                │                │
     │ {launch_workflow│                │                │
     │  "Marketing"}   │                │                │
     │                 │                │                │
     │                 │ 2. Validate    │                │
     │                 │ dependencies   │                │
     │                 │────────────────►                │
     │                 │                │ 3. Query       │
     │                 │                │ WorkflowDeps   │
     │                 │                │────────────────►
     │                 │                │◄───────────────│
     │                 │                │ Marketing:     │
     │                 │                │ requires       │
     │                 │                │ Generator      │
     │                 │                │ (COMPLETED)    │
     │                 │                │                │
     │                 │                │ 4. Query       │
     │                 │                │ ChatSessions   │
     │                 │                │ for Generator  │
     │                 │                │────────────────►
     │                 │                │◄───────────────│
     │                 │                │ Generator:     │
     │                 │                │ status =       │
     │                 │                │ IN_PROGRESS ❌ │
     │                 │◄───────────────│                │
     │                 │ NOT VALID:     │                │
     │                 │ "Generator     │                │
     │                 │  not complete" │                │
     │                 │                │                │
     │ 5. Send error   │                │                │
     │◄────────────────│                │                │
     │ chat.dependency_│                │                │
     │ blocked         │                │                │
     │ {               │                │                │
     │   workflow:     │                │                │
     │   "Marketing",  │                │                │
     │   message:      │                │                │
     │   "Please       │                │                │
     │    complete     │                │                │
     │    Generator    │                │                │
     │    first"       │                │                │
     │ }               │                │                │
     │                 │                │                │
     │ 6. Frontend     │                │                │
     │ shows warning   │                │                │
     │ modal/toast     │                │                │
     │                 │                │                │
```

**Key Points**:
1. **Investment Marketplace has NO dependencies** - user can browse/invest anytime, even mid-Generator
2. **Marketing Automation requires Generator COMPLETED** - blocked if Generator is IN_PROGRESS
3. **Backend validates BEFORE creating session** - no partial state created on failure
4. **Frontend receives `chat.dependency_blocked` event** with clear error message
5. **User must complete Generator workflow** - then Marketing button will work

**User Experience**:
```
User: [In Generator workflow, building task management app]
User: [Clicks "View Investments"]
✅ ALLOWED - Investment Marketplace opens (no dependencies)

User: [Browsing apps to invest in]
User: "Should I invest in this e-commerce app?"
✅ ALLOWED - Chat answers questions about investments

User: [Clicks "Launch Marketing Automation"]
❌ BLOCKED - Toast appears:
   "Please complete the Generator workflow first. 
    Marketing automation requires your app to be fully generated."

User: [Returns to Generator workflow, completes it]
Generator: status changed to COMPLETED

User: [Clicks "Launch Marketing Automation"]
✅ ALLOWED - Marketing Automation opens with new session
```

---

## ⚡ Performance Considerations

### Optimization 1: Lazy Loading
- Artifacts load state only when needed (not on every WebSocket message)
- Frontend caches artifact state locally, syncs on `artifact.state.updated` events

### Optimization 2: Connection Pooling
- Session manager reuses MongoDB client across operations
- WebSocket handler maintains connection pool per app

### Optimization 3: Selective Broadcasting
- `artifact.state.updated` only sent to clients viewing that artifact
- Uses chat_id correlation to filter recipients

### Optimization 4: Incremental State Updates
```python
# Bad: Replace entire state
await update_artifact_state(artifact_id, app_id, {
    "steps": [...],  # 1000 items
    "currentStep": 3
})

# Good: Update only what changed
await update_artifact_state(artifact_id, app_id, {
    "currentStep": 3
})
```

---

## 🧩 Extension Points

### Adding New Artifact Types

1. **Backend**: Register type in artifact type enum (optional)
2. **Frontend**: Create React component for new type
3. **Session Manager**: No changes needed (generic state storage)

### Adding New Artifact Actions

1. **Frontend**: Send custom action via `sendArtifactAction()`
2. **Backend**: Handle in `_handle_artifact_action()`:
   ```python
   if action == "my_custom_action":
       payload_data = payload.get("data")
       # Custom logic here
       await my_custom_handler(payload_data)
   ```

### Adding Workflow-Specific Logic

1. Create handler in your workflow's tools directory
2. Import and call from `_handle_artifact_action()`
3. Use session_manager functions for state persistence

---

## 📈 Scalability

### Current Design Limits
- **Sessions per user**: Unlimited (but typically 2-5 active)
- **Artifacts per user**: Unlimited (typically 10-50)
- **Concurrent WebSocket connections**: Limited by FastAPI/Uvicorn config
- **Artifact state size**: 16MB (MongoDB document limit)

### Scaling Strategies
- **Horizontal scaling**: Multiple FastAPI instances behind load balancer
- **Database sharding**: Shard by `app_id`
- **Caching**: Redis cache for hot artifact states
- **CDN**: Serve static artifact UI components via CDN

---

## ✅ Next Steps

Now that you understand the architecture:
1. **Backend integration** → [`03-BACKEND-INTEGRATION.md`](./03-BACKEND-INTEGRATION.md) - Use session_manager in your code
2. **Frontend integration** → [`04-FRONTEND-INTEGRATION.md`](./04-FRONTEND-INTEGRATION.md) - Wire React components
3. **See examples** → [`05-EXAMPLES.md`](./05-EXAMPLES.md) - Real-world patterns
