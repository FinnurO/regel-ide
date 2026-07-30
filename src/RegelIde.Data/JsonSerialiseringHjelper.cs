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

    /// <summary>
    /// Validerer en jsonb-verdi som kommer rå fra klienten, og returnerer den normalisert.
    /// Tom/utelatt verdi blir <c>{}</c>.
    /// <para>
    /// De øvrige jsonb-kolonnene fylles av <see cref="JsonSerializer"/> fra typede records og er
    /// gyldige per konstruksjon. <c>parametre</c> på Vilkår er den eneste som tar imot en rå
    /// streng, og den ble tidligere skrevet usjekket rett i kolonnen: ugyldig JSON ga en
    /// <c>DbUpdateException</c> fra Postgres og dermed en ubehandlet 500 i stedet for en 400.
    /// </para>
    /// <para>
    /// Valideringen her er dessuten det eneste vernet mot at søppel havner i basen den dagen
    /// SQLite-profilen tas i bruk — SQLite lagrer JSON som ren TEXT og validerer ingenting, så
    /// en ugyldig verdi ville blitt liggende og først sprukket ved <em>lesing</em>, som en rad
    /// som permanent feiler.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentException">Verdien er ikke gyldig JSON, eller ikke et JSON-objekt.</exception>
    public static string ValiderJsonObjekt(string? json, string feltnavn)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }

        JsonDocument dokument;
        try
        {
            dokument = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new ArgumentException($"{feltnavn} er ikke gyldig JSON: {e.Message}");
        }

        using (dokument)
        {
            if (dokument.RootElement.ValueKind is not JsonValueKind.Object)
            {
                throw new ArgumentException(
                    $"{feltnavn} må være et JSON-objekt, ikke {dokument.RootElement.ValueKind}. Eksempel: {{\"aldersgrense\": 18}}.");
            }
        }

        return json;
    }
}
