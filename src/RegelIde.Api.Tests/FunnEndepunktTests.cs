using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// [Ny, issue #429 «Funn til vurdering», 2026-10-30] Endepunktene mot hele API-et. Spørsmålene (akseptansekriteriene): kan andre enn de
/// publiseringsansvarlige godkjenne for publisering? Inneholder eksporten noe som ikke er godkjent, eller personnavn? Hva svarer API-et
/// når premissporten stopper en godkjenning?
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class FunnEndepunktTests
{
    private readonly HttpClient _client;
    private readonly EmbeddedPostgresApiFixture _fixture;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public FunnEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    private async Task<Guid> BrukerAsync(string rolle) =>
        (await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", Json))!.First(b => b.Rolle == rolle).Id;

    /// <summary>Brukeren med id-en fiksturen har satt som publiseringsansvarlig — opprettes én gang i den delte testbasen.</summary>
    private async Task<Guid> PubliseringsansvarligAsync()
    {
        await using var db = _fixture.NyDbContext();
        var id = EmbeddedPostgresApiFixture.PubliseringsansvarligTestId;
        if (!await db.Brukere.AnyAsync(b => b.Id == id))
        {
            var virksomhet = await db.Brukere.Select(b => b.VirksomhetId).FirstAsync();
            db.Brukere.Add(new Bruker { Id = id, Navn = "Publiseringsansvarlig Test", VirksomhetId = virksomhet, Rolle = "Fagansvarlig" });
            await db.SaveChangesAsync();
        }
        return id;
    }

    private static HttpRequestMessage Med(HttpMethod metode, string url, Guid bruker, object? body = null)
    {
        var r = new HttpRequestMessage(metode, url) { Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, bruker.ToString() } } };
        if (body is not null) r.Content = JsonContent.Create(body);
        return r;
    }

    private async Task<JsonElement> SendAsync(HttpRequestMessage r, HttpStatusCode forventet)
    {
        var svar = await _client.SendAsync(r);
        var tekst = await svar.Content.ReadAsStringAsync();
        Assert.True(svar.StatusCode == forventet, $"{(int)svar.StatusCode}: {tekst}");
        return string.IsNullOrEmpty(tekst) ? default : JsonDocument.Parse(tekst).RootElement.Clone();
    }

    private async Task<Guid> JuristvurdertFunnAsync(Guid jurist, string tittel, string kategori = "mulig_motstrid", object[]? koblinger = null)
    {
        var opprettet = await SendAsync(Med(HttpMethod.Post, "/api/funn", jurist, new
        {
            kategori, tittel, beskrivelse = "To bestemmelser gir ulikt svar.", koblinger = koblinger ?? [],
            vurderingspunkter = new[] { new { type = "bortforklaring_provd", overskrift = "Opphevelse", tekst = "ikke funnet." } },
        }), HttpStatusCode.Created);
        var id = opprettet.GetProperty("id").GetGuid();
        await SendAsync(Med(HttpMethod.Post, $"/api/funn/{id}/juristvurdering", jurist,
            new { vurdering = "Ordlyden er i motstrid.", vurdertAvType = "menneske" }), HttpStatusCode.OK);
        return id;
    }

    [Fact]
    public async Task Bare_publiseringsansvarlige_kan_godkjenne_for_publisering_og_trekke_tilbake()
    {
        var jurist = await BrukerAsync("Jurist");
        var ansvarlig = await PubliseringsansvarligAsync();
        var id = await JuristvurdertFunnAsync(jurist, $"Tilgang {Guid.NewGuid():N}");

        var nei = await SendAsync(Med(HttpMethod.Post, $"/api/funn/{id}/godkjenn-for-publisering", jurist), HttpStatusCode.Forbidden);
        Assert.Contains("publiseringsansvarlig", nei.GetProperty("feil").GetString(), StringComparison.OrdinalIgnoreCase);

        var tilgangJurist = await SendAsync(Med(HttpMethod.Get, "/api/funn/tilgang", jurist), HttpStatusCode.OK);
        Assert.False(tilgangJurist.GetProperty("kanPublisere").GetBoolean());
        Assert.True((await SendAsync(Med(HttpMethod.Get, "/api/funn/tilgang", ansvarlig), HttpStatusCode.OK)).GetProperty("kanPublisere").GetBoolean());

        var godkjent = await SendAsync(Med(HttpMethod.Post, $"/api/funn/{id}/godkjenn-for-publisering", ansvarlig), HttpStatusCode.OK);
        Assert.Equal("godkjent_for_publisering", godkjent.GetProperty("status").GetString());

        await SendAsync(Med(HttpMethod.Post, $"/api/funn/{id}/trekk-tilbake", jurist), HttpStatusCode.Forbidden);
        var tilbake = await SendAsync(Med(HttpMethod.Post, $"/api/funn/{id}/trekk-tilbake", ansvarlig), HttpStatusCode.OK);
        Assert.Equal("juristvurdert", tilbake.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Eksporten_inneholder_bare_godkjente_funn_og_ingen_personnavn()
    {
        var jurist = await BrukerAsync("Jurist");
        var ansvarlig = await PubliseringsansvarligAsync();
        var godkjentTittel = $"Godkjent {Guid.NewGuid():N}";
        var ikkeTittel = $"Ikke godkjent {Guid.NewGuid():N}";
        var godkjent = await JuristvurdertFunnAsync(jurist, godkjentTittel);
        await JuristvurdertFunnAsync(jurist, ikkeTittel);
        await SendAsync(Med(HttpMethod.Post, $"/api/funn/{godkjent}/godkjenn-for-publisering", ansvarlig), HttpStatusCode.OK);

        var svar = await _client.GetStringAsync("/api/funn/publiserbare");
        Assert.Contains(godkjentTittel, svar);
        Assert.DoesNotContain(ikkeTittel, svar);
        Assert.DoesNotContain("Kari Jurist", svar);
        Assert.DoesNotContain("Publiseringsansvarlig Test", svar);
        using var dok = JsonDocument.Parse(svar);
        Assert.All(dok.RootElement.GetProperty("funn").EnumerateArray(), f => Assert.True(f.TryGetProperty("godkjentDato", out _)));
    }

    [Fact]
    public async Task Menneskelig_juristvurdering_krever_rollen_Jurist_men_en_juristrunde_kan_fores_inn()
    {
        var saksbehandler = await BrukerAsync("Saksbehandler");
        var opprettet = await SendAsync(Med(HttpMethod.Post, "/api/funn", saksbehandler, new
        {
            kategori = "uklarhet_hull", undertype = "hull", tittel = $"Hull {Guid.NewGuid():N}", beskrivelse = "Ingen overgangsregel.",
            vurderingspunkter = new[] { new { type = "bortforklaring_provd", tekst = "ingen overgangsregel funnet." } },
        }), HttpStatusCode.Created);
        Assert.Equal("oppdaget", opprettet.GetProperty("status").GetString());
        Assert.Equal("manuell", opprettet.GetProperty("oppdagelsesKilde").GetString());
        var id = opprettet.GetProperty("id").GetGuid();

        await SendAsync(Med(HttpMethod.Post, $"/api/funn/{id}/juristvurdering", saksbehandler,
            new { vurdering = "Hull.", vurdertAvType = "menneske" }), HttpStatusCode.Forbidden);
        var ki = await SendAsync(Med(HttpMethod.Post, $"/api/funn/{id}/juristvurdering", saksbehandler,
            new { vurdering = "Hull.", vurdertAvType = "ki_agent" }), HttpStatusCode.OK);
        Assert.Equal("ki_agent", ki.GetProperty("vurdertAvType").GetString());
        Assert.StartsWith("jurist-utfordrer (KI-agent)", ki.GetProperty("juristvurdertAv").GetString());
        // Å forklare bort krever Jurist eller publiseringstilgang.
        await SendAsync(Med(HttpMethod.Post, $"/api/funn/{id}/forklar-bort", saksbehandler, new { begrunnelse = "x" }), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Premissporten_svarer_409_med_mangelen()
    {
        var jurist = await BrukerAsync("Jurist");
        var ansvarlig = await PubliseringsansvarligAsync();
        Guid navneformId;
        await using (var db = _fixture.NyDbContext())
        {
            var aktor = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Nedlagt departement {Guid.NewGuid():N}", Livsstatus = "nedlagt", Aktiv = false };
            db.Virksomheter.Add(aktor);
            var nf = new BegrepEntitet
            {
                Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = aktor.Id, Navneformgrunn = "gjeldende",
                Term = aktor.Navn, Status = "publisert", OpprettetAv = "test", Godkjenning = Strukturkanter.MaskineltForeslatt,
            };
            db.Begreper.Add(nf);
            await db.SaveChangesAsync();
            navneformId = nf.Id;
        }
        var id = await JuristvurdertFunnAsync(jurist, $"Utgått {Guid.NewGuid():N}", "utgatt_aktor",
            [new { rolle = "navneform", etikett = "Nedlagt departement", begrepId = navneformId }]);

        var detalj = await SendAsync(Med(HttpMethod.Get, $"/api/funn/{id}", jurist), HttpStatusCode.OK);
        Assert.Contains("ikke faglig vurdert", detalj.GetProperty("premissmangler")[0].GetString());
        var konflikt = await SendAsync(Med(HttpMethod.Post, $"/api/funn/{id}/godkjenn-for-publisering", ansvarlig), HttpStatusCode.Conflict);
        Assert.Contains("Premissporten", konflikt.GetProperty("feil").GetString());
    }

    [Fact]
    public async Task Maskinregelen_torrkjorer_som_standard_skriver_med_skriv_true_og_er_idempotent()
    {
        // [Ny, #429 PR 2] Endepunktet: tørrkjøring uten ?skriv=true, telling per gruppe og regel, og ingen duplikater ved ny kjøring.
        var jurist = await BrukerAsync("Jurist");
        var navn = "Testkyst" + new string(Guid.NewGuid().ToString("N").Select(c => (char)('a' + c % 26)).Take(10).ToArray()) + "departementet";
        var rid = Guid.NewGuid();
        await using (var db = _fixture.NyDbContext())
        {
            var aktor = new Virksomhet { Id = Guid.NewGuid(), Navn = navn, Livsstatus = "nedlagt", Aktiv = false };
            db.Virksomheter.Add(aktor);
            db.Begreper.Add(new BegrepEntitet
            {
                Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = aktor.Id, Navneformgrunn = "gjeldende",
                Term = navn, Status = "publisert", OpprettetAv = "test", Godkjenning = Strukturkanter.MaskineltForeslatt,
            });
            var eli = $"https://lovdata.no/eli/forskrift/2099/02/02/{Random.Shared.Next(1_000_000, 9_999_999)}/nor";
            db.Rettskilder.Add(new RettskildeEntitet
            {
                Id = rid, Doctype = "act", Kildetype = "Forskrift", Status = "Gjeldende", Importrolle = "primaer", Tittel = $"Testforskrift {rid:N}",
                Eli = eli, AknXml = "<akomaNtoso/>", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
            db.RettskildeNoder.Add(new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = rid, Eid = $"{eli}/§2/ledd-1", KildeId = "§2/ledd-1", NodeType = "ledd",
                Tekst = $"Klage over vedtak etter § 1 går til {navn}.", TekstHash = "h",
            });
            await db.SaveChangesAsync();
        }

        var url = $"/api/funn/maskinregel/utgatt-aktor?rettskildeId={rid}";
        var torr = await SendAsync(Med(HttpMethod.Post, url, jurist), HttpStatusCode.OK);
        Assert.False(torr.GetProperty("skrevet").GetBoolean());
        Assert.Equal(1, torr.GetProperty("foreslatt").GetInt32());
        Assert.Equal(1, torr.GetProperty("foreslattPerGruppe").GetProperty("D").GetInt32());
        Assert.Equal(0, torr.GetProperty("ikkeForeslattPerRegel").GetProperty("R3").GetInt32());
        Assert.Empty((await SendAsync(Med(HttpMethod.Get, $"/api/funn?rettskildeId={rid}", jurist), HttpStatusCode.OK)).EnumerateArray());

        var skrevet = await SendAsync(Med(HttpMethod.Post, url + "&skriv=true", jurist), HttpStatusCode.OK);
        Assert.Equal(1, skrevet.GetProperty("nye").GetInt32());
        var igjen = await SendAsync(Med(HttpMethod.Post, url + "&skriv=true", jurist), HttpStatusCode.OK);
        Assert.Equal(0, igjen.GetProperty("nye").GetInt32());
        Assert.Equal(1, igjen.GetProperty("finnes").GetInt32());

        var liste = (await SendAsync(Med(HttpMethod.Get, $"/api/funn?rettskildeId={rid}", jurist), HttpStatusCode.OK)).EnumerateArray().ToList();
        var funn = Assert.Single(liste);
        Assert.Equal("oppdaget", funn.GetProperty("status").GetString());
        Assert.Equal(UtgattAktorFunnregel.OppdagelsesKilde, funn.GetProperty("oppdagelsesKilde").GetString());
        var bestemmelse = funn.GetProperty("koblinger").EnumerateArray().Single(k => k.GetProperty("rolle").GetString() == "gjelder_bestemmelse");
        Assert.Equal(navn, bestemmelse.GetProperty("quoteExact").GetString());
        Assert.Contains(funn.GetProperty("premissmangler").EnumerateArray(), m => m.GetString()!.StartsWith("Etterfølger ikke vurdert"));
    }
}
