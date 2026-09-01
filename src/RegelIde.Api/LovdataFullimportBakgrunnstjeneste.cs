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

            // [Ny, kodegjennomgang 2026-08-30] Navnekandidat-fiks 3 — verken enkelt-import eller denne
            // fullimport-jobben trigget FØR NÅ noensinne NavnekandidatOppdagelseTjeneste.SveipAsync selv;
            // POST /api/navnekandidater/sveip måtte kalles eksplisitt. Bekreftet i praksis: et manuelt
            // sveip av kommuneloven (allerede importert i god tid) ga 301 helt NYE treff — beviset er at
            // sveipet aldri hadde kjørt før, ikke at mønstrene ikke fanger noe der. Kjøres derfor her,
            // ETTER at selve fullimporten er ferdig og committet, over HELE korpuset (rettskildeId=null)
            // — idempotent (se NavnekandidatOppdagelseTjenesteTests), trygt å kjøre på nytt selv om noen
            // rettskilder allerede er sveipet fra før. Egen try/catch: et sveip som feiler skal aldri få
            // det til å SE UT som om selve fullimporten (som allerede er committet over) feilet.
            try
            {
                var navnekandidatRegister = scope.ServiceProvider.GetRequiredService<NavnekandidatOppdagelseTjeneste>();
                var sveipResultat = await navnekandidatRegister.SveipAsync(
                    rettskildeId: null, LovdataFullimportTjeneste.SystemBruker, stoppingToken);
                logger.LogInformation(
                    "Navnekandidat-sveip etter fullimport fullført: {Funnet} treff totalt, {Nye} nye kandidater.",
                    sveipResultat.AntallTreffFunnet, sveipResultat.AntallNyeKandidater);
            }
            catch (OperationCanceledException)
            {
                // Normal avslutning ved app-shutdown midt i sveipet — ikke en feil.
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Navnekandidat-sveip etter fullimport feilet uventet.");
            }

            // [Ny, 2026-08-30] Samme begrunnelse som navnekandidat-sveipet over: selve fullimporten
            // setter AnsvarligDepartement direkte for radene den (opp)oppretter/oppdaterer, men denne
            // idempotente tilbakefyllingen dekker likevel ev. rader som av andre grunner endte opp
            // NULL — se AnsvarligDepartementBackfillTjeneste.cs. Egen try/catch av samme grunn: en
            // feilet tilbakefylling skal aldri se ut som at selve fullimporten (allerede committet) feilet.
            try
            {
                var dbForBackfill = scope.ServiceProvider.GetRequiredService<RegelIdeDbContext>();
                var backfillAntall = await AnsvarligDepartementBackfillTjeneste.KjorAsync(dbForBackfill, stoppingToken);
                logger.LogInformation(
                    "AnsvarligDepartement-tilbakefylling etter fullimport fullført: {Antall} rettskilder oppdatert.",
                    backfillAntall);
            }
            catch (OperationCanceledException)
            {
                // Normal avslutning ved app-shutdown midt i tilbakefyllingen — ikke en feil.
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AnsvarligDepartement-tilbakefylling etter fullimport feilet uventet.");
            }
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
