using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>
/// Repeterbar, idempotent importjobb for den sjette kilden i høstelaget (<see cref="EksternKildeEntitet"/>,
/// docs/13-backlog.md) — kommune.no-tjenester, høstet av Johanns eget eksterne skrapeskript (utenfor
/// dette repoet) mot ~327 individuelle kommune.no-nettsteder via fem ulike underliggende metoder
/// (<c>SKJEMA_NO_API</c>/<c>ACOS_API</c>/<c>HTML_INNEBYGD_JSON</c>/<c>HTML</c>/<c>UNNTAK_HTML_KATALOG</c>).
/// FIL-basert, samme "ingen stabil offentlig URL/API denne appen kan polle selv"-begrunnelse som
/// <see cref="TjenestelisteImporter"/> — <see cref="ImporterAsync"/> tar derfor selve JSON-STRENGEN som
/// parameter, ikke en <c>HttpClient</c>-avhengighet.
/// <para>
/// **Egen klasse, ikke en tredje <see cref="TjenestelisteImporter.Kildetype"/>-verdi bolted-on** — denne
/// kilden er strukturelt ulik de to <see cref="TjenestelisteImporter"/> allerede dekker. Toppnivået er
/// IKKE et bart array av tjenester; det er et array av KOMMUNE-objekter, hver med sin egen
/// <c>records[]</c>-liste av tjenester (pluss kommune-metadata som <c>sources[]</c>/<c>antall_tjenester</c>
/// denne importøren ikke trenger å bevare per rad, se punkt (c)). Å tvinge dette inn i
/// <see cref="TjenestelisteImporter"/>s flate "ett bart array av tjenester"-løkke ville krevd å endre
/// selve toppnivå-parse-strategien for alle tre kildene — mer forstyrrende enn en egen, liten klasse.
/// </para>
/// <para>
/// **(a) EKTE, verifisert funn som avgjør identitetsnøkkelen**: i det ekte 15 332-rads
/// produksjonsuttrekket deler 139 URL-er TO GENUINT FORSKJELLIGE kommuner — fordi det finnes to reelle,
/// distinkte kommuner som begge heter "Herøy" (organisasjonsnummer <c>872417982</c> i Nordland,
/// <c>964978840</c> i Møre og Romsdal), begge med tjenester hostet under NØYAKTIG samme
/// <c>skjema.heroy.kommune.no</c>-URL-mønster. En <c>url</c>-alene identitetsnøkkel (mønsteret
/// <see cref="TjenestelisteImporter"/> trygt bruker for SINE kilder, der det empirisk aldri finnes to
/// ulike organisasjoner bak samme url) ville derfor STILLE kollapset begge kommunenes distinkte tjenester
/// til én rad her — en ekte datafeil, ikke en teoretisk risiko. Den committede test-fixturen
/// (<c>Testdata/KommuneTjenesteHosting/treff-sample.json</c>) inneholder bevisst nøyaktig dette ekte
/// url-kollisjonsparet, se <see cref="KommuneTjenesteHenterTests"/>.
/// </para>
/// <para>
/// **(b) Løsningen — sammensatt nøkkel (kommunens <c>organisasjonsnummer</c>, tjenestens egen <c>url</c>)**,
/// bygget av <see cref="BeregnEksternId"/>. Dette kollapser fortsatt de ~74 EKTE innad-i-kommune-
/// duplikatene i produksjonsdataene (samme tjeneste gjenfunnet av mer enn én av de fem skrapemetodene for
/// SAMME kommune — genuint samme ting, trygt å kollapse) mens de to Herøy-kommunenes rader forblir
/// distinkte. <c>organisasjonsnummer</c> hentes fra den EIENDE kommune-objektets EGET felt (strukturelt
/// garantert til stede på hvert kommune-objekt), IKKE fra det enkelte tjeneste-recordets nestede
/// <c>tilbys_av[0].organisasjonsnummer</c> — verifisert mot fixturen at de to alltid stemmer overens i
/// praksis, men <c>tilbys_av</c> kunne i teorien vært tom, så det ville vært en svakere kilde å basere
/// nøkkelen på. Skilletegn: <c>"::"</c> — organisasjonsnummer er alltid EKSAKT 9 SIFFER (norsk standard)
/// og kan derfor per definisjon aldri selv inneholde et kolon; alt etter FØRSTE <c>"::"</c> er url, uansett
/// hva som faktisk står i selve url-strengen (som i prinsippet kunne inneholde kolon).
/// </para>
/// <para>
/// **(c) RaaJson er selve tjeneste-recordet, verbatim, INGEN kommune-kontekst tilføyd** — hvert record i
/// <c>records[]</c> er allerede selvforsynt (<c>tjenestenavn</c>/<c>url</c>/<c>kategori</c>/<c>beskrivelse</c>/
/// <c>tilbys_av</c>/<c>kilder</c>), verifisert mot fixturen. Kommune-nivå-aggregatene (<c>sources[]</c>,
/// <c>antall_tjenester</c>) er nyttige for Johanns egen skrape-diagnostikk, men ikke noe denne importøren
/// trenger å duplisere inn i hver enkelt rad — samme "ikke gjett/tilføy felt som ikke er der"-prinsipp som
/// resten av høstelaget.
/// </para>
/// <para>
/// **(d) Batching PER KOMMUNE, ikke én batch for hele filen eller én per record** — ~15 000 rader/327
/// kommuner i produksjon (fila er ~8,7 MB). Én batch for HELE filen ville tape alt arbeid ved en
/// avbrutt/timet-ut kjøring; én <c>SaveChangesAsync</c> PER RECORD ville vært ~15 000 rundtrip-kall, unødig
/// kostbart. Per-kommune (~327 kall, hver typisk noen titalls rader) er samme balanse som
/// <see cref="AltinnSkjemaoversiktHenter"/>s "én lagring per etat"-valg — delvis fremgang er varig ved
/// avbrudd, og idempotent upsert gjør re-kjøring etter avbrudd trygt.
/// </para>
/// <para>
/// **(e) Kestrel maks request-body-størrelse** — ~8,7 MB/~15 000 rader-produksjonsfilen er godt innenfor
/// Kestrels DEFAULT maks request-body-grense (30 MB, <c>IHttpMaxRequestBodySizeFeature</c>). Ingen
/// eksplisitt <c>KestrelServerOptions.Limits.MaxRequestBodySize</c>-konfigurasjon er lagt til i
/// <c>Program.cs</c> — bekreftet, ikke bare antatt (se PR-beskrivelsen for feature/kommune-tjenester-hoster).
/// </para>
/// <para>
/// **(f) Samme upsert-/hash-/"ingen gjettet fallback"-mønster som resten av høstelaget**: matcher på
/// (<see cref="EksternKildeEntitet.Kildetype"/>, <see cref="EksternKildeEntitet.EksternId"/>), uendret
/// <see cref="EksternKildeEntitet.InnholdsHash"/> ⇒ raden røres ikke i det hele tatt. Produksjonsdataene har
/// EMPIRISK null kommuner med manglende <c>organisasjonsnummer</c> — men dette telles og synliggjøres
/// eksplisitt likevel (<see cref="KommuneTjenesteHostingResultat.RecordsMedManglendeOrganisasjonsnummer"/>),
/// aldri stille antatt for fremtidige leveranser. En kommune uten <c>organisasjonsnummer</c> kan ikke få en
/// trygg sammensatt nøkkel (se punkt b) — dens records telles og hoppes over, IKKE falt tilbake til
/// url-alene (det er nøyaktig den utrygge oppførselen punkt a/b beviser er feil for denne kilden).
/// </para>
/// </summary>
public sealed class KommuneTjenesteHenter(RegelIdeDbContext db)
{
    /// <summary><see cref="EksternKildeEntitet.Kildetype"/>-verdien denne høsteren skriver.</summary>
    public const string Kildetype = "kommune_tjeneste";

    /// <summary>Se klassekommentaren punkt (b) for hvorfor nettopp dette skilletegnet er trygt.</summary>
    private const string Skilletegn = "::";

    private static readonly JsonSerializerOptions JsonInnstillinger = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Kun nok av tjeneste-recordets form til å hente identitetsnøkkelens andre halvdel (<c>url</c>) — resten
    /// av objektet (<c>tjenestenavn</c>, <c>kategori</c>, <c>beskrivelse</c>, <c>tilbys_av</c>, <c>kilder</c>
    /// m.fl.) tas vare på verbatim via <see cref="JsonElement.GetRawText"/>, se klassekommentaren punkt (c).
    /// </summary>
    private sealed record KommuneTjenesteRecordIdentitet([property: JsonPropertyName("url")] string? Url);

    /// <summary>
    /// Bygger <see cref="EksternKildeEntitet.EksternId"/> som en deterministisk sammensatt nøkkel av den
    /// eiende kommunens <paramref name="organisasjonsnummer"/> og tjenestens egen <paramref name="url"/> —
    /// se klassekommentaren punkt (a)/(b) for den ekte Herøy-url-kollisjonen dette løser.
    /// </summary>
    public static string BeregnEksternId(string organisasjonsnummer, string url) =>
        $"{organisasjonsnummer}{Skilletegn}{url}";

    /// <summary>Kommunens eget <c>organisasjonsnummer</c>-felt, eller <c>null</c> hvis det mangler/ikke er en streng — ingen gjettet fallback.</summary>
    private static string? LesKommuneOrganisasjonsnummer(JsonElement kommuneElement) =>
        kommuneElement.TryGetProperty("organisasjonsnummer", out var verdi) && verdi.ValueKind == JsonValueKind.String
            ? verdi.GetString()
            : null;

    /// <summary>
    /// Importerer et array av KOMMUNE-objekter (ikke et bart array av tjenester, se klassekommentaren), mottatt
    /// verbatim (se <c>POST /api/eksterne-kilder/kommune-tjenester/importer</c>, som sender rå-body uendret
    /// videre hit — INGEN deserialiser/reserialiser-runde før den når denne metoden).
    /// </summary>
    public async Task<KommuneTjenesteHostingResultat> ImporterAsync(string raaJson, CancellationToken ct = default)
    {
        using var dokument = JsonDocument.Parse(raaJson);

        var eksisterende = await db.EksterneKilder
            .Where(k => k.Kildetype == Kildetype)
            .ToDictionaryAsync(k => k.EksternId, StringComparer.Ordinal, ct);

        var nye = 0;
        var oppdaterte = 0;
        var uendret = 0;
        var manglendeOrganisasjonsnummer = 0;
        var naa = DateTimeOffset.UtcNow;

        foreach (var kommuneElement in dokument.RootElement.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();
            var organisasjonsnummer = LesKommuneOrganisasjonsnummer(kommuneElement);

            if (!kommuneElement.TryGetProperty("records", out var recordsElement) || recordsElement.ValueKind != JsonValueKind.Array)
            {
                continue; // ingen tjenester å importere for denne kommunen — ikke observert i ekte data, men ingen gjettet fallback.
            }

            foreach (var record in recordsElement.EnumerateArray())
            {
                var raaTekst = record.GetRawText();

                if (string.IsNullOrWhiteSpace(organisasjonsnummer))
                {
                    // Ingen trygg sammensatt nøkkel mulig uten organisasjonsnummer (se klassekommentaren
                    // punkt b/f) — telles og hoppes over, ALDRI falt tilbake til url-alene.
                    manglendeOrganisasjonsnummer++;
                    continue;
                }

                var identitet = JsonSerializer.Deserialize<KommuneTjenesteRecordIdentitet>(raaTekst, JsonInnstillinger);
                if (identitet?.Url is not { Length: > 0 } url) continue; // ingen gjettet fallback — hopper over rader uten url.

                var eksternId = BeregnEksternId(organisasjonsnummer, url);
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
                    eksisterende[eksternId] = nyRad; // defensivt: unngår duplikat-innsetting om samme nøkkel skulle forekomme to ganger i samme fil.
                    nye++;
                }
            }

            // Inkrementell lagring PER KOMMUNE — se klassekommentaren punkt (d).
            await db.SaveChangesAsync(ct);
        }

        return new KommuneTjenesteHostingResultat(nye, oppdaterte, uendret, manglendeOrganisasjonsnummer);
    }
}

/// <summary>Sammendrag av én <see cref="KommuneTjenesteHenter.ImporterAsync"/>-kjøring.</summary>
public sealed record KommuneTjenesteHostingResultat(int Nye, int Oppdaterte, int Uendret, int RecordsMedManglendeOrganisasjonsnummer);
