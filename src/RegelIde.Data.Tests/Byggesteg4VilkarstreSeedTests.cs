using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>Byggesteg 4 runde 1-testcaseinnhold (2026-07-30, docs/01-referansemodell.md §5.5), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class Byggesteg4VilkarstreSeedTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public Byggesteg4VilkarstreSeedTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task ForberedForutsetningerAsync(RegelIdeDbContext db)
    {
        db.Virksomheter.Add(new Virksomhet { Id = Guid.NewGuid(), Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));
        await Byggesteg2InnholdSeed.SeedAsync(db);
    }

    [Fact]
    public async Task Seeder_hele_treet_fra_referansemodellen_55()
    {
        await using var db = _fixture.NyDbContext();
        await ForberedForutsetningerAsync(db);

        await Byggesteg4VilkarstreSeed.SeedAsync(db);

        var rRoot = await db.Regelnoder.SingleAsync(r => r.Tittel == "Vedtak om skjenkebevilling");
        Assert.True(rRoot.ErRotnode);
        Assert.Equal("OG", rRoot.BarnOperator);

        var rootBarn = await db.RegelnodeBarn.Where(b => b.RegelnodeId == rRoot.Id).ToListAsync();
        Assert.Equal(3, rootBarn.Count); // V-ALDER, V-VANDEL, R-SKJENKETID
        Assert.Equal(2, rootBarn.Count(b => b.BarnType == "vilkar"));
        Assert.Single(rootBarn, b => b.BarnType == "regelnode");

        var rSkjenketidId = rootBarn.Single(b => b.BarnType == "regelnode").BarnId;
        var skjenketidBarn = await db.RegelnodeBarn.Where(b => b.RegelnodeId == rSkjenketidId).ToListAsync();
        Assert.Equal(2, skjenketidBarn.Count); // V-STED, V-KLOKKESLETT
        Assert.All(skjenketidBarn, b => Assert.Equal("vilkar", b.BarnType));

        var unntak = await db.Unntak.SingleAsync(u => u.GjelderRegelId == rSkjenketidId);
        Assert.Equal("vilkar", unntak.BetingelseType);

        var vandel = await db.Vilkar.SingleAsync(v => v.Tittel == "Vandelsvilkår");
        Assert.Equal("skjonnsbasert", vandel.Vurderingstype);
        Assert.NotNull(vandel.SkjonnsgrunnlagBegrepId);
        Assert.Contains("Økonomisk vandel", vandel.SkjonnsmomenterJson);

        var tjeneste = await db.Tjenester.SingleAsync(t => t.Tittel == "Alminnelig skjenkebevilling");
        Assert.Equal(rRoot.Id, tjeneste.RotnodeId);
    }

    [Fact]
    public async Task Seeder_tekst_tagger_som_kobler_vilkar_tilbake_til_lovteksten()
    {
        // 2026-07-30: fiksen på "Vilkår i vilkårstreet uten sporbar kobling tilbake til lovteksten".
        await using var db = _fixture.NyDbContext();
        await ForberedForutsetningerAsync(db);

        await Byggesteg4VilkarstreSeed.SeedAsync(db);

        var vilkarTagger = await db.TekstTagger.Where(t => t.Kind == "vilkar" && t.RefId != null).ToListAsync();
        Assert.Equal(4, vilkarTagger.Count); // V-ALDER, V-VANDEL, V-STED, V-KLOKKESLETT

        var vAlder = await db.Vilkar.SingleAsync(v => v.Tittel == "Aldersvilkår");
        var aldersTagg = Assert.Single(vilkarTagger, t => t.RefId == vAlder.Id);
        Assert.EndsWith("/§1-5/ledd-1", aldersTagg.NodeEid);

        var vVandel = await db.Vilkar.SingleAsync(v => v.Tittel == "Vandelsvilkår");
        Assert.Contains(vilkarTagger, t => t.RefId == vVandel.Id && t.NodeEid.EndsWith("/§1-7b/ledd-1"));
    }

    [Fact]
    public async Task Seeding_er_idempotent()
    {
        await using var db = _fixture.NyDbContext();
        await ForberedForutsetningerAsync(db);

        await Byggesteg4VilkarstreSeed.SeedAsync(db);
        var antallVilkarForste = await db.Vilkar.CountAsync();
        var antallRegelnoderForste = await db.Regelnoder.CountAsync();
        var antallUnntakForste = await db.Unntak.CountAsync();
        var antallTaggerForste = await db.TekstTagger.CountAsync(t => t.Kind == "vilkar" && t.RefId != null);

        await Byggesteg4VilkarstreSeed.SeedAsync(db);

        Assert.Equal(antallVilkarForste, await db.Vilkar.CountAsync());
        Assert.Equal(antallRegelnoderForste, await db.Regelnoder.CountAsync());
        Assert.Equal(antallUnntakForste, await db.Unntak.CountAsync());
        Assert.Equal(antallTaggerForste, await db.TekstTagger.CountAsync(t => t.Kind == "vilkar" && t.RefId != null));
    }
}
