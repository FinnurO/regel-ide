using RegelIde.Data;

namespace RegelIde.Api;

/// <summary>
/// [Ny, registernavn-runden, 2026-09-08] Kjører <see cref="VirksomhetRegisternavnSynkTjeneste"/> i
/// bakgrunnen ved oppstart: setter <see cref="Virksomhet.Navn"/> til Brregs egen form for alle rader i
/// katalogfila, og oppretter navneformene visningen bruker (SSR for kommunene, SNL / en godkjent liste
/// for resten).
///
/// <para>
/// En <see cref="BackgroundService"/> og IKKE et steg i <c>Program.cs</c>' egen oppstartsblokk, av
/// samme grunn som <see cref="LovdataFullimportBakgrunnstjeneste"/> oppgir: første kjøring gjør inntil
/// 447 Brreg-oppslag pluss inntil 402 SSR/SNL-oppslag. Lagt inline ville det lagt minutter på
/// oppstarten før appen svarte på noe som helst. Johann valgte eksplisitt bakgrunnsjobb
/// (2026-09-08): «appen er brukbar med én gang, og navnene faller på plass etter hvert».
/// </para>
///
/// <para>
/// <b>Ikke gated bak et konfigflagg</b>, til forskjell fra Lovdata-fullimporten. Den er dyr OG
/// gjentakende (hele Lovdata-korpuset, hver restart, derav issue #132); denne er dyr KUN første gang:
/// andre kjøring gjør null skrivinger og null HTTP-kall, siden radene alt har
/// <see cref="Virksomhet.SistBrregSynkronisert"/> satt og SSR/SNL-svarene ligger i
/// <c>ekstern_navneoppslag_cache</c>. En vanlig restart koster derfor ingenting, og da er et flagg som
/// må huskes verre enn ingen flagg — navnene skal bare være riktige.
/// </para>
///
/// <para>
/// <b>Kjører ETTER oppstartsblokkens seeds.</b> En <see cref="BackgroundService"/> startes av verten
/// etter at toppnivå-koden i <c>Program.cs</c> (inkludert hele seed-blokken) er ferdig, så
/// <see cref="OrganisasjonsregisterSeed"/> har alltid opprettet radene før denne leter dem opp. Synken
/// oppretter bevisst ALDRI en manglende rad selv — finner den ikke virksomheten, hopper den over den:
/// katalogseeding er seedens ansvar, ikke synkens.
/// </para>
/// </summary>
public sealed class VirksomhetRegisternavnSynkBakgrunnstjeneste(
    IServiceScopeFactory scopeFactory, ILogger<VirksomhetRegisternavnSynkBakgrunnstjeneste> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var tjeneste = scope.ServiceProvider.GetRequiredService<VirksomhetRegisternavnSynkTjeneste>();

            logger.LogInformation(
                "Registernavn-synk starter i bakgrunnen (Brreg-navn + navneformer fra SSR/SNL)...");
            var r = await tjeneste.KjorAsync(tvingBrregOppslag: false, stoppingToken);

            logger.LogInformation(
                "Registernavn-synk fullført: {Navn} navn satt til registerform, {Navneformer} navneformer "
                + "opprettet, {Uten} rader uten navneform, {Slettet} rader slettet, {Brreg} Brreg-oppslag.",
                r.RettedeNavn.Count, r.OpprettedeNavneformer.Count, r.UtenNavneform.Count,
                r.SlettedeRader.Count, r.BrregOppslag);

            // Hver enkelt endring logges, ikke bare antallet — samme etterprøvbarhetskrav som de
            // øvrige tilbakefyllingene i Program.cs holder seg til.
            foreach (var n in r.RettedeNavn)
            {
                logger.LogInformation(
                    "Registernavn {VirksomhetId}: «{Fra}» → «{Til}».", n.VirksomhetId, n.Fra, n.Til);
            }
            foreach (var nf in r.OpprettedeNavneformer)
            {
                logger.LogInformation(
                    "Navneform opprettet for {VirksomhetId}: «{Term}» ({Grunn}, kilde: {Kilde}).",
                    nf.VirksomhetId, nf.Term, nf.Grunn, nf.Kilde);
            }

            // Sletting logges som Warning, ikke Information: det er den ene destruktive handlingen i
            // hele jobben (issue #157 «ingen stille destruksjon»), og den skal være lett å finne igjen.
            foreach (var s in r.SlettedeRader)
            {
                logger.LogWarning(
                    "Fjernet katalograd {Organisasjonsnummer} «{Navn}»: {Utfall}.",
                    s.Organisasjonsnummer, s.Navn, s.Utfall);
            }

            // En rad uten navneform vises med Brregs VERSAL-form. Det er en bevisst, akseptert tilstand
            // (ingen gjettet fallback), men den skal være synlig — ellers ser det ut som en feil i UI-et.
            foreach (var u in r.UtenNavneform)
            {
                logger.LogInformation(
                    "Ingen navneform for {VirksomhetId} «{Navn}» — {Grunn}. Vises med registerets form.",
                    u.VirksomhetId, u.Navn, u.Grunn);
            }
            foreach (var h in r.HoppetOver)
            {
                logger.LogWarning("Registernavn-synk hoppet over: {Detalj}", h);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal avslutning ved app-shutdown midt i synken — ikke en feil. Jobben er idempotent, så
            // neste oppstart fortsetter der denne slapp uten å gjenta det som alt er gjort.
        }
        catch (Exception ex)
        {
            // Egen catch: en feilet navnesynk skal ALDRI ta ned appen. Navnene står da i sin
            // nåværende form, og neste oppstart prøver på nytt.
            logger.LogError(ex, "Registernavn-synk feilet. Navnene står urørt; nytt forsøk ved neste oppstart.");
        }
    }
}
