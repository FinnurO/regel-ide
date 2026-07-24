import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { api, hentValgtBrukerId, settValgtBrukerId } from '../api/client';
import type { BrukerDto } from '../api/types';

interface BrukerContextVerdi {
  brukere: BrukerDto[];
  gjeldendeBruker: BrukerDto | null;
  velgBruker: (brukerId: string | null) => void;
  laster: boolean;
}

const BrukerContext = createContext<BrukerContextVerdi | null>(null);

/**
 * Enkel testbruker-velger — IKKE ekte autentisering, se Bruker-kommentaren i
 * RegelIde.Data/Entiteter.cs. Erstattes av Ansattporten-innlogging senere.
 */
export function BrukerProvider({ children }: { children: ReactNode }) {
  const [brukere, setBrukere] = useState<BrukerDto[]>([]);
  const [gjeldendeBrukerId, setGjeldendeBrukerId] = useState<string | null>(hentValgtBrukerId());
  const [laster, setLaster] = useState(true);

  useEffect(() => {
    api
      .hentBrukere()
      .then((liste) => {
        setBrukere(liste);
        // Velg automatisk første testbruker hvis ingen er valgt ennå, slik at import fungerer med det samme.
        if (!hentValgtBrukerId() && liste.length > 0) {
          settValgtBrukerId(liste[0].id);
          setGjeldendeBrukerId(liste[0].id);
        }
      })
      .finally(() => setLaster(false));
  }, []);

  const velgBruker = (brukerId: string | null) => {
    settValgtBrukerId(brukerId);
    setGjeldendeBrukerId(brukerId);
  };

  const gjeldendeBruker = brukere.find((b) => b.id === gjeldendeBrukerId) ?? null;

  return (
    <BrukerContext.Provider value={{ brukere, gjeldendeBruker, velgBruker, laster }}>
      {children}
    </BrukerContext.Provider>
  );
}

export function useBruker() {
  const ctx = useContext(BrukerContext);
  if (!ctx) throw new Error('useBruker må brukes innenfor en BrukerProvider');
  return ctx;
}
