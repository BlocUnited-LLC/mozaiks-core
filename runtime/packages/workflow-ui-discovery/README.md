# @mozaiks/workflow-ui-discovery

Automatic discovery and index generation for mozaiks workflow UI components.

## Problem

When building workflow UIs, each workflow has a `components/` directory containing React components. Manually maintaining `index.js` export files is error-prone and violates DRY principles.

**Before (manual process):**
```bash
# User adds MyComponent.jsx
# User must remember to run:
node scripts/generate-workflow-index.js
# If they forget, imports break at runtime
```

**After (automatic):**
```bash
# User adds MyComponent.jsx
# ✅ Index auto-regenerates on startup/file change
# No manual steps required
```

## Installation

```bash
npm install @mozaiks/workflow-ui-discovery
# or
pnpm add @mozaiks/workflow-ui-discovery
```

## Usage

### CLI Usage

```bash
# One-time generation
npx workflow-ui-discover ./frontend/workflows

# Watch mode (for development)
npx workflow-ui-discover --watch ./frontend/workflows

# With custom config
npx workflow-ui-discover -c ./config/ui-discovery.json ./frontend/workflows
```

### Programmatic Usage

```javascript
import { discover, watch } from '@mozaiks/workflow-ui-discovery';

// One-time generation
const result = discover('./frontend/workflows');
console.log(`Found ${result.componentsFound} components`);

// Watch mode
const watcher = await watch('./frontend/workflows', {
  onRegenerate: ({ workflow }) => console.log(`Updated: ${workflow}`),
  onError: ({ workflow, error }) => console.error(`Error in ${workflow}: ${error}`)
});

// Later: stop watching
watcher.stop();
```

### Integration with Runtime

The discovery system is designed to be called automatically by the mozaiks runtime:

```javascript
// In your runtime initialization
import { WorkflowComponentDiscovery, ComponentWatcher } from '@mozaiks/workflow-ui-discovery';

async function initializeRuntime(config) {
  const workflowsPath = config.workflowsPath;
  
  if (config.ui_component_discovery?.enabled !== false) {
    const discovery = new WorkflowComponentDiscovery(workflowsPath, {
      ...config.ui_component_discovery
    });
    
    // Generate indexes on startup
    if (config.ui_component_discovery?.on_startup !== 'skip') {
      discovery.run();
    }
    
    // Start watcher in development mode
    if (process.env.NODE_ENV === 'development' && config.ui_component_discovery?.watch_mode !== false) {
      const watcher = new ComponentWatcher(workflowsPath);
      await watcher.start();
      
      // Store watcher for cleanup
      runtime.componentWatcher = watcher;
    }
  }
}
```

## Directory Structure

The discovery system expects this structure:

```
frontend/workflows/
├── AgentGenerator/
│   ├── theme_config.json     ← Workflow config (triggers workflow index)
│   ├── index.js              ← AUTO-GENERATED (workflow metadata)
│   └── components/
│       ├── index.js          ← AUTO-GENERATED (component exports)
│       ├── ActionPlan.jsx
│       ├── FileDownloadCenter.jsx
│       └── MermaidDiagram.jsx
├── SubscriptionManager/
│   ├── theme_config.json     ← Workflow config
│   ├── index.js              ← AUTO-GENERATED
│   └── components/
│       ├── index.js          ← AUTO-GENERATED
│       └── PlanPreview.jsx
├── _shared/
│   └── components/
│       ├── index.js          ← AUTO-GENERATED
│       ├── StatusBadge.jsx
│       └── ApprovalButtons.jsx
└── index.js                  ← AUTO-GENERATED (root registry)
```

## Generated Files

### Component Index (`{workflow}/components/index.js`)

```javascript
/**
 * AUTO-GENERATED FILE - DO NOT EDIT
 * Generated: 2025-01-25T10:30:00.000Z
 */

export { default as ActionPlan } from './ActionPlan';
export { default as FileDownloadCenter } from './FileDownloadCenter';
export { default as MermaidDiagram } from './MermaidDiagram';
```

### Workflow Index (`{workflow}/index.js`)

When a workflow has a `theme_config.json` file, a workflow-level index is also generated:

```javascript
/**
 * AUTO-GENERATED FILE - DO NOT EDIT
 * Workflow: AgentGenerator
 * Generated: 2025-01-25T10:30:00.000Z
 */

import themeConfig from './theme_config.json';
import * as components from './components/index.js';

export const workflow = {
  id: 'AgentGenerator',
  name: 'AgentGenerator',
  description: 'Agent Generator Workflow',
  config: themeConfig,
  components,
};

export { themeConfig, components };
export default workflow;
```

### Root Registry (`workflows/index.js`)

```javascript
/**
 * AUTO-GENERATED FILE - DO NOT EDIT
 * Generated: 2025-01-25T10:30:00.000Z
 */

// Shared components available to all workflows
export * as shared from './_shared/components/index.js';

export * as AgentGenerator from './AgentGenerator/components/index.js';
export * as SubscriptionManager from './SubscriptionManager/components/index.js';

/**
 * Lazy-loading registry for workflow components.
 */
export const workflowRegistry = {
  'AgentGenerator': () => import('./AgentGenerator/components/index.js'),
  'SubscriptionManager': () => import('./SubscriptionManager/components/index.js'),
};

/**
 * Get component module for a specific workflow.
 */
export async function getWorkflowComponents(workflowName) {
  const loader = workflowRegistry[workflowName];
  if (!loader) {
    throw new Error(`Unknown workflow: ${workflowName}`);
  }
  return loader();
}
```

## Configuration

Configuration can be provided via:
1. CLI flags
2. JSON config file (`-c ./config/ui-discovery.json`)
3. In `mozaiks.config.js` under `ui_component_discovery`

### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `enabled` | boolean | `true` | Enable/disable discovery |
| `scan_paths` | string[] | `["frontend/workflows/*/components/"]` | Paths to scan |
| `exclude_files` | string[] | `["index.js", "*.test.js", ...]` | Files to exclude |
| `component_extensions` | string[] | `[".js", ".jsx", ".ts", ".tsx"]` | File extensions |
| `auto_generate_index` | boolean | `true` | Auto-generate index files |
| `generate_root_registry` | boolean | `true` | Generate root registry |
| `generate_workflow_index` | boolean | `true` | Generate workflow-level index.js |
| `workflow_config_file` | string | `"theme_config.json"` | Config file that triggers workflow index |
| `index_template` | string | `"esm"` | `"esm"`, `"cjs"`, or `"typescript"` |
| `watch_mode` | boolean | `true` | Enable file watching in dev |
| `watch_debounce_ms` | number | `300` | Debounce delay for watch |
| `on_startup` | string | `"generate"` | `"generate"`, `"validate"`, `"skip"` |
| `on_build` | string | `"generate"` | `"generate"`, `"validate"`, `"skip"` |

### Example Config

```json
{
  "ui_component_discovery": {
    "enabled": true,
    "scan_paths": [
      "frontend/workflows/*/components/",
      "frontend/workflows/_shared/components/"
    ],
    "exclude_files": [
      "index.js",
      "*.test.js",
      "*.stories.js"
    ],
    "auto_generate_index": true,
    "watch_mode": true
  }
}
```

## How It Works

1. **On Startup**: Scans all workflow directories for `components/` folders
2. **Discovery**: Lists all component files (excluding index.js, tests, etc.)
3. **Generation**: Creates/updates index.js with exports for each component
4. **Registry**: Generates root index.js with lazy-loading registry
5. **Watch Mode**: In development, watches for file changes and regenerates

## Benefits

- ✅ **Zero manual steps** for self-hosters
- ✅ **Hot reload works** automatically when adding components
- ✅ **Consistent behavior** across all deployments
- ✅ **No script maintenance** required
- ✅ **Lazy loading** support via generated registry
- ✅ **TypeScript support** via index template option

## API Reference

### `WorkflowComponentDiscovery`

```typescript
class WorkflowComponentDiscovery {
  constructor(workflowsRoot: string, options?: DiscoveryOptions);
  
  // Run full discovery and generation
  run(): DiscoveryResult;
  
  // Regenerate a specific workflow
  regenerateWorkflow(workflowName: string): boolean;
  
  // Regenerate root registry only
  regenerateRootRegistry(): void;
  
  // Get discovered component directories
  discoverComponentDirectories(): Map<string, string>;
  
  // Scan a directory for components
  scanComponentFiles(componentsDir: string): string[];
}
```

### `ComponentWatcher`

```typescript
class ComponentWatcher {
  constructor(workflowsRoot: string, options?: WatcherOptions);
  
  // Start watching
  start(): Promise<void>;
  
  // Stop watching
  stop(): void;
  
  // Get watcher status
  getStatus(): { isRunning: boolean; watchedDirectories: number };
}
```

### Convenience Functions

```typescript
// One-time discovery
function discover(workflowsPath: string, options?: DiscoveryOptions): DiscoveryResult;

// Start watcher
async function watch(workflowsPath: string, options?: WatcherOptions): Promise<ComponentWatcher>;
```

## License

MIT © BlocUnited
