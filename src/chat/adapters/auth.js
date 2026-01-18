// Authentication adapter interface
export class AuthAdapter {
  async getCurrentUser() {
    throw new Error('getCurrentUser must be implemented');
  }

  async login(credentials) {
    throw new Error('login must be implemented');
  }

  async logout() {
    throw new Error('logout must be implemented');
  }

  async refreshToken() {
    throw new Error('refreshToken must be implemented');
  }

  onAuthStateChange(callback) {
    throw new Error('onAuthStateChange must be implemented');
  }
  
  /**
   * Get the current access token for API calls.
   * This is used by api.js to add Authorization headers.
   */
  getAccessToken() {
    return null;
  }
}

// Mock Authentication Adapter (for development/standalone mode ONLY)
// WARNING: This adapter should NEVER be used in production!
export class MockAuthAdapter extends AuthAdapter {
  constructor() {
    super();
    // Check if we're in production and warn/disable
    if (typeof process !== 'undefined' && process.env?.NODE_ENV === 'production') {
      console.error('[SECURITY ERROR] MockAuthAdapter cannot be used in production!');
      this.disabled = true;
    }
    
    this.currentUser = {
      id: '56132',
      username: 'John Doe',
      email: 'john.doe@example.com',
      avatar: '/default-avatar.png',
      role: 'user'
    };
    this.authStateCallbacks = [];
  }

  async getCurrentUser() {
    if (this.disabled) return null;
    return this.currentUser;
  }

  async login(credentials) {
    if (this.disabled) return { success: false, error: 'Mock auth disabled in production' };
    
    // Mock login
    console.log('Mock login with:', credentials);
    this.notifyAuthStateChange(this.currentUser);
    return { success: true, user: this.currentUser };
  }

  async logout() {
    if (this.disabled) return { success: false };
    
    this.currentUser = null;
    this.notifyAuthStateChange(null);
    return { success: true };
  }

  async refreshToken() {
    if (this.disabled) return { success: false };
    return { success: true, token: 'mock-token' };
  }
  
  getAccessToken() {
    if (this.disabled) return null;
    // Mock adapter returns a placeholder token for development
    return 'mock-development-token';
  }

  onAuthStateChange(callback) {
    this.authStateCallbacks.push(callback);
    // Immediately call with current state
    callback(this.disabled ? null : this.currentUser);
  }

  notifyAuthStateChange(user) {
    this.authStateCallbacks.forEach(callback => callback(user));
  }

  setUser(user) {
    if (this.disabled) return;
    this.currentUser = user;
    this.notifyAuthStateChange(user);
  }
}

// External Authentication Adapter (for embedded mode)
// Use this when ChatUI is embedded in MozaiksCore or another host app
// that provides authentication context
export class ExternalAuthAdapter extends AuthAdapter {
  constructor(externalAuthProvider) {
    super();
    this.externalAuth = externalAuthProvider;
  }

  async getCurrentUser() {
    return this.externalAuth.getCurrentUser();
  }

  async login(credentials) {
    return this.externalAuth.login(credentials);
  }

  async logout() {
    return this.externalAuth.logout();
  }

  async refreshToken() {
    return this.externalAuth.refreshToken();
  }
  
  getAccessToken() {
    // Delegate to external auth provider
    if (typeof this.externalAuth.getAccessToken === 'function') {
      return this.externalAuth.getAccessToken();
    }
    return null;
  }

  onAuthStateChange(callback) {
    return this.externalAuth.onAuthStateChange(callback);
  }
}

// Token-based Authentication Adapter
export class TokenAuthAdapter extends AuthAdapter {
  constructor(apiBaseUrl, tokenKey = 'chatui_token') {
    super();
    this.apiBaseUrl = apiBaseUrl;
    this.tokenKey = tokenKey;
    this.currentUser = null;
    this.authStateCallbacks = [];
  }

  async getCurrentUser() {
    if (this.currentUser) return this.currentUser;

    const token = this.getAccessToken();
    if (!token) return null;

    try {
      const response = await fetch(`${this.apiBaseUrl}/api/auth/me`, {
        headers: { Authorization: `Bearer ${token}` }
      });

      if (response.ok) {
        this.currentUser = await response.json();
        return this.currentUser;
      }
    } catch (error) {
      console.error('Auth error:', error);
    }

    return null;
  }

  async login(credentials) {
    try {
      const response = await fetch(`${this.apiBaseUrl}/api/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(credentials)
      });

      if (response.ok) {
        const data = await response.json();
        localStorage.setItem(this.tokenKey, data.token);
        this.currentUser = data.user;
        this.notifyAuthStateChange(this.currentUser);
        return { success: true, user: this.currentUser };
      }
    } catch (error) {
      console.error('Login error:', error);
    }

    return { success: false, error: 'Login failed' };
  }

  async logout() {
    localStorage.removeItem(this.tokenKey);
    this.currentUser = null;
    this.notifyAuthStateChange(null);
    return { success: true };
  }

  async refreshToken() {
    // Implementation depends on your refresh token strategy
    return { success: true };
  }
  
  getAccessToken() {
    // Get token from localStorage
    return localStorage.getItem(this.tokenKey);
  }

  onAuthStateChange(callback) {
    this.authStateCallbacks.push(callback);
    // Immediately call with current state
    callback(this.currentUser);
  }

  notifyAuthStateChange(user) {
    this.authStateCallbacks.forEach(callback => callback(user));
  }
}
