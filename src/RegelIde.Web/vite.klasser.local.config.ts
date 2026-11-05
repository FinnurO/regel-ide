// Midlertidig, ikke committet (#442 kaldsjekk): 5373 er opptatt av en annen agent, og API-ets CORS tillater bare 5173/5373.
// Proxyen gjør kallene same-origin, så CORS ikke trengs.
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
export default defineConfig({
  base: './',
  plugins: [react()],
  server: { port: 5473, strictPort: true, proxy: { '/api': 'http://localhost:5387' } },
})
