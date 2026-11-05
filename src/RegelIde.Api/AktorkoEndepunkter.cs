using RegelIde.Data;

namespace RegelIde.Api;

/// <summary>
/// [Ny, issue #440 PR 1, Johanns avgjørelser 2026-10-30] Aktørkøen: ett sted for ukjente aktører, gruppert per navn med alle forekomster.
/// Erstatter /navnekandidater og /virksomhet-kandidater som godkjenningssted. PR 1 var bare lesing. Egen fil, som <see cref="FunnEndepunkter"/>.
/// [ENDRET, #440 PR 2] Skrivingen: godkjenn (aktør eller klasse/rolle/område, navneform, hjemmel eller «hjemmel mangler» og tagger i én
/// transaksjon), «ikke en aktør», koble til en aktør som finnes, skill ut / ta med / tilbakestill, og «Tagg alle som generisk omtale» per ord
/// med tørrkjøring. Reglene står i <see cref="AktorkoSkrivTjeneste"/>.
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

        // ---------------------------------------------------------------- [Ny, #440 PR 2] Skriving

        aktorko.MapPost("/{nokkel}/godkjenn", (string nokkel, AktorkoGodkjenning body, HttpRequest request, RegelIdeDbContext db,
                AktorkoSkrivTjeneste tjeneste, CancellationToken ct) =>
                Skriv(request, db, ct, bruker => tjeneste.GodkjennAsync(nokkel, body, bruker, ct), nokkel))
            .WithName("GodkjennAktorkoNavn")
            .WithSummary("Issue #440 PR 2 — godkjenner ett navn i aktørkøen i ÉN transaksjon: oppretter aktøren (eller klassen, rollen eller området), "
                         + "navneformen med hjemmelen fra et av forslagene eller «hjemmel mangler» (hjemmel_art = oppretting når forslaget velges), "
                         + "tagger alle forekomstene og setter kandidatene til Godkjent. Ingenting er forhåndsvalgt: type, navn (en av skrivemåtene), "
                         + "hjemmel og godkjenning må sendes. forventetForekomster avviser med 409 når navnet er endret siden det ble lest. Feiler et "
                         + "steg, rulles alt tilbake. Svaret er kvitteringen.");

        aktorko.MapPost("/{nokkel}/ikke-aktor", (string nokkel, AktorkoIkkeAktorRequest body, HttpRequest request, RegelIdeDbContext db,
                AktorkoSkrivTjeneste tjeneste, CancellationToken ct) =>
                Skriv(request, db, ct, bruker => tjeneste.IkkeAktorAsync(nokkel, body.Begrunnelse, body.ForventetForekomster, bruker, ct), nokkel))
            .WithName("AktorkoIkkeAktor")
            .WithSummary("Issue #440 PR 2 — «ikke en aktør»: alle forekomstene av navnet avvises med begrunnelsen (påkrevd). Radene slettes ikke.");

        aktorko.MapPost("/{nokkel}/koble", (string nokkel, AktorkoKobling body, HttpRequest request, RegelIdeDbContext db,
                AktorkoSkrivTjeneste tjeneste, CancellationToken ct) =>
                Skriv(request, db, ct, bruker => tjeneste.KobleAsync(nokkel, body, bruker, ct), nokkel))
            .WithName("AktorkoKoble")
            .WithSummary("Issue #440 PR 2 — kobler navnet til en aktør som finnes, som ny navneform med grunn (påkrevd), og tagger alle forekomstene. "
                         + "Kvitteringen varsler når navnet blir utgått navn eller kortform, eller når aktøren er nedlagt.");

        aktorko.MapPost("/{nokkel}/skill-ut", (string nokkel, AktorkoVariantRequest body, HttpRequest request, RegelIdeDbContext db,
                AktorkoSkrivTjeneste tjeneste, CancellationToken ct) =>
                Skriv(request, db, ct, bruker => tjeneste.SkillUtAsync(nokkel, body.Variant, bruker, ct), nokkel))
            .WithName("AktorkoSkillUt")
            .WithSummary("Issue #440 PR 2 — skiller ut en variant som eget navn (navnekandidater.aktorko_gruppe = varianten).");

        aktorko.MapPost("/{nokkel}/ta-med", (string nokkel, AktorkoVariantRequest body, HttpRequest request, RegelIdeDbContext db,
                AktorkoSkrivTjeneste tjeneste, CancellationToken ct) =>
                Skriv(request, db, ct, bruker => tjeneste.TaMedAsync(nokkel, body.Variant, bruker, ct), nokkel))
            .WithName("AktorkoTaMed")
            .WithSummary("Issue #440 PR 2 — tar med et ord fra et annet navn i køen (typisk et genitivforslag) i dette navnet. Navnets egne "
                         + "forekomster festes også (aktorko_gruppe = nøkkelen).");

        aktorko.MapPost("/{nokkel}/tilbakestill-gruppering", (string nokkel, HttpRequest request, RegelIdeDbContext db,
                AktorkoSkrivTjeneste tjeneste, CancellationToken ct) =>
                Skriv(request, db, ct, bruker => tjeneste.TilbakestillAsync(nokkel, bruker, ct), nokkel))
            .WithName("AktorkoTilbakestillGruppering")
            .WithSummary("Issue #440 PR 2 — setter aktorko_gruppe tilbake til NULL for alle forekomstene av navnet: regelen grupperer igjen.");

        aktorko.MapPost("/generisk-omtale", async (AktorkoGeneriskOmtaleRequest body, HttpRequest request, RegelIdeDbContext db,
                AktorkoSkrivTjeneste tjeneste, CancellationToken ct) =>
            {
                var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
                if (bruker is null) return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
                try
                {
                    return Results.Ok(await tjeneste.TaggSomGeneriskAsync(body.Ord, body.Skriv ?? false, bruker.Navn, ct));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { feil = ex.Message });
                }
            })
            .WithName("AktorkoTaggSomGeneriskOmtale")
            .WithSummary("Issue #440 PR 2, Johanns avgjørelse 2026-10-30 — «Tagg alle som generisk omtale» for ett ord: alle ventende forekomster av "
                         + "nøyaktig ordet på rutene generisk ord og aktørkøen tagges mot den generiske navneformen (opprettes ved behov) og settes til "
                         + "Godkjent. skriv=false (standard) er en tørrkjøring: samme arbeid i en transaksjon som rulles tilbake.");
    }

    /// <summary>Felles for de skrivende kallene: innlogget bruker, 404 når navnet ikke står i køen, 400 ved ugyldig valg, 409 når navnet er
    /// endret siden det ble lest.</summary>
    private static async Task<IResult> Skriv(HttpRequest request, RegelIdeDbContext db, CancellationToken ct,
        Func<string, Task<AktorkoKvittering?>> handling, string nokkel)
    {
        var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
        if (bruker is null) return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
        try
        {
            return await handling(bruker.Navn) is { } kvittering
                ? Results.Ok(kvittering)
                : Results.NotFound(new { feil = $"«{nokkel}» står ikke i aktørkøen. Det er kanskje alt behandlet." });
        }
        catch (AktorkoKonfliktException ex)
        {
            return Results.Conflict(new { feil = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
    }
}

/// <summary>[Ny, #440 PR 2] «Ikke en aktør» for alle forekomstene av et navn.</summary>
public sealed record AktorkoIkkeAktorRequest(string? Begrunnelse, int? ForventetForekomster = null);

/// <summary>[Ny, #440 PR 2] Varianten (ordet) som skal skilles ut eller tas med.</summary>
public sealed record AktorkoVariantRequest(string? Variant);

/// <summary>[Ny, #440 PR 2] «Tagg alle som generisk omtale» for ett ord. <c>Skriv</c> = false/null er tørrkjøring.</summary>
public sealed record AktorkoGeneriskOmtaleRequest(string? Ord, bool? Skriv);
