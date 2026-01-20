// src/main.jsx
import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';
import './index.css';
// Import ChatUI styles (widget safe-area, transport styles, etc.)
import './chat/index.css';
import './chat/styles/TransportAwareChat.css';
import { ThemeProvider } from './core/theme/ThemeProvider';
import { PluginProvider } from './core/plugins/PluginProvider';
import { AuthProvider } from './auth/AuthContext';
import { ProfileProvider } from './profile/ProfileContext';
import { NotificationsProvider } from './notifications/NotificationsContext';
import { AppConfigProvider, useAppConfig } from './AppConfig';

// Import SubscriptionProvider from core location
import { SubscriptionProvider } from './subscription/SubscriptionContext';

// Wrap the app with AppConfigProvider to determine if monetization is enabled
const AppWithConfig = () => {
  const { monetizationEnabled, isLoading } = useAppConfig();

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="text-center">
          <div className="animate-spin h-10 w-10 border-4 border-accent border-t-transparent rounded-full mx-auto"></div>
          <p className="mt-2">Loading application configuration...</p>
        </div>
      </div>
    );
  }

  return (
    <ThemeProvider>
      <AuthProvider>
        {monetizationEnabled ? (
          // With monetization enabled, include SubscriptionProvider
          <SubscriptionProvider>
            <ProfileProvider>
              <NotificationsProvider>
                <PluginProvider>
                  <App />
                </PluginProvider>
              </NotificationsProvider>
            </ProfileProvider>
          </SubscriptionProvider>
        ) : (
          // Without monetization, skip SubscriptionProvider
          <ProfileProvider>
            <NotificationsProvider>
              <PluginProvider>
                <App />
              </PluginProvider>
            </NotificationsProvider>
          </ProfileProvider>
        )}
      </AuthProvider>
    </ThemeProvider>
  );
};

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <AppConfigProvider>
      <AppWithConfig />
    </AppConfigProvider>
  </React.StrictMode>
);