import { Field, Label, Select } from '@digdir/designsystemet-react';
import type { KildeUtenforKorpusDokumentasjon, KildeUtenforKorpusType } from '../api/types';

/**
 * [Ny, issue #311, Johanns beslutning 2026-10-07] ÉN delt kilde for visning og valg av «kilde utenfor korpus»:
 * TYPEN (kgl.res., instruks, …, nettside/annet) og DOKUMENTASJONEN (primær = selve kilden, sekundær = en tekst
 * som refererer den). Begge er påkrevd når en strukturkant ikke har hjemmel i korpus, og ingen av dem
 * forhåndsvelges — typen gjettes ikke (CLAUDE.md §8). Samme «én komponent, aldri en lokal variant»-regel som
 * `NavneformgrunnTag` (docs/09 §15).
 */
export const KILDETYPE_VISNING: Record<KildeUtenforKorpusType, string> = {
  kgl_res: 'Kgl.res.',
  instruks: 'Instruks',
  tildelingsbrev: 'Tildelingsbrev',
  vedtekter: 'Vedtekter',
  styrevedtak: 'Styrevedtak',
  forarbeider: 'Forarbeider (prop., innst., NOU)',
  nettside_annet: 'Nettside/annet',
  // [Ny, issue #312] Kartverket/Enhetsregisteret/SSR — autoritativt register, ikke rettskilde.
  register: 'Register',
};

export const DOKUMENTASJON_VISNING: Record<KildeUtenforKorpusDokumentasjon, string> = {
  primaer: 'Primær — selve kilden',
  sekundaer: 'Sekundær — en tekst som refererer kilden',
};

export function KildeUtenforKorpusVelger({
  type, dokumentasjon, onType, onDokumentasjon, disabled,
}: {
  type: KildeUtenforKorpusType | '';
  dokumentasjon: KildeUtenforKorpusDokumentasjon | '';
  onType: (v: KildeUtenforKorpusType | '') => void;
  onDokumentasjon: (v: KildeUtenforKorpusDokumentasjon | '') => void;
  disabled?: boolean;
}) {
  return (
    <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
      <Field data-size="sm" style={{ minWidth: '14rem' }}>
        <Label>Kildetype</Label>
        <Select data-size="sm" value={type} disabled={disabled}
          onChange={(e) => onType(e.target.value as KildeUtenforKorpusType | '')}>
          <Select.Option value="">Velg type …</Select.Option>
          {(Object.keys(KILDETYPE_VISNING) as KildeUtenforKorpusType[]).map((k) => (
            <Select.Option key={k} value={k}>{KILDETYPE_VISNING[k]}</Select.Option>
          ))}
        </Select>
      </Field>
      <Field data-size="sm" style={{ minWidth: '16rem' }}>
        <Label>Dokumentasjon</Label>
        <Select data-size="sm" value={dokumentasjon} disabled={disabled}
          onChange={(e) => onDokumentasjon(e.target.value as KildeUtenforKorpusDokumentasjon | '')}>
          <Select.Option value="">Velg …</Select.Option>
          {(Object.keys(DOKUMENTASJON_VISNING) as KildeUtenforKorpusDokumentasjon[]).map((k) => (
            <Select.Option key={k} value={k}>{DOKUMENTASJON_VISNING[k]}</Select.Option>
          ))}
        </Select>
      </Field>
    </div>
  );
}
