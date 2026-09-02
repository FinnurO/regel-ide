import type {
  ApiFeil,
  BegrepBruktIRettskildeDto,
  BegrepDto,
  BegrepRequest,
  BrukerDto,
  OppdaterBrukerRequest,
  OpprettBrukerRequest,
  DatasettDto,
  DatasettVerdiDto,
  BegrepsforslagDto,
  DokumentReferanseDto,
  HandlingDto,
  HandlingMedTjenesteDto,
  HandlingRegelverksreferanseDto,
  HandlingRequest,
  HandlingTjenesteDto,
  KobleHandlingRequest,
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
  KjorForslagRequest,
  KjorForslagResponsDto,
  KjorHandlingsforslagRequest,
  TjenesteMedHandlingerDto,
  KunnskapsbibliotekFilDto,
  KunnskapsbibliotekLenkeDto,
  LovdataImportstatusDto,
  LovdataKatalogTreffDto,
  LeggTilKodeRequest,
  LeggTilLenkeRequest,
  LeggTilVilkarInputRequest,
  OppdaterRettskildeIrrelevantRequest,
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
  NettsideLenkeMedMalDto,
  NettsideStiDto,
  RettskildeDetalj,
  RettskildeHjemletForDto,
  RettskildeHjemmelDto,
  RettskildeNodeDto,
  RettskildeReferanseDto,
  RettskildeSammendrag,
  ImportRettighetRequest,
  AvhengighetsgrafDto,
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
  TjenesteTverrTenantTreffDto,
  TjenesteforslagDto,
  TjenesteforslagBatchRequest,
  TjenesteforslagBatchResultatDto,
  TjenesteforslagSlettBatchResultatDto,
  MittForslagDto,
  BegrepsforslagBatchRequest,
  BegrepsforslagBatchResultatDto,
  HandbokRettskildeomfangDto,
  TjenesteRequest,
  UnntakDto,
  VeiledningDto,
  VilkarDto,
  VilkarRequest,
  VilkarstreKommentarDto,
  VirksomhetDto,
  OpprettVirksomhetRequest,
  BrregEnhetDto,
  VirksomhetsbegrepDto,
  MyndighetstildelingDto,
  ParagrafspennParDto,
  VirksomhetKandidatDto,
  SveipVirksomhetKandidaterRequest,
  SveipVirksomhetKandidaterResultatDto,
  VirksomhetKandidatBatchRequest,
  VirksomhetKandidatBatchResultatDto,
  HardslettVirksomhetKandidaterResultatDto,
  NavnekandidatDto,
  SveipNavnekandidaterRequest,
  SveipNavnekandidaterResultatDto,
  NavnekandidatBatchRequest,
  NavnekandidatBatchResultatDto,
  SlettNavnekandidaterResultatDto,
  VisningsinnstillingInput,
} from './types';

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5187';

/**
 * Bygger URL-en for et API-kall.
 *
 * Er API_BASE satt (vite dev mot et API på annen port) brukes den absolutt, som før. Er den tom
 * — altså API og SPA fra samme origin, som i containeren — gjøres stien relativ, slik at den
 * løses mot <base href> og treffer riktig også når appen står under et sti-prefiks. Med en
 * rot-absolutt "/api/..." ville kallet gått utenfor appen og gitt 404 i app-clusteret.
 */
export function apiUrl(path: string): string {
  return API_BASE ? `${API_BASE}${path}` : path.replace(/^\//, '');
}
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

  const svar = await fetch(apiUrl(path), { ...init, headers });
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
  // ?inkluderIrrelevante=true (2026-08-30) — utelatt/false ekskluderer ErIrrelevant-markerte kilder
  // stille fra standardvisningen, se RettskilderListe.tsx.
  hentRettskilder: (virksomhetId?: string, inkluderIrrelevante?: boolean) => {
    const params = new URLSearchParams();
    if (virksomhetId) params.set('virksomhetId', virksomhetId);
    if (inkluderIrrelevante) params.set('inkluderIrrelevante', 'true');
    const query = params.toString();
    return kall<RettskildeSammendrag[]>(`/api/rettskilder${query ? `?${query}` : ''}`);
  },

  hentRettskilde: (id: string) => kall<RettskildeDetalj>(`/api/rettskilder/${id}`),

  hentNoder: (id: string) => kall<RettskildeNodeDto[]>(`/api/rettskilder/${id}/noder`),

  hentReferanser: (id: string) => kall<RettskildeReferanseDto[]>(`/api/rettskilder/${id}/referanser`),

  hentHjemmel: (id: string) => kall<RettskildeHjemmelDto[]>(`/api/rettskilder/${id}/hjemmel`),

  hentHjemmelFor: (id: string) => kall<RettskildeHjemletForDto[]>(`/api/rettskilder/${id}/hjemmel-for`),

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

  hentReferertAvDokumenter: (id: string) =>
    kall<DokumentReferanseDto[]>(`/api/rettskilder/${id}/referert-av-dokumenter`),

  oppdaterRettskildeMetadata: (id: string, request: OppdaterRettskildeMetadataRequest) =>
    kall<RettskildeDetalj>(`/api/rettskilder/${id}/metadata`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  oppdaterRettskildeIrrelevant: (id: string, request: OppdaterRettskildeIrrelevantRequest) =>
    kall<RettskildeDetalj>(`/api/rettskilder/${id}/irrelevant`, {
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

  opprettBruker: (request: OpprettBrukerRequest) =>
    kall<BrukerDto>('/api/brukere', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  oppdaterBruker: (id: string, request: OppdaterBrukerRequest) =>
    kall<BrukerDto>(`/api/brukere/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  // Innlogget brukers visningsinnstillinger for Tjeneste-siden (2026-08-27) — per bruker, se VisningsinnstillingInput.
  hentTjenesteVisningsinnstillinger: () => kall<VisningsinnstillingInput>('/api/brukere/meg/tjeneste-visning'),

  lagreTjenesteVisningsinnstillinger: (request: VisningsinnstillingInput) =>
    kall<VisningsinnstillingInput>('/api/brukere/meg/tjeneste-visning', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  hentVirksomheter: () => kall<VirksomhetDto[]>('/api/virksomheter'),

  hentVirksomhetsbegrep: (virksomhetId: string) =>
    kall<VirksomhetsbegrepDto[]>(`/api/virksomheter/${virksomhetId}/begrep`),

  opprettVirksomhetsbegrep: (request: { virksomhetId: string; term: string; skosUrl: string | null }) =>
    kall<VirksomhetsbegrepDto>('/api/virksomhetsbegrep', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  hentMyndighetstildelingerForVirksomhet: (virksomhetId: string) =>
    kall<MyndighetstildelingDto[]>(`/api/virksomheter/${virksomhetId}/myndighetstildelinger`),

  /** ALLE gruppebegrep på tvers av lover — søk/velg-grunnlag for LeggTilMyndighetstildelingForm. */
  hentGruppebegrep: () => kall<VirksomhetsbegrepDto[]>('/api/gruppebegrep'),

  opprettMyndighetstildeling: (request: {
    gruppeBegrepId: string; virksomhetId: string; hjemmelRettskildeId: string;
    paragrafspenn: ParagrafspennParDto[]; vilkaar: string | null;
  }) =>
    kall<MyndighetstildelingDto>('/api/myndighetstildelinger', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  /** Departement-virksomhet-lenke (2026-08-30) — gjeldende lover/forskrifter der AnsvarligDepartement eksakt matcher denne virksomhetens navn. */
  hentRettskilderAnsvarligFor: (virksomhetId: string) =>
    kall<RettskildeSammendrag[]>(`/api/virksomheter/${virksomhetId}/rettskilder-ansvarlig-for`),

  hentVentendeKandidater: (virksomhetId: string) =>
    kall<VirksomhetKandidatDto[]>(`/api/virksomhet-kandidater?virksomhetId=${virksomhetId}`),

  /** Full kandidatliste-UI (kravspek §4.2 pkt. 3) — status='Alle' fjerner statusfiltreringen helt. */
  hentVirksomhetKandidater: (filter: { virksomhetId?: string; rettskildeId?: string; status?: string }) => {
    const parametre = new URLSearchParams();
    if (filter.virksomhetId) parametre.set('virksomhetId', filter.virksomhetId);
    if (filter.rettskildeId) parametre.set('rettskildeId', filter.rettskildeId);
    if (filter.status) parametre.set('status', filter.status);
    const sok = parametre.toString();
    return kall<VirksomhetKandidatDto[]>(`/api/virksomhet-kandidater${sok ? `?${sok}` : ''}`);
  },

  sveipVirksomhetKandidater: (request: SveipVirksomhetKandidaterRequest) =>
    kall<SveipVirksomhetKandidaterResultatDto>('/api/virksomhet-kandidater/sveip', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  godkjennVirksomhetKandidaterBatch: (request: VirksomhetKandidatBatchRequest) =>
    kall<VirksomhetKandidatBatchResultatDto>('/api/virksomhet-kandidater/godkjenn-batch', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  avvisVirksomhetKandidaterBatch: (request: VirksomhetKandidatBatchRequest) =>
    kall<VirksomhetKandidatBatchResultatDto>('/api/virksomhet-kandidater/avvis-batch', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  hardslettVirksomhetKandidat: (id: string) =>
    kall<void>(`/api/virksomhet-kandidater/${id}`, { method: 'DELETE' }),

  /** [Ny] Massehardsletting — KUN 'Avvist'-rader rammes, uansett filter (backend tvinger dette, se
   * VirksomhetKandidatTjeneste.HardslettAlleAvvisteAsync) — til forskjell fra slettAlleNavnekandidater
   * under, som aksepterer et fritt statusfilter fordi den entiteten ikke har noen sidevirkning å
   * beskytte. Status sendes derfor bevisst IKKE med her — klienten har ingen gyldig verdi å tilby utover
   * det backend allerede tvinger. */
  hardslettAlleAvvisteVirksomhetKandidater: (filter: { virksomhetId?: string; rettskildeId?: string }) => {
    const parametre = new URLSearchParams();
    if (filter.virksomhetId) parametre.set('virksomhetId', filter.virksomhetId);
    if (filter.rettskildeId) parametre.set('rettskildeId', filter.rettskildeId);
    const sok = parametre.toString();
    return kall<HardslettVirksomhetKandidaterResultatDto>(`/api/virksomhet-kandidater${sok ? `?${sok}` : ''}`, { method: 'DELETE' });
  },

  settVirksomhetForvaltningsniva: (id: string, forvaltningsniva: string | null) =>
    kall<VirksomhetDto>(`/api/virksomheter/${id}/forvaltningsniva`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ forvaltningsniva }),
    }),

  /** [Ny, 2026-08-30] Opprett en virksomhet med KUN navn — for aktører uten egen Brreg-registrering
   * (f.eks. Kystvakten, del av Forsvaret). */
  opprettVirksomhet: (request: OpprettVirksomhetRequest) =>
    kall<VirksomhetDto>('/api/virksomheter', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  /** [Ny, 2026-08-29, docs/13-backlog.md §9] Fritekstsøk mot Brreg — for å finne og opprette
   * virksomheter som mangler i katalogen. */
  sokBrreg: (tekst: string) => kall<BrregEnhetDto[]>(`/api/virksomheter/brreg-sok?q=${encodeURIComponent(tekst)}`),

  opprettVirksomhetFraBrreg: (organisasjonsnummer: string) =>
    kall<VirksomhetDto>('/api/virksomheter/fra-brreg', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ organisasjonsnummer }),
    }),

  godkjennVirksomhetKandidat: (id: string) =>
    kall<VirksomhetKandidatDto>(`/api/virksomhet-kandidater/${id}/godkjenn`, { method: 'POST' }),

  avvisVirksomhetKandidat: (id: string) =>
    kall<VirksomhetKandidatDto>(`/api/virksomhet-kandidater/${id}/avvis`, { method: 'POST' }),

  /** [Ny, docs/13-backlog.md §9] Oppdagelsesmekanismen — komplementær til virksomhet-kandidatene over. */
  hentNavnekandidater: (filter: { rettskildeId?: string; status?: string; kategori?: string }) => {
    const parametre = new URLSearchParams();
    if (filter.rettskildeId) parametre.set('rettskildeId', filter.rettskildeId);
    if (filter.status) parametre.set('status', filter.status);
    if (filter.kategori) parametre.set('kategori', filter.kategori);
    const sok = parametre.toString();
    return kall<NavnekandidatDto[]>(`/api/navnekandidater${sok ? `?${sok}` : ''}`);
  },

  sveipNavnekandidater: (request: SveipNavnekandidaterRequest) =>
    kall<SveipNavnekandidaterResultatDto>('/api/navnekandidater/sveip', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  godkjennNavnekandidat: (id: string) =>
    kall<NavnekandidatDto>(`/api/navnekandidater/${id}/godkjenn`, { method: 'POST' }),

  avvisNavnekandidat: (id: string) =>
    kall<NavnekandidatDto>(`/api/navnekandidater/${id}/avvis`, { method: 'POST' }),

  godkjennNavnekandidaterBatch: (request: NavnekandidatBatchRequest) =>
    kall<NavnekandidatBatchResultatDto>('/api/navnekandidater/godkjenn-batch', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  avvisNavnekandidaterBatch: (request: NavnekandidatBatchRequest) =>
    kall<NavnekandidatBatchResultatDto>('/api/navnekandidater/avvis-batch', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  /** [Ny, 2026-08-30] Ekte sletting av ÉN rad, uansett status — se backend-kommentaren
   * (NavnekandidatOppdagelseTjeneste.SlettAsync) for hvorfor. */
  slettNavnekandidat: (id: string) =>
    kall<void>(`/api/navnekandidater/${id}`, { method: 'DELETE' }),

  /** [Ny, 2026-08-30] Massesletting, valgfritt filtrert (samme filterparametre som hentNavnekandidater
   * over) — utelatt status betyr her "ingen statusfilter" (slett ALLE statuser), IKKE
   * hentNavnekandidater sin "utelatt = kun Venter"-standard. Server-side RemoveRange, ikke N separate
   * kall — nødvendig for ytelse ved tusenvis av kandidater (docs-kommentar i NavnekandidaterListe.tsx). */
  slettAlleNavnekandidater: (filter: { status?: string; kategori?: string; rettskildeId?: string }) => {
    const parametre = new URLSearchParams();
    if (filter.status) parametre.set('status', filter.status);
    if (filter.kategori) parametre.set('kategori', filter.kategori);
    if (filter.rettskildeId) parametre.set('rettskildeId', filter.rettskildeId);
    const sok = parametre.toString();
    return kall<SlettNavnekandidaterResultatDto>(`/api/navnekandidater${sok ? `?${sok}` : ''}`, { method: 'DELETE' });
  },

  hentTaggKinds: () => kall<TaggKindKonfigurasjonDto[]>('/api/konfigurasjon/tagg-kinds'),

  importerFraLovdata: (datokode: string) =>
    kall<{ id: string }>('/api/rettskilder/lovdata', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ datokode }),
    }),

  sokLovdataKatalog: (q: string) =>
    kall<LovdataKatalogTreffDto[]>(`/api/lovdata-katalog/sok?q=${encodeURIComponent(q)}`),

  hentLovdataImportstatus: (importert?: boolean) =>
    kall<LovdataImportstatusDto[]>(`/api/lovdata-importstatus${importert !== undefined ? `?importert=${importert}` : ''}`),

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

  // ---------- Nettsider / Brukerveiledning (docs/15-handbok-dokumentgraf-notat.md §3.1/§3.2/§3.4) ----------
  // ---------- Punkt 8: nettsider vises nå via /api/rettskilder — disse to dekker kun §3.4/§3.2. ----------

  hentRettskildeStier: (id: string) => kall<NettsideStiDto[]>(`/api/rettskilder/${id}/stier`),

  hentRettskildeNettsideLenker: (id: string) => kall<NettsideLenkeMedMalDto[]>(`/api/rettskilder/${id}/nettside-lenker`),

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

  /** [Ny, 2026-08-28] Hard-sletter et FORTSATT ubehandlet forslag (foreslatt_av_ai/
   * foreslatt_av_annen_virksomhet) — enten eieren eller virksomheten som faktisk kjørte importen kan
   * slette. IKKE en "Avvis"-erstatning (den beholder innholdet, se TjenesteforslagKo.tsx) — for
   * opprydding etter en import-test. */
  slettTjenesteforslag: (id: string) =>
    kall<void>(`/api/tjenester/${id}/forslag`, { method: 'DELETE' }),

  hentTjenesteRegelverksreferanser: (id: string) =>
    kall<TjenesteRegelverksreferanseDto[]>(`/api/tjenester/${id}/regelverksreferanser`),

  // Hele, sammensatte modelleksporten for én tjeneste (snake_case JSON, se
  // RettighetModellEksportTjeneste) — ingen egen DTO her, formen er bevisst fleksibel/under utvikling
  // og speiler ikke UI-ets egne camelCase-typer et-til-et.
  hentModelleksport: (id: string) => kall<Record<string, unknown>>(`/api/tjenester/${id}/modelleksport`),

  sokTjenesterTverrTenant: (q: string) =>
    kall<TjenesteTverrTenantTreffDto[]>(`/api/tjenester/sok-tverr-tenant?q=${encodeURIComponent(q)}`),

  // ---------- «Identifiser tjenester» (byggesteg 5 runde 1, docs/06-veikart.md) — stub-KI ----------

  hentTjenesteforslagKo: () => kall<TjenesteforslagDto[]>('/api/tjenester/forslag'),

  /** [Ny, 2026-08-29] Motstykket til hentTjenesteforslagKo — se MittForslagDto. */
  hentMineForslagTilAndreVirksomheter: () => kall<MittForslagDto[]>('/api/tjenester/foreslatt-av-meg'),

  kjorTjenesteforslag: (request: KjorForslagRequest) =>
    kall<KjorForslagResponsDto<TjenesteDto>>('/api/tjenester/forslag/kjor', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  /** Massegodkjenning/-avvisning/-sletting av tjenesteforslag (store test-import-/-sveip-mengder) —
   * server-side batch, per-rad-feilhåndtering, samme mønster som virksomhet-kandidater-batchen over.
   * `slettTjenesteforslagBatch` dekker BÅDE «Ventende forslag» sin Slett OG «Mine forslag til andre
   * virksomheter» sin Slett-alle-merkede — se backend-DTO-kommentaren for hvorfor ett endepunkt holder. */
  godkjennTjenesteforslagBatch: (request: TjenesteforslagBatchRequest) =>
    kall<TjenesteforslagBatchResultatDto>('/api/tjenester/forslag/godkjenn-batch', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  avvisTjenesteforslagBatch: (request: TjenesteforslagBatchRequest) =>
    kall<TjenesteforslagBatchResultatDto>('/api/tjenester/forslag/avvis-batch', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  slettTjenesteforslagBatch: (request: TjenesteforslagBatchRequest) =>
    kall<TjenesteforslagSlettBatchResultatDto>('/api/tjenester/forslag/slett-batch', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  // Omfang "full" (handlingsforslag-ki-omfang-runden) — samme endepunkt som over, men request.omfang
  // = 'full' gir en ANNEN responsform (Tjeneste + Handlinger per element) — egen typet klientmetode
  // i stedet for å overbelaste kjorTjenesteforslag sin returtype.
  kjorFullTjenesteforslag: (request: KjorForslagRequest) =>
    kall<KjorForslagResponsDto<TjenesteMedHandlingerDto>>('/api/tjenester/forslag/kjor', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  // Omfang "handling" (handlingsforslag-ki-omfang-runden) — Handlinger for ÉN eksisterende tjeneste.
  kjorHandlingsforslag: (tjenesteId: string, request: KjorHandlingsforslagRequest) =>
    kall<KjorForslagResponsDto<HandlingDto>>(`/api/tjenester/${tjenesteId}/handlinger/forslag/kjor`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  // ---------- Kunnskapsbibliotek (byggesteg 5 runde 1) — kun brukt av «Identifiser tjenester» ----------

  hentKunnskapsbibliotekLenker: () => kall<KunnskapsbibliotekLenkeDto[]>('/api/kunnskapsbibliotek/lenker'),

  leggTilKunnskapsbibliotekLenke: (request: LeggTilLenkeRequest) =>
    kall<KunnskapsbibliotekLenkeDto>('/api/kunnskapsbibliotek/lenker', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  slettKunnskapsbibliotekLenke: (id: string) =>
    kall<void>(`/api/kunnskapsbibliotek/lenker/${id}`, { method: 'DELETE' }),

  hentKunnskapsbibliotekFiler: () => kall<KunnskapsbibliotekFilDto[]>('/api/kunnskapsbibliotek/filer'),

  lastOppKunnskapsbibliotekFil: (fil: File, tittel?: string) => {
    const skjema = new FormData();
    skjema.append('fil', fil);
    if (tittel?.trim()) skjema.append('tittel', tittel.trim());
    return kall<KunnskapsbibliotekFilDto>('/api/kunnskapsbibliotek/filer', { method: 'POST', body: skjema });
  },

  slettKunnskapsbibliotekFil: (id: string) =>
    kall<void>(`/api/kunnskapsbibliotek/filer/${id}`, { method: 'DELETE' }),

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

  // ---------- Import-wizard (2026-08-28) — modelleksport-JSON → ekte tjenester/handlinger ----------

  importerRettighet: (malVirksomhetId: string, request: ImportRettighetRequest) =>
    kall<TjenesteDto>(`/api/import/${malVirksomhetId}/rettigheter`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  // ---------- Tjenestereise-graf (2026-08-28) — multi-hop, interaktiv visualisering ----------

  hentTjenestereiseGraf: (id: string, valg: { dybde: number; inkluderHandlinger: boolean; livshendelse: string | null }) => {
    const sok = new URLSearchParams({ dybde: String(valg.dybde), inkluderHandlinger: String(valg.inkluderHandlinger) });
    if (valg.livshendelse) sok.set('livshendelse', valg.livshendelse);
    return kall<AvhengighetsgrafDto>(`/api/tjenester/${id}/avhengighetsgraf?${sok.toString()}`);
  },

  hentDistinkteLivshendelser: () => kall<string[]>('/api/tjenester/livshendelser'),

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

  /** Ekte reverse-oppslag — rettskilde-noder som faktisk NEVNER begrepets Term i lovteksten (ikke det
   * samme som BegrepDto.lovreferanseEid, se BegrepBruktIRettskilderTjeneste på serveren). */
  hentBegrepBruktIRettskilder: (id: string) => kall<BegrepBruktIRettskildeDto[]>(`/api/begreper/${id}/brukt-i-rettskilder`),

  // ---------- «Identifiser begrep» (byggesteg 5 runde 1, docs/06-veikart.md) — stub-KI ----------

  hentBegrepsforslagKo: () => kall<BegrepsforslagDto[]>('/api/begreper/forslag'),

  kjorBegrepsforslag: (request: KjorForslagRequest) =>
    kall<KjorForslagResponsDto<BegrepDto>>('/api/begreper/forslag/kjor', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  /** Massegodkjenning/-avvisning av begrepsforslag — samme mønster som tjenesteforslag-batchen over. */
  godkjennBegrepsforslagBatch: (request: BegrepsforslagBatchRequest) =>
    kall<BegrepsforslagBatchResultatDto>('/api/begreper/forslag/godkjenn-batch', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  avvisBegrepsforslagBatch: (request: BegrepsforslagBatchRequest) =>
    kall<BegrepsforslagBatchResultatDto>('/api/begreper/forslag/avvis-batch', {
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

  // ---------- Handlinger (2026-08-20) — konkrete handlinger tilknyttet en Rettighet (Tjeneste) ----------

  /** Toppnivå-listen (2026-08-22) — ALLE handlinger tvers av ALLE tjenester, ett kall. */
  hentAlleHandlinger: () => kall<HandlingMedTjenesteDto[]>('/api/handlinger'),

  hentHandlinger: (tjenesteId: string) => kall<HandlingDto[]>(`/api/tjenester/${tjenesteId}/handlinger`),

  hentHandling: (handlingId: string) => kall<HandlingDto>(`/api/tjenester/handlinger/${handlingId}`),

  opprettHandling: (tjenesteId: string, request: HandlingRequest) =>
    kall<HandlingDto>(`/api/tjenester/${tjenesteId}/handlinger`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  oppdaterHandling: (handlingId: string, request: HandlingRequest) =>
    kall<HandlingDto>(`/api/tjenester/handlinger/${handlingId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  slettHandling: (handlingId: string) =>
    kall<void>(`/api/tjenester/handlinger/${handlingId}`, { method: 'DELETE' }),

  settHandlingStatus: (handlingId: string, request: SettStatusRequest) =>
    kall<HandlingDto>(`/api/tjenester/handlinger/${handlingId}/status`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  settHandlingRotnode: (handlingId: string, request: SettRotnodeRequest) =>
    kall<HandlingDto>(`/api/tjenester/handlinger/${handlingId}/rotnode`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  // ---------- Delte handlings-koblinger (2026-08-27, Tjenestedetalj-redesignrunden) ----------
  // "Koble eksisterende handling" — se HandlingTjenesteEntitet på serveren.

  /** Søker blant EGEN virksomhets handlinger (kandidatlisten for «koble eksisterende handling») — IKKE åpen tvers av virksomheter. */
  sokHandlingRegister: (sok: string) =>
    kall<HandlingDto[]>(`/api/tjenester/handlinger/register?sok=${encodeURIComponent(sok)}`),

  kobleHandlingTilTjeneste: (tjenesteId: string, request: KobleHandlingRequest) =>
    kall<HandlingTjenesteDto>(`/api/tjenester/${tjenesteId}/handlinger/koble`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  fjernHandlingTjenesteKobling: (koblingId: string) =>
    kall<void>(`/api/tjenester/handlinger/koblinger/${koblingId}`, { method: 'DELETE' }),

  hentHandlingRegelverksreferanser: (handlingId: string) =>
    kall<HandlingRegelverksreferanseDto[]>(`/api/tjenester/handlinger/${handlingId}/regelverksreferanser`),

  flyttHandlingTilTjeneste: (handlingId: string, tjenesteId: string) =>
    kall<HandlingDto>(`/api/tjenester/handlinger/${handlingId}/flytt-til-tjeneste`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ tjenesteId }),
    }),
};
