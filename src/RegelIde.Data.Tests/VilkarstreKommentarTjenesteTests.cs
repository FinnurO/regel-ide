namespace RegelIde.Data.Tests;

/// <summary>Veiledningskommentarer på vilkårstre-noder (docs/12-fasit-handbok-leveranse.md "Hovedfunn" + dimensjon A), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class VilkarstreKommentarTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public VilkarstreKommentarTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<(Guid Virksomhet, Guid Vilkar)> NyttVilkarAsync(RegelIdeDbContext db)
    {
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();
        var vilkar = await new VilkarregisterTjeneste(db).OpprettAsync(
            virksomhet, "Aldersvilkår", null, null, "materiell", null, null, null, "regelbasert", null,
            null, null, false, null, null, null, false, null, null, "Kari Jurist");
        return (virksomhet, vilkar.Id);
    }

    [Fact]
    public async Task Oppretter_kommentar_pa_vilkar()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, vilkar) = await NyttVilkarAsync(db);

        var tjeneste = new VilkarstreKommentarTjeneste(db);
        var kommentar = await tjeneste.OpprettAsync(virksomhet, "vilkar", vilkar, "praktisk-rad", "<p>Terskelen er lav.</p>", "Kari Jurist");

        Assert.Equal("praktisk-rad", kommentar.Dokumenttype);
        Assert.Equal("<p>Terskelen er lav.</p>", kommentar.TekstHtml);
    }

    [Fact]
    public async Task Farlig_markup_saneres_ved_opprettelse()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, vilkar) = await NyttVilkarAsync(db);

        var tjeneste = new VilkarstreKommentarTjeneste(db);
        var kommentar = await tjeneste.OpprettAsync(
            virksomhet, "vilkar", vilkar, "kommentar", "<p>Trygt</p><script>alert(1)</script>", "Kari Jurist");

        Assert.DoesNotContain("script", kommentar.TekstHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sjekkliste_med_ul_beholdes_etter_sanering()
    {
        // 2026-07-30, dimensjon G — ul/ol/li er nettopp lagt til KommentarTekstSanering sin allow-list.
        await using var db = _fixture.NyDbContext();
        var (virksomhet, vilkar) = await NyttVilkarAsync(db);

        var tjeneste = new VilkarstreKommentarTjeneste(db);
        var kommentar = await tjeneste.OpprettAsync(
            virksomhet, "vilkar", vilkar, "sjekkliste", "<ul><li>Er søknaden komplett?</li></ul>", "Kari Jurist");

        Assert.Contains("<ul>", kommentar.TekstHtml);
        Assert.Contains("<li>Er søknaden komplett?</li>", kommentar.TekstHtml);
    }

    [Fact]
    public async Task Flere_kommentarer_pa_samme_node_far_stigende_rekkefolge()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, vilkar) = await NyttVilkarAsync(db);

        var tjeneste = new VilkarstreKommentarTjeneste(db);
        var forste = await tjeneste.OpprettAsync(virksomhet, "vilkar", vilkar, "hjemmel", "<p>Første</p>", "Kari Jurist");
        var andre = await tjeneste.OpprettAsync(virksomhet, "vilkar", vilkar, "kommentar", "<p>Andre</p>", "Kari Jurist");

        Assert.Equal(0, forste.Rekkefolge);
        Assert.Equal(1, andre.Rekkefolge);
    }

    [Fact]
    public async Task Ukjent_dokumenttype_avvises()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, vilkar) = await NyttVilkarAsync(db);

        var tjeneste = new VilkarstreKommentarTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(
            () => tjeneste.OpprettAsync(virksomhet, "vilkar", vilkar, "ukjent-type", "<p>x</p>", "Kari Jurist"));
    }

    [Fact]
    public async Task Ukjent_maltype_avvises()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, vilkar) = await NyttVilkarAsync(db);

        var tjeneste = new VilkarstreKommentarTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(
            () => tjeneste.OpprettAsync(virksomhet, "vedtak", vilkar, "kommentar", "<p>x</p>", "Kari Jurist"));
    }

    [Fact]
    public async Task Ukjent_mal_id_avvises()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, _) = await NyttVilkarAsync(db);

        var tjeneste = new VilkarstreKommentarTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(
            () => tjeneste.OpprettAsync(virksomhet, "vilkar", Guid.NewGuid(), "kommentar", "<p>x</p>", "Kari Jurist"));
    }

    [Fact]
    public async Task Henter_kommentarer_for_node_i_rekkefolge()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, vilkar) = await NyttVilkarAsync(db);

        var tjeneste = new VilkarstreKommentarTjeneste(db);
        await tjeneste.OpprettAsync(virksomhet, "vilkar", vilkar, "hjemmel", "<p>Først</p>", "Kari Jurist");
        await tjeneste.OpprettAsync(virksomhet, "vilkar", vilkar, "praktisk-rad", "<p>Sist</p>", "Kari Jurist");

        var liste = await tjeneste.HentForNodeAsync("vilkar", vilkar);

        Assert.Equal(2, liste.Count);
        Assert.Equal("<p>Først</p>", liste[0].TekstHtml);
        Assert.Equal("<p>Sist</p>", liste[1].TekstHtml);
    }

    [Fact]
    public async Task Oppdaterer_kommentar()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, vilkar) = await NyttVilkarAsync(db);

        var tjeneste = new VilkarstreKommentarTjeneste(db);
        var kommentar = await tjeneste.OpprettAsync(virksomhet, "vilkar", vilkar, "kommentar", "<p>Original</p>", "Kari Jurist");

        var oppdatert = await tjeneste.OppdaterAsync(kommentar.Id, "praktisk-rad", "<p>Endret</p>", "Kari Jurist");

        Assert.NotNull(oppdatert);
        Assert.Equal("praktisk-rad", oppdatert!.Dokumenttype);
        Assert.Equal("<p>Endret</p>", oppdatert.TekstHtml);
        Assert.NotNull(oppdatert.SistEndretTidspunkt);
    }

    [Fact]
    public async Task Sletter_kommentar()
    {
        await using var db = _fixture.NyDbContext();
        var (virksomhet, vilkar) = await NyttVilkarAsync(db);

        var tjeneste = new VilkarstreKommentarTjeneste(db);
        var kommentar = await tjeneste.OpprettAsync(virksomhet, "vilkar", vilkar, "kommentar", "<p>x</p>", "Kari Jurist");

        Assert.True(await tjeneste.SlettAsync(kommentar.Id));
        Assert.Empty(await tjeneste.HentForNodeAsync("vilkar", vilkar));
    }
}
