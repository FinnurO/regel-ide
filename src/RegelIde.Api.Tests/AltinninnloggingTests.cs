using Microsoft.AspNetCore.Http;
using RegelIde.Api.Autentisering;

namespace RegelIde.Api.Tests;

/// <summary>
/// Enhetstester for omdirigeringen til Altinns innlogging. Det som testes her er avgjørelsene:
/// hva som er en nettleser-navigasjon, og hvilken URL brukeren sendes til. Selve pipeline-koblingen
/// er verifisert mot en kjørende container — se docs/deploy-altinn-app-cluster.md.
/// </summary>
public class AltinninnloggingTests
{
    private static HttpRequest Forespørsel(
        string sti, string metode = "GET", string? accept = "text/html,application/xhtml+xml")
    {
        var kontekst = new DefaultHttpContext();
        kontekst.Request.Method = metode;
        kontekst.Request.Path = sti;
        if (accept is not null) kontekst.Request.Headers.Accept = accept;
        return kontekst.Request;
    }

    // ---------- Hva som skal redirectes ----------

    [Theory]
    [InlineData("/")]
    [InlineData("/vilkarstre")]
    [InlineData("/rettskilder/noe/dypt")]
    public void Dokumentforespørsler_fra_nettleser_skal_til_innlogging(string sti)
        => Assert.True(Altinninnlogging.ErNettlesernavigasjon(Forespørsel(sti)));

    [Theory]
    [InlineData("/health")]
    [InlineData("/helse")]
    public void Helsesjekkene_redirectes_aldri(string sti)
    {
        // Klyngen spør uten cookie. En redirect her ville gjort at probene aldri ble klare, og
        // appen ville sett død ut for Kubernetes selv om den var helt frisk. Testen bruker
        // text/html med vilje: den skal feile på stien, ikke på at proben tilfeldigvis sender */*.
        Assert.False(Altinninnlogging.ErNettlesernavigasjon(Forespørsel(sti)));
    }

    [Theory]
    [InlineData("/api/meg")]
    [InlineData("/api/oppsett")]
    [InlineData("/api/rettskilder/noe")]
    public void Api_kall_redirectes_aldri(string sti)
    {
        // API-et skal svare 401 slik at klienten kan reagere. En 302 til plattformen ville blitt
        // fulgt av fetch og gitt et uforståelig CORS-brudd i stedet for en statuskode.
        Assert.False(Altinninnlogging.ErNettlesernavigasjon(Forespørsel(sti)));
    }

    [Fact]
    public void Sti_som_bare_begynner_paa_samme_tekst_er_ikke_unntatt()
    {
        // "/apier" er ikke "/api". Prefikssjekken må se på segmentgrensen.
        Assert.True(Altinninnlogging.ErNettlesernavigasjon(Forespørsel("/apier")));
        Assert.True(Altinninnlogging.ErNettlesernavigasjon(Forespørsel("/helsestasjon")));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public void Bare_get_redirectes(string metode)
        => Assert.False(Altinninnlogging.ErNettlesernavigasjon(Forespørsel("/vilkarstre", metode)));

    [Theory]
    [InlineData(null)]
    [InlineData("*/*")]
    [InlineData("application/json")]
    public void Forespørsler_som_ikke_ber_om_html_redirectes_ikke(string? accept)
        => Assert.False(Altinninnlogging.ErNettlesernavigasjon(Forespørsel("/vilkarstre", accept: accept)));

    // ---------- Retur-URL ----------

    [Fact]
    public void Retur_url_tvinges_til_https_bak_terminerende_proxy()
    {
        // TLS termineres i ingressen, så Kestrel ser ren HTTP. Sendte vi det som goto, ville
        // brukeren kommet tilbake over HTTP.
        var kontekst = new DefaultHttpContext();
        kontekst.Request.Scheme = "http";
        kontekst.Request.Host = new HostString("ttd.apps.at23.altinn.cloud");
        kontekst.Request.PathBase = "/ttd/finnuro-poc-regel-editor";
        kontekst.Request.Path = "/vilkarstre";

        Assert.Equal(
            "https://ttd.apps.at23.altinn.cloud/ttd/finnuro-poc-regel-editor/vilkarstre",
            Altinninnlogging.ByggReturUrl(kontekst.Request, tvingHttps: true));
    }

    [Fact]
    public void Retur_url_beholder_sti_prefikset_og_query()
    {
        // Uten PathBase ville returen landet utenfor appen og gitt 404 gjennom ingressen.
        var kontekst = new DefaultHttpContext();
        kontekst.Request.Scheme = "https";
        kontekst.Request.Host = new HostString("ttd.apps.at23.altinn.cloud");
        kontekst.Request.PathBase = "/ttd/app";
        kontekst.Request.Path = "/rettskilder";
        kontekst.Request.QueryString = new QueryString("?id=42");

        Assert.Equal(
            "https://ttd.apps.at23.altinn.cloud/ttd/app/rettskilder?id=42",
            Altinninnlogging.ByggReturUrl(kontekst.Request, tvingHttps: false));
    }

    [Fact]
    public void Lokal_kjoring_beholder_sitt_eget_skjema()
    {
        var kontekst = new DefaultHttpContext();
        kontekst.Request.Scheme = "http";
        kontekst.Request.Host = new HostString("localhost:8080");
        kontekst.Request.Path = "/";

        Assert.Equal("http://localhost:8080/",
            Altinninnlogging.ByggReturUrl(kontekst.Request, tvingHttps: false));
    }

    // ---------- Innloggings-URL ----------

    [Fact]
    public void Innloggings_url_peker_paa_plattformens_authentication_endepunkt()
        => Assert.Equal(
            "https://platform.at23.altinn.cloud/authentication/api/v1/authentication"
            + "?goto=https%3A%2F%2Fttd.apps.at23.altinn.cloud%2Fttd%2Fapp%2Fvilkarstre",
            Altinninnlogging.ByggInnloggingsUrl(
                "https://platform.at23.altinn.cloud",
                "https://ttd.apps.at23.altinn.cloud/ttd/app/vilkarstre"));

    [Fact]
    public void Avsluttende_skratrek_paa_plattformen_gir_ikke_dobbel_skratrek()
        => Assert.StartsWith(
            "https://platform.tt02.altinn.no/authentication/",
            Altinninnlogging.ByggInnloggingsUrl("https://platform.tt02.altinn.no/", "https://x/"));

    // ---------- Feilsiden ----------

    [Fact]
    public void Feilsiden_navngir_plattformen_som_ble_validert_mot()
    {
        // Feilsiden vises når innloggingen gikk bra hos Altinn, men vi ikke godtok cookien. I
        // praksis er det alltid plattform-URL-en som er feil, så den må stå der.
        var side = Altinninnlogging.Feilside("https://platform.tt02.altinn.no");

        Assert.Contains("https://platform.tt02.altinn.no", side);
        Assert.Contains("RegelIde__Altinn__Plattform", side);
    }
}
