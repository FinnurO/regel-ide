namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, Refs #486 PR 2] Ordlydsreglene for «statsråd» som ren funksjon. Spørsmålet modellen skal kunne besvare: hva er «statsråd» i denne
/// setningen: organet statsrådet, Kongen i statsråd eller rollen? Eksemplene er fra korpuset (juristrunden i data/fasit/strukturmodell/statsrad/).
/// Ordet i hakeparentes er forekomsten.
/// </summary>
public class StatsradOrdlydsreglerTests
{
    private static StatsradTreff? Les(string tekst, string? tittel = null, bool konvensjon = false)
    {
        var start = tekst.IndexOf('[');
        var slutt = tekst.IndexOf(']');
        var ren = tekst.Remove(slutt, 1).Remove(start, 1);
        return StatsradOrdlydsregler.Vurder(ren, start, slutt - 1, [tittel ?? "Testlov", null], konvensjon);
    }

    [Theory]
    [InlineData("oppnevnt av Kongen i [statsråd], jf. kapittel 5")]
    [InlineData("For Kongen i [statsråds] myndighet til å utsette")]
    public void Kongen_i_statsrad_tagges_ikke_her(string tekst)
    {
        var t = Les(tekst);
        Assert.Equal((null, StatsradOrdlydsregler.DekketKongenIStatsrad), (t?.Lesning, t?.Regel));
    }

    [Theory]
    [InlineData("Dog kan egentlige militære kommandosaker, i den utstrekning som kongen bestemmer, unntas fra behandling i [statsråd].")]
    [InlineData("Ved oppnevninger i [statsråd] skal det, dersom utvalget ikke tilfredsstiller")]
    [InlineData("Det ble i [statsråd] 16. april 2010 vedtatt at fullmaktene i lov")]
    [InlineData("Dette gjelder likevel ikke saker om utnevning av dommere i [statsråd].")]
    [InlineData("gjelder ikke for avgjørelser truffet i [statsråd].")]
    public void I_statsrad_med_beslutningsord_er_Kongen_i_statsrad(string tekst) =>
        Assert.Equal(Forekomstlesninger.KongenIStatsrad, Les(tekst)?.Lesning);

    [Fact]
    public void I_statsrad_i_en_kilde_om_krig_er_slutning()
    {
        // Suksesjonsforskriften § 5 og § 6: kongen kan være borte, og statsrådet kan beslutte (juristrunden q5).
        const string tittel = "Midlertidig suksesjon for regjeringens medlemmer når riket er i krig eller krig truer m.m.";
        Assert.Null(Les("Statsrådet skal formalisere fungeringen som statsråd gjennom vedtak i [statsråd].", tittel));
        Assert.Null(Les("og det ikke er tid til å treffe vedtak i [statsråd], kan statsministeren", tittel));
        Assert.Contains("krig", StatsradOrdlydsregler.Slutningsgrunn("vedtak i statsråd", 9, 17, [tittel]));
    }

    [Theory]
    [InlineData("Er en [statsråd] funksjonsudyktig", StatsradOrdlydsregler.RolleRolleord)]
    [InlineData("mistillit til vedkommende [statsråd] alene", StatsradOrdlydsregler.RolleRolleord)]
    [InlineData("skal den ansvarlige [statsråd] varsles", StatsradOrdlydsregler.RolleRolleord)]
    [InlineData("vurdering av den enkelte [statsråds] forvaltning", StatsradOrdlydsregler.RolleRolleord)]
    [InlineData("tre inn som statsminister eller [statsråd] etter regelverket", StatsradOrdlydsregler.RolleSideordning)]
    [InlineData("mellom embetsverk og [statsråd]. Møteleder", StatsradOrdlydsregler.RolleSideordning)]
    [InlineData("Nkom og Datatilsynet. [Statsråd] Astrup har videre", StatsradOrdlydsregler.RolleTittel)]
    [InlineData("Ocean Space Centre fra [statsråd] Jan Christian Vestre", StatsradOrdlydsregler.RolleTittel)]
    [InlineData("Etter at [statsråden] har svart", StatsradOrdlydsregler.RolleBestemtFlertall)]
    [InlineData("forelegges berørte [statsråder] til uttalelse", StatsradOrdlydsregler.RolleBestemtFlertall)]
    [InlineData("statssekretærer og [statsrådenes] politiske rådgivere", StatsradOrdlydsregler.RolleBestemtFlertall)]
    public void Rollen(string tekst, string regel)
    {
        var t = Les(tekst);
        Assert.Equal((Forekomstlesninger.RolleStatsrad, regel), (t?.Lesning, t?.Regel));
    }

    [Theory]
    [InlineData("mistillit til vedkommende statsråd alene eller til det samlede [statsråd].", StatsradOrdlydsregler.OrganIntetkjonn)]
    [InlineData("Kongen velger og beskikker, etter å ha hørt sitt [statsråd], alle sivile", StatsradOrdlydsregler.OrganIntetkjonn)]
    [InlineData("Enhver som er medlem av [statsrådet], har plikt", StatsradOrdlydsregler.OrganStatsradet)]
    [InlineData("Til å ta sete i [statsrådet] kan kongen", StatsradOrdlydsregler.OrganStatsradet)]
    [InlineData("Kongen fordeler forretningene blant [statsrådets] medlemmer", StatsradOrdlydsregler.OrganStatsradet)]
    public void Organet(string tekst, string regel)
    {
        var t = Les(tekst);
        Assert.Equal((Forekomstlesninger.Statsradet, regel), (t?.Lesning, t?.Regel));
    }

    [Theory]
    [InlineData("Kongen har rett til i [statsrådet] å benåde forbrytere")]
    [InlineData("under utførelsen av deres gjøremål utenfor [statsrådet].")]
    [InlineData("til avholdelse av beslutningsdyktig [statsråd].")]
    [InlineData("Protokollen føres særskilt for hvert [statsråd].")]
    [InlineData("Bestemmelsene gjelder ikke [statsråd] i egenskap av regjeringsmedlem.")]
    public void Slutning(string tekst) => Assert.Null(Les(tekst));

    [Fact]
    public void Lovtittel_er_ikke_aktor()
    {
        var t = Les("etter tidligere lov 14. desember 1951 nr. 11 om pensjonsordning for [statsråder].");
        Assert.Equal((Forekomstlesninger.IkkeAktor, StatsradOrdlydsregler.IkkeAktorLovtittel), (t?.Lesning, t?.Regel));
        // Ikke en tittel: «pensjonsordningen for statsråder» er rollen.
        Assert.Equal(Forekomstlesninger.RolleStatsrad, Les("opptjeningstid fra pensjonsordningen for [statsråder] skal")?.Lesning);
    }

    [Fact]
    public void Konvensjonstekst_er_generisk_konvensjonsstat() =>
        Assert.Equal(Forekomstlesninger.GeneriskKonvensjonsstat,
            Les("av regjeringen eller av vedkommende [statsråd] eller departement (ministerium) i virksomhetslandet", konvensjon: true)?.Lesning);

    [Theory]
    [InlineData("statsrådsprotokollen", 0)]
    [InlineData("vedkommende fagstatsråds ansvar", 0)]
    [InlineData("nærings-statsråd", 0)]
    [InlineData("Statsråden og statsrådene, statsrådets og statsråds", 4)]
    public void Ordet_er_hele_ord_uten_sammensetninger(string tekst, int antall) =>
        Assert.Equal(antall, StatsradOrdlydsregler.Ordet.Matches(tekst).Count);
}
