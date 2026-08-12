using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>«Identifiser begrep» (byggesteg 5 runde 1, docs/06-veikart.md), mot ekte embedded Postgres og den ekte <see cref="KiAgentKlientStub"/>.</summary>
[Collection(DataTestCollection.Navn)]
public class BegrepsforslagTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public BegrepsforslagTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Kjorer_forslag_oppretter_begrep_med_status_foreslatt_av_ai_og_proveniens()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));

        var forslagstjeneste = new BegrepsforslagTjeneste(db, new KiAgentKlientStub(), new BegrepsregisterTjeneste(db), new ConfigurationBuilder().Build());
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
    public async Task Ukjent_rettskilde_kastes_ingen_gjettet_fallback()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var forslagstjeneste = new BegrepsforslagTjeneste(db, new KiAgentKlientStub(), new BegrepsregisterTjeneste(db), new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            forslagstjeneste.KjorForslagAsync(virksomhet, [Guid.NewGuid()], "system-ki"));
    }

    [Fact]
    public async Task Ingen_valgte_rettskilder_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var forslagstjeneste = new BegrepsforslagTjeneste(db, new KiAgentKlientStub(), new BegrepsregisterTjeneste(db), new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            forslagstjeneste.KjorForslagAsync(virksomhet, [], "system-ki"));
    }

    /// <summary>Fanget i klient som en ekte chatmodell ofte gjør — se JsonSvarHjelper.StrimleKodeblokk (byggesteg 5 runde 3).</summary>
    private sealed class MarkdownFensetKlient : IKiAgentKlient
    {
        public Task<KiSvar> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default) =>
            // Bevisst et distinkt, oppdiktet term — ikke et ekte lovbegrep som "uklanderlig vandel"
            // (se Byggesteg2InnholdSeed.cs sin GLOBALE term-eksistens-guard, ikke virksomhet-scopet).
            Task.FromResult(new KiSvar(
                "```json\n[{\"Term\": \"testbegrep-markdown-fenset\", \"Definisjon\": \"d\", \"Begrepstype\": \"faktabegrep\", \"LovreferanseEid\": null}]\n```",
                InputTokens: 123, OutputTokens: 45));
    }

    [Fact]
    public async Task Markdown_kodeblokk_rundt_svaret_strimles_og_parses()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));

        var forslagstjeneste = new BegrepsforslagTjeneste(db, new MarkdownFensetKlient(), new BegrepsregisterTjeneste(db), new ConfigurationBuilder().Build());
        var resultat = await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");

        Assert.Single(resultat.Opprettede);
        Assert.Equal("testbegrep-markdown-fenset", resultat.Opprettede[0].Term);
        Assert.Equal(123, resultat.InputTokens);
        Assert.Equal(45, resultat.OutputTokens);
    }

    /// <summary>Returnerer to forslag: ett med en gyldig eId (satt av testen selv, se under) og ett med
    /// en eId ekte modeller er observert å levere live (byggesteg 5 runde 3): en KORTFORM av taggen den
    /// faktisk fikk servert, ikke ordrett identisk med noden — matcher derfor ingen faktisk node.</summary>
    private sealed class DelvisUgyldigEidKlient(string gyldigEid) : IKiAgentKlient
    {
        public Task<KiSvar> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default) =>
            Task.FromResult(new KiSvar((
                """[{"Term": "gyldig-referert-begrep", "Definisjon": "d1", "Begrepstype": "faktabegrep", "LovreferanseEid": "GYLDIG_EID"}, """ +
                """{"Term": "hallusinert-referert-begrep", "Definisjon": "d2", "Begrepstype": "faktabegrep", "LovreferanseEid": "§ikke-en-faktisk-node"}]"""
                ).Replace("GYLDIG_EID", gyldigEid), InputTokens: null, OutputTokens: null));
    }

    [Fact]
    public async Task Hallusinert_eId_dropper_kun_den_referansen_ikke_hele_batchen()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));
        var gyldigEid = (await db.RettskildeNoder.FirstAsync(n => n.RettskildeId == rettskildeId)).Eid;

        var forslagstjeneste = new BegrepsforslagTjeneste(
            db, new DelvisUgyldigEidKlient(gyldigEid), new BegrepsregisterTjeneste(db), new ConfigurationBuilder().Build());
        var resultat = await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");

        Assert.Equal(2, resultat.Opprettede.Count);
        var gyldig = resultat.Opprettede.Single(b => b.Term == "gyldig-referert-begrep");
        var hallusinert = resultat.Opprettede.Single(b => b.Term == "hallusinert-referert-begrep");
        Assert.Equal(gyldigEid, gyldig.LovreferanseEid);
        Assert.Null(hallusinert.LovreferanseEid);
    }

    /// <summary>Svarer med en tom array — gyldig KI-respons (agenten fant ingenting), men skal gi en
    /// forklarende melding, ikke bare stillhet UI-et ellers ikke kan skille fra en feil (byggesteg 5
    /// runde 3, observert live: en ekte modell svarte "[]" på 49k input-tokens kontekst).</summary>
    private sealed class TomtSvarKlient : IKiAgentKlient
    {
        public Task<KiSvar> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default) =>
            Task.FromResult(new KiSvar("[]", InputTokens: 49123, OutputTokens: 2));
    }

    [Fact]
    public async Task Tomt_svar_gir_forklarende_melding_og_token_forbruk_ikke_stillhet()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));

        var forslagstjeneste = new BegrepsforslagTjeneste(db, new TomtSvarKlient(), new BegrepsregisterTjeneste(db), new ConfigurationBuilder().Build());
        var resultat = await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");

        Assert.Empty(resultat.Opprettede);
        Assert.NotNull(resultat.Melding);
        Assert.Equal(49123, resultat.InputTokens);
        Assert.Equal(2, resultat.OutputTokens);
    }

    /// <summary>R0 (docs/13-backlog.md §4 punkt 7) — svarer med et tomt array på det FØRSTE kallet,
    /// et rikt svar på det ANDRE — beviser at KiForslagRetryHjelper faktisk gjør ett nytt kall med
    /// SAMME kontekst (fanget og sammenlignet her) i stedet for bare å gi opp på første tomme svar.</summary>
    private sealed class TomtSaRiktSvarKlient : IKiAgentKlient
    {
        private int _kall;
        public int AntallKall => _kall;
        public string? FoersteKontekst { get; private set; }
        public string? AndreKontekst { get; private set; }

        public Task<KiSvar> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default)
        {
            _kall++;
            if (_kall == 1)
            {
                FoersteKontekst = kontekst;
                return Task.FromResult(new KiSvar("[]", InputTokens: 100, OutputTokens: 1));
            }
            AndreKontekst = kontekst;
            return Task.FromResult(new KiSvar(
                """[{"Term": "begrep-fra-retry", "Definisjon": "d", "Begrepstype": "faktabegrep"}]""",
                InputTokens: 100, OutputTokens: 5));
        }
    }

    [Fact]
    public async Task Tomt_forste_svar_gir_ett_automatisk_retry_med_samme_kontekst_som_lykkes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));

        var klient = new TomtSaRiktSvarKlient();
        var forslagstjeneste = new BegrepsforslagTjeneste(db, klient, new BegrepsregisterTjeneste(db), new ConfigurationBuilder().Build());
        var resultat = await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");

        Assert.Equal(2, klient.AntallKall);
        Assert.Equal(klient.FoersteKontekst, klient.AndreKontekst); // samme kontekst, ikke en endret prompt
        Assert.Single(resultat.Opprettede);
        Assert.Equal("begrep-fra-retry", resultat.Opprettede[0].Term);
        Assert.Null(resultat.Melding);
    }

    /// <summary>Alltid tomt array — beviser at retry-en gir opp etter ETT ekstra forsøk (ikke en
    /// uendelig løkke) og returnerer et tomt, men gyldig, resultat i stedet for å kaste en feil.</summary>
    private sealed class AlltidTomtSvarKlient : IKiAgentKlient
    {
        public int AntallKall { get; private set; }

        public Task<KiSvar> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default)
        {
            AntallKall++;
            return Task.FromResult(new KiSvar("[]", InputTokens: 200, OutputTokens: 2));
        }
    }

    [Fact]
    public async Task Tomt_svar_ogsa_etter_retry_gir_tomt_resultat_kaster_ikke_og_ringer_nettopp_to_ganger()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));

        var klient = new AlltidTomtSvarKlient();
        var forslagstjeneste = new BegrepsforslagTjeneste(db, klient, new BegrepsregisterTjeneste(db), new ConfigurationBuilder().Build());
        var resultat = await forslagstjeneste.KjorForslagAsync(virksomhet, [rettskildeId], "system-ki");

        Assert.Equal(2, klient.AntallKall); // nøyaktig ett retry, ikke en uendelig løkke
        Assert.Empty(resultat.Opprettede);
        Assert.NotNull(resultat.Melding);
    }
}
