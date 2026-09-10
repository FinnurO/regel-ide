import { useState } from 'react';

/**
 * [Ny, kandidatside-runden, 2026-09-09, issue #216] Avkryssingstilstanden for en kandidattabell.
 *
 * <p>
 * Delt av de tre kandidatsidene, som hadde hver sin kopi. Merk at de to semantikkene under er
 * BEVISST ulike, og at forskjellen er dokumentert der den ble bestemt:
 * </p>
 *
 * <ul>
 *   <li><b>`velgAlleViste`</b> NULLSTILLER hele utvalget ved avhukning. «Alle viste» er en
 *       hovedbryter for gjeldende side, og å skru den av betyr «ingenting valgt».</li>
 *   <li><b>`vekslGruppe`</b> gjør det IKKE — den legger til eller fjerner bare gruppens egne rader,
 *       og lar resten av utvalget stå. En gruppe er et delsett, ikke en hovedbryter. Johanns krav var
 *       dessuten at den skal treffe HELE gruppen, også de radene som er kollapset bort.</li>
 * </ul>
 *
 * <p>
 * Ingen av sidene skal finne opp sin egen variant av dette igjen: at «velg alle» og «velg gruppe»
 * oppfører seg ulikt er en avgjørelse, ikke en tilfeldighet, og den bor nå på ett sted.
 * </p>
 */
export interface Kandidatvalg {
  valgte: Set<string>;
  antall: number;
  erValgt: (id: string) => boolean;
  veksl: (id: string, valgt: boolean) => void;
  /** Hovedbryter for de viste radene. Avhukning nullstiller HELE utvalget — se klassekommentaren. */
  velgAlleViste: (viste: readonly { id: string }[], valgt: boolean) => void;
  /** Legger til/fjerner en gruppes rader UTEN å røre resten av utvalget. */
  vekslGruppe: (rader: readonly { id: string }[], valgt: boolean) => void;
  /** True når det finnes viste rader OG alle er valgt — tilstanden «alle viste»-boksen skal vise. */
  alleVisteErValgt: (viste: readonly { id: string }[]) => boolean;
  nullstill: () => void;
}

export function useKandidatvalg(): Kandidatvalg {
  const [valgte, setValgte] = useState<Set<string>>(new Set());

  return {
    valgte,
    antall: valgte.size,
    erValgt: (id) => valgte.has(id),
    veksl: (id, valgt) => setValgte((forrige) => {
      const ny = new Set(forrige);
      if (valgt) ny.add(id);
      else ny.delete(id);
      return ny;
    }),
    velgAlleViste: (viste, valgt) => setValgte(valgt ? new Set(viste.map((r) => r.id)) : new Set()),
    vekslGruppe: (rader, valgt) => setValgte((forrige) => {
      const ny = new Set(forrige);
      for (const r of rader) {
        if (valgt) ny.add(r.id);
        else ny.delete(r.id);
      }
      return ny;
    }),
    alleVisteErValgt: (viste) => viste.length > 0 && viste.every((r) => valgte.has(r.id)),
    nullstill: () => setValgte(new Set()),
  };
}
