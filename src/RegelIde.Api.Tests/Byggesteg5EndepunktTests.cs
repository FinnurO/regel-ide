using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using RegelIde.Api;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// Integrasjonstester for byggesteg 5 runde 1 («Identifiser tjenester»/«Identifiser begrep», stub-KI,
/// docs/06-veikart.md) — kjører mot ekte embedded Postgres inkl. Program.cs' egen oppstartsseeding
/// (alkoholloven er allerede importert som rettskilde før noen test kjører).
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class Byggesteg5EndepunktTests
{
    private readonly EmbeddedPostgresApiFixture _fixture;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Byggesteg5EndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    private async Task<BrukerDto> HentTestbrukerAsync()
    {
        var brukere = await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", JsonInnstillinger);
        return brukere!.Single(b => b.Rolle == "Jurist");
    }

    private async Task<Guid> HentAlkohollovenIdAsync()
    {
        var rettskilder = await _client.GetFromJsonAsync<List<RettskildeSammendrag>>("/api/rettskilder", JsonInnstillinger);
        return rettskilder!.First(r => r.Tittel.Contains("alkohol", StringComparison.OrdinalIgnoreCase)).Id;
    }

    private static HttpRequestMessage MedBruker(HttpMethod metode, string url, Guid brukerId, object? body = null)
    {
        var request = new HttpRequestMessage(metode, url) { Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString() } } };
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    /// <summary>Minimal, gyldig PDF bygget for hånd — se RegelIde.Data.Tests/TestFilFixtures.cs for samme mønster/begrunnelse.</summary>
    private static byte[] LagTestPdf(string? tekst)
    {
        var innholdStream = tekst is null ? "" : $"BT /F1 12 Tf 10 100 Td ({tekst}) Tj ET";
        var objekter = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 4 0 R >> >> /MediaBox [0 0 300 300] /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {innholdStream.Length} >>\nstream\n{innholdStream}\nendstream",
        };
        var sb = new System.Text.StringBuilder("%PDF-1.4\n");
        var offsets = new int[objekter.Length];
        for (var i = 0; i < objekter.Length; i++)
        {
            offsets[i] = sb.Length;
            sb.Append($"{i + 1} 0 obj\n{objekter[i]}\nendobj\n");
        }
        var xrefStart = sb.Length;
        sb.Append($"xref\n0 {objekter.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) sb.Append($"{offset:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {objekter.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefStart}\n%%EOF");
        return System.Text.Encoding.ASCII.GetBytes(sb.ToString());
    }

    private const string TestPdfTekst =
        "Dette er en ekte tekst-PDF for testing av opplasting til kunnskapsbiblioteket via API-et. " +
        "Teksten er bevisst gjort lang nok til å passere terskelen for hva som regnes som et tekstlag.";

    private static HttpRequestMessage MedFilOpplasting(string url, Guid brukerId, string filnavn, byte[] innhold, string? tittel = null)
    {
        var innholdContent = new ByteArrayContent(innhold);
        innholdContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        var multipart = new MultipartFormDataContent { { innholdContent, "fil", filnavn } };
        if (tittel is not null) multipart.Add(new StringContent(tittel), "tittel");
        return new HttpRequestMessage(HttpMethod.Post, url)
        {
            Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString() } },
            Content = multipart,
        };
    }

    // ---------- Kunnskapsbibliotek-lenker ----------

    [Fact]
    public async Task Legger_til_lister_og_sletter_kunnskapsbibliotek_lenke()
    {
        var bruker = await HentTestbrukerAsync();

        var opprettSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/kunnskapsbibliotek/lenker", bruker.Id,
            new LeggTilLenkeRequest("https://testkommunen.no/tjenester", "Om tjenestetilbudet")));
        Assert.Equal(HttpStatusCode.Created, opprettSvar.StatusCode);
        var lenke = await opprettSvar.Content.ReadFromJsonAsync<KunnskapsbibliotekLenkeDto>(JsonInnstillinger);

        var listeSvar = await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/kunnskapsbibliotek/lenker", bruker.Id));
        var liste = await listeSvar.Content.ReadFromJsonAsync<List<KunnskapsbibliotekLenkeDto>>(JsonInnstillinger);
        Assert.Contains(liste!, l => l.Id == lenke!.Id);

        var slettSvar = await _client.SendAsync(MedBruker(HttpMethod.Delete, $"/api/kunnskapsbibliotek/lenker/{lenke!.Id}", bruker.Id));
        Assert.Equal(HttpStatusCode.NoContent, slettSvar.StatusCode);
    }

    [Fact]
    public async Task Ugyldig_url_gir_400()
    {
        var bruker = await HentTestbrukerAsync();
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/kunnskapsbibliotek/lenker", bruker.Id,
            new LeggTilLenkeRequest("ikke-en-url", null)));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    // ---------- Kunnskapsbibliotek-filer ----------

    [Fact]
    public async Task Laster_opp_lister_og_sletter_kunnskapsbibliotek_fil()
    {
        var bruker = await HentTestbrukerAsync();

        var opprettSvar = await _client.SendAsync(MedFilOpplasting(
            "/api/kunnskapsbibliotek/filer", bruker.Id, "skjema.pdf", LagTestPdf(TestPdfTekst)));
        Assert.Equal(HttpStatusCode.Created, opprettSvar.StatusCode);
        var fil = await opprettSvar.Content.ReadFromJsonAsync<KunnskapsbibliotekFilDto>(JsonInnstillinger);
        Assert.Equal("pdf", fil!.Filtype);
        Assert.Contains("ekte tekst-PDF", fil.UtvunnetTekst);

        var listeSvar = await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/kunnskapsbibliotek/filer", bruker.Id));
        var liste = await listeSvar.Content.ReadFromJsonAsync<List<KunnskapsbibliotekFilDto>>(JsonInnstillinger);
        Assert.Contains(liste!, f => f.Id == fil.Id);

        var slettSvar = await _client.SendAsync(MedBruker(HttpMethod.Delete, $"/api/kunnskapsbibliotek/filer/{fil.Id}", bruker.Id));
        Assert.Equal(HttpStatusCode.NoContent, slettSvar.StatusCode);
    }

    [Fact]
    public async Task Fil_med_tittel_lagrer_og_returnerer_tittelen()
    {
        var bruker = await HentTestbrukerAsync();

        var opprettSvar = await _client.SendAsync(MedFilOpplasting(
            "/api/kunnskapsbibliotek/filer", bruker.Id, "skjema.pdf", LagTestPdf(TestPdfTekst), "Søknadsskjema (test)"));
        Assert.Equal(HttpStatusCode.Created, opprettSvar.StatusCode);
        var fil = await opprettSvar.Content.ReadFromJsonAsync<KunnskapsbibliotekFilDto>(JsonInnstillinger);
        Assert.Equal("Søknadsskjema (test)", fil!.Tittel);

        var listeSvar = await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/kunnskapsbibliotek/filer", bruker.Id));
        var liste = await listeSvar.Content.ReadFromJsonAsync<List<KunnskapsbibliotekFilDto>>(JsonInnstillinger);
        Assert.Contains(liste!, f => f.Id == fil.Id && f.Tittel == "Søknadsskjema (test)");
    }

    [Fact]
    public async Task Skannet_pdf_uten_tekstlag_gir_400()
    {
        var bruker = await HentTestbrukerAsync();
        var svar = await _client.SendAsync(MedFilOpplasting(
            "/api/kunnskapsbibliotek/filer", bruker.Id, "skann.pdf", LagTestPdf(tekst: null)));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Kunnskapsbibliotek_filer_er_isolert_per_virksomhet()
    {
        var bruker = await HentTestbrukerAsync();
        await _client.SendAsync(MedFilOpplasting("/api/kunnskapsbibliotek/filer", bruker.Id, "skjema.pdf", LagTestPdf(TestPdfTekst)));

        Guid annenBrukerId;
        await using (var db = _fixture.NyDbContext())
        {
            var annenVirksomhetId = Guid.NewGuid();
            db.Virksomheter.Add(new Virksomhet { Id = annenVirksomhetId, Navn = "Nok en annen kommune (byggesteg 5-test)" });
            annenBrukerId = Guid.NewGuid();
            db.Brukere.Add(new Bruker { Id = annenBrukerId, Navn = "Uvedkommende", VirksomhetId = annenVirksomhetId, Rolle = "Testrolle" });
            await db.SaveChangesAsync();
        }

        var annenListeSvar = await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/kunnskapsbibliotek/filer", annenBrukerId));
        var annenListe = await annenListeSvar.Content.ReadFromJsonAsync<List<KunnskapsbibliotekFilDto>>(JsonInnstillinger);
        Assert.Empty(annenListe!);
    }

    // ---------- «Identifiser begrep» ----------

    [Fact]
    public async Task Kjorer_begrepsforslag_og_finner_det_i_koen()
    {
        var bruker = await HentTestbrukerAsync();
        var rettskildeId = await HentAlkohollovenIdAsync();

        var kjorSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/begreper/forslag/kjor", bruker.Id,
            new KjorForslagRequest([rettskildeId])));
        Assert.Equal(HttpStatusCode.OK, kjorSvar.StatusCode);
        var respons = await kjorSvar.Content.ReadFromJsonAsync<KjorForslagResponsDto<BegrepDto>>(JsonInnstillinger);
        var opprettede = respons!.Forslag;
        Assert.Single(opprettede);
        Assert.Equal("foreslatt_av_ai", opprettede[0].Status);
        Assert.Null(respons.Melding);

        var koSvar = await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/begreper/forslag", bruker.Id));
        var ko = await koSvar.Content.ReadFromJsonAsync<List<BegrepsforslagDto>>(JsonInnstillinger);
        Assert.Contains(ko!, f => f.Begrep.Id == opprettede[0].Id);
    }

    [Fact]
    public async Task Begrepsforslag_ukjent_rettskilde_gir_400()
    {
        var bruker = await HentTestbrukerAsync();
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/begreper/forslag/kjor", bruker.Id,
            new KjorForslagRequest([Guid.NewGuid()])));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Begrepsforslag_ko_er_isolert_per_virksomhet()
    {
        var bruker = await HentTestbrukerAsync();
        var rettskildeId = await HentAlkohollovenIdAsync();
        await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/begreper/forslag/kjor", bruker.Id,
            new KjorForslagRequest([rettskildeId])));

        Guid annenBrukerId;
        await using (var db = _fixture.NyDbContext())
        {
            var annenVirksomhetId = Guid.NewGuid();
            db.Virksomheter.Add(new Virksomhet { Id = annenVirksomhetId, Navn = "Annen kommune (byggesteg 5-test)" });
            annenBrukerId = Guid.NewGuid();
            db.Brukere.Add(new Bruker { Id = annenBrukerId, Navn = "Uvedkommende", VirksomhetId = annenVirksomhetId, Rolle = "Testrolle" });
            await db.SaveChangesAsync();
        }

        var annenKoSvar = await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/begreper/forslag", annenBrukerId));
        var annenKo = await annenKoSvar.Content.ReadFromJsonAsync<List<BegrepsforslagDto>>(JsonInnstillinger);
        Assert.Empty(annenKo!);
    }

    [Fact]
    public async Task Full_avvis_rediger_godkjenn_syklus_pa_begrepsforslag()
    {
        var bruker = await HentTestbrukerAsync();
        var rettskildeId = await HentAlkohollovenIdAsync();

        var kjorSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/begreper/forslag/kjor", bruker.Id,
            new KjorForslagRequest([rettskildeId])));
        var opprettede = await kjorSvar.Content.ReadFromJsonAsync<KjorForslagResponsDto<BegrepDto>>(JsonInnstillinger);
        var forslagId = opprettede!.Forslag[0].Id;

        // Avvis -> tilbake til utkast
        var avvisSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/begreper/{forslagId}/status", bruker.Id,
            new SettStatusRequest("utkast")));
        Assert.Equal(HttpStatusCode.OK, avvisSvar.StatusCode);
        var avvist = await avvisSvar.Content.ReadFromJsonAsync<BegrepDto>(JsonInnstillinger);
        Assert.Equal("utkast", avvist!.Status);

        // Kjør et nytt forslag for å teste Godkjenn-veien uavhengig av det avviste
        var kjor2 = await (await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/begreper/forslag/kjor", bruker.Id,
            new KjorForslagRequest([rettskildeId])))).Content.ReadFromJsonAsync<KjorForslagResponsDto<BegrepDto>>(JsonInnstillinger);
        var godkjennSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/begreper/{kjor2!.Forslag[0].Id}/status", bruker.Id,
            new SettStatusRequest("validert", "Ola Fagansvarlig")));
        Assert.Equal(HttpStatusCode.OK, godkjennSvar.StatusCode);
        var godkjent = await godkjennSvar.Content.ReadFromJsonAsync<BegrepDto>(JsonInnstillinger);
        Assert.Equal("validert", godkjent!.Status);

        // Godkjent forslag skal ikke lenger stå i køen
        var koSvar = await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/begreper/forslag", bruker.Id));
        var ko = await koSvar.Content.ReadFromJsonAsync<List<BegrepsforslagDto>>(JsonInnstillinger);
        Assert.DoesNotContain(ko!, f => f.Begrep.Id == godkjent.Id);
    }

    // ---------- «Identifiser tjenester» ----------

    [Fact]
    public async Task Kjorer_tjenesteforslag_med_kunnskapsbibliotek_lenke_og_finner_det_i_koen()
    {
        var bruker = await HentTestbrukerAsync();
        var rettskildeId = await HentAlkohollovenIdAsync();
        await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/kunnskapsbibliotek/lenker", bruker.Id,
            new LeggTilLenkeRequest("https://testkommunen.no/tjenester", "Om tjenestetilbudet")));

        var kjorSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/tjenester/forslag/kjor", bruker.Id,
            new KjorForslagRequest([rettskildeId])));
        Assert.Equal(HttpStatusCode.OK, kjorSvar.StatusCode);
        var respons = await kjorSvar.Content.ReadFromJsonAsync<KjorForslagResponsDto<TjenesteDto>>(JsonInnstillinger);
        var opprettede = respons!.Forslag;
        Assert.Single(opprettede);
        Assert.Equal("foreslatt_av_ai", opprettede[0].Status);
        Assert.Null(respons.Melding);

        var koSvar = await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/tjenester/forslag", bruker.Id));
        var ko = await koSvar.Content.ReadFromJsonAsync<List<TjenesteforslagDto>>(JsonInnstillinger);
        Assert.Contains(ko!, f => f.Tjeneste.Id == opprettede[0].Id);
    }

    [Fact]
    public async Task Tjenesteforslag_ko_er_isolert_per_virksomhet()
    {
        var bruker = await HentTestbrukerAsync();
        var rettskildeId = await HentAlkohollovenIdAsync();
        await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/tjenester/forslag/kjor", bruker.Id,
            new KjorForslagRequest([rettskildeId])));

        Guid annenBrukerId;
        await using (var db = _fixture.NyDbContext())
        {
            var annenVirksomhetId = Guid.NewGuid();
            db.Virksomheter.Add(new Virksomhet { Id = annenVirksomhetId, Navn = "Enda en annen kommune (byggesteg 5-test)" });
            annenBrukerId = Guid.NewGuid();
            db.Brukere.Add(new Bruker { Id = annenBrukerId, Navn = "Uvedkommende", VirksomhetId = annenVirksomhetId, Rolle = "Testrolle" });
            await db.SaveChangesAsync();
        }

        var annenKoSvar = await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/tjenester/forslag", annenBrukerId));
        var annenKo = await annenKoSvar.Content.ReadFromJsonAsync<List<TjenesteforslagDto>>(JsonInnstillinger);
        Assert.Empty(annenKo!);
    }

    [Fact]
    public async Task Full_avvis_rediger_godkjenn_syklus_pa_tjenesteforslag()
    {
        var bruker = await HentTestbrukerAsync();
        var rettskildeId = await HentAlkohollovenIdAsync();

        var opprettede = await (await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/tjenester/forslag/kjor", bruker.Id,
            new KjorForslagRequest([rettskildeId])))).Content.ReadFromJsonAsync<KjorForslagResponsDto<TjenesteDto>>(JsonInnstillinger);
        var forslagId = opprettede!.Forslag[0].Id;

        var redigerSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/tjenester/{forslagId}/status", bruker.Id,
            new SettStatusRequest("under_revisjon")));
        Assert.Equal(HttpStatusCode.OK, redigerSvar.StatusCode);
        var redigert = await redigerSvar.Content.ReadFromJsonAsync<TjenesteDto>(JsonInnstillinger);
        Assert.Equal("under_revisjon", redigert!.Status);

        var godkjennSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/tjenester/{forslagId}/status", bruker.Id,
            new SettStatusRequest("validert", "Ola Fagansvarlig")));
        var godkjent = await godkjennSvar.Content.ReadFromJsonAsync<TjenesteDto>(JsonInnstillinger);
        Assert.Equal("validert", godkjent!.Status);

        var koSvar = await _client.SendAsync(MedBruker(HttpMethod.Get, "/api/tjenester/forslag", bruker.Id));
        var ko = await koSvar.Content.ReadFromJsonAsync<List<TjenesteforslagDto>>(JsonInnstillinger);
        Assert.DoesNotContain(ko!, f => f.Tjeneste.Id == godkjent.Id);
    }

    // ---------- Omfang (handlingsforslag-ki-omfang-runden) ----------

    [Fact]
    public async Task Omfang_full_oppretter_tjeneste_og_handlinger_i_samme_kall()
    {
        var bruker = await HentTestbrukerAsync();
        var rettskildeId = await HentAlkohollovenIdAsync();

        var kjorSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/tjenester/forslag/kjor", bruker.Id,
            new KjorForslagRequest([rettskildeId], Omfang: "full")));
        Assert.Equal(HttpStatusCode.OK, kjorSvar.StatusCode);
        var respons = await kjorSvar.Content.ReadFromJsonAsync<KjorForslagResponsDto<TjenesteMedHandlingerDto>>(JsonInnstillinger);
        var opprettede = respons!.Forslag;
        Assert.Single(opprettede);
        Assert.Equal("foreslatt_av_ai", opprettede[0].Tjeneste.Status);
        // Stub-KI-en (KiAgentKlientStub) gir ett fast handlingsforslag under stub-tjenesten — se
        // IKiAgentKlient.cs sitt FullSvar-felt.
        Assert.Single(opprettede[0].Handlinger);
        Assert.Equal("foreslatt_av_ai", opprettede[0].Handlinger[0].Status);
        Assert.Equal(opprettede[0].Tjeneste.Id, opprettede[0].Handlinger[0].TjenesteId);

        // Handlingen skal også dukke opp i den vanlige handlinger-listen for tjenesten.
        var handlingerSvar = await _client.SendAsync(MedBruker(HttpMethod.Get, $"/api/tjenester/{opprettede[0].Tjeneste.Id}/handlinger", bruker.Id));
        var handlinger = await handlingerSvar.Content.ReadFromJsonAsync<List<HandlingDto>>(JsonInnstillinger);
        Assert.Contains(handlinger!, h => h.Id == opprettede[0].Handlinger[0].Id);
    }

    [Fact]
    public async Task Omfang_handling_pa_tjeneste_endepunktet_gir_400_henviser_til_eget_endepunkt()
    {
        var bruker = await HentTestbrukerAsync();
        var rettskildeId = await HentAlkohollovenIdAsync();

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/tjenester/forslag/kjor", bruker.Id,
            new KjorForslagRequest([rettskildeId], Omfang: "handling")));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Ukjent_omfang_gir_400()
    {
        var bruker = await HentTestbrukerAsync();
        var rettskildeId = await HentAlkohollovenIdAsync();

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/tjenester/forslag/kjor", bruker.Id,
            new KjorForslagRequest([rettskildeId], Omfang: "noe-ukjent")));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }

    [Fact]
    public async Task Kjorer_handlingsforslag_for_eksisterende_tjeneste_og_finner_dem_i_tjenestens_handlingsliste()
    {
        var bruker = await HentTestbrukerAsync();
        var rettskildeId = await HentAlkohollovenIdAsync();

        var tjenesteSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/tjenester", bruker.Id,
            new TjenesteRequest("Oppgaveregisteret — Testkommunen (API-test)", null, null, null, null, null, null, null, null, null, null, null)));
        Assert.Equal(HttpStatusCode.Created, tjenesteSvar.StatusCode);
        var tjeneste = await tjenesteSvar.Content.ReadFromJsonAsync<TjenesteDto>(JsonInnstillinger);

        var kjorSvar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/tjenester/{tjeneste!.Id}/handlinger/forslag/kjor", bruker.Id,
            new KjorHandlingsforslagRequest([rettskildeId])));
        Assert.Equal(HttpStatusCode.OK, kjorSvar.StatusCode);
        var respons = await kjorSvar.Content.ReadFromJsonAsync<KjorForslagResponsDto<HandlingDto>>(JsonInnstillinger);
        var opprettede = respons!.Forslag;
        Assert.Single(opprettede);
        Assert.Equal("foreslatt_av_ai", opprettede[0].Status);
        Assert.Equal(tjeneste.Id, opprettede[0].TjenesteId);

        var handlingerSvar = await _client.SendAsync(MedBruker(HttpMethod.Get, $"/api/tjenester/{tjeneste.Id}/handlinger", bruker.Id));
        var handlinger = await handlingerSvar.Content.ReadFromJsonAsync<List<HandlingDto>>(JsonInnstillinger);
        Assert.Contains(handlinger!, h => h.Id == opprettede[0].Id);
    }

    [Fact]
    public async Task Handlingsforslag_for_ukjent_tjeneste_gir_400()
    {
        var bruker = await HentTestbrukerAsync();
        var rettskildeId = await HentAlkohollovenIdAsync();

        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/tjenester/{Guid.NewGuid()}/handlinger/forslag/kjor", bruker.Id,
            new KjorHandlingsforslagRequest([rettskildeId])));
        Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
    }
}
