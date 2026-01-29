# Declarative Shell Contract v1.0

> **Status**: LOCKED
> **Version**: 1.0.0
> **Last Updated**: 2025-01-28
> **Parties**: mozaiks-core (Shell) ↔ mozaiks-platform (Generators)

## Overview

The Declarative Shell Contract defines the interface between mozaiks-core's Shell (React frontend) and mozaiks-platform's generators. This contract enables Platform to generate navigation and branding configuration that the Shell renders without hardcoded routes.

## Key Principles

1. **Shell is Data-Driven**: Routes, navigation items, and branding come from JSON configuration
2. **Platform Generates, Shell Renders**: Platform generators produce configs, Shell consumes them
3. **Component Registry**: Named components are registered and referenced by string in configs
4. **Graceful Degradation**: Shell works with sensible defaults when configs are missing

## Configuration Files

### navigation.json

Location: `/navigation.json` (served from app's public directory)

```json
{
  "$schema": "https://mozaiks.io/schemas/navigation.v1.json",
  "version": "1.0.0",
  "routes": [
    {
      "path": "/",
      "component": "ChatPage",
      "exact": true,
      "meta": {
        "title": "Chat",
        "requiresAuth": false
      }
    },
    {
      "path": "/dashboard",
      "component": "DashboardPage",
      "meta": {
        "title": "Dashboard",
        "requiresAuth": true,
        "authRedirect": "/login"
      }
    },
    {
      "path": "/settings",
      "component": "SettingsPage",
      "meta": {
        "title": "Settings",
        "requiresAuth": true,
        "description": "Application settings"
      }
    }
  ],
  "sidebar": {
    "items": [
      {
        "id": "chat",
        "label": "Chat",
        "icon": "MessageSquare",
        "path": "/",
        "order": 1
      },
      {
        "id": "dashboard",
        "label": "Dashboard",
        "icon": "LayoutDashboard",
        "path": "/dashboard",
        "order": 2
      }
    ]
  },
  "topNav": {
    "items": []
  }
}
```

#### Route Schema

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `path` | string | Yes | Route path (React Router format, supports params like `:id`) |
| `component` | string | Yes | Component name (must be registered in component registry) |
| `exact` | boolean | No | Whether path must match exactly |
| `meta.title` | string | No | Page title (used in document title) |
| `meta.requiresAuth` | boolean | No | Whether route requires authentication |
| `meta.authRedirect` | string | No | Redirect path when auth required but not authenticated |
| `meta.description` | string | No | Page description |

#### Sidebar Item Schema

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | Yes | Unique identifier |
| `label` | string | Yes | Display text |
| `icon` | string | Yes | Lucide icon name |
| `path` | string | Yes | Navigation path |
| `order` | number | No | Sort order (lower = higher) |
| `badge` | string/number | No | Badge to display |
| `children` | array | No | Nested navigation items |

### branding.json

Location: `/branding.json` (served from app's public directory)

```json
{
  "$schema": "https://mozaiks.io/schemas/branding.v1.json",
  "version": "1.0.0",
  "app": {
    "name": "MyApp",
    "tagline": "AI-Powered Workflows",
    "logo": "/logo.svg",
    "favicon": "/favicon.ico"
  },
  "theme": {
    "mode": "system",
    "colors": {
      "primary": "#3B82F6",
      "secondary": "#6366F1",
      "accent": "#8B5CF6"
    }
  },
  "layout": {
    "sidebar": {
      "position": "left",
      "collapsible": true,
      "defaultCollapsed": false
    },
    "header": {
      "visible": true,
      "sticky": true
    }
  }
}
```

#### App Schema

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | Application name |
| `tagline` | string | No | Application tagline |
| `logo` | string | No | Path to logo image |
| `favicon` | string | No | Path to favicon |

#### Theme Schema

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `mode` | string | No | `"light"`, `"dark"`, or `"system"` |
| `colors.primary` | string | No | Primary brand color (hex) |
| `colors.secondary` | string | No | Secondary brand color (hex) |
| `colors.accent` | string | No | Accent color (hex) |

## Component Registry

Components are registered in the Shell's component registry before being referenced in navigation.json.

### Registration

```javascript
import { registerComponent } from '@mozaiks/shell/registry';
import DashboardPage from './pages/DashboardPage';

registerComponent('DashboardPage', DashboardPage, {
  core: false,
  description: 'Main dashboard page'
});
```

### Core Components

The following components are registered by default:

| Component | Description |
|-----------|-------------|
| `ChatPage` | Main chat interface |
| `MyWorkflowsPage` | User workflows listing |
| `ArtifactPage` | Artifact viewer |

### Plugin Components

Platform generators can register additional components for plugin pages:

```javascript
import { registerComponent } from '@mozaiks/shell/registry';
import PluginPageContainer from '@mozaiks/shell/components/PluginPageContainer';

// Generic container for plugin pages
registerComponent('PluginPage', PluginPageContainer);

// Feature-specific pages
registerComponent('BillingPage', BillingPage);
registerComponent('AnalyticsPage', AnalyticsPage);
```

## React Context Providers

### useNavigation Hook

Access navigation configuration in components:

```javascript
import { useNavigation } from '@mozaiks/shell/providers';

function Sidebar() {
  const { getSidebarItems, loading } = useNavigation();

  if (loading) return <Loading />;

  return (
    <nav>
      {getSidebarItems().map(item => (
        <NavItem key={item.id} {...item} />
      ))}
    </nav>
  );
}
```

### useBranding Hook

Access branding configuration in components:

```javascript
import { useBranding } from '@mozaiks/shell/providers';

function Header() {
  const { appName, logo, theme } = useBranding();

  return (
    <header>
      {logo && <img src={logo} alt={appName} />}
      <h1>{appName}</h1>
    </header>
  );
}
```

## Icon Library

Icons are referenced by name using the [Lucide](https://lucide.dev/) icon library.

Example usage in navigation.json:
```json
{
  "icon": "MessageSquare"
}
```

Common icons:
- `MessageSquare` - Chat/messages
- `LayoutDashboard` - Dashboard
- `Settings` - Settings
- `Users` - Users/accounts
- `CreditCard` - Billing
- `BarChart` - Analytics
- `Workflow` - Workflows

## Versioning

Both configuration files include a `version` field for future compatibility:

```json
{
  "version": "1.0.0"
}
```

The Shell will validate version compatibility and provide migration guidance when versions differ.

## Error Handling

### Missing Configuration

When configuration files are not found (404), the Shell uses sensible defaults:
- Default route to `ChatPage` at `/`
- Default app name: "Mozaiks"
- System theme mode

### Invalid Configuration

When configuration fails validation:
- Error logged to console
- Fallback to defaults
- Warning displayed in development mode

### Unregistered Components

When a route references an unregistered component:
- Error boundary catches the issue
- "Component Not Registered" message displayed
- Developer guidance shown in development mode

## Migration Path

### V1 → V2 (Planned)

Future versions may support:
- Module federation for lazy-loaded plugin components
- Dynamic configuration reloading
- Remote configuration sources
- A/B testing support

## Files Created

| File | Purpose |
|------|---------|
| `src/registry/componentRegistry.js` | Component registration API |
| `src/registry/coreComponents.js` | Core component registration |
| `src/providers/NavigationProvider.jsx` | Navigation context provider |
| `src/providers/BrandingProvider.jsx` | Branding context provider |
| `src/components/RouteRenderer.jsx` | Dynamic route rendering |
| `src/components/PluginPageContainer.jsx` | Plugin page wrapper |
| `public/navigation.json` | Default navigation config |
| `public/branding.json` | Default branding config |

## Changelog

### v1.0.0 (2025-01-28)
- Initial contract definition
- Core component registry
- Navigation and branding providers
- RouteRenderer with auth support
- PluginPageContainer for consistent styling
