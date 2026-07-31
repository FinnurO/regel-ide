using Microsoft.Extensions.Configuration;
using RegelIde.Api;

namespace RegelIde.Api.Tests;

/// <summary>
/// Enhetstester for lesing av sti-prefikset og omskriving av <c>&lt;base href&gt;</c>. At rutingen
/// faktisk virker under et prefiks er verifisert ende-til-ende mot containeren — se
/// docs/deploy-altinn-app-cluster.md.
/// </summary>
public class StiprefiksTests
{
    private static IConfiguration Konfig(string? verdi) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>(Stiprefiks.Konfigurasjonsnokkel, verdi)])
            .Build();

    // ---------- Lesing ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    public void Tomt_prefiks_gir_null_slik_at_lokal_kjoring_er_upaavirket(string? verdi)
        => Assert.Null(Stiprefiks.Les(Konfig(verdi)));

    [Theory]
    [InlineData("/ttd/finnuro-poc-regel-editor", "/ttd/finnuro-poc-regel-editor")]
    [InlineData("/ttd/finnuro-poc-regel-editor/", "/ttd/finnuro-poc-regel-editor")]
    [InlineData("  /ttd/app/  ", "/ttd/app")]
    public void Prefiks_normaliseres_uten_avsluttende_skratrek(string verdi, string forventet)
        => Assert.Equal(forventet, Stiprefiks.Les(Konfig(verdi)));

    [Fact]
    public void Prefiks_uten_innledende_skratrek_feiler_ved_oppstart()
    {
        // UsePathBase krever innledende '/'. Uten sjekken starter appen fint og svarer 404 på alt,
        // som er en langt vanskeligere feil å finne enn en tydelig oppstartsfeil.
        var feil = Assert.Throws<InvalidOperationException>(() => Stiprefiks.Les(Konfig("ttd/app")));

        Assert.Contains(Stiprefiks.Konfigurasjonsnokkel, feil.Message);
        Assert.Contains("må starte med", feil.Message);
    }

    // ---------- base href ----------

    private const string Indeks = """
        <!doctype html>
        <html lang="nb">
          <head>
            <base href="/" />
            <link rel="icon" href="favicon.svg" />
          </head>
        </html>
        """;

    [Fact]
    public void Base_href_settes_til_prefikset_med_avsluttende_skratrek()
    {
        // Skråstreken er ikke kosmetikk: uten den tolker nettleseren siste segment som et filnavn
        // og kaster det, slik at assets/x.js løses mot /ttd/ i stedet for /ttd/app/.
        var html = Stiprefiks.SettBaseHref(Indeks, "/ttd/app");

        Assert.Contains("""<base href="/ttd/app/" />""", html);
        Assert.DoesNotContain("""<base href="/" />""", html);
    }

    [Fact]
    public void Uten_prefiks_blir_base_href_staaende_paa_rot()
    {
        var html = Stiprefiks.SettBaseHref(Indeks, null);

        Assert.Contains("""<base href="/" />""", html);
    }

    [Fact]
    public void Resten_av_dokumentet_er_urort()
    {
        var html = Stiprefiks.SettBaseHref(Indeks, "/ttd/app");

        Assert.Contains("""<link rel="icon" href="favicon.svg" />""", html);
        Assert.Contains("<!doctype html>", html);
    }

    [Fact]
    public void Plassholderen_finnes_faktisk_i_index_html()
    {
        // Fanger opp at noen endrer index.html uten å endre plassholderen her. Da ville
        // omskrivingen blitt en stille no-op, og appen ville vært knekt kun under et prefiks.
        var sti = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "RegelIde.Web", "index.html");
        Assert.True(File.Exists(sti), $"Fant ikke {Path.GetFullPath(sti)}");

        var html = File.ReadAllText(sti);
        Assert.NotEqual(html, Stiprefiks.SettBaseHref(html, "/ttd/app"));
    }
}
