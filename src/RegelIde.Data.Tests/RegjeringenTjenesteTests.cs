using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #486] Statsrådet og tagg per forekomst av «regjeringen», mot ekte embedded Postgres. Spørsmålene modellen skal kunne besvare
/// etterpå (S1):
/// <list type="number">
/// <item>Hvem er statsrådet, og hvor er det hjemlet? Et organ med navneformen «statsrådet» (Grl. § 15 første ledd) og aktørtypen organ
/// (Grl. § 12 første ledd), organ for staten (G har_organ). Alt som forslag.</item>
/// <item>Hva er «regjeringen» i denne forekomsten? Ordlydsregelen eller «slutning», som et forslag per tagg. «regjeringen» er ikke navneform for
/// noen aktør.</item>
/// <item>Juristens vilkår 1 og 2: en vurdert tagg per forekomst går foran nodens binding, og er bare noen av forekomstene i noden tagget, er
/// svaret «flertydig». Uten vurderte forekomstbindinger er oppslaget som før.</item>
/// <item>Er skrivingen idempotent, og skriver tørrkjøringen ingenting?</item>
/// </list>
/// Testbasen er delt: statsrådet har fast id, og «regjeringen» er én generisk navneform for hele basen. Påstandene gjelder testens egne
/// rettskilder og de faste id-ene.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class RegjeringenTjenesteTests(EmbeddedPostgresFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var db = fixture.NyDbContext();
        await Strukturkanter.SeedStartsettAsync(db);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static string Unik() => new string(Guid.NewGuid().ToString("N").Where(char.IsLetter).Take(8).ToArray()).PadRight(8, 'x');

    private static AktoridentitetTjeneste Identitet(RegelIdeDbContext db) =>
        new(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)), new VirksomhetOppslagTjeneste(db));

    private static RegjeringenTjeneste Tjeneste(RegelIdeDbContext db) => new(db, new StrukturkantTjeneste(db), Identitet(db));

    /// <summary>Grunnloven § 12 første ledd, § 15 første ledd og § 32 første ledd, én gang i basen, og aktøren staten (fast id fra #463).</summary>
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
        if (!await db.Virksomheter.AnyAsync(v => v.Id == NorgeStatenTjeneste.StatenId))
        {
            db.Virksomheter.Add(new Virksomhet { Id = NorgeStatenTjeneste.StatenId, Navn = "staten", Aktiv = true });
            await db.SaveChangesAsync();
        }
    }

    /// <summary>En rettskilde med én node per tekst, eid av et nytt testdepartement (taggens eier).</summary>
    private static async Task<(Guid Id, List<string> Eider)> RettskildeAsync(RegelIdeDbContext db, params string[] tekster)
    {
        var dep = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Testdepartementet {Unik()}", Aktiv = true };
        db.Virksomheter.Add(dep);
        var id = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = id, Doctype = "act", Kildetype = "Lov", Status = "Gjeldende", Tittel = "Testlov " + id, AnsvarligDepartement = [dep.Navn], AknXml = "<akomaNtoso/>",
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var eider = new List<string>();
        for (var i = 0; i < tekster.Length; i++)
        {
            var eid = $"https://test/{id:N}/§{i + 1}/ledd-1";
            eider.Add(eid);
            db.RettskildeNoder.Add(new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = id, Eid = eid, KildeId = $"ledd-{i + 1}", NodeType = "ledd", Tekst = tekster[i], Sorteringsrekkefolge = i,
            });
        }
        await db.SaveChangesAsync();
        return (id, eider);
    }

    [Fact]
    public async Task Statsradet_og_en_tagg_per_forekomst_skrives_som_forslag_idempotent_og_torrkjoringen_skriver_ingenting()
    {
        await using var db = fixture.NyDbContext();
        await GrunnlagAsync(db);
        var (kilde, eider) = await RettskildeAsync(db,
            "Medlemmer av Stortinget og regjeringen som må oppholde seg utenfor hjemmet.",
            "Dokumentet publiseres på regjeringen.no.",
            "Regjeringen skal legge fram for Stortinget en melding hvert år.");

        var taggerFor = await db.TekstTagger.CountAsync(t => t.RettskildeId == kilde);
        var torr = await Tjeneste(db).KjorAsync(skriv: false, "test");
        Assert.False(torr.Skrevet);
        Assert.Equal(0, await db.TekstTagger.CountAsync(t => t.RettskildeId == kilde));
        Assert.Equal(0, taggerFor);
        var mine = torr.Forekomster.Where(f => f.RettskildeId == kilde).ToList();
        Assert.Equal(3, mine.Count);
        Assert.Equal([Forekomstlesninger.RolleStatsrad, Forekomstlesninger.IkkeAktor, Forekomstlesninger.Slutning], mine.Select(f => f.Lesning));

        var r1 = await Tjeneste(db).KjorAsync(skriv: true, "test");
        Assert.True(r1.Skrevet);
        Assert.Contains(r1.Statsradet.Utfall, new[] { "opprettet", "finnes" });
        var mine1 = r1.Forekomster.Where(f => f.RettskildeId == kilde).ToList();
        Assert.All(mine1, f => Assert.Equal("ny", f.Binding));

        // Statsrådet: organ med navneform og aktørtype som forslag, G har_organ → staten, og «regjeringen» er ikke navneform for det.
        await using var les = fixture.NyDbContext();
        var nf = await les.Begreper.SingleAsync(b => b.VirksomhetReferanseId == RegjeringenTjeneste.StatsradetId && b.Entitetsstatus == "gjeldende"
                                                    && b.Godkjenning != Strukturkanter.Avvist);
        Assert.Equal("statsrådet", nf.Term);
        // [PR #489] Hjemmelen er § 12 første ledd med hjemmelens art oppretting; navnet står ikke der, så det er fortolkning.
        Assert.Equal(RegjeringenTjeneste.Grl12Eid, nf.LovreferanseEid);
        Assert.Equal("oppretting", nf.HjemmelArt);
        Assert.Equal(Strukturkanter.Fortolkning, nf.Kunnskapsstatus);
        Assert.Equal(Strukturkanter.ManueltForeslatt, nf.Godkjenning);
        var hjemmel = await new AktorsideTjeneste(les, new StrukturkantTjeneste(les), Identitet(les)).HjemmelAsync(RegjeringenTjeneste.StatsradetId);
        Assert.Equal(Hjemmelkilder.HjemmelArt, hjemmel!.Kilde);
        // [PR #489] Rollen statsråd er et eget rollebegrep, del av statsrådet.
        var rolle = await les.Begreper.SingleAsync(b => b.Id == RegjeringenTjeneste.StatsradRolleId);
        Assert.Equal(Nodetyper.Rolle, rolle.Begrepskategori);
        Assert.Equal(RegjeringenTjeneste.Grl15Eid, rolle.LovreferanseEid);
        // [Juristmålingen j3] Statsrådet har medlemmer i rollen statsråd (G har_medlemmer med rolle som til-node, Grl. § 12 første ledd).
        var medlemmer = await les.Strukturkanter.SingleAsync(k => k.FraVirksomhetId == RegjeringenTjeneste.StatsradetId && k.Typekode == "har_medlemmer");
        Assert.Equal(RegjeringenTjeneste.StatsradRolleId, medlemmer.TilBegrepId);
        Assert.Equal(RegjeringenTjeneste.Grl12Eid, medlemmer.HjemmelEid);
        Assert.Equal(RegjeringenTjeneste.HarMedlemmerObjekt, medlemmer.Objekt);
        Assert.False(await les.Begreper.AnyAsync(b => b.VirksomhetReferanseId != null && b.Term.ToLower() == "regjeringen" && b.Entitetsstatus == "gjeldende"));
        var aktortype = await les.AktortypeUtsagn.SingleAsync(a => a.VirksomhetId == RegjeringenTjeneste.StatsradetId);
        Assert.Equal("organ", aktortype.Aktortype);
        Assert.Equal(RegjeringenTjeneste.Grl12Eid, aktortype.HjemmelEid);
        var g = await les.Strukturkanter.SingleAsync(k => k.FraVirksomhetId == RegjeringenTjeneste.StatsradetId && k.Typekode == "har_organ");
        Assert.Equal(NorgeStatenTjeneste.StatenId, g.TilVirksomhetId);
        Assert.Equal(Strukturkanter.ManueltForeslatt, g.Status);

        // Forekomstbindingene: maskinelt foreslått, rollen har statsrådet som referent, slutningen har ingen.
        var bindinger = await les.NavneformBindinger.Where(b => b.RettskildeId == kilde).ToListAsync();
        Assert.Equal(3, bindinger.Count);
        Assert.All(bindinger, b => Assert.Equal(Strukturkanter.MaskineltForeslatt, b.Status));
        Assert.All(bindinger, b => Assert.NotNull(b.TekstTaggId));
        Assert.Equal(RegjeringenTjeneste.StatsradRolleId, bindinger.Single(b => b.Lesning == Forekomstlesninger.RolleStatsrad).TilBegrepId);
        var slutning = bindinger.Single(b => b.Lesning == Forekomstlesninger.Slutning);
        Assert.Null(slutning.TilVirksomhetId);
        Assert.Null(slutning.TilBegrepId);
        Assert.Equal(Strukturkanter.Uavklart, slutning.Kunnskapsstatus);
        Assert.StartsWith("[slutning]", slutning.Begrunnelse);

        // Andre kjøring: ingenting nytt.
        var r2 = await Tjeneste(db).KjorAsync(skriv: true, "test");
        Assert.Equal("finnes", r2.Statsradet.Utfall);
        Assert.All(r2.Forekomster.Where(f => f.RettskildeId == kilde), f => Assert.Equal("finnes", f.Binding));
        Assert.Equal(3, await les.NavneformBindinger.CountAsync(b => b.RettskildeId == kilde));

        // [Juristmålingen j3] Bare har_medlemmer kan ha en rolle som til-node på G; har_organ til en rolle avvises fortsatt.
        await Assert.ThrowsAsync<ArgumentException>(() => new StrukturkantTjeneste(db).OpprettAsync(new NyStrukturkant(Strukturkanter.Organtilhorighet,
            "har_organ", Kantnode.Virksomhet(RegjeringenTjeneste.StatsradetId), Kantnode.Begrep(RegjeringenTjeneste.StatsradRolleId),
            HjemmelRettskildeId: kilde, HjemmelEid: eider[0], Status: Strukturkanter.ManueltForeslatt), "test"));

        // En slutning kan ikke godkjennes som den er.
        var generiske = new GeneriskNavneformTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => generiske.GodkjennAsync(slutning.Id, "test"));
        _ = eider;
    }

    [Fact]
    public async Task Vurdert_tagg_per_forekomst_gaar_foran_nodens_binding_og_delvis_tagget_node_er_flertydig()
    {
        await using var db = fixture.NyDbContext();
        await GrunnlagAsync(db);
        // To forekomster i samme node: «medlem av regjeringen» (rolle) og «oversendt fra regjeringen» (slutning), som i avgraderingsreglementet.
        var (kilde, eider) = await RettskildeAsync(db,
            "bygger på et dokument oversendt fra regjeringen eller en redegjørelse av et medlem av regjeringen, skal det innhentes uttalelse.",
            "Den utvidete komiteen drøfter med regjeringen viktige spørsmål.");
        await Tjeneste(db).KjorAsync(skriv: true, "test");
        var generiske = new GeneriskNavneformTjeneste(db);
        var nfId = (await generiske.FinnAsync(RegjeringenTjeneste.Term))!.Id;
        var tagger = await db.TekstTagger.Where(t => t.RettskildeId == kilde && t.RefId == nfId).OrderBy(t => t.NodeEid).ThenBy(t => t.StartOffset).ToListAsync();
        Assert.Equal(3, tagger.Count);
        var (fra, medlem, andre) = (tagger[0], tagger[1], tagger[2]);
        var bindinger = await db.NavneformBindinger.Where(b => b.RettskildeId == kilde).ToDictionaryAsync(b => b.TekstTaggId!.Value);

        // En nodebinding for hele rettskilden (vurdert) til Kongen i statsråd-lignende testaktør: brukes som før så lenge ingen forekomst er vurdert.
        var aktor = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Testorgan {Unik()}", Aktiv = true };
        db.Virksomheter.Add(aktor);
        await db.SaveChangesAsync();
        await generiske.OpprettBindingAsync(new NyNavneformBinding(nfId, kilde, Kantnode.Virksomhet(aktor.Id), HjemmelRettskildeId: kilde, HjemmelEid: eider[1],
            Kunnskapsstatus: Strukturkanter.Fortolkning, Tolkningsargumenter: ["ordlyd"], Begrunnelse: "test", Status: Strukturkanter.FagligVurdert), "test");

        var forNoden = await generiske.LosOppAsync(nfId, kilde, eider[0]);
        Assert.Equal(GeneriskOppslagstatus.Bundet, forNoden.Status);
        Assert.Equal(aktor.Id, forNoden.Referent!.VirksomhetId);
        Assert.False(forNoden.Forekomst);

        // Vilkår 1 og 2: rollen godkjennes. Taggen går foran nodens binding; den andre forekomsten og noden som helhet er flertydige.
        await generiske.GodkjennAsync(bindinger[medlem.Id].Id, "test");
        var rolle = await generiske.LosOppAsync(nfId, kilde, eider[0], taggId: medlem.Id);
        Assert.Equal(GeneriskOppslagstatus.Bundet, rolle.Status);
        Assert.Equal(RegjeringenTjeneste.StatsradRolleId, rolle.Referent!.BegrepId);
        Assert.Equal(Forekomstlesninger.RolleStatsrad, rolle.Lesning);
        Assert.True(rolle.Forekomst);
        Assert.Equal(GeneriskOppslagstatus.Flertydig, (await generiske.LosOppAsync(nfId, kilde, eider[0], taggId: fra.Id)).Status);
        Assert.Equal(GeneriskOppslagstatus.Flertydig, (await generiske.LosOppAsync(nfId, kilde, eider[0])).Status);
        // Den andre noden har ingen vurdert forekomstbinding: nodens binding gjelder som før.
        var andreNode = await generiske.LosOppAsync(nfId, kilde, eider[1], taggId: andre.Id);
        Assert.Equal(GeneriskOppslagstatus.Bundet, andreNode.Status);
        Assert.Equal(aktor.Id, andreNode.Referent!.VirksomhetId);

        // Et menneske avviser slutningen og registrerer forekomsten som fremmed stat (bare for testen): da er alle forekomstene i noden tagget,
        // og noden som helhet har to ulike svar, altså flertydig, mens hver forekomst har sitt.
        await generiske.AvvisAsync(bindinger[fra.Id].Id, "test", "test");
        await generiske.OpprettBindingAsync(new NyNavneformBinding(nfId, kilde, new Kantnode(null, null), HjemmelRettskildeId: kilde, HjemmelEid: eider[0],
            Kunnskapsstatus: Strukturkanter.Tekstfunn, Status: Strukturkanter.Godkjent, TekstTaggId: fra.Id, Lesning: Forekomstlesninger.FremmedStat), "test");
        var ikkeAktor = await generiske.LosOppAsync(nfId, kilde, eider[0], taggId: fra.Id);
        Assert.Equal(GeneriskOppslagstatus.IkkeAktor, ikkeAktor.Status);
        Assert.Null(ikkeAktor.Referent);
        Assert.Equal(GeneriskOppslagstatus.Flertydig, (await generiske.LosOppAsync(nfId, kilde, eider[0])).Status);

        // Taggvisningen bruker taggen: rollen er bundet, og forslaget for den andre noden står ved taggen uten å brukes.
        var visning = await generiske.LosOppTaggerAsync(kilde);
        Assert.Equal(GeneriskOppslagstatus.Bundet, visning.Single(v => v.TaggId == medlem.Id).Status);
        Assert.Equal(Forekomstlesninger.Slutning, visning.Single(v => v.TaggId == andre.Id).Forekomstforslag!.Lesning);
    }

    [Fact]
    public async Task Forekomstbinding_valideres_lesning_uten_referent_og_tagg_mot_annen_navneform_avvises()
    {
        await using var db = fixture.NyDbContext();
        await GrunnlagAsync(db);
        var (kilde, eider) = await RettskildeAsync(db, "Statsforvalteren er Kongens og Regjeringens representant i fylket.");
        await Tjeneste(db).KjorAsync(skriv: true, "test");
        var generiske = new GeneriskNavneformTjeneste(db);
        var nfId = (await generiske.FinnAsync(RegjeringenTjeneste.Term))!.Id;
        var tagg = await db.TekstTagger.SingleAsync(t => t.RettskildeId == kilde && t.RefId == nfId);

        // Lesning uten referent med referent, lesning uten tagg, og paragrafspenn på en forekomstbinding.
        await Assert.ThrowsAsync<ArgumentException>(() => generiske.OpprettBindingAsync(new NyNavneformBinding(nfId, kilde,
            Kantnode.Virksomhet(RegjeringenTjeneste.StatsradetId), Kunnskapsstatus: Strukturkanter.Uavklart, Begrunnelse: "x",
            TekstTaggId: tagg.Id, Lesning: Forekomstlesninger.FremmedStat), "test"));
        await Assert.ThrowsAsync<ArgumentException>(() => generiske.OpprettBindingAsync(new NyNavneformBinding(nfId, kilde,
            Kantnode.Virksomhet(RegjeringenTjeneste.StatsradetId), Kunnskapsstatus: Strukturkanter.Uavklart, Begrunnelse: "x",
            Lesning: Forekomstlesninger.RolleStatsrad), "test"));
        await Assert.ThrowsAsync<ArgumentException>(() => generiske.OpprettBindingAsync(new NyNavneformBinding(nfId, kilde,
            Kantnode.Virksomhet(RegjeringenTjeneste.StatsradetId), [new ParagrafspennPar(eider[0], null)], Kunnskapsstatus: Strukturkanter.Uavklart,
            Begrunnelse: "x", TekstTaggId: tagg.Id), "test"));
        // En slutning kan ikke registreres som vurdert.
        await Assert.ThrowsAsync<ArgumentException>(() => generiske.OpprettBindingAsync(new NyNavneformBinding(nfId, kilde, new Kantnode(null, null),
            Kunnskapsstatus: Strukturkanter.Uavklart, Begrunnelse: "x", Status: Strukturkanter.Godkjent, TekstTaggId: tagg.Id,
            Lesning: Forekomstlesninger.Slutning), "test"));
        // Samme tagg har alt et forslag (slutning): et nytt forslag for samme forekomst er en motsigelse.
        await Assert.ThrowsAsync<ArgumentException>(() => generiske.OpprettBindingAsync(new NyNavneformBinding(nfId, kilde,
            Kantnode.Virksomhet(RegjeringenTjeneste.StatsradetId), Kunnskapsstatus: Strukturkanter.Uavklart, Begrunnelse: "x", TekstTaggId: tagg.Id), "test"));
    }
}
