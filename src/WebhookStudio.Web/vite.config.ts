import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
export default defineConfig({
  plugins: [react()],
  build: { outDir: '../WebhookStudio.Api/wwwroot', emptyOutDir: true },
  server: { proxy: { '/api': 'http://localhost:5080', '/hooks': 'http://localhost:5080', '/hubs': { target: 'http://localhost:5080', ws: true } } },
  test: { include: ['src/**/*.test.tsx'], environment: 'jsdom', setupFiles: './src/test/setup.ts', css: true }
});
