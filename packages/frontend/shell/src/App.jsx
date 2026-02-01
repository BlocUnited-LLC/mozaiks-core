import React, { useEffect } from 'react';
import { BrowserRouter as Router } from 'react-router-dom';
import { ChatUIProvider, useChatUI } from './context/ChatUIContext';
import { NavigationProvider } from './providers/NavigationProvider';
import { BrandingProvider } from './providers/BrandingProvider';
import RouteRenderer from './components/RouteRenderer';
import { GlobalChatWidgetWrapper } from '@mozaiks/chat-ui';
import './styles/TransportAwareChat.css';

// Import core component registration
import './registry/coreComponents';

/**
 * AppContent - Inner component that has access to context and renders routes
 */
const AppContent = () => {
  const { user } = useChatUI();

  // Handle auth required events
  const handleAuthRequired = (path) => {
    console.log(`[App] Auth required for path: ${path}`);
    // Could trigger login modal, redirect, etc.
  };

  return (
    <RouteRenderer
      isAuthenticated={!!user}
      onAuthRequired={handleAuthRequired}
    />
  );
};

/**
 * Unified ChatUI App - Declarative, data-driven shell
 *
 * The Shell is now configured via:
 * - navigation.json: Routes and navigation structure
 * - branding.json: App branding, theme, and layout
 *
 * Components are registered via the component registry.
 * Platform generators produce the JSON configs, Shell renders them.
 */
function App() {
  const handleChatUIReady = () => {
    console.log('ChatUI is ready!');
  };
  const navigationPath = process.env.REACT_APP_NAVIGATION_PATH || '/navigation.json';

  const handleNavigationLoad = (config) => {
    if (process.env.NODE_ENV === 'development') {
      console.log('[App] Navigation config loaded:', config.version);
    }
  };

  const handleBrandingLoad = (config) => {
    if (process.env.NODE_ENV === 'development') {
      console.log('[App] Branding config loaded:', config.version);
    }
  };

  return (
    <BrandingProvider onLoad={handleBrandingLoad}>
      <NavigationProvider onLoad={handleNavigationLoad} configPath={navigationPath}>
        <ChatUIProvider onReady={handleChatUIReady}>
          <Router future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
            <GlobalChatWidgetWrapper />
            <AppContent />
          </Router>
        </ChatUIProvider>
      </NavigationProvider>
    </BrandingProvider>
  );
}

export default App;
