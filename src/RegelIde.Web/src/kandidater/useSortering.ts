import { useState } from 'react';

/**
 * [Ny, kandidatside-runden, 2026-09-09, issue #216] Sorteringstilstanden for en kandidattabell:
 * hvilken kolonne, og hvilken retning.
 *
 * <p>
 * De tre kandidatsidene (`NavnekandidaterListe`, `VirksomhetKandidaterListe`, `Begrepskandidater`)
 * hadde hver sin IDENTISKE kopi av `bytteSortering` og `sorteringsindikator` — samme sju linjer,
 * tre steder. Det er ikke bare duplisering: det er tre steder å endre når sorteringen skal oppføre
 * seg annerledes, og tre steder å glemme. Sidene hadde alt begynt å sprike på andre felt (se
 * issuet), og dette er samme mekanisme tidligere i forløpet.
 * </p>
 *
 * <p>
 * Generisk over kolonnenøkkelen, siden de tre sidene sorterer på ulike kolonner — det er den ENESTE
 * reelle forskjellen mellom dem, og den beholdes i kallerens egen `Sorteringskolonne`-type.
 * </p>
 *
 * <p>
 * Selve SAMMENLIGNINGEN ligger bevisst IKKE her. Hva det betyr å sortere på «rettskilde» krever et
 * oppslag mot rettskildelisten, og på «konfidens» en rangordning — kunnskap som hører til siden,
 * ikke til en tilstandshook. Hooken eier hvilken kolonne og hvilken retning; kalleren eier hva det
 * betyr.
 * </p>
 */
export interface Sortering<TKolonne extends string> {
  kolonne: TKolonne;
  stigende: boolean;
  /** Klikk på en kolonneoverskrift: samme kolonne snur retningen, ny kolonne starter stigende. */
  bytt: (kolonne: TKolonne) => void;
  /** `' ▲'`/`' ▼'` for den aktive kolonnen, tom streng for de andre. */
  indikator: (kolonne: TKolonne) => string;
}

/**
 * @param startStigende Startretning. Alle tre kandidatsidene starter SYNKENDE på «opprettet» —
 * nyeste først er det riktige utgangspunktet for en arbeidskø. Et klikk på en ANNEN kolonne starter
 * alltid stigende, uavhengig av dette.
 */
export function useSortering<TKolonne extends string>(
  startKolonne: TKolonne,
  startStigende = true,
): Sortering<TKolonne> {
  const [kolonne, setKolonne] = useState<TKolonne>(startKolonne);
  const [stigende, setStigende] = useState(startStigende);

  return {
    kolonne,
    stigende,
    bytt: (ny) => {
      if (ny === kolonne) {
        setStigende((s) => !s);
      } else {
        setKolonne(ny);
        setStigende(true);
      }
    },
    indikator: (k) => (k !== kolonne ? '' : stigende ? ' ▲' : ' ▼'),
  };
}
