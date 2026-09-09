/**
 * [Ny, punktliste-runden, 2026-09-09, issue #213] Definisjonens punktliste — de reglene som avgjør
 * HVA som vises når man åpner et ledd med underordnede punkt-noder.
 *
 * <p><b>Feilen som motiverte modulen.</b> `RettskildeDetalj` rendret bare den valgte nodens egen
 * `Tekst`. Åpner man merverdiavgiftsforskriften § 1-3-1 ledd-1 sto det «(1) Med personkjøretøy
 * menes» — og så ingenting. Selve definisjonen, punkt 1–8, lå som egne noder i basen (verifisert
 * live mot `GET /api/rettskilder/{id}/noder` 2026-09-09) men var borte fra skjermen. Det treffer
 * kjernen i `docs/32` §1: en avkuttet definisjon SER komplett ut, og modelløren som leser den får et
 * galt bilde av hva loven sier.</p>
 *
 * <p>Modulen er bevisst REN (ingen React, ingen DTO-import — se `docs/09` §17): reglene her er verdt
 * å teste, og en test skal ikke kreve et rendringsoppsett. Nodeformen er derfor et STRUKTURELT
 * minimum ({@link PunktNode}), som `RettskildeDetalj`s eget `TreNode` tilfredsstiller uten
 * konvertering.</p>
 */

/**
 * Det minste en node må ha for at reglene under skal kunne brukes på den — strukturelt matchet av
 * `RettskildeDetalj`s interne `TreNode` (som er `RettskildeNodeDto` + `barn`). Bevisst ikke
 * `RettskildeNodeDto` selv: da måtte modulen importert `api/types`, og barn-relasjonen finnes ikke
 * på DTO-en i det hele tatt (den bygges i visningen av `parentNodeId`).
 */
export interface PunktNode {
  eid: string;
  nodeType: string;
  nummer: string | null;
  tekst: string | null;
  barn: readonly PunktNode[];
}

/** Ett punkt klart til visning, med sine egne underpunkter (punkt-i-punkt finnes i ekte data). */
export interface PunktVisning {
  /** Punktnodens EGEN eId — det er den en tagg i punktet skal lagres mot, ikke leddets. */
  eid: string;
  /**
   * Listemerket som vises. Dette er punktnodens `nummer`, som Lovdata-importen setter til punktets
   * POSISJON i lista (`punktIndeks` i `LovdataHtmlParser.ParseEnListe`), ikke til lovens egen
   * listemerking.
   *
   * <p><b>[LÅST] Merket er vår posisjon, ikke lovens bokstav — og det skal sies høyt i visningen.</b>
   * Lovdatas HTML har den ekte merkingen i `<li data-name="a.">`, og en opptelling over alle åtte
   * råfixturene i `data/kilder/raw-lovdata` (2026-09-09) viser at DEN VANLIGSTE formen er bokstaver:
   * 554 av 891 `<li>` har `data-name="a."`/`"b."`/… mot 285 med `"1."`/`"2."`/… (52 andre, mest
   * `"-"`). Importen leser ikke `data-name` i det hele tatt, så feltet finnes ikke i basen. Å rendre «1.» der loven sier «a.»
   * ville vært nøyaktig feilen `docs/09` §18 dokumenterer for paragrafnummer: en etikett som ser
   * riktig ut, men er feil, er verre enn en rå identifikator. Kalleren viser derfor merket sammen med
   * en synlig fotnote om hva det ER (se `PUNKTMERKE_FORKLARING`), og en skjemaendring som bærer
   * lovens egen merking er rapportert som funn — den eies ikke av denne runden.</p>
   */
  merke: string;
  tekst: string;
  punkter: PunktVisning[];
}

/**
 * Teksten som må stå ved en punktliste for at leseren skal vite hva listemerkene er. Ligger her, ved
 * regelen som produserer merkene, i stedet for som en løs streng i `RettskildeDetalj` — samme
 * «forklaringen bor sammen med det den forklarer»-linje som `KandidatflytForklaring` (`docs/09` §19).
 */
export const PUNKTMERKE_FORKLARING =
  'Listemerkene er punktnummeret i eId-en (punkt-1, punkt-2 …), altså punktets posisjon i lista. ' +
  'Lovdatas egen merking (ofte «a.», «b.» …) importeres ikke, og er derfor ikke vist.';

/**
 * Punktbarna til en node, i dokumentrekkefølge, rekursivt.
 *
 * <p>Rekkefølgen er IKKE sortert her: `GET /api/rettskilder/{id}/noder` leverer nodene sortert på
 * `Sorteringsrekkefolge` (se `RettskildeRepository.NoderForAsync`), og visningen bygger `barn` i den
 * rekkefølgen den mottar dem. En sortering på `nummer` her ville vært en STRENG-sortering («10» før
 * «2») og dermed gjort rekkefølgen dårligere, ikke bedre.</p>
 *
 * <p>Punkt-i-punkt er bekreftet ekte (alkoholforskriften § 6-2 har gebyrsatser som underpunkter,
 * § 14-3 punkt 14 har tekst + underliste), derfor rekursjonen framfor ett nivå.</p>
 *
 * <p>Et punkt UTEN egen tekst hoppes over sammen med sine barn: det finnes ingenting å vise for det,
 * og et tomt listeelement med et nummer ville påstått at loven har et punkt uten innhold.</p>
 */
export function underordnedePunkter(node: PunktNode): PunktVisning[] {
  return node.barn
    .filter((barn) => barn.nodeType === 'punkt' && barn.tekst != null && barn.tekst.trim() !== '')
    .map((barn) => ({
      eid: barn.eid,
      merke: barn.nummer ?? sisteEidSegment(barn.eid),
      tekst: barn.tekst!,
      punkter: underordnedePunkter(barn),
    }));
}

/** Siste eId-segment («punkt-3»), som merke-fallback for en punktnode uten `nummer`. */
function sisteEidSegment(eid: string): string {
  return eid.slice(eid.lastIndexOf('/') + 1);
}

/**
 * Tegn som kan stå ETTER et setningssluttende punktum uten at setningen fortsetter — sluttsitattegn
 * og lukkeparenteser. «… innrettet for persontransport.»» skal leses som avsluttet.
 */
const AVSLUTTENDE_TEGN = /[»"'’”)\]\s]+$/u;

/**
 * <b>Er en del av nodens egen tekst tekst som hører ETTER punktlisten?</b>
 *
 * <p>Bakgrunnen er den verre varianten av issue #213: merverdiavgiftsforskriften § 1-3-2 ledd-2 har
 * punktlisten MIDT i setningen, og leddets egen `Tekst` blir da
 * «… I tillegg må fotografiet være Bilder fra ordinær fotografvirksomhet anses ikke som kunstneriske
 * fotografier.» — de fire vilkårene som hører mellom «være» og «Bilder» ligger som punkt-noder.
 * Teksten som vises er ikke bare ufullstendig, den sier noe ANNET enn loven.</p>
 *
 * <p><b>Hvorfor skjøten ikke kan finnes, bare påvises.</b> `LovdataHtmlParser.HentSegmenter` setter
 * inn ETT mellomrom der `<ul>`/`<ol>` sto, og `LeggTilLeddEllerPunktNode` kollapser deretter
 * whitespace til nodens `Tekst`. Selve posisjonen finnes altså i parserens `Segmenter`-liste, men
 * `Segmenter` lagres ikke — `RettskildeNodeEntitet` har bare `Tekst`. Frontend har derfor ingen måte
 * å vite HVOR skjøten er, og §8 i CLAUDE.md er utvetydig: da skal den ikke gjettes. Å dele teksten
 * på «siste punktum før en stor forbokstav» ville truffet i noen av tilfellene og skjøvet halve
 * setninger feil vei i resten.</p>
 *
 * <p><b>Slutningen som brukes i stedet, og hvorfor den er en slutning og ikke en gjetning.</b> En
 * innledning som står RETT FØR en liste er aldri en avsluttet setning — den ender på «menes», «for
 * følgende:», «herunder om:». Slutter nodens egen tekst likevel på punktum/utropstegn/spørsmålstegn
 * mens noden HAR punktbarn, må den inneholde noe mer enn innledningen. Konklusjonen er bevisst svak:
 * «det står tekst her som ikke hører rett før lista, og vi vet ikke hvor skjøten er» — en synlig
 * innrømmelse av uvisshet, ikke en påstand om innhold. Det er nøyaktig det akseptansekriterium 4 i
 * issue #213 ber om: «Klarer vi ikke å rekonstruere setningsflyten, skal det være SYNLIG at noe står
 * imellom — ikke stille sammenskjøtet.»</p>
 *
 * <p><b>Målt, ikke antatt.</b> Kjørt mot dev-basen 2026-09-09 over åtte dokumenter
 * (merverdiavgiftsforskriften, forvaltningsloven, plan- og bygningsloven, alkoholforskriften,
 * merverdiavgiftsloven, arbeidsmiljøloven, kommuneloven, barnehageloven): 285 noder har punktbarn og
 * egen tekst, 24 av dem slår ut på regelen. Alle 24 ble inspisert (start + slutt av leddteksten mot
 * punktlista), 8 av dem lest i sin helhet, og hver enkelt viste en ekte sammenskjøting
 * («… dersom følgende vilkår er oppfylt Formidleren er likevel ansvarlig …», «… inneholde følgende
 * opplysninger Returer av varer …»). Ingen falske positive observert. En falsk
 * positiv koster dessuten bare en fotnote for mye, aldri en gal påstand om lovens innhold.</p>
 */
export function harTekstEtterListen(tekst: string | null | undefined, antallPunkter: number): boolean {
  if (antallPunkter === 0 || !tekst) return false;
  const uten = tekst.trim().replace(AVSLUTTENDE_TEGN, '');
  return /[.!?]$/.test(uten);
}
