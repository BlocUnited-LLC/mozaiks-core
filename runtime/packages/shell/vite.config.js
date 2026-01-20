// vite.config.js

import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import { resolve } from 'path';

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  
  // Frontend plugin path - falls back to src/plugins_stub if not set
  const frontendPluginsPath = env.MOZAIKS_FRONTEND_PLUGINS_PATH
    ? resolve(env.MOZAIKS_FRONTEND_PLUGINS_PATH)
    : resolve(__dirname, 'src/plugins_stub');
    
  // Frontend workflows path - falls back to src/workflows_stub if not set  
  const frontendWorkflowsPath = env.MOZAIKS_FRONTEND_WORKFLOWS_PATH
    ? resolve(env.MOZAIKS_FRONTEND_WORKFLOWS_PATH)
    : resolve(__dirname, 'src/workflows_stub');

  return {
    plugins: [react()],
    resolve: {
      alias: {
        '@': resolve(__dirname, 'src'),
        '@core': resolve(__dirname, 'src/core'),
        '@components': resolve(__dirname, 'src/components'),
        '@plugins': frontendPluginsPath,
        '@chat-workflows': frontendWorkflowsPath,
      }
    },
    server: {
      proxy: {
        '/api': {
          target: 'http://localhost:8080', // FastAPI default port (Azure Container Apps)
          changeOrigin: true,
        }
      },
      fs: {
        allow: [
          frontendPluginsPath,
          frontendWorkflowsPath,
          resolve(__dirname, 'src')
        ]
      }
    },
    build: {
      chunkSizeWarningLimit: 600, // Slightly above our main chunk
      rollupOptions: {
        output: {
          manualChunks: {
            // Split vendor chunks for better caching
            'vendor-react': ['react', 'react-dom', 'react-router-dom'],
            'vendor-monaco': ['@monaco-editor/react'],
          }
        }
      }
    }
  };
});
