using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>
/// Repeterbar, idempotent høstejobb mot Altinns offentlige, autentiseringsfrie ressursregister-API
/// (<c>https://tjenesteoversikten.no/api/v1/prod/resource/resourcelist?includeApps=true&amp;includeAltinn2=true</c>,
/// ~4200 ressurser, ETT kall henter alt — ingen paginering). Sideordnet <see cref="OppgaveregisterHenter"/>
/// (samme høstelag, se <see cref="EksternKildeEntitet"/> for hvorfor ingen FK til domenemodellen ennå) —
/// eneste vesentlige forskjell er filteret i <see cref="SkalHostes"/> under.
/// <para>
/// **Filter til <c>resourceType == "AltinnApp"</c>** (~820 av ~4200) — Johanns uttalte forretningsbehov
/// denne runden, IKKE alle åtte ressurstypene API-et faktisk returnerer (<c>AltinnApp</c>,
/// <c>MaskinportenSchema</c>, <c>GenericAccessResource</c>, <c>CorrespondenceService</c>,
/// <c>Systemresource</c>, <c>BrokerService</c>, <c>MigratedApp</c>, <c>Consent</c>). Skrevet som én egen,
/// lett synlig metode (<see cref="SkalHostes"/>) i stedet for bakt inn i selve parse-/upsert-løkken,
/// slik at en senere runde kan utvide til flere <c>resourceType</c>-verdier uten å røre resten av
/// høsteren — samme "fri streng, ikke CHECK-constraint"-filosofi som <see cref="EksternKildeEntitet.Kildetype"/>.
/// </para>
/// <para>
/// Trigges på forespørsel (<c>POST /api/eksterne-kilder/altinn-ressurser/hent</c>), IKKE ved oppstart —
/// samme begrunnelse som <see cref="OppgaveregisterHenter"/>. Upsert på (<see cref="Kildetype"/>,
/// <see cref="EksternKildeEntitet.EksternId"/>=ressursens egen <c>identifier</c>), uendret
/// <see cref="EksternKildeEntitet.InnholdsHash"/> ⇒ raden røres ikke. Én batch-<c>SaveChangesAsync</c>.
/// </para>
/// </summary>
public sealed class AltinnRessursHenter(HttpClient http, RegelIdeDbContext db)
{
    /// <summary><see cref="EksternKildeEntitet.Kildetype"/>-verdien denne høsteren skriver.</summary>
    public const string Kildetype = "altinn_ressurs";

    private const string RessursUrl = "https://tjenesteoversikten.no/api/v1/prod/resource/resourcelist?includeApps=true&includeAltinn2=true";

    private static readonly JsonSerializerOptions JsonInnstillinger = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Kun nok av ressursens form til å hente identitetsnøkkelen (<c>identifier</c>) og avgjøre filteret
    /// (<c>resourceType</c>) — resten av objektet (multilingual <c>title</c>/<c>description</c>/
    /// <c>rightDescription</c>, <c>resourceReferences</c>, <c>contactPoints</c>,
    /// <c>authorizationReference</c> m.fl.) tas vare på verbatim via <see cref="JsonElement.GetRawText"/>
    /// i stedet for å modelleres, se <see cref="EksternKildeEntitet"/> punkt (b).
    /// </summary>
    private sealed record RessursIdentitet(
        [property: JsonPropertyName("identifier")] string? Identifier,
        [property: JsonPropertyName("resourceType")] string? ResourceType);

    /// <summary>Den ENE, lett-endrede filterklausulen — se klassekommentaren.</summary>
    private static bool SkalHostes(string? resourceType) => resourceType == "AltinnApp";

    public async Task<AltinnRessursHostingResultat> HentAlleRessurserAsync(CancellationToken ct = default)
    {
        var json = await http.GetStringAsync(RessursUrl, ct);
        using var dokument = JsonDocument.Parse(json);

        var eksisterende = await db.EksterneKilder
            .Where(k => k.Kildetype == Kildetype)
            .ToDictionaryAsync(k => k.EksternId, StringComparer.Ordinal, ct);

        var nye = 0;
        var oppdaterte = 0;
        var uendret = 0;
        var naa = DateTimeOffset.UtcNow;

        foreach (var element in dokument.RootElement.EnumerateArray())
        {
            var raaTekst = element.GetRawText();
            var identitet = JsonSerializer.Deserialize<RessursIdentitet>(raaTekst, JsonInnstillinger);
            if (!SkalHostes(identitet?.ResourceType)) continue;
            if (identitet?.Identifier is not { Length: > 0 } eksternId) continue; // ingen gjettet fallback — hopper over rader uten identifikator.

            var hash = LovdataIdentifikatorer.BeregnTekstHash(raaTekst);

            if (eksisterende.TryGetValue(eksternId, out var rad))
            {
                if (rad.InnholdsHash == hash)
                {
                    uendret++;
                    continue;
                }

                rad.RaaJson = raaTekst;
                rad.InnholdsHash = hash;
                rad.HentetTidspunkt = naa;
                oppdaterte++;
            }
            else
            {
                var nyRad = new EksternKildeEntitet
                {
                    Id = Guid.NewGuid(),
                    Kildetype = Kildetype,
                    EksternId = eksternId,
                    RaaJson = raaTekst,
                    InnholdsHash = hash,
                    HentetTidspunkt = naa,
                };
                db.EksterneKilder.Add(nyRad);
                eksisterende[eksternId] = nyRad; // defensivt: unngår duplikat-innsetting om samme identifier skulle forekomme to ganger i samme respons.
                nye++;
            }
        }

        await db.SaveChangesAsync(ct);
        return new AltinnRessursHostingResultat(nye, oppdaterte, uendret);
    }
}

/// <summary>Sammendrag av én <see cref="AltinnRessursHenter.HentAlleRessurserAsync"/>-kjøring.</summary>
public sealed record AltinnRessursHostingResultat(int Nye, int Oppdaterte, int Uendret);
