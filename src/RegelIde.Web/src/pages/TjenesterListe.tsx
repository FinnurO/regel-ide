import { useEffect, useState, type FormEvent } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router';
import { Button, Heading, Link, Paragraph, Table, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { TjenesteDto } from '../api/types';

export default function TjenesterListe() {
  const navigate = useNavigate();
  const [tjenester, setTjenester] = useState<TjenesteDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [nyTittel, setNyTittel] = useState('');
  const [oppretterFeil, setOppretterFeil] = useState<string | null>(null);
  const [oppretter, setOppretter] = useState(false);

  useEffect(() => {
    api
      .hentTjenester()
      .then(setTjenester)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av tjenester.'));
  }, []);

  async function opprett(e: FormEvent) {
    e.preventDefault();
    setOppretterFeil(null);
    setOppretter(true);
    try {
      const tjeneste = await api.opprettTjeneste({
        tittel: nyTittel.trim(), beskrivelse: null, kompetentMyndighet: null, output: null,
        tjenestetype: null, malgruppe: null, kanaler: null, kostnad: null, behandlingstid: null,
        kontaktpunkt: null, konsekvensVedBrudd: null, sprak: null,
      });
      navigate(`/tjenester/${tjeneste.id}`);
    } catch (err) {
      setOppretterFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av tjeneste.');
    } finally {
      setOppretter(false);
    }
  }

  return (
    <>
      <Heading level={1} data-size="lg">
        Tjenester
      </Heading>
      <Paragraph style={{ marginBottom: '1rem' }}>
        Tjenestedefinisjoner (CPSV-AP-NO, produktkrav kap. 3.2) — virksomhetens eget arbeidsprodukt.
      </Paragraph>

      <form onSubmit={opprett} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginBottom: '1.5rem' }}>
        <Textfield label="Ny tjeneste" placeholder="f.eks. Alminnelig skjenkebevilling" value={nyTittel}
          onChange={(e) => setNyTittel(e.target.value)} required />
        <Button type="submit" disabled={oppretter || !nyTittel.trim()}>
          {oppretter ? 'Oppretter …' : 'Opprett'}
        </Button>
      </form>
      {oppretterFeil && <div className="feilmelding" style={{ marginBottom: '1rem' }}>{oppretterFeil}</div>}

      {feil && <div className="feilmelding">{feil}</div>}
      {!tjenester && !feil && <Paragraph>Laster …</Paragraph>}
      {tjenester && tjenester.length === 0 && <Paragraph>Ingen tjenester funnet.</Paragraph>}

      {tjenester && tjenester.length > 0 && (
        <Table border>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>Tittel</Table.HeaderCell>
              <Table.HeaderCell>Tjenestetype</Table.HeaderCell>
              <Table.HeaderCell>Status</Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {tjenester.map((t) => (
              <Table.Row key={t.id}>
                <Table.Cell>
                  <Link asChild>
                    <RouterLink to={`/tjenester/${t.id}`}>{t.tittel}</RouterLink>
                  </Link>
                </Table.Cell>
                <Table.Cell>{t.tjenestetype ?? '—'}</Table.Cell>
                <Table.Cell>{t.status}</Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
      )}
    </>
  );
}
