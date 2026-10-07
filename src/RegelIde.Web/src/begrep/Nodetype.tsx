import { Field, Label, Radio, Select, Tag } from '@digdir/designsystemet-react';
import type { Aktortype, Begrepsnodetype, Kandidatnodetype } from '../api/types';

/**
 * [Ny, issue #310 «nodetype-akse», 2026-10-07, docs/33 §4.1–4.2] ÉN delt kilde for visning og valg av
 * nodetypen på et begrep med gruppefunksjon (klasse/rolle/område/organ) og aktørtypen på en virksomhet
 * — samme «én komponent, aldri en lokal variant»-regel som `NavneformgrunnTag` (docs/09 §15).
 *
 * <h3>Fargerollene (docs/09 §15, oppdatert i samme runde)</h3>
 * Typen er en KATEGORI, ikke en status — derfor ingen statusfarger (`success`/`warning`/`danger`), som
 * i appen betyr «riktig/forsiktig/feil». `gruppe` (ikke reklassifisert / nodetype ikke avgjort) er
 * `neutral`: den er nettopp UAVKLART, og skal ikke se ut som en av typene. `organ` deler `accent` med
 * virksomhet-kategorien i navnekandidatlisten, fordi et organ hører hjemme som aktør (docs/33 §4.1).
 * En verdi komponenten ikke kjenner vises RÅ i stedet for å skjules (samme regel som `NavneformgrunnTag`).
 */
type Farge = 'brand2' | 'brand3' | 'info' | 'accent' | 'neutral';

export const NODETYPE_VISNING: Record<string, { tekst: string; farge: Farge; forklaring: string }> = {
  klasse: {
    tekst: 'Klasse',
    farge: 'brand2',
    forklaring: 'En mengde aktører loven omtaler samlet — det som gjelder klassen, gjelder hvert medlem (f.eks. «kommunene», «språkutviklingskommuner»).',
  },
  rolle: {
    tekst: 'Rolle',
    farge: 'brand3',
    forklaring: 'En funksjon som innehas av en aktør, ofte med ulik innehaver per paragraf (f.eks. «reguleringsmyndighet», «departementet»).',
  },
  omrade: {
    tekst: 'Område',
    farge: 'info',
    forklaring: 'Et territorium (f.eks. «forvaltningsområdet for samiske språk», «Troms»).',
  },
  organ: {
    tekst: 'Organ',
    farge: 'accent',
    forklaring: 'Et organ loven omtaler som ennå ikke finnes som virksomhet i katalogen (f.eks. «Kongen i statsråd»).',
  },
  gruppe: {
    tekst: 'Gruppe (type ikke avgjort)',
    farge: 'neutral',
    forklaring: 'Generisk aktøromtale der typen ikke er valgt ennå — velg klasse, rolle eller område.',
  },
};

/** Typen som `Tag` — brukt på BegrepDetalj, i lister og i veiviserens oppsummering. */
export function BegrepskategoriTag({ kategori }: { kategori: string | null | undefined }) {
  if (!kategori) return null;
  const visning = NODETYPE_VISNING[kategori];
  if (!visning) return <Tag data-size="sm">{kategori}</Tag>;
  return (
    <Tag data-color={visning.farge} data-size="sm" title={visning.forklaring}>
      {visning.tekst}
    </Tag>
  );
}

export const KANDIDATNODETYPER: readonly Kandidatnodetype[] = ['klasse', 'rolle', 'omrade'];
export const BEGREPSNODETYPER: readonly Begrepsnodetype[] = ['klasse', 'rolle', 'omrade', 'organ'];

/**
 * Valg av nodetype som radioknapper — veiviserens steg 2 og «Behandle gruppen». Ingen forhåndsvalgt
 * verdi med mindre kalleren sender en (kandidatens foreslåtte type): typen gjettes ikke (CLAUDE.md §8).
 */
export function NodetypeVelger({
  name, value, onChange, disabled, typer = KANDIDATNODETYPER,
}: {
  name: string;
  value: string | null;
  onChange: (verdi: Begrepsnodetype) => void;
  disabled?: boolean;
  typer?: readonly Begrepsnodetype[];
}) {
  return (
    <Field data-size="sm">
      {typer.map((type) => (
        <Radio
          key={type}
          name={name}
          value={type}
          label={NODETYPE_VISNING[type].tekst}
          description={NODETYPE_VISNING[type].forklaring}
          checked={value === type}
          onChange={() => onChange(type)}
          disabled={disabled}
        />
      ))}
    </Field>
  );
}

// ---------- Aktørtype på Virksomhet ----------

export const AKTORTYPE_VISNING: Record<Aktortype, { tekst: string; forklaring: string }> = {
  rettssubjekt: { tekst: 'Rettssubjekt', forklaring: 'Kan ha rettigheter og plikter selv — f.eks. en kommune, staten, et helseforetak.' },
  organ: { tekst: 'Organ', forklaring: 'Handler på vegne av et rettssubjekt — f.eks. Stortinget, et departement, kommunestyret.' },
  organisatorisk_enhet: { tekst: 'Organisatorisk enhet', forklaring: 'En enhet inne i en virksomhet — f.eks. RME i NVE.' },
};

/** Aktørtypen som `Tag`. NULL = uavklart vises som `neutral` «Aktørtype uavklart» bare når
 * `visUavklart` er satt (samme valg som `NavneformgrunnTag`s `visUspesifisert`). Fargen er `neutral`
 * for alle tre: typen er en KATEGORI, og ingen av dem er «bedre» enn de andre. */
export function AktortypeTag({ aktortype, visUavklart }: { aktortype: Aktortype | null; visUavklart?: boolean }) {
  if (!aktortype) {
    return visUavklart ? <Tag data-color="neutral" data-size="sm" variant="outline">Aktørtype uavklart</Tag> : null;
  }
  const visning = AKTORTYPE_VISNING[aktortype];
  if (!visning) return <Tag data-size="sm">{aktortype}</Tag>;
  return <Tag data-color="neutral" data-size="sm" title={visning.forklaring}>{visning.tekst}</Tag>;
}

/** Velger for aktørtype — '' = uavklart (NULL). */
export function AktortypeVelger({
  value, onChange, disabled,
}: {
  value: Aktortype | null;
  onChange: (verdi: Aktortype | null) => void;
  disabled?: boolean;
}) {
  return (
    <Field data-size="sm" style={{ maxWidth: '16rem' }}>
      {/* Skjult label — velgeren står i en tabellrad med «Aktørtype» som radoverskrift, samme mønster
        * som forvaltningsnivå-velgeren på VirksomhetDetalj. */}
      <Label style={{ display: 'none' }}>Aktørtype</Label>
      <Select
        value={value ?? ''}
        disabled={disabled}
        onChange={(e) => onChange((e.target.value || null) as Aktortype | null)}
      >
        <Select.Option value="">Uavklart</Select.Option>
        {(Object.keys(AKTORTYPE_VISNING) as Aktortype[]).map((a) => (
          <Select.Option key={a} value={a}>{AKTORTYPE_VISNING[a].tekst}</Select.Option>
        ))}
      </Select>
    </Field>
  );
}
