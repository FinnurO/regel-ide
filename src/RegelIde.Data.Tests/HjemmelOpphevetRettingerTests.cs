using Microsoft.EntityFrameworkCore;
using static RegelIde.Data.Tests.HjemmelsstatusTests;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, #472 PR 4, prosjekteierens avgjørelse 2026-11-24 etter måling 1: «Bygg rettingene, mål på nytt»] Rettingene etter juristens måling 1 av
/// funnet «Hjemmel opphevet», med eksemplene fra utvalget (H07–H20 i <c>data/fasit/strukturmodell/juristrunde-472/</c>). Hver test er et spørsmål
/// modellen skal kunne besvare (CLAUDE.md §0). Korpusene er bygget med tilfeldige numre, som i <see cref="HjemmelsstatusTests"/>.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class HjemmelOpphevetRettingerTests(EmbeddedPostgresFixture fixture)
{
    private static async Task<HjemmelOpphevetRapport> KjorAsync(RegelIdeDbContext db, RettskildeEntitet f, bool skriv, bool oppfrisk = false)
    {
        await new HjemmelsstatusRegel(db).KjorAsync(skriv: true, "Test", lesLovtidend: false, forskriftIder: [f.Id]);
        return await new HjemmelOpphevetFunnregel(db, new FunnTjeneste(db)).KjorAsync(skriv, "Test", true, [f.Id], medRader: true, oppfrisk: oppfrisk);
    }

    private static Task<FunnEntitet?> FunnAsync(RegelIdeDbContext db, RettskildeEntitet f) =>
        db.Funn.Include(x => x.Koblinger).Include(x => x.Vurderingspunkter)
            .FirstOrDefaultAsync(x => x.EksternNokkel == HjemmelOpphevetFunnregel.NokkelPrefiks + UtgattAktorFunnregel.EliKort(f.Eli!));

    private static async Task KunngjortAsync(RegelIdeDbContext db, RettskildeEntitet f)
    {
        f.Kildeform = HjemmelOpphevetFunnregel.KildeformKunngjort;
        await db.SaveChangesAsync();
    }

    /// <summary>H08: LTI/forskrift/2005-04-29-378, delegering etter utlendingsloven 1988, som utlendingsloven 2008 § 140 opphever. Korpuset har
    /// vedtaket bare som kunngjort versjon.</summary>
    private static async Task<RettskildeEntitet> ByggH08Async(Korpus k, RegelIdeDbContext db)
    {
        DateOnly d1988 = new(1988, 6, 24), dVedtak = new(2005, 4, 29);
        int n1988 = Nr(), nVedtak = Nr();
        var l1988 = await k.StubAsync("lov", d1988, n1988);
        var l2008 = await k.DokumentAsync("lov", new(2008, 5, 15), Nr(), "NL", "Lov om utlendingers adgang til riket og deres opphold her (utlendingsloven)",
            [("§140", null, "Ikrafttredelse"), ("§140/ledd-1", $"Loven gjelder fra den tid Kongen bestemmer. Fra den tiden loven trer i kraft oppheves lov {Ref(d1988, n1988)} om utlendingers adgang til riket og deres opphold her.", null)]);
        await k.EndrerAsync(l2008, l1988);
        var vedtak = await k.DokumentAsync("forskrift", dVedtak, nVedtak, "LTI", "Delegering av myndighet til Kommunal- og regionaldepartementet etter utlendingsloven.",
            [("kap-I", null, null), ("kap-I/ledd-1", "Myndigheten delegeres til Kommunal- og regionaldepartementet.", null)],
            annetOm: $"Fastsatt ved Kronprinsreg.res. 29. april 2005 med hjemmel i lov {Ref(d1988, n1988)} om utlendingers adgang til riket og deres opphold her (utlendingsloven).");
        await KunngjortAsync(db, vedtak);
        await k.HjemlerAsync(vedtak, (l1988, null));
        return vedtak;
    }

    [Fact]
    public async Task H08_Gir_et_vedtak_som_selv_er_opphevet_i_korpus_funn_Nei_og_et_lagret_funn_forklares_bort_uten_a_slettes()
    {
        await using var db = fixture.NyDbContext();
        var k = new Korpus(db);
        var vedtak = await ByggH08Async(k, db);

        // Uten opphevingen i korpus: funnet skrives (som i måling 1), med premissmangelen for en kunngjort versjon.
        var r1 = await KjorAsync(db, vedtak, skriv: true);
        Assert.Equal((1, 1), (r1.Nye, r1.KunngjortIkkeKontrollert));
        var funn = (await FunnAsync(db, vedtak))!;
        Assert.Equal(Funn.Oppdaget, funn.Status);

        // Opphevingen kommer inn i korpus (DEL/forskrift/2006-06-30-761 punkt 2). Samme lesing som R8: «Endrer» og hel opphevingssetning.
        var opphever = await k.DokumentAsync("forskrift", new(2006, 6, 30), Nr(), "DEL", "Delegering av myndighet til AID etter utlendingsloven.",
        [
            ("punkt-1", "Myndigheten delegeres til Arbeids- og inkluderingsdepartementet.", null),
            ("punkt-2", $"Vedtak {Ref(new(2005, 4, 29), int.Parse(vedtak.DokumentId!.Split('-')[^1]))} om delegering av myndighet til Kommunal- og "
                + "regionaldepartementet etter utlendingsloven oppheves.", null),
        ]);
        await k.EndrerAsync(opphever, vedtak);

        var torr = await KjorAsync(db, vedtak, skriv: false);
        Assert.Equal((1, 0, 1, 0), (torr.ForskriftenOpphevet, torr.Foreslatt, torr.ForklaresBort, torr.ForklartBortSatt));
        Assert.Equal(1, torr.ForskriftenOpphevetPerPrefiks!["LTI"]);
        var rad = Assert.Single(torr.Rader!);
        Assert.Equal(HjemmelOpphevetFunnregel.UtfallForskriftenOpphevet, rad.Utfall);
        Assert.Contains("/punkt-2", rad.Bevis);

        var skrevet = await KjorAsync(db, vedtak, skriv: true);
        Assert.Equal((1, 1, 0), (skrevet.ForklaresBort, skrevet.ForklartBortSatt, skrevet.Nye));
        funn = (await FunnAsync(db, vedtak))!;
        Assert.Equal((Funn.ForklartBort, Funn.Maskinregel, HjemmelOpphevetFunnregel.OppdagelsesKilde),
            (funn.Status, funn.VurdertAvType, funn.ForklartBortAv));
        Assert.Contains("selv opphevet", funn.ForklartBortBegrunnelse);
        Assert.Equal(1, await db.Funn.CountAsync(x => x.Id == funn.Id));

        // En ny kjøring gjør ingenting mer, og gjenåpner ikke.
        var igjen = await KjorAsync(db, vedtak, skriv: true);
        Assert.Equal((0, 0, 0, 0), (igjen.ForklaresBort, igjen.ForklartBortSatt, igjen.Nye, igjen.UtenGrunnlag));
    }

    [Fact]
    public async Task H07_Far_en_kunngjort_versjon_som_ikke_kan_kontrolleres_premissmangelen_kunngjort_versjon()
    {
        await using var db = fixture.NyDbContext();
        var vedtak = await ByggH08Async(new Korpus(db), db);
        var r = await KjorAsync(db, vedtak, skriv: true);
        Assert.Equal((1, 0), (r.KunngjortIkkeKontrollert, r.ForskriftenOpphevet));

        var funn = (await FunnAsync(db, vedtak))!;
        // Påstanden sier ikke at Lovdata viser vedtaket som gjeldende: korpuset har bare den kunngjorte versjonen.
        Assert.DoesNotContain("Lovdata viser", funn.Beskrivelse);
        Assert.Contains("bare slik det ble kunngjort i Norsk Lovtidend", funn.Beskrivelse);
        Assert.Contains(FunnTjeneste.KunngjortMangelStart, funn.Beskrivelse);

        var tjeneste = new FunnTjeneste(db);
        Assert.Contains(await tjeneste.PremissmanglerAsync(funn), m => m.StartsWith(FunnTjeneste.KunngjortMangelStart));

        // Juristens punkt «Gjeldende status» fjerner mangelen.
        await tjeneste.JuristvurderAsync(funn.Id, "Vurdert.", Funn.KiAgent, "Test",
        [
            new(Funn.BortforklaringProvd, "Slått opp i Lovdata."),
            new(Funn.Vurdering, "Lovdata (DELO) viser vedtaket som opphevet.", Funn.GjeldendeStatusOverskrift),
        ]);
        funn = (await FunnAsync(db, vedtak))!;
        Assert.DoesNotContain(await tjeneste.PremissmanglerAsync(funn), m => m.StartsWith(FunnTjeneste.KunngjortMangelStart));
    }

    [Fact]
    public async Task H13_Far_en_konsolidert_forskrift_premissmangelen_for_kunngjort_versjon_Nei()
    {
        await using var db = fixture.NyDbContext();
        var k = new Korpus(db);
        DateOnly d = new(2007, 12, 21);
        var n = Nr();
        var toll = await k.StubAsync("lov", d, n);
        var varefl = await k.DokumentAsync("lov", new(2022, 3, 11), Nr(), "NL", "Lov om inn- og utførsel av varer (vareførselsloven)",
            [("§14-1", null, "Ikrafttredelse"), ("§14-1/ledd-1", $"Fra samme tidspunkt oppheves lov {Ref(d, n)} om toll og vareførsel.", null)]);
        await k.EndrerAsync(varefl, toll);
        var f = await k.DokumentAsync("forskrift", new(2016, 12, 2), Nr(), "DEL", "Tildeling av myndighet etter tolloven § 8-3",
            [("ledd-1", "Myndigheten tildeles Finansdepartementet.", null)],
            annetOm: $"Fastsatt ved kgl.res. med hjemmel i lov {Ref(d, n)} om toll og vareførsel (tolloven) § 8-3.");
        f.Kildeform = "konsolidert";
        await db.SaveChangesAsync();
        await k.HjemlerAsync(f, (toll, "§8-3"));

        var r = await KjorAsync(db, f, skriv: true);
        Assert.Equal((1, 0), (r.Nye, r.KunngjortIkkeKontrollert));
        var funn = (await FunnAsync(db, f))!;
        Assert.StartsWith("Lovdata viser", funn.Beskrivelse);
        Assert.DoesNotContain(await new FunnTjeneste(db).PremissmanglerAsync(funn), m => m.StartsWith(FunnTjeneste.KunngjortMangelStart));
    }

    [Fact]
    public async Task H18_Folges_endringsloven_til_hovedloven_fra_endringslovens_egen_del_II_Nei_den_er_ukjent_END()
    {
        await using var db = fixture.NyDbContext();
        var k = new Korpus(db);
        DateOnly d2003 = new(2003, 6, 20), dEndr = new(2007, 6, 29);
        int n2003 = Nr(), nEndr = Nr();
        var l2003 = await k.StubAsync("lov", d2003, n2003);
        var endr = await k.StubAsync("lov", dEndr, nEndr);
        var l2018 = await k.DokumentAsync("lov", new(2018, 6, 8), Nr(), "NL", "Lov om høyere yrkesfaglig utdanning (fagskoleloven)",
        [
            ("§44", null, "Ikrafttredelses- og overgangsregler"),
            ("§44/ledd-2", $"Fra samme tidspunkt oppheves lov {Ref(d2003, n2003)} om fagskoleutdanning.", null),
            ("§44/ledd-3", $"Forskrifter gitt med hjemmel i lov {Ref(d2003, n2003)} om fagskoleutdanning skal gjelde frem til 31. desember 2019.", null),
        ]);
        await k.EndrerAsync(l2018, l2003);
        var hjemmelstekst = $"Fastsatt ved Kronprinsreg.res. 29. juni 2007 med hjemmel i lov {Ref(dEndr, nEndr)} om endringer i lov {Ref(d2003, n2003)} "
            + "om fagskoleutdanning del II nr. 1. Fremmet av Kunnskapsdepartementet.";
        var f = await k.DokumentAsync("forskrift", new(2007, 6, 29), Nr(), "DEL",
            $"Ikrafttredelse av lov {Ref(dEndr, nEndr)} om endringer i lov {Ref(d2003, n2003)} om fagskoleutdanning", [("ledd-1", "Loven trer i kraft 1. august 2007.", null)],
            annetOm: hjemmelstekst);
        await k.HjemlerAsync(f, (endr, null));

        var r = await KjorAsync(db, f, skriv: true);
        var rad = await db.Hjemmelsstatuser.SingleAsync(x => x.RettskildeId == f.Id);
        Assert.Equal((Hjemmelsstatus.Ukjent, Hjemmelsstatus.GrunnEnd, (string?)null), (rad.Status, rad.UkjentGrunn, rad.FulgtEli));
        Assert.Equal((0, 0), (r.Kandidater, r.Foreslatt));
        Assert.Null(await FunnAsync(db, f));

        Assert.Equal("II", Hjemmelstekstlesing.EndringslovensEgenDel(hjemmelstekst, Hjemmelstekstlesing.Mal(endr.Eli)!));
    }

    [Theory]
    // Uten del, og del I (endringene i hovedloven): følges som før.
    [InlineData("med hjemmel i lov 13. desember 2002 nr. 77 om endringer i lov 10. juni 1988 nr. 40 om finansieringsvirksomhet.", null)]
    [InlineData("med hjemmel i lov 13. desember 2002 nr. 77 om endringer i lov 10. juni 1988 nr. 40 om finansieringsvirksomhet del I § 2.", null)]
    // Del II og senere, også etter «m.v.» i tittelen (punktum) og før «jf.».
    [InlineData("med hjemmel i lov 13. desember 2002 nr. 77 om endringer i lov 10. juni 1988 nr. 40 om verdipapirhandel m.v. del X, jf. kgl.res. 7. juni 1996 nr. 571.", "X")]
    [InlineData("med hjemmel i lov 13. desember 2002 nr. 77 om endringer i lov 10. juni 1988 nr. 40 om bokføring (bokføringsloven) del II, nr. 3. Fremmet av Finansdepartementet.", "II")]
    // En del som hører til en annen endringslov i samme hjemmelstekst, leses ikke for denne.
    [InlineData("med hjemmel i lov 13. desember 2002 nr. 77 om endringer i lov 10. juni 1988 nr. 40 om særavgifter, lov 29. juni 2007 nr. 47 om endringar i lov 21. november 1952 nr. 2 del IV.", null)]
    public void H18_Hvilken_del_av_endringsloven_viser_hjemmelsteksten_til(string tekst, string? del) =>
        Assert.Equal(del, Hjemmelstekstlesing.EndringslovensEgenDel(tekst, Hjemmelstekstlesing.Mal("https://lovdata.no/eli/lov/2002/12/13/77/nor")!));

    [Fact]
    public void H20_Er_et_ressortvedtak_gjenstandslost_nar_bare_en_av_gjenstandene_er_opphevet_Nei_bare_nar_alle_er_det()
    {
        Guid id = Guid.NewGuid();
        HjemmelPremiss Grl12() => new(Guid.NewGuid(), "grl", Hjemmelsstatus.Kompetanseledd, Hjemmelsstatus.Aktiv, Strukturkanter.Normalisering, null);
        HjemmelPremiss Gjenstand(string status) => new(Guid.NewGuid(), "lov", Hjemmelsstatus.Gjenstand, status, Strukturkanter.Normalisering, null);

        // DEL/forskrift/2000-12-15-1263: riksborgerrettsloven 1950, lotteriloven 1995 og personopplysningsloven 2000 er opphevet; odelslova lever.
        IReadOnlyList<HjemmelPremiss> h20 = [Grl12(), Gjenstand(Hjemmelsstatus.Gjenstandslos), Gjenstand(Hjemmelsstatus.Aktiv),
            Gjenstand(Hjemmelsstatus.Gjenstandslos), Gjenstand(Hjemmelsstatus.Gjenstandslos)];
        var s = HjemmelsstatusSamlet.Beregn(id, _ => h20);
        Assert.False(s.Gjenstandslos);
        Assert.Null(HjemmelOpphevetFunnregel.Undertype(s));

        // En gjenstand som er ukjent, gir heller ikke merket.
        Assert.False(HjemmelsstatusSamlet.Beregn(id, _ => [Grl12(), Gjenstand(Hjemmelsstatus.Gjenstandslos), Gjenstand(Hjemmelsstatus.Ukjent)]).Gjenstandslos);

        // Alle gjenstandene opphevet: gjenstandsløs som før.
        var alle = HjemmelsstatusSamlet.Beregn(id, _ => [Grl12(), Gjenstand(Hjemmelsstatus.Gjenstandslos), Gjenstand(Hjemmelsstatus.Gjenstandslos)]);
        Assert.True(alle.Gjenstandslos);
        Assert.Equal(HjemmelOpphevetFunnregel.UndertypeGjenstandslos, HjemmelOpphevetFunnregel.Undertype(alle));
    }

    [Fact]
    public async Task H15_Far_en_forskrift_som_star_som_hjemmel_uten_rolle_rollen_kompetanseledd_og_er_SF_1976_08_03_9783_viderefort()
    {
        await using var db = fixture.NyDbContext();
        var k = new Korpus(db);
        DateOnly d1953 = new(1953, 7, 17), d1975 = new(1975, 12, 29);
        int n1953 = Nr(), n1975 = Nr();
        var l1953 = await k.StubAsync("lov", d1953, n1953);
        var f1975 = await k.StubAsync("forskrift", d1975, n1975);
        var l2010 = await k.DokumentAsync("lov", new(2010, 6, 25), Nr(), "NL", "Lov om kommunal beredskapsplikt, sivile beskyttelsestiltak og Sivilforsvaret (sivilbeskyttelsesloven)",
        [
            ("§39", null, "Overgangsbestemmelser"),
            ("§39/ledd-1", $"Forskrifter gitt med hjemmel i lov {Ref(d1953, n1953)} om kommunal beredskapsplikt, sivile beskyttelsestiltak og Sivilforsvaret gjelder også etter at loven her har trådt i kraft.", null),
            ("§40", null, "Ikrafttredelse"),
            ("§40/ledd-2", $"Fra samme dato oppheves lov {Ref(d1953, n1953)} om kommunal beredskapsplikt, sivile beskyttelsestiltak og Sivilforsvaret.", null),
        ]);
        await k.EndrerAsync(l2010, l1953);
        var f1995 = await k.DokumentAsync("forskrift", new(1995, 3, 15), Nr(), "SF", "Forskrift om tilfluktsrom",
            [("§41", null, "Ikrafttredelse"), ("§41/ledd-2", $"Fra samme tidspunkt oppheves forskrift av {Ref(d1975, n1975)} om tilfluktsrom.", null)]);
        await k.EndrerAsync(f1995, f1975);
        // Hjemmelsfeltet er tomt; Lovdata gir forskriften fra 1975 og sivilforsvarsloven § 41 som hjemmel.
        var f = await k.DokumentAsync("forskrift", new(1976, 8, 3), Nr(), "SF", "Forskrifter om tilfluktsrom ved industrivernpliktige bedrifter og virksomheter.",
            [("punkt-1", "Reglene gjelder i tillegg til forskriften fra 1975.", null)]);
        await k.HjemlerAsync(f, (f1975, null), (l1953, "§41"));

        var r = await KjorAsync(db, f, skriv: true);
        var rader = await db.Hjemmelsstatuser.Where(x => x.RettskildeId == f.Id).ToListAsync();
        var r1975 = Assert.Single(rader, x => x.HjemmelEli == f1975.Eli);
        Assert.Equal((Hjemmelsstatus.Kompetanseledd, Hjemmelsstatus.RolleregelForskriftSomHjemmel, Hjemmelsstatus.Opphevet),
            (r1975.Rolle, r1975.RolleRegel, r1975.Status));
        Assert.Equal(Hjemmelsstatus.Viderefort, Assert.Single(rader, x => x.HjemmelEli == l1953.Eli).Status);
        // Kompetanseleddet teller ikke når det finnes en hjemmel med rolle NULL eller materiell: samlet videreført, ingen funn.
        Assert.Equal(1, r.PerSamletStatus[Hjemmelsstatus.SamletViderefort]);
        Assert.Equal(0, r.Kandidater);
        Assert.Null(await FunnAsync(db, f));
    }

    [Fact]
    public void H14_Baerer_en_videreforing_som_ikke_nevner_vedtakstypen_alene_Nei_samlet_status_blir_uavklart()
    {
        Guid id = Guid.NewGuid();
        HjemmelPremiss Opphevet() => new(Guid.NewGuid(), "sentralbankloven", Hjemmelsstatus.Materiell, Hjemmelsstatus.Opphevet, Strukturkanter.Tekstfunn, null);
        HjemmelPremiss Videreført(string dekning) =>
            new(Guid.NewGuid(), "finansvl", Hjemmelsstatus.Materiell, Hjemmelsstatus.Viderefort, Strukturkanter.Fortolkning, null, dekning);

        // DEL/forskrift/2003-06-20-730: sentralbankloven 1985 faller, finansvl. 1988 er videreført av finansforetaksloven § 23-2 andre ledd,
        // som bare nevner «forskrifter».
        var h14 = HjemmelsstatusSamlet.Beregn(id, _ => [Opphevet(), Videreført(Hjemmelsstatus.DokumenttypeIkkeNevnt)]);
        Assert.Equal(Hjemmelsstatus.SamletUavklart, h14.Status);
        Assert.Equal(HjemmelOpphevetFunnregel.UndertypeUavklart, HjemmelOpphevetFunnregel.Undertype(h14));

        // Dekket videreføring bærer: blandede hjemler som før.
        Assert.Equal(Hjemmelsstatus.SamletBlandede, HjemmelsstatusSamlet.Beregn(id, _ => [Opphevet(), Videreført(Hjemmelsstatus.Dekket)]).Status);

        // Når ingenting faller, endrer det ingenting: FOR-1993-12-17-1314 er videreført (fase 1 AK 2, «så langt de passer»).
        Assert.Equal(Hjemmelsstatus.SamletViderefort, HjemmelsstatusSamlet.Beregn(id, _ => [Videreført(Hjemmelsstatus.DokumenttypeIkkeNevnt)]).Status);
    }

    [Fact]
    public async Task Oppfrisk_Skrives_et_lagret_funn_med_gammel_pastand_pa_nytt_nar_ingen_har_rort_det()
    {
        await using var db = fixture.NyDbContext();
        var vedtak = await ByggH08Async(new Korpus(db), db);
        await KjorAsync(db, vedtak, skriv: true);
        var funn = (await FunnAsync(db, vedtak))!;
        // Påstanden slik PR 2 skrev den.
        await db.Funn.Where(x => x.Id == funn.Id).ExecuteUpdateAsync(x => x.SetProperty(f => f.Beskrivelse, "Lovdata viser … som gjeldende."));

        var torr = await KjorAsync(db, vedtak, skriv: true, oppfrisk: false);
        Assert.Equal((1, 0), (torr.Oppfriskes, torr.Oppfrisket));
        var r = await KjorAsync(db, vedtak, skriv: true, oppfrisk: true);
        Assert.Equal((1, 1), (r.Oppfriskes, r.Oppfrisket));
        db.ChangeTracker.Clear();
        Assert.Contains(FunnTjeneste.KunngjortMangelStart, (await FunnAsync(db, vedtak))!.Beskrivelse);
        Assert.Equal(0, (await KjorAsync(db, vedtak, skriv: true, oppfrisk: true)).Oppfriskes);
    }
}
