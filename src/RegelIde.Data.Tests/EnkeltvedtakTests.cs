using static RegelIde.Data.Tests.HjemmelsstatusTests;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, #514, prosjekteierens avgjørelse 2026-11-27] Enkeltvedtak i SF og INS som overgangsregelen dekker (#472 måling 3b, N14 og N16). Spørsmålene
/// modellen skal kunne besvare (CLAUDE.md §0):
/// <list type="bullet">
/// <item>Er vedtaket et enkeltvedtak, lest av tittelen og innholdet (en tillatelse, godkjenning eller status til en navngitt part)?</item>
/// <item>Er et slikt vedtak dekket når overgangsregelen nevner enkeltvedtak, så forskriften er videreført og ikke «uavklart»?</item>
/// <item>Er en instruks eller et organiseringsvedtak for et statlig organ fortsatt ikke dekket?</item>
/// </list>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class EnkeltvedtakTests(EmbeddedPostgresFixture fixture)
{
    [Theory]
    // N16: SF/forskrift/2001-08-31-1074.
    [InlineData("Tillatelse for Posten Norge BA til å utgi og godta bruk av frimerker og andre frankeringsmidler der Norge eller Noreg forekommer.", null,
        "Med hjemmel i lov av 29. november 1996 nr. 73 om formidling av landsdekkende postsendinger (postloven) § 7 meddeles Posten Norge BA tillatelse til å utgi og godta bruk av frimerker og andre frankeringsmidler der Norge eller Noreg forekommer. Tillatelsen trer i kraft 1. oktober 2001 og gjelder inntil videre.",
        "Posten Norge BA")]
    // N14: INS/forskrift/2011-06-24-650.
    [InlineData("Lovisenberg diakonale høgskole status som høgskole", "Forskrift om høgskolestatus for LDH",
        "Med hjemmel i § 1-2 annet ledd i lov 1. april 2005 nr. 15 om universiteter og høyskoler får Lovisenberg diakonale høgskole med virkning fra 1. juli 2011 status som høgskole med fullmakter etter loven § 3-3 annet ledd.",
        "Lovisenberg diakonale høgskole")]
    // INS/forskrift/2007-03-16-304: «godkjennes som».
    [InlineData("Vedtak om godkjenning av Norsk Lærerakademi Bachelor- og masterstudier som høyskole.", null,
        "Med hjemmel i § 1-2 i lov 1. april 2005 nr. 15 om universiteter og høyskoler fastsettes det at Norsk Lærerakademi Bachelor- og masterstudier med virkning fra 1. april 2007 godkjennes som høyskole med de rettigheter det medfører.",
        "Norsk Lærerakademi Bachelor- og masterstudier")]
    public void Et_vedtak_til_en_navngitt_part_er_et_enkeltvedtak(string tittel, string? kortnavn, string tekst, string part)
    {
        var l = Enkeltvedtaklesing.Les(tittel, kortnavn, [tekst]);
        Assert.NotNull(l);
        Assert.Equal(part, l.Part);
        Assert.Contains(part, l.Sitat);
    }

    [Theory]
    // Status for et statlig organ er et organiseringsvedtak (#438), ikke et enkeltvedtak (INS/forskrift/2009-11-20-1428).
    [InlineData("Vedtak om status som vitenskapelig høgskole og nytt navn for Høgskolen i Molde",
        "Med hjemmel i § 1-2 annet ledd i lov 1. april 2005 nr. 15 om universiteter og høyskoler får Høgskolen i Molde med virkning fra 1. januar 2010 status som vitenskapelig høyskole med fullmakter etter loven § 3-3 annet ledd.")]
    // Parten står ikke i tittelen (SF/forskrift/1952-08-22-5).
    [InlineData("Forskrifter om vernepliktige sivilarbeidere.", "Arbeidet utføres ved Dillingøy leir i Moss. Sivilarbeidere gis tillatelse til å bo utenfor leiren.")]
    // Generelle regler, selv om de gir tillatelse (en forskrift med virkeområde).
    [InlineData("Forskrift om tillatelse for Posten Norge BA", "§ 1. Virkeområde. Forskriften gjelder for postsendinger. Posten Norge BA gis tillatelse til å utgi frimerker.")]
    // En instruks uten part og avgjørelse.
    [InlineData("Instruks om grensekontrollstasjoner mv.", "Grensekontrollstasjonene skal føre kontroll med alle forsendelser.")]
    public void Annet_enn_et_enkeltvedtak_til_en_privat_part_er_ikke_et_enkeltvedtak(string tittel, string tekst) =>
        Assert.Null(Enkeltvedtaklesing.Les(tittel, null, [tekst]));

    [Fact]
    public void Et_langt_vedtak_er_ikke_et_enkeltvedtak()
    {
        var noder = Enumerable.Range(1, Enkeltvedtaklesing.MaksNoder).Select(i => $"Punkt {i}.").Append("Posten Norge BA gis tillatelse til å utgi frimerker.").ToList();
        Assert.Null(Enkeltvedtaklesing.Les("Tillatelse for Posten Norge BA", null, noder));
    }

    /// <summary>Postloven 1996 er opphevet av postloven 2015 § 56, og § 57 fjerde ledd viderefører enkeltvedtak (N16).</summary>
    private static async Task<(RettskildeEntitet Lov1996, int Nr)> PostlovAsync(Korpus k)
    {
        DateOnly d1996 = new(1996, 11, 29);
        var n1996 = Nr();
        var l1996 = await k.StubAsync("lov", d1996, n1996);
        var l2015 = await k.DokumentAsync("lov", new(2015, 9, 4), Nr(), "NL", "Lov om posttjenester (postloven)",
        [
            ("§56", null, "Ikrafttredelse"),
            ("§56/ledd-2", $"Fra den tid loven trer i kraft oppheves lov {Ref(d1996, n1996)} om formidling av landsdekkende postsendinger.", null),
            ("§57", null, "Overgangsbestemmelser"),
            ("§57/ledd-4", $"Enkeltvedtak med hjemmel i lov {Ref(d1996, n1996)} om formidling av landsdekkende postsendinger som er i kraft ved denne lovs ikrafttredelse, skal fortsatt gjelde.", null),
        ]);
        await k.EndrerAsync(l2015, l1996);
        return (l1996, n1996);
    }

    [Fact]
    public async Task N16_Er_en_tillatelse_til_Posten_i_SF_dekket_av_en_overgangsregel_om_enkeltvedtak_Ja_og_videreført()
    {
        await using var db = fixture.NyDbContext();
        var k = new Korpus(db);
        var (l1996, n) = await PostlovAsync(k);
        var tillatelse = await k.DokumentAsync("forskrift", new(2001, 8, 31), Nr(), "SF",
            "Tillatelse for Posten Norge BA til å utgi og godta bruk av frimerker og andre frankeringsmidler der Norge eller Noreg forekommer.",
            [("ledd-1", $"Med hjemmel i lov av {Ref(new(1996, 11, 29), n)} om formidling av landsdekkende postsendinger (postloven) § 7 meddeles Posten Norge BA tillatelse til å utgi og godta bruk av frimerker. Tillatelsen trer i kraft 1. oktober 2001 og gjelder inntil videre.", null)],
            annetOm: $"Fastsatt ved kgl.res. 31. august 2001 med hjemmel i lov av {Ref(new(1996, 11, 29), n)} om formidling av landsdekkende postsendinger (postloven) § 7.");
        await k.HjemlerAsync(tillatelse, (l1996, "§7"));

        var rapport = await new HjemmelsstatusRegel(db).KjorAsync(skriv: true, "Test", lesLovtidend: false, medRader: true, forskriftIder: [tillatelse.Id]);
        Assert.Equal(1, rapport.Enkeltvedtak);
        Assert.Equal("Posten Norge BA", Assert.Single(rapport.EnkeltvedtakListe!).Part);
        var (samlet, rader) = (await new HjemmelsstatusRegel(db).LesAsync(tillatelse.Id))!.Value;
        var rad = Assert.Single(rader.Values);
        Assert.Equal((Hjemmelsstatus.Viderefort, Hjemmelsstatus.Dekket), (rad.Status, rad.VidereforingDekning));
        Assert.Equal(Hjemmelsstatus.SamletViderefort, samlet.Status);
        var funn = await new HjemmelOpphevetFunnregel(db, new FunnTjeneste(db)).KjorAsync(true, "Test", true, [tillatelse.Id]);
        Assert.Equal(0, funn.Nye);
    }

    [Fact]
    public async Task En_instruks_er_fortsatt_ikke_dekket_av_en_overgangsregel_om_enkeltvedtak_og_er_uavklart()
    {
        await using var db = fixture.NyDbContext();
        var k = new Korpus(db);
        var (l1996, n) = await PostlovAsync(k);
        var instruks = await k.DokumentAsync("forskrift", new(2001, 8, 31), Nr(), "INS", "Instruks om kontroll av postsendinger.",
            [("ledd-1", "Postkontoret skal føre kontroll med alle sendinger.", null)],
            annetOm: $"Fastsatt med hjemmel i lov av {Ref(new(1996, 11, 29), n)} om formidling av landsdekkende postsendinger (postloven) § 7.");
        await k.HjemlerAsync(instruks, (l1996, "§7"));

        var rapport = await new HjemmelsstatusRegel(db).KjorAsync(skriv: true, "Test", lesLovtidend: false, forskriftIder: [instruks.Id]);
        Assert.Equal(0, rapport.Enkeltvedtak);
        var (samlet, rader) = (await new HjemmelsstatusRegel(db).LesAsync(instruks.Id))!.Value;
        Assert.Equal(Hjemmelsstatus.DokumenttypeIkkeNevnt, Assert.Single(rader.Values).VidereforingDekning);
        Assert.Equal(Hjemmelsstatus.SamletUavklart, samlet.Status);
    }
}
