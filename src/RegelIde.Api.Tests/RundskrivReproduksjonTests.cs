using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RegelIde.Api;
using RegelIde.Data;
using Xunit.Abstractions;

namespace RegelIde.Api.Tests;

/// <summary>
/// Reproduksjonstest mot `docs/kildegrunnlag/skjenkebevilling-rundskriv-fasit.md` (versjon 4,
/// 2026-07-31) — Johanns eksplisitte spørsmål: "kan du lage en test som prøver å reprodusere den
/// filen via applikasjonen ... for å se om det er mulig". Regel-ide genererer ingen prosa, så dette
/// er IKKE en tekstlikhet-test — det er en DEKNINGSTEST: for hver §-seksjon i kildedokumentet,
/// bekreftes om et representativt fragment faktisk kan gjenfinnes i den ekte, live-genererte
/// veiledningen (`GET /api/tjenester/{id}/veiledning`), eller om seksjonen bekreftet IKKE kan
/// representeres med dagens datamodell (§3/§9/§12 — se docs/12-fasit-handbok-leveranse.md).
///
/// To seksjoner (§6/§11) demonstreres via den EKTE forfatter-mekanismen (POST
/// /api/vilkarstre-kommentarer) i Arrange-delen, ikke en test-only bypass — det er nøyaktig den
/// samme HTTP-veien en jurist ville brukt fra Egenskapspanelets "Veiledning"-fane.
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class RundskrivReproduksjonTests
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public RundskrivReproduksjonTests(EmbeddedPostgresApiFixture fixture, ITestOutputHelper output)
    {
        _client = fixture.Factory.CreateClient();
        _output = output;
    }

    private static HttpRequestMessage MedBruker(HttpMethod metode, string url, Guid brukerId) =>
        new(metode, url) { Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString() } } };

    private async Task<Guid> HentJuristIdAsync()
    {
        var brukere = await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", JsonInnstillinger);
        return brukere!.Single(b => b.Rolle == "Jurist").Id;
    }

    private async Task<Guid> HentTjenesteIdAsync(Guid juristId)
    {
        var respons = await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/tjenester", juristId));
        var tjenester = await respons.Content.ReadFromJsonAsync<List<TjenesteDto>>(JsonInnstillinger);
        return tjenester!.Single(t => t.Tittel == "Alminnelig skjenkebevilling").Id;
    }

    private static VeiledningNodeDto? FinnNode(VeiledningNodeDto node, string tittel)
    {
        if (node.Tittel == tittel) return node;
        foreach (var barn in node.Barn)
        {
            var funnet = FinnNode(barn, tittel);
            if (funnet is not null) return funnet;
        }
        return null;
    }

    /// <summary>Slår sammen all synlig tekst i treet (titler, hjemmel, kommentar-HTML) til ett søkbart dokument — selve "gjenfinn et fragment"-mekanismen denne testen bruker.</summary>
    private static string SamleAllTekst(VeiledningNodeDto node)
    {
        var sb = new StringBuilder();
        void Besok(VeiledningNodeDto n)
        {
            sb.Append(n.Tittel).Append(' ').Append(n.Beskrivelse).Append(' ');
            foreach (var g in n.JuridiskGrunnlag) sb.Append(g.Kilde).Append(' ').Append(g.EId).Append(' ');
            foreach (var k in n.Kommentarer) sb.Append(k.TekstHtml).Append(' ');
            foreach (var b in n.Barn) Besok(b);
            foreach (var u in n.Unntak)
            {
                sb.Append(u.Tittel).Append(' ').Append(u.Beskrivelse).Append(' ');
                foreach (var k in u.Kommentarer) sb.Append(k.TekstHtml).Append(' ');
            }
        }
        Besok(node);
        return sb.ToString();
    }

    [Fact]
    public async Task Paragraf2_saksgangens_fem_sporsmal_gjenfinnes_som_beslutningssekvensen_i_treet()
    {
        var juristId = await HentJuristIdAsync();
        var tjenesteId = await HentTjenesteIdAsync(juristId);
        var veiledning = await _client.GetFromJsonAsync<VeiledningDto>($"/api/tjenester/{tjenesteId}/veiledning", JsonInnstillinger);

        // §2 lister habilitet, formalia, serveringsbevilling, vandel, kvalifikasjon, kommunalt skjønn.
        // Av disse har dagens vilkårstre strukturert dekning for vandel (Vandelsvilkår), kvalifikasjon
        // (Aldersvilkår) og kommunalt skjønn (R-SKJENKETID + DatasettVerdi) — de tre andre er §3/§4/§5,
        // bekreftet IKKE representerbare (se egen test under).
        Assert.NotNull(FinnNode(veiledning!.Rot, "Vandelsvilkår"));
        Assert.NotNull(FinnNode(veiledning.Rot, "Aldersvilkår"));
        Assert.NotNull(FinnNode(veiledning.Rot, "Klokkeslettsvilkår"));
    }

    [Fact]
    public async Task Paragraf3_4_5_og_9_og_12_har_ingen_representasjon_i_dagens_datamodell()
    {
        // Dette er IKKE en midlertidig svikt å fikse — det er en bekreftelse av kjente,
        // dokumenterte gap (docs/12-fasit-handbok-leveranse.md). Testen feiler bevisst hvis noen i en
        // fremtidig runde bygger dekning uten å oppdatere denne testen og skåringstabellen sammen.
        var juristId = await HentJuristIdAsync();
        var tjenesteId = await HentTjenesteIdAsync(juristId);
        var veiledning = await _client.GetFromJsonAsync<VeiledningDto>($"/api/tjenester/{tjenesteId}/veiledning", JsonInnstillinger);
        var heleTreet = SamleAllTekst(veiledning!.Rot);

        // §3 Habilitet (fvl. § 8) — saksbehandlerens EGEN habilitet, ikke søkerens eligibility. Passer
        // ikke i Vilkår/Regelnode-ontologien (som alltid evaluerer søkeren), derfor ingen "habilitet"-tekst.
        Assert.DoesNotContain("habil", heleTreet, StringComparison.OrdinalIgnoreCase);

        // §4 Formalia (fvl. § 17, søknad komplett) og §5 Serveringsbevilling — ingen egen Vilkår-node.
        Assert.DoesNotContain("serveringsbevilling", heleTreet, StringComparison.OrdinalIgnoreCase);

        // §9 Prikkbelastning / Gyldighet (vedtaks-varighet) — Vedtaksvirkning eies bevisst av
        // forklaringsmodell-api, ikke regel-ide (docs/01-referansemodell.md).
        Assert.DoesNotContain("prikkbelastning", heleTreet, StringComparison.OrdinalIgnoreCase);

        // §12 Relevante tjenester — TjenesteDto har ikke noe "relaterte tjenester"-felt å inspisere i
        // det hele tatt (bekreftet ved kodegjennomgang av Dtos.cs) — det ER selve gapet.
        Assert.DoesNotContain("Omsetningsoppgave", heleTreet, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Paragraf6_arsaker_til_avslag_kan_representeres_via_veiledningskommentar_mekanismen()
    {
        // §6 Vandelsvurdering sin liste over ni konkrete avslagsgrunner er FAKTA/kunnskap en jurist
        // typisk skriver ned i håndbok-arbeidet, ikke noe vilkårstreet strukturerer i dag. Dette
        // demonstrerer at den EKSISTERENDE VilkarstreKommentar-mekanismen (samme POST-endepunkt som
        // Egenskapspanelets "Veiledning"-fane bruker) er tilstrekkelig til å bære innholdet — men det
        // krever at en forfatter faktisk skriver det inn, ingen automatikk.
        var juristId = await HentJuristIdAsync();
        var tjenesteId = await HentTjenesteIdAsync(juristId);
        var veiledningFor = await _client.GetFromJsonAsync<VeiledningDto>($"/api/tjenester/{tjenesteId}/veiledning", JsonInnstillinger);
        var vandelsvilkar = FinnNode(veiledningFor!.Rot, "Vandelsvilkår");
        Assert.NotNull(vandelsvilkar);

        var opprettRespons = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/vilkarstre-kommentarer")
        {
            Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, juristId.ToString() } },
            Content = JsonContent.Create(new
            {
                malType = "vilkar",
                malId = vandelsvilkar!.Id,
                dokumenttype = "kommentar",
                tekstHtml = "<p>Manglende innlevering av mva-oppgaver kan gi avslag på vandelsgrunnlag.</p>",
            }),
        });
        opprettRespons.EnsureSuccessStatusCode();

        var veiledningEtter = await _client.GetFromJsonAsync<VeiledningDto>($"/api/tjenester/{tjenesteId}/veiledning", JsonInnstillinger);
        var heleTreet = SamleAllTekst(veiledningEtter!.Rot);
        Assert.Contains("mva-oppgaver", heleTreet, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Paragraf11_sjekkliste_kan_representeres_som_ekte_liste_ikke_bare_nummererte_avsnitt()
    {
        // §11 (bevisst duplikatnummerert i kildedokumentet — se docs/12-fasit-handbok-leveranse.md
        // "Prinsipp: rekkefølge og nummerering er alltid beregnet") er en avkrysningsbar sjekkliste.
        // Sanitizeren tillater ul/li (denne sesjonen) — dette bekrefter at MEKANISMEN virker
        // ende-til-ende via det ekte endepunktet, ikke bare i en isolert sanitizer-enhetstest.
        var juristId = await HentJuristIdAsync();
        var tjenesteId = await HentTjenesteIdAsync(juristId);
        var veiledningFor = await _client.GetFromJsonAsync<VeiledningDto>($"/api/tjenester/{tjenesteId}/veiledning", JsonInnstillinger);
        var vandelsvilkar = FinnNode(veiledningFor!.Rot, "Vandelsvilkår");

        var opprettRespons = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/vilkarstre-kommentarer")
        {
            Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, juristId.ToString() } },
            Content = JsonContent.Create(new
            {
                malType = "vilkar",
                malId = vandelsvilkar!.Id,
                dokumenttype = "sjekkliste",
                tekstHtml = "<ul><li>Kontrollert organisasjonsnummer</li><li>Kontrollert skatteattest</li></ul>",
            }),
        });
        opprettRespons.EnsureSuccessStatusCode();

        var veiledningEtter = await _client.GetFromJsonAsync<VeiledningDto>($"/api/tjenester/{tjenesteId}/veiledning", JsonInnstillinger);
        var sjekklisteKommentar = FinnNode(veiledningEtter!.Rot, "Vandelsvilkår")!.Kommentarer.Single(k => k.Dokumenttype == "sjekkliste");
        Assert.Contains("<ul>", sjekklisteKommentar.TekstHtml);
        Assert.Contains("Kontrollert organisasjonsnummer", sjekklisteKommentar.TekstHtml);
    }

    [Fact]
    public async Task Paragraf8_kommunal_skjonnsvurdering_dekkes_delvis_kun_klokkeslett_er_strukturert()
    {
        // §8s tabell har fem parametre (maks bevillinger, forbudte konsepter, politisk behandling,
        // kunnskapsprøve-krav, kommunale tilleggsvilkår) — kun klokkeslett er faktisk seedet som
        // DatasettVerdi i dag (se KommunaleParametreSeed). Dette er en ærlig, delvis-dekning-test,
        // ikke en fullstendig én-til-én-reproduksjon.
        var juristId = await HentJuristIdAsync();
        var tjenesteId = await HentTjenesteIdAsync(juristId);
        var virksomheter = await _client.GetFromJsonAsync<List<VirksomhetDto>>("/api/virksomheter", JsonInnstillinger);
        var tonsbergId = virksomheter!.Single(v => v.Navn == "Tønsberg kommune").Id;

        var veiledning = await _client.GetFromJsonAsync<VeiledningDto>(
            $"/api/tjenester/{tjenesteId}/veiledning?virksomhetId={tonsbergId}", JsonInnstillinger);
        var klokkeslettsvilkar = FinnNode(veiledning!.Rot, "Klokkeslettsvilkår");
        Assert.NotNull(klokkeslettsvilkar);
        Assert.Single(klokkeslettsvilkar!.InputDatasettVerdier);

        var heleTreet = SamleAllTekst(veiledning.Rot);
        Assert.DoesNotContain("Ansvarlig vertskap", heleTreet, StringComparison.OrdinalIgnoreCase); // kommunalt tilleggsvilkår — ikke modellert
    }

    /// <summary>
    /// Samlet dekningsrapport, skrevet til test-output (ikke til docs/-filen — den oppdateres manuelt
    /// basert på dette). Svarer direkte på "se om det er mulig å gjennomføre det": ja, delvis, med et
    /// presist, begrunnet kart over hvor grensen går i dag.
    /// </summary>
    [Fact]
    public async Task Dekningsrapport_for_rundskriv_v4_skrives_til_test_output()
    {
        var juristId = await HentJuristIdAsync();
        var tjenesteId = await HentTjenesteIdAsync(juristId);
        var veiledning = await _client.GetFromJsonAsync<VeiledningDto>($"/api/tjenester/{tjenesteId}/veiledning", JsonInnstillinger);
        var heleTreet = SamleAllTekst(veiledning!.Rot);

        var rader = new[]
        {
            ("§2 Saksgang (oversikt)", FinnNode(veiledning.Rot, "Vandelsvilkår") is not null ? "Delvis (3 av 6 spørsmål strukturert)" : "Nei"),
            ("§3 Habilitet", "Nei — passer ikke i Vilkår/Regelnode-ontologien (evaluerer saksbehandler, ikke søker)"),
            ("§4 Formalia", "Nei — ingen søknad-komplett-vilkår modellert"),
            ("§5 Serveringsbevilling", "Nei — ingen egen vilkår-node"),
            ("§6 Vandelsvurdering", "Delvis — vilkåret finnes strukturert, avslagsgrunner krever manuell VilkarstreKommentar"),
            ("§7 Kvalifikasjonskrav", "Delvis — aldersgrense strukturert, >1000-gjester-terskel og kunnskapsprøve-unntak er ikke"),
            ("§8 Kommunal skjønnsvurdering", "Delvis — kun klokkeslett er DatasettVerdi, resten av tabellen er ikke"),
            ("§9 Vilkår i vedtaket (Gyldighet/Prikkbelastning)", "Nei — Vedtaksvirkning eies av forklaringsmodell-api"),
            ("§11 Sjekkliste", "Delvis — mekanismen (ul/li) virker, konkrete punkter krever manuell kommentar"),
            ("§12 Relevante tjenester", "Nei — Tjeneste har ikke noe relatert-tjenester-felt"),
        };

        _output.WriteLine("# Dekningsrapport: skjenkebevilling-rundskriv-fasit.md (v4) vs. generert veiledning");
        _output.WriteLine("");
        _output.WriteLine("| Seksjon | Dekning i dag |");
        _output.WriteLine("|---|---|");
        foreach (var (seksjon, dekning) in rader)
        {
            _output.WriteLine($"| {seksjon} | {dekning} |");
        }
        _output.WriteLine("");
        _output.WriteLine($"(Kontrollpunkt — «Ansvarlig vertskap» funnet i treet: {heleTreet.Contains("Ansvarlig vertskap", StringComparison.OrdinalIgnoreCase)})");
    }
}
