import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { ApiError, api, hentValgtBrukerId, settValgtBrukerId } from '../api/client';
import type { BrukerDto } from '../api/types';

interface BrukerContextVerdi {
  brukere: BrukerDto[];
  gjeldendeBruker: BrukerDto | null;
  velgBruker: (brukerId: string | null) => void;
  laster: boolean;
  /** Om serveren kjører med ekte innlogging. Da skal brukervelgeren ikke vises. */
  ekteInnlogging: boolean;
  /** Satt når vi er innlogget, men serveren ikke fant noen brukerkonto å knytte oss til. */
  innloggingsfeil: string | null;
}

const BrukerContext = createContext<BrukerContextVerdi | null>(null);

/**
 * Henter gjeldende bruker. Under testbruker-profilen er dette en velger over seedede brukere
 * (IKKE autentisering); under Altinn-profilen er brukeren gitt av innloggingen og lista tom.
 * Serveren bestemmer hvilken av delene via /api/oppsett — se Autentiseringsoppsett.cs.
 */
export function BrukerProvider({ children }: { children: ReactNode }) {
  const [brukere, setBrukere] = useState<BrukerDto[]>([]);
  const [gjeldendeBrukerId, setGjeldendeBrukerId] = useState<string | null>(hentValgtBrukerId());
  const [innloggetBruker, setInnloggetBruker] = useState<BrukerDto | null>(null);
  const [ekteInnlogging, setEkteInnlogging] = useState(false);
  const [innloggingsfeil, setInnloggingsfeil] = useState<string | null>(null);
  const [laster, setLaster] = useState(true);

  useEffect(() => {
    api
      .hentOppsett()
      .then(async (oppsett) => {
        if (oppsett.autentisering === 'altinn') {
          setEkteInnlogging(true);
          try {
            setInnloggetBruker(await api.hentMeg());
          } catch (feil) {
            // Selve dokumentet ble servert, så serveren godtok cookien — kom vi likevel hit,
            // manglet claimet vi identifiserer brukeren med. Vi laster IKKE siden på nytt: det
            // ville gitt en evig runddans, siden en ny innlogging ikke ville endret claimene.
            // /api/meg/claims viser hva tokenet faktisk inneholder (krever VisClaims=true).
            if (feil instanceof ApiError && feil.status === 401) {
              setInnloggingsfeil(feil.message);
              return;
            }
            throw feil;
          }
          return;
        }

        const liste = await api.hentBrukere();
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

  const gjeldendeBruker = ekteInnlogging
    ? innloggetBruker
    : (brukere.find((b) => b.id === gjeldendeBrukerId) ?? null);

  return (
    <BrukerContext.Provider
      value={{ brukere, gjeldendeBruker, velgBruker, laster, ekteInnlogging, innloggingsfeil }}
    >
      {children}
    </BrukerContext.Provider>
  );
}

export function useBruker() {
  const ctx = useContext(BrukerContext);
  if (!ctx) throw new Error('useBruker må brukes innenfor en BrukerProvider');
  return ctx;
}
