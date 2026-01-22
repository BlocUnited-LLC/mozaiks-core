# ChatUI Architecture Guide

## Purpose

This document explains the React-based ChatUI frontend architecture in MozaiksAI, covering application structure, context providers, WebSocket transport, message rendering, workflow component loading, and state management patterns. Understanding ChatUI architecture is essential for extending the frontend, debugging UI issues, and maintaining transport layer coordination.

## Overview

**ChatUI** is a **transport-agnostic React application** that provides the user-facing chat interface for MozaiksAI workflows. It connects to the AG2 runtime via WebSocket, renders agent messages and UI tool components, manages conversation state, and sends user responses back to the backend.

> UI / tooling / theming implementation details have been consolidated into: `frontend/unified_ui_tools_and_design.md`. All prior scattered docs (design system, auto-tool flow, theme management) were removed in favor of that single authoritative guide.

**Key Characteristics:**
- **Single-Page Application (SPA)**: React Router with dynamic routing
- **Context-Driven State**: Centralized state via `ChatUIContext`
- **Transport Agnostic**: Supports WebSocket (primary) and REST (fallback)
- **Event-Driven**: Processes `chat.*` events from runtime via Simple Events protocol
- **Dynamic Component Loading**: Workflow UI components loaded on-demand
- **Session Persistence**: Automatic chat resume with sequence tracking
- **Multi-Tenant**: Supports app_id/user_id/chat_id scoping

## Architecture Layers

```
┌─────────────────────────────────────────────────────────────────────┐
│                      ChatUI Architecture                             │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│ 1. ROUTING LAYER (React Router)                                     │
├─────────────────────────────────────────────────────────────────────┤
│  App.js                                                              │
│   └─> <ChatUIProvider>                                              │
│        └─> <Router>                                                 │
│             └─> Routes:                                             │
│                  /                        → ChatPage               │
│                  /chat                    → ChatPage               │
│                  /chat/:appId      → ChatPage               │
│                  /app/:appId/:workflowname → ChatPage │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│ 2. CONTEXT LAYER (Global State)                                     │
├─────────────────────────────────────────────────────────────────────┤
│  ChatUIContext.js                                                    │
│   • User authentication state (user, logout)                        │
│   • Service adapters (auth, api)                                    │
│   • Initialization flags (initialized, loading)                     │
│   • Workflow registry status (workflowsInitialized)                 │
│   • Configuration access (config)                                   │
│                                                                      │
│  Consumer: const { user, api, config } = useChatUI();              │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│ 3. SERVICE LAYER (Transport & API)                                  │
├─────────────────────────────────────────────────────────────────────┤
│  services/index.js                                                   │
│   └─> AuthAdapter (MockAuthAdapter | TokenAuthAdapter)             │
│   └─> ApiAdapter (WebSocketApiAdapter | RestApiAdapter)            │
│                                                                      │
│  adapters/api.js                                                     │
│   • WebSocketApiAdapter.createWebSocketConnection()                 │
│   • startChat() → POST /api/chat/start                             │
│   • sendMessageToWorkflow() → POST /chat/{app}/{chat}/input │
│   • getMessageHistory() → GET /api/chat/history                    │
│   • WebSocket event handling (onopen, onmessage, onerror, onclose) │
│   • Sequence tracking & resume (lastClientIndex)                    │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│ 4. PAGE LAYER (Main UI)                                             │
├─────────────────────────────────────────────────────────────────────┤
│  pages/ChatPage.js (1,448 lines - primary orchestrator)             │
│   • WebSocket connection management                                 │
│   • Message state (messages, setMessages)                           │
│   • Artifact panel state (currentArtifactMessages)                  │
│   • Chat lifecycle (start/resume)                                   │
│   • Event routing (handleIncoming → chat.* events)                  │
│   • User input handling (sendMessage)                               │
│   • UI tool response coordination                                   │
│   • Spinner and loading states                                      │
│                                                                      │
│  Layout:                                                             │
│   <Header />                                                         │
│   <ChatInterface />  ← Main chat area                              │
│   <ArtifactPanel />  ← Side panel for artifact-mode UI tools       │
│   <Footer />                                                         │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│ 5. COMPONENT LAYER (UI Elements)                                    │
├─────────────────────────────────────────────────────────────────────┤
│  components/chat/                                                    │
│   • ChatInterface.js → Message list + input box                    │
│   • ChatMessage.js → Individual message rendering                  │
│   • ArtifactPanel.js → Side panel for artifact-mode components     │
│   • ConnectionStatus.js → WebSocket status indicator               │
│                                                                      │
│  components/layout/                                                  │
│   • Header.js → Top navigation + user menu                         │
│   • Footer.js → Bottom bar                                         │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│ 6. CORE LAYER (Dynamic Systems)                                     │
├─────────────────────────────────────────────────────────────────────┤
│  core/WorkflowUIRouter.js                                            │
│   • Dynamic component loading from workflows/{workflow}/components/ │
│   • Component cache with per-chat cache_seed scoping               │
│   • Error fallbacks and loading states                              │
│                                                                      │
│  core/dynamicUIHandler.js                                            │
│   • Processes backend UI events (ui_tool_event, user_input_request) │
│   • Event handler registry                                          │
│   • UI update callbacks                                             │
│                                                                      │
│  core/eventDispatcher.js                                             │
│   • Emits frontend events                                           │
│   • Event correlation                                                │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│ 7. WORKFLOW LAYER (Dynamic Components)                              │
├─────────────────────────────────────────────────────────────────────┤
│  workflows/index.js                                                  │
│   • WorkflowRegistry (fetch metadata from /api/workflows)           │
│   • Workflow initialization with retry/cache fallback              │
│   • Component discovery and registration                            │
│                                                                      │
│  workflows/{WorkflowName}/components/                                │
│   • index.js → Export all workflow UI components                   │
│   • {ComponentName}.js → Individual UI tool components             │
│                                                                      │
│  Example: workflows/Generator/components/                            │
│   • index.js                                                         │
│   • AgentAPIKeyInput.js                                             │
│   • ActionPlan.js                                                    │
│   • GenerateAndDownload.js                                          │
└─────────────────────────────────────────────────────────────────────┘
```

## Application Bootstrap

### Entry Point: `index.js`

```javascript
import React from 'react';
import ReactDOM from 'react-dom/client';
import './index.css';
import App from './App';

const root = ReactDOM.createRoot(document.getElementById('root'));
root.render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
```

**Bootstrap Flow:**
1. React renders `<App />` into `#root` DOM element
2. `App.js` wraps routing in `<ChatUIProvider>`
3. Context initializes services, workflows, and auth
4. Routes resolve to `<ChatPage />` based on URL

### App.js Structure

```javascript
import { ChatUIProvider } from './context/ChatUIContext';
import ChatPage from './pages/ChatPage';

function App() {
  const handleChatUIReady = () => {
    console.log('ChatUI is ready!');
  };

  return (
    <ChatUIProvider onReady={handleChatUIReady}>
      <Router>
        <Routes>
          <Route path="/" element={<ChatPage />} />
          <Route path="/chat" element={<ChatPage />} />
          <Route path="/chat/:appId" element={<ChatPage />} />
          <Route path="/app/:appId/:workflowname" element={<ChatPage />} />
          <Route path="*" element={<ChatPage />} />
        </Routes>
      </Router>
    </ChatUIProvider>
  );
}
```

**Routing Patterns:**
- `/` → Default chat with config defaults
- `/chat/:appId` → Chat for specific app
- `/app/:appId/:workflowname` → Specific workflow for app

**URL Parameters:**
- `appId`: Multi-tenant app identifier (e.g., `"68542c1109381de738222350"`)
- `workflowname`: Workflow to execute (e.g., `"Generator"`)

## ChatUIContext Provider

### Purpose

`ChatUIContext` is the **global state container** for the entire application, managing:
- User authentication state
- Service adapter instances
- Initialization lifecycle
- Workflow registry status
- Configuration access

### Initialization Flow

```javascript
export const ChatUIProvider = ({ children, onReady, ... }) => {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  const [initialized, setInitialized] = useState(false);
  const [workflowsInitialized, setWorkflowsInitialized] = useState(false);

  useEffect(() => {
    const initializeServices = async () => {
      // 1. Initialize workflow registry (fetch from /api/workflows)
      await initializeWorkflows();
      setWorkflowsInitialized(true);

      // 2. Initialize services with adapters
      services.initialize({ authAdapter, apiAdapter });
      const authAdapterInst = services.getAuthAdapter();
      const apiAdapterInst = services.getApiAdapter();

      // 3. Get current user
      const currentUser = await authAdapterInst?.getCurrentUser();
      setUser(currentUser);

      // 4. Listen for auth state changes
      authAdapterInst?.onAuthStateChange((newUser) => setUser(newUser));

      // 5. Mark as initialized
      setInitialized(true);
      setLoading(false);
      onReady();
    };

    initializeServices();
  }, [authAdapter, apiAdapter, onReady]);

  // ... context value and provider ...
};
```

**Initialization Sequence:**
1. Workflow registry initialization (fetch metadata from backend)
2. Auth/API service adapter setup
3. User authentication check
4. Auth state listener registration
5. Ready callback invocation

**Timeout Guard:**

```javascript
const WORKFLOW_INIT_TIMEOUT_MS = 8000;

await Promise.race([
  initializeWorkflows(),
  new Promise((_, reject) => 
    setTimeout(() => reject(new Error('workflow_init_timeout')), WORKFLOW_INIT_TIMEOUT_MS)
  )
]);
```

Prevents endless loading spinner if workflow API is unreachable.

### Context API

**Provided Values:**

```javascript
const contextValue = {
  // User state
  user,              // Current user object (from auth adapter)
  setUser,           // Update user state
  loading,           // Initial load state
  initialized,       // Services ready

  // System state
  agentSystemInitialized,   // Agent system ready (workflow-based)
  workflowsInitialized,     // Workflow registry loaded

  // Configuration
  config: config.getConfig(),  // App configuration object

  // Services
  auth: authAdapterInstance,   // Auth adapter instance
  api: apiAdapterInstance,     // API adapter instance

  // Actions
  logout: async () => { ... }  // Logout function
};
```

**Usage Pattern:**

```javascript
import { useChatUI } from '../context/ChatUIContext';

function MyComponent() {
  const { user, api, config, workflowsInitialized } = useChatUI();

  if (!workflowsInitialized) {
    return <LoadingSpinner />;
  }

  // ... use context values ...
}
```

## Service Layer

### Services Singleton

**Purpose:** Centralized service management for auth and API adapters

**Structure:**

```javascript
class ChatUIServices {
  constructor() {
    this.authAdapter = null;
    this.apiAdapter = null;
    this.initialized = false;
  }

  initialize(options = {}) {
    this.authAdapter = this.createAuthAdapter(options.authAdapter);
    this.apiAdapter = this.createApiAdapter(options.apiAdapter);
    this.initialized = true;
  }

  createAuthAdapter(customAdapter) {
    if (customAdapter) return customAdapter;
    const authMode = config.get('auth.mode');  // 'mock' | 'token'
    return authMode === 'mock' 
      ? new MockAuthAdapter() 
      : new TokenAuthAdapter(config.get('api.baseUrl'));
  }

  createApiAdapter(customAdapter) {
    if (customAdapter) return customAdapter;
    const { wsUrl, baseUrl } = config.get('api');
    return wsUrl 
      ? new WebSocketApiAdapter({ wsUrl, baseUrl })
      : new RestApiAdapter({ baseUrl });
  }
}

const services = new ChatUIServices();
export default services;
```

**Adapter Selection:**
- **WebSocket preferred** when `config.api.wsUrl` is set
- **REST fallback** when only `config.api.baseUrl` is set
- **Custom adapters** via `initialize({ authAdapter, apiAdapter })`

### WebSocketApiAdapter

**Primary transport layer** for real-time chat communication.

**Key Methods:**

#### createWebSocketConnection

```javascript
createWebSocketConnection(appId, userId, callbacks, workflowname, chatId) {
  const wsUrl = `${wsBase}/ws/${workflowname}/${appId}/${chatId}/${userId}`;
  const socket = new WebSocket(wsUrl);

  // Sequence tracking for resume capability
  let lastSequence = parseInt(localStorage.getItem(`ws_idx_${chatId}`) || '0');
  let resumePending = false;

  socket.onopen = () => {
    console.log("WebSocket connection established");
    if (lastSequence > 0) {
      // Request resume for existing chat
      socket.send(JSON.stringify({
        type: 'client.resume',
        chat_id: chatId,
        lastClientIndex: lastSequence
      }));
    }
    callbacks.onOpen?.();
  };

  socket.onmessage = (event) => {
    const data = JSON.parse(event.data);

    // Track sequence numbers
    if (data.seq && typeof data.seq === 'number') {
      if (data.seq > lastSequence) {
        lastSequence = data.seq;
        localStorage.setItem(`ws_idx_${chatId}`, lastSequence.toString());
      } else if (data.seq < lastSequence - 1 && !resumePending) {
        // Sequence gap detected - request resume
        console.warn(`Sequence gap: received ${data.seq}, expected > ${lastSequence}`);
        sendResume();
        return;
      }
    }

    // Handle resume boundary
    if (data.type === 'chat.resume_boundary') {
      console.log(`Resume completed: ${data.data?.replayed_events || 0} events replayed`);
      resumePending = false;
    }

    callbacks.onMessage?.(data);
  };

  return {
    socket,
    send: (message) => {
      if (socket.readyState === WebSocket.OPEN) {
        socket.send(typeof message === 'object' ? JSON.stringify(message) : message);
        return true;
      }
      return false;
    },
    close: () => socket.close()
  };
}
```

**WebSocket URL Pattern:**

```
ws://localhost:8000/ws/{workflow}/{app_id}/{chat_id}/{user_id}

Example:
ws://localhost:8000/ws/Generator/68542c1109381de738222350/chat_abc123/56132
```

**Resume Protocol:**

1. Frontend stores `lastClientIndex` (seq number) in localStorage per chat
2. On reconnect, sends `client.resume` with last known sequence
3. Backend replays missed events (seq > lastClientIndex)
4. Backend emits `chat.resume_boundary` to mark replay completion
5. Frontend processes new events normally

#### startChat

```javascript
async startChat(appId, workflowname, userId, fetchOpts = {}) {
  const clientRequestId = crypto.randomUUID();
  const response = await fetch(`http://localhost:8000/api/chat/start`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      app_id: appId,
      user_id: userId,
      workflow_name: workflowname,
      client_request_id: clientRequestId
    }),
    ...fetchOpts
  });

  if (!response.ok) {
    throw new Error(`Start chat failed: ${response.status}`);
  }

  const result = await response.json();
  return {
    chat_id: result.chat_id,
    cache_seed: result.cache_seed,
    ...result
  };
}
```

**Response Structure:**

```json
{
  "chat_id": "chat_abc123",
  "cache_seed": "12345",
  "workflow_name": "Generator",
  "app_id": "68542c1109381de738222350",
  "user_id": "56132",
  "status": "started"
}
```

#### sendMessageToWorkflow

```javascript
async sendMessageToWorkflow(message, appId, userId, workflowname, chatId) {
  const response = await fetch(
    `http://localhost:8000/chat/${appId}/${chatId}/${userId}/input`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        message,
        workflow_name: workflowname,
        app_id: appId,
        user_id: userId
      })
    }
  );

  if (!response.ok) {
    return { success: false, error: `HTTP ${response.status}` };
  }

  return await response.json();
}
```

**Fallback Pattern:** Used when WebSocket is unavailable; sends message via HTTP POST.

## ChatPage Component

### Purpose

`ChatPage.js` is the **primary orchestrator** for chat sessions, managing:
- WebSocket connection lifecycle
- Message state and rendering
- Chat start/resume logic
- Event routing and processing
- UI tool coordination
- Artifact panel state

### State Management

**Core State Variables:**

```javascript
const [messages, setMessages] = useState([]);                    // Chat messages
const messagesRef = useRef([]);                                 // Non-stale message access
const [ws, setWs] = useState(null);                             // WebSocket connection
const [connectionStatus, setConnectionStatus] = useState('disconnected');
const [currentChatId, setCurrentChatId] = useState(null);       // Active chat ID
const [cacheSeed, setCacheSeed] = useState(null);               // Per-chat cache seed
const [chatExists, setChatExists] = useState(null);             // tri-state: null/true/false
const [currentArtifactMessages, setCurrentArtifactMessages] = useState([]);  // Artifact panel
const [isSidePanelOpen, setIsSidePanelOpen] = useState(false);  // Panel visibility
const [showInitSpinner, setShowInitSpinner] = useState(false);  // Loading indicator
const [tokensExhausted, setTokensExhausted] = useState(false);  // Token limit reached
```

**Ref Guards (prevent race conditions):**

```javascript
const connectionInProgressRef = useRef(false);      // Prevent duplicate connections
const pendingStartRef = useRef(false);              // Prevent overlapping starts
const initSpinnerShownRef = useRef(false);          // Spinner show tracking
const initSpinnerHiddenOnceRef = useRef(false);     // Spinner hide tracking
const artifactRestoredOnceRef = useRef(false);      // Artifact restore guard
```

### Connection Lifecycle

**Flow:**

```
1. ChatPage mounts
   ↓
2. Check localStorage for existing chat_id
   ↓
3a. chat_id exists → Start resume flow
3b. No chat_id → Start new chat flow
   ↓
4. Establish WebSocket connection
   ↓
5. Send client.resume (if existing) or start fresh
   ↓
6. Process incoming events via handleIncoming
   ↓
7. Render messages and UI tools
```

**Connection Initialization:**

```javascript
useEffect(() => {
  // Prevent duplicate connections
  if (connectionInitialized || connectionInProgressRef.current) {
    return;
  }

  connectionInProgressRef.current = true;

  const initConnection = async () => {
    try {
      // Get stored chat_id
      const storedChatId = localStorage.getItem(LOCAL_STORAGE_KEY);

      if (storedChatId) {
        // Resume existing chat
        await resumeChat(storedChatId);
      } else {
        // Start new chat
        await startNewChat();
      }

      setConnectionInitialized(true);
    } catch (error) {
      console.error('Connection initialization failed:', error);
    } finally {
      connectionInProgressRef.current = false;
    }
  };

  initConnection();
}, [currentAppId, currentUserId, currentWorkflowName]);
```

**Start New Chat:**

```javascript
const startNewChat = async () => {
  try {
    // Call backend to create new chat
    const result = await api.startChat(
      currentAppId,
      currentWorkflowName,
      currentUserId
    );

    const { chat_id, cache_seed } = result;

    // Store chat_id and cache_seed
    localStorage.setItem(LOCAL_STORAGE_KEY, chat_id);
    localStorage.setItem(`${LOCAL_STORAGE_KEY}.cache_seed.${chat_id}`, cache_seed);

    setCurrentChatId(chat_id);
    setCacheSeed(cache_seed);
    setChatExists(true);

    // Establish WebSocket connection
    connectWebSocket(chat_id);

    console.log(`✅ New chat started: ${chat_id}`);
  } catch (error) {
    console.error('Failed to start new chat:', error);
  }
};
```

**Resume Existing Chat:**

```javascript
const resumeChat = async (chatId) => {
  try {
    // Load cache_seed for this chat
    const storedSeed = localStorage.getItem(`${LOCAL_STORAGE_KEY}.cache_seed.${chatId}`);

    setCurrentChatId(chatId);
    setCacheSeed(storedSeed);
    setChatExists(true);

    // Establish WebSocket (will auto-resume via seq tracking)
    connectWebSocket(chatId);

    console.log(`✅ Resuming chat: ${chatId}`);
  } catch (error) {
    console.error('Failed to resume chat:', error);
  }
};
```

**WebSocket Connection:**

```javascript
const connectWebSocket = (chatId) => {
  const wsConnection = api.createWebSocketConnection(
    currentAppId,
    currentUserId,
    {
      onOpen: () => {
        setConnectionStatus('connected');
        setShowInitSpinner(true);  // Show spinner until first message
        initSpinnerShownRef.current = true;
      },
      onMessage: handleIncoming,  // Route events to handler
      onError: (error) => {
        console.error('WebSocket error:', error);
        setConnectionStatus('error');
      },
      onClose: () => {
        setConnectionStatus('disconnected');
        setWs(null);
      }
    },
    currentWorkflowName,
    chatId
  );

  setWs(wsConnection);
};
```

### Event Processing

**Entry Point: `handleIncoming(data)`**

```javascript
const handleIncoming = useCallback((data) => {
  if (!data?.type) return;

  // Log for debugging
  logAgentOutput('INCOMING', extractAgentName(data), data, { type: data.type });

  // Hide initial spinner on first interactive event
  if (initSpinnerShownRef.current && !initSpinnerHiddenOnceRef.current) {
    const isText = data.type === 'chat.text';
    const isInputRequest = data.type === 'chat.input_request';
    
    if (isText || isInputRequest) {
      initSpinnerHiddenOnceRef.current = true;
      setShowInitSpinner(false);
    }
  }

  // Route events to specialized handlers
  switch (data.type) {
    case 'chat.text':
      handleTextMessage(data);
      break;
    case 'chat.tool_call':
      handleToolCall(data);
      break;
    case 'chat.tool_response':
      handleToolResponse(data);
      break;
    case 'chat.input_request':
      handleInputRequest(data);
      break;
    case 'chat.resume_boundary':
      handleResumeBoundary(data);
      break;
    case 'chat.token_exhausted':
      setTokensExhausted(true);
      break;
    default:
      console.warn(`Unhandled event type: ${data.type}`);
  }
}, [/* dependencies */]);
```

**Event Handlers:**

**chat.text (Agent Messages):**

```javascript
const handleTextMessage = (data) => {
  const agentName = extractAgentName(data);
  const content = data.content || data.data?.content || '';
  const ui_hidden = data.ui_hidden || data.data?.ui_hidden || false;

  // Skip rendering if ui_hidden (internal coordination tokens)
  if (ui_hidden) {
    console.log(`[UI_HIDDEN] Suppressing message from ${agentName}`);
    return;
  }

  // Add to messages
  setMessages(prev => [
    ...prev,
    {
      id: data.event_id || crypto.randomUUID(),
      role: 'assistant',
      content,
      agent: agentName,
      timestamp: Date.now(),
      type: 'text'
    }
  ]);
};
```

**chat.tool_call (UI Tool Invocation):**

```javascript
const handleToolCall = (data) => {
  const { tool_name, component_type, payload, corr } = data;

  // Determine display mode from payload
  const mode = payload?.ui_config?.mode || payload?.mode || 'inline';

  // Create UI tool message
  const toolMessage = {
    id: data.event_id || crypto.randomUUID(),
    role: 'tool',
    type: 'ui_tool',
    tool_name,
    component_type,
    payload,
    corr,
    mode,
    timestamp: Date.now()
  };

  if (mode === 'artifact') {
    // Render in artifact panel
    setCurrentArtifactMessages([toolMessage]);
    setIsSidePanelOpen(true);
    lastArtifactEventRef.current = data.event_id;
  } else {
    // Render inline in chat
    setMessages(prev => [...prev, toolMessage]);
  }
};
```

**chat.tool_response (Tool Completion):**

```javascript
const handleToolResponse = (data) => {
  const { call_id, status, success, content, interaction_type } = data;

  console.log(`Tool response: ${call_id} - ${status} (${success ? 'success' : 'failed'})`);

  // Suppress intermediate auto-tool success responses (already handled by UI renderer)
  // Auto-tools emit multiple events: action_plan, mermaid_sequence_diagram, final display tool
  // Only show failures (for debugging) or non-auto-tool responses
  if (interaction_type === 'auto_tool' && success) {
    console.log(`⏭️ Skipping auto-tool success response (${data.tool_name}) - handled by UI renderer`);
    return;
  }

  // Display error messages or non-auto-tool responses
  const responseContent = success 
    ? `✅ Tool Response: ${content || 'Success'}` 
    : `❌ Tool Failed: ${content || 'Error'}`;
    
  setMessages(prev => [
    ...prev,
    {
      id: crypto.randomUUID(),
      role: 'system',
      content: responseContent,
      type: success ? 'info' : 'error',
      timestamp: Date.now()
    }
  ]);
};
```

### User Input Handling

**Send Message:**

```javascript
const sendMessage = async (messageText) => {
  if (!messageText.trim() || !ws || !currentChatId) return;

  // Add user message to UI immediately
  const userMessage = {
    id: crypto.randomUUID(),
    role: 'user',
    content: messageText,
    timestamp: Date.now(),
    type: 'text'
  };
  setMessages(prev => [...prev, userMessage]);

  // Send via WebSocket
  ws.send({
    type: 'user_message',
    content: messageText,
    chat_id: currentChatId,
    app_id: currentAppId,
    user_id: currentUserId,
    workflow_name: currentWorkflowName
  });
};
```

**UI Tool Response:**

```javascript
const submitInputRequest = useCallback((responseData) => {
  console.log('Sending WebSocket response:', responseData);
  
  if (ws && ws.send) {
    return ws.send(responseData);  // Send as object; adapter serializes
  } else {
    console.warn('No WebSocket connection available for UI tool response');
  }
}, [ws]);
```

## Dynamic Component System

### WorkflowUIRouter

**Purpose:** Dynamically load and render workflow-specific UI tool components.

**Architecture:**

```javascript
const WorkflowUIRouter = ({ payload, onResponse, ui_tool_id, eventId }) => {
  const [Component, setComponent] = useState(null);
  const [error, setError] = useState(null);
  const [isLoading, setIsLoading] = useState(true);

  const sourceWorkflowName = payload?.workflow_name || 'Unknown';
  const componentType = payload?.component_type || 'UnknownComponent';

  const loadWorkflowComponent = useCallback(async (workflow, component) => {
    try {
      // Generate cache key with chat-specific cache_seed
      const chatId = localStorage.getItem('mozaiks.current_chat_id');
      const cacheSeed = localStorage.getItem(`mozaiks.current_chat_id.cache_seed.${chatId}`);
      const cacheKey = `${chatId}:${cacheSeed}:${workflow}:${component}`;

      // Check cache first
      if (componentCache.has(cacheKey)) {
        setComponent(componentCache.get(cacheKey));
        setIsLoading(false);
        return;
      }

      // Dynamically import workflow component
      const workflowModule = await import(`../workflows/${workflow}/components/index.js`);
      const WorkflowComponent = workflowModule.default[component] || workflowModule[component];

      if (!WorkflowComponent) {
        throw new Error(`Component '${component}' not found in workflow '${workflow}'`);
      }

      // Cache the component
      componentCache.set(cacheKey, WorkflowComponent);
      setComponent(() => WorkflowComponent);

      console.log(`✅ Loaded ${workflow}:${component}`);
    } catch (loadError) {
      // Fallback to core components
      const coreModule = await import('./ui/index.js');
      const coreComponent = coreModule.UserInputRequest;
      
      if (coreComponent) {
        setComponent(() => coreComponent);
      } else {
        setError({
          type: 'component_not_found',
          workflow,
          component,
          message: loadError.message
        });
      }
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    loadWorkflowComponent(sourceWorkflowName, componentType);
  }, [sourceWorkflowName, componentType, loadWorkflowComponent]);

  if (isLoading) {
    return <LoadingIndicator />;
  }

  if (error) {
    return <ErrorDisplay error={error} />;
  }

  return <Component payload={payload} onResponse={onResponse} />;
};
```

**Component Cache:**

```javascript
const componentCache = new Map();

// Cache key structure:
// "{chat_id}:{cache_seed}:{workflow_name}:{component_name}"

// Example:
// "chat_abc123:12345:Generator:ActionPlan"
```

**Why cache_seed?**
- Prevents stale components when workflow changes mid-chat
- Backend increments `cache_seed` on workflow updates
- Frontend invalidates cache when `cache_seed` changes
- Ensures UI components match backend workflow version

**Dynamic Import Pattern:**

```javascript
// Runtime dynamic import (no build-time bundling)
const workflowModule = await import(`../workflows/${workflow}/components/index.js`);

// Works for any workflow directory:
// workflows/Generator/components/index.js
// workflows/MarketingAutomation/components/index.js
// workflows/DataAnalysis/components/index.js
```

### Workflow Registry

**Purpose:** Fetch workflow metadata from backend API (single source of truth).

**Structure:**

```javascript
class WorkflowRegistry {
  constructor() {
    this.loadedWorkflows = new Map();
    this.apiBaseUrl = 'http://localhost:8000/api';
    this.cacheKey = 'mozaiks_workflows_cache_v1';
  }

  async initializeWorkflows({ allowCacheFallback = true } = {}) {
    try {
      // Fetch from backend
      const json = await this._fetchWithRetries();
      
      if (json.workflows && Array.isArray(json.workflows)) {
        json.workflows.forEach(wf => {
          this.loadedWorkflows.set(wf.name, wf);
        });
        
        // Save to cache
        this._saveToCache();
        this.initialized = true;
        this.ready = true;

        console.log(`✅ Loaded ${json.workflows.length} workflows from API`);
      }
    } catch (error) {
      console.error('Failed to fetch workflows from API:', error);
      
      // Fallback to cache
      if (allowCacheFallback && this._loadFromCache()) {
        console.warn('Using cached workflows (offline mode)');
      } else {
        throw new Error('Workflow registry initialization failed');
      }
    }
  }

  getLoadedWorkflows() {
    return Array.from(this.loadedWorkflows.values());
  }

  getWorkflow(name) {
    return this.loadedWorkflows.get(name);
  }
}

const registry = new WorkflowRegistry();
export const initializeWorkflows = () => registry.initializeWorkflows();
export const getLoadedWorkflows = () => registry.getLoadedWorkflows();
export const getWorkflow = (name) => registry.getWorkflow(name);
```

**API Response Structure:**

```json
{
  "workflows": [
    {
      "name": "Generator",
      "display_name": "Workflow Generator",
      "description": "Create custom workflows from user prompts",
      "version": "1.0.0",
      "available": true,
      "components": ["ActionPlan", "AgentAPIKeyInput", "GenerateAndDownload"]
    }
  ]
}
```

**Cache Fallback:**

```javascript
_loadFromCache() {
  const raw = localStorage.getItem(this.cacheKey);
  if (!raw) return false;
  
  const parsed = JSON.parse(raw);
  if (Array.isArray(parsed.workflows)) {
    parsed.workflows.forEach(w => this.loadedWorkflows.set(w.name, w));
    return true;
  }
  
  return false;
}
```

Enables offline operation when backend is unreachable.

## Message Rendering

### ChatMessage Component

**Purpose:** Render individual messages with role-specific styling.

**Structure:**

```javascript
const ChatMessage = ({ message }) => {
  const { role, content, agent, type, component_type, payload } = message;

  // Text messages
  if (type === 'text') {
    return (
      <div className={`message message-${role}`}>
        {role === 'assistant' && <div className="agent-name">{agent}</div>}
        <div className="message-content">{content}</div>
      </div>
    );
  }

  // UI tool messages
  if (type === 'ui_tool') {
    return (
      <div className="message message-tool">
        <WorkflowUIRouter
          payload={payload}
          onResponse={handleToolResponse}
          ui_tool_id={message.tool_name}
          eventId={message.id}
        />
      </div>
    );
  }

  // System messages
  if (type === 'error' || type === 'system') {
    return (
      <div className="message message-system">
        <div className="system-content">{content}</div>
      </div>
    );
  }

  return null;
};
```

**Role-Based Styling:**

```css
.message-user {
  background: var(--color-user-message);
  align-self: flex-end;
  border-radius: 12px 12px 0 12px;
}

.message-assistant {
  background: var(--color-assistant-message);
  align-self: flex-start;
  border-radius: 12px 12px 12px 0;
}

.message-tool {
  background: var(--color-tool-background);
  border: 1px solid var(--color-tool-border);
}

.message-system {
  background: var(--color-system-message);
  text-align: center;
  font-style: italic;
}
```

### ArtifactPanel Component

**Purpose:** Side panel for artifact-mode UI tool components.

**Behavior:**
- Opens when `mode: "artifact"` tool is invoked
- Displays single artifact at a time (replaces previous)
- Auto-collapses when new inline message arrives
- Provides close button for manual dismissal

**Structure:**

```javascript
const ArtifactPanel = ({ 
  isOpen, 
  onClose, 
  artifactMessages, 
  submitInputRequest 
}) => {
  if (!isOpen || artifactMessages.length === 0) {
    return null;
  }

  const currentArtifact = artifactMessages[0];  // Show most recent

  return (
    <div className={`artifact-panel ${isOpen ? 'open' : 'closed'}`}>
      <div className="artifact-header">
        <h3>{currentArtifact.component_type}</h3>
        <button onClick={onClose}>✕</button>
      </div>
      
      <div className="artifact-content">
        <WorkflowUIRouter
          payload={currentArtifact.payload}
          onResponse={submitInputRequest}
          ui_tool_id={currentArtifact.tool_name}
          eventId={currentArtifact.id}
        />
      </div>
    </div>
  );
};
```

**Auto-Collapse Logic:**

```javascript
// In ChatPage.js
const handleTextMessage = (data) => {
  // ... add message ...

  // Auto-collapse artifact panel on new text message
  if (isSidePanelOpen && lastArtifactEventRef.current) {
    console.log('Auto-collapsing artifact panel for new text message');
    setIsSidePanelOpen(false);
  }
};
```

## State Persistence

### LocalStorage Keys

```javascript
// Chat session
'mozaiks.current_chat_id'                                    // Current active chat ID
'mozaiks.current_chat_id.cache_seed.{chat_id}'              // Cache seed per chat

// WebSocket resume
'ws_idx_{chat_id}'                                           // Last sequence number per chat

// Workflow registry
'mozaiks_workflows_cache_v1'                                 // Cached workflow metadata

// Debug flags
'mozaiks.debug_all_agents'                                   // Enable agent output logging
'mozaiks.debug_pipeline'                                     // Enable pipeline logging
```

### Session Resume

**Flow:**

```
1. Page refresh
   ↓
2. ChatPage reads 'mozaiks.current_chat_id' from localStorage
   ↓
3. ChatPage loads cache_seed for that chat_id
   ↓
4. WebSocket connects with stored chat_id
   ↓
5. WebSocketApiAdapter reads 'ws_idx_{chat_id}' (last sequence)
   ↓
6. Sends client.resume with lastClientIndex
   ↓
7. Backend replays missed events (seq > lastClientIndex)
   ↓
8. chat.resume_boundary marks end of replay
   ↓
9. Normal event processing continues
```

**Persistence Guarantees:**
- Chat ID persists across page refreshes
- Sequence tracking prevents duplicate event processing
- Cache seed ensures UI components match backend workflow version
- Workflow cache enables offline operation

## Best Practices

### Component Development

1. **Use Context for Global State:** Access `useChatUI()` instead of prop drilling
2. **Handle Loading States:** Show spinners during async operations
3. **Error Boundaries:** Wrap dynamic imports in try/catch with fallbacks
4. **Cache Awareness:** Include `cache_seed` in cache keys for dynamic components
5. **WebSocket Safety:** Check `ws.readyState` before sending messages

### Event Handling

1. **Namespace Events:** Only process `chat.*` events; ignore others
2. **Extract Agent Names:** Use `extractAgentName(data)` for nested structures
3. **Respect ui_hidden:** Skip rendering messages with `ui_hidden: true`
4. **Correlate Events:** Use `corr` field to link tool_call → tool_response
5. **Sequence Tracking:** Persist `lastClientIndex` for resume capability

### State Management

1. **Use Refs for Race Guards:** Prevent duplicate connections/spinners with refs
2. **Mirror State in Refs:** Access latest state in callbacks via `useRef`
3. **Batch Updates:** Use functional setState to avoid stale closures
4. **Cleanup Effects:** Return cleanup functions from useEffect
5. **Debounce User Input:** Prevent rapid-fire message sends

### Performance

1. **Lazy Load Components:** Use dynamic imports for workflow UI components
2. **Cache Components:** Store loaded components in Map with cache_seed scoping
3. **Memoize Callbacks:** Use `useCallback` for event handlers
4. **Virtualize Long Lists:** Use react-window for 100+ messages
5. **Throttle Logs:** Disable verbose logging in production

## Debugging

### Enable Debug Logging

```javascript
// In browser console or localStorage
localStorage.setItem('mozaiks.debug_all_agents', 'true');
localStorage.setItem('mozaiks.debug_pipeline', 'true');

// Reload page
location.reload();
```

**Log Output:**

```
🛰️ [INCOMING] {agent: "ContextAgent", content: "...", type: "chat.text"}
🔧 Initializing workflow registry...
✅ Workflow registry initialized
🛠️ [WS-CONN] WebSocket workflow resolution: {provided: "Generator", actual: "Generator"}
🔗 Connecting to WebSocket: ws://localhost:8000/ws/Generator/...
📡 Sending client.resume with lastClientIndex: 42
✅ Resume completed: 5 events replayed
🛰️ WorkflowUIRouter: Loading component {workflow: "Generator", component: "ActionPlan"}
✅ WorkflowUIRouter: Loaded Generator:ActionPlan
```

### Common Issues

**WebSocket Not Connecting:**

**Symptom:** Connection status stuck on "disconnected"

**Checks:**
1. Backend running on expected port (default: 8000)
2. WebSocket URL correct in config (`ws://localhost:8000`)
3. App ID and user ID valid
4. Chat ID generated via `/api/chat/start`
5. Check browser console for WebSocket errors

**Fix:**

```javascript
// Check config
console.log(config.get('api'));
// Expected: { wsUrl: "ws://localhost:8000", baseUrl: "http://localhost:8000" }

// Verify connection
const { api } = useChatUI();
console.log(api.config);
```

**Component Not Loading:**

**Symptom:** "Component not found" error in WorkflowUIRouter

**Checks:**
1. Component exists at `workflows/{workflow}/components/{ComponentName}.js`
2. Component exported in `workflows/{workflow}/components/index.js`
3. `component_type` matches export name (case-sensitive)
4. No syntax errors in component file

**Fix:**

```javascript
// Check exports
import * as Components from '../workflows/Generator/components';
console.log(Object.keys(Components));
// Expected: ["ActionPlan", "AgentAPIKeyInput", "GenerateAndDownload"]
```

**Messages Not Rendering:**

**Symptom:** Events logged but no messages appear in chat

**Checks:**
1. Event type is `chat.text` or `chat.tool_call`
2. Message not marked `ui_hidden: true`
3. Content not empty string
4. Messages state updating (check React DevTools)

**Fix:**

```javascript
// Log message state
console.log('Messages:', messages);

// Check event handler
const handleTextMessage = (data) => {
  console.log('Processing text message:', data);
  // ... verify setMessages called ...
};
```

## Next Steps

- **[UI Components Reference](./ui_components.md)**: Workflow component patterns and contracts
- **[Workflow Integration](./workflow_integration.md)**: Component registration and dynamic loading
- **[WebSocket Streaming](../runtime/transport_and_streaming.md)**: Backend transport layer
- **[Event Pipeline](../runtime/event_pipeline.md)**: Event routing and correlation
- **[UI Tool Pipeline](../workflows/ui_tool_pipeline.md)**: Complete agent-to-frontend flow
