/**
 * @fileoverview File watcher for automatic component discovery in development mode.
 * 
 * Watches for changes in workflow component directories and automatically
 * regenerates index.js files when components are added, removed, or renamed.
 * 
 * @module @mozaiks/workflow-ui-discovery/watcher
 */

import fs from 'fs';
import path from 'path';
import { WorkflowComponentDiscovery } from './discovery.js';

/**
 * Debounce function to prevent rapid successive calls.
 * 
 * @param {Function} fn - Function to debounce
 * @param {number} delay - Delay in milliseconds
 * @returns {Function} Debounced function
 */
function debounce(fn, delay) {
  let timeoutId = null;
  return (...args) => {
    if (timeoutId) {
      clearTimeout(timeoutId);
    }
    timeoutId = setTimeout(() => {
      fn(...args);
      timeoutId = null;
    }, delay);
  };
}

/**
 * ComponentWatcher - Watches workflow directories for changes and auto-regenerates indexes.
 */
export class ComponentWatcher {
  /**
   * @param {string} workflowsRoot - Absolute path to the workflows directory
   * @param {Object} options - Watcher options
   * @param {number} [options.debounceMs=300] - Debounce delay in milliseconds
   * @param {Function} [options.onRegenerate] - Callback when an index is regenerated
   * @param {Function} [options.onError] - Callback when an error occurs
   * @param {Object} [options.discoveryOptions] - Options to pass to WorkflowComponentDiscovery
   */
  constructor(workflowsRoot, options = {}) {
    this.workflowsRoot = workflowsRoot;
    this.debounceMs = options.debounceMs ?? 300;
    this.onRegenerate = options.onRegenerate ?? (() => {});
    this.onError = options.onError ?? ((err) => console.error(err));
    this.discoveryOptions = options.discoveryOptions ?? {};
    
    this.discovery = new WorkflowComponentDiscovery(workflowsRoot, this.discoveryOptions);
    this.watchers = new Map(); // path -> FSWatcher
    this.isRunning = false;
    
    // Debounced regeneration per workflow
    this.debouncedRegenerate = new Map(); // workflowName -> debounced function
  }

  /**
   * Gets or creates a debounced regeneration function for a workflow.
   * 
   * @param {string} workflowName - Name of the workflow
   * @returns {Function} Debounced regeneration function
   */
  getRegenerator(workflowName) {
    if (!this.debouncedRegenerate.has(workflowName)) {
      const regenerate = debounce(() => {
        try {
          const updated = this.discovery.regenerateWorkflow(workflowName);
          if (updated) {
            this.onRegenerate({ workflow: workflowName, timestamp: new Date() });
          }
        } catch (error) {
          this.onError({ workflow: workflowName, error });
        }
      }, this.debounceMs);
      
      this.debouncedRegenerate.set(workflowName, regenerate);
    }
    
    return this.debouncedRegenerate.get(workflowName);
  }

  /**
   * Extracts the workflow name from a file path.
   * 
   * @param {string} filePath - The changed file path
   * @returns {string|null} The workflow name, or null if not in a workflow
   */
  extractWorkflowName(filePath) {
    const relative = path.relative(this.workflowsRoot, filePath);
    const parts = relative.split(path.sep);
    
    if (parts.length >= 2 && parts[1] === 'components') {
      return parts[0];
    }
    
    return null;
  }

  /**
   * Handles a file change event.
   * 
   * @param {string} eventType - 'rename' or 'change'
   * @param {string} filename - The changed filename (relative to watched dir)
   * @param {string} watchedDir - The directory being watched
   */
  handleChange(eventType, filename, watchedDir) {
    if (!filename) return;
    
    const ext = path.extname(filename);
    const componentExtensions = this.discoveryOptions.componentExtensions ?? ['.js', '.jsx', '.ts', '.tsx'];
    const workflowConfigFile = this.discoveryOptions.workflowConfigFile ?? 'theme_config.json';
    
    // Check if it's a workflow config file change
    if (filename === workflowConfigFile) {
      const workflowName = path.basename(watchedDir);
      console.log(`[watcher] Workflow config changed in ${workflowName}: ${filename} (${eventType})`);
      const regenerate = this.getRegenerator(workflowName);
      regenerate();
      return;
    }
    
    // Only care about component file changes
    if (!componentExtensions.includes(ext)) return;
    if (filename === 'index.js' || filename === 'index.ts') return;
    
    // Extract workflow name from the watched directory
    const workflowName = this.extractWorkflowName(watchedDir);
    
    if (workflowName) {
      console.log(`[watcher] Change detected in ${workflowName}: ${filename} (${eventType})`);
      const regenerate = this.getRegenerator(workflowName);
      regenerate();
    }
  }

  /**
   * Starts watching for changes.
   * 
   * @returns {Promise<void>}
   */
  async start() {
    if (this.isRunning) {
      console.warn('[watcher] Already running');
      return;
    }

    console.log(`[watcher] Starting component watcher for: ${this.workflowsRoot}`);

    // Run initial discovery
    const result = this.discovery.run();
    
    if (result.errors.length > 0) {
      for (const err of result.errors) {
        this.onError(err);
      }
    }

    // Set up watchers for each workflow's components directory
    const componentDirs = this.discovery.discoverComponentDirectories();
    
    for (const [workflowName, componentsDir] of componentDirs) {
      this.watchDirectory(workflowName, componentsDir);
      
      // Also watch the workflow root for theme_config.json changes
      const workflowRoot = path.dirname(componentsDir);
      this.watchWorkflowRoot(workflowName, workflowRoot);
    }

    // Also watch for new workflow directories being created
    this.watchForNewWorkflows();

    this.isRunning = true;
    console.log(`[watcher] Watching ${this.watchers.size} directories`);
  }

  /**
   * Sets up a watcher for a specific components directory.
   * 
   * @param {string} workflowName - Name of the workflow
   * @param {string} componentsDir - Path to the components directory
   */
  watchDirectory(workflowName, componentsDir) {
    if (this.watchers.has(componentsDir)) {
      return; // Already watching
    }

    try {
      const watcher = fs.watch(
        componentsDir,
        { persistent: true },
        (eventType, filename) => {
          this.handleChange(eventType, filename, componentsDir);
        }
      );

      watcher.on('error', (error) => {
        console.error(`[watcher] Error watching ${componentsDir}: ${error.message}`);
        this.onError({ workflow: workflowName, error });
      });

      this.watchers.set(componentsDir, watcher);
      console.log(`[watcher] Watching: ${workflowName}/components/`);
    } catch (error) {
      console.error(`[watcher] Failed to watch ${componentsDir}: ${error.message}`);
      this.onError({ workflow: workflowName, error });
    }
  }

  /**
   * Sets up a watcher for a workflow root directory (for theme_config.json changes).
   * 
   * @param {string} workflowName - Name of the workflow
   * @param {string} workflowRoot - Path to the workflow root directory
   */
  watchWorkflowRoot(workflowName, workflowRoot) {
    const watchKey = `${workflowRoot}:root`;
    if (this.watchers.has(watchKey)) {
      return; // Already watching
    }

    try {
      const watcher = fs.watch(
        workflowRoot,
        { persistent: true },
        (eventType, filename) => {
          this.handleChange(eventType, filename, workflowRoot);
        }
      );

      watcher.on('error', (error) => {
        console.error(`[watcher] Error watching ${workflowRoot}: ${error.message}`);
        this.onError({ workflow: workflowName, error });
      });

      this.watchers.set(watchKey, watcher);
      console.log(`[watcher] Watching: ${workflowName}/ (for config changes)`);
    } catch (error) {
      console.error(`[watcher] Failed to watch ${workflowRoot}: ${error.message}`);
      this.onError({ workflow: workflowName, error });
    }
  }

  /**
   * Watches for new workflow directories being created.
   */
  watchForNewWorkflows() {
    try {
      const rootWatcher = fs.watch(
        this.workflowsRoot,
        { persistent: true },
        debounce((eventType, filename) => {
          if (!filename) return;
          
          const potentialWorkflowDir = path.join(this.workflowsRoot, filename);
          const componentsDir = path.join(potentialWorkflowDir, 'components');
          
          // Check if this is a new workflow with a components directory
          if (fs.existsSync(componentsDir) && !this.watchers.has(componentsDir)) {
            console.log(`[watcher] New workflow detected: ${filename}`);
            this.watchDirectory(filename, componentsDir);
            
            // Run discovery for the new workflow
            this.discovery.regenerateWorkflow(filename);
          }
        }, 500)
      );

      rootWatcher.on('error', (error) => {
        console.error(`[watcher] Error watching workflows root: ${error.message}`);
      });

      this.watchers.set(this.workflowsRoot, rootWatcher);
    } catch (error) {
      console.error(`[watcher] Failed to watch workflows root: ${error.message}`);
    }
  }

  /**
   * Stops all watchers.
   */
  stop() {
    console.log('[watcher] Stopping component watcher...');
    
    for (const [watchPath, watcher] of this.watchers) {
      watcher.close();
    }
    
    this.watchers.clear();
    this.debouncedRegenerate.clear();
    this.isRunning = false;
    
    console.log('[watcher] Stopped');
  }

  /**
   * Gets the current status of the watcher.
   * 
   * @returns {Object} Status object
   */
  getStatus() {
    return {
      isRunning: this.isRunning,
      watchedDirectories: this.watchers.size,
      discoveredWorkflows: Object.keys(this.discovery.discoveredComponents).length
    };
  }
}

export default ComponentWatcher;
