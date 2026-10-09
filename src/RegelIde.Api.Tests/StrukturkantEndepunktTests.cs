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
    public async Task Typekonfigurasjonen_har_alle_aatte_kategoriene_og_ingen_av_de_gamle_relasjonskodene()
    {
        var alle = await _client.GetFromJsonAsync<List<RelasjonsTypeKonfigurasjonDto>>("/api/konfigurasjon/relasjonstyper", JsonInnstillinger);
        Assert.Equal(Strukturkanter.Kategorier.OrderBy(k => k), alle!.Select(t => t.Kategori).Distinct().OrderBy(k => k));

        var r = await _client.GetFromJsonAsync<List<RelasjonsTypeKonfigurasjonDto>>("/api/konfigurasjon/relasjonstyper?kategori=R", JsonInnstillinger);
        Assert.All(r!, t => Assert.Equal("R", t.Kategori));
        // [ENDRET, issue #330] Før: «de gamle relasjonstypene uendret». Nå skal ingen av dem tilbys (veiviseren og
        // «Legg til relasjon» lister nettopp denne lista) — og API-oppstartens seed skal ikke ha lagt dem inn igjen.
        Assert.DoesNotContain(r!, t => RelasjonskodeHarmonisering.GamleKoder.Contains(t.Kode));
        Assert.DoesNotContain(alle!, t => RelasjonskodeHarmonisering.GamleKoder.Contains(t.Kode));
        var sekretariat = Assert.Single(r!, t => t.Kode == "sekretariat_for");
        Assert.Equal(("er sekretariat for {0}", "har sekretariat hos {0}"), (sekretariat.FraVisningsmal, sekretariat.TilVisningsmal));
        // [ENDRET, issue #341] Myndighetsrelasjonene er ikke lenger R-typer — de er K med motpart.
        Assert.DoesNotContain(r!, t => t.Kode is "klageinstans_for" or "instruksjon" or "omgjoring" or "oppnevner" or "delegerer_til");
        Assert.Contains(r!, t => t.Kode == "etterfolger");
        Assert.Contains(r!, t => t.Kode == Strukturkanter.HarDelegertTil);
        var g = await _client.GetFromJsonAsync<List<RelasjonsTypeKonfigurasjonDto>>("/api/konfigurasjon/relasjonstyper?kategori=G", JsonInnstillinger);
        Assert.Contains(g!, t => t.Kode == "del_av"); // der enhet_i havnet
        // [Ny, issue #352-tillegg] Sammensetningen i den enkelte sak er saksavhengig; organets faste medlemmer er det ikke.
        Assert.True(Assert.Single(g!, t => t.Kode == Strukturkanter.SettesMed).Saksavhengig);
        Assert.False(Assert.Single(g!, t => t.Kode == "har_medlemmer").Saksavhengig);

        var k = await _client.GetFromJsonAsync<List<RelasjonsTypeKonfigurasjonDto>>("/api/konfigurasjon/relasjonstyper?kategori=K", JsonInnstillinger);
        // [ENDRET, issue #341] forskrift → normgivning (normform forskrift); hver K-type har familie og ev. fvl-kategori.
        Assert.DoesNotContain(k!, t => t.Kode == "forskrift");
        var normgivning = Assert.Single(k!, t => t.Kode == Strukturkanter.Normgivning);
        Assert.Equal("normgivning", normgivning.Familie);
        var klage = Assert.Single(k!, t => t.Kode == "klage");
        Assert.Equal(("klage_overproving", "har klagekompetanse {0}"), (klage.Familie, klage.FraVisningsmal));
        Assert.Equal("enkeltvedtak", Assert.Single(k!, t => t.Kode == "vedtak").FvlKategori);
        Assert.Null(Assert.Single(k!, t => t.Kode == Strukturkanter.Beslutning).Familie);
        // [Ny, issue #352, Johanns beslutninger 2026-10-08] Familien heter oppnevning; utpeking/ansettelse er undertyper, ikke
        // typer; forelegging er kontroll. R velger/ankeinstans_for er flyttet til K; radgir og oppretter er fortsatt R.
        Assert.Equal("oppnevning", Assert.Single(k!, t => t.Kode == Strukturkanter.Oppnevning).Familie);
        Assert.Equal("oppnevning", Assert.Single(k!, t => t.Kode == "avsetting").Familie);
        Assert.Equal("kontroll", Assert.Single(k!, t => t.Kode == "forelegging").Familie);
        Assert.DoesNotContain(k!, t => t.Kode is "utpeking" or "ansettelse" || t.Familie == "personell");
        Assert.DoesNotContain(r!, t => t.Kode is "velger" or "ankeinstans_for");
        Assert.Contains(r!, t => t.Kode == "radgir");
        Assert.Contains(r!, t => t.Kode == "oppretter");
    }

    /// <summary>[Ny, issue #352] S9 «hvem velger medlemmene av forliksrådet?»: oppnevningskompetanse med undertype valg og
    /// motpart gjennom API-et; familiefilteret oppnevning; feil undertype gir 400.</summary>
    [Fact]
    public async Task K_oppnevning_med_undertype_og_familien_oppnevning()
    {
        var brukerId = await HentJuristIdAsync();
        var (lovId, paragrafEid) = await OpprettRettskildeMedParagrafAsync();
        var (kommunestyret, _) = await OpprettVirksomhetAsync("Kommunestyret");
        var (forliksradet, forliksradetNavn) = await OpprettVirksomhetAsync("Forliksrådet");

        var svar = await PostKantAsync(brukerId, new
        {
            Kategori = "K", Typekode = "oppnevning", FraVirksomhetId = kommunestyret, TilVirksomhetId = forliksradet,
            HjemmelRettskildeId = lovId, HjemmelEid = paragrafEid, Undertype = "valg", Polaritet = "positiv",
        });
        Assert.Equal(HttpStatusCode.Created, svar.StatusCode);
        var kant = (await svar.Content.ReadFromJsonAsync<StrukturkantDto>(JsonInnstillinger))!;
        Assert.Equal(("valg", "oppnevning"), (kant.Undertype, kant.Familie));

        var oppnevning = await _client.GetFromJsonAsync<List<StrukturkantDto>>(
            $"/api/strukturkanter?virksomhetId={kommunestyret}&familie=oppnevning", JsonInnstillinger);
        Assert.Equal($"har oppnevningskompetanse (valg) overfor {forliksradetNavn}", Assert.Single(oppnevning!).Visningstekst);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/strukturkanter?virksomhetId={kommunestyret}&familie=personell")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostKantAsync(brukerId, new
        {
            Kategori = "K", Typekode = "oppnevning", FraVirksomhetId = kommunestyret, TilVirksomhetId = forliksradet,
            HjemmelRettskildeId = lovId, Undertype = "anke", Polaritet = "positiv",
        })).StatusCode);
    }

    /// <summary>[Ny, issue #353] P med modalitet gjennom API-et; folketrygden som ordning (aktørtype + ordningstype) forvaltet av
    /// et organ; og S6 «hvem har kommune X samarbeidsplikt med?» med klassen og et synlig hull.</summary>
    [Fact]
    public async Task P_plikt_ordning_og_S6_plikter_for_kommune()
    {
        var brukerId = await HentJuristIdAsync();
        var (lovId, paragrafEid) = await OpprettRettskildeMedParagrafAsync();
        var (folketrygden, _) = await OpprettVirksomhetAsync("Folketrygden");
        var (hdir, hdirNavn) = await OpprettVirksomhetAsync("Helsedirektoratet");

        // Ordningstype uten aktørtype ordning avvises; med går den gjennom.
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync($"/api/virksomheter/{folketrygden}/aktortype",
            new { Aktortype = "organ", Ordningstype = "trygdeordning" })).StatusCode);
        var ordning = await _client.PutAsJsonAsync($"/api/virksomheter/{folketrygden}/aktortype",
            new { Aktortype = "ordning", Ordningstype = "trygdeordning" });
        Assert.Equal(HttpStatusCode.OK, ordning.StatusCode);
        var ordningDto = (await ordning.Content.ReadFromJsonAsync<VirksomhetDto>(JsonInnstillinger))!;
        Assert.Equal(("ordning", "trygdeordning"), (ordningDto.Aktortype, ordningDto.Ordningstype));

        var forvaltes = await PostKantAsync(brukerId, new
        {
            Kategori = "R", Typekode = "forvaltes_av", FraVirksomhetId = folketrygden, TilVirksomhetId = hdir, HjemmelRettskildeId = lovId,
            HjemmelEid = paragrafEid, AvgrensningTekst = "folketrygdloven kapittel 5", Polaritet = "positiv",
        });
        Assert.Equal(HttpStatusCode.Created, forvaltes.StatusCode);
        Assert.StartsWith($"Folketrygden", (await forvaltes.Content.ReadFromJsonAsync<StrukturkantDto>(JsonInnstillinger))!.Visningstekst);

        var betaling = await PostKantAsync(brukerId, new
        {
            Kategori = "P", Typekode = "betaling", FraVirksomhetId = folketrygden, HjemmelRettskildeId = lovId, HjemmelEid = paragrafEid,
            Objekt = "behandlings- og forpleiningsutgifter", Modalitet = "skal", Polaritet = "positiv",
        });
        Assert.Equal(HttpStatusCode.Created, betaling.StatusCode);
        var kant = (await betaling.Content.ReadFromJsonAsync<StrukturkantDto>(JsonInnstillinger))!;
        Assert.Equal(("P", "skal", (StrukturnodeDto?)null), (kant.Kategori, kant.Modalitet, kant.Til));
        Assert.Contains("har betalingsplikt (skal) behandlings- og forpleiningsutgifter", kant.Visningstekst);
        // Modalitet utenfor P avvises.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostKantAsync(brukerId, new
        {
            Kategori = "R", Typekode = "forvaltes_av", FraVirksomhetId = folketrygden, TilVirksomhetId = hdir, HjemmelRettskildeId = lovId,
            Modalitet = "skal", Polaritet = "positiv",
        })).StatusCode);
        Assert.NotNull(hdirNavn);

        // S6: kommunen som område og rettssubjekt, en avtaleplikt fra en klasse uten registrert medlemskap.
        var kommunenummer = "T" + Guid.NewGuid().ToString("N")[..6];
        Guid kommuneOmrade;
        await using (var db = _fixture.NyDbContext())
        {
            var b = new BegrepEntitet
            {
                Id = Guid.NewGuid(), Begrepskategori = Nodetyper.Omrade, Omradetype = Omradetyper.Kommune, Omradekode = kommunenummer,
                Term = $"Testkommune-{kommunenummer}", Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            };
            db.Begreper.Add(b);
            db.Virksomheter.Add(new Virksomhet { Id = Guid.NewGuid(), Navn = $"Testkommune {kommunenummer}", Kommunenummer = kommunenummer });
            await db.SaveChangesAsync();
            kommuneOmrade = b.Id;
            var kommune = await db.Virksomheter.SingleAsync(v => v.Kommunenummer == kommunenummer);
            await new StrukturkantTjeneste(db).OpprettAsync(new NyStrukturkant("A", "har_ansvarsomrade", Kantnode.Virksomhet(kommune.Id),
                Kantnode.Begrep(b.Id), HjemmelRettskildeId: lovId, HjemmelEid: paragrafEid), "test");
        }
        var kommunen = await OpprettBegrepAsync(brukerId, lovId, Nodetyper.Klasse, "kommunen");
        var rhf = await OpprettBegrepAsync(brukerId, lovId, Nodetyper.Klasse, "det regionale helseforetaket i helseregionen");
        Assert.Equal(HttpStatusCode.Created, (await PostKantAsync(brukerId, new
        {
            Kategori = "P", Typekode = "avtale", FraBegrepId = kommunen.Id, TilBegrepId = rhf.Id, HjemmelRettskildeId = lovId,
            HjemmelEid = paragrafEid, Modalitet = "skal", Polaritet = "positiv",
        })).StatusCode);

        var plikter = await _client.GetFromJsonAsync<KommunePlikterDto>($"/api/omrader/kommuner/{kommunenummer}/plikter?type=avtale", JsonInnstillinger);
        Assert.Equal(kommuneOmrade, plikter!.Kommune.Id);
        // [ENDRET, #353-retting] Begge retninger: «retning» sier hvilken ende kommunen er.
        var treff = Assert.Single(plikter.Plikter, t => t.Kant.Fra.Id == kommunen.Id && t.Retning == "kommunen_skal");
        Assert.Equal(("klasse_uten_registrert_medlemskap", "mangler"), (treff.Grunnlag, treff.Motpart.Status));
        Assert.Contains("#340", treff.Motpart.Hull);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/omrader/kommuner/{kommunenummer}/plikter?type=mote")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/omrader/kommuner/XXXX/plikter")).StatusCode);
    }

    // ---------------- Opprett og les per node / per kategori ----------------

    [Fact]
    public async Task R_kant_opprettes_og_leses_fra_begge_sider_med_hver_sin_visningstekst()
    {
        var brukerId = await HentJuristIdAsync();
        var (merkenemnd, merkenemndNavn) = await OpprettVirksomhetAsync("Lokal merkenemnd");
        var (statsforvalteren, statsforvalterenNavn) = await OpprettVirksomhetAsync("Statsforvalteren");

        // [ENDRET, issue #330] sekretariat_for (statsforvalteren → nemnda) i stedet for den fjernede «sekretariat»
        // (nemnda → statsforvalteren). Samme utsagn, motsatt lagret retning — visningstekstene under er uendret.
        var svar = await PostKantAsync(brukerId, new
        {
            Kategori = "R", Typekode = "sekretariat_for", FraVirksomhetId = statsforvalteren, TilVirksomhetId = merkenemnd,
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
            Kategori = "R", Typekode = "sekretariat_for", FraVirksomhetId = statsforvalteren, TilVirksomhetId = merkenemnd,
            KildeUtenforKorpusTekst = "docs/28-eksempel", KildeUtenforKorpusType = "nettside_annet", KildeUtenforKorpusDokumentasjon = "primaer", Polaritet = "positiv",
        });
        Assert.Equal(HttpStatusCode.OK, igjen.StatusCode);
        Assert.Equal(kant.Id, (await igjen.Content.ReadFromJsonAsync<StrukturkantDto>(JsonInnstillinger))!.Id);

        // Kategorifilteret: ingen M-kanter for denne noden.
        Assert.Empty((await _client.GetFromJsonAsync<List<StrukturkantDto>>($"/api/strukturkanter?virksomhetId={merkenemnd}&kategori=M", JsonInnstillinger))!);
    }

    /// <summary>[Ny, issue #341] S9: «hvilken kompetanse har A, overfor hvem?» — K med motpart, normform/grunnlag/delegerbar
    /// gjennom API-et, og filteret på familie.</summary>
    [Fact]
    public async Task K_kant_med_motpart_normform_og_familiefilter()
    {
        var brukerId = await HentJuristIdAsync();
        var (lovId, paragrafEid) = await OpprettRettskildeMedParagrafAsync();
        var (dep, _) = await OpprettVirksomhetAsync("Energidepartementet");
        var (nemnd, nemndNavn) = await OpprettVirksomhetAsync("Energiklagenemnda");

        var klage = await PostKantAsync(brukerId, new
        {
            Kategori = "K", Typekode = "klage", FraVirksomhetId = dep, TilVirksomhetId = nemnd, HjemmelRettskildeId = lovId,
            HjemmelEid = paragrafEid, AvgrensningTekst = "enkeltvedtak i første instans", Polaritet = "positiv",
        });
        Assert.Equal(HttpStatusCode.Created, klage.StatusCode);
        var forskrift = await PostKantAsync(brukerId, new
        {
            Kategori = "K", Typekode = "normgivning", FraVirksomhetId = dep, HjemmelRettskildeId = lovId, HjemmelEid = paragrafEid,
            Objekt = "nettariffer", Normform = "forskrift", Delegerbar = true, Grunnlag = "offentligrettslig", Polaritet = "positiv",
        });
        Assert.Equal(HttpStatusCode.Created, forskrift.StatusCode);
        var f = (await forskrift.Content.ReadFromJsonAsync<StrukturkantDto>(JsonInnstillinger))!;
        Assert.Equal(("forskrift", true, "offentligrettslig", "normgivning", "forskrift"), (f.Normform, f.Delegerbar, f.Grunnlag, f.Familie, f.FvlKategori));

        var fraDep = await _client.GetFromJsonAsync<List<StrukturkantDto>>($"/api/strukturkanter?virksomhetId={dep}&kategori=K", JsonInnstillinger);
        Assert.Contains(fraDep!, k => k.Visningstekst == $"har klagekompetanse overfor {nemndNavn}");
        var fraNemnda = await _client.GetFromJsonAsync<List<StrukturkantDto>>($"/api/strukturkanter?virksomhetId={nemnd}", JsonInnstillinger);
        Assert.EndsWith("har klagekompetanse overfor denne", Assert.Single(fraNemnda!).Visningstekst);

        var normgivning = await _client.GetFromJsonAsync<List<StrukturkantDto>>($"/api/strukturkanter?virksomhetId={dep}&familie=normgivning", JsonInnstillinger);
        Assert.Equal(f.Id, Assert.Single(normgivning!).Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/strukturkanter?virksomhetId={dep}&familie=alt")).StatusCode);
        // Normform på en annen type enn normgivning: 400.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostKantAsync(brukerId, new
        {
            Kategori = "K", Typekode = "vedtak", FraVirksomhetId = dep, HjemmelRettskildeId = lovId, Objekt = "x", Normform = "forskrift", Polaritet = "positiv",
        })).StatusCode);
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
            Kategori = "R", Typekode = "administrativt_underordnet", FraVirksomhetId = a, TilVirksomhetId = b, HjemmelRettskildeId = lovId,
        })).StatusCode);
        // Ukjent typekode.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostKantAsync(brukerId, new
        {
            Kategori = "R", Typekode = "ukjent_type_finnes_ikke", FraVirksomhetId = a, TilVirksomhetId = b, HjemmelRettskildeId = lovId, Polaritet = "positiv",
        })).StatusCode);
        // Verken hjemmel eller kilde utenfor korpus.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostKantAsync(brukerId, new
        {
            Kategori = "R", Typekode = "administrativt_underordnet", FraVirksomhetId = a, TilVirksomhetId = b, Polaritet = "positiv",
        })).StatusCode);
        // Ikke-eksisterende node.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostKantAsync(brukerId, new
        {
            Kategori = "R", Typekode = "administrativt_underordnet", FraVirksomhetId = a, TilVirksomhetId = Guid.NewGuid(), HjemmelRettskildeId = lovId, Polaritet = "positiv",
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

    /// <summary>[Ny, issue #330 AC2] De gamle kodene er borte fra konfigurasjonen OG fra Startsett, så POST avviser
    /// dem — også etter at API-oppstarten har kjørt seeden (fixturen reiser verten før testene kjører).</summary>
    [Fact]
    public async Task De_gamle_relasjonskodene_avvises_med_400()
    {
        var brukerId = await HentJuristIdAsync();
        var (lovId, _) = await OpprettRettskildeMedParagrafAsync();
        var (a, _) = await OpprettVirksomhetAsync("Gammel kode A");
        var (b, _) = await OpprettVirksomhetAsync("Gammel kode B");
        foreach (var kode in RelasjonskodeHarmonisering.GamleKoder)
        {
            var svar = await PostKantAsync(brukerId, new
            {
                Kategori = "R", Typekode = kode, FraVirksomhetId = a, TilVirksomhetId = b, HjemmelRettskildeId = lovId, Polaritet = "positiv",
            });
            Assert.Equal(HttpStatusCode.BadRequest, svar.StatusCode);
            Assert.Contains($"Ukjent typekode '{kode}'", await svar.Content.ReadAsStringAsync());
        }
        Assert.DoesNotContain(Strukturkanter.Startsett, s => RelasjonskodeHarmonisering.GamleKoder.Contains(s.Kode));
    }

    /// <summary>
    /// [Ny, issue #330] PUT /api/strukturkanter/{id}/avgrensning — veien Energiklagenemnda-raden rettes gjennom.
    /// Spørsmålene: settes spenn og tekst på SAMME kant (id beholdes), logges gammel og ny verdi i Proveniens, og
    /// avvises en ukjent eId, en ukjent kant og en uinnlogget bruker?
    /// </summary>
    [Fact]
    public async Task Avgrensning_kan_settes_paa_en_eksisterende_kant_og_logges()
    {
        var brukerId = await HentJuristIdAsync();
        var (lovId, paragrafEid) = await OpprettRettskildeMedParagrafAsync();
        var (dep, _) = await OpprettVirksomhetAsync("Departementet");
        var (nemnd, _) = await OpprettVirksomhetAsync("Klagenemnda");
        var opprettet = await PostKantAsync(brukerId, new
        {
            Kategori = "K", Typekode = "klage", FraVirksomhetId = dep, TilVirksomhetId = nemnd,
            HjemmelRettskildeId = lovId, HjemmelEid = paragrafEid, Polaritet = "positiv",
        });
        var kant = (await opprettet.Content.ReadFromJsonAsync<StrukturkantDto>(JsonInnstillinger))!;
        Assert.Empty(kant.Paragrafspenn);
        Assert.Null(kant.AvgrensningTekst);

        var url = $"/api/strukturkanter/{kant.Id}/avgrensning";
        var body = new
        {
            Paragrafspenn = new[] { new { FraEid = paragrafEid, TilEid = (string?)null } },
            AvgrensningTekst = "  enkeltvedtak nemnda treffer i første instans ",
        };
        var svar = await _client.SendAsync(MedBruker(HttpMethod.Put, url, brukerId, body));
        Assert.Equal(HttpStatusCode.OK, svar.StatusCode);
        var etter = (await svar.Content.ReadFromJsonAsync<StrukturkantDto>(JsonInnstillinger))!;
        Assert.Equal(kant.Id, etter.Id);
        Assert.Equal(paragrafEid, Assert.Single(etter.Paragrafspenn).FraEid);
        Assert.Equal("enkeltvedtak nemnda treffer i første instans", etter.AvgrensningTekst);
        Assert.Equal(("klage", dep, nemnd), (etter.Typekode, etter.Fra.Id, etter.Til!.Id)); // identiteten urørt

        await using (var db = _fixture.NyDbContext())
        {
            var p = await db.Proveniens.SingleAsync(x => x.EntitetId == kant.Id && x.Handling == "endret");
            Assert.Equal(StrukturkantTjeneste.ProveniensType, p.EntitetType);
            using var refs = JsonDocument.Parse(p.KildeReferanserJson!);
            Assert.Equal("avgrensning", refs.RootElement.GetProperty("felt").GetString());
            Assert.Equal(JsonValueKind.Null, refs.RootElement.GetProperty("forAvgrensningTekst").ValueKind);
            Assert.Equal(paragrafEid, refs.RootElement.GetProperty("nyttParagrafspenn")[0].GetProperty("FraEid").GetString());
            var lagret = await db.Strukturkanter.AsNoTracking().SingleAsync(x => x.Id == kant.Id);
            Assert.NotNull(lagret.SistEndretTidspunkt);
        }

        // Samme verdi igjen: 200, ingen ny proveniensrad.
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(MedBruker(HttpMethod.Put, url, brukerId, body))).StatusCode);
        await using (var db = _fixture.NyDbContext())
        {
            Assert.Equal(1, await db.Proveniens.CountAsync(x => x.EntitetId == kant.Id && x.Handling == "endret"));
        }

        // Ukjent eId: 400, og kanten er uendret.
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SendAsync(MedBruker(HttpMethod.Put, url, brukerId, new
        {
            Paragrafspenn = new[] { new { FraEid = "https://test/finnes-ikke/§9", TilEid = (string?)null } },
        }))).StatusCode);
        var uendret = await _client.GetFromJsonAsync<StrukturkantDto>($"/api/strukturkanter/{kant.Id}", JsonInnstillinger);
        Assert.Equal(paragrafEid, Assert.Single(uendret!.Paragrafspenn).FraEid);

        // Ukjent kant: 404. Uten bruker: avvist.
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(MedBruker(HttpMethod.Put,
            $"/api/strukturkanter/{Guid.NewGuid()}/avgrensning", brukerId, body))).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await _client.PutAsJsonAsync(url, body)).StatusCode);
    }

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
        // [ENDRET, issue #341] Hjemmelsstedet er hjemmel-eId; lesefasaden viser det fortsatt som paragrafspenn.

        var utlopt = await PostKantAsync(brukerId, new
        {
            Kategori = "M", Typekode = "medlem_av", FraVirksomhetId = virksomhet, TilBegrepId = klasse.Id, HjemmelRettskildeId = hjemmelId,
            HjemmelEid = hjemmelEid, AvgrensningTekst = "fengsel", Polaritet = "positiv",
            GyldigFra = new DateOnly(2020, 1, 1), GyldigTil = new DateOnly(2021, 12, 31),
        });
        Assert.Equal(HttpStatusCode.Created, utlopt.StatusCode);
        var innehar = await PostKantAsync(brukerId, new
        {
            Kategori = "I", Typekode = "innehar", FraVirksomhetId = virksomhet, TilBegrepId = rolle.Id, HjemmelRettskildeId = hjemmelId,
            HjemmelEid = hjemmelEid, Polaritet = "positiv",
        });
        Assert.Equal(HttpStatusCode.Created, innehar.StatusCode);
        var gruppeAvGruppe = await PostKantAsync(brukerId, new
        {
            Kategori = "M", Typekode = "medlem_av", FraBegrepId = klasse.Id, TilBegrepId = omrade.Id, HjemmelRettskildeId = hjemmelId,
            HjemmelEid = hjemmelEid, Polaritet = "positiv",
        });
        Assert.Equal(HttpStatusCode.Created, gruppeAvGruppe.StatusCode);

        var tildelinger = await _client.GetFromJsonAsync<List<MyndighetstildelingDto>>($"/api/virksomheter/{virksomhet}/myndighetstildelinger", JsonInnstillinger);
        Assert.Equal(2, tildelinger!.Count);
        var gammelForm = Assert.Single(tildelinger, t => t.GruppeBegrepId == klasse.Id);
        Assert.Equal(("fengsel", hjemmelId), (gammelForm.Vilkaar, gammelForm.HjemmelRettskildeId));
        Assert.Equal(hjemmelEid, Assert.Single(gammelForm.Paragrafspenn).FraEid); // nettsidens hjemmelLabel leser dette
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
            Kategori = "K", Typekode = "klage", FraVirksomhetId = a, TilVirksomhetId = b, HjemmelRettskildeId = lovId, Polaritet = "positiv",
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
