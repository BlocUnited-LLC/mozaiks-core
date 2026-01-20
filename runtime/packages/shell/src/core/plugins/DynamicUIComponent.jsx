// /src/core/plugins/DynamicUIComponent.jsx
import React, { Suspense, useState, useEffect } from "react";
import { usePlugins } from "./usePlugins";
import { useAuth } from "../../auth/AuthContext";

const DynamicUIComponent = ({ 
  pluginName, 
  componentName = "default", 
  pluginProps = {},
  fallback = null 
}) => {
  const [Component, setComponent] = useState(null);
  const [isAccessible, setIsAccessible] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const { getComponent, hasPlugin, plugins } = usePlugins();
  const { isAuthenticated, authFetch } = useAuth();

  // Debug output for plugin existence
  useEffect(() => {
    console.log(`Attempting to load plugin: ${pluginName}`);
    console.log(`All plugins:`, plugins);
    console.log(`hasPlugin check result:`, hasPlugin(pluginName));
  }, [pluginName, plugins, hasPlugin]);

  // Check if user has access to this plugin
  useEffect(() => {
    const checkAccess = async () => {
      try {
        // First check if plugin exists
        if (!hasPlugin(pluginName)) {
          console.warn(`Plugin ${pluginName} not found in registered plugins`);
          
          // Additional check - try directly accessing the plugin anyway
          // This bypasses the hasPlugin check which might be failing
          try {
            const module = await import(`@plugins/${pluginName}/index.js`);
            console.log("Successfully loaded plugin module directly:", module);
            setIsAccessible(true);
            setLoading(false);
            return;
          } catch (importErr) {
            console.error(`Failed direct import attempt for ${pluginName}:`, importErr);
            setError(`Plugin ${pluginName} not found or disabled`);
            setIsAccessible(false);
            return;
          }
        }

        // Special case for subscription_manager - always grant access in frontend when monetization is enabled
        if (pluginName === "subscription_manager") {
          // Fetch app config to check if monetization is enabled
          const appConfigResponse = await fetch('/api/app-config');
          if (appConfigResponse.ok) {
            const appConfig = await appConfigResponse.json();
            if (appConfig.monetization_enabled) {
              setIsAccessible(true);
              setLoading(false);
              return;
            }
          }
        }

        // Normal access check for other plugins
        if (!isAuthenticated) {
          setError("Authentication required");
          setIsAccessible(false);
          return;
        }

        const response = await authFetch(`/api/check-plugin-access/${pluginName}`);
        
        if (!response.ok) {
          throw new Error("Failed to check plugin access");
        }

        const data = await response.json();
        console.log(`Access check for ${pluginName}:`, data);
        setIsAccessible(data.access);
        
        if (!data.access) {
          setError(`You don't have access to ${pluginName}`);
        }
      } catch (err) {
        setError(err.message);
        setIsAccessible(false);
        console.error(`Error checking plugin access: ${err}`);
      } finally {
        setLoading(false);
      }
    };

    checkAccess();
  }, [pluginName, hasPlugin, plugins, isAuthenticated, authFetch]);

  // Load component if accessible
  useEffect(() => {
    if (!isAccessible) return;

    // First try to get from registered components
    const registeredComponent = getComponent(pluginName, componentName);
    
    if (registeredComponent) {
      console.log(`Found registered component for ${pluginName}.${componentName}`);
      setComponent(() => registeredComponent);
      return;
    }

    // If not registered, try dynamic import
    const loadComponent = async () => {
      try {
        // Try loading the component directly first
        console.log(`Trying to load component from @plugins/${pluginName}/${componentName}.jsx`);
        const module = await import(`@plugins/${pluginName}/${componentName}.jsx`);
        setComponent(() => module.default);
      } catch (err) {
        console.error(`Error loading specific component, trying index.js: ${err.message}`);
        
        // Fall back to index.js if componentName.jsx doesn't exist
        try {
          const module = await import(`@plugins/${pluginName}/index.js`);
          console.log(`Successfully loaded index.js for ${pluginName}`, module);
          setComponent(() => module.default);
        } catch (indexErr) {
          setError(`Failed to load component: ${indexErr.message}`);
          console.error(`Error loading plugin UI for '${pluginName}':`, indexErr);
        }
      }
    };

    loadComponent();
  }, [isAccessible, pluginName, componentName, getComponent]);

  // Render states
  if (loading) {
    return fallback || (
      <div className="bg-secondary p-4 rounded animate-pulse">
        <div className="h-4 bg-gray-200 rounded w-1/4 mb-2"></div>
        <div className="h-8 bg-gray-200 rounded w-full"></div>
      </div>
    );
  }

  if (error || !isAccessible) {
    return (
      <div className="bg-red-50 border border-red-200 p-4 rounded text-red-600">
        <p className="font-medium">Error</p>
        <p className="text-sm">{error || "Access denied"}</p>
        <p className="text-xs mt-2">Plugin name: {pluginName}</p>
        <p className="text-xs">Available plugins: {plugins.map(p => p.name).join(', ')}</p>
      </div>
    );
  }

  if (!Component) {
    return fallback || (
      <div className="bg-secondary p-4 rounded animate-pulse">
        <div className="h-8 bg-gray-200 rounded w-full"></div>
      </div>
    );
  }

  return (
    <Suspense fallback={fallback || <div className="animate-pulse">Loading...</div>}>
      <Component {...pluginProps} />
    </Suspense>
  );
};

export default DynamicUIComponent;
