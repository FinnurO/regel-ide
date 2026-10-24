using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>Én klassifisert kilde i rapporten fra <see cref="DisposisjonstypeTjeneste.KlassifiserAsync"/>.</summary>
public sealed record DisposisjonstypeRad(Guid RettskildeId, string Tittel, string? Kildeform, IReadOnlyList<Disposisjonstreff> Treff);

/// <summary>Rapporten fra en klassifiseringskjøring. <see cref="PerType"/> teller kilder (ikke rader) per type; <see cref="Uklassifisert"/>
/// er kildene uten noen type. <see cref="Rader"/> er bare med når kalleren ba om dem.</summary>
public sealed record DisposisjonstypeRapport(
    bool Skrevet, int Kilder, int Uklassifisert, IReadOnlyDictionary<string, int> PerType, IReadOnlyDictionary<string, int> PerKilde,
    int RaderLagtTil, int RaderFjernet, IReadOnlyList<DisposisjonstypeRad> Rader, int ReneEndringsvedtak = 0);

/// <summary>
/// [Ny, issue #395 del 1, 2026-10-23] Klassifiserer rettskildene etter disposisjonstype med de deterministiske reglene i
/// <see cref="Disposisjonstyper"/> og lagrer treffene i <see cref="RettskildeDisposisjonstypeEntitet"/>. Bare delte (nasjonale), primære,
/// gjeldende lover og forskrifter — det er dem Lovdata-titlene og vedtaksformen gjelder for.
/// <para>
/// <b>Idempotent og ikke-destruktiv for mennesker.</b> En kjøring erstatter bare maskinens egne rader (kilde <c>tittel</c> og
/// <c>monster</c>) for kilden den klassifiserer: rader som ikke lenger slår til, fjernes, nye legges til, uendrede står. Rader med kilde
/// <c>manuell</c> eller <c>ki</c> røres aldri. Tørrkjøring (<c>skriv = false</c>) gir samme rapport uten å skrive.
/// </para>
/// </summary>
public sealed class DisposisjonstypeTjeneste(RegelIdeDbContext db)
{
    public const string SystemBruker = "system-disposisjonstype";

    public async Task<DisposisjonstypeRapport> KlassifiserAsync(
        bool skriv, Guid? rettskildeId = null, bool medRader = false, string bruker = SystemBruker, CancellationToken ct = default)
    {
        var kilder = await db.Rettskilder
            .Where(r => r.Importrolle == "primaer" && r.Entitetsstatus == "gjeldende" && r.VirksomhetId == null
                        && (r.Kildetype == "Lov" || r.Kildetype == "Forskrift"))
            .Where(r => rettskildeId == null || r.Id == rettskildeId)
            .Select(r => new { r.Id, r.Tittel, r.Kildeform })
            .ToListAsync(ct);
        // [Ny, issue #395 avgjørelse 6] Hvor mange dokumenter hver kilde endrer (changesToDocuments), for markøren for rene endringsvedtak.
        var endrer = (await db.RettskildeEndringer.Where(e => rettskildeId == null || e.RettskildeId == rettskildeId)
                .GroupBy(e => e.RettskildeId).Select(g => new { g.Key, Antall = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.Antall);
        var reneEndringer = 0;
        if (rettskildeId is not null && kilder.Count == 0)
        {
            throw new ArgumentException($"Fant ingen gjeldende, primær, delt lov eller forskrift med id '{rettskildeId}'. Ingen gjettet fallback.");
        }

        var perType = Disposisjonstyper.Alle.ToDictionary(t => t, _ => 0);
        var perKilde = new Dictionary<string, int> { [Disposisjonstyper.KildeTittel] = 0, [Disposisjonstyper.KildeMonster] = 0 };
        var rader = new List<DisposisjonstypeRad>();
        int uklassifisert = 0, lagtTil = 0, fjernet = 0;

        var eksisterendeAlle = await db.RettskildeDisposisjonstyper
            .Where(d => d.Kilde == Disposisjonstyper.KildeTittel || d.Kilde == Disposisjonstyper.KildeMonster)
            .Where(d => rettskildeId == null || d.RettskildeId == rettskildeId)
            .ToListAsync(ct);
        var eksisterendePerKilde = eksisterendeAlle.GroupBy(d => d.RettskildeId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var k in kilder)
        {
            var treff = await KlassifiserEnAsync(k.Id, k.Tittel, ct);
            if (treff.Count == 0) uklassifisert++;
            foreach (var type in treff.Select(t => t.Disposisjonstype).Distinct()) perType[type]++;
            foreach (var t in treff) perKilde[t.Kilde]++;
            if (medRader) rader.Add(new DisposisjonstypeRad(k.Id, k.Tittel, k.Kildeform, treff));

            // [Ny, issue #395 avgjørelse 6] Rent endringsvedtak: BÅDE hodet sier at kilden endrer andre dokumenter, OG tittelen har formen
            // «Forskrift/Lov om endring(er) i …», OG ingen organiseringstype slo til. To uavhengige signaler fra kilden; tittelen alene er ikke nok.
            var antallEndret = endrer.GetValueOrDefault(k.Id);
            var erRen = antallEndret > 0 && Disposisjonstyper.ErRentEndringsvedtak(k.Tittel)
                && !treff.Any(t => Disposisjonstyper.Organiseringstyper.Contains(t.Disposisjonstype));
            if (erRen) reneEndringer++;
            if (skriv)
            {
                var rk = await db.Rettskilder.FirstAsync(r => r.Id == k.Id, ct);
                rk.RentEndringsvedtak = erRen ? true : null;
                rk.RentEndringsvedtakGrunnlag = erRen
                    ? $"Regel: rent-endringsvedtak. Endrer {antallEndret} dokument(er) (changesToDocuments); tittelform «… om endring(er) i …»; ingen organiseringstype."
                    : null;
            }

            var gamle = eksisterendePerKilde.GetValueOrDefault(k.Id) ?? [];
            foreach (var g in gamle.Where(g => !treff.Any(t => t.Disposisjonstype == g.Disposisjonstype && t.Kilde == g.Kilde)))
            {
                fjernet++;
                if (skriv) db.RettskildeDisposisjonstyper.Remove(g);
            }
            foreach (var t in treff.Where(t => !gamle.Any(g => g.Disposisjonstype == t.Disposisjonstype && g.Kilde == t.Kilde)))
            {
                lagtTil++;
                if (skriv)
                {
                    db.RettskildeDisposisjonstyper.Add(new RettskildeDisposisjonstypeEntitet
                    {
                        Id = Guid.NewGuid(),
                        RettskildeId = k.Id,
                        Disposisjonstype = t.Disposisjonstype,
                        Kilde = t.Kilde,
                        Regel = t.Regel,
                        Treff = t.Treff,
                        NodeEid = t.NodeEid,
                        // Tittel- og mønsterreglene normaliserer ordformen etter en navngitt regel (docs/33 §4.6).
                        Kunnskapsstatus = Strukturkanter.Normalisering,
                        OpprettetAv = bruker,
                        OpprettetTidspunkt = DateTimeOffset.UtcNow,
                    });
                }
            }
        }
        if (skriv) await db.SaveChangesAsync(ct);

        return new DisposisjonstypeRapport(skriv, kilder.Count, uklassifisert, perType, perKilde, lagtTil, fjernet, rader, reneEndringer);
    }

    /// <summary>Tittelreglene, og innholdsmønstrene der tittelen ikke avgjør (<see cref="Disposisjonstyper.SkalKjoreInnholdsmonstre"/>).</summary>
    public async Task<IReadOnlyList<Disposisjonstreff>> KlassifiserEnAsync(Guid rettskildeId, string tittel, CancellationToken ct = default)
    {
        var tittelTreff = Disposisjonstyper.KlassifiserTittel(tittel);
        if (!Disposisjonstyper.SkalKjoreInnholdsmonstre(tittelTreff)) return tittelTreff;

        var noder = await db.RettskildeNoder
            .Where(n => n.RettskildeId == rettskildeId && n.Entitetsstatus == "gjeldende" && !n.Opphevet && n.Tekst != null)
            .OrderBy(n => n.Sorteringsrekkefolge)
            .Select(n => new { n.Eid, n.Tekst })
            .ToListAsync(ct);
        // En uklassifisert tittel får innholdsmønstrene bare når kilden er kort (se Disposisjonstyper.MaksNoderForInnholdsmonstre).
        if (tittelTreff.Count == 0 && noder.Count > Disposisjonstyper.MaksNoderForInnholdsmonstre) return tittelTreff;
        var innhold = Disposisjonstyper.KlassifiserInnhold(noder.Select(n => (n.Eid, n.Tekst!)));
        return [.. tittelTreff, .. innhold.Where(i => tittelTreff.All(t => t.Disposisjonstype != i.Disposisjonstype))];
    }

    /// <summary>Typene per rettskilde for listevisningen (alle metoder). Tom ordbok-oppføring = uklassifisert.</summary>
    public async Task<Dictionary<Guid, List<string>>> TyperPerRettskildeAsync(CancellationToken ct = default) =>
        (await db.RettskildeDisposisjonstyper.Select(d => new { d.RettskildeId, d.Disposisjonstype }).ToListAsync(ct))
            .GroupBy(d => d.RettskildeId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Disposisjonstype).Distinct()
                .OrderBy(t => Disposisjonstyper.Alle.ToList().IndexOf(t)).ToList());
}
