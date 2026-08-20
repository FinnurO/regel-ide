import { useEffect, useMemo, useState, type FormEvent } from 'react';
import { Link as RouterLink, useParams } from 'react-router';
import { Button, Field, Heading, Label, Link, Paragraph, Select, Tag, Textarea, Textfield } from '@digdir/designsystemet-react';
import { ApiError, api } from '../api/client';
import { eidVisningstekst, rettskildeLenke } from '../api/eidLenker';
import type {
  HandlingDto, HendelseDto, RegelnodeDto, RettskildeNodeDto, RettskildeSammendrag, TjenesteavhengighetDto, TjenesteDto,
  TjenesteInnholdInput, TjenesteRegelverksreferanseDto, TjenesteTverrTenantTreffDto,
} from '../api/types';
import { GYLDIGE_HANDLINGSTYPER, GYLDIGE_RETTIGHETSTYPER, GYLDIGE_UTFORT_AV } from '../api/types';

const STATUSER = ['utkast', 'under_revisjon', 'validert', 'publisert', 'tilbaketrukket', 'arkivert'];

/** 'for'/'avhengig_av'/'input_til' er de generelle relasjonene; de tre første har en presis betydning (docs/03-domenemodell.md §1.5). */
const TJENESTEAVHENGIGHET_REL = [
  { id: 'forutsetning_for', label: 'er forutsetning for' },
  { id: 'gir_mulighet_til', label: 'gir mulighet til' },
  { id: 'utlost_av', label: 'utløses av en hendelse' },
  { id: 'for', label: 'kommer før (generelt)' },
  { id: 'avhengig_av', label: 'er avhengig av (generelt)' },
  { id: 'input_til', label: 'er input til (generelt)' },
];

export default function TjenesteDetalj() {
  const { id } = useParams<{ id: string }>();
  const [tjeneste, setTjeneste] = useState<TjenesteDto | null>(null);
  const [referanser, setReferanser] = useState<TjenesteRegelverksreferanseDto[] | null>(null);
  const [rettskilder, setRettskilder] = useState<RettskildeSammendrag[]>([]);
  const [feil, setFeil] = useState<string | null>(null);

  const [tittel, setTittel] = useState('');
  const [beskrivelse, setBeskrivelse] = useState('');
  const [kompetentMyndighet, setKompetentMyndighet] = useState('');
  const [tjenestetype, setTjenestetype] = useState('');
  const [malgruppe, setMalgruppe] = useState('');
  const [kanaler, setKanaler] = useState('');
  const [kostnad, setKostnad] = useState('');
  const [behandlingstid, setBehandlingstid] = useState('');
  const [kontaktpunkt, setKontaktpunkt] = useState('');
  const [konsekvensVedBrudd, setKonsekvensVedBrudd] = useState('');
  const [sprak, setSprak] = useState('');
  const [livshendelser, setLivshendelser] = useState('');
  const [losKlassifisering, setLosKlassifisering] = useState('');
  const [tjenesteomrade, setTjenesteomrade] = useState('');
  const [type, setType] = useState('');
  const [formal, setFormal] = useState('');
  const [lagrer, setLagrer] = useState(false);
  const [lagreFeil, setLagreFeil] = useState<string | null>(null);
  const [statusEndres, setStatusEndres] = useState(false);

  // ---------- Innhold (2026-08-20, Tjenestedetalj-runde 2) — rettighetens rike, forfattede
  // innholdsseksjoner. Hvert listefelt redigeres som linjeseparert tekst (én rad per linje) —
  // samme prinsipp som Egenskaper-formens kommaseparerte lister, bare linjeskift siden innholdet
  // her typisk er hele setninger, ikke enkeltord. ----------
  const [visRedigerInnhold, setVisRedigerInnhold] = useState(false);
  const [innholdLagrer, setInnholdLagrer] = useState(false);
  const [innholdFeil, setInnholdFeil] = useState<string | null>(null);

  const [iTidspunktOgFrister, setITidspunktOgFrister] = useState('');
  const [iInnsenderHvemKanSende, setIInnsenderHvemKanSende] = useState('');
  const [iInnsenderInnlogging, setIInnsenderInnlogging] = useState('');
  const [iVedlegg, setIVedlegg] = useState('');
  const [iVedleggMerknad, setIVedleggMerknad] = useState('');
  const [iOpplysninger, setIOpplysninger] = useState('');
  const [iOpplysningerMerknad, setIOpplysningerMerknad] = useState('');
  const [iVeiledning, setIVeiledning] = useState('');
  const [iVeiledningMerknad, setIVeiledningMerknad] = useState('');
  const [iInnsendingKanal, setIInnsendingKanal] = useState('');
  const [iInnsendingEtterMottak, setIInnsendingEtterMottak] = useState('');
  const [iInnsendingMerknad, setIInnsendingMerknad] = useState('');
  const [iKontaktGenerelt, setIKontaktGenerelt] = useState('');
  const [iKontaktKommunenKanVeiledeOm, setIKontaktKommunenKanVeiledeOm] = useState('');
  const [iHviInnledning, setIHviInnledning] = useState('');
  const [iHviVarighet, setIHviVarighet] = useState('');
  const [iHviPlikter, setIHviPlikter] = useState('');
  const [iHviEndringerPlikt, setIHviEndringerPlikt] = useState('');
  const [iHviEndringerEksempler, setIHviEndringerEksempler] = useState('');
  const [iHviKontrollOgTilsyn, setIHviKontrollOgTilsyn] = useState('');
  const [iHviAvgrensningMerknad, setIHviAvgrensningMerknad] = useState('');
  const [iHviKravTilDrift, setIHviKravTilDrift] = useState('');
  const [iHviTommeavtaleOgKontroll, setIHviTommeavtaleOgKontroll] = useState('');
  const [iHviRapportering, setIHviRapportering] = useState('');

  const [nyReferanseRettskildeId, setNyReferanseRettskildeId] = useState('');
  const [nyReferanseEid, setNyReferanseEid] = useState('');
  const [leggerTilReferanse, setLeggerTilReferanse] = useState(false);
  const [referanseFeil, setReferanseFeil] = useState<string | null>(null);

  // Node-oppslag per rettskilde (punkt 5/7, avklaringsrunde 2026-08-13) — brukt BÅDE til lesbar
  // gruppert visning av eksisterende referanser (eidVisningstekst) og til paragraf-picker-en i
  // "Koble referanse"-formen under. Ett delt Map, hentet lazy/idempotent per rettskilde-id.
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

  const [hendelser, setHendelser] = useState<HendelseDto[] | null>(null);
  const [alleHendelser, setAlleHendelser] = useState<HendelseDto[]>([]);
  const [nyHendelseId, setNyHendelseId] = useState('');
  const [leggerTilHendelse, setLeggerTilHendelse] = useState(false);
  const [visNyHendelse, setVisNyHendelse] = useState(false);
  const [nyHendelseNavn, setNyHendelseNavn] = useState('');
  const [nyHendelseType, setNyHendelseType] = useState('virksomhetshendelse');
  const [hendelseFeil, setHendelseFeil] = useState<string | null>(null);

  const [handlinger, setHandlinger] = useState<HandlingDto[] | null>(null);
  const [nyHandlingNavn, setNyHandlingNavn] = useState('');
  const [nyHandlingType, setNyHandlingType] = useState<string>(GYLDIGE_HANDLINGSTYPER[0]);
  const [nyHandlingUtfortAv, setNyHandlingUtfortAv] = useState('');
  const [leggerTilHandling, setLeggerTilHandling] = useState(false);
  const [handlingFeil, setHandlingFeil] = useState<string | null>(null);

  const [avhengigheter, setAvhengigheter] = useState<TjenesteavhengighetDto[] | null>(null);
  const [alleTjenester, setAlleTjenester] = useState<TjenesteDto[]>([]);
  const [nyAvhengighetTilId, setNyAvhengighetTilId] = useState('');
  const [nyAvhengighetRel, setNyAvhengighetRel] = useState('forutsetning_for');
  const [nyAvhengighetHendelseId, setNyAvhengighetHendelseId] = useState('');
  const [nyAvhengighetBeskrivelse, setNyAvhengighetBeskrivelse] = useState('');
  const [leggerTilAvhengighet, setLeggerTilAvhengighet] = useState(false);
  const [avhengighetFeil, setAvhengighetFeil] = useState<string | null>(null);

  // Ekstern referanse (2026-08-19) — «avansert/manuell»-fallback, samme mønster som
  // "Avansert / manuell eId" ved siden av paragraf-picker-en: den manuelle trioen er et alternativt mål,
  // ikke en samtidig kombinasjon — å velge en tjeneste (egen ELLER cross-tenant) tømmer den, og omvendt.
  const [nyAvhengighetTilOrgnr, setNyAvhengighetTilOrgnr] = useState('');
  const [nyAvhengighetTilNavn, setNyAvhengighetTilNavn] = useState('');
  const [nyAvhengighetTilUrl, setNyAvhengighetTilUrl] = useState('');
  const [tverrTenantSok, setTverrTenantSok] = useState('');
  const [tverrTenantTreff, setTverrTenantTreff] = useState<TjenesteTverrTenantTreffDto[]>([]);
  const [tverrTenantSokerLaster, setTverrTenantSokerLaster] = useState(false);
  const [valgtTverrTenantTreff, setValgtTverrTenantTreff] = useState<TjenesteTverrTenantTreffDto | null>(null);

  function velgTilTjeneste(tjenesteId: string, treff: TjenesteTverrTenantTreffDto | null = null) {
    setNyAvhengighetTilId(tjenesteId);
    setValgtTverrTenantTreff(treff);
    setNyAvhengighetTilOrgnr('');
    setNyAvhengighetTilNavn('');
    setNyAvhengighetTilUrl('');
  }

  function endreEkstern(felt: 'orgnr' | 'navn' | 'url', verdi: string) {
    if (felt === 'orgnr') setNyAvhengighetTilOrgnr(verdi);
    else if (felt === 'navn') setNyAvhengighetTilNavn(verdi);
    else setNyAvhengighetTilUrl(verdi);
    if (felt !== 'url') {
      setNyAvhengighetTilId('');
      setValgtTverrTenantTreff(null);
    }
  }

  useEffect(() => {
    if (!tverrTenantSok.trim()) { setTverrTenantTreff([]); return; }
    setTverrTenantSokerLaster(true);
    const tidsavbrudd = setTimeout(() => {
      api.sokTjenesterTverrTenant(tverrTenantSok.trim())
        .then(setTverrTenantTreff)
        .catch(() => setTverrTenantTreff([]))
        .finally(() => setTverrTenantSokerLaster(false));
    }, 300);
    return () => clearTimeout(tidsavbrudd);
  }, [tverrTenantSok]);

  const [rotnode, setRotnode] = useState<RegelnodeDto | null>(null);
  const [regelnoder, setRegelnoder] = useState<RegelnodeDto[]>([]);
  const [visOpprettRotnode, setVisOpprettRotnode] = useState(false);
  const [nyRotnodeTittel, setNyRotnodeTittel] = useState('');
  const [visByttRotnode, setVisByttRotnode] = useState(false);
  const [valgtRotnodeId, setValgtRotnodeId] = useState('');
  const [rotnodeEndres, setRotnodeEndres] = useState(false);
  const [rotnodeFeil, setRotnodeFeil] = useState<string | null>(null);

  /** Kanaler/språk redigeres som kommaseparert tekst i denne runden — ingen multi-select-UI bygget ennå. */
  function tilListe(kommaseparert: string): string[] {
    return kommaseparert.split(',').map((s) => s.trim()).filter(Boolean);
  }

  /** Innhold-listefelt (setninger, ikke enkeltord) redigeres linjeseparert — én rad per linje. */
  function tilListeNL(tekst: string): string[] {
    return tekst.split('\n').map((s) => s.trim()).filter(Boolean);
  }
  function fraListeNL(liste: string[]): string {
    return liste.join('\n');
  }

  function fyllSkjemaFra(t: TjenesteDto) {
    setTittel(t.tittel);
    setBeskrivelse(t.beskrivelse ?? '');
    setKompetentMyndighet(t.kompetentMyndighet ?? '');
    setTjenestetype(t.tjenestetype ?? '');
    setMalgruppe(t.malgruppe.join(', '));
    setKanaler(t.kanaler.join(', '));
    setKostnad(t.kostnad ?? '');
    setBehandlingstid(t.behandlingstid ?? '');
    setKontaktpunkt(t.kontaktpunkt ?? '');
    setKonsekvensVedBrudd(t.konsekvensVedBrudd ?? '');
    setSprak(t.sprak.join(', '));
    setLivshendelser(t.livshendelser.join(', '));
    setLosKlassifisering(t.losKlassifisering ?? '');
    setTjenesteomrade(t.tjenesteomrade ?? '');
    setType(t.type ?? '');
    setFormal(t.formal ?? '');

    const i = t.innhold;
    setITidspunktOgFrister(i?.tidspunktOgFrister ?? '');
    setIInnsenderHvemKanSende(fraListeNL(i?.innsenderOgTilgang?.hvemKanSende ?? []));
    setIInnsenderInnlogging(i?.innsenderOgTilgang?.innlogging ?? '');
    setIVedlegg(fraListeNL(i?.vedlegg ?? []));
    setIVedleggMerknad(i?.vedleggMerknad ?? '');
    setIOpplysninger(fraListeNL(i?.opplysningerSomSkalSendesInn ?? []));
    setIOpplysningerMerknad(i?.opplysningerMerknad ?? '');
    setIVeiledning(fraListeNL(i?.veiledningOgUtfylling ?? []));
    setIVeiledningMerknad(i?.veiledningMerknad ?? '');
    setIInnsendingKanal(i?.innsendingOgOppfolging?.kanal ?? '');
    setIInnsendingEtterMottak(fraListeNL(i?.innsendingOgOppfolging?.etterMottak ?? []));
    setIInnsendingMerknad(i?.innsendingOgOppfolging?.merknad ?? '');
    setIKontaktGenerelt(i?.kontaktOgHjelp?.generelt ?? '');
    setIKontaktKommunenKanVeiledeOm(fraListeNL(i?.kontaktOgHjelp?.kommunenKanVeiledeOm ?? []));
    setIHviInnledning(i?.hvaRettighetenInnebarer?.innledning ?? '');
    setIHviVarighet(i?.hvaRettighetenInnebarer?.varighet ?? '');
    setIHviPlikter(fraListeNL(i?.hvaRettighetenInnebarer?.plikter ?? []));
    setIHviEndringerPlikt(i?.hvaRettighetenInnebarer?.endringerIVirksomheten?.plikt ?? '');
    setIHviEndringerEksempler(fraListeNL(i?.hvaRettighetenInnebarer?.endringerIVirksomheten?.eksempler ?? []));
    setIHviKontrollOgTilsyn(i?.hvaRettighetenInnebarer?.kontrollOgTilsyn ?? '');
    setIHviAvgrensningMerknad(i?.hvaRettighetenInnebarer?.avgrensningMerknad ?? '');
    setIHviKravTilDrift(i?.hvaRettighetenInnebarer?.kravTilDrift ?? '');
    setIHviTommeavtaleOgKontroll(i?.hvaRettighetenInnebarer?.tommeavtaleOgKontroll ?? '');
    setIHviRapportering(i?.hvaRettighetenInnebarer?.rapportering ?? '');
  }

  /** Bygger TjenesteInnholdInput fra det flate skjema-state-et over. */
  function byggInnhold(): TjenesteInnholdInput {
    const harEndringer = iHviEndringerPlikt.trim() || iHviEndringerEksempler.trim();
    const harInnsender = iInnsenderHvemKanSende.trim() || iInnsenderInnlogging.trim();
    const harInnsending = iInnsendingKanal.trim() || iInnsendingEtterMottak.trim() || iInnsendingMerknad.trim();
    const harKontakt = iKontaktGenerelt.trim() || iKontaktKommunenKanVeiledeOm.trim();
    return {
      tidspunktOgFrister: iTidspunktOgFrister.trim() || null,
      innsenderOgTilgang: harInnsender
        ? { hvemKanSende: tilListeNL(iInnsenderHvemKanSende), innlogging: iInnsenderInnlogging.trim() || null } : null,
      vedlegg: tilListeNL(iVedlegg),
      vedleggMerknad: iVedleggMerknad.trim() || null,
      opplysningerSomSkalSendesInn: tilListeNL(iOpplysninger),
      opplysningerMerknad: iOpplysningerMerknad.trim() || null,
      veiledningOgUtfylling: tilListeNL(iVeiledning),
      veiledningMerknad: iVeiledningMerknad.trim() || null,
      innsendingOgOppfolging: harInnsending
        ? { kanal: iInnsendingKanal.trim() || null, etterMottak: tilListeNL(iInnsendingEtterMottak), merknad: iInnsendingMerknad.trim() || null }
        : null,
      kontaktOgHjelp: harKontakt
        ? { generelt: iKontaktGenerelt.trim() || null, kommunenKanVeiledeOm: tilListeNL(iKontaktKommunenKanVeiledeOm) } : null,
      hvaRettighetenInnebarer: {
        innledning: iHviInnledning.trim() || null,
        varighet: iHviVarighet.trim() || null,
        plikter: tilListeNL(iHviPlikter),
        endringerIVirksomheten: harEndringer
          ? { plikt: iHviEndringerPlikt.trim() || null, eksempler: tilListeNL(iHviEndringerEksempler) } : null,
        kontrollOgTilsyn: iHviKontrollOgTilsyn.trim() || null,
        avgrensningMerknad: iHviAvgrensningMerknad.trim() || null,
        kravTilDrift: iHviKravTilDrift.trim() || null,
        tommeavtaleOgKontroll: iHviTommeavtaleOgKontroll.trim() || null,
        rapportering: iHviRapportering.trim() || null,
      },
    };
  }

  useEffect(() => {
    if (!id) return;
    api.hentTjeneste(id).then((t) => { setTjeneste(t); fyllSkjemaFra(t); })
      .catch((e) => setFeil(e instanceof ApiError ? e.message : 'Ukjent feil ved henting av tjeneste.'));
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

  // Punkt 5 — hent nodene til hver rettskilde faktisk referert i lista, slik at eidVisningstekst kan
  // vise "{kortnavn} § {nummer} — {overskrift}" i stedet for rå eId.
  useEffect(() => {
    if (!referanser) return;
    for (const rettskildeId of new Set(referanser.map((r) => r.tilRettskildeId))) {
      sikreNoderFor(rettskildeId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [referanser]);

  // Punkt 7 — paragraf-picker-en i "Koble referanse" trenger nodene til den VALGTE rettskilden.
  useEffect(() => {
    if (nyReferanseRettskildeId) sikreNoderFor(nyReferanseRettskildeId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [nyReferanseRettskildeId]);

  async function opprettRotnode(e: FormEvent) {
    e.preventDefault();
    if (!id || !nyRotnodeTittel.trim()) return;
    setRotnodeFeil(null);
    setRotnodeEndres(true);
    try {
      const nyRegelnode = await api.opprettRegelnode({
        tittel: nyRotnodeTittel.trim(), beskrivelse: null, generiskMal: null, barnOperator: 'OG',
        utdataNavn: 'Vedtak', utdataType: 'vedtak', erRotnode: true, juridiskGrunnlag: null,
        innvilgelseTekst: null, avslagTekst: null,
      });
      const oppdatert = await api.settTjenesteRotnode(id, { regelnodeId: nyRegelnode.id });
      setTjeneste(oppdatert);
      setVisOpprettRotnode(false);
      setNyRotnodeTittel('');
    } catch (err) {
      setRotnodeFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av rotnode.');
    } finally {
      setRotnodeEndres(false);
    }
  }

  async function byttRotnode(e: FormEvent) {
    e.preventDefault();
    if (!id || !valgtRotnodeId) return;
    setRotnodeFeil(null);
    setRotnodeEndres(true);
    try {
      const oppdatert = await api.settTjenesteRotnode(id, { regelnodeId: valgtRotnodeId });
      setTjeneste(oppdatert);
      setVisByttRotnode(false);
      setValgtRotnodeId('');
    } catch (err) {
      setRotnodeFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved bytte av rotnode.');
    } finally {
      setRotnodeEndres(false);
    }
  }

  async function fjernRotnode() {
    if (!id) return;
    setRotnodeFeil(null);
    setRotnodeEndres(true);
    try {
      const oppdatert = await api.fjernTjenesteRotnode(id);
      setTjeneste(oppdatert);
    } catch (err) {
      setRotnodeFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved fjerning av rotnode.');
    } finally {
      setRotnodeEndres(false);
    }
  }

  async function lagre(e: FormEvent) {
    e.preventDefault();
    if (!id || !tjeneste) return;
    setLagreFeil(null);
    setLagrer(true);
    try {
      const oppdatert = await api.oppdaterTjeneste(id, {
        tittel: tittel.trim(), beskrivelse: beskrivelse.trim() || null,
        kompetentMyndighet: kompetentMyndighet.trim() || null, output: tjeneste.output,
        tjenestetype: tjenestetype.trim() || null, malgruppe: tilListe(malgruppe), kanaler: tilListe(kanaler),
        kostnad: kostnad.trim() || null, behandlingstid: behandlingstid.trim() || null, kontaktpunkt: kontaktpunkt.trim() || null,
        konsekvensVedBrudd: konsekvensVedBrudd.trim() || null, sprak: tilListe(sprak),
        livshendelser: tilListe(livshendelser), losKlassifisering: losKlassifisering.trim() || null,
        tjenesteomrade: tjenesteomrade.trim() || null,
        type: type || null, formal: formal.trim() || null, innhold: tjeneste.innhold,
      });
      setTjeneste(oppdatert);
    } catch (err) {
      setLagreFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring.');
    } finally {
      setLagrer(false);
    }
  }

  async function lagreInnhold(e: FormEvent) {
    e.preventDefault();
    if (!id || !tjeneste) return;
    setInnholdFeil(null);
    setInnholdLagrer(true);
    try {
      const oppdatert = await api.oppdaterTjeneste(id, {
        tittel: tjeneste.tittel, beskrivelse: tjeneste.beskrivelse, kompetentMyndighet: tjeneste.kompetentMyndighet,
        output: tjeneste.output, tjenestetype: tjeneste.tjenestetype, malgruppe: tjeneste.malgruppe,
        kanaler: tjeneste.kanaler, kostnad: tjeneste.kostnad, behandlingstid: tjeneste.behandlingstid,
        kontaktpunkt: tjeneste.kontaktpunkt, konsekvensVedBrudd: tjeneste.konsekvensVedBrudd, sprak: tjeneste.sprak,
        livshendelser: tjeneste.livshendelser, losKlassifisering: tjeneste.losKlassifisering,
        tjenesteomrade: tjeneste.tjenesteomrade, type: tjeneste.type, formal: tjeneste.formal,
        innhold: byggInnhold(),
      });
      setTjeneste(oppdatert);
      setVisRedigerInnhold(false);
    } catch (err) {
      setInnholdFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved lagring av innhold.');
    } finally {
      setInnholdLagrer(false);
    }
  }

  async function leggTilReferanse(e: FormEvent) {
    e.preventDefault();
    if (!id || !nyReferanseRettskildeId || !nyReferanseEid.trim()) return;
    setReferanseFeil(null);
    setLeggerTilReferanse(true);
    try {
      const ny = await api.kobleTjenesteRegelverksreferanse(id, {
        tilRettskildeId: nyReferanseRettskildeId, tilEid: nyReferanseEid.trim(),
      });
      setReferanser((forrige) => [...(forrige ?? []), ny]);
      setNyReferanseEid('');
    } catch (err) {
      setReferanseFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved kobling av referanse.');
    } finally {
      setLeggerTilReferanse(false);
    }
  }

  async function fjernReferanse(referanseId: string) {
    await api.fjernTjenesteRegelverksreferanse(referanseId);
    setReferanser((forrige) => (forrige ?? []).filter((r) => r.id !== referanseId));
  }

  // Punkt 5 — én gruppe per referert lov/forskrift, i stedet for én flat liste.
  const referanserGruppert = useMemo(() => {
    const kart = new Map<string, TjenesteRegelverksreferanseDto[]>();
    for (const r of referanser ?? []) {
      const liste = kart.get(r.tilRettskildeId) ?? [];
      liste.push(r);
      kart.set(r.tilRettskildeId, liste);
    }
    return [...kart.entries()];
  }, [referanser]);

  // Punkt 7 — kun blad-noder med en faktisk paragraf/nummer er valgbare (ikke kapittel-noder uten
  // egen paragraf) — se eidLenker.ts/TjenesteDetalj-kommentaren i planen for begrunnelsen.
  // EKTE FUNN 2026-08-14: en Brukerveiledning har ÉN node ("side"-type) uten noe Nummer (den har per
  // definisjon ingen paragrafinndeling, §3.1) — det opprinnelige filteret (`&& n.nummer`) fjernet
  // derfor den eneste noden en Brukerveiledning har, og etterlot brukeren uten noe å velge og ingen
  // måte å oppdage riktig eId manuelt. En "side"-node ER en reell, hel referanse (hele siden) — la
  // den alltid være valgbar.
  const paragrafKandidater = (noderPerRettskilde.get(nyReferanseRettskildeId) ?? [])
    .filter((n) => n.nodeType === 'side' || (n.nodeType !== 'kapittel' && n.nummer));

  async function kobleHendelse(e: FormEvent) {
    e.preventDefault();
    if (!id || !nyHendelseId) return;
    setHendelseFeil(null);
    setLeggerTilHendelse(true);
    try {
      const oppdatert = await api.kobleTjenesteHendelse(id, { hendelseId: nyHendelseId });
      setHendelser(oppdatert);
      setNyHendelseId('');
    } catch (err) {
      setHendelseFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved kobling av hendelse.');
    } finally {
      setLeggerTilHendelse(false);
    }
  }

  async function opprettOgKobleHendelse(e: FormEvent) {
    e.preventDefault();
    if (!id || !nyHendelseNavn.trim()) return;
    setHendelseFeil(null);
    setLeggerTilHendelse(true);
    try {
      const hendelse = await api.opprettHendelse({ navn: nyHendelseNavn.trim(), type: nyHendelseType, beskrivelse: null });
      setAlleHendelser((forrige) => [...forrige, hendelse]);
      const oppdatert = await api.kobleTjenesteHendelse(id, { hendelseId: hendelse.id });
      setHendelser(oppdatert);
      setNyHendelseNavn('');
      setVisNyHendelse(false);
    } catch (err) {
      setHendelseFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av hendelse.');
    } finally {
      setLeggerTilHendelse(false);
    }
  }

  async function fjernHendelse(hendelseId: string) {
    if (!id) return;
    await api.fjernTjenesteHendelse(id, hendelseId);
    setHendelser((forrige) => (forrige ?? []).filter((h) => h.id !== hendelseId));
  }

  async function opprettHandling(e: FormEvent) {
    e.preventDefault();
    if (!id || !nyHandlingNavn.trim()) return;
    setHandlingFeil(null);
    setLeggerTilHandling(true);
    try {
      const ny = await api.opprettHandling(id, {
        navn: nyHandlingNavn.trim(), handlingstype: nyHandlingType, bruksomraade: null,
        utfortAv: nyHandlingUtfortAv || null, kanaler: null, behandlingstid: null, kostnad: null,
        vedlegg: null, veiledningstekst: null, arsaker: null, resultat: null, merknad: null,
      });
      setHandlinger((forrige) => [...(forrige ?? []), ny].sort((a, b) => a.navn.localeCompare(b.navn)));
      setNyHandlingNavn('');
      setNyHandlingUtfortAv('');
    } catch (err) {
      setHandlingFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av handling.');
    } finally {
      setLeggerTilHandling(false);
    }
  }

  async function leggTilAvhengighet(e: FormEvent) {
    e.preventDefault();
    const harEksternMal = !nyAvhengighetTilId && nyAvhengighetTilOrgnr.trim() && nyAvhengighetTilNavn.trim();
    if (!id || (!nyAvhengighetTilId && !harEksternMal)) return;
    setAvhengighetFeil(null);
    setLeggerTilAvhengighet(true);
    try {
      const oppdatert = await api.opprettTjenesteavhengighet(id, {
        tilTjenesteId: nyAvhengighetTilId || null,
        rel: nyAvhengighetRel,
        hendelseId: nyAvhengighetRel === 'utlost_av' ? nyAvhengighetHendelseId || null : null,
        beskrivelse: nyAvhengighetBeskrivelse.trim() || null,
        tilOrganisasjonsnummer: nyAvhengighetTilId ? null : nyAvhengighetTilOrgnr.trim() || null,
        tilNavn: nyAvhengighetTilId ? null : nyAvhengighetTilNavn.trim() || null,
        tilUrl: nyAvhengighetTilId ? null : nyAvhengighetTilUrl.trim() || null,
      });
      setAvhengigheter(oppdatert);
      velgTilTjeneste('');
      setTverrTenantSok('');
      setTverrTenantTreff([]);
      setNyAvhengighetHendelseId('');
      setNyAvhengighetBeskrivelse('');
    } catch (err) {
      setAvhengighetFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved opprettelse av avhengighet.');
    } finally {
      setLeggerTilAvhengighet(false);
    }
  }

  async function fjernAvhengighet(avhengighetId: string) {
    await api.slettTjenesteavhengighet(avhengighetId);
    setAvhengigheter((forrige) => (forrige ?? []).filter((a) => a.id !== avhengighetId));
  }

  async function endreStatus(nyStatus: string) {
    if (!id) return;
    setStatusEndres(true);
    setLagreFeil(null);
    try {
      const oppdatert = await api.settTjenesteStatus(id, { status: nyStatus });
      setTjeneste(oppdatert);
    } catch (err) {
      setLagreFeil(err instanceof ApiError ? err.message : 'Ukjent feil ved statusendring.');
    } finally {
      setStatusEndres(false);
    }
  }

  if (feil) return <div className="feilmelding">{feil}</div>;
  if (!tjeneste) return <Paragraph>Laster …</Paragraph>;

  return (
    <>
      <Heading level={1} data-size="lg">
        {tjeneste.tittel}
      </Heading>
      <Tag data-color="info" style={{ marginBottom: '1.5rem' }}>{tjeneste.status}</Tag>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Vilkårstre
        </Heading>
        {tjeneste.rotnodeId ? (
          <>
            <Paragraph style={{ marginBottom: '0.75rem', display: 'flex', gap: '1rem', alignItems: 'center', flexWrap: 'wrap' }}>
              <span>Rotnode: <strong>{rotnode?.tittel ?? '…'}</strong></span>
              <Link asChild>
                <RouterLink to={`/vilkarstre/${tjeneste.rotnodeId}`}>Åpne vilkårstre →</RouterLink>
              </Link>
              <Link asChild>
                <RouterLink to={`/tjenester/${tjeneste.id}/veiledning`}>Åpne veiledning →</RouterLink>
              </Link>
            </Paragraph>
            <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.75rem' }}>
              <Button data-size="sm" variant="secondary" onClick={() => setVisByttRotnode((v) => !v)}>
                {visByttRotnode ? 'Avbryt' : 'Bytt rotnode'}
              </Button>
              <Button data-size="sm" variant="tertiary" data-color="danger" disabled={rotnodeEndres} onClick={fjernRotnode}>
                Fjern rotnode
              </Button>
            </div>
            {visByttRotnode && (
              <form onSubmit={byttRotnode} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
                <Field>
                  <Label>Ny rotnode (regelnode)</Label>
                  <Select data-size="sm" value={valgtRotnodeId} onChange={(e) => setValgtRotnodeId(e.target.value)}>
                    <Select.Option value="">Velg …</Select.Option>
                    {regelnoder.map((r) => <Select.Option key={r.id} value={r.id}>{r.tittel}</Select.Option>)}
                  </Select>
                </Field>
                <Button data-size="sm" type="submit" disabled={rotnodeEndres || !valgtRotnodeId}>
                  {rotnodeEndres ? 'Setter …' : 'Sett som rotnode'}
                </Button>
              </form>
            )}
          </>
        ) : visOpprettRotnode ? (
          <form onSubmit={opprettRotnode} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
            <Textfield data-size="sm" label="Rotnodens tittel" value={nyRotnodeTittel} onChange={(e) => setNyRotnodeTittel(e.target.value)} required />
            <Button data-size="sm" type="submit" disabled={rotnodeEndres || !nyRotnodeTittel.trim()}>
              {rotnodeEndres ? 'Oppretter …' : 'Opprett'}
            </Button>
            <Button data-size="sm" variant="tertiary" onClick={() => setVisOpprettRotnode(false)}>Avbryt</Button>
          </form>
        ) : (
          <Button data-size="sm" variant="secondary" onClick={() => { setVisOpprettRotnode(true); setNyRotnodeTittel(`Vedtak om ${tjeneste.tittel.toLowerCase()}`); }}>
            Opprett rotnode
          </Button>
        )}
        {rotnodeFeil && <div className="feilmelding" style={{ marginTop: '0.5rem' }}>{rotnodeFeil}</div>}
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Egenskaper
        </Heading>
        <form onSubmit={lagre} style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem', maxWidth: '40rem' }}>
          <Textfield label="Tittel" value={tittel} onChange={(e) => setTittel(e.target.value)} required />
          <Field>
            <Label>Beskrivelse</Label>
            <Textarea value={beskrivelse} onChange={(e) => setBeskrivelse(e.target.value)} rows={3} />
          </Field>
          <Field>
            <Label>Formål</Label>
            <Textarea value={formal} onChange={(e) => setFormal(e.target.value)} rows={3}
              placeholder="F.eks. lovens eget «§1 Formål»-avsnitt — atskilt fra Beskrivelse over." />
          </Field>
          <Textfield label="Kompetent myndighet" value={kompetentMyndighet} onChange={(e) => setKompetentMyndighet(e.target.value)} />
          <Textfield label="Tjenestetype" value={tjenestetype} onChange={(e) => setTjenestetype(e.target.value)} />
          <Field>
            <Label>Rettighetstype</Label>
            <Select value={type} onChange={(e) => setType(e.target.value)}>
              <Select.Option value="">Ikke satt</Select.Option>
              {GYLDIGE_RETTIGHETSTYPER.map((t) => <Select.Option key={t} value={t}>{t}</Select.Option>)}
            </Select>
          </Field>
          <Textfield label="Målgruppe (kommaseparert)" value={malgruppe} onChange={(e) => setMalgruppe(e.target.value)}
            placeholder="f.eks. Virksomheter som skal etablere et nytt serveringssted" />
          <Textfield label="Kanaler (kommaseparert)" value={kanaler} onChange={(e) => setKanaler(e.target.value)} placeholder="f.eks. Nett, Skranke" />
          <Textfield label="Kostnad" value={kostnad} onChange={(e) => setKostnad(e.target.value)} />
          <Textfield label="Behandlingstid" value={behandlingstid} onChange={(e) => setBehandlingstid(e.target.value)} />
          <Textfield label="Kontaktpunkt" value={kontaktpunkt} onChange={(e) => setKontaktpunkt(e.target.value)} />
          <Textfield label="Konsekvens ved brudd" value={konsekvensVedBrudd} onChange={(e) => setKonsekvensVedBrudd(e.target.value)} />
          <Textfield label="Språk (kommaseparert)" value={sprak} onChange={(e) => setSprak(e.target.value)} placeholder="f.eks. nb, en" />
          <Textfield label="Livshendelser (kommaseparert)" value={livshendelser} onChange={(e) => setLivshendelser(e.target.value)}
            placeholder="f.eks. Starte og drive en bedrift" />
          <Textfield label="LOS-klassifisering" value={losKlassifisering} onChange={(e) => setLosKlassifisering(e.target.value)} />
          <Textfield label="Tjenesteområde" value={tjenesteomrade} onChange={(e) => setTjenesteomrade(e.target.value)}
            placeholder="f.eks. Næring, salg og servering" />
          {lagreFeil && <div className="feilmelding">{lagreFeil}</div>}
          <div>
            <Button type="submit" disabled={lagrer}>{lagrer ? 'Lagrer …' : 'Lagre'}</Button>
          </div>
        </form>
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
          <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
            Innhold
          </Heading>
          <Button data-size="sm" variant="tertiary" onClick={() => setVisRedigerInnhold((v) => !v)}>
            {visRedigerInnhold ? 'Avbryt' : tjeneste.innhold ? 'Rediger innhold' : 'Legg til innhold'}
          </Button>
        </div>
        <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)', marginBottom: '0.75rem' }}>
          Rettighetens rike, forfattede innholdsseksjoner — tidspunkt/frister, hvem som kan sende inn,
          vedlegg, veiledning, hva rettigheten faktisk innebærer for innehaveren.
        </Paragraph>

        {!visRedigerInnhold && !tjeneste.innhold && <Paragraph>Ingen innhold registrert ennå.</Paragraph>}

        {!visRedigerInnhold && tjeneste.innhold && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
            {tjeneste.innhold.tidspunktOgFrister && (
              <div>
                <Heading level={3} data-size="xs" style={{ marginBottom: '0.25rem' }}>Tidspunkt og frister</Heading>
                <Paragraph>{tjeneste.innhold.tidspunktOgFrister}</Paragraph>
              </div>
            )}
            {tjeneste.innhold.innsenderOgTilgang && (
              <div>
                <Heading level={3} data-size="xs" style={{ marginBottom: '0.25rem' }}>Innsender og tilgang</Heading>
                {tjeneste.innhold.innsenderOgTilgang.hvemKanSende.length > 0 && (
                  <ul style={{ margin: '0 0 0.3rem', paddingLeft: '1.25rem' }}>
                    {tjeneste.innhold.innsenderOgTilgang.hvemKanSende.map((s, i) => <li key={i}>{s}</li>)}
                  </ul>
                )}
                {tjeneste.innhold.innsenderOgTilgang.innlogging && (
                  <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
                    {tjeneste.innhold.innsenderOgTilgang.innlogging}
                  </Paragraph>
                )}
              </div>
            )}
            {tjeneste.innhold.vedlegg.length > 0 && (
              <div>
                <Heading level={3} data-size="xs" style={{ marginBottom: '0.25rem' }}>Vedlegg</Heading>
                <ul style={{ margin: '0 0 0.3rem', paddingLeft: '1.25rem' }}>
                  {tjeneste.innhold.vedlegg.map((s, i) => <li key={i}>{s}</li>)}
                </ul>
                {tjeneste.innhold.vedleggMerknad && (
                  <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
                    {tjeneste.innhold.vedleggMerknad}
                  </Paragraph>
                )}
              </div>
            )}
            {tjeneste.innhold.opplysningerSomSkalSendesInn.length > 0 && (
              <div>
                <Heading level={3} data-size="xs" style={{ marginBottom: '0.25rem' }}>Opplysninger som skal sendes inn</Heading>
                <ul style={{ margin: '0 0 0.3rem', paddingLeft: '1.25rem' }}>
                  {tjeneste.innhold.opplysningerSomSkalSendesInn.map((s, i) => <li key={i}>{s}</li>)}
                </ul>
                {tjeneste.innhold.opplysningerMerknad && (
                  <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
                    {tjeneste.innhold.opplysningerMerknad}
                  </Paragraph>
                )}
              </div>
            )}
            {tjeneste.innhold.veiledningOgUtfylling.length > 0 && (
              <div>
                <Heading level={3} data-size="xs" style={{ marginBottom: '0.25rem' }}>Veiledning og utfylling</Heading>
                <ul style={{ margin: '0 0 0.3rem', paddingLeft: '1.25rem' }}>
                  {tjeneste.innhold.veiledningOgUtfylling.map((s, i) => <li key={i}>{s}</li>)}
                </ul>
                {tjeneste.innhold.veiledningMerknad && (
                  <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
                    {tjeneste.innhold.veiledningMerknad}
                  </Paragraph>
                )}
              </div>
            )}
            {tjeneste.innhold.innsendingOgOppfolging && (
              <div>
                <Heading level={3} data-size="xs" style={{ marginBottom: '0.25rem' }}>Innsending og oppfølging</Heading>
                {tjeneste.innhold.innsendingOgOppfolging.kanal && <Paragraph>{tjeneste.innhold.innsendingOgOppfolging.kanal}</Paragraph>}
                {tjeneste.innhold.innsendingOgOppfolging.etterMottak.length > 0 && (
                  <ul style={{ margin: '0 0 0.3rem', paddingLeft: '1.25rem' }}>
                    {tjeneste.innhold.innsendingOgOppfolging.etterMottak.map((s, i) => <li key={i}>{s}</li>)}
                  </ul>
                )}
                {tjeneste.innhold.innsendingOgOppfolging.merknad && (
                  <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
                    {tjeneste.innhold.innsendingOgOppfolging.merknad}
                  </Paragraph>
                )}
              </div>
            )}
            {tjeneste.innhold.kontaktOgHjelp && (
              <div>
                <Heading level={3} data-size="xs" style={{ marginBottom: '0.25rem' }}>Kontakt og hjelp</Heading>
                {tjeneste.innhold.kontaktOgHjelp.generelt && <Paragraph>{tjeneste.innhold.kontaktOgHjelp.generelt}</Paragraph>}
                {tjeneste.innhold.kontaktOgHjelp.kommunenKanVeiledeOm.length > 0 && (
                  <ul style={{ margin: '0.3rem 0 0', paddingLeft: '1.25rem' }}>
                    {tjeneste.innhold.kontaktOgHjelp.kommunenKanVeiledeOm.map((s, i) => <li key={i}>{s}</li>)}
                  </ul>
                )}
              </div>
            )}
            {tjeneste.innhold.hvaRettighetenInnebarer && (
              <div>
                <Heading level={3} data-size="xs" style={{ marginBottom: '0.25rem' }}>Hva rettigheten innebærer</Heading>
                {tjeneste.innhold.hvaRettighetenInnebarer.innledning && <Paragraph style={{ marginBottom: '0.4rem' }}>{tjeneste.innhold.hvaRettighetenInnebarer.innledning}</Paragraph>}
                {tjeneste.innhold.hvaRettighetenInnebarer.varighet && <Paragraph style={{ marginBottom: '0.4rem' }}>{tjeneste.innhold.hvaRettighetenInnebarer.varighet}</Paragraph>}
                {tjeneste.innhold.hvaRettighetenInnebarer.plikter.length > 0 && (
                  <ul style={{ margin: '0 0 0.4rem', paddingLeft: '1.25rem' }}>
                    {tjeneste.innhold.hvaRettighetenInnebarer.plikter.map((s, i) => <li key={i}>{s}</li>)}
                  </ul>
                )}
                {tjeneste.innhold.hvaRettighetenInnebarer.endringerIVirksomheten && (
                  <div style={{ marginBottom: '0.4rem' }}>
                    {tjeneste.innhold.hvaRettighetenInnebarer.endringerIVirksomheten.plikt && (
                      <Paragraph>{tjeneste.innhold.hvaRettighetenInnebarer.endringerIVirksomheten.plikt}</Paragraph>
                    )}
                    {tjeneste.innhold.hvaRettighetenInnebarer.endringerIVirksomheten.eksempler.length > 0 && (
                      <ul style={{ margin: '0.2rem 0 0', paddingLeft: '1.25rem' }}>
                        {tjeneste.innhold.hvaRettighetenInnebarer.endringerIVirksomheten.eksempler.map((s, i) => <li key={i}>{s}</li>)}
                      </ul>
                    )}
                  </div>
                )}
                {tjeneste.innhold.hvaRettighetenInnebarer.kravTilDrift && <Paragraph style={{ marginBottom: '0.4rem' }}>{tjeneste.innhold.hvaRettighetenInnebarer.kravTilDrift}</Paragraph>}
                {tjeneste.innhold.hvaRettighetenInnebarer.tommeavtaleOgKontroll && <Paragraph style={{ marginBottom: '0.4rem' }}>{tjeneste.innhold.hvaRettighetenInnebarer.tommeavtaleOgKontroll}</Paragraph>}
                {tjeneste.innhold.hvaRettighetenInnebarer.kontrollOgTilsyn && <Paragraph style={{ marginBottom: '0.4rem' }}>{tjeneste.innhold.hvaRettighetenInnebarer.kontrollOgTilsyn}</Paragraph>}
                {tjeneste.innhold.hvaRettighetenInnebarer.rapportering && <Paragraph style={{ marginBottom: '0.4rem' }}>{tjeneste.innhold.hvaRettighetenInnebarer.rapportering}</Paragraph>}
                {tjeneste.innhold.hvaRettighetenInnebarer.avgrensningMerknad && (
                  <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)' }}>
                    {tjeneste.innhold.hvaRettighetenInnebarer.avgrensningMerknad}
                  </Paragraph>
                )}
              </div>
            )}
          </div>
        )}

        {visRedigerInnhold && (
          <form onSubmit={lagreInnhold} style={{ display: 'flex', flexDirection: 'column', gap: '1rem', maxWidth: '40rem' }}>
            <Field><Label>Tidspunkt og frister</Label><Textarea value={iTidspunktOgFrister} onChange={(e) => setITidspunktOgFrister(e.target.value)} rows={2} /></Field>

            <Heading level={4} data-size="2xs">Innsender og tilgang</Heading>
            <Field><Label>Hvem kan sende (én pr. linje)</Label><Textarea value={iInnsenderHvemKanSende} onChange={(e) => setIInnsenderHvemKanSende(e.target.value)} rows={3} /></Field>
            <Textfield label="Innlogging" value={iInnsenderInnlogging} onChange={(e) => setIInnsenderInnlogging(e.target.value)} />

            <Heading level={4} data-size="2xs">Vedlegg</Heading>
            <Field><Label>Vedlegg (én pr. linje)</Label><Textarea value={iVedlegg} onChange={(e) => setIVedlegg(e.target.value)} rows={3} /></Field>
            <Textfield label="Merknad" value={iVedleggMerknad} onChange={(e) => setIVedleggMerknad(e.target.value)} />

            <Heading level={4} data-size="2xs">Opplysninger som skal sendes inn</Heading>
            <Field><Label>Opplysninger (én pr. linje)</Label><Textarea value={iOpplysninger} onChange={(e) => setIOpplysninger(e.target.value)} rows={3} /></Field>
            <Textfield label="Merknad" value={iOpplysningerMerknad} onChange={(e) => setIOpplysningerMerknad(e.target.value)} />

            <Heading level={4} data-size="2xs">Veiledning og utfylling</Heading>
            <Field><Label>Veiledningspunkter (én pr. linje)</Label><Textarea value={iVeiledning} onChange={(e) => setIVeiledning(e.target.value)} rows={3} /></Field>
            <Textfield label="Merknad" value={iVeiledningMerknad} onChange={(e) => setIVeiledningMerknad(e.target.value)} />

            <Heading level={4} data-size="2xs">Innsending og oppfølging</Heading>
            <Textfield label="Kanal" value={iInnsendingKanal} onChange={(e) => setIInnsendingKanal(e.target.value)} />
            <Field><Label>Etter mottak (én pr. linje)</Label><Textarea value={iInnsendingEtterMottak} onChange={(e) => setIInnsendingEtterMottak(e.target.value)} rows={3} /></Field>
            <Textfield label="Merknad" value={iInnsendingMerknad} onChange={(e) => setIInnsendingMerknad(e.target.value)} />

            <Heading level={4} data-size="2xs">Kontakt og hjelp</Heading>
            <Textfield label="Generelt" value={iKontaktGenerelt} onChange={(e) => setIKontaktGenerelt(e.target.value)} />
            <Field><Label>Kommunen kan veilede om (én pr. linje)</Label><Textarea value={iKontaktKommunenKanVeiledeOm} onChange={(e) => setIKontaktKommunenKanVeiledeOm(e.target.value)} rows={3} /></Field>

            <Heading level={4} data-size="2xs">Hva rettigheten innebærer</Heading>
            <Textfield label="Innledning" value={iHviInnledning} onChange={(e) => setIHviInnledning(e.target.value)} />
            <Textfield label="Varighet" value={iHviVarighet} onChange={(e) => setIHviVarighet(e.target.value)} />
            <Field><Label>Plikter (én pr. linje)</Label><Textarea value={iHviPlikter} onChange={(e) => setIHviPlikter(e.target.value)} rows={3} /></Field>
            <Textfield label="Endringer i virksomheten — plikt" value={iHviEndringerPlikt} onChange={(e) => setIHviEndringerPlikt(e.target.value)} />
            <Field><Label>Endringer i virksomheten — eksempler (én pr. linje)</Label><Textarea value={iHviEndringerEksempler} onChange={(e) => setIHviEndringerEksempler(e.target.value)} rows={3} /></Field>
            <Field><Label>Krav til drift (kun relevant for løpende krav/plikt-typer rettigheter)</Label><Textarea value={iHviKravTilDrift} onChange={(e) => setIHviKravTilDrift(e.target.value)} rows={2} /></Field>
            <Field><Label>Tømmeavtale og kontroll</Label><Textarea value={iHviTommeavtaleOgKontroll} onChange={(e) => setIHviTommeavtaleOgKontroll(e.target.value)} rows={2} /></Field>
            <Field><Label>Rapportering</Label><Textarea value={iHviRapportering} onChange={(e) => setIHviRapportering(e.target.value)} rows={2} /></Field>
            <Field><Label>Kontroll og tilsyn</Label><Textarea value={iHviKontrollOgTilsyn} onChange={(e) => setIHviKontrollOgTilsyn(e.target.value)} rows={2} /></Field>
            <Field><Label>Avgrensning (f.eks. mot en tilstøtende rettighet)</Label><Textarea value={iHviAvgrensningMerknad} onChange={(e) => setIHviAvgrensningMerknad(e.target.value)} rows={2} /></Field>

            {innholdFeil && <div className="feilmelding">{innholdFeil}</div>}
            <div><Button type="submit" disabled={innholdLagrer}>{innholdLagrer ? 'Lagrer …' : 'Lagre innhold'}</Button></div>
          </form>
        )}
      </section>

      <section style={{ marginBottom: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Status
        </Heading>
        <Select value={tjeneste.status} disabled={statusEndres} onChange={(e) => endreStatus(e.target.value)} style={{ maxWidth: '16rem' }}>
          {STATUSER.map((s) => (
            <Select.Option key={s} value={s}>{s}</Select.Option>
          ))}
        </Select>
      </section>

      <section>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Regelverksreferanser
        </Heading>
        {referanser === null && <Paragraph>Laster …</Paragraph>}
        {referanser && referanser.length === 0 && <Paragraph>Ingen regelverksreferanser koblet ennå.</Paragraph>}
        {referanser && referanser.length > 0 && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem', marginBottom: '0.75rem' }}>
            {referanserGruppert.map(([tilRettskildeId, rader]) => {
              const rettskilde = rettskilder.find((r) => r.id === tilRettskildeId);
              return (
                <div key={tilRettskildeId}>
                  <Heading level={3} data-size="xs" style={{ marginBottom: '0.3rem' }}>
                    {rettskilde ? (rettskilde.kortnavn ?? rettskilde.tittel) : tilRettskildeId}
                  </Heading>
                  <ul style={{ margin: 0, paddingLeft: '1.25rem' }}>
                    {rader.map((r) => {
                      const visningstekst = eidVisningstekst(r.tilEid, rettskilder, noderPerRettskilde);
                      const href = rettskildeLenke(r.tilEid, rettskilder);
                      return (
                        <li key={r.id} style={{ fontSize: 'var(--ds-font-size-1)', display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                          {href ? (
                            <Link asChild><RouterLink to={href}>{visningstekst ?? r.tilEid}</RouterLink></Link>
                          ) : (
                            <span style={visningstekst ? undefined : { fontFamily: 'monospace' }}>{visningstekst ?? r.tilEid}</span>
                          )}
                          <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjernReferanse(r.id)}>Fjern</Button>
                        </li>
                      );
                    })}
                  </ul>
                </div>
              );
            })}
          </div>
        )}

        <form onSubmit={leggTilReferanse} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginTop: '0.75rem' }}>
          <Field>
            <Label>Rettskilde</Label>
            <Select
              data-size="sm"
              value={nyReferanseRettskildeId}
              onChange={(e) => { setNyReferanseRettskildeId(e.target.value); setNyReferanseEid(''); }}
            >
              <Select.Option value="">Velg …</Select.Option>
              {rettskilder.map((r) => <Select.Option key={r.id} value={r.id}>{r.tittel}</Select.Option>)}
            </Select>
          </Field>
          {nyReferanseRettskildeId && paragrafKandidater.length > 0 && (
            <Field>
              <Label>Paragraf</Label>
              <Select data-size="sm" value={nyReferanseEid} onChange={(e) => setNyReferanseEid(e.target.value)}>
                <Select.Option value="">Velg …</Select.Option>
                {paragrafKandidater.map((n) => (
                  <Select.Option key={n.id} value={n.eid}>
                    {/* "side"-noder (Brukerveiledning) har ikke noe paragrafnummer — vis "Hele siden" i stedet */}
                    {n.nodeType === 'side' ? 'Hele siden' : n.nummer}{n.overskrift ? ` — ${n.overskrift}` : ''}
                  </Select.Option>
                ))}
              </Select>
            </Field>
          )}
          <Textfield data-size="sm" label="Avansert / manuell eId" value={nyReferanseEid}
            onChange={(e) => setNyReferanseEid(e.target.value)} style={{ minWidth: '22rem', fontFamily: 'monospace' }} />
          <Button data-size="sm" type="submit" disabled={leggerTilReferanse || !nyReferanseRettskildeId || !nyReferanseEid.trim()}>
            {leggerTilReferanse ? 'Kobler …' : 'Koble referanse'}
          </Button>
          {referanseFeil && <div className="feilmelding" style={{ width: '100%' }}>{referanseFeil}</div>}
        </form>
      </section>

      <section style={{ marginTop: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Hendelser
        </Heading>
        <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)', marginBottom: '0.75rem' }}>
          Ren, symmetrisk klassifisering (docs/03-domenemodell.md §1.5) — ingen retning. To tjenester som
          deler samme hendelse blir relaterte uten at én forårsaker den andre.
        </Paragraph>
        {hendelser === null && <Paragraph>Laster …</Paragraph>}
        {hendelser && hendelser.length === 0 && <Paragraph>Ingen hendelser koblet ennå.</Paragraph>}
        {hendelser && hendelser.length > 0 && (
          <ul>
            {hendelser.map((h) => (
              <li key={h.id} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                <span>{h.navn}</span>
                <Tag data-color="neutral" data-size="sm">{h.type}</Tag>
                <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjernHendelse(h.id)}>Fjern</Button>
              </li>
            ))}
          </ul>
        )}

        <form onSubmit={kobleHendelse} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginTop: '0.75rem' }}>
          <Field>
            <Label>Eksisterende hendelse</Label>
            <Select data-size="sm" value={nyHendelseId} onChange={(e) => setNyHendelseId(e.target.value)}>
              <Select.Option value="">Velg …</Select.Option>
              {alleHendelser
                .filter((h) => !(hendelser ?? []).some((koblet) => koblet.id === h.id))
                .map((h) => <Select.Option key={h.id} value={h.id}>{h.navn} ({h.type})</Select.Option>)}
            </Select>
          </Field>
          <Button data-size="sm" type="submit" disabled={leggerTilHendelse || !nyHendelseId}>
            {leggerTilHendelse ? 'Kobler …' : 'Koble hendelse'}
          </Button>
          <Button data-size="sm" variant="tertiary" onClick={() => setVisNyHendelse((v) => !v)}>
            {visNyHendelse ? 'Avbryt' : '+ Ny hendelse'}
          </Button>
        </form>
        {visNyHendelse && (
          <form onSubmit={opprettOgKobleHendelse} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginTop: '0.5rem' }}>
            <Textfield data-size="sm" label="Navn på ny hendelse" value={nyHendelseNavn} onChange={(e) => setNyHendelseNavn(e.target.value)} required />
            <Field>
              <Label>Type</Label>
              <Select data-size="sm" value={nyHendelseType} onChange={(e) => setNyHendelseType(e.target.value)}>
                <Select.Option value="generell">Generell (cv:Event)</Select.Option>
                <Select.Option value="livshendelse">Livshendelse</Select.Option>
                <Select.Option value="virksomhetshendelse">Virksomhetshendelse</Select.Option>
              </Select>
            </Field>
            <Button data-size="sm" type="submit" disabled={leggerTilHendelse || !nyHendelseNavn.trim()}>
              {leggerTilHendelse ? 'Oppretter …' : 'Opprett og koble'}
            </Button>
          </form>
        )}
        {hendelseFeil && <div className="feilmelding" style={{ marginTop: '0.5rem' }}>{hendelseFeil}</div>}
      </section>

      <section style={{ marginTop: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Handlinger
        </Heading>
        <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)', marginBottom: '0.75rem' }}>
          Konkrete, tidsavgrensede interaksjoner knyttet til denne rettigheten (søknad, melding, klage …) —
          hver med egne kanaler/vedlegg/behandlingstid/veiledningstekst.
        </Paragraph>
        {handlinger === null && <Paragraph>Laster …</Paragraph>}
        {handlinger && handlinger.length === 0 && <Paragraph>Ingen handlinger registrert ennå.</Paragraph>}
        {handlinger && handlinger.length > 0 && (
          <ul>
            {handlinger.map((h) => (
              <li key={h.id} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                <Link asChild>
                  <RouterLink to={`/tjenester/${tjeneste.id}/handlinger/${h.id}`}>{h.navn}</RouterLink>
                </Link>
                <Tag data-color="info" data-size="sm">{h.handlingstype}</Tag>
                {h.utfortAv && <Tag data-color="neutral" data-size="sm">{h.utfortAv}</Tag>}
                <Tag data-color="neutral" data-size="sm">{h.status}</Tag>
              </li>
            ))}
          </ul>
        )}

        <form onSubmit={opprettHandling} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginTop: '0.75rem' }}>
          <Textfield data-size="sm" label="Navn på ny handling" value={nyHandlingNavn} onChange={(e) => setNyHandlingNavn(e.target.value)} required />
          <Field>
            <Label>Handlingstype</Label>
            <Select data-size="sm" value={nyHandlingType} onChange={(e) => setNyHandlingType(e.target.value)}>
              {GYLDIGE_HANDLINGSTYPER.map((t) => <Select.Option key={t} value={t}>{t}</Select.Option>)}
            </Select>
          </Field>
          <Field>
            <Label>Utført av</Label>
            <Select data-size="sm" value={nyHandlingUtfortAv} onChange={(e) => setNyHandlingUtfortAv(e.target.value)}>
              <Select.Option value="">Ikke satt</Select.Option>
              {GYLDIGE_UTFORT_AV.map((u) => <Select.Option key={u} value={u}>{u}</Select.Option>)}
            </Select>
          </Field>
          <Button data-size="sm" type="submit" disabled={leggerTilHandling || !nyHandlingNavn.trim()}>
            {leggerTilHandling ? 'Oppretter …' : 'Opprett handling'}
          </Button>
        </form>
        {handlingFeil && <div className="feilmelding" style={{ marginTop: '0.5rem' }}>{handlingFeil}</div>}
      </section>

      <section style={{ marginTop: '2rem' }}>
        <Heading level={2} data-size="sm" style={{ marginBottom: '0.75rem' }}>
          Tjenesteavhengigheter
        </Heading>
        <Paragraph style={{ color: 'var(--ds-color-neutral-text-subtle)', fontSize: 'var(--ds-font-size-1)', marginBottom: '0.75rem' }}>
          Rettede, årsaksforklarte koblinger mellom to tjenester (docs/03-domenemodell.md §1.5) — ett
          rettet kant per relasjon, vist med riktig tekst uansett hvilken side du ser fra.
        </Paragraph>
        {avhengigheter === null && <Paragraph>Laster …</Paragraph>}
        {avhengigheter && avhengigheter.length === 0 && <Paragraph>Ingen tjenesteavhengigheter registrert ennå.</Paragraph>}
        {avhengigheter && avhengigheter.length > 0 && (
          <ul>
            {avhengigheter.map((a) => (
              <li key={a.id} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                {/* Ekstern referanse (motpartTjenesteId null) har ingen ekte Tjeneste-rad å navigere til
                    — vis visningsteksten som ren tekst i stedet for en /tjenester/:id-lenke. */}
                {a.motpartTjenesteId ? (
                  <Link asChild>
                    <RouterLink to={`/tjenester/${a.motpartTjenesteId}`}>{a.visningstekst}</RouterLink>
                  </Link>
                ) : (
                  <span>{a.visningstekst}</span>
                )}
                {a.motpartOrganisasjonsnummer && (
                  <Tag data-color="info" data-size="sm">org.nr {a.motpartOrganisasjonsnummer}</Tag>
                )}
                {a.motpartUrl && (
                  <Link href={a.motpartUrl} target="_blank" rel="noreferrer" style={{ fontSize: 'var(--ds-font-size-1)' }}>↗</Link>
                )}
                {a.beskrivelse && <Tag data-color="neutral" data-size="sm">{a.beskrivelse}</Tag>}
                {/* Sletting virker uansett hvilken side raden vises fra — samme rad-id begge steder. */}
                <Button variant="tertiary" data-color="danger" data-size="sm" onClick={() => fjernAvhengighet(a.id)}>Fjern</Button>
              </li>
            ))}
          </ul>
        )}

        <form onSubmit={leggTilAvhengighet} style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap', marginTop: '0.75rem' }}>
          <Field>
            <Label>Relasjon (denne tjenesten …)</Label>
            <Select data-size="sm" value={nyAvhengighetRel} onChange={(e) => setNyAvhengighetRel(e.target.value)}>
              {TJENESTEAVHENGIGHET_REL.map((r) => <Select.Option key={r.id} value={r.id}>{r.label}</Select.Option>)}
            </Select>
          </Field>
          <Field>
            <Label>Til tjeneste (egen virksomhet)</Label>
            <Select data-size="sm" value={nyAvhengighetTilId} onChange={(e) => velgTilTjeneste(e.target.value)}>
              <Select.Option value="">Velg …</Select.Option>
              {alleTjenester.filter((t) => t.id !== id).map((t) => <Select.Option key={t.id} value={t.id}>{t.tittel}</Select.Option>)}
            </Select>
          </Field>
          {nyAvhengighetRel === 'utlost_av' && (
            <Field>
              <Label>Hendelse</Label>
              <Select data-size="sm" value={nyAvhengighetHendelseId} onChange={(e) => setNyAvhengighetHendelseId(e.target.value)}>
                <Select.Option value="">Velg …</Select.Option>
                {alleHendelser.map((h) => <Select.Option key={h.id} value={h.id}>{h.navn}</Select.Option>)}
              </Select>
            </Field>
          )}
          <Textfield data-size="sm" label="Nyanse/unntak (valgfritt)" value={nyAvhengighetBeskrivelse}
            onChange={(e) => setNyAvhengighetBeskrivelse(e.target.value)} style={{ minWidth: '16rem' }} />
          <Button data-size="sm" type="submit"
            disabled={leggerTilAvhengighet || (!nyAvhengighetTilId && !(nyAvhengighetTilOrgnr.trim() && nyAvhengighetTilNavn.trim()))}>
            {leggerTilAvhengighet ? 'Oppretter …' : 'Opprett avhengighet'}
          </Button>
        </form>

        <div style={{ marginTop: '0.75rem', paddingTop: '0.75rem', borderTop: '1px solid var(--ds-color-neutral-border-subtle)' }}>
          <Paragraph style={{ fontSize: 'var(--ds-font-size-1)', color: 'var(--ds-color-neutral-text-subtle)', marginBottom: '0.5rem' }}>
            Eller finn en ANNEN virksomhets publiserte tjeneste, eller — hvis den ikke finnes som en ekte
            tjeneste i Regel-IDE i det hele tatt (f.eks. en tjeneste hos Mattilsynet/Politiet) — oppgi den
            som en ekstern referanse manuelt nedenfor.
          </Paragraph>
          <Textfield data-size="sm" label="Søk i andre virksomheters publiserte tjenester" value={tverrTenantSok}
            onChange={(e) => setTverrTenantSok(e.target.value)} style={{ maxWidth: '24rem', marginBottom: '0.5rem' }} />
          {tverrTenantSokerLaster && <Paragraph style={{ fontSize: 'var(--ds-font-size-1)' }}>Søker …</Paragraph>}
          {!tverrTenantSokerLaster && tverrTenantSok.trim() && tverrTenantTreff.length === 0 && (
            <Paragraph style={{ fontSize: 'var(--ds-font-size-1)' }}>Ingen treff.</Paragraph>
          )}
          {tverrTenantTreff.length > 0 && (
            <ul style={{ maxHeight: '12rem', overflow: 'auto', marginBottom: '0.5rem' }}>
              {tverrTenantTreff.map((t) => (
                <li key={t.id} style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', marginBottom: '0.2rem' }}>
                  <span style={{ flex: 1, fontSize: 'var(--ds-font-size-1)' }}>
                    {t.tittel} <span style={{ color: 'var(--ds-color-neutral-text-subtle)' }}>({t.virksomhetNavn})</span>
                  </span>
                  <Button data-size="sm" variant="tertiary" onClick={() => velgTilTjeneste(t.id, t)}>Velg</Button>
                </li>
              ))}
            </ul>
          )}
          {valgtTverrTenantTreff && (
            <Tag data-color="success" data-size="sm" style={{ marginBottom: '0.5rem' }}>
              Valgt: {valgtTverrTenantTreff.tittel} ({valgtTverrTenantTreff.virksomhetNavn})
            </Tag>
          )}
          <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'flex-end', flexWrap: 'wrap' }}>
            <Textfield data-size="sm" label="Avansert / manuell — organisasjonsnummer" value={nyAvhengighetTilOrgnr}
              onChange={(e) => endreEkstern('orgnr', e.target.value)} style={{ minWidth: '12rem' }} />
            <Textfield data-size="sm" label="Navn på tjenesten" value={nyAvhengighetTilNavn}
              onChange={(e) => endreEkstern('navn', e.target.value)} style={{ minWidth: '16rem' }} />
            <Textfield data-size="sm" label="URL (valgfritt)" value={nyAvhengighetTilUrl}
              onChange={(e) => endreEkstern('url', e.target.value)} style={{ minWidth: '14rem' }} />
          </div>
        </div>
        {avhengighetFeil && <div className="feilmelding" style={{ marginTop: '0.5rem' }}>{avhengighetFeil}</div>}
      </section>
    </>
  );
}
