using Oppsett = RegelIde.Data.Tests.UtgattAktorFunnregelTests.Oppsett;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, #472 PR 3, Johanns avgjørelse 2026-11-18 (4)] Maskinregelen for utgåtte aktører (#429) bruker den delte tekstlesingen fra hjemmelsstatusen
/// (#472 PR 1, <see cref="Hjemmelstekstlesing"/>). Formulert som spørsmålene modellen skal kunne besvare (CLAUDE.md §0):
/// <list type="bullet">
/// <item>Er kilden opphevet i sin helhet (R8) når opphevingen står i passiv, med «til» etter nummeret, eller med punktum i tittelen?</item>
/// <item>Holder vakten et mål som bare står i tittelen til noe annet, utenfor R8, også med utstederen mellom dokumenttypen og datoen?</item>
/// <item>Godtar vakten en liste av hele dokumenter og en beskrivelse med «fastsatt ved»/«ved vedtak», men ikke «følgende bestemmelser:»
/// eller «Andre ledd … i delegeringsbeslutning av …»?</item>
/// <item>Gir R9 og hjemmelsstatusen samme svar på samme tekst: formelen i foreldrenoden, V3 i bokmål og en lov uten nummer?</item>
/// </list>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class UtgattAktorFunnregelTekstlesingTests(EmbeddedPostgresFixture fixture)
{
    private const string Mal = "{MAL}";

    private static void Lti(RettskildeEntitet r) => r.DokumentId = "LTI/" + r.DokumentId!["SF/".Length..];

    private static string Ref(RettskildeEntitet rk) => $"1. januar 2099 nr. {rk.Eli!.Split('/')[^2]}";

    /// <summary>Kjører R8 for én LTI-kilde med en utgått aktør, der en annen kilde har kilden i «Endrer» og <paramref name="tekst"/> som node.</summary>
    private async Task<UtgattAktorRad> R8Async(string tekst)
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        var (a, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Tl"), nedlagt: true);
        var (kilde, _) = await o.ForskriftAsync(Lti, $"{a.Navn} kan videredelegere myndigheten.");
        var (opphevende, _) = await o.ForskriftAsync(tekst.Replace(Mal, Ref(kilde)));
        db.RettskildeEndringer.Add(new RettskildeEndringEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = opphevende.Id, EndringEid = kilde.Eli!, EndringRettskildeId = kilde.Id,
        });
        await db.SaveChangesAsync();
        var r = await new UtgattAktorFunnregel(db, new FunnTjeneste(db)).KjorAsync(skriv: false, "Test", [kilde.Id], medRader: true);
        return Assert.Single(r.Rader!);
    }

    [Theory]
    // Passiv, verbet etter målet (fase 1, E1u).
    [InlineData("Vedtak " + Mal + " om delegering av myndighet blir opphevet.")]
    // «til» etter nummeret (fase 1).
    [InlineData("Fra samme tid oppheves forskrift " + Mal + " til gjennomføring av EØS-avtalen vedlegg II.")]
    // Hele noden når tittelen har punktum (LTI/forskrift/2004-04-16-631 kap. III, FOR-1993-11-12-1022).
    [InlineData("Vedtak " + Mal + " om ikrafttredelse av lov 11. juni 1993 nr. 65 om konkurranse i ervervsvirksomhet (konkurranseloven). Delegering av myndighet, oppheves.")]
    // Vakten med utstederen mellom dokumenttypen og datoen (DEL/forskrift/2003-12-19-1790 kap. VI, AK 7 i #472).
    [InlineData("Fra 1. januar 2004 oppheves følgende delegeringer: Kongelig resolusjon fra Forbruker- og administrasjonsdepartementet av " + Mal + " om myndighetsoverføring.")]
    // [Ny vaktform] Nummerert liste av hele vedtak (FOR-2018-06-04-810 kap. 4).
    [InlineData("Fra samme tid oppheves følgende vedtak: 4.1. | Vedtak 18. juli 2013 nr. 921 om delegering av myndighet etter forvaltningsloven § 13d første ledd. 4.2. | Vedtak " + Mal + " om delegering av myndighet til Arbeids- og velferdsdirektoratet.")]
    // [Ny vaktform] Liste der det første dokumentet mangler nummer (FOR-2015-06-19-703).
    [InlineData("Instruksen trer i kraft straks og avløser kgl.res. 12. desember 1997 – Retningslinjer for regionalt samordningsansvar ved kriser og katastrofer i fred, og kgl.res. " + Mal + " – Instruks for samfunnssikkerhets- og beredskapsarbeidet.")]
    // [Ny vaktform] Beskrivelse og identifikasjon (FOR-2008-04-11-337 kap. VI, FOR-2022-12-22-2499).
    [InlineData("Følgende delegeringsbeslutninger oppheves:Delegering av myndighet til Fiskeridepartementet etter lov om havner og farvann m.v. § 6 første ledd, § 14 annet ledd og § 22, fastsatt ved kgl.res. " + Mal + ".")]
    [InlineData("Fra samme tidspunkt oppheves departementets delegering av myndighet til Sentralenheten ved vedtak " + Mal + ".")]
    public async Task R8_leser_de_utvidede_formene_og_vakten_godtar_malet_som_objekt(string tekst)
    {
        var rad = await R8Async(tekst);
        Assert.Equal(UtgattAktorFunnregel.UtfallKildefeil, rad.Utfall);
        Assert.StartsWith("R8: rettskilden er opphevet av", rad.Bevis);
    }

    [Theory]
    // Vakten (fase 1): målet står bare i tittelen til et annet dokument som oppheves. Før #472 PR 3 ble dette lest som en hel oppheving i R8.
    [InlineData("Fra samme tid oppheves forskrift 2. januar 2099 nr. 5 om ikrafttredelse av vedtak " + Mal + ".")]
    [InlineData("Kongelig resolusjon 23. mars 2099 nr. 689 om overføring av myndighet etter forskrift (midlertidig) " + Mal + " for undersøkelse etter naturforekomster oppheves.")]
    // Delmarkørene: «følgende bestemmelser:» (FOR-2010-06-16-820) og en del i dokumentet (FOR-2015-12-11-1667 kap. II).
    [InlineData("Fra samme tidspunkt oppheves følgende bestemmelser: Forskrift " + Mal + " om samtrafikkevnen § 3 syvende og åttende ledd.")]
    [InlineData("Andre ledd siste strekpunkt i delegeringsbeslutning av " + Mal + " om delegering av myndighet, oppheves.")]
    // Et paragrafspenn etter målet (FOR-1978-12-22-4), som vakten i PR 1 avviste.
    [InlineData("Fra samme tidspunkt oppheves forskrifter " + Mal + " om behandling av forbrukertvister mv. § 1 til § 9.")]
    public async Task R8_slaar_ikke_til_naar_vakten_avviser_eller_bare_en_del_oppheves(string tekst)
    {
        var rad = await R8Async(tekst);
        Assert.Equal(UtgattAktorFunnregel.UtfallForeslatt, rad.Utfall);
    }

    [Theory]
    [InlineData("Fra samme tid oppheves følgende vedtak: 4.1. | Vedtak 18. juli 2013 nr. 921 om delegering etter forvaltningsloven § 13d. 4.2. | Vedtak 31. mai 2012 nr. 474 om delegering.", 2012, 5, 31, "474", true)]
    [InlineData("Vedtaket avløser delegering av myndighet etter ligningsloven § 4-3 nr. 3 og § 4-4 nr. 7 vedtatt 15. oktober 1990 og 24. oktober 1991 nr. 742, samt delegeringsvedtak 5. mars 1984 nr. 3450.", 1991, 10, 24, "742", true)]
    [InlineData("Samtidig oppheves forskrift for sortering, pakking og merking av planteskolevarer, av 25. mars 1988 nr. 647.", 1988, 3, 25, "647", true)]
    [InlineData("Fra samme dato oppheves følgende forskrift: Forskrift 22. april 1994 nr. 318 om tilskudd til skogbruksplanlegging.", 1994, 4, 22, "318", true)]
    // «… av» godtas ikke når beskrivelsen nevner et annet dokument.
    [InlineData("Samtidig oppheves forskrift om endringer i forskrift av 25. mars 1988 nr. 647.", 1988, 3, 25, "647", false)]
    // «gitt i medhold av» er hjemmelen, ikke identifikasjonen.
    [InlineData("Fra samme tid oppheves forskrift om tilskudd gitt i medhold av lov 25. mars 1988 nr. 647.", 1988, 3, 25, "647", false)]
    public void Vakten_godtar_lister_og_identifikasjon_men_ikke_et_annet_dokument(string s, int aar, int mnd, int dag, string nr, bool objekt) =>
        Assert.Equal(objekt, Hjemmelstekstlesing.MalErObjekt(s, new Dokumentmal("forskrift", aar, mnd, dag, nr)));

    [Fact]
    public async Task R9_og_hjemmelsstatusen_gir_samme_svar_paa_samme_tekst()
    {
        await using var db = fixture.NyDbContext();
        var k = new HjemmelsstatusTests.Korpus(db);
        var o = new Oppsett(db);
        var (a, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Samsvar"), nedlagt: true);
        DateOnly dGammel = new(1997, 3, 4);
        var nGammel = HjemmelsstatusTests.Nr();
        var gammel = await k.StubAsync("lov", dGammel, nGammel);
        // Formelen i foreldrenoden (deltakerloven 1999 § 32 nr. 2, M16) og V3 i bokmål (uhl. 2024 § 17-3).
        // Datoen er passert: en oppheving som ikke er i kraft, gir ukjent i hjemmelsstatusen.
        var ny = await k.DokumentAsync("lov", new(2019, 6, 7), HjemmelsstatusTests.Nr(), "NL", "Lov om testing",
        [
            ("§10", null, "Ikrafttredelse og oppheving"),
            ("§10/ledd-1", "Fra samme tid oppheves følgende lover:", null),
            ("§10/ledd-1/punkt-1", $"Lov {HjemmelsstatusTests.Ref(dGammel, nGammel)} om testing.", null),
            ("§11", null, "Overgangsregler"),
            ("§11/ledd-1", "Forskrifter og enkeltvedtak som er fastsatt med hjemmel i regler som blir opphevet eller endret med denne loven, gjelder til "
                + "departementet fastsetter noe annet.", null),
        ]);
        await k.EndrerAsync(ny, gammel);
        var (f, _) = await o.ForskriftAsync(Lti, $"Myndighet etter lov {HjemmelsstatusTests.Ref(dGammel, nGammel)} om testing delegeres til {a.Navn}.");
        await k.HjemlerAsync(f, (gammel, "§3"));

        var hs = await new HjemmelsstatusRegel(db).KjorAsync(skriv: false, "Test", lesLovtidend: false, medRader: true, forskriftIder: [f.Id]);
        var hsRad = Assert.Single(hs.Rader!, r => r.HjemmelEid.StartsWith(gammel.Eli!, StringComparison.Ordinal));
        Assert.Equal((Hjemmelsstatus.Viderefort, Hjemmelsstatus.E1, "V3"), (hsRad.Status, hsRad.Bevistrinn, hsRad.VidereforingForm));

        var r9 = await new UtgattAktorFunnregel(db, new FunnTjeneste(db)).KjorAsync(skriv: false, "Test", [f.Id], medRader: true);
        var mangel = Assert.Single(Assert.Single(r9.Rader!).Premissmangler!);
        // Samme opphevingssetning og samme videreføring som hjemmelsstatusen.
        Assert.Contains($"«{hsRad.OpphevetAvSitat}»", mangel);
        Assert.Contains("Fra samme tid oppheves følgende lover: Lov", mangel);
        Assert.Contains("§ 10 første ledd punkt 1 opphever den", mangel);
        Assert.Contains("§ 11 første ledd kan videreføre", mangel);
        Assert.Contains("regler som blir opphevet", mangel);
    }

    [Fact]
    public async Task R9_leser_en_hjemmelslov_uten_nummer()
    {
        // Skifteloven 21. februar 1930 har ikke nummer; arveloven 2019 § 179 andre ledd opphever den. Før #472 PR 3 ble den ikke lest (FOR-1990-11-02-883).
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        var (a, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Skifte"), nedlagt: true);
        var aar = Random.Shared.Next(1700, 1799);
        var eliGammel = $"https://lovdata.no/eli/lov/{aar}/02/21/nor";
        var gammel = new RettskildeEntitet
        {
            Id = Guid.NewGuid(), Doctype = "act", Kildetype = "Lov", Status = "Utkast", Importrolle = "referanse", Tittel = eliGammel, Eli = eliGammel,
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Rettskilder.Add(gammel);
        await db.SaveChangesAsync();
        var k = new HjemmelsstatusTests.Korpus(db);
        var ny = await k.DokumentAsync("lov", new(2099, 6, 14), HjemmelsstatusTests.Nr(), "NL", "Lov om arv og dødsboskifte",
            [("§179", null, null), ("§179/ledd-2", $"Fra den tid loven trer i kraft, oppheves lov 21. februar {aar} om skifte og lov 3. mars 1972 nr. 5 om arv m.m.", null)]);
        await k.EndrerAsync(ny, gammel);
        var (f, _) = await o.ForskriftAsync(Lti, $"Myndighet etter skifteloven delegeres til {a.Navn}.");
        await k.HjemlerAsync(f, (gammel, null));

        var r = await new UtgattAktorFunnregel(db, new FunnTjeneste(db)).KjorAsync(skriv: false, "Test", [f.Id], medRader: true);
        var mangel = Assert.Single(Assert.Single(r.Rader!).Premissmangler!);
        Assert.Contains($"bygger på lov 21. februar {aar} (Lovdatas hjemmelsfelt)", mangel);
        Assert.Contains($"oppheves lov 21. februar {aar} om skifte", mangel);
    }

    [Fact]
    public async Task Malvedtaket_leses_med_samme_vakt_og_samme_passiv_som_hjemmelsstatusen()
    {
        await using var db = fixture.NyDbContext();
        var o = new Oppsett(db);
        var (a, _) = await o.AktorAsync(UtgattAktorFunnregelTests.Departement("Malv"), nedlagt: true);
        async Task<string> BeskrivelseAsync(string tekst)
        {
            var (mal, _) = await o.ForskriftAsync("Målvedtaket.");
            var (kilde, _) = await o.ForskriftAsync(tekst.Replace(Mal, Ref(mal)), $"Klage går til {a.Navn}.");
            db.RettskildeEndringer.Add(new RettskildeEndringEntitet { Id = Guid.NewGuid(), RettskildeId = kilde.Id, EndringEid = mal.Eli!, EndringRettskildeId = mal.Id });
            await db.SaveChangesAsync();
            var regel = new UtgattAktorFunnregel(db, new FunnTjeneste(db));
            await regel.KjorAsync(skriv: true, "Test", [kilde.Id]);
            return (await new FunnTjeneste(db).ListeAsync(rettskildeId: kilde.Id)).Single().Beskrivelse!;
        }
        // Nynorsk passiv (FOR-2017-03-10-313 punkt 11): opphevingen leses nå.
        Assert.Contains("- Rettskilden opphever", await BeskrivelseAsync($"Vedtak {Mal} om delegering av mynde blir oppheva."));
        // En del av målvedtaket (FOR-2015-12-11-1667 kap. II): ikke lenger «opphever».
        var del = await BeskrivelseAsync($"Andre ledd siste strekpunkt i delegeringsbeslutning av {Mal} om delegering av myndighet, oppheves.");
        Assert.Contains("- Rettskilden endrer eller opphever", del);
        Assert.DoesNotContain("- Rettskilden opphever", del);
    }
}
