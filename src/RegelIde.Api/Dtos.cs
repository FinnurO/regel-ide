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

/// <summary>Forespørsel for PATCH /api/rettskilder/{id}/metadata — AK-3.3.6, kun Kortnavn/Utgiver.</summary>
public sealed record OppdaterRettskildeMetadataRequest(string? Kortnavn, string? Utgiver);

/// <summary>Én node i rettskildens tre (kapittel/underinndeling/paragraf/ledd/punkt), for tre-navigasjon.</summary>
public sealed record RettskildeNodeDto(
    Guid Id, string Eid, Guid? ParentNodeId, string NodeType, string? Nummer, string? Overskrift, string? Tekst,
    bool Opphevet, DateOnly? OpphevetDato, int Versjon, HandbokKommentarMetadataDto? HandbokMetadata)
{
    public static RettskildeNodeDto FraEntitet(RettskildeNodeEntitet n) => new(
        n.Id, n.Eid, n.ParentNodeId, n.NodeType, n.Nummer, n.Overskrift, n.Tekst, n.Opphevet, n.OpphevetDato,
        n.Versjon, n.HandbokMetadata is null ? null : HandbokKommentarMetadataDto.FraEntitet(n.HandbokMetadata));
}

/// <summary>Håndbok-kommentarseksjonens 1:1-metadata (docs/03-domenemodell.md §1.1.1). Kun satt for kommentar-noder.</summary>
public sealed record HandbokKommentarMetadataDto(
    string Dokumenttype, bool Bindende, string FesteNiva, string Status, string? Revisjonsgrunn,
    DateOnly? Publisert, DateOnly? SistFagligEndret, IReadOnlyList<string> Marginord)
{
    public static HandbokKommentarMetadataDto FraEntitet(HandbokKommentarMetadataEntitet m) => new(
        m.Dokumenttype, m.Bindende, m.FesteNiva, m.Status, m.Revisjonsgrunn, m.Publisert, m.SistFagligEndret, m.Marginord);
}

/// <summary>Forespørsel for POST /api/handboker.</summary>
public sealed record OpprettHandbokRequest(string Tittel);

/// <summary>Forespørsel for POST /api/handboker/{id}/kapitler.</summary>
public sealed record OpprettKapittelNodeRequest(Guid? ParentNodeId, string Nummer, string? Overskrift);

/// <summary>Forespørsel for POST /api/handboker/{id}/kommentarer.</summary>
public sealed record OpprettKommentarNodeRequest(
    Guid ParentNodeId, string Nummer, string? Overskrift, string TekstHtml,
    string Dokumenttype, string FesteNiva, IReadOnlyList<string>? Marginord);

/// <summary>Forespørsel for PUT /api/handboker/{id}/kommentarer/{nodeId} — oppretter alltid en ny versjon.</summary>
public sealed record RedigerKommentarNodeRequest(
    string TekstHtml, string? Overskrift, string Dokumenttype, string FesteNiva, IReadOnlyList<string>? Marginord);

/// <summary>Forespørsel for POST .../lovreferanser.</summary>
public sealed record KobleLovreferanseRequest(Guid TilRettskildeId, string TilEid);

/// <summary>Forespørsel for POST .../revisjonsmerke — AK-3.3.12.</summary>
public sealed record SettRevisjonsmerkeRequest(string Revisjonsgrunn);

/// <summary>Forespørsel for POST .../publiser — AK-3.3.11. GodkjentAv er påkrevd kun for bindende seksjoner.</summary>
public sealed record PubliserKommentarRequest(string? GodkjentAv);

/// <summary>Kryssreferanse funnet i løpeteksten (intern eller ekstern, §3.1 steg 6).</summary>
public sealed record RettskildeReferanseDto(Guid Id, Guid FraNodeId, Guid TilRettskildeId, string TilEid)
{
    public static RettskildeReferanseDto FraEntitet(RettskildeReferanseEntitet r) =>
        new(r.Id, r.FraNodeId, r.TilRettskildeId, r.TilEid);
}

/// <summary>Tekst-tag (§1.2 i domenemodellen, AK-3.3.1–3.3.4). `RefId` er alltid null i byggesteg 1.</summary>
public sealed record TekstTaggDto(
    Guid Id, Guid RettskildeId, string NodeEid, int StartOffset, int EndOffset,
    string QuotePrefix, string QuoteExact, string QuoteSuffix, string Kind, Guid? RefId, string OpprettetAv,
    bool KreverGjennomgang)
{
    public static TekstTaggDto FraEntitet(TekstTaggEntitet t) => new(
        t.Id, t.RettskildeId, t.NodeEid, t.StartOffset, t.EndOffset,
        t.QuotePrefix, t.QuoteExact, t.QuoteSuffix, t.Kind, t.RefId, t.OpprettetAv, t.KreverGjennomgang);
}

/// <summary>Forespørsel for POST /api/rettskilder/{id}/tagger.</summary>
public sealed record OpprettTekstTaggRequest(
    string NodeEid, int StartOffset, int EndOffset, string QuotePrefix, string QuoteExact, string QuoteSuffix, string Kind);

/// <summary>Konfigurerbare tag-kinds (2026-07-25, erstatter en tidligere hardkodet liste).</summary>
public sealed record TaggKindKonfigurasjonDto(string Kode, string Navn, string Farge)
{
    public static TaggKindKonfigurasjonDto FraEntitet(TaggKindKonfigurasjonEntitet k) => new(k.Kode, k.Navn, k.Farge);
}
