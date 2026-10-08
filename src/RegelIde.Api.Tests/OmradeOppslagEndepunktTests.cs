using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// [Ny, issue #312 «Strukturmodell 7: områderegister», 2026-10-08] <c>/api/omrader/…/tilhorighet</c> mot hele API-et.
/// <para>
/// Hele registeret seedes IKKE her: databasen deles av alle API-testklassene (ApiTestCollection), og ~1 500 kanter
/// ville endret tellinger andre tester hviler på — derfor er oppstartsseeden gated av i fixturen. Seeden og tallene
/// testes i <c>RegelIde.Data.Tests/OmraderegisterSeedTests</c>; her bygges et lite, syntetisk utsnitt (egne koder
/// <c>T…</c>) for å teste ruting, serialisering og rubrikkstatusene over HTTP.
/// </para>
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class OmradeOppslagEndepunktTests(EmbeddedPostgresApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Ukjent_kommunenummer_gir_404()
    {
        var svar = await _client.GetAsync("/api/omrader/kommuner/0000/tilhorighet");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Tilhorighet_gir_rubrikker_med_status_og_ingen_valgt_kandidat_nar_det_er_flere()
    {
        var kode = "T" + Random.Shared.Next(100_000, 999_999);
        Guid kommuneId;
        await using (var db = fixture.NyDbContext())
        {
            await Strukturkanter.SeedStartsettAsync(db);
            var kanter = new StrukturkantTjeneste(db);
            BegrepEntitet Omr(string type, string? k, string term)
            {
                var b = new BegrepEntitet
                {
                    Id = Guid.NewGuid(), Begrepskategori = "omrade", Omradetype = type, Omradekode = k, Term = term,
                    Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
                };
                db.Begreper.Add(b);
                return b;
            }
            var fylke = Omr(Omradetyper.Fylke, kode + "F", "Testfylke " + kode);
            var kommune = Omr(Omradetyper.Kommune, kode, "Testkommune " + kode);
            kommuneId = kommune.Id;
            var rettssubjekt = new Virksomhet { Id = Guid.NewGuid(), Navn = "TESTKOMMUNE " + kode, Kommunenummer = kode, OpprettetTidspunkt = DateTimeOffset.UtcNow };
            var tingrettA = new Virksomhet { Id = Guid.NewGuid(), Navn = "A TINGRETT " + kode, OpprettetTidspunkt = DateTimeOffset.UtcNow };
            var tingrettB = new Virksomhet { Id = Guid.NewGuid(), Navn = "B TINGRETT " + kode, OpprettetTidspunkt = DateTimeOffset.UtcNow };
            db.Virksomheter.AddRange(rettssubjekt, tingrettA, tingrettB);
            await db.SaveChangesAsync();

            NyStrukturkant Kant(string kat, string type, Kantnode fra, Kantnode til) => new(kat, type, fra, til,
                KildeUtenforKorpusTekst: "Syntetisk testdata (#312)", KildeUtenforKorpusType: Strukturkanter.Register,
                KildeUtenforKorpusDokumentasjon: Strukturkanter.Primaer);
            await kanter.OpprettAsync(Kant("O", "bestar_av", Kantnode.Begrep(fylke.Id), Kantnode.Begrep(kommune.Id)), "test");
            await kanter.OpprettAsync(Kant("A", "har_ansvarsomrade", Kantnode.Virksomhet(rettssubjekt.Id), Kantnode.Begrep(kommune.Id)), "test");
            // To tingretter på samme kommune = domstolloven § 66 annet ledd: ingen av dem skal velges.
            await kanter.OpprettAsync(Kant("A", "har_ansvarsomrade", Kantnode.Virksomhet(tingrettA.Id), Kantnode.Begrep(kommune.Id)), "test");
            await kanter.OpprettAsync(Kant("A", "har_ansvarsomrade", Kantnode.Virksomhet(tingrettB.Id), Kantnode.Begrep(kommune.Id)), "test");
        }

        var svar = await _client.GetFromJsonAsync<JsonElement>($"/api/omrader/kommuner/{kode}/tilhorighet", Json);
        Assert.Equal(kode, svar.GetProperty("kommune").GetProperty("omradekode").GetString());
        var rubrikker = svar.GetProperty("rubrikker").EnumerateArray()
            .ToDictionary(r => r.GetProperty("rubrikk").GetString()!, r => r);
        Assert.Equal("entydig", rubrikker["kommune"].GetProperty("status").GetString());
        Assert.Equal("entydig", rubrikker["fylke"].GetProperty("status").GetString());
        Assert.Equal("ikke_entydig", rubrikker["tingrett"].GetProperty("status").GetString());
        Assert.Equal(2, rubrikker["tingrett"].GetProperty("kandidater").GetArrayLength());
        Assert.Equal("mangler", rubrikker["lagdømme"].GetProperty("status").GetString());

        // Samme svar på begrep-id-en, og begrepet bærer områdetype og kode.
        var perId = await _client.GetFromJsonAsync<JsonElement>($"/api/omrader/{kommuneId}/tilhorighet", Json);
        Assert.Equal(kode, perId.GetProperty("kommune").GetProperty("omradekode").GetString());
        var begrep = await _client.GetFromJsonAsync<JsonElement>($"/api/begreper/{kommuneId}", Json);
        Assert.Equal("kommune", begrep.GetProperty("omradetype").GetString());
        Assert.Equal(kode, begrep.GetProperty("omradekode").GetString());
    }
}
