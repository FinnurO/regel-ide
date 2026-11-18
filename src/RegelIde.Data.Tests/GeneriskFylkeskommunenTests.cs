using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #470] Standardregelen for den generiske «fylkeskommunen» og skrivingen av navneformen. Spørsmålene modellen skal kunne besvare
/// etterpå (S1/S6): hva betyr «fylkeskommunen» i rettskilde X — klassen fylkeskommune, eller ikke avklart fordi kilden selv sier noe annet?
/// Gir regelen noen gang Oslo? Hvilke kilder navngir en bestemt fylkeskommune, og hvor er bindingen foreslått?
/// <para>Testbasen er delt med resten av samlingen. En gjeldende generisk «fylkeskommunen» eller klasse «fylkeskommune» endrer rutingen i
/// andre tester («Fylkeskommunen» blir et kjent ord), så begge arkiveres etter hver test (samme grunn som i KlasseoppsettTjenesteTests).</para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class GeneriskFylkeskommunenTests(EmbeddedPostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var db = fixture.NyDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE begreper SET entitetsstatus = 'arkivert' WHERE entitetsstatus = 'gjeldende' AND ("
            + "(begrepskategori = 'klasse' AND medlemskapsregel = 'omraderegister_fylkeskommune') "
            + "OR (navneformgrunn = 'generisk' AND term = 'fylkeskommunen'))");
    }

    private static string Kort() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>Klassen fylkeskommune som #442 skriver den: forslag, hjemmel kl. § 2-1 første ledd.</summary>
    private static async Task<BegrepEntitet> KlasseAsync(RegelIdeDbContext db)
    {
        var finnes = await db.Begreper.FirstOrDefaultAsync(b => b.Medlemskapsregel == Klasseregler.OmraderegisterFylkeskommune && b.Entitetsstatus == "gjeldende");
        if (finnes is not null) return finnes;
        var b = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Term = "fylkeskommune", Begrepskategori = Nodetyper.Klasse, LovreferanseEid = Klasseregler.KommunelovenInndelingEid,
            Medlemskapsregel = Klasseregler.OmraderegisterFylkeskommune, Kunnskapsstatus = Strukturkanter.Tekstfunn,
            Godkjenning = Strukturkanter.ManueltForeslatt, Status = "publisert", OpprettetAv = "test",
        };
        db.Begreper.Add(b);
        await db.SaveChangesAsync();
        return b;
    }

    /// <summary>Kommuneloven med leddene regelen siterer. Én gang i basen: noden må være entydig (samme som KlasseoppsettTjenesteTests).</summary>
    private static async Task KommunelovenAsync(RegelIdeDbContext db)
    {
        if (await db.RettskildeNoder.AnyAsync(n => n.Eid == Klasseregler.KommunelovenFylkeskommuneEid && n.Entitetsstatus == "gjeldende")) return;
        var r = new RettskildeEntitet
        {
            Id = Guid.NewGuid(), Doctype = "act", Kildetype = "Lov", Tittel = "Lov om kommuner og fylkeskommuner (kommuneloven)", Kortnavn = "Kommuneloven",
            Status = "Gjeldende", OpprettetAv = "test", AknXml = "<akomaNtoso/>",
        };
        db.Rettskilder.Add(r);
        var i = 0;
        RettskildeNodeEntitet Ledd(string eid, string tekst) => new()
        {
            Id = Guid.NewGuid(), RettskildeId = r.Id, Eid = eid, KildeId = $"l{++i}", NodeType = "ledd", Nummer = $"{i}", Tekst = tekst, Sorteringsrekkefolge = i,
        };
        db.RettskildeNoder.AddRange(
            Ledd(Klasseregler.KommunelovenInndelingEid, "Norge er inndelt i kommuner og fylkeskommuner med en egen folkevalgt ledelse."),
            Ledd(Klasseregler.KommunelovenFylkeskommuneEid, "Hver kommune hører til et fylke. Hvert fylke unntatt Oslo utgjør en fylkeskommune."),
            Ledd(Klasseregler.KommunelovenNavneregelEid, "Fylkeskommunen skal ha samme navn som fylket."));
        await db.SaveChangesAsync();
    }

    /// <summary>En forskrift med én paragraf per tekstliste (paragrafnode + ledd), så spennet kan være paragrafen.</summary>
    private static async Task<RettskildeEntitet> ForskriftAsync(RegelIdeDbContext db, string tittel, params string[][] paragrafer)
    {
        var n = Kort();
        var r = new RettskildeEntitet
        {
            Id = Guid.NewGuid(), Doctype = "regulation", Kildetype = "Forskrift", Tittel = $"{tittel} {n}", Eli = $"https://lovdata.no/eli/forskrift/2099/01/01/{n}/nor",
            Status = "Gjeldende", OpprettetAv = "test", AknXml = "<akomaNtoso/>",
        };
        db.Rettskilder.Add(r);
        var s = 0;
        for (var p = 0; p < paragrafer.Length; p++)
        {
            var par = new RettskildeNodeEntitet
            {
                Id = Guid.NewGuid(), RettskildeId = r.Id, Eid = $"{r.Eli}/§{p + 1}", KildeId = $"p{p}", NodeType = "paragraf", Sorteringsrekkefolge = ++s,
            };
            db.RettskildeNoder.Add(par);
            for (var l = 0; l < paragrafer[p].Length; l++)
            {
                db.RettskildeNoder.Add(new RettskildeNodeEntitet
                {
                    Id = Guid.NewGuid(), RettskildeId = r.Id, Eid = $"{r.Eli}/§{p + 1}/ledd-{l + 1}", KildeId = $"p{p}l{l}", NodeType = "ledd",
                    ParentNodeId = par.Id, Tekst = paragrafer[p][l], Sorteringsrekkefolge = ++s,
                });
            }
        }
        await db.SaveChangesAsync();
        return r;
    }

    [Fact]
    public void Navngitte_fylkeskommuner_skiller_navn_fra_ord_som_innleder_setningen()
    {
        string[] Navn(string t) => GeneriskStandardregelTjeneste.NavngitteFylkeskommuner(t).Select(f => f.Ledd).ToArray();
        Assert.Equal(new[] { "Troms" }, Navn("Vedtak om tilskudd fattes av Troms fylkeskommune."));
        Assert.Equal(new[] { "Troms og Finnmark" }, Navn("Fram til 31. desember 2023 er Troms og Finnmark fylkeskommune klageinstans."));
        Assert.Equal(new[] { "Nordland" }, Navn("Saken avgjøres av fylkeskommunen i Nordland."));
        Assert.True(GeneriskStandardregelTjeneste.NavngitteFylkeskommuner("i samarbeid med Nordland og Finnmark fylkeskommuner").Single().Flertall);
        // Målt i delt base 2026-11-17: disse tre er ikke navn.
        Assert.Empty(Navn("Den fylkeskommune hvor løyvesøker har sin forretningsadresse er løyvemyndighet."));
        Assert.Empty(Navn("Enhver fylkeskommune plikter å medvirke til undervisning."));
        Assert.Empty(Navn("Med fylkeskommune i denne forskriften menes også Longyearbyen lokalstyre."));
        Assert.Empty(Navn("Fylkeskommunen, i Oslo kommunen, er forvaltningsmyndighet. Kommune og fylkeskommune skal samarbeide."));
    }

    [Fact]
    public void Paragrafen_er_eId_en_til_og_med_paragrafleddet()
    {
        Assert.Equal("https://lovdata.no/eli/forskrift/2023/08/08/1312/nor/§9",
            FylkeskommunenOppsettTjeneste.Paragraf("https://lovdata.no/eli/forskrift/2023/08/08/1312/nor/§9/ledd-1"));
        Assert.Equal("x/nor/kap-2/§4-10", FylkeskommunenOppsettTjeneste.Paragraf("x/nor/kap-2/§4-10/ledd-1/punkt-a"));
        Assert.Null(FylkeskommunenOppsettTjeneste.Paragraf("https://lovdata.no/eli/lov/2023/06/16/1/nor/kap-I/ledd-4"));
    }

    [Fact]
    public async Task Regelen_gir_klassen_aldri_Oslo_og_hull_naar_kilden_definerer_eller_navngir()
    {
        await using var db = fixture.NyDbContext();
        var klasse = await KlasseAsync(db);
        var regler = new GeneriskStandardregelTjeneste(db);

        // Det vanlige: klassen, primær, avledet, med kommunelovens hjemler (eId og sitat) på regelen.
        var vanlig = await ForskriftAsync(db, "Forskrift om fylkesveg", ["Fylkeskommunen skal sørge for vedlikehold."]);
        var s = (await regler.LosAsync("fylkeskommunen", vanlig.Id))!;
        Assert.Equal((StandardregelUtfall.Lost, StandardregelRute.FastReferent, klasse.Id, Strukturkanter.Avledet),
            (s.Utfall, s.Rute, s.Referent!.BegrepId!.Value, s.Kunnskapsstatus));
        Assert.Equal(Klasseregler.KommunelovenInndelingEid, s.Premisser.Single().HjemmelEid);
        Assert.Contains(s.Merknader, m => m.Contains("er et forslag som ikke er vurdert ennå", StringComparison.Ordinal));
        var regel = GeneriskStandardregelTjeneste.For("Fylkeskommunen")!;
        Assert.Equal(GeneriskStandardregelTjeneste.KlasseFylkeskommune, regel.Regelkode);
        Assert.Equal(new[] { Klasseregler.KommunelovenInndelingEid, Klasseregler.KommunelovenFylkeskommuneEid, Klasseregler.KommunelovenNavneregelEid },
            regel.Hjemler.Select(h => h.Eid));
        Assert.Contains("unntatt Oslo", regel.Hjemler[1].Sitat);

        // «Den fylkeskommune hvor …» er ikke et navn: klassen.
        var den = await ForskriftAsync(db, "Yrkestransportforskriften", ["Den fylkeskommune hvor løyvesøker har sin forretningsadresse er løyvemyndighet. Fylkeskommunen kan kreve gebyr."]);
        Assert.Equal(StandardregelUtfall.Lost, (await regler.LosAsync("fylkeskommunen", den.Id))!.Utfall);

        // Oslo: klassen, aldri Oslo, og en merknad som peker på #464.
        var oslo = await ForskriftAsync(db, "Tunnelsikkerhetsforskrift", ["Fylkeskommunen, i Oslo kommunen, er forvaltningsmyndighet."]);
        var so = (await regler.LosAsync("fylkeskommunen", oslo.Id))!;
        Assert.Equal((StandardregelUtfall.Lost, klasse.Id), (so.Utfall, so.Referent!.BegrepId!.Value));
        Assert.Null(so.Referent.VirksomhetId);
        Assert.Contains(so.Merknader, m => m.Contains("#464", StringComparison.Ordinal) && m.Contains("unntatt Oslo", StringComparison.Ordinal));

        // (a) kilden definerer ordet, også med ord mellom («i denne forskriften»).
        var definerer = await ForskriftAsync(db, "Forskrift om regionale kulturfond",
            ["For Svalbard forvaltes ordningen av Longyearbyen lokalstyre. Med fylkeskommune i denne forskriften menes også Longyearbyen lokalstyre.",
             "Fylkeskommunen fordeler midlene."]);
        Assert.StartsWith("Kilden definerer", (await regler.LosAsync("fylkeskommunen", definerer.Id))!.Forklaring);

        // (b) kilden navngir en bestemt fylkeskommune: hull for hele kilden, med navnet.
        var navngir = await ForskriftAsync(db, "Opplæringsforskrifta", ["Fylkeskommunen skal gi tilbod."], ["Inntaksnemnda i Finnmark fylkeskommune skal ta inn søkjarar."]);
        var sn = (await regler.LosAsync("fylkeskommunen", navngir.Id))!;
        Assert.Equal(StandardregelUtfall.IkkeLost, sn.Utfall);
        Assert.StartsWith("Kilden navngir en bestemt fylkeskommune (Finnmark fylkeskommune)", sn.Forklaring);

        // (d) Svalbard.
        var svalbard = await ForskriftAsync(db, "Forskrift om fylkesveg på Svalbard", ["Fylkeskommunen skal"]);
        Assert.Equal(StandardregelUtfall.IkkeLost, (await regler.LosAsync("fylkeskommunen", svalbard.Id))!.Utfall);

        // Dekningen teller grunnen for seg.
        var dekning = await new GeneriskStandardregelTjeneste(db).DekningAsync("fylkeskommunen", 0);
        Assert.True(dekning.IkkeLostGrunner.GetValueOrDefault("kilden_navngir_fylkeskommune") >= 1);
    }

    [Fact]
    public async Task Skrivingen_er_idempotent_gir_forslag_og_binder_bare_paragrafen_som_navngir_en()
    {
        await using var db = fixture.NyDbContext();
        await KommunelovenAsync(db);
        var klasse = await KlasseAsync(db);
        // Bare bokstaver: navneregelen krever et ord av bokstaver foran «fylkeskommune» (sifre i en Guid ville ikke vært et navn).
        var fylke = "Testfylke" + new string(Kort().Select(c => (char)('a' + Convert.ToInt32(c.ToString(), 16))).ToArray());
        var fk = new Virksomhet { Id = Guid.NewGuid(), Navn = $"{fylke.ToUpperInvariant()} FYLKESKOMMUNE", Forvaltningsniva = "fylkeskommune" };
        db.Virksomheter.Add(fk);
        await db.SaveChangesAsync();
        // § 1 bruker ordet, § 2 navngir fylkeskommunen og bruker ordet (forslag), § 3 navngir en ukjent (ingen aktør, ikke forslag).
        var kilde = await ForskriftAsync(db, "Forskrift om tilskudd i nord",
            ["Fylkeskommunen kunngjør utlysningen."],
            [$"Vedtak om tilskudd fattes av {fylke} fylkeskommune.", "Fylkeskommunen kan dispensere fra fristene."],
            ["Ukjentland fylkeskommune er klageinstans. Fylkeskommunen orienteres."]);

        async Task<(int Begreper, int Bindinger, int Proveniens)> TellAsync()
        {
            await using var t = fixture.NyDbContext();
            return (await t.Begreper.IgnoreQueryFilters().CountAsync(), await t.NavneformBindinger.IgnoreQueryFilters().CountAsync(), await t.Proveniens.CountAsync());
        }

        // Tørrkjøring: samme rapport som skrivingen, ingenting lagret.
        var for1 = await TellAsync();
        var torr = await new FylkeskommunenOppsettTjeneste(db).KjorAsync(skriv: false, "test");
        Assert.False(torr.Skrevet);
        Assert.Equal(for1, await TellAsync());
        Assert.Equal(klasse.Id, torr.KlasseId);
        Assert.Contains(torr.Navneform.Utfall, new[] { "opprettet", "finnes" });
        var tf = Assert.Single(torr.Bindingsforslag, b => b.RettskildeId == kilde.Id);
        Assert.Equal(($"{kilde.Eli}/§2", $"{kilde.Eli}/§2/ledd-1", fk.Id, "ny"), (tf.Paragraf, tf.HjemmelEid, tf.VirksomhetId, tf.Utfall));
        var manuelt = Assert.Single(torr.BindManuelt, m => m.RettskildeId == kilde.Id);
        Assert.Equal((3, 1), (manuelt.Noder, manuelt.Bindingsforslag));
        Assert.Contains(torr.Uparet, u => u.Contains("Ukjentland fylkeskommune", StringComparison.Ordinal));

        // Første skriving: navneformen som forslag, bindingen som maskinelt forslag med fortolkning og paragrafen som spenn.
        await using var db1 = fixture.NyDbContext();
        var forste = await new FylkeskommunenOppsettTjeneste(db1).KjorAsync(skriv: true, "Kari Jurist");
        Assert.True(forste.Skrevet);
        await using var les = fixture.NyDbContext();
        var nf = await les.Begreper.SingleAsync(b => b.Id == forste.Navneform.NavneformId);
        Assert.Equal((VirksomhetsbegrepTjeneste.Generisk, "fylkeskommunen", (Guid?)null), (nf.Navneformgrunn, nf.Term, nf.VirksomhetReferanseId));
        if (forste.Navneform.Utfall == "opprettet") Assert.Equal(Strukturkanter.ManueltForeslatt, nf.Godkjenning);
        var bid = Assert.Single(forste.Bindingsforslag, b => b.RettskildeId == kilde.Id).BindingId!.Value;
        var binding = await les.NavneformBindinger.SingleAsync(b => b.Id == bid);
        Assert.Equal((Strukturkanter.MaskineltForeslatt, Strukturkanter.Fortolkning, FylkeskommunenOppsettTjeneste.BindingOppdagelseskilde, fk.Id),
            (binding.Status, binding.Kunnskapsstatus, binding.OppdagelsesKilde, binding.TilVirksomhetId!.Value));
        Assert.Contains($"{kilde.Eli}/§2", binding.ParagrafspennJson);

        // Forslaget brukes ikke før det er vurdert: oppslaget i § 2 gir fortsatt regelens svar (hull, kilden navngir en fylkeskommune).
        var o = await new GeneriskNavneformTjeneste(les).LosOppAsync(nf.Id, kilde.Id, $"{kilde.Eli}/§2/ledd-2");
        Assert.Equal((GeneriskOppslagstatus.Ubundet, 1), (o.Status, o.AntallForeslatteBindinger));

        // Andre skriving: ingenting nytt.
        var etterForste = await TellAsync();
        await using var db2 = fixture.NyDbContext();
        var andre = await new FylkeskommunenOppsettTjeneste(db2).KjorAsync(skriv: true, "test");
        Assert.Equal(etterForste, await TellAsync());
        Assert.Equal("finnes", andre.Navneform.Utfall);
        Assert.Equal("finnes", Assert.Single(andre.Bindingsforslag, b => b.RettskildeId == kilde.Id).Utfall);

        // Et avvist bindingsforslag foreslås ikke på nytt.
        await new GeneriskNavneformTjeneste(db2).AvvisAsync(bid, "Kari Jurist", "test");
        await using var db3 = fixture.NyDbContext();
        var tredje = await new FylkeskommunenOppsettTjeneste(db3).KjorAsync(skriv: true, "test");
        Assert.Equal("avvist", Assert.Single(tredje.Bindingsforslag, b => b.RettskildeId == kilde.Id).Utfall);
        Assert.Equal(etterForste.Bindinger, (await TellAsync()).Bindinger);
    }

    [Fact]
    public async Task Uten_klassen_skrives_ingenting()
    {
        await using var db = fixture.NyDbContext();
        await KommunelovenAsync(db);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE begreper SET entitetsstatus = 'arkivert' WHERE entitetsstatus = 'gjeldende' AND medlemskapsregel = 'omraderegister_fylkeskommune'");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new FylkeskommunenOppsettTjeneste(db).KjorAsync(skriv: true, "test"));
        Assert.Contains("Klassen fylkeskommune finnes ikke", ex.Message);
        Assert.False(await db.Begreper.AnyAsync(b => b.Navneformgrunn == VirksomhetsbegrepTjeneste.Generisk && b.Term == "fylkeskommunen"
                                                     && b.Entitetsstatus == "gjeldende"));
    }
}
