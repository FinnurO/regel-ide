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
        // [Ny, issue #385 AC12] Bindingene til generiske navneformer flagges etter samme regel.
        var bindinger = await FlaggBindingerAsync(db, gammelRettskildeId, nyeNoder, endretAv, ct);
        if (kanter.Count == 0) return bindinger;
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
        return antall + bindinger;
    }

    /// <summary>
    /// [Ny, issue #385 AC12, Johann 2026-10-16] «Hjemmel endret» på bindingene (<see cref="NavneformBindingEntitet"/>), som strukturkantene:
    /// en binding som gjelder VERKET den gamle versjonen hører til (#384), flagges når teksten i hjemmelen eller i paragrafspennet den
    /// gjelder, er endret i den nye versjonen. Et tomt spenn er hele rettskilden: [ENDRET, issue #390, Johanns tillegg 2026-10-23] da flagger
    /// bare en endring i en node der navneformen er TAGGET (før: enhver tekstendring i rettskilden, som ga for mye støy). Ingenting annet endres:
    /// bindingen oppløser som før og står i lista til faglig vurdering til den godkjennes på nytt.
    /// </summary>
    private static async Task<int> FlaggBindingerAsync(
        RegelIdeDbContext db, Guid gammelRettskildeId, IReadOnlyList<Nodeavtrykk> nyeNoder, string endretAv, CancellationToken ct)
    {
        var gammel = await db.Rettskilder.Where(r => r.Id == gammelRettskildeId).Select(r => new { r.Eli, r.VirksomhetId }).FirstOrDefaultAsync(ct);
        if (gammel is null) return 0;
        var iVerket = gammel.Eli is null
            ? db.NavneformBindinger.Where(b => b.RettskildeId == gammelRettskildeId)
            : db.NavneformBindinger.Where(b => db.Rettskilder.Any(r => r.Id == b.RettskildeId && r.Eli == gammel.Eli && r.VirksomhetId == gammel.VirksomhetId));
        var bindinger = await iVerket.Where(b => b.Entitetsstatus == "gjeldende" && b.Status != Strukturkanter.Avvist && b.HjemmelEndretTidspunkt == null)
            .ToListAsync(ct);
        if (bindinger.Count == 0) return 0;
        var gamle = await db.RettskildeNoder.Where(n => n.RettskildeId == gammelRettskildeId)
            .OrderBy(n => n.Sorteringsrekkefolge).Select(n => new { n.Eid, n.Overskrift, n.TekstHash, n.Sorteringsrekkefolge }).ToListAsync(ct);
        var gamleAvtrykk = gamle.Select(n => new Nodeavtrykk(n.Eid, n.Overskrift, n.TekstHash)).ToList();
        bool Endret(string eid) => !Deltre(gamleAvtrykk, eid).SequenceEqual(Deltre(nyeNoder, eid));
        var helEndret = !gamleAvtrykk.OrderBy(n => n.Eid, StringComparer.Ordinal).SequenceEqual(nyeNoder.OrderBy(n => n.Eid, StringComparer.Ordinal));
        var hjemmelIVerket = await db.Rettskilder.Where(r => r.Id == gammelRettskildeId || (gammel.Eli != null && r.Eli == gammel.Eli
            && r.VirksomhetId == gammel.VirksomhetId)).Select(r => r.Id).ToListAsync(ct);

        var antall = 0;
        foreach (var b in bindinger)
        {
            string? hvorfor = null;
            if (b.HjemmelEid is { } heid && b.HjemmelRettskildeId is { } hid && hjemmelIVerket.Contains(hid) && Endret(heid))
            {
                hvorfor = heid;
            }
            else
            {
                var spenn = JsonSerializer.Deserialize<List<ParagrafspennPar>>(b.ParagrafspennJson, JsonSerialiseringHjelper.Innstillinger) ?? [];
                if (spenn.Count == 0)
                {
                    // [ENDRET, issue #390, Johanns tillegg 2026-10-23 fra #385] Før: enhver tekstendring i rettskilden flagget, og det ga for
                    // mye støy. Nå bare når en node med en TAGGET forekomst av navneformen er endret (eller borte) i den nye versjonen. Taggene
                    // leses som de står i basen (AsNoTracking): det er forekomstene i versjonen bindingen ble vurdert mot. En endring i et ledd
                    // uten forekomst gir ikke flagg. En forekomst som ikke er tagget, kan ikke ses her — den flagger ikke (CLAUDE.md §8).
                    if (helEndret)
                    {
                        var taggedeNoder = await db.TekstTagger.AsNoTracking()
                            .Where(t => t.RefId == b.NavneformId && t.Kind == "virksomhet" && t.Entitetsstatus == "gjeldende"
                                        && hjemmelIVerket.Contains(t.RettskildeId))
                            .Select(t => t.NodeEid).Distinct().ToListAsync(ct);
                        var endretForekomst = taggedeNoder.OrderBy(e => e, StringComparer.Ordinal).FirstOrDefault(Endret);
                        if (endretForekomst is not null) hvorfor = endretForekomst;
                    }
                }
                else
                {
                    foreach (var par in spenn)
                    {
                        var fra = gamle.FindIndex(n => n.Eid == par.FraEid);
                        var til = par.TilEid is null ? fra : gamle.FindIndex(n => n.Eid == par.TilEid);
                        if (fra < 0 || til < 0) { hvorfor = par.FraEid; break; }
                        var (lo, hi) = (Math.Min(fra, til), Math.Max(fra, til));
                        var endret = gamle.Skip(lo).Take(hi - lo + 1).Select(n => n.Eid).FirstOrDefault(Endret);
                        if (endret is not null) { hvorfor = endret; break; }
                    }
                }
            }
            if (hvorfor is null) continue;
            b.HjemmelEndretTidspunkt = DateTimeOffset.UtcNow;
            var prov = ProveniensHjelper.NyRad("navneform_binding", b.Id, virksomhetId: null, "endret", endretAv);
            prov.KildeReferanserJson = JsonSerializer.Serialize(new
            {
                felt = "hjemmel_endret", issue = 385, endretI = hvorfor,
                merknad = "Teksten i hjemmelen eller paragrafspennet er endret i en ny versjon — vurder bindingen på nytt. Ingenting annet er endret.",
            });
            db.Proveniens.Add(prov);
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
