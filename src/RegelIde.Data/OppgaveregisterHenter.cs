using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>
/// Repeterbar, idempotent høstejobb mot Oppgaveregisterets (Brønnøysundregistrene) offentlige,
/// autentiseringsfrie bulk-endepunkt <c>https://data.brreg.no/oppgaveregisteret/api/skjema/alle.json</c>
/// (~900 skjemaer, ~2.5MB, ETT kall henter alt — endepunktet har ingen paginering). Skriver én
/// <see cref="EksternKildeEntitet"/>-rad per skjema med <see cref="Kildetype"/> som kildetype.
/// <para>
/// Trigges på forespørsel (<c>POST /api/eksterne-kilder/oppgaveregister/hent</c>), IKKE ved hver
/// oppstart — å hente ~900 rader over nett ved hver dev-restart ville vært bortkastet. Til forskjell
/// fra <see cref="LovdataKatalogTjeneste"/>s automatiske 24-timers-foreldelsesgrense er dette KUN en
/// eksplisitt trigger denne runden, ingen automatisk bakgrunnsoppdatering.
/// </para>
/// <para>
/// Upsert, ikke full-rebuild (til forskjell fra <see cref="LovdataKatalogTjeneste"/>s slett-og-bygg-
/// på-nytt): matcher eksisterende rad på (<see cref="EksternKildeEntitet.Kildetype"/>,
/// <see cref="EksternKildeEntitet.EksternId"/>). Uendret <see cref="EksternKildeEntitet.InnholdsHash"/>
/// ⇒ raden røres ikke i det hele tatt (heller ikke <see cref="EksternKildeEntitet.HentetTidspunkt"/> —
/// den skal reflektere siste faktiske ENDRING, ikke siste kjøring). Én batch-<c>SaveChangesAsync</c>
/// for hele høstingen, ikke ett kall per rad.
/// </para>
/// </summary>
public sealed class OppgaveregisterHenter(HttpClient http, RegelIdeDbContext db)
{
    /// <summary><see cref="EksternKildeEntitet.Kildetype"/>-verdien denne høsteren skriver.</summary>
    public const string Kildetype = "oppgaveregister_skjema";

    private const string SkjemaUrl = "https://data.brreg.no/oppgaveregisteret/api/skjema/alle.json";

    private static readonly JsonSerializerOptions JsonInnstillinger = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Kun nok av skjemaets form til å hente identitetsnøkkelen (<c>guid</c>) — resten av objektet
    /// tas vare på verbatim via <see cref="JsonElement.GetRawText"/> i stedet for å modelleres, se
    /// <see cref="EksternKildeEntitet"/> punkt (b).
    /// </summary>
    private sealed record SkjemaIdentitet([property: JsonPropertyName("guid")] string? SkjemaGuid);

    public async Task<OppgaveregisterHostingResultat> HentAlleSkjemaAsync(CancellationToken ct = default)
    {
        var json = await http.GetStringAsync(SkjemaUrl, ct);
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
            var identitet = JsonSerializer.Deserialize<SkjemaIdentitet>(raaTekst, JsonInnstillinger);
            if (identitet?.SkjemaGuid is not { Length: > 0 } eksternId) continue; // ingen gjettet fallback — hopper over rader uten identifikator.

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
                eksisterende[eksternId] = nyRad; // defensivt: unngår duplikat-innsetting om samme guid skulle forekomme to ganger i samme respons.
                nye++;
            }
        }

        await db.SaveChangesAsync(ct);
        return new OppgaveregisterHostingResultat(nye, oppdaterte, uendret);
    }
}

/// <summary>Sammendrag av én <see cref="OppgaveregisterHenter.HentAlleSkjemaAsync"/>-kjøring.</summary>
public sealed record OppgaveregisterHostingResultat(int Nye, int Oppdaterte, int Uendret);
