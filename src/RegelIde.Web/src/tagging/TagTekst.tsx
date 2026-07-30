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
import { useCallback, useMemo, useRef, useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import { ToggleGroup, Tag, Button, Link } from '@digdir/designsystemet-react';

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
  onTag: (t: { start: number; end: number; kind: TagKindId; ref: string | null }) => void;
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
   * Slår opp en menneskelesbar lenke for en koblet tagg sin `ref` — 2026-07-30, fikser at
   * koblede tagger kun viste sin rå GUID/eId. `undefined` betyr «ingen lenke tilgjengelig ennå»
   * (f.eks. treffet mangler i den ferdiglastede listen), og faller da tilbake til rå tekst.
   */
  resolveRef?: (kind: TagKindId, ref: string) => { label: string; href: string } | undefined;
  /** Hvilket lag som vises (én type om gangen — radio). Ukontrollert hvis utelatt. */
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

/* --------------------------- komponent --------------------------- */

export function TagTekst({
  text, tags, kinds, onTag, onRemoveTag, registry, onLinkTag, resolveRef,
  activeKind, onActiveKindChange, showTagList = true, readOnly = false, references,
}: TagTekstProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [internalKind, setInternalKind] = useState<TagKindId>(kinds[0]?.id);
  const active = activeKind ?? internalKind;
  const setActive = onActiveKindChange ?? setInternalKind;

  const [sel, setSel] = useState<{ start: number; end: number; text: string } | null>(null);
  const [pendingKind, setPendingKind] = useState<TagKindId | null>(null);

  const kindById = useMemo(() => Object.fromEntries(kinds.map((k) => [k.id, k])), [kinds]);
  const shownTags = useMemo(() => tags.filter((t) => t.kind === active), [tags, active]);
  const segments = useMemo(() => splitByReferences(text, references, shownTags), [text, references, shownTags]);

  const captureSelection = useCallback(() => {
    if (readOnly || !containerRef.current) return;
    const off = selectionOffsets(containerRef.current);
    setSel(off);
    setPendingKind(null);
  }, [readOnly]);

  const commit = useCallback(
    (kind: TagKindId, ref: string | null) => {
      if (!sel) return;
      // Overlapp lov på tvers av kinds, men ikke innen samme kind.
      if (overlapsSameKind(tags, sel.start, sel.end, kind)) {
        setSel(null);
        setPendingKind(null);
        window.getSelection()?.removeAllRanges();
        return;
      }
      onTag({ start: sel.start, end: sel.end, kind, ref });
      setActive(kind); // vis laget man nettopp tagget i
      setSel(null);
      setPendingKind(null);
      window.getSelection()?.removeAllRanges();
    },
    [sel, onTag, tags, setActive],
  );

  const clearSelection = useCallback(() => {
    setSel(null);
    setPendingKind(null);
    window.getSelection()?.removeAllRanges();
  }, []);

  return (
    <div>
      {/* Lag-velger — Designsystemet ToggleGroup (single, radio): ett lag vises av gangen */}
      <ToggleGroup
        value={active}
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
          meny: ingen posisjonering, ingen kollisjon, alltid synlig i flyten. */}
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

      {/* Tekstflate — egen markeringslogikk */}
      <div
        ref={containerRef}
        onMouseUp={captureSelection}
        onKeyUp={captureSelection}
        style={{
          fontSize: 'var(--ds-font-size-4)',
          lineHeight: 'var(--ds-line-height-lg)',
          userSelect: 'text',
        }}
      >
        {segments.map((s, i) =>
          s.href ? (
            <Link asChild key={i}>
              <RouterLink to={s.href}>{s.text}</RouterLink>
            </Link>
          ) : s.kind ? (
            <mark
              key={i}
              title={`${kindById[s.kind]?.label ?? s.kind}${s.ref ? `: ${s.ref}` : ''}`}
              style={{
                background: `var(--ds-color-${kindById[s.kind]?.color}-surface-tinted)`,
                color: `var(--ds-color-${kindById[s.kind]?.color}-text-default)`,
                borderBottom: `2px solid var(--ds-color-${kindById[s.kind]?.color}-border-default)`,
                borderRadius: 'var(--ds-border-radius-sm)',
                padding: '0 2px',
              }}
            >
              {s.text}
            </mark>
          ) : (
            <span key={i}>{s.text}</span>
          ),
        )}
      </div>

      {/* Tagg-liste med Fjern — Designsystemet Tag + Button */}
      {showTagList && tags.length > 0 && (
        <div
          style={{
            marginTop: 'var(--ds-size-4)',
            paddingTop: 'var(--ds-size-3)',
            borderTop: '1px solid var(--ds-color-neutral-border-subtle)',
          }}
        >
          <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--ds-size-2)' }}>
            {tags.map((t) => (
              <div key={t.id} style={{ display: 'flex', alignItems: 'center', gap: 'var(--ds-size-2)' }}>
                <Tag data-color={kindById[t.kind]?.color} data-size="sm">
                  {kindById[t.kind]?.label}
                </Tag>
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
                  «{text.slice(t.start, t.end)}»
                  {t.ref &&
                    (() => {
                      const lenke = resolveRef?.(t.kind, t.ref);
                      return lenke ? (
                        <>
                          {' → '}
                          <Link asChild>
                            <RouterLink to={lenke.href}>{lenke.label}</RouterLink>
                          </Link>
                        </>
                      ) : (
                        ` → ${t.ref}`
                      );
                    })()}
                </span>
                {!readOnly && !t.ref && onLinkTag && (registry?.[t.kind]?.length ?? 0) > 0 && (
                  <select
                    aria-label={`Koble tagg til eksisterende ${kindById[t.kind]?.label ?? t.kind}`}
                    defaultValue=""
                    onChange={(e) => {
                      if (e.target.value) onLinkTag(t.id, e.target.value);
                    }}
                    style={{ fontSize: 'var(--ds-font-size-1)' }}
                  >
                    <option value="" disabled>
                      Koble til …
                    </option>
                    {registry![t.kind].map((cand) => (
                      <option key={cand.ref} value={cand.ref}>
                        {cand.label}
                      </option>
                    ))}
                  </select>
                )}
                {!readOnly && (
                  <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => onRemoveTag(t.id)}>
                    Fjern
                  </Button>
                )}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
