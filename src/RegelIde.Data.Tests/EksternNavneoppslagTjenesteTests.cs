using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// docs/31-navneform-berikelse-snl-ssr-spesifikasjon.md — <see cref="EksternNavneoppslagTjeneste"/>.
/// Stub-<see cref="HttpMessageHandler"/> (samme prinsipp som <c>BrregKlientTests</c>) — INGEN ekte
/// nettverkskall mot snl.no/ws.geonorge.no i denne test-suiten. Se
/// <see cref="EksternNavneoppslagTjenesteLiveTests"/> for de(t) ekte, levende kallet/kallene per API.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class EksternNavneoppslagTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public EksternNavneoppslagTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Ruter på et vilkårlig delegat — enklere enn BrregKlientTests sin rene URL-prefiks-
    /// dictionary siden denne tjenesten kaller TO ulike API-er (snl.no søk, snl.no artikkel-JSON,
    /// ws.geonorge.no søk) med ulik URL-form per kall, og enkelte tester trenger å KASTE for å
    /// simulere en nettverksfeil (docs/31 §3).</summary>
    private sealed class RutetHandler(Func<HttpRequestMessage, HttpResponseMessage> svar) : HttpMessageHandler
    {
        public int AntallKall { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            AntallKall++;
            return Task.FromResult(svar(request));
        }
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    // ---------- SNL ----------

    /// <summary>
    /// Kjernescenariet fra docs/31 §6/Johanns eget testeksempel: søk på "Advokatforeningen" gir INGEN
    /// søketreff med <c>headword</c> nøyaktig lik "Advokatforeningen" (den offisielle artikkelen heter
    /// "Den Norske Advokatforening"; "Advokatforeningen" er kun artikkelens "også kjent som"-alias) —
    /// verifisert LIVE under byggingen (se <see cref="EksternNavneoppslagTjenesteLiveTests"/>). Denne
    /// testen speiler nøyaktig den formen med en stubbet, deterministisk versjon av samme respons.
    /// </summary>
    [Fact]
    public async Task SlaOppSnlAsync_bekrefter_institusjon_via_alias_ikke_bare_headword()
    {
        var handler = new RutetHandler(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("/api/v1/search"))
            {
                return Json("""
                [
                  { "article_type_id": 16, "taxonomy_title": "Arbeidslivsorganisasjoner",
                    "article_url": "https://snl.no/Den_Norske_Advokatforening",
                    "article_url_json": "https://snl.no/Den_Norske_Advokatforening.json" },
                  { "article_type_id": 1, "taxonomy_title": "Rettsvesen",
                    "article_url": "https://snl.no/advokatforening",
                    "article_url_json": "https://snl.no/advokatforening.json" }
                ]
                """);
            }
            if (url.EndsWith("Den_Norske_Advokatforening.json"))
            {
                return Json("""
                {
                  "headword": "Den Norske Advokatforening",
                  "url": "https://snl.no/Den_Norske_Advokatforening",
                  "metadata": {
                    "organization_name": "Den Norske Advokatforening",
                    "organization_number": "936575668",
                    "alternative_form": "<p>Advokatforeningen</p>"
                  }
                }
                """);
            }
            throw new InvalidOperationException($"Uventet URL i test: {url}");
        });

        await using var db = _fixture.NyDbContext();
        var tjeneste = new EksternNavneoppslagTjeneste(new HttpClient(handler), db);

        var resultat = await tjeneste.SlaOppSnlAsync("Advokatforeningen");

        Assert.True(resultat.Treff);
        Assert.Equal("Arbeidslivsorganisasjoner", resultat.TaksonomiKategori);
        Assert.Equal("https://snl.no/Den_Norske_Advokatforening", resultat.EksternUrl);
        Assert.Equal("936575668", resultat.Organisasjonsnummer);
        Assert.Contains("Advokatforeningen", resultat.Alias!);
        // [Ny, #158] BekreftetNavn er artikkelens EGEN normalt skrevne headword — IKKE søketermen
        // ("Advokatforeningen", alias) som ble slått opp. Brukt til å foreslå en navneform ved
        // Brreg-import (POST /api/virksomheter/fra-brreg) uten en gjettet versalisering.
        Assert.Equal("Den Norske Advokatforening", resultat.BekreftetNavn);
        // KUN organisasjonstype-treffet (article_type_id 16) skal ha blitt hentet i fullt — det andre
        // (article_type_id 1, "advokatforening" generisk ordforklaring) skal ALDRI trigge et ekstra kall.
        Assert.Equal(2, handler.AntallKall); // 1 søk + 1 artikkel-JSON
    }

    /// <summary>[Ny, #158] Cache-hit-veien (<see cref="EksternNavneoppslagTjeneste"/> leser fra
    /// <see cref="EksternNavneoppslagCacheEntitet"/> før noe nettverkskall) må bevare
    /// <see cref="EksternOppslagResultat.BekreftetNavn"/> på samme måte som de andre feltene — ellers
    /// ville et cache-hit for et allerede slått opp navn stille miste navneform-forslaget andre gang.</summary>
    [Fact]
    public async Task SlaOppSnlAsync_bevarer_bekreftet_navn_pa_cache_hit()
    {
        var handler = new RutetHandler(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("/api/v1/search"))
            {
                return Json("""
                [{ "article_type_id": 16, "taxonomy_title": "Offentlige etater og direktorater",
                   "article_url": "https://snl.no/Miljodirektoratet",
                   "article_url_json": "https://snl.no/Miljodirektoratet.json" }]
                """);
            }
            if (url.EndsWith("Miljodirektoratet.json"))
            {
                // [Rettet, 2026-09-03] "title" (ikke "headword") er artikkelens FAKTISKE toppnivå-
                // tittelfelt live — og "metadata" må være til stede (om enn tom/uten organization_name,
                // som for en ren statlig etat uten AS-registrering) for at koden i det hele tatt skal
                // vurdere treffet — se SlaOppSnlAsync sin kommentar.
                return Json("""{ "title": "Miljødirektoratet", "url": "https://snl.no/Miljodirektoratet", "metadata": {} }""");
            }
            throw new InvalidOperationException($"Uventet URL i test: {url}");
        });

        await using var db = _fixture.NyDbContext();
        var forsteKall = await new EksternNavneoppslagTjeneste(new HttpClient(handler), db)
            .SlaOppSnlAsync("MILJØDIREKTORATET");
        Assert.Equal("Miljødirektoratet", forsteKall.BekreftetNavn);

        // Nytt DbContext + en handler som ville kastet på ethvert nettverkskall — cache-hit MÅ unngå det.
        var kasterHandler = new RutetHandler(_ => throw new InvalidOperationException("Skulle vært cache-hit."));
        await using var db2 = _fixture.NyDbContext();
        var andreKall = await new EksternNavneoppslagTjeneste(new HttpClient(kasterHandler), db2)
            .SlaOppSnlAsync("MILJØDIREKTORATET");

        Assert.True(andreKall.Treff);
        Assert.Equal("Miljødirektoratet", andreKall.BekreftetNavn);
        Assert.Equal(0, kasterHandler.AntallKall);
    }

    [Fact]
    public async Task SlaOppSnlAsync_person_gir_ingen_treff_selv_med_samme_navn_i_snippet()
    {
        var handler = new RutetHandler(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("/api/v1/search"))
            {
                return Json("""[{ "article_type_id": 2, "taxonomy_title": "Norske politikere" }]""");
            }
            throw new InvalidOperationException($"Uventet URL i test: {url}");
        });

        await using var db = _fixture.NyDbContext();
        var tjeneste = new EksternNavneoppslagTjeneste(new HttpClient(handler), db);

        var resultat = await tjeneste.SlaOppSnlAsync("EnPolitiker");

        Assert.False(resultat.Treff);
    }

    [Fact]
    public async Task SlaOppSnlAsync_organisasjonstreff_uten_navnematch_gir_ingen_treff()
    {
        // article_type_id 16 (organisasjon), men verken headword/organization_name/alias tilsvarer
        // søketermen -- skal IKKE bekreftes (docs/31 §6: for løs match ville gitt falske positiver).
        var handler = new RutetHandler(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("/api/v1/search"))
            {
                return Json("""
                [{ "article_type_id": 16, "taxonomy_title": "Test", "article_url_json": "https://snl.no/X.json" }]
                """);
            }
            if (url.EndsWith("X.json"))
            {
                return Json("""{ "headword": "HeltAnnenting", "metadata": { "organization_name": "HeltAnnenting" } }""");
            }
            throw new InvalidOperationException($"Uventet URL i test: {url}");
        });

        await using var db = _fixture.NyDbContext();
        var tjeneste = new EksternNavneoppslagTjeneste(new HttpClient(handler), db);

        var resultat = await tjeneste.SlaOppSnlAsync("UrelatertTerm");

        Assert.False(resultat.Treff);
    }

    // ---------- SSR ----------

    [Fact]
    public async Task SlaOppSsrAsync_eksakt_skrivemate_treff_gir_treff_med_objekttype()
    {
        // NB: "Nordbukta" er bevisst FORSKJELLIG fra termen i testen under (og fra alle andre termer i
        // denne delte DataTestCollection-en, se NavnekandidatOppdagelseTjenesteTests Del F) — samme
        // (Term, Kilde)-cachenøkkel ville ellers kollidert på tvers av tester/testklasser og latt
        // FØRSTE kjørte test sitt (feil, for den andre testen) cache-svar "vinne" for begge.
        var handler = new RutetHandler(req => Json("""
        {
          "navn": [
            { "skrivemåte": "Nordbukta", "navneobjekttype": "Tettsted" }
          ]
        }
        """));

        await using var db = _fixture.NyDbContext();
        var tjeneste = new EksternNavneoppslagTjeneste(new HttpClient(handler), db);

        var resultat = await tjeneste.SlaOppSsrAsync("Nordbukta");

        Assert.True(resultat.Treff);
        Assert.Equal("Tettsted", resultat.TaksonomiKategori);
    }

    [Fact]
    public async Task SlaOppSsrAsync_ingen_eksakt_skrivemate_gir_ingen_treff()
    {
        // Fulltekstsøket kan gi treff på NÆRLIGGENDE navn uten at søketermen selv er et stadnamn.
        var handler = new RutetHandler(req => Json("""
        { "navn": [ { "skrivemåte": "Sorengkaia", "navneobjekttype": "Bukt" } ] }
        """));

        await using var db = _fixture.NyDbContext();
        var tjeneste = new EksternNavneoppslagTjeneste(new HttpClient(handler), db);

        var resultat = await tjeneste.SlaOppSsrAsync("Sorenga");

        Assert.False(resultat.Treff);
    }

    // ---------- Cache ----------

    [Fact]
    public async Task Samme_term_slatt_opp_to_ganger_gjor_kun_ett_faktisk_http_kall()
    {
        var handler = new RutetHandler(req => Json("""[{ "article_type_id": 2 }]""")); // ingen org-treff -> raskt "ingen treff"

        await using var db = _fixture.NyDbContext();
        var tjeneste = new EksternNavneoppslagTjeneste(new HttpClient(handler), db);
        var term = "Cachetest" + Guid.NewGuid().ToString("N"); // unik term -- delt DB mellom tester

        var resultat1 = await tjeneste.SlaOppSnlAsync(term);
        var resultat2 = await tjeneste.SlaOppSnlAsync(term);

        Assert.False(resultat1.Treff);
        Assert.False(resultat2.Treff);
        Assert.Equal(1, handler.AntallKall); // KUN søkekallet for FØRSTE oppslag -- andre er cache-hit.

        var cacheRad = await db.EksternNavneoppslagCache.SingleAsync(c => c.Term == term.ToLowerInvariant() && c.Kilde == "snl");
        Assert.False(cacheRad.Treff);
    }

    [Fact]
    public async Task Cache_treffer_pa_tvers_av_store_sma_bokstaver_i_termen()
    {
        var handler = new RutetHandler(req => Json("""[{ "article_type_id": 2 }]"""));
        await using var db = _fixture.NyDbContext();
        var tjeneste = new EksternNavneoppslagTjeneste(new HttpClient(handler), db);
        var term = "BlandetCase" + Guid.NewGuid().ToString("N");

        await tjeneste.SlaOppSnlAsync(term);
        await tjeneste.SlaOppSnlAsync(term.ToUpperInvariant());

        Assert.Equal(1, handler.AntallKall);
    }

    // ---------- Nettverksfeil ----------

    [Fact]
    public async Task Nettverksfeil_gir_ingen_treff_uten_a_kaste_og_cacher_ikke()
    {
        var handler = new RutetHandler(_ => throw new HttpRequestException("simulert nettverksfeil"));
        await using var db = _fixture.NyDbContext();
        var tjeneste = new EksternNavneoppslagTjeneste(new HttpClient(handler), db);
        var term = "Feilterm" + Guid.NewGuid().ToString("N");

        var resultat = await tjeneste.SlaOppSnlAsync(term);

        Assert.False(resultat.Treff); // "ukjent" behandlet som "ingen treff" (docs/31 §3), IKKE en kastet feil.
        Assert.False(await db.EksternNavneoppslagCache.AnyAsync(c => c.Term == term.ToLowerInvariant()));
    }

    [Fact]
    public async Task Nettverksfeil_ikke_cachet_sa_nytt_forsok_prover_pa_nytt_live()
    {
        var forsteForsokFeilet = true;
        var handler = new RutetHandler(req =>
        {
            if (forsteForsokFeilet)
            {
                forsteForsokFeilet = false;
                throw new HttpRequestException("simulert forbigående feil");
            }
            return Json("""[{ "article_type_id": 2 }]""");
        });
        await using var db = _fixture.NyDbContext();
        var tjeneste = new EksternNavneoppslagTjeneste(new HttpClient(handler), db);
        var term = "Retryterm" + Guid.NewGuid().ToString("N");

        var forsteResultat = await tjeneste.SlaOppSnlAsync(term);
        var andreResultat = await tjeneste.SlaOppSnlAsync(term);

        Assert.False(forsteResultat.Treff);
        Assert.False(andreResultat.Treff);
        Assert.Equal(2, handler.AntallKall); // IKKE 1 -- feilen forgiftet ikke cachen, andre forsøk gikk live på nytt.
    }

    [Fact]
    public async Task Timeout_TaskCanceledException_uten_egen_cancellation_gir_ogsa_ingen_treff()
    {
        var handler = new RutetHandler(_ => throw new TaskCanceledException("simulert timeout"));
        await using var db = _fixture.NyDbContext();
        var tjeneste = new EksternNavneoppslagTjeneste(new HttpClient(handler), db);

        var resultat = await tjeneste.SlaOppSsrAsync("Timeoutterm" + Guid.NewGuid().ToString("N"));

        Assert.False(resultat.Treff);
    }

    /// <summary>Til forskjell fra testen over: her er det CALLERENS EGEN token som avbrytes — dette
    /// er en ekte cancellation, IKKE en nettverksfeil, og skal derfor propagere, ikke svelges som
    /// "ingen treff" (docs/31 §3s "behandles som ukjent" gjelder KUN nettverksfeil).</summary>
    [Fact]
    public async Task Ekte_cancellation_fra_calleren_propagerer_og_svelges_ikke()
    {
        var handler = new RutetHandler(_ => throw new TaskCanceledException("skal aldri nås"));
        await using var db = _fixture.NyDbContext();
        var tjeneste = new EksternNavneoppslagTjeneste(new HttpClient(handler), db);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => tjeneste.SlaOppSnlAsync("Avbruttterm", cts.Token));
    }
}
