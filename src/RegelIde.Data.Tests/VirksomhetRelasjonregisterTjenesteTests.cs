using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="VirksomhetRelasjonregisterTjeneste"/> (docs/28, docs/29 §Del C) mot ekte embedded Postgres.
/// Merk: <see cref="RelasjonsTypeKonfigurasjonEntitet"/> seedes normalt ved API-oppstart (Program.cs) —
/// denne fixturen kjører KUN migrasjoner, ingen seed, så hver test som trenger en gyldig relasjonstype
/// setter den selv opp via <see cref="NyRelasjonsTypeAsync"/>.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class VirksomhetRelasjonregisterTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public VirksomhetRelasjonregisterTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<Guid> NyVirksomhetAsync(RegelIdeDbContext db, string navn = "Testkommunen")
    {
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"{navn}-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        return virksomhet.Id;
    }

    /// <summary>Samme som <see cref="NyVirksomhetAsync"/>, men returnerer også det faktiske (unike, Guid-
    /// suffikserte) navnet — nødvendig når testen selv skal assertere på visningstekst som inneholder navnet.</summary>
    private static async Task<(Guid Id, string Navn)> NyVirksomhetMedNavnAsync(RegelIdeDbContext db, string navn)
    {
        var unikNavn = $"{navn}-{Guid.NewGuid():N}";
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = unikNavn };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        return (virksomhet.Id, unikNavn);
    }

    private static async Task<string> NyRelasjonsTypeAsync(
        RegelIdeDbContext db, string? kode = null, string fraMal = "er underlagt {0}", string tilMal = "er eier/overordnet for {0}", bool aktiv = true)
    {
        kode ??= $"type-{Guid.NewGuid():N}";
        db.RelasjonsTypeKonfigurasjoner.Add(new RelasjonsTypeKonfigurasjonEntitet
        {
            Id = Guid.NewGuid(), Kode = kode, FraVisningsmal = fraMal, TilVisningsmal = tilMal, Aktiv = aktiv,
        });
        await db.SaveChangesAsync();
        return kode;
    }

    [Fact]
    public async Task Oppretter_relasjon_med_proveniens()
    {
        await using var db = _fixture.NyDbContext();
        var fra = await NyVirksomhetAsync(db, "Lokal merkenemnd");
        var til = await NyVirksomhetAsync(db, "Statsforvalteren");
        var type = await NyRelasjonsTypeAsync(db, "underlagt-provtest");

        var register = new VirksomhetRelasjonregisterTjeneste(db);
        var relasjon = await register.OpprettAsync(fra, til, type, null, null, null, "Kari Jurist");

        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetId == relasjon.Id);
        Assert.Equal("opprettet", proveniens.Handling);
        Assert.Equal("gjeldende", relasjon.Entitetsstatus);
    }

    [Fact]
    public async Task Kan_ikke_ha_relasjon_til_seg_selv()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var type = await NyRelasjonsTypeAsync(db);

        var register = new VirksomhetRelasjonregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(virksomhet, virksomhet, type, null, null, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Ukjent_relasjonstype_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var fra = await NyVirksomhetAsync(db);
        var til = await NyVirksomhetAsync(db);

        var register = new VirksomhetRelasjonregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(fra, til, "ukjent_type", null, null, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Inaktiv_relasjonstype_avvises_som_ukjent()
    {
        await using var db = _fixture.NyDbContext();
        var fra = await NyVirksomhetAsync(db);
        var til = await NyVirksomhetAsync(db);
        var type = await NyRelasjonsTypeAsync(db, aktiv: false);

        var register = new VirksomhetRelasjonregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(fra, til, type, null, null, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Duplikat_samme_fra_til_type_avvises()
    {
        await using var db = _fixture.NyDbContext();
        var fra = await NyVirksomhetAsync(db);
        var til = await NyVirksomhetAsync(db);
        var type = await NyRelasjonsTypeAsync(db);

        var register = new VirksomhetRelasjonregisterTjeneste(db);
        await register.OpprettAsync(fra, til, type, null, null, null, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(fra, til, type, null, null, null, "Kari Jurist"));
    }

    /// <summary>
    /// docs/29 §Del C, §Steg 7 punkt 3 — selve poenget hentet fra OverordnetEnhetId-bug-lærdommen i
    /// docs/28: SAMME lagrede rad skal gi ULIK visningstekst avhengig av hvilken virksomhet man spør
    /// fra, ikke bare antas riktig fordi mønsteret er kopiert fra Tjenesteavhengighet.
    /// </summary>
    [Fact]
    public async Task Samme_rad_gir_ulik_visningstekst_fra_og_til_siden()
    {
        await using var db = _fixture.NyDbContext();
        var (merkenemnd, merkenemndNavn) = await NyVirksomhetMedNavnAsync(db, "Lokal merkenemnd");
        var (statsforvalteren, statsforvalterenNavn) = await NyVirksomhetMedNavnAsync(db, "Statsforvalteren");
        var type = await NyRelasjonsTypeAsync(db, "sekretariat-visningtest", "har sekretariat hos {0}", "er sekretariat for {0}");

        var register = new VirksomhetRelasjonregisterTjeneste(db);
        await register.OpprettAsync(merkenemnd, statsforvalteren, type, null, null, null, "Kari Jurist");

        var fraSiden = await register.HentForVirksomhetAsync(merkenemnd);
        var visningFra = Assert.Single(fraSiden);
        Assert.Equal("fra", visningFra.Retning);
        Assert.Equal($"har sekretariat hos {statsforvalterenNavn}", visningFra.Visningstekst);
        Assert.Equal(statsforvalteren, visningFra.MotpartVirksomhetId);

        var tilSiden = await register.HentForVirksomhetAsync(statsforvalteren);
        var visningTil = Assert.Single(tilSiden);
        Assert.Equal("til", visningTil.Retning);
        Assert.Equal($"er sekretariat for {merkenemndNavn}", visningTil.Visningstekst);
        Assert.Equal(merkenemnd, visningTil.MotpartVirksomhetId);
    }

    [Fact]
    public async Task Sletter_relasjon()
    {
        await using var db = _fixture.NyDbContext();
        var fra = await NyVirksomhetAsync(db);
        var til = await NyVirksomhetAsync(db);
        var type = await NyRelasjonsTypeAsync(db);

        var register = new VirksomhetRelasjonregisterTjeneste(db);
        var relasjon = await register.OpprettAsync(fra, til, type, null, null, null, "Kari Jurist");

        Assert.True(await register.SlettAsync(relasjon.Id));
        Assert.False(await db.VirksomhetRelasjoner.AnyAsync(r => r.Id == relasjon.Id));
        Assert.False(await register.SlettAsync(relasjon.Id));
    }
}
