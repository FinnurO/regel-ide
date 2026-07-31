import type {
  ApiFeil,
  BegrepDto,
  BegrepRequest,
  BrukerDto,
  DatasettDto,
  DatasettVerdiDto,
  HendelseDto,
  HendelseRequest,
  KobleHendelseRequest,
  KobleBarnRequest,
  KobleLovreferanseRequest,
  KobleRegelverksreferanseRequest,
  KobleTaggTilEntitetRequest,
  KodelisteDto,
  KodelisteKodeDto,
  KodelisteRequest,
  LeggTilKodeRequest,
  LeggTilVilkarInputRequest,
  OppdaterRettskildeMetadataRequest,
  OppdaterUnntakRequest,
  OppdaterVilkarstreKommentarRequest,
  OpprettHandbokRequest,
  OpprettKapittelNodeRequest,
  OpprettKommentarNodeRequest,
  OpprettTekstTaggRequest,
  OpprettUnntakRequest,
  OpprettVilkarstreKommentarRequest,
  ProveniensDto,
  PubliserKommentarRequest,
  RedigerKommentarNodeRequest,
  RegelnodeBarnDto,
  RegelnodeDto,
  RegelnodeRequest,
  RettskildeDetalj,
  RettskildeNodeDto,
  RettskildeReferanseDto,
  RettskildeSammendrag,
  SettDatasettVerdiRequest,
  SettOperatorRequest,
  SettRevisjonsmerkeRequest,
  SettRotnodeRequest,
  SettStatusRequest,
  TaggKindKonfigurasjonDto,
  TekstTaggDto,
  TjenesteDto,
  TjenesteReferanseDto,
  TjenesteRegelverksreferanseDto,
  TjenesteavhengighetDto,
  TjenesteavhengighetRequest,
  HandbokRettskildeomfangDto,
  TjenesteRequest,
  UnntakDto,
  VeiledningDto,
  VilkarDto,
  VilkarRequest,
  VilkarstreKommentarDto,
  VirksomhetDto,
} from './types';

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5187';
const BRUKER_ID_LAGRINGSNOKKEL = 'regelide.brukerId';

export class ApiError extends Error {
  public status: number;
  constructor(message: string, status: number) {
    super(message);
    this.status = status;
  }
}

export function hentValgtBrukerId(): string | null {
  return localStorage.getItem(BRUKER_ID_LAGRINGSNOKKEL);
}

export function settValgtBrukerId(brukerId: string | null) {
  if (brukerId) localStorage.setItem(BRUKER_ID_LAGRINGSNOKKEL, brukerId);
  else localStorage.removeItem(BRUKER_ID_LAGRINGSNOKKEL);
}

async function kall<T>(path: string, init?: RequestInit): Promise<T> {
  const brukerId = hentValgtBrukerId();
  const headers = new Headers(init?.headers);
  if (brukerId && !headers.has('X-Bruker-Id')) headers.set('X-Bruker-Id', brukerId);

  const svar = await fetch(`${API_BASE}${path}`, { ...init, headers });
  if (!svar.ok) {
    let melding = `${svar.status} ${svar.statusText}`;
    try {
      const feil = (await svar.json()) as ApiFeil;
      if (feil?.feil) melding = feil.feil;
    } catch {
      // ikke JSON — behold statusteksten
    }
    throw new ApiError(melding, svar.status);
  }
  if (svar.status === 204) return undefined as T;
  return (await svar.json()) as T;
}

export const api = {
  hentRettskilder: (virksomhetId?: string) =>
    kall<RettskildeSammendrag[]>(`/api/rettskilder${virksomhetId ? `?virksomhetId=${virksomhetId}` : ''}`),

  hentRettskilde: (id: string) => kall<RettskildeDetalj>(`/api/rettskilder/${id}`),

  hentNoder: (id: string) => kall<RettskildeNodeDto[]>(`/api/rettskilder/${id}/noder`),

  hentReferanser: (id: string) => kall<RettskildeReferanseDto[]>(`/api/rettskilder/${id}/referanser`),

  opprettNodeReferanse: (rettskildeId: string, nodeId: string, request: KobleLovreferanseRequest) =>
    kall<RettskildeReferanseDto>(`/api/rettskilder/${rettskildeId}/noder/${nodeId}/referanser`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  fjernNodeReferanse: (rettskildeId: string, referanseId: string) =>
    kall<void>(`/api/rettskilder/${rettskildeId}/referanser/${referanseId}`, { method: 'DELETE' }),

  hentReferertAvTjenester: (id: string) =>
    kall<TjenesteReferanseDto[]>(`/api/rettskilder/${id}/referert-av-tjenester`),

  oppdaterRettskildeMetadata: (id: string, request: OppdaterRettskildeMetadataRequest) =>
    kall<RettskildeDetalj>(`/api/rettskilder/${id}/metadata`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  hentTagger: (rettskildeId: string) => kall<TekstTaggDto[]>(`/api/rettskilder/${rettskildeId}/tagger`),

  opprettTagg: (rettskildeId: string, request: OpprettTekstTaggRequest) =>
    kall<TekstTaggDto>(`/api/rettskilder/${rettskildeId}/tagger`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  slettTagg: (rettskildeId: string, taggId: string) =>
    kall<void>(`/api/rettskilder/${rettskildeId}/tagger/${taggId}`, { method: 'DELETE' }),

  kobleTaggTilEntitet: (rettskildeId: string, taggId: string, request: KobleTaggTilEntitetRequest) =>
    kall<TekstTaggDto>(`/api/rettskilder/${rettskildeId}/tagger/${taggId}/koble`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  hentBrukere: () => kall<BrukerDto[]>('/api/brukere'),
  hentOppsett: () => kall<{ autentisering: 'testbruker' | 'altinn' }>('/api/oppsett'),
  hentMeg: () => kall<BrukerDto>('/api/meg'),

  hentVirksomheter: () => kall<VirksomhetDto[]>('/api/virksomheter'),

  hentTaggKinds: () => kall<TaggKindKonfigurasjonDto[]>('/api/konfigurasjon/tagg-kinds'),

  importerFraLovdata: (datokode: string) =>
    kall<{ id: string }>('/api/rettskilder/lovdata', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ datokode }),
    }),

  importerFraFil: (fil: File, virksomhetId?: string) => {
    const skjema = new FormData();
    skjema.append('fil', fil);
    const query = virksomhetId ? `?virksomhetId=${virksomhetId}` : '';
    return kall<{ id: string }>(`/api/rettskilder/fil${query}`, { method: 'POST', body: skjema });
  },

  // ---------- Håndbok/rundskriv-forfatterflyt (2026-07-26, AK-3.3.8–3.3.12) ----------

  opprettHandbok: (request: OpprettHandbokRequest) =>
    kall<{ id: string }>('/api/handboker', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  opprettKapittelNode: (handbokId: string, request: OpprettKapittelNodeRequest) =>
    kall<RettskildeNodeDto>(`/api/handboker/${handbokId}/kapitler`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  opprettKommentarNode: (handbokId: string, request: OpprettKommentarNodeRequest) =>
    kall<RettskildeNodeDto>(`/api/handboker/${handbokId}/kommentarer`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  redigerKommentarNode: (handbokId: string, nodeId: string, request: RedigerKommentarNodeRequest) =>
    kall<RettskildeNodeDto>(`/api/handboker/${handbokId}/kommentarer/${nodeId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  hentVersjonshistorikk: (handbokId: string, eid: string) =>
    kall<RettskildeNodeDto[]>(`/api/handboker/${handbokId}/kommentarer/versjoner?eid=${encodeURIComponent(eid)}`),

  kobleLovreferanse: (handbokId: string, nodeId: string, request: KobleLovreferanseRequest) =>
    kall<RettskildeReferanseDto>(`/api/handboker/${handbokId}/kommentarer/${nodeId}/lovreferanser`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  fjernLovreferanse: (handbokId: string, nodeId: string, referanseId: string) =>
    kall<void>(`/api/handboker/${handbokId}/kommentarer/${nodeId}/lovreferanser/${referanseId}`, { method: 'DELETE' }),

  settRevisjonsmerke: (handbokId: string, nodeId: string, request: SettRevisjonsmerkeRequest) =>
    kall<void>(`/api/handboker/${handbokId}/kommentarer/${nodeId}/revisjonsmerke`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  publiserKommentar: (handbokId: string, nodeId: string, request: PubliserKommentarRequest) =>
    kall<void>(`/api/handboker/${handbokId}/kommentarer/${nodeId}/publiser`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  hentHandbokRettskildeomfang: (handbokId: string) =>
    kall<HandbokRettskildeomfangDto[]>(`/api/handboker/${handbokId}/rettskilder`),

  leggTilHandbokRettskildeomfang: (handbokId: string, tilRettskildeId: string) =>
    kall<HandbokRettskildeomfangDto>(`/api/handboker/${handbokId}/rettskilder`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tilRettskildeId }),
    }),

  fjernHandbokRettskildeomfang: (handbokId: string, omfangId: string) =>
    kall<void>(`/api/handboker/${handbokId}/rettskilder/${omfangId}`, { method: 'DELETE' }),

  // ---------- Tjenesteregister (CPSV-AP-NO, docs/03-domenemodell.md §1.5) — byggesteg 2 ----------

  hentTjenester: () => kall<TjenesteDto[]>('/api/tjenester'),

  hentTjeneste: (id: string) => kall<TjenesteDto>(`/api/tjenester/${id}`),

  opprettTjeneste: (request: TjenesteRequest) =>
    kall<TjenesteDto>('/api/tjenester', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  oppdaterTjeneste: (id: string, request: TjenesteRequest) =>
    kall<TjenesteDto>(`/api/tjenester/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  settTjenesteStatus: (id: string, request: SettStatusRequest) =>
    kall<TjenesteDto>(`/api/tjenester/${id}/status`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  hentTjenesteRegelverksreferanser: (id: string) =>
    kall<TjenesteRegelverksreferanseDto[]>(`/api/tjenester/${id}/regelverksreferanser`),

  kobleTjenesteRegelverksreferanse: (id: string, request: KobleRegelverksreferanseRequest) =>
    kall<TjenesteRegelverksreferanseDto>(`/api/tjenester/${id}/regelverksreferanser`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  fjernTjenesteRegelverksreferanse: (referanseId: string) =>
    kall<void>(`/api/tjenester/regelverksreferanser/${referanseId}`, { method: 'DELETE' }),

  // ---------- Hendelseregister (docs/03-domenemodell.md §1.5, docs/13-backlog.md §2.1) ----------

  hentHendelser: () => kall<HendelseDto[]>('/api/hendelser'),

  opprettHendelse: (request: HendelseRequest) =>
    kall<HendelseDto>('/api/hendelser', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  hentTjenesteHendelser: (id: string) => kall<HendelseDto[]>(`/api/tjenester/${id}/hendelser`),

  kobleTjenesteHendelse: (id: string, request: KobleHendelseRequest) =>
    kall<HendelseDto[]>(`/api/tjenester/${id}/hendelser`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  fjernTjenesteHendelse: (id: string, hendelseId: string) =>
    kall<void>(`/api/tjenester/${id}/hendelser/${hendelseId}`, { method: 'DELETE' }),

  // ---------- Tjenesteavhengighetregister (docs/03-domenemodell.md §1.5, docs/13-backlog.md §2.1) ----------

  hentTjenesteavhengigheter: (id: string) => kall<TjenesteavhengighetDto[]>(`/api/tjenester/${id}/avhengigheter`),

  opprettTjenesteavhengighet: (id: string, request: TjenesteavhengighetRequest) =>
    kall<TjenesteavhengighetDto[]>(`/api/tjenester/${id}/avhengigheter`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  slettTjenesteavhengighet: (avhengighetId: string) =>
    kall<void>(`/api/tjenester/avhengigheter/${avhengighetId}`, { method: 'DELETE' }),

  // ---------- Begrepsregister (SKOS, docs/03-domenemodell.md §1.3) — byggesteg 2 ----------

  hentBegreper: () => kall<BegrepDto[]>('/api/begreper'),

  hentBegrep: (id: string) => kall<BegrepDto>(`/api/begreper/${id}`),

  opprettBegrep: (request: BegrepRequest) =>
    kall<BegrepDto>('/api/begreper', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  oppdaterBegrep: (id: string, request: BegrepRequest) =>
    kall<BegrepDto>(`/api/begreper/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  settBegrepStatus: (id: string, request: SettStatusRequest) =>
    kall<BegrepDto>(`/api/begreper/${id}/status`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  // ---------- Kodelisteregister / verdidomene (docs/03-domenemodell.md §1.4) — byggesteg 2 ----------

  hentKodelister: () => kall<KodelisteDto[]>('/api/kodelister'),

  hentKodeliste: (id: string) => kall<KodelisteDto>(`/api/kodelister/${id}`),

  opprettKodeliste: (request: KodelisteRequest) =>
    kall<KodelisteDto>('/api/kodelister', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  leggTilKodelisteKode: (id: string, request: LeggTilKodeRequest) =>
    kall<KodelisteKodeDto>(`/api/kodelister/${id}/koder`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  fjernKodelisteKode: (kodeId: string) => kall<void>(`/api/kodelister/koder/${kodeId}`, { method: 'DELETE' }),

  settKodelisteStatus: (id: string, request: SettStatusRequest) =>
    kall<KodelisteDto>(`/api/kodelister/${id}/status`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  // ---------- Vilkårstre (byggesteg 4 runde 1, docs/03-domenemodell.md §1.6/§1.8-1.10) ----------

  hentDatasett: () => kall<DatasettDto[]>('/api/datasett'),

  hentDatasettVerdier: (id: string) => kall<DatasettVerdiDto[]>(`/api/datasett/${id}/verdier`),

  settDatasettVerdi: (id: string, request: SettDatasettVerdiRequest) =>
    kall<DatasettVerdiDto>(`/api/datasett/${id}/verdier`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  fjernDatasettVerdi: (verdiId: string) => kall<void>(`/api/datasett/verdier/${verdiId}`, { method: 'DELETE' }),

  // ---------- Vilkårstre-kommentarer (docs/12-fasit-handbok-leveranse.md "Hovedfunn" + dimensjon A) ----------

  hentVilkarstreKommentarer: (malType: string, malId: string) =>
    kall<VilkarstreKommentarDto[]>(`/api/vilkarstre-kommentarer?malType=${malType}&malId=${malId}`),

  opprettVilkarstreKommentar: (request: OpprettVilkarstreKommentarRequest) =>
    kall<VilkarstreKommentarDto>('/api/vilkarstre-kommentarer', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  oppdaterVilkarstreKommentar: (id: string, request: OppdaterVilkarstreKommentarRequest) =>
    kall<VilkarstreKommentarDto>(`/api/vilkarstre-kommentarer/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  fjernVilkarstreKommentar: (id: string) => kall<void>(`/api/vilkarstre-kommentarer/${id}`, { method: 'DELETE' }),

  flyttVilkarstreKommentar: (id: string, retning: 'opp' | 'ned') =>
    kall<VilkarstreKommentarDto>(`/api/vilkarstre-kommentarer/${id}/flytt`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ retning }),
    }),

  hentVilkarListe: (tjenesteId?: string) =>
    kall<VilkarDto[]>(tjenesteId ? `/api/vilkar?tjenesteId=${tjenesteId}` : '/api/vilkar'),

  hentVilkar: (id: string) => kall<VilkarDto>(`/api/vilkar/${id}`),

  opprettVilkar: (request: VilkarRequest) =>
    kall<VilkarDto>('/api/vilkar', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  oppdaterVilkar: (id: string, request: VilkarRequest) =>
    kall<VilkarDto>(`/api/vilkar/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  settVilkarStatus: (id: string, request: SettStatusRequest) =>
    kall<VilkarDto>(`/api/vilkar/${id}/status`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  hentVilkarInput: (id: string) => kall<DatasettDto[]>(`/api/vilkar/${id}/input`),

  leggTilVilkarInput: (id: string, request: LeggTilVilkarInputRequest) =>
    kall<DatasettDto>(`/api/vilkar/${id}/input`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  fjernVilkarInput: (id: string, datasettId: string) =>
    kall<void>(`/api/vilkar/${id}/input/${datasettId}`, { method: 'DELETE' }),

  hentVilkarHistorikk: (id: string) => kall<ProveniensDto[]>(`/api/vilkar/${id}/historikk`),

  hentRegelnodeListe: () => kall<RegelnodeDto[]>('/api/regelnoder'),

  hentRegelnode: (id: string) => kall<RegelnodeDto>(`/api/regelnoder/${id}`),

  opprettRegelnode: (request: RegelnodeRequest) =>
    kall<RegelnodeDto>('/api/regelnoder', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  oppdaterRegelnode: (id: string, request: RegelnodeRequest) =>
    kall<RegelnodeDto>(`/api/regelnoder/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  settRegelnodeOperator: (id: string, request: SettOperatorRequest) =>
    kall<RegelnodeDto>(`/api/regelnoder/${id}/operator`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  settRegelnodeStatus: (id: string, request: SettStatusRequest) =>
    kall<RegelnodeDto>(`/api/regelnoder/${id}/status`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  hentRegelnodeBarn: (id: string) => kall<RegelnodeBarnDto[]>(`/api/regelnoder/${id}/barn`),

  kobleRegelnodeBarn: (id: string, request: KobleBarnRequest) =>
    kall<RegelnodeBarnDto>(`/api/regelnoder/${id}/barn`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  fjernRegelnodeBarn: (id: string, barnType: string, barnId: string) =>
    kall<void>(`/api/regelnoder/${id}/barn/${barnType}/${barnId}`, { method: 'DELETE' }),

  hentRegelnodeHistorikk: (id: string) => kall<ProveniensDto[]>(`/api/regelnoder/${id}/historikk`),

  hentUnntakListe: () => kall<UnntakDto[]>('/api/unntak'),

  hentUnntak: (id: string) => kall<UnntakDto>(`/api/unntak/${id}`),

  opprettUnntak: (request: OpprettUnntakRequest) =>
    kall<UnntakDto>('/api/unntak', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  oppdaterUnntak: (id: string, request: OppdaterUnntakRequest) =>
    kall<UnntakDto>(`/api/unntak/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  settUnntakStatus: (id: string, request: SettStatusRequest) =>
    kall<UnntakDto>(`/api/unntak/${id}/status`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  hentUnntakHistorikk: (id: string) => kall<ProveniensDto[]>(`/api/unntak/${id}/historikk`),

  settTjenesteRotnode: (id: string, request: SettRotnodeRequest) =>
    kall<TjenesteDto>(`/api/tjenester/${id}/rotnode`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  fjernTjenesteRotnode: (id: string) =>
    kall<TjenesteDto>(`/api/tjenester/${id}/rotnode`, { method: 'DELETE' }),

  hentTjenesteVeiledning: (id: string, virksomhetId: string | null) =>
    kall<VeiledningDto>(`/api/tjenester/${id}/veiledning${virksomhetId ? `?virksomhetId=${virksomhetId}` : ''}`),
};
