using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RegelIde.Api;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Punkt 8 (avklaringsrunde 2026-08-13) — <c>/api/nettsider</c> er fjernet; en nettside ER nå en
/// ordinær <see cref="RettskildeEntitet"/> (Kildetype="Brukerveiledning"), vist via de vanlige
/// <c>/api/rettskilder</c>-endepunktene + de to nye §3.4/§3.2-endepunktene (stier/nettside-lenker).
/// Denne filen ERSTATTER <c>NettsiderEndepunktTests.cs</c> (fjernet) — samme dekning som der (at
/// Bergen-nettsidenes lenker faktisk løser mot rettskilder), nå verifisert mot de nye endepunktene i
/// stedet for de fjernede <c>/api/nettsider</c>-endepunktene.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class BrukerveiledningEndepunktTests
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public BrukerveiledningEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _client = fixture.Factory.CreateClient();
    }

    private async Task<RettskildeSammendrag> HentBundlingssidenAsync()
    {
        var liste = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);
        return liste!.Single(r => r.Kildetype == "Brukerveiledning" && r.Tittel.Contains("Retningslinjer for tildeling"));
    }

    [Fact]
    public async Task Rettskilder_listen_inneholder_alle_23_bergen_brukerveiledningene()
    {
        var virksomheter = await _client.GetFromJsonAsync<List<VirksomhetDto>>("/api/virksomheter", JsonInnstillinger);
        var bergen = virksomheter!.Single(v => v.Navn == "Bergen kommune");

        var liste = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>($"/api/rettskilder?virksomhetId={bergen.Id}", JsonInnstillinger);

        Assert.NotNull(liste);
        Assert.Equal(23, liste!.Count(r => r.Kildetype == "Brukerveiledning"));
    }

    [Fact]
    public async Task Brukerveiledning_har_akkurat_en_side_node_med_raatekst_og_er_ikke_utkast()
    {
        var bundling = await HentBundlingssidenAsync();

        var detalj = await _client.GetFromJsonAsync<RettskildeDetalj>($"/api/rettskilder/{bundling.Id}", JsonInnstillinger);
        Assert.Equal("Gjeldende", detalj!.Status); // synlig i åpne-data-endepunktet, ikke en kladd.

        var noder = await _client.GetFromJsonAsync<List<RettskildeNodeDto>>($"/api/rettskilder/{bundling.Id}/noder", JsonInnstillinger);
        var sideNode = Assert.Single(noder!);
        Assert.Equal("side", sideNode.NodeType);
        // Eid er nå sidens egen KanoniskUrl/Url (rettet 2026-08-14 — literal "side" kolliderte på
        // tvers av ALLE Brukerveiledning-rader, se BrukerveiledningImportTjeneste-kommentaren).
        Assert.Equal(detalj.Url, sideNode.Eid);
        Assert.NotNull(sideNode.Tekst);
    }

    [Fact]
    public async Task Brukerveiledning_stier_endepunktet_viser_minst_en_navigasjonssti()
    {
        var bundling = await HentBundlingssidenAsync();

        var stier = await _client.GetFromJsonAsync<List<NettsideStiDto>>($"/api/rettskilder/{bundling.Id}/stier", JsonInnstillinger);

        Assert.NotNull(stier);
        Assert.True(stier!.Count >= 1);
    }

    [Fact]
    public async Task Stier_for_ukjent_rettskilde_gir_404()
    {
        var svar = await _client.GetAsync($"/api/rettskilder/{Guid.NewGuid()}/stier");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }

    [Fact]
    public async Task Bundlingssiden_har_nettside_lenker_lost_til_ekte_rettskilder()
    {
        var bundling = await HentBundlingssidenAsync();

        var lenker = await _client.GetFromJsonAsync<List<NettsideLenkeMedMalDto>>($"/api/rettskilder/{bundling.Id}/nettside-lenker", JsonInnstillinger);

        Assert.NotNull(lenker);

        var lovdatalenkeAlkoholloven = lenker!.Single(l => l.RaaHref == "https://lovdata.no/dokument/NL/lov/1989-06-02-27");
        Assert.Equal("lovdatalenke", lovdatalenkeAlkoholloven.Type);
        Assert.NotNull(lovdatalenkeAlkoholloven.TilRettskildeId);
        Assert.Equal("https://lovdata.no/eli/lov/1989/06/02/27/nor", lovdatalenkeAlkoholloven.TilRettskildeEli);

        var lovdatalenkeForskriften = lenker!.Single(l => l.RaaHref == "https://lovdata.no/dokument/SF/forskrift/2005-06-08-538");
        Assert.NotNull(lovdatalenkeForskriften.TilRettskildeId);

        // PDF-omtale-lenken (§2 Lag 1) — matchet på RettskildeEntitet.Url, ikke Eli, siden retningslinjene
        // ikke har noen ELI. TilRettskildeTittel bekrefter at det er RETT rettskilde, ikke bare "en" rettskilde.
        var pdfLenke = lenker!.Single(l => l.RaaHref == "/api/rest/filer/V51903878");
        Assert.Equal("lenker_til", pdfLenke.Type);
        Assert.NotNull(pdfLenke.TilRettskildeId);
        Assert.Contains("Retningslinjer for tildeling", pdfLenke.TilRettskildeTittel);
    }

    [Fact]
    public async Task Nettside_lenker_for_ukjent_rettskilde_gir_404()
    {
        var svar = await _client.GetAsync($"/api/rettskilder/{Guid.NewGuid()}/nettside-lenker");
        Assert.Equal(HttpStatusCode.NotFound, svar.StatusCode);
    }
}
