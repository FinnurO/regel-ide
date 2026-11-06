using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #440, Johanns avgjørelse 2026-11-05] Godkjenningen i aktørkøen med forhåndsklassifiseringens hjemmel og navn, mot ekte embedded
/// Postgres. Spørsmålene: lagres filas hjemmel (rettskilde, eId, kunnskapsstatus og sitatet i begrunnelsen) og grunnformen, og avvises alt
/// som ikke står i fila for nøkkelen? Navnene er nøklene i <c>klassifisering.json</c>; eId-ene står i en testrettskilde, så rettskilden
/// løses entydig til den.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class AktorkoForhandGodkjenningTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public AktorkoForhandGodkjenningTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class KasterHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Ingen eksterne oppslag her.");
    }

    private static AktorkoSkrivTjeneste Tjeneste(RegelIdeDbContext db)
    {
        var vo = new VirksomhetOppslagTjeneste(db);
        var tt = new TekstTaggTjeneste(db, vo);
        var vb = new VirksomhetsbegrepTjeneste(db);
        var kandidater = new NavnekandidatOppdagelseTjeneste(db, vb, tt, vo, new EksternNavneoppslagTjeneste(new HttpClient(new KasterHandler()), db),
            new StrukturkantTjeneste(db));
        return new AktorkoSkrivTjeneste(db, new AktorkoTjeneste(db), kandidater, vb, new AktoridentitetTjeneste(db, tt, vo));
    }

    private static ForhandPost Post(string nokkel) => Forhandsklassifisering.Alle.Single(p => p.Nokkel == nokkel);

    /// <summary>En rettskilde med to noder: hjemmelen (filas eId) og en node der navnet står. Én kandidat i hver av dem som har navnet.</summary>
    private static async Task<Guid> OppsettAsync(RegelIdeDbContext db, string nokkel, string hjemmeltekst, string forekomsttekst, string navnITeksten)
    {
        var p = Post(nokkel);
        var departement = new Virksomhet { Id = Guid.NewGuid(), Navn = "Testdepartementet " + Guid.NewGuid().ToString("N")[..8] };
        db.Virksomheter.Add(departement);
        var id = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = id, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse", Tittel = "Testlov " + id,
            AnsvarligDepartement = [departement.Navn], OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var annen = $"https://test/{id:N}/§9/ledd-1";
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = id, Eid = p.Hjemmel!.Eid, KildeId = "h", NodeType = "ledd", Tekst = hjemmeltekst, Sorteringsrekkefolge = 0,
        });
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = id, Eid = annen, KildeId = "f", NodeType = "ledd", Tekst = forekomsttekst, Sorteringsrekkefolge = 1,
        });
        var start = forekomsttekst.IndexOf(navnITeksten, StringComparison.Ordinal);
        db.Navnekandidater.Add(new NavnekandidatEntitet
        {
            Id = Guid.NewGuid(), ForeslattTekst = navnITeksten, Kategori = "gruppe", RettskildeId = id, NodeEid = annen, StartOffset = start,
            EndOffset = start + navnITeksten.Length, Status = "Venter", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Grunnformen_og_hjemmelen_fra_fila_lagres_for_en_rolle()
    {
        await using var db = _fixture.NyDbContext();
        var p = Post("kommunelegen");
        var lov = await OppsettAsync(db, "kommunelegen", "Kommunen skal ha en eller flere kommuneleger.", "Kommunelegen skal gi råd.", "Kommunelegen");

        var k = await Tjeneste(db).GodkjennAsync("kommunelegen", new AktorkoGodkjenning(
            Nodetyper.Rolle, p.NyNode, Aktorkohjemmel.Forhandsklassifisering, null, p.Hjemmel!.Eid, Strukturkanter.FagligVurdert), "test");

        var begrep = await db.Begreper.SingleAsync(b => b.Id == k!.BegrepId);
        Assert.Equal(("kommunelege", lov, p.Hjemmel.Eid, Strukturkanter.Normalisering), (begrep.Term, begrep.LovkildeId!.Value, begrep.LovreferanseEid, begrep.Kunnskapsstatus));
        Assert.Contains("forhåndsklassifiseringen", k!.Linjer[0]);
        Assert.Contains(k.Linjer, l => l.Contains("normalisering"));
        Assert.Equal(1, k.Tagget);
    }

    [Fact]
    public async Task Fortolkning_krever_tolkningsargument_som_mennesket_velger()
    {
        await using var db = _fixture.NyDbContext();
        var p = Post("omsorgskommune");
        await OppsettAsync(db, "omsorgskommune", "Omsorgskommunen skal betale.", "Omsorgskommunen skal betale for hjelpen.", "Omsorgskommunen");
        var uten = new AktorkoGodkjenning(Nodetyper.Rolle, "omsorgskommune", Aktorkohjemmel.Forhandsklassifisering, null, p.Hjemmel!.Eid,
            Strukturkanter.FagligVurdert);

        // «omsorgskommune» står ikke i teksten (bare «Omsorgskommunen»), men er navnet i fila.
        var feil = await Assert.ThrowsAsync<ArgumentException>(() => Tjeneste(db).GodkjennAsync("omsorgskommunen", uten, "test"));
        Assert.Contains("tolkningsargument", feil.Message);

        var k = await Tjeneste(db).GodkjennAsync("omsorgskommunen", uten with { Tolkningsargumenter = ["ordlyd"] }, "test");
        var begrep = await db.Begreper.SingleAsync(b => b.Id == k!.BegrepId);
        Assert.Equal(("omsorgskommune", Strukturkanter.Fortolkning), (begrep.Term, begrep.Kunnskapsstatus));
        Assert.Contains("Hjemmel fra forhåndsklassifiseringen", begrep.Begrunnelse);
        Assert.Contains(p.Hjemmel.Sitat!, begrep.Begrunnelse);
    }

    [Fact]
    public async Task Det_som_ikke_star_i_fila_for_nokkelen_avvises_og_ingenting_skrives()
    {
        await using var db = _fixture.NyDbContext();
        var p = Post("regionalt helseforetak");
        await OppsettAsync(db, "regionalt helseforetak", "I hver helseregion skal det være ett regionalt helseforetak.",
            "Det regionale helseforetaket skal sørge for tjenestene, og regionalt helseforetak skal samarbeide.", "regionalt helseforetak");
        var riktig = new AktorkoGodkjenning(Nodetyper.Klasse, "regionalt helseforetak", Aktorkohjemmel.Forhandsklassifisering, null, p.Hjemmel!.Eid,
            Strukturkanter.FagligVurdert);
        var t = Tjeneste(db);
        var begreper = await db.Begreper.CountAsync();

        // En annen eId enn filas.
        await Assert.ThrowsAsync<ArgumentException>(() => t.GodkjennAsync("regionalt helseforetak", riktig with { HjemmelEid = p.Hjemmel.Eid + "/punkt-1" }, "test"));
        // En annen rettskilde enn den bestemmelsen står i.
        await Assert.ThrowsAsync<ArgumentException>(() => t.GodkjennAsync("regionalt helseforetak", riktig with { HjemmelRettskildeId = Guid.NewGuid() }, "test"));
        // Et navn som verken står i teksten eller er filas navn.
        await Assert.ThrowsAsync<ArgumentException>(() => t.GodkjennAsync("regionalt helseforetak", riktig with { Navn = "regionale helseforetak" }, "test"));
        // Tolkningsargumenter når hjemmelen ikke er fortolkning (tekstfunn).
        await Assert.ThrowsAsync<ArgumentException>(() => t.GodkjennAsync("regionalt helseforetak", riktig with { Tolkningsargumenter = ["ordlyd"] }, "test"));
        Assert.Equal(begreper, await db.Begreper.CountAsync());

        var k = await t.GodkjennAsync("regionalt helseforetak", riktig, "test");
        var begrep = await db.Begreper.SingleAsync(b => b.Id == k!.BegrepId);
        Assert.Equal((p.Hjemmel.Eid, Strukturkanter.Tekstfunn, (string?)null), (begrep.LovreferanseEid, begrep.Kunnskapsstatus, begrep.HjemmelArt));
    }

    [Fact]
    public async Task En_aktor_beholder_hjemmelen_med_normalisering_selv_om_bestemmelsen_ikke_viser_navnet_ordrett()
    {
        await using var db = _fixture.NyDbContext();
        var p = Post("sysselmesteren på svalbard");
        await OppsettAsync(db, "sysselmesteren på svalbard", "På Svalbard skal det være en sysselmester som oppnevnes av Kongen.",
            "Saken avgjøres av Sysselmesteren på Svalbard.", "Sysselmesteren på Svalbard");

        var k = await Tjeneste(db).GodkjennAsync("sysselmesteren på svalbard", new AktorkoGodkjenning(
            Aktorkotyper.Aktor, "Sysselmesteren på Svalbard", Aktorkohjemmel.Forhandsklassifisering, null, p.Hjemmel!.Eid, Strukturkanter.Godkjent), "test");

        var navneform = await db.Begreper.SingleAsync(b => b.Id == k!.NavneformId);
        Assert.Equal((p.Hjemmel.Eid, Strukturkanter.Normalisering, Strukturkanter.Godkjent), (navneform.LovreferanseEid, navneform.Kunnskapsstatus, navneform.Godkjenning));
        Assert.Empty(k!.Varsler);
    }

    [Fact]
    public async Task Et_navn_som_ikke_star_i_fila_kan_ikke_bruke_filas_hjemmel()
    {
        await using var db = _fixture.NyDbContext();
        var navn = "testnavn" + new string(Guid.NewGuid().ToString("N")[..8].Select(c => (char)('a' + Convert.ToInt32(c.ToString(), 16))).ToArray());
        var departement = new Virksomhet { Id = Guid.NewGuid(), Navn = "Testdepartementet " + navn };
        db.Virksomheter.Add(departement);
        var id = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = id, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse", Tittel = "Testlov " + id,
            AnsvarligDepartement = [departement.Navn], OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var eid = $"https://test/{id:N}/§1/ledd-1";
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = id, Eid = eid, KildeId = "1", NodeType = "ledd", Tekst = $"Saken går til {navn}.", Sorteringsrekkefolge = 0,
        });
        db.Navnekandidater.Add(new NavnekandidatEntitet
        {
            Id = Guid.NewGuid(), ForeslattTekst = navn, Kategori = "gruppe", RettskildeId = id, NodeEid = eid, StartOffset = 15, EndOffset = 15 + navn.Length,
            Status = "Venter", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var feil = await Assert.ThrowsAsync<ArgumentException>(() => Tjeneste(db).GodkjennAsync(navn, new AktorkoGodkjenning(
            Aktorkotyper.Aktor, navn, Aktorkohjemmel.Forhandsklassifisering, null, eid, Strukturkanter.FagligVurdert), "test"));
        Assert.Contains("ingen hjemmel i forhåndsklassifiseringen", feil.Message);
    }
}
