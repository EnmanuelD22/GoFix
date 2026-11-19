import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'GOFIX_');
  const apiTarget = new URL(env.GOFIX_API_TARGET ?? 'https://localhost:7043');

  if (apiTarget.protocol !== 'https:') {
    throw new Error('GOFIX_API_TARGET debe usar HTTPS.');
  }

  return {
    plugins: [react()],
    server: {
      port: 5173,
      strictPort: true,
      proxy: {
        // El navegador conserva el mismo origen para peticiones y cookies.
        '/api': { target: apiTarget.origin, changeOrigin: true },
      },
    },
  };
});
