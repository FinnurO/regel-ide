import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router';
import '@digdir/designsystemet-css';
import '@digdir/designsystemet-theme/digdir.css';
import './index.css';
import App from './App.tsx';
import { BrukerProvider } from './bruker/BrukerContext.tsx';
import { KonfigurasjonProvider } from './konfigurasjon/KonfigurasjonContext.tsx';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <KonfigurasjonProvider>
        <BrukerProvider>
          <App />
        </BrukerProvider>
      </KonfigurasjonProvider>
    </BrowserRouter>
  </StrictMode>,
);
