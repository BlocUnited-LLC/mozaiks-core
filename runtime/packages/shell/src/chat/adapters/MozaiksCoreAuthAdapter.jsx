/**
 * MozaiksCoreAuthAdapter
 * 
 * Bridges the main MozaiksCore AuthContext with the ChatUI's auth adapter system.
 * This adapter allows ChatUI to use the same authentication state as the host app.
 */
import { AuthAdapter } from './auth';

/**
 * Create an auth adapter that wraps the main app's auth context.
 * 
 * Usage:
 * ```javascript
 * import { useAuth } from '../auth/AuthContext';
 * import { createMozaiksCoreAuthAdapter } from '../chat/adapters/MozaiksCoreAuthAdapter';
 * 
 * function App() {
 *   const auth = useAuth();
 *   const chatAuthAdapter = useMemo(() => createMozaiksCoreAuthAdapter(auth), [auth]);
 *   
 *   return (
 *     <ChatUIProvider authAdapter={chatAuthAdapter}>
 *       ...
 *     </ChatUIProvider>
 *   );
 * }
 * ```
 */
export function createMozaiksCoreAuthAdapter(auth) {
  if (!auth) {
    console.warn('[MozaiksCoreAuthAdapter] No auth context provided, using noop adapter');
    return new NoopAuthAdapter();
  }

  return new MozaiksCoreAuthAdapter(auth);
}

class MozaiksCoreAuthAdapter extends AuthAdapter {
  constructor(auth) {
    super();
    this.auth = auth;
    this.authStateCallbacks = [];
    this._lastUser = null;
  }

  async getCurrentUser() {
    const { user, isAuthenticated } = this.auth;
    if (!isAuthenticated || !user) return null;

    // Map MozaiksCore user to ChatUI user format
    return {
      id: user.user_id || user.id || user.sub,
      username: user.username || user.name || user.email,
      email: user.email,
      avatar: user.avatar || user.picture || '/default-avatar.png',
      role: user.role || 'user',
    };
  }

  async login(credentials) {
    // ChatUI should not initiate login — delegate to host app
    console.warn('[MozaiksCoreAuthAdapter] Login should be handled by the host app');
    return { success: false, error: 'Login must be handled by the host app' };
  }

  async logout() {
    try {
      await this.auth.logout?.();
      this.notifyAuthStateChange(null);
      return { success: true };
    } catch (error) {
      console.error('[MozaiksCoreAuthAdapter] Logout error:', error);
      return { success: false, error: error.message };
    }
  }

  async refreshToken() {
    // Token refresh is handled by the host app's auth context
    try {
      await this.auth.refreshUser?.();
      return { success: true };
    } catch (error) {
      return { success: false, error: error.message };
    }
  }

  getAccessToken() {
    // Synchronous access - for API calls within ChatUI
    // The host auth context handles async token retrieval
    if (typeof this.auth.getAccessToken === 'function') {
      // getAccessToken might be async; we need a cached/sync version
      // For now, trigger it and cache. ChatUI's api.js handles async fetching.
      return this._cachedToken || null;
    }
    return null;
  }

  /**
   * Async method to get the access token for WebSocket/API connections.
   * ChatUI's api.js will call this when needed.
   */
  async getAccessTokenAsync() {
    if (typeof this.auth.getRuntimeAccessToken === 'function') {
      // Prefer the runtime token (for connecting to MozaiksAI)
      this._cachedToken = await this.auth.getRuntimeAccessToken();
      return this._cachedToken;
    }
    if (typeof this.auth.getAccessToken === 'function') {
      this._cachedToken = await this.auth.getAccessToken();
      return this._cachedToken;
    }
    return null;
  }

  /**
   * Get the user's subject claim (for routing hints in WebSocket paths).
   */
  async getTokenSubject() {
    if (typeof this.auth.getTokenSubject === 'function') {
      return await this.auth.getTokenSubject();
    }
    // Fallback to user id
    const user = await this.getCurrentUser();
    return user?.id || null;
  }

  onAuthStateChange(callback) {
    this.authStateCallbacks.push(callback);
    
    // Immediately call with current state
    this.getCurrentUser().then(user => {
      callback(user);
      this._lastUser = user;
    });

    // Return unsubscribe function
    return () => {
      const idx = this.authStateCallbacks.indexOf(callback);
      if (idx > -1) this.authStateCallbacks.splice(idx, 1);
    };
  }

  notifyAuthStateChange(user) {
    this._lastUser = user;
    this.authStateCallbacks.forEach(callback => {
      try {
        callback(user);
      } catch (error) {
        console.error('[MozaiksCoreAuthAdapter] Auth state callback error:', error);
      }
    });
  }

  /**
   * Call this when the host app's auth state changes.
   * This should be called from a useEffect that watches the auth context.
   */
  syncAuthState() {
    this.getCurrentUser().then(user => {
      const changed = JSON.stringify(user) !== JSON.stringify(this._lastUser);
      if (changed) {
        this.notifyAuthStateChange(user);
      }
    });
  }
}

/**
 * Noop adapter for when no auth context is available.
 * Returns null for all auth operations.
 */
class NoopAuthAdapter extends AuthAdapter {
  async getCurrentUser() {
    return null;
  }

  async login() {
    return { success: false, error: 'No auth context available' };
  }

  async logout() {
    return { success: true };
  }

  async refreshToken() {
    return { success: false, error: 'No auth context available' };
  }

  getAccessToken() {
    return null;
  }

  onAuthStateChange(callback) {
    callback(null);
    return () => {};
  }
}

export default createMozaiksCoreAuthAdapter;
