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
}

export interface RettskildeReferanseDto {
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

export type TaggKind = 'begrep' | 'tjeneste' | 'vilkar' | 'regel';

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

export interface ApiFeil {
  feil: string;
}
