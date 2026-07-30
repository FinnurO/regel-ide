import { useEffect, useState, type FormEvent } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router';
import { Button, Heading, Link, Paragraph, Select, Table, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { KodelisteDto } from '../api/types';
import { useBruker } from '../bruker/BrukerContext';

export default function KodelisterListe() {
  const navigate = useNavigate();
  const { gjeldendeBruker } = useBruker();
  const [kodelister, setKodelister] = useState<KodelisteDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [nyKode, setNyKode] = useState('');
  const [nyNavn, setNyNavn] = useState('');
  const [nyType, setNyType] = useState('juridisk');
  const [oppretterFeil, setOppretterFeil] = useState<string | null>(null);
  const [oppretter, setOppretter] = useState(false);

  useEffect(() => {
    api
      .hentKodelister()
      .then(setKodelister)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av kodelister.'));
  }, []);

  async function opprett(e: FormEvent) {
    e.preventDefault();
    setOppretterFeil(null);
    setOppretter(true);
    try {
      const kodeliste = await api.opprettKodeliste({
        kode: nyKode.trim(), navn: nyNavn.trim(), type: nyType,
        virksomhetId: nyType === 'ekstern-referanse' ? null : (gjeldendeBruker?.virksomhetId ?? null),
        juridiskGrunnlagEid: null, eksternKildeUri: nyType === 'ekstern-referanse' ? '(fyll ut)' : null,
        eksternKildeVersjon: null,
      });
      navigate(`/kodelister/${kodeliste.id}`);
    } catch (err) {
      setOppretterFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av kodeliste.');
    } finally {
      setOppretter(false);
    }
  }

  return (
    <>
      <Heading level={1} data-size="lg">
        Kodelister
      </Heading>
      <Paragraph style={{ marginBottom: '1rem' }}>
        Kodelister/verdiregister (produktkrav kap. 3.7) — juridisk, teknisk eller ekstern-referanse
        (delt/uten virksomhet, refererer en autoritativ kilde).
      </Paragraph>

      <form onSubmit={opprett} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginBottom: '1.5rem' }}>
        <Textfield label="Kode" placeholder="f.eks. KL-VANDELSOMRADE" value={nyKode}
          onChange={(e) => setNyKode(e.target.value)} required />
        <Textfield label="Navn" value={nyNavn} onChange={(e) => setNyNavn(e.target.value)} required />
        <Select label="Type" value={nyType} onChange={(e) => setNyType(e.target.value)}>
          <Select.Option value="juridisk">Juridisk</Select.Option>
          <Select.Option value="teknisk">Teknisk</Select.Option>
          <Select.Option value="ekstern-referanse">Ekstern-referanse</Select.Option>
        </Select>
        <Button type="submit" disabled={oppretter || !nyKode.trim() || !nyNavn.trim()}>
          {oppretter ? 'Oppretter …' : 'Opprett'}
        </Button>
      </form>
      {oppretterFeil && <div className="feilmelding" style={{ marginBottom: '1rem' }}>{oppretterFeil}</div>}

      {feil && <div className="feilmelding">{feil}</div>}
      {!kodelister && !feil && <Paragraph>Laster …</Paragraph>}
      {kodelister && kodelister.length === 0 && <Paragraph>Ingen kodelister funnet.</Paragraph>}

      {kodelister && kodelister.length > 0 && (
        <Table border>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>Kode</Table.HeaderCell>
              <Table.HeaderCell>Navn</Table.HeaderCell>
              <Table.HeaderCell>Type</Table.HeaderCell>
              <Table.HeaderCell>Antall koder</Table.HeaderCell>
              <Table.HeaderCell>Status</Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            {kodelister.map((k) => (
              <Table.Row key={k.id}>
                <Table.Cell style={{ fontFamily: 'monospace' }}>
                  <Link asChild>
                    <RouterLink to={`/kodelister/${k.id}`}>{k.kode}</RouterLink>
                  </Link>
                </Table.Cell>
                <Table.Cell>{k.navn}</Table.Cell>
                <Table.Cell>{k.type}</Table.Cell>
                <Table.Cell>{k.koder.length}</Table.Cell>
                <Table.Cell>{k.status}</Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>
      )}
    </>
  );
}
