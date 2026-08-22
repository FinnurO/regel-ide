import { useEffect, useMemo, useState } from 'react';

/**
 * Faste sidestørrelser (Johanns eksplisitte ønske: "f.eks 20, 50, 100, alle") — bevisst IKKE
 * fritekst, se docs/09-design-konvensjoner.md §9.
 */
export const SIDESTORRELSER = [20, 50, 100, 'alle'] as const;
export type Sidestorrelse = (typeof SIDESTORRELSER)[number];

export interface UsePagineringResultat<T> {
  /** Gjeldende side, 1-indeksert. Alltid gyldig (klemt til [1, totaltAntallSider]). */
  side: number;
  settSide: (side: number) => void;
  sidestorrelse: Sidestorrelse;
  /** Setter sidestørrelse OG nullstiller til side 1 (en ny sidestørrelse endrer sidetallingen). */
  settSidestorrelse: (sidestorrelse: Sidestorrelse) => void;
  totaltAntallSider: number;
  totaltAntallRader: number;
  /** Kun radene som skal rendres for gjeldende side — selve render-optimaliseringen. */
  visteRader: T[];
}

/**
 * Delt paginerings-hook for lange lister (rettskilder, tjenester, handlinger, virksomheter,
 * virksomhetskandidater — se docs/09-design-konvensjoner.md §9). Paginerer et allerede
 * FILTRERT OG SORTERT array — selve datasettet er fortsatt helt tilgjengelig for filtrering/
 * sortering, paginering er kun en render-optimalisering av VISNINGEN, ikke en serverside-
 * begrensning.
 *
 * `rader` bør være resultatet av kallerens egen `useMemo` for filter+sortering (samme mønster i
 * alle fem listesidene: `filterTekst`/`sortKolonne`/`sortStigende`). Når den memoiserte referansen
 * endrer seg (nytt filter, ny sortering, nye data hentet) nullstilles siden automatisk til 1 — uten
 * dette kunne brukeren blitt stående på en side som plutselig er tom fordi filtreringen endret
 * hvor mange treff det er.
 */
export function usePaginering<T>(rader: T[]): UsePagineringResultat<T> {
  const [side, setSide] = useState(1);
  const [sidestorrelse, setSidestorrelseRaw] = useState<Sidestorrelse>(20);

  // eslint-disable-next-line react-hooks/exhaustive-deps -- bevisst: skal KUN trigge når `rader`
  // (den memoiserte filter+sorterte listen) får en ny referanse, ikke ved andre re-renders.
  useEffect(() => {
    setSide(1);
  }, [rader]);

  const totaltAntallRader = rader.length;
  const totaltAntallSider =
    sidestorrelse === 'alle' ? 1 : Math.max(1, Math.ceil(totaltAntallRader / sidestorrelse));
  const gyldigSide = Math.min(Math.max(side, 1), totaltAntallSider);

  const visteRader = useMemo(() => {
    if (sidestorrelse === 'alle') return rader;
    const start = (gyldigSide - 1) * sidestorrelse;
    return rader.slice(start, start + sidestorrelse);
  }, [rader, sidestorrelse, gyldigSide]);

  function settSidestorrelse(ny: Sidestorrelse) {
    setSidestorrelseRaw(ny);
    setSide(1);
  }

  return {
    side: gyldigSide,
    settSide: setSide,
    sidestorrelse,
    settSidestorrelse,
    totaltAntallSider,
    totaltAntallRader,
    visteRader,
  };
}
