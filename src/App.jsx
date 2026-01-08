// /src/App.jsx
import React, { useState, useEffect } from "react";
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
  const { branding } = useTheme();
  const { plugins } = usePlugins();
  const { user, isAuthenticated, logout, authFetch } = useAuth();
  const { monetizationEnabled } = useAppConfig();
  const [navigation, setNavigation] = useState([]);

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
    <Router>
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
          <div className="container mx-auto p-4">
            <Routes>
              <Route path="/auth/login" element={<AuthLoginRoute />} />
              <Route path="/auth/callback" element={<AuthCallbackRoute />} />
              <Route path="/auth/logout" element={<AuthLogoutRoute />} />

              <Route 
                path="/login" 
                element={isAuthenticated ? <Navigate to="/profile" replace /> : <LoginPage />} 
              />
              <Route
                path="/register"
                element={isAuthenticated ? <Navigate to="/profile" replace /> : <RegisterPage />}
              />

              <Route 
                path="/profile" 
                element={
                  <ProtectedRoute>
                    <ProfilePage />
                  </ProtectedRoute>
                } 
              />

              <Route 
                path="/settings" 
                element={
                  <ProtectedRoute>
                    <SettingsPage />
                  </ProtectedRoute>
                } 
              />

              <Route
                path="/ai"
                element={
                  <ProtectedRoute>
                    <AICapabilitiesPage />
                  </ProtectedRoute>
                }
              />

              {monetizationEnabled && (
                <Route 
                  path="/subscriptions" 
                  element={
                    <ProtectedRoute>
                      <SubscriptionPage />
                    </ProtectedRoute>
                  } 
                />
              )}

              <Route 
                path="/plugins/:pluginName" 
                element={
                  <ProtectedRoute>
                    <PluginPage />
                  </ProtectedRoute>
                } 
              />

              {/* Home - redirect to profile if signed in, login otherwise */}
              <Route 
                path="/" 
                element={
                  isAuthenticated ? 
                    <Navigate to="/profile" replace /> : 
                    <Navigate to="/login" replace />
                } 
              />
            </Routes>
          </div>
        </main>
      </div>
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
