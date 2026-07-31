import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router';
import '@digdir/designsystemet-css';
import '@digdir/designsystemet-theme/digdir.css';
import './index.css';
import App from './App.tsx';
import { BrukerProvider } from './bruker/BrukerContext.tsx';
import { KonfigurasjonProvider } from './konfigurasjon/KonfigurasjonContext.tsx';

/**
 * Sti-prefikset appen er servert under, lest fra <base href> som serveren setter ved kjøretid.
 * Uten dette ville klientruteren trodd den står på rot, og alle lenker ville pekt utenfor appen
 * når vi kjører under /{org}/{app}/ i Altinns app-cluster. Tom streng lokalt.
 */
const stiprefiks = new URL(document.baseURI).pathname.replace(/\/$/, '');

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter basename={stiprefiks}>
      <KonfigurasjonProvider>
        <BrukerProvider>
          <App />
        </BrukerProvider>
      </KonfigurasjonProvider>
    </BrowserRouter>
  </StrictMode>,
);
