// /src/core/theme/ThemeProvider.jsx

import React, { createContext, useEffect, useState } from 'react';

export const ThemeContext = createContext();

export function ThemeProvider({ children }) {
  const [themeData, setThemeData] = useState({
    currentTheme: 'light',
    themes: {},
    typography: {},
    layout: {},
    branding: {},
  });

  useEffect(() => {
    loadThemes();
  }, []);

  const loadThemes = async () => {
    try {
      const response = await fetch("/api/theme-config");
      if (!response.ok) throw new Error("Failed to fetch theme config");
      
      const data = await response.json();

      // Process theme data
      const themes = {};
      data.available_themes.forEach(theme => {
        themes[theme.id] = theme.colors;
      });

      const typography = data.typography || {
        font_family: "Inter, sans-serif",
        base_font_size: "16px",
        heading_weight: "600",
        body_weight: "400"
      };

      const layout = data.layout || {
        border_radius: "8px",
        spacing_unit: "10px",
        max_width: "1200px",
        container_padding: "20px"
      };

      // Set theme data
      setThemeData({
        currentTheme: data.default_theme,
        themes,
        typography,
        layout,
        branding: data.branding || {}
      });

      // Apply theme
      applyTheme(data.default_theme, themes);
      applyTypographyAndLayout(typography, layout);
      applyBranding(data.branding || {});
    } catch (error) {
      console.error("⚠️ Theme config could not be loaded. Using fallback settings.", error);
      setFallbackSettings();
    }
  };

  const applyTheme = (themeId, themes = themeData.themes) => {
    if (!themes[themeId]) {
      console.warn(`⚠️ Theme '${themeId}' not found. Falling back to 'light'.`);
      themeId = "light";
    }

    const theme = themes[themeId];
    localStorage.setItem("mozaiks_theme", themeId);

    // Apply theme colors dynamically (only if they exist)
    if (theme) {
      Object.entries(theme).forEach(([key, value]) => {
        if (value) {
          document.documentElement.style.setProperty(`--${key}`, value);
        }
      });
    }

    setThemeData(prev => ({ ...prev, currentTheme: themeId }));
  };

  const applyTypographyAndLayout = (typography = themeData.typography, layout = themeData.layout) => {
    Object.entries(typography).forEach(([key, value]) => {
      document.documentElement.style.setProperty(`--${key.replace('_', '-')}`, value);
    });

    Object.entries(layout).forEach(([key, value]) => {
      document.documentElement.style.setProperty(`--${key.replace('_', '-')}`, value);
    });
  };

  const applyBranding = (branding = themeData.branding) => {
    if (branding.logo_url) {
      const logoElement = document.getElementById("app-logo");
      if (logoElement) logoElement.src = branding.logo_url;
    }

    if (branding.favicon_url) {
      const faviconElement = document.querySelector("link[rel='icon']");
      if (faviconElement) faviconElement.href = branding.favicon_url;
    }
  };

  const setFallbackSettings = () => {
    console.warn("⚠️ Applying fallback theme due to missing configuration.");
    
    const typography = {
      font_family: "Arial, sans-serif",
      base_font_size: "14px",
      heading_weight: "600",
      body_weight: "400"
    };

    const layout = {
      border_radius: "5px",
      spacing_unit: "8px",
      max_width: "1100px",
      container_padding: "15px"
    };

    const themes = {
      light: {
        primary: "#DDDDDD",
        secondary: "#F5F5F5",
        accent: "#888888",
        background: "#FFFFFF",
        text_primary: "#333333",
        text_secondary: "#777777"
      }
    };

    setThemeData({
      currentTheme: 'light',
      themes,
      typography,
      layout,
      branding: {}
    });

    applyTheme('light', themes);
    applyTypographyAndLayout(typography, layout);
  };

  return (
    <ThemeContext.Provider 
      value={{ 
        ...themeData, 
        setTheme: (themeId) => applyTheme(themeId),
        toggleTheme: () => {
          const themeIds = Object.keys(themeData.themes);
          const currentIndex = themeIds.indexOf(themeData.currentTheme);
          const nextTheme = themeIds[(currentIndex + 1) % themeIds.length];
          applyTheme(nextTheme);
        }
      }}
    >
      {children}
    </ThemeContext.Provider>
  );
}