namespace RegelIde.Data.Tests;

/// <summary>Tjenesteeksport (2026-08-20) — samlet JSON-eksport av én tjeneste, mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class TjenesteEksportTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public TjenesteEksportTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<Guid> NyVirksomhetAsync(RegelIdeDbContext db, string navn = "Testkommunen")
    {
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = navn });
        await db.SaveChangesAsync();
        return virksomhet;
    }

    private static async Task<Guid> NyTjenesteAsync(RegelIdeDbContext db, Guid virksomhetId, string tittel)
    {
        var tjeneste = await new TjenesteregisterTjeneste(db).OpprettAsync(
            virksomhetId, tittel, null, null, null, null, null, null, null, null, null, null, null, "Kari Jurist");
        return tjeneste.Id;
    }

    private static TjenesteEksportTjeneste NyEksportTjeneste(RegelIdeDbContext db) => new(
        new TjenesteregisterTjeneste(db), new HendelseregisterTjeneste(db), new TjenesteavhengighetregisterTjeneste(db), db);

    [Fact]
    public async Task Tjeneste_uten_koblinger_gir_tomme_lister_ikke_gjettet_innhold()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var tjenesteId = await NyTjenesteAsync(db, virksomhet, "Serveringsbevilling");

        var eksport = await NyEksportTjeneste(db).EksporterAsync(tjenesteId);

        Assert.NotNull(eksport);
        Assert.Empty(eksport!.Regelverksreferanser);
        Assert.Empty(eksport.Hendelser);
        Assert.Empty(eksport.Avhengigheter);
        Assert.Equal("Testkommunen", eksport.VirksomhetNavn);
    }

    [Fact]
    public async Task Eksport_inkluderer_avhengigheter_i_begge_retninger_inkl_ekstern_referanse()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var serveringsbevilling = await NyTjenesteAsync(db, virksomhet, "Serveringsbevilling");
        var etablererproven = await NyTjenesteAsync(db, virksomhet, "Etablererprøven");

        var avhengighetregister = new TjenesteavhengighetregisterTjeneste(db);
        await avhengighetregister.OpprettAsync(
            virksomhet, serveringsbevilling, etablererproven, "forutsetning_for", null, null, "Kari Jurist");
        await avhengighetregister.OpprettAsync(
            virksomhet, serveringsbevilling, null, "avhengig_av", null,
            "Kan kreves, avhenger av om virksomheten produserer fettholdig avløpsvann.", "Kari Jurist",
            tilOrganisasjonsnummer: "985399077", tilNavn: "Ny næringsmiddelvirksomhet (Mattilsynet)");

        var eksport = await NyEksportTjeneste(db).EksporterAsync(serveringsbevilling);

        Assert.NotNull(eksport);
        Assert.Equal(2, eksport!.Avhengigheter.Count);
        Assert.Contains(eksport.Avhengigheter, a => a.MotpartTjenesteId == etablererproven);
        Assert.Contains(eksport.Avhengigheter, a => a.MotpartOrganisasjonsnummer == "985399077");
    }

    [Fact]
    public async Task Ukjent_tjeneste_gir_null()
    {
        await using var db = _fixture.NyDbContext();
        var eksport = await NyEksportTjeneste(db).EksporterAsync(Guid.NewGuid());
        Assert.Null(eksport);
    }
}
