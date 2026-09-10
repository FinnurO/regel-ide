import { useEffect, useMemo, useRef, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { Alert, Button, Card, Checkbox, Dialog, Field, Heading, Label, Link, Paragraph, Select, Table, Tag } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenkeForId } from '../api/eidLenker';
import type { BegrepsforekomstDto, RettskildeDetalj, RettskildeSammendrag } from '../api/types';
import { RettskildeVelger } from '../rettskilde/RettskildeVelger';
import { Pagineringskontroll } from '../tabell/Pagineringskontroll';
import { usePaginering } from '../tabell/usePaginering';
import { useVirksomheter } from '../virksomhet/useVirksomheter';
import { VirksomhetVelger } from '../virksomhet/VirksomhetVelger';
import { useSortering } from '../kandidater/useSortering';
import { useKandidatvalg } from '../kandidater/useKandidatvalg';
import { useNodeEtiketter, useRettskildeoppslag } from '../kandidater/useNodeEtiketter';
import { Massehandlingsrad } from '../kandidater/Massehandlingsrad';

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

/**
 * [Ny, kandidatside-runden, 2026-09-09, issue #167] Kolonnen viste den rå koden («hoy»). Kodene er et
 * lukket vokabular med CHECK-constraint i basen, og oversettelsen hører i visningen — samme
 * arbeidsdeling som `KonfidensTag` gjør for navnekandidatene. Ukjente koder vises som de er, ikke
 * gjettet om til noe annet.
 */
const KONFIDENS_TEKST: Record<string, string> = {
  hoy: 'Høy',
  middels: 'Middels',
  lav: 'Lav',
  krever_oppslag: 'Krever oppslag',
};

const DEFINISJON_AVKORT_LENGDE = 100;

// [Ny, fler-verdi-departement, 2026-09-04] En rettskilde kan ha FLERE ansvarlige departementer —
// forhåndsutfyllingen under trenger ÉN virksomhet, så det FØRSTE departementet i kildens egen rekkefølge
// som faktisk løser til en ekte Virksomhet brukes (samme "primær eier"-valg som andre steder i denne
// runden, se NavnekandidatOppdagelseTjeneste.OpprettDepartementTaggHvisMuligAsync).
function forsteAnsvarligDepartementVirksomhetId(detalj: RettskildeDetalj): string | null {
  return detalj.ansvarligDepartementLenker.find((l) => l.virksomhetId != null)?.virksomhetId ?? null;
}

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
  // [Ny, kandidatside-runden, 2026-09-09, issue #167] Konfidensfilteret. Serverfilteret kom i #222,
  // men ingen klient sendte parameteren — se filterkortet i JSX for hvorfor dette er et FILTER og
  // ikke bare en kolonne.
  const [konfidensFilter, setKonfidensFilter] = useState<'' | 'hoy' | 'middels' | 'lav' | 'krever_oppslag'>('');

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

  // Forhåndsutfylling av virksomhetsvalget fra rettskildens ansvarlige departement (Johanns forslag,
  // 2026-09-02) — ALDRI en gjettet fallback: `RettskildeDetalj.ansvarligDepartementLenker` inneholder
  // ett bekreftet, eksakt navnetreff PER departement (resolvert på serveren, se Program.cs
  // GET /api/rettskilder/{id}), og feltet forblir tomt når INGEN av dem løste, akkurat som før.
  // [ENDRET, fler-verdi-departement, 2026-09-04] En rettskilde kan ha flere departementer — det FØRSTE
  // som faktisk løser til en ekte Virksomhet brukes til forhåndsutfyllingen (se
  // forsteAnsvarligDepartementVirksomhetId under), samme "primær eier"-valg som andre steder i denne
  // runden (NavnekandidatOppdagelseTjeneste.OpprettDepartementTaggHvisMuligAsync).
  // `RettskildeSammendrag` (allerede hentet over til `rettskilder`, brukt for `visRettskilde`) har
  // KUN den rå departement-STRENGEN, ikke det resolverte virksomhet-id-et — det krever et eget kall
  // mot detalj-endepunktet. Hentes LATIG (kun når godkjenn-dialogen faktisk åpnes, ikke for alle
  // rader i lista) og bufres per rettskildeId slik at å åpne dialogen for flere kandidater fra SAMME
  // rettskilde ikke gir gjentatte nettverkskall.
  const [rettskildeDetaljerPerId, setRettskildeDetaljerPerId] = useState<Map<string, RettskildeDetalj>>(new Map());
  const sisteRettskildeDetaljForesporsel = useRef(0);

  // [ENDRET, kandidatside-runden, 2026-09-09, issue #216] Delt hook — de tre kandidatsidene hadde
  // hver sin identiske kopi av sorteringstilstanden og de to hjelpefunksjonene.
  const sortering = useSortering<Sorteringskolonne>('opprettet', false);

  // [Ny, kandidatside-runden, 2026-09-09, issue #216] Avkryssing + massehandling. Samme delte hook og
  // samme delte rad som de to andre kandidatsidene; denne siden hadde ingen av dem.
  //
  // Virksomheten holdes SEPARAT fra `godkjennVirksomhetId` (enkeltrad-dialogen) med vilje: dialogen
  // forhåndsutfylles fra rettskildens ansvarlige departement per rad, mens massevalget gjelder et
  // utvalg som kan spenne flere rettskilder. Å dele feltet ville latt en forhåndsutfylling fra ÉN rad
  // bestemme registeret for tjue andre — nøyaktig den slags gjetting applikasjonen ikke skal gjøre.
  const valg = useKandidatvalg();
  const [masseVirksomhetId, setMasseVirksomhetId] = useState('');
  const [massehandlingKjorer, setMassehandlingKjorer] = useState(false);
  const [massehandlingFeil, setMassehandlingFeil] = useState<string | null>(null);

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
        konfidens: konfidensFilter || undefined,
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

  useEffect(lastForekomster, [rettskildeFilter, monsterFilter, statusFilter, konfidensFilter]);

  function lastAvvisteForekomster() {
    api
      .hentBegrepsforekomster({ rettskildeId: rettskildeFilter || undefined, status: 'Avvist' })
      .then(setAvvisteForekomster)
      .catch(() => setAvvisteForekomster(null));
  }

  useEffect(lastAvvisteForekomster, [rettskildeFilter]);

  const rettskildeOppslag = useRettskildeoppslag(rettskilder);

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
    setGodkjennFeil(null);

    const bufret = rettskildeDetaljerPerId.get(forekomst.rettskildeId);
    if (bufret) {
      setGodkjennVirksomhetId(forsteAnsvarligDepartementVirksomhetId(bufret) ?? '');
      return;
    }

    // Ingen gjettet fallback mens oppslaget pågår — feltet starter tomt, akkurat som om
    // forhåndsutfylling ikke fantes, og brukeren kan søke opp virksomheten manuelt uansett utfall.
    setGodkjennVirksomhetId('');
    const denneForesporselen = ++sisteRettskildeDetaljForesporsel.current;
    api
      .hentRettskilde(forekomst.rettskildeId)
      .then((detalj) => {
        setRettskildeDetaljerPerId((forrige) => new Map(forrige).set(forekomst.rettskildeId, detalj));
        // Kun forhåndsutfyll dersom godkjenn-dialogen fortsatt er den samme forespørselen utløste —
        // vern mot at brukeren rakk å lukke/åpne dialogen for en ANNEN kandidat før dette svarte.
        if (denneForesporselen === sisteRettskildeDetaljForesporsel.current) {
          setGodkjennVirksomhetId(forsteAnsvarligDepartementVirksomhetId(detalj) ?? '');
        }
      })
      .catch(() => {
        // Stille feil — forhåndsutfylling er en bekvemmelighet, ikke kritisk. Feltet forblir tomt og
        // brukeren søker opp virksomheten manuelt via VirksomhetVelger, som før denne fiksen.
      });
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

  // ---------- Massehandling (#216) ----------

  /** Felles etterbehandling: samme per-rad-feilrapportering som de to andre køene. */
  function rapporterBatch(rader: { ok: boolean; feil: string | null }[]) {
    const feilede = rader.filter((r) => !r.ok);
    if (feilede.length > 0) {
      setMassehandlingFeil(
        `${feilede.length} av ${rader.length} rad(er) feilet: ${feilede.map((r) => r.feil).join('; ')}`,
      );
    }
    lastForekomster();
    lastAvvisteForekomster();
  }

  async function massegodkjenn() {
    if (valg.antall === 0) return;
    // Ingen gjettet virksomhet. Dette er den ene forutsetningen godkjenning her har som de to andre
    // køene ikke har, og den skal sies rett ut i stedet for å utledes fra f.eks. første rads
    // departement.
    if (!masseVirksomhetId) {
      setMassehandlingFeil('Velg hvilket register begrepene skal landes i før du godkjenner.');
      return;
    }
    setMassehandlingKjorer(true);
    setMassehandlingFeil(null);
    try {
      const resultat = await api.godkjennBegrepsforekomsterBatch({
        ider: [...valg.valgte],
        virksomhetId: masseVirksomhetId,
      });
      rapporterBatch(resultat.rader);
      valg.nullstill();
    } catch (err) {
      setMassehandlingFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved massegodkjenning.');
    } finally {
      setMassehandlingKjorer(false);
    }
  }

  async function massavvis() {
    if (valg.antall === 0) return;
    setMassehandlingKjorer(true);
    setMassehandlingFeil(null);
    try {
      const resultat = await api.avvisBegrepsforekomsterBatch({ ider: [...valg.valgte] });
      rapporterBatch(resultat.rader);
      valg.nullstill();
    } catch (err) {
      setMassehandlingFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved masseavvisning.');
    } finally {
      setMassehandlingKjorer(false);
    }
  }

  /**
   * Presis sletting av de avkryssede radene — kun 'Avvist' kan slettes, akkurat som enkeltrad-
   * slettingen. Hopper stille over de valgte radene som ikke er avvist i stedet for å feile hele
   * handlingen, og sier i dialogen hvor mange som faktisk slettes vs. hoppes over. Samme oppførsel
   * som VirksomhetKandidaterListe — det er den siden brukeren nettopp kom fra.
   */
  async function slettValgte() {
    if (valg.antall === 0) return;
    const avvisteValgte = (forekomster ?? []).filter((f) => valg.erValgt(f.id) && f.status === 'Avvist').map((f) => f.id);
    const hoppetOver = valg.antall - avvisteValgte.length;
    if (avvisteValgte.length === 0) {
      setMassehandlingFeil('Ingen av de valgte radene er avvist — kun avviste kandidater kan slettes her.');
      return;
    }
    const advarsel = hoppetOver > 0
      ? `${avvisteValgte.length} avvist(e) kandidat(er) slettes permanent. ${hoppetOver} valgte rad(er) er ikke avvist og hoppes over. Fortsette?`
      : `Slette ${avvisteValgte.length} avvist(e) kandidat(er) permanent? Dette kan ikke angres.`;
    if (!window.confirm(advarsel)) return;

    setMassehandlingKjorer(true);
    setMassehandlingFeil(null);
    try {
      // Ingen batch-DELETE finnes for denne køen — N kall, men bare for de radene som faktisk kan
      // slettes. Samme løsning som VirksomhetKandidaterListe, med samme begrensning.
      for (const id of avvisteValgte) {
        await api.slettBegrepsforekomst(id);
      }
      valg.nullstill();
      lastForekomster();
      lastAvvisteForekomster();
    } catch (err) {
      setMassehandlingFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved sletting av valgte kandidater.');
    } finally {
      setMassehandlingKjorer(false);
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
      sortering.kolonne === 'begrep'
        ? f.begrep
        : sortering.kolonne === 'monster'
          ? f.monsterId
          : sortering.kolonne === 'rettskilde'
            ? rettskildeOppslag.tittel(f.rettskildeId)
            : sortering.kolonne === 'status'
              ? f.status
              : f.opprettetTidspunkt;
    return [...forekomster].sort((a, b) => {
      const cmp = sortnokkel(a).localeCompare(sortnokkel(b), 'nb');
      return sortering.stigende ? cmp : -cmp;
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [forekomster, sortering.kolonne, sortering.stigende, rettskildeOppslag.perId]);

  const paginering = usePaginering(viste ?? []);

  // [Ny, 2026-09-02, issue #115] Node-tekst per rettskilde — samme lazy-per-rettskilde-mønster som
  // VirksomhetKandidaterListe.tsx/NavnekandidaterListe.tsx, kun for rettskildene bak GJELDENDE SIDE.
  // [ENDRET, kandidatside-runden, 2026-09-09, issue #216] Delt hook: sen node-henting per
  // rettskilde for de VISTE radene, og etiketten via paragrafEtikett. Den lokale kopien her
  // bygde «§ {node.nummer}», som for et LEDD ga leddnummeret — «§ 6» for § 36 sjette ledd.
  const nodeEtiketter = useNodeEtiketter(paginering.visteRader);

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

        {/* [Ny, kandidatside-runden, 2026-09-09, issue #167] Konfidens som FILTER, ikke bare kolonne.
            Serverfilteret kom i #222; her sendes det først. Grunnen til at det trengs: M11-mønstrene
            «beregnes»/«angir» ble lagt til på konfidens 'lav' nettopp fordi de treffer bredere enn de
            eksplisitte definisjonsmønstrene. Å kunne se KUN de lave radene er å kunne kvalitetssikre
            det mønstervalget — og å kunne se kun de høye er å kunne massegodkjenne trygt. */}
        <Field style={{ minWidth: '13rem' }}>
          <Label>Konfidens</Label>
          <Select data-size="sm" value={konfidensFilter} onChange={(e) => setKonfidensFilter(e.target.value as typeof konfidensFilter)}>
            <Select.Option value="">All konfidens</Select.Option>
            <Select.Option value="hoy">Høy</Select.Option>
            <Select.Option value="middels">Middels</Select.Option>
            <Select.Option value="lav">Lav</Select.Option>
            <Select.Option value="krever_oppslag">Krever oppslag</Select.Option>
          </Select>
        </Field>
      </div>

      {/* [Ny, kandidatside-runden, 2026-09-09, issue #216] Massehandling — den eneste kandidatkøen som
          ikke hadde det. Et M1-sveip på én definisjonsparagraf gir tjue rader i samme paragraf, og én
          rad om gangen er ikke en arbeidsflate.

          Virksomhetsvelgeren står INNI raden fordi godkjenning her ikke kan utledes: en forekomst er
          delt/objektiv, men et begrepsregister har en eier (se klassekommentaren). Den gjelder hele
          utvalget — rader fra samme definisjonsparagraf hører til samme register; skal to begreper til
          ULIKE registre, er det to utvalg. */}
      <Massehandlingsrad
        antallValgte={valg.antall}
        kjorer={massehandlingKjorer}
        feil={massehandlingFeil}
        merknad={rettskildeFilter ? ' — filtrert til én rettskilde' : undefined}
        onGodkjenn={massegodkjenn}
        onAvvis={massavvis}
        onSlett={slettValgte}
      >
        <div style={{ minWidth: '18rem' }}>
          <VirksomhetVelger
            virksomheter={virksomheter}
            value={masseVirksomhetId}
            onChange={setMasseVirksomhetId}
            label="Registeret begrepene landes i"
            tomValgTekst="Velg virksomhet …"
          />
        </div>
      </Massehandlingsrad>

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
                  {/* [Ny, #216] Samme hovedbryter som de to andre kandidatsidene: «alle viste» gjelder
                      GJELDENDE SIDE, og avhukning nullstiller hele utvalget (se useKandidatvalg). */}
                  <Table.HeaderCell>
                    <Checkbox
                      aria-label="Velg alle viste"
                      checked={valg.alleVisteErValgt(paginering.visteRader)}
                      onChange={(e) => valg.velgAlleViste(paginering.visteRader, e.target.checked)}
                    />
                  </Table.HeaderCell>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => sortering.bytt('begrep')}>
                      Begrep{sortering.indikator('begrep')}
                    </button>
                  </Table.HeaderCell>
                  <Table.HeaderCell>Definisjon</Table.HeaderCell>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => sortering.bytt('monster')}>
                      Mønster{sortering.indikator('monster')}
                    </button>
                  </Table.HeaderCell>
                  <Table.HeaderCell>Konfidens</Table.HeaderCell>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => sortering.bytt('rettskilde')}>
                      Rettskilde{sortering.indikator('rettskilde')}
                    </button>
                  </Table.HeaderCell>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => sortering.bytt('status')}>
                      Status{sortering.indikator('status')}
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
                      <Table.Cell>
                        <Checkbox
                          aria-label={`Velg ${f.begrepOriginal}`}
                          checked={valg.erValgt(f.id)}
                          onChange={(e) => valg.veksl(f.id, e.target.checked)}
                        />
                      </Table.Cell>
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
                        <Tag data-color={KONFIDENS_FARGE[f.konfidens] ?? 'neutral'} data-size="sm">
                          {KONFIDENS_TEKST[f.konfidens] ?? f.konfidens}
                        </Tag>
                      </Table.Cell>
                      <Table.Cell style={{ fontSize: 'var(--ds-font-size-1)' }}>
                        <Link asChild>
                          <RouterLink to={rettskildeLenkeForId(f.rettskildeId, f.nodeEid)} target="_blank">
                            {rettskildeOppslag.tittel(f.rettskildeId)} — {nodeEtiketter.etikett(f.rettskildeId, f.nodeEid)} ↗
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
