# Manual Workflow Switching (UI-Driven Multi-Workflow Sessions)

## Overview

Users can start/pause/resume multiple workflows within a single WebSocket connection using **UI buttons** (no LLM intent detection). This enables fluid navigation between workflows (Generator, Investor, Marketing, etc.) and "Ask Mozaiks" general Q&A mode.

---

## Architecture

### Key Components

1. **SessionRegistry** (`core/transport/session_registry.py`)
   - Tracks active/paused workflows per WebSocket connection
   - Enforces "one active workflow at a time" rule
   - Supports "Ask Mozaiks" mode (all workflows paused)

2. **WebSocket Event Handlers** (`core/transport/simple_transport.py`)
   - `chat.start_workflow` - Start new workflow from UI button
   - `chat.switch_workflow` - Switch to existing workflow tab
   - `chat.enter_general_mode` - Pause all, enter general Q&A

3. **Session List Endpoint** (`shared_app.py`)
   - `GET /api/sessions/list/{app_id}/{user_id}`
   - Returns all IN_PROGRESS sessions for UI tab rendering

---

## User Flow Examples

### Scenario 1: Mid-Generator → Investor Portal

```
User: Building app in Generator workflow (50% complete)
  └─ Status: IN_PROGRESS, chat_id: "chat_gen_123"

User clicks "Investor Portal" button
  ├─ Frontend sends: { type: "chat.start_workflow", workflow_name: "Investor" }
  ├─ Backend: Pauses Generator, creates chat_inv_456
  ├─ Backend sends: { type: "chat.workflow_started", chat_id: "chat_inv_456" }
  └─ Frontend: Renders new "Investor ▶️" tab, "Generator ⏸️" tab

User invests in an app, then clicks "Generator ⏸️" tab
  ├─ Frontend sends: { type: "chat.switch_workflow", chat_id: "chat_gen_123" }
  ├─ Backend: Pauses Investor, resumes Generator
  ├─ Backend sends: { type: "chat.context_switched", to_chat_id: "chat_gen_123" }
  └─ Frontend: Shows "Generator ▶️", "Investor ⏸️", restores artifact at 50%
```

### Scenario 2: "Ask Mozaiks" Mode

```
User: In middle of Generator workflow
  └─ Status: active

User clicks "Ask Mozaiks" button
  ├─ Frontend sends: { type: "chat.enter_general_mode" }
  ├─ Backend: Pauses Generator
  ├─ Backend sends: { type: "chat.mode_changed", mode: "general" }
  └─ Frontend: Shows "Ask Mozaiks ▶️", "Generator ⏸️"

User: "What's the weather in SF?"
  └─ Routed to general Q&A (no workflow active)

User clicks "Generator ⏸️" tab
  └─ Resumes Generator workflow (same as Scenario 1 tab switch)
```

---

## Frontend Integration

### 1. Session Tab Component

```tsx
// src/components/chat/SessionTabs.tsx

import { useEffect, useState } from 'react';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';

export function SessionTabs({ ws, appId, userId }) {
  const [sessions, setSessions] = useState([]);
  const [activeTab, setActiveTab] = useState('general');

  // Fetch user's IN_PROGRESS sessions on mount
  useEffect(() => {
    fetch(`/api/sessions/list/${appId}/${userId}`)
      .then(res => res.json())
      .then(data => {
        setSessions(data.sessions);
        if (data.sessions.length > 0) {
          setActiveTab(data.sessions[0].chat_id);
        }
      });
  }, [appId, userId]);

  // Listen for backend events (new workflow, context switch, mode change)
  useEffect(() => {
    if (!ws) return;

    const handleMessage = (event) => {
      const data = JSON.parse(event.data);

      if (data.type === 'chat.workflow_started') {
        // New workflow created, add tab
        setSessions(prev => [...prev, {
          chat_id: data.data.chat_id,
          workflow_name: data.data.workflow_name,
          status: 'active'
        }]);
        setActiveTab(data.data.chat_id);
      }

      if (data.type === 'chat.context_switched') {
        // User switched tabs, update active state
        setActiveTab(data.data.to_chat_id);
      }

      if (data.type === 'chat.mode_changed' && data.data.mode === 'general') {
        setActiveTab('general');
      }
    };

    ws.addEventListener('message', handleMessage);
    return () => ws.removeEventListener('message', handleMessage);
  }, [ws]);

  const handleTabClick = (chatId) => {
    if (chatId === 'general') {
      ws?.send(JSON.stringify({ type: 'chat.enter_general_mode' }));
    } else {
      ws?.send(JSON.stringify({
        type: 'chat.switch_workflow',
        chat_id: chatId
      }));
    }
  };

  return (
    <Tabs value={activeTab} onValueChange={handleTabClick}>
      <TabsList>
        <TabsTrigger value="general">Ask Mozaiks</TabsTrigger>
        {sessions.map(session => (
          <TabsTrigger key={session.chat_id} value={session.chat_id}>
            {session.workflow_name} {session.status === 'paused' ? '⏸️' : '▶️'}
          </TabsTrigger>
        ))}
      </TabsList>
    </Tabs>
  );
}
```

### 2. Start Workflow Button

```tsx
// src/components/portals/InvestorPortalButton.tsx

export function InvestorPortalButton({ ws }) {
  const handleClick = () => {
    ws?.send(JSON.stringify({
      type: 'chat.start_workflow',
      workflow_name: 'Investor'
    }));
  };

  return (
    <button onClick={handleClick}>
      Open Investor Portal
    </button>
  );
}
```

---

## Backend Event Reference

### Inbound (Frontend → Backend)

#### `chat.start_workflow`
Starts a new workflow from UI button.

```json
{
  "type": "chat.start_workflow",
  "workflow_name": "Investor"
}
```

**Backend Response:**
```json
{
  "type": "chat.workflow_started",
  "data": {
    "chat_id": "chat_inv_abc123",
    "workflow_name": "Investor",
    "app_id": "ent_001",
    "user_id": "user_456"
  }
}
```

#### `chat.switch_workflow`
Switches to existing workflow tab (pauses current, resumes target).

```json
{
  "type": "chat.switch_workflow",
  "chat_id": "chat_gen_123"
}
```

**Backend Response:**
```json
{
  "type": "chat.context_switched",
  "data": {
    "from_chat_id": "chat_inv_abc",
    "to_chat_id": "chat_gen_123",
    "workflow_name": "Generator",
    "artifact_id": "artifact_xyz",
    "app_id": "ent_001"
  }
}
```

#### `chat.enter_general_mode`
Pauses all workflows, enters general Q&A.

```json
{
  "type": "chat.enter_general_mode"
}
```

**Backend Response:**
```json
{
  "type": "chat.mode_changed",
  "data": {
    "mode": "general",
    "message": "Ask me anything! Workflows paused."
  }
}
```

### Outbound (Backend → Frontend)

#### `chat.workflow_started`
New workflow created and activated.

#### `chat.context_switched`
User switched between workflows.

#### `chat.mode_changed`
Entered/exited general mode.

#### `chat.error`
Workflow operation failed (e.g., switch to non-existent chat).

---

## Implementation Status

✅ **Completed:**
- `SessionRegistry` for tracking workflows per WebSocket
- WebSocket event handlers for manual switching
- `/api/sessions/list` endpoint for UI tab rendering
- Multi-workflow session support in `shared_app.py`

⚠️ **Not Implemented (Future):**
- LLM-based intent detection (classify user messages to auto-switch)
- Portal-level route guards (prevent accessing portals without prerequisites)
- Persistent session state across browser refreshes (requires localStorage sync)

---

## Differences from LLM Intent Detection (Future)

| Feature | Manual (Current) | Auto (Future) |
|---------|------------------|---------------|
| **Trigger** | UI button clicks | LLM classifies user messages |
| **User Flow** | Explicit ("Open Investor Portal") | Natural language ("I want to invest") |
| **Complexity** | Low (simple event routing) | High (requires LLM per message) |
| **Reliability** | 100% (user controls) | Depends on LLM accuracy |
| **Use Case** | Initial MVP, clear portals | Fluid conversational UX |

---

## Next Steps

1. **Frontend Implementation:**
   - Build `SessionTabs` component
   - Add "Start Workflow" buttons to portals
   - Wire up WebSocket event listeners

2. **Portal Guards (Optional):**
   - Add route-level checks (e.g., `/marketing` requires Generator complete)
   - Use `dependencies.py` logic for prerequisites (or replace with portal access checks)

3. **Testing:**
   - Start Generator, switch to Investor, return to Generator
   - Verify artifact state preserved across switches
   - Test "Ask Mozaiks" mode pause/resume

4. **Future Enhancement:**
   - Add LLM intent detection for auto-switching
   - Implement persistent session registry (survive server restarts)
   - Add session metadata to UI (last active time, progress indicators)
