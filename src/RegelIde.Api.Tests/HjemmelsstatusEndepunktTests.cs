using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// [Ny, issue #472 PR 1] Endepunktene for hjemmelsstatusen mot hele API-et: tørrkjører maskinregelen som standard, skriver bare med
/// <c>?skriv=true</c>, og svarer rettskildesiden med statusen per hjemmel og den samlede statusen (som regnes ut, ikke lagres).
/// <c>?lovtidend=false</c> i testene: den ekte kilden laster ned Lovtidend-arkivet.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class HjemmelsstatusEndepunktTests(EmbeddedPostgresApiFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private async Task<JsonElement> SendAsync(HttpMethod metode, string url, Guid? bruker = null)
    {
        var r = new HttpRequestMessage(metode, url);
        if (bruker is { } b) r.Headers.Add(GjeldendeBrukerTjeneste.HeaderNavn, b.ToString());
        var svar = await _client.SendAsync(r);
        var tekst = await svar.Content.ReadAsStringAsync();
        Assert.True(svar.StatusCode == HttpStatusCode.OK, $"{(int)svar.StatusCode}: {tekst}");
        return JsonDocument.Parse(tekst).RootElement.Clone();
    }

    [Fact]
    public async Task Torrkjoring_er_standard_og_rettskildesiden_far_status_bevis_og_samlet_status()
    {
        var bruker = (await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", Json))!.First().Id;
        var nr = Random.Shared.Next(100_000, 999_999);
        var gammelEli = $"https://lovdata.no/eli/lov/1961/05/24/{nr}/nor";
        Guid forskriftId;
        await using (var db = fixture.NyDbContext())
        {
            RettskildeEntitet Kilde(string eli, string kildetype, string importrolle, string? dokumentId, string? annet = null) => new()
            {
                Id = Guid.NewGuid(), Doctype = "act", Kildetype = kildetype, Status = importrolle == "primaer" ? "Gjeldende" : "Utkast",
                Importrolle = importrolle, Tittel = dokumentId ?? eli, Eli = eli, DokumentId = dokumentId, AnnetOmDokumentet = annet,
                AknXml = importrolle == "primaer" ? "<akomaNtoso/>" : null,
                OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow, Ikrafttredelse = new DateOnly(2015, 1, 1),
            };
            var gammel = Kilde(gammelEli, "Lov", "referanse", null);
            var nyEli = $"https://lovdata.no/eli/lov/2015/04/10/{nr}/nor";
            var ny = Kilde(nyEli, "Lov", "primaer", $"NL/lov/2015-04-10-{nr}");
            var f = Kilde($"https://lovdata.no/eli/forskrift/1990/01/01/{nr}/nor", "Forskrift", "primaer", $"SF/forskrift/1990-01-01-{nr}",
                $"Fastsatt med hjemmel i lov 24. mai 1961 nr. {nr} om sparebanker § 5.");
            db.Rettskilder.AddRange(gammel, ny, f);
            db.RettskildeNoder.AddRange(
                new RettskildeNodeEntitet { Id = Guid.NewGuid(), RettskildeId = ny.Id, Eid = $"{nyEli}/§23-3/ledd-4", KildeId = "a", NodeType = "ledd",
                    Tekst = $"Lov 24. mai 1961 nr. {nr} om sparebanker oppheves.", Sorteringsrekkefolge = 1 },
                new RettskildeNodeEntitet { Id = Guid.NewGuid(), RettskildeId = ny.Id, Eid = $"{nyEli}/§23-2/ledd-2", KildeId = "b", NodeType = "ledd",
                    Tekst = $"Forskrifter gitt med hjemmel i lov 24. mai 1961 nr. {nr} om sparebanker gjelder også etter at loven her har trådt i kraft.",
                    Sorteringsrekkefolge = 0 });
            db.RettskildeEndringer.Add(new RettskildeEndringEntitet { Id = Guid.NewGuid(), RettskildeId = ny.Id, EndringEid = gammelEli, EndringRettskildeId = gammel.Id });
            db.RettskildeHjemler.Add(new RettskildeHjemmelEntitet { Id = Guid.NewGuid(), RettskildeId = f.Id, HjemmelEid = $"{gammelEli}/§5", HjemmelRettskildeId = gammel.Id });
            await db.SaveChangesAsync();
            forskriftId = f.Id;
        }

        var forSkriving = await SendAsync(HttpMethod.Get, $"/api/rettskilder/{forskriftId}/hjemmelsstatus");
        Assert.False(forSkriving.GetProperty("beregnet").GetBoolean());

        var torr = await SendAsync(HttpMethod.Post, $"/api/hjemmelsstatus/maskinregel?lovtidend=false&rettskildeId={forskriftId}", bruker);
        Assert.False(torr.GetProperty("skrevet").GetBoolean());
        Assert.Equal(1, torr.GetProperty("perStatus").GetProperty("viderefort").GetInt32());
        Assert.Equal(1, torr.GetProperty("perBevistrinn").GetProperty("E1").GetInt32());
        Assert.Equal(0, torr.GetProperty("referanserader").GetProperty("aktive").GetInt32());
        Assert.False((await SendAsync(HttpMethod.Get, $"/api/rettskilder/{forskriftId}/hjemmelsstatus")).GetProperty("beregnet").GetBoolean());

        var skrevet = await SendAsync(HttpMethod.Post, $"/api/hjemmelsstatus/maskinregel?skriv=true&lovtidend=false&rettskildeId={forskriftId}", bruker);
        Assert.Equal(1, skrevet.GetProperty("skrevne").GetInt32());

        var side = await SendAsync(HttpMethod.Get, $"/api/rettskilder/{forskriftId}/hjemmelsstatus");
        Assert.True(side.GetProperty("beregnet").GetBoolean());
        Assert.Equal("viderefort", side.GetProperty("samletStatus").GetString());
        var h = side.GetProperty("hjemler")[0];
        Assert.Equal("materiell", h.GetProperty("rolle").GetString());
        Assert.Equal("§ 5", h.GetProperty("bestemmelse").GetString());
        Assert.Equal("§ 23-3 fjerde ledd", h.GetProperty("opphevetAv").GetProperty("bestemmelse").GetString());
        Assert.Equal($"LOV-2015-04-10-{nr}", h.GetProperty("opphevetAv").GetProperty("referanse").GetString());
        Assert.Equal("V1", h.GetProperty("videreforing").GetProperty("form").GetString());
        Assert.Equal("dekket", h.GetProperty("videreforing").GetProperty("dekning").GetString());
        Assert.Equal("tekstfunn", h.GetProperty("kunnskapsstatus").GetString());
    }
}
