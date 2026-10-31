using System.Text.Json;
using RegelIde.Data;

namespace RegelIde.Api;

/// <summary>
/// [Ny, issue #429 «Funn til vurdering», 2026-10-30] Endepunktene for registeret. Egen fil i stedet for flere hundre nye linjer i
/// <c>Program.cs</c>; registreres med <see cref="MapFunnEndepunkter"/>.
/// <para>
/// <b>Tilgang — kodebasens første rolle- og personsjekk.</b> Godkjenning for publisering og tilbaketrekking krever at den innloggede
/// brukerens id står i <see cref="Funn.KonfignokkelPubliseringsansvarlige"/> (403 ellers). Lista er tom som standard: da kan ingen
/// publisere. En menneskelig juristvurdering krever rollen «Jurist»; å forklare bort krever «Jurist» eller publiseringstilgang.
/// [LÅST, Johann 2026-10-30] Testbrukermodus er ikke sikkerhet (X-Bruker-Id kan settes fritt). Den reelle sperren er at nettsiden
/// bare endres når en PR med <c>nettside/data/funn/register.json</c> flettes.
/// </para>
/// </summary>
public static class FunnEndepunkter
{
    public const string JuristRolle = "Jurist";

    /// <summary>Id-ene i <see cref="Funn.KonfignokkelPubliseringsansvarlige"/>. En verdi som ikke er en Guid, ignoreres (og gir ingen tilgang).</summary>
    public static HashSet<Guid> Publiseringsansvarlige(IConfiguration konfig) =>
        (konfig.GetSection(Funn.KonfignokkelPubliseringsansvarlige).Get<string[]>() ?? [])
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToHashSet();

    public static void MapFunnEndepunkter(this WebApplication app)
    {
        var funn = app.MapGroup("/api/funn").WithOpenApi();

        funn.MapGet("/", async (string? kategori, string? status, Guid? rettskildeId, Guid? virksomhetId, FunnTjeneste tjeneste,
                CancellationToken ct) =>
            {
                var rader = await tjeneste.ListeAsync(kategori, status, rettskildeId, virksomhetId, ct);
                // [ENDRET, #429 PR 2] Premissmanglene for hele lista i to oppslag: maskinregelen gir rundt 700 funn.
                var mangler = await tjeneste.PremissmanglerForAsync(rader, ct);
                return Results.Ok(rader.Select(f => FunnDto.Fra(f, mangler[f.Id], forhandsvisning: null)).ToList());
            })
            .WithName("HentFunn")
            .WithSummary("Issue #429 — registeret over funn til vurdering, nyeste først. Filtre: kategori, status, rettskildeId (også andre "
                         + "versjoner av samme verk, via ELI) og virksomhetId.");

        // Før /{id:guid}, så «publiserbare» og «tilgang» ikke tolkes som id.
        funn.MapGet("/publiserbare", async (FunnTjeneste tjeneste, CancellationToken ct) =>
            {
                var p = await tjeneste.HentPubliserbareAsync(ct);
                return Results.Ok(new { funn = p.Funn, holdtTilbake = p.HoldtTilbake });
            })
            .WithName("HentPubliserbareFunn")
            .WithSummary("Issue #429 AK3 — eksporten til nettsiden: BARE funn som er godkjent for publisering, og bare det frosne "
                         + "øyeblikksbildet (hash kontrollert). Ingen personnavn. holdtTilbake lister nøkler der hashen ikke stemte.");

        // [Ny, #429 PR 2, Johanns avgjørelser 2026-10-30/31] Maskinregelen for utgåtte aktører. Tørrkjøring er standard: uten ?skriv=true
        // skrives ingenting, og rapporten viser hva som ville blitt foreslått, med telling per gruppe (A–D) og per regel (R1–R4).
        funn.MapPost("/maskinregel/utgatt-aktor", async (bool? skriv, bool? medRader, Guid? rettskildeId, HttpRequest request,
                RegelIdeDbContext db, UtgattAktorFunnregel regel, CancellationToken ct) =>
            {
                var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
                if (bruker is null) return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
                var rapport = await regel.KjorAsync(skriv ?? false, bruker.Navn, rettskildeId is { } rid ? [rid] : null, medRader ?? false, ct);
                return Results.Ok(rapport);
            })
            .WithName("KjorMaskinregelUtgattAktor")
            .WithSummary("Issue #429 PR 2 — foreslår ett funn per (rettskilde, aktør) der en gjeldende primær rettskilde bruker et navn på en "
                         + "nedlagt aktør eller et utgått navn. Organiseringsvedtak, rene endringsvedtak og tvetydige navn telles, men foreslås "
                         + "ikke; det samme gjelder par der alle forekomstene treffes av R1–R4. ?skriv=true lagrer som «oppdaget» "
                         + "(monster:utgatt-aktor); uten er det en tørrkjøring. Idempotent: en ny kjøring gir 0 nye og gjenåpner ikke funn som "
                         + "er forklart bort. ?medRader=true tar med parene; ?rettskildeId= avgrenser.");

        funn.MapGet("/tilgang", async (HttpRequest request, RegelIdeDbContext db, IConfiguration konfig, CancellationToken ct) =>
            {
                var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
                var ansvarlige = Publiseringsansvarlige(konfig);
                return Results.Ok(new
                {
                    kanPublisere = bruker is not null && ansvarlige.Contains(bruker.Id),
                    kanJuristvurdere = bruker is not null && bruker.Rolle == JuristRolle,
                    publiseringsansvarligeKonfigurert = ansvarlige.Count > 0,
                });
            })
            .WithName("HentFunnTilgang")
            .WithSummary("Issue #429 — om innlogget bruker kan godkjenne for publisering (RegelIde:Funn:Publiseringsansvarlige) og "
                         + "juristvurdere som menneske (rollen Jurist).");

        funn.MapGet("/{id:guid}", async (Guid id, FunnTjeneste tjeneste, CancellationToken ct) =>
            {
                var f = await tjeneste.HentAsync(id, ct);
                if (f is null) return Results.NotFound(new { feil = $"Ingen funn med id '{id}'." });
                return Results.Ok(FunnDto.Fra(f, await tjeneste.PremissmanglerAsync(f, ct), Forhandsvisning(f)));
            })
            .WithName("HentEttFunn")
            .WithSummary("Issue #429 — ett funn med koblinger, vurderingspunkter, premissmangler og forhåndsvisning av det som blir "
                         + "publisert (det frosne øyeblikksbildet når funnet er godkjent).");

        funn.MapPost("/", async (NyttFunnRequest body, HttpRequest request, FunnTjeneste tjeneste, RegelIdeDbContext db, CancellationToken ct) =>
            {
                var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
                if (bruker is null) return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
                try
                {
                    var (f, ny) = await tjeneste.OpprettAsync(new NyttFunn(
                        body.Kategori, body.Undertype, body.Tittel, body.Beskrivelse,
                        string.IsNullOrWhiteSpace(body.OppdagelsesKilde) ? "manuell" : body.OppdagelsesKilde,
                        body.InternMerknad, body.FunnetI, body.EksternNokkel,
                        body.Koblinger?.Select(k => k.TilNy()).ToList(), body.Vurderingspunkter?.Select(p => p.TilNy()).ToList()),
                        bruker.Navn, ct);
                    var dto = FunnDto.Fra(f, await tjeneste.PremissmanglerAsync(f, ct), Forhandsvisning(f));
                    return ny ? Results.Created($"/api/funn/{f.Id}", dto) : Results.Ok(dto);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { feil = ex.Message });
                }
            })
            .WithName("OpprettFunn")
            .WithSummary("Issue #429 — registrerer et funn (status oppdaget), fra skjemaet eller en agent. oppdagelsesKilde: manuell | "
                         + "agent:jurist-utfordrer | ki:<modell>. Idempotent på eksternNokkel: finnes den, svarer 200 med den eksisterende raden.");

        funn.MapPut("/{id:guid}", async (Guid id, FunnEndringRequest body, HttpRequest request, FunnTjeneste tjeneste,
                RegelIdeDbContext db, CancellationToken ct) =>
            {
                var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
                if (bruker is null) return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
                try
                {
                    var f = await tjeneste.OppdaterAsync(id, new FunnEndring(
                        body.Tittel, body.Beskrivelse, body.Undertype, body.Vurdering, body.InternMerknad,
                        body.Koblinger?.Select(k => k.TilNy()).ToList(), body.Vurderingspunkter?.Select(p => p.TilNy()).ToList()),
                        bruker.Navn, ct);
                    return f is null
                        ? Results.NotFound(new { feil = $"Ingen funn med id '{id}'." })
                        : Results.Ok(FunnDto.Fra(f, await tjeneste.PremissmanglerAsync(f, ct), Forhandsvisning(f)));
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { feil = ex.Message });
                }
            })
            .WithName("OppdaterFunn")
            .WithSummary("Issue #429 AK3 — redigerer et funn. Er det godkjent og det offentlige innholdet endres, går det tilbake til "
                         + "juristvurdert og publiseres ikke før det godkjennes på nytt.");

        funn.MapPost("/{id:guid}/juristvurdering", async (Guid id, JuristvurderingRequest body, HttpRequest request,
                FunnTjeneste tjeneste, RegelIdeDbContext db, CancellationToken ct) =>
            {
                var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
                if (bruker is null) return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
                if (body.VurdertAvType == Funn.Menneske && bruker.Rolle != JuristRolle)
                {
                    return Forbudt("En juristvurdering fra et menneske krever rollen Jurist. Resultatet av en juristrunde med agenten "
                                   + "føres inn med vurdertAvType «ki_agent».");
                }
                return await Overgang(id, tjeneste, ct, () => tjeneste.JuristvurderAsync(id, body.Vurdering, body.VurdertAvType,
                    body.VurdertAvType == Funn.KiAgent ? $"{body.Agent ?? "jurist-utfordrer"} (KI-agent), ført inn av {bruker.Navn}" : bruker.Navn,
                    body.Vurderingspunkter?.Select(p => p.TilNy()).ToList(), ct: ct));
            })
            .WithName("JuristvurderFunn")
            .WithSummary("Issue #429 — oppdaget → juristvurdert. Krever vurdering og minst én prøvd bortforklaring. vurdertAvType: menneske "
                         + "(krever rollen Jurist) | ki_agent (juristagenten, vises med eget merke).");

        funn.MapPost("/{id:guid}/forklar-bort", async (Guid id, ForklarBortRequest body, HttpRequest request, FunnTjeneste tjeneste,
                RegelIdeDbContext db, IConfiguration konfig, CancellationToken ct) =>
            {
                var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
                if (bruker is null) return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
                if (bruker.Rolle != JuristRolle && !Publiseringsansvarlige(konfig).Contains(bruker.Id))
                {
                    return Forbudt("Å forklare bort et funn krever rollen Jurist eller publiseringstilgang.");
                }
                return await Overgang(id, tjeneste, ct, () => tjeneste.ForklarBortAsync(id, body.Begrunnelse, bruker.Navn, ct: ct));
            })
            .WithName("ForklarBortFunn")
            .WithSummary("Issue #429 — oppdaget/juristvurdert → forklart bort, med begrunnelse. Publiseres ikke.");

        funn.MapPost("/{id:guid}/godkjenn-for-publisering", async (Guid id, HttpRequest request, FunnTjeneste tjeneste,
                RegelIdeDbContext db, IConfiguration konfig, CancellationToken ct) =>
            {
                var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
                if (bruker is null) return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
                if (IkkePubliseringsansvarlig(bruker, konfig) is { } nei) return nei;
                return await Overgang(id, tjeneste, ct, () => tjeneste.GodkjennForPubliseringAsync(id, bruker.Navn, ct: ct));
            })
            .WithName("GodkjennFunnForPublisering")
            .WithSummary("Issue #429 AK3 — juristvurdert → godkjent for publisering. Bare publiseringsansvarlige (403 ellers), og bare når "
                         + "premissporten er åpen (409 med manglene ellers). Fryser øyeblikksbildet som eksporteres.");

        funn.MapPost("/{id:guid}/trekk-tilbake", async (Guid id, HttpRequest request, FunnTjeneste tjeneste, RegelIdeDbContext db,
                IConfiguration konfig, CancellationToken ct) =>
            {
                var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
                if (bruker is null) return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
                if (IkkePubliseringsansvarlig(bruker, konfig) is { } nei) return nei;
                return await Overgang(id, tjeneste, ct, () => tjeneste.TrekkTilbakeAsync(id, bruker.Navn, ct));
            })
            .WithName("TrekkFunnTilbake")
            .WithSummary("Issue #429 — godkjent → juristvurdert; funnet forsvinner fra neste eksport. Bare publiseringsansvarlige.");
    }

    private static IResult Forbudt(string melding) => Results.Json(new { feil = melding }, statusCode: StatusCodes.Status403Forbidden);

    private static IResult? IkkePubliseringsansvarlig(Bruker bruker, IConfiguration konfig)
    {
        var ansvarlige = Publiseringsansvarlige(konfig);
        if (ansvarlige.Contains(bruker.Id)) return null;
        return Forbudt(ansvarlige.Count == 0
            ? "Ingen er satt opp som publiseringsansvarlig (RegelIde:Funn:Publiseringsansvarlige er tom), så ingen kan godkjenne for publisering."
            : "Bare publiseringsansvarlige (RegelIde:Funn:Publiseringsansvarlige) kan godkjenne for publisering eller trekke tilbake.");
    }

    private static async Task<IResult> Overgang(Guid id, FunnTjeneste tjeneste, CancellationToken ct, Func<Task<FunnEntitet?>> handling)
    {
        try
        {
            var f = await handling();
            return f is null
                ? Results.NotFound(new { feil = $"Ingen funn med id '{id}'." })
                : Results.Ok(FunnDto.Fra(f, await tjeneste.PremissmanglerAsync(f, ct), Forhandsvisning(f)));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { feil = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { feil = ex.Message });
        }
    }

    /// <summary>Det som blir publisert: det frosne øyeblikksbildet når funnet er godkjent, ellers det funnet ville gitt i dag. Funn som er
    /// forklart bort, publiseres ikke og får ingen forhåndsvisning.</summary>
    private static JsonElement? Forhandsvisning(FunnEntitet f)
    {
        if (f.Status == Funn.ForklartBort) return null;
        var json = f.Status == Funn.GodkjentForPublisering && f.GodkjentInnholdJson is not null
            ? f.GodkjentInnholdJson
            : FunnTjeneste.Serialiser(FunnTjeneste.ByggOffentligInnhold(f, DateOnly.FromDateTime(DateTime.UtcNow)));
        using var dok = JsonDocument.Parse(json);
        return dok.RootElement.Clone();
    }
}

public sealed record FunnKoblingRequest(
    string Rolle, string Etikett, string? Merknad, string? RettskildeEli, string? Eid, Guid? VirksomhetId, Guid? BegrepId,
    Guid? StrukturkantId, Guid? KildefeilId, string? EksternLenke, string? QuoteExact = null)
{
    public NyKobling TilNy() => new(Rolle, Etikett, Merknad, RettskildeEli, Eid, VirksomhetId, BegrepId, StrukturkantId, KildefeilId, EksternLenke,
        QuoteExact);
}

public sealed record FunnVurderingspunktRequest(string Type, string Tekst, string? Overskrift, string? Sikkerhet)
{
    public NyttVurderingspunkt TilNy() => new(Type, Tekst, Overskrift, Sikkerhet);
}

public sealed record NyttFunnRequest(
    string Kategori, string? Undertype, string Tittel, string Beskrivelse, string? OppdagelsesKilde, string? InternMerknad, string? FunnetI,
    string? EksternNokkel, List<FunnKoblingRequest>? Koblinger, List<FunnVurderingspunktRequest>? Vurderingspunkter);

public sealed record FunnEndringRequest(
    string Tittel, string Beskrivelse, string? Undertype, string? Vurdering, string? InternMerknad,
    List<FunnKoblingRequest>? Koblinger, List<FunnVurderingspunktRequest>? Vurderingspunkter);

public sealed record JuristvurderingRequest(string Vurdering, string VurdertAvType, string? Agent, List<FunnVurderingspunktRequest>? Vurderingspunkter);

public sealed record ForklarBortRequest(string Begrunnelse);

public sealed record FunnKoblingDto(
    Guid Id, string Rolle, int Sortering, string Etikett, string? Merknad, string? RettskildeEli, Guid? RettskildeId, string? Eid,
    Guid? NodeId, Guid? VirksomhetId, Guid? BegrepId, Guid? StrukturkantId, Guid? KildefeilId, string? EksternLenke, string? QuoteExact);

public sealed record FunnVurderingspunktDto(Guid Id, string Type, int Sortering, string? Overskrift, string Tekst, string? Sikkerhet);

/// <summary>[Ny, issue #429] Hele funnet for appen (ikke for nettsiden — den får bare øyeblikksbildet).</summary>
public sealed record FunnDto(
    Guid Id, string? EksternNokkel, string Kategori, string? Undertype, string Tittel, string Beskrivelse, string? Vurdering,
    string? InternMerknad, string Status, string OppdagelsesKilde, string? FunnetI, string OpprettetAv, DateTimeOffset OpprettetTidspunkt,
    string? SistEndretAv, DateTimeOffset? SistEndretTidspunkt, string? JuristvurdertAv, DateTimeOffset? JuristvurdertTidspunkt,
    string? VurdertAvType, string? ForklartBortBegrunnelse, string? ForklartBortAv, DateTimeOffset? ForklartBortTidspunkt,
    string? GodkjentAv, DateTimeOffset? GodkjentTidspunkt, DateTimeOffset? KildeEndretTidspunkt,
    List<FunnKoblingDto> Koblinger, List<FunnVurderingspunktDto> Vurderingspunkter, List<string> Premissmangler, JsonElement? Forhandsvisning)
{
    public static FunnDto Fra(FunnEntitet f, List<string> premissmangler, JsonElement? forhandsvisning) => new(
        f.Id, f.EksternNokkel, f.Kategori, f.Undertype, f.Tittel, f.Beskrivelse, f.Vurdering, f.InternMerknad, f.Status, f.OppdagelsesKilde,
        f.FunnetI, f.OpprettetAv, f.OpprettetTidspunkt, f.SistEndretAv, f.SistEndretTidspunkt, f.JuristvurdertAv, f.JuristvurdertTidspunkt,
        f.VurdertAvType, f.ForklartBortBegrunnelse, f.ForklartBortAv, f.ForklartBortTidspunkt, f.GodkjentAv, f.GodkjentTidspunkt,
        f.KildeEndretTidspunkt,
        f.Koblinger.OrderBy(k => k.Sortering).Select(k => new FunnKoblingDto(k.Id, k.Rolle, k.Sortering, k.Etikett, k.Merknad,
            k.RettskildeEli, k.RettskildeId, k.Eid, k.NodeId, k.VirksomhetId, k.BegrepId, k.StrukturkantId, k.KildefeilId, k.EksternLenke,
            k.QuoteExact)).ToList(),
        f.Vurderingspunkter.OrderBy(p => p.Sortering).Select(p => new FunnVurderingspunktDto(p.Id, p.Type, p.Sortering, p.Overskrift, p.Tekst,
            p.Sikkerhet)).ToList(),
        premissmangler, forhandsvisning);
}
