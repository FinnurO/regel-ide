import { NavLink, Route, Routes } from 'react-router';
import { Dropdown, Label } from '@digdir/designsystemet-react';
import { useBruker } from './bruker/BrukerContext';
import RettskilderListe from './pages/RettskilderListe';
import RettskildeDetalj from './pages/RettskildeDetalj';
import Importer from './pages/Importer';
import HandbokOpprett from './pages/HandbokOpprett';
import TjenesterListe from './pages/TjenesterListe';
import TjenesteDetalj from './pages/TjenesteDetalj';
import HandlingDetalj from './pages/HandlingDetalj';
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
import BrukereListe from './pages/BrukereListe';

/**
 * Identitetsindikator — alltid synlig øverst til høyre, samme sted på alle sider (se .topbar i
 * index.css). Erstatter den tidligere løpende teksten + fullbredde-<select> i sidebaren.
 * <para>
 * Under ekte innlogging (Altinn) vises identiteten som ren tekst UTEN bytt-mulighet — det gir ingen
 * mening å "bytte" når man faktisk er innlogget. Kun testbruker-profilen får en klikkbar brikke som
 * åpner en Dropdown for å bytte testbruker (erstatter den gamle <select>en, ikke i tillegg til den).
 * </para>
 */
function IdentitetsBrikke() {
  const { brukere, gjeldendeBruker, velgBruker, laster, ekteInnlogging, innloggingsfeil } = useBruker();

  if (laster) return null;

  if (innloggingsfeil) {
    // «Innlogget som ukjent» ville skjult at noe er galt. Si hva som feilet i stedet.
    return (
      <div className="identitet-brikke identitet-brikke--feil" role="status">
        <Label data-size="sm">{innloggingsfeil}</Label>
      </div>
    );
  }

  if (!gjeldendeBruker) return null;

  const detaljer = (
    <span className="identitet-brikke__tekst">
      <span className="identitet-brikke__navn">{gjeldendeBruker.navn}</span>
      <span className="identitet-brikke__meta">
        {gjeldendeBruker.virksomhetNavn} · {gjeldendeBruker.rolle}
      </span>
    </span>
  );

  if (ekteInnlogging) {
    return (
      <div className="identitet-brikke" aria-label="Innlogget bruker">
        {detaljer}
      </div>
    );
  }

  return (
    <Dropdown.TriggerContext>
      <Dropdown.Trigger variant="tertiary" className="identitet-brikke identitet-brikke--knapp">
        {detaljer}
        <span aria-hidden="true" className="identitet-brikke__pil">▾</span>
      </Dropdown.Trigger>
      <Dropdown placement="bottom-end">
        <Dropdown.Heading>Bytt testbruker</Dropdown.Heading>
        <Dropdown.List>
          {brukere.map((b) => (
            <Dropdown.Item key={b.id}>
              <Dropdown.Button
                aria-current={b.id === gjeldendeBruker.id}
                data-valgt={b.id === gjeldendeBruker.id || undefined}
                onClick={() => velgBruker(b.id)}
              >
                {b.navn} ({b.rolle}) — {b.virksomhetNavn}
              </Dropdown.Button>
            </Dropdown.Item>
          ))}
        </Dropdown.List>
      </Dropdown>
    </Dropdown.TriggerContext>
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
          <NavLink to="/tjenester/forslag" className={({ isActive }) => (isActive ? 'aktiv' : '')}>
            Identifiser tjenester (KI)
          </NavLink>
          <NavLink to="/begreper/forslag" className={({ isActive }) => (isActive ? 'aktiv' : '')}>
            Identifiser begrep (KI)
          </NavLink>
          <NavLink to="/brukere" className={({ isActive }) => (isActive ? 'aktiv' : '')}>
            Brukere
          </NavLink>
        </nav>
      </aside>
      <div className="hovedomrade">
        <header className="topbar">
          <IdentitetsBrikke />
        </header>
        <main className="innhold">
          <Routes>
            <Route path="/" element={<RettskilderListe />} />
            <Route path="/rettskilder/:id" element={<RettskildeDetalj />} />
            <Route path="/importer" element={<Importer />} />
            <Route path="/handboker/ny" element={<HandbokOpprett />} />
            <Route path="/tjenester" element={<TjenesterListe />} />
            <Route path="/tjenester/forslag" element={<TjenesteforslagKo />} />
            <Route path="/tjenester/:id" element={<TjenesteDetalj />} />
            <Route path="/tjenester/:tjenesteId/handlinger/:handlingId" element={<HandlingDetalj />} />
            <Route path="/begreper" element={<BegreperListe />} />
            <Route path="/begreper/forslag" element={<BegrepsforslagKo />} />
            <Route path="/begreper/:id" element={<BegrepDetalj />} />
            <Route path="/kodelister" element={<KodelisterListe />} />
            <Route path="/kodelister/:id" element={<KodelisteDetalj />} />
            <Route path="/vilkarstre" element={<VilkarstreListe />} />
            <Route path="/vilkarstre/:rotnodeId" element={<VilkarstreDetalj />} />
            <Route path="/datasett" element={<DatasettListe />} />
            <Route path="/datasett/:id" element={<DatasettDetalj />} />
            <Route path="/tjenester/:id/veiledning" element={<TjenesteVeiledning />} />
            <Route path="/brukere" element={<BrukereListe />} />
          </Routes>
        </main>
      </div>
    </div>
  );
}

export default App;
