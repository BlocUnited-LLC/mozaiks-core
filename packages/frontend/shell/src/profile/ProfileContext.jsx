// /src/profile/ProfileContext.jsx
import React, { createContext, useState, useEffect, useContext } from 'react';
import { useAuth } from '../auth/AuthContext';

const ProfileContext = createContext(null);

export const ProfileProvider = ({ children }) => {
  const { isAuthenticated, user, authFetch } = useAuth();
  const [profile, setProfile] = useState(null);
  const [settingsConfig, setSettingsConfig] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [isSaving, setIsSaving] = useState(false);
  const [pluginSettings, setPluginSettings] = useState({});

  // Load user profile and settings configuration
  useEffect(() => {
    const loadProfileData = async () => {
      if (!isAuthenticated) {
        setIsLoading(false);
        return;
      }

      setIsLoading(true);
      setError(null);

      try {
        // Fetch settings configuration
        const configResponse = await authFetch('/api/settings-config');

        if (!configResponse.ok) {
          throw new Error('Failed to fetch settings configuration');
        }

        const configData = await configResponse.json();
        setSettingsConfig(configData.profile_sections || []);

        // Fetch user profile data
        const profileResponse = await authFetch('/api/user-profile');

        if (!profileResponse.ok) {
          throw new Error('Failed to fetch user profile');
        }

        const profileData = await profileResponse.json();
        setProfile(profileData);
        
        // Initialize plugin settings if available in profile
        if (profileData.plugin_settings) {
          setPluginSettings(profileData.plugin_settings);
        }
      } catch (err) {
        console.error('Error fetching profile data:', err);
        setError(err.message);
      } finally {
        setIsLoading(false);
      }
    };

    loadProfileData();
  }, [isAuthenticated, user, authFetch]);

  // Update user profile
  const updateProfile = async (updatedData) => {
    if (!isAuthenticated || !profile) return false;

    setIsSaving(true);
    setError(null);

    try {
      const response = await authFetch('/api/update-profile', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(updatedData)
      });

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.detail || 'Failed to update profile');
      }

      // Update local state with the new data
      setProfile(prev => ({ ...prev, ...updatedData }));
      return true;
    } catch (err) {
      console.error('Error updating profile:', err);
      setError(err.message);
      return false;
    } finally {
      setIsSaving(false);
    }
  };

  // Load plugin settings
  const getPluginSettings = async (pluginName) => {
    if (!isAuthenticated) return null;
    
    try {
      // Check if we already have settings cached
      if (pluginSettings[pluginName]) {
        return pluginSettings[pluginName];
      }
      
      // Fetch from API if not cached
      const response = await authFetch(`/api/plugin-settings/${pluginName}`);
      
      if (!response.ok) {
        throw new Error(`Failed to fetch settings for plugin ${pluginName}`);
      }
      
      const settings = await response.json();
      
      // Update cache
      setPluginSettings(prev => ({
        ...prev,
        [pluginName]: settings
      }));
      
      return settings;
    } catch (err) {
      console.error(`Error fetching settings for plugin ${pluginName}:`, err);
      return {};
    }
  };
  
  // Save plugin settings
  const updatePluginSettings = async (pluginName, settings) => {
    if (!isAuthenticated) return false;
    
    setIsSaving(true);
    setError(null);
    
    try {
      const response = await authFetch(`/api/plugin-settings/${pluginName}`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(settings)
      });
      
      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.detail || `Failed to update settings for plugin ${pluginName}`);
      }
      
      // Update local state with the new settings
      setPluginSettings(prev => ({
        ...prev,
        [pluginName]: settings
      }));
      
      return true;
    } catch (err) {
      console.error(`Error updating settings for plugin ${pluginName}:`, err);
      setError(err.message);
      return false;
    } finally {
      setIsSaving(false);
    }
  };

  const value = {
    profile,
    settingsConfig,
    pluginSettings,
    isLoading,
    error,
    isSaving,
    updateProfile,
    getPluginSettings,
    updatePluginSettings
  };

  return <ProfileContext.Provider value={value}>{children}</ProfileContext.Provider>;
};

export const useProfile = () => {
  const context = useContext(ProfileContext);
  if (!context) {
    throw new Error('useProfile must be used within a ProfileProvider');
  }
  return context;
};
