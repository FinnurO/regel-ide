using System.Text;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #446 PR 1] <see cref="VedtaksformTjeneste"/> mot ekte Postgres: formen og ikraftstatusen skrives på kilden, rollen per ledd på nodene,
/// en ikraftsetting i korpus følges når «I kraft fra» sier «Kongen bestemmer», og en ny kjøring endrer ingenting.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class VedtaksformTjenesteTests(EmbeddedPostgresFixture fixture)
{
    private static string Html(string kropp) =>
        $"<!DOCTYPE html><html><body><main class=\"documentBody\" id=\"dokument\"><h1>Tittel</h1>{kropp}</main></body></html>";

    private static RettskildeEntitet Kilde(string tittel, string dokid, string html, string? ikraftRaa) => new()
    {
        Id = Guid.NewGuid(), Doctype = "doc", Kildetype = dokid.Contains("/lov/") ? "Lov" : "Forskrift", Status = "Gjeldende", Importrolle = "primaer",
        Tittel = tittel, DokumentId = dokid, AknXml = "<akomaNtoso/>", Innhold = Encoding.UTF8.GetBytes(html), IkrafttredelseRaa = ikraftRaa,
        OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Endringslov_far_form_ikraft_fra_ikraftsettingen_og_roller_per_ledd_idempotent()
    {
        await using var db = fixture.NyDbContext();
        var n = Guid.NewGuid().ToString("N")[..6];
        var lov = Kilde($"Lov om endringer i testloven {n}", $"LTI/lov/2021-03-26-{n}",
            Html("<section class=\"section\" id=\"kapittel-1\"><h2>I</h2><article class=\"legalP\" id=\"kapittel-1-ledd-1\">I lov 1. januar 2000 nr. 1 om test "
                + "gjøres følgende endringer:</article><article class=\"defaultP\">§ 3 skal lyde:</article>"
                + "<article class=\"legalP\" id=\"kapittel-1-ledd-2\">Landbruksdepartementet fører tilsyn.</article></section>"
                + "<section class=\"section\" id=\"kapittel-2\"><h2>II</h2><article class=\"legalP\" id=\"kapittel-2-ledd-1\">Loven gjelder fra den tid Kongen "
                + "bestemmer.</article></section>"),
            "Kongen bestemmer");
        var ikraftsetting = Kilde($"Ikraftsetting av lov 26. mars 2021 om endringer i testloven {n}", $"LTI/forskrift/2021-03-26-{n}",
            Html("<article class=\"legalP\" id=\"ledd-1\">Loven trer i kraft 1. april 2021.</article>"), "2021-04-01");
        db.Rettskilder.AddRange(lov, ikraftsetting);
        db.RettskildeHjemler.Add(new RettskildeHjemmelEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = ikraftsetting.Id, HjemmelEid = $"https://test/{n}", HjemmelRettskildeId = lov.Id,
        });
        var noder = new (string Eid, string KildeId, string? Rolle)[]
        {
            ("kap-I/ledd-1", "kapittel-1-ledd-1", null), ("kap-I/ledd-2", "kapittel-1-ledd-2", null), ("kap-II/ledd-1", "kapittel-2-ledd-1", null),
            ("kap-III/ledd-1", "kapittel-3-ledd-1", "sitert_tekst"), // en gammel rolle som ikke lenger gjelder, fjernes
        };
        var i = 0;
        foreach (var (eid, kid, rolle) in noder)
        {
            db.RettskildeNoder.Add(new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = lov.Id, Eid = eid, KildeId = kid, NodeType = "ledd", Tekst = "x", Sorteringsrekkefolge = i++,
                Endringsrolle = rolle,
            });
        }
        await db.SaveChangesAsync();
        var tjeneste = new VedtaksformTjeneste(db);
        var idag = new DateOnly(2026, 11, 26);

        var torr = await tjeneste.KlassifiserAsync(skriv: false, lov.Id, idag: idag);
        Assert.Equal(1, torr.KilderEndret);
        Assert.Null((await db.Rettskilder.AsNoTracking().SingleAsync(r => r.Id == lov.Id)).Vedtaksform);

        var rapport = await tjeneste.KlassifiserAsync(skriv: true, lov.Id, medRader: true, idag: idag);
        Assert.Equal(1, rapport.IkraftFulgtIKorpus);
        var rad = Assert.Single(rapport.Rader);
        Assert.True(rad.UttomtVirkning);
        var lagret = await db.Rettskilder.AsNoTracking().SingleAsync(r => r.Id == lov.Id);
        Assert.Equal(Vedtaksform.RentEndringsvedtak, lagret.Vedtaksform);
        Assert.Equal("normalisering", lagret.VedtaksformKunnskapsstatus);
        Assert.Equal(Vedtaksform.Regelversjon, lagret.VedtaksformRegelversjon);
        Assert.Equal(Vedtaksform.IkraftJa, lagret.IkraftStatus);
        Assert.Contains(ikraftsetting.DokumentId!, lagret.IkraftStatusGrunnlag);
        // [ENDRET, #446 PR 2] Markøren er avledet: ren form og i kraft → uttømt virkning.
        Assert.True(lagret.RentEndringsvedtak);
        Assert.Contains("vedtaksform rent_endringsvedtak, ikraftstatus ja", lagret.RentEndringsvedtakGrunnlag);
        Assert.Contains(lov.Id, await Sveipeutvalg.StandardUtelattAsync(db));

        var roller = await db.RettskildeNoder.AsNoTracking().Where(x => x.RettskildeId == lov.Id).ToDictionaryAsync(x => x.KildeId, x => x.Endringsrolle);
        Assert.Equal(Vedtaksform.RolleEndringsinstruks, roller["kapittel-1-ledd-1"]);
        Assert.Equal(Vedtaksform.RolleSitertTekst, roller["kapittel-1-ledd-2"]);
        Assert.Equal(Vedtaksform.RolleIkrafttredelse, roller["kapittel-2-ledd-1"]);
        Assert.Null(roller["kapittel-3-ledd-1"]);

        var igjen = await tjeneste.KlassifiserAsync(skriv: true, lov.Id, idag: idag);
        Assert.Equal(0, igjen.KilderEndret);
        Assert.Equal(0, igjen.NoderEndret);
    }

    [Fact]
    public async Task Ukjent_rettskilde_gir_ArgumentException()
    {
        await using var db = fixture.NyDbContext();
        await Assert.ThrowsAsync<ArgumentException>(() => new VedtaksformTjeneste(db).KlassifiserAsync(false, Guid.NewGuid()));
    }
}
