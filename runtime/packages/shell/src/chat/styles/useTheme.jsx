// ============================================================================
// FILE: ChatUI/src/styles/useTheme.js
// PURPOSE: React hook for accessing theme configuration in components
// USAGE: const theme = useTheme();
// ============================================================================

import { useState, useEffect } from 'react';
import { getTheme, DEFAULT_THEME, getCurrentAppId } from './themeProvider';

const DYNAMIC_FALLBACKS = {
  primary: {
    main: {
      bg: 'bg-[var(--color-primary)]',
      text: 'text-[var(--color-text-on-accent)] text-white',
      border: 'border-[var(--color-primary)]',
      ring: 'ring-[var(--color-primary-light)]',
      shadow: '[box-shadow:0_0_0_rgba(var(--color-primary-rgb),0.3)]',
    },
    light: {
      bg: 'bg-[var(--color-primary-light)]',
      text: 'text-[var(--color-text-primary)] text-slate-900',
      border: 'border-[var(--color-primary-light)]',
      ring: 'ring-[var(--color-primary-light)]',
      shadow: '[box-shadow:0_0_0_rgba(var(--color-primary-light-rgb),0.3)]',
    },
    dark: {
      bg: 'bg-[var(--color-primary-dark)]',
      text: 'text-[var(--color-text-on-accent)] text-white',
      border: 'border-[var(--color-primary-dark)]',
      ring: 'ring-[var(--color-primary-dark)]',
      shadow: '[box-shadow:0_0_0_rgba(var(--color-primary-dark-rgb),0.3)]',
    },
  },
  secondary: {
    main: {
      bg: 'bg-[var(--color-secondary)]',
      text: 'text-[var(--color-text-on-accent)] text-white',
      border: 'border-[var(--color-secondary)]',
      ring: 'ring-[var(--color-secondary-light)]',
      shadow: '[box-shadow:0_0_0_rgba(var(--color-secondary-rgb),0.3)]',
    },
    light: {
      bg: 'bg-[var(--color-secondary-light)]',
      text: 'text-[var(--color-text-primary)] text-slate-900',
      border: 'border-[var(--color-secondary-light)]',
      ring: 'ring-[var(--color-secondary-light)]',
      shadow: '[box-shadow:0_0_0_rgba(var(--color-secondary-light-rgb),0.3)]',
    },
    dark: {
      bg: 'bg-[var(--color-secondary-dark)]',
      text: 'text-[var(--color-text-on-accent)] text-white',
      border: 'border-[var(--color-secondary-dark)]',
      ring: 'ring-[var(--color-secondary-dark)]',
      shadow: '[box-shadow:0_0_0_rgba(var(--color-secondary-dark-rgb),0.35)]',
    },
  },
  accent: {
    main: {
      bg: 'bg-[var(--color-accent)]',
      text: 'text-[var(--color-text-on-accent)] text-white',
      border: 'border-[var(--color-accent)]',
      ring: 'ring-[var(--color-accent-light)]',
      shadow: '[box-shadow:0_0_0_rgba(var(--color-accent-rgb),0.3)]',
    },
    light: {
      bg: 'bg-[var(--color-accent-light)]',
      text: 'text-[var(--color-text-primary)] text-slate-900',
      border: 'border-[var(--color-accent-light)]',
      ring: 'ring-[var(--color-accent-light)]',
      shadow: '[box-shadow:0_0_0_rgba(var(--color-accent-light-rgb),0.3)]',
    },
    dark: {
      bg: 'bg-[var(--color-accent-dark)]',
      text: 'text-[var(--color-text-on-accent)] text-white',
      border: 'border-[var(--color-accent-dark)]',
      ring: 'ring-[var(--color-accent)]',
      shadow: '[box-shadow:0_0_0_rgba(var(--color-accent-dark-rgb),0.35)]',
    },
  },
  success: {
    main: {
      bg: 'bg-[var(--color-success)]',
      text: 'text-[var(--color-text-on-accent)] text-white',
      border: 'border-[var(--color-success)]',
      ring: 'ring-[var(--color-success)]',
      shadow: '[box-shadow:0_0_0_rgba(var(--color-success-rgb),0.25)]',
    },
  },
  warning: {
    main: {
      bg: 'bg-[var(--color-accent)]',
      text: 'text-[var(--color-text-primary)] text-slate-900',
      border: 'border-[var(--color-accent)]',
      ring: 'ring-[var(--color-accent-light)]',
      shadow: '[box-shadow:0_0_0_rgba(var(--color-accent-rgb),0.4)]',
    },
  },
  error: {
    main: {
      bg: 'bg-[var(--color-error)]',
      text: 'text-[var(--color-text-on-accent)] text-white',
      border: 'border-[var(--color-error)]',
      ring: 'ring-[var(--color-error)]',
      shadow: '[box-shadow:0_0_0_rgba(var(--color-error-rgb),0.3)]',
    },
  },
};

const VAR_MAP = {
  primary: { main: 'color-primary', light: 'color-primary-light', dark: 'color-primary-dark' },
  secondary: { main: 'color-secondary', light: 'color-secondary-light', dark: 'color-secondary-dark' },
  accent: { main: 'color-accent', light: 'color-accent-light', dark: 'color-accent-dark' },
  success: { main: 'color-success' },
  warning: { main: 'color-warning' },
  error: { main: 'color-error' },
};

/**
 * THEME CONTEXT HOOK
 * Provides access to current theme configuration
 * 
 * @param {string} appId - Optional app ID (defaults to context)
 * @returns {Object} Theme configuration object
 * 
 * @example
 * const theme = useTheme();
 * console.log(theme.colors.primary.main); // '#06b6d4'
 * console.log(theme.fonts.heading.family); // 'Orbitron'
 */
export function useTheme(appId = null) {
  const [theme, setTheme] = useState(DEFAULT_THEME);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function loadTheme() {
      try {
        // Get app ID from props or context
        const eid = appId || getCurrentAppId();
        
        console.log(`🎨 [useTheme] Loading theme for app: ${eid}`);
        const loadedTheme = await getTheme(eid);
        
        if (!cancelled) {
          setTheme(loadedTheme);
          setLoading(false);
        }
      } catch (error) {
        console.error('🎨 [useTheme] Failed to load theme:', error);
        if (!cancelled) {
          setTheme(DEFAULT_THEME);
          setLoading(false);
        }
      }
    }

    loadTheme();

    return () => {
      cancelled = true;
    };
  }, [appId]);

  return { theme, loading };
}

/**
 * THEME-AWARE COLOR HELPER
 * Maps semantic color names to actual hex values from theme
 * 
 * @param {Object} theme - Theme object from useTheme()
 * @param {string} colorKey - Semantic color key (e.g., 'primary', 'secondary')
 * @returns {Object} Color values { main, light, dark, name }
 * 
 * @example
 * const theme = useTheme();
 * const primaryColor = getThemeColor(theme, 'primary');
 * <div style={{ backgroundColor: primaryColor.main }}>...</div>
 */
export function getThemeColor(theme, colorKey = 'primary') {
  return theme?.colors?.[colorKey] || DEFAULT_THEME.colors.primary;
}

/**
 * THEME-AWARE FONT HELPER
 * Gets font configuration from theme
 * 
 * @param {Object} theme - Theme object from useTheme()
 * @param {string} fontKey - Font type ('body', 'heading', 'logo')
 * @returns {Object} Font configuration { family, fallbacks, tailwindClass }
 * 
 * @example
 * const theme = useTheme();
 * const headingFont = getThemeFont(theme, 'heading');
 * <h1 className={headingFont.tailwindClass}>Title</h1>
 */
export function getThemeFont(theme, fontKey = 'body') {
  return theme?.fonts?.[fontKey] || DEFAULT_THEME.fonts.body;
}

/**
 * DYNAMIC COLOR CLASS GENERATOR
 * Generates Tailwind-compatible color classes based on theme
 * 
 * FUTURE ENHANCEMENT: When we migrate to CSS variables
 * this will generate classes like 'bg-[var(--color-primary)]'
 * 
 * CURRENT: Returns static Tailwind classes (cyan/violet)
 * 
 * @param {Object} theme - Theme object
 * @param {string} type - Color type ('primary', 'secondary', 'accent')
 * @param {string} variant - Variant ('main', 'light', 'dark')
 * @param {string} property - CSS property ('bg', 'text', 'border', 'ring', 'shadow')
 * @returns {string} Tailwind class string
 * 
 * @example
 * const theme = useTheme();
 * const bgClass = getDynamicColorClass(theme, 'primary', 'main', 'bg');
 * <div className={bgClass}>...</div>
 */
export function getDynamicColorClass(theme, type = 'primary', variant = 'main', property = 'bg') {
  // Intentionally unused for now but kept for future custom logic
  void theme;

  const varName = VAR_MAP[type]?.[variant] || VAR_MAP[type]?.main || 'color-primary';
  const fallback = DYNAMIC_FALLBACKS[type]?.[variant]?.[property]
    || DYNAMIC_FALLBACKS[type]?.main?.[property]
    || '';

  if (property === 'shadow') {
    const shadowVar = {
      primary: 'shadow-primary',
      secondary: 'shadow-secondary',
      accent: 'shadow-accent',
      success: 'shadow-success',
      warning: 'shadow-warning',
      error: 'shadow-error',
    }[type] || 'shadow-primary';

    const base = `[box-shadow:var(--${shadowVar},0_0_0_rgba(0,0,0,0))]`;
    return [base, fallback].filter(Boolean).join(' ').trim();
  }

  const propertyMap = {
    bg: `bg-[var(--${varName})]`,
    text: `text-[var(--${varName})]`,
    border: `border-[var(--${varName})]`,
    ring: `ring-[var(--${varName})]`,
  };

  const base = propertyMap[property] || propertyMap.bg;
  return [base, fallback].filter(Boolean).join(' ').trim();
}

export default useTheme;
