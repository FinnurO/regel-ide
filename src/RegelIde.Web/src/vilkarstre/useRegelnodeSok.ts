import { useMemo, useState } from 'react';
import type { RegelnodeDto } from '../api/types';

/** Øvre grense på antall treff mount'et som `<Suggestion.Option>` samtidig — se docs/09 §10. */
export const MAKS_REGELNODE_TREFF = 50;

/**
 * Samme "søk-FØR-mount"-teknikk som `rettskilde/useRettskildeSok.ts` (docs/09 §10) — brukt av
 * `RegelnodeVelger` for "Bytt til eksisterende regelnode" i Vilkårstre-fanen (Tjenestedetalj-
 * redesignrunden 2026-08-27). Erstatter et rått `<Select>` over ALLE regelnoder i systemet.
 */
export function useRegelnodeSok(regelnoder: RegelnodeDto[]) {
  const [sok, setSok] = useState('');

  const alleTreff = useMemo(() => {
    const s = sok.trim().toLowerCase();
    if (!s) return [];
    return regelnoder.filter((r) => r.tittel.toLowerCase().includes(s));
  }, [regelnoder, sok]);

  return {
    sok,
    setSok,
    treff: alleTreff.slice(0, MAKS_REGELNODE_TREFF),
    alleTreffAntall: alleTreff.length,
  };
}
