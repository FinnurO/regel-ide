using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>[Ny, issue #290] Se HandlingRegelverksreferanseforslagTjeneste sin klassekommentar — samme
/// "ekte embedded Postgres + ekte KiAgentKlientStub"-mønster som
/// TjenesteRegelverksreferanseforslagTjenesteTests (issue #286), her uten embedding-infrastruktur
/// (deterministisk regex+bekreftelse-innsnevring i stedet).</summary>
[Collection(DataTestCollection.Navn)]
public class HandlingRegelverksreferanseforslagTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public HandlingRegelverksreferanseforslagTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static HandlingRegelverksreferanseforslagTjeneste NyTjeneste(RegelIdeDbContext db) =>
        new(db, new KiAgentKlientStub(), new HandlingregisterTjeneste(db), new ConfigurationBuilder().Build());

    private sealed record Testoppsett(
        Guid VirksomhetId, Guid RettskildeId, string Eli, Guid TjenesteId, Guid HandlingId,
        string ForsteParagrafnummer, string AndreParagrafnummer);

    /// <summary>Bygger en virksomhet + importert alkoholloven + en tjeneste/handling med en
    /// DOKUMENTNIVÅ regelverksreferanse og en fritekst-henvisning som nevner TO ekte, bekreftede
    /// paragrafnumre — samme form som "§§ 21-4, 22-3" i det virkelige Oppgaveregister-korpuset.</summary>
    private static async Task<Testoppsett> ByggTestoppsettAsync(RegelIdeDbContext db, string? fritekst = null)
    {
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 7, 24)));
        var eli = await db.Rettskilder.Where(r => r.Id == rettskildeId).Select(r => r.Eli!).SingleAsync();

        // Paragraf-NODEN selv har aldri egen Tekst (kun ledd/punkt har, se RettskildeNodeEntitet.Tekst
        // sin doc-kommentar — bekreftet empirisk 2026-09-30 mot nettopp denne fixturen: 98 paragraf-
        // noder, samtlige med Tekst=null) — HandlingRegelverksreferanseforslagTjeneste samler selv
        // paragrafens tekst fra dens ledd/punkt-etterkommere (SamleParagrafTekst), men testoppsettet her
        // trenger to paragrafer som FAKTISK har minst én tekstbærende etterkommer, ellers ville
        // KjorForslagAsync selv droppet dem stille som "ingen kandidat".
        var alleNoder = await db.RettskildeNoder.Where(n => n.RettskildeId == rettskildeId).ToListAsync();
        var paragrafPerId = alleNoder.Where(n => n.NodeType == "paragraf").ToDictionary(n => n.Id);
        var paragrafNoder = alleNoder
            .Where(n => n.Tekst != null && n.ParentNodeId != null && paragrafPerId.ContainsKey(n.ParentNodeId.Value))
            .OrderBy(n => n.Sorteringsrekkefolge)
            .Select(n => paragrafPerId[n.ParentNodeId!.Value])
            .DistinctBy(n => n.Id)
            .Take(2)
            .ToList();
        Assert.True(paragrafNoder.Count == 2, "Testforutsetning: alkoholloven-fixturen må ha minst to paragrafer med tekstbærende etterkommere.");
        string ParagrafnummerFraEid(string eid) => eid[(eli.Length + 1)..]; // "{eli}/§X-Y" -> "§X-Y"
        var forsteNr = ParagrafnummerFraEid(paragrafNoder[0].Eid);
        var andreNr = ParagrafnummerFraEid(paragrafNoder[1].Eid);

        var tjeneste = new TjenesteEntitet
        {
            Id = Guid.NewGuid(), VirksomhetId = virksomhet, Tittel = "Oppgaveregisteret — Testkommunen",
            Status = "utkast", OpprettetAv = "oppgaveregister-import",
        };
        db.Tjenester.Add(tjeneste);
        var handling = new HandlingEntitet
        {
            Id = Guid.NewGuid(), TjenesteId = tjeneste.Id, Navn = "Søknad om skjenkebevilling", Handlingstype = "soke",
            Status = "utkast", OpprettetAv = "oppgaveregister-import",
        };
        db.Handlinger.Add(handling);
        db.HandlingRegelverksreferanser.Add(new HandlingRegelverksreferanseEntitet
        {
            Id = Guid.NewGuid(), HandlingId = handling.Id, TilRettskildeId = rettskildeId, TilEid = eli,
            KildeHenvisningFritekst = fritekst ?? $"§§ {forsteNr[1..]}, {andreNr[1..]}",
        });
        await db.SaveChangesAsync();

        return new Testoppsett(virksomhet, rettskildeId, eli, tjeneste.Id, handling.Id, forsteNr, andreNr);
    }

    // ---------- TrekkUtFlereParagrafnumre (ren funksjon, ingen DB) ----------

    [Theory]
    [InlineData("§ 42", new[] { "§42" })]
    [InlineData("§4-1", new[] { "§4-1" })]
    [InlineData("§§ 21-4, 22-3", new[] { "§21-4", "§22-3" })]
    [InlineData("§ 5, jf. § 3", new[] { "§5", "§3" })]
    [InlineData("§§ 1 til 5", new[] { "§1", "§5" })]
    [InlineData("Kapittel 5", new string[0])]
    [InlineData(null, new string[0])]
    public void TrekkUtFlereParagrafnumre_gir_forventede_kandidater(string? henvisning, string[] forventet)
    {
        var resultat = HandlingRegelverksreferanseforslagTjeneste.TrekkUtFlereParagrafnumre(henvisning ?? "");
        Assert.Equal(forventet, resultat);
    }

    // ---------- KjorForslagAsync / kø / godkjenn / avvis ----------

    [Fact]
    public async Task Kjorer_forslag_oppretter_ventende_forslagsrad_for_dokumentnivarad_med_flere_paragrafer()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await ByggTestoppsettAsync(db);

        var forslagstjeneste = NyTjeneste(db);
        var resultat = await forslagstjeneste.KjorForslagAsync(oppsett.VirksomhetId, [oppsett.RettskildeId], "system-ki");

        Assert.Equal(1, resultat.AntallKandidatrader);
        Assert.Equal(1, resultat.AntallVurdert);
        Assert.True(resultat.AntallNyeForslag >= 1);

        var forslag = await db.HandlingRegelverksreferanseForslag.Where(f => f.HandlingId == oppsett.HandlingId).ToListAsync();
        Assert.NotEmpty(forslag);
        Assert.All(forslag, f => Assert.Equal("Venter", f.Status));
        Assert.All(forslag, f => Assert.Equal("stub-v1", f.AiForslagVersjon));
        Assert.All(forslag, f => Assert.NotNull(f.Begrunnelse));
        // Stubben ekkoer FØRSTE [eId] i konteksten — som er den FØRSTE bekreftede kandidatparagrafen.
        Assert.Contains(forslag, f => f.TilEid == $"{oppsett.Eli}/{oppsett.ForsteParagrafnummer}");
    }

    [Fact]
    public async Task Henvisning_uten_paragraftegn_gir_ingen_forslag()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await ByggTestoppsettAsync(db, fritekst: "Kapittel 5");

        var forslagstjeneste = NyTjeneste(db);
        var resultat = await forslagstjeneste.KjorForslagAsync(oppsett.VirksomhetId, [oppsett.RettskildeId], "system-ki");

        Assert.Equal(1, resultat.AntallKandidatrader); // fortsatt en kandidatrad (dokumentnivå+fritekst)…
        Assert.Equal(0, resultat.AntallVurdert); // …men ingen "§" å tolke, så ingen KI-kall.
        Assert.Equal(0, resultat.AntallNyeForslag);
    }

    [Fact]
    public async Task Rad_uten_fritekst_foreslas_ikke()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await ByggTestoppsettAsync(db);
        var rad = await db.HandlingRegelverksreferanser.SingleAsync(r => r.HandlingId == oppsett.HandlingId);
        rad.KildeHenvisningFritekst = null;
        await db.SaveChangesAsync();

        var forslagstjeneste = NyTjeneste(db);
        var resultat = await forslagstjeneste.KjorForslagAsync(oppsett.VirksomhetId, [oppsett.RettskildeId], "system-ki");

        Assert.Equal(0, resultat.AntallKandidatrader);
    }

    [Fact]
    public async Task Rad_allerede_pa_paragrafniva_foreslas_ikke()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await ByggTestoppsettAsync(db);
        var rad = await db.HandlingRegelverksreferanser.SingleAsync(r => r.HandlingId == oppsett.HandlingId);
        rad.TilEid = $"{oppsett.Eli}/{oppsett.ForsteParagrafnummer}"; // allerede oppgradert (f.eks. av seeden selv)
        await db.SaveChangesAsync();

        var forslagstjeneste = NyTjeneste(db);
        var resultat = await forslagstjeneste.KjorForslagAsync(oppsett.VirksomhetId, [oppsett.RettskildeId], "system-ki");

        Assert.Equal(0, resultat.AntallKandidatrader);
    }

    [Fact]
    public async Task Kjort_forslag_pa_nytt_oppretter_ingen_dublett()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await ByggTestoppsettAsync(db);
        var forslagstjeneste = NyTjeneste(db);

        var forsteKjoring = await forslagstjeneste.KjorForslagAsync(oppsett.VirksomhetId, [oppsett.RettskildeId], "system-ki");
        var antallEtterForste = await db.HandlingRegelverksreferanseForslag.CountAsync();
        var andreKjoring = await forslagstjeneste.KjorForslagAsync(oppsett.VirksomhetId, [oppsett.RettskildeId], "system-ki");

        Assert.True(forsteKjoring.AntallNyeForslag >= 1);
        Assert.Equal(0, andreKjoring.AntallNyeForslag);
        Assert.Equal(antallEtterForste, await db.HandlingRegelverksreferanseForslag.CountAsync());
    }

    [Fact]
    public async Task Godkjenn_oppgraderer_eksisterende_regelverksreferanse_til_paragrafniva()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await ByggTestoppsettAsync(db);
        var forslagstjeneste = NyTjeneste(db);
        await forslagstjeneste.KjorForslagAsync(oppsett.VirksomhetId, [oppsett.RettskildeId], "system-ki");
        var forslag = await db.HandlingRegelverksreferanseForslag.FirstAsync(f => f.HandlingId == oppsett.HandlingId);

        var godkjent = await forslagstjeneste.GodkjennAsync(forslag.Id, "Kari Jurist");

        Assert.NotNull(godkjent);
        Assert.Equal("Godkjent", godkjent!.Status);
        Assert.Equal("Kari Jurist", godkjent.BehandletAv);
        Assert.NotNull(godkjent.BehandletTidspunkt);

        var ekteReferanse = await db.HandlingRegelverksreferanser.SingleAsync(r => r.HandlingId == oppsett.HandlingId);
        Assert.Equal(forslag.TilEid, ekteReferanse.TilEid);
        Assert.NotEqual(oppsett.Eli, ekteReferanse.TilEid); // ikke lenger dokumentnivå
        // Ingen søsterrad opprettet — fortsatt kun ÉN regelverksreferanse-rad for dette (handling, rettskilde)-paret.
        Assert.Single(await db.HandlingRegelverksreferanser.Where(r => r.HandlingId == oppsett.HandlingId).ToListAsync());
    }

    [Fact]
    public async Task Avvis_setter_status_avvist_uten_a_oppgradere_ekte_referanse()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await ByggTestoppsettAsync(db);
        var forslagstjeneste = NyTjeneste(db);
        await forslagstjeneste.KjorForslagAsync(oppsett.VirksomhetId, [oppsett.RettskildeId], "system-ki");
        var forslag = await db.HandlingRegelverksreferanseForslag.FirstAsync(f => f.HandlingId == oppsett.HandlingId);

        var avvist = await forslagstjeneste.AvvisAsync(forslag.Id, "Kari Jurist");

        Assert.Equal("Avvist", avvist!.Status);
        var ekteReferanse = await db.HandlingRegelverksreferanser.SingleAsync(r => r.HandlingId == oppsett.HandlingId);
        Assert.Equal(oppsett.Eli, ekteReferanse.TilEid); // fortsatt dokumentnivå — ikke rørt av en avvisning.
    }

    [Fact]
    public async Task Godkjenn_av_allerede_behandlet_forslag_kaster()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await ByggTestoppsettAsync(db);
        var forslagstjeneste = NyTjeneste(db);
        await forslagstjeneste.KjorForslagAsync(oppsett.VirksomhetId, [oppsett.RettskildeId], "system-ki");
        var forslag = await db.HandlingRegelverksreferanseForslag.FirstAsync(f => f.HandlingId == oppsett.HandlingId);
        await forslagstjeneste.AvvisAsync(forslag.Id, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(() => forslagstjeneste.GodkjennAsync(forslag.Id, "Kari Jurist"));
    }

    [Fact]
    public async Task Ukjent_rettskilde_kastes_ingen_gjettet_fallback()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = Guid.NewGuid();
        db.Virksomheter.Add(new Virksomhet { Id = virksomhet, Navn = "Testkommunen" });
        await db.SaveChangesAsync();

        var forslagstjeneste = NyTjeneste(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            forslagstjeneste.KjorForslagAsync(virksomhet, [Guid.NewGuid()], "system-ki"));
    }

    [Fact]
    public async Task ListerMedHandlingAsync_default_viser_kun_ventende()
    {
        await using var db = _fixture.NyDbContext();
        var oppsett = await ByggTestoppsettAsync(db);
        var forslagstjeneste = NyTjeneste(db);
        await forslagstjeneste.KjorForslagAsync(oppsett.VirksomhetId, [oppsett.RettskildeId], "system-ki");
        var forslag = await db.HandlingRegelverksreferanseForslag.FirstAsync(f => f.HandlingId == oppsett.HandlingId);
        await forslagstjeneste.AvvisAsync(forslag.Id, "Kari Jurist");

        var kunVenter = await forslagstjeneste.ListerMedHandlingAsync(oppsett.VirksomhetId);
        var alle = await forslagstjeneste.ListerMedHandlingAsync(oppsett.VirksomhetId, status: null);

        Assert.Empty(kunVenter);
        Assert.NotEmpty(alle);
        Assert.All(alle, r => Assert.Equal("Søknad om skjenkebevilling", r.Handling.Navn));
    }
}
