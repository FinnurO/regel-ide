using RegelIde.Data;

namespace RegelIde.Api;

/// <summary>
/// Kjører <see cref="LovdataFullimportTjeneste"/> (alle lover + sentrale forskrifter fra Lovdatas
/// bulk-arkiv, docs/13-backlog.md §6) i bakgrunnen ved oppstart. En <see cref="BackgroundService"/>
/// (ikke et synkront steg i Program.cs' egen oppstartsblokk) fordi en full runde kan ta flere
/// minutter (tusenvis av dokumenter, ingen Lovdata-API-nøkkel å avgrense mot et
/// "endret siden sist"-tidsstempel) — appen skal svare på helsesjekk/requests umiddelbart, ikke stå
/// og vente på at hele Lovdata-korpuset er gjennomgått.
///
/// Gated bak <c>RegelIde:LovdataFullimport:AktivVedOppstart</c> (default PÅ) slik at tester og lokal
/// rask iterasjon kan slå den av uten kodeendring — se <c>RegelIde__LovdataFullimport__AktivVedOppstart</c>
/// i EmbeddedPostgresApiFixture (samme miljøvariabel-mønster som tvinger IKiAgentKlient til Stub der).
/// </summary>
public sealed class LovdataFullimportBakgrunnstjeneste(
    IServiceScopeFactory scopeFactory, IConfiguration konfig, ILogger<LovdataFullimportBakgrunnstjeneste> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!konfig.GetValue("RegelIde:LovdataFullimport:AktivVedOppstart", defaultValue: true))
        {
            logger.LogInformation(
                "Lovdata-fullimport ved oppstart er avslått (RegelIde:LovdataFullimport:AktivVedOppstart=false).");
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var tjeneste = scope.ServiceProvider.GetRequiredService<LovdataFullimportTjeneste>();
            logger.LogInformation("Lovdata-fullimport starter i bakgrunnen (alle lover + sentrale forskrifter)...");
            var resultat = await tjeneste.KjorAsync(stoppingToken);
            logger.LogInformation("Lovdata-fullimport fullført: {Resultat}", resultat);
        }
        catch (OperationCanceledException)
        {
            // Normal avslutning ved app-shutdown midt i en runde — ikke en feil.
        }
        catch (Exception ex)
        {
            // Skal ALDRI velte hele appen — dette er en bakgrunns-bekvemmelighet, ikke en kritisk
            // oppstartsavhengighet (i motsetning til migrasjon/skjemaoppsett i Program.cs).
            logger.LogError(ex, "Lovdata-fullimport feilet uventet.");
        }
    }
}
