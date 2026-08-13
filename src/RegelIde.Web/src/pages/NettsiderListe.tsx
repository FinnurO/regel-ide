import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Heading, Link, Paragraph, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { NettsideSammendragDto } from '../api/types';

const STITYPE_FARGE: Record<string, 'info' | 'success'> = { tematisk: 'info', organisatorisk: 'success' };

export default function NettsiderListe() {
  const [nettsider, setNettsider] = useState<NettsideSammendragDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  useEffect(() => {
    api
      .hentNettsider()
      .then(setNettsider)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av nettsider.'));
  }, []);

  return (
    <>
      <Heading level={1} data-size="lg">
        Nettsider
      </Heading>
      <Paragraph style={{ marginBottom: '1rem' }}>
        Kommunale nettsider hentet inn i dokumentgrafen (docs/15-handbok-dokumentgraf-notat.md §3.1) —
        deterministiske lenker (Lovdata-lenker og interne lenker) er løst mot importerte rettskilder og
        andre nettsider der det er mulig.
      </Paragraph>

      {feil && <div className="feilmelding">{feil}</div>}
      {!nettsider && !feil && <Paragraph>Laster …</Paragraph>}
      {nettsider && nettsider.length === 0 && <Paragraph>Ingen nettsider funnet.</Paragraph>}

      {nettsider && nettsider.length > 0 && (
        <Table border>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>Tittel</Table.HeaderCell>
              <Table.HeaderCell>Kanonisk URL</Table.HeaderCell>
              <Table.HeaderCell>Stitype</Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {nettsider.map((n) => (
              <Table.Row key={n.id}>
                <Table.Cell>
                  <Link asChild>
                    <RouterLink to={`/nettsider/${n.id}`}>{n.tittel ?? '(uten tittel)'}</RouterLink>
                  </Link>
                </Table.Cell>
                <Table.Cell style={{ fontFamily: 'monospace', fontSize: 'var(--ds-font-size-1)' }}>
                  {n.kanoniskUrl}
                </Table.Cell>
                <Table.Cell>
                  <div style={{ display: 'flex', gap: '0.35rem' }}>
                    {n.stiTyper.length === 0 && <span style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>—</span>}
                    {n.stiTyper.map((t) => (
                      <Tag key={t} data-color={STITYPE_FARGE[t] ?? 'neutral'} data-size="sm">{t}</Tag>
                    ))}
                  </div>
                </Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
      )}
    </>
  );
}
