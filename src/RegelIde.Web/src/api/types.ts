// Speiler DTO-ene i RegelIde.Api/Dtos.cs + GjeldendeBrukerTjeneste.cs nøyaktig.

export interface RettskildeSammendrag {
  id: string;
  virksomhetId: string | null;
  eli: string | null;
  tittel: string;
  kortnavn: string | null;
  kildetype: string;
  /** ["Kommunal- og distriktsdepartementet", ...] — flere ved delt departementsansvar (fler-verdi-
   * departement, 2026-09-04). NULL for alt som ikke er Lovdata-importert Lov/Forskrift. */
  ansvarligDepartement: string[] | null;
  /** Header-nivå «irrelevant for regel-ide»-markering (2026-08-30). Kompakt badge-verdi. */
  erIrrelevant: boolean;
  /** [Ny, 2026-09-02, issue #114] Fritekst — hvorfor markert irrelevant. Kun meningsfullt når
   * erIrrelevant er true. Flyttet hit fra kun RettskildeDetalj slik at «Utenfor korpuset»-fanen i
   * RettskilderListe.tsx kan vise begrunnelsen direkte i tabellraden. */
  irrelevantKommentar: string | null;
  /** [Ny, 2026-09-02] Rå, utrunket Lovdata-"dateInForce"-streng. Populert KUN for Lov/Forskrift — NULL
   * for andre kildetyper er forventet fravær av data, IKKE "ikke i kraft" (se RettskilderListe.tsx). */
  ikrafttredelseRaa: string | null;
  /** [Ny, issue #395] 'konsolidert' (gjeldende-uttrekket) | 'kunngjort' (Norsk Lovtidend avd. I) | null (ukjent). */
  kildeform?: string | null;
  /** [Ny, issue #395] Disposisjonstypene (oppretting, navneendring, …, regulering). Tom liste = uklassifisert. Utelatt (undefined/null)
   * i batch-oppslagene, som ikke henter dem — det er ikke det samme som uklassifisert. */
  disposisjonstyper?: string[] | null;
}

/** [Ny, fler-verdi-departement, 2026-09-04] Ett departement fra RettskildeDetalj.ansvarligDepartement
 * sammen med dets løste Virksomhet-id (eller null). */
export interface AnsvarligDepartementLenke {
  departement: string;
  virksomhetId: string | null;
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
  /** Rå strenger fra Lovdatas "ministry"-metadatafelt (departement-virksomhet-lenke, 2026-08-30) — FLERE
   * ved delt departementsansvar (fler-verdi-departement, 2026-09-04). NULL for alt som ikke er
   * Lov/Forskrift importert fra Lovdata. */
  ansvarligDepartement: string[] | null;
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
  /** [ENDRET, fler-verdi-departement, 2026-09-04] Ett element PER departement i ansvarligDepartement
   * (samme rekkefølge) — erstatter den tidligere enkeltstående ansvarligDepartementVirksomhetId, som kun
   * ga mening da feltet var én streng. virksomhetId er null når nettopp DET departementet ikke finnes
   * som egen Virksomhet-rad ("ingen gjettet fallback": vises da som ren tekst, ikke en lenke). */
  ansvarligDepartementLenker: AnsvarligDepartementLenke[];
  /** Header-nivå «irrelevant for regel-ide»-markering (2026-08-30) — menneskelig, eksplisitt valg, aldri utledet fra tittelmønster. */
  erIrrelevant: boolean;
  /** Fritekst — hvorfor. Kun meningsfullt når erIrrelevant er true, men slettes IKKE automatisk om markeringen fjernes igjen. */
  irrelevantKommentar: string | null;
  /** [Ny, 2026-09-02] Rå, utrunkert verdi av Lovdatas dateInForce-header-felt, ved siden av den trunkerte ikrafttredelse. NULL for alt som ikke er Lovdata-importert. */
  ikrafttredelseRaa: string | null;
  /** [Ny, 2026-09-02] Rå, utrunkert verdi av Lovdatas lastChangeInForce-header-felt. */
  konsolidertDatoRaa: string | null;
  /** [Ny, 2026-09-02] Lovdatas header-felt "Sist endret ved" — rå tekst, ikke en strukturert kobling. */
  sistEndretVed: string | null;
  // ---------- [Ny, 2026-09-03, issue #127] De resterende 10 av 15 bekreftede Lovdata header-
  // metadatafelt. NULL for alt som ikke er Lovdata-importert Lov/Forskrift, samme mønster som
  // ansvarligDepartement over.
  /** Header-feltet "Kunngjort" (dateOfPublication). */
  kunngjort: string | null;
  /** Header-feltet "Rettsområde" (legalArea) — kan ha flere verdier, skilt med ", ". */
  rettsomrade: string | null;
  /** Header-feltet "EU/EØS-henvisning" (eeaReferences) — kan ha flere linjer, skilt med linjeskift. */
  euEosHenvisning: string | null;
  /** Header-feltet "DokumentID" (dokid) — Lovdatas interne dokument-id, distinkt fra eli. */
  dokumentId: string | null;
  /** Header-feltet "RefID" (refid). */
  refId: string | null;
  /** Header-feltet "Gjelder for" (appliesTo, typisk "Norge"). */
  gjelderFor: string | null;
  /** Header-feltet "Etat" (subunit) — kan ha flere verdier, skilt med ", ". */
  etat: string | null;
  /** Header-feltet "Publisert i" (publishedIn). */
  publisertI: string | null;
  /** Header-feltet "Annet om dokumentet" (miscInformation) — kan ha flere linjer, skilt med linjeskift. */
  annetOmDokumentet: string | null;
  /** Header-feltet "Siste rettelse" (lastupdated). */
  sisteRettelse: string | null;
  /** [Ny, 2026-09-03, issue #131] true når RettskildeEntitet.Innhold (rå kilde-HTML) er lagret — styrer om «Vis kilde»-knappen vises. */
  harKilde: boolean;
  /** [Ny, issue #215] Organet som FASTSATTE forskriften, utledet av hjemmelslinja. null når kilden
   * ikke har frasen. Se FastsattAvDto — dette er IKKE virksomhetId (eierskap), og det står ved siden
   * av ansvarligDepartement, ikke i stedet for. */
  fastsattAv: FastsattAvDto | null;
}

/** [Ny, issue #215] `tekst` er frasen som den STÅR i kilden («styret ved …») og er det som vises.
 * `organnavn` er navnet som slås opp i katalogen (institusjonen, ikke styret) — null for «kgl.res»,
 * siden Kongen i statsråd er et gruppebegrep og ikke en virksomhet. `virksomhetId` løses ved lesing
 * og er null når navnet ikke gav ETT entydig treff; teksten vises da uten lenke. */
export interface FastsattAvDto {
  tekst: string;
  organnavn: string | null;
  virksomhetId: string | null;
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

/**
 * Hjemmel-referanse (2026-08-30) — header-metadatafeltet `<dt class="basedOn">Hjemmel</dt>`: hvilken
 * paragraf i hvilken lov denne rettskilden (typisk en forskrift) er hjemlet i. `hjemmelEid` er i SAMME
 * eId-format som en vanlig paragraf-node (se eidLenker.ts) — gjenbruk `rettskildeLenke`/
 * `finnRettskildeForEid` derfra i stedet for å bygge en egen lenke-oppslagslogikk.
 * `hjemmelRettskildeId` peker ALLTID til en ekte rad (primær eller en referanse-stub som ennå ikke er
 * synlig i den åpne rettskilde-lista) — «ingen gjettet fallback»: vis kun som lenke når
 * `finnRettskildeForEid`/`rettskildeLenke` faktisk finner et treff i den allerede hentede listen.
 */
export interface RettskildeHjemmelDto {
  id: string;
  /** Paragraf-eId — eller, fra issue #217, en ledd-eId («…/§13-1/ledd-4») når kilden presiserer. */
  hjemmelEid: string;
  hjemmelRettskildeId: string;
  sorteringsrekkefolge: number;
  /** [Ny, issue #217] Presiseringen kilden oppgav som IKKE kunne løses til en node («bokstav-a»).
   * Skal VISES, ikke skjules — se backend-kommentaren (RettskildeHjemmelEntitet.UlostPresisering). */
  ulostPresisering: string | null;
}

/** Motsatt retning av RettskildeHjemmelDto — én forskrift som er hjemlet i DENNE loven. */
export interface RettskildeHjemletForDto {
  forskriftId: string;
  forskriftTittel: string;
  hjemmelEid: string;
}

/**
 * [Ny, issue #193] Bulk-variant av RettskildeHjemletForDto — én rad per (forskrift, lov)-
 * hjemmelrelasjon for HELE korpuset i ett kall, brukt av RettskilderListe.tsx sin
 * departement→lov→forskrift-fane til å bygge hierarkiet uten ett /hjemmel-for-kall per lov.
 * Ingen tittel/sorteringsrekkefølge — slå forskriftId/lovId opp mot den allerede hentede
 * RettskildeSammendrag-lista.
 */
export interface RettskildeHjemmelRelasjonDto {
  forskriftId: string;
  lovId: string;
  hjemmelEid: string;
}

/**
 * [Ny, navneform-kjede-runden, 2026-09-08] Én forekomst av en navneform i en rettskildetekst — altså
 * én `virksomhet`-tagg som peker på navneformen. Svarer på Johanns «where used»: hvor er navneformen
 * faktisk brukt?
 *
 * Flat form (navneformens `term`/`navneformgrunn` gjentas per forekomst), samme minimalitetsprinsipp
 * som `RettskildeHjemmelRelasjonDto` — grupper selv på `navneformId`.
 */
export interface VirksomhetNavneformForekomstDto {
  navneformId: string;
  term: string;
  navneformgrunn: Navneformgrunn | null;
  rettskildeId: string;
  rettskildeTittel: string;
  nodeEid: string;
  quoteExact: string;
  /** Tegnposisjonen i noden. RADENS IDENTITET — samme navneform kan være tagget to steder i samme
   * ledd, så `(rettskildeId, nodeEid)` alene er IKKE unikt og duger ikke som React-`key`. Selve
   * lenken peker på noden; offsetene skiller forekomstene fra hverandre. */
  startOffset: number;
  endOffset: number;
}

/** Én myndighetstildeling med GRUPPEBEGREPET navngitt, nøklet på tildelingens egen id — slå den inn i
 * myndighetstildelings-tabellen, som viser tildelingene men ikke hvilken gruppe de gjelder. */
export interface VirksomhetGruppetildelingDto {
  tildelingId: string;
  gruppeBegrepId: string;
  gruppeTerm: string;
  /** [Ny, issue #310] Nodetypen til begrepet tildelingen gjelder (klasse/rolle/omrade/organ/gruppe). */
  gruppeBegrepskategori: string | null;
}

/** `GET /api/virksomheter/{id}/where-used` — ETT kall for alle virksomhetens navneformer (ikke ett per
 * navneform). Virksomhetsrelasjoner er bevisst IKKE med: de vises allerede i sin helhet i egen
 * seksjon, se `VirksomhetWhereUsedTjeneste` sin klassekommentar. */
export interface VirksomhetWhereUsedDto {
  navneformForekomster: VirksomhetNavneformForekomstDto[];
  gruppetildelinger: VirksomhetGruppetildelingDto[];
}

/**
 * Endring-referanse (2026-09-02) — header-metadatafeltet `<dt class="changesToDocuments">Endrer</dt>`:
 * hvilke(t) andre dokument(er) DENNE rettskilden endrer. Samme "ingen join, la klienten slå opp"-mønster
 * som RettskildeHjemmelDto — slå `endringRettskildeId` opp mot den allerede hentede rettskilde-lista.
 */
export interface RettskildeEndringDto {
  id: string;
  endringEid: string;
  endringRettskildeId: string;
  sorteringsrekkefolge: number;
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
  /** [Ny, issue #395 M5] 'nedlagt' for et nedlagt departement (ingen registerkobling, gyldighet fra vedtakene); ellers null/utelatt. */
  livsstatus?: string | null;
  gyldigFra?: string | null;
  gyldigTil?: string | null;
  /** [Ny, issue #397] Duplikat erstattet av denne aktøren (reversibelt). */
  erstattetAvId?: string | null;
  /** [Ny, Johanns avgjørelse S3 på #397] Nedlagt uten kjent hovedetterfølger (regnet ut). */
  hovedetterfolgerUkjent?: boolean | null;
  /**
   * REGISTERETS egen form av navnet (issue #158) — for Brreg-synkroniserte rader betyr det VERSALER,
   * og for tospråklige kommuner en konkatenering uten skilletegn:
   * «GAIVUONA SUOHKAN KÅFJORD KOMMUNE KAIVUONON KOMUUNI».
   *
   * IKKE dette feltet i UI. Bruk `visningsnavn` — se dens egen kommentar. Dette feltet vises kun der
   * poenget ER registreringen (grunndata-raden «Registrert i Enhetsregisteret» på VirksomhetDetalj).
   */
  navn: string;
  /**
   * [Ny, registernavn-runden, 2026-09-08] Navnet som skal VISES: virksomhetens navneform med grunn
   * `'gjeldende'` når den finnes, ellers `navn`. Aldri null — backend faller alltid tilbake på `navn`
   * (se `VirksomhetDto.FraEntitet` og `VirksomhetVisningsnavnTjeneste` i backend).
   *
   * Bakgrunn: Johann rapporterte «Gaivuona suohkan kåfjord kommune kaivuonon komuuni» i tagg-listen
   * under forskrift 2005-06-17-657 § 1. Løsningen var å slutte å regne på navnet og i stedet hente
   * den lesbare formen fra Kartverkets SSR (som også har de diakritiske tegnene Brreg mangler:
   * «Kárášjoga gielda»), med `navn` beholdt som registerets form.
   */
  visningsnavn: string;
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
  /** [Ny, issue #310] rettssubjekt | organ | organisatorisk_enhet — null = uavklart. Automatisk bare for
   * KOMM/FYLK (og forvaltningsnivå kommune/fylkeskommune), ellers satt av et menneske. */
  aktortype: Aktortype | null;
  /** [Ny, issue #353] trygdeordning | fond | tilskuddsordning — bare når aktørtypen er 'ordning'; null = ikke angitt. */
  ordningstype: Ordningstype | null;
  /** [Ny, issue #385 AC1/AC2] Sann når aktøren ikke har en godkjent gjeldende navneform — `visningsnavn` er da registernavnet. */
  navneformMangler?: boolean | null;
}

/** [Ny, 2026-08-30] Opprett en virksomhet med KUN navn — se POST /api/virksomheter. */
export interface OpprettVirksomhetRequest {
  navn: string;
  overordnetEnhetId: string | null;
  /** [Ny, issue #386] Navnet registreres som gjeldende navneform med dette grunnlaget og denne statusen. */
  grunnlag?: UtsagnsgrunnlagRequest | null;
  /** [Ny, issue #386] Navnet kommer fra denne navnekandidaten; hjemmelen er noden der navnet står. */
  navnFraNavnekandidatId?: string | null;
}

/**
 * [Ny, issue #157] Oversikt over ALT som rammes av en kaskadesletting av virksomheten — grunnlaget for
 * bekreftelsesdialogen FØR selve `DELETE /api/virksomheter/{id}?bekreft=true`-kallet. Se
 * `GET /api/virksomheter/{id}/slett-oversikt`.
 */
export interface VirksomhetSlettOversiktDto {
  virksomhetId: string;
  virksomhetNavn: string;
  tjenester: number;
  rettskilder: number;
  begreper: number;
  navneformer: number;
  brukere: number;
  /** [ENDRET, issue #311] Var myndighetstildelinger + virksomhetRelasjoner — nå alle strukturkanter med
   * virksomheten i en av endene (kaskaderer). */
  strukturkanter: number;
  virksomhetKandidater: number;
  virksomhetNettsider: number;
  kodelister: number;
  datasett: number;
  vilkar: number;
  regelnoder: number;
  unntak: number;
  vilkarstreKommentarer: number;
  tekstTagger: number;
  hendelser: number;
  kunnskapsbibliotekLenker: number;
  kunnskapsbibliotekFiler: number;
  underliggendeVirksomheter: number;
  tekstTaggerMedPublisertReferanse: number;
  kanSlettes: boolean;
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
  /** [Ny, Johanns avgjørelse P4 på #397] Hjemmel i konsolidert DEL-tekst fra før 2001: «ikke kontrollert mot kunngjort tekst». */
  ikkeKontrollertMotKunngjort?: boolean;
  id: string;
  virksomhetId: string | null;
  begrepskategori: string | null;
  virksomhetReferanseId: string | null;
  lovkildeId: string | null;
  term: string;
  definisjon: string | null;
  /** [Ny, issue #194] SNL-artikkelens URL når navneformen stammer fra et bekreftet SNL-oppslag
   * (auto-opprettet ved Brreg- eller manuell virksomhetsopprettelse) — `null` for manuelt lagt til
   * navneformer uten en slik kilde. Samme felt/mønster som `NavnekandidatDto.snlUrl`. */
  skosUrl: string | null;
  status: string;
  /** Se `Navneformgrunn`. `null` = uspesifisert. */
  navneformgrunn: Navneformgrunn | null;
  // [Ny, issue #385] Navneformen som modellutsagn. Hjemmel = lovkildeId + lovreferanseEid. godkjenning null = registrert før #385.
  lovreferanseEid?: string | null;
  kunnskapsstatus?: Kunnskapsstatus | null;
  godkjenning?: Godkjenning | null;
  kildeUtenforKorpusTekst?: string | null;
  kildeUtenforKorpusType?: KildeUtenforKorpusType | null;
  kildeUtenforKorpusDokumentasjon?: KildeUtenforKorpusDokumentasjon | null;
  kildeUtenforKorpusLenker?: string[] | null;
  begrunnelse?: string | null;
  tolkningsargumenter?: Tolkningsargument[] | null;
  oppdagelsesKilde?: string | null;
  avvisningBegrunnelse?: string | null;
  sistEndretAv?: string | null;
}

export interface ParagrafspennParDto {
  fraEid: string;
  tilEid: string | null;
}

// ---------- Strukturkanter (issue #311 «Strukturmodell 6», docs/33 §4.3) ----------

/** [Ny, issue #311] De åtte kategoriene — lukket vokabular, speilet av `Strukturkanter.Kategorier` i
 * RegelIde.Data. R relasjon, K kompetanse, M medlemskap, O områdesammensetning, A ansvarsområde,
 * G organtilhørighet, I rolleinnehav, T klassenivå. Typekoden innenfor kategorien er konfigurerbar
 * (`RelasjonsTypeKonfigurasjonDto`). */
export type Strukturkantkategori = 'R' | 'K' | 'M' | 'O' | 'A' | 'G' | 'I' | 'T'
  // [Ny, issue #353] P = plikt overfor motpart (samarbeid, avtale, betaling, bistand, informasjon, konsultasjon).
  | 'P'
  // [Ny, issue #368] F = oppgave/funksjon (forvaltning, samordning, sørge for, oversikt, bistand, sekretariat, rådgivning,
  // veiledning). Gir aldri K eller P i seg selv (docs/profil/02-begrepsmanual.md §F).
  | 'F';

/** [Ny, issue #311, Johanns beslutning 2026-10-07] Typen kilde utenfor korpus — speilet av
 * `Strukturkanter.KildeUtenforKorpusTyper`. `nettside_annet` = bare dokumentert på en nettside e.l. —
 * arbeidslista over struktur uten forankring i en rettskilde. */
export type KildeUtenforKorpusType =
  'kgl_res' | 'instruks' | 'tildelingsbrev' | 'vedtekter' | 'styrevedtak' | 'forarbeider' | 'nettside_annet'
  // [Ny, issue #312] Et autoritativt register som ikke er en rettskilde (Kartverket, Enhetsregisteret, SSR) —
  // se Strukturkanter.Register.
  | 'register';
/** [Ny, Johanns beslutning 2026-10-07] Lenken/teksten er selve kilden (`primaer`) eller en tekst som refererer
 * den (`sekundaer`, f.eks. en artikkel som omtaler en kgl.res. — og som kan ta feil om den, se #311). */
export type KildeUtenforKorpusDokumentasjon = 'primaer' | 'sekundaer';

/** [Ny, issue #311] Én ende av en kant: en virksomhet (aktør) eller et begrep med gruppefunksjon.
 * `nodetype` er aktørtypen (kan være null = uavklart) eller begrepskategorien. */
export interface StrukturnodeDto {
  type: 'virksomhet' | 'begrep';
  id: string;
  navn: string;
  nodetype: string | null;
  /** [Ny, issue #390 AK 3] Områdetypen når noden er et registrert område, ellers null. */
  omradetype?: Omradetype | null;
}

/** [Ny, issue #311] GET /api/strukturkanter?virksomhetId|begrepId=… — én kant med navn og visningstekst.
 * `retning` er nodens side når listen er hentet for en node ('fra'/'til'), ellers null (per hjemmel,
 * forslagskø). `til` er null bare for K/T og P ([ENDRET, issue #353]). */
/** [Ny, issue #341, Johanns hierarkibeslutning 2026-10-08] Kompetansefamiliene — speilet av `Strukturkanter.Familier`.
 * [ENDRET, issue #352] 'personell' heter 'oppnevning' (Johanns beslutning 1). */
export type Kompetansefamilie =
  'struktur' | 'oppnevning' | 'styring' | 'normgivning' | 'kontroll' | 'klage_overproving' | 'vedtak' | 'sanksjon';
/** [Ny, issue #352] Undertypen på oppnevning (valg/ansettelse/utpeking/oppnevning) og overprøving (anke) — speilet av
 * `Strukturkanter.Undertyper`. */
export type Kompetanseundertype = 'valg' | 'ansettelse' | 'utpeking' | 'oppnevning' | 'anke'
  // [Ny, issue #355] Avslutning speiler innsetting: utnevning/konstitusjon på oppnevning, avsetting/oppsigelse/avskjed på avsetting,
  // tilbakekall på vedtak.
  | 'utnevning' | 'konstitusjon' | 'avsetting' | 'oppsigelse' | 'avskjed' | 'tilbakekall';
/** [Ny, issue #341] Forvaltningslovens § 2-perspektiv på en kompetansetype. */
export type FvlKategori = 'forskrift' | 'enkeltvedtak' | 'ikke_vedtak';
/** [Ny, issue #341] Normformen på en normgivningskompetanse — speilet av `Strukturkanter.Normformer`. */
export type Normform = 'forskrift' | 'reglement' | 'arbeidsordning' | 'vedtekter' | 'instruks';
/** [Ny, issue #341] Kompetansens grunnlag. */
export type Kompetansegrunnlag = 'offentligrettslig' | 'privatrettslig';
/** [Ny, issue #353] Modaliteten på en plikt — speilet av `Strukturkanter.Modaliteter` (CHECK ck_strukturkanter_modalitet).
 * 'bor' lagres uten ø og vises «bør». Bare på P; null = ikke angitt (presens «Staten dekker …» gjettes ikke til «skal»). */
export type Modalitet = 'skal' | 'kan' | 'bor';
/** [Ny, issue #353] P-typekodene — speilet av `Strukturkanter.Plikttyper`. */
export type Plikttype = 'samarbeid' | 'avtale' | 'betaling' | 'bistand' | 'informasjon' | 'konsultasjon';

/** [Ny, issue #368] F-typekodene — speilet av `Strukturkanter.Oppgavetyper`. */
export type Oppgavetype =
  'forvaltning' | 'samordning' | 'sorge_for' | 'oversikt' | 'bistand' | 'sekretariatsfunksjon' | 'radgivning' | 'veiledning' | 'kontroll';

export interface StrukturkantDto {
  id: string;
  kategori: Strukturkantkategori;
  typekode: string;
  retning: 'fra' | 'til' | null;
  visningstekst: string;
  fra: StrukturnodeDto;
  til: StrukturnodeDto | null;
  objekt: string | null;
  paragrafspenn: ParagrafspennParDto[];
  avgrensningTekst: string | null;
  polaritet: 'positiv' | 'negativ';
  hjemmelRettskildeId: string | null;
  hjemmelRettskildeTittel: string | null;
  hjemmelEid: string | null;
  kildeUtenforKorpusTekst: string | null;
  kildeUtenforKorpusLenke: string | null;
  kildeUtenforKorpusType: KildeUtenforKorpusType | null;
  kildeUtenforKorpusDokumentasjon: KildeUtenforKorpusDokumentasjon | null;
  gyldigFra: string | null;
  gyldigTil: string | null;
  /** [ENDRET, issue #367] Godkjenning (hvem har godkjent), ikke metode — se `kunnskapsstatus`. */
  status: Godkjenning;
  /** 'manuell' | 'monster:<id>' | 'ki:<modell>' — hvilken mekanisme som fant utsagnet (docs/33 §4.3). */
  oppdagelsesKilde: string;
  kommentar: string | null;
  opprettetAv: string;
  opprettetTidspunkt: string;
  // [Ny, issue #341] K-feltene. For K er `til` MOTPARTEN («har klagekompetanse overfor …»).
  normform: Normform | null;
  grunnlag: Kompetansegrunnlag | null;
  /** [ENDRET, issue #367 / #335] Delegeringsadgang etter TEKSTEN (null = ikke vurdert) og slutningen etter alminnelige regler. */
  delegeringsadgang: Delegeringsadgang | null;
  delegeringSlutning: DelegeringSlutning | null;
  /** Normgivning der motparten er innehaveren selv (avledet). */
  selvregulering: boolean;
  /** Typens familie og fvl-kategori fra typekonfigurasjonen (bare K). */
  familie: Kompetansefamilie | null;
  /** [Ny, issue #352] Undertypen (bare K oppnevning/overproving). null = ikke angitt. Står også i visningsteksten i parentes. */
  undertype: Kompetanseundertype | null;
  fvlKategori: FvlKategori | null;
  /** [Ny, issue #353] Modaliteten (bare P). null = ikke angitt. Står alt i visningsteksten i parentes («(skal)», «(bør)»). */
  modalitet: Modalitet | null;
  /** [Ny, issue #367] Hvordan utsagnet er kommet fram (null = ikke vurdert), med grunnlaget og analysehistorikken. */
  kunnskapsstatus: Kunnskapsstatus | null;
  begrunnelse: string | null;
  tolkningsargumenter: Tolkningsargument[] | null;
  premissIder: string[] | null;
  entitetsstatus: 'gjeldende' | 'foreldet';
  erstatterKantId: string | null;
  profilversjon: string | null;
  /** [Ny, issue #367 AC4] Den versjonerte hjemmelnoden; merknaden når den mangler, og resynk-flagget «hjemmel endret». */
  hjemmelNodeId: string | null;
  hjemmelVersjonMerknad: string | null;
  hjemmelEndretTidspunkt: string | null;
  /** [Ny, issue #313 / #374] Hvem som sist godkjente/avviste, når, og begrunnelsen for en avvisning. */
  sistEndretAv?: string | null;
  sistEndretTidspunkt?: string | null;
  avvisningBegrunnelse?: string | null;
  /** [Ny, issue #397] Omfanget av en avledet R etterfolger: antall lovdeler som gikk over (talt fra premissene). Null ellers. */
  omfang?: number | null;
}

/** [Ny, issue #313 AC4] Forhåndsvisning: slik kanten blir seende ut på fra-siden, til-siden og rettskildesiden. */
export interface StrukturkantForhandsvisningDto {
  fraSide: StrukturkantDto;
  tilSide: StrukturkantDto | null;
  rettskildeside: StrukturkantDto;
}

/** [Ny, issue #313] Rapporten fra én lasting av strukturforslag (StrukturforslagTjeneste.LastAsync). */
export type Forslagsutfall = 'ny' | 'fantes_som_forslag' | 'fantes_godkjent' | 'tidligere_avvist' | 'dublett_i_kjoringen' | 'ikke_lastet';
export interface ForslagsutfallRad {
  utsagnId: string; eid: string; sitat: string; fasitKategori: string; fasitType: string;
  kategori: string | null; typekode: string | null; utfall: Forslagsutfall; arsak: string | null; detalj: string | null;
  kantId: string | null; oppdagelseskilde: string;
}
export interface ForslagKategoritall {
  kategori: string; utsagn: number; nye: number; fantesSomForslag: number; fantesGodkjent: number; tidligereAvvist: number;
  dubletter: number; ikkeLastet: number;
}
export interface StrukturforslagRapport {
  rettskildeId: string; rettskildeTittel: string; lag: 'monster' | 'ki'; oppdagelseskilde: string; noderLest: number; utsagnTotalt: number;
  perKategori: ForslagKategoritall[];
  ikkeLastetPerArsak: { arsak: string; tekst: string; antall: number; eksempler: ForslagsutfallRad[] }[];
  uloesteAktorer: {
    tekstform: string; arsak: string; antallUtsagn: number; navnekandidatId: string | null; treff: string[]; status: Aktorstatus | null;
    /** [Ny, issue #440] Aktørkøens rute; bare 'aktorko' gir en navnekandidat. */
    aktorkoRute: Aktorkorute | null;
  }[];
  rader: ForslagsutfallRad[];
  kiInputTokens: number | null; kiOutputTokens: number | null; kiKastedeRader: number | null;
  /** [Ny, issue #378 A3] Aktørene per status — samme klassifisering som aktørsveipet. */
  aktorerPerStatus: { status: Aktorstatus; antall: number }[];
  /** [Ny, issue #385 AC3] Hva navnetreffene ga. */
  aktoridentitet?: Treffidentitet | null;
}

/** [Ny, issue #367 AC2] Godkjenning (strukturkant.status). Før #367: 'foreslatt_av_ai' | 'validert'. */
export type Godkjenning = 'maskinelt_foreslatt' | 'manuelt_foreslatt' | 'faglig_vurdert' | 'godkjent' | 'avvist';
/** [Ny, issue #367 AC1, docs/profil/03 §5] */
export type Kunnskapsstatus = 'tekstfunn' | 'normalisering' | 'fortolkning' | 'avledet' | 'eksternt_dokumentert' | 'uavklart' | 'omstridt' | 'foreldet';
export type Tolkningsargument = 'ordlyd' | 'kontekst_system' | 'formal' | 'forarbeider' | 'praksis' | 'reelle_hensyn' | 'antitetisk_analogisk';
/** [Ny, issue #367 AC5 / #335] */
export type Delegeringsadgang = 'uttrykkelig_tillatt' | 'uttrykkelig_forbudt' | 'ikke_regulert';
export type DelegeringSlutning = 'trolig_delegerbar' | 'trolig_ikke_delegerbar';

/** [Ny, issue #311] Rå kantfelt — svaret fra veiviserens kobl-til-*-endepunkter. */
export interface StrukturkantRadDto {
  id: string;
  kategori: Strukturkantkategori;
  typekode: string;
  fraVirksomhetId: string | null;
  fraBegrepId: string | null;
  tilVirksomhetId: string | null;
  tilBegrepId: string | null;
  hjemmelRettskildeId: string | null;
  hjemmelEid: string | null;
  kildeUtenforKorpusTekst: string | null;
  kildeUtenforKorpusType: KildeUtenforKorpusType | null;
  kildeUtenforKorpusDokumentasjon: KildeUtenforKorpusDokumentasjon | null;
  paragrafspenn: ParagrafspennParDto[];
  avgrensningTekst: string | null;
  polaritet: 'positiv' | 'negativ';
  status: Godkjenning;
}

/** [Ny, issue #311] POST /api/strukturkanter. Nøyaktig én fra-node; høyst én til-node (ingen bare for K/T og — [ENDRET, issue #353] — P).
 * Hjemmel ELLER kilde utenfor korpus er påkrevd, og polariteten MÅ oppgis (ingen standardverdi). */
export interface StrukturkantRequest {
  kategori: Strukturkantkategori;
  typekode: string;
  fraVirksomhetId?: string | null;
  fraBegrepId?: string | null;
  tilVirksomhetId?: string | null;
  tilBegrepId?: string | null;
  hjemmelRettskildeId?: string | null;
  hjemmelEid?: string | null;
  kildeUtenforKorpusTekst?: string | null;
  kildeUtenforKorpusLenke?: string | null;
  /** Påkrevd sammen med kildeteksten (Johanns beslutning 2026-10-07). */
  kildeUtenforKorpusType?: KildeUtenforKorpusType | null;
  kildeUtenforKorpusDokumentasjon?: KildeUtenforKorpusDokumentasjon | null;
  paragrafspenn?: ParagrafspennParDto[];
  avgrensningTekst?: string | null;
  objekt?: string | null;
  polaritet: 'positiv' | 'negativ';
  gyldigFra?: string | null;
  gyldigTil?: string | null;
  kommentar?: string | null;
  /** [Ny, issue #341] Bare på K (normform bare på normgivning). Null/utelatt = ikke angitt. */
  normform?: Normform | null;
  grunnlag?: Kompetansegrunnlag | null;
  /** [ENDRET, issue #367 / #335] Erstatter delegerbar. Slutningen bare sammen med 'ikke_regulert'. */
  delegeringsadgang?: Delegeringsadgang | null;
  delegeringSlutning?: DelegeringSlutning | null;
  /** [Ny, issue #352] Bare på K oppnevning/overproving. Null/utelatt = ikke angitt. */
  undertype?: Kompetanseundertype | null;
  /** [Ny, issue #353] Bare på P. Null/utelatt = ikke angitt (serveren avviser modalitet på andre kategorier). */
  modalitet?: Modalitet | null;
  /** [Ny, issue #367] Godkjenning (utelatt = godkjent) og kunnskapsstatus med grunnlag. */
  status?: Exclude<Godkjenning, 'maskinelt_foreslatt'> | null;
  kunnskapsstatus?: Kunnskapsstatus | null;
  begrunnelse?: string | null;
  tolkningsargumenter?: Tolkningsargument[] | null;
  premisser?: string[] | null;
}

// [FJERNET, issue #311] GruppeMedlemskapRequest, VirksomhetRelasjonDto/-Request/-HjemletDto og
// NavnekandidatRelasjonDto — erstattet av Strukturkant*-typene over. MyndighetstildelingDto og
// GruppeMedlemskapDto under er beholdt fordi API-et fortsatt serverer dem som LESEFASADER for
// nettside-eksporten; frontend leser StrukturkantDto.

/** [ENDRET, issue #311] Lesefasade (M-/I-kant i myndighetstildelingens gamle form) — se Dtos.cs. */
export interface MyndighetstildelingDto {
  id: string;
  gruppeBegrepId: string;
  virksomhetId: string;
  hjemmelRettskildeId: string;
  paragrafspenn: ParagrafspennParDto[];
  vilkaar: string | null;
  gyldigFra: string | null;
  gyldigTil: string | null;
  /** [Ny, issue #285 AC5] 'foreslatt_av_ai' | 'validert' — se MyndighetstildelingEntitet.Status. */
  status: 'foreslatt_av_ai' | 'validert';
}

/** [Ny, gruppemedlemskap-runden, 2026-09-08, issue #164] «Gruppe av gruppe» — `underordnetGruppeBegrepId`
 * er MEDLEM av `overordnetGruppeBegrepId`, hjemlet i `hjemmelRettskildeId` (typisk en forskrift, ikke
 * den loven som definerer gruppene). Samme form som `MyndighetstildelingDto`, som er den tilsvarende
 * kanten ned til en konkret virksomhet. */
export interface GruppeMedlemskapDto {
  id: string;
  overordnetGruppeBegrepId: string;
  underordnetGruppeBegrepId: string;
  hjemmelRettskildeId: string;
  paragrafspenn: ParagrafspennParDto[];
  gyldigFra: string | null;
  gyldigTil: string | null;
  /** [Ny, issue #285 AC5] 'foreslatt_av_ai' | 'validert' — se GruppeMedlemskapEntitet.Status. */
  status: 'foreslatt_av_ai' | 'validert';
}

// ---------- KI-oppdagelse av virksomheter/grupper/roller/relasjoner (issue #285) ----------

/** POST /api/ki-oppdagelse/kjor. */
export interface KiOppdagelseRequest {
  rettskildeIder: string[];
}

/** Ett behandlet KI-forslag — se KiOppdagelseKandidatUtfall (RegelIde.Data) for feltenes betydning.
 * `*IkkeOpprettetGrunn`/`NavnekandidatFeil` er null når raden faktisk ble opprettet (id-feltet satt). */
export interface KiOppdagelseKandidatUtfallDto {
  /** [ENDRET, issue #310] KI-en foreslår nodetypen; 'gruppe' = «usikker, mennesket velger». */
  type: 'virksomhet' | Kandidatnodetype | 'gruppe';
  navn: string;
  nodeEid: string;
  navnekandidatId: string | null;
  navnekandidatFeil: string | null;
  /** [ENDRET, issue #311] Kant-id-er (M/I for rolle, R for relasjon, M for gruppe-av-gruppe). */
  rolleKantId: string | null;
  rolleIkkeOpprettetGrunn: string | null;
  relasjonKantId: string | null;
  relasjonIkkeOpprettetGrunn: string | null;
  gruppeAvGruppeKantId: string | null;
  gruppeAvGruppeIkkeOpprettetGrunn: string | null;
  /** [Ny, issue #440] Aktørkøens rute for navnet; annet enn 'aktorko' = kjent ord, ingen navnekandidat (grunnen i navnekandidatFeil). */
  aktorkoRute: Aktorkorute | null;
}

export interface KiOppdagelseSamletResultatDto {
  kandidater: KiOppdagelseKandidatUtfallDto[];
  inputTokens: number | null;
  outputTokens: number | null;
  meldinger: string[];
}

/** GET /api/ki-oppdagelse/ko — alle ventende (status='foreslatt_av_ai') strukturkanter.
 * [ENDRET, issue #311] `type` er kantens kategori; `aiForslagVersjon` er oppdagelseskilden. */
export interface KiForslagKoRadDto {
  type: Strukturkantkategori;
  id: string;
  visningstekst: string;
  aiForslagVersjon: string | null;
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

/** Resultat av DELETE /api/virksomhet-kandidater (massehardsletting) — kun 'Avvist'-rader rammes, se
 * backend-kommentaren (VirksomhetKandidatTjeneste.HardslettAlleAvvisteAsync). */
export interface HardslettVirksomhetKandidaterResultatDto {
  antallSlettet: number;
}

/** Oppdagelsesmekanismen (docs/13-backlog.md §9) — komplementær til VirksomhetKandidatDto over.
 * `oppdagelsesKilde`/`snl*`/`ssr*` (docs/31-navneform-berikelse-snl-ssr-spesifikasjon.md) — kun satt
 * for kandidater funnet via det nye "stor bokstav midt i setning"-mønsteret
 * (`oppdagelsesKilde === 'stor-bokstav-snl-ssr'`); `null` for de opprinnelige, presise mønstrene.
 * Berikelsen slås opp server-side fra en ekstern-oppslag-cache PÅ LESETIDSPUNKTET, ikke lagret på
 * selve kandidatraden — se NavnekandidatDto på API-siden. */
/**
 * [Ny, navneformgrunn-runden, 2026-09-07] BEGRUNNELSEN for at en navneform peker på virksomheten sin
 * — kun meningsfull for `begrepskategori === 'virksomhet'`. `null` = uspesifisert (alle rader som
 * fantes før denne runden). Lukket vokabular, speilet av CHECK-constrainten `ck_begreper_navneformgrunn`
 * og av `VirksomhetsbegrepTjeneste.Navneformgrunner` i backend — hold de tre i takt.
 *
 * - `gjeldende`    gjeldende, offisielt navn (normaltilfellet)
 * - `utgatt`       historisk/avløst navn som fortsatt STÅR i lovtekst (Arkivverket -> Nasjonalarkivet)
 * - `kortform`     kontekstavhengig kortform («Suldal» betyr Suldal kommune her)
 * - `feilskriving` skrivefeil i KILDETEKSTEN («Matilsynet» med én t)
 *
 * MERK skillet: dette forklarer en LEGITIM streng som faktisk står i lovteksten. En regex-ARTEFAKT
 * («Ø Suldal kommune») er et annet problem, som rettes med PATCH /api/navnekandidater/{id}.
 */
export type Navneformgrunn = 'gjeldende' | 'utgatt' | 'kortform' | 'feilskriving' | 'parallellnavn'
  // [Ny, issue #378] Generisk omtale — én per term for hele korpuset, uten virksomhet. Velges aldri i NavneformgrunnVelger.
  | 'generisk';

/**
 * [Ny, issue #310 «nodetype-akse», 2026-10-07, docs/33 §4.1–4.2] Nodetypene et begrep med gruppefunksjon
 * kan ha. Speiler `Nodetyper` i RegelIde.Data (og CHECK-ene). `Kandidatnodetype` er de et menneske kan
 * VELGE for en navnekandidat. [ENDRET, issue #311] `organ` er ikke lenger en begrepskategori.
 * 'gruppe' er utfaset som begrepstype, men lever videre på en KANDIDAT som «nodetype ikke avgjort».
 */
export type Kandidatnodetype = 'klasse' | 'rolle' | 'omrade';
/** [ENDRET, issue #311] 'organ' fjernet — organer er virksomheter (Aktortype 'organ'). */
export type Begrepsnodetype = Kandidatnodetype;
/** Alle kategorier med gruppefunksjon — kan være begrep-noder i en strukturkant. */
export const BEGREPSKATEGORIER_MED_GRUPPEFUNKSJON: readonly string[] = ['gruppe', 'klasse', 'rolle', 'omrade'];
export function harGruppefunksjon(begrepskategori: string | null | undefined): boolean {
  return !!begrepskategori && BEGREPSKATEGORIER_MED_GRUPPEFUNKSJON.includes(begrepskategori);
}
/** [Ny, issue #310] Aktørtype på Virksomhet — NULL = uavklart.
 * [ENDRET, issue #353] + 'ordning': en IKKE-aktør rettskilden gir en funksjon (folketrygden, et fond, en tilskuddsordning) —
 * speilet av `Nodetyper.Aktortyper`. */
export type Aktortype = 'rettssubjekt' | 'organ' | 'organisatorisk_enhet' | 'ordning';
/** [Ny, issue #353] Undertypen til en ordning — speilet av `Nodetyper.Ordningstyper`. Bare sammen med aktørtypen 'ordning'. */
export type Ordningstype = 'trygdeordning' | 'fond' | 'tilskuddsordning';

/** [Ny, navnekandidat-wizard-runden, 2026-09-07] PATCH /api/navnekandidater/{id} — utelatt/undefined
 * felt betyr «la stå uendret». Kun for REGEX-ARTEFAKTER i teksten, se `Navneformgrunn` sitt skille.
 * [ENDRET, issue #203 pkt. 2] 'administrativ_inndeling' lagt til. */
export interface OppdaterNavnekandidatRequest {
  foreslattTekst?: string;
  /** [ENDRET, issue #310] 'administrativ_inndeling' er slått inn i 'omrade'. */
  kategori?: Navnekandidatkategori;
}

/** [Ny, issue #310] Navnekandidatens kategori: 'gruppe' = generisk aktøromtale, nodetype ikke avgjort. */
export type Navnekandidatkategori = 'virksomhet' | 'gruppe' | Kandidatnodetype;

/** [Ny, navnekandidat-wizard-runden, 2026-09-07] POST /api/navnekandidater/{id}/kobl-til-virksomhet. */
export interface KoblNavnekandidatTilVirksomhetRequest extends VeivisergrunnlagFelt {
  virksomhetId: string;
  navneformgrunn: Navneformgrunn | null;
}

/** Svaret fra kobl-til-virksomhet — hele den lukkede kjeden. */
export interface NavnekandidatKoblingResultatDto {
  kandidat: NavnekandidatDto;
  navneform: BegrepDto;
  /** `null` når ingen tagg kunne opprettes (ukjent/uoppløsbart ansvarlig departement, eller en
   * tekstposisjon som ikke lenger stemmer). Skal VISES som en begrensning, ikke skjules. */
  taggId: string | null;
  rettskildeId: string;
  nodeEid: string;
  /** [Ny, issue #386] Navnet som gjeldende navneform når aktøren ble opprettet med bare et navn; ellers null. */
  gjeldendeNavneform?: BegrepDto | null;
  gjeldendeNavneformMerknad?: string | null;
}

/** [Ny, gruppemedlemskap-runden, 2026-09-08] POST /api/navnekandidater/{id}/kobl-til-gruppemedlemskap.
 * Hjemmelen sendes IKKE — den er alltid kandidatens egen rettskilde (det er der navnet står). */
export interface KoblNavnekandidatTilGruppemedlemskapRequest extends VeivisergrunnlagFelt {
  virksomhetId: string;
  gruppeBegrepId: string;
  navneformgrunn: Navneformgrunn | null;
}

/** Som `NavnekandidatKoblingResultatDto`, pluss selve medlemskapsraden. */
export interface NavnekandidatGruppemedlemskapResultatDto {
  kandidat: NavnekandidatDto;
  navneform: BegrepDto;
  taggId: string | null;
  rettskildeId: string;
  nodeEid: string;
  /** [ENDRET, issue #311] Kanten (M, eller I når gruppebegrepet er en rolle). */
  tildeling: StrukturkantRadDto;
  /** [Ny, issue #386] Navnet som gjeldende navneform når aktøren ble opprettet med bare et navn; ellers null. */
  gjeldendeNavneform?: BegrepDto | null;
  gjeldendeNavneformMerknad?: string | null;
}

export interface NavnekandidatDto {
  id: string;
  foreslattTekst: string;
  // [ENDRET, issue #203 pkt. 2/3] 'administrativ_inndeling' lagt til — nasjon/fylke/kommune,
  // SSR-bekreftet ved klassifisering (KlassifiserAsync), ikke satt av mønstergjenkjenningen selv.
  // [ENDRET, issue #310] ...og nå slått inn i 'omrade'. 'klasse'/'rolle'/'omrade' = nodetype foreslått
  // (KI/SSR) eller valgt; 'gruppe' = generisk aktøromtale, nodetype ikke avgjort.
  kategori: Navnekandidatkategori;
  rettskildeId: string;
  nodeEid: string;
  startOffset: number;
  endOffset: number;
  status: string;
  opprettetAv: string;
  opprettetTidspunkt: string;
  behandletAv: string | null;
  behandletTidspunkt: string | null;
  oppdagelsesKilde: string | null;
  snlUrl: string | null;
  snlAlias: string[] | null;
  snlOrganisasjonsnummer: string | null;
  ssrBekreftetStedsnavn: boolean | null;
  ssrObjektType: string | null;
  /** [Ny, konfidens-runden, 2026-09-09] `'hoy'` | `'lav'` | `null` (ikke klassifisert — alle
   * `gruppe`-kandidater, og rader fra før feltet fantes). Erstatter automatisk avvisning: et treff
   * SNL/SSR ikke bekreftet venter nå på et menneske med LAV konfidens, i stedet for å bli avvist av
   * systemet. Johann 2026-09-09. */
  konfidens: string | null;
  /** Hvorfor konfidensen ble som den ble — `'snl_treff'` | `'ssr_med_institusjonsord'` |
   * `'ssr_uten_institusjonsord'` | `'ukjent_i_snl_og_ssr'`, eller `null`. «Lav» alene er ikke
   * handlingsrettet; grunnen er det. */
  konfidensGrunn: string | null;
}

/** [Ny, navnekandidat-alle-mekanismer-runden, 2026-09-21, issue #283 AC1-4] POST /api/navnekandidater/
 * manuell — den manuelle inngangsdøren fra `TagTekst`s tag-linje («Behandle som organ/gruppe →»). */
export interface NavnekandidatManuellRequest {
  rettskildeId: string;
  nodeEid: string;
  startOffset: number;
  endOffset: number;
  foreslattTekst: string;
}

/** [Ny, navnekandidat-alle-mekanismer-runden, 2026-09-21, issue #283 AC5/AC6] POST /api/navnekandidater/
 * {id}/kobl-til-myndighetstildeling — det valgfrie «Rolle tildelt her»-steget, for BÅDE virksomhet- og
 * gruppemedlem-slaget. `rolleBegrepId` er et FRITT valgt gruppebegrep, ikke gruppemedlem-sporets egen
 * gruppe. Hjemmelen sendes ikke — den er alltid kandidatens egen rettskilde. */
export interface KoblNavnekandidatTilMyndighetstildelingRequest extends VeivisergrunnlagFelt {
  virksomhetId: string;
  rolleBegrepId: string;
  /** [Ny, issue #341] HVOR i kandidatens rettskilde rollen tildeles — null = kandidatens egen node. */
  hjemmelEid: string | null;
  /** [ENDRET, issue #341] Nå bare AVGRENSNINGEN: hvilke paragrafer rollen gjelder for (valgfri). */
  paragrafspenn: ParagrafspennParDto[];
  vilkaar: string | null;
  navneformgrunn: Navneformgrunn | null;
}

/** Som `NavnekandidatGruppemedlemskapResultatDto`, men `tildeling` gjelder et fritt valgt rollebegrep. */
export interface NavnekandidatMyndighetstildelingResultatDto {
  kandidat: NavnekandidatDto;
  navneform: BegrepDto;
  taggId: string | null;
  rettskildeId: string;
  nodeEid: string;
  tildeling: StrukturkantRadDto;
  /** [Ny, issue #386] Navnet som gjeldende navneform når aktøren ble opprettet med bare et navn; ellers null. */
  gjeldendeNavneform?: BegrepDto | null;
  gjeldendeNavneformMerknad?: string | null;
}

/** [Ny, navnekandidat-alle-mekanismer-runden, 2026-09-21, issue #283 AC7/AC8] POST /api/navnekandidater/
 * {id}/kobl-til-relasjon — det valgfrie «Relasjon til annen virksomhet»-steget. `hjemletHer=true`
 * hjemler relasjonen i kandidatens egen rettskilde/node (sendes ikke — settes server-side);
 * `false` lagrer kun `kommentar` som fritekst, uten hjemmel. */
export interface KoblNavnekandidatTilRelasjonRequest extends VeivisergrunnlagFelt {
  virksomhetId: string;
  navneformgrunn: Navneformgrunn | null;
  motpartVirksomhetId: string;
  relasjonsType: string;
  hjemletHer: boolean;
  kommentar: string | null;
  /** [Ny, issue #311] Påkrevd når `hjemletHer` er false — kommentaren lagres som kilde utenfor korpus. */
  kildeUtenforKorpusType?: KildeUtenforKorpusType | null;
  kildeUtenforKorpusDokumentasjon?: KildeUtenforKorpusDokumentasjon | null;
}

export interface NavnekandidatRelasjonResultatDto {
  kandidat: NavnekandidatDto;
  navneform: BegrepDto;
  taggId: string | null;
  rettskildeId: string;
  nodeEid: string;
  /** [ENDRET, issue #311] R-kanten. */
  relasjon: StrukturkantRadDto;
  /** [Ny, issue #386] Navnet som gjeldende navneform når aktøren ble opprettet med bare et navn; ellers null. */
  gjeldendeNavneform?: BegrepDto | null;
  gjeldendeNavneformMerknad?: string | null;
}

/** [Ny, navnekandidat-alle-mekanismer-runden, 2026-09-21, issue #283 AC9] POST /api/navnekandidater/
 * {id}/kobl-til-gruppe-av-gruppe — kun for kategori='gruppe'-kandidater. */
export interface KoblNavnekandidatTilGruppeAvGruppeRequest {
  overordnetGruppeBegrepId: string;
  /** [Ny, issue #310] Påkrevd for en 'gruppe'-kandidat (uavklart type). */
  nodetype?: Kandidatnodetype;
}

export interface NavnekandidatGruppeAvGruppeResultatDto {
  kandidat: NavnekandidatDto;
  gruppebegrep: BegrepDto;
  /** [ENDRET, issue #311] M-kanten (fra = det nye begrepet, til = den overordnede gruppen). */
  medlemskap: StrukturkantRadDto;
}

/** [Ny, issue #298 AC3] POST /api/navnekandidater/{id}/godkjenn-som-fast-gruppebegrep — kun for
 * kategori='gruppe'-kandidater. Ingen request-body: get-or-create avgjøres av ForeslattTekst alene. */
export interface NavnekandidatFastGruppebegrepResultatDto {
  kandidat: NavnekandidatDto;
  gruppebegrep: BegrepDto;
  /** `true` hvis gruppebegrepet ble OPPRETTET nå, `false` hvis en eksisterende fast rad med samme
   * Term (case-insensitiv) ble GJENBRUKT i stedet. */
  varNyttBegrep: boolean;
}

/** rettskildeId=null sveiper hele det importerte korpuset, satt snevrer inn til én rettskilde. */
export interface SveipNavnekandidaterRequest {
  rettskildeId: string | null;
}

export interface SveipNavnekandidaterResultatDto {
  antallTreffFunnet: number;
  antallNyeKandidater: number;
  /** [Ny, issue #440] Treff aktørkøens ruting sendte et annet sted, per rute. De ble ikke navnekandidater. */
  utenforAktorkoen: Partial<Record<Aktorkorute, number>>;
}

/** Massegodkjenning/-avvisning — egen, ikke delt DTO-familie (se backend-kommentaren på
 * NavnekandidatBatchRequest for begrunnelsen: samme form som VirksomhetKandidatBatch*, men holdt
 * separat per kø). */
export interface NavnekandidatBatchRequest {
  ider: string[];
}

export interface NavnekandidatBatchRadDto {
  id: string;
  ok: boolean;
  feil: string | null;
  resultat: NavnekandidatDto | null;
}

export interface NavnekandidatBatchResultatDto {
  rader: NavnekandidatBatchRadDto[];
}

/** [Ny, issue #299 AC3/AC4] POST /api/navnekandidater/kobl-til-virksomhet-batch — «Behandle gruppen»
 * for 'virksomhet'-kandidater: SAMME valgte/opprettede virksomhet kobles til ALLE `ider` i ett kall.
 * Svaret gjenbruker `NavnekandidatBatchResultatDto` (samme per-rad ok/feil-form som de andre batchene). */
export interface KoblNavnekandidaterTilVirksomhetBatchRequest {
  ider: string[];
  virksomhetId: string;
  navneformgrunn: Navneformgrunn | null;
  /** `true` = saksbehandleren har eksplisitt valgt «behandle alle som virksomhet» for en gruppe med
   * blandet kategori — rader med annen kategori omkategoriseres først (utelatt/false = som før). */
  omkategoriser?: boolean;
}

/** [Ny, issue #299 AC3/AC4] POST /api/navnekandidater/godkjenn-gruppe-batch — «Behandle gruppen» for
 * 'gruppe'/'administrativ_inndeling'-kandidater: ETT delt begrep (opprettet/gjenbrukt — se
 * NavnekandidatOppdagelseTjeneste.GodkjennGruppeBatchAsync) kobles til ALLE `ider`. `fast` speiler
 * steg 2-radioknappen i enkeltrad-veiviseren (issue #298 AC3) — kun gyldig for 'gruppe'-kandidater. */
export interface NavnekandidatGruppeBatchRequest {
  ider: string[];
  fast: boolean;
  /** Eksplisitt valg om å behandle HELE gruppen som denne kategorien (omkategoriserer avvikende rader
   * først); utelatt = hver rads egen kategori. */
  // [ENDRET, issue #310] En NODETYPE — påkrevd når gruppen har kandidater med uavklart type ('gruppe').
  tilKategori?: Kandidatnodetype;
}

/** [Ny, 2026-08-30] Resultat av DELETE /api/navnekandidater (massesletting, valgfritt filtrert). */
export interface SlettNavnekandidaterResultatDto {
  antallSlettet: number;
}

/** [Ny, «flytt Slett inn i massehandling-raden», 2026-09-02] Massesletting av et PRESIST avkrysset
 * utvalg (POST /api/navnekandidater/slett-batch) — bruker NavnekandidatBatchRequest over for selve
 * id-listen, men egen radform (ingen `resultat`-felt — en slettet rad har ingen ny DTO). */
export interface NavnekandidatSlettBatchRadDto {
  id: string;
  ok: boolean;
  feil: string | null;
}

export interface NavnekandidatSlettBatchResultatDto {
  rader: NavnekandidatSlettBatchRadDto[];
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

/** PATCH /api/rettskilder/{id}/irrelevant (2026-08-30) — setter begge feltene samtidig. */
export interface OppdaterRettskildeIrrelevantRequest {
  erIrrelevant: boolean;
  irrelevantKommentar: string | null;
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

/** [Ny, issue #286] Kandidatkø-rad for GET /api/tjenester/regelverksreferanse-forslag — KI-foreslått
 * kobling for en EKSISTERENDE tjeneste uten regelverksreferanser. */
export interface TjenesteRegelverksreferanseForslagDto {
  id: string;
  tjenesteId: string;
  tjenesteTittel: string;
  virksomhetId: string;
  tilRettskildeId: string;
  tilEid: string;
  begrunnelse: string | null;
  status: string;
  aiForslagVersjon: string | null;
  opprettetAv: string;
  opprettetTidspunkt: string;
  behandletAv: string | null;
  behandletTidspunkt: string | null;
}

export interface KjorRegelverksreferanseforslagRequest {
  rettskildeIder: string[];
}

export interface KjorRegelverksreferanseforslagResponsDto {
  antallTjenesterUtenReferanse: number;
  antallVurdert: number;
  antallNyeForslag: number;
  inputTokens: number | null;
  outputTokens: number | null;
}

/** [Ny, issue #286] GET /api/rettskilder/{id}/statistikk — «Denne loven forvaltes av X virksomheter,
 * har Y begrep, har Z tjenester». */
export interface RettskildeStatistikkDto {
  rettskildeId: string;
  antallVirksomheter: number;
  antallBegrep: number;
  antallTjenester: number;
}

/** Samme rolle for en Handling som TjenesteRegelverksreferanseDto har for en Tjeneste (2026-08-22,
 * se OppgaveregisterHandlingSeed) — kun lesing i UI-et ennå, ingen generisk koble til/fjern-endepunkt
 * finnes (issue #290 la til en SNEVER oppgraderingsvei via regelverksreferanse-forslag-køen under). */
export interface HandlingRegelverksreferanseDto {
  id: string;
  handlingId: string;
  tilRettskildeId: string;
  tilEid: string;
}

/** [Ny, issue #290] Kandidatkø-rad for GET /api/tjenester/handlinger/regelverksreferanse-forslag —
 * KI-foreslått OPPGRADERING (dokumentnivå → paragrafnivå) av en Handling-regelverksreferanse. */
export interface HandlingRegelverksreferanseForslagDto {
  id: string;
  handlingId: string;
  handlingNavn: string;
  tjenesteTittel: string;
  tilRettskildeId: string;
  tilEid: string;
  begrunnelse: string | null;
  status: string;
  aiForslagVersjon: string | null;
  opprettetAv: string;
  opprettetTidspunkt: string;
  behandletAv: string | null;
  behandletTidspunkt: string | null;
}

export interface KjorHandlingRegelverksreferanseforslagRequest {
  rettskildeIder: string[];
}

export interface KjorHandlingRegelverksreferanseforslagResponsDto {
  antallKandidatrader: number;
  antallVurdert: number;
  antallNyeForslag: number;
  inputTokens: number | null;
  outputTokens: number | null;
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

/** Konfigurerbar typekode for strukturkanter (docs/29 §Del C) — GET /api/konfigurasjon/relasjonstyper[?kategori=X].
 * [ENDRET, issue #311] `kategori` — identiteten er (kategori, kode). */
export interface RelasjonsTypeKonfigurasjonDto {
  kategori: Strukturkantkategori;
  kode: string;
  fraVisningsmal: string;
  tilVisningsmal: string;
  /** [Ny, issue #341] Kompetansefamilien (bare K) — null på «beslutning» (øverst) og på en type som ikke er plassert. */
  familie?: Kompetansefamilie | null;
  /** [Ny, issue #341] forskrift | enkeltvedtak | ikke_vedtak (bare K), null = ikke avklart. */
  fvlKategori?: FvlKategori | null;
  /** [Ny, issue #352-tillegg] Gjelder typen den enkelte saken (G settes_med), ikke organet fast? */
  saksavhengig?: boolean;
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
  virksomhetId: string | null;
  /** null = ordinært begrep (faktabegrep/handlingsbegrep). 'virksomhet' = navneform for
   * virksomhetReferanseId. 'gruppe' = gruppebegrep hjemlet i lovkildeId. Se docs/20 §2.3/§2.4.
   * [ENDRET, issue #310] 'klasse' | 'rolle' | 'omrade' (organ fjernet i #311) — de typede begrepene med
   * gruppefunksjon (se `Begrepsnodetype`); 'gruppe' finnes bare på rader som ikke er reklassifisert. */
  begrepskategori: string | null;
  /** Kun satt når begrepskategori === 'virksomhet' — hvilken virksomhet dette er en navneform for. */
  virksomhetReferanseId: string | null;
  /** Kun satt for begrep med gruppefunksjon — loven begrepet er hjemlet i. null = fast, nasjonalt (#298). */
  lovkildeId: string | null;
  term: string;
  definisjon: string | null;
  lovreferanseEid: string | null;
  gjelderFor: string[];
  kodelisteReferanseId: string | null;
  skosUrl: string | null;
  begrepstype: string | null;
  status: string;
  versjon: number;
  /** Kun meningsfull når begrepskategori === 'virksomhet'. Se `Navneformgrunn`. */
  navneformgrunn: Navneformgrunn | null;
  /** [Ny, issue #312] Kun for registrerte områder (begrepskategori 'omrade'): fylke, kommune, tettsted, lagsogn,
   * lagdomme, helseregion, annet. null for alt annet, også områder registrert for hånd før #312. */
  omradetype?: Omradetype | null;
  /** [Ny, issue #312] Kommune-/fylkesnummer (Kartverket) eller SSR-stedsnummer. Attributt, ikke identitet alene. */
  omradekode?: string | null;
  /** [Ny, issue #384] Regelen medlemskapet i klassen utledes etter (null = registrert med M-kanter). */
  medlemskapsregel?: 'omraderegister_kommune' | null;
  /** [Ny, issue #384] Kunnskapsstatus for begrepet selv (null = ikke vurdert). */
  kunnskapsstatus?: Kunnskapsstatus | null;
}

/** [Ny, issue #384] Ett utledet medlem: rettssubjektet, kommunen det er rettssubjekt for, og territoriekanten (premisset). */
export interface UtledetMedlemDto {
  virksomhetId: string; navn: string; kommunenummer: string; omradeId: string; omradenavn: string; premissKantId: string; validert: boolean;
}
/** [Ny, issue #384] GET /api/gruppebegrep/{id}/utledet-medlemskap — beregnet, ikke lagret. */
export interface UtledetMedlemskapDto {
  klasseId: string; term: string; regel: string; hjemmel: string | null; klassensKunnskapsstatus: Kunnskapsstatus | null;
  kunnskapsstatus: Kunnskapsstatus; antallOmrader: number; medlemmer: UtledetMedlemDto[]; hull: string[];
}

/** [Ny, issue #312] Speilet av `Omradetyper.Alle` (CHECK ck_begreper_omradetype). */
export type Omradetype = 'fylke' | 'kommune' | 'tettsted' | 'lagsogn' | 'lagdomme' | 'helseregion' | 'annet';

export interface OmradeVisningDto {
  id: string;
  navn: string;
  omradetype: Omradetype | null;
  omradekode: string | null;
}

/** [Ny, issue #312, AC5] Én rubrikk i «gitt kommune X → …». `status` sier om svaret er entydig; ved flere
 * kandidater velges ingen (CLAUDE.md §8). Id er virksomhetens id for aktør-rubrikker, områdets begrep-id ellers. */
export interface TilhorighetsrubrikkDto {
  rubrikk: string;
  status: 'entydig' | 'ikke_entydig' | 'mangler';
  /** [Ny, #312] `forslag` = svaret hviler på en kant som ennå ikke er godkjent. */
  kandidater: { id: string; navn: string; forslag: boolean }[];
}

export interface KommuneTilhorighetDto {
  kommune: OmradeVisningDto;
  rubrikker: TilhorighetsrubrikkDto[];
  overordnede: OmradeVisningDto[];
  ansvarlige: { virksomhetId: string; navn: string; typekode: string; via: OmradeVisningDto; kantId: string }[];
}

/** [Ny, issue #390 AK 1] Ett svar i «Område og tilhørighet»: noden, premissene (kant-id-er i kjedens rekkefølge, slått opp i
 * `VirksomhetTilhorighetDto.premisser`) og det svakeste premisset. `kunnskapsstatus` er aldri sterkere enn det svakeste premisset
 * (docs/33 §4.6). `avledet` = false bare for en lagret kant fra aktøren selv (et registrert medlemskap). */
export interface TilhorighetKandidatDto {
  node: StrukturnodeDto;
  avledet: boolean;
  kunnskapsstatus: Kunnskapsstatus | null;
  premisser: string[];
  svakestePremissId: string | null;
  svakestePremissStatus: Kunnskapsstatus | null;
  forslag: boolean;
}

/** [Ny, issue #390 AK 1] `flere` = flere svar er riktig (en tingrett med to rettssteder); `ikke_entydig` = flere, ingen valgt. */
export interface TilhorighetSvarDto {
  rubrikk: string;
  status: 'entydig' | 'ikke_entydig' | 'flere' | 'mangler';
  kandidater: TilhorighetKandidatDto[];
  hull: string | null;
}

/** [Ny, issue #390 AK 1] GET /api/virksomheter/{id}/omrade-tilhorighet. `territorium` = null: aktøren har ikke eget territorium (`hull`). */
export interface VirksomhetTilhorighetDto {
  aktor: StrukturnodeDto;
  territorium: OmradeVisningDto | null;
  territoriekantId: string | null;
  hull: string | null;
  svar: TilhorighetSvarDto[];
  klasser: TilhorighetKandidatDto[];
  premisser: StrukturkantDto[];
}

/** [Ny, issue #418] Ett treff i «Kompetanse via klasse eller rolle»: K-kanten, `retning` (`har` = K fra klassen/rollen, `overfor` = K til
 * den), klassene og rollene i veien (`via`), og statusen etter svakeste premiss (docs/33 §4.6). `premisser` slås opp i
 * `KompetanseViaKlasseDto.premisser`. */
export interface KompetanseViaKlasseTreffDto {
  kant: StrukturkantDto;
  retning: 'har' | 'overfor';
  via: StrukturnodeDto[];
  kunnskapsstatus: Kunnskapsstatus;
  premisser: string[];
  svakestePremissId: string | null;
  svakestePremissStatus: Kunnskapsstatus | null;
  forslag: boolean;
}

/** [Ny, issue #418] GET /api/virksomheter/{id}/kompetanse-via-klasse. */
export interface KompetanseViaKlasseDto {
  aktor: StrukturnodeDto;
  treff: KompetanseViaKlasseTreffDto[];
  premisser: StrukturkantDto[];
  hull: string[];
}

/** [Ny, issue #353] Hvorfor en plikt gjelder (eller kan gjelde) kommunen: fra kommunens egen virksomhet, fra en klasse
 * kommunen er REGISTRERT medlem av, eller fra en klasse uten ett eneste registrert medlem (avgjøres ikke — `grunnlagHull`). */
/** [Ny, #353-retting] `kommunen_skal` = kommunen er pliktsubjektet; `overfor_kommunen` = andre har plikten overfor kommunen. */
export type PliktRetning = 'kommunen_skal' | 'overfor_kommunen';
export type PliktGrunnlag = 'direkte' | 'medlem_av' | 'klasse_uten_registrert_medlemskap';
/** [Ny, issue #353] Motparten sett fra kommunen: `konkret` (til er en virksomhet), `entydig` (klasse/rolle løst til ett medlem
 * via område), `ikke_entydig` (flere — ingen velges), `mangler` (ingen løsning, `hull` sier hvorfor), `ikke_angitt` (teksten
 * sier ikke hvem). */
export type PliktMotpartStatus = 'konkret' | 'entydig' | 'ikke_entydig' | 'mangler' | 'ikke_angitt';

/** [Ny, issue #353] Én plikt sett fra kommunen — `PliktTreffDto` i Dtos.cs. Kandidatene er virksomheter. */
export interface PliktTreffDto {
  kant: StrukturkantDto;
  /** [ENDRET, #353-retting] Hvilken ende kommunen er: pliktsubjektet, eller den plikten er overfor. */
  retning: PliktRetning;
  grunnlag: PliktGrunnlag;
  grunnlagHull: string | null;
  motpart: {
    status: PliktMotpartStatus;
    kandidater: { id: string; navn: string; forslag: boolean }[];
    hull: string | null;
  };
}

/** [Ny, issue #353 AC5, docs/32 S6] GET /api/omrader/kommuner/{kommunenummer}/plikter — «hvem har kommune X (samarbeids)plikt
 * med?». `hull` er det oppslaget ikke kan avgjøre, i klartekst (f.eks. at kommunen som rettssubjekt ikke er registrert). */
export interface KommunePlikterDto {
  kommune: OmradeVisningDto;
  kommunevirksomhet: StrukturnodeDto | null;
  /** P-typen det ble filtrert på, null = alle. */
  type: Plikttype | null;
  plikter: PliktTreffDto[];
  hull: string[];
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

/** Ett treff fra GET /api/begreper/{id}/brukt-i-rettskilder — se BegrepBruktIRettskilderTjeneste på
 * serveren. IKKE det samme som BegrepDto.lovreferanseEid (én manuelt satt referanse): dette er et
 * ekte reverse-oppslag av alle steder Term faktisk forekommer i importert lovtekst (maks 50 treff). */
export interface BegrepBruktIRettskildeDto {
  rettskildeId: string;
  nodeEid: string;
  rettskildeTittel: string;
  snippet: string;
}

/** Ett treff fra GET /api/begreper/{id}/taggede-forekomster — EKTE, taggkoblede forekomster
 * (TekstTaggEntitet.RefId == begrepets id), til forskjell fra BegrepBruktIRettskildeDto sitt rå
 * fulltekstsøk. Se TekstTaggTjeneste.ListerForRefIdAsync på serveren. */
export interface BegrepTaggetForekomstDto {
  taggId: string;
  rettskildeId: string;
  nodeEid: string;
  rettskildeTittel: string;
  quotePrefix: string;
  quoteExact: string;
  quoteSuffix: string;
}

// ---------- Begrepsforekomster — begrepsoppdagelse (M1/M11), docs/24, speiler RegelIde.Api/Dtos.cs ----------

/** Ett deterministisk (regex-basert) sveip-treff — arbeidskø, IKKE et register (docs/24 §1.1/§1.2).
 * 'kildetype': eksplisitt_liste|egen_paragraf|inline_menes|skal_forstas_som|copula|heretter_kalt|
 * ekstern_referanse|eos_referanse|vedleggstabell|distribuert. 'monsterId': "M1"|"M11" i praksis i dag.
 * 'konfidens': hoy|middels|lav|krever_oppslag. 'scope': hele_dokumentet|kapittel|paragraf.
 * 'status': Venter|Godkjent|Avvist (samme lette arbeidskø-modell som VirksomhetKandidatDto/NavnekandidatDto). */
export interface BegrepsforekomstDto {
  id: string;
  rettskildeId: string;
  nodeEid: string;
  startOffset: number;
  endOffset: number;
  begrep: string;
  begrepOriginal: string;
  definisjon: string | null;
  kildetype: string;
  monsterId: string;
  konfidens: string;
  scope: string;
  scopeRefEid: string | null;
  henvisningsMaal: string | null;
  status: string;
  begrepId: string | null;
  opprettetAv: string;
  opprettetTidspunkt: string;
  behandletAv: string | null;
  behandletTidspunkt: string | null;
}

/** [Ny, kandidatside-runden, 2026-09-09, issue #216] Massegodkjenning/-avvisning av
 * begrepsforekomster. `virksomhetId` gjelder HELE utvalget — hvilken virksomhets register begrepet
 * landes i kan ikke utledes, og et utvalg fra samme definisjonsparagraf hører til samme register.
 * Skal to begreper til ULIKE registre, er det to utvalg. Se backend-DTO-kommentaren. */
export interface GodkjennBegrepsforekomsterBatchRequest {
  ider: string[];
  virksomhetId: string;
}

export interface BegrepsforekomstBatchRequest {
  ider: string[];
}

export interface BegrepsforekomstBatchRadDto {
  id: string;
  ok: boolean;
  feil: string | null;
  resultat: BegrepsforekomstDto | null;
}

export interface BegrepsforekomstBatchResultatDto {
  rader: BegrepsforekomstBatchRadDto[];
}

/** rettskildeId=null sveiper HELE det importerte (delte/nasjonale) korpuset, satt snevrer inn til én rettskilde. */
export interface SveipBegrepsforekomsterRequest {
  rettskildeId: string | null;
}

export interface SveipBegrepsforekomsterResultatDto {
  antallTreffFunnet: number;
  antallNyeForekomster: number;
}

/** Godkjenning krever et eksplisitt valg av HVILKEN virksomhets register begrepet skal landes i — en
 * forekomst er delt/objektiv, men et Begrep-register krever en eier (se Dtos.cs/BegrepsforekomstTjeneste). */
export interface GodkjennBegrepsforekomstRequest {
  virksomhetId: string;
}

/** Resultat av DELETE /api/begrepsforekomster (massehardsletting) — kun 'Avvist'-rader rammes. */
export interface HardslettBegrepsforekomsterResultatDto {
  antallSlettet: number;
}

// ---------- Begrep-definisjon-relasjoner — «definert likt som» på tvers av forskrifter (issue #212) ----------

/** Ett foreslått par i kandidatkøen GET /api/begrep-definisjon-relasjoner — bærer begge underliggende
 * forekomster join'et inn (se BegrepDefinisjonRelasjonKandidatDto på serveren) slik at ordlyden fra
 * begge rettskilder kan vises uten et ekstra kall per rad. */
export interface BegrepDefinisjonRelasjonKandidatDto {
  id: string;
  normalisertDefinisjon: string;
  status: string;
  opprettetAv: string;
  opprettetTidspunkt: string;
  behandletAv: string | null;
  behandletTidspunkt: string | null;
  fra: BegrepsforekomstDto;
  til: BegrepsforekomstDto;
}

export interface BegrepDefinisjonRelasjonSveipResultatDto {
  antallGrupperFunnet: number;
  antallNyeKandidater: number;
}

/** Én bekreftet relasjon vist FRA et gitt begrep (BegrepDetalj — «også definert i N andre rettskilder»,
 * issue #212 akseptansekriterium 5). 'kilde': 'sveip' | 'manuell'. */
export interface BegrepDefinisjonRelasjonDto {
  relatertBegrepId: string;
  relatertTerm: string;
  relatertLovreferanseEid: string | null;
  relatertVirksomhetId: string | null;
  kilde: string;
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

/** Hvilken triggervei som startet en Lovdata-resynk-kjøring (administrasjon-Lovdata-resynk, GitHub-issue #104). */
export type LovdataResynkUtlost = 'Oppstart' | 'Manuell' | 'Planlagt';

export type LovdataResynkStatus = 'Pågår' | 'Fullført' | 'Feilet';

/**
 * Én rad i kjøre-historikken for Lovdata full-resynk — se LovdataResynkKjoringEntitet på serveren.
 * `nyeVersjoner` er tallet issue #104 ba om å vise TYDELIG: hvor mange dokumenter som faktisk fikk
 * endret INNHOLD ved denne kjøringen (til forskjell fra `nye`, som er nyoppdagede dokumenter) — det
 * finnes i dag INGEN godkjenningskø for disse (bevisst IKKE besluttet avveining, se PR-beskrivelsen).
 * Tellerne er alle `null` mens kjøringen fortsatt er `Pågår`, eller hvis den feilet HELT (Feilet) før
 * noe ble behandlet.
 *
 * `antallNyeKilderOppdaget`/`nyeKilderSveipUtfortTidspunkt` [Ny, aksjonskrok-runden, 2026-09-10, issue
 * #201 del B] — grunnlaget for varslingsraden ("N nye kilder oppdaget i kjøring X — kjør navnekandidat-
 * sveip?"). `antallNyeKilderOppdaget` teller KUN utfall 'Ny' (ikke 'ForfremmetStub'), til forskjell fra
 * `nye` over som slår sammen begge — se serverens LovdataFullimportTjeneste-kommentar.
 */
export interface LovdataResynkKjoringDto {
  id: string;
  utlost: LovdataResynkUtlost;
  utlostAvBruker: string | null;
  status: LovdataResynkStatus;
  startetTidspunkt: string;
  fullfortTidspunkt: string | null;
  nye: number | null;
  nyeVersjoner: number | null;
  uendret: number | null;
  feilet: number | null;
  totaltBehandlet: number | null;
  feilmelding: string | null;
  antallNyeKilderOppdaget: number;
  nyeKilderSveipUtfortTidspunkt: string | null;
}

/** [Ny, feillogg-runden, 2026-09-10, issue #201 del A] Ett feilet importforsøk i feilloggen for ÉN
 * kjøring — se GET /api/administrasjon/lovdata-resynk/{kjoringId}/feilede-dokumenter. */
export interface LovdataImportstatusHistorikkDto {
  id: string;
  datokode: string;
  type: string;
  tittel: string | null;
  eli: string;
  feilmelding: string | null;
  forsoktTidspunkt: string;
}

/** [Ny, aksjonskrok-runden, 2026-09-10, issue #201 del B] Svar fra bekreftelsesknappens
 * POST .../{kjoringId}/navnekandidat-sveip. */
export interface KjorNyeKilderSveipResultatDto {
  antallRettskilderForsokt: number;
  antallRettskilderHoppetOver: number;
  antallTreffFunnet: number;
  antallNyeKandidater: number;
}

/** `intervallTimer` null eller 0 = aldri automatisk (kun oppstart/manuell). */
export interface LovdataResynkInnstillingDto {
  intervallTimer: number | null;
  sistEndretTidspunkt: string;
  sistEndretAv: string | null;
}

export interface OppdaterLovdataResynkInnstillingRequest {
  intervallTimer: number | null;
}

/** DENNE sakens fire tilstander (issue #249) — IKKE navnekandidat-/virksomhetkandidat-mønsterets
 * Venter/Godkjent/Avvist. En kildefeil forblir en feil om den godkjennes eller ei; statusen her
 * beskriver OPPFØLGING, ikke godkjenning. Se KildefeilEntitet.Status på serveren. */
export type KildefeilStatus = 'Ny' | 'Kjent' | 'Rettet-hos-oss' | 'Venter-på-Lovdata';

/** Én rad i det varige kildefeil-registeret — se KildefeilEntitet på serveren. `type`/
 * `funnetAvMekanisme` er fri tekst (ikke et lukket sett i klienten heller), nettopp fordi et
 * FREMTIDIG sveip skal kunne skrive nye verdier uten en frontend-endring her (akseptansekriterium 3). */
export interface KildefeilDto {
  id: string;
  rettskildeId: string;
  rettskildeEid: string | null;
  type: string;
  beskrivelse: string;
  funnetAvMekanisme: string;
  status: KildefeilStatus;
  opprettetAv: string;
  opprettetTidspunkt: string;
}

/** Svaret fra POST /api/administrasjon/hjemmel-validering/registrer-kildefeil. */
export interface KildefeilRegistreringDto {
  totaltFunnet: number;
  nyeRegistrert: number;
  totaltRegistrertForMekanismen: number;
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

/** Massegodkjenning/-avvisning av begrepsforslag — egen DTO-familie, se NavnekandidatBatchRequest over
 * for begrunnelsen om hvorfor hver kø har sin egen fremfor en delt/generisk variant. */
export interface BegrepsforslagBatchRequest {
  ider: string[];
}

export interface BegrepsforslagBatchRadDto {
  id: string;
  ok: boolean;
  feil: string | null;
  resultat: BegrepDto | null;
}

export interface BegrepsforslagBatchResultatDto {
  rader: BegrepsforslagBatchRadDto[];
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

/** Massegodkjenning/-avvisning av tjenesteforslag — egen DTO-familie, se NavnekandidatBatchRequest over. */
export interface TjenesteforslagBatchRequest {
  ider: string[];
}

export interface TjenesteforslagBatchRadDto {
  id: string;
  ok: boolean;
  feil: string | null;
  resultat: TjenesteDto | null;
}

export interface TjenesteforslagBatchResultatDto {
  rader: TjenesteforslagBatchRadDto[];
}

/** Massesletting av ubehandlede tjenesteforslag — dekker BÅDE «Ventende forslag» sin enkeltrad-Slett
 * OG «Mine forslag til andre virksomheter» sin Slett-alle-merkede (samme SlettForslagAsync-endepunkt
 * uansett hvilken seksjon knappen står i, se backend-DTO-kommentaren). */
export interface TjenesteforslagSlettBatchRadDto {
  id: string;
  ok: boolean;
  feil: string | null;
}

export interface TjenesteforslagSlettBatchResultatDto {
  rader: TjenesteforslagSlettBatchRadDto[];
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



// ---------- [Ny, issue #378] Aktørsveip, generiske navneformer og bindinger ----------

/** [Ny, issue #378 A1] Aktørstatus i aktørsveipet og lasterapporten — speilet av `Aktorstatus` i RegelIde.Data. */
export type Aktorstatus = 'kjent' | 'ny_navneform_for_kjent_aktor' | 'ny_aktor' | 'generisk' | 'flertydig';

export interface AktorsveipRad {
  aktorId: string; tekstform: string; varianter: string[]; status: Aktorstatus; forekomster: number; lastbareForekomster: number;
  eidEksempel: string; treff: string[]; kandidater: string[]; antallKandidater: number; generiskNavneformId: string | null;
  bundneForekomster: number; foreslatteBindinger: number; referent: StrukturnodeDto | null; navnekandidatId: string | null;
  /** [Ny, issue #440] Aktørkøens rute for tekstformen. Bare 'aktorko' legges i navnekandidatkøen, [ENDRET, Johanns avgjørelse 2026-10-31]
   * pluss generisk ord uten binding (`blirNavnekandidatIAktorsveipet` i kandidater/aktorkorute.ts). */
  aktorkoRute: Aktorkorute | null;
}
export interface AktorsveipRapport {
  rettskildeId: string; rettskildeTittel: string; lag: 'monster' | 'ki'; noderLest: number; utsagnTotalt: number; aktorerTotalt: number;
  perStatus: { status: Aktorstatus; antall: number }[]; aktorer: AktorsveipRad[]; navnekandidaterSkrevet: boolean;
  /** [Ny, issue #385 AC3] Hva navnetreffene ga når sveipet skrev (null i tørrkjøringen). */
  aktoridentitet?: Treffidentitet | null;
}

/** [Ny, issue #378 B5] Én binding av en generisk navneform i én rettskilde. */
export interface NavneformBindingDto {
  id: string; navneformId: string; term: string; rettskildeId: string; rettskildeTittel: string;
  paragrafspenn: ParagrafspennParDto[]; referent: StrukturnodeDto;
  hjemmelRettskildeId: string | null; hjemmelEid: string | null;
  kildeUtenforKorpusTekst: string | null; kildeUtenforKorpusLenke: string | null;
  kildeUtenforKorpusType: KildeUtenforKorpusType | null; kildeUtenforKorpusDokumentasjon: KildeUtenforKorpusDokumentasjon | null;
  kunnskapsstatus: Kunnskapsstatus | null; begrunnelse: string | null; tolkningsargumenter: Tolkningsargument[] | null;
  status: Godkjenning; oppdagelsesKilde: string; kommentar: string | null; avvisningBegrunnelse: string | null;
  opprettetAv: string; opprettetTidspunkt: string; sistEndretAv: string | null; sistEndretTidspunkt: string | null;
  /** [Ny, issue #378, Johanns beslutning 2026-10-16] Bindingens egen gyldighet (ISO-dato). Null = åpen ende. */
  gyldigFra: string | null; gyldigTil: string | null;
  /** [Ny, issue #384] Verket bindingen følger (ELI), versjonen den ble registrert mot, og den gjeldende versjonen av verket. */
  verkEli?: string | null; registrertVersjon?: number; gjeldendeRettskildeId?: string;
  /** [Ny, issue #385 AC12] Satt når teksten i hjemmelen eller spennet er endret i en ny versjon. */
  hjemmelEndretTidspunkt?: string | null;
  /** [Ny, issue #385 AC8] Alle lenkene til dokumentasjonen. */
  kildeUtenforKorpusLenker?: string[] | null;
}
/** [Ny, issue #378 AC7] «Hvor brukes ‹styret›, og hva betyr det i hver rettskilde?» */
export interface GeneriskNavneformOversikt {
  navneformId: string; term: string; antallTagger: number;
  rettskilder: { rettskildeId: string; rettskildeTittel: string; antallTagger: number; bindinger: NavneformBindingDto[] }[];
}
/** [Ny, issue #378 B6] Oppløsningen av én tagg mot en generisk navneform. */
export interface GeneriskTaggOppslag {
  // [ENDRET, issue #384] + 'spenn_endret': en godkjent binding kunne ikke flyttes entydig til denne versjonen (ingen referent).
  // [ENDRET, issue #394] + 'regel': standardregelen ga referenten (ingen vurdert binding dekker forekomsten).
  taggId: string; navneformId: string; status: 'bundet' | 'regel' | 'ubundet' | 'flertydig' | 'spenn_endret'; bindingId: string | null;
  referent: StrukturnodeDto | null;
  /** [Ny, issue #394 AK 2] Termen, kilden (binding/regel), bindingens godkjenning og standardregelens svar. */
  term?: string; kilde?: 'binding' | 'regel' | null; bindingStatus?: Godkjenning | null; bindingKunnskapsstatus?: Kunnskapsstatus | null;
  regel?: StandardregelSvar | null;
}
/** [Ny, issue #394 AK 1] En standardregel for en generisk navneform. */
export interface GeneriskStandardregel {
  term: string; regelkode: string; navn: string; forklaring: string; grunnlag: string; kunnskapsstatus: Kunnskapsstatus;
}
/** [Ny, issue #394 AK 1] Ett premiss et standardregelsvar bygger på. */
export interface StandardregelPremiss {
  type: 'ressortkant' | 'lovdata_felt' | 'klasse' | 'navnehistorikk'; tekst: string; kantId: string | null; status: Godkjenning | null;
  kunnskapsstatus: Kunnskapsstatus | null; hjemmelRettskildeId: string | null; hjemmelEid: string | null; dokumentasjon: 'primaer' | 'sekundaer' | null;
}
/** [Ny, issue #394 AK 1] Svaret fra en standardregel for én rettskilde. */
export interface StandardregelSvar {
  term: string; regelkode: string; regelnavn: string; utfall: 'lost' | 'ikke_lost' | 'flertydig';
  // [ENDRET, Johanns beslutning på PR #398] + 'lovdata_utgatt_navn': Lovdatas felt bruker et utgått navn som navnehistorikken fører til én aktør.
  rute: 'ressortfordeling' | 'lovdata' | 'lovdata_utgatt_navn' | 'fast_referent' | null; referentVisning: StrukturnodeDto | null;
  kunnskapsstatus: Kunnskapsstatus; dokumentasjon: 'primaer' | 'sekundaer' | null; oppslagsdato: string;
  gyldigFra: string | null; gyldigTil: string | null; forklaring: string; premisser: StandardregelPremiss[]; merknader: string[];
  referentNedlagt: string | null;
  /** [Ny, Johanns avgjørelse på #397] Navnet er brukt av flere identiteter; svaret er valgt etter dato. */
  valgtEtterDato?: boolean;
}
/** [Ny, issue #394 AK 4] Ett forslag til hva en generisk omtale betyr, med ferdig utfylt grunnlag. */
export interface GeneriskReferentforslag {
  kilde: 'standardregel' | 'ansvarlig_departement' | 'samme_rettskilde' | 'navneform' | 'tidligere_binding' | 'ny_aktor_fra_kandidat';
  /** `type: 'ny'` = en navnekandidat som ikke finnes som aktør ennå (id-en er kandidatens); aktøren opprettes ved lagring. */
  referent: Omit<StrukturnodeDto, 'type'> & { type: StrukturnodeDto['type'] | 'ny' }; grunn: string; rang: number; kunnskapsstatus: Kunnskapsstatus; tolkningsargumenter: Tolkningsargument[];
  begrunnelse: string; trengerIkkeBinding: boolean;
}
/** [Ny, issue #394 AK 4] Alt veiviserens én-skjerm trenger for én kandidat. */
export interface GeneriskKandidatkontekst {
  kandidatId: string; term: string; rettskildeId: string; rettskildeTittel: string; nodeEid: string; navneformId: string | null;
  standardregel: GeneriskStandardregel | null; regelsvar: StandardregelSvar | null; forslag: GeneriskReferentforslag[];
  forekomsterIRettskilden: number; bindingerHer: NavneformBindingDto[];
}
/** [Ny, issue #378 B5] Bindingen veiviseren og API-et sender. */
export interface NavneformBindingRequest {
  navneformId?: string; rettskildeId?: string; tilVirksomhetId: string | null; tilBegrepId: string | null;
  paragrafspenn: ParagrafspennParDto[]; hjemmelRettskildeId: string | null; hjemmelEid: string | null;
  kildeUtenforKorpusTekst: string | null; kildeUtenforKorpusLenke: string | null;
  kildeUtenforKorpusType: KildeUtenforKorpusType | null; kildeUtenforKorpusDokumentasjon: KildeUtenforKorpusDokumentasjon | null;
  kunnskapsstatus: Kunnskapsstatus | null; begrunnelse: string | null; tolkningsargumenter: Tolkningsargument[] | null;
  status: Godkjenning; kommentar: string | null;
  gyldigFra: string | null; gyldigTil: string | null;
  /** [Ny, issue #385 AC8] Én eller flere lenker. */
  kildeUtenforKorpusLenker?: string[] | null;
}

// ---------- [Ny, issue #385] Aktøridentitet ----------

/** «Hva bygger dette på?» */
export type Grunnlagstype = 'hjemmel' | 'kilde' | 'mangler';

/** Veiviserens to nye steg som ett objekt — speilet av `UtsagnsgrunnlagRequest` i API-et. */
export interface UtsagnsgrunnlagRequest {
  grunnlag: Grunnlagstype;
  hjemmelRettskildeId: string | null; hjemmelEid: string | null;
  kildeTekst: string | null; kildeType: KildeUtenforKorpusType | null; kildeDokumentasjon: KildeUtenforKorpusDokumentasjon | null;
  kildeLenker: string[] | null;
  kunnskapsstatus: Kunnskapsstatus | null; godkjenning: Godkjenning; begrunnelse: string | null; tolkningsargumenter: Tolkningsargument[] | null;
}

export interface Treffidentitet {
  navneformForslag: number; kilderSatt: number; taggerOpprettet: number; taggerFantes: number; taggerUtenEier: number;
  taggerKolliderer: number; ulikSkrivemaate: number; aktortypeForslag: number;
}

export type Navneformforslagutfall = 'foreslatt' | 'forslag_finnes' | 'tidligere_avvist' | 'navneform_finnes' | 'ingen_treff'
  | 'ingen_egennavnform' | 'flere_former';
export interface Navneformforslagrad {
  virksomhetId: string; registernavn: string; organisasjonsnummer: string | null; utfall: Navneformforslagutfall; form: string | null;
  kunnskapsstatus: Kunnskapsstatus | null; rettskildeId: string | null; rettskildeTittel: string | null; eid: string | null;
  forekomster: number; rettskilder: number; former: string[]; navneformId: string | null;
}
export interface NavneformforslagRapport { skrevet: boolean; aktorerUtenGjeldendeNavneform: number; foreslatt: number; rader: Navneformforslagrad[] }
export interface EtterfyllingRapport {
  skrevet: boolean; kantTreff: number; aktorer: number; tall: Treffidentitet;
  rader: { virksomhetId: string; aktor: string; rettskildeId: string; rettskildeTittel: string; noder: number; tall: Treffidentitet }[];
}

export interface AktortypeUtsagnDto {
  id: string; virksomhetId: string; aktortype: Aktortype; ordningstype: Ordningstype | null;
  hjemmelRettskildeId: string | null; hjemmelEid: string | null;
  kildeUtenforKorpusTekst: string | null; kildeUtenforKorpusType: KildeUtenforKorpusType | null;
  kildeUtenforKorpusDokumentasjon: KildeUtenforKorpusDokumentasjon | null; kildeUtenforKorpusLenker: string[] | null;
  kunnskapsstatus: Kunnskapsstatus | null; begrunnelse: string | null; tolkningsargumenter: Tolkningsargument[] | null;
  status: Godkjenning; oppdagelsesKilde: string; avvisningBegrunnelse: string | null;
  opprettetAv: string; opprettetTidspunkt: string; sistEndretAv: string | null; sistEndretTidspunkt: string | null;
}

/** Felles tillegg til kobl-til-endepunktene (issue #385 AC6–AC10). Valgfrie. */
export interface VeivisergrunnlagFelt {
  grunnlag?: UtsagnsgrunnlagRequest | null;
  aktortype?: Aktortype | null;
  ordningstype?: Ordningstype | null;
  /** [Ny, issue #386] Aktøren ble opprettet med bare et navn i denne veiviserkjøringen: navnet blir gjeldende navneform med samme grunnlag. */
  nyAktorMedBareNavn?: boolean;
}

// ---------------------------------------------------------------------------------------------------------------------------------
// [Ny, issue #429 «Funn til vurdering», 2026-10-30] Registeret — se FunnEndepunkter.cs og Funn.cs på serveren.
export type FunnKategori = 'utgatt_aktor' | 'mulig_motstrid' | 'uklarhet_hull';
export type FunnStatus = 'oppdaget' | 'juristvurdert' | 'godkjent_for_publisering' | 'forklart_bort';
export type FunnKoblingsrolle = 'gjelder_bestemmelse' | 'motstridende_bestemmelse' | 'berort_aktor' | 'navneform' | 'etterfolger' | 'kilde';
export type FunnVurderingspunktType = 'bortforklaring_provd' | 'vurdering' | 'ikke_avgjort';

export interface FunnKoblingDto {
  id: string;
  rolle: FunnKoblingsrolle;
  sortering: number;
  etikett: string;
  merknad: string | null;
  rettskildeEli: string | null;
  rettskildeId: string | null;
  eid: string | null;
  nodeId: string | null;
  virksomhetId: string | null;
  begrepId: string | null;
  strukturkantId: string | null;
  kildefeilId: string | null;
  eksternLenke: string | null;
}

export interface FunnVurderingspunktDto {
  id: string;
  type: FunnVurderingspunktType;
  sortering: number;
  overskrift: string | null;
  tekst: string;
  sikkerhet: string | null;
}

/** Den offentlige representasjonen — nøyaktig det som eksporteres til nettside/data/funn/register.json. */
export interface FunnOffentlig {
  id: string;
  kategori: FunnKategori;
  undertype: string | null;
  tittel: string;
  beskrivelse: string;
  vurdering: string | null;
  bestemmelser: { rolle: string; etikett: string; merknad: string | null; eli: string | null; eid: string | null }[];
  navneformer: { etikett: string; merknad: string | null }[];
  aktorer: { etikett: string; merknad: string | null }[];
  etterfolgere: { etikett: string; merknad: string | null }[];
  bortforklaringer: { overskrift: string | null; tekst: string }[];
  vurderingspunkter: { overskrift: string | null; tekst: string; sikkerhet: string | null }[];
  ikkeAvgjort: { overskrift: string | null; tekst: string }[];
  kilder: { etikett: string; merknad: string | null; lenke: string | null; eli: string | null; eid: string | null }[];
  juristvurdertAvKi: boolean;
  godkjentDato: string;
}

export interface FunnDto {
  id: string;
  eksternNokkel: string | null;
  kategori: FunnKategori;
  undertype: string | null;
  tittel: string;
  beskrivelse: string;
  vurdering: string | null;
  internMerknad: string | null;
  status: FunnStatus;
  oppdagelsesKilde: string;
  funnetI: string | null;
  opprettetAv: string;
  opprettetTidspunkt: string;
  sistEndretAv: string | null;
  sistEndretTidspunkt: string | null;
  juristvurdertAv: string | null;
  juristvurdertTidspunkt: string | null;
  vurdertAvType: 'menneske' | 'ki_agent' | null;
  forklartBortBegrunnelse: string | null;
  forklartBortAv: string | null;
  forklartBortTidspunkt: string | null;
  godkjentAv: string | null;
  godkjentTidspunkt: string | null;
  kildeEndretTidspunkt: string | null;
  koblinger: FunnKoblingDto[];
  vurderingspunkter: FunnVurderingspunktDto[];
  /** Premissporten (docs/33 §4.6): tom = åpen. */
  premissmangler: string[];
  /** Det som blir publisert. null for funn som er forklart bort, og i lista. */
  forhandsvisning: FunnOffentlig | null;
}

export interface FunnTilgangDto {
  kanPublisere: boolean;
  kanJuristvurdere: boolean;
  publiseringsansvarligeKonfigurert: boolean;
}

export interface FunnKoblingInn {
  rolle: FunnKoblingsrolle;
  etikett: string;
  merknad?: string | null;
  rettskildeEli?: string | null;
  eid?: string | null;
  virksomhetId?: string | null;
  begrepId?: string | null;
  strukturkantId?: string | null;
  kildefeilId?: string | null;
  eksternLenke?: string | null;
}

export interface FunnVurderingspunktInn {
  type: FunnVurderingspunktType;
  tekst: string;
  overskrift?: string | null;
  sikkerhet?: string | null;
}

export interface NyttFunnInn {
  kategori: FunnKategori;
  undertype: string | null;
  tittel: string;
  beskrivelse: string;
  oppdagelsesKilde?: string;
  internMerknad?: string | null;
  funnetI?: string | null;
  koblinger: FunnKoblingInn[];
  vurderingspunkter: FunnVurderingspunktInn[];
}

// ---------- [Ny, issue #440 PR 1] Aktørkøen (bare lesing) — speilet av AktorkoTjeneste i RegelIde.Data ----------

/** Rutene en ventende navnekandidat kan havne i, i den rekkefølgen reglene prøves. Bare 'aktorko' står i køen. */
export type Aktorkorute = 'generisk' | 'kjent_aktor' | 'kjent_begrep' | 'boyningsform' | 'aktorko';

export interface AktorkoOrdtreff {
  ord: string; rute: Aktorkorute; forekomster: number;
  /** Det kjente ordet som traff, i klart språk. Null i aktørkøen. */
  treff: string | null;
  /** For en bøyningsform: ordet den ligner, ruten til det ordet og suffikset («+r», «−et»). */
  ligner: string | null; boyningAvRute: Aktorkorute | null; suffiks: string | null;
  /** Bare for et eksakt generisk ord. En bøyningsform arver den aldri. */
  standardregel: string | null;
}
export interface AktorkoRutetall {
  rute: Aktorkorute; tekst: string; forekomster: number; rettskilder: number; ulikeOrd: number; topp: AktorkoOrdtreff[];
}
export interface AktorkoFordeling {
  ventende: number; ruter: AktorkoRutetall[]; antallNavn: number; medFlereForekomster: number; ordIKoen: number; genitivforslag: number;
  hyppigste: AktorkoOrdtreff[];
}
export interface AktorkoSkrivemate { tekst: string; forekomster: number }
export interface AktorkoVariant {
  normalisert: string; regel: 'samme_ord' | 'boyning'; boyningAv: string | null; suffiks: string | null; forekomster: number;
  skrivemater: AktorkoSkrivemate[];
}
export interface AktorkoHjemmelforslag {
  rettskildeId: string; rettskilde: string; kortnavn: string | null; eid: string; bestemmelse: string | null; uttrykk: string;
  setning: string; treff: string; treffStart: number; kunnskapsstatus: string;
}
export interface AktorkoRad {
  nokkel: string; navn: string; forekomster: number; rettskilder: number; varianter: AktorkoVariant[]; rettskildeIder: string[];
  hjemmelforslag: number; forsteHjemmelforslag: AktorkoHjemmelforslag | null; genitivforslagInn: number; bareMerknader: boolean;
}
export interface AktorkoListe { ventende: number; antallNavn: number; medFlereForekomster: number; navn: AktorkoRad[] }
export interface AktorkoGenitivforslag { fraNokkel: string; fraVariant: string; tilNokkel: string; etterledd: string; forekomster: number }
export interface AktorkoForekomst {
  navnekandidatId: string; tekst: string; eid: string; bestemmelse: string | null;
  /** Setningen; null når nodeteksten er endret siden sveipet (da vises ikke et gjettet utdrag). */
  utdrag: string | null; utdragStart: number; erMerknad: boolean; kategori: string; oppdagelseskilde: string | null; konfidens: string | null;
}
export interface AktorkoRettskildeForekomster { rettskildeId: string; tittel: string; kortnavn: string | null; forekomster: AktorkoForekomst[] }
export interface AktorkoDetalj {
  nokkel: string; navn: string; forekomster: number; varianter: AktorkoVariant[]; genitivforslagInn: AktorkoGenitivforslag[];
  genitivforslagUt: AktorkoGenitivforslag[]; perRettskilde: AktorkoRettskildeForekomster[]; hjemmelforslag: AktorkoHjemmelforslag[];
  hjemmelregel: string;
}
