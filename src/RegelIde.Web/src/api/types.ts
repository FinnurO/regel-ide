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
