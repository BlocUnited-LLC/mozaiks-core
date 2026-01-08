// /src/core/plugins/usePlugins.js
import { useContext, useCallback, useMemo } from 'react';
import { PluginContext } from './PluginProvider';
import { useAuth } from '../../auth/AuthContext';

/**
 * Custom hook to access plugin functionality
 * Provides access to plugins and related operations
 * 
 * @returns {Object} Plugin context with helper functions
 */
export function usePlugins() {
  const context = useContext(PluginContext);
  const { isAuthenticated, authFetch } = useAuth();
  
  if (!context) {
    throw new Error('usePlugins must be used within a PluginProvider');
  }
  
  // Enhanced helpers that build on the context
  
  /**
   * Check if a specific plugin is loading
   * 
   * @returns {boolean} Whether plugins are currently loading
   */
  const isLoading = context.loading;
  
  /**
   * Get all available plugins
   * 
   * @returns {Array} List of available plugins
   */
  const availablePlugins = useMemo(() => context.plugins || [], [context.plugins]);
  
  /**
   * Get plugin names as a simple array
   * 
   * @returns {Array<string>} List of plugin names
   */
  const pluginNames = useMemo(() => 
    (context.plugins || []).map(plugin => plugin.name), 
    [context.plugins]
  );
  
  /**
   * Execute a plugin operation
   * 
   * @param {string} pluginName Name of the plugin to execute
   * @param {Object} data Data to send to the plugin
   * @returns {Promise<Object>} Result from the plugin execution
   */
  const executePlugin = useCallback(async (pluginName, data = {}) => {
    if (!context.hasPlugin(pluginName)) {
      throw new Error(`Plugin "${pluginName}" not found or not accessible`);
    }
    
    if (!isAuthenticated) {
      throw new Error('Authentication required to execute plugin');
    }
    
    try {
      const response = await authFetch(`/api/execute/${pluginName}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(data)
      });
      
      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(errorText || `Failed to execute plugin: ${response.statusText}`);
      }
      
      return await response.json();
    } catch (error) {
      console.error(`Error executing plugin ${pluginName}:`, error);
      throw error;
    }
  }, [context.hasPlugin, isAuthenticated, authFetch]);
  
  /**
   * Get plugin settings
   * 
   * @param {string} pluginName Name of the plugin
   * @returns {Promise<Object>} Plugin settings
   */
  const getPluginSettings = useCallback(async (pluginName) => {
    if (!isAuthenticated) {
      throw new Error('Authentication required to get plugin settings');
    }
    
    try {
      const response = await authFetch(`/api/plugin-settings/${pluginName}`);
      
      if (!response.ok) {
        throw new Error(`Failed to get plugin settings: ${response.statusText}`);
      }
      
      return await response.json();
    } catch (error) {
      console.error(`Error getting settings for plugin ${pluginName}:`, error);
      throw error;
    }
  }, [isAuthenticated, authFetch]);
  
  /**
   * Save plugin settings
   * 
   * @param {string} pluginName Name of the plugin
   * @param {Object} settings Settings to save
   * @returns {Promise<Object>} Result of the save operation
   */
  const savePluginSettings = useCallback(async (pluginName, settings) => {
    if (!isAuthenticated) {
      throw new Error('Authentication required to save plugin settings');
    }
    
    try {
      const response = await authFetch(`/api/plugin-settings/${pluginName}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(settings)
      });
      
      if (!response.ok) {
        throw new Error(`Failed to save plugin settings: ${response.statusText}`);
      }
      
      return await response.json();
    } catch (error) {
      console.error(`Error saving settings for plugin ${pluginName}:`, error);
      throw error;
    }
  }, [isAuthenticated, authFetch]);
  
  // Return enhanced context
  return {
    ...context,
    isLoading,
    availablePlugins,
    pluginNames,
    executePlugin,
    getPluginSettings,
    savePluginSettings
  };
}
