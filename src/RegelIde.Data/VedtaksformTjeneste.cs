using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>Én kilde i rapporten fra <see cref="VedtaksformTjeneste.KlassifiserAsync"/> (bare med <c>medRader</c>).</summary>
public sealed record VedtaksformRad(
    Guid RettskildeId, string? DokumentId, string Tittel, string Klasse, string Grunnlag, string IkraftStatus, string IkraftGrunnlag,
    bool? RentEndringsvedtakFor, bool? UttomtVirkning, IReadOnlyDictionary<string, int> PerRolle);

/// <summary>Rapporten fra en kjøring. <see cref="Overgang"/> er «dagens rent_endringsvedtak → ny klasse».</summary>
public sealed record VedtaksformRapport(
    bool Skrevet, string Regelversjon, int Kilder, IReadOnlyDictionary<string, int> PerKlasse,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> PerPrefiks, IReadOnlyDictionary<string, int> PerIkraftStatus,
    IReadOnlyDictionary<string, int> ReneEtterIkraftStatus, int IkraftFulgtIKorpus, IReadOnlyDictionary<string, int> Overgang,
    IReadOnlyDictionary<string, int> UttomtVirkning, IReadOnlyDictionary<string, int> NoderPerRolle, int KilderEndret, int NoderEndret,
    IReadOnlyList<VedtaksformRad> Rader);

/// <summary>
/// [Ny, issue #446 PR 1, prosjekteierens avgjørelse 2026-11-25] Setter vedtaksformen (<see cref="RettskildeEntitet.Vedtaksform"/>), ikraftstatusen
/// (<see cref="RettskildeEntitet.IkraftStatus"/>) og rollen per ledd (<see cref="RettskildeNodeEntitet.Endringsrolle"/>) for gjeldende, primære,
/// delte lover og forskrifter, med regelen i <see cref="Vedtaksform"/>. Samme populasjon som <see cref="DisposisjonstypeTjeneste"/>.
/// <para>
/// <b>Ikraftstatusen</b> leses av Lovdatas «I kraft fra» (<see cref="Vedtaksform.IkraftFraFelt"/>). Når feltet har en del uten dato («Kongen
/// bestemmer»), følges en ikraftsetting i korpus (juristrunden D8): et rent ikrafttredelsesvedtak som har kilden som hjemmel i hodet (basedOn), og
/// som setter HELE loven i kraft på en passert dato (<see cref="Vedtaksform.HelIkraftsetting"/>). Ellers står den som ukjent — ingen gjetting.
/// </para>
/// <para>
/// <b>PR 1 skriver ikke <c>rent_endringsvedtak</c>.</b> Rapporten viser hva den avledede verdien (<see cref="Vedtaksform.UttomtVirkning"/>) ville
/// blitt; koblingen til #472, #429 og sveipeutvalget er PR 2. Idempotent: en ny kjøring endrer ingenting. Tørrkjøring er standard.
/// </para>
/// </summary>
public sealed partial class VedtaksformTjeneste(RegelIdeDbContext db)
{
    private const int Batch = 100;

    [GeneratedRegex(@"(^|-)punkt-\d+-ledd-1$")] private static partial Regex ForsteLeddIPunkt();

    public async Task<VedtaksformRapport> KlassifiserAsync(
        bool skriv, Guid? rettskildeId = null, bool medRader = false, DateOnly? idag = null, CancellationToken ct = default)
    {
        var dag = idag ?? DateOnly.FromDateTime(DateTime.Now);
        var kilder = await db.Rettskilder.AsNoTracking()
            .Where(r => r.Importrolle == "primaer" && r.Entitetsstatus == "gjeldende" && r.VirksomhetId == null
                        && (r.Kildetype == "Lov" || r.Kildetype == "Forskrift"))
            .Where(r => rettskildeId == null || r.Id == rettskildeId)
            .Select(r => new
            {
                r.Id, r.DokumentId, r.Tittel, r.IkrafttredelseRaa, r.Ikrafttredelse, r.RentEndringsvedtak,
                r.Vedtaksform, r.VedtaksformGrunnlag, r.VedtaksformKunnskapsstatus, r.VedtaksformRegelversjon, r.IkraftStatus, r.IkraftStatusGrunnlag,
            })
            .ToListAsync(ct);
        if (rettskildeId is not null && kilder.Count == 0)
        {
            throw new ArgumentException($"Fant ingen gjeldende, primær, delt lov eller forskrift med id '{rettskildeId}'. Ingen gjettet fallback.");
        }
        var ider = kilder.Select(k => k.Id).ToList();
        var tittelPerId = kilder.ToDictionary(k => k.Id, k => k.Tittel);
        var endrer = (await db.RettskildeEndringer.AsNoTracking().GroupBy(e => e.RettskildeId).Select(g => new { g.Key, Antall = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.Antall);

        // ---- 1. Formen, kilde for kilde (rå HTML i porsjoner) ----
        var resultat = new Dictionary<Guid, VedtaksformResultat>();
        foreach (var bit in ider.Chunk(Batch))
        {
            var innhold = await db.Rettskilder.AsNoTracking().Where(r => bit.Contains(r.Id)).Select(r => new { r.Id, r.Innhold }).ToListAsync(ct);
            foreach (var i in innhold)
            {
                var html = i.Innhold is null ? null : Encoding.UTF8.GetString(i.Innhold);
                resultat[i.Id] = Vedtaksform.Klassifiser(tittelPerId[i.Id], html, endrer.GetValueOrDefault(i.Id));
            }
        }

        // ---- 2. Ikraftstatusen, med ikraftsettinger i korpus ----
        var ikraftsettinger = new Dictionary<Guid, List<Guid>>();
        var reneIkraft = resultat.Where(r => r.Value.Klasse == Vedtaksform.RentIkrafttredelsesvedtak).Select(r => r.Key).ToList();
        if (reneIkraft.Count > 0)
        {
            foreach (var h in await db.RettskildeHjemler.AsNoTracking().Where(h => reneIkraft.Contains(h.RettskildeId))
                         .Select(h => new { h.RettskildeId, h.HjemmelRettskildeId }).ToListAsync(ct))
            {
                if (!ikraftsettinger.TryGetValue(h.HjemmelRettskildeId, out var l)) ikraftsettinger[h.HjemmelRettskildeId] = l = [];
                l.Add(h.RettskildeId);
            }
        }
        // Rene ikrafttredelsesvedtak utenfor kjøringen (en kjøring for én kilde) leses også.
        var trengs = ikraftsettinger.Values.SelectMany(v => v).Where(v => !resultat.ContainsKey(v)).Distinct().ToList();
        var dokid = kilder.ToDictionary(k => k.Id, k => k.DokumentId);
        if (rettskildeId is not null)
        {
            var mulige = await db.RettskildeHjemler.AsNoTracking().Where(h => h.HjemmelRettskildeId == rettskildeId).Select(h => h.RettskildeId).ToListAsync(ct);
            foreach (var m in await db.Rettskilder.AsNoTracking().Where(r => mulige.Contains(r.Id) && r.Entitetsstatus == "gjeldende")
                         .Select(r => new { r.Id, r.DokumentId, r.Tittel, r.Innhold }).ToListAsync(ct))
            {
                var res = Vedtaksform.Klassifiser(m.Tittel, m.Innhold is null ? null : Encoding.UTF8.GetString(m.Innhold), endrer.GetValueOrDefault(m.Id));
                if (res.Klasse != Vedtaksform.RentIkrafttredelsesvedtak) continue;
                resultat.TryAdd(m.Id, res);
                dokid.TryAdd(m.Id, m.DokumentId);
                if (!ikraftsettinger.TryGetValue(rettskildeId.Value, out var l)) ikraftsettinger[rettskildeId.Value] = l = [];
                if (!l.Contains(m.Id)) l.Add(m.Id);
            }
        }

        var ikraft = new Dictionary<Guid, (string Status, string Grunnlag)>();
        var fulgt = 0;
        foreach (var k in kilder)
        {
            var (status, grunnlag) = Vedtaksform.IkraftFraFelt(k.IkrafttredelseRaa, k.Ikrafttredelse, dag);
            if (status == Vedtaksform.IkraftUkjent && ikraftsettinger.TryGetValue(k.Id, out var kandidater))
            {
                foreach (var kand in kandidater.Where(resultat.ContainsKey))
                {
                    if (Vedtaksform.HelIkraftsetting(resultat[kand], dag) is { } hel)
                    {
                        status = Vedtaksform.IkraftJa;
                        grunnlag = $"{grunnlag} Satt i kraft {hel.Dato:yyyy-MM-dd} ved {dokid.GetValueOrDefault(kand) ?? kand.ToString()}: «{hel.Sitat}».";
                        fulgt++;
                        break;
                    }
                }
            }
            ikraft[k.Id] = (status, grunnlag);
        }

        // ---- 3. Rapport og skriving ----
        var perKlasse = Vedtaksform.Klasser.ToDictionary(k => k, _ => 0);
        var perPrefiks = new SortedDictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var perIkraft = Vedtaksform.Ikraftstatuser.ToDictionary(k => k, _ => 0);
        var reneIkraftStatus = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var overgang = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var uttomt = new Dictionary<string, int> { ["true"] = 0, ["false"] = 0, ["null"] = 0 };
        var noderPerRolle = Vedtaksform.LagredeRoller.ToDictionary(r => r, _ => 0);
        var rader = new List<VedtaksformRad>();
        int kilderEndret = 0, noderEndret = 0;
        foreach (var k in kilder)
        {
            var r = resultat[k.Id];
            var (status, ikraftGrunnlag) = ikraft[k.Id];
            perKlasse[r.Klasse]++;
            var prefiks = (k.DokumentId ?? "(ingen)").Split('/')[0];
            if (!perPrefiks.TryGetValue(prefiks, out var pp)) perPrefiks[prefiks] = pp = Vedtaksform.Klasser.ToDictionary(x => x, _ => 0);
            pp[r.Klasse]++;
            perIkraft[status]++;
            if (Vedtaksform.Rene.Contains(r.Klasse)) reneIkraftStatus[$"{r.Klasse}:{status}"] = reneIkraftStatus.GetValueOrDefault($"{r.Klasse}:{status}") + 1;
            var forKey = k.RentEndringsvedtak switch { true => "true", false => "false", null => "NULL" };
            overgang[$"{forKey} → {r.Klasse}"] = overgang.GetValueOrDefault($"{forKey} → {r.Klasse}") + 1;
            var u = Vedtaksform.UttomtVirkning(r.Klasse, status);
            uttomt[u switch { true => "true", false => "false", null => "null" }]++;
            var roller = NodeRoller(r);
            foreach (var rolle in roller.Values) noderPerRolle[rolle]++;
            if (medRader)
            {
                rader.Add(new VedtaksformRad(k.Id, k.DokumentId, k.Tittel, r.Klasse, r.Grunnlag, status, ikraftGrunnlag, k.RentEndringsvedtak, u, r.PerRolle));
            }

            var endret = k.Vedtaksform != r.Klasse || k.VedtaksformGrunnlag != r.Grunnlag || k.VedtaksformKunnskapsstatus != r.Kunnskapsstatus
                         || k.VedtaksformRegelversjon != Vedtaksform.Regelversjon || k.IkraftStatus != status || k.IkraftStatusGrunnlag != ikraftGrunnlag;
            if (endret) kilderEndret++;
            if (!skriv) continue;
            if (endret)
            {
                await db.Rettskilder.Where(x => x.Id == k.Id).ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Vedtaksform, r.Klasse)
                    .SetProperty(x => x.VedtaksformGrunnlag, r.Grunnlag)
                    .SetProperty(x => x.VedtaksformKunnskapsstatus, r.Kunnskapsstatus)
                    .SetProperty(x => x.VedtaksformRegelversjon, Vedtaksform.Regelversjon)
                    .SetProperty(x => x.IkraftStatus, status)
                    .SetProperty(x => x.IkraftStatusGrunnlag, ikraftGrunnlag), ct);
            }
            noderEndret += await SkrivNoderollerAsync(k.Id, roller, ct);
        }

        return new VedtaksformRapport(skriv, Vedtaksform.Regelversjon, kilder.Count, perKlasse,
            perPrefiks.ToDictionary(p => p.Key, p => (IReadOnlyDictionary<string, int>)p.Value), perIkraft, reneIkraftStatus, fulgt, overgang, uttomt,
            noderPerRolle, kilderEndret, noderEndret, rader);
    }

    /// <summary>Rollene per node, nøkkel = Lovdatas id slik noden har den (<c>kilde_id</c>). Et punkt i en liste har id-en til listeelementet
    /// (<c>…-punkt-2</c>), mens teksten står i <c>…-punkt-2-ledd-1</c>. Eget innhold lagres ikke.</summary>
    public static Dictionary<string, string> NodeRoller(VedtaksformResultat r)
    {
        var ut = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var e in r.Enheter.Where(e => e.HtmlId is not null && Vedtaksform.LagredeRoller.Contains(e.Rolle)))
        {
            ut.TryAdd(e.HtmlId!, e.Rolle);
            if (ForsteLeddIPunkt().IsMatch(e.HtmlId!)) ut.TryAdd(e.HtmlId![..^"-ledd-1".Length], e.Rolle);
        }
        return ut;
    }

    private async Task<int> SkrivNoderollerAsync(Guid rettskildeId, Dictionary<string, string> roller, CancellationToken ct)
    {
        var kid = roller.Keys.ToArray();
        var rolle = kid.Select(k => roller[k]).ToArray();
        var satt = kid.Length == 0 ? 0 : await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE rettskilde_noder n SET endringsrolle = v.rolle
            FROM (SELECT unnest({kid}::text[]) AS kid, unnest({rolle}::text[]) AS rolle) v
            WHERE n.rettskilde_id = {rettskildeId} AND n.entitetsstatus = 'gjeldende' AND n.kilde_id = v.kid
              AND n.endringsrolle IS DISTINCT FROM v.rolle
            """, ct);
        var fjernet = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE rettskilde_noder SET endringsrolle = NULL
            WHERE rettskilde_id = {rettskildeId} AND entitetsstatus = 'gjeldende' AND endringsrolle IS NOT NULL AND NOT (kilde_id = ANY({kid}::text[]))
            """, ct);
        return satt + fjernet;
    }
}
