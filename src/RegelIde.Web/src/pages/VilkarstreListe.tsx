import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Heading, Link, Paragraph, Table } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { TjenesteDto } from '../api/types';

/**
 * Rotnode-kobling opprettes/endres/fjernes kun på TjenesteDetalj.tsx (2026-07-31, fasit-runde 5) —
 * ett sted å vedlikeholde koblingen, i stedet for et separat, write-once opprettelsesskjema her som
 * ikke lot deg se eller angre en feilaktig opprettelse i etterkant.
 */
export default function VilkarstreListe() {
  const [tjenester, setTjenester] = useState<TjenesteDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  useEffect(() => {
    api.hentTjenester().then(setTjenester)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av tjenester.'));
  }, []);

  return (
    <>
      <Heading level={1} data-size="lg">
        Vilkårstre
      </Heading>
      <Paragraph style={{ marginBottom: '1rem' }}>
        Grafeditor for Vilkår/Regel/Unntak (produktkrav kap. 3.4) — velg en tjeneste for å åpne dens vilkårstre.
        Rotnode opprettes/endres på selve tjenestesiden.
      </Paragraph>

      {feil && <div className="feilmelding">{feil}</div>}
      {!tjenester && !feil && <Paragraph>Laster …</Paragraph>}
      {tjenester && tjenester.length === 0 && <Paragraph>Ingen tjenester funnet — opprett en under «Tjenester» først.</Paragraph>}

      {tjenester && tjenester.length > 0 && (
        <Table border>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>Tjeneste</Table.HeaderCell>
              <Table.HeaderCell>Vilkårstre</Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {tjenester.map((t) => (
              <Table.Row key={t.id}>
                <Table.Cell>{t.tittel}</Table.Cell>
                <Table.Cell>
                  {t.rotnodeId ? (
                    <Link asChild>
                      <RouterLink to={`/vilkarstre/${t.rotnodeId}`}>Åpne vilkårstre</RouterLink>
                    </Link>
                  ) : (
                    <Link asChild>
                      <RouterLink to={`/tjenester/${t.id}`}>Opprett rotnode på tjenestesiden →</RouterLink>
                    </Link>
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
