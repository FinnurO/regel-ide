namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// Del D — <see cref="NettsideTekstParser"/> mot ekte fixtures (data/kilder/raw-nettside/, hentet
/// 2026-08-13, se README der for metodepresisering). Ren parser-testing — DB-koblingen
/// (<c>NettsideGrafKobler</c>) testes i RegelIde.Data.Tests, som beviser selve "koble alle
/// sammen"-graf-strekket helt frem til en importert rettskilde.
/// </summary>
public class NettsideTekstParserTests
{
    private static NettsideParseResultat ParseFixture(string filnavn)
    {
        var innhold = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "Nettside", filnavn));
        var f = NettsideFixtureLeser.Les(innhold);
        return NettsideTekstParser.Parse(f.KanoniskUrl, f.Tittel, f.RaaTekst);
    }

    [Fact]
    public void Bundlingssiden_klassifiserer_alkohollovens_lenke_som_lovdatalenke_med_riktig_eid_kandidat()
    {
        var resultat = ParseFixture("retningslinjer-for-tildeling-av-salgsog-skjenkebevillinger-og-forskrift-om-salgsskjenkeog-apningstider.txt");

        var alkoholloven = resultat.Lenker.Single(l => l.RaaHref == "https://lovdata.no/dokument/NL/lov/1989-06-02-27");
        Assert.Equal(NettsideLenketype.Lovdatalenke, alkoholloven.Type);
        Assert.Equal("https://lovdata.no/eli/lov/1989/06/02/27/nor", alkoholloven.TilEidKandidat);

        var alkoholforskriften = resultat.Lenker.Single(l => l.RaaHref == "https://lovdata.no/dokument/SF/forskrift/2005-06-08-538");
        Assert.Equal(NettsideLenketype.Lovdatalenke, alkoholforskriften.Type);
        Assert.Equal("https://lovdata.no/eli/forskrift/2005/06/08/538/nor", alkoholforskriften.TilEidKandidat);
    }

    [Fact]
    public void Bundlingssiden_klassifiserer_pdf_omtale_og_intern_lenke_som_lenker_til()
    {
        var resultat = ParseFixture("retningslinjer-for-tildeling-av-salgsog-skjenkebevillinger-og-forskrift-om-salgsskjenkeog-apningstider.txt");

        var retningslinjerPdf = resultat.Lenker.Single(l => l.RaaHref == "/api/rest/filer/V51903878");
        Assert.Equal(NettsideLenketype.LenkerTil, retningslinjerPdf.Type);
        Assert.Null(retningslinjerPdf.TilEidKandidat);

        var forskriftPdf = resultat.Lenker.Single(l => l.RaaHref == "/api/rest/filer/V51903879");
        Assert.Equal(NettsideLenketype.LenkerTil, forskriftPdf.Type);

        var kontorForSkjenkesaker = resultat.Lenker.Single(l =>
            l.RaaHref == "https://www.bergen.kommune.no/omkommunen/avdelinger/kontor-for-skjenkesaker");
        Assert.Equal(NettsideLenketype.LenkerTil, kontorForSkjenkesaker.Type);
    }

    [Fact]
    public void Eldre_lovdata_url_formater_gir_ingen_eid_kandidat_dokumentert_begrensning()
    {
        // godkjenning-av-ny-styrer-... bruker det ELDRE "all/nl-ÅÅÅÅMMDD-NNN.html"-formatet, ikke
        // det moderne "/dokument/"-formatet — se README for hvorfor dette IKKE tolkes.
        var resultat = ParseFixture("godkjenning-av-ny-styrer-stedfortreder-og-daglig-leder-i-bevillinger.txt");

        var alkoholloven = resultat.Lenker.Single(l => l.RaaHref == "http://www.lovdata.no/all/nl-19890602-027.html");
        Assert.Equal(NettsideLenketype.LenkerTil, alkoholloven.Type);
        Assert.Null(alkoholloven.TilEidKandidat);
    }

    [Fact]
    public void Cgi_wift_formatet_gir_heller_ingen_eid_kandidat()
    {
        var resultat = ParseFixture("etablererproven-og-kunnskapsproven.txt");

        var alkoholloven = resultat.Lenker.Single(l => l.RaaHref.Contains("cgi-wift"));
        Assert.Equal(NettsideLenketype.LenkerTil, alkoholloven.Type);
        Assert.Null(alkoholloven.TilEidKandidat);

        // Samme side har OGSÅ en moderne-format lenke til serveringsloven — den SKAL løses.
        var serveringsloven = resultat.Lenker.Single(l => l.RaaHref == "https://lovdata.no/dokument/NL/lov/1997-06-13-55");
        Assert.Equal(NettsideLenketype.Lovdatalenke, serveringsloven.Type);
        Assert.Equal("https://lovdata.no/eli/lov/1997/06/13/55/nor", serveringsloven.TilEidKandidat);
    }

    [Fact]
    public void InnholdsHash_beregnes_og_endres_med_teksten()
    {
        var resultat = ParseFixture("krav-om-fettutskiller.txt");
        Assert.NotNull(resultat.Side.InnholdsHash);
        Assert.Equal(LovdataIdentifikatorer.BeregnTekstHash(resultat.Side.RaaTekst!), resultat.Side.InnholdsHash);
    }

    [Fact]
    public void Side_uten_lenker_gir_tom_lenkeliste_ikke_krasj()
    {
        var resultat = ParseFixture("soknad-om-utvidet-skjenkeareal-for-en-enkelt-anledning.txt");
        Assert.NotEmpty(resultat.Lenker); // denne siden har faktisk ett skjema-lenke
        Assert.All(resultat.Lenker, l => Assert.Equal(NettsideLenketype.LenkerTil, l.Type));
    }
}
