import { EXPERIMENTAL_Suggestion as Suggestion, Field, Label, Paragraph, type SuggestionItem } from '@digdir/designsystemet-react';
import type { RettskildeSammendrag } from '../api/types';
import { useRettskildeSok } from './useRettskildeSok';

export interface RettskildeVelgerProps {
  rettskilder: RettskildeSammendrag[];
  /** Id på valgt rettskilde, `''` for ikke valgt. */
  value: string;
  onChange: (id: string) => void;
  label?: string;
}

/**
 * Enkeltvalg av ÉN rettskilde blant alle — søsterkomponent til `RettskildeFlervalg` (samme
 * `Suggestion`-baserte "søk-før-mount"-teknikk, se docs/09 §10 og `useRettskildeSok`), men
 * `multiple={false}` og en streng-`value` i stedet for et `Set`. Erstatter et rått
 * `<Select>`+`alleRettskilder.map(...)` med 5893 `<option>` — samme DOM-monteringsfelle som
 * `RettskildeFlervalg` løser, bare for enkeltvalg (velge HVILKEN rettskilde en lovreferanse skal
 * peke på, f.eks. `KommentarRedigering.tsx`/`RettskildeDetalj.tsx`).
 */
export function RettskildeVelger({ rettskilder, value, onChange, label = 'Rettskilde' }: RettskildeVelgerProps) {
  const { sok, setSok, treff, alleTreffAntall } = useRettskildeSok(rettskilder);

  const valgt = rettskilder.find((r) => r.id === value);
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
          placeholder="Søk rettskilder …"
          onInput={(e) => setSok(e.currentTarget.value)}
        />
        <Suggestion.Clear onClick={() => setSok('')} />
        <Suggestion.List>
          <Suggestion.Empty>
            {sok.trim() ? 'Ingen rettskilder matcher søket' : 'Skriv for å søke blant rettskildene …'}
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
