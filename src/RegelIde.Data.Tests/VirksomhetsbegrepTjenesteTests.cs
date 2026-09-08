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
}
