import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
// Port 5173 is the origin allowed by the API (Cors:AllowedOrigins in appsettings.json).
export default defineConfig({
  plugins: [react()],
  server: { port: 5173, strictPort: true },
})
