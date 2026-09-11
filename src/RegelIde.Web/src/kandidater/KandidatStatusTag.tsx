import { Tag } from '@digdir/designsystemet-react';

/**
 * [Ny, issue #267, 2026-09-11] Venter/Godkjent/Avvist-status på en kandidatkø-rad.
 *
 * <p>
 * Funnet duplisert IDENTISK i fire filer (`BegrepDefinisjonRelasjonKo.tsx`, `Begrepskandidater.tsx`,
 * `NavnekandidaterListe.tsx`, `VirksomhetKandidaterListe.tsx`) — samme farge-map
 * (`Venter: warning, Godkjent: success, Avvist: danger`), samme fallback (`neutral` for ukjent
 * status). Konsolidert til én kilde her.
 * </p>
 *
 * <p>
 * <b>Bevisst IKKE slått sammen med</b> de to andre stedene en konstant med samme NAVN
 * (`STATUS_FARGE`) fantes: `AdministrasjonLovdataResynk.tsx` (`Pågår/Fullført/Feilet` — en
 * synkroniseringsjobbs status) og `KildefeilListe.tsx` (`Ny/Kjent/Rettet-hos-oss/Venter-på-Lovdata`
 * — en feilkøs status). Samme variabelnavn ble valgt uavhengig tre steder, men nøkkelrommene og
 * betydningen er reelt ulike domener — å tvinge dem inn i denne komponenten ville skjule det, ikke
 * fjerne duplisering (docs/09 §25).
 * </p>
 */
export function KandidatStatusTag({ status, tekst }: { status: string; tekst?: string }) {
  const farge: 'neutral' | 'warning' | 'success' | 'danger' =
    status === 'Venter' ? 'warning' : status === 'Godkjent' ? 'success' : status === 'Avvist' ? 'danger' : 'neutral';
  return (
    <Tag data-color={farge} data-size="sm">
      {tekst ?? status}
    </Tag>
  );
}
