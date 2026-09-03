import { useEffect, useState, type FormEvent } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router';
import { Alert, Button, Card, Dialog, Field, Heading, Label, Link, Paragraph, Select, Spinner, Table, Tag, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { KodelisteDto, MyndighetstildelingDto, RettskildeNodeDto, RettskildeSammendrag, VirksomhetKandidatDto, VirksomhetRelasjonDto, VirksomhetSlettOversiktDto, VirksomhetsbegrepDto } from '../api/types';
import { useVirksomheter } from '../virksomhet/useVirksomheter';
import { LeggTilMyndighetstildelingForm } from '../virksomhet/LeggTilMyndighetstildelingForm';
import { LeggTilVirksomhetRelasjonForm } from '../virksomhet/LeggTilVirksomhetRelasjonForm';

/** [Ny, issue #157] Rad-etiketter for bekreftelsesdialogen — KUN de feltene som faktisk kan være > 0
 * for en reell virksomhet vises (0-rader skjules, se `SlettVirksomhetSeksjon` under). Rekkefølgen her
 * er visningsrekkefølgen. */
const SLETT_OVERSIKT_ETIKETTER: [key: keyof VirksomhetSlettOversiktDto, etikett: string][] = [
  ['tjenester', 'Tjenester'],
  ['rettskilder', 'Egne (lokale) rettskilder'],
  ['begreper', 'Begreper (arbeidsprodukt)'],
  ['navneformer', 'Navneformer'],
  ['brukere', 'Brukere'],
  ['myndighetstildelinger', 'Myndighetstildelinger'],
  ['virksomhetKandidater', 'Navnekandidater i kø'],
  ['virksomhetRelasjoner', 'Relasjoner til andre virksomheter'],
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

export default function VirksomhetDetalj() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { virksomheter, virksomheterPerId, laster: virksomheterLaster } = useVirksomheter();

  const [begrep, setBegrep] = useState<VirksomhetsbegrepDto[] | null>(null);
  const [tildelinger, setTildelinger] = useState<MyndighetstildelingDto[] | null>(null);
  const [kandidater, setKandidater] = useState<VirksomhetKandidatDto[] | null>(null);
  const [relasjoner, setRelasjoner] = useState<VirksomhetRelasjonDto[] | null>(null);
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [visLeggTilTildeling, setVisLeggTilTildeling] = useState(false);
  const [visLeggTilRelasjon, setVisLeggTilRelasjon] = useState(false);
  // Departement-virksomhet-lenke (2026-08-30) — ikke betinget på noen egen "er departement"-boolsk,
  // se oppgavebeskrivelsen: lastes for ENHVER virksomhet, seksjonen skjules bare når listen er tom.
  const [rettskilderAnsvarligFor, setRettskilderAnsvarligFor] = useState<RettskildeSammendrag[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  const [nyTerm, setNyTerm] = useState('');
  const [leggerTil, setLeggerTil] = useState(false);
  const [leggTilFeil, setLeggTilFeil] = useState<string | null>(null);

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
  function visNodeKort(rettskildeId: string, eid: string): string {
    const node = noderPerRettskilde.get(rettskildeId)?.find((n) => n.eid === eid);
    if (!node) return eid;
    if (node.nodeType === 'side') return 'Hele siden';
    const paragraf = node.nummer ? `§ ${node.nummer}` : eid;
    return node.overskrift ? `${paragraf} — ${node.overskrift}` : paragraf;
  }

  function lastAlt() {
    if (!id) return;
    api.hentVirksomhetsbegrep(id).then(setBegrep)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av begrep.'));
    api.hentMyndighetstildelingerForVirksomhet(id).then(setTildelinger).catch(() => setTildelinger([]));
    api.hentVentendeKandidater(id).then(setKandidater).catch(() => setKandidater([]));
    api.hentRettskilderAnsvarligFor(id).then(setRettskilderAnsvarligFor).catch(() => setRettskilderAnsvarligFor([]));
    api.hentVirksomhetRelasjoner(id).then(setRelasjoner).catch(() => setRelasjoner([]));
  }

  useEffect(lastAlt, [id]);
  useEffect(() => {
    for (const t of tildelinger ?? []) sikreNoderFor(t.hjemmelRettskildeId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tildelinger]);
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

  async function leggTilBegrep(e: FormEvent) {
    e.preventDefault();
    if (!id || !nyTerm.trim()) return;
    setLeggTilFeil(null);
    setLeggerTil(true);
    try {
      await api.opprettVirksomhetsbegrep({ virksomhetId: id, term: nyTerm.trim(), skosUrl: null });
      setNyTerm('');
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

  return (
    <>
      <nav aria-label="Brødsmulesti" style={{ display: 'flex', gap: '0.4rem', fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.6rem', flexWrap: 'wrap' }}>
        <Link asChild><RouterLink to="/virksomheter">Virksomheter</RouterLink></Link>
        <span>/</span>
        <span style={{ color: 'var(--ds-color-neutral-text-default)' }}>{virksomhet.navn}</span>
      </nav>

      <Heading level={1} data-size="lg" style={{ marginBottom: '0.2rem' }}>
        {virksomhet.navn}
      </Heading>
      <Paragraph style={{ marginBottom: '0.75rem', display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
        <Tag data-color={forvaltningsniva ? 'info' : 'neutral'} data-size="sm">
          {forvaltningsniva ?? 'Forvaltningsnivå ikke satt'}
        </Tag>
        <Tag data-color={virksomhet.aktiv ? 'success' : 'neutral'} data-size="sm">
          {virksomhet.aktiv ? 'Aktiv' : 'Sovende'}
        </Tag>
      </Paragraph>

      {feil && <Alert data-color="danger" style={{ marginBottom: '1rem' }}>{feil}</Alert>}

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Grunndata
        </Heading>
        <Card style={{ padding: '1rem' }}>
          <Table>
            <Table.Body>
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
                    ? virksomheterPerId.get(virksomhet.overordnetEnhetId)?.navn ?? virksomhet.overordnetEnhetId
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

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Relasjoner til andre virksomheter
        </Heading>
        <Paragraph style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)' }}>
          Navngitte relasjoner til BESTEMTE, konkrete virksomheter (f.eks. «underlagt», «sekretariat for»)
          — til forskjell fra «Overordnet enhet» i Grunndata over, som er automatisk Brreg-avledet uten
          hjemmel. Listen viser relasjoner i BEGGE retninger fra denne virksomhetens ståsted — samme rad
          kan altså vises med ulik tekst på motpartens side.
        </Paragraph>
        <Card style={{ padding: relasjoner && relasjoner.length > 0 ? 0 : '1rem', overflow: 'hidden', marginBottom: '0.75rem' }}>
          {!relasjoner && <Spinner aria-label="Laster …" data-size="sm" />}
          {relasjoner && relasjoner.length === 0 && <Paragraph style={{ margin: 0 }}>Ingen relasjoner registrert.</Paragraph>}
          {relasjoner && relasjoner.length > 0 && (
            <Table>
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell>Relasjon</Table.HeaderCell>
                  <Table.HeaderCell>Hjemmel/kommentar</Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {relasjoner.map((r) => (
                  <Table.Row key={r.id}>
                    <Table.Cell>
                      {r.visningstekst}{' '}
                      <Link asChild>
                        <RouterLink to={`/virksomheter/${r.motpartVirksomhetId}`}>({r.motpartNavn})</RouterLink>
                      </Link>
                    </Table.Cell>
                    <Table.Cell style={{ fontSize: 'var(--ds-font-size-1)' }}>
                      {r.hjemmelEid || r.kommentar
                        ? [r.hjemmelEid, r.kommentar].filter(Boolean).join(' — ')
                        : '—'}
                    </Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          )}
        </Card>
        <Button data-size="sm" variant="secondary" onClick={() => setVisLeggTilRelasjon((v) => !v)}>
          {visLeggTilRelasjon ? 'Skjul skjema' : 'Legg til relasjon'}
        </Button>
        {visLeggTilRelasjon && id && (
          <LeggTilVirksomhetRelasjonForm
            virksomhetId={id}
            virksomheter={virksomheter}
            rettskilder={rettskilder}
            onOpprettet={(nye) => {
              setRelasjoner(nye);
              setVisLeggTilRelasjon(false);
            }}
          />
        )}
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Navneformer i rettskildetekst
        </Heading>
        <Paragraph style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)' }}>
          Alle navneformer under peker på samme virksomhet — synonymer (f.eks. «Fylkesmann»/«Statsforvalter») er bare flere rader, ingen egen mekanisme.
        </Paragraph>
        <Card style={{ padding: begrep && begrep.length > 0 ? 0 : '1rem', overflow: 'hidden', marginBottom: '0.75rem' }}>
          {!begrep && <Spinner aria-label="Laster …" data-size="sm" />}
          {begrep && begrep.length === 0 && <Paragraph style={{ margin: 0 }}>Ingen navneformer registrert ennå.</Paragraph>}
          {begrep && begrep.length > 0 && (
            <Table>
              <Table.Body>
                {begrep.map((b) => (
                  <Table.Row key={b.id}>
                    <Table.Cell>{b.term}</Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          )}
        </Card>
        <form onSubmit={leggTilBegrep} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end' }}>
          <Textfield label="Ny navneform" placeholder="f.eks. Statsforvalter" value={nyTerm}
            onChange={(e) => setNyTerm(e.target.value)} required />
          <Button type="submit" disabled={leggerTil || !nyTerm.trim()}>
            {leggerTil ? 'Legger til …' : 'Legg til'}
          </Button>
        </form>
        {leggTilFeil && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{leggTilFeil}</Alert>}
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Myndighetstildelinger
        </Heading>
        <Paragraph style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)' }}>
          Gruppebegrep (f.eks. «forurensningsmyndighet») tildelt denne virksomheten gjennom en forskrift.
          Gyldighet arves fra hjemmelen, og kan i tillegg avgrenses av en egen gyldighetsperiode under
          (de aller fleste tildelinger er permanente og viser ingen periode).
        </Paragraph>
        <Card style={{ padding: tildelinger && tildelinger.length > 0 ? 0 : '1rem', overflow: 'hidden', marginBottom: '0.75rem' }}>
          {!tildelinger && <Spinner aria-label="Laster …" data-size="sm" />}
          {tildelinger && tildelinger.length === 0 && <Paragraph style={{ margin: 0 }}>Ingen myndighetstildelinger registrert.</Paragraph>}
          {tildelinger && tildelinger.length > 0 && (
            <Table>
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell>Paragrafspenn</Table.HeaderCell>
                  <Table.HeaderCell>Vilkår</Table.HeaderCell>
                  <Table.HeaderCell>Gyldighetsperiode</Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {tildelinger.map((t) => (
                  <Table.Row key={t.id}>
                    <Table.Cell style={{ fontSize: 'var(--ds-font-size-1)' }}>
                      {t.paragrafspenn
                        .map((p) =>
                          p.tilEid
                            ? `${visNodeKort(t.hjemmelRettskildeId, p.fraEid)} – ${visNodeKort(t.hjemmelRettskildeId, p.tilEid)}`
                            : visNodeKort(t.hjemmelRettskildeId, p.fraEid),
                        )
                        .join(', ')}
                    </Table.Cell>
                    <Table.Cell>{t.vilkaar ?? '—'}</Table.Cell>
                    <Table.Cell>{t.gyldigFra || t.gyldigTil ? `${t.gyldigFra ?? ''}–${t.gyldigTil ?? ''}` : '—'}</Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          )}
        </Card>
        <Button data-size="sm" variant="secondary" onClick={() => setVisLeggTilTildeling((v) => !v)}>
          {visLeggTilTildeling ? 'Skjul skjema' : 'Legg til myndighetstildeling'}
        </Button>
        {visLeggTilTildeling && id && (
          <LeggTilMyndighetstildelingForm
            virksomhetId={id}
            rettskilder={rettskilder}
            onOpprettet={(ny) => {
              setTildelinger((forrige) => [...(forrige ?? []), ny]);
              setVisLeggTilTildeling(false);
            }}
          />
        )}
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Ansvarlig for
        </Heading>
        <Paragraph style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)' }}>
          Gjeldende lover/forskrifter der Lovdata oppgir denne virksomheten som ansvarlig departement
          (eksakt navnetreff, ingen fuzzy-matching — se rettskildens egen "Ansvarlig departement"-felt).
        </Paragraph>
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

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Ventende kandidater
        </Heading>
        <Paragraph style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)' }}>
          Funn fra tekstsøk som ikke er godkjent eller avvist ennå.{' '}
          <Link asChild><RouterLink to={`/virksomhet-kandidater?virksomhetId=${id}`}>Se full kandidatliste (alle statuser, filtrerbar)</RouterLink></Link>
        </Paragraph>
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
                    <Table.Cell style={{ fontSize: 'var(--ds-font-size-1)' }}>
                      {/* [Rettet, 2026-09-02, issue #115] "Node"-kolonnen er den ENESTE plassen i
                          denne tabellen som viser hvilken rettskilde treffet gjelder (ingen egen
                          "Rettskilde"-kolonne) — derfor kilde OG paragraf her, ikke bare paragrafen. */}
                      {(() => {
                        const rettskilde = rettskilder.find((r) => r.id === k.rettskildeId);
                        const kildeNavn = rettskilde ? rettskilde.tittel : k.rettskildeId;
                        return `${kildeNavn} — ${visNodeKort(k.rettskildeId, k.nodeEid)}`;
                      })()}
                    </Table.Cell>
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

      <SlettVirksomhetSeksjon
        virksomhetId={id!}
        virksomhetNavn={virksomhet.navn}
        onSlettet={() => navigate('/virksomheter')}
      />
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
        <Paragraph style={{ marginBottom: '0.75rem', fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
          Sletter virksomheten og ALT tilknyttet innhold den eier (tjenester, rettskilder, begreper,
          brukere m.fl.) — ingen tilbakestilling. Du får se nøyaktig hva som rammes før du bekrefter.
        </Paragraph>
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
                <Paragraph style={{ margin: 0, fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
                  {underliggende} underliggende virksomhet{underliggende === 1 ? '' : 'er'} mister koblingen til denne som
                  overordnet enhet (slettes IKKE selv).
                </Paragraph>
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
