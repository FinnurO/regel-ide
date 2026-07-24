using RegelIde.Data;

namespace RegelIde.Api;

/// <summary>
/// Lett sammendrag for listeendepunktet. <see cref="Id"/> er databaseradens Guid — det låste
/// skjemaet (§2 i teknisk design) har ingen egen "datokode"-kolonne, kun (nullable) ELI, så Guid-en
/// er den naturlige, alltid-URL-sikre nøkkelen for enkeltoppslag.
/// </summary>
public sealed record RettskildeSammendrag(Guid Id, Guid? VirksomhetId, string? Eli, string Tittel, string? Kortnavn, string Kildetype)
{
    public static RettskildeSammendrag FraEntitet(RettskildeEntitet r) =>
        new(r.Id, r.VirksomhetId, r.Eli, r.Tittel, r.Kortnavn, r.Kildetype);
}

/// <summary>Full rettskilde: metadata + kanonisk AKN-XML (§1 i teknisk design).</summary>
public sealed record RettskildeDetalj(
    Guid Id, Guid? VirksomhetId, string Doctype, string Kildetype, string Tittel, string? Kortnavn, string? Eli,
    DateOnly? Ikrafttredelse, DateOnly? KonsolidertDato, string? Utgiver, string Status, string? AknXml)
{
    public static RettskildeDetalj FraEntitet(RettskildeEntitet r) => new(
        r.Id, r.VirksomhetId, r.Doctype, r.Kildetype, r.Tittel, r.Kortnavn, r.Eli,
        r.Ikrafttredelse, r.KonsolidertDato, r.Utgiver, r.Status, r.AknXml);
}

/// <summary>Forespørsel for POST /api/rettskilder/lovdata.</summary>
public sealed record LovdataImportRequest(string Datokode);

/// <summary>Én node i rettskildens tre (kapittel/underinndeling/paragraf/ledd/punkt), for tre-navigasjon.</summary>
public sealed record RettskildeNodeDto(
    Guid Id, string Eid, Guid? ParentNodeId, string NodeType, string? Nummer, string? Overskrift, string? Tekst)
{
    public static RettskildeNodeDto FraEntitet(RettskildeNodeEntitet n) =>
        new(n.Id, n.Eid, n.ParentNodeId, n.NodeType, n.Nummer, n.Overskrift, n.Tekst);
}

/// <summary>Kryssreferanse funnet i løpeteksten (intern eller ekstern, §3.1 steg 6).</summary>
public sealed record RettskildeReferanseDto(Guid FraNodeId, Guid TilRettskildeId, string TilEid)
{
    public static RettskildeReferanseDto FraEntitet(RettskildeReferanseEntitet r) =>
        new(r.FraNodeId, r.TilRettskildeId, r.TilEid);
}

/// <summary>Tekst-tag (§1.2 i domenemodellen, AK-3.3.1–3.3.4). `RefId` er alltid null i byggesteg 1.</summary>
public sealed record TekstTaggDto(
    Guid Id, Guid RettskildeId, string NodeEid, int StartOffset, int EndOffset,
    string QuotePrefix, string QuoteExact, string QuoteSuffix, string Kind, Guid? RefId, string OpprettetAv)
{
    public static TekstTaggDto FraEntitet(TekstTaggEntitet t) => new(
        t.Id, t.RettskildeId, t.NodeEid, t.StartOffset, t.EndOffset,
        t.QuotePrefix, t.QuoteExact, t.QuoteSuffix, t.Kind, t.RefId, t.OpprettetAv);
}

/// <summary>Forespørsel for POST /api/rettskilder/{id}/tagger.</summary>
public sealed record OpprettTekstTaggRequest(
    string NodeEid, int StartOffset, int EndOffset, string QuotePrefix, string QuoteExact, string QuoteSuffix, string Kind);
