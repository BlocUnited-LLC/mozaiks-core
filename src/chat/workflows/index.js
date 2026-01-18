// ==============================================================================
// FILE: ChatUI/src/workflows/index.js
// DESCRIPTION: Workflow metadata registry (CLEAN VERSION - API DRIVEN)
// PURPOSE: Fetch workflow metadata from backend API (no duplication!)
// ==============================================================================

/**
 * 🎯 WORKFLOW REGISTRY - API DRIVEN CLEAN VERSION
 * 
 * Registry for workflow metadata fetched from backend API.
 * Single source of truth: json files in backend, accessed via /api/workflows
 * No duplicate configuration files, no hardcoded metadata.
 * 
 * Benefits:
 * - Single source of truth (backend json files)
 * - No duplicate metadata
 * - Real-time configuration updates
 * - Clean separation of concerns
 */

import config from '../config';

class WorkflowRegistry {
  constructor() {
    this.loadedWorkflows = new Map();
    this.initialized = false;
    const baseUrlRaw = typeof config?.get === 'function' ? config.get('api.baseUrl') : undefined;
    const baseUrl = typeof baseUrlRaw === 'string' && baseUrlRaw.endsWith('/') ? baseUrlRaw.slice(0, -1) : (baseUrlRaw || 'http://localhost:8000');
    this.apiBaseUrl = baseUrl.endsWith('/api') ? baseUrl : `${baseUrl}/api`; // Direct backend API base URL
    this.ready = false; // only true after at least one workflow loaded (or cached)
    this.lastError = null;
    this.maxRetries = 5;
    this.retryDelays = [250, 750, 2000, 4000, 8000]; // ms
    this.cacheKey = 'mozaiks_workflows_cache_v1';
  }

  // Load from localStorage cache (best-effort)
  _loadFromCache() {
    try {
      const raw = localStorage.getItem(this.cacheKey);
      if (!raw) return false;
      const parsed = JSON.parse(raw);
      if (!parsed || typeof parsed !== 'object') return false;
      if (!Array.isArray(parsed.workflows)) return false;
      parsed.workflows.forEach(w => {
        if (w && w.name) this.loadedWorkflows.set(w.name, w);
      });
      if (this.loadedWorkflows.size > 0) {
        console.warn('🗃️ WorkflowRegistry: Loaded workflows from cache (offline fallback)');
        this.initialized = true;
        this.ready = true;
        return true;
      }
    } catch (e) {
      console.debug('Cache load failed', e);
    }
    return false;
  }

  _saveToCache() {
    try {
      const payload = { ts: Date.now(), workflows: this.getLoadedWorkflows() };
      localStorage.setItem(this.cacheKey, JSON.stringify(payload));
    } catch (e) {
      console.debug('Cache save skipped', e);
    }
  }

  async _fetchWorkflowsOnce(signal) {
    const response = await fetch(`${this.apiBaseUrl}/workflows`, { signal });
    if (!response.ok) {
      throw new Error(`API request failed: ${response.status} ${response.statusText}`);
    }
    return await response.json();
  }

  async _fetchWithRetries() {
    let attempt = 0;
    let controller;
    while (attempt < this.maxRetries) {
      controller = new AbortController();
      const signal = controller.signal;
      try {
        if (attempt > 0) {
          console.log(`🔁 WorkflowRegistry: Retry attempt ${attempt + 1}/${this.maxRetries}`);
        }
        const json = await this._fetchWorkflowsOnce(signal);
        return json;
      } catch (err) {
        this.lastError = err;
        if (attempt === this.maxRetries - 1) break;
        const delay = this.retryDelays[Math.min(attempt, this.retryDelays.length - 1)];
        await new Promise(res => setTimeout(res, delay));
        attempt += 1;
        continue;
      }
    }
    throw this.lastError || new Error('Unknown workflow fetch failure');
  }

  /**
   * Initialize all workflows by fetching from backend API
   */
  async initializeWorkflows({ allowCacheFallback = true } = {}) {
    if (this.initialized) {
      console.log('⏭️ WorkflowRegistry: Already initialized');
      return this.getWorkflowSummary();
    }

    console.log('🚀 WorkflowRegistry: Fetching workflows from backend API...');

    // Try live fetch with retries
    try {
      const workflowConfigs = await this._fetchWithRetries();
      // Process each workflow configuration
      for (const [workflowName, config] of Object.entries(workflowConfigs)) {
        const workflowInfo = {
          name: workflowName,
          displayName: config.name || workflowName,
          description: `${config.name || workflowName} workflow`,
          version: '1.0.0',
          // Build structured outputs mapping for agents (agentName -> bool)
          structuredOutputs: (() => {
            const map = {};
            try {
              const so = config.structured_outputs || {};
              const registry = so.registry || so.models || so || {};
              if (Array.isArray(registry)) {
                registry.forEach(a => { if (typeof a === 'string') map[a] = true; else if (a && a.name) map[a.name] = true; });
              } else if (typeof registry === 'object') {
                Object.keys(registry).forEach(k => { map[k] = true; });
              }
            } catch (e) { /* ignore parsing errors */ }
            return map;
          })(),
          metadata: {
            maxTurns: config.max_turns,
            humanInTheLoop: config.human_in_the_loop,
            startupMode: config.startup_mode,
            orchestrationPattern: config.orchestration_pattern,
            chatPaneAgents: config.chat_pane_agents || [],
            artifactAgents: config.artifact_agents || [],
            initialMessage: config.initial_message,
            uiTools: config.ui_tools || {}
          },
          visualAgents: Array.isArray(config.visual_agents) ? config.visual_agents : [],
          tools: config.tools || {},
          loadedAt: new Date().toISOString()
        };
        
        this.loadedWorkflows.set(workflowName, workflowInfo);
        console.log(`✅ Loaded workflow from API: ${workflowName}`);
      }

      this.initialized = true;
      this.ready = this.loadedWorkflows.size > 0;
      if (this.ready) this._saveToCache();
      console.log(`✅ WorkflowRegistry: Loaded ${this.loadedWorkflows.size} workflows from backend`);
      
      return this.getWorkflowSummary();

    } catch (error) {
      console.error('❌ WorkflowRegistry: Failed to fetch workflows from API after retries:', error);
      if (allowCacheFallback) {
        const cached = this._loadFromCache();
        if (cached) {
          console.warn('⚠️ Using cached workflows; backend unavailable');
          return this.getWorkflowSummary();
        }
      }
      // Still failed: mark initialized=false but ready=false and rethrow for UI layer to display banner
      this.initialized = false;
      this.ready = false;
      throw error;
    }
  }

  /**
   * Get all loaded workflows
   * @returns {Array} - Array of workflow info objects
   */
  getLoadedWorkflows() {
    return Array.from(this.loadedWorkflows.values());
  }

  /**
   * Get specific workflow info
   * @param {string} workflowName - Name of the workflow
   * @returns {Object|null} - Workflow info or null
   */
  getWorkflow(workflowName) {
    return this.loadedWorkflows.get(workflowName) || null;
  }

  /**
   * Get workflow summary for debugging
   * @returns {Object} - Summary of all workflows
   */
  getWorkflowSummary() {
    return {
      initialized: this.initialized,
      ready: this.ready,
      workflowCount: this.loadedWorkflows.size,
      workflows: this.getLoadedWorkflows().map(w => ({
        name: w.name,
        displayName: w.displayName,
        version: w.version,
  agentCount: Array.isArray(w.visualAgents) ? w.visualAgents.length : 0,
        hasHumanInLoop: w.metadata.humanInTheLoop
      }))
    };
  }

  /**
   * Refresh workflows from backend (useful for development)
   */
  async refresh() {
    console.log('🔄 WorkflowRegistry: Refreshing workflows from backend...');
    this.clear();
    return await this.initializeWorkflows();
  }

  /**
   * Clear all loaded workflows
   */
  clear() {
    const count = this.loadedWorkflows.size;
    this.loadedWorkflows.clear();
    this.initialized = false;
    this.ready = false;
    console.log(`🧹 WorkflowRegistry: Cleared ${count} workflows`);
  }

  /**
   * Get registry statistics
   * @returns {Object} - Registry stats
   */
  getStats() {
    return {
      initialized: this.initialized,
      ready: this.ready,
      loadedWorkflows: this.loadedWorkflows.size,
      workflowNames: Array.from(this.loadedWorkflows.keys()),
      apiEndpoint: `${this.apiBaseUrl}/workflows`
    };
  }
}

// Create singleton instance
const workflowRegistry = new WorkflowRegistry();

// Export both the instance and convenience methods
export default workflowRegistry;

export const initializeWorkflows = (opts) => workflowRegistry.initializeWorkflows(opts);
export const getLoadedWorkflows = () => workflowRegistry.getLoadedWorkflows();
export const getWorkflow = (name) => workflowRegistry.getWorkflow(name);
export const getWorkflowSummary = () => workflowRegistry.getWorkflowSummary();
export const refreshWorkflows = () => workflowRegistry.refresh();
