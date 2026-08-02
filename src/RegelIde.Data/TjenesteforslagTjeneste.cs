using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// «Identifiser tjenester» (byggesteg 5 runde 1, docs/06-veikart.md) — foreslår nye Tjeneste-objekter
/// fra valgte rettskilder pluss virksomhetens registrerte kunnskapsbibliotek-lenker (nettside o.l.).
/// Bevisst IKKE avhengig av at noe Tjeneste-objekt finnes fra før — det er nettopp det denne agenten
/// finner ut. Oppretter forslag via <see cref="TjenesteregisterTjeneste.OpprettForslagFraKiAsync"/>.
/// </summary>
public sealed class TjenesteforslagTjeneste(RegelIdeDbContext db, IKiAgentKlient kiKlient, TjenesteregisterTjeneste tjenesteregister)
{
    private const string AiForslagVersjon = "stub-v1";

    private sealed record TjenesteForslagJson(string Tittel, string? KortBeskrivelse);

    public async Task<List<TjenesteEntitet>> KjorForslagAsync(
        Guid virksomhetId, IReadOnlyList<Guid> rettskildeIder, string opprettetAv, CancellationToken ct = default)
    {
        var rettskildeKontekst = await RettskildeKontekstHjelper.ByggKontekstAsync(db, rettskildeIder, ct);
        var lenker = await db.KunnskapsbibliotekLenker
            .Where(l => l.VirksomhetId == virksomhetId)
            .ToListAsync(ct);
        var filer = await db.KunnskapsbibliotekFiler
            .Where(f => f.VirksomhetId == virksomhetId)
            .Select(f => new { f.Id, f.Filnavn, f.UtvunnetTekst })
            .ToListAsync(ct);

        var sb = new StringBuilder(rettskildeKontekst);
        if (lenker.Count > 0)
        {
            sb.AppendLine("# Kunnskapsbibliotek-lenker");
            foreach (var lenke in lenker)
            {
                sb.AppendLine(lenke.Beskrivelse is null ? lenke.Url : $"{lenke.Url} — {lenke.Beskrivelse}");
            }
        }
        if (filer.Count > 0)
        {
            sb.AppendLine("# Kunnskapsbibliotek-filer");
            foreach (var fil in filer)
            {
                sb.AppendLine($"## {fil.Filnavn}");
                sb.AppendLine(fil.UtvunnetTekst);
            }
        }

        var svar = await kiKlient.GenererAsync("Identifiser tjenester fra rettskilder og kunnskapsbibliotek", sb.ToString(), ct);

        List<TjenesteForslagJson>? forslag;
        try
        {
            forslag = JsonSerializer.Deserialize<List<TjenesteForslagJson>>(svar, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"KI-klienten returnerte ugyldig JSON for tjenesteforslag: {ex.Message}", ex);
        }
        if (forslag is null || forslag.Count == 0) return [];

        var kildeReferanserJson = JsonSerializer.Serialize(new
        {
            rettskildeIder,
            lenkeIder = lenker.Select(l => l.Id),
            filIder = filer.Select(f => f.Id),
        });
        var opprettede = new List<TjenesteEntitet>();
        foreach (var f in forslag)
        {
            var tjeneste = await tjenesteregister.OpprettForslagFraKiAsync(
                virksomhetId, f.Tittel, f.KortBeskrivelse, opprettetAv, AiForslagVersjon, kildeReferanserJson, ct);
            opprettede.Add(tjeneste);
        }
        return opprettede;
    }
}
