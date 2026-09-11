/**
 * TagTekst
 * ------------------------------------------------------------------
 * Generell tekst-tagger: viser en tekstflate der brukeren kan markere
 * et ord/avsnitt og knytte det til en modell-entitet (begrep, vilkår,
 * regel, tjeneste — utvidbart). Taggene er posisjonsbaserte (tegn-offset).
 * Samme strekning kan bære flere tagger på tvers av lag (kinds), men bare
 * ett lag vises av gangen (radio) — så visningen holder seg ryddig.
 *
 * Brukes i rettskildevisningen, men er ikke bundet til AKN — «text» er
 * ren streng, og «kinds» konfigureres av forelder.
 *
 * DESIGNSYSTEMET-KOMPONENTER SOM BRUKES
 *   - ToggleGroup   → lag-velger (radio: ett lag vises av gangen)
 *   - Button        → tag-linjens handlinger, «Fjern»
 *   - Tag           → fargede markeringer + tagg-listen
 * Selve tekst-/markeringslogikken finnes IKKE i DS og er egen kode her.
 *
 * TAG-LINJE (ikke flytende meny): når bruker markerer tekst, aktiveres en fast
 * handlingslinje rett over teksten (steg 1: velg type, steg 2: ny/eksisterende).
 * Bevisst valg — ingen posisjonering mot viewport, ingen kollisjon, og linjen
 * er tastaturtilgjengelig i vanlig fokusrekkefølge.
 *
 * TOKENS: kun --ds-* (ingen egne farger). kind → semantisk rolle, konfigurerbart
 * via `kinds` (2026-07-25: hentes fra GET /api/konfigurasjon/tagg-kinds via
 * KonfigurasjonContext, ikke hardkodet — se RegelIde.Data/Entiteter.cs' egen
 * TaggKindKonfigurasjonEntitet-kommentar).
 *
 * Opprinnelig utkast fra Claude Design (2026-07-26, versjon 2 — erstatter en
 * tidligere flytende Dropdown-meny med denne faste tag-linjen); tilpasset her:
 *   - `ref` er `string | null`, ikke påkrevd — docs/06-veikart.md sier en tagg
 *     skal lagres med ref:null helt til byggesteg 2/4 gir den noe å peke på.
 *     Originalens `ref==='__new__'`-sentinel (åpne opprett-dialog for ny
 *     entitet) er derfor byttet med en direkte «Ny tagg»-handling som committer
 *     ref:null med én gang — det finnes ingen entitet å opprette ennå.
 */
import { useCallback, useMemo, useRef, useState, type CSSProperties, type ReactNode } from 'react';
import { Link as RouterLink } from 'react-router';
import { ToggleGroup, Tag, Button, Link } from '@digdir/designsystemet-react';
// [Ny, tagg-synlig-runden, 2026-09-08] Lagvalget ligger i en egen, ren modul — se lagvalg.ts.
import { velgAktivtLag } from './lagvalg';
import { Metatekst } from '../entitet/Metatekst';

/* ------------------------------ typer ------------------------------ */

export type TagKindId = string; // 'begrep' | 'tjeneste' | 'vilkar' | 'regel' | ...

export interface TagKind {
  id: TagKindId;
  label: string; // «Begrep»
  /** Designsystemet fargerolle for Tag/markering. */
  color: 'brand1' | 'brand2' | 'accent' | 'warning' | 'info' | 'success' | 'danger' | 'neutral';
}

export interface TextTag {
  id: string;
  start: number; // tegn-offset (inklusiv)
  end: number; // tegn-offset (eksklusiv)
  kind: TagKindId;
  /** Null inntil taggen er knyttet til en reell entitet (byggesteg 2/4). */
  ref: string | null;
  /** quoteSelector-relokering ved reimport fant ikke et entydig treff (2026-07-29) — se docs/05-arkitektur-og-nfk.md §3.1. */
  kreverGjennomgang?: boolean;
}

/** Kandidater for «knytt til eksisterende», gruppert per kind. Tom/utelatt i byggesteg 1. */
export type Registry = Record<TagKindId, Array<{ ref: string; label: string }>>;

export interface TagTekstProps {
  /** Ren tekst som skal vises og tagges. */
  text: string;
  /** Gjeldende tagger (kontrollert). */
  tags: TextTag[];
  /** Hvilke tagtyper som finnes. */
  kinds: TagKind[];
  /** Opprett ny tag. */
  /**
   * Opprett ny tag.
   *
   * <p>[ENDRET, punktliste-runden, 2026-09-09, issue #213] Andre argument er NØKKELEN til
   * tekstblokken markeringen ble gjort i: `null` for hovedteksten (`text`), ellers `nokkel` fra den
   * {@link Tekstblokk} det gjelder. Offsettene er ALLTID relative til DEN blokkens egen tekst.
   * Uten dette leddet ville en tagg i punkt 3 blitt lagret mot leddets eId og leddets tegnposisjoner
   * — altså en sporbarhetspåstand om et helt annet sted i teksten (akseptansekriterium 3 i #213).</p>
   */
  onTag: (t: { start: number; end: number; kind: TagKindId; ref: string | null }, blokkNokkel: string | null) => void;
  onRemoveTag: (id: string) => void;
  /** Kandidater for «knytt til eksisterende» — utelatt/tom i byggesteg 1. */
  registry?: Registry;
  /**
   * Kobler en ALLEREDE OPPRETTET tagg (ref===null) til en eksisterende entitet — byggesteg 2,
   * docs/06-veikart.md: låser opp TekstTaggEntitet.RefId. Til forskjell fra `registry`-kandidatene i
   * tag-linjen over (som gjelder NYE tagger, valgt idet man tagger), gjelder dette tagger som allerede
   * finnes i tagg-listen fra en tid før den tilhørende Begrep/Tjeneste-raden ble opprettet.
   */
  onLinkTag?: (tagId: string, ref: string) => void;
  /**
   * Utløser «opprett ny entitet fra dette utdraget» for en umerket tagg (2026-07-31,
   * docs/13-backlog.md §2.5) — til forskjell fra `onLinkTag` (koble til noe som ALLEREDE finnes),
   * dette er starten på en tekst-først-forfatterflyt: identifiser vilkåret i lovteksten, opprett det
   * derfra, koble taggen — uten at det samtidig plasseres i regelgrafen (et eget, senere steg).
   * Selve opprettelses-skjemaet (trenger f.eks. et Tjeneste-valg) er applikasjonsspesifikt og
   * rendres av forelder, ikke her — denne komponenten er bare utløseren.
   */
  onOpprettFraTag?: (tagId: string, kind: TagKindId) => void;
  /** Hvilke kinds `onOpprettFraTag`-knappen skal vises for. Utelatt/tom viser den ingen steder. */
  opprettFraTagKinds?: TagKindId[];
  /**
   * Slår opp en menneskelesbar lenke for en koblet tagg sin `ref` — 2026-07-30, fikser at
   * koblede tagger kun viste sin rå GUID/eId. `undefined` betyr «ingen lenke tilgjengelig ennå»
   * (f.eks. treffet mangler i den ferdiglastede listen), og faller da tilbake til rå tekst.
   *
   * <p>[UTVIDET, navneform-kjede-runden, 2026-09-08] `mellomledd` lar kalleren vise et LEDD MELLOM
   * sitatet og lenken, slik at tagg-listen kan vise hele kjeden i stedet for bare endepunktet:
   * `«Karasjok» → [Kortform] → Karasjoga gielda / Karasjok kommune`. Det er kalleren, ikke denne
   * komponenten, som vet hva mellomleddet ER — `TagTekst` er bevisst domene-agnostisk (den kjenner
   * ikke navneformer), og `NavneformgrunnTag` skal etter docs/09 §15 ha ÉN delt kilde, brukt av
   * kalleren. Derfor en ferdig `ReactNode` inn, ikke et navneform-felt.</p>
   *
   * <p>`mellomleddTekst` er den samme opplysningen som REN TEKST, til `title`-tooltipet på selve
   * markeringen i løpeteksten — et `title`-attributt kan ikke bære en `ReactNode`. Begge er
   * valgfrie; utelates de, ser visningen ut nøyaktig som før.</p>
   */
  resolveRef?: (
    kind: TagKindId,
    ref: string,
  ) => {
    label: string;
    href: string;
    mellomledd?: ReactNode;
    mellomleddTekst?: string;
    /**
     * <p>[Ny, tagg-synlig-runden, 2026-09-08] En opplysning som hører til lenkemålet, men som IKKE
     * skal være hovedleddet i kjeden — vises kun som `title` (hover) på lenken. Konkret grunn:
     * virksomhetens offisielle REGISTERNAVN («Karasjoga gielda / Karasjok kommune») sto tidligere
     * som selve lenketeksten, og Johanns innvending var at hovedleddet skal være navneformen
     * («Karasjok kommune»). Opplysningen er fortsatt sann og nyttig, så den flyttes til hover
     * framfor å fjernes.</p>
     */
    titleTillegg?: string;
  } | undefined;
  /**
   * Hvilket lag som vises (én type om gangen — radio). Ukontrollert hvis utelatt.
   *
   * <p>[PRESISERT, tagg-synlig-runden, 2026-09-08] En TOM/utelatt verdi betyr «brukeren har ikke
   * valgt lag ennå», og komponenten deriverer da selv hvilket lag som skal vises (se
   * `finnStandardLag`). En kontrollerende forelder skal derfor starte på `''` og først sette en
   * verdi når `onActiveKindChange` fyrer — ikke forhåndsvelge `kinds[0]`, som var nettopp feilen
   * denne runden retter.</p>
   */
  activeKind?: TagKindId;
  onActiveKindChange?: (id: TagKindId) => void;
  /** Vis tagg-listen med Fjern under teksten. Default true. */
  showTagList?: boolean;
  readOnly?: boolean;
  /**
   * Kryssreferanser funnet i selve løpeteksten (Lovdatas egne `<a href>`-lenker under import,
   * 2026-07-30) — rendres som en alltid-synlig innebygd lenke, uavhengig av hvilket tag-lag som
   * vises (i motsetning til `tags`, som kun viser ett lag om gangen). Offsettene er absolutte mot
   * `text`, samme koordinatsystem som `tags`.
   */
  references?: { start: number; end: number; href: string }[];
  /**
   * [Ny, punktliste-runden, 2026-09-09, issue #213] UNDERORDNEDE tekstblokker som hører til samme
   * bestemmelse som `text`, men som er egne noder med egne eId-er og egne tegnposisjoner — i praksis
   * punkt-nodene under et ledd.
   *
   * <p><b>Hvorfor de bor inni denne komponenten og ikke som N søsken-`TagTekst` i kalleren.</b> En
   * `TagTekst` per punkt ville gitt åtte lag-velgere, åtte tagg-linjer og åtte tagg-lister på skjermen
   * for én definisjon. Verre: lag-valget (`velgAktivtLag`, `docs/09` §17/§21) skal se på ALT som vises
   * samtidig — er de eneste taggene i punkt 3, må laget som velges ved kald åpning være DET laget.
   * Åtte uavhengige komponenter kan ikke ta det valget. Én komponent med flere tekstflater kan.</p>
   *
   * <p>Hver blokk har sin egen tekstflate med sin egen seleksjons-container, slik at offsettene som
   * sendes til `onTag` er relative til blokkens EGEN tekst. Blokkene kan nøstes (`underblokker`) —
   * punkt-i-punkt finnes i ekte data.</p>
   */
  underblokker?: Tekstblokk[];
  /**
   * [Ny, punktliste-runden, 2026-09-09] Fotnote rendret rett UNDER underblokk-lista, f.eks. hva
   * listemerkene faktisk er. En ferdig `ReactNode` fra kalleren, av samme grunn som `mellomledd` i
   * `resolveRef`: denne komponenten er domene-agnostisk og vet ikke hva et punktnummer i en norsk
   * forskrift betyr. Vises kun når det finnes underblokker.
   */
  underblokkerFotnote?: ReactNode;
}

/**
 * [Ny, punktliste-runden, 2026-09-09, issue #213] Én underordnet tekstblokk — se
 * {@link TagTekstProps.underblokker}.
 */
export interface Tekstblokk {
  /** Blokkens identitet i kallerens verden (nodens eId). Sendes tilbake i `onTag`. */
  nokkel: string;
  /** Listemerket som vises foran teksten. Utelates det, vises ingen merking. */
  merke?: string;
  /** `title` på merket — kallerens forklaring av hva merket ER. Se `underblokkerFotnote`. */
  merkeForklaring?: string;
  tekst: string;
  tags: TextTag[];
  references?: { start: number; end: number; href: string }[];
  underblokker?: Tekstblokk[];
}

/** Blokkene flatet ut, til bokkføring (lagvalg, tagg-liste) — visningen rekurserer i stedet, se `BlokkListe`. */
interface FlatBlokk {
  /** `null` = hovedteksten (`text`-propen). */
  nokkel: string | null;
  merke?: string;
  tekst: string;
  tags: TextTag[];
  references?: { start: number; end: number; href: string }[];
}

function flatUtBlokker(blokker: Tekstblokk[] | undefined): FlatBlokk[] {
  if (!blokker) return [];
  return blokker.flatMap((b) => [
    { nokkel: b.nokkel, merke: b.merke, tekst: b.tekst, tags: b.tags, references: b.references },
    ...flatUtBlokker(b.underblokker),
  ]);
}

/* --------------------------- hjelpere --------------------------- */

interface Seg {
  text: string;
  kind?: TagKindId;
  ref?: string | null;
  tagId?: string;
  /** Satt for en innebygd kryssreferanse-lenke (se `references`-propen) — ignorerer kind/mark-styling. */
  href?: string;
}

/** Del teksten i segmenter for ÉTT lag (én kind om gangen). Innenfor ett
 *  lag kan tagger ikke overlappe, så segmenteringen er sekvensiell.
 *  Samme tekststrekning kan bære flere tagger på tvers av lag — men bare
 *  ett lag vises av gangen (radio), så visningen blir aldri rotete. */
function buildSegments(text: string, tags: TextTag[]): Seg[] {
  const sorted = [...tags].filter((t) => t.end > t.start).sort((a, b) => a.start - b.start);
  const out: Seg[] = [];
  let i = 0;
  for (const t of sorted) {
    if (t.start < i) continue; // hopp over evt. overlapp innen laget
    if (t.start > i) out.push({ text: text.slice(i, t.start) });
    out.push({ text: text.slice(t.start, t.end), kind: t.kind, ref: t.ref, tagId: t.id });
    i = t.end;
  }
  if (i < text.length) out.push({ text: text.slice(i) });
  return out;
}

/** Klipper/forskyver tagger til en lokal understrekning [rs,re) — brukes til å dele tag-rendering
 *  opp mellom referanse-strekninger uten å endre `commit`/`onTag`s absolutte koordinatsystem. */
function clipTags(tags: TextTag[], rs: number, re: number): TextTag[] {
  return tags
    .filter((t) => t.end > rs && t.start < re)
    .map((t) => ({ ...t, start: Math.max(t.start, rs) - rs, end: Math.min(t.end, re) - rs }));
}

/** To-nivås segmentering: topp-nivå deler teksten i innebygde referanse-lenker (alltid synlige,
 *  uavhengig av aktivt tag-lag — se `references`-propen) og «vanlige» strekninger mellom dem. For
 *  en vanlig strekning kjøres den eksisterende per-kind `buildSegments` uendret. En referanse-
 *  strekning rendres direkte som lenke, uten videre tag-mark-nesting inni — en bevisst forenkling,
 *  se `TagTekstProps.references`-docen. */
function splitByReferences(
  text: string,
  references: { start: number; end: number; href: string }[] | undefined,
  tags: TextTag[],
): Seg[] {
  if (!references || references.length === 0) return buildSegments(text, tags);
  const sorted = [...references].filter((r) => r.end > r.start).sort((a, b) => a.start - b.start);
  const out: Seg[] = [];
  let i = 0;
  for (const r of sorted) {
    if (r.start < i) continue; // overlappende referanser — bør ikke skje, hopp over for robusthet
    if (r.start > i) out.push(...buildSegments(text.slice(i, r.start), clipTags(tags, i, r.start)));
    out.push({ text: text.slice(r.start, r.end), href: r.href });
    i = r.end;
  }
  if (i < text.length) out.push(...buildSegments(text.slice(i), clipTags(tags, i, text.length)));
  return out;
}

/** Sant hvis [start,end) overlapper en eksisterende tag AV SAMME kind.
 *  (Overlapp på tvers av kinds er lov — «uklanderlig vandel» kan være
 *  både begrep og vilkår.) */
function overlapsSameKind(tags: TextTag[], start: number, end: number, kind: TagKindId): boolean {
  return tags.some((t) => t.kind === kind && t.end > t.start && start < t.end && end > t.start);
}

/** Offset for start/slutt av gjeldende seleksjon, relativt til container. */
function selectionOffsets(container: HTMLElement): { start: number; end: number; text: string } | null {
  const sel = window.getSelection();
  if (!sel || sel.rangeCount === 0) return null;
  const text = sel.toString().trim();
  if (!text) return null;
  const range = sel.getRangeAt(0);
  if (!container.contains(range.startContainer)) return null;
  const pre = document.createRange();
  pre.selectNodeContents(container);
  pre.setEnd(range.startContainer, range.startOffset);
  const post = document.createRange();
  post.selectNodeContents(container);
  post.setEnd(range.endContainer, range.endOffset);
  return { start: pre.toString().length, end: post.toString().length, text };
}

/**
 * [Ny, gruppemedlemskap-runden, 2026-09-08, issue #164] Ett tagget segment i LØPETEKSTEN.
 *
 * <p>Er taggen koblet til noe `resolveRef` kan gi en lenke for, er markeringen selv navigerbar.
 * Før dette var den bare en `<mark>` med et `title`-tooltip som viste en rå GUID, og den ENESTE
 * veien videre gikk via tagg-listen under teksten. Johanns krav er nettopp den veien: se «Karasjok»
 * merket i forskriften og gå derfra til Karasjok kommune — og se «språkutviklingskommuner» merket i
 * sameloven og gå derfra til gruppen for å se hva den inneholder. Det er markeringen i teksten man
 * peker på når man leser, ikke en liste lenger ned.</p>
 *
 * <p><b>`draggable={false}` er ikke pynt.</b> En `<a>` i teksten gjør at museklikk-og-dra starter en
 * lenke-DRAGGING i stedet for en tekst-SELEKSJON, og seleksjon er nøyaktig hvordan man oppretter en
 * ny tagg (`selectionOffsets`). Uten dette ville det å gjøre tagger navigerbare ha ødelagt taggingen
 * over og rundt allerede taggede ord.</p>
 *
 * <p>Fargen og understrekingen er uendret fra den ikke-lenkede markeringen, og lenken arver
 * `color: inherit`: en tagg skal se ut som en tagg, ikke som en blå lenke midt i lovteksten. At den
 * er klikkbar formidles av `cursor: pointer` og `title`, ikke av at fargekoden brytes — fargen bærer
 * allerede en egen betydning (hvilket tagg-lag).</p>
 */
function TaggetSegment({
  tekst, etikett, farge, lenke, taggId,
}: {
  tekst: string;
  etikett: string;
  farge: string | undefined;
  lenke: { label: string; href: string; mellomleddTekst?: string; titleTillegg?: string } | undefined;
  /** [Ny, tagg-synlig-runden, 2026-09-08] Ankeret taggraden under teksten ruller til — se
   *  `visTaggITeksten`. Rent DOM-anker, ingen visuell effekt. */
  taggId: string | undefined;
}) {
  const stil: CSSProperties = {
    background: `var(--ds-color-${farge}-surface-tinted)`,
    color: `var(--ds-color-${farge}-text-default)`,
    borderBottom: `2px solid var(--ds-color-${farge}-border-default)`,
    borderRadius: 'var(--ds-border-radius-sm)',
    padding: '0 2px',
  };
  if (!lenke) {
    return <mark data-tagg-id={taggId} title={etikett} style={stil}>{tekst}</mark>;
  }
  // Samme kjede som tagg-listen viser, men som ren tekst — et `title`-attributt kan ikke bære en
  // ReactNode, så mellomleddet kommer som `mellomleddTekst`. Se `resolveRef`-propens kommentar.
  // `titleTillegg` er opplysningen som IKKE skal være hovedledd (registernavnet) — den henger på
  // enden av hover-kjeden i stedet for å forsvinne.
  const kjede =
    (lenke.mellomleddTekst
      ? `${etikett} → ${lenke.mellomleddTekst} → ${lenke.label}`
      : `${etikett} → ${lenke.label}`) +
    (lenke.titleTillegg ? ` (${lenke.titleTillegg})` : '') +
    ' — åpne';
  return (
    <mark data-tagg-id={taggId} title={kjede} style={{ ...stil, cursor: 'pointer' }}>
      <Link asChild>
        <RouterLink to={lenke.href} draggable={false} style={{ color: 'inherit' }}>{tekst}</RouterLink>
      </Link>
    </mark>
  );
}

/* ----------------------- tekstflate (én blokk) ----------------------- */

/**
 * [Ny, punktliste-runden, 2026-09-09, issue #213] Selve tekstflaten for ÉN tekstblokk, utskilt fra
 * `TagTekst` sin render.
 *
 * <p>Grunnen den er en egen komponent: `selectionOffsets` måler offsettene mot sin CONTAINER, og en
 * tagg skal lagres mot noden teksten faktisk står i. Hver blokk må derfor ha sin egen container-ref
 * — hovedteksten og hvert punkt hver sin. Lå alt i én container ville en markering i punkt 3 fått
 * offsett målt fra starten av leddet, altså en sporbarhetspåstand om et sted i teksten der det ikke
 * står noe slikt.</p>
 */
function Tekstflate({
  blokk, segmenter, kindById, resolveRef, readOnly, onSelection,
}: {
  blokk: FlatBlokk;
  segmenter: Seg[];
  kindById: Record<string, TagKind>;
  resolveRef: TagTekstProps['resolveRef'];
  readOnly: boolean;
  onSelection: (nokkel: string | null, off: { start: number; end: number; text: string } | null) => void;
}) {
  const ref = useRef<HTMLDivElement>(null);
  const fang = useCallback(() => {
    if (readOnly || !ref.current) return;
    onSelection(blokk.nokkel, selectionOffsets(ref.current));
  }, [readOnly, onSelection, blokk.nokkel]);

  return (
    <div
      ref={ref}
      onMouseUp={fang}
      onKeyUp={fang}
      style={{
        fontSize: 'var(--ds-font-size-4)',
        lineHeight: 'var(--ds-line-height-lg)',
        userSelect: 'text',
      }}
    >
      {segmenter.map((s, i) =>
        s.href ? (
          <Link asChild key={i}>
            <RouterLink to={s.href}>{s.text}</RouterLink>
          </Link>
        ) : s.kind ? (
          (() => {
            const lenke = s.ref ? resolveRef?.(s.kind, s.ref) : undefined;
            const lagNavn = kindById[s.kind]?.label ?? s.kind;
            return (
              <TaggetSegment
                key={i}
                taggId={s.tagId}
                tekst={s.text}
                // [ENDRET, tagg-synlig-runden, 2026-09-08] Den rå `ref`-GUIDen står bare i
                // hover-teksten når kjeden IKKE kunne resolves. Kan den resolves, er GUIDen ren
                // støy foran et lesbart navn («Virksomhet: 9ea6cbe2-… → Karasjok → …»).
                etikett={`${lagNavn}${s.ref && !lenke ? `: ${s.ref}` : ''}`}
                farge={kindById[s.kind]?.color}
                lenke={lenke}
              />
            );
          })()
        ) : (
          <span key={i}>{s.text}</span>
        ),
      )}
    </div>
  );
}

/**
 * [Ny, punktliste-runden, 2026-09-09, issue #213] Underblokk-lista, rekursivt.
 *
 * <p>En ekte `<ol>` med `<li>` fordi det ER en liste — akseptansekriterium 2 i #213: leseren skal se
 * at det er en liste, ikke løpetekst limt sammen, og en skjermleser skal få det samme («liste med 8
 * elementer»). `listStyle: 'none'` fordi merket rendres selv: nettleserens egen nummerering ville
 * lagt et ANDRE, konkurrerende tall ved siden av vårt (og en `<ol>` teller alltid 1, 2, 3 uansett hva
 * nodene faktisk heter). Innrykket er `paddingInlineStart` på lista, slik at punktene står visuelt
 * UNDERORDNET innledningen framfor å flyte i samme venstremarg.</p>
 */
function BlokkListe({
  blokker, rendreBlokk,
}: {
  blokker: Tekstblokk[];
  rendreBlokk: (blokk: Tekstblokk) => ReactNode;
}) {
  return (
    <ol
      style={{
        listStyle: 'none',
        margin: 'var(--ds-size-2) 0 0',
        paddingInlineStart: 'var(--ds-size-6)',
        display: 'flex',
        flexDirection: 'column',
        gap: 'var(--ds-size-2)',
      }}
    >
      {blokker.map((b) => (
        <li key={b.nokkel} style={{ display: 'flex', gap: 'var(--ds-size-3)', alignItems: 'flex-start' }}>
          {b.merke && (
            <Metatekst as="span"
              title={b.merkeForklaring}
              style={{
                flex: '0 0 auto',
                minWidth: '1.6rem',
                color: 'var(--ds-color-neutral-text-subtle)',
                lineHeight: 'var(--ds-line-height-lg)',
              }}
>
              {b.merke}
            </Metatekst>
          )}
          <div style={{ flex: 1, minWidth: 0 }}>
            {rendreBlokk(b)}
            {b.underblokker && b.underblokker.length > 0 && (
              <BlokkListe blokker={b.underblokker} rendreBlokk={rendreBlokk} />
            )}
          </div>
        </li>
      ))}
    </ol>
  );
}

/* --------------------------- komponent --------------------------- */

export function TagTekst({
  text, tags, kinds, onTag, onRemoveTag, registry, onLinkTag, onOpprettFraTag, opprettFraTagKinds, resolveRef,
  activeKind, onActiveKindChange, showTagList = true, readOnly = false, references,
  underblokker, underblokkerFotnote,
}: TagTekstProps) {
  // [ENDRET, punktliste-runden, 2026-09-09] Ref-en er flyttet til `Tekstflate` (én per blokk, siden
  // offsettene måles mot containeren). Denne ref-en er nå bare ROT-noden, brukt av `visTaggITeksten`
  // til å finne `[data-tagg-id]` uansett hvilken blokk markeringen står i.
  const rotRef = useRef<HTMLDivElement>(null);
  // [ENDRET, tagg-synlig-runden, 2026-09-08] `null` = brukeren har ikke valgt lag ennå (var
  // `kinds[0]?.id`, som gjorde at teksten sto umarkert på en node uten begrep-tagger). Selve
  // defaulten kommer fra `finnStandardLag`, som ser på nodens faktiske tagger — og den slår inn KUN
  // så lenge brukeren ikke har valgt selv, slik at et manuelt lagvalg står.
  const [internalKind, setInternalKind] = useState<TagKindId | null>(null);
  // Tom streng fra en kontrollerende forelder betyr også «ikke valgt» — se `activeKind`-propen.
  const brukervalg = activeKind ?? internalKind;

  // [Ny, punktliste-runden, 2026-09-09] Hovedteksten og underblokkene som ÉN flat liste, til all
  // bokkføring: lagvalg, tagg-liste og overlapp-sjekk. Visningen rekurserer i stedet (`BlokkListe`),
  // siden en liste skal rendres som en liste.
  const flateBlokker = useMemo<FlatBlokk[]>(
    () => [{ nokkel: null, tekst: text, tags, references }, ...flatUtBlokker(underblokker)],
    [text, tags, references, underblokker],
  );
  // [ENDRET, punktliste-runden, 2026-09-09] Lagvalget ser på taggene i ALT som vises, ikke bare i
  // hovedteksten. Ellers ville en definisjon der de eneste taggene ligger i punkt 3 åpnet i et tomt
  // lag — samme feilklasse som `docs/09` §17/§21 dokumenterer: «en default som bestemmer hva brukeren
  // SER ved kald åpning skal vise mest mulig av det som faktisk er markert».
  const alleTagger = useMemo(() => flateBlokker.flatMap((b) => b.tags), [flateBlokker]);
  const active = useMemo(() => velgAktivtLag(brukervalg, alleTagger, kinds), [brukervalg, alleTagger, kinds]);
  const setActive = onActiveKindChange ?? setInternalKind;

  // [ENDRET, punktliste-runden, 2026-09-09] Seleksjonen bærer nå BLOKKEN den ble gjort i (`nokkel`),
  // slik at `commit` lagrer taggen mot riktig node. `null` = hovedteksten.
  const [sel, setSel] = useState<{ nokkel: string | null; start: number; end: number; text: string } | null>(null);
  const [pendingKind, setPendingKind] = useState<TagKindId | null>(null);

  const kindById = useMemo(() => Object.fromEntries(kinds.map((k) => [k.id, k])), [kinds]);
  // Segmentene beregnes per blokk — hver blokk har sin egen tekst, sine egne tagger og sine egne
  // referanse-offsett, alle i blokkens eget koordinatsystem.
  const segmenterPerBlokk = useMemo(() => {
    const kart = new Map<string | null, Seg[]>();
    for (const b of flateBlokker) {
      kart.set(b.nokkel, splitByReferences(b.tekst, b.references, b.tags.filter((t) => t.kind === active)));
    }
    return kart;
  }, [flateBlokker, active]);

  // Tagg-listen under teksten viser ALLE tagger fra ALLE blokker (samme «listen står komplett»-valg
  // som `docs/09` §17 begrunner for lag) — hver rad bærer med seg blokken sin, slik at sitatet kan
  // klippes fra RIKTIG tekst og radens plassering (hvilket punkt) kan vises.
  const taggrader = useMemo(
    () => flateBlokker.flatMap((b) => b.tags.map((t) => ({ tagg: t, blokk: b }))),
    [flateBlokker],
  );

  const haandterSeleksjon = useCallback(
    (nokkel: string | null, off: { start: number; end: number; text: string } | null) => {
      setSel(off ? { nokkel, ...off } : null);
      setPendingKind(null);
    },
    [],
  );

  const commit = useCallback(
    (kind: TagKindId, ref: string | null) => {
      if (!sel) return;
      // Overlapp lov på tvers av kinds, men ikke innen samme kind — sjekket mot taggene i DEN blokken
      // markeringen står i, ikke mot alle blokkene: offsettene er blokk-lokale, så en tagg i punkt 2
      // og en i punkt 5 kan ha samme tallverdier uten å overlappe i det hele tatt.
      const blokk = flateBlokker.find((b) => b.nokkel === sel.nokkel);
      if (blokk && overlapsSameKind(blokk.tags, sel.start, sel.end, kind)) {
        setSel(null);
        setPendingKind(null);
        window.getSelection()?.removeAllRanges();
        return;
      }
      onTag({ start: sel.start, end: sel.end, kind, ref }, sel.nokkel);
      setActive(kind); // vis laget man nettopp tagget i
      setSel(null);
      setPendingKind(null);
      window.getSelection()?.removeAllRanges();
    },
    [sel, onTag, flateBlokker, setActive],
  );

  /**
   * [Ny, tagg-synlig-runden, 2026-09-08] Gjør taggraden UNDER teksten koherent med markeringen I
   * teksten: aktiverer radens eget lag og ruller markeringen inn i synsfeltet.
   *
   * <p><b>Valget som er tatt, og hvorfor.</b> Tagg-listen lister ALLE tagger uansett aktivt lag,
   * mens teksten bare markerer ett. De to kunne motsi hverandre (14 rader listet, ingenting
   * markert). Alternativet var å FILTRERE listen til aktivt lag, men da forsvinner den eneste
   * antydningen om at noden har tagger i andre lag i det hele tatt — og å skjule at det finnes
   * arbeid i et annet lag er en dårligere feil enn å vise en rad som ikke er markert akkurat nå.
   * Listen beholdes derfor komplett, og motsigelsen løses ved at raden ER veien til markeringen:
   * ett klikk bytter lag og ruller markeringen fram. Rader i et annet lag enn det aktive er
   * samtidig dempet og forklarer seg selv i `title`, så uoverensstemmelsen er synlig og
   * forklart framfor skjult.</p>
   *
   * <p>[ENDRET, punktliste-runden, 2026-09-09] Søket går fra ROT-noden, ikke fra én tekstflate —
   * markeringen kan stå i et punkt under leddet.</p>
   */
  const visTaggITeksten = useCallback(
    (t: TextTag) => {
      if (t.kind !== active) setActive(t.kind);
      // Markeringen finnes først etter at lagbyttet er rendret — derfor på neste frame.
      requestAnimationFrame(() => {
        rotRef.current
          ?.querySelector(`[data-tagg-id="${t.id}"]`)
          ?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
      });
    },
    [active, setActive],
  );

  const clearSelection = useCallback(() => {
    setSel(null);
    setPendingKind(null);
    window.getSelection()?.removeAllRanges();
  }, []);

  /** Én tekstflate for en underblokk — slår opp de ferdig beregnede segmentene på blokkens nøkkel. */
  const rendreUnderblokk = useCallback(
    (b: Tekstblokk): ReactNode => {
      const flat = flateBlokker.find((f) => f.nokkel === b.nokkel);
      if (!flat) return null;
      return (
        <Tekstflate
          blokk={flat}
          segmenter={segmenterPerBlokk.get(b.nokkel) ?? []}
          kindById={kindById}
          resolveRef={resolveRef}
          readOnly={readOnly}
          onSelection={haandterSeleksjon}
        />
      );
    },
    [flateBlokker, segmenterPerBlokk, kindById, resolveRef, readOnly, haandterSeleksjon],
  );

  return (
    <div ref={rotRef}>
      {/* Lag-velger — Designsystemet ToggleGroup (single, radio): ett lag vises av gangen */}
      <ToggleGroup
        value={active ?? ''}
        onChange={setActive}
        data-size="sm"
        data-toggle-group="Vis tagger"
        style={{ marginBottom: 'var(--ds-size-3)' }}
      >
        {kinds.map((k) => (
          <ToggleGroup.Item key={k.id} value={k.id}>
            {k.label}
          </ToggleGroup.Item>
        ))}
      </ToggleGroup>

      {/* TAG-LINJE — fast handlingslinje rett over teksten. Erstatter flytende
          meny: ingen posisjonering, ingen kollisjon, alltid synlig i flyten.
          [PRESISERT, punktliste-runden, 2026-09-09] ÉN linje for alle blokkene, ikke én per punkt:
          åtte tagg-linjer under hverandre for én definisjon er ikke et verktøy. Linjen viser hvilken
          blokk markeringen står i via sitatet, og `commit` lagrer mot den blokken. */}
      {!readOnly && (
        <div
          role="toolbar"
          aria-label="Tagg markert tekst"
          style={{
            display: 'flex', alignItems: 'center', gap: 'var(--ds-size-2)',
            flexWrap: 'wrap', minHeight: 'var(--ds-size-10)',
            padding: 'var(--ds-size-2) var(--ds-size-3)',
            marginBottom: 'var(--ds-size-3)',
            borderRadius: 'var(--ds-border-radius-default)',
            background: sel ? 'var(--ds-color-accent-surface-tinted)' : 'var(--ds-color-neutral-surface-tinted)',
            border: `1px solid ${sel ? 'var(--ds-color-accent-border-subtle)' : 'var(--ds-color-neutral-border-subtle)'}`,
          }}
        >
          {!sel ? (
            <span style={{ fontSize: 'var(--ds-font-size-2)', color: 'var(--ds-color-neutral-text-subtle)' }}>
              Marker tekst for å tagge den
            </span>
          ) : !pendingKind ? (
            <>
              <span style={{ fontSize: 'var(--ds-font-size-2)', color: 'var(--ds-color-neutral-text-subtle)', maxWidth: 260, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                «{sel.text}» →
              </span>
              {kinds.map((k) => (
                <Button key={k.id} variant="secondary" data-size="sm" onClick={() => setPendingKind(k.id)}>
                  <span style={{ width: 9, height: 9, borderRadius: 2, flex: '0 0 auto', background: `var(--ds-color-${k.color}-base-default)` }} />
                  {k.label}
                </Button>
              ))}
              <Button variant="tertiary" data-size="sm" onClick={clearSelection} style={{ marginInlineStart: 'auto' }}>
                Avbryt
              </Button>
            </>
          ) : (
            <>
              <Tag data-color={kindById[pendingKind]?.color} data-size="sm">
                {kindById[pendingKind]?.label}
              </Tag>
              <Button variant="secondary" data-size="sm" onClick={() => commit(pendingKind, null)}>
                Ny tagg
              </Button>
              {(registry?.[pendingKind] ?? []).slice(0, 4).map((cand) => (
                <Button key={cand.ref} variant="tertiary" data-size="sm" onClick={() => commit(pendingKind, cand.ref)}>
                  {cand.label}
                </Button>
              ))}
              <Button variant="tertiary" data-size="sm" onClick={() => setPendingKind(null)} style={{ marginInlineStart: 'auto' }}>
                ‹ Tilbake
              </Button>
            </>
          )}
        </div>
      )}

      {/* Tekstflate — egen markeringslogikk. Hovedteksten først, deretter underblokkene som liste. */}
      <Tekstflate
        blokk={flateBlokker[0]}
        segmenter={segmenterPerBlokk.get(null) ?? []}
        kindById={kindById}
        resolveRef={resolveRef}
        readOnly={readOnly}
        onSelection={haandterSeleksjon}
      />

      {underblokker && underblokker.length > 0 && (
        <>
          <BlokkListe blokker={underblokker} rendreBlokk={rendreUnderblokk} />
          {/* Metatekst etter `docs/09` §6: font-size-1 KOMBINERT med neutral-text-subtle, ikke opacity. */}
          {underblokkerFotnote && (
            <Metatekst as="div" style={{ marginTop: 'var(--ds-size-2)', color: 'var(--ds-color-neutral-text-subtle)' }}>
              {underblokkerFotnote}
            </Metatekst>
          )}
        </>
      )}

      {/* Tagg-liste med Fjern — Designsystemet Tag + Button */}
      {showTagList && taggrader.length > 0 && (
        <div
          style={{
            marginTop: 'var(--ds-size-4)',
            paddingTop: 'var(--ds-size-3)',
            borderTop: '1px solid var(--ds-color-neutral-border-subtle)',
          }}
        >
          <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--ds-size-2)' }}>
            {taggrader.map(({ tagg: t, blokk }) => {
              // [Ny, tagg-synlig-runden, 2026-09-08] Rader i et ANNET lag enn det som vises er
              // dempet og forklarer seg selv — de er ikke markert i teksten akkurat nå, og det skal
              // være synlig framfor forvirrende. Se `visTaggITeksten` for hvorfor listen likevel
              // beholdes komplett.
              const iAktivtLag = t.kind === active;
              const lagNavn = kindById[t.kind]?.label ?? t.kind;
              return (
              <div
                key={t.id}
                style={{ display: 'flex', alignItems: 'center', gap: 'var(--ds-size-2)', opacity: iAktivtLag ? 1 : 0.6 }}
              >
                <Tag
                  data-color={kindById[t.kind]?.color}
                  data-size="sm"
                  role="button"
                  tabIndex={0}
                  title={
                    iAktivtLag
                      ? `Rull markeringen i teksten inn i synsfeltet (laget «${lagNavn}» vises)`
                      : `Vises ikke i teksten nå — klikk for å vise laget «${lagNavn}»`
                  }
                  style={{ cursor: 'pointer' }}
                  onClick={() => visTaggITeksten(t)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      visTaggITeksten(t);
                    }
                  }}
                >
                  {lagNavn}
                </Tag>
                {/* [Ny, punktliste-runden, 2026-09-09] HVOR taggen står, når den ikke står i
                  * hovedteksten. Uten dette leddet ville tagg-listen under et ledd med åtte punkter
                  * vist åtte sitater uten å si hvilket punkt hvert av dem hører til — og et sitat er
                  * ikke en posisjon. */}
                {blokk.nokkel !== null && blokk.merke && (
                  <Tag data-color="neutral" data-size="sm" title={`Taggen står i punkt ${blokk.merke}, med punktets egen eId (${blokk.nokkel})`}>
                    punkt {blokk.merke}
                  </Tag>
                )}
                {t.kreverGjennomgang && (
                  <Tag data-color="danger" data-size="sm" title="Fant ikke et entydig treff ved reimport av rettskilden — sitatet må sjekkes manuelt.">
                    Krever gjennomgang
                  </Tag>
                )}
                <span
                  style={{
                    flex: 1, minWidth: 0, color: 'var(--ds-color-neutral-text-subtle)',
                    overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
                  }}
                >
                  {/* Sitatet er selv en klikkflate til markeringen i teksten — samme handling som
                    * lag-merkelappen til venstre. Bevisst IKKE lagt på hele raden: raden inneholder
                    * lenken videre til entiteten, og et klikk der skal navigere, ikke bytte lag. */}
                  <span
                    role="button"
                    tabIndex={0}
                    title={
                      iAktivtLag
                        ? 'Rull markeringen i teksten inn i synsfeltet'
                        : `Vises ikke i teksten nå — klikk for å vise laget «${lagNavn}»`
                    }
                    style={{ cursor: 'pointer' }}
                    onClick={() => visTaggITeksten(t)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        visTaggITeksten(t);
                      }
                    }}
                  >
                    {/* [ENDRET, punktliste-runden, 2026-09-09] Sitatet klippes fra taggens EGEN
                      * blokk, ikke fra hovedteksten: en tagg i punkt 3 har offsett i punktets tekst,
                      * og `text.slice(...)` ville gitt et vilkårlig utsnitt av leddet i stedet. */}
                    «{blokk.tekst.slice(t.start, t.end)}»
                  </span>
                  {t.ref &&
                    (() => {
                      const lenke = resolveRef?.(t.kind, t.ref);
                      if (!lenke) return ` → ${t.ref}`;
                      return (
                        <>
                          {/* [Ny, navneform-kjede-runden, 2026-09-08] Hele kjeden, ikke bare
                            * endepunktet: «Karasjok» → [Kortform] → Karasjoga gielda / Karasjok
                            * kommune. Mellomleddet er en ferdig ReactNode fra kalleren — se
                            * `resolveRef`-propens kommentar for hvorfor det ikke bygges her. */}
                          {lenke.mellomledd && (
                            <>
                              {' → '}
                              {lenke.mellomledd}
                            </>
                          )}
                          {' → '}
                          <Link asChild>
                            {/* `titleTillegg` på hover, ikke i lenketeksten: opplysningen hører til
                              * målet, men skal ikke være hovedleddet i kjeden — se propen. */}
                            <RouterLink to={lenke.href} title={lenke.titleTillegg}>
                              {lenke.label}
                            </RouterLink>
                          </Link>
                        </>
                      );
                    })()}
                </span>
                {!readOnly && !t.ref && onLinkTag && (registry?.[t.kind]?.length ?? 0) > 0 && (
                  <Metatekst as="select"
                    aria-label={`Koble tagg til eksisterende ${kindById[t.kind]?.label ?? t.kind}`}
                    defaultValue=""
                    onChange={(e) => {
                      if (e.target.value) onLinkTag(t.id, e.target.value);
                    }}
                    
                  >
                    <option value="" disabled>
                      Koble til …
                    </option>
                    {registry![t.kind].map((cand) => (
                      <option key={cand.ref} value={cand.ref}>
                        {cand.label}
                      </option>
                    ))}
                  </Metatekst>
                )}
                {!readOnly && !t.ref && onOpprettFraTag && (opprettFraTagKinds ?? []).includes(t.kind) && (
                  <Button variant="tertiary" data-size="sm" onClick={() => onOpprettFraTag(t.id, t.kind)}>
                    Opprett {kindById[t.kind]?.label.toLowerCase() ?? t.kind} fra dette utdraget →
                  </Button>
                )}
                {!readOnly && (
                  <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => onRemoveTag(t.id)}>
                    Fjern
                  </Button>
                )}
              </div>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
