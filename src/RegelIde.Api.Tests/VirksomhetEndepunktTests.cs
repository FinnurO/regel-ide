using System.Net.Http.Json;
using System.Text.Json;
using RegelIde.Api;

namespace RegelIde.Api.Tests;

/// <summary>
/// Verifiserer at <see cref="VirksomhetDto.Aktiv"/> (2026-08-14, organisasjonsregister-seeding) faktisk
/// flyter gjennom <c>/api/virksomheter</c> — kjører mot samme fullt oppstartede/seedede API som
/// <see cref="BrukerEndepunktTests"/> (Program.cs sin egen migrasjon+seeding, inkl.
/// <c>OrganisasjonsregisterSeed</c>), IKKE en isolert testdatabase.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class VirksomhetEndepunktTests
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web);

    public VirksomhetEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task Virksomhetliste_inneholder_aktiv_felt_med_forventede_verdier()
    {
        var virksomheter = await _client.GetFromJsonAsync<List<VirksomhetDto>>("/api/virksomheter", JsonInnstillinger);

        Assert.NotNull(virksomheter);
        Assert.NotEmpty(virksomheter!);

        // Bergen kommune og Agder fylkeskommune er Johanns eksplisitt navngitte aktive virksomheter
        // (docs/00-endringslogg-v0.3.md/organisasjonsregister-seeding, 2026-08-14).
        var bergen = Assert.Single(virksomheter!, v => v.Navn == "Bergen kommune");
        Assert.True(bergen.Aktiv);
        var agder = Assert.Single(virksomheter!, v => v.Navn == "Agder fylkeskommune");
        Assert.True(agder.Aktiv);
        var testkommunen = virksomheter!.First(v => v.Navn == "Testkommunen");
        Assert.True(testkommunen.Aktiv);

        // Organisasjonsregisteret seeder et stort antall sovende (Aktiv=false) kommuner/fylkeskommuner
        // fra Seed/organisasjoner-norge.json — minst én reell, kjent kommune skal derfor være inaktiv.
        //
        // [ENDRET, registernavn-runden, 2026-09-08] Navnet er nå registerets ORDRETTE form fra
        // kildefilen ("OSLO KOMMUNE"), ikke lenger omskrevet til "Oslo kommune" av den fjernede
        // FormaterNavnEnkelt. Den lesbare formen ligger i `visningsnavn`.
        var oslo = Assert.Single(virksomheter!, v => v.Navn == "OSLO KOMMUNE");
        Assert.False(oslo.Aktiv);
        Assert.True(virksomheter!.Count(v => !v.Aktiv) > 300);
    }

    /// <summary>
    /// [Ny, registernavn-runden, 2026-09-08] `visningsnavn` er ALDRI null og faller tilbake på `navn`.
    /// Det er kontrakten hele frontend hviler på (`useVirksomheter.ts` leser feltet direkte uten
    /// null-sjekk) — uten fallbacken ville en rad uten 'gjeldende'-navneform vist tom tekst i hver
    /// eier-kolonne og hver velger.
    /// <para>
    /// Testen kjører mot en base der bakgrunnssynken IKKE har opprettet navneformer (den er en
    /// BackgroundService som gjør eksterne kall, og testfixturen venter ikke på den), så dette er
    /// nettopp fallback-tilfellet — som er det som MÅ holde.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Virksomhetliste_har_alltid_visningsnavn_og_faller_tilbake_pa_registernavnet()
    {
        var virksomheter = await _client.GetFromJsonAsync<List<VirksomhetDto>>("/api/virksomheter", JsonInnstillinger);

        Assert.NotNull(virksomheter);
        Assert.All(virksomheter!, v => Assert.False(string.IsNullOrWhiteSpace(v.Visningsnavn)));

        var oslo = Assert.Single(virksomheter!, v => v.Navn == "OSLO KOMMUNE");
        Assert.Equal("OSLO KOMMUNE", oslo.Visningsnavn);
    }
}
