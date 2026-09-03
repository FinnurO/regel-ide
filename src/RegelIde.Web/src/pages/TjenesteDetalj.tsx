import { useEffect, useMemo, useState } from 'react';
import { Link as RouterLink, useParams } from 'react-router';
import { Alert, Button, Link, Paragraph, Spinner, Tabs, Tag } from '@digdir/designsystemet-react';
import { ApiError, api, apiUrl } from '../api/client';
import { eidVisningstekst } from '../api/eidLenker';
import type {
  HandlingDto, HendelseDto, RegelnodeDto, RettskildeNodeDto, RettskildeSammendrag, TjenesteavhengighetDto,
  TjenesteDto, TjenesteRegelverksreferanseDto,
} from '../api/types';
import { SEKSJON_LABELER, SEKSJON_NOKLER, type SeksjonNokkel } from '../api/tjenesteFelt';
import { useVisningsinnstillinger } from '../tjeneste/useVisningsinnstillinger';
import { OversiktFane } from '../tjeneste/OversiktFane';
import { VilkarstreFane } from '../tjeneste/VilkarstreFane';
import { InnholdFane } from '../tjeneste/InnholdFane';
import { StatusFane } from '../tjeneste/StatusFane';
import { RegelverkFane } from '../tjeneste/RegelverkFane';
import { HendelserFane } from '../tjeneste/HendelserFane';
import { HandlingerFane } from '../tjeneste/HandlingerFane';
import { AvhengigheterFane } from '../tjeneste/AvhengigheterFane';
import { KontekstPanel, type KontekstPanelGruppe } from '../entitet/KontekstPanel';
import type { DetaljVisning } from '../entitet/detaljVisning';

/**
 * Tjeneste-siden — redesignet (2026-08-27) fra ett 1253-linjers, endimensjonalt skjema
 * (docs/22-tjeneste-side-redesign-brief.md) til en fanebasert side med en Oversikt-landingsside,
 * reorderbare/lukkbare Innhold-accordions (`InnholdFane`), felt-nivå regelverksreferanser, frie
 * innholdselementer, delte handlinger og et gjennomgående "referanser"-kontekstpanel
 * (`KontekstPanel`) — se plan-notatet for canvasen dette bygger på. Selve fane-innholdet er
 * flyttet ut i `src/RegelIde.Web/src/tjeneste/*Fane.tsx`; denne filen er skallet: datainnhenting,
 * fane-/panel-navigasjon, og visningsinnstillingene som styrer rekkefølge/synlighet.
 */
export default function TjenesteDetalj() {
  const { id } = useParams<{ id: string }>();

  const [tjeneste, setTjeneste] = useState<TjenesteDto | null>(null);
  const [referanser, setReferanser] = useState<TjenesteRegelverksreferanseDto[] | null>(null);
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [regelnoder, setRegelnoder] = useState<RegelnodeDto[]>([]);
  const [rotnode, setRotnode] = useState<RegelnodeDto | null>(null);
  const [hendelser, setHendelser] = useState<HendelseDto[] | null>(null);
  const [alleHendelser, setAlleHendelser] = useState<HendelseDto[]>([]);
  const [handlinger, setHandlinger] = useState<HandlingDto[] | null>(null);
  const [avhengigheter, setAvhengigheter] = useState<TjenesteavhengighetDto[] | null>(null);
  const [alleTjenester, setAlleTjenester] = useState<TjenesteDto[]>([]);
  const [feil, setFeil] = useState<string | null>(null);

  const [noderPerRettskilde, setNoderPerRettskilde] = useState<Map<string, RettskildeNodeDto[]>>(new Map());
  async function sikreNoderFor(rettskildeId: string) {
    if (!rettskildeId || noderPerRettskilde.has(rettskildeId)) return;
    try {
      const noder = await api.hentNoder(rettskildeId);
      setNoderPerRettskilde((forrige) => new Map(forrige).set(rettskildeId, noder));
    } catch {
      // Ingen gjettet fallback — kalleren viser rå eId / en tom picker når nodene ikke lot seg hente.
    }
  }

  const [section, setSection] = useState<'oversikt' | SeksjonNokkel>('oversikt');
  const [visTilpassFaner, setVisTilpassFaner] = useState(false);
  const [visModelleksport, setVisModelleksport] = useState(false);
  const [modelleksport, setModelleksport] = useState<Record<string, unknown> | null>(null);
  const [modelleksportLaster, setModelleksportLaster] = useState(false);
  const [modelleksportFeil, setModelleksportFeil] = useState<string | null>(null);

  const [rightCollapsed, setRightCollapsed] = useState(false);
  const [rightTab, setRightTab] = useState<'relasjoner' | 'detaljer'>('relasjoner');
  const [selectedDetail, setSelectedDetail] = useState<DetaljVisning | null>(null);

  const {
    innstilling, flyttSeksjon, skjulSeksjon, visSeksjon,
    flyttAccordion, settAccordionApen, apneAlleAccordions,
  } = useVisningsinnstillinger();

  useEffect(() => {
    if (!id) return;
    api.hentTjeneste(id).then(setTjeneste).catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av tjeneste.'));
    api.hentTjenesteRegelverksreferanser(id).then(setReferanser).catch(() => setReferanser([]));
    api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([]));
    api.hentRegelnodeListe().then(setRegelnoder).catch(() => setRegelnoder([]));
    api.hentTjenesteHendelser(id).then(setHendelser).catch(() => setHendelser([]));
    api.hentHandlinger(id).then(setHandlinger).catch(() => setHandlinger([]));
    api.hentHendelser().then(setAlleHendelser).catch(() => setAlleHendelser([]));
    api.hentTjenesteavhengigheter(id).then(setAvhengigheter).catch(() => setAvhengigheter([]));
    api.hentTjenester().then(setAlleTjenester).catch(() => setAlleTjenester([]));
  }, [id]);

  useEffect(() => {
    if (!tjeneste?.rotnodeId) { setRotnode(null); return; }
    api.hentRegelnode(tjeneste.rotnodeId).then(setRotnode).catch(() => setRotnode(null));
  }, [tjeneste?.rotnodeId]);

  // Nodene til hver rettskilde faktisk referert i lista — slik at eidVisningstekst kan vise
  // "{tittel} § {nummer} — {overskrift}" i stedet for rå eId, i alle faner/panelet som viser referanser.
  useEffect(() => {
    if (!referanser) return;
    for (const rettskildeId of new Set(referanser.map((r) => r.tilRettskildeId))) {
      sikreNoderFor(rettskildeId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [referanser]);

  async function apneModelleksport() {
    if (!id) return;
    setVisModelleksport(true);
    if (modelleksport) return;
    setModelleksportFeil(null);
    setModelleksportLaster(true);
    try {
      setModelleksport(await api.hentModelleksport(id));
    } catch (err) {
      setModelleksportFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved henting av JSON-eksport.');
    } finally {
      setModelleksportLaster(false);
    }
  }

  const flateReferanser = useMemo(() => (referanser ?? []).filter((r) => r.felt === null), [referanser]);

  function visReferanseDetalj(r: TjenesteRegelverksreferanseDto) {
    const rettskilde = rettskilder.find((rk) => rk.id === r.tilRettskildeId);
    const node = noderPerRettskilde.get(r.tilRettskildeId)?.find((n) => n.eid === r.tilEid);
    setSelectedDetail({
      title: eidVisningstekst(r.tilEid, rettskilder, noderPerRettskilde) ?? r.tilEid,
      meta: rettskilde ? rettskilde.tittel : 'Regelverksreferanse',
      body: node?.tekst ?? null,
    });
    setRightTab('detaljer');
  }

  // Generalisert KontekstPanel (docs/30 §4 punkt 1) — Tjeneste-siden bygger selv opp gruppene og hva
  // hvert klikk skal vise; ingen visuell endring i seg selv, kun samme 3 grupper som før flyttet hit.
  const kontekstGrupper: KontekstPanelGruppe[] = useMemo(() => [
    {
      heading: 'Regelverksreferanser',
      items: flateReferanser.map((r) => ({
        key: r.id,
        label: eidVisningstekst(r.tilEid, rettskilder, noderPerRettskilde) ?? r.tilEid,
        onClick: () => visReferanseDetalj(r),
      })),
    },
    {
      heading: 'Hendelser',
      items: (hendelser ?? []).map((h) => ({
        key: h.id,
        label: h.navn,
        onClick: () => { setSelectedDetail({ title: h.navn, meta: `Hendelse · ${h.type}`, body: h.beskrivelse }); setRightTab('detaljer'); },
      })),
    },
    {
      heading: 'Avhengigheter',
      items: (avhengigheter ?? []).map((a) => ({
        key: a.id,
        label: a.visningstekst,
        onClick: () => { setSelectedDetail({ title: a.visningstekst, meta: `Avhengighet · ${a.rel}`, body: a.beskrivelse }); setRightTab('detaljer'); },
      })),
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
  ], [flateReferanser, rettskilder, noderPerRettskilde, hendelser, avhengigheter]);

  if (feil) return <Alert data-color="danger">{feil}</Alert>;
  if (!tjeneste || !id) return <Spinner aria-label="Laster …" data-size="sm" />;

  const synligeSeksjoner = (innstilling?.seksjonsrekkefolge ?? SEKSJON_NOKLER as unknown as string[])
    .filter((k) => !(innstilling?.skjulteSeksjoner ?? []).includes(k)) as SeksjonNokkel[];
  const skjulteSeksjoner = (innstilling?.skjulteSeksjoner ?? []) as SeksjonNokkel[];

  return (
    <div className="tjenestedetalj-fullbredde" style={{ display: 'flex', gap: '0', height: '100%' }}>
      <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
        <nav aria-label="Brødsmulesti" style={{ display: 'flex', gap: '0.4rem', fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.6rem', flexWrap: 'wrap' }}>
          <Link asChild><RouterLink to="/tjenester">Tjenester</RouterLink></Link>
          <span>/</span>
          {section === 'oversikt' ? (
            <span style={{ color: 'var(--ds-color-neutral-text-default)' }}>{tjeneste.tittel}</span>
          ) : (
            <>
              <button type="button" onClick={() => setSection('oversikt')}
                style={{ background: 'none', border: 'none', padding: 0, font: 'inherit', color: 'var(--ds-color-accent-text-default)', cursor: 'pointer' }}>
                {tjeneste.tittel}
              </button>
              <span>/</span>
              <span style={{ color: 'var(--ds-color-neutral-text-default)' }}>{SEKSJON_LABELER[section]}</span>
            </>
          )}
        </nav>

        <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: '1rem', marginBottom: '0.6rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
            <h1 className="h1" style={{ fontSize: 'var(--ds-font-size-7)', fontWeight: 500, margin: 0 }}>{tjeneste.tittel}</h1>
            <Tag data-color="neutral" data-size="sm">{tjeneste.status}</Tag>
          </div>
          <Button variant="secondary" data-size="sm" onClick={() => (visModelleksport ? setVisModelleksport(false) : apneModelleksport())}>
            {visModelleksport ? 'Skjul JSON' : 'Vis JSON'}
          </Button>
        </div>

        {visModelleksport && (
          <div style={{ marginBottom: '1rem' }}>
            {modelleksportLaster && <Spinner aria-label="Laster …" data-size="sm" />}
            {modelleksportFeil && <Alert data-color="danger">{modelleksportFeil}</Alert>}
            {modelleksport && (
              <>
                <pre style={{
                  maxHeight: '16rem', overflow: 'auto', padding: '0.75rem 1rem', borderRadius: 'var(--ds-border-radius-md)',
                  background: 'var(--ds-color-neutral-surface-tinted)', fontSize: 'var(--ds-font-size-1)', margin: '0 0 0.4rem',
                }}>
                  {JSON.stringify(modelleksport, null, 2)}
                </pre>
                <Link href={apiUrl(`/api/tjenester/${tjeneste.id}/modelleksport`)} target="_blank" rel="noreferrer" style={{ fontSize: 'var(--ds-font-size-1)' }}>
                  Åpne full respons i ny fane →
                </Link>
              </>
            )}
          </div>
        )}

        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.75rem' }}>
          <Tabs value={section} onChange={(v) => setSection(v as 'oversikt' | SeksjonNokkel)} style={{ flex: 1, minWidth: 0 }}>
            <Tabs.List>
              <Tabs.Tab value="oversikt">Oversikt</Tabs.Tab>
              {synligeSeksjoner.map((k) => <Tabs.Tab key={k} value={k}>{SEKSJON_LABELER[k]}</Tabs.Tab>)}
            </Tabs.List>
          </Tabs>
          <Button variant="tertiary" data-size="sm" onClick={() => setVisTilpassFaner((v) => !v)} title="Tilpass faner">⚙</Button>
        </div>

        {visTilpassFaner && innstilling && (
          <div style={{
            border: '1px solid var(--ds-color-neutral-border-subtle)', borderRadius: 'var(--ds-border-radius-md)',
            padding: '0.75rem', marginBottom: '0.75rem', maxWidth: '24rem',
          }}>
            <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', fontWeight: 600, marginBottom: '0.4rem' }}>Rekkefølge og synlighet</Paragraph>
            {synligeSeksjoner.map((k, idx) => (
              <div key={k} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '0.25rem 0', fontSize: 'var(--ds-font-size-1)' }}>
                <span>{SEKSJON_LABELER[k]}</span>
                <span style={{ display: 'flex', gap: '0.2rem' }}>
                  <Button variant="tertiary" data-size="sm" disabled={idx === 0} onClick={() => flyttSeksjon(k, -1)} style={{ minWidth: 0, padding: '0 0.3rem' }}>↑</Button>
                  <Button variant="tertiary" data-size="sm" disabled={idx === synligeSeksjoner.length - 1} onClick={() => flyttSeksjon(k, 1)} style={{ minWidth: 0, padding: '0 0.3rem' }}>↓</Button>
                  <Button variant="tertiary" data-size="sm" onClick={() => { skjulSeksjon(k); if (section === k) setSection('oversikt'); }} style={{ minWidth: 0, padding: '0 0.3rem' }}>✕</Button>
                </span>
              </div>
            ))}
            {skjulteSeksjoner.length > 0 && (
              <>
                <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', fontWeight: 600, margin: '0.6rem 0 0.3rem' }}>Skjult</Paragraph>
                {skjulteSeksjoner.map((k) => (
                  <div key={k} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '0.25rem 0', fontSize: 'var(--ds-font-size-1)', opacity: 0.7 }}>
                    <span>{SEKSJON_LABELER[k]}</span>
                    <Button variant="tertiary" data-size="sm" onClick={() => visSeksjon(k)} style={{ minWidth: 0, padding: '0 0.3rem' }}>↺</Button>
                  </div>
                ))}
              </>
            )}
          </div>
        )}

        <div style={{ flex: 1, overflowY: 'auto', paddingRight: '0.25rem' }}>
          {section === 'oversikt' && (
            <OversiktFane
              tjeneste={tjeneste} rotnode={rotnode}
              antallReferanser={flateReferanser.length} antallHendelser={(hendelser ?? []).length}
              antallHandlinger={(handlinger ?? []).length} antallAvhengigheter={(avhengigheter ?? []).length}
              onGaTilFane={setSection}
            />
          )}
          {section === 'vilkarstre' && (
            <VilkarstreFane tjeneste={tjeneste} rotnode={rotnode} regelnoder={regelnoder} onTjenesteOppdatert={setTjeneste} />
          )}
          {section === 'innhold' && innstilling && (
            <InnholdFane
              tjeneste={tjeneste} onTjenesteOppdatert={setTjeneste}
              referanser={referanser} setReferanser={setReferanser}
              rettskilder={rettskilder} noderPerRettskilde={noderPerRettskilde} sikreNoderFor={sikreNoderFor}
              onSelectDetail={(v) => { setSelectedDetail(v); setRightTab('detaljer'); }}
              accordionRekkefolge={innstilling.accordionRekkefolge} accordionApne={innstilling.accordionApne}
              flyttAccordion={flyttAccordion} settAccordionApen={settAccordionApen} apneAlleAccordions={apneAlleAccordions}
            />
          )}
          {section === 'status' && <StatusFane tjeneste={tjeneste} onTjenesteOppdatert={setTjeneste} />}
          {section === 'regelverk' && (
            <RegelverkFane
              tjenesteId={id} referanser={referanser} setReferanser={setReferanser} rettskilder={rettskilder}
              noderPerRettskilde={noderPerRettskilde} sikreNoderFor={sikreNoderFor}
              onSelectDetail={(v) => { setSelectedDetail(v); setRightTab('detaljer'); }}
            />
          )}
          {section === 'hendelser' && (
            <HendelserFane
              tjenesteId={id} hendelser={hendelser} setHendelser={setHendelser}
              alleHendelser={alleHendelser} setAlleHendelser={setAlleHendelser}
              onSelectDetail={(v) => { setSelectedDetail(v); setRightTab('detaljer'); }}
            />
          )}
          {section === 'handlinger' && (
            <HandlingerFane tjenesteId={id} handlinger={handlinger} setHandlinger={setHandlinger} referanser={referanser} />
          )}
          {section === 'avhengigheter' && (
            <AvhengigheterFane
              tjenesteId={id} avhengigheter={avhengigheter} setAvhengigheter={setAvhengigheter}
              alleTjenester={alleTjenester} alleHendelser={alleHendelser}
              onSelectDetail={(v) => { setSelectedDetail(v); setRightTab('detaljer'); }}
            />
          )}
        </div>
      </div>

      <KontekstPanel
        collapsed={rightCollapsed} onToggleCollapsed={() => setRightCollapsed((v) => !v)}
        rightTab={rightTab} setRightTab={setRightTab}
        grupper={kontekstGrupper}
        selectedDetail={selectedDetail}
        onClearDetail={() => setSelectedDetail(null)}
      />
    </div>
  );
}
