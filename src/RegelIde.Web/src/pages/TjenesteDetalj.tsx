import { useEffect, useState, type FormEvent } from 'react';
import { Link as RouterLink, useParams } from 'react-router';
import { Button, Field, Heading, Label, Link, Paragraph, Select, Tag, Textarea, Textfield } from '@digdir/designsystemet-react';
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
  const [malgruppe, setMalgruppe] = useState('');
  const [kanaler, setKanaler] = useState('');
  const [kostnad, setKostnad] = useState('');
  const [behandlingstid, setBehandlingstid] = useState('');
  const [kontaktpunkt, setKontaktpunkt] = useState('');
  const [konsekvensVedBrudd, setKonsekvensVedBrudd] = useState('');
  const [sprak, setSprak] = useState('');
  const [lagrer, setLagrer] = useState(false);
  const [lagreFeil, setLagreFeil] = useState<string | null>(null);
  const [statusEndres, setStatusEndres] = useState(false);

  const [nyReferanseRettskildeId, setNyReferanseRettskildeId] = useState('');
  const [nyReferanseEid, setNyReferanseEid] = useState('');
  const [leggerTilReferanse, setLeggerTilReferanse] = useState(false);
  const [referanseFeil, setReferanseFeil] = useState<string | null>(null);

  /** Kanaler/språk redigeres som kommaseparert tekst i denne runden — ingen multi-select-UI bygget ennå. */
  function tilListe(kommaseparert: string): string[] {
    return kommaseparert.split(',').map((s) => s.trim()).filter(Boolean);
  }

  function fyllSkjemaFra(t: TjenesteDto) {
    setTittel(t.tittel);
    setBeskrivelse(t.beskrivelse ?? '');
    setKompetentMyndighet(t.kompetentMyndighet ?? '');
    setTjenestetype(t.tjenestetype ?? '');
    setMalgruppe(t.malgruppe ?? '');
    setKanaler(t.kanaler.join(', '));
    setKostnad(t.kostnad ?? '');
    setBehandlingstid(t.behandlingstid ?? '');
    setKontaktpunkt(t.kontaktpunkt ?? '');
    setKonsekvensVedBrudd(t.konsekvensVedBrudd ?? '');
    setSprak(t.sprak.join(', '));
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
        tjenestetype: tjenestetype.trim() || null, malgruppe: malgruppe.trim() || null, kanaler: tilListe(kanaler),
        kostnad: kostnad.trim() || null, behandlingstid: behandlingstid.trim() || null, kontaktpunkt: kontaktpunkt.trim() || null,
        konsekvensVedBrudd: konsekvensVedBrudd.trim() || null, sprak: tilListe(sprak),
      });
      setTjeneste(oppdatert);
    } catch (err) {
      setLagreFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring.');
    } finally {
      setLagrer(false);
    }
  }

  async function leggTilReferanse(e: FormEvent) {
    e.preventDefault();
    if (!id || !nyReferanseRettskildeId || !nyReferanseEid.trim()) return;
    setReferanseFeil(null);
    setLeggerTilReferanse(true);
    try {
      const ny = await api.kobleTjenesteRegelverksreferanse(id, {
        tilRettskildeId: nyReferanseRettskildeId, tilEid: nyReferanseEid.trim(),
      });
      setReferanser((forrige) => [...(forrige ?? []), ny]);
      setNyReferanseEid('');
    } catch (err) {
      setReferanseFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved kobling av referanse.');
    } finally {
      setLeggerTilReferanse(false);
    }
  }

  async function fjernReferanse(referanseId: string) {
    await api.fjernTjenesteRegelverksreferanse(referanseId);
    setReferanser((forrige) => (forrige ?? []).filter((r) => r.id !== referanseId));
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
        <Paragraph style={{ marginBottom: '1.5rem', display: 'flex', gap: '1rem' }}>
          <Link asChild>
            <RouterLink to={`/vilkarstre/${tjeneste.rotnodeId}`}>Åpne vilkårstre →</RouterLink>
          </Link>
          <Link asChild>
            <RouterLink to={`/tjenester/${tjeneste.id}/veiledning`}>Åpne veiledning →</RouterLink>
          </Link>
        </Paragraph>
      )}

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Egenskaper
        </Heading>
        <form onSubmit={lagre} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', maxWidth: '40rem' }}>
          <Textfield label="Tittel" value={tittel} onChange={(e) => setTittel(e.target.value)} required />
          <Field>
            <Label>Beskrivelse</Label>
            <Textarea value={beskrivelse} onChange={(e) => setBeskrivelse(e.target.value)} rows={3} />
          </Field>
          <Textfield label="Kompetent myndighet" value={kompetentMyndighet} onChange={(e) => setKompetentMyndighet(e.target.value)} />
          <Textfield label="Tjenestetype" value={tjenestetype} onChange={(e) => setTjenestetype(e.target.value)} />
          <Textfield label="Målgruppe" value={malgruppe} onChange={(e) => setMalgruppe(e.target.value)} />
          <Textfield label="Kanaler (kommaseparert)" value={kanaler} onChange={(e) => setKanaler(e.target.value)} placeholder="f.eks. Nett, Skranke" />
          <Textfield label="Kostnad" value={kostnad} onChange={(e) => setKostnad(e.target.value)} />
          <Textfield label="Behandlingstid" value={behandlingstid} onChange={(e) => setBehandlingstid(e.target.value)} />
          <Textfield label="Kontaktpunkt" value={kontaktpunkt} onChange={(e) => setKontaktpunkt(e.target.value)} />
          <Textfield label="Konsekvens ved brudd" value={konsekvensVedBrudd} onChange={(e) => setKonsekvensVedBrudd(e.target.value)} />
          <Textfield label="Språk (kommaseparert)" value={sprak} onChange={(e) => setSprak(e.target.value)} placeholder="f.eks. nb, en" />
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
                <li key={r.id} style={{ fontFamily: 'monospace', fontSize: 'var(--ds-font-size-1)', display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                  {href ? <Link asChild><RouterLink to={href}>{r.tilEid}</RouterLink></Link> : r.tilEid}
                  <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjernReferanse(r.id)}>Fjern</Button>
                </li>
              );
            })}
          </ul>
        )}

        <form onSubmit={leggTilReferanse} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginTop: '0.75rem' }}>
          <Field>
            <Label>Rettskilde</Label>
            <Select data-size="sm" value={nyReferanseRettskildeId} onChange={(e) => setNyReferanseRettskildeId(e.target.value)}>
              <Select.Option value="">Velg …</Select.Option>
              {rettskilder.map((r) => <Select.Option key={r.id} value={r.id}>{r.tittel}</Select.Option>)}
            </Select>
          </Field>
          <Textfield data-size="sm" label="eId (f.eks. https://lovdata.no/eli/lov/.../§4-1)" value={nyReferanseEid}
            onChange={(e) => setNyReferanseEid(e.target.value)} style={{ minWidth: '22rem', fontFamily: 'monospace' }} />
          <Button data-size="sm" type="submit" disabled={leggerTilReferanse || !nyReferanseRettskildeId || !nyReferanseEid.trim()}>
            {leggerTilReferanse ? 'Kobler …' : 'Koble referanse'}
          </Button>
          {referanseFeil && <div className="feilmelding" style={{ width: '100%' }}>{referanseFeil}</div>}
        </form>
      </section>
    </>
  );
}
