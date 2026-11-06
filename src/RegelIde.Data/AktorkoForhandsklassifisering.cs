using System.Text.Json;
using System.Text.Json.Serialization;

namespace RegelIde.Data;

// [Ny, issue #440, Johanns avgjørelse 2026-11-05] Forhåndsklassifiseringen av navnene i aktørkøen (PR #462) vist i køen, så Johann kan godta
// forslaget med én knapp. Klassifiseringen er laget av en KI-agent (modellereren) og vurdert av juristutfordreren i to runder; ingen menneske
// har kontrollert den. Den er DATA i repoet (data/fasit/strukturmodell/aktorko-forhand/klassifisering.json), innebygd i assemblyen, og
// skrives aldri til basen. «Godta forslaget» fyller bare skjemaene i køen; skrivingen går gjennom de eksisterende endepunktene
// (AktorkoSkrivTjeneste), og et menneske bekrefter hvert kall (docs/33 §5.16.2, docs/09 §54).

/// <summary>Handlingen et forslag tilsvarer i køen. Lukket vokabular, speilet i frontenden (<c>aktorkoForhand.ts</c>).</summary>
public static class Forhandshandling
{
    /// <summary>Aktør, klasse, rolle eller område: <c>POST /api/aktorko/{nøkkel}/godkjenn</c>.</summary>
    public const string Godkjenn = "godkjenn";
    /// <summary>Samme som eller utgått navn for en aktør som finnes: <c>/koble</c>.</summary>
    public const string Koble = "koble";
    /// <summary>Ikke en aktør: <c>/ikke-aktor</c>.</summary>
    public const string IkkeAktor = "ikke_aktor";
    /// <summary>Omtale av en annen node: «Tagg alle som generisk omtale» (<c>/generisk-omtale</c>) for ordet.</summary>
    public const string GeneriskOmtale = "generisk_omtale";
    /// <summary>Samme som et annet navn i køen: <c>/{det andre navnet}/ta-med</c>.</summary>
    public const string TaMed = "ta_med";
    /// <summary>Venter på modellvalget (#463, Norge og nivåene). Ingen knapp.</summary>
    public const string Venter = "venter";
}

/// <summary>Typen i forslaget, med ASCII-koder (JSON-fila har «aktør», «område», «ikke_aktør»).</summary>
public static class Forhandstype
{
    public const string Aktor = "aktor";
    public const string Klasse = "klasse";
    public const string Rolle = "rolle";
    public const string Omrade = "omrade";
    public const string Omtale = "omtale";
    public const string IkkeAktor = "ikke_aktor";
    public const string Venter = "venter";

    public static string FraFil(string type) => type switch
    {
        "aktør" => Aktor,
        "område" => Omrade,
        "ikke_aktør" => IkkeAktor,
        _ => type,
    };
}

// ------------------------------------------------------------------------------------------------ Fila slik den står

/// <summary>Én post i <c>klassifisering.json</c>, slik fila er skrevet (PR #462). Feltene leses som de er; ingenting fylles inn.</summary>
public sealed record ForhandPost
{
    [JsonPropertyName("nr")] public int Nr { get; init; }
    [JsonPropertyName("nokkel")] public string Nokkel { get; init; } = "";
    [JsonPropertyName("navn")] public string Navn { get; init; } = "";
    [JsonPropertyName("forekomster")] public int Forekomster { get; init; }
    [JsonPropertyName("rettskilder")] public int Rettskilder { get; init; }
    [JsonPropertyName("varianter")] public List<ForhandVariant> Varianter { get; init; } = [];
    [JsonPropertyName("koens_hjemmelforslag")] public List<ForhandKoforslag> KoensHjemmelforslag { get; init; } = [];
    [JsonPropertyName("pilot")] public bool Pilot { get; init; }
    [JsonPropertyName("type")] public string Type { get; init; } = "";
    [JsonPropertyName("undertype")] public string? Undertype { get; init; }
    [JsonPropertyName("ny_node")] public string? NyNode { get; init; }
    [JsonPropertyName("relasjoner")] public List<ForhandRelasjonFil> Relasjoner { get; init; } = [];
    [JsonPropertyName("hjemmel")] public ForhandHjemmelFil? Hjemmel { get; init; }
    [JsonPropertyName("hjemmel_merknad")] public string? HjemmelMerknad { get; init; }
    [JsonPropertyName("hjemmel_status")] public string? HjemmelStatus { get; init; }
    [JsonPropertyName("sikkerhet")] public string? Sikkerhet { get; init; }
    [JsonPropertyName("begrunnelse")] public string Begrunnelse { get; init; } = "";
    [JsonPropertyName("venter_pa_modellvalg")] public bool VenterPaModellvalg { get; init; }
    [JsonPropertyName("jurist_runde1")] public string? JuristRunde1 { get; init; }
    [JsonPropertyName("jurist_alvor")] public string? JuristAlvor { get; init; }
    [JsonPropertyName("jurist_innvending")] public string? JuristInnvending { get; init; }
    [JsonPropertyName("modellerer_svar")] public string? ModellererSvar { get; init; }
    [JsonPropertyName("replikk")] public string? Replikk { get; init; }
    [JsonPropertyName("rettet_etter_juristen")] public bool RettetEtterJuristen { get; init; }
    [JsonPropertyName("ta_med_i")] public string? TaMedI { get; init; }
}

public sealed record ForhandVariant(
    [property: JsonPropertyName("normalisert")] string Normalisert,
    [property: JsonPropertyName("regel")] string Regel,
    [property: JsonPropertyName("forekomster")] int Forekomster);

public sealed record ForhandKoforslag(
    [property: JsonPropertyName("eid")] string Eid,
    [property: JsonPropertyName("bestemmelse")] string? Bestemmelse,
    [property: JsonPropertyName("kunnskapsstatus")] string? Kunnskapsstatus);

/// <summary>En relasjon i forslaget. <see cref="Til"/> er et objekt med <c>id</c> (node i basen), <c>ny_node</c> (finnes ikke ennå) eller
/// <c>tekst</c>, eller null (for eksempel en medlemsregel uten målnode).</summary>
public sealed record ForhandRelasjonFil(
    [property: JsonPropertyName("relasjon")] string Relasjon,
    [property: JsonPropertyName("til")] JsonElement? Til,
    [property: JsonPropertyName("merknad")] string? Merknad);

public sealed record ForhandHjemmelFil(
    [property: JsonPropertyName("rettskildeId")] Guid? RettskildeId,
    [property: JsonPropertyName("rettskilde")] string? Rettskilde,
    [property: JsonPropertyName("eid")] string Eid,
    [property: JsonPropertyName("sitat")] string? Sitat,
    [property: JsonPropertyName("kunnskapsstatus")] string? Kunnskapsstatus,
    [property: JsonPropertyName("kilde")] string? Kilde,
    [property: JsonPropertyName("tolkningsargumenter")] List<string>? Tolkningsargumenter = null,
    [property: JsonPropertyName("tolkningsbegrunnelse")] string? Tolkningsbegrunnelse = null);

public sealed record ForhandFil(
    [property: JsonPropertyName("generert")] string Generert,
    [property: JsonPropertyName("antall")] int Antall,
    [property: JsonPropertyName("navn")] List<ForhandPost> Navn);

// ------------------------------------------------------------------------------------------------ Det API-et viser

/// <summary>Kort form i lista (<c>GET /api/aktorko</c>): det filtrene trenger.</summary>
/// <param name="Type">Typen i forslaget (<see cref="Forhandstype"/>). <c>venter</c> også når fila har en annen type, men navnet venter på
/// modellvalg (Finland og Sverige står som område i fila).</param>
/// <param name="Sikkerhet"><c>hoy</c>, <c>middels</c>, <c>lav</c>, eller null («-» i fila: ikke vurdert).</param>
/// <param name="Utdatert">Navnet har endret seg siden klassifiseringen (andre forekomster eller varianter). Teksten sier hva.</param>
public sealed record AktorkoForhandKort(int Nr, string Type, string? Sikkerhet, string Handling, string? Utdatert);

/// <summary>En node relasjonen peker på.</summary>
/// <param name="Id">Id-en i basen. Null for en ny node eller en tekst.</param>
/// <param name="Art"><c>virksomhet</c> eller <c>begrep</c> når id-en finnes i basen; null ellers.</param>
/// <param name="Kategori">Begrepskategorien (klasse, rolle, omrade …) for et begrep.</param>
/// <param name="Ny">Noden finnes ikke ennå (<c>ny_node</c> i fila).</param>
/// <param name="Finnes">Id-en finnes i basen nå. False for en id som ikke finnes (vises uten lenke), og for ny node og tekst.</param>
public sealed record ForhandNode(Guid? Id, string Navn, string? Art, string? Kategori, bool Ny, bool Finnes);

/// <summary>En relasjon i forslaget, med om handlingen skriver den.</summary>
/// <param name="SkrivesAvHandlingen">Handlingen (koble) skriver relasjonen: navnet blir navneform på aktøren.</param>
/// <param name="EgetSteg">Hvorfor relasjonen ikke skrives nå, og hva som må gjøres. Null når handlingen skriver den.</param>
public sealed record ForhandRelasjon(string Relasjon, ForhandNode? Til, string? Merknad, bool SkrivesAvHandlingen, string? EgetSteg);

/// <summary>Hjemmelen (eller medlemsregelens kilde) i forslaget.</summary>
/// <param name="RettskildeId">Rettskilden bestemmelsen står i: fra fila, eller slått opp på eId-en i basen (bare når det er nøyaktig én).</param>
/// <param name="Sitat">Teksten. Fra fila når den har sitatet; ellers bestemmelsens tekst i basen nå.</param>
/// <param name="SitatKilde"><c>klassifiseringen</c>, <c>basen</c> eller <c>ingen</c> (eId-en finnes ikke i basen).</param>
/// <param name="ErRegelensForslag">Samme rettskilde og eId som et av køens hjemmelforslag.</param>
/// <param name="KanLagres">[ENDRET, Johanns avgjørelse 2026-11-05] Godkjenningen kan lagre hjemmelen fra fila
/// (<see cref="Aktorkohjemmel.Forhandsklassifisering"/>): bestemmelsen står i korpus, og rettskilden er entydig. Fila har ingen hjemmel
/// utenfor korpus (type og lenke), så en eId som ikke står i korpus, kan ikke lagres og står som eget steg.</param>
/// <param name="Tolkningsargumenter">[Ny, #440, Johanns oppdrag 2026-11-06] Argumentene fra fila når hjemmelen er fortolkning (lukket liste,
/// <see cref="Strukturkanter.Tolkningsargumenter"/>), vurdert i juristrunden i <c>aktorko-forhand/tolkningsargumenter/</c>. Godkjenningen
/// lagrer disse og ingen andre. Null når fila ikke har dem.</param>
/// <param name="Tolkningsbegrunnelse">Hva hvert argument bygger på, i klart språk. Står i begrunnelsen som lagres.</param>
public sealed record ForhandHjemmel(
    Guid? RettskildeId, string? Rettskilde, string Eid, string? Bestemmelse, string? Sitat, string SitatKilde, string? Kunnskapsstatus,
    bool ErRegelensForslag, bool KanLagres = false, IReadOnlyList<string>? Tolkningsargumenter = null, string? Tolkningsbegrunnelse = null);

/// <summary>Aktøren navnet skal kobles til (handlingen <see cref="Forhandshandling.Koble"/>).</summary>
/// <param name="Navneformgrunn">Grunnen relasjonen gir: <c>utgatt</c> for utgått navn, <c>kortform</c> når merknaden sier kortform. Null når
/// forslaget ikke sier det; da velger mennesket (ingen gjettet grunn).</param>
public sealed record ForhandKobling(Guid VirksomhetId, string VirksomhetNavn, bool Finnes, string Relasjon, string? Navneformgrunn);

/// <summary>Hele forslaget for ett navn (<c>GET /api/aktorko/{nøkkel}</c>).</summary>
/// <param name="Typeforslag">Typen slik fila har den (Finland: område), også når navnet venter.</param>
/// <param name="TaMedINokkel">For <see cref="Forhandshandling.TaMed"/>: nøkkelen til navnet ordet skal tas med i.</param>
/// <param name="EgneSteg">Det handlingen ikke skriver, i klart språk: relasjoner, en hjemmel som ikke er regelens forslag, et navn som ikke
/// står i teksten, aktørtypen.</param>
public sealed record AktorkoForhandsklassifisering(
    int Nr, string Type, string Typeforslag, string? Undertype, string? NyNode, string Handling, string? Sikkerhet, string Begrunnelse,
    IReadOnlyList<ForhandRelasjon> Relasjoner, ForhandHjemmel? Hjemmel, string? HjemmelStatus, string? HjemmelMerknad,
    string? Juristdom, string? JuristAlvor, string? JuristInnvending, string? ModellererSvar, string? Replikk, bool RettetEtterJuristen,
    bool VenterPaModellvalg, bool Pilot, string? Utdatert, ForhandKobling? Kobling, string? TaMedINokkel, string? TaMedINavn,
    IReadOnlyList<string> EgneSteg, string Kilde);

/// <summary>Det <see cref="Forhandsklassifisering.Visning"/> trenger fra basen: nodene relasjonene peker på, og bestemmelsene hjemlene står i.</summary>
public sealed record ForhandOppslag(
    IReadOnlyDictionary<Guid, (string Art, string Navn, string? Kategori)> Noder,
    IReadOnlyDictionary<string, (Guid RettskildeId, string Rettskilde, string? Tekst)> Bestemmelser);

/// <summary>
/// [Ny, issue #440, 2026-11-05] Leser forhåndsklassifiseringen (innebygd ressurs) og regner ut hva den betyr for et navn i køen: handlingen,
/// om navnet er endret siden, og hva som må gjøres i eget steg. Rene funksjoner; oppslagene i basen gjør <see cref="AktorkoTjeneste"/>.
/// </summary>
public static class Forhandsklassifisering
{
    public const string Ressursnavn = "RegelIde.Data.Aktorko.forhandsklassifisering.json";
    public const string Kilde = "data/fasit/strukturmodell/aktorko-forhand/klassifisering.json";

    private static readonly Lazy<ForhandFil> Fil = new(LesFil);

    /// <summary>Alle postene, i fila sin rekkefølge.</summary>
    public static IReadOnlyList<ForhandPost> Alle => Fil.Value.Navn;

    /// <summary>Datoen fila ble laget.</summary>
    public static string Generert => Fil.Value.Generert;

    private static readonly Lazy<Dictionary<string, ForhandPost>> PerNokkel = new(() => Alle.ToDictionary(p => p.Nokkel, StringComparer.Ordinal));
    private static readonly Lazy<Dictionary<string, ForhandPost>> PerNr = new(() => Alle.ToDictionary(p => $"n{p.Nr}", StringComparer.Ordinal));

    internal static ForhandFil LesFil()
    {
        using var strom = typeof(Forhandsklassifisering).Assembly.GetManifestResourceStream(Ressursnavn)
            ?? throw new InvalidOperationException($"Fant ikke den innebygde ressursen {Ressursnavn}.");
        return JsonSerializer.Deserialize<ForhandFil>(strom) ?? throw new InvalidOperationException($"{Ressursnavn} er tom.");
    }

    /// <summary>
    /// Posten for et navn i køen, knyttet med nøkkelen. Står ikke nøkkelen i fila (nøkkelen er ordet med flest forekomster, og den kan skifte når
    /// tallene endrer seg), prøves de andre variantene; da er navnet alltid endret siden, og <see cref="Utdatert"/> sier det. Null når ingen
    /// variant står som nøkkel: navnet kom i køen etter klassifiseringen.
    /// </summary>
    /// <remarks>[ENDRET, 2026-11-05] Siste utvei: posten som har en av navnets varianter blant sine varianter, når det er nøyaktig én
    /// («omsorgskommunen» alene i køen er varianten av posten «omsorgskommune»). Flere poster gir null: posten gjettes ikke.</remarks>
    public static ForhandPost? Finn(Kogruppe g)
    {
        if (PerNokkel.Value.GetValueOrDefault(g.Nokkel) is { } p) return p;
        if (g.Varianter.Select(v => PerNokkel.Value.GetValueOrDefault(v.Normalisert)).FirstOrDefault(x => x is not null) is { } q) return q;
        var ord = g.Varianter.Select(v => v.Normalisert).ToHashSet(StringComparer.Ordinal);
        return Alle.Where(x => x.Varianter.Any(v => ord.Contains(v.Normalisert))).ToList() is [var eneste] ? eneste : null;
    }

    /// <summary>Posten med nummeret («n70»), for «ta med i n70».</summary>
    public static ForhandPost? FinnNr(string nr) => PerNr.Value.GetValueOrDefault(nr);

    /// <summary>Typen filtrene bruker: <c>venter</c> når navnet venter på modellvalg, ellers typen i fila.</summary>
    public static string Type(ForhandPost p) => p.VenterPaModellvalg ? Forhandstype.Venter : Forhandstype.FraFil(p.Type);

    /// <summary>«-» i fila betyr ikke vurdert (navnene som venter).</summary>
    public static string? Sikkerhet(ForhandPost p) => p.Sikkerhet is "hoy" or "middels" or "lav" ? p.Sikkerhet : null;

    /// <summary>
    /// Handlingen forslaget tilsvarer. Samme utledning som kolonnen «Anbefaling» i <c>anbefalinger.md</c>, med ett avvik: navnene med
    /// <c>venter_pa_modellvalg</c> (også Finland og Sverige, som fila har som område) får <see cref="Forhandshandling.Venter"/> og ingen knapp
    /// (Johanns avgjørelse 2026-11-05: ingen forslag før beslutningsnotatet, #463).
    /// </summary>
    public static string Handling(ForhandPost p)
    {
        if (p.VenterPaModellvalg) return Forhandshandling.Venter;
        return Forhandstype.FraFil(p.Type) switch
        {
            Forhandstype.Aktor or Forhandstype.Klasse or Forhandstype.Rolle or Forhandstype.Omrade => Forhandshandling.Godkjenn,
            Forhandstype.IkkeAktor => Forhandshandling.IkkeAktor,
            Forhandstype.Omtale when p.TaMedI is not null => Forhandshandling.TaMed,
            Forhandstype.Omtale when Koblingsrelasjon(p) is not null => Forhandshandling.Koble,
            Forhandstype.Omtale => Forhandshandling.GeneriskOmtale,
            _ => Forhandshandling.Venter,
        };
    }

    /// <summary>Relasjonen som gjør en omtale til en kobling: «samme som» eller «utgått navn» til en aktør (id i tabellen virksomheter).</summary>
    public static ForhandRelasjonFil? Koblingsrelasjon(ForhandPost p) =>
        p.Relasjoner.FirstOrDefault(r => r.Relasjon is "samme_som" or "utgatt_navn" && Felt(r.Til, "id") is not null
                                         && Felt(r.Til, "tabell") == "virksomheter");

    /// <summary>Navneformgrunnen relasjonen gir: utgått navn → <c>utgatt</c>, en merknad som begynner med «kortform» → <c>kortform</c>.
    /// Ellers null: forslaget sier ikke hvilken grunn, og den gjettes ikke (CLAUDE.md §8).</summary>
    public static string? Navneformgrunn(ForhandRelasjonFil r) =>
        r.Relasjon == "utgatt_navn" ? "utgatt"
        : (r.Merknad ?? "").StartsWith("kortform", StringComparison.OrdinalIgnoreCase) ? "kortform"
        : null;

    /// <summary>
    /// Hva som er endret siden klassifiseringen: antall forekomster eller variantene (ordene og tallene per ord). Null når navnet er likt.
    /// Rettskildene telles ikke for seg: en ny rettskilde gir alltid en ny forekomst.
    /// </summary>
    public static string? Utdatert(ForhandPost p, Kogruppe g)
    {
        var deler = new List<string>();
        if (p.Nokkel != g.Nokkel) deler.Add($"nøkkelen var «{p.Nokkel}», nå «{g.Nokkel}»");
        if (p.Forekomster != g.Forekomster.Count) deler.Add($"{p.Forekomster} forekomster da, {g.Forekomster.Count} nå");
        var da = p.Varianter.ToDictionary(v => v.Normalisert, v => v.Forekomster, StringComparer.Ordinal);
        var na = g.Varianter.ToDictionary(v => v.Normalisert, v => v.Forekomster, StringComparer.Ordinal);
        var nye = na.Keys.Except(da.Keys).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var borte = da.Keys.Except(na.Keys).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var endret = da.Keys.Intersect(na.Keys).Where(k => da[k] != na[k]).OrderBy(x => x, StringComparer.Ordinal).ToList();
        if (nye.Count > 0) deler.Add("nye varianter: " + string.Join(", ", nye.Select(x => $"«{x}»")));
        if (borte.Count > 0) deler.Add("varianter som ikke står i navnet nå: " + string.Join(", ", borte.Select(x => $"«{x}»")));
        if (endret.Count > 0 && p.Forekomster == g.Forekomster.Count) deler.Add("andre tall per variant: " + string.Join(", ", endret.Select(x => $"«{x}»")));
        return deler.Count == 0 ? null : string.Join("; ", deler);
    }

    /// <summary>Kortformen i lista.</summary>
    public static AktorkoForhandKort? Kort(Kogruppe g) =>
        Finn(g) is { } p ? new AktorkoForhandKort(p.Nr, Type(p), Sikkerhet(p), Handling(p), Utdatert(p, g)) : null;

    /// <summary>Id-ene relasjonene peker på, for oppslaget i basen.</summary>
    public static IEnumerable<Guid> NodeIder(ForhandPost p) =>
        p.Relasjoner.Select(r => Felt(r.Til, "id")).Where(x => x is not null).Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).Distinct();

    /// <summary>
    /// Hele forslaget for visningen. <paramref name="regelensForslag"/> er køens hjemmelforslag nå (rettskilde og eId); bare en hjemmel blant
    /// dem kan lagres av godkjenningen (<c>AktorkoSkrivTjeneste.GodkjennAsync</c>). Alt annet listes i <see cref="AktorkoForhandsklassifisering.EgneSteg"/>.
    /// </summary>
    public static AktorkoForhandsklassifisering Visning(ForhandPost p, Kogruppe g, IReadOnlyCollection<(Guid RettskildeId, string Eid)> regelensForslag,
        ForhandOppslag oppslag)
    {
        var handling = Handling(p);
        var kobling = handling == Forhandshandling.Koble ? Koblingsrelasjon(p) : null;
        var egneSteg = new List<string>();

        var relasjoner = p.Relasjoner.Select(r =>
        {
            var til = Node(r.Til, oppslag);
            var skrives = kobling is not null && ReferenceEquals(r, kobling);
            return new ForhandRelasjon(r.Relasjon, til, r.Merknad, skrives, skrives ? null : EgetSteg(r, til, handling));
        }).ToList();
        foreach (var r in relasjoner.Where(r => r.EgetSteg is not null && r.Relasjon != "venter"))
        {
            egneSteg.Add($"{Relasjonstekst(r.Relasjon)} {r.Til?.Navn ?? r.Merknad ?? ""}".Trim() + $": {r.EgetSteg}");
        }

        ForhandHjemmel? hjemmel = null;
        if (p.Hjemmel is { } h)
        {
            oppslag.Bestemmelser.TryGetValue(h.Eid, out var b);
            var rettskildeId = h.RettskildeId ?? (b.RettskildeId == Guid.Empty ? null : b.RettskildeId);
            var regelens = rettskildeId is { } id && regelensForslag.Contains((id, h.Eid));
            var kanLagres = b.RettskildeId != Guid.Empty || regelens;
            var sitat = !string.IsNullOrWhiteSpace(h.Sitat) ? h.Sitat : b.Tekst;
            hjemmel = new ForhandHjemmel(kanLagres ? b.RettskildeId : rettskildeId, b.Rettskilde ?? h.Rettskilde, h.Eid, AktorkoTjeneste.Bestemmelse(h.Eid),
                sitat, !string.IsNullOrWhiteSpace(h.Sitat) ? "klassifiseringen" : b.Tekst is not null ? "basen" : "ingen", h.Kunnskapsstatus, regelens, kanLagres,
                h.Tolkningsargumenter, h.Tolkningsbegrunnelse);
            if (handling == Forhandshandling.Godkjenn && !kanLagres)
            {
                egneSteg.Add($"Hjemmelen ({h.Rettskilde ?? "rettskilden"} {hjemmel.Bestemmelse ?? h.Eid}) er ikke funnet entydig i korpus, og fila har ingen kilde "
                             + "utenfor korpus. Godkjenningen kan ikke lagre den; den registreres etterpå på aktørsiden eller begrepssiden.");
            }
        }

        // [ENDRET, Johanns avgjørelse 2026-11-05] Et navn i forslaget som ikke står i teksten (grunnformen), kan nå lagres av godkjenningen,
        // kontrollert mot fila. Det er ikke lenger et eget steg.
        if (handling == Forhandshandling.Godkjenn && Forhandstype.FraFil(p.Type) == Forhandstype.Aktor && p.Undertype is { } undertype)
        {
            egneSteg.Add($"Aktørtypen ({undertype}) settes på aktørsiden; godkjenningen oppretter aktøren uten type.");
        }

        var taMed = p.TaMedI is { } nr ? FinnNr(nr) : null;
        var koblingsvisning = kobling is not null && Node(kobling.Til, oppslag) is { Id: { } vid } node
            ? new ForhandKobling(vid, node.Navn, node.Finnes, kobling.Relasjon, Navneformgrunn(kobling))
            : null;

        return new AktorkoForhandsklassifisering(p.Nr, Type(p), Forhandstype.FraFil(p.Type), p.Undertype, p.NyNode, handling, Sikkerhet(p),
            p.Begrunnelse, relasjoner, hjemmel, p.HjemmelStatus, p.HjemmelMerknad, p.JuristRunde1, p.JuristAlvor, p.JuristInnvending,
            p.ModellererSvar, p.Replikk, p.RettetEtterJuristen, p.VenterPaModellvalg, p.Pilot, Utdatert(p, g), koblingsvisning,
            taMed?.Nokkel, taMed?.Navn, egneSteg, $"{Kilde}, n{p.Nr}");
    }

    private static string? EgetSteg(ForhandRelasjonFil r, ForhandNode? til, string handling)
    {
        if (r.Relasjon == "venter") return "venter på modellvalget (#463).";
        if (til is { Ny: true }) return $"«{til.Navn}» finnes ikke i basen ennå. Den opprettes først, så kan relasjonen registreres.";
        if (til is { Id: not null, Finnes: false }) return "noden finnes ikke i basen nå (id-en er ikke funnet). Kontroller forslaget.";
        return r.Relasjon switch
        {
            "medlemsregel" => "medlemsregelen registreres på klassen (begrepssiden). Køen skriver ikke medlemsregler.",
            "omtale_av" or "generisk_omtale" when handling == Forhandshandling.GeneriskOmtale =>
                "«Tagg alle som generisk omtale» binder ikke ordet. Bindingen til noden registreres som navneformbinding (docs/33 §5.7).",
            _ => "køen skriver ikke relasjoner. Registreres som strukturkant eller begrepsrelasjon.",
        };
    }

    /// <summary>Relasjonskoden som tekst i «eget steg». Visningen har sin egen tabell (<c>aktorkoForhand.ts</c>).</summary>
    public static string Relasjonstekst(string relasjon) => relasjon switch
    {
        "rolle_for" => "Rolle for",
        "rolle_innehas_av" => "Innehas av",
        "medlem" => "Medlem",
        "medlemsregel" => "Medlemsregel",
        "underklasse_av" => "Underklasse av",
        "organ_i" => "Organ i",
        "samme_som" => "Samme som",
        "omtale_av" => "Omtale av",
        "utgatt_navn" => "Utgått navn for",
        "kortform_for" => "Kortform for",
        _ => char.ToUpperInvariant(relasjon[0]) + relasjon[1..].Replace('_', ' '),
    };

    private static ForhandNode? Node(JsonElement? til, ForhandOppslag oppslag)
    {
        if (til is not { ValueKind: JsonValueKind.Object } t) return null;
        if (Felt(t, "ny_node") is { } ny) return new ForhandNode(null, ny, null, null, true, false);
        if (Felt(t, "id") is { } idTekst && Guid.TryParse(idTekst, out var id))
        {
            var funnet = oppslag.Noder.TryGetValue(id, out var n);
            return new ForhandNode(id, funnet ? n.Navn : Felt(t, "navn") ?? idTekst, funnet ? n.Art : null, funnet ? n.Kategori : Felt(t, "kategori"),
                false, funnet);
        }
        return Felt(t, "tekst") is { } tekst ? new ForhandNode(null, tekst, null, null, false, false) : null;
    }

    private static string? Felt(JsonElement? e, string navn) =>
        e is { ValueKind: JsonValueKind.Object } o && o.TryGetProperty(navn, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
