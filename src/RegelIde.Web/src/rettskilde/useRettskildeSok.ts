import { useMemo, useState } from 'react';
import type { RettskildeSammendrag } from '../api/types';

/** Øvre grense på antall treff mount'et som `<Suggestion.Option>` samtidig — se docs/09 §10. */
export const MAKS_RETTSKILDE_TREFF = 50;

/**
 * Delt "søk-FØR-mount"-filtrering, brukt av BEGGE `RettskildeFlervalg` og `RettskildeVelger`.
 * <para>
 * Grunnen dette finnes: `Suggestion` (som `Combobox`) mounter ALLE `<Suggestion.Option>`-barn i
 * DOM-en uansett — dens innebygde `filter`-prop virker ved å SKJULE ferdig-mount'ede options
 * (`option.disabled`), ikke ved å utelate dem fra treet. Docs/09 §9 dokumenterer et reelt
 * render-timeout med bare ~451 `<option>` (native `Select`); med 5893 rettskilder i dag (13× så
 * mange) er samme felle nesten garantert uten denne omveien. Løsningen: egen `sok`-state filtrerer
 * `rettskilder`-arrayet FØR rendering, og kun de første `MAKS_RETTSKILDE_TREFF` treffene sendes
 * videre til `<Suggestion.Option>` — ved tomt søk returneres et tomt array (null options mount'et),
 * ikke hele lista.
 * </para>
 */
export function useRettskildeSok(rettskilder: RettskildeSammendrag[]) {
  const [sok, setSok] = useState('');

  const alleTreff = useMemo(() => {
    const s = sok.trim().toLowerCase();
    if (!s) return [];
    return rettskilder.filter((r) => r.tittel.toLowerCase().includes(s));
  }, [rettskilder, sok]);

  return {
    sok,
    setSok,
    treff: alleTreff.slice(0, MAKS_RETTSKILDE_TREFF),
    alleTreffAntall: alleTreff.length,
  };
}
