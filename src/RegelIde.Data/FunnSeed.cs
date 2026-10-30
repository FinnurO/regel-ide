using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #429, Johanns avgjørelse 2026-10-30 «seed av de fem funnene»] Fører inn funnene som alt står i #429: funn 1 (godkjent for
/// publisering av Johann 2026-10-29), kontrollen for lærere (forklart bort) og tre kandidater (oppdaget). Kilden er
/// <c>Seed/funn-429.json</c>, innebygd i assemblyen; tekstene er ordrett fra kommentarene i saken.
/// <para>
/// <b>Idempotent på stabil nøkkel</b> (<see cref="FunnEntitet.EksternNokkel"/>, CLAUDE.md §4): finnes nøkkelen, røres raden ikke — en
/// redigering eller en tilbaketrekking Johann har gjort i appen, overskrives aldri. Statusovergangene går gjennom
/// <see cref="FunnTjeneste"/> med tidspunktene fra saken, så CHECK-ene og øyeblikksbildet blir de samme som ved en vanlig godkjenning.
/// Godkjenningen av funn 1 går utenom tilgangssjekken i API-et: den er en avgjørelse som alt er tatt og står skriftlig i #429.
/// </para>
/// <para>
/// <b>Aktører</b> slås opp på organisasjonsnummer, og bare når fila ikke har et, på eksakt navn. Finnes aktøren ikke, hoppes koblingen
/// over og meldes — aldri nærmeste treff (CLAUDE.md §8). Bestemmelser slås opp av <see cref="FunnTjeneste"/> (ELI + eId, NULL hvis de
/// ikke er i korpuset).
/// </para>
/// Skriver til databasen ved oppstart ⇒ gated av <see cref="Funn.KonfignokkelSeedVedOppstart"/>, false i testfiksturen for API-et.
/// </summary>
public static class FunnSeed
{
    public const string Ressursnavn = "RegelIde.Data.Seed.funn-429.json";
    public const string Av = "seed:funn-429.json (#429)";

    public sealed record Resultat(int Nye, int Fantes, IReadOnlyList<string> HoppetOver);

    public static async Task<Resultat> SeedAsync(RegelIdeDbContext db, CancellationToken ct = default)
    {
        var fil = LesFil();
        var tjeneste = new FunnTjeneste(db);
        int nye = 0, fantes = 0;
        var hoppet = new List<string>();
        foreach (var f in fil.Funn)
        {
            if (await db.Funn.AnyAsync(x => x.EksternNokkel == f.Nokkel, ct))
            {
                fantes++;
                continue;
            }
            var koblinger = new List<NyKobling>();
            foreach (var k in f.Koblinger)
            {
                Guid? virksomhetId = null;
                if (k.Organisasjonsnummer is not null || k.VirksomhetNavn is not null)
                {
                    var treff = k.Organisasjonsnummer is { } orgnr
                        ? await db.Virksomheter.Where(v => v.Organisasjonsnummer == orgnr).Select(v => v.Id).Take(2).ToListAsync(ct)
                        : await db.Virksomheter.Where(v => v.Navn == k.VirksomhetNavn).Select(v => v.Id).Take(2).ToListAsync(ct);
                    if (treff.Count != 1)
                    {
                        hoppet.Add($"{f.Nokkel}: aktøren «{k.Etikett}» ({k.Organisasjonsnummer ?? k.VirksomhetNavn}) ga {treff.Count} treff — koblingen er hoppet over.");
                        continue;
                    }
                    virksomhetId = treff[0];
                }
                koblinger.Add(new NyKobling(k.Rolle, k.Etikett, k.Merknad, k.Eli, k.Eid, VirksomhetId: virksomhetId, EksternLenke: k.Lenke));
            }
            var punkter = f.Vurderingspunkter.Select(p => new NyttVurderingspunkt(p.Type, p.Tekst, p.Overskrift, p.Sikkerhet)).ToList();
            var (funn, _) = await tjeneste.OpprettAsync(new NyttFunn(
                f.Kategori, f.Undertype, f.Tittel, f.Beskrivelse, f.OppdagelsesKilde, f.InternMerknad, f.FunnetI, f.Nokkel,
                koblinger, punkter), Av, ct);
            if (f.Juristvurdering is { } j)
            {
                await tjeneste.JuristvurderAsync(funn.Id, j.Vurdering, j.VurdertAvType, j.Av, tidspunkt: j.Tidspunkt, ct: ct);
            }
            if (f.ForklartBort is { } fb)
            {
                await tjeneste.ForklarBortAsync(funn.Id, fb.Begrunnelse, fb.Av, fb.Tidspunkt, ct);
            }
            if (f.GodkjentForPublisering is { } g)
            {
                await tjeneste.GodkjennForPubliseringAsync(funn.Id, g.Av, g.Tidspunkt, ct);
            }
            nye++;
        }
        return new Resultat(nye, fantes, hoppet);
    }

    internal static Seedfil LesFil()
    {
        using var strom = typeof(FunnSeed).Assembly.GetManifestResourceStream(Ressursnavn)
            ?? throw new InvalidOperationException($"Fant ikke den innebygde ressursen {Ressursnavn}.");
        return JsonSerializer.Deserialize<Seedfil>(strom, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException($"{Ressursnavn} er tom.");
    }

    internal sealed record Seedfil([property: JsonPropertyName("funn")] List<SeedFunn> Funn);

    internal sealed record SeedFunn(
        string Nokkel, string Kategori, string? Undertype, string Tittel, string Beskrivelse, string? InternMerknad,
        string OppdagelsesKilde, string? FunnetI, List<SeedKobling> Koblinger, List<SeedPunkt> Vurderingspunkter,
        SeedJuristvurdering? Juristvurdering, SeedForklartBort? ForklartBort, SeedGodkjenning? GodkjentForPublisering);

    internal sealed record SeedKobling(
        string Rolle, string Etikett, string? Merknad, string? Eli, string? Eid, string? Organisasjonsnummer, string? VirksomhetNavn, string? Lenke);

    internal sealed record SeedPunkt(string Type, string Tekst, string? Overskrift, string? Sikkerhet);
    internal sealed record SeedJuristvurdering(string Vurdering, string VurdertAvType, string Av, DateTimeOffset Tidspunkt);
    internal sealed record SeedForklartBort(string Begrunnelse, string Av, DateTimeOffset Tidspunkt);
    internal sealed record SeedGodkjenning(string Av, DateTimeOffset Tidspunkt);
}
