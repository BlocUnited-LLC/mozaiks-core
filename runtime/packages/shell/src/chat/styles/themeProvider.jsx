// ============================================================================
// FILE: ChatUI/src/styles/themeProvider.js
// PURPOSE: Dynamic theme system for multi-tenant branding
// USAGE: Loads app-specific fonts, colors, and design tokens
// ============================================================================

/**
 * DEFAULT THEME (MozaiksAI brand)
 * Fallback when no app-specific theme is configured
 */
const DEFAULT_THEME = {
  // Font configuration
  fonts: {
    body: {
      family: 'Rajdhani',
      fallbacks: 'ui-sans-serif, system-ui, -apple-system, sans-serif',
      googleFont: 'https://fonts.googleapis.com/css2?family=Rajdhani:wght@300;400;500;600;700&display=swap',
      tailwindClass: 'font-sans',
    },
    heading: {
      family: 'Orbitron',
      fallbacks: 'Rajdhani, ui-sans-serif, system-ui, sans-serif',
      googleFont: 'https://fonts.googleapis.com/css2?family=Orbitron:wght@400;600;700;900&display=swap',
      tailwindClass: 'font-heading',
    },
    logo: {
      family: 'Fagrak Inline',
      fallbacks: 'Rajdhani, ui-sans-serif, system-ui, sans-serif',
      localFont: true, // Loaded via @font-face in index.css
      tailwindClass: 'font-logo',
    },
  },

  // Color palette and semantic surfaces
  colors: {
    primary: {
      main: '#06b6d4',      // cyan-500
      light: '#67e8f9',     // cyan-300
      dark: '#0e7490',      // cyan-700
      name: 'cyan',
    },
    secondary: {
      main: '#8b5cf6',      // violet-500
      light: '#a78bfa',     // violet-400
      dark: '#6d28d9',      // violet-700
      name: 'violet',
    },
    accent: {
      main: '#f59e0b',      // amber-500
      light: '#fbbf24',     // amber-400
      dark: '#d97706',      // amber-600
      name: 'amber',
    },
    success: {
      main: '#10b981',      // emerald-500
      light: '#34d399',     // emerald-400
      dark: '#059669',      // emerald-600
      name: 'emerald',
    },
    warning: {
      main: '#f59e0b',      // amber-500
      light: '#fbbf24',     // amber-400
      dark: '#d97706',      // amber-600
      name: 'amber',
    },
    error: {
      main: '#ef4444',      // red-500
      light: '#f87171',     // red-400
      dark: '#dc2626',      // red-600
      name: 'red',
    },
    background: {
      base: '#0b1220',
      surface: '#0f1724',
      elevated: '#131d33',
      overlay: 'rgba(13, 23, 42, 0.72)',
    },
    border: {
      subtle: '#1e293b',
      strong: '#334155',
      accent: '#06b6d4',
    },
    text: {
      primary: '#e6eef8',
      secondary: '#94a3b8',
      muted: '#64748b',
      onAccent: '#f8fafc',
    },
  },

  // Shadow tokens reused across the design system
  shadows: {
    primary: '0 20px 45px rgba(6, 182, 212, 0.24)',
    secondary: '0 20px 45px rgba(139, 92, 246, 0.24)',
    accent: '0 18px 40px rgba(245, 158, 11, 0.32)',
    success: '0 18px 40px rgba(16, 185, 129, 0.24)',
    warning: '0 18px 45px rgba(245, 158, 11, 0.34)',
    error: '0 18px 45px rgba(239, 68, 68, 0.3)',
    elevated: '0 24px 60px rgba(11, 18, 32, 0.55)',
    focus: '0 0 0 3px rgba(8, 145, 178, 0.55)',
  },

  // Branding
  branding: {
    name: 'MozaiksAI',
    logo: '/mozaik_logo.svg',
    favicon: '/mozaik.png',
  },
};

/**
 * THEME CACHE
 * Stores loaded themes by app_id
 */
const themeCache = new Map();

const CURRENT_APP_ID_STORAGE_KEY = 'mozaiks.current_app_id';

function normalizeAppId(appId) {
  if (appId == null) return 'default';
  if (typeof appId === 'string') {
    const trimmed = appId.trim();
    return trimmed.length > 0 ? trimmed : 'default';
  }
  return String(appId) || 'default';
}

function cacheTheme(appId, theme, meta = null) {
  themeCache.set(appId, { theme, meta });
}

/**
 * LOAD THEME FROM API
 * Fetches app-specific theme configuration
 * 
 * @param {string} appId - Unique app identifier
 * @returns {Promise<Object>} Theme configuration object
 */
async function loadThemeFromAPI(appId) {
  const normalizedId = normalizeAppId(appId);
  try {
    console.log(`🎨 [THEME] Loading theme for app: ${normalizedId}`);

  const controller = new AbortController();
  const timeout = globalThis.setTimeout(() => controller.abort(), 8000);

    try {
      const response = await fetch(`/api/themes/${encodeURIComponent(normalizedId)}`, {
        method: 'GET',
        headers: {
          Accept: 'application/json',
        },
        signal: controller.signal,
      });

      if (response.status === 404) {
        console.warn(`⚠️ [THEME] Theme not found for ${normalizedId}, using default`);
        return { theme: DEFAULT_THEME, meta: { source: 'default', appId: normalizedId } };
      }

      if (!response.ok) {
        const text = await response.text();
        throw new Error(`Theme API error (${response.status}): ${text}`);
      }

      const data = await response.json();
      const theme = data?.theme;

      if (!theme || typeof theme !== 'object') {
        throw new Error('Theme API returned invalid payload');
      }

      return {
        theme,
        meta: {
          source: data?.source || 'custom',
          appId: data?.app_id || data?.app_id || normalizedId,
          updatedAt: data?.updatedAt || null,
          updatedBy: data?.updatedBy || null,
        },
      };
    } catch (error) {
      if (error?.name === 'AbortError') {
        console.warn(`⏱️ [THEME] Theme request timed out for ${normalizedId}`);
      } else {
        console.error(`❌ [THEME] Failed to fetch theme for ${normalizedId}:`, error);
      }
      return { theme: DEFAULT_THEME, meta: { source: 'default', appId: normalizedId } };
    } finally {
      globalThis.clearTimeout(timeout);
    }
  } catch (error) {
    console.warn(`⚠️ [THEME] Failed to load theme for ${appId}, using default:`, error);
    return { theme: DEFAULT_THEME, meta: { source: 'default', appId: normalizedId } };
  }
}

/**
 * GET THEME
 * Retrieves theme with caching
 * 
 * @param {string} appId - Unique app identifier
 * @returns {Promise<Object>} Theme configuration
 */
export async function getTheme(appId = 'default') {
  const normalizedId = normalizeAppId(appId);
  // Check cache first
  if (themeCache.has(normalizedId)) {
    const cached = themeCache.get(normalizedId);
    console.log(`🎨 [THEME] Using cached theme for ${normalizedId}`);
    return cached.theme;
  }

  // Load from API
  const { theme, meta } = await loadThemeFromAPI(normalizedId);

  // Cache it
  cacheTheme(normalizedId, theme, meta);

  return theme;
}

export function getThemeMetadata(appId = 'default') {
  const normalizedId = normalizeAppId(appId);
  const cached = themeCache.get(normalizedId);
  return cached?.meta || null;
}

/**
 * APPLY THEME TO DOM
 * Injects theme fonts and CSS variables into the document
 * 
 * @param {Object} theme - Theme configuration object
 */
export function applyTheme(theme) {
  try {
    console.log(`🎨 [THEME] Applying theme:`, theme.branding?.name || 'Custom');

    // 1. Load Google Fonts if needed
    const fonts = theme.fonts || DEFAULT_THEME.fonts;
    Object.values(fonts).forEach((font) => {
      if (font.googleFont && !font.localFont) {
        loadGoogleFont(font.googleFont);
      }
    });

    // 2. Update CSS custom properties for colors
  const colors = theme.colors || DEFAULT_THEME.colors;
  const shadows = theme.shadows || DEFAULT_THEME.shadows;
  updateCSSVariables(colors, shadows);

    // 3. Update document title and favicon
    const branding = theme.branding || DEFAULT_THEME.branding;
    if (branding.name) {
      document.title = branding.name;
    }
    if (branding.favicon) {
      updateFavicon(branding.favicon);
    }

    console.log(`✅ [THEME] Theme applied successfully`);
  } catch (error) {
    console.error(`❌ [THEME] Error applying theme:`, error);
  }
}

/**
 * LOAD GOOGLE FONT
 * Injects Google Font link into document head
 * 
 * @param {string} fontUrl - Google Fonts URL
 */
function loadGoogleFont(fontUrl) {
  // Check if already loaded
  const existingLink = document.querySelector(`link[href="${fontUrl}"]`);
  if (existingLink) return;

  // Create and inject link element
  const link = document.createElement('link');
  link.rel = 'stylesheet';
  link.href = fontUrl;
  document.head.appendChild(link);
  
  console.log(`🎨 [THEME] Loaded font: ${fontUrl}`);
}

/**
 * Convert a hex color string into "r, g, b" format suitable for CSS vars
 * @param {string} hex
 * @returns {string|null}
 */
function hexToRgb(hex) {
  if (!hex || typeof hex !== 'string') return null;
  const normalized = hex.replace('#', '').trim();

  if (![3, 6].includes(normalized.length)) return null;

  const expand = normalized.length === 3
    ? normalized.split('').map((char) => `${char}${char}`).join('')
    : normalized;

  const intVal = Number.parseInt(expand, 16);
  if (Number.isNaN(intVal)) return null;

  const r = (intVal >> 16) & 255;
  const g = (intVal >> 8) & 255;
  const b = intVal & 255;

  return `${r}, ${g}, ${b}`;
}

function setColorVar(root, name, value, fallback) {
  const resolved = (value || fallback || '').trim();
  if (!resolved) return;

  root.style.setProperty(`--${name}`, resolved);

  const rgb = hexToRgb(resolved);
  if (rgb) {
    root.style.setProperty(`--${name}-rgb`, rgb);
  }
}

function setShadowVar(root, name, value, fallback) {
  const resolved = value || fallback;
  if (!resolved) return;
  root.style.setProperty(`--${name}`, resolved);
}

/**
 * UPDATE CSS VARIABLES
 * Injects color values as CSS custom properties
 * 
 * @param {Object} themeColors - Color configuration
 * @param {Object} themeShadows - Shadow token configuration
 */
function updateCSSVariables(themeColors, themeShadows) {
  const root = document.documentElement;

  const colors = themeColors || DEFAULT_THEME.colors;
  const shadows = themeShadows || DEFAULT_THEME.shadows;

  // Primary, secondary & accent scales
  setColorVar(root, 'color-primary', colors.primary?.main, DEFAULT_THEME.colors.primary.main);
  setColorVar(root, 'color-primary-light', colors.primary?.light, DEFAULT_THEME.colors.primary.light);
  setColorVar(root, 'color-primary-dark', colors.primary?.dark, DEFAULT_THEME.colors.primary.dark);

  setColorVar(root, 'color-secondary', colors.secondary?.main, DEFAULT_THEME.colors.secondary.main);
  setColorVar(root, 'color-secondary-light', colors.secondary?.light, DEFAULT_THEME.colors.secondary.light);
  setColorVar(root, 'color-secondary-dark', colors.secondary?.dark, DEFAULT_THEME.colors.secondary.dark);

  setColorVar(root, 'color-accent', colors.accent?.main, DEFAULT_THEME.colors.accent.main);
  setColorVar(root, 'color-accent-light', colors.accent?.light, DEFAULT_THEME.colors.accent.light);
  setColorVar(root, 'color-accent-dark', colors.accent?.dark, DEFAULT_THEME.colors.accent.dark);

  // Status colors
  setColorVar(root, 'color-success', colors.success?.main, DEFAULT_THEME.colors.success.main);
  setColorVar(root, 'color-warning', colors.warning?.main, DEFAULT_THEME.colors.warning.main);
  setColorVar(root, 'color-error', colors.error?.main, DEFAULT_THEME.colors.error.main);

  // Surface & background tokens
  setColorVar(root, 'color-background', colors.background?.base, DEFAULT_THEME.colors.background.base);
  setColorVar(root, 'color-surface', colors.background?.surface, DEFAULT_THEME.colors.background.surface);
  setColorVar(root, 'color-surface-alt', colors.background?.elevated, DEFAULT_THEME.colors.background.elevated);

  const surfaceOverlay = colors.background?.overlay || DEFAULT_THEME.colors.background.overlay;
  if (surfaceOverlay) {
    root.style.setProperty('--color-surface-overlay', surfaceOverlay);
  }

  // Border & text semantic colors
  setColorVar(root, 'color-border-subtle', colors.border?.subtle, DEFAULT_THEME.colors.border.subtle);
  setColorVar(root, 'color-border-strong', colors.border?.strong, DEFAULT_THEME.colors.border.strong);
  setColorVar(root, 'color-border-accent', colors.border?.accent, DEFAULT_THEME.colors.border.accent);

  setColorVar(root, 'color-text-primary', colors.text?.primary, DEFAULT_THEME.colors.text.primary);
  setColorVar(root, 'color-text-secondary', colors.text?.secondary, DEFAULT_THEME.colors.text.secondary);
  setColorVar(root, 'color-text-muted', colors.text?.muted, DEFAULT_THEME.colors.text.muted);
  setColorVar(root, 'color-text-on-accent', colors.text?.onAccent, DEFAULT_THEME.colors.text.onAccent);

  // Legacy aliases to ease migration
  setColorVar(root, 'color-card', colors.background?.surface, DEFAULT_THEME.colors.background.surface);
  setColorVar(root, 'color-border', colors.border?.subtle, DEFAULT_THEME.colors.border.subtle);
  setColorVar(root, 'color-dark', colors.background?.base, DEFAULT_THEME.colors.background.base);
  setColorVar(root, 'color-light', colors.text?.primary, DEFAULT_THEME.colors.text.primary);

  // Shadow tokens
  setShadowVar(root, 'shadow-primary', shadows?.primary, DEFAULT_THEME.shadows.primary);
  setShadowVar(root, 'shadow-secondary', shadows?.secondary, DEFAULT_THEME.shadows.secondary);
  setShadowVar(root, 'shadow-accent', shadows?.accent, DEFAULT_THEME.shadows.accent);
  setShadowVar(root, 'shadow-success', shadows?.success, DEFAULT_THEME.shadows.success);
  setShadowVar(root, 'shadow-warning', shadows?.warning, DEFAULT_THEME.shadows.warning);
  setShadowVar(root, 'shadow-error', shadows?.error, DEFAULT_THEME.shadows.error);
  setShadowVar(root, 'shadow-elevated', shadows?.elevated, DEFAULT_THEME.shadows.elevated);
  setShadowVar(root, 'shadow-focus', shadows?.focus, DEFAULT_THEME.shadows.focus);
}

/**
 * UPDATE FAVICON
 * Updates the document favicon
 * 
 * @param {string} faviconUrl - Path to favicon
 */
function updateFavicon(faviconUrl) {
  let link = document.querySelector("link[rel~='icon']");
  if (!link) {
    link = document.createElement('link');
    link.rel = 'icon';
    document.head.appendChild(link);
  }
  link.href = faviconUrl;
}

/**
 * CLEAR THEME CACHE
 * Force reload of theme on next access
 * 
 * @param {string} appId - Optional specific app to clear
 */
export function clearThemeCache(appId = null) {
  if (!appId) {
    themeCache.clear();
    console.log('🧹 [THEME] Cleared all theme cache');
    return;
  }

  const normalizedId = normalizeAppId(appId);
  themeCache.delete(normalizedId);
  console.log(`🧹 [THEME] Cleared cache for ${normalizedId}`);
}

/**
 * INITIALIZE THEME
 * Main entry point - loads and applies theme for app
 * 
 * @param {string} appId - Unique app identifier
 * @returns {Promise<Object>} Applied theme configuration
 */
export async function initializeTheme(appId = 'default') {
  const normalizedId = normalizeAppId(appId);
  console.log(`🎨 [THEME] Initializing theme for ${normalizedId}`);
  
  // Store current app ID for hook access
  if (normalizedId) {
    try {
      localStorage.setItem(CURRENT_APP_ID_STORAGE_KEY, normalizedId);
    } catch (_) {
      /* ignore storage errors */
    }
  }
  
  const theme = await getTheme(normalizedId);
  applyTheme(theme);
  
  return theme;
}

/**
 * GET CURRENT app ID
 * Helper to retrieve current app context
 * 
 * @returns {string} Current app ID or 'default'
 */
export function getCurrentAppId() {
  try {
    const stored = localStorage.getItem(CURRENT_APP_ID_STORAGE_KEY);
    if (!stored) return 'default';
    const trimmed = stored.trim();
    return trimmed.length > 0 ? trimmed : 'default';
  } catch (_) {
    return 'default';
  }
}

// Export default theme for reference
export { DEFAULT_THEME };
