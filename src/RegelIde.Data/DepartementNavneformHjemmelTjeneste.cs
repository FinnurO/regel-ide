using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>Utfallet for ett levende departement i <see cref="DepartementNavneformHjemmelTjeneste"/>.</summary>
public static class NavneformHjemmelUtfall
{
    public const string Foreslatt = "foreslatt";
    /// <summary>Den godkjente gjeldende navneformen har alt hjemmel i navnevedtaket.</summary>
    public const string HarHjemmelINavnevedtaket = "har_hjemmel_i_navnevedtaket";
    public const string ForslagFinnes = "forslag_finnes";
    public const string TidligereAvvist = "tidligere_avvist";
    /// <summary>Navnet var i bruk før det eldste organiseringsvedtaket i korpuset — ingen navnevedtak å vise til.</summary>
    public const string UtenNavnevedtak = "uten_navnevedtak";
}

/// <summary>Ett levende departement: navnevedtaket, hjemmelen navneformen har i dag, og hva som ble (eller ville blitt) foreslått.</summary>
public sealed record NavneformHjemmelRad(
    Guid VirksomhetId, string Departement, string Utfall, Guid? GjeldendeNavneformId, string? GjeldendeTerm, string? GammelHjemmel,
    LivslopHjemmel? Navnevedtak, string? Kunnskapsstatus, Guid? ForslagId);

public sealed record NavneformHjemmelRapport(bool Skrevet, int LevendeIKatalogen, int Foreslatt, IReadOnlyList<NavneformHjemmelRad> Rader);

/// <summary>
/// [Ny, issue #394 AK 9, Johanns avgjørelse 3 på #395 2026-10-23] Navneformene for departementene får hjemmel i NAVNEVEDTAKET — vedtaket
/// som opprettet departementet eller ga det navnet det har i dag (<see cref="LivslopNavn.GittVed"/> for det siste navnet i livsløpet,
/// #395). Målt 2026-10-23: 13 av 16 levende departementer hadde ingen eller en tilfeldig hjemmel (Landbruks- og matdepartementet:
/// «Forskrift om avlsfremmende tiltak på bier» § 3).
/// <para>
/// <b>Bare forslag.</b> For hvert levende departement i katalogen der navnevedtaket ikke alt er hjemmelen, skrives en NY navneform med
/// samme term, grunn «gjeldende», hjemmel i navnevedtaket, <c>maskinelt_foreslatt</c> og <see cref="BegrepEntitet.ErstatterId"/> = den
/// godkjente gjeldende navneformen. Den gamle røres ikke nå: den settes som <c>erstattet</c> (og taggene flyttes til den nye) først når
/// forslaget godkjennes (<see cref="AktoridentitetTjeneste.GodkjennNavneformAsync"/>). En navneform er et modellutsagn med én hjemmel
/// (#385); å bytte hjemmelen på raden direkte ville vært å endre et godkjent utsagn uten at noen har vurdert det.
/// </para>
/// <para>
/// Kunnskapsstatus: <c>tekstfunn</c> når navnet står ordrett i vedtaksteksten, ellers <c>normalisering</c> (genitiv eller ubestemt form
/// gjort til bestemt grunnform) — samme regel som livsløpet bruker for de utgåtte navnene.
/// </para>
/// </summary>
public sealed class DepartementNavneformHjemmelTjeneste(RegelIdeDbContext db, DepartementLivslopTjeneste livslop)
{
    public const string OppdagelsesKilde = "monster:livslop-navnevedtak";

    public async Task<NavneformHjemmelRapport> KjorAsync(bool skriv, string bruker, CancellationToken ct = default)
    {
        var rapport = await livslop.KjorAsync(skriv: false, bruker, ct);
        var levende = rapport.Identiteter.Where(i => i.Nedlagt is null && i.KatalogVirksomhetId is not null).ToList();
        var titler = new Dictionary<Guid, string>();
        async Task<string> HjemmelTekstAsync(Guid? rid, string? eid)
        {
            if (rid is null) return eid is null ? "ingen hjemmel" : $"«{eid}» (uten rettskilde)";
            if (!titler.TryGetValue(rid.Value, out var t))
            {
                t = await db.Rettskilder.Where(r => r.Id == rid).Select(r => r.Kortnavn ?? r.Tittel).FirstOrDefaultAsync(ct) ?? "(ukjent rettskilde)";
                titler[rid.Value] = t;
            }
            return eid is null ? t : $"{t} ({eid})";
        }

        var rader = new List<NavneformHjemmelRad>();
        foreach (var i in levende)
        {
            var vid = i.KatalogVirksomhetId!.Value;
            var sist = i.Navn[^1];
            var former = await db.Begreper
                .Where(b => b.Begrepskategori == "virksomhet" && b.VirksomhetReferanseId == vid && b.Navneformgrunn == "gjeldende"
                            && (b.Entitetsstatus == "gjeldende" || b.Godkjenning == Strukturkanter.Avvist))
                .ToListAsync(ct);
            var gjeldende = former.FirstOrDefault(b => b.Entitetsstatus == "gjeldende"
                && (b.Godkjenning == null || b.Godkjenning == Strukturkanter.Godkjent));
            var gammel = gjeldende is null ? null : await HjemmelTekstAsync(gjeldende.LovkildeId, gjeldende.LovreferanseEid);
            NavneformHjemmelRad Rad(string utfall, string? ks = null, Guid? forslag = null) => new(vid, i.KatalogNavn ?? sist.Navn, utfall,
                gjeldende?.Id, gjeldende?.Term, gammel, sist.GittVed, ks, forslag);

            if (sist.GittVed is not { } h) { rader.Add(Rad(NavneformHjemmelUtfall.UtenNavnevedtak)); continue; }
            bool SammeHjemmel(BegrepEntitet b) => b.LovkildeId == h.RettskildeId && b.LovreferanseEid == h.Eid;
            if (gjeldende is not null && SammeHjemmel(gjeldende)) { rader.Add(Rad(NavneformHjemmelUtfall.HarHjemmelINavnevedtaket)); continue; }
            if (former.FirstOrDefault(b => b.Entitetsstatus == "gjeldende" && b.Godkjenning is { } g && Strukturkanter.Ventende.Contains(g)
                    && SammeHjemmel(b)) is { } venter)
            {
                rader.Add(Rad(NavneformHjemmelUtfall.ForslagFinnes, venter.Kunnskapsstatus, venter.Id));
                continue;
            }
            if (former.Any(b => b.Godkjenning == Strukturkanter.Avvist && SammeHjemmel(b)))
            {
                rader.Add(Rad(NavneformHjemmelUtfall.TidligereAvvist));
                continue;
            }

            // Termen er den godkjente navneformens (navnet katalogen alt viser); finnes ingen, livsløpets navn.
            var term = gjeldende?.Term ?? sist.Navn;
            var ordrett = h.Tekst.Contains(term, StringComparison.Ordinal);
            var ks = ordrett ? Strukturkanter.Tekstfunn : Strukturkanter.Normalisering;
            Guid? id = null;
            if (skriv)
            {
                var b = new BegrepEntitet
                {
                    Id = Guid.NewGuid(),
                    Begrepskategori = "virksomhet",
                    VirksomhetReferanseId = vid,
                    Navneformgrunn = "gjeldende",
                    Term = term,
                    Status = "publisert",
                    GyldigFra = sist.Fra,
                    LovkildeId = h.RettskildeId,
                    LovreferanseEid = h.Eid,
                    Kunnskapsstatus = ks,
                    Begrunnelse = (ordrett
                            ? $"Navnevedtaket: «{Avkort(h.Tekst)}»"
                            : $"Navnevedtaket: «{Avkort(h.Tekst)}» Navnet står i genitiv eller ubestemt form i vedtaket og er gjort til bestemt grunnform.")
                        + (gjeldende is null ? "" : $" Erstatter hjemmelen navneformen har i dag ({gammel}) når forslaget godkjennes."),
                    ErstatterId = gjeldende?.Id,
                    Godkjenning = Strukturkanter.MaskineltForeslatt,
                    OppdagelsesKilde = OppdagelsesKilde,
                    OpprettetAv = bruker,
                    OpprettetTidspunkt = DateTimeOffset.UtcNow,
                };
                db.Begreper.Add(b);
                db.Proveniens.Add(ProveniensHjelper.NyRad("begrep", b.Id, virksomhetId: null, "foreslatt", bruker));
                await db.SaveChangesAsync(ct);
                id = b.Id;
            }
            rader.Add(Rad(NavneformHjemmelUtfall.Foreslatt, ks, id));
        }
        return new NavneformHjemmelRapport(skriv, levende.Count, rader.Count(r => r.Utfall == NavneformHjemmelUtfall.Foreslatt), rader);
    }

    private static string Avkort(string s) => s.Length <= 160 ? s : s[..157] + "…";
}
