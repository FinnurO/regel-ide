using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>Oppsummering av én <see cref="LovdataFullimportTjeneste.KjorAsync"/>-kjøring.</summary>
public sealed record LovdataFullimportResultat(int Nye, int NyeVersjoner, int Uendret, int Feilet, int TotaltBehandlet)
{
    public override string ToString() =>
        $"{TotaltBehandlet} behandlet: {Nye} nye, {NyeVersjoner} nye versjoner, {Uendret} uendret, {Feilet} feilet.";
}

/// <summary>
/// Full Lovdata-synkronisering (docs/13-backlog.md §6 "Daglig Lovdata-synkronisering (full + delta)")
/// — henter og importerer ALLE gjeldende lover og sentrale forskrifter fra Lovdatas offisielle
/// bulk-datasett, ikke bare den ene rettskilden brukeren eksplisitt velger via
/// <c>POST /api/rettskilder/lovdata</c>. Kalt fra <c>RegelIde.Api</c> ved oppstart (se
/// LovdataFullimportBakgrunnstjeneste), men er selv fri for ASP.NET-avhengigheter — testbar/kjørbar
/// helt uavhengig av verten.
///
/// "Delta-analysen" brukeren ba om er allerede <see cref="RettskildeImportTjeneste.ImporterMedUtfallAsync"/>
/// sin eksisterende idempotens-logikk (bit-identisk AKN → <see cref="RettskildeImportUtfall.Uendret"/>,
/// ingen ny rad) — denne tjenesten legger bare til: (1) å iterere ALLE dokumenter i stedet for ett,
/// og (2) å telle/rapportere utfallet på tvers av dem. Lovdata har ingen gratis
/// <c>documentHistory</c>-endepunkt å avgrense mot (krever egen API-nøkkel, se
/// docs/05-arkitektur-og-nfk.md §1.1) — hvert kjøring må derfor faktisk konvertere og sammenligne
/// AKN for alle dokumenter, det finnes ingen snarvei via et "endret siden sist"-tidsstempel i dag.
/// </summary>
public sealed class LovdataFullimportTjeneste(
    LovdataBulkHenter bulkHenter, RettskildeImportTjeneste importer, RegelIdeDbContext db,
    LovdataImportstatusTjeneste importstatusTjeneste, ILogger<LovdataFullimportTjeneste>? logger = null)
{
    private readonly ILogger<LovdataFullimportTjeneste> _logger = logger ?? NullLogger<LovdataFullimportTjeneste>.Instance;

    // Placeholder inntil ekte autentisering/brukerkontekst finnes i systemet — samme mønster som
    // RettskildeImportTjeneste.SystemBruker, men eget navn slik at proveniensen skiller en
    // brukerutløst enkeltimport fra denne automatiske bakgrunnsjobben.
    public const string SystemBruker = "system-lovdata-fullimport";

    /// <summary>
    /// Kjører én full runde: henter alle oppføringer fra begge bulk-arkiv, konverterer hver til AKN,
    /// og importerer/delta-sjekker via <see cref="RettskildeImportTjeneste"/>. Alltid delt/nasjonalt
    /// (virksomhetId=null) — bulk-arkivet inneholder per definisjon kun nasjonale Lov/Forskrift,
    /// samme begrunnelse som det eksisterende <c>ImporterFraLovdata</c>-endepunktet.
    /// En feil på ETT dokument (uventet HTML-avvik parseren ikke kjenner, §3.3) stopper ikke resten
    /// av runden — logges og telles som «feilet», ingen gjettet fallback for det enkelte dokumentet.
    /// </summary>
    public async Task<LovdataFullimportResultat> KjorAsync(CancellationToken ct = default)
    {
        int nye = 0, nyeVersjoner = 0, uendret = 0, feilet = 0, totalt = 0;

        await foreach (var (datokode, type, html) in bulkHenter.HentAlleDokumenterAsync(ct))
        {
            totalt++;

            // Avledet fra datokoden ALENE (LovdataIdentifikatorer.AvledEliFraDatokode), ikke fra den
            // strukturelle AKN-parsingen under — derfor alltid tilgjengelig, selv når selve importen
            // feiler. Datokoden kommer fra vårt EGET regex-treff på arkivfilnavnet (LovdataBulkHenter),
            // så den er alltid velformet — denne skal aldri kunne kaste i praksis.
            var eli = LovdataIdentifikatorer.AvledEliFraDatokode(datokode, out _);
            var tittel = LovdataBulkHenter.LesTittelBesteForsok(html);

            try
            {
                var konvertert = LovdataKonverterer.Konverter(html);
                var resultat = await importer.ImporterMedUtfallAsync(konvertert, virksomhetId: null, SystemBruker, ct);
                switch (resultat.Utfall)
                {
                    case RettskildeImportUtfall.Ny or RettskildeImportUtfall.ForfremmetStub:
                        nye++;
                        break;
                    case RettskildeImportUtfall.NyVersjon:
                        nyeVersjoner++;
                        _logger.LogInformation("Lovdata-fullimport: ny versjon av {Datokode}.", datokode);
                        break;
                    case RettskildeImportUtfall.Uendret:
                        uendret++;
                        break;
                }

                await importstatusTjeneste.OppdaterAsync(datokode, type, tittel, eli, importert: true, resultat.RettskildeId, feilmelding: null, ct);
            }
            catch (OperationCanceledException)
            {
                throw; // avslutning (f.eks. app-shutdown) — ikke en dokumentfeil, skal ikke telles/svelges
            }
            catch (Exception ex)
            {
                // Bevisst FANGET BREDT (ikke bare FormatException/NotSupportedException fra §3.3):
                // en runde over ALLE dokumenter i det ekte, levende Lovdata-korpuset MÅ tåle uventede
                // avvik verken parseren eller RettskildeImportTjeneste er skrevet for (f.eks. et reelt
                // funn under bygging av denne tjenesten: to noder med samme eId i LOV-1999-12-17-95
                // kastet ArgumentException dypt inne i SettInnNoderOgReferanserAsync, IKKE en
                // FormatException) — ett uventet dokument skal aldri stoppe de tusen andre.
                feilet++;
                _logger.LogWarning(ex, "Lovdata-fullimport: kunne ikke importere {Datokode}, hoppet over.", datokode);

                // KRITISK: entiteter som ble db.Add()-et før krasjet (RettskildeEntitet/-Node/-Referanse
                // for DETTE dokumentet) sitter fortsatt i endringssporeren i "Added"-tilstand siden
                // SaveChangesAsync aldri ble kalt. Uten denne rydder de ville blitt forsøkt lagret
                // sammen med — og korrumpert — det NESTE dokumentets SaveChangesAsync-kall, siden
                // samme DbContext gjenbrukes for hele runden (av ytelsesgrunner, ikke én kontekst per
                // dokument). Trygt å nullstille helt: ingenting annet er bevisst i endring her mellom
                // dokumenter i denne løkken.
                db.ChangeTracker.Clear();

                // Flagget brukeren ba om ("den er ikke importert") — med url/eId og feilmelding, slik
                // at man kan SE hvilke dokumenter som ikke lar seg parse og ta dem case-by-case
                // (docs/13-backlog.md §6). Egen try/catch: denne bekvemmeligheten skal aldri selv
                // kunne velte runden videre dersom NOE uventet skjer med den også.
                try
                {
                    await importstatusTjeneste.OppdaterAsync(datokode, type, tittel, eli, importert: false, rettskildeId: null, ex.Message, ct);
                }
                catch (Exception statusEx) when (statusEx is not OperationCanceledException)
                {
                    _logger.LogWarning(statusEx, "Lovdata-fullimport: kunne ikke lagre importstatus for {Datokode}.", datokode);
                    db.ChangeTracker.Clear();
                }
            }

            if (totalt % 250 == 0)
            {
                _logger.LogInformation(
                    "Lovdata-fullimport: {Totalt} behandlet så langt ({Nye} nye, {NyeVersjoner} nye versjoner, {Uendret} uendret, {Feilet} feilet).",
                    totalt, nye, nyeVersjoner, uendret, feilet);
            }
        }

        return new LovdataFullimportResultat(nye, nyeVersjoner, uendret, feilet, totalt);
    }
}
