import type { RettskildeSammendrag } from './types';

/** En eId er alltid `{rettskilde.eli}/{sti}` — ingen eget oppslags-endepunkt nødvendig. */
export function finnRettskildeForEid(
  eid: string,
  rettskilder: RettskildeSammendrag[],
): RettskildeSammendrag | undefined {
  return rettskilder.find((r) => r.eli && eid.startsWith(r.eli));
}

export function rettskildeLenke(eid: string, rettskilder: RettskildeSammendrag[]): string | undefined {
  const rettskilde = finnRettskildeForEid(eid, rettskilder);
  return rettskilde ? `/rettskilder/${rettskilde.id}?eid=${encodeURIComponent(eid)}` : undefined;
}
