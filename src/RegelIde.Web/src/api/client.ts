import type {
  ApiFeil,
  BrukerDto,
  OpprettTekstTaggRequest,
  RettskildeDetalj,
  RettskildeNodeDto,
  RettskildeReferanseDto,
  RettskildeSammendrag,
  TekstTaggDto,
  VirksomhetDto,
} from './types';

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5187';
const BRUKER_ID_LAGRINGSNOKKEL = 'regelide.brukerId';

export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
  ) {
    super(message);
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

  hentTagger: (rettskildeId: string) => kall<TekstTaggDto[]>(`/api/rettskilder/${rettskildeId}/tagger`),

  opprettTagg: (rettskildeId: string, request: OpprettTekstTaggRequest) =>
    kall<TekstTaggDto>(`/api/rettskilder/${rettskildeId}/tagger`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }),

  slettTagg: (rettskildeId: string, taggId: string) =>
    kall<void>(`/api/rettskilder/${rettskildeId}/tagger/${taggId}`, { method: 'DELETE' }),

  hentBrukere: () => kall<BrukerDto[]>('/api/brukere'),

  hentVirksomheter: () => kall<VirksomhetDto[]>('/api/virksomheter'),

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
};
