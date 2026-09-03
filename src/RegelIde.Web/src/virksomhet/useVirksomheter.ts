import { useCallback, useEffect, useMemo, useState } from 'react';
import { api } from '../api/client';
import type { VirksomhetDto } from '../api/types';

interface VirksomheterVerdi {
  virksomheter: VirksomhetDto[];
  virksomheterPerId: Map<string, VirksomhetDto>;
  laster: boolean;
  /**
   * `null`/`undefined` → "Delt / nasjonal" (nasjonal/delt rettskilde, samme betydning som
   * `RettskildeEntitet.VirksomhetId == null` osv. — se Entiteter.cs). Satt, men (ennå) ikke
   * funnet i den hentede listen → rå GUID-en, ALDRI et oppfunnet navn ("ingen gjettet
   * fallback" — samme prinsipp som `rettskildeLenke`/`eidVisningstekst` i eidLenker.ts).
   */
  visEier: (virksomhetId: string | null | undefined) => string;
  /** [Ny, 2026-08-29] Henter listen på nytt — brukt etter at en ny virksomhet er opprettet fra Brreg
   * (se VirksomheterListe.tsx), siden hooken ellers kun henter én gang ved mount. */
  oppdater: () => void;
}

/**
 * Henter `/api/virksomheter` ÉN gang per bruk og eksponerer et oppslag på Id → navn. Erstatter tre
 * tidligere spredte `api.hentVirksomheter().then((liste) => liste.find(...))`-mønstre
 * (BegrepDetalj.tsx, DatasettDetalj.tsx, TjenesteVeiledning.tsx) og badge-fallbacken i
 * RettskilderListe.tsx, med ett sted for regelen "eier vises som navn, aldri som rå GUID eller
 * en «Virksomhetseid»-badge uten navn".
 *
 * Bevisst en enkel hook med eget lokalt state (ikke en React Context/Provider) — virksomhetslisten
 * er liten (åpne data, ingen tilgangssperre) og endres sjelden i én sesjon, samme
 * "ingen unødvendig abstraksjon"-linje som resten av kodebasen (jf. BrukerContext, som derimot
 * legitimt ER en Context siden gjeldende bruker faktisk må deles/synkroniseres appen over).
 */
export function useVirksomheter(): VirksomheterVerdi {
  const [virksomheter, setVirksomheter] = useState<VirksomhetDto[]>([]);
  const [laster, setLaster] = useState(true);

  function hent() {
    setLaster(true);
    api
      .hentVirksomheter()
      .then(setVirksomheter)
      .catch(() => setVirksomheter([]))
      .finally(() => setLaster(false));
  }

  useEffect(hent, []);

  // Memoisert/stabilisert (ikke gjenoppbygd på hver render) — se usePaginering.ts sin
  // reset-til-side-1-effekt, som nullstiller siden når `viste`-arrayen (bygget med disse i
  // dependency-arrayet hos forbrukere) får ny referanse. Se GitHub-issue #145.
  const virksomheterPerId = useMemo(
    () => new Map(virksomheter.map((v) => [v.id, v] as const)),
    [virksomheter],
  );

  const visEier = useCallback(
    (virksomhetId: string | null | undefined): string => {
      if (!virksomhetId) return 'Delt / nasjonal';
      if (laster) return '…';
      return virksomheterPerId.get(virksomhetId)?.navn ?? virksomhetId;
    },
    [virksomheterPerId, laster],
  );

  return { virksomheter, virksomheterPerId, laster, visEier, oppdater: hent };
}
