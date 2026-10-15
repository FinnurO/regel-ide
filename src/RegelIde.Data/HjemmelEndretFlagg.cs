using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #367 AC4, docs/34 §5] «Hjemmel endret, vurder på nytt»: når hjemmelnoden en strukturkant peker på
/// (<see cref="StrukturkantEntitet.HjemmelNodeId"/>), erstattes av en ny versjon med ANNEN tekst, settes
/// <see cref="StrukturkantEntitet.HjemmelEndretTidspunkt"/> og en proveniensrad skrives. Ingenting annet endres: kanten peker fortsatt
/// på versjonen den ble vurdert mot, og kunnskapsstatus og godkjenning står — flagget sender den til faglig vurdering.
/// <para>
/// <b>Hva «endret» betyr:</b> nodens deltre (noden selv og alle noder med eId under den) sammenlignet på eId, overskrift og
/// teksthash. En ny konsolidert versjon der akkurat denne bestemmelsen er uendret, flagger ikke (Lovdata-resynken lager et helt nytt
/// nodetre for hver versjon, så «ny node» alene ville flagget alt). Forsvinner eId-en, er det en endring.
/// </para>
/// Kalles fra <see cref="RettskildeImportTjeneste"/> (ny versjon av en lov/forskrift) og <see cref="HandbokForfatterTjeneste"/>
/// (ny versjon av en kommentarseksjon) før <c>SaveChangesAsync</c>; kalleren lagrer.
/// </summary>
public static class HjemmelEndretFlagg
{
    /// <summary>Én node i et deltre-sammenligningsgrunnlag.</summary>
    public sealed record Nodeavtrykk(string Eid, string? Overskrift, string? TekstHash);

    /// <summary>Ny versjon av en hel rettskilde: flagger kantene som peker på en node i <paramref name="gammelRettskildeId"/> der
    /// deltreet er endret i <paramref name="nyeNoder"/>.</summary>
    /// <returns>Antall kanter som ble flagget.</returns>
    public static async Task<int> FlaggForNyVersjonAsync(
        RegelIdeDbContext db, Guid gammelRettskildeId, IReadOnlyList<Nodeavtrykk> nyeNoder, string endretAv, CancellationToken ct)
    {
        var kanter = await db.Strukturkanter
            .Where(k => k.HjemmelNodeId != null && k.HjemmelEndretTidspunkt == null
                        && db.RettskildeNoder.Any(n => n.Id == k.HjemmelNodeId && n.RettskildeId == gammelRettskildeId))
            .ToListAsync(ct);
        if (kanter.Count == 0) return 0;
        var gamle = await db.RettskildeNoder.Where(n => n.RettskildeId == gammelRettskildeId)
            .Select(n => new { n.Id, n.Eid, n.Overskrift, n.TekstHash }).ToListAsync(ct);
        var gammelEid = gamle.ToDictionary(n => n.Id, n => n.Eid);
        var gamleAvtrykk = gamle.Select(n => new Nodeavtrykk(n.Eid, n.Overskrift, n.TekstHash)).ToList();
        var antall = 0;
        foreach (var kant in kanter)
        {
            var eid = gammelEid[kant.HjemmelNodeId!.Value];
            if (Deltre(gamleAvtrykk, eid).SequenceEqual(Deltre(nyeNoder, eid))) continue;
            Flagg(db, kant, endretAv, eid);
            antall++;
        }
        return antall;
    }

    /// <summary>Ny versjon av én node (håndbokrevisjon): flagger kantene som peker på <paramref name="gammelNodeId"/> når teksten eller
    /// overskriften er endret.</summary>
    public static async Task<int> FlaggForNodeAsync(
        RegelIdeDbContext db, RettskildeNodeEntitet gammel, RettskildeNodeEntitet ny, string endretAv, CancellationToken ct)
    {
        if (gammel.TekstHash == ny.TekstHash && gammel.Overskrift == ny.Overskrift) return 0;
        var kanter = await db.Strukturkanter.Where(k => k.HjemmelNodeId == gammel.Id && k.HjemmelEndretTidspunkt == null).ToListAsync(ct);
        foreach (var kant in kanter) Flagg(db, kant, endretAv, gammel.Eid);
        return kanter.Count;
    }

    private static IEnumerable<Nodeavtrykk> Deltre(IEnumerable<Nodeavtrykk> noder, string eid) =>
        noder.Where(n => n.Eid == eid || n.Eid.StartsWith(eid + "/", StringComparison.Ordinal))
            .OrderBy(n => n.Eid, StringComparer.Ordinal);

    private static void Flagg(RegelIdeDbContext db, StrukturkantEntitet kant, string endretAv, string eid)
    {
        kant.HjemmelEndretTidspunkt = DateTimeOffset.UtcNow;
        var prov = ProveniensHjelper.NyRad(StrukturkantTjeneste.ProveniensType, kant.Id, virksomhetId: null, "endret", endretAv);
        prov.KildeReferanserJson = JsonSerializer.Serialize(new
        {
            felt = "hjemmel_endret", issue = 367, hjemmelEid = eid,
            merknad = "Hjemmelens tekst er endret i en ny versjon — vurder kanten på nytt. Ingenting annet er endret.",
        });
        db.Proveniens.Add(prov);
    }
}
