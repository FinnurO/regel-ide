import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { ApiError, api, hentValgtBrukerId, settValgtBrukerId } from '../api/client';
import type { BrukerDto } from '../api/types';

interface BrukerContextVerdi {
  /** KUN testbrukere (ErAltinnBruker=false) — brukervelgeren skal aldri liste ekte identiteter. */
  brukere: BrukerDto[];
  gjeldendeBruker: BrukerDto | null;
  velgBruker: (brukerId: string | null) => void;
  laster: boolean;
  /** Om serveren kjører med ekte innlogging. Da skal brukervelgeren ikke vises. */
  ekteInnlogging: boolean;
  /** Satt når vi er innlogget, men serveren ikke fant noen brukerkonto å knytte oss til. */
  innloggingsfeil: string | null;
  /**
   * Henter testbrukerlisten på nytt — kalles fra brukerhåndteringssiden etter opprett/rediger, slik
   * at identitetsbrikken (som får sin liste én gang ved oppstart) viser en nyopprettet bruker uten
   * at hele siden må lastes på nytt.
   */
  lastBrukerePaNytt: () => Promise<void>;
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

  /**
   * Henter/oppdaterer testbrukerlisten. Filtrerer bort ErAltinnBruker-rader — /api/brukere lister nå
   * ALLE brukere (se GjeldendeBrukerTjeneste.cs), men brukervelgeren her skal kun tilby testbrukere.
   */
  async function lastBrukerePaNytt() {
    const liste = await api.hentBrukere();
    const testbrukere = liste.filter((b) => !b.erAltinnBruker);
    setBrukere(testbrukere);
    // Velg automatisk første testbruker hvis ingen er valgt ennå, slik at import fungerer med det samme.
    if (!hentValgtBrukerId() && testbrukere.length > 0) {
      settValgtBrukerId(testbrukere[0].id);
      setGjeldendeBrukerId(testbrukere[0].id);
    }
  }

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

        await lastBrukerePaNytt();
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
      value={{ brukere, gjeldendeBruker, velgBruker, laster, ekteInnlogging, innloggingsfeil, lastBrukerePaNytt }}
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
