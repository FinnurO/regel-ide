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

export interface ApiFeil {
  feil: string;
}
