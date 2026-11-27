import type { StrukturkantDto } from '../api/types';

/**
 * [Ny, issue #313] Ren logikk for forslagskøen for strukturkanter (`Strukturforslagsko.tsx`), skilt ut så den kan testes uten DOM.
 */

/** Tilliten docs/33 §5.5 gir et forslag ut fra laget og typen. `null` = normal (ingen lapp). Mønsterlaget er normal; KI er lav, unntatt
 *  forskriftskompetanse (normal) og tilsynskompetanse (middels). En KLASSIFISERING, ikke en avgjørelse (docs/09 §21). */
export function tillitFor(kant: Pick<StrukturkantDto, 'oppdagelsesKilde' | 'kategori' | 'typekode' | 'normform'>): 'middels' | 'lav' | null {
  if (!kant.oppdagelsesKilde.startsWith('ki:')) return null;
  if (kant.kategori === 'K' && kant.typekode === 'normgivning' && kant.normform === 'forskrift') return null;
  if (kant.kategori === 'K' && kant.typekode === 'tilsyn') return 'middels';
  return 'lav';
}

export interface Hjemmelgruppe {
  hjemmelRettskildeId: string | null;
  tittel: string;
  kanter: StrukturkantDto[];
}

/**
 * [Ny, issue #517 PR 3] Filteret fra lenkene på «Til vurdering»: `?type=etterfolger` (typekoden) og `?hjemmel=id,id` (hjemmelens rettskilde,
 * pilotens kilder). Tomt filter gir alle forslagene.
 */
export interface Forslagsfilter {
  type: string | null;
  hjemler: string[];
}

export function forslagsfilterFra(parametre: URLSearchParams): Forslagsfilter {
  const type = parametre.get('type')?.trim() || null;
  const hjemler = (parametre.get('hjemmel') ?? '').split(',').map((x) => x.trim()).filter((x) => x.length > 0);
  return { type, hjemler };
}

export function filtrerForslag<T extends Pick<StrukturkantDto, 'typekode' | 'hjemmelRettskildeId'>>(kanter: T[], f: Forslagsfilter): T[] {
  if (!f.type && f.hjemler.length === 0) return kanter;
  const hjemler = new Set(f.hjemler);
  return kanter.filter((k) => (!f.type || k.typekode === f.type)
    && (hjemler.size === 0 || (k.hjemmelRettskildeId !== null && hjemler.has(k.hjemmelRettskildeId))));
}

/** «Godkjenn alle N» og «Avvis alle N» i en hjemmelgruppe virker på alle forslagene med hjemmelen i API-et. Med et typefilter er gruppen bare
 *  en del av dem, så knappene står ikke: tallet på knappen ville ikke vært det knappen gjør. Et hjemmelfilter tar med hele grupper. */
export const gruppehandlingerTillatt = (f: Forslagsfilter) => !f.type;

/** Forslagene gruppert per hjemmel (rettskilde), flest først; kanter uten hjemmel i korpus samles sist. */
export function grupperPerHjemmel(kanter: StrukturkantDto[]): Hjemmelgruppe[] {
  const grupper = new Map<string, Hjemmelgruppe>();
  for (const k of kanter) {
    const nokkel = k.hjemmelRettskildeId ?? '';
    const g = grupper.get(nokkel) ?? {
      hjemmelRettskildeId: k.hjemmelRettskildeId,
      tittel: k.hjemmelRettskildeTittel ?? (k.hjemmelRettskildeId ? 'Rettskilde' : 'Uten hjemmel i korpus'),
      kanter: [],
    };
    g.kanter.push(k);
    grupper.set(nokkel, g);
  }
  return [...grupper.values()].sort((a, b) =>
    (a.hjemmelRettskildeId === null ? 1 : 0) - (b.hjemmelRettskildeId === null ? 1 : 0)
    || b.kanter.length - a.kanter.length
    || a.tittel.localeCompare(b.tittel, 'nb'));
}

/** «Avvist av Kari Jurist 15.10.2026: begrunnelse» — hvem, når og hvorfor (#374 AC1/AC3). */
export function avvisningstekst(kant: Pick<StrukturkantDto, 'sistEndretAv' | 'sistEndretTidspunkt' | 'avvisningBegrunnelse'>): string {
  const dato = kant.sistEndretTidspunkt ? new Date(kant.sistEndretTidspunkt).toLocaleDateString('nb-NO') : 'ukjent tidspunkt';
  return `Avvist av ${kant.sistEndretAv ?? 'ukjent'} (menneske) ${dato}${kant.avvisningBegrunnelse ? `: ${kant.avvisningBegrunnelse}` : ''}`;
}

/** Visningsnavn for utfallene i lasterapporten. */
export const UTFALL_VISNING: Record<string, string> = {
  ny: 'Nye forslag',
  fantes_som_forslag: 'Fantes alt som forslag',
  fantes_godkjent: 'Fantes alt godkjent',
  tidligere_avvist: 'Tidligere avvist',
  dublett_i_kjoringen: 'Dublett i kjøringen',
  ikke_lastet: 'Ikke lastet',
};

/** [Ny, issue #378 A1] Aktørstatusene i aktørsveipet og lasterapporten — tekst for visning. */
export const AKTORSTATUS_VISNING: Record<string, string> = {
  kjent: 'Kjent',
  ny_navneform_for_kjent_aktor: 'Ny navneform for kjent aktør (kandidat)',
  ny_aktor: 'Ny aktør',
  generisk: 'Generisk, ikke bundet',
  flertydig: 'Flertydig',
};
