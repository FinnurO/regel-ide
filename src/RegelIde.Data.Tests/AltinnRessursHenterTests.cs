using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data.Tests;

/// <summary>
/// docs/13-backlog.md — Altinn ressursregister-høsteren (rått høstelag, se <see cref="EksternKildeEntitet"/>).
/// Samme stub-<see cref="HttpMessageHandler"/>-prinsipp som <see cref="OppgaveregisterHenterTests"/> — ingen
/// ekte nettverkskall i test-suiten. Fixturedataene (<see cref="Testdata.LesAltinnRessursliste"/>) er TRE
/// EKTE ressurser fra det offentlige API-et (tjenesteoversikten.no): to <c>AltinnApp</c> og én
/// <c>MaskinportenSchema</c> — valgt nettopp for å bevise at <see cref="AltinnRessursHenter"/>s filter
/// faktisk ekskluderer den siste.
/// </summary>
[Collection(DataTestCollection.Navn)]
public class AltinnRessursHenterTests
{
    private readonly EmbeddedPostgresFixture _fixture;

    public AltinnRessursHenterTests(EmbeddedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class StubHandler(HttpStatusCode status, string responsBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(responsBody, Encoding.UTF8, "application/json") });
    }

    private static AltinnRessursHenter LagHenter(RegelIdeDbContext db, string responsJson) =>
        new(new HttpClient(new StubHandler(HttpStatusCode.OK, responsJson)), db);

    private static readonly string TreRessurserJson = Testdata.LesAltinnRessursliste();

    /// <summary>Identisk med <see cref="TreRessurserJson"/> bortsett fra brg-ressursens norske beskrivelse — brukes til å teste at re-høsting KUN oppdaterer den ene endrede raden.</summary>
    private static readonly string TreRessurserEndretJson = TreRessurserJson.Replace(
        "Innsending av årsregnskap til Regnskapsregisteret", "Innsending av ENDRET årsregnskap til Regnskapsregisteret");

    [Fact]
    public async Task Forste_hosting_oppretter_kun_altinnapp_ressursene_og_filtrerer_bort_andre_typer()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        var resultat = await LagHenter(db, TreRessurserJson).HentAlleRessurserAsync();

        Assert.Equal(2, resultat.Nye);
        Assert.Equal(0, resultat.Oppdaterte);
        Assert.Equal(0, resultat.Uendret);

        var rader = await db.EksterneKilder.Where(k => k.Kildetype == AltinnRessursHenter.Kildetype).ToListAsync();
        Assert.Equal(2, rader.Count);
        Assert.Contains(rader, r => r.EksternId == "app_brg_aarsregnskap-bank-202404");
        Assert.Contains(rader, r => r.EksternId == "app_dsb_farligstoff");
        Assert.DoesNotContain(rader, r => r.EksternId == "72735971-43c2-4eb1-9ff1-e2dcb3fdf4fd"); // MaskinportenSchema — ekskludert av filteret
        Assert.All(rader, r => Assert.Contains("\"resourceType\": \"AltinnApp\"", r.RaaJson));
        Assert.All(rader, r => Assert.False(string.IsNullOrWhiteSpace(r.InnholdsHash)));
    }

    [Fact]
    public async Task Uendret_gjenhosting_er_en_no_op()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        await LagHenter(db, TreRessurserJson).HentAlleRessurserAsync();
        var forHentetTidspunkter = await db.EksterneKilder
            .Where(k => k.Kildetype == AltinnRessursHenter.Kildetype)
            .ToDictionaryAsync(k => k.EksternId, k => k.HentetTidspunkt);

        var resultat = await LagHenter(db, TreRessurserJson).HentAlleRessurserAsync();

        Assert.Equal(0, resultat.Nye);
        Assert.Equal(0, resultat.Oppdaterte);
        Assert.Equal(2, resultat.Uendret);

        var antall = await db.EksterneKilder.CountAsync(k => k.Kildetype == AltinnRessursHenter.Kildetype);
        Assert.Equal(2, antall); // ingen duplikater ved re-høsting

        var etterHentetTidspunkter = await db.EksterneKilder
            .Where(k => k.Kildetype == AltinnRessursHenter.Kildetype)
            .ToDictionaryAsync(k => k.EksternId, k => k.HentetTidspunkt);
        Assert.Equal(forHentetTidspunkter, etterHentetTidspunkter); // uendret hash ⇒ HentetTidspunkt IKKE bumpet
    }

    [Fact]
    public async Task Endret_felt_pa_en_ressurs_oppdaterer_kun_den_raden()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        await LagHenter(db, TreRessurserJson).HentAlleRessurserAsync();
        // AsNoTracking (se OppgaveregisterHenterTests for full begrunnelse): unngår at "før"-øyeblikksbildet
        // deler identisk objektreferanse med raden andre HentAlleRessurserAsync-kallet muterer.
        var forBrg = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == AltinnRessursHenter.Kildetype && k.EksternId == "app_brg_aarsregnskap-bank-202404");
        var forDsb = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == AltinnRessursHenter.Kildetype && k.EksternId == "app_dsb_farligstoff");

        var resultat = await LagHenter(db, TreRessurserEndretJson).HentAlleRessurserAsync();

        Assert.Equal(0, resultat.Nye);
        Assert.Equal(1, resultat.Oppdaterte);
        Assert.Equal(1, resultat.Uendret);

        var etterBrg = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == AltinnRessursHenter.Kildetype && k.EksternId == "app_brg_aarsregnskap-bank-202404");
        Assert.Contains("ENDRET", etterBrg.RaaJson);
        Assert.NotEqual(forBrg.InnholdsHash, etterBrg.InnholdsHash);
        Assert.True(etterBrg.HentetTidspunkt > forBrg.HentetTidspunkt);

        var etterDsb = await db.EksterneKilder.AsNoTracking().SingleAsync(k => k.Kildetype == AltinnRessursHenter.Kildetype && k.EksternId == "app_dsb_farligstoff");
        Assert.Equal(forDsb.InnholdsHash, etterDsb.InnholdsHash);
        Assert.Equal(forDsb.HentetTidspunkt, etterDsb.HentetTidspunkt);
    }

    [Fact]
    public async Task Unik_indeks_hindrer_duplikat_pa_kildetype_og_ekstern_id()
    {
        await using var db = _fixture.NyDbContext();
        await db.EksterneKilder.ExecuteDeleteAsync();

        db.EksterneKilder.Add(new EksternKildeEntitet
        {
            Id = Guid.NewGuid(), Kildetype = AltinnRessursHenter.Kildetype, EksternId = "DUPLIKAT-TEST",
            RaaJson = "{}", InnholdsHash = "a", HentetTidspunkt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        db.EksterneKilder.Add(new EksternKildeEntitet
        {
            Id = Guid.NewGuid(), Kildetype = AltinnRessursHenter.Kildetype, EksternId = "DUPLIKAT-TEST",
            RaaJson = "{}", InnholdsHash = "b", HentetTidspunkt = DateTimeOffset.UtcNow,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
