/**
 * AppGenerator Workflow (frontend module)
 *
 * NOTE:
 * - UI tool rendering in ChatUI is dynamically routed via `WorkflowUIRouter`
 *   using `workflows/<WorkflowName>/components/index.js`.
 * - This file is provided for workflow-specific helpers/config and is not
 *   currently used as a registry entry.
 */

import themeConfig from './theme_config.json';

export const AppGeneratorWorkflow = {
  id: 'AppGenerator',
  name: 'App Generator',
  description: 'Generate full applications with E2B validation and preview',
  config: themeConfig,
};

export default AppGeneratorWorkflow;

