using RegelIde.Data.Strukturkonvertering;

namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #312 «Strukturmodell 7: områderegister», 2026-10-08] Tolker forskrift om inndelingen av rettskretser
/// og lagdømmer (FOR-2021-01-22-163) til domstolstrukturen — tingrett → kommuner, tingrett → rettssted, lagsogn →
/// tingretter, lagdømme → lagsogn — og løser navnene opp mot Kartverkets kommuner og SSR. Ren funksjon: ingen
/// database, ingen nettverk. <see cref="OmraderegisterSeed"/> skriver resultatet som strukturkanter.
/// <para>
/// <b>Konverteringen fra #307 gjør selve lesingen.</b> Tolkeren kjører <see cref="MonsterStrukturkonverterer"/>
/// over forskriftens noder og plukker utsagnene fra inndelingsmønstrene (<c>inndeling-rettskrets</c>,
/// <c>inndeling-rettssted</c>, <c>inndeling-sogner</c>, <c>inndeling-utgjor</c>). Det eneste som er lagt til i
/// konverteringen er <see cref="Inndeling.UtgjorNavn"/> — se under. Ingen ny parser.
/// </para>
/// <para>
/// <b>Hva tolkeren bestemmer, og etter hvilken regel</b> (alle deterministiske; det som ikke avgjøres, listes i
/// <see cref="Resultat.Uloste"/> — CLAUDE.md §8):
/// </para>
/// <list type="number">
/// <item><b>Kommunenavn → kommunenummer.</b> Eksakt likhet mellom én av tekstens skråstrek-former
/// («Kárášjohka/Karasjok») og Kartverkets <c>kommunenavnNorsk</c>. En form som slutter på « herad» eller
/// « kommune» prøves også uten endelsen («Voss herad» — Brreg-navnet er «VOSS HERAD», Kartverket har «Voss»).
/// Gir det flere kommuner (Herøy, Våler), velges den hvis fylke står som «&lt;fylkesnavn&gt; fylke» i paragrafens
/// første ledd («Buskerud fylke og Innlandet fylke har rettskretsene …») — forskriftens egen avgrensning. Fortsatt
/// flere eller ingen: uløst.</item>
/// <item><b>Rettssted.</b> Er navnet et kommunenavn, er rettsstedet den kommunen — SAMME områdenode som
/// tingretten har ansvar for (Johanns funn 1 på #312: to kanter til samme node, aldri to løse noder). Ellers
/// slås det opp i SSR-øyeblikksbildet, og bare treff i en av kommunene tingretten selv dekker godtas (et rettssted
/// ligger i sin egen rettskrets — antakelse, men den er sjekkbar: et rettssted uten slikt treff listes). Flere
/// treff: navneobjekttypen rangeres By &gt; Tettsted &gt; Tettbebyggelse &gt; Bygdelag (bygd), og det må være
/// nøyaktig ett på beste rang («Ski»: By og Bygdelag, begge i Nordre Follo → By).</item>
/// <item><b>Lagsogn og lagdømme.</b> Lagsognene tas fra «Til lagsognet X sogner …»-leddene, som navngir hvert
/// lagsogn for seg. Lagdømmet er navnet i paragrafens «… utgjør N lagdømme.» (<see cref="Inndeling.UtgjorNavn"/>).
/// Konverteringens egen deling av «utgjør»-lista brukes IKKE til sammensetningen: den deler «Oslo, Asker og Bærum,
/// Buskerud og Søndre Østfold» i fire lagsogn der forskriften har tre, og forkaster «Møre og Romsdal og
/// Trööndelagen/Trøndelag» (målt 2026-10-08) — avvikene listes i <see cref="Resultat.Konverteringsavvik"/>, så de
/// kan rettes i #307-mønsteret.</item>
/// <item><b>Delt kommune (domstolloven § 66 annet ledd).</b> En kommune som ligger under mer enn én tingrett
/// listes i <see cref="Resultat.DelteKommuner"/> med alle tingrettene. Ingen av dem velges; oppslaget viser
/// «ikke entydig».</item>
/// </list>
/// </summary>
public static class DomstolinndelingTolker
{
    public const string ForskriftEli = "https://lovdata.no/eli/forskrift/2021/01/22/163/nor";

    /// <summary>Teksten i § 10 første ledd som er hjemmelen for at lagdømmet har én lagmannsrett.</summary>
    public const string LagmannsrettSetning = "Hvert lagdømme har en lagmannsrett";

    /// <summary>SSR-typene et rettssted kan være, i prioritert rekkefølge (samme sett som fornyelsesverktøyet).</summary>
    public static readonly string[] Bebyggelsestyper = ["By", "Tettsted", "Tettbebyggelse", "Bygdelag (bygd)"];

    public sealed record Kommunetreff(string Tekstform, string Kommunenummer, string Kommunenavn, string Fylkesnavn, string? Avgjort);

    public sealed record Tettstedtreff(string Navn, long Stedsnummer, string Navneobjekttype, string Kommunenummer);

    /// <summary>Et rettssted: enten en kommune (<see cref="Kommunenummer"/>) eller et tettsted i en kommune.</summary>
    public sealed record Rettssted(string Tekstform, string? Kommunenummer, Tettstedtreff? Tettsted);

    public sealed record Tingrett(string Tekstform, string Eid, IReadOnlyList<Kommunetreff> Kommuner, IReadOnlyList<Rettssted> Rettssteder);

    public sealed record Lagsogn(string Navn, string Eid, IReadOnlyList<string> Tingretter);

    public sealed record Lagdomme(string Navn, string Eid, IReadOnlyList<string> Lagsogn);

    public sealed record Resultat(
        IReadOnlyList<Tingrett> Tingretter,
        IReadOnlyList<Lagsogn> Lagsogn,
        IReadOnlyList<Lagdomme> Lagdommer,
        string? LagmannsrettEid,
        IReadOnlyList<string> Uloste,
        IReadOnlyDictionary<string, IReadOnlyList<string>> DelteKommuner,
        IReadOnlyList<string> KommunerUtenTingrett,
        IReadOnlyList<string> Konverteringsavvik);

    public static Resultat Tolk(
        IReadOnlyList<Strukturnode> noder, OmraderegisterKilder.Kartverketsnapshot kartverket, OmraderegisterKilder.SsrSnapshot ssr)
    {
        var dokument = new MonsterStrukturkonverterer().Konverter(
            new Strukturkonverteringsgrunnlag("Forskrift om inndelingen av rettskretser og lagdømmer", ForskriftEli, noder));
        var aktor = dokument.Aktorer.ToDictionary(a => a.Id, a => a.Tekstform);
        string Tekst(string? id) => id is null ? "" : aktor[id];
        IEnumerable<StrukturUtsagn> Fra(string monster) =>
            dokument.Utsagn.Where(u => u.Oppdagelseskilde == "monster:" + monster);

        var uloste = new List<string>();
        var avvik = new List<string>();
        var tekstPerEid = noder.Where(n => n.Tekst is not null).GroupBy(n => n.Eid).ToDictionary(g => g.Key, g => g.First().Tekst!);
        var alleKommuner = kartverket.Kommuner.ToList();

        // ---- Tingrett → kommuner ----
        var tingretter = new List<(string Navn, string Eid, List<Kommunetreff> Kommuner, List<string> Steder)>();
        foreach (var gruppe in Fra("inndeling-rettskrets").GroupBy(u => (Tingrett: Tekst(u.Fra), u.Eid)))
        {
            var kommuner = new List<Kommunetreff>();
            foreach (var u in gruppe)
            {
                var navn = Tekst(u.Til);
                var treff = LosKommune(navn, gruppe.Key.Eid, alleKommuner, tekstPerEid, out var grunn);
                if (treff is null) uloste.Add($"Kommunen «{navn}» i {gruppe.Key.Tingrett} ({gruppe.Key.Eid}): {grunn}");
                else kommuner.Add(treff);
            }
            tingretter.Add((gruppe.Key.Tingrett, gruppe.Key.Eid, kommuner, []));
        }

        // ---- Tingrett → rettssted ----
        foreach (var u in Fra("inndeling-rettssted"))
        {
            var t = tingretter.FirstOrDefault(x => x.Navn == Tekst(u.Fra) && x.Eid == u.Eid);
            if (t.Navn is null)
            {
                uloste.Add($"Rettsstedet «{Tekst(u.Til)}» ({u.Eid}) hører til «{Tekst(u.Fra)}», som ikke har noen kommuneliste i samme ledd.");
                continue;
            }
            t.Steder.Add(Tekst(u.Til));
        }
        var ferdigeTingretter = new List<Tingrett>();
        foreach (var t in tingretter)
        {
            var egneKommuner = t.Kommuner.Select(k => k.Kommunenummer).ToHashSet();
            var steder = new List<Rettssted>();
            foreach (var sted in t.Steder)
            {
                var losning = LosRettssted(sted, egneKommuner, alleKommuner, ssr, out var grunn);
                if (losning is null) uloste.Add($"Rettsstedet «{sted}» for {t.Navn} ({t.Eid}): {grunn}");
                else steder.Add(losning);
            }
            ferdigeTingretter.Add(new Tingrett(t.Navn, t.Eid, t.Kommuner, steder));
        }

        // ---- Delte kommuner og kommuner uten tingrett ----
        var tingrettPerKommune = ferdigeTingretter
            .SelectMany(t => t.Kommuner.Select(k => (k.Kommunenummer, t.Tekstform)))
            .GroupBy(x => x.Kommunenummer)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Tekstform).Distinct().ToList());
        var delte = tingrettPerKommune.Where(kv => kv.Value.Count > 1)
            .ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value);
        var utenTingrett = alleKommuner.Where(k => !tingrettPerKommune.ContainsKey(k.Kommune.Kommunenummer))
            .Select(k => $"{k.Kommune.Kommunenummer} {k.Kommune.KommunenavnNorsk}").ToList();

        // ---- Lagsogn (fra «Til lagsognet X sogner …») ----
        var tingrettNavn = ferdigeTingretter.Select(t => t.Tekstform).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var lagsogn = new List<Lagsogn>();
        foreach (var gruppe in Fra("inndeling-sogner").GroupBy(u => (Navn: Tekst(u.Fra), u.Eid)))
        {
            var medlemmer = gruppe.Select(u => Tekst(u.Til)).ToList();
            foreach (var m in medlemmer.Where(m => !tingrettNavn.Contains(m)))
            {
                uloste.Add($"«{m}» sogner til lagsognet {gruppe.Key.Navn} ({gruppe.Key.Eid}), men står ikke som tingrett i kapittel 1.");
            }
            lagsogn.Add(new Lagsogn(gruppe.Key.Navn, gruppe.Key.Eid, medlemmer.Where(tingrettNavn.Contains).ToList()));
        }
        foreach (var t in ferdigeTingretter)
        {
            var antall = lagsogn.Count(l => l.Tingretter.Contains(t.Tekstform, StringComparer.OrdinalIgnoreCase));
            if (antall != 1) uloste.Add($"{t.Tekstform} sogner til {antall} lagsogn (skal være nøyaktig ett).");
        }

        // ---- Lagdømme (navnet fra «… utgjør N lagdømme.», medlemmene fra sogner-leddene i samme paragraf) ----
        var lagdommer = new List<Lagdomme>();
        foreach (var gruppe in lagsogn.GroupBy(l => Paragraf(l.Eid)))
        {
            var forsteLedd = gruppe.Key + "/ledd-1";
            var tekst = tekstPerEid.GetValueOrDefault(forsteLedd);
            var navn = tekst is null ? null : MonsterStrukturkonverterer.Setninger(tekst).Select(Inndeling.UtgjorNavn).FirstOrDefault(n => n is not null);
            if (navn is null)
            {
                uloste.Add($"Fant ikke «… utgjør N lagdømme» i {forsteLedd} — lagsognene {string.Join(", ", gruppe.Select(l => l.Navn))} er ikke knyttet til et lagdømme.");
                continue;
            }
            var medlemmer = gruppe.Select(l => l.Navn).ToList();
            foreach (var m in medlemmer.Where(m => !tekst!.Contains(m, StringComparison.Ordinal)))
            {
                uloste.Add($"Lagsognet «{m}» står ikke i {forsteLedd} («{tekst}»).");
            }
            var konvertert = Fra("inndeling-utgjor").Where(u => u.Eid == forsteLedd).Select(u => Tekst(u.Til)).ToList();
            if (!konvertert.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(medlemmer))
            {
                avvik.Add($"{forsteLedd}: konverteringen (inndeling-utgjor) ga [{string.Join(" | ", konvertert)}], "
                          + $"sogner-leddene navngir [{string.Join(" | ", medlemmer)}].");
            }
            lagdommer.Add(new Lagdomme(navn, forsteLedd, medlemmer));
        }

        var lagmannsrettEid = noder.FirstOrDefault(n => n.Tekst?.Contains(LagmannsrettSetning, StringComparison.Ordinal) == true)?.Eid;
        if (lagmannsrettEid is null) uloste.Add($"Fant ikke «{LagmannsrettSetning}» i forskriften — ingen lagmannsrett knyttes til lagdømmene.");

        return new Resultat(ferdigeTingretter, lagsogn, lagdommer, lagmannsrettEid, uloste, delte, utenTingrett, avvik);
    }

    /// <summary>«…/§2/ledd-1/punkt-6» → «…/§2».</summary>
    public static string Paragraf(string eid)
    {
        var i = eid.IndexOf("/ledd-", StringComparison.Ordinal);
        return i < 0 ? eid : eid[..i];
    }

    /// <summary>Skråstrek-formene, pluss hver form uten en avsluttende « herad»/« kommune».</summary>
    public static IReadOnlyList<string> Former(string tekstform)
    {
        var former = new List<string>();
        foreach (var del in tekstform.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            former.Add(del);
            foreach (var endelse in new[] { " herad", " kommune" })
            {
                if (del.EndsWith(endelse, StringComparison.Ordinal)) former.Add(del[..^endelse.Length]);
            }
        }
        return former;
    }

    private static Kommunetreff? LosKommune(
        string tekstform, string eid,
        IReadOnlyList<(OmraderegisterKilder.KartverketFylke Fylke, OmraderegisterKilder.KartverketKommune Kommune)> alle,
        IReadOnlyDictionary<string, string> tekstPerEid, out string grunn)
    {
        var former = Former(tekstform);
        var kandidater = alle.Where(k => former.Contains(k.Kommune.KommunenavnNorsk, StringComparer.Ordinal)).ToList();
        if (kandidater.Count == 1)
        {
            grunn = "";
            var k = kandidater[0];
            return new Kommunetreff(tekstform, k.Kommune.Kommunenummer, k.Kommune.KommunenavnNorsk, k.Fylke.Fylkesnavn, null);
        }
        if (kandidater.Count == 0)
        {
            grunn = "ingen kommune i Kartverket-fila har dette navnet (eksakt, per skråstrek-form).";
            return null;
        }
        var forsteLedd = tekstPerEid.GetValueOrDefault(Paragraf(eid) + "/ledd-1") ?? "";
        var iParagrafen = kandidater.Where(k => forsteLedd.Contains(k.Fylke.Fylkesnavn + " fylke", StringComparison.Ordinal)).ToList();
        if (iParagrafen.Count == 1)
        {
            grunn = "";
            var k = iParagrafen[0];
            return new Kommunetreff(tekstform, k.Kommune.Kommunenummer, k.Kommune.KommunenavnNorsk, k.Fylke.Fylkesnavn,
                $"{kandidater.Count} kommuner heter «{k.Kommune.KommunenavnNorsk}» "
                + $"({string.Join(", ", kandidater.Select(x => $"{x.Kommune.Kommunenummer} i {x.Fylke.Fylkesnavn}"))}); "
                + $"paragrafens første ledd nevner «{k.Fylke.Fylkesnavn} fylke».");
        }
        grunn = $"{kandidater.Count} kommuner har navnet ({string.Join(", ", kandidater.Select(x => $"{x.Kommune.Kommunenummer} i {x.Fylke.Fylkesnavn}"))}), "
                + $"og paragrafens første ledd avgrenser til {iParagrafen.Count} av dem.";
        return null;
    }

    private static Rettssted? LosRettssted(
        string tekstform, IReadOnlySet<string> egneKommuner,
        IReadOnlyList<(OmraderegisterKilder.KartverketFylke Fylke, OmraderegisterKilder.KartverketKommune Kommune)> alle,
        OmraderegisterKilder.SsrSnapshot ssr, out string grunn)
    {
        var former = Former(tekstform);
        var kommuner = alle.Where(k => former.Contains(k.Kommune.KommunenavnNorsk, StringComparer.Ordinal)).ToList();
        if (kommuner.Count > 0)
        {
            var egne = kommuner.Where(k => egneKommuner.Contains(k.Kommune.Kommunenummer)).ToList();
            var valgt = egne.Count == 1 ? egne[0] : kommuner.Count == 1 ? kommuner[0] : default;
            if (valgt.Kommune is not null)
            {
                grunn = "";
                return new Rettssted(tekstform, valgt.Kommune.Kommunenummer, null);
            }
            grunn = $"navnet passer {kommuner.Count} kommuner, og tingrettens egne kommuner avgjør det ikke.";
            return null;
        }

        var sok = ssr.Navn.Where(s => former.Contains(s.Sok, StringComparer.Ordinal)).ToList();
        if (sok.Count == 0)
        {
            grunn = "ikke et kommunenavn, og ikke med i SSR-øyeblikksbildet (legg navnet til i fornye-omraderegister.py).";
            return null;
        }
        var kandidater = sok.SelectMany(s => s.Treff)
            .Where(t => Bebyggelsestyper.Contains(t.Navneobjekttype))
            .SelectMany(t => t.Kommuner.Where(k => egneKommuner.Contains(k.Kommunenummer))
                .Select(k => new Tettstedtreff(t.Skrivemate, t.Stedsnummer, t.Navneobjekttype, k.Kommunenummer)))
            .ToList();
        if (kandidater.Count == 0)
        {
            grunn = "SSR har ingen bebyggelse med dette navnet i noen av kommunene tingretten dekker.";
            return null;
        }
        var besteRang = kandidater.Min(k => Array.IndexOf(Bebyggelsestyper, k.Navneobjekttype));
        var beste = kandidater.Where(k => Array.IndexOf(Bebyggelsestyper, k.Navneobjekttype) == besteRang).ToList();
        if (beste.Count != 1)
        {
            grunn = $"SSR har {beste.Count} treff av typen {Bebyggelsestyper[besteRang]} i tingrettens kommuner "
                    + $"({string.Join(", ", beste.Select(b => $"stedsnummer {b.Stedsnummer} i {b.Kommunenummer}"))}).";
            return null;
        }
        grunn = "";
        return new Rettssted(tekstform, null, beste[0]);
    }
}
