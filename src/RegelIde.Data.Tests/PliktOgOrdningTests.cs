using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #353 «pliktrelasjoner mellom parter (kategori P) og nodetypen ordning», 2026-10-09] <see cref="StrukturkantTjeneste"/>
/// og <see cref="OmradeOppslagTjeneste"/> mot ekte embedded Postgres.
/// <para>
/// Spørsmålene (sakens AC1, AC2 og AC5): Kan en plikt overfor en motpart lagres med modalitet, også uten motpart (betaling der
/// mottakeren ikke står i teksten) og mellom medlemmer av samme klasse? Avvises modalitet utenfor P og ukjente verdier? Kan
/// folketrygden lagres som ORDNING med undertype, forvaltet av et organ (R forvaltes_av) — og avvises en ordning der den ikke hører
/// hjemme? Og S6: «Hvem har kommune X samarbeidsplikt med?» gir klassen og et synlig hull når medlemskap/område mangler, og
/// konkrete RHF-er når de finnes.
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class PliktOgOrdningTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public PliktOgOrdningTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static string Unik(string prefiks) => $"{prefiks}-{Guid.NewGuid():N}";

    private sealed record Oppsett(Guid LovId, string ParagrafEid, string AnnenParagrafEid);

    private static async Task<Oppsett> NyttOppsettAsync(RegelIdeDbContext db)
    {
        await Strukturkanter.SeedStartsettAsync(db);
        var lovId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
        var paragrafer = await db.RettskildeNoder.Where(n => n.RettskildeId == lovId && n.NodeType == "paragraf")
            .OrderBy(n => n.Sorteringsrekkefolge).Take(2).ToListAsync();
        return new Oppsett(lovId, paragrafer[0].Eid, paragrafer[1].Eid);
    }

    private static async Task<Guid> NyVirksomhetAsync(RegelIdeDbContext db, string navn, string? aktortype = null, string? ordningstype = null)
    {
        var v = new Virksomhet { Id = Guid.NewGuid(), Navn = Unik(navn), Aktortype = aktortype, Ordningstype = ordningstype };
        db.Virksomheter.Add(v);
        await db.SaveChangesAsync();
        return v.Id;
    }

    private static async Task<Guid> NyttBegrepAsync(RegelIdeDbContext db, string nodetype, Guid lovId, string prefiks) =>
        (await new VirksomhetsbegrepTjeneste(db).OpprettGruppebegrepAsync(nodetype, lovId, Unik(prefiks), "Kari Jurist")).Id;

    // ---------------- P: plikt overfor motpart ----------------

    [Fact]
    public async Task P_samarbeidsplikt_lagres_med_modalitet_og_visningstekst_fra_begge_sider()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var reguleringsmyndigheten = await NyVirksomhetAsync(db, "Reguleringsmyndigheten");
        var acer = await NyVirksomhetAsync(db, "ACER");
        var tjeneste = new StrukturkantTjeneste(db);

        // energiloven § 2-5 fjerde ledd: «Reguleringsmyndigheten skal samarbeide med andre lands reguleringsmyndigheter …»
        var r = await tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Plikt, "samarbeid", Kantnode.Virksomhet(reguleringsmyndigheten), Kantnode.Virksomhet(acer),
            HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid, Modalitet: "skal",
            AvgrensningTekst: "i samsvar med Norges EØS-rettslige forpliktelser"), "Kari Jurist");
        Assert.Equal(("P", "samarbeid", "skal"), (r.Kant.Kategori, r.Kant.Typekode, r.Kant.Modalitet));

        var fraSiden = Assert.Single(await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(reguleringsmyndigheten), Strukturkanter.Plikt));
        Assert.StartsWith("har samarbeidsplikt (skal) overfor ACER", fraSiden.Visningstekst);
        Assert.Equal("skal", fraSiden.Modalitet);
        var tilSiden = Assert.Single(await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(acer)));
        Assert.EndsWith(" har samarbeidsplikt overfor denne", tilSiden.Visningstekst);

        // Gjensidighet sluttes aldri (L13): ingen kant ACER → reguleringsmyndigheten ble laget.
        Assert.Equal(1, await db.Strukturkanter.CountAsync(k => k.FraVirksomhetId == acer || k.TilVirksomhetId == acer));

        // «skal» og «kan» samarbeide er to utsagn (modaliteten er identitet); samme modalitet igjen er samme kant.
        var kan = await tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Plikt, "samarbeid", Kantnode.Virksomhet(reguleringsmyndigheten), Kantnode.Virksomhet(acer),
            HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid, Modalitet: "kan",
            AvgrensningTekst: "i samsvar med Norges EØS-rettslige forpliktelser"), "Kari Jurist");
        Assert.True(kan.VarNy);
        var igjen = await tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Plikt, "samarbeid", Kantnode.Virksomhet(reguleringsmyndigheten), Kantnode.Virksomhet(acer),
            HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid, Modalitet: "skal",
            AvgrensningTekst: "i samsvar med Norges EØS-rettslige forpliktelser"), "Kari Jurist");
        Assert.False(igjen.VarNy);
    }

    [Fact]
    public async Task P_betaling_uten_mottaker_krever_objekt_og_bor_leses_med_o()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var rhf = await NyttBegrepAsync(db, Nodetyper.Klasse, o.LovId, "det regionale helseforetaket i pasientens bostedsregion");
        var tjeneste = new StrukturkantTjeneste(db);

        // «Utgiftene til X» sier ikke hvem som får pengene: til = null, men da må objektet si hva plikten gjelder.
        var feil = await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Plikt, "betaling", Kantnode.Begrep(rhf), Til: null, HjemmelRettskildeId: o.LovId), "Kari Jurist"));
        Assert.Contains("uten motpart må si HVA plikten gjelder", feil.Message);

        var ok = await tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Plikt, "betaling", Kantnode.Begrep(rhf), Til: null, HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid,
            Objekt: "utgifter til behandling, forpleining og reise som ytes av andre tjenesteytere", Modalitet: "bor"), "Kari Jurist");
        var v = Assert.Single(await tjeneste.HentForNodeAsync(Kantnode.Begrep(rhf), Strukturkanter.Plikt));
        Assert.Equal(ok.Kant.Id, v.Id);
        Assert.Null(v.Til);
        Assert.Equal("har betalingsplikt (bør) utgifter til behandling, forpleining og reise som ytes av andre tjenesteytere", v.Visningstekst);
    }

    [Fact]
    public async Task P_mellom_medlemmer_av_samme_klasse_er_lov_men_ikke_en_virksomhet_overfor_seg_selv()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        // samisk opplæringsforskrift § 2: «bør samiskopplæringen skje i samarbeid mellom flere organ som omfattes av …»
        var organer = await NyttBegrepAsync(db, Nodetyper.Klasse, o.LovId, "organ som omfattes av samelovens kapittel 3");
        var kommunen = await NyVirksomhetAsync(db, "Karasjok kommune");
        var tjeneste = new StrukturkantTjeneste(db);

        var ok = await tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Plikt, "samarbeid", Kantnode.Begrep(organer), Kantnode.Begrep(organer),
            HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid, Modalitet: "bor"), "Kari Jurist");
        Assert.Equal(ok.Kant.FraBegrepId, ok.Kant.TilBegrepId);

        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Plikt, "samarbeid", Kantnode.Virksomhet(kommunen), Kantnode.Virksomhet(kommunen),
            HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid), "Kari Jurist"));
    }

    [Theory]
    [InlineData("P", "samarbeid", "plikter")] // ukjent modalitet
    [InlineData("K", "klage", "skal")] // modalitet bare på P
    [InlineData("R", "eies_av", "skal")]
    public async Task Modalitet_utenfor_P_eller_utenfor_lista_avvises(string kategori, string kode, string modalitet)
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var (a, b) = (await NyVirksomhetAsync(db, "A"), await NyVirksomhetAsync(db, "B"));
        var feil = await Assert.ThrowsAsync<ArgumentException>(() => new StrukturkantTjeneste(db).OpprettAsync(new NyStrukturkant(
            kategori, kode, Kantnode.Virksomhet(a), Kantnode.Virksomhet(b), HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid,
            Modalitet: modalitet), "Kari Jurist"));
        Assert.Contains("odalitet", feil.Message);
    }

    [Fact]
    public async Task Typekonfigurasjonen_har_de_seks_plikttypene_og_forvaltes_av_og_tilhorer()
    {
        await using var db = _fixture.NyDbContext();
        await Strukturkanter.SeedStartsettAsync(db);
        var p = await db.RelasjonsTypeKonfigurasjoner.Where(t => t.Kategori == Strukturkanter.Plikt).Select(t => t.Kode).ToListAsync();
        Assert.Equal(["avtale", "betaling", "bistand", "informasjon", "konsultasjon", "samarbeid"], p.Order());
        Assert.True(await db.RelasjonsTypeKonfigurasjoner.AnyAsync(t => t.Kategori == "R" && t.Kode == Strukturkanter.ForvaltesAv));
        Assert.True(await db.RelasjonsTypeKonfigurasjoner.AnyAsync(t => t.Kategori == "G" && t.Kode == Strukturkanter.Tilhorer));
        Assert.False(await db.RelasjonsTypeKonfigurasjoner.AnyAsync(t => t.Kategori == Strukturkanter.Plikt && t.Familie != null));
    }

    // ---------------- Ordning ----------------

    [Fact]
    public async Task Folketrygden_som_ordning_forvaltes_av_et_organ_og_er_pliktsubjekt_for_betaling()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var folketrygden = await NyVirksomhetAsync(db, "Folketrygden", Nodetyper.Ordning, "trygdeordning");
        var hdir = await NyVirksomhetAsync(db, "Helsedirektoratet", "organ");
        var tjeneste = new StrukturkantTjeneste(db);

        // folketrygdloven § 21-11 a første ledd: «Helsedirektoratet skal forvalte kapittel 5 …»
        var forvaltes = await tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Relasjon, Strukturkanter.ForvaltesAv, Kantnode.Virksomhet(folketrygden), Kantnode.Virksomhet(hdir),
            HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid, AvgrensningTekst: "folketrygdloven kapittel 5"), "Kari Jurist");
        Assert.True(forvaltes.VarNy);
        var v = Assert.Single(await tjeneste.HentForNodeAsync(Kantnode.Virksomhet(folketrygden), Strukturkanter.Relasjon));
        Assert.Equal(Nodetyper.Ordning, v.Fra.Nodetype);
        Assert.StartsWith("forvaltes av Helsedirektoratet", v.Visningstekst);

        // spesialisthelsetjenesteloven § 5-3 annet ledd: «Folketrygden skal dekke behandlings- og forpleiningsutgifter …»
        var betaling = await tjeneste.OpprettAsync(new NyStrukturkant(
            Strukturkanter.Plikt, "betaling", Kantnode.Virksomhet(folketrygden), Til: null, HjemmelRettskildeId: o.LovId,
            HjemmelEid: o.AnnenParagrafEid, Objekt: "behandlings- og forpleiningsutgifter for pasient som ikke har bosted i riket",
            Modalitet: "skal"), "Kari Jurist");
        Assert.True(betaling.VarNy);
    }

    [Fact]
    public async Task En_ordning_avvises_der_den_ikke_hører_hjemme_og_forvaltes_av_krever_en_ordning()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var fondet = await NyVirksomhetAsync(db, "Fondet", Nodetyper.Ordning, "fond");
        var dep = await NyVirksomhetAsync(db, "Departementet", "organ");
        var staten = await NyVirksomhetAsync(db, "Staten", "rettssubjekt");
        var tjeneste = new StrukturkantTjeneste(db);

        Task<StrukturkantOpprettet> Kant(string kategori, string kode, Guid fra, Guid til) => tjeneste.OpprettAsync(new NyStrukturkant(
            kategori, kode, Kantnode.Virksomhet(fra), Kantnode.Virksomhet(til), HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid),
            "Kari Jurist");

        // En ordning har ingen kompetanse og er ikke en aktør i R eies_av …
        Assert.Contains("ikke en aktør", (await Assert.ThrowsAsync<ArgumentException>(() => Kant("K", "klage", fondet, dep))).Message);
        Assert.Contains("ikke en aktør", (await Assert.ThrowsAsync<ArgumentException>(() => Kant("R", "eies_av", fondet, staten))).Message);
        // … og som til-node bare i P.
        Assert.Contains("bare være til-node i P", (await Assert.ThrowsAsync<ArgumentException>(() => Kant("K", "tilsyn", dep, fondet))).Message);
        // forvaltes_av og tilhorer krever at fra-noden ER en ordning — aktørtypen gjettes ikke.
        Assert.Contains("går fra en ORDNING", (await Assert.ThrowsAsync<ArgumentException>(() => Kant("R", Strukturkanter.ForvaltesAv, dep, staten))).Message);
        // G tilhorer fra ordning til rettssubjekt (bare når hjemlet — her har den hjemmel).
        Assert.True((await Kant("G", Strukturkanter.Tilhorer, fondet, staten)).VarNy);
        // Betaling TIL en ordning er lov.
        Assert.True((await Kant("P", "betaling", dep, fondet)).VarNy);
    }

    [Fact]
    public async Task Ordningstypen_finnes_bare_paa_en_ordning_og_er_en_lukket_liste()
    {
        await using var db = _fixture.NyDbContext();
        Assert.True(Nodetyper.ErGyldigOrdningstype(Nodetyper.Ordning, "trygdeordning"));
        Assert.True(Nodetyper.ErGyldigOrdningstype("organ", null));
        Assert.False(Nodetyper.ErGyldigOrdningstype("organ", "fond"));
        Assert.False(Nodetyper.ErGyldigOrdningstype(Nodetyper.Ordning, "stiftelse"));
        // CHECK ck_virksomheter_ordningstype håndhever det samme i basen.
        db.Virksomheter.Add(new Virksomhet { Id = Guid.NewGuid(), Navn = Unik("Feil"), Aktortype = "organ", Ordningstype = "fond" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    // ---------------- S6: «Hvem har kommune X samarbeidsplikt med?» ----------------

    private static async Task<Guid> NyttOmradeAsync(RegelIdeDbContext db, string type, string term, string? kode = null)
    {
        var b = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = Nodetyper.Omrade, Omradetype = type, Omradekode = kode, Term = Unik(term),
            Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(b);
        await db.SaveChangesAsync();
        return b.Id;
    }

    [Fact]
    public async Task S6_samarbeidsplikt_gir_klassen_og_et_synlig_hull_og_konkret_RHF_naar_omradet_er_lastet()
    {
        await using var db = _fixture.NyDbContext();
        var o = await NyttOppsettAsync(db);
        var tjeneste = new StrukturkantTjeneste(db);
        var oppslag = new OmradeOppslagTjeneste(db, tjeneste);
        var kommunenummer = "T" + Guid.NewGuid().ToString("N")[..6];

        // Kommunen som område og rettssubjekt (A har_ansvarsomrade til eget territorium, samme kommunenummer — #312-regelen).
        var kommuneOmrade = await NyttOmradeAsync(db, Omradetyper.Kommune, "Testkommune", kommunenummer);
        var fylke = await NyttOmradeAsync(db, Omradetyper.Fylke, "Testfylke", "T" + Guid.NewGuid().ToString("N")[..4]);
        var helseregion = await NyttOmradeAsync(db, Omradetyper.Helseregion, "Helseregion Test");
        var kommune = new Virksomhet { Id = Guid.NewGuid(), Navn = Unik("Testkommune"), Aktortype = "rettssubjekt", Kommunenummer = kommunenummer };
        db.Virksomheter.Add(kommune);
        await db.SaveChangesAsync();
        Task<StrukturkantOpprettet> Kant(string kategori, string kode, Kantnode fra, Kantnode? til, string? modalitet = null) =>
            tjeneste.OpprettAsync(new NyStrukturkant(kategori, kode, fra, til, HjemmelRettskildeId: o.LovId, HjemmelEid: o.ParagrafEid,
                Modalitet: modalitet), "test");
        await Kant("A", "har_ansvarsomrade", Kantnode.Virksomhet(kommune.Id), Kantnode.Begrep(kommuneOmrade));
        await Kant("O", "bestar_av", Kantnode.Begrep(fylke), Kantnode.Begrep(kommuneOmrade));

        // «Kommunen skal inngå samarbeidsavtale med det regionale helseforetaket i helseregionen» (hotl. § 6-1): ÉN kant mellom klasser.
        var kommunene = await NyttBegrepAsync(db, Nodetyper.Klasse, o.LovId, "kommunen");
        var rhfKlassen = await NyttBegrepAsync(db, Nodetyper.Klasse, o.LovId, "det regionale helseforetaket i helseregionen");
        await Kant("P", "avtale", Kantnode.Begrep(kommunene), Kantnode.Begrep(rhfKlassen), "skal");

        // 1) Verken kommunens medlemskap i «kommunen» eller RHF-klassens medlemmer er lastet: klassen og to synlige hull.
        var svar = await oppslag.PlikterForKommunenummerAsync(kommunenummer, "avtale");
        Assert.NotNull(svar);
        Assert.Equal(kommune.Id, svar.Kommunevirksomhet!.Id);
        // (Den delte testbasen kan ha andre klassekanter uten medlemmer — de listes også, med hull; vi ser på vår.)
        var treff = Assert.Single(svar.Plikter, t => t.Kant.Fra.Id == kommunene);
        Assert.Equal("klasse_uten_registrert_medlemskap", treff.Grunnlag);
        Assert.Contains("ingen registrerte medlemmer", treff.GrunnlagHull);
        Assert.Equal("mangler", treff.Motpart.Status);
        Assert.Contains("#340", treff.Motpart.Hull);
        Assert.Empty(treff.Motpart.Ider);

        // 2) RHF-et er medlem av klassen, men helseregionen er ikke koblet til kommunens fylke: fortsatt hull, ingen gjetning.
        var rhf = await NyVirksomhetAsync(db, "Helse Test RHF", "rettssubjekt");
        await Kant("M", "medlem_av", Kantnode.Virksomhet(rhf), Kantnode.Begrep(rhfKlassen));
        await Kant("A", "har_ansvarsomrade", Kantnode.Virksomhet(rhf), Kantnode.Begrep(helseregion));
        svar = await oppslag.PlikterForKommunenummerAsync(kommunenummer, "avtale");
        treff = Assert.Single(svar!.Plikter, t => t.Kant.Fra.Id == kommunene);
        Assert.Equal("mangler", treff.Motpart.Status);
        Assert.Contains("ansvarsområde som dekker kommunen", treff.Motpart.Hull);

        // 3) Helseregionen består av fylket (vedtektene, ekstern kilde): kommunen ligger i helseregionen, RHF-et er motparten.
        await Kant("O", "bestar_av", Kantnode.Begrep(helseregion), Kantnode.Begrep(fylke));
        // … og kommunen er registrert medlem av «kommunen».
        await Kant("M", "medlem_av", Kantnode.Virksomhet(kommune.Id), Kantnode.Begrep(kommunene));
        svar = await oppslag.PlikterForKommunenummerAsync(kommunenummer, "avtale");
        treff = Assert.Single(svar!.Plikter, t => t.Kant.Fra.Id == kommunene);
        Assert.Equal("medlem_av", treff.Grunnlag);
        Assert.Null(treff.GrunnlagHull);
        Assert.Equal(("entydig", rhf), (treff.Motpart.Status, Assert.Single(treff.Motpart.Ider)));

        // Typefilteret: ingen samarbeidsplikt registrert, bare avtaleplikt.
        Assert.DoesNotContain((await oppslag.PlikterForKommunenummerAsync(kommunenummer, "samarbeid"))!.Plikter, t => t.Kant.Fra.Id == kommunene);
        await Assert.ThrowsAsync<ArgumentException>(() => oppslag.PlikterForKommunenummerAsync(kommunenummer, "møte"));
    }
}
