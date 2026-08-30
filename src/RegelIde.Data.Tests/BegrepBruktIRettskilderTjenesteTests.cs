using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// «Brukt i rettskilder» (<see cref="BegrepBruktIRettskilderTjeneste"/>) — ekte reverse-oppslag av et
/// Begrep sin <see cref="BegrepEntitet.Term"/> i faktisk lovtekst. Samme «DELT embedded Postgres mellom
/// alle tester i samlingen»-forbehold som <c>NavnekandidatOppdagelseTjenesteTests</c>: hver test bruker
/// sin EGEN, ferske, syntetiske rettskilde med et unikt, oppdiktet Term/institusjonsnavn, slik at
/// resultatet forblir deterministisk uansett kjørerekkefølge og hva andre testklassers fixturer måtte
/// ha lagt inn i samme DB.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class BegrepBruktIRettskilderTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public BegrepBruktIRettskilderTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<Guid> OpprettRettskildeMedNodeAsync(
        RegelIdeDbContext db, string tekst, string entitetsstatus = "gjeldende", string? eid = null)
    {
        var rettskildeId = Guid.NewGuid();
        var nodeEid = eid ?? $"https://test/{Guid.NewGuid():N}/§1/ledd-1";
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse",
            Entitetsstatus = entitetsstatus, Tittel = "Testlov " + rettskildeId,
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = nodeEid, KildeId = "ledd-1",
            NodeType = "ledd", Tekst = tekst,
        });
        await db.SaveChangesAsync();
        return rettskildeId;
    }

    private static async Task<Guid> OpprettBegrepAsync(RegelIdeDbContext db, string term)
    {
        var virksomhetId = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhetId, Navn = "Testvirksomhet " + virksomhetId });
        var begrep = new BegrepEntitet
        {
            Id = Guid.NewGuid(), VirksomhetId = virksomhetId, Term = term, Definisjon = "En test-definisjon.",
            Begrepstype = "faktabegrep", Status = "utkast", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(begrep);
        await db.SaveChangesAsync();
        return begrep.Id;
    }

    [Fact]
    public async Task Finner_kjent_treff_i_importert_testrettskilde()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(
            db, "Søknad om saeretervervstillatelse behandles av kommunen innen fire uker.");
        var begrepId = await OpprettBegrepAsync(db, "saeretervervstillatelse");

        var tjeneste = new BegrepBruktIRettskilderTjeneste(db);
        var treff = await tjeneste.FinnAsync(begrepId);

        var enkelt = Assert.Single(treff);
        Assert.Equal(rettskildeId, enkelt.RettskildeId);
        Assert.Contains("saeretervervstillatelse", enkelt.Snippet, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Respekterer_ordgrense_substreng_av_lengre_ord_gir_ikke_treff()
    {
        await using var db = _fixture.NyDbContext();
        // "bevillingshaaandtering" inneholder termen "haandter" som substreng — skal IKKE telle som
        // treff (samme presisjonsprinsipp som ordgrense-sveipene i VirksomhetKandidatSveipTjeneste/
        // NavnekandidatOppdagelseTjeneste: en hel, ordgrense-avgrenset streng, ikke en delstreng).
        await OpprettRettskildeMedNodeAsync(db, "Reglene om bevillingshaandteringsprosessxyz gjelder for alle søknader.");
        var begrepId = await OpprettBegrepAsync(db, "haandteringsprosess");

        var tjeneste = new BegrepBruktIRettskilderTjeneste(db);
        var treff = await tjeneste.FinnAsync(begrepId);

        Assert.Empty(treff);
    }

    [Fact]
    public async Task Returnerer_tomt_for_begrep_som_ikke_forekommer_noe_sted()
    {
        await using var db = _fixture.NyDbContext();
        await OpprettRettskildeMedNodeAsync(db, "Denne teksten nevner ingenting relevant her.");
        var begrepId = await OpprettBegrepAsync(db, "et-helt-oppdiktet-begrep-xyzabc123");

        var tjeneste = new BegrepBruktIRettskilderTjeneste(db);
        var treff = await tjeneste.FinnAsync(begrepId);

        Assert.Empty(treff);
    }

    [Fact]
    public async Task Respekterer_entitetsstatus_filteret_ingen_treff_fra_en_erstattet_rettskilde()
    {
        await using var db = _fixture.NyDbContext();
        // Samme scenario som NavnekandidatOppdagelseTjenesteTests sin
        // "Sveip_hopper_over_noder_fra_en_erstattet_rettskilde": rettskildens EGEN Entitetsstatus er
        // "erstattet" (reimportert), mens noden selv fortsatt står som "gjeldende" (noder endrer aldri
        // dette ved reimport av Lov/Forskrift) — begge må sjekkes, ikke bare nodens.
        await OpprettRettskildeMedNodeAsync(
            db, "Søknad om vindkraftlisenstillatelse behandles av kommunen innen fire uker.",
            entitetsstatus: "erstattet");
        var begrepId = await OpprettBegrepAsync(db, "vindkraftlisenstillatelse");

        var tjeneste = new BegrepBruktIRettskilderTjeneste(db);
        var treff = await tjeneste.FinnAsync(begrepId);

        Assert.Empty(treff);
    }

    [Fact]
    public async Task Case_insensitivt_treff_uansett_store_smaa_bokstaver()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettRettskildeMedNodeAsync(db, "Vedtak om Kystfartoeyregistrering fattes av departementet.");
        var begrepId = await OpprettBegrepAsync(db, "kystfartoeyregistrering");

        var tjeneste = new BegrepBruktIRettskilderTjeneste(db);
        var treff = await tjeneste.FinnAsync(begrepId);

        var enkelt = Assert.Single(treff);
        Assert.Equal(rettskildeId, enkelt.RettskildeId);
    }

    [Fact]
    public async Task Returnerer_tomt_for_begrep_som_ikke_finnes()
    {
        await using var db = _fixture.NyDbContext();
        var tjeneste = new BegrepBruktIRettskilderTjeneste(db);
        var treff = await tjeneste.FinnAsync(Guid.NewGuid());
        Assert.Empty(treff);
    }
}
