import { Combobox } from '@digdir/designsystemet-react';
import type { VirksomhetDto } from '../api/types';

export interface VirksomhetVelgerProps {
  virksomheter: VirksomhetDto[];
  /** Valgt virksomhetId, eller '' for "ingen/alle" (tekst styrt av `tomValgTekst`). */
  value: string;
  onChange: (virksomhetId: string) => void;
  label: string;
  /** Teksten på det tomme valget øverst i lista, f.eks. "Alle virksomheter" eller "Velg virksomhet …". */
  tomValgTekst: string;
  hideLabel?: boolean;
  style?: React.CSSProperties;
}

/**
 * Søkbar erstatning for et `<Select>` med ÉN `<option>` pr. virksomhet (docs/09-design-
 * konvensjoner.md §9). Med ~451 virksomheter er en render-alle-som-`<option>`-`<Select>` selve
 * ytelsesproblemet — Designsystemets `Combobox` løser dette FORDI den er søkbar (bruker skriver,
 * lista filtreres), ikke fordi den er paginert (den er det ikke, og trenger ikke være det — se
 * `usePaginering.ts`/`Pagineringskontroll.tsx` for det separate tabellrad-problemet).
 *
 * Egen `filter` er nødvendig: standardfilteret i Combobox matcher mot OPTION-VERDIEN
 * (`option.value`, her virksomhetens GUID), ikke visningsteksten — uten dette ville brukeren måtte
 * skrive inn en GUID for å finne noe. Vi filtrerer på `option.label` (navnet) i stedet.
 */
// Tom streng som ekte Combobox.Option-verdi krasjet komponenten opprinnelig (bekreftet ved live-
// verifisering, 2026-08-22) — byttet til en garantert ikke-tom sentinel for "tomValgTekst"-
// alternativet, og mappes tilbake til den offentlige '' ("ingen/alle")-konvensjonen i onChange.
const TOM_VALG_SENTINEL = '__ingen_virksomhet_valgt__';

export function VirksomhetVelger({ virksomheter, value, onChange, label, tomValgTekst, hideLabel, style }: VirksomhetVelgerProps) {
  // DEN FAKTISKE krasj-årsaken (samme feilmelding, fant den ved å lese selve pakkens kildekode —
  // node_modules/@digdir/designsystemet-react/dist/esm/components/Combobox/Combobox.js): Combobox
  // (merket @deprecated i denne pakkeversjonen — "Use Suggestion instead") har en intern useEffect
  // som gjør `options[prefix(v)].value` UTEN null-sjekk for hver streng i `value`-arrayet. Er `value`
  // satt til en id som IKKE (ennå) finnes blant de rendrede <Combobox.Option>-barna — nettopp
  // situasjonen når siden åpnes med ?virksomhetId=... FØR virksomhetslisten er ferdig hentet — kaster
  // biblioteket sitt eget "Cannot read properties of undefined (reading 'value')". Løsningen her er
  // IKKE i vår kode i streng forstand, men vi unngår hele feilklassen ved aldri å sende en `value`
  // videre til Combobox som ikke faktisk finnes i `virksomheter` ennå.
  const gyldigValgtId = value && virksomheter.some((v) => v.id === value) ? value : '';

  return (
    <Combobox
      label={label}
      hideLabel={hideLabel}
      size="sm"
      style={style}
      value={gyldigValgtId ? [gyldigValgtId] : []}
      onValueChange={(nyVerdi) => {
        const valgt = nyVerdi[0];
        onChange(!valgt || valgt === TOM_VALG_SENTINEL ? '' : valgt);
      }}
      filter={(inputValue, option) => option.label.toLowerCase().includes(inputValue.toLowerCase())}
    >
      <Combobox.Empty>Ingen virksomheter matcher søket</Combobox.Empty>
      <Combobox.Option value={TOM_VALG_SENTINEL}>{tomValgTekst}</Combobox.Option>
      {virksomheter.map((v) => (
        <Combobox.Option key={v.id} value={v.id}>
          {v.navn}
        </Combobox.Option>
      ))}
    </Combobox>
  );
}
