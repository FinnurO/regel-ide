using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>
/// Repeterbar, idempotent importjobb, delt av flere strukturelt like FIL-BASERTE kilder — til forskjell
/// fra de andre høsterne i dette laget (<see cref="OppgaveregisterHenter"/>, <see cref="AltinnRessursHenter"/>,
/// <see cref="AltinnSkjemaoversiktHenter"/>), som alle henter direkte over nett selv. Kildene denne
/// klassen dekker leveres av Johanns egne eksterne skrapeskript (utenfor dette repoet) som rå JSON-body
/// han selv poster inn — det finnes ingen stabil offentlig URL/API denne appen kan polle selv, for
/// ingen av kildene. <see cref="ImporterAsync"/> tar derfor selve JSON-STRENGEN som parameter, IKKE en
/// <c>HttpClient</c>-avhengighet — ingen utgående nettverkskall i denne klassen i det hele tatt.
/// <para>
/// Opprinnelig bygget (feature/statsforvalter-tjenester-hoster) kun for Statsforvalternes "skjema og
/// tjenester"-oversikt, deretter generalisert (feature/generaliser-tjenesteliste-importer) da en andre
/// kilde — fylkeskommunenes "dialog"-kontaktskjema-oversikt — viste seg strukturelt identisk. Den
/// eneste egentlige forskjellen mellom kildene er <see cref="Kildetype"/>-strengen, som nå er en
/// PARAMETER til <see cref="ImporterAsync"/> i stedet for en hardkodet konstant på klassen — se
/// <see cref="Statsforvalter"/>/<see cref="FylkeskommuneDialog"/> for de to kjente verdiene.
/// </para>
/// <para>
/// Toppnivå i kildeformatet er et bart JSON-array (ingen konvolutt). <see cref="EksternKildeEntitet.EksternId"/>
/// er tjenestens EGEN <c>url</c>-felt, IKKE <c>tjenestenavn</c> — empirisk bevist nødvendig av et ekte
/// bokmål/nynorsk PDF-variant-par i Statsforvalter-produksjonsdataene ("Klage på forvaltningsvedtak", to
/// rader, samme navn, ulik url).
/// </para>
/// <para>
/// <c>tilbys_av</c> er en empirisk, strukturell realitet fra kildene — én tjeneste tilbys av N
/// organisasjoner (for Statsforvalter-kilden: 288 rader/10 embeter, 157 med 1 tilbyder, 56 med 2, 56 med
/// 8, 19 med alle 10; for fylkeskommune-dialogkilden derimot er hver rad 100 % lokalt/fylke-spesifikt
/// innhold — empirisk ALLTID nøyaktig 1 tilbyder i de ~655 ekte produksjonsradene) — IKKE en modellert
/// master/instans-relasjon, uansett kilde. Den designbeslutningen er eksplisitt parkert (Johann: "la oss
/// vente litt med master") — denne importøren gjør INGENTING med mønsteret utover å bevare det verbatim
/// i <see cref="EksternKildeEntitet.RaaJson"/> og telle et kjent datakvalitetsavvik, se
/// <see cref="TjenestelisteHostingResultat.TilbydereMedManglendeOrgnummer"/>.
/// </para>
/// <para>
/// Samme upsert-/hash-/batch-mønster som <see cref="OppgaveregisterHenter"/>: matcher på
/// (<see cref="EksternKildeEntitet.Kildetype"/>, <see cref="EksternKildeEntitet.EksternId"/>), uendret
/// <see cref="EksternKildeEntitet.InnholdsHash"/> ⇒ raden røres ikke i det hele tatt (heller ikke
/// <see cref="EksternKildeEntitet.HentetTidspunkt"/>, som skal reflektere siste faktiske ENDRING, ikke
/// siste kjøring). Én batch-<c>SaveChangesAsync</c> for hele importen.
/// </para>
/// </summary>
public sealed class TjenestelisteImporter(RegelIdeDbContext db)
{
    /// <summary><see cref="EksternKildeEntitet.Kildetype"/>-verdien for Statsforvalternes "skjema og tjenester"-oversikt.</summary>
    public const string Statsforvalter = "statsforvalter_tjeneste";

    /// <summary><see cref="EksternKildeEntitet.Kildetype"/>-verdien for fylkeskommunenes "dialog"-kontaktskjema-oversikt.</summary>
    public const string FylkeskommuneDialog = "fylkeskommune_dialogtjeneste";

    private static readonly JsonSerializerOptions JsonInnstillinger = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Kun nok av tjenestens form til å hente identitetsnøkkelen (<c>url</c>) og telle manglende
    /// organisasjonsnummer i <c>tilbys_av</c> — resten av objektet (<c>tjenestenavn</c>, <c>tema</c>/<c>kategori</c>,
    /// <c>beskrivelse</c> m.fl.) tas vare på verbatim via <see cref="JsonElement.GetRawText"/> i stedet
    /// for å modelleres, se <see cref="EksternKildeEntitet"/> punkt (b).
    /// </summary>
    private sealed record TjenesteIdentitet(
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("tilbys_av")] List<TilbyderIdentitet>? TilbysAv);

    /// <summary>
    /// Kun <c>organisasjonsnummer</c>. Johanns egne oppstrøms-skraper (hans kode, utenfor dette
    /// repoet, ingenting vi retter her) har en kjent skjørhet der fallback for et manglende
    /// organisasjonsnummer stille kan skrive en tom streng. Vi skal ALDRI behandle en tom/manglende
    /// verdi som en ekte identifikator — kun telle og synliggjøre den (<see cref="TjenestelisteHostingResultat.TilbydereMedManglendeOrgnummer"/>),
    /// samme "ingen gjettet fallback"-prinsipp som ellers i denne kodebasen.
    /// </summary>
    private sealed record TilbyderIdentitet([property: JsonPropertyName("organisasjonsnummer")] string? Organisasjonsnummer);

    /// <summary>
    /// Importerer et bart JSON-array av tjenester for den angitte <paramref name="kildetype"/>, mottatt
    /// verbatim (se <c>POST /api/eksterne-kilder/statsforvalter-tjenester/importer</c> og
    /// <c>POST /api/eksterne-kilder/fylkeskommune-tjenester/importer</c>, som begge sender rå-body
    /// uendret videre hit — INGEN deserialiser/reserialiser-runde før den når denne metoden, slik at
    /// <see cref="EksternKildeEntitet.RaaJson"/> blir byte-identisk med det som ble postet per rad).
    /// </summary>
    public async Task<TjenestelisteHostingResultat> ImporterAsync(string raaJson, string kildetype, CancellationToken ct = default)
    {
        using var dokument = JsonDocument.Parse(raaJson);

        var eksisterende = await db.EksterneKilder
            .Where(k => k.Kildetype == kildetype)
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
                    Kildetype = kildetype,
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
        return new TjenestelisteHostingResultat(nye, oppdaterte, uendret, manglendeOrgnummer);
    }
}

/// <summary>Sammendrag av én <see cref="TjenestelisteImporter.ImporterAsync"/>-kjøring.</summary>
public sealed record TjenestelisteHostingResultat(int Nye, int Oppdaterte, int Uendret, int TilbydereMedManglendeOrgnummer);
