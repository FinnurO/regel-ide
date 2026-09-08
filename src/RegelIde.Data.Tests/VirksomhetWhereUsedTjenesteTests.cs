using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="VirksomhetWhereUsedTjeneste"/> — «hvor er denne virksomheten koblet inn».
///
/// <para>
/// Alle testene bruker EGNE, GUID-unike virksomheter og rettskilder: den delte
/// DataTestCollection-databasen inneholder rader fra andre testklasser, og oppslaget er scopet til ÉN
/// virksomhet, så det er nettopp det scopet som gjør assertene trygge. Ingen test asserterer på
/// totaler.
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class VirksomhetWhereUsedTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public VirksomhetWhereUsedTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<Guid> NyVirksomhetAsync(RegelIdeDbContext db, string navn)
    {
        var id = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = id, Navn = navn, OpprettetTidspunkt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<(Guid RettskildeId, string NodeEid, string Tekst)> NyRettskildeMedNodeAsync(
        RegelIdeDbContext db, string tittel, string tekst)
    {
        var rettskildeId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId,
            Doctype = "act",
            Kildetype = "Forskrift",
            Tittel = tittel,
            Eli = $"https://lovdata.no/eli/forskrift/2026/01/01/{Math.Abs(rettskildeId.GetHashCode())}/nor",
            Status = "Gjeldende",
            Importrolle = "referanse",
            OpprettetAv = "test",
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var nodeEid = $"whereused/{rettskildeId}/§1/ledd-1";
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(),
            RettskildeId = rettskildeId,
            Eid = nodeEid,
            KildeId = nodeEid, // source_id — samme verdi som Eid holder for en syntetisk testnode.
            NodeType = "ledd",
            Tekst = tekst,
        });
        await db.SaveChangesAsync();
        return (rettskildeId, nodeEid, tekst);
    }

    /// <summary>Oppretter taggen gjennom TJENESTELAGET (ikke db.Add) — samme kodevei appen bruker, så
    /// testen også bekrefter at den nye RefId-valideringen godtar en navneform.</summary>
    private static async Task<TekstTaggEntitet> NyNavneformTaggAsync(
        RegelIdeDbContext db, Guid rettskildeId, string nodeEid, string tekst, Guid eierVirksomhetId,
        Guid navneformId, int start, int slutt)
    {
        var tjeneste = new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db));
        var tagg = await tjeneste.OpprettAsync(
            rettskildeId, eierVirksomhetId, "test", nodeEid, start, slutt,
            "", tekst[start..slutt], "", "virksomhet");
        await tjeneste.KobleTilEntitetAsync(tagg!.Id, navneformId, "test");
        return tagg;
    }

    [Fact]
    public async Task Finner_navneformens_forekomster_med_rettskilde_og_node()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetId = await NyVirksomhetAsync(db, $"Whereused kommune {Guid.NewGuid()}");
        var tittel = $"Whereused-forskrift {Guid.NewGuid()}";
        var (rettskildeId, nodeEid, tekst) = await NyRettskildeMedNodeAsync(db, tittel, "Karasjok er med.");
        var navneform = await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(
            virksomhetId, "Karasjok", "test", skosUrl: null, navneformgrunn: "kortform");
        await NyNavneformTaggAsync(db, rettskildeId, nodeEid, tekst, virksomhetId, navneform.Id, 0, 8);

        var resultat = await new VirksomhetWhereUsedTjeneste(db).HentAsync(virksomhetId);

        var forekomst = Assert.Single(resultat.NavneformForekomster);
        Assert.Equal(navneform.Id, forekomst.NavneformId);
        Assert.Equal("Karasjok", forekomst.Term);
        Assert.Equal("kortform", forekomst.Navneformgrunn);
        Assert.Equal(rettskildeId, forekomst.RettskildeId);
        Assert.Equal(tittel, forekomst.RettskildeTittel);
        Assert.Equal(nodeEid, forekomst.NodeEid);
        Assert.Equal("Karasjok", forekomst.QuoteExact);
    }

    /// <summary>
    /// Bulk-poenget: ETT kall dekker ALLE virksomhetens navneformer, også når de er tagget i ULIKE
    /// rettskilder. Ville dette krevd ett kall per navneform, var det nettopp N+1-en endepunktet
    /// finnes for å unngå.
    /// </summary>
    [Fact]
    public async Task Dekker_alle_navneformene_i_ett_oppslag()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetId = await NyVirksomhetAsync(db, $"Bulk kommune {Guid.NewGuid()}");
        var register = new VirksomhetsbegrepTjeneste(db);

        var kortform = await register.OpprettVirksomhetsbegrepAsync(
            virksomhetId, "Statsforvalter", "test", skosUrl: null, navneformgrunn: "gjeldende");
        var utgatt = await register.OpprettVirksomhetsbegrepAsync(
            virksomhetId, "Fylkesmann", "test", skosUrl: null, navneformgrunn: "utgatt");

        var (rk1, node1, tekst1) = await NyRettskildeMedNodeAsync(
            db, $"Bulk A {Guid.NewGuid()}", "Statsforvalter gjør noe.");
        await NyNavneformTaggAsync(db, rk1, node1, tekst1, virksomhetId, kortform.Id, 0, 14);

        var (rk2, node2, tekst2) = await NyRettskildeMedNodeAsync(
            db, $"Bulk B {Guid.NewGuid()}", "Fylkesmann gjorde noe.");
        await NyNavneformTaggAsync(db, rk2, node2, tekst2, virksomhetId, utgatt.Id, 0, 10);

        var resultat = await new VirksomhetWhereUsedTjeneste(db).HentAsync(virksomhetId);

        Assert.Equal(2, resultat.NavneformForekomster.Count);
        Assert.Contains(resultat.NavneformForekomster, f => f.NavneformId == kortform.Id && f.RettskildeId == rk1);
        Assert.Contains(resultat.NavneformForekomster, f => f.NavneformId == utgatt.Id && f.RettskildeId == rk2);
    }

    /// <summary>Samme navneform tagget i TO paragrafer gir TO rader — «hvor er den brukt» er et
    /// spørsmål per forekomst, ikke per navneform (samme «hjemmel per RAD»-holdning som docs/09 §16).</summary>
    [Fact]
    public async Task Gir_en_rad_per_forekomst_ikke_per_navneform()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetId = await NyVirksomhetAsync(db, $"Toforekomster {Guid.NewGuid()}");
        var navneform = await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(
            virksomhetId, "Tana", "test");
        var (rettskildeId, nodeEid, tekst) = await NyRettskildeMedNodeAsync(
            db, $"Toforekomster forskrift {Guid.NewGuid()}", "Tana og Tana igjen.");

        await NyNavneformTaggAsync(db, rettskildeId, nodeEid, tekst, virksomhetId, navneform.Id, 0, 4);
        await NyNavneformTaggAsync(db, rettskildeId, nodeEid, tekst, virksomhetId, navneform.Id, 8, 12);

        var resultat = await new VirksomhetWhereUsedTjeneste(db).HentAsync(virksomhetId);

        Assert.Equal(2, resultat.NavneformForekomster.Count);
        Assert.All(resultat.NavneformForekomster, f => Assert.Equal(navneform.Id, f.NavneformId));

        // De to radene ligger i SAMME node, så (navneform, rettskilde, node) er IKKE unikt — det er
        // offsetene som skiller dem. Asserteres eksplisitt fordi klienten nøkler radene på nettopp
        // dette: uten offsetene fikk de to radene identisk React-key. Se «Én rad per FOREKOMST» i
        // VirksomhetWhereUsedTjeneste.
        Assert.Single(resultat.NavneformForekomster.Select(f => f.NodeEid).Distinct());
        Assert.Equal(
            [0, 8],
            resultat.NavneformForekomster.Select(f => f.StartOffset).OrderBy(o => o).ToArray());
        Assert.Equal(
            2,
            resultat.NavneformForekomster
                .Select(f => (f.NavneformId, f.RettskildeId, f.NodeEid, f.StartOffset))
                .Distinct()
                .Count());
    }

    /// <summary>En navneform som ikke er tagget noe sted gir ingen forekomst-rad — visningen skiller
    /// selv «ikke tagget» fra «laster» (§15), og tjenesten skal ikke oppfinne en tom rad.</summary>
    [Fact]
    public async Task Navneform_uten_tagg_gir_ingen_forekomst()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetId = await NyVirksomhetAsync(db, $"Utagget {Guid.NewGuid()}");
        await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(virksomhetId, "Utagget navn", "test");

        var resultat = await new VirksomhetWhereUsedTjeneste(db).HentAsync(virksomhetId);

        Assert.Empty(resultat.NavneformForekomster);
    }

    /// <summary>
    /// En annen virksomhets navneform/tagg skal ALDRI lekke inn — oppslaget er scopet til
    /// virksomheten det spørres om.
    /// </summary>
    [Fact]
    public async Task Lekker_ikke_en_annen_virksomhets_forekomster()
    {
        await using var db = _fixture.NyDbContext();
        var min = await NyVirksomhetAsync(db, $"Min kommune {Guid.NewGuid()}");
        var annen = await NyVirksomhetAsync(db, $"Annen kommune {Guid.NewGuid()}");
        var annenNavneform = await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(
            annen, "Annen kortform", "test");
        var (rettskildeId, nodeEid, tekst) = await NyRettskildeMedNodeAsync(
            db, $"Lekkasjetest {Guid.NewGuid()}", "Annen kortform er med.");
        await NyNavneformTaggAsync(db, rettskildeId, nodeEid, tekst, annen, annenNavneform.Id, 0, 14);

        var resultat = await new VirksomhetWhereUsedTjeneste(db).HentAsync(min);

        Assert.Empty(resultat.NavneformForekomster);
    }

    /// <summary>Kriterium 6: gruppetildelingen skal navngi GRUPPEN — den opplysningen
    /// myndighetstildelings-tabellen manglet.</summary>
    [Fact]
    public async Task Navngir_gruppebegrepet_for_hver_myndighetstildeling()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetId = await NyVirksomhetAsync(db, $"Gruppetildeling kommune {Guid.NewGuid()}");
        var (rettskildeId, nodeEid, _) = await NyRettskildeMedNodeAsync(
            db, $"Gruppetildeling forskrift {Guid.NewGuid()}", "Noe tekst.");

        var gruppeTerm = $"språkutviklingskommuner {Guid.NewGuid():N}";
        var gruppe = new BegrepEntitet
        {
            Id = Guid.NewGuid(),
            Begrepskategori = "gruppe",
            Term = gruppeTerm,
            LovkildeId = rettskildeId,
            Status = "utkast",
            OpprettetAv = "test",
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(gruppe);
        await db.SaveChangesAsync();

        var tildeling = await new MyndighetstildelingTjeneste(db).OpprettAsync(
            gruppe.Id, virksomhetId, rettskildeId, [new ParagrafspennPar(nodeEid, null)], vilkaar: null, "test");

        var resultat = await new VirksomhetWhereUsedTjeneste(db).HentAsync(virksomhetId);

        var rad = Assert.Single(resultat.Gruppetildelinger);
        Assert.Equal(tildeling.Id, rad.TildelingId);
        Assert.Equal(gruppe.Id, rad.GruppeBegrepId);
        Assert.Equal(gruppeTerm, rad.GruppeTerm);
    }
}
