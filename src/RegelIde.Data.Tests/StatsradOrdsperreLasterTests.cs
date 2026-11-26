using Microsoft.EntityFrameworkCore;
using RegelIde.Data.Strukturkonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, Refs #486, prosjekteierens klarsignal 2026-11-25] Ordsperren i lasteren når referenten er et rollebegrep. Spørsmålet: blir en aktør med
/// tekstformen «statsråd» koblet til rollen statsråd i et ledd der ordet står i «vedtak i statsråd»? Nei: alle forekomstene i noden er sperret,
/// og utsagnet får årsaken <see cref="Forslagsarsak.AktorOrdsperre"/>. I et ledd med «en statsråd» stopper ikke sperren.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class StatsradOrdsperreLasterTests(EmbeddedPostgresFixture fixture)
{
    private sealed class KasterHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Ingen nettverkskall i testen.");
    }

    private static StrukturforslagTjeneste NyLaster(RegelIdeDbContext db) =>
        new(db, new StrukturkantTjeneste(db), new VirksomhetOppslagTjeneste(db), new NavnekandidatOppdagelseTjeneste(
            db, new VirksomhetsbegrepTjeneste(db), new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)),
            new VirksomhetOppslagTjeneste(db), new EksternNavneoppslagTjeneste(new HttpClient(new KasterHandler()), db), new StrukturkantTjeneste(db)));

    [Fact]
    public async Task Sperret_statsrad_kobles_ikke_til_rollen()
    {
        await using var db = fixture.NyDbContext();
        var rettskildeId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse", Tittel = "Statsrådlov " + rettskildeId,
            OpprettetAv = "Test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        db.RettskildeNoder.AddRange(
            new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = "paragraf-1/ledd-1", KildeId = "paragraf-1/ledd-1", NodeType = "ledd",
                Tekst = "Saker som vedtas i statsråd, kunngjøres.", Sorteringsrekkefolge = 0,
            },
            new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = "paragraf-1/ledd-2", KildeId = "paragraf-1/ledd-2", NodeType = "ledd",
                Tekst = "Er en statsråd forhindret, avgjør statsministeren saken.", Sorteringsrekkefolge = 1,
            });
        // Rollen gjelder bare denne loven, så testen ikke avhenger av om rollebegrepet for hele korpuset (#489) finnes i basen.
        var finnesGlobal = await db.Begreper.AnyAsync(b => b.Entitetsstatus == "gjeldende" && b.Begrepskategori == Nodetyper.Rolle
                                                           && b.LovkildeId == null && b.Term == "statsråd");
        if (!finnesGlobal)
        {
            db.Begreper.Add(new BegrepEntitet
            {
                Id = Guid.NewGuid(), Begrepskategori = Nodetyper.Rolle, Term = "statsråd", LovkildeId = rettskildeId, Status = "publisert", OpprettetAv = "Test",
            });
        }
        await db.SaveChangesAsync();

        StrukturAktor A(string id, string t) => new(id, t, [], [], 1, null, null, null, null, null, null);
        StrukturUtsagn U(string id, string eid) => new(id, eid, "sitat " + id, "kompetanse", "vedtakskompetanse", "a1",
            null, "saken", "positiv", null, null, null, null, null) { Oppdagelseskilde = "monster:test" };
        var dok = new Strukturdokument("Statsrådlov", "eli", [], "test", 1, [A("a1", "statsråd")],
            [U("u1", "paragraf-1/ledd-1"), U("u2", "paragraf-1/ledd-2")]);

        var rapport = await NyLaster(db).LastAsync(rettskildeId, dok, "monster", "monster:ukjent", "Test");

        Assert.Equal(Forslagsarsak.AktorOrdsperre, rapport.Rader.Single(r => r.UtsagnId == "u1").Arsak);
        Assert.NotEqual(Forslagsarsak.AktorOrdsperre, rapport.Rader.Single(r => r.UtsagnId == "u2").Arsak);
    }
}
