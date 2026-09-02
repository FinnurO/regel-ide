import { useEffect, useMemo, useRef, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Card, Dialog, Field, Heading, Label, Link, Paragraph, Select, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenkeForId } from '../api/eidLenker';
import type { BegrepsforekomstDto, RettskildeSammendrag } from '../api/types';
import { RettskildeVelger } from '../rettskilde/RettskildeVelger';
import { Pagineringskontroll } from '../tabell/Pagineringskontroll';
import { usePaginering } from '../tabell/usePaginering';
import { useVirksomheter } from '../virksomhet/useVirksomheter';
import { VirksomhetVelger } from '../virksomhet/VirksomhetVelger';

type Sorteringskolonne = 'begrep' | 'monster' | 'rettskilde' | 'status' | 'opprettet';

const STATUS_FARGE: Record<string, 'neutral' | 'warning' | 'success' | 'danger'> = {
  Venter: 'warning',
  Godkjent: 'success',
  Avvist: 'danger',
};

const KONFIDENS_FARGE: Record<string, 'neutral' | 'warning' | 'success' | 'danger' | 'info'> = {
  hoy: 'success',
  middels: 'warning',
  lav: 'danger',
  krever_oppslag: 'info',
};

const DEFINISJON_AVKORT_LENGDE = 100;

/**
 * Arbeidskø for `BegrepsforekomstEntitet` (M1/M11-begrepsoppdagelse, docs/24) — deterministisk
 * (regex-basert) sveip av rettskildetekst, HELT egen kø fra dagens KI-drevne «Identifiser
 * begrep»-forslag (`BegrepsforslagKo.tsx`), se docs/24 §1.2 for hvorfor: samme term kan dukke opp
 * som flere, delvis motstridende forekomster på tvers av korpuset, og de fleste skal ALDRI bli en
 * egen `Begrep`-registerrad — akkurat som `VirksomhetKandidat`/`Navnekandidat` er kø-mekanikken
 * her strukturelt lånt fra (sveip-knapp, filter, per-rad godkjenn/avvis), IKKE fra
 * `BegrepsforslagKo`s "kandidatraden ER selve BegrepEntitet-en"-mønster.
 *
 * FØRSTE side bygget etter det saksbehandlerverktøy-mønsteret docs/09-design-konvensjoner.md §14/
 * docs/30 vedtok 2026-09-02 — bygget rett fra dag én med det nye mønsteret (Card alltid rendret,
 * `data-size="sm"` konsekvent, kompakt tabelltetthet), ikke det gamle mønsteret
 * `NavnekandidaterListe.tsx`/`VirksomhetKandidaterListe.tsx` ble bygget med FØR omleggingen — se de
 * to filenes kommentarer for selve kø-MEKANIKKEN, som er gjenbrukt her nesten uendret.
 *
 * Godkjenning krever et eksplisitt virksomhetsvalg (en forekomst er delt/objektiv, men et
 * `Begrep`-register krever en eier, se `GodkjennBegrepsforekomstRequest`-kommentaren) — løst med en
 * liten `Dialog` + `VirksomhetVelger` (søkbar, IKKE en rå `<Select>` med alle virksomheter, samme
 * ytelsesfelle docs/09 §9.2 advarer mot) i stedet for et inline-felt i selve raden.
 */
export default function Begrepskandidater() {
  const { virksomheter } = useVirksomheter();
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);

  const [rettskildeFilter, setRettskildeFilter] = useState('');
  const [monsterFilter, setMonsterFilter] = useState<'' | 'M1' | 'M11'>('');
  const [statusFilter, setStatusFilter] = useState<'Venter' | 'Godkjent' | 'Avvist' | 'Alle'>('Venter');

  const [forekomster, setForekomster] = useState<BegrepsforekomstDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [laster, setLaster] = useState(false);

  const [utvidet, setUtvidet] = useState<Set<string>>(new Set());

  // Sletting — KUN 'Avvist'-rader kan hardslettes (samme begrunnelse som VirksomhetKandidaterListe.tsx/
  // NavnekandidaterListe.tsx: en 'Godkjent' rad har en ekte tekst-tagg/et ekte begrep som ikke kan
  // fjernes i etterkant, og en 'Venter'-rad skal behandles, ikke bare forsvinne). Hentet UAVHENGIG av
  // statusFilter over — «Slett alle avviste»-knappen skal vise riktig antall selv når et annet
  // statusfilter er valgt.
  const [avvisteForekomster, setAvvisteForekomster] = useState<BegrepsforekomstDto[] | null>(null);
  const [sletterAlle, setSletterAlle] = useState(false);
  const [slettAlleFeil, setSlettAlleFeil] = useState<string | null>(null);

  const [sveipRettskildeId, setSveipRettskildeId] = useState('');
  const [sveiper, setSveiper] = useState(false);
  const [sveipFeil, setSveipFeil] = useState<string | null>(null);
  const [sveipResultat, setSveipResultat] = useState<{ funnet: number; nye: number } | null>(null);

  // Godkjenn-dialog — venter på virksomhetsvalg (se klassekommentaren).
  const [godkjennForekomst, setGodkjennForekomst] = useState<BegrepsforekomstDto | null>(null);
  const [godkjennVirksomhetId, setGodkjennVirksomhetId] = useState('');
  const [godkjenner, setGodkjenner] = useState(false);
  const [godkjennFeil, setGodkjennFeil] = useState<string | null>(null);

  const [sortKolonne, setSortKolonne] = useState<Sorteringskolonne>('opprettet');
  const [sortStigende, setSortStigende] = useState(false);

  useEffect(() => {
    api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([]));
  }, []);

  // Samme "kun siste utstedte forespørsel får sette state"-vern som VirksomhetKandidaterListe.tsx/
  // NavnekandidaterListe.tsx — se den filens kommentar for hele resonnementet.
  const sisteForesporsel = useRef(0);

  function lastForekomster() {
    const denneForesporselen = ++sisteForesporsel.current;
    setLaster(true);
    setFeil(null);
    api
      .hentBegrepsforekomster({
        rettskildeId: rettskildeFilter || undefined,
        monsterId: monsterFilter || undefined,
        status: statusFilter,
      })
      .then((liste) => {
        if (denneForesporselen !== sisteForesporsel.current) return;
        setForekomster(liste);
      })
      .catch((e) => {
        if (denneForesporselen !== sisteForesporsel.current) return;
        setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av begrepskandidater.');
      })
      .finally(() => {
        if (denneForesporselen === sisteForesporsel.current) setLaster(false);
      });
  }

  useEffect(lastForekomster, [rettskildeFilter, monsterFilter, statusFilter]);

  function lastAvvisteForekomster() {
    api
      .hentBegrepsforekomster({ rettskildeId: rettskildeFilter || undefined, status: 'Avvist' })
      .then(setAvvisteForekomster)
      .catch(() => setAvvisteForekomster(null));
  }

  useEffect(lastAvvisteForekomster, [rettskildeFilter]);

  const rettskilderPerId = useMemo(() => new Map(rettskilder.map((r) => [r.id, r] as const)), [rettskilder]);
  function visRettskilde(rettskildeId: string): string {
    return rettskilderPerId.get(rettskildeId)?.kortnavn ?? rettskilderPerId.get(rettskildeId)?.tittel ?? rettskildeId;
  }

  async function kjorSveip() {
    setSveiper(true);
    setSveipFeil(null);
    setSveipResultat(null);
    try {
      const resultat = await api.sveipBegrepsforekomster(sveipRettskildeId || null);
      setSveipResultat({ funnet: resultat.antallTreffFunnet, nye: resultat.antallNyeForekomster });
      lastForekomster();
      lastAvvisteForekomster();
    } catch (err) {
      setSveipFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved sveip.');
    } finally {
      setSveiper(false);
    }
  }

  function apneGodkjennDialog(forekomst: BegrepsforekomstDto) {
    setGodkjennForekomst(forekomst);
    setGodkjennVirksomhetId('');
    setGodkjennFeil(null);
  }

  async function bekreftGodkjenn() {
    if (!godkjennForekomst || !godkjennVirksomhetId) return;
    setGodkjenner(true);
    setGodkjennFeil(null);
    try {
      await api.godkjennBegrepsforekomst(godkjennForekomst.id, godkjennVirksomhetId);
      setGodkjennForekomst(null);
      lastForekomster();
    } catch (err) {
      setGodkjennFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved godkjenning.');
    } finally {
      setGodkjenner(false);
    }
  }

  async function avvis(id: string) {
    try {
      await api.avvisBegrepsforekomst(id);
      lastForekomster();
      lastAvvisteForekomster();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved avvisning.');
    }
  }

  async function slettEnkelt(id: string) {
    if (!window.confirm('Slette denne begrepskandidaten permanent? Dette kan ikke angres.')) return;
    try {
      await api.slettBegrepsforekomst(id);
      lastForekomster();
      lastAvvisteForekomster();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved sletting.');
    }
  }

  async function slettAlle() {
    const antall = avvisteForekomster?.length ?? 0;
    if (antall === 0) return;
    if (!window.confirm(`Slette ${antall} avvist(e) begrepskandidat(er) permanent? Dette kan ikke angres.`)) return;

    setSletterAlle(true);
    setSlettAlleFeil(null);
    try {
      await api.slettAlleAvvisteBegrepsforekomster({ rettskildeId: rettskildeFilter || undefined });
      lastForekomster();
      lastAvvisteForekomster();
    } catch (err) {
      setSlettAlleFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved massesletting.');
    } finally {
      setSletterAlle(false);
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

  function vekslUtvidet(id: string) {
    setUtvidet((forrige) => {
      const ny = new Set(forrige);
      if (ny.has(id)) ny.delete(id); else ny.add(id);
      return ny;
    });
  }

  const viste = useMemo(() => {
    if (!forekomster) return null;
    const sortnokkel = (f: BegrepsforekomstDto) =>
      sortKolonne === 'begrep'
        ? f.begrep
        : sortKolonne === 'monster'
          ? f.monsterId
          : sortKolonne === 'rettskilde'
            ? visRettskilde(f.rettskildeId)
            : sortKolonne === 'status'
              ? f.status
              : f.opprettetTidspunkt;
    return [...forekomster].sort((a, b) => {
      const cmp = sortnokkel(a).localeCompare(sortnokkel(b), 'nb');
      return sortStigende ? cmp : -cmp;
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [forekomster, sortKolonne, sortStigende, rettskilderPerId]);

  const paginering = usePaginering(viste ?? []);

  return (
    <>
      <Heading level={1} data-size="lg" style={{ marginBottom: '0.2rem' }}>
        Begrepskandidater
      </Heading>
      <Paragraph style={{ marginBottom: '1.25rem', fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
        Deterministisk (regex-basert) sveip etter begrepsdefinisjoner i rettskildetekst (M1: eksplisitt
        definisjonsliste, M11: egen definisjonsparagraf, docs/24) — godkjenn for å opprette et begrep i
        en valgt virksomhets register pluss en ekte tekst-tagg, avvis for å fjerne fra køen. Egen kø fra{' '}
        <Link asChild><RouterLink to="/begreper/forslag">KI-forslag begrep</RouterLink></Link>, som
        opererer direkte på selve begrepsregisteret.
      </Paragraph>

      <Card style={{ padding: '1rem', marginBottom: '1.5rem' }}>
        <Heading level={2} data-size="xs" style={{ marginBottom: '0.5rem' }}>
          Kjør sveip
        </Heading>
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', marginBottom: '0.5rem' }}>
          Ingen rettskilde valgt = hele det importerte korpuset. Idempotent — kan kjøres flere ganger
          uten duplikater.
        </Paragraph>
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <RettskildeVelger rettskilder={rettskilder} value={sveipRettskildeId} onChange={setSveipRettskildeId} label="Rettskilde (tomt = hele korpuset)" />
          <Button data-size="sm" onClick={kjorSveip} disabled={sveiper}>
            {sveiper ? 'Sveiper …' : 'Kjør sveip'}
          </Button>
        </div>
        {sveipFeil && <Alert data-color="danger" data-size="sm" style={{ marginTop: '0.5rem' }}>{sveipFeil}</Alert>}
        {sveipResultat && (
          <Alert data-color="info" data-size="sm" style={{ marginTop: '0.5rem' }}>
            Fant {sveipResultat.funnet} treff totalt, {sveipResultat.nye} nye begrepskandidater lagt i køen.
          </Alert>
        )}
      </Card>

      <div style={{ display: 'flex', gap: '1rem', alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: '1rem' }}>
        <RettskildeVelger rettskilder={rettskilder} value={rettskildeFilter} onChange={setRettskildeFilter} label="Rettskilde (tomt = alle)" />
        <Field style={{ minWidth: '10rem' }}>
          <Label>Mønster</Label>
          <Select data-size="sm" value={monsterFilter} onChange={(e) => setMonsterFilter(e.target.value as typeof monsterFilter)}>
            <Select.Option value="">Alle mønstre</Select.Option>
            <Select.Option value="M1">M1 (definisjonsliste)</Select.Option>
            <Select.Option value="M11">M11 (definisjonsparagraf)</Select.Option>
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

      <Card style={{ padding: '1rem', marginBottom: '1rem' }}>
        <Heading level={2} data-size="xs" style={{ marginBottom: '0.5rem' }}>
          Slett avviste begrepskandidater
        </Heading>
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
          Ekte, irreversibel sletting av 'Avvist'-kandidater — nyttig for å tømme køen før et nytt sveip.
          Respekterer rettskildefilteret over, men IKKE statusfilteret — kun 'Avvist'-rader kan slettes.
        </Paragraph>
        <Button
          data-size="sm"
          data-color="danger"
          onClick={slettAlle}
          disabled={!avvisteForekomster || avvisteForekomster.length === 0 || sletterAlle}
        >
          {sletterAlle ? 'Sletter …' : `Slett alle avviste (${avvisteForekomster?.length ?? 0})`}
        </Button>
        {slettAlleFeil && <Alert data-color="danger" data-size="sm" style={{ marginTop: '0.5rem' }}>{slettAlleFeil}</Alert>}
      </Card>

      {feil && <Alert data-color="danger" data-size="sm" style={{ marginBottom: '1rem' }}>{feil}</Alert>}

      {/* Card ALLTID rendret (docs/09 §14/docs/30 §3.1 pkt. 5) — tom-tilstand er en Paragraph INNI
          kortet, ikke et betinget-rendret kort. */}
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        {laster && !forekomster ? (
          <Paragraph style={{ padding: '1rem', margin: 0 }}>Laster …</Paragraph>
        ) : viste && viste.length === 0 ? (
          <Paragraph style={{ padding: '1rem', margin: 0 }}>Ingen begrepskandidater matcher filteret.</Paragraph>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <Table data-density="compact" data-size="sm">
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('begrep')}>
                      Begrep{sorteringsindikator('begrep')}
                    </button>
                  </Table.HeaderCell>
                  <Table.HeaderCell>Definisjon</Table.HeaderCell>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('monster')}>
                      Mønster{sorteringsindikator('monster')}
                    </button>
                  </Table.HeaderCell>
                  <Table.HeaderCell>Konfidens</Table.HeaderCell>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('rettskilde')}>
                      Rettskilde{sorteringsindikator('rettskilde')}
                    </button>
                  </Table.HeaderCell>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('status')}>
                      Status{sorteringsindikator('status')}
                    </button>
                  </Table.HeaderCell>
                  <Table.HeaderCell>Handling</Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {paginering.visteRader.map((f) => {
                  const definisjon = f.definisjon ?? '';
                  const erLang = definisjon.length > DEFINISJON_AVKORT_LENGDE;
                  const erUtvidet = utvidet.has(f.id);
                  return (
                    <Table.Row key={f.id}>
                      <Table.Cell style={{ fontWeight: 500 }}>
                        {f.begrepOriginal}
                        {f.begrepOriginal.toLowerCase() !== f.begrep.toLowerCase() && (
                          <div style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
                            normalisert: {f.begrep}
                          </div>
                        )}
                      </Table.Cell>
                      <Table.Cell style={{ maxWidth: '28rem' }}>
                        {!f.definisjon ? (
                          <span style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>—</span>
                        ) : (
                          <span title={erLang ? f.definisjon : undefined}>
                            {erUtvidet || !erLang ? f.definisjon : `${definisjon.slice(0, DEFINISJON_AVKORT_LENGDE)}…`}
                            {erLang && (
                              <>
                                {' '}
                                <button type="button" className="tabell-sorter-knapp" onClick={() => vekslUtvidet(f.id)}>
                                  {erUtvidet ? 'Vis mindre' : 'Vis mer'}
                                </button>
                              </>
                            )}
                          </span>
                        )}
                      </Table.Cell>
                      <Table.Cell>
                        <Tag data-color="accent" data-size="sm">{f.monsterId}</Tag>
                      </Table.Cell>
                      <Table.Cell>
                        <Tag data-color={KONFIDENS_FARGE[f.konfidens] ?? 'neutral'} data-size="sm">{f.konfidens}</Tag>
                      </Table.Cell>
                      <Table.Cell style={{ fontFamily: 'monospace', fontSize: 'var(--ds-font-size-1)' }}>
                        <Link asChild>
                          <RouterLink to={rettskildeLenkeForId(f.rettskildeId, f.nodeEid)} target="_blank">
                            {visRettskilde(f.rettskildeId)} — {f.nodeEid} ↗
                          </RouterLink>
                        </Link>
                      </Table.Cell>
                      <Table.Cell>
                        <Tag data-color={STATUS_FARGE[f.status] ?? 'neutral'} data-size="sm">{f.status}</Tag>
                      </Table.Cell>
                      <Table.Cell>
                        <div style={{ display: 'flex', gap: '0.4rem', flexWrap: 'wrap', alignItems: 'center' }}>
                          {f.status === 'Venter' ? (
                            <>
                              <Button data-size="sm" onClick={() => apneGodkjennDialog(f)}>Godkjenn</Button>
                              <Button data-size="sm" variant="tertiary" onClick={() => avvis(f.id)}>Avvis</Button>
                            </>
                          ) : (
                            <span style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
                              {f.behandletAv ? `Behandlet av ${f.behandletAv}` : '—'}
                            </span>
                          )}
                          {/* KUN 'Avvist' — se klassekommentaren/backend-kommentaren for hvorfor. */}
                          {f.status === 'Avvist' && (
                            <Button data-size="sm" variant="tertiary" data-color="danger" onClick={() => slettEnkelt(f.id)}>
                              Slett
                            </Button>
                          )}
                        </div>
                      </Table.Cell>
                    </Table.Row>
                  );
                })}
              </Table.Body>
            </Table>
          </div>
        )}
      </Card>

      {viste && viste.length > 0 && <Pagineringskontroll {...paginering} />}

      <Dialog
        open={godkjennForekomst !== null}
        onClose={() => setGodkjennForekomst(null)}
        closeButton="Avbryt"
        style={{ maxWidth: '30rem' }}
      >
        <Dialog.Block>
          <Heading level={2} data-size="xs" style={{ marginBottom: '0.5rem' }}>
            Godkjenn begrepskandidat
          </Heading>
          <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', marginBottom: '0.75rem' }}>
            «{godkjennForekomst?.begrepOriginal}» opprettes som et nytt begrep i valgt virksomhets
            register, pluss en ekte tekst-tagg i rettskilden. En forekomst er delt/objektiv, men
            registeret krever en eier — velg hvilken virksomhet begrepet skal landes i.
          </Paragraph>
          <VirksomhetVelger
            virksomheter={virksomheter}
            value={godkjennVirksomhetId}
            onChange={setGodkjennVirksomhetId}
            label="Virksomhet"
            tomValgTekst="Velg virksomhet …"
          />
          {godkjennFeil && <Alert data-color="danger" data-size="sm" style={{ marginTop: '0.75rem' }}>{godkjennFeil}</Alert>}
        </Dialog.Block>
        <Dialog.Block style={{ display: 'flex', gap: '0.5rem', justifyContent: 'flex-end' }}>
          <Button data-size="sm" variant="secondary" onClick={() => setGodkjennForekomst(null)}>Avbryt</Button>
          <Button data-size="sm" onClick={bekreftGodkjenn} disabled={!godkjennVirksomhetId || godkjenner}>
            {godkjenner ? 'Godkjenner …' : 'Godkjenn'}
          </Button>
        </Dialog.Block>
      </Dialog>
    </>
  );
}
