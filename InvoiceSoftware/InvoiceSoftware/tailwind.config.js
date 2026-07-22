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
        // ---- Washi neutrals -------------------------------------------------
        // The app styles itself with ~3.6k hard-coded gray-*/slate-* utilities
        // rather than the design tokens, so the brand is rolled out by
        // replacing those two scales outright: every existing utility picks up
        // the warm paper/ink tones with no markup changes. Derived from the
        // landing page's OKLCH ramp (hue ~55-83, very low chroma) so the app
        // and the marketing site share one palette. Both scales are identical
        // on purpose — `gray` and `slate` were used interchangeably.
        gray: {
          50: '#fdfbf7', 100: '#f8f5f0', 200: '#f1ece5', 300: '#ded8d0',
          400: '#bcb4ac', 500: '#988f87', 600: '#776e68', 700: '#544d48',
          800: '#37302c', 900: '#251f1b', 950: '#120d0a',
        },
        slate: {
          50: '#fdfbf7', 100: '#f8f5f0', 200: '#f1ece5', 300: '#ded8d0',
          400: '#bcb4ac', 500: '#988f87', 600: '#776e68', 700: '#544d48',
          800: '#37302c', 900: '#251f1b', 950: '#120d0a',
        },
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
        sans: ['"Zen Kaku Gothic New"', 'system-ui', 'sans-serif'],
        display: ['"Shippori Mincho"', 'Georgia', 'serif'],
        mono: ['"IBM Plex Mono"', 'ui-monospace', 'SFMono-Regular', 'Menlo', 'monospace'],
      },
    },
  },
  plugins: [],
}
