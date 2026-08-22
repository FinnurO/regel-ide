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
export function VirksomhetVelger({ virksomheter, value, onChange, label, tomValgTekst, hideLabel, style }: VirksomhetVelgerProps) {
  return (
    <Combobox
      label={label}
      hideLabel={hideLabel}
      size="sm"
      style={style}
      value={value ? [value] : []}
      onValueChange={(nyVerdi) => onChange(nyVerdi[0] ?? '')}
      filter={(inputValue, option) => option.label.toLowerCase().includes(inputValue.toLowerCase())}
    >
      <Combobox.Empty>Ingen virksomheter matcher søket</Combobox.Empty>
      <Combobox.Option value="">{tomValgTekst}</Combobox.Option>
      {virksomheter.map((v) => (
        <Combobox.Option key={v.id} value={v.id}>
          {v.navn}
        </Combobox.Option>
      ))}
    </Combobox>
  );
}
