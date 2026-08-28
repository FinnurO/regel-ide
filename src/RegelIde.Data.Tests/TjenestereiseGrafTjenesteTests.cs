namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, 2026-08-28, import-wizard/tjenestereise-graf-runden] Multi-hop BFS-traversering — dekker
/// dybdegrense, loop-sikkerhet på en ekte syklisk graf, livshendelse-filter, og handling-inkludering.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class TjenestereiseGrafTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public TjenestereiseGrafTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static TjenestereiseGrafTjeneste NyGraftjeneste(RegelIdeDbContext db) =>
        new(db, new TjenesteavhengighetregisterTjeneste(db), new HandlingTjenesteregisterTjeneste(db));

    private static async Task<Guid> NyVirksomhetAsync(RegelIdeDbContext db)
    {
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        return virksomhet;
    }

    private static async Task<Guid> NyTjenesteAsync(RegelIdeDbContext db, Guid virksomhetId, string tittel, IReadOnlyList<string>? livshendelser = null)
    {
        var t = await new TjenesteregisterTjeneste(db).OpprettAsync(
            virksomhetId, tittel, null, null, null, null, null, null, null, null, null, null, null, "Kari Jurist",
            livshendelser: livshendelser);
        return t.Id;
    }

    [Fact]
    public async Task Dybde_1_gir_kun_direkte_naboer()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var a = await NyTjenesteAsync(db, virksomhet, "A");
        var b = await NyTjenesteAsync(db, virksomhet, "B");
        var c = await NyTjenesteAsync(db, virksomhet, "C");
        var avhengighet = new TjenesteavhengighetregisterTjeneste(db);
        await avhengighet.OpprettAsync(virksomhet, a, b, "forutsetning_for", null, null, "Kari Jurist");
        await avhengighet.OpprettAsync(virksomhet, b, c, "forutsetning_for", null, null, "Kari Jurist");

        var graf = await NyGraftjeneste(db).ByggAsync(a, dybde: 1, inkluderHandlinger: false, livshendelseFilter: null);

        Assert.NotNull(graf);
        Assert.Equal(2, graf!.Noder.Count); // A + B, ikke C
        Assert.DoesNotContain(graf.Noder, n => n.Id == c);
    }

    [Fact]
    public async Task Dybde_2_naar_to_hopp_unna()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var a = await NyTjenesteAsync(db, virksomhet, "A");
        var b = await NyTjenesteAsync(db, virksomhet, "B");
        var c = await NyTjenesteAsync(db, virksomhet, "C");
        var avhengighet = new TjenesteavhengighetregisterTjeneste(db);
        await avhengighet.OpprettAsync(virksomhet, a, b, "forutsetning_for", null, null, "Kari Jurist");
        await avhengighet.OpprettAsync(virksomhet, b, c, "forutsetning_for", null, null, "Kari Jurist");

        var graf = await NyGraftjeneste(db).ByggAsync(a, dybde: 2, inkluderHandlinger: false, livshendelseFilter: null);

        Assert.NotNull(graf);
        Assert.Equal(3, graf!.Noder.Count);
        Assert.Contains(graf.Noder, n => n.Id == c);
    }

    [Fact]
    public async Task Diamant_konvergens_gir_noden_kun_en_gang()
    {
        // Skriveveien (TjenesteavhengighetregisterTjeneste.LukkerSykelAsync) forhindrer ekte sykler —
        // en diamant (A->B->D og A->C->D, to stier til SAMME node) er derimot fullt lovlig og en
        // reell test av at BFS-ens besøkt-sett dedupliserer riktig uten å telle D to ganger.
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var a = await NyTjenesteAsync(db, virksomhet, "A");
        var b = await NyTjenesteAsync(db, virksomhet, "B");
        var c = await NyTjenesteAsync(db, virksomhet, "C");
        var d = await NyTjenesteAsync(db, virksomhet, "D");
        var avhengighet = new TjenesteavhengighetregisterTjeneste(db);
        await avhengighet.OpprettAsync(virksomhet, a, b, "forutsetning_for", null, null, "Kari Jurist");
        await avhengighet.OpprettAsync(virksomhet, a, c, "gir_mulighet_til", null, null, "Kari Jurist");
        await avhengighet.OpprettAsync(virksomhet, b, d, "forutsetning_for", null, null, "Kari Jurist");
        await avhengighet.OpprettAsync(virksomhet, c, d, "gir_mulighet_til", null, null, "Kari Jurist");

        var graf = await NyGraftjeneste(db).ByggAsync(a, dybde: 5, inkluderHandlinger: false, livshendelseFilter: null);

        Assert.NotNull(graf);
        Assert.Equal(4, graf!.Noder.Count); // A, B, C, D — D KUN én gang
        Assert.Equal(graf.Noder.Select(n => n.Id).Distinct().Count(), graf.Noder.Count);
        Assert.Equal(4, graf.Kanter.Count);
    }

    [Fact]
    public async Task Livshendelse_filter_beholder_sentrum_men_fjerner_ikke_matchende_naboer()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var a = await NyTjenesteAsync(db, virksomhet, "A"); // ingen livshendelse
        var b = await NyTjenesteAsync(db, virksomhet, "B", ["Gifte seg"]);
        var c = await NyTjenesteAsync(db, virksomhet, "C", ["Noe annet"]);
        var avhengighet = new TjenesteavhengighetregisterTjeneste(db);
        await avhengighet.OpprettAsync(virksomhet, a, b, "forutsetning_for", null, null, "Kari Jurist");
        await avhengighet.OpprettAsync(virksomhet, a, c, "gir_mulighet_til", null, null, "Kari Jurist");

        var graf = await NyGraftjeneste(db).ByggAsync(a, dybde: 1, inkluderHandlinger: false, livshendelseFilter: "Gifte seg");

        Assert.NotNull(graf);
        Assert.Contains(graf!.Noder, n => n.Id == a); // sentrum alltid med
        Assert.Contains(graf.Noder, n => n.Id == b);
        Assert.DoesNotContain(graf.Noder, n => n.Id == c);
    }

    [Fact]
    public async Task InkluderHandlinger_legger_pa_handling_noder_med_har_handling_kant()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var a = await NyTjenesteAsync(db, virksomhet, "A");
        await new HandlingregisterTjeneste(db).OpprettAsync(
            virksomhet, a, "Søke", "soke", null, "soker", null, null, null, null, null, null, null, null, "Kari Jurist");

        var graf = await NyGraftjeneste(db).ByggAsync(a, dybde: 1, inkluderHandlinger: true, livshendelseFilter: null);

        Assert.NotNull(graf);
        Assert.Contains(graf!.Noder, n => n.ErHandling && n.Navn == "Søke");
        Assert.Contains(graf.Kanter, k => k.ErHandlingTilhorighet && k.Rel == "har_handling");
    }

    [Fact]
    public async Task Ukjent_tjeneste_gir_null()
    {
        await using var db = _fixture.NyDbContext();
        var graf = await NyGraftjeneste(db).ByggAsync(Guid.NewGuid(), 2, false, null);
        Assert.Null(graf);
    }
}
