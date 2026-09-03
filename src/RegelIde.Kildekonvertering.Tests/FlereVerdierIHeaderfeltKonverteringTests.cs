namespace RegelIde.Kildekonvertering.Tests;

/// <summary>
/// Issue #152 (opprinnelig) — <c>HentFelt("ministry")</c> brukte tidligere rått <c>.InnerText.Trim()</c>
/// på <c>&lt;dd class="ministry"&gt;&lt;ul&gt;&lt;li&gt;…&lt;/li&gt;&lt;li&gt;…&lt;/li&gt;&lt;/ul&gt;&lt;/dd&gt;</c>
/// — for et dokument med FLERE ansvarlige departementer limte dette sammen navnene UTEN skilletegn
/// ("Landbruks- og matdepartementetNærings- og fiskeridepartementet", bekreftet ekte for 103 av 5899
/// rettskilder, live-hentet HTML for cbe34f67-a029-4bb3-861e-c825e596a585 "Forskrift om
/// næringsmessig transport av dyr"). [ENDRET, fler-verdi-departement, 2026-09-04] Den opprinnelige
/// fiksen produserte en ", "-sammensatt STRENG som mellomsteg — <see cref="RettskildeMetadata.AnsvarligDepartement"/>
/// er nå en EKTE liste (§ HentSammensattTekstListe), så disse testene verifiserer nå listeformen, ikke
/// lenger kommaseparering. Testes ved å utvide alkoholloven-fixturens ENKELT-departement-&lt;dd&gt; til
/// to &lt;li&gt;-elementer, samme "targeted fixture-injeksjon"-mønster som EdgeCaseTests.cs/
/// RaaMetadataKonverteringTests.cs allerede bruker for andre header-felt.
/// </summary>
public class FlereVerdierIHeaderfeltKonverteringTests
{
    [Fact]
    public void Ministry_med_flere_departementer_gir_egen_liste_ikke_sammenlimt()
    {
        var html = Testdata.LesAlkoholloven().Replace(
            "<dd class=\"ministry\"><ul><li>Helse- og omsorgsdepartementet</li></ul></dd>",
            "<dd class=\"ministry\"><ul><li>Helse- og omsorgsdepartementet</li><li>Nærings- og fiskeridepartementet</li></ul></dd>");

        var resultat = LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 2));

        Assert.Equal(
            ["Helse- og omsorgsdepartementet", "Nærings- og fiskeridepartementet"],
            resultat.Metadata.AnsvarligDepartement);
    }

    [Fact]
    public void Ministry_med_ett_departement_gir_ettelements_liste()
    {
        // Regresjonsvern: den langt vanligste, IKKE-berørte formen (ett <li> i lista, som ALLE 8
        // fixturene i data/kilder/raw-lovdata/ faktisk har) skal fortsatt gi nøyaktig samme ETT-elements
        // liste som før denne fler-verdi-runden.
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 9, 2));

        Assert.Equal(["Helse- og omsorgsdepartementet"], resultat.Metadata.AnsvarligDepartement);
    }

    [Fact]
    public void Felt_med_br_separerte_verdier_skilles_med_linjeskift_ikke_sammenlimt()
    {
        // Samme klasse bug som ministry, bekreftet ekte for miscInformation/eeaReferences (live-hentet
        // HTML, cbe34f67-a029-4bb3-861e-c825e596a585): "…(i kraft 7 april 2020).<br/><strong>Endret</strong>
        // ved …" har INGEN mellomrom mellom punktum og <br/> — et rått InnerText-kall ville gitt
        // "...2020).Endret ved..." sammenlimt. Simulert her via lastChangedBy-feltet (finnes allerede i
        // alkoholloven-fixturen), som er trygt å omforme uten å påvirke andre, allerede etablerte tester.
        var html = Testdata.LesAlkoholloven().Replace(
            "<dd class=\"lastChangedBy\"><a href=\"lov/2026-05-29-21\">lov/2026-05-29-21</a> fra 2026-07-20</dd>",
            "<dd class=\"lastChangedBy\">Første del.<br/>Andre del uten mellomrom foran.</dd>");

        var resultat = LovdataKonverterer.Konverter(html, new DateOnly(2026, 9, 2));

        Assert.Equal("Første del.\nAndre del uten mellomrom foran.", resultat.Metadata.SistEndretVed);
    }
}
