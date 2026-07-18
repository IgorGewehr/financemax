import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5173,
    strictPort: false,
    // Proxy de dev: `lib/api/client.ts` fala `/api/...` RELATIVO por padrão (mesma convenção que
    // produção usa via túnel — ver comentário lá e `.env.example`); em dev, quem resolve esse
    // caminho pro `Financemax.Api` local é este proxy do Vite. Ajuste a porta se o servidor local
    // não estiver em :8090 (ou defina `VITE_API_BASE_URL` pra pular o proxy inteiramente).
    proxy: {
      '/api': {
        target: 'http://localhost:8090',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
    target: 'es2022',
  },
});
