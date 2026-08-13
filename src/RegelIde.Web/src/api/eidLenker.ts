import type { RettskildeNodeDto, RettskildeSammendrag } from './types';

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

/**
 * Menneskelesbar visningstekst for en eId (punkt 7, avklaringsrunde 2026-08-13) —
 * `"{kortnavn ?? tittel} § {nummer} — {overskrift}"`, eller så mye av den formen som faktisk finnes
 * (nummer/overskrift utelates hver for seg når noden ikke har dem). Slår opp rettskilden via
 * {@link finnRettskildeForEid}, deretter noden med akkurat denne eId-en i kallerens (allerede
 * hentede) node-liste FOR DEN rettskilden — `noderPerRettskilde` er et Map fra rettskilde-id til
 * dens node-liste, siden en liste med referanser typisk peker på flere ulike rettskilder samtidig.
 *
 * Returnerer `undefined` når rettskilden eller noden ikke finnes/ennå ikke er hentet — ALDRI en
 * oppfunnet tekst ("ingen gjettet fallback"). Kalleren viser da rå eId som fallback, samme mønster
 * som {@link rettskildeLenke} allerede har.
 */
export function eidVisningstekst(
  eid: string,
  rettskilder: RettskildeSammendrag[],
  noderPerRettskilde: Map<string, RettskildeNodeDto[]>,
): string | undefined {
  const rettskilde = finnRettskildeForEid(eid, rettskilder);
  if (!rettskilde) return undefined;

  const node = noderPerRettskilde.get(rettskilde.id)?.find((n) => n.eid === eid);
  if (!node) return undefined;

  const kilde = rettskilde.kortnavn ?? rettskilde.tittel;
  const paragraf = node.nummer ? `§ ${node.nummer}` : null;
  const overskrift = node.overskrift ? `— ${node.overskrift}` : null;
  return [kilde, paragraf, overskrift].filter((del): del is string => del !== null).join(' ');
}
