import type { Aktorkorute, Aktorstatus } from '../api/types';

/**
 * [Ny, issue #440 PR 1] Én delt kilde for rutene i aktørkøen (docs/09 §50): navnet på hver rute, og setningen sveipene viser om treffene
 * aktørkøens ruting sendte et annet sted. Brukt av aktørkøen, navnekandidatsveipet (historikklista og rettskildesiden), forslagskøen
 * (aktørsveipet) og KI-oppdagelsen.
 */
export const AKTORKORUTE_TEKST: Record<Aktorkorute, string> = {
  generisk: 'Generisk ord',
  kjent_aktor: 'Kjent aktør',
  kjent_begrep: 'Kjent klasse, rolle eller område',
  boyningsform: 'Bøyningsform av et kjent ord',
  aktorko: 'Aktørkøen',
};

const REKKEFOLGE: Aktorkorute[] = ['generisk', 'kjent_aktor', 'kjent_begrep', 'boyningsform'];

/**
 * «12 treff er kjente ord og ble ikke lagt i køen: generisk ord 8, kjent aktør 4.» Null når sveipet ikke sendte noe et annet sted, så
 * kalleren ikke viser en tom setning.
 */
export function utenforAktorkoenTekst(utenfor: Partial<Record<Aktorkorute, number>> | null | undefined): string | null {
  if (!utenfor) return null;
  const deler = REKKEFOLGE.filter((r) => (utenfor[r] ?? 0) > 0).map((r) => `${AKTORKORUTE_TEKST[r].toLowerCase()} ${utenfor[r]}`);
  if (deler.length === 0) return null;
  const sum = REKKEFOLGE.reduce((s, r) => s + (utenfor[r] ?? 0), 0);
  return `${sum} treff ${sum === 1 ? 'er et kjent ord' : 'er kjente ord'} og ble ikke lagt i køen: ${deler.join(', ')}.`;
}

/**
 * [Ny, issue #440, Johanns avgjørelse 2026-10-31] Om aktørsveipet legger raden i navnekandidatkøen — speiler
 * `AktorsveipRad.BlirNavnekandidat` i tjenesten. Ikke kjente og ikke flertydige, og bare når aktørkøens ruting ikke kjenner ordet, med ÉTT
 * unntak: et generisk ord uten binding («kommunen»). Da står «Behandle»-lenken til én-skjermen for generisk omtale (docs/09 §42) som før
 * #444. Alle andre kjente ord (kjent aktør, klasse/rolle/område, bøyningsform) holdes utenfor. Mangler ruten (eldre svar), gjelder regelen
 * fra før #440: alle som ikke er kjente eller flertydige.
 */
export function blirNavnekandidatIAktorsveipet(status: Aktorstatus, rute: Aktorkorute | null | undefined): boolean {
  if (status === 'kjent' || status === 'flertydig') return false;
  const r = rute ?? 'aktorko';
  return r === 'aktorko' || erGeneriskOrdUtenBinding(status, r);
}

/** [Ny, issue #440, Johanns avgjørelse 2026-10-31] Unntaket i `blirNavnekandidatIAktorsveipet`: likt ord som en generisk navneform, uten binding. */
export function erGeneriskOrdUtenBinding(status: Aktorstatus, rute: Aktorkorute | null | undefined): boolean {
  return status === 'generisk' && rute === 'generisk';
}
