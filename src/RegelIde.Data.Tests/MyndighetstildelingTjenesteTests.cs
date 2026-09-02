using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="MyndighetstildelingTjeneste"/> (docs/20 §2.5, docs/29 §Del B) mot ekte embedded Postgres —
/// kobler et gruppebegrep til en konkret virksomhet, hjemlet i en forskrift. Gyldighet arves fra
/// hjemmelen OG kan avgrenses av tildelingens egne GyldigFra/GyldigTil, se
/// <see cref="MyndighetstildelingTjeneste.ErGjeldendeAsync"/>.
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

        var gruppebegrep = await new VirksomhetsbegrepTjeneste(db).OpprettGruppebegrepAsync(lovId, NyTerm("kontrollmyndighet"), "Kari Jurist");
        var register = new MyndighetstildelingTjeneste(db);
        var tildeling = await register.OpprettAsync(
            gruppebegrep.Id, virksomhet.Id, forskrift, [new ParagrafspennPar(paragrafEid, null)], "kommunale avløpsanlegg", "Kari Jurist");

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
        var gruppebegrep = await new VirksomhetsbegrepTjeneste(db).OpprettGruppebegrepAsync(lovId, NyTerm("kontrollmyndighet-tom"), "Kari Jurist");

        var register = new MyndighetstildelingTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(
            () => register.OpprettAsync(gruppebegrep.Id, virksomhet.Id, forskrift, [], null, "Kari Jurist"));
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
        var gruppebegrep = await new VirksomhetsbegrepTjeneste(db).OpprettGruppebegrepAsync(lovId, NyTerm("kontrollmyndighet-ukjent"), "Kari Jurist");

        var register = new MyndighetstildelingTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            gruppebegrep.Id, virksomhet.Id, forskrift, [new ParagrafspennPar("https://ukjent/eid", null)], null, "Kari Jurist"));
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
        var gruppebegrep = await new VirksomhetsbegrepTjeneste(db).OpprettGruppebegrepAsync(lovId, NyTerm("kontrollmyndighet-opphevet"), "Kari Jurist");

        var register = new MyndighetstildelingTjeneste(db);
        var tildeling = await register.OpprettAsync(
            gruppebegrep.Id, virksomhet.Id, forskriftId, [new ParagrafspennPar(paragrafEid, null)], null, "Kari Jurist");

        Assert.True(await register.ErGjeldendeAsync(tildeling));

        var forskrift = await db.Rettskilder.SingleAsync(r => r.Id == forskriftId);
        forskrift.Status = "Opphevet";
        await db.SaveChangesAsync();

        Assert.False(await register.ErGjeldendeAsync(tildeling));
    }

    /// <summary>
    /// docs/29 §Del B, §Steg 4 punkt 3 — selve motivasjonen for hele mekanismen (Vertskommune-eksempelet
    /// fra docs/28): en tildeling med en EGEN GyldigTil i fortiden skal falle ut, SELV OM hjemmelen selv
    /// fortsatt er fullt gjeldende.
    /// </summary>
    [Fact]
    public async Task GyldigTil_i_fortiden_pa_tildelingen_selv_gjor_den_ikke_gjeldende_selv_om_hjemmelen_er_det()
    {
        await using var db = _fixture.NyDbContext();
        var (lovId, paragrafEid) = await OpprettAlkohollovenMedParagrafAsync(db);
        var forskriftId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholforskriften(), new DateOnly(2026, 8, 22)));
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        var gruppebegrep = await new VirksomhetsbegrepTjeneste(db).OpprettGruppebegrepAsync(lovId, NyTerm("vertskommune-utlopt"), "Kari Jurist");

        var register = new MyndighetstildelingTjeneste(db);
        var tildeling = await register.OpprettAsync(
            gruppebegrep.Id, virksomhet.Id, forskriftId, [new ParagrafspennPar(paragrafEid, null)], null, "Kari Jurist",
            gyldigFra: new DateOnly(2020, 1, 1), gyldigTil: new DateOnly(2021, 12, 31));

        // Hjemmelen selv er urørt/fortsatt gjeldende — kun tildelingens EGEN GyldigTil er utløpt.
        var forskrift = await db.Rettskilder.SingleAsync(r => r.Id == forskriftId);
        Assert.NotEqual("Opphevet", forskrift.Status);

        Assert.False(await register.ErGjeldendeAsync(tildeling));
        Assert.True(await register.ErGjeldendeAsync(tildeling, new DateOnly(2021, 6, 1)));
    }

    /// <summary>docs/29 §Del B punkt 2 — filteret må FAKTISK ekskludere utløpte tildelinger fra
    /// <c>kunGjeldende: true</c>-spørringer, ikke bare finnes som et informativt felt.</summary>
    [Fact]
    public async Task KunGjeldende_filter_ekskluderer_tildeling_med_utlopt_egen_gyldigperiode()
    {
        await using var db = _fixture.NyDbContext();
        var (lovId, paragrafEid) = await OpprettAlkohollovenMedParagrafAsync(db);
        var forskriftId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholforskriften(), new DateOnly(2026, 8, 22)));
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        var gruppebegrep = await new VirksomhetsbegrepTjeneste(db).OpprettGruppebegrepAsync(lovId, NyTerm("vertskommune-filter"), "Kari Jurist");

        var register = new MyndighetstildelingTjeneste(db);
        var utlopt = await register.OpprettAsync(
            gruppebegrep.Id, virksomhet.Id, forskriftId, [new ParagrafspennPar(paragrafEid, null)], null, "Kari Jurist",
            gyldigFra: new DateOnly(2020, 1, 1), gyldigTil: new DateOnly(2021, 12, 31));
        var permanent = await register.OpprettAsync(
            gruppebegrep.Id, virksomhet.Id, forskriftId, [new ParagrafspennPar(paragrafEid, null)], "et annet vilkår", "Kari Jurist");

        var alle = await register.AlleForVirksomhetAsync(virksomhet.Id);
        Assert.Equal(2, alle.Count);

        var kunGjeldende = await register.AlleForVirksomhetAsync(virksomhet.Id, kunGjeldende: true);
        Assert.Single(kunGjeldende);
        Assert.Equal(permanent.Id, kunGjeldende[0].Id);
        Assert.DoesNotContain(kunGjeldende, t => t.Id == utlopt.Id);

        var alleForGruppe = await register.AlleForGruppeBegrepAsync(gruppebegrep.Id, kunGjeldende: true);
        Assert.Single(alleForGruppe);
        Assert.Equal(permanent.Id, alleForGruppe[0].Id);
    }
}
