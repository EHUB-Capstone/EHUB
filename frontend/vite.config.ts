import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // proxy API requests to backend
    proxy: {
      '/api': {
        target: 'http://localhost:5226',
        changeOrigin: true,
      }
    }
  }
})
