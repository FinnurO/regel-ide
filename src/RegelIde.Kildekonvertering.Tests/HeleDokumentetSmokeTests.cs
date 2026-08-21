namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// Kjører hele konverteringen mot de ekte, fullstendige dokumentene (ikke bare testcase-utdrag) —
/// bekrefter at parseren håndterer alle strukturelle varianter som faktisk finnes i alkoholloven/
/// alkoholforskriften, jf. 06-veikart.md byggesteg 1 ("hele loven, ikke bare de relevante kapitlene").
/// </summary>
public class HeleDokumentetSmokeTests
{
    [Fact]
    public void Konverterer_hele_alkoholloven_uten_feil()
    {
        var html = Testdata.LesAlkoholloven();
        var resultat = LovdataKonverterer.Konverter(html, new DateOnly(2026, 7, 23));
        Assert.NotEmpty(resultat.Noder);
    }

    [Fact]
    public void Konverterer_hele_alkoholforskriften_uten_feil()
    {
        var html = Testdata.LesAlkoholforskriften();
        var resultat = LovdataKonverterer.Konverter(html, new DateOnly(2026, 7, 23));
        Assert.NotEmpty(resultat.Noder);
    }

    [Fact]
    public void Konverterer_hele_forvaltningsloven_uten_feil()
    {
        var html = Testdata.LesForvaltningsloven();
        var resultat = LovdataKonverterer.Konverter(html, new DateOnly(2026, 7, 23));
        Assert.NotEmpty(resultat.Noder);
    }

    /// <summary>
    /// Kapittelfri lov — paragrafer direkte i documentBody, ingen kapittel-<see cref="NodeType.Kapittel"/>-
    /// noder i det hele tatt (bekreftet ekte, se Testdata.LesMotorferdselloven). Regresjonstest for
    /// funnet 2026-08-20 under full Lovdata-synkronisering — 716 av 5879 dokumenter i korpuset falt i
    /// nøyaktig denne kategorien.
    /// </summary>
    [Fact]
    public void Konverterer_kapittelfri_lov_uten_feil()
    {
        var html = Testdata.LesMotorferdselloven();
        var resultat = LovdataKonverterer.Konverter(html, new DateOnly(2026, 8, 20));

        Assert.NotEmpty(resultat.Noder);
        Assert.DoesNotContain(resultat.Noder, n => n.NodeType == NodeType.Kapittel);
        Assert.Contains(resultat.Noder, n => n.NodeType == NodeType.Paragraf && n.ParentEid is null);
    }

    /// <summary>
    /// Innlemmet EU-forordningstekst (GDPR-vedlegget) med flere strukturvarianter samlet i ett
    /// dokument — se Testdata.LesPersonopplysningsloven. Regresjonstest for hele runden med gjennomgang
    /// mot Lovdatas offisielle formatdokumentasjon (https://api.lovdata.no/xmldocs, 2026-08-21):
    /// KAPITTEL-ord i store bokstaver, kommentarprosa (defaultP) uten paragrafer på kapittelnivå,
    /// en tredje underinndelingsdybde ("Avsnitt N") uten data-name-attributt, sentrerte avslutnings-
    /// avsnitt (centeredP), og fotnoter med flere strukturerte ledd/avsnitt-barn i stedet for ren
    /// inline-tekst.
    /// </summary>
    [Fact]
    public void Konverterer_personopplysningsloven_med_innlemmet_gdpr_tekst_uten_feil()
    {
        var html = Testdata.LesPersonopplysningsloven();
        var resultat = LovdataKonverterer.Konverter(html, new DateOnly(2026, 8, 21));

        Assert.NotEmpty(resultat.Noder);
        Assert.Contains(resultat.Noder, n => n.NodeType == NodeType.Underinndeling && n.Nummer == "1" && n.Overskrift?.StartsWith("Åpenhet") == true);
    }

    /// <summary>
    /// "Kap. N."-forkortelsen OG en punktliste direkte under en paragraf uten omsluttende ledd — se
    /// Testdata.LesTannhelsetjenesteloven. Regresjonstest for begge funnene, løst i samme runde som
    /// personopplysningsloven-fixturen.
    /// </summary>
    [Fact]
    public void Konverterer_tannhelsetjenesteloven_med_kap_forkortelse_og_liste_under_paragraf_uten_feil()
    {
        var html = Testdata.LesTannhelsetjenesteloven();
        var resultat = LovdataKonverterer.Konverter(html, new DateOnly(2026, 8, 21));

        Assert.NotEmpty(resultat.Noder);
        Assert.Contains(resultat.Noder, n => n.NodeType == NodeType.Punkt);
    }

}
