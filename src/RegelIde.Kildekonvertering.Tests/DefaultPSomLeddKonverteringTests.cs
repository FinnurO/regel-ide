namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// Regresjonstest for et bekreftet ekte, tapt-innhold-tilfelle: FOR-2001-03-09-439 (forskrift om
/// skipsmedisin) § 4 "Fartøygrupper" mistet HELE innholdet i tre av sine fire ledd ved import — Johann
/// fant dette manuelt via /begrepskandidater-siden og bekreftet ordrett mot
/// https://lovdata.no/forskrift/2001-03-09-439/§4.
///
/// Rotårsak (bekreftet mot ekte rå HTML hentet via <see cref="RegelIde.Data.LovdataBulkHenter.HentRaaHtmlAsync"/>,
/// IKKE gjettet): § 4 sitt ledd-1 ("Med fartøygrupper menes:") er en ordinær
/// <c>&lt;article class="legalP" id="…-ledd-1"&gt;</c>, men de tre påfølgende definisjonsavsnittene
/// ("Fartøygruppe A:"/"B:"/"C:" …) er <c>&lt;article class="defaultP"&gt;</c> UTEN NOE id-attributt
/// overhodet — Lovdatas offisielle klassedokumentasjon (https://api.lovdata.no/xmldocs) definerer
/// "defaultP" som formelt "ikke et juridisk ledd", og <see cref="LovdataHtmlParser"/> behandlet derfor
/// (før denne fiksen) ETHVERT defaultP direkte i en paragraf som metainformasjon/kommentar og hoppet
/// stille over det — korrekt for det ENE tidligere bekreftede tilfellet (personopplysningsloven § 34,
/// et "– – –"-plassholderavsnitt for elidert endringshistorikk), men FEIL her: disse tre avsnittene ER
/// reelt, substansielt rettskildeinnhold (ledd 2/3/4 under samme § 4), bare uten Lovdatas vanlige
/// auto-genererte ledd-id.
///
/// HTML-fixturen under er en MINIMAL, syntetisk isolasjon av nøyaktig dette mønsteret (ikke hele det
/// ekte dokumentet) — verifisert byte-for-byte mot den faktiske rå HTML-en for § 4 (samme klasser,
/// samme fravær av id-attributt på defaultP-avsnittene, samme tekst).
/// </summary>
public class DefaultPSomLeddKonverteringTests
{
    private const string MinimalDokumentMal = """
        <!DOCTYPE html><html lang="nb"><head><title>{0}</title></head><body>
        <header class="documentHeader" id="hode"><dl class="data-document-key-info">
        <dt class="legacyID">Datokode</dt><dd class="legacyID">{1}</dd>
        <dt class="ministry">Departement</dt><dd class="ministry"><ul><li>Testdepartementet</li></ul></dd>
        <dt class="title">Tittel</dt><dd class="title">{0}</dd>
        </dl></header>
        <main class="documentBody"><h1>{0}</h1>
        {2}
        </main></body></html>
        """;

    /// <summary>
    /// Kjerne-regresjonen: § 4 sine tre "Fartøygruppe A/B/C"-defaultP-avsnitt (uten id-attributt) skal
    /// nå fanges som ledd 2, 3 og 4 under paragrafen — ikke tapes.
    /// </summary>
    [Fact]
    public void Defaultp_uten_id_direkte_i_paragraf_fanges_som_ledd_nar_teksten_er_reelt_innhold()
    {
        var kropp = """
            <section class="section" data-name="kap1" id="kapittel-1"><h2>Kapittel 1. Alminnelige bestemmelser.</h2>
            <article class="legalArticle" data-lovdata-URL="SF/forskrift/2001-03-09-439/§4" data-name="§4" id="kapittel-1-paragraf-4">
            <h3 class="legalArticleHeader"><span class="legalArticleValue">§ 4</span>. <span class="legalArticleTitle">Fartøygrupper</span></h3>
            <article class="legalP" id="kapittel-1-paragraf-4-ledd-1">Med fartøygrupper menes:</article>
            <article class="defaultP"><i>Fartøygruppe A:</i> Havgående fartøyer herunder fartøyer som driver fiske til havs, uten begrensninger i fartsområde, samt havgående fartøyer som ikke faller inn under fartøygruppe B.</article>
            <article class="defaultP"><i>Fartøygruppe B:</i> Havgående fartøyer herunder fartøyer som driver fiske til havs, i farvann mindre enn 150 nautiske mil fra nærmeste havn med mulighet for medisinskfaglig assistanse eller 175 nautiske mil fra nærmeste havn med mulighet for medisinskfaglig assistanse dersom de i tillegg kontinuerlig oppholder seg innenfor rekkevidden til en helikoptertjeneste.</article>
            <article class="defaultP"><i>Fartøygruppe C:</i> Fartøyer som driver havnetrafikk og fartøyer som oppholder seg enten inntil 20 nautiske mil fra grunnlinjen eller som ikke har annen lugarinnretning enn et styrehus.</article>
            </article>
            </section>
            """;
        var html = string.Format(MinimalDokumentMal, "Forskrift om skipsmedisin", "FOR-2001-03-09-439", kropp);

        var resultat = LovdataKonverterer.Konverter(html);

        var paragraf4 = resultat.Noder.Single(n => n.NodeType == NodeType.Paragraf && n.Nummer == "§ 4");
        var ledd = resultat.Noder.Where(n => n.ParentEid == paragraf4.Eid && n.NodeType == NodeType.Ledd)
            .OrderBy(n => n.SorteringsRekkefolge).ToList();

        Assert.Equal(4, ledd.Count);
        Assert.Equal("Med fartøygrupper menes:", ledd[0].Tekst);
        Assert.StartsWith("Fartøygruppe A: Havgående fartøyer", ledd[1].Tekst);
        Assert.StartsWith("Fartøygruppe B: Havgående fartøyer", ledd[2].Tekst);
        Assert.StartsWith("Fartøygruppe C: Fartøyer som driver havnetrafikk", ledd[3].Tekst);

        // eId-ene må fortsatt følge det vanlige ledd-mønsteret (fortløpende, under paragrafens eId).
        Assert.Equal($"{paragraf4.Eid}/ledd-1", ledd[0].Eid);
        Assert.Equal($"{paragraf4.Eid}/ledd-2", ledd[1].Eid);
        Assert.Equal($"{paragraf4.Eid}/ledd-3", ledd[2].Eid);
        Assert.Equal($"{paragraf4.Eid}/ledd-4", ledd[3].Eid);

        // KildeId er PÅKREVD (Modeller.cs) og må derfor være syntetisert deterministisk siden kilde-
        // HTML-en ikke har noe id-attributt på disse tre avsnittene — men skal aldri kollidere med en
        // ekte Lovdata-id (som alltid inneholder "-ledd-"/"-paragraf-"-mønsteret, ikke "-avsnitt-").
        Assert.All(ledd.Skip(1), l => Assert.Contains("-avsnitt-", l.KildeId));
        Assert.Equal(4, ledd.Select(l => l.KildeId).Distinct().Count());
    }

    /// <summary>
    /// Regresjon i MOTSATT retning: et defaultP direkte i en paragraf som KUN er tankestreker (Lovdatas
    /// bekreftede plassholder-konvensjon for elidert innhold, personopplysningsloven § 34) skal
    /// FORTSATT hoppes over, ikke stille bli et tomt/meningsløst "ledd" — denne fiksen skiller på
    /// avsnittets faktiske tekstinnhold, ikke bare "defaultP direkte i paragraf => alltid ledd".
    /// </summary>
    [Fact]
    public void Defaultp_som_kun_er_tankestreker_hoppes_fortsatt_over_som_elidert_plassholder()
    {
        var kropp = """
            <section class="section" data-name="kap1" id="kapittel-1"><h2>Kapittel 1. Alminnelige bestemmelser.</h2>
            <article class="legalArticle" data-lovdata-URL="NL/lov/2018-06-15-38/§34" data-name="§34" id="kapittel-1-paragraf-34">
            <h3 class="legalArticleHeader"><span class="legalArticleValue">§ 34</span>. <span class="legalArticleTitle">Endringer i andre lover</span></h3>
            <article class="legalP" id="kapittel-1-paragraf-34-ledd-1">Fra den tiden loven her trer i kraft, gjøres følgende endringer i andre lover:</article>
            <article class="defaultP">– – –</article>
            </article>
            </section>
            """;
        var html = string.Format(MinimalDokumentMal, "Testlov", "LOV-2018-06-15-38", kropp);

        var resultat = LovdataKonverterer.Konverter(html);

        var paragraf34 = resultat.Noder.Single(n => n.NodeType == NodeType.Paragraf && n.Nummer == "§ 34");
        var ledd = resultat.Noder.Where(n => n.ParentEid == paragraf34.Eid && n.NodeType == NodeType.Ledd).ToList();

        Assert.Single(ledd);
        Assert.Equal("Fra den tiden loven her trer i kraft, gjøres følgende endringer i andre lover:", ledd[0].Tekst);
    }
}
