using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api.Tests;

/// <summary>
/// [Ny, issue #311 «Strukturmodell 6», 2026-10-07] Integrasjonstester for <c>/api/strukturkanter</c>, typekonfigurasjonen,
/// forslagskøen og lesefasadene nettside-eksporten bruker — mot hele API-et og ekte embedded Postgres.
/// Erstatter <c>VirksomhetRelasjonEndepunktTests</c>, <c>GruppeMedlemskapEndepunktTests</c> og
/// <c>MyndighetstildelingEndepunktTests</c> ([FJERNET, #311]); #310-testene for nodetype/aktørtype som sto i den
/// siste er flyttet hit uendret (pluss at <c>organ</c> nå avvises som nodetype).
/// <para>
/// Spørsmålene (issue #311 AC3): kan kantene leses per node og per kategori, og kan et forslag godkjennes eller
/// avvises over API-et? Og leser nettside-eksportens gamle URL-er fortsatt det samme?
/// </para>
/// </summary>
[Collection(ApiTestCollection.Navn)]
public class StrukturkantEndepunktTests
{
    private readonly HttpClient _client;
    private readonly EmbeddedPostgresApiFixture _fixture;

    private static readonly JsonSerializerOptions JsonInnstillinger = new(JsonSerializerDefaults.Web);

    public StrukturkantEndepunktTests(EmbeddedPostgresApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
    }

    private async Task<Guid> HentJuristIdAsync()
    {
        var brukere = await _client.GetFromJsonAsync<List<BrukerDto>>("/api/brukere", JsonInnstillinger);
        return brukere!.Single(b => b.Rolle == "Jurist").Id;
    }

    private static HttpRequestMessage MedBruker(HttpMethod metode, string url, Guid brukerId, object? body = null)
    {
        var request = new HttpRequestMessage(metode, url) { Headers = { { GjeldendeBrukerTjeneste.HeaderNavn, brukerId.ToString() } } };
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    private async Task<(Guid RettskildeId, string ParagrafEid)> OpprettRettskildeMedParagrafAsync()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = "Testlov " + rettskildeId, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var eid = $"https://test/{rettskildeId:N}/§1";
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = eid, KildeId = "§1", NodeType = "paragraf", Nummer = "§ 1",
        });
        await db.SaveChangesAsync();
        return (rettskildeId, eid);
    }

    private async Task<(Guid Id, string Navn)> OpprettVirksomhetAsync(string prefiks)
    {
        await using var db = _fixture.NyDbContext();
        var v = new Virksomhet { Id = Guid.NewGuid(), Navn = $"{prefiks} {Guid.NewGuid():N}" };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return (v.Id, v.Navn);
    }

    private async Task<BegrepDto> OpprettBegrepAsync(Guid brukerId, Guid lovId, string nodetype, string prefiks)
    {
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/gruppebegrep", brukerId,
            new { LovkildeId = lovId, Term = $"{prefiks}-{Guid.NewGuid():N}", Nodetype = nodetype }));
        svar.EnsureSuccessStatusCode();
        return (await svar.Content.ReadFromJsonAsync<BegrepDto>(JsonInnstillinger))!;
    }

    private async Task<HttpResponseMessage> PostKantAsync(Guid brukerId, object body) =>
        await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/strukturkanter", brukerId, body));

    // ---------------- Typekonfigurasjonen ----------------

    [Fact]
    public async Task Typekonfigurasjonen_har_alle_aatte_kategoriene_og_de_gamle_relasjonstypene_uendret()
    {
        var alle = await _client.GetFromJsonAsync<List<RelasjonsTypeKonfigurasjonDto>>("/api/konfigurasjon/relasjonstyper", JsonInnstillinger);
        Assert.Equal(Strukturkanter.Kategorier.OrderBy(k => k), alle!.Select(t => t.Kategori).Distinct().OrderBy(k => k));

        var r = await _client.GetFromJsonAsync<List<RelasjonsTypeKonfigurasjonDto>>("/api/konfigurasjon/relasjonstyper?kategori=R", JsonInnstillinger);
        Assert.All(r!, t => Assert.Equal("R", t.Kategori));
        var sekretariat = Assert.Single(r!, t => t.Kode == "sekretariat");
        Assert.Equal(("har sekretariat hos {0}", "er sekretariat for {0}"), (sekretariat.FraVisningsmal, sekretariat.TilVisningsmal));
        Assert.Contains(r!, t => t.Kode == "klageinstans_for"); // docs/33 §4.3-startsettet

        var k = await _client.GetFromJsonAsync<List<RelasjonsTypeKonfigurasjonDto>>("/api/konfigurasjon/relasjonstyper?kategori=K", JsonInnstillinger);
        Assert.Contains(k!, t => t.Kode == "forskrift");
        Assert.Contains(k!, t => t.Kode == "instruksjon"); // samme kode som i R — identiteten er (kategori, kode).
    }

    // ---------------- Opprett og les per node / per kategori ----------------

    [Fact]
    public async Task R_kant_opprettes_og_leses_fra_begge_sider_med_hver_sin_visningstekst()
    {
        var brukerId = await HentJuristIdAsync();
        var (merkenemnd, merkenemndNavn) = await OpprettVirksomhetAsync("Lokal merkenemnd");
        var (statsforvalteren, statsforvalterenNavn) = await OpprettVirksomhetAsync("Statsforvalteren");

        var svar = await PostKantAsync(brukerId, new
        {
            Kategori = "R", Typekode = "sekretariat", FraVirksomhetId = merkenemnd, TilVirksomhetId = statsforvalteren,
            KildeUtenforKorpusTekst = "docs/28-eksempel", KildeUtenforKorpusType = "nettside_annet", KildeUtenforKorpusDokumentasjon = "primaer", Polaritet = "positiv",
        });
        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);
        var kant = (await svar.Content.ReadFromJsonAsync<StrukturkantDto>(JsonInnstillinger))!;
        Assert.Equal(("validert", "manuell"), (kant.Status, kant.OppdagelsesKilde));

        var fra = await _client.GetFromJsonAsync<List<StrukturkantDto>>($"/api/strukturkanter?virksomhetId={merkenemnd}&kategori=R", JsonInnstillinger);
        var til = await _client.GetFromJsonAsync<List<StrukturkantDto>>($"/api/strukturkanter?virksomhetId={statsforvalteren}", JsonInnstillinger);
        Assert.Equal($"har sekretariat hos {statsforvalterenNavn}", Assert.Single(fra!).Visningstekst);
        Assert.Equal($"er sekretariat for {merkenemndNavn}", Assert.Single(til!).Visningstekst);
        Assert.Equal("docs/28-eksempel", til![0].KildeUtenforKorpusTekst);

        // Samme utsagn en gang til: 200 med SAMME kant (idempotent), ikke en dublett.
        var igjen = await PostKantAsync(brukerId, new
        {
            Kategori = "R", Typekode = "sekretariat", FraVirksomhetId = merkenemnd, TilVirksomhetId = statsforvalteren,
            KildeUtenforKorpusTekst = "docs/28-eksempel", KildeUtenforKorpusType = "nettside_annet", KildeUtenforKorpusDokumentasjon = "primaer", Polaritet = "positiv",
        });
        Assert.Equal(HttpStatusCode.OK, igjen.StatusCode);
        Assert.Equal(kant.Id, (await igjen.Content.ReadFromJsonAsync<StrukturkantDto>(JsonInnstillinger))!.Id);

        // Kategorifilteret: ingen M-kanter for denne noden.
        Assert.Empty((await _client.GetFromJsonAsync<List<StrukturkantDto>>($"/api/strukturkanter?virksomhetId={merkenemnd}&kategori=M", JsonInnstillinger))!);
    }

    [Fact]
    public async Task K_kant_uten_tilnode_med_negativ_polaritet_og_avgrensning()
    {
        var brukerId = await HentJuristIdAsync();
        var (lovId, paragrafEid) = await OpprettRettskildeMedParagrafAsync();
        var (dep, _) = await OpprettVirksomhetAsync("Departementet");

        var svar = await PostKantAsync(brukerId, new
        {
            Kategori = "K", Typekode = "instruksjon", FraVirksomhetId = dep, HjemmelRettskildeId = lovId, HjemmelEid = paragrafEid,
            Paragrafspenn = new[] { new { FraEid = paragrafEid, TilEid = (string?)null } },
            AvgrensningTekst = "enkeltsaker", Objekt = "klagenemndas avgjørelser", Polaritet = "negativ",
        });
        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);
        var kant = (await svar.Content.ReadFromJsonAsync<StrukturkantDto>(JsonInnstillinger))!;
        Assert.Null(kant.Til);
        Assert.Equal(("negativ", "enkeltsaker"), (kant.Polaritet, kant.AvgrensningTekst));
        Assert.Equal(paragrafEid, Assert.Single(kant.Paragrafspenn).FraEid);

        // Fra lovens side (hjemmel): kanten er med.
        var forLoven = await _client.GetFromJsonAsync<List<StrukturkantDto>>($"/api/rettskilder/{lovId}/strukturkanter", JsonInnstillinger);
        Assert.Single(forLoven!, x => x.Id == kant.Id);
    }

    [Fact]
    public async Task Ugyldige_kanter_gir_400_ingen_gjettet_fallback()
    {
        var brukerId = await HentJuristIdAsync();
        var (lovId, paragrafEid) = await OpprettRettskildeMedParagrafAsync();
        var (a, _) = await OpprettVirksomhetAsync("A");
        var (b, _) = await OpprettVirksomhetAsync("B");

        // Polaritet må oppgis.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostKantAsync(brukerId, new
        {
            Kategori = "R", Typekode = "underlagt", FraVirksomhetId = a, TilVirksomhetId = b, HjemmelRettskildeId = lovId,
        })).StatusCode);
        // Ukjent typekode.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostKantAsync(brukerId, new
        {
            Kategori = "R", Typekode = "ukjent_type_finnes_ikke", FraVirksomhetId = a, TilVirksomhetId = b, HjemmelRettskildeId = lovId, Polaritet = "positiv",
        })).StatusCode);
        // Verken hjemmel eller kilde utenfor korpus.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostKantAsync(brukerId, new
        {
            Kategori = "R", Typekode = "underlagt", FraVirksomhetId = a, TilVirksomhetId = b, Polaritet = "positiv",
        })).StatusCode);
        // Ikke-eksisterende node.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostKantAsync(brukerId, new
        {
            Kategori = "R", Typekode = "underlagt", FraVirksomhetId = a, TilVirksomhetId = Guid.NewGuid(), HjemmelRettskildeId = lovId, Polaritet = "positiv",
        })).StatusCode);
        // Feil nodetype: M krever klasse/område som mål — en virksomhet er ikke det.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostKantAsync(brukerId, new
        {
            Kategori = "M", Typekode = "medlem_av", FraVirksomhetId = a, TilVirksomhetId = b, HjemmelRettskildeId = lovId,
            Paragrafspenn = new[] { new { FraEid = paragrafEid, TilEid = (string?)null } }, Polaritet = "positiv",
        })).StatusCode);
        // Uten node: bare forslagskøen er lov.
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/strukturkanter")).StatusCode);
    }

    // ---------------- Forslag: godkjenn / avvis / slett ----------------

    private async Task<Guid> OpprettForslagAsync(Guid fra, Guid til, string typekode)
    {
        await using var db = _fixture.NyDbContext();
        var kant = new StrukturkantEntitet
        {
            Id = Guid.NewGuid(), Kategori = "R", Typekode = typekode, FraVirksomhetId = fra, TilVirksomhetId = til,
            KildeUtenforKorpusTekst = "KI-forslag", KildeUtenforKorpusType = Strukturkanter.NettsideAnnet, KildeUtenforKorpusDokumentasjon = Strukturkanter.Sekundaer,
            Status = "foreslatt_av_ai", OppdagelsesKilde = "ki:test",
            OpprettetAv = "system-ki", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Strukturkanter.Add(kant);
        await db.SaveChangesAsync();
        return kant.Id;
    }

    [Fact]
    public async Task Forslag_vises_i_koen_og_kan_godkjennes_avvises_og_slettes()
    {
        var brukerId = await HentJuristIdAsync();
        var (a, _) = await OpprettVirksomhetAsync("Forslag A");
        var (b, _) = await OpprettVirksomhetAsync("Forslag B");
        var godkjennes = await OpprettForslagAsync(a, b, "rapporterer_til");
        var avvises = await OpprettForslagAsync(a, b, "radgir");

        var ko = await _client.GetFromJsonAsync<List<KiForslagKoRadDto>>("/api/ki-oppdagelse/ko", JsonInnstillinger);
        Assert.Contains(ko!, r => r.Id == godkjennes && r.Type == "R" && r.AiForslagVersjon == "ki:test");
        var viaListe = await _client.GetFromJsonAsync<List<StrukturkantDto>>("/api/strukturkanter?status=foreslatt_av_ai&kategori=R", JsonInnstillinger);
        Assert.Contains(viaListe!, r => r.Id == avvises);

        var godkjent = await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/strukturkanter/{godkjennes}/godkjenn", brukerId));
        Assert.Equal(HttpStatusCode.OK, godkjent.StatusCode);
        Assert.Equal("validert", (await godkjent.Content.ReadFromJsonAsync<StrukturkantDto>(JsonInnstillinger))!.Status);
        // En validert kant kan ikke godkjennes igjen eller avvises — den slettes.
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/strukturkanter/{godkjennes}/godkjenn", brukerId))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/strukturkanter/{godkjennes}/avvis", brukerId))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/strukturkanter/{avvises}/avvis", brukerId))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(MedBruker(HttpMethod.Delete, $"/api/strukturkanter/{godkjennes}", brukerId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/strukturkanter/{godkjennes}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(MedBruker(HttpMethod.Post, $"/api/strukturkanter/{Guid.NewGuid()}/godkjenn", brukerId))).StatusCode);
    }

    // ---------------- [Ny, Johanns beslutning 2026-10-07] Arbeidslista: bare dokumentert på nettside ----------------

    [Fact]
    public async Task Uten_korpusforankring_lister_nettside_annet_og_filtrerer_paa_kildetype()
    {
        var brukerId = await HentJuristIdAsync();
        var (a, _) = await OpprettVirksomhetAsync("Org-kart A");
        var (b, _) = await OpprettVirksomhetAsync("Org-kart B");
        var nettside = await PostKantAsync(brukerId, new
        {
            Kategori = "R", Typekode = "sekretariat_for", FraVirksomhetId = a, TilVirksomhetId = b, Polaritet = "positiv",
            KildeUtenforKorpusTekst = "organisasjonskartet", KildeUtenforKorpusType = "nettside_annet", KildeUtenforKorpusDokumentasjon = "primaer",
        });
        Assert.Equal(HttpStatusCode.Created, nettside.StatusCode);
        var nettsideId = (await nettside.Content.ReadFromJsonAsync<StrukturkantDto>(JsonInnstillinger))!.Id;
        var vedtekter = await PostKantAsync(brukerId, new
        {
            Kategori = "R", Typekode = "eies_av", FraVirksomhetId = a, TilVirksomhetId = b, Polaritet = "positiv",
            KildeUtenforKorpusTekst = "vedtektene § 2", KildeUtenforKorpusType = "vedtekter", KildeUtenforKorpusDokumentasjon = "primaer",
        });
        var vedtekterId = (await vedtekter.Content.ReadFromJsonAsync<StrukturkantDto>(JsonInnstillinger))!.Id;
        // Uten type ⇒ 400: typen gjettes ikke.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostKantAsync(brukerId, new
        {
            Kategori = "R", Typekode = "radgir", FraVirksomhetId = a, TilVirksomhetId = b, Polaritet = "positiv",
            KildeUtenforKorpusTekst = "nettsiden",
        })).StatusCode);

        var standard = await _client.GetFromJsonAsync<List<StrukturkantDto>>("/api/strukturkanter/uten-korpusforankring", JsonInnstillinger);
        Assert.Contains(standard!, k => k.Id == nettsideId && k.KildeUtenforKorpusType == "nettside_annet"
            && k.KildeUtenforKorpusDokumentasjon == "primaer");
        Assert.DoesNotContain(standard!, k => k.Id == vedtekterId);
        var bareVedtekter = await _client.GetFromJsonAsync<List<StrukturkantDto>>("/api/strukturkanter/uten-korpusforankring?kildetype=vedtekter", JsonInnstillinger);
        Assert.All(bareVedtekter!, k => Assert.Equal("vedtekter", k.KildeUtenforKorpusType));
        Assert.Contains(bareVedtekter!, k => k.Id == vedtekterId);
        var alle = await _client.GetFromJsonAsync<List<StrukturkantDto>>("/api/strukturkanter/uten-korpusforankring?kildetype=alle", JsonInnstillinger);
        Assert.Contains(alle!, k => k.Id == nettsideId);
        Assert.Contains(alle!, k => k.Id == vedtekterId);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/strukturkanter/uten-korpusforankring?kildetype=blogg")).StatusCode);
    }

    // ---------------- Lesefasadene for nettside-eksporten ----------------

    [Fact]
    public async Task Lesefasadene_viser_M_og_I_kanter_i_den_gamle_formen()
    {
        var brukerId = await HentJuristIdAsync();
        var (lovId, paragrafEid) = await OpprettRettskildeMedParagrafAsync();
        var (hjemmelId, hjemmelEid) = await OpprettRettskildeMedParagrafAsync();
        var (virksomhet, _) = await OpprettVirksomhetAsync("Vertskommune");
        var klasse = await OpprettBegrepAsync(brukerId, lovId, "klasse", "vertskommuner");
        var omrade = await OpprettBegrepAsync(brukerId, lovId, "omrade", "forvaltningsomradet");
        var rolle = await OpprettBegrepAsync(brukerId, lovId, "rolle", "forurensningsmyndighet");
        var spenn = new[] { new { FraEid = hjemmelEid, TilEid = (string?)null } };

        var utlopt = await PostKantAsync(brukerId, new
        {
            Kategori = "M", Typekode = "medlem_av", FraVirksomhetId = virksomhet, TilBegrepId = klasse.Id, HjemmelRettskildeId = hjemmelId,
            Paragrafspenn = spenn, AvgrensningTekst = "fengsel", Polaritet = "positiv",
            GyldigFra = new DateOnly(2020, 1, 1), GyldigTil = new DateOnly(2021, 12, 31),
        });
        Assert.Equal(HttpStatusCode.Created, utlopt.StatusCode);
        var innehar = await PostKantAsync(brukerId, new
        {
            Kategori = "I", Typekode = "innehar", FraVirksomhetId = virksomhet, TilBegrepId = rolle.Id, HjemmelRettskildeId = hjemmelId,
            Paragrafspenn = spenn, Polaritet = "positiv",
        });
        Assert.Equal(HttpStatusCode.Created, innehar.StatusCode);
        var gruppeAvGruppe = await PostKantAsync(brukerId, new
        {
            Kategori = "M", Typekode = "medlem_av", FraBegrepId = klasse.Id, TilBegrepId = omrade.Id, HjemmelRettskildeId = hjemmelId,
            Paragrafspenn = spenn, Polaritet = "positiv",
        });
        Assert.Equal(HttpStatusCode.Created, gruppeAvGruppe.StatusCode);

        var tildelinger = await _client.GetFromJsonAsync<List<MyndighetstildelingDto>>($"/api/virksomheter/{virksomhet}/myndighetstildelinger", JsonInnstillinger);
        Assert.Equal(2, tildelinger!.Count);
        var gammelForm = Assert.Single(tildelinger, t => t.GruppeBegrepId == klasse.Id);
        Assert.Equal(("fengsel", hjemmelId), (gammelForm.Vilkaar, gammelForm.HjemmelRettskildeId));
        var gjeldende = await _client.GetFromJsonAsync<List<MyndighetstildelingDto>>($"/api/virksomheter/{virksomhet}/myndighetstildelinger?gjeldende=true", JsonInnstillinger);
        Assert.Equal(rolle.Id, Assert.Single(gjeldende!).GruppeBegrepId);

        Assert.Single((await _client.GetFromJsonAsync<List<MyndighetstildelingDto>>($"/api/gruppebegrep/{rolle.Id}/tildelinger", JsonInnstillinger))!);
        var medlemsgrupper = await _client.GetFromJsonAsync<List<GruppeMedlemskapDto>>($"/api/gruppebegrep/{omrade.Id}/medlemsgrupper", JsonInnstillinger);
        Assert.Equal(klasse.Id, Assert.Single(medlemsgrupper!).UnderordnetGruppeBegrepId);
        var overordnede = await _client.GetFromJsonAsync<List<GruppeMedlemskapDto>>($"/api/gruppebegrep/{klasse.Id}/overordnede-grupper", JsonInnstillinger);
        Assert.Equal(omrade.Id, Assert.Single(overordnede!).OverordnetGruppeBegrepId);

        // Begrepets side i det nye endepunktet: én kant inn (virksomheten) og én ut (området).
        var forKlassen = await _client.GetFromJsonAsync<List<StrukturkantDto>>($"/api/strukturkanter?begrepId={klasse.Id}", JsonInnstillinger);
        Assert.Equal(2, forKlassen!.Count);
        Assert.Contains(forKlassen, k => k.Retning == "til" && k.Fra.Type == "virksomhet");
        Assert.Contains(forKlassen, k => k.Retning == "fra" && k.Til!.Nodetype == "omrade");
        _ = lovId; _ = paragrafEid;
    }

    [Fact]
    public async Task Statistikken_teller_virksomheter_i_begge_ender_av_hjemlede_kanter()
    {
        var brukerId = await HentJuristIdAsync();
        var (lovId, _) = await OpprettRettskildeMedParagrafAsync();
        var (a, _) = await OpprettVirksomhetAsync("Stat A");
        var (b, _) = await OpprettVirksomhetAsync("Stat B");
        Assert.Equal(HttpStatusCode.Created, (await PostKantAsync(brukerId, new
        {
            Kategori = "R", Typekode = "klageinstans_for", FraVirksomhetId = a, TilVirksomhetId = b, HjemmelRettskildeId = lovId, Polaritet = "positiv",
        })).StatusCode);

        var statistikk = await _client.GetFromJsonAsync<JsonElement>($"/api/rettskilder/{lovId}/statistikk", JsonInnstillinger);
        Assert.Equal(2, statistikk.GetProperty("antallVirksomheter").GetInt32());
    }

    // ---------- [Flyttet fra MyndighetstildelingEndepunktTests, issue #310] nodetype og aktørtype ----------

    [Fact]
    public async Task Nodetype_kan_settes_pa_gruppebegrep_og_ugyldig_type_gir_400()
    {
        var brukerId = await HentJuristIdAsync();
        var (lovId, _) = await OpprettRettskildeMedParagrafAsync();
        var utenType = await _client.SendAsync(MedBruker(HttpMethod.Post, "/api/gruppebegrep", brukerId,
            new { LovkildeId = lovId, Term = $"uten-type-{Guid.NewGuid():N}" }));
        Assert.Equal(HttpStatusCode.BadRequest, utenType.StatusCode); // nodetype påkrevd — gjettes ikke.

        var begrep = await OpprettBegrepAsync(brukerId, lovId, "rolle", "vertskommuner");
        var settSvar = await _client.SendAsync(MedBruker(HttpMethod.Put, $"/api/gruppebegrep/{begrep.Id}/nodetype", brukerId,
            new { Nodetype = "klasse" }));
        Assert.Equal(HttpStatusCode.OK, settSvar.StatusCode);
        Assert.Equal("klasse", (await settSvar.Content.ReadFromJsonAsync<BegrepDto>(JsonInnstillinger))!.Begrepskategori);

        foreach (var ugyldigType in new[] { "gruppe", "organ" }) // [ENDRET, issue #311] organ er en Virksomhet.
        {
            var ugyldig = await _client.SendAsync(MedBruker(HttpMethod.Put, $"/api/gruppebegrep/{begrep.Id}/nodetype", brukerId,
                new { Nodetype = ugyldigType }));
            Assert.Equal(HttpStatusCode.BadRequest, ugyldig.StatusCode);
        }
        var ukjent = await _client.SendAsync(MedBruker(HttpMethod.Put, $"/api/gruppebegrep/{Guid.NewGuid()}/nodetype", brukerId,
            new { Nodetype = "rolle" }));
        Assert.Equal(HttpStatusCode.NotFound, ukjent.StatusCode);
    }

    [Fact]
    public async Task Aktortype_kan_settes_nullstilles_og_ugyldig_verdi_gir_400()
    {
        var (virksomhetId, _) = await OpprettVirksomhetAsync("Aktortype");

        var settSvar = await _client.PutAsJsonAsync($"/api/virksomheter/{virksomhetId}/aktortype", new { Aktortype = "organ" });
        Assert.Equal(HttpStatusCode.OK, settSvar.StatusCode);
        Assert.Equal("organ", (await settSvar.Content.ReadFromJsonAsync<VirksomhetDto>(JsonInnstillinger))!.Aktortype);

        var nullSvar = await _client.PutAsJsonAsync($"/api/virksomheter/{virksomhetId}/aktortype", new { Aktortype = (string?)null });
        Assert.Equal(HttpStatusCode.OK, nullSvar.StatusCode);
        Assert.Null((await nullSvar.Content.ReadFromJsonAsync<VirksomhetDto>(JsonInnstillinger))!.Aktortype);

        var ugyldig = await _client.PutAsJsonAsync($"/api/virksomheter/{virksomhetId}/aktortype", new { Aktortype = "kommune" });
        Assert.Equal(HttpStatusCode.BadRequest, ugyldig.StatusCode);
    }

    /// <summary>Organene fra migrasjonen finnes i enhver base — også i API-testenes (Johanns beslutning på #311).</summary>
    [Fact]
    public async Task Organene_finnes_som_virksomheter_med_aktortype_organ()
    {
        await using var db = _fixture.NyDbContext();
        var stortinget = await db.Virksomheter.SingleAsync(v => v.Organisasjonsnummer == StrukturkantMigrering.StortingetOrgnr);
        var kongen = await db.Virksomheter.SingleAsync(v => v.Navn == StrukturkantMigrering.KongenIStatsradNavn);
        Assert.Equal(("organ", "organ"), (stortinget.Aktortype, kongen.Aktortype));
        Assert.False(await db.Begreper.AnyAsync(b => b.Begrepskategori == "organ"));
    }
}
