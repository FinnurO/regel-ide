using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>«Identifiser tjenester» (byggesteg 5 runde 1, docs/06-veikart.md), mot ekte embedded Postgres og den ekte <see cref="KiAgentKlientStub"/>.</summary>
[Collection(DataTestCollection.Navn)]
public class TjenesteforslagTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public TjenesteforslagTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Kjorer_forslag_oppretter_tjeneste_med_status_foreslatt_av_ai_og_proveniens()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));
        await new KunnskapsbibliotekTjeneste(db).LeggTilLenkeAsync(
            virksomhet, "https://testkommunen.no/tjenester", "Om tjenestetilbudet", "Kari Jurist");

        var forslagstjeneste = new TjenesteforslagTjeneste(db, new KiAgentKlientStub(), new TjenesteregisterTjeneste(db), new ConfigurationBuilder().Build());
        var resultat = await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");

        Assert.Single(resultat.Opprettede);
        Assert.Equal("foreslatt_av_ai", resultat.Opprettede[0].Status);
        Assert.Null(resultat.Melding);
        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetId == resultat.Opprettede[0].Id);
        Assert.Equal("foreslatt_av_ai", proveniens.Handling);
        Assert.NotNull(proveniens.AiForslagVersjon);
        Assert.Contains(rettskildeId.ToString(), proveniens.KildeReferanserJson);
    }

    [Fact]
    public async Task Fungerer_uten_registrerte_kunnskapsbibliotek_lenker()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));

        var forslagstjeneste = new TjenesteforslagTjeneste(db, new KiAgentKlientStub(), new TjenesteregisterTjeneste(db), new ConfigurationBuilder().Build());
        var resultat = await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");

        Assert.Single(resultat.Opprettede);
    }

    [Fact]
    public async Task Ukjent_rettskilde_kastes_ingen_gjettet_fallback()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var forslagstjeneste = new TjenesteforslagTjeneste(db, new KiAgentKlientStub(), new TjenesteregisterTjeneste(db), new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            forslagstjeneste.KjorForslagAsync(virksomhet, [Guid.NewGuid()], "system-ki"));
    }

    /// <summary>Fanger opp kontekst-strengen agenten faktisk fikk, og returnerer et fast, fullt utfylt svar (byggesteg 5 runde 3).</summary>
    private sealed class FangendeKlient : IKiAgentKlient
    {
        public string? SisteKontekst { get; private set; }

        public Task<KiSvar> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default)
        {
            SisteKontekst = kontekst;
            return Task.FromResult(new KiSvar("""
                [{"Tittel": "Testtjeneste", "KortBeskrivelse": "d", "KompetentMyndighet": "Testkommunen",
                  "Output": "Et vedtak", "Tjenestetype": "Bevilling", "Malgruppe": "Virksomheter",
                  "Kanaler": ["digitalt", "fysisk"], "Kostnad": "Gratis", "Behandlingstid": "4 uker",
                  "Kontaktpunkt": "postmottak@testkommunen.no", "KonsekvensVedBrudd": "Inndragning",
                  "Sprak": ["norsk", "engelsk"]}]
                """, InputTokens: 321, OutputTokens: 65));
        }
    }

    [Fact]
    public async Task Kontekst_inkluderer_eid_per_node_og_alle_cpsv_felt_lagres()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));

        var klient = new FangendeKlient();
        var forslagstjeneste = new TjenesteforslagTjeneste(db, klient, new TjenesteregisterTjeneste(db), new ConfigurationBuilder().Build());
        var resultat = await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");

        Assert.Contains("[", klient.SisteKontekst); // eId-tag foran nodetekst, se RettskildeKontekstHjelper

        Assert.Single(resultat.Opprettede);
        Assert.Equal(321, resultat.InputTokens);
        Assert.Equal(65, resultat.OutputTokens);
        var tjeneste = resultat.Opprettede[0];
        Assert.Equal("Testkommunen", tjeneste.KompetentMyndighet);
        Assert.Equal("Et vedtak", tjeneste.Output);
        Assert.Equal("Bevilling", tjeneste.Tjenestetype);
        Assert.Equal("Virksomheter", tjeneste.Malgruppe);
        Assert.Equal(["digitalt", "fysisk"], tjeneste.Kanaler);
        Assert.Equal("Gratis", tjeneste.Kostnad);
        Assert.Equal("4 uker", tjeneste.Behandlingstid);
        Assert.Equal("postmottak@testkommunen.no", tjeneste.Kontaktpunkt);
        Assert.Equal("Inndragning", tjeneste.KonsekvensVedBrudd);
        Assert.Equal(["norsk", "engelsk"], tjeneste.Sprak);
    }
}
