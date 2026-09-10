using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data.Tests;

/// <summary>
/// <see cref="VirksomhetsbegrepTjeneste"/> (docs/20 §2.3/§2.4) mot ekte embedded Postgres — de to nye
/// <see cref="BegrepEntitet.Begrepskategori"/>-verdiene, delt/nasjonal referansedata uten eiende
/// virksomhet (til forskjell fra <see cref="BegrepsregisterTjeneste"/>s ordinære fakta-/handlingsbegrep).
/// </summary>
[Collection(DataTestCollection.Navn)]
public class VirksomhetsbegrepTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public VirksomhetsbegrepTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<Guid> OpprettAlkohollovenAsync(RegelIdeDbContext db)
    {
        var resultat = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesAlkoholloven(), new DateOnly(2026, 8, 22)));
        return resultat;
    }

    /// <summary>
    /// [Ny, nemnd/sekretariat-runden, 2026-09-09] Dublett-navneformer fantes i basen og ga dobbelt
    /// markering av samme organnavn i samme setning (observert for «Energiklagenemnda» og
    /// «Konkurransetilsynet»). Case-insensitivt fordi sveipet er det.
    /// </summary>
    [Fact]
    public async Task Nekter_samme_navneform_to_ganger_mot_samme_virksomhet()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-nemnd-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        var register = new VirksomhetsbegrepTjeneste(db);
        await register.OpprettVirksomhetsbegrepAsync(virksomhet.Id, "Energiklagenemnda", "Kari Jurist");

        var feil = await Assert.ThrowsAsync<ArgumentException>(() =>
            register.OpprettVirksomhetsbegrepAsync(virksomhet.Id, "energiklagenemnda", "Kari Jurist"));
        Assert.Contains("allerede navneformen", feil.Message);

        // …men samme term mot en ANNEN virksomhet er en helt annen opplysning og skal gå gjennom.
        var annen = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-annen-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(annen);
        await db.SaveChangesAsync();
        Assert.NotNull(await register.OpprettVirksomhetsbegrepAsync(annen.Id, "Energiklagenemnda", "Kari Jurist"));
    }

    /// <summary>
    /// [Ny, 2026-09-09] Grunnen kunne før bare settes VED opprettelse — en navneform fra
    /// katalogimporten kunne derfor ikke merkes i etterkant uten å lage en dublett.
    /// </summary>
    [Fact]
    public async Task Setter_navneformgrunn_pa_eksisterende_navneform()
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-nemnd-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        var register = new VirksomhetsbegrepTjeneste(db);
        var navneform = await register.OpprettVirksomhetsbegrepAsync(virksomhet.Id, "Nemnda", "Kari Jurist");
        Assert.Null(navneform.Navneformgrunn);

        Assert.True(await register.SettNavneformgrunnAsync(navneform.Id, "gjeldende", "Kari Jurist"));
        Assert.Equal("gjeldende", (await db.Begreper.SingleAsync(b => b.Id == navneform.Id)).Navneformgrunn);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            register.SettNavneformgrunnAsync(navneform.Id, "tullball", "Kari Jurist"));
        Assert.False(await register.SettNavneformgrunnAsync(Guid.NewGuid(), "gjeldende", "Kari Jurist"));
    }

    /// <summary>
    /// [Ny, 2026-09-09] Taggene MÅ følge med: en tagg som peker på en navneform som ikke finnes lenger
    /// er en markering i lovteksten som ikke kan følges noe sted.
    /// </summary>
    [Fact]
    public async Task Sletter_navneform_og_taggene_som_peker_pa_den()
    {
        await using var db = _fixture.NyDbContext();
        var rettskildeId = await OpprettAlkohollovenAsync(db);
        var node = await db.RettskildeNoder.FirstAsync(n => n.RettskildeId == rettskildeId && n.Tekst != null);
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-nemnd-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();
        var register = new VirksomhetsbegrepTjeneste(db);
        var navneform = await register.OpprettVirksomhetsbegrepAsync(virksomhet.Id, node.Tekst![..5], "Kari Jurist");
        db.TekstTagger.Add(new TekstTaggEntitet
        {
            Id = Guid.NewGuid(), VirksomhetId = virksomhet.Id, RettskildeId = rettskildeId, NodeEid = node.Eid,
            StartOffset = 0, EndOffset = 5, QuotePrefix = "", QuoteExact = node.Tekst![..5], QuoteSuffix = "",
            NodeTekstHash = "hash", Kind = "virksomhet", RefId = navneform.Id,
            OpprettetAv = "Kari Jurist", OpprettetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        Assert.Equal(1, await register.SlettVirksomhetsbegrepAsync(navneform.Id, "Kari Jurist"));
        Assert.False(await db.Begreper.AnyAsync(b => b.Id == navneform.Id));
        Assert.False(await db.TekstTagger.AnyAsync(t => t.RefId == navneform.Id));
        Assert.Null(await register.SlettVirksomhetsbegrepAsync(navneform.Id, "Kari Jurist"));
    }

    [Fact]
    public async Task Oppretter_virksomhetsbegrep_uten_eiende_virksomhet()
    {
        await using var db = _fixture.NyDbContext();
        var mattilsynet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-Mattilsynet-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(mattilsynet);
        await db.SaveChangesAsync();

        var register = new VirksomhetsbegrepTjeneste(db);
        var begrep = await register.OpprettVirksomhetsbegrepAsync(mattilsynet.Id, "Mattilsynet", "Kari Jurist");

        Assert.Equal("virksomhet", begrep.Begrepskategori);
        Assert.Equal(mattilsynet.Id, begrep.VirksomhetReferanseId);
        Assert.Null(begrep.VirksomhetId); // delt/nasjonal referansedata — ingen eiende virksomhet (docs/20 §2.3).

        var alle = await register.AlleVirksomhetsbegrepForAsync(mattilsynet.Id);
        Assert.Single(alle);
    }

    /// <summary>
    /// [Ny, navneformgrunn-runden, 2026-09-07] Det lukkede vokabularet for
    /// <see cref="BegrepEntitet.Navneformgrunn"/>, håndhevet i TJENESTELAGET (CHECK-constrainten
    /// `ck_begreper_navneformgrunn` er andre forsvarslinje). NULL er bevisst gyldig — uspesifisert.
    /// Johanns tre konkrete eksempler er dekket eksplisitt: Arkivverket (`utgatt`), «Suldal» som
    /// kortform for Suldal kommune (`kortform`), «Matilsynet» med én t (`feilskriving`).
    /// </summary>
    [Theory]
    [InlineData("gjeldende")]
    [InlineData("utgatt")]
    [InlineData("kortform")]
    [InlineData("feilskriving")]
    [InlineData(null)]
    public async Task Godtar_hele_navneformgrunn_vokabularet_og_null(string? grunn)
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-Navneformgrunn-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var begrep = await new VirksomhetsbegrepTjeneste(db).OpprettVirksomhetsbegrepAsync(
            virksomhet.Id, NyTerm("Navneform"), "Kari Jurist", navneformgrunn: grunn);

        Assert.Equal(grunn, begrep.Navneformgrunn);

        // Faktisk PERSISTERT (ikke bare satt på det returnerte objektet) — CHECK-constrainten ville
        // ellers kunne avvise verdien ved lagring uten at testen merket det.
        await using var friskDb = _fixture.NyDbContext();
        Assert.Equal(grunn, (await friskDb.Begreper.SingleAsync(b => b.Id == begrep.Id)).Navneformgrunn);
    }

    [Theory]
    [InlineData("utdatert")]      // nær 'utgatt', men ikke i vokabularet.
    [InlineData("Gjeldende")]     // feil kasus — vokabularet er ordinalt, ikke case-insensitivt.
    [InlineData("")]              // tom streng normaliseres BEVISST ikke stille til NULL.
    [InlineData("kort form")]
    public async Task Avviser_navneformgrunn_utenfor_vokabularet(string grunn)
    {
        await using var db = _fixture.NyDbContext();
        var virksomhet = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-Ugyldiggrunn-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(virksomhet);
        await db.SaveChangesAsync();

        var register = new VirksomhetsbegrepTjeneste(db);
        var feil = await Assert.ThrowsAsync<ArgumentException>(() => register.OpprettVirksomhetsbegrepAsync(
            virksomhet.Id, NyTerm("Navneform"), "Kari Jurist", navneformgrunn: grunn));
        Assert.Contains("navneformgrunn", feil.Message, StringComparison.OrdinalIgnoreCase);

        // Ingen halvveis opprettet rad etter en avvist verdi.
        Assert.Empty(await register.AlleVirksomhetsbegrepForAsync(virksomhet.Id));
    }

    /// <summary>Grunnen er kun meningsfull for navneformer — et GRUPPEbegrep har ingen, og skal
    /// fortsatt kunne opprettes (feltet er nullbart, ikke påkrevd noe sted).</summary>
    [Fact]
    public async Task Gruppebegrep_har_ingen_navneformgrunn()
    {
        await using var db = _fixture.NyDbContext();
        var lovId = await OpprettAlkohollovenAsync(db);
        var gruppe = await new VirksomhetsbegrepTjeneste(db).OpprettGruppebegrepAsync(
            lovId, NyTerm("kontrollmyndighet"), "Kari Jurist");
        Assert.Null(gruppe.Navneformgrunn);
    }

    [Fact]
    public async Task Synonymer_er_bare_flere_rader_mot_samme_virksomhet()
    {
        await using var db = _fixture.NyDbContext();
        var statsforvalteren = new Virksomhet { Id = Guid.NewGuid(), Navn = $"Test-Statsforvalteren-{Guid.NewGuid():N}" };
        db.Virksomheter.Add(statsforvalteren);
        await db.SaveChangesAsync();

        var register = new VirksomhetsbegrepTjeneste(db);
        await register.OpprettVirksomhetsbegrepAsync(statsforvalteren.Id, "Statsforvalter", "Kari Jurist");
        await register.OpprettVirksomhetsbegrepAsync(statsforvalteren.Id, "Fylkesmann", "Kari Jurist");

        var alle = await register.AlleVirksomhetsbegrepForAsync(statsforvalteren.Id);
        Assert.Equal(2, alle.Count);
        Assert.Contains(alle, b => b.Term == "Statsforvalter");
        Assert.Contains(alle, b => b.Term == "Fylkesmann");
    }

    // Alkoholloven/forvaltningsloven importeres idempotent (samme ELI) og deler derfor SAMME rad på
    // tvers av alle tester i denne delte DataTestCollection-databasen — samme "unik streng per test"-
    // begrunnelse som NyKode() i KodelisteregisterTjenesteTests.
    private static string NyTerm(string prefiks) => $"{prefiks}-{Guid.NewGuid():N}";

    [Fact]
    public async Task Gruppebegrep_samme_term_i_samme_lov_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var lovkildeId = await OpprettAlkohollovenAsync(db);
        var term = NyTerm("kontrollmyndighet");

        var register = new VirksomhetsbegrepTjeneste(db);
        await register.OpprettGruppebegrepAsync(lovkildeId, term, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(
            () => register.OpprettGruppebegrepAsync(lovkildeId, term, "Kari Jurist"));
    }

    [Fact]
    public async Task Gruppebegrep_samme_term_i_ulik_lov_er_to_ulike_rader()
    {
        await using var db = _fixture.NyDbContext();
        var alkoholloven = await OpprettAlkohollovenAsync(db);
        var forvaltningsloven = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesForvaltningsloven(), new DateOnly(2026, 8, 22)));
        var term = NyTerm("tilsynsmyndighet");

        var register = new VirksomhetsbegrepTjeneste(db);
        var forsteRad = await register.OpprettGruppebegrepAsync(alkoholloven, term, "Kari Jurist");
        var andreRad = await register.OpprettGruppebegrepAsync(forvaltningsloven, term, "Kari Jurist");

        Assert.NotEqual(forsteRad.Id, andreRad.Id);
        Assert.Equal(alkoholloven, forsteRad.LovkildeId);
        Assert.Equal(forvaltningsloven, andreRad.LovkildeId);
    }

    // ---------- [Ny, issue #203 pkt. 2] Administrativ inndeling — samme (Term, LovkildeId)-scoping som
    // gruppebegrep over (besluttet med Johann 2026-09-10), egen Begrepskategori-verdi og egen metode
    // (OpprettAdministrativInndelingAsync) — se den metodens kommentar for hvorfor ikke slått sammen
    // med OpprettGruppebegrepAsync til én parameterisert metode. ----------

    [Fact]
    public async Task Administrativ_inndeling_far_riktig_begrepskategori_og_ingen_navneformgrunn()
    {
        await using var db = _fixture.NyDbContext();
        var lovId = await OpprettAlkohollovenAsync(db);
        var inndeling = await new VirksomhetsbegrepTjeneste(db).OpprettAdministrativInndelingAsync(
            lovId, NyTerm("Suldal kommune"), "Kari Jurist");

        Assert.Equal("administrativ_inndeling", inndeling.Begrepskategori);
        Assert.Equal(lovId, inndeling.LovkildeId);
        Assert.Null(inndeling.VirksomhetId);
        Assert.Null(inndeling.Navneformgrunn);
        Assert.Equal("publisert", inndeling.Status);
    }

    [Fact]
    public async Task Administrativ_inndeling_samme_term_i_samme_lov_kastes()
    {
        await using var db = _fixture.NyDbContext();
        var lovkildeId = await OpprettAlkohollovenAsync(db);
        var term = NyTerm("Hedmark fylke");

        var register = new VirksomhetsbegrepTjeneste(db);
        await register.OpprettAdministrativInndelingAsync(lovkildeId, term, "Kari Jurist");

        await Assert.ThrowsAsync<ArgumentException>(
            () => register.OpprettAdministrativInndelingAsync(lovkildeId, term, "Kari Jurist"));
    }

    [Fact]
    public async Task Administrativ_inndeling_samme_term_i_ulik_lov_er_to_ulike_rader()
    {
        await using var db = _fixture.NyDbContext();
        var alkoholloven = await OpprettAlkohollovenAsync(db);
        var forvaltningsloven = await new RettskildeImportTjeneste(db).ImporterAsync(
            LovdataKonverterer.Konverter(Testdata.LesForvaltningsloven(), new DateOnly(2026, 8, 22)));
        var term = NyTerm("Østfold fylke");

        var register = new VirksomhetsbegrepTjeneste(db);
        var forsteRad = await register.OpprettAdministrativInndelingAsync(alkoholloven, term, "Kari Jurist");
        var andreRad = await register.OpprettAdministrativInndelingAsync(forvaltningsloven, term, "Kari Jurist");

        Assert.NotEqual(forsteRad.Id, andreRad.Id);
        Assert.Equal(alkoholloven, forsteRad.LovkildeId);
        Assert.Equal(forvaltningsloven, andreRad.LovkildeId);
    }

    /// <summary>Samme term (Term, LovkildeId) er OK på tvers av de to ULIKE kategoriene — de to unike
    /// partielle indeksene (ux_begreper_gruppebegrep_term_lovkilde/ux_begreper_administrativ_inndeling_
    /// term_lovkilde) er hver filtrert på SIN EGEN Begrepskategori, se RegelIdeDbContext. Dekker
    /// samtidig regresjonen som oppsto da migrasjonen først ble generert (EF slo de to identiske
    /// (Term, LovkildeId)-HasIndex-kallene sammen til ÉN og mistet gruppebegrep-indeksen stille — se
    /// PR-beskrivelsen) — hadde den regresjonen ikke vært rettet, ville enten denne testen eller
    /// Gruppebegrep_samme_term_i_samme_lov_kastes feilet.</summary>
    [Fact]
    public async Task Samme_term_og_lov_er_ok_for_gruppe_og_administrativ_inndeling_samtidig()
    {
        await using var db = _fixture.NyDbContext();
        var lovkildeId = await OpprettAlkohollovenAsync(db);
        var term = NyTerm("delt-navn");

        var register = new VirksomhetsbegrepTjeneste(db);
        var gruppe = await register.OpprettGruppebegrepAsync(lovkildeId, term, "Kari Jurist");
        var inndeling = await register.OpprettAdministrativInndelingAsync(lovkildeId, term, "Kari Jurist");

        Assert.NotEqual(gruppe.Id, inndeling.Id);
        Assert.Equal("gruppe", gruppe.Begrepskategori);
        Assert.Equal("administrativ_inndeling", inndeling.Begrepskategori);
    }
}
