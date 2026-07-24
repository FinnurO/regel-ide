import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import { Checkbox, Heading, Link, Paragraph, Table } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { RettskildeSammendrag } from '../api/types';
import { useBruker } from '../bruker/BrukerContext';

export default function RettskilderListe() {
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [kunMine, setKunMine] = useState(false);
  const { gjeldendeBruker } = useBruker();

  useEffect(() => {
    setFeil(null);
    setRettskilder(null);
    const virksomhetId = kunMine && gjeldendeBruker ? gjeldendeBruker.virksomhetId : undefined;
    api
      .hentRettskilder(virksomhetId)
      .then(setRettskilder)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av rettskilder.'));
  }, [kunMine, gjeldendeBruker]);

  return (
    <>
      <Heading level={1} data-size="lg">
        Rettskilder
      </Heading>
      <Paragraph style={{ marginBottom: '1rem' }}>
        Åpne data — delte/nasjonale kilder (Lov/Forskrift fra Lovdata) og alle virksomheters
        publiserte lokale kilder. Kladder (status «Utkast») vises aldri her.
      </Paragraph>

      {gjeldendeBruker && (
        <Checkbox
          label={`Vis kun ${gjeldendeBruker.virksomhetNavn} sine egne kilder`}
          checked={kunMine}
          onChange={(e) => setKunMine(e.target.checked)}
          style={{ marginBottom: '1rem' }}
        />
      )}

      {feil && <div className="feilmelding">{feil}</div>}

      {!rettskilder && !feil && <Paragraph>Laster …</Paragraph>}

      {rettskilder && rettskilder.length === 0 && <Paragraph>Ingen rettskilder funnet.</Paragraph>}

      {rettskilder && rettskilder.length > 0 && (
        <Table className="rettskilde-tabell" border>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>Tittel</Table.HeaderCell>
              <Table.HeaderCell>Kildetype</Table.HeaderCell>
              <Table.HeaderCell>ELI</Table.HeaderCell>
              <Table.HeaderCell>Eierskap</Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {rettskilder.map((r) => (
              <Table.Row key={r.id}>
                <Table.Cell>
                  <Link asChild>
                    <RouterLink to={`/rettskilder/${r.id}`}>{r.kortnavn ?? r.tittel}</RouterLink>
                  </Link>
                </Table.Cell>
                <Table.Cell>{r.kildetype}</Table.Cell>
                <Table.Cell style={{ fontSize: 'var(--ds-font-size-1)' }}>{r.eli ?? '—'}</Table.Cell>
                <Table.Cell>
                  {r.virksomhetId ? (
                    <span className="badge-virksomhet">Virksomhetseid</span>
                  ) : (
                    <span className="badge-delt">Delt / nasjonal</span>
                  )}
                </Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
      )}
    </>
  );
}
