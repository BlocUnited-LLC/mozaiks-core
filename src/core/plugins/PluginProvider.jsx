// /src/core/plugins/PluginProvider.jsx
// SECURITY: This provider does NOT handle auth — it only reacts to auth state changes.
import React, { createContext, useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { useAuth } from '../../auth/AuthContext';
import { localTokenStore } from '../../auth/runtime/localTokenStore';
import { getAuthMode } from '../../auth/runtime/authConfig';

export const PluginContext = createContext();

const PLUGIN_REFRESH_INTERVAL = 60000; // Refresh plugins every minute
const PLUGIN_CACHE_KEY = 'mozaiks_plugins_cache';
const PLUGIN_CACHE_EXPIRY = 'mozaiks_plugins_cache_expiry';

/**
 * Provider for plugin-related functionality
 * Handles loading, caching, and registering plugin components
 * 
 * @param {Object} props Component props
 * @param {React.ReactNode} props.children Child components
 */
export function PluginProvider({ children }) {
  const { isAuthenticated, authFetch } = useAuth();
  // State for plugins and loading status
  const [plugins, setPlugins] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [registeredComponents, setRegisteredComponents] = useState({});
  
  // Reference for refresh interval
  const refreshIntervalRef = useRef(null);
  
  // Functions to manage plugins

  /**
   * Fetch the latest plugins from the API
   * Uses caching for improved performance
   */
  const fetchPlugins = useCallback(async (ignoreCache = false) => {
    try {
      if (!isAuthenticated) {
        setPlugins([]);
        setLoading(false);
        return;
      }

      setLoading(true);
      
      // Try to use cached data first if not explicitly ignoring cache
      if (!ignoreCache) {
        const cachedPlugins = localStorage.getItem(PLUGIN_CACHE_KEY);
        const cacheExpiry = localStorage.getItem(PLUGIN_CACHE_EXPIRY);
        
        if (cachedPlugins && cacheExpiry) {
          // Check if cache is still valid
          if (Date.now() < parseInt(cacheExpiry, 10)) {
            try {
              const parsedCache = JSON.parse(cachedPlugins);
              setPlugins(parsedCache || []);
              setLoading(false);
              console.log('Using cached plugins', parsedCache);
              return;
            } catch (parseError) {
              console.error('Error parsing cached plugins:', parseError);
            }
          }
        }
      }
      
      // Fetch from API if no cache or cache expired
      const response = await authFetch('/api/available-plugins', {
        headers: {
          'Cache-Control': 'no-cache'
        }
      });
      
      if (!response.ok) {
        throw new Error(`Failed to fetch plugins: ${response.statusText}`);
      }
      
      const data = await response.json();
      const pluginsList = data.plugins || [];
      
      // Update state with fetched plugins
      setPlugins(pluginsList);
      
      // Cache the plugins for future use
      localStorage.setItem(PLUGIN_CACHE_KEY, JSON.stringify(pluginsList));
      localStorage.setItem(PLUGIN_CACHE_EXPIRY, String(Date.now() + 300000)); // 5 minute cache
      
      console.log('Plugins fetched and cached:', pluginsList);
    } catch (err) {
      setError(err.message);
      console.error('Error fetching plugins:', err);
    } finally {
      setLoading(false);
    }
  }, [authFetch, isAuthenticated]);

  /**
   * Register a component for a plugin
   * 
   * @param {string} pluginName Name of the plugin
   * @param {string} componentName Name of the component
   * @param {React.Component} component Component to register
   */
  const registerComponent = useCallback((pluginName, componentName, component) => {
    setRegisteredComponents(prev => ({
      ...prev,
      [pluginName]: {
        ...(prev[pluginName] || {}),
        [componentName]: component
      }
    }));
  }, []);

  /**
   * Get a registered component
   * 
   * @param {string} pluginName Name of the plugin
   * @param {string} componentName Name of the component
   * @returns {React.Component|null} The component or null if not found
   */
  const getComponent = useCallback((pluginName, componentName) => {
    return registeredComponents[pluginName]?.[componentName] || null;
  }, [registeredComponents]);

  /**
   * Check if a plugin is available
   * 
   * @param {string} name Name of the plugin to check
   * @returns {boolean} Whether the plugin is available
   */
  const hasPlugin = useCallback((name) => {
    return plugins.some(p => p.name === name && p.enabled !== false);
  }, [plugins]);

  /**
   * Get a specific plugin by name
   * 
   * @param {string} name Name of the plugin to get
   * @returns {Object|null} The plugin or null if not found
   */
  const getPlugin = useCallback((name) => {
    return plugins.find(p => p.name === name && p.enabled !== false) || null;
  }, [plugins]);

  /**
   * Refresh plugins from the API
   */
  const refreshPlugins = useCallback(() => {
    console.log('Refreshing plugins from API');
    fetchPlugins(true); // Force refresh from API, ignoring cache
  }, [fetchPlugins]);

  // Initial load of plugins
  useEffect(() => {
    if (!isAuthenticated) {
      setPlugins([]);
      setLoading(false);
      if (refreshIntervalRef.current) {
        clearInterval(refreshIntervalRef.current);
        refreshIntervalRef.current = null;
      }
      return;
    }

    fetchPlugins();

    refreshIntervalRef.current = setInterval(() => {
      fetchPlugins();
    }, PLUGIN_REFRESH_INTERVAL);

    return () => {
      if (refreshIntervalRef.current) {
        clearInterval(refreshIntervalRef.current);
      }
    };
  }, [fetchPlugins, isAuthenticated]);

  // Refresh plugins when auth status changes
  // SECURITY: localTokenStore listener only relevant in local mode
  useEffect(() => {
    const authMode = getAuthMode();
    
    // ISOLATED: Only listen for localTokenStore changes in local mode
    // Platform/external modes use OIDC events, not storage-based tokens
    if (authMode !== 'local') {
      return; // No cleanup needed — listener not registered
    }

    const handleStorageChange = (e) => {
      if (e.key === localTokenStore.key) {
        console.log('Auth token changed, refreshing plugins');
        // Clear plugin cache
        localStorage.removeItem(PLUGIN_CACHE_KEY);
        localStorage.removeItem(PLUGIN_CACHE_EXPIRY);
        // Refresh plugins
        fetchPlugins(true);
      }
    };
    
    window.addEventListener('storage', handleStorageChange);
    
    return () => {
      window.removeEventListener('storage', handleStorageChange);
    };
  }, [fetchPlugins]);

  // Create memoized context value to prevent unnecessary re-renders
  const contextValue = useMemo(() => ({
    plugins,
    loading,
    error,
    registeredComponents,
    registerComponent,
    getComponent,
    hasPlugin,
    getPlugin,
    refreshPlugins,
  }), [plugins, loading, error, registeredComponents, registerComponent, getComponent, hasPlugin, getPlugin, refreshPlugins]);

  return (
    <PluginContext.Provider value={contextValue}>
      {children}
    </PluginContext.Provider>
  );
}
