using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>Håndbok/rundskriv-forfatterflyt (docs/03-domenemodell.md §1.1.1), mot ekte embedded Postgres.</summary>
[Collection(DataTestCollection.Navn)]
public class HandbokForfatterTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public HandbokForfatterTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<Guid> ImporterAlkoholovenAsync(RegelIdeDbContext db)
    {
        var resultat = LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24));
        return await new RettskildeImportTjeneste(db).ImporterAsync(resultat);
    }

    [Fact]
    public async Task Oppretter_handbok_kapittel_og_kommentarseksjon()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var tjeneste = new HandbokForfatterTjeneste(db);
        var handbok = await tjeneste.OpprettHandbokAsync("Testkommunens håndbok", virksomhet, "Kari Jurist");

        Assert.Equal("Rundskriv", handbok.Kildetype);
        Assert.Equal("doc", handbok.Doctype);
        Assert.Equal("Gjeldende", handbok.Status); // v1: synlig for forfatteren via samme lese-endepunkter, se HandbokForfatterTjeneste-kommentar
        Assert.NotNull(handbok.AknXml);

        var kapittel = await tjeneste.OpprettKapittelNodeAsync(handbok.Id, null, "1", "Skjenkebevilling", "Kari Jurist");
        Assert.Equal("kapittel", kapittel.NodeType);
        Assert.Equal(1, kapittel.Versjon);
        Assert.Equal("gjeldende", kapittel.Entitetsstatus);

        var resultat = await tjeneste.OpprettKommentarNodeAsync(
            handbok.Id, kapittel.Id, "1.1", "Om vandelskravet", "<p>En kommentar.</p>",
            "kommentar", "bestemmelse", ["vandel"], "Kari Jurist");

        Assert.Equal("ledd", resultat.Node.NodeType);
        Assert.Equal("<p>En kommentar.</p>", resultat.Node.Tekst);
        Assert.False(resultat.Metadata.Bindende);
        Assert.Equal("under_arbeid", resultat.Metadata.Status);
        Assert.Equal(["vandel"], resultat.Metadata.Marginord);

        var proveniens = await db.Proveniens.Where(p => p.EntitetId == resultat.Node.Id).ToListAsync();
        Assert.Contains(proveniens, p => p.Handling == "opprettet" && p.EntitetType == "rettskilde_node");
    }

    [Theory]
    [InlineData("kommentar", false)]
    [InlineData("retningslinje", true)]
    [InlineData("instruks", true)]
    [InlineData("handbok", true)]
    public async Task Bindende_utledes_riktig_av_dokumenttype(string dokumenttype, bool forventetBindende)
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var tjeneste = new HandbokForfatterTjeneste(db);
        var handbok = await tjeneste.OpprettHandbokAsync("Håndbok", virksomhet, "Kari Jurist");
        var kapittel = await tjeneste.OpprettKapittelNodeAsync(handbok.Id, null, "1", null, "Kari Jurist");

        var resultat = await tjeneste.OpprettKommentarNodeAsync(
            handbok.Id, kapittel.Id, "1.1", null, "<p>Tekst</p>", dokumenttype, "ledd", null, "Kari Jurist");

        Assert.Equal(forventetBindende, resultat.Metadata.Bindende);
    }

    [Fact]
    public async Task Ugyldig_dokumenttype_kastes_ingen_gjettet_fallback()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var tjeneste = new HandbokForfatterTjeneste(db);
        var handbok = await tjeneste.OpprettHandbokAsync("Håndbok", virksomhet, "Kari Jurist");
        var kapittel = await tjeneste.OpprettKapittelNodeAsync(handbok.Id, null, "1", null, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(() => tjeneste.OpprettKommentarNodeAsync(
            handbok.Id, kapittel.Id, "1.1", null, "<p>Tekst</p>", "veiledning", "ledd", null, "Kari Jurist"));
    }

    [Fact]
    public async Task Redigering_oppretter_ny_versjon_og_arkiverer_forrige()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var tjeneste = new HandbokForfatterTjeneste(db);
        var handbok = await tjeneste.OpprettHandbokAsync("Håndbok", virksomhet, "Kari Jurist");
        var kapittel = await tjeneste.OpprettKapittelNodeAsync(handbok.Id, null, "1", null, "Kari Jurist");
        var v1 = await tjeneste.OpprettKommentarNodeAsync(
            handbok.Id, kapittel.Id, "1.1", "Tittel v1", "<p>Første versjon</p>", "kommentar", "ledd", null, "Kari Jurist");

        var v2 = await tjeneste.RedigerKommentarNodeAsync(
            v1.Node.Id, "<p>Andre versjon</p>", "Tittel v2", "retningslinje", "ledd", ["nytt"], "Ola Fagansvarlig");

        Assert.Equal(v1.Node.Eid, v2.Node.Eid);
        Assert.Equal(2, v2.Node.Versjon);
        Assert.Equal("gjeldende", v2.Node.Entitetsstatus);
        Assert.Equal(v1.Node.Id, v2.Node.ErstatterNodeId);
        Assert.True(v2.Metadata.Bindende); // retningslinje

        var forrige = await db.RettskildeNoder.SingleAsync(n => n.Id == v1.Node.Id);
        Assert.Equal("erstattet", forrige.Entitetsstatus);

        var historikk = await tjeneste.HentVersjonshistorikkAsync(handbok.Id, v1.Node.Eid);
        Assert.Equal(2, historikk.Count);
        Assert.Equal(2, historikk[0].Versjon); // nyeste først
        Assert.Equal(1, historikk[1].Versjon);
    }

    [Fact]
    public async Task Redigering_kopierer_lovreferanser_fremover_til_ny_versjon()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var alkoholovenId = await ImporterAlkoholovenAsync(db);
        var paragraf = await db.RettskildeNoder.FirstAsync(n => n.RettskildeId == alkoholovenId && n.NodeType == "paragraf");

        var tjeneste = new HandbokForfatterTjeneste(db);
        var handbok = await tjeneste.OpprettHandbokAsync("Håndbok", virksomhet, "Kari Jurist");
        var kapittel = await tjeneste.OpprettKapittelNodeAsync(handbok.Id, null, "1", null, "Kari Jurist");
        var v1 = await tjeneste.OpprettKommentarNodeAsync(
            handbok.Id, kapittel.Id, "1.1", null, "<p>v1</p>", "kommentar", "bestemmelse", null, "Kari Jurist");
        await tjeneste.KobleLovreferanseAsync(v1.Node.Id, alkoholovenId, paragraf.Eid);

        var v2 = await tjeneste.RedigerKommentarNodeAsync(v1.Node.Id, "<p>v2</p>", null, "kommentar", "bestemmelse", null, "Kari Jurist");

        var v1Referanser = await db.RettskildeReferanser.Where(r => r.FraNodeId == v1.Node.Id).ToListAsync();
        var v2Referanser = await db.RettskildeReferanser.Where(r => r.FraNodeId == v2.Node.Id).ToListAsync();
        Assert.Single(v1Referanser); // historisk versjon beholder sin egen, urørt
        Assert.Single(v2Referanser); // ny versjon arver den, ikke tom
        Assert.Equal(paragraf.Eid, v2Referanser[0].TilEid);
    }

    [Fact]
    public async Task Sanerer_bort_ikke_tillatt_markup_men_beholder_tillatte_tagger()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var tjeneste = new HandbokForfatterTjeneste(db);
        var handbok = await tjeneste.OpprettHandbokAsync("Håndbok", virksomhet, "Kari Jurist");
        var kapittel = await tjeneste.OpprettKapittelNodeAsync(handbok.Id, null, "1", null, "Kari Jurist");

        var ondsinnetHtml = "<p>Trygg <b>fet</b> tekst</p><script>alert(1)</script><table><tr><td>tabell</td></tr></table>" +
            "<p style=\"color:red\">med stil</p><a href=\"javascript:alert(1)\" onclick=\"bad()\">lenke</a>";

        var resultat = await tjeneste.OpprettKommentarNodeAsync(
            handbok.Id, kapittel.Id, "1.1", null, ondsinnetHtml, "kommentar", "ledd", null, "Kari Jurist");

        Assert.Contains("<p>Trygg <b>fet</b> tekst</p>", resultat.Node.Tekst);
        Assert.DoesNotContain("script", resultat.Node.Tekst, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert(1)", resultat.Node.Tekst);
        Assert.DoesNotContain("table", resultat.Node.Tekst, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("style=", resultat.Node.Tekst);
        Assert.DoesNotContain("onclick", resultat.Node.Tekst);
        Assert.DoesNotContain("javascript:", resultat.Node.Tekst);
    }

    [Fact]
    public async Task Kobler_og_fjerner_lovreferanse()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var alkoholovenId = await ImporterAlkoholovenAsync(db);
        var paragraf = await db.RettskildeNoder.FirstAsync(n => n.RettskildeId == alkoholovenId && n.NodeType == "paragraf");

        var tjeneste = new HandbokForfatterTjeneste(db);
        var handbok = await tjeneste.OpprettHandbokAsync("Håndbok", virksomhet, "Kari Jurist");
        var kapittel = await tjeneste.OpprettKapittelNodeAsync(handbok.Id, null, "1", null, "Kari Jurist");
        var kommentar = await tjeneste.OpprettKommentarNodeAsync(
            handbok.Id, kapittel.Id, "1.1", null, "<p>Kommentar</p>", "kommentar", "bestemmelse", null, "Kari Jurist");

        var referanse = await tjeneste.KobleLovreferanseAsync(kommentar.Node.Id, alkoholovenId, paragraf.Eid);
        Assert.Equal(kommentar.Node.Id, referanse.FraNodeId);

        var duplikat = await Assert.ThrowsAsync<ArgumentException>(() =>
            tjeneste.KobleLovreferanseAsync(kommentar.Node.Id, alkoholovenId, paragraf.Eid));
        Assert.Contains("allerede koblet", duplikat.Message);

        var fjernet = await tjeneste.FjernLovreferanseAsync(referanse.Id);
        Assert.True(fjernet);
        Assert.False(await db.RettskildeReferanser.AnyAsync(r => r.Id == referanse.Id));
    }

    [Fact]
    public async Task Revisjonsmerke_krever_ikke_tom_revisjonsgrunn()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var tjeneste = new HandbokForfatterTjeneste(db);
        var handbok = await tjeneste.OpprettHandbokAsync("Håndbok", virksomhet, "Kari Jurist");
        var kapittel = await tjeneste.OpprettKapittelNodeAsync(handbok.Id, null, "1", null, "Kari Jurist");
        var kommentar = await tjeneste.OpprettKommentarNodeAsync(
            handbok.Id, kapittel.Id, "1.1", null, "<p>Kommentar</p>", "kommentar", "ledd", null, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            tjeneste.SettRevisjonsmerkeAsync(kommentar.Node.Id, "  ", "Kari Jurist"));

        await tjeneste.SettRevisjonsmerkeAsync(kommentar.Node.Id, "Loven er endret siden sist.", "Kari Jurist");

        var metadata = await db.HandbokKommentarMetadata.SingleAsync(m => m.NodeId == kommentar.Node.Id);
        Assert.Equal("ma_revideres", metadata.Status);
        Assert.Equal("Loven er endret siden sist.", metadata.Revisjonsgrunn);
    }

    [Fact]
    public async Task Publisering_av_bindende_seksjon_krever_godkjenner()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var tjeneste = new HandbokForfatterTjeneste(db);
        var handbok = await tjeneste.OpprettHandbokAsync("Håndbok", virksomhet, "Kari Jurist");
        var kapittel = await tjeneste.OpprettKapittelNodeAsync(handbok.Id, null, "1", null, "Kari Jurist");
        var bindende = await tjeneste.OpprettKommentarNodeAsync(
            handbok.Id, kapittel.Id, "1.1", null, "<p>Instruks</p>", "instruks", "ledd", null, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            tjeneste.PubliserKommentarAsync(bindende.Node.Id, godkjentAv: null, "Kari Jurist"));

        await tjeneste.PubliserKommentarAsync(bindende.Node.Id, "Ola Fagansvarlig", "Kari Jurist");

        var metadata = await db.HandbokKommentarMetadata.SingleAsync(m => m.NodeId == bindende.Node.Id);
        Assert.Equal("publisert", metadata.Status);
        Assert.NotNull(metadata.Publisert);

        var proveniens = await db.Proveniens
            .Where(p => p.EntitetId == bindende.Node.Id && p.Handling == "publisert")
            .SingleAsync();
        Assert.Equal("Ola Fagansvarlig", proveniens.GodkjentAv);
    }

    [Fact]
    public async Task Publisering_av_ikke_bindende_seksjon_krever_ikke_godkjenner()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var tjeneste = new HandbokForfatterTjeneste(db);
        var handbok = await tjeneste.OpprettHandbokAsync("Håndbok", virksomhet, "Kari Jurist");
        var kapittel = await tjeneste.OpprettKapittelNodeAsync(handbok.Id, null, "1", null, "Kari Jurist");
        var kommentar = await tjeneste.OpprettKommentarNodeAsync(
            handbok.Id, kapittel.Id, "1.1", null, "<p>Kommentar</p>", "kommentar", "ledd", null, "Kari Jurist");

        await tjeneste.PubliserKommentarAsync(kommentar.Node.Id, godkjentAv: null, "Kari Jurist");

        var metadata = await db.HandbokKommentarMetadata.SingleAsync(m => m.NodeId == kommentar.Node.Id);
        Assert.Equal("publisert", metadata.Status);
    }
}
