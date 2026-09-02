import { useState } from 'react';
import { Alert, Card, Heading, Paragraph } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { TjenesteDto } from '../api/types';
import { StatusStepper } from '../entitet/StatusStepper';

export interface StatusFaneProps {
  tjeneste: TjenesteDto;
  onTjenesteOppdatert: (t: TjenesteDto) => void;
}

/** Samme redigeringsfunksjon som tidligere, nå via den delte `StatusStepper` (docs/30 §3.3/§4 punkt 7
 * — saksbehandlertilpasningen 2026-09-02) i stedet for en ren visnings-pipeline + separat `<Select>`
 * ved siden av: klikk på et steg i selve pipelinen ER handlingen nå (med Dialog-bekreftelse på
 * publisert→tilbaketrukket/arkivert), ingen data-/API-endring. */
export function StatusFane({ tjeneste, onTjenesteOppdatert }: StatusFaneProps) {
  const [statusEndres, setStatusEndres] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);

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
      <StatusStepper status={tjeneste.status} onChange={endreStatus} disabled={statusEndres} />
      {feil && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{feil}</Alert>}
      <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginTop: '0.6rem' }}>
        Å sette status til «publisert» gjør tjenesten synlig i innbyggerveiledningen.
      </Paragraph>
    </Card>
  );
}
