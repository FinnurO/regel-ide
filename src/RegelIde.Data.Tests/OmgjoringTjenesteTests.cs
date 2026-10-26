using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #386] Mot ekte embedded Postgres. Spørsmålene modellen skal kunne besvare etter en omgjøring:
/// <list type="bullet">
/// <item>Er «Tilsynsutvalget for dommere» en aktør, og hva heter den i loven? En aktør med gjeldende navneform og kortform, som forslag,
/// hjemlet der navnet står.</item>
/// <item>Hvor står den? Taggen som pekte på rollebegrepet, peker nå på navneformen og dekker hele navnet; kortformen tagges der den står
/// alene i samme rettskilde. Kanten som gikk fra rollen, går fra aktøren.</item>
/// <item>Kan det angres? Begrepet, taggene og kantene settes tilbake fra proveniensen; ingenting er slettet.</item>
/// </list>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class OmgjoringTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public OmgjoringTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static string Unik() => new string(Guid.NewGuid().ToString("N").Where(char.IsLetter).Take(8).ToArray()).PadRight(8, 'x');

    private static (OmgjoringTjeneste Omgjoring, AktoridentitetTjeneste Identitet) Tjenester(RegelIdeDbContext db)
    {
        var identitet = new AktoridentitetTjeneste(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)), new VirksomhetOppslagTjeneste(db));
        return (new OmgjoringTjeneste(db, identitet), identitet);
    }

    private sealed record Oppsett(Guid RettskildeId, string Eid, string Tekst, Guid RolleId, Guid TaggId, Guid KantId, string Fullt, string Kort);

    private static async Task<Oppsett> SettOppAsync(RegelIdeDbContext db)
    {
        var u = Unik();
        var kort = $"Testutvalget{u}";
        var fullt = $"{kort} for dommere";
        var tekst = $"{fullt} behandler klager. {kort} skal bestå av fem medlemmer. {kort}s vedtak er endelige.";
        var eier = new Virksomhet { Id = Guid.NewGuid(), Navn = $"TESTDEPARTEMENTET{u.ToUpperInvariant()}" };
        db.Virksomheter.Add(eier);
        var rk = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rk, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse", Tittel = "Testlov " + rk,
            AnsvarligDepartement = [eier.Navn], OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var eid = $"https://test/{rk:N}/§235/ledd-1";
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rk, Eid = eid, KildeId = "ledd-1", NodeType = "ledd", Tekst = tekst, Sorteringsrekkefolge = 0,
        });
        var rolle = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Term = fullt, Begrepskategori = "rolle", LovkildeId = rk, LovreferanseEid = eid, Status = "publisert",
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(rolle);
        var tagg = new TekstTaggEntitet
        {
            Id = Guid.NewGuid(), VirksomhetId = eier.Id, RettskildeId = rk, NodeEid = eid, StartOffset = 0, EndOffset = kort.Length,
            QuotePrefix = "", QuoteExact = kort, QuoteSuffix = " for dommere", NodeTekstHash = "x", Kind = "begrep", RefId = rolle.Id,
            OpprettetAv = "test",
        };
        db.TekstTagger.Add(tagg);
        var kant = new StrukturkantEntitet
        {
            Id = Guid.NewGuid(), Kategori = "K", Typekode = "normgivning", FraBegrepId = rolle.Id, Objekt = "forskrift om noe",
            HjemmelRettskildeId = rk, HjemmelEid = eid, Status = Strukturkanter.MaskineltForeslatt, OppdagelsesKilde = "test", OpprettetAv = "test",
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Strukturkanter.Add(kant);
        await db.SaveChangesAsync();
        return new Oppsett(rk, eid, tekst, rolle.Id, tagg.Id, kant.Id, fullt, kort);
    }

    private static OmgjoringTilAktor Spesifikasjon(Oppsett o) => new(o.Fullt, null,
    [
        new OmgjortNavneform(o.Fullt, "gjeldende", new Utsagnsgrunnlag
        {
            Grunnlag = Utsagnsgrunnlag.Hjemmel, HjemmelRettskildeId = o.RettskildeId, HjemmelEid = o.Eid,
            Kunnskapsstatus = Strukturkanter.Tekstfunn, Godkjenning = Strukturkanter.ManueltForeslatt,
        }),
        new OmgjortNavneform(o.Kort, "kortform", new Utsagnsgrunnlag
        {
            Grunnlag = Utsagnsgrunnlag.Hjemmel, HjemmelRettskildeId = o.RettskildeId, HjemmelEid = o.Eid,
            Kunnskapsstatus = Strukturkanter.Fortolkning, Tolkningsargumenter = ["ordlyd", "kontekst_system"],
            Begrunnelse = "Kortformen brukes om organet i samme ledd.", Godkjenning = Strukturkanter.ManueltForeslatt,
        }),
    ], "organ", new Utsagnsgrunnlag
    {
        Grunnlag = Utsagnsgrunnlag.Hjemmel, HjemmelRettskildeId = o.RettskildeId, HjemmelEid = o.Eid,
        Kunnskapsstatus = Strukturkanter.Fortolkning, Tolkningsargumenter = ["ordlyd", "kontekst_system"],
        Begrunnelse = "Behandler klager og treffer enkeltvedtak.", Godkjenning = Strukturkanter.ManueltForeslatt,
    });

    [Fact]
    public async Task Rollebegrep_blir_aktor_med_navneformer_taggene_folger_og_kanten_flyttes()
    {
        await using var db = _fixture.NyDbContext();
        var o = await SettOppAsync(db);

        var r = await Tjenester(db).Omgjoring.OmgjorTilAktorAsync(o.RolleId, Spesifikasjon(o), "test");

        Assert.True(r.AktorVarNy);
        Assert.Equal((1, 1, 1), (r.TaggerFlyttet, r.TaggerUtvidet, r.KanterFlyttet));
        var gjeldende = r.Navneformer.Single(n => n.Navneformgrunn == "gjeldende");
        Assert.Equal((o.Fullt, Strukturkanter.ManueltForeslatt, Strukturkanter.Tekstfunn, (Guid?)o.RolleId),
            (gjeldende.Term, gjeldende.Godkjenning, gjeldende.Kunnskapsstatus, gjeldende.ErstatterId));

        await using var les = _fixture.NyDbContext();
        Assert.Equal("erstattet", (await les.Begreper.SingleAsync(b => b.Id == o.RolleId)).Entitetsstatus);
        var tagg = await les.TekstTagger.SingleAsync(t => t.Id == o.TaggId);
        Assert.Equal(("virksomhet", (Guid?)gjeldende.Id, 0, o.Fullt.Length, o.Fullt), (tagg.Kind, tagg.RefId, tagg.StartOffset, tagg.EndOffset, tagg.QuoteExact));
        var kant = await les.Strukturkanter.SingleAsync(k => k.Id == o.KantId);
        Assert.Equal(((Guid?)null, (Guid?)r.Aktor.Id), (kant.FraBegrepId, kant.FraVirksomhetId));
        Assert.Equal(2, await les.Proveniens.CountAsync(p => p.Handling == OmgjoringTjeneste.HandlingFlyttet
            && (p.EntitetId == o.TaggId || p.EntitetId == o.KantId)));

        // Kortformen tagges der den står alene (nøyaktig lik, hele ord): annet punktum, ikke «…s vedtak» og ikke inni det fulle navnet.
        var kortform = r.Navneformer.Single(n => n.Navneformgrunn == "kortform");
        var kortTagger = await les.TekstTagger.Where(t => t.RefId == kortform.Id).ToListAsync();
        Assert.Equal([o.Tekst.IndexOf($"{o.Kort} skal", StringComparison.Ordinal)], kortTagger.Select(t => t.StartOffset).ToList());

        var type = await les.AktortypeUtsagn.SingleAsync(a => a.VirksomhetId == r.Aktor.Id);
        Assert.Equal(("organ", Strukturkanter.ManueltForeslatt), (type.Aktortype, type.Status));
        Assert.Null((await les.Virksomheter.SingleAsync(v => v.Id == r.Aktor.Id)).Aktortype); // settes først ved godkjenning
    }

    [Fact]
    public async Task Omgjoringen_kan_angres_og_ingenting_er_slettet()
    {
        await using var db = _fixture.NyDbContext();
        var o = await SettOppAsync(db);
        var (omgjoring, identitet) = Tjenester(db);
        var r = await omgjoring.OmgjorTilAktorAsync(o.RolleId, Spesifikasjon(o), "test");
        var gjeldende = r.Navneformer.Single(n => n.Navneformgrunn == "gjeldende");

        // Å avvise navneformen direkte ville slettet den flyttede taggen; det stoppes.
        await Assert.ThrowsAsync<ArgumentException>(() => identitet.AvvisNavneformAsync(gjeldende.Id, "nei", "test"));

        var a = await omgjoring.AngreAsync(o.RolleId, null, "test");
        Assert.Equal((1, 1, 2, 1), (a.TaggerTilbake, a.KanterTilbake, a.NavneformerAvvist, a.AktortyperAvvist));

        await using var les = _fixture.NyDbContext();
        Assert.Equal("gjeldende", (await les.Begreper.SingleAsync(b => b.Id == o.RolleId)).Entitetsstatus);
        var tagg = await les.TekstTagger.SingleAsync(t => t.Id == o.TaggId);
        Assert.Equal(("begrep", (Guid?)o.RolleId, o.Kort.Length, o.Kort), (tagg.Kind, tagg.RefId, tagg.EndOffset, tagg.QuoteExact));
        var kant = await les.Strukturkanter.SingleAsync(k => k.Id == o.KantId);
        Assert.Equal(((Guid?)o.RolleId, (Guid?)null), (kant.FraBegrepId, kant.FraVirksomhetId));
        Assert.All(await les.Begreper.Where(b => b.VirksomhetReferanseId == r.Aktor.Id).ToListAsync(),
            b => Assert.Equal((Strukturkanter.Avvist, "arkivert"), (b.Godkjenning, b.Entitetsstatus)));
    }

    [Fact]
    public async Task Omgjoring_krever_gjeldende_navneform_og_et_nodebegrep()
    {
        await using var db = _fixture.NyDbContext();
        var o = await SettOppAsync(db);
        var (omgjoring, _) = Tjenester(db);
        var uten = Spesifikasjon(o) with { Navneformer = [Spesifikasjon(o).Navneformer[1]] };
        await Assert.ThrowsAsync<ArgumentException>(() => omgjoring.OmgjorTilAktorAsync(o.RolleId, uten, "test"));
        await omgjoring.OmgjorTilAktorAsync(o.RolleId, Spesifikasjon(o), "test");
        await Assert.ThrowsAsync<ArgumentException>(() => omgjoring.OmgjorTilAktorAsync(o.RolleId, Spesifikasjon(o), "test"));
    }
}
