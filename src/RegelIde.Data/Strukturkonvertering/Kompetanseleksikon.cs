using System.Text.Json;
using System.Text.Json.Serialization;

namespace RegelIde.Data.Strukturkonvertering;

/// <summary>
/// [Ny, issue #341, Johanns beslutning 3 2026-10-08] Det deterministiske leksikonet: hvilke lovuttrykk mønsterlaget kjenner,
/// og hvilken kategori, type, normform og familie hvert av dem gir. <see cref="Monsterkatalog"/> henter kategori/type/
/// normform herfra ved id — regex-ene står i koden, betydningen i leksikonet.
/// <para>
/// <b>Hvorfor en datafil (<c>kompetanseleksikon.json</c>, innebygd ressurs) og ikke en konfigurasjonstabell:</b>
/// mønsterlaget kjøres og måles uten database (docs/33 §5.4, <c>Strukturfasit</c>-testene leser bare filer), og en regel
/// gir mening bare sammen med regex-en som finner uttrykket — som er kode. En fil i repoet er versjonert av git, lesbar i en
/// PR og endres sammen med mønsteret og målingen i samme commit. En tabell ville krevd migrasjon, et redigeringsgrensesnitt
/// og en vei for å holde den lik koden, uten å gi noe mønsterlaget trenger. Feltet <c>versjon</c> økes ved hver endring.
/// </para>
/// <para>
/// <b>KI og ukjente uttrykk:</b> KI-laget får uttrykkene i instruksen og skal bare foreslå for uttrykk leksikonet IKKE
/// kjenner (<see cref="KiStrukturkonverterer.SystemInstruks"/>); kan heller ikke KI typebestemme en kompetanse, er typen
/// <see cref="Strukturkontrakt.Ukjent"/>. At et godkjent KI-forslag blir en ny regel her, er ikke bygget (oppfølgingssak).
/// </para>
/// </summary>
public static class Kompetanseleksikon
{
    /// <summary>Én regel: mønster-id-en og betydningen.</summary>
    public sealed record Regel(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("uttrykk")] IReadOnlyList<string> Uttrykk,
        [property: JsonPropertyName("kategori")] string Kategori,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("normform")] string? Normform,
        [property: JsonPropertyName("familie")] string? Familie,
        [property: JsonPropertyName("merknad")] string? Merknad);

    private sealed record Fil(
        [property: JsonPropertyName("versjon")] string Versjon,
        [property: JsonPropertyName("regler")] IReadOnlyList<Regel> Regler);

    private static readonly Lazy<Fil> Innhold = new(() =>
    {
        using var strøm = typeof(Kompetanseleksikon).Assembly.GetManifestResourceStream("RegelIde.Data.Strukturkonvertering.kompetanseleksikon.json")
            ?? throw new InvalidOperationException("Fant ikke den innebygde ressursen kompetanseleksikon.json.");
        return JsonSerializer.Deserialize<Fil>(strøm) ?? throw new InvalidOperationException("kompetanseleksikon.json er tom.");
    });

    /// <summary>Versjonen av leksikonet (feltet <c>versjon</c> i fila).</summary>
    public static string Versjon => Innhold.Value.Versjon;

    /// <summary>Alle reglene, i filas rekkefølge.</summary>
    public static IReadOnlyList<Regel> Regler => Innhold.Value.Regler;

    /// <summary>Regelen for et mønster. Mangler den, er det en programmeringsfeil — et mønster uten regel har ingen
    /// betydning (ingen gjettet fallback).</summary>
    public static Regel For(string monsterId) =>
        Regler.FirstOrDefault(r => r.Id == monsterId)
        ?? throw new InvalidOperationException($"Mønsteret «{monsterId}» har ingen regel i kompetanseleksikon.json.");
}
