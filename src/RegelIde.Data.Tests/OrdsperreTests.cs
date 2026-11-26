namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #463] Ordsperren for «staten» som ren funksjon. Spørsmålet modellen skal kunne besvare: betyr «staten» her den norske staten?
/// Sperren gjelder ikke «den (adj.) staten» (men «den norske staten» gjelder), «staten der/hvor/hvis», konvensjonstekst og bindestrek rett foran.
/// </summary>
public class OrdsperreTests
{
    private static string? Vurder(string tekst, bool konvensjon = false)
    {
        var start = tekst.IndexOf("[staten]", StringComparison.Ordinal);
        var ren = tekst.Replace("[staten]", "staten");
        if (start < 0)
        {
            start = tekst.IndexOf("[Staten]", StringComparison.Ordinal);
            ren = tekst.Replace("[Staten]", "Staten");
        }
        return Ordsperrer.Vurder(Ordsperrer.Staten, ren, start, start + "staten".Length, konvensjon);
    }

    [Theory]
    [InlineData("Søksmål mot [staten] rettes mot departementet.")]
    [InlineData("Motorvogn som høyrer den norske [staten] til, treng ikkje vera trygda.")]
    [InlineData("i den utstrekning denne [staten] har gitt samtykke")]
    [InlineData("avgiften tilfaller [staten].")]
    [InlineData("[Staten] dekker utgiftene.")]
    [InlineData("i denne [staten]")] // Johann 2026-11-18: sperres ikke
    // Johann 2026-11-18: det betingede «hvis» sperrer ikke (de tre om den norske staten i korpuset).
    [InlineData("rett til erstatning av [staten] hvis tapet skyldes feil")]
    [InlineData("dekkes av [staten] hvis den unge ikke har rett til fri reise")]
    public void Gjelder(string tekst) => Assert.Null(Vurder(tekst));

    [Theory]
    [InlineData("myndigheten i den anmodede [staten] skal", Ordsperrer.DenAdjektiv)]
    [InlineData("i den andre [staten]", Ordsperrer.DenAdjektiv)]
    [InlineData("den særlig berørte [staten]", Ordsperrer.DenAdjektiv)]
    [InlineData("skifte i den [staten] der han hadde sitt bosted", Ordsperrer.DenAdjektiv)]
    [InlineData("Den [staten] der fartøyet er registrert", Ordsperrer.DenAdjektiv)]
    [InlineData("reise til [staten] hvor de er bosatt", Ordsperrer.DerHvorHvis)]
    [InlineData("regjeringen i [staten] hvis flagg skipet fører", Ordsperrer.DerHvorHvis)]
    [InlineData("navn på [staten] hvis lovgivning gjelder", Ordsperrer.DerHvorHvis)]
    [InlineData("myndigheten i EØS-[staten]", Ordsperrer.BindestrekForan)]
    public void Sperret(string tekst, string grunn) => Assert.Equal(grunn, Vurder(tekst));

    [Theory]
    [InlineData(" hvis flagg skipet fører", true)]
    [InlineData(" hvis lovgivning gjelder", true)]
    [InlineData(" hvis tapet skyldes", false)]
    [InlineData(" hvis den unge", false)]
    [InlineData(" hvis det er nødvendig", false)]
    [InlineData(" hvis myndighetene samtykker", false)]
    [InlineData(" der han bor", false)]
    public void Possessivt_hvis(string etter, bool forventet) => Assert.Equal(forventet, Ordsperrer.ErPossessivtHvis(etter));

    [Fact]
    public void Konvensjonstekst_sperrer_bare_det_som_ellers_gjelder()
    {
        Assert.Equal(Ordsperrer.Konvensjonstekst, Vurder("[staten] skal utpeke en sentralmyndighet", konvensjon: true));
        Assert.Equal(Ordsperrer.DenAdjektiv, Vurder("den anmodede [staten] skal", konvensjon: true));
    }

    [Fact]
    public void Andre_ord_har_ingen_sperre()
    {
        Assert.False(Ordsperrer.HarSperre("departementet"));
        Assert.Null(Ordsperrer.Vurder("departementet", "den andre departementet", 10, 23, true));
        Assert.True(Ordsperrer.HarSperre("Staten"));
    }

    [Theory]
    [InlineData("Konvensjon av 25 oktober 1980 om de sivile sider ved internasjonal barnebortføring.", true)]
    [InlineData("Vedlegg 19. Avtale mellom Kongeriket Norges regjering og Den Russiske Føderasjons regjering om forenkling", true)]
    [InlineData("Europeisk konvensjon av 20 mai 1980 om anerkjennelse og fullbyrding av avgjørelser om foreldreansvar", true)]
    [InlineData("Hongkong-konvensjonen", true)]
    [InlineData("Vedlegg 2. 2002 protokollen til Aten-konvensjonen om befordring av passasjerer", true)]
    [InlineData("EØS-avtalen vedlegg V punkt 2 (forordning (EU) nr. 492/2011)", true)]
    [InlineData("Konvensjon mellem Norge, Danmark, Finnland, Island og Sverige om arv og dødsboskifte", true)]
    // [Ny, Refs #486, juristrunden a5] Et sammensatt ord på «konvensjon» (nordisk miljøvernkonvensjon, art. 11 «vedkommende statsråd»).
    [InlineData("Miljøvernkonvensjon av 19. februar 1974, mellom Danmark, Finland, Norge og Sverige.", true)]
    [InlineData("Avtalebrudd. Arbeidsstans.", false)]
    [InlineData("Tariffavtaler.", false)]
    [InlineData("Overenskomst med fremmed stat.", false)]
    [InlineData("Sertifikat etter 1992 ansvarskonvensjonen, 2001 bunkerskonvensjonen", false)]
    [InlineData("Ansvar og erstatning etter reglene i den internasjonale konvensjon 1992", false)]
    [InlineData(null, false)]
    public void Konvensjonsoverskrift(string? overskrift, bool forventet) => Assert.Equal(forventet, Ordsperrer.ErKonvensjonsoverskrift(overskrift));

    /// <summary>[Ny, Refs #486] Ordet i hakeparentes i teksten, vurdert mot <paramref name="term"/>.</summary>
    private static string? VurderOrd(string term, string tekst, bool konvensjon = false)
    {
        var start = tekst.IndexOf('[');
        var slutt = tekst.IndexOf(']');
        var ren = tekst.Remove(slutt, 1).Remove(start, 1);
        return Ordsperrer.Vurder(term, ren, start, slutt - 1, konvensjon);
    }

    /// <summary>[Ny, Refs #486] «Betyr ‹statsråd› her rollen?» Ja, med hankjønn, rolleord, tittel foran navn eller sideordning; nei etter «i»,
    /// med intetkjønn foran og om møtet. Eksemplene er fra korpuset (juristrunden i data/fasit/strukturmodell/statsrad/).</summary>
    [Theory]
    [InlineData("beslutning om mistillit til vedkommende [statsråd] alene")]
    [InlineData("Er en [statsråd] funksjonsudyktig, fungerer den statssekretæren")]
    [InlineData("skal den ansvarlige [statsråd] varsles")]
    [InlineData("Den enkelte [statsråd] har det konstitusjonelle ansvaret")]
    [InlineData("tilligger [statsråd] Kallmyr. [Statsråd] Kallmyr og")]
    [InlineData("ansvaret for prosjektet Ocean Space Centre fra [statsråd] Jan Christian Vestre")]
    [InlineData("kan ikke tre inn som statsminister eller [statsråd] etter regelverket her.")]
    public void Statsrad_er_rollen(string tekst) => Assert.Null(VurderOrd(Ordsperrer.Statsrad, tekst));

    [Theory]
    [InlineData("oppnevnt av Kongen i [statsråd], jf. kapittel 5", Ordsperrer.IForan)]
    [InlineData("det ikke er tid til å treffe vedtak i [statsråd], kan statsministeren", Ordsperrer.IForan)]
    [InlineData("unntas fra behandling i [statsråd].", Ordsperrer.IForan)]
    [InlineData("Kongen i [Statsråd] kan", Ordsperrer.IForan)]
    [InlineData("mistillit til vedkommende statsråd alene eller til det samlede [statsråd].", Ordsperrer.Intetkjonn)]
    [InlineData("etter å ha hørt sitt [statsråd], alle sivile", Ordsperrer.Intetkjonn)]
    [InlineData("at riket til enhver tid har et [statsråd], en statsminister", Ordsperrer.Intetkjonn)]
    [InlineData("inntil et beslutningsdyktig [statsråd] skulle bestemme", Ordsperrer.Motet)]
    [InlineData("til avholdelse av beslutningsdyktig [statsråd].", Ordsperrer.Motet)]
    [InlineData("Protokollen føres særskilt for hvert [statsråd].", Ordsperrer.Motet)]
    [InlineData("samtidig som saken sendes til forberedende [statsråd].", Ordsperrer.Motet)]
    [InlineData("Dersom en sak som er sendt, trekkes fra [statsråd], bes melding gitt", Ordsperrer.Motet)]
    public void Statsrad_er_ikke_rollen(string tekst, string grunn) => Assert.Equal(grunn, VurderOrd(Ordsperrer.Statsrad, tekst));

    [Fact]
    public void Statsrad_i_konvensjonstekst_er_ikke_rollen() =>
        Assert.Equal(Ordsperrer.Konvensjonstekst,
            VurderOrd(Ordsperrer.Statsrad, "av regjeringen eller av vedkommende [statsråd] eller departement (ministerium) i virksomhetslandet", true));

    /// <summary>[Ny, Refs #486] «Betyr ‹statsrådet› her organet?» Ja ved medlemskap og sete; nei når det er møtet («i statsrådet», «utenfor
    /// statsrådet»). Kjent pris: Grl. § 27 og Regjeringsinstruksen § 1 sperres (juristrunden a2).</summary>
    [Theory]
    [InlineData("Enhver som er medlem av [statsrådet], har plikt")]
    [InlineData("er han eller hun berettiget til å ta sete i [statsrådet], dog uten stemme")]
    [InlineData("Far og sønn eller to søsken må ikke på samme tid ha sete i [statsrådet].")]
    [InlineData("I motsatt fall forestår [statsrådet] bestyrelsen av riket.")]
    public void Statsradet_er_organet(string tekst) => Assert.Null(VurderOrd(Ordsperrer.Statsradet, tekst));

    [Theory]
    [InlineData("Kongen har rett til i [statsrådet] å benåde forbrytere", Ordsperrer.IForan)]
    [InlineData("I [statsrådet], som består av så mange medlemmer", Ordsperrer.IForan)]
    [InlineData("under utførelsen av deres gjøremål utenfor [statsrådet].", Ordsperrer.UtenforForan)]
    public void Statsradet_om_motet_er_ikke_organet(string tekst, string grunn) => Assert.Equal(grunn, VurderOrd(Ordsperrer.Statsradet, tekst));

    [Fact]
    public void Sammensetninger_og_bindestrek_er_ikke_treff()
    {
        Assert.DoesNotMatch(AktoridentitetTjeneste.HeleOrd("statsråd", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "statsrådsprotokollen");
        Assert.DoesNotMatch(AktoridentitetTjeneste.HeleOrd("statsråd", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "vedkommende fagstatsråds");
        Assert.Equal(Ordsperrer.BindestrekForan, VurderOrd(Ordsperrer.Statsrad, "nærings-[statsråd]"));
        Assert.True(Ordsperrer.HarSperre("Statsråd"));
        Assert.True(Ordsperrer.HarSperre("statsrådet"));
        Assert.False(Ordsperrer.HarSperre("statsråden"));
    }
}
