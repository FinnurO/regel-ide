import { useEffect, useMemo, useRef, useState } from 'react';
import { Link as RouterLink, useSearchParams } from 'react-router';
import { Button, Card, Checkbox, Field, Heading, Label, Link, Paragraph, Select, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenkeForId } from '../api/eidLenker';
import type { RettskildeNodeDto, RettskildeSammendrag, VirksomhetKandidatDto } from '../api/types';
import { Pagineringskontroll } from '../tabell/Pagineringskontroll';
import { usePaginering } from '../tabell/usePaginering';
import { useVirksomheter } from '../virksomhet/useVirksomheter';
import { VirksomhetVelger } from '../virksomhet/VirksomhetVelger';

type Sorteringskolonne = 'virksomhet' | 'rettskilde' | 'status' | 'opprettet';

const STATUS_FARGE: Record<string, 'neutral' | 'warning' | 'success' | 'danger'> = {
  Venter: 'warning',
  Godkjent: 'success',
  Avvist: 'danger',
};

/**
 * Kandidatliste (kravspek §4.2 pkt. 3/4) — sorterbar/filtrerbar på virksomhet, lov/forskrift og
 * status, med avkrysningsbokser for massegodkjenning/-avvisning. Filtreringen på rettskilde er
 * spesielt nyttig for massegodkjenning: begrenser handlingen til ÉN lov/forskrift av gangen (nyttig
 * til testformål og for høyfrekvente virksomheter med mange treff), i stedet for å godkjenne alle
 * ventende kandidater for en virksomhet i ett jafs.
 *
 * ?virksomhetId= i URL-en forhåndsvelger filteret — brukt av «Se alle kandidater»-lenken fra
 * VirksomhetDetalj.tsx.
 */
export default function VirksomhetKandidaterListe() {
  const [søkeparametre] = useSearchParams();
  const { virksomheter, visEier } = useVirksomheter();
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);

  const [virksomhetFilter, setVirksomhetFilter] = useState(søkeparametre.get('virksomhetId') ?? '');
  const [rettskildeFilter, setRettskildeFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState<'Venter' | 'Godkjent' | 'Avvist' | 'Alle'>('Venter');

  const [kandidater, setKandidater] = useState<VirksomhetKandidatDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [laster, setLaster] = useState(false);

  const [valgte, setValgte] = useState<Set<string>>(new Set());
  const [massehandlingKjorer, setMassehandlingKjorer] = useState(false);
  const [massehandlingFeil, setMassehandlingFeil] = useState<string | null>(null);

  const [sveipVirksomhetId, setSveipVirksomhetId] = useState('');
  const [sveiper, setSveiper] = useState(false);
  const [sveipFeil, setSveipFeil] = useState<string | null>(null);
  const [sveipResultat, setSveipResultat] = useState<{ funnet: number; nye: number } | null>(null);

  const [sortKolonne, setSortKolonne] = useState<Sorteringskolonne>('opprettet');
  const [sortStigende, setSortStigende] = useState(false);

  useEffect(() => {
    api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([]));
  }, []);

  // Node-tekst per rettskilde (2026-08-22, samme lazy-per-rettskilde-mønster som TjenesteDetalj/
  // HandlingDetalj) — brukt til å vise selve NAVNEFORM-TEKSTEN treffet fant (StartOffset/EndOffset
  // skåret ut av nodens Tekst), ikke bare den rå node-eId-en. Uten dette er det ikke synlig i lista
  // OM det var "Advokattilsynet" eller en annen navneform (f.eks. "Tilsynsrådet for advokatvirksomhet")
  // som ga treffet.
  const [noderPerRettskilde, setNoderPerRettskilde] = useState<Map<string, RettskildeNodeDto[]>>(new Map());

  function visNavneformFunnet(k: VirksomhetKandidatDto): string | null {
    const node = noderPerRettskilde.get(k.rettskildeId)?.find((n) => n.eid === k.nodeEid);
    if (!node?.tekst) return null;
    return node.tekst.slice(k.startOffset, k.endOffset);
  }

  // Forespørsel-sekvensnummer (2026-08-22, Johanns tilbakemelding: kandidater for en virksomhet dukket
  // opp i lista mens et ANNET filter var valgt) — uten dette kunne en TREG, ELDRE forespørsel (f.eks.
  // fra filteret rett før brukeren byttet raskt til et nytt) svare ETTER en NYERE, og overskrive
  // resultatet med data som ikke lenger matcher det synlige filteret. `hentVirksomheter`/`api.kall`
  // har ingen innebygd avbrytnings-mekanisme (ingen AbortController), så vi løser det her i stedet:
  // hvert kall får sitt eget løpenummer, og kun svaret fra det SISTE utstedte kallet får lov til å
  // sette state.
  const sisteForesporsel = useRef(0);

  function lastKandidater() {
    const denneForesporselen = ++sisteForesporsel.current;
    setLaster(true);
    setFeil(null);
    api
      .hentVirksomhetKandidater({
        virksomhetId: virksomhetFilter || undefined,
        rettskildeId: rettskildeFilter || undefined,
        status: statusFilter,
      })
      .then((liste) => {
        if (denneForesporselen !== sisteForesporsel.current) return; // en nyere forespørsel er allerede i gang/ferdig
        setKandidater(liste);
        setValgte(new Set()); // Nytt filter/ny liste — forrige utvalg gjelder ikke lenger.
      })
      .catch((e) => {
        if (denneForesporselen !== sisteForesporsel.current) return;
        setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av kandidater.');
      })
      .finally(() => {
        if (denneForesporselen === sisteForesporsel.current) setLaster(false);
      });
  }

  useEffect(lastKandidater, [virksomhetFilter, rettskildeFilter, statusFilter]);

  // [Rettet, 2026-08-30 — gjeninnført etter at et tidligere forsøk gikk tapt i en umerget
  // grensammenslåing] «Virksomhet»-filteret brukte hele katalogen (~476 rader) i VirksomhetVelger —
  // Johann observerte at feltet hang/var tregt å skrive i (bekreftet: kun 3 av 476 virksomheter har
  // noen kandidat i det hele tatt). Egen, uavhengig forespørsel — scopet av rettskilde/status som
  // resten av lista, men ALDRI av virksomhetFilter selv (ellers ville nedtrekkslisten krympe til kun
  // ÉN virksomhet i det øyeblikket brukeren velger den).
  const [virksomhetIderMedKandidater, setVirksomhetIderMedKandidater] = useState<Set<string> | null>(null);
  useEffect(() => {
    api
      .hentVirksomhetKandidater({ rettskildeId: rettskildeFilter || undefined, status: statusFilter })
      .then((liste) => setVirksomhetIderMedKandidater(new Set(liste.map((k) => k.virksomhetId))))
      .catch(() => setVirksomhetIderMedKandidater(null));
  }, [rettskildeFilter, statusFilter]);

  const virksomheterMedKandidater = useMemo(
    () => (virksomhetIderMedKandidater ? virksomheter.filter((v) => virksomhetIderMedKandidater.has(v.id)) : virksomheter),
    [virksomheter, virksomhetIderMedKandidater],
  );

  const rettskilderPerId = useMemo(() => new Map(rettskilder.map((r) => [r.id, r] as const)), [rettskilder]);
  function visRettskilde(rettskildeId: string): string {
    return rettskilderPerId.get(rettskildeId)?.kortnavn ?? rettskilderPerId.get(rettskildeId)?.tittel ?? rettskildeId;
  }

  async function kjorSveip() {
    if (!sveipVirksomhetId) return;
    setSveiper(true);
    setSveipFeil(null);
    setSveipResultat(null);
    try {
      const resultat = await api.sveipVirksomhetKandidater({ virksomhetId: sveipVirksomhetId });
      setSveipResultat({ funnet: resultat.antallTreffFunnet, nye: resultat.antallNyeKandidater });
      lastKandidater();
    } catch (err) {
      setSveipFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved sveip.');
    } finally {
      setSveiper(false);
    }
  }

  function vekslValgt(id: string, valgt: boolean) {
    setValgte((forrige) => {
      const ny = new Set(forrige);
      if (valgt) ny.add(id); else ny.delete(id);
      return ny;
    });
  }

  // Merk: "alle viste" betyr alle på GJELDENDE SIDE, ikke hele det filtrerte treffsettet — samme
  // avgrensning som checkbox-etiketten "Velg alle viste" allerede antydet før paginering fantes,
  // nå bare eksplisitt riktig i og med at "viste" er per side.
  function vekslAlleViste(valgt: boolean) {
    setValgte(valgt ? new Set(paginering.visteRader.map((k) => k.id)) : new Set());
  }

  async function massehandling(handling: 'godkjenn' | 'avvis') {
    if (valgte.size === 0) return;
    setMassehandlingKjorer(true);
    setMassehandlingFeil(null);
    try {
      const request = { ider: [...valgte] };
      const resultat = handling === 'godkjenn'
        ? await api.godkjennVirksomhetKandidaterBatch(request)
        : await api.avvisVirksomhetKandidaterBatch(request);
      const feilede = resultat.rader.filter((r) => !r.ok);
      if (feilede.length > 0) {
        setMassehandlingFeil(
          `${feilede.length} av ${resultat.rader.length} rad(er) feilet: ${feilede.map((r) => r.feil).join('; ')}`,
        );
      }
      lastKandidater();
    } catch (err) {
      setMassehandlingFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved massehandling.');
    } finally {
      setMassehandlingKjorer(false);
    }
  }

  async function enkelthandling(id: string, handling: 'godkjenn' | 'avvis') {
    try {
      if (handling === 'godkjenn') await api.godkjennVirksomhetKandidat(id);
      else await api.avvisVirksomhetKandidat(id);
      lastKandidater();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved behandling av kandidat.');
    }
  }

  function bytteSortering(kolonne: Sorteringskolonne) {
    if (sortKolonne === kolonne) setSortStigende((s) => !s);
    else {
      setSortKolonne(kolonne);
      setSortStigende(true);
    }
  }
  function sorteringsindikator(kolonne: Sorteringskolonne) {
    if (sortKolonne !== kolonne) return '';
    return sortStigende ? ' ▲' : ' ▼';
  }

  const viste = useMemo(() => {
    if (!kandidater) return null;
    const sortnokkel = (k: VirksomhetKandidatDto) =>
      sortKolonne === 'virksomhet'
        ? visEier(k.virksomhetId)
        : sortKolonne === 'rettskilde'
          ? visRettskilde(k.rettskildeId)
          : sortKolonne === 'status'
            ? k.status
            : k.opprettetTidspunkt;
    return [...kandidater].sort((a, b) => {
      const cmp = sortnokkel(a).localeCompare(sortnokkel(b), 'nb');
      return sortStigende ? cmp : -cmp;
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [kandidater, sortKolonne, sortStigende, visEier, rettskilderPerId]);

  const paginering = usePaginering(viste ?? []);

  // Hent node-tekst KUN for rettskildene på GJELDENDE SIDE (2026-08-22) — ikke for hele det
  // filtrerte treffsettet. Med case-insensitiv sveip (samme dag) kan én virksomhet ha kandidater
  // spredt over titalls-hundretalls ULIKE rettskilder samtidig (f.eks. 373 treff for "fylkeskommune"
  // på tvers av store deler av lovverket) — å hente noder for ALLE av dem samtidig var en reell,
  // observert render-treg/timeout-regresjon. Paginering gjør denne mengden avgrenset og forutsigbar
  // (maks ett `hentNoder`-kall per DISTINKT rettskilde blant de viste radene, ikke per rad).
  useEffect(() => {
    for (const rettskildeId of new Set(paginering.visteRader.map((k) => k.rettskildeId))) {
      if (noderPerRettskilde.has(rettskildeId)) continue;
      api.hentNoder(rettskildeId)
        .then((noder) => setNoderPerRettskilde((forrige) => new Map(forrige).set(rettskildeId, noder)))
        .catch(() => {}); // ingen gjettet fallback — viser rå node-eId under når nodene ikke lot seg hente
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [paginering.visteRader]);

  return (
    <>
      <Heading level={1} data-size="lg" style={{ marginBottom: '0.2rem' }}>
        Virksomhetskandidater
      </Heading>
      <Paragraph style={{ marginBottom: '1.25rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
        Forekomster av virksomheters navneformer funnet ved tekstsøk i rettskilder — godkjenn for å
        opprette en faktisk tekst-tagg, avvis for å fjerne fra køen.
      </Paragraph>

      <Card style={{ padding: '1rem', marginBottom: '1.5rem' }}>
        <Heading level={2} data-size="xs" style={{ marginBottom: '0.5rem' }}>
          Kjør sveip
        </Heading>
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', marginBottom: '0.5rem' }}>
          Søker gjennom alle rettskilder etter forekomster av virksomhetens registrerte navneformer
          (se Virksomhetsdetalj → «Navneformer i rettskildetekst») og legger nye treff i køen som «Venter».
        </Paragraph>
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <VirksomhetVelger
            virksomheter={virksomheter}
            value={sveipVirksomhetId}
            onChange={setSveipVirksomhetId}
            label="Virksomhet å sveipe for"
            tomValgTekst="Velg virksomhet …"
            style={{ minWidth: '20rem' }}
          />
          <Button onClick={kjorSveip} disabled={!sveipVirksomhetId || sveiper}>
            {sveiper ? 'Sveiper …' : 'Kjør sveip'}
          </Button>
        </div>
        {sveipFeil && <div className="feilmelding" style={{ marginTop: '0.5rem' }}>{sveipFeil}</div>}
        {sveipResultat && (
          <div className="infomelding" style={{ marginTop: '0.5rem' }}>
            Fant {sveipResultat.funnet} treff totalt, {sveipResultat.nye} nye kandidater lagt i køen.
          </div>
        )}
      </Card>

      <div style={{ display: 'flex', gap: '1rem', alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: '1rem' }}>
        <VirksomhetVelger
          virksomheter={virksomheterMedKandidater}
          value={virksomhetFilter}
          onChange={setVirksomhetFilter}
          label="Virksomhet"
          tomValgTekst="Alle virksomheter"
          style={{ minWidth: '16rem' }}
        />
        <Field style={{ minWidth: '18rem' }}>
          <Label>Lov/forskrift</Label>
          <Select data-size="sm" value={rettskildeFilter} onChange={(e) => setRettskildeFilter(e.target.value)}>
            <Select.Option value="">Alle rettskilder</Select.Option>
            {rettskilder.map((r) => (
              <Select.Option key={r.id} value={r.id}>{r.kortnavn ?? r.tittel}</Select.Option>
            ))}
          </Select>
        </Field>
        <Field style={{ minWidth: '10rem' }}>
          <Label>Status</Label>
          <Select data-size="sm" value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as typeof statusFilter)}>
            <Select.Option value="Venter">Venter</Select.Option>
            <Select.Option value="Godkjent">Godkjent</Select.Option>
            <Select.Option value="Avvist">Avvist</Select.Option>
            <Select.Option value="Alle">Alle</Select.Option>
          </Select>
        </Field>
      </div>

      <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '1rem', flexWrap: 'wrap' }}>
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', margin: 0 }}>
          {valgte.size} valgt{valgte.size === 1 ? '' : 'e'}
          {rettskildeFilter ? ' — filtrert til én lov/forskrift' : ''}
        </Paragraph>
        <Button data-size="sm" onClick={() => massehandling('godkjenn')} disabled={valgte.size === 0 || massehandlingKjorer}>
          {massehandlingKjorer ? 'Godkjenner …' : 'Godkjenn valgte'}
        </Button>
        <Button data-size="sm" variant="secondary" onClick={() => massehandling('avvis')} disabled={valgte.size === 0 || massehandlingKjorer}>
          {massehandlingKjorer ? 'Avviser …' : 'Avvis valgte'}
        </Button>
      </div>
      {massehandlingFeil && <div className="feilmelding" style={{ marginBottom: '1rem' }}>{massehandlingFeil}</div>}

      {feil && <div className="feilmelding" style={{ marginBottom: '1rem' }}>{feil}</div>}
      {laster && !kandidater && <Paragraph>Laster …</Paragraph>}
      {viste && viste.length === 0 && <Paragraph>Ingen kandidater matcher filteret.</Paragraph>}

      {viste && viste.length > 0 && (
        <Card style={{ padding: 0, overflow: 'hidden' }}>
          <div style={{ overflowX: 'auto' }}>
            <Table>
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell>
                    <Checkbox
                      aria-label="Velg alle viste"
                      checked={paginering.visteRader.length > 0 && paginering.visteRader.every((k) => valgte.has(k.id))}
                      onChange={(e) => vekslAlleViste(e.target.checked)}
                    />
                  </Table.HeaderCell>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('virksomhet')}>
                      Virksomhet{sorteringsindikator('virksomhet')}
                    </button>
                  </Table.HeaderCell>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('rettskilde')}>
                      Lov/forskrift{sorteringsindikator('rettskilde')}
                    </button>
                  </Table.HeaderCell>
                  <Table.HeaderCell>Node</Table.HeaderCell>
                  <Table.HeaderCell>Navneform funnet</Table.HeaderCell>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('status')}>
                      Status{sorteringsindikator('status')}
                    </button>
                  </Table.HeaderCell>
                  <Table.HeaderCell>Handling</Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {paginering.visteRader.map((k) => (
                  <Table.Row key={k.id}>
                    <Table.Cell>
                      <Checkbox
                        aria-label={`Velg kandidat ${k.id}`}
                        checked={valgte.has(k.id)}
                        onChange={(e) => vekslValgt(k.id, e.target.checked)}
                      />
                    </Table.Cell>
                    <Table.Cell>{visEier(k.virksomhetId)}</Table.Cell>
                    <Table.Cell>{visRettskilde(k.rettskildeId)}</Table.Cell>
                    <Table.Cell style={{ fontFamily: 'monospace', fontSize: 'var(--ds-font-size-1)' }}>
                      {/* Slik at bruker kan lese noden i sin fulle sammenheng FØR godkjenning
                          (Johanns tilbakemelding 2026-08-22) — åpner rettskildevisningen på nøyaktig
                          denne noden. [Rettet, 2026-08-30] Bruker rettskildeLenkeForId (rettskildeId
                          allerede kjent på raden) i stedet for rettskildeLenke sin ELI-prefiks-
                          gjetting — den fant ingen treff for kap-/rom-/punkt-nummererte noder
                          (LovdataIdentifikatorer.KapittelEid er bevisst ELI-uavhengig). */}
                      <Link asChild>
                        <RouterLink to={rettskildeLenkeForId(k.rettskildeId, k.nodeEid)} target="_blank">{k.nodeEid} ↗</RouterLink>
                      </Link>
                    </Table.Cell>
                    <Table.Cell>
                      {(() => {
                        const navneform = visNavneformFunnet(k);
                        return navneform ? (
                          <Tag data-color="accent" data-size="sm">{navneform}</Tag>
                        ) : (
                          <span style={{ color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)' }}>…</span>
                        );
                      })()}
                    </Table.Cell>
                    <Table.Cell>
                      <Tag data-color={STATUS_FARGE[k.status] ?? 'neutral'} data-size="sm">{k.status}</Tag>
                    </Table.Cell>
                    <Table.Cell>
                      {k.status === 'Venter' ? (
                        <div style={{ display: 'flex', gap: '0.4rem' }}>
                          <Button data-size="sm" onClick={() => enkelthandling(k.id, 'godkjenn')}>Godkjenn</Button>
                          <Button data-size="sm" variant="tertiary" onClick={() => enkelthandling(k.id, 'avvis')}>Avvis</Button>
                        </div>
                      ) : (
                        <span style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
                          {k.behandletAv ? `Behandlet av ${k.behandletAv}` : '—'}
                        </span>
                      )}
                    </Table.Cell>
                  </Table.Row>
                ))}
              </Table.Body>
            </Table>
          </div>
        </Card>
      )}

      {viste && viste.length > 0 && <Pagineringskontroll {...paginering} />}
    </>
  );
}
