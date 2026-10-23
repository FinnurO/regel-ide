using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>[Ny, issue #395] De rene reglene uten database: disposisjonstypene på tittel og innhold, kildeformen fra dokid og
/// livsløpsmønstrene. Eksemplene er ordrette titler og ledd fra korpuset og Lovtidend-arkivet (målt 2026-10-23).</summary>
public class OrganiseringskilderMonstreTests
{
    private static string[] Typer(string tittel) => Disposisjonstyper.KlassifiserTittel(tittel).Select(t => t.Disposisjonstype).ToArray();

    [Theory]
    [InlineData("Delegering av myndighet til Samferdselsdepartementet etter lov om merking av forbruksvarer m.v. § 3 og § 6a.", "delegering")]
    [InlineData("Overføring av myndighet til Arbeids- og administrasjonsdepartementet etter lov av 13. juni 1980 nr. 35 om fri rettshjelp.", "delegering")]
    [InlineData("Overføring av forvaltningsansvaret for lov 4. juli 2003 nr. 74 om hundehold (hundeloven) fra Justis- og beredskapsdepartementet til Landbruks- og matdepartementet", "overforing_ressortfordeling")]
    [InlineData("Instruks for Regjeringsadvokaten", "instruks")]
    [InlineData("Våpeninstruks for politiet", "instruks")]
    [InlineData("Vedtak om opprettelse av Mattilsynet.", "oppretting")]
    [InlineData("Vedtak om endring av navn fra Nye Kripos til Kripos.", "navneendring")]
    [InlineData("Omorganisering av departementsstrukturen fra 1. oktober 2004.", "omorganisering")]
    [InlineData("Endringer i departementstrukturen fra 1. januar 2010", "omorganisering")]
    [InlineData("Meddelelse om ikrafttredelse av lov 22. juni 2018 nr. 83 om kommuner og fylkeskommuner (kommuneloven)", "ikrafttredelse")]
    [InlineData("Forskrift om omsetning av alkoholholdig drikk mv. (alkoholforskriften)", "regulering")]
    public void Tittelen_gir_typen(string tittel, string forventet) => Assert.Equal([forventet], Typer(tittel));

    [Fact]
    public void En_tittel_kan_gi_flere_typer()
    {
        Assert.Equal(["oppretting", "nedleggelse"], Typer("Opprettelse av Næringsdepartementet og nedleggelse av Industridepartementet."));
        Assert.Equal(["ikrafttredelse", "delegering"],
            Typer("Ikrafttredelse av lov 31. mai 1974 nr. 19 om konsesjon og om forkjøpsrett for det offentlige ved erverv av fast eiendom (kosesjonsloven). Delegering av myndighet etter loven."));
    }

    [Theory]
    // Ingen gjetting: et rent endringsvedtak er ikke en regulering, og et stortingsvedtak har ingen regel som slår til.
    [InlineData("Forskrift om endring i forskrift om sikkerhet ved leketøy (leketøyforskriften)")]
    [InlineData("Lov om endringer i skatteloven")]
    [InlineData("Stortingsvedtak om fastsetting av finansskatt på lønn for 2025")]
    // Navnet på en forskrift er ikke et organ (funnet ved kaldsjekken 2026-10-23).
    [InlineData("Endring av navn på forskrift om distriktsinndelinger mv. i politi- og lensmannsetaten")]
    public void Uten_regel_som_slaar_til_er_kilden_uklassifisert(string tittel) => Assert.Empty(Typer(tittel));

    [Fact]
    public void Opprettelse_av_noe_som_ikke_er_et_organ_er_ikke_organisering() =>
        // Objektet har liten forbokstav: et område, ikke et organ. Tittelen har regelverksform og blir en vanlig regulering.
        Assert.Equal(["regulering"], Typer("Forskrift om opprettelse av et permanent restriksjonsområde over Jan Mayen mv."));

    [Theory]
    // Juristrunden 2026-10-23 (K01–K03): anledningen i tittelen er ikke disposisjonen.
    [InlineData("Endring i forskrifter som følge av oppretting av Miljødirektoratet")]
    [InlineData("Overføring av myndighet fra Arbeids- og administrasjonsdepartementet og Barne- og familiedepartementet til Justis- og politidepartementet i forbindelse med etableringen av Direktoratet for samfunnssikkerhet og beredskap.")]
    public void Anledningen_i_tittelen_gir_ikke_oppretting(string tittel) => Assert.DoesNotContain("oppretting", Typer(tittel));

    [Fact]
    public void Instruksen_som_objekt_midt_i_tittelen_er_ikke_en_instruks() =>
        Assert.Equal(["delegering"], Typer("Delegering av myndighet til å endre instruksen for rådet for Fiskeridirektoratets ernæringsinstitutt."));

    [Fact]
    public void Innholdsmonstrene_leser_punktene_i_et_omorganiseringsvedtak()
    {
        var tittel = Disposisjonstyper.KlassifiserTittel("Omorganisering av departementsstrukturen fra 1. oktober 2004.");
        Assert.True(Disposisjonstyper.SkalKjoreInnholdsmonstre(tittel));
        var innhold = Disposisjonstyper.KlassifiserInnhold(
        [
            ("punkt-1", "Opprettelse av et Arbeids- og sosialdepartement, et Helse- og omsorgsdepartement og et Moderniseringsdepartement."),
            ("punkt-2", "Nedleggelse av Sosialdepartementet, Helsedepartementet og Arbeids- og administrasjonsdepartementet."),
            ("punkt-8", "Landbruksdepartementets navn endres til Landbruks- og matdepartementet."),
        ]);
        Assert.Equal(["oppretting", "nedleggelse", "navneendring"], innhold.Select(i => i.Disposisjonstype));
        Assert.Equal("punkt-8", innhold.Single(i => i.Disposisjonstype == "navneendring").NodeEid);
        Assert.All(innhold, i => Assert.Equal(Disposisjonstyper.KildeMonster, i.Kilde));
    }

    [Theory]
    [InlineData("LTI/forskrift/2004-10-01-1304", "kunngjort")]
    [InlineData("SF/forskrift/2005-06-08-538", "konsolidert")]
    [InlineData("DEL/forskrift/1991-06-28-4372", "konsolidert")]
    [InlineData("NL/lov/1989-06-02-27", "konsolidert")]
    [InlineData("XYZ/forskrift/2004-10-01-1", null)]
    [InlineData(null, null)]
    public void Kildeformen_leses_av_dokid_prefikset(string? dokid, string? forventet) =>
        Assert.Equal(forventet, RettskildeMetadata.AvledKildeform(dokid));

    [Fact]
    public void Lovtidend_fixturen_parses_av_samme_parser_med_hodemetadata()
    {
        var resultat = LovdataKonverterer.Konverter(Testdata.LesOmorganisering2004(), new DateOnly(2026, 10, 23));
        var m = resultat.Metadata;
        Assert.Equal("kunngjort", m.Kildeform);
        Assert.Equal("https://lovdata.no/eli/forskrift/2004/10/01/1304/nor", m.Eli);
        Assert.Equal(["Statsministerens kontor"], m.AnsvarligDepartement);
        Assert.StartsWith("Fastsatt ved kgl.res. 1. oktober 2004", m.AnnetOmDokumentet);
        Assert.Equal(new DateOnly(2004, 10, 1), m.Ikrafttredelse);
        Assert.Contains(resultat.Hjemler, h => h.Eid.Contains("lov/1814/05/17"));
        Assert.Single(resultat.Endringer); // changesToDocuments: forskrift/2004-06-18-916
    }

    // ---------------------------------------------------------------- Livsløpsmønstrene

    [Fact]
    public void Navnelister_deles_bare_paa_hele_departementsnavn()
    {
        var liste = DepartementLivslopMonstre.Navneliste(
            "et Arbeids- og sosialdepartement, et Helse- og omsorgsdepartement og et Moderniseringsdepartement.");
        Assert.Equal(["Arbeids- og sosialdepartementet", "Helse- og omsorgsdepartementet", "Moderniseringsdepartementet"], liste!.Select(n => n.Navn));
        Assert.Equal(["Barne-, likestillings- og inkluderingsdepartementet", "Kulturdepartementet"],
            DepartementLivslopMonstre.Navneliste("Barne-, likestillings- og inkluderingsdepartementet (BLD) og Kulturdepartementet (KUD).")!.Select(n => n.Navn));
        Assert.Null(DepartementLivslopMonstre.Navneliste("Statens reindriftsforvaltning og Landbruksdepartementet"));
    }

    [Theory]
    [InlineData("Landbruksdepartementets navn endres til Landbruks- og matdepartementet.", "Landbruksdepartementet", "Landbruks- og matdepartementet")]
    [InlineData("Justis- og politidepartementets navn endres fra 1. januar 2012 til Justis- og beredskapsdepartementet, forkortet som før til JD.", "Justis- og politidepartementet", "Justis- og beredskapsdepartementet")]
    [InlineData("Miljøverndepartementet skifter navn til Klima- og miljødepartementet (KLD).", "Miljøverndepartementet", "Klima- og miljødepartementet")]
    [InlineData("[...]Med virkning fra 1. januar 2024 endrer Olje- og energidepartementet navn til Energidepartementet. Departementet delegeres myndighet …", "Olje- og energidepartementet", "Energidepartementet")]
    public void Navnebytter_leses(string ledd, string fra, string til)
    {
        var h = Assert.Single(DepartementLivslopMonstre.LesLedd(ledd, out _));
        Assert.Equal(DepartementLivslopMonstre.Navnebytte, h.Type);
        Assert.Equal(fra, h.Fra!.Value.Navn);
        Assert.Equal(til, h.Til!.Value.Navn);
    }

    [Fact]
    public void Utsatt_navnebytte_merkes()
    {
        var h = Assert.Single(DepartementLivslopMonstre.LesLedd(
            "Fiskeridepartementet endrer navn til Fiskeri- og kystdepartementet, forkortet til FKD. Endringen iverksettes fra det tidspunkt Statsministerens kontor bestemmer.", out _));
        Assert.True(h.Utsatt);
    }

    [Fact]
    public void Skrivefeil_i_kilden_rettes_ikke()
    {
        // FOR-2013-12-13-1455 punkt 7 har «regionaldepartementel». Ingen hendelse, og leddet rapporteres.
        var h = DepartementLivslopMonstre.LesLedd("Kommunal- og regionaldepartementel skifter navn til Kommunal- og moderniseringsdepartementet (KMD).", out var ulest);
        Assert.Empty(h);
        Assert.NotNull(ulest);
    }

    [Theory]
    // Overskriftsformene i korpusets omorganiseringsvedtak (målt 2026-10-23).
    [InlineData("Vedlegg 1. Oversikt over lover Arbeidsdepartementet skal ha ansvaret for", "Arbeidsdepartementet")]
    [InlineData("Vedlegg 2. Oversikter over lover Utdannings- og forskningsdepartementet skal ha forvaltningsansvaret for:", "Utdannings- og forskningsdepartementet")]
    [InlineData("Vedlegg 5. Oversikt over lover som Kommunal- og moderniseringsdepartementet skal ha forvaltningsansvaret for", "Kommunal- og moderniseringsdepartementet")]
    [InlineData("Vedlegg. Oversikt over lover som Justis- og beredskapsdepartementet skal ha overført forvaltningsansvaret for", "Justis- og beredskapsdepartementet")]
    [InlineData("Oversikt over lover som Nærings- og fiskeridepartementet har forvaltningsansvar for etter endringene:", "Nærings- og fiskeridepartementet")]
    [InlineData("Lover Kommunal- og regionaldepartementet skal ha forvaltningsansvaret for:", "Kommunal- og regionaldepartementet")]
    [InlineData("Vedlegg 4. Overføring av forvaltningsansvaret for lover til Helse- og omsorgsdepartementet", "Helse- og omsorgsdepartementet")]
    [InlineData("Lover som overføres fra Nærings- og energidepartementet til Olje- og energidepartementet pr. 1. januar 1997:", "Olje- og energidepartementet")]
    public void Vedleggsoverskriftene_gir_departementet(string overskrift, string departement) =>
        Assert.Equal(departement, DepartementLivslopMonstre.LesVedleggsoverskrift(overskrift)!.Value.Navn);

    [Fact]
    public void Vedleggsoverskrift_uten_departement_gir_ingen_tildeling() =>
        Assert.Null(DepartementLivslopMonstre.LesVedleggsoverskrift("Overføring av forvaltningen av lover, myndighet, fullmakter, ansvar for utvalg, råd m.v."));

    [Fact]
    public void Overforing_og_vedlegg_gir_lovens_datokode()
    {
        var h = Assert.Single(DepartementLivslopMonstre.LesLedd(
            "Forvaltningsansvaret for lov 29. april 2005 nr. 20 om innkreving av underholdsbidrag mv. (bidragsinnkrevingsloven) overføres fra Arbeids- og sosialdepartementet til Finansdepartementet.", out _));
        Assert.Equal("Finansdepartementet", h.Til!.Value.Navn);
        Assert.Equal("LOV-2005-04-29-20", DepartementLivslopMonstre.LovDatokode(h.Objekt!));
        Assert.Equal("LOV-1925-07-17-11", DepartementLivslopMonstre.LovDatokode("Lov 17. juli 1925 nr. 11 om Svalbard, § 4"));
        Assert.Equal("LOV-1957-05-03", DepartementLivslopMonstre.LovDatokode("Lov 3. mai 1957 om pensjonering av fastlønt befal"));
        Assert.Null(DepartementLivslopMonstre.LovDatokode("Kirkelige anordninger i medhold av Grunnloven § 16"));
        var v = DepartementLivslopMonstre.LesVedleggsoverskrift("Vedlegg 2. Oversikt over lover Helse- og omsorgsdepartementet skal ha forvaltningsansvaret for:");
        Assert.Equal("Helse- og omsorgsdepartementet", v!.Value.Navn);
        Assert.True(v.Value.Funksjonsord);
    }
}
