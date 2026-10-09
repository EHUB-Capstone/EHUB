import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    headers: {
      // Google Sign-In's popup returns the credential via window.postMessage.
      'Cross-Origin-Opener-Policy': 'same-origin-allow-popups',
    },
    // proxy API requests to backend
    proxy: {
      '/api': {
        target: 'http://localhost:5226',
        changeOrigin: true,
        ws: true,
      }
    }
  },
  preview: {
    headers: {
      'Cross-Origin-Opener-Policy': 'same-origin-allow-popups',
    },
  },
})
