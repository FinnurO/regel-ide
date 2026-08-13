import { useEffect, useState } from 'react';
import { Link as RouterLink, useParams } from 'react-router';
import { Heading, Link, Paragraph, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { NettsideDetaljDto } from '../api/types';
import { RaaTekstMedLenker } from '../nettside/RaaTekstMedLenker';

export default function NettsideDetalj() {
  const { id } = useParams<{ id: string }>();
  const [detalj, setDetalj] = useState<NettsideDetaljDto | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    setFeil(null);
    setDetalj(null);
    api
      .hentNettside(id)
      .then(setDetalj)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av nettsiden.'));
  }, [id]);

  if (feil) return <div className="feilmelding">{feil}</div>;
  if (!detalj) return <Paragraph>Laster …</Paragraph>;

  return (
    <>
      <Link asChild>
        <RouterLink to="/nettsider">← Tilbake til listen</RouterLink>
      </Link>
      <Heading level={1} data-size="lg" style={{ marginTop: '0.5rem' }}>
        {detalj.tittel ?? '(uten tittel)'}
      </Heading>
      <Paragraph style={{ fontFamily: 'monospace', fontSize: 'var(--ds-font-size-1)', marginBottom: '1rem' }}>
        <Link href={detalj.kanoniskUrl} target="_blank" rel="noopener noreferrer">{detalj.kanoniskUrl}</Link>
      </Paragraph>

      <section style={{ marginBottom: '1.5rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.5rem' }}>
          Navigasjonsstier
        </Heading>
        {detalj.stier.length === 0 && (
          <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>
            Ingen kjent navigasjonssti for denne siden.
          </Paragraph>
        )}
        {detalj.stier.length > 0 && (
          <ul style={{ margin: 0, padding: 0, listStyle: 'none', display: 'flex', flexDirection: 'column', gap: '0.35rem' }}>
            {detalj.stier.map((s, i) => (
              <li key={i} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                <Tag data-color={s.stiType === 'tematisk' ? 'info' : 'success'} data-size="sm">{s.stiType}</Tag>
                <span style={{ fontFamily: 'monospace', fontSize: 'var(--ds-font-size-1)' }}>{s.sti}</span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section style={{ marginBottom: '1.5rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.5rem' }}>
          Innhold
        </Heading>
        {detalj.raaTekst ? (
          <div style={{ maxWidth: '60rem' }}>
            <RaaTekstMedLenker raaTekst={detalj.raaTekst} lenker={detalj.lenker} />
          </div>
        ) : (
          <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Ingen tekst hentet for denne siden.</Paragraph>
        )}
      </section>

      <section>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.5rem' }}>
          Lenker ({detalj.lenker.length})
        </Heading>
        {detalj.lenker.length === 0 && <Paragraph>Ingen utgående lenker funnet.</Paragraph>}
        {detalj.lenker.length > 0 && (
          <Table border data-size="sm">
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>Type</Table.HeaderCell>
                <Table.HeaderCell>Ankertekst</Table.HeaderCell>
                <Table.HeaderCell>Href</Table.HeaderCell>
                <Table.HeaderCell>Oppløsning</Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {detalj.lenker.map((l) => (
                <Table.Row key={l.id}>
                  <Table.Cell><Tag data-color={l.type === 'lovdatalenke' ? 'warning' : 'neutral'} data-size="sm">{l.type}</Tag></Table.Cell>
                  <Table.Cell>{l.ankerTekst ?? '—'}</Table.Cell>
                  <Table.Cell style={{ fontFamily: 'monospace', fontSize: 'var(--ds-font-size-1)', wordBreak: 'break-all' }}>
                    {l.raaHref}
                  </Table.Cell>
                  <Table.Cell>
                    {l.tilNettsideDokumentId && (
                      <Link asChild>
                        <RouterLink to={`/nettsider/${l.tilNettsideDokumentId}`}>
                          {l.tilNettsideDokumentTittel ?? l.tilNettsideDokumentKanoniskUrl}
                        </RouterLink>
                      </Link>
                    )}
                    {l.tilRettskildeId && (
                      <Link asChild>
                        <RouterLink to={`/rettskilder/${l.tilRettskildeId}`}>
                          {l.tilRettskildeTittel ?? l.tilRettskildeEli}
                        </RouterLink>
                      </Link>
                    )}
                    {!l.tilNettsideDokumentId && !l.tilRettskildeId && (
                      <span style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>uløst / ekstern</span>
                    )}
                  </Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table>
        )}
      </section>
    </>
  );
}
