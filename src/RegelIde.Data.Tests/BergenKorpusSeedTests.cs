using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="BergenKorpusSeed"/> mot ekte embedded Postgres — samme fixture-mønster som
/// <see cref="TestkommuneInnholdSeedTests"/>/<see cref="AgderFylkeskommuneSeedTests"/>. Leser
/// data/kilder/ direkte fra disk (samme sti produksjonskoden selv bruker via Program.cs'
/// <c>RegelIde:Kildemappe</c>-konvensjon), IKKE fra Testdata-kopiene de andre testene bruker — se
/// <see cref="FinnDataKilderRotmappe"/>.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class BergenKorpusSeedTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public BergenKorpusSeedTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Går oppover fra testkjøringens bin-mappe til den finner den ekte <c>data/kilder</c>-mappen
    /// i repoet — samme mappe Program.cs peker på via <c>RegelIde:Kildemappe</c>/ContentRootPath, bare
    /// robust mot testprosjektets varierende bin-dybde i stedet for et hardkodet antall "..".</summary>
    private static string FinnDataKilderRotmappe()
    {
        var katalog = new DirectoryInfo(AppContext.BaseDirectory);
        while (katalog is not null)
        {
            var kandidat = Path.Combine(katalog.FullName, "data", "kilder");
            if (Directory.Exists(kandidat)) return kandidat;
            katalog = katalog.Parent;
        }
        throw new DirectoryNotFoundException("Fant ikke data/kilder i noen foreldermappe av testkjøringen.");
    }

    [Fact]
    public async Task Seeder_virksomhet_lovdata_handboker_og_nettsider()
    {
        await using var db = _fixture.NyDbContext();
        var rotmappe = FinnDataKilderRotmappe();

        await BergenKorpusSeed.SeedAsync(db, rotmappe);

        var bergen = await db.Virksomheter.SingleAsync(v => v.Navn == "Bergen kommune");
        Assert.Equal("4601", bergen.Kommunenummer);
        Assert.Equal("kommune", bergen.Forvaltningsniva);
        // Organisasjonsnummer er den STABILE nøkkelen (docs/15 §3.3, LÅST) — må ikke være null.
        Assert.Equal("964338531", bergen.Organisasjonsnummer);

        // Alkoholloven/alkoholforskriften — delt/nasjonalt (VirksomhetId=null), se BergenKorpusSeed-kommentaren.
        var alkoholloven = await db.Rettskilder.SingleAsync(r => r.Eli == "https://lovdata.no/eli/lov/1989/06/02/27/nor");
        Assert.Null(alkoholloven.VirksomhetId);
        var alkoholforskriften = await db.Rettskilder.SingleAsync(r => r.Eli == "https://lovdata.no/eli/forskrift/2005/06/08/538/nor");
        Assert.Null(alkoholforskriften.VirksomhetId);

        // Håndbok-fixturene — Bergen-scopet.
        var retningslinjer = await db.Rettskilder.SingleAsync(r => r.InterntDokNr == "SD-24-113");
        Assert.Equal(bergen.Id, retningslinjer.VirksomhetId);
        Assert.Equal("Virksomhetsdokument", retningslinjer.Kildetype);
        Assert.True(await db.RettskildeNoder.AnyAsync(n => n.RettskildeId == retningslinjer.Id));

        var forskrift = await db.Rettskilder.SingleAsync(r => r.InterntDokNr == "SD-24-114");
        Assert.Equal(bergen.Id, forskrift.VirksomhetId);
        Assert.Equal("Forskrift", forskrift.Kildetype);

        // Nettsidene — punkt 8 (avklaringsrunde 2026-08-13): 23 RETTSKILDER (Kildetype="Brukerveiledning"),
        // Bergen-scopet, ikke lenger en egen NettsideDokumentEntitet-tabell. Minst én lenke løst helt
        // frem til alkoholloven.
        var antallNettsider = await db.Rettskilder.CountAsync(r => r.VirksomhetId == bergen.Id && r.Kildetype == "Brukerveiledning");
        Assert.Equal(23, antallNettsider);

        var bundling = await db.Rettskilder.SingleAsync(r => r.Kildetype == "Brukerveiledning" && r.Url!.Contains("retningslinjer-for-tildeling"));
        var bundlingSideNode = await db.RettskildeNoder.SingleAsync(n => n.RettskildeId == bundling.Id && n.Eid == "side");
        var lovdatalenke = await db.NettsideLenker.SingleAsync(
            l => l.FraNodeId == bundlingSideNode.Id && l.RaaHref == "https://lovdata.no/dokument/NL/lov/1989-06-02-27");
        Assert.Equal(alkoholloven.Id, lovdatalenke.TilRettskildeId);
    }

    [Fact]
    public async Task Seeding_er_idempotent()
    {
        await using var db = _fixture.NyDbContext();
        var rotmappe = FinnDataKilderRotmappe();

        await BergenKorpusSeed.SeedAsync(db, rotmappe);
        var antallEtterForste = await db.Virksomheter.CountAsync(v => v.Navn == "Bergen kommune");
        var antallNettsiderForste = await db.Rettskilder.CountAsync(r => r.Kildetype == "Brukerveiledning");
        var antallRettskilderForste = await db.Rettskilder.CountAsync();

        await BergenKorpusSeed.SeedAsync(db, rotmappe);
        var antallEtterAndre = await db.Virksomheter.CountAsync(v => v.Navn == "Bergen kommune");
        var antallNettsiderAndre = await db.Rettskilder.CountAsync(r => r.Kildetype == "Brukerveiledning");
        var antallRettskilderAndre = await db.Rettskilder.CountAsync();

        Assert.Equal(1, antallEtterForste);
        Assert.Equal(antallEtterForste, antallEtterAndre);
        Assert.Equal(antallNettsiderForste, antallNettsiderAndre);
        Assert.Equal(antallRettskilderForste, antallRettskilderAndre);
    }
}
