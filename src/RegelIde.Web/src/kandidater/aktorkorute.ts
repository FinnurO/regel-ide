import type { Aktorkorute } from '../api/types';

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
