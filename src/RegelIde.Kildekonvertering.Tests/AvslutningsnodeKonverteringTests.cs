namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// [Ny, avslutningsnode-runden, 2026-10-09, issue #361] Tekst etter en liste i et ledd eller punkt blir
/// en egen avslutningsnode (<c>node_type = avslutning</c>, eId <c>{forelder-eId}/avslutning</c>,
/// <c>-2</c>, <c>-3</c> … ved flere lister), sortert etter punktene. Før ble den limt inn i leddets egen
/// tekst, slik at leddteksten ikke var ordrett lovtekst og avslutningen ble vist foran lista.
///
/// <para>
/// Spørsmålet testene svarer på er det modelløren stiller (docs/32 §1): <b>hva står det i loven, i den
/// rekkefølgen det står?</b> Energiloven § 10-2 annet ledd er saken som avdekket feilen: lagret
/// leddtekst var «… samt i Første punktum gjelder likevel …», der lista a–c sto imellom.
/// </para>
///
/// <para>
/// De syntetiske tilfellene lages ved målrettede endringer av det ekte energiloven-utdraget (samme
/// praksis som <see cref="EdgeCaseTests"/>), og formen på hvert tilfelle er hentet fra et konkret,
/// målt dokument i Lovdatas bulk (navngitt i hver test).
/// </para>
/// </summary>
public class AvslutningsnodeKonverteringTests
{
    // ---------- Ekte fixturer ----------

    [Fact]
    public void Energiloven_10_2_annet_ledd_har_innledning_punkter_og_avslutning_i_lovens_rekkefolge()
    {
        var r = LovdataKonverterer.Konverter(Testdata.LesEnergilovenUtdrag10_2(), new DateOnly(2026, 10, 9));
        var leddEid = $"{Avslutningsutdrag.ParagrafEid}/ledd-2";

        var ledd = r.Noder.Single(n => n.Eid == leddEid);
        Assert.Equal(
            "Reguleringsmyndigheten kan, uavhengig av første ledd, helt eller delvis trekke tilbake en " +
            "omsetningskonsesjon etter § 4-1 ved grovt eller gjentatte brudd på bestemmelser gitt i eller i " +
            "medhold av loven her, samt i",
            ledd.Tekst);

        var barn = r.Noder.Where(n => n.ParentEid == leddEid).OrderBy(n => n.SorteringsRekkefolge).ToList();
        Assert.Equal(["punkt-1", "punkt-2", "punkt-3", "avslutning"], barn.Select(n => n.Eid[(leddEid.Length + 1)..]));
        Assert.Equal("avtaleloven § 38 b", barn[0].Tekst);

        var avslutning = barn[^1];
        Assert.Equal(NodeType.Avslutning, avslutning.NodeType);
        Assert.Equal(
            "Første punktum gjelder likevel bare når lovbruddet gir rimelig grunn til å tro at fortsatt " +
            "virksomhet kan skade sluttbrukere eller tilliten til strømmarkedet eller aktørene i strømmarkedet.",
            avslutning.Tekst);
        Assert.Null(avslutning.Nummer);
        Assert.True(ledd.SorteringsRekkefolge < barn[0].SorteringsRekkefolge);

        // Ledd uten liste er urørt: ingen avslutning under første, tredje og fjerde ledd.
        Assert.Single(r.Noder, n => n.NodeType == NodeType.Avslutning);
    }

    [Fact]
    public void Energiloven_10_2_akn_har_intro_og_wrapUp_i_lista()
    {
        var r = LovdataKonverterer.Konverter(Testdata.LesEnergilovenUtdrag10_2(), new DateOnly(2026, 10, 9));
        var leddEid = $"{Avslutningsutdrag.ParagrafEid}/ledd-2";

        Assert.Contains(
            $"<paragraph eId=\"{leddEid}\" regelIde:kildeId=\"kapittel-11-paragraf-3-ledd-2\"><num>2</num><list><intro><p>Reguleringsmyndigheten",
            r.AknXml);
        Assert.Contains("samt i</p></intro><point ", r.AknXml);
        Assert.Contains(
            $"</point><wrapUp eId=\"{leddEid}/avslutning\" regelIde:kildeId=\"kapittel-11-paragraf-3-ledd-2-avslutning\"><p>Første punktum gjelder likevel",
            r.AknXml);
        Assert.Contains("strømmarkedet.</p></wrapUp></list></paragraph>", r.AknXml);
        // Innledningen står én gang, ikke dobbelt (både i <content> og <intro>).
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(r.AknXml, "Reguleringsmyndigheten kan, uavhengig"));
    }

    [Fact]
    public void Forvaltningsloven_18d_referansen_i_avslutningen_flyttes_til_avslutningsnoden_med_riktig_posisjon()
    {
        var r = LovdataKonverterer.Konverter(Testdata.LesForvaltningsloven(), new DateOnly(2026, 10, 9));
        const string lovEli = "https://lovdata.no/eli/lov/1967/02/10/nor";
        var leddEid = $"{lovEli}/§18d/ledd-1";

        var ledd = r.Noder.Single(n => n.Eid == leddEid);
        Assert.Equal("Unntakene i §§ 18 a og 18 b gjelder ikke:", ledd.Tekst);

        var avslutning = r.Noder.Single(n => n.Eid == $"{leddEid}/avslutning");
        Assert.Equal(
            "§ 18 a gjelder likevel for dokument som blir utvekslet mellom kommunale og fylkeskommunale " +
            "kontrollutvalg og utvalgets sekretariat.",
            avslutning.Tekst);
        Assert.Equal(
            ["punkt-1", "punkt-2", "punkt-3", "punkt-4", "avslutning"],
            r.Noder.Where(n => n.ParentEid == leddEid).OrderBy(n => n.SorteringsRekkefolge).Select(n => n.Eid[(leddEid.Length + 1)..]));

        // Leddet beholder bare referansene i innledningen …
        var fraLedd = r.Referanser.Where(x => x.FraNodeEid == leddEid).ToList();
        Assert.Equal([$"{lovEli}/§18a", $"{lovEli}/§18b"], fraLedd.Select(x => x.TilEid));
        Assert.All(fraLedd, x => Assert.True(x.TekstStart + x.TekstLengde <= ledd.Tekst!.Length));
        // … og referansen i avslutningen peker fra avslutningsnoden, med posisjon i DENS tekst.
        var fraAvslutning = Assert.Single(r.Referanser, x => x.FraNodeEid == avslutning.Eid);
        Assert.Equal($"{lovEli}/§18a", fraAvslutning.TilEid);
        Assert.Equal("§ 18 a", avslutning.Tekst!.Substring(fraAvslutning.TekstStart!.Value, fraAvslutning.TekstLengde!.Value));
    }

    // ---------- Syntetiske varianter, i ledd og punkt ----------

    [Fact]
    public void Los_tekst_etter_liste_blir_avslutning()
    {
        // Formen fra 188 legalP i bulken (målt 2026-10-09), f.eks. sf-19921127-109: tekst rett i
        // leddet etter </ol>, uten <p class="leddfortsettelse">.
        var r = Konverter(
            "<article class=\"legalP\" id=\"kapittel-11-paragraf-3-ledd-1\">Ferdsel i innmark som fører til utmark," +
            Liste("kapittel-11-paragraf-3-ledd-1", "over tun", "over gårdsvei") +
            " er tillatt hele året.</article>");
        var leddEid = $"{Avslutningsutdrag.ParagrafEid}/ledd-1";

        Assert.Equal("Ferdsel i innmark som fører til utmark,", r.Noder.Single(n => n.Eid == leddEid).Tekst);
        Assert.Equal("er tillatt hele året.", r.Noder.Single(n => n.Eid == $"{leddEid}/avslutning").Tekst);
    }

    [Fact]
    public void To_lister_med_tekst_etter_hver_gir_avslutning_og_avslutning_2_og_fortlopende_punktnummer()
    {
        // Formen «LØS LISTE LØS LISTE» (29 legalP i 3 dokumenter, flest i sf-20211216-3622).
        var r = Konverter(
            "<article class=\"legalP\" id=\"kapittel-11-paragraf-3-ledd-1\">Innledning:" +
            Liste("kapittel-11-paragraf-3-ledd-1", "a", "b") + "Mellomtekst:" +
            Liste("kapittel-11-paragraf-3-ledd-1-x", "c", "d") + "Sluttekst.</article>");
        var leddEid = $"{Avslutningsutdrag.ParagrafEid}/ledd-1";

        var barn = r.Noder.Where(n => n.ParentEid == leddEid).OrderBy(n => n.SorteringsRekkefolge).ToList();
        Assert.Equal(
            ["punkt-1", "punkt-2", "avslutning", "punkt-3", "punkt-4", "avslutning-2"],
            barn.Select(n => n.Eid[(leddEid.Length + 1)..]));
        Assert.Equal("Mellomtekst:", barn[2].Tekst);
        Assert.Equal("Sluttekst.", barn[5].Tekst);
        Assert.Equal("kapittel-11-paragraf-3-ledd-1-avslutning-2", barn[5].KildeId);
        Assert.Equal("Innledning:", r.Noder.Single(n => n.Eid == leddEid).Tekst);
    }

    [Fact]
    public void Nestet_ledd_etter_liste_i_numberedLegalP_havner_i_avslutningen()
    {
        // Formen fra 343 numberedLegalP i bulken, f.eks. nl-19250807-000 (Svalbard-bergverksordningen)
        // § 2 post 1: «… under følgende betingelser:» + liste + <article class="legalP"> med neste ledd.
        // Det nestede leddet var limt inn i leddteksten før; nå står det etter punktene.
        var r = Konverter(
            "<article class=\"numberedLegalP\" data-numerator=\"1\" id=\"kapittel-11-paragraf-3-nummer-1\">1. Vilkår:" +
            Liste("kapittel-11-paragraf-3-nummer-1", "første", "andre") +
            "<article class=\"legalP\" id=\"kapittel-11-paragraf-3-nummer-1-ledd-1\">Gebyret beregnes særskilt.</article></article>");
        var leddEid = $"{Avslutningsutdrag.ParagrafEid}/ledd-1";

        Assert.Equal("1. Vilkår:", r.Noder.Single(n => n.Eid == leddEid).Tekst);
        Assert.Equal("Gebyret beregnes særskilt.", r.Noder.Single(n => n.Eid == $"{leddEid}/avslutning").Tekst);
    }

    [Fact]
    public void Liste_uten_li_deler_ikke_teksten()
    {
        // En tom liste gir ingen punkter, og en avslutning uten punkter foran seg ville vært meningsløs —
        // teksten beholdes samlet, med det gamle mellomrommet der lista sto.
        var r = Konverter(
            "<article class=\"legalP\" id=\"kapittel-11-paragraf-3-ledd-1\">Før<ol class=\"defaultList\"></ol>etter.</article>");

        Assert.Equal("Før etter.", r.Noder.Single(n => n.Eid == $"{Avslutningsutdrag.ParagrafEid}/ledd-1").Tekst);
        Assert.DoesNotContain(r.Noder, n => n.NodeType == NodeType.Avslutning);
    }

    [Fact]
    public void Ukjent_element_etter_liste_kaster_fortsatt()
    {
        var ex = Assert.Throws<NotSupportedException>(() => Konverter(
            "<article class=\"legalP\" id=\"kapittel-11-paragraf-3-ledd-1\">Innledning:" +
            Liste("kapittel-11-paragraf-3-ledd-1", "a") + "<figure>ukjent</figure></article>"));
        Assert.Contains("Ingen gjettet fallback", ex.Message);
    }

    // ---------- Leddfortsettelse direkte under paragrafen (HåndterParagrafBarn) ----------

    [Fact]
    public void Leddfortsettelse_etter_liste_direkte_under_paragrafen_blir_avslutning_pa_paragrafen()
    {
        // Formen fra sf-19801023-8798 § 3 og sf-20191121-1578 § 2: <ol> direkte under paragrafen, så
        // <p class="leddfortsettelse">. Før ble teksten limt inn i SISTE PUNKT.
        var r = Konverter(
            Liste("kapittel-11-paragraf-3", "den som ikke har betalt", "den som har meldt fra") +
            "<p class=\"leddfortsettelse\">kan pålegges tilleggsavgift.</p>" +
            "<article class=\"legalP\" id=\"kapittel-11-paragraf-3-ledd-1\">Dette gjelder også ellers.</article>");
        var p = Avslutningsutdrag.ParagrafEid;

        Assert.Equal("den som har meldt fra", r.Noder.Single(n => n.Eid == $"{p}/punkt-2").Tekst);
        var avslutning = r.Noder.Single(n => n.Eid == $"{p}/avslutning");
        Assert.Equal(p, avslutning.ParentEid);
        Assert.Equal("kan pålegges tilleggsavgift.", avslutning.Tekst);
        Assert.Equal("kapittel-11-paragraf-3-avslutning", avslutning.KildeId);
        Assert.Equal(
            ["punkt-1", "punkt-2", "avslutning", "ledd-1"],
            r.Noder.Where(n => n.ParentEid == p).OrderBy(n => n.SorteringsRekkefolge).Select(n => n.Eid[(p.Length + 1)..]));
    }

    [Fact]
    public void Leddfortsettelse_etter_en_avslutning_foyes_til_samme_avslutning()
    {
        // To leddfortsettelser etter hverandre (sf-20160519-0542, «◄B» «►EØS»).
        var r = Konverter(
            Liste("kapittel-11-paragraf-3", "a") +
            "<p class=\"leddfortsettelse\">Første.</p><p class=\"leddfortsettelse\">Andre.</p>");

        var avslutning = Assert.Single(r.Noder, n => n.NodeType == NodeType.Avslutning);
        Assert.Equal("Første. Andre.", avslutning.Tekst);
        Assert.Equal(LovdataIdentifikatorer.BeregnTekstHash("Første. Andre."), avslutning.TekstHash);
    }

    [Fact]
    public void Leddfortsettelse_etter_ledd_uten_punkter_foyes_til_leddet_som_for()
    {
        // Ledd + fotnote + leddfortsettelse (sf-20110606-0666, «◄ M1»): ingen liste å avslutte, og
        // teksten står allerede i riktig rekkefølge. Uendret oppførsel.
        var r = Konverter(
            "<article class=\"legalP\" id=\"kapittel-11-paragraf-3-ledd-1\">Leddtekst.</article>" +
            "<footer class=\"footnotes\"></footer><p class=\"leddfortsettelse\">◄ M1</p>");

        Assert.Equal("Leddtekst. ◄ M1", r.Noder.Single(n => n.Eid == $"{Avslutningsutdrag.ParagrafEid}/ledd-1").Tekst);
        Assert.DoesNotContain(r.Noder, n => n.NodeType == NodeType.Avslutning);
    }

    [Fact]
    public void Leddfortsettelse_etter_ledd_med_punkter_blir_avslutning_pa_leddet()
    {
        var r = Konverter(
            "<article class=\"legalP\" id=\"kapittel-11-paragraf-3-ledd-1\">Innledning:" +
            Liste("kapittel-11-paragraf-3-ledd-1", "a", "b") + "</article>" +
            "<p class=\"leddfortsettelse\">Etter lista, men utenfor leddet i HTML-en.</p>");
        var leddEid = $"{Avslutningsutdrag.ParagrafEid}/ledd-1";

        Assert.Equal("Innledning:", r.Noder.Single(n => n.Eid == leddEid).Tekst);
        var avslutning = r.Noder.Single(n => n.Eid == $"{leddEid}/avslutning");
        Assert.Equal("Etter lista, men utenfor leddet i HTML-en.", avslutning.Tekst);
        Assert.Equal(leddEid, avslutning.ParentEid);
    }

    [Fact]
    public void Leddfortsettelse_etter_ledd_som_alt_slutter_med_avslutning_foyes_til_den()
    {
        var r = Konverter(
            "<article class=\"legalP\" id=\"kapittel-11-paragraf-3-ledd-1\">Innledning:" +
            Liste("kapittel-11-paragraf-3-ledd-1", "a") +
            "<p class=\"leddfortsettelse\">Inne.</p></article><p class=\"leddfortsettelse\">Ute.</p>");

        var avslutning = Assert.Single(r.Noder, n => n.NodeType == NodeType.Avslutning);
        Assert.Equal("Inne. Ute.", avslutning.Tekst);
    }

    // ---------- Hjelpere ----------

    private static KonverteringResultat Konverter(string paragrafinnhold) =>
        LovdataKonverterer.Konverter(Avslutningsutdrag.MedParagrafinnhold(paragrafinnhold), new DateOnly(2026, 10, 9));

    /// <summary>En <c>&lt;ol class="defaultList"&gt;</c> i Lovdatas form (li → listArticle → legalP).</summary>
    internal static string Liste(string idPrefiks, params string[] punkter) =>
        "<ol class=\"defaultList\" type=\"a\">" +
        string.Concat(punkter.Select((tekst, i) =>
            $"<li data-name=\"{(char)('a' + i)}.\" value=\"{i + 1}\"><article class=\"listArticle\" id=\"{idPrefiks}-punkt-{i + 1}\">" +
            $"<article class=\"legalP\" id=\"{idPrefiks}-punkt-{i + 1}-ledd-1\">{tekst}</article></article></li>")) +
        "</ol>";
}

/// <summary>
/// [Ny, avslutningsnode-runden, 2026-10-09, issue #361] Det ekte energiloven-utdraget (§ 10-2) med
/// paragrafens innhold byttet ut — overskrift, dokumenthode og endringshistorikk står urørt.
/// </summary>
internal static class Avslutningsutdrag
{
    public const string LovEli = "https://lovdata.no/eli/lov/1990/06/29/50/nor";
    public const string ParagrafEid = LovEli + "/§10-2";

    public static string MedParagrafinnhold(string innhold)
    {
        var html = Testdata.LesEnergilovenUtdrag10_2();
        var start = html.IndexOf("</h3>", StringComparison.Ordinal) + "</h3>".Length;
        var slutt = html.IndexOf("<article class=\"changesToParent\">", StringComparison.Ordinal);
        Assert.True(start > "</h3>".Length && slutt > start, "Utdraget har ikke forventet form");
        return html[..start] + innhold + html[slutt..];
    }
}
