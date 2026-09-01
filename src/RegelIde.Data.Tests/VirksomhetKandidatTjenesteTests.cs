using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="VirksomhetKandidatTjeneste"/> (docs/20 §2.6) mot ekte embedded Postgres — arbeidskøen
/// for godkjenning av virksomhetsforekomster funnet ved tekstsøk, OG (kandidatsøk-og-godkjenning-
/// runden) at <see cref="VirksomhetKandidatTjeneste.GodkjennAsync"/> nå faktisk oppretter en
/// <see cref="TekstTaggEntitet"/>. Selve sveipefunksjonen (tekstsøket) testes separat i
/// <see cref="VirksomhetKandidatSveipTjenesteTests"/> — testene her dekker køens egen logikk med
/// manuelt konstruerte kandidater.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class VirksomhetKandidatTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public VirksomhetKandidatTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<(Guid RettskildeId, RettskildeNodeEntitet Node)> OpprettAlkohollovenMedParagrafAsync(RegelIdeDbContext db)
    {
        var rettskildeId = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
        // Bladtekst (§ Tekst-feltet) finnes kun på ledd/punkt-noder, ikke på selve paragraf-noden —
        // se RettskildeNodeEntitet.Tekst sin kommentar. Må derfor slå opp et ledd her, ikke en paragraf.
        var ledd = await db.RettskildeNoder.FirstAsync(
            n => n.RettskildeId == rettskildeId && n.NodeType == "ledd" && n.Tekst != null && n.Tekst.Length >= 10);
        return (rettskildeId, ledd);
    }

    private static VirksomhetKandidatTjeneste NyTjeneste(RegelIdeDbContext db) =>
        new(db, new TekstTaggTjeneste(db, new VirksomhetOppslagTjeneste(db)));

    /// <summary>
    /// [Rettet, 2026-09-01] Fersk, syntetisk rettskilde med unik Guid — TIL FORSKJELL fra
    /// <see cref="OpprettAlkohollovenMedParagrafAsync"/>, som er idempotent på ELI og derfor deler
    /// SAMME rettskilde-rad med alle andre tester i denne DELTE DataTestCollection-databasen (se
    /// RettskildeImportTjeneste). Det er trygt for tester som kun sjekker EKSISTENS av en spesifikk
    /// kandidat-id, men et rettskilde-FILTRERT bulk-slett-antall (som i testen under) telles på tvers
    /// av ALLE tester som noensinne har lagt en avvist kandidat på den delte alkoholloven-raden —
    /// bekreftet ved en reell testfeil (forventet 1, fikk 4). Samme mønster som
    /// <c>NavnekandidatOppdagelseTjenesteTests.OpprettRettskildeMedNodeAsync</c> løser dette med.
    /// </summary>
    private static async Task<(Guid RettskildeId, RettskildeNodeEntitet Node)> OpprettSyntetiskRettskildeAsync(RegelIdeDbContext db)
    {
        var rettskildeId = Guid.NewGuid();
        var nodeEid = $"https://test/{rettskildeId:N}/§1/ledd-1";
        db.Rettskilder.Add(new RettskildeEntitet
        {
            Id = rettskildeId, Doctype = "doc", Kildetype = "Lov", Status = "Gjeldende", Importrolle = "referanse",
            Tittel = "Testlov " + rettskildeId, OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        var node = new RettskildeNodeEntitet
        {
            Id = Guid.NewGuid(), RettskildeId = rettskildeId, Eid = nodeEid, KildeId = "ledd-1",
            NodeType = "ledd", Tekst = "Testtekst for slettetest.",
        };
        db.RettskildeNoder.Add(node);
        await db.SaveChangesAsync();
        return (rettskildeId, node);
    }

    [Fact]
    public async Task Oppretter_kandidat_og_lister_i_ventende()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettAlkohollovenMedParagrafAsync(db);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var register = NyTjeneste(db);
        var kandidat = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, node.Eid, 0, 4, "sveip");

        Assert.Equal("Venter", kandidat.Status);
        Assert.Equal(0, kandidat.StartOffset);
        Assert.Equal(4, kandidat.EndOffset);
        var ventende = await register.ListerVentendeAsync(virksomhet.Id);
        Assert.Single(ventende);
    }

    [Fact]
    public async Task Gjentatt_sveip_gir_samme_rad_ikke_duplikat()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettAlkohollovenMedParagrafAsync(db);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var register = NyTjeneste(db);
        var forste = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, node.Eid, 0, 4, "sveip");
        var andre = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, node.Eid, 0, 4, "sveip");

        Assert.Equal(forste.Id, andre.Id);
    }

    [Fact]
    public async Task To_ulike_treff_i_samme_node_gir_to_uavhengige_kandidater()
    {
        // Designvalg dokumentert på VirksomhetKandidatEntitet.StartOffset: kandidat-nøkkelen er utvidet
        // til å inkludere START-posisjon, nettopp for å dekke dette tilfellet — ett sveip kan gi flere
        // treff i samme node, og de skal kunne godkjennes/avvises uavhengig av hverandre.
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettAlkohollovenMedParagrafAsync(db);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var register = NyTjeneste(db);
        var forste = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, node.Eid, 0, 4, "sveip");
        var andre = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, node.Eid, 5, 9, "sveip");

        Assert.NotEqual(forste.Id, andre.Id);
        Assert.Equal(2, (await register.ListerVentendeAsync(virksomhet.Id)).Count);
    }

    [Fact]
    public async Task Avvist_kandidat_dukker_ikke_opp_i_ventende_og_gjenskapes_ikke_av_nytt_sveip()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettAlkohollovenMedParagrafAsync(db);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var register = NyTjeneste(db);
        var kandidat = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, node.Eid, 0, 4, "sveip");
        await register.AvvisAsync(kandidat.Id, "Kari Jurist");

        Assert.Empty(await register.ListerVentendeAsync(virksomhet.Id));

        // Nytt "sveip" på samme (virksomhet, rettskilde, node, start) skal IKKE gjenskape en Venter-rad.
        var etterNyttSveip = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, node.Eid, 0, 4, "sveip");
        Assert.Equal(kandidat.Id, etterNyttSveip.Id);
        Assert.Equal("Avvist", etterNyttSveip.Status);
    }

    [Fact]
    public async Task Kan_ikke_avvise_kandidat_som_ikke_star_i_venter()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettAlkohollovenMedParagrafAsync(db);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var register = NyTjeneste(db);
        var kandidat = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, node.Eid, 0, 4, "sveip");
        await register.AvvisAsync(kandidat.Id, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(() => register.AvvisAsync(kandidat.Id, "Kari Jurist"));
    }

    [Fact]
    public async Task Godkjenn_setter_status_og_behandlet_av()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettAlkohollovenMedParagrafAsync(db);
        var begrepTerm = node.Tekst![..4];
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = virksomhet.Id, VirksomhetId = null,
            Term = begrepTerm, Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var register = NyTjeneste(db);
        var kandidat = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, node.Eid, 0, 4, "sveip");
        var godkjent = await register.GodkjennAsync(kandidat.Id, "Kari Jurist");

        Assert.NotNull(godkjent);
        Assert.Equal("Godkjent", godkjent!.Status);
        Assert.Equal("Kari Jurist", godkjent.BehandletAv);
        Assert.NotNull(godkjent.BehandletTidspunkt);
    }

    [Fact]
    public async Task Godkjenn_oppretter_ekte_teksttagg_med_kind_begrep_og_refid_navneform()
    {
        // Kravspek §4.2 pkt. 5 + den låste regelen (kandidatsøk-og-godkjenning-runden): en godkjent
        // kandidat skal produsere en RIKTIG TekstTagg — kind="begrep", RefId = navneform-Begrep-raden,
        // IKKE en egen "virksomhet"-kind som peker direkte på Virksomhet.
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettAlkohollovenMedParagrafAsync(db);
        var begrepTerm = node.Tekst![..4];
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        var navneform = new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = virksomhet.Id, VirksomhetId = null,
            Term = begrepTerm, Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        };
        db.Begreper.Add(navneform);
        await db.SaveChangesAsync();

        var register = NyTjeneste(db);
        var kandidat = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, node.Eid, 0, 4, "sveip");
        await register.GodkjennAsync(kandidat.Id, "Kari Jurist");

        // Filtrert på VirksomhetId (fersk Guid per test) — DB-en er DELT mellom alle tester i
        // samlingen (ICollectionFixture), og alkoholloven-importen er idempotent per ELI, så samme
        // node kan ha tagger fra andre testmetoder liggende fra før.
        var tagg = await db.TekstTagger.SingleAsync(t => t.RettskildeId == rettskildeId && t.NodeEid == node.Eid && t.VirksomhetId == virksomhet.Id);
        Assert.Equal("begrep", tagg.Kind);
        Assert.Equal(navneform.Id, tagg.RefId);
        Assert.Equal(virksomhet.Id, tagg.VirksomhetId);
        Assert.Equal(0, tagg.StartOffset);
        Assert.Equal(4, tagg.EndOffset);
        Assert.Equal(begrepTerm, tagg.QuoteExact);
        Assert.Equal("gjeldende", tagg.Entitetsstatus);
    }

    [Fact]
    public async Task Godkjenn_kaster_hvis_ingen_navneform_matcher_intervallet()
    {
        // Vernet dokumentert i GodkjennAsync: hvis noden er endret siden sveipet (eller navneformen
        // aldri fantes) skal godkjenning IKKE stille lagre en tagg som ikke faktisk stemmer.
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettAlkohollovenMedParagrafAsync(db);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync(); // Ingen navneform-Begrep registrert for denne virksomheten.

        var register = NyTjeneste(db);
        var kandidat = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, node.Eid, 0, 4, "sveip");

        await Assert.ThrowsAsync<ArgumentException>(() => register.GodkjennAsync(kandidat.Id, "Kari Jurist"));
        var uendret = await db.VirksomhetKandidater.FindAsync(kandidat.Id);
        Assert.Equal("Venter", uendret!.Status); // Status skal IKKE flippes til Godkjent når tagg-oppretting feiler.
    }

    [Fact]
    public async Task Kan_ikke_godkjenne_kandidat_som_ikke_star_i_venter()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettAlkohollovenMedParagrafAsync(db);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var register = NyTjeneste(db);
        var kandidat = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, node.Eid, 0, 4, "sveip");
        await register.AvvisAsync(kandidat.Id, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(() => register.GodkjennAsync(kandidat.Id, "Kari Jurist"));
    }

    [Fact]
    public async Task Kun_avviste_kan_hardslettes()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettAlkohollovenMedParagrafAsync(db);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var register = NyTjeneste(db);
        var kandidat = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, node.Eid, 0, 4, "sveip");

        await Assert.ThrowsAsync<ArgumentException>(() => register.HardslettAvvistAsync(kandidat.Id));

        await register.AvvisAsync(kandidat.Id, "Kari Jurist");
        Assert.True(await register.HardslettAvvistAsync(kandidat.Id));
    }

    // ---------- Massehardsletting (HardslettAlleAvvisteAsync) — samme Avvist-only-restriksjon som
    // HardslettAvvistAsync over, nå som bulk-variant med filter. ----------

    /// <summary>Massehardsletting filtrert på rettskilde skal KUN slette den ene rettskildens avviste
    /// rad — en 'Venter'-rad i SAMME rettskilde, og en avvist rad i en ANNEN rettskilde, skal begge
    /// forbli urørt (rammer verken feil status eller feil rettskilde).</summary>
    [Fact]
    public async Task HardslettAlleAvviste_med_rettskildefilter_rammer_kun_avviste_rader_i_valgt_rettskilde()
    {
        await using var db = _fixture.NyDbContext();
        // Syntetisk, IKKE delt alkoholloven-fixturen — et rettskilde-filtrert antall må telles mot
        // en rettskilde KUN denne testen bruker (se OpprettSyntetiskRettskildeAsync sin kommentar).
        var (rettskildeIdA, nodeA) = await OpprettSyntetiskRettskildeAsync(db);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var register = NyTjeneste(db);
        // To treff i SAMME rettskilde/node (ulike startOffset, se StartOffset-kommentaren) — én avvist,
        // én fortsatt Venter.
        var avvistKandidat = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeIdA, nodeA.Eid, 0, 4, "sveip");
        var venterKandidat = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeIdA, nodeA.Eid, 5, 9, "sveip");
        await register.AvvisAsync(avvistKandidat.Id, "Kari Jurist");

        var antallSlettet = await register.HardslettAlleAvvisteAsync(rettskildeId: rettskildeIdA);

        Assert.Equal(1, antallSlettet);
        Assert.False(await db.VirksomhetKandidater.AnyAsync(k => k.Id == avvistKandidat.Id));
        Assert.True(await db.VirksomhetKandidater.AnyAsync(k => k.Id == venterKandidat.Id)); // urørt — 'Venter'.
    }

    [Fact]
    public async Task HardslettAlleAvviste_med_statusVenter_kaster_og_sletter_ingenting()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettAlkohollovenMedParagrafAsync(db);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var register = NyTjeneste(db);
        var kandidat = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, node.Eid, 0, 4, "sveip");
        await register.AvvisAsync(kandidat.Id, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(
            () => register.HardslettAlleAvvisteAsync(rettskildeId: rettskildeId, status: "Venter"));

        Assert.True(await db.VirksomhetKandidater.AnyAsync(k => k.Id == kandidat.Id)); // ikke slettet.
    }

    /// <summary>Den definerende forskjellen fra navnekandidater: en 'Godkjent' rad kan ALDRI
    /// hardslettes bulk-veien heller (i motsetning til NavnekandidatOppdagelseTjeneste.SlettAlleAsync,
    /// som aksepterer status='Godkjent') — fordi taggen den opprettet ikke kan fjernes i etterkant
    /// (TekstTaggTjeneste.SlettAsync nekter å fjerne en tagg med RefId satt).</summary>
    [Fact]
    public async Task HardslettAlleAvviste_med_statusGodkjent_kaster_og_sletter_ingenting()
    {
        await using var db = _fixture.NyDbContext();
        var (rettskildeId, node) = await OpprettAlkohollovenMedParagrafAsync(db);
        var begrepTerm = node.Tekst![..4];
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-virksomhet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        db.Begreper.Add(new BegrepEntitet
        {
            Id = Guid.NewGuid(), Begrepskategori = "virksomhet", VirksomhetReferanseId = virksomhet.Id, VirksomhetId = null,
            Term = begrepTerm, Status = "publisert", OpprettetAv = "test", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var register = NyTjeneste(db);
        var kandidat = await register.OpprettEllerFinnAsync(virksomhet.Id, rettskildeId, node.Eid, 0, 4, "sveip");
        await register.GodkjennAsync(kandidat.Id, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(
            () => register.HardslettAlleAvvisteAsync(rettskildeId: rettskildeId, status: "Godkjent"));

        Assert.True(await db.VirksomhetKandidater.AnyAsync(k => k.Id == kandidat.Id)); // ikke slettet.
        Assert.True(await db.TekstTagger.AnyAsync(t => t.RettskildeId == rettskildeId && t.VirksomhetId == virksomhet.Id)); // taggen består urørt.
    }

    [Fact]
    public async Task HardslettAlleAvviste_uten_treff_returnerer_null_uten_a_kaste()
    {
        await using var db = _fixture.NyDbContext();
        // Syntetisk, IKKE delt alkoholloven-fixturen — se OpprettSyntetiskRettskildeAsync sin
        // kommentar: et rettskilde-filtrert "forventet 0 treff" holder ikke mot en rad andre tester
        // også bruker.
        var (rettskildeId, _) = await OpprettSyntetiskRettskildeAsync(db);

        var register = NyTjeneste(db);
        var antallSlettet = await register.HardslettAlleAvvisteAsync(rettskildeId: rettskildeId);

        Assert.Equal(0, antallSlettet);
    }
}
