using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// Bokføring av kjøre-historikk (administrasjon-Lovdata-resynk, GitHub-issue #104) — mot ekte embedded
/// Postgres, men UTEN noe ekte Lovdata-nettverkskall: selve "arbeidet" i FullforKjoringAsync/
/// KjorOgRegistrerAsync er her alltid en enkel, lokal lambda (se <see cref="LovdataResynkKjoringTjeneste"/>s
/// klassekommentar for hvorfor det er mulig).
/// <para>
/// <c>lovdata_resynk_kjoringer</c> er, med vilje, en GLOBAL tabell uten noen virksomhet-/Guid-nøkkel å
/// filtrere per test på (<see cref="LovdataResynkKjoringTjeneste.ErKjoringPagaendeAsync"/>/
/// <see cref="LovdataResynkKjoringTjeneste.SisteFerdigeKjoringAsync"/> spør bevisst HELE tabellen — det
/// er selve poenget: aldri to samtidige kjøringer, uansett hvem som trigget dem). Delt embedded Postgres
/// for HELE assemblyen (EmbeddedPostgresFixture, ikke nullstilt mellom tester) gjør derfor at hver test
/// her MÅ tømme tabellen selv FØR den setter opp sitt eget scenario — se <see cref="RyddAsync"/> —
/// ellers ville rekkefølgen andre tester (i denne eller andre testklasser i samme collection) kjørte i
/// gjort disse testene ikke-deterministiske.
/// </para>
/// </summary>
[Collection(DataTestCollection.Navn)]
public class LovdataResynkKjoringTjenesteTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public LovdataResynkKjoringTjenesteTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static Task RyddAsync(RegelIdeDbContext db) =>
        db.Database.ExecuteSqlRawAsync("DELETE FROM lovdata_resynk_kjoringer;");

    private static Task<LovdataFullimportResultat> FastResultat(CancellationToken _) =>
        Task.FromResult(new LovdataFullimportResultat(Nye: 3, NyeVersjoner: 2, Uendret: 100, Feilet: 1, TotaltBehandlet: 106));

    [Fact]
    public async Task StartKjoringAsync_oppretter_en_pagaende_rad()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);
        var tjeneste = new LovdataResynkKjoringTjeneste(db);

        var forTidspunkt = DateTimeOffset.UtcNow;
        var kjoringId = await tjeneste.StartKjoringAsync(LovdataResynkUtlost.Manuell, "Kari Saksbehandler");

        var rad = await db.LovdataResynkKjoringer.SingleAsync(k => k.Id == kjoringId);
        Assert.Equal(LovdataResynkStatus.Pagar, rad.Status);
        Assert.Equal(LovdataResynkUtlost.Manuell, rad.Utlost);
        Assert.Equal("Kari Saksbehandler", rad.UtlostAvBruker);
        Assert.Null(rad.FullfortTidspunkt);
        Assert.Null(rad.TotaltBehandlet);
        Assert.True(rad.StartetTidspunkt >= forTidspunkt);
    }

    [Fact]
    public async Task ErKjoringPagaendeAsync_er_sann_mens_kjoringen_pagar_og_usann_etter_fullfort()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);
        var tjeneste = new LovdataResynkKjoringTjeneste(db);

        var kjoringId = await tjeneste.StartKjoringAsync(LovdataResynkUtlost.Oppstart, utlostAvBruker: null);
        Assert.True(await tjeneste.ErKjoringPagaendeAsync());

        await tjeneste.FullforKjoringAsync(kjoringId, FastResultat);
        Assert.False(await tjeneste.ErKjoringPagaendeAsync());
    }

    [Fact]
    public async Task FullforKjoringAsync_ved_suksess_registrerer_fullfort_og_tellerne()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);
        var tjeneste = new LovdataResynkKjoringTjeneste(db);

        var kjoringId = await tjeneste.StartKjoringAsync(LovdataResynkUtlost.Planlagt, utlostAvBruker: null);
        var resultat = await tjeneste.FullforKjoringAsync(kjoringId, FastResultat);

        Assert.Equal(106, resultat.TotaltBehandlet);

        var rad = await db.LovdataResynkKjoringer.SingleAsync(k => k.Id == kjoringId);
        Assert.Equal(LovdataResynkStatus.Fullfort, rad.Status);
        Assert.NotNull(rad.FullfortTidspunkt);
        Assert.Equal(3, rad.Nye);
        Assert.Equal(2, rad.NyeVersjoner);
        Assert.Equal(100, rad.Uendret);
        Assert.Equal(1, rad.Feilet);
        Assert.Equal(106, rad.TotaltBehandlet);
        Assert.Null(rad.Feilmelding);
    }

    [Fact]
    public async Task FullforKjoringAsync_ved_feil_registrerer_feilet_og_kaster_videre()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);
        var tjeneste = new LovdataResynkKjoringTjeneste(db);

        var kjoringId = await tjeneste.StartKjoringAsync(LovdataResynkUtlost.Manuell, "Kari Saksbehandler");

        var unntak = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tjeneste.FullforKjoringAsync(kjoringId, _ => throw new InvalidOperationException("Lovdata utilgjengelig")));
        Assert.Equal("Lovdata utilgjengelig", unntak.Message);

        var rad = await db.LovdataResynkKjoringer.SingleAsync(k => k.Id == kjoringId);
        Assert.Equal(LovdataResynkStatus.Feilet, rad.Status);
        Assert.Equal("Lovdata utilgjengelig", rad.Feilmelding);
        Assert.NotNull(rad.FullfortTidspunkt);
        Assert.Null(rad.TotaltBehandlet); // aldri kommet i mål -- tellerne forblir null, ikke gjettet 0
    }

    [Fact]
    public async Task FullforKjoringAsync_ved_kansellering_registrerer_avbrutt_melding()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);
        var tjeneste = new LovdataResynkKjoringTjeneste(db);

        var kjoringId = await tjeneste.StartKjoringAsync(LovdataResynkUtlost.Oppstart, utlostAvBruker: null);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            tjeneste.FullforKjoringAsync(kjoringId, _ => throw new OperationCanceledException()));

        var rad = await db.LovdataResynkKjoringer.SingleAsync(k => k.Id == kjoringId);
        Assert.Equal(LovdataResynkStatus.Feilet, rad.Status);
        Assert.Equal("Avbrutt (app-avslutning).", rad.Feilmelding);
    }

    [Fact]
    public async Task KjorOgRegistrerAsync_starter_og_fullforer_i_ett_kall()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);
        var tjeneste = new LovdataResynkKjoringTjeneste(db);

        var resultat = await tjeneste.KjorOgRegistrerAsync(LovdataResynkUtlost.Oppstart, utlostAvBruker: null, FastResultat);
        Assert.Equal(106, resultat.TotaltBehandlet);

        var rad = await db.LovdataResynkKjoringer.SingleAsync(k => k.Utlost == LovdataResynkUtlost.Oppstart);
        Assert.Equal(LovdataResynkStatus.Fullfort, rad.Status);
    }

    [Fact]
    public async Task SisteFerdigeKjoringAsync_ekskluderer_pagaende_og_returnerer_nyeste_ferdige()
    {
        await using var db = _fixture.NyDbContext();
        await RyddAsync(db);
        var tjeneste = new LovdataResynkKjoringTjeneste(db);

        var forste = await tjeneste.StartKjoringAsync(LovdataResynkUtlost.Oppstart, utlostAvBruker: null);
        await tjeneste.FullforKjoringAsync(forste, FastResultat);

        await Task.Delay(10); // sikrer et faktisk senere StartetTidspunkt enn "forste" over

        var andre = await tjeneste.StartKjoringAsync(LovdataResynkUtlost.Planlagt, utlostAvBruker: null);
        await tjeneste.FullforKjoringAsync(andre, FastResultat);

        // En TREDJE, fortsatt pågående kjøring skal IKKE returneres selv om den (hypotetisk) hadde et
        // senere StartetTidspunkt enn begge de ferdige -- kun ferdige kjøringer er "siste kjente utfall".
        await tjeneste.StartKjoringAsync(LovdataResynkUtlost.Manuell, "Kari Saksbehandler");

        var siste = await tjeneste.SisteFerdigeKjoringAsync();
        Assert.NotNull(siste);
        Assert.Equal(andre, siste!.Id);
    }
}
