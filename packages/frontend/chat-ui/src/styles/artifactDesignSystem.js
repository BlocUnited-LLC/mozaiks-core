// Minimal design-system surface for @mozaiks/chat-ui.
//
// This is intentionally small: it provides only the tokens used by the exported
// chat-ui components, while allowing hosts to control look/feel via CSS vars.

export const colors = {
  brand: {
    primaryLight: {
      text: 'text-[var(--color-primary-light)]',
    },
  },
  text: {
    primary: 'text-[var(--color-text-primary,rgba(255,255,255,0.92))]',
    secondary: 'text-[var(--color-text-secondary,rgba(203,213,225,0.9))]',
    muted: 'text-[var(--color-text-muted,rgba(148,163,184,0.85))]',
  },
};

export const typography = {
  heading: {
    h2: 'text-2xl font-bold',
  },
  body: {
    lg: 'text-base',
    sm: 'text-xs',
    xs: 'text-[11px]',
  },
  label: {
    md: 'text-xs font-semibold uppercase tracking-wide',
    sm: 'text-[11px] font-semibold uppercase tracking-wider',
  },
};

export const components = {
  panel: {
    artifact:
      'rounded-2xl border border-[rgba(var(--color-primary-light-rgb),0.25)] bg-[rgba(4,8,18,0.75)] backdrop-blur-xl shadow-[0_20px_60px_rgba(2,6,23,0.6)] p-6',
  },
  button: {
    primary:
      'rounded-xl bg-gradient-to-r from-[var(--color-primary)] to-[var(--color-secondary)] text-white font-semibold',
  },
};
