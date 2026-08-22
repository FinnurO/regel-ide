using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="MyndighetstildelingTjeneste"/> (docs/20 §2.5) mot ekte embedded Postgres — kobler et
/// rollebegrep til en konkret virksomhet, hjemlet i en forskrift. Gyldighet ARVES fra hjemmelen —
/// ingen egne datoer på tildelingen selv, se <see cref="MyndighetstildelingTjeneste.ErGjeldendeAsync"/>.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class MyndighetstildelingTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public MyndighetstildelingTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // Alkoholloven importeres idempotent (samme ELI) og deler derfor SAMME rad på tvers av alle tester
    // i denne delte DataTestCollection-databasen — samme "unik streng per test"-begrunnelse som
    // NyKode() i KodelisteregisterTjenesteTests.
    private static string NyTerm(string prefiks) => $"{prefiks}-{Guid.NewGuid():N}";

    private static async Task<(Guid LovId, string ParagrafEid)> OpprettAlkohollovenMedParagrafAsync(RegelIdeDbContext db)
    {
        var lovId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
        var paragraf = await db.RettskildeNoder.FirstAsync(n => n.RettskildeId == lovId && n.NodeType == "paragraf");
        return (lovId, paragraf.Eid);
    }

    [Fact]
    public async Task Oppretter_myndighetstildeling_med_strukturert_paragrafspenn()
    {
        await using var db = _fixture.NyDbContext();
        var (lovId, paragrafEid) = await OpprettAlkohollovenMedParagrafAsync(db);
        var forskrift = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholforskriften(), new DateOnly(2026, 8, 22)));
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var rollebegrep = await new VirksomhetsbegrepTjeneste(db).OpprettRollebegrepAsync(lovId, NyTerm("kontrollmyndighet"), "Kari Jurist");
        var register = new MyndighetstildelingTjeneste(db);
        var tildeling = await register.OpprettAsync(
            rollebegrep.Id, virksomhet.Id, forskrift, [new ParagrafspennPar(paragrafEid, null)], "kommunale avløpsanlegg", "Kari Jurist");

        var lest = MyndighetstildelingTjeneste.LesParagrafspenn(tildeling);
        Assert.Single(lest);
        Assert.Equal(paragrafEid, lest[0].FraEid);
        Assert.Null(lest[0].TilEid);
        Assert.Equal("kommunale avløpsanlegg", tildeling.Vilkaar);

        var forVirksomhet = await register.AlleForVirksomhetAsync(virksomhet.Id);
        Assert.Single(forVirksomhet);
    }

    [Fact]
    public async Task Tomt_paragrafspenn_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var (lovId, _) = await OpprettAlkohollovenMedParagrafAsync(db);
        var forskrift = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholforskriften(), new DateOnly(2026, 8, 22)));
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        var rollebegrep = await new VirksomhetsbegrepTjeneste(db).OpprettRollebegrepAsync(lovId, NyTerm("kontrollmyndighet-tom"), "Kari Jurist");

        var register = new MyndighetstildelingTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(
            () => register.OpprettAsync(rollebegrep.Id, virksomhet.Id, forskrift, [], null, "Kari Jurist"));
    }

    [Fact]
    public async Task Ukjent_eid_i_paragrafspenn_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var (lovId, _) = await OpprettAlkohollovenMedParagrafAsync(db);
        var forskrift = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholforskriften(), new DateOnly(2026, 8, 22)));
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        var rollebegrep = await new VirksomhetsbegrepTjeneste(db).OpprettRollebegrepAsync(lovId, NyTerm("kontrollmyndighet-ukjent"), "Kari Jurist");

        var register = new MyndighetstildelingTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            rollebegrep.Id, virksomhet.Id, forskrift, [new ParagrafspennPar("https://ukjent/eid", null)], null, "Kari Jurist"));
    }

    [Fact]
    public async Task Gyldighet_arves_fra_opphevet_hjemmel()
    {
        await using var db = _fixture.NyDbContext();
        var (lovId, paragrafEid) = await OpprettAlkohollovenMedParagrafAsync(db);
        var forskriftId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholforskriften(), new DateOnly(2026, 8, 22)));
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        var rollebegrep = await new VirksomhetsbegrepTjeneste(db).OpprettRollebegrepAsync(lovId, NyTerm("kontrollmyndighet-opphevet"), "Kari Jurist");

        var register = new MyndighetstildelingTjeneste(db);
        var tildeling = await register.OpprettAsync(
            rollebegrep.Id, virksomhet.Id, forskriftId, [new ParagrafspennPar(paragrafEid, null)], null, "Kari Jurist");

        Assert.True(await register.ErGjeldendeAsync(tildeling));

        var forskrift = await db.Rettskilder.SingleAsync(r => r.Id == forskriftId);
        forskrift.Status = "Opphevet";
        await db.SaveChangesAsync();

        Assert.False(await register.ErGjeldendeAsync(tildeling));
    }
}
