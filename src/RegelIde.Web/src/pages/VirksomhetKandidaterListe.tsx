import { Fragment, useEffect, useMemo, useRef, useState } from 'react';
import { Link as RouterLink, useSearchParams } from 'react-router';
import { Button, Card, Checkbox, Field, Heading, Label, Link, Paragraph, Select, Table, Tag, Textfield, ToggleGroup } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { rettskildeLenkeForId } from '../api/eidLenker';
import type { RettskildeSammendrag, VirksomhetKandidatDto } from '../api/types';
import { Pagineringskontroll } from '../tabell/Pagineringskontroll';
import { usePaginering } from '../tabell/usePaginering';
import { useVirksomheter } from '../virksomhet/useVirksomheter';
import { VirksomhetVelger } from '../virksomhet/VirksomhetVelger';
import { KandidatflytForklaring } from '../kandidater/KandidatflytForklaring';
import { KandidatStatusTag } from '../kandidater/KandidatStatusTag';
import { useSortering } from '../kandidater/useSortering';
import { useKandidatvalg } from '../kandidater/useKandidatvalg';
import { Massehandlingsrad } from '../kandidater/Massehandlingsrad';
import { useNodeEtiketter, useRettskildeoppslag } from '../kandidater/useNodeEtiketter';

type Sorteringskolonne = 'virksomhet' | 'rettskilde' | 'status' | 'opprettet';

/**
 * [Ny, issue #265, 2026-09-11] Gruppert visning — samme "se forslagene i sammenheng"-behov Johann
 * ba om for NavnekandidaterListe.tsx (2026-08-30), samme klient-side-over-det-allerede-filtrerte-
 * settet-prinsipp. Grupperer på virksomhet eller rettskilde i stedet for foreslått tekst (denne
 * køen har ingen tilsvarende "foreslattTekst"-dimensjon å gruppere fritt på — hver rad ER allerede
 * knyttet til én bestemt, kjent virksomhet). 'ingen' er standard (dagens flate visning, uendret).
 *
 * Ikke delt med NavnekandidaterListe.tsx sin tilsvarende logikk ennå — det er nøyaktig den
 * generaliseringen issue #264 (delt kandidatside-infrastruktur) dekker. Bevisst en egen, lokal kopi
 * her i mellomtiden fremfor å la #265 vente på #264 (se #265 sitt akseptansekriterium 2).
 */
type Gruppering = 'ingen' | 'virksomhet' | 'rettskilde';

interface Kandidatgruppe {
  nokkel: string;
  visningsnavn: string;
  rader: VirksomhetKandidatDto[];
}

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

  // [Ny, issue #265] Fritekstsøk — filtrerer den allerede hentede/filtrerte lista klient-side på
  // virksomhetsnavn og rettskildetittel (samme to felt som vises i tabellen). IKKE på
  // "Navneform funnet"-teksten: den kommer fra node-teksten, som kun hentes lat for den GJELDENDE
  // SIDEN (`useNodeEtiketter(synligeRader)`) — å søke i den for hele treffsettet ville krevd å hente
  // node-tekst for alle rader på én gang, nøyaktig den render-trege regresjonen som allerede er
  // unngått bevisst andre steder i denne fila (se `nodeEtiketter`-kommentaren under).
  const [fritekstSok, setFritekstSok] = useState('');

  const [gruppering, setGruppering] = useState<Gruppering>('ingen');
  const [gruppeApne, setGruppeApne] = useState<Set<string>>(new Set());
  useEffect(() => setGruppeApne(new Set()), [gruppering]); // nytt grupperingsvalg — forrige åpne/lukkede grupper gjelder ikke lenger
  function vekslGruppeApen(nokkel: string) {
    setGruppeApne((forrige) => {
      const ny = new Set(forrige);
      if (ny.has(nokkel)) ny.delete(nokkel); else ny.add(nokkel);
      return ny;
    });
  }

  const [kandidater, setKandidater] = useState<VirksomhetKandidatDto[] | null>(null);
  const [feil, setFeil] = useState<string | null>(null);
  const [laster, setLaster] = useState(false);

  // [ENDRET, kandidatside-runden, 2026-09-09, issue #216] Delt hook. Merk at «alle viste»
  // NULLSTILLER hele utvalget ved avhukning, mens «velg gruppe» bare rører gruppens egne
  // rader — den forskjellen er en avgjørelse, og den bor nå ett sted. Se useKandidatvalg.
  const valg = useKandidatvalg();
  const [massehandlingKjorer, setMassehandlingKjorer] = useState(false);
  const [massehandlingFeil, setMassehandlingFeil] = useState<string | null>(null);

  // Sletting — KUN 'Avvist'-rader kan hardslettes her (til forskjell fra navnekandidater, der ALLE
  // statuser kan hardslettes): en 'Godkjent' rad har opprettet en ekte tekst-tagg (koblet til en
  // navneform) som ikke kan fjernes i etterkant (TekstTaggTjeneste.SlettAsync nekter å fjerne en tagg
  // med RefId satt), og en 'Venter'-rad skal behandles (godkjennes/avvises), ikke bare forsvinne. Se
  // VirksomhetKandidatTjeneste.HardslettAlleAvvisteAsync for hele resonnementet. Antallet under hentes
  // derfor UAVHENGIG av statusFilter over (som styrer hovedtabellen) — «Slett alle avviste»-knappen skal
  // vise riktig antall selv når statusFilter='Venter'/'Godkjent'/'Alle' er valgt.
  const [avvisteKandidater, setAvvisteKandidater] = useState<VirksomhetKandidatDto[] | null>(null);
  const [sletterAlle, setSletterAlle] = useState(false);
  const [slettAlleFeil, setSlettAlleFeil] = useState<string | null>(null);

  const [sveipVirksomhetId, setSveipVirksomhetId] = useState('');
  const [sveiper, setSveiper] = useState(false);
  const [sveipFeil, setSveipFeil] = useState<string | null>(null);
  const [sveipResultat, setSveipResultat] = useState<{ funnet: number; nye: number } | null>(null);

  // [ENDRET, kandidatside-runden, 2026-09-09, issue #216] Delt hook — de tre kandidatsidene hadde
  // hver sin identiske kopi av sorteringstilstanden og de to hjelpefunksjonene.
  const sortering = useSortering<Sorteringskolonne>('opprettet', false);

  // [ENDRET, issue #256, 2026-09-10] Var tidligere `api.hentRettskilder()` UTEN filter — hele det
  // synlige korpuset (5899 rader, 2,6 MB, 1,9 s målt live) bare for å fylle «Lov/forskrift»-
  // nedtrekket og slå opp titler i tabellen. Samme feilklasse som «Virksomhet»-dropdownen hadde
  // (se `virksomhetIderMedKandidater`-kommentaren under, 2026-08-30) — løst her på samme måte:
  // hent FØRST hvilke rettskilde-IDer som faktisk har en kandidat under gjeldende virksomhet-/
  // statusfilter (IKKE rettskildeFilter selv — ellers ville nedtrekket krympet til kun ÉN rettskilde
  // i det øyeblikket brukeren velger den), og hent titlene KUN for DEM.
  const [rettskildeIderMedKandidater, setRettskildeIderMedKandidater] = useState<Set<string> | null>(null);
  useEffect(() => {
    api
      .hentVirksomhetKandidater({ virksomhetId: virksomhetFilter || undefined, status: statusFilter })
      .then((liste) => setRettskildeIderMedKandidater(new Set(liste.map((k) => k.rettskildeId))))
      .catch(() => setRettskildeIderMedKandidater(null));
  }, [virksomhetFilter, statusFilter]);

  useEffect(() => {
    if (rettskildeIderMedKandidater === null) return; // ikke lastet ennå — vent, ikke hent alt som fallback.
    if (rettskildeIderMedKandidater.size === 0) {
      setRettskilder([]);
      return;
    }
    api.hentRettskilderForIder([...rettskildeIderMedKandidater])
      .then(setRettskilder)
      .catch(() => setRettskilder([]));
  }, [rettskildeIderMedKandidater]);

  // Node-tekst per rettskilde (2026-08-22, samme lazy-per-rettskilde-mønster som TjenesteDetalj/
  // HandlingDetalj) — brukt til å vise selve NAVNEFORM-TEKSTEN treffet fant (StartOffset/EndOffset
  // skåret ut av nodens Tekst), ikke bare den rå node-eId-en. Uten dette er det ikke synlig i lista
  // OM det var "Advokattilsynet" eller en annen navneform (f.eks. "Tilsynsrådet for advokatvirksomhet")
  // som ga treffet.

  function visNavneformFunnet(k: VirksomhetKandidatDto): string | null {
    const node = nodeEtiketter.node(k.rettskildeId, k.nodeEid);
    if (!node?.tekst) return null;
    return node.tekst.slice(k.startOffset, k.endOffset);
  }

  // [Ny, 2026-09-02, issue #115] Menneskelesbar "Node"-visning — "§ {nummer} — {overskrift}" i stedet
  // for rå nodeEid, gjenbruker de allerede hentede nodene (samme node som
  // `visNavneformFunnet` slår opp). Kilden vises allerede i egen "Lov/forskrift"-kolonne rett ved
  // siden av, så vi bygger teksten direkte fra noden i stedet for å gå via `eidVisningstekst` (som
  // ville dratt inn kortnavnet en gang til). Faller tilbake til rå eId når noden ikke er funnet ennå.

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
        valg.nullstill(); // Nytt filter/ny liste — forrige utvalg gjelder ikke lenger.
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

  function lastAvvisteKandidater() {
    api
      .hentVirksomhetKandidater({ virksomhetId: virksomhetFilter || undefined, rettskildeId: rettskildeFilter || undefined, status: 'Avvist' })
      .then(setAvvisteKandidater)
      .catch(() => setAvvisteKandidater(null));
  }

  useEffect(lastAvvisteKandidater, [virksomhetFilter, rettskildeFilter]);

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

  const rettskildeOppslag = useRettskildeoppslag(rettskilder);

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


  // Merk: "alle viste" betyr alle på GJELDENDE SIDE, ikke hele det filtrerte treffsettet — samme
  // avgrensning som checkbox-etiketten "Velg alle viste" allerede antydet før paginering fantes,
  // nå bare eksplisitt riktig i og med at "viste" er per side.

  async function massehandling(handling: 'godkjenn' | 'avvis') {
    if (valg.antall === 0) return;
    setMassehandlingKjorer(true);
    setMassehandlingFeil(null);
    try {
      const request = { ider: [...valg.valgte] };
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

  // Sletting — kun tilgjengelig for 'Avvist'-rader, se state-kommentaren over for hvorfor.
  async function slettEnkelt(id: string) {
    if (!window.confirm('Slette denne kandidaten permanent? Dette kan ikke angres.')) return;
    try {
      await api.hardslettVirksomhetKandidat(id);
      lastKandidater();
      lastAvvisteKandidater();
    } catch (err) {
      setFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved sletting av kandidat.');
    }
  }

  // «Slett valgte» — samme sted/mønster som Godkjenn/Avvis valgte (massehandling-raden), men kun
  // 'Avvist'-rader kan faktisk slettes (se state-kommentaren over). Hopper stille over valgte rader
  // som ikke er avvist i stedet for å feile hele handlingen — bekrefter tydelig i dialogen hvor mange
  // som faktisk slettes vs. hoppes over.
  async function slettValgte() {
    if (valg.antall === 0) return;
    const avvisteValgte = (kandidater ?? []).filter((k) => valg.erValgt(k.id) && k.status === 'Avvist').map((k) => k.id);
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
      for (const id of avvisteValgte) {
        await api.hardslettVirksomhetKandidat(id);
      }
      valg.nullstill();
      lastKandidater();
      lastAvvisteKandidater();
    } catch (err) {
      setMassehandlingFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved sletting av valgte kandidater.');
    } finally {
      setMassehandlingKjorer(false);
    }
  }

  async function slettAlle() {
    const antall = avvisteKandidater?.length ?? 0;
    if (antall === 0) return;
    if (!window.confirm(`Slette ${antall} avvist(e) kandidat(er) permanent? Dette kan ikke angres.`)) return;

    setSletterAlle(true);
    setSlettAlleFeil(null);
    try {
      await api.hardslettAlleAvvisteVirksomhetKandidater({
        virksomhetId: virksomhetFilter || undefined,
        rettskildeId: rettskildeFilter || undefined,
      });
      lastKandidater();
      lastAvvisteKandidater();
    } catch (err) {
      setSlettAlleFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved massesletting.');
    } finally {
      setSletterAlle(false);
    }
  }


  const viste = useMemo(() => {
    if (!kandidater) return null;
    const tekst = fritekstSok.trim().toLowerCase();
    const filtrert = tekst
      ? kandidater.filter(
          (k) => visEier(k.virksomhetId).toLowerCase().includes(tekst)
            || rettskildeOppslag.tittel(k.rettskildeId).toLowerCase().includes(tekst),
        )
      : kandidater;
    const sortnokkel = (k: VirksomhetKandidatDto) =>
      sortering.kolonne === 'virksomhet'
        ? visEier(k.virksomhetId)
        : sortering.kolonne === 'rettskilde'
          ? rettskildeOppslag.tittel(k.rettskildeId)
          : sortering.kolonne === 'status'
            ? k.status
            : k.opprettetTidspunkt;
    return [...filtrert].sort((a, b) => {
      const cmp = sortnokkel(a).localeCompare(sortnokkel(b), 'nb');
      return sortering.stigende ? cmp : -cmp;
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [kandidater, fritekstSok, sortering.kolonne, sortering.stigende, visEier, rettskildeOppslag.perId]);

  const paginering = usePaginering(viste ?? []);

  // Gruppert visning (se `Gruppering`-kommentaren over) — bygget OVENPÅ det allerede filtrerte og
  // sorterte `viste`-settet, samme "gruppene sorteres etter antall, flest først"-regel som
  // NavnekandidaterListe.tsx sin tilsvarende `grupper`.
  const grupper = useMemo<Kandidatgruppe[] | null>(() => {
    if (!viste || gruppering === 'ingen') return null;
    const perNokkel = new Map<string, VirksomhetKandidatDto[]>();
    for (const k of viste) {
      const nokkel = gruppering === 'virksomhet' ? k.virksomhetId : k.rettskildeId;
      const eksisterende = perNokkel.get(nokkel);
      if (eksisterende) eksisterende.push(k); else perNokkel.set(nokkel, [k]);
    }
    return [...perNokkel.entries()]
      .map(([nokkel, rader]): Kandidatgruppe => ({
        nokkel,
        visningsnavn: gruppering === 'virksomhet' ? visEier(nokkel) : rettskildeOppslag.tittel(nokkel),
        rader,
      }))
      .sort((a, b) => b.rader.length - a.rader.length || a.visningsnavn.localeCompare(b.visningsnavn, 'nb'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [viste, gruppering, visEier, rettskildeOppslag.perId]);

  // «Velg alle»-toppboksen virker på gjeldende SIDE ved flat visning (uendret), men HELE det
  // filtrerte settet ved gruppering — der finnes ingen paginering å avgrense til, og kollapsede
  // grupper skal fortsatt kunne velges i sin helhet (samme regel som NavnekandidaterListe.tsx).
  const raderForMasterSjekkboks = gruppering === 'ingen' ? paginering.visteRader : (viste ?? []);

  // Radene FAKTISK synlig akkurat nå — gjeldende side ved flat visning, men KUN radene i ÅPNE
  // grupper ved gruppert visning (kollapsede grupper er ikke rendret, og skal derfor ikke trigge
  // node-henting for sine rettskilder).
  const synligeRader = useMemo(() => {
    if (gruppering === 'ingen') return paginering.visteRader;
    if (!grupper) return [];
    return grupper.filter((g) => gruppeApne.has(g.nokkel)).flatMap((g) => g.rader);
  }, [gruppering, paginering.visteRader, grupper, gruppeApne]);

  function apneAlleGrupper() { if (grupper) setGruppeApne(new Set(grupper.map((g) => g.nokkel))); }
  function lukkAlleGrupper() { setGruppeApne(new Set()); }

  // Hent node-tekst KUN for rettskildene faktisk SYNLIG akkurat nå (2026-08-22, utvidet til å dekke
  // gruppert visning i issue #265) — ikke for hele det filtrerte treffsettet. Med case-insensitiv
  // sveip (samme dag) kan én virksomhet ha kandidater spredt over titalls-hundretalls ULIKE
  // rettskilder samtidig (f.eks. 373 treff for "fylkeskommune" på tvers av store deler av lovverket)
  // — å hente noder for ALLE av dem samtidig var en reell, observert render-treg/timeout-regresjon.
  // Paginering (flat visning) / åpne grupper (gruppert visning) gjør denne mengden avgrenset og
  // forutsigbar (maks ett `hentNoder`-kall per DISTINKT rettskilde blant de synlige radene).
  // [ENDRET, kandidatside-runden, 2026-09-09, issue #216] Delt hook: sen node-henting per
  // rettskilde for de VISTE radene, og etiketten via paragrafEtikett. Den lokale kopien her
  // bygde «§ {node.nummer}», som for et LEDD ga leddnummeret — «§ 6» for § 36 sjette ledd.
  const nodeEtiketter = useNodeEtiketter(synligeRader);

  return (
    <>
      <Heading level={1} data-size="lg" style={{ marginBottom: '0.2rem' }}>
        Virksomhetskandidater
      </Heading>
      <Paragraph style={{ marginBottom: '1rem', color: 'var(--ds-color-neutral-text-subtle)' }}>
        Forekomster av virksomheters navneformer funnet ved tekstsøk i rettskilder — godkjenn for å
        opprette en faktisk tekst-tagg, avvis for å fjerne fra køen.
      </Paragraph>
      <KandidatflytForklaring aktiv="virksomhet" />

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
          <Button data-size="sm" onClick={kjorSveip} disabled={!sveipVirksomhetId || sveiper}>
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
              <Select.Option key={r.id} value={r.id}>{r.tittel}</Select.Option>
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
        <Textfield
          data-size="sm"
          label="Søk"
          placeholder="Virksomhet eller lov/forskrift"
          value={fritekstSok}
          onChange={(e) => setFritekstSok(e.target.value)}
          style={{ maxWidth: '18rem' }}
        />
      </div>

      <div style={{ display: 'flex', gap: '0.75rem', alignItems: 'center', flexWrap: 'wrap', marginBottom: '1rem' }}>
        <Label style={{ margin: 0 }}>Gruppering</Label>
        <ToggleGroup
          value={gruppering}
          onChange={(v) => setGruppering(v as Gruppering)}
          data-size="sm"
          data-toggle-group="Gruppering"
        >
          <ToggleGroup.Item value="ingen">Ingen (flat liste)</ToggleGroup.Item>
          <ToggleGroup.Item value="virksomhet">Virksomhet</ToggleGroup.Item>
          <ToggleGroup.Item value="rettskilde">Rettskilde</ToggleGroup.Item>
        </ToggleGroup>
        {gruppering !== 'ingen' && (
          <>
            <Button data-size="sm" variant="tertiary" onClick={apneAlleGrupper}>Åpne alle</Button>
            <Button data-size="sm" variant="tertiary" onClick={lukkAlleGrupper}>Lukk alle</Button>
          </>
        )}
      </div>

      {/* [ENDRET, kandidatside-runden, 2026-09-09, issue #216] Delt komponent — samme rad på alle tre
          kandidatsidene, inkludert begrepskandidatsiden som tidligere ikke hadde massehandling. */}
      <Massehandlingsrad
        antallValgte={valg.antall}
        kjorer={massehandlingKjorer}
        feil={massehandlingFeil}
        merknad={rettskildeFilter ? ' — filtrert til én lov/forskrift' : undefined}
        onGodkjenn={() => massehandling('godkjenn')}
        onAvvis={() => massehandling('avvis')}
        onSlett={slettValgte}
      />

      <Card style={{ padding: '1rem', marginBottom: '1rem' }}>
        <Heading level={2} data-size="xs" style={{ marginBottom: '0.5rem' }}>
          Slett avviste kandidater
        </Heading>
        <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.75rem' }}>
          «Slett valgte» over sletter et PRESIST utvalg (kun avviste blant de markerte radene). Dette
          kortet er for STOR, filterbasert sletting: ekte, irreversibel sletting av ALLE 'Avvist'-
          kandidater innenfor virksomhet-/rettskildefilteret — nyttig for å tømme køen før et nytt sveip
          (den posisjonsbaserte idempotensen hindrer ellers et nytt sveip i noensinne å re-evaluere en
          avvist posisjon på nytt). Respekterer IKKE statusfilteret over — kun 'Avvist'-rader kan slettes
          uansett metode: en 'Venter'-rad skal behandles (godkjennes/avvises), og en 'Godkjent'-rad har
          opprettet en ekte tekst-tagg som ikke kan fjernes i etterkant.
        </Paragraph>
        <Button
          data-size="sm"
          data-color="danger"
          onClick={slettAlle}
          disabled={!avvisteKandidater || avvisteKandidater.length === 0 || sletterAlle}
        >
          {sletterAlle ? 'Sletter …' : `Slett alle avviste kandidater (${avvisteKandidater?.length ?? 0})`}
        </Button>
        {slettAlleFeil && <div className="feilmelding" style={{ marginTop: '0.5rem' }}>{slettAlleFeil}</div>}
      </Card>

      {feil && <div className="feilmelding" style={{ marginBottom: '1rem' }}>{feil}</div>}

      {/* Card ALLTID rendret (docs/09 §14 / docs/30 §3.1 pkt. 5, samme mønster som
          Begrepskandidater.tsx allerede bruker) — tom-/laste-tilstand er en Paragraph INNI kortet,
          aldri et betinget-rendret kort utenfor. */}
      <Card style={{ padding: 0, overflow: 'hidden' }}>
        {laster && !kandidater ? (
          <Paragraph style={{ padding: '1rem', margin: 0 }}>Laster …</Paragraph>
        ) : viste && viste.length === 0 ? (
          <Paragraph style={{ padding: '1rem', margin: 0 }}>Ingen kandidater matcher filteret.</Paragraph>
        ) : viste && viste.length > 0 ? (
          <div style={{ overflowX: 'auto' }}>
            <Table data-density="compact">
              <Table.Head>
                <Table.Row>
                  <Table.HeaderCell>
                    <Checkbox
                      aria-label="Velg alle viste"
                      checked={raderForMasterSjekkboks.length > 0 && raderForMasterSjekkboks.every((k) => valg.erValgt(k.id))}
                      onChange={(e) => valg.velgAlleViste(raderForMasterSjekkboks, e.target.checked)}
                    />
                  </Table.HeaderCell>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => sortering.bytt('virksomhet')}>
                      Virksomhet{sortering.indikator('virksomhet')}
                    </button>
                  </Table.HeaderCell>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => sortering.bytt('rettskilde')}>
                      Lov/forskrift{sortering.indikator('rettskilde')}
                    </button>
                  </Table.HeaderCell>
                  <Table.HeaderCell>Node</Table.HeaderCell>
                  <Table.HeaderCell>Navneform funnet</Table.HeaderCell>
                  <Table.HeaderCell>
                    <button type="button" className="tabell-sorter-knapp" onClick={() => sortering.bytt('status')}>
                      Status{sortering.indikator('status')}
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
                              checked={g.rader.every((k) => valg.erValgt(k.id))}
                              onChange={(e) => valg.vekslGruppe(g.rader, e.target.checked)}
                            />
                          </Table.Cell>
                          <Table.Cell colSpan={6}>
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
        ) : null}
      </Card>

      {gruppering === 'ingen' && viste && viste.length > 0 && <Pagineringskontroll {...paginering} />}
    </>
  );

  function renderKandidatRad(k: VirksomhetKandidatDto) {
    return (
      <Table.Row key={k.id}>
        <Table.Cell>
          <Checkbox
            aria-label={`Velg kandidat ${k.id}`}
            checked={valg.erValgt(k.id)}
            onChange={(e) => valg.veksl(k.id, e.target.checked)}
          />
        </Table.Cell>
        <Table.Cell>{visEier(k.virksomhetId)}</Table.Cell>
        <Table.Cell>{rettskildeOppslag.tittel(k.rettskildeId)}</Table.Cell>
        <Table.Cell style={{ fontSize: 'var(--ds-font-size-1)' }}>
          {/* [Rettet, 2026-09-02, issue #115] Viser nå "§ nummer — overskrift" (visNodeTekst)
              i stedet for rå nodeEid — monospace-stilen passet den rå eId-koden, ikke prosa. */}
          {/* Slik at bruker kan lese noden i sin fulle sammenheng FØR godkjenning
              (Johanns tilbakemelding 2026-08-22) — åpner rettskildevisningen på nøyaktig
              denne noden. [Rettet, 2026-08-30] Bruker rettskildeLenkeForId (rettskildeId
              allerede kjent på raden) i stedet for rettskildeLenke sin ELI-prefiks-
              gjetting — den fant ingen treff for kap-/rom-/punkt-nummererte noder
              (LovdataIdentifikatorer.KapittelEid er bevisst ELI-uavhengig). */}
          <Link asChild>
            <RouterLink to={rettskildeLenkeForId(k.rettskildeId, k.nodeEid)} target="_blank">{nodeEtiketter.etikett(k.rettskildeId, k.nodeEid)} ↗</RouterLink>
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
          <KandidatStatusTag status={k.status} />
        </Table.Cell>
        <Table.Cell>
          <div style={{ display: 'flex', gap: '0.4rem', flexWrap: 'wrap', alignItems: 'center' }}>
            {k.status === 'Venter' ? (
              <>
                <Button data-size="sm" onClick={() => enkelthandling(k.id, 'godkjenn')}>Godkjenn</Button>
                <Button data-size="sm" variant="tertiary" onClick={() => enkelthandling(k.id, 'avvis')}>Avvis</Button>
              </>
            ) : (
              <span style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
                {k.behandletAv ? `Behandlet av ${k.behandletAv}` : '—'}
              </span>
            )}
            {/* KUN 'Avvist' — en 'Godkjent' rad har en ekte tekst-tagg som ikke kan fjernes i
                etterkant, og en 'Venter'-rad skal behandles, ikke bare forsvinne. Se
                VirksomhetKandidatTjeneste.HardslettAvvistAsync/HardslettAlleAvvisteAsync. */}
            {k.status === 'Avvist' && (
              <Button data-size="sm" variant="tertiary" data-color="danger" onClick={() => slettEnkelt(k.id)}>
                Slett
              </Button>
            )}
          </div>
        </Table.Cell>
      </Table.Row>
    );
  }
}
