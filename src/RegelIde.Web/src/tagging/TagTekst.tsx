/**
 * TagTekst
 * ------------------------------------------------------------------
 * Generell tekst-tagger: viser en tekstflate der brukeren kan markere
 * et ord/avsnitt og knytte det til en modell-entitet (begrep, vilkår,
 * regel, tjeneste — utvidbart). Taggene er posisjonsbaserte (tegn-offset).
 * Ett lag (kind) vises av gangen via en ToggleGroup, så visningen holder
 * seg ryddig selv når samme strekning bærer flere tagger på tvers av lag.
 *
 * Opprinnelig utkast fra Claude Design; tilpasset her (2026-07-24) for
 * byggesteg 1:
 *   - `ref` er `string | null`, ikke påkrevd — docs/06-veikart.md sier
 *     en tagg skal lagres med ref:null helt til byggesteg 2/4 gir den
 *     noe å peke på. Originalen antok et allerede-eksisterende register.
 *   - `Dropdown.Context` (brukt i originalen) finnes ikke i den
 *     installerte @digdir/designsystemet-react (1.18.0) — riktig API er
 *     `Dropdown` med `open`/`onClose` direkte (bygget på Popovers
 *     kontrollerte modus), uten et usynlig Trigger-anker.
 *   - Ny `showLayerSwitch`-prop: når man (som her) bruker én TagTekst
 *     per avsnitt i et helt dokument, vil hvert avsnitt ellers få sin
 *     egen ToggleGroup/tagg-liste. Med `false` viser instansen kun selve
 *     tekstflaten, og forelder styrer ett delt lagvalg + én samlet liste.
 *
 * Brukes i rettskildevisningen, men er ikke bundet til AKN — «text» er
 * ren streng, og «kinds» konfigureres av forelder.
 *
 * DESIGNSYSTEMET-KOMPONENTER SOM BRUKES
 *   - ToggleGroup   → lag-filteret («Vis tagger»)
 *   - Dropdown      → 2-stegs kontekstmeny (type → handling)
 *   - Tag           → fargede markeringer + tagg-listen
 *   - Button        → «Fjern», menyhandlinger
 * Selve tekst-/markeringslogikken finnes IKKE i DS og er egen kode her.
 *
 * TOKENS: kun --ds-* (ingen egne farger). kind → semantisk rolle,
 * konfigurerbart via `kinds` (se KINDS i RettskildeDetalj.tsx for
 * byggesteg 1s faktiske firevalg: begrep=accent, tjeneste=info,
 * vilkar=warning, regel=success — Designsystemet har ingen lilla-familie,
 * se docs/09-design-konvensjoner.md).
 */
import { useCallback, useMemo, useRef, useState } from 'react';
import { ToggleGroup, Dropdown, Tag, Button } from '@digdir/designsystemet-react';

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
  /** Hvilket lag som vises (én type om gangen — radio). Ukontrollert hvis utelatt. */
  activeKind?: TagKindId;
  onActiveKindChange?: (id: TagKindId) => void;
  /** Vis ToggleGroup-lagvelgeren i denne instansen. Default true — sett false når forelder viser én delt velger for flere TagTekst-instanser (f.eks. ett avsnitt per instans i et helt dokument). */
  showLayerSwitch?: boolean;
  /** Vis tagg-listen med Fjern under teksten. Default true — samme begrunnelse som showLayerSwitch. */
  showTagList?: boolean;
  readOnly?: boolean;
}

/* --------------------------- hjelpere --------------------------- */

interface Seg {
  text: string;
  kind?: TagKindId;
  ref?: string | null;
  tagId?: string;
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
  text,
  tags,
  kinds,
  onTag,
  onRemoveTag,
  registry,
  activeKind,
  onActiveKindChange,
  showLayerSwitch = true,
  showTagList = true,
  readOnly = false,
}: TagTekstProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [internalKind, setInternalKind] = useState<TagKindId>(kinds[0]?.id);
  const active = activeKind ?? internalKind;
  const setActive = onActiveKindChange ?? setInternalKind;

  const [menu, setMenu] = useState<
    { x: number; y: number; start: number; end: number; text: string; step: 'type' | 'action'; kind?: TagKindId } | null
  >(null);

  const kindById = useMemo(() => Object.fromEntries(kinds.map((k) => [k.id, k])), [kinds]);
  const shownTags = useMemo(() => tags.filter((t) => t.kind === active), [tags, active]);
  const segments = useMemo(() => buildSegments(text, shownTags), [text, shownTags]);

  const openMenu = useCallback(() => {
    if (readOnly || !containerRef.current) return;
    const off = selectionOffsets(containerRef.current);
    if (!off) {
      setMenu(null);
      return;
    }
    let rect: DOMRect | undefined;
    try {
      rect = window.getSelection()!.getRangeAt(0).getBoundingClientRect();
    } catch {
      /* noop */
    }
    setMenu({
      x: rect ? rect.left + rect.width / 2 : 0,
      y: rect ? rect.bottom + 8 : 0,
      start: off.start,
      end: off.end,
      text: off.text,
      step: 'type',
    });
  }, [readOnly]);

  const commit = useCallback(
    (kind: TagKindId, ref: string | null) => {
      if (!menu) return;
      // Overlapp lov på tvers av kinds, men ikke innen samme kind.
      if (overlapsSameKind(tags, menu.start, menu.end, kind)) {
        setMenu(null);
        window.getSelection()?.removeAllRanges();
        return;
      }
      onTag({ start: menu.start, end: menu.end, kind, ref });
      setActive(kind); // vis laget man nettopp tagget i
      setMenu(null);
      window.getSelection()?.removeAllRanges();
    },
    [menu, onTag, tags, setActive],
  );

  return (
    <div>
      {showLayerSwitch && (
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
      )}

      {/* Tekstflate — egen markeringslogikk */}
      <div
        ref={containerRef}
        onMouseUp={openMenu}
        onContextMenu={(e) => {
          if (!readOnly) {
            e.preventDefault();
            openMenu();
          }
        }}
        style={{
          fontSize: 'var(--ds-font-size-4)',
          lineHeight: 'var(--ds-line-height-lg)',
          userSelect: 'text',
        }}
      >
        {segments.map((s, i) =>
          s.kind ? (
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

      {/* 2-stegs kontekstmeny — Designsystemet Dropdown, kontrollert og manuelt posisjonert
          ved seleksjon (ikke via native popovertarget-anker, siden det ikke finnes noen synlig
          trigger-knapp å feste den til). */}
      {menu && (
        <Dropdown open onClose={() => setMenu(null)} style={{ position: 'fixed', left: menu.x, top: menu.y }}>
          {menu.step === 'type' ? (
            <>
              <Dropdown.Heading>Velg type</Dropdown.Heading>
              <Dropdown.List>
                {kinds.map((k) => (
                  <Dropdown.Item key={k.id}>
                    <Dropdown.Button onClick={() => setMenu({ ...menu, step: 'action', kind: k.id })}>
                      <span
                        style={{
                          width: 9,
                          height: 9,
                          borderRadius: 2,
                          flex: '0 0 auto',
                          background: `var(--ds-color-${k.color}-base-default)`,
                        }}
                      />
                      {k.label}
                    </Dropdown.Button>
                  </Dropdown.Item>
                ))}
              </Dropdown.List>
            </>
          ) : (
            <>
              <Dropdown.Heading>{kindById[menu.kind!]?.label}</Dropdown.Heading>
              <Dropdown.List>
                <Dropdown.Item>
                  <Dropdown.Button onClick={() => setMenu({ ...menu, step: 'type', kind: undefined })}>
                    ‹ Velg annen type
                  </Dropdown.Button>
                </Dropdown.Item>
                <Dropdown.Item>
                  <Dropdown.Button onClick={() => commit(menu.kind!, null)}>Ny tagg</Dropdown.Button>
                </Dropdown.Item>
                {(registry?.[menu.kind!] ?? []).map((cand) => (
                  <Dropdown.Item key={cand.ref}>
                    <Dropdown.Button onClick={() => commit(menu.kind!, cand.ref)}>{cand.label}</Dropdown.Button>
                  </Dropdown.Item>
                ))}
              </Dropdown.List>
            </>
          )}
        </Dropdown>
      )}

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
                <span
                  style={{
                    flex: 1,
                    minWidth: 0,
                    color: 'var(--ds-color-neutral-text-subtle)',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                    whiteSpace: 'nowrap',
                  }}
                >
                  «{text.slice(t.start, t.end)}»{t.ref ? ` → ${t.ref}` : ''}
                </span>
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
