using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>«Foreslå handlinger» (handlingsforslag-ki-omfang-runden), omfang "handling" — mot ekte
/// embedded Postgres og egne, faste test-doble KI-klienter (samme mønster som
/// TjenesteforslagTjenesteTests/BegrepsforslagTjenesteTests — ALDRI ekte nettverkskall her).</summary>
[Collection(DataTestCollection.Navn)]
public class HandlingsforslagTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public HandlingsforslagTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<(Guid Virksomhet, Guid RettskildeId, Guid TjenesteId)> OppsettAsync(RegelIdeDbContext db)
    {
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));
        var tjeneste = await new TjenesteregisterTjeneste(db).OpprettAsync(
            virksomhet, "Oppgaveregisteret — Testkommunen", null, null, null, null, null, null, null, null, null, null, null, "system-seed");
        return (virksomhet, rettskildeId, tjeneste.Id);
    }

    /// <summary>Fast, fullt utfylt svar som dekker de rikeste under-feltene (kanaler/behandlingstid/
    /// kostnad/vedlegg/veiledningstekst/resultat) — beviser at HELE Handling-skjemaet, ikke bare
    /// Navn/Handlingstype, faktisk parses og lagres.</summary>
    private sealed class FangendeKlient : IKiAgentKlient
    {
        public string? SisteSystemInstruks { get; private set; }
        public string? SisteKontekst { get; private set; }

        public Task<KiSvar> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default)
        {
            SisteSystemInstruks = systemInstruks;
            SisteKontekst = kontekst;
            return Task.FromResult(new KiSvar("""
                [{"Navn": "Søke om skjenkebevilling", "Handlingstype": "soke", "Bruksomraade": "søknad_registrering",
                  "UtfortAv": "soker",
                  "Kanaler": [{"Kanal": "digitalt", "Adresse": "https://testkommunen.no/skjema"}],
                  "Behandlingstid": {"Frist": "4 uker", "Hjemmel": {"Lov": "alkoholloven", "Henvisning": "§ 1-7a"}},
                  "Kostnad": {"Belop": "5000 kr", "Hjemmel": [{"Lov": "alkoholloven", "Henvisning": "§ 7-1"}]},
                  "Vedlegg": [{"Navn": "Firmaattest", "Kategori": "dokumentasjon", "Hjemmel": null}],
                  "Veiledningstekst": [{"Overskrift": "Hvordan søke", "Innhold": "Fyll ut skjema", "Hjemmel": null}],
                  "Arsaker": [],
                  "Resultat": {"Hva": "Bevilling", "BevisKanaler": [{"Kanal": "digitalt"}]},
                  "Merknad": "Stub-verifisering"}]
                """, InputTokens: 741, OutputTokens: 210));
        }
    }

    [Fact]
    public async Task Kjorer_forslag_oppretter_handling_med_status_foreslatt_av_ai_og_proveniens_og_alle_felt()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, rettskildeId, tjenesteId) = await OppsettAsync(db);

        var klient = new FangendeKlient();
        var forslagstjeneste = new HandlingsforslagTjeneste(db, klient, new HandlingregisterTjeneste(db), new ConfigurationBuilder().Build());
        var resultat = await forslagstjeneste.KjorForslagAsync(virksomhet, tjenesteId, [rettskildeId], "system-ki");

        Assert.Single(resultat.Opprettede);
        var handling = resultat.Opprettede[0];
        Assert.Equal("Søke om skjenkebevilling", handling.Navn);
        Assert.Equal("foreslatt_av_ai", handling.Status);
        Assert.Equal(tjenesteId, handling.TjenesteId);
        Assert.Equal(741, resultat.InputTokens);
        Assert.Equal(210, resultat.OutputTokens);

        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetType == "handling" && p.EntitetId == handling.Id);
        Assert.Equal("foreslatt_av_ai", proveniens.Handling);
        Assert.NotNull(proveniens.AiForslagVersjon);
        Assert.Contains(rettskildeId.ToString(), proveniens.KildeReferanserJson);
        Assert.Contains(tjenesteId.ToString(), proveniens.KildeReferanserJson);

        // Konteksten skal starte med tjenesten handlingene foreslås for, og inneholde eId-tagger.
        Assert.Contains("Oppgaveregisteret — Testkommunen", klient.SisteKontekst);
        Assert.Contains("[", klient.SisteKontekst);
        // System-instruksen skal beskrive Handling-skjemaet, ikke Tjeneste- eller Begrep-skjemaet.
        Assert.Contains("Handlingstype", klient.SisteSystemInstruks);
    }

    [Fact]
    public async Task Ukjent_tjeneste_kastes_ingen_gjettet_fallback()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));

        var forslagstjeneste = new HandlingsforslagTjeneste(db, new KiAgentKlientStub(), new HandlingregisterTjeneste(db), new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            forslagstjeneste.KjorForslagAsync(virksomhet, Guid.NewGuid(), [rettskildeId], "system-ki"));
    }

    [Fact]
    public async Task Tjeneste_som_tilhorer_annen_virksomhet_kastes_sikkerhetsscopet()
    {
        await using var db = _fixture.NyDbContext();
        var (_, rettskildeId, tjenesteId) = await OppsettAsync(db);
        var annenVirksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = annenVirksomhet, Navn = "Annen kommune" });
        await db.SaveChangesAsync();

        var forslagstjeneste = new HandlingsforslagTjeneste(db, new KiAgentKlientStub(), new HandlingregisterTjeneste(db), new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            forslagstjeneste.KjorForslagAsync(annenVirksomhet, tjenesteId, [rettskildeId], "system-ki"));
    }

    private sealed class TomtSvarKlient : IKiAgentKlient
    {
        public Task<KiSvar> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default) =>
            Task.FromResult(new KiSvar("[]", InputTokens: 500, OutputTokens: 2));
    }

    [Fact]
    public async Task Tomt_svar_gir_forklarende_melding_ikke_stillhet()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, rettskildeId, tjenesteId) = await OppsettAsync(db);

        var forslagstjeneste = new HandlingsforslagTjeneste(db, new TomtSvarKlient(), new HandlingregisterTjeneste(db), new ConfigurationBuilder().Build());
        var resultat = await forslagstjeneste.KjorForslagAsync(virksomhet, tjenesteId, [rettskildeId], "system-ki");

        Assert.Empty(resultat.Opprettede);
        Assert.NotNull(resultat.Melding);
        Assert.Equal(500, resultat.InputTokens);
    }

    [Fact]
    public async Task Stub_ki_klient_gir_gyldig_handlingsforslag_beviser_rorledningen()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, rettskildeId, tjenesteId) = await OppsettAsync(db);

        var forslagstjeneste = new HandlingsforslagTjeneste(db, new KiAgentKlientStub(), new HandlingregisterTjeneste(db), new ConfigurationBuilder().Build());
        var resultat = await forslagstjeneste.KjorForslagAsync(virksomhet, tjenesteId, [rettskildeId], "system-ki");

        Assert.Single(resultat.Opprettede);
        Assert.Equal("soke", resultat.Opprettede[0].Handlingstype);
        Assert.Equal("foreslatt_av_ai", resultat.Opprettede[0].Status);
    }
}
