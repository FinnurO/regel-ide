namespace RegelIde.Data.Tests;

/// <summary>Kunnskapsbibliotek (byggesteg 5 runde 1, docs/06-veikart.md), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class KunnskapsbibliotekTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public KunnskapsbibliotekTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Legger_til_lenke_med_gyldig_url()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var tjeneste = new KunnskapsbibliotekTjeneste(db);
        var lenke = await tjeneste.LeggTilLenkeAsync(virksomhet, "https://testkommunen.no/tjenester", "Om tjenestetilbudet", "Kari Jurist");

        Assert.Equal("https://testkommunen.no/tjenester", lenke.Url);
        var liste = await tjeneste.ListerForVirksomhetAsync(virksomhet);
        Assert.Single(liste);
    }

    [Theory]
    [InlineData("ikke-en-url")]
    [InlineData("ftp://testkommunen.no")]
    [InlineData("")]
    public async Task Ugyldig_url_kastes_ingen_gjettet_fallback(string ugyldigUrl)
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var tjeneste = new KunnskapsbibliotekTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            tjeneste.LeggTilLenkeAsync(virksomhet, ugyldigUrl, null, "Kari Jurist"));
    }

    [Fact]
    public async Task Sletter_lenke()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var tjeneste = new KunnskapsbibliotekTjeneste(db);
        var lenke = await tjeneste.LeggTilLenkeAsync(virksomhet, "https://testkommunen.no", null, "Kari Jurist");

        var slettet = await tjeneste.SlettAsync(lenke.Id);

        Assert.True(slettet);
        Assert.Empty(await tjeneste.ListerForVirksomhetAsync(virksomhet));
    }

    [Fact]
    public async Task Lister_kun_egen_virksomhets_lenker()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetA = Guid.NewGuid();
        var virksomhetB = Guid.NewGuid();
        db.Virksomheter.AddRange(
            new Virksomhet { Id = virksomhetA, Navn = "Testkommunen A" },
            new Virksomhet { Id = virksomhetB, Navn = "Testkommunen B" });
        await db.SaveChangesAsync();

        var tjeneste = new KunnskapsbibliotekTjeneste(db);
        await tjeneste.LeggTilLenkeAsync(virksomhetA, "https://a.testkommunen.no", null, "Kari Jurist");
        await tjeneste.LeggTilLenkeAsync(virksomhetB, "https://b.testkommunen.no", null, "Kari Jurist");

        var listeA = await tjeneste.ListerForVirksomhetAsync(virksomhetA);
        Assert.Single(listeA);
        Assert.Equal("https://a.testkommunen.no", listeA[0].Url);
    }

    private const string LangTekst =
        "Dette er en ekte tekst-PDF for testing av tekstuttrekk og kunnskapsbiblioteket i regel-ide. " +
        "Teksten er bevisst gjort lang nok til å passere terskelen for hva som regnes som et tekstlag.";

    [Fact]
    public async Task Legger_til_pdf_fil_med_tekstlag()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var tjeneste = new KunnskapsbibliotekTjeneste(db);
        var fil = await tjeneste.LeggTilFilAsync(virksomhet, "skjema.pdf", TestFilFixtures.LagPdf(LangTekst), "Kari Jurist", "Søknadsskjema (test)");

        Assert.Equal("pdf", fil.Filtype);
        Assert.Equal("Søknadsskjema (test)", fil.Tittel);
        Assert.Contains("ekte tekst-PDF", fil.UtvunnetTekst);
        var liste = await tjeneste.ListerFilerForVirksomhetAsync(virksomhet);
        Assert.Single(liste);
        Assert.Equal("Søknadsskjema (test)", liste[0].Tittel);
        Assert.Empty(liste[0].Innhold); // listing skal ikke hente rå bytes over wire
    }

    [Fact]
    public async Task Legger_til_docx_fil_med_tekst()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var tjeneste = new KunnskapsbibliotekTjeneste(db);
        var fil = await tjeneste.LeggTilFilAsync(virksomhet, "notat.docx", TestFilFixtures.LagDocx(LangTekst), "Kari Jurist");

        Assert.Equal("docx", fil.Filtype);
        Assert.Contains("ekte tekst-PDF", fil.UtvunnetTekst);
    }

    [Fact]
    public async Task Skannet_pdf_uten_tekstlag_avvises()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var tjeneste = new KunnskapsbibliotekTjeneste(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tjeneste.LeggTilFilAsync(virksomhet, "skann.pdf", TestFilFixtures.LagPdf(tekst: null), "Kari Jurist"));

        Assert.Empty(await tjeneste.ListerFilerForVirksomhetAsync(virksomhet));
    }

    [Fact]
    public async Task For_stor_fil_avvises_uten_forsok_pa_tekstuttrekk()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var tjeneste = new KunnskapsbibliotekTjeneste(db);
        var forStor = new byte[21 * 1024 * 1024];
        await Assert.ThrowsAsync<ArgumentException>(() =>
            tjeneste.LeggTilFilAsync(virksomhet, "stor.pdf", forStor, "Kari Jurist"));
    }

    [Fact]
    public async Task Sletter_fil()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var tjeneste = new KunnskapsbibliotekTjeneste(db);
        var fil = await tjeneste.LeggTilFilAsync(virksomhet, "skjema.pdf", TestFilFixtures.LagPdf(LangTekst), "Kari Jurist");

        Assert.True(await tjeneste.SlettFilAsync(fil.Id));
        Assert.Empty(await tjeneste.ListerFilerForVirksomhetAsync(virksomhet));
    }

    [Fact]
    public async Task Lister_kun_egen_virksomhets_filer()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetA = Guid.NewGuid();
        var virksomhetB = Guid.NewGuid();
        db.Virksomheter.AddRange(
            new Virksomhet { Id = virksomhetA, Navn = "Testkommunen A" },
            new Virksomhet { Id = virksomhetB, Navn = "Testkommunen B" });
        await db.SaveChangesAsync();

        var tjeneste = new KunnskapsbibliotekTjeneste(db);
        await tjeneste.LeggTilFilAsync(virksomhetA, "a.pdf", TestFilFixtures.LagPdf(LangTekst), "Kari Jurist");
        await tjeneste.LeggTilFilAsync(virksomhetB, "b.pdf", TestFilFixtures.LagPdf(LangTekst), "Kari Jurist");

        var listeA = await tjeneste.ListerFilerForVirksomhetAsync(virksomhetA);
        Assert.Single(listeA);
        Assert.Equal("a.pdf", listeA[0].Filnavn);
    }
}
