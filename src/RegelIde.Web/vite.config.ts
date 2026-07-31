import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  // Relative asset-URL-er. Altinns app-cluster serverer appen under /{org}/{app}/, og ingressen
  // stripper ikke prefikset. Med absolutte URL-er ville /assets/... peket utenfor appen og gitt
  // 404. Prefikset kan ikke bakes inn her, siden det ville låst imaget til én sti — det settes
  // i stedet som <base href> ved kjøretid av serveren, se Program.cs.
  base: './',
  plugins: [react()],
})
