import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../Calendar.Api/wwwroot',
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/api': {
        target: 'https://localhost:7143',
        changeOrigin: true,
        secure: false,
      },
      '/health': {
        target: 'https://localhost:7143',
        changeOrigin: true,
        secure: false,
      },
      '/openapi': {
        target: 'https://localhost:7143',
        changeOrigin: true,
        secure: false,
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
    css: true,
  },
})
