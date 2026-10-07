using System.Text.RegularExpressions;

namespace RegelIde.Data.Strukturkonvertering;

/// <summary>
/// [Ny, #307 strukturmodell-mønster, 2026-10-07] Strukturerte lister i inndelingsforskrifter og
/// inndelingsparagrafer → <c>O bestar_av</c> (og <c>A har_sete_i</c> for rettssteder).
/// <para>
/// Listene er det tydeligste strukturutsagnet lovteksten har, men også der en naiv deling på «og» går
/// galt: «Evje og Hornnes», «Nore og Uvdal», «Sogn og Fjordane tingrett» er ETT navn. Regelen er derfor:
/// del på komma; bare det SISTE elementet deles på sitt første «og» («Sigdal og Øvre Eiker»), og et
/// ledende «og» etter komma fjernes («…, og Øygarden»). Et navn med «og» som står sist i en liste
/// («…, Åmli og Evje og Hornnes») blir feil — det er målt, ikke skjult.
/// </para>
/// <para>
/// Alt-eller-ingenting per liste: hvis ett element ikke begynner med stor bokstav, er det ikke en
/// navneliste (eller den er definert ved retning/komplement), og hele lista forkastes heller enn å
/// gi en delvis og dermed misvisende sammensetning.
/// </para>
/// </summary>
internal static class Inndeling
{
    private const RegexOptions Valg = RegexOptions.CultureInvariant;

    private static readonly Regex RettskretsUttrykk = new(
        @"^\[?(?:.*?\brettskretsen\s+)?(?<navn>[^,:;]{3,120}?\btingrett),\s+med\s+rettssted(?:er)?\s+i\s+(?<steder>[^;]+?),\s+som\s+dekker\s+"
        + @"(?:kommunene\s+(?<liste>.+?)|(?<en>\p{Lu}[\p{L}\-/]*(?:\s+\p{L}[\p{L}\-/]*)?)\s+kommune)\s*(?:,\s*og|,|\.|\s+og)?\s*$", Valg);

    /// <summary>«N tingrett, med rettssteder i A og B, som dekker kommunene …» → kommuner eller rettssteder.</summary>
    public static IReadOnlyList<Monsterfunn> Rettskrets(string setning, bool kommuner)
    {
        var m = RettskretsUttrykk.Match(setning);
        if (!m.Success) return [];
        var navn = m.Groups["navn"].Value.Trim();
        if (!Aktorfrase.ErNavn(navn)) return [];

        IReadOnlyList<string> elementer;
        if (!kommuner) elementer = Navneliste(m.Groups["steder"].Value);
        else if (m.Groups["en"].Success) elementer = [m.Groups["en"].Value];
        else elementer = Navneliste(m.Groups["liste"].Value);

        return Funn(navn, elementer);
    }

    private static readonly Regex KommunelisteHode = new(@"^\[?(?<navn>[^:]{2,160}?):\s*(?<rest>(?:de\s+)?(?:kommunene|fylkene)\b.*)$", Valg);

    private static readonly Regex KommunelisteSegment = new(
        @"\b(?:kommunene|fylkene)\s+(?!fra\s+og\s+med\b)(?<liste>\p{Lu}.*?)"
        + @"(?=\s+i\s+(?:\p{L}+\s+){1,4}?fylker?\b|\s*[.;]?\s*$)", Valg);

    /// <summary>«Østre valgkrets/…: kommunene A, B og C i Finnmark fylke (og kommunene …)».</summary>
    public static IReadOnlyList<Monsterfunn> Kommuneliste(string setning)
    {
        var hode = KommunelisteHode.Match(setning);
        if (!hode.Success) return [];
        var navn = hode.Groups["navn"].Value.Trim();
        if (!Aktorfrase.ErNavn(navn)) return [];

        var funn = new List<Monsterfunn>();
        foreach (Match seg in KommunelisteSegment.Matches(hode.Groups["rest"].Value))
        {
            funn.AddRange(Funn(navn, Navneliste(seg.Groups["liste"].Value)));
        }
        return funn;
    }

    private static readonly Regex KommuneIFylkeUttrykk = new(
        @"\bkommunene\s+(?!fra\s+og\s+med\b)(?<liste>\p{Lu}[^:;]*?)\s+i\s+(?<fylke>\p{Lu}[\p{L}\-]*(?:\s+og\s+\p{Lu}[\p{L}\-]*)?\s+fylke)\b", Valg);

    /// <summary>[Ny, #307, 2026-10-07] «kommunene A, B og C i X fylke» → hver kommune <c>del_av</c> «X fylke».</summary>
    public static IReadOnlyList<Monsterfunn> KommuneIFylke(string setning)
    {
        var funn = new List<Monsterfunn>();
        foreach (Match m in KommuneIFylkeUttrykk.Matches(setning))
        {
            var fylke = m.Groups["fylke"].Value;
            funn.AddRange(Navneliste(m.Groups["liste"].Value).Select(k => new Monsterfunn(k, [k], [fylke], null, "positiv")));
        }
        return funn;
    }

    private static readonly Regex HarRettskretsenUttrykk = new(
        @"^\[?(?<fylke>\p{Lu}[^,:;]{1,80}?\s+fylke)\s+har\s+rettskretsen\s+(?<navn>[^,:;]{3,120}?\btingrett),", Valg);

    /// <summary>[Ny, #307, 2026-10-07] «Agder fylke har rettskretsen Agder tingrett, …» → tingretten <c>del_av</c> fylket.</summary>
    public static IReadOnlyList<Monsterfunn> HarRettskretsen(string setning)
    {
        var m = HarRettskretsenUttrykk.Match(setning);
        if (!m.Success) return [];
        var navn = m.Groups["navn"].Value.Trim();
        var fylke = m.Groups["fylke"].Value.Trim();
        return Aktorfrase.ErNavn(navn) && Aktorfrase.ErNavn(fylke)
            ? [new Monsterfunn(RegexMonster.Sitat(setning[..(m.Groups["navn"].Index)]).Trim(), [navn], [fylke], null, "positiv")]
            : [];
    }

    private static readonly Regex UtgjorUttrykk = new(
        @"^\[?(?:Lagsognene|Kommunene|Fylkene|Rettskretsene|Lagdømmene|Valgkretsene)\s+(?<liste>.+?)\s+utgjør\s+(?<navn>\p{Lu}.+?)\s*\.?$", Valg);

    /// <summary>«Lagsognene A, B og C utgjør N lagdømme.»</summary>
    public static IReadOnlyList<Monsterfunn> Utgjor(string setning)
    {
        var m = UtgjorUttrykk.Match(setning);
        if (!m.Success) return [];
        var navn = m.Groups["navn"].Value.Trim();
        return Aktorfrase.ErNavn(navn) ? Funn(navn, Navneliste(m.Groups["liste"].Value)) : [];
    }

    private static readonly Regex SognerUttrykk = new(
        @"^\[?Til\s+(?:lagsognet|lagdømmet|rettskretsen|domssognet)\s+(?<navn>\p{Lu}.+?)\s+sogner\s+(?<liste>.+?)\s*\.?$", Valg);

    private static readonly Regex Tingrett = new(@"\btingrett\b", Valg);

    /// <summary>«Til lagsognet N sogner A tingrett, B tingrett og C tingrett.»</summary>
    public static IReadOnlyList<Monsterfunn> Sogner(string setning)
    {
        var m = SognerUttrykk.Match(setning);
        if (!m.Success) return [];
        var navn = m.Groups["navn"].Value.Trim();
        if (!Aktorfrase.ErNavn(navn)) return [];
        var liste = m.Groups["liste"].Value;
        // Når elementene er tingretter, er «tingrett» selv skilletegnet — det tåler både navn med «og»
        // («Salten og Lofoten tingrett») og et manglende komma mellom to elementer.
        var elementer = Tingrett.IsMatch(liste) ? DelEtterEtterledd(liste, Tingrett) : Navneliste(liste);
        return Funn(navn, elementer);
    }

    private static readonly Regex BestarAvUttrykk = new(
        @"^\[?(?<navn>\p{Lu}[\p{L}\-]*(?:\s+\p{L}[\p{L}\-]*){0,5}?)\s+består\s+(?:i\s+tillegg\s+)?av\s+(?<liste>\p{Lu}[^.;:]*?)\s*\.?$", Valg);

    /// <summary>«N består (i tillegg) av A, B og C» der alle elementene er egennavn.</summary>
    public static IReadOnlyList<Monsterfunn> BestarAvListe(string setning)
    {
        var m = BestarAvUttrykk.Match(setning);
        if (!m.Success) return [];
        var navn = Aktorfrase.Tolk(m.Groups["navn"].Value);
        if (navn.Count != 1) return [];
        var elementer = Navneliste(m.Groups["liste"].Value);
        return elementer.Count >= 2 ? Funn(navn[0], elementer) : [];
    }

    // ---- Listedeling ---------------------------------------------------------------------------------

    private static readonly Regex Komma = new(@",\s+", Valg);

    /// <summary>
    /// Deler en navneliste etter regelen i klassekommentaren. Returnerer tom liste hvis ett element ikke
    /// begynner med stor bokstav (alt-eller-ingenting). Hvert element er en delstreng av inndata.
    /// </summary>
    public static IReadOnlyList<string> Navneliste(string liste)
    {
        var t = liste.Trim().TrimEnd(',', '.', ';', ' ');
        if (t.EndsWith(" og", StringComparison.Ordinal)) t = t[..^3].TrimEnd(',', ' ');
        if (t.Length == 0) return [];

        var deler = Komma.Split(t).Select(d => d.Trim()).ToList();
        var siste = deler[^1];
        if (siste.StartsWith("og ", StringComparison.Ordinal))
        {
            deler[^1] = siste[3..].Trim();
        }
        else
        {
            var og = siste.IndexOf(" og ", StringComparison.Ordinal);
            // [ENDRET, #307, 2026-10-07] To «og» i siste element («Møre og Romsdal og Trööndelagen/
            // Trøndelag») er tvetydig: ett av dem er inne i et navn, og teksten sier ikke hvilket. Da
            // forkastes lista (alt-eller-ingenting) i stedet for å velge — målt: første versjon delte på
            // første «og» og ga «Møre» + «Romsdal og Trööndelagen/Trøndelag» som to lagsogn.
            if (og > 0 && siste.IndexOf(" og ", og + 4, StringComparison.Ordinal) > 0) return [];
            if (og > 0)
            {
                deler[^1] = siste[..og].Trim();
                deler.Add(siste[(og + 4)..].Trim());
            }
        }

        return deler.All(ErListenavn) ? deler : [];
    }

    private static readonly Regex LedendeSkille = new(@"^(?:,\s*og\s+|,\s*|og\s+)", Valg);

    private static IReadOnlyList<string> DelEtterEtterledd(string liste, Regex etterledd)
    {
        var deler = new List<string>();
        var start = 0;
        foreach (Match m in etterledd.Matches(liste))
        {
            var del = liste[start..(m.Index + m.Length)].Trim();
            del = LedendeSkille.Replace(del, "").Trim();
            deler.Add(del);
            start = m.Index + m.Length;
        }
        return deler.All(ErListenavn) ? deler : [];
    }

    private static bool ErListenavn(string element) =>
        element.Length > 0 && char.IsUpper(element[0]) && !element.Any(char.IsDigit) && element.Split(' ').Length <= 8;

    private static IReadOnlyList<Monsterfunn> Funn(string navn, IReadOnlyList<string> elementer) =>
        elementer.Select(e => new Monsterfunn(e, [navn], [e], null, "positiv")).ToList();
}
