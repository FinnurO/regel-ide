namespace RegelIde.Kildekonvertering;

/// <summary>
/// Node-typer per docs/08-byggesteg1-teknisk-design.md §2 (rettskilde_noder.node_type).
/// Enum-navnene (lowercased) matcher DB-verdiene direkte, se <see cref="NodeTypeExtensions.TilDbVerdi"/>.
/// </summary>
public enum NodeType
{
    Kapittel,
    Underinndeling,
    Paragraf,
    Ledd,
    Punkt,
}

public static class NodeTypeExtensions
{
    public static string TilDbVerdi(this NodeType type) => type.ToString().ToLowerInvariant();
}

public enum Kildetype
{
    Lov,
    Forskrift,
}

/// <summary>En fotnote knyttet til en paragraf, modellert som AKN &lt;authorialNote&gt; (§3.2 i teknisk design), atskilt fra hovedteksten.</summary>
public sealed record Fotnote(string Etikett, string Tekst);

/// <summary>
/// Én tekstflate innenfor et ledd/punkt: enten ren tekst, eller en intern/ekstern kryssreferanse
/// (fra &lt;a href="lov/…"&gt; i løpeteksten). Brukes til å bygge både ren søketekst (§ i tekst_hash)
/// og AKN-serialiseringens &lt;ref&gt;-elementer (§1.3) fra samme kilde, uten å hente ut teksten to ganger.
/// </summary>
public sealed record TekstSegment(string Tekst, string? ReferanseTilEid, bool ErInternReferanse);

/// <summary>
/// Rå kryssreferanse funnet i løpeteksten til en ledd/punkt-node (§3.1 steg 6).
/// <see cref="TilEid"/> er en best-effort-konstruksjon (§1.2s deterministiske utvidelse) —
/// hvorvidt målet allerede finnes i biblioteket eller må opprettes som referanse-stub er en
/// beslutning som krever databasetilgang, og er derfor bevisst utenfor denne rene pipelinens scope
/// (se docs/06-veikart.md byggesteg 1 og 08-byggesteg1-teknisk-design.md §3.1 steg 6).
/// </summary>
public sealed record RettskildeReferanse(
    string FraNodeEid,
    string TilEid,
    bool ErInternReferanse,
    Kildetype? TilKildetype,
    string? TilDatokode
);

public sealed record RettskildeNode
{
    public required string Eid { get; init; }
    public string Kildesystem { get; init; } = "lovdata";
    public required string KildeId { get; init; }
    public string? ParentEid { get; init; }
    public required NodeType NodeType { get; init; }
    public string? Nummer { get; init; }
    public string? Overskrift { get; init; }

    /// <summary>Kun for ledd/punkt-noder (bladtekst) — ren tekst, tagger fjernet. Se §2 i teknisk design.</summary>
    public string? Tekst { get; init; }

    /// <summary>SHA-256 av normalisert tekst, §3.4. Null for noder uten Tekst.</summary>
    public string? TekstHash { get; init; }

    public required int SorteringsRekkefolge { get; init; }

    /// <summary>(Opphevet)-paragraf, §3.2 — noden produseres alltid, aldri hoppet over.</summary>
    public bool Opphevet { get; init; }
    public DateOnly? OpphevetDato { get; init; }

    /// <summary>Kun relevant for paragraf-noder — fotnoter tilhørende denne paragrafen.</summary>
    public IReadOnlyList<Fotnote> Fotnoter { get; init; } = [];

    /// <summary>
    /// Tekstsegmentene som Tekst/TekstHash er avledet fra. Bevares for AKN-serialisering
    /// slik at interne kryssreferanser kan gjenskapes som &lt;ref&gt; (§1.3) uten å re-parse HTML.
    /// Null for noder uten løpetekst (kapittel/underinndeling/opphevet paragraf).
    /// </summary>
    public IReadOnlyList<TekstSegment>? Segmenter { get; init; }
}

public sealed record RettskildeMetadata
{
    public required Kildetype Kildetype { get; init; }
    public string Doctype { get; init; } = "act";
    public required string Tittel { get; init; }
    public string? Kortnavn { get; init; }

    /// <summary>Verifisert, ekstern ELI-URI på lovnivå — kanonisk rot for eId, §1.2 (låst).</summary>
    public required string Eli { get; init; }

    public required string Datokode { get; init; }
    public DateOnly? Ikrafttredelse { get; init; }
    public DateOnly? KonsolidertDato { get; init; }
    public string Utgiver { get; init; } = "Lovdata";
    public required string AnsvarligDepartement { get; init; }

    /// <summary>FRBRauthor-href: 'stortinget' for Lov, avledet fra departement for Forskrift (Vedlegg A.1).</summary>
    public required string FrbrAuthorHref { get; init; }
    public required string FrbrAuthorShowAs { get; init; }

    public string Status { get; init; } = "Gjeldende";
}

public sealed record KonverteringResultat
{
    public required RettskildeMetadata Metadata { get; init; }
    public required IReadOnlyList<RettskildeNode> Noder { get; init; }
    public required IReadOnlyList<RettskildeReferanse> Referanser { get; init; }
    public required string AknXml { get; init; }
    public required DateOnly ImportDato { get; init; }
}
