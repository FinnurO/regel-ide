import { useEffect, useMemo, useState } from 'react';
import { api } from '../api/client';
import type { RettskildeNodeDto, RettskildeSammendrag } from '../api/types';
import { paragrafEtikett } from '../rettskilde/paragrafEtikett';

/**
 * [Ny, kandidatside-runden, 2026-09-09, issue #216] Node- og rettskildevisning for de tre
 * kandidatsidene.
 *
 * <p>
 * Alle tre hadde sin egen kopi av BÅDE den late node-hentingen og etikettbyggingen, og alle tre
 * bygde etiketten på samme GALE måte: `§ ${node.nummer}`. Et ledds `nummer` ER leddnummeret, så
 * konkurranseloven § 36 sjette ledd ble vist som «§ 6» — feil paragraf, og verre enn rå eId fordi
 * den ser riktig ut. Lovdata-importen legger dessuten «§» inn i paragrafnodens eget nummer, så
 * paragraftreff ble «§ § 36».
 * </p>
 *
 * <p>
 * Rettelsen fantes allerede i {@link paragrafEtikett} (den klatrer opp til paragrafnoden via
 * `parentNodeId`), men bare `VirksomhetDetalj`/`RettskildeDetalj` brukte den. Nå går alle tre
 * kandidatsidene samme vei — issue #216 punkt 5.
 * </p>
 *
 * <p>
 * Hentingen skjer bevisst KUN for rettskildene bak de VISTE radene, ikke hele treffsettet: med
 * case-insensitiv sveip kan én virksomhet ha kandidater spredt over hundrevis av ulike rettskilder
 * (373 treff for «fylkeskommune»), og å hente noder for alle var en observert render-regresjon.
 * Ett kall per DISTINKT rettskilde blant de viste radene, aldri per rad.
 * </p>
 */
export interface NodeEtiketter {
  /** Nodene for én rettskilde, eller `undefined` når de ikke er hentet ennå. */
  noderFor: (rettskildeId: string) => RettskildeNodeDto[] | undefined;
  /** Selve noden et treff peker på — trengs der teksten skal skjæres ut med start/end-offset. */
  node: (rettskildeId: string, nodeEid: string) => RettskildeNodeDto | undefined;
  /** «§ 36 sjette ledd — Overskrift», eller rå eId når nodene ikke er hentet / ingen paragraf finnes. */
  etikett: (rettskildeId: string, nodeEid: string) => string;
}

/** Ren regel, testet uten React: etikett med overskrift, eller rå eId når det ikke finnes noe bedre. */
export function nodeEtikett(noder: RettskildeNodeDto[] | undefined, nodeEid: string): string {
  const etikett = paragrafEtikett(noder, nodeEid);
  if (!etikett) return nodeEid;
  return etikett.overskrift ? `${etikett.tekst} — ${etikett.overskrift}` : etikett.tekst;
}

export function useNodeEtiketter(synligeRader: readonly { rettskildeId: string }[]): NodeEtiketter {
  const [noderPerRettskilde, setNoderPerRettskilde] = useState<Map<string, RettskildeNodeDto[]>>(new Map());

  useEffect(() => {
    for (const rettskildeId of new Set(synligeRader.map((r) => r.rettskildeId))) {
      if (noderPerRettskilde.has(rettskildeId)) continue;
      api.hentNoder(rettskildeId)
        .then((noder) => setNoderPerRettskilde((forrige) => new Map(forrige).set(rettskildeId, noder)))
        .catch(() => {}); // ingen gjettet fallback — kalleren viser rå eId når nodene ikke lot seg hente
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [synligeRader]);

  return {
    noderFor: (rettskildeId) => noderPerRettskilde.get(rettskildeId),
    node: (rettskildeId, nodeEid) => noderPerRettskilde.get(rettskildeId)?.find((n) => n.eid === nodeEid),
    etikett: (rettskildeId, nodeEid) => nodeEtikett(noderPerRettskilde.get(rettskildeId), nodeEid),
  };
}

/**
 * [Ny, kandidatside-runden, 2026-09-09, issue #216] Oppslag fra rettskildeId til tittel/departement,
 * den tredje identiske kopien på de tre sidene. Faller tilbake til id-en selv — en rå GUID i
 * kolonnen er ærligere enn en tom celle når rettskildelista ikke er lastet.
 */
export function useRettskildeoppslag(rettskilder: RettskildeSammendrag[]) {
  const perId = useMemo(() => new Map(rettskilder.map((r) => [r.id, r] as const)), [rettskilder]);
  return {
    perId,
    tittel: (rettskildeId: string) => perId.get(rettskildeId)?.tittel ?? rettskildeId,
    ansvarligDepartement: (rettskildeId: string) => perId.get(rettskildeId)?.ansvarligDepartement ?? null,
  };
}
