// ============================================================================
// FILE: ChatUI/src/styles/artifactDesignSystem.js
// PURPOSE: Centralized design system for artifact UI components
// USAGE: Import into workflow UI components to maintain consistency
// NOTE: This module uses Tailwind utility classes that resolve dynamically
//       based on the active theme loaded via themeProvider.js
// ============================================================================

/**
 * FONT SYSTEM
 * Uses Tailwind font utilities that map to theme-configured fonts
 * The actual font families are loaded dynamically per app
 * via themeProvider.js and tailwind.config.js
 * 
 * Default (MozaiksAI brand):
 * - Body/Default: Rajdhani (font-sans)
 * - Headings: Orbitron (font-heading)
 * - Logo/Branding: Fagrak Inline (font-logo)
 * 
 * These can be overridden per app via theme API
 */
export const fonts = {
  // Body text - default across all components
  // Resolves to theme.fonts.body.family (e.g., Rajdhani)
  body: 'font-sans',
  
  // Headings (h1-h6, section titles)
  // Resolves to theme.fonts.heading.family (e.g., Orbitron)
  heading: 'font-heading',
  
  // Branding elements (logos, special callouts)
  // Resolves to theme.fonts.logo.family (e.g., Fagrak Inline)
  logo: 'font-logo',
};

// Helper utilities for composing CSS variable backed Tailwind classes
const createVarClass = (type, varName, fallback = '') => {
  const map = {
    bg: `bg-[var(--${varName})]`,
    border: `border-[var(--${varName})]`,
    text: `text-[var(--${varName})]`,
    ring: `ring-[var(--${varName})]`,
  };

  const base = map[type];
  if (!base) return fallback;
  return [base, fallback].filter(Boolean).join(' ').trim();
};

const createAlphaVarClass = (type, varName, alpha, fallback = '') => {
  const sanitized = Math.max(0, Math.min(1, Number(alpha) || 0));
  const base = `${type}-[rgba(var(--${varName}-rgb),${sanitized})]`;
  return [base, fallback].filter(Boolean).join(' ').trim();
};

const createShadowVarClass = (varName, fallback = '') => {
  const base = `[box-shadow:var(--${varName},0_0_0_rgba(0,0,0,0))]`;
  return [base, fallback].filter(Boolean).join(' ').trim();
};

const createGradientStopVar = (stop, varName, fallback = '') => {
  const base = `${stop}-[var(--${varName})]`;
  return [base, fallback].filter(Boolean).join(' ').trim();
};

/**
 * COLOR & SURFACE TOKENS
 * All tokens resolve to CSS variables with Tailwind fallbacks for safety.
 */
const FALLBACKS = {
  brand: {
  primary: { bg: 'bg-[var(--color-primary)]', border: 'border-[var(--color-primary)]', text: 'text-white', ring: 'ring-[var(--color-primary-light)]' },
  primaryLight: { bg: 'bg-[var(--color-primary-light)]', border: 'border-[var(--color-primary-light)]', text: 'text-cyan-100', ring: 'ring-[var(--color-primary-light)]' },
    primaryDark: { bg: 'bg-[var(--color-primary-dark)]', border: 'border-[var(--color-primary-dark)]', text: 'text-white', ring: 'ring-[var(--color-primary-dark)]' },
    secondary: { bg: 'bg-[var(--color-secondary)]', border: 'border-[var(--color-secondary)]', text: 'text-white', ring: 'ring-[var(--color-secondary-light)]' },
    accent: { bg: 'bg-[var(--color-accent)]', border: 'border-[var(--color-accent)]', text: 'text-slate-900', ring: 'ring-[var(--color-accent-light)]' },
  },
  status: {
    success: { bg: 'bg-[var(--color-success)]', border: 'border-[var(--color-success)]', text: 'text-white', ring: 'ring-[var(--color-success)]' },
    warning: { bg: 'bg-[var(--color-accent)]', border: 'border-[var(--color-accent)]', text: 'text-slate-900', ring: 'ring-[var(--color-accent-light)]' },
    error: { bg: 'bg-[var(--color-error)]', border: 'border-[var(--color-error)]', text: 'text-white', ring: 'ring-[var(--color-error)]' },
  },
  surface: {
    base: 'bg-slate-950',
    baseOverlay: 'bg-slate-950/85',
    raised: 'bg-slate-900',
    raisedOverlay: 'bg-slate-900/80',
    elevated: 'bg-slate-850',
    overlay: 'bg-slate-900/60',
  },
  border: {
    subtle: 'border-slate-700',
    strong: 'border-slate-600',
    accent: 'border-[var(--color-primary)]',
  },
  text: {
    primary: 'text-slate-100',
    secondary: 'text-slate-300',
    muted: 'text-slate-400',
    onAccent: 'text-white',
  },
  shadow: {
    primary: '[box-shadow:0_0_0_rgba(var(--color-primary-rgb),0.3)]',
    secondary: '[box-shadow:0_0_0_rgba(var(--color-secondary-rgb),0.3)]',
    accent: '[box-shadow:0_0_0_rgba(var(--color-accent-rgb),0.3)]',
    success: '[box-shadow:0_0_0_rgba(var(--color-success-rgb),0.25)]',
    warning: '[box-shadow:0_0_0_rgba(var(--color-accent-rgb),0.4)]',
    error: '[box-shadow:0_0_0_rgba(var(--color-error-rgb),0.3)]',
    elevated: 'shadow-[0_24px_60px_rgba(15,23,42,0.55)]',
    focus: 'shadow-[0_0_0_3px_rgba(8,145,178,0.55)]',
  },
};

const createColorScale = (varName, fallback = {}) => ({
  bg: createVarClass('bg', varName, fallback.bg),
  border: createVarClass('border', varName, fallback.border),
  text: createVarClass('text', varName, fallback.text),
  ring: createVarClass('ring', varName, fallback.ring),
});

export const colors = {
  brand: {
    primary: createColorScale('color-primary', FALLBACKS.brand.primary),
    primaryLight: createColorScale('color-primary-light', FALLBACKS.brand.primaryLight),
    primaryDark: createColorScale('color-primary-dark', FALLBACKS.brand.primaryDark),
    secondary: createColorScale('color-secondary', FALLBACKS.brand.secondary),
    accent: createColorScale('color-accent', FALLBACKS.brand.accent),
  },
  status: {
    success: createColorScale('color-success', FALLBACKS.status.success),
    warning: createColorScale('color-warning', FALLBACKS.status.warning),
    error: createColorScale('color-error', FALLBACKS.status.error),
  },
  surface: {
    base: createVarClass('bg', 'color-background', FALLBACKS.surface.base),
    baseOverlay: createAlphaVarClass('bg', 'color-background', 0.85, FALLBACKS.surface.baseOverlay),
    raised: createVarClass('bg', 'color-surface', FALLBACKS.surface.raised),
    raisedOverlay: createAlphaVarClass('bg', 'color-surface', 0.75, FALLBACKS.surface.raisedOverlay),
    elevated: createVarClass('bg', 'color-surface-alt', FALLBACKS.surface.elevated),
    overlay: `bg-[var(--color-surface-overlay,rgba(13,23,42,0.72))] ${FALLBACKS.surface.overlay}`,
  },
  border: {
    subtle: createVarClass('border', 'color-border-subtle', FALLBACKS.border.subtle),
    strong: createVarClass('border', 'color-border-strong', FALLBACKS.border.strong),
    accent: createVarClass('border', 'color-border-accent', FALLBACKS.border.accent),
  },
  text: {
    primary: createVarClass('text', 'color-text-primary', FALLBACKS.text.primary),
    secondary: createVarClass('text', 'color-text-secondary', FALLBACKS.text.secondary),
    muted: createVarClass('text', 'color-text-muted', FALLBACKS.text.muted),
    onAccent: createVarClass('text', 'color-text-on-accent', FALLBACKS.text.onAccent),
  },
};

export const shadows = {
  primary: createShadowVarClass('shadow-primary', FALLBACKS.shadow.primary),
  secondary: createShadowVarClass('shadow-secondary', FALLBACKS.shadow.secondary),
  accent: createShadowVarClass('shadow-accent', FALLBACKS.shadow.accent),
  success: createShadowVarClass('shadow-success', FALLBACKS.shadow.success),
  warning: createShadowVarClass('shadow-warning', FALLBACKS.shadow.warning),
  error: createShadowVarClass('shadow-error', FALLBACKS.shadow.error),
  elevated: createShadowVarClass('shadow-elevated', FALLBACKS.shadow.elevated),
  focus: createShadowVarClass('shadow-focus', FALLBACKS.shadow.focus),
};

export const gradients = {
  surface: {
    from: createGradientStopVar('from', 'color-surface-alt', 'from-slate-900'),
    to: createGradientStopVar('to', 'color-surface', 'to-slate-800'),
  },
  accent: {
    from: createGradientStopVar('from', 'color-primary', 'from-[var(--color-primary)]'),
    to: createGradientStopVar('to', 'color-secondary', 'to-[var(--color-secondary)]'),
  },
};

/**
 * TYPOGRAPHY SCALE
 * Consistent sizing for text elements
 */
export const typography = {
  // Display/Hero text
  display: {
    xl: `text-5xl ${fonts.heading} font-black`, // Main artifact titles
    lg: `text-4xl ${fonts.heading} font-black`,
    md: `text-3xl ${fonts.heading} font-bold`,
  },
  
  // Headings
  heading: {
    xl: `text-2xl ${fonts.heading} font-bold`,
    lg: `text-xl ${fonts.heading} font-black`,
    md: `text-lg ${fonts.heading} font-bold`,
    sm: `text-base ${fonts.heading} font-bold`,
    xs: `text-sm ${fonts.heading} font-bold`,
  },
  
  // Body text
  body: {
    lg: `text-base ${fonts.body}`,
    md: `text-sm ${fonts.body}`,
    sm: `text-xs ${fonts.body}`,
  },
  
  // Labels/badges
  label: {
    lg: `text-sm ${fonts.body} font-bold uppercase tracking-wider`,
    md: `text-xs ${fonts.body} font-bold uppercase tracking-wide`,
    sm: `text-[11px] ${fonts.body} font-bold uppercase tracking-wider`,
  },
};

/**
 * SPACING SYSTEM
 * Consistent padding/gaps
 */
export const spacing = {
  section: 'space-y-8', // Between major sections
  subsection: 'space-y-6', // Between subsections
  group: 'space-y-4', // Between related items
  tight: 'space-y-3', // Tight groupings
  items: 'space-y-2', // Individual items
  
  padding: {
    xl: 'p-8',
    lg: 'p-6',
    md: 'p-5',
    sm: 'p-4',
    xs: 'p-3',
  },
  
  gap: {
    lg: 'gap-5',
    md: 'gap-4',
    sm: 'gap-3',
    xs: 'gap-2',
  },
};

/**
 * COMPONENT PATTERNS
 * Reusable component style patterns
 */
export const components = {
  // Card styles
  card: {
    primary: [
      'rounded-2xl border-3',
      colors.border.accent,
      'bg-gradient-to-br',
      gradients.surface.from,
      gradients.surface.to,
      shadows.primary,
      'shadow-2xl',
      spacing.padding.xl,
    ].join(' '),
    secondary: [
      'rounded-xl border-2',
      colors.border.subtle,
      colors.surface.raised,
      shadows.elevated,
      spacing.padding.lg,
    ].join(' '),
    ghost: [
      'rounded-lg border',
      colors.border.subtle,
      colors.surface.raisedOverlay,
      spacing.padding.md,
    ].join(' '),
  },
  
  // Button styles
  button: {
    primary: [
      'rounded-lg',
      colors.brand.primary.bg,
      'px-6 py-3',
      typography.label.md,
      colors.text.onAccent,
      'transition-all',
      'hover:bg-[var(--color-primary-light)]',
      'hover:bg-[var(--color-primary-light)]',
      'disabled:opacity-60',
    ].join(' '),
    secondary: [
      'rounded-lg border-2',
      colors.border.subtle,
      colors.surface.raised,
      'px-6 py-3',
      typography.label.md,
      colors.text.secondary,
      'transition-all',
      'hover:border-[var(--color-border-strong)]',
      'hover:border-slate-500',
      'hover:bg-[rgba(var(--color-surface-rgb),0.92)]',
      'hover:bg-slate-800',
      'disabled:opacity-60',
    ].join(' '),
    ghost: [
      'rounded-lg border',
      colors.border.subtle,
      colors.surface.raisedOverlay,
      'px-4 py-2',
      typography.body.md,
      colors.text.secondary,
      'transition',
      'hover:border-[var(--color-border-accent)]',
      'hover:border-[var(--color-primary-light)]',
      'hover:bg-[rgba(var(--color-surface-alt-rgb),0.75)]',
      'hover:bg-slate-800/70',
      'disabled:opacity-60',
    ].join(' '),
  },

  // Panel / container surfaces shared across artifact and inline components
  panel: {
    inline: [
      'rounded-xl border backdrop-blur-md shadow-xl transition-all',
      createAlphaVarClass('border', 'color-primary', 0.35, 'border-[rgba(var(--color-primary-rgb),0.28)]'),
      createAlphaVarClass('bg', 'color-surface', 0.78, 'bg-[rgba(var(--color-surface-rgb),0.78)]'),
      shadows.primary,
      spacing.padding.lg,
    ].join(' '),
    subtle: [
      'rounded-lg border backdrop-blur-sm transition-colors',
      colors.border.subtle,
      colors.surface.raisedOverlay,
      spacing.padding.md,
    ].join(' '),
    modal: [
      'rounded-2xl border-2 shadow-2xl backdrop-blur-lg transition-all',
      colors.border.accent,
      colors.surface.raised,
      shadows.elevated,
      spacing.padding.xl,
    ].join(' '),
  },

  // Input styles with theme-aware borders and focus states
  input: {
    base: [
      'w-full rounded-lg border px-4 py-3 transition-colors',
      createAlphaVarClass('border', 'color-primary', 0.28, 'border-[rgba(var(--color-primary-rgb),0.28)]'),
      createAlphaVarClass('bg', 'color-surface-alt', 0.5, 'bg-[rgba(var(--color-surface-alt-rgb),0.5)]'),
      colors.text.primary,
      'placeholder:text-[rgba(var(--color-text-muted-rgb,148,163,184),0.7)]',
      'focus:outline-none focus:ring-2',
      'focus:ring-[var(--color-primary-light)] focus:ring-cyan-300',
      'focus:border-[var(--color-primary)] focus:border-cyan-400',
    ].join(' '),
    error: [
      'border-[var(--color-error)] focus:ring-[var(--color-error)] focus:border-[var(--color-error)]',
      'focus:ring-red-400 focus:border-red-400',
    ].join(' '),
    disabled: 'opacity-50 cursor-not-allowed',
    withIcon: 'pr-12',
  },
  
  // Badge/chip styles
  badge: {
    primary: [
      'inline-flex items-center',
      spacing.gap.xs,
      'rounded-lg border-2',
      createAlphaVarClass('border', 'color-primary', 0.45, 'border-[rgba(var(--color-primary-rgb),0.5)]'),
      createAlphaVarClass('bg', 'color-primary', 0.12, 'bg-[rgba(var(--color-primary-rgb),0.1)]'),
      colors.brand.primary.text,
      'px-4 py-2',
      typography.label.md,
    ].join(' '),
    secondary: [
      'inline-flex items-center',
      spacing.gap.xs,
      'rounded-lg border-2',
      createAlphaVarClass('border', 'color-secondary', 0.45, 'border-[rgba(var(--color-secondary-rgb),0.5)]'),
      createAlphaVarClass('bg', 'color-secondary', 0.12, 'bg-[rgba(var(--color-secondary-rgb),0.1)]'),
      colors.brand.secondary.text,
      'px-4 py-2',
      typography.label.md,
    ].join(' '),
    success: [
      'inline-flex items-center',
      spacing.gap.xs,
      'rounded-lg border-2',
      createAlphaVarClass('border', 'color-success', 0.45, 'border-[rgba(var(--color-success-rgb),0.5)]'),
      createAlphaVarClass('bg', 'color-success', 0.12, 'bg-[rgba(var(--color-success-rgb),0.1)]'),
      colors.status.success.text,
      'px-4 py-2',
      typography.label.md,
    ].join(' '),
    warning: [
      'inline-flex items-center',
      spacing.gap.xs,
      'rounded-lg',
      colors.status.warning.bg,
      'px-4 py-2.5',
      typography.label.md,
      colors.text.onAccent,
      shadows.warning,
      'shadow-lg',
    ].join(' '),
    neutral: [
      'inline-flex items-center',
      spacing.gap.sm,
      'rounded-lg border-2',
      colors.border.subtle,
      colors.surface.raised,
      'px-5 py-3',
      typography.body.lg,
      colors.text.primary,
      'font-bold',
    ].join(' '),
  },
  
  // Section header
  sectionHeader: [
    'flex items-center',
    spacing.gap.sm,
    'rounded-lg',
    colors.surface.raised,
    'px-6 py-4',
    'border-l-4',
    colors.border.accent,
    colors.text.primary,
  ].join(' '),
  
  // Accordion item (closed)
  accordionClosed: [
    'overflow-hidden rounded-2xl border-3',
    colors.border.subtle,
    colors.surface.raisedOverlay,
    'transition-all',
    shadows.elevated,
  ].join(' '),
  
  // Accordion item (open)
  accordionOpen: [
    'overflow-hidden rounded-2xl border-3',
    colors.border.accent,
    colors.surface.raised,
    'shadow-2xl',
    shadows.primary,
    'transition-all',
  ].join(' '),
  
  // Icon container
  iconContainer: {
    primary: [
      'rounded-lg',
      createAlphaVarClass('bg', 'color-primary', 0.2, 'bg-[rgba(var(--color-primary-rgb),0.2)]'),
      'p-2.5 ring-2',
      createAlphaVarClass('ring', 'color-primary', 0.5, 'ring-[rgba(var(--color-primary-light-rgb),0.5)]'),
    ].join(' '),
    secondary: [
      'rounded-lg',
      createAlphaVarClass('bg', 'color-secondary', 0.2, 'bg-[rgba(var(--color-secondary-rgb),0.2)]'),
      'p-2.5 ring-2',
      createAlphaVarClass('ring', 'color-secondary', 0.5, 'ring-[rgba(var(--color-secondary-light-rgb),0.5)]'),
    ].join(' '),
    warning: [
      'rounded-lg',
      createAlphaVarClass('bg', 'color-warning', 0.2, 'bg-[rgba(var(--color-accent-rgb),0.2)]'),
      'p-2.5 ring-2',
      createAlphaVarClass('ring', 'color-warning', 0.45, 'ring-[rgba(var(--color-accent-light-rgb),0.5)]'),
    ].join(' '),
  },
};

/**
 * LAYOUT PATTERNS
 * Common layout structures
 */
export const layouts = {
  // Full-bleed artifact container
  artifactContainer: [
    'min-h-screen',
    spacing.section,
    'rounded-2xl',
    colors.surface.base,
    spacing.padding.xl,
  ].join(' '),
  
  // Two-column grid (modules + flowchart)
  twoColumnGrid: 'grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]',
  
  // Tool grid
  toolGrid: 'grid grid-cols-1 gap-3 lg:grid-cols-2',
};

/**
 * UTILITY HELPERS
 * Common utility class combinations
 */
export const utils = {
  // Text truncation
  truncate: 'truncate',
  lineClamp: (lines) => `line-clamp-${lines}`,
  
  // Transitions
  transition: {
    all: 'transition-all',
    colors: 'transition-colors',
  },
  
  // Hover effects
  hover: {
    lift: 'hover:shadow-xl hover:shadow-[var(--shadow-primary,0_12px_30px_rgba(6,182,212,0.22))] hover:[box-shadow:0_0_0_rgba(var(--color-primary-rgb),0.2)]',
    border: 'hover:border-[var(--color-primary-light)] hover:border-[var(--color-primary-light)]',
  },
};

/**
 * EXAMPLE USAGE:
 * 
 * import { typography, colors, components, spacing } from '../../../styles/artifactDesignSystem';
 * 
 * <div className={components.card.primary}>
 *   <h1 className={`${typography.display.xl} ${colors.text.primary}`}>Workflow Name</h1>
 *   <p className={`${typography.body.md} ${colors.text.secondary}`}>Description</p>
 *   <button className={components.button.primary}>Approve</button>
 * </div>
 */

// Export everything as a single default object for convenience
const designSystem = {
  fonts,
  colors,
  shadows,
  gradients,
  typography,
  spacing,
  components,
  layouts,
  utils,
};

export default designSystem;
