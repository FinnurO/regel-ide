using RegelIde.Data;

namespace RegelIde.Api;

/// <summary>
/// Periodisk BackgroundService (administrasjon-Lovdata-resynk, GitHub-issue #104) — sjekker HVER TIME
/// om det er på tide å kjøre en planlagt Lovdata-resynk, basert på den database-lagrede
/// <see cref="LovdataResynkInnstillingEntitet"/> (endres fra administrasjonssiden, ingen redeploy
/// nødvendig) og tidspunktet for siste ferdige kjøring — se <see cref="LovdataResynkPlanleggerTjeneste"/>
/// for selve avgjørelsen og orkestreringen; denne klassens eneste jobb er "vekk hver time, kall den,
/// sov videre" (samme "tynn hosting-glue, all ekte logikk i RegelIde.Data"-arbeidsdeling som
/// <see cref="LovdataFullimportBakgrunnstjeneste"/>/<see cref="LovdataFullimportTjeneste"/>).
/// <para>
/// Timen mellom SJEKKENE er IKKE brukerens lagrede intervall (som kan være dager) — den er kun hvor ofte
/// vi ser etter om intervallet har gått ut, hyppig nok til at en daglig/ukentlig innstilling faktisk
/// trigges nær riktig tidspunkt uten å polle databasen unødig ofte.
/// </para>
/// <para>
/// Gated bak <c>RegelIde:LovdataFullimport:PlanlagtResynkAktiv</c> (default PÅ), samme mønster som
/// <see cref="LovdataFullimportBakgrunnstjeneste"/>s <c>AktivVedOppstart</c>-flagg — se
/// <c>RegelIde__LovdataFullimport__PlanlagtResynkAktiv</c> i EmbeddedPostgresApiFixture for hvorfor
/// dette MÅ være avslått i testsuiten (ellers ville hver API-test som kjører lenge nok risikere en ekte,
/// utilsiktet Lovdata-fullimport i bakgrunnen).
/// </para>
/// </summary>
public sealed class LovdataResynkPlanleggerBakgrunnstjeneste(
    IServiceScopeFactory scopeFactory, IConfiguration konfig, TimeProvider klokke,
    ILogger<LovdataResynkPlanleggerBakgrunnstjeneste> logger)
    : BackgroundService
{
    private static readonly TimeSpan SjekkIntervall = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!konfig.GetValue("RegelIde:LovdataFullimport:PlanlagtResynkAktiv", defaultValue: true))
        {
            logger.LogInformation(
                "Planlagt Lovdata-resynk-sjekk er avslått (RegelIde:LovdataFullimport:PlanlagtResynkAktiv=false).");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var planlegger = scope.ServiceProvider.GetRequiredService<LovdataResynkPlanleggerTjeneste>();
                var fullimport = scope.ServiceProvider.GetRequiredService<LovdataFullimportTjeneste>();

                var startet = await planlegger.KjorHvisPaaTideAsync(fullimport.KjorAsync, stoppingToken);
                if (startet)
                {
                    logger.LogInformation("Planlagt Lovdata-resynk fullført (trigget av den periodiske sjekken).");
                }
            }
            catch (OperationCanceledException)
            {
                break; // normal avslutning ved app-shutdown -- ikke en feil
            }
            catch (Exception ex)
            {
                // Skal ALDRI velte hele appen -- samme begrunnelse som LovdataFullimportBakgrunnstjeneste.
                // Selve kjøringens feil er allerede registrert i historikken av LovdataResynkKjoringTjeneste;
                // denne logges i tillegg fordi den kan skyldes noe FØR/RUNDT selve kjøringen (f.eks. et
                // uventet unntak i innstilling/siste-kjøring-oppslaget).
                logger.LogError(ex, "Planlagt Lovdata-resynk-sjekk feilet uventet.");
            }

            try
            {
                await Task.Delay(SjekkIntervall, klokke, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
