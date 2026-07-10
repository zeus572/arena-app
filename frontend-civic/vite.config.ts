/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5175,
    strictPort: true,
  },
  test: {
    // Playwright owns e2e/*.spec.ts (run via npm run test:e2e); keep Vitest
    // to the unit tests so `npm test` doesn't trip over Playwright's runner.
    include: ['src/**/*.test.{ts,tsx}'],
  },
})
