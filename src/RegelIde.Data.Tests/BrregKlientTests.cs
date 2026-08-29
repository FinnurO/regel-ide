using System.Net;
using System.Text;

namespace RegelIde.Data.Tests;

/// <summary>
/// docs/13-backlog.md §9 — <see cref="BrregKlient"/>. Samme stub-<see cref="HttpMessageHandler"/>-
/// prinsipp som <see cref="OppgaveregisterHenterTests"/>/<see cref="AltinnRessursHenterTests"/> — ingen
/// ekte nettverkskall mot data.brreg.no i test-suiten.
/// </summary>
public class BrregKlientTests
{
    /// <summary>Ruter på URL-mønster i stedet for én fast svar-kropp (til forskjell fra de andre
    /// hosternes enklere stubber) — <see cref="BrregKlient.HentPaOrgnrAsync"/> prøver to ulike
    /// endepunkt (<c>/enheter/{orgnr}</c> så <c>/underenheter/{orgnr}</c>), og testene under trenger å
    /// kunne simulere at det ene finnes og det andre ikke (eller omvendt).</summary>
    private sealed class RutetStubHandler(IReadOnlyDictionary<string, (HttpStatusCode Status, string Body)> ruter) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var sti = request.RequestUri!.AbsolutePath + (request.RequestUri.Query);
            var treff = ruter.FirstOrDefault(r => sti.Contains(r.Key));
            var (status, body) = treff.Value == default ? (HttpStatusCode.NotFound, "{}") : treff.Value;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private static BrregKlient LagKlient(IReadOnlyDictionary<string, (HttpStatusCode, string)> ruter) =>
        new(new HttpClient(new RutetStubHandler(ruter)));

    private const string MiljodirektoratetJson = """
    {
      "organisasjonsnummer": "999601391",
      "navn": "MILJØDIREKTORATET",
      "organisasjonsform": { "kode": "ORGL", "beskrivelse": "Organisasjonsledd" },
      "institusjonellSektorkode": { "kode": "6100", "beskrivelse": "Statsforvaltningen" },
      "hjemmeside": "www.miljodirektoratet.no"
    }
    """;

    [Fact]
    public async Task SokAsync_bygger_navnesok_for_ikke_numerisk_tekst()
    {
        var kalteUrler = new List<string>();
        var handler = new SporHandler(kalteUrler, HttpStatusCode.OK, """{ "_embedded": { "enheter": [] } }""");
        var klient = new BrregKlient(new HttpClient(handler));

        await klient.SokAsync("Miljødirektoratet");

        Assert.Single(kalteUrler);
        Assert.Contains("navn=Milj", kalteUrler[0]); // URL-enkodet, sjekker prefiks er nok
        Assert.DoesNotContain("organisasjonsnummer=", kalteUrler[0]);
    }

    [Fact]
    public async Task SokAsync_tolker_9_sifre_som_organisasjonsnummer_ikke_navn()
    {
        var kalteUrler = new List<string>();
        var handler = new SporHandler(kalteUrler, HttpStatusCode.OK, """{ "_embedded": { "enheter": [] } }""");
        var klient = new BrregKlient(new HttpClient(handler));

        await klient.SokAsync("999601391");

        Assert.Contains("organisasjonsnummer=999601391", kalteUrler[0]);
        Assert.DoesNotContain("navn=", kalteUrler[0]);
    }

    [Fact]
    public async Task SokAsync_parser_treff_riktig_fra_ekte_brreg_responsform()
    {
        var handler = new SporHandler([], HttpStatusCode.OK, $$"""{ "_embedded": { "enheter": [{{MiljodirektoratetJson}}] } } """);
        var klient = new BrregKlient(new HttpClient(handler));

        var treff = await klient.SokAsync("Miljødirektoratet");

        var enhet = Assert.Single(treff);
        Assert.Equal("999601391", enhet.Organisasjonsnummer);
        Assert.Equal("MILJØDIREKTORATET", enhet.Navn);
        Assert.Equal("ORGL", enhet.Organisasjonsform?.Kode);
        Assert.Equal("6100", enhet.InstitusjonellSektorkode?.Kode);
        Assert.True(enhet.ErAktiv);
    }

    [Fact]
    public async Task HentPaOrgnrAsync_finner_hovedenhet_direkte()
    {
        var klient = LagKlient(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["/enheter/999601391"] = (HttpStatusCode.OK, MiljodirektoratetJson),
        });

        var enhet = await klient.HentPaOrgnrAsync("999601391");

        Assert.NotNull(enhet);
        Assert.Equal("MILJØDIREKTORATET", enhet!.Navn);
    }

    /// <summary>Selve fallback-mekanismen — hovedenhet-endepunktet svarer 404, underenhet-endepunktet
    /// finner den. Uten dette ville enhver Brreg-underenhet (driftsenhet/avdeling) blitt uopprettelig
    /// via dette verktøyet.</summary>
    [Fact]
    public async Task HentPaOrgnrAsync_faller_tilbake_til_underenhet_nar_hovedenhet_ikke_finnes()
    {
        var klient = LagKlient(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["/enheter/912345678"] = (HttpStatusCode.NotFound, "{}"),
            ["/underenheter/912345678"] = (HttpStatusCode.OK, """{ "organisasjonsnummer": "912345678", "navn": "En underenhet" }"""),
        });

        var enhet = await klient.HentPaOrgnrAsync("912345678");

        Assert.NotNull(enhet);
        Assert.Equal("En underenhet", enhet!.Navn);
    }

    [Fact]
    public async Task HentPaOrgnrAsync_returnerer_null_nar_ingen_av_endepunktene_finner_noe()
    {
        var klient = LagKlient(new Dictionary<string, (HttpStatusCode, string)>
        {
            ["/enheter/000000000"] = (HttpStatusCode.NotFound, "{}"),
            ["/underenheter/000000000"] = (HttpStatusCode.NotFound, "{}"),
        });

        var enhet = await klient.HentPaOrgnrAsync("000000000");

        Assert.Null(enhet);
    }

    /// <summary>Fanger opp URL-ene faktisk kalt, til å verifisere spørreparameter-bygging isolert fra
    /// selve responsen.</summary>
    private sealed class SporHandler(List<string> kalteUrler, HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            kalteUrler.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
