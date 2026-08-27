import { useState } from 'react';
import { Alert, Card, Heading, Paragraph, Select, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { TjenesteDto } from '../api/types';

const STATUSER = ['utkast', 'under_revisjon', 'validert', 'publisert', 'tilbaketrukket', 'arkivert'];

export interface StatusFaneProps {
  tjeneste: TjenesteDto;
  onTjenesteOppdatert: (t: TjenesteDto) => void;
}

/** Samme redigeringsfunksjon som tidligere, pluss en ren visuell pipeline over de 6 statusene
 * (Tjenestedetalj-redesignrunden 2026-08-27) — ingen nye data, kun tydeligere fremstilling av det
 * allerede eksisterende statusløpet (docs/03-domenemodell.md §3.1). */
export function StatusFane({ tjeneste, onTjenesteOppdatert }: StatusFaneProps) {
  const [statusEndres, setStatusEndres] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);
  const gjeldendeIndeks = STATUSER.indexOf(tjeneste.status);

  async function endreStatus(nyStatus: string) {
    setStatusEndres(true);
    setFeil(null);
    try {
      onTjenesteOppdatert(await api.settTjenesteStatus(tjeneste.id, { status: nyStatus }));
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved statusendring.');
    } finally {
      setStatusEndres(false);
    }
  }

  return (
    <Card style={{ maxWidth: '640px', padding: '1rem 1.25rem' }}>
      <Heading level={2} data-size="xs" style={{ marginBottom: '0.6rem' }}>Status</Heading>
      <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', marginBottom: '1rem', flexWrap: 'wrap' }}>
        {STATUSER.map((s, i) => (
          <span key={s} style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
            <Tag data-size="sm" data-color={i === gjeldendeIndeks ? 'info' : 'neutral'} variant={i === gjeldendeIndeks ? 'default' : 'outline'}>
              {s}
            </Tag>
            {i < STATUSER.length - 1 && <span style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>→</span>}
          </span>
        ))}
      </div>
      <Select data-size="sm" value={tjeneste.status} disabled={statusEndres} onChange={(e) => endreStatus(e.target.value)} style={{ maxWidth: '16rem' }}>
        {STATUSER.map((s) => <Select.Option key={s} value={s}>{s}</Select.Option>)}
      </Select>
      {feil && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{feil}</Alert>}
      <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginTop: '0.6rem' }}>
        Å sette status til «publisert» gjør tjenesten synlig i innbyggerveiledningen.
      </Paragraph>
    </Card>
  );
}
