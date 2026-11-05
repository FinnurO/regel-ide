using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #440 PR 2] Skrivingen i aktørkøen mot ekte embedded Postgres (<see cref="AktorkoSkrivTjeneste"/>). Akseptansekriteriene som
/// spørsmål:
/// <list type="bullet">
/// <item><b>AK5</b> «Oppretter én godkjenning alt i én transaksjon, uten at noe er forhåndsvalgt, og sier kvitteringen hva som skjedde?» Aktør
/// eller klasse, navneform med hjemmel eller «hjemmel mangler», hjemmel_art og tagger for alle forekomstene; ved feil rulles alt tilbake.</item>
/// <item>«Ikke en aktør» krever begrunnelse; koble varsler om utgått navn og kortform; skill ut / ta med lagres i aktorko_gruppe.</item>
/// <item>«Tagg alle som generisk omtale»: tørrkjøringen skriver ingenting, men gir de samme tallene som skrivingen.</item>
/// </list>
/// Navnene er unike per test (bare bokstaver), fordi testsamlingen deler database.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class AktorkoSkrivTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public AktorkoSkrivTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class KasterHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Ingen eksterne oppslag her.");
    }

    private static string Unik(string s) => s + new string(Guid.NewGuid().ToString("N")[..10].Select(c => (char)('a' + Convert.ToInt32(c.ToString(), 16))).ToArray());

    private static AktorkoSkrivTjeneste Tjeneste(RegelIdeDbContext db)
    {
        var vo = new VirksomhetOppslagTjeneste(db);
        var tt = new TekstTaggTjeneste(db, vo);
        var vb = new VirksomhetsbegrepTjeneste(db);
        var kandidater = new NavnekandidatOppdagelseTjeneste(db, vb, tt, vo, new EksternNavneoppslagTjeneste(new HttpClient(new KasterHandler()), db),
            new StrukturkantTjeneste(db));
        return new AktorkoSkrivTjeneste(db, new AktorkoTjeneste(db), kandidater, vb, new AktoridentitetTjeneste(db, tt, vo));
    }

    /// <summary>En rettskilde eid av et nytt departement (så taggene får en eier), med én node per tekst.</summary>
    private static async Task<(Guid Id, List<string> Eider)> RettskildeAsync(RegelIdeDbContext db, params string[] tekster)
    {
        var departement = new Virksomhet { Id = Guid.NewGuid(), Navn = Unik("Testdepartementet ") };
        db.Virksomheter.Add(departement);
        var id = Guid.NewGuid();
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = id, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse", Tittel = "Testlov " + id,
            AnsvarligDepartement = [departement.Navn], OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
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

    private static NavnekandidatEntitet Kandidat(RegelIdeDbContext db, Guid rettskildeId, string eid, string tekst, string navn)
    {
        var start = tekst.IndexOf(navn, StringComparison.Ordinal);
        var k = new NavnekandidatEntitet
        {
            Id = Guid.NewGuid(), ForeslattTekst = navn, Kategori = "gruppe", RettskildeId = rettskildeId, NodeEid = eid, StartOffset = start,
            EndOffset = start + navn.Length, Status = "Venter", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Navnekandidater.Add(k);
        return k;
    }

    private static string Stor(string s) => char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>«skal hver barnehage ha et ‹råd›» i § 1, og navnet i bestemt form i § 2 og § 3 (to forekomster, samme ord).</summary>
    private static async Task<(Guid Lov, List<string> Eider, string Rad, string Bestemt, List<NavnekandidatEntitet> Kandidater)> RaadAsync(RegelIdeDbContext db)
    {
        var rad = Unik("testraad");
        var bestemt = rad + "et";
        var tekster = new[]
        {
            $"For å sikre samarbeidet skal hver barnehage ha et {rad} og et samarbeidsutvalg.",
            $"{Stor(bestemt)} skal få uttale seg.",
            $"Barnehagen skal legge saken fram for {bestemt}.",
        };
        var (lov, eider) = await RettskildeAsync(db, tekster);
        var k = new List<NavnekandidatEntitet>
        {
            Kandidat(db, lov, eider[1], tekster[1], Stor(bestemt)),
            Kandidat(db, lov, eider[2], tekster[2], bestemt),
        };
        await db.SaveChangesAsync();
        return (lov, eider, rad, bestemt, k);
    }

    // ------------------------------------------------------------------ AK5: godkjenning i én transaksjon

    [Fact]
    public async Task AK5_godkjenning_som_aktor_med_hjemmelforslag_oppretter_aktor_navneform_hjemmel_art_og_tagger()
    {
        await using var db = _fixture.NyDbContext();
        var (lov, eider, rad, bestemt, kandidater) = await RaadAsync(db);

        var kvittering = await Tjeneste(db).GodkjennAsync(bestemt, new AktorkoGodkjenning(
            Aktorkotyper.Aktor, bestemt, Aktorkohjemmel.Forslag, lov, eider[0], Strukturkanter.FagligVurdert, ForventetForekomster: 2), "test");

        Assert.NotNull(kvittering);
        Assert.Equal(("godkjent", 2, 2, 0), (kvittering.Handling, kvittering.Forekomster, kvittering.Tagget, kvittering.IkkeTagget));
        var v = await db.Virksomheter.SingleAsync(x => x.Id == kvittering.VirksomhetId);
        Assert.Equal(bestemt, v.Navn);
        var navneform = await db.Begreper.SingleAsync(b => b.Id == kvittering.NavneformId);
        Assert.Equal((bestemt, "gjeldende", eider[0], Strukturkanter.Normalisering, Strukturkanter.FagligVurdert, "oppretting"),
            (navneform.Term, navneform.Navneformgrunn, navneform.LovreferanseEid, navneform.Kunnskapsstatus, navneform.Godkjenning, navneform.HjemmelArt));
        Assert.Contains($"«{rad}»", navneform.Begrunnelse);

        var ider = kandidater.Select(k => k.Id).ToList();
        var etter = await db.Navnekandidater.AsNoTracking().Where(k => ider.Contains(k.Id)).ToListAsync();
        Assert.All(etter, k => Assert.Equal(("Godkjent", "virksomhet"), (k.Status, k.Kategori)));
        var tagger = await db.TekstTagger.Where(t => t.RettskildeId == lov && t.RefId == navneform.Id).ToListAsync();
        Assert.Equal(2, tagger.Count);
        Assert.All(tagger, t => Assert.Equal("virksomhet", t.Kind));
        Assert.Contains(kvittering.Linjer, l => l.Contains("oppretting"));
        Assert.Null(await new AktorkoTjeneste(db).DetaljAsync(bestemt)); // ute av køen
    }

    [Fact]
    public async Task AK5_hjemmel_mangler_gir_uavklart_med_hva_som_er_undersokt_og_ingen_hjemmel_art()
    {
        await using var db = _fixture.NyDbContext();
        var navn = Unik("Testdirektoratet");
        var tekst = $"{navn} skal veilede kommunene.";
        var (lov, eider) = await RettskildeAsync(db, tekst);
        Kandidat(db, lov, eider[0], tekst, navn);
        await db.SaveChangesAsync();

        var kvittering = await Tjeneste(db).GodkjennAsync(navn, new AktorkoGodkjenning(
            Aktorkotyper.Aktor, navn, Aktorkohjemmel.Mangler, null, null, Strukturkanter.Godkjent), "test");

        var navneform = await db.Begreper.SingleAsync(b => b.Id == kvittering!.NavneformId);
        Assert.Equal((Strukturkanter.Uavklart, (string?)null, (string?)null), (navneform.Kunnskapsstatus, navneform.LovreferanseEid, navneform.HjemmelArt));
        Assert.Contains(AktorkoTjeneste.Hullmerknad, navneform.Begrunnelse);
        Assert.Contains(kvittering!.Linjer, l => l.Contains("hjemmel mangler"));
    }

    [Fact]
    public async Task AK5_godkjenning_som_klasse_gir_lovspesifikt_begrep_hjemlet_i_forslaget_og_tagger_mot_begrepet()
    {
        await using var db = _fixture.NyDbContext();
        var (lov, eider, _, bestemt, _) = await RaadAsync(db);

        var kvittering = await Tjeneste(db).GodkjennAsync(bestemt, new AktorkoGodkjenning(
            Nodetyper.Klasse, bestemt, Aktorkohjemmel.Forslag, lov, eider[0], Strukturkanter.Godkjent), "test");

        var begrep = await db.Begreper.SingleAsync(b => b.Id == kvittering!.BegrepId);
        Assert.Equal((Nodetyper.Klasse, lov, eider[0], "oppretting", Strukturkanter.Godkjent),
            (begrep.Begrepskategori, begrep.LovkildeId, begrep.LovreferanseEid, begrep.HjemmelArt, begrep.Godkjenning));
        Assert.Equal(2, await db.TekstTagger.CountAsync(t => t.RettskildeId == lov && t.RefId == begrep.Id && t.Kind == "begrep"));
        Assert.Null(kvittering!.VirksomhetId);
        Assert.StartsWith("Klassen «", kvittering.Linjer[0]);
    }

    [Theory]
    [InlineData(null, "forslag", Strukturkanter.Godkjent, "Velg hva navnet er")]
    [InlineData("aktor", null, Strukturkanter.Godkjent, "Velg hjemmel")]
    [InlineData("aktor", "forslag", null, "Velg hvor sikkert")]
    [InlineData("aktor", "forslag", "maskinelt_foreslatt", "Velg hvor sikkert")]
    [InlineData("klasse", "mangler", Strukturkanter.Godkjent, "Velg hvor klassen gjelder")]
    public async Task AK5_ingenting_er_forhandsvalgt_og_et_manglende_valg_skriver_ingenting(string? type, string? hjemmel, string? godkjenning, string feil)
    {
        await using var db = _fixture.NyDbContext();
        var (lov, eider, _, bestemt, kandidater) = await RaadAsync(db);
        var virksomheter = await db.Virksomheter.CountAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Tjeneste(db).GodkjennAsync(bestemt,
            new AktorkoGodkjenning(type, bestemt, hjemmel, hjemmel == "forslag" ? lov : null, hjemmel == "forslag" ? eider[0] : null, godkjenning), "test"));

        Assert.Contains(feil, ex.Message);
        Assert.Equal(virksomheter, await db.Virksomheter.CountAsync());
        Assert.All(await db.Navnekandidater.AsNoTracking().Where(k => k.Id == kandidater[0].Id).ToListAsync(), k => Assert.Equal("Venter", k.Status));
    }

    [Fact]
    public async Task Navnet_maa_vaere_en_skrivemaate_fra_teksten_og_hjemmelen_et_av_forslagene()
    {
        await using var db = _fixture.NyDbContext();
        var (lov, eider, _, bestemt, _) = await RaadAsync(db);
        var t = Tjeneste(db);

        await Assert.ThrowsAsync<ArgumentException>(() => t.GodkjennAsync(bestemt, new AktorkoGodkjenning(
            Aktorkotyper.Aktor, Stor(bestemt) + " i kommunen", Aktorkohjemmel.Mangler, null, null, Strukturkanter.Godkjent), "test"));
        await Assert.ThrowsAsync<ArgumentException>(() => t.GodkjennAsync(bestemt, new AktorkoGodkjenning(
            Aktorkotyper.Aktor, bestemt, Aktorkohjemmel.Forslag, lov, eider[1], Strukturkanter.Godkjent), "test"));
    }

    [Fact]
    public async Task AK5_feil_i_et_steg_ruller_tilbake_alt_aktoren_navneformen_og_taggene()
    {
        await using var db = _fixture.NyDbContext();
        var (lov, eider, _, bestemt, kandidater) = await RaadAsync(db);
        var funksjon = Unik("aktorko_feil_");
        // Siste steg (kandidatene settes til Godkjent) feiler i databasen. Da er aktøren, navneformen og taggene alt skrevet i transaksjonen.
        await db.Database.ExecuteSqlRawAsync(
            $"CREATE FUNCTION {funksjon}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.status = 'Godkjent' AND lower(NEW.foreslatt_tekst) = '{bestemt}' "
            + $"THEN RAISE EXCEPTION 'testfeil'; END IF; RETURN NEW; END $$; "
            + $"CREATE TRIGGER {funksjon} BEFORE UPDATE ON navnekandidater FOR EACH ROW EXECUTE FUNCTION {funksjon}();");
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => Tjeneste(db).GodkjennAsync(bestemt, new AktorkoGodkjenning(
                Aktorkotyper.Aktor, bestemt, Aktorkohjemmel.Forslag, lov, eider[0], Strukturkanter.Godkjent), "test"));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync($"DROP TRIGGER {funksjon} ON navnekandidater; DROP FUNCTION {funksjon}();");
        }

        await using var ny = _fixture.NyDbContext();
        Assert.False(await ny.Virksomheter.AnyAsync(v => v.Navn == bestemt));
        Assert.False(await ny.Begreper.AnyAsync(b => b.Term == bestemt));
        Assert.Equal(0, await ny.TekstTagger.CountAsync(t => t.RettskildeId == lov));
        var ider = kandidater.Select(k => k.Id).ToList();
        Assert.All(await ny.Navnekandidater.Where(k => ider.Contains(k.Id)).ToListAsync(), k => Assert.Equal(("Venter", "gruppe"), (k.Status, k.Kategori)));
        Assert.NotNull(await new AktorkoTjeneste(ny).DetaljAsync(bestemt)); // står i køen som før
    }

    [Fact]
    public async Task Endret_navn_siden_lesing_gir_konflikt_og_skriver_ingenting()
    {
        await using var db = _fixture.NyDbContext();
        var (_, _, _, bestemt, kandidater) = await RaadAsync(db);
        await Assert.ThrowsAsync<AktorkoKonfliktException>(() => Tjeneste(db).GodkjennAsync(bestemt, new AktorkoGodkjenning(
            Aktorkotyper.Aktor, bestemt, Aktorkohjemmel.Mangler, null, null, Strukturkanter.Godkjent, ForventetForekomster: 3), "test"));
        Assert.Equal("Venter", (await db.Navnekandidater.AsNoTracking().SingleAsync(k => k.Id == kandidater[0].Id)).Status);
    }

    // ------------------------------------------------------------------ Andre handlinger

    [Fact]
    public async Task Ikke_en_aktor_krever_begrunnelse_og_avviser_alle_forekomstene_uten_a_slette()
    {
        await using var db = _fixture.NyDbContext();
        var (_, _, _, bestemt, kandidater) = await RaadAsync(db);
        var t = Tjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => t.IkkeAktorAsync(bestemt, "  ", null, "test"));

        var kvittering = await t.IkkeAktorAsync(bestemt, "Et utvalg i hver barnehage, ikke en aktør.", 2, "test");

        Assert.Equal(2, kvittering!.Forekomster);
        var ider = kandidater.Select(k => k.Id).ToList();
        var rader = await db.Navnekandidater.AsNoTracking().Where(k => ider.Contains(k.Id)).ToListAsync();
        Assert.All(rader, k => Assert.Equal(("Avvist", "Et utvalg i hver barnehage, ikke en aktør."), (k.Status, k.AvvisningBegrunnelse)));
    }

    [Fact]
    public async Task Koble_til_aktor_som_finnes_gir_ny_navneform_med_grunn_og_varsler_om_utgatt_navn()
    {
        await using var db = _fixture.NyDbContext();
        var (lov, _, _, bestemt, _) = await RaadAsync(db);
        var aktor = new Virksomhet { Id = Guid.NewGuid(), Navn = Unik("Foreldreutvalget for barnehager ") };
        db.Virksomheter.Add(aktor);
        await db.SaveChangesAsync();
        var t = Tjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() => t.KobleAsync(bestemt, new AktorkoKobling(aktor.Id, bestemt, null), "test"));

        var kvittering = await t.KobleAsync(bestemt, new AktorkoKobling(aktor.Id, bestemt, "utgatt", 2), "test");

        var navneform = await db.Begreper.SingleAsync(b => b.Id == kvittering!.NavneformId);
        Assert.Equal((aktor.Id, "utgatt", bestemt), (navneform.VirksomhetReferanseId!.Value, navneform.Navneformgrunn, navneform.Term));
        Assert.Contains(kvittering!.Varsler, v => v.Contains("UTGÅTT"));
        Assert.Equal(2, await db.TekstTagger.CountAsync(x => x.RettskildeId == lov && x.RefId == navneform.Id));
        Assert.Null(AktorkoSkrivTjeneste.Grunnvarsel("gjeldende", "x", "y"));
        Assert.Contains("KORTFORM", AktorkoSkrivTjeneste.Grunnvarsel("kortform", "x", "y"));
    }

    [Fact]
    public async Task Skill_ut_ta_med_og_tilbakestill_lagres_i_aktorko_gruppe()
    {
        await using var db = _fixture.NyDbContext();
        var stamme = Unik("styrexx");
        var tekster = new[] { $"{stamme}et skal møtes.", $"Et {stamme} kan nedsettes.", $"Barnehagens {stamme}et skal uttale seg." };
        var (lov, eider) = await RettskildeAsync(db, tekster);
        var bestemt = Kandidat(db, lov, eider[0], tekster[0], stamme + "et");
        var ubestemt = Kandidat(db, lov, eider[1], tekster[1], stamme);
        var genitiv = Kandidat(db, lov, eider[2], tekster[2], $"Barnehagens {stamme}et");
        await db.SaveChangesAsync();
        var t = Tjeneste(db);
        var ko = new AktorkoTjeneste(db);
        Assert.Equal(2, (await ko.DetaljAsync(stamme))!.Varianter.Count); // bøyning: slått sammen

        await t.SkillUtAsync(stamme + "et", stamme, "test");
        db.ChangeTracker.Clear();
        Assert.Equal(stamme, (await db.Navnekandidater.SingleAsync(k => k.Id == ubestemt.Id)).AktorkoGruppe);
        var utskilt = (await ko.DetaljAsync(stamme))!;
        Assert.True(utskilt.Manuell);
        Assert.Single(utskilt.Varianter);
        Assert.Single((await ko.DetaljAsync(stamme + "et"))!.Varianter);

        await t.TaMedAsync(stamme + "et", $"barnehagens {stamme}et", "test");
        db.ChangeTracker.Clear();
        var medGenitiv = (await ko.DetaljAsync(stamme + "et"))!;
        Assert.Equal(["samme_ord", "manuell"], medGenitiv.Varianter.Select(v => v.Regel));
        Assert.Equal(stamme + "et", (await db.Navnekandidater.SingleAsync(k => k.Id == genitiv.Id)).AktorkoGruppe);
        Assert.Equal(stamme + "et", (await db.Navnekandidater.SingleAsync(k => k.Id == bestemt.Id)).AktorkoGruppe);

        await t.TilbakestillAsync(stamme + "et", "test");
        await t.TilbakestillAsync(stamme, "test");
        db.ChangeTracker.Clear();
        Assert.All(await db.Navnekandidater.Where(k => k.RettskildeId == lov).ToListAsync(), k => Assert.Null(k.AktorkoGruppe));
        Assert.Equal(2, (await ko.DetaljAsync(stamme))!.Varianter.Count);
    }

    // ------------------------------------------------------------------ Tagg alle som generisk omtale

    [Fact]
    public async Task Tagg_alle_som_generisk_omtale_torrkjorer_forst_og_skriver_samme_tall()
    {
        await using var db = _fixture.NyDbContext();
        var ord = Unik("utvalgetx");
        var tekster = new[] { $"{Stor(ord)} skal møtes.", $"Saken legges fram for {ord}.", $"Saken legges fram for {ord}et." };
        var (lov, eider) = await RettskildeAsync(db, tekster);
        var a = Kandidat(db, lov, eider[0], tekster[0], Stor(ord));
        var b = Kandidat(db, lov, eider[1], tekster[1], ord);
        var c = Kandidat(db, lov, eider[2], tekster[2], ord + "et"); // bøyning: ikke med (bare nøyaktig ordet)
        await db.SaveChangesAsync();
        var t = Tjeneste(db);

        var torr = await t.TaggSomGeneriskAsync(ord, false, "test");

        Assert.Equal((false, 2, 2, 0, false), (torr.Skrevet, torr.Forekomster, torr.Tagget, torr.IkkeTagget, torr.NavneformFinnes));
        Assert.False(await db.Begreper.AnyAsync(x => x.Term == ord));
        Assert.Equal(0, await db.TekstTagger.CountAsync(x => x.RettskildeId == lov));
        Assert.All(await db.Navnekandidater.AsNoTracking().Where(k => k.RettskildeId == lov).ToListAsync(), k => Assert.Equal("Venter", k.Status));

        var skrevet = await t.TaggSomGeneriskAsync(ord, true, "test");

        Assert.Equal((true, 2, 2), (skrevet.Skrevet, skrevet.Forekomster, skrevet.Tagget));
        var navneform = await db.Begreper.SingleAsync(x => x.Term == ord && x.Navneformgrunn == VirksomhetsbegrepTjeneste.Generisk);
        Assert.Equal(2, await db.TekstTagger.CountAsync(x => x.RettskildeId == lov && x.RefId == navneform.Id));
        var status = await db.Navnekandidater.AsNoTracking().Where(k => k.RettskildeId == lov).ToDictionaryAsync(k => k.Id, k => k.Status);
        Assert.Equal(("Godkjent", "Godkjent", "Venter"), (status[a.Id], status[b.Id], status[c.Id]));
    }

    [Fact]
    public async Task Tagg_alle_som_generisk_omtale_sperres_naar_ordet_er_navneform_for_en_aktor()
    {
        await using var db = _fixture.NyDbContext();
        var ord = Unik("kontoretx");
        var aktor = new Virksomhet { Id = Guid.NewGuid(), Navn = Unik("Kontoraktor ") };
        db.Virksomheter.Add(aktor);
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = aktor.Id, Term = ord, Status = "publisert", OpprettetAv = "test",
            OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var r = await Tjeneste(db).TaggSomGeneriskAsync(ord, true, "test");

        Assert.False(r.Skrevet);
        Assert.Contains(aktor.Navn, r.Sperre);
    }

    // ------------------------------------------------------------------ hjemmel_art (#441)

    [Theory]
    [InlineData("annet", "https://test/§1/ledd-1")]
    [InlineData("oppretting", null)]
    public async Task Hjemmel_art_har_lukket_liste_og_krever_hjemmel(string art, string? eid)
    {
        await using var db = _fixture.NyDbContext();
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "virksomhet", Term = Unik("Hjemmelart"), Status = "publisert", OpprettetAv = "test",
            OpprettetTidspunkt = DateTimeOffset.UtcNow, HjemmelArt = art, LovreferanseEid = eid,
        });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("ck_begreper_hjemmel_art", ex.InnerException!.Message);
    }
}
