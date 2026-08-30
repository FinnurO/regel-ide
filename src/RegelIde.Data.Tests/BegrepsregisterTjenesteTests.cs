using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>Begrepsregister (SKOS, docs/03-domenemodell.md §1.3), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class BegrepsregisterTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public BegrepsregisterTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // Kodelister.Kode har en ekte global unique-indeks — se samme kommentar i KodelisteregisterTjenesteTests.
    private static string NyKode() => $"KL-TEST-{Guid.NewGuid():N}";

    private static async Task<(Guid RettskildeId, RettskildeNodeEntitet Node)> ImporterAlkohollovenOgFinnParagrafAsync(RegelIdeDbContext db)
    {
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));
        var node = await db.RettskildeNoder.FirstAsync(n => n.RettskildeId == rettskildeId && n.NodeType == "paragraf");
        return (rettskildeId, node);
    }

    [Fact]
    public async Task Oppretter_begrep_med_gyldig_lovreferanse()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var (_, node) = await ImporterAlkohollovenOgFinnParagrafAsync(db);

        var register = new BegrepsregisterTjeneste(db);
        var begrep = await register.OpprettAsync(virksomhet, "eksempelbegrep", "Definisjon", node.Eid,
            null, null, null, "handlingsbegrep", "Kari Jurist");

        Assert.Equal("utkast", begrep.Status);
        Assert.Equal(node.Eid, begrep.LovreferanseEid);
        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetId == begrep.Id);
        Assert.Equal("opprettet", proveniens.Handling);
    }

    [Fact]
    public async Task Ukjent_lovreferanse_kastes_ingen_gjettet_fallback()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new BegrepsregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            virksomhet, "eksempelbegrep", "Definisjon", "ukjent-eid", null, null, null, "handlingsbegrep", "Kari Jurist"));
    }

    [Fact]
    public async Task Ukjent_begrepstype_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new BegrepsregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            virksomhet, "eksempelbegrep", "Definisjon", null, null, null, null, "ukjent-type", "Kari Jurist"));
    }

    [Fact]
    public async Task Oppretter_begrep_med_gyldig_kodelistereferanse()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var kodelisteregister = new KodelisteregisterTjeneste(db);
        var kodeliste = await kodelisteregister.OpprettAsync(
            virksomhet, NyKode(), "Test", "juridisk", null, null, null, "Kari Jurist");

        var register = new BegrepsregisterTjeneste(db);
        var begrep = await register.OpprettAsync(virksomhet, "eksempelbegrep", "Definisjon", null,
            null, kodeliste.Id, null, "handlingsbegrep", "Kari Jurist");

        Assert.Equal(kodeliste.Id, begrep.KodelisteReferanseId);
    }

    [Fact]
    public async Task Ukjent_kodelistereferanse_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new BegrepsregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettAsync(
            virksomhet, "eksempelbegrep", "Definisjon", null, null, Guid.NewGuid(), null, "handlingsbegrep", "Kari Jurist"));
    }

    [Fact]
    public async Task Oppdaterer_begrep_oker_versjon()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new BegrepsregisterTjeneste(db);
        var begrep = await register.OpprettAsync(virksomhet, "eksempelbegrep", "Definisjon", null,
            null, null, null, "handlingsbegrep", "Kari Jurist");

        var oppdatert = await register.OppdaterAsync(begrep.Id, "eksempelbegrep", "Ny definisjon", null,
            null, null, null, "handlingsbegrep", "Ola Fagansvarlig");

        Assert.NotNull(oppdatert);
        Assert.Equal("Ny definisjon", oppdatert!.Definisjon);
        Assert.Equal(2, oppdatert.Versjon);
    }

    [Fact]
    public async Task Oppdaterer_virksomhet_navneform_rorer_ikke_definisjon_eller_begrepstype()
    {
        // [Rettet, 2026-08-30] Regresjonstest for bugen beskrevet i OppdaterAsync sin XML-kommentar:
        // PUT /api/begreper/{id} skal ALDRI kunne forurense en virksomhet-/rolle-navneform med en
        // oppfunnet definisjon/begrepstype, uansett hva som sendes inn i requesten.
        // [Rettet, 2026-08-30, testisolasjon] Bruker en syntetisk, garantert unik virksomhetsnavn —
        // IKKE "Agder fylkeskommune"/"Testkommunen" (delte, navn-guardede fixtures fra andre
        // testklasser i samme DataTestCollection-database, se OrganisasjonsregisterSeedTests sin
        // klassekommentar) — en ekstra rad med et av disse navnene brøt
        // OrganisasjonsregisterSeedTests sitt eget Single()-oppslag, avhengig av kjørerekkefølge.
        await using var db = _fixture.NyDbContext();
        var malVirksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = malVirksomhet, Navn = $"Testvirksomhet {malVirksomhet:N}" });
        await db.SaveChangesAsync();

        var navneform = await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(
            malVirksomhet, "Fylkeskommune", "Kari Jurist");
        Assert.Null(navneform.Definisjon);
        Assert.Null(navneform.Begrepstype);

        var register = new BegrepsregisterTjeneste(db);
        var oppdatert = await register.OppdaterAsync(navneform.Id, "Fylkeskommune (rettet)", "en oppfunnet definisjon", null,
            null, null, null, "faktabegrep", "Ola Fagansvarlig");

        Assert.NotNull(oppdatert);
        Assert.Equal("Fylkeskommune (rettet)", oppdatert!.Term);
        Assert.Null(oppdatert.Definisjon);
        Assert.Null(oppdatert.Begrepstype);
    }

    [Fact]
    public async Task Setter_status()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new BegrepsregisterTjeneste(db);
        var begrep = await register.OpprettAsync(virksomhet, "eksempelbegrep", "Definisjon", null,
            null, null, null, "handlingsbegrep", "Kari Jurist");

        var oppdatert = await register.SettStatusAsync(begrep.Id, "validert", "Kari Jurist");

        Assert.NotNull(oppdatert);
        Assert.Equal("validert", oppdatert!.Status);
    }

    [Fact]
    public async Task Setter_status_med_godkjentAv_logges_i_proveniens()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var register = new BegrepsregisterTjeneste(db);
        var begrep = await register.OpprettAsync(virksomhet, "eksempelbegrep", "Definisjon", null,
            null, null, null, "handlingsbegrep", "Kari Jurist");

        await register.SettStatusAsync(begrep.Id, "validert", "Kari Jurist", godkjentAv: "Ola Fagansvarlig");

        var proveniens = await db.Proveniens
            .Where(p => p.EntitetId == begrep.Id && p.Handling == "validert")
            .SingleAsync();
        Assert.Equal("Ola Fagansvarlig", proveniens.GodkjentAv);
    }

    [Fact]
    public async Task Byggesteg5_oppretter_forslag_fra_ki_med_status_foreslatt_av_ai()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        // Merk: bevisst IKKE "uklanderlig vandel" — det navnet brukes av Byggesteg2InnholdSeed sin
        // globale term-guard (se SeedBegrepAsync) og en kollisjon her ville stille skjult sabotert
        // Byggesteg2InnholdSeedTests i den delte embedded-Postgres-collectionen.
        var register = new BegrepsregisterTjeneste(db);
        var begrep = await register.OpprettForslagFraKiAsync(
            virksomhet, "uklanderlig vandel (KI-forslag-test)", "Definisjon fra KI", null, null, null, null,
            "faktabegrep", "system-ki", "stub-v1", """{"rettskildeIder":[]}""");

        Assert.Equal("foreslatt_av_ai", begrep.Status);
        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetId == begrep.Id);
        Assert.Equal("foreslatt_av_ai", proveniens.Handling);
        Assert.Equal("stub-v1", proveniens.AiForslagVersjon);
        Assert.NotNull(proveniens.KildeReferanserJson);
    }
}
