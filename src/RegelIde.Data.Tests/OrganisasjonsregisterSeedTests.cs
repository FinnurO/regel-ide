using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="OrganisasjonsregisterSeed"/> mot ekte embedded Postgres — samme delte
/// DataTestCollection-database som <see cref="AgderFylkeskommuneSeedTests"/>/
/// <see cref="KommunaleParametreSeedTests"/>/<see cref="BergenKorpusSeedTests"/>. Kjører derfor de
/// SAMME idempotente forutsetningsseedene disse bruker (se <see cref="ForberedEksisterendeVirksomheterAsync"/>)
/// i stedet for å opprette egne rader med de samme navnene — å opprette en ANDRE "Agder fylkeskommune"-
/// eller "Bergen kommune"-rad her ville brutt disse andre testklassenes egne <c>Single</c>-oppslag/
/// interne "finnes fra før"-guarder, avhengig av kjørerekkefølge. Bruker den ekte kildefilen
/// (Seed/organisasjoner-norge.json, kopiert til testens bin-mappe via csproj-en) — ingen egen liten
/// test-JSON, siden hele poenget er å verifisere ekte backfill mot de faktiske 3 navngitte radene.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class OrganisasjonsregisterSeedTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public OrganisasjonsregisterSeedTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Samme robuste opp-søk som <see cref="BergenKorpusSeedTests"/> bruker for å finne den ekte data/kilder-mappen.</summary>
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

    /// <summary>
    /// Samme forutsetningskjede som <see cref="KommunaleParametreSeedTests"/> bruker for Tønsberg/Bærum,
    /// pluss Agder og Bergen — hvert ledd er idempotent/guardet på et stabilt navn, derfor trygt å kalle
    /// fra flere testklasser i samme delte database uansett rekkefølge.
    /// </summary>
    private static async Task ForberedEksisterendeVirksomheterAsync(RegelIdeDbContext db)
    {
        await AgderFylkeskommuneSeed.SeedAsync(db);
        await BergenKorpusSeed.SeedAsync(db, FinnDataKilderRotmappe());

        if (!await db.Virksomheter.AnyAsync(v => v.Navn == "Testkommunen"))
        {
            db.Virksomheter.Add(new Virksomhet { Id = Guid.NewGuid(), Navn = "Testkommunen" });
            await db.SaveChangesAsync();
        }

        await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));
        await Byggesteg2InnholdSeed.SeedAsync(db);
        await Byggesteg4VilkarstreSeed.SeedAsync(db);
        await KommunaleParametreSeed.SeedAsync(db); // oppretter Tønsberg/Bærum uten organisasjonsnummer.
    }

    [Fact]
    public async Task Backfiller_organisasjonsnummer_forvaltningsniva_og_aktiv_pa_eksisterende_rader()
    {
        await using var db = _fixture.NyDbContext();
        await ForberedEksisterendeVirksomheterAsync(db);

        await OrganisasjonsregisterSeed.SeedAsync(db);

        var agder = await db.Virksomheter.SingleAsync(v => v.Navn == "Agder fylkeskommune");
        Assert.Equal("921707134", agder.Organisasjonsnummer);
        Assert.Equal("fylke", agder.Forvaltningsniva);
        Assert.True(agder.Aktiv); // Johanns eksplisitte instruks — alltid aktiv.

        var barum = await db.Virksomheter.SingleAsync(v => v.Navn == "Bærum kommune");
        Assert.Equal("935478715", barum.Organisasjonsnummer);
        Assert.Equal("kommune", barum.Forvaltningsniva);
        Assert.False(barum.Aktiv);

        var tonsberg = await db.Virksomheter.SingleAsync(v => v.Navn == "Tønsberg kommune");
        Assert.Equal("921383681", tonsberg.Organisasjonsnummer);
        Assert.Equal("kommune", tonsberg.Forvaltningsniva);
        Assert.False(tonsberg.Aktiv);

        // Bergen kommune har allerede organisasjonsnummer/kommunenummer/forvaltningsniva fra
        // BergenKorpusSeed — seeden skal kun tvinge Aktiv, ALDRI overskrive de andre (kun NULL-backfill).
        // FirstAsync, ikke SingleAsync — samme defensive valg som Testkommunen-oppslaget over: denne
        // delte DataTestCollection-databasen kan i praksis se mer enn én "Bergen kommune"-rad avhengig
        // av hvilke andre testklasser (nå: ServeringsbevillingModellSeedTests) som også kaller
        // BergenKorpusSeed i samme kjøring.
        var bergen = await db.Virksomheter.FirstAsync(v => v.Navn == "Bergen kommune");
        Assert.Equal("964338531", bergen.Organisasjonsnummer);
        Assert.Equal("4601", bergen.Kommunenummer);
        Assert.Equal("kommune", bergen.Forvaltningsniva);
        Assert.True(bergen.Aktiv); // Johanns eksplisitte instruks — alltid aktiv.

        var testkommunen = await db.Virksomheter.FirstAsync(v => v.Navn == "Testkommunen");
        Assert.True(testkommunen.Aktiv); // fiktiv, ikke i kildefilen — defensivt eksplisitt satt uansett.

        // En reell, sovende kommune fra kildefilen — opprettet av seeden, IKKE aktiv (Johann ba kun om
        // Bergen/Agder/Testkommunen), og uten oppfunnet kommunenummer (kildefilen har ikke det feltet).
        var oslo = await db.Virksomheter.SingleAsync(v => v.Navn == "Oslo kommune");
        Assert.Equal("958935420", oslo.Organisasjonsnummer);
        Assert.Equal("kommune", oslo.Forvaltningsniva);
        Assert.Null(oslo.Kommunenummer);
        Assert.False(oslo.Aktiv);

        // Andre organisasjonstyper i kildefilen (STAT/ORGL/SF/STI/FLI/AS/ANNA/SÆR) hoppes bevisst over.
        Assert.False(await db.Virksomheter.AnyAsync(v => v.Navn.Contains("Statsforvalteren")));
    }

    [Fact]
    public async Task Seeding_er_idempotent_og_klobrer_ikke_en_manuelt_aktivert_rad()
    {
        await using var db = _fixture.NyDbContext();
        await ForberedEksisterendeVirksomheterAsync(db);

        await OrganisasjonsregisterSeed.SeedAsync(db);
        var antallForste = await db.Virksomheter.CountAsync();
        // Egen sentinel-kommune (Trondheim), ADSKILT fra Oslo som den andre testen i denne klassen
        // asserterer på — samme delte DataTestCollection-database, så en manuell Aktiv-mutasjon her
        // må ikke kunne bløe over i en annen tests forventninger om startverdien.
        var trondheimForste = await db.Virksomheter.SingleAsync(v => v.Navn == "Trondheim kommune");
        Assert.False(trondheimForste.Aktiv);

        // Simulerer en fremtidig manuell aktivering (ingen admin-UI for dette bygget ennå, se
        // Virksomhet.Aktiv-kommentaren) — noe en senere oppstart av seeden ALDRI skal nullstille.
        trondheimForste.Aktiv = true;
        await db.SaveChangesAsync();

        await OrganisasjonsregisterSeed.SeedAsync(db);

        Assert.Equal(antallForste, await db.Virksomheter.CountAsync()); // ingen duplikater
        var trondheimAndre = await db.Virksomheter.SingleAsync(v => v.Navn == "Trondheim kommune");
        Assert.True(trondheimAndre.Aktiv); // IKKE nullstilt av den andre kjøringen

        // Bergen/Agder forblir tvunget aktive, uendret av gjentatt kjøring.
        var agder = await db.Virksomheter.SingleAsync(v => v.Navn == "Agder fylkeskommune");
        Assert.True(agder.Aktiv);
        // FirstAsync, ikke SingleAsync — samme defensive valg som Testkommunen-oppslaget over: denne
        // delte DataTestCollection-databasen kan i praksis se mer enn én "Bergen kommune"-rad avhengig
        // av hvilke andre testklasser (nå: ServeringsbevillingModellSeedTests) som også kaller
        // BergenKorpusSeed i samme kjøring.
        var bergen = await db.Virksomheter.FirstAsync(v => v.Navn == "Bergen kommune");
        Assert.True(bergen.Aktiv);
    }

    [Fact]
    public async Task Seeder_to_testbrukere_for_bergen_kommune_idempotent()
    {
        await using var db = _fixture.NyDbContext();
        await ForberedEksisterendeVirksomheterAsync(db);

        await OrganisasjonsregisterSeed.SeedAsync(db);

        // FirstAsync, ikke SingleAsync — samme defensive valg som Testkommunen-oppslaget over: denne
        // delte DataTestCollection-databasen kan i praksis se mer enn én "Bergen kommune"-rad avhengig
        // av hvilke andre testklasser (nå: ServeringsbevillingModellSeedTests) som også kaller
        // BergenKorpusSeed i samme kjøring.
        var bergen = await db.Virksomheter.FirstAsync(v => v.Navn == "Bergen kommune");
        var brukere = await db.Brukere.Where(b => b.VirksomhetId == bergen.Id).ToListAsync();
        Assert.Equal(2, brukere.Count);
        Assert.Contains(brukere, b => b.Navn == "Mari Fagansvarlig" && b.Rolle == "Fagansvarlig");
        Assert.Contains(brukere, b => b.Navn == "Jonas Saksbehandler" && b.Rolle == "Saksbehandler");

        await OrganisasjonsregisterSeed.SeedAsync(db); // idempotent — ingen ekstra brukere.
        Assert.Equal(2, await db.Brukere.CountAsync(b => b.VirksomhetId == bergen.Id));
    }
}
