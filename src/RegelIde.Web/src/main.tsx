import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import '@digdir/designsystemet-css';
import '@digdir/designsystemet-theme/digdir.css';
import './index.css';
import App from './App.tsx';
import { BrukerProvider } from './bruker/BrukerContext.tsx';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <BrukerProvider>
        <App />
      </BrukerProvider>
    </BrowserRouter>
  </StrictMode>,
);
