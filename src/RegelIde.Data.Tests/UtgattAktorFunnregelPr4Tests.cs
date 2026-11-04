using Microsoft.EntityFrameworkCore;
using Oppsett = RegelIde.Data.Tests.UtgattAktorFunnregelTests.Oppsett;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, #429 PR 4, Johanns avgjørelser 2026-11-04 etter måling 3] Små rettinger og to modellvalg i maskinregelen for utgåtte aktører.
/// Formulert som spørsmålene modellen skal kunne besvare (CLAUDE.md §0):
/// <list type="bullet">
/// <item>(1a) Er en oppheving «for så vidt gjelder …» eller «… nr. 1017 pkt. II» delvis, mens de hele opphevingene juristen kontrollerte
/// fortsatt er hele?</item>
/// <item>(1b) Viser R9 videreføringen «med hjemmel i … skal fortsatt gjelde» (ekomloven 2024 § 18-2)?</item>
/// <item>(1c) Leser R3 og R9 opphevingsformelen i noden over («oppheves følgende delegeringer:»)?</item>
/// <item>(2) Får et funn der navnet bare står i avgiverformelen, egen undertype og plass sist i lista, uten å holdes tilbake?</item>
/// <item>(3) Hvem har oppgaven nå når etterfølgerkjeden forgrener seg, og hvor mange kanter krever premissporten?</item>
/// <item>(4) Kan et menneske gjenåpne et funn som er forklart bort, og avvise en kildefeil, uten at maskinregelen gjør det om igjen?</item>
/// </list>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class UtgattAktorFunnregelPr4Tests(EmbeddedPostgresFixture fixture)
{
    private const string Fd = "Fiskeridepartementet";

    // ---- (1a) Delvis som hviteliste ------------------------------------------------------------------------------------------

    [Theory]
    // F06 i måling 3 (FOR-2022-10-21-1777 punkt 2 om FOR-2020-01-10-33): «for så vidt gjelder» etter verbet.
    [InlineData("Vedtak om delegering av myndighet etter postloven, fastsatt ved kongelig resolusjon 10. januar 2020 nr. 33, oppheves for så vidt gjelder vedtaket del I nr. 1 annet ledd annet punktum og nr. 4 annet ledd.", 2020, 1, 10, "33")]
    // 43d70df4 (FOR-2005-04-29-378 kap. II om FOR-1990-12-21-1017): «pkt. II» rett etter nummeret.
    [InlineData("Samtidig oppheves tidligere delegeringsvedtak som delegerer Kongens myndighet til å gi forskrift etter utlendingsloven, fastsatt i Kronprinsreg.res. 21. desember 1990 nr. 1017 pkt. II, kgl.res. 3. juli 1992 nr. 496, kgl.res. 10. januar 1997 nr. 11.", 1990, 12, 21, "1017")]
    // «for så vidt» foran målet, og former som ikke står på hvitelisten etter verbet.
    [InlineData("For så vidt gjelder klagesaker oppheves vedtak 10. januar 2020 nr. 33.", 2020, 1, 10, "33")]
    [InlineData("Vedtak 10. januar 2020 nr. 33 om delegering oppheves med unntak av punkt 3.", 2020, 1, 10, "33")]
    [InlineData("Vedtak 10. januar 2020 nr. 33 om delegering oppheves så langt det gjelder havner.", 2020, 1, 10, "33")]
    [InlineData("Kgl.res. 10. januar 2020 nr. 33 nr. 4 oppheves.", 2020, 1, 10, "33")]
    public void Delvise_opphevinger_er_ikke_hele(string tekst, int aar, int mnd, int dag, string nr) =>
        Assert.Null(Opphevingskontroll.FullOpphevingssetning(tekst, new DateOnly(aar, mnd, dag), nr));

    [Theory]
    // Hele opphevinger fra juristens kontroll av de 47 kildefeilene (kildefeil_opphevende.tsv), som skal forbli hele.
    [InlineData("Kongelig resolusjon 15. august 2014 nr. 1083 om delegering av myndighet etter lov 15. august 2014 nr. 61 om losordningen § 2 tredje ledd og § 6 sjette ledd oppheves med virkning fra 1. januar 2020.", 2014, 8, 15, "1083")]
    [InlineData("Fiskeri- og kystdepartementets vedtak av 22. januar 2003 nr. 58 om delegering av kompetanse til å fatte vedtak om ervervstillatelse etter deltakerloven oppheves ved ikraftsetting av denne delegeringen.", 2003, 1, 22, "58")]
    [InlineData("Instruks 1. mars 2005 nr. 1832 for føring av fangstdagbok oppheves.", 2005, 3, 1, "1832")]
    [InlineData("Kongelig resolusjon av 21. februar 2003 nr. 200 hvor Kongens myndighet etter lov av 20. desember 1974 nr. 73 § 26, annet ledd delegeres til Landbruksdepartementet, oppheves.", 2003, 2, 21, "200")]
    [InlineData("Følgende delegeringsbeslutninger oppheves:Delegering av myndighet til Fiskeridepartementet etter lov om havner og farvann m.v. § 6 første ledd, § 14 annet ledd og § 22, fastsatt ved kgl.res. 17. januar 2003 nr. 38.Delegering av myndighet til Fiskeridepartementet etter lov om havner og farvann m.v., fastsatt ved kgl.res. 11. desember 1998 nr. 1561.", 2003, 1, 17, "38")]
    [InlineData("Instruksen gjelder fra og med 1. januar 2018 og erstatter fra samme tidspunkt administrasjonsinstruks fastsatt den 22. november 2012 nr. 1102 for pensjonsordningen for apotekvirksomhet.", 2012, 11, 22, "1102")]
    [InlineData("Vedtak 28. oktober 2005 nr. 1241 om delegering av myndighet til Justis- og politidepartementet etter lov om tinglysing § 4a, oppheves.", 2005, 10, 28, "1241")]
    [InlineData("Instruksen trer i kraft straks og avløser kgl.res. 12. desember 1997 – Retningslinjer for regionalt samordningsansvar ved kriser og katastrofer i fred, og kgl.res. 18. april 2008 nr. 388 – Instruks for samfunnssikkerhets- og beredskapsarbeidet til Fylkesmannen og Sysselmannen på Svalbard.", 2008, 4, 18, "388")]
    // En liste: begge vedtakene oppheves helt.
    [InlineData("Fra samme tidspunkt oppheves kongelig resolusjon 27. oktober 2017 nr. 1666 og kongelig resolusjon 18. desember 1992 nr. 1079.", 1992, 12, 18, "1079")]
    [InlineData("Fra samme tidspunkt oppheves kongelig resolusjon 27. oktober 2017 nr. 1666 og kongelig resolusjon 18. desember 1992 nr. 1079.", 2017, 10, 27, "1666")]
    public void Hele_opphevinger_er_fortsatt_hele(string tekst, int aar, int mnd, int dag, string nr) =>
        Assert.NotNull(Opphevingskontroll.FullOpphevingssetning(tekst, new DateOnly(aar, mnd, dag), nr));

    // ---- (1b) Videreføringen ---------------------------------------------------------------------------------------------------

    [Theory]
    // M05: ekomloven 2024 § 18-2 første ledd.
    [InlineData("Enkeltvedtak og forskrift med hjemmel i lov 4. juli 2003 nr. 83 om elektronisk kommunikasjon skal fortsatt gjelde.", true)]
    [InlineData("Forskrifter gitt i medhold av lov 4. juli 2003 nr. 83 gjelder fortsatt.", true)]
    [InlineData("Forskrifter gitt i medhold av lov 4. juli 2003 nr. 83 fortsatt gjelde inntil annet er bestemt.", true)]
    // Uten «i medhold av»/«med hjemmel i», eller uten at noe skal gjelde: ingen videreføring.
    [InlineData("Saker etter lov 4. juli 2003 nr. 83 skal fortsatt behandles etter den loven.", false)]
    [InlineData("Forskrift med hjemmel i lov 4. juli 2003 nr. 83 oppheves.", false)]
    public void Videreforingen_kjennes_igjen(string tekst, bool finnes) =>
        Assert.Equal(finnes, Opphevingskontroll.Videreforingssetning(tekst, new DateOnly(2003, 7, 4), "83") is not null);

    // ---- (1c) Opphevingsformelen i noden over ------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Fra 1. januar 2004 oppheves følgende delegeringer:", true)] // M20
    [InlineData("Fra samme tid oppheves følgende lover:", true)] // M16
    [InlineData("Følgende delegeringsbeslutninger oppheves:", true)]
    [InlineData("oppheves:", true)]
    [InlineData("Følgende delegeringer gjelder:", false)]
    [InlineData("Departementet kan bestemme at forskriften oppheves følgende år.", false)]
    [InlineData("Fra samme tid oppheves følgende lover: lov 1. januar 2000 nr. 1.", false)]
    public void Opphevingsformelen_kjennes_igjen(string forelder, bool formel) =>
        Assert.Equal(formel, UtgattAktorRegler.Opphevingsformel(forelder));

    [Fact]
    public void R3_leser_opphevingsformelen_i_noden_over()
    {
        const string punkt = "Kongelig resolusjon fra Fiskeridepartementet av 29. januar 1988 om opprettelsen av Statens næringsmiddeltilsyn.";
        Assert.Contains("R3", UtgattAktorRegler.Treff(Fd, punkt, "Fra 1. januar 2004 oppheves følgende delegeringer:"));
        Assert.DoesNotContain("R3", UtgattAktorRegler.Treff(Fd, punkt, "Følgende delegeringer gjelder:"));
        Assert.DoesNotContain("R3", UtgattAktorRegler.Treff(Fd, punkt, null));
    }

    // ---- (2) Avgiverformelen ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("Fiskeridepartementets myndighet etter forvaltningsloven § 9 annet ledd delegeres fra dags dato til Fiskeridirektoratet.", true)] // M10
    [InlineData("Fiskeridepartementet delegerer med dette følgende myndighet til Fiskeridirektoratet:", true)] // M12
    [InlineData("Fiskeridepartementet har delegert myndigheten etter § 5 til Fiskeridirektoratet.", true)]
    [InlineData("Fiskeridepartementet fatter i medhold av lov om fiske følgende vedtak:", true)] // M11
    // Mottakeren, ikke avgiveren.
    [InlineData("Myndigheten etter § 5 delegeres til Fiskeridepartementet.", false)]
    [InlineData("Fiskeridepartementet kan delegere myndigheten til Fiskeridirektoratet.", false)]
    // Termen står to ganger: mer enn avgiveren.
    [InlineData("Fiskeridepartementet delegerer med dette myndigheten. Klage går til Fiskeridepartementet.", false)]
    public void Avgiverformelen_kjennes_igjen(string tekst, bool avgiver) =>
        Assert.Equal(avgiver, UtgattAktorRegler.Avgiverformel(Fd, tekst));

    [Fact]
    public async Task Avgiverformelen_gir_egen_undertype_sist_i_lista_og_holdes_ikke_tilbake()
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        var (a, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Avgiver"), nedlagt: true);
        // Bare avgiverformelen, og en historisk opplysning som R4 teller.
        var (bare, _) = await o.ForskriftAsync($"{a.Navn} delegerer med dette følgende myndighet til direktoratet.", $"Saken lå tidligere {a.Navn} til last.");
        // Avgiverformelen og en operativ forekomst: et vanlig funn.
        var (begge, _) = await o.ForskriftAsync($"{a.Navn}s myndighet etter § 3 delegeres til direktoratet.", $"Klage går til {a.Navn}.");
        var regel = new UtgattAktorFunnregel(db, new FunnTjeneste(db));

        var torr = await regel.KjorAsync(skriv: false, "Test", [bare.Id, begge.Id]);
        Assert.Equal(2, torr.Foreslatt);
        Assert.Equal(1, torr.Avgiverformel);
        Assert.Equal(2, torr.AvgiverformelMinstEnForekomst);

        await regel.KjorAsync(skriv: true, "Test", [bare.Id, begge.Id]);
        var t = new FunnTjeneste(db);
        var fBare = Assert.Single(await t.ListeAsync(rettskildeId: bare.Id));
        Assert.Equal(UtgattAktorFunnregel.UndertypeAvgiverformel, fBare.Undertype);
        Assert.Equal(Funn.Oppdaget, fBare.Status);
        Assert.Contains("Navnet står bare i avgiverformelen (§ 1 første ledd)", fBare.Beskrivelse);
        var fBegge = Assert.Single(await t.ListeAsync(rettskildeId: begge.Id));
        Assert.Equal("nedlagt_aktor", fBegge.Undertype);
        Assert.DoesNotContain("avgiverformelen", fBegge.Beskrivelse);

        // Sist i lista, selv om det er opprettet sist (lista er ellers nyeste først).
        var liste = await t.ListeAsync(virksomhetId: a.Id);
        Assert.Equal(fBare.Id, liste[^1].Id);

        // Et funn lastet før PR 4 (undertypen fra gruppen) får undertypen ved oppfrisking.
        fBare.Undertype = "nedlagt_aktor";
        fBare.Beskrivelse = "Påstanden fra PR 3.";
        await db.SaveChangesAsync();
        var r = await regel.KjorAsync(skriv: true, "Test", [bare.Id, begge.Id], oppfrisk: true);
        Assert.Equal(1, r.Oppfrisket);
        Assert.Equal(UtgattAktorFunnregel.UndertypeAvgiverformel, (await db.Funn.AsNoTracking().SingleAsync(f => f.Id == fBare.Id)).Undertype);
    }

    // ---- (3) Kjedeviften --------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Kjeden_folges_bare_uten_forgrening_og_premissporten_krever_bare_den_direkte_veien()
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        // M17-mønsteret: S → H (nedlagt) → Hod (levende), og S → So (nedlagt) → fire levende. Hele kjeden har fem levende aktører.
        var (s, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Sosialhelse"), nedlagt: true);
        var (h, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Helse"), nedlagt: true);
        var (so, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Sosial"), nedlagt: true);
        var (hod, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Helseomsorg"), nedlagt: false);
        var direkte1 = await o.EtterfolgerAsync(h.Id, s.Id, "test");
        var direkte2 = await o.EtterfolgerAsync(so.Id, s.Id, "test");
        var ugrenet = await o.EtterfolgerAsync(hod.Id, h.Id, "test");
        var gren = new List<Guid>();
        foreach (var n in new[] { "Arbeid", "Barn", "Kirke", "Kultur" })
        {
            var (l, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement(n), nedlagt: false);
            gren.Add((await o.EtterfolgerAsync(l.Id, so.Id, "test")).Id);
        }
        // ≤ 3: X → Y (nedlagt) → to levende. Navnene står, men grenen følges ikke.
        var (x, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Iks"), nedlagt: true);
        var (y, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Yps"), nedlagt: true);
        var (b1, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Beone"), nedlagt: false);
        var (b2, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Betwo"), nedlagt: false);
        var xy = await o.EtterfolgerAsync(y.Id, x.Id, "test");
        await o.EtterfolgerAsync(b1.Id, y.Id, "test");
        await o.EtterfolgerAsync(b2.Id, y.Id, "test");

        var (rk, _) = await o.ForskriftAsync($"Klage går til {s.Navn}.", $"{x.Navn} fører tilsyn.");
        var regel = new UtgattAktorFunnregel(db, new FunnTjeneste(db));
        var r = await regel.KjorAsync(skriv: true, "Test", [rk.Id]);
        Assert.Equal(2, r.KjedeForgrenet);
        Assert.Equal(4, r.EtterfolgerkanterKoblet); // 3 for S, 1 for X — ikke 7 og 3
        var t = new FunnTjeneste(db);
        var funn = await t.ListeAsync(rettskildeId: rk.Id);
        FunnEntitet For(Guid vid) => funn.Single(f => f.Koblinger.Any(k => k.Rolle == Funn.BerortAktor && k.VirksomhetId == vid));

        var fS = For(s.Id);
        Assert.Equal(new[] { direkte1.Id, direkte2.Id, ugrenet.Id }.Order(),
            fS.Koblinger.Where(k => k.Rolle == Funn.Etterfolger).Select(k => k.StrukturkantId!.Value).Order());
        Assert.DoesNotContain(fS.Koblinger, k => k.StrukturkantId is { } id && gren.Contains(id));
        Assert.Contains("Kjeden av etterfølgere i modellen forgrener seg til 5 levende aktører.", fS.Beskrivelse);
        Assert.Contains($"Den er ikke fulgt videre fra {so.Navn}, og bare kantene på den direkte veien er koblet på.", fS.Beskrivelse);
        Assert.DoesNotContain(hod.Navn, fS.Beskrivelse); // over tre: bare antallet
        Assert.Equal(3, (await t.PremissmanglerAsync(fS)).Count(m => m.StartsWith("Etterfølger ikke vurdert")));

        var fX = For(x.Id);
        Assert.Equal([xy.Id], fX.Koblinger.Where(k => k.Rolle == Funn.Etterfolger).Select(k => k.StrukturkantId!.Value));
        Assert.Contains($"Etter {y.Navn} forgrener kjeden seg til ", fX.Beskrivelse);
        Assert.Contains(b1.Navn, fX.Beskrivelse);
        Assert.Contains(b2.Navn, fX.Beskrivelse);
        Assert.Contains("Den grenen er ikke fulgt, og kantene i den er ikke koblet på.", fX.Beskrivelse);
        Assert.Single(await t.PremissmanglerAsync(fX), m => m.StartsWith("Etterfølger ikke vurdert"));
    }

    // ---- (1c) R9 med formelen i noden over, og (1b) videreføringen, mot databasen ---------------------------------------------------

    [Fact]
    public async Task R9_leser_opphevingsformelen_i_noden_over_og_viser_viderefaringen()
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        var (a, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Fiskhjemmel"), nedlagt: true);
        var nr = Random.Shared.Next(100, 999);
        RettskildeEntitet Lov(string eli, string rolle, string tittel, string? dok = null) => new()
        {
            Id = Guid.NewGuid(), Doctype = "act", Kildetype = "Lov", Status = "Gjeldende", Importrolle = rolle, Tittel = tittel, Eli = eli,
            DokumentId = dok, AknXml = rolle == "primaer" ? "<akomaNtoso/>" : null, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        var gammel = Lov($"https://lovdata.no/eli/lov/2097/06/16/{nr}/nor", "referanse", "Gammel lov");
        var ny = Lov($"https://lovdata.no/eli/lov/2099/03/26/{nr}/nor", "primaer", "Ny lov", $"NL/lov/2099-03-26-{nr}");
        // Negativ: samme oppsett, men forelderen er ingen opphevingsformel.
        var gammel2 = Lov($"https://lovdata.no/eli/lov/2097/06/17/{nr}/nor", "referanse", "Gammel lov 2");
        var ny2 = Lov($"https://lovdata.no/eli/lov/2099/03/27/{nr}/nor", "primaer", "Ny lov 2", $"NL/lov/2099-03-27-{nr}");
        db.Rettskilder.AddRange(gammel, ny, gammel2, ny2);
        RettskildeNodeEntitet Node(RettskildeEntitet rk, string kilde, string tekst, Guid? forelder, int rekkefolge) => new()
        {
            Id = Guid.NewGuid(), RettskildeId = rk.Id, Eid = $"{rk.Eli}/{kilde}", KildeId = kilde, NodeType = "punkt", TekstHash = "h",
            Tekst = tekst, ParentNodeId = forelder, Sorteringsrekkefolge = rekkefolge,
        };
        var formel = Node(ny, "§32/punkt-2", "Fra samme tid oppheves følgende lover:", null, 1);
        var ikkeFormel = Node(ny2, "§32/punkt-2", "Følgende lover gjelder fortsatt:", null, 1);
        db.RettskildeNoder.AddRange(formel, ikkeFormel,
            Node(ny, "§32/punkt-2/punkt-4", $"Lov av 16. juni 2097 nr. {nr} om regulering av deltagelsen i fisket.", formel.Id, 2),
            Node(ny, "§32/punkt-3", $"Enkeltvedtak og forskrift med hjemmel i lov 16. juni 2097 nr. {nr} skal fortsatt gjelde.", null, 3),
            Node(ny2, "§32/punkt-2/punkt-4", $"Lov av 17. juni 2097 nr. {nr} om regulering av deltagelsen i fisket.", ikkeFormel.Id, 2));
        db.RettskildeEndringer.AddRange(
            new RettskildeEndringEntitet { Id = Guid.NewGuid(), RettskildeId = ny.Id, EndringEid = gammel.Eli!, EndringRettskildeId = gammel.Id },
            new RettskildeEndringEntitet { Id = Guid.NewGuid(), RettskildeId = ny2.Id, EndringEid = gammel2.Eli!, EndringRettskildeId = gammel2.Id });
        await db.SaveChangesAsync();

        var (rk, _) = await o.ForskriftAsync($"Myndighet etter § 6 i lov av 16. juni 2097 nr. {nr} om deltagelsen i fisket delegeres til {a.Navn}.");
        var (rk2, _) = await o.ForskriftAsync($"Myndighet etter § 6 i lov av 17. juni 2097 nr. {nr} om deltagelsen i fisket delegeres til {a.Navn}.");
        var r = await new UtgattAktorFunnregel(db, new FunnTjeneste(db)).KjorAsync(skriv: false, "Test", [rk.Id, rk2.Id], medRader: true);
        var mangel = Assert.Single(r.Rader!.Single(x => x.DokumentId == rk.DokumentId).Premissmangler!);
        Assert.StartsWith("Hjemmelsloven kan være opphevet", mangel);
        Assert.Contains($"LOV-2099-03-26-{nr}", mangel);
        Assert.Contains("Fra samme tid oppheves følgende lover: Lov av 16. juni 2097", mangel);
        Assert.Contains("kan videreføre vedtak og forskrifter", mangel);
        Assert.Contains("med hjemmel i", mangel);
        Assert.Empty(r.Rader!.Single(x => x.DokumentId == rk2.DokumentId).Premissmangler!);
    }

    // ---- (4) Gjenåpning og avvisning ----------------------------------------------------------------------------------------------

    private static void Lti(RettskildeEntitet r) => r.DokumentId = "LTI/" + r.DokumentId!["SF/".Length..];

    [Fact]
    public async Task Et_gjenapnet_funn_forklares_ikke_bort_igjen_av_maskinregelen()
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        var (a, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Gjenapne"), nedlagt: true);
        var (rk, ledd) = await o.ForskriftAsync($"{a.Navn} avgjør.");
        var regel = new UtgattAktorFunnregel(db, new FunnTjeneste(db));
        await regel.KjorAsync(skriv: true, "Test", [rk.Id]);
        // Etter lastingen blir noden en endringsingress (R5), og regelen forklarer funnet bort.
        ledd[0].Tekst = $"I vedtak 1. januar 2000 nr. 1 om {a.Navn} gjøres følgende endringer:";
        await db.SaveChangesAsync();
        Assert.Equal(1, (await regel.KjorAsync(skriv: true, "Test", [rk.Id])).ForklartBortSatt);
        var t = new FunnTjeneste(db);
        var id = (await t.ListeAsync(rettskildeId: rk.Id)).Single().Id;

        // Bare fra forklart bort, og bare med begrunnelse.
        await Assert.ThrowsAsync<ArgumentException>(() => t.GjenapneAsync(id, " ", "Jurist Test"));
        var f = (await t.GjenapneAsync(id, "Maskinregelen leste en delvis oppheving som hel (måling 3, F06).", "Jurist Test"))!;
        Assert.Equal(Funn.Oppdaget, f.Status);
        Assert.Null(f.VurdertAvType); // merket «Forklart bort av maskinregel» forsvinner
        Assert.Null(f.ForklartBortBegrunnelse);
        Assert.Equal("Jurist Test", f.GjenapnetAv);
        Assert.NotNull(f.GjenapnetTidspunkt);
        var p = await db.Proveniens.SingleAsync(x => x.EntitetId == id && x.Handling == "gjenapnet");
        Assert.Equal("Jurist Test", p.EndretAv);
        Assert.Contains("R5", p.KildeReferanserJson); // den tidligere begrunnelsen står i proveniensen
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.GjenapneAsync(id, "igjen", "Jurist Test"));

        // Regelen holder fortsatt paret tilbake, men forklarer ikke bort et funn et menneske har gjenåpnet.
        var torr = await regel.KjorAsync(skriv: false, "Test", [rk.Id]);
        Assert.Equal(0, torr.ForklaresBort);
        Assert.Equal(1, torr.HoldtTilbakeMenGjenapnet);
        Assert.Equal(1, torr.HoldtTilbakeMenAlleredeVurdert);
        var igjen = await regel.KjorAsync(skriv: true, "Test", [rk.Id], oppfrisk: true);
        Assert.Equal(0, igjen.ForklartBortSatt);
        Assert.Equal(Funn.Oppdaget, (await db.Funn.AsNoTracking().SingleAsync(x => x.Id == id)).Status);
        Assert.False(await t.ForklarBortMaskineltAsync(id, "x", UtgattAktorFunnregel.OppdagelsesKilde, "Test"));
    }

    [Fact]
    public async Task En_avvist_kildefeil_opprettes_ikke_paa_nytt_og_kilden_gir_funn()
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        var (a, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Avvist"), nedlagt: true);
        var (t, _) = await o.ForskriftAsync(Lti, $"{a.Navn} kan videredelegere myndigheten.");
        var nr = int.Parse(t.Eli!.Split('/')[^2]);
        var (s, _) = await o.ForskriftAsync($"Fra samme tid oppheves vedtak 1. januar 2099 nr. {nr} om delegering av myndighet.");
        db.RettskildeEndringer.Add(new RettskildeEndringEntitet { Id = Guid.NewGuid(), RettskildeId = s.Id, EndringEid = t.Eli!, EndringRettskildeId = t.Id });
        await db.SaveChangesAsync();
        var regel = new UtgattAktorFunnregel(db, new FunnTjeneste(db));
        Assert.Equal(1, (await regel.KjorAsync(skriv: true, "Test", [t.Id])).KildefeilNye);
        var kf = await db.Kildefeil.SingleAsync(k => k.RettskildeId == t.Id);

        var tjeneste = new KildefeilTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.AvvisAsync(kf.Id, "", "Jurist Test"));
        var avvist = (await tjeneste.AvvisAsync(kf.Id, "Delvis oppheving (måling 3).", "Jurist Test"))!;
        Assert.Equal(KildefeilTjeneste.Avvist, avvist.Status);
        Assert.Equal("Jurist Test", avvist.StatusEndretAv);
        Assert.NotNull(avvist.StatusEndretTidspunkt);
        Assert.True(await db.Proveniens.AnyAsync(p => p.EntitetId == kf.Id && p.EntitetType == KildefeilTjeneste.ProveniensType && p.Handling == "avvist"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tjeneste.AvvisAsync(kf.Id, "igjen", "Jurist Test"));

        // Ny kjøring: R8 slår fortsatt til på teksten, men mennesket har avvist kildefeilen. Ingen ny kildefeil, og kilden gir funn.
        var r = await regel.KjorAsync(skriv: true, "Test", [t.Id]);
        Assert.Equal(1, r.R8OverstyrtAvAvvistKildefeil);
        Assert.Equal(0, r.KildefeilRettskilder);
        Assert.Equal(0, r.KildefeilNye);
        Assert.Equal(1, r.Nye);
        var rad = await db.Kildefeil.AsNoTracking().SingleAsync(k => k.RettskildeId == t.Id);
        Assert.Equal(KildefeilTjeneste.Avvist, rad.Status);
        // Og den generiske skriveveien gir den avviste raden tilbake, uendret.
        var finn = await tjeneste.OpprettEllerFinnAsync(t.Id, null, UtgattAktorFunnregel.KildefeiltypeOpphevetAvAnnenKilde, "ny tekst",
            UtgattAktorFunnregel.OppdagelsesKilde, "Test");
        Assert.Equal(kf.Id, finn.Id);
        Assert.Equal(KildefeilTjeneste.Avvist, finn.Status);
    }
}
