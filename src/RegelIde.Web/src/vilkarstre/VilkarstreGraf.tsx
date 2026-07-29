/**
 * VilkarstreGraf
 * ------------------------------------------------------------------
 * DMN-DRD-stil graf over vilkårstreet (produktkrav kap. 3.4) — egen,
 * enkel SVG-komponent, samme filosofi som RettskildeTre.tsx/TagTekst.tsx
 * (kun --ds-*-tokens, ingen nytt npm-avhengighet). Automatisk lagdelt
 * topp-ned-layout: rotnoden øverst, dybde = avstand fra rot, X-spredning
 * per søsken-antall. IKKE dra-og-slipp i runde 1 — klikk for å velge node.
 *
 * Informasjonsflyt nedenfra og opp: piler fra barn (lenger ned) til
 * forelder (lenger opp), pilhode mot noden som krever input. Unntak
 * tegnes som en satellitt til side for sin gjelder_regel, med distinkt
 * (stiplet, varselfarget) kantstil.
 */
import { useMemo } from 'react';
import type { VilkarstreNode } from './bygging';

const COL_WIDTH = 190;
const ROW_HEIGHT = 130;
const NODE_WIDTH = 160;
const NODE_HEIGHT = 64;
const MARGIN = 40;

interface Posisjon { x: number; y: number }
interface Kant { fraId: string; tilId: string; type: 'barn' | 'unntak' }

interface LayoutResultat {
  posisjoner: Map<string, Posisjon>;
  kanter: Kant[];
  bredde: number;
  hoyde: number;
  alleNoder: Map<string, VilkarstreNode>;
}

function layout(root: VilkarstreNode): LayoutResultat {
  const posisjoner = new Map<string, Posisjon>();
  const kanter: Kant[] = [];
  const alleNoder = new Map<string, VilkarstreNode>();
  let nesteKolonne = 0;
  let maksDybde = 0;
  let maksX = 0;

  function plasserUnntak(node: VilkarstreNode, dybde: number, foreldreX: number) {
    node.unntak.forEach((u, i) => {
      const satelittX = foreldreX + NODE_WIDTH * 0.85 + i * (COL_WIDTH * 0.75);
      const satelittY = dybde * ROW_HEIGHT + ROW_HEIGHT * 0.35;
      posisjoner.set(u.betingelse.id, { x: satelittX, y: satelittY });
      alleNoder.set(u.betingelse.id, u.betingelse);
      kanter.push({ fraId: node.id, tilId: u.betingelse.id, type: 'unntak' });
      maksX = Math.max(maksX, satelittX);
    });
  }

  function plasser(node: VilkarstreNode, dybde: number): number {
    alleNoder.set(node.id, node);
    maksDybde = Math.max(maksDybde, dybde);

    if (node.children.length === 0) {
      const x = nesteKolonne++ * COL_WIDTH;
      posisjoner.set(node.id, { x, y: dybde * ROW_HEIGHT });
      plasserUnntak(node, dybde, x);
      maksX = Math.max(maksX, x);
      return x;
    }

    const barnX = node.children.map((c) => {
      kanter.push({ fraId: c.id, tilId: node.id, type: 'barn' });
      return plasser(c, dybde + 1);
    });
    const x = (Math.min(...barnX) + Math.max(...barnX)) / 2;
    posisjoner.set(node.id, { x, y: dybde * ROW_HEIGHT });
    plasserUnntak(node, dybde, x);
    maksX = Math.max(maksX, x);
    return x;
  }

  plasser(root, 0);

  return {
    posisjoner, kanter, alleNoder,
    bredde: maksX + NODE_WIDTH + MARGIN * 2,
    hoyde: (maksDybde + 1) * ROW_HEIGHT + NODE_HEIGHT * 0.4 + MARGIN * 2,
  };
}

const KIND_FARGE: Record<string, { bg: string; border: string; tekst: string }> = {
  regelnode: {
    bg: 'var(--ds-color-accent-surface-tinted)',
    border: 'var(--ds-color-accent-border-strong)',
    tekst: 'var(--ds-color-accent-text-default)',
  },
  vilkar: {
    bg: 'var(--ds-color-info-surface-tinted)',
    border: 'var(--ds-color-info-border-strong)',
    tekst: 'var(--ds-color-info-text-default)',
  },
};

interface VilkarstreGrafProps {
  root: VilkarstreNode;
  valgtId?: string;
  onSelect: (node: VilkarstreNode) => void;
}

export function VilkarstreGraf({ root, valgtId, onSelect }: VilkarstreGrafProps) {
  const { posisjoner, kanter, bredde, hoyde, alleNoder } = useMemo(() => layout(root), [root]);

  return (
    <svg
      viewBox={`${-MARGIN} ${-MARGIN} ${bredde} ${hoyde}`}
      style={{ width: '100%', height: 'auto', maxHeight: '65vh', fontFamily: 'var(--ds-font-family)' }}
      role="img"
      aria-label="Graf over vilkårstreet"
    >
      <defs>
        <marker id="vt-pil" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
          <path d="M0,0 L10,5 L0,10 z" fill="var(--ds-color-neutral-text-subtle)" />
        </marker>
        <marker id="vt-pil-unntak" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
          <path d="M0,0 L10,5 L0,10 z" fill="var(--ds-color-warning-base-default)" />
        </marker>
      </defs>

      {kanter.map((k) => {
        const fra = posisjoner.get(k.fraId)!;
        const til = posisjoner.get(k.tilId)!;
        const erUnntak = k.type === 'unntak';
        // barn: fra barnets topp til forelderens bunn (informasjonsflyt nedenfra og opp).
        // unntak: fra regelnodens høyre midtpunkt til betingelsens venstre midtpunkt.
        const x1 = erUnntak ? fra.x + NODE_WIDTH : fra.x + NODE_WIDTH / 2;
        const y1 = erUnntak ? fra.y + NODE_HEIGHT / 2 : fra.y;
        const x2 = erUnntak ? til.x : til.x + NODE_WIDTH / 2;
        const y2 = erUnntak ? til.y + NODE_HEIGHT / 2 : til.y + NODE_HEIGHT;
        return (
          <line
            key={`${k.fraId}-${k.tilId}`}
            x1={x1} y1={y1} x2={x2} y2={y2}
            stroke={erUnntak ? 'var(--ds-color-warning-base-default)' : 'var(--ds-color-neutral-border-strong)'}
            strokeWidth={2}
            strokeDasharray={erUnntak ? '6 4' : undefined}
            markerEnd={erUnntak ? 'url(#vt-pil-unntak)' : 'url(#vt-pil)'}
          />
        );
      })}

      {[...alleNoder.values()].map((node, i) => {
        const pos = posisjoner.get(node.id)!;
        const farge = KIND_FARGE[node.kind];
        const valgt = node.id === valgtId;
        return (
          <g
            key={node.id}
            transform={`translate(${pos.x}, ${pos.y})`}
            onClick={() => onSelect(node)}
            style={{ cursor: 'pointer' }}
            role="button"
            tabIndex={0}
            aria-label={node.tittel}
            onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') onSelect(node); }}
          >
            <rect
              width={NODE_WIDTH} height={NODE_HEIGHT} rx={8}
              fill={farge.bg}
              stroke={valgt ? 'var(--ds-color-accent-base-default)' : farge.border}
              strokeWidth={valgt ? 3 : node.erRotnode ? 2.5 : 1.5}
            />
            <text x={10} y={18} fontSize={10} fill="var(--ds-color-neutral-text-subtle)">{i + 1}</text>
            <text x={NODE_WIDTH / 2} y={28} textAnchor="middle" fontSize={12} fontWeight={600} fill={farge.tekst}>
              {kortTekst(node.tittel, 22)}
            </text>
            <text x={NODE_WIDTH / 2} y={46} textAnchor="middle" fontSize={10} fill="var(--ds-color-neutral-text-subtle)">
              {node.kind === 'regelnode'
                ? `${node.erRotnode ? 'Rotnode · ' : ''}${node.barnOperator}`
                : node.erFormel ? 'Formel' : node.vurderingstype === 'skjonnsbasert' ? 'Skjønn' : node.vilkarstype}
            </text>
          </g>
        );
      })}
    </svg>
  );
}

function kortTekst(s: string, maks: number) {
  return s.length > maks ? `${s.slice(0, maks - 1)}…` : s;
}
