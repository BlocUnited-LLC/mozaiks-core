# Standalone Integration: Existing Apps + MozaiksAI (Source of Truth)

This document explains how **existing applications** integrate MozaiksAI Runtime + ChatUI to add AI-powered workflows. This is the **AI Plugin only** scenario.

---

## TL;DR — What This Document Covers

| Aspect | Standalone Integration |
|--------|------------------------|
| **Who uses it** | Existing apps with their own backend/frontend |
| **What they get** | MozaiksAI Runtime + ChatUI + AI Plugins only |
| **What agents produce** | Workflows, tools, artifacts — AND integration code for the host app |
| **Key challenge** | Agents must wire WebSocket URLs into the host app's existing UI components |

---

## The Standalone Integration Model

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    EXISTING APPLICATION (Any Framework)                     │
│                                                                             │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │              Host App Frontend (React, Vue, Angular, etc.)            │  │
│  │                                                                       │  │
│  │  ┌─────────────────────────────────────────────────────────────────┐  │  │
│  │  │  Existing Pages & Components (owned by host app)                │  │  │
│  │  │  • Navigation, routing, dashboards, settings                    │  │  │
│  │  │  • Business logic UI, forms, lists                              │  │  │
│  │  │                                                                 │  │  │
│  │  │  ┌─────────────────────────────────────────────────────────┐    │  │  │
│  │  │  │  AI-Enhanced Components (agent-generated integration)   │    │  │  │
│  │  │  │  • WebSocket listeners wired to existing components     │    │  │  │
│  │  │  │  • Event handlers for artifact updates                  │    │  │  │
│  │  │  │  • State bindings to workflow events                    │    │  │  │
│  │  │  └─────────────────────────────────────────────────────────┘    │  │  │
│  │  └─────────────────────────────────────────────────────────────────┘  │  │
│  │                                                                       │  │
│  │  ┌─────────────────────────────────────────────────────────────────┐  │  │
│  │  │  ChatUI (npm package — embedded)                                │  │  │
│  │  │  • Widget / Overlay / Artifact panels                           │  │  │
│  │  │  • WebSocket connection to Runtime                              │  │  │
│  │  └─────────────────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                               │                             │
└───────────────────────────────────────────────┼─────────────────────────────┘
                                                │ WebSocket + HTTP
                                                ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         BACKEND INFRASTRUCTURE                              │
│                                                                             │
│  ┌───────────────────────────────┐   ┌───────────────────────────────────┐  │
│  │   Host App Backend            │   │   MozaiksAI Runtime (Python)      │  │
│  │   (.NET, Node, Python, etc.)  │   │                                   │  │
│  │                               │   │  • Workflow execution (AG2)       │  │
│  │  • Auth (owned by host)       │   │  • WebSocket transport            │  │
│  │  • Billing (owned by host)    │   │  • Chat persistence (Mongo)       │  │
│  │  • Business Logic             │   │  • Token accounting               │  │
│  │  • Session Broker endpoint    │◄──┤  • Artifact events                │  │
│  └───────────────────────────────┘   └───────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Key Difference: Agents Modify the Host App's Codebase

In standalone integration, there's **no plugin system**. The agents must generate code that integrates directly into the host application's existing frontend codebase.

### What AgentGenerator Produces (Standalone)

| Output | Location | Purpose |
|--------|----------|---------|
| Workflow JSON | `workflows/{name}/orchestrator.json` | Workflow configuration (runtime side) |
| Agent definitions | `workflows/{name}/agents.json` | Agent roles and prompts |
| Tool stubs | `workflows/{name}/tools/` | Python callables for agent actions |
| Artifact components | `ChatUI/src/workflows/{name}/components/` | React components for ChatUI artifacts |
| **Host App Integration** | **Host app's codebase** | WebSocket wiring, event handlers, state bindings |

### The Integration Code Challenge

Unlike the Full Platform pattern (where Function Plugins are self-contained), standalone integration requires the agents to:

1. **Understand the host app's framework** (React, Vue, Angular, Svelte, etc.)
2. **Identify existing UI components** that should react to workflow events
3. **Generate integration code** that wires WebSocket URLs and event handlers into those components
4. **Respect the host app's patterns** (state management, styling, conventions)

---

## How WebSocket Integration Works

### MozaiksAI Runtime Events

The runtime emits events over WebSocket that the host app can subscribe to:

```
WebSocket URL: wss://{runtime}/ws/{workflow}/{app_id}/{chat_id}/{user_id}

Events emitted:
├── workflow:started      → Workflow has begun
├── agent:message         → Agent sent a message
├── tool:called           → Tool was invoked
├── artifact:created      → New artifact available
├── artifact:updated      → Artifact data changed
├── artifact:action       → User action on artifact
└── workflow:completed    → Workflow finished
```

### Host App Integration Patterns

**Pattern A: ChatUI handles everything (simplest)**
```
Host App → embeds ChatUI → ChatUI manages WebSocket → artifacts render in overlay
```
The host app just includes ChatUI; all AI interaction happens in the chat overlay.

**Pattern B: Host app components subscribe to events (advanced)**
```
Host App → embeds ChatUI → ChatUI manages WebSocket
                        ↓
         Host App components listen to artifact events
                        ↓
         Existing pages/components update based on workflow state
```
The host app's existing UI components subscribe to workflow events and update in place.

---

## Agent-Generated Integration Code Examples

### Example: React Host App

The agent generates integration code for a React app that wants to show campaign status in an existing dashboard:

**Generated: `src/integrations/mozaiks/useCampaignWorkflow.ts`**
```typescript
import { useMozaiksEvents } from '@mozaiks/chatui';

export function useCampaignWorkflow(chatId: string) {
  const [campaignData, setCampaignData] = useState(null);
  const [status, setStatus] = useState<'idle' | 'running' | 'complete'>('idle');

  useMozaiksEvents(chatId, {
    'artifact:updated': (event) => {
      if (event.artifactType === 'campaign-builder') {
        setCampaignData(event.data);
      }
    },
    'workflow:started': () => setStatus('running'),
    'workflow:completed': () => setStatus('complete'),
  });

  return { campaignData, status };
}
```

**Generated: Modification to existing `CampaignDashboard.tsx`**
```typescript
// Agent adds this import
import { useCampaignWorkflow } from '../integrations/mozaiks/useCampaignWorkflow';

// Agent modifies the component to include workflow state
function CampaignDashboard({ chatId }) {
  const { campaignData, status } = useCampaignWorkflow(chatId);
  
  // Existing dashboard code now has access to real-time workflow data
  return (
    <div>
      {status === 'running' && <WorkflowProgressBar />}
      <ExistingCampaignCards campaigns={campaignData?.campaigns ?? existingCampaigns} />
    </div>
  );
}
```

### Example: Vue Host App

**Generated: `src/composables/useMozaiksWorkflow.ts`**
```typescript
import { ref, onMounted, onUnmounted } from 'vue';
import { mozaiksEventBus } from '@mozaiks/chatui/vue';

export function useMozaiksWorkflow(chatId: string) {
  const artifactData = ref(null);
  const isRunning = ref(false);

  onMounted(() => {
    mozaiksEventBus.on(`artifact:updated:${chatId}`, (data) => {
      artifactData.value = data;
    });
  });

  return { artifactData, isRunning };
}
```

---

## UI Ownership in Standalone Integration

### What the Host App Owns

| Responsibility | Notes |
|----------------|-------|
| Navigation & routing | Sidebar, tabs, page structure |
| Product pages | Lists, dashboards, settings, admin |
| Auth & identity | SSO, JWT issuance, session management |
| Billing & subscriptions | Entitlements, usage tracking |
| **Integration points** | Where AI workflows are launched, where results appear |

### What MozaiksAI Owns

| Responsibility | Notes |
|----------------|-------|
| Chat widget & overlay | ChatUI npm package |
| Artifact rendering | Inside ChatUI overlay/panels |
| Workflow execution | Runtime service |
| WebSocket transport | Event streaming to host app |

### What Agents Generate (The Bridge)

| Output | Purpose |
|--------|---------|
| Workflow definitions | What the AI does |
| Artifact components | UI inside ChatUI |
| **Integration hooks** | Connect host app components to workflow events |
| **Component modifications** | Wire existing UI to receive artifact updates |

---

## Standalone Integration Example: Mozaiks Platform

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                       MOZAIKS PLATFORM (Standalone)                         │
│                                                                             │
│  Frontend: MOZ-UI (React)                                                   │
│  ├── Dashboard page (MOZ-UI owns)                                           │
│  ├── Marketplace page (MOZ-UI owns)                                         │
│  ├── App Management page (MOZ-UI owns)                                      │
│  │                                                                          │
│  ├── AI-Enhanced Components (agent-generated):                              │
│  │   └── Dashboard cards that update when workflows complete                │
│  │   └── Notification badges for workflow status                            │
│  │   └── Quick-action buttons that launch specific workflows                │
│  │                                                                          │
│  └── ChatUI (npm package)                                                   │
│      └── Widget, Overlay, Artifacts                                         │
│                                                                             │
│  Backend: MOZ-Microservices (.NET)                                          │
│  ├── Auth, Billing, User Management (owned by .NET)                         │
│  └── Session Broker endpoint → MozaiksAI Runtime                            │
│                                                                             │
│  AI Layer: MozaiksAI Runtime (Python)                                       │
│  └── AgentGenerator, AppAnalyzer, and other workflows                       │
│                                                                             │
│  NOTE: NO MOZAIKSCORE — MOZ-UI/MOZ-Microservices IS the host app            │
└─────────────────────────────────────────────────────────────────────────────┘
```

## What Agents Need to Know About the Host App

For standalone integration, agents generating AI Plugins need context about:

| Context | Why |
|---------|-----|
| **Framework** | React, Vue, Angular, Svelte — affects hook/composable patterns |
| **State management** | Redux, Zustand, Pinia, etc. — affects where to store workflow state |
| **Styling approach** | Tailwind, CSS modules, styled-components — affects generated components |
| **Existing components** | What already exists that should react to workflows |
| **Integration points** | Where users launch workflows, where results should appear |

This context allows agents to generate integration code that **respects the host app's conventions** rather than fighting them.

---

## AppIntegrator Workflow: Action Plan

> **Status:** Coming Soon (Full Platform first, Standalone second)
> **First Customer:** Mozaiks Platform itself (MOZ-UI + MOZ-Microservices)

### Core Philosophy: Non-Invasive Integration

The agents should NOT try to rewrite business logic. They should:
- **Wire in** the ChatUI + WebSocket connection
- **Find or create** minimal UI entry points (button, panel, etc.)
- **Touch only** files that MUST change for the integration to work
- **Respect** existing patterns — don't fight the host app's conventions

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  PRINCIPLE: The user can always manually place WebSocket URLs anywhere.    │
│  Our job is to make that easier, not to rewrite their app.                 │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

### Phase 1: Codebase Scan (Read-Only)

**Goal:** Understand the host app's structure without modifying anything.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  SCAN PHASE                                                                 │
│                                                                             │
│  ┌─────────────────────────────┐    ┌─────────────────────────────┐        │
│  │  Frontend Scanner           │    │  Backend Scanner            │        │
│  │                             │    │                             │        │
│  │  Detects:                   │    │  Detects:                   │        │
│  │  • Framework (React/Vue/etc)│    │  • Framework (.NET/Node/etc)│        │
│  │  • Entry point (App.tsx)    │    │  • Auth pattern (JWT/etc)   │        │
│  │  • State management         │    │  • API structure            │        │
│  │  • Existing components      │    │  • Existing endpoints       │        │
│  │  • Styling approach         │    │                             │        │
│  └─────────────────────────────┘    └─────────────────────────────┘        │
│                                                                             │
│  Output: HostAppContext JSON (passed to all subsequent agents)             │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Tool: `scan_host_app`**
```python
def scan_host_app(frontend_path: str, backend_path: str) -> HostAppContext:
    """
    Returns a context object with:
    - frontend_framework: "react" | "vue" | "angular" | "svelte" | "unknown"
    - frontend_entry: path to main App component
    - state_management: "redux" | "zustand" | "pinia" | "context" | "none"
    - styling: "tailwind" | "css-modules" | "styled-components" | "scss"
    - backend_framework: "aspnet" | "express" | "fastapi" | "django" | "unknown"
    - auth_pattern: "jwt" | "session" | "oauth" | "unknown"
    - existing_api_routes: list of route patterns found
    """
```

---

### Phase 2: Integration Planning (Minimal File Selection)

**Goal:** Identify the MINIMUM set of files that need to change.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  PLANNING PHASE                                                             │
│                                                                             │
│  Input: HostAppContext + User's workflow description                        │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │  Integration Planner Agent                                           │   │
│  │                                                                      │   │
│  │  Decides:                                                            │   │
│  │  1. Frontend entry point — WHERE to wire ChatUI                      │   │
│  │     → Usually: App.tsx or layout component (wrap with Provider)      │   │
│  │                                                                      │   │
│  │  2. UI trigger point — WHERE user launches workflow                  │   │
│  │     → Option A: Add button to existing page                          │   │
│  │     → Option B: Create new minimal page/component                    │   │
│  │                                                                      │   │
│  │  3. Result display point — WHERE workflow results appear             │   │
│  │     → Option A: Just in ChatUI overlay (simplest)                    │   │
│  │     → Option B: Also update existing component (more complex)        │   │
│  │                                                                      │   │
│  │  4. Backend touchpoints — WHAT backend changes (if any)              │   │
│  │     → Session Broker endpoint (required)                             │   │
│  │     → Data API endpoints (only if workflow reads/writes host data)   │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
│  Output: IntegrationPlan with explicit file list                           │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Key Decision Tree:**

```
Does the workflow need to READ existing app data?
├── NO → No data API needed, just Session Broker
└── YES → Add read endpoint OR use existing API (if available)

Does the workflow need to WRITE to existing app data?
├── NO → Results stay in ChatUI artifacts
└── YES → Add write endpoint OR use existing API (if available)

Should workflow results appear OUTSIDE ChatUI?
├── NO → Just use ChatUI overlay (simplest integration)
└── YES → Identify which component + add WebSocket listener
```

---

### Phase 3: Code Generation (Minimal Changes)

**Goal:** Generate ONLY what's needed, respecting host app patterns.

#### Frontend Changes (Always Required)

**1. ChatUI Provider Setup**
```
File: {frontend_entry} (e.g., App.tsx)
Change: Wrap with <ChatUIProvider>
Invasiveness: LOW — just adds a wrapper
```

```tsx
// BEFORE
function App() {
  return <Router>...</Router>;
}

// AFTER (agent-generated change)
import { ChatUIProvider } from '@mozaiks/chatui';

function App() {
  return (
    <ChatUIProvider config={{
      runtime: { url: process.env.MOZAIKS_RUNTIME_URL },
      app: { id: 'host-app-id' }
    }}>
      <Router>...</Router>
    </ChatUIProvider>
  );
}
```

**2. Workflow Launch Point**
```
File: Existing page OR new component
Change: Add button/trigger that opens ChatUI with workflow
Invasiveness: LOW — adds a button, doesn't change logic
```

```tsx
// Option A: Add to existing page (if user specifies where)
import { useChatUI } from '@mozaiks/chatui';

function ExistingPage() {
  const { openWorkflow } = useChatUI();
  
  return (
    <div>
      {/* Existing content unchanged */}
      <button onClick={() => openWorkflow('campaign-builder')}>
        Build with AI
      </button>
    </div>
  );
}

// Option B: Create new minimal component
export function AILauncher() {
  const { openWorkflow } = useChatUI();
  return <button onClick={() => openWorkflow('campaign-builder')}>Build with AI</button>;
}
```

**3. Result Listener (ONLY if user wants results outside ChatUI)**
```
File: Target component
Change: Add hook to listen for workflow events
Invasiveness: MEDIUM — adds state, but doesn't change existing logic
```

```tsx
// Only generated if user explicitly wants results in existing component
import { useMozaiksEvents } from '@mozaiks/chatui';

function ExistingDashboard() {
  const [aiResults, setAiResults] = useState(null);
  
  useMozaiksEvents({
    'workflow:completed': (event) => {
      if (event.workflow === 'campaign-builder') {
        setAiResults(event.result);
        // Optionally trigger refresh of existing data
      }
    }
  });
  
  return (
    <div>
      {/* Existing dashboard — UNCHANGED */}
      {aiResults && <AIResultsBanner result={aiResults} />}
    </div>
  );
}
```

#### Backend Changes (Minimal)

**1. Session Broker Endpoint (Required)**
```
File: New controller/route file
Change: ADD new file (doesn't modify existing)
Invasiveness: LOW — additive only
```

```csharp
// NEW FILE: Controllers/MozaiksController.cs
[ApiController]
[Route("api/mozaiks")]
public class MozaiksController : ControllerBase
{
    [HttpPost("sessions")]
    [Authorize] // Uses existing auth
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        // Call MozaiksAI Runtime
        var session = await _mozaiksClient.CreateSession(new {
            app_id = "host-app",
            user_id = userId,
            workflow_name = request.WorkflowName
        });
        
        return Ok(new {
            chat_id = session.ChatId,
            ws_url = $"{_config.RuntimeWsUrl}/ws/{request.WorkflowName}/host-app/{session.ChatId}/{userId}"
        });
    }
}
```

**2. Data Bridge Endpoints (ONLY if workflow needs host data)**
```
File: New controller OR existing controller
Change: Add endpoints that workflow tools can call
Invasiveness: MEDIUM — but still additive
```

```csharp
// Only if workflow needs to read/write campaigns
[HttpGet("bridge/campaigns")]
public async Task<IActionResult> GetCampaigns() 
{
    // Expose existing data to AI workflow
    return Ok(await _campaignService.GetAll());
}

[HttpPost("bridge/campaigns")]
public async Task<IActionResult> CreateCampaign([FromBody] Campaign campaign)
{
    // Allow AI workflow to create campaigns
    return Ok(await _campaignService.Create(campaign));
}
```

---

### Phase 4: AI Plugin Generation (Standard)

**Goal:** Create the workflow that runs on MozaiksAI Runtime.

This part is the same regardless of Standalone vs Full Platform:

```
workflows/{workflow-name}/
├── orchestrator.json      # Workflow definition
├── agents.json            # Agent roles and prompts
├── tools/                 # Python tool implementations
│   ├── __init__.py
│   └── campaign_tools.py  # If workflow needs to call host APIs
└── handoffs.json          # Agent transitions
```

**Tool that calls host app's bridge API:**
```python
# workflows/campaign-builder/tools/campaign_tools.py

async def get_existing_campaigns(context: ToolContext) -> List[Campaign]:
    """Fetch campaigns from the host app's API."""
    response = await context.http_client.get(
        f"{context.host_app_url}/api/mozaiks/bridge/campaigns",
        headers={"Authorization": f"Bearer {context.user_token}"}
    )
    return response.json()

async def save_campaign(context: ToolContext, campaign: Campaign) -> Campaign:
    """Save a new campaign to the host app."""
    response = await context.http_client.post(
        f"{context.host_app_url}/api/mozaiks/bridge/campaigns",
        json=campaign.dict(),
        headers={"Authorization": f"Bearer {context.user_token}"}
    )
    return response.json()
```

---

### Agent Prompt Guidelines

**For all code-generating agents in this workflow:**

```
INTEGRATION PHILOSOPHY:
- Be NON-INVASIVE. Do not rewrite existing business logic.
- Touch ONLY files that must change for the integration to work.
- PREFER adding new files over modifying existing files.
- When modifying existing files, make MINIMAL changes.
- RESPECT the host app's patterns (naming, styling, structure).
- If unsure, choose the SIMPLER option.

DO:
✓ Add ChatUIProvider wrapper to entry point
✓ Add new button/trigger components
✓ Add new API endpoints in separate files
✓ Add event listeners that DON'T change existing logic

DO NOT:
✗ Refactor existing components
✗ Change existing API endpoints
✗ Modify existing business logic
✗ "Improve" code style or structure
✗ Add features beyond what's needed for integration
```
---

**Key insight:** Standalone integration gives more flexibility but requires agents to understand and modify the host app's existing codebase. Full Platform integration is simpler because everything follows the MozaiksCore plugin contract.
