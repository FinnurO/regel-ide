using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, Refs #486 PR 2] Tagg per forekomst av «statsråd», mot ekte embedded Postgres. Spørsmålene modellen skal kunne besvare etterpå:
/// <list type="number">
/// <item>Hva er «statsråd» i denne forekomsten? Kongen i statsråd, organet statsrådet, rollen statsråd eller slutning, som et forslag per tagg
/// med referent.</item>
/// <item>Tagges forekomsten i «Kongen i statsråd»? Nei: den hører til aktørens navneform.</item>
/// <item>Er skrivingen idempotent, og skriver tørrkjøringen ingenting?</item>
/// </list>
/// Testbasen er delt: påstandene gjelder testens egen rettskilde og de faste id-ene.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class StatsradTjenesteTests(EmbeddedPostgresFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var db = fixture.NyDbContext();
        await Strukturkanter.SeedStartsettAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static AktoridentitetTjeneste Identitet(RegelIdeDbContext db) =>
        new(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)), new VirksomhetOppslagTjeneste(db));

    /// <summary>Grunnloven §§ 12, 15, 28 og 32 (første ledd), staten, Kongen i statsråd med navneformen, og statsrådet og rollen fra
    /// <see cref="RegjeringenTjeneste"/>.</summary>
    private static async Task GrunnlagAsync(RegelIdeDbContext db)
    {
        async Task Ledd(string eid, string tekst)
        {
            if (await db.RettskildeNoder.AnyAsync(n => n.Eid == eid && n.Entitetsstatus == "gjeldende")) return;
            var r = new RettskildeEntitet
            {
                Id = Guid.NewGuid(), Doctype = "act", Kildetype = "Lov", Tittel = "Kongeriket Norges Grunnlov " + eid[^12..], Status = "Gjeldende",
                OpprettetAv = "test", AknXml = "<akomaNtoso/>",
            };
            db.Rettskilder.Add(r);
            db.RettskildeNoder.Add(new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = r.Id, Eid = eid, KildeId = "l1", NodeType = "ledd", Nummer = "1", Tekst = tekst, Sorteringsrekkefolge = 1,
            });
            await db.SaveChangesAsync();
        }
        await Ledd(RegjeringenTjeneste.Grl12Eid,
            "Kongen velger selv et råd av stemmeberettigede norske borgere. Dette råd skal bestå av en statsminister og i det minste syv andre medlemmer.");
        await Ledd(RegjeringenTjeneste.Grl15Eid,
            "Enhver som er medlem av statsrådet, har plikt til å innlevere sin avskjedssøknad etter at Stortinget har fattet beslutning om mistillit "
            + "til vedkommende statsråd alene eller til det samlede statsråd.");
        await Ledd(RegjeringenTjeneste.Grl32Eid,
            "De beslutninger som fattes av regjeringen under kongens fravær, utferdiges i kongens navn og undertegnes av statsrådet.");
        await Ledd(StatsradTjeneste.Grl28Eid,
            "Innstillinger om embetsutnevnelser og andre saker av viktighet skal foredras i statsrådet av det medlem til hvis fag de hører, og sakene skal "
            + "ekspederes av ham eller henne i overensstemmelse med den beslutning som er tatt i statsrådet.");
        if (!await db.Virksomheter.AnyAsync(v => v.Id == NorgeStatenTjeneste.StatenId))
        {
            db.Virksomheter.Add(new Virksomhet { Id = NorgeStatenTjeneste.StatenId, Navn = "staten", Aktiv = true });
            await db.SaveChangesAsync();
        }
        if (!await db.Begreper.AnyAsync(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende" && b.Term == RegjeringenTjeneste.KongenIStatsrad))
        {
            var kongen = new Virksomhet { Id = Guid.NewGuid(), Navn = RegjeringenTjeneste.KongenIStatsrad, Aktiv = true };
            db.Virksomheter.Add(kongen);
            db.Begreper.Add(new BegrepEntitet
            {
                Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = kongen.Id, Term = RegjeringenTjeneste.KongenIStatsrad,
                Status = "publisert", OpprettetAv = "test",
            });
            await db.SaveChangesAsync();
        }
        await new RegjeringenTjeneste(db, new StrukturkantTjeneste(db), Identitet(db)).KjorAsync(skriv: true, "test");
    }

    [Fact]
    public async Task En_tagg_per_forekomst_skrives_som_forslag_idempotent_og_torrkjoringen_skriver_ingenting()
    {
        await using var db = fixture.NyDbContext();
        await GrunnlagAsync(db);
        var dep = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Testdepartementet {Guid.NewGuid():N}", Aktiv = true };
        db.Virksomheter.Add(dep);
        var kilde = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = kilde, Doctype = "act", Kildetype = "Lov", Status = "Gjeldende", Tittel = "Testlov " + kilde, AnsvarligDepartement = [dep.Navn],
            AknXml = "<akomaNtoso/>", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        string[] tekster =
        [
            "Saker som vedtas i statsråd, kunngjøres. Kongen i statsråd kan gi forskrift.",
            "Er en statsråd forhindret, avgjør statsministeren. Statsråden skal varsles.",
            "Enhver som er medlem av statsrådet, kan kreve protokollen opplest. Kongen har rett til i statsrådet å benåde.",
            "Protokollen føres særskilt for hvert statsråd.",
        ];
        for (var i = 0; i < tekster.Length; i++)
        {
            db.RettskildeNoder.Add(new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = kilde, Eid = $"https://test/{kilde:N}/§{i + 1}/ledd-1", KildeId = $"ledd-{i + 1}", NodeType = "ledd",
                Tekst = tekster[i], Sorteringsrekkefolge = i,
            });
        }
        await db.SaveChangesAsync();

        var torr = await new StatsradTjeneste(db, Identitet(db)).KjorAsync(skriv: false, "test");
        Assert.False(torr.Skrevet);
        Assert.Equal(0, await db.TekstTagger.CountAsync(t => t.RettskildeId == kilde));
        var mine = torr.Forekomster.Where(f => f.RettskildeId == kilde).ToList();
        string? Lesning(string ord, int nr = 0) => mine.Where(f => f.Ord == ord).ElementAt(nr).Lesning;
        Assert.Equal(Forekomstlesninger.KongenIStatsrad, Lesning("statsråd", 0));
        Assert.Null(Lesning("statsråd", 1));                                                     // i «Kongen i statsråd»: ikke tagget
        Assert.Equal(Forekomstlesninger.RolleStatsrad, Lesning("statsråd", 2));                  // «en statsråd»
        Assert.Equal(Forekomstlesninger.RolleStatsrad, Lesning("Statsråden"));
        Assert.Equal(Forekomstlesninger.Statsradet, Lesning("statsrådet", 0));                   // «medlem av statsrådet»
        Assert.Equal(Forekomstlesninger.Slutning, Lesning("statsrådet", 1));                     // «i statsrådet»: møtet
        Assert.Equal(Forekomstlesninger.Slutning, Lesning("statsråd", 3));                       // «hvert statsråd»: møtet
        Assert.Equal(6, mine.Count(f => f.Binding == "ny"));

        var skrevet = await new StatsradTjeneste(db, Identitet(db)).KjorAsync(skriv: true, "test");
        Assert.True(skrevet.Skrevet);
        var bindinger = await db.NavneformBindinger
            .Join(db.TekstTagger.Where(t => t.RettskildeId == kilde), b => b.TekstTaggId, t => t.Id, (b, t) => b).ToListAsync();
        Assert.Equal(6, bindinger.Count);
        Assert.All(bindinger, b => Assert.Equal(Strukturkanter.MaskineltForeslatt, b.Status));
        Assert.Equal(RegjeringenTjeneste.StatsradRolleId, bindinger.Single(b => b.Lesning == Forekomstlesninger.RolleStatsrad && b.OppdagelsesKilde.Contains("rolle")
            && b.Begrunnelse!.Contains("«en statsråd»")).TilBegrepId);
        Assert.Equal(RegjeringenTjeneste.StatsradetId, bindinger.Single(b => b.Lesning == Forekomstlesninger.Statsradet).TilVirksomhetId);
        Assert.NotNull(bindinger.Single(b => b.Lesning == Forekomstlesninger.KongenIStatsrad).TilVirksomhetId);
        Assert.All(bindinger.Where(b => b.Lesning == Forekomstlesninger.Slutning), b => Assert.Null(b.TilVirksomhetId ?? b.TilBegrepId));

        var igjen = await new StatsradTjeneste(db, Identitet(db)).KjorAsync(skriv: true, "test");
        Assert.All(igjen.Forekomster.Where(f => f.RettskildeId == kilde && f.Lesning is not null), f => Assert.Equal("finnes", f.Binding));
        Assert.Equal("finnes", igjen.Navneform.Utfall);
    }
}
