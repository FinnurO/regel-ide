import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router';
import {
  Alert, Breadcrumbs, Button, Card, Divider, Field, Heading, Link, Paragraph, Radio, Search,
  Spinner, Table, Tag, Textfield,
} from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type {
  BrregEnhetDto, NavnekandidatDto, Navneformgrunn, RettskildeDetalj as RettskildeDetaljDto,
  RettskildeNodeDto,
} from '../api/types';
import { BerikelseVisning } from '../virksomhet/BerikelseVisning';
import { NavneformgrunnVelger } from '../virksomhet/Navneformgrunn';
import { VirksomhetVelger } from '../virksomhet/VirksomhetVelger';
import { useVirksomheter } from '../virksomhet/useVirksomheter';

/**
 * [Ny, navnekandidat-wizard-runden, 2026-09-07] Behandling av ÉN navnekandidat, ende til ende.
 *
 * <h3>Hva den løser</h3>
 * «Godkjenn» på Navnekandidater-siden var én knapp som betydde ulike ting per kategori, og for
 * `virksomhet` endte den i en blindvei: det ble opprettet en `TekstTagg` med `RefId=null` som ALDRI
 * ble koblet, og «Finn/opprett virksomhet»-lenken forsvant så snart status ikke var «Venter» lenger.
 * Denne veiviseren tar saksbehandleren gjennom hele kjeden i stedet, og lukker den:
 * navneform (med begrunnelse) → godkjent kandidat → en tagg som faktisk PEKER på virksomheten.
 *
 * <h3>De to mekanismene som IKKE skal blandes</h3>
 * Steg 1 retter TEKSTEN, og finnes bare for regex-artefakter («Ø Suldal kommune», der en ledende Ø er
 * limt inn fra en koordinat). Steg 3 velger BEGRUNNELSEN, for legitime strenger som faktisk står slik
 * i lovteksten («Suldal», «Matilsynet», «Arkivverket») — de skal beholdes ordrett, ikke skrives om.
 * Se `Navneformgrunn.tsx` og `NavnekandidatOppdagelseTjeneste.OppdaterAsync`.
 *
 * <h3>Bevisst UTENFOR denne runden</h3>
 * Kategoriene «administrativ inndeling» og «medlem av eksisterende gruppe» er ikke bygget, og har
 * bevisst ingen stubber som ser byggbare ut — en halvferdig vei i en veiviser er verre enn ingen.
 * Korreksjonsregel-tabellen (issue #203 pkt. 4) kommer separat; steg 1 retter ÉN rad, og lærer ikke
 * et mønster som kan brukes på nye sveip.
 *
 * <h3>Designmønster</h3>
 * Ny side, så den følger saksbehandler-mønsteret fra dag én (docs/09 §14): brødsmulesti,
 * `data-size="sm"` gjennomgående, `Card` ALLTID rendret med tom-tilstand som `Paragraph` inni, og
 * steg-indikatoren som en `Tag`-rekke (samme visuelle idiom som `StatusStepper`, men den er bundet
 * til den 6-trinns entitets-statusmodellen og passer ikke her).
 */

/** Steg 2 sitt valg. `null` = ikke valgt ennå. */
type Slag = 'virksomhet' | 'gruppe' | 'irrelevant';

/** Steg 3 sin gren: koble til en som finnes, eller opprette en ny (fra Brreg, eller kun navn). */
type VirksomhetVei = 'eksisterende' | 'brreg' | 'kunNavn';

const STEG_TITLER = [
  'Kontekst',
  'Er teksten riktig?',
  'Hva slags ting er dette?',
  'Hvilken virksomhet?',
  'Bekreft',
] as const;

/**
 * Setningen fra rettskilden med treffet markert. Bygget på nøyaktig de samme offsetene og det samme
 * 30-tegns kontekstvinduet som backend bruker for `QuotePrefix`/`QuoteSuffix`
 * (`NavnekandidatOppdagelseTjeneste`) — men her vises HELE nodeteksten, med treffet uthevet, siden
 * poenget er at saksbehandleren skal kunne lese setningen og selv vurdere om treffet er riktig
 * avgrenset. Et for smalt vindu er nettopp hvordan «Ø Suldal kommune» slapp gjennom i utgangspunktet.
 */
function TreffIKontekst({ tekst, start, slutt }: { tekst: string; start: number; slutt: number }) {
  // Defensivt: rettskilden kan ha endret seg siden sveipet, så offsetene er ikke garantert gyldige.
  // Da vises teksten UTEN markering i stedet for å kaste eller markere feil sted.
  const gyldig = start >= 0 && slutt <= tekst.length && slutt > start;
  if (!gyldig) {
    return (
      <>
        <Paragraph data-size="sm" style={{ margin: 0, fontStyle: 'italic' }}>{tekst}</Paragraph>
        <Alert data-color="warning" data-size="sm" style={{ marginTop: '0.5rem' }}>
          Tegnposisjonene ({start}–{slutt}) stemmer ikke med nodeteksten ({tekst.length} tegn) —
          rettskilden er sannsynligvis endret siden sveipet. Treffet kan ikke markeres, og det blir
          ikke opprettet noen tagg i rettskilden ved fullføring.
        </Alert>
      </>
    );
  }
  return (
    <Paragraph data-size="sm" style={{ margin: 0 }}>
      {tekst.slice(0, start)}
      <mark
        style={{
          background: 'var(--ds-color-brand1-surface-tinted)',
          color: 'var(--ds-color-brand1-text-default)',
          borderBottom: '2px solid var(--ds-color-brand1-border-default)',
          fontWeight: 'var(--ds-font-weight-semibold)',
        }}
      >
        {tekst.slice(start, slutt)}
      </mark>
      {tekst.slice(slutt)}
    </Paragraph>
  );
}

export default function NavnekandidatVeiviser() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { virksomheter, oppdater: oppdaterVirksomheter } = useVirksomheter();

  const [kandidat, setKandidat] = useState<NavnekandidatDto | null>(null);
  const [rettskilde, setRettskilde] = useState<RettskildeDetaljDto | null>(null);
  const [noder, setNoder] = useState<RettskildeNodeDto[] | null>(null);
  const [lasteFeil, setLasteFeil] = useState<string | null>(null);

  const [steg, setSteg] = useState(0);

  // Steg 1
  const [tekst, setTekst] = useState('');
  const [lagrerTekst, setLagrerTekst] = useState(false);

  // Steg 2
  const [slag, setSlag] = useState<Slag | null>(null);

  // Steg 3
  const [vei, setVei] = useState<VirksomhetVei>('eksisterende');
  const [valgtVirksomhetId, setValgtVirksomhetId] = useState('');
  const [navneformgrunn, setNavneformgrunn] = useState<Navneformgrunn | null>(null);
  const [brregSok, setBrregSok] = useState('');
  const [brregTreff, setBrregTreff] = useState<BrregEnhetDto[] | null>(null);
  const [brregSoker, setBrregSoker] = useState(false);
  const [nyttNavn, setNyttNavn] = useState('');
  const [oppretterVirksomhet, setOppretterVirksomhet] = useState(false);

  // Steg 4 / avslutning
  const [fullfører, setFullfører] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);
  const [ferdig, setFerdig] = useState<{
    tittel: string;
    detaljer: string[];
    rettskildeLenke: string | null;
    /** Navnet på tagg-laget taggen faktisk havnet i — «Virksomhet» for virksomhet-veien,
     * «Begrep» for gruppe-veien (der taggen er en `begrep`-tagg mot gruppebegrepet). Må følge
     * utfallet, ikke være hardkodet: å sende saksbehandleren til feil lag er en påstand som
     * ikke stemmer, og laget er nettopp det man må velge for å SE markeringen. */
    taggLag: string | null;
    virksomhetLenke: string | null;
    advarsel: string | null;
  } | null>(null);

  useEffect(() => {
    if (!id) return;
    setLasteFeil(null);
    api.hentNavnekandidat(id)
      .then((k) => {
        setKandidat(k);
        setTekst(k.foreslattTekst);
        // Forhåndsvelg det opplagte: en 'gruppe'-kandidat skal normalt bli et gruppebegrep, en
        // 'virksomhet'-kandidat en konkret virksomhet. Fortsatt et VALG brukeren ser og kan endre —
        // steg 2 hoppes aldri over automatisk.
        setSlag(k.kategori === 'gruppe' ? 'gruppe' : 'virksomhet');
        setNyttNavn(k.foreslattTekst);
        setBrregSok(k.foreslattTekst);
        return Promise.all([api.hentRettskilde(k.rettskildeId), api.hentNoder(k.rettskildeId)]);
      })
      .then(([r, n]) => {
        setRettskilde(r);
        setNoder(n);
      })
      .catch((e) => setLasteFeil(e instanceof ApiError ? e.message : 'Kunne ikke laste navnekandidaten.'));
  }, [id]);

  const node = useMemo(
    () => (noder && kandidat ? noder.find((n) => n.eid === kandidat.nodeEid) ?? null : null),
    [noder, kandidat],
  );

  /** Treffet flytter seg når teksten er rettet — vis markeringen der den NYE teksten faktisk står,
   * ikke på det gamle intervallet. Finner vi den ikke, faller vi tilbake til sveipets offsets. */
  const markering = useMemo(() => {
    if (!kandidat || !node?.tekst) return null;
    const funnet = node.tekst.indexOf(kandidat.foreslattTekst);
    return funnet >= 0
      ? { start: funnet, slutt: funnet + kandidat.foreslattTekst.length }
      : { start: kandidat.startOffset, slutt: kandidat.endOffset };
  }, [kandidat, node]);

  async function lagreTekst() {
    if (!id || !kandidat) return;
    const ny = tekst.trim();
    if (ny === kandidat.foreslattTekst) { setSteg(2); return; } // ingen endring — ingen unødvendig skriving.
    setLagrerTekst(true);
    setFeil(null);
    try {
      const oppdatert = await api.oppdaterNavnekandidat(id, { foreslattTekst: ny });
      setKandidat(oppdatert);
      setTekst(oppdatert.foreslattTekst);
      setNyttNavn(oppdatert.foreslattTekst);
      setBrregSok(oppdatert.foreslattTekst);
      setSteg(2);
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Kunne ikke lagre den rettede teksten.');
    } finally {
      setLagrerTekst(false);
    }
  }

  async function søkBrreg() {
    if (!brregSok.trim()) return;
    setBrregSoker(true);
    setFeil(null);
    try {
      setBrregTreff(await api.sokBrreg(brregSok.trim()));
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Brreg-søket feilet.');
      setBrregTreff(null);
    } finally {
      setBrregSoker(false);
    }
  }

  /** Oppretter virksomheten (Brreg eller kun navn) og VELGER den — steg 3 er ikke ferdig før en
   * konkret virksomhet er valgt, uansett hvilken vei man kom dit. */
  async function opprettOgVelg(fra: 'brreg' | 'kunNavn', organisasjonsnummer?: string) {
    setOppretterVirksomhet(true);
    setFeil(null);
    try {
      const ny = fra === 'brreg'
        ? await api.opprettVirksomhetFraBrreg(organisasjonsnummer!)
        : await api.opprettVirksomhet({ navn: nyttNavn.trim(), overordnetEnhetId: null });
      await oppdaterVirksomheter();
      setValgtVirksomhetId(ny.id);
      setVei('eksisterende'); // den er nå nettopp det — en eksisterende, valgt virksomhet.
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Kunne ikke opprette virksomheten.');
    } finally {
      setOppretterVirksomhet(false);
    }
  }

  /** Steg 2-utfallene som avslutter veiviseren uten å gå via steg 3. */
  async function fullførIkkeVirksomhet(valg: 'gruppe' | 'irrelevant') {
    if (!id || !kandidat) return;
    setFullfører(true);
    setFeil(null);
    try {
      if (valg === 'gruppe') {
        // BEVISST helt uendret vei: samme GodkjennAsync som dagens fungerende hurtig-Godkjenn
        // (oppretter Begrep(gruppe) + tagg m/RefId). Ingen regresjon her (AK5).
        await api.godkjennNavnekandidat(id);
        setFerdig({
          tittel: `«${kandidat.foreslattTekst}» er opprettet som gruppebegrep.`,
          detaljer: [
            'Gruppebegrepet er hjemlet i denne rettskilden (navn + lov utgjør identiteten).',
            'Tekst-taggen for forekomsten er koblet til det nye gruppebegrepet.',
            'Navnekandidaten er satt til «Godkjent».',
          ],
          rettskildeLenke: `/rettskilder/${kandidat.rettskildeId}?eid=${encodeURIComponent(kandidat.nodeEid)}`,
          // Gruppe-veien lager en 'begrep'-tagg mot gruppebegrepet — altså laget «Begrep»,
          // IKKE «Virksomhet». Se GodkjennAsync.
          taggLag: 'Begrep',
          virksomhetLenke: null,
          advarsel: null,
        });
      } else {
        await api.avvisNavnekandidat(id);
        setFerdig({
          tittel: `«${kandidat.foreslattTekst}» er avvist.`,
          detaljer: ['Navnekandidaten er satt til «Avvist», og det er ikke opprettet noe innhold.'],
          rettskildeLenke: null,
          taggLag: null,
          virksomhetLenke: null,
          advarsel: null,
        });
      }
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Handlingen feilet.');
    } finally {
      setFullfører(false);
    }
  }

  /** Steg 4: lukker kjeden. */
  async function fullførVirksomhet() {
    if (!id || !kandidat || !valgtVirksomhetId) return;
    setFullfører(true);
    setFeil(null);
    try {
      const resultat = await api.koblNavnekandidatTilVirksomhet(id, {
        virksomhetId: valgtVirksomhetId,
        navneformgrunn,
      });
      const virksomhetNavn = virksomheter.find((v) => v.id === valgtVirksomhetId)?.navn ?? 'virksomheten';
      setFerdig({
        tittel: `«${resultat.navneform.term}» er nå en navneform for ${virksomhetNavn}.`,
        detaljer: [
          navneformgrunn
            ? `Begrunnelsen er lagret som «${navneformgrunn}».`
            : 'Ingen begrunnelse er satt (uspesifisert) — den kan settes senere på virksomhetens side.',
          'Navnekandidaten er satt til «Godkjent».',
          resultat.taggId
            ? 'Tekst-taggen for forekomsten peker nå på virksomheten, og er synlig i rettskilden under laget «Virksomhet».'
            : 'Ingen tekst-tagg ble opprettet — se advarselen under.',
        ],
        rettskildeLenke: resultat.taggId
          ? `/rettskilder/${resultat.rettskildeId}?eid=${encodeURIComponent(resultat.nodeEid)}`
          : null,
        taggLag: resultat.taggId ? 'Virksomhet' : null,
        virksomhetLenke: `/virksomheter/${valgtVirksomhetId}`,
        // Den dokumenterte degraderingen skal VISES, ikke skjules.
        advarsel: resultat.taggId
          ? null
          : 'Navneformen er koblet, men det ble ikke opprettet noen tagg i rettskildeteksten. Det skjer '
            + 'når rettskilden ikke har et ansvarlig departement som finnes i virksomhetskatalogen (en tagg '
            + 'må eies av en virksomhet), eller når tegnposisjonene ikke lenger stemmer fordi rettskilden er '
            + 'endret siden sveipet.',
      });
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Kunne ikke fullføre koblingen.');
    } finally {
      setFullfører(false);
    }
  }

  if (lasteFeil) {
    return (
      <>
        <Heading level={1} data-size="lg" style={{ marginBottom: '1rem' }}>Behandle navnekandidat</Heading>
        <Alert data-color="danger">{lasteFeil}</Alert>
      </>
    );
  }
  if (!kandidat) return <Spinner aria-label="Laster navnekandidaten …" />;

  const valgtVirksomhet = virksomheter.find((v) => v.id === valgtVirksomhetId) ?? null;

  return (
    <>
      <Breadcrumbs data-size="sm" style={{ marginBottom: '0.75rem' }}>
        <Breadcrumbs.List>
          <Breadcrumbs.Item>
            <Breadcrumbs.Link asChild><RouterLink to="/navnekandidater">Navnekandidater</RouterLink></Breadcrumbs.Link>
          </Breadcrumbs.Item>
          <Breadcrumbs.Item>
            <Breadcrumbs.Link aria-current="page">Behandle «{kandidat.foreslattTekst}»</Breadcrumbs.Link>
          </Breadcrumbs.Item>
        </Breadcrumbs.List>
      </Breadcrumbs>

      <Heading level={1} data-size="lg" style={{ marginBottom: '0.5rem' }}>
        Behandle «{kandidat.foreslattTekst}»
      </Heading>
      <Paragraph style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap', marginBottom: '1rem' }}>
        <Tag data-size="sm" data-color="neutral">Kategori: {kandidat.kategori}</Tag>
        <Tag data-size="sm" data-color={kandidat.status === 'Godkjent' ? 'success' : kandidat.status === 'Avvist' ? 'danger' : 'info'}>
          {kandidat.status}
        </Tag>
      </Paragraph>

      {/* Steg-indikator — samme Tag-rekke-idiom som StatusStepper (docs/09 §14), men egen, siden
        * StatusStepper er bundet til den 6-trinns entitets-statusmodellen. */}
      <div style={{ display: 'flex', gap: '0.4rem', flexWrap: 'wrap', alignItems: 'center', marginBottom: '1rem' }}>
        {STEG_TITLER.map((tittel, i) => (
          <Tag
            key={tittel}
            data-size="sm"
            data-color={i === steg ? 'accent' : 'neutral'}
            variant={i > steg ? 'outline' : 'default'}
          >
            {i + 1}. {tittel}
          </Tag>
        ))}
      </div>

      {ferdig ? (
        <Card style={{ padding: '1rem' }}>
          <Alert data-color="success" style={{ marginBottom: '0.75rem' }}>{ferdig.tittel}</Alert>
          {ferdig.advarsel && (
            <Alert data-color="warning" style={{ marginBottom: '0.75rem' }}>{ferdig.advarsel}</Alert>
          )}
          <Heading level={2} data-size="xs" style={{ marginBottom: '0.35rem' }}>Dette skjedde</Heading>
          <ul style={{ margin: '0 0 1rem 1.1rem', padding: 0 }}>
            {ferdig.detaljer.map((d) => (
              <li key={d} style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>{d}</li>
            ))}
          </ul>
          <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
            {ferdig.rettskildeLenke && (
              <Button data-size="sm" asChild>
                <RouterLink to={ferdig.rettskildeLenke}>Se i rettskilden ↗</RouterLink>
              </Button>
            )}
            {ferdig.virksomhetLenke && (
              <Button data-size="sm" variant="secondary" asChild>
                <RouterLink to={ferdig.virksomhetLenke}>Se virksomheten ↗</RouterLink>
              </Button>
            )}
            <Button data-size="sm" variant="tertiary" onClick={() => navigate('/navnekandidater')}>
              Tilbake til navnekandidater
            </Button>
          </div>
          {ferdig.rettskildeLenke && ferdig.taggLag && (
            <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginTop: '0.75rem', marginBottom: 0 }}>
              Taggen ligger i laget «{ferdig.taggLag}» i tagg-velgeren over lovteksten — velg det
              laget for å se markeringen.
            </Paragraph>
          )}
        </Card>
      ) : (
        <>
          {/* ---------------- Steg 0: Kontekst ---------------- */}
          <Card style={{ padding: '1rem', marginBottom: '1rem' }}>
            <Heading level={2} data-size="sm" style={{ marginBottom: '0.35rem' }}>1. Kontekst</Heading>
            <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
              Slik står treffet i rettskilden. Les setningen før du bestemmer deg — er treffet feil
              avgrenset, rettes teksten i neste steg.
            </Paragraph>

            <Table data-size="sm" style={{ marginBottom: '0.75rem', width: '100%' }}>
              <Table.Body>
                <Table.Row>
                  <Table.HeaderCell scope="row">Rettskilde</Table.HeaderCell>
                  <Table.Cell>
                    {rettskilde ? (
                      <Link asChild>
                        <RouterLink to={`/rettskilder/${kandidat.rettskildeId}?eid=${encodeURIComponent(kandidat.nodeEid)}`}>
                          {rettskilde.tittel} ↗
                        </RouterLink>
                      </Link>
                    ) : <Spinner aria-label="Laster …" data-size="xs" />}
                  </Table.Cell>
                </Table.Row>
                <Table.Row>
                  <Table.HeaderCell scope="row">Node</Table.HeaderCell>
                  <Table.Cell>
                    <span style={{ fontFamily: 'var(--ds-font-family-mono, monospace)', fontSize: 'var(--ds-font-size-1)' }}>
                      {kandidat.nodeEid}
                    </span>
                  </Table.Cell>
                </Table.Row>
                <Table.Row>
                  <Table.HeaderCell scope="row">Ansvarlig departement</Table.HeaderCell>
                  <Table.Cell>
                    {/* «Ikke kjent» er en PÅSTAND om rettskilden, og skal derfor ikke vises mens
                      * rettskilden fortsatt lastes — da er svaret ikke «ukjent», bare ikke kommet
                      * ennå. Samme spinner-mens-laster som Rettskilde-raden over. */}
                    {rettskilde === null
                      ? <Spinner aria-label="Laster …" data-size="xs" />
                      : rettskilde.ansvarligDepartement?.length
                        ? rettskilde.ansvarligDepartement.join(', ')
                        : <span style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Ikke kjent</span>}
                  </Table.Cell>
                </Table.Row>
                <Table.Row>
                  <Table.HeaderCell scope="row">Ekstern berikelse</Table.HeaderCell>
                  <Table.Cell><BerikelseVisning k={kandidat} /></Table.Cell>
                </Table.Row>
              </Table.Body>
            </Table>

            <Heading level={3} data-size="xs" style={{ marginBottom: '0.35rem' }}>Setningen i rettskilden</Heading>
            <Card style={{ padding: '0.75rem', background: 'var(--ds-color-neutral-surface-tinted)' }}>
              {node?.tekst && markering
                ? <TreffIKontekst tekst={node.tekst} start={markering.start} slutt={markering.slutt} />
                : noder === null
                  ? <Spinner aria-label="Laster nodeteksten …" data-size="sm" />
                  : (
                    <Paragraph data-size="sm" style={{ margin: 0 }}>
                      Fant ikke noden «{kandidat.nodeEid}» i rettskilden. Den kan være fjernet ved en
                      reimport — teksten kan derfor ikke vises, og det blir ikke opprettet noen tagg.
                    </Paragraph>
                  )}
            </Card>

            {steg === 0 && (
              <div style={{ display: 'flex', gap: '0.5rem', marginTop: '1rem' }}>
                <Button data-size="sm" onClick={() => setSteg(1)}>Neste</Button>
                <Button data-size="sm" variant="tertiary" onClick={() => navigate('/navnekandidater')}>Avbryt</Button>
              </div>
            )}
          </Card>

          {/* ---------------- Steg 1: Er teksten riktig? ---------------- */}
          {steg >= 1 && (
            <Card style={{ padding: '1rem', marginBottom: '1rem' }}>
              <Heading level={2} data-size="sm" style={{ marginBottom: '0.35rem' }}>2. Er teksten riktig?</Heading>
              <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
                Rett bare teksten hvis sveipet har tatt med tegn som ikke hører til navnet — f.eks.
                «Ø Suldal kommune», der Ø kommer fra en koordinat rett foran. Er navnet derimot
                LEGITIMT slik det står («Suldal», «Matilsynet», «Arkivverket»), la det stå: du
                forklarer det med en begrunnelse i stedet, senere i veiviseren.
              </Paragraph>
              <Textfield
                data-size="sm"
                label="Foreslått tekst"
                value={tekst}
                onChange={(e) => setTekst(e.target.value)}
                readOnly={steg !== 1}
                style={{ maxWidth: '28rem', marginBottom: '0.75rem' }}
              />
              {kandidat.status === 'Avvist' && steg === 1 && (
                <Alert data-color="info" data-size="sm" style={{ marginBottom: '0.75rem' }}>
                  Denne raden er avvist. Lagrer du en rettet tekst, settes den tilbake til «Venter»
                  slik at den kan behandles på nytt.
                </Alert>
              )}
              {steg === 1 && (
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <Button data-size="sm" onClick={lagreTekst} disabled={lagrerTekst || !tekst.trim()}>
                    {lagrerTekst ? 'Lagrer …' : tekst.trim() === kandidat.foreslattTekst ? 'Teksten er riktig — neste' : 'Lagre rettet tekst'}
                  </Button>
                  <Button data-size="sm" variant="tertiary" onClick={() => setSteg(0)}>Tilbake</Button>
                </div>
              )}
            </Card>
          )}

          {/* ---------------- Steg 2: Hva slags ting er dette? ---------------- */}
          {steg >= 2 && (
            <Card style={{ padding: '1rem', marginBottom: '1rem' }}>
              <Heading level={2} data-size="sm" style={{ marginBottom: '0.35rem' }}>3. Hva slags ting er dette?</Heading>
              <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
                «Administrativ inndeling» og «medlem av en eksisterende gruppe» er bevisst ikke med i
                denne runden — velg «Ikke relevant» hvis treffet er ett av dem, og ta det opp separat.
              </Paragraph>
              <Field data-size="sm" style={{ marginBottom: '0.75rem' }}>
                <Radio
                  name="slag"
                  label="Konkret virksomhet"
                  description="Ett navngitt organ/aktør, f.eks. Arkivverket eller Suldal kommune."
                  value="virksomhet"
                  checked={slag === 'virksomhet'}
                  onChange={() => setSlag('virksomhet')}
                  disabled={steg !== 2}
                />
                <Radio
                  name="slag"
                  label="Gruppe som defineres her"
                  description="En rolle/gruppe loven selv definerer, f.eks. «forurensningsmyndighet»."
                  value="gruppe"
                  checked={slag === 'gruppe'}
                  onChange={() => setSlag('gruppe')}
                  disabled={steg !== 2}
                />
                <Radio
                  name="slag"
                  label="Ikke relevant"
                  description="Ikke en aktør — eller noe denne runden ikke håndterer. Raden avvises."
                  value="irrelevant"
                  checked={slag === 'irrelevant'}
                  onChange={() => setSlag('irrelevant')}
                  disabled={steg !== 2}
                />
              </Field>
              {steg === 2 && (
                <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                  {slag === 'virksomhet' && (
                    <Button data-size="sm" onClick={() => setSteg(3)}>Neste</Button>
                  )}
                  {slag === 'gruppe' && (
                    <Button data-size="sm" onClick={() => fullførIkkeVirksomhet('gruppe')} disabled={fullfører}>
                      {fullfører ? 'Oppretter …' : 'Opprett gruppebegrep og godkjenn'}
                    </Button>
                  )}
                  {slag === 'irrelevant' && (
                    <Button data-size="sm" data-color="danger" onClick={() => fullførIkkeVirksomhet('irrelevant')} disabled={fullfører}>
                      {fullfører ? 'Avviser …' : 'Avvis kandidaten'}
                    </Button>
                  )}
                  <Button data-size="sm" variant="tertiary" onClick={() => setSteg(1)}>Tilbake</Button>
                </div>
              )}
            </Card>
          )}

          {/* ---------------- Steg 3: Hvilken virksomhet? ---------------- */}
          {steg >= 3 && (
            <Card style={{ padding: '1rem', marginBottom: '1rem' }}>
              <Heading level={2} data-size="sm" style={{ marginBottom: '0.35rem' }}>4. Hvilken virksomhet?</Heading>
              <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
                Finn virksomheten i katalogen, eller opprett den — fra Brønnøysundregisteret hvis den
                er registrert der, ellers med bare navnet.
              </Paragraph>

              <Field data-size="sm" style={{ marginBottom: '0.75rem' }}>
                <Radio name="vei" label="Velg fra katalogen" value="eksisterende"
                  checked={vei === 'eksisterende'} onChange={() => setVei('eksisterende')} disabled={steg !== 3} />
                <Radio name="vei" label="Søk i Brønnøysundregisteret og opprett" value="brreg"
                  checked={vei === 'brreg'} onChange={() => setVei('brreg')} disabled={steg !== 3} />
                <Radio name="vei" label="Opprett med bare navn" value="kunNavn"
                  description="For aktører uten egen Brreg-registrering, f.eks. Kystvakten som del av Forsvaret."
                  checked={vei === 'kunNavn'} onChange={() => setVei('kunNavn')} disabled={steg !== 3} />
              </Field>

              {vei === 'eksisterende' && (
                <VirksomhetVelger
                  virksomheter={virksomheter}
                  value={valgtVirksomhetId}
                  onChange={setValgtVirksomhetId}
                  label="Virksomhet"
                  tomValgTekst="Velg virksomhet …"
                  style={{ marginBottom: '0.75rem', maxWidth: '28rem' }}
                />
              )}

              {vei === 'brreg' && steg === 3 && (
                <div style={{ marginBottom: '0.75rem' }}>
                  <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginBottom: '0.5rem', maxWidth: '28rem' }}>
                    <Search data-size="sm" style={{ flex: 1 }}>
                      <Search.Input
                        aria-label="Søk i Brønnøysundregisteret"
                        value={brregSok}
                        onChange={(e) => setBrregSok(e.target.value)}
                        onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); void søkBrreg(); } }}
                      />
                      <Search.Clear />
                    </Search>
                    <Button data-size="sm" onClick={søkBrreg} disabled={brregSoker || !brregSok.trim()}>
                      {brregSoker ? 'Søker …' : 'Søk'}
                    </Button>
                  </div>
                  {/* Kortet rendres ALLTID når et søk er gjort — tom-tilstand som Paragraph inni (docs/09 §14). */}
                  {brregTreff !== null && (
                    <Card style={{ padding: brregTreff.length > 0 ? 0 : '1rem', overflow: 'hidden' }}>
                      {brregTreff.length === 0 ? (
                        <Paragraph data-size="sm" style={{ margin: 0 }}>Ingen treff i Brreg på «{brregSok}».</Paragraph>
                      ) : (
                        <Table data-size="sm" data-density="compact" style={{ width: '100%' }}>
                          <Table.Head>
                            <Table.Row>
                              <Table.HeaderCell>Navn</Table.HeaderCell>
                              <Table.HeaderCell>Org.nr.</Table.HeaderCell>
                              <Table.HeaderCell>Form</Table.HeaderCell>
                              <Table.HeaderCell />
                            </Table.Row>
                          </Table.Head>
                          <Table.Body>
                            {brregTreff.map((e) => (
                              <Table.Row key={e.organisasjonsnummer}>
                                <Table.Cell>{e.navn}</Table.Cell>
                                <Table.Cell>{e.organisasjonsnummer}</Table.Cell>
                                <Table.Cell>{e.organisasjonsformBeskrivelse ?? e.organisasjonsformKode ?? '—'}</Table.Cell>
                                <Table.Cell>
                                  <Button
                                    data-size="sm"
                                    variant="secondary"
                                    onClick={() => opprettOgVelg('brreg', e.organisasjonsnummer)}
                                    disabled={oppretterVirksomhet}
                                  >
                                    Opprett og velg
                                  </Button>
                                </Table.Cell>
                              </Table.Row>
                            ))}
                          </Table.Body>
                        </Table>
                      )}
                    </Card>
                  )}
                </div>
              )}

              {vei === 'kunNavn' && steg === 3 && (
                <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', marginBottom: '0.75rem', flexWrap: 'wrap' }}>
                  <Textfield
                    data-size="sm"
                    label="Navn på virksomheten"
                    value={nyttNavn}
                    onChange={(e) => setNyttNavn(e.target.value)}
                  />
                  <Button data-size="sm" variant="secondary" onClick={() => opprettOgVelg('kunNavn')} disabled={oppretterVirksomhet || !nyttNavn.trim()}>
                    {oppretterVirksomhet ? 'Oppretter …' : 'Opprett og velg'}
                  </Button>
                </div>
              )}

              {valgtVirksomhet && (
                <Alert data-color="info" data-size="sm" style={{ marginBottom: '0.75rem' }}>
                  Valgt virksomhet: <strong>{valgtVirksomhet.navn}</strong>
                  {valgtVirksomhet.organisasjonsnummer ? ` (org.nr. ${valgtVirksomhet.organisasjonsnummer})` : ''}
                </Alert>
              )}

              <Divider style={{ margin: '0.75rem 0' }} />

              <Heading level={3} data-size="xs" style={{ marginBottom: '0.35rem' }}>
                Hvorfor peker «{kandidat.foreslattTekst}» på denne virksomheten?
              </Heading>
              <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.5rem' }}>
                Dette er stedet for legitime strenger som ikke er det offisielle navnet — et utgått
                navn, en kortform, eller en skrivefeil i kildeteksten. Kan stå tom.
              </Paragraph>
              <NavneformgrunnVelger
                value={navneformgrunn}
                onChange={setNavneformgrunn}
                label="Begrunnelse"
                style={{ marginBottom: '0.75rem', maxWidth: '28rem' }}
              />

              {steg === 3 && (
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                  <Button data-size="sm" onClick={() => setSteg(4)} disabled={!valgtVirksomhetId}>Neste</Button>
                  <Button data-size="sm" variant="tertiary" onClick={() => setSteg(2)}>Tilbake</Button>
                </div>
              )}
            </Card>
          )}

          {/* ---------------- Steg 4: Bekreft ---------------- */}
          {steg >= 4 && (
            <Card style={{ padding: '1rem', marginBottom: '1rem' }}>
              <Heading level={2} data-size="sm" style={{ marginBottom: '0.35rem' }}>5. Bekreft</Heading>
              <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
                Dette blir opprettet eller endret når du fullfører:
              </Paragraph>
              <Table data-size="sm" style={{ marginBottom: '1rem', width: '100%' }}>
                <Table.Body>
                  <Table.Row>
                    <Table.HeaderCell scope="row">Navneform</Table.HeaderCell>
                    <Table.Cell>«{kandidat.foreslattTekst}»</Table.Cell>
                  </Table.Row>
                  <Table.Row>
                    <Table.HeaderCell scope="row">Peker på virksomhet</Table.HeaderCell>
                    <Table.Cell>{valgtVirksomhet?.navn ?? '—'}</Table.Cell>
                  </Table.Row>
                  <Table.Row>
                    <Table.HeaderCell scope="row">Begrunnelse</Table.HeaderCell>
                    <Table.Cell>{navneformgrunn ?? 'Uspesifisert'}</Table.Cell>
                  </Table.Row>
                  <Table.Row>
                    <Table.HeaderCell scope="row">Navnekandidat</Table.HeaderCell>
                    <Table.Cell>Settes til «Godkjent»</Table.Cell>
                  </Table.Row>
                  <Table.Row>
                    <Table.HeaderCell scope="row">Tagg i rettskilden</Table.HeaderCell>
                    <Table.Cell>
                      Kobles til virksomheten, i laget «Virksomhet». En eventuell ubundet tagg på
                      samme sted gjenbrukes i stedet for at en ny opprettes.
                    </Table.Cell>
                  </Table.Row>
                </Table.Body>
              </Table>
              <div style={{ display: 'flex', gap: '0.5rem' }}>
                <Button data-size="sm" onClick={fullførVirksomhet} disabled={fullfører || !valgtVirksomhetId}>
                  {fullfører ? 'Fullfører …' : 'Fullfør'}
                </Button>
                <Button data-size="sm" variant="tertiary" onClick={() => setSteg(3)}>Tilbake</Button>
              </div>
            </Card>
          )}
        </>
      )}

      {feil && <Alert data-color="danger" style={{ marginTop: '0.75rem' }}>{feil}</Alert>}
    </>
  );
}
