using RegelIde.Data;

namespace RegelIde.Api;

/// <summary>
/// [Ny, issue #440 PR 1, Johanns avgjørelser 2026-10-30] Aktørkøen: ett sted for ukjente aktører, gruppert per navn med alle forekomster.
/// Erstatter /navnekandidater og /virksomhet-kandidater som godkjenningssted. <b>Bare lesing</b>: ingen skriving, ingen godkjenning og ingen
/// migrering i PR 1. Godkjenningen (aktør, navneform, hjemmel og tagger i én transaksjon) kommer i PR 2, etter juristrunden på
/// hjemmelregelen. Egen fil, som <see cref="FunnEndepunkter"/>.
/// </summary>
public static class AktorkoEndepunkter
{
    public static void MapAktorkoEndepunkter(this WebApplication app)
    {
        var aktorko = app.MapGroup("/api/aktorko").WithOpenApi();

        aktorko.MapGet("/", async (AktorkoTjeneste tjeneste, CancellationToken ct) => Results.Ok(await tjeneste.KoAsync(ct)))
            .WithName("HentAktorko")
            .WithSummary("Issue #440 — aktørkøen: ventende navnekandidater som ikke er et generisk ord, en kjent aktør, en kjent klasse/rolle/område "
                         + "eller en bøyningsform av dem, gruppert per navn (samme ord og bøyning slås sammen automatisk). Per navn: forekomster, "
                         + "rettskilder, varianter med regelen, og hjemmelforslaget etter regelen (bare forslag) eller hullet. Skriver ingenting.");

        // Før /{nokkel}, så «fordeling» ikke tolkes som et navn.
        aktorko.MapGet("/fordeling", async (AktorkoTjeneste tjeneste, CancellationToken ct) => Results.Ok(await tjeneste.FordelingAsync(ct)))
            .WithName("HentAktorkoFordeling")
            .WithSummary("Issue #440 — alle ventende navnekandidater fordelt på rutene (generisk, kjent_aktor, kjent_begrep, boyningsform, aktorko) "
                         + "med forekomster, rettskilder og de hyppigste ordene per rute. Summen er antall ventende; ingen kandidat står i to ruter.");

        aktorko.MapGet("/{nokkel}", async (string nokkel, AktorkoTjeneste tjeneste, CancellationToken ct) =>
                await tjeneste.DetaljAsync(nokkel, ct) is { } detalj
                    ? Results.Ok(detalj)
                    : Results.NotFound(new { feil = $"«{nokkel}» står ikke i aktørkøen. Det er kanskje et kjent ord, eller ikke en ventende navnekandidat." }))
            .WithName("HentAktorkoNavn")
            .WithSummary("Issue #440 — ett navn i aktørkøen, slått opp på hvilken som helst variant: variantene med regelen, genitivforslagene "
                         + "(bare forslag), forekomstene per rettskilde med setningen, og hjemmelforslagene (bare forslag) eller hullet.");
    }
}
