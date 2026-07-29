using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace RegelIde.Data;

/// <summary>
/// Delt <see cref="JsonSerializerOptions"/> for jsonb-verdiobjekt-lister (Vilkår/Regelnode/Unntak sin
/// juridisk_grunnlag/skjonnsmomenter osv.) — <see cref="JsonSerializer"/> sin default-encoder unicode-
/// escaper alt utenfor ren ASCII (§ → §, Ø → Ø), som gjør norske paragraftegn/bokstaver
/// ulesbare ved manuell inspeksjon av jsonb-kolonnen i databasen. Ingen funksjonell forskjell for
/// <see cref="JsonSerializer.Deserialize{TValue}(string, JsonSerializerOptions?)"/>, kun for skriving.
/// </summary>
internal static class JsonSerialiseringHjelper
{
    public static readonly JsonSerializerOptions Innstillinger = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };
}
