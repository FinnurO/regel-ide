import { useEffect, useState, type FormEvent } from 'react';
import { useParams } from 'react-router-dom';
import { Button, Heading, Paragraph, Select, Tag, Textarea, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { BegrepDto } from '../api/types';

const STATUSER = ['utkast', 'under_revisjon', 'validert', 'publisert', 'tilbaketrukket', 'arkivert'];

export default function BegrepDetalj() {
  const { id } = useParams<{ id: string }>();
  const [begrep, setBegrep] = useState<BegrepDto | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  const [term, setTerm] = useState('');
  const [definisjon, setDefinisjon] = useState('');
  const [lovreferanseEid, setLovreferanseEid] = useState('');
  const [begrepstype, setBegrepstype] = useState('faktabegrep');
  const [lagrer, setLagrer] = useState(false);
  const [lagreFeil, setLagreFeil] = useState<string | null>(null);
  const [statusEndres, setStatusEndres] = useState(false);

  function fyllSkjemaFra(b: BegrepDto) {
    setTerm(b.term);
    setDefinisjon(b.definisjon);
    setLovreferanseEid(b.lovreferanseEid ?? '');
    setBegrepstype(b.begrepstype);
  }

  useEffect(() => {
    if (!id) return;
    api.hentBegrep(id).then((b) => { setBegrep(b); fyllSkjemaFra(b); })
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av begrep.'));
  }, [id]);

  async function lagre(e: FormEvent) {
    e.preventDefault();
    if (!id || !begrep) return;
    setLagreFeil(null);
    setLagrer(true);
    try {
      const oppdatert = await api.oppdaterBegrep(id, {
        term: term.trim(), definisjon: definisjon.trim(), lovreferanseEid: lovreferanseEid.trim() || null,
        gjelderFor: begrep.gjelderFor, kodelisteReferanseId: begrep.kodelisteReferanseId,
        skosUrl: begrep.skosUrl, begrepstype,
      });
      setBegrep(oppdatert);
    } catch (err) {
      setLagreFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring.');
    } finally {
      setLagrer(false);
    }
  }

  async function endreStatus(nyStatus: string) {
    if (!id) return;
    setStatusEndres(true);
    setLagreFeil(null);
    try {
      const oppdatert = await api.settBegrepStatus(id, { status: nyStatus });
      setBegrep(oppdatert);
    } catch (err) {
      setLagreFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved statusendring.');
    } finally {
      setStatusEndres(false);
    }
  }

  if (feil) return <div className="feilmelding">{feil}</div>;
  if (!begrep) return <Paragraph>Laster …</Paragraph>;

  return (
    <>
      <Heading level={1} data-size="lg">
        «{begrep.term}»
      </Heading>
      <Tag data-color="info" style={{ marginBottom: '1.5rem' }}>{begrep.status}</Tag>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Egenskaper
        </Heading>
        <form onSubmit={lagre} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', maxWidth: '40rem' }}>
          <Textfield label="Term" value={term} onChange={(e) => setTerm(e.target.value)} required />
          <Textarea label="Definisjon" value={definisjon} onChange={(e) => setDefinisjon(e.target.value)} rows={3} required />
          <Textfield label="Lovreferanse (eId)" value={lovreferanseEid} onChange={(e) => setLovreferanseEid(e.target.value)}
            style={{ fontFamily: 'monospace' }} />
          <Select label="Begrepstype" value={begrepstype} onChange={(e) => setBegrepstype(e.target.value)}>
            <Select.Option value="faktabegrep">Faktabegrep</Select.Option>
            <Select.Option value="handlingsbegrep">Handlingsbegrep</Select.Option>
          </Select>
          {lagreFeil && <div className="feilmelding">{lagreFeil}</div>}
          <div>
            <Button type="submit" disabled={lagrer}>{lagrer ? 'Lagrer …' : 'Lagre'}</Button>
          </div>
        </form>
      </section>

      <section>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Status
        </Heading>
        <Select value={begrep.status} disabled={statusEndres} onChange={(e) => endreStatus(e.target.value)} style={{ maxWidth: '16rem' }}>
          {STATUSER.map((s) => (
            <Select.Option key={s} value={s}>{s}</Select.Option>
          ))}
        </Select>
      </section>
    </>
  );
}
