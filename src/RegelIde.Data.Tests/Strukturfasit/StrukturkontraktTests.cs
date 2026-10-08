using System.Text.RegularExpressions;
using RegelIde.Data.Strukturkonvertering;

namespace RegelIde.Data.Tests.Strukturfasit;

/// <summary>
/// [Ny, #308 strukturmodell-KI, 2026-10-07] Vakt mot at <see cref="Strukturkontrakt"/> (det KI-laget
/// instrueres med og validerer mot) drifter fra FORMAT.md (kontrakten, docs/33 §5.1). Leser lista
/// «Typer per kategori» i FORMAT.md og krever nøyaktig samme typer per kategori — en ny type der skal
/// ikke stille bli avvist av valideringen.
/// </summary>
public class StrukturkontraktTests
{
    [Fact]
    public void Typer_per_kategori_er_de_samme_som_i_FORMAT_md()
    {
        var linjer = File.ReadAllLines(Path.Combine(StrukturfasitLeser.FasitMappe, "FORMAT.md"));
        var start = Array.FindIndex(linjer, l => l.StartsWith("Typer per kategori", StringComparison.Ordinal));
        Assert.True(start >= 0, "Fant ikke «Typer per kategori» i FORMAT.md.");

        var format = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        List<string>? gjeldende = null;
        foreach (var linje in linjer.Skip(start + 1).TakeWhile(l => !l.StartsWith("#", StringComparison.Ordinal)))
        {
            // To former i FORMAT.md: «- relasjon (aktør→aktør): …» og «- konstituerende: …».
            var kategori = Regex.Match(linje, @"^- (\w+)(?: \(|:)");
            if (kategori.Success) format[kategori.Groups[1].Value] = gjeldende = [];
            if (gjeldende is null) continue;
            gjeldende.AddRange(Regex.Matches(linje, @"`(\w+)`").Select(m => m.Groups[1].Value));
        }

        Assert.Equal(format.Keys.Order(), Strukturkontrakt.TyperPerKategori.Keys.Order());
        foreach (var (kategori, typer) in format)
        {
            Assert.Equal(typer, Strukturkontrakt.TyperPerKategori[kategori]);
        }
    }

    [Theory]
    [InlineData("kompetanse", "normgivningskompetanse", true)]
    [InlineData("kompetanse", "forskriftskompetanse", false)] // [ENDRET, #341] er normgivning med normform forskrift
    [InlineData("kompetanse", "ukjent", true)]
    [InlineData("relasjon", "klageinstans_for", false)] // [ENDRET, #341] er klagekompetanse med motpart
    [InlineData("relasjon", "har_delegert_til", true)]
    [InlineData("relasjon", "velger", false)] // [Ny, #352] er oppnevningskompetanse med undertype valg
    [InlineData("kompetanse", "utpekingskompetanse", false)] // [Ny, #352] er oppnevningskompetanse med undertype utpeking
    [InlineData("kompetanse", "ansettelseskompetanse", false)] // [Ny, #352] er oppnevningskompetanse med undertype ansettelse
    [InlineData("relasjon", "radgir", true)] // [Ny, #352] Johanns beslutning 4: struktur
    [InlineData("relasjon", "del_av", true)]
    [InlineData("sammensetning_omrade", "del_av", true)]
    [InlineData("kompetanse", "annet:klageordning", true)]
    [InlineData("annet:klage", "annet:klageordning", true)]
    [InlineData("annet:klage", "normgivningskompetanse", false)]
    [InlineData("kompetanse", "bestar_av", false)]
    [InlineData("organsammensetning", "ledes_av", false)]
    [InlineData("kompetanse", "annet:", false)]
    [InlineData("kompetanse", "annet:Stor Bokstav", false)]
    public void Type_er_gyldig_bare_for_sin_kategori_eller_som_annet(string kategori, string type, bool gyldig)
    {
        Assert.Equal(gyldig, Strukturkontrakt.ErGyldigType(kategori, type));
    }

    [Fact]
    public void Systeminstruksen_lister_alle_typene_valideringen_godtar()
    {
        foreach (var type in Strukturkontrakt.TyperPerKategori.Values.SelectMany(t => t))
        {
            Assert.Contains(type, KiStrukturkonverterer.SystemInstruks);
        }
    }
}
