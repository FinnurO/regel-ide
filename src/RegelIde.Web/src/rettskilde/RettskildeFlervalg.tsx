import { useMemo, useState, type FormEvent } from 'react';
import { EXPERIMENTAL_Suggestion as Suggestion, Field, Label, Paragraph, type SuggestionItem } from '@digdir/designsystemet-react';
import type { RettskildeSammendrag } from '../api/types';

/** Øvre grense på antall treff mount'et som `<Suggestion.Option>` samtidig — se filens toppkommentar. */
const MAKS_TREFF = 50;

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
 * `.d.ts`/kildefiler i `node_modules` (ikke gjettet) — se docs/09 §10 for detaljene.
 * <para>
 * <b>Bevisst IKKE library'ets innebygde `filter`-prop:</b> `Suggestion` (som `Combobox`) mount'er
 * ALLE `<Suggestion.Option>`-barn i DOM-en uansett — filtrering skjer ved å SKJULE ferdig-mount'ede
 * options (`option.disabled`), ikke ved å utelate dem. Docs/09 §9 dokumenterer at nettopp dette
 * mønsteret (native `<Select>` med kun ~451 `<option>`) ga et reelt render-timeout ved
 * live-verifisering — med 5893 rettskilder her (13× så mange) er risikoen for samme feil stor nok
 * til at vi ikke gjetter oss friske. Løsning: egen React-state (`sok`) driver et EKSTERNT filter på
 * `rettskilder`-arrayet, og kun de (maks `MAKS_TREFF`) treffene mount'es som
 * `<Suggestion.Option>` — null options i DOM-en før brukeren har skrevet noe.
 * </para>
 */
export function RettskildeFlervalg({ rettskilder, valgte, onChange, label = 'Rettskilder' }: RettskildeFlervalgProps) {
  const [sok, setSok] = useState('');

  const valgteItems = useMemo(
    () => rettskilder.filter((r) => valgte.has(r.id)).map((r) => ({ label: r.tittel, value: r.id })),
    [rettskilder, valgte],
  );

  const alleTreff = useMemo(() => {
    const s = sok.trim().toLowerCase();
    if (!s) return [];
    return rettskilder.filter((r) => r.tittel.toLowerCase().includes(s));
  }, [rettskilder, sok]);
  const treff = alleTreff.slice(0, MAKS_TREFF);

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
          onInput={(e: FormEvent<HTMLInputElement>) => setSok(e.currentTarget.value)}
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
      {alleTreff.length > MAKS_TREFF && (
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginTop: '0.2rem' }}>
          Viser {MAKS_TREFF} av {alleTreff.length} treff — skriv et mer spesifikt søk for å se flere.
        </Paragraph>
      )}
    </Field>
  );
}
