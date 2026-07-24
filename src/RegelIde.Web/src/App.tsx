import { NavLink, Route, Routes } from 'react-router-dom';
import { Field, Label, Select } from '@digdir/designsystemet-react';
import { useBruker } from './bruker/BrukerContext';
import RettskilderListe from './pages/RettskilderListe';
import RettskildeDetalj from './pages/RettskildeDetalj';
import Importer from './pages/Importer';

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
        </nav>
        <BrukerVelger />
      </aside>
      <main className="innhold">
        <Routes>
          <Route path="/" element={<RettskilderListe />} />
          <Route path="/rettskilder/:id" element={<RettskildeDetalj />} />
          <Route path="/importer" element={<Importer />} />
        </Routes>
      </main>
    </div>
  );
}

export default App;
