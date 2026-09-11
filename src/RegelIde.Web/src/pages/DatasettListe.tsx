import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Card, Heading, Link, Paragraph, Spinner, Table } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { DatasettDto } from '../api/types';

/**
 * Read-only oversikt over Datasett-registeret (produktkrav kap. 3.6) — kun seedet i dag, ingen
 * opprett-UI (se DatasettDetalj.tsx for verdiregistrering per felt).
 */
export default function DatasettListe() {
  const [datasett, setDatasett] = useState<DatasettDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  useEffect(() => {
    api.hentDatasett().then(setDatasett).catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av datasett.'));
  }, []);

  return (
    <>
      <Heading level={1} data-size="lg" style={{ marginBottom: '1rem' }}>
        Datasett
      </Heading>
      <Paragraph style={{ marginBottom: '1.5rem' }}>
        Feltdefinisjoner brukt som input til Vilkår — verdier (kommunale/nasjonale) registreres per
        felt, se lenken i tabellen.
      </Paragraph>

      {feil && <Alert data-color="danger" style={{ marginBottom: '1rem' }}>{feil}</Alert>}

      {/* [Rettet, issue #279] Card ALLTID rendret (docs/09 §14) — tom-/laste-tilstand er en
          Paragraph INNI kortet, ikke et betinget-rendret kort utenfor. */}
      <Card style={{ padding: datasett && datasett.length > 0 ? 0 : '1rem', overflow: 'hidden' }}>
      {!datasett && !feil ? (
        <Spinner aria-label="Laster …" data-size="sm" />
      ) : datasett && datasett.length === 0 ? (
        <Paragraph style={{ margin: 0 }}>Ingen datasett funnet.</Paragraph>
      ) : datasett && datasett.length > 0 ? (
        <Table>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>Felt</Table.HeaderCell>
              <Table.HeaderCell>Prop</Table.HeaderCell>
              <Table.HeaderCell>Dtype</Table.HeaderCell>
              <Table.HeaderCell>Type</Table.HeaderCell>
              <Table.HeaderCell>Kilde</Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {datasett.map((d) => (
              <Table.Row key={d.id}>
                <Table.Cell>
                  <Link asChild>
                    <RouterLink to={`/datasett/${d.id}`}>{d.felt}</RouterLink>
                  </Link>
                </Table.Cell>
                <Table.Cell style={{ fontFamily: 'monospace' }}>{d.prop}</Table.Cell>
                <Table.Cell>{d.dtype}</Table.Cell>
                <Table.Cell>{d.type}</Table.Cell>
                <Table.Cell>{d.kilde ?? '—'}</Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
      ) : null}
      </Card>
    </>
  );
}
