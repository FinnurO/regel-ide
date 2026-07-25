/**
 * RettskildeTre
 * ------------------------------------------------------------------
 * Hierarkisk navigator for en rettskilde (AKN). Fem nodenivåer med
 * distinkt visuell rytme, slik at dybden leses uten å telle innrykk:
 *
 *   kapittel        Seksjonsoverskrift. Versal, halvfet, egen skillelinje
 *                   over. Ikke klikkbar som innhold — kun ekspander.
 *   underinndeling  Romertall-inndeling i kapittel (AKN <hcontainer>).
 *                   Romertallet i en liten nøytral pille.
 *   paragraf        Den adresserbare bestemmelsen. §-nummeret i en
 *                   accent-chip (mono) + tittel i vanlig vekt. Dette er
 *                   nivået brukeren lenker til, så det er mest markert.
 *   ledd            Bærer løpeteksten. Nummermarkør i en liten sirkel,
 *                   tittel erstattes av et tekstutdrag (2 linjer).
 *   punkt           Bokstav-/tallpunkt. Markør «a)» i mono, dempet.
 *
 * Dybde markeres med BÅDE innrykk og en tynn ledelinje (guide rail), som
 * er det som gjør 4–5 nivåer lesbart. Opphevet paragraf: gjennomstreket
 * nummer, dempet tekst, «Opphevet»-tag, aldri barn.
 *
 * Opprinnelig utkast fra Claude Design; tilpasset her (2026-07-26):
 * `--ds-font-size-minus-1` (brukt for ledd/punkt-markørene) finnes ikke i
 * installert @digdir/designsystemet-theme (skalaen går kun 1–10, ingen
 * "minus"-varianter) — byttet til `--ds-font-size-1`, den minste reelle.
 *
 * DESIGNSYSTEMET-KOMPONENTER SOM BRUKES
 *   - Tag     → «Opphevet», kommentarteller, statusmerke
 * Selve tre-/tastaturlogikken finnes IKKE i DS og er egen kode her.
 *
 * TOKENS: kun --ds-*.
 */
import { useCallback, useMemo, useState } from 'react';
import { Tag } from '@digdir/designsystemet-react';

/* ------------------------------ typer ------------------------------ */

export type NodeType = 'kapittel' | 'underinndeling' | 'paragraf' | 'ledd' | 'punkt';

/** Status på virksomhetens kommentar knyttet til noden (ikke på lovteksten). */
export type KommentarStatus = 'under_arbeid' | 'til_godkjenning' | 'publisert' | 'ma_revideres';

export interface RettskildeNode {
  /** Stabil id, normalt AKN eId: kap_1 · par_1-7b · par_1-7b/ledd_1 · …/bokstav_a */
  eId: string;
  nodeType: NodeType;
  /** Markør: «Kapittel 1.», «II», «§ 1-7b», «1.», «a)» */
  merke: string;
  /** Overskrift. Finnes på kapittel/underinndeling/paragraf. */
  tittel?: string;
  /** Løpetekst. Finnes bare på ledd og punkt. */
  tekst?: string;
  /** Kun paragraf. Opphevet paragraf produseres som node, men uten barn. */
  opphevet?: boolean;
  /** Antall kommentarnoder festet på dette nivået. */
  antallKommentarer?: number;
  /** Status på kommentaren, hvis den finnes. */
  kommentarStatus?: KommentarStatus;
  children?: RettskildeNode[];
}

export interface RettskildeTreProps {
  nodes: RettskildeNode[];
  selectedEId?: string;
  onSelect?: (eId: string, node: RettskildeNode) => void;
  defaultExpanded?: string[];
  /** Vis kommentartellere/statusmerker til høyre. Default true. */
  visKommentarStatus?: boolean;
  /** Fritekstfilter. Skjuler noder som ikke matcher, men beholder forfedre. */
  filter?: string;
}

/* --------------------------- nivåstiler --------------------------- */

const INDENT: Record<NodeType, number> = {
  kapittel: 0, underinndeling: 1, paragraf: 1, ledd: 2, punkt: 3,
};

const STATUS_META: Record<KommentarStatus, { label: string; color: string }> = {
  under_arbeid: { label: 'Under arbeid', color: 'neutral' },
  til_godkjenning: { label: 'Til godkjenning', color: 'warning' },
  publisert: { label: 'Publisert', color: 'success' },
  ma_revideres: { label: 'Må revideres', color: 'danger' },
};

/** Rad-typografi per nivå — den visuelle rytmen som gjør dybden lesbar. */
function rowType(t: NodeType, opphevet?: boolean) {
  const dim = opphevet ? 'var(--ds-color-neutral-text-subtle)' : 'var(--ds-color-neutral-text-default)';
  switch (t) {
    case 'kapittel':
      return {
        fontSize: 'var(--ds-font-size-1)', fontWeight: 'var(--ds-font-weight-semibold)',
        letterSpacing: 'var(--ds-letter-spacing-1)', textTransform: 'uppercase' as const,
        color: 'var(--ds-color-neutral-text-subtle)',
      };
    case 'underinndeling':
      return { fontSize: 'var(--ds-font-size-2)', fontWeight: 'var(--ds-font-weight-medium)', color: dim };
    case 'paragraf':
      return { fontSize: 'var(--ds-font-size-2)', fontWeight: 'var(--ds-font-weight-medium)', color: dim };
    case 'ledd':
      return { fontSize: 'var(--ds-font-size-1)', fontWeight: 'var(--ds-font-weight-regular)', color: 'var(--ds-color-neutral-text-subtle)' };
    case 'punkt':
      return { fontSize: 'var(--ds-font-size-1)', fontWeight: 'var(--ds-font-weight-regular)', color: 'var(--ds-color-neutral-text-subtle)' };
  }
}

/** Markørens utseende per nivå. */
function markStyle(t: NodeType, opphevet?: boolean): React.CSSProperties {
  const base: React.CSSProperties = {
    flex: '0 0 auto', display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
    fontFamily: 'var(--ds-font-family)',
  };
  switch (t) {
    case 'underinndeling':
      return { ...base, minWidth: 22, padding: '1px 7px', borderRadius: 'var(--ds-border-radius-full)',
        background: 'var(--ds-color-neutral-surface-tinted)', color: 'var(--ds-color-neutral-text-subtle)',
        fontSize: 'var(--ds-font-size-1)', fontWeight: 'var(--ds-font-weight-semibold)' };
    case 'paragraf':
      return { ...base, padding: '1px 8px', borderRadius: 'var(--ds-border-radius-sm)',
        background: opphevet ? 'var(--ds-color-neutral-surface-tinted)' : 'var(--ds-color-accent-surface-tinted)',
        color: opphevet ? 'var(--ds-color-neutral-text-subtle)' : 'var(--ds-color-accent-text-default)',
        fontSize: 'var(--ds-font-size-1)', fontWeight: 'var(--ds-font-weight-semibold)',
        textDecoration: opphevet ? 'line-through' : 'none' };
    case 'ledd':
      return { ...base, width: 20, height: 20, borderRadius: 'var(--ds-border-radius-full)',
        border: '1px solid var(--ds-color-neutral-border-subtle)', color: 'var(--ds-color-neutral-text-subtle)',
        fontSize: 'var(--ds-font-size-1)' };
    case 'punkt':
      return { ...base, width: 20, color: 'var(--ds-color-neutral-text-subtle)',
        fontSize: 'var(--ds-font-size-1)', fontWeight: 'var(--ds-font-weight-medium)' };
    default:
      return { ...base, display: 'none' };
  }
}

/* --------------------------- komponent --------------------------- */

export function RettskildeTre({
  nodes, selectedEId, onSelect, defaultExpanded = [],
  visKommentarStatus = true, filter,
}: RettskildeTreProps) {
  const [expanded, setExpanded] = useState<Set<string>>(new Set(defaultExpanded));

  const toggle = useCallback((eId: string) => {
    setExpanded(prev => { const n = new Set(prev); n.has(eId) ? n.delete(eId) : n.add(eId); return n; });
  }, []);

  const match = useCallback((n: RettskildeNode): boolean => {
    if (!filter) return true;
    const q = filter.toLowerCase();
    const self = `${n.merke} ${n.tittel ?? ''} ${n.tekst ?? ''}`.toLowerCase().includes(q);
    return self || (n.children ?? []).some(match);
  }, [filter]);

  const rows = useMemo(() => {
    const out: RettskildeNode[] = [];
    const walk = (list: RettskildeNode[]) => {
      for (const n of list) {
        if (!match(n)) continue;
        out.push(n);
        // Opphevet paragraf har aldri barn; ledd/punkt vises når forelder er åpen.
        const open = expanded.has(n.eId) || !!filter;
        if (open && n.children?.length && !n.opphevet) walk(n.children);
      }
    };
    walk(nodes);
    return out;
  }, [nodes, expanded, filter, match]);

  return (
    <div role="tree" aria-label="Rettskildestruktur" style={{ padding: 'var(--ds-size-2)' }}>
      {rows.map(n => {
        const type = rowType(n.nodeType, n.opphevet)!;
        const depth = INDENT[n.nodeType];
        const isSel = n.eId === selectedEId;
        const hasChildren = !!n.children?.length && !n.opphevet;
        const open = expanded.has(n.eId);
        const status = n.kommentarStatus ? STATUS_META[n.kommentarStatus] : undefined;

        return (
          <div
            key={n.eId}
            role="treeitem"
            aria-selected={isSel}
            aria-expanded={hasChildren ? open : undefined}
            tabIndex={0}
            onClick={() => { if (hasChildren) toggle(n.eId); onSelect?.(n.eId, n); }}
            onKeyDown={e => {
              if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); if (hasChildren) toggle(n.eId); onSelect?.(n.eId, n); }
              if (e.key === 'ArrowRight' && hasChildren && !open) toggle(n.eId);
              if (e.key === 'ArrowLeft' && hasChildren && open) toggle(n.eId);
            }}
            style={{
              position: 'relative',
              display: 'flex', alignItems: n.nodeType === 'ledd' || n.nodeType === 'punkt' ? 'flex-start' : 'center',
              gap: 'var(--ds-size-2)',
              // Kapittel får luft over + skillelinje: markerer seksjonsstart.
              marginTop: n.nodeType === 'kapittel' ? 'var(--ds-size-4)' : 0,
              paddingTop: n.nodeType === 'kapittel' ? 'var(--ds-size-3)' : 'var(--ds-size-2)',
              paddingBottom: 'var(--ds-size-2)',
              paddingInlineStart: `calc(var(--ds-size-3) + ${depth} * var(--ds-size-5))`,
              paddingInlineEnd: 'var(--ds-size-2)',
              borderTop: n.nodeType === 'kapittel' ? '1px solid var(--ds-color-neutral-border-subtle)' : 'none',
              borderRadius: 'var(--ds-border-radius-default)',
              background: isSel ? 'var(--ds-color-accent-surface-tinted)' : 'transparent',
              cursor: 'pointer',
              opacity: n.opphevet ? 0.65 : 1,
            }}
          >
            {/* Ledelinje: gjør dype nivåer lesbare uten å telle innrykk */}
            {depth > 0 && (
              <span aria-hidden style={{
                position: 'absolute', insetBlock: 0,
                insetInlineStart: `calc(var(--ds-size-3) + ${depth - 1} * var(--ds-size-5) + var(--ds-size-2))`,
                width: 1, background: 'var(--ds-color-neutral-border-subtle)',
              }} />
            )}

            {/* Ekspandérindikator — kun der det finnes barn */}
            <span aria-hidden style={{
              width: 14, flex: '0 0 auto', color: 'var(--ds-color-neutral-text-subtle)',
              transform: open ? 'rotate(90deg)' : 'none', transition: 'transform .1s',
              visibility: hasChildren ? 'visible' : 'hidden', lineHeight: 1,
            }}>›</span>

            {/* Nivåspesifikk markør */}
            {n.nodeType !== 'kapittel' && (
              <span style={markStyle(n.nodeType, n.opphevet)}>{n.merke}</span>
            )}

            {/* Etikett: tittel for kapittel/underinndeling/paragraf, tekstutdrag for ledd/punkt */}
            <span style={{
              flex: 1, minWidth: 0, ...type,
              display: '-webkit-box', WebkitLineClamp: n.nodeType === 'ledd' || n.nodeType === 'punkt' ? 2 : 1,
              WebkitBoxOrient: 'vertical', overflow: 'hidden',
              lineHeight: 'var(--ds-line-height-sm)',
            }}>
              {n.nodeType === 'kapittel' ? `${n.merke} ${n.tittel ?? ''}`.trim() : (n.tittel ?? n.tekst ?? '')}
            </span>

            {n.opphevet && <Tag data-color="neutral" data-size="sm">Opphevet</Tag>}

            {visKommentarStatus && status && (
              <Tag data-color={status.color} data-size="sm">
                {n.antallKommentarer && n.antallKommentarer > 1 ? `${status.label} (${n.antallKommentarer})` : status.label}
              </Tag>
            )}
            {visKommentarStatus && !status && !!n.antallKommentarer && (
              <Tag data-color="brand1" data-size="sm">{n.antallKommentarer}</Tag>
            )}
          </div>
        );
      })}
    </div>
  );
}
