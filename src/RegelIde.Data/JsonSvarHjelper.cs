using System.Text.RegularExpressions;

namespace RegelIde.Data;

/// <summary>
/// Byggesteg 5 runde 3 — ekte chatmodeller pakker ofte JSON-svar i en markdown-kodeblokk
/// (```json ... ``` eller ``` ... ```) selv når system-instruksen ber dem la være. Rein
/// tilleggsrobusthet ved <see cref="JsonSerializer.Deserialize{TValue}(string, JsonSerializerOptions?)"/>
/// av <see cref="IKiAgentKlient.GenererAsync"/>-svar — IKKE en erstatning for at
/// <see cref="BegrepsforslagTjeneste"/>/<see cref="TjenesteforslagTjeneste"/> sine system-instrukser
/// selv skal be om ren JSON.
/// </summary>
internal static partial class JsonSvarHjelper
{
    [GeneratedRegex(@"^\s*```(?:json)?\s*\n?(.*?)\n?```\s*$", RegexOptions.Singleline)]
    private static partial Regex KodeblokkMønster();

    /// <summary>Strimler en evt. omsluttende ```/```json-kodeblokk. Uendret hvis ingen kodeblokk finnes.</summary>
    public static string StrimleKodeblokk(string svar)
    {
        var match = KodeblokkMønster().Match(svar);
        return match.Success ? match.Groups[1].Value : svar;
    }
}
