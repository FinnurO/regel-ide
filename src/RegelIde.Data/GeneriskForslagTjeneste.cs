using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>Hvor et forslag til referent kommer fra (veiviserens én-skjerm for generisk omtale, #394 AK 4).</summary>
public static class GeneriskForslagKilde
{
    /// <summary>Standardregelen for termen gir svaret — ingen binding trengs.</summary>
    public const string Standardregel = "standardregel";
    /// <summary>Lovdatas «Ansvarlig departement» for rettskilden (sekundær kilde).</summary>
    public const string AnsvarligDepartement = "ansvarlig_departement";
    /// <summary>Aktøren, klassen eller rollen er nevnt (tagget eller i en strukturkant hjemlet) i samme rettskilde.</summary>
    public const string SammeRettskilde = "samme_rettskilde";
    /// <summary>En navneform i korpuset der termen står som eget ord («Styret for domstoladministrasjonen» for «styret»).</summary>
    public const string Navneform = "navneform";
    /// <summary>Termen er bundet til denne referenten i andre rettskilder.</summary>
    public const string TidligereBinding = "tidligere_binding";
    /// <summary>En navnekandidat i samme rettskilde der ordet står som eget ord («Styret for domstoladministrasjonen» for «styret»), og som
    /// ikke finnes som aktør ennå. Referenten har typen <c>ny</c> og id-en til navnekandidaten; aktøren opprettes først når noen lagrer.</summary>
    public const string NyAktorFraKandidat = "ny_aktor_fra_kandidat";
}

/// <summary>
/// Ett forslag til hva en generisk omtale betyr. <see cref="Grunn"/> er klart språk («Nevnt i § 33a første ledd, og navnet inneholder
/// «styret»»). Grunnlaget er ferdig utfylt etter hvordan referenten ble funnet: kunnskapsstatus, tolkningsargumenter og begrunnelse —
/// veiviseren viser dem under «Detaljer», og de kan endres der.
/// </summary>
public sealed record GeneriskReferentforslag(
    string Kilde, KantnodeVisning Referent, string Grunn, int Rang, string Kunnskapsstatus, IReadOnlyList<string> Tolkningsargumenter,
    string Begrunnelse, bool TrengerIkkeBinding = false);

/// <summary>Alt veiviserens én-skjerm trenger for én kandidat.</summary>
public sealed record GeneriskKandidatkontekst(
    Guid KandidatId, string Term, Guid RettskildeId, string RettskildeTittel, string NodeEid, Guid? NavneformId,
    GeneriskStandardregel? Standardregel, StandardregelSvar? Regelsvar, IReadOnlyList<GeneriskReferentforslag> Forslag,
    int ForekomsterIRettskilden, IReadOnlyList<NavneformBindingVisning> BindingerHer);

/// <summary>
/// [Ny, issue #394 AK 4, Johann 2026-10-23: «tungvint å koble «departementet» til lovens faktiske eierdepartement»] Forslagene til
/// veiviserens én-skjerm for generisk omtale, mest sannsynlige øverst: standardregelen (når den løser — da trengs ingen binding), aktører
/// som er nevnt i samme rettskilde (høyest når navnet inneholder ordet, og når de står i samme paragraf), navneformer i korpuset som
/// inneholder ordet, referenter ordet er bundet til andre steder, og rettskildens ansvarlige departement.
/// <para>
/// Forslagene er ikke svar: ingenting lagres her, og et forslag blir en binding bare når et menneske velger det og lagrer. Grunnlaget
/// som fylles ut, sier hvordan referenten ble funnet (kunnskapsstatus <c>fortolkning</c> med <c>kontekst_system</c>, og en begrunnelse som
/// sier hvor), slik at det ikke ser sikrere ut enn det er (CLAUDE.md §8).
/// </para>
/// </summary>
public sealed class GeneriskForslagTjeneste(RegelIdeDbContext db, GeneriskNavneformTjeneste generiske)
{
    private const int MaksPerKilde = 8;

    public async Task<GeneriskKandidatkontekst?> ForKandidatAsync(Guid kandidatId, CancellationToken ct = default)
    {
        var k = await db.Navnekandidater.Where(x => x.Id == kandidatId)
            .Select(x => new { x.Id, x.ForeslattTekst, x.RettskildeId, x.NodeEid }).FirstOrDefaultAsync(ct);
        if (k is null) return null;
        var term = GeneriskNavneformTjeneste.NormaliserTerm(k.ForeslattTekst);
        var rk = await db.Rettskilder.Where(r => r.Id == k.RettskildeId)
            .Select(r => new { r.Id, Tittel = r.Kortnavn ?? r.Tittel, r.AnsvarligDepartement }).FirstAsync(ct);
        var navneform = await generiske.FinnAsync(term, ct);
        var regel = GeneriskStandardregelTjeneste.For(term);
        var regelsvar = regel is null ? null : await new GeneriskStandardregelTjeneste(db).LosAsync(term, k.RettskildeId, null, ct);
        var noder = await db.RettskildeNoder.Where(n => n.RettskildeId == k.RettskildeId && n.Entitetsstatus == "gjeldende")
            .Select(n => new { n.Id, n.Eid, n.ParentNodeId, n.NodeType, n.Nummer, n.Tekst }).ToListAsync(ct);
        var perId = noder.ToDictionary(n => n.Id);
        string? Paragraf(string eid)
        {
            var n = noder.FirstOrDefault(x => x.Eid == eid);
            while (n is not null && n.NodeType != "paragraf") n = n.ParentNodeId is { } p && perId.TryGetValue(p, out var f) ? f : null;
            return n?.Eid;
        }
        string Sted(string eid) => noder.FirstOrDefault(x => x.Eid == eid) is { } n ? NodeTekst(n.Eid) : eid;
        var hereParagraf = Paragraf(k.NodeEid);
        var ord = new Regex($@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(term)}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var forekomster = noder.Sum(n => n.Tekst is null ? 0 : ord.Matches(n.Tekst).Count);
        var forslag = new List<GeneriskReferentforslag>();
        var sett = new HashSet<(string, Guid)>();
        void Legg(GeneriskReferentforslag f)
        {
            if (sett.Add((f.Referent.Type, f.Referent.Id))) forslag.Add(f);
        }
        var visningsnavn = new VirksomhetVisningsnavnTjeneste(db);

        // 1. Standardregelen.
        if (regelsvar is { Utfall: StandardregelUtfall.Lost, ReferentVisning: { } rv })
        {
            Legg(new GeneriskReferentforslag(GeneriskForslagKilde.Standardregel, rv,
                $"Standardregelen «{regelsvar.Regelnavn}»: {regelsvar.Forklaring}"
                + (regelsvar.Dokumentasjon == "sekundaer" ? " Bygger på en sekundær kilde." : ""),
                0, Strukturkanter.Fortolkning, ["kontekst_system"],
                $"Samme svar som standardregelen «{regelsvar.Regelnavn}» gir for {rk.Tittel}.", TrengerIkkeBinding: true));
        }

        // 2. Nevnt i samme rettskilde: tagger (aktør via navneform, klasse/rolle som begrep) og strukturkanter hjemlet her.
        var tagger = await db.TekstTagger.Where(t => t.RettskildeId == k.RettskildeId && t.Entitetsstatus == "gjeldende" && t.RefId != null
                && (t.Kind == "virksomhet" || t.Kind == "begrep"))
            .Select(t => new { t.Kind, RefId = t.RefId!.Value, t.NodeEid, t.QuoteExact }).ToListAsync(ct);
        var navneformIder = tagger.Where(t => t.Kind == "virksomhet").Select(t => t.RefId).Distinct().ToList();
        var navneformTilAktor = await db.Begreper.Where(b => navneformIder.Contains(b.Id) && b.VirksomhetReferanseId != null && b.Navneformgrunn != VirksomhetsbegrepTjeneste.Generisk)
            .Select(b => new { b.Id, Vid = b.VirksomhetReferanseId!.Value }).ToDictionaryAsync(b => b.Id, b => b.Vid, ct);
        var begrepIder = tagger.Where(t => t.Kind == "begrep").Select(t => t.RefId).Distinct().ToList();
        var klasseRolle = await db.Begreper.Where(b => begrepIder.Contains(b.Id) && b.Entitetsstatus == "gjeldende"
                && (b.Begrepskategori == Nodetyper.Klasse || b.Begrepskategori == Nodetyper.Rolle))
            .Select(b => new { b.Id, b.Term, b.Begrepskategori }).ToDictionaryAsync(b => b.Id, ct);
        var kantAktorer = await db.Strukturkanter.Where(x => x.HjemmelRettskildeId == k.RettskildeId && x.HjemmelEid != null)
            .Select(x => new { x.FraVirksomhetId, x.TilVirksomhetId, x.FraBegrepId, x.TilBegrepId, Eid = x.HjemmelEid! }).ToListAsync(ct);
        var nevnt = new List<(string Type, Guid Id, string Eid)>();
        foreach (var t in tagger)
        {
            if (t.Kind == "virksomhet" && navneformTilAktor.TryGetValue(t.RefId, out var vid)) nevnt.Add(("virksomhet", vid, t.NodeEid));
            else if (t.Kind == "begrep" && klasseRolle.ContainsKey(t.RefId)) nevnt.Add(("begrep", t.RefId, t.NodeEid));
        }
        foreach (var x in kantAktorer)
        {
            foreach (var v in new[] { x.FraVirksomhetId, x.TilVirksomhetId }.OfType<Guid>()) nevnt.Add(("virksomhet", v, x.Eid));
        }
        var aktorIder = nevnt.Where(n => n.Type == "virksomhet").Select(n => n.Id).Distinct().ToList();
        var aktorer = await db.Virksomheter.Where(v => aktorIder.Contains(v.Id) && v.Livsstatus == null)
            .Select(v => new { v.Id, v.Navn, v.Aktortype }).ToDictionaryAsync(v => v.Id, ct);
        var kandidaterHer = new List<(KantnodeVisning V, string Eid, bool Inneholder, bool SammeParagraf)>();
        foreach (var g in nevnt.GroupBy(n => (n.Type, n.Id)))
        {
            KantnodeVisning? v = null;
            if (g.Key.Type == "virksomhet" && aktorer.TryGetValue(g.Key.Id, out var a))
            {
                v = new KantnodeVisning("virksomhet", a.Id, await visningsnavn.ForAsync(a.Id, ct) ?? a.Navn, a.Aktortype);
            }
            else if (g.Key.Type == "begrep" && klasseRolle.TryGetValue(g.Key.Id, out var b))
            {
                v = new KantnodeVisning("begrep", b.Id, b.Term, b.Begrepskategori);
            }
            if (v is null) continue;
            var sammeParagraf = hereParagraf is not null && g.Any(n => Paragraf(n.Eid) == hereParagraf);
            var eid = g.OrderBy(n => Paragraf(n.Eid) == hereParagraf ? 0 : 1).First().Eid;
            kandidaterHer.Add((v, eid, ord.IsMatch(v.Navn), sammeParagraf));
        }
        foreach (var x in kandidaterHer.OrderBy(x => x.Inneholder ? 0 : 1).ThenBy(x => x.SammeParagraf ? 0 : 1).ThenBy(x => x.V.Navn, StringComparer.Ordinal)
                     .Take(MaksPerKilde * 2))
        {
            var rang = 10 + (x.Inneholder ? 0 : 20) + (x.SammeParagraf ? 0 : 5);
            var grunn = $"Nevnt i {Sted(x.Eid)} i {rk.Tittel}" + (x.Inneholder ? $", og navnet inneholder «{term}»." : ".");
            Legg(new GeneriskReferentforslag(GeneriskForslagKilde.SammeRettskilde, x.V, grunn, rang, Strukturkanter.Fortolkning, ["kontekst_system"],
                $"«{term}» i {Sted(k.NodeEid)} viser til {x.V.Navn}, som er nevnt i {Sted(x.Eid)} i samme rettskilde"
                + (x.Inneholder ? $" — «{term}» er kortformen av navnet." : ".")));
        }

        // 2b. Navnekandidater i samme rettskilde som inneholder ordet og ikke er kjente aktører (Johanns eksempel: «styret» i domstolloven
        // § 33a, der «Styret for domstoladministrasjonen» står i samme ledd, men ikke er registrert).
        var kandidatnavn = await db.Navnekandidater.Where(x => x.RettskildeId == k.RettskildeId && x.Status == "Venter" && x.Id != k.Id)
            .Select(x => new { x.Id, x.ForeslattTekst, x.NodeEid }).ToListAsync(ct);
        var kjenteNavn = (await db.Virksomheter.Where(v => v.Livsstatus == null).Select(v => v.Navn.ToLower()).ToListAsync(ct))
            .Concat(await db.Begreper.Where(b => b.Begrepskategori == "virksomhet" && b.Entitetsstatus == "gjeldende").Select(b => b.Term.ToLower()).ToListAsync(ct))
            .ToHashSet();
        foreach (var x in kandidatnavn.Where(x => ord.IsMatch(x.ForeslattTekst) && GeneriskNavneformTjeneste.NormaliserTerm(x.ForeslattTekst) != term
                         && !kjenteNavn.Contains(x.ForeslattTekst.ToLower()))
                     .GroupBy(x => x.ForeslattTekst.ToLower()).Select(g => g.OrderBy(y => Paragraf(y.NodeEid) == hereParagraf ? 0 : 1).First())
                     .Take(MaksPerKilde))
        {
            var sammeParagraf = hereParagraf is not null && Paragraf(x.NodeEid) == hereParagraf;
            Legg(new GeneriskReferentforslag(GeneriskForslagKilde.NyAktorFraKandidat, new KantnodeVisning("ny", x.Id, x.ForeslattTekst, null),
                $"Står i {Sted(x.NodeEid)} som «{x.ForeslattTekst}», og navnet inneholder «{term}». Finnes ikke som aktør ennå — opprettes når du lagrer.",
                sammeParagraf ? 8 : 25, Strukturkanter.Fortolkning, ["kontekst_system"],
                $"«{term}» i {Sted(k.NodeEid)} viser til «{x.ForeslattTekst}», som står i {Sted(x.NodeEid)} i samme rettskilde — «{term}» er kortformen."));
        }

        // 3. Navneformer i korpuset der ordet står som eget ord (ikke generiske, ikke nedlagte aktører).
        var mønster = $@"\m{Regex.Escape(term)}\M";
        var navnetreff = await db.Begreper.FromSqlRaw(
                """
                SELECT * FROM begreper WHERE begrepskategori = 'virksomhet' AND entitetsstatus = 'gjeldende' AND virksomhet_referanse_id IS NOT NULL
                  AND (navneformgrunn IS NULL OR navneformgrunn <> 'generisk') AND term ~* {0} AND lower(term) <> {1}
                """, mønster, term)
            .Select(b => new { b.Term, Vid = b.VirksomhetReferanseId!.Value }).Take(50).ToListAsync(ct);
        var treffAktorer = navnetreff.Select(t => t.Vid).Distinct().ToList();
        var levende = await db.Virksomheter.Where(v => treffAktorer.Contains(v.Id) && v.Livsstatus == null)
            .Select(v => new { v.Id, v.Navn, v.Aktortype }).ToDictionaryAsync(v => v.Id, ct);
        foreach (var t in navnetreff.Where(t => levende.ContainsKey(t.Vid)).GroupBy(t => t.Vid).Take(MaksPerKilde))
        {
            var a = levende[t.Key];
            var v = new KantnodeVisning("virksomhet", a.Id, await visningsnavn.ForAsync(a.Id, ct) ?? a.Navn, a.Aktortype);
            Legg(new GeneriskReferentforslag(GeneriskForslagKilde.Navneform, v, $"Navneformen «{t.First().Term}» inneholder «{term}».", 40,
                Strukturkanter.Fortolkning, ["kontekst_system"],
                $"«{term}» i {Sted(k.NodeEid)} i {rk.Tittel} er lest som kortform av «{t.First().Term}»."));
        }

        // 4. Referenter termen er bundet til i andre rettskilder.
        if (navneform is not null)
        {
            var andre = (await generiske.HentBindingerAsync(navneformId: navneform.Id, ct: ct))
                .Where(b => b.RettskildeId != k.RettskildeId).GroupBy(b => (b.Referent.Type, b.Referent.Id)).Take(MaksPerKilde);
            foreach (var g in andre)
            {
                var b = g.First();
                Legg(new GeneriskReferentforslag(GeneriskForslagKilde.TidligereBinding, b.Referent,
                    $"«{term}» er bundet til dette i {string.Join(", ", g.Select(x => x.RettskildeTittel).Distinct().Take(3))}"
                    + (g.Count() > 3 ? $" og {g.Count() - 3} til" : "") + ".", 50, Strukturkanter.Fortolkning, ["kontekst_system"],
                    $"«{term}» i {rk.Tittel} er lest som i {b.RettskildeTittel}, der det er bundet til {b.Referent.Navn}."));
            }
        }

        // 5. Rettskildens ansvarlige departement (Lovdata, sekundær) — høyt bare for «departementet»-aktige ord.
        foreach (var navn in (rk.AnsvarligDepartement ?? []).Distinct())
        {
            if (await new VirksomhetOppslagTjeneste(db).FinnVirksomhetIdForNavnAsync(navn) is not { } did) continue;
            var a = await db.Virksomheter.Where(v => v.Id == did).Select(v => new { v.Id, v.Navn, v.Aktortype }).FirstAsync(ct);
            var v = new KantnodeVisning("virksomhet", a.Id, await visningsnavn.ForAsync(a.Id, ct) ?? a.Navn, a.Aktortype);
            Legg(new GeneriskReferentforslag(GeneriskForslagKilde.AnsvarligDepartement, v,
                $"Lovdata oppgir {v.Navn} som ansvarlig departement for {rk.Tittel} (sekundær kilde).",
                term.Contains("departement", StringComparison.Ordinal) ? 5 : 60, Strukturkanter.Fortolkning, ["kontekst_system"],
                $"«{term}» i {rk.Tittel} er lest som rettskildens ansvarlige departement etter Lovdata (sekundær kilde)."));
        }

        var bindingerHer = navneform is null ? [] : await generiske.HentBindingerAsync(navneformId: navneform.Id, rettskildeId: k.RettskildeId, ct: ct);
        return new GeneriskKandidatkontekst(k.Id, term, k.RettskildeId, rk.Tittel, k.NodeEid, navneform?.Id, regel, regelsvar,
            forslag.OrderBy(f => f.Rang).ToList(), forekomster, bindingerHer);

        string NodeTekst(string eid)
        {
            // «§ 33a første ledd» av eId-en (…/§33a/ledd-1/punkt-2), uten å slå opp på nytt. Ukjent form → eId-en som den er.
            var m = Regex.Match(eid, @"/(§[^/]+)(?:/ledd-(\d+))?(?:/punkt-(\d+))?$");
            if (!m.Success) return eid;
            var s = "§ " + m.Groups[1].Value[1..];
            if (m.Groups[2].Success) s += $" {Ordenstall(int.Parse(m.Groups[2].Value))} ledd";
            if (m.Groups[3].Success) s += $" punkt {m.Groups[3].Value}";
            return s;
        }
    }

    private static string Ordenstall(int n) => n switch
    {
        1 => "første", 2 => "annet", 3 => "tredje", 4 => "fjerde", 5 => "femte", 6 => "sjette", 7 => "sjuende", 8 => "åttende",
        _ => $"{n}.",
    };
}
