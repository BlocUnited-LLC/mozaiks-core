// src/auth/AuthContext.jsx
import React, { createContext, useCallback, useEffect, useMemo, useState, useContext } from 'react';
import { getAuthMode, isTokenExchangeEnabled } from './runtime/authConfig';
import { appTokenStore } from './runtime/appTokenStore';
import { localTokenStore } from './runtime/localTokenStore';
import { oidc } from './runtime/oidcClient';

const AuthContext = createContext(null);

export const AuthProvider = ({ children }) => {
  const authMode = useMemo(() => getAuthMode(), []);
  const tokenExchangeEnabled = useMemo(() => isTokenExchangeEnabled(), []);

  const [user, setUser] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);

  const validateToken = useCallback(async (token) => {
    if (!token) return null;
    try {
      const response = await fetch('/api/auth/validate-token', {
        headers: { Authorization: `Bearer ${token}` },
      });
      if (!response.ok) return null;
      const data = await response.json();
      return { username: data.username, user_id: data.user_id };
    } catch {
      return null;
    }
  }, []);

  const exchangeToken = useCallback(async () => {
    if (!tokenExchangeEnabled) return null;
    if (authMode !== 'external' && authMode !== 'platform') return null;

    const platformToken = await oidc.getAccessToken();
    if (!platformToken) return null;

    const response = await fetch('/api/auth/token-exchange', {
      method: 'POST',
      headers: { Authorization: `Bearer ${platformToken}` },
    });

    if (!response.ok) return null;

    const data = await response.json();
    const appToken = (data?.access_token || '').trim();
    if (!appToken) return null;

    appTokenStore.set(appToken);
    return appToken;
  }, [authMode, tokenExchangeEnabled]);

  const getAccessToken = useCallback(async () => {
    if (authMode === 'local') return localTokenStore.get();
    if (authMode === 'external' || authMode === 'platform') {
      if (!tokenExchangeEnabled) return await oidc.getAccessToken();

      const existing = appTokenStore.get();
      if (existing) return existing;

      return await exchangeToken();
    }
    return null;
  }, [authMode, tokenExchangeEnabled, exchangeToken]);

  const getRuntimeAccessToken = useCallback(async () => {
    if (authMode === 'local') return localTokenStore.get();
    if (authMode === 'external' || authMode === 'platform') {
      // Prefer the externally-issued token for connecting to MozaiksAI runtime.
      const externalToken = await oidc.getAccessToken();
      if (externalToken) return externalToken;
      // Fallback: app-scoped token (only works if the runtime is configured to trust it).
      return appTokenStore.get();
    }
    return null;
  }, [authMode]);

  /**
   * Get the subject (sub) claim from the current token.
   * 
   * SECURITY NOTES:
   * - This is for LEGACY ROUTING HINTS only — NOT for identity.
   * - The server MUST derive user identity from the JWT sub claim.
   * - MozaiksAI validates that any path-based user_id matches JWT sub.
   * - In local mode, returns the server-validated user_id (NOT client state).
   * 
   * @returns {Promise<string|null>} The sub claim (platform/external) or server-validated user_id (local)
   */
  const getTokenSubject = useCallback(async () => {
    // SECURITY: Platform/external modes MUST use OIDC-derived sub only
    if (authMode === 'platform' || authMode === 'external') {
      return await oidc.getTokenSubject();
    }
    // ISOLATED: Local mode path — only reachable when MOZAIKS_AUTH_MODE=local
    if (authMode === 'local') {
      // user.user_id is set ONLY from /api/auth/validate-token server response
      // This is server-validated, not client-generated
      return user?.user_id || null;
    }
    return null;
  }, [authMode, user?.user_id]);

  const refreshUser = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const token = await getAccessToken();
      if (!token) {
        setUser(null);
        return null;
      }

      const resolvedUser = await validateToken(token);
      if (!resolvedUser) {
        // SECURITY: Mode-isolated cleanup — each branch is mutually exclusive
        if (authMode === 'local') {
          // ISOLATED: Only reachable when MOZAIKS_AUTH_MODE=local
          localTokenStore.clear();
        } else if (authMode === 'platform' || authMode === 'external') {
          // Platform/external: use OIDC or token-exchange stores only
          if (tokenExchangeEnabled) {
            appTokenStore.clear();
          } else {
            await oidc.removeUser();
          }
        }
        setUser(null);
        return null;
      }

      setUser(resolvedUser);
      return resolvedUser;
    } catch (err) {
      console.error('Authentication check failed:', err);
      setUser(null);
      setError('Authentication check failed');
      return null;
    } finally {
      setIsLoading(false);
    }
  }, [getAccessToken, validateToken, authMode]);

  useEffect(() => {
    refreshUser();
  }, [refreshUser]);

  const beginLogin = useCallback(
    async (returnTo) => {
      setError(null);
      if (authMode === 'local') return;
      try {
        await oidc.signinRedirect(returnTo);
      } catch (err) {
        console.error('OIDC login failed:', err);
        setError(err?.message || 'Login is not configured');
      }
    },
    [authMode]
  );

  const completeLogin = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      if (authMode !== 'external' && authMode !== 'platform') {
        const resolvedUser = await refreshUser();
        return { user: resolvedUser, returnTo: undefined };
      }

      const { returnTo } = await oidc.handleSigninCallback();
      const resolvedUser = await refreshUser();
      return { user: resolvedUser, returnTo };
    } catch (err) {
      console.error('OIDC callback failed:', err);
      setError('Login failed');
      return { user: null, returnTo: undefined };
    } finally {
      setIsLoading(false);
    }
  }, [authMode, refreshUser]);

  const login = useCallback(
    async (username, password, { remember = false } = {}) => {
      if (authMode !== 'local') return false;
      setIsLoading(true);
      setError(null);
      try {
        const formData = new URLSearchParams();
        formData.append('username', username);
        formData.append('password', password);

        const response = await fetch('/api/auth/token', {
          method: 'POST',
          headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
          body: formData,
        });

        const data = await response.json();
        if (!response.ok) throw new Error(data.detail || 'Login failed');

        localTokenStore.set(data.access_token, remember);
        const resolvedUser = await validateToken(data.access_token);
        setUser(resolvedUser);
        return true;
      } catch (err) {
        setError(err.message || 'Login failed');
        return false;
      } finally {
        setIsLoading(false);
      }
    },
    [authMode, validateToken]
  );

  const register = useCallback(
    async (username, password, email, fullName, { remember = false } = {}) => {
      if (authMode !== 'local') return false;
      setIsLoading(true);
      setError(null);
      try {
        const response = await fetch('/api/auth/register', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ username, password, email, full_name: fullName }),
        });

        const data = await response.json();
        if (!response.ok) throw new Error(data.detail || 'Registration failed');

        localTokenStore.set(data.access_token, remember);
        const resolvedUser = await validateToken(data.access_token);
        setUser(resolvedUser);
        return true;
      } catch (err) {
        setError(err.message || 'Registration failed');
        return false;
      } finally {
        setIsLoading(false);
      }
    },
    [authMode, validateToken]
  );

  const logout = useCallback(async () => {
    setError(null);
    try {
      if (authMode === 'local') {
        localTokenStore.clear();
      } else {
        appTokenStore.clear();
        await oidc.signoutRedirect();
      }
    } finally {
      setUser(null);
    }
  }, [authMode]);

  const authFetch = useCallback(
    async (input, init = {}) => {
      const token = await getAccessToken();
      const headers = new Headers(init.headers || {});
      if (token) headers.set('Authorization', `Bearer ${token}`);

      const response = await fetch(input, { ...init, headers });

      if (response.status === 401 && tokenExchangeEnabled && (authMode === 'external' || authMode === 'platform')) {
        appTokenStore.clear();
        const refreshed = await exchangeToken();
        if (refreshed) {
          const retryHeaders = new Headers(init.headers || {});
          retryHeaders.set('Authorization', `Bearer ${refreshed}`);
          return await fetch(input, { ...init, headers: retryHeaders });
        }
      }

      if (response.status === 401 || response.status === 403) {
        try {
          if (authMode === 'local') localTokenStore.clear();
          else if (tokenExchangeEnabled) appTokenStore.clear();
          else await oidc.removeUser();
        } catch {
          // ignore
        }
        setUser(null);
      }

      return response;
    },
    [getAccessToken, authMode, tokenExchangeEnabled, exchangeToken]
  );

  const value = useMemo(
    () => ({
      user,
      isAuthenticated: !!user,
      isLoading,
      error,
      authMode,
      tokenExchangeEnabled,
      refreshUser,
      getAccessToken,
      getRuntimeAccessToken,
      getTokenSubject,
      beginLogin,
      completeLogin,
      login,
      register,
      authFetch,
      logout,
    }),
    [
      user,
      isLoading,
      error,
      authMode,
      tokenExchangeEnabled,
      refreshUser,
      getAccessToken,
      getRuntimeAccessToken,
      getTokenSubject,
      beginLogin,
      completeLogin,
      login,
      register,
      authFetch,
      logout,
    ]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
