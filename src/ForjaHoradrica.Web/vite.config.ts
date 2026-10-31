import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({ mode }) => {
  const environment = loadEnv(mode, '.', 'AKARA_')
  const apiOrigin = new URL(environment.AKARA_API_ORIGIN || 'http://127.0.0.1:5273').origin
  return {
  plugins: [react()],
  server: { port: 5173, strictPort: true, proxy: { '/api': {
    target: apiOrigin, changeOrigin: true,
    headers: { Origin: apiOrigin },
  } } },
  build: { outDir: 'dist', emptyOutDir: true },
  }
})
