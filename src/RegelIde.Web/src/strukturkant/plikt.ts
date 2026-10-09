import type {
  Aktortype, Modalitet, PliktGrunnlag, PliktMotpartStatus, PliktRetning, RelasjonsTypeKonfigurasjonDto,
} from '../api/types';

/**
 * [Ny, issue #353 «plikt overfor motpart og ordning», 2026-10-09] Ren logikk for P-kantene og ordningen — skilt ut fra
 * komponentene så den kan testes (plikt.test.ts), samme mønster som `paragrafEtikett.ts`/`departementLenke.ts`.
 *
 * Fargerollene og tekstene her er docs/09 §34. Ingenting her gjetter en verdi: en ukjent verdi vises rå, et tomt valg er NULL
 * (CLAUDE.md §8).
 */

/** Modalitetene i fast rekkefølge, med teksten skjemaet viser. Speilet av `Strukturkanter.Modaliteter`. */
export const MODALITETER: readonly { verdi: Modalitet; tekst: string }[] = [
  { verdi: 'skal', tekst: 'Skal' },
  { verdi: 'kan', tekst: 'Kan' },
  { verdi: 'bor', tekst: 'Bør' },
];

/** Modaliteten slik den LESES — «bor» lagres uten ø (som de andre lukkede vokabularene) og vises «bør». Speiler
 * `StrukturkantTjeneste.Modalitetsord`; null = ikke angitt. En ukjent verdi returneres rå. */
export function modalitetsord(modalitet: string | null | undefined): string | null {
  if (!modalitet) return null;
  return modalitet === 'bor' ? 'bør' : modalitet;
}

/** Verdien fra «Modalitet»-velgeren til request-feltet: '' (Ikke angitt) og alt utenfor den lukkede lista blir null —
 * aldri en standardverdi (CLAUDE.md §8). */
export function modalitetFraValg(valg: string): Modalitet | null {
  return MODALITETER.some((m) => m.verdi === valg) ? (valg as Modalitet) : null;
}

/** Teksten i typevelgeren for en P-type: «Plikt — «har samarbeidsplikt overfor motparten»». Samme form som K
 * («Kompetanse (familie) — «… overfor motparten»»), docs/09 §33/§34. Malen er «har samarbeidsplikt {0}», der {0} i
 * visningsteksten blir «(skal) overfor B». */
export function pliktValgtekst(t: Pick<RelasjonsTypeKonfigurasjonDto, 'fraVisningsmal'>): string {
  return `Plikt — «${t.fraVisningsmal.replace('{0}', 'overfor motparten')}»`;
}

/**
 * Hvilke R/K/P-typer «Legg til relasjon, kompetanse eller plikt» skal tilby, gitt aktørtypen til virksomheten skjemaet står
 * på (fra-noden). Speiler `Strukturkanter.OrdningLovSomFra`/`KreverOrdningSomFra` — serveren avviser resten uansett, men et
 * valg som alltid gir 400 skal ikke tilbys:
 * <ul>
 *   <li>En ordning er ikke en aktør: som fra-node bare P og R `forvaltes_av` (G `tilhorer` er ikke i dette skjemaet).</li>
 *   <li>R `forvaltes_av` KREVER en ordning som fra-node — skjules for alle andre, også uavklart aktørtype (den gjettes ikke).</li>
 * </ul>
 */
export function typerForAktortype<T extends Pick<RelasjonsTypeKonfigurasjonDto, 'kategori' | 'kode'>>(
  typer: readonly T[], aktortype: Aktortype | null,
): T[] {
  const erForvaltesAv = (t: T) => t.kategori === 'R' && t.kode === 'forvaltes_av';
  return aktortype === 'ordning'
    ? typer.filter((t) => t.kategori === 'P' || erForvaltesAv(t))
    : typer.filter((t) => !erForvaltesAv(t));
}

/** En ordning kan bare være til-node (motpart) i P — `Strukturkanter.OrdningLovSomTil`. */
export function ordningKanVaereMotpart(kategori: string | null): boolean {
  return kategori === 'P';
}

/** Grunnlaget for at en plikt gjelder kommunen (S6), i klartekst. Ukjent verdi vises rå. */
export function pliktGrunnlagTekst(grunnlag: PliktGrunnlag | string, klassenavn?: string): string {
  switch (grunnlag) {
    case 'direkte': return 'Direkte (kommunen selv)';
    case 'medlem_av': return klassenavn ? `Via medlemskap i «${klassenavn}»` : 'Via registrert medlemskap';
    case 'klasse_uten_registrert_medlemskap':
      return klassenavn ? `«${klassenavn}» — klasse uten registrert medlemskap` : 'Klasse uten registrert medlemskap';
    default: return grunnlag;
  }
}

/**
 * Statusmerket for motparten (S6) — samme fargeroller som tilhørigheten (docs/09 §32): løst (konkret/entydig) = ingen tag,
 * «Ikke entydig» = `warning` (flere kandidater, ingen valgt), «Mangler» og «Ikke angitt» = `neutral`. Grunnlaget
 * «klasse uten registrert medlemskap» er ikke en motpartsstatus og har sitt eget hull. Ukjent status vises rå som `neutral`.
 */
export function motpartStatusVisning(
  status: PliktMotpartStatus | string,
): { tekst: string; farge: 'warning' | 'neutral'; forklaring: string } | null {
  switch (status) {
    case 'konkret':
    case 'entydig':
      return null;
    case 'ikke_entydig':
      return { tekst: 'Ikke entydig', farge: 'warning', forklaring: 'Flere kandidater — ingen er valgt.' };
    case 'mangler':
      return { tekst: 'Mangler', farge: 'neutral', forklaring: 'Strukturen gir ikke et svar (se hullet).' };
    case 'ikke_angitt':
      return { tekst: 'Ikke angitt', farge: 'neutral', forklaring: 'Teksten sier ikke hvem motparten er.' };
    default:
      return { tekst: status, farge: 'neutral', forklaring: 'Ukjent status fra API-et — vist rått.' };
  }
}

/** [Ny, #353-retting] Retningen sett fra kommunen (S6): «Kommunen skal» eller «Overfor kommunen». Ukjent verdi vises rå. */
export function pliktRetningTekst(retning: PliktRetning | string): string {
  switch (retning) {
    case 'kommunen_skal': return 'Kommunen skal';
    case 'overfor_kommunen': return 'Overfor kommunen';
    default: return retning;
  }
}
