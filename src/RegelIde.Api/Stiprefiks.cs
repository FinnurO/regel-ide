namespace RegelIde.Api;

/// <summary>
/// Sti-prefikset appen er servert under. Altinns app-cluster serverer på <c>/{org}/{app}/</c>, og
/// ingressen stripper ikke prefikset — appen må håndtere det selv.
/// <para>
/// Prefikset kan ikke bakes inn i SPA-bygget, for da ville imaget vært låst til én sti. Det leses
/// derfor ved oppstart og settes som <c>&lt;base href&gt;</c> i index.html, som alle relative
/// asset- og API-URL-er løses mot. Se docs/deploy-altinn-app-cluster.md.
/// </para>
/// </summary>
public static class Stiprefiks
{
    public const string Konfigurasjonsnokkel = "RegelIde:Stiprefiks";

    /// <summary>Plassholderen i index.html som byttes ut. Må matche src/RegelIde.Web/index.html.</summary>
    private const string Plassholder = "<base href=\"/\" />";

    /// <summary>
    /// Leser og normaliserer prefikset. Returnerer null når det ikke er satt, slik at lokal
    /// kjøring er upåvirket.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Når prefikset mangler innledende skråstrek. <c>UsePathBase</c> krever det, og uten denne
    /// sjekken ville appen startet fint og svart 404 på alt — en langt vanskeligere feil å finne
    /// enn en tydelig oppstartsfeil.
    /// </exception>
    public static string? Les(IConfiguration konfigurasjon)
    {
        var prefiks = (konfigurasjon[Konfigurasjonsnokkel] ?? "").Trim().TrimEnd('/');
        if (prefiks.Length == 0) return null;

        if (!prefiks.StartsWith('/'))
        {
            throw new InvalidOperationException(
                $"{Konfigurasjonsnokkel} må starte med '/'. Fikk '{prefiks}'.");
        }
        return prefiks;
    }

    /// <summary>
    /// Setter <c>&lt;base href&gt;</c> i index.html til prefikset.
    /// <para>
    /// Den avsluttende skråstreken er ikke kosmetikk: uten den tolker nettleseren siste segment
    /// som et filnavn og kaster det, slik at <c>assets/x.js</c> løses mot <c>/ttd/</c> i stedet
    /// for <c>/ttd/app/</c>.
    /// </para>
    /// </summary>
    public static string SettBaseHref(string html, string? prefiks) =>
        html.Replace(Plassholder, $"<base href=\"{prefiks ?? ""}/\" />");
}
