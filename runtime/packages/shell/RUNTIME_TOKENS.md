# Runtime Tokens (ChatUI Layer)

Mozaiks Runtime exposes a small set of CSS custom properties that every generated page can rely on. These tokens keep the floating widget, chat surfaces, and artifacts stylistically consistent across apps while still allowing per-tenant overrides.

## How to Use These Tokens

- The defaults live in `src/index.css` under the `:root` selector.
- To customize a token for a specific app/app, set the variable on a scoped wrapper (e.g., `.app-dark { --color-primary: #ff7ae6; }`).
- Generators should reference these tokens instead of hardcoding colors, spacing, or shadows. This lets the runtime adjust platform-wide behavior without regenerating pages.

## Token Catalog

| Token | Default | Scope | Description |
| --- | --- | --- | --- |
| `--color-primary` | `#06b6d4` | global | Primary brand accent used across buttons, gradients, and glows. |
| `--color-primary-light` | `#67e8f9` | global | Lighter variant for borders, hover states, and icon fills. |
| `--color-secondary` | `#8b5cf6` | global | Secondary accent color for gradients and highlights. |
| `--color-secondary-light` | `#a78bfa` | global | Light secondary used for subtle borders and glows. |
| `--color-accent` | `#f59e0b` | global | Warm accent for warnings or actionable CTAs. |
| `--color-success` | `#10b981` | global | Success/confirmation color. |
| `--color-warning` | `#f59e0b` | global | Warning color shared with accent for consistency. |
| `--color-error` | `#ef4444` | global | Error/destructive color.
| `--color-background` | `#0b1220` | global | Default page background. |
| `--color-surface` | `#0f1724` | global | Card/background surface token for chat shells. |
| `--color-surface-alt` | `#131d33` | global | Alternate surface used on panels such as the artifact drawer. |
| `--color-border-subtle` | `#1e293b` | global | Default border for cards and dividers. |
| `--color-border-strong` | `#334155` | global | Emphasized border color for selected states. |
| `--color-text-primary` | `#e6eef8` | global | Main text color. |
| `--color-text-secondary` | `#94a3b8` | global | Secondary/placeholder text. |
| `--shadow-primary` | `0 20px 45px rgba(6, 182, 212, 0.24)` | global | Elevated glow used for interactive controls. |
| `--shadow-secondary` | `0 20px 45px rgba(139, 92, 246, 0.24)` | global | Secondary glow option. |
| `--shadow-elevated` | `0 24px 60px rgba(11, 18, 32, 0.55)` | global | Default card shadow. |
| `--chat-bubble-radius` | `18px` | global | Rounded corner radius for chat bubbles. |
| `--chat-bubble-padding-y` | `0.95rem` | global | Vertical padding applied to chat bubbles. |
| `--chat-bubble-padding-x` | `1.15rem` | global | Horizontal padding inside chat bubbles. |
| `--chat-bubble-line-height` | `1.45` | global | Line-height used for chat copy to ensure readability. |
| `--chat-bubble-shadow` | `0 18px 40px rgba(2, 6, 23, 0.35)` | global | Soft elevation for message cards. |
| `--chat-user-bg` | `linear-gradient(140deg, rgba(6, 182, 212, 0.35), rgba(14, 116, 144, 0.45))` | global | Background fill for user messages. |
| `--chat-agent-bg` | `linear-gradient(140deg, rgba(12, 21, 38, 0.9), rgba(23, 37, 84, 0.9))` | global | Background fill for agent messages. |
| `--chat-user-border` | `rgba(103, 232, 249, 0.45)` | global | Border color for user bubbles. |
| `--chat-agent-border` | `rgba(99, 102, 241, 0.35)` | global | Border color for agent bubbles. |
| `--chat-divider-color` | `rgba(148, 163, 184, 0.25)` | global | Subtle divider used on chat chrome/input seams. |
| `--chat-input-bg` | `rgba(6, 11, 25, 0.85)` | global | Background for the transmission input surface. |
| `--chat-input-border` | `rgba(148, 163, 184, 0.35)` | global | Border color for the textarea inside the transmission input. |
| `--chat-input-shadow` | `0 15px 45px rgba(2, 6, 23, 0.45)` | global | Drop shadow that separates the composer from the feed. |
| `--widget-bottom-offset` | `calc(env(safe-area-inset-bottom, 0px) + 2rem)` | global / override per-route | Controls the floating widget's distance from the bottom of the viewport. Applied via the `widget-safe-bottom` helper class. |

## Helper Classes Tied to Tokens

| Class | What it does | Dependent Tokens |
| --- | --- | --- |
| `.widget-safe-bottom` | Sets `bottom` to `var(--widget-bottom-offset)` for any fixed widget container. | `--widget-bottom-offset` |
| `.artifact-cta`, `.artifact-hover-glow`, etc. | Use the shadow/color tokens above to render consistent CTA buttons and hover glows. | `--color-primary(-light)`, `--shadow-primary`, etc. |

## Guidance for Generator Agents

1. **Use tokens, not literals** – reference the variables listed above instead of hardcoded hex values or pixel offsets whenever your UI mimics the runtime shell.
2. **Scope overrides intentionally** – when an app wants a different palette, set the variable on that app's root container. Do not edit runtime components.
3. **Extend via proposal** – if a new pattern repeatedly needs its own token (e.g., widget width, chat radius), add it to `src/index.css` and document it here so every agent can consume it safely.
4. **Widget positioning** – never set `bottom` on the persistent widget. Use `widget-safe-bottom` and adjust `--widget-bottom-offset` if more clearance is required.

Keeping these tokens centralized preserves multi-tenant isolation and lets the runtime evolve without re-generating every page.
