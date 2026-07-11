/** @type {import('tailwindcss').Config} */
module.exports = {
  darkMode: 'class',
  content: [
    "./Components/**/*.{razor,html,cshtml}",
    "./Pages/**/*.{razor,html,cshtml}",
    "../InvoiceSoftware.Client/**/*.{razor,html,cshtml}",
  ],
  theme: {
    extend: {
      colors: {
        // Financial Premium accent — deep teal
        primary: {
          50: '#e7f2f3',
          100: '#c9e4e6',
          200: '#9fd0d4',
          300: '#66b3ba',
          400: '#33969f',
          500: '#147d87',
          600: '#0e5c66',
          700: '#0c4a53',
          800: '#0b3c44',
          900: '#0a3238',
        },
        // Design tokens (CSS-var backed) so they can be used as utilities
        line: { DEFAULT: 'var(--color-border)', subtle: 'var(--color-border-subtle)' },
        surface: { DEFAULT: 'var(--color-surface)', sunken: 'var(--color-surface-sunken)' },
        ink: 'var(--color-text-primary)',
        success: 'var(--color-success)',
        danger: 'var(--color-danger)',
        warning: 'var(--color-warning)',
        info: 'var(--color-info)',
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
        display: ['"Sora"', 'Inter', 'system-ui', 'sans-serif'],
        mono: ['"IBM Plex Mono"', 'ui-monospace', 'SFMono-Regular', 'Menlo', 'monospace'],
      },
    },
  },
  plugins: [],
}
