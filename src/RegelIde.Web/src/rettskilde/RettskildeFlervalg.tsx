import { EXPERIMENTAL_Suggestion as Suggestion, Field, Label, type SuggestionItem } from '@digdir/designsystemet-react';
import type { RettskildeSammendrag } from '../api/types';
import { useRettskildeSok } from './useRettskildeSok';
import { Metatekst } from '../entitet/Metatekst';

export interface RettskildeFlervalgProps {
  rettskilder: RettskildeSammendrag[];
  valgte: Set<string>;
  onChange: (valgte: Set<string>) => void;
  label?: string;
}

/**
 * Flervalg av rettskilder — erstatter én `Checkbox` per rettskilde (docs/09 §10; uholdbart med
 * 5893 reelle rader i dag, se `curl /api/rettskilder | wc`) med Designsystemets `Suggestion`
 * (`EXPERIMENTAL_Suggestion`, `multiple`-modus). API verifisert mot installert `1.18.0` sine
 * `.d.ts`/kildefiler i `node_modules` (ikke gjettet) — se docs/09 §10 for detaljene, inkl. hvorfor
 * DOM-monteringen styres selv (`useRettskildeSok`) i stedet for library'ets egen `filter`-prop.
 */
export function RettskildeFlervalg({ rettskilder, valgte, onChange, label = 'Rettskilder' }: RettskildeFlervalgProps) {
  const { sok, setSok, treff, alleTreffAntall } = useRettskildeSok(rettskilder);

  const valgteItems = rettskilder
    .filter((r) => valgte.has(r.id))
    .map((r) => ({ label: r.tittel, value: r.id }));

  return (
    <Field style={{ marginBottom: '0.75rem', maxWidth: '30rem' }}>
      <Label>{label}</Label>
      <Suggestion
        multiple
        filter={false}
        selected={valgteItems}
        onSelectedChange={(items: SuggestionItem[]) => onChange(new Set(items.map((i) => i.value)))}
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
        <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginTop: '0.2rem' }}>
          Viser {treff.length} av {alleTreffAntall} treff — skriv et mer spesifikt søk for å se flere.
        </Metatekst>
      )}
    </Field>
  );
}
