import { EXPERIMENTAL_Suggestion as Suggestion, Field, Label, Paragraph, type SuggestionItem } from '@digdir/designsystemet-react';
import type { RegelnodeDto } from '../api/types';
import { useRegelnodeSok } from './useRegelnodeSok';

export interface RegelnodeVelgerProps {
  regelnoder: RegelnodeDto[];
  /** Id på valgt regelnode, `''` for ikke valgt. */
  value: string;
  onChange: (id: string) => void;
  label?: string;
}

/**
 * Enkeltvalg av ÉN regelnode blant alle — søsterkomponent til `rettskilde/RettskildeVelger.tsx`
 * (samme `Suggestion`-baserte "søk-før-mount"-teknikk, se docs/09 §10/§10.1 og
 * `useRegelnodeSok`). Erstatter et rått `<Select>` med `regelnoder.map(...)` i "Bytt til
 * eksisterende regelnode" (Vilkårstre-fanen) — samme DOM-monteringsfelle-unngåelse, nå for
 * regelnoder i stedet for rettskilder.
 */
export function RegelnodeVelger({ regelnoder, value, onChange, label = 'Regelnode' }: RegelnodeVelgerProps) {
  const { sok, setSok, treff, alleTreffAntall } = useRegelnodeSok(regelnoder);

  const valgt = regelnoder.find((r) => r.id === value);
  const selected: SuggestionItem | null = valgt ? { label: valgt.tittel, value: valgt.id } : null;

  return (
    <Field style={{ maxWidth: '30rem' }}>
      <Label>{label}</Label>
      <Suggestion
        multiple={false}
        filter={false}
        selected={selected}
        onSelectedChange={(item: SuggestionItem | null) => onChange(item?.value ?? '')}
      >
        <Suggestion.Input
          placeholder="Søk regelnoder …"
          onInput={(e) => setSok(e.currentTarget.value)}
        />
        <Suggestion.Clear onClick={() => setSok('')} />
        <Suggestion.List>
          <Suggestion.Empty>
            {sok.trim() ? 'Ingen regelnoder matcher søket' : 'Skriv for å søke blant regelnodene …'}
          </Suggestion.Empty>
          {treff.map((r) => (
            <Suggestion.Option key={r.id} value={r.id}>{r.tittel}</Suggestion.Option>
          ))}
        </Suggestion.List>
      </Suggestion>
      {alleTreffAntall > treff.length && (
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginTop: '0.2rem' }}>
          Viser {treff.length} av {alleTreffAntall} treff — skriv et mer spesifikt søk for å se flere.
        </Paragraph>
      )}
    </Field>
  );
}
