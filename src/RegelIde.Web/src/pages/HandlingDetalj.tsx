/**
 * HandlingDetalj (2026-08-20)
 * ------------------------------------------------------------------
 * Egen side for én Handling — konkrete, tidsavgrensede interaksjoner knyttet til en Rettighet
 * (Tjeneste), se HandlingEntitet i RegelIde.Data for begrunnelsen. Ikke krymmet inn i
 * TjenesteDetalj.tsx (allerede en stor side) — samme "egen detaljside per entitet"-mønster som
 * Begrep/Kodeliste/Vilkårstre.
 *
 * De rike underfeltene (kanaler/vedlegg/veiledningstekst/arsaker) er JSONB-arrayer uten egen id —
 * samme liste+miniform-mønster som Regelverksreferanser/Hendelser på TjenesteDetalj, men "Fjern"
 * erstatter HELE arrayet via PUT (ikke en egen DELETE-rad, siden verdiene ikke har egen identitet).
 * kostnad/behandlingstid/resultat redigeres som enkle ett-objekt-former, samme stil som
 * "Egenskaper"-seksjonen på TjenesteDetalj.
 */
import { useEffect, useState, type FormEvent } from 'react';
import { Link as RouterLink, useParams } from 'react-router';
import { Button, Field, Heading, Label, Link, Paragraph, Select, Tag, Textarea, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import {
  GYLDIGE_HANDLINGSTYPER, GYLDIGE_UTFORT_AV,
  type HandlingArsakInput, type HandlingDto, type HandlingHjemmelInput, type HandlingKanalInput,
  type HandlingRequest, type HandlingVedleggInput, type HandlingVeiledningstekstInput, type RegelnodeDto, type TjenesteDto,
} from '../api/types';

const STATUSER = ['utkast', 'under_revisjon', 'validert', 'publisert', 'tilbaketrukket', 'arkivert'];

/** null hvis input er tom — skiller "ingen hjemmel oppgitt" fra en tom streng i JSON-en. */
function hjemmelEllerNull(lov: string, henvisning: string): HandlingHjemmelInput | null {
  return lov.trim() ? { lov: lov.trim(), henvisning: henvisning.trim() || null } : null;
}

function VisHjemmel({ hjemmel }: { hjemmel: HandlingHjemmelInput | null }) {
  if (!hjemmel) return null;
  return <Tag data-color="info" data-size="sm">{hjemmel.lov}{hjemmel.henvisning ? ` ${hjemmel.henvisning}` : ''}</Tag>;
}

export default function HandlingDetalj() {
  const { tjenesteId, handlingId } = useParams<{ tjenesteId: string; handlingId: string }>();
  const [handling, setHandling] = useState<HandlingDto | null>(null);
  const [tjeneste, setTjeneste] = useState<TjenesteDto | null>(null);
  const [regelnoder, setRegelnoder] = useState<RegelnodeDto[]>([]);
  const [rotnode, setRotnode] = useState<RegelnodeDto | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [lagreFeil, setLagreFeil] = useState<string | null>(null);

  const [navn, setNavn] = useState('');
  const [handlingstype, setHandlingstype] = useState('');
  const [bruksomraade, setBruksomraade] = useState('');
  const [utfortAv, setUtfortAv] = useState('');
  const [merknad, setMerknad] = useState('');
  const [lagrer, setLagrer] = useState(false);
  const [statusEndres, setStatusEndres] = useState(false);

  useEffect(() => {
    if (!handlingId) return;
    api.hentHandling(handlingId).then(fyllSkjemaFra)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av handling.'));
    api.hentRegelnodeListe().then(setRegelnoder).catch(() => setRegelnoder([]));
  }, [handlingId]);

  useEffect(() => {
    if (!tjenesteId) return;
    api.hentTjeneste(tjenesteId).then(setTjeneste).catch(() => setTjeneste(null));
  }, [tjenesteId]);

  useEffect(() => {
    if (!handling?.rotnodeId) { setRotnode(null); return; }
    api.hentRegelnode(handling.rotnodeId).then(setRotnode).catch(() => setRotnode(null));
  }, [handling?.rotnodeId]);

  function fyllSkjemaFra(h: HandlingDto) {
    setHandling(h);
    setNavn(h.navn);
    setHandlingstype(h.handlingstype);
    setBruksomraade(h.bruksomraade ?? '');
    setUtfortAv(h.utfortAv ?? '');
    setMerknad(h.merknad ?? '');
  }

  /** Bygger et FULLT HandlingRequest fra gjeldende tilstand + overstyringer, og lagrer. Samme
   * "skriver ALLE felt"-mønster som TjenesteregisterTjeneste.OppdaterAsync — hvert delfelt som
   * IKKE er en del av denne endringen må derfor sendes tilbake uendret. */
  async function oppdater(overstyring: Partial<HandlingRequest>) {
    if (!handling) return null;
    const request: HandlingRequest = {
      navn: handling.navn, handlingstype: handling.handlingstype, bruksomraade: handling.bruksomraade,
      utfortAv: handling.utfortAv, kanaler: handling.kanaler, behandlingstid: handling.behandlingstid,
      kostnad: handling.kostnad, vedlegg: handling.vedlegg, veiledningstekst: handling.veiledningstekst,
      arsaker: handling.arsaker, resultat: handling.resultat, merknad: handling.merknad,
      ...overstyring,
    };
    const oppdatert = await api.oppdaterHandling(handling.id, request);
    setHandling(oppdatert);
    return oppdatert;
  }

  async function lagreEgenskaper(e: FormEvent) {
    e.preventDefault();
    if (!handling) return;
    setLagreFeil(null);
    setLagrer(true);
    try {
      await oppdater({
        navn: navn.trim(), handlingstype, bruksomraade: bruksomraade.trim() || null,
        utfortAv: utfortAv || null, merknad: merknad.trim() || null,
      });
    } catch (err) {
      setLagreFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring.');
    } finally {
      setLagrer(false);
    }
  }

  async function endreStatus(nyStatus: string) {
    if (!handling) return;
    setStatusEndres(true);
    setLagreFeil(null);
    try {
      const oppdatert = await api.settHandlingStatus(handling.id, { status: nyStatus });
      setHandling(oppdatert);
    } catch (err) {
      setLagreFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved statusendring.');
    } finally {
      setStatusEndres(false);
    }
  }

  // ---------- Kanaler ----------
  const [nyKanalKanal, setNyKanalKanal] = useState('');
  const [nyKanalAdresse, setNyKanalAdresse] = useState('');
  const [kanalFeil, setKanalFeil] = useState<string | null>(null);

  async function leggTilKanal(e: FormEvent) {
    e.preventDefault();
    if (!handling || !nyKanalKanal.trim()) return;
    setKanalFeil(null);
    try {
      const ny: HandlingKanalInput = { kanal: nyKanalKanal.trim(), adresse: nyKanalAdresse.trim() || null };
      await oppdater({ kanaler: [...handling.kanaler, ny] });
      setNyKanalKanal('');
      setNyKanalAdresse('');
    } catch (err) {
      setKanalFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved kobling av kanal.');
    }
  }

  async function fjernKanal(index: number) {
    if (!handling) return;
    await oppdater({ kanaler: handling.kanaler.filter((_, i) => i !== index) });
  }

  // ---------- Vedlegg ----------
  const [nyVedleggNavn, setNyVedleggNavn] = useState('');
  const [nyVedleggKategori, setNyVedleggKategori] = useState('');
  const [nyVedleggLov, setNyVedleggLov] = useState('');
  const [nyVedleggHenvisning, setNyVedleggHenvisning] = useState('');
  const [vedleggFeil, setVedleggFeil] = useState<string | null>(null);

  async function leggTilVedlegg(e: FormEvent) {
    e.preventDefault();
    if (!handling || !nyVedleggNavn.trim()) return;
    setVedleggFeil(null);
    try {
      const ny: HandlingVedleggInput = {
        navn: nyVedleggNavn.trim(), kategori: nyVedleggKategori.trim() || null,
        hjemmel: hjemmelEllerNull(nyVedleggLov, nyVedleggHenvisning),
      };
      await oppdater({ vedlegg: [...handling.vedlegg, ny] });
      setNyVedleggNavn(''); setNyVedleggKategori(''); setNyVedleggLov(''); setNyVedleggHenvisning('');
    } catch (err) {
      setVedleggFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved tillegg av vedlegg.');
    }
  }

  async function fjernVedlegg(index: number) {
    if (!handling) return;
    await oppdater({ vedlegg: handling.vedlegg.filter((_, i) => i !== index) });
  }

  // ---------- Veiledningstekst ----------
  const [nyVeiledningOverskrift, setNyVeiledningOverskrift] = useState('');
  const [nyVeiledningInnhold, setNyVeiledningInnhold] = useState('');
  const [nyVeiledningLov, setNyVeiledningLov] = useState('');
  const [nyVeiledningHenvisning, setNyVeiledningHenvisning] = useState('');
  const [veiledningFeil, setVeiledningFeil] = useState<string | null>(null);

  async function leggTilVeiledning(e: FormEvent) {
    e.preventDefault();
    if (!handling || !nyVeiledningOverskrift.trim()) return;
    setVeiledningFeil(null);
    try {
      const ny: HandlingVeiledningstekstInput = {
        overskrift: nyVeiledningOverskrift.trim(), innhold: nyVeiledningInnhold.trim() || null,
        hjemmel: hjemmelEllerNull(nyVeiledningLov, nyVeiledningHenvisning),
      };
      await oppdater({ veiledningstekst: [...handling.veiledningstekst, ny] });
      setNyVeiledningOverskrift(''); setNyVeiledningInnhold(''); setNyVeiledningLov(''); setNyVeiledningHenvisning('');
    } catch (err) {
      setVeiledningFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved tillegg av veiledningstekst.');
    }
  }

  async function fjernVeiledning(index: number) {
    if (!handling) return;
    await oppdater({ veiledningstekst: handling.veiledningstekst.filter((_, i) => i !== index) });
  }

  // ---------- Årsaker (kun for handlinger av typen "bortfall/tilbaketrekking") ----------
  const [nyArsakArsak, setNyArsakArsak] = useState('');
  const [nyArsakLov, setNyArsakLov] = useState('');
  const [nyArsakHenvisning, setNyArsakHenvisning] = useState('');
  const [arsakFeil, setArsakFeil] = useState<string | null>(null);

  async function leggTilArsak(e: FormEvent) {
    e.preventDefault();
    if (!handling || !nyArsakArsak.trim() || !nyArsakLov.trim()) return;
    setArsakFeil(null);
    try {
      const ny: HandlingArsakInput = { arsak: nyArsakArsak.trim(), hjemmel: { lov: nyArsakLov.trim(), henvisning: nyArsakHenvisning.trim() || null } };
      await oppdater({ arsaker: [...handling.arsaker, ny] });
      setNyArsakArsak(''); setNyArsakLov(''); setNyArsakHenvisning('');
    } catch (err) {
      setArsakFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved tillegg av årsak.');
    }
  }

  async function fjernArsak(index: number) {
    if (!handling) return;
    await oppdater({ arsaker: handling.arsaker.filter((_, i) => i !== index) });
  }

  // ---------- Kostnad / Behandlingstid / Resultat — ett-objekt-former ----------
  const [belop, setBelop] = useState('');
  const [belopFeil, setBelopFeil] = useState<string | null>(null);
  const [belopLagrer, setBelopLagrer] = useState(false);

  const [frist, setFrist] = useState('');
  const [behandlingstidLov, setBehandlingstidLov] = useState('');
  const [behandlingstidHenvisning, setBehandlingstidHenvisning] = useState('');
  const [behandlingstidFeil, setBehandlingstidFeil] = useState<string | null>(null);
  const [behandlingstidLagrer, setBehandlingstidLagrer] = useState(false);

  const [hva, setHva] = useState('');
  const [nyBevisKanal, setNyBevisKanal] = useState('');
  const [resultatFeil, setResultatFeil] = useState<string | null>(null);

  // Kun re-synk fra serverens verdi når det er en ANNEN handling (id-endring), ikke ved hver
  // lokale oppdater()-runde (som selv setter handling fra svaret og ellers ville trigget denne på
  // nytt og overskrevet input brukeren akkurat skrev i et NABO-felt i samme seksjon).
  useEffect(() => {
    if (!handling) return;
    setBelop(handling.kostnad.belop ?? '');
    setFrist(handling.behandlingstid.frist ?? '');
    setBehandlingstidLov(handling.behandlingstid.hjemmel?.lov ?? '');
    setBehandlingstidHenvisning(handling.behandlingstid.hjemmel?.henvisning ?? '');
    setHva(handling.resultat.hva ?? '');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [handling?.id]);

  async function lagreKostnad(e: FormEvent) {
    e.preventDefault();
    if (!handling) return;
    setBelopFeil(null);
    setBelopLagrer(true);
    try {
      await oppdater({ kostnad: { belop: belop.trim() || null, hjemmel: handling.kostnad.hjemmel } });
    } catch (err) {
      setBelopFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring av kostnad.');
    } finally {
      setBelopLagrer(false);
    }
  }

  async function fjernKostnadHjemmel(index: number) {
    if (!handling) return;
    await oppdater({ kostnad: { belop: handling.kostnad.belop, hjemmel: handling.kostnad.hjemmel.filter((_, i) => i !== index) } });
  }

  async function leggTilKostnadHjemmel(e: FormEvent) {
    e.preventDefault();
    if (!handling || !nyKostnadHjemmelLov.trim()) return;
    await oppdater({
      kostnad: {
        belop: handling.kostnad.belop,
        hjemmel: [...handling.kostnad.hjemmel, { lov: nyKostnadHjemmelLov.trim(), henvisning: nyKostnadHjemmelHenvisning.trim() || null }],
      },
    });
    setNyKostnadHjemmelLov('');
    setNyKostnadHjemmelHenvisning('');
  }

  const [nyKostnadHjemmelLov, setNyKostnadHjemmelLov] = useState('');
  const [nyKostnadHjemmelHenvisning, setNyKostnadHjemmelHenvisning] = useState('');

  async function lagreBehandlingstid(e: FormEvent) {
    e.preventDefault();
    if (!handling) return;
    setBehandlingstidFeil(null);
    setBehandlingstidLagrer(true);
    try {
      await oppdater({ behandlingstid: { frist: frist.trim() || null, hjemmel: hjemmelEllerNull(behandlingstidLov, behandlingstidHenvisning) } });
    } catch (err) {
      setBehandlingstidFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring av behandlingstid.');
    } finally {
      setBehandlingstidLagrer(false);
    }
  }

  async function lagreResultatHva(e: FormEvent) {
    e.preventDefault();
    if (!handling) return;
    setResultatFeil(null);
    try {
      await oppdater({ resultat: { hva: hva.trim() || null, bevisKanaler: handling.resultat.bevisKanaler } });
    } catch (err) {
      setResultatFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring av resultat.');
    }
  }

  async function leggTilBevisKanal(e: FormEvent) {
    e.preventDefault();
    if (!handling || !nyBevisKanal.trim()) return;
    await oppdater({ resultat: { hva: handling.resultat.hva, bevisKanaler: [...handling.resultat.bevisKanaler, { kanal: nyBevisKanal.trim() }] } });
    setNyBevisKanal('');
  }

  async function fjernBevisKanal(index: number) {
    if (!handling) return;
    await oppdater({ resultat: { hva: handling.resultat.hva, bevisKanaler: handling.resultat.bevisKanaler.filter((_, i) => i !== index) } });
  }

  // ---------- Rotnode-override ----------
  const [valgtRotnodeId, setValgtRotnodeId] = useState('');
  const [rotnodeEndres, setRotnodeEndres] = useState(false);
  const [rotnodeFeil, setRotnodeFeil] = useState<string | null>(null);

  async function byttRotnode(e: FormEvent) {
    e.preventDefault();
    if (!handling || !valgtRotnodeId) return;
    setRotnodeFeil(null);
    setRotnodeEndres(true);
    try {
      const oppdatert = await api.settHandlingRotnode(handling.id, { regelnodeId: valgtRotnodeId });
      setHandling(oppdatert);
      setValgtRotnodeId('');
    } catch (err) {
      setRotnodeFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved bytte av rotnode.');
    } finally {
      setRotnodeEndres(false);
    }
  }

  if (feil) return <div className="feilmelding">{feil}</div>;
  if (!handling) return <Paragraph>Laster …</Paragraph>;

  return (
    <>
      <Paragraph style={{ marginBottom: '0.5rem' }}>
        <Link asChild><RouterLink to={`/tjenester/${tjenesteId}`}>← {tjeneste?.tittel ?? 'Tilbake til rettigheten'}</RouterLink></Link>
      </Paragraph>
      <Heading level={1} data-size="lg">{handling.navn}</Heading>
      <Tag data-color="info" style={{ marginBottom: '1.5rem' }}>{handling.status}</Tag>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>Egenskaper</Heading>
        <form onSubmit={lagreEgenskaper} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', maxWidth: '40rem' }}>
          <Textfield label="Navn" value={navn} onChange={(e) => setNavn(e.target.value)} required />
          <Field>
            <Label>Handlingstype</Label>
            <Select value={handlingstype} onChange={(e) => setHandlingstype(e.target.value)}>
              {GYLDIGE_HANDLINGSTYPER.map((t) => <Select.Option key={t} value={t}>{t}</Select.Option>)}
            </Select>
          </Field>
          <Textfield label="Bruksområde" value={bruksomraade} onChange={(e) => setBruksomraade(e.target.value)} />
          <Field>
            <Label>Utført av</Label>
            <Select value={utfortAv} onChange={(e) => setUtfortAv(e.target.value)}>
              <Select.Option value="">Ikke satt</Select.Option>
              {GYLDIGE_UTFORT_AV.map((u) => <Select.Option key={u} value={u}>{u}</Select.Option>)}
            </Select>
          </Field>
          <Field>
            <Label>Merknad</Label>
            <Textarea value={merknad} onChange={(e) => setMerknad(e.target.value)} rows={2} />
          </Field>
          {lagreFeil && <div className="feilmelding">{lagreFeil}</div>}
          <div><Button type="submit" disabled={lagrer}>{lagrer ? 'Lagrer …' : 'Lagre'}</Button></div>
        </form>
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>Status</Heading>
        <Select value={handling.status} disabled={statusEndres} onChange={(e) => endreStatus(e.target.value)} style={{ maxWidth: '16rem' }}>
          {STATUSER.map((s) => <Select.Option key={s} value={s}>{s}</Select.Option>)}
        </Select>
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>Rotnode (overstyring)</Heading>
        <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)', marginBottom: '0.75rem' }}>
          Kobler denne ENE handlingens saksbehandling til en egen rotnode i vilkårstreet — mangler den,
          brukes rettighetens egen rotnode ({tjeneste?.rotnodeId ? 'satt' : 'ikke satt'}).
        </Paragraph>
        {handling.rotnodeId ? (
          <Paragraph style={{ marginBottom: '0.75rem' }}>
            Rotnode: <strong>{rotnode?.tittel ?? '…'}</strong>{' '}
            <Link asChild><RouterLink to={`/vilkarstre/${handling.rotnodeId}`}>Åpne vilkårstre →</RouterLink></Link>
          </Paragraph>
        ) : (
          <Paragraph style={{ marginBottom: '0.75rem' }}>Ingen egen rotnode satt — bruker rettighetens.</Paragraph>
        )}
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
        {rotnodeFeil && <div className="feilmelding" style={{ marginTop: '0.5rem' }}>{rotnodeFeil}</div>}
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>Kanaler</Heading>
        {handling.kanaler.length === 0 && <Paragraph>Ingen kanaler registrert ennå.</Paragraph>}
        {handling.kanaler.length > 0 && (
          <ul>
            {handling.kanaler.map((k, i) => (
              <li key={i} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                <span>{k.kanal}</span>
                {k.adresse && <Tag data-color="neutral" data-size="sm">{k.adresse}</Tag>}
                <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjernKanal(i)}>Fjern</Button>
              </li>
            ))}
          </ul>
        )}
        <form onSubmit={leggTilKanal} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginTop: '0.75rem' }}>
          <Textfield data-size="sm" label="Kanal" value={nyKanalKanal} onChange={(e) => setNyKanalKanal(e.target.value)}
            placeholder="f.eks. elektronisk, skranke, post" required />
          <Textfield data-size="sm" label="Adresse (valgfritt)" value={nyKanalAdresse} onChange={(e) => setNyKanalAdresse(e.target.value)} />
          <Button data-size="sm" type="submit" disabled={!nyKanalKanal.trim()}>Legg til kanal</Button>
        </form>
        {kanalFeil && <div className="feilmelding" style={{ marginTop: '0.5rem' }}>{kanalFeil}</div>}
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>Behandlingstid</Heading>
        <form onSubmit={lagreBehandlingstid} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <Textfield data-size="sm" label="Frist" value={frist} onChange={(e) => setFrist(e.target.value)} style={{ minWidth: '24rem' }} />
          <Textfield data-size="sm" label="Hjemmel — lov" value={behandlingstidLov} onChange={(e) => setBehandlingstidLov(e.target.value)} />
          <Textfield data-size="sm" label="Hjemmel — henvisning" value={behandlingstidHenvisning} onChange={(e) => setBehandlingstidHenvisning(e.target.value)} />
          <Button data-size="sm" type="submit" disabled={behandlingstidLagrer}>{behandlingstidLagrer ? 'Lagrer …' : 'Lagre'}</Button>
        </form>
        {behandlingstidFeil && <div className="feilmelding" style={{ marginTop: '0.5rem' }}>{behandlingstidFeil}</div>}
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>Kostnad</Heading>
        <form onSubmit={lagreKostnad} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: '0.75rem' }}>
          <Textfield data-size="sm" label="Beløp/beskrivelse" value={belop} onChange={(e) => setBelop(e.target.value)} style={{ minWidth: '28rem' }} />
          <Button data-size="sm" type="submit" disabled={belopLagrer}>{belopLagrer ? 'Lagrer …' : 'Lagre'}</Button>
        </form>
        {belopFeil && <div className="feilmelding" style={{ marginBottom: '0.5rem' }}>{belopFeil}</div>}
        {handling.kostnad.hjemmel.length > 0 && (
          <ul>
            {handling.kostnad.hjemmel.map((h, i) => (
              <li key={i} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                <VisHjemmel hjemmel={h} />
                <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjernKostnadHjemmel(i)}>Fjern</Button>
              </li>
            ))}
          </ul>
        )}
        <form onSubmit={leggTilKostnadHjemmel} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginTop: '0.5rem' }}>
          <Textfield data-size="sm" label="Hjemmel — lov" value={nyKostnadHjemmelLov} onChange={(e) => setNyKostnadHjemmelLov(e.target.value)} />
          <Textfield data-size="sm" label="Hjemmel — henvisning" value={nyKostnadHjemmelHenvisning} onChange={(e) => setNyKostnadHjemmelHenvisning(e.target.value)} />
          <Button data-size="sm" type="submit" disabled={!nyKostnadHjemmelLov.trim()}>Legg til hjemmel</Button>
        </form>
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>Vedlegg</Heading>
        {handling.vedlegg.length === 0 && <Paragraph>Ingen vedlegg registrert ennå.</Paragraph>}
        {handling.vedlegg.length > 0 && (
          <ul>
            {handling.vedlegg.map((v, i) => (
              <li key={i} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                <span>{v.navn}</span>
                {v.kategori && <Tag data-color="neutral" data-size="sm">{v.kategori}</Tag>}
                <VisHjemmel hjemmel={v.hjemmel} />
                <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjernVedlegg(i)}>Fjern</Button>
              </li>
            ))}
          </ul>
        )}
        <form onSubmit={leggTilVedlegg} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginTop: '0.75rem' }}>
          <Textfield data-size="sm" label="Navn" value={nyVedleggNavn} onChange={(e) => setNyVedleggNavn(e.target.value)} required />
          <Textfield data-size="sm" label="Kategori (valgfritt)" value={nyVedleggKategori} onChange={(e) => setNyVedleggKategori(e.target.value)} />
          <Textfield data-size="sm" label="Hjemmel — lov" value={nyVedleggLov} onChange={(e) => setNyVedleggLov(e.target.value)} />
          <Textfield data-size="sm" label="Hjemmel — henvisning" value={nyVedleggHenvisning} onChange={(e) => setNyVedleggHenvisning(e.target.value)} />
          <Button data-size="sm" type="submit" disabled={!nyVedleggNavn.trim()}>Legg til vedlegg</Button>
        </form>
        {vedleggFeil && <div className="feilmelding" style={{ marginTop: '0.5rem' }}>{vedleggFeil}</div>}
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>Veiledningstekst</Heading>
        {handling.veiledningstekst.length === 0 && <Paragraph>Ingen veiledningstekst registrert ennå.</Paragraph>}
        {handling.veiledningstekst.length > 0 && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', marginBottom: '0.75rem' }}>
            {handling.veiledningstekst.map((v, i) => (
              <div key={i} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-start' }}>
                <div style={{ flex: 1 }}>
                  <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                    <strong>{v.overskrift}</strong>
                    <VisHjemmel hjemmel={v.hjemmel} />
                  </div>
                  {v.innhold && <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', marginTop: '0.2rem' }}>{v.innhold}</Paragraph>}
                </div>
                <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjernVeiledning(i)}>Fjern</Button>
              </div>
            ))}
          </div>
        )}
        <form onSubmit={leggTilVeiledning} style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', maxWidth: '40rem' }}>
          <Textfield data-size="sm" label="Overskrift" value={nyVeiledningOverskrift} onChange={(e) => setNyVeiledningOverskrift(e.target.value)} required />
          <Field>
            <Label>Innhold</Label>
            <Textarea value={nyVeiledningInnhold} onChange={(e) => setNyVeiledningInnhold(e.target.value)} rows={2} />
          </Field>
          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            <Textfield data-size="sm" label="Hjemmel — lov" value={nyVeiledningLov} onChange={(e) => setNyVeiledningLov(e.target.value)} />
            <Textfield data-size="sm" label="Hjemmel — henvisning" value={nyVeiledningHenvisning} onChange={(e) => setNyVeiledningHenvisning(e.target.value)} />
          </div>
          <div><Button data-size="sm" type="submit" disabled={!nyVeiledningOverskrift.trim()}>Legg til veiledningstekst</Button></div>
        </form>
        {veiledningFeil && <div className="feilmelding" style={{ marginTop: '0.5rem' }}>{veiledningFeil}</div>}
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>Årsaker til bortfall/tilbaketrekking</Heading>
        <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)', marginBottom: '0.75rem' }}>
          Kun relevant for handlinger som representerer at rettigheten faller bort eller trekkes tilbake.
        </Paragraph>
        {handling.arsaker.length === 0 && <Paragraph>Ingen årsaker registrert ennå.</Paragraph>}
        {handling.arsaker.length > 0 && (
          <ul>
            {handling.arsaker.map((a, i) => (
              <li key={i} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                <span>{a.arsak}</span>
                <VisHjemmel hjemmel={a.hjemmel} />
                <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjernArsak(i)}>Fjern</Button>
              </li>
            ))}
          </ul>
        )}
        <form onSubmit={leggTilArsak} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginTop: '0.75rem' }}>
          <Textfield data-size="sm" label="Årsak" value={nyArsakArsak} onChange={(e) => setNyArsakArsak(e.target.value)} required />
          <Textfield data-size="sm" label="Hjemmel — lov" value={nyArsakLov} onChange={(e) => setNyArsakLov(e.target.value)} required />
          <Textfield data-size="sm" label="Hjemmel — henvisning" value={nyArsakHenvisning} onChange={(e) => setNyArsakHenvisning(e.target.value)} />
          <Button data-size="sm" type="submit" disabled={!nyArsakArsak.trim() || !nyArsakLov.trim()}>Legg til årsak</Button>
        </form>
        {arsakFeil && <div className="feilmelding" style={{ marginTop: '0.5rem' }}>{arsakFeil}</div>}
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>Resultat</Heading>
        <form onSubmit={lagreResultatHva} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: '0.75rem' }}>
          <Textfield data-size="sm" label="Hva oppnås" value={hva} onChange={(e) => setHva(e.target.value)} style={{ minWidth: '28rem' }} />
          <Button data-size="sm" type="submit">Lagre</Button>
        </form>
        {resultatFeil && <div className="feilmelding" style={{ marginBottom: '0.5rem' }}>{resultatFeil}</div>}
        <Label>Bevis-kanaler (hvordan resultatet dokumenteres)</Label>
        {handling.resultat.bevisKanaler.length > 0 && (
          <ul>
            {handling.resultat.bevisKanaler.map((b, i) => (
              <li key={i} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                <span>{b.kanal}</span>
                <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjernBevisKanal(i)}>Fjern</Button>
              </li>
            ))}
          </ul>
        )}
        <form onSubmit={leggTilBevisKanal} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginTop: '0.5rem' }}>
          <Textfield data-size="sm" label="Ny bevis-kanal" value={nyBevisKanal} onChange={(e) => setNyBevisKanal(e.target.value)} />
          <Button data-size="sm" type="submit" disabled={!nyBevisKanal.trim()}>Legg til</Button>
        </form>
      </section>
    </>
  );
}
