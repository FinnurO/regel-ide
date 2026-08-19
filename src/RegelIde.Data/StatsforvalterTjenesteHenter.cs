using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>
/// Repeterbar, idempotent importjobb for Statsforvalternes "skjema og tjenester"-oversikt — FIL-BASERT,
/// til forskjell fra de tre andre høsterne i dette laget (<see cref="OppgaveregisterHenter"/>,
/// <see cref="AltinnRessursHenter"/>, <see cref="AltinnSkjemaoversiktHenter"/>), som alle henter direkte
/// over nett selv. Johann kjører sin egen eksterne Python-skrape (utenfor dette repoet) mot alle 10
/// Statsforvalter-embetenes "skjema og tjenester"-sider periodisk og leverer resultatet som en rå
/// JSON-body han selv poster inn — det finnes ingen stabil offentlig URL/API denne appen kan polle selv.
/// <see cref="ImporterAsync"/> tar derfor selve JSON-STRENGEN som parameter, IKKE en
/// <c>HttpClient</c>-avhengighet — ingen utgående nettverkskall i denne klassen i det hele tatt.
/// <para>
/// Toppnivå i kildeformatet er et bart JSON-array (ingen konvolutt). <see cref="EksternKildeEntitet.EksternId"/>
/// er tjenestens EGEN <c>url</c>-felt, IKKE <c>tjenestenavn</c> — empirisk bevist nødvendig av et ekte
/// bokmål/nynorsk PDF-variant-par i produksjonsdataene ("Klage på forvaltningsvedtak", to rader, samme
/// navn, ulik url).
/// </para>
/// <para>
/// <c>tilbys_av</c> er en empirisk, strukturell realitet fra kilden — én tjeneste tilbys av N
/// organisasjoner (fordeling i ekte produksjonsdata, 288 rader/10 embeter: 157 med 1 tilbyder, 56 med 2,
/// 56 med 8, 19 med alle 10) — IKKE en modellert master/instans-relasjon. Den designbeslutningen er
/// eksplisitt parkert (Johann: "la oss vente litt med master") — denne importøren gjør INGENTING med
/// mønsteret utover å bevare det verbatim i <see cref="EksternKildeEntitet.RaaJson"/> og telle et kjent
/// datakvalitetsavvik, se <see cref="StatsforvalterTjenesteHostingResultat.TilbydereMedManglendeOrgnummer"/>.
/// </para>
/// <para>
/// Samme upsert-/hash-/batch-mønster som <see cref="OppgaveregisterHenter"/>: matcher på
/// (<see cref="Kildetype"/>, <see cref="EksternKildeEntitet.EksternId"/>), uendret
/// <see cref="EksternKildeEntitet.InnholdsHash"/> ⇒ raden røres ikke i det hele tatt (heller ikke
/// <see cref="EksternKildeEntitet.HentetTidspunkt"/>, som skal reflektere siste faktiske ENDRING, ikke
/// siste kjøring). Én batch-<c>SaveChangesAsync</c> for hele importen.
/// </para>
/// </summary>
public sealed class StatsforvalterTjenesteHenter(RegelIdeDbContext db)
{
    /// <summary><see cref="EksternKildeEntitet.Kildetype"/>-verdien denne importøren skriver.</summary>
    public const string Kildetype = "statsforvalter_tjeneste";

    private static readonly JsonSerializerOptions JsonInnstillinger = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Kun nok av tjenestens form til å hente identitetsnøkkelen (<c>url</c>) og telle manglende
    /// organisasjonsnummer i <c>tilbys_av</c> — resten av objektet (<c>tjenestenavn</c>, <c>tema</c>,
    /// <c>beskrivelse</c> m.fl.) tas vare på verbatim via <see cref="JsonElement.GetRawText"/> i stedet
    /// for å modelleres, se <see cref="EksternKildeEntitet"/> punkt (b).
    /// </summary>
    private sealed record TjenesteIdentitet(
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("tilbys_av")] List<TilbyderIdentitet>? TilbysAv);

    /// <summary>
    /// Kun <c>organisasjonsnummer</c>. Johanns egen oppstrøms-skrape (hans kode, utenfor dette repoet,
    /// ingenting vi retter her) har en kjent skjørhet der fallback for et manglende organisasjonsnummer
    /// stille kan skrive en tom streng. Vi skal ALDRI behandle en tom/manglende verdi som en ekte
    /// identifikator — kun telle og synliggjøre den (<see cref="StatsforvalterTjenesteHostingResultat.TilbydereMedManglendeOrgnummer"/>),
    /// samme "ingen gjettet fallback"-prinsipp som ellers i denne kodebasen.
    /// </summary>
    private sealed record TilbyderIdentitet([property: JsonPropertyName("organisasjonsnummer")] string? Organisasjonsnummer);

    /// <summary>
    /// Importerer et bart JSON-array av Statsforvalter-tjenester, mottatt verbatim (se
    /// <c>POST /api/eksterne-kilder/statsforvalter-tjenester/importer</c>, som sender rå-body uendret
    /// videre hit — INGEN deserialiser/reserialiser-runde før den når denne metoden, slik at
    /// <see cref="EksternKildeEntitet.RaaJson"/> blir byte-identisk med det som ble postet per rad).
    /// </summary>
    public async Task<StatsforvalterTjenesteHostingResultat> ImporterAsync(string raaJson, CancellationToken ct = default)
    {
        using var dokument = JsonDocument.Parse(raaJson);

        var eksisterende = await db.EksterneKilder
            .Where(k => k.Kildetype == Kildetype)
            .ToDictionaryAsync(k => k.EksternId, StringComparer.Ordinal, ct);

        var nye = 0;
        var oppdaterte = 0;
        var uendret = 0;
        var manglendeOrgnummer = 0;
        var naa = DateTimeOffset.UtcNow;

        foreach (var element in dokument.RootElement.EnumerateArray())
        {
            var raaTekst = element.GetRawText();
            var identitet = JsonSerializer.Deserialize<TjenesteIdentitet>(raaTekst, JsonInnstillinger);

            foreach (var tilbyder in identitet?.TilbysAv ?? [])
            {
                if (string.IsNullOrWhiteSpace(tilbyder.Organisasjonsnummer)) manglendeOrgnummer++;
            }

            if (identitet?.Url is not { Length: > 0 } eksternId) continue; // ingen gjettet fallback — hopper over rader uten identifikator.

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
                eksisterende[eksternId] = nyRad; // defensivt: unngår duplikat-innsetting om samme url skulle forekomme to ganger i samme fil.
                nye++;
            }
        }

        await db.SaveChangesAsync(ct);
        return new StatsforvalterTjenesteHostingResultat(nye, oppdaterte, uendret, manglendeOrgnummer);
    }
}

/// <summary>Sammendrag av én <see cref="StatsforvalterTjenesteHenter.ImporterAsync"/>-kjøring.</summary>
public sealed record StatsforvalterTjenesteHostingResultat(int Nye, int Oppdaterte, int Uendret, int TilbydereMedManglendeOrgnummer);
