using Microsoft.EntityFrameworkCore;
using RegelIde.Data.Strukturkonvertering;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #419, Johanns avgjørelser 2026-10-29/30] Merknader og veiledning i forskriftsteksten. Akseptansekriteriene som spørsmål:
/// <list type="number">
/// <item><b>AK1</b> Hvilke noder i en forskrift er merknader eller veiledning, og ikke normtekst? Merkes de ved import etter regel 1 (kapittel
/// øverst med eId <c>kap-merknad|veiledning|kommentar…</c>, hele undertreet), unntatt fusjonsforskriften? Får eksisterende noder samme
/// merking av tilbakefyllingen, idempotent? Overlever merkingen en ny import (ny versjon)?</item>
/// <item><b>AK2</b> Kan en slik node bli hjemmel for en kant? Nei: konverteringen leser dem ikke, lasteren hopper over dem og teller dem,
/// skriveveien avviser dem (også manuelt), og et forslag med slik hjemmel kan ikke godkjennes.</item>
/// <item><b>AK3</b> Hvilke eksisterende forslag har slik hjemmel? Tørrkjøringen teller dem uten å skrive; med skriv avvises de med
/// begrunnelsen «hjemmel er merknad/veiledning (#419)» av maskinen; godkjente og faglig vurderte røres ikke; en ny kjøring finner 0.</item>
/// </list>
/// Fixturene er ekte Lovdata-HTML (Testdata/Merknadsnoder, se csproj-kommentaren).
/// </summary>
[Collection(DataTestCollection.Navn)]
public class MerknadsnoderTests
{
    private const string ForeldrebetalingEli = "https://lovdata.no/eli/forskrift/2005/12/16/1478/nor";
    private const string FusjonEli = "https://lovdata.no/eli/forskrift/2008/01/09/50/nor";
    private const string Merknadskapittel = "kap-merknader-til-de-enkelte-bestemmelsene";

    private readonly EmbeddedPostgresFixture _fixture;

    public MerknadsnoderTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static string Les(string fil) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Testdata", "Merknadsnoder", fil));

    private static KonverteringResultat Foreldrebetaling(string? html = null) =>
        LovdataKonverterer.Konverter(html ?? Les("foreldrebetaling-FOR-2005-12-16-1478.html"), new DateOnly(2026, 10, 30));

    private static KonverteringResultat Fusjon() =>
        LovdataKonverterer.Konverter(Les("fusjon-arbeidstakerrepresentasjon-FOR-2008-01-09-50.html"), new DateOnly(2026, 10, 30));

    private static StrukturforslagTjeneste NyLaster(RegelIdeDbContext db) => new(
        db, new StrukturkantTjeneste(db), new VirksomhetOppslagTjeneste(db),
        new NavnekandidatOppdagelseTjeneste(
            db, new VirksomhetsbegrepTjeneste(db), new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)),
            new VirksomhetOppslagTjeneste(db), new EksternNavneoppslagTjeneste(new HttpClient(new KasterHandler()), db),
            new StrukturkantTjeneste(db)));

    private sealed class KasterHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new InvalidOperationException("Ingen eksterne oppslag i denne testen.");
    }

    /// <summary>Merknadskapitlet og hele undertreet, slik det står i rettskilde_noder (via parent_node_id).</summary>
    private static async Task<HashSet<Guid>> UndertreAsync(RegelIdeDbContext db, Guid rettskildeId, string rotEid)
    {
        var noder = await db.RettskildeNoder.Where(n => n.RettskildeId == rettskildeId).Select(n => new { n.Id, n.ParentNodeId, n.Eid }).ToListAsync();
        var rot = noder.Single(n => n.Eid == rotEid).Id;
        var sett = new HashSet<Guid> { rot };
        bool la;
        do
        {
            la = false;
            foreach (var n in noder.Where(n => n.ParentNodeId is { } p && sett.Contains(p))) la |= sett.Add(n.Id);
        } while (la);
        return sett;
    }

    // ------------------------------------------------------------------ AK1: regelen

    [Fact]
    public void Regelen_er_regel_1_og_ingenting_annet()
    {
        (string, string?, string)[] noder =
        [
            ("kap-merknader", null, "kapittel"),                       // rot
            ("kap-merknader/rom-til-1", "kap-merknader", "underinndeling"),
            ("kap-merknader/rom-til-1/ledd-1", "kap-merknader/rom-til-1", "ledd"),
            ("kap-veiledning-til-forskriften", null, "kapittel"),       // rot
            ("kap-kommentarer", "https://lovdata.no/eli/x/nor", "kapittel"), // forelder er ikke en node → toppnivå (som parent_node_id NULL)
            ("kap-2", null, "kapittel"),
            ("kap-2/rom-merknader", "kap-2", "underinndeling"),         // regel 2 er forkastet: underinndeling lenger ned merkes ikke
            ("kap-2/rom-merknader/ledd-1", "kap-2/rom-merknader", "ledd"),
            ("kap-veiledende-materiale", null, "kapittel"),             // ikke i regelen (målt bom, behandles som normtekst)
            ("kap-Merknader", null, "kapittel"),                        // store/små bokstaver skilles, slik regelen ble målt
            ("kap-merknader-i-del", "kap-del-1", "kapittel"),           // kapittel med forelder: ikke øverst
            ("kap-del-1", null, "kapittel"),
            ("https://lovdata.no/eli/x/nor/§1", null, "paragraf"),
        ];
        var merket = Merknadsnoder.Merk("https://lovdata.no/eli/x/nor", noder);
        Assert.Equal(
            new[] { "kap-kommentarer", "kap-merknader", "kap-merknader/rom-til-1", "kap-merknader/rom-til-1/ledd-1", "kap-veiledning-til-forskriften" },
            merket.OrderBy(e => e, StringComparer.Ordinal));

        // Unntaket gjelder bare den ene rettskilden.
        Assert.Empty(Merknadsnoder.Merk(FusjonEli, [("kap-merknader", null, "kapittel"), ("kap-merknader/ledd-1", "kap-merknader", "ledd")]));
        Assert.Equal(2, Merknadsnoder.Merk(ForeldrebetalingEli, [("kap-merknader", null, "kapittel"), ("kap-merknader/ledd-1", "kap-merknader", "ledd")]).Count);
    }

    [Fact]
    public void Migrasjonens_frosne_SQL_har_samme_regel_og_unntak_som_koden()
    {
        Assert.Equal(Merknadsnoder.Unntak.Select(u => (u.Eli, u.Eid)), MerknadsnodeMigrering.FrosneUnntak);
        Assert.Contains($"n.eid ~ '{Merknadsnoder.EidMonster}'", MerknadsnodeMigrering.TilbakefyllSql);
        foreach (var (eli, eid) in MerknadsnodeMigrering.FrosneUnntak)
        {
            Assert.Contains($"('{eli}', '{eid}')", MerknadsnodeMigrering.TilbakefyllSql);
        }
    }

    // ------------------------------------------------------------------ AK1: import, unntak, tilbakefylling, ny import

    [Fact]
    public async Task Import_merker_merknadskapitlet_og_hele_undertreet_og_ingen_normtekst()
    {
        await using var db = _fixture.NyDbContext();
        var id = await new RettskildeImportTjeneste(db).ImporterAsync(Foreldrebetaling());

        var undertre = await UndertreAsync(db, id, Merknadskapittel);
        var merket = await db.RettskildeNoder.Where(n => n.RettskildeId == id && n.ErMerknad).Select(n => n.Id).ToListAsync();
        Assert.Equal(18, undertre.Count); // målt i den delte basen 2026-10-29
        Assert.Equal(undertre.OrderBy(g => g), merket.OrderBy(g => g));
        Assert.False(await db.RettskildeNoder.AnyAsync(n => n.RettskildeId == id && n.NodeType == "paragraf" && n.ErMerknad));
        Assert.True(await db.RettskildeNoder.AnyAsync(n => n.RettskildeId == id && n.NodeType == "paragraf" && !n.ErMerknad));
    }

    [Fact]
    public async Task Unntaket_fusjonsforskriftens_vedlegg_merkes_ikke()
    {
        await using var db = _fixture.NyDbContext();
        var id = await new RettskildeImportTjeneste(db).ImporterAsync(Fusjon());
        Assert.True(await db.RettskildeNoder.AnyAsync(n => n.RettskildeId == id && n.Eid == "kap-merknader" && n.ParentNodeId == null));
        Assert.False(await db.RettskildeNoder.AnyAsync(n => n.RettskildeId == id && n.ErMerknad));
    }

    [Fact]
    public async Task Tilbakefyllingen_merker_eksisterende_noder_likt_med_importen_og_er_idempotent()
    {
        await using var db = _fixture.NyDbContext();
        var importer = new RettskildeImportTjeneste(db);
        var id = await importer.ImporterAsync(Foreldrebetaling());
        var fusjon = await importer.ImporterAsync(Fusjon());
        var fasit = await db.RettskildeNoder.Where(n => n.RettskildeId == id && n.ErMerknad).Select(n => n.Id).OrderBy(g => g).ToListAsync();
        Assert.NotEmpty(fasit);

        // Som før migrasjonen: ingen merking.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE rettskilde_noder SET er_merknad = false WHERE rettskilde_id IN ({id}, {fusjon})");

        var forste = await db.Database.ExecuteSqlRawAsync(MerknadsnodeMigrering.TilbakefyllSql);
        Assert.True(forste >= fasit.Count); // hele basen; minst de 18 her
        await using (var les = _fixture.NyDbContext())
        {
            Assert.Equal(fasit, await les.RettskildeNoder.Where(n => n.RettskildeId == id && n.ErMerknad).Select(n => n.Id).OrderBy(g => g).ToListAsync());
            Assert.False(await les.RettskildeNoder.AnyAsync(n => n.RettskildeId == fusjon && n.ErMerknad));
        }

        Assert.Equal(0, await db.Database.ExecuteSqlRawAsync(MerknadsnodeMigrering.TilbakefyllSql)); // idempotent
    }

    [Fact]
    public async Task Merkingen_overlever_en_ny_import_av_endret_tekst()
    {
        await using var db = _fixture.NyDbContext();
        var importer = new RettskildeImportTjeneste(db);
        await importer.ImporterAsync(Foreldrebetaling());
        var html = Les("foreldrebetaling-FOR-2005-12-16-1478.html");
        // Endre en tekst i normteksten, så importen gir en ny versjon (resynk-veien).
        var forsteLedd = System.Text.RegularExpressions.Regex.Match(html, "<article class=\"legalP\"[^>]*>([^<]{20})").Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(forsteLedd));
        var endret = html.Replace(forsteLedd, forsteLedd + " (endret #419)", StringComparison.Ordinal);

        var resultat = await importer.ImporterMedUtfallAsync(Foreldrebetaling(endret));
        Assert.Equal(RettskildeImportUtfall.NyVersjon, resultat.Utfall);
        Assert.Equal(18, await db.RettskildeNoder.CountAsync(n => n.RettskildeId == resultat.RettskildeId && n.ErMerknad));
        Assert.True(await db.RettskildeNoder.AnyAsync(n => n.RettskildeId == resultat.RettskildeId && n.Eid == Merknadskapittel && n.ErMerknad));

        // Tilbake til originalen (ny versjon igjen), så andre tester ser den ekte teksten.
        await importer.ImporterAsync(Foreldrebetaling());
    }

    // ------------------------------------------------------------------ AK2: aldri hjemmel

    [Fact]
    public async Task Konverteringen_leser_dem_ikke_og_lasteren_hopper_over_og_teller_dem()
    {
        await using var db = _fixture.NyDbContext();
        await Strukturkanter.SeedStartsettAsync(db);
        var id = await new RettskildeImportTjeneste(db).ImporterAsync(Foreldrebetaling());
        var laster = NyLaster(db);

        var grunnlag = await laster.ByggGrunnlagAsync(id);
        Assert.Equal(18, grunnlag.MerknadsnoderUtelatt);
        Assert.DoesNotContain(grunnlag.Noder, n => n.Eid.StartsWith("kap-merknader", StringComparison.Ordinal));
        Assert.Contains(grunnlag.Noder, n => n.NodeType == "ledd");

        // Både mønster- og KI-laget får bare grunnlaget; mønsterlagets utsagn har derfor ingen merknads-eId.
        var dok = new MonsterStrukturkonverterer().Konverter(grunnlag);
        Assert.DoesNotContain(dok.Utsagn, u => u.Eid.StartsWith("kap-merknader", StringComparison.Ordinal));

        // Et dokument laget før merkingen (lagret konvertering) med eId i merknadene: lasteren laster det ikke, og sier hvorfor.
        var merknadsledd = await db.RettskildeNoder.Where(n => n.RettskildeId == id && n.ErMerknad && n.Tekst != null)
            .OrderBy(n => n.Sorteringsrekkefolge).FirstAsync();
        var organ = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Merknadsorgan-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(organ);
        await db.SaveChangesAsync();
        var gammelt = new Strukturdokument("Foreldrebetaling", ForeldrebetalingEli, [], "test", 1,
            [new StrukturAktor("a1", organ.Navn, [], [], 1, null, null, null, null, null, null)],
            [new StrukturUtsagn("u1", merknadsledd.Eid, "sitat", "kompetanse", "vedtakskompetanse", "a1", null, "satser", "positiv",
                null, null, null, null, null) { Oppdagelseskilde = "ki:test" }]);
        var rapport = await laster.LastAsync(id, gammelt, "ki", "ki:test", "Testlaster");
        var rad = Assert.Single(rapport.Rader);
        Assert.Equal((Forslagsutfall.IkkeLastet, Forslagsarsak.HjemmelErMerknad), (rad.Utfall, rad.Arsak));
        Assert.Equal(18, rapport.MerknadsnoderUtelatt);
        Assert.Contains(rapport.IkkeLastetPerArsak, a => a.Arsak == Forslagsarsak.HjemmelErMerknad && a.Antall == 1);
        Assert.Null(rad.KantId);
        Assert.False(await db.Strukturkanter.IgnoreQueryFilters().AnyAsync(k => k.FraVirksomhetId == organ.Id));
    }

    [Fact]
    public async Task Skriveveien_avviser_merknadshjemmel_ogsaa_manuelt_og_et_forslag_kan_ikke_godkjennes()
    {
        await using var db = _fixture.NyDbContext();
        await Strukturkanter.SeedStartsettAsync(db);
        var id = await new RettskildeImportTjeneste(db).ImporterAsync(Foreldrebetaling());
        var merknad = await db.RettskildeNoder.Where(n => n.RettskildeId == id && n.ErMerknad && n.NodeType == "ledd").FirstAsync();
        var a = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Merknad-A-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(a);
        await db.SaveChangesAsync();
        var kanter = new StrukturkantTjeneste(db);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => kanter.OpprettAsync(
            new NyStrukturkant(Strukturkanter.Kompetanse, "vedtak", Kantnode.Virksomhet(a.Id), null,
                HjemmelRettskildeId: id, HjemmelEid: merknad.Eid, Objekt: "foreldrebetaling"), "Kari Jurist"));
        Assert.Contains("#419", ex.Message);

        // Et forslag som alt ligger der (fra før merkingen) kan ikke godkjennes, og «godkjenn alle» hopper over det.
        var forslag = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO strukturkanter ("Id", kategori, typekode, fra_virksomhet_id, objekt, hjemmel_rettskilde_id, hjemmel_eid, hjemmel_node_id,
                status, oppdagelses_kilde, opprettet_av, opprettet_tidspunkt)
            VALUES ({forslag}, 'K', 'vedtak', {a.Id}, 'godkjenn-test', {id}, {merknad.Eid}, {merknad.Id}, 'maskinelt_foreslatt', 'ki:test', 'test', now())
            """);
        await Assert.ThrowsAsync<ArgumentException>(() => kanter.GodkjennAsync(forslag, "Kari Jurist"));
        await kanter.GodkjennAlleForHjemmelAsync(id, "ki:", "Kari Jurist");
        await using var les = _fixture.NyDbContext();
        Assert.Equal(Strukturkanter.MaskineltForeslatt, (await les.Strukturkanter.SingleAsync(k => k.Id == forslag)).Status);
        // Rydd opp, så avvisningstesten bare ser sine egne.
        await les.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM strukturkanter WHERE \"Id\" = {forslag}");
    }

    // ------------------------------------------------------------------ AK3: avvisningen

    [Fact]
    public async Task Avvisningen_toerrkjoerer_avviser_bare_forslag_med_begrunnelse_og_er_idempotent()
    {
        await using var db = _fixture.NyDbContext();
        await Strukturkanter.SeedStartsettAsync(db);
        var id = await new RettskildeImportTjeneste(db).ImporterAsync(Foreldrebetaling());
        var merknad = await db.RettskildeNoder.Where(n => n.RettskildeId == id && n.ErMerknad && n.NodeType == "ledd")
            .OrderBy(n => n.Sorteringsrekkefolge).FirstAsync();
        var normtekst = await db.RettskildeNoder.Where(n => n.RettskildeId == id && !n.ErMerknad && n.NodeType == "ledd")
            .OrderBy(n => n.Sorteringsrekkefolge).FirstAsync();
        var a = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Merknad-avvis-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(a);
        await db.SaveChangesAsync();

        async Task<Guid> KantAsync(string status, string eid, Guid? nodeId, string objekt)
        {
            var k = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO strukturkanter ("Id", kategori, typekode, fra_virksomhet_id, objekt, hjemmel_rettskilde_id, hjemmel_eid, hjemmel_node_id,
                    status, oppdagelses_kilde, opprettet_av, opprettet_tidspunkt)
                VALUES ({k}, 'K', 'vedtak', {a.Id}, {objekt}, {id}, {eid}, {nodeId}, {status}, 'ki:test', 'test', now())
                """);
            return k;
        }
        var maskinelt = await KantAsync(Strukturkanter.MaskineltForeslatt, merknad.Eid, merknad.Id, "m1");
        var utenNode = await KantAsync(Strukturkanter.MaskineltForeslatt, merknad.Eid, null, "m2"); // eldre rad uten hjemmel_node_id
        var manuelt = await KantAsync(Strukturkanter.ManueltForeslatt, merknad.Eid, merknad.Id, "m3");
        var godkjent = await KantAsync(Strukturkanter.Godkjent, merknad.Eid, merknad.Id, "g1");
        var vurdert = await KantAsync(Strukturkanter.FagligVurdert, merknad.Eid, merknad.Id, "f1");
        var vanlig = await KantAsync(Strukturkanter.MaskineltForeslatt, normtekst.Eid, normtekst.Id, "n1");
        var mine = new[] { maskinelt, utenNode, manuelt };

        var tjeneste = new MerknadshjemmelAvvisningTjeneste(db, new StrukturkantTjeneste(db));
        var torr = await tjeneste.KjorAsync(skriv: false);
        Assert.False(torr.Skrevet);
        Assert.Equal(mine.OrderBy(g => g), torr.Forslag.Select(f => f.KantId).Where(mine.Contains).OrderBy(g => g));
        Assert.DoesNotContain(torr.Forslag, f => f.KantId == godkjent || f.KantId == vurdert || f.KantId == vanlig);
        Assert.True(torr.IkkeBerort[Strukturkanter.Godkjent] >= 1);
        Assert.True(torr.IkkeBerort[Strukturkanter.FagligVurdert] >= 1);
        Assert.Equal("hjemmel er merknad/veiledning (#419)", torr.Begrunnelse);
        await using (var les = _fixture.NyDbContext())
        {
            Assert.Equal(3, await les.Strukturkanter.CountAsync(k => mine.Contains(k.Id))); // ingenting skrevet
        }

        var skrevet = await tjeneste.KjorAsync(skriv: true);
        Assert.True(skrevet.Skrevet);
        Assert.Equal(torr.Antall, skrevet.Antall);
        await using (var les = _fixture.NyDbContext())
        {
            var avviste = await les.Strukturkanter.IgnoreQueryFilters().Where(k => mine.Contains(k.Id)).ToListAsync();
            Assert.All(avviste, k => Assert.Equal(
                (Strukturkanter.Avvist, "hjemmel er merknad/veiledning (#419)", Merknadsnoder.SystemBruker),
                (k.Status, k.AvvisningBegrunnelse, k.SistEndretAv)));
            Assert.Equal(3, await les.Proveniens.CountAsync(p => mine.Contains(p.EntitetId) && p.Handling == "avvist"));
            Assert.Equal(Strukturkanter.Godkjent, (await les.Strukturkanter.SingleAsync(k => k.Id == godkjent)).Status);
            Assert.Equal(Strukturkanter.FagligVurdert, (await les.Strukturkanter.SingleAsync(k => k.Id == vurdert)).Status);
            Assert.Equal(Strukturkanter.MaskineltForeslatt, (await les.Strukturkanter.SingleAsync(k => k.Id == vanlig)).Status);
        }

        var igjen = await new MerknadshjemmelAvvisningTjeneste(db, new StrukturkantTjeneste(db)).KjorAsync(skriv: true);
        Assert.Equal(0, igjen.Antall);

        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM strukturkanter WHERE \"Id\" IN ({godkjent}, {vurdert}, {vanlig})");
    }
}
