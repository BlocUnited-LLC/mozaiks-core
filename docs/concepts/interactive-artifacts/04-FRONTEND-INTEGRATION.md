# Frontend Integration Guide

This guide shows you how to integrate Interactive Artifacts into your React frontend.

---

## 🎯 Overview

The frontend needs to:
1. **Send artifact actions** to backend (launch workflows, update state)
2. **Handle navigation events** from backend (switch between workflows)
3. **Handle dependency blocks** when prerequisites not met
4. **Update artifact UI** when state changes
5. **Provide session switcher** (like browser tabs)

---

## 🚀 Quick Start

### Step 1: Create WebSocket Utility Function

```typescript
// src/utils/websocket.ts

export interface ArtifactAction {
  action: 'launch_workflow' | 'update_state' | string;
  artifact_id?: string;
  payload?: Record<string, any>;
}

export function sendArtifactAction(
  ws: WebSocket,
  action: ArtifactAction,
  chatId: string,
  correlationId?: string
) {
  if (ws.readyState !== WebSocket.OPEN) {
    console.error('❌ WebSocket not connected');
    return;
  }

  const message = {
    type: 'chat.artifact_action',
    data: action,
    chat_id: chatId,
    correlation_id: correlationId || `req_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
    timestamp: new Date().toISOString()
  };

  ws.send(JSON.stringify(message));
}
```

---

### Step 2: Handle Backend Events

```typescript
// src/hooks/useArtifactEvents.ts

import { useEffect } from 'react';

export interface NavigateEvent {
  type: 'chat.navigate';
  data: {
    chat_id: string;
    workflow_name: string;
    artifact_instance_id: string;
    app_id: string;
  };
}

export interface DependencyBlockedEvent {
  type: 'chat.dependency_blocked';
  data: {
    workflow_name: string;
    message: string;
    error_code: string;
  };
}

export interface ArtifactStateUpdatedEvent {
  type: 'artifact.state.updated';
  data: {
    artifact_id: string;
    state_delta: Record<string, any>;
  };
}

export function useArtifactEvents(
  ws: WebSocket | null,
  onNavigate: (event: NavigateEvent) => void,
  onDependencyBlocked: (event: DependencyBlockedEvent) => void,
  onStateUpdated: (event: ArtifactStateUpdatedEvent) => void
) {
  useEffect(() => {
    if (!ws) return;

    const handleMessage = (event: MessageEvent) => {
      try {
        const message = JSON.parse(event.data);

        switch (message.type) {
          case 'chat.navigate':
            onNavigate(message);
            break;

          case 'chat.dependency_blocked':
            onDependencyBlocked(message);
            break;

          case 'artifact.state.updated':
            onStateUpdated(message);
            break;

          default:
            // Handle other message types (chat messages, etc.)
            break;
        }
      } catch (error) {
        console.error('Failed to parse WebSocket message:', error);
      }
    };

    ws.addEventListener('message', handleMessage);

    return () => {
      ws.removeEventListener('message', handleMessage);
    };
  }, [ws, onNavigate, onDependencyBlocked, onStateUpdated]);
}
```

---

### Step 3: Create Artifact Component

```tsx
// src/components/artifacts/AppBuilderArtifact.tsx

import React from 'react';
import { sendArtifactAction } from '@/utils/websocket';

interface AppBuilderState {
  app_name: string;
  architecture: string | null;
  features: string[];
  build_progress: number;
  deployment_status: string;
  revenue_to_date: number;
  buttons?: Array<{
    label: string;
    action: string;
    workflow?: string;
  }>;
}

interface AppBuilderArtifactProps {
  artifactId: string;
  chatId: string;
  state: AppBuilderState;
  ws: WebSocket | null;
}

export function AppBuilderArtifact({
  artifactId,
  chatId,
  state,
  ws
}: AppBuilderArtifactProps) {
  const handleLaunchWorkflow = (workflowName: string) => {
    if (!ws) return;

    sendArtifactAction(
      ws,
      {
        action: 'launch_workflow',
        payload: {
          workflow_name: workflowName,
          artifact_type: workflowName === 'RevenueDashboard' 
            ? 'RevenueDashboard' 
            : 'MarketingDashboard'
        }
      },
      chatId
    );
  };

  const handleDeployApp = () => {
    if (!ws) return;

    sendArtifactAction(
      ws,
      {
        action: 'deploy_app',
        artifact_id: artifactId,
        payload: {
          environment: 'production'
        }
      },
      chatId
    );
  };

  return (
    <div className="artifact-container">
      <div className="artifact-header">
        <h2>{state.app_name}</h2>
        <span className="status-badge">{state.deployment_status}</span>
      </div>

      <div className="artifact-body">
        {/* Build Progress */}
        <div className="progress-section">
          <label>Build Progress</label>
          <div className="progress-bar">
            <div 
              className="progress-fill" 
              style={{ width: `${state.build_progress}%` }}
            />
          </div>
          <span>{state.build_progress}%</span>
        </div>

        {/* Architecture */}
        {state.architecture && (
          <div className="architecture-section">
            <label>Architecture</label>
            <p>{state.architecture}</p>
          </div>
        )}

        {/* Features */}
        <div className="features-section">
          <label>Features</label>
          <ul>
            {state.features.map((feature, idx) => (
              <li key={idx}>✅ {feature}</li>
            ))}
          </ul>
        </div>

        {/* Revenue */}
        <div className="revenue-section">
          <label>Revenue to Date</label>
          <p className="revenue-amount">${state.revenue_to_date.toFixed(2)}</p>
        </div>

        {/* Action Buttons */}
        <div className="actions">
          {state.buttons?.map((button, idx) => (
            <button
              key={idx}
              onClick={() => {
                if (button.action === 'launch_workflow' && button.workflow) {
                  handleLaunchWorkflow(button.workflow);
                } else if (button.action === 'deploy_app') {
                  handleDeployApp();
                }
              }}
              className="action-button"
            >
              {button.label}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
```

---

### Step 4: Create Main Chat Component with Navigation

```tsx
// src/components/chat/ChatWithArtifacts.tsx

import React, { useState, useEffect } from 'react';
import { useArtifactEvents } from '@/hooks/useArtifactEvents';
import { AppBuilderArtifact } from '@/components/artifacts/AppBuilderArtifact';
import { RevenueDashboard } from '@/components/artifacts/RevenueDashboard';
import { toast } from 'sonner'; // or your toast library

interface Session {
  chatId: string;
  workflowName: string;
  artifactId: string;
  artifactType: string;
  artifactState: any;
}

export function ChatWithArtifacts() {
  const [ws, setWs] = useState<WebSocket | null>(null);
  const [sessions, setSessions] = useState<Session[]>([]);
  const [activeSessionIndex, setActiveSessionIndex] = useState(0);

  // Handle navigation events
  const handleNavigate = (event: NavigateEvent) => {
    const { chat_id, workflow_name, artifact_instance_id } = event.data;

    // Check if session already exists
    const existingIndex = sessions.findIndex(s => s.chatId === chat_id);

    if (existingIndex !== -1) {
      // Switch to existing session
      setActiveSessionIndex(existingIndex);
    } else {
      // Create new session tab
      const newSession: Session = {
        chatId: chat_id,
        workflowName: workflow_name,
        artifactId: artifact_instance_id,
        artifactType: inferArtifactType(workflow_name),
        artifactState: {}
      };

      setSessions(prev => [...prev, newSession]);
      setActiveSessionIndex(sessions.length); // Switch to new session

      // Reconnect WebSocket to new chat_id
      reconnectWebSocket(chat_id);
    }

    toast.success(`Navigated to ${workflow_name}`);
  };

  // Handle dependency blocked
  const handleDependencyBlocked = (event: DependencyBlockedEvent) => {
    const { workflow_name, message } = event.data;

    toast.error(message, {
      description: `Cannot launch ${workflow_name}`,
      duration: 5000
    });
  };

  // Handle artifact state updates
  const handleStateUpdated = (event: ArtifactStateUpdatedEvent) => {
    const { artifact_id, state_delta } = event.data;

    setSessions(prev =>
      prev.map(session =>
        session.artifactId === artifact_id
          ? {
              ...session,
              artifactState: { ...session.artifactState, ...state_delta }
            }
          : session
      )
    );
  };

  // Use artifact events hook
  useArtifactEvents(
    ws,
    handleNavigate,
    handleDependencyBlocked,
    handleStateUpdated
  );

  // WebSocket connection
  useEffect(() => {
    const websocket = new WebSocket('ws://localhost:8000/ws');

    websocket.onopen = () => {
      console.log('✅ WebSocket connected');
      setWs(websocket);
    };

    websocket.onerror = (error) => {
      console.error('❌ WebSocket error:', error);
    };

    websocket.onclose = () => {
      console.log('🔌 WebSocket disconnected');
      setWs(null);
    };

    return () => {
      websocket.close();
    };
  }, []);

  const reconnectWebSocket = (chatId: string) => {
    if (ws) {
      ws.close();
    }

    const newWs = new WebSocket(`ws://localhost:8000/ws?chat_id=${chatId}`);
    newWs.onopen = () => setWs(newWs);
  };

  const inferArtifactType = (workflowName: string): string => {
    const mapping: Record<string, string> = {
      'AppBuilder': 'AppBuilderArtifact',
      'RevenueDashboard': 'RevenueDashboard',
      'InvestmentMarketplace': 'InvestmentMarketplace',
      'MarketingAutomation': 'MarketingDashboard',
      'ChallengeTracker': 'ChallengeTracker'
    };
    return mapping[workflowName] || 'GenericArtifact';
  };

  const activeSession = sessions[activeSessionIndex];

  return (
    <div className="chat-container">
      {/* Session Tabs (like browser tabs) */}
      <div className="session-tabs">
        {sessions.map((session, index) => (
          <button
            key={session.chatId}
            className={`session-tab ${index === activeSessionIndex ? 'active' : ''}`}
            onClick={() => setActiveSessionIndex(index)}
          >
            {session.workflowName}
            <button
              className="close-tab"
              onClick={(e) => {
                e.stopPropagation();
                setSessions(prev => prev.filter((_, i) => i !== index));
                if (activeSessionIndex >= sessions.length - 1) {
                  setActiveSessionIndex(Math.max(0, sessions.length - 2));
                }
              }}
            >
              ×
            </button>
          </button>
        ))}
      </div>

      {/* Active Session Content */}
      {activeSession && (
        <div className="session-content">
          {/* Chat Messages */}
          <div className="chat-messages">
            {/* Render chat messages here */}
          </div>

          {/* Artifact Display */}
          <div className="artifact-display">
            {activeSession.artifactType === 'AppBuilderArtifact' && (
              <AppBuilderArtifact
                artifactId={activeSession.artifactId}
                chatId={activeSession.chatId}
                state={activeSession.artifactState}
                ws={ws}
              />
            )}

            {activeSession.artifactType === 'RevenueDashboard' && (
              <RevenueDashboard
                artifactId={activeSession.artifactId}
                chatId={activeSession.chatId}
                state={activeSession.artifactState}
                ws={ws}
              />
            )}

            {/* Add other artifact types */}
          </div>
        </div>
      )}
    </div>
  );
}
```

---

## 🎨 Component Examples

### Revenue Dashboard Artifact

```tsx
// src/components/artifacts/RevenueDashboard.tsx

import React from 'react';
import { sendArtifactAction } from '@/utils/websocket';

interface RevenueDashboardState {
  total_revenue: number;
  apps: Array<{
    app_id: string;
    app_name: string;
    revenue: number;
    costs: number;
    roi: number;
  }>;
  last_updated: number;
  chart_data?: any;
}

interface RevenueDashboardProps {
  artifactId: string;
  chatId: string;
  state: RevenueDashboardState;
  ws: WebSocket | null;
}

export function RevenueDashboard({
  artifactId,
  chatId,
  state,
  ws
}: RevenueDashboardProps) {
  const handleViewAppDetails = (appId: string) => {
    if (!ws) return;

    sendArtifactAction(
      ws,
      {
        action: 'launch_workflow',
        payload: {
          workflow_name: 'AppBuilder',
          artifact_type: 'AppBuilderArtifact',
          app_id: appId
        }
      },
      chatId
    );
  };

  return (
    <div className="artifact-container">
      <div className="artifact-header">
        <h2>Revenue Dashboard</h2>
        <span className="last-updated">
          Updated {new Date(state.last_updated * 1000).toLocaleTimeString()}
        </span>
      </div>

      <div className="artifact-body">
        {/* Total Revenue */}
        <div className="total-revenue-card">
          <label>Total Revenue</label>
          <h1 className="revenue-amount">${state.total_revenue.toFixed(2)}</h1>
        </div>

        {/* Apps Table */}
        <div className="apps-table">
          <table>
            <thead>
              <tr>
                <th>App Name</th>
                <th>Revenue</th>
                <th>Costs</th>
                <th>ROI</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {state.apps.map(app => (
                <tr key={app.app_id}>
                  <td>{app.app_name}</td>
                  <td className="revenue">${app.revenue.toFixed(2)}</td>
                  <td className="costs">${app.costs.toFixed(2)}</td>
                  <td className={app.roi >= 0 ? 'roi-positive' : 'roi-negative'}>
                    {app.roi.toFixed(1)}%
                  </td>
                  <td>
                    <button onClick={() => handleViewAppDetails(app.app_id)}>
                      View Details
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Chart */}
        {state.chart_data && (
          <div className="chart-container">
            {/* Render your chart here (e.g., Recharts, Chart.js) */}
          </div>
        )}
      </div>
    </div>
  );
}
```

---

### Investment Marketplace Artifact

```tsx
// src/components/artifacts/InvestmentMarketplace.tsx

import React, { useState } from 'react';
import { sendArtifactAction } from '@/utils/websocket';

interface InvestmentMarketplaceState {
  apps: Array<{
    app_id: string;
    app_name: string;
    creator: string;
    revenue: number;
    asking_price: number;
    category: string;
  }>;
  filters: {
    category: string;
    min_revenue: number;
  };
  selected_app: string | null;
  investment_amount: number;
}

interface InvestmentMarketplaceProps {
  artifactId: string;
  chatId: string;
  state: InvestmentMarketplaceState;
  ws: WebSocket | null;
}

export function InvestmentMarketplace({
  artifactId,
  chatId,
  state,
  ws
}: InvestmentMarketplaceProps) {
  const [selectedApp, setSelectedApp] = useState<string | null>(null);
  const [investmentAmount, setInvestmentAmount] = useState<number>(100);

  const handleInvest = (appId: string) => {
    if (!ws) return;

    sendArtifactAction(
      ws,
      {
        action: 'invest_in_app',
        artifact_id: artifactId,
        payload: {
          app_id: appId,
          amount: investmentAmount
        }
      },
      chatId
    );

    // Update local state
    sendArtifactAction(
      ws,
      {
        action: 'update_state',
        artifact_id: artifactId,
        payload: {
          state_updates: {
            selected_app: appId,
            investment_amount: investmentAmount
          }
        }
      },
      chatId
    );
  };

  const handleFilterChange = (category: string) => {
    if (!ws) return;

    sendArtifactAction(
      ws,
      {
        action: 'update_state',
        artifact_id: artifactId,
        payload: {
          state_updates: {
            filters: { ...state.filters, category }
          }
        }
      },
      chatId
    );
  };

  return (
    <div className="artifact-container">
      <div className="artifact-header">
        <h2>Investment Marketplace</h2>
        <p>Browse and invest in apps built by others</p>
      </div>

      <div className="artifact-body">
        {/* Filters */}
        <div className="filters">
          <select 
            value={state.filters.category}
            onChange={(e) => handleFilterChange(e.target.value)}
          >
            <option value="all">All Categories</option>
            <option value="saas">SaaS</option>
            <option value="ecommerce">E-Commerce</option>
            <option value="marketplace">Marketplace</option>
          </select>
        </div>

        {/* Apps Grid */}
        <div className="apps-grid">
          {state.apps.map(app => (
            <div key={app.app_id} className="app-card">
              <h3>{app.app_name}</h3>
              <p className="creator">by {app.creator}</p>
              <div className="app-stats">
                <span>Revenue: ${app.revenue.toFixed(2)}</span>
                <span>Category: {app.category}</span>
              </div>
              <div className="invest-section">
                <input
                  type="number"
                  value={investmentAmount}
                  onChange={(e) => setInvestmentAmount(Number(e.target.value))}
                  min={10}
                  step={10}
                />
                <button onClick={() => handleInvest(app.app_id)}>
                  Invest ${investmentAmount}
                </button>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
```

---

## 🔧 State Management

### Option 1: React Context (Simple)

```tsx
// src/contexts/ArtifactContext.tsx

import React, { createContext, useContext, useState } from 'react';

interface ArtifactContextValue {
  artifacts: Record<string, any>; // artifact_id -> state
  updateArtifact: (artifactId: string, stateDelta: any) => void;
}

const ArtifactContext = createContext<ArtifactContextValue | null>(null);

export function ArtifactProvider({ children }: { children: React.ReactNode }) {
  const [artifacts, setArtifacts] = useState<Record<string, any>>({});

  const updateArtifact = (artifactId: string, stateDelta: any) => {
    setArtifacts(prev => ({
      ...prev,
      [artifactId]: { ...prev[artifactId], ...stateDelta }
    }));
  };

  return (
    <ArtifactContext.Provider value={{ artifacts, updateArtifact }}>
      {children}
    </ArtifactContext.Provider>
  );
}

export function useArtifacts() {
  const context = useContext(ArtifactContext);
  if (!context) throw new Error('useArtifacts must be used within ArtifactProvider');
  return context;
}
```

---

### Option 2: Zustand (Recommended)

```typescript
// src/stores/artifactStore.ts

import { create } from 'zustand';

interface Session {
  chatId: string;
  workflowName: string;
  artifactId: string;
  artifactType: string;
  artifactState: any;
}

interface ArtifactStore {
  sessions: Session[];
  activeSessionIndex: number;
  
  addSession: (session: Session) => void;
  removeSession: (index: number) => void;
  setActiveSession: (index: number) => void;
  updateArtifactState: (artifactId: string, stateDelta: any) => void;
}

export const useArtifactStore = create<ArtifactStore>((set) => ({
  sessions: [],
  activeSessionIndex: 0,
  
  addSession: (session) =>
    set((state) => ({
      sessions: [...state.sessions, session],
      activeSessionIndex: state.sessions.length
    })),
  
  removeSession: (index) =>
    set((state) => ({
      sessions: state.sessions.filter((_, i) => i !== index),
      activeSessionIndex: Math.max(0, Math.min(state.activeSessionIndex, state.sessions.length - 2))
    })),
  
  setActiveSession: (index) =>
    set({ activeSessionIndex: index }),
  
  updateArtifactState: (artifactId, stateDelta) =>
    set((state) => ({
      sessions: state.sessions.map((session) =>
        session.artifactId === artifactId
          ? { ...session, artifactState: { ...session.artifactState, ...stateDelta } }
          : session
      )
    }))
}));
```

**Usage**:

```tsx
import { useArtifactStore } from '@/stores/artifactStore';

export function ChatWithArtifacts() {
  const { sessions, activeSessionIndex, addSession, updateArtifactState } = useArtifactStore();

  const handleStateUpdated = (event: ArtifactStateUpdatedEvent) => {
    const { artifact_id, state_delta } = event.data;
    updateArtifactState(artifact_id, state_delta);
  };

  // (Return JSX with event subscriptions and component rendering)
}
```

---

## 🎯 Best Practices

### 1. Handle WebSocket Connection States

```tsx
enum ConnectionState {
  CONNECTING = 'connecting',
  CONNECTED = 'connected',
  DISCONNECTED = 'disconnected',
  ERROR = 'error'
}

export function useWebSocketConnection(url: string) {
  const [ws, setWs] = useState<WebSocket | null>(null);
  const [state, setState] = useState<ConnectionState>(ConnectionState.DISCONNECTED);

  useEffect(() => {
    setState(ConnectionState.CONNECTING);
    const websocket = new WebSocket(url);

    websocket.onopen = () => {
      setState(ConnectionState.CONNECTED);
      setWs(websocket);
    };

    websocket.onerror = () => {
      setState(ConnectionState.ERROR);
    };

    websocket.onclose = () => {
      setState(ConnectionState.DISCONNECTED);
      setWs(null);
    };

    return () => websocket.close();
  }, [url]);

  return { ws, state };
}
```

---

### 2. Debounce State Updates

```tsx
import { useDebouncedCallback } from 'use-debounce';

export function AppBuilderArtifact({ artifactId, chatId, state, ws }) {
  const debouncedUpdate = useDebouncedCallback(
    (updates: Record<string, any>) => {
      if (!ws) return;
      
      sendArtifactAction(
        ws,
        {
          action: 'update_state',
          artifact_id: artifactId,
          payload: { state_updates: updates }
        },
        chatId
      );
    },
    500 // Wait 500ms after last change
  );

  const handleFeatureChange = (feature: string, checked: boolean) => {
    const newFeatures = checked
      ? [...state.features, feature]
      : state.features.filter(f => f !== feature);
    
    // Debounced - won't spam backend
    debouncedUpdate({ features: newFeatures });
  };

  // (Return JSX rendering progress bar, features list, and action buttons)
}
```

---

### 3. Show Loading States

```tsx
export function AppBuilderArtifact({ artifactId, chatId, state, ws }) {
  const [isNavigating, setIsNavigating] = useState(false);

  const handleLaunchWorkflow = async (workflowName: string) => {
    setIsNavigating(true);
    
    sendArtifactAction(ws, {
      action: 'launch_workflow',
      payload: { workflow_name: workflowName }
    }, chatId);

    // Will be reset when navigation event arrives
  };

  return (
    <div className={isNavigating ? 'loading' : ''}>
      {/* (Render app details, progress indicators, etc.) */}
      <button 
        onClick={() => handleLaunchWorkflow('RevenueDashboard')}
        disabled={isNavigating}
      >
        {isNavigating ? 'Loading...' : 'View Revenue'}
      </button>
    </div>
  );
}
```

---

### 4. Handle Errors Gracefully

```tsx
export function useArtifactEvents(ws, onNavigate, onDependencyBlocked, onStateUpdated) {
  useEffect(() => {
    if (!ws) return;

    const handleMessage = (event: MessageEvent) => {
      try {
        const message = JSON.parse(event.data);

        switch (message.type) {
          case 'chat.navigate':
            onNavigate(message);
            break;

          case 'chat.dependency_blocked':
            onDependencyBlocked(message);
            break;

          case 'artifact.state.updated':
            onStateUpdated(message);
            break;

          case 'error':
            toast.error(message.data.message);
            break;

          default:
            console.warn('Unknown message type:', message.type);
        }
      } catch (error) {
        console.error('Failed to parse WebSocket message:', error);
        toast.error('Failed to process server message');
      }
    };

    ws.addEventListener('message', handleMessage);
    return () => ws.removeEventListener('message', handleMessage);
  }, [ws, onNavigate, onDependencyBlocked, onStateUpdated]);
}
```

---

## 🐛 Common Issues

### Issue 1: Artifact Not Updating After State Change

**Symptom**: You send `update_state` but UI doesn't reflect changes.

**Solutions**:
1. Check WebSocket is connected: `ws.readyState === WebSocket.OPEN`
2. Verify backend is broadcasting `artifact.state.updated` event
3. Check you're listening for the event in `useArtifactEvents`
4. Verify artifact_id matches between frontend and backend

**Debug**:
```tsx
ws.addEventListener('message', (event) => {
  console.log('📩 Received:', JSON.parse(event.data));
});

sendArtifactAction(ws, action, chatId);
console.log('📤 Sent:', action);
```

---

### Issue 2: Navigation Event Not Triggering

**Symptom**: User clicks "View Revenue" but nothing happens.

**Solutions**:
1. Check `sendArtifactAction` is being called
2. Verify backend dependency validation passed
3. Check for `chat.dependency_blocked` event in console
4. Ensure `onNavigate` callback is properly wired

---

### Issue 3: Multiple Tabs for Same Workflow

**Symptom**: User has 2+ tabs for "AppBuilder".

**Explanation**: This is intentional! Users can build multiple apps.

**Solution**: If you want to prevent this:
```tsx
const handleNavigate = (event: NavigateEvent) => {
  const { chat_id, workflow_name } = event.data;

  // Check if workflow already has a tab
  const existingIndex = sessions.findIndex(
    s => s.workflowName === workflow_name && s.chatId === chat_id
  );

  if (existingIndex !== -1) {
    setActiveSessionIndex(existingIndex);
    return;
  }

  // Create new tab
  // (Add session to state with new chat_id, workflow_name, artifact_id, etc.)
};
```

---

## 📚 Next Steps

Now that you understand frontend integration:
1. **Complete Examples** → [`05-EXAMPLES.md`](./05-EXAMPLES.md) - Full end-to-end code
2. **Troubleshooting** → [`06-TROUBLESHOOTING.md`](./06-TROUBLESHOOTING.md) - Debug common issues
3. **Backend Guide** → [`03-BACKEND-INTEGRATION.md`](./03-BACKEND-INTEGRATION.md) - Review backend patterns

---

## 🔗 Reference

**Key Functions**:
- `sendArtifactAction(ws, action, chatId)` - Send action to backend
- `useArtifactEvents(ws, callbacks)` - Listen for backend events
- `useArtifactStore()` - Zustand store for state management

**Event Types**:
- `chat.navigate` - Backend requests navigation to new workflow
- `chat.dependency_blocked` - Workflow blocked by prerequisites
- `artifact.state.updated` - Artifact state changed

**Component Pattern**:
- Each artifact type = separate React component
- Props: `artifactId`, `chatId`, `state`, `ws`
- Use `sendArtifactAction` for all backend communication
