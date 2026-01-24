/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./src/**/*.{js,jsx,ts,tsx}"],
  theme: {
    extend: {
      fontFamily: {
        // Global body font
        sans: ["Rajdhani", "ui-sans-serif", "system-ui", "-apple-system", "Segoe UI", "Roboto", "Ubuntu", "Cantarell", "Noto Sans", "Helvetica Neue", "Arial", "sans-serif"],
        // Headings font
        heading: ["Orbitron", "Rajdhani", "ui-sans-serif", "system-ui", "sans-serif"],
        // Logo font
        logo: ["Fagrak Inline", "Rajdhani", "ui-sans-serif", "system-ui", "sans-serif"],
      },
      container: {
        center: true,
      },
      screens: {
        sm: "480px",
        md: "768px",
        lg: "976px",
        xl: "1440px",
      },
  colors: {
        purple: "#7e5bef",
        pink: "#ff49db",
        orange: "#ff7849",
        green: "#13ce66",
        yellow: "#ffc82c",
        white: "#ffffff",
  // Streamlined palette

        // Mozaiks Website theme palette
        primary: "#6d28d9",
        secondary: "#06b6d4",
        accent: "#f59e0b",
        dark: "#030712",
        light: "#f8fafc",
        card: "#0f172a",
        border: "#1e293b",
      },
      height: {
        "600x": "600px",
        "509x": "509px",
      },
      fontSize: {
        "8x": "8px",
      },
    },
  },
  plugins: [require("@tailwindcss/forms")],
};
