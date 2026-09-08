import { Field, Label, Select, Tag } from '@digdir/designsystemet-react';
import type { Navneformgrunn } from '../api/types';

/**
 * [Ny, navneformgrunn-runden, 2026-09-07] Delt presentasjon av `BegrepDto.navneformgrunn` — ÉN kilde
 * for både velgeren (der en navneform LEGGES TIL) og taggen (der navneformer LISTES), slik at farge
 * og tekst ikke driver fra hverandre mellom `VirksomheterListe`, `VirksomhetDetalj`, `BegrepDetalj`
 * og navnekandidat-wizarden.
 *
 * Johanns bestilling ordrett: «når man kobler en navneform til en virksomhet så må det være mulig å
 * angi grunnen til at det er der. Da tenker jeg f.eks at en navneform er utgått slik som Arkivverket
 * og Verneplikt, at med kun navnet 'Suldal' i en kontekst betyr 'Suldal kommune', at et navn er
 * feilskrevet slik som jeg har sett 'Matilsynet' med bare 1 't'.»
 *
 * FARGEVALGET er et krav, ikke pynt: en UTGÅTT eller FEILSKREVET navneform skal aldri kunne
 * forveksles med det offisielle navnet. Derfor `success` (grønn) kun for `gjeldende`, `warning`
 * (oransje) for `utgatt` og `danger` (rød) for `feilskriving` — tydelig ulike semantiske roller
 * fra Designsystemet, ingen egne farger (docs/09 §4: kun `--ds-*`-roller).
 *
 * [Ny verdi, registernavn-runden, 2026-09-08] `parallellnavn` = et LIKESTILT offisielt navn på et
 * annet språk («Gáivuona suohkan» ved siden av «Kåfjord kommune»), hentet fra Kartverkets SSR.
 * Fargen er `accent`, og valget er tvunget: docs/09 §15 låser `success` til `gjeldende` ALENE, og
 * `warning`/`danger`/`info` er tatt av utgatt/feilskriving/kortform. `neutral` var det eneste andre
 * ledige, men den er allerede «Uspesifisert grunn» (nederst i denne filen) — to nøytrale merkelapper
 * som bare skilles av `variant="outline"` er ikke en lesbar forskjell. `accent` er dessuten riktig
 * SEMANTISK: et parallellnavn er ikke en advarsel og ikke en feil, men det er heller ikke DEN
 * gjeldende formen visningen plukker, så det skal ikke være grønt.
 */
export const NAVNEFORMGRUNN_VALG: ReadonlyArray<{
  verdi: Navneformgrunn;
  label: string;
  /** Designsystemets fargerolle — se klassekommentaren for hvorfor disse er ulike. */
  farge: 'success' | 'info' | 'warning' | 'danger' | 'accent';
  /** Kort forklaring vist i velgerens hjelpetekst — Johanns egne eksempler, ikke oppdiktede. */
  hjelp: string;
}> = [
  { verdi: 'gjeldende', label: 'Gjeldende navn', farge: 'success', hjelp: 'Virksomhetens gjeldende, offisielle navn — normaltilfellet.' },
  { verdi: 'utgatt', label: 'Utgått navn', farge: 'warning', hjelp: 'Historisk navn som fortsatt står i lovteksten, f.eks. «Arkivverket» (nå Nasjonalarkivet).' },
  { verdi: 'kortform', label: 'Kortform', farge: 'info', hjelp: 'Kontekstavhengig kortform, f.eks. «Suldal» som i denne sammenhengen betyr Suldal kommune.' },
  { verdi: 'feilskriving', label: 'Feilskriving', farge: 'danger', hjelp: 'Skrivefeil i kildeteksten, f.eks. «Matilsynet» med bare én t.' },
  // [Ny, registernavn-runden, 2026-09-08] Se klassekommentarens avsnitt om fargevalget for hvorfor
  // denne må være `accent` og ikke `success`.
  {
    verdi: 'parallellnavn',
    label: 'Parallellnavn',
    farge: 'accent',
    hjelp: 'Likestilt offisielt navn på et annet språk, f.eks. «Gáivuona suohkan» for Kåfjord kommune.',
  },
];

const PER_VERDI = new Map(NAVNEFORMGRUNN_VALG.map((v) => [v.verdi, v]));

/**
 * Navneformgrunnen som en `Tag`, til bruk der navneformer LISTES. Returnerer `null` for en
 * uspesifisert (NULL) grunn — bevisst: de aller fleste eksisterende navneformene har ingen grunn
 * ennå (ingen datamigrering, se `BegrepEntitet.Navneformgrunn`), og en «Uspesifisert»-tag på hver av
 * dem ville vært ren støy i en tabell. Sett `visUspesifisert` der fraværet er selve poenget (f.eks.
 * en oversikt over hva som mangler å bli utfylt).
 */
export function NavneformgrunnTag({
  grunn, visUspesifisert = false,
}: { grunn: Navneformgrunn | null; visUspesifisert?: boolean }) {
  if (grunn === null) {
    return visUspesifisert
      ? <Tag data-size="sm" data-color="neutral" variant="outline">Uspesifisert grunn</Tag>
      : null;
  }
  const valg = PER_VERDI.get(grunn);
  // Ukjent verdi (f.eks. en verdi lagt til i backend men ikke her ennå) vises RÅ i stedet for å
  // skjules — «ikke funnet ≠ oppfunnet»: saksbehandleren skal se at det står noe vi ikke kjenner.
  if (!valg) return <Tag data-size="sm" data-color="neutral">{grunn}</Tag>;
  return <Tag data-size="sm" data-color={valg.farge}>{valg.label}</Tag>;
}

/**
 * Velgeren, til bruk der en navneform LEGGES TIL eller kobles. `Field` + `Label` + `Select` er det
 * påkrevde mønsteret for `Select` (docs/09 §5-tabellen) — til forskjell fra `Combobox`/`Switch`, som
 * kapsler sin egen label.
 *
 * IKKE påkrevd: det tomme valget («Uspesifisert») er en fullt gyldig verdi som sender `null`. Å tvinge
 * et valg her ville betydd at saksbehandleren måtte gjette en grunn for å komme videre — nøyaktig det
 * «ingen gjettet fallback»-holdningen i resten av kodebasen unngår.
 */
export function NavneformgrunnVelger({
  value, onChange, label = 'Grunn til at navneformen peker hit', visHjelp = true, style,
}: {
  value: Navneformgrunn | null;
  onChange: (verdi: Navneformgrunn | null) => void;
  label?: string;
  visHjelp?: boolean;
  style?: React.CSSProperties;
}) {
  const valgtHjelp = value === null ? null : PER_VERDI.get(value)?.hjelp ?? null;
  return (
    <Field data-size="sm" style={style}>
      <Label>{label}</Label>
      <Select
        value={value ?? ''}
        onChange={(e) => onChange(e.target.value === '' ? null : (e.target.value as Navneformgrunn))}
      >
        <Select.Option value="">Uspesifisert</Select.Option>
        {NAVNEFORMGRUNN_VALG.map((v) => (
          <Select.Option key={v.verdi} value={v.verdi}>{v.label}</Select.Option>
        ))}
      </Select>
      {visHjelp && valgtHjelp && (
        // Metatekst: BÅDE mindre størrelse og subtil farge (docs/09 §6 — kun fargen alene er den
        // dokumenterte feilen som er begått før). Aldri `opacity` (docs/09 §7).
        <span style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
          {valgtHjelp}
        </span>
      )}
    </Field>
  );
}
