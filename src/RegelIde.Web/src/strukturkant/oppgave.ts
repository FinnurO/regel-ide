import type { RelasjonsTypeKonfigurasjonDto } from '../api/types';

/**
 * [Ny, issue #368 «oppgave/funksjon», 2026-10-15] Hjelpere for kategorien F i «Legg til relasjon, kompetanse, plikt eller oppgave»
 * (docs/09 §37, begrepsmanualen docs/profil/02 §F).
 *
 * En oppgave gir aldri K eller P i seg selv. Skjemaet har derfor ingen «overfor» for F: det oppgaven gjelder (område, klasse eller
 * aktør) er valgfritt, og OPPGAVEN selv (objektet) er påkrevd — samme krav som serveren (CHECK ck_strukturkanter_oppgave_objekt).
 */

/** Teksten i typevelgeren for en F-type: «Oppgave — «har samordningsoppgave …»». Malen er «har samordningsoppgave {0}», der
 * {0} i visningsteksten blir «(skal) beredskapsarbeid — gjelder B». */
export function oppgaveValgtekst(t: Pick<RelasjonsTypeKonfigurasjonDto, 'fraVisningsmal'>): string {
  return `Oppgave — «${t.fraVisningsmal.replace('{0}', '…').trim()}»`;
}

/** Kategoriene modaliteten (skal/kan/bør) hører til — speilet av `Strukturkanter.KategorierMedModalitet`. */
export function harModalitet(kategori: string | null): boolean {
  return kategori === 'P' || kategori === 'F';
}

/** Kan skjemaet sendes, sett fra motpart/objekt? R/K: motparten er påkrevd. P: motpart ELLER objekt/hjemmel-eId. F: objektet
 * (oppgaven) er alltid påkrevd, det oppgaven gjelder er valgfritt. */
export function harMotpartEllerHva(kategori: string | null, motpart: string, objekt: string, hjemmelEid: string): boolean {
  if (kategori === 'F') return !!objekt.trim();
  if (motpart) return true;
  return kategori === 'P' && (!!objekt.trim() || !!hjemmelEid.trim());
}
