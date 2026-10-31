using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// [Ny, issue #429 «Funn til vurdering», 2026-10-30] Registeret, statusflyten, premissporten, øyeblikksbildet og seed-fila.
/// Testene er formulert etter spørsmålene i akseptansekriteriene (CLAUDE.md §0): hvilke funn er godkjent, av hvem og med hvilken tekst;
/// hvilke bortforklaringer er prøvd; kan noe publiseres som ikke er godkjent; hva stopper premissporten; og hvilke funn har en kilde som
/// er endret siden vurderingen.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class FunnTjenesteTests(EmbeddedPostgresFixture fixture)
{
    private static string Unik(string s) => $"{s} {Guid.NewGuid():N}";

    private static NyttFunn Motstrid(string? nokkel = null, IReadOnlyList<NyKobling>? koblinger = null) => new(
        Funn.MuligMotstrid, null, Unik("Klageinstans"), "To bestemmelser gir ulikt svar:\n- A sier X.\n- B sier Y.", "manuell",
        InternMerknad: "intern", EksternNokkel: nokkel, Koblinger: koblinger,
        Vurderingspunkter: [new NyttVurderingspunkt(Funn.BortforklaringProvd, "ingen opphevelse funnet.", "Opphevelse")]);

    private static async Task<(Guid Rettskilde, string Eli, string Eid)> RettskildeMedLeddAsync(RegelIdeDbContext db)
    {
        var id = Guid.NewGuid();
        var eli = $"https://lovdata.no/eli/forskrift/2099/01/01/{Random.Shared.Next(1_000_000, 9_999_999)}/nor";
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = id, Doctype = "act", Kildetype = "Forskrift", Status = "Gjeldende", Importrolle = "primaer", Tittel = Unik("Testforskrift"),
            Eli = eli, AknXml = "<akomaNtoso/>", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var paragraf = $"{eli}/§1";
        db.RettskildeNoder.Add(new RettskildeNodeEntitet { Id = Guid.NewGuid(), RettskildeId = id, Eid = paragraf, KildeId = "§1", NodeType = "paragraf" });
        db.RettskildeNoder.Add(new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = id, Eid = paragraf + "/ledd-1", KildeId = "§1/ledd-1", NodeType = "ledd", Tekst = "Klage går til departementet.",
            TekstHash = "h1",
        });
        await db.SaveChangesAsync();
        return (id, eli, paragraf + "/ledd-1");
    }

    private static async Task<FunnEntitet> GodkjentAsync(FunnTjeneste t, NyttFunn n)
    {
        var (f, _) = await t.OpprettAsync(n, "Test");
        await t.JuristvurderAsync(f.Id, "Ordlyden er i motstrid.", Funn.KiAgent, "jurist-utfordrer");
        return (await t.GodkjennForPubliseringAsync(f.Id, "Publiseringsansvarlig"))!;
    }

    [Fact]
    public async Task Hvilke_funn_er_godkjent_av_hvem_og_med_hvilken_tekst()
    {
        await using var db = fixture.NyDbContext();
        var t = new FunnTjeneste(db);
        var (rk, eli, eid) = await RettskildeMedLeddAsync(db);
        var f = await GodkjentAsync(t, Motstrid(Unik("test"), [new NyKobling(Funn.MotstridendeBestemmelse, "§ 1 første ledd", "sier departementet", eli, eid)]));

        Assert.Equal(Funn.GodkjentForPublisering, f.Status);
        Assert.Equal("Publiseringsansvarlig", f.GodkjentAv);
        Assert.Equal(FunnTjeneste.Hash(f.GodkjentInnholdJson!), f.GodkjentInnholdHash);
        // Bestemmelsen er slått opp i korpuset på ELI + eId.
        var kobling = f.Koblinger.Single();
        Assert.Equal(rk, kobling.RettskildeId);
        Assert.NotNull(kobling.NodeId);

        var publiserbare = await t.HentPubliserbareAsync();
        var mitt = publiserbare.Funn.Single(j => j.GetProperty("id").GetString() == f.EksternNokkel);
        Assert.Equal(f.Tittel, mitt.GetProperty("tittel").GetString());
        Assert.Equal("Opphevelse", mitt.GetProperty("bortforklaringer")[0].GetProperty("overskrift").GetString());
        // Ingen personnavn og ingen intern merknad i det som publiseres.
        var tekst = mitt.GetRawText();
        Assert.DoesNotContain("Publiseringsansvarlig", tekst);
        Assert.DoesNotContain("jurist-utfordrer", tekst);
        Assert.DoesNotContain("intern", tekst);
        Assert.True(mitt.GetProperty("juristvurdertAvKi").GetBoolean());
    }

    [Fact]
    public async Task Endret_tekst_etter_godkjenning_publiseres_ikke_men_intern_merknad_beholder_godkjenningen()
    {
        await using var db = fixture.NyDbContext();
        var t = new FunnTjeneste(db);
        var f = await GodkjentAsync(t, Motstrid(Unik("test")));

        await t.OppdaterAsync(f.Id, new FunnEndring(f.Tittel, f.Beskrivelse, null, f.Vurdering, "ny intern merknad"), "Test");
        Assert.Equal(Funn.GodkjentForPublisering, (await t.HentAsync(f.Id))!.Status);

        await t.OppdaterAsync(f.Id, new FunnEndring(f.Tittel, f.Beskrivelse + " Endret.", null, f.Vurdering, null), "Test");
        var etter = (await t.HentAsync(f.Id))!;
        Assert.Equal(Funn.Juristvurdert, etter.Status);
        Assert.Null(etter.GodkjentInnholdJson);
        Assert.DoesNotContain((await t.HentPubliserbareAsync()).Funn, j => j.GetProperty("id").GetString() == f.EksternNokkel);
        Assert.True(await db.Proveniens.AnyAsync(p => p.EntitetId == f.Id && p.Handling == "publisering_trukket_ved_endring"));
    }

    [Fact]
    public async Task Et_oyeblikksbilde_med_feil_hash_holdes_tilbake()
    {
        await using var db = fixture.NyDbContext();
        var t = new FunnTjeneste(db);
        var f = await GodkjentAsync(t, Motstrid(Unik("test")));
        f.GodkjentInnholdJson = f.GodkjentInnholdJson!.Replace(f.Tittel, "Endret utenom tjenesten");
        await db.SaveChangesAsync();

        var p = await t.HentPubliserbareAsync();
        Assert.Contains(f.EksternNokkel!, p.HoldtTilbake);
        Assert.DoesNotContain(p.Funn, j => j.GetProperty("id").GetString() == f.EksternNokkel);
    }

    [Fact]
    public async Task Statusflyten_stopper_ulovlige_overganger()
    {
        await using var db = fixture.NyDbContext();
        var t = new FunnTjeneste(db);
        var (f, _) = await t.OpprettAsync(Motstrid() with { Vurderingspunkter = [] }, "Test");

        await Assert.ThrowsAsync<InvalidOperationException>(() => t.GodkjennForPubliseringAsync(f.Id, "X")); // ikke juristvurdert
        // En juristvurdering uten prøvd bortforklaring avvises (#429: hvert funn viser hva som er prøvd).
        await Assert.ThrowsAsync<ArgumentException>(() => t.JuristvurderAsync(f.Id, "Vurdering", Funn.Menneske, "Kari Jurist"));
        await t.JuristvurderAsync(f.Id, "Vurdering", Funn.Menneske, "Kari Jurist",
            [new NyttVurderingspunkt(Funn.BortforklaringProvd, "prøvd"), new NyttVurderingspunkt(Funn.Vurdering, "for motstriden", Sikkerhet: "hoy")]);
        await t.GodkjennForPubliseringAsync(f.Id, "X");
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.ForklarBortAsync(f.Id, "Begrunnelse", "X")); // godkjent: trekk tilbake først
        await t.TrekkTilbakeAsync(f.Id, "X");
        var forklart = await t.ForklarBortAsync(f.Id, "Lex specialis.", "Kari Jurist");
        Assert.Equal(Funn.ForklartBort, forklart!.Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.JuristvurderAsync(f.Id, "Ny", Funn.Menneske, "X"));
        // Sikkerhet bare på type vurdering.
        await Assert.ThrowsAsync<ArgumentException>(() => t.OpprettAsync(Motstrid() with
        {
            Vurderingspunkter = [new NyttVurderingspunkt(Funn.BortforklaringProvd, "x", Sikkerhet: "hoy")],
        }, "Test"));
    }

    [Fact]
    public async Task Eksternnokkel_gjor_registreringen_idempotent()
    {
        await using var db = fixture.NyDbContext();
        var t = new FunnTjeneste(db);
        var nokkel = Unik("agent");
        var (forste, ny1) = await t.OpprettAsync(Motstrid(nokkel), "Agent");
        var (andre, ny2) = await t.OpprettAsync(Motstrid(nokkel) with { Tittel = "Annen tittel" }, "Agent");
        Assert.True(ny1);
        Assert.False(ny2);
        Assert.Equal(forste.Id, andre.Id);
        Assert.Equal(forste.Tittel, andre.Tittel);
    }

    [Fact]
    public async Task En_bestemmelse_som_ikke_er_i_korpuset_gir_null_ikke_naermeste_treff()
    {
        await using var db = fixture.NyDbContext();
        var t = new FunnTjeneste(db);
        var (_, eli, _) = await RettskildeMedLeddAsync(db);
        var (f, _) = await t.OpprettAsync(Motstrid(koblinger:
        [
            new NyKobling(Funn.GjelderBestemmelse, "Finnes ikke", RettskildeEli: "https://lovdata.no/eli/forskrift/1900/01/01/1/nor", Eid: "x/§1"),
            new NyKobling(Funn.GjelderBestemmelse, "§ 99", RettskildeEli: eli, Eid: eli + "/§99"),
        ]), "Test");
        Assert.All(f.Koblinger.Where(k => k.Etikett == "Finnes ikke"), k => Assert.Null(k.RettskildeId));
        var p99 = f.Koblinger.Single(k => k.Etikett == "§ 99");
        Assert.NotNull(p99.RettskildeId);
        Assert.Null(p99.NodeId);
        await Assert.ThrowsAsync<ArgumentException>(() => t.OpprettAsync(Motstrid(koblinger:
            [new NyKobling(Funn.BerortAktor, "Uten mål")]), "Test"));
    }

    [Fact]
    public async Task Premissporten_krever_faglig_vurdert_navneform_og_etterfolgerkant()
    {
        await using var db = fixture.NyDbContext();
        var nedlagt = new Virksomhet { Id = Guid.NewGuid(), Navn = Unik("Fiskeri- og kystdepartementet"), Livsstatus = "nedlagt", Aktiv = false };
        var etterfolger = new Virksomhet { Id = Guid.NewGuid(), Navn = Unik("Nærings- og fiskeridepartementet") };
        db.Virksomheter.AddRange(nedlagt, etterfolger);
        var navneform = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = nedlagt.Id, Navneformgrunn = "gjeldende",
            Term = nedlagt.Navn, Status = "publisert", OpprettetAv = "test", Godkjenning = Strukturkanter.MaskineltForeslatt,
        };
        db.Begreper.Add(navneform);
        var kant = new StrukturkantEntitet
        {
            Id = Guid.NewGuid(), Kategori = Strukturkanter.Relasjon, Typekode = "etterfolger", FraVirksomhetId = etterfolger.Id,
            TilVirksomhetId = nedlagt.Id, Status = Strukturkanter.MaskineltForeslatt, OppdagelsesKilde = "monster:livslop-etterfolger",
            OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
            KildeUtenforKorpusTekst = "Testvedtak", KildeUtenforKorpusType = "kgl_res", KildeUtenforKorpusDokumentasjon = "primaer",
        };
        db.Strukturkanter.Add(kant);
        await db.SaveChangesAsync();
        var t = new FunnTjeneste(db);

        var (uten, _) = await t.OpprettAsync(new NyttFunn(Funn.UtgattAktor, "nedlagt_aktor", Unik("Viser til nedlagt"), "Teksten viser til X.", "manuell",
            Koblinger: [new NyKobling(Funn.Navneform, "Fiskeri- og kystdepartementet", BegrepId: navneform.Id)],
            Vurderingspunkter: [new NyttVurderingspunkt(Funn.BortforklaringProvd, "ingen ressortoverføring som retter teksten.")]), "Test");
        var (med, _) = await t.OpprettAsync(new NyttFunn(Funn.UtgattAktor, "nedlagt_aktor", Unik("Viser til nedlagt, med etterfølger"),
            "Teksten viser til X.", "manuell",
            Koblinger:
            [
                new NyKobling(Funn.Navneform, "Fiskeri- og kystdepartementet", BegrepId: navneform.Id),
                new NyKobling(Funn.Etterfolger, "Nærings- og fiskeridepartementet", StrukturkantId: kant.Id),
            ],
            Vurderingspunkter: [new NyttVurderingspunkt(Funn.BortforklaringProvd, "prøvd")]), "Test");
        foreach (var id in new[] { uten.Id, med.Id }) await t.JuristvurderAsync(id, "Vurdert.", Funn.KiAgent, "agent");

        // Navneformen er bare maskinelt foreslått: begge stoppes.
        var feil = await Assert.ThrowsAsync<InvalidOperationException>(() => t.GodkjennForPubliseringAsync(uten.Id, "X"));
        Assert.Contains("ikke faglig vurdert", feil.Message);

        navneform.Godkjenning = Strukturkanter.FagligVurdert;
        await db.SaveChangesAsync();
        // Uten etterfølgerkant: åpen, og publiseres med tom etterfølgerliste («etterfølger ukjent»).
        var godkjent = await t.GodkjennForPubliseringAsync(uten.Id, "X");
        using (var dok = JsonDocument.Parse(godkjent!.GodkjentInnholdJson!))
        {
            Assert.Equal(0, dok.RootElement.GetProperty("etterfolgere").GetArrayLength());
        }
        // Med en etterfølgerkant som ikke er vurdert: «etterfølger ikke vurdert».
        var feil2 = await Assert.ThrowsAsync<InvalidOperationException>(() => t.GodkjennForPubliseringAsync(med.Id, "X"));
        Assert.Contains("Etterfølger ikke vurdert", feil2.Message);
        kant.Status = Strukturkanter.FagligVurdert;
        await db.SaveChangesAsync();
        Assert.Equal(Funn.GodkjentForPublisering, (await t.GodkjennForPubliseringAsync(med.Id, "X"))!.Status);
    }

    [Fact]
    public async Task Hvilke_funn_har_en_kilde_som_er_endret_siden_vurderingen()
    {
        await using var db = fixture.NyDbContext();
        var t = new FunnTjeneste(db);
        var (rk, eli, eid) = await RettskildeMedLeddAsync(db);
        var (f, _) = await t.OpprettAsync(Motstrid(koblinger: [new NyKobling(Funn.MotstridendeBestemmelse, "§ 1", RettskildeEli: eli, Eid: eid)]), "Test");
        await t.JuristvurderAsync(f.Id, "Vurdert.", Funn.KiAgent, "agent");
        var noder = (await db.RettskildeNoder.Where(n => n.RettskildeId == rk).ToListAsync())
            .Select(n => new HjemmelEndretFlagg.Nodeavtrykk(n.Eid, n.Overskrift, n.TekstHash)).ToList();

        await HjemmelEndretFlagg.FlaggForNyVersjonAsync(db, rk, noder, "resynk", default);
        await db.SaveChangesAsync();
        Assert.Null((await db.Funn.AsNoTracking().SingleAsync(x => x.Id == f.Id)).KildeEndretTidspunkt); // uendret tekst: ikke flagget

        var endret = noder.Select(n => n.Eid == eid ? n with { TekstHash = "ny" } : n).ToList();
        await HjemmelEndretFlagg.FlaggForNyVersjonAsync(db, rk, endret, "resynk", default);
        await db.SaveChangesAsync();
        var etter = await db.Funn.AsNoTracking().SingleAsync(x => x.Id == f.Id);
        Assert.NotNull(etter.KildeEndretTidspunkt);
        Assert.Equal(Funn.Juristvurdert, etter.Status); // et flagg, ikke en statusendring

        await t.JuristvurderAsync(f.Id, "Vurdert på nytt.", Funn.KiAgent, "agent");
        Assert.Null((await db.Funn.AsNoTracking().SingleAsync(x => x.Id == f.Id)).KildeEndretTidspunkt);
    }

    [Fact]
    public async Task Hvilke_funn_finnes_i_rettskilde_X_og_hvilke_bortforklaringer_er_provd()
    {
        await using var db = fixture.NyDbContext();
        var t = new FunnTjeneste(db);
        var (rk, eli, eid) = await RettskildeMedLeddAsync(db);
        var (f, _) = await t.OpprettAsync(Motstrid(koblinger: [new NyKobling(Funn.GjelderBestemmelse, "§ 1", RettskildeEli: eli, Eid: eid)]), "Test");

        var iRettskilden = await t.ListeAsync(rettskildeId: rk);
        var treff = Assert.Single(iRettskilden);
        Assert.Equal(f.Id, treff.Id);
        Assert.Equal(["Opphevelse"], treff.Vurderingspunkter.Where(p => p.Type == Funn.BortforklaringProvd).Select(p => p.Overskrift));
    }

    /// <summary>Seed-fila: de fem funnene fra #429 med riktig status, funn 1 er det eneste som publiseres, og en ny kjøring endrer ingenting.</summary>
    [Fact]
    public async Task Seed_gir_de_fem_funnene_og_bare_funn_1_publiseres()
    {
        await using var db = fixture.NyDbContext();
        var forste = await FunnSeed.SeedAsync(db);
        Assert.Equal(5, forste.Nye + forste.Fantes);
        var andre = await FunnSeed.SeedAsync(db);
        Assert.Equal((0, 5), (andre.Nye, andre.Fantes));

        var rader = await db.Funn.AsNoTracking().Include(f => f.Vurderingspunkter).Where(f => f.EksternNokkel!.StartsWith("429-")).ToListAsync();
        Assert.Equal(Funn.GodkjentForPublisering, rader.Single(f => f.EksternNokkel == "429-funn-1").Status);
        Assert.Equal(Funn.ForklartBort, rader.Single(f => f.EksternNokkel == "429-kontroll-laerer").Status);
        Assert.Equal(3, rader.Count(f => f.Status == Funn.Oppdaget));
        Assert.Equal(5, rader.Single(f => f.EksternNokkel == "429-funn-1").Vurderingspunkter.Count(p => p.Type == Funn.BortforklaringProvd));

        var publiserbare = await new FunnTjeneste(db).HentPubliserbareAsync();
        var funn1 = Assert.Single(publiserbare.Funn, j => j.GetProperty("id").GetString()!.StartsWith("429-"));
        Assert.Equal("429-funn-1", funn1.GetProperty("id").GetString());
        Assert.Equal("2026-10-29", funn1.GetProperty("godkjentDato").GetString());
        var tekst = funn1.GetRawText();
        Assert.DoesNotContain("Johann", tekst);
        Assert.DoesNotContain("Kontrollert mot basen", tekst); // intern merknad
        Assert.Equal(2, funn1.GetProperty("bestemmelser").GetArrayLength());
        Assert.Equal(5, funn1.GetProperty("kilder").GetArrayLength());
    }
}
