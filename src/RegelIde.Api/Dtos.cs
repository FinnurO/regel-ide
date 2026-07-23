using RegelIde.Kildekonvertering;

namespace RegelIde.Api;

/// <summary>Lett sammendrag for listeendepunktet — full AKN-XML/nodetre hentes per rettskilde, ikke i listen.</summary>
public sealed record RettskildeSammendrag(string Datokode, string Eli, string Tittel, string? Kortnavn, Kildetype Kildetype)
{
    public static RettskildeSammendrag FraMetadata(RettskildeMetadata m) =>
        new(m.Datokode, m.Eli, m.Tittel, m.Kortnavn, m.Kildetype);
}

/// <summary>Full rettskilde: metadata + kanonisk AKN-XML (§1 i teknisk design).</summary>
public sealed record RettskildeDetalj(RettskildeMetadata Metadata, string AknXml, DateOnly ImportDato)
{
    public static RettskildeDetalj FraResultat(KonverteringResultat r) => new(r.Metadata, r.AknXml, r.ImportDato);
}
