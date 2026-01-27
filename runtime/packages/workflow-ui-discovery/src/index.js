/**
 * @fileoverview Main entry point for @mozaiks/workflow-ui-discovery
 * 
 * This module provides automatic discovery and index generation for
 * workflow UI components. It's designed to be used:
 * 
 * 1. As a library - import and use WorkflowComponentDiscovery directly
 * 2. As a CLI tool - npx workflow-ui-discover
 * 3. Integrated into the runtime - called automatically on startup
 * 
 * @module @mozaiks/workflow-ui-discovery
 */

export { WorkflowComponentDiscovery } from './discovery.js';
export { ComponentWatcher } from './watcher.js';

// Default export for convenience
import { WorkflowComponentDiscovery } from './discovery.js';
export default WorkflowComponentDiscovery;

/**
 * Creates and runs component discovery.
 * Convenience function for one-time generation.
 * 
 * @param {string} workflowsPath - Path to the workflows directory
 * @param {Object} options - Discovery options
 * @returns {Object} Discovery result
 * 
 * @example
 * import { discover } from '@mozaiks/workflow-ui-discovery';
 * const result = discover('./frontend/workflows');
 */
export function discover(workflowsPath, options = {}) {
  const discovery = new WorkflowComponentDiscovery(workflowsPath, options);
  return discovery.run();
}

/**
 * Creates and starts a component watcher.
 * Convenience function for watch mode.
 * 
 * @param {string} workflowsPath - Path to the workflows directory
 * @param {Object} options - Watcher options
 * @returns {ComponentWatcher} The started watcher instance
 * 
 * @example
 * import { watch } from '@mozaiks/workflow-ui-discovery';
 * const watcher = await watch('./frontend/workflows', {
 *   onRegenerate: ({ workflow }) => console.log(`Updated: ${workflow}`)
 * });
 * // Later: watcher.stop();
 */
export async function watch(workflowsPath, options = {}) {
  const { ComponentWatcher } = await import('./watcher.js');
  const watcher = new ComponentWatcher(workflowsPath, options);
  await watcher.start();
  return watcher;
}
