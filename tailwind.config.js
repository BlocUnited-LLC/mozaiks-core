// tailwind.config.js

/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./index.html", // include root index.html
    "./src/**/*.{js,jsx,ts,tsx}",
  ],
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
  plugins: [],
}
