import { useEffect, useState, type FormEvent } from 'react';
import { Link as RouterLink, useParams } from 'react-router';
import { Alert, Button, Heading, Link, Paragraph, Select, Spinner, Table, Tag, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenke } from '../api/eidLenker';
import type { KodelisteDto, RettskildeSammendrag } from '../api/types';

const STATUSER = ['utkast', 'under_revisjon', 'validert', 'publisert', 'tilbaketrukket', 'arkivert'];

export default function KodelisteDetalj() {
  const { id } = useParams<{ id: string }>();
  const [kodeliste, setKodeliste] = useState<KodelisteDto | null>(null);
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [feil, setFeil] = useState<string | null>(null);
  const [statusEndres, setStatusEndres] = useState(false);
  const [statusFeil, setStatusFeil] = useState<string | null>(null);

  const [nyKode, setNyKode] = useState('');
  const [nyTerm, setNyTerm] = useState('');
  const [leggerTilKode, setLeggerTilKode] = useState(false);
  const [kodeFeil, setKodeFeil] = useState<string | null>(null);

  function lastKodeliste() {
    if (!id) return;
    api.hentKodeliste(id).then(setKodeliste)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av kodeliste.'));
  }

  useEffect(lastKodeliste, [id]);
  useEffect(() => { api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([])); }, []);

  async function endreStatus(nyStatus: string) {
    if (!id) return;
    setStatusEndres(true);
    setStatusFeil(null);
    try {
      const oppdatert = await api.settKodelisteStatus(id, { status: nyStatus });
      setKodeliste(oppdatert);
    } catch (err) {
      setStatusFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved statusendring.');
    } finally {
      setStatusEndres(false);
    }
  }

  async function leggTilKode(e: FormEvent) {
    e.preventDefault();
    if (!id) return;
    setKodeFeil(null);
    setLeggerTilKode(true);
    try {
      await api.leggTilKodelisteKode(id, { kode: nyKode.trim(), term: nyTerm.trim(), definisjon: null, gyldigFra: null, gyldigTil: null });
      setNyKode('');
      setNyTerm('');
      lastKodeliste();
    } catch (err) {
      setKodeFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av kode.');
    } finally {
      setLeggerTilKode(false);
    }
  }

  async function fjernKode(kodeId: string) {
    await api.fjernKodelisteKode(kodeId);
    lastKodeliste();
  }

  if (feil) return <Alert data-color="danger">{feil}</Alert>;
  if (!kodeliste) return <Spinner aria-label="Laster …" data-size="sm" />;

  const erEksternReferanse = kodeliste.type === 'ekstern-referanse';

  return (
    <>
      <Heading level={1} data-size="lg" style={{ fontFamily: 'monospace' }}>
        {kodeliste.kode}
      </Heading>
      <Paragraph style={{ marginBottom: '0.5rem' }}>{kodeliste.navn}</Paragraph>
      <Tag data-color="info" style={{ marginBottom: '1.5rem' }}>{kodeliste.type} · {kodeliste.status}</Tag>

      {erEksternReferanse && (
        <Paragraph style={{ marginBottom: '1.5rem' }}>
          Ekstern kilde: <Link href={kodeliste.eksternKildeUri ?? undefined} target="_blank" rel="noopener noreferrer">{kodeliste.eksternKildeUri}</Link>
          {kodeliste.eksternKildeVersjon && ` (versjon ${kodeliste.eksternKildeVersjon})`}
        </Paragraph>
      )}
      {kodeliste.juridiskGrunnlagEid && (
        <Paragraph style={{ marginBottom: '1.5rem', fontFamily: 'monospace', fontSize: 'var(--ds-font-size-1)' }}>
          Juridisk grunnlag:{' '}
          {(() => {
            const href = rettskildeLenke(kodeliste.juridiskGrunnlagEid, rettskilder);
            return href ? (
              <Link asChild><RouterLink to={href}>{kodeliste.juridiskGrunnlagEid}</RouterLink></Link>
            ) : (
              kodeliste.juridiskGrunnlagEid
            );
          })()}
        </Paragraph>
      )}

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Koder
        </Heading>
        {kodeliste.koder.length === 0 ? (
          <Paragraph>Ingen koder registrert ennå.</Paragraph>
        ) : (
          <Table border style={{ marginBottom: '1rem' }}>
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>Kode</Table.HeaderCell>
                <Table.HeaderCell>Term</Table.HeaderCell>
                <Table.HeaderCell>Definisjon</Table.HeaderCell>
                <Table.HeaderCell></Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {kodeliste.koder.map((k) => (
                <Table.Row key={k.id}>
                  <Table.Cell style={{ fontFamily: 'monospace' }}>{k.kode}</Table.Cell>
                  <Table.Cell>{k.term}</Table.Cell>
                  <Table.Cell>{k.definisjon ?? '—'}</Table.Cell>
                  <Table.Cell>
                    <Button variant="tertiary" data-size="sm" onClick={() => fjernKode(k.id)}>Fjern</Button>
                  </Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table>
        )}

        <form onSubmit={leggTilKode} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end' }}>
          <Textfield label="Ny kode" value={nyKode} onChange={(e) => setNyKode(e.target.value)} required />
          <Textfield label="Term" value={nyTerm} onChange={(e) => setNyTerm(e.target.value)} required />
          <Button type="submit" disabled={leggerTilKode || !nyKode.trim() || !nyTerm.trim()}>
            {leggerTilKode ? 'Legger til …' : 'Ny kode'}
          </Button>
        </form>
        {kodeFeil && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{kodeFeil}</Alert>}
      </section>

      <section>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Status
        </Heading>
        {erEksternReferanse ? (
          <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>
            Ekstern-referanse-kodelister har ikke et publiseringssteg — alltid «{kodeliste.status}».
          </Paragraph>
        ) : (
          <Select value={kodeliste.status} disabled={statusEndres} onChange={(e) => endreStatus(e.target.value)} style={{ maxWidth: '16rem' }}>
            {STATUSER.map((s) => (
              <Select.Option key={s} value={s}>{s}</Select.Option>
            ))}
          </Select>
        )}
        {statusFeil && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{statusFeil}</Alert>}
      </section>
    </>
  );
}
