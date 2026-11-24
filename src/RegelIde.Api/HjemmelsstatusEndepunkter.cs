using Microsoft.EntityFrameworkCore;
using RegelIde.Data;

namespace RegelIde.Api;

/// <summary>En kilde (opphever, viderefører, erstatter) slik rettskildesiden viser den: lenke, kort referanse og bestemmelse.</summary>
public sealed record HjemmelsstatusKildeDto(
    Guid? RettskildeId, string? DokumentId, string? Referanse, string? Tittel, string? Eid, string? Bestemmelse, string? Sitat, bool FraKunngjort);

public sealed record HjemmelsstatusVidereforingDto(
    HjemmelsstatusKildeDto Kilde, string Form, string? Forbehold, DateOnly? Tidsfrist, string? Dekning);

public sealed record HjemmelsstatusRadDto(
    Guid RettskildeHjemmelId, string HjemmelEid, string? HjemmelEli, Guid? MalRettskildeId, string? MalReferanse, string? MalTittel, string? Bestemmelse,
    string? Rolle, string? RolleRegel, string Status, string EffektivStatus, bool Teller, string? ArvetSamletStatus, string? Merknad,
    string? Bevistrinn, string? UkjentGrunn, string? FulgtEli, HjemmelsstatusKildeDto? OpphevetAv, DateOnly? OpphevetDato,
    HjemmelsstatusVidereforingDto? Videreforing, HjemmelsstatusKildeDto? ErstattetAv, string? Premissmangel, string Kunnskapsstatus);

/// <summary>Svaret på «hvilke hjemler har forskriften, hvilken status har hver, og hva blir den samlede statusen?» (#472 AK 1–2).</summary>
public sealed record HjemmelsstatusDto(
    bool Beregnet, string? Regelversjon, DateTimeOffset? BeregnetTidspunkt, string? SamletStatus, string? SamletKunnskapsstatus,
    bool Gjenstandslos, Guid? SvakestePremissId, bool? RentEndringsvedtak, IReadOnlyList<HjemmelsstatusRadDto> Hjemler,
    Guid? FunnId = null, bool MuligBortfaltVises = false);

/// <summary>
/// [Ny, issue #472 PR 1, Johanns avgjørelser 2026-11-18] Endepunktene for hjemmelsstatusen. Egen fil, som <see cref="FunnEndepunkter"/>.
/// [LÅST, Johann 2026-11-18 (4)] Bygges nå, publiseres ikke: ingenting her lager funn eller endrer nettsiden. Funnene kommer i PR 2 og står som
/// «oppdaget» til #446 er klassifisert og juristen har målt presisjonen.
/// </summary>
public static class HjemmelsstatusEndepunkter
{
    /// <summary>«LOV-1993-06-11-65» for en dokument-ELI når dokumentet bare er en referanse-stub (kaldsjekken 2026-11-20: den rå ELI-en sto i
    /// tabellen). Ukjent form → ELI-en som den er.</summary>
    public static string? EliReferanse(string? eli) => HjemmelOpphevetFunnregel.EliReferanse(eli);

    public static void MapHjemmelsstatusEndepunkter(this WebApplication app)
    {
        app.MapPost("/api/hjemmelsstatus/maskinregel", async (bool? skriv, bool? lovtidend, bool? medRader, Guid? rettskildeId, HttpRequest request,
                RegelIdeDbContext db, HjemmelsstatusRegel regel, CancellationToken ct) =>
            {
                var bruker = await GjeldendeBrukerTjeneste.FinnAsync(request, db, ct);
                if (bruker is null) return GjeldendeBrukerTjeneste.IkkeInnloggetSvar(request);
                return Results.Ok(await regel.KjorAsync(skriv ?? false, bruker.Navn, lovtidend ?? true, medRader ?? false,
                    rettskildeId is { } id ? [id] : null, ct));
            })
            .WithOpenApi()
            .WithName("KjorMaskinregelHjemmelsstatus")
            .WithSummary("Issue #472 PR 1 — status per rad i rettskilde_hjemler: aktiv, opphevet (med bevistrinn E1/E1u/E2/EN_LTI, opphevende "
                         + "bestemmelse og sitat), videreført (V1–V4, forbehold, tidsfrist, dekning), videreføring utløpt, erstattet, gjenstandsløs "
                         + "eller ukjent, og rollen (materiell, kompetanseledd, overgangsgrunnlag, gjenstand eller NULL). Rapporten teller per status, "
                         + "per bevistrinn og per samlet status for gjeldende forskrifter (rene endringsvedtak for seg). Tørrkjøring er standard; "
                         + "?skriv=true skriver tabellen hjemmelsstatus på nytt. ?lovtidend=false lar være å laste ned Lovtidend-arkivet (69 MB), og "
                         + "EN-radene blir da stående som ukjent. ?medRader=true tar med radene; ?rettskildeId= avgrenser (med forskriftene den "
                         + "bygger på).");

        app.MapGet("/api/rettskilder/{id:guid}/hjemmelsstatus", async (Guid id, RegelIdeDbContext db, HjemmelsstatusRegel regel, CancellationToken ct) =>
            {
                var f = await db.Rettskilder.AsNoTracking().Where(r => r.Id == id).Select(r => new { r.Id, r.Eli, r.RentEndringsvedtak }).FirstOrDefaultAsync(ct);
                if (f is null) return Results.NotFound(new { feil = $"Ingen rettskilde med id '{id}'." });
                if (await regel.LesAsync(id, ct) is not { } lest)
                    return Results.Ok(new HjemmelsstatusDto(false, null, null, null, null, false, null, f.RentEndringsvedtak, []));
                var (samlet, rader) = lest;
                var egne = samlet.Premisser.Select(p => (P: p, R: rader[p.Rad.HjemmelId])).ToList();
                var ider = egne.SelectMany(x => new[] { x.R.MalRettskildeId, x.R.OpphevetAvRettskildeId, x.R.VidereforingRettskildeId, x.R.ErstattetAvRettskildeId })
                    .Where(g => g is not null).Select(g => g!.Value).Distinct().ToList();
                var kilder = await db.Rettskilder.AsNoTracking().Where(r => ider.Contains(r.Id))
                    .Select(r => new { r.Id, r.DokumentId, r.Eli, Tittel = r.Kortnavn ?? r.Tittel }).ToDictionaryAsync(r => r.Id, ct);
                string? Referanse(Guid? k) => k is { } g && kilder.TryGetValue(g, out var x) ? UtgattAktorFunnregel.Dokumentreferanse(x.DokumentId) ?? x.Eli : null;
                HjemmelsstatusKildeDto? Kilde(Guid? k, string? eid, string? sitat, bool kunngjort) => k is null && eid is null ? null
                    : new HjemmelsstatusKildeDto(k, k is { } g && kilder.TryGetValue(g, out var x) ? x.DokumentId : null, Referanse(k),
                        k is { } g2 && kilder.TryGetValue(g2, out var y) ? y.Tittel : null, eid,
                        eid is null ? null : Hjemmelstekstlesing.Bestemmelsesetikett(eid), sitat, kunngjort);
                var dto = egne.Select(x =>
                {
                    var r = x.R;
                    return new HjemmelsstatusRadDto(
                        r.RettskildeHjemmelId, r.HjemmelEid, r.HjemmelEli, r.MalRettskildeId, Referanse(r.MalRettskildeId) ?? EliReferanse(r.HjemmelEli),
                        r.MalRettskildeId is { } m && kilder.TryGetValue(m, out var mk) ? mk.Tittel : null,
                        r.HjemmelEid == r.HjemmelEli ? null : Hjemmelstekstlesing.Bestemmelsesetikett(r.HjemmelEid),
                        r.Rolle, r.RolleRegel, r.Status, x.P.Status, x.P.Teller, x.P.Arvet?.Status, x.P.Merknad, r.Bevistrinn, r.UkjentGrunn, r.FulgtEli,
                        Kilde(r.OpphevetAvRettskildeId, r.OpphevetAvEid, r.OpphevetAvSitat, r.OpphevetFraKunngjort), r.OpphevetDato,
                        r.VidereforingForm is null ? null : new HjemmelsstatusVidereforingDto(
                            Kilde(r.VidereforingRettskildeId, r.VidereforingEid, r.VidereforingSitat, r.VidereforingFraKunngjort)!, r.VidereforingForm,
                            r.VidereforingForbehold, r.VidereforingTidsfrist, r.VidereforingDekning),
                        Kilde(r.ErstattetAvRettskildeId, r.ErstattetAvEid, null, false), r.Premissmangel, x.P.Kunnskapsstatus);
                }).ToList();
                var forste = egne.Select(x => x.R).MaxBy(r => r.BeregnetTidspunkt)!;
                // [Ny, #472 PR 2, premissporten] «mulig bortfalt» står bare når funnet for forskriften har «Ny hjemmel» vurdert eller merket
                // «ikke vurdert». Uten funn (ikke kjørt, eller uttømt virkning) står det ikke.
                var nokkel = f.Eli is null ? null : HjemmelOpphevetFunnregel.NokkelPrefiks + UtgattAktorFunnregel.EliKort(f.Eli);
                var funn = nokkel is null ? null
                    : await db.Funn.AsNoTracking().Include(x => x.Vurderingspunkter).FirstOrDefaultAsync(x => x.EksternNokkel == nokkel, ct);
                return Results.Ok(new HjemmelsstatusDto(true, forste.Regelversjon, forste.BeregnetTidspunkt, samlet.Status, samlet.Kunnskapsstatus,
                    samlet.Gjenstandslos, samlet.SvakestePremissId, f.RentEndringsvedtak, dto, funn?.Id,
                    funn is not null && funn.Status != Funn.ForklartBort && FunnTjeneste.NyHjemmelVurdert(funn)));
            })
            .WithOpenApi()
            .WithName("HentHjemmelsstatus")
            .WithSummary("Issue #472 PR 1 — hjemlene til rettskilden med status, rolle, bevis og videreføring, og den samlede statusen regnet ut "
                         + "etter svakeste premiss med rekursjon (lagres ikke). beregnet=false når maskinregelen ikke er kjørt med ?skriv=true.");
    }
}
