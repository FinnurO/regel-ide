using RegelIde.Data;

namespace RegelIde.Api;

/// <summary>
/// [Ny, issue #517 «Til vurdering», Johanns avgjørelser 2026-11-27] Tellingene til startsiden. Ett lett kall som bare teller (AK 8); egen fil,
/// som <see cref="AktorkoEndepunkter"/>. Reglene står i <see cref="TilVurderingTjeneste"/>.
/// </summary>
public static class TilVurderingEndepunkter
{
    public static void MapTilVurderingEndepunkter(this WebApplication app)
    {
        app.MapGet("/api/til-vurdering", async (TilVurderingTjeneste tjeneste, CancellationToken ct) => Results.Ok(await tjeneste.HentAsync(ct)))
            .WithOpenApi()
            .WithName("HentTilVurdering")
            .WithSummary("Issue #517 — det som venter på en menneskelig avgjørelse, per kø: aktørkøen etter forhåndsklassifiseringen, navneformene "
                         + "og etterfølgerkantene som holder igjen funn om utgåtte aktører (per aktør), pilotforslagene, funnene om opphevet hjemmel, "
                         + "forekomstbindingene, klasse- og navneformforslag, ressortkanter, kildefeil og de eldre køene. Bare tall; skriver ingenting.");
    }
}
