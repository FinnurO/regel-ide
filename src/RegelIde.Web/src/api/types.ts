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

export interface RettskildeReferanseDto {
  id: string;
  fraNodeId: string;
  tilRettskildeId: string;
  tilEid: string;
}

export interface BrukerDto {
  id: string;
  navn: string;
  virksomhetId: string;
  virksomhetNavn: string;
  rolle: string;
}

export interface VirksomhetDto {
  id: string;
  navn: string;
  organisasjonsnummer: string | null;
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
}

export interface TjenesteDto {
  id: string;
  virksomhetId: string;
  tittel: string;
  beskrivelse: string | null;
  kompetentMyndighet: string | null;
  output: string | null;
  tjenestetype: string | null;
  malgruppe: string | null;
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
}

export interface TjenesteRequest {
  tittel: string;
  beskrivelse: string | null;
  kompetentMyndighet: string | null;
  output: string | null;
  tjenestetype: string | null;
  malgruppe: string | null;
  kanaler: string[] | null;
  kostnad: string | null;
  behandlingstid: string | null;
  kontaktpunkt: string | null;
  konsekvensVedBrudd: string | null;
  sprak: string[] | null;
}

export interface TjenesteRegelverksreferanseDto {
  id: string;
  tjenesteId: string;
  tilRettskildeId: string;
  tilEid: string;
}

export interface KobleRegelverksreferanseRequest {
  tilRettskildeId: string;
  tilEid: string;
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

/** Motsatt retning av tjenestens regelverksreferanser — hvilke tjenester som refererer denne rettskilden. */
export interface TjenesteReferanseDto {
  tjenesteId: string;
  tjenesteTittel: string;
  tilEid: string;
}
