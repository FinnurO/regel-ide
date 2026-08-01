using System.Text.Json;

namespace RegelIde.Data;

/// <summary>
/// «Identifiser begrep» (byggesteg 5 runde 1, docs/06-veikart.md) — rent rettskilde-drevet, ingen
/// kobling til Tjeneste. Kaller <see cref="IKiAgentKlient"/> med de valgte rettskildenes faktiske
/// tekst som kontekst og oppretter forslag via <see cref="BegrepsregisterTjeneste.OpprettForslagFraKiAsync"/>.
/// </summary>
public sealed class BegrepsforslagTjeneste(RegelIdeDbContext db, IKiAgentKlient kiKlient, BegrepsregisterTjeneste begrepsregister)
{
    private const string AiForslagVersjon = "stub-v1";

    private sealed record BegrepForslagJson(string Term, string Definisjon, string Begrepstype, string? LovreferanseEid);

    public async Task<List<BegrepEntitet>> KjorForslagAsync(
        Guid virksomhetId, IReadOnlyList<Guid> rettskildeIder, string opprettetAv, CancellationToken ct = default)
    {
        var kontekst = await RettskildeKontekstHjelper.ByggKontekstAsync(db, rettskildeIder, ct);
        var svar = await kiKlient.GenererAsync("Identifiser begrep i valgte rettskilder", kontekst, ct);

        List<BegrepForslagJson>? forslag;
        try
        {
            forslag = JsonSerializer.Deserialize<List<BegrepForslagJson>>(svar, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"KI-klienten returnerte ugyldig JSON for begrepsforslag: {ex.Message}", ex);
        }
        if (forslag is null || forslag.Count == 0) return [];

        var kildeReferanserJson = JsonSerializer.Serialize(new { rettskildeIder });
        var opprettede = new List<BegrepEntitet>();
        foreach (var f in forslag)
        {
            var begrep = await begrepsregister.OpprettForslagFraKiAsync(
                virksomhetId, f.Term, f.Definisjon, f.LovreferanseEid, gjelderFor: null, kodelisteReferanseId: null,
                skosUrl: null, f.Begrepstype, opprettetAv, AiForslagVersjon, kildeReferanserJson, ct);
            opprettede.Add(begrep);
        }
        return opprettede;
    }
}
