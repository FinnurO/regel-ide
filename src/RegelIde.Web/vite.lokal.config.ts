// Midlertidig, ikke committet (kaldsjekk #385): web på 5373 med proxy til API på 5387.
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
export default defineConfig({ base: './', plugins: [react()], server: { port: 5373, strictPort: true, proxy: { '/api': 'http://localhost:5387', '/helse': 'http://localhost:5387' } } })
