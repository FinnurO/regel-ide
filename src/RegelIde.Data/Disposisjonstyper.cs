using System.Text.RegularExpressions;

namespace RegelIde.Data;

/// <summary>Ett treff fra <see cref="Disposisjonstyper"/>-klassifisereren: typen, regelen, metoden og grunnlaget.</summary>
public sealed record Disposisjonstreff(string Disposisjonstype, string Kilde, string Regel, string Treff, string? NodeEid);

/// <summary>
/// [Ny, issue #395 del 1, 2026-10-23] Den lukkede listen over disposisjonstyper og den deterministiske klassifisereren (tittel først,
/// deretter mønster på innholdet der tittelen ikke avgjør). Ren funksjon uten database: samme regler brukes av
/// <see cref="DisposisjonstypeTjeneste"/> (korpuset), av <see cref="LovtidendImportTjeneste"/> (utvalget av kunngjorte vedtak som skal
/// importeres) og av testene.
/// <para>
/// <b>Hvorfor tittelen først.</b> Lovdata-titler på organiseringsvedtak er formelle og navngir disposisjonen («Delegering av myndighet
/// etter …», «Overføring av forvaltningsansvaret for … fra … til …», «Opprettelse av Næringsdepartementet og nedleggelse av
/// Industridepartementet»). En tittelregel er derfor en normalisering av ordformen, ikke en tolkning (kunnskapsstatus
/// <c>normalisering</c>, docs/33 §4.6). Innholdsmønstrene kjøres bare når tittelen ikke ga noen type, eller bare ga
/// <c>omorganisering</c> (en paraplytittel som «Omorganisering av departementsstrukturen» sier ikke hva som opprettes og nedlegges —
/// det står i punktene). Å kjøre dem på vanlige reguleringer ville gjort hver forskrift med «myndigheten delegeres» i en paragraf til
/// et delegeringsvedtak.
/// </para>
/// <para>
/// <b>Ingen gjetting (CLAUDE.md §8).</b> Ingen regel slår til → ingen rad, kilden står som uklassifisert. <c>regulering</c> settes bare
/// når tittelen har form av et regelverk («Forskrift om …», «Lov om …», «Reglement for …») og ingen annen type slo til, og aldri på et
/// rent endringsvedtak («Forskrift om endring i …»), som ikke er en disposisjonstype i den lukkede listen (sveipefilteret
/// <see cref="Sveipeutvalg"/> kjenner det igjen på tittelen). KI-laget er ikke bygget i denne runden; kolonnen <c>kilde</c> tar
/// <c>ki</c> slik at det kan legges til uten migrasjon.
/// </para>
/// </summary>
public static partial class Disposisjonstyper
{
    public const string Oppretting = "oppretting";
    public const string Navneendring = "navneendring";
    public const string Nedleggelse = "nedleggelse";
    public const string Omorganisering = "omorganisering";
    public const string OverforingRessortfordeling = "overforing_ressortfordeling";
    public const string Delegering = "delegering";
    public const string Instruks = "instruks";
    public const string Ikrafttredelse = "ikrafttredelse";
    public const string Regulering = "regulering";

    /// <summary>[LÅST, #395 del 1] Den lukkede listen, i den rekkefølgen saken oppgir den. Speilet i CHECK-en på tabellen.</summary>
    public static readonly IReadOnlyList<string> Alle =
        [Oppretting, Navneendring, Nedleggelse, Omorganisering, OverforingRessortfordeling, Delegering, Instruks, Ikrafttredelse, Regulering];

    /// <summary>[LÅST, #395 presisering 2026-10-23] Organiseringstypene: det aktørsveipet og livsløpet bruker, og det første
    /// Lovtidend-uttrekket importerer. Ikrafttredelse og regulering er ikke med.</summary>
    public static readonly IReadOnlySet<string> Organiseringstyper = new HashSet<string>
        { Oppretting, Navneendring, Nedleggelse, Omorganisering, OverforingRessortfordeling, Delegering, Instruks };

    /// <summary>Livsløpstypene (del 3): de organiseringstypene et departements livsløp kan stå i. Delegering og instruks er ikke med.</summary>
    public static readonly IReadOnlySet<string> Livslopstyper = new HashSet<string>
        { Oppretting, Navneendring, Nedleggelse, Omorganisering, OverforingRessortfordeling };

    public const string KildeTittel = "tittel";
    public const string KildeMonster = "monster";
    public const string KildeKi = "ki";
    public const string KildeManuell = "manuell";

    private const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // ---------------------------------------------------------------- Tittelregler
    // Hver regel er (id, type, mønster). Id-en står i raden (regel = "tittel:<id>") slik at en kontrollør ser hvilken regel som slo til.
    // Målt mot korpusets 5 873 titler og Lovtidend-arkivets titler 2026-10-23 (se docs/08 §Lovtidend og PR-en for #395).

    // «Ikrafttredelse», «Ikraftsetting», «Ikraftsetjing», «Delvis ikrafttredelse», «Meddelelse om ikrafttredelse». \b foran: ikke «kraftforsyningen».
    [GeneratedRegex(@"\bikraft\w*", Opt)] private static partial Regex TIkraft();

    // «Delegering av myndighet», «delegasjon», «delegert», «Del. av fullmakt» (Lovdatas forkortelse i eldre titler).
    // [ENDRET etter måling 2026-10-23] + «Tildeling av myndighet …», «Bemyndigelse …» og «Fullmakt til …» først i tittelen.
    [GeneratedRegex(@"\bdelegering\w*|\bdelegasjon\w*|\bdelegert\w*|\bdelegere\w*|\bDel\. av fullmakt|\btildeling av (myndighet|kompetanse)\b|\bbemyndig\w*|^fullmakt\b", Opt)]
    private static partial Regex TDelegering();

    // «Overføring av myndighet til X etter lov …» UTEN «fra»: myndighet legges fra Kongen (implisitt) til et organ — en delegering.
    [GeneratedRegex(@"\boverføring av (myndighet|kompetanse|fullmakt)\b(?!.*\bfra\b)", Opt)] private static partial Regex TOverforingUtenFra();

    // Ressortfordeling: ansvar/myndighet flyttes FRA ett organ TIL et annet, eller ansvar for lover fordeles.
    [GeneratedRegex(@"\boverføring av\b.*\bfra\b.*\btil\b|\bforvaltningsansvar\w*|\bressortfordeling\w*|\bfordeling av (ansvar|forvaltningsansvar)\w*|\bansvarsfordeling\w*|\boverføring av (ansvar|lovansvar|oppgaver|ansvarsområde)\w*", Opt)]
    private static partial Regex TOverforing();

    // [ENDRET etter måling 2026-10-23] + sammensatt ord FØRST i tittelen («Våpeninstruks for politiet»), men ikke midt i («… fastsettelse av
    // styreinstruks» i et delegeringsvedtak er ikke en instruks).
    // [ENDRET igjen etter måling 2026-10-23] Bare FØRST i tittelen: midt i tittelen er instruksen objektet for et annet vedtak
    // («Delegering av myndighet til å endre instruksen for …», «… og om opphevelse av instruks for Statens helsetilsyn»,
    // «instruksjonsmyndighet»). «Vedtak om endring i instruks for Regjeringsadvokaten» er et instruksvedtak og er med.
    [GeneratedRegex(@"^(vedtak om |forskrift om )?(endring(er)? i |utfyllande |utfyllende )?\p{L}*instruks(en)?\b", Opt)] private static partial Regex TInstruks();

    // Oppretting av et ORGAN: objektet er et egennavn (stor forbokstav), eventuelt etter «et/en/ei» og «ny/nytt». «Opprettelse av et permanent
    // restriksjonsområde» og «et harmonisert europeisk rapporteringssystem» er ikke organisering og treffer ikke. «opprettholdelse» treffer ikke.
    // Nøkkelordet uten skille på store og små bokstaver («Opprettelse av …» først i tittelen), forbokstaven i objektet MED.
    [GeneratedRegex(@"\b(?i:opprettelse|oppretting|opprettinga|etablering|etableringen) av (?i:et |en |ei )?(?i:ny |nytt |nye )?\p{Lu}", RegexOptions.CultureInvariant)]
    private static partial Regex TOpprettingStorForbokstav();

    [GeneratedRegex(@"\b(?i:nedleggelse|nedlegging|nedlegginga|avvikling|avviklingen) av (?i:et |en |ei )?\p{Lu}|(?i:\blegges ned\b|\bnedlegges\b)", RegexOptions.CultureInvariant)]
    private static partial Regex TNedleggelse();

    [GeneratedRegex(@"\bnavneendring\w*|\bendring av navn\b|\bnytt navn\b|\bnamneendring\w*|\bnytt namn\b|\bnavn(et)? endres\b|\bendrer navn\b|\bskifter navn\b", Opt)]
    private static partial Regex TNavneendring();

    [GeneratedRegex(@"\bomorganis\w*|\breorganis\w*|\bdepartements?truktur\w*|\bsammenslåing\w*|\bsamanslåing\w*|\bslås sammen\b|\bslås saman\b", Opt)]
    private static partial Regex TOmorganisering();

    // Regelverkets form. «Midlertidig forskrift», «Forskrifter om», «Reglement for», «Vedtekter for», «Regulativ».
    [GeneratedRegex(@"^(midlertidig\s+|midlertidige\s+|mellombels\s+)?(forskrift|forskrifter|forskrifta|lov|lova|loven|reglement|vedtekter|regulativ)\b", Opt)]
    private static partial Regex TRegelverksform();

    /// <summary>Et rent endringsvedtak: «Forskrift om endring(er) i …», «Lov om endringar i …», «Endring i forskrift …». Ikke en
    /// disposisjonstype (lukket liste), men sveipefilteret hopper over det som standard (#395 presisering).</summary>
    [GeneratedRegex(@"^((forskrift|lov|vedtak)\s+(om|til)\s+)?(endring|endringer|endringar|endring og oppheving|endringer og opphevelse|oppheving|opphevelse)\s+(i|av)\b", Opt)]
    private static partial Regex TRenEndring();

    public static bool ErRentEndringsvedtak(string tittel) => TRenEndring().IsMatch(tittel.Trim());

    /// <summary>Tittelreglene. Returnerer alle typer som slo til (tom liste = tittelen avgjør ikke).</summary>
    public static IReadOnlyList<Disposisjonstreff> KlassifiserTittel(string tittel)
    {
        var t = tittel.Trim();
        var treff = new List<Disposisjonstreff>();
        void Legg(string type, string regel)
        {
            if (treff.All(x => x.Disposisjonstype != type)) treff.Add(new(type, KildeTittel, $"tittel:{regel}", Avkort(t), null));
        }

        if (TIkraft().IsMatch(t)) Legg(Ikrafttredelse, "ikrafttredelse");
        if (TDelegering().IsMatch(t)) Legg(Delegering, "delegering");
        if (TOverforingUtenFra().IsMatch(t)) Legg(Delegering, "overforing-av-myndighet-til");
        if (TOverforing().IsMatch(t)) Legg(OverforingRessortfordeling, "overforing-fra-til");
        if (TInstruks().IsMatch(t)) Legg(Instruks, "instruks");
        if (TOpprettingStorForbokstav().IsMatch(t))
            Legg(Oppretting, "opprettelse-av-organ");
        if (TNedleggelse().IsMatch(t)) Legg(Nedleggelse, "nedleggelse-av-organ");
        if (TNavneendring().IsMatch(t)) Legg(Navneendring, "navneendring");
        if (TOmorganisering().IsMatch(t)) Legg(Omorganisering, "omorganisering");

        if (treff.Count == 0 && TRegelverksform().IsMatch(t) && !ErRentEndringsvedtak(t))
        {
            Legg(Regulering, "regelverksform");
        }
        return treff;
    }

    // ---------------------------------------------------------------- Innholdsmønstre (punktene i vedtaket)

    [GeneratedRegex(@"^\s*(opprettelse|oppretting|etablering) av\b|\bDet opprettes\b|\bopprettes (et|en|ei) \p{Lu}|\bopprettes som\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex MOppretting();
    [GeneratedRegex(@"^\s*(nedleggelse|nedlegging|avvikling) av\b|\blegges ned\b|\bnedlegges\b", Opt)] private static partial Regex MNedleggelse();
    // [ENDRET etter måling 2026-10-23] + omvendt ordstilling etter et adverbial: «Med virkning fra 1. januar 2024 endrer Olje- og
    // energidepartementet navn til Energidepartementet» (FOR-2023-10-16-1668).
    [GeneratedRegex(@"\bnavn(et)? endres( fra [^.]{1,40}?)? til\b|\b(endrer|skifter)\b[^.]{0,120}?\bnavn til\b|\bfår (nytt )?navnet\b|\bnamn(et)? vert endra til\b|\bendrar namn til\b", Opt)]
    private static partial Regex MNavneendring();
    [GeneratedRegex(@"\bforvaltningsansvaret for\b|^\s*overføring av (ansvaret|forvaltningsansvaret|myndighet|ansvar)\b|\boverføres fra\b|\bblir overført fra\b", Opt)]
    private static partial Regex MOverforing();
    [GeneratedRegex(@"\bdelegeres\b|^\s*delegering (til|av)\b|\bdelegerer\b", Opt)] private static partial Regex MDelegering();
    [GeneratedRegex(@"\bslås sammen\b|\bslås saman\b|^\s*sammenslåing av\b", Opt)] private static partial Regex MOmorganisering();

    /// <summary>Om innholdsmønstrene skal kjøres for en kilde med disse tittelreglene: bare når tittelen ikke avgjorde, eller bare ga
    /// paraplytypen <c>omorganisering</c>. Se klassekommentaren.</summary>
    /// <para>[ENDRET etter måling 2026-10-23] For en uklassifisert tittel bare når kilden er kort (høyst <see cref="MaksNoderForInnholdsmonstre"/>
    /// noder): et organiseringsvedtak er noen få punkter. Uten grensen ga Grunnloven («nedlegges eden»), Bestemmelser om økonomistyring i
    /// staten og Kirkeordningen treff på nedleggelse og delegering.</para>
    public static bool SkalKjoreInnholdsmonstre(IReadOnlyList<Disposisjonstreff> tittelTreff) =>
        tittelTreff.Count == 0 || tittelTreff.All(t => t.Disposisjonstype == Omorganisering);

    public const int MaksNoderForInnholdsmonstre = 30;

    /// <summary>Innholdsmønstrene over nodenes tekst. Første treff per type vinner (eId og ledd står i raden).</summary>
    public static IReadOnlyList<Disposisjonstreff> KlassifiserInnhold(IEnumerable<(string Eid, string Tekst)> noder)
    {
        var treff = new List<Disposisjonstreff>();
        var regler = new (string Type, string Id, Regex R)[]
        {
            (Oppretting, "opprettelse-av", MOppretting()),
            (Nedleggelse, "nedleggelse-av", MNedleggelse()),
            (Navneendring, "navn-endres-til", MNavneendring()),
            (OverforingRessortfordeling, "forvaltningsansvar-overfores", MOverforing()),
            (Delegering, "delegeres", MDelegering()),
            (Omorganisering, "slas-sammen", MOmorganisering()),
        };
        foreach (var (eid, tekst) in noder)
        {
            if (string.IsNullOrWhiteSpace(tekst)) continue;
            foreach (var (type, id, r) in regler)
            {
                if (treff.Any(x => x.Disposisjonstype == type)) continue;
                if (r.IsMatch(tekst)) treff.Add(new(type, KildeMonster, $"monster:{id}", Avkort(tekst), eid));
            }
        }
        return treff;
    }

    private static string Avkort(string s) => s.Length <= 300 ? s : s[..300] + "…";
}
