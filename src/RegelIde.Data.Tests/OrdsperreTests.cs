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
    [InlineData("i denne [staten]")] // ikke med i notatet; telles i målingen
    public void Gjelder(string tekst) => Assert.Null(Vurder(tekst));

    [Theory]
    [InlineData("myndigheten i den anmodede [staten] skal", Ordsperrer.DenAdjektiv)]
    [InlineData("i den andre [staten]", Ordsperrer.DenAdjektiv)]
    [InlineData("den særlig berørte [staten]", Ordsperrer.DenAdjektiv)]
    [InlineData("skifte i den [staten] der han hadde sitt bosted", Ordsperrer.DenAdjektiv)]
    [InlineData("Den [staten] der fartøyet er registrert", Ordsperrer.DenAdjektiv)]
    [InlineData("reise til [staten] hvor de er bosatt", Ordsperrer.DerHvorHvis)]
    [InlineData("regjeringen i [staten] hvis flagg skipet fører", Ordsperrer.DerHvorHvis)]
    [InlineData("myndigheten i EØS-[staten]", Ordsperrer.BindestrekForan)]
    public void Sperret(string tekst, string grunn) => Assert.Equal(grunn, Vurder(tekst));

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
    [InlineData("Avtalebrudd. Arbeidsstans.", false)]
    [InlineData("Tariffavtaler.", false)]
    [InlineData("Overenskomst med fremmed stat.", false)]
    [InlineData("Sertifikat etter 1992 ansvarskonvensjonen, 2001 bunkerskonvensjonen", false)]
    [InlineData("Ansvar og erstatning etter reglene i den internasjonale konvensjon 1992", false)]
    [InlineData(null, false)]
    public void Konvensjonsoverskrift(string? overskrift, bool forventet) => Assert.Equal(forventet, Ordsperrer.ErKonvensjonsoverskrift(overskrift));
}
