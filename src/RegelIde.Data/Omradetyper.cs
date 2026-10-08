namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #312 «Strukturmodell 7: områderegister», 2026-10-08, docs/33 §4.1] Det lukkede vokabularet for
/// <see cref="BegrepEntitet.Omradetype"/> — speilet av CHECK <c>ck_begreper_omradetype</c>. Endres det ene, må
/// det andre endres i samme migrasjon (samme regel som <see cref="Nodetyper"/>).
/// <para>
/// <b>Hvilke områder som får en egen node</b> (Johanns funn fra fasitkontrollen 2026-10-07, kommentarene på #312):
/// bare områder en kilde NAVNGIR. Rettskretsen har ikke eget navn — «Oslo tingrett, med rettssted i Oslo, som
/// dekker Oslo kommune» — så tingretten får <c>A har_ansvarsomrade</c> direkte til kommunene, og det finnes ingen
/// «rettskrets»-type her. Lagsogn og lagdømme navngis i inndelingsforskriften, helseregionene i RHF-vedtektene.
/// Statsforvalterens embetsområde navngis ikke som område, så statsforvalteren får kantene direkte til fylkene.
/// </para>
/// </summary>
public static class Omradetyper
{
    /// <summary>Fylke i Kartverkets inndeling — kode = fylkesnummer.</summary>
    public const string Fylke = "fylke";

    /// <summary>Kommune som TERRITORIUM (ikke rettssubjektet, som er en <see cref="Virksomhet"/>) — kode = kommunenummer.</summary>
    public const string Kommune = "kommune";

    /// <summary>Tettsted/by som er rettssted uten å være en kommune («Finnsnes», «Mo i Rana») — kode = SSR-stedsnummer.
    /// Ligger i kommunen via <c>O bestar_av</c> (kommune → tettsted). Johanns funn 1 på #312: aldri to løse noder for
    /// samme sted, så et rettssted som ER en kommune får ingen egen tettsted-node.</summary>
    public const string Tettsted = "tettsted";

    /// <summary>Lagsogn (domstolloven, inndelingsforskriften kap. 2). Ingen kode; term = «lagsogn &lt;navn&gt;».</summary>
    public const string Lagsogn = "lagsogn";

    /// <summary>Lagdømme (inndelingsforskriften kap. 2). Ingen kode; term = tekstformen («Gulating lagdømme»).</summary>
    public const string Lagdomme = "lagdomme";

    /// <summary>Helseregion (RHF-vedtektene § 3, «Helseregion Nord»). Ingen kode.</summary>
    public const string Helseregion = "helseregion";

    /// <summary>Et område en kilde navngir som ikke er fylke/kommune i Kartverkets inndeling — Svalbard i Helse Nord RHFs
    /// vedtekter er det eneste i dag.</summary>
    public const string Annet = "annet";

    public static readonly string[] Alle = [Fylke, Kommune, Tettsted, Lagsogn, Lagdomme, Helseregion, Annet];

    public static bool ErGyldig(string? verdi) => verdi is not null && Alle.Contains(verdi);

    /// <summary>Visningsnavn — samme ord som UI-et (Omradetype i begrep/Nodetype.tsx).</summary>
    public static string Visningsnavn(string? omradetype) => omradetype switch
    {
        Fylke => "fylke",
        Kommune => "kommune",
        Tettsted => "tettsted",
        Lagsogn => "lagsogn",
        Lagdomme => "lagdømme",
        Helseregion => "helseregion",
        Annet => "annet område",
        null => "område",
        _ => omradetype,
    };
}
