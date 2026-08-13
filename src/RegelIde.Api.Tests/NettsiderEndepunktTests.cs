using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RegelIde.Api;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstester for <c>/api/nettsider</c> — kjører hele API-et (inkl. migrasjon +
/// <see cref="RegelIde.Data.BergenKorpusSeed"/>-seedingen i Program.cs) mot en ekte, embedded Postgres-
/// instans og de ekte fixturene i data/kilder/raw-nettside/.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class NettsiderEndepunktTests
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public NettsiderEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    private async Task<NettsideSammendragDto> HentBundlingssidenAsync()
    {
        var liste = await _client.GetFromJsonAsync<List<NettsideSammendragDto>>("/api/nettsider", JsonInnstillinger);
        return liste!.Single(d => d.KanoniskUrl.Contains("retningslinjer-for-tildeling"));
    }

    [Fact]
    public async Task Listen_inneholder_alle_23_bergen_sidene_med_stitype_badges()
    {
        var liste = await _client.GetFromJsonAsync<List<NettsideSammendragDto>>("/api/nettsider", JsonInnstillinger);

        Assert.NotNull(liste);
        Assert.Equal(23, liste!.Count);
        var fettutskiller = liste.Single(d => d.KanoniskUrl.EndsWith("krav-om-fettutskiller"));
        Assert.Equal(["tematisk"], fettutskiller.StiTyper);
    }

    [Fact]
    public async Task Virksomhetsfilter_snevrer_inn_til_bergens_egen_liste()
    {
        var alle = await _client.GetFromJsonAsync<List<NettsideSammendragDto>>("/api/nettsider", JsonInnstillinger);
        var virksomheter = await _client.GetFromJsonAsync<List<VirksomhetDto>>("/api/virksomheter", JsonInnstillinger);
        var bergen = virksomheter!.Single(v => v.Navn == "Bergen kommune");

        var filtrert = await _client.GetFromJsonAsync<List<NettsideSammendragDto>>($"/api/nettsider?virksomhetId={bergen.Id}", JsonInnstillinger);

        Assert.Equal(alle!.Count, filtrert!.Count); // alle 23 er Bergens egne i denne rundens korpus.
    }

    [Fact]
    public async Task Ukjent_id_gir_404()
    {
        var svar = await _client.GetAsync($"/api/nettsider/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Bundlingssiden_har_lovdatalenker_lost_til_ekte_rettskilder_og_pdf_lenke_lost_til_retningslinjene()
    {
        var sammendrag = await HentBundlingssidenAsync();
        var detalj = await _client.GetFromJsonAsync<NettsideDetaljDto>($"/api/nettsider/{sammendrag.Id}", JsonInnstillinger);

        Assert.NotNull(detalj);
        Assert.NotNull(detalj!.RaaTekst);
        Assert.True(detalj.Stier.Count >= 1);

        var lovdatalenkeAlkoholloven = detalj.Lenker.Single(l => l.RaaHref == "https://lovdata.no/dokument/NL/lov/1989-06-02-27");
        Assert.Equal("lovdatalenke", lovdatalenkeAlkoholloven.Type);
        Assert.NotNull(lovdatalenkeAlkoholloven.TilRettskildeId);
        Assert.Equal("https://lovdata.no/eli/lov/1989/06/02/27/nor", lovdatalenkeAlkoholloven.TilRettskildeEli);

        var lovdatalenkeForskriften = detalj.Lenker.Single(l => l.RaaHref == "https://lovdata.no/dokument/SF/forskrift/2005-06-08-538");
        Assert.NotNull(lovdatalenkeForskriften.TilRettskildeId);

        // PDF-omtale-lenken (§2 Lag 1) — matchet på RettskildeEntitet.Url, ikke Eli, siden retningslinjene
        // ikke har noen ELI. TilRettskildeTittel bekrefter at det er RETT rettskilde, ikke bare "en" rettskilde.
        var pdfLenke = detalj.Lenker.Single(l => l.RaaHref == "/api/rest/filer/V51903878");
        Assert.Equal("lenker_til", pdfLenke.Type);
        Assert.NotNull(pdfLenke.TilRettskildeId);
        Assert.Contains("Retningslinjer for tildeling", pdfLenke.TilRettskildeTittel);
    }
}
