namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// Issue #127 — de resterende 10 av 15 bekreftede Lovdata header-metadatafelt (dateOfPublication/
/// legalArea/eeaReferences/dokid/refid/appliesTo/subunit/publishedIn/miscInformation/lastupdated), som
/// til nå aldri ble parset/lagret/vist. Ingen av de 8 eksisterende fixturene har ALLE disse feltene, så
/// testene her injiserer dem inn i alkoholloven-fixturen — verdiene er hentet ORDRETT fra tre live-
/// hentede, ekte Lovdata-dokumenter (2026-09-03: forskrift om næringsmessig transport av dyr,
/// luftfartsloven, lov om pristiltak), ikke oppdiktet.
/// </summary>
public class ResterendeMetadatafeltKonverteringTests
{
    [Fact]
    public void Enkle_ett_verdi_felt_fanges_som_ra_tekst()
    {
        var html = Testdata.LesAlkoholloven()
            .Replace(
                "<dt class=\"legacyID\">Datokode</dt>",
                "<dt class=\"dokid\">DokumentID</dt><dd class=\"dokid\">NL/lov/1989-06-02-27</dd>" +
                "<dt class=\"refid\">RefID</dt><dd class=\"refid\">lov/1989-06-02-27</dd>" +
                "<dt class=\"dateOfPublication\">Kunngjort</dt><dd class=\"dateOfPublication\">2013-06-21 15:35</dd>" +
                "<dt class=\"appliesTo\">Gjelder for</dt><dd class=\"appliesTo\">Norge</dd>" +
                "<dt class=\"publishedIn\">Publisert i</dt><dd class=\"publishedIn\">I 2012 hefte 4</dd>" +
                "<dt class=\"lastupdated\">Siste rettelse</dt><dd class=\"lastupdated\">2021-09-09 (faglige fotnoter fjernet)</dd>" +
                "<dt class=\"legacyID\">Datokode</dt>");

        var m = LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 2)).Metadata;

        Assert.Equal("NL/lov/1989-06-02-27", m.DokumentId);
        Assert.Equal("lov/1989-06-02-27", m.RefId);
        Assert.Equal("2013-06-21 15:35", m.Kunngjort);
        Assert.Equal("Norge", m.GjelderFor);
        Assert.Equal("I 2012 hefte 4", m.PublisertI);
        Assert.Equal("2021-09-09 (faglige fotnoter fjernet)", m.SisteRettelse);
    }

    [Fact]
    public void Subunit_med_flere_verdier_skilles_med_kommategn()
    {
        // Ekte innhold (rk152, "Forskrift om næringsmessig transport av dyr"):
        // <dd class="subunit"><ul><li>Avdeling for matpolitikk</li></ul></dd> — utvidet her til to
        // verdier for å teste samme "flere <li> => komma-skilt"-oppførsel som ministry (issue #152).
        var html = Testdata.LesAlkoholloven().Replace(
            "<dt class=\"legacyID\">Datokode</dt>",
            "<dt class=\"subunit\">Etat</dt><dd class=\"subunit\"><ul><li>Avdeling for matpolitikk</li>" +
            "<li>Folkehelseavdelingen</li></ul></dd><dt class=\"legacyID\">Datokode</dt>");

        var m = LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 2)).Metadata;

        Assert.Equal("Avdeling for matpolitikk, Folkehelseavdelingen", m.Etat);
    }

    [Fact]
    public void LegalArea_med_flere_omrader_skilles_med_kommategn_hver_med_bevart_brodsmulesti()
    {
        // Ekte innhold (lovendring159, "Lov om endringer i barnevernloven"):
        // <dd class="legalArea"><ul><li><a href="legal-areas/07" title="…">Familie-, person- og
        // barnerett</a> &gt; <a href="legal-areas/07.04" title="Barnevern">Barnevern</a></li></ul></dd>
        // — brødsmulestien ("… > …") er ekte tekstinnhold MELLOM to <a>-lenker i SAMME <li>, bevares
        // uendret; kun ULIKE <li>-elementer skal skilles med det NYE kommategnet.
        var html = Testdata.LesAlkoholloven().Replace(
            "<dt class=\"legacyID\">Datokode</dt>",
            "<dt class=\"legalArea\">Rettsområde</dt><dd class=\"legalArea\"><ul>" +
            "<li><a href=\"legal-areas/07\" title=\"Familie-, person- og barnerett\">Familie-, person- og barnerett</a> &gt; " +
            "<a href=\"legal-areas/07.04\" title=\"Barnevern\">Barnevern</a></li>" +
            "<li><a href=\"legal-areas/11\" title=\"Forvaltningsrett\">Forvaltningsrett</a></li>" +
            "</ul></dd><dt class=\"legacyID\">Datokode</dt>");

        var m = LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 2)).Metadata;

        Assert.Equal("Familie-, person- og barnerett > Barnevern, Forvaltningsrett", m.Rettsomrade);
    }

    [Fact]
    public void MiscInformation_med_br_separerte_avsnitt_skilles_med_linjeskift()
    {
        // Ekte innhold (rk152, forkortet) — <br/> UTEN mellomrom foran neste <strong> er den bekreftede
        // sammenlimings-fellen (issue #152s generelle mønster, ikke bare ministry).
        var html = Testdata.LesAlkoholloven().Replace(
            "<dt class=\"legacyID\">Datokode</dt>",
            "<dt class=\"miscInformation\">Annet om dokumentet</dt><dd class=\"miscInformation\">" +
            "<strong>Hjemmel:</strong> Fastsatt av Landbruks- og matdepartementet 8. februar 2012." +
            "<br /><strong>Endret</strong> ved forskrift 9 mai 2023 nr. 678.</dd>" +
            "<dt class=\"legacyID\">Datokode</dt>");

        var m = LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 2)).Metadata;

        Assert.Equal(
            "Hjemmel: Fastsatt av Landbruks- og matdepartementet 8. februar 2012.\nEndret ved forskrift 9 mai 2023 nr. 678.",
            m.AnnetOmDokumentet);
    }

    [Fact]
    public void Felt_som_mangler_i_kilden_gir_null_ikke_en_feil()
    {
        // alkoholloven-fixturen har FAKTISK 6 av de 10 (dokid/eeaReferences/legalArea/lastupdated/
        // miscInformation/refid — bekreftet via <dt class="…">-gjennomgang av selve fixturen), men
        // mangler de fire andre. «Ingen gjettet fallback» (§3.3): fravær er forventet, ikke en feil —
        // testet her mot nettopp de fire som faktisk mangler i denne fixturen (regresjonsvern mot en
        // fremtidig fixture-oppdatering som ved et uhell legger dem til uten at testen oppdages foreldet).
        var m = LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 9, 2)).Metadata;

        Assert.Null(m.Kunngjort);
        Assert.Null(m.GjelderFor);
        Assert.Null(m.Etat);
        Assert.Null(m.PublisertI);

        // Og de 6 som FAKTISK finnes i fixturen parses uten å kaste (verdiene i seg selv er ikke
        // interessante her — dekket av de andre, målrettede testene over).
        Assert.NotNull(m.DokumentId);
        Assert.NotNull(m.EuEosHenvisning);
        Assert.NotNull(m.Rettsomrade);
        Assert.NotNull(m.SisteRettelse);
        Assert.NotNull(m.AnnetOmDokumentet);
        Assert.NotNull(m.RefId);
    }
}
