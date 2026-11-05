using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #440 PR 1, Johanns avgjørelser 2026-10-30] Rutene en ventende navnekandidat kan havne i. Hver kandidat havner i nøyaktig én,
/// og reglene prøves i denne rekkefølgen (<see cref="Aktorkoregler.Ruter"/>).
/// </summary>
public static class Aktorkoruter
{
    /// <summary>Likt ord som en generisk navneform («departementet», «kommunen», «statsforvalteren»). Standardregelen eller en binding gir
    /// svaret per rettskilde (docs/33 §5.10).</summary>
    public const string Generisk = "generisk";
    /// <summary>Likt ord som en navneform eller et registernavn for en aktør i katalogen.</summary>
    public const string KjentAktor = "kjent_aktor";
    /// <summary>Likt ord som en kjent klasse, rolle, område eller gruppe.</summary>
    public const string KjentBegrep = "kjent_begrep";
    /// <summary>En bøyningsform av et ord i en av de tre rutene over. Arver ALDRI regelen eller bindingen til ordet den ligner.</summary>
    public const string Boyningsform = "boyningsform";
    /// <summary>Ingen av reglene over traff: et navn vi ikke kjenner. Bare disse står i aktørkøen.</summary>
    public const string Aktorko = "aktorko";

    public static readonly string[] Alle = [Generisk, KjentAktor, KjentBegrep, Boyningsform, Aktorko];

    public static string Tekst(string rute) => rute switch
    {
        Generisk => "generisk ord",
        KjentAktor => "kjent aktør",
        KjentBegrep => "kjent klasse, rolle eller område",
        Boyningsform => "bøyningsform av et kjent ord",
        Aktorko => "aktørkøen",
        _ => rute,
    };
}

/// <summary>Ett kjent ord i rutingstabellen.</summary>
/// <param name="Term">Ordet slik det står i katalogen.</param>
/// <param name="Art"><c>generisk</c>, <c>navneform</c>, <c>registernavn</c>, eller nodetypen (<c>klasse</c>, <c>rolle</c>, <c>omrade</c>,
/// <c>gruppe</c>).</param>
/// <param name="Navn">Det ordet peker på: aktørens registernavn, begrepets term, eller termen selv for et generisk ord.</param>
/// <param name="LovkildeId">For et lovspesifikt begrep: loven det gjelder i (se <see cref="Rutingstabell"/>).</param>
/// <param name="Standardregel">For et generisk ord: standardregelens navn, eller null når ordet ikke har en.</param>
public sealed record KjentOrd(string Term, string Art, string Navn, Guid? VirksomhetId, Guid? BegrepId, Guid? LovkildeId, string? Standardregel);

/// <summary>
/// [Ny, issue #440] Det rutingen slår opp i, lest én gang (<see cref="AktorkoTjeneste.LastRutingstabellAsync"/>) og deretter brukt av
/// rene funksjoner. Nøklene er normaliserte (<see cref="Aktorkoregler.Normaliser"/>).
/// <para>
/// <b>Lovspesifikke begreper</b> (klasse, rolle eller område med <see cref="KjentOrd.LovkildeId"/>) gjelder bare i rettskilden selv og i
/// rettskildene som har hjemmel i den loven (<see cref="HjemmelLover"/>). Samme regel som <c>Aktorklassifiserer</c> i aktørsveipet (#378):
/// «departementet» (rolle i helse- og omsorgstjenesteloven) ble «kjent» i barnehageloven, og det var en gjetning. Uten denne regelen ville
/// «kommunene» i barnehageloven blitt «klassen kommunene (reindriftsloven)».
/// </para>
/// </summary>
public sealed class Rutingstabell(
    IReadOnlyDictionary<string, KjentOrd> generiske,
    IReadOnlyDictionary<string, IReadOnlyList<KjentOrd>> aktorer,
    IReadOnlyDictionary<string, IReadOnlyList<KjentOrd>> begreper,
    IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> hjemmelLover)
{
    public IReadOnlyDictionary<string, KjentOrd> Generiske { get; } = generiske;
    public IReadOnlyDictionary<string, IReadOnlyList<KjentOrd>> Aktorer { get; } = aktorer;
    public IReadOnlyDictionary<string, IReadOnlyList<KjentOrd>> Begreper { get; } = begreper;
    /// <summary>Per rettskilde: lovene den har hjemmel i (<c>rettskilde_hjemler</c>).</summary>
    public IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> HjemmelLover { get; } = hjemmelLover;

    /// <summary>Begrepene for nøkkelen som gjelder i rettskilden: de lovuavhengige, og de lovspesifikke for rettskilden selv eller en lov den
    /// har hjemmel i.</summary>
    public IReadOnlyList<KjentOrd> BegreperI(string nokkel, Guid rettskildeId)
    {
        if (!Begreper.TryGetValue(nokkel, out var alle)) return [];
        HjemmelLover.TryGetValue(rettskildeId, out var lover);
        return alle.Where(b => b.LovkildeId is not { } lov || lov == rettskildeId || (lover?.Contains(lov) ?? false)).ToList();
    }
}

/// <summary>Svaret fra rutingen for én tekst.</summary>
/// <param name="Rute">En av <see cref="Aktorkoruter"/>.</param>
/// <param name="Normalisert">Teksten normalisert (<see cref="Aktorkoregler.Normaliser"/>).</param>
/// <param name="Treff">Det kjente ordet (eller ordene) som traff. Tom for aktørkøen.</param>
/// <param name="BoyningAvRute">For en bøyningsform: ruten til ordet den ligner (generisk, kjent aktør eller kjent begrep).</param>
/// <param name="Ligner">For en bøyningsform: det normaliserte, kjente ordet den ligner.</param>
/// <param name="Suffiks">For en bøyningsform: forskjellen, med fortegn: «+r» (kandidaten har suffikset i tillegg), «−et» (det kjente ordet har
/// det).</param>
public sealed record Rutingssvar(
    string Rute, string Normalisert, IReadOnlyList<KjentOrd> Treff, string? BoyningAvRute = null, string? Ligner = null, string? Suffiks = null)
{
    /// <summary>Standardregelen som gjelder for ordet: bare for et eksakt generisk ord. En bøyningsform arver den ALDRI (#440).</summary>
    public string? Standardregel => Rute == Aktorkoruter.Generisk ? Treff.FirstOrDefault()?.Standardregel : null;
}

/// <summary>Én forekomst i køen: én ventende navnekandidat.</summary>
/// <param name="Gruppe">[Ny, #440 PR 2] <see cref="NavnekandidatEntitet.AktorkoGruppe"/>: navnet et menneske har lagt forekomsten under («skill
/// ut» / «ta med»). Null = beregnet av regelen.</param>
/// <param name="Frase">[Ny, #440, Johanns avgjørelse 2026-11-05] Navnet med forleddet foran slik det står i teksten («felles samarbeidsutvalg»),
/// når forleddssperren slo til for forekomsten (<see cref="Aktorkoregler.SperretFrase"/>). Null = ingen sperre; da er navnet bare
/// <see cref="Tekst"/>.</param>
public sealed record KoForekomst(
    Guid NavnekandidatId, string Tekst, Guid RettskildeId, string NodeEid, int Start, int Slutt, string Kategori, string? Oppdagelseskilde,
    string? Konfidens, string? Gruppe = null, string? Frase = null)
{
    /// <summary>Navnet forekomsten står for, slik det står i teksten: frasen med forleddet når sperren slo til, ellers teksten.</summary>
    public string Navnetekst => Frase ?? Tekst;

    /// <summary>[ENDRET, #440, 2026-11-05] Regnes av <see cref="Navnetekst"/>, ikke bare <see cref="Tekst"/>, og ved hvert kall (ikke lagret
    /// ved konstruksjon): ellers ville <c>with { Frase = … }</c> beholdt den gamle verdien.</summary>
    public string Normalisert => Aktorkoregler.Normaliser(Navnetekst);
}

/// <summary>Én skrivemåte av en variant, slik den står i teksten («Foreldrerådet» og «foreldrerådet» er to skrivemåter av samme ord).</summary>
public sealed record Skrivemate(string Tekst, int Forekomster);

/// <summary>Én variant (ett normalisert ord) i et navn.</summary>
/// <param name="Regel"><c>samme_ord</c> for navnets eget ord, <c>boyning</c> for en bøyningsform slått sammen etter regel 2, <c>manuell</c> for
/// et ord et menneske har tatt med i navnet («ta med», #440 PR 2) uten at bøyningsregelen binder det til navnets ord.</param>
/// <param name="BoyningAv">For en bøyningsform: varianten den er slått sammen med (kan være en annen bøyningsform enn navnets eget ord).</param>
/// <param name="Suffiks">For en bøyningsform: forskjellen mot <see cref="BoyningAv"/>, med fortegn («+r», «−et»).</param>
public sealed record Kovariant(string Normalisert, string Regel, string? BoyningAv, string? Suffiks, int Forekomster, IReadOnlyList<Skrivemate> Skrivemater);

/// <summary>Ett navn i aktørkøen: varianter slått sammen automatisk (regel 1 og 2) og alle forekomstene.</summary>
/// <param name="Nokkel">Det normaliserte ordet med flest forekomster (likhet: det første i ordinal rekkefølge). Stabil så lenge tallene er det;
/// <see cref="AktorkoTjeneste.DetaljAsync"/> slår opp på hvilken som helst variant.</param>
/// <param name="Navn">Den vanligste skrivemåten av nøkkelen (likhet: liten forbokstav først, så ordinal rekkefølge).</param>
/// <param name="Manuell">[Ny, #440 PR 2] Gruppen er satt av et menneske (minst én forekomst har <see cref="KoForekomst.Gruppe"/>), ikke bare
/// beregnet. Vises, og kan tilbakestilles.</param>
public sealed record Kogruppe(string Nokkel, string Navn, IReadOnlyList<Kovariant> Varianter, IReadOnlyList<KoForekomst> Forekomster, bool Manuell = false)
{
    public int Rettskilder => Forekomster.Select(f => f.RettskildeId).Distinct().Count();
}

/// <summary>Regel 3: «barnehagens foreldreråd» (genitivforledd) mot navnet «foreldrerådet». Bare et forslag — slås aldri sammen automatisk.</summary>
/// <param name="FraNokkel">Navnet med genitivforleddet (står som eget navn i køen).</param>
/// <param name="FraVariant">Varianten med genitivforleddet.</param>
/// <param name="TilNokkel">Navnet etterleddet ligner.</param>
/// <param name="Etterledd">Ordet etter genitiven.</param>
public sealed record Genitivforslag(string FraNokkel, string FraVariant, string TilNokkel, string Etterledd, int Forekomster);

/// <summary>En node hjemmelregelen leser.</summary>
public sealed record HjemmelNode(Guid RettskildeId, string Eid, string Tekst, bool ErMerknad, int Rekkefolge);

/// <summary>Et hjemmelforslag etter regelen i <see cref="Aktorkoregler.FinnHjemmelforslag"/>. Bare et forslag i PR 1.</summary>
/// <param name="Uttrykk">Opprettelsesuttrykket som traff (<see cref="Aktorkoregler.Opprettelsesuttrykk"/>).</param>
/// <param name="Setning">Setningen med uttrykket og navnet.</param>
/// <param name="Treff">Navnet slik det står i setningen («foreldreråd»).</param>
/// <param name="TreffStart">Treffets posisjon i <see cref="Setning"/>, for markering.</param>
/// <param name="Kunnskapsstatus"><c>tekstfunn</c> når setningen har en av variantene ordrett, <c>normalisering</c> når den har en annen
/// bøyningsform (typisk ubestemt form: «skal … ha et foreldreråd»).</param>
public sealed record Hjemmelforslag(
    Guid RettskildeId, string Eid, string Uttrykk, string Setning, string Treff, int TreffStart, string Kunnskapsstatus);

/// <summary>[Ny, #440 PR 2] Et treff etter hjemmelregelen: forslaget, eller grunnen til at forleddssperren stoppet det.</summary>
public sealed record Hjemmeltreff(Hjemmelforslag Forslag, string? Sperret);

/// <summary>
/// [Ny, issue #440 PR 1, Johanns avgjørelser 2026-10-30] De rene reglene bak aktørkøen: ruting, gruppering og hjemmelforslag. Ingen
/// databasekall, ingen tilstand — samme inndata gir samme svar hver gang (AK2), og testene kjører uten database.
/// <para>
/// <b>Hvorfor en kø per navn.</b> Johann 2026-10-30: «gjentagelsen skjer vel på forslagsidene … altså før aktøren er opprettet». 1 211
/// ventende navnekandidater var én rad per forekomst, og det samme navnet måtte godkjennes én gang per forekomst, på fem ulike steder. Målt
/// samme dag: 818 av de 1 211 er ord vi alt kjenner (generiske ord, kjente aktører, klasser, bøyningsformer av dem); de 393 som er igjen,
/// er 192 navn. Treffene beregnes ved lesing. Ingenting flyttes (Johanns avgjørelse).
/// </para>
/// </summary>
public static partial class Aktorkoregler
{
    /// <summary>
    /// [LÅST, #440] Den lukkede suffikslisten for bøyningsformer. Lista er lengst-først bare for lesbarhet; regelen sjekker alle.
    /// </summary>
    public static readonly IReadOnlyList<string> Boyningssuffikser =
        ["ene", "ane", "ens", "ets", "en", "et", "er", "ar", "ne", "a", "e", "n", "r", "t", "s"];

    /// <summary>[LÅST, #440] Stammen (det korteste av de to ordene) må ha minst så mange tegn.</summary>
    public const int MinsteStamme = 4;

    /// <summary>Samme ord (regel 1): trimmet, mellomrom slått sammen, uten forskjell på store og små bokstaver.</summary>
    public static string Normaliser(string tekst) => FlereMellomrom().Replace(tekst.Trim(), " ").ToLowerInvariant();

    /// <summary>
    /// Regel 2 for to normaliserte ord: er det ene ordet det andre pluss ett suffiks fra <see cref="Boyningssuffikser"/>, og har det korteste
    /// minst <see cref="MinsteStamme"/> tegn? Returnerer suffikset med fortegn sett fra <paramref name="b"/> («+r»: <paramref name="b"/> har
    /// suffikset i tillegg; «−et»: <paramref name="a"/> har det), eller null.
    /// <para>
    /// To ulike suffikser på samme stamme gir IKKE en bøyning: «departementene» og «departementet» er ikke slått sammen. Det er bevisst; det
    /// ene er flertall, det andre entall, og det første står derfor som eget navn.
    /// </para>
    /// </summary>
    public static string? BoyningSuffiks(string a, string b)
    {
        if (a.Length == b.Length) return null;
        var (kort, lang, fortegn) = a.Length < b.Length ? (a, b, "+") : (b, a, "−");
        if (kort.Length < MinsteStamme || !lang.StartsWith(kort, StringComparison.Ordinal)) return null;
        var suffiks = lang[kort.Length..];
        return Boyningssuffikser.Contains(suffiks) ? fortegn + suffiks : null;
    }

    /// <summary>Kandidatene ordet kan være en bøyningsform av: ordet uten et suffiks, eller ordet med et suffiks.</summary>
    private static IEnumerable<string> Boyningskandidater(string ord)
    {
        foreach (var s in Boyningssuffikser)
        {
            if (ord.Length >= MinsteStamme) yield return ord + s;
            if (ord.EndsWith(s, StringComparison.Ordinal) && ord.Length - s.Length >= MinsteStamme) yield return ord[..^s.Length];
        }
    }

    /// <summary>
    /// [LÅST, #440] Rutingen for én tekst, i rekkefølge: (1) likt ord som en generisk navneform → (2) likt ord som en aktørs navneform eller
    /// registernavn → (3) likt ord som en kjent klasse, rolle, område eller gruppe som gjelder i rettskilden → (4) en bøyningsform av et ord i
    /// 1–3, prøvd i samme rekkefølge → (5) aktørkøen.
    /// <para>
    /// En bøyningsform arver ALDRI regelen eller bindingen til ordet den ligner: «et departement» er ikke «departementet», og «kommuner» er
    /// ikke klassen «kommune». <see cref="Rutingssvar.Standardregel"/> er derfor null for den, og ruten er en egen rute, ikke ordets.
    /// </para>
    /// <para>
    /// Ved flere treff i samme tabell velges det første i ordinal rekkefølge, så svaret er det samme hver gang. Alle treffene står i svaret.
    /// </para>
    /// </summary>
    public static Rutingssvar Ruter(Rutingstabell tabell, string tekst, Guid rettskildeId)
    {
        var k = Normaliser(tekst);
        if (tabell.Generiske.TryGetValue(k, out var g)) return new(Aktorkoruter.Generisk, k, [g]);
        if (tabell.Aktorer.TryGetValue(k, out var a) && a.Count > 0) return new(Aktorkoruter.KjentAktor, k, a);
        var b = tabell.BegreperI(k, rettskildeId);
        if (b.Count > 0) return new(Aktorkoruter.KjentBegrep, k, b);

        var kandidater = Boyningskandidater(k).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();
        foreach (var x in kandidater)
        {
            if (tabell.Generiske.TryGetValue(x, out var gx))
                return new(Aktorkoruter.Boyningsform, k, [gx], Aktorkoruter.Generisk, x, BoyningSuffiks(x, k));
        }
        foreach (var x in kandidater)
        {
            if (tabell.Aktorer.TryGetValue(x, out var ax) && ax.Count > 0)
                return new(Aktorkoruter.Boyningsform, k, ax, Aktorkoruter.KjentAktor, x, BoyningSuffiks(x, k));
        }
        foreach (var x in kandidater)
        {
            var bx = tabell.BegreperI(x, rettskildeId);
            if (bx.Count > 0) return new(Aktorkoruter.Boyningsform, k, bx, Aktorkoruter.KjentBegrep, x, BoyningSuffiks(x, k));
        }
        return new(Aktorkoruter.Aktorko, k, []);
    }

    /// <summary>Regel 2 på ord med flere ledd: likt antall ord, like forledd, og det siste ordet er en bøyningsform.</summary>
    public static string? FlerordsBoyning(string a, string b)
    {
        var wa = a.Split(' ');
        var wb = b.Split(' ');
        if (wa.Length != wb.Length) return null;
        for (var i = 0; i < wa.Length - 1; i++) if (wa[i] != wb[i]) return null;
        return BoyningSuffiks(wa[^1], wb[^1]);
    }

    /// <summary>
    /// [LÅST, #440] Grupperingen av forekomstene i aktørkøen til navn. To nivåer slås sammen automatisk, og regelen står på varianten:
    /// <list type="number">
    /// <item><b>Samme ord:</b> <see cref="Normaliser"/>. «Foreldrerådet» = «foreldrerådet».</item>
    /// <item><b>Bøyning:</b> <see cref="FlerordsBoyning"/>. Sammenslåingen er transitiv (en sammenhengende kjede av bøyninger blir ett navn).</item>
    /// </list>
    /// [Ny, Johanns avgjørelse 2026-11-05, #440] <b>Forleddssperren gjelder også her</b>, med samme regel som hjemmelregelen: en forekomst
    /// der sperren slo til (<see cref="KoForekomst.Frase"/>, regnet av <see cref="SperretFrase"/>), grupperes som hele frasen («felles
    /// samarbeidsutvalg»). Frasen har et ord mer enn «samarbeidsutvalget», så regel 2 slår dem aldri sammen, og den står som eget navn.
    /// <para>
    /// Det tredje nivået, genitivforledd, er bare et forslag (<see cref="FinnGenitivforslag"/>): «domstolens leder» slås aldri sammen med
    /// «leder».
    /// </para>
    /// <para>
    /// Resultatet avhenger ikke av rekkefølgen på inndataene: komponentene er de samme uansett, nøkkelen er valgt etter antall og ordinal
    /// rekkefølge, og lista er sortert på antall forekomster og så nøkkel.
    /// </para>
    /// </summary>
    public static IReadOnlyList<Kogruppe> Grupper(IEnumerable<KoForekomst> forekomster)
    {
        // [Ny, #440 PR 2] Menneskets valg først: en forekomst med Gruppe står i det navnet, og en forekomst uten med et ord som alt står i et
        // slikt navn, følger med (ny forekomst av samme ord etter et nytt sveip). Resten grupperes av regelen som før. Ved et ord i flere
        // manuelle navn velges det første i ordinal rekkefølge, så svaret er det samme hver gang.
        var alle = forekomster.ToList();
        var manuelle = alle.Where(f => f.Gruppe is not null)
            .GroupBy(f => Normaliser(f.Gruppe!), StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var ordTilManuell = new Dictionary<string, string>(StringComparer.Ordinal);
        // Navnets eget ord først: «ta med» kan legge «styrets leder» under «styret» før noen forekomst av «styret» selv er festet.
        foreach (var navn in manuelle.Keys) ordTilManuell.TryAdd(navn, navn);
        foreach (var (navn, medlemmer) in manuelle)
        {
            foreach (var f in medlemmer) ordTilManuell.TryAdd(f.Normalisert, navn);
        }
        foreach (var f in alle.Where(f => f.Gruppe is null && ordTilManuell.ContainsKey(f.Normalisert)))
        {
            manuelle[ordTilManuell[f.Normalisert]].Add(f);
        }

        var grupper = new List<Kogruppe>();
        var beregnet = alle.Where(f => f.Gruppe is null && !ordTilManuell.ContainsKey(f.Normalisert)).ToList();
        var perOrd = beregnet.GroupBy(f => f.Normalisert, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var ord = perOrd.Keys.OrderBy(o => o, StringComparer.Ordinal).ToList();

        var forelder = ord.ToDictionary(o => o, o => o, StringComparer.Ordinal);
        string Rot(string o) { while (forelder[o] != o) o = forelder[o]; return o; }
        for (var i = 0; i < ord.Count; i++)
        {
            for (var j = i + 1; j < ord.Count; j++)
            {
                if (FlerordsBoyning(ord[i], ord[j]) is null) continue;
                var (ri, rj) = (Rot(ord[i]), Rot(ord[j]));
                if (ri != rj) forelder[string.CompareOrdinal(ri, rj) < 0 ? rj : ri] = string.CompareOrdinal(ri, rj) < 0 ? ri : rj;
            }
        }
        foreach (var komponent in ord.GroupBy(Rot, StringComparer.Ordinal))
        {
            grupper.Add(LagGruppe(komponent.SelectMany(o => perOrd[o]).ToList(), null, false));
        }
        foreach (var (navn, medlemmer) in manuelle) grupper.Add(LagGruppe(medlemmer, navn, true));
        return grupper.OrderByDescending(g => g.Forekomster.Count).ThenBy(g => g.Nokkel, StringComparer.Ordinal).ToList();
    }

    /// <summary>Ett navn av forekomstene: nøkkelen (det ønskede ordet når det står blant ordene, ellers ordet med flest forekomster), og
    /// regelen per variant (bredde først fra nøkkelen langs bøyningene). Et ord som ikke nås langs en bøyning, er tatt med av et menneske.</summary>
    private static Kogruppe LagGruppe(List<KoForekomst> forekomster, string? onsketNokkel, bool manuell)
    {
        var perOrd = forekomster.GroupBy(f => f.Normalisert, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var medlemmer = perOrd.Keys.OrderBy(o => o, StringComparer.Ordinal).ToList();
        var nokkel = onsketNokkel is not null && perOrd.ContainsKey(onsketNokkel)
            ? onsketNokkel
            : medlemmer.OrderByDescending(o => perOrd[o].Count).ThenBy(o => o, StringComparer.Ordinal).First();

        var regel = new Dictionary<string, (string Av, string Suffiks)>(StringComparer.Ordinal);
        var ko = new Queue<string>([nokkel]);
        var sett = new HashSet<string>(StringComparer.Ordinal) { nokkel };
        while (ko.Count > 0)
        {
            var o = ko.Dequeue();
            foreach (var n in medlemmer)
            {
                if (sett.Contains(n) || FlerordsBoyning(o, n) is not { } suffiks) continue;
                sett.Add(n);
                regel[n] = (o, suffiks);
                ko.Enqueue(n);
            }
        }

        var varianter = medlemmer
            .OrderBy(o => o == nokkel ? 0 : 1).ThenByDescending(o => perOrd[o].Count).ThenBy(o => o, StringComparer.Ordinal)
            .Select(o => o == nokkel
                ? new Kovariant(o, "samme_ord", null, null, perOrd[o].Count, Skrivemater(perOrd[o]))
                : regel.TryGetValue(o, out var r)
                    ? new Kovariant(o, "boyning", r.Av, r.Suffiks, perOrd[o].Count, Skrivemater(perOrd[o]))
                    : new Kovariant(o, "manuell", null, null, perOrd[o].Count, Skrivemater(perOrd[o])))
            .ToList();
        var alle = forekomster.OrderBy(f => f.RettskildeId).ThenBy(f => f.NodeEid, StringComparer.Ordinal).ThenBy(f => f.Start).ToList();
        return new Kogruppe(nokkel, varianter[0].Skrivemater[0].Tekst, varianter, alle, manuell);
    }

    /// <summary>Skrivemåtene, vanligste først. Likhet: liten forbokstav først (en stor forbokstav er ofte bare setningsstart), så ordinal.
    /// [ENDRET, #440, 2026-11-05] Av <see cref="KoForekomst.Navnetekst"/>: med forleddet når sperren slo til, så «felles samarbeidsutvalg» kan
    /// godkjennes som det står i teksten.</summary>
    private static List<Skrivemate> Skrivemater(IEnumerable<KoForekomst> forekomster) => forekomster
        .GroupBy(f => FlereMellomrom().Replace(f.Navnetekst.Trim(), " "), StringComparer.Ordinal)
        .Select(g => new Skrivemate(g.Key, g.Count()))
        .OrderByDescending(s => s.Forekomster).ThenBy(s => s.Tekst.Length > 0 && char.IsUpper(s.Tekst[0])).ThenBy(s => s.Tekst, StringComparer.Ordinal)
        .ToList();

    /// <summary>
    /// [LÅST, #440] Regel 3, bare forslag: en variant med to eller flere ord der det første ender på «s» (genitiv), og resten er samme ord
    /// eller en bøyningsform (<see cref="FlerordsBoyning"/>) av en variant i et ANNET navn. Målt 2026-10-30: 10 forslag, og 5 av dem ville
    /// vært gale som automatikk («domstolens leder» og «nemndas leder» mot «leder», «lånekassens klagenemnd» mot «klagenemnda»).
    /// </summary>
    public static IReadOnlyList<Genitivforslag> FinnGenitivforslag(IReadOnlyList<Kogruppe> grupper)
    {
        var ut = new List<Genitivforslag>();
        var gruppeFor = grupper.SelectMany(g => g.Varianter.Select(v => (v.Normalisert, g.Nokkel)))
            .ToDictionary(x => x.Normalisert, x => x.Nokkel, StringComparer.Ordinal);
        foreach (var g in grupper)
        {
            foreach (var v in g.Varianter)
            {
                var ordene = v.Normalisert.Split(' ');
                if (ordene.Length < 2 || !ordene[0].EndsWith('s')) continue;
                // [Ny, #440, 2026-11-05] En variant forleddssperren har skilt ut («felles samarbeidsutvalg»), er avgjort som et annet navn. «felles»
                // ender på «s» uten å være genitiv, og et forslag om å slå den sammen igjen ville motsagt sperren.
                if (g.Forekomster.Where(f => f.Normalisert == v.Normalisert).All(f => f.Frase is not null)) continue;
                var etterledd = string.Join(' ', ordene[1..]);
                var mal = gruppeFor
                    .Where(x => x.Value != g.Nokkel && (x.Key == etterledd || FlerordsBoyning(x.Key, etterledd) is not null))
                    .Select(x => x.Value).Distinct().OrderBy(x => x, StringComparer.Ordinal);
                foreach (var til in mal) ut.Add(new Genitivforslag(g.Nokkel, v.Normalisert, til, etterledd, v.Forekomster));
            }
        }
        return ut;
    }

    /// <summary>
    /// [ENDRET, #440 PR 2, presisert etter juristrunde-440 (data/fasit/strukturmodell/juristrunde-440/sammendrag.md), der juristen og
    /// modellereren ble enige] Den lukkede lista over opprettelsesuttrykk. Begge styrer en nominalfrase, og navnet må være KJERNEN i den
    /// (<see cref="FinnHjemmeltreff"/>):
    /// <list type="bullet">
    /// <item><c>skal_ha</c>: «skal … ha et/en/ei/ett (eller flere)», høyst seks ord mellom «skal» og «ha» («skal hver barnehage ha et
    /// foreldreråd»).</item>
    /// <item><c>skal_vaere</c> (nytt): «det skal være et …» og «skal det … være et …» («I hver kommune skal det være et forliksråd»).
    /// Artikkelen er valgfri her, så «det skal være felles samarbeidsutvalg» blir et treff som forleddssperren stopper.</item>
    /// </list>
    /// [FJERNET, juristrunden] «opprettes», «oppnevner» og «etableres» med navnet hvor som helst i setningen: 0 av 10 forslag holdt.
    /// Oppnevning, valg, utnevnelse og tilsetting gir aldri forslag; de er kompetanse og forutsetter at organet finnes.
    /// [Ikke med før de er målt (KONTROLLER)]: «det opprettes/etableres/nedsettes et X», «skal/kan opprette et X», «ledes av et X».
    /// </summary>
    public static readonly IReadOnlyList<(string Kode, Regex Monster)> Opprettelsesuttrykk =
    [
        ("skal_ha", SkalHa()),
        ("skal_vaere", SkalVaere()),
    ];

    public static string UttrykkTekst(string kode) => kode switch
    {
        "skal_ha" => "skal … ha et",
        "skal_vaere" => "det skal være et",
        _ => kode,
    };

    /// <summary>[LÅST, juristrunde-440 punkt 1] Ordene som avslutter nominalfrasen: preposisjoner (lukket liste), «som», «at» og «enn», og
    /// noen få ord som innleder en leddsetning. Komma og andre skilletegn avslutter den også.</summary>
    public static readonly IReadOnlySet<string> Frasestopp = new HashSet<string>(StringComparer.Ordinal)
    {
        "som", "at", "enn", "å",
        "i", "på", "for", "til", "av", "med", "fra", "om", "under", "over", "ved", "etter", "mot", "hos", "uten", "mellom", "innen", "blant",
        "gjennom", "overfor", "per", "pr", "hvor", "der", "når", "slik",
    };

    /// <summary>Sideordning i frasen: «og»/«eller», med valgfri ny artikkel.</summary>
    private static readonly IReadOnlySet<string> Sideordning = new HashSet<string>(StringComparer.Ordinal) { "og", "eller" };
    private static readonly IReadOnlySet<string> Artikler = new HashSet<string>(StringComparer.Ordinal) { "et", "en", "ei", "ett", "ein" };

    /// <summary>[LÅST, juristrunde-440 punkt 1] Høyst så mange ord per ledd i frasen.</summary>
    public const int MaksOrdPerLedd = 3;

    /// <summary>
    /// [LÅST, juristrunde-440 punkt 3, forleddssperren] Ordene som kan stå foran navnet i leddet uten at det blir et annet organ: tallord og
    /// mengdeord («nitten andre dommere»). Alt annet foran navnet sperrer: «felles forliksråd» og «felles samarbeidsutvalg» er andre organer
    /// enn «forliksrådet» og «samarbeidsutvalget». Lista er lukket, så et ord som ikke står her, sperrer heller enn å gjette.
    /// </summary>
    public static readonly IReadOnlySet<string> TillatteForledd = new HashSet<string>(StringComparer.Ordinal)
    {
        "to", "tre", "fire", "fem", "seks", "sju", "syv", "åtte", "ni", "ti", "elleve", "tolv", "tretten", "fjorten", "femten", "seksten",
        "sytten", "atten", "nitten", "tjue", "andre", "annen", "annet", "flere", "mange", "så", "egen", "eget", "egne",
    };

    /// <summary>Setningene i en tekst: delt etter punktum, utropstegn, spørsmålstegn eller semikolon fulgt av mellomrom.</summary>
    public static IEnumerable<(int Start, string Tekst)> Setninger(string tekst)
    {
        var start = 0;
        foreach (Match m in Setningsskille().Matches(tekst))
        {
            yield return (start, tekst[start..m.Index]);
            start = m.Index + m.Length;
        }
        if (start < tekst.Length) yield return (start, tekst[start..]);
    }

    /// <summary>
    /// [LÅST, #440 PR 2] Hjemmelforslaget for et navn: setninger i <paramref name="noder"/> (rettskildene der navnet står) der et
    /// <see cref="Opprettelsesuttrykk"/> styrer en nominalfrase med navnet som kjerne (<see cref="FinnHjemmeltreff"/>). Treff som stoppes av
    /// forleddssperren, er ikke forslag.
    /// <para>
    /// <b>Merknader og veiledning er aldri med</b> (<see cref="RettskildeNodeEntitet.ErMerknad"/>, #419): de kan aldri være hjemmel. Finnes det
    /// ingen bestemmelse, er svaret tomt, og hullet vises. Ingenting gjettes inn i stedet (CLAUDE.md §8).
    /// </para>
    /// </summary>
    public static IReadOnlyList<Hjemmelforslag> FinnHjemmelforslag(IEnumerable<string> varianter, IEnumerable<HjemmelNode> noder) =>
        FinnHjemmeltreff(varianter, noder).Where(t => t.Sperret is null).Select(t => t.Forslag).ToList();

    /// <summary>
    /// Alle treffene regelen gir, også de forleddssperren stopper (<see cref="Hjemmeltreff.Sperret"/>), så sperren kan vises og måles.
    /// Fremgangsmåten per setning og uttrykk:
    /// <list type="number">
    /// <item>Frasen begynner rett etter artikkelen og går fram til første ord i <see cref="Frasestopp"/>, et skilletegn eller setningsslutt.</item>
    /// <item>Frasen deles i ledd ved «og»/«eller» (med valgfri ny artikkel). Et ledd med mer enn <see cref="MaksOrdPerLedd"/> ord avslutter
    /// frasen: da er det ikke en enkel nominalfrase.</item>
    /// <item>Kjernen er de siste ordene i leddet: navnet (samme ord eller en bøyningsform, flerordsnavn med like forledd). Ord foran kjernen
    /// må stå i <see cref="TillatteForledd"/>; ellers er treffet sperret.</item>
    /// </list>
    /// Ett treff per setning. Kunnskapsstatusen er <c>tekstfunn</c> når kjernen står ordrett som en av variantene, ellers <c>normalisering</c>.
    /// Opprettelsen står i teksten i begge tilfeller, fordi uttrykket er selve opprettelsen (juristrunde-440 punkt 5).
    /// </summary>
    public static IReadOnlyList<Hjemmeltreff> FinnHjemmeltreff(IEnumerable<string> varianter, IEnumerable<HjemmelNode> noder)
    {
        var former = varianter.Distinct(StringComparer.Ordinal).Select(v => v.Split(' ')).ToList();
        var ut = new List<Hjemmeltreff>();
        foreach (var node in noder.Where(n => !n.ErMerknad).OrderBy(n => n.RettskildeId).ThenBy(n => n.Rekkefolge).ThenBy(n => n.Eid, StringComparer.Ordinal))
        {
            foreach (var (_, setning) in Setninger(node.Tekst))
            {
                Hjemmeltreff? funnet = null;
                foreach (var (kode, monster) in Opprettelsesuttrykk)
                {
                    foreach (Match m in monster.Matches(setning))
                    {
                        if (FinnKjerne(setning, m.Index + m.Length, former) is not { } k) continue;
                        funnet = new Hjemmeltreff(
                            new Hjemmelforslag(node.RettskildeId, node.Eid, kode, setning, k.Tekst, k.Pos,
                                k.Ordrett ? Strukturkanter.Tekstfunn : Strukturkanter.Normalisering),
                            k.Sperret);
                        if (k.Sperret is null) break;
                    }
                    if (funnet is { Sperret: null }) break;
                }
                if (funnet is not null) ut.Add(funnet);
            }
        }
        return ut;
    }

    private static (int Pos, string Tekst, bool Ordrett, string? Sperret)? FinnKjerne(string setning, int fra, List<string[]> former)
    {
        (int, string, bool, string?)? sperret = null;
        foreach (var ledd in Fraseledd(setning, fra))
        {
            if (Kjerne(setning, ledd, former) is { } k)
            {
                if (k.Sperret is null) return k;
                sperret ??= k;
            }
        }
        return sperret;
    }

    /// <summary>
    /// [Ny, #440, 2026-11-05: skilt ut av <c>FinnKjerne</c> uten endring i oppførselen, så grupperingen bruker nøyaktig samme frase som
    /// hjemmelregelen] Leddene i nominalfrasen som begynner ved <paramref name="fra"/> (rett etter uttrykket): fram til første ord i
    /// <see cref="Frasestopp"/>, et skilletegn eller setningsslutt, delt ved «og»/«eller» med valgfri ny artikkel. Et tomt ledd eller et ledd
    /// med mer enn <see cref="MaksOrdPerLedd"/> ord avslutter frasen og gis ikke ut.
    /// </summary>
    private static IEnumerable<List<Match>> Fraseledd(string setning, int fra)
    {
        // Tokens etter uttrykket: ord, eller ett tegn som ikke er en bokstav eller mellomrom (skilletegn og tall avslutter frasen).
        var tokens = new List<Match>();
        for (var m = Frasetokens().Match(setning, fra); m.Success; m = m.NextMatch()) tokens.Add(m);
        var ledd = new List<Match>();
        var i = 0;
        while (true)
        {
            var ord = i < tokens.Count ? tokens[i].Value.ToLowerInvariant() : null;
            var slutt = ord is null || !char.IsLetter(ord[0]) || Frasestopp.Contains(ord);
            var deler = !slutt && Sideordning.Contains(ord!);
            if (slutt || deler)
            {
                if (ledd.Count == 0 || ledd.Count > MaksOrdPerLedd) yield break;
                yield return ledd;
                if (slutt) yield break;
                ledd = [];
                i++;
                if (i < tokens.Count && Artikler.Contains(tokens[i].Value.ToLowerInvariant())) i++;
                continue;
            }
            ledd.Add(tokens[i]);
            i++;
        }
    }

    /// <summary>
    /// [Ny, Johanns avgjørelse 2026-11-05, #440] Forleddssperren for grupperingen, med samme regel som hjemmelregelen: står forekomsten
    /// (<paramref name="start"/>–<paramref name="slutt"/> i <paramref name="tekst"/>) som kjernen i et ledd i en nominalfrase et
    /// <see cref="Opprettelsesuttrykk"/> styrer, og står det et ord foran den i leddet som ikke er i <see cref="TillatteForledd"/>, er det et
    /// annet navn. Svaret er da leddet slik det står i teksten, fra det første ordet som sperrer («felles samarbeidsutvalg»; tallord foran
    /// tas ikke med). Ellers null.
    /// <para>
    /// <b>Avgrensning:</b> sperren gjelder bare der hjemmelregelen leser en frase, altså etter «skal … ha et» og «det skal være (et)». Andre
    /// steder vet regelen ikke hvor frasen begynner (er «skal» i «skal samarbeidsutvalget» et forledd?), og da gjettes det ikke (CLAUDE.md §8).
    /// Målt i bhl. § 5 første ledd: «det skal være felles samarbeidsutvalg».
    /// </para>
    /// </summary>
    public static string? SperretFrase(string tekst, int start, int slutt)
    {
        foreach (var (s0, setning) in Setninger(tekst))
        {
            if (start < s0 || slutt > s0 + setning.Length) continue;
            foreach (var (_, monster) in Opprettelsesuttrykk)
            {
                foreach (Match m in monster.Matches(setning))
                {
                    foreach (var ledd in Fraseledd(setning, m.Index + m.Length))
                    {
                        var siste = ledd[^1];
                        if (s0 + siste.Index + siste.Length != slutt) continue;
                        var kjerne = ledd.FindIndex(t => s0 + t.Index == start);
                        if (kjerne <= 0) continue;
                        var forste = ledd.FindIndex(t => !TillatteForledd.Contains(t.Value.ToLowerInvariant()));
                        if (forste < 0 || forste >= kjerne) return null;
                        return setning[ledd[forste].Index..(siste.Index + siste.Length)];
                    }
                }
            }
        }
        return null;
    }

    private static (int Pos, string Tekst, bool Ordrett, string? Sperret)? Kjerne(string setning, List<Match> ledd, List<string[]> former)
    {
        var ord = ledd.Select(m => m.Value.ToLowerInvariant()).ToArray();
        // Ordrett først: står kjernen ordrett som en variant, er det tekstfunn selv om en annen variant også er en bøyning av den.
        foreach (var (form, bareOrdrett) in new[] { true, false }.SelectMany(b =>
                     former.OrderByDescending(f => f.Length).ThenBy(f => string.Join(' ', f), StringComparer.Ordinal).Select(f => (f, b))))
        {
            if (form.Length > ord.Length || (bareOrdrett && ord[^1] != form[^1])) continue;
            var hale = ord[^form.Length..];
            var forledd = true;
            for (var j = 0; j < form.Length - 1; j++) if (hale[j] != form[j]) { forledd = false; break; }
            if (!forledd) continue;
            var likt = hale[^1] == form[^1];
            if (!likt && BoyningSuffiks(form[^1], hale[^1]) is null) continue;
            var forran = ord[..^form.Length];
            var sperre = forran.Any(o => !TillatteForledd.Contains(o));
            var forste = ledd[ord.Length - form.Length];
            var siste = ledd[^1];
            return (forste.Index, setning[forste.Index..(siste.Index + siste.Length)], likt,
                sperre ? $"forleddssperre: «{string.Join(' ', ord)}» er et annet navn enn «{string.Join(' ', form)}»" : null);
        }
        return null;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex FlereMellomrom();

    [GeneratedRegex(@"(?<=[.!?;])\s+")]
    private static partial Regex Setningsskille();

    [GeneratedRegex(@"[\p{L}]+(?:-[\p{L}]+)*")]
    private static partial Regex Ord();

    [GeneratedRegex(@"\bskal\b(?:\s+\S+){0,6}?\s+ha\s+(?:et|en|ei|ett)(?:\s+eller\s+flere)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex SkalHa();

    // Artikkelen er valgfri her (ikke ved «skal ha»): juristrunden målte «det skal være felles samarbeidsutvalg» (bhl. § 5) som et treff
    // forleddssperren stopper. «skal ha» uten artikkel ga fire falske forslag for «medlemmer» og er ikke med.
    [GeneratedRegex(@"(?:\bdet\s+skal|\bskal\s+det\b(?:\s+\S+){0,6}?)\s+være(?:\s+(?:et|en|ei|ett)(?:\s+eller\s+flere)?)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex SkalVaere();

    [GeneratedRegex(@"[\p{L}]+(?:-[\p{L}]+)*|[^\s\p{L}]")]
    private static partial Regex Frasetokens();
}

// ------------------------------------------------------------------------------------------------ Svarformene

/// <summary>Én rute i fordelingen.</summary>
/// <param name="Topp">De hyppigste ordene i ruten, med treffet.</param>
public sealed record Rutetall(string Rute, string Tekst, int Forekomster, int Rettskilder, int UlikeOrd, IReadOnlyList<Ordtreff> Topp);

/// <summary>Ett ord og hvor det havnet.</summary>
/// <param name="Treff">Det kjente ordet som traff, i klart språk («navneform for Kongen i statsråd»). Null i aktørkøen.</param>
/// <param name="Ligner">For en bøyningsform: det kjente ordet den ligner.</param>
/// <param name="BoyningAvRute">For en bøyningsform: ruten til ordet den ligner.</param>
/// <param name="Standardregel">Bare for et eksakt generisk ord. En bøyningsform arver den aldri.</param>
public sealed record Ordtreff(
    string Ord, string Rute, int Forekomster, string? Treff, string? Ligner, string? BoyningAvRute, string? Suffiks, string? Standardregel);

/// <summary>Fordelingen av alle ventende navnekandidater på rutene (<c>GET /api/aktorko/fordeling</c>).</summary>
/// <param name="Ventende">Alle ventende navnekandidater. Summen av <see cref="Ruter"/> er dette tallet, og ingen kandidat står i to ruter.</param>
/// <param name="AntallNavn">Navnene i aktørkøen etter gruppering.</param>
/// <param name="OrdIKoen">Ulike ord (normalisert) i aktørkøen før sammenslåing etter bøyning.</param>
/// <param name="Hyppigste">De hyppigste ordene på tvers av rutene.</param>
public sealed record AktorkoFordeling(
    int Ventende, IReadOnlyList<Rutetall> Ruter, int AntallNavn, int MedFlereForekomster, int OrdIKoen, int Genitivforslag,
    IReadOnlyList<Ordtreff> Hyppigste);

/// <summary>Et hjemmelforslag slik API-et viser det: med rettskilden og bestemmelsen.</summary>
/// <param name="AnnenParagraf">[Ny, Johanns avgjørelse 2026-11-01 etter juristrunde-440] Når hjemmelen står i en annen paragraf enn ALLE
/// forekomstene: «hjemmelen står i § 9; forekomstene i § 33b». Null når en forekomst står i samme paragraf. En opplysning regnet ut av
/// eId-ene, ikke en påstand om at forslaget er feil; vises som nøytral metatekst, ikke som varsel.</param>
/// <param name="Sperret">[Ny, #440 PR 2] Satt når forleddssperren stoppet treffet: da er det ikke et forslag, og det kan ikke godkjennes.</param>
public sealed record Hjemmelforslagvisning(
    Guid RettskildeId, string Rettskilde, string? Kortnavn, string Eid, string? Bestemmelse, string Uttrykk, string Setning, string Treff,
    int TreffStart, string Kunnskapsstatus, string? AnnenParagraf = null, string? Sperret = null);

/// <summary>Én rad i aktørkøen (<c>GET /api/aktorko</c>).</summary>
/// <param name="Hjemmelforslag">Antall forslag etter regelen. 0 = hullet (ingen bestemmelse i rettskildene der navnet står).</param>
/// <param name="ForsteHjemmelforslag">Det første forslaget (rettskilde, så rekkefølgen i teksten).</param>
/// <param name="GenitivforslagInn">Varianter med genitivforledd i andre navn som kan høre hit (bare forslag).</param>
/// <param name="BareMerknader">Alle forekomstene står i merknader eller veiledning (#419).</param>
public sealed record AktorkoRad(
    string Nokkel, string Navn, int Forekomster, int Rettskilder, IReadOnlyList<Kovariant> Varianter, IReadOnlyList<Guid> RettskildeIder,
    int Hjemmelforslag, Hjemmelforslagvisning? ForsteHjemmelforslag, int GenitivforslagInn, bool BareMerknader, bool Manuell = false);

public sealed record AktorkoListe(int Ventende, int AntallNavn, int MedFlereForekomster, IReadOnlyList<AktorkoRad> Navn);

/// <summary>Én forekomst slik detaljvisningen viser den.</summary>
/// <param name="Utdrag">Setningen forekomsten står i. Null når nodeteksten ikke lenger har navnet på posisjonen sveipet fant (teksten er endret
/// siden) — da vises ikke et gjettet utdrag.</param>
/// <param name="UtdragStart">Forekomstens posisjon i <see cref="Utdrag"/>.</param>
/// <param name="ErMerknad">Noden er merknad eller veiledning (#419): forekomsten kan tagges, men blir aldri hjemmel.</param>
public sealed record Forekomstvisning(
    Guid NavnekandidatId, string Tekst, string Eid, string? Bestemmelse, string? Utdrag, int UtdragStart, bool ErMerknad, string Kategori,
    string? Oppdagelseskilde, string? Konfidens);

public sealed record RettskildeForekomster(Guid RettskildeId, string Tittel, string? Kortnavn, IReadOnlyList<Forekomstvisning> Forekomster);

/// <summary>Ett navn i aktørkøen med det som trengs for å vurdere det (<c>GET /api/aktorko/{nøkkel}</c>).</summary>
/// <param name="GenitivforslagInn">Varianter i andre navn med genitivforledd, der etterleddet er dette navnet. Bare forslag.</param>
/// <param name="GenitivforslagUt">Når dette navnet selv har genitivforledd: navnene etterleddet ligner. Bare forslag.</param>
/// <param name="Hjemmelforslag">Forslagene etter regelen. Tom = hullet. Vises bare som forslag i PR 1.</param>
/// <param name="Hjemmelregel">Regelen i klart språk, slik den vises.</param>
/// <param name="SperredeTreff">[Ny, #440 PR 2] Treff forleddssperren stoppet («felles samarbeidsutvalg»). Vises, men er ikke forslag.</param>
/// <param name="Manuell">[Ny, #440 PR 2] Navnet er satt sammen av et menneske («skill ut» / «ta med»), ikke bare av regelen.</param>
/// <param name="Hullmerknad">[Ny, Johanns avgjørelse 2026-11-01] Metateksten under «Hjemmel for aktøren mangler»: hva som er undersøkt.</param>
public sealed record AktorkoDetalj(
    string Nokkel, string Navn, int Forekomster, IReadOnlyList<Kovariant> Varianter, IReadOnlyList<Genitivforslag> GenitivforslagInn,
    IReadOnlyList<Genitivforslag> GenitivforslagUt, IReadOnlyList<RettskildeForekomster> PerRettskilde,
    IReadOnlyList<Hjemmelforslagvisning> Hjemmelforslag, string Hjemmelregel, IReadOnlyList<Hjemmelforslagvisning>? SperredeTreff = null,
    bool Manuell = false, string? Hullmerknad = null);

// ------------------------------------------------------------------------------------------------ Tjenesten

/// <summary>
/// [Ny, issue #440 PR 1, Johanns avgjørelser 2026-10-30] Aktørkøen: ventende navnekandidater rutet og gruppert ved lesing. <b>Skriver
/// ingenting</b> (PR 1 er bare lesing; godkjenningen kommer i PR 2). Reglene står i <see cref="Aktorkoregler"/>.
/// </summary>
public sealed class AktorkoTjeneste(RegelIdeDbContext db)
{
    /// <summary>[ENDRET, #440 PR 2] Regelen i klart språk, presisert etter juristrunde-440.</summary>
    public const string Hjemmelregel =
        "En bestemmelse i en rettskilde der navnet står, der «skal … ha et/en/ei (eller flere)» eller «det skal være et …» styrer en " +
        "nominalfrase med navnet som kjerne (samme ord eller en bøyningsform). Frasen går fram til første preposisjon, «som», «at», «enn» " +
        "eller komma, og kan ha «og»/«eller» med ny artikkel. Står et annet ord enn tall- og mengdeord foran navnet («felles forliksråd»), er " +
        "det et annet organ, og det blir ikke forslag (forleddssperren). Oppnevning, valg, utnevnelse og tilsetting gir aldri forslag. " +
        "Merknader og veiledning er aldri med. Kunnskapsstatusen er tekstfunn når navnet står ordrett, ellers normalisering. " +
        "Regelen er et forslag: et menneske godkjenner hjemmelen (juristrunde-440).";

    /// <summary>[Ny, Johanns avgjørelse 2026-11-01] Metateksten under «Hjemmel for aktøren mangler». «Ikke funnet» er ikke «finnes ikke»
    /// (docs/profil/03 §5); teksten sier hva som er undersøkt, og hovedteksten er den samme som på aktørsiden (#441).</summary>
    public const string Hullmerknad = "ikke funnet av regelen; andre lover og vedtekter er ikke søkt";

    /// <summary>
    /// Leser tabellen rutingen slår opp i. Statisk, så sveipene kan bruke den uten en ny avhengighet.
    /// <list type="bullet">
    /// <item><b>Generiske:</b> navneformer med grunn <c>generisk</c> som ikke er avvist, med standardregelen der ordet har en. Et generisk ord
    /// uten standardregel er fortsatt generisk (svaret kommer da bare fra en binding), og står aldri i aktørkøen. Målt 2026-10-31: alle tre
    /// generiske navneformer har en standardregel.</item>
    /// <item><b>Aktører:</b> navneformer (ikke generiske, ikke avviste) med en aktør, og registernavnene, for aktører som ikke er erstattet
    /// (duplikat). Også nedlagte aktører: et navn på en nedlagt aktør er kjent og hører ikke hjemme i en kø for nye aktører (målt: ingen
    /// ventende kandidat treffer bare en nedlagt aktør). Kortform- og parallellnavnforslag som ikke er vurdert, er også med: navnet er kjent som
    /// forslag og behandles på aktøren.</item>
    /// <item><b>Begreper:</b> klasse, rolle, område og gruppe som ikke er avvist, med loven for de lovspesifikke (se <see cref="Rutingstabell"/>).</item>
    /// </list>
    /// [#423] En avvist navneform er aldri med. «Fylkeskommune» på Agder fylkeskommune ble avvist 2026-10-31, og da treffer 0 kandidater Agder.
    /// </summary>
    public static async Task<Rutingstabell> LastRutingstabellAsync(RegelIdeDbContext db, CancellationToken ct = default)
    {
        var begreper = await db.Begreper
            .Where(b => b.Entitetsstatus == "gjeldende" && (b.Godkjenning == null || b.Godkjenning != Strukturkanter.Avvist))
            .Select(b => new { b.Id, b.Term, b.Begrepskategori, b.Navneformgrunn, b.VirksomhetReferanseId, b.LovkildeId })
            .ToListAsync(ct);
        var virksomheter = await db.Virksomheter.Select(v => new { v.Id, v.Navn, v.ErstattetAvId }).ToListAsync(ct);
        var navnPerId = virksomheter.ToDictionary(v => v.Id, v => v.Navn);
        var erstattet = virksomheter.Where(v => v.ErstattetAvId != null).Select(v => v.Id).ToHashSet();

        var generiske = new Dictionary<string, KjentOrd>(StringComparer.Ordinal);
        foreach (var b in begreper.Where(b => b.Begrepskategori == "virksomhet" && b.Navneformgrunn == VirksomhetsbegrepTjeneste.Generisk)
                     .OrderBy(b => b.Term, StringComparer.Ordinal))
        {
            generiske.TryAdd(Aktorkoregler.Normaliser(b.Term),
                new KjentOrd(b.Term, "generisk", b.Term, null, b.Id, null, GeneriskStandardregelTjeneste.For(b.Term)?.Navn));
        }

        static void LeggTil(Dictionary<string, List<KjentOrd>> d, string term, KjentOrd verdi)
        {
            var k = Aktorkoregler.Normaliser(term);
            if (!d.TryGetValue(k, out var l)) d[k] = l = [];
            l.Add(verdi);
        }

        var aktorer = new Dictionary<string, List<KjentOrd>>(StringComparer.Ordinal);
        foreach (var b in begreper.Where(b => b.Begrepskategori == "virksomhet" && b.Navneformgrunn != VirksomhetsbegrepTjeneste.Generisk
                                              && b.VirksomhetReferanseId is { } vid && !erstattet.Contains(vid) && navnPerId.ContainsKey(vid)))
        {
            var vid = b.VirksomhetReferanseId!.Value;
            LeggTil(aktorer, b.Term, new KjentOrd(b.Term, "navneform", navnPerId[vid], vid, b.Id, null, null));
        }
        foreach (var v in virksomheter.Where(v => v.ErstattetAvId == null))
        {
            LeggTil(aktorer, v.Navn, new KjentOrd(v.Navn, "registernavn", v.Navn, v.Id, null, null, null));
        }

        var kjenteBegreper = new Dictionary<string, List<KjentOrd>>(StringComparer.Ordinal);
        foreach (var b in begreper.Where(b => b.Begrepskategori is not null && Nodetyper.MedGruppefunksjon.Contains(b.Begrepskategori)))
        {
            LeggTil(kjenteBegreper, b.Term, new KjentOrd(b.Term, b.Begrepskategori!, b.Term, null, b.Id, b.LovkildeId, null));
        }

        var hjemler = await db.RettskildeHjemler.Select(h => new { h.RettskildeId, h.HjemmelRettskildeId }).ToListAsync(ct);
        var lover = hjemler.GroupBy(h => h.RettskildeId)
            .ToDictionary(g => g.Key, g => (IReadOnlySet<Guid>)g.Select(x => x.HjemmelRettskildeId).ToHashSet());

        static IReadOnlyDictionary<string, IReadOnlyList<KjentOrd>> Sortert(Dictionary<string, List<KjentOrd>> d) =>
            d.ToDictionary(x => x.Key, x => (IReadOnlyList<KjentOrd>)x.Value
                .OrderBy(o => o.Art, StringComparer.Ordinal).ThenBy(o => o.Navn, StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
        return new Rutingstabell(generiske, Sortert(aktorer), Sortert(kjenteBegreper), lover);
    }

    private async Task<(List<KoForekomst> Forekomster, List<Rutingssvar> Svar)> RutAlleAsync(CancellationToken ct)
    {
        var tabell = await LastRutingstabellAsync(db, ct);
        var forekomster = (await db.Navnekandidater.Where(k => k.Status == "Venter")
                .Select(k => new { k.Id, k.ForeslattTekst, k.RettskildeId, k.NodeEid, k.StartOffset, k.EndOffset, k.Kategori, k.OppdagelsesKilde, k.Konfidens, k.AktorkoGruppe })
                .ToListAsync(ct))
            .Select(k => new KoForekomst(k.Id, k.ForeslattTekst, k.RettskildeId, k.NodeEid, k.StartOffset, k.EndOffset, k.Kategori,
                k.OppdagelsesKilde, k.Konfidens, k.AktorkoGruppe))
            .OrderBy(f => f.NavnekandidatId).ToList();
        var svar = forekomster.Select(f => Aktorkoregler.Ruter(tabell, f.Tekst, f.RettskildeId)).ToList();
        await SettForleddssperreAsync(forekomster, svar, ct);
        return (forekomster, svar);
    }

    /// <summary>
    /// [Ny, Johanns avgjørelse 2026-11-05, #440] Forleddssperren i grupperingen (<see cref="Aktorkoregler.SperretFrase"/>): for forekomstene i
    /// aktørkøen leses leddet de står i, og der sperren slår til, får forekomsten frasen («felles samarbeidsutvalg»). Rutingen er uendret; den
    /// er gjort på teksten før dette. Bare når teksten fortsatt har navnet på posisjonen sveipet fant (samme vakt som utdraget i
    /// <see cref="DetaljAsync"/>); ellers er teksten endret, og en frase ville vært gjettet.
    /// </summary>
    private async Task SettForleddssperreAsync(List<KoForekomst> forekomster, List<Rutingssvar> svar, CancellationToken ct)
    {
        var iKo = Enumerable.Range(0, forekomster.Count).Where(i => svar[i].Rute == Aktorkoruter.Aktorko).ToList();
        if (iKo.Count == 0) return;
        var ider = iKo.Select(i => forekomster[i].RettskildeId).Distinct().ToList();
        var eids = iKo.Select(i => forekomster[i].NodeEid).Distinct().ToList();
        // Samme grove forfilter som hjemmelnodene: sperren kan bare slå til der et av uttrykkene står.
        var noder = (await db.RettskildeNoder
                .Where(n => ider.Contains(n.RettskildeId) && eids.Contains(n.Eid) && n.Entitetsstatus == "gjeldende" && n.Tekst != null
                            && n.Tekst.ToLower().Contains("skal") && (n.Tekst.ToLower().Contains("ha e") || n.Tekst.ToLower().Contains("være")))
                .Select(n => new { n.RettskildeId, n.Eid, n.Tekst })
                .ToListAsync(ct))
            .GroupBy(n => (n.RettskildeId, n.Eid)).ToDictionary(g => g.Key, g => g.First().Tekst!);
        foreach (var i in iKo)
        {
            var f = forekomster[i];
            if (!noder.TryGetValue((f.RettskildeId, f.NodeEid), out var tekst)) continue;
            if (f.Start < 0 || f.Slutt > tekst.Length || f.Slutt <= f.Start
                || !string.Equals(tekst[f.Start..f.Slutt], f.Tekst, StringComparison.OrdinalIgnoreCase)) continue;
            if (Aktorkoregler.SperretFrase(tekst, f.Start, f.Slutt) is { } frase) forekomster[i] = f with { Frase = frase };
        }
    }

    private static List<Kogruppe> KoGrupper(List<KoForekomst> forekomster, List<Rutingssvar> svar) =>
        Aktorkoregler.Grupper(forekomster.Where((_, i) => svar[i].Rute == Aktorkoruter.Aktorko)).ToList();

    /// <summary>Treffet i klart språk: «navneform for Kongen i statsråd», «klasse «kommune»».</summary>
    public static string Treffbeskrivelse(Rutingssvar s)
    {
        var t = s.Treff.FirstOrDefault();
        if (t is null) return "";
        var antall = s.Treff.Select(x => x.Navn).Distinct().Count();
        var flere = antall > 1 ? $" (og {antall - 1} til)" : "";
        return t.Art switch
        {
            "generisk" => $"generisk navneform «{t.Term}»" + (s.Rute == Aktorkoruter.Generisk && t.Standardregel is { } r ? $", standardregel «{r}»" : ""),
            "navneform" => $"navneform for {t.Navn}{flere}",
            "registernavn" => $"registernavnet {t.Navn}{flere}",
            _ => $"{Nodetypetekst(t.Art)} «{t.Term}»{flere}",
        };
    }

    private static string Nodetypetekst(string art) => art switch
    {
        Nodetyper.Klasse => "klassen",
        Nodetyper.Rolle => "rollen",
        Nodetyper.Omrade => "området",
        Nodetyper.Gruppe => "gruppen",
        _ => art,
    };

    private static Ordtreff LagOrdtreff(string ord, int antall, Rutingssvar s) =>
        new(ord, s.Rute, antall, s.Rute == Aktorkoruter.Aktorko ? null : Treffbeskrivelse(s), s.Ligner, s.BoyningAvRute, s.Suffiks, s.Standardregel);

    /// <summary>Fordelingen på rutene. Regnes ved lesing; ingenting flyttes.</summary>
    public async Task<AktorkoFordeling> FordelingAsync(CancellationToken ct = default)
    {
        var (forekomster, svar) = await RutAlleAsync(ct);
        var par = forekomster.Zip(svar, (f, s) => (F: f, S: s)).ToList();

        var ruter = Aktorkoruter.Alle.Select(r =>
        {
            var i = par.Where(p => p.S.Rute == r).ToList();
            var topp = i.GroupBy(p => p.F.Normalisert, StringComparer.Ordinal)
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(8)
                .Select(g => LagOrdtreff(g.Key, g.Count(), g.First().S)).ToList();
            return new Rutetall(r, Aktorkoruter.Tekst(r), i.Count, i.Select(p => p.F.RettskildeId).Distinct().Count(),
                i.Select(p => p.F.Normalisert).Distinct().Count(), topp);
        }).ToList();

        var grupper = KoGrupper(forekomster, svar);
        var hyppigste = par.GroupBy(p => p.F.Normalisert, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Take(12)
            .Select(g => LagOrdtreff(g.Key, g.Count(), g.First().S)).ToList();
        return new AktorkoFordeling(forekomster.Count, ruter, grupper.Count, grupper.Count(g => g.Forekomster.Count > 1),
            grupper.Sum(g => g.Varianter.Count), Aktorkoregler.FinnGenitivforslag(grupper).Count, hyppigste);
    }

    /// <summary>Nodene hjemmelregelen leser i rettskildene: gjeldende, ikke opphevet, ikke merknad, og med et av ordene i uttrykkene (et grovt
    /// forfilter i databasen; selve regelen er <see cref="Aktorkoregler.FinnHjemmelforslag"/>).</summary>
    private async Task<List<HjemmelNode>> HjemmelnoderAsync(IReadOnlyCollection<Guid> rettskildeIder, CancellationToken ct)
    {
        var rader = await db.RettskildeNoder
            .Where(n => rettskildeIder.Contains(n.RettskildeId) && n.Entitetsstatus == "gjeldende" && !n.Opphevet && !n.ErMerknad && n.Tekst != null
                        // [ENDRET, #440 PR 2] Forfilteret følger de to uttrykkene som står igjen etter juristrunden. «være» uten artikkel er med:
                        // «det skal være felles samarbeidsutvalg» er et treff forleddssperren stopper, og det skal vises (målt i kaldsjekken).
                        && n.Tekst.ToLower().Contains("skal") && (n.Tekst.ToLower().Contains("ha e") || n.Tekst.ToLower().Contains("være")))
            .Select(n => new { n.RettskildeId, n.Eid, n.Tekst, n.ErMerknad, n.Sorteringsrekkefolge })
            .ToListAsync(ct);
        return rader.Select(n => new HjemmelNode(n.RettskildeId, n.Eid, n.Tekst!, n.ErMerknad, n.Sorteringsrekkefolge)).ToList();
    }

    private async Task<Dictionary<Guid, (string Tittel, string? Kortnavn)>> TitlerAsync(IReadOnlyCollection<Guid> ider, CancellationToken ct) =>
        (await db.Rettskilder.Where(r => ider.Contains(r.Id)).Select(r => new { r.Id, r.Tittel, r.Kortnavn }).ToListAsync(ct))
        .ToDictionary(r => r.Id, r => (r.Tittel, r.Kortnavn));

    private static Hjemmelforslagvisning Vis(Hjemmelforslag h, Dictionary<Guid, (string Tittel, string? Kortnavn)> titler)
    {
        var funnet = titler.TryGetValue(h.RettskildeId, out var t);
        return new(h.RettskildeId, funnet ? t.Tittel : h.RettskildeId.ToString(), funnet ? t.Kortnavn : null, h.Eid, Bestemmelse(h.Eid),
            Aktorkoregler.UttrykkTekst(h.Uttrykk), h.Setning, h.Treff, h.TreffStart, h.Kunnskapsstatus);
    }

    /// <summary>Hjemmelforslagene for et navn: bare i rettskildene der navnet står (#440).</summary>
    private static List<Hjemmelforslag> ForslagFor(Kogruppe g, ILookup<Guid, HjemmelNode> noder) =>
        Aktorkoregler.FinnHjemmelforslag(g.Varianter.Select(v => v.Normalisert),
            g.Forekomster.Select(f => f.RettskildeId).Distinct().SelectMany(id => noder[id])).ToList();

    /// <summary>Aktørkøen: ett navn per rad, sortert på antall forekomster.</summary>
    public async Task<AktorkoListe> KoAsync(CancellationToken ct = default)
    {
        var (forekomster, svar) = await RutAlleAsync(ct);
        var grupper = KoGrupper(forekomster, svar);
        var ider = grupper.SelectMany(g => g.Forekomster.Select(f => f.RettskildeId)).Distinct().ToList();
        var noder = (await HjemmelnoderAsync(ider, ct)).ToLookup(n => n.RettskildeId);
        var titler = await TitlerAsync(ider, ct);
        var merknadsnoder = await MerknadsnoderAsync(grupper.SelectMany(g => g.Forekomster).ToList(), ct);
        var genitiv = Aktorkoregler.FinnGenitivforslag(grupper);

        var rader = grupper.Select(g =>
        {
            var forslag = ForslagFor(g, noder);
            return new AktorkoRad(g.Nokkel, g.Navn, g.Forekomster.Count, g.Rettskilder, g.Varianter,
                g.Forekomster.Select(f => f.RettskildeId).Distinct().ToList(), forslag.Count,
                forslag.Count > 0 ? Vis(forslag[0], titler) : null, genitiv.Count(x => x.TilNokkel == g.Nokkel),
                g.Forekomster.All(f => merknadsnoder.Contains((f.RettskildeId, f.NodeEid))), g.Manuell);
        }).ToList();
        return new AktorkoListe(forekomster.Count, grupper.Count, grupper.Count(g => g.Forekomster.Count > 1), rader);
    }

    private async Task<HashSet<(Guid, string)>> MerknadsnoderAsync(List<KoForekomst> forekomster, CancellationToken ct)
    {
        var ider = forekomster.Select(f => f.RettskildeId).Distinct().ToList();
        var eids = forekomster.Select(f => f.NodeEid).Distinct().ToList();
        return (await db.RettskildeNoder
                .Where(n => ider.Contains(n.RettskildeId) && eids.Contains(n.Eid) && n.Entitetsstatus == "gjeldende" && n.ErMerknad)
                .Select(n => new { n.RettskildeId, n.Eid }).ToListAsync(ct))
            .Select(n => (n.RettskildeId, n.Eid)).ToHashSet();
    }

    /// <summary>[Ny, #440 PR 2] Gruppen for et navn, slått opp på hvilken som helst variant. Null når ordet ikke står i aktørkøen.</summary>
    public async Task<Kogruppe?> FinnGruppeAsync(string nokkel, CancellationToken ct = default)
    {
        var (forekomster, svar) = await RutAlleAsync(ct);
        var n = Aktorkoregler.Normaliser(nokkel);
        return KoGrupper(forekomster, svar).FirstOrDefault(x => x.Varianter.Any(v => v.Normalisert == n));
    }

    /// <summary>[Ny, #440 PR 2] Hjemmelforslagene for en gruppe, regnet ut på nytt (samme regel som visningen).</summary>
    public async Task<IReadOnlyList<Hjemmelforslag>> HjemmelforslagAsync(Kogruppe g, CancellationToken ct = default)
    {
        var ider = g.Forekomster.Select(f => f.RettskildeId).Distinct().ToList();
        return ForslagFor(g, (await HjemmelnoderAsync(ider, ct)).ToLookup(n => n.RettskildeId));
    }

    /// <summary>[Ny, #440 PR 2] Ventende forekomster av nøyaktig ordet (normalisert), delt på dem som har en av <paramref name="ruter"/> og
    /// resten (med ruten). Brukes av «Tagg alle som generisk omtale».</summary>
    public async Task<(List<KoForekomst> Med, List<(KoForekomst Forekomst, string Rute)> Utenfor)> ForekomsterAvOrdAsync(
        string ord, IReadOnlyCollection<string> ruter, CancellationToken ct = default)
    {
        var (forekomster, svar) = await RutAlleAsync(ct);
        var n = Aktorkoregler.Normaliser(ord);
        var par = forekomster.Zip(svar).Where(p => p.First.Normalisert == n).ToList();
        return (par.Where(p => ruter.Contains(p.Second.Rute)).Select(p => p.First).ToList(),
            par.Where(p => !ruter.Contains(p.Second.Rute)).Select(p => (p.First, p.Second.Rute)).ToList());
    }

    /// <summary>Ett navn, slått opp på hvilken som helst av variantene (normalisert). Null når ordet ikke står i aktørkøen.</summary>
    public async Task<AktorkoDetalj?> DetaljAsync(string nokkel, CancellationToken ct = default)
    {
        var (forekomster, svar) = await RutAlleAsync(ct);
        var grupper = KoGrupper(forekomster, svar);
        var n = Aktorkoregler.Normaliser(nokkel);
        var g = grupper.FirstOrDefault(x => x.Varianter.Any(v => v.Normalisert == n));
        if (g is null) return null;

        var ider = g.Forekomster.Select(f => f.RettskildeId).Distinct().ToList();
        var titler = await TitlerAsync(ider, ct);
        var hjemmelnoder = (await HjemmelnoderAsync(ider, ct)).ToLookup(x => x.RettskildeId);
        var eids = g.Forekomster.Select(f => f.NodeEid).Distinct().ToList();
        var noder = (await db.RettskildeNoder
                .Where(x => ider.Contains(x.RettskildeId) && eids.Contains(x.Eid) && x.Entitetsstatus == "gjeldende")
                .Select(x => new { x.RettskildeId, x.Eid, x.Tekst, x.ErMerknad, x.Sorteringsrekkefolge }).ToListAsync(ct))
            .ToDictionary(x => (x.RettskildeId, x.Eid));

        Forekomstvisning Forekomst(KoForekomst f)
        {
            noder.TryGetValue((f.RettskildeId, f.NodeEid), out var node);
            string? utdrag = null;
            var utdragStart = 0;
            var tekst = node?.Tekst;
            // Bare når teksten fortsatt har navnet på posisjonen sveipet fant. Ellers er teksten endret siden, og et utdrag ville vært gjettet.
            if (tekst is not null && f.Start >= 0 && f.Slutt <= tekst.Length && f.Slutt > f.Start
                && string.Equals(tekst[f.Start..f.Slutt], f.Tekst, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var (start, setning) in Aktorkoregler.Setninger(tekst))
                {
                    if (f.Start < start || f.Start >= start + setning.Length) continue;
                    utdrag = setning;
                    utdragStart = f.Start - start;
                    break;
                }
            }
            return new Forekomstvisning(f.NavnekandidatId, f.Tekst, f.NodeEid, Bestemmelse(f.NodeEid), utdrag, utdragStart,
                node?.ErMerknad ?? false, f.Kategori, f.Oppdagelseskilde, f.Konfidens);
        }

        int Rekkefolge(KoForekomst f) => noder.TryGetValue((f.RettskildeId, f.NodeEid), out var nn) ? nn.Sorteringsrekkefolge : int.MaxValue;
        var perRettskilde = g.Forekomster.GroupBy(f => f.RettskildeId)
            .Select(x =>
            {
                var funnet = titler.TryGetValue(x.Key, out var t);
                return new RettskildeForekomster(x.Key, funnet ? t.Tittel : x.Key.ToString(), funnet ? t.Kortnavn : null,
                    x.OrderBy(Rekkefolge).ThenBy(f => f.Start).Select(Forekomst).ToList());
            })
            .OrderByDescending(r => r.Forekomster.Count).ThenBy(r => r.Tittel, StringComparer.Ordinal).ToList();

        var genitiv = Aktorkoregler.FinnGenitivforslag(grupper);
        return new AktorkoDetalj(g.Nokkel, g.Navn, g.Forekomster.Count, g.Varianter,
            genitiv.Where(x => x.TilNokkel == g.Nokkel).ToList(), genitiv.Where(x => x.FraNokkel == g.Nokkel).ToList(), perRettskilde,
            ForslagFor(g, hjemmelnoder).Select(h => Vis(h, titler) with { AnnenParagraf = AnnenParagraf(h, g.Forekomster, titler) }).ToList(),
            Hjemmelregel,
            Aktorkoregler.FinnHjemmeltreff(g.Varianter.Select(v => v.Normalisert), ider.SelectMany(id => hjemmelnoder[id]))
                .Where(t => t.Sperret is not null).Select(t => Vis(t.Forslag, titler) with { Sperret = t.Sperret }).ToList(),
            g.Manuell, Hullmerknad);
    }

    /// <summary>Paragrafen i en eId («§ 33b»), uten ledd og punkt. Null uten paragraf.</summary>
    public static string? Paragraf(string eid)
    {
        var m = Regex.Match(eid, @"/(§[^/]+)(?:/ledd-\d+)?(?:/punkt-\d+)?$");
        return m.Success ? "§ " + m.Groups[1].Value[1..] : null;
    }

    /// <summary>
    /// [Ny, Johanns avgjørelse 2026-11-01 etter juristrunde-440, punkt R] «hjemmelen står i § X; forekomstene i § Y» når hjemmelen står i en
    /// annen paragraf enn ALLE forekomstene (samme rettskilde og samme paragraf teller som samme). Forekomster i andre rettskilder vises med
    /// kortnavnet foran. Null når en forekomst står i samme paragraf, eller når hjemmelen ikke har paragraf.
    /// </summary>
    public static string? AnnenParagraf(Hjemmelforslag h, IEnumerable<KoForekomst> forekomster, IReadOnlyDictionary<Guid, (string Tittel, string? Kortnavn)> titler)
    {
        var hjemmel = Paragraf(h.Eid);
        if (hjemmel is null) return null;
        var liste = forekomster.Select(f => (f.RettskildeId, Paragraf: Paragraf(f.NodeEid))).Distinct().ToList();
        if (liste.Any(f => f.RettskildeId == h.RettskildeId && f.Paragraf == hjemmel)) return null;
        string Navn(Guid id) => titler.TryGetValue(id, out var t) ? t.Kortnavn ?? t.Tittel : id.ToString();
        var tekster = liste
            .OrderBy(f => f.RettskildeId == h.RettskildeId ? 0 : 1).ThenBy(f => Navn(f.RettskildeId), StringComparer.Ordinal)
            .ThenBy(f => f.Paragraf, StringComparer.Ordinal)
            .Select(f => (f.RettskildeId == h.RettskildeId ? "" : Navn(f.RettskildeId) + " ") + (f.Paragraf ?? "en bestemmelse uten paragraf"))
            .Distinct().ToList();
        var vist = tekster.Count > 4 ? string.Join(", ", tekster.Take(4)) + $" og {tekster.Count - 4} til" : string.Join(", ", tekster);
        return $"hjemmelen står i {hjemmel}; forekomstene i {vist}";
    }

    private static readonly string[] Ordenstall = ["", "første", "andre", "tredje", "fjerde", "femte", "sjette", "sjuende", "åttende", "niende", "tiende"];

    /// <summary>«§ 4 første ledd» av eId-en (…/§4/ledd-1/punkt-2). Null når eId-en ikke har en paragraf (kapittelinndelte forskrifter,
    /// merknader); da viser klienten eId-en. Samme ordenstall som <c>paragrafEtikett</c> i frontenden.</summary>
    public static string? Bestemmelse(string eid)
    {
        var m = Regex.Match(eid, @"/(§[^/]+)(?:/ledd-(\d+))?(?:/punkt-(\d+))?$");
        if (!m.Success) return null;
        var s = "§ " + m.Groups[1].Value[1..];
        if (m.Groups[2].Success)
        {
            var n = int.Parse(m.Groups[2].Value);
            s += n < Ordenstall.Length ? $" {Ordenstall[n]} ledd" : $" ledd {n}";
        }
        if (m.Groups[3].Success) s += $" punkt {m.Groups[3].Value}";
        return s;
    }
}
