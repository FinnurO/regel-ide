using static RegelIde.Data.Tests.HjemmelsstatusTests;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, #501 (#472 PR 5), prosjekteierens klarsignal 2026-11-25] Videreføringen der overgangsregelen bruker lovens kortnavn, og kortnavnet
/// bare står i tittelen til den opphevede loven. Eksempelet er M13 i juristmåling 2 (data/fasit/strukturmodell/juristrunde-472/maling-2/):
/// viltressursloven § 61 andre ledd, «Forskrifter gitt i medhold av viltloven gjelder også etter at loven her har trådt i kraft.» Spørsmålene:
/// <list type="number">
/// <item>Er en forskrift etter viltloven videreført av viltressursloven § 61 andre ledd (M13)?</item>
/// <item>Gis det et svar når kortnavnet er tvetydig? (Nei.)</item>
/// <item>Gis det et svar når kortnavnet ikke står i noen tittel, eller ikke står etter hjemmelsuttrykket? (Nei.)</item>
/// <item>Får R9 i #429 samme svar?</item>
/// </list>
/// Numrene og kortnavnene er tilfeldige, så de ikke kolliderer med andre tester i den delte testbasen.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class HjemmelKortnavnTests(EmbeddedPostgresFixture fixture)
{
    /// <summary>Et tilfeldig kortnavn av bokstaver, «vilt‹x›loven».</summary>
    private static string Kortnavn(string stamme = "vilt") =>
        stamme + new string(Enumerable.Range(0, 8).Select(_ => (char)('a' + Random.Shared.Next(26))).ToArray()) + "loven";

    private sealed record M13Oppsett(RettskildeEntitet Forskrift, RettskildeEntitet Viltloven, RettskildeEntitet Viltressursloven, string Kortnavn);

    /// <summary>
    /// M13: SF/forskrift/2019-12-17-1878 med hjemmel i viltloven 1981 § 26 (og viltressursloven § 30). Viltressursloven har viltloven i
    /// «Endrer», men opphevingssetningen står bare i den kunngjorte teksten. Hjemmelsteksten til forskriften siterer tittelen med kortnavnet.
    /// </summary>
    private static async Task<(M13Oppsett O, FalskKunngjort Kunngjort)> ByggM13Async(Korpus k, string kortnavn, string? hjemmelstekstKortnavn,
        string overgangsregel)
    {
        DateOnly d1981 = new(1981, 5, 29);
        var n1981 = Nr();
        var viltloven = await k.StubAsync("lov", d1981, n1981);
        var ny = await k.DokumentAsync("lov", new(2025, 6, 20), Nr(), "NL", "Lov om jakt, fangst og felling av vilt mv. (viltressursloven)",
        [
            ("§30", null, "Forskrift om åte"),
            ("§30/ledd-1", "Departementet kan gi forskrift om utlegging av åte.", null),
            ("§61", null, "Ikrafttredelse og overgangsbestemmelser"),
            ("§61/ledd-1", "Loven gjelder fra den tiden Kongen bestemmer. Kongen kan sette i kraft de enkelte bestemmelsene til forskjellig tid.", null),
            ("§61/ledd-2", overgangsregel, null),
            ("§61/ledd-3", "Departementet kan gi overgangsbestemmelser.", null),
        ]);
        await k.EndrerAsync(ny, viltloven);
        var tittel = hjemmelstekstKortnavn is null ? "" : $" ({hjemmelstekstKortnavn})";
        var f = await k.DokumentAsync("forskrift", new(2019, 12, 17), Nr(), "SF", "Forskrift om utlegging av åte og føring av vilt",
            [("§1", null, null), ("§1/ledd-1", "Det er forbudt å legge ut åte.", null)],
            annetOm: $"Hjemmel: Fastsatt av Miljødirektoratet 17. desember 2019 med hjemmel i lov {Ref(d1981, n1981)} om jakt og fangst av vilt{tittel} "
                + "§ 26 nr. 8.\nTilføyd hjemmel: Lov 20. juni 2025 nr. 102 om jakt, fangst og felling av vilt mv. (viltressursloven) § 30.");
        await k.HjemlerAsync(f, (viltloven, "§26"), (ny, "§30"));
        var kunngjort = new FalskKunngjort(new()
        {
            [ny.Eli!] = [new KunngjortNode($"{ny.Eli}/§61", null, $"Fra samme tid oppheves lov {Ref(d1981, n1981)} om jakt og fangst av vilt.", 1)],
        });
        return (new M13Oppsett(f, viltloven, ny, kortnavn), kunngjort);
    }

    private static string Overgangsregel(string kortnavn) =>
        $"Forskrifter gitt i medhold av {kortnavn} gjelder også etter at loven her har trådt i kraft.";

    private static async Task<HjemmelsstatusEntitet> ViltlovradAsync(RegelIdeDbContext db, M13Oppsett o, FalskKunngjort kunngjort)
    {
        await new HjemmelsstatusRegel(db, kunngjort).KjorAsync(skriv: true, "Test", lesLovtidend: true, forskriftIder: [o.Forskrift.Id]);
        var (_, rader) = (await new HjemmelsstatusRegel(db).LesAsync(o.Forskrift.Id))!.Value;
        return Assert.Single(rader.Values, r => r.HjemmelEli == o.Viltloven.Eli);
    }

    // ---- 1. M13 -------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task M13_Er_aateforskriften_viderefort_av_viltressursloven_paragraf_61_andre_ledd()
    {
        await using var db = fixture.NyDbContext();
        var kort = Kortnavn();
        var (o, kunngjort) = await ByggM13Async(new Korpus(db), kort, kort, Overgangsregel(kort));

        var rad = await ViltlovradAsync(db, o, kunngjort);
        Assert.Equal(Hjemmelsstatus.EnLovtidend, rad.Bevistrinn);
        Assert.Equal((Hjemmelsstatus.Viderefort, "V4"), (rad.Status, rad.VidereforingForm));
        Assert.Equal($"{o.Viltressursloven.Eli}/§61/ledd-2", rad.VidereforingEid);
        Assert.Equal(Overgangsregel(kort), rad.VidereforingSitat);
        Assert.Equal(Hjemmelsstatus.Dekket, rad.VidereforingDekning);
        // Kortnavnet er koblet til loven av tittelen, ikke av setningen: normalisering, ikke tekstfunn.
        Assert.Equal(Strukturkanter.Normalisering, rad.Kunnskapsstatus);
        var (samlet, _) = (await new HjemmelsstatusRegel(db).LesAsync(o.Forskrift.Id))!.Value;
        Assert.Equal(Hjemmelsstatus.SamletViderefort, samlet.Status);
    }

    [Fact]
    public async Task M13_Nynorsk_kortnavn_i_tittelen_gir_samme_svar_som_bokmal()
    {
        await using var db = fixture.NyDbContext();
        var kort = Kortnavn();
        var nynorsk = kort[..^"loven".Length] + "lova";
        var (o, kunngjort) = await ByggM13Async(new Korpus(db), kort, nynorsk, Overgangsregel(kort));
        var rad = await ViltlovradAsync(db, o, kunngjort);
        Assert.Equal((Hjemmelsstatus.Viderefort, "V4"), (rad.Status, rad.VidereforingForm));
    }

    // ---- 2. Tvetydig kortnavn -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Tvetydig_Kortnavnet_brukes_om_en_annen_lov_og_gir_ikke_svar()
    {
        await using var db = fixture.NyDbContext();
        var k = new Korpus(db);
        var kort = Kortnavn();
        var (o, kunngjort) = await ByggM13Async(k, kort, kort, Overgangsregel(kort));
        // En annen lov i korpus har samme kortnavn (som straffeloven 1902 og 2005).
        var annen = await k.DokumentAsync("lov", new(2010, 1, 15), Nr(), "NL", $"Lov om noe annet ({kort})", [("§1", "Tekst.", null)]);

        var rad = await ViltlovradAsync(db, o, kunngjort);
        Assert.Equal(Hjemmelsstatus.Opphevet, rad.Status);
        Assert.Null(rad.VidereforingForm);
        var register = await Kortnavnregister.LastAsync(db);
        Assert.Equal((null, Kortnavnregister.GrunnFlereLover), register.Les(o.Viltloven.Eli));
        Assert.Equal((null, Kortnavnregister.GrunnFlereLover), register.Les(annen.Eli));
    }

    [Fact]
    public async Task Tvetydig_To_titler_gir_loven_ulike_kortnavn_og_gir_ikke_svar()
    {
        await using var db = fixture.NyDbContext();
        var k = new Korpus(db);
        var kort = Kortnavn();
        var (o, kunngjort) = await ByggM13Async(k, kort, kort, Overgangsregel(kort));
        var m = Hjemmelstekstlesing.Mal(o.Viltloven.Eli)!;
        // En annen hjemmelstekst siterer samme lov med et annet kortnavn.
        await k.DokumentAsync("forskrift", new(2001, 3, 1), Nr(), "SF", "Forskrift om noe", [("§1", "Tekst.", null)],
            annetOm: $"Fastsatt med hjemmel i lov {m.Dag}. mai {m.Aar} nr. {m.Nr} om jakt og fangst av vilt ({Kortnavn("jakt")}) § 9.");

        var rad = await ViltlovradAsync(db, o, kunngjort);
        Assert.Equal(Hjemmelsstatus.Opphevet, rad.Status);
        Assert.Equal((null, Kortnavnregister.GrunnFlereKortnavn), (await Kortnavnregister.LastAsync(db)).Les(o.Viltloven.Eli));
    }

    // ---- 3. Negative tilfeller ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Negativ_Uten_kortnavn_i_noen_tittel_er_hjemmelen_opphevet_som_for()
    {
        await using var db = fixture.NyDbContext();
        var kort = Kortnavn();
        var (o, kunngjort) = await ByggM13Async(new Korpus(db), kort, hjemmelstekstKortnavn: null, Overgangsregel(kort));
        var rad = await ViltlovradAsync(db, o, kunngjort);
        Assert.Equal(Hjemmelsstatus.Opphevet, rad.Status);
        Assert.Equal((null, Kortnavnregister.GrunnIngen), (await Kortnavnregister.LastAsync(db)).Les(o.Viltloven.Eli));
    }

    [Fact]
    public async Task Negativ_Kortnavnet_som_ikke_star_etter_hjemmelsuttrykket_viderefører_ikke()
    {
        await using var db = fixture.NyDbContext();
        var kort = Kortnavn();
        // Kortnavnet nevnes, men setningen viderefører forskrifter gitt i medhold av loven her.
        var (o, kunngjort) = await ByggM13Async(new Korpus(db), kort, kort,
            $"Forskrifter gitt i medhold av loven her gjelder også for saker som ble startet etter {kort}.");
        var rad = await ViltlovradAsync(db, o, kunngjort);
        Assert.Equal(Hjemmelsstatus.Opphevet, rad.Status);
        Assert.Null(rad.VidereforingForm);
    }

    // ---- Tekstformene -------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("med hjemmel i lov 29. mai 1981 nr. 38 om jakt og fangst av vilt (viltloven) § 26 nr. 8", "1981-05-29", "38", "viltloven")]
    [InlineData("jf. lov 4. februar 1977 nr. 4 om arbeidervern og arbeidsmiljø m.v. (arbeidsmiljøloven – aml.) § 14", "1977-02-04", "4", "arbeidsmiljøloven")]
    [InlineData("med heimel i lov 15. juli 1925 om skifte (skiftelova) § 3", "1925-07-15", "", "skiftelova")]
    [InlineData("med hjemmel i lov 1. juni 2001 nr. 3 om endringer i lov 28. februar 1997 nr. 19 (folketrygdloven)", "1997-02-28", "19", "folketrygdloven")]
    [InlineData("med hjemmel i lov 1. juni 2001 nr. 3 om endringer i folketrygdloven (pensjonsloven)", null, null, null)]
    [InlineData("med hjemmel i lov 1. juni 2001 nr. 3 § 4 (viltloven)", null, null, null)]
    public void Sitert_lovtittel_gir_kortnavnet_bare_for_loven_tittelen_horer_til(string tekst, string? dato, string? nr, string? kortnavn)
    {
        var t = Hjemmelstekstlesing.SiterteLovtitler(tekst).ToList();
        if (dato is null)
        {
            Assert.Empty(t);
            return;
        }
        var (lov, k) = Assert.Single(t);
        Assert.Equal((DateOnly.Parse(dato), nr, kortnavn), (lov.Dato, lov.Nr, k));
    }

    [Theory]
    [InlineData("Lov om jakt, fangst og felling av vilt mv. (viltressursloven)", "viltressursloven")]
    [InlineData("Lov om arbeidsmiljø, arbeidstid og stillingsvern mv. (arbeidsmiljøloven)", "arbeidsmiljøloven")]
    [InlineData("Lov om endringer i folketrygdloven (pensjonsloven)", null)]
    [InlineData("Lov om pristiltak", null)]
    public void Kortnavnet_i_lovens_egen_tittel(string tittel, string? kortnavn) => Assert.Equal(kortnavn, Hjemmelstekstlesing.KortnavnITittel(tittel));

    [Fact]
    public void Registeret_er_entydig_bare_nar_loven_har_ett_kortnavn_og_kortnavnet_bare_en_lov()
    {
        const string a = "https://lovdata.no/eli/lov/1981/05/29/38/nor", b = "https://lovdata.no/eli/lov/2025/06/20/102/nor";
        const string c = "https://lovdata.no/eli/lov/1902/05/22/10/nor", d = "https://lovdata.no/eli/lov/2005/05/20/28/nor";
        var r = Kortnavnregister.Bygg(
        [
            (a, a, null, null),
            (b, "Lov om jakt, fangst og felling av vilt mv. (viltressursloven)", "Viltressursloven", null),
            (null, "Forskrift om åte", null, "med hjemmel i lov 29. mai 1981 nr. 38 om jakt og fangst av vilt (viltloven) § 26"),
            (null, "Forskrift om noe", null, "med heimel i lov 29. mai 1981 nr. 38 om jakt og fangst av vilt (viltlova) § 26"),
            (c, c, null, null),
            (null, "Forskrift", null, "med hjemmel i lov 22. mai 1902 nr. 10 Almindelig borgerlig Straffelov (straffeloven) § 1"),
            (null, "Forskrift", null, "med hjemmel i lov 22. mai 1902 nr. 10 om straff (straffeloven) § 1"),
            (d, "Lov om straff (straffeloven)", "Straffeloven", null),
        ]);
        Assert.Equal("viltloven", r.Entydig(a));
        Assert.Equal("viltressursloven", r.Entydig(b));
        Assert.Null(r.Entydig(c));
        Assert.Equal(Kortnavnregister.GrunnFlereLover, r.Les(d).Grunn);
        Assert.Equal(Kortnavnregister.GrunnIngen, r.Les("https://lovdata.no/eli/lov/1990/01/01/1/nor").Grunn);
    }

    // ---- 4. R9 i #429 får samme svar ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task R9_viser_videreforingen_med_kortnavnet_fra_tittelen_og_ikke_nar_det_er_tvetydig()
    {
        await using var db = fixture.NyDbContext();
        var k = new Korpus(db);
        async Task<(RettskildeEntitet Gammel, RettskildeEntitet Ny, RettskildeEntitet Forskrift)> ByggAsync(string kort)
        {
            DateOnly dg = new(1981, 5, 29);
            var ng = Nr();
            var gammel = await k.StubAsync("lov", dg, ng);
            var ny = await k.DokumentAsync("lov", new(2025, 6, 20), Nr(), "NL", "Lov om jakt, fangst og felling av vilt mv.",
            [
                ("§61", null, "Ikrafttredelse"),
                ("§61/ledd-1", $"Fra samme tid oppheves lov {Ref(dg, ng)} om jakt og fangst av vilt.", null),
                ("§61/ledd-2", Overgangsregel(kort), null),
            ]);
            await k.EndrerAsync(ny, gammel);
            var f = await k.DokumentAsync("forskrift", new(2019, 12, 17), Nr(), "SF", "Forskrift om åte", [("§1", "Tekst.", null)],
                annetOm: $"Fastsatt med hjemmel i lov {Ref(dg, ng)} om jakt og fangst av vilt ({kort}) § 26 nr. 8.");
            await k.HjemlerAsync(f, (gammel, "§26"));
            return (gammel, ny, f);
        }
        var kort = Kortnavn();
        var (gammel, ny, f) = await ByggAsync(kort);
        var tvetydig = Kortnavn();
        var (gammel2, _, f2) = await ByggAsync(tvetydig);
        await k.DokumentAsync("lov", new(2011, 2, 3), Nr(), "NL", $"Lov om noe annet ({tvetydig})", [("§1", "Tekst.", null)]);

        var r9 = await Opphevingskontroll.HjemmelOpphevetAsync(db, [f.Id, f2.Id], new Dictionary<Guid, IReadOnlyList<(string, string?)>>());
        var h = Assert.Single(r9[f.Id]);
        Assert.Equal(gammel.Eli, h.HjemmelEli);
        Assert.NotNull(h.Viderefores);
        Assert.Equal($"{ny.Eli}/§61/ledd-2", h.Viderefores!.Eid);
        Assert.Equal(Overgangsregel(kort), h.Viderefores.Sitat);
        Assert.Contains("kan videreføre vedtak og forskrifter", Opphevingskontroll.Premissmangeltekst(h));

        var h2 = Assert.Single(r9[f2.Id]);
        Assert.Equal(gammel2.Eli, h2.HjemmelEli);
        Assert.Null(h2.Viderefores);

        // Samme svar i hjemmelsstatusen (korpuset, E1).
        await new HjemmelsstatusRegel(db).KjorAsync(skriv: true, "Test", lesLovtidend: false, forskriftIder: [f.Id, f2.Id]);
        var (_, rader) = (await new HjemmelsstatusRegel(db).LesAsync(f.Id))!.Value;
        Assert.Equal((Hjemmelsstatus.Viderefort, "V4"), (rader.Values.Single().Status, rader.Values.Single().VidereforingForm));
        var (_, rader2) = (await new HjemmelsstatusRegel(db).LesAsync(f2.Id))!.Value;
        Assert.Equal(Hjemmelsstatus.Opphevet, rader2.Values.Single().Status);
    }
}
