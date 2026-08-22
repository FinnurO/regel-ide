import { Navigate, Route, Routes } from 'react-router';
import { Sidebar } from './nav/Sidebar';
import RettskilderListe from './pages/RettskilderListe';
import RettskildeDetalj from './pages/RettskildeDetalj';
import Importer from './pages/Importer';
import HandbokOpprett from './pages/HandbokOpprett';
import TjenesterListe from './pages/TjenesterListe';
import TjenesteDetalj from './pages/TjenesteDetalj';
import HandlingDetalj from './pages/HandlingDetalj';
import HandlingerListe from './pages/HandlingerListe';
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
import VirksomheterListe from './pages/VirksomheterListe';
import VirksomhetDetalj from './pages/VirksomhetDetalj';
import VirksomhetKandidaterListe from './pages/VirksomhetKandidaterListe';

function App() {
  return (
    <div className="layout">
      <Sidebar />
      <main className="innhold">
        <Routes>
          {/* "Startside" (Startside Alternativ 1c, 2026-08-20) ER Tjenester-siden — ingen egen
              landingsside bygget, siden det er virksomhetens eget arbeidsprodukt (ikke Rettskilder,
              den forrige defaultruten) som er det naturlige stedet å lande. */}
          <Route path="/" element={<Navigate to="/tjenester" replace />} />
          <Route path="/rettskilder" element={<RettskilderListe />} />
          <Route path="/rettskilder/:id" element={<RettskildeDetalj />} />
          <Route path="/importer" element={<Importer />} />
          <Route path="/handboker/ny" element={<HandbokOpprett />} />
          <Route path="/tjenester" element={<TjenesterListe />} />
          <Route path="/tjenester/forslag" element={<TjenesteforslagKo />} />
          <Route path="/tjenester/:id" element={<TjenesteDetalj />} />
          <Route path="/tjenester/:tjenesteId/handlinger/:handlingId" element={<HandlingDetalj />} />
          <Route path="/handlinger" element={<HandlingerListe />} />
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
          <Route path="/virksomheter" element={<VirksomheterListe />} />
          <Route path="/virksomheter/:id" element={<VirksomhetDetalj />} />
          <Route path="/virksomhet-kandidater" element={<VirksomhetKandidaterListe />} />
        </Routes>
      </main>
    </div>
  );
}

export default App;
