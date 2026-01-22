# UI Components Reference

## Purpose

This document provides comprehensive patterns and contracts for creating workflow-specific UI components in MozaiksAI. It covers display modes (artifact/inline/fullscreen), props interfaces, event handling, styling patterns, and best practices for building interactive agent-driven UI tools.

## Overview

**UI Components** are React components that render in response to agent tool invocations. They serve as the **visual interface** between users and agents, collecting input, displaying information, and facilitating interactive workflows.

**Key Characteristics:**
- **Workflow-Scoped**: Each workflow has its own `components/` directory
- **Dynamically Loaded**: Components imported on-demand via `WorkflowUIRouter`
- **Event-Driven**: Receive `payload` from backend, send responses via `onResponse`
- **Mode-Aware**: Support `artifact`, `inline`, and `fullscreen` display modes
- **Stateful**: Manage local UI state independently of chat state

---

## Design System Quick Reference

All workflow components must import the shared artifact design tokens and avoid hand-crafted Tailwind strings for shared treatments.

```javascript
import { typography, components, spacing, layouts } from '../../../styles/artifactDesignSystem';
import { createToolsLogger } from '../../../core/toolsLogger';
```

| Element | Tokens | Notes |
|---------|--------|-------|
| Root container | `layouts.artifactContainer` | Always include `data-agent-message-id` for observability. |
| Typography | `typography.display|heading|body|label` | Orbitron for headings, Rajdhani for body, uppercase labels preset. |
| Cards & surfaces | `components.card.primary|secondary|ghost` | Use primary for hero panels, secondary for subsections, ghost for subtle groups. |
| Buttons | `components.button.primary|secondary|ghost` | Primary = cyan call-to-action, secondary = slate outline, ghost = low emphasis. |
| Badges | `components.badge.primary|success|warning|neutral` | Status colors map to cyan/emerald/amber/slate. |
| Icon wrappers | `components.iconContainer.primary|secondary|warning` | Required for lucide icons to apply glow + ring. |
| Spacing | `spacing.section|subsection|group|padding.*|gap.*` | Controls vertical rhythm and padding primitives. |

Rules of thumb:

- Never inline Orbitron/Rajdhani class strings; rely on tokens to keep typography consistent.
- Attach `createToolsLogger` to log `start/done/error` events for every user action (enables UI observability).
- Keep icons in `lucide-react`; common sizes: `h-4 w-4` (inline), `h-5 w-5` (artifact headers).
- Extend the design system module first whenever a new visual pattern is required.

---

## Component Types

### 1. Input Components

**Purpose:** Collect user input and send responses back to agents.

**Examples:**
- `AgentAPIKeyInput`: Secure API key collection
- `FormInput`: Multi-field form with validation
- `ConfirmationDialog`: Yes/No decision prompts

**Pattern:**

```javascript
const AgentAPIKeyInput = ({ 
  payload, 
  onResponse, 
  ui_tool_id, 
  eventId 
}) => {
  const [apiKey, setApiKey] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e) => {
    e.preventDefault();
    
    if (!apiKey.trim()) {
      setError('API key is required');
      return;
    }

    setIsSubmitting(true);
    
    try {
      await onResponse({
        status: 'success',
        action: 'submit',
        data: {
          service: payload.service,
          apiKey: apiKey.trim(),
          submissionTime: new Date().toISOString(),
          ui_tool_id,
          eventId
        }
      });
      
      setApiKey(''); // Clear after submit
    } catch (error) {
      setError('Submission failed. Please try again.');
      
      await onResponse({
        status: 'error',
        action: 'submit',
        error: error.message,
        data: { service: payload.service, ui_tool_id, eventId }
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="api-key-form">
      <h3>{payload.label || 'API Key Required'}</h3>
      <p>{payload.description}</p>
      
      <input
        type="password"
        value={apiKey}
        onChange={(e) => setApiKey(e.target.value)}
        placeholder={payload.placeholder}
        disabled={isSubmitting}
      />
      
      {error && <p className="error">{error}</p>}
      
      <button type="submit" disabled={isSubmitting}>
        {isSubmitting ? 'Submitting...' : 'Submit'}
      </button>
    </form>
  );
};
```

**Response Contract:**

```javascript
// Success response
{
  status: 'success',       // Required: 'success' | 'error' | 'cancelled'
  action: 'submit',        // Action identifier
  data: {                  // Tool-specific data
    [key]: value,
    ui_tool_id,           // Echo tool ID for correlation
    eventId               // Echo event ID for correlation
  }
}

// Error response
{
  status: 'error',
  action: 'submit',
  error: 'Error message',
  data: { ui_tool_id, eventId }
}

// Cancel response
{
  status: 'cancelled',
  action: 'cancel',
  data: { ui_tool_id, eventId }
}
```

### 2. Display Components

**Purpose:** Render read-only information for user review.

**Examples:**
- `ActionPlan`: Hierarchical workflow visualization
- `DataTable`: Tabular data display
- `Chart`: Data visualization
- `CodePreview`: Syntax-highlighted code

**Pattern:**

```javascript
const ActionPlan = ({ payload, onResponse, ui_tool_id, eventId }) => {
  const [openPhases, setOpenPhases] = useState({});
  const workflow = payload?.workflow || payload?.ActionPlan || {};

  const togglePhase = (index) => {
    setOpenPhases(prev => ({ ...prev, [index]: !prev[index] }));
  };

  const handleApprove = async () => {
    await onResponse({
      status: 'success',
      action: 'approve',
      data: {
        approved: true,
        approvalTime: new Date().toISOString(),
        ui_tool_id,
        eventId
      }
    });
  };

  const handleReject = async () => {
    await onResponse({
      status: 'success',
      action: 'reject',
      data: {
        approved: false,
        rejectionTime: new Date().toISOString(),
        ui_tool_id,
        eventId
      }
    });
  };

  return (
    <div className="action-plan-artifact">
      <header>
        <h2>{workflow.name || 'Workflow Plan'}</h2>
        <p>{workflow.description}</p>
      </header>

      <section className="phases">
        {workflow.phases?.map((phase, idx) => (
          <PhaseAccordion
            key={idx}
            phase={phase}
            index={idx}
            open={!!openPhases[idx]}
            onToggle={() => togglePhase(idx)}
          />
        ))}
      </section>

      <footer className="actions">
        <button onClick={handleReject} className="reject">
          Reject
        </button>
        <button onClick={handleApprove} className="approve">
          Approve Plan
        </button>
      </footer>
    </div>
  );
};
```

**Read-Only Pattern (No user action required):**

```javascript
const DataPreview = ({ payload }) => {
  // No onResponse - purely informational
  const data = payload?.data || [];

  return (
    <div className="data-preview">
      <h3>Data Preview</h3>
      <table>
        <thead>
          <tr>
            {Object.keys(data[0] || {}).map(key => (
              <th key={key}>{key}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {data.map((row, idx) => (
            <tr key={idx}>
              {Object.values(row).map((val, vIdx) => (
                <td key={vIdx}>{val}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
};
```

### 3. Interactive Components

**Purpose:** Complex multi-step interactions with state management.

**Examples:**
- `FileDownloadCenter`: File generation and download orchestration
- `WorkflowBuilder`: Drag-and-drop workflow designer
- `ChatInterface`: Nested chat for sub-workflows

**Pattern:**

```javascript
const FileDownloadCenter = ({ payload, onResponse, ui_tool_id, eventId }) => {
  const [status, setStatus] = useState('idle'); // idle | generating | ready | error
  const [files, setFiles] = useState([]);
  const [progress, setProgress] = useState(0);

  useEffect(() => {
    // Auto-start file generation on mount
    if (payload?.autoGenerate) {
      generateFiles();
    }
  }, []);

  const generateFiles = async () => {
    setStatus('generating');
    setProgress(0);

    try {
      // Simulate progress
      const interval = setInterval(() => {
        setProgress(prev => Math.min(prev + 10, 90));
      }, 200);

      // Request file generation from backend
      const response = await fetch('/api/workflow/generate', {
        method: 'POST',
        body: JSON.stringify({
          workflow_id: payload.workflow_id,
          format: payload.format || 'zip'
        })
      });

      clearInterval(interval);
      setProgress(100);

      const result = await response.json();
      setFiles(result.files || []);
      setStatus('ready');

      // Notify agent of successful generation
      await onResponse({
        status: 'success',
        action: 'generated',
        data: {
          file_count: result.files.length,
          total_size: result.total_size,
          ui_tool_id,
          eventId
        }
      });
    } catch (error) {
      console.error('File generation failed:', error);
      setStatus('error');

      await onResponse({
        status: 'error',
        action: 'generate',
        error: error.message,
        data: { ui_tool_id, eventId }
      });
    }
  };

  const handleDownload = (file) => {
    const link = document.createElement('a');
    link.href = file.download_url;
    link.download = file.name;
    link.click();
  };

  const handleComplete = async () => {
    await onResponse({
      status: 'success',
      action: 'complete',
      data: {
        downloaded: true,
        completionTime: new Date().toISOString(),
        ui_tool_id,
        eventId
      }
    });
  };

  return (
    <div className="file-download-center">
      <h2>File Generation</h2>

      {status === 'generating' && (
        <div className="progress-bar">
          <div className="progress-fill" style={{ width: `${progress}%` }} />
          <span>{progress}% Complete</span>
        </div>
      )}

      {status === 'ready' && (
        <div className="file-list">
          <h3>Generated Files ({files.length})</h3>
          {files.map((file, idx) => (
            <div key={idx} className="file-item">
              <span>{file.name}</span>
              <span>{file.size}</span>
              <button onClick={() => handleDownload(file)}>
                Download
              </button>
            </div>
          ))}
          <button onClick={handleComplete} className="complete">
            Done
          </button>
        </div>
      )}

      {status === 'error' && (
        <div className="error-state">
          <p>File generation failed</p>
          <button onClick={generateFiles}>Retry</button>
        </div>
      )}
    </div>
  );
};
```

## Props Contract

### Standard Props

Every UI component receives these standard props from `WorkflowUIRouter`:

```typescript
interface UIComponentProps {
  // REQUIRED PROPS
  
  payload: object;          // Tool-specific data from agent
                            // Structure defined by tool's Pydantic model
  
  onResponse: (response: ResponseObject) => Promise<void>;
                            // Function to send response back to agent
                            // Must be called when user completes interaction
  
  ui_tool_id: string;       // Tool identifier (e.g., "action_plan")
                            // Echo this in response data for correlation
  
  eventId: string;          // Unique event ID for this tool invocation
                            // Echo this in response data for correlation
  
  // OPTIONAL PROPS (context metadata)
  
  workflowName?: string;           // Source workflow name (e.g., "Generator")
  sourceWorkflowName?: string;     // Alias for workflowName
  generatedWorkflowName?: string;  // Generated user workflow name (for display)
  componentId?: string;            // Component identifier override
}
```

### Payload Structure

**Payload** contains tool-specific data defined by the tool's Pydantic model:

```javascript
// Example: ActionPlanCall model
payload = {
  workflow: {                    // Nested ActionPlan model
    name: "Marketing Automation",
    description: "Automate social media posting",
    phases: [
      {
        name: "Data Collection",
        description: "Gather content sources",
        agents: [
          {
            name: "ContentFetcher",
            description: "Fetch RSS feeds",
            connectedTools: [
              { name: "rss_reader", purpose: "Parse RSS feeds" }
            ],
            humanInLoop: false
          }
        ]
      }
    ],
    trigger: "scheduled",
    mermaidChart: "graph TD\n  A[Start]..."
  },
  agent_message: "Review and approve the workflow plan",
  ui_config: {
    mode: "artifact",
    component: "ActionPlan"
  }
}

// Example: AgentAPIKeyInput model
payload = {
  service: "openai",
  label: "OpenAI API Key",
  description: "Enter your OpenAI API key to continue",
  placeholder: "sk-...",
  required: true,
  maskInput: true,
  agent_message_id: "msg_abc123"
}
```

**Best Practice:** Access payload fields with defaults:

```javascript
const service = payload?.service || 'openai';
const required = payload?.required !== undefined ? payload.required : true;
```

### Response Object

**Response** sent via `onResponse()` must follow this structure:

```typescript
interface ResponseObject {
  status: 'success' | 'error' | 'cancelled';  // REQUIRED: Response status
  action: string;                              // REQUIRED: Action identifier
  data?: object;                               // OPTIONAL: Response data
  error?: string;                              // REQUIRED if status === 'error'
}
```

**Standard Response Patterns:**

```javascript
// Success with data
{
  status: 'success',
  action: 'submit',
  data: {
    apiKey: 'sk-...',
    service: 'openai',
    ui_tool_id: 'request_api_key',
    eventId: 'evt_abc123'
  }
}

// User approval
{
  status: 'success',
  action: 'approve',
  data: {
    approved: true,
    approvalTime: new Date().toISOString(),
    ui_tool_id: 'action_plan',
    eventId: 'evt_def456'
  }
}

// User cancellation
{
  status: 'cancelled',
  action: 'cancel',
  data: {
    cancelTime: new Date().toISOString(),
    ui_tool_id: 'action_plan',
    eventId: 'evt_def456'
  }
}

// Error (validation, network, etc.)
{
  status: 'error',
  action: 'submit',
  error: 'Invalid API key format',
  data: {
    ui_tool_id: 'request_api_key',
    eventId: 'evt_abc123'
  }
}
```

**Critical:** Always echo `ui_tool_id` and `eventId` in response data for backend correlation.

## Display Modes

### 1. Inline Mode

**Behavior:** Renders directly in chat message stream.

**Use Cases:**
- Short forms (1-3 fields)
- Simple confirmations
- Inline data displays

**Configuration (tools.json):**

```json
{
  "tool": "simple_form",
  "ui": {
    "mode": "inline",
    "component": "SimpleForm"
  }
}
```

**Styling Guidelines:**

```css
.inline-component {
  max-width: 600px;           /* Fit within chat width */
  padding: 1rem;
  border-radius: 8px;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
}

.inline-component h3 {
  font-size: 1rem;            /* Compact headers */
  margin-bottom: 0.5rem;
}

.inline-component button {
  font-size: 0.875rem;        /* Smaller buttons */
  padding: 0.5rem 1rem;
}
```

**Example:**

```javascript
const SimpleForm = ({ payload, onResponse, ui_tool_id, eventId }) => {
  const [value, setValue] = useState('');

  return (
    <div className="inline-component simple-form">
      <h3>{payload.title}</h3>
      <input
        type="text"
        value={value}
        onChange={(e) => setValue(e.target.value)}
        placeholder={payload.placeholder}
      />
      <button onClick={() => onResponse({
        status: 'success',
        action: 'submit',
        data: { value, ui_tool_id, eventId }
      })}>
        Submit
      </button>
    </div>
  );
};
```

### 2. Artifact Mode

**Behavior:** Renders in side panel (`ArtifactPanel`), replacing previous artifact.

**Use Cases:**
- Complex visualizations
- Multi-step workflows
- Large data displays
- Document editors

**Configuration (tools.json):**

```json
{
  "tool": "action_plan",
  "ui": {
    "mode": "artifact",
    "component": "ActionPlan"
  }
}
```

**Styling Guidelines:**

```css
.artifact-component {
  min-height: 100vh;          /* Full-height panel */
  padding: 2rem;
  background: var(--color-surface);
}

.artifact-component header {
  position: sticky;
  top: 0;
  background: var(--color-surface);
  padding-bottom: 1rem;
  border-bottom: 2px solid var(--color-border);
  z-index: 10;
}

.artifact-component footer {
  position: sticky;
  bottom: 0;
  background: var(--color-surface);
  padding-top: 1rem;
  border-top: 2px solid var(--color-border);
}
```

**Lifecycle Behavior:**

```
1. Agent invokes tool with mode: "artifact"
   ↓
2. ChatPage.handleToolCall() detects artifact mode
   ↓
3. setCurrentArtifactMessages([toolMessage])  // Replace previous
   ↓
4. setIsSidePanelOpen(true)                   // Show panel
   ↓
5. ArtifactPanel renders component in side panel
   ↓
6. User interacts and submits response
   ↓
7. onResponse() sends data to backend
   ↓
8. Panel auto-collapses when new text message arrives
```

**Auto-Collapse Logic:**

```javascript
// In ChatPage.js
const handleTextMessage = (data) => {
  // Add message to chat
  setMessages(prev => [...prev, newMessage]);

  // Auto-collapse artifact panel
  if (isSidePanelOpen && lastArtifactEventRef.current) {
    console.log('Auto-collapsing artifact panel for new text message');
    setIsSidePanelOpen(false);
  }
};
```

**Manual Close:**

```javascript
// ArtifactPanel provides close button
<button onClick={onClose} className="close-button">
  ✕ Close
</button>
```

### 3. Fullscreen Mode (Future)

**Behavior:** Renders in modal overlay covering entire viewport.

**Use Cases:**
- Immersive experiences
- Complex editors
- Multi-page wizards

**Configuration (tools.json):**

```json
{
  "tool": "workflow_builder",
  "ui": {
    "mode": "fullscreen",
    "component": "WorkflowBuilder"
  }
}
```

**Implementation:** Not yet implemented; reserved for future use.

## Component Structure

### Directory Layout

```
workflows/
  {WorkflowName}/
    components/
      index.js                  ← Export all components
      {ComponentName}.js        ← Individual components
      shared/                   ← Shared utilities (optional)
        utils.js
        hooks.js
```

### index.js Pattern

```javascript
// workflows/Generator/components/index.js

import AgentAPIKeyInput from './AgentAPIKeyInput';
import FileDownloadCenter from './FileDownloadCenter';
import ActionPlan from './ActionPlan';

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

**Critical:** Export as both default object and named exports for compatibility with `WorkflowUIRouter`.

### Component File Pattern

```javascript
// workflows/Generator/components/AgentAPIKeyInput.js

import React, { useState } from 'react';

/**
 * AgentAPIKeyInput - Secure API key collection component
 * 
 * @param {Object} payload - Tool data from agent
 * @param {Function} onResponse - Response callback
 * @param {String} ui_tool_id - Tool identifier
 * @param {String} eventId - Event identifier
 */
const AgentAPIKeyInput = ({ 
  payload, 
  onResponse, 
  ui_tool_id, 
  eventId 
}) => {
  // Component implementation...
  
  return (
    <div className="agent-api-key-input">
      {/* Component JSX */}
    </div>
  );
};

export default AgentAPIKeyInput;
```

## State Management

### Local State (useState)

**Use For:**
- Form input values
- UI toggles (visibility, accordion open/close)
- Loading states
- Error messages

**Pattern:**

```javascript
const MyComponent = ({ payload, onResponse, ui_tool_id, eventId }) => {
  const [formData, setFormData] = useState({
    field1: '',
    field2: ''
  });
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');

  const handleChange = (field, value) => {
    setFormData(prev => ({ ...prev, [field]: value }));
    if (error) setError(''); // Clear error on edit
  };

  const handleSubmit = async () => {
    setIsSubmitting(true);
    try {
      await onResponse({ status: 'success', data: formData });
    } catch (err) {
      setError(err.message);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (/* JSX */);
};
```

### Effects (useEffect)

**Use For:**
- Auto-starting processes on mount
- Subscribing to external events
- Cleanup on unmount

**Pattern:**

```javascript
const FileDownloadCenter = ({ payload, onResponse, ui_tool_id, eventId }) => {
  const [files, setFiles] = useState([]);

  useEffect(() => {
    // Auto-generate files on mount
    if (payload?.autoGenerate) {
      generateFiles();
    }

    // Cleanup function
    return () => {
      // Cancel pending requests, clear intervals, etc.
    };
  }, [payload?.autoGenerate]); // Dependencies

  const generateFiles = async () => {
    // Implementation...
  };

  return (/* JSX */);
};
```

### Refs (useRef)

**Use For:**
- DOM element access
- Mutable values that don't trigger re-renders
- Previous value tracking

**Pattern:**

```javascript
const MermaidPreview = ({ chart }) => {
  const ref = useRef(null);
  const renderedChartRef = useRef('');

  useEffect(() => {
    // Only re-render if chart changed
    if (chart && chart !== renderedChartRef.current) {
      const renderChart = async () => {
        const { svg } = await mermaid.render('mermaid-chart', chart);
        if (ref.current) {
          ref.current.innerHTML = svg;
          renderedChartRef.current = chart;
        }
      };
      renderChart();
    }
  }, [chart]);

  return <div ref={ref} className="mermaid-chart" />;
};
```

### Context (useContext)

**Use For:**
- Accessing global app state (ChatUIContext)
- Theme/styling configuration
- Shared utilities

**Pattern:**

```javascript
import { useChatUI } from '../../../context/ChatUIContext';

const MyComponent = ({ payload, onResponse, ui_tool_id, eventId }) => {
  const { user, config, api } = useChatUI();

  const handleAction = async () => {
    // Use context values
    const response = await api.sendCustomRequest({
      user_id: user.id,
      ...payload
    });

    await onResponse({ status: 'success', data: response });
  };

  return (/* JSX */);
};
```

## Styling Patterns

### CSS Variable System

**Use centralized design tokens:**

```css
/* Available CSS variables (defined in index.css) */
:root {
  /* Colors */
  --color-primary: #00d4ff;
  --color-primary-light: #4de4ff;
  --color-primary-rgb: 0, 212, 255;
  
  --color-secondary: #9333ea;
  --color-secondary-light: #a855f7;
  --color-secondary-rgb: 147, 51, 234;
  
  --color-accent: #f59e0b;
  --color-accent-light: #fbbf24;
  --color-accent-rgb: 245, 158, 11;
  
  --color-success: #10b981;
  --color-success-rgb: 16, 185, 129;
  
  --color-error: #ef4444;
  --color-error-rgb: 239, 68, 68;
  
  --color-warning: #f59e0b;
  --color-warning-rgb: 245, 158, 11;
  
  /* Surfaces */
  --color-surface: #1e293b;
  --color-surface-alt: #334155;
  --color-surface-rgb: 30, 41, 59;
  
  /* Text */
  --color-text-primary: #f8fafc;
  --color-text-secondary: #cbd5e1;
  --color-text-muted: #94a3b8;
  
  /* Borders */
  --color-border: #475569;
}
```

**Usage in Components:**

```css
.my-component {
  background: var(--color-surface);
  border: 2px solid var(--color-primary);
  color: var(--color-text-primary);
}

.my-component:hover {
  border-color: var(--color-primary-light);
  box-shadow: 0 0 20px rgba(var(--color-primary-rgb), 0.3);
}

.error-message {
  color: var(--color-error);
  background: rgba(var(--color-error-rgb), 0.1);
  border: 1px solid var(--color-error);
}
```

### Tailwind Classes

**MozaiksAI uses Tailwind CSS** for rapid styling:

```javascript
<div className="rounded-lg border-2 border-cyan-500 bg-slate-800 p-6 shadow-xl hover:border-cyan-400 hover:shadow-2xl transition-all">
  <h3 className="text-xl font-bold text-white mb-4">
    Title
  </h3>
  <p className="text-sm text-slate-300 leading-relaxed">
    Description
  </p>
</div>
```

**Common Patterns:**

```javascript
// Card
className="rounded-lg border-2 border-slate-600 bg-slate-800 p-6"

// Button (primary)
className="rounded-lg bg-gradient-to-r from-cyan-500 to-blue-600 px-6 py-3 font-bold text-white hover:from-cyan-400 hover:to-blue-500 transition-all"

// Button (secondary)
className="rounded-lg border-2 border-slate-500 bg-slate-700 px-6 py-3 font-semibold text-slate-200 hover:bg-slate-600 transition-colors"

// Input
className="w-full rounded-lg border-2 border-slate-600 bg-slate-900 px-4 py-3 text-white placeholder-slate-400 focus:border-cyan-500 focus:outline-none focus:ring-2 focus:ring-cyan-500/50"

// Accordion (open)
className="rounded-xl border-2 border-cyan-400 bg-slate-800 shadow-xl"

// Accordion (closed)
className="rounded-xl border-2 border-slate-600 bg-slate-800/50"
```

### Responsive Design

**Use Tailwind responsive prefixes:**

```javascript
<div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
  {/* Items */}
</div>

<div className="text-sm md:text-base lg:text-lg">
  Responsive text
</div>

<div className="p-4 md:p-6 lg:p-8">
  Responsive padding
</div>
```

## Event Handling

### onResponse Function

**Signature:**

```typescript
onResponse: (response: ResponseObject) => Promise<void>
```

**Backend Processing:**

```
1. Component calls onResponse({ status: 'success', data: {...} })
   ↓
2. ChatPage.submitInputRequest() receives response
   ↓
3. WebSocket.send(response) serializes and sends to backend
   ↓
4. Backend receives as chat.ui_tool_response event
   ↓
5. Runtime correlates response with original tool_call via eventId
   ↓
6. Tool function returns response data to agent
   ↓
7. Agent continues conversation with response context
```

**Error Handling:**

```javascript
const handleSubmit = async () => {
  try {
    await onResponse({ status: 'success', data: formData });
  } catch (error) {
    console.error('Failed to send response:', error);
    
    // Show error to user
    setError('Failed to submit. Please try again.');
    
    // Send error response
    await onResponse({
      status: 'error',
      action: 'submit',
      error: error.message,
      data: { ui_tool_id, eventId }
    });
  }
};
```

### Multiple Actions

**Pattern:** Component supports multiple user actions.

```javascript
const ActionPlan = ({ payload, onResponse, ui_tool_id, eventId }) => {
  const handleApprove = async () => {
    await onResponse({
      status: 'success',
      action: 'approve',
      data: {
        approved: true,
        approvalTime: new Date().toISOString(),
        ui_tool_id,
        eventId
      }
    });
  };

  const handleReject = async () => {
    await onResponse({
      status: 'success',
      action: 'reject',
      data: {
        approved: false,
        rejectionTime: new Date().toISOString(),
        ui_tool_id,
        eventId
      }
    });
  };

  const handleRequestChanges = async (feedback) => {
    await onResponse({
      status: 'success',
      action: 'request_changes',
      data: {
        feedback,
        requestTime: new Date().toISOString(),
        ui_tool_id,
        eventId
      }
    });
  };

  return (
    <div>
      {/* Component content */}
      <button onClick={handleReject}>Reject</button>
      <button onClick={() => handleRequestChanges(feedbackText)}>
        Request Changes
      </button>
      <button onClick={handleApprove}>Approve</button>
    </div>
  );
};
```

**Backend Handling:**

```python
async def action_plan(...) -> dict:
    # Wait for response
    response = await use_ui_tool(...)
    
    # Handle different actions
    if response.get('action') == 'approve':
        return {"status": "approved", "workflow": workflow}
    elif response.get('action') == 'reject':
        return {"status": "rejected", "reason": "User rejected plan"}
    elif response.get('action') == 'request_changes':
        feedback = response.get('data', {}).get('feedback')
        return {"status": "needs_revision", "feedback": feedback}
```

## Best Practices

### 1. Defensive Payload Parsing

**Problem:** Payload structure may vary or contain unexpected data.

**Solution:** Use optional chaining and defaults.

```javascript
// ❌ BAD: Assumes structure
const name = payload.workflow.name;

// ✅ GOOD: Defensive access
const workflow = payload?.workflow || payload?.ActionPlan || {};
const name = workflow.name || 'Untitled Workflow';
const phases = Array.isArray(workflow.phases) ? workflow.phases : [];
```

### 2. Echo Correlation IDs

**Always include `ui_tool_id` and `eventId` in response data:**

```javascript
await onResponse({
  status: 'success',
  action: 'submit',
  data: {
    // Your data here
    apiKey: apiKey.trim(),
    
    // Echo correlation IDs (REQUIRED)
    ui_tool_id,
    eventId
  }
});
```

Backend uses these for event correlation and tool response routing.

### 3. Handle Loading States

**Show feedback during async operations:**

```javascript
const [isSubmitting, setIsSubmitting] = useState(false);

const handleSubmit = async () => {
  setIsSubmitting(true);
  try {
    await onResponse({ ... });
  } finally {
    setIsSubmitting(false);
  }
};

return (
  <button disabled={isSubmitting}>
    {isSubmitting ? 'Submitting...' : 'Submit'}
  </button>
);
```

### 4. Clear Sensitive Data

**Clear input fields after submission:**

```javascript
const handleSubmit = async () => {
  await onResponse({ status: 'success', data: { apiKey } });
  
  // Clear sensitive data
  setApiKey('');
};
```

### 5. Provide Cancel Actions

**Allow users to cancel operations:**

```javascript
const handleCancel = async () => {
  await onResponse({
    status: 'cancelled',
    action: 'cancel',
    data: { ui_tool_id, eventId }
  });
};

return (
  <div>
    <button onClick={handleSubmit}>Submit</button>
    <button onClick={handleCancel}>Cancel</button>
  </div>
);
```

### 6. Accessibility

**Support keyboard navigation and screen readers:**

```javascript
<button
  onClick={handleSubmit}
  aria-label="Submit API key"
  aria-disabled={isSubmitting}
  tabIndex={0}
>
  Submit
</button>

<input
  id="api-key-input"
  type="password"
  aria-label="API Key Input"
  aria-required={required}
  aria-invalid={!!error}
/>

{error && (
  <p role="alert" className="error">
    {error}
  </p>
)}
```

### 7. Error Boundaries

**Wrap components in error boundaries:**

```javascript
class ComponentErrorBoundary extends React.Component {
  state = { hasError: false, error: null };

  static getDerivedStateFromError(error) {
    return { hasError: true, error };
  }

  componentDidCatch(error, info) {
    console.error('Component error:', error, info);
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="error-boundary">
          <h3>Component Error</h3>
          <p>{this.state.error?.message}</p>
          <button onClick={() => this.setState({ hasError: false })}>
            Retry
          </button>
        </div>
      );
    }

    return this.props.children;
  }
}
```

## Testing Components

### Manual Testing

**Test Checklist:**
- [ ] Component loads without errors
- [ ] All props handled correctly
- [ ] Payload defaults work when fields missing
- [ ] Form validation prevents invalid submissions
- [ ] Loading states display correctly
- [ ] Error states render appropriately
- [ ] Success responses send correct data
- [ ] Cancel/reject flows work
- [ ] Component clears sensitive data after submit
- [ ] Responsive layout works on mobile/tablet/desktop

### Debug Logging

**Enable component debugging:**

```javascript
const DEBUG = localStorage.getItem('debug_ui_components') === 'true';

const AgentAPIKeyInput = ({ payload, onResponse, ui_tool_id, eventId }) => {
  if (DEBUG) {
    console.log('AgentAPIKeyInput mounted:', {
      payload,
      ui_tool_id,
      eventId
    });
  }

  const handleSubmit = async () => {
    if (DEBUG) console.log('Submitting:', { apiKey: '***', service });
    
    await onResponse({ ... });
    
    if (DEBUG) console.log('Response sent successfully');
  };

  // ...
};
```

**Enable in browser:**

```javascript
localStorage.setItem('debug_ui_components', 'true');
location.reload();
```

## Next Steps

- **[Workflow Integration](./workflow_integration.md)**: Component registration and dynamic loading
- **[ChatUI Architecture](./chatui_architecture.md)**: Frontend application structure
- **[UI Tool Pipeline](../workflows/ui_tool_pipeline.md)**: Complete agent-to-frontend flow
- **[Tool Manifest](../workflows/tool_manifest.md)**: Tool registration and configuration
- **[Structured Outputs](../workflows/structured_outputs.md)**: Pydantic schema design for payloads
