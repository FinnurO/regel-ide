import { useEffect, useState, type FormEvent } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router';
import { Alert, Button, Card, Dialog, Field, Heading, Label, Link, Paragraph, Select, Spinner, Table, Tabs, Tag, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenkeForId } from '../api/eidLenker';
import type { Aktortype, KodelisteDto, Navneformgrunn, RettskildeNodeDto, RettskildeSammendrag, StrukturkantDto, VirksomhetKandidatDto, VirksomhetSlettOversiktDto, VirksomhetsbegrepDto, VirksomhetWhereUsedDto } from '../api/types';
import { NavneformgrunnTag, NavneformgrunnVelger } from '../virksomhet/Navneformgrunn';
import { useVirksomheter } from '../virksomhet/useVirksomheter';
import { LeggTilMyndighetstildelingForm } from '../virksomhet/LeggTilMyndighetstildelingForm';
import { StrukturkantTabell } from '../strukturkant/StrukturkantTabell';
import { paragrafEtikett } from '../rettskilde/paragrafEtikett';
import { LeggTilVirksomhetRelasjonForm } from '../virksomhet/LeggTilVirksomhetRelasjonForm';
import { Metatekst } from '../entitet/Metatekst';
import { AktortypeTag, AktortypeVelger } from '../begrep/Nodetype';

/** [Ny, issue #157] Rad-etiketter for bekreftelsesdialogen — KUN de feltene som faktisk kan være > 0
 * for en reell virksomhet vises (0-rader skjules, se `SlettVirksomhetSeksjon` under). Rekkefølgen her
 * er visningsrekkefølgen. */
const SLETT_OVERSIKT_ETIKETTER: [key: keyof VirksomhetSlettOversiktDto, etikett: string][] = [
  ['tjenester', 'Tjenester'],
  ['rettskilder', 'Egne (lokale) rettskilder'],
  ['begreper', 'Begreper (arbeidsprodukt)'],
  ['navneformer', 'Navneformer'],
  ['brukere', 'Brukere'],
  // [ENDRET, issue #311] Var «Myndighetstildelinger» + «Relasjoner til andre virksomheter».
  ['strukturkanter', 'Strukturutsagn (relasjoner, tilhørigheter, kompetanse m.m.)'],
  ['virksomhetKandidater', 'Navnekandidater i kø'],
  ['virksomhetNettsider', 'Nettsider'],
  ['kodelister', 'Kodelister'],
  ['datasett', 'Datasett'],
  ['vilkar', 'Vilkår'],
  ['regelnoder', 'Regelnoder'],
  ['unntak', 'Unntak'],
  ['vilkarstreKommentarer', 'Vilkårstre-kommentarer'],
  ['tekstTagger', 'Tekst-tagger'],
  ['hendelser', 'Hendelser'],
  ['kunnskapsbibliotekLenker', 'Kunnskapsbibliotek-lenker'],
  ['kunnskapsbibliotekFiler', 'Kunnskapsbibliotek-filer'],
];

/**
 * [Ny, issue #268, 2026-09-11] Fanegruppering — de tidligere 8 flate seksjonene var dobbelt så mange
 * som docs/30 §3.1 pkt. 3 sin egen "faner ved >~4 seksjoner"-grense. Gruppert etter hva seksjonene
 * FAKTISK svarer på (docs/32 §3 S1–S7), ikke bare for å redusere antallet — "Myndighet & relasjoner"
 * og "Rettskilder" er to ulike spørsmål (hvem styrer virksomheten vs. hvilke rettskilder den selv har
 * fastsatt), holdt adskilt. Se docs/design-canvas/VirksomhetDetalj.dc.html.
 */
type Fane = 'grunndata' | 'navneformer' | 'myndighet' | 'rettskilder' | 'kandidater' | 'farligSone';

export default function VirksomhetDetalj() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { virksomheter, virksomheterPerId, laster: virksomheterLaster } = useVirksomheter();
  const [fane, setFane] = useState<Fane>('grunndata');

  const [begrep, setBegrep] = useState<VirksomhetsbegrepDto[] | null>(null);
  // [ENDRET, issue #311] ÉN liste med alle strukturkantene virksomheten står i (var tildelinger + relasjoner
  // fra to tabeller). Seksjonene under filtrerer den på kategori.
  const [kanter, setKanter] = useState<StrukturkantDto[] | null>(null);
  const [kandidater, setKandidater] = useState<VirksomhetKandidatDto[] | null>(null);
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [visLeggTilTildeling, setVisLeggTilTildeling] = useState(false);
  const [visLeggTilRelasjon, setVisLeggTilRelasjon] = useState(false);
  // Departement-virksomhet-lenke (2026-08-30) — ikke betinget på noen egen "er departement"-boolsk,
  // se oppgavebeskrivelsen: lastes for ENHVER virksomhet, seksjonen skjules bare når listen er tom.
  const [rettskilderAnsvarligFor, setRettskilderAnsvarligFor] = useState<RettskildeSammendrag[] | null>(null);
  // [Ny, fastsatt-av-runden, 2026-09-10, issue #215] Et ANNET spørsmål enn ansvarligFor over.
  const [rettskilderFastsattAv, setRettskilderFastsattAv] = useState<RettskildeSammendrag[] | null>(null);
  // [Ny, navneform-kjede-runden, 2026-09-08] «Where used» — ETT kall som dekker ALLE navneformene
  // (hvor de er tagget) OG hvilket gruppebegrep hver myndighetstildeling gjelder. Se
  // VirksomhetWhereUsedTjeneste for hvorfor det er ett samlet oppslag og ikke ett per navneform.
  const [whereUsed, setWhereUsed] = useState<VirksomhetWhereUsedDto | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  const [nyTerm, setNyTerm] = useState('');
  // [Ny, navneformgrunn-runden, 2026-09-07] Default null (uspesifisert) — se
  // NavneformgrunnVelger sin kommentar for hvorfor ingen verdi forhåndsvelges.
  const [nyNavneformgrunn, setNyNavneformgrunn] = useState<Navneformgrunn | null>(null);
  const [leggerTil, setLeggerTil] = useState(false);
  const [leggTilFeil, setLeggTilFeil] = useState<string | null>(null);

  // [Ny, 2026-09-09, issue #135] Sletting av én navneform. Egen dialog framfor en rå knapp: å
  // slette en navneform sletter OGSÅ tekst-taggene som peker på den (se
  // VirksomhetsbegrepTjeneste.SlettVirksomhetsbegrepAsync — en tagg mot en navneform som ikke finnes
  // lenger er en markering i lovteksten som ikke kan følges noe sted). Saksbehandleren skal se HVOR
  // MANGE forekomster som forsvinner FØR hun bekrefter, samme prinsipp som slett-virksomhet-dialogen
  // nederst på siden: «du får se nøyaktig hva som rammes før du bekrefter».
  const [navneformTilSletting, setNavneformTilSletting] = useState<VirksomhetsbegrepDto | null>(null);
  const [sletterNavneform, setSletterNavneform] = useState(false);
  const [slettNavneformFeil, setSlettNavneformFeil] = useState<string | null>(null);

  async function bekreftSlettNavneform() {
    if (!navneformTilSletting) return;
    setSletterNavneform(true);
    setSlettNavneformFeil(null);
    try {
      await api.slettVirksomhetsbegrep(navneformTilSletting.id);
      setNavneformTilSletting(null);
      lastAlt(); // navneformer, «Brukt i» og whereUsed må alle hentes på nytt.
    } catch (e) {
      setSlettNavneformFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved sletting av navneform.');
    } finally {
      setSletterNavneform(false);
    }
  }

  const [sveiper, setSveiper] = useState(false);
  const [sveipFeil, setSveipFeil] = useState<string | null>(null);
  const [sveipResultat, setSveipResultat] = useState<{ funnet: number; nye: number } | null>(null);

  const [forvaltningsnivaKodeliste, setForvaltningsnivaKodeliste] = useState<KodelisteDto | null>(null);
  const [forvaltningsnivaLagres, setForvaltningsnivaLagres] = useState(false);
  const [forvaltningsnivaFeil, setForvaltningsnivaFeil] = useState<string | null>(null);
  // useVirksomheter() henter og cacher ÉN gang per bruk — den har ingen "hent på nytt"-funksjon
  // (ville krevd å endre en delt hook brukt mange steder). Lagrer derfor den nyeste verdien lokalt her
  // og lar den overstyre hook-verdien i visningen under, i stedet for å endre den delte hooken.
  const [forvaltningsnivaOverstyrt, setForvaltningsnivaOverstyrt] = useState<string | null | undefined>(undefined);
  // [Ny, issue #310 «nodetype-akse»] Aktørtype — samme «lokal overstyring av hook-verdien»-mønster som
  // forvaltningsnivået rett over, av samme grunn (useVirksomheter har ingen «hent på nytt»).
  const [aktortypeOverstyrt, setAktortypeOverstyrt] = useState<Aktortype | null | undefined>(undefined);
  const [aktortypeLagres, setAktortypeLagres] = useState(false);
  const [aktortypeFeil, setAktortypeFeil] = useState<string | null>(null);

  // [Ny, 2026-09-02, issue #115] Node-tekst per rettskilde — samme lazy-per-rettskilde-mønster som
  // VirksomhetKandidaterListe.tsx/LeggTilMyndighetstildelingForm.tsx, slik at "Paragrafspenn"- og
  // "Node"-kolonnene under kan vise "§ nummer — overskrift" i stedet for rå eId.
  const [noderPerRettskilde, setNoderPerRettskilde] = useState<Map<string, RettskildeNodeDto[]>>(new Map());
  function sikreNoderFor(rettskildeId: string) {
    if (!rettskildeId || noderPerRettskilde.has(rettskildeId)) return;
    api.hentNoder(rettskildeId)
      .then((noder) => setNoderPerRettskilde((forrige) => new Map(forrige).set(rettskildeId, noder)))
      .catch(() => {}); // ingen gjettet fallback — viser rå eId når nodene ikke lot seg hente
  }
  // [ENDRET, nemnd/sekretariat-runden, 2026-09-09] Går via `paragrafEtikett`, som klatrer opp til
  // PARAGRAFEN. To feil ble rettet med det: en eId som peker på et ledd viste leddnummeret som
  // paragraf («§ 1» for en tagg i konkurranseloven § 35 første ledd), og en eId som peker på
  // paragrafnoden selv ga «§ § 36», siden Lovdata-importen alt har «§» i paragrafnodens nummer.
  function visNodeKort(rettskildeId: string, eid: string): string {
    const noder = noderPerRettskilde.get(rettskildeId);
    const node = noder?.find((n) => n.eid === eid);
    if (!node) return eid;
    if (node.nodeType === 'side') return 'Hele siden';
    const etikett = paragrafEtikett(noder, eid);
    if (etikett) return etikett.overskrift ? `${etikett.tekst} — ${etikett.overskrift}` : etikett.tekst;
    return node.nummer ?? eid;
  }

  // [FJERNET, issue #311] hjemmelEtikett — hjemmelvisningen for kanter bor nå i StrukturkantTabell.

  function lastAlt() {
    if (!id) return;
    api.hentVirksomhetsbegrep(id).then(setBegrep)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av begrep.'));
    api.hentStrukturkanter({ virksomhetId: id }).then(setKanter).catch(() => setKanter([]));
    api.hentVentendeKandidater(id).then(setKandidater).catch(() => setKandidater([]));
    api.hentRettskilderAnsvarligFor(id).then(setRettskilderAnsvarligFor).catch(() => setRettskilderAnsvarligFor([]));
    api.hentRettskilderFastsattAv(id).then(setRettskilderFastsattAv).catch(() => setRettskilderFastsattAv([]));
    // Tom-ved-feil, samme mønster som de andre valgfrie seksjonene over: en virksomhet uten
    // koblinger er et helt normalt svar, ikke en feil som fortjener en banner.
    api.hentVirksomhetWhereUsed(id).then(setWhereUsed)
      .catch(() => setWhereUsed({ navneformForekomster: [], gruppetildelinger: [] }));
  }

  useEffect(lastAlt, [id]);
  // [FJERNET, issue #311] Nodehenting for tildelingenes/relasjonenes hjemler — StrukturkantTabell gjør det selv.
  // [Ny, navneform-kjede-runden, 2026-09-08] Nodene for rettskildene navneformene er TAGGET i, slik
  // at «Brukt i»-kolonnen kan vise «§ 1 — overskrift» i stedet for en rå eId — samme lazy-per-
  // rettskilde-mønster som tildelinger over.
  useEffect(() => {
    for (const rettskildeId of new Set((whereUsed?.navneformForekomster ?? []).map((f) => f.rettskildeId))) {
      sikreNoderFor(rettskildeId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [whereUsed]);
  useEffect(() => {
    for (const rettskildeId of new Set((kandidater ?? []).map((k) => k.rettskildeId))) sikreNoderFor(rettskildeId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [kandidater]);
  useEffect(() => {
    api.hentKodelister()
      .then((liste) => setForvaltningsnivaKodeliste(liste.find((k) => k.kode === 'KL-FORVALTNINGSNIVA') ?? null))
      .catch(() => setForvaltningsnivaKodeliste(null));
    api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([]));
  }, []);

  async function endreForvaltningsniva(verdi: string) {
    if (!id) return;
    setForvaltningsnivaFeil(null);
    setForvaltningsnivaLagres(true);
    try {
      const oppdatert = await api.settVirksomhetForvaltningsniva(id, verdi === '' ? null : verdi);
      setForvaltningsnivaOverstyrt(oppdatert.forvaltningsniva);
    } catch (err) {
      setForvaltningsnivaFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved endring av forvaltningsnivå.');
    } finally {
      setForvaltningsnivaLagres(false);
    }
  }

  async function endreAktortype(verdi: Aktortype | null) {
    if (!id) return;
    setAktortypeFeil(null);
    setAktortypeLagres(true);
    try {
      const oppdatert = await api.settVirksomhetAktortype(id, verdi);
      setAktortypeOverstyrt(oppdatert.aktortype);
    } catch (err) {
      setAktortypeFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved endring av aktørtype.');
    } finally {
      setAktortypeLagres(false);
    }
  }

  async function leggTilBegrep(e: FormEvent) {
    e.preventDefault();
    if (!id || !nyTerm.trim()) return;
    setLeggTilFeil(null);
    setLeggerTil(true);
    try {
      await api.opprettVirksomhetsbegrep({
        virksomhetId: id, term: nyTerm.trim(), skosUrl: null, navneformgrunn: nyNavneformgrunn,
      });
      setNyTerm('');
      setNyNavneformgrunn(null);
      lastAlt();
    } catch (err) {
      setLeggTilFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av navneform.');
    } finally {
      setLeggerTil(false);
    }
  }

  async function kjorSveip() {
    if (!id) return;
    setSveiper(true);
    setSveipFeil(null);
    setSveipResultat(null);
    try {
      const resultat = await api.sveipVirksomhetKandidater({ virksomhetId: id });
      setSveipResultat({ funnet: resultat.antallTreffFunnet, nye: resultat.antallNyeKandidater });
      lastAlt();
    } catch (err) {
      setSveipFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved sveip.');
    } finally {
      setSveiper(false);
    }
  }

  if (virksomheterLaster) return <Spinner aria-label="Laster …" data-size="sm" />;
  const virksomhet = id ? virksomheterPerId.get(id) : undefined;
  if (!virksomhet) return <Alert data-color="danger">Fant ingen virksomhet med id «{id}».</Alert>;

  const forvaltningsniva = forvaltningsnivaOverstyrt === undefined ? virksomhet.forvaltningsniva : forvaltningsnivaOverstyrt;
  const aktortype = aktortypeOverstyrt === undefined ? virksomhet.aktortype : aktortypeOverstyrt;

  return (
    <>
      <Metatekst as="nav" aria-label="Brødsmulesti" style={{ display: 'flex', gap: '0.4rem', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.6rem', flexWrap: 'wrap' }}>
        <Link asChild><RouterLink to="/virksomheter">Virksomheter</RouterLink></Link>
        <span>/</span>
        <span style={{ color: 'var(--ds-color-neutral-text-default)' }}>{virksomhet.visningsnavn}</span>
      </Metatekst>

      {/* [ENDRET, registernavn-runden, 2026-09-08] visningsnavn i brødsmule og H1; registerets egen
          form står i grunndata-tabellen under («Registrert navn (Brreg)»). Se VirksomhetDto i
          types.ts for hvorfor de to er forskjellige. */}
      <Heading level={1} data-size="lg" style={{ marginBottom: '0.2rem' }}>
        {virksomhet.visningsnavn}
      </Heading>
      <Paragraph style={{ marginBottom: '0.75rem', display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
        <Tag data-color={forvaltningsniva ? 'info' : 'neutral'} data-size="sm">
          {forvaltningsniva ?? 'Forvaltningsnivå ikke satt'}
        </Tag>
        <Tag data-color={virksomhet.aktiv ? 'success' : 'neutral'} data-size="sm">
          {virksomhet.aktiv ? 'Aktiv' : 'Sovende'}
        </Tag>
        {/* [Ny, issue #310] Aktørtypen (docs/33 §4.1). «Uavklart» vises eksplisitt her — på
          * detaljsiden er fraværet selve opplysningen (samme valg som NavneformgrunnTag visUspesifisert). */}
        <AktortypeTag aktortype={aktortype} visUavklart />
      </Paragraph>

      {feil && <Alert data-color="danger" style={{ marginBottom: '1rem' }}>{feil}</Alert>}

      <Tabs value={fane} onChange={(v) => setFane(v as Fane)} style={{ marginBottom: '1rem' }}>
        <Tabs.List>
          <Tabs.Tab value="grunndata">Grunndata</Tabs.Tab>
          <Tabs.Tab value="navneformer">Navneformer</Tabs.Tab>
          <Tabs.Tab value="myndighet">Myndighet &amp; relasjoner</Tabs.Tab>
          <Tabs.Tab value="rettskilder">Rettskilder</Tabs.Tab>
          <Tabs.Tab value="kandidater">Kandidater</Tabs.Tab>
          <Tabs.Tab value="farligSone">Farlig sone</Tabs.Tab>
        </Tabs.List>
      </Tabs>

      {fane === 'grunndata' && (
      <section style={{ marginBottom: '2rem' }}>
        <Card style={{ padding: '1rem' }}>
          <Table>
            <Table.Body>
              {/* [Ny, registernavn-runden, 2026-09-08] Registerets egen form, vist ORDRETT. Vises
                  bare når den skiller seg fra visningsnavnet — for de fleste radene er de like, og en
                  rad som gjentar overskriften ville vært ren støy. Ikke monospace: dette er prosa,
                  ikke en kode (docs/09 §0 — monospace kun for kode/eId/organisasjonsnummer). */}
              {virksomhet.navn !== virksomhet.visningsnavn && (
                <Table.Row>
                  <Table.HeaderCell>Registrert navn (Brreg)</Table.HeaderCell>
                  <Table.Cell>
                    {virksomhet.navn}
                    <Metatekst as="span" style={{ display: 'block', color: 'var(--ds-color-neutral-text-subtle)' }}>
                      Beholdes i registerets egen form (issue #158). Navnet som vises ellers i appen er
                      virksomhetens gjeldende navneform.
                    </Metatekst>
                  </Table.Cell>
                </Table.Row>
              )}
              <Table.Row>
                <Table.HeaderCell>Organisasjonsnummer</Table.HeaderCell>
                <Table.Cell style={{ fontFamily: 'monospace' }}>{virksomhet.organisasjonsnummer ?? '—'}</Table.Cell>
              </Table.Row>
              <Table.Row>
                <Table.HeaderCell>Forvaltningsnivå</Table.HeaderCell>
                <Table.Cell>
                  <Field style={{ maxWidth: '16rem' }}>
                    <Label style={{ display: 'none' }}>Forvaltningsnivå</Label>
                    <Select data-size="sm" value={forvaltningsniva ?? ''} disabled={forvaltningsnivaLagres}
                      onChange={(e) => endreForvaltningsniva(e.target.value)}>
                      <Select.Option value="">Ikke satt</Select.Option>
                      {forvaltningsnivaKodeliste?.koder.map((k) => (
                        <Select.Option key={k.kode} value={k.kode}>{k.term}</Select.Option>
                      ))}
                    </Select>
                  </Field>
                  {forvaltningsnivaFeil && <Alert data-color="danger" style={{ marginTop: '0.25rem' }}>{forvaltningsnivaFeil}</Alert>}
                </Table.Cell>
              </Table.Row>
              {/* [Ny, issue #310 «nodetype-akse», docs/33 §4.1] Automatisk utfylt bare for KOMM/FYLK
                * (rettssubjekt); alt annet setter et menneske her. */}
              <Table.Row>
                <Table.HeaderCell>Aktørtype</Table.HeaderCell>
                <Table.Cell>
                  <AktortypeVelger value={aktortype} onChange={endreAktortype} disabled={aktortypeLagres} />
                  {aktortypeFeil && <Alert data-color="danger" style={{ marginTop: '0.25rem' }}>{aktortypeFeil}</Alert>}
                </Table.Cell>
              </Table.Row>
              <Table.Row>
                <Table.HeaderCell>Organisasjonsform (Brreg)</Table.HeaderCell>
                <Table.Cell>{virksomhet.organisasjonsformKode ?? '—'}</Table.Cell>
              </Table.Row>
              <Table.Row>
                <Table.HeaderCell>Sektorkode (Brreg)</Table.HeaderCell>
                <Table.Cell>{virksomhet.sektorkode ?? '—'}</Table.Cell>
              </Table.Row>
              <Table.Row>
                <Table.HeaderCell>Overordnet enhet</Table.HeaderCell>
                <Table.Cell>
                  {virksomhet.overordnetEnhetId
                    ? virksomheterPerId.get(virksomhet.overordnetEnhetId)?.visningsnavn ?? virksomhet.overordnetEnhetId
                    : '—'}
                </Table.Cell>
              </Table.Row>
              <Table.Row>
                <Table.HeaderCell>Sist synkronisert mot Brreg</Table.HeaderCell>
                <Table.Cell>{virksomhet.sistBrregSynkronisert ?? 'Aldri (kun seedet)'}</Table.Cell>
              </Table.Row>
            </Table.Body>
          </Table>
        </Card>
      </section>
      )}

      {fane === 'myndighet' && (
      <section style={{ marginBottom: '2rem' }}>
        <Heading level={3} data-size="xs" style={{ marginBottom: '0.75rem' }}>
          Relasjoner til andre virksomheter
        </Heading>
        <Metatekst style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
          Navngitte relasjoner til BESTEMTE, konkrete virksomheter (f.eks. «er klageinstans for», «er sekretariat for»)
          — til forskjell fra «Overordnet enhet» i Grunndata over, som er automatisk Brreg-avledet uten
          hjemmel. Listen viser relasjoner i BEGGE retninger fra denne virksomhetens ståsted — samme rad
          kan altså vises med ulik tekst på motpartens side.
        </Metatekst>
        {/* [ENDRET, issue #311] R-kantene, med polaritet/avgrensning/kilde utenfor korpus (docs/33 §4.3). */}
        <StrukturkantTabell
          kanter={kanter && kanter.filter((k) => k.kategori === 'R')}
          tomTekst="Ingen relasjoner registrert."
        />
        <Button data-size="sm" variant="secondary" onClick={() => setVisLeggTilRelasjon((v) => !v)}>
          {visLeggTilRelasjon ? 'Skjul skjema' : 'Legg til relasjon'}
        </Button>
        {visLeggTilRelasjon && id && (
          <LeggTilVirksomhetRelasjonForm
            virksomhetId={id}
            virksomheter={virksomheter}
            rettskilder={rettskilder}
            onOpprettet={() => {
              api.hentStrukturkanter({ virksomhetId: id }).then(setKanter).catch(() => {});
              setVisLeggTilRelasjon(false);
            }}
          />
        )}
      </section>
      )}

      {fane === 'navneformer' && (
      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Navneformer i rettskildetekst
        </Heading>
        <Metatekst style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
          Alle navneformer under peker på samme virksomhet — synonymer (f.eks. «Fylkesmann»/«Statsforvalter») er bare flere rader, ingen egen mekanisme.
          «Brukt i» viser hvor navneformen faktisk er tagget i en rettskildetekst, med lenke til paragrafen.
        </Metatekst>
        <Card style={{ padding: begrep && begrep.length > 0 ? 0 : '1rem', overflow: 'hidden', marginBottom: '0.75rem' }}>
          {!begrep && <Spinner aria-label="Laster …" data-size="sm" />}
          {begrep && begrep.length === 0 && <Paragraph style={{ margin: 0 }}>Ingen navneformer registrert ennå.</Paragraph>}
          {begrep && begrep.length > 0 && (
            <Table data-density="compact">
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell>Navneform</Table.HeaderCell>
                  <Table.HeaderCell>Grunn</Table.HeaderCell>
                  <Table.HeaderCell>Kilde</Table.HeaderCell>
                  <Table.HeaderCell>Brukt i</Table.HeaderCell>
                  <Table.HeaderCell>Handling</Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {begrep.map((b) => (
                  <Table.Row key={b.id}>
                    <Table.Cell>{b.term}</Table.Cell>
                    {/* [Ny, navneformgrunn-runden, 2026-09-07] Grunnen til at navneformen peker hit.
                      * En utgått/feilskrevet navneform skal ALDRI se ut som det offisielle navnet —
                      * se Navneformgrunn.tsx for fargevalget. Tom celle for uspesifisert grunn
                      * (de fleste eksisterende radene), bevisst i stedet for «Uspesifisert»-støy. */}
                    <Table.Cell><NavneformgrunnTag grunn={b.navneformgrunn} /></Table.Cell>
                    <Table.Cell>
                      {/* [Ny, issue #194] Samme SNL-lenke-mønster som NavnekandidaterListe.tsx sin
                       * BerikelseVisning — saksbehandler skal kunne åpne og selv verifisere
                       * SNL-artikkelen bak en auto-opprettet navneform (Brreg-import ELLER manuell
                       * "kun navn"-opprettelse, begge går nå gjennom samme SNL-oppslag). */}
                      {b.skosUrl && (
                        <Link href={b.skosUrl} target="_blank" rel="noopener noreferrer" data-size="sm">
                          <Tag data-color="success" data-size="sm">SNL ↗</Tag>
                        </Link>
                      )}
                    </Table.Cell>
                    {/* [Ny, navneform-kjede-runden, 2026-09-08] «Where used» per navneform — svarer
                      * på Johanns «jeg hadde forventet at man ser koblinger fra Virksomheten» og
                      * «at det er rettskildekoblinger til navneformene». Lenken går til nøyaktig
                      * NODEN (?eid=…), ikke bare til rettskilden — samme «hjemmel per RAD»-prinsipp
                      * som docs/09 §16 låser for gruppemedlemskap: en navneform kan være tagget i
                      * flere paragrafer, og da er en lenke til dokumentet for grov.
                      *
                      * `null` = laster ⇒ Spinner. «Ikke brukt ennå» skal ALDRI vises mens data
                      * fortsatt lastes (docs/09 §15) — da er svaret ikke tomt, bare ikke kommet. */}
                    <Metatekst as={Table.Cell}>
                      {!whereUsed && <Spinner aria-label="Laster …" data-size="xs" />}
                      {whereUsed && (() => {
                        const forekomster = whereUsed.navneformForekomster.filter((f) => f.navneformId === b.id);
                        if (forekomster.length === 0) {
                          return <span style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Ikke tagget i noen rettskildetekst</span>;
                        }
                        return (
                          <ul style={{ margin: 0, paddingLeft: '1rem' }}>
                            {/* Offsetene MÅ med i nøkkelen under: samme navneform kan være tagget to
                              * steder i samme ledd, og (rettskilde, node) alene ga da to rader med
                              * IDENTISK key — React advarte, og radene kunne bli duplisert eller
                              * utelatt. Se `startOffset` i types.ts. */}
                            {forekomster.map((f) => (
                              <li key={`${f.rettskildeId}-${f.nodeEid}-${f.startOffset}`}>
                                <Link asChild>
                                  <RouterLink to={rettskildeLenkeForId(f.rettskildeId, f.nodeEid)}>
                                    {f.rettskildeTittel} — {visNodeKort(f.rettskildeId, f.nodeEid)}
                                  </RouterLink>
                                </Link>
                              </li>
                            ))}
                          </ul>
                        );
                      })()}
                    </Metatekst>
                    {/* [Ny, 2026-09-09, issue #135] Fjern navneform. Åpner en dialog framfor å slette
                      * direkte: sletting tar med tekst-taggene som peker på navneformen, og antallet
                      * står i «Brukt i»-kolonnen rett til venstre — men det skal SIES, ikke leses ut
                      * av en tabell. Samme «du får se hva som rammes»-prinsipp som
                      * slett-virksomhet-dialogen nederst på siden. */}
                    <Table.Cell>
                      <Button
                        data-size="sm"
                        variant="tertiary"
                        data-color="danger"
                        onClick={() => { setSlettNavneformFeil(null); setNavneformTilSletting(b); }}
                      >
                        Fjern
                      </Button>
                    </Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          )}
        </Card>
      {/* [Ny, 2026-09-09, issue #135] Bekreftelse for sletting av ÉN navneform. Egen dialog, ikke
        * gjenbruk av slett-virksomhet-dialogen under: den handler om en helt annen entitet og har sin
        * egen «kanSlettes»-blokkering. Konsekvensen som må fram er hvor mange TAGGER som forsvinner,
        * og at visningsnavnet endres hvis man sletter den gjeldende navneformen. */}
      <Dialog
        open={navneformTilSletting !== null}
        onClose={() => setNavneformTilSletting(null)}
        closeButton="Avbryt"
        style={{ maxWidth: '32rem' }}
      >
        <Dialog.Block>
          <Heading level={3} data-size="xs" style={{ marginBottom: '0.5rem' }}>
            Fjerne navneformen «{navneformTilSletting?.term}»?
          </Heading>
          {navneformTilSletting && (() => {
            const antallTagger = (whereUsed?.navneformForekomster ?? [])
              .filter((f) => f.navneformId === navneformTilSletting.id).length;
            const erGjeldende = navneformTilSletting.navneformgrunn === 'gjeldende';
            return (
              <>
                <Paragraph style={{ marginBottom: antallTagger > 0 || erGjeldende ? '0.5rem' : 0 }}>
                  {antallTagger === 0
                    ? 'Navneformen er ikke tagget i noen rettskildetekst. Ingenting annet forsvinner.'
                    : `Dette fjerner samtidig ${antallTagger} markering${antallTagger === 1 ? '' : 'er'} i `
                      + 'rettskildetekst. Markeringene forsvinner fra lovteksten — de kan ikke gjenopprettes, '
                      + 'men et nytt virksomhetssveip vil finne forekomstene på nytt hvis navneformen legges inn igjen.'}
                </Paragraph>
                {erGjeldende && (
                  <Alert data-color="warning" style={{ marginBottom: 0 }}>
                    Dette er virksomhetens GJELDENDE navneform. Uten den faller visningsnavnet tilbake
                    til registernavnet fra Brreg.
                  </Alert>
                )}
              </>
            );
          })()}
          {slettNavneformFeil && <Alert data-color="danger" style={{ marginTop: '0.75rem' }}>{slettNavneformFeil}</Alert>}
        </Dialog.Block>
        <Dialog.Block style={{ display: 'flex', gap: '0.5rem', justifyContent: 'flex-end' }}>
          <Button data-size="sm" variant="secondary" onClick={() => setNavneformTilSletting(null)}>Avbryt</Button>
          <Button data-size="sm" data-color="danger" onClick={bekreftSlettNavneform} disabled={sletterNavneform}>
            {sletterNavneform ? 'Fjerner …' : 'Bekreft fjerning'}
          </Button>
        </Dialog.Block>
      </Dialog>

        <form onSubmit={leggTilBegrep} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <Textfield data-size="sm" label="Ny navneform" placeholder="f.eks. Statsforvalter" value={nyTerm}
            onChange={(e) => setNyTerm(e.target.value)} required />
          <NavneformgrunnVelger
            value={nyNavneformgrunn}
            onChange={setNyNavneformgrunn}
            label="Grunn"
            visHjelp={false}
            style={{ minWidth: '12rem' }}
          />
          <Button data-size="sm" type="submit" disabled={leggerTil || !nyTerm.trim()}>
            {leggerTil ? 'Legger til …' : 'Legg til'}
          </Button>
        </form>
        {leggTilFeil && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{leggTilFeil}</Alert>}
      </section>
      )}

      {fane === 'myndighet' && (
      <section style={{ marginBottom: '2rem' }}>
        <Heading level={3} data-size="xs" style={{ marginBottom: '0.75rem' }}>
          Medlemskap og roller
        </Heading>
        <Metatekst style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
          Klasser og områder virksomheten er MEDLEM av (f.eks. «språkutviklingskommuner»), og roller den
          INNEHAR (f.eks. «reguleringsmyndighet») — to ulike påstander: det som gjelder en klasse gjelder hvert
          medlem, mens en rolle bare gjelder innenfor sin avgrensning (docs/33 §4.2). Gyldighet arves fra
          hjemmelen, og kan i tillegg avgrenses av en egen periode.
        </Metatekst>
        {/* [ENDRET, issue #311] Var «Myndighetstildelinger» (egen tabell); nå M-/I-kanter fra virksomheten. */}
        <StrukturkantTabell
          kanter={kanter && kanter.filter((k) => (k.kategori === 'M' || k.kategori === 'I') && k.retning === 'fra')}
          tomTekst="Ingen medlemskap eller roller registrert."
          visKategori
        />
        <Button data-size="sm" variant="secondary" onClick={() => setVisLeggTilTildeling((v) => !v)}>
          {visLeggTilTildeling ? 'Skjul skjema' : 'Legg til medlemskap/rolle'}
        </Button>
        {visLeggTilTildeling && id && (
          <LeggTilMyndighetstildelingForm
            virksomhetId={id}
            rettskilder={rettskilder}
            onOpprettet={(ny) => {
              setKanter((forrige) => [...(forrige ?? []), ny]);
              setVisLeggTilTildeling(false);
            }}
          />
        )}
      </section>
      )}

      {fane === 'myndighet' && (
      <section style={{ marginBottom: '2rem' }}>
        <Heading level={3} data-size="xs" style={{ marginBottom: '0.75rem' }}>
          Kompetanse, ansvarsområder og organtilhørighet
        </Heading>
        <Metatekst style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
          Øvrige strukturutsagn (issue #311, docs/33 §4.3): hvilken kompetanse virksomheten har etter hvilken
          bestemmelse, hvilke områder den har ansvar for, og hvilket rettssubjekt den er organ for. Registreres
          i dag av konverteringen (#313) eller over API-et — det finnes ikke et eget skjema for disse ennå.
        </Metatekst>
        <StrukturkantTabell
          kanter={kanter && kanter.filter((k) => k.kategori !== 'R' && !((k.kategori === 'M' || k.kategori === 'I') && k.retning === 'fra'))}
          tomTekst="Ingen andre strukturutsagn registrert."
          visKategori
        />
      </section>
      )}

      {fane === 'myndighet' && (
      <section style={{ marginBottom: '2rem' }}>
        <Heading level={3} data-size="xs" style={{ marginBottom: '0.75rem' }}>
          Ansvarlig for
        </Heading>
        <Metatekst style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
          Gjeldende lover/forskrifter der Lovdata oppgir denne virksomheten som ansvarlig departement
          (eksakt navnetreff, ingen fuzzy-matching — se rettskildens egen "Ansvarlig departement"-felt).
        </Metatekst>
        <Card style={{ padding: rettskilderAnsvarligFor && rettskilderAnsvarligFor.length > 0 ? 0 : '1rem', overflow: 'hidden' }}>
          {!rettskilderAnsvarligFor && <Spinner aria-label="Laster …" data-size="sm" />}
          {rettskilderAnsvarligFor && rettskilderAnsvarligFor.length === 0 && (
            <Paragraph style={{ margin: 0 }}>Ingen rettskilder registrert med denne virksomheten som ansvarlig departement.</Paragraph>
          )}
          {rettskilderAnsvarligFor && rettskilderAnsvarligFor.length > 0 && (
            <Table>
              <Table.Body>
                {rettskilderAnsvarligFor.map((r) => (
                  <Table.Row key={r.id}>
                    <Table.Cell>
                      <Link asChild>
                        <RouterLink to={`/rettskilder/${r.id}`}>{r.tittel}</RouterLink>
                      </Link>
                    </Table.Cell>
                    <Table.Cell>{r.kildetype}</Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          )}
        </Card>
      </section>
      )}

      {/* [Ny, fastsatt-av-runden, 2026-09-10, issue #215] Motstykket til «Ansvarlig for» over, og et
          ANNET spørsmål: Kunnskapsdepartementet har departementsansvaret for NTNUs ph.d.-forskrift,
          NTNUs styre har FASTSATT den. Uten denne seksjonen var forskriften usynlig fra NTNUs egen
          side, selv om dokumentet sier rett ut hvem som fastsatte den.
          [ENDRET, issue #268] Egen fane «Rettskilder» — et ANNET spørsmål enn «Myndighet &
          relasjoner» (hvilke rettskilder virksomheten selv har fastsatt, ikke hvem som styrer den). */}
      {fane === 'rettskilder' && (
      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Fastsatt av denne virksomheten
        </Heading>
        <Metatekst style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
          Gjeldende rettskilder der «Fastsatt av»-frasen i hjemmelslinja peker på denne virksomheten —
          matchet mot registernavnet og virksomhetens navneformer. «Fastsatt av styret ved X» regnes
          som fastsatt av X: styret er organet innad, institusjonen er den katalogen kjenner.
        </Metatekst>
        <Card style={{ padding: rettskilderFastsattAv && rettskilderFastsattAv.length > 0 ? 0 : '1rem', overflow: 'hidden' }}>
          {!rettskilderFastsattAv && <Spinner aria-label="Laster …" data-size="sm" />}
          {rettskilderFastsattAv && rettskilderFastsattAv.length === 0 && (
            <Paragraph style={{ margin: 0 }}>Ingen rettskilder er registrert som fastsatt av denne virksomheten.</Paragraph>
          )}
          {rettskilderFastsattAv && rettskilderFastsattAv.length > 0 && (
            <Table>
              <Table.Body>
                {rettskilderFastsattAv.map((r) => (
                  <Table.Row key={r.id}>
                    <Table.Cell>
                      <Link asChild>
                        <RouterLink to={`/rettskilder/${r.id}`}>{r.tittel}</RouterLink>
                      </Link>
                    </Table.Cell>
                    <Table.Cell>{r.kildetype}</Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          )}
        </Card>
      </section>
      )}

      {fane === 'kandidater' && (
      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Ventende kandidater
        </Heading>
        <Metatekst style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
          Funn fra tekstsøk som ikke er godkjent eller avvist ennå.{' '}
          <Link asChild><RouterLink to={`/virksomhet-kandidater?virksomhetId=${id}`}>Se full kandidatliste (alle statuser, filtrerbar)</RouterLink></Link>
        </Metatekst>
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '0.75rem' }}>
          <Button data-size="sm" variant="secondary" onClick={kjorSveip} disabled={sveiper}>
            {sveiper ? 'Sveiper …' : 'Kjør sveip for denne virksomheten'}
          </Button>
        </div>
        {sveipFeil && <Alert data-color="danger" style={{ marginBottom: '0.75rem' }}>{sveipFeil}</Alert>}
        {sveipResultat && (
          <Alert data-color="info" style={{ marginBottom: '0.75rem' }}>
            Fant {sveipResultat.funnet} treff totalt, {sveipResultat.nye} nye kandidater lagt i køen.
          </Alert>
        )}
        <Card style={{ padding: kandidater && kandidater.length > 0 ? 0 : '1rem', overflow: 'hidden' }}>
          {!kandidater && <Spinner aria-label="Laster …" data-size="sm" />}
          {kandidater && kandidater.length === 0 && <Paragraph style={{ margin: 0 }}>Ingen ventende kandidater.</Paragraph>}
          {kandidater && kandidater.length > 0 && (
            <Table>
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell>Node</Table.HeaderCell>
                  <Table.HeaderCell>Handling</Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {kandidater.map((k) => (
                  <Table.Row key={k.id}>
                    <Metatekst as={Table.Cell}>
                      {/* [Rettet, 2026-09-02, issue #115] "Node"-kolonnen er den ENESTE plassen i
                          denne tabellen som viser hvilken rettskilde treffet gjelder (ingen egen
                          "Rettskilde"-kolonne) — derfor kilde OG paragraf her, ikke bare paragrafen. */}
                      {(() => {
                        const rettskilde = rettskilder.find((r) => r.id === k.rettskildeId);
                        const kildeNavn = rettskilde ? rettskilde.tittel : k.rettskildeId;
                        return `${kildeNavn} — ${visNodeKort(k.rettskildeId, k.nodeEid)}`;
                      })()}
                    </Metatekst>
                    <Table.Cell style={{ display: 'flex', gap: '0.5rem' }}>
                      <Button
                        data-size="sm"
                        variant="secondary"
                        onClick={() => api.godkjennVirksomhetKandidat(k.id).then(lastAlt)}
                      >
                        Godkjenn
                      </Button>
                      <Button
                        data-size="sm"
                        variant="tertiary"
                        onClick={() => api.avvisVirksomhetKandidat(k.id).then(lastAlt)}
                      >
                        Avvis
                      </Button>
                    </Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          )}
        </Card>
      </section>
      )}

      {fane === 'farligSone' && (
        <SlettVirksomhetSeksjon
          virksomhetId={id!}
          virksomhetNavn={virksomhet.visningsnavn}
          onSlettet={() => navigate('/virksomheter')}
        />
      )}
    </>
  );
}

/**
 * [Ny, issue #157] Kaskadesletting — ingen `DELETE`-vei fantes for `Virksomhet` tidligere. "Ingen
 * stille destruksjon" (samme holdning som resten av appen): et klikk på «Slett virksomhet» henter
 * FØRST oversikten (`GET .../slett-oversikt`, sletter ingenting selv) og viser den i en `Dialog` —
 * selve slettingen (`DELETE .../{id}?bekreft=true`) skjer KUN etter et eksplisitt andre klikk på
 * «Bekreft sletting» i dialogen. Blokkert (av en publisert tekst-tagg-referanse, eller en uforutsett
 * referanse fra en annen virksomhets data) vises som en tydelig feilmelding i stedet for en disabled
 * knapp uten forklaring — bekreftelsesforsøket er det som avdekker blokkeringen.
 */
function SlettVirksomhetSeksjon({
  virksomhetId, virksomhetNavn, onSlettet,
}: { virksomhetId: string; virksomhetNavn: string; onSlettet: () => void }) {
  const [oversikt, setOversikt] = useState<VirksomhetSlettOversiktDto | null>(null);
  const [henterOversikt, setHenterOversikt] = useState(false);
  const [dialogApen, setDialogApen] = useState(false);
  const [sletter, setSletter] = useState(false);
  const [feil, setFeil] = useState<string | null>(null);

  async function apneDialog() {
    setFeil(null);
    setHenterOversikt(true);
    try {
      setOversikt(await api.hentVirksomhetSlettOversikt(virksomhetId));
      setDialogApen(true);
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved henting av slett-oversikt.');
    } finally {
      setHenterOversikt(false);
    }
  }

  async function bekreftSletting() {
    setSletter(true);
    setFeil(null);
    try {
      await api.slettVirksomhet(virksomhetId);
      setDialogApen(false);
      onSlettet();
    } catch (err) {
      // Blokkert (publisert referanse / uforutsett referanse fra en annen virksomhet) eller en annen
      // feil — dialogen blir stående åpen med feilmeldingen, ingenting ble slettet på backend-siden.
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved sletting.');
    } finally {
      setSletter(false);
    }
  }

  const synligeRader = oversikt
    ? SLETT_OVERSIKT_ETIKETTER.filter(([nokkel]) => (oversikt[nokkel] as number) > 0)
    : [];
  const underliggende = oversikt?.underliggendeVirksomheter ?? 0;

  return (
    <section style={{ marginBottom: '2rem' }}>
      <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
        Farlig sone
      </Heading>
      <Card style={{ padding: '1rem', borderColor: 'var(--ds-color-danger-border-default)' }}>
        <Metatekst style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
          Sletter virksomheten og ALT tilknyttet innhold den eier (tjenester, rettskilder, begreper,
          brukere m.fl.) — ingen tilbakestilling. Du får se nøyaktig hva som rammes før du bekrefter.
        </Metatekst>
        {feil && <Alert data-color="danger" style={{ marginBottom: '0.75rem' }}>{feil}</Alert>}
        <Button data-size="sm" data-color="danger" variant="secondary" onClick={apneDialog} disabled={henterOversikt}>
          {henterOversikt ? 'Henter oversikt …' : 'Slett virksomhet'}
        </Button>
      </Card>

      <Dialog open={dialogApen} onClose={() => setDialogApen(false)} closeButton="Avbryt" style={{ maxWidth: '32rem' }}>
        <Dialog.Block>
          <Heading level={3} data-size="xs" style={{ marginBottom: '0.5rem' }}>
            Slette «{virksomhetNavn}»?
          </Heading>
          {oversikt && synligeRader.length === 0 && underliggende === 0 && (
            <Paragraph style={{ margin: 0 }}>Ingen tilknyttede rader — kan slettes uten videre konsekvenser.</Paragraph>
          )}
          {oversikt && (synligeRader.length > 0 || underliggende > 0) && (
            <>
              <Paragraph style={{ marginBottom: '0.5rem' }}>Dette sletter i tillegg:</Paragraph>
              <Table style={{ marginBottom: underliggende > 0 ? '0.5rem' : 0 }}>
                <Table.Body>
                  {synligeRader.map(([nokkel, etikett]) => (
                    <Table.Row key={nokkel}>
                      <Table.HeaderCell style={{ fontWeight: 'normal' }}>{etikett}</Table.HeaderCell>
                      <Table.Cell style={{ textAlign: 'right' }}>{oversikt[nokkel] as number}</Table.Cell>
                    </Table.Row>
                  ))}
                </Table.Body>
              </Table>
              {underliggende > 0 && (
                <Metatekst style={{ margin: 0, color: 'var(--ds-color-neutral-text-subtle)' }}>
                  {underliggende} underliggende virksomhet{underliggende === 1 ? '' : 'er'} mister koblingen til denne som
                  overordnet enhet (slettes IKKE selv).
                </Metatekst>
              )}
            </>
          )}
          {oversikt && !oversikt.kanSlettes && (
            <Alert data-color="danger" style={{ marginTop: '0.75rem' }}>
              {oversikt.tekstTaggerMedPublisertReferanse} tekst-tagg(er) har en publisert referanse og
              blokkerer slettingen — se «Bekreft sletting» for detaljer, eller fjern referansene først.
            </Alert>
          )}
          {feil && <Alert data-color="danger" style={{ marginTop: '0.75rem' }}>{feil}</Alert>}
        </Dialog.Block>
        <Dialog.Block style={{ display: 'flex', gap: '0.5rem', justifyContent: 'flex-end' }}>
          <Button data-size="sm" variant="secondary" onClick={() => setDialogApen(false)}>Avbryt</Button>
          <Button data-size="sm" data-color="danger" onClick={bekreftSletting} disabled={sletter || oversikt?.kanSlettes === false}>
            {sletter ? 'Sletter …' : 'Bekreft sletting'}
          </Button>
        </Dialog.Block>
      </Dialog>
    </section>
  );
}
