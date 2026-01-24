# Frontend Workflows Integration

This document describes how mozaiks-core's ChatUI shell integrates with workflow-specific UI components from mozaiks-platform.

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                      mozaiks-core (open source)                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │            runtime/packages/shell (ChatUI)               │   │
│  │  ┌─────────────────────────────────────────────────────┐ │   │
│  │  │  src/core/WorkflowUIRouter.js                       │ │   │
│  │  │  - Receives UI tool events from agents              │ │   │
│  │  │  - Dynamic import: @chat-workflows/${workflow}/...  │ │   │
│  │  └─────────────────────────────────────────────────────┘ │   │
│  │                           │                               │   │
│  │                           ▼                               │   │
│  │  ┌─────────────────────────────────────────────────────┐ │   │
│  │  │  vite.config.js alias:                              │ │   │
│  │  │  @chat-workflows → MOZAIKS_FRONTEND_WORKFLOWS_PATH  │ │   │
│  │  │                  → ./src/workflows_stub (fallback)  │ │   │
│  │  └─────────────────────────────────────────────────────┘ │   │
│  └──────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────┘
                              │
                              │ env var points to
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│                   mozaiks-platform (proprietary)                 │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              frontend/workflows/                          │   │
│  │  ├── core/             # WorkflowUIRouter, toolsLogger   │   │
│  │  ├── styles/           # artifactDesignSystem            │   │
│  │  ├── hooks/            # Shared React hooks              │   │
│  │  ├── AgentGenerator/   # Agent Generator UI components   │   │
│  │  │   └── components/                                      │   │
│  │  │       ├── ActionPlan.js                               │   │
│  │  │       ├── AgentAPIKeysBundleInput.js                  │   │
│  │  │       ├── FileDownloadCenter.js                       │   │
│  │  │       └── index.js                                    │   │
│  │  ├── AppGenerator/     # App Generator UI components     │   │
│  │  ├── ValueEngine/      # Value Engine UI components      │   │
│  │  └── index.js          # Workflow registry               │   │
│  └──────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────┘
```

## Configuration

### Environment Variable

Set `MOZAIKS_FRONTEND_WORKFLOWS_PATH` to point to the platform workflows directory:

```bash
# Windows PowerShell
$env:MOZAIKS_FRONTEND_WORKFLOWS_PATH = "C:\path\to\mozaiks-platform\frontend\workflows"

# Linux/Mac
export MOZAIKS_FRONTEND_WORKFLOWS_PATH="/path/to/mozaiks-platform/frontend/workflows"
```

### Vite Configuration

The shell's `vite.config.js` resolves the alias:

```javascript
resolve: {
  alias: {
    '@chat-workflows': process.env.MOZAIKS_FRONTEND_WORKFLOWS_PATH
      ? resolve(process.env.MOZAIKS_FRONTEND_WORKFLOWS_PATH)
      : resolve(__dirname, 'src/workflows_stub')
  }
}
```

## How It Works

1. **Agent emits UI tool event** with `workflow_name` and `component_type`
2. **WorkflowUIRouter** receives the event
3. **Dynamic import** loads `@chat-workflows/${workflow}/components/index.js`
4. **Component rendered** with payload and callback handlers

### Event Payload Structure

```javascript
{
  workflow_name: "AgentGenerator",    // Which workflow's components to load
  component_type: "ActionPlan",       // Which component from that workflow
  workflow: { /* workflow metadata */ },
  // ... component-specific data
}
```

## Adding New Workflows

### 1. Create Workflow Directory

```
frontend/workflows/MyWorkflow/
└── components/
    ├── MyComponent.js
    └── index.js
```

### 2. Export Components

```javascript
// frontend/workflows/MyWorkflow/components/index.js
import MyComponent from './MyComponent';
import AnotherComponent from './AnotherComponent';

export default {
  MyComponent,
  AnotherComponent
};

export { MyComponent, AnotherComponent };
```

### 3. Register in Index

```javascript
// frontend/workflows/index.js
const AVAILABLE_WORKFLOWS = [
  'AgentGenerator',
  'AppGenerator', 
  'ValueEngine',
  'MyWorkflow'  // Add here
];
```

### 4. Use Shared Utilities

```javascript
// Import from workflow root
import { createToolsLogger } from '../../core/toolsLogger';
import { components, colors } from '../../styles/artifactDesignSystem';
```

## Component Interface

Each workflow component receives these props:

```typescript
interface WorkflowComponentProps {
  payload: object;              // The UI tool event payload
  onResponse: (data) => void;   // Send response back to agent
  onCancel: (data) => void;     // Cancel/close the artifact
  submitInputRequest: fn;       // Submit user input
  ui_tool_id: string;           // Unique ID for this tool invocation
  eventId: string;              // Event ID for caching/tracking
}
```

## Self-Hosted Mode

When running mozaiks-core standalone without mozaiks-platform:

1. Leave `MOZAIKS_FRONTEND_WORKFLOWS_PATH` unset
2. Shell falls back to `src/workflows_stub/`
3. Stub provides no-op implementations
4. Core chat functionality works without workflow UI artifacts

## Design System

The `styles/artifactDesignSystem.js` provides:

- **fonts** - Typography utilities (body, heading, logo)
- **colors** - Theme-aware color utilities using CSS variables
- **components** - Pre-built component style objects
- **spacing** - Consistent spacing values

All styles use CSS variables for theming support, allowing each deployment to customize the look and feel.

## Troubleshooting

### Component Not Loading

1. Check `MOZAIKS_FRONTEND_WORKFLOWS_PATH` is set correctly
2. Verify the workflow directory exists
3. Check browser console for import errors
4. Ensure `components/index.js` exports the component

### Import Errors

Components should use relative imports from the workflow root:
- ✅ `../../core/toolsLogger`
- ❌ `../../../core/toolsLogger` (old structure)

### Caching Issues

The WorkflowUIRouter caches components by:
- Chat ID
- Cache seed
- Workflow name
- Component type
- Event ID

Clear browser cache or use incognito mode if seeing stale components.
