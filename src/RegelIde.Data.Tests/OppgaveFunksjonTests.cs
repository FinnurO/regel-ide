using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #368 «oppgave/funksjon (kategori F)», 2026-10-15] <see cref="StrukturkantTjeneste"/> mot ekte embedded Postgres.
/// <para>
/// Spørsmålene modellen skal kunne besvare etterpå (sakens AC1 og AC5, docs/32 S2/S4): «Hvilke oppgaver har Beredskapsmyndigheten,
/// og etter hvilken bestemmelse?» — og, like viktig, at spørsmålet «hvilken kompetanse (K) eller plikt (P) har den?» IKKE svarer med
/// oppgaven: F gir aldri K eller P i seg selv (docs/34 §6.1, begrepsmanualen §F). Avvises en oppgave uten objekt? Tillates modalitet
/// på F (og fortsatt ikke på K)? Er «skal» og «kan» to utsagn?
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class OppgaveFunksjonTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public OppgaveFunksjonTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static string Unik(string prefiks) => $"{prefiks}-{Guid.NewGuid():N}";

    private static async Task<(Guid LovId, string Eid)> OppsettAsync(RegelIdeDbContext db)
    {
        await Strukturkanter.SeedStartsettAsync(db);
        var lovId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
        var eid = (await db.RettskildeNoder.Where(n => n.RettskildeId == lovId && n.NodeType == "paragraf")
            .OrderBy(n => n.Sorteringsrekkefolge).FirstAsync()).Eid;
        return (lovId, eid);
    }

    private static async Task<Guid> NyVirksomhetAsync(RegelIdeDbContext db, string navn)
    {
        var v = new Virksomhet { Id = Guid.NewGuid(), Navn = Unik(navn) };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return v.Id;
    }

    [Fact]
    public async Task Oppgavetypene_seedes_med_maler_uten_overfor()
    {
        await using var db = _fixture.NyDbContext();
        await Strukturkanter.SeedStartsettAsync(db);
        var typer = await db.RelasjonsTypeKonfigurasjoner.AsNoTracking().Where(t => t.Kategori == Strukturkanter.Oppgave).ToListAsync();
        Assert.Equal(Strukturkanter.Oppgavetyper.Select(t => t.Kode).Order(), typer.Select(t => t.Kode).Order());
        Assert.All(typer, t => Assert.DoesNotContain("overfor", t.FraVisningsmal + t.TilVisningsmal));
        Assert.All(typer, t => Assert.Null(t.Familie)); // F har ingen kompetansefamilie
    }

    [Fact]
    public async Task F_samordning_lagres_med_modalitet_og_vises_som_oppgave_ikke_som_kompetanse_eller_plikt()
    {
        await using var db = _fixture.NyDbContext();
        var (lov, eid) = await OppsettAsync(db);
        var beredskap = await NyVirksomhetAsync(db, "Beredskapsmyndigheten");
        var tjeneste = new StrukturkantTjeneste(db);

        // energiloven § 9-1 (u160): «Beredskapsmyndigheten skal samordne beredskapsarbeid …»
        var r = await tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Oppgave, "samordning", Kantnode.Virksomhet(beredskap), null,
            HjemmelRettskildeId: lov, HjemmelEid: eid, Objekt: "beredskapsarbeid", Modalitet: "skal",
            Kunnskapsstatus: Strukturkanter.Fortolkning, Begrunnelse: "«skal samordne» uten hjemmel til å binde — oppgave, ikke myndighet.",
            Tolkningsargumenter: ["ordlyd", "kontekst_system"]), "Kari Jurist");
        Assert.Equal(("F", "samordning", "skal"), (r.Kant.Kategori, r.Kant.Typekode, r.Kant.Modalitet));

        var visning = Assert.Single(await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(beredskap), Strukturkanter.Oppgave));
        Assert.Equal("har samordningsoppgave (skal) beredskapsarbeid", visning.Visningstekst);

        // F gir aldri K eller P: spørsmålet om kompetanse eller plikt svarer ikke med oppgaven, og ingen K/P-kant er sluttet.
        Assert.Empty(await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(beredskap), Strukturkanter.Kompetanse));
        Assert.Empty(await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(beredskap), Strukturkanter.Plikt));
        Assert.Equal(1, await db.Strukturkanter.CountAsync(k => k.FraVirksomhetId == beredskap));

        // «skal» og «kan» samordne er to utsagn; samme modalitet igjen er samme kant (idempotens).
        var igjen = await tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Oppgave, "samordning", Kantnode.Virksomhet(beredskap), null,
            HjemmelRettskildeId: lov, HjemmelEid: eid, Objekt: "beredskapsarbeid", Modalitet: "skal"), "Kari Jurist");
        Assert.False(igjen.VarNy);
        var kan = await tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Oppgave, "samordning", Kantnode.Virksomhet(beredskap), null,
            HjemmelRettskildeId: lov, HjemmelEid: eid, Objekt: "beredskapsarbeid", Modalitet: "kan"), "Kari Jurist");
        Assert.True(kan.VarNy);
    }

    [Fact]
    public async Task F_med_det_oppgaven_gjelder_leses_fra_begge_sider_uten_overfor()
    {
        await using var db = _fixture.NyDbContext();
        var (lov, eid) = await OppsettAsync(db);
        var namsfogden = await NyVirksomhetAsync(db, "Namsfogden");
        var forliksradet = await NyVirksomhetAsync(db, "Forliksrådet");
        var tjeneste = new StrukturkantTjeneste(db);

        // Syntetisk rad for visningen. (Juristrunden i #368: «Namsfogden er sekretariat for forliksrådene», domstolloven u70, er R alene —
        // F bare når teksten sier hva sekretariatet gjør, som «sekretariat som foretar saksforberedelse».)
        await tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Oppgave, "sekretariat", Kantnode.Virksomhet(namsfogden), Kantnode.Virksomhet(forliksradet),
            HjemmelRettskildeId: lov, HjemmelEid: eid, Objekt: "sekretariat for forliksrådene", AvgrensningTekst: "i sitt distrikt"),
            "Kari Jurist");

        var fra = Assert.Single(await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(namsfogden)));
        Assert.StartsWith("har sekretariatsfunksjon sekretariat for forliksrådene — gjelder ", fra.Visningstekst);
        var til = Assert.Single(await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(forliksradet)));
        Assert.EndsWith(" har sekretariatsfunksjon sekretariat for forliksrådene — gjelder denne", til.Visningstekst);
        Assert.DoesNotContain("overfor", fra.Visningstekst + til.Visningstekst);
    }

    [Fact]
    public async Task F_uten_objekt_avvises_av_tjenesten_og_av_CHECK_en_og_modalitet_er_fortsatt_ikke_lov_paa_K()
    {
        await using var db = _fixture.NyDbContext();
        var (lov, eid) = await OppsettAsync(db);
        var a = await NyVirksomhetAsync(db, "Koordinerende enhet");
        var tjeneste = new StrukturkantTjeneste(db);

        var feil = await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Oppgave, "sorge_for", Kantnode.Virksomhet(a), null, HjemmelRettskildeId: lov, HjemmelEid: eid), "Kari Jurist"));
        Assert.Contains("HVA oppgaven er", feil.Message);

        // CHECK ck_strukturkanter_oppgave_objekt: også en skrivevei utenom tjenesten stoppes.
        var db2 = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO strukturkanter ("Id", kategori, typekode, fra_virksomhet_id, hjemmel_rettskilde_id, hjemmel_eid,
                status, oppdagelses_kilde, opprettet_av, opprettet_tidspunkt)
            VALUES ({Guid.NewGuid()}, 'F', 'sorge_for', {a}, {lov}, {eid}, 'godkjent', 'manuell', 'test', now())
            """));
        Assert.Contains("ck_strukturkanter_oppgave_objekt", db2.Message);

        var k = await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Kompetanse, "samordning", Kantnode.Virksomhet(a), null, HjemmelRettskildeId: lov, HjemmelEid: eid,
            Objekt: "x", Modalitet: "skal"), "Kari Jurist"));
        Assert.Contains("PLIKT (P) eller en OPPGAVE (F)", k.Message);
    }
}
