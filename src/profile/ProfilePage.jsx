// /src/profile/ProfilePage.jsx
import React, { useState, useCallback, useEffect } from 'react';
import { useProfile } from './ProfileContext';
import { useTheme } from '../core/theme/useTheme';
import { useNotifications } from '../notifications/NotificationsContext';
import DynamicUIComponent from '../core/plugins/DynamicUIComponent';

const ProfilePage = () => {
  const { 
    profile, 
    settingsConfig, 
    isLoading, 
    error, 
    isSaving, 
    updateProfile,
    pluginSettings,
    getPluginSettings,
    updatePluginSettings
  } = useProfile();
  
  const {
    config: notificationsConfig,
    preferences: notificationPreferences,
    updatePreferences: updateNotificationPreferences,
    isLoading: notificationsLoading
  } = useNotifications();
  
  const { branding, currentTheme, setTheme } = useTheme();
  const [activeSection, setActiveSection] = useState('personal');
  const [formData, setFormData] = useState({});
  const [loadedPluginSettings, setLoadedPluginSettings] = useState({});
  const [formErrors, setFormErrors] = useState({});
  const [successMessage, setSuccessMessage] = useState(null);
  const [loadingPluginSettings, setLoadingPluginSettings] = useState(false);

  // Initialize form data when profile loads
  useEffect(() => {
    if (profile) {
      setFormData(profile);
    }
  }, [profile]);

  // Load plugin settings when needed
  useEffect(() => {
    const currentSection = settingsConfig.find(s => s.id === activeSection);
    if (!currentSection) return;

    const pluginSettingsFields = currentSection.fields.filter(f => f.type === 'plugin-settings');
    
    if (pluginSettingsFields.length > 0) {
      setLoadingPluginSettings(true);
      
      const loadSettings = async () => {
        const settingsObj = {};
        
        for (const field of pluginSettingsFields) {
          const pluginName = field.plugin;
          
          // Check if we already have these settings
          if (pluginSettings[pluginName]) {
            settingsObj[pluginName] = pluginSettings[pluginName];
          } else {
            // Load settings from API
            try {
              const settings = await getPluginSettings(pluginName);
              settingsObj[pluginName] = settings;
            } catch (err) {
              console.error(`Error loading settings for plugin ${pluginName}:`, err);
              settingsObj[pluginName] = {};
            }
          }
        }
        
        setLoadedPluginSettings(settingsObj);
        setLoadingPluginSettings(false);
      };
      
      loadSettings();
    }
  }, [activeSection, settingsConfig, getPluginSettings, pluginSettings]);

  // Handle input changes
  const handleChange = (field, value) => {
    setFormData(prev => ({ ...prev, [field]: value }));

    // Clear any errors for this field
    if (formErrors[field]) {
      setFormErrors(prev => {
        const newErrors = { ...prev };
        delete newErrors[field];
        return newErrors;
      });
    }
    
    // Clear success message when user starts editing
    if (successMessage) {
      setSuccessMessage(null);
    }
  };

  // Handle plugin settings changes
  const handlePluginSettingsChange = (pluginName, settings) => {
    setLoadedPluginSettings(prev => ({
      ...prev,
      [pluginName]: settings
    }));
    
    // Clear success message when user starts editing
    if (successMessage) {
      setSuccessMessage(null);
    }
  };

  // Handle section change
  const handleSectionChange = (sectionId) => {
    setActiveSection(sectionId);
  };

  // Handle form submission
  const handleSubmit = async (e) => {
    e.preventDefault();
    
    // If we're in the notifications section, handle notification preferences
    if (activeSection === 'notifications') {
      try {
        const success = await updateNotificationPreferences(formData.notification_preferences || {});
        
        if (success) {
          setSuccessMessage('Notification preferences updated successfully');
          setTimeout(() => setSuccessMessage(null), 3000);
        }
      } catch (err) {
        console.error('Error updating notification preferences:', err);
        setFormErrors({ general: 'Failed to update notification preferences' });
      }
      return;
    }
    
    // If we're in a section with plugin settings
    const currentSection = settingsConfig.find(s => s.id === activeSection);
    const hasPluginSettings = currentSection?.fields?.some(f => f.type === 'plugin-settings');
    
    if (hasPluginSettings) {
      try {
        // Update all plugin settings in this section
        const pluginsToUpdate = currentSection.fields
          .filter(f => f.type === 'plugin-settings')
          .map(f => f.plugin);
        
        let updateSuccess = true;
        
        for (const plugin of pluginsToUpdate) {
          if (loadedPluginSettings[plugin]) {
            const success = await updatePluginSettings(plugin, loadedPluginSettings[plugin]);
            if (!success) {
              updateSuccess = false;
            }
          }
        }
        
        if (updateSuccess) {
          setSuccessMessage('Plugin settings updated successfully');
          setTimeout(() => setSuccessMessage(null), 3000);
        } else {
          setFormErrors({ general: 'Some plugin settings failed to update' });
        }
      } catch (err) {
        console.error('Error updating plugin settings:', err);
        setFormErrors({ general: 'Failed to update plugin settings' });
      }
      return;
    }
    
    // Otherwise, handle regular profile updates
    // Validate required fields for the active section
    const requiredFields = currentSection?.fields
      .filter(f => f.required && f.editable)
      .map(f => f.id) || [];
    
    const newErrors = {};
    requiredFields.forEach(field => {
      if (!formData[field]) {
        newErrors[field] = 'This field is required';
      }
    });
    
    if (Object.keys(newErrors).length > 0) {
      setFormErrors(newErrors);
      return;
    }
    
    // Get only the changed values for this section
    const fieldsInSection = currentSection?.fields.map(f => f.id) || [];
    const updates = {};
    
    fieldsInSection.forEach(field => {
      if (formData[field] !== profile[field]) {
        updates[field] = formData[field];
      }
    });
    
    if (Object.keys(updates).length === 0) {
      setSuccessMessage('No changes to save');
      return;
    }
    
    const success = await updateProfile(updates);
    
    if (success) {
      setSuccessMessage('Profile updated successfully');
      setTimeout(() => setSuccessMessage(null), 3000);
    }
  };

  // Custom handler for theme toggle
  const handleThemeToggle = useCallback(() => {
    // Choose the next theme (if you have multiple themes)
    const availableThemes = ['light', 'dark', 'purple']; 
    const currentIndex = availableThemes.indexOf(currentTheme);
    const nextTheme = availableThemes[(currentIndex + 1) % availableThemes.length];
    
    // Update the theme through the theme provider
    setTheme(nextTheme);
    
    // Also update the form data 
    setFormData(prev => ({ ...prev, theme: nextTheme }));
    
    // Clear success message
    if (successMessage) {
      setSuccessMessage(null);
    }
  }, [currentTheme, setTheme, successMessage]);

  // Handle notification preference change
  const handleNotificationToggle = (notificationId, isEnabled) => {
    setFormData(prev => ({
      ...prev,
      notification_preferences: {
        ...(prev.notification_preferences || {}),
        [notificationId]: {
          ...(prev.notification_preferences?.[notificationId] || {}),
          enabled: isEnabled
        }
      }
    }));
    
    // Clear success message when user starts editing
    if (successMessage) {
      setSuccessMessage(null);
    }
  };

  // Loading state
  if (isLoading || notificationsLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background">
        <div className="text-center">
          <svg className="animate-spin h-10 w-10 text-accent mx-auto" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
          </svg>
          <p className="mt-2 text-text-primary">Loading profile information...</p>
        </div>
      </div>
    );
  }

  if (!profile || !settingsConfig.length) {
    return (
      <div className="container mx-auto p-6 max-w-4xl bg-background">
        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded">
          <p className="font-bold">Error</p>
          <p>{error || "Failed to load profile information"}</p>
        </div>
      </div>
    );
  }

  // Get current section
  const currentSection = settingsConfig.find(s => s.id === activeSection) || settingsConfig[0];

  // Prepare notification preferences for the form if we're in that section
  if (activeSection === 'notifications' && !formData.notification_preferences) {
    setFormData(prev => ({
      ...prev,
      notification_preferences: notificationPreferences || {}
    }));
  }

  return (
    <div className="container mx-auto p-6 max-w-4xl bg-background">
      <header className="mb-8 text-center">
        <h1 className="text-3xl font-bold text-text-primary mb-2">
          Profile Settings
        </h1>
        <p className="text-text-secondary">
          Manage your {branding.app_name || "Mozaiks"} profile
        </p>
      </header>
      
      {error && (
        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded mb-6">
          <p className="font-bold">Error</p>
          <p>{error}</p>
        </div>
      )}
      
      {formErrors.general && (
        <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded mb-6">
          <p>{formErrors.general}</p>
        </div>
      )}
      
      {successMessage && (
        <div className="bg-green-100 border border-green-400 text-green-700 px-4 py-3 rounded mb-6 animate-fadeIn">
          <p>{successMessage}</p>
        </div>
      )}
      
      <div className="flex flex-col md:flex-row gap-6">
        {/* Sidebar */}
        <aside className="md:w-64 bg-primary rounded-lg p-4 shadow-md h-fit sticky top-6">
          <nav>
            <ul className="space-y-1">
              {settingsConfig
                .filter(section => section.visible)
                .sort((a, b) => a.order - b.order)
                .map(section => (
                  <li key={section.id}>
                    <button
                      onClick={() => handleSectionChange(section.id)}
                      className={`w-full text-left px-4 py-3 rounded flex items-center relative transition-all duration-200 ${
                        activeSection === section.id 
                          ? 'bg-accent text-white font-medium shadow-sm' 
                          : 'hover:bg-secondary text-text-primary'
                      }`}
                    >
                      {/* Left highlight bar for active section */}
                      {activeSection === section.id && (
                        <span className="absolute left-0 top-0 bottom-0 w-1 bg-white rounded-l"></span>
                      )}
                      <span className="mr-3">
                        {renderIcon(section.icon)}
                      </span>
                      {section.title}
                    </button>
                  </li>
                ))
              }
            </ul>
          </nav>
        </aside>
        
        {/* Main content */}
        <div className="flex-1">
          <div className="bg-primary rounded-lg shadow-md overflow-hidden">
            <div className="p-6 border-b border-gray-200 bg-secondary bg-opacity-30">
              <h2 className="text-xl font-semibold text-text-primary flex items-center">
                <span className="mr-3 text-accent">
                  {renderIcon(currentSection.icon)}
                </span>
                {currentSection.title}
              </h2>
            </div>
            
            <form onSubmit={handleSubmit}>
              <div className="p-6 space-y-6">
                {loadingPluginSettings && (
                  <div className="flex justify-center items-center py-4">
                    <svg className="animate-spin h-6 w-6 text-accent" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                    </svg>
                    <span className="ml-2 text-text-secondary">Loading plugin settings...</span>
                  </div>
                )}
                
                {!loadingPluginSettings && activeSection === 'notifications' ? (
                  renderNotificationSettings()
                ) : (
                  !loadingPluginSettings && currentSection.fields
                    .filter(field => field.visible !== false)
                    .map(field => renderFormField(
                      field, 
                      formData[field.id] || '', 
                      formErrors[field.id], 
                      field.editable && handleChange,
                      currentTheme,
                      handleThemeToggle,
                      loadedPluginSettings,
                      handlePluginSettingsChange
                    ))
                )}
              </div>
              
              <div className="px-6 py-4 bg-secondary bg-opacity-20 border-t border-gray-200 flex justify-end">
                <button
                  type="submit"
                  disabled={isSaving || loadingPluginSettings || (!currentSection.editable && activeSection !== 'notifications' && !currentSection.fields?.some(f => f.type === 'plugin-settings'))}
                  className={`px-6 py-2 rounded-md font-medium transition-all duration-200 ${
                    isSaving || loadingPluginSettings || (!currentSection.editable && activeSection !== 'notifications' && !currentSection.fields?.some(f => f.type === 'plugin-settings'))
                      ? 'bg-gray-300 text-gray-500 cursor-not-allowed'
                      : 'bg-accent text-white hover:bg-opacity-90 shadow-sm hover:shadow'
                  }`}
                >
                  {isSaving ? (
                    <span className="flex items-center">
                      <svg className="animate-spin -ml-1 mr-2 h-4 w-4 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                      </svg>
                      Saving...
                    </span>
                  ) : 'Save Changes'}
                </button>
              </div>
            </form>
          </div>
        </div>
      </div>
    </div>
  );

  // Helper function to render notification settings
  function renderNotificationSettings() {
    const categories = notificationsConfig?.categories || [];
    const notifications = notificationsConfig?.notifications || [];
    
    // If no notification config is available
    if (categories.length === 0 || notifications.length === 0) {
      return (
        <div className="p-4 text-text-secondary text-center">
          <p>No notification settings available</p>
        </div>
      );
    }

    return (
      <div className="space-y-6">
        {categories.map(category => {
          const categoryNotifications = notifications.filter(n => n.category === category.id);
          
          if (categoryNotifications.length === 0) return null;
          
          return (
            <div key={category.id} className="mb-6">
              <h3 className="text-lg font-medium text-text-primary mb-2 flex items-center">
                <span className="mr-2">{renderCategoryIcon(category.icon)}</span>
                {category.name}
              </h3>
              <p className="text-sm text-text-secondary mb-4">{category.description}</p>
              
              <div className="space-y-4 border-l-2 border-gray-200 pl-4">
                {categoryNotifications.map(notification => {
                  const prefs = formData.notification_preferences?.[notification.id] || {};
                  const isEnabled = prefs.enabled !== undefined ? prefs.enabled : notification.default;
                  
                  return (
                    <div key={notification.id} className="bg-secondary bg-opacity-20 p-4 rounded-md">
                      <div className="flex justify-between items-center">
                        <div>
                          <h4 className="font-medium text-text-primary">{notification.name}</h4>
                          <p className="text-sm text-text-secondary mt-1">{notification.description}</p>
                        </div>
                        
                        <div className="relative">
                          <input
                            type="checkbox"
                            id={notification.id}
                            checked={isEnabled}
                            onChange={() => handleNotificationToggle(notification.id, !isEnabled)}
                            className="sr-only"
                          />
                          <div 
                            className={`w-14 h-7 flex items-center rounded-full p-1 cursor-pointer transition-colors duration-300 ease-in-out ${
                              isEnabled ? 'bg-accent' : 'bg-gray-300'
                            }`}
                            onClick={() => handleNotificationToggle(notification.id, !isEnabled)}
                          >
                            <div
                              className={`bg-white w-5 h-5 rounded-full shadow-md transform transition-transform duration-300 ease-in-out ${
                                isEnabled ? 'translate-x-7' : ''
                              }`}
                            ></div>
                          </div>
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          );
        })}
      </div>
    );
  }
};

// Helper function to render icons
const renderIcon = (iconName) => {
  switch (iconName) {
    case 'user':
      return (
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
          <circle cx="12" cy="7" r="4"></circle>
        </svg>
      );
    case 'lock':
      return (
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
          <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
        </svg>
      );
    case 'bell':
      return (
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"></path>
          <path d="M13.73 21a2 2 0 0 1-3.46 0"></path>
        </svg>
      );
    case 'palette':
      return (
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <circle cx="13.5" cy="6.5" r=".5"></circle>
          <circle cx="17.5" cy="10.5" r=".5"></circle>
          <circle cx="8.5" cy="7.5" r=".5"></circle>
          <circle cx="6.5" cy="12.5" r=".5"></circle>
          <path d="M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10c.926 0 1.648-.746 1.648-1.688 0-.437-.18-.835-.437-1.125-.29-.289-.438-.652-.438-1.125a1.64 1.64 0 0 1 1.668-1.668h1.996c3.051 0 5.555-2.503 5.555-5.554C21.965 6.012 17.461 2 12 2z"></path>
        </svg>
      );
    case 'puzzle':
      return (
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M19.439 7.85c-.049.322.059.648.289.878l1.568 1.568c.47.47.47 1.229 0 1.698l-1.42 1.42c-.47.47-1.229.47-1.698 0l-1.568-1.568a.989.989 0 0 0-.878-.289 1 1 0 0 0-.93.978v2.222c0 .661-.536 1.197-1.197 1.197h-2.01c-.661 0-1.197-.536-1.197-1.197v-2.222a1 1 0 0 0-.93-.978.989.989 0 0 0-.878.289l-1.568 1.568c-.47.47-1.229.47-1.698 0l-1.42-1.42c-.47-.47-.47-1.229 0-1.698l1.568-1.568a.989.989 0 0 0 .289-.878 1 1 0 0 0-.978-.93H2.561c-.661 0-1.197-.536-1.197-1.197v-2.01c0-.661.536-1.197 1.197-1.197h2.222a1 1 0 0 0 .978-.93.989.989 0 0 0-.289-.878l-1.568-1.568c-.47-.47-.47-1.229 0-1.698l1.42-1.42c.47-.47 1.229-.47 1.698 0l1.568 1.568c.23.23.556.338.878.29.535-.081.93-.53.93-1.071V3.197C10.398 2.536 10.934 2 11.595 2h2.01c.661 0 1.197.536 1.197 1.197v2.222c0 .541.395.99.93 1.071.322.049.648-.059.878-.29l1.568-1.568c.47-.47 1.229-.47 1.698 0l1.42 1.42c.47.47.47 1.229 0 1.698l-1.568 1.568a.989.989 0 0 0-.289.878 1 1 0 0 0 .978.93h2.222c.661 0 1.197.536 1.197 1.197v2.01c0 .661-.536 1.197-1.197 1.197h-2.222a1 1 0 0 0-.978.93z"></path>
        </svg>
      );
    default:
      return (
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <circle cx="12" cy="12" r="10"></circle>
          <line x1="12" y1="8" x2="12" y2="16"></line>
          <line x1="8" y1="12" x2="16" y2="12"></line>
        </svg>
      );
  }
};

// Helper function to render category icons
const renderCategoryIcon = (iconName) => {
  switch (iconName) {
    case 'user':
      return (
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
          <circle cx="12" cy="7" r="4"></circle>
        </svg>
      );
    case 'credit-card':
      return (
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <rect x="1" y="4" width="22" height="16" rx="2" ry="2"></rect>
          <line x1="1" y1="10" x2="23" y2="10"></line>
        </svg>
      );
    case 'puzzle':
      return (
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M19.439 7.85c-.049.322.059.648.289.878l1.568 1.568c.47.47.47 1.229 0 1.698l-1.42 1.42c-.47.47-1.229.47-1.698 0l-1.568-1.568a.989.989 0 0 0-.878-.289 1 1 0 0 0-.93.978v2.222c0 .661-.536 1.197-1.197 1.197h-2.01c-.661 0-1.197-.536-1.197-1.197v-2.222a1 1 0 0 0-.93-.978.989.989 0 0 0-.878.289l-1.568 1.568c-.47.47-1.229.47-1.698 0l-1.42-1.42c-.47-.47-.47-1.229 0-1.698l1.568-1.568a.989.989 0 0 0 .289-.878 1 1 0 0 0-.978-.93H2.561c-.661 0-1.197-.536-1.197-1.197v-2.01c0-.661.536-1.197 1.197-1.197h2.222a1 1 0 0 0 .978-.93.989.989 0 0 0-.289-.878l-1.568-1.568c-.47-.47-.47-1.229 0-1.698l1.42-1.42c.47-.47 1.229-.47 1.698 0l1.568 1.568c.23.23.556.338.878.29.535-.081.93-.53.93-1.071V3.197C10.398 2.536 10.934 2 11.595 2h2.01c.661 0 1.197.536 1.197 1.197v2.222c0 .541.395.99.93 1.071.322.049.648-.059.878-.29l1.568-1.568c.47-.47 1.229-.47 1.698 0l1.42 1.42c.47.47.47 1.229 0 1.698l-1.568 1.568a.989.

989 0 0 0-.289.878 1 1 0 0 0 .978.93h2.222c.661 0 1.197.536 1.197 1.197v2.01c0 .661-.536 1.197-1.197 1.197h-2.222a1 1 0 0 0-.978.93z"></path>
        </svg>
      );
    case 'bell':
      return (
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"></path>
          <path d="M13.73 21a2 2 0 0 1-3.46 0"></path>
        </svg>
      );
    case 'file':
      return (
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
          <polyline points="14 2 14 8 20 8"></polyline>
          <line x1="16" y1="13" x2="8" y2="13"></line>
          <line x1="16" y1="17" x2="8" y2="17"></line>
          <polyline points="10 9 9 9 8 9"></polyline>
        </svg>
      );
    default:
      return (
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <circle cx="12" cy="12" r="10"></circle>
          <line x1="12" y1="8" x2="12" y2="16"></line>
          <line x1="8" y1="12" x2="16" y2="12"></line>
        </svg>
      );
  }
};

// Helper function to render form fields based on type
const renderFormField = (field, value, error, handleChange, currentTheme, handleThemeToggle, pluginSettings, handlePluginSettingsChange) => {
  const isDisabled = !handleChange;
  
  const commonProps = {
    id: field.id,
    name: field.id,
    disabled: isDisabled,
    className: `w-full px-3 py-2 border ${
      error ? 'border-red-500' : 'border-gray-300'
    } rounded bg-secondary text-text-primary ${
      isDisabled ? 'opacity-70 cursor-not-allowed' : ''
    } focus:ring-2 focus:ring-accent focus:border-accent`,
  };
  
  // Special handler for plugin-settings type
  if (field.type === 'plugin-settings') {
    return (
      <div key={field.id} className="mb-6">
        <h3 className="text-lg font-medium text-text-primary mb-3">{field.label}</h3>
        <div className="bg-secondary bg-opacity-30 rounded-md overflow-hidden border border-gray-200">
          <DynamicUIComponent 
            pluginName={field.plugin}
            componentName="settings/SettingsPanel"
            pluginProps={{
              currentSettings: pluginSettings[field.plugin] || {},
              onSettingsChange: (settings) => handlePluginSettingsChange(field.plugin, settings)
            }}
            fallback={
              <div className="p-4 text-text-secondary text-center">
                <p>No settings available for this plugin</p>
              </div>
            }
          />
        </div>
      </div>
    );
  }
  
  switch (field.type) {
    case 'text':
      return (
        <div key={field.id} className="mb-4">
          <label htmlFor={field.id} className="block text-sm font-medium text-text-secondary mb-1">
            {field.label} {field.required && <span className="text-red-500">*</span>}
          </label>
          <input
            type="text"
            value={value}
            onChange={e => handleChange && handleChange(field.id, e.target.value)}
            {...commonProps}
          />
          {error && <p className="mt-1 text-sm text-red-600">{error}</p>}
        </div>
      );
      
    case 'email':
      return (
        <div key={field.id} className="mb-4">
          <label htmlFor={field.id} className="block text-sm font-medium text-text-secondary mb-1">
            {field.label} {field.required && <span className="text-red-500">*</span>}
          </label>
          <input
            type="email"
            value={value}
            onChange={e => handleChange && handleChange(field.id, e.target.value)}
            {...commonProps}
          />
          {error && <p className="mt-1 text-sm text-red-600">{error}</p>}
        </div>
      );
      
    case 'textarea':
      return (
        <div key={field.id} className="mb-4">
          <label htmlFor={field.id} className="block text-sm font-medium text-text-secondary mb-1">
            {field.label} {field.required && <span className="text-red-500">*</span>}
          </label>
          <textarea
            value={value}
            onChange={e => handleChange && handleChange(field.id, e.target.value)}
            rows="4"
            {...commonProps}
          ></textarea>
          {error && <p className="mt-1 text-sm text-red-600">{error}</p>}
        </div>
      );
      
    case 'password-change':
      return (
        <div key={field.id} className="mb-4">
          <label htmlFor={field.id} className="block text-sm font-medium text-text-secondary mb-1">
            {field.label}
          </label>
          <button
            type="button"
            onClick={() => alert('Password change functionality would be implemented here')}
            className={`px-4 py-2 rounded-md text-sm ${
              isDisabled
                ? 'bg-gray-300 text-gray-500 cursor-not-allowed'
                : 'bg-accent text-white hover:bg-opacity-90 transition-all'
            }`}
            disabled={isDisabled}
          >
            <span className="flex items-center">
              <svg xmlns="http://www.w3.org/2000/svg" className="h-4 w-4 mr-2" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <rect x="3" y="11" width="18" height="11" rx="2" ry="2"></rect>
                <path d="M7 11V7a5 5 0 0 1 10 0v4"></path>
              </svg>
              Change Password
            </span>
          </button>
        </div>
      );
      
    case 'toggle':
      return (
        <div key={field.id} className="mb-4 flex items-center justify-between py-2">
          <label htmlFor={field.id} className="text-sm font-medium text-text-secondary">
            {field.label}
          </label>
          <div className="relative">
            <input
              type="checkbox"
              id={field.id}
              name={field.id}
              checked={value === true}
              onChange={e => handleChange && handleChange(field.id, e.target.checked)}
              disabled={isDisabled}
              className="sr-only"
            />
            <div 
              className={`w-14 h-7 flex items-center rounded-full p-1 cursor-pointer transition-colors duration-300 ease-in-out ${
                value === true ? 'bg-accent' : 'bg-gray-300'
              } ${isDisabled ? 'opacity-50 cursor-not-allowed' : ''}`}
              onClick={() => !isDisabled && handleChange && handleChange(field.id, !value)}
            >
              <div
                className={`bg-white w-5 h-5 rounded-full shadow-md transform transition-transform duration-300 ease-in-out ${
                  value === true ? 'translate-x-7' : ''
                }`}
              ></div>
            </div>
          </div>
        </div>
      );

    case 'theme-selector':
      return (
        <div key={field.id} className="mb-4 flex flex-col sm:flex-row sm:items-center sm:justify-between py-2">
          <label htmlFor={field.id} className="text-sm font-medium text-text-secondary mb-2 sm:mb-0">
            {field.label}
          </label>
          <div className="flex space-x-2">
            <button
              type="button"
              onClick={handleThemeToggle}
              className="px-4 py-2 rounded-md text-sm bg-accent text-white hover:bg-opacity-90 transition-all flex items-center"
              aria-label="Toggle theme"
              disabled={isDisabled}
            >
              {currentTheme === 'dark' ? (
                <><span className="mr-2">☀️</span> Light Mode</>
              ) : currentTheme === 'light' ? (
                <><span className="mr-2">🌙</span> Dark Mode</>
              ) : (
                <><span className="mr-2">🎨</span> Next Theme</>
              )}
            </button>
          </div>
        </div>
      );
      
    case 'select':
      return (
        <div key={field.id} className="mb-4">
          <label htmlFor={field.id} className="block text-sm font-medium text-text-secondary mb-1">
            {field.label} {field.required && <span className="text-red-500">*</span>}
          </label>
          <select
            value={value}
            onChange={e => handleChange && handleChange(field.id, e.target.value)}
            {...commonProps}
          >
            {field.options?.map(option => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            )) || <option value="">No options available</option>}
          </select>
          {error && <p className="mt-1 text-sm text-red-600">{error}</p>}
        </div>
      );
      
    case 'image':
      return (
        <div key={field.id} className="mb-4">
          <label htmlFor={field.id} className="block text-sm font-medium text-text-secondary mb-1">
            {field.label}
          </label>
          <div className="flex items-center space-x-4">
            <div className="w-16 h-16 rounded-full overflow-hidden bg-gray-200 flex items-center justify-center">
              {value ? (
                <img src={value} alt="Profile" className="w-full h-full object-cover" />
              ) : (
                <svg xmlns="http://www.w3.org/2000/svg" className="h-8 w-8 text-gray-400" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
                  <circle cx="12" cy="7" r="4"></circle>
                </svg>
              )}
            </div>
            <button
              type="button"
              disabled={isDisabled}
              className={`px-4 py-2 rounded-md text-sm ${
                isDisabled
                  ? 'bg-gray-300 text-gray-500 cursor-not-allowed'
                  : 'bg-secondary hover:bg-opacity-90 text-text-primary border border-gray-300'
              }`}
              onClick={() => alert('Image upload functionality would be implemented here')}
            >
              Upload Image
            </button>
          </div>
        </div>
      );
      
    default:
      return null;
  }
};

export default ProfilePage;


