import type { RettskildeNodeDto } from '../api/types';

/**
 * [Ny, nemnd/sekretariat-runden, 2026-09-09] «§ 36 sjette ledd» for en eId som peker på et LEDD
 * eller PUNKT.
 *
 * Hvorfor denne finnes ved siden av {@link eidVisningstekst}: den bruker nodens EGET `nummer`, og et
 * ledds nummer er leddnummeret. For konkurranseloven § 36 sjette ledd ga den derfor «§ 6» — feil
 * paragraf, og verre enn å vise rå eId, siden den ser riktig ut. Her klatres det opp til
 * paragrafnoden via `parentNodeId` før nummeret leses.
 *
 * Returnerer `undefined` når nodene ikke er hentet ennå, eller når det ikke finnes noen paragrafnode
 * over noden — ingen gjettet etikett. Kalleren viser da rå eId (samme mønster som resten av appen).
 */
export function paragrafEtikett(
  noder: RettskildeNodeDto[] | undefined,
  eid: string,
): { tekst: string; overskrift: string | null } | undefined {
  const node = noder?.find((n) => n.eid === eid);
  if (!noder || !node) return undefined;

  let paragraf = node;
  const besokt = new Set<string>();
  while (paragraf.nodeType !== 'paragraf' && paragraf.parentNodeId && !besokt.has(paragraf.id)) {
    besokt.add(paragraf.id);
    const forelder = noder.find((n) => n.id === paragraf.parentNodeId);
    if (!forelder) break;
    paragraf = forelder;
  }
  if (paragraf.nodeType !== 'paragraf' || !paragraf.nummer) return undefined;

  // Lovdata-importen legger «§» inn i paragrafnodens nummer («§ 36»), men ikke i leddets («6»).
  const paragrafnummer = paragraf.nummer.startsWith('§') ? paragraf.nummer : `§ ${paragraf.nummer}`;
  return {
    tekst: `${paragrafnummer}${leddDel(node)}`,
    overskrift: paragraf.overskrift ?? null,
  };
}

/**
 * Ordenstall opp til tiende ledd. Over det skrives «ledd 11» — en bestemmelse med elleve ledd finnes
 * knapt, og «ellevte» i en etikett ingen leser er ikke verdt en lengre tabell.
 */
const LEDD_ORDENSTALL = ['', 'første', 'andre', 'tredje', 'fjerde', 'femte', 'sjette', 'sjuende',
  'åttende', 'niende', 'tiende'];

function leddDel(node: RettskildeNodeDto): string {
  if (!node.nummer) return '';
  if (node.nodeType === 'ledd') {
    const ordenstall = LEDD_ORDENSTALL[Number(node.nummer)];
    return ordenstall ? ` ${ordenstall} ledd` : ` ledd ${node.nummer}`;
  }
  if (node.nodeType === 'punkt') return ` punkt ${node.nummer}`;
  return '';
}
