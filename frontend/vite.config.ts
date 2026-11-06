import { defineConfig } from 'vite';
const apiProxy = { '/api': 'http://localhost:5050' };
export default defineConfig({
  server: { port: 5173, strictPort: true, proxy: apiProxy },
  preview: { proxy: apiProxy },
});
