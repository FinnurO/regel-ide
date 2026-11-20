namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, #472 PR 1, Johanns avgjørelser 2026-11-18] Statusen per hjemmel og maskinregelen. Én test per akseptansekriterium 1–7 i fase 1 (§6),
/// formulert som spørsmålene modellen skal kunne besvare (CLAUDE.md §0). Eksemplene er juristutvalgets (S01, S04/S07, S15, S20), bygget med
/// tilfeldige numre så de ikke kolliderer med andre tester i den delte testbasen. Kriterium 8 (juristens presisjonsmåling) er ikke en test.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class HjemmelsstatusTests(EmbeddedPostgresFixture fixture)
{
    private static readonly string[] Maaneder =
        ["januar", "februar", "mars", "april", "mai", "juni", "juli", "august", "september", "oktober", "november", "desember"];

    /// <summary>«11. juni 1993 nr. 65».</summary>
    internal static string Ref(DateOnly d, int nr) => $"{d.Day}. {Maaneder[d.Month - 1]} {d.Year} nr. {nr}";

    internal static string Eli(string type, DateOnly d, int nr) => $"https://lovdata.no/eli/{type}/{d.Year}/{d.Month:00}/{d.Day:00}/{nr}/nor";

    internal static int Nr() => Random.Shared.Next(100_000, 999_999);

    /// <summary>Et lite korpus: dokumenter med noder, stubber, hjemler og «Endrer».</summary>
    internal sealed class Korpus(RegelIdeDbContext db)
    {
        /// <summary>En gjeldende primærkilde. Nodene gis som (relativ eId, tekst, overskrift); forelderen er eId-en uten siste ledd når den er
        /// gitt. Eid-ene lagres med ELI-en foran, som Lovdata-importen gjør.</summary>
        public async Task<RettskildeEntitet> DokumentAsync(string type, DateOnly dato, int nr, string prefiks, string tittel,
            (string Eid, string? Tekst, string? Overskrift)[] noder, string? annetOm = null, bool? rentEndringsvedtak = null)
        {
            var eli = Eli(type, dato, nr);
            var rk = new RettskildeEntitet
            {
                Id = Guid.NewGuid(), Doctype = "act", Kildetype = type == "lov" ? "Lov" : "Forskrift", Status = "Gjeldende", Importrolle = "primaer",
                Tittel = tittel, Eli = eli, DokumentId = $"{prefiks}/{type}/{dato:yyyy-MM-dd}-{nr}", AknXml = "<akomaNtoso/>",
                AnnetOmDokumentet = annetOm, RentEndringsvedtak = rentEndringsvedtak, Ikrafttredelse = dato,
                OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            };
            db.Rettskilder.Add(rk);
            var ider = new Dictionary<string, Guid>();
            var i = 0;
            foreach (var (rel, tekst, overskrift) in noder)
            {
                var id = Guid.NewGuid();
                ider[rel] = id;
                var forelder = rel.Contains('/') ? rel[..rel.LastIndexOf('/')] : null;
                var siste = rel.Split('/')[^1];
                db.RettskildeNoder.Add(new RettskildeNodeEntitet
                {
                    Id = id, RettskildeId = rk.Id, Eid = $"{eli}/{rel}", KildeId = rel, Tekst = tekst, Overskrift = overskrift, Sorteringsrekkefolge = i++,
                    NodeType = siste.StartsWith("ledd") ? "ledd" : siste.StartsWith("punkt") ? "punkt" : siste.StartsWith("kap") ? "kapittel" : "paragraf",
                    ParentNodeId = forelder is not null && ider.TryGetValue(forelder, out var p) ? p : null, TekstHash = tekst is null ? null : $"h{i}",
                });
            }
            await db.SaveChangesAsync();
            return rk;
        }

        /// <summary>En referanse-stub, som importen lager: <c>entitetsstatus = gjeldende</c>, status Utkast, ELI-en som tittel.</summary>
        public async Task<RettskildeEntitet> StubAsync(string type, DateOnly dato, int nr)
        {
            var eli = Eli(type, dato, nr);
            var rk = new RettskildeEntitet
            {
                Id = Guid.NewGuid(), Doctype = "act", Kildetype = type == "lov" ? "Lov" : "Forskrift", Status = "Utkast", Importrolle = "referanse",
                Tittel = eli, Eli = eli, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            };
            db.Rettskilder.Add(rk);
            await db.SaveChangesAsync();
            return rk;
        }

        public async Task HjemlerAsync(RettskildeEntitet f, params (RettskildeEntitet Mal, string? Bestemmelse)[] hjemler)
        {
            var i = 0;
            foreach (var (mal, b) in hjemler)
            {
                db.RettskildeHjemler.Add(new RettskildeHjemmelEntitet
                {
                    Id = Guid.NewGuid(), RettskildeId = f.Id, HjemmelEid = b is null ? mal.Eli! : $"{mal.Eli}/{b}", HjemmelRettskildeId = mal.Id,
                    Sorteringsrekkefolge = i++,
                });
            }
            await db.SaveChangesAsync();
        }

        public async Task EndrerAsync(RettskildeEntitet kilde, RettskildeEntitet mal)
        {
            db.RettskildeEndringer.Add(new RettskildeEndringEntitet { Id = Guid.NewGuid(), RettskildeId = kilde.Id, EndringEid = mal.Eli!, EndringRettskildeId = mal.Id });
            await db.SaveChangesAsync();
        }
    }

    /// <summary>S01: DEL/forskrift/1993-12-17-1314 og hjemlene, med konkurranseloven 2004 og LTI/forskrift/2004-04-16-631.</summary>
    private sealed record S01(RettskildeEntitet F, RettskildeEntitet L65, RettskildeEntitet L66, RettskildeEntitet F1022, RettskildeEntitet F1023,
        RettskildeEntitet L2004, RettskildeEntitet Lti631);

    private static async Task<S01> ByggS01Async(Korpus k)
    {
        DateOnly d65 = new(1993, 6, 11), dKgl = new(1993, 11, 12);
        int n65 = Nr(), n66 = Nr(), n1022 = Nr(), n1023 = Nr();
        var l65 = await k.StubAsync("lov", d65, n65);
        var l66 = await k.DokumentAsync("lov", d65, n66, "NL", "Lov om pristiltak", [("§1", null, "Formål"), ("§1/ledd-1", "Loven skal …", null)]);
        var f1022 = await k.StubAsync("forskrift", dKgl, n1022);
        var f1023 = await k.DokumentAsync("forskrift", dKgl, n1023, "DEL", "Delegering av myndighet etter lov om pristiltak.",
            [("ledd-1", "Myndigheten delegeres til departementet.", null)]);
        var l2004 = await k.DokumentAsync("lov", new(2004, 3, 5), Nr(), "NL", "Lov om konkurranse mellom foretak og kontroll med foretakssammenslutninger",
        [
            ("§48", null, "Ikrafttredelse"),
            ("§48/ledd-1", "Loven trer i kraft fra den tid Kongen bestemmer.", null),
            ("§48/ledd-2", $"Fra samme tid oppheves lov {Ref(d65, n65)} om konkurranse i ervervsvirksomhet.", null),
            ("§49", null, "Overgangsregler"),
            ("§49/ledd-1", $"Forskrifter, vedtekter, reglementer og instrukser som er gitt i medhold av lov {Ref(d65, n65)} om konkurranse i "
                + "ervervsvirksomhet gjelder fortsatt så langt de passer, inntil Kongen opphever eller endrer disse i medhold av denne lov eller ved "
                + "særskilt bestemmelse.", null),
        ]);
        await k.EndrerAsync(l2004, l65);
        var lti = await k.DokumentAsync("forskrift", new(2004, 4, 16), Nr(), "LTI",
            "Delegering av myndighet til Arbeids- og administrasjonsdepartementet etter konkurranseloven.",
        [
            ("kap-I", null, null),
            ("kap-I/ledd-1", "Arbeids- og administrasjonsdepartementet gis myndighet til å vedta forskrifter etter konkurranseloven § 7 tredje ledd.", null),
            ("kap-II", null, null),
            ("kap-II/ledd-1", "Bestemmelsen under del I trer i kraft straks.", null),
            ("kap-III", null, null),
            ("kap-III/ledd-1", $"Vedtak {Ref(dKgl, n1022)} om ikrafttredelse av lov {Ref(d65, n65)} om konkurranse i ervervsvirksomhet "
                + "(konkurranseloven). Delegering av myndighet, oppheves.", null),
        ]);
        await k.EndrerAsync(lti, f1022);
        var f = await k.DokumentAsync("forskrift", new(1993, 12, 17), Nr(), "DEL",
            "Delegering av myndighet etter lov om konkurranse i ervervsvirksomhet (konkurranseloven) og lov om pristiltak.",
            [("ledd-1", "Myndigheten etter konkurranseloven § 7-2 delegeres til Konkurransetilsynet.", null)],
            annetOm: $"Fastsatt av Administrasjonsdepartementet 17. desember 1993 med hjemmel i lov av {Ref(d65, n65)} om konkurranse i "
                + $"ervervsvirksomhet (konkurranseloven) § 7-2 og lov av {Ref(d65, n66)} om pristiltak § 1, jf. kgl.res. av {Ref(dKgl, n1022)} og "
                + $"kgl.res. av {Ref(dKgl, n1023)}.");
        await k.HjemlerAsync(f, (l65, "§7-2"), (l66, "§1"), (f1022, null), (f1023, null));
        return new S01(f, l65, l66, f1022, f1023, l2004, lti);
    }

    private static HjemmelsstatusEntitet Rad(IReadOnlyDictionary<Guid, HjemmelsstatusEntitet> rader, Guid forskrift, RettskildeEntitet mal) =>
        Assert.Single(rader.Values, r => r.RettskildeId == forskrift && r.HjemmelEli == mal.Eli);

    [Fact]
    public async Task AK1_Hvilke_hjemler_har_FOR_1993_12_17_1314_og_hvilke_er_opphevet_av_hvilken_bestemmelse()
    {
        await using var db = fixture.NyDbContext();
        var s = await ByggS01Async(new Korpus(db));
        await new HjemmelsstatusRegel(db).KjorAsync(skriv: true, "Test", lesLovtidend: false, forskriftIder: [s.F.Id]);
        var (_, rader) = (await new HjemmelsstatusRegel(db).LesAsync(s.F.Id))!.Value;

        // 1993-65 § 7-2: opphevet av konkurranseloven 2004 § 48 andre ledd, med sitat (og videreført, AK 2).
        var l65 = Rad(rader, s.F.Id, s.L65);
        Assert.Equal(Hjemmelsstatus.Materiell, l65.Rolle);
        Assert.Equal(Hjemmelsstatus.E1, l65.Bevistrinn);
        Assert.Equal(s.L2004.Id, l65.OpphevetAvRettskildeId);
        Assert.Equal($"{s.L2004.Eli}/§48/ledd-2", l65.OpphevetAvEid);
        Assert.NotNull(l65.OpphevetAvNodeId);
        Assert.StartsWith("Fra samme tid oppheves lov 11. juni 1993 nr.", l65.OpphevetAvSitat);
        Assert.Equal(s.L2004.Ikrafttredelse, l65.OpphevetDato);

        // 1993-66 og FOR-1023: aktive. FOR-1023 er et delegeringsvedtak og dermed et kompetanseledd.
        Assert.Equal(Hjemmelsstatus.Aktiv, Rad(rader, s.F.Id, s.L66).Status);
        Assert.Equal(Hjemmelsstatus.Materiell, Rad(rader, s.F.Id, s.L66).Rolle);
        var f1023 = Rad(rader, s.F.Id, s.F1023);
        Assert.Equal((Hjemmelsstatus.Aktiv, Hjemmelsstatus.Kompetanseledd), (f1023.Status, f1023.Rolle));

        // FOR-1022: kompetanseledd, opphevet av LTI 2004-631 kap. III (utvidet form: punktum i tittelen) og erstattet av kap. I.
        var f1022 = Rad(rader, s.F.Id, s.F1022);
        Assert.Equal(Hjemmelsstatus.Kompetanseledd, f1022.Rolle);
        Assert.Equal("jf_vedtak", f1022.RolleRegel);
        Assert.Equal(Hjemmelsstatus.Erstattet, f1022.Status);
        Assert.Equal(Hjemmelsstatus.E1u, f1022.Bevistrinn);
        Assert.Equal($"{s.Lti631.Eli}/kap-III/ledd-1", f1022.OpphevetAvEid);
        Assert.Equal(s.Lti631.Id, f1022.ErstattetAvRettskildeId);
        Assert.Equal($"{s.Lti631.Eli}/kap-I/ledd-1", f1022.ErstattetAvEid);
        Assert.Equal(Strukturkanter.Fortolkning, f1022.Kunnskapsstatus);
    }

    [Fact]
    public async Task AK2_Er_FOR_1993_12_17_1314_viderefort_og_med_hvilket_forbehold()
    {
        await using var db = fixture.NyDbContext();
        var s = await ByggS01Async(new Korpus(db));
        await new HjemmelsstatusRegel(db).KjorAsync(skriv: true, "Test", lesLovtidend: false, forskriftIder: [s.F.Id]);
        var (samlet, rader) = (await new HjemmelsstatusRegel(db).LesAsync(s.F.Id))!.Value;

        var l65 = Rad(rader, s.F.Id, s.L65);
        Assert.Equal(Hjemmelsstatus.Viderefort, l65.Status);
        Assert.Equal("V1", l65.VidereforingForm);
        Assert.Equal($"{s.L2004.Eli}/§49/ledd-1", l65.VidereforingEid);
        Assert.Contains("gjelder fortsatt så langt de passer", l65.VidereforingSitat);
        Assert.Equal("så langt de passer", l65.VidereforingForbehold);
        Assert.Null(l65.VidereforingTidsfrist);
        // Setningen nevner «forskrifter, vedtekter, reglementer og instrukser», ikke delegering: fortolkning (juristrunde 1 R3).
        Assert.Equal(Hjemmelsstatus.DokumenttypeIkkeNevnt, l65.VidereforingDekning);
        Assert.Equal(Strukturkanter.Fortolkning, l65.Kunnskapsstatus);

        // Samlet: videreført, ikke delvis sovende (FOR-1022 er et kompetanseledd og teller ikke), med kunnskapsstatus fortolkning.
        Assert.Equal(Hjemmelsstatus.SamletViderefort, samlet.Status);
        Assert.Equal(Strukturkanter.Fortolkning, samlet.Kunnskapsstatus);
        Assert.Equal(l65.RettskildeHjemmelId, samlet.SvakestePremissId);
        Assert.False(samlet.Premisser.Single(p => p.Rad.HjemmelId == Rad(rader, s.F.Id, s.F1022).RettskildeHjemmelId).Teller);
    }

    [Fact]
    public async Task AK3_Hvor_mange_forskrifter_har_minst_en_opphevet_hjemmel_og_hvordan_fordeler_de_seg()
    {
        await using var db = fixture.NyDbContext();
        var k = new Korpus(db);
        var s = await ByggS01Async(k);
        // Et rent endringsvedtak (#446-kolonnen) med en opphevet hjemmel telles for seg.
        var endring = await k.DokumentAsync("forskrift", new(1995, 1, 1), Nr(), "LTI", "Forskrift om endring i forskrift om noe",
            [("ledd-1", "I forskrift … gjøres følgende endring: …", null)],
            annetOm: $"Fastsatt med hjemmel i lov {Ref(new(1993, 6, 11), int.Parse(s.L65.Eli!.Split('/')[^2]))} om konkurranse i ervervsvirksomhet.",
            rentEndringsvedtak: true);
        await k.HjemlerAsync(endring, (s.L65, "§1-1"));

        var r = await new HjemmelsstatusRegel(db).KjorAsync(skriv: false, "Test", lesLovtidend: false, forskriftIder: [s.F.Id, endring.Id]);
        Assert.False(r.Skrevet);
        Assert.Equal(r.Hjemmelsrader, r.PerStatus.Values.Sum());
        Assert.Equal(r.Hjemmelsrader, r.PerBevistrinn.Values.Sum());
        Assert.Equal(r.Forskrifter.MedHjemmel, r.Forskrifter.PerSamletStatus.Values.Sum());
        Assert.Equal(1, r.Forskrifter.PerRentEndringsvedtak["rent_endringsvedtak"].Values.Sum());
        Assert.Equal(1, r.Forskrifter.PerRentEndringsvedtak["rent_endringsvedtak"][Hjemmelsstatus.SamletViderefort]);
        Assert.Equal(2, r.Forskrifter.MinstEnHjemmelOpphevetMedTekstbevis);
        // Tørrkjøringen skriver ingenting.
        Assert.False(db.Hjemmelsstatuser.Any(x => x.RettskildeId == endring.Id));
    }

    [Fact]
    public async Task AK4_Blir_en_referanserad_noen_gang_aktiv_bare_fordi_den_star_som_gjeldende()
    {
        await using var db = fixture.NyDbContext();
        var k = new Korpus(db);
        // En lov som bare finnes som referanse-stub, uten spor i korpuset.
        var stub = await k.StubAsync("lov", new(1931, 2, 3), Nr());
        Assert.Equal("gjeldende", stub.Entitetsstatus);
        var f = await k.DokumentAsync("forskrift", new(1960, 1, 1), Nr(), "SF", "Forskrift om noe", [("ledd-1", "Tekst.", null)],
            annetOm: $"Fastsatt med hjemmel i lov {Ref(new(1931, 2, 3), int.Parse(stub.Eli!.Split('/')[^2]))} om noe § 3.");
        await k.HjemlerAsync(f, (stub, "§3"));

        var r = await new HjemmelsstatusRegel(db).KjorAsync(skriv: true, "Test", lesLovtidend: false, forskriftIder: [f.Id]);
        Assert.Equal(0, r.Referanserader.Aktive);
        Assert.Equal(r.Referanserader.Rader, r.Referanserader.MedEntitetsstatusGjeldende);
        var (samlet, rader) = (await new HjemmelsstatusRegel(db).LesAsync(f.Id))!.Value;
        var rad = Rad(rader, f.Id, stub);
        Assert.Equal(Hjemmelsstatus.Ukjent, rad.Status);
        Assert.Equal(Hjemmelsstatus.GrunnIngen, rad.UkjentGrunn);
        Assert.Equal(Strukturkanter.Uavklart, rad.Kunnskapsstatus);
        Assert.Equal(Hjemmelsstatus.SamletUkjent, samlet.Status);
    }

    [Fact]
    public async Task AK5_Far_SF_2017_06_16_1124_videreforing_utlopt_31_12_2019_fra_fagskoleloven_2018_paragraf_44()
    {
        await using var db = fixture.NyDbContext();
        var k = new Korpus(db);
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

        await new HjemmelsstatusRegel(db).KjorAsync(skriv: true, "Test", lesLovtidend: false, forskriftIder: [f.Id]);
        var (samlet, rader) = (await new HjemmelsstatusRegel(db).LesAsync(f.Id))!.Value;
        foreach (var r in rader.Values.Where(r => r.HjemmelEli == l2003.Eli))
        {
            Assert.Equal(Hjemmelsstatus.VidereforingUtlopt, r.Status);
            Assert.Equal(new DateOnly(2019, 12, 31), r.VidereforingTidsfrist);
            Assert.Equal($"{l2018.Eli}/§44/ledd-3", r.VidereforingEid);
            Assert.Equal($"{l2018.Eli}/§44/ledd-2", r.OpphevetAvEid);
        }
        var p44 = Assert.Single(rader.Values, r => r.HjemmelEli == l2018.Eli);
        Assert.Equal((Hjemmelsstatus.Overgangsgrunnlag, Hjemmelsstatus.Aktiv), (p44.Rolle, p44.Status));
        // Overgangsgrunnlaget teller ikke: forskriften står ikke på en aktiv hjemmel når videreføringen er utløpt.
        Assert.Equal(Hjemmelsstatus.SamletVidereforingUtlopt, samlet.Status);
        Assert.Equal(Strukturkanter.Fortolkning, samlet.Kunnskapsstatus);
    }

    [Fact]
    public async Task AK6_Far_S04_og_S07_samme_status_for_FOR_2016_12_02_1426()
    {
        await using var db = fixture.NyDbContext();
        var k = new Korpus(db);
        DateOnly dToll = new(2007, 12, 21);
        var nToll = Nr();
        var toll = await k.StubAsync("lov", dToll, nToll);
        var varefl = await k.DokumentAsync("lov", new(2022, 3, 11), Nr(), "NL", "Lov om inn- og utførsel av varer (vareførselsloven)",
            [("§14-1", null, "Ikrafttredelse"), ("§14-1/ledd-1", $"Loven gjelder fra den tid Kongen bestemmer. Fra samme tidspunkt oppheves lov {Ref(dToll, nToll)} om toll og vareførsel.", null)]);
        await k.EndrerAsync(varefl, toll);
        var n1426 = Nr();
        var s04 = await k.DokumentAsync("forskrift", new(2016, 12, 2), n1426, "DEL", "Tildeling av myndighet etter tolloven § 8-3",
            [("ledd-1", "Myndigheten etter tolloven § 8-3 tildeles Finansdepartementet.", null)],
            annetOm: $"Hjemmel: Fastsatt ved kgl.res. 2. desember 2016 med hjemmel i lov {Ref(dToll, nToll)} om toll og vareførsel (tolloven) § 8-3.");
        await k.HjemlerAsync(s04, (toll, "§8-3"));
        var s07 = await k.DokumentAsync("forskrift", new(2008, 3, 7), Nr(), "SF", "Forskrift om sikkerhetsmekanisme ved import av landbruksvarer",
            [("§1", null, null), ("§1/ledd-1", "Tekst.", null)],
            annetOm: $"Tilføyd hjemmel: Lov {Ref(dToll, nToll)} om toll og vareførsel (tolloven) § 8-3. Delegeringsvedtak {Ref(new(2016, 12, 2), n1426)}.");
        await k.HjemlerAsync(s07, (toll, "§8-3"), (s04, null));

        // Bare S07 i omfanget: regelen tar med S04 fordi S07 bygger på den.
        await new HjemmelsstatusRegel(db).KjorAsync(skriv: true, "Test", lesLovtidend: false, forskriftIder: [s07.Id]);
        var (samlet04, _) = (await new HjemmelsstatusRegel(db).LesAsync(s04.Id))!.Value;
        var (samlet07, rader07) = (await new HjemmelsstatusRegel(db).LesAsync(s07.Id))!.Value;

        Assert.Equal(Hjemmelsstatus.SamletHjemmelOpphevet, samlet04.Status);
        var rad1426 = Rad(rader07, s07.Id, s04);
        Assert.Equal((Hjemmelsstatus.Kompetanseledd, Hjemmelsstatus.Aktiv), (rad1426.Rolle, rad1426.Status));
        var p = samlet07.Premisser.Single(x => x.Rad.HjemmelId == rad1426.RettskildeHjemmelId);
        // Samme status for FOR-2016-1426 i S04 og i S07: arvet, ikke «aktiv».
        Assert.Equal(samlet04.Status, p.Arvet!.Status);
        Assert.Equal(Hjemmelsstatus.Opphevet, p.Status);
        Assert.False(p.Teller);
        Assert.Equal(Hjemmelsstatus.SamletHjemmelOpphevet, samlet07.Status);
    }

    [Fact]
    public void AK6_En_hjemmelskjede_i_sirkel_gir_ukjent_og_ikke_uendelig_rekursjon()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();
        var rader = new Dictionary<Guid, IReadOnlyList<HjemmelPremiss>>
        {
            [a] = [new(Guid.NewGuid(), "a", Hjemmelsstatus.Materiell, Hjemmelsstatus.Aktiv, Strukturkanter.Normalisering, b)],
            [b] = [new(Guid.NewGuid(), "b", Hjemmelsstatus.Materiell, Hjemmelsstatus.Aktiv, Strukturkanter.Normalisering, a)],
        };
        var s = HjemmelsstatusSamlet.Beregn(a, id => rader.GetValueOrDefault(id));
        Assert.Equal(Hjemmelsstatus.SamletUkjent, s.Status);
        Assert.Equal(Strukturkanter.Uavklart, s.Kunnskapsstatus);
    }

    [Fact]
    public async Task AK7_Leses_FOR_1988_312_i_S15_som_opphevet_av_formelen_i_foreldrenoden()
    {
        await using var db = fixture.NyDbContext();
        var k = new Korpus(db);
        DateOnly d312 = new(1988, 4, 29);
        var n312 = Nr();
        var f312 = await k.StubAsync("forskrift", d312, n312);
        var f1790 = await k.DokumentAsync("forskrift", new(2003, 12, 19), Nr(), "DEL", "Delegering av myndighet til Mattilsynet",
        [
            ("kap-VI", null, null),
            ("kap-VI/ledd-1", "Fra 1. januar 2004 oppheves følgende delegeringer:", null),
            ("kap-VI/ledd-1/punkt-1", "Kongelig resolusjon fra Forbruker- og administrasjonsdepartementet av 29. januar 1988 om opprettelsen av Statens næringsmiddeltilsyn.", null),
            ("kap-VI/ledd-1/punkt-2", $"Kongelig resolusjon fra Forbruker- og administrasjonsdepartementet av {Ref(d312, n312)} om myndighetsoverføring til "
                + "Statens næringsmiddeltilsyn m.v.", null),
        ]);
        await k.EndrerAsync(f1790, f312);
        var s15 = await k.DokumentAsync("forskrift", new(2002, 12, 4), Nr(), "LTI",
            "Vedtak om opphevelse av vedtak om delvis tilbaketrekking av delegert myndighet på næringsmiddelområdet.",
            [("ledd-1", "Vedtaket oppheves.", null)],
            annetOm: $"Fastsatt av Statens næringsmiddeltilsyn 4. desember 2002 med hjemmel i kgl.res. av {Ref(d312, n312)}.");
        await k.HjemlerAsync(s15, (f312, null));

        await new HjemmelsstatusRegel(db).KjorAsync(skriv: true, "Test", lesLovtidend: false, forskriftIder: [s15.Id]);
        var (_, rader) = (await new HjemmelsstatusRegel(db).LesAsync(s15.Id))!.Value;
        var rad = Rad(rader, s15.Id, f312);
        Assert.Equal(Hjemmelsstatus.E1, rad.Bevistrinn);
        Assert.Equal(f1790.Id, rad.OpphevetAvRettskildeId);
        Assert.Equal($"{f1790.Eli}/kap-VI/ledd-1/punkt-2", rad.OpphevetAvEid);
        Assert.StartsWith("Fra 1. januar 2004 oppheves følgende delegeringer: Kongelig resolusjon", rad.OpphevetAvSitat);
        Assert.Equal("kap. VI første ledd punkt 2", Hjemmelstekstlesing.Bestemmelsesetikett(rad.OpphevetAvEid!));
    }

    // ---- EN med Lovtidend (Johanns avgjørelse 2026-11-18 (3)) --------------------------------------------------------------------

    private sealed class FalskKunngjort(Dictionary<string, IReadOnlyList<KunngjortNode>> tekster) : IKunngjortTekstkilde
    {
        public List<string> Etterspurt { get; } = [];

        public Task<IReadOnlyDictionary<string, IReadOnlyList<KunngjortNode>>> HentAsync(IReadOnlyCollection<string> eliListe, CancellationToken ct)
        {
            Etterspurt.AddRange(eliListe);
            return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<KunngjortNode>>>(
                tekster.Where(t => eliListe.Contains(t.Key)).ToDictionary(t => t.Key, t => t.Value));
        }
    }

    [Fact]
    public async Task EN_gruppen_leses_i_den_kunngjorte_teksten_nar_den_konsoliderte_har_mistet_opphevingssetningen()
    {
        await using var db = fixture.NyDbContext();
        var k = new Korpus(db);
        DateOnly d2005 = new(2005, 4, 1);
        var n2005 = Nr();
        var uhl2005 = await k.StubAsync("lov", d2005, n2005);
        // Den konsoliderte uhl. 2024: «Endrer» peker på uhl. 2005, men opphevingssetningen er borte. Overgangsregelen står.
        var uhl2024 = await k.DokumentAsync("lov", new(2024, 3, 8), Nr(), "NL", "Lov om universiteter og høyskoler (universitets- og høyskoleloven)",
        [
            ("§17-2", null, "Ikrafttredelse og opphevelse"),
            ("§17-2/ledd-1", "Loven gjelder fra den tiden Kongen bestemmer.", null),
            ("§17-3", null, "Overgangsregler"),
            ("§17-3/ledd-1", "Forskrifter og enkeltvedtak som er fastsatt med hjemmel i regler som blir opphevet eller endret med denne loven, gjelder til "
                + "departementet fastsetter noe annet.", null),
        ]);
        await k.EndrerAsync(uhl2024, uhl2005);
        var f = await k.DokumentAsync("forskrift", new(2006, 1, 1), Nr(), "SF", "Forskrift om opptak", [("§1", null, null), ("§1/ledd-1", "Tekst.", null)],
            annetOm: $"Fastsatt med hjemmel i lov {Ref(d2005, n2005)} om universiteter og høyskoler § 3-6.");
        await k.HjemlerAsync(f, (uhl2005, "§3-6"));

        // Uten Lovtidend: EN, ukjent, med videreføringen som opplysning.
        var uten = await new HjemmelsstatusRegel(db).KjorAsync(skriv: false, "Test", lesLovtidend: false, medRader: true, forskriftIder: [f.Id]);
        var radUten = Assert.Single(uten.Rader!, r => r.HjemmelEid.StartsWith(uhl2005.Eli!));
        Assert.Equal((Hjemmelsstatus.Ukjent, Hjemmelsstatus.GrunnEn), (radUten.Status, radUten.UkjentGrunn));

        // Med Lovtidend: setningen står i endringsblokken i den kunngjorte teksten.
        var kunngjort = new FalskKunngjort(new()
        {
            [uhl2024.Eli!] = LovtidendKunngjortTekstkilde.Endringsblokker(
                "<section><article class=\"legalArticle\" data-name=\"§17-2\"><h3>§ 17-2. Ikrafttredelse og opphevelse</h3>"
                + "<article class=\"document-change\" data-document=\"lov/2005-04-01-x\"><article class=\"change\" data-repeal-part=\"lov/2005-04-01-x\">"
                + $"<article class=\"legalP\">Loven trer i kraft fra den tiden Kongen bestemmer. Samtidig oppheves lov {Ref(d2005, n2005)} om universiteter og "
                + "høyskoler. Kongen kan sette i kraft de ulike reglene til ulik tid.</article></article></article></article></section>", uhl2024.Eli!).ToList(),
        });
        var med = await new HjemmelsstatusRegel(db, kunngjort).KjorAsync(skriv: true, "Test", lesLovtidend: true, forskriftIder: [f.Id]);
        Assert.Contains(uhl2024.Eli!, kunngjort.Etterspurt);
        Assert.Equal(1, med.Lovtidend.EnDokumenterLost);
        var (samlet, rader) = (await new HjemmelsstatusRegel(db).LesAsync(f.Id))!.Value;
        var rad = Rad(rader, f.Id, uhl2005);
        Assert.Equal(Hjemmelsstatus.EnLovtidend, rad.Bevistrinn);
        Assert.True(rad.OpphevetFraKunngjort);
        Assert.Null(rad.OpphevetAvNodeId);
        Assert.Equal(uhl2024.Id, rad.OpphevetAvRettskildeId);
        Assert.Equal($"{uhl2024.Eli}/§17-2", rad.OpphevetAvEid);
        Assert.Equal("Samtidig oppheves lov " + Ref(d2005, n2005) + " om universiteter og høyskoler.", rad.OpphevetAvSitat);
        // «Kongen kan sette i kraft de ulike reglene til ulik tid» står bare i den kunngjorte teksten her; premissmangelen leses i korpuset.
        // Videreføringen er den generiske formen i § 17-3 (V3).
        Assert.Equal((Hjemmelsstatus.Viderefort, "V3"), (rad.Status, rad.VidereforingForm));
        Assert.Equal($"{uhl2024.Eli}/§17-3/ledd-1", rad.VidereforingEid);
        Assert.Equal(Strukturkanter.Normalisering, rad.Kunnskapsstatus);
        Assert.Equal(Hjemmelsstatus.SamletViderefort, samlet.Status);
    }

    // ---- Tekstformene -----------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Forskrifter gitt med hjemmel i lov 20. juni 2003 nr. 56 skal gjelde frem til 31. desember 2019 eller til forskriften blir opphevet.", "2019-12-31")]
    [InlineData("Forskriften gjelder for budsjett- og regnskapsåret 1993.", "1993-12-31")]
    [InlineData("Forskrifter gitt i medhold av lov 11. juni 1993 nr. 65 gjelder fortsatt inntil Kongen bestemmer annet.", null)]
    [InlineData("Forskrifter gitt med hjemmel i lov 1. januar 2000 nr. 1 gjelder til 1. mars 2001 nr. 3 oppheves.", null)]
    public void Tidsfristen_leses_av_videreforingen(string s, string? frist) =>
        Assert.Equal(frist is null ? null : DateOnly.Parse(frist), Hjemmelstekstlesing.Tidsfrist(s));

    [Fact]
    public void Avgrensningen_i_videreforingen_sammenlignes_med_hjemmelens_paragraf()
    {
        var mal = Hjemmelstekstlesing.Mal("https://lovdata.no/eli/lov/1982/11/19/66/nor")!;
        const string s = "Forskrifter gitt i medhold av lov 19. november 1982 nr. 66 om helsetjenesten i kommunene kapittel 4a og § 7-9 gjelder fortsatt.";
        var (kap, par) = Hjemmelstekstlesing.Avgrensning(s, mal);
        Assert.Equal(["4a"], kap);
        Assert.Equal(["7-9"], par);
        Assert.Equal("4a-1", Hjemmelstekstlesing.Paragraf("https://lovdata.no/eli/lov/1982/11/19/66/nor/§4a-1/ledd-2"));
    }

    [Theory]
    // Juristens vakt (fase 1): målet står i tittelen til noe annet som oppheves.
    [InlineData("Forskrift 12. november 1993 nr. 1023 om ikrafttredelse av lov 11. juni 1993 nr. 65 oppheves.", 1993, 6, 11, "65", false)]
    [InlineData("Fra samme tid oppheves lov 11. juni 1993 nr. 65 om konkurranse i ervervsvirksomhet.", 1993, 6, 11, "65", true)]
    // AK 7: utstederen mellom dokumenttypen og datoen.
    [InlineData("Fra 1. januar 2004 oppheves følgende delegeringer: Kongelig resolusjon fra Forbruker- og administrasjonsdepartementet av 29. april 1988 nr. 312 om myndighetsoverføring.", 1988, 4, 29, "312", true)]
    public void Vakten_skiller_malet_fra_et_dokument_i_tittelen(string s, int aar, int mnd, int dag, string nr, bool objekt) =>
        Assert.Equal(objekt, Hjemmelstekstlesing.MalErObjekt(s, new Dokumentmal("lov", aar, mnd, dag, nr)));

    [Fact]
    public void Den_utvidede_formen_leser_passiv_og_punktum_i_tittelen()
    {
        var m1022 = new Dokumentmal("forskrift", 1993, 11, 12, "1022");
        Assert.Null(Hjemmelstekstlesing.FullOpphevingssetning(
            "Vedtak 12. november 1993 nr. 1022 om ikrafttredelse av lov 11. juni 1993 nr. 65 (konkurranseloven). Delegering av myndighet, oppheves.", m1022));
        Assert.NotNull(Hjemmelstekstlesing.UtvidetOpphevingssetning(
            "Vedtak 12. november 1993 nr. 1022 om ikrafttredelse av lov 11. juni 1993 nr. 65 (konkurranseloven). Delegering av myndighet, oppheves.", m1022));
        var m4 = new Dokumentmal("lov", 1968, 6, 7, "4");
        // «til» etter nummeret, og passiv.
        Assert.Null(Hjemmelstekstlesing.FullOpphevingssetning("Fra samme tid oppheves lov 7. juni 1968 nr. 4 til gjennomføring av noe.", m4));
        Assert.NotNull(Hjemmelstekstlesing.UtvidetOpphevingssetning("Fra samme tid oppheves lov 7. juni 1968 nr. 4 til gjennomføring av noe.", m4));
        Assert.NotNull(Hjemmelstekstlesing.UtvidetOpphevingssetning("Lov 7. juni 1968 nr. 4 om noe blir opphevet.", m4));
        // Lov uten nummer: datoen som ikke følges av «nr.».
        var uten = Hjemmelstekstlesing.Mal("https://lovdata.no/eli/lov/1925/07/15/nor")!;
        Assert.Equal("", uten.Nr);
        Assert.NotNull(Hjemmelstekstlesing.FullOpphevingssetning("Lov 15. juli 1925 om noe oppheves.", uten));
        Assert.Null(Hjemmelstekstlesing.FullOpphevingssetning("Lov 15. juli 1925 nr. 3 om noe oppheves.", uten));
    }

    [Fact]
    public void Endringslov_folges_bare_nar_tittelen_navngir_en_lov()
    {
        var m = new Dokumentmal("lov", 2002, 12, 13, "77");
        Assert.Equal("https://lovdata.no/eli/lov/1988/06/10/40/nor", Hjemmelstekstlesing.EndringslovTil(
            "med hjemmel i lov av 10. juni 1988 nr. 40 § 2-25, jf. lov av 13. desember 2002 nr. 77 om endringer i lov 10. juni 1988 nr. 40 om finansieringsvirksomhet.", m));
        Assert.Null(Hjemmelstekstlesing.EndringslovTil("jf. lov 13. desember 2002 nr. 77 om endringer i lov 10. juni 1988 nr. 40 og enkelte andre lover.", m));
        Assert.True(Hjemmelstekstlesing.ErEndringslov("jf. lov 13. desember 2002 nr. 77 om endringer i lov 10. juni 1988 nr. 40 og enkelte andre lover.", m));
    }
}
