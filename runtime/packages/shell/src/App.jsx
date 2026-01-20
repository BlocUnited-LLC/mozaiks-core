// /src/App.jsx
import React, { useState, useEffect, useMemo } from "react";
import { BrowserRouter as Router, Routes, Route, Navigate, Link, useParams, useNavigate } from "react-router-dom";
import { useTheme } from './core/theme/useTheme';
import { usePlugins } from './core/plugins/usePlugins';
import { useAuth } from './auth/AuthContext';
import { useAppConfig } from './AppConfig';
import DynamicUIComponent from './core/plugins/DynamicUIComponent';
import LoginPage from './auth/LoginPage';
import RegisterPage from './auth/RegisterPage';
import ProtectedRoute from './auth/ProtectedRoute';
import ProfilePage from './profile/ProfilePage';
import NotificationsMenu from './notifications/NotificationsMenu';
import SubscriptionPage from './subscription/SubscriptionPage';
import AICapabilitiesPage from './ai/AICapabilitiesPage';
import AuthLoginRoute from './auth/AuthLoginRoute';
import AuthCallbackRoute from './auth/AuthCallbackRoute';
import AuthLogoutRoute from './auth/AuthLogoutRoute';

// ChatUI Integration
import { ChatUIProvider } from './chat/context/ChatUIContext';
import { createMozaiksCoreAuthAdapter } from './chat/adapters/MozaiksCoreAuthAdapter';
import GlobalChatWidgetWrapper from './chat/components/layout/GlobalChatWidgetWrapper';
import ChatPage from './chat/pages/ChatPage';
import MyWorkflowsPage from './chat/pages/MyWorkflowsPage';
import ArtifactPage from './chat/pages/ArtifactPage';

const SettingsPage = () => <div className="p-4">Settings Content</div>;

const UserMenu = ({ user, logout }) => {
  const [isOpen, setIsOpen] = useState(false);
  const navigate = useNavigate();

  const toggleMenu = () => setIsOpen(!isOpen);

  const handleProfileClick = () => {
    setIsOpen(false);
    navigate('/profile');
  };

  const handleLogoutClick = () => {
    setIsOpen(false);
    logout();
  };

  useEffect(() => {
    const handleClickOutside = (event) => {
      if (isOpen && !event.target.closest('.user-menu')) {
        setIsOpen(false);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isOpen]);

  return (
    <div className="relative user-menu">
      <button 
        onClick={toggleMenu}
        className="flex items-center space-x-2 bg-secondary hover:bg-opacity-90 p-2 rounded-full focus:outline-none"
      >
        <span className="text-xl" aria-label="User profile">👤</span>
      </button>
      
      {isOpen && (
        <div className="absolute right-0 mt-2 w-48 bg-primary border border-gray-200 rounded-md shadow-lg z-10">
          <div className="p-3 border-b border-gray-200">
            <p className="font-medium text-text-primary">{user.username}</p>
          </div>
          <ul>
            <li>
              <button 
                onClick={handleProfileClick}
                className="w-full text-left px-4 py-2 text-text-primary hover:bg-secondary flex items-center"
              >
                <span className="mr-2">👤</span> Profile
              </button>
            </li>
            <li>
              <button 
                onClick={handleLogoutClick}
                className="w-full text-left px-4 py-2 text-text-primary hover:bg-secondary flex items-center"
              >
                <span className="mr-2">🚪</span> Logout
              </button>
            </li>
          </ul>
        </div>
      )}
    </div>
  );
};

const App = () => {
  const auth = useAuth();
  const { branding } = useTheme();
  const { plugins } = usePlugins();
  const { user, isAuthenticated, logout, authFetch } = auth;
  const { monetizationEnabled } = useAppConfig();
  const [navigation, setNavigation] = useState([]);

  // Create ChatUI auth adapter that bridges to our AuthContext
  const chatAuthAdapter = useMemo(() => createMozaiksCoreAuthAdapter(auth), [auth]);

  useEffect(() => {
    async function fetchNavigation() {
      if (!isAuthenticated) return;

      try {
        const response = await authFetch("/api/navigation");

        if (!response.ok) throw new Error("Failed to load navigation");

        const data = await response.json();
        const filteredNavigation = data.navigation?.filter(item => item.path !== '/profile') || [];
        setNavigation(filteredNavigation);
      } catch (error) {
        console.error("⚠️ Navigation fetch error:", error);
      }
    }

    fetchNavigation();
  }, [isAuthenticated, authFetch]);

  return (
    <Router future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
      <ChatUIProvider 
        authAdapter={chatAuthAdapter}
        onReady={() => console.log('🚀 ChatUI ready and integrated with MozaiksCore')}
      >
        {/* Global Chat Widget - renders on non-chat routes when in widget mode */}
        <GlobalChatWidgetWrapper />
        
        <div className="min-h-screen bg-background flex flex-col">
          <header className="bg-primary p-4 shadow-md">
            <div className="container mx-auto flex justify-between items-center">
              <div className="flex items-center">
                {branding.logo_url && (
                  <img 
                    src={branding.logo_url} 
                    alt={branding.app_name || "Mozaiks"} 
                    className="h-8 w-auto mr-2"
                  />
                )}
                <h1 className="text-2xl font-bold text-text-primary">
                  {branding.app_name || "Mozaiks"}
                </h1>
              </div>

              {isAuthenticated && (
                <nav className="hidden md:flex space-x-4">
                  {navigation.map((navItem) => (
                    <Link 
                      key={navItem.path} 
                      to={navItem.path}
                      className="text-text-primary hover:text-accent"
                    >
                      {navItem.label}
                    </Link>
                  ))}
                </nav>
              )}

              <div className="flex items-center space-x-4">
                {isAuthenticated ? (
                  <>
                    <NotificationsMenu />
                    <UserMenu user={user} logout={logout} />
                  </>
                ) : (
                  <Link to="/login" className="btn btn-primary">
                    Login
                  </Link>
                )}
              </div>
            </div>
          </header>

          <main className="flex-grow">
            <Routes>
              {/* Auth Routes (no layout container) */}
              <Route path="/auth/login" element={<AuthLoginRoute />} />
              <Route path="/auth/callback" element={<AuthCallbackRoute />} />
              <Route path="/auth/logout" element={<AuthLogoutRoute />} />

              {/* Chat Routes (full-screen, no container padding) */}
              <Route 
                path="/chat/*" 
                element={
                  <ProtectedRoute>
                    <ChatPage />
                  </ProtectedRoute>
                } 
              />
              <Route 
                path="/app/:appId/:workflowName/*" 
                element={
                  <ProtectedRoute>
                    <ChatPage />
                  </ProtectedRoute>
                } 
              />
              <Route 
                path="/workflows" 
                element={
                  <ProtectedRoute>
                    <MyWorkflowsPage />
                  </ProtectedRoute>
                } 
              />
              <Route 
                path="/my-workflows" 
                element={
                  <ProtectedRoute>
                    <MyWorkflowsPage />
                  </ProtectedRoute>
                } 
              />
              <Route 
                path="/artifacts/:artifactId" 
                element={
                  <ProtectedRoute>
                    <ArtifactPage />
                  </ProtectedRoute>
                } 
              />

              {/* Standard Routes (with container padding) */}
              <Route 
                path="/login" 
                element={
                  <div className="container mx-auto p-4">
                    {isAuthenticated ? <Navigate to="/chat" replace /> : <LoginPage />}
                  </div>
                } 
              />
              <Route
                path="/register"
                element={
                  <div className="container mx-auto p-4">
                    {isAuthenticated ? <Navigate to="/chat" replace /> : <RegisterPage />}
                  </div>
                }
              />

              <Route 
                path="/profile" 
                element={
                  <ProtectedRoute>
                    <div className="container mx-auto p-4">
                      <ProfilePage />
                    </div>
                  </ProtectedRoute>
                } 
              />

              <Route 
                path="/settings" 
                element={
                  <ProtectedRoute>
                    <div className="container mx-auto p-4">
                      <SettingsPage />
                    </div>
                  </ProtectedRoute>
                } 
              />

              <Route
                path="/ai"
                element={
                  <ProtectedRoute>
                    <div className="container mx-auto p-4">
                      <AICapabilitiesPage />
                    </div>
                  </ProtectedRoute>
                }
              />

              {monetizationEnabled && (
                <Route 
                  path="/subscriptions" 
                  element={
                    <ProtectedRoute>
                      <div className="container mx-auto p-4">
                        <SubscriptionPage />
                      </div>
                    </ProtectedRoute>
                  } 
                />
              )}

              <Route 
                path="/plugins/:pluginName" 
                element={
                  <ProtectedRoute>
                    <div className="container mx-auto p-4">
                      <PluginPage />
                    </div>
                  </ProtectedRoute>
                } 
              />

              {/* Home - redirect to chat if signed in, login otherwise */}
              <Route 
                path="/" 
                element={
                  isAuthenticated ? 
                    <Navigate to="/chat" replace /> : 
                    <Navigate to="/login" replace />
                } 
              />
            </Routes>
          </main>
        </div>
      </ChatUIProvider>
    </Router>
  );
};

const PluginPage = () => {
  const { pluginName } = useParams();

  return (
    <div className="plugin-container">
      <h2 className="plugin-header">Plugin: {pluginName}</h2>
      <DynamicUIComponent pluginName={pluginName} pluginProps={{}} />
    </div>
  );
};

export default App;
