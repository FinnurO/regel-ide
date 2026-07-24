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

export interface ApiFeil {
  feil: string;
}
