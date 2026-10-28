using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>[Ny, issue #390 AK 1] Ett svar i «Område og tilhørighet»: noden (område eller aktør), premissene i kjedens rekkefølge, det svakeste
/// premisset og statusen svaret kan vises med etter regelen om svakeste premiss (docs/33 §4.6).</summary>
/// <param name="Avledet">False bare når svaret ER én lagret kant fra aktøren selv (et registrert medlemskap). Da gjelder kantens egen
/// kunnskapsstatus, og den er ikke regnet om.</param>
/// <param name="Kunnskapsstatus"><c>avledet</c> når det svakeste premisset er minst like sterkt som <c>avledet</c>; ellers premissets nivå
/// (<see cref="SvakestePremiss.SvarStatus"/>). For en lagret kant: kantens egen (NULL = ikke vurdert).</param>
/// <param name="Forslag">Minst ett premiss er ikke godkjent (forslag eller faglig vurdert).</param>
public sealed record TilhorighetKandidat(
    KantnodeVisning Node, bool Avledet, string? Kunnskapsstatus, IReadOnlyList<Guid> Premisser,
    Guid? SvakestePremissId, string? SvakestePremissStatus, bool Forslag);

/// <summary>[Ny, issue #390 AK 1] Én rubrikk. <see cref="Status"/>: <c>entydig</c> | <c>ikke_entydig</c> (flere, ingen valgt) |
/// <c>flere</c> (flere er riktig, f.eks. en tingrett med to rettssteder) | <c>mangler</c> (med <see cref="Hull"/>).</summary>
public sealed record TilhorighetSvar(string Rubrikk, string Status, IReadOnlyList<TilhorighetKandidat> Kandidater, string? Hull);

/// <summary>[Ny, issue #390 AK 1] «Område og tilhørighet» for en aktør med eget territorium. <see cref="Territorium"/> er null når aktøren
/// ikke har ett, og da sier <see cref="Hull"/> hvorfor. <see cref="Premisser"/> er alle kantene svarene bygger på, én gang hver.</summary>
public sealed record VirksomhetTilhorighet(
    KantnodeVisning Aktor, OmradeVisning? Territorium, Guid? TerritoriekantId, string? Hull,
    IReadOnlyList<TilhorighetSvar> Svar, IReadOnlyList<TilhorighetKandidat> Klasser, IReadOnlyList<StrukturkantVisning> Premisser);

/// <summary>
/// [Ny, issue #390 AK 1, Johann 2026-10-16 (Alta-eksempelet), docs/33 §4.1] Det en aktør med eget territorium hører til, sett fra AKTØREN:
/// fylke, tingrett og rettssted, lagsogn, lagdømme, lagmannsrett, statsforvalter, helseregion, regionalt helseforetak, valgkrets og
/// medlemskap i klasser. Før #390 sto dette bare på områdesiden (<see cref="OmradeOppslagTjeneste.ForKommuneAsync"/>): tingretten har
/// domsmyndighet over territoriet, ikke over kommunen som juridisk person, og den som åpnet Alta kommune, så ikke hvilken tingrett den
/// hører til.
/// <para>
/// <b>Ingenting lagres (#345).</b> Alt regnes ut fra kantene, med <b>territoriekanten</b> (aktøren <c>A har_ansvarsomrade</c> kommunen, samme
/// kommunenummer — regelen fra <see cref="KlassemedlemskapTjeneste"/>) som første premiss i hver kjede. Kjedene er de samme som i
/// tilhørigheten på områdesiden: kommune ← fylke (O), kommune ← tingrett → lagsogn (A + A sogner_til) → lagdømme (O) ← lagmannsrett (A),
/// fylke ← statsforvalter (A), fylke → helseregion (O) ← RHF (A), tingrett → rettssted (A har_sete_i).
/// </para>
/// <para>
/// <b>Svakeste premiss (docs/33 §4.6).</b> Hvert svar vises med <see cref="SvakestePremiss.SvarStatus"/> av det svakeste premisset: et svar
/// som bygger på en kant uten vurdert kunnskapsstatus, er <c>uavklart</c>, ikke <c>avledet</c>. Det svakeste premisset står i svaret, så
/// siden kan si hvilket. Når samme svar kan nås langs flere veier, brukes veien med det sterkeste svakeste premisset (så kortest).
/// Regelen håndheves her og ikke globalt — det er #411.
/// </para>
/// <para>
/// <b>Ingen gjetting (CLAUDE.md §8).</b> Ingen eller flere territorier → ingen svar, og hullet sies. Flere kandidater i en rubrikk der bare én
/// kan være riktig → <c>ikke_entydig</c>, ingen velges. Valgkretsen er ikke registrert i områderegisteret, og står som <c>mangler</c>.
/// </para>
/// </summary>
public sealed class OmradeTilhorighetTjeneste(RegelIdeDbContext db, StrukturkantTjeneste kanttjeneste)
{
    private readonly KlassemedlemskapTjeneste klassemedlemskap = new(db, kanttjeneste);

    /// <summary>Rubrikkene i fast rekkefølge (samme som tilhørigheten på områdesiden, pluss rettssted og valgkrets).</summary>
    public static readonly string[] Rubrikker =
        ["fylke", "tingrett", "rettssted", "lagsogn", "lagdømme", "lagmannsrett", "statsforvalter", "helseregion", "RHF", "valgkrets"];

    /// <returns>Null hvis virksomheten ikke finnes.</returns>
    public async Task<VirksomhetTilhorighet?> ForVirksomhetAsync(Guid virksomhetId, CancellationToken ct = default)
    {
        var v = await db.Virksomheter.Where(x => x.Id == virksomhetId)
            .Select(x => new { x.Id, x.Navn, x.Kommunenummer, x.Aktortype }).FirstOrDefaultAsync(ct);
        if (v is null) return null;
        var visningsnavn = await new VirksomhetVisningsnavnTjeneste(db).AlleAsync(ct);
        var aktor = new KantnodeVisning("virksomhet", v.Id, visningsnavn.GetValueOrDefault(v.Id) ?? v.Navn, v.Aktortype);

        // Alle gjeldende A- og O-kanter, én gang. Områderegisteret er lite (≈ 2 000 kanter), og kjedene går på tvers av det.
        var kanter = await kanttjeneste.FiltrerGjeldendeAsync(await db.Strukturkanter
            .Where(k => k.Kategori == Strukturkanter.Omradesammensetning || k.Kategori == Strukturkanter.Ansvarsomrade
                        || k.Kategori == Strukturkanter.Medlemskap)
            .ToListAsync(ct), ct);
        var kantPerId = kanter.ToDictionary(k => k.Id);

        // (1) Territoriet: aktørens A har_ansvarsomrade til et gjeldende kommuneområde med aktørens kommunenummer.
        var egneA = kanter.Where(k => k.Kategori == Strukturkanter.Ansvarsomrade && k.Typekode == "har_ansvarsomrade"
                                      && k.FraVirksomhetId == v.Id && k.TilBegrepId != null).ToList();
        var egneOmradeIder = egneA.Select(k => k.TilBegrepId!.Value).Distinct().ToList();
        var egneOmrader = await db.Begreper.Where(b => egneOmradeIder.Contains(b.Id))
            .Select(b => new { b.Id, b.Term, b.Omradetype, b.Omradekode, b.Entitetsstatus, b.GyldigTil }).ToListAsync(ct);
        var territorier = egneOmrader.Where(o => o.Omradetype == Omradetyper.Kommune && o.Entitetsstatus == "gjeldende" && o.GyldigTil == null
                                                 && v.Kommunenummer is not null && o.Omradekode == v.Kommunenummer).ToList();
        if (territorier.Count != 1)
        {
            var hull = territorier.Count == 0
                ? v.Kommunenummer is null
                    ? "Aktøren har ikke eget territorium i områderegisteret (ingen kommunenummer, og ingen territoriekant). Tilhørighet via territoriet gjelder kommunene; fylkeskommunene har ingen territoriekant ennå."
                    : $"Ingen territoriekant: aktøren har kommunenummer {v.Kommunenummer}, men ingen gjeldende ansvarsområde-kant til kommuneområdet med det nummeret."
                : $"{territorier.Count} territorier med kommunenummer {v.Kommunenummer} — ikke entydig, ingen valgt.";
            return new VirksomhetTilhorighet(aktor, null, null, hull, [], [], []);
        }
        var t = territorier[0];
        var territorium = new OmradeVisning(t.Id, t.Term, t.Omradetype, t.Omradekode);
        // Flere kanter fra aktøren til territoriet (en godkjent og et forslag): den godkjente er premisset (som KlassemedlemskapTjeneste).
        var territoriekant = egneA.Where(k => k.TilBegrepId == t.Id)
            .OrderBy(k => k.Status == Strukturkanter.Godkjent ? 0 : 1).ThenBy(k => SvakestePremiss.Rang(k.Kunnskapsstatus)).ThenBy(k => k.Id).First();

        int Rang(IReadOnlyList<Guid> vei) => vei.Count == 0 ? 0 : vei.Max(id => SvakestePremiss.Rang(kantPerId[id].Kunnskapsstatus));
        bool Bedre(IReadOnlyList<Guid> ny, IReadOnlyList<Guid>? gammel) => gammel is null
            || Rang(ny) < Rang(gammel)
            || (Rang(ny) == Rang(gammel) && ny.Count(id => kantPerId[id].Status != Strukturkanter.Godkjent) < gammel.Count(id => kantPerId[id].Status != Strukturkanter.Godkjent))
            || (Rang(ny) == Rang(gammel) && ny.Count < gammel.Count);

        // (2) Foreldrene til et område, med premissene for hvert ledd: O-kanten (forelder består av barnet), eller — #345 — tingretten
        // som har ansvarsområde i barnet og sogner til forelderen (to premisser).
        var oKanter = kanter.Where(k => k.Kategori == Strukturkanter.Omradesammensetning && k.FraBegrepId != null && k.TilBegrepId != null);
        var ansvar = kanter.Where(k => k.Kategori == Strukturkanter.Ansvarsomrade && k.Typekode == "har_ansvarsomrade"
                                       && k.FraVirksomhetId != null && k.TilBegrepId != null).ToList();
        var sogner = kanter.Where(k => k.Kategori == Strukturkanter.Ansvarsomrade && k.Typekode == OmraderegisterSeed.SognerTil
                                       && k.FraVirksomhetId != null && k.TilBegrepId != null).ToList();
        var sognerPerAktor = sogner.GroupBy(k => k.FraVirksomhetId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        var foreldre = oKanter.Select(o => (Barn: o.TilBegrepId!.Value, Forelder: o.FraBegrepId!.Value, Ledd: (IReadOnlyList<Guid>)[o.Id]))
            .Concat(ansvar.SelectMany(a => (sognerPerAktor.GetValueOrDefault(a.FraVirksomhetId!.Value) ?? [])
                .Select(s => (Barn: a.TilBegrepId!.Value, Forelder: s.TilBegrepId!.Value, Ledd: (IReadOnlyList<Guid>)[a.Id, s.Id]))))
            .GroupBy(x => x.Barn).ToDictionary(g => g.Key, g => g.ToList());

        // Veien til hvert område fra territoriet. Avslapning til ingen bedre vei finnes; dybden er begrenset (kjedene er ≤ 4 ledd).
        var vei = new Dictionary<Guid, List<Guid>> { [t.Id] = [territoriekant.Id] };
        var ko = new Queue<Guid>([t.Id]);
        var steg = 0;
        while (ko.Count > 0 && steg++ < 10_000)
        {
            var barn = ko.Dequeue();
            foreach (var f in foreldre.GetValueOrDefault(barn) ?? [])
            {
                if (f.Forelder == t.Id) continue;
                var ny = vei[barn].Concat(f.Ledd).ToList();
                if (ny.Count > 12 || !Bedre(ny, vei.GetValueOrDefault(f.Forelder))) continue;
                vei[f.Forelder] = ny;
                ko.Enqueue(f.Forelder);
            }
        }
        var omradeIder = vei.Keys.ToList();
        var omrader = await db.Begreper.Where(b => omradeIder.Contains(b.Id))
            .Select(b => new OmradeVisning(b.Id, b.Term, b.Omradetype, b.Omradekode)).ToDictionaryAsync(o => o.Id, ct);

        TilhorighetKandidat Kandidat(KantnodeVisning node, IReadOnlyList<Guid> premisser)
        {
            var svakest = SvakestePremiss.Finn(premisser.Select(id => (id, kantPerId[id].Kunnskapsstatus)));
            return new TilhorighetKandidat(node, true, SvakestePremiss.SvarStatus(svakest?.Kunnskapsstatus), premisser,
                svakest?.Id, svakest?.Kunnskapsstatus, premisser.Any(id => kantPerId[id].Status != Strukturkanter.Godkjent));
        }
        KantnodeVisning Omradenode(OmradeVisning o) => new("begrep", o.Id, o.Navn, Nodetyper.Omrade) { Omradetype = o.Omradetype };
        var aktorIder = ansvar.Select(k => k.FraVirksomhetId!.Value)
            .Concat(kanter.Where(k => k.Typekode == "har_sete_i" && k.FraVirksomhetId != null).Select(k => k.FraVirksomhetId!.Value)).Distinct().ToList();
        var aktorer = await db.Virksomheter.Where(x => aktorIder.Contains(x.Id)).Select(x => new { x.Id, x.Navn, x.Aktortype })
            .ToDictionaryAsync(x => x.Id, x => new KantnodeVisning("virksomhet", x.Id, visningsnavn.GetValueOrDefault(x.Id) ?? x.Navn, x.Aktortype), ct);

        // Én kandidat per node: veien med det sterkeste svakeste premisset.
        static List<TilhorighetKandidat> Beste(IEnumerable<TilhorighetKandidat> k) => k.GroupBy(x => x.Node.Id)
            .Select(g => g.OrderBy(x => SvakestePremiss.Rang(x.SvakestePremissStatus)).ThenBy(x => x.Forslag ? 1 : 0).ThenBy(x => x.Premisser.Count).First())
            .OrderBy(x => x.Node.Navn, StringComparer.Ordinal).ToList();

        List<TilhorighetKandidat> Omradetype(string type) => Beste(vei.Where(x => x.Key != t.Id && omrader.TryGetValue(x.Key, out var o) && o.Omradetype == type)
            .Select(x => Kandidat(Omradenode(omrader[x.Key]), x.Value)));
        List<TilhorighetKandidat> AnsvarligeFor(string type) => Beste(ansvar
            .Where(a => vei.ContainsKey(a.TilBegrepId!.Value) && a.TilBegrepId != t.Id && omrader[a.TilBegrepId!.Value].Omradetype == type)
            .Select(a => Kandidat(aktorer[a.FraVirksomhetId!.Value], [.. vei[a.TilBegrepId!.Value], a.Id])));

        // Tingretten: de ANDRE aktørene med ansvarsområde direkte i territoriet (samme regel som OmradeOppslagTjeneste, docs/33 §4.1).
        var tingrettKanter = ansvar.Where(a => a.TilBegrepId == t.Id && a.FraVirksomhetId != v.Id).ToList();
        var tingrett = Beste(tingrettKanter.Select(a => Kandidat(aktorer[a.FraVirksomhetId!.Value], [territoriekant.Id, a.Id])));
        // Rettsstedet: tingrettens A har_sete_i. En tingrett kan ha flere rettssteder (Vestre Finnmark: Alta og Hammerfest).
        var seter = kanter.Where(k => k.Kategori == Strukturkanter.Ansvarsomrade && k.Typekode == "har_sete_i" && k.FraVirksomhetId != null && k.TilBegrepId != null).ToList();
        var seteOmradeIder = seter.Select(s => s.TilBegrepId!.Value).Distinct().ToList();
        var seteOmrader = await db.Begreper.Where(b => seteOmradeIder.Contains(b.Id))
            .Select(b => new OmradeVisning(b.Id, b.Term, b.Omradetype, b.Omradekode)).ToDictionaryAsync(o => o.Id, ct);
        var rettssted = Beste(tingrettKanter.SelectMany(a => seter.Where(s => s.FraVirksomhetId == a.FraVirksomhetId)
            .Select(s => Kandidat(Omradenode(seteOmrader[s.TilBegrepId!.Value]), [territoriekant.Id, a.Id, s.Id]))));

        TilhorighetSvar Svar(string rubrikk, List<TilhorighetKandidat> k, bool flereErRiktig = false, string? hullVedMangel = null) => new(rubrikk,
            k.Count switch { 0 => "mangler", 1 => "entydig", _ => flereErRiktig ? "flere" : "ikke_entydig" }, k,
            k.Count == 0 ? hullVedMangel ?? "Ingen kant i registeret gir et svar." : null);
        var svar = new List<TilhorighetSvar>
        {
            Svar("fylke", Omradetype(Omradetyper.Fylke)),
            Svar("tingrett", tingrett),
            Svar("rettssted", rettssted, flereErRiktig: true, tingrett.Count == 0 ? "Ingen tingrett — da er det heller ikke noe rettssted." : null),
            Svar("lagsogn", Omradetype(Omradetyper.Lagsogn)),
            Svar("lagdømme", Omradetype(Omradetyper.Lagdomme)),
            Svar("lagmannsrett", AnsvarligeFor(Omradetyper.Lagdomme)),
            Svar("statsforvalter", AnsvarligeFor(Omradetyper.Fylke)),
            Svar("helseregion", Omradetype(Omradetyper.Helseregion)),
            Svar("RHF", AnsvarligeFor(Omradetyper.Helseregion)),
            // Valgkretsene er ikke en områdetype i registeret (#312). Ikke funnet ≠ oppfunnet: ingen avledning fra fylket.
            new TilhorighetSvar("valgkrets", "mangler", [],
                "Valgkretsene er ikke registrert i områderegisteret. Ingen valgkrets vises før inndelingen er lastet fra en kilde — den utledes ikke av fylket."),
        };

        // (3) Klasser. Utledet medlemskap (klassen «kommune», #384) har territoriekanten som premiss. Registrerte M-kanter fra aktøren er
        // lagrede utsagn med egen status; fra territoriet eller videre fra en klasse er de avledet.
        var klasser = new List<TilhorighetKandidat>();
        foreach (var klasseId in (await klassemedlemskap.KlasserMedRegelAsync(ct)).Keys)
        {
            var m = (await klassemedlemskap.UtledAsync(klasseId, v.Id, ct))?.Medlemmer.FirstOrDefault();
            if (m is null || !kantPerId.ContainsKey(m.PremissKantId)) continue;
            var klasse = await db.Begreper.Where(b => b.Id == klasseId).Select(b => new KantnodeVisning("begrep", b.Id, b.Term, b.Begrepskategori)).FirstAsync(ct);
            klasser.Add(Kandidat(klasse, [m.PremissKantId]));
        }
        var mKanter = kanter.Where(k => k.Kategori == Strukturkanter.Medlemskap && k.TilBegrepId != null).ToList();
        var mVei = new Dictionary<Guid, List<Guid>>();
        var mko = new Queue<(Guid? V, Guid? B, List<Guid> Vei)>([(v.Id, null, []), (null, t.Id, [territoriekant.Id])]);
        foreach (var k in klasser) mko.Enqueue((null, k.Node.Id, [.. k.Premisser]));
        steg = 0;
        while (mko.Count > 0 && steg++ < 10_000)
        {
            var (fv, fb, forrige) = mko.Dequeue();
            foreach (var m in mKanter.Where(m => (fv != null && m.FraVirksomhetId == fv) || (fb != null && m.FraBegrepId == fb)))
            {
                var ny = forrige.Append(m.Id).ToList();
                if (ny.Count > 12 || !Bedre(ny, mVei.GetValueOrDefault(m.TilBegrepId!.Value))) continue;
                mVei[m.TilBegrepId!.Value] = ny;
                mko.Enqueue((null, m.TilBegrepId, ny));
            }
        }
        var klasseIder = mVei.Keys.ToList();
        var klassenoder = await db.Begreper.Where(b => klasseIder.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => new KantnodeVisning("begrep", b.Id, b.Term, b.Begrepskategori), ct);
        foreach (var (klasseId, premisser) in mVei)
        {
            var direkte = premisser.Count == 1 && kantPerId[premisser[0]].FraVirksomhetId == v.Id;
            klasser.Add(direkte
                ? new TilhorighetKandidat(klassenoder[klasseId], false, kantPerId[premisser[0]].Kunnskapsstatus, premisser, null, null,
                    kantPerId[premisser[0]].Status != Strukturkanter.Godkjent)
                : Kandidat(klassenoder[klasseId], premisser));
        }
        klasser = klasser.GroupBy(k => k.Node.Id).Select(g => g.OrderBy(x => x.Avledet ? 1 : 0).ThenBy(x => SvakestePremiss.Rang(x.SvakestePremissStatus)).First())
            .OrderBy(k => k.Node.Navn, StringComparer.Ordinal).ToList();

        var premissIder = svar.SelectMany(s => s.Kandidater).Concat(klasser).SelectMany(k => k.Premisser).Distinct().ToList();
        var visninger = await kanttjeneste.ByggVisningerAsync(premissIder.Select(id => kantPerId[id]).ToList(), perspektiv: null, ct);
        return new VirksomhetTilhorighet(aktor, territorium, territoriekant.Id, null, svar, klasser, visninger);
    }
}
