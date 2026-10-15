using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #367 «utsagnsstatus», docs/34 §3–5, docs/profil/03 §5] Kunnskapsstatus, godkjenning, analysehistorikk og versjonert hjemmel
/// i <see cref="StrukturkantTjeneste"/> og <see cref="HjemmelEndretFlagg"/>, mot ekte embedded Postgres (CHECK-ene og constraint-triggeren
/// fra migrasjonen gjelder). Spørsmålene: Krever hver kunnskapsstatus grunnlaget metodenotatet sier? Gir en revisjon en ny rad og gjør den
/// gamle foreldet? Kan to omstridte tolkninger stå side om side? Flagges en kant når hjemmelens tekst endres — og bare da?
/// </summary>
[Collection(DataTestCollection.Navn)]
public class UtsagnsstatusTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public UtsagnsstatusTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static string Unik(string prefiks) => $"{prefiks}-{Guid.NewGuid():N}";

    private static async Task<(Guid LovId, string ParagrafEid)> OppsettAsync(RegelIdeDbContext db)
    {
        await Strukturkanter.SeedStartsettAsync(db);
        var lovId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
        var paragraf = await db.RettskildeNoder.Where(n => n.RettskildeId == lovId && n.NodeType == "paragraf")
            .OrderBy(n => n.Sorteringsrekkefolge).FirstAsync();
        return (lovId, paragraf.Eid);
    }

    private static async Task<Guid> VirksomhetAsync(RegelIdeDbContext db, string navn)
    {
        var v = new Virksomhet { Id = Guid.NewGuid(), Navn = Unik(navn) };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return v.Id;
    }

    /// <summary>AC1/AC4: kravene til grunnlag, profilversjon på vurderte rader, og den versjonerte hjemmelnoden satt ved opprettelse.</summary>
    [Fact]
    public async Task Kunnskapsstatus_krever_grunnlaget_og_hjemmelnoden_settes()
    {
        await using var db = _fixture.NyDbContext();
        var (lov, eid) = await OppsettAsync(db);
        var (a, b) = (await VirksomhetAsync(db, "Organ A"), await VirksomhetAsync(db, "Organ B"));
        var t = new StrukturkantTjeneste(db);

        var tekstfunn = (await t.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "klage", Kantnode.Virksomhet(b), Kantnode.Virksomhet(a),
            HjemmelRettskildeId: lov, HjemmelEid: eid, Kunnskapsstatus: "tekstfunn"), "Kari Jurist")).Kant;
        var node = await db.RettskildeNoder.SingleAsync(n => n.RettskildeId == lov && n.Eid == eid);
        Assert.Equal((node.Id, "tekstfunn", Strukturkanter.Profilversjon), (tekstfunn.HjemmelNodeId, tekstfunn.Kunnskapsstatus, tekstfunn.Profilversjon));
        var uten = (await t.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "tilsyn", Kantnode.Virksomhet(b), Kantnode.Virksomhet(a),
            HjemmelRettskildeId: lov, HjemmelEid: eid), "Kari Jurist")).Kant;
        Assert.Equal(((string?)null, (string?)null), (uten.Kunnskapsstatus, uten.Profilversjon)); // ikke vurdert

        Task Feil(string? status, Guid? hjemmel = null, string? heid = null, string? kilde = null, string? begrunnelse = null,
            IReadOnlyList<string>? arg = null) =>
            Assert.ThrowsAsync<ArgumentException>(() => t.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "vedtak",
                Kantnode.Virksomhet(a), null, HjemmelRettskildeId: hjemmel, HjemmelEid: heid, Objekt: "x",
                KildeUtenforKorpusTekst: kilde, KildeUtenforKorpusType: kilde is null ? null : "vedtekter",
                KildeUtenforKorpusDokumentasjon: kilde is null ? null : "primaer", Kunnskapsstatus: status, Begrunnelse: begrunnelse,
                Tolkningsargumenter: arg), "Kari Jurist"));
        await Feil("tekstfunn", hjemmel: lov);                                     // uten eId
        await Feil("normalisering", kilde: "Vedtektene § 3");                       // uten hjemmel i korpus
        await Feil("eksternt_dokumentert", hjemmel: lov, heid: eid);               // uten kilde utenfor korpus
        await Feil("fortolkning", hjemmel: lov, heid: eid, begrunnelse: "fordi");  // uten tolkningsargument
        await Feil("fortolkning", hjemmel: lov, heid: eid, begrunnelse: "fordi", arg: ["magefølelse"]);
        await Feil("avledet", hjemmel: lov, heid: eid);                            // uten premiss
        await Feil("foreldet", hjemmel: lov, heid: eid);                           // settes bare ved revisjon
        await Feil("antakelse", hjemmel: lov, heid: eid);

        var fortolkning = (await t.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "instruksjon", Kantnode.Virksomhet(b),
            Kantnode.Virksomhet(a), HjemmelRettskildeId: lov, HjemmelEid: eid, Kunnskapsstatus: "fortolkning",
            Begrunnelse: "Sett mot § 29.", Tolkningsargumenter: ["ordlyd", "kontekst_system"]), "Kari Jurist")).Kant;
        Assert.Equal(["ordlyd", "kontekst_system"], fortolkning.Tolkningsargumenter!);

        // Avledet: premissene lagres og vises; constraint-triggeren nekter avledet uten premiss også utenom tjenesten.
        var avledet = (await t.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "overproving", Kantnode.Virksomhet(b),
            Kantnode.Virksomhet(a), HjemmelRettskildeId: lov, HjemmelEid: eid, Kunnskapsstatus: "avledet",
            Premisser: [tekstfunn.Id, uten.Id]), "Kari Jurist")).Kant;
        Assert.Equal(new[] { tekstfunn.Id, uten.Id }.OrderBy(x => x), (await t.HentAsync(avledet.Id))!.PremissIder!);
        var utenId = uten.Id;
        var avledetId = avledet.Id;
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE strukturkanter SET kunnskapsstatus = 'avledet' WHERE \"Id\" = {utenId}"));
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM strukturkant_premisser WHERE kant_id = {avledetId}"));
    }

    /// <summary>AC3: en revisjon gir en ny rad og den gamle blir foreldet (ikke slettet); en mislykket revisjon ruller tilbake; omstridte
    /// utsagn slipper identitetsregelen.</summary>
    [Fact]
    public async Task Revisjon_gir_ny_rad_og_omstridt_slipper_identitetsregelen()
    {
        await using var db = _fixture.NyDbContext();
        var (lov, eid) = await OppsettAsync(db);
        var (a, b) = (await VirksomhetAsync(db, "Foretaksmøtet"), await VirksomhetAsync(db, "Helseforetaket"));
        var t = new StrukturkantTjeneste(db);
        NyStrukturkant Ny(string? status = null, string? begrunnelse = null, IReadOnlyList<string>? arg = null) =>
            new(Strukturkanter.Kompetanse, "instruksjon", Kantnode.Virksomhet(a), Kantnode.Virksomhet(b), HjemmelRettskildeId: lov,
                HjemmelEid: eid, Kunnskapsstatus: status, Begrunnelse: begrunnelse, Tolkningsargumenter: arg);

        var gammel = (await t.OpprettAsync(Ny(), "Kari Jurist")).Kant;
        var ny = (await t.ReviderAsync(gammel.Id, Ny("fortolkning", "Eierstyring er beslutning, ikke instruksjon (§ 16 mot § 29).",
            ["kontekst_system"]), "Kari Jurist"))!;
        Assert.Equal(gammel.Id, ny.ErstatterKantId);
        await using (var les = _fixture.NyDbContext())
        {
            Assert.Equal("foreldet", (await les.Strukturkanter.IgnoreQueryFilters().SingleAsync(k => k.Id == gammel.Id)).Entitetsstatus);
        }
        Assert.Equal(ny.Id, Assert.Single(await t.HentForNodeAsync(Kantnode.Virksomhet(a))).Id); // bare den gjeldende vises
        Assert.Null(await t.ReviderAsync(gammel.Id, Ny(), "Kari Jurist")); // en foreldet rad revideres ikke

        await Assert.ThrowsAsync<ArgumentException>(() => t.ReviderAsync(ny.Id, Ny("fortolkning"), "Kari Jurist"));
        await using (var les = _fixture.NyDbContext())
        {
            Assert.Equal("gjeldende", (await les.Strukturkanter.IgnoreQueryFilters().SingleAsync(k => k.Id == ny.Id)).Entitetsstatus);
        }

        await using var db2 = _fixture.NyDbContext();
        var t2 = new StrukturkantTjeneste(db2);
        var o1 = await t2.OpprettAsync(Ny("omstridt", "Tolkning 1"), "Kari Jurist");
        var o2 = await t2.OpprettAsync(Ny("omstridt", "Tolkning 2"), "Kari Jurist");
        Assert.True(o1.VarNy && o2.VarNy);
        Assert.NotEqual(o1.Kant.Id, o2.Kant.Id);
    }

    /// <summary>AC2: et forslag i hver av de ventende statusene står i forslagskøen og godkjennes til «godkjent»; en godkjent rad har
    /// ingenting å godkjenne.</summary>
    [Fact]
    public async Task Godkjenning_fra_alle_ventende_statuser()
    {
        await using var db = _fixture.NyDbContext();
        var (lov, _) = await OppsettAsync(db);
        var t = new StrukturkantTjeneste(db);
        foreach (var status in Strukturkanter.Ventende)
        {
            var (a, b) = (await VirksomhetAsync(db, "A"), await VirksomhetAsync(db, "B"));
            var kant = (await t.OpprettAsync(new NyStrukturkant(Strukturkanter.Relasjon, "radgir", Kantnode.Virksomhet(a), Kantnode.Virksomhet(b),
                HjemmelRettskildeId: lov, Status: status, OppdagelsesKilde: status == Strukturkanter.MaskineltForeslatt ? "ki:test" : null),
                "Kari Jurist")).Kant;
            Assert.Contains(await t.HentForslagAsync(), v => v.Id == kant.Id);
            Assert.Equal("godkjent", (await t.GodkjennAsync(kant.Id, "Kari Jurist"))!.Status);
            await Assert.ThrowsAsync<ArgumentException>(() => t.GodkjennAsync(kant.Id, "Kari Jurist"));
        }
    }

    /// <summary>AC4: «hjemmel endret, vurder på nytt» settes når hjemmelnodens deltre har annen tekst i den nye versjonen — ikke når den
    /// er uendret, og bare én gang. Kunnskapsstatus, godkjenning og hjemmelnoden står.</summary>
    [Fact]
    public async Task Ny_versjon_med_endret_tekst_flagger_kanten()
    {
        await using var db = _fixture.NyDbContext();
        var (lov, eid) = await OppsettAsync(db);
        var (a, b) = (await VirksomhetAsync(db, "A"), await VirksomhetAsync(db, "B"));
        var t = new StrukturkantTjeneste(db);
        var kant = (await t.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, "klage", Kantnode.Virksomhet(b), Kantnode.Virksomhet(a),
            HjemmelRettskildeId: lov, HjemmelEid: eid, Kunnskapsstatus: "tekstfunn"), "Kari Jurist")).Kant;
        var noder = (await db.RettskildeNoder.Where(n => n.RettskildeId == lov).ToListAsync())
            .Select(n => new HjemmelEndretFlagg.Nodeavtrykk(n.Eid, n.Overskrift, n.TekstHash)).ToList();
        var bruker = Unik("resynk");

        await HjemmelEndretFlagg.FlaggForNyVersjonAsync(db, lov, noder, bruker, default);
        await db.SaveChangesAsync();
        Assert.Null((await db.Strukturkanter.AsNoTracking().SingleAsync(k => k.Id == kant.Id)).HjemmelEndretTidspunkt);

        var endret = noder.Select(n => n.Eid.StartsWith(eid + "/", StringComparison.Ordinal) ? n with { TekstHash = "annen" } : n).ToList();
        Assert.True(await HjemmelEndretFlagg.FlaggForNyVersjonAsync(db, lov, endret, bruker, default) >= 1);
        await db.SaveChangesAsync();
        await HjemmelEndretFlagg.FlaggForNyVersjonAsync(db, lov, endret, bruker, default);
        await db.SaveChangesAsync();

        var etter = await db.Strukturkanter.AsNoTracking().SingleAsync(k => k.Id == kant.Id);
        Assert.NotNull(etter.HjemmelEndretTidspunkt);
        Assert.Equal(("tekstfunn", "godkjent", kant.HjemmelNodeId), (etter.Kunnskapsstatus, etter.Status, etter.HjemmelNodeId));
        Assert.Equal(1, await db.Proveniens.CountAsync(p => p.EntitetId == kant.Id && p.EndretAv == bruker)); // flagget én gang
    }
}
