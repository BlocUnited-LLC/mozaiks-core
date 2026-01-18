# Shared Workflow UI Components

This folder contains reusable UI components for workflow frontends.

## Available Components

Components in `_shared/` are available to all workflows via the dynamic loader.

## Adding Workflow-Specific UI

Create a folder matching your workflow name:

```
ChatUI/src/workflows/
├── _shared/              # Reusable components
├── index.js              # API-driven registry
└── MyWorkflow/           # Your workflow-specific UI
    ├── components/
    │   └── MyArtifact.js
    ├── index.js          # Exports { components, theme_config }
    └── theme_config.json
```

The `WorkflowUIRouter` will automatically load your components when the workflow runs.
