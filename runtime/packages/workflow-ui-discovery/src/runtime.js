/**
 * @fileoverview Runtime integration for automatic component discovery.
 * 
 * This module provides the integration point between the mozaiks runtime
 * and the component discovery system. It's designed to be called during
 * runtime initialization.
 * 
 * Usage in runtime:
 *   import { initializeComponentDiscovery } from '@mozaiks/workflow-ui-discovery/runtime';
 *   await initializeComponentDiscovery(runtimeConfig);
 * 
 * @module @mozaiks/workflow-ui-discovery/runtime
 */

import path from 'path';
import fs from 'fs';
import { WorkflowComponentDiscovery } from './discovery.js';
import { ComponentWatcher } from './watcher.js';

/**
 * Default runtime configuration for component discovery.
 */
const DEFAULT_RUNTIME_CONFIG = {
  ui_component_discovery: {
    enabled: true,
    on_startup: 'generate',      // 'generate' | 'validate' | 'skip'
    on_build: 'generate',        // 'generate' | 'validate' | 'skip'
    watch_mode: true,            // Only active in development
    workflows_path: null,        // Auto-detect if not specified
  }
};

/**
 * Tries to auto-detect the workflows directory.
 * 
 * @param {string} baseDir - Base directory to search from
 * @returns {string|null} Path to workflows directory, or null if not found
 */
function autoDetectWorkflowsPath(baseDir) {
  const commonPaths = [
    'frontend/workflows',
    'src/frontend/workflows',
    'workflows',
    'src/workflows',
    'ui/workflows'
  ];

  for (const relPath of commonPaths) {
    const fullPath = path.join(baseDir, relPath);
    if (fs.existsSync(fullPath)) {
      return fullPath;
    }
  }

  return null;
}

/**
 * Initializes the component discovery system for the runtime.
 * 
 * This function should be called during runtime startup. It will:
 * 1. Detect or use the configured workflows path
 * 2. Run initial discovery/generation
 * 3. Start the file watcher in development mode
 * 
 * @param {Object} config - Runtime configuration
 * @param {Object} [config.ui_component_discovery] - Discovery configuration
 * @param {string} [config.baseDir] - Base directory (defaults to process.cwd())
 * @returns {Promise<RuntimeDiscoveryResult>} Result with discovery stats and watcher
 * 
 * @example
 * const { watcher, result } = await initializeComponentDiscovery({
 *   baseDir: '/app',
 *   ui_component_discovery: {
 *     enabled: true,
 *     watch_mode: true
 *   }
 * });
 */
export async function initializeComponentDiscovery(config = {}) {
  const mergedConfig = {
    ...DEFAULT_RUNTIME_CONFIG,
    ...config,
    ui_component_discovery: {
      ...DEFAULT_RUNTIME_CONFIG.ui_component_discovery,
      ...config.ui_component_discovery
    }
  };

  const discoveryConfig = mergedConfig.ui_component_discovery;
  const baseDir = config.baseDir || process.cwd();

  // Return early if disabled
  if (!discoveryConfig.enabled) {
    console.log('[runtime] UI component discovery is disabled');
    return {
      enabled: false,
      watcher: null,
      result: null
    };
  }

  // Determine workflows path
  let workflowsPath = discoveryConfig.workflows_path;
  if (!workflowsPath) {
    workflowsPath = autoDetectWorkflowsPath(baseDir);
  }

  if (!workflowsPath) {
    console.warn('[runtime] Could not find workflows directory, skipping component discovery');
    return {
      enabled: true,
      watcher: null,
      result: null,
      error: 'Workflows directory not found'
    };
  }

  // Make path absolute
  if (!path.isAbsolute(workflowsPath)) {
    workflowsPath = path.join(baseDir, workflowsPath);
  }

  console.log(`[runtime] Initializing UI component discovery: ${workflowsPath}`);

  const discoveryOptions = {
    componentExtensions: discoveryConfig.component_extensions,
    excludeFiles: discoveryConfig.exclude_files,
    generateRootRegistry: discoveryConfig.generate_root_registry ?? true,
    indexTemplate: discoveryConfig.index_template ?? 'esm'
  };

  const discovery = new WorkflowComponentDiscovery(workflowsPath, discoveryOptions);
  let result = null;
  let watcher = null;

  // Run initial discovery based on startup action
  const startupAction = discoveryConfig.on_startup ?? 'generate';
  
  if (startupAction === 'generate') {
    result = discovery.run();
    
    if (result.errors.length > 0) {
      console.warn(`[runtime] Component discovery completed with ${result.errors.length} errors`);
    } else {
      console.log(`[runtime] Component discovery complete: ${result.componentsFound} components in ${result.workflowsScanned} workflows`);
    }
  } else if (startupAction === 'validate') {
    // Just check that indexes exist and are up-to-date
    result = validateIndexes(discovery, workflowsPath);
  }

  // Start watcher in development mode
  const isDevelopment = process.env.NODE_ENV === 'development' || process.env.NODE_ENV === undefined;
  const shouldWatch = discoveryConfig.watch_mode !== false && isDevelopment;

  if (shouldWatch) {
    watcher = new ComponentWatcher(workflowsPath, {
      discoveryOptions,
      onRegenerate: ({ workflow, timestamp }) => {
        console.log(`[runtime] Component index regenerated: ${workflow}`);
      },
      onError: ({ workflow, error }) => {
        console.error(`[runtime] Component discovery error in ${workflow}: ${error.message}`);
      }
    });

    await watcher.start();
    console.log('[runtime] Component watcher started');
  }

  return {
    enabled: true,
    workflowsPath,
    watcher,
    result,
    discovery
  };
}

/**
 * Validates that all index files exist and are up-to-date.
 * Does not modify any files.
 * 
 * @param {WorkflowComponentDiscovery} discovery - Discovery instance
 * @param {string} workflowsPath - Path to workflows directory
 * @returns {Object} Validation result
 */
function validateIndexes(discovery, workflowsPath) {
  const result = {
    valid: true,
    missingIndexes: [],
    outdatedIndexes: [],
    workflowsChecked: 0
  };

  const componentDirs = discovery.discoverComponentDirectories();
  result.workflowsChecked = componentDirs.size;

  for (const [workflowName, componentsDir] of componentDirs) {
    const indexPath = path.join(componentsDir, 'index.js');
    
    if (!fs.existsSync(indexPath)) {
      result.valid = false;
      result.missingIndexes.push(workflowName);
      continue;
    }

    // Check if index is up-to-date
    const componentFiles = discovery.scanComponentFiles(componentsDir);
    const expectedContent = discovery.generateIndexContent(componentFiles);
    const actualContent = fs.readFileSync(indexPath, 'utf8');
    
    // Normalize for comparison (ignore timestamp)
    const normalize = (str) => str.replace(/Generated: .+/g, '');
    
    if (normalize(expectedContent) !== normalize(actualContent)) {
      result.valid = false;
      result.outdatedIndexes.push(workflowName);
    }
  }

  if (!result.valid) {
    console.warn('[runtime] Component indexes are out of date. Run discovery to update.');
    if (result.missingIndexes.length > 0) {
      console.warn(`  Missing: ${result.missingIndexes.join(', ')}`);
    }
    if (result.outdatedIndexes.length > 0) {
      console.warn(`  Outdated: ${result.outdatedIndexes.join(', ')}`);
    }
  }

  return result;
}

/**
 * Cleanup function to stop the watcher.
 * Call this during runtime shutdown.
 * 
 * @param {Object} discoveryState - State returned from initializeComponentDiscovery
 */
export function cleanupComponentDiscovery(discoveryState) {
  if (discoveryState?.watcher) {
    discoveryState.watcher.stop();
    console.log('[runtime] Component watcher stopped');
  }
}

/**
 * Build-time hook for generating component indexes.
 * Call this in your build script before bundling.
 * 
 * @param {string} workflowsPath - Path to workflows directory
 * @param {Object} options - Discovery options
 * @returns {Object} Discovery result
 * 
 * @example
 * // In build script
 * import { generateForBuild } from '@mozaiks/workflow-ui-discovery/runtime';
 * await generateForBuild('./frontend/workflows');
 */
export function generateForBuild(workflowsPath, options = {}) {
  console.log('[build] Generating component indexes...');
  
  const discovery = new WorkflowComponentDiscovery(workflowsPath, options);
  const result = discovery.run();
  
  if (result.errors.length > 0) {
    console.error('[build] Component discovery errors:');
    for (const err of result.errors) {
      console.error(`  - ${err.workflow}: ${err.error}`);
    }
    throw new Error('Component discovery failed');
  }
  
  console.log(`[build] Generated indexes for ${result.workflowsScanned} workflows`);
  return result;
}

/**
 * @typedef {Object} RuntimeDiscoveryResult
 * @property {boolean} enabled - Whether discovery is enabled
 * @property {string} [workflowsPath] - Path to workflows directory
 * @property {ComponentWatcher|null} watcher - Watcher instance (null if not watching)
 * @property {Object|null} result - Discovery result
 * @property {WorkflowComponentDiscovery} [discovery] - Discovery instance
 * @property {string} [error] - Error message if initialization failed
 */

export default initializeComponentDiscovery;
