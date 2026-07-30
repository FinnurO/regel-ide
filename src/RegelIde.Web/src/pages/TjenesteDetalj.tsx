import { useEffect, useState, type FormEvent } from 'react';
import { Link as RouterLink, useParams } from 'react-router';
import { Button, Heading, Link, Paragraph, Select, Tag, Textarea, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenke } from '../api/eidLenker';
import type { RettskildeSammendrag, TjenesteDto, TjenesteRegelverksreferanseDto } from '../api/types';

const STATUSER = ['utkast', 'under_revisjon', 'validert', 'publisert', 'tilbaketrukket', 'arkivert'];

export default function TjenesteDetalj() {
  const { id } = useParams<{ id: string }>();
  const [tjeneste, setTjeneste] = useState<TjenesteDto | null>(null);
  const [referanser, setReferanser] = useState<TjenesteRegelverksreferanseDto[] | null>(null);
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [feil, setFeil] = useState<string | null>(null);

  const [tittel, setTittel] = useState('');
  const [beskrivelse, setBeskrivelse] = useState('');
  const [kompetentMyndighet, setKompetentMyndighet] = useState('');
  const [tjenestetype, setTjenestetype] = useState('');
  const [lagrer, setLagrer] = useState(false);
  const [lagreFeil, setLagreFeil] = useState<string | null>(null);
  const [statusEndres, setStatusEndres] = useState(false);

  function fyllSkjemaFra(t: TjenesteDto) {
    setTittel(t.tittel);
    setBeskrivelse(t.beskrivelse ?? '');
    setKompetentMyndighet(t.kompetentMyndighet ?? '');
    setTjenestetype(t.tjenestetype ?? '');
  }

  useEffect(() => {
    if (!id) return;
    api.hentTjeneste(id).then((t) => { setTjeneste(t); fyllSkjemaFra(t); })
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av tjeneste.'));
    api.hentTjenesteRegelverksreferanser(id).then(setReferanser).catch(() => setReferanser([]));
    api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([]));
  }, [id]);

  async function lagre(e: FormEvent) {
    e.preventDefault();
    if (!id || !tjeneste) return;
    setLagreFeil(null);
    setLagrer(true);
    try {
      const oppdatert = await api.oppdaterTjeneste(id, {
        tittel: tittel.trim(), beskrivelse: beskrivelse.trim() || null,
        kompetentMyndighet: kompetentMyndighet.trim() || null, output: tjeneste.output,
        tjenestetype: tjenestetype.trim() || null, malgruppe: tjeneste.malgruppe, kanaler: tjeneste.kanaler,
        kostnad: tjeneste.kostnad, behandlingstid: tjeneste.behandlingstid, kontaktpunkt: tjeneste.kontaktpunkt,
        konsekvensVedBrudd: tjeneste.konsekvensVedBrudd, sprak: tjeneste.sprak,
      });
      setTjeneste(oppdatert);
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
      const oppdatert = await api.settTjenesteStatus(id, { status: nyStatus });
      setTjeneste(oppdatert);
    } catch (err) {
      setLagreFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved statusendring.');
    } finally {
      setStatusEndres(false);
    }
  }

  if (feil) return <div className="feilmelding">{feil}</div>;
  if (!tjeneste) return <Paragraph>Laster …</Paragraph>;

  return (
    <>
      <Heading level={1} data-size="lg">
        {tjeneste.tittel}
      </Heading>
      <Tag data-color="info" style={{ marginBottom: '1.5rem' }}>{tjeneste.status}</Tag>

      {tjeneste.rotnodeId && (
        <Paragraph style={{ marginBottom: '1.5rem' }}>
          <Link asChild>
            <RouterLink to={`/vilkarstre/${tjeneste.rotnodeId}`}>Åpne vilkårstre →</RouterLink>
          </Link>
        </Paragraph>
      )}

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Egenskaper
        </Heading>
        <form onSubmit={lagre} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', maxWidth: '40rem' }}>
          <Textfield label="Tittel" value={tittel} onChange={(e) => setTittel(e.target.value)} required />
          <Textarea label="Beskrivelse" value={beskrivelse} onChange={(e) => setBeskrivelse(e.target.value)} rows={3} />
          <Textfield label="Kompetent myndighet" value={kompetentMyndighet} onChange={(e) => setKompetentMyndighet(e.target.value)} />
          <Textfield label="Tjenestetype" value={tjenestetype} onChange={(e) => setTjenestetype(e.target.value)} />
          {lagreFeil && <div className="feilmelding">{lagreFeil}</div>}
          <div>
            <Button type="submit" disabled={lagrer}>{lagrer ? 'Lagrer …' : 'Lagre'}</Button>
          </div>
        </form>
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Status
        </Heading>
        <Select value={tjeneste.status} disabled={statusEndres} onChange={(e) => endreStatus(e.target.value)} style={{ maxWidth: '16rem' }}>
          {STATUSER.map((s) => (
            <Select.Option key={s} value={s}>{s}</Select.Option>
          ))}
        </Select>
      </section>

      <section>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Regelverksreferanser
        </Heading>
        {referanser === null && <Paragraph>Laster …</Paragraph>}
        {referanser && referanser.length === 0 && <Paragraph>Ingen regelverksreferanser koblet ennå.</Paragraph>}
        {referanser && referanser.length > 0 && (
          <ul>
            {referanser.map((r) => {
              const href = rettskildeLenke(r.tilEid, rettskilder);
              return (
                <li key={r.id} style={{ fontFamily: 'monospace', fontSize: 'var(--ds-font-size-1)' }}>
                  {href ? <Link asChild><RouterLink to={href}>{r.tilEid}</RouterLink></Link> : r.tilEid}
                </li>
              );
            })}
          </ul>
        )}
      </section>
    </>
  );
}
