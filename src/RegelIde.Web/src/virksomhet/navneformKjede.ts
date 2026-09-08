/**
 * navneformKjede
 * ------------------------------------------------------------------
 * [Ny, tagg-synlig-runden, 2026-09-08] Hvilket navn som er HOVEDLEDDET når en virksomhet-tagg
 * resolves til noe menneskelesbart.
 *
 * <p><b>Feilen dette retter.</b> Taggraden viste
 * `«Karasjok» → [Kortform] → Karasjoga gielda / Karasjok kommune`, altså rett fra den tagget
 * kortformen til virksomhetens offisielle, tospråklige REGISTERNAVN. Johanns innvending: mellomleddet
 * skal være en NAVNEFORM — `«Karasjok» → «Karasjok kommune» → virksomheten`. Registernavnet er en
 * opplysning om virksomheten i Enhetsregisteret, ikke det leddet en saksbehandler leser kjeden
 * gjennom.</p>
 *
 * <p><b>Hvorfor kjeden løses HER, i visningen, og ikke i datamodellen.</b> Det finnes bevisst INGEN
 * navneform→navneform-FK: begge navneformene («Karasjok» med grunn `kortform` og «Karasjok kommune»
 * med grunn `gjeldende`) peker på SAMME virksomhet, og mellomleddet finnes ved å slå opp den
 * `gjeldende` navneformen for den virksomheten. Det er et dokumentert valg, tatt fordi alternativet
 * koster en migrasjon, et nytt felt å holde konsistent, og en ny syklusrisiko (navneform A → B → A)
 * — uten å gi noen opplysning modellen ikke allerede har. Skulle en virksomhet en dag trenge FLERE
 * gjeldende navneformer (f.eks. én per målform), er DET tidspunktet å revurdere valget på, ikke nå.</p>
 *
 * <p><b>Ingen gjettet fallback.</b> Finnes ingen `gjeldende` navneform, faller hovedleddet tilbake
 * til virksomhetens registernavn — det som FAKTISK finnes — og aldri til et navn utledet av
 * kortformen (f.eks. «Karasjok» + « kommune»). Samme «ikke funnet ≠ oppfunnet»-holdning som
 * datalaget.</p>
 */
import type { BegrepDto } from '../api/types';

/** Hva kjeden skal vises som. `hovedledd` er lenketeksten; `registernavn` er opplysningen som er
 *  flyttet til hover fordi den ikke skal være hovedleddet (se `finnHovedledd`). */
export interface NavneformKjede {
  hovedledd: string;
  /** Satt bare når registernavnet er noe ANNET enn hovedleddet — ellers ville hoveret gjentatt
   *  lenketeksten, som er ren støy. */
  registernavn?: string;
}

/**
 * Hovedleddet for en virksomhet-tagg: den `gjeldende` navneformen for samme virksomhet hvis den
 * finnes, ellers virksomhetens registernavn.
 *
 * @param virksomhetId Virksomheten navneformen peker på.
 * @param alleNavneformer Alle kjente navneformer (begrep med kategori `'virksomhet'`). Søkes for en
 *   rad med samme `virksomhetReferanseId` og `navneformgrunn === 'gjeldende'`.
 * @param registernavn Virksomhetens `navn` slik det står i katalogen, eller `undefined` når
 *   virksomheten ikke er lastet.
 * @returns `undefined` bare når det ikke finnes NOE navn å vise — da har kalleren ingenting sant å
 *   vise, og skal ikke finne på noe.
 */
export function finnHovedledd(
  virksomhetId: string,
  alleNavneformer: readonly BegrepDto[],
  registernavn: string | undefined,
): NavneformKjede | undefined {
  const gjeldende = alleNavneformer.find(
    (b) => b.virksomhetReferanseId === virksomhetId && b.navneformgrunn === 'gjeldende',
  );
  const hovedledd = gjeldende?.term ?? registernavn;
  if (!hovedledd) return undefined;
  return {
    hovedledd,
    registernavn: registernavn && registernavn !== hovedledd ? registernavn : undefined,
  };
}
