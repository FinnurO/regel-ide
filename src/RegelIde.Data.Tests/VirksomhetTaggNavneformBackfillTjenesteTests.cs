using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="VirksomhetTaggNavneformBackfillTjeneste"/> — flyttingen av <c>Kind='virksomhet'</c>-
/// taggers <see cref="TekstTaggEntitet.RefId"/> fra <see cref="Virksomhet"/> til NAVNEFORMEN.
///
/// <para>
/// Radene skrives med <c>db.TekstTagger.Add</c> og RefId satt DIREKTE til virksomhetens id — altså
/// den gamle, nå ugyldige formen. Det er nødvendig og bevisst: etter endringen ville
/// <see cref="TekstTaggTjeneste.KobleTilEntitetAsync"/> AVVIST en slik kobling, så en «gammel» rad kan
/// ikke lenger lages gjennom tjenestelaget. Testen må derfor konstruere utgangstilstanden direkte, som
/// er nøyaktig hva den gjør — den simulerer en database skrevet av forrige versjon av koden.
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class VirksomhetTaggNavneformBackfillTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public VirksomhetTaggNavneformBackfillTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// En rettskilde + node å hylle taggene på. Egen, GUID-unik rettskilde per test — den delte
    /// DataTestCollection-databasen betyr at tjenesten ser ALLE 'virksomhet'-tagger i basen, så hver
    /// test må assertere på SINE EGNE rader (aldri på totaler) og ikke kunne forstyrre andre.
    /// </summary>
    private static async Task<(Guid RettskildeId, string NodeEid, string Tekst)> NyRettskildeMedNodeAsync(
        RegelIdeDbContext db, string tekst)
    {
        var rettskildeId = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId,
            Doctype = "act",
            Kildetype = "Lov",
            Tittel = $"Kjedemigreringstest {rettskildeId}",
            Eli = $"https://lovdata.no/eli/lov/2026/01/01/{Math.Abs(rettskildeId.GetHashCode())}/nor",
            Status = "Gjeldende",
            Importrolle = "referanse", // ck_rettskilder_akn_xml krever dette når AknXml er NULL.
            OpprettetAv = "test",
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var nodeEid = $"kjedetest/{rettskildeId}/§1/ledd-1";
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

    /// <summary>Legger inn en tagg i den GAMLE formen — RefId direkte på virksomheten.</summary>
    private static async Task<Guid> LeggTilGammelTaggAsync(
        RegelIdeDbContext db, Guid rettskildeId, string nodeEid, string tekst, Guid eierVirksomhetId,
        Guid refId, int start, int slutt)
    {
        var taggId = Guid.NewGuid();
        db.TekstTagger.Add(new TekstTaggEntitet
        {
            Id = taggId,
            VirksomhetId = eierVirksomhetId,
            RettskildeId = rettskildeId,
            NodeEid = nodeEid,
            StartOffset = start,
            EndOffset = slutt,
            QuotePrefix = "",
            QuoteExact = tekst[start..slutt],
            QuoteSuffix = "",
            NodeTekstHash = LovdataIdentifikatorer.BeregnTekstHash(tekst),
            Kind = "virksomhet",
            RefId = refId,
            OpprettetAv = "test",
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return taggId;
    }

    private static async Task<Guid> NyVirksomhetAsync(RegelIdeDbContext db, string navn)
    {
        var id = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = id, Navn = navn, OpprettetTidspunkt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Flytter_RefId_fra_virksomheten_til_navneformen()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetId = await NyVirksomhetAsync(db, $"Flyttetest kommune {Guid.NewGuid()}");
        var (rettskildeId, nodeEid, tekst) = await NyRettskildeMedNodeAsync(db, "Karasjok er med.");
        var taggId = await LeggTilGammelTaggAsync(db, rettskildeId, nodeEid, tekst, virksomhetId, virksomhetId, 0, 8);

        var navneform = await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(
            virksomhetId, "Karasjok", "test", skosUrl: null, navneformgrunn: "kortform");

        var resultat = await VirksomhetTaggNavneformBackfillTjeneste.KjorAsync(db);

        var min = Assert.Single(resultat.Flyttet, f => f.TaggId == taggId);
        Assert.Equal(virksomhetId, min.FraVirksomhetId);
        Assert.Equal(navneform.Id, min.TilNavneformId);
        Assert.Equal("Karasjok", min.QuoteExact);

        var lagret = await db.TekstTagger.SingleAsync(t => t.Id == taggId);
        Assert.Equal(navneform.Id, lagret.RefId);
        Assert.Equal("virksomhet", lagret.Kind); // laget er UENDRET — bare referansemålet flyttes.
    }

    /// <summary>Kriterium 3: idempotent — andre kjøring flytter ingenting og teller raden som
    /// «allerede flyttet», ikke som et problem.</summary>
    [Fact]
    public async Task Er_idempotent_andre_kjoring_flytter_ingenting()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetId = await NyVirksomhetAsync(db, $"Idempotenstest kommune {Guid.NewGuid()}");
        var (rettskildeId, nodeEid, tekst) = await NyRettskildeMedNodeAsync(db, "Kautokeino er med.");
        var taggId = await LeggTilGammelTaggAsync(db, rettskildeId, nodeEid, tekst, virksomhetId, virksomhetId, 0, 10);
        var navneform = await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(
            virksomhetId, "Kautokeino", "test");

        var forste = await VirksomhetTaggNavneformBackfillTjeneste.KjorAsync(db);
        var andre = await VirksomhetTaggNavneformBackfillTjeneste.KjorAsync(db);

        Assert.Contains(forste.Flyttet, f => f.TaggId == taggId);
        Assert.DoesNotContain(andre.Flyttet, f => f.TaggId == taggId);
        Assert.DoesNotContain(andre.Uflyttbare, u => u.TaggId == taggId); // ikke et problem, bare ferdig.

        var lagret = await db.TekstTagger.SingleAsync(t => t.Id == taggId);
        Assert.Equal(navneform.Id, lagret.RefId);
    }

    /// <summary>
    /// Kriterium 3: en rad UTEN matchende navneform står URØRT og RAPPORTERES — ingen gjettet
    /// fallback, og ingen navneform opprettes for å få migreringen til å se komplett ut.
    /// </summary>
    [Fact]
    public async Task Rad_uten_matchende_navneform_star_urort_og_rapporteres()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetId = await NyVirksomhetAsync(db, $"Uflyttbar kommune {Guid.NewGuid()}");
        var (rettskildeId, nodeEid, tekst) = await NyRettskildeMedNodeAsync(db, "Nesseby er med.");
        var taggId = await LeggTilGammelTaggAsync(db, rettskildeId, nodeEid, tekst, virksomhetId, virksomhetId, 0, 7);
        // Ingen navneform opprettet for «Nesseby».

        var antallNavneformerFor = await db.Begreper.CountAsync(b => b.VirksomhetReferanseId == virksomhetId);
        var resultat = await VirksomhetTaggNavneformBackfillTjeneste.KjorAsync(db);

        var rapportert = Assert.Single(resultat.Uflyttbare, u => u.TaggId == taggId);
        Assert.Contains("Nesseby", rapportert.Grunn);
        Assert.Equal(virksomhetId, rapportert.RefId);

        var lagret = await db.TekstTagger.SingleAsync(t => t.Id == taggId);
        Assert.Equal(virksomhetId, lagret.RefId); // ordrett urørt.
        Assert.Equal(antallNavneformerFor, await db.Begreper.CountAsync(b => b.VirksomhetReferanseId == virksomhetId));
    }

    /// <summary>
    /// Matchingen krever BÅDE riktig term OG riktig virksomhet. Her finnes en navneform med samme
    /// TERM, men for en ANNEN virksomhet — den skal IKKE brukes.
    /// </summary>
    [Fact]
    public async Task Navneform_med_samme_term_men_annen_virksomhet_brukes_ikke()
    {
        await using var db = _fixture.NyDbContext();
        var riktig = await NyVirksomhetAsync(db, $"Riktig kommune {Guid.NewGuid()}");
        var feil = await NyVirksomhetAsync(db, $"Feil kommune {Guid.NewGuid()}");
        var term = $"Tvetydig{Guid.NewGuid():N}";
        var (rettskildeId, nodeEid, tekst) = await NyRettskildeMedNodeAsync(db, $"{term} er med.");
        var taggId = await LeggTilGammelTaggAsync(db, rettskildeId, nodeEid, tekst, riktig, riktig, 0, term.Length);

        // Navneformen finnes KUN for den andre virksomheten.
        await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(feil, term, "test");

        var resultat = await VirksomhetTaggNavneformBackfillTjeneste.KjorAsync(db);

        Assert.Single(resultat.Uflyttbare, u => u.TaggId == taggId);
        var lagret = await db.TekstTagger.SingleAsync(t => t.Id == taggId);
        Assert.Equal(riktig, lagret.RefId); // urørt.
    }

    /// <summary>
    /// Ved SYNONYMER velges navneformen med nøyaktig samme term som taggens QuoteExact — ikke en
    /// vilkårlig av virksomhetens navneformer. Dette er hele grunnen til at kjeden må være eksplisitt.
    /// </summary>
    [Fact]
    public async Task Velger_navneformen_med_samme_term_ved_synonymer()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetId = await NyVirksomhetAsync(db, $"Synonymtest {Guid.NewGuid()}");
        var (rettskildeId, nodeEid, tekst) = await NyRettskildeMedNodeAsync(db, "Statsforvalter og Fylkesmann.");
        // Taggen dekker «Statsforvalter» (0..14).
        var taggId = await LeggTilGammelTaggAsync(db, rettskildeId, nodeEid, tekst, virksomhetId, virksomhetId, 0, 14);

        var register = new VirksomhetsbegrepTjeneste(db);
        var utgatt = await register.OpprettVirksomhetsbegrepAsync(
            virksomhetId, "Fylkesmann", "test", skosUrl: null, navneformgrunn: "utgatt");
        var gjeldende = await register.OpprettVirksomhetsbegrepAsync(
            virksomhetId, "Statsforvalter", "test", skosUrl: null, navneformgrunn: "gjeldende");

        var resultat = await VirksomhetTaggNavneformBackfillTjeneste.KjorAsync(db);

        var min = Assert.Single(resultat.Flyttet, f => f.TaggId == taggId);
        Assert.Equal(gjeldende.Id, min.TilNavneformId);
        Assert.NotEqual(utgatt.Id, min.TilNavneformId);
    }

    /// <summary>En tagg som alt peker på en navneform (opprettet av den NYE koden) skal ikke røres,
    /// og skal ikke rapporteres som uflyttbar.</summary>
    [Fact]
    public async Task Rorer_ikke_en_tagg_som_alt_peker_pa_en_navneform()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetId = await NyVirksomhetAsync(db, $"Alleredeflyttet {Guid.NewGuid()}");
        var (rettskildeId, nodeEid, tekst) = await NyRettskildeMedNodeAsync(db, "Tana er med.");
        var navneform = await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(
            virksomhetId, "Tana", "test");
        var taggId = await LeggTilGammelTaggAsync(
            db, rettskildeId, nodeEid, tekst, virksomhetId, navneform.Id, 0, 4);

        var resultat = await VirksomhetTaggNavneformBackfillTjeneste.KjorAsync(db);

        Assert.DoesNotContain(resultat.Flyttet, f => f.TaggId == taggId);
        Assert.DoesNotContain(resultat.Uflyttbare, u => u.TaggId == taggId);
        var lagret = await db.TekstTagger.SingleAsync(t => t.Id == taggId);
        Assert.Equal(navneform.Id, lagret.RefId);
    }

    /// <summary>
    /// En arkivert tagg røres ikke — samme <c>Entitetsstatus == "gjeldende"</c>-avgrensning som resten
    /// av tagg-spørringene bruker.
    /// </summary>
    [Fact]
    public async Task Rorer_ikke_en_arkivert_tagg()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetId = await NyVirksomhetAsync(db, $"Arkivert {Guid.NewGuid()}");
        var (rettskildeId, nodeEid, tekst) = await NyRettskildeMedNodeAsync(db, "Porsanger er med.");
        var taggId = await LeggTilGammelTaggAsync(db, rettskildeId, nodeEid, tekst, virksomhetId, virksomhetId, 0, 9);
        await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(virksomhetId, "Porsanger", "test");

        var tagg = await db.TekstTagger.SingleAsync(t => t.Id == taggId);
        tagg.Entitetsstatus = "arkivert";
        await db.SaveChangesAsync();

        var resultat = await VirksomhetTaggNavneformBackfillTjeneste.KjorAsync(db);

        Assert.DoesNotContain(resultat.Flyttet, f => f.TaggId == taggId);
        var lagret = await db.TekstTagger.SingleAsync(t => t.Id == taggId);
        Assert.Equal(virksomhetId, lagret.RefId);
    }

    /// <summary>
    /// Dublett-tilfellet, observert i praksis ved første oppstart etter denne runden: seeden hadde
    /// laget en NY tagg (mot navneformen) ved siden av den GAMLE (mot virksomheten), på nøyaktig samme
    /// posisjon. Den gamle kan da ikke flyttes — tekst_tagger_unik_tagg ville blitt brutt — men den
    /// skal heller ikke bli stående som 'gjeldende', for da vises samme ord tagget to ganger og den ene
    /// lar seg ikke resolve. Den ARKIVERES.
    /// </summary>
    [Fact]
    public async Task Arkiverer_foreldet_dublett_nar_posisjonen_alt_er_riktig_tagget()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhetId = await NyVirksomhetAsync(db, $"Dublett kommune {Guid.NewGuid()}");
        var (rettskildeId, nodeEid, tekst) = await NyRettskildeMedNodeAsync(db, "Snasa er med.");
        var navneform = await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(
            virksomhetId, "Snasa", "test", skosUrl: null, navneformgrunn: "kortform");

        // Den GAMLE raden (mot virksomheten) og den NYE (mot navneformen), samme posisjon.
        var gammelTaggId = await LeggTilGammelTaggAsync(
            db, rettskildeId, nodeEid, tekst, virksomhetId, virksomhetId, 0, 5);
        var nyTaggId = await LeggTilGammelTaggAsync(
            db, rettskildeId, nodeEid, tekst, virksomhetId, navneform.Id, 0, 5);

        var resultat = await VirksomhetTaggNavneformBackfillTjeneste.KjorAsync(db);

        var arkivert = Assert.Single(resultat.ArkiverteDubletter, a => a.TaggId == gammelTaggId);
        Assert.Equal(navneform.Id, arkivert.NavneformId);
        Assert.DoesNotContain(resultat.Uflyttbare, u => u.TaggId == gammelTaggId); // ikke et problem — løst.

        var gammel = await db.TekstTagger.SingleAsync(t => t.Id == gammelTaggId);
        Assert.Equal("arkivert", gammel.Entitetsstatus);
        Assert.Equal(virksomhetId, gammel.RefId); // RefId urørt — raden er arkivert, ikke omskrevet.

        // Den riktige raden står igjen som den ENESTE gjeldende taggen på posisjonen.
        var gjeldende = await db.TekstTagger
            .Where(t => t.RettskildeId == rettskildeId && t.NodeEid == nodeEid
                        && t.StartOffset == 0 && t.EndOffset == 5 && t.Entitetsstatus == "gjeldende")
            .ToListAsync();
        var eneste = Assert.Single(gjeldende);
        Assert.Equal(nyTaggId, eneste.Id);
        Assert.Equal(navneform.Id, eneste.RefId);

        // Arkiveringen er også idempotent: andre kjøring ser ikke den arkiverte raden i det hele tatt.
        var andre = await VirksomhetTaggNavneformBackfillTjeneste.KjorAsync(db);
        Assert.DoesNotContain(andre.ArkiverteDubletter, a => a.TaggId == gammelTaggId);
    }
}
