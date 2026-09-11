import { useEffect, useState, type FormEvent } from 'react';
import { Link as RouterLink, useParams } from 'react-router';
import { Alert, Button, Card, Field, Heading, Label, Link, Paragraph, Select, Spinner, Tabs, Tag, Textarea, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { NavneformgrunnTag } from '../virksomhet/Navneformgrunn';
import { GruppeMedlemmer } from '../virksomhet/GruppeMedlemmer';
import { finnRettskildeForEid, rettskildeLenke, rettskildeLenkeForId } from '../api/eidLenker';
import { useVirksomheter } from '../virksomhet/useVirksomheter';
import type { BegrepBruktIRettskildeDto, BegrepDefinisjonRelasjonDto, BegrepDto, BegrepTaggetForekomstDto, RettskildeSammendrag, VilkarDto } from '../api/types';
import { StatusStepper } from '../entitet/StatusStepper';
import { Metatekst } from '../entitet/Metatekst';

/**
 * [Ny, issue #276, 2026-09-11] Fanegruppering — 6-7 flate seksjoner (avhengig av begrepskategori),
 * over docs/30 §3.1 pkt. 3 sin "faner ved >~4 seksjoner"-grense. Gruppert etter hva seksjonen
 * FAKTISK svarer på (samme prinsipp som VirksomhetDetalj, issue #268): Grunndata (identitet/redigering),
 * Bruk (hvor forekommer/brukes begrepet), Relasjoner (kun vist når det er noe å vise — gruppe-
 * medlemskap eller bekreftede definisjonsrelasjoner).
 */
type Fane = 'grunndata' | 'bruk' | 'relasjoner';

export default function BegrepDetalj() {
  const { id } = useParams<{ id: string }>();
  const [fane, setFane] = useState<Fane>('grunndata');
  const [begrep, setBegrep] = useState<BegrepDto | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [bruktIVilkar, setBruktIVilkar] = useState<Array<{ vilkar: VilkarDto; rotnodeId: string | undefined }>>([]);
  const [bruktIRettskilder, setBruktIRettskilder] = useState<BegrepBruktIRettskildeDto[]>([]);
  const [taggedeForekomster, setTaggedeForekomster] = useState<BegrepTaggetForekomstDto[]>([]);
  const [definisjonsrelasjoner, setDefinisjonsrelasjoner] = useState<BegrepDefinisjonRelasjonDto[]>([]);
  const { visEier } = useVirksomheter();

  const [term, setTerm] = useState('');
  const [definisjon, setDefinisjon] = useState('');
  const [lovreferanseEid, setLovreferanseEid] = useState('');
  const [begrepstype, setBegrepstype] = useState('faktabegrep');
  const [lagrer, setLagrer] = useState(false);
  const [lagreFeil, setLagreFeil] = useState<string | null>(null);
  const [statusEndres, setStatusEndres] = useState(false);

  function fyllSkjemaFra(b: BegrepDto) {
    setTerm(b.term);
    setDefinisjon(b.definisjon ?? '');
    setLovreferanseEid(b.lovreferanseEid ?? '');
    setBegrepstype(b.begrepstype ?? 'faktabegrep');
  }

  useEffect(() => {
    if (!id) return;
    api.hentBegrep(id).then((b) => {
      setBegrep(b);
      fyllSkjemaFra(b);
    }).catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av begrep.'));
    api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([]));
    // «Brukt i vilkår» — bevisst forenkling (kun ett vilkårstre finnes i dag, se plan «Sammenhengende navigasjon»):
    // rotnodeId hentes fra første tjeneste som har en satt, i stedet for en generell reverse-oppslag.
    Promise.all([api.hentVilkarListe(), api.hentTjenester()])
      .then(([vilkarListe, tjenester]) => {
        const rotnodeId = tjenester.find((t) => t.rotnodeId)?.rotnodeId ?? undefined;
        setBruktIVilkar(
          vilkarListe
            .filter((v) => v.begrepId === id || v.skjonnsgrunnlagBegrepId === id)
            .map((v) => ({ vilkar: v, rotnodeId })),
        );
      })
      .catch(() => setBruktIVilkar([]));
    api.hentBegrepBruktIRettskilder(id).then(setBruktIRettskilder).catch(() => setBruktIRettskilder([]));
    api.hentBegrepTaggedeForekomster(id).then(setTaggedeForekomster).catch(() => setTaggedeForekomster([]));
    // [Ny, #212] Bekreftede «definert likt som»-relasjoner — se seksjonen «Også definert i andre
    // rettskilder» under. Egen try/catch-fallback (tom liste) som resten av siden, ikke en global feil.
    api.hentBegrepDefinisjonsrelasjoner(id).then(setDefinisjonsrelasjoner).catch(() => setDefinisjonsrelasjoner([]));
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

  if (feil) return <Alert data-color="danger">{feil}</Alert>;
  if (!begrep) return <Spinner aria-label="Laster …" data-size="sm" />;

  return (
    <>
      <Metatekst as="nav" aria-label="Brødsmulesti" style={{ display: 'flex', gap: '0.4rem', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.6rem', flexWrap: 'wrap' }}>
        <Link asChild><RouterLink to="/begreper">Begreper</RouterLink></Link>
        <span>/</span>
        <span style={{ color: 'var(--ds-color-neutral-text-default)' }}>«{begrep.term}»</span>
      </Metatekst>

      <Heading level={1} data-size="lg">
        «{begrep.term}»
      </Heading>
      <Paragraph style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap', margin: '0.5rem 0 1.5rem' }}>
        <Tag data-color="info" data-size="sm">{begrep.status}</Tag>
        {begrep.begrepskategori === 'virksomhet' && <Tag data-color="success" data-size="sm">Virksomhet-navneform</Tag>}
        {/* [Ny, navneformgrunn-runden, 2026-09-07] Grunnen står PÅ statuslinjen rett under H1 (docs/09
          * §6-mønsteret: Paragraph som wrapper med Tag-er inni) — for en navneform er «hvorfor peker
          * dette hit» like viktig identitetsinformasjon som selve kategorien ved siden av. */}
        {begrep.begrepskategori === 'virksomhet' && <NavneformgrunnTag grunn={begrep.navneformgrunn} visUspesifisert />}
        {begrep.begrepskategori === 'gruppe' && <Tag data-color="success" data-size="sm">Gruppebegrep</Tag>}
        {/* [Ny, issue #203 pkt. 2] */}
        {begrep.begrepskategori === 'administrativ_inndeling' && <Tag data-color="success" data-size="sm">Administrativ inndeling</Tag>}
        <Metatekst as="span" style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>
          Eier: {visEier(begrep.virksomhetId)}
        </Metatekst>
      </Paragraph>

      <Tabs value={fane} onChange={(v) => setFane(v as Fane)} style={{ marginBottom: '1rem' }}>
        <Tabs.List>
          <Tabs.Tab value="grunndata">Grunndata</Tabs.Tab>
          <Tabs.Tab value="bruk">Bruk</Tabs.Tab>
          {(begrep.begrepskategori === 'gruppe' || definisjonsrelasjoner.length > 0) && (
            <Tabs.Tab value="relasjoner">Relasjoner</Tabs.Tab>
          )}
        </Tabs.List>
      </Tabs>

      {fane === 'grunndata' && (
      <>
      {(begrep.begrepskategori === 'virksomhet' || begrep.begrepskategori === 'gruppe'
        || begrep.begrepskategori === 'administrativ_inndeling') && (
        <section style={{ marginBottom: '1.5rem' }}>
          <Heading level={3} data-size="xs" style={{ marginBottom: '0.75rem' }}>
            Lenket til
          </Heading>
          {begrep.begrepskategori === 'virksomhet' && begrep.virksomhetReferanseId && (
            <Paragraph>
              Navneform for{' '}
              <Link asChild>
                <RouterLink to={`/virksomheter/${begrep.virksomhetReferanseId}`}>{visEier(begrep.virksomhetReferanseId)}</RouterLink>
              </Link>
            </Paragraph>
          )}
          {/* [Ny, issue #203 pkt. 2] Samme visning/lenkevalg som gruppebegrep under — administrativ
            * inndeling har nøyaktig samme (Term, LovkildeId)-scoping og samme LovreferanseEid-mønster. */}
          {(begrep.begrepskategori === 'gruppe' || begrep.begrepskategori === 'administrativ_inndeling') && begrep.lovkildeId && (
            <Paragraph>
              {begrep.begrepskategori === 'gruppe' ? 'Gruppebegrep hjemlet i' : 'Administrativ inndeling hjemlet i'}{' '}
              {(() => {
                const lov = rettskilder.find((r) => r.id === begrep.lovkildeId);
                if (!lov) return <span>{begrep.lovkildeId}</span>;
                // [Rettet, 2026-08-30] Lenk til NØYAKTIG paragrafen (via lovreferanseEid, satt
                // automatisk ved godkjenning fra en navnekandidat, se OpprettGruppebegrepAsync) når
                // den finnes — en bar /rettskilder/{id}-lenke uten eid velger ingen node og lander
                // på en tom side (Johann observerte nettopp dette for «Statsforvalteren»). Faller
                // tilbake til en lenke til hele loven (uten valgt node) for eldre/manuelt opprettede
                // gruppebegrep uten kjent opprinnelsesparagraf.
                const nodeHref = begrep.lovreferanseEid ? rettskildeLenke(begrep.lovreferanseEid, rettskilder) : null;
                return (
                  <Link asChild>
                    <RouterLink to={nodeHref ?? `/rettskilder/${lov.id}`}>{lov.tittel}</RouterLink>
                  </Link>
                );
              })()}
            </Paragraph>
          )}
        </section>
      )}
      </>
      )}

      {fane === 'relasjoner' && (
      <>
      {/* [Ny, gruppemedlemskap-runden, 2026-09-08, issue #164] Drill-through fra gruppebegrepet til
        * det gruppen faktisk INNEHOLDER — begge nivåene (medlemsgrupper og konkrete virksomheter) og
        * retningen oppover. Uten denne var et gruppebegrep en blindvei: siden viste hva gruppen ER
        * hjemlet i, men aldri hvem som er i den. Se `GruppeMedlemmer` for hvorfor det er tre lister. */}
      {begrep.begrepskategori === 'gruppe' && id && (
        <GruppeMedlemmer gruppeBegrepId={id} rettskilder={rettskilder} />
      )}
      </>
      )}

      {fane === 'grunndata' && (
      <>
      <section style={{ marginBottom: '2rem' }}>
        <Heading level={3} data-size="xs" style={{ marginBottom: '0.75rem' }}>
          Egenskaper
        </Heading>
        <form onSubmit={lagre} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', maxWidth: '40rem' }}>
          <Textfield label="Term" value={term} onChange={(e) => setTerm(e.target.value)} required />
          {begrep.begrepskategori !== 'virksomhet' && begrep.begrepskategori !== 'gruppe'
            && begrep.begrepskategori !== 'administrativ_inndeling' && (
            <Field>
              <Label>Definisjon</Label>
              <Textarea value={definisjon} onChange={(e) => setDefinisjon(e.target.value)} rows={3} required />
            </Field>
          )}
          <Textfield label="Lovreferanse (eId)" value={lovreferanseEid} onChange={(e) => setLovreferanseEid(e.target.value)}
            style={{ fontFamily: 'monospace' }} />
          {begrep.lovreferanseEid && (
            <Metatekst style={{ marginTop: '-0.5rem', display: 'flex', gap: '0.4rem', alignItems: 'baseline', flexWrap: 'wrap' }}>
              {(() => {
                // [Rettet, 2026-09-02] Vis rettskildens navn som lenketekst (mer interessant enn den
                // rå eId-en, Johann) — eId-en beholdes fortsatt synlig, bare som liten metatekst ved
                // siden av, for presis sporbarhet.
                const rettskilde = finnRettskildeForEid(begrep.lovreferanseEid, rettskilder);
                const href = rettskildeLenke(begrep.lovreferanseEid, rettskilder);
                if (!rettskilde || !href) {
                  return <span style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>Fant ikke rettskilden for denne eId-en.</span>;
                }
                return (
                  <>
                    <Link asChild><RouterLink to={href}>{rettskilde.tittel}</RouterLink></Link>
                    <Metatekst as="span" style={{ fontFamily: 'monospace', color: 'var(--ds-color-neutral-text-subtle)' }}>
                      ({begrep.lovreferanseEid})
                    </Metatekst>
                  </>
                );
              })()}
            </Metatekst>
          )}
          {begrep.begrepskategori !== 'virksomhet' && begrep.begrepskategori !== 'gruppe'
            && begrep.begrepskategori !== 'administrativ_inndeling' && (
            <Field>
              <Label>Begrepstype</Label>
              <Select value={begrepstype} onChange={(e) => setBegrepstype(e.target.value)}>
                <Select.Option value="faktabegrep">Faktabegrep</Select.Option>
                <Select.Option value="handlingsbegrep">Handlingsbegrep</Select.Option>
              </Select>
            </Field>
          )}
          {lagreFeil && <Alert data-color="danger">{lagreFeil}</Alert>}
          <div>
            <Button data-size="sm" type="submit" disabled={lagrer}>{lagrer ? 'Lagrer …' : 'Lagre'}</Button>
          </div>
        </form>
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={3} data-size="xs" style={{ marginBottom: '0.75rem' }}>
          Status
        </Heading>
        <StatusStepper status={begrep.status} onChange={endreStatus} disabled={statusEndres} />
      </section>
      </>
      )}

      {fane === 'bruk' && (
      <>
      <section style={{ marginBottom: '2rem' }}>
        <Heading level={3} data-size="xs" style={{ marginBottom: '0.75rem' }}>
          Brukt i vilkår
        </Heading>
        <Card style={{ padding: bruktIVilkar.length > 0 ? 0 : '1rem', overflow: 'hidden' }}>
          {bruktIVilkar.length === 0 ? (
            <Paragraph style={{ margin: 0 }}>Ikke brukt i noe vilkår ennå.</Paragraph>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem', padding: '0.75rem' }}>
              {bruktIVilkar.map(({ vilkar, rotnodeId }) =>
                rotnodeId ? (
                  <Link asChild key={vilkar.id}>
                    <RouterLink to={`/vilkarstre/${rotnodeId}?fokusVilkar=${vilkar.id}`}>{vilkar.tittel}</RouterLink>
                  </Link>
                ) : (
                  <span key={vilkar.id}>{vilkar.tittel}</span>
                ),
              )}
            </div>
          )}
        </Card>
      </section>

      <section style={{ marginBottom: '2rem' }}>
        {(() => {
          // [Ny, 2026-09-02, Fiks 1+2] EKTE, taggkoblede forekomster — opprettet automatisk ved
          // godkjenning (BegrepsforekomstTjeneste.GodkjennAsync) for andre eksakte forekomster av
          // termen i SAMME rettskilde som definisjonen. Til forskjell fra seksjonen under er dette
          // strukturelle koblinger, ikke bare tekstlig sammenfall.
          const definerendeRettskilde = begrep.lovreferanseEid ? finnRettskildeForEid(begrep.lovreferanseEid, rettskilder) : undefined;
          const rettskildeNavn =
            taggedeForekomster[0]?.rettskildeTittel ?? definerendeRettskilde?.tittel;
          return (
            <Heading level={3} data-size="xs" style={{ marginBottom: '0.75rem' }}>
              {rettskildeNavn ? `Forekomster i ${rettskildeNavn}` : 'Forekomster (taggkoblet)'}
            </Heading>
          );
        })()}
        <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginTop: '-0.5rem', marginBottom: '0.75rem' }}>
          Steder i den definerende rettskilden som er EKTE, bekreftede koblinger til akkurat dette begrepet (samme
          tekstmerkings-mekanisme som resten av appen), ikke bare et tekstlig sammenfall.
        </Metatekst>
        <Card style={{ padding: taggedeForekomster.length > 0 ? 0 : '1rem', overflow: 'hidden' }}>
          {taggedeForekomster.length === 0 ? (
            <Paragraph style={{ margin: 0 }}>Ingen andre taggkoblede forekomster funnet i den definerende rettskilden ennå.</Paragraph>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', padding: '0.75rem' }}>
              {taggedeForekomster.map((t) => (
                <div key={t.taggId}>
                  <Link asChild>
                    <RouterLink to={rettskildeLenkeForId(t.rettskildeId, t.nodeEid)}>{t.rettskildeTittel}</RouterLink>
                  </Link>
                  <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', margin: 0 }}>
                    {t.quotePrefix}<strong style={{ color: 'var(--ds-color-neutral-text-default)' }}>{t.quoteExact}</strong>{t.quoteSuffix}
                  </Metatekst>
                </div>
              ))}
            </div>
          )}
        </Card>
      </section>
      </>
      )}

      {/* [Ny, #212, 2026-09-10] AC5 — «også definert i N andre rettskilder». Kjernebeslutningen (Johann
        * 2026-09-09): ett begrep per forskrift, ALDRI slått sammen til én rad (NTNUs og MFs definisjon
        * er to ulike tekster med ulik hjemmel og ulik fastsetter) — denne seksjonen gjør likheten
        * SPØRRBAR (docs/32 §3 S5/S6) uten å late som forskjellen forsvinner: hver relatert rad er et
        * eget, selvstendig begrep med sin egen lenke, ikke et sammenslått felt.
        * [ENDRET, issue #276] Hører til "Relasjoner"-fanen (samme fane som GruppeMedlemmer over). */}
      {fane === 'relasjoner' && definisjonsrelasjoner.length > 0 && (
        <section style={{ marginBottom: '2rem' }}>
          <Heading level={3} data-size="xs" style={{ marginBottom: '0.75rem' }}>
            Også definert i {definisjonsrelasjoner.length} {definisjonsrelasjoner.length === 1 ? 'annen rettskilde' : 'andre rettskilder'}
          </Heading>
          <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', marginTop: '-0.5rem', marginBottom: '0.75rem' }}>
            Samme definisjonstekst (eksakt lik etter normalisering) funnet i en ANNEN forskrift, bekreftet av en
            saksbehandler — se det relaterte begrepet for dets egen ordlyd og hjemmel. Ikke slått sammen til ett
            begrep her (issue #212).
          </Metatekst>
          <Card style={{ padding: 0, overflow: 'hidden' }}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', padding: '0.75rem' }}>
              {definisjonsrelasjoner.map((r) => {
                const href = r.relatertLovreferanseEid ? rettskildeLenke(r.relatertLovreferanseEid, rettskilder) : null;
                return (
                  <div key={r.relatertBegrepId}>
                    <Link asChild>
                      <RouterLink to={`/begreper/${r.relatertBegrepId}`}>«{r.relatertTerm}»</RouterLink>
                    </Link>
                    {href && (
                      <>
                        {' — '}
                        <Link asChild>
                          <RouterLink to={href}>{finnRettskildeForEid(r.relatertLovreferanseEid ?? '', rettskilder)?.tittel ?? 'paragraf'}</RouterLink>
                        </Link>
                      </>
                    )}
                  </div>
                );
              })}
            </div>
          </Card>
        </section>
      )}

      {fane === 'bruk' && (
      <section>
        <Heading level={3} data-size="xs" style={{ marginBottom: '0.75rem' }}>
          Andre steder ordet forekommer i korpuset
        </Heading>
        <Alert data-color="warning" data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Dette er IKKE bekreftede koblinger til dette begrepet — kun et rått tekstsøk etter «{begrep.term}» på tvers av
          ALLE importerte rettskilder (ordgrense-avgrenset, case-insensitivt). Samme ord brukt et annet sted kan gjelde
          et helt annet begrep. Se seksjonen over for de ekte, taggkoblede forekomstene.
        </Alert>
        <Card style={{ padding: bruktIRettskilder.length > 0 ? 0 : '1rem', overflow: 'hidden' }}>
          {bruktIRettskilder.length === 0 ? (
            <Paragraph style={{ margin: 0 }}>Ingen forekomster funnet i importert lovtekst ennå.</Paragraph>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', padding: '0.75rem' }}>
              {bruktIRettskilder.map((r) => (
                <div key={`${r.rettskildeId}-${r.nodeEid}`}>
                  <Link asChild>
                    <RouterLink to={rettskildeLenkeForId(r.rettskildeId, r.nodeEid)}>{r.rettskildeTittel}</RouterLink>
                  </Link>
                  <Metatekst style={{ color: 'var(--ds-color-neutral-text-subtle)', margin: 0 }}>
                    {r.snippet}
                  </Metatekst>
                </div>
              ))}
            </div>
          )}
        </Card>
      </section>
      )}
    </>
  );
}
