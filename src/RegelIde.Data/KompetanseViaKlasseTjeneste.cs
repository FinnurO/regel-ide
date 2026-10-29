using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>[Ny, issue #418] Én kompetanse aktøren har (eller som andre har overfor den) fordi den er medlem av en klasse eller har en rolle.
/// <see cref="Retning"/>: <c>har</c> (K fra klassen/rollen) | <c>overfor</c> (K til klassen/rollen). <see cref="Via"/> er klassene og rollene
/// i veien, i rekkefølge. <see cref="Kunnskapsstatus"/> er <see cref="SvakestePremiss.SvarStatus"/> av det svakeste premisset — aldri sterkere
/// enn medlemskapet, rolleinnehavet eller K-kanten.</summary>
public sealed record KompetanseViaKlasseTreff(
    StrukturkantVisning Kant, string Retning, IReadOnlyList<KantnodeVisning> Via, string Kunnskapsstatus, IReadOnlyList<Guid> Premisser,
    Guid? SvakestePremissId, string? SvakestePremissStatus, bool Forslag);

/// <summary>[Ny, issue #418] Svaret på «hvilken kompetanse har aktøren via klasser og roller?». <see cref="Premisser"/> er alle kantene svarene
/// bygger på, én gang hver. <see cref="Hull"/> sier hva som ikke er avgjort (klasser uten registrert eller utledet medlemskap).</summary>
public sealed record KompetanseViaKlasse(
    KantnodeVisning Aktor, IReadOnlyList<KompetanseViaKlasseTreff> Treff, IReadOnlyList<StrukturkantVisning> Premisser, IReadOnlyList<string> Hull);

/// <summary>
/// [Ny, issue #418, Johanns avgjørelser (a) og (b) 2026-10-29, docs/33 §4.3 og §4.6] K kan ha en klasse som innehaver, lest distributivt: hvert
/// medlem av klassen har kompetansen. Rollen «lokal barnehagemyndighet» er registrert som rolle klassen kommune har (I fra klasse). Denne
/// tjenesten svarer for ÉN aktør: «hvilken kompetanse har Alta kommune?» — K-kantene fra klassene aktøren er medlem av, og fra rollene aktøren
/// eller klassene har, og (motsatt retning) K-kantene som har en slik klasse eller rolle som motpart.
/// <para>
/// <b>Ingenting lagres (#345).</b> Svaret er avledet: premissene er medlemskapet (utledet via territoriekanten for klassen «kommune», #384, eller
/// registrerte M-kanter, også klasse i klasse), rolleinnehavet (I) og K-kanten selv.
/// </para>
/// <para>
/// <b>Svakeste premiss (docs/33 §4.6)</b> håndheves her som i tilhørigheten (#390): svaret er <c>avledet</c> bare når det svakeste premisset er
/// minst like sterkt som <c>avledet</c>. En K-kant som ikke er vurdert, gir <c>uavklart</c>. Når samme kant nås langs flere veier, brukes veien med
/// det sterkeste svakeste premisset (så færrest forslag, så kortest). Å håndheve regelen globalt, også der utsagnene skrives, er #411.
/// </para>
/// <para>
/// <b>Kjent avgrensning:</b> premissene er kanter. Klassens egen kunnskapsstatus (begrepet, f.eks. «kommune» = tekstfunn) og standardregelen som
/// løste «kommunen» til klassen da K-kanten ble lastet (#394), er ikke premisser her — K-kanten bærer ikke hvordan fra-noden ble løst (#411).
/// </para>
/// <para>
/// <b>Ingen gjetting (CLAUDE.md §8).</b> En klasse uten registrert eller utledet medlemskap gir ikke et treff, selv om aktøren kan være medlem;
/// den står i <see cref="KompetanseViaKlasse.Hull"/> når den har K-kanter.
/// </para>
/// </summary>
public sealed class KompetanseViaKlasseTjeneste(RegelIdeDbContext db, StrukturkantTjeneste kanttjeneste)
{
    private readonly KlassemedlemskapTjeneste klassemedlemskap = new(db, kanttjeneste);

    /// <returns>Null hvis virksomheten ikke finnes.</returns>
    public async Task<KompetanseViaKlasse?> ForVirksomhetAsync(Guid virksomhetId, CancellationToken ct = default)
    {
        var v = await db.Virksomheter.Where(x => x.Id == virksomhetId).Select(x => new { x.Id, x.Navn, x.Aktortype }).FirstOrDefaultAsync(ct);
        if (v is null) return null;
        var visningsnavn = await new VirksomhetVisningsnavnTjeneste(db).AlleAsync(ct);
        var aktor = new KantnodeVisning("virksomhet", v.Id, visningsnavn.GetValueOrDefault(v.Id) ?? v.Navn, v.Aktortype);

        // M- og I-kantene (medlemskap og rolleinnehav). Små tabeller; veiene går på tvers av dem.
        var mi = await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
            .Where(k => (k.Kategori == Strukturkanter.Medlemskap || k.Kategori == Strukturkanter.Rolleinnehav) && k.TilBegrepId != null)
            .ToListAsync(ct), ct);
        var kantPerId = mi.ToDictionary(k => k.Id);

        int Rang(IReadOnlyList<Guid> vei) => vei.Count == 0 ? 0 : vei.Max(id => SvakestePremiss.Rang(kantPerId[id].Kunnskapsstatus));
        int Forslag(IReadOnlyList<Guid> vei) => vei.Count(id => kantPerId[id].Status != Strukturkanter.Godkjent);
        bool Bedre(IReadOnlyList<Guid> ny, IReadOnlyList<Guid>? gammel) => gammel is null
            || Rang(ny) < Rang(gammel)
            || (Rang(ny) == Rang(gammel) && Forslag(ny) < Forslag(gammel))
            || (Rang(ny) == Rang(gammel) && Forslag(ny) == Forslag(gammel) && ny.Count < gammel.Count);

        // Veien til hver klasse/rolle (og til territoriet, som bare er et mellomledd: et område kan være medlem av en klasse, #164).
        var vei = new Dictionary<Guid, (List<Guid> Premisser, List<Guid> Noder)>();
        var ko = new Queue<Guid>();
        void Besok(Guid node, List<Guid> premisser, List<Guid> noder)
        {
            if (premisser.Count > 12 || !Bedre(premisser, vei.TryGetValue(node, out var g) ? g.Premisser : null)) return;
            vei[node] = (premisser, noder);
            ko.Enqueue(node);
        }

        // (1) Utledet medlemskap (klassen «kommune», #384): territoriekanten er premisset. Territoriet selv er et mellomledd.
        var territorier = new HashSet<Guid>();
        foreach (var klasseId in (await klassemedlemskap.KlasserMedRegelAsync(ct)).Keys)
        {
            var m = (await klassemedlemskap.UtledAsync(klasseId, v.Id, ct))?.Medlemmer.FirstOrDefault();
            if (m is null) continue;
            if (!kantPerId.ContainsKey(m.PremissKantId))
            {
                var premiss = await db.Strukturkanter.FirstOrDefaultAsync(k => k.Id == m.PremissKantId, ct);
                if (premiss is null) continue;
                kantPerId[premiss.Id] = premiss;
            }
            Besok(klasseId, [m.PremissKantId], [klasseId]);
            territorier.Add(m.OmradeId);
            Besok(m.OmradeId, [m.PremissKantId], []);
        }
        // (2) Registrerte M- og I-kanter fra aktøren.
        foreach (var k in mi.Where(k => k.FraVirksomhetId == v.Id)) Besok(k.TilBegrepId!.Value, [k.Id], [k.TilBegrepId!.Value]);
        // (3) Videre fra en klasse/et område: klasse i klasse (M) og rolle klassen har (I).
        var steg = 0;
        while (ko.Count > 0 && steg++ < 10_000)
        {
            var node = ko.Dequeue();
            var (premisser, veinoder) = vei[node];
            foreach (var k in mi.Where(k => k.FraBegrepId == node && k.TilBegrepId != node))
            {
                Besok(k.TilBegrepId!.Value, [.. premisser, k.Id], [.. veinoder, k.TilBegrepId!.Value]);
            }
        }
        foreach (var t in territorier.Where(t => vei.TryGetValue(t, out var x) && x.Noder.Count == 0)) vei.Remove(t);

        var noderIder = vei.Keys.ToList();
        var noder = await db.Begreper.Where(b => noderIder.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => new KantnodeVisning("begrep", b.Id, b.Term, b.Begrepskategori), ct);

        // (4) K-kantene fra eller til en klasse/rolle i veien.
        var kKanter = await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Kompetanse
                        && ((k.FraBegrepId != null && noderIder.Contains(k.FraBegrepId.Value))
                            || (k.TilBegrepId != null && noderIder.Contains(k.TilBegrepId.Value))))
            .ToListAsync(ct), ct);
        foreach (var k in kKanter) kantPerId[k.Id] = k;

        var beste = new Dictionary<(Guid, string), (StrukturkantEntitet Kant, string Retning, List<Guid> Premisser, List<Guid> Noder)>();
        foreach (var k in kKanter)
        {
            foreach (var (node, retning) in new[] { (k.FraBegrepId, "har"), (k.TilBegrepId, "overfor") })
            {
                if (node is not { } n || !vei.TryGetValue(n, out var x)) continue;
                List<Guid> premisser = [.. x.Premisser, k.Id];
                var nokkel = (k.Id, retning);
                if (beste.TryGetValue(nokkel, out var g) && !Bedre(premisser, g.Premisser)) continue;
                beste[nokkel] = (k, retning, premisser, x.Noder);
            }
        }

        var visninger = (await kanttjeneste.ByggVisningerAsync(beste.Values.Select(b => b.Kant).DistinctBy(k => k.Id).ToList(), perspektiv: null, ct))
            .ToDictionary(x => x.Id);
        var treff = beste.Values.Select(b =>
            {
                var svakest = SvakestePremiss.Finn(b.Premisser.Select(id => (id, kantPerId[id].Kunnskapsstatus)));
                return new KompetanseViaKlasseTreff(visninger[b.Kant.Id], b.Retning, b.Noder.Select(n => noder[n]).ToList(),
                    SvakestePremiss.SvarStatus(svakest?.Kunnskapsstatus), b.Premisser, svakest?.Id, svakest?.Kunnskapsstatus,
                    b.Premisser.Any(id => kantPerId[id].Status != Strukturkanter.Godkjent));
            })
            .OrderBy(t => t.Retning == "har" ? 0 : 1).ThenBy(t => t.Kant.Visningstekst, StringComparer.Ordinal).ToList();

        // (5) Hull: klasser med K-kanter som verken har registrerte medlemmer (M) eller en medlemskapsregel. Om aktøren er medlem, avgjøres ikke her.
        var klasserMedRegel = (await klassemedlemskap.KlasserMedRegelAsync(ct)).Keys.ToHashSet();
        var klasserMedMedlemmer = mi.Where(k => k.Kategori == Strukturkanter.Medlemskap).Select(k => k.TilBegrepId!.Value).ToHashSet();
        var kFraKlasser = await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Kompetanse && k.FraBegrepId != null)
            .Join(db.Begreper.Where(b => b.Begrepskategori == Nodetyper.Klasse), k => k.FraBegrepId, b => b.Id, (k, b) => new { b.Id, b.Term })
            .Distinct().ToListAsync(ct);
        var uavgjort = kFraKlasser.Where(b => !klasserMedRegel.Contains(b.Id) && !klasserMedMedlemmer.Contains(b.Id) && !vei.ContainsKey(b.Id))
            .Select(b => b.Term).Distinct().Order(StringComparer.Ordinal).ToList();
        var hull = uavgjort.Count == 0 ? new List<string>()
            : [$"Klasser med kompetanse, men uten registrert eller utledet medlemskap: {string.Join(", ", uavgjort.Select(t => $"«{t}»"))}. "
               + "Om aktøren er medlem, avgjøres ikke her (ikke funnet ≠ oppfunnet)."];

        var premissIder = treff.SelectMany(t => t.Premisser).Distinct().ToList();
        var premissVisninger = await kanttjeneste.ByggVisningerAsync(premissIder.Select(id => kantPerId[id]).ToList(), perspektiv: null, ct);
        return new KompetanseViaKlasse(aktor, treff, premissVisninger, hull);
    }
}
