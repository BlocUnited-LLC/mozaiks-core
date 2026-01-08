// src/auth/LoginPage.jsx
import React, { useEffect, useMemo, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from './AuthContext';
import { useTheme } from '../core/theme/useTheme';

const LoginPage = () => {
  const { authMode, beginLogin, login, isLoading, error, isAuthenticated } = useAuth();
  const { currentTheme, branding } = useTheme();
  const navigate = useNavigate();
  const location = useLocation();

  const redirectTo = useMemo(() => {
    const from = location.state?.from?.pathname;
    return typeof from === 'string' && from ? from : '/profile';
  }, [location.state]);

  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(false);

  useEffect(() => {
    if (isAuthenticated) {
      navigate(redirectTo, { replace: true });
    }
  }, [isAuthenticated, navigate, redirectTo]);

  const handleOidcLogin = async () => {
    await beginLogin(redirectTo);
  };

  const handleLocalSubmit = async (e) => {
    e.preventDefault();
    const ok = await login(username, password, { remember: rememberMe });
    if (ok) {
      navigate(redirectTo, { replace: true });
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-background">
      <div className="max-w-md w-full space-y-8 p-10 bg-primary rounded-lg shadow-lg">
        <div className="text-center">
          {branding.logo_url && (
            <img
              className="mx-auto h-16 w-auto mb-4"
              src={branding.logo_url}
              alt={branding.app_name || "Mozaiks"}
            />
          )}
          <h2 className="text-3xl font-bold text-text-primary">
            {branding.app_name || "Mozaiks"}
          </h2>
          <p className="mt-2 text-text-secondary">
            Authentication
          </p>
        </div>
        
        {error && (
          <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded relative" role="alert">
            <span className="block sm:inline">{error}</span>
          </div>
        )}
        
        {authMode === 'local' ? (
          <form className="mt-6 space-y-6" onSubmit={handleLocalSubmit}>
            <div className="bg-yellow-50 border border-yellow-200 text-yellow-800 px-4 py-3 rounded text-sm" role="alert">
              <strong>Local Development Mode</strong>
              <p className="mt-1">This mode is for offline development only. Not recommended for production.</p>
            </div>
            <div className="rounded-md -space-y-px">
              <div className="mb-4">
                <label htmlFor="username" className="block text-sm font-medium text-text-secondary mb-1">
                  Username
                </label>
                <input
                  id="username"
                  name="username"
                  type="text"
                  required
                  className="appearance-none rounded relative block w-full px-3 py-2 border border-gray-300 bg-secondary placeholder-text-secondary text-text-primary focus:outline-none focus:ring-accent focus:border-accent focus:z-10 sm:text-sm"
                  placeholder="Username"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                />
              </div>
              <div>
                <label htmlFor="password" className="block text-sm font-medium text-text-secondary mb-1">
                  Password
                </label>
                <input
                  id="password"
                  name="password"
                  type="password"
                  required
                  className="appearance-none rounded relative block w-full px-3 py-2 border border-gray-300 bg-secondary placeholder-text-secondary text-text-primary focus:outline-none focus:ring-accent focus:border-accent focus:z-10 sm:text-sm"
                  placeholder="Password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                />
              </div>
            </div>

            <div className="flex items-center justify-between">
              <label className="flex items-center gap-2 text-sm text-text-secondary">
                <input
                  type="checkbox"
                  className="h-4 w-4 text-accent focus:ring-accent border-gray-300 rounded"
                  checked={rememberMe}
                  onChange={(e) => setRememberMe(e.target.checked)}
                />
                Remember me
              </label>
              <div className="text-sm">
                <Link to="/register" className="font-medium text-accent hover:opacity-80">
                  Create account
                </Link>
              </div>
            </div>

            <button
              type="submit"
              disabled={isLoading}
              className="group relative w-full flex justify-center py-2 px-4 border border-transparent text-sm font-medium rounded-md text-white bg-accent hover:opacity-90 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-accent"
            >
              {isLoading ? 'Signing in…' : 'Sign in'}
            </button>
          </form>
        ) : (
          <div className="mt-6 space-y-4">
            <div className="text-sm text-text-secondary text-center">
              <p>Sign in via your identity provider</p>
              <p className="mt-1 text-xs opacity-70">You will be redirected to complete authentication</p>
            </div>

            <button
              type="button"
              onClick={handleOidcLogin}
              disabled={isLoading}
              className="group relative w-full flex justify-center py-2 px-4 border border-transparent text-sm font-medium rounded-md text-white bg-accent hover:opacity-90 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-accent"
            >
              {isLoading ? 'Redirecting…' : 'Continue to Sign In'}
            </button>
          </div>
        )}
      </div>
    </div>
  );
};

export default LoginPage;
