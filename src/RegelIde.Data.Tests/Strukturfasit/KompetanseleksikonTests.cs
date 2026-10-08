using RegelIde.Data.Strukturkonvertering;

namespace RegelIde.Data.Tests.Strukturfasit;

/// <summary>
/// [Ny, issue #341, Johanns beslutning 3 2026-10-08] Leksikonet styrer mønsterlaget: hvert mønster har nøyaktig én regel og
/// omvendt, typene er gyldige i kontrakten (FORMAT.md), familien er typens, og normform står bare på normgivning. KI-instruksen
/// lister uttrykkene (KI skal bare foreslå for det leksikonet IKKE kjenner). Ingen database.
/// </summary>
public class KompetanseleksikonTests
{
    [Fact]
    public void Hvert_monster_har_en_regel_og_hver_regel_et_monster()
    {
        var monstre = MonsterStrukturkonverterer.Monstre.Select(m => m.Id).ToList();
        var regler = Kompetanseleksikon.Regler.Select(r => r.Id).ToList();
        Assert.Equal(regler.Count, regler.Distinct().Count());
        Assert.Equal(monstre.Order(), regler.Order());
        Assert.False(string.IsNullOrWhiteSpace(Kompetanseleksikon.Versjon));
    }

    [Fact]
    public void Monstrene_far_kategori_type_og_normform_fra_leksikonet()
    {
        foreach (var m in MonsterStrukturkonverterer.Monstre)
        {
            var r = Kompetanseleksikon.For(m.Id);
            Assert.Equal((r.Kategori, r.Type, r.Normform), (m.Kategori, m.Type, m.Normform));
        }
    }

    [Fact]
    public void Reglene_er_gyldige_i_kontrakten_og_familien_er_typens()
    {
        foreach (var r in Kompetanseleksikon.Regler)
        {
            Assert.True(Strukturkontrakt.ErGyldigType(r.Kategori, r.Type), $"{r.Id}: {r.Kategori}/{r.Type}");
            Assert.NotEmpty(r.Uttrykk);
            if (r.Normform is not null)
            {
                Assert.Equal("normgivningskompetanse", r.Type);
                Assert.Contains(r.Normform, Strukturkontrakt.Normformer);
            }
            if (r.Kategori == "kompetanse")
            {
                var type = Assert.Single(Strukturkanter.Kompetansetyper, t => t.FasitType == r.Type);
                Assert.Equal(type.Familie, r.Familie);
            }
            else
            {
                Assert.Null(r.Familie);
            }
        }
        // Johanns eksempler i beslutning 3.
        Assert.Equal(("normgivningskompetanse", "forskrift"), (Kompetanseleksikon.For("forskrift-gi").Type, Kompetanseleksikon.For("forskrift-gi").Normform));
        Assert.Equal("klagekompetanse", Kompetanseleksikon.For("klage-er-klageinstans").Type);
        Assert.Equal("beslutningskompetanse", Kompetanseleksikon.For("beslutning-beslutningsmyndighet").Type);
        Assert.Equal("samordningskompetanse", Kompetanseleksikon.For("samordning-samordne").Type);
    }

    [Fact]
    public void KI_instruksen_lister_leksikonet_og_ukjent()
    {
        Assert.Contains(Kompetanseleksikon.Versjon, KiStrukturkonverterer.SystemInstruks);
        foreach (var uttrykk in Kompetanseleksikon.Regler.SelectMany(r => r.Uttrykk))
        {
            Assert.Contains(uttrykk, KiStrukturkonverterer.SystemInstruks);
        }
        Assert.Contains($"\"{Strukturkontrakt.Ukjent}\"", KiStrukturkonverterer.SystemInstruks);
    }

    [Theory]
    [InlineData("Kongen kan gi forskrift om tilsyn.", true)]
    [InlineData("Kongen i statsråd kan gi forskrift om tilsyn.", false)]
    [InlineData("Kommunestyret selv kan gi forskrift om gebyrer.", false)]
    [InlineData("Departementet kan gi forskrift om gebyrer.", null)]
    public void Delegerbar_avgjores_paa_sitatet(string tekst, bool? forventet)
    {
        var d = new MonsterStrukturkonverterer().Konverter(new Strukturkonverteringsgrunnlag("test", "https://lovdata.no/eli/lov/test/nor",
            [new Strukturnode("test", "https://lovdata.no/eli/lov/test/nor", "https://lovdata.no/eli/lov/test/nor/§1/ledd-1", "ledd", null, tekst)]));
        var u = Assert.Single(d.Utsagn, x => x.Type == "normgivningskompetanse");
        Assert.Equal(forventet, u.Delegerbar);
        Assert.Equal("forskrift", u.Normform);
    }

    [Fact]
    public void Beslutningsmyndighet_og_samordne_gir_de_nye_typene_men_passiv_samordnes_ikke()
    {
        Utsagn("Sametinget har beslutningsmyndighet når dette følger av andre bestemmelser.", "beslutningskompetanse", "Sametinget");
        Utsagn("Kommunen skal samordne tjenestetilbudet etter første ledd.", "samordningskompetanse", "Kommunen");
        var passiv = Konverter("Planen skal samordnes med kommunens øvrige beredskapsplaner.");
        Assert.DoesNotContain(passiv.Utsagn, u => u.Type == "samordningskompetanse");

        static void Utsagn(string tekst, string type, string fra)
        {
            var d = Konverter(tekst);
            var u = Assert.Single(d.Utsagn, x => x.Type == type);
            Assert.Equal(fra, d.Aktorer.Single(a => a.Id == u.Fra).Tekstform);
        }
    }

    private static Strukturdokument Konverter(string tekst) =>
        new MonsterStrukturkonverterer().Konverter(new Strukturkonverteringsgrunnlag("test", "https://lovdata.no/eli/lov/test/nor",
            [new Strukturnode("test", "https://lovdata.no/eli/lov/test/nor", "https://lovdata.no/eli/lov/test/nor/§1/ledd-1", "ledd", null, tekst)]));
}
