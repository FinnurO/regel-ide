using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>Én node i en tjenestereise-graf — enten en Tjeneste eller (valgfritt) en Handling under den.</summary>
public sealed record TjenestereiseNode(
    Guid Id, string Navn, bool ErHandling, string? Type, string? KompetentMyndighet,
    IReadOnlyList<string> Livshendelser, string? Status);

/// <summary>
/// Én kant. <see cref="ErHandlingTilhorighet"/> = tilhørighet fra en Tjeneste til EN AV SINE EGNE
/// handlinger — IKKE en ekte <see cref="TjenesteavhengighetEntitet"/>-relasjon, kun en synlig
/// container-kant i grafen (satt sammen her, ikke en egen Rel-verdi i <see cref="Rel"/> — den
/// forblir en av de 8 EKTE <c>GyldigeRel</c>-verdiene for ekte avhengigheter).
/// </summary>
public sealed record TjenestereiseKant(Guid FraId, Guid TilId, string Rel, bool ErHandlingTilhorighet);

public sealed record TjenestereiseGraf(IReadOnlyList<TjenestereiseNode> Noder, IReadOnlyList<TjenestereiseKant> Kanter);

/// <summary>
/// [Ny, 2026-08-28] Multi-hop traversering av tjenesteavhengigheter for en interaktiv graf-
/// visualisering (`Tjenestereise.tsx`) — ALLE eksisterende avhengighet-endepunkter
/// (<see cref="TjenesteavhengighetregisterTjeneste.HentForTjenesteAsync"/>) returnerer kun ÉN
/// tjenestes DIREKTE (1-hopp) kanter; denne bygger videre derfra opp til <c>dybde</c> hopp.
/// Enkel BFS med et besøkt-sett (samme grunnholdning som <see cref="TjenesteavhengighetregisterTjeneste
/// .LukkerSykelAsync"/> bruker ved skriving — en syklisk graf skal aldri gi uendelig traversering).
/// Eksterne referanser (<see cref="TjenesteavhengighetVisning.MotpartTjenesteId"/> null) kan IKKE
/// traverseres videre — de vises ikke som egne noder i v1 (ingen ekte Tjeneste-rad å hente data fra).
/// </summary>
public sealed class TjenestereiseGrafTjeneste(
    RegelIdeDbContext db, TjenesteavhengighetregisterTjeneste avhengighetregister, HandlingTjenesteregisterTjeneste handlingTjenesteregister)
{
    /// <summary>Hardkodet øvre grense — ingen ubegrenset traversering, samme "ingen gjettet fallback"-
    /// holdning: et dybdetak er en bevisst grense, ikke en glemt en.</summary>
    public const int MaksDybde = 5;

    public async Task<TjenestereiseGraf?> ByggAsync(
        Guid sentrumId, int dybde, bool inkluderHandlinger, string? livshendelseFilter, CancellationToken ct = default)
    {
        dybde = Math.Clamp(dybde, 1, MaksDybde);
        var sentrum = await db.Tjenester.FirstOrDefaultAsync(t => t.Id == sentrumId && t.Entitetsstatus == "gjeldende", ct);
        if (sentrum is null) return null;

        var besokt = new Dictionary<Guid, TjenesteEntitet> { [sentrum.Id] = sentrum };
        var kanter = new List<TjenestereiseKant>();
        var frontier = new List<Guid> { sentrum.Id };

        for (var hopp = 0; hopp < dybde && frontier.Count > 0; hopp++)
        {
            var nesteFrontier = new List<Guid>();
            foreach (var id in frontier)
            {
                var avhengigheter = await avhengighetregister.HentForTjenesteAsync(id, ct);
                foreach (var a in avhengigheter)
                {
                    if (a.MotpartTjenesteId is not { } motpartId) continue;
                    var (fraId, tilId) = a.Retning == "fra" ? (id, motpartId) : (motpartId, id);
                    if (!kanter.Any(k => k.FraId == fraId && k.TilId == tilId && k.Rel == a.Rel))
                    {
                        kanter.Add(new TjenestereiseKant(fraId, tilId, a.Rel, ErHandlingTilhorighet: false));
                    }
                    if (!besokt.ContainsKey(motpartId))
                    {
                        var motpart = await db.Tjenester.FirstOrDefaultAsync(t => t.Id == motpartId && t.Entitetsstatus == "gjeldende", ct);
                        if (motpart is null) continue; // slettet siden avhengigheten ble opprettet — hopp over, ikke gjett
                        besokt[motpartId] = motpart;
                        nesteFrontier.Add(motpartId);
                    }
                }
            }
            frontier = nesteFrontier;
        }

        // Livshendelse-filter: fjerner noder (og enhver kant som berører dem) som ikke matcher —
        // sentrum selv beholdes alltid, uansett filter, siden det ville vært meningsløst å filtrere
        // vekk noden brukeren faktisk ba om å se fra.
        var synligeIder = string.IsNullOrWhiteSpace(livshendelseFilter)
            ? besokt.Keys.ToHashSet()
            : besokt.Where(kv => kv.Key == sentrum.Id || kv.Value.Livshendelser.Contains(livshendelseFilter)).Select(kv => kv.Key).ToHashSet();

        var noder = synligeIder.Select(id => besokt[id])
            .Select(t => new TjenestereiseNode(t.Id, t.Tittel, false, t.Type, t.KompetentMyndighet, t.Livshendelser, t.Status))
            .ToList();
        var synligeKanter = kanter.Where(k => synligeIder.Contains(k.FraId) && synligeIder.Contains(k.TilId)).ToList();

        if (inkluderHandlinger)
        {
            foreach (var id in synligeIder)
            {
                var handlinger = await handlingTjenesteregister.HentForTjenesteAsync(id, ct);
                foreach (var h in handlinger)
                {
                    noder.Add(new TjenestereiseNode(h.Id, h.Navn, true, h.Handlingstype, null, [], h.Status));
                    synligeKanter.Add(new TjenestereiseKant(id, h.Id, "har_handling", ErHandlingTilhorighet: true));
                }
            }
        }

        return new TjenestereiseGraf(noder, synligeKanter);
    }

    /// <summary>Til livshendelse-filter-nedtrekket — ingen kodeliste finnes (fri `text[]`, se
    /// TjenesteEntitet.Livshendelser), så mulige verdier avledes fra faktisk lagret data.</summary>
    public async Task<List<string>> DistinkteLivshendelserAsync(CancellationToken ct = default) =>
        await db.Tjenester
            .Where(t => t.Entitetsstatus == "gjeldende")
            .SelectMany(t => t.Livshendelser)
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync(ct);
}
