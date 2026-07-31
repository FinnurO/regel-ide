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
/// representeres med dagens datamodell (kun §12 gjenstår som et rent modellgap — se under).
///
/// 2026-07-31, runde 2: <see cref="FasitRunde4Seed"/> (RegelIde.Data) seeder nå det som opprinnelig
/// ble opprettet via ekte, engangs-HTTP-kall mot en kjørende utviklingsinstans (5 nye Vilkår, 13 nye
/// Tjenester, ekte tekst-tagger, 10 VilkarstreKommentarer) — så testen her måler det faktiske,
/// gjentakbare innholdet i test-databasen, ikke bare den opprinnelige, magrere seed-baselinen. Se
/// docs/13-backlog.md §1/§4 punkt 1 for hvorfor dette var nødvendig: uten seeden var testen
/// permanent, unødvendig pessimistisk om §3/§4/§5/§8/§9, uavhengig av hvor mye som faktisk var bygget
/// "for hånd" i en annen database.
///
/// To seksjoner (§6/§11) demonstreres i tillegg via den EKTE forfatter-mekanismen (POST
/// /api/vilkarstre-kommentarer) i Arrange-delen av sine respektive tester, ikke en test-only bypass —
/// det er nøyaktig den samme HTTP-veien en jurist ville brukt fra Egenskapspanelets "Veiledning"-fane.
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
        // Alle seks har nå en strukturert Vilkår-node i treet (FasitRunde4Seed, 2026-07-31 runde 2) —
        // se den egne testen under for de nyansene som fortsatt IKKE er dekket (>1000-gjester-terskel,
        // parametertabellen i §8, Vedtaksvirkning i §9).
        Assert.NotNull(FinnNode(veiledning!.Rot, "Vandelsvilkår"));
        Assert.NotNull(FinnNode(veiledning.Rot, "Aldersvilkår"));
        Assert.NotNull(FinnNode(veiledning.Rot, "Klokkeslettsvilkår"));
        Assert.NotNull(FinnNode(veiledning.Rot, "Habilitet"));
        Assert.NotNull(FinnNode(veiledning.Rot, "Formalia"));
        Assert.NotNull(FinnNode(veiledning.Rot, "Serveringsbevillingsvilkår"));
        Assert.NotNull(FinnNode(veiledning.Rot, "Kunnskapsprøve"));
        Assert.NotNull(FinnNode(veiledning.Rot, "Kommunal skjønnsvurdering"));
    }

    [Fact]
    public async Task Paragraf3_4_5_og_9_dekkes_nå_strukturert_men_12_er_fortsatt_et_rent_modellgap()
    {
        // Runde 1 (2026-07-31) bekreftet at §3/§4/§5/§9 IKKE var representerbare — men det var et
        // INNHOLDSGAP i seed-dataene, ikke et modellgap (docs/12-fasit-handbok-leveranse.md "Runde 4":
        // "domenemodellen tillot det hele tiden, ingen ny entitet eller migrasjon var nødvendig").
        // FasitRunde4Seed (runde 2) fyller nettopp dette innholdet. §12 er det ene gjenstående ekte
        // modellgapet: TjenesteDto har fortsatt ikke noe "relaterte tjenester"-felt (bekreftet ved
        // kodegjennomgang av Dtos.cs) — de 13 tjenestene fra FasitRunde4Seed finnes som egne,
        // frittstående Tjeneste-rader, men ingen mekanisme kobler dem til "Alminnelig skjenkebevilling"
        // (se docs/13-backlog.md §2.1 Hendelse/Tjenesteavhengighet — nettopp det som ville lukket dette).
        var juristId = await HentJuristIdAsync();
        var tjenesteId = await HentTjenesteIdAsync(juristId);
        var veiledning = await _client.GetFromJsonAsync<VeiledningDto>($"/api/tjenester/{tjenesteId}/veiledning", JsonInnstillinger);
        var heleTreet = SamleAllTekst(veiledning!.Rot);

        // §3 Habilitet (fvl. § 8) — modellert som et formelt Vilkår med GjelderRolle="saksbehandler".
        // Merk presiseringen: dette evaluerer saksbehandlerens EGEN habilitet, ikke søkerens — en bevisst
        // utvidelse av hvordan Vilkår-ontologien i praksis brukes, ikke noe INV-en i seg selv krever.
        Assert.Contains("habil", heleTreet, StringComparison.OrdinalIgnoreCase);

        // §4 Formalia (fvl. §§ 11/17) og §5 Serveringsbevilling (serveringsloven § 3) — egne Vilkår-noder.
        Assert.Contains("serveringsbevilling", heleTreet, StringComparison.OrdinalIgnoreCase);

        // §9 Faste vilkår/prikkbelastning — representert som fritekst-VilkarstreKommentar på rotnoden,
        // IKKE som et strukturert Vedtaksvirkning-felt (den eies fortsatt bevisst av forklaringsmodell-api,
        // dimensjon E). "Kan skrives ned" er ikke det samme som "kan modelleres strukturert" — se docs/13-backlog.md §2.6.
        Assert.Contains("prikkbelastning", heleTreet, StringComparison.OrdinalIgnoreCase);

        // §12 Relevante tjenester — fortsatt et rent modellgap, se XML-doc over.
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
        // Sanitizeren tillater ul/li — dette bekrefter at MEKANISMEN virker ende-til-ende via det ekte
        // endepunktet, ikke bare i en isolert sanitizer-enhetstest. FasitRunde4Seed har allerede lagt en
        // egen, innholdsriktig sjekkliste (§6 avslagsgrunner) på samme Vilkår — denne testen legger til
        // en TREDJE, egen sjekkliste-kommentar og verifiserer den spesifikt (ikke via .Single(), som ville
        // feilet nå som noden har flere sjekkliste-kommentarer).
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
        var sjekklisteKommentar = FinnNode(veiledningEtter!.Rot, "Vandelsvilkår")!.Kommentarer
            .Single(k => k.Dokumenttype == "sjekkliste" && k.TekstHtml.Contains("organisasjonsnummer"));
        Assert.Contains("<ul>", sjekklisteKommentar.TekstHtml);
        Assert.Contains("Kontrollert organisasjonsnummer", sjekklisteKommentar.TekstHtml);
    }

    [Fact]
    public async Task Paragraf8_kommunal_skjonnsvurdering_har_na_bade_strukturert_vilkar_og_fritekst_parametertabell()
    {
        // §8s tabell har fem parametre (maks bevillinger, forbudte konsepter, politisk behandling,
        // kunnskapsprøve-krav, kommunale tilleggsvilkår). Klokkeslett er den eneste som er strukturert
        // som DatasettVerdi (se KommunaleParametreSeed) — resten (inkl. «Ansvarlig vertskap») er nå
        // representert som fritekst-VilkarstreKommentar på det nye Kommunal skjønnsvurdering-Vilkåret
        // (FasitRunde4Seed), IKKE som strukturerte, individuelt spørrbare felt. Dette er fortsatt en
        // ærlig delvis-dekning-test, ikke en fullstendig én-til-én-reproduksjon.
        var juristId = await HentJuristIdAsync();
        var tjenesteId = await HentTjenesteIdAsync(juristId);
        var virksomheter = await _client.GetFromJsonAsync<List<VirksomhetDto>>("/api/virksomheter", JsonInnstillinger);
        var tonsbergId = virksomheter!.Single(v => v.Navn == "Tønsberg kommune").Id;

        var veiledning = await _client.GetFromJsonAsync<VeiledningDto>(
            $"/api/tjenester/{tjenesteId}/veiledning?virksomhetId={tonsbergId}", JsonInnstillinger);
        var klokkeslettsvilkar = FinnNode(veiledning!.Rot, "Klokkeslettsvilkår");
        Assert.NotNull(klokkeslettsvilkar);
        Assert.Single(klokkeslettsvilkar!.InputDatasettVerdier);

        var kommunalSkjonnsvurdering = FinnNode(veiledning.Rot, "Kommunal skjønnsvurdering");
        Assert.NotNull(kommunalSkjonnsvurdering);
        Assert.Empty(kommunalSkjonnsvurdering!.InputDatasettVerdier); // «Ansvarlig vertskap» er fritekst, ikke et strukturert Datasett-felt

        var heleTreet = SamleAllTekst(veiledning.Rot);
        Assert.Contains("Ansvarlig vertskap", heleTreet, StringComparison.OrdinalIgnoreCase); // kommunalt tilleggsvilkår — fritekst, ikke DatasettVerdi
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
            ("§2 Saksgang (oversikt)", "Ja — alle seks spørsmål (habilitet, formalia, serveringsbevilling, vandel, kvalifikasjon, kommunalt skjønn) har nå en Vilkår-node"),
            ("§3 Habilitet", "Delvis — modellert som Vilkår med GjelderRolle=\"saksbehandler\" (fvl § 8); en bevisst utvidelse av ontologien, ikke dens opprinnelige sikte (søkeren)"),
            ("§4 Formalia", "Ja — egen Vilkår-node, juridisk grunnlag fvl §§ 11/17"),
            ("§5 Serveringsbevilling", "Ja — egen Vilkår-node, juridisk grunnlag serveringsloven § 3"),
            ("§6 Vandelsvurdering", "Delvis — vilkåret finnes strukturert, avslagsgrunner nå seedet som ekte sjekkliste-VilkarstreKommentar"),
            ("§7 Kvalifikasjonskrav", "Delvis — aldersgrense og kunnskapsprøve strukturert som egne Vilkår, >1000-gjester-unntaket er fortsatt ikke betinget modellert"),
            ("§8 Kommunal skjønnsvurdering", "Delvis — egen Vilkår-node med skjønnsmomenter og hjemmel; kun klokkeslett er DatasettVerdi, resten av parametertabellen (inkl. «Ansvarlig vertskap») er fritekst-kommentar"),
            ("§9 Vilkår i vedtaket (Gyldighet/Prikkbelastning/gebyr)", "Delvis — representert som 5 fritekst-VilkarstreKommentarer på rotnoden, IKKE som strukturerte Vedtaksvirkning-felt (dimensjon E, se docs/13-backlog.md §2.6)"),
            ("§11 Sjekkliste", "Delvis — mekanismen (ul/li) virker ende-til-ende, og §6s konkrete avslagsgrunner er nå seedet som ekte sjekkliste"),
            ("§12 Relevante tjenester", "Nei — de 13 tjenestene finnes som egne Tjeneste-rader, men Tjeneste har ikke noe relatert-tjenester-felt til å koble dem til «Alminnelig skjenkebevilling» (se docs/13-backlog.md §2.1)"),
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
