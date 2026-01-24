// Simple configuration for agentic chat platform
class ChatUIConfig {
  constructor() {
    this.config = this.loadConfig();
  }

  loadConfig() {
    // Detect production vs development
    const isProduction = process.env.NODE_ENV === 'production';
    
    // Default auth mode: 'token' in production, 'mock' in development
    // Mock mode should NEVER be used in production - it bypasses authentication
    const defaultAuthMode = isProduction ? 'token' : (process.env.REACT_APP_AUTH_MODE || 'mock');
    
    // Warn if mock mode is explicitly set in production
    if (isProduction && process.env.REACT_APP_AUTH_MODE === 'mock') {
      console.error('[SECURITY WARNING] Mock auth mode is disabled in production builds');
    }
    
    const defaultConfig = {
      // API Configuration
      api: {
        baseUrl: process.env.REACT_APP_API_BASE_URL || 'http://localhost:8000',
        wsUrl: process.env.REACT_APP_WS_URL || 'ws://localhost:8000',
      },

      // Auth Configuration
      auth: {
        // In production: always use 'token' mode (enforced)
        // In development: use 'mock' for convenience unless overridden
        mode: isProduction ? 'token' : defaultAuthMode,
      },

      // UI Configuration
      ui: {
        showHeader: process.env.REACT_APP_SHOW_HEADER !== 'false',
        enableNotifications: process.env.REACT_APP_ENABLE_NOTIFICATIONS !== 'false',
      },

      // Chat Configuration
      chat: {
        // Auth system placeholder - replace with actual auth implementation
        defaultAppId: process.env.REACT_APP_DEFAULT_APP_ID || process.env.REACT_APP_DEFAULT_app_id,
        defaultUserId: process.env.REACT_APP_DEFAULT_USER_ID || '56132',
        // Do not force a placeholder workflow; fall back to backend discovery
        defaultWorkflow: process.env.REACT_APP_DEFAULT_WORKFLOW || '',
      },
    };

    // Override with window.ChatUIConfig if available
    if (typeof window !== 'undefined' && window.ChatUIConfig) {
      return { ...defaultConfig, ...window.ChatUIConfig };
    }

    return defaultConfig;
  }

  get(path) {
    return path.split('.').reduce((current, key) => current?.[key], this.config);
  }

  getConfig() {
    return this.config;
  }
}

// Singleton instance
const configInstance = new ChatUIConfig();

export default configInstance;
