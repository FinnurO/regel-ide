namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #486] Ordlydsreglene for «regjeringen» (<see cref="RegjeringenOrdlydsregler"/>), uten base. Eksemplene er setninger fra korpuset
/// (målt i delt <c>regelide</c> 2026-11-20), forkortet. Spørsmålet reglene skal besvare: hva er «regjeringen» i denne forekomsten — og når
/// avgjør ordlyden det ikke (slutning, ingen gjetting)?
/// </summary>
public class RegjeringenOrdlydsreglerTests
{
    private static RegjeringenTreff? Les(string tekst, int forekomst = 0, string tittel = "Testlov")
    {
        var m = RegjeringenOrdlydsregler.Ordet.Matches(tekst)[forekomst];
        return RegjeringenOrdlydsregler.Vurder(tekst, m.Index, m.Index + m.Length, [tittel, null]);
    }

    [Theory]
    // 1. Fremmed stat
    [InlineData("Avtale mellom regjeringen i Kongeriket Norge og regjeringen i Republikken Ungarn om medisinsk stønad", 1, "fremmed_stat", "fremmed:regjeringen_i_stat")]
    [InlineData("herunder korrespondanse med regjeringen i tredjestaten, med hensyn til nevnte tredjestat", 0, "fremmed_stat", "fremmed:regjeringen_i_stat")]
    [InlineData("andre syriske personer, enheter eller organer, herunder den syriske regjeringen,", 0, "fremmed_stat", "fremmed:nasjonalitet")]
    [InlineData("til direkte eller indirekte fordel for Belarus' regjeringen eller til en militær sluttbruker", 0, "fremmed_stat", "fremmed:genitiv")]
    [InlineData("Regjeringen for Det Forente Kongerike kan, ved underretning gitt ad diplomatisk vei", 0, "fremmed_stat", "fremmed:regjeringen_for")]
    // 2. Generisk konvensjonsstat
    [InlineData("Administrasjon betyr regjeringen i den staten hvis flagg fartøyet fører.", 0, "generisk_konvensjonsstat", "generisk:konvensjonsstat")]
    [InlineData("etter anmodning fra myndigheten, av regjeringen i en annen stat som er part i konvensjonen", 0, "generisk_konvensjonsstat", "generisk:konvensjonsstat")]
    [InlineData("I tilfelle som nevnt i artikkel 11 kan regjeringen i hver av statene kreve at en kommisjon", 0, "generisk_konvensjonsstat", "generisk:konvensjonsstat")]
    [InlineData("skal den umiddelbart underrette regjeringen i medlemsstaten der foretaket har sitt hovedkontor", 0, "generisk_konvensjonsstat", "generisk:konvensjonsstat")]
    [InlineData("skal så snart som mulig oversendes den regjeringen som har anmodet om utstedelsen.", 0, "generisk_konvensjonsstat", "generisk:konvensjonsstat")]
    [InlineData("Dette sertifikatet er utstedt etter fullmakt fra regjeringen i ... ... .. (statens offisielle betegnelse)", 0, "generisk_konvensjonsstat", "generisk:skjemamal")]
    // 3. Ikke aktør
    [InlineData("Programkoden som skal brukes, er offentlig tilgjengelig på regjeringen.no.", 0, "ikke_aktor", "ikke_aktor:nettsted")]
    [InlineData("Avtale mellom regjeringen i Kongeriket Norge og regjeringen i Australia om trygd", 0, "ikke_aktor", "ikke_aktor:avtaletittel")]
    [InlineData("Regjeringens sikkerhetsutvalg (RSU) er det øverste organet for å diskutere sikkerhetssaker", 0, "ikke_aktor", "ikke_aktor:navn")]
    [InlineData("instruks for Regjeringens presse- og informasjonstjeneste (RPI)", 0, "ikke_aktor", "ikke_aktor:navn")]
    // 4. Rollen statsråd, også i oppramsing
    [InlineData("medlemmer av regjeringen", 0, "rolle_statsrad", "rolle:medlem_av")]
    [InlineData("Medlemmer av Stortinget og regjeringen som må oppholde seg utenfor registrert bosted", 0, "rolle_statsrad", "rolle:medlem_av")]
    [InlineData("og som retter seg mot medlemmer av Kongehuset, Stortinget, regjeringen, Høyesterett eller representanter", 0, "rolle_statsrad", "rolle:medlem_av")]
    [InlineData("gjerningspersonen er et medlem av regjeringen, Stortinget eller Høyesterett", 0, "rolle_statsrad", "rolle:medlem_av")]
    [InlineData("gjelder ikke for stortingsrepresentanter, regjeringens medlemmer og dommere i Høyesterett.", 0, "rolle_statsrad", "rolle:regjeringens_medlemmer")]
    [InlineData("Dersom halvparten eller mindre enn halvparten av regjeringens ordinære medlemmer er til stede", 0, "rolle_statsrad", "rolle:regjeringens_medlemmer")]
    // 5. Funksjonen
    [InlineData("Dette skal føre regjeringen i kongens navn og på hans vegne.", 0, "funksjon", "funksjon:styret_av_riket")]
    [InlineData("Så snart kongen, som myndig, tiltrer regjeringen, avlegger han følgende ed", 0, "funksjon", "funksjon:styret_av_riket")]
    [InlineData("Inntil Stortinget er samlet og har anordnet regjeringen under kongens mindreårighet", 0, "funksjon", "funksjon:styret_av_riket")]
    // 6. «Regjeringens res.» = Kongen i statsråd
    [InlineData("Regjeringens res. av 5. november 1999 nr. 1145 om Kystvakten (kystvaktinstruksen)", 0, "regjeringens_resolusjon", "resolusjon:grl_32")]
    [InlineData("Sikkerhetsrådet for luftfarten, opprettet ved regjeringens resolusjon 4. juli 1991, skal konsulteres", 0, "regjeringens_resolusjon", "resolusjon:grl_32")]
    public void Regelen_leser_forekomsten(string tekst, int forekomst, string lesning, string regel)
    {
        var t = Les(tekst, forekomst);
        Assert.NotNull(t);
        Assert.Equal(lesning, t!.Lesning);
        Assert.Equal(regel, t.Regel);
    }

    [Theory]
    // Plikten til å legge fram for Stortinget er ikke avgjort (Johann 2026-11-20): slutning, ingen regel.
    [InlineData("skal regjeringen i 2020 og deretter hvert femte år legge fram for Stortinget oppdatert klimamål")]
    [InlineData("Regjeringen skal meddele Stortinget alle de opplysninger som er nødvendige")]
    // r14: statsråder, statssekretærer eller politiske rådgivere «i regjeringen» er ikke «medlemmer av regjeringen».
    [InlineData("stortingsrepresentanter som er utnevnt til statsråder, statssekretærer eller politiske rådgivere i regjeringen")]
    // Organet i en oppramsing uten «medlem av» (straffeloven § 115).
    [InlineData("volder fare for at Kongen, Regenten, regjeringen, Stortinget, Høyesterett eller Riksretten")]
    // Navneregelen er ikke et prefiks (juristens retting av R).
    [InlineData("1. Regjeringens departementer er indtil videre:")]
    [InlineData("Vegdirektøren er Regjeringens/Samferdselsdepartementets og Forsvarsjefens rådgiver")]
    // «regjeringen i Kongeriket Norge» utenfor en avtaletittel er ikke fremmed.
    [InlineData("Den myndighet som er tillagt regjeringen i Kongeriket Norge etter protokollen")]
    // «den norske regjeringen» er ikke fremmed.
    [InlineData("etter samråd med den norske regjeringen")]
    public void Ordlyden_avgjor_ikke_og_det_blir_slutning(string tekst) => Assert.Null(Les(tekst));

    [Fact]
    public void Sanksjonsforskrifter_avgrenses_ved_tittel_og_den_russiske_resolusjonen_er_fremmed_ikke_Kongen_i_statsrad()
    {
        const string tekst = "Den russiske føderasjons regjerings dekret nr. 1767, endret ved regjeringens resolusjon nr. 380 av 27. mars 2024";
        var m = RegjeringenOrdlydsregler.Ordet.Match(tekst);
        var sanksjon = RegjeringenOrdlydsregler.Vurder(tekst, m.Index, m.Index + m.Length, ["Sanksjonsforskrift Ukraina (territoriell integritet mv.)"]);
        Assert.Equal(Forekomstlesninger.FremmedStat, sanksjon!.Lesning);
        Assert.Equal(RegjeringenOrdlydsregler.FremmedSanksjonstittel, sanksjon.Regel);
        // Uten sanksjonstittel ville regel 6 slått til: rekkefølgen er det som skiller dem.
        var annen = RegjeringenOrdlydsregler.Vurder(tekst, m.Index, m.Index + m.Length, ["Testforskrift"]);
        Assert.Equal(Forekomstlesninger.RegjeringensResolusjon, annen!.Lesning);
        // [Juristmålingen m1] Ordlyden går foran tittelen: «den syriske regjeringen» er et tekstfunn også i en sanksjonsforskrift.
        const string syria = "andre syriske personer, enheter eller organer, herunder den syriske regjeringen,";
        var s = RegjeringenOrdlydsregler.Ordet.Match(syria);
        Assert.Equal(RegjeringenOrdlydsregler.FremmedNasjonalitet,
            RegjeringenOrdlydsregler.Vurder(syria, s.Index, s.Index + s.Length, ["Forskrift om restriktive tiltak mot Syria"])!.Regel);
    }

    [Fact]
    public void Ordet_er_regjeringen_og_regjeringens_som_hele_ord_uten_bindestrek_foran()
    {
        var treff = RegjeringenOrdlydsregler.Ordet.Matches("regjeringen, Regjeringens, regjeringene, ny-regjeringen, regjeringen.no")
            .Select(m => m.Value).ToList();
        Assert.Equal(["regjeringen", "Regjeringens", "regjeringen"], treff);
    }

    [Fact]
    public void Signalord_er_bare_opplysning()
    {
        const string tekst = "For prosjekter over terskelverdi skal oppdragsdepartementet fremme sak for regjeringen om OFP.";
        var m = RegjeringenOrdlydsregler.Ordet.Match(tekst);
        Assert.Null(RegjeringenOrdlydsregler.Vurder(tekst, m.Index, m.Index + m.Length, ["Instruks"]));
        Assert.Contains(RegjeringenOrdlydsregler.Signal(tekst, m.Index, m.Index + m.Length), s => s.Contains("fremme sak for"));
    }
}
