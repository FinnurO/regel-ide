import { NavLink, Route, Routes } from 'react-router-dom';
import { Field, Label, Select } from '@digdir/designsystemet-react';
import { useBruker } from './bruker/BrukerContext';
import RettskilderListe from './pages/RettskilderListe';
import RettskildeDetalj from './pages/RettskildeDetalj';
import Importer from './pages/Importer';
import HandbokOpprett from './pages/HandbokOpprett';
import TjenesterListe from './pages/TjenesterListe';
import TjenesteDetalj from './pages/TjenesteDetalj';
import BegreperListe from './pages/BegreperListe';
import BegrepDetalj from './pages/BegrepDetalj';
import KodelisterListe from './pages/KodelisterListe';
import KodelisteDetalj from './pages/KodelisteDetalj';

function BrukerVelger() {
  const { brukere, gjeldendeBruker, velgBruker, laster } = useBruker();

  if (laster) return null;

  return (
    <div className="bruker-velger">
      <Field>
        <Label data-size="sm">Innlogget som (testbruker)</Label>
        <Select
          value={gjeldendeBruker?.id ?? ''}
          onChange={(e) => velgBruker(e.target.value || null)}
        >
          {brukere.map((b) => (
            <Select.Option key={b.id} value={b.id}>
              {b.navn} ({b.rolle}) — {b.virksomhetNavn}
            </Select.Option>
          ))}
        </Select>
      </Field>
    </div>
  );
}

function App() {
  return (
    <div className="layout">
      <aside className="sidebar">
        <h1>Regel-IDE</h1>
        <nav>
          <NavLink to="/" end className={({ isActive }) => (isActive ? 'aktiv' : '')}>
            Rettskilder
          </NavLink>
          <NavLink to="/importer" className={({ isActive }) => (isActive ? 'aktiv' : '')}>
            Importer
          </NavLink>
          <NavLink to="/handboker/ny" className={({ isActive }) => (isActive ? 'aktiv' : '')}>
            Ny håndbok
          </NavLink>
          <NavLink to="/tjenester" className={({ isActive }) => (isActive ? 'aktiv' : '')}>
            Tjenester
          </NavLink>
          <NavLink to="/begreper" className={({ isActive }) => (isActive ? 'aktiv' : '')}>
            Begreper
          </NavLink>
          <NavLink to="/kodelister" className={({ isActive }) => (isActive ? 'aktiv' : '')}>
            Kodelister
          </NavLink>
        </nav>
        <BrukerVelger />
      </aside>
      <main className="innhold">
        <Routes>
          <Route path="/" element={<RettskilderListe />} />
          <Route path="/rettskilder/:id" element={<RettskildeDetalj />} />
          <Route path="/importer" element={<Importer />} />
          <Route path="/handboker/ny" element={<HandbokOpprett />} />
          <Route path="/tjenester" element={<TjenesterListe />} />
          <Route path="/tjenester/:id" element={<TjenesteDetalj />} />
          <Route path="/begreper" element={<BegreperListe />} />
          <Route path="/begreper/:id" element={<BegrepDetalj />} />
          <Route path="/kodelister" element={<KodelisterListe />} />
          <Route path="/kodelister/:id" element={<KodelisteDetalj />} />
        </Routes>
      </main>
    </div>
  );
}

export default App;
