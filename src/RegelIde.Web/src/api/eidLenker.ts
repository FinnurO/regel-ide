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
 * [Rettet, 2026-08-30] Samme lenke som {@link rettskildeLenke}, men for kallere som ALLEREDE vet
 * hvilken rettskilde eId-en hører til (typisk en kandidatrad med sin egen `rettskildeId`-felt) —
 * bygger lenken direkte i stedet for å lete etter en rettskilde hvis ELI er PREFIKS av eId-en.
 * Nødvendig fordi den ELI-prefiks-antakelsen ikke er universell: `LovdataIdentifikatorer.KapittelEid`
 * bygger bevisst en eId "uavhengig av rettskildens ELI" (kap-/rom-/punkt-nummererte dokumenter, f.eks.
 * instrukser) — Johann observerte at slike noder ikke ble lenket til (viste rå eId som tekst) i
 * Navnekandidater-/Virksomhetskandidater-listene, fordi den generelle {@link rettskildeLenke} da
 * ikke fant NOEN rettskilde hvis ELI var prefiks av "kap-I/rom-3/punkt-2". Her trengs ikke det
 * søket i det hele tatt — rettskildeId er allerede kjent.
 */
export function rettskildeLenkeForId(rettskildeId: string, eid: string): string {
  return `/rettskilder/${rettskildeId}?eid=${encodeURIComponent(eid)}`;
}

/**
 * Menneskelesbar visningstekst for en eId (punkt 7, avklaringsrunde 2026-08-13; RETTET 2026-09-03,
 * issue #151 — brukte tidligere `kortnavn ?? tittel`, Johann ønsker ALLTID den fulle `tittel`, aldri
 * den forkortede `kortnavn`) —
 * `"{tittel} § {nummer} — {overskrift}"`, eller så mye av den formen som faktisk finnes
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

  const kilde = rettskilde.tittel;
  const paragraf = node.nummer ? `§ ${node.nummer}` : null;
  const overskrift = node.overskrift ? `— ${node.overskrift}` : null;
  return [kilde, paragraf, overskrift].filter((del): del is string => del !== null).join(' ');
}
