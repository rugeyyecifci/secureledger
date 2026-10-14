import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // "/api" ile başlayan istekleri API'ye ilet (API: http://localhost:5059).
    // Böylece tarayıcı tek bir adresle konuşur, API'de ek ayar gerekmez.
    proxy: {
      '/api': 'http://localhost:5059',
    },
  },
})
