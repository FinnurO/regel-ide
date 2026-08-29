// Speiler DTO-ene i RegelIde.Api/Dtos.cs + GjeldendeBrukerTjeneste.cs nøyaktig.

export interface RettskildeSammendrag {
  id: string;
  virksomhetId: string | null;
  eli: string | null;
  tittel: string;
  kortnavn: string | null;
  kildetype: string;
}

export interface RettskildeDetalj {
  id: string;
  virksomhetId: string | null;
  doctype: string;
  kildetype: string;
  tittel: string;
  kortnavn: string | null;
  eli: string | null;
  ikrafttredelse: string | null;
  konsolidertDato: string | null;
  utgiver: string | null;
  status: string;
  aknXml: string | null;
  /** ELI (over) er ALLTID skrivebeskyttet — disse fem er derimot redigerbare via oppdaterRettskildeMetadata. */
  interntDokNr: string | null;
  revisjonsnr: string | null;
  vedtattAv: string | null;
  vedtaksdato: string | null;
  gyldigTil: string | null;
  /** Kildens opprinnelige URL — satt for Brukerveiledning (den hentede nettsidens URL) og noen håndbøker. */
  url: string | null;
}

export interface RettskildeNodeDto {
  id: string;
  eid: string;
  parentNodeId: string | null;
  nodeType: string;
  nummer: string | null;
  overskrift: string | null;
  tekst: string | null;
  /** Flyttet gjennom fra Lovdatas data-repealeddate (2026-07-24) — se docs/08-byggesteg1-teknisk-design.md §2. */
  opphevet: boolean;
  opphevetDato: string | null;
  /** Node-nivå versjonering (2026-07-26) — kun >1 for redigerte håndbok-seksjoner. */
  versjon: number;
  /** Kun satt for håndbok-kommentarseksjoner (docs/03-domenemodell.md §1.1.1). */
  handbokMetadata: HandbokKommentarMetadataDto | null;
}

/** Håndbok-kommentarseksjonens 1:1-metadata. 'dokumenttype': kommentar|retningslinje|instruks|handbok. 'status': under_arbeid|til_godkjenning|publisert|ma_revideres. */
export interface HandbokKommentarMetadataDto {
  dokumenttype: string;
  bindende: boolean;
  festeNiva: string;
  status: string;
  revisjonsgrunn: string | null;
  publisert: string | null;
  sistFagligEndret: string | null;
  marginord: string[];
}

export interface OpprettHandbokRequest {
  tittel: string;
}

export interface OpprettKapittelNodeRequest {
  parentNodeId: string | null;
  nummer: string;
  overskrift: string | null;
}

export interface OpprettKommentarNodeRequest {
  parentNodeId: string;
  nummer: string;
  overskrift: string | null;
  tekstHtml: string;
  dokumenttype: string;
  festeNiva: string;
  marginord: string[] | null;
}

export interface RedigerKommentarNodeRequest {
  tekstHtml: string;
  overskrift: string | null;
  dokumenttype: string;
  festeNiva: string;
  marginord: string[] | null;
}

export interface KobleLovreferanseRequest {
  tilRettskildeId: string;
  tilEid: string;
}

export interface SettRevisjonsmerkeRequest {
  revisjonsgrunn: string;
}

export interface PubliserKommentarRequest {
  godkjentAv: string | null;
}

/** 'import' = auto-fanget fra Lovdatas egne kryssreferanse-lenker (skrivebeskyttet), 'manuell' = lagt til av en bruker. */
export interface RettskildeReferanseDto {
  id: string;
  fraNodeId: string;
  tilRettskildeId: string;
  tilEid: string;
  opprinnelse: string;
  /** Posisjon (tegn-offset/lengde) for referansens synlige tekst i FraNode sin tekst — null for manuelle referanser og de fåtallige import-referansene uten entydig treff. */
  tekstStart: number | null;
  tekstLengde: number | null;
}

export interface BrukerDto {
  id: string;
  navn: string;
  virksomhetId: string;
  virksomhetNavn: string;
  rolle: string;
  /** Speiler Bruker.AltinnBrukerId != null på serveren — skiller ekte innloggede fra testbrukere. */
  erAltinnBruker: boolean;
}

/** RBAC-matrisen, docs/03-domenemodell.md §2 — se BrukerregisterTjeneste.GyldigeRoller på serveren. */
export type BrukerRolle = 'Fagansvarlig' | 'Jurist' | 'Systemforvalter' | 'Saksbehandler';

export interface OpprettBrukerRequest {
  navn: string;
  rolle: BrukerRolle;
  virksomhetId: string;
}

export interface OppdaterBrukerRequest {
  rolle: BrukerRolle;
  virksomhetId: string;
}

export interface VirksomhetDto {
  id: string;
  navn: string;
  organisasjonsnummer: string | null;
  /**
   * Gater om virksomheten skal kunne VELGES for nytt arbeid (opprett/tilordne bruker, ny kommunal
   * datasett-verdi osv.) — se `Virksomhet.Aktiv` i Entiteter.cs. Skal ALDRI filtrere bort en
   * virksomhet fra visning av allerede eksisterende innhold den eier (se `visEier` i useVirksomheter.ts).
   */
  aktiv: boolean;
  /** 'stat' | 'kommune' | 'fylkeskommune' | 'statsforvalter' | 'tingrett' | 'lagmannsrett' | 'jordskifterett' — se docs/20 §2.1. NULL for alt utenom kommune/fylkeskommune til noen fyller det inn manuelt. */
  forvaltningsniva: string | null;
  organisasjonsformKode: string | null;
  sektorkode: string | null;
  overordnetEnhetId: string | null;
  sistBrregSynkronisert: string | null;
}

/** [Ny, 2026-08-30] Opprett en virksomhet med KUN navn — se POST /api/virksomheter. */
export interface OpprettVirksomhetRequest {
  navn: string;
  overordnetEnhetId: string | null;
}

/** [Ny, 2026-08-29, docs/13-backlog.md §9] Ett søketreff fra Brønnøysundregisterets Enhetsregister —
 * se GET /api/virksomheter/brreg-sok. */
export interface BrregEnhetDto {
  organisasjonsnummer: string;
  navn: string;
  organisasjonsformKode: string | null;
  organisasjonsformBeskrivelse: string | null;
  poststed: string | null;
  erAktiv: boolean;
}

export interface VirksomhetsbegrepDto {
  id: string;
  virksomhetId: string | null;
  begrepskategori: string | null;
  virksomhetReferanseId: string | null;
  lovkildeId: string | null;
  term: string;
  definisjon: string | null;
  status: string;
}

export interface ParagrafspennParDto {
  fraEid: string;
  tilEid: string | null;
}

export interface MyndighetstildelingDto {
  id: string;
  rolleBegrepId: string;
  virksomhetId: string;
  hjemmelRettskildeId: string;
  paragrafspenn: ParagrafspennParDto[];
  vilkaar: string | null;
}

export interface VirksomhetKandidatDto {
  id: string;
  virksomhetId: string;
  rettskildeId: string;
  nodeEid: string;
  startOffset: number;
  endOffset: number;
  status: string;
  opprettetAv: string;
  opprettetTidspunkt: string;
  behandletAv: string | null;
  behandletTidspunkt: string | null;
}

/** Kravspek §4.2 pkt. 1/2 — sveip-trigger for én virksomhet. */
export interface SveipVirksomhetKandidaterRequest {
  virksomhetId: string;
}

export interface SveipVirksomhetKandidaterResultatDto {
  antallTreffFunnet: number;
  antallNyeKandidater: number;
}

/** Massegodkjenning/-avvisning (kravspek §4.2 pkt. 4) — server-side batch, per-rad-feilhåndtering. */
export interface VirksomhetKandidatBatchRequest {
  ider: string[];
}

export interface VirksomhetKandidatBatchRadDto {
  id: string;
  ok: boolean;
  feil: string | null;
  resultat: VirksomhetKandidatDto | null;
}

export interface VirksomhetKandidatBatchResultatDto {
  rader: VirksomhetKandidatBatchRadDto[];
}

/** Oppdagelsesmekanismen (docs/13-backlog.md §9) — komplementær til VirksomhetKandidatDto over. */
export interface NavnekandidatDto {
  id: string;
  foreslattTekst: string;
  kategori: 'virksomhet' | 'rolle';
  rettskildeId: string;
  nodeEid: string;
  startOffset: number;
  endOffset: number;
  status: string;
  opprettetAv: string;
  opprettetTidspunkt: string;
  behandletAv: string | null;
  behandletTidspunkt: string | null;
}

/** rettskildeId=null sveiper hele det importerte korpuset, satt snevrer inn til én rettskilde. */
export interface SveipNavnekandidaterRequest {
  rettskildeId: string | null;
}

export interface SveipNavnekandidaterResultatDto {
  antallTreffFunnet: number;
  antallNyeKandidater: number;
}

/** Ikke lenger en fast literal-union — kind-settet er konfigurasjonsstyrt (se TaggKindKonfigurasjonDto), ikke hardkodet. */
export type TaggKind = string;

/// Tekst-tag (§1.2 i domenemodellen, AK-3.3.1–3.3.4). `refId` er alltid null i byggesteg 1.
export interface TekstTaggDto {
  id: string;
  rettskildeId: string;
  nodeEid: string;
  startOffset: number;
  endOffset: number;
  quotePrefix: string;
  quoteExact: string;
  quoteSuffix: string;
  kind: TaggKind;
  refId: string | null;
  opprettetAv: string;
  /** quoteSelector-relokering ved reimport (2026-07-29) fant ikke et entydig treff — se docs/05-arkitektur-og-nfk.md §3.1. */
  kreverGjennomgang: boolean;
}

export interface OpprettTekstTaggRequest {
  nodeEid: string;
  startOffset: number;
  endOffset: number;
  quotePrefix: string;
  quoteExact: string;
  quoteSuffix: string;
  kind: TaggKind;
}

/** Konfigurerbare tag-kinds (2026-07-25, erstatter en tidligere hardkodet liste). */
export interface TaggKindKonfigurasjonDto {
  kode: string;
  navn: string;
  farge: string;
}

export interface OppdaterRettskildeMetadataRequest {
  kortnavn: string | null;
  utgiver: string | null;
  /** Utelates trygt (bindes til null server-side) — kun RettskildeDetalj-siden fyller ut disse i dag. */
  interntDokNr?: string | null;
  revisjonsnr?: string | null;
  vedtattAv?: string | null;
  vedtaksdato?: string | null;
  gyldigTil?: string | null;
  konsolidertDato?: string | null;
}

export interface ApiFeil {
  feil: string;
}

// ---------- Tjeneste/Begrep/Kodeliste (byggesteg 2, 2026-07-29, docs/03-domenemodell.md §1.3-1.5) ----------

export interface KobleTaggTilEntitetRequest {
  refId: string;
}

/** Felles statusløp for Tjeneste/Begrep/Kodeliste: utkast|under_revisjon|validert|publisert|tilbaketrukket|arkivert. */
export interface SettStatusRequest {
  status: string;
  /** Byggesteg 5 runde 1 (AK-3.10.2) — settes når status endres til "validert" etter et KI-forslag. */
  godkjentAv?: string;
}

export interface TjenesteDto {
  id: string;
  virksomhetId: string;
  tittel: string;
  beskrivelse: string | null;
  kompetentMyndighet: string | null;
  output: string | null;
  tjenestetype: string | null;
  /** Rettighet-utvidelse (2026-08-20) — ble string|null, nå en liste (postgres text[]), samme mønster som kanaler/sprak. */
  malgruppe: string[];
  kanaler: string[];
  kostnad: string | null;
  behandlingstid: string | null;
  kontaktpunkt: string | null;
  konsekvensVedBrudd: string | null;
  sprak: string[];
  status: string;
  versjon: number;
  /** Byggesteg 4 — peker til rotnoden (alltid en Regelnode) i tjenestens vilkårstre. */
  rotnodeId: string | null;
  /** Rettighet-utvidelse (2026-08-20, docs/17-forvaltningsstruktur-master-tjeneste.md) — CPSV-AP-NO cv:LifeEvent-lignende koblinger. */
  livshendelser: string[];
  losKlassifisering: string | null;
  tjenesteomrade: string | null;
  /** Rettighetstype (2026-08-20, Tjenestedetalj-runde 2) — se GYLDIGE_RETTIGHETSTYPER. */
  type: string | null;
  /** Formålsteksten (typisk lovens eget "§1 Formål") — atskilt fra beskrivelse. */
  formal: string | null;
  innhold: TjenesteInnholdInput | null;
  /** Frie, egendefinerte innholdsseksjoner utover de faste Innhold-feltene (2026-08-27,
   * Tjenestedetalj-redesignrunden) — "+ Legg til eget innholdselement". */
  egneInnholdselementer: EgetInnholdselementInput[];
}

export interface TjenesteRequest {
  tittel: string;
  beskrivelse: string | null;
  kompetentMyndighet: string | null;
  output: string | null;
  tjenestetype: string | null;
  malgruppe: string[] | null;
  kanaler: string[] | null;
  kostnad: string | null;
  behandlingstid: string | null;
  kontaktpunkt: string | null;
  konsekvensVedBrudd: string | null;
  sprak: string[] | null;
  livshendelser?: string[] | null;
  losKlassifisering?: string | null;
  tjenesteomrade?: string | null;
  type?: string | null;
  formal?: string | null;
  innhold?: TjenesteInnholdInput | null;
  egneInnholdselementer?: EgetInnholdselementInput[] | null;
}

/** Ett fritt, egendefinert innholdselement ("+ Legg til eget innholdselement",
 * Tjenestedetalj-redesignrunden 2026-08-27). `id` genereres klientside (`crypto.randomUUID()`) og
 * MÅ være stabil over lagringer — den kan være mål for en felt-nivå regelverksreferanse
 * (`felt = "egneInnholdselementer.{id}"`). */
export interface EgetInnholdselementInput {
  id: string;
  tittel: string;
  tekst: string | null;
}

// ---------- Rettighetens "innhold" (2026-08-20, Tjenestedetalj-runde 2) ----------
// Fra serveringsbevilling-modell-forslag.json sin rettigheter[].innhold — se
// TjenesteregisterTjeneste.cs for den autoritative C#-definisjonen dette speiler.

export interface TjenesteInnsenderInput {
  hvemKanSende: string[];
  innlogging: string | null;
}

export interface TjenesteInnsendingInput {
  kanal: string | null;
  etterMottak: string[];
  merknad: string | null;
}

export interface TjenesteKontaktInput {
  generelt: string | null;
  kommunenKanVeiledeOm: string[];
}

export interface TjenesteEndringerInput {
  plikt: string | null;
  eksempler: string[];
}

/** Supersett av begge modellerte rettigheters underfelt — hver rettighet fyller bare det som gjelder. */
export interface TjenesteHvaRettighetenInnebarerInput {
  innledning: string | null;
  varighet: string | null;
  plikter: string[];
  endringerIVirksomheten: TjenesteEndringerInput | null;
  kontrollOgTilsyn: string | null;
  avgrensningMerknad: string | null;
  kravTilDrift: string | null;
  tommeavtaleOgKontroll: string | null;
  rapportering: string | null;
}

export interface TjenesteInnholdInput {
  tidspunktOgFrister: string | null;
  innsenderOgTilgang: TjenesteInnsenderInput | null;
  vedlegg: string[];
  vedleggMerknad: string | null;
  opplysningerSomSkalSendesInn: string[];
  opplysningerMerknad: string | null;
  veiledningOgUtfylling: string[];
  veiledningMerknad: string | null;
  innsendingOgOppfolging: TjenesteInnsendingInput | null;
  kontaktOgHjelp: TjenesteKontaktInput | null;
  hvaRettighetenInnebarer: TjenesteHvaRettighetenInnebarerInput | null;
}

/** TjenesteregisterTjeneste.GyldigeRettighetstyper på serveren. */
export const GYLDIGE_RETTIGHETSTYPER = ['myndighetsutovelse', 'ytelse', 'infrastruktur', 'veiledning', 'medvirkning'] as const;

// ---------- Handling (2026-08-20) — se HandlingEntitet i RegelIde.Data for begrunnelse ----------

/** Et lovsitat i kortform — 'lov' er et kortnavn (f.eks. "serveringsloven"), ikke en full tittel/Eli. */
export interface HandlingHjemmelInput {
  lov: string;
  henvisning: string | null;
}

export interface HandlingKanalInput {
  kanal: string;
  adresse: string | null;
}

export interface HandlingBehandlingstidInput {
  frist: string | null;
  hjemmel: HandlingHjemmelInput | null;
}

export interface HandlingKostnadInput {
  belop: string | null;
  hjemmel: HandlingHjemmelInput[];
}

export interface HandlingVedleggInput {
  navn: string;
  kategori: string | null;
  hjemmel: HandlingHjemmelInput | null;
}

export interface HandlingVeiledningstekstInput {
  overskrift: string;
  innhold: string | null;
  hjemmel: HandlingHjemmelInput | null;
}

export interface HandlingArsakInput {
  arsak: string;
  hjemmel: HandlingHjemmelInput;
}

export interface HandlingBevisKanalInput {
  kanal: string;
}

export interface HandlingResultatInput {
  hva: string | null;
  bevisKanaler: HandlingBevisKanalInput[];
}

/** Handling — en konkret, tidsavgrenset interaksjon knyttet til en Rettighet (Tjeneste). Se docs/17/18 + planen for begrunnelsen. */
export interface HandlingDto {
  id: string;
  tjenesteId: string;
  navn: string;
  handlingstype: string;
  bruksomraade: string | null;
  utfortAv: string | null;
  /** Override av Tjeneste.rotnodeId for nettopp DENNE handlingens saksbehandling — mangler den, gjelder rettighetens. */
  rotnodeId: string | null;
  /** Hvilket høstet Oppgaveregister-skjema denne handlingen ble seedet fra (2026-08-22, OppgaveregisterHandlingSeed) — null for håndskrevne handlinger. */
  eksternKildeId: string | null;
  kanaler: HandlingKanalInput[];
  behandlingstid: HandlingBehandlingstidInput;
  kostnad: HandlingKostnadInput;
  vedlegg: HandlingVedleggInput[];
  veiledningstekst: HandlingVeiledningstekstInput[];
  arsaker: HandlingArsakInput[];
  resultat: HandlingResultatInput;
  merknad: string | null;
  status: string;
  versjon: number;
}

/** Én rad fra GET /api/handlinger (toppnivå-siden, 2026-08-22) — Handlingen selv pluss eiende tjenestes
 * tittel og virksomhetId, slik at HandlingerListe.tsx ikke må gjøre ett kall per tjeneste. */
export interface HandlingMedTjenesteDto {
  handling: HandlingDto;
  tjenesteTittel: string;
  virksomhetId: string;
}

/** Forespørsel for POST /api/tjenester/{id}/handlinger/koble (2026-08-27) — kobler en
 * EKSISTERENDE handling (som virksomheten selv eier) sekundært til denne tjenesten. */
export interface KobleHandlingRequest {
  handlingId: string;
}

/** Én sekundær handlings-kobling (2026-08-27) — se HandlingTjenesteEntitet på serveren. */
export interface HandlingTjenesteDto {
  id: string;
  handlingId: string;
  tjenesteId: string;
}

export interface HandlingRequest {
  navn: string;
  handlingstype: string;
  bruksomraade: string | null;
  utfortAv: string | null;
  kanaler: HandlingKanalInput[] | null;
  behandlingstid: HandlingBehandlingstidInput | null;
  kostnad: HandlingKostnadInput | null;
  vedlegg: HandlingVedleggInput[] | null;
  veiledningstekst: HandlingVeiledningstekstInput[] | null;
  arsaker: HandlingArsakInput[] | null;
  resultat: HandlingResultatInput | null;
  merknad: string | null;
}

/** HandlingregisterTjeneste.GyldigeHandlingstyper på serveren — hold synkron, ikke koblet til en KodelisteEntitet i v1. */
export const GYLDIGE_HANDLINGSTYPER = [
  'soke', 'endre', 'si_opp', 'melde', 'registrere', 'rapportere', 'ettersende_dokumentasjon',
  'klage', 'gi_samtykke', 'trekke_samtykke', 'be_om_innsyn', 'bestille', 'kontrolleres', 'avslutte', 'annet',
] as const;

/** HandlingregisterTjeneste.GyldigeUtfortAv på serveren. */
export const GYLDIGE_UTFORT_AV = ['soker', 'forvaltning', 'tredjepart'] as const;

export interface TjenesteRegelverksreferanseDto {
  id: string;
  tjenesteId: string;
  tilRettskildeId: string;
  tilEid: string;
  /** null = gjelder hele tjenesten (Regelverksreferanser-fanen). Satt = knyttet til ETT bestemt
   * felt i Innhold-fanen — se feltnøkkel-konvensjonen i api/tjenesteFelt.ts. */
  felt: string | null;
}

/** Samme rolle for en Handling som TjenesteRegelverksreferanseDto har for en Tjeneste (2026-08-22,
 * se OppgaveregisterHandlingSeed) — kun lesing i UI-et ennå, ingen koble til/fjern-endepunkt finnes. */
export interface HandlingRegelverksreferanseDto {
  id: string;
  handlingId: string;
  tilRettskildeId: string;
  tilEid: string;
}

/** Håndbok-nivå rettskildeomfang (docs/12-fasit-handbok-leveranse.md, 2026-07-31). */
export interface HandbokRettskildeomfangDto {
  id: string;
  handbokId: string;
  tilRettskildeId: string;
}

// ---------- Nettsider / Brukerveiledning (docs/15-handbok-dokumentgraf-notat.md §3.1/§3.2/§3.4) ----------
// ---------- Punkt 8 (avklaringsrunde 2026-08-13): full konvergens — en nettside ER nå en    ----------
// ---------- ordinær RettskildeDetalj/RettskildeNodeDto (Kildetype="Brukerveiledning"). Disse  ----------
// ---------- to typene dekker kun det som IKKE allerede finnes der: §3.4-stier og §3.2-lenker. ----------

/** GET /api/rettskilder/{id}/stier — §3.4, kun ikke-tom for Kildetype="Brukerveiledning". */
export interface NettsideStiDto {
  sti: string;
  stiType: string;
}

/**
 * GET /api/rettskilder/{id}/nettside-lenker — én utgående lenke (§3.2) fra en Brukerveilednings
 * side-node. Til…-feltene er null når lenken er uløst (ekstern, eller et ikke-håndtert Lovdata-
 * URL-format) — ÉN målfamilie nå, ikke to (punkt 8 kollapset "intern nettside-lenke" og
 * "PDF-omtale-lenke til håndbok" til nøyaktig samme oppløsning, siden begge mål nå ER RettskildeEntitet).
 */
export interface NettsideLenkeMedMalDto {
  id: string;
  type: string;
  raaHref: string;
  ankerTekst: string | null;
  tilRettskildeId: string | null;
  tilRettskildeTittel: string | null;
  tilRettskildeEli: string | null;
}

export interface KobleRegelverksreferanseRequest {
  tilRettskildeId: string;
  tilEid: string;
  felt?: string | null;
}

/**
 * [Ny, 2026-08-28, import-wizard-runden] POST /api/import/{malVirksomhetId}/rettigheter — ett
 * allerede menneske-bekreftet element fra en modelleksport-JSON. `tjeneste`/`handlinger` gjenbruker
 * EKSAKT samme request-formene som de vanlige skriveendepunktene; `regelverksreferanser` peker
 * allerede på ekte rettskilde-noder (wizarden har løst navn→FK FØR dette sendes). Avhengigheter
 * sendes IKKE her, se `ImportWizard.tsx`.
 */
export interface ImportRettighetRequest {
  tjeneste: TjenesteRequest;
  handlinger: HandlingRequest[];
  regelverksreferanser: KobleRegelverksreferanseRequest[];
}

/** Hendelse (docs/03-domenemodell.md §1.5, docs/13-backlog.md §2.1). 'type': generell|livshendelse|virksomhetshendelse. */
export interface HendelseDto {
  id: string;
  virksomhetId: string | null;
  navn: string;
  type: string;
  beskrivelse: string | null;
}

export interface HendelseRequest {
  navn: string;
  type: string;
  beskrivelse: string | null;
}

export interface KobleHendelseRequest {
  hendelseId: string;
}

/**
 * Én tjenesteavhengighet sett fra den spurte tjenestens ståsted — retning+visningstekst er ferdig
 * beregnet server-side. Motparten er ENTEN en ekte tjeneste (`motpartTjenesteId` satt,
 * `motpartOrganisasjonsnummer` null) ELLER en ekstern plassholder (omvendt) — `motpartNavn` er alltid
 * populert uansett hvilket, slik at en visning kan rendres med kun én null-sjekk (på
 * `motpartTjenesteId`, for å avgjøre om en `/tjenester/:id`-lenke gir mening — en ekstern referanse har
 * ingen ekte Tjeneste-rad å navigere til).
 */
export interface TjenesteavhengighetDto {
  id: string;
  rel: string;
  retning: 'fra' | 'til';
  visningstekst: string;
  motpartTjenesteId: string | null;
  motpartOrganisasjonsnummer: string | null;
  motpartNavn: string;
  motpartUrl: string | null;
  hendelseId: string | null;
  hendelseNavn: string | null;
  beskrivelse: string | null;
}

/**
 * `tilTjenesteId` ELLER (`tilOrganisasjonsnummer` + `tilNavn`, valgfritt `tilUrl`) — nøyaktig ett av de
 * to målene må oppgis.
 */
export interface TjenesteavhengighetRequest {
  tilTjenesteId: string | null;
  rel: string;
  hendelseId: string | null;
  beskrivelse: string | null;
  tilOrganisasjonsnummer?: string | null;
  tilNavn?: string | null;
  tilUrl?: string | null;
}

/** Ett cross-tenant søketreff (GET /api/tjenester/sok-tverr-tenant) — kun publiserte tjenester fra ALLE virksomheter. */
export interface TjenesteTverrTenantTreffDto {
  id: string;
  tittel: string;
  beskrivelse: string | null;
  virksomhetId: string;
  virksomhetNavn: string;
}

/** [Ny, 2026-08-28] Én node i en tjenestereise-graf (GET /api/tjenester/{id}/avhengighetsgraf). */
export interface GrafNodeDto {
  id: string;
  navn: string;
  erHandling: boolean;
  type: string | null;
  kompetentMyndighet: string | null;
  livshendelser: string[];
  status: string | null;
}

/** `erHandlingTilhorighet` = ikke en ekte avhengighet, kun tjeneste→egen-handling-tilhørighet. */
export interface GrafKantDto {
  fraId: string;
  tilId: string;
  rel: string;
  erHandlingTilhorighet: boolean;
}

export interface AvhengighetsgrafDto {
  noder: GrafNodeDto[];
  kanter: GrafKantDto[];
}

/** SKOS-begrep (docs/03-domenemodell.md §1.3). 'begrepstype': faktabegrep|handlingsbegrep. */
export interface BegrepDto {
  id: string;
  virksomhetId: string;
  term: string;
  definisjon: string;
  lovreferanseEid: string | null;
  gjelderFor: string[];
  kodelisteReferanseId: string | null;
  skosUrl: string | null;
  begrepstype: string;
  status: string;
  versjon: number;
}

export interface BegrepRequest {
  term: string;
  definisjon: string;
  lovreferanseEid: string | null;
  gjelderFor: string[] | null;
  kodelisteReferanseId: string | null;
  skosUrl: string | null;
  begrepstype: string;
}

export interface KodelisteKodeDto {
  id: string;
  kode: string;
  term: string;
  definisjon: string | null;
  gyldigFra: string | null;
  gyldigTil: string | null;
  erstattesAvKodeId: string | null;
}

/** Kodeliste/verdidomene (docs/03-domenemodell.md §1.4). 'type': juridisk|teknisk|ekstern-referanse. */
export interface KodelisteDto {
  id: string;
  virksomhetId: string | null;
  kode: string;
  navn: string;
  type: string;
  juridiskGrunnlagEid: string | null;
  eksternKildeUri: string | null;
  eksternKildeVersjon: string | null;
  status: string;
  versjon: number;
  koder: KodelisteKodeDto[];
}

export interface KodelisteRequest {
  kode: string;
  navn: string;
  type: string;
  virksomhetId: string | null;
  juridiskGrunnlagEid: string | null;
  eksternKildeUri: string | null;
  eksternKildeVersjon: string | null;
}

export interface LeggTilKodeRequest {
  kode: string;
  term: string;
  definisjon: string | null;
  gyldigFra: string | null;
  gyldigTil: string | null;
}

// ---------- Vilkårstre (byggesteg 4 runde 1, docs/03-domenemodell.md §1.6/§1.8-1.10) ----------

export interface JuridiskGrunnlagInput {
  kilde: string;
  eId: string;
}

/** presedensreferanse er ubrukelig til byggesteg 3 (Presedensregister) finnes. */
export interface SkjonnsmomentInput {
  navn: string;
  beskrivelse: string | null;
  presedensreferanse: string | null;
}

export interface ProveniensDto {
  id: string;
  entitetType: string;
  entitetId: string;
  endretAv: string;
  dato: string;
  handling: string;
  godkjentAv: string | null;
}

/** Datasett (§1.6), minimal — full skjerm er byggesteg 6. Kun lesing i denne runden, seedet. */
export interface DatasettDto {
  id: string;
  virksomhetId: string;
  felt: string;
  prop: string;
  dtype: string;
  type: string;
  kilde: string | null;
  kodelisteId: string | null;
  grunnlag: string | null;
  lagring: string | null;
  mottakere: string[];
  bruk: string | null;
}

/** Vilkår (§1.8) — bladnode i vilkårstreet. 'vilkarstype': formell|materiell. 'vurderingstype': regelbasert|skjonnsbasert|hybrid. */
export interface VilkarDto {
  id: string;
  virksomhetId: string;
  /** Hvilken tjeneste dette vilkåret er identifisert for — atskilt fra om det er koblet inn i vilkårstreet. */
  tjenesteId: string | null;
  tittel: string;
  beskrivelse: string | null;
  generiskMal: string | null;
  vilkarstype: string;
  gjelderRolle: string | null;
  juridiskGrunnlag: JuridiskGrunnlagInput[];
  begrepId: string | null;
  vurderingstype: string;
  parametreJson: string;
  skjonnsgrunnlagBegrepId: string | null;
  skjonnsmomenter: SkjonnsmomentInput[];
  kreverDokumentasjon: boolean;
  eskaleringsrolle: string | null;
  veiledningTilBruker: string | null;
  veiledningTilSaksbehandler: string | null;
  /** Lett annotering (docs/10-rules-as-code-landskap.md) — dette er egentlig en beregnet verdi, ikke et ekte testbart vilkår. */
  erFormel: boolean;
  formelBeskrivelse: string | null;
  status: string;
  versjon: number;
}

export interface VilkarRequest {
  tittel: string;
  beskrivelse: string | null;
  generiskMal: string | null;
  vilkarstype: string;
  gjelderRolle: string | null;
  juridiskGrunnlag: JuridiskGrunnlagInput[] | null;
  begrepId: string | null;
  vurderingstype: string;
  parametreJson: string | null;
  skjonnsgrunnlagBegrepId: string | null;
  skjonnsmomenter: SkjonnsmomentInput[] | null;
  kreverDokumentasjon: boolean;
  eskaleringsrolle: string | null;
  veiledningTilBruker: string | null;
  veiledningTilSaksbehandler: string | null;
  erFormel: boolean;
  formelBeskrivelse: string | null;
  tjenesteId: string | null;
}

export interface LeggTilVilkarInputRequest {
  datasettId: string;
}

/** Regelnode (§1.9) — komposisjonsnode. 'barnOperator': OG|ELLER|IKKE. */
export interface RegelnodeDto {
  id: string;
  virksomhetId: string;
  tittel: string;
  beskrivelse: string | null;
  generiskMal: string | null;
  barnOperator: string;
  utdataNavn: string;
  utdataType: string;
  erRotnode: boolean;
  juridiskGrunnlag: JuridiskGrunnlagInput[];
  innvilgelseTekst: string | null;
  avslagTekst: string | null;
  status: string;
  versjon: number;
}

export interface RegelnodeRequest {
  tittel: string;
  beskrivelse: string | null;
  generiskMal: string | null;
  barnOperator: string;
  utdataNavn: string;
  utdataType: string;
  erRotnode: boolean;
  juridiskGrunnlag: JuridiskGrunnlagInput[] | null;
  innvilgelseTekst: string | null;
  avslagTekst: string | null;
}

/** 'barnType': vilkar|regelnode. */
export interface RegelnodeBarnDto {
  id: string;
  regelnodeId: string;
  barnType: string;
  barnId: string;
}

export interface KobleBarnRequest {
  barnType: string;
  barnId: string;
}

export interface SettOperatorRequest {
  barnOperator: string;
}

/** Unntak (§1.10). 'betingelseType': vilkar|regelnode. */
export interface UnntakDto {
  id: string;
  virksomhetId: string;
  tittel: string;
  beskrivelse: string | null;
  gjelderRegelId: string;
  betingelseType: string;
  betingelseId: string;
  juridiskGrunnlag: JuridiskGrunnlagInput[];
  status: string;
  versjon: number;
}

export interface OpprettUnntakRequest {
  tittel: string;
  beskrivelse: string | null;
  gjelderRegelId: string;
  betingelseType: string;
  betingelseId: string;
  juridiskGrunnlag: JuridiskGrunnlagInput[] | null;
}

export interface OppdaterUnntakRequest {
  tittel: string;
  beskrivelse: string | null;
  juridiskGrunnlag: JuridiskGrunnlagInput[] | null;
}

export interface SettRotnodeRequest {
  regelnodeId: string;
}

/** Kommunal/nasjonal parameterverdi (docs/12-fasit-handbok-leveranse.md dimensjon C). virksomhetId null = nasjonal standardverdi. */
export interface DatasettVerdiDto {
  id: string;
  datasettId: string;
  virksomhetId: string | null;
  verdiJson: string;
  kilde: string | null;
}

export interface SettDatasettVerdiRequest {
  virksomhetId: string | null;
  verdiJson: string;
  kilde: string | null;
}

/** Veiledningskommentar på en vilkårstre-node (docs/12-fasit-handbok-leveranse.md "Hovedfunn" + dimensjon A). 'dokumenttype': kommentar|hjemmel|praktisk-rad|sjekkliste. */
export interface VilkarstreKommentarDto {
  id: string;
  malType: string;
  malId: string;
  dokumenttype: string;
  tekstHtml: string;
  rekkefolge: number;
}

export interface OpprettVilkarstreKommentarRequest {
  malType: string;
  malId: string;
  dokumenttype: string;
  tekstHtml: string;
}

export interface OppdaterVilkarstreKommentarRequest {
  dokumenttype: string;
  tekstHtml: string;
}

export interface VeiledningDatasettVerdiDto {
  datasettId: string;
  felt: string;
  prop: string;
  verdiJson: string;
  kilde: string | null;
  erStandardverdi: boolean;
}

export interface VeiledningUnntakDto {
  id: string;
  tittel: string;
  beskrivelse: string | null;
  betingelseType: string;
  betingelseId: string;
  betingelseTittel: string;
  kommentarer: VilkarstreKommentarDto[];
}

/** Én node i veiledningstreet — 'type': vilkar|regelnode avgjør hvilke felt som er satt. */
export interface VeiledningNodeDto {
  id: string;
  type: string;
  tittel: string;
  beskrivelse: string | null;
  vilkarstype: string | null;
  vurderingstype: string | null;
  skjonnsmomenter: SkjonnsmomentInput[];
  barnOperator: string | null;
  juridiskGrunnlag: JuridiskGrunnlagInput[];
  inputDatasettVerdier: VeiledningDatasettVerdiDto[];
  kommentarer: VilkarstreKommentarDto[];
  barn: VeiledningNodeDto[];
  unntak: VeiledningUnntakDto[];
}

export interface VeiledningDto {
  tjenesteId: string;
  tjenesteTittel: string;
  virksomhetId: string | null;
  rot: VeiledningNodeDto;
}

/** Motsatt retning av tjenestens regelverksreferanser — hvilke tjenester som refererer denne rettskilden. */
export interface TjenesteReferanseDto {
  tjenesteId: string;
  tjenesteTittel: string;
  tilEid: string;
}

/** Motsatt retning av RettskildeReferanseDto — punkt 6/9, andre dokumenters (håndbok/rundskriv) noder som refererer denne rettskilden. */
export interface DokumentReferanseDto {
  dokumentId: string;
  dokumentTittel: string;
  fraNodeEid: string;
  fraNodeOverskrift: string | null;
  tilEid: string;
}

// ---------- Byggesteg 5 runde 1 — Kunnskapsbibliotek + AI-forslag (docs/06-veikart.md) ----------

/** Kunnskapsbibliotek-lenke — kun brukt av «Identifiser tjenester»-agenten. */
export interface KunnskapsbibliotekLenkeDto {
  id: string;
  virksomhetId: string;
  url: string;
  beskrivelse: string | null;
  opprettetAv: string;
  opprettetTidspunkt: string;
}

export interface LeggTilLenkeRequest {
  url: string;
  beskrivelse?: string | null;
}

/** Kunnskapsbibliotek-fil (byggesteg 5 runde 2) — inneholder aldri rå fil-bytes, kun utvunnet tekst. */
export interface KunnskapsbibliotekFilDto {
  id: string;
  virksomhetId: string;
  filnavn: string;
  tittel: string | null;
  filtype: string;
  utvunnetTekst: string;
  opprettetAv: string;
  opprettetTidspunkt: string;
}

/** Ett søketreff i Lovdata-katalogen (byggesteg 5 runde 2) — kun metadata, ingen full tekst. */
export interface LovdataKatalogTreffDto {
  datokode: string;
  tittel: string;
  type: string;
}

/**
 * Siste kjente importforsøk for ETT KJENT Lovdata-dokument (fra bulk-arkivet) — se
 * LovdataImportstatusEntitet på serveren. `importert=false` betyr at LovdataFullimportTjeneste (eller
 * en enkeltimport via api.importerFraLovdata) ikke klarte å AKN-konvertere dokumentet — `feilmelding`
 * er da satt til den faktiske unntaksmeldingen, til triage/case-by-case-vurdering (docs/13-backlog.md §6).
 */
export interface LovdataImportstatusDto {
  datokode: string;
  type: string;
  tittel: string | null;
  eli: string;
  importert: boolean;
  rettskildeId: string | null;
  feilmelding: string | null;
  sistForsoktTidspunkt: string;
}

/**
 * `omfang` (handlingsforslag-ki-omfang-runden) brukes KUN av POST /api/tjenester/forslag/kjor —
 * "tjeneste" (default, uendret oppførsel) eller "full" (Tjeneste + Handlinger i samme kall).
 * "handling" hører til det EGNE endepunktet POST /api/tjenester/{id}/handlinger/forslag/kjor
 * (se KjorHandlingsforslagRequest) — det krever en eksisterende tjeneste, som ikke finnes her.
 */
export interface KjorForslagRequest {
  rettskildeIder: string[];
  omfang?: 'tjeneste' | 'full';
}

/** Forespørsel for POST /api/tjenester/{id}/handlinger/forslag/kjor (omfang "handling") —
 * {id} i ruten ER tjenesten handlingene skal foreslås for. */
export interface KjorHandlingsforslagRequest {
  rettskildeIder: string[];
}

/** Ett element i svaret fra omfang "full" — tjenesten pluss handlingene KI-en foreslo under den. */
export interface TjenesteMedHandlingerDto {
  tjeneste: TjenesteDto;
  handlinger: HandlingDto[];
}

/**
 * Svar fra POST .../forslag/kjor (byggesteg 5 runde 3) — token-forbruk fra KI-kallet (null hvis
 * leverandøren ikke rapporterer det) og en eksplisitt melding når agenten svarte, men fant null
 * forslag i valgt kontekst — skiller "kjørte, fant ingenting" fra stillhet som ellers ser ut som en feil.
 */
export interface KjorForslagResponsDto<T> {
  forslag: T[];
  inputTokens: number | null;
  outputTokens: number | null;
  melding: string | null;
}

/** Kø-visning for «Identifiser begrep» — beriker BegrepDto med proveniens fra AI-forslaget. */
export interface BegrepsforslagDto {
  begrep: BegrepDto;
  aiForslagVersjon: string | null;
  foreslattTidspunkt: string;
  kildeReferanserJson: string | null;
}

/**
 * Kø-visning for «Identifiser tjenester» — beriker TjenesteDto med proveniens fra forslaget.
 * `aiForslagVersjon` satt = KI-forslag; `foreslattAvVirksomhetNavn` satt = tverr-virksomhet
 * import-forslag (2026-08-28). Nøyaktig én av de to er satt.
 */
export interface TjenesteforslagDto {
  tjeneste: TjenesteDto;
  aiForslagVersjon: string | null;
  foreslattTidspunkt: string;
  kildeReferanserJson: string | null;
  foreslattAvVirksomhetNavn: string | null;
}

/** [Ny, 2026-08-29] Motstykket til TjenesteforslagDto — tjenester DENNE virksomheten selv har
 * foreslått til en ANNEN virksomhet, fortsatt ubehandlet der. Se GET /api/tjenester/foreslatt-av-meg. */
export interface MittForslagDto {
  tjeneste: TjenesteDto;
  foreslattTidspunkt: string;
  malVirksomhetNavn: string;
}

/**
 * Innlogget brukers foretrukne fanerekkefølge/-synlighet og accordion-rekkefølge/åpen-tilstand på
 * Tjeneste-siden (2026-08-27, Tjenestedetalj-redesignrunden) — GET/PUT /api/brukere/meg/tjeneste-visning.
 * Per BRUKER, ikke per tjeneste — se BrukerVisningsinnstillingEntitet på serveren. Egendefinerte
 * innholdselementer har sin egen rekkefølge/åpen-tilstand lagret på selve tjenesten
 * (TjenesteDto.egneInnholdselementer), ikke her.
 */
export interface VisningsinnstillingInput {
  /** De 7 faste fane-nøklene, ordnet. "oversikt" er alltid først og er ALDRI med her. */
  seksjonsrekkefolge: string[];
  /** Delmengde av samme 7 nøkler — skjulte faner. */
  skjulteSeksjoner: string[];
  /** De 9 faste accordion-nøklene i Innhold-fanen, ordnet. */
  accordionRekkefolge: string[];
  /** Åpen/lukket per fast accordion-nøkkel. */
  accordionApne: Record<string, boolean>;
}
