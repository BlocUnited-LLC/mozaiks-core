// vite.config.js

import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { resolve } from 'path';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src'),
      '@core': resolve(__dirname, 'src/core'),
      '@components': resolve(__dirname, 'src/components'),
      '@plugins': resolve(__dirname, 'src/plugins'),
    }
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:8000', // Adjust to your FastAPI port
        changeOrigin: true,
      }
    }
  }
});