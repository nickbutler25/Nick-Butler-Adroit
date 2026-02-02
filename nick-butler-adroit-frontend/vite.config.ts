import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: 'build',
  },
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'https://localhost:7055',
        secure: false,
        changeOrigin: true,
      },
      '/hubs': {
        target: 'wss://localhost:7055',
        secure: false,
        ws: true,
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: './src/setupTests.ts',
    globals: true,
    exclude: ['e2e/**', 'node_modules/**'],
  },
});
