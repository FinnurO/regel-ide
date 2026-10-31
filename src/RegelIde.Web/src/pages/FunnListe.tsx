import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Card, Field, Heading, Label, Link, Paragraph, Search, Select, Spinner, Table } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { FunnDto, FunnKategori, FunnStatus } from '../api/types';
import { Metatekst } from '../entitet/Metatekst';
import { ALLE_KATEGORIER, ALLE_STATUSER, FunnMerker, KATEGORI_TEKST, STATUS_VISNING } from '../funn/FunnVisning';

/**
 * [Ny, issue #429 «Funn til vurdering», 2026-10-30] Registeret: steder der rettskildene ikke stemmer med seg selv eller med
 * virkeligheten. Listemalen i docs/09 §28: søk og filtre øverst, tabell, og et kort som alltid vises, også når lista er tom.
 * Tettheten er vanlig (ikke `compact`): i PR 1 er det fem funn (docs/09 §26). Maskinregelen for utgåtte aktører (PR 2) gir rundt
 * 770, og da skal tettheten og grupperingen per aktør vurderes på nytt.
 */
/** [Ny, #429 PR 2, kaldsjekken 2026-10-31] Et maskinfunn kan peke på 69 bestemmelser (advokatloven og Advokattilsynet i testbasen).
 * Lista viser de tre første og antallet resten; hele lista står på detaljsiden. */
function bestemmelseskort(f: FunnDto): string {
  const alle = f.koblinger.filter((k) => k.rolle === 'gjelder_bestemmelse' || k.rolle === 'motstridende_bestemmelse').map((k) => k.etikett);
  if (alle.length === 0) return '—';
  return alle.length <= 3 ? alle.join('; ') : `${alle.slice(0, 3).join('; ')} og ${alle.length - 3} til`;
}

export default function FunnListe() {
  const [funn, setFunn] = useState<FunnDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [kategori, setKategori] = useState<FunnKategori | ''>('');
  const [status, setStatus] = useState<FunnStatus | ''>('');
  const [sok, setSok] = useState('');

  useEffect(() => {
    setFunn(null);
    api.hentFunn({ kategori: kategori || undefined, status: status || undefined })
      .then((r) => { setFunn(r); setFeil(null); })
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av funn.'));
  }, [kategori, status]);

  const synlige = useMemo(() => {
    const q = sok.trim().toLowerCase();
    if (!funn || !q) return funn ?? [];
    return funn.filter((f) => [f.tittel, f.beskrivelse, ...f.koblinger.map((k) => k.etikett)].some((t) => t.toLowerCase().includes(q)));
  }, [funn, sok]);

  return (
    <>
      <Heading level={1} data-size="lg" style={{ marginBottom: '0.2rem' }}>Funn til vurdering</Heading>
      <Metatekst style={{ marginBottom: '1.25rem', maxWidth: '52rem' }}>
        Steder der rettskildene ikke stemmer med seg selv eller med virkeligheten: utgåtte aktører, mulig motstrid, uklarheter og hull.
        Det er observasjoner fra modellen med kilder, ikke påstander om feil i loven. Juristen prøver først å forklare et funn bort. Bare
        funn som ikke lar seg forklare bort, og som en publiseringsansvarlig har godkjent, publiseres på nettsiden (#429).
      </Metatekst>

      <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'flex-end', marginBottom: '0.75rem', flexWrap: 'wrap' }}>
        <Field style={{ minWidth: '16rem' }}>
          <Label>Søk</Label>
          <Search data-size="sm">
            <Search.Input aria-label="Søk i funn" value={sok} onChange={(e) => setSok(e.target.value)} placeholder="Tittel, tekst eller bestemmelse …" />
            <Search.Clear onClick={() => setSok('')} />
          </Search>
        </Field>
        <Field style={{ minWidth: '12rem' }}>
          <Label>Kategori</Label>
          <Select data-size="sm" value={kategori} onChange={(e) => setKategori(e.target.value as FunnKategori | '')}>
            <Select.Option value="">Alle</Select.Option>
            {ALLE_KATEGORIER.map((k) => <Select.Option key={k} value={k}>{KATEGORI_TEKST[k]}</Select.Option>)}
          </Select>
        </Field>
        <Field style={{ minWidth: '12rem' }}>
          <Label>Status</Label>
          <Select data-size="sm" value={status} onChange={(e) => setStatus(e.target.value as FunnStatus | '')}>
            <Select.Option value="">Alle</Select.Option>
            {ALLE_STATUSER.map((s) => <Select.Option key={s} value={s}>{STATUS_VISNING[s].tekst}</Select.Option>)}
          </Select>
        </Field>
        <Button data-size="sm" asChild><RouterLink to="/funn/ny">Registrer funn</RouterLink></Button>
      </div>

      {feil && <Alert data-color="danger" data-size="sm" style={{ marginBottom: '0.75rem' }}>{feil}</Alert>}

      <Card style={{ padding: 0, overflow: 'hidden' }} data-size="sm">
        {funn === null && !feil ? (
          <div style={{ padding: '1rem' }}><Spinner aria-label="Laster funn …" data-size="sm" /></div>
        ) : synlige.length === 0 ? (
          <Paragraph data-size="sm" style={{ padding: '1rem', margin: 0 }}>
            {funn && funn.length > 0 ? 'Ingen funn passer søket.' : 'Ingen funn med disse filtrene.'}
          </Paragraph>
        ) : (
          <Table data-size="sm">
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>Funn</Table.HeaderCell>
                <Table.HeaderCell>Status</Table.HeaderCell>
                <Table.HeaderCell>Bestemmelser</Table.HeaderCell>
                <Table.HeaderCell>Funnet i</Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {synlige.map((f) => (
                <Table.Row key={f.id}>
                  <Table.Cell>
                    <Link asChild><RouterLink to={`/funn/${f.id}`}>{f.tittel}</RouterLink></Link>
                  </Table.Cell>
                  <Table.Cell>
                    <span style={{ display: 'flex', gap: '0.3rem', flexWrap: 'wrap' }}><FunnMerker funn={f} /></span>
                  </Table.Cell>
                  <Table.Cell>
                    <Metatekst as="span">{bestemmelseskort(f)}</Metatekst>
                  </Table.Cell>
                  <Table.Cell><Metatekst as="span">{f.funnetI ?? '—'}</Metatekst></Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table>
        )}
      </Card>
    </>
  );
}
