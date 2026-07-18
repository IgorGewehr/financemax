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
    // Sem proxy de dev: diferente do sistemax de origem (mesma origem do Kestrel embutido), o
    // financemax fala com uma base URL ABSOLUTA (`VITE_API_BASE_URL`, ver `.env.example` e
    // `lib/api/client.ts`) — o servidor mora numa VM remota atrás de Cloudflare Tunnel, não no
    // mesmo processo do Vite. CORS é responsabilidade do `Financemax.Api` (F2).
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
    target: 'es2022',
  },
});
