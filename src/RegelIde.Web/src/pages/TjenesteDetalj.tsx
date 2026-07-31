import { useEffect, useState, type FormEvent } from 'react';
import { Link as RouterLink, useParams } from 'react-router';
import { Button, Field, Heading, Label, Link, Paragraph, Select, Tag, Textarea, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenke } from '../api/eidLenker';
import type {
  HendelseDto, RegelnodeDto, RettskildeSammendrag, TjenesteavhengighetDto, TjenesteDto, TjenesteRegelverksreferanseDto,
} from '../api/types';

const STATUSER = ['utkast', 'under_revisjon', 'validert', 'publisert', 'tilbaketrukket', 'arkivert'];

/** 'for'/'avhengig_av'/'input_til' er de generelle relasjonene; de tre første har en presis betydning (docs/03-domenemodell.md §1.5). */
const TJENESTEAVHENGIGHET_REL = [
  { id: 'forutsetning_for', label: 'er forutsetning for' },
  { id: 'gir_mulighet_til', label: 'gir mulighet til' },
  { id: 'utlost_av', label: 'utløses av en hendelse' },
  { id: 'for', label: 'kommer før (generelt)' },
  { id: 'avhengig_av', label: 'er avhengig av (generelt)' },
  { id: 'input_til', label: 'er input til (generelt)' },
];

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

  const [hendelser, setHendelser] = useState<HendelseDto[] | null>(null);
  const [alleHendelser, setAlleHendelser] = useState<HendelseDto[]>([]);
  const [nyHendelseId, setNyHendelseId] = useState('');
  const [leggerTilHendelse, setLeggerTilHendelse] = useState(false);
  const [visNyHendelse, setVisNyHendelse] = useState(false);
  const [nyHendelseNavn, setNyHendelseNavn] = useState('');
  const [nyHendelseType, setNyHendelseType] = useState('virksomhetshendelse');
  const [hendelseFeil, setHendelseFeil] = useState<string | null>(null);

  const [avhengigheter, setAvhengigheter] = useState<TjenesteavhengighetDto[] | null>(null);
  const [alleTjenester, setAlleTjenester] = useState<TjenesteDto[]>([]);
  const [nyAvhengighetTilId, setNyAvhengighetTilId] = useState('');
  const [nyAvhengighetRel, setNyAvhengighetRel] = useState('forutsetning_for');
  const [nyAvhengighetHendelseId, setNyAvhengighetHendelseId] = useState('');
  const [nyAvhengighetBeskrivelse, setNyAvhengighetBeskrivelse] = useState('');
  const [leggerTilAvhengighet, setLeggerTilAvhengighet] = useState(false);
  const [avhengighetFeil, setAvhengighetFeil] = useState<string | null>(null);

  const [rotnode, setRotnode] = useState<RegelnodeDto | null>(null);
  const [regelnoder, setRegelnoder] = useState<RegelnodeDto[]>([]);
  const [visOpprettRotnode, setVisOpprettRotnode] = useState(false);
  const [nyRotnodeTittel, setNyRotnodeTittel] = useState('');
  const [visByttRotnode, setVisByttRotnode] = useState(false);
  const [valgtRotnodeId, setValgtRotnodeId] = useState('');
  const [rotnodeEndres, setRotnodeEndres] = useState(false);
  const [rotnodeFeil, setRotnodeFeil] = useState<string | null>(null);

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
    api.hentRegelnodeListe().then(setRegelnoder).catch(() => setRegelnoder([]));
    api.hentTjenesteHendelser(id).then(setHendelser).catch(() => setHendelser([]));
    api.hentHendelser().then(setAlleHendelser).catch(() => setAlleHendelser([]));
    api.hentTjenesteavhengigheter(id).then(setAvhengigheter).catch(() => setAvhengigheter([]));
    api.hentTjenester().then(setAlleTjenester).catch(() => setAlleTjenester([]));
  }, [id]);

  useEffect(() => {
    if (!tjeneste?.rotnodeId) { setRotnode(null); return; }
    api.hentRegelnode(tjeneste.rotnodeId).then(setRotnode).catch(() => setRotnode(null));
  }, [tjeneste?.rotnodeId]);

  async function opprettRotnode(e: FormEvent) {
    e.preventDefault();
    if (!id || !nyRotnodeTittel.trim()) return;
    setRotnodeFeil(null);
    setRotnodeEndres(true);
    try {
      const nyRegelnode = await api.opprettRegelnode({
        tittel: nyRotnodeTittel.trim(), beskrivelse: null, generiskMal: null, barnOperator: 'OG',
        utdataNavn: 'Vedtak', utdataType: 'vedtak', erRotnode: true, juridiskGrunnlag: null,
        innvilgelseTekst: null, avslagTekst: null,
      });
      const oppdatert = await api.settTjenesteRotnode(id, { regelnodeId: nyRegelnode.id });
      setTjeneste(oppdatert);
      setVisOpprettRotnode(false);
      setNyRotnodeTittel('');
    } catch (err) {
      setRotnodeFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av rotnode.');
    } finally {
      setRotnodeEndres(false);
    }
  }

  async function byttRotnode(e: FormEvent) {
    e.preventDefault();
    if (!id || !valgtRotnodeId) return;
    setRotnodeFeil(null);
    setRotnodeEndres(true);
    try {
      const oppdatert = await api.settTjenesteRotnode(id, { regelnodeId: valgtRotnodeId });
      setTjeneste(oppdatert);
      setVisByttRotnode(false);
      setValgtRotnodeId('');
    } catch (err) {
      setRotnodeFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved bytte av rotnode.');
    } finally {
      setRotnodeEndres(false);
    }
  }

  async function fjernRotnode() {
    if (!id) return;
    setRotnodeFeil(null);
    setRotnodeEndres(true);
    try {
      const oppdatert = await api.fjernTjenesteRotnode(id);
      setTjeneste(oppdatert);
    } catch (err) {
      setRotnodeFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved fjerning av rotnode.');
    } finally {
      setRotnodeEndres(false);
    }
  }

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

  async function kobleHendelse(e: FormEvent) {
    e.preventDefault();
    if (!id || !nyHendelseId) return;
    setHendelseFeil(null);
    setLeggerTilHendelse(true);
    try {
      const oppdatert = await api.kobleTjenesteHendelse(id, { hendelseId: nyHendelseId });
      setHendelser(oppdatert);
      setNyHendelseId('');
    } catch (err) {
      setHendelseFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved kobling av hendelse.');
    } finally {
      setLeggerTilHendelse(false);
    }
  }

  async function opprettOgKobleHendelse(e: FormEvent) {
    e.preventDefault();
    if (!id || !nyHendelseNavn.trim()) return;
    setHendelseFeil(null);
    setLeggerTilHendelse(true);
    try {
      const hendelse = await api.opprettHendelse({ navn: nyHendelseNavn.trim(), type: nyHendelseType, beskrivelse: null });
      setAlleHendelser((forrige) => [...forrige, hendelse]);
      const oppdatert = await api.kobleTjenesteHendelse(id, { hendelseId: hendelse.id });
      setHendelser(oppdatert);
      setNyHendelseNavn('');
      setVisNyHendelse(false);
    } catch (err) {
      setHendelseFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av hendelse.');
    } finally {
      setLeggerTilHendelse(false);
    }
  }

  async function fjernHendelse(hendelseId: string) {
    if (!id) return;
    await api.fjernTjenesteHendelse(id, hendelseId);
    setHendelser((forrige) => (forrige ?? []).filter((h) => h.id !== hendelseId));
  }

  async function leggTilAvhengighet(e: FormEvent) {
    e.preventDefault();
    if (!id || !nyAvhengighetTilId) return;
    setAvhengighetFeil(null);
    setLeggerTilAvhengighet(true);
    try {
      const oppdatert = await api.opprettTjenesteavhengighet(id, {
        tilTjenesteId: nyAvhengighetTilId,
        rel: nyAvhengighetRel,
        hendelseId: nyAvhengighetRel === 'utlost_av' ? nyAvhengighetHendelseId || null : null,
        beskrivelse: nyAvhengighetBeskrivelse.trim() || null,
      });
      setAvhengigheter(oppdatert);
      setNyAvhengighetTilId('');
      setNyAvhengighetHendelseId('');
      setNyAvhengighetBeskrivelse('');
    } catch (err) {
      setAvhengighetFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av avhengighet.');
    } finally {
      setLeggerTilAvhengighet(false);
    }
  }

  async function fjernAvhengighet(avhengighetId: string) {
    await api.slettTjenesteavhengighet(avhengighetId);
    setAvhengigheter((forrige) => (forrige ?? []).filter((a) => a.id !== avhengighetId));
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

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Vilkårstre
        </Heading>
        {tjeneste.rotnodeId ? (
          <>
            <Paragraph style={{ marginBottom: '0.75rem', display: 'flex', gap: '1rem', alignItems: 'center', flexWrap: 'wrap' }}>
              <span>Rotnode: <strong>{rotnode?.tittel ?? '…'}</strong></span>
              <Link asChild>
                <RouterLink to={`/vilkarstre/${tjeneste.rotnodeId}`}>Åpne vilkårstre →</RouterLink>
              </Link>
              <Link asChild>
                <RouterLink to={`/tjenester/${tjeneste.id}/veiledning`}>Åpne veiledning →</RouterLink>
              </Link>
            </Paragraph>
            <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.75rem' }}>
              <Button data-size="sm" variant="secondary" onClick={() => setVisByttRotnode((v) => !v)}>
                {visByttRotnode ? 'Avbryt' : 'Bytt rotnode'}
              </Button>
              <Button data-size="sm" variant="tertiary" data-color="danger" disabled={rotnodeEndres} onClick={fjernRotnode}>
                Fjern rotnode
              </Button>
            </div>
            {visByttRotnode && (
              <form onSubmit={byttRotnode} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
                <Field>
                  <Label>Ny rotnode (regelnode)</Label>
                  <Select data-size="sm" value={valgtRotnodeId} onChange={(e) => setValgtRotnodeId(e.target.value)}>
                    <Select.Option value="">Velg …</Select.Option>
                    {regelnoder.map((r) => <Select.Option key={r.id} value={r.id}>{r.tittel}</Select.Option>)}
                  </Select>
                </Field>
                <Button data-size="sm" type="submit" disabled={rotnodeEndres || !valgtRotnodeId}>
                  {rotnodeEndres ? 'Setter …' : 'Sett som rotnode'}
                </Button>
              </form>
            )}
          </>
        ) : visOpprettRotnode ? (
          <form onSubmit={opprettRotnode} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
            <Textfield data-size="sm" label="Rotnodens tittel" value={nyRotnodeTittel} onChange={(e) => setNyRotnodeTittel(e.target.value)} required />
            <Button data-size="sm" type="submit" disabled={rotnodeEndres || !nyRotnodeTittel.trim()}>
              {rotnodeEndres ? 'Oppretter …' : 'Opprett'}
            </Button>
            <Button data-size="sm" variant="tertiary" onClick={() => setVisOpprettRotnode(false)}>Avbryt</Button>
          </form>
        ) : (
          <Button data-size="sm" variant="secondary" onClick={() => { setVisOpprettRotnode(true); setNyRotnodeTittel(`Vedtak om ${tjeneste.tittel.toLowerCase()}`); }}>
            Opprett rotnode
          </Button>
        )}
        {rotnodeFeil && <div className="feilmelding" style={{ marginTop: '0.5rem' }}>{rotnodeFeil}</div>}
      </section>

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

      <section style={{ marginTop: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Hendelser
        </Heading>
        <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)', marginBottom: '0.75rem' }}>
          Ren, symmetrisk klassifisering (docs/03-domenemodell.md §1.5) — ingen retning. To tjenester som
          deler samme hendelse blir relaterte uten at én forårsaker den andre.
        </Paragraph>
        {hendelser === null && <Paragraph>Laster …</Paragraph>}
        {hendelser && hendelser.length === 0 && <Paragraph>Ingen hendelser koblet ennå.</Paragraph>}
        {hendelser && hendelser.length > 0 && (
          <ul>
            {hendelser.map((h) => (
              <li key={h.id} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                <span>{h.navn}</span>
                <Tag data-color="neutral" data-size="sm">{h.type}</Tag>
                <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjernHendelse(h.id)}>Fjern</Button>
              </li>
            ))}
          </ul>
        )}

        <form onSubmit={kobleHendelse} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginTop: '0.75rem' }}>
          <Field>
            <Label>Eksisterende hendelse</Label>
            <Select data-size="sm" value={nyHendelseId} onChange={(e) => setNyHendelseId(e.target.value)}>
              <Select.Option value="">Velg …</Select.Option>
              {alleHendelser
                .filter((h) => !(hendelser ?? []).some((koblet) => koblet.id === h.id))
                .map((h) => <Select.Option key={h.id} value={h.id}>{h.navn} ({h.type})</Select.Option>)}
            </Select>
          </Field>
          <Button data-size="sm" type="submit" disabled={leggerTilHendelse || !nyHendelseId}>
            {leggerTilHendelse ? 'Kobler …' : 'Koble hendelse'}
          </Button>
          <Button data-size="sm" variant="tertiary" onClick={() => setVisNyHendelse((v) => !v)}>
            {visNyHendelse ? 'Avbryt' : '+ Ny hendelse'}
          </Button>
        </form>
        {visNyHendelse && (
          <form onSubmit={opprettOgKobleHendelse} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginTop: '0.5rem' }}>
            <Textfield data-size="sm" label="Navn på ny hendelse" value={nyHendelseNavn} onChange={(e) => setNyHendelseNavn(e.target.value)} required />
            <Field>
              <Label>Type</Label>
              <Select data-size="sm" value={nyHendelseType} onChange={(e) => setNyHendelseType(e.target.value)}>
                <Select.Option value="generell">Generell (cv:Event)</Select.Option>
                <Select.Option value="livshendelse">Livshendelse</Select.Option>
                <Select.Option value="virksomhetshendelse">Virksomhetshendelse</Select.Option>
              </Select>
            </Field>
            <Button data-size="sm" type="submit" disabled={leggerTilHendelse || !nyHendelseNavn.trim()}>
              {leggerTilHendelse ? 'Oppretter …' : 'Opprett og koble'}
            </Button>
          </form>
        )}
        {hendelseFeil && <div className="feilmelding" style={{ marginTop: '0.5rem' }}>{hendelseFeil}</div>}
      </section>

      <section style={{ marginTop: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Tjenesteavhengigheter
        </Heading>
        <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)', marginBottom: '0.75rem' }}>
          Rettede, årsaksforklarte koblinger mellom to tjenester (docs/03-domenemodell.md §1.5) — ett
          rettet kant per relasjon, vist med riktig tekst uansett hvilken side du ser fra.
        </Paragraph>
        {avhengigheter === null && <Paragraph>Laster …</Paragraph>}
        {avhengigheter && avhengigheter.length === 0 && <Paragraph>Ingen tjenesteavhengigheter registrert ennå.</Paragraph>}
        {avhengigheter && avhengigheter.length > 0 && (
          <ul>
            {avhengigheter.map((a) => (
              <li key={a.id} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                <Link asChild>
                  <RouterLink to={`/tjenester/${a.motpartTjenesteId}`}>{a.visningstekst}</RouterLink>
                </Link>
                {a.beskrivelse && <Tag data-color="neutral" data-size="sm">{a.beskrivelse}</Tag>}
                {/* Sletting virker uansett hvilken side raden vises fra — samme rad-id begge steder. */}
                <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjernAvhengighet(a.id)}>Fjern</Button>
              </li>
            ))}
          </ul>
        )}

        <form onSubmit={leggTilAvhengighet} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginTop: '0.75rem' }}>
          <Field>
            <Label>Relasjon (denne tjenesten …)</Label>
            <Select data-size="sm" value={nyAvhengighetRel} onChange={(e) => setNyAvhengighetRel(e.target.value)}>
              {TJENESTEAVHENGIGHET_REL.map((r) => <Select.Option key={r.id} value={r.id}>{r.label}</Select.Option>)}
            </Select>
          </Field>
          <Field>
            <Label>Til tjeneste</Label>
            <Select data-size="sm" value={nyAvhengighetTilId} onChange={(e) => setNyAvhengighetTilId(e.target.value)}>
              <Select.Option value="">Velg …</Select.Option>
              {alleTjenester.filter((t) => t.id !== id).map((t) => <Select.Option key={t.id} value={t.id}>{t.tittel}</Select.Option>)}
            </Select>
          </Field>
          {nyAvhengighetRel === 'utlost_av' && (
            <Field>
              <Label>Hendelse</Label>
              <Select data-size="sm" value={nyAvhengighetHendelseId} onChange={(e) => setNyAvhengighetHendelseId(e.target.value)}>
                <Select.Option value="">Velg …</Select.Option>
                {alleHendelser.map((h) => <Select.Option key={h.id} value={h.id}>{h.navn}</Select.Option>)}
              </Select>
            </Field>
          )}
          <Textfield data-size="sm" label="Nyanse/unntak (valgfritt)" value={nyAvhengighetBeskrivelse}
            onChange={(e) => setNyAvhengighetBeskrivelse(e.target.value)} style={{ minWidth: '16rem' }} />
          <Button data-size="sm" type="submit" disabled={leggerTilAvhengighet || !nyAvhengighetTilId}>
            {leggerTilAvhengighet ? 'Oppretter …' : 'Opprett avhengighet'}
          </Button>
        </form>
        {avhengighetFeil && <div className="feilmelding" style={{ marginTop: '0.5rem' }}>{avhengighetFeil}</div>}
      </section>
    </>
  );
}
