using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// [Ny, issue #440 PR 1] Aktørkøens endepunkter mot hele API-et: svarer de uten innlogging og uten å skrive, summerer fordelingen til antall
/// ventende (AK3), og gir «fordeling» sin egen rute og ikke et navn?
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class AktorkoEndepunktTests
{
    private readonly HttpClient _client;
    private readonly EmbeddedPostgresApiFixture _fixture;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AktorkoEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    [Fact]
    public async Task Fordelingen_summerer_til_antall_ventende_og_lista_og_ukjent_navn_svarer_uten_a_skrive()
    {
        await using var db = _fixture.NyDbContext();
        var ventende = await db.Navnekandidater.CountAsync(k => k.Status == "Venter");
        var forBegreper = await db.Begreper.CountAsync();

        var fordeling = await _client.GetFromJsonAsync<AktorkoFordeling>("/api/aktorko/fordeling", Json);
        Assert.NotNull(fordeling);
        Assert.Equal(ventende, fordeling.Ventende);
        Assert.Equal(ventende, fordeling.Ruter.Sum(r => r.Forekomster));
        Assert.Equal(Aktorkoruter.Alle, fordeling.Ruter.Select(r => r.Rute));

        var liste = await _client.GetFromJsonAsync<AktorkoListe>("/api/aktorko", Json);
        Assert.Equal(fordeling.AntallNavn, liste!.AntallNavn);
        Assert.Equal(fordeling.Ruter.Single(r => r.Rute == Aktorkoruter.Aktorko).Forekomster, liste.Navn.Sum(n => n.Forekomster));

        var ukjent = await _client.GetAsync($"/api/aktorko/{Uri.EscapeDataString("finnes ikke " + Guid.NewGuid())}");
        Assert.Equal(HttpStatusCode.NotFound, ukjent.StatusCode);

        Assert.Equal((ventende, forBegreper), (await db.Navnekandidater.CountAsync(k => k.Status == "Venter"), await db.Begreper.CountAsync()));
    }

    // ------------------------------------------------------------------ [Ny, #440 PR 2] Skrivingen

    private static string Unik(string s) => s + new string(Guid.NewGuid().ToString("N")[..10].Select(c => (char)('a' + Convert.ToInt32(c.ToString(), 16))).ToArray());

    private async Task<Guid> BrukerIdAsync()
    {
        var brukere = await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", Json);
        return brukere!.Single(b => b.Rolle == "Jurist").Id;
    }

    private static HttpRequestMessage MedBruker(HttpMethod metode, string url, Guid? brukerId, object? body = null)
    {
        var request = new HttpRequestMessage(metode, url);
        if (brukerId is { } id) request.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, id.ToString());
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    /// <summary>Et navn med to forekomster og et hjemmelforslag (§ 1: «skal hver barnehage ha et ‹råd›»).</summary>
    private async Task<(Guid Lov, string Hjemmel, string Navn)> RaadAsync()
    {
        await using var db = _fixture.NyDbContext();
        var rad = Unik("apiraad");
        var navn = rad + "et";
        var lov = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = lov, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse", Tittel = "Testlov " + lov,
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var tekster = new[] { $"Hver barnehage skal ha et {rad}.", $"Saken legges fram for {navn}.", $"Saken drøftes i {navn}." };
        for (var i = 0; i < tekster.Length; i++)
        {
            var eid = $"https://test/{lov:N}/§{i + 1}/ledd-1";
            db.RettskildeNoder.Add(new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = lov, Eid = eid, KildeId = $"ledd-{i + 1}", NodeType = "ledd", Tekst = tekster[i], Sorteringsrekkefolge = i,
            });
            if (i == 0) continue;
            var start = tekster[i].IndexOf(navn, StringComparison.Ordinal);
            db.Navnekandidater.Add(new NavnekandidatEntitet
            {
                Id = Guid.NewGuid(), ForeslattTekst = navn, Kategori = "gruppe", RettskildeId = lov, NodeEid = eid, StartOffset = start,
                EndOffset = start + navn.Length, Status = "Venter", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
        }
        await db.SaveChangesAsync();
        return (lov, $"https://test/{lov:N}/§1/ledd-1", navn);
    }

    [Fact]
    public async Task AK5_godkjenn_krever_innlogging_og_valg_avviser_endret_navn_og_gir_kvittering()
    {
        var bruker = await BrukerIdAsync();
        var (lov, hjemmel, navn) = await RaadAsync();
        var url = $"/api/aktorko/{Uri.EscapeDataString(navn)}/godkjenn";
        var gyldig = new
        {
            type = "aktor", navn, hjemmel = "forslag", hjemmelRettskildeId = lov, hjemmelEid = hjemmel, godkjenning = "godkjent", forventetForekomster = 2,
        };

        Assert.NotEqual(HttpStatusCode.OK, (await _client.SendAsync(MedBruker(HttpMethod.Post, url, null, gyldig))).StatusCode);
        var utenType = new { navn, hjemmel = "forslag", hjemmelRettskildeId = lov, hjemmelEid = hjemmel, godkjenning = "godkjent" };
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(MedBruker(HttpMethod.Post, url, bruker, utenType))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await _client.SendAsync(MedBruker(HttpMethod.Post, url, bruker, gyldig with { forventetForekomster = 5 }))).StatusCode);

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, url, bruker, gyldig));
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var kvittering = await svar.Content.ReadFromJsonAsync<AktorkoKvittering>(Json);
        Assert.Equal(("godkjent", 2, "oppretting"), (kvittering!.Handling, kvittering.Forekomster, kvittering.HjemmelArt));
        Assert.Contains(kvittering.Linjer, l => l.Contains($"Aktøren «{navn}» er opprettet"));

        // Navnet er ute av køen: en ny godkjenning gir 404.
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(MedBruker(HttpMethod.Post, url, bruker, gyldig))).StatusCode);
    }

    [Fact]
    public async Task Detaljen_viser_hullmerknaden_og_annen_paragraf_som_metatekst()
    {
        var (_, _, navn) = await RaadAsync();
        var detalj = await _client.GetFromJsonAsync<AktorkoDetalj>($"/api/aktorko/{Uri.EscapeDataString(navn)}", Json);
        Assert.Equal(AktorkoTjeneste.Hullmerknad, detalj!.Hullmerknad);
        var forslag = Assert.Single(detalj.Hjemmelforslag);
        Assert.Equal("hjemmelen står i § 1; forekomstene i § 2, § 3", forslag.AnnenParagraf);
    }

    [Fact]
    public async Task Tagg_alle_som_generisk_omtale_er_torrkjoring_uten_skriv()
    {
        var bruker = await BrukerIdAsync();
        var (_, _, navn) = await RaadAsync();
        await using var db = _fixture.NyDbContext();
        var forBegreper = await db.Begreper.CountAsync();

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/aktorko/generisk-omtale", bruker, new { ord = navn }));
        var rapport = await svar.Content.ReadFromJsonAsync<GeneriskOmtaleRapport>(Json);

        Assert.Equal((false, 2), (rapport!.Skrevet, rapport.Forekomster));
        Assert.Equal(forBegreper, await db.Begreper.CountAsync());
        Assert.Equal(2, await db.Navnekandidater.CountAsync(k => k.ForeslattTekst == navn && k.Status == "Venter"));
    }
}
