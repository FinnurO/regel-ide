using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>Hendelseregister (docs/03-domenemodell.md §1.5, docs/13-backlog.md §2.1), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class HendelseregisterTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public HendelseregisterTjenesteTests(EmbeddedPostgresFixture fixture)
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

    [Fact]
    public async Task Oppretter_hendelse_med_virksomhetseid_og_proveniens()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);

        var register = new HendelseregisterTjeneste(db);
        var hendelse = await register.OpprettAsync(
            virksomhet, "Eierskifte", "virksomhetshendelse", "En eier overtar driften av virksomheten.", "Kari Jurist");

        Assert.Equal("gjeldende", hendelse.Entitetsstatus);
        var proveniens = await db.Proveniens.SingleAsync(p => p.EntitetId == hendelse.Id);
        Assert.Equal("opprettet", proveniens.Handling);
    }

    [Fact]
    public async Task Ukjent_type_kastes_ingen_gjettet_fallback()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);

        var register = new HendelseregisterTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettAsync(virksomhet, "Eierskifte", "ukjent_type", null, "Kari Jurist"));
    }

    [Fact]
    public async Task Lister_nasjonale_hendelser_uansett_virksomhet_men_lokale_kun_for_egen_virksomhet()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetA = await NyVirksomhetAsync(db, "Testkommunen");
        var virksomhetB = await NyVirksomhetAsync(db, "Nabokommunen");

        var register = new HendelseregisterTjeneste(db);
        await register.OpprettAsync(null, "Kontroll/tilsyn (nasjonal)", "generell", null, "Systemforvalter");
        await register.OpprettAsync(virksomhetA, "Lokal hendelse A", "virksomhetshendelse", null, "Kari Jurist");
        await register.OpprettAsync(virksomhetB, "Lokal hendelse B", "virksomhetshendelse", null, "Kari Jurist");

        var listeForA = await register.ListerAsync(virksomhetA);
        Assert.Contains(listeForA, h => h.Navn == "Kontroll/tilsyn (nasjonal)");
        Assert.Contains(listeForA, h => h.Navn == "Lokal hendelse A");
        Assert.DoesNotContain(listeForA, h => h.Navn == "Lokal hendelse B");
    }

    [Fact]
    public async Task Kobler_tjeneste_til_hendelse_symmetrisk_ingen_retning()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var tjenesteA = await NyTjenesteAsync(db, virksomhet, "Alminnelig skjenkebevilling");
        var tjenesteB = await NyTjenesteAsync(db, virksomhet, "Kontroller av salgs- og skjenkesteder");

        var register = new HendelseregisterTjeneste(db);
        var hendelse = await register.OpprettAsync(null, "Kontroll/tilsyn", "virksomhetshendelse", null, "Kari Jurist");
        await register.KobleTilTjenesteAsync(tjenesteA, hendelse.Id);
        await register.KobleTilTjenesteAsync(tjenesteB, hendelse.Id);

        var forA = await register.ListerForTjenesteAsync(tjenesteA);
        var forB = await register.ListerForTjenesteAsync(tjenesteB);
        Assert.Single(forA, h => h.Id == hendelse.Id);
        Assert.Single(forB, h => h.Id == hendelse.Id);
    }

    [Fact]
    public async Task Kan_ikke_koble_samme_tjeneste_til_samme_hendelse_to_ganger()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var tjeneste = await NyTjenesteAsync(db, virksomhet, "Alminnelig skjenkebevilling");

        var register = new HendelseregisterTjeneste(db);
        var hendelse = await register.OpprettAsync(null, "Eierskifte", "virksomhetshendelse", null, "Kari Jurist");
        await register.KobleTilTjenesteAsync(tjeneste, hendelse.Id);

        await Assert.ThrowsAsync<ArgumentException>(() => register.KobleTilTjenesteAsync(tjeneste, hendelse.Id));
    }

    [Fact]
    public async Task Fjerner_klassifisering_uten_a_slette_selve_hendelsen()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = await NyVirksomhetAsync(db);
        var tjeneste = await NyTjenesteAsync(db, virksomhet, "Alminnelig skjenkebevilling");

        var register = new HendelseregisterTjeneste(db);
        var hendelse = await register.OpprettAsync(null, "Eierskifte", "virksomhetshendelse", null, "Kari Jurist");
        await register.KobleTilTjenesteAsync(tjeneste, hendelse.Id);

        var fjernet = await register.FjernFraTjenesteAsync(tjeneste, hendelse.Id);
        Assert.True(fjernet);
        Assert.Empty(await register.ListerForTjenesteAsync(tjeneste));
        Assert.NotNull(await register.FinnAsync(hendelse.Id));
    }
}
