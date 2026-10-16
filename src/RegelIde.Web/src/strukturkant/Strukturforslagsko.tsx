import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import {
  Alert, Button, Card, Checkbox, Details, Field, Heading, Label, Link, Paragraph, Select, Spinner, Switch, Table, Tag, Textfield,
} from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type {
  AktorsveipRapport, RettskildeSammendrag, StrukturforslagRapport, StrukturkantDto, StrukturkantForhandsvisningDto,
} from '../api/types';
import { RettskildeVelger } from '../rettskilde/RettskildeVelger';
import { Metatekst } from '../entitet/Metatekst';
import { TreffidentitetTekst } from '../virksomhet/AktoridentitetVerktoy';
import { Massehandlingsrad } from '../kandidater/Massehandlingsrad';
import { useKandidatvalg } from '../kandidater/useKandidatvalg';
import { STRUKTURKANT_KATEGORI_VISNING, StrukturkantTabell } from './StrukturkantTabell';
import { AKTORSTATUS_VISNING, avvisningstekst, grupperPerHjemmel, tillitFor, UTFALL_VISNING, type Hjemmelgruppe } from './forslagsko';

/**
 * [Ny, issue #313 «Strukturmodell 8», 2026-10-15] Forslagskøen for strukturkanter — ÉN kø for alle maskinelle forslag (mønster og KI),
 * erstatter den gamle lista på KI-oppdagelsessiden (#264: ingen ny kø-side med egen mekanikk). Gjenbruker kandidatbiblioteket
 * (`useKandidatvalg`, `Massehandlingsrad`) og `StrukturkantTabell` (docs/09 §31) — ingen egen kanttabell.
 *
 * - **Last forslag:** kjører konverteringen (mønster/KI) på én rettskilde og viser rapporten: tall per kategori, hva som ikke ble lastet og
 *   hvorfor, aktører som ble navnekandidater, og hva som var «tidligere avvist» (foreslås ikke på nytt, #374).
 * - **Behandle:** godkjenn/avvis per forslag, de valgte, eller alle per hjemmel. Avvis tar en valgfri begrunnelse og SLETTER IKKE (#374).
 * - **Forhåndsvis:** slik forslaget blir seende ut på aktørsidene og rettskildesiden (samme visningstekst som sidene beregner).
 * - **Vis avviste:** de avviste, med hvem, når og hvorfor.
 */
export function Strukturforslagsko() {
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [rettskildeId, setRettskildeId] = useState('');
  const [lag, setLag] = useState<'monster' | 'ki'>('monster');
  const [laster, setLaster] = useState(false);
  const [rapport, setRapport] = useState<StrukturforslagRapport | null>(null);
  const [lastFeil, setLastFeil] = useState<string | null>(null);
  // [Ny, issue #378 A1] Aktørsveipet (steg 1): klassifiserer aktørene uten å skrive forslag.
  const [sveip, setSveip] = useState<AktorsveipRapport | null>(null);

  const [forslag, setForslag] = useState<StrukturkantDto[] | null>(null);
  const [visAvviste, setVisAvviste] = useState(false);
  const [avviste, setAvviste] = useState<StrukturkantDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [kjorer, setKjorer] = useState(false);
  const [begrunnelse, setBegrunnelse] = useState('');
  const [bekreft, setBekreft] = useState<{ hjemmel: string; handling: 'godkjenn' | 'avvis' } | null>(null);
  const [forhand, setForhand] = useState<{ id: string; data: StrukturkantForhandsvisningDto | null } | null>(null);
  const valg = useKandidatvalg();

  function lastKo() {
    api.hentStrukturkantForslag().then(setForslag).catch((e) => setFeil(e instanceof ApiError ? e.message : 'Kunne ikke hente forslagene.'));
    if (visAvviste) api.hentAvvisteStrukturkanter().then(setAvviste).catch(() => setAvviste([]));
  }

  useEffect(() => {
    api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([]));
  }, []);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(lastKo, [visAvviste]);

  const grupper = useMemo(() => grupperPerHjemmel(forslag ?? []), [forslag]);

  async function kjorAktorsveip(navnekandidater: boolean) {
    setLastFeil(null);
    setLaster(true);
    try {
      setSveip(await api.aktorsveip(rettskildeId, lag, navnekandidater));
    } catch (e) {
      setLastFeil(e instanceof ApiError ? e.message : 'Ukjent feil i aktørsveipet.');
    } finally {
      setLaster(false);
    }
  }

  async function lastForslag() {
    setLastFeil(null);
    setRapport(null);
    setLaster(true);
    try {
      setRapport(await api.lastStrukturforslag(rettskildeId, lag));
      lastKo();
    } catch (e) {
      setLastFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved lasting av forslag.');
    } finally {
      setLaster(false);
    }
  }

  /** Kjører én handling per id; beholder det som gikk, og melder «n av m feilet» (Massehandlingsrad-mønsteret). */
  async function behandle(ider: string[], handling: 'godkjenn' | 'avvis') {
    setFeil(null);
    setKjorer(true);
    const feilet: string[] = [];
    for (const id of ider) {
      try {
        if (handling === 'godkjenn') await api.godkjennStrukturkant(id);
        else await api.avvisStrukturkant(id, begrunnelse);
      } catch (e) {
        feilet.push(e instanceof ApiError ? e.message : id);
      }
    }
    if (feilet.length > 0) setFeil(`${feilet.length} av ${ider.length} feilet: ${feilet[0]}`);
    valg.nullstill();
    setKjorer(false);
    lastKo();
  }

  async function alleForHjemmel(g: Hjemmelgruppe, handling: 'godkjenn' | 'avvis') {
    if (!g.hjemmelRettskildeId) return;
    setFeil(null);
    setKjorer(true);
    try {
      if (handling === 'godkjenn') await api.godkjennAlleStrukturkantForslag(g.hjemmelRettskildeId);
      else await api.avvisAlleStrukturkantForslag(g.hjemmelRettskildeId, begrunnelse);
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Ukjent feil.');
    } finally {
      setBekreft(null);
      setKjorer(false);
      lastKo();
    }
  }

  async function forhandsvis(id: string) {
    if (forhand?.id === id) { setForhand(null); return; }
    setForhand({ id, data: null });
    try {
      setForhand({ id, data: await api.forhandsvisStrukturkant(id) });
    } catch {
      setForhand(null);
    }
  }

  function Forhandsvisning({ kant }: { kant: StrukturkantDto }) {
    if (forhand?.id !== kant.id) return null;
    if (!forhand.data) return <Spinner aria-label="Laster forhåndsvisning …" data-size="sm" />;
    const f = forhand.data;
    return (
      <Card style={{ padding: '0.75rem', marginBottom: '0.75rem' }}>
        <Heading level={4} data-size="2xs">Slik blir forslaget seende ut når det er godkjent</Heading>
        <Metatekst>På {f.fraSide.fra.navn} sin side</Metatekst>
        <StrukturkantTabell kanter={[f.fraSide]} tomTekst="" />
        {f.tilSide && f.tilSide.til && (
          <>
            <Metatekst>På {f.tilSide.til.navn} sin side</Metatekst>
            <StrukturkantTabell kanter={[f.tilSide]} tomTekst="" />
          </>
        )}
        <Metatekst>På rettskildesiden ({f.rettskildeside.hjemmelRettskildeTittel ?? 'hjemmelen'})</Metatekst>
        <StrukturkantTabell kanter={[f.rettskildeside]} tomTekst="" visKategori visHjemmelKilde={false} />
      </Card>
    );
  }

  function Radhandlinger({ kant }: { kant: StrukturkantDto }) {
    const tillit = tillitFor(kant);
    return (
      <div style={{ display: 'flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap' }}>
        <Checkbox aria-label="Velg forslaget" data-size="sm" checked={valg.erValgt(kant.id)}
          onChange={(e) => valg.veksl(kant.id, e.target.checked)} />
        {tillit && (
          <Tag data-size="sm" data-color="neutral" title="KI-forslag: tilliten docs/33 §5.5 gir typen (målt presisjon). En klassifisering, ikke en avgjørelse.">
            {tillit === 'lav' ? 'Lav tillit (KI)' : 'Middels tillit (KI)'}
          </Tag>
        )}
        <Button variant="tertiary" data-size="sm" onClick={() => forhandsvis(kant.id)}>
          {forhand?.id === kant.id ? 'Skjul forhåndsvisning' : 'Forhåndsvis'}
        </Button>
        <Button variant="tertiary" data-size="sm" disabled={kjorer} onClick={() => behandle([kant.id], 'avvis')}>Avvis</Button>
        <Button data-size="sm" disabled={kjorer} onClick={() => behandle([kant.id], 'godkjenn')}>Godkjenn</Button>
      </div>
    );
  }

  return (
    <>
      <Heading level={2} data-size="sm" style={{ marginTop: '1rem' }}>Forslagskø for strukturkanter</Heading>
      <Paragraph style={{ marginBottom: '1rem', maxWidth: '46rem' }}>
        Maskinelle forslag (mønsterlaget og KI) til strukturkanter — relasjon, kompetanse, plikt, medlemskap, område og tilhørighet. Et forslag
        regnes ikke som gjeldende før et menneske har godkjent det. Avviste forslag slettes ikke: de blir stående som avvist med hvem, når og
        hvorfor, og foreslås ikke på nytt.
      </Paragraph>

      <Card style={{ padding: '1rem', marginBottom: '1rem' }}>
        <Heading level={3} data-size="xs">Last forslag fra en rettskilde</Heading>
        <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <RettskildeVelger rettskilder={rettskilder} value={rettskildeId} onChange={setRettskildeId} />
          <Field>
            <Label>Lag</Label>
            <Select data-size="sm" value={lag} onChange={(e) => setLag(e.target.value as 'monster' | 'ki')}>
              <Select.Option value="monster">Mønster (deterministisk)</Select.Option>
              <Select.Option value="ki">KI (lav tillit utenom forskrift)</Select.Option>
            </Select>
          </Field>
          <Button data-size="sm" variant="secondary" onClick={() => kjorAktorsveip(false)} disabled={laster || !rettskildeId}>
            1. Aktørsveip
          </Button>
          <Button data-size="sm" onClick={lastForslag} disabled={laster || !rettskildeId}>
            {laster ? 'Kjører …' : '2. Last forslag'}
          </Button>
        </div>
        <Metatekst>
          Steg 1 (aktørsveip) viser hvilke aktører konverteringen finner og om de er kjent, uten å skrive forslag. Behandle de ukjente i
          navnekandidatveiviseren — koble til en aktør, opprett en, eller marker som generisk og bind for rettskilden — og last så forslagene
          (steg 2). En ny kjøring på samme rettskilde lager ingen duplikater. Aktører løses bare ved eksakt treff eller en godkjent binding.
        </Metatekst>
        {lastFeil && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{lastFeil}</Alert>}
        {sveip && <Aktorsveiprapport rapport={sveip} kjorer={laster} onSkrivKandidater={() => kjorAktorsveip(true)} />}
        {rapport && <Lasterapport rapport={rapport} />}
      </Card>

      <div style={{ display: 'flex', gap: '1rem', alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: '0.75rem' }}>
        <Textfield data-size="sm" label="Begrunnelse ved avvisning (valgfri)" value={begrunnelse}
          onChange={(e) => setBegrunnelse(e.target.value)} style={{ minWidth: '22rem' }} />
        <Switch label="Vis avviste" checked={visAvviste} onChange={(e) => setVisAvviste(e.target.checked)} />
      </div>
      {feil && <Alert data-color="danger" style={{ marginBottom: '0.75rem' }}>{feil}</Alert>}

      {!forslag && <Spinner aria-label="Laster …" data-size="sm" />}
      {forslag && forslag.length === 0 && <Paragraph>Ingen ventende forslag.</Paragraph>}
      {grupper.map((g) => {
        const nokkel = g.hjemmelRettskildeId ?? 'ingen';
        const venter = bekreft?.hjemmel === nokkel ? bekreft.handling : null;
        return (
          <Details key={nokkel} style={{ marginBottom: '0.6rem' }}>
            <Details.Summary>{g.tittel} — {g.kanter.length} forslag</Details.Summary>
            <Details.Content>
              {g.hjemmelRettskildeId && (
                <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '0.5rem' }}>
                  {venter ? (
                    <>
                      <Metatekst style={{ margin: 0, alignSelf: 'center' }}>
                        {venter === 'godkjenn' ? 'Godkjenne' : 'Avvise'} alle maskinelle forslag hjemlet i {g.tittel}?
                      </Metatekst>
                      <Button data-size="sm" data-color={venter === 'avvis' ? 'danger' : undefined} disabled={kjorer}
                        onClick={() => alleForHjemmel(g, venter)}>Bekreft</Button>
                      <Button data-size="sm" variant="tertiary" onClick={() => setBekreft(null)}>Avbryt</Button>
                    </>
                  ) : (
                    <>
                      <Button data-size="sm" variant="secondary" onClick={() => setBekreft({ hjemmel: nokkel, handling: 'godkjenn' })}>
                        Godkjenn alle {g.kanter.length}
                      </Button>
                      <Button data-size="sm" variant="secondary" onClick={() => setBekreft({ hjemmel: nokkel, handling: 'avvis' })}>
                        Avvis alle {g.kanter.length}
                      </Button>
                    </>
                  )}
                </div>
              )}
              <Massehandlingsrad
                antallValgte={g.kanter.filter((k) => valg.erValgt(k.id)).length} kjorer={kjorer} feil={null}
                onGodkjenn={() => behandle(g.kanter.filter((k) => valg.erValgt(k.id)).map((k) => k.id), 'godkjenn')}
                onAvvis={() => behandle(g.kanter.filter((k) => valg.erValgt(k.id)).map((k) => k.id), 'avvis')}
              >
                <Checkbox data-size="sm" label="Velg alle i gruppen" checked={g.kanter.every((k) => valg.erValgt(k.id))}
                  onChange={(e) => valg.vekslGruppe(g.kanter, e.target.checked)} />
              </Massehandlingsrad>
              {g.kanter.filter((k) => forhand?.id === k.id).map((k) => <Forhandsvisning key={k.id} kant={k} />)}
              <StrukturkantTabell kanter={g.kanter} tomTekst="" visKategori visHjemmelKilde={false}
                handlinger={(k) => <Radhandlinger kant={k} />} />
            </Details.Content>
          </Details>
        );
      })}

      {visAvviste && (
        <>
          <Heading level={3} data-size="xs" style={{ marginTop: '1rem' }}>Avviste</Heading>
          <StrukturkantTabell kanter={avviste} tomTekst="Ingen avviste." visKategori
            handlinger={(k) => <Metatekst style={{ margin: 0 }}>{avvisningstekst(k)}</Metatekst>} />
        </>
      )}
    </>
  );
}

/** [Ny, issue #378 A1] Aktørsveipets rapport: antall per status og hver aktør med status, treff/kandidater og navnekandidat. */
function Aktorsveiprapport({ rapport, kjorer, onSkrivKandidater }: {
  rapport: AktorsveipRapport; kjorer: boolean; onSkrivKandidater: () => void;
}) {
  const ukjente = rapport.aktorer.filter((a) => a.status !== 'kjent' && a.status !== 'flertydig');
  function detalj(a: AktorsveipRapport['aktorer'][number]): string {
    const deler: string[] = [];
    if (a.referent) deler.push(`Bundet: ${a.referent.navn}`);
    else if (a.generiskNavneformId) deler.push(`Generisk — ${a.bundneForekomster} av ${a.forekomster} bundet`);
    if (a.foreslatteBindinger > 0) deler.push(`${a.foreslatteBindinger} foreslått binding venter`);
    if (a.treff.length > 0 && !a.referent) deler.push(a.treff.join(', '));
    if (a.kandidater.length > 0) {
      const flere = a.antallKandidater > a.kandidater.length ? ` (+${a.antallKandidater - a.kandidater.length})` : '';
      deler.push(`Står i: ${a.kandidater.join('; ')}${flere}`);
    }
    return deler.join(' · ');
  }
  return (
    <div style={{ marginTop: '0.75rem' }}>
      <Paragraph>
        Aktørsveip, {rapport.rettskildeTittel}: {rapport.aktorerTotalt} aktører i {rapport.utsagnTotalt} utsagn fra {rapport.noderLest} noder.
        Ingen forslag er skrevet.
      </Paragraph>
      <Table data-density="compact" border>
        <Table.Head>
          <Table.Row>{rapport.perStatus.map((p) => <Table.HeaderCell key={p.status}>{AKTORSTATUS_VISNING[p.status]}</Table.HeaderCell>)}</Table.Row>
        </Table.Head>
        <Table.Body>
          <Table.Row>{rapport.perStatus.map((p) => <Table.Cell key={p.status}>{p.antall}</Table.Cell>)}</Table.Row>
        </Table.Body>
      </Table>
      {!rapport.navnekandidaterSkrevet && ukjente.length > 0 && (
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginTop: '0.5rem', flexWrap: 'wrap' }}>
          <Button data-size="sm" variant="secondary" disabled={kjorer} onClick={onSkrivKandidater}>
            Legg {ukjente.length} i navnekandidatkøen
          </Button>
          <Metatekst as="span" style={{ margin: 0 }}>
            Ny aktør, ny navneform og generisk uten binding. Flertydige får ingen kandidat. [#385] For de kjente aktørene registreres samtidig
            navneformen (forslag ved treff via registernavnet), kilden der navnet står og taggen på nøyaktig spenn.
          </Metatekst>
        </div>
      )}
      {rapport.aktoridentitet && (
        <Metatekst style={{ marginTop: '0.5rem' }}>Navnetreffene: <TreffidentitetTekst t={rapport.aktoridentitet} /></Metatekst>
      )}
      <Details style={{ marginTop: '0.5rem' }}>
        <Details.Summary>Aktørene ({rapport.aktorer.length})</Details.Summary>
        <Details.Content>
          <Table data-density="compact" border>
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>Tekstform</Table.HeaderCell>
                <Table.HeaderCell>Status</Table.HeaderCell>
                <Table.HeaderCell>Utsagn</Table.HeaderCell>
                <Table.HeaderCell>Treff, binding eller kandidater</Table.HeaderCell>
                <Table.HeaderCell>Behandle</Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {rapport.aktorer.map((a) => (
                <Table.Row key={a.aktorId}>
                  <Table.Cell>«{a.tekstform}»</Table.Cell>
                  <Table.Cell><Tag data-size="sm" data-color="neutral">{AKTORSTATUS_VISNING[a.status]}</Tag></Table.Cell>
                  <Table.Cell>
                    {a.forekomster}
                    {a.lastbareForekomster !== a.forekomster && <Metatekst as="span"> ({a.lastbareForekomster} lastbare)</Metatekst>}
                  </Table.Cell>
                  <Table.Cell><Metatekst as="span" style={{ margin: 0 }}>{detalj(a)}</Metatekst></Table.Cell>
                  <Table.Cell>
                    {a.navnekandidatId && (
                      <Link asChild><RouterLink to={`/navnekandidater/${a.navnekandidatId}/behandle`}>Behandle</RouterLink></Link>
                    )}
                  </Table.Cell>
                </Table.Row>
              ))}
            </Table.Body>
          </Table>
        </Details.Content>
      </Details>
    </div>
  );
}

function Lasterapport({ rapport }: { rapport: StrukturforslagRapport }) {
  const tidligereAvvist = rapport.rader.filter((r) => r.utfall === 'tidligere_avvist');
  return (
    <div style={{ marginTop: '0.75rem' }}>
      <Paragraph>
        {rapport.rettskildeTittel}: {rapport.utsagnTotalt} utsagn fra {rapport.noderLest} noder ({rapport.lag === 'ki' ? 'KI' : 'mønster'}).
        {rapport.kiKastedeRader !== null && ` ${rapport.kiKastedeRader} KI-rader kastet av valideringen.`}
        {rapport.kiInputTokens !== null && ` ${rapport.kiInputTokens} / ${rapport.kiOutputTokens ?? '—'} tokens.`}
      </Paragraph>
      <Table data-density="compact" border>
        <Table.Head>
          <Table.Row>
            <Table.HeaderCell>Kategori</Table.HeaderCell>
            <Table.HeaderCell>Utsagn</Table.HeaderCell>
            <Table.HeaderCell>{UTFALL_VISNING.ny}</Table.HeaderCell>
            <Table.HeaderCell>{UTFALL_VISNING.fantes_som_forslag}</Table.HeaderCell>
            <Table.HeaderCell>{UTFALL_VISNING.fantes_godkjent}</Table.HeaderCell>
            <Table.HeaderCell>{UTFALL_VISNING.tidligere_avvist}</Table.HeaderCell>
            <Table.HeaderCell>{UTFALL_VISNING.dublett_i_kjoringen}</Table.HeaderCell>
            <Table.HeaderCell>{UTFALL_VISNING.ikke_lastet}</Table.HeaderCell>
          </Table.Row>
        </Table.Head>
        <Table.Body>
          {rapport.perKategori.map((t) => (
            <Table.Row key={t.kategori}>
              <Table.Cell>{STRUKTURKANT_KATEGORI_VISNING[t.kategori as keyof typeof STRUKTURKANT_KATEGORI_VISNING]?.tekst ?? t.kategori}</Table.Cell>
              <Table.Cell>{t.utsagn}</Table.Cell>
              <Table.Cell>{t.nye}</Table.Cell>
              <Table.Cell>{t.fantesSomForslag}</Table.Cell>
              <Table.Cell>{t.fantesGodkjent}</Table.Cell>
              <Table.Cell>{t.tidligereAvvist}</Table.Cell>
              <Table.Cell>{t.dubletter}</Table.Cell>
              <Table.Cell>{t.ikkeLastet}</Table.Cell>
            </Table.Row>
          ))}
        </Table.Body>
      </Table>
      {rapport.aktorerPerStatus.length > 0 && (
        <Metatekst style={{ marginTop: '0.5rem' }}>
          Aktører nå: {rapport.aktorerPerStatus.filter((p) => p.antall > 0).map((p) => `${AKTORSTATUS_VISNING[p.status]} ${p.antall}`).join(' · ')}
          {' '}— sammenlign med aktørsveipet før gjennomgangen.
        </Metatekst>
      )}
      {rapport.aktoridentitet && (
        <Metatekst style={{ marginTop: '0.25rem' }}>Navnetreffene (#385): <TreffidentitetTekst t={rapport.aktoridentitet} /></Metatekst>
      )}
      {rapport.ikkeLastetPerArsak.length > 0 && (
        <>
          <Heading level={4} data-size="2xs" style={{ marginTop: '0.75rem' }}>Ikke lastet, med årsak</Heading>
          <ul style={{ margin: 0 }}>
            {rapport.ikkeLastetPerArsak.map((a) => (
              <li key={a.arsak}>
                <strong>{a.antall}</strong> — {a.tekst}
                {a.eksempler[0]?.detalj && <Metatekst as="span"> (f.eks. {a.eksempler[0].detalj})</Metatekst>}
              </li>
            ))}
          </ul>
        </>
      )}
      {rapport.uloesteAktorer.length > 0 && (
        <>
          <Heading level={4} data-size="2xs" style={{ marginTop: '0.75rem' }}>Aktører som ikke kunne løses opp</Heading>
          <ul style={{ margin: 0 }}>
            {rapport.uloesteAktorer.map((a) => (
              <li key={a.tekstform}>
                «{a.tekstform}» ({a.antallUtsagn} utsagn){a.arsak === 'aktor_flertydig' ? ` — flertydig: ${a.treff.join(', ')}` : ''}
                {a.navnekandidatId && (
                  <> — <Link asChild><RouterLink to={`/navnekandidater/${a.navnekandidatId}/behandle`}>behandle navnekandidaten</RouterLink></Link></>
                )}
              </li>
            ))}
          </ul>
        </>
      )}
      {tidligereAvvist.length > 0 && (
        <Alert data-color="info" style={{ marginTop: '0.75rem' }}>
          {tidligereAvvist.length} utsagn er tidligere avvist og ble ikke foreslått på nytt. Slå på «Vis avviste» for å se hvem som avviste og hvorfor.
        </Alert>
      )}
    </div>
  );
}
