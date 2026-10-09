using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #355 «avslutning speiler innsetting», Johanns beslutninger 2026-10-09] <see cref="StrukturkantTjeneste"/> og
/// <see cref="OmradeOppslagTjeneste"/> mot ekte embedded Postgres.
/// <para>
/// Spørsmålene (sakens AC1 og AC4): Kan avslutningen lagres som avsetting med undertype (avsetting/oppsigelse/avskjed), utnevning og
/// konstitusjon som oppnevning, tilbakekall som vedtak — og avvises en undertype typen ikke har? Er forelegging borte som K-type? Kan
/// en STILLING (rolle) være G del_av et organ («Høyesteretts direktør» del_av Høyesterett)? Og S6: «hvem kan sette inn en fast
/// dommer?» gir bare Kongen, og «hvem er ankeinstans for X tingrett?» gir én lagmannsrett, avledet via lagsogn → lagdømme.
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class AvslutningOgAnkeinstansTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public AvslutningOgAnkeinstansTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static string Unik(string prefiks) => $"{prefiks}-{Guid.NewGuid():N}";

    private sealed record Oppsett(Guid LovId, string ParagrafEid);

    private static async Task<Oppsett> NyttOppsettAsync(RegelIdeDbContext db)
    {
        await Strukturkanter.SeedStartsettAsync(db);
        var lovId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
        var paragraf = await db.RettskildeNoder.Where(n => n.RettskildeId == lovId && n.NodeType == "paragraf")
            .OrderBy(n => n.Sorteringsrekkefolge).FirstAsync();
        return new Oppsett(lovId, paragraf.Eid);
    }

    private static async Task<Guid> NyVirksomhetAsync(RegelIdeDbContext db, string navn, string? aktortype = "organ")
    {
        var v = new Virksomhet { Id = Guid.NewGuid(), Navn = Unik(navn), Aktortype = aktortype };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return v.Id;
    }

    private static async Task<Guid> NyttBegrepAsync(RegelIdeDbContext db, string nodetype, Guid lovId, string prefiks) =>
        (await new VirksomhetsbegrepTjeneste(db).OpprettGruppebegrepAsync(nodetype, lovId, Unik(prefiks), "Kari Jurist")).Id;

    private static async Task<Guid> NyttOmradeAsync(RegelIdeDbContext db, string type, string term)
    {
        var b = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = Nodetyper.Omrade, Omradetype = type, Term = Unik(term),
            Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(b);
        await db.SaveChangesAsync();
        return b.Id;
    }

    // ---------------- Undertypene og forelegging ----------------

    [Fact]
    public async Task Avslutning_utnevning_konstitusjon_og_tilbakekall_lagres_med_undertype_og_feil_undertype_avvises()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var kongen = await NyVirksomhetAsync(db, "Kongen");
        var styret = await NyVirksomhetAsync(db, "Styret");
        var dommere = await NyttBegrepAsync(db, Nodetyper.Rolle, o.LovId, "dommere");
        var dagligLeder = await NyttBegrepAsync(db, Nodetyper.Rolle, o.LovId, "daglig leder");
        var tjeneste = new StrukturkantTjeneste(db);
        Task<StrukturkantOpprettet> K(string type, Guid fra, Guid? tilBegrep, string? undertype, string polaritet = "positiv", string? objekt = null) =>
            tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, type, Kantnode.Virksomhet(fra),
                tilBegrep is { } t ? Kantnode.Begrep(t) : null, HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid,
                Objekt: objekt, Polaritet: polaritet, Undertype: undertype), "Kari Jurist");

        // Domstolloven § 55 første ledd: utnevning (embete). Helseforetaksloven § 36 første ledd: oppsigelse og avskjed (to rader, L13).
        var utnevning = await K(Strukturkanter.Oppnevning, kongen, dommere, "utnevning");
        var oppsigelse = await K(Strukturkanter.Avsetting, styret, dagligLeder, "oppsigelse");
        var avskjed = await K(Strukturkanter.Avsetting, styret, dagligLeder, "avskjed");
        Assert.True(avskjed.VarNy); // undertypen er identitet: oppsigelse og avskjed er to utsagn
        var konstitusjon = await K(Strukturkanter.Oppnevning, kongen, null, "konstitusjon", objekt: "konstitusjon av ny dommer");
        var tilbakekall = await K(Strukturkanter.Vedtak, kongen, null, "tilbakekall", objekt: "tilbakekall av godkjenning");
        Assert.Equal(["utnevning", "oppsigelse", "avskjed", "konstitusjon", "tilbakekall"],
            new[] { utnevning, oppsigelse, avskjed, konstitusjon, tilbakekall }.Select(r => r.Kant.Undertype));

        var fraStyret = await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(styret), Strukturkanter.Kompetanse);
        Assert.Contains(fraStyret, v => v.Visningstekst.StartsWith("har avsettingskompetanse (avskjed) overfor"));

        // En undertype typen ikke har, avvises — i tjenesten og i databasen (ck_strukturkanter_undertype).
        var feil = await Assert.ThrowsAsync<ArgumentException>(() => K(Strukturkanter.Avsetting, styret, dagligLeder, "anke"));
        Assert.Contains("avsetting, oppsigelse, avskjed", feil.Message);
        await Assert.ThrowsAsync<ArgumentException>(() => K(Strukturkanter.Vedtak, kongen, null, "avskjed", objekt: "x"));
        db.Strukturkanter.Add(new StrukturkantEntitet
        {
            Id = Guid.NewGuid(), Kategori = "K", Typekode = Strukturkanter.Vedtak, FraVirksomhetId = kongen, Objekt = "x",
            Undertype = "utnevning", Polaritet = "positiv", HjemmelRettskildeId = o.LovId, Status = "validert",
            OppdagelsesKilde = "manuell", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Forelegging_er_ikke_lenger_en_kompetansetype()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var domstol = await NyVirksomhetAsync(db, "Domstolen");
        var efta = await NyVirksomhetAsync(db, "EFTA-domstolen");
        Assert.DoesNotContain(Strukturkanter.Kompetansetyper, t => t.Kode == "forelegging");
        Assert.False(await db.RelasjonsTypeKonfigurasjoner.AnyAsync(k => k.Kategori == "K" && k.Kode == "forelegging"));
        await Assert.ThrowsAsync<ArgumentException>(() => new StrukturkantTjeneste(db).OpprettAsync(new NyStrukturkant(
            Strukturkanter.Kompetanse, "forelegging", Kantnode.Virksomhet(domstol), Kantnode.Virksomhet(efta),
            HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid, Polaritet: "positiv"), "Kari Jurist"));

        // Domstolloven § 51 a (beslutning 4): rådgivende svar → P konsultasjon med modalitet kan, ODA artikkel 34 i objektet/kommentaren.
        var p = await new StrukturkantTjeneste(db).OpprettAsync(new NyStrukturkant(
            Strukturkanter.Plikt, "konsultasjon", Kantnode.Virksomhet(domstol), Kantnode.Virksomhet(efta),
            HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid, Objekt: "rådgivende tolkningsuttalelse", Polaritet: "positiv",
            Modalitet: "kan"), "Kari Jurist");
        Assert.Equal(("P", "konsultasjon", "kan"), (p.Kant.Kategori, p.Kant.Typekode, p.Kant.Modalitet));
    }

    /// <summary>Johanns beslutning 1 på u22/u165: «Høyesterett skal ha en direktør» er G del_av fra STILLINGEN (en rolle knyttet
    /// til nettopp Høyesterett) til organet — ikke T (klassenivå). G må tillate rolle som fra; ingen ny type.</summary>
    [Fact]
    public async Task G_del_av_tillater_en_stilling_som_fra_og_ingen_modalitet()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var hoyesterett = await NyVirksomhetAsync(db, "Høyesterett");
        var direktor = await NyttBegrepAsync(db, Nodetyper.Rolle, o.LovId, "Høyesteretts direktør");
        var tjeneste = new StrukturkantTjeneste(db);
        var r = await tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Organtilhorighet, "del_av", Kantnode.Begrep(direktor), Kantnode.Virksomhet(hoyesterett),
            HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid, Polaritet: "positiv"), "Kari Jurist");
        Assert.Equal(("G", "del_av", direktor, hoyesterett), (r.Kant.Kategori, r.Kant.Typekode, r.Kant.FraBegrepId!.Value, r.Kant.TilVirksomhetId!.Value));
        // Beslutning 2: ingen modalitet på strukturkanter.
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Organtilhorighet, "del_av", Kantnode.Begrep(direktor), Kantnode.Virksomhet(hoyesterett),
            HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid, Polaritet: "positiv", Modalitet: "skal"), "Kari Jurist"));
    }

    // ---------------- S6 ----------------

    /// <summary>AC4: «Hvem kan sette inn en fast dommer?» — uten undertypen svarer oppnevningskantene til dommerrollen Kongen,
    /// Innstillingsrådet og domstollederen; med undertypen utnevning (embete) bare Kongen.</summary>
    [Fact]
    public async Task S6_fast_dommer_gir_bare_Kongen_med_undertypen_utnevning()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var kongen = await NyVirksomhetAsync(db, "Kongen");
        var innstillingsradet = await NyVirksomhetAsync(db, "Innstillingsrådet for dommere");
        var domstolleder = await NyttBegrepAsync(db, Nodetyper.Rolle, o.LovId, "domstolens leder");
        var dommere = await NyttBegrepAsync(db, Nodetyper.Rolle, o.LovId, "dommere");
        var tjeneste = new StrukturkantTjeneste(db);
        Task<StrukturkantOpprettet> Oppnevning(Kantnode fra, string undertype, string? avgrensning = null) =>
            tjeneste.OpprettAsync(new NyStrukturkant(Strukturkanter.Kompetanse, Strukturkanter.Oppnevning, fra, Kantnode.Begrep(dommere),
                HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid, Polaritet: "positiv", Undertype: undertype,
                AvgrensningTekst: avgrensning), "Kari Jurist");
        await Oppnevning(Kantnode.Virksomhet(kongen), "utnevning");
        await Oppnevning(Kantnode.Virksomhet(innstillingsradet), "konstitusjon");
        await Oppnevning(Kantnode.Begrep(domstolleder), "konstitusjon", "inntil tre måneder");
        await Oppnevning(Kantnode.Virksomhet(kongen), "konstitusjon", "lengre enn ett år eller Høyesterett");

        var alle = await tjeneste.HentForNodeAsync(Kantnode.Begrep(dommere), Strukturkanter.Kompetanse);
        Assert.Equal(3, alle.Select(v => v.Fra.Id).Distinct().Count());
        var fast = alle.Where(v => v.Undertype == "utnevning").ToList();
        Assert.Equal(kongen, Assert.Single(fast).Fra.Id);
        // Fra dommerrollens side står undertypen i teksten (kaldtesten 2026-10-09: den forsvant fra motpartens side).
        Assert.EndsWith(" har oppnevningskompetanse (utnevning) overfor denne", Assert.Single(fast).Visningstekst);
    }

    /// <summary>AC4: «Hvem er ankeinstans for X tingrett?» gir ÉN lagmannsrett, avledet: K overprøving/anke fra hver lagmannsrett
    /// til klassen tingrett (avgrenset til eget lagdømme), og kjeden tingrett → lagsogn → lagdømme (#345). Uten registrert
    /// medlemskap i klassen: hull, ingen gjettet instans.</summary>
    [Fact]
    public async Task S6_ankeinstans_for_tingrett_er_en_lagmannsrett_avledet_via_lagdommet()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var tjeneste = new StrukturkantTjeneste(db);
        var oppslag = new OmradeOppslagTjeneste(db, tjeneste);
        Task<StrukturkantOpprettet> Kant(string kategori, string kode, Kantnode fra, Kantnode til, string? undertype = null, string? avgrensning = null) =>
            tjeneste.OpprettAsync(new NyStrukturkant(kategori, kode, fra, til, HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid,
                Polaritet: "positiv", Undertype: undertype, AvgrensningTekst: avgrensning), "test");

        // To lagdømmer, hvert med sitt lagsogn og sin lagmannsrett; tingretten sogner til lagsognet i det første.
        var agderLagdomme = await NyttOmradeAsync(db, Omradetyper.Lagdomme, "Agder lagdømme");
        var gulatingLagdomme = await NyttOmradeAsync(db, Omradetyper.Lagdomme, "Gulating lagdømme");
        var agderLagsogn = await NyttOmradeAsync(db, Omradetyper.Lagsogn, "lagsogn Agder");
        var gulaLagsogn = await NyttOmradeAsync(db, Omradetyper.Lagsogn, "lagsogn Gula");
        var kommune = await NyttOmradeAsync(db, Omradetyper.Kommune, "Kristiansand");
        var agderLr = await NyVirksomhetAsync(db, "AGDER LAGMANNSRETT");
        var gulatingLr = await NyVirksomhetAsync(db, "GULATING LAGMANNSRETT");
        var tingrett = await NyVirksomhetAsync(db, "AGDER TINGRETT");
        await Kant("O", "bestar_av", Kantnode.Begrep(agderLagdomme), Kantnode.Begrep(agderLagsogn));
        await Kant("O", "bestar_av", Kantnode.Begrep(gulatingLagdomme), Kantnode.Begrep(gulaLagsogn));
        await Kant("A", "har_ansvarsomrade", Kantnode.Virksomhet(agderLr), Kantnode.Begrep(agderLagdomme));
        await Kant("A", "har_ansvarsomrade", Kantnode.Virksomhet(gulatingLr), Kantnode.Begrep(gulatingLagdomme));
        await Kant("A", "har_ansvarsomrade", Kantnode.Virksomhet(tingrett), Kantnode.Begrep(kommune));
        await Kant("A", OmraderegisterSeed.SognerTil, Kantnode.Virksomhet(tingrett), Kantnode.Begrep(agderLagsogn));

        // Inndelingsforskriften § 10 første ledd: K overprøving/anke fra lagmannsretten til tingretten (klassen), eget lagdømme.
        var tingrettene = await NyttBegrepAsync(db, Nodetyper.Klasse, o.LovId, "tingrettene");
        foreach (var lr in new[] { agderLr, gulatingLr })
        {
            await Kant("K", Strukturkanter.Overproving, Kantnode.Virksomhet(lr), Kantnode.Begrep(tingrettene), "anke", "eget lagdømme");
        }

        // 1) Tingretten er ikke registrert medlem av klassen: kantene listes med hull, men ingen instans velges.
        var svar = await oppslag.AnkeinstansAsync(tingrett);
        Assert.NotNull(svar);
        Assert.Equal("mangler", svar.Status);
        var usikker = Assert.Single(svar.Kanter); // bare Agder: Gulatings ansvarsområde dekker ikke tingrettens lagsogn
        Assert.Equal("klasse_uten_registrert_medlemskap", usikker.Grunnlag);
        Assert.Contains("ingen registrerte medlemmer", usikker.GrunnlagHull);
        Assert.Equal(agderLr, Assert.Single(usikker.Instans.Ider));

        // 2) Med registrert medlemskap: ÉN lagmannsrett, avledet via lagsogn → lagdømme.
        await Kant("M", "medlem_av", Kantnode.Virksomhet(tingrett), Kantnode.Begrep(tingrettene));
        svar = await oppslag.AnkeinstansAsync(tingrett);
        Assert.Equal("entydig", svar!.Status);
        Assert.Equal(agderLr, Assert.Single(svar.Ider));
        Assert.False(Assert.Single(svar.Forslag)); // alle kantene her er validert; forslag merkes (kaldtesten viste domstolkantene som forslag)
        Assert.Equal("medlem_av", Assert.Single(svar.Kanter).Grunnlag);
        Assert.Contains(svar.Omrader, x => x.Id == agderLagdomme);
        Assert.DoesNotContain(svar.Omrader, x => x.Id == gulatingLagdomme);

        // 3) En tingrett uten lagsogn: ingen instans kan pares (hull), ingen gjettes.
        var loesTingrett = await NyVirksomhetAsync(db, "LØS TINGRETT");
        await Kant("M", "medlem_av", Kantnode.Virksomhet(loesTingrett), Kantnode.Begrep(tingrettene));
        var loes = await oppslag.AnkeinstansAsync(loesTingrett);
        Assert.Equal("mangler", loes!.Status);
        Assert.Contains(loes.Hull, h => h.Contains("ingen registrerte områder"));

        Assert.Null(await oppslag.AnkeinstansAsync(Guid.NewGuid()));
    }
}
