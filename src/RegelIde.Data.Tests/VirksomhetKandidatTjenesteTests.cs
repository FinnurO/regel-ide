using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="VirksomhetKandidatTjeneste"/> (docs/20 §2.6) mot ekte embedded Postgres — arbeidskøen
/// for godkjenning av virksomhetsforekomster funnet ved tekstsøk. Selve sveipefunksjonen (tekstsøket)
/// er ikke bygget i denne runden (se klassekommentaren) — testene her dekker køens egen logikk med
/// manuelt konstruerte kandidater.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class VirksomhetKandidatTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public VirksomhetKandidatTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<(Guid RettskildeId, string NodeEid)> OpprettAlkohollovenMedParagrafAsync(RegelIdeDbContext db)
    {
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
        var paragraf = await db.RettskildeNoder.FirstAsync(n => n.RettskildeId == rettskildeId && n.NodeType == "paragraf");
        return (rettskildeId, paragraf.Eid);
    }

    [Fact]
    public async Task Oppretter_kandidat_og_lister_i_ventende()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, nodeEid) = await OpprettAlkohollovenMedParagrafAsync(db);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var register = new VirksomhetKandidatTjeneste(db);
        var kandidat = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, nodeEid, "sveip");

        Assert.Equal("Venter", kandidat.Status);
        var ventende = await register.ListerVentendeAsync(virksomhet.Id);
        Assert.Single(ventende);
    }

    [Fact]
    public async Task Gjentatt_sveip_gir_samme_rad_ikke_duplikat()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, nodeEid) = await OpprettAlkohollovenMedParagrafAsync(db);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var register = new VirksomhetKandidatTjeneste(db);
        var forste = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, nodeEid, "sveip");
        var andre = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, nodeEid, "sveip");

        Assert.Equal(forste.Id, andre.Id);
    }

    [Fact]
    public async Task Avvist_kandidat_dukker_ikke_opp_i_ventende_og_gjenskapes_ikke_av_nytt_sveip()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, nodeEid) = await OpprettAlkohollovenMedParagrafAsync(db);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var register = new VirksomhetKandidatTjeneste(db);
        var kandidat = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, nodeEid, "sveip");
        await register.AvvisAsync(kandidat.Id, "Kari Jurist");

        Assert.Empty(await register.ListerVentendeAsync(virksomhet.Id));

        // Nytt "sveip" på samme (virksomhet, rettskilde, node) skal IKKE gjenskape en Venter-rad.
        var etterNyttSveip = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, nodeEid, "sveip");
        Assert.Equal(kandidat.Id, etterNyttSveip.Id);
        Assert.Equal("Avvist", etterNyttSveip.Status);
    }

    [Fact]
    public async Task Godkjenn_setter_status_og_behandlet_av()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, nodeEid) = await OpprettAlkohollovenMedParagrafAsync(db);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var register = new VirksomhetKandidatTjeneste(db);
        var kandidat = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, nodeEid, "sveip");
        var godkjent = await register.GodkjennAsync(kandidat.Id, "Kari Jurist");

        Assert.NotNull(godkjent);
        Assert.Equal("Godkjent", godkjent!.Status);
        Assert.Equal("Kari Jurist", godkjent.BehandletAv);
        Assert.NotNull(godkjent.BehandletTidspunkt);
    }

    [Fact]
    public async Task Kun_avviste_kan_hardslettes()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, nodeEid) = await OpprettAlkohollovenMedParagrafAsync(db);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var register = new VirksomhetKandidatTjeneste(db);
        var kandidat = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, nodeEid, "sveip");

        await Assert.ThrowsAsync<ArgumentException>(() => register.HardslettAvvistAsync(kandidat.Id));

        await register.AvvisAsync(kandidat.Id, "Kari Jurist");
        Assert.True(await register.HardslettAvvistAsync(kandidat.Id));
    }
}
