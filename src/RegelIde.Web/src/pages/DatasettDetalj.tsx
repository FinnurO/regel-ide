import { useEffect, useState, type FormEvent } from 'react';
import { useParams } from 'react-router';
import { Button, Field, Heading, Label, Paragraph, Select, Table, Tag, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { DatasettDto, DatasettVerdiDto, VirksomhetDto } from '../api/types';

/** Viser en Datasett-verdi lesbart — de er lagret som JSON (streng/tall/boolsk/liste), ikke rå tekst. */
function VisVerdi({ verdiJson }: { verdiJson: string }) {
  try {
    const verdi = JSON.parse(verdiJson);
    return <>{typeof verdi === 'string' ? verdi : JSON.stringify(verdi)}</>;
  } catch {
    return <>{verdiJson}</>;
  }
}

export default function DatasettDetalj() {
  const { id } = useParams<{ id: string }>();
  const [datasett, setDatasett] = useState<DatasettDto | null>(null);
  const [verdier, setVerdier] = useState<DatasettVerdiDto[] | null>(null);
  const [virksomheter, setVirksomheter] = useState<VirksomhetDto[]>([]);
  const [feil, setFeil] = useState<string | null>(null);

  const [nyVirksomhetId, setNyVirksomhetId] = useState('');
  const [nyVerdi, setNyVerdi] = useState('');
  const [nyKilde, setNyKilde] = useState('');
  const [lagrer, setLagrer] = useState(false);
  const [skjemaFeil, setSkjemaFeil] = useState<string | null>(null);

  function lastVerdier() {
    if (!id) return;
    api.hentDatasettVerdier(id).then(setVerdier).catch(() => setVerdier([]));
  }

  useEffect(() => {
    if (!id) return;
    api.hentDatasett().then((liste) => {
      const d = liste.find((x) => x.id === id);
      if (!d) {
        setFeil(`Fant ingen datasett med id '${id}'.`);
        return;
      }
      setDatasett(d);
    }).catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av datasett.'));
    lastVerdier();
  }, [id]);

  useEffect(() => { api.hentVirksomheter().then(setVirksomheter).catch(() => setVirksomheter([])); }, []);

  async function leggTilVerdi(e: FormEvent) {
    e.preventDefault();
    if (!id || !nyVerdi.trim()) return;
    setSkjemaFeil(null);
    setLagrer(true);
    try {
      await api.settDatasettVerdi(id, {
        virksomhetId: nyVirksomhetId || null,
        verdiJson: JSON.stringify(nyVerdi.trim()),
        kilde: nyKilde.trim() || null,
      });
      setNyVirksomhetId('');
      setNyVerdi('');
      setNyKilde('');
      lastVerdier();
    } catch (err) {
      setSkjemaFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring av verdi.');
    } finally {
      setLagrer(false);
    }
  }

  async function fjernVerdi(verdiId: string) {
    await api.fjernDatasettVerdi(verdiId);
    lastVerdier();
  }

  if (feil) return <div className="feilmelding">{feil}</div>;
  if (!datasett || verdier === null) return <Paragraph>Laster …</Paragraph>;

  const virksomhetNavn = new Map(virksomheter.map((v) => [v.id, v.navn]));
  const standardverdi = verdier.find((v) => v.virksomhetId === null);
  const kommuneverdier = verdier.filter((v) => v.virksomhetId !== null);
  const virksomheterUtenVerdi = virksomheter.filter((v) => !kommuneverdier.some((k) => k.virksomhetId === v.id));

  return (
    <>
      <Heading level={1} data-size="lg" style={{ fontFamily: 'monospace' }}>{datasett.prop}</Heading>
      <Paragraph style={{ marginBottom: '0.5rem' }}>{datasett.felt}</Paragraph>
      <Tag data-color="info" style={{ marginBottom: '1.5rem' }}>{datasett.dtype} · {datasett.type}</Tag>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Kommunale/nasjonale verdier
        </Heading>
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
          Standardverdien brukes for enhver virksomhet uten egen registrert verdi — en teknisk
          standardverdi, ikke en juridisk norm om at disse virksomhetene faktisk mangler egne regler.
        </Paragraph>
        <Table border style={{ marginBottom: '1rem' }}>
          <Table.Head>
            <Table.Row>
              <Table.HeaderCell>Virksomhet</Table.HeaderCell>
              <Table.HeaderCell>Verdi</Table.HeaderCell>
              <Table.HeaderCell>Kilde</Table.HeaderCell>
              <Table.HeaderCell></Table.HeaderCell>
            </Table.Row>
          </Table.Head>
          <Table.Body>
            <Table.Row>
              <Table.Cell><Tag data-color="neutral" data-size="sm">Standardverdi</Tag></Table.Cell>
              <Table.Cell>{standardverdi ? <VisVerdi verdiJson={standardverdi.verdiJson} /> : '—'}</Table.Cell>
              <Table.Cell>{standardverdi?.kilde ?? '—'}</Table.Cell>
              <Table.Cell>
                {standardverdi && (
                  <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjernVerdi(standardverdi.id)}>Fjern</Button>
                )}
              </Table.Cell>
            </Table.Row>
            {kommuneverdier.map((v) => (
              <Table.Row key={v.id}>
                <Table.Cell>{virksomhetNavn.get(v.virksomhetId!) ?? v.virksomhetId}</Table.Cell>
                <Table.Cell><VisVerdi verdiJson={v.verdiJson} /></Table.Cell>
                <Table.Cell>{v.kilde ?? '—'}</Table.Cell>
                <Table.Cell>
                  <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjernVerdi(v.id)}>Fjern</Button>
                </Table.Cell>
              </Table.Row>
            ))}
          </Table.Body>
        </Table>

        <form onSubmit={leggTilVerdi} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <Field>
            <Label>Virksomhet</Label>
            <Select data-size="sm" value={nyVirksomhetId} onChange={(e) => setNyVirksomhetId(e.target.value)}>
              <Select.Option value="">(nasjonal standardverdi)</Select.Option>
              {virksomheterUtenVerdi.map((v) => <Select.Option key={v.id} value={v.id}>{v.navn}</Select.Option>)}
            </Select>
          </Field>
          <Textfield data-size="sm" label="Verdi" value={nyVerdi} onChange={(e) => setNyVerdi(e.target.value)} />
          <Textfield data-size="sm" label="Kilde" value={nyKilde} onChange={(e) => setNyKilde(e.target.value)}
            style={{ minWidth: '18rem' }} />
          <Button data-size="sm" type="submit" disabled={lagrer || !nyVerdi.trim()}>
            {lagrer ? 'Lagrer …' : 'Legg til'}
          </Button>
        </form>
        {skjemaFeil && <div className="feilmelding" style={{ marginTop: '0.5rem' }}>{skjemaFeil}</div>}
      </section>
    </>
  );
}
