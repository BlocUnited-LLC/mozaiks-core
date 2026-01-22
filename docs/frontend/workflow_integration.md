# Workflow Integration Guide

## Purpose

This document explains how workflows integrate with the ChatUI frontend, covering component registration, dynamic loading, backend API coordination, workflow metadata synchronization, and cache management. Understanding workflow integration is essential for adding new workflows, debugging component loading issues, and maintaining the frontend-backend contract.

## Overview

**Workflow Integration** is the **bidirectional coordination** between backend workflow definitions and frontend UI components. It ensures that:
- Frontend knows which workflows exist
- Components load dynamically for any workflow
- Backend metadata drives frontend behavior
- Cache synchronization prevents stale UI

**Key Integration Points:**
1. **Workflow Registry API** (`/api/workflows`) - Backend exposes workflow metadata
2. **WorkflowRegistry (Frontend)** - Fetches and caches workflow configurations
3. **WorkflowUIRouter** - Dynamically loads workflow-specific components
4. **ChatUIContext** - Initializes workflows on app startup
5. **Cache Synchronization** - `cache_seed` ensures UI matches backend version

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                  Workflow Integration Architecture                   │
└─────────────────────────────────────────────────────────────────────┘

BACKEND (Python/FastAPI)                    FRONTEND (React)
┌────────────────────────────┐              ┌────────────────────────────┐
│ workflows/{Workflow}/      │              │ ChatUI/src/workflows/      │
│   agents.json              │              │   index.js                 │
│   tools.json               │              │   {Workflow}/components/   │
│   structured_outputs.json  │              │     index.js               │
│   orchestrator.json        │              │     Component1.js          │
│   context_variables.json   │──────────┐   │     Component2.js          │
│   tools/*.py               │          │   └────────────────────────────┘
└────────────────────────────┘          │
                                        │
        ↓                               │           ↓
┌────────────────────────────┐          │   ┌────────────────────────────┐
│ core/workflow/             │          │   │ WorkflowRegistry           │
│   workflow_manager.py      │          │   │   .initializeWorkflows()   │
│   - load_workflow()        │          │   │   .getWorkflow(name)       │
│   - get_config()           │          │   │   .loadedWorkflows Map     │
│   - get_all_workflow_names│          │   └────────────────────────────┘
└────────────────────────────┘          │
                                        │           ↓
        ↓                               │   ┌────────────────────────────┐
┌────────────────────────────┐          │   │ WorkflowUIRouter           │
│ FastAPI Endpoints          │          │   │   - Dynamic import()       │
│ /api/workflows             │◄─────────┤   │   - Component caching      │
│ /api/workflows/config      │  HTTP GET│   │   - Error fallbacks        │
│ /api/workflows/{name}/...  │          │   └────────────────────────────┘
└────────────────────────────┘          │
                                        │           ↓
        ↓                               │   ┌────────────────────────────┐
┌────────────────────────────┐          │   │ ChatPage                   │
│ Response JSON              │──────────┘   │   - Renders components     │
│ {                          │              │   - Passes props           │
│   "Generator": {           │              │   - Handles responses      │
│     "name": "...",         │              └────────────────────────────┘
│     "agents": {...},       │
│     "tools": {...},        │
│     "structured_outputs": {...}
│   },                       │
│   "OtherWorkflow": {...}   │
│ }                          │
└────────────────────────────┘

COORDINATION FLOW:
1. Backend: workflow_manager loads workflow manifests
2. Backend: /api/workflows exposes aggregated configs
3. Frontend: WorkflowRegistry fetches on app init
4. Frontend: Stores in Map + localStorage cache
5. Frontend: WorkflowUIRouter imports components dynamically
6. Frontend: Renders with props from backend payload
```

## Backend: Workflow API

### Endpoint: GET /api/workflows

**Purpose:** Expose all workflow configurations to frontend.

**Implementation (shared_app.py):**

```python
@app.get("/api/workflows")
async def get_workflows():
    """Get all workflows for frontend"""
    try:
        from core.workflow.workflow_manager import workflow_manager
        
        configs = {}
        for workflow_name in workflow_manager.get_all_workflow_names():
            configs[workflow_name] = workflow_manager.get_config(workflow_name)
        
        get_workflow_logger("shared_app").info(
            "WORKFLOWS_REQUESTED: Workflows requested by frontend",
            workflow_count=len(configs)
        )
        
        return configs
        
    except Exception as e:
        logger.error(f"Failed to get workflows: {e}")
        raise HTTPException(status_code=500, detail="Failed to retrieve workflows")
```

**Response Structure:**

```json
{
  "Generator": {
    "name": "Generator",
    "agents": {
      "InterviewAgent": { "system_message": "...", "auto_tool_mode": false },
      "ContextAgent": { "system_message": "...", "auto_tool_mode": true }
    },
    "tools": [
      {
        "agent": "ContextAgent",
        "file": "action_plan.py",
        "function": "action_plan",
        "tool_type": "UI_Tool",
        "ui": { "component": "ActionPlan", "mode": "artifact" }
      }
    ],
    "structured_outputs": {
      "models": {
        "ActionPlanCall": { "type": "model", "fields": {...} }
      },
      "registry": {
        "ContextAgent": "ActionPlanCall"
      }
    },
    "orchestrator": {
      "max_turns": 30,
      "human_in_the_loop": false,
      "startup_mode": "immediate",
      "orchestration_pattern": "handoff_chain"
    },
    "context_variables": {
      "base_variables": [...],
      "derived_variables": [...]
    },
    "initial_message": "What would you like to automate?",
    "ui_tools": {
      "action_plan": { "component": "ActionPlan", "mode": "artifact" }
    }
  },
  "MarketingAutomation": {
    "name": "MarketingAutomation",
    "agents": {...},
    "tools": [...],
    ...
  }
}
```

**Key Fields:**
- `name`: Workflow identifier (matches directory name)
- `agents`: Agent configurations from `agents.json`
- `tools`: Tool registry from `tools.json`
- `structured_outputs`: Schema definitions from `structured_outputs.json`
- `orchestrator`: Runtime config from `orchestrator.json`
- `context_variables`: State variables from `context_variables.json`
- `initial_message`: First agent message to display
- `ui_tools`: UI component mappings

### workflow_manager.py

**Purpose:** Load and manage workflow configurations.

**Key Methods:**

```python
class WorkflowManager:
    def load_workflow(self, workflow_name: str) -> None:
        """Load workflow from disk"""
        workflow_dir = Path("workflows") / workflow_name
        
        # Load all manifest files
        agents = self._load_json(workflow_dir / "agents.json")
        tools = self._load_json(workflow_dir / "tools.json")
        structured_outputs = self._load_json(workflow_dir / "structured_outputs.json")
        orchestrator = self._load_json(workflow_dir / "orchestrator.json")
        context_variables = self._load_json(workflow_dir / "context_variables.json")
        
        # Store in registry
        self._workflows[workflow_name] = {
            "name": workflow_name,
            "agents": agents.get("agents", {}),
            "tools": tools.get("tools", []),
            "structured_outputs": structured_outputs,
            "orchestrator": orchestrator,
            "context_variables": context_variables,
            # ... additional metadata
        }

    def get_config(self, workflow_name: str) -> dict:
        """Get workflow configuration"""
        if workflow_name not in self._workflows:
            self.load_workflow(workflow_name)
        return self._workflows[workflow_name]

    def get_all_workflow_names(self) -> list[str]:
        """Get list of all available workflows"""
        workflows_dir = Path("workflows")
        return [
            d.name for d in workflows_dir.iterdir()
            if d.is_dir() and not d.name.startswith("_")
        ]
```

**Discovery Pattern:**

```python
# Auto-discover workflows by scanning directories
workflows_dir = Path("workflows")
for workflow_dir in workflows_dir.iterdir():
    if workflow_dir.is_dir() and not workflow_dir.name.startswith("_"):
        workflow_manager.load_workflow(workflow_dir.name)
```

## Frontend: WorkflowRegistry

### Purpose

`WorkflowRegistry` is a **singleton service** that:
- Fetches workflow metadata from `/api/workflows`
- Caches workflow configs in memory and localStorage
- Provides lookup methods for components
- Handles offline fallback via cached data

### Class Structure

```javascript
class WorkflowRegistry {
  constructor() {
    this.loadedWorkflows = new Map();      // In-memory workflow storage
    this.initialized = false;              // Initialization flag
    this.apiBaseUrl = 'http://localhost:8000/api';
    this.ready = false;                    // At least one workflow loaded
    this.lastError = null;                 // Last fetch error
    this.maxRetries = 5;                   // Retry attempts
    this.retryDelays = [250, 750, 2000, 4000, 8000]; // Backoff delays (ms)
    this.cacheKey = 'mozaiks_workflows_cache_v1';
  }

  // ... methods ...
}

const workflowRegistry = new WorkflowRegistry();
export default workflowRegistry;
```

### Initialization

**Entry Point: `initializeWorkflows()`**

```javascript
async initializeWorkflows({ allowCacheFallback = true } = {}) {
  if (this.initialized) {
    console.log('⏭️ WorkflowRegistry: Already initialized');
    return this.getWorkflowSummary();
  }

  console.log('🚀 WorkflowRegistry: Fetching workflows from backend API...');

  try {
    // Fetch from backend with retries
    const workflowConfigs = await this._fetchWithRetries();

    // Process each workflow
    for (const [workflowName, config] of Object.entries(workflowConfigs)) {
      const workflowInfo = {
        name: workflowName,
        displayName: config.name || workflowName,
        description: `${config.name || workflowName} workflow`,
        version: '1.0.0',
        
        // Extract structured outputs mapping
        structuredOutputs: this._extractStructuredOutputs(config),
        
        // Store metadata
        metadata: {
          maxTurns: config.max_turns,
          humanInTheLoop: config.human_in_the_loop,
          startupMode: config.startup_mode,
          orchestrationPattern: config.orchestration_pattern,
          chatPaneAgents: config.chat_pane_agents || [],
          artifactAgents: config.artifact_agents || [],
          initialMessage: config.initial_message,
          uiTools: config.ui_tools || {}
        },
        
        visualAgents: config.visual_agents || {},
        tools: config.tools || {},
        loadedAt: new Date().toISOString()
      };

      this.loadedWorkflows.set(workflowName, workflowInfo);
      console.log(`✅ Loaded workflow from API: ${workflowName}`);
    }

    this.initialized = true;
    this.ready = this.loadedWorkflows.size > 0;
    
    // Save to cache
    if (this.ready) this._saveToCache();
    
    console.log(`✅ WorkflowRegistry: Loaded ${this.loadedWorkflows.size} workflows from backend`);
    return this.getWorkflowSummary();

  } catch (error) {
    console.error('❌ WorkflowRegistry: Failed to fetch workflows from API:', error);
    
    // Try cache fallback
    if (allowCacheFallback && this._loadFromCache()) {
      console.warn('⚠️ Using cached workflows; backend unavailable');
      return this.getWorkflowSummary();
    }
    
    // Total failure
    this.initialized = false;
    this.ready = false;
    throw error;
  }
}
```

**Structured Outputs Extraction:**

```javascript
_extractStructuredOutputs(config) {
  const map = {};
  
  try {
    const so = config.structured_outputs || {};
    const registry = so.registry || so.models || so || {};
    
    if (Array.isArray(registry)) {
      // Array format: ["AgentName1", "AgentName2"]
      registry.forEach(a => {
        if (typeof a === 'string') {
          map[a] = true;
        } else if (a && a.name) {
          map[a.name] = true;
        }
      });
    } else if (typeof registry === 'object') {
      // Object format: { "AgentName": "ModelName" }
      Object.keys(registry).forEach(k => {
        map[k] = true;
      });
    }
  } catch (e) {
    console.warn('Failed to extract structured outputs:', e);
  }
  
  return map;
}
```

### Retry Logic

**Exponential Backoff:**

```javascript
async _fetchWithRetries() {
  let attempt = 0;
  let controller;

  while (attempt < this.maxRetries) {
    controller = new AbortController();
    const signal = controller.signal;

    try {
      if (attempt > 0) {
        console.log(`🔁 WorkflowRegistry: Retry attempt ${attempt + 1}/${this.maxRetries}`);
      }

      const response = await fetch(`${this.apiBaseUrl}/workflows`, { signal });
      
      if (!response.ok) {
        throw new Error(`API request failed: ${response.status} ${response.statusText}`);
      }

      return await response.json();

    } catch (err) {
      this.lastError = err;
      
      // Don't retry on last attempt
      if (attempt === this.maxRetries - 1) break;
      
      // Wait with exponential backoff
      const delay = this.retryDelays[Math.min(attempt, this.retryDelays.length - 1)];
      await new Promise(res => setTimeout(res, delay));
      
      attempt += 1;
    }
  }

  throw this.lastError || new Error('Unknown workflow fetch failure');
}
```

**Retry Delays:** 250ms → 750ms → 2s → 4s → 8s (total ~15s max)

### Cache Management

**Save to localStorage:**

```javascript
_saveToCache() {
  try {
    const payload = {
      ts: Date.now(),
      workflows: this.getLoadedWorkflows()
    };
    
    localStorage.setItem(this.cacheKey, JSON.stringify(payload));
    console.log('💾 WorkflowRegistry: Saved to cache');
  } catch (e) {
    console.debug('Cache save skipped:', e);
  }
}
```

**Load from localStorage:**

```javascript
_loadFromCache() {
  try {
    const raw = localStorage.getItem(this.cacheKey);
    if (!raw) return false;

    const parsed = JSON.parse(raw);
    if (!parsed || !Array.isArray(parsed.workflows)) return false;

    parsed.workflows.forEach(w => {
      if (w && w.name) {
        this.loadedWorkflows.set(w.name, w);
      }
    });

    if (this.loadedWorkflows.size > 0) {
      console.warn('🗃️ WorkflowRegistry: Loaded workflows from cache (offline fallback)');
      this.initialized = true;
      this.ready = true;
      return true;
    }
  } catch (e) {
    console.debug('Cache load failed:', e);
  }

  return false;
}
```

**Cache Key:** `mozaiks_workflows_cache_v1`

**Offline Support:**

```
1. App loads
   ↓
2. Tries to fetch from /api/workflows
   ↓
3a. Success → Use live data, save to cache
3b. Failure → Load from cache (if exists)
   ↓
4. If cache also fails → Show error banner
```

### Public API

**Exported Functions:**

```javascript
// Get all loaded workflows
export const getLoadedWorkflows = () => workflowRegistry.getLoadedWorkflows();

// Get specific workflow
export const getWorkflow = (name) => workflowRegistry.getWorkflow(name);

// Initialize workflows (call on app startup)
export const initializeWorkflows = (opts) => workflowRegistry.initializeWorkflows(opts);

// Get workflow summary (debugging)
export const getWorkflowSummary = () => workflowRegistry.getWorkflowSummary();

// Refresh workflows (development)
export const refreshWorkflows = () => workflowRegistry.refresh();
```

**Usage in Components:**

```javascript
import { getLoadedWorkflows, getWorkflow } from '../workflows';

const MyComponent = () => {
  const workflows = getLoadedWorkflows();
  const generatorWorkflow = getWorkflow('Generator');

  return (
    <div>
      <h3>Available Workflows ({workflows.length})</h3>
      <ul>
        {workflows.map(w => (
          <li key={w.name}>
            {w.displayName} - {w.description}
          </li>
        ))}
      </ul>
    </div>
  );
};
```

## ChatUIContext Integration

### Initialization Flow

**ChatUIProvider initializes workflows on app startup:**

```javascript
export const ChatUIProvider = ({ children, onReady, ... }) => {
  const [workflowsInitialized, setWorkflowsInitialized] = useState(false);
  const WORKFLOW_INIT_TIMEOUT_MS = 8000;

  useEffect(() => {
    const initializeServices = async () => {
      try {
        // 1. Initialize workflow registry FIRST
        console.log('🔧 Initializing workflow registry...');
        
        try {
          await Promise.race([
            initializeWorkflows(),
            new Promise((_, reject) => 
              setTimeout(() => reject(new Error('workflow_init_timeout')), WORKFLOW_INIT_TIMEOUT_MS)
            )
          ]);
          
          setWorkflowsInitialized(true);
          console.log('✅ Workflow registry initialized');
          
        } catch (wfErr) {
          if (wfErr.message === 'workflow_init_timeout') {
            console.warn('⚠️ Workflow initialization timed out – continuing with partial UI.');
          } else {
            console.warn('⚠️ Workflow registry init failed:', wfErr);
          }
        }

        // 2. Initialize services (auth, api adapters)
        services.initialize({ authAdapter, apiAdapter });
        
        // 3. Get current user
        const currentUser = await authAdapterInst?.getCurrentUser();
        setUser(currentUser);

        // 4. Mark as initialized
        setInitialized(true);
        setLoading(false);
        onReady();

      } catch (error) {
        console.error('Failed to initialize ChatUI:', error);
        setLoading(false);
      }
    };

    initializeServices();
  }, [authAdapter, apiAdapter, onReady]);

  // Context value includes workflowsInitialized flag
  const contextValue = {
    // ... other values ...
    workflowsInitialized,
  };

  return (
    <ChatUIContext.Provider value={contextValue}>
      {children}
    </ChatUIContext.Provider>
  );
};
```

**Why Initialize Workflows First?**
- Components need workflow metadata to render correctly
- Early initialization prevents "Component not found" errors
- Timeout guard prevents endless loading spinner

### Loading State Handling

**Display spinner while workflows load:**

```javascript
const MyPage = () => {
  const { workflowsInitialized } = useChatUI();

  if (!workflowsInitialized) {
    return (
      <div className="loading-container">
        <div className="spinner"></div>
        <p>Loading workflows...</p>
      </div>
    );
  }

  return (
    <div>
      {/* Page content */}
    </div>
  );
};
```

## WorkflowUIRouter: Dynamic Loading

### Component Resolution

**Entry Point: `WorkflowUIRouter` component**

```javascript
const WorkflowUIRouter = ({ payload, onResponse, ui_tool_id, eventId }) => {
  const [Component, setComponent] = useState(null);
  const [error, setError] = useState(null);
  const [isLoading, setIsLoading] = useState(true);

  const sourceWorkflowName = payload?.workflow_name || 'Unknown';
  const componentType = payload?.component_type || 'UnknownComponent';

  const loadWorkflowComponent = useCallback(async (workflow, component) => {
    try {
      setIsLoading(true);
      setError(null);

      // 1. Generate cache key with chat-specific cache_seed
      const chatId = localStorage.getItem('mozaiks.current_chat_id');
      const cacheSeed = localStorage.getItem(`mozaiks.current_chat_id.cache_seed.${chatId}`);
      const cacheKey = `${chatId}:${cacheSeed}:${workflow}:${component}`;

      // 2. Check cache first
      if (componentCache.has(cacheKey)) {
        console.log('🛰️ WorkflowUIRouter: Cache hit', { cacheKey });
        setComponent(componentCache.get(cacheKey));
        setIsLoading(false);
        return;
      }

      // 3. Dynamically import workflow component module
      const workflowModule = await import(`../workflows/${workflow}/components/index.js`);

      // 4. Extract specific component
      const WorkflowComponent = workflowModule.default[component] || workflowModule[component];

      if (!WorkflowComponent) {
        throw new Error(`Component '${component}' not found in workflow '${workflow}'`);
      }

      // 5. Cache the component
      componentCache.set(cacheKey, WorkflowComponent);
      setComponent(() => WorkflowComponent);

      console.log(`✅ WorkflowUIRouter: Loaded ${workflow}:${component}`);

    } catch (loadError) {
      console.warn(`⚠️ WorkflowUIRouter: Failed to load ${workflow}:${component}`, loadError);

      // 6. Fallback to core components
      try {
        const coreModule = await import('./ui/index.js');
        const coreComponents = {
          'UserInputRequest': coreModule.UserInputRequest,
          'user_input': coreModule.UserInputRequest,
        };

        const coreComponent = coreComponents[component] || coreComponents[ui_tool_id];
        
        if (coreComponent) {
          console.log(`✅ WorkflowUIRouter: Using core component ${component || ui_tool_id}`);
          setComponent(() => coreComponent);
          setIsLoading(false);
          return;
        }
      } catch (coreError) {
        console.warn(`⚠️ WorkflowUIRouter: Failed to load core components`, coreError);
      }

      // 7. No component found - error state
      setError({
        type: 'component_not_found',
        workflow,
        component,
        message: loadError.message
      });
    } finally {
      setIsLoading(false);
    }
  }, [ui_tool_id]);

  useEffect(() => {
    loadWorkflowComponent(sourceWorkflowName, componentType);
  }, [sourceWorkflowName, componentType, loadWorkflowComponent]);

  // Render states
  if (isLoading) {
    return <LoadingIndicator />;
  }

  if (error) {
    return <ErrorDisplay error={error} />;
  }

  return <Component payload={payload} onResponse={onResponse} ui_tool_id={ui_tool_id} eventId={eventId} />;
};
```

### Component Cache

**Cache Structure:**

```javascript
const componentCache = new Map();

// Cache key format:
// "{chat_id}:{cache_seed}:{workflow_name}:{component_name}"

// Example:
// "chat_abc123:12345:Generator:ActionPlan"
```

**Why Include cache_seed?**
- Backend increments `cache_seed` when workflow changes
- Frontend detects mismatch and reloads components
- Prevents rendering stale components after workflow updates

**Cache Lifetime:**
- **Persistent:** Lasts for entire page session
- **Invalidated:** When `cache_seed` changes or page refreshes

### Dynamic Import Pattern

**Webpack Magic Comments (optional):**

```javascript
// Preload all workflow components for faster loading
const workflowModule = await import(
  /* webpackChunkName: "workflow-[request]" */
  `../workflows/${workflow}/components/index.js`
);
```

**Build Output:**
```
dist/
  static/js/
    workflow-Generator.chunk.js
    workflow-MarketingAutomation.chunk.js
    workflow-DataAnalysis.chunk.js
```

Each workflow's components bundled separately for optimal code splitting.

## Cache Synchronization

### cache_seed Purpose

**Problem:** Workflow definitions can change while chat is active (development or dynamic updates).

**Solution:** Backend assigns unique `cache_seed` per workflow version.

**Flow:**

```
1. User starts chat → Backend generates cache_seed (e.g., "12345")
   ↓
2. Backend sends cache_seed in /api/chat/start response
   ↓
3. Frontend stores: localStorage["mozaiks.current_chat_id.cache_seed.{chat_id}"] = "12345"
   ↓
4. Frontend uses cache_seed in component cache key
   ↓
5. Workflow updated → Backend increments cache_seed to "12346"
   ↓
6. New chat.tool_call event has updated cache_seed
   ↓
7. Frontend detects mismatch: "12345" != "12346"
   ↓
8. WorkflowUIRouter cache key changes → Cache miss → Component reloaded
```

### Implementation

**Backend (shared_app.py):**

```python
@app.post("/api/chats/{app_id}/{workflow_name}/start")
async def start_chat(app_id: str, workflow_name: str, request: Request):
  payload = await request.json()
  user_id = payload["user_id"]

  chat_id = str(uuid4())
  cache_seed = await persistence_manager.get_or_assign_cache_seed(chat_id, app_id)

  await persistence_manager.create_chat_session(
    chat_id=chat_id,
    app_id=app_id,
    user_id=user_id,
    workflow_name=workflow_name,
    cache_seed=cache_seed,
  )

  return {
    "chat_id": chat_id,
    "cache_seed": cache_seed,
    "workflow_name": workflow_name,
    "app_id": app_id,
  }
```

**Frontend (ChatPage.js):**

```javascript
const startNewChat = async () => {
  const result = await api.startChat(appId, workflowName, userId);
  
  const { chat_id, cache_seed } = result;
  
  // Store both chat_id and cache_seed
  localStorage.setItem('mozaiks.current_chat_id', chat_id);
  localStorage.setItem(`mozaiks.current_chat_id.cache_seed.${chat_id}`, cache_seed);
  
  setCurrentChatId(chat_id);
  setCacheSeed(cache_seed);
};
```

**Frontend (WorkflowUIRouter.js):**

```javascript
const loadWorkflowComponent = async (workflow, component) => {
  // Retrieve chat-specific cache_seed
  const chatId = localStorage.getItem('mozaiks.current_chat_id');
  const cacheSeed = localStorage.getItem(`mozaiks.current_chat_id.cache_seed.${chatId}`);
  
  // Include in cache key
  const cacheKey = `${chatId}:${cacheSeed}:${workflow}:${component}`;
  
  if (componentCache.has(cacheKey)) {
    // Cache hit - use cached component
    return componentCache.get(cacheKey);
  }
  
  // Cache miss - load component
  const module = await import(`../workflows/${workflow}/components/index.js`);
  componentCache.set(cacheKey, module.default[component]);
  
  return module.default[component];
};
```

## Component Registration

### Workflow Component Structure

**Required Directory Layout:**

```
ChatUI/src/workflows/
  {WorkflowName}/
    components/
      index.js              ← REQUIRED: Export all components
      Component1.js         ← Individual component files
      Component2.js
      Component3.js
```

### index.js Export Pattern

**Default + Named Exports:**

```javascript
// workflows/Generator/components/index.js

import AgentAPIKeyInput from './AgentAPIKeyInput';
import FileDownloadCenter from './FileDownloadCenter';
import ActionPlan from './ActionPlan';

// Default export as object (REQUIRED for WorkflowUIRouter)
const GeneratorComponents = {
  AgentAPIKeyInput,
  FileDownloadCenter,
  ActionPlan
};

export default GeneratorComponents;

// Named exports for convenience
export {
  AgentAPIKeyInput,
  FileDownloadCenter,
  ActionPlan
};
```

**Why Both?**

```javascript
// WorkflowUIRouter uses default export
const workflowModule = await import('../workflows/Generator/components/index.js');
const Component = workflowModule.default['ActionPlan']; // ✅ Works

// Direct imports also work
import { ActionPlan } from '../workflows/Generator/components';
```

### Component Naming Convention

**Rules:**
1. **PascalCase** (e.g., `ActionPlan`, not `action_plan`)
2. **Match backend component_type** exactly (case-sensitive)
3. **Descriptive names** (e.g., `FileDownloadCenter`, not `Download`)

**Backend → Frontend Mapping:**

```python
# Backend (tools.json)
{
  "ui": {
    "component": "ActionPlan",  # Must match frontend export name
    "mode": "artifact"
  }
}
```

```javascript
// Frontend (workflows/Generator/components/index.js)
const GeneratorComponents = {
  ActionPlan,  // ✅ Matches "ActionPlan" from backend
  // ...
};
```

## Error Handling

### Component Load Failures

**Scenario 1: Component Not Found**

```
Error: Component 'ActionPlan' not found in workflow 'Generator'
```

**Causes:**
- Component not exported in `index.js`
- Spelling mismatch (backend: "ActionPlan", frontend: "ActionPlanView")
- Component file doesn't exist

**Debug:**

```javascript
// Check exports
import * as Components from '../workflows/Generator/components';
console.log(Object.keys(Components));
// Expected: ["default", "ActionPlan", "AgentAPIKeyInput", ...]
```

**Scenario 2: Import Error**

```
Error: Cannot find module '../workflows/Generator/components/index.js'
```

**Causes:**
- Workflow directory doesn't exist
- `components/` directory missing
- `index.js` file missing

**Fix:** Create missing directory structure.

**Scenario 3: Syntax Error in Component**

```
SyntaxError: Unexpected token '<' in ActionPlan.js
```

**Causes:**
- Syntax error in JSX
- Missing import statements
- Invalid JavaScript

**Debug:** Check browser console for full error stack.

### Fallback Strategies

**1. Core Components Fallback:**

```javascript
try {
  // Try workflow-specific component
  const workflowModule = await import(`../workflows/${workflow}/components/index.js`);
  return workflowModule.default[component];
} catch (error) {
  // Fall back to core components
  const coreModule = await import('./ui/index.js');
  return coreModule.UserInputRequest;
}
```

**2. Error Display Component:**

```javascript
const ErrorDisplay = ({ error }) => (
  <div className="component-error">
    <h3>Component Loading Error</h3>
    <p>Could not load component <code>{error.component}</code> from workflow <code>{error.workflow}</code></p>
    <p className="error-message">{error.message}</p>
    <details>
      <summary>Expected structure</summary>
      <pre>
{`workflows/
  ${error.workflow}/
    components/
      index.js          ← Must export { ${error.component} }
      ${error.component}.js  ← Component implementation`}
      </pre>
    </details>
  </div>
);
```

## Best Practices

### 1. Workflow Discovery

**Auto-Discover Workflows:**

```javascript
// ✅ GOOD: Fetch from backend
const workflows = await fetch('/api/workflows').then(r => r.json());

// ❌ BAD: Hardcode workflow list
const workflows = ['Generator', 'MarketingAutomation'];
```

**Why?** Backend is source of truth; frontend shouldn't know workflows a priori.

### 2. Component Lazy Loading

**Load on Demand:**

```javascript
// ✅ GOOD: Dynamic import when needed
const Component = await import(`../workflows/${workflow}/components/index.js`);

// ❌ BAD: Import all workflows upfront
import GeneratorComponents from '../workflows/Generator/components';
import MarketingComponents from '../workflows/MarketingAutomation/components';
```

**Why?** Reduces initial bundle size; only load what's used.

### 3. Cache Management

**Respect cache_seed:**

```javascript
// ✅ GOOD: Include cache_seed in cache key
const cacheKey = `${chatId}:${cacheSeed}:${workflow}:${component}`;

// ❌ BAD: Ignore cache_seed
const cacheKey = `${workflow}:${component}`;
```

**Why?** Prevents stale components when workflow updates.

### 4. Error Boundaries

**Wrap Dynamic Components:**

```javascript
<ErrorBoundary FallbackComponent={ErrorFallback}>
  <WorkflowUIRouter {...props} />
</ErrorBoundary>
```

**Why?** Component errors don't crash entire app.

### 5. Timeout Guards

**Prevent Endless Loading:**

```javascript
await Promise.race([
  initializeWorkflows(),
  new Promise((_, reject) => 
    setTimeout(() => reject(new Error('timeout')), 8000)
  )
]);
```

**Why?** Backend unreachable shouldn't block app indefinitely.

## Debugging

### Enable Debug Logging

```javascript
// In browser console
localStorage.setItem('debug_workflow_loading', 'true');
location.reload();
```

**Log Output:**

```
🚀 WorkflowRegistry: Fetching workflows from backend API...
✅ Loaded workflow from API: Generator
✅ WorkflowRegistry: Loaded 1 workflows from backend
🛰️ WorkflowUIRouter: Loading component {workflow: "Generator", component: "ActionPlan"}
🛰️ WorkflowUIRouter: Cache miss
✅ WorkflowUIRouter: Loaded Generator:ActionPlan
```

### Inspect Workflow Registry

```javascript
// In browser console
import { getWorkflowSummary } from '../workflows';

const summary = getWorkflowSummary();
console.log(summary);

// Output:
// {
//   initialized: true,
//   ready: true,
//   workflowCount: 1,
//   workflows: [
//     { name: "Generator", displayName: "Workflow Generator", version: "1.0.0", agentCount: 2 }
//   ]
// }
```

### Check Component Cache

```javascript
// In WorkflowUIRouter.js (add temporarily)
console.log('Component cache:', Array.from(componentCache.keys()));

// Output:
// ["chat_abc123:12345:Generator:ActionPlan", "chat_abc123:12345:Generator:AgentAPIKeyInput"]
```

### Verify Backend API

```bash
# Test workflow endpoint
curl http://localhost:8000/api/workflows | jq '.'

# Expected output:
# {
#   "Generator": {
#     "name": "Generator",
#     "agents": {...},
#     "tools": [...]
#   }
# }
```

## Next Steps

- **[ChatUI Architecture](./chatui_architecture.md)**: Frontend application structure
- **[UI Components Reference](./ui_components.md)**: Component patterns and props contracts
- **[Workflow Authoring](../workflows/workflow_authoring.md)**: Creating new workflows
- **[Configuration Reference](../runtime/configuration_reference.md)**: Environment variables and settings
- **[Deployment Guide](../operations/deployment.md)**: Production deployment patterns
