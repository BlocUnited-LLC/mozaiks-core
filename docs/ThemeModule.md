# Theme Module

## Overview
The Theme module controls the platform's visual styling and branding, ensuring a consistent and dynamic user interface based on configurable theme settings. It manages color schemes, typography, layout properties, and branding assets, allowing for runtime theme switching without requiring page reloads.

## Core Responsibilities
- Loading and parsing theme configuration
- Applying theme properties across the application
- Dynamic theme switching
- Branding asset management (logo, favicon)
- CSS variable management
- Fallback handling for missing configurations
- Typography and layout standardization
- Theme persistence via localStorage

## Dependencies

### Internal Dependencies
- **Orchestration**: Endpoint registration via `director.py`
- **Event Bus**: Publishing theme change events

### External Dependencies
- **React**: Context API for theme distribution
- **Tailwind CSS**: Utility-first CSS framework for styling
- **PostCSS**: CSS processing and variable handling

## API Reference

### Backend Endpoints

#### `GET /api/theme-config`
Returns the complete theme configuration including available themes, typography, layout, and branding.
- **Returns**: Complete theme configuration object

#### `POST /api/change-theme`
Changes the current theme for a user.
- **Parameters**:
  - `theme_name`: Name of theme to apply
  - Requires authentication
- **Returns**: Success message with new theme name

#### `GET /api/current-theme`
Gets the current theme for the authenticated user.
- **Parameters**:
  - Requires authentication
- **Returns**: Current theme name

### Frontend Methods

#### `useTheme()` Hook
Custom React hook to access theme state and functions.
- **Returns**:
  - `currentTheme` (string): Current theme ID
  - `themes` (object): Available themes
  - `typography` (object): Typography settings
  - `layout` (object): Layout settings
  - `branding` (object): Branding elements
  - `setTheme(themeId)` (function): Change theme
  - `toggleTheme()` (function): Cycle through available themes

#### ThemeProvider Methods

##### `applyTheme(themeId, themes)`
Applies a specific theme.
- **Parameters**:
  - `themeId` (string): Theme to apply
  - `themes` (object, optional): Themes object

##### `applyTypographyAndLayout(typography, layout)`
Applies typography and layout settings.
- **Parameters**:
  - `typography` (object): Typography settings
  - `layout` (object): Layout settings

##### `applyBranding(branding)`
Applies branding elements (logo, favicon).
- **Parameters**:
  - `branding` (object): Branding elements

## Configuration

### Theme Configuration
Located at `/backend/core/config/theme_config.json`.

Example structure:
```json
{
    "branding": {
        "logo_url": "/assets/logo.png",
        "favicon_url": "/assets/favicon.ico",
        "app_name": "Mozaiks"
    },
    "available_themes": [
        {
            "name": "Day Light",
            "id": "light",
            "colors": {
                "primary": "#FFFFFF",
                "secondary": "#F5F7FA",
                "accent": "#0047AB",
                "background": "#FFFFFF",
                "text_primary": "#000000",
                "text_secondary": "#555555"
            }
        },
        {
            "name": "Midnight Dark",
            "id": "dark",
            "colors": {
                "primary": "#1E1E1E",
                "secondary": "#252526",
                "accent": "#007ACC",
                "background": "#1E1E1E",
                "text_primary": "#FFFFFF",
                "text_secondary": "#A1A1A1"
            }
        }
    ],
    "default_theme": "light",
    "typography": {
        "font_family": "Inter, sans-serif",
        "base_font_size": "16px",
        "heading_weight": "600",
        "body_weight": "400"
    },
    "layout": {
        "border_radius": "8px",
        "spacing_unit": "10px",
        "max_width": "1200px",
        "container_padding": "20px"
    }
}
```

### CSS Variables
These are configured in `index.css` and applied dynamically by the ThemeProvider:

```css
:root {
    --primary: #FFFFFF;
    --secondary: #F5F7FA;
    --accent: #0047AB;
    --background: #FFFFFF;
    --text_primary: #000000;
    --text_secondary: #555555;

    --font-family: 'Inter', sans-serif;
    --base-font-size: 16px;
    --heading-weight: 600;
    --body-weight: 400;

    --border-radius: 8px;
    --spacing-unit: 10px;
    --max-width: 1200px;
    --container-padding: 20px;
}
```

### Tailwind Configuration
Tailwind is configured to use the CSS variables in `tailwind.config.js`:

```js
module.exports = {
  theme: {
    extend: {
      colors: {
        primary: 'var(--primary)',
        secondary: 'var(--secondary)',
        accent: 'var(--accent)',
        background: 'var(--background)',
        'text-primary': 'var(--text_primary)',
        'text-secondary': 'var(--text_secondary)',
      },
      borderRadius: {
        DEFAULT: 'var(--border-radius)',
      },
      spacing: {
        unit: 'var(--spacing-unit)',
        container: 'var(--container-padding)',
      },
      fontFamily: {
        sans: 'var(--font-family)',
      },
      fontSize: {
        base: 'var(--base-font-size)',
      },
    },
  },
}
```

## Data Models

### Theme Object
```typescript
interface Theme {
  name: string;          // Display name of the theme
  id: string;            // Unique identifier 
  colors: {
    primary: string;     // CSS color value
    secondary: string;   // CSS color value
    accent: string;      // CSS color value
    background: string;  // CSS color value
    text_primary: string;// CSS color value
    text_secondary: string; // CSS color value
  }
}
```

### Typography Object
```typescript
interface Typography {
  font_family: string;   // Font stack
  base_font_size: string;// CSS size value
  heading_weight: string;// Font weight value
  body_weight: string;   // Font weight value
}
```

### Layout Object
```typescript
interface Layout {
  border_radius: string; // CSS size value
  spacing_unit: string;  // CSS size value
  max_width: string;     // CSS size value
  container_padding: string; // CSS size value
}
```

### Branding Object
```typescript
interface Branding {
  logo_url: string;      // Path to logo image
  favicon_url: string;   // Path to favicon
  app_name: string;      // Application name
}
```

## Integration Points

### Component Usage
React components should use the `useTheme` hook to access theme data:

```jsx
import { useTheme } from '../core/theme/useTheme';

const MyComponent = () => {
  const { currentTheme, branding } = useTheme();
  
  return (
    <div className="bg-primary text-text-primary p-4 rounded">
      <h2>Current theme: {currentTheme}</h2>
      <p>App name: {branding.app_name}</p>
    </div>
  );
};
```

### Theme Toggle Integration
Add theme toggle functionality to components:

```jsx
import { useTheme } from '../core/theme/useTheme';

const ThemeToggle = () => {
  const { currentTheme, toggleTheme } = useTheme();
  
  return (
    <button onClick={toggleTheme} className="p-2 rounded-full bg-accent text-white">
      {currentTheme === 'dark' ? '☀️' : '🌙'}
    </button>
  );
};
```

### CSS Class Structure
Use Tailwind's utility classes with the theme variables:

```jsx
<div className="bg-primary text-text-primary p-4 border border-secondary rounded">
  <h2 className="text-accent font-bold">Themed Component</h2>
</div>
```

## Events

### Events Published
- `theme_changed`: When a theme is changed

### Events Subscribed To
- None directly

## Adding a New Theme

1. **Update Theme Config**:
   Add a new theme to the `available_themes` array in `theme_config.json`:

   ```json
   {
     "name": "Ocean Blue",
     "id": "ocean",
     "colors": {
       "primary": "#EBF8FF",
       "secondary": "#C3DAFE",
       "accent": "#3182CE",
       "background": "#F7FAFC",
       "text_primary": "#2D3748",
       "text_secondary": "#4A5568"
     }
   }
   ```

2. **Update Frontend Components** (if needed):
   If the theme requires specific handling, update the `ThemeProvider.jsx` to handle it.

3. **Test Across Components**:
   Ensure the theme works well with all UI components.

## Common Issues & Troubleshooting

### Theme Not Applying
- Check that CSS variables are being set correctly in the document root
- Verify the theme ID exists in the configuration
- Check localStorage for saved theme preference
- Inspect browser console for errors

### Missing Assets
- Verify logo and favicon paths are correct
- Check that assets exist in the public directory
- Look for error logs related to missing assets

### CSS Variables Not Working
- Ensure PostCSS is configured correctly
- Check that Tailwind is using the variables correctly
- Verify browser support for CSS variables

### Theme Persistence Issues
- Clear localStorage if theme switching is not working
- Check that theme is being saved correctly
- Verify theme loading logic in the ThemeProvider

## Advanced Customization

### Custom Theme Components
Create theme-specific components by checking the current theme:

```jsx
const ThemedButton = () => {
  const { currentTheme } = useTheme();
  
  // Different styles based on theme
  const buttonClass = currentTheme === 'dark' 
    ? 'bg-blue-600 hover:bg-blue-700 text-white' 
    : 'bg-blue-100 hover:bg-blue-200 text-blue-800';
  
  return <button className={`px-4 py-2 rounded ${buttonClass}`}>Button</button>;
};
```

### Dynamic Color Generation
Generate complementary colors based on theme:

```jsx
const generateAccentHover = (accent) => {
  // Logic to darken/lighten the accent color
  return darkenColor(accent, 0.1);
};
```

## Related Files
- `/backend/core/config/theme_config.json`
- `/backend/core/director.py` (theme endpoints)
- `/src/core/theme/ThemeProvider.jsx`
- `/src/core/theme/useTheme.js`
- `/src/index.css`
- `/tailwind.config.js`
- `/postcss.config.js`
- `/public/assets/` (logo and favicon files)