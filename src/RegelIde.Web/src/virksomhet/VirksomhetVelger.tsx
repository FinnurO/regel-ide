import { EXPERIMENTAL_Suggestion as Suggestion, Field, Label, type SuggestionItem } from '@digdir/designsystemet-react';
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
 * ytelsesproblemet — Designsystemets `Suggestion` løser dette FORDI den er søkbar (bruker skriver,
 * lista filtreres), ikke fordi den er paginert (den er det ikke, og trenger ikke være det — se
 * `usePaginering.ts`/`Pagineringskontroll.tsx` for det separate tabellrad-problemet).
 *
 * `Suggestion`, IKKE den (nyere) deprecated `Combobox` (byttet 2026-09-02 — se bug-fiks under):
 * samme mønster som `RettskildeVelger.tsx`/`GlobaltSok.tsx` (`EXPERIMENTAL_Suggestion` + `Field`/
 * `Label` for et ekte, tilgjengelig feltnavn — `Suggestion.Input` har ingen egen `label`-prop slik
 * `Combobox` hadde).
 *
 * Standardfilteret i `Suggestion` matcher ALLEREDE mot visningsteksten (`option.label`), ikke
 * verdien — i motsetning til den gamle `Combobox` (der egen `filter` var nødvendig fordi
 * standardfilteret der matchet mot OPTION-VERDIEN, virksomhetens GUID). Ingen egen `filter`-prop
 * trengs her.
 *
 * BUG FIKSET (2026-09-02) — `VirksomhetVelger` (da `Combobox`-basert) viste ingen forslag i det
 * hele tatt når den ble brukt inni en Designsystemet `Dialog` (bekreftet: `Begrepskandidater.tsx`s
 * godkjenn-dialog). Rotårsak, funnet ved live-inspeksjon av rendret DOM (IKKE gjettet): `Dialog` er
 * en ekte native `<dialog>` vist med `showModal()`, som plasserer den i nettleserens eget
 * "toppnivå-lag". `Combobox`s forslagsliste portaleres derimot til `document.body` (via Floating
 * UI sin `FloatingPortal`) med standard posisjoneringsstrategi `'absolute'` — en strategi som
 * regner listens posisjon relativt til `offsetParent`/dokument-scroll. Referanseelementet
 * (input-feltet) sitter INNI toppnivå-laget (dialogen), mens forslagslisten portaleres UTENFOR det
 * — to forskjellige koordinatrom. Resultatet, bekreftet i devtools: listen rendres faktisk
 * (`display:block`, ikke `display:none` slik det så ut ved første øyekast i browseren), men med
 * `transform: translate(…, -19432.7px)` — forskjøvet ca. 19 433 piksler over viewporten, altså helt
 * usynlig for brukeren. Dette er en kjent begrensning i hvordan Floating UI sin `'absolute'`-
 * strategi samspiller med native `<dialog>`s toppnivå-lag, ikke noe som kan fikses med en prop på
 * `Combobox` (biblioteket eksponerer ingen `strategy`-override der).
 *
 * `Suggestion` bruker i stedet native Popover API (`<u-datalist popover="manual">`, se
 * `suggestion-list.js`) — popover-elementer havner i SITT EGET toppnivå-lag, som nettleseren
 * stabler korrekt oppå/i forhold til `<dialog>`s toppnivå-lag uansett scroll/offsetParent. Dette er
 * allerede et bevist, fungerende mønster i denne kodebasen — `GlobaltSok.tsx` bruker `Suggestion`
 * inni akkurat samme `Dialog`-komponent. INGEN strukturell endring i `Begrepskandidater.tsx` var
 * nødvendig (virksomhetsvalget er fortsatt i selve `Dialog`en, ikke flyttet inline i raden) — feilen
 * satt utelukkende i hvilken underliggende Designsystemet-komponent `VirksomhetVelger` bygde på.
 */
const TOM_VALG_SENTINEL = '__ingen_virksomhet_valgt__';

export function VirksomhetVelger({ virksomheter, value, onChange, label, tomValgTekst, hideLabel, style }: VirksomhetVelgerProps) {
  const valgtVirksomhet = virksomheter.find((v) => v.id === value);
  const selected: SuggestionItem | null = valgtVirksomhet
    ? { label: valgtVirksomhet.navn, value: valgtVirksomhet.id }
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
          <Suggestion.Empty>Ingen virksomheter matcher søket</Suggestion.Empty>
          <Suggestion.Option value={TOM_VALG_SENTINEL}>{tomValgTekst}</Suggestion.Option>
          {virksomheter.map((v) => (
            <Suggestion.Option key={v.id} value={v.id}>
              {v.navn}
            </Suggestion.Option>
          ))}
        </Suggestion.List>
      </Suggestion>
    </Field>
  );
}
