import { Fragment, useEffect, useMemo, useRef, useState } from 'react';
import { Link as RouterLink, useSearchParams } from 'react-router';
import { Alert, Button, Card, Checkbox, Field, Heading, Label, Link, Paragraph, Select, Table, Tag, Textfield, ToggleGroup } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenkeForId } from '../api/eidLenker';
import type { NavnekandidatDto, RettskildeSammendrag } from '../api/types';
import { RettskildeFlervalg } from '../rettskilde/RettskildeFlervalg';
import { RettskildeVelger } from '../rettskilde/RettskildeVelger';
import { Pagineringskontroll } from '../tabell/Pagineringskontroll';
import { usePaginering } from '../tabell/usePaginering';

type Sorteringskolonne = 'foreslattTekst' | 'kategori' | 'rettskilde' | 'status' | 'opprettet';

/**
 * Gruppering av listen (2026-08-30, Johanns eksplisitte ønske "det må bli enklere å se forslagene i
 * sammenheng ... gruppere/vise hierarkisk") — klient-side, samme "over den allerede hentede listen"-
 * prinsipp som resten av filtreringen/sorteringen her, ikke et nytt serverendepunkt. 'ingen' er
 * standard (dagens flate visning, uendret). Grupperingsnøkkelen for 'foreslattTekst' er den EKSAKTE
 * strengen (case-sensitiv) — normaliserer IKKE bort store/små bokstaver-varianter selv (det er
 * `navnekandidat-flerords-normalisering`-branchens ansvar server-side, ikke gjort her); grupperingen
 * fungerer generelt uansett, og vil automatisk bli enda mer effektiv den dagen den branchen merges.
 */
type Gruppering = 'ingen' | 'foreslattTekst' | 'rettskilde';

interface Kandidatgruppe {
  nokkel: string;
  visningsnavn: string;
  rader: NavnekandidatDto[];
}

const STATUS_FARGE: Record<string, 'neutral' | 'warning' | 'success' | 'danger'> = {
  Venter: 'warning',
  Godkjent: 'success',
  Avvist: 'danger',
};

const KATEGORI_FARGE: Record<string, 'info' | 'accent'> = {
  virksomhet: 'accent',
  gruppe: 'info',
};

/**
 * Oppdagelseskø (docs/13-backlog.md §9) — komplementær til `VirksomhetKandidaterListe.tsx`, samme
 * mønster tett fulgt (sveip-panel + filtrerbar tabell + godkjenn/avvis per rad). Den avgjørende
 * forskjellen fra virksomhetskandidatene er hva "godkjenn" faktisk gjør, se `kandidatHandlingTekst`:
 * for `"gruppe"` opprettes et EKTE gruppebegrep direkte (serversiden har alt den trenger), for
 * `"virksomhet"` settes kun status — selve virksomhetskoblingen (ny ELLER eksisterende virksomhet)
 * krever et menneske og skjer via Brreg-søket/"opprett med bare navn"-skjemaet på `/virksomheter`
 * (lenken under sender med `?forslagNavn=` som forhåndsutfyller begge der).
 *
 * Massehandling (avkrysningsbokser + «Godkjenn valgte»/«Avvis valgte», 2026-08-30) — store
 * test-sveip gjennom hele det importerte korpuset kan legge svært mange kandidater i køen samtidig,
 * og enkeltrad-behandling skalerer ikke da. Samme UX/backend-mønster som
 * VirksomhetKandidaterListe.tsx (se den filens kommentarer for hele resonnementet) — batchen
 * håndterer BEGGE kategoriene korrekt i samme kall siden serveren uansett kaller samme
 * GodkjennAsync/AvvisAsync per rad, ikke en egen batch-spesifikk forgrening.
 *
 * Sortering/gruppering/filtrering (2026-08-30) — med ~3990 kandidater i én flat, rettskilde-ordnet
 * liste ba Johann eksplisitt (to ganger) om bedre oversikt: "sortere på foreslått tekst ... gruppere/
 * vise hierarkisk ... multiple select på rettskilde, foreslått tekst". Alt er klient-side over den
 * allerede hentede `kandidater`-listen, samme mønster som BegreperListe.tsx sitt filter/sortering —
 * ingen nye serverendepunkter. `rettskildeId`-serverfilteret som fantes her tidligere (ett enkelt
 * valg, sendt som spørreparameter) er ERSTATTET av et klient-side flervalgsfilter
 * (`RettskildeFlervalg`, samme komponent som «Identifiser begrep»/«Identifiser tjenester» bruker for
 * å velge blant 5893 rettskilder uten å mounte alle som options, se docs/09 §10) — kategori/status
 * er fortsatt reelle serverfiltre (uendret). Gruppering ('ingen' | 'foreslattTekst' | 'rettskilde')
 * er en ren visningsmodus: ved gruppering vises IKKE paginering (antall GRUPPER er uansett drastisk
 * lavere enn antall rader, se `Kandidatgruppe`), og «velg alle»/gruppens egen avkrysningsboks
 * velger radene i det fulle filtrerte settet uansett kollaps-tilstand (kollaps skjuler kun VISNING,
 * ikke utvalg) — se `vekslGruppe`/`raderForMasterSjekkboks`.
 */
export default function NavnekandidaterListe() {
  const [searchParams] = useSearchParams();
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);

  const [kategoriFilter, setKategoriFilter] = useState<'virksomhet' | 'gruppe' | ''>('');
  const [statusFilter, setStatusFilter] = useState<'Venter' | 'Godkjent' | 'Avvist' | 'Alle'>('Venter');

  // Klient-side filtre (se klassekommentaren) — virker på den allerede hentede `kandidater`-listen,
  // ikke på serverspørringen. Forhåndsutfylt fra ?rettskildeId=... (RettskildeDetalj.tsx sin «Sveip
  // etter navnekandidater»-lenke, navnekandidat-fiks 3 del 2) — kun en INITIAL verdi (useState-
  // argumentet evalueres kun ved første render); brukeren kan fritt endre/utvide flervalgsfilteret
  // videre via RettskildeFlervalg under, akkurat som uten lenken.
  const [rettskildeValgteFilter, setRettskildeValgteFilter] = useState<Set<string>>(() => {
    const forhandsvalgt = searchParams.get('rettskildeId');
    return forhandsvalgt ? new Set([forhandsvalgt]) : new Set();
  });
  const [filterForeslattTekst, setFilterForeslattTekst] = useState('');

  const [gruppering, setGruppering] = useState<Gruppering>('ingen');
  const [gruppeApne, setGruppeApne] = useState<Set<string>>(new Set());

  const [kandidater, setKandidater] = useState<NavnekandidatDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [laster, setLaster] = useState(false);

  const [valgte, setValgte] = useState<Set<string>>(new Set());
  const [massehandlingKjorer, setMassehandlingKjorer] = useState(false);
  const [massehandlingFeil, setMassehandlingFeil] = useState<string | null>(null);

  // Sletting (2026-08-30) — se docs-kommentaren i NavnekandidatOppdagelseTjeneste.SlettAsync/
  // SlettAlleAsync for hvorfor "avvis" alene ikke holder for ytelsestest-scenarioet.
  const [sletterAlle, setSletterAlle] = useState(false);
  const [slettAlleFeil, setSlettAlleFeil] = useState<string | null>(null);

  const [sveipRettskildeId, setSveipRettskildeId] = useState('');
  const [sveiper, setSveiper] = useState(false);
  const [sveipFeil, setSveipFeil] = useState<string | null>(null);
  const [sveipResultat, setSveipResultat] = useState<{ funnet: number; nye: number } | null>(null);

  const [sortKolonne, setSortKolonne] = useState<Sorteringskolonne>('opprettet');
  const [sortStigende, setSortStigende] = useState(false);

  useEffect(() => {
    api.hentRettskilder().then(setRettskilder).catch(() => setRettskilder([]));
  }, []);

  // Samme "kun siste utstedte forespørsel får sette state"-vern som VirksomhetKandidaterListe.tsx —
  // se den filens kommentar for hele resonnementet (rask filterbytte kan ellers la et treg, eldre
  // svar overskrive et nyere).
  const sisteForesporsel = useRef(0);

  function lastKandidater() {
    const denneForesporselen = ++sisteForesporsel.current;
    setLaster(true);
    setFeil(null);
    api
      .hentNavnekandidater({
        kategori: kategoriFilter || undefined,
        status: statusFilter,
      })
      .then((liste) => {
        if (denneForesporselen !== sisteForesporsel.current) return;
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

  useEffect(lastKandidater, [kategoriFilter, statusFilter]);

  // Nytt grupperingsvalg — forrige åpne/lukkede grupper gjelder ikke lenger (andre nøkler: rettskilde-
  // id-er vs. foreslått-tekst-strenger).
  useEffect(() => {
    setGruppeApne(new Set());
  }, [gruppering]);

  const rettskilderPerId = useMemo(() => new Map(rettskilder.map((r) => [r.id, r] as const)), [rettskilder]);
  function visRettskilde(rettskildeId: string): string {
    return rettskilderPerId.get(rettskildeId)?.kortnavn ?? rettskilderPerId.get(rettskildeId)?.tittel ?? rettskildeId;
  }
  // Navnekandidat-fiks 2 (2026-08-30) — Lovdatas eget metadata for HVILKET departement en rettskilde
  // faktisk gjelder (RettskildeEntitet.AnsvarligDepartement), slått opp via den allerede-hentede
  // rettskildelisten (samme mønster som visRettskilde over) i stedet for et eget kall. Spesielt viktig
  // for "departementet"/"Kongen i statsråd"-kandidater, som ellers ikke sier noe om HVILKET departement.
  function visAnsvarligDepartement(rettskildeId: string): string | null {
    return rettskilderPerId.get(rettskildeId)?.ansvarligDepartement ?? null;
  }

  async function kjorSveip() {
    setSveiper(true);
    setSveipFeil(null);
    setSveipResultat(null);
    try {
      const resultat = await api.sveipNavnekandidater({ rettskildeId: sveipRettskildeId || null });
      setSveipResultat({ funnet: resultat.antallTreffFunnet, nye: resultat.antallNyeKandidater });
      lastKandidater();
    } catch (err) {
      setSveipFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved sveip.');
    } finally {
      setSveiper(false);
    }
  }

  async function enkelthandling(id: string, handling: 'godkjenn' | 'avvis') {
    try {
      if (handling === 'godkjenn') await api.godkjennNavnekandidat(id);
      else await api.avvisNavnekandidat(id);
      lastKandidater();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved behandling av kandidat.');
    }
  }

  function vekslValgt(id: string, valgt: boolean) {
    setValgte((forrige) => {
      const ny = new Set(forrige);
      if (valgt) ny.add(id); else ny.delete(id);
      return ny;
    });
  }

  // "Alle viste" = alle på GJELDENDE SIDE ved 'ingen' gruppering (samme avgrensning som
  // VirksomhetKandidaterListe.tsx), MEN hele det filtrerte settet ved gruppering (ingen paginering da
  // — se `raderForMasterSjekkboks`).
  function vekslAlleViste(valgt: boolean) {
    setValgte(valgt ? new Set(raderForMasterSjekkboks.map((k) => k.id)) : new Set());
  }

  // Gruppens EGEN avkrysningsboks — velger/fjerner ALLE radene i gruppen (uansett om gruppen er
  // kollapset, jf. Johanns krav om at «velg alle»-lignende kontroller skal treffe hele gruppen, ikke
  // bare det synlige) uten å nullstille resten av utvalget (i motsetning til `vekslAlleViste`, som
  // bevisst nullstiller alt ved avhukning — samme mønster videreført herfra).
  function vekslGruppe(rader: NavnekandidatDto[], valgt: boolean) {
    setValgte((forrige) => {
      const ny = new Set(forrige);
      for (const k of rader) {
        if (valgt) ny.add(k.id); else ny.delete(k.id);
      }
      return ny;
    });
  }

  function vekslGruppeApen(nokkel: string) {
    setGruppeApne((forrige) => {
      const ny = new Set(forrige);
      if (ny.has(nokkel)) ny.delete(nokkel); else ny.add(nokkel);
      return ny;
    });
  }

  // Radene «Slett alle kandidater» faktisk vil ramme — samme (server-filtrerte) `kandidater`-liste som
  // resten av siden viser (kategori/status er reelle serverfiltre, se lastKandidater), snevret inn av
  // rettskilde-FLERVALGET (client-side, samme mengde som resten av filtreringen bruker). Bevisst IKKE
  // filtrert av `filterForeslattTekst` — det finnes ingen tilsvarende serverfilter for fritekst i
  // slett-endepunktet (kun status/kategori/rettskildeId, samme filterparametre som GET /), se
  // advarselsteksten i "Slett kandidater"-kortet under.
  const kandidaterForSletting = useMemo(
    () => (kandidater ?? []).filter((k) => rettskildeValgteFilter.size === 0 || rettskildeValgteFilter.has(k.rettskildeId)),
    [kandidater, rettskildeValgteFilter],
  );

  async function slettEnkelt(id: string) {
    if (!window.confirm('Slette denne kandidaten permanent? Dette kan ikke angres.')) return;
    try {
      await api.slettNavnekandidat(id);
      lastKandidater();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved sletting av kandidat.');
    }
  }

  async function slettAlle() {
    const antall = kandidaterForSletting.length;
    if (antall === 0) return;
    const tekstAdvarsel = filterForeslattTekst.trim()
      ? ` («Foreslått tekst inneholder»-filteret påvirker IKKE denne slettingen — kun kategori/status/rettskilde gjør det)`
      : '';
    if (!window.confirm(`Slette ${antall} kandidat(er) permanent${tekstAdvarsel}? Dette kan ikke angres.`)) return;

    setSletterAlle(true);
    setSlettAlleFeil(null);
    try {
      const statusParam = statusFilter === 'Alle' ? undefined : statusFilter;
      const kategoriParam = kategoriFilter || undefined;
      // Backend-filteret tar KUN én rettskildeId av gangen (samme filter-signatur som ListerAsync) —
      // flervalget her løses derfor med ett kall per valgt rettskilde (eller ett kall uten
      // rettskildeId-filter, dvs. alle rettskilder, hvis ingen er valgt i flervalget).
      const rettskildeIder = rettskildeValgteFilter.size > 0 ? [...rettskildeValgteFilter] : [undefined];
      for (const rettskildeId of rettskildeIder) {
        await api.slettAlleNavnekandidater({ status: statusParam, kategori: kategoriParam, rettskildeId });
      }
      lastKandidater();
    } catch (err) {
      setSlettAlleFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved massesletting.');
    } finally {
      setSletterAlle(false);
    }
  }

  async function massehandling(handling: 'godkjenn' | 'avvis') {
    if (valgte.size === 0) return;
    setMassehandlingKjorer(true);
    setMassehandlingFeil(null);
    try {
      const request = { ider: [...valgte] };
      const resultat = handling === 'godkjenn'
        ? await api.godkjennNavnekandidaterBatch(request)
        : await api.avvisNavnekandidaterBatch(request);
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
    const tekst = filterForeslattTekst.trim().toLowerCase();
    const filtrert = kandidater.filter((k) => {
      if (rettskildeValgteFilter.size > 0 && !rettskildeValgteFilter.has(k.rettskildeId)) return false;
      if (tekst && !k.foreslattTekst.toLowerCase().includes(tekst)) return false;
      return true;
    });
    const sortnokkel = (k: NavnekandidatDto) =>
      sortKolonne === 'foreslattTekst'
        ? k.foreslattTekst
        : sortKolonne === 'kategori'
          ? k.kategori
          : sortKolonne === 'rettskilde'
            ? visRettskilde(k.rettskildeId)
            : sortKolonne === 'status'
              ? k.status
              : k.opprettetTidspunkt;
    return [...filtrert].sort((a, b) => {
      const cmp = sortnokkel(a).localeCompare(sortnokkel(b), 'nb');
      return sortStigende ? cmp : -cmp;
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [kandidater, rettskildeValgteFilter, filterForeslattTekst, sortKolonne, sortStigende, rettskilderPerId]);

  const paginering = usePaginering(viste ?? []);

  // Gruppert visning (se `Gruppering`-kommentaren over) — bygget OVENPÅ det allerede filtrerte og
  // sorterte `viste`-settet, altså inkluderer den samme klient-filtreringen/sorteringen som den flate
  // visningen. Radrekkefølgen INNI hver gruppe arver dermed `sortKolonne`/`sortStigende`; selve
  // GRUPPENE sorteres etter antall (flest først, jf. Johanns "se forslagene i sammenheng" — de mest
  // gjentatte forslagene er det mest interessante å se samlet), med alfabetisk (nb) som tiebreak.
  const grupper = useMemo<Kandidatgruppe[] | null>(() => {
    if (!viste || gruppering === 'ingen') return null;
    const perNokkel = new Map<string, NavnekandidatDto[]>();
    for (const k of viste) {
      const nokkel = gruppering === 'foreslattTekst' ? k.foreslattTekst : k.rettskildeId;
      const eksisterende = perNokkel.get(nokkel);
      if (eksisterende) eksisterende.push(k); else perNokkel.set(nokkel, [k]);
    }
    return [...perNokkel.entries()]
      .map(([nokkel, rader]): Kandidatgruppe => ({
        nokkel,
        visningsnavn: gruppering === 'rettskilde' ? visRettskilde(nokkel) : nokkel,
        rader,
      }))
      .sort((a, b) => b.rader.length - a.rader.length || a.visningsnavn.localeCompare(b.visningsnavn, 'nb'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [viste, gruppering, rettskilderPerId]);

  // Radene «velg alle»-toppboksen (og av-huking) skal virke på: gjeldende SIDE ved flat visning
  // (samme avgrensning som før), men HELE det filtrerte settet ved gruppering — der finnes det ingen
  // paginering å avgrense til (se JSX under), og kollapsede grupper skal fortsatt kunne velges i sin
  // helhet.
  const raderForMasterSjekkboks = gruppering === 'ingen' ? paginering.visteRader : (viste ?? []);

  function apneAlleGrupper() {
    if (grupper) setGruppeApne(new Set(grupper.map((g) => g.nokkel)));
  }
  function lukkAlleGrupper() {
    setGruppeApne(new Set());
  }

  // Selve raden — delt mellom flat visning (`paginering.visteRader.map(...)`) og gruppert visning
  // (radene inni en åpnet gruppe), slik at markup for én rad kun finnes ett sted.
  function renderKandidatRad(k: NavnekandidatDto) {
    return (
      <Table.Row key={k.id}>
        <Table.Cell>
          <Checkbox
            aria-label={`Velg kandidat ${k.id}`}
            checked={valgte.has(k.id)}
            onChange={(e) => vekslValgt(k.id, e.target.checked)}
          />
        </Table.Cell>
        <Table.Cell>
          <Tag data-color={KATEGORI_FARGE[k.kategori] ?? 'neutral'} data-size="sm">{k.kategori}</Tag>
        </Table.Cell>
        <Table.Cell style={{ fontWeight: 500 }}>{k.foreslattTekst}</Table.Cell>
        <Table.Cell>{visRettskilde(k.rettskildeId)}</Table.Cell>
        <Table.Cell style={{ fontFamily: 'monospace', fontSize: 'var(--ds-font-size-1)' }}>
          <Link asChild>
            <RouterLink to={rettskildeLenkeForId(k.rettskildeId, k.nodeEid)} target="_blank">{k.nodeEid} ↗</RouterLink>
          </Link>
        </Table.Cell>
        <Table.Cell>
          {(() => {
            const departement = visAnsvarligDepartement(k.rettskildeId);
            // Spesielt synlig for "departementet"/"Kongen i statsråd"-kandidater (se
            // metodekommentaren) — men vist for ALLE kategorier, siden feltet uansett bare
            // sier hvilket departement som eier RETTSKILDEN, ikke bare denne enkelttermen.
            return departement ? (
              <Tag data-color="neutral" data-size="sm">{departement}</Tag>
            ) : (
              <span style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>—</span>
            );
          })()}
        </Table.Cell>
        <Table.Cell>
          <Tag data-color={STATUS_FARGE[k.status] ?? 'neutral'} data-size="sm">{k.status}</Tag>
        </Table.Cell>
        <Table.Cell>
          <div style={{ display: 'flex', gap: '0.4rem', flexWrap: 'wrap', alignItems: 'center' }}>
            {k.status === 'Venter' ? (
              <>
                <Button data-size="sm" onClick={() => enkelthandling(k.id, 'godkjenn')}>Godkjenn</Button>
                <Button data-size="sm" variant="tertiary" onClick={() => enkelthandling(k.id, 'avvis')}>Avvis</Button>
                {k.kategori === 'virksomhet' && (
                  <Link asChild>
                    {/* `navnekandidatId` med (2026-08-30, "koble til eksisterende virksomhet"-veien) —
                        lar landingssiden tilby å godkjenne DENNE kandidatraden i samme handling som å
                        koble navneformen, se `KoblEksisterendeVirksomhetPanel` i VirksomheterListe.tsx. */}
                    <RouterLink
                      to={`/virksomheter?forslagNavn=${encodeURIComponent(k.foreslattTekst)}&navnekandidatId=${k.id}`}
                      target="_blank"
                    >
                      Finn/opprett virksomhet ↗
                    </RouterLink>
                  </Link>
                )}
              </>
            ) : (
              <span style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
                {k.behandletAv ? `Behandlet av ${k.behandletAv}` : '—'}
              </span>
            )}
            {/* Vist for ALLE statuser (ikke bare Venter) — formålet med sletting er full opprydding av
                korpuset (også allerede godkjente/avviste rader), se NavnekandidatOppdagelseTjeneste
                .SlettAsync sin kommentar for hvorfor. */}
            <Button data-size="sm" variant="tertiary" data-color="danger" onClick={() => slettEnkelt(k.id)}>
              Slett
            </Button>
          </div>
        </Table.Cell>
      </Table.Row>
    );
  }

  return (
    <>
      <Heading level={1} data-size="lg" style={{ marginBottom: '0.2rem' }}>
        Navnekandidater
      </Heading>
      <Paragraph style={{ marginBottom: '1.25rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
        Egennavn/juridiske aktører oppdaget ved regex-mønstergjenkjenning i allerede importert
        rettskildetekst (docs/13-backlog.md §9) — ren tekstanalyse, ikke KI. Komplementær til{' '}
        <Link asChild><RouterLink to="/virksomhet-kandidater">Virksomhetskandidater</RouterLink></Link>,
        som bekrefter FLERE forekomster av allerede kjente navn; dette er en oppdagelseskø for HELT NYE
        navn ingen registrert navneform/gruppebegrep dekker ennå.
      </Paragraph>

      <Card style={{ padding: '1rem', marginBottom: '1.5rem' }}>
        <Heading level={2} data-size="xs" style={{ marginBottom: '0.5rem' }}>
          Kjør sveip
        </Heading>
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', marginBottom: '0.5rem' }}>
          Ingen rettskilde valgt = hele det importerte korpuset. Dekningen er begrenset til det som
          faktisk er importert, ikke alle norske lover/forskrifter.
        </Paragraph>
        <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <RettskildeVelger rettskilder={rettskilder} value={sveipRettskildeId} onChange={setSveipRettskildeId} label="Rettskilde (tomt = hele korpuset)" />
          <Button onClick={kjorSveip} disabled={sveiper}>
            {sveiper ? 'Sveiper …' : 'Kjør sveip'}
          </Button>
        </div>
        {sveipFeil && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{sveipFeil}</Alert>}
        {sveipResultat && (
          <Alert data-color="info" style={{ marginTop: '0.5rem' }}>
            Fant {sveipResultat.funnet} treff totalt, {sveipResultat.nye} nye kandidater lagt i køen.
          </Alert>
        )}
      </Card>

      <div style={{ display: 'flex', gap: '1rem', alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: '1rem' }}>
        <Field style={{ minWidth: '12rem' }}>
          <Label>Kategori</Label>
          <Select data-size="sm" value={kategoriFilter} onChange={(e) => setKategoriFilter(e.target.value as typeof kategoriFilter)}>
            <Select.Option value="">Alle kategorier</Select.Option>
            <Select.Option value="virksomhet">Virksomhet</Select.Option>
            <Select.Option value="gruppe">Gruppe</Select.Option>
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
          Filtrer og grupper
        </Heading>
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
          Virker på listen som allerede er hentet (kategori/status over styrer selve
          serverspørringen) — nyttig for å se f.eks. samme foreslåtte tekst på tvers av mange
          rettskilder i sammenheng, i stedet for spredt ut over hundrevis av enkeltrader.
        </Paragraph>
        <div style={{ display: 'flex', gap: '1rem', alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: '0.75rem' }}>
          <RettskildeFlervalg
            rettskilder={rettskilder}
            valgte={rettskildeValgteFilter}
            onChange={setRettskildeValgteFilter}
            label="Rettskilder (tomt = alle)"
          />
          <Textfield
            label="Foreslått tekst inneholder"
            placeholder="f.eks. statsforvalteren"
            value={filterForeslattTekst}
            onChange={(e) => setFilterForeslattTekst(e.target.value)}
            style={{ maxWidth: '18rem' }}
          />
        </div>
        <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center', flexWrap: 'wrap' }}>
          <Label style={{ margin: 0 }}>Gruppering</Label>
          <ToggleGroup
            value={gruppering}
            onChange={(v) => setGruppering(v as Gruppering)}
            data-size="sm"
            data-toggle-group="Gruppering"
          >
            <ToggleGroup.Item value="ingen">Ingen (flat liste)</ToggleGroup.Item>
            <ToggleGroup.Item value="foreslattTekst">Foreslått tekst</ToggleGroup.Item>
            <ToggleGroup.Item value="rettskilde">Rettskilde</ToggleGroup.Item>
          </ToggleGroup>
          {gruppering !== 'ingen' && (
            <>
              <Button data-size="sm" variant="tertiary" onClick={apneAlleGrupper}>Åpne alle</Button>
              <Button data-size="sm" variant="tertiary" onClick={lukkAlleGrupper}>Lukk alle</Button>
            </>
          )}
        </div>
      </Card>

      <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '1rem', flexWrap: 'wrap' }}>
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', margin: 0 }}>
          {valgte.size} valgt{valgte.size === 1 ? '' : 'e'}
        </Paragraph>
        <Button data-size="sm" onClick={() => massehandling('godkjenn')} disabled={valgte.size === 0 || massehandlingKjorer}>
          {massehandlingKjorer ? 'Godkjenner …' : 'Godkjenn valgte'}
        </Button>
        <Button data-size="sm" variant="secondary" onClick={() => massehandling('avvis')} disabled={valgte.size === 0 || massehandlingKjorer}>
          {massehandlingKjorer ? 'Avviser …' : 'Avvis valgte'}
        </Button>
      </div>
      {massehandlingFeil && <div className="feilmelding" style={{ marginBottom: '1rem' }}>{massehandlingFeil}</div>}

      <Card style={{ padding: '1rem', marginBottom: '1rem' }}>
        <Heading level={2} data-size="xs" style={{ marginBottom: '0.5rem' }}>
          Slett kandidater
        </Heading>
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
          Ekte, irreversibel sletting av HELE korpuset eller et avgrenset delsett (kategori/status over,
          rettskilder i flervalget over) — nyttig for å tømme køen før et nytt sveip med oppdaterte
          mønsterregler (den posisjonsbaserte idempotensen hindrer ellers et nytt sveip i å re-evaluere
          allerede sveipet tekst). Respekterer IKKE «Foreslått tekst inneholder»-filteret over — kun
          kategori/status/rettskilde gjør det.
        </Paragraph>
        <Button
          data-size="sm"
          data-color="danger"
          onClick={slettAlle}
          disabled={kandidaterForSletting.length === 0 || sletterAlle}
        >
          {sletterAlle ? 'Sletter …' : `Slett alle kandidater (${kandidaterForSletting.length})`}
        </Button>
        {slettAlleFeil && <Alert data-color="danger" style={{ marginTop: '0.5rem' }}>{slettAlleFeil}</Alert>}
      </Card>

      {feil && <Alert data-color="danger" style={{ marginBottom: '1rem' }}>{feil}</Alert>}
      {laster && !kandidater && <Paragraph>Laster …</Paragraph>}
      {viste && viste.length === 0 && <Paragraph>Ingen kandidater matcher filteret.</Paragraph>}

      {viste && viste.length > 0 && (
        <Card style={{ padding: 0, overflow: 'hidden' }}>
          <div style={{ overflowX: 'auto' }}>
            <Table data-density="compact">
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell>
                    <Checkbox
                      aria-label="Velg alle viste"
                      checked={raderForMasterSjekkboks.length > 0 && raderForMasterSjekkboks.every((k) => valgte.has(k.id))}
                      onChange={(e) => vekslAlleViste(e.target.checked)}
                    />
                  </Table.HeaderCell>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('kategori')}>
                      Kategori{sorteringsindikator('kategori')}
                    </button>
                  </Table.HeaderCell>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('foreslattTekst')}>
                      Foreslått tekst{sorteringsindikator('foreslattTekst')}
                    </button>
                  </Table.HeaderCell>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('rettskilde')}>
                      Lov/forskrift{sorteringsindikator('rettskilde')}
                    </button>
                  </Table.HeaderCell>
                  <Table.HeaderCell>Node</Table.HeaderCell>
                  <Table.HeaderCell>Ansvarlig departement</Table.HeaderCell>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => bytteSortering('status')}>
                      Status{sorteringsindikator('status')}
                    </button>
                  </Table.HeaderCell>
                  <Table.HeaderCell>Handling</Table.HeaderCell>
                </Table.Row>
              </Table.Head>
              <Table.Body>
                {gruppering === 'ingen'
                  ? paginering.visteRader.map(renderKandidatRad)
                  : grupper!.map((g) => (
                      <Fragment key={g.nokkel}>
                        <Table.Row style={{ background: 'var(--ds-color-neutral-surface-tinted)' }}>
                          <Table.Cell>
                            <Checkbox
                              aria-label={`Velg alle i gruppen ${g.visningsnavn}`}
                              checked={g.rader.every((k) => valgte.has(k.id))}
                              onChange={(e) => vekslGruppe(g.rader, e.target.checked)}
                            />
                          </Table.Cell>
                          <Table.Cell colSpan={7}>
                            <button
                              type="button"
                              className="tabell-gruppe-knapp"
                              onClick={() => vekslGruppeApen(g.nokkel)}
                              aria-expanded={gruppeApne.has(g.nokkel)}
                            >
                              {gruppeApne.has(g.nokkel) ? '▼' : '▶'} {g.visningsnavn}
                            </button>
                            <Tag data-color="neutral" data-size="sm" style={{ marginLeft: '0.5rem' }}>
                              {g.rader.length} kandidat{g.rader.length === 1 ? '' : 'er'}
                            </Tag>
                          </Table.Cell>
                        </Table.Row>
                        {gruppeApne.has(g.nokkel) && g.rader.map(renderKandidatRad)}
                      </Fragment>
                    ))}
              </Table.Body>
            </Table>
          </div>
        </Card>
      )}

      {gruppering === 'ingen' && viste && viste.length > 0 && <Pagineringskontroll {...paginering} />}
    </>
  );
}
