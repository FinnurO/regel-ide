import { NavLink, Route, Routes } from 'react-router';
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
import VilkarstreListe from './pages/VilkarstreListe';
import VilkarstreDetalj from './pages/VilkarstreDetalj';
import DatasettDetalj from './pages/DatasettDetalj';
import DatasettListe from './pages/DatasettListe';
import TjenesteVeiledning from './pages/TjenesteVeiledning';
import TjenesteforslagKo from './pages/TjenesteforslagKo';
import BegrepsforslagKo from './pages/BegrepsforslagKo';
import NettsiderListe from './pages/NettsiderListe';
import NettsideDetalj from './pages/NettsideDetalj';

function BrukerVelger() {
  const { brukere, gjeldendeBruker, velgBruker, laster, ekteInnlogging, innloggingsfeil } = useBruker();

  if (laster) return null;

  // Med ekte innlogging er brukeren gitt — da er en nedtrekksliste både misvisende og uten effekt.
  if (ekteInnlogging) {
    return (
      <div className="bruker-velger">
        {/* «Innlogget som ukjent» ville skjult at noe er galt. Si hva som feilet i stedet. */}
        <Label data-size="sm">
          {innloggingsfeil ?? (
            <>
              Innlogget som {gjeldendeBruker?.navn ?? 'ukjent'}
              {gjeldendeBruker && ` (${gjeldendeBruker.rolle}) — ${gjeldendeBruker.virksomhetNavn}`}
            </>
          )}
        </Label>
      </div>
    );
  }

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
          <NavLink to="/vilkarstre" className={({ isActive }) => (isActive ? 'aktiv' : '')}>
            Vilkårstre
          </NavLink>
          <NavLink to="/datasett" className={({ isActive }) => (isActive ? 'aktiv' : '')}>
            Datasett
          </NavLink>
          <NavLink to="/nettsider" className={({ isActive }) => (isActive ? 'aktiv' : '')}>
            Nettsider
          </NavLink>
          <NavLink to="/tjenester/forslag" className={({ isActive }) => (isActive ? 'aktiv' : '')}>
            Identifiser tjenester (KI)
          </NavLink>
          <NavLink to="/begreper/forslag" className={({ isActive }) => (isActive ? 'aktiv' : '')}>
            Identifiser begrep (KI)
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
          <Route path="/tjenester/forslag" element={<TjenesteforslagKo />} />
          <Route path="/tjenester/:id" element={<TjenesteDetalj />} />
          <Route path="/begreper" element={<BegreperListe />} />
          <Route path="/begreper/forslag" element={<BegrepsforslagKo />} />
          <Route path="/begreper/:id" element={<BegrepDetalj />} />
          <Route path="/kodelister" element={<KodelisterListe />} />
          <Route path="/kodelister/:id" element={<KodelisteDetalj />} />
          <Route path="/vilkarstre" element={<VilkarstreListe />} />
          <Route path="/vilkarstre/:rotnodeId" element={<VilkarstreDetalj />} />
          <Route path="/datasett" element={<DatasettListe />} />
          <Route path="/datasett/:id" element={<DatasettDetalj />} />
          <Route path="/nettsider" element={<NettsiderListe />} />
          <Route path="/nettsider/:id" element={<NettsideDetalj />} />
          <Route path="/tjenester/:id/veiledning" element={<TjenesteVeiledning />} />
        </Routes>
      </main>
    </div>
  );
}

export default App;
