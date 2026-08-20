using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// Seed for Rettighet/Handling-modellrunden (2026-08-20), mot ekte embedded Postgres. Samme delte
/// DataTestCollection-database som <see cref="OrganisasjonsregisterSeedTests"/>/
/// <see cref="BergenKorpusSeedTests"/> — bygger derfor opp forutsetningene via de SAMME ekte,
/// idempotente produksjonsseedene (<see cref="BergenKorpusSeed"/>, <see cref="FasitRunde4Seed"/>) i
/// stedet for å hånd-opprette "Bergen kommune"/"Testkommunen" på nytt, som ville brutt andre
/// testklassers egne unikhets-antakelser (samme fragilitet <c>OrganisasjonsregisterSeedTests</c>s
/// klassekommentar advarer mot).
/// </summary>
[Collection(DataTestCollection.Navn)]
public class ServeringsbevillingModellSeedTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public ServeringsbevillingModellSeedTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Samme robuste opp-søk som <see cref="BergenKorpusSeedTests"/>/<see cref="OrganisasjonsregisterSeedTests"/> bruker.</summary>
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

    private static async Task<(Virksomhet Testkommunen, Guid Serveringsbevilling, Virksomhet Bergen)> ForutsetningerAsync(RegelIdeDbContext db)
    {
        await BergenKorpusSeed.SeedAsync(db, FinnDataKilderRotmappe());

        if (!await db.Virksomheter.AnyAsync(v => v.Navn == "Testkommunen"))
        {
            db.Virksomheter.Add(new Virksomhet { Id = Guid.NewGuid(), Navn = "Testkommunen", OpprettetTidspunkt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        // FasitRunde4Seed forutsetter hele kjeden Program.cs kjører før den (samme rekkefølge som
        // OrganisasjonsregisterSeedTests.ForberedEksisterendeVirksomheterAsync bruker) — uten denne
        // kjeden no-oper FasitRunde4Seed stille (bekreftet: "Serveringsbevilling" ble aldri opprettet
        // i en isolert kjøring av denne testklassen alene, uten resten av kjeden).
        await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));
        await Byggesteg2InnholdSeed.SeedAsync(db); // oppretter "Alminnelig skjenkebevilling".
        await Byggesteg4VilkarstreSeed.SeedAsync(db); // krever raden over — bygger rotnode + Vandelsvilkår.
        await KommunaleParametreSeed.SeedAsync(db);
        await FasitRunde4Seed.SeedAsync(db); // oppretter "Serveringsbevilling" (blant 13 andre), krever kjeden over.

        // Finn "Serveringsbevilling" FØRST, og AVLED Testkommunen fra DEN radens eget VirksomhetId —
        // ikke et uavhengig navn-oppslag ved siden av. FasitRunde4Seed gjør sitt EGET interne
        // FirstOrDefaultAsync-oppslag på "Testkommunen"; hvis flere rader med det navnet finnes i
        // denne delte DataTestCollection-databasen (andre testklasser oppretter også "Testkommunen"),
        // er det INGEN garanti at et uavhengig oppslag her lander på nøyaktig samme rad som
        // FasitRunde4Seed selv brukte. Å avlede fra Serveringsbevilling.VirksomhetId gjør de
        // garantert konsistente.
        //
        // MEN: "Tittel == Serveringsbevilling" er IKKE nok til å plukke riktig rad i den fulle
        // Data.Tests-kjøringen — flere HELT ANDRE testklasser (TjenesteavhengighetregisterTjeneste-
        // Tests, TjenesteEksportTjenesteTests, TjenesteregisterTjenesteTests, HandlingregisterTjeneste-
        // Tests) oppretter SINE EGNE "Serveringsbevilling"-rader under egne "Testkommunen"-virksomheter,
        // helt uavhengig av FasitRunde4Seed. Et uskopet FirstAsync landet derfor på en tilfeldig av
        // disse i full-suite-kjøring (bekreftet empirisk: isolert klassekjøring — 3/3 grønt — men
        // 2/3 rødt i full Data.Tests-kjøring, med tomme/0-resultater — fordi ServeringsbevillingModell-
        // Seed sitt EGET interne oppslag (skopet på riktig Testkommunen) traff en ANNEN rad enn den
        // testen her hadde fanget opp). Fasit-raden er den ENESTE som har
        // KompetentMyndighet == "Testkommunen" satt (FasitRunde4Seed.RelevanteTjenester-løkken) — de
        // andre testenes rader lar dette feltet stå null. Bruk det som ekstra filter.
        var serveringsbevilling = await db.Tjenester.FirstAsync(
            t => t.Tittel == "Serveringsbevilling" && t.KompetentMyndighet == "Testkommunen");
        var testkommunen = await db.Virksomheter.SingleAsync(v => v.Id == serveringsbevilling.VirksomhetId);
        var bergen = await db.Virksomheter.FirstAsync(v => v.Navn == "Bergen kommune");

        return (testkommunen, serveringsbevilling.Id, bergen);
    }

    [Fact]
    public async Task Seeder_handlinger_under_serveringsbevilling_og_fettutskiller()
    {
        await using var db = _fixture.NyDbContext();
        var (testkommunen, serveringsbevillingId, bergen) = await ForutsetningerAsync(db);

        await ServeringsbevillingModellSeed.SeedAsync(db);

        var serveringsbevilling = await db.Tjenester.SingleAsync(t => t.Id == serveringsbevillingId);
        Assert.Equal(4, serveringsbevilling.Malgruppe.Count);
        Assert.Equal(["Starte og drive en bedrift"], serveringsbevilling.Livshendelser);
        Assert.Equal("Næring, salg og servering", serveringsbevilling.Tjenesteomrade);
        Assert.NotNull(serveringsbevilling.KonsekvensVedBrudd);

        var handlingerUnderServering = await db.Handlinger.Where(h => h.TjenesteId == serveringsbevilling.Id).ToListAsync();
        Assert.Equal(9, handlingerUnderServering.Count);

        var fettutskiller = await db.Tjenester.SingleAsync(t => t.Tittel == "Krav om fettutskiller" && t.VirksomhetId == bergen.Id);
        Assert.Equal(bergen.Id, fettutskiller.VirksomhetId);

        var handlingerUnderFettutskiller = await db.Handlinger.Where(h => h.TjenesteId == fettutskiller.Id).ToListAsync();
        Assert.Equal(5, handlingerUnderFettutskiller.Count);
    }

    [Fact]
    public async Task Kjort_to_ganger_gir_samme_antall_rader_idempotent()
    {
        await using var db = _fixture.NyDbContext();
        await ForutsetningerAsync(db);

        await ServeringsbevillingModellSeed.SeedAsync(db);
        var antallForste = await db.Handlinger.CountAsync();

        await ServeringsbevillingModellSeed.SeedAsync(db);
        var antallAndre = await db.Handlinger.CountAsync();

        Assert.Equal(antallForste, antallAndre);
    }

    [Fact]
    public async Task Ny_avhengighet_til_fettutskiller_og_kunngjoring_av_konkurs_opprettes()
    {
        await using var db = _fixture.NyDbContext();
        var (_, serveringsbevillingId, _) = await ForutsetningerAsync(db);

        await ServeringsbevillingModellSeed.SeedAsync(db);

        var avhengigheter = await new TjenesteavhengighetregisterTjeneste(db).HentForTjenesteAsync(serveringsbevillingId);

        Assert.Contains(avhengigheter, a => a.MotpartNavn == "Krav om fettutskiller" && a.Rel == "avhengig_av");
        Assert.Contains(avhengigheter, a => a.Rel == "kan_miste" && a.MotpartOrganisasjonsnummer == "974760673");
    }
}
