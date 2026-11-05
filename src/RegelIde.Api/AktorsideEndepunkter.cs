using RegelIde.Data;

namespace RegelIde.Api;

/// <summary>
/// [Ny, issue #441, Johanns avgjørelser 2026-10-30] Den samlede aktørsiden: hjemmelen for aktøren øverst, og de andre rettskildene som relasjoner
/// med én rad per rettskilde. Egen fil, som <see cref="AktorkoEndepunkter"/>. Reglene står i <see cref="AktorsideTjeneste"/>.
/// </summary>
public static class AktorsideEndepunkter
{
    public static void MapAktorsideEndepunkter(this WebApplication app)
    {
        app.MapGet("/api/virksomheter/{id:guid}/hjemmel", async (Guid id, AktorsideTjeneste tjeneste, CancellationToken ct) =>
                await tjeneste.HjemmelAsync(id, ct) is { } h
                    ? Results.Ok(AktorHjemmelDto.Fra(h))
                    : Results.NotFound(new { feil = $"Ingen virksomhet med id '{id}'." }))
            .WithOpenApi()
            .WithName("HentAktorHjemmel")
            .WithSummary("Issue #441 — hjemmelen for at aktøren finnes: R oppretter først, så en navneform med hjemmel_art, så den gjeldende navneformen "
                         + "når hjemmelen står i en kilde med disposisjonstype oppretting, navneendring eller omorganisering. Ellers status = mangler, med "
                         + "hullmerknaden fra aktørkøen, «mest omtalt i» (bare direkte utsagn og taggede forekomster) og «det som finnes» (klasse og register).");

        app.MapPost("/api/virksomheter/{id:guid}/hjemmel/godkjenn", async (Guid id, GodkjennHjemmelRequest? body, HttpRequest request,
                RegelIdeDbContext db, AktorsideTjeneste tjeneste, CancellationToken ct) =>
            {
                var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
                if (bruker is null) return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
                try
                {
                    return await tjeneste.GodkjennHjemmelAsync(id, body?.HjemmelArt, bruker.Navn, ct) is { } kvittering
                        ? Results.Ok(kvittering)
                        : Results.NotFound(new { feil = $"Ingen virksomhet med id '{id}'." });
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { feil = ex.Message });
                }
            })
            .WithOpenApi()
            .WithName("GodkjennAktorHjemmel")
            .WithSummary("Issue #441 — «Godkjenn hjemmelen»: navneformen og R oppretter godkjennes i ÉN transaksjon, og hjemmel_art settes på navneformen "
                         + "når den mangler (oppretting/navnevedtak etter regelen; hjemmelArt i kroppen når regelen ikke avgjør det). Feiler et steg, "
                         + "rulles alt tilbake.");

        app.MapGet("/api/virksomheter/{id:guid}/rettskilder", async (Guid id, AktorsideTjeneste tjeneste, CancellationToken ct) =>
                await tjeneste.RettskilderAsync(id, ct) is { } r
                    ? Results.Ok(AktorRettskilderDto.Fra(r))
                    : Results.NotFound(new { feil = $"Ingen virksomhet med id '{id}'." }))
            .WithOpenApi()
            .WithName("HentAktorRettskilder")
            .WithSummary("Issue #441 — én rad per rettskilde aktøren står i (erstatter sju kall på aktørsiden): utsagn der aktøren er part (K, P, F, annet), "
                         + "forslag som venter (utsagn, navnetreff, navneformer), og merkene Lovdatas «Ansvarlig departement» (sekundær kilde, aldri et "
                         + "utsagn), «Fastsatt av» og «Navnet tagget». Rettskilden med hjemmelen for aktøren gjentas ikke. For en kommune står utsagn via "
                         + "klasse (kompetanse #418, plikter #353) i egne rader med svakeste premiss.");

        app.MapPost("/api/virksomheter/{id:guid}/rettskilder/{rettskildeId:guid}/godkjenn-alle", async (Guid id, Guid rettskildeId, HttpRequest request,
                RegelIdeDbContext db, AktorsideTjeneste tjeneste, CancellationToken ct) =>
            {
                var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
                if (bruker is null) return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
                var (godkjent, hoppetOver) = await tjeneste.GodkjennAlleIRettskildenAsync(id, rettskildeId, bruker.Navn, ct);
                return Results.Ok(new { godkjent, hoppetOver });
            })
            .WithOpenApi()
            .WithName("GodkjennAlleAktorutsagnIRettskilden")
            .WithSummary("Issue #441 — «Godkjenn alle N» i én rad: de ventende utsagnene der aktøren er part og hjemmelen står i rettskilden. Andre "
                         + "aktørers forslag i samme rettskilde røres ikke. Forslag med hjemmel i en merknad hoppes over (#419).");
    }
}

/// <summary>[Ny, issue #441] Kroppen til «Godkjenn hjemmelen». <c>HjemmelArt</c> (oppretting | navnevedtak) trengs bare når regelen ikke avgjør det.</summary>
public sealed record GodkjennHjemmelRequest(string? HjemmelArt);

/// <summary>[Ny, issue #441] Svaret på GET /api/virksomheter/{id}/hjemmel — se <see cref="AktorHjemmel"/>.</summary>
public sealed record AktorHjemmelDto(
    Guid VirksomhetId, string Status, string? Kilde,
    Guid? RettskildeId, string? RettskildeTittel, string? Kortnavn, string? DokumentId, string? Eid, string? Sitat, DateOnly? GjelderFra,
    IReadOnlyList<string> Disposisjonstyper, AktorHjemmelNavneform? Navneform, StrukturkantDto? Oppretterkant, AktorHjemmelNavneform? Navnevedtak,
    string? HjemmelArtForslag, bool MaVelgeHjemmelArt, bool Venter, IReadOnlyList<StrukturkantDto> UtsagnIKilden,
    string? Hullmerknad, IReadOnlyList<MestOmtalt> MestOmtalt, IReadOnlyList<DetSomFinnes> DetSomFinnes)
{
    public static AktorHjemmelDto Fra(AktorHjemmel h) => new(h.VirksomhetId, h.Status, h.Kilde, h.RettskildeId, h.RettskildeTittel, h.Kortnavn,
        h.DokumentId, h.Eid, h.Sitat, h.GjelderFra, h.Disposisjonstyper, h.Navneform,
        h.Oppretterkant is null ? null : StrukturkantDto.FraVisning(h.Oppretterkant), h.Navnevedtak, h.HjemmelArtForslag, h.MaVelgeHjemmelArt,
        h.Venter, h.UtsagnIKilden.Select(StrukturkantDto.FraVisning).ToList(), h.Hullmerknad, h.MestOmtalt, h.DetSomFinnes);
}

/// <summary>[Ny, issue #441] Én rad — se <see cref="AktorRettskilderad"/>.</summary>
public sealed record AktorRettskilderadDto(
    string Gruppe, Guid? RettskildeId, string Tittel, string? Kortnavn, string? Kildetype, string? DokumentId, string? KildeUtenforKorpusType,
    bool AnsvarligDepartement, bool FastsattAv, int NavnetTagget, int NavnetreffVenter, int NavneformerVenter,
    int Kompetanse, int Plikt, int Oppgave, int Annet, int Venter,
    IReadOnlyList<StrukturkantDto> Utsagn, IReadOnlyList<KompetanseViaKlasseTreffDto> ViaKlasse);

/// <summary>[Ny, issue #441] Svaret på GET /api/virksomheter/{id}/rettskilder — se <see cref="AktorRettskilder"/>.</summary>
public sealed record AktorRettskilderDto(
    Guid VirksomhetId, Guid? HjemmelRettskildeId, IReadOnlyList<AktorRettskilderadDto> Rader, IReadOnlyList<StrukturkantDto> Premisser,
    IReadOnlyList<string> Hull, AktorRettskildeTall Tall)
{
    public static AktorRettskilderDto Fra(AktorRettskilder r) => new(r.VirksomhetId, r.HjemmelRettskildeId,
        r.Rader.Select(x => new AktorRettskilderadDto(x.Gruppe, x.RettskildeId, x.Tittel, x.Kortnavn, x.Kildetype, x.DokumentId, x.KildeUtenforKorpusType,
            x.AnsvarligDepartement, x.FastsattAv, x.NavnetTagget, x.NavnetreffVenter, x.NavneformerVenter, x.Kompetanse, x.Plikt, x.Oppgave, x.Annet,
            x.Venter, x.Utsagn.Select(StrukturkantDto.FraVisning).ToList(),
            x.ViaKlasse.Select(t => new KompetanseViaKlasseTreffDto(StrukturkantDto.FraVisning(t.Kant), t.Retning,
                t.Via.Select(StrukturnodeDto.FraVisning).ToList(), t.Kunnskapsstatus, t.Premisser, t.SvakestePremissId, t.SvakestePremissStatus,
                t.Forslag)).ToList())).ToList(),
        r.Premisser.Select(StrukturkantDto.FraVisning).ToList(), r.Hull, r.Tall);
}
