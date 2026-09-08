import { EXPERIMENTAL_Suggestion as Suggestion, Field, Label, type SuggestionItem } from '@digdir/designsystemet-react';
import type { RettskildeSammendrag, VirksomhetsbegrepDto } from '../api/types';

export interface GruppebegrepVelgerProps {
  gruppebegrep: VirksomhetsbegrepDto[];
  /** Valgt gruppebegrep-id, eller '' for «ingen valgt». */
  value: string;
  onChange: (gruppeBegrepId: string) => void;
  label: string;
  tomValgTekst: string;
  /** Brukes til å vise HVILKEN lov gruppen er hjemlet i, i selve valglinjen. Utelatt = bare termen. */
  rettskilder?: RettskildeSammendrag[];
  /** Skjuler et gruppebegrep fra lista (f.eks. gruppen man er i ferd med å legge medlemmer TIL —
   * en gruppe kan ikke være medlem av seg selv, og valget skal da ikke engang tilbys). */
  skjulId?: string;
  hideLabel?: boolean;
  style?: React.CSSProperties;
}

/**
 * [Ny, gruppemedlemskap-runden, 2026-09-08, issue #164] Søkbart valg av ETT gruppebegrep — samme
 * `Suggestion`-mønster og samme begrunnelse som `VirksomhetVelger` (docs/09 §9.2), og bevisst ikke
 * et `<Select>`: gruppebegrepene er få i dag (~7), men de vokser med hver godkjente
 * gruppe-navnekandidat, og et `<Select>` her ville måttet byttes ut igjen senere.
 *
 * Ett gruppebegrep identifiseres av TERM + LOV (`ux_begreper_gruppebegrep_term_lovkilde`), ikke av
 * termen alene — «kommunene» kan finnes for flere lover og betyr da ikke det samme. Derfor viser
 * valglinjen loven når `rettskilder` er gitt: uten den kunne saksbehandleren ikke skille to like
 * termer fra hverandre. Mangler loven i `rettskilder`-lista, vises termen alene i stedet for en
 * gjettet lovtittel.
 */
const TOM_VALG_SENTINEL = '__ingen_gruppe_valgt__';

function visning(begrep: VirksomhetsbegrepDto, rettskilder?: RettskildeSammendrag[]): string {
  if (!rettskilder || !begrep.lovkildeId) return begrep.term;
  const lov = rettskilder.find((r) => r.id === begrep.lovkildeId);
  return lov ? `${begrep.term} — ${lov.tittel}` : begrep.term;
}

export function GruppebegrepVelger({
  gruppebegrep, value, onChange, label, tomValgTekst, rettskilder, skjulId, hideLabel, style,
}: GruppebegrepVelgerProps) {
  const valgbare = skjulId ? gruppebegrep.filter((g) => g.id !== skjulId) : gruppebegrep;
  const valgt = valgbare.find((g) => g.id === value);
  const selected: SuggestionItem | null = valgt
    ? { label: visning(valgt, rettskilder), value: valgt.id }
    : null;

  return (
    <Field data-size="sm" style={style}>
      <Label className={hideLabel ? 'ds-sr-only' : undefined}>{label}</Label>
      <Suggestion
        multiple={false}
        selected={selected}
        onSelectedChange={(item: SuggestionItem | null) =>
          onChange(!item || item.value === TOM_VALG_SENTINEL ? '' : item.value)
        }
      >
        <Suggestion.Input />
        <Suggestion.Clear onClick={() => onChange('')} />
        <Suggestion.List>
          <Suggestion.Empty>Ingen gruppebegrep matcher søket</Suggestion.Empty>
          <Suggestion.Option value={TOM_VALG_SENTINEL}>{tomValgTekst}</Suggestion.Option>
          {valgbare.map((g) => (
            <Suggestion.Option key={g.id} value={g.id}>
              {visning(g, rettskilder)}
            </Suggestion.Option>
          ))}
        </Suggestion.List>
      </Suggestion>
    </Field>
  );
}
