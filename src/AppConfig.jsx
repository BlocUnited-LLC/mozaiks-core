// src/AppConfig.jsx
import React, { createContext, useState, useEffect, useContext } from 'react';

// Create context for application configuration
const AppConfigContext = createContext(null);

export function AppConfigProvider({ children }) {
  const [config, setConfig] = useState({
    monetizationEnabled: false,
    appName: 'Mozaiks',
    appVersion: '1.0.0',
    env: 'development',
    isLoading: true,
    error: null
  });

  useEffect(() => {
    const fetchAppConfig = async () => {
      try {
        const response = await fetch('/api/app-config');
        if (!response.ok) {
          throw new Error('Failed to fetch app configuration');
        }
        
        const data = await response.json();
        setConfig({
          monetizationEnabled: data.monetization_enabled || false,
          appName: data.app_name || 'Mozaiks',
          appVersion: data.app_version || '1.0.0',
          env: data.env || 'development',
          isLoading: false,
          error: null
        });
      } catch (error) {
        console.error('Error fetching app config:', error);
        setConfig(prev => ({
          ...prev,
          isLoading: false,
          error: error.message
        }));
      }
    };

    fetchAppConfig();
  }, []);

  return (
    <AppConfigContext.Provider value={config}>
      {children}
    </AppConfigContext.Provider>
  );
}

export function useAppConfig() {
  const context = useContext(AppConfigContext);
  if (!context) {
    throw new Error('useAppConfig must be used within an AppConfigProvider');
  }
  return context;
}