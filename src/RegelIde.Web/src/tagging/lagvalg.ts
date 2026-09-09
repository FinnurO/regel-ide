/**
 * lagvalg
 * ------------------------------------------------------------------
 * [Ny, tagg-synlig-runden, 2026-09-08] Hvilket TAGG-LAG `TagTekst` viser. Skilt ut fra
 * TagTekst.tsx som en ren, avhengighetsfri modul: regelen under er den ene som avgjorde om Johann
 * så noe som helst markert i lovteksten, og den skal kunne testes uten å rendre React eller
 * Designsystemet.
 */
import type { TagKind, TagKindId, TextTag } from './TagTekst';

/**
 * [Ny, tagg-synlig-runden, 2026-09-08] Hvilket lag som skal vises når brukeren IKKE har valgt selv.
 *
 * <p><b>Feilen dette retter.</b> Aktivt lag defaultet til `kinds[0]` («Begrep») uten å se på hvilke
 * lag noden faktisk HAR tagger i. Forskrift 2005-06-17-657 § 1 ledd-1 har kun `virksomhet`-tagger,
 * så siden åpnet med Begrep aktivt: 14 taggrader listet under teksten, men teksten helt umarkert.
 * For en leser ser det ut som taggingen ikke virker — man ser ikke at det bare er et annet lag som
 * vises. Defaulten skal derfor følge DATAENE på noden som vises, ikke rekkefølgen i konfigurasjonen.</p>
 *
 * <p><b>[ENDRET, konfidens-runden, 2026-09-09] Velger laget med FLEST tagger på noden</b>, ikke det
 * første i `kinds`-rekkefølgen som har minst én. Grunn: da gruppebegrepene ble tagget i forskrift
 * 2005-06-17-657 § 1 ledd-1 fikk noden 7 `begrep`-tagger ved siden av 14 `virksomhet`-tagger, og
 * «første med minst én» valgte da Begrep — som skjulte de 14 kommunenavnene bak en fane. Regelen
 * skal vise MEST mulig av det som faktisk er markert på noden man åpner; det er hele hensikten med
 * å ha en default i det hele tatt.</p>
 *
 * <p>`kinds`-rekkefølgen (konfigurasjonens egen prioritering) bryter likhet ved samme antall, og er
 * fortsatt fallback når noden ikke har tagger i det hele tatt: da finnes det ingen markering å vise
 * uansett, og et vilkårlig annet lag ville ikke vært bedre.</p>
 *
 * <p>Dette er BARE en default. Brukerens eget lagvalg overstyrer den og skal aldri overskrives —
 * derfor er `null` («ikke valgt») en egen tilstand i komponenten, og ikke `kinds[0]`.</p>
 */
export function finnStandardLag(tags: TextTag[], kinds: TagKind[]): TagKindId | undefined {
  let beste: { id: TagKindId; antall: number } | undefined;
  for (const k of kinds) {
    const antall = tags.filter((t) => t.kind === k.id).length;
    // Streng >: første lag i kinds-rekkefølgen vinner ved likhet.
    if (antall > 0 && (beste === undefined || antall > beste.antall)) {
      beste = { id: k.id, antall };
    }
  }
  return beste?.id ?? kinds[0]?.id;
}

/**
 * [Ny, tagg-synlig-runden, 2026-09-08] Hvilket lag som faktisk vises: brukerens eget valg hvis det
 * finnes, ellers `finnStandardLag`.
 *
 * <p>Skilt ut som en egen, ren funksjon nettopp fordi PRESEDENSEN er det som må holdes i hevd:
 * defaultingen skal aldri overta for et manuelt lagvalg, heller ikke når brukeren velger et lag
 * som ikke har tagger på noden (da er tom markering det RIKTIGE svaret — brukeren spurte om det
 * laget). En `null`/tom `brukervalg` betyr «ikke valgt ennå», ikke «valgte ingenting».</p>
 */
export function velgAktivtLag(
  brukervalg: TagKindId | null | undefined,
  tags: TextTag[],
  kinds: TagKind[],
): TagKindId | undefined {
  return (brukervalg || null) ?? finnStandardLag(tags, kinds);
}
