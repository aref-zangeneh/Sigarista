import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  base: '/admin/',
  plugins: [
    react({
      jsxImportSource: '@persian-jsx',
    }),
  ],
  resolve: {
    alias: {
      '@persian-jsx/jsx-runtime': fileURLToPath(
        new URL('./src/jsx/jsx-runtime.js', import.meta.url),
      ),
      '@persian-jsx/jsx-dev-runtime': fileURLToPath(
        new URL('./src/jsx/jsx-dev-runtime.js', import.meta.url),
      ),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5282',
        changeOrigin: true,
      },
    },
  },
})
