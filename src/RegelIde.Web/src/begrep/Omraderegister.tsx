import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Card, Heading, Link, Paragraph, Spinner, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import type { KommunePlikterDto, KommuneTilhorighetDto, Omradetype, StrukturkantDto, TilhorighetsrubrikkDto } from '../api/types';
import { StrukturkantTabell, nodeHref } from '../strukturkant/StrukturkantTabell';
import { motpartStatusVisning, pliktGrunnlagTekst, pliktRetningTekst } from '../strukturkant/plikt';
import { Metatekst } from '../entitet/Metatekst';

/**
 * [Ny, issue #312 «Strukturmodell 7: områderegister», 2026-10-08] Det et REGISTRERT område (fylke, kommune,
 * tettsted, lagsogn, lagdømme, helseregion) skal svare på (docs/32 S8, docs/33 §6.1 tommelfingerregel 5 og 10):
 * hva består det av, hva inngår det i, hvem har ansvar der — og for en kommune: hvilket fylke, hvilken tingrett,
 * lagdømme, statsforvalter og RHF den hører til.
 *
 * Kantene vises med den delte `StrukturkantTabell` (docs/09 §31) — ingen lokal kanttabell. Tilhørigheten er en
 * egen, kompakt tabell fordi den er et BEREGNET svar (fra kantene), ikke kanter: hver rubrikk viser kandidatene
 * og, når det ikke er nøyaktig én, «Ikke entydig» (`warning`) eller «Mangler» (`neutral`) — ingenting velges
 * (CLAUDE.md §8, samme regel #314 skal bruke).
 *
 * Seksjonsmønster som resten av BegrepDetalj (docs/09 §16): `Card` alltid rendret med tom-tilstand inni,
 * `null` = laster ⇒ `Spinner`.
 */
export const OMRADETYPE_VISNING: Record<Omradetype, string> = {
  fylke: 'Fylke',
  kommune: 'Kommune (territorium)',
  tettsted: 'Tettsted',
  lagsogn: 'Lagsogn',
  lagdomme: 'Lagdømme',
  helseregion: 'Helseregion',
  annet: 'Annet område',
};

/** Rubrikknavn slik de vises — samme rekkefølge som tjenesten gir dem. */
const RUBRIKK_VISNING: Record<string, string> = {
  kommune: 'Kommunen (rettssubjekt)',
  fylke: 'Fylke',
  tingrett: 'Tingrett',
  lagsogn: 'Lagsogn',
  'lagdømme': 'Lagdømme',
  lagmannsrett: 'Lagmannsrett',
  statsforvalter: 'Statsforvalter',
  helseregion: 'Helseregion',
  RHF: 'Regionalt helseforetak',
};

/** Rubrikker der kandidatene er OMRÅDER (lenke til begrepet); de andre er virksomheter. */
const OMRADERUBRIKKER = new Set(['fylke', 'lagsogn', 'lagdømme', 'helseregion']);

export function OmradetypeTag({ omradetype, omradekode }: { omradetype: Omradetype; omradekode?: string | null }) {
  return (
    <Tag data-color="neutral" data-size="sm" title="Områdetype og kode i det autoritative registeret (issue #312).">
      {OMRADETYPE_VISNING[omradetype] ?? omradetype}{omradekode ? ` ${omradekode}` : ''}
    </Tag>
  );
}

function Rubrikkstatus({ r }: { r: TilhorighetsrubrikkDto }) {
  if (r.status === 'entydig') return null;
  return r.status === 'ikke_entydig'
    ? <Tag data-color="warning" data-size="sm" title="Flere kandidater — ingen er valgt.">Ikke entydig</Tag>
    : <Tag data-color="neutral" data-size="sm" title="Ingen kant i registeret gir et svar.">Mangler</Tag>;
}

/**
 * [Ny, issue #312, Johanns beslutning 2026-10-08] Samlet godkjenning: domstolkantene konverteringen leser ut av
 * inndelingsforskriften lagres som forslag og godkjennes samlet per hjemmelsrettskilde (hver kant logges i Proveniens).
 * Vises når området har forslag. To klikk (knapp → bekreft), fordi handlingen gjelder ALLE forslag med samme hjemmel,
 * også i andre områder — tallet står i knappen. Bare mønsterforslag (`monster:`), aldri KI-forslag med samme hjemmel.
 */
function SamletGodkjenning({ kanter, onGodkjent }: { kanter: StrukturkantDto[]; onGodkjent: () => void }) {
  const [grupper, setGrupper] = useState<{ hjemmelRettskildeId: string; tittel: string; antall: number }[] | null>(null);
  const [bekreft, setBekreft] = useState<string | null>(null);
  const [melding, setMelding] = useState<string | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const hjemler = new Set(kanter.filter((k) => k.status === 'maskinelt_foreslatt' && k.hjemmelRettskildeId
    && k.oppdagelsesKilde.startsWith('monster:')).map((k) => k.hjemmelRettskildeId!));
  useEffect(() => {
    if (hjemler.size === 0) return;
    api.hentStrukturkantForslagPerHjemmel().then(setGrupper).catch(() => setGrupper([]));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [kanter]);
  if (hjemler.size === 0 && !melding) return null;
  async function godkjenn(id: string) {
    setFeil(null);
    try {
      const svar = await api.godkjennAlleStrukturkantForslag(id, 'monster:');
      setMelding(`${svar.godkjent} forslag er godkjent.`);
      setBekreft(null);
      onGodkjent();
    } catch (e) {
      setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved godkjenning.');
    }
  }
  return (
    <section style={{ marginBottom: '2rem' }}>
      <Heading level={3} data-size="xs" style={{ marginBottom: '0.75rem' }}>Forslag som venter på godkjenning</Heading>
      <Metatekst style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
        Kantene under som er merket «Forslag» er lest maskinelt ut av lovteksten. Kontroller dem mot hjemmelen, og godkjenn
        alle forslag med samme hjemmel samlet. Hver kant logges i proveniensen.
      </Metatekst>
      {melding && <Alert data-color="success" data-size="sm" style={{ marginBottom: '0.5rem' }}>{melding}</Alert>}
      {feil && <Alert data-color="danger" data-size="sm" style={{ marginBottom: '0.5rem' }}>{feil}</Alert>}
      {!grupper && hjemler.size > 0 && <Spinner aria-label="Laster …" data-size="sm" />}
      {grupper?.filter((g) => hjemler.has(g.hjemmelRettskildeId)).map((g) => (
        <Paragraph key={g.hjemmelRettskildeId} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
          {bekreft === g.hjemmelRettskildeId ? (
            <>
              <Button data-size="sm" onClick={() => godkjenn(g.hjemmelRettskildeId)}>Bekreft: godkjenn {g.antall} forslag</Button>
              <Button data-size="sm" variant="tertiary" onClick={() => setBekreft(null)}>Avbryt</Button>
            </>
          ) : (
            <Button data-size="sm" variant="secondary" onClick={() => setBekreft(g.hjemmelRettskildeId)}>
              Godkjenn alle {g.antall} forslag hjemlet i {g.tittel}
            </Button>
          )}
        </Paragraph>
      ))}
    </section>
  );
}

/**
 * [Ny, issue #353 AC5, docs/32 S6] «Hvem har kommunen plikt overfor, og hvem har plikt overfor kommunen?» [ENDRET, #353-retting: begge retninger, kolonnen Retning] — svaret fra GET /api/omrader/kommuner/{nr}/plikter.
 * Et BEREGNET svar, som tilhørigheten: per plikt utsagnet, hvorfor den gjelder kommunen (direkte, via registrert medlemskap,
 * eller fra en klasse uten registrert medlemskap — da med hullet), og motparten løst via område. Statusfargene er
 * tilhørighetens (docs/09 §32/§34): løst = ingen tag, «Ikke entydig» = warning, «Mangler»/«Ikke angitt» = neutral. Ingen
 * kandidat velges. Uten plikter vises en kort metatekst — fraværet er selve svaret på spørsmålet.
 */
function KommunensPlikter({ kommunenummer }: { kommunenummer: string }) {
  const [svar, setSvar] = useState<KommunePlikterDto | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  useEffect(() => {
    setSvar(null);
    setFeil(null);
    api.hentKommunePlikter(kommunenummer).then(setSvar)
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved oppslag av plikter.'));
  }, [kommunenummer]);

  return (
    <section style={{ marginBottom: '2rem' }}>
      <Heading level={3} data-size="xs" style={{ marginBottom: '0.75rem' }}>Plikter overfor motpart</Heading>
      <Metatekst style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
        Hvem kommunen skal samarbeide med, inngå avtale med, betale til, bistå, informere eller konsultere — og hvem som har slike plikter overfor kommunen (issue #353). En plikt
        som er registrert på en klasse («kommunen», «det regionale helseforetaket i helseregionen»), løses til konkrete parter
        gjennom områdene — mangler medlemskapet eller områdeinndelingen, står hullet i klartekst. Ingen kandidat velges.
      </Metatekst>
      {feil && <Alert data-color="danger" data-size="sm">{feil}</Alert>}
      {!svar && !feil && <Spinner aria-label="Laster …" data-size="sm" />}
      {svar && svar.hull.map((h) => (
        <Metatekst key={h} style={{ marginBottom: '0.5rem', color: 'var(--ds-color-neutral-text-subtle)' }}>{h}</Metatekst>
      ))}
      {svar && svar.plikter.length === 0 && (
        <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Ingen registrerte plikter gjelder kommunen.</Metatekst>
      )}
      {svar && svar.plikter.length > 0 && (
        <Card style={{ padding: 0, overflow: 'hidden' }}>
          <Table data-density="compact">
            <Table.Head>
              <Table.Row>
                <Table.HeaderCell>Retning</Table.HeaderCell>
                <Table.HeaderCell>Plikt</Table.HeaderCell>
                <Table.HeaderCell>Gjelder kommunen fordi</Table.HeaderCell>
                <Table.HeaderCell>Motpart</Table.HeaderCell>
              </Table.Row>
            </Table.Head>
            <Table.Body>
              {svar.plikter.map((t) => {
                const status = motpartStatusVisning(t.motpart.status);
                return (
                  <Table.Row key={`${t.kant.id}:${t.retning}`}>
                    <Table.Cell>{pliktRetningTekst(t.retning)}</Table.Cell>
                    <Table.Cell>
                      {t.kant.visningstekst}
                      {t.kant.hjemmelRettskildeTittel && (
                        <Metatekst as="span" style={{ display: 'block', color: 'var(--ds-color-neutral-text-subtle)' }}>
                          {t.kant.hjemmelRettskildeTittel}
                        </Metatekst>
                      )}
                    </Table.Cell>
                    <Table.Cell>
                      {pliktGrunnlagTekst(t.grunnlag, t.retning === 'kommunen_skal' ? t.kant.fra.navn : t.kant.til?.navn)}
                      {t.grunnlagHull && (
                        <Metatekst as="span" style={{ display: 'block', color: 'var(--ds-color-neutral-text-subtle)' }}>{t.grunnlagHull}</Metatekst>
                      )}
                    </Table.Cell>
                    <Table.Cell>
                      <span style={{ display: 'inline-flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
                        {t.motpart.kandidater.map((k, i) => (
                          <span key={k.id}>
                            <Link asChild><RouterLink to={`/virksomheter/${k.id}`}>{k.navn}</RouterLink></Link>
                            {i < t.motpart.kandidater.length - 1 ? ',' : ''}
                          </span>
                        ))}
                        {t.motpart.kandidater.length === 0 && '—'}
                        {status && <Tag data-color={status.farge} data-size="sm" title={status.forklaring}>{status.tekst}</Tag>}
                      </span>
                      {t.motpart.hull && (
                        <Metatekst as="span" style={{ display: 'block', color: 'var(--ds-color-neutral-text-subtle)' }}>{t.motpart.hull}</Metatekst>
                      )}
                    </Table.Cell>
                  </Table.Row>
                );
              })}
            </Table.Body>
          </Table>
        </Card>
      )}
    </section>
  );
}

/** [Ny, issue #390 AK 3] Flertallsform av en områdetype i overskriften «Består av …». */
const OMRADETYPE_FLERTALL: Record<Omradetype, string> = {
  fylke: 'fylker', kommune: 'kommuner', tettsted: 'tettsteder', lagsogn: 'lagsogn', lagdomme: 'lagdømmer', helseregion: 'helseregioner',
  annet: 'andre områder',
};

/** [Ny, issue #390 AK 3] Entallsform i overskriften «Ligger i …». */
const OMRADETYPE_ENTALL: Record<Omradetype, string> = {
  fylke: 'fylke', kommune: 'kommune', tettsted: 'tettsted', lagsogn: 'lagsogn', lagdomme: 'lagdømme', helseregion: 'helseregion',
  annet: 'annet område',
};

/** [Ny, issue #390 AK 3] Én gruppe relasjoner med samme betydning sett fra området. */
interface Relasjonsgruppe { nokkel: string; tittel: string; kolonne: string; teknisk: string; kanter: StrukturkantDto[] }

/**
 * [Ny, issue #390 AK 3] Grupperer områdets kanter etter BETYDNING. Reglene:
 * <ul>
 *   <li>A har_ansvarsomrade fra kommunens eget rettssubjekt (rubrikken «kommune» i tilhørigheten) → «Er territoriet til».</li>
 *   <li>A har_ansvarsomrade ellers: fra tingretten (kanten er lest ut av inndelingsforskriften, oppdagelseskilde `monster:inndeling-*`) →
 *       «Domstol (tingrett)»; fra lagmannsretten (`monster:lagdomme-*`) → «Lagmannsrett»; på et fylke → «Statsforvalter»; på en
 *       helseregion → «Regionalt helseforetak» (samme rubrikkregel som tilhørigheten, docs/33 §4.1); ellers «Har ansvarsområde her».
 *       Navnet på aktøren brukes aldri.</li>
 *   <li>A har_sete_i → «Rettssted for»; A sogner_til → «Tingretter som sogner hit» (#345).</li>
 *   <li>O der området er delen → «Ligger i ‹type›» (den andre nodens områdetype); der området er helheten → «Består av ‹typer›».</li>
 *   <li>Alt annet → «Andre strukturutsagn», med kategorien som merke.</li>
 * </ul>
 */
function grupperRelasjoner(kanter: StrukturkantDto[], omradetype: Omradetype, egne: Set<string>): Relasjonsgruppe[] {
  const grupper = new Map<string, Relasjonsgruppe>();
  const legg = (nokkel: string, tittel: string, kolonne: string, teknisk: string, k: StrukturkantDto) => {
    if (!grupper.has(nokkel)) grupper.set(nokkel, { nokkel, tittel, kolonne, teknisk, kanter: [] });
    grupper.get(nokkel)!.kanter.push(k);
  };
  for (const k of kanter) {
    const annen = k.retning === 'til' ? k.fra : k.til;
    if (k.kategori === 'A' && k.retning === 'til') {
      if (k.typekode === 'har_ansvarsomrade' && egne.has(k.fra.id)) {
        legg('1-territorie', 'Er territoriet til', 'Rettssubjekt',
          'Ansvarsområde (A har_ansvarsomrade) fra rettssubjektet med samme kommunenummer i Enhetsregisteret.', k);
      } else if (k.typekode === 'har_ansvarsomrade' && k.oppdagelsesKilde.startsWith('monster:inndeling')) {
        legg('3-domstol', 'Domstol (tingrett)', 'Tingrett',
          'Ansvarsområde (A har_ansvarsomrade) fra tingretten: domssognet, lest ut av forskrift om inndelingen av rettskretser og lagdømmer.', k);
      } else if (k.typekode === 'har_ansvarsomrade' && k.oppdagelsesKilde.startsWith('monster:lagdomme')) {
        legg('6-lagmannsrett', 'Lagmannsrett', 'Lagmannsrett',
          'Ansvarsområde (A har_ansvarsomrade) fra lagmannsretten for lagdømmet (inndelingsforskriften § 10 første ledd).', k);
      } else if (k.typekode === 'har_ansvarsomrade' && omradetype === 'fylke') {
        legg('5-statsforvalter', 'Statsforvalter', 'Embete', 'Ansvarsområde (A har_ansvarsomrade) fra embetet til fylket (embetsinndelingen).', k);
      } else if (k.typekode === 'har_ansvarsomrade' && omradetype === 'helseregion') {
        legg('7-rhf', 'Regionalt helseforetak', 'Helseforetak', 'Ansvarsområde (A har_ansvarsomrade) fra helseforetaket (vedtektene § 3).', k);
      } else if (k.typekode === 'har_sete_i') {
        legg('4-rettssted', 'Rettssted for', 'Tingrett', 'Sete (A har_sete_i): tingretten har rettssted her.', k);
      } else if (k.typekode === 'sogner_til') {
        legg('4-sogner', 'Tingretter som sogner hit', 'Tingrett',
          'A sogner_til: kommunene i lagsognet er kommunene disse tingrettene dekker (#345).', k);
      } else {
        legg('8-ansvar', 'Har ansvarsområde her', 'Aktør', `Ansvarsområde (A ${k.typekode}).`, k);
      }
    } else if (k.kategori === 'O' && k.retning === 'til') {
      const t = annen?.omradetype ?? null;
      legg(`2-ligger-${t ?? 'x'}`, t ? `Ligger i ${OMRADETYPE_ENTALL[t]}` : 'Ligger i', 'Område',
        'Områdesammensetning (O bestar_av) sett fra delen.', k);
    } else if (k.kategori === 'O' && k.retning === 'fra') {
      const t = annen?.omradetype ?? null;
      legg(`9-bestar-${t ?? 'x'}`, t ? `Består av ${OMRADETYPE_FLERTALL[t]}` : 'Består av', 'Område',
        'Områdesammensetning (O bestar_av) sett fra helheten.', k);
    } else {
      legg('99-andre', 'Andre strukturutsagn', 'Utsagn', 'Andre kategorier; kategorien står som merke på raden.', k);
    }
  }
  return [...grupper.values()].sort((a, b) => a.nokkel.localeCompare(b.nokkel, 'nb', { numeric: true }));
}

/** [Ny, issue #390 AK 3] Relasjonstabellen i klart språk: én `StrukturkantTabell` (docs/09 §31) per gruppe, med den andre noden i første
 * kolonne og kilden i egen kolonne. Samme mønster som kompetansefamiliene (docs/09 §33). */
function OmradeRelasjoner({ kanter, omradetype, egneRettssubjekter }: {
  kanter: StrukturkantDto[] | null; omradetype: Omradetype; egneRettssubjekter: Set<string>;
}) {
  const grupper = kanter ? grupperRelasjoner(kanter, omradetype, egneRettssubjekter) : null;
  return (
    <section style={{ marginBottom: '2rem' }}>
      <Heading level={3} data-size="xs" style={{ marginBottom: '0.5rem' }}>Relasjoner</Heading>
      <Metatekst style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
        Gruppert etter hva relasjonen betyr for området. Kilden (hjemmel i lovteksten eller registeret) står i egen kolonne, og den tekniske
        kategorien står under hver overskrift.
      </Metatekst>
      {!grupper && <Spinner aria-label="Laster …" data-size="sm" />}
      {grupper && grupper.length === 0 && (
        <Card style={{ padding: '1rem' }}><Paragraph style={{ margin: 0 }}>Området har ingen registrerte relasjoner.</Paragraph></Card>
      )}
      {grupper?.map((g) => (
        <div key={g.nokkel} style={{ marginBottom: '1.25rem' }}>
          <Heading level={4} data-size="2xs" style={{ marginBottom: '0.25rem' }}>{g.tittel}</Heading>
          <Metatekst style={{ marginBottom: '0.5rem', color: 'var(--ds-color-neutral-text-subtle)' }}>{g.teknisk}</Metatekst>
          <StrukturkantTabell kanter={g.kanter} tomTekst="" visKategori={g.nokkel === '99-andre'} utsagnOverskrift={g.kolonne}
            utsagn={g.nokkel === '99-andre' ? undefined : (k) => {
              const annen = k.retning === 'til' ? k.fra : k.til;
              return annen ? <Link asChild><RouterLink to={nodeHref(annen)}>{annen.navn}</RouterLink></Link> : k.visningstekst;
            }} />
        </div>
      ))}
    </section>
  );
}

export function Omraderegister({ omradeId, omradetype }: { omradeId: string; omradetype: Omradetype }) {
  const [kanter, setKanter] = useState<StrukturkantDto[] | null>(null);
  const [tilhorighet, setTilhorighet] = useState<KommuneTilhorighetDto | null>(null);
  const [feil, setFeil] = useState<string | null>(null);

  const [versjon, setVersjon] = useState(0);
  useEffect(() => {
    setKanter(null);
    setTilhorighet(null);
    api.hentStrukturkanter({ begrepId: omradeId }).then(setKanter).catch(() => setKanter([]));
    if (omradetype === 'kommune') {
      api.hentOmradeTilhorighet(omradeId).then(setTilhorighet)
        .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved oppslag av tilhørighet.'));
    }
  }, [omradeId, omradetype, versjon]);

  // [FJERNET, issue #390 AK 3] sammensetning / inngarI / ansvar / sognerHit / ovrige — grupperingen bor nå i `grupperRelasjoner`.
  // [Fra #345] «sogner_til» (tingrett → lagsogn) er fortsatt egen gruppe, «Tingretter som sogner hit».

  return (
    <>
      {kanter && <SamletGodkjenning kanter={kanter} onGodkjent={() => setVersjon((v) => v + 1)} />}

      {omradetype === 'kommune' && (
        <section style={{ marginBottom: '2rem' }}>
          <Heading level={3} data-size="xs" style={{ marginBottom: '0.75rem' }}>Tilhørighet</Heading>
          <Metatekst style={{ marginBottom: '0.75rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
            Beregnet fra kantene under og i de overordnede områdene: fylke og helseregion fra Kartverket og
            RHF-vedtektene, statsforvalteren fra embetsinndelingen. Tingrett, lagsogn, lagdømme og lagmannsrett kommer fra
            forskrift om inndelingen av rettskretser og lagdømmer, gjennom kjeden kommune ← tingrett → lagsogn → lagdømme.
            Er det ikke nøyaktig én kandidat, velges ingen.
          </Metatekst>
          {feil && <Alert data-color="danger" data-size="sm">{feil}</Alert>}
          <Card style={{ padding: tilhorighet ? 0 : '1rem', overflow: 'hidden', marginBottom: '0.75rem' }}>
            {!tilhorighet && !feil && <Spinner aria-label="Laster …" data-size="sm" />}
            {tilhorighet && (
              <Table data-density="compact">
                <Table.Body>
                  {tilhorighet.rubrikker.map((r) => (
                    <Table.Row key={r.rubrikk}>
                      <Table.HeaderCell style={{ width: '14rem' }}>{RUBRIKK_VISNING[r.rubrikk] ?? r.rubrikk}</Table.HeaderCell>
                      <Table.Cell>
                        <span style={{ display: 'inline-flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
                          {r.kandidater.map((k, i) => (
                            <span key={k.id}>
                              <Link asChild>
                                <RouterLink to={OMRADERUBRIKKER.has(r.rubrikk) ? `/begreper/${k.id}` : `/virksomheter/${k.id}`}>{k.navn}</RouterLink>
                              </Link>
                              {k.forslag && (
                                <Tag data-color="info" data-size="sm" style={{ marginLeft: '0.3rem' }}
                                  title="Svaret hviler på en kant som ennå er et forslag (tolket lovtekst, ikke godkjent).">Forslag</Tag>
                              )}
                              {i < r.kandidater.length - 1 ? ',' : ''}
                            </span>
                          ))}
                          {r.kandidater.length === 0 && '—'}
                          <Rubrikkstatus r={r} />
                        </span>
                      </Table.Cell>
                    </Table.Row>
                  ))}
                </Table.Body>
              </Table>
            )}
          </Card>
        </section>
      )}

      {/* [Ny, issue #353] S6 «hvem har kommunen plikt overfor?» — bare for en kommune med kommunenummer. */}
      {omradetype === 'kommune' && tilhorighet?.kommune.omradekode && (
        <KommunensPlikter kommunenummer={tilhorighet.kommune.omradekode} />
      )}

      {/* [ENDRET, issue #390 AK 3, Johann 2026-10-16] Relasjonene i klart språk, gruppert etter betydning («Er territoriet til», «Ligger i
        * fylke», «Domstol», «Rettssted for» …). Før: fire seksjoner med kanttekst sett fra områdets side («er ansvarsområde for Alta
        * kommune»), som var vanskelig å lese. Kilden står i egen kolonne; den tekniske kategorien står som forklaring under overskriften. */}
      <OmradeRelasjoner kanter={kanter} omradetype={omradetype}
        egneRettssubjekter={new Set(tilhorighet?.rubrikker.find((r) => r.rubrikk === 'kommune')?.kandidater.map((k) => k.id) ?? [])} />
    </>
  );
}
