using Microsoft.EntityFrameworkCore;
using static RegelIde.Data.Tests.HjemmelsstatusTests;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, #472 PR 2, Johanns avgjørelser 2026-11-18 og 2026-11-20] Funnregelen «Hjemmel opphevet», undertypene og premissporten «Ny hjemmel».
/// Akseptansekriteriene i fase 1 §6 som gjelder funn, formulert som spørsmål (CLAUDE.md §0). Korpusene er de samme som i
/// <see cref="HjemmelsstatusTests"/>, med tilfeldige numre i den delte testbasen; regelen kjøres alltid avgrenset til forskriftene i testen.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class HjemmelOpphevetFunnregelTests(EmbeddedPostgresFixture fixture)
{
    /// <summary>S04: en delegering etter tolloven 2007, som vareførselsloven opphever uten videreføring.</summary>
    private static async Task<RettskildeEntitet> ByggS04Async(Korpus k, bool? rentEndringsvedtak = null)
    {
        DateOnly dToll = new(2007, 12, 21);
        var nToll = Nr();
        var toll = await k.StubAsync("lov", dToll, nToll);
        var varefl = await k.DokumentAsync("lov", new(2022, 3, 11), Nr(), "NL", "Lov om inn- og utførsel av varer (vareførselsloven)",
            [("§14-1", null, "Ikrafttredelse"), ("§14-1/ledd-1", $"Loven gjelder fra den tid Kongen bestemmer. Fra samme tidspunkt oppheves lov {Ref(dToll, nToll)} om toll og vareførsel.", null)]);
        await k.EndrerAsync(varefl, toll);
        var s04 = await k.DokumentAsync("forskrift", new(2016, 12, 2), Nr(), "DEL", "Tildeling av myndighet etter tolloven § 8-3",
            [("ledd-1", "Myndigheten etter tolloven § 8-3 tildeles Finansdepartementet.", null)],
            annetOm: $"Hjemmel: Fastsatt ved kgl.res. 2. desember 2016 med hjemmel i lov {Ref(dToll, nToll)} om toll og vareførsel (tolloven) § 8-3.",
            rentEndringsvedtak: rentEndringsvedtak);
        await k.HjemlerAsync(s04, (toll, "§8-3"));
        return s04;
    }

    /// <summary>S20: SF/forskrift/2017-06-16-1124, videreført av fagskoleloven 2018 § 44 tredje ledd fram til 31. desember 2019.</summary>
    private static async Task<(RettskildeEntitet F, RettskildeEntitet L2018)> ByggS20Async(Korpus k)
    {
        DateOnly d2003 = new(2003, 6, 20);
        var n2003 = Nr();
        var l2003 = await k.StubAsync("lov", d2003, n2003);
        var l2018 = await k.DokumentAsync("lov", new(2018, 6, 8), Nr(), "NL", "Lov om høyere yrkesfaglig utdanning (fagskoleloven)",
        [
            ("§44", null, "Ikrafttredelses- og overgangsregler"),
            ("§44/ledd-1", "Loven gjelder fra den tiden Kongen bestemmer.", null),
            ("§44/ledd-2", $"Fra samme tidspunkt oppheves lov {Ref(d2003, n2003)} om fagskoleutdanning.", null),
            ("§44/ledd-3", $"Forskrifter gitt med hjemmel i lov {Ref(d2003, n2003)} om fagskoleutdanning skal gjelde frem til 31. desember 2019 eller "
                + "til forskriften blir opphevet.", null),
        ]);
        await k.EndrerAsync(l2018, l2003);
        var f = await k.DokumentAsync("forskrift", new(2017, 6, 16), Nr(), "SF", "Forskrift om fagskoleutdanning ved Treider Fagskoler",
            [("§1-1", null, "Virkeområde"), ("§1-1/ledd-1", "Forskriften gjelder …", null)],
            annetOm: $"Hjemmel: Fastsatt av styret ved Treider Fagskoler 16. juni 2017 med hjemmel i lov {Ref(d2003, n2003)} om fagskoleutdanning "
                + "(fagskoleloven) § 4 annet ledd og § 6 femte ledd.");
        await k.HjemlerAsync(f, (l2003, "§4"), (l2003, "§6"), (l2018, "§44"));
        return (f, l2018);
    }

    private static async Task<HjemmelOpphevetRapport> KjorAsync(RegelIdeDbContext db, RettskildeEntitet f, bool skriv, bool nullGirFunn = true)
    {
        await new HjemmelsstatusRegel(db).KjorAsync(skriv: true, "Test", lesLovtidend: false, forskriftIder: [f.Id]);
        return await new HjemmelOpphevetFunnregel(db, new FunnTjeneste(db)).KjorAsync(skriv, "Test", nullGirFunn, [f.Id], medRader: true);
    }

    private static Task<FunnEntitet?> FunnAsync(RegelIdeDbContext db, RettskildeEntitet f) =>
        db.Funn.Include(x => x.Koblinger).Include(x => x.Vurderingspunkter)
            .FirstOrDefaultAsync(x => x.EksternNokkel == HjemmelOpphevetFunnregel.NokkelPrefiks + UtgattAktorFunnregel.EliKort(f.Eli!));

    [Fact]
    public async Task AK2_Gir_FOR_1993_12_17_1314_et_funn_om_mulig_bortfalt_Nei_den_er_viderefort_etter_konkurranseloven_2004_paragraf_49()
    {
        await using var db = fixture.NyDbContext();
        var s = await ByggS01Async(new Korpus(db));
        // Bryteren på: NULL i #446-kolonnen stopper ikke funnet, så det er videreføringen som avgjør.
        var r = await KjorAsync(db, s.F, skriv: true);
        Assert.Equal(1, r.Forskrifter);
        Assert.Equal(1, r.PerSamletStatus[Hjemmelsstatus.SamletViderefort]);
        Assert.Equal(0, r.Kandidater);
        Assert.Equal(0, r.Foreslatt);
        Assert.Null(await FunnAsync(db, s.F));
    }

    [Fact]
    public async Task AK5_Gir_SF_2017_06_16_1124_funnet_videreforing_utlopt_med_den_opphevende_og_den_viderforende_bestemmelsen()
    {
        await using var db = fixture.NyDbContext();
        var (f, l2018) = await ByggS20Async(new Korpus(db));
        var r = await KjorAsync(db, f, skriv: true);
        Assert.Equal(1, r.Nye);
        Assert.Equal(1, r.ForeslattPerUndertype[HjemmelOpphevetFunnregel.UndertypeVidereforingUtlopt]);

        var funn = (await FunnAsync(db, f))!;
        Assert.Equal((Funn.HjemmelOpphevet, HjemmelOpphevetFunnregel.UndertypeVidereforingUtlopt, Funn.Oppdaget),
            (funn.Kategori, funn.Undertype, funn.Status));
        Assert.Equal(HjemmelOpphevetFunnregel.OppdagelsesKilde, funn.OppdagelsesKilde);
        Assert.Contains(HjemmelOpphevetFunnregel.Regelversjon, funn.InternMerknad);
        Assert.DoesNotContain("bortfalt", funn.Tittel);
        Assert.DoesNotContain("sovende", funn.Beskrivelse, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fram til 31.12.2019", funn.Beskrivelse);

        // Koblingene: forskriften, den opphevende bestemmelsen og overgangsregelen.
        var gjelder = Assert.Single(funn.Koblinger, k => k.Rolle == Funn.GjelderBestemmelse);
        Assert.Equal((f.Eli, f.Id), (gjelder.RettskildeEli, gjelder.RettskildeId));
        var opphever = Assert.Single(funn.Koblinger, k => k.Rolle == Funn.OpphevendeBestemmelse);
        Assert.Equal((l2018.Eli, $"{l2018.Eli}/§44/ledd-2"), (opphever.RettskildeEli, opphever.Eid));
        Assert.NotNull(opphever.NodeId);
        var vf = Assert.Single(funn.Koblinger, k => k.Rolle == Funn.Videreforingsbestemmelse);
        Assert.Equal($"{l2018.Eli}/§44/ledd-3", vf.Eid);
        Assert.Contains("31.12.2019", vf.Merknad);

        // Funnet står i forskriften, ikke i fagskoleloven 2018.
        var tjeneste = new FunnTjeneste(db);
        Assert.Contains(await tjeneste.ListeAsync(rettskildeId: f.Id), x => x.Id == funn.Id);
        Assert.DoesNotContain(await tjeneste.ListeAsync(rettskildeId: l2018.Id), x => x.Id == funn.Id);
    }

    [Fact]
    public async Task Premissporten_Vises_funnet_som_mulig_bortfalt_for_Ny_hjemmel_er_vurdert_Nei()
    {
        await using var db = fixture.NyDbContext();
        var s04 = await ByggS04Async(new Korpus(db), rentEndringsvedtak: null);
        var r = await KjorAsync(db, s04, skriv: true);
        Assert.Equal(1, r.ForeslattPerUndertype[HjemmelOpphevetFunnregel.UndertypeHjemmelOpphevet]);
        Assert.Equal(1, r.HoldtAvPremissporten);
        Assert.Equal(0, r.MuligBortfaltVises);

        var tjeneste = new FunnTjeneste(db);
        var funn = (await FunnAsync(db, s04))!;
        Assert.StartsWith("Hjemmel opphevet, videreføring ikke funnet:", funn.Tittel);
        Assert.DoesNotContain("bortfalt", funn.Tittel + funn.Beskrivelse);
        Assert.False(FunnTjeneste.MuligBortfaltVises(funn));
        var mangler = await tjeneste.PremissmanglerAsync(funn);
        Assert.Contains(mangler, m => m.StartsWith(FunnTjeneste.NyHjemmelMangelStart));
        Assert.Contains(mangler, m => m.StartsWith("Hjemmel ikke vurdert"));
        Assert.Contains(mangler, m => m.StartsWith(FunnTjeneste.UttomtVirkningMangelStart));

        // Juristvurdert uten «Ny hjemmel»: fortsatt ikke «mulig bortfalt», og godkjenningen stoppes.
        await tjeneste.JuristvurderAsync(funn.Id, "Vurdert.", Funn.KiAgent, "Test",
        [
            new(Funn.BortforklaringProvd, "Søkt etter overgangsregel i vareførselsloven og overgangsforskriftene; ingen funnet."),
            new(Funn.Vurdering, "Uten virkning etter ordlyden.", Funn.HjemmelOverskrift),
        ]);
        funn = (await FunnAsync(db, s04))!;
        Assert.False(FunnTjeneste.MuligBortfaltVises(funn));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tjeneste.GodkjennForPubliseringAsync(funn.Id, "Test"));

        // «Ny hjemmel» merket «ikke vurdert» åpner merket; mangelen for «Ny hjemmel» forsvinner.
        await tjeneste.JuristvurderAsync(funn.Id, "Vurdert.", Funn.KiAgent, "Test",
        [
            .. funn.Vurderingspunkter.OrderBy(p => p.Sortering).Select(p => new NyttVurderingspunkt(p.Type, p.Tekst, p.Overskrift, p.Sikkerhet)),
            new(Funn.IkkeAvgjort, "Ikke vurdert.", Funn.NyHjemmelOverskrift),
        ]);
        funn = (await FunnAsync(db, s04))!;
        Assert.True(FunnTjeneste.MuligBortfaltVises(funn));
        mangler = await tjeneste.PremissmanglerAsync(funn);
        Assert.DoesNotContain(mangler, m => m.StartsWith(FunnTjeneste.NyHjemmelMangelStart));
        Assert.DoesNotContain(mangler, m => m.StartsWith("Hjemmel ikke vurdert"));
        // #446 er fortsatt ikke klassifisert: porten holder godkjenningen.
        Assert.Contains(mangler, m => m.StartsWith(FunnTjeneste.UttomtVirkningMangelStart));

        var r2 = await KjorAsync(db, s04, skriv: false);
        Assert.Equal((0, 1, 0, 1), (r2.HoldtAvPremissporten, r2.MuligBortfaltVises, r2.Nye, r2.Finnes));
    }

    [Fact]
    public async Task Idempotens_Gir_en_ny_kjoring_nye_funn_Nei()
    {
        await using var db = fixture.NyDbContext();
        var s04 = await ByggS04Async(new Korpus(db), rentEndringsvedtak: null);
        var forste = await KjorAsync(db, s04, skriv: true);
        Assert.Equal(1, forste.Nye);
        var andre = await KjorAsync(db, s04, skriv: true);
        Assert.Equal((0, 1, 0, 0), (andre.Nye, andre.Finnes, andre.UndertypeEndret, andre.UtenGrunnlag));
        Assert.Equal(1, await db.Funn.CountAsync(x => x.EksternNokkel == HjemmelOpphevetFunnregel.NokkelPrefiks + UtgattAktorFunnregel.EliKort(s04.Eli!)));
    }

    [Fact]
    public async Task Uttomt_virkning_med_mal_utenfor_korpus_gir_funn_med_premissmangel_446_AK7()
    {
        await using var db = fixture.NyDbContext();
        var k = new Korpus(db);
        var ren = await ByggS04Async(k, rentEndringsvedtak: true);
        // Målet i «Endrer» er bare en referanse-stub: teksten der kan ikke kontrolleres.
        await k.EndrerAsync(ren, await k.StubAsync("forskrift", new(2010, 1, 1), Nr()));

        var r = await KjorAsync(db, ren, skriv: true, nullGirFunn: true);
        Assert.Equal((0, 1, 1), (r.UtelattRentEndringsvedtak, r.RentMenMalUtenforKorpus, r.Foreslatt));
        var funn = (await FunnAsync(db, ren))!;
        var mangler = await new FunnTjeneste(db).PremissmanglerAsync(funn);
        Assert.Contains(mangler, m => m.StartsWith(FunnTjeneste.MalUtenforKorpusMangelStart));
        Assert.DoesNotContain(mangler, m => m.StartsWith(FunnTjeneste.UttomtVirkningMangelStart));
    }

    [Fact]
    public async Task Uttomt_virkning_Gir_et_rent_endringsvedtak_eller_en_uklassifisert_forskrift_funn()
    {
        await using var db = fixture.NyDbContext();
        var k = new Korpus(db);
        var ren = await ByggS04Async(k, rentEndringsvedtak: true);
        var uklassifisert = await ByggS04Async(k, rentEndringsvedtak: null);

        // Et rent endringsvedtak gir aldri funn, uansett bryteren.
        var r = await KjorAsync(db, ren, skriv: true, nullGirFunn: true);
        Assert.Equal((1, 0, 0, 0), (r.UtelattRentEndringsvedtak, r.Foreslatt, r.Streng.Funn, r.NullGirFunn.Funn));
        Assert.Null(await FunnAsync(db, ren));

        // NULL i #446-kolonnen: bryteren av (fase 1 ordrett) gir ikke funn; rapporten viser begge tolkningene. Standarden er på
        // (prosjekteierens avgjørelse 2026-11-24), men bryteren står, så den kan slås av.
        var av = await KjorAsync(db, uklassifisert, skriv: true, nullGirFunn: false);
        Assert.Equal((1, 0, 0, 1), (av.IkkeKlassifisert, av.Foreslatt, av.Streng.Funn, av.NullGirFunn.Funn));
        Assert.Equal(1, av.NullGirFunn.PerPrefiks["DEL"]);
        Assert.Equal(HjemmelOpphevetFunnregel.UtfallIkkeKlassifisert, Assert.Single(av.Rader!).Utfall);
        Assert.Null(await FunnAsync(db, uklassifisert));

        // Bryteren på: funnet skrives, med premissmangelen for #446.
        var paa = await KjorAsync(db, uklassifisert, skriv: true, nullGirFunn: true);
        Assert.Equal((1, 1), (paa.Foreslatt, paa.Nye));
        Assert.Contains("ikke klassifisert (#446)", (await FunnAsync(db, uklassifisert))!.Beskrivelse);
    }

    [Fact]
    public void Arv_Gir_en_hjemmel_som_selv_er_en_forskrift_med_blandede_hjemler_funn_Nei_den_arves_som_ukjent()
    {
        Guid forelder = Guid.NewGuid(), barn = Guid.NewGuid();
        var rader = new Dictionary<Guid, IReadOnlyList<HjemmelPremiss>>
        {
            // Barnet: én materiell hjemmel opphevet, én aktiv → blandede hjemler.
            [barn] =
            [
                new(Guid.NewGuid(), "a", Hjemmelsstatus.Materiell, Hjemmelsstatus.Opphevet, Strukturkanter.Tekstfunn, null),
                new(Guid.NewGuid(), "b", Hjemmelsstatus.Materiell, Hjemmelsstatus.Aktiv, Strukturkanter.Normalisering, null),
            ],
            // Forelderen står bare på barnet.
            [forelder] = [new(Guid.NewGuid(), "barn", Hjemmelsstatus.Materiell, Hjemmelsstatus.Aktiv, Strukturkanter.Normalisering, barn)],
        };
        var memo = new Dictionary<Guid, SamletHjemmelsstatus>();
        var sb = HjemmelsstatusSamlet.Beregn(barn, id => rader.GetValueOrDefault(id), memo);
        var sf = HjemmelsstatusSamlet.Beregn(forelder, id => rader.GetValueOrDefault(id), memo);
        Assert.Equal(HjemmelOpphevetFunnregel.UndertypeBlandede, HjemmelOpphevetFunnregel.Undertype(sb));
        Assert.Equal(Hjemmelsstatus.SamletUkjent, sf.Status);
        Assert.Null(HjemmelOpphevetFunnregel.Undertype(sf));
    }
}
