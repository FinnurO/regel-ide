using System.Text.Json.Nodes;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, 2026-08-28] Rettighet-modelleksport (GET /api/tjenester/{id}/modelleksport og flertalls-
/// motstykket) — dekker de tre utvidelsene denne runden la til: feltnivå-regelverksreferanser, frie
/// egne innholdselementer, og delte (koblede, ikke bare eide) handlinger. Samme
/// embedded-Postgres-mønster som <see cref="TjenesteEksportTjenesteTests"/>.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class RettighetModellEksportTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public RettighetModellEksportTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static RettighetModellEksportTjeneste NyEksport(RegelIdeDbContext db) =>
        new(db, new HandlingTjenesteregisterTjeneste(db), new TjenesteavhengighetregisterTjeneste(db));

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

    /// <summary>Samme mønster som TjenesteforslagTjenesteTests — én rettskilde med én oppslåbar node.</summary>
    private static async Task<(Guid RettskildeId, string Eid)> NyRettskildeMedNodeAsync(RegelIdeDbContext db)
    {
        var rettskildeId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId, Doctype = "act", Kildetype = "Lov", Importrolle = "referanse",
            Tittel = "Testlov", Kortnavn = "testl", Status = "Gjeldende", OpprettetAv = "system-test",
        });
        var eid = $"test/{Guid.NewGuid()}";
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = eid,
            KildeId = $"k-{Guid.NewGuid()}", NodeType = "ledd", Tekst = "Testtekst", Sorteringsrekkefolge = 0,
        });
        await db.SaveChangesAsync();
        return (rettskildeId, eid);
    }

    [Fact]
    public async Task Regelverksreferanse_uten_felt_gir_null_felt_i_eksporten()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var tjenesteId = await NyTjenesteAsync(db, virksomhet, "Serveringsbevilling");
        var (rettskildeId, eid) = await NyRettskildeMedNodeAsync(db);
        await new TjenesteregisterTjeneste(db).KobleRegelverksreferanseAsync(tjenesteId, rettskildeId, eid);

        var eksport = await NyEksport(db).EksporterAsync(tjenesteId);

        var referanse = Assert.Single(eksport!["regelverksreferanser"]!.AsArray());
        Assert.Null(referanse!["felt"]);
    }

    [Fact]
    public async Task Regelverksreferanse_med_felt_vises_korrekt_i_eksporten()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var tjenesteId = await NyTjenesteAsync(db, virksomhet, "Serveringsbevilling");
        var (rettskildeId, eid) = await NyRettskildeMedNodeAsync(db);
        await new TjenesteregisterTjeneste(db).KobleRegelverksreferanseAsync(tjenesteId, rettskildeId, eid, felt: "tittel");

        var eksport = await NyEksport(db).EksporterAsync(tjenesteId);

        var referanse = Assert.Single(eksport!["regelverksreferanser"]!.AsArray());
        Assert.Equal("tittel", referanse!["felt"]!.GetValue<string>());
    }

    [Fact]
    public async Task Egne_innholdselementer_vises_i_innhold()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var tjeneste = await new TjenesteregisterTjeneste(db).OpprettAsync(
            virksomhet, "Serveringsbevilling", null, null, null, null, null, null, null, null, null, null, null, "Kari Jurist",
            egneInnholdselementer: [new EgetInnholdselementInput("e1", "Ekstra info", "Noe fritekst")]);

        var eksport = await NyEksport(db).EksporterAsync(tjeneste.Id);

        var element = Assert.Single(eksport!["innhold"]!["egne_innholdselementer"]!.AsArray());
        Assert.Equal("e1", element!["id"]!.GetValue<string>());
        Assert.Equal("Ekstra info", element["tittel"]!.GetValue<string>());
        Assert.Equal("Noe fritekst", element["tekst"]!.GetValue<string>());
    }

    [Fact]
    public async Task Tjeneste_uten_innhold_og_uten_egne_elementer_gir_null_innhold()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var tjenesteId = await NyTjenesteAsync(db, virksomhet, "Serveringsbevilling");

        var eksport = await NyEksport(db).EksporterAsync(tjenesteId);

        Assert.Null(eksport!["innhold"]);
    }

    [Fact]
    public async Task Handling_skiller_eid_fra_kun_koblet_tjeneste()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var eierTjeneste = await NyTjenesteAsync(db, virksomhet, "Serveringsbevilling");
        var annenTjeneste = await NyTjenesteAsync(db, virksomhet, "Skjenkebevilling");

        var handling = await new HandlingregisterTjeneste(db).OpprettAsync(
            virksomhet, eierTjeneste, "Søke om bevilling", "soke", null, null,
            null, null, null, null, null, null, null, null, "Kari Jurist");
        var koblingregister = new HandlingTjenesteregisterTjeneste(db);
        await koblingregister.KobleAsync(annenTjeneste, virksomhet, handling.Id);

        var eksportEier = await NyEksport(db).EksporterAsync(eierTjeneste);
        var eksportAnnen = await NyEksport(db).EksporterAsync(annenTjeneste);

        var iEier = Assert.Single(eksportEier!["handlinger"]!.AsArray());
        Assert.True(iEier!["eies_av_denne_tjenesten"]!.GetValue<bool>());

        var iAnnen = Assert.Single(eksportAnnen!["handlinger"]!.AsArray());
        Assert.False(iAnnen!["eies_av_denne_tjenesten"]!.GetValue<bool>());
    }

    [Fact]
    public async Task EksporterFlereAsync_gir_rettigheter_for_gyldige_ider_og_hopper_over_ugyldig()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var id1 = await NyTjenesteAsync(db, virksomhet, "Serveringsbevilling");
        var id2 = await NyTjenesteAsync(db, virksomhet, "Skjenkebevilling");
        var ukjentId = Guid.NewGuid();

        var eksport = await NyEksport(db).EksporterFlereAsync([id1, id2, ukjentId]);

        var rettigheter = eksport["rettigheter"]!.AsArray();
        Assert.Equal(2, rettigheter.Count);
        Assert.Contains(rettigheter, r => r!["navn"]!.GetValue<string>() == "Serveringsbevilling");
        Assert.Contains(rettigheter, r => r!["navn"]!.GetValue<string>() == "Skjenkebevilling");
    }

    [Fact]
    public async Task Ukjent_tjeneste_gir_null_for_enkelt_eksport()
    {
        await using var db = _fixture.NyDbContext();
        var eksport = await NyEksport(db).EksporterAsync(Guid.NewGuid());
        Assert.Null(eksport);
    }
}
