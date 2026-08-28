import { useEffect, useMemo, useState } from 'react';
import {
  Alert, Button, Checkbox, Details, Field, Heading, Label, Paragraph, Tag, Textarea, Textfield,
} from '@digdir/designsystemet-react';
import { Link as RouterLink } from 'react-router';
import { ApiError, api } from '../api/client';
import type {
  RettskildeSammendrag, TjenesteTverrTenantTreffDto, VirksomhetDto,
} from '../api/types';
import { useBruker } from '../bruker/BrukerContext';
import { RettskildeVelger } from '../rettskilde/RettskildeVelger';
import { VirksomhetVelger } from '../virksomhet/VirksomhetVelger';
import { gjettRettskildeSokeord, gjettVirksomhetSokeord, konverterRettighet } from '../import/konverterModelleksport';
import { tolkModelleksportJson, type RaaAvhengighet, type RaaRettighet } from '../import/modelleksportTyper';
import { FELTVISNING_DEFAULT, type FeltvisningValg, type GrafKantLik, type GrafNodeLik } from '../graf/grafFelles';
import { TjenesteGrafCanvas } from '../graf/TjenesteGrafCanvas';

interface ReferanseTilstand {
  lov: string | null;
  henvisning: string | null;
  felt: string | null;
  rettskildeId: string;
  eid: string;
  utelatt: boolean;
}

interface RettighetTilstand {
  malVirksomhetId: string;
  tverrTenantSok: string;
  tverrTenantTreff: TjenesteTverrTenantTreffDto[];
  sokerTverrTenant: boolean;
  koblerTilEksisterendeId: string | null;
  referanser: ReferanseTilstand[];
  opprettetTjenesteId: string | null;
  advarsler: string[];
  feil: string | null;
  oppretter: boolean;
  /** [Ny, 2026-08-28] Sletter-i-gang-indikator for "Slett"-knappen — se `slettForslag`. */
  sletterForslag: boolean;
}

function nyTilstand(raa: RaaRettighet, virksomheter: VirksomhetDto[], egenVirksomhetId: string): RettighetTilstand {
  const gjettetNavn = gjettVirksomhetSokeord(raa.kompetent_myndighet).toLowerCase();
  const gjettetTreff = gjettetNavn
    ? virksomheter.find((v) => v.navn.toLowerCase().includes(gjettetNavn) || gjettetNavn.includes(v.navn.toLowerCase()))
    : undefined;
  return {
    malVirksomhetId: gjettetTreff?.id ?? egenVirksomhetId,
    tverrTenantSok: '',
    tverrTenantTreff: [],
    sokerTverrTenant: false,
    koblerTilEksisterendeId: null,
    referanser: raa.regelverksreferanser.map((r) => ({
      lov: r.lov, henvisning: r.henvisning, felt: r.felt, rettskildeId: '', eid: '', utelatt: false,
    })),
    opprettetTjenesteId: null,
    advarsler: [],
    feil: null,
    oppretter: false,
    sletterForslag: false,
  };
}

/** Ferdigresolvert id for rettighet nr. `i` — koblet ELLER nyopprettet, `null` = ikke ferdig ennå. */
function ferdigId(t: RettighetTilstand): string | null {
  return t.opprettetTjenesteId ?? t.koblerTilEksisterendeId;
}

/**
 * [Ny, 2026-08-28] Import-wizard for modelleksport-JSON (`{ rettigheter: [...] }`, se
 * docs/23-tjeneste-modell-eksport-og-skjema.md) — løser de to harde problemene docs/23 §6 flagget:
 * navn→ekte FK (virksomhet/rettskilde/motpart-tjeneste) og koblet-vs-duplisert. Se
 * docs/21-feltmapping-eksterne-kilder.md for selve mapping-reglene. INGEN fuzzy-matching — hvert
 * forhåndsutfylt forslag er kun det, mennesket bekrefter/søker/velger selv (samme "ingen gjettet
 * fallback"-holdning som resten av huset).
 */
export default function ImportWizard() {
  const { gjeldendeBruker } = useBruker();
  const [raaTekst, setRaaTekst] = useState('');
  const [rettigheter, setRettigheter] = useState<RaaRettighet[] | null>(null);
  const [parseFeil, setParseFeil] = useState<string | null>(null);
  const [tilstander, setTilstander] = useState<RettighetTilstand[]>([]);

  const [virksomheter, setVirksomheter] = useState<VirksomhetDto[]>([]);
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);

  const [bulkKjorer, setBulkKjorer] = useState(false);
  const [bulkFremdrift, setBulkFremdrift] = useState<{ ferdig: number; totalt: number } | null>(null);

  const [visGraf, setVisGraf] = useState(false);
  const [grafInkludererHandlinger, setGrafInkludererHandlinger] = useState(false);
  const [grafFelt, setGrafFelt] = useState<FeltvisningValg>(FELTVISNING_DEFAULT);

  useEffect(() => {
    api.hentVirksomheter().then(setVirksomheter).catch(() => setVirksomheter([]));
    api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([]));
  }, []);

  function lastOpp() {
    setParseFeil(null);
    try {
      const parset = tolkModelleksportJson(raaTekst);
      setRettigheter(parset);
      setTilstander(parset.map((r) => nyTilstand(r, virksomheter, gjeldendeBruker?.virksomhetId ?? '')));
    } catch (err) {
      setParseFeil(err instanceof Error ? err.message : 'Ukjent feil ved tolkning av filen.');
      setRettigheter(null);
    }
  }

  function oppdater(i: number, delvis: Partial<RettighetTilstand>) {
    setTilstander((forrige) => forrige.map((t, idx) => (idx === i ? { ...t, ...delvis } : t)));
  }

  function oppdaterReferanse(i: number, j: number, delvis: Partial<ReferanseTilstand>) {
    setTilstander((forrige) => forrige.map((t, idx) => {
      if (idx !== i) return t;
      return { ...t, referanser: t.referanser.map((r, ridx) => (ridx === j ? { ...r, ...delvis } : r)) };
    }));
  }

  async function sokTverrTenant(i: number, sok: string) {
    oppdater(i, { tverrTenantSok: sok, sokerTverrTenant: true });
    try {
      const treff = sok.trim() ? await api.sokTjenesterTverrTenant(sok.trim()) : [];
      oppdater(i, { tverrTenantTreff: treff, sokerTverrTenant: false });
    } catch {
      oppdater(i, { tverrTenantTreff: [], sokerTverrTenant: false });
    }
  }

  async function opprettRettighet(i: number) {
    if (!rettigheter) return;
    const raa = rettigheter[i];
    const t = tilstander[i];
    if (!t.malVirksomhetId) {
      oppdater(i, { feil: 'Velg en mål-virksomhet først.' });
      return;
    }
    oppdater(i, { oppretter: true, feil: null });
    try {
      const { request, handlinger, advarsler } = konverterRettighet(raa);
      const regelverksreferanser = t.referanser
        .filter((r) => !r.utelatt && r.rettskildeId && r.eid.trim())
        .map((r) => ({ tilRettskildeId: r.rettskildeId, tilEid: r.eid.trim(), felt: r.felt }));
      const utelattReferanser = t.referanser.filter((r) => r.utelatt || !r.rettskildeId || !r.eid.trim()).length;
      const alleAdvarsler = [
        ...advarsler,
        ...(utelattReferanser > 0 ? [`${utelattReferanser} regelverksreferanse(r) ble utelatt (ikke koblet til en ekte rettskilde-node).`] : []),
      ];
      const opprettet = await api.importerRettighet(t.malVirksomhetId, { tjeneste: request, handlinger, regelverksreferanser });
      oppdater(i, { opprettetTjenesteId: opprettet.id, advarsler: alleAdvarsler, oppretter: false });
    } catch (err) {
      oppdater(i, { feil: err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse.', oppretter: false });
    }
  }

  /** [Ny, 2026-08-28] Angre en nettopp opprettet rettighet — KUN mens den fortsatt står som et
   * ubehandlet forslag (se `TjenesteregisterTjeneste.SlettForslagAsync` på serveren), som en
   * nyopprettet import-rad alltid gjør inntil noen faktisk har validert/publisert/redigert den.
   * Bevisst KUN tilgjengelig for `opprettetTjenesteId` (denne wizard-kjøringen faktisk OPPRETTET
   * tjenesten) — aldri for en rad som ble KOBLET til en allerede eksisterende tjeneste
   * (`koblerTilEksisterendeId`), siden den tjenesten kan være ekte, verdifullt innhold fra før
   * wizarden noensinne rørte den. Setter tilbake til "ikke ferdig" ved suksess, slik at raden kan
   * rettes opp (f.eks. annen mål-virksomhet) og importeres på nytt. */
  async function slettForslag(i: number) {
    const t = tilstander[i];
    if (!t.opprettetTjenesteId) return;
    oppdater(i, { sletterForslag: true, feil: null });
    try {
      await api.slettTjenesteforslag(t.opprettetTjenesteId);
      oppdater(i, { opprettetTjenesteId: null, sletterForslag: false, advarsler: [] });
    } catch (err) {
      oppdater(i, { feil: err instanceof ApiError ? err.message : 'Ukjent feil ved sletting.', sletterForslag: false });
    }
  }

  /** [Ny, 2026-08-28] Bulk-opprettelse — nødvendig fra 69+ rettigheter/import (én-om-gangen holdt
   * ikke skala). Kjører sekvensielt (ikke parallelt — unngår å hamre databasen med 69 samtidige
   * skrivinger) gjennom hver GJENSTÅENDE rad med DENS EGNE, allerede satte valg (mål-virksomhet fra
   * gjettingen, evt. manuelt justerte referanser) — endrer ingenting ved raden selv, kun trigger
   * opprettelsen. Feil på én rad stopper IKKE resten (samme "fortsett forbi feil"-holdning som
   * avhengighet-bulken under) — feilen vises der den alltid har vist seg, i radens egen Alert.</summary> */
  async function opprettAlleGjenstaende() {
    if (!rettigheter) return;
    const indekser = tilstander
      .map((t, i) => (ferdigId(t) ? null : i))
      .filter((i): i is number => i !== null);
    setBulkKjorer(true);
    setBulkFremdrift({ ferdig: 0, totalt: indekser.length });
    for (const i of indekser) {
      await opprettRettighet(i);
      setBulkFremdrift((f) => (f ? { ...f, ferdig: f.ferdig + 1 } : f));
    }
    setBulkKjorer(false);
  }

  /** [Ny, 2026-08-28] Bulk-angring — motstykket til `opprettAlleGjenstaende`, samme "sekvensielt,
   * fortsett forbi feil"-holdning. Til opprydding etter en stor test-import (69+ rettigheter):
   * KUN radene DENNE wizard-kjøringen faktisk opprettet (`opprettetTjenesteId`), aldri koblede rader
   * (se `slettForslag`). */
  async function slettAlleOpprettede() {
    const indekser = tilstander
      .map((t, i) => (t.opprettetTjenesteId ? i : null))
      .filter((i): i is number => i !== null);
    setBulkKjorer(true);
    setBulkFremdrift({ ferdig: 0, totalt: indekser.length });
    for (const i of indekser) {
      await slettForslag(i);
      setBulkFremdrift((f) => (f ? { ...f, ferdig: f.ferdig + 1 } : f));
    }
    setBulkKjorer(false);
  }

  const alleFerdige = tilstander.length > 0 && tilstander.every((t) => ferdigId(t) !== null);
  const antallGjenstaende = tilstander.filter((t) => !ferdigId(t)).length;
  const antallSlettbare = tilstander.filter((t) => t.opprettetTjenesteId).length;

  /** In-memory graf-forhåndsvisning — FØR noe er persistert. Gjenbruker
   * `byggAvhengighetKandidater` (definert under) med et identitetskart (navn→navn) i stedet for
   * ekte id-er, siden ingenting har fått en ekte GUID ennå. Kun til visuell sjekk av strukturen —
   * "ser dette riktig ut" — ingen skriving skjer her. */
  const inMemoryGraf = useMemo<{ noder: GrafNodeLik[]; kanter: GrafKantLik[] } | null>(() => {
    if (!rettigheter) return null;
    const identitet = new Map<string, string>();
    rettigheter.forEach((r) => identitet.set(r.navn.trim().toLowerCase(), r.navn));
    const kandidater = byggAvhengighetKandidater(rettigheter, identitet);
    const noder: GrafNodeLik[] = rettigheter.map((r) => ({
      id: r.navn, navn: r.navn, erHandling: false, type: r.type,
      kompetentMyndighet: r.kompetent_myndighet, livshendelser: r.livshendelser, status: r.status,
    }));
    const kanter: GrafKantLik[] = kandidater
      .filter((k) => k.malType === 'tjeneste' && k.fraId && k.tilId)
      .map((k) => ({ fraId: k.fraId!, tilId: k.tilId!, rel: k.rel, erHandlingTilhorighet: false }));
    if (grafInkludererHandlinger) {
      rettigheter.forEach((r) => {
        r.handlinger.forEach((h, hi) => {
          const handlingId = `${r.navn}::${h.navn}::${hi}`;
          noder.push({ id: handlingId, navn: h.navn, erHandling: true, type: h.handlingstype, kompetentMyndighet: null, livshendelser: [], status: null });
          kanter.push({ fraId: r.navn, tilId: handlingId, rel: 'har_handling', erHandlingTilhorighet: true });
        });
      });
    }
    return { noder, kanter };
  }, [rettigheter, grafInkludererHandlinger]);

  // navn (lowercase, trim) → ferdig ekte id, for batch-intern avhengighet-resolusjon.
  const navnTilId = useMemo(() => {
    const kart = new Map<string, string>();
    rettigheter?.forEach((r, i) => {
      const id = ferdigId(tilstander[i]);
      if (id) kart.set(r.navn.trim().toLowerCase(), id);
    });
    return kart;
  }, [rettigheter, tilstander]);

  return (
    <>
      <Heading level={1} data-size="lg">Importer modelleksport-JSON</Heading>
      <Paragraph style={{ marginBottom: '1rem', maxWidth: '48rem' }}>
        Lim inn en modelleksport-JSON med ett eller flere rettigheter (samme form som «Vis JSON» på
        en tjeneste, eller GET /api/tjenester/modelleksport) — for hver: velg mål-virksomhet, koble
        regelverksreferanser til ekte rettskilde-noder eller til en allerede eksisterende tjeneste i
        stedet for å opprette en duplikat. Bruk «Importer alle N gjenstående rettigheter» for å
        opprette dem samlet i stedet for én om gangen. Velger du en ANNEN virksomhet enn din egen,
        lander rettigheten som et forslag i den virksomhetens{' '}
        <RouterLink to="/tjenester/forslag">forslagskø</RouterLink> — ikke direkte som gjeldende
        innhold.
      </Paragraph>

      {!rettigheter && (
        <div style={{ maxWidth: '48rem' }}>
          <Field>
            <Label>Modelleksport-JSON</Label>
            <Textarea
              value={raaTekst}
              onChange={(e) => setRaaTekst(e.target.value)}
              rows={12}
              style={{ fontFamily: 'monospace', fontSize: 'var(--ds-font-size-1)' }}
            />
          </Field>
          {parseFeil && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{parseFeil}</Alert>}
          <Button style={{ marginTop: '0.75rem' }} onClick={lastOpp} disabled={!raaTekst.trim()}>
            Tolk JSON
          </Button>
        </div>
      )}

      {rettigheter && (
        <>
          <Paragraph style={{ marginBottom: '0.75rem' }}>
            Fant {rettigheter.length} rettighet(er). {tilstander.filter((t) => ferdigId(t)).length} av {rettigheter.length} klare.
          </Paragraph>

          <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center', marginBottom: '1rem', flexWrap: 'wrap' }}>
            <Button variant="secondary" onClick={() => setVisGraf((v) => !v)}>
              {visGraf ? 'Skjul graf-forhåndsvisning' : 'Forhåndsvis som graf (før noe opprettes)'}
            </Button>
            {antallGjenstaende > 0 && (
              <Button onClick={opprettAlleGjenstaende} disabled={bulkKjorer}>
                {bulkKjorer && bulkFremdrift
                  ? `Importerer … (${bulkFremdrift.ferdig}/${bulkFremdrift.totalt})`
                  : `Importer alle ${antallGjenstaende} gjenstående rettigheter`}
              </Button>
            )}
            {antallSlettbare > 0 && (
              <Button variant="secondary" data-color="danger" onClick={slettAlleOpprettede} disabled={bulkKjorer}>
                {bulkKjorer && bulkFremdrift
                  ? `Sletter … (${bulkFremdrift.ferdig}/${bulkFremdrift.totalt})`
                  : `Slett alle ${antallSlettbare} opprettede (angre denne importen)`}
              </Button>
            )}
          </div>

          {visGraf && inMemoryGraf && (
            <div style={{ marginBottom: '1.5rem' }}>
              <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.5rem' }}>
                In-memory forhåndsvisning av HELE den opplastede filen (kun batch-interne
                tjeneste↔tjeneste-avhengigheter — eksterne referanser og ikke-navngitte treff vises
                ikke her). Ingenting er lagret ennå.
              </Paragraph>
              <Checkbox
                label="Inkluder handlinger"
                checked={grafInkludererHandlinger}
                onChange={(e) => setGrafInkludererHandlinger(e.target.checked)}
                style={{ marginBottom: '0.5rem' }}
              />
              <TjenesteGrafCanvas noder={inMemoryGraf.noder} kanter={inMemoryGraf.kanter} felt={grafFelt} onFeltChange={setGrafFelt} hoyde="55vh" />
            </div>
          )}

          {rettigheter.map((raa, i) => {
            const t = tilstander[i];
            const ferdig = ferdigId(t);
            return (
              <Details key={raa.navn + i} style={{ marginBottom: '0.5rem' }}>
                <Details.Summary>
                  {raa.navn}{' '}
                  {ferdig && <Tag data-color="success" data-size="sm">{t.opprettetTjenesteId ? 'Opprettet' : 'Koblet til eksisterende'}</Tag>}
                </Details.Summary>
                <Details.Content>
                  <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
                    {raa.formal ?? '(ingen formålstekst)'} — kompetent myndighet (fritekst, uendret): «{raa.kompetent_myndighet ?? '—'}»
                  </Paragraph>

                  {t.opprettetTjenesteId && (
                    <>
                      {t.feil && <Alert data-color="danger" style={{ marginBottom: '0.5rem' }}>{t.feil}</Alert>}
                      <Button
                        variant="secondary" data-color="danger" data-size="sm"
                        onClick={() => slettForslag(i)} disabled={t.sletterForslag}
                        style={{ marginBottom: '0.75rem' }}
                      >
                        {t.sletterForslag ? 'Sletter …' : 'Slett (angre denne rettigheten)'}
                      </Button>
                    </>
                  )}

                  {!ferdig && (
                    <>
                      <div style={{ marginBottom: '0.75rem', maxWidth: '24rem' }}>
                        <VirksomhetVelger
                          virksomheter={virksomheter}
                          value={t.malVirksomhetId}
                          onChange={(id) => oppdater(i, { malVirksomhetId: id })}
                          label="Mål-virksomhet (eier av den nye tjenesten)"
                          tomValgTekst="Velg virksomhet …"
                        />
                        {gjeldendeBruker && t.malVirksomhetId && t.malVirksomhetId !== gjeldendeBruker.virksomhetId && (
                          <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-warning-text-default)' }}>
                            Lander som forslag i denne virksomhetens kø — ikke direkte gjeldende.
                          </Paragraph>
                        )}
                      </div>

                      <div style={{ marginBottom: '0.75rem' }}>
                        <Textfield
                          label="Finnes tjenesten allerede? Søk (tvers av virksomheter)"
                          value={t.tverrTenantSok}
                          onChange={(e) => sokTverrTenant(i, e.target.value)}
                          style={{ maxWidth: '24rem' }}
                        />
                        {t.tverrTenantTreff.length > 0 && (
                          <ul style={{ marginTop: '0.3rem' }}>
                            {t.tverrTenantTreff.map((tr) => (
                              <li key={tr.id} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                                <span>{tr.tittel} ({tr.virksomhetNavn})</span>
                                <Button data-size="sm" variant="secondary" onClick={() => oppdater(i, { koblerTilEksisterendeId: tr.id })}>
                                  Koble til denne i stedet
                                </Button>
                              </li>
                            ))}
                          </ul>
                        )}
                      </div>

                      {t.referanser.length > 0 && (
                        <div style={{ marginBottom: '0.75rem' }}>
                          <Paragraph style={{ fontWeight: 'var(--ds-font-weight-medium)', fontSize: 'var(--ds-font-size-1)' }}>
                            Regelverksreferanser
                          </Paragraph>
                          {t.referanser.map((r, j) => (
                            <div key={j} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: '0.4rem' }}>
                              <span style={{ fontSize: 'var(--ds-font-size-1)', minWidth: '12rem' }}>
                                «{r.lov ?? '—'}» {r.henvisning ? `→ ${r.henvisning}` : ''}{r.felt ? ` (felt: ${r.felt})` : ''}
                              </span>
                              {!r.utelatt && (
                                <>
                                  <RettskildeVelgerForsokFylt
                                    rettskilder={rettskilder}
                                    gjettSokeord={gjettRettskildeSokeord(r.lov)}
                                    value={r.rettskildeId}
                                    onChange={(id) => oppdaterReferanse(i, j, { rettskildeId: id })}
                                  />
                                  <Textfield
                                    aria-label="eId (paragraf)"
                                    placeholder="Paragraf/eId …"
                                    value={r.eid}
                                    onChange={(e) => oppdaterReferanse(i, j, { eid: e.target.value })}
                                    style={{ minWidth: '16rem', fontFamily: 'monospace' }}
                                  />
                                </>
                              )}
                              <Button data-size="sm" variant="tertiary" onClick={() => oppdaterReferanse(i, j, { utelatt: !r.utelatt })}>
                                {r.utelatt ? 'Ta med igjen' : 'Utelat'}
                              </Button>
                            </div>
                          ))}
                        </div>
                      )}

                      {t.feil && <Alert data-color="danger" style={{ marginBottom: '0.5rem' }}>{t.feil}</Alert>}
                      <Button onClick={() => opprettRettighet(i)} disabled={t.oppretter || !t.malVirksomhetId}>
                        {t.oppretter ? 'Oppretter …' : 'Opprett denne rettigheten'}
                      </Button>
                    </>
                  )}

                  {ferdig && t.advarsler.length > 0 && (
                    <Alert data-color="warning">
                      {t.advarsler.map((a, k) => <div key={k}>{a}</div>)}
                    </Alert>
                  )}
                </Details.Content>
              </Details>
            );
          })}

          {alleFerdige && rettigheter.some((r) => r.avhengigheter.length > 0) && (
            <AvhengigheterSeksjon rettigheter={rettigheter} navnTilId={navnTilId} />
          )}
        </>
      )}
    </>
  );
}

/** Rettskilde-velger som forhåndsutfyller søkefeltet med et gjettet søkeord — velgeren selv styrer valget. */
function RettskildeVelgerForsokFylt({
  rettskilder, gjettSokeord, value, onChange,
}: { rettskilder: RettskildeSammendrag[]; gjettSokeord: string; value: string; onChange: (id: string) => void }) {
  // RettskildeVelger søker selv (useRettskildeSok) — vi kan ikke forhåndsutfylle SØKETEKSTEN uten å
  // duplisere komponenten, men vi kan vise det gjettede søkeordet som en hint-tekst ved siden av.
  return (
    <div style={{ display: 'flex', gap: '0.4rem', alignItems: 'center' }}>
      <RettskildeVelger rettskilder={rettskilder} value={value} onChange={onChange} label="Rettskilde" />
      {gjettSokeord && !value && (
        <span style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
          (forslag: søk «{gjettSokeord}»)
        </span>
      )}
    </div>
  );
}

interface AvhengighetKandidat {
  key: string;
  fraNavn: string;
  fraId: string | null;
  tilNavn: string;
  tilId: string | null;
  rel: string;
  malType: 'tjeneste' | 'ekstern_referanse';
  organisasjonsnummer: string | null;
  kildeurl: string | null;
  merknad: string | null;
}

/**
 * Bygger de FAKTISKE, unike kantene fra alle rettigheters `avhengigheter[]` — modelleksport-formen
 * viser samme kant fra BEGGE endepunkters ståsted (samme som en ekte eksport ville), så en reciprok
 * "retning: til"-oppføring som allerede er dekket av en "retning: fra"-oppføring et annet sted i
 * batchen SKAL IKKE opprettes på nytt. Se docs/21 for begrunnelsen.
 */
function byggAvhengighetKandidater(rettigheter: RaaRettighet[], navnTilId: Map<string, string>): AvhengighetKandidat[] {
  const fraEntries: { fraNavn: string; a: RaaAvhengighet }[] = [];
  rettigheter.forEach((r) => r.avhengigheter.forEach((a) => {
    if (a.retning === 'fra') fraEntries.push({ fraNavn: r.navn, a });
  }));

  const erDekket = (ownNavn: string, a: RaaAvhengighet) =>
    fraEntries.some((f) =>
      f.a.rel === a.rel &&
      f.fraNavn.trim().toLowerCase() === a.mal_navn.trim().toLowerCase() &&
      f.a.mal_navn.trim().toLowerCase() === ownNavn.trim().toLowerCase());

  const finnId = (navn: string) => navnTilId.get(navn.trim().toLowerCase()) ?? null;

  const resultat: AvhengighetKandidat[] = [];
  const settInn = (fraNavn: string, a: RaaAvhengighet) => {
    resultat.push({
      key: `${fraNavn}|${a.rel}|${a.mal_navn}`,
      fraNavn,
      fraId: finnId(fraNavn),
      tilNavn: a.mal_navn,
      tilId: a.mal_type === 'tjeneste' ? finnId(a.mal_navn) : null,
      rel: a.rel,
      malType: a.mal_type,
      organisasjonsnummer: a.organisasjonsnummer ?? null,
      kildeurl: a.kildeurl ?? null,
      merknad: a.merknad ?? null,
    });
  };

  for (const { fraNavn, a } of fraEntries) settInn(fraNavn, a);

  // "til"-oppføringer uten en reciprok "fra"-oppføring et annet sted — inverter (den ANDRE tjenesten
  // er da faktisk kilden til kanten, selv om DENNE rettigheten er den som nevner den).
  rettigheter.forEach((r) => r.avhengigheter.forEach((a) => {
    if (a.retning === 'til' && !erDekket(r.navn, a)) {
      settInn(a.mal_navn, { ...a, retning: 'fra', mal_navn: r.navn });
    }
  }));

  return resultat;
}

function AvhengigheterSeksjon({ rettigheter, navnTilId }: { rettigheter: RaaRettighet[]; navnTilId: Map<string, string> }) {
  const [kandidater, setKandidater] = useState<AvhengighetKandidat[] | null>(null);
  const [opprettet, setOpprettet] = useState<Set<string>>(new Set());
  const [feilPerKant, setFeilPerKant] = useState<Map<string, string>>(new Map());
  const [oppretterKant, setOppretterKant] = useState<string | null>(null);
  const [tverrTenantSok, setTverrTenantSok] = useState<Map<string, string>>(new Map());
  const [tverrTenantTreff, setTverrTenantTreff] = useState<Map<string, TjenesteTverrTenantTreffDto[]>>(new Map());
  const [manueltValgtId, setManueltValgtId] = useState<Map<string, string>>(new Map());
  const [bulkKjorer, setBulkKjorer] = useState(false);
  const [bulkFremdrift, setBulkFremdrift] = useState<{ ferdig: number; totalt: number } | null>(null);

  useEffect(() => {
    setKandidater(byggAvhengighetKandidater(rettigheter, navnTilId));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function sok(key: string, tekst: string) {
    setTverrTenantSok((m) => new Map(m).set(key, tekst));
    const treff = tekst.trim() ? await api.sokTjenesterTverrTenant(tekst.trim()).catch(() => []) : [];
    setTverrTenantTreff((m) => new Map(m).set(key, treff));
  }

  async function opprettKant(k: AvhengighetKandidat) {
    if (!k.fraId) {
      setFeilPerKant((m) => new Map(m).set(k.key, `Fant ikke ekte id for «${k.fraNavn}» — den må opprettes/kobles først.`));
      return;
    }
    const tilId = k.malType === 'tjeneste' ? (k.tilId ?? manueltValgtId.get(k.key) ?? null) : null;
    if (k.malType === 'tjeneste' && !tilId) {
      setFeilPerKant((m) => new Map(m).set(k.key, `Fant ikke ekte id for «${k.tilNavn}» — søk og velg manuelt under.`));
      return;
    }
    setOppretterKant(k.key);
    setFeilPerKant((m) => { const ny = new Map(m); ny.delete(k.key); return ny; });
    try {
      await api.opprettTjenesteavhengighet(k.fraId, {
        tilTjenesteId: tilId,
        rel: k.rel,
        hendelseId: null,
        beskrivelse: k.merknad,
        tilOrganisasjonsnummer: k.malType === 'ekstern_referanse' ? k.organisasjonsnummer : undefined,
        tilNavn: k.malType === 'ekstern_referanse' ? k.tilNavn : undefined,
        tilUrl: k.malType === 'ekstern_referanse' ? k.kildeurl : undefined,
      });
      setOpprettet((s) => new Set(s).add(k.key));
    } catch (err) {
      setFeilPerKant((m) => new Map(m).set(k.key, err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av avhengighet.'));
    } finally {
      setOppretterKant(null);
    }
  }

  /** Bulk — kun kanter som IKKE trenger manuelt tverr-tenant-søk (allerede batch-internt resolvert,
   * eller ekstern referanse). De som faktisk mangler et treff må fortsatt løses enkeltvis under. */
  async function opprettAlleKanter() {
    if (!kandidater) return;
    const gjenstaende = kandidater.filter((k) =>
      !opprettet.has(k.key) && (k.malType === 'ekstern_referanse' || k.tilId !== null || manueltValgtId.has(k.key)));
    setBulkKjorer(true);
    setBulkFremdrift({ ferdig: 0, totalt: gjenstaende.length });
    for (const k of gjenstaende) {
      await opprettKant(k);
      setBulkFremdrift((f) => (f ? { ...f, ferdig: f.ferdig + 1 } : f));
    }
    setBulkKjorer(false);
  }

  if (!kandidater) return null;
  const antallKlareForBulk = kandidater.filter((k) =>
    !opprettet.has(k.key) && (k.malType === 'ekstern_referanse' || k.tilId !== null || manueltValgtId.has(k.key))).length;

  return (
    <>
      <Heading level={2} data-size="sm" style={{ marginTop: '1.5rem' }}>
        Avhengigheter ({kandidater.length} unike kanter funnet i importen)
      </Heading>
      {antallKlareForBulk > 0 && (
        <Button onClick={opprettAlleKanter} disabled={bulkKjorer} style={{ marginBottom: '0.75rem' }}>
          {bulkKjorer && bulkFremdrift
            ? `Oppretter … (${bulkFremdrift.ferdig}/${bulkFremdrift.totalt})`
            : `Opprett alle ${antallKlareForBulk} klare kanter`}
        </Button>
      )}
      {kandidater.map((k) => {
        const erOpprettet = opprettet.has(k.key);
        const feil = feilPerKant.get(k.key);
        const trengerManueltValg = k.malType === 'tjeneste' && !k.tilId && !manueltValgtId.get(k.key);
        return (
          <div key={k.key} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap', marginBottom: '0.4rem' }}>
            <span style={{ fontSize: 'var(--ds-font-size-1)', minWidth: '28rem' }}>
              «{k.fraNavn}» — {k.rel} → «{k.tilNavn}»{k.malType === 'ekstern_referanse' ? ' (ekstern referanse)' : ''}
            </span>
            {trengerManueltValg && !erOpprettet && (
              <>
                <Textfield
                  aria-label={`Søk «${k.tilNavn}»`}
                  placeholder={`Søk «${k.tilNavn}» …`}
                  value={tverrTenantSok.get(k.key) ?? ''}
                  onChange={(e) => sok(k.key, e.target.value)}
                  style={{ maxWidth: '16rem' }}
                />
                {(tverrTenantTreff.get(k.key) ?? []).map((tr) => (
                  <Button key={tr.id} data-size="sm" variant="secondary" onClick={() => setManueltValgtId((m) => new Map(m).set(k.key, tr.id))}>
                    Velg {tr.tittel}
                  </Button>
                ))}
              </>
            )}
            {!erOpprettet && (
              <Button data-size="sm" onClick={() => opprettKant(k)} disabled={oppretterKant === k.key}>
                {oppretterKant === k.key ? 'Oppretter …' : 'Opprett'}
              </Button>
            )}
            {erOpprettet && <Tag data-color="success" data-size="sm">Opprettet</Tag>}
            {feil && <span style={{ color: 'var(--ds-color-danger-text-default)', fontSize: 'var(--ds-font-size-1)' }}>{feil}</span>}
          </div>
        );
      })}
    </>
  );
}
