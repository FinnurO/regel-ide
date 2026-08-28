import { useEffect, useState } from 'react';
import {
  Checkbox, Combobox, Field, Heading, Label, Paragraph, Select,
} from '@digdir/designsystemet-react';
import { api } from '../api/client';
import type { AvhengighetsgrafDto, TjenesteDto } from '../api/types';
import { FELTVISNING_DEFAULT, type FeltvisningValg } from '../graf/grafFelles';
import { TjenesteGrafCanvas } from '../graf/TjenesteGrafCanvas';

/**
 * [Ny, 2026-08-28] Interaktiv tjenestereise-graf — velg sentrum, dybde, om handlinger skal vises,
 * livshendelse-filter, og hvilke felt som vises per node. Bygger på det nye multi-hop
 * `GET /api/tjenester/{id}/avhengighetsgraf`-endepunktet (`TjenestereiseGrafTjeneste`). Selve
 * lerretet/layouten er delt med `ImportWizard.tsx` sin in-memory forhåndsvisning, se
 * `graf/TjenesteGrafCanvas.tsx`. Bruker `@xyflow/react` (React Flow) — appens første nye
 * frontend-npm-avhengighet, se docs/09 §12.
 */
export default function Tjenestereise() {
  const [tjenester, setTjenester] = useState<TjenesteDto[]>([]);
  const [sentrumId, setSentrumId] = useState('');
  const [dybde, setDybde] = useState(2);
  const [inkluderHandlinger, setInkluderHandlinger] = useState(false);
  const [livshendelser, setLivshendelser] = useState<string[]>([]);
  const [livshendelseFilter, setLivshendelseFilter] = useState('');
  const [graf, setGraf] = useState<AvhengighetsgrafDto | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [felt, setFelt] = useState<FeltvisningValg>(FELTVISNING_DEFAULT);

  useEffect(() => {
    api.hentTjenester().then(setTjenester).catch(() => setTjenester([]));
    api.hentDistinkteLivshendelser().then(setLivshendelser).catch(() => setLivshendelser([]));
  }, []);

  useEffect(() => {
    if (!sentrumId) {
      setGraf(null);
      return;
    }
    setFeil(null);
    api.hentTjenestereiseGraf(sentrumId, { dybde, inkluderHandlinger, livshendelse: livshendelseFilter || null })
      .then(setGraf)
      .catch(() => setFeil('Kunne ikke hente tjenestereise-grafen.'));
  }, [sentrumId, dybde, inkluderHandlinger, livshendelseFilter]);

  return (
    <>
      <Heading level={1} data-size="lg">Tjenestereise</Heading>
      <Paragraph style={{ marginBottom: '1rem', maxWidth: '48rem' }}>
        Velg en tjeneste som sentrum for å se hvordan den henger sammen med andre tjenester (og
        valgfritt deres handlinger) gjennom avhengighetsgrafen — dra noder rundt, juster dybde og
        filter.
      </Paragraph>

      <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap', marginBottom: '1rem', alignItems: 'flex-end' }}>
        <Field style={{ minWidth: '20rem' }}>
          <Label>Sentrum-tjeneste</Label>
          <Combobox
            size="sm"
            value={sentrumId ? [sentrumId] : []}
            onValueChange={(v) => setSentrumId(v[0] ?? '')}
            filter={(inputValue, option) => option.label.toLowerCase().includes(inputValue.toLowerCase())}
          >
            <Combobox.Empty>Ingen tjenester matcher søket</Combobox.Empty>
            {tjenester.map((t) => (
              <Combobox.Option key={t.id} value={t.id}>{t.tittel}</Combobox.Option>
            ))}
          </Combobox>
        </Field>

        <Field style={{ maxWidth: '10rem' }}>
          <Label>Dybde (hopp)</Label>
          <Select data-size="sm" value={String(dybde)} onChange={(e) => setDybde(Number(e.target.value))}>
            {[1, 2, 3, 4, 5].map((d) => <Select.Option key={d} value={String(d)}>{d}</Select.Option>)}
          </Select>
        </Field>

        <Checkbox label="Inkluder handlinger" checked={inkluderHandlinger} onChange={(e) => setInkluderHandlinger(e.target.checked)} />

        <Field style={{ minWidth: '14rem' }}>
          <Label>Livshendelse-filter</Label>
          <Select data-size="sm" value={livshendelseFilter} onChange={(e) => setLivshendelseFilter(e.target.value)}>
            <Select.Option value="">Alle</Select.Option>
            {livshendelser.map((l) => <Select.Option key={l} value={l}>{l}</Select.Option>)}
          </Select>
        </Field>
      </div>

      {feil && <Paragraph style={{ color: 'var(--ds-color-danger-text-default)' }}>{feil}</Paragraph>}

      {!sentrumId && <Paragraph>Velg en sentrum-tjeneste for å tegne grafen.</Paragraph>}

      {sentrumId && graf && (
        <TjenesteGrafCanvas noder={graf.noder} kanter={graf.kanter} felt={felt} onFeltChange={setFelt} fremhevetId={sentrumId} />
      )}
    </>
  );
}
