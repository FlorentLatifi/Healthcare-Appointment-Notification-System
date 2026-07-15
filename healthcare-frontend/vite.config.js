import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// Proxy /api → local Kestrel so the browser stays same-origin (http://localhost:5173).
// That allows the httpOnly refresh cookie (Path=/api/v1) to be stored and sent.
// Point VITE_API_BASE_URL at /api/v1 (relative) in .env for local dev.
export default defineConfig({
  plugins: [tailwindcss(), react()],
  server: {
    watch: {
      usePolling: true,
    },
    hmr: {
      overlay: true,
    },
    proxy: {
      '/api': {
        // Prefer HTTP profile so Secure cookies are not forced on http://localhost:5173
        target: process.env.VITE_API_PROXY_TARGET || 'http://localhost:5171',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
