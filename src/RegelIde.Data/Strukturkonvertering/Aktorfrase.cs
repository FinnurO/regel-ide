using System.Text.RegularExpressions;

namespace RegelIde.Data.Strukturkonvertering;

/// <summary>
/// [Ny, #307 strukturmodell-mønster, 2026-10-07] Avgjør om et tekstutsnitt som et mønster har fanget i
/// subjekt- eller objektposisjon er en aktørfrase, og deler sideordnede aktører («Reguleringsmyndigheten
/// og klagenemnda») i hver sin.
/// <para>
/// Aktører identifiseres i denne saken KUN ved tekstform (#307): ingen oppslag mot katalogen
/// (#313/#314), ingen fuzzy-treff, ingen normalisering ut over det teksten selv viser (CLAUDE.md §8).
/// Det eneste som fjernes er ord som ikke er del av navnet: en ledende «[» (Lovdata-markering av tekst
/// som ikke er i kraft), etterstilt «selv» («Kommunestyret selv» = ikke-delegerbar kompetanse, men
/// aktøren er kommunestyret) og avsluttende tegnsetting.
/// </para>
/// <para>
/// Avvisningen er bevisst grov og konservativ: kan frasen ikke være en aktør (pronomen, preposisjon
/// først, tall/paragraftegn, et verb inni), returneres ingenting, og mønsteret lar endepunktet stå
/// null fremfor å gjette. Det koster gjenfinning, ikke presisjon — det er den riktige veien å feile.
/// </para>
/// </summary>
public static class Aktorfrase
{
    /// <summary>Lengste frase som godtas (ord). «Det regionale helseforetaket i helseregionen» = 5.</summary>
    private const int MaksOrd = 6;

    // Ord som aldri forekommer inne i et aktørnavn i de fem fasitkildene, men som ofte står mellom et
    // subjekt og verbet når et mønster har grepet for mye (bisetning, adverbial). «i», «for», «og» og
    // «eller» er bevisst IKKE her: «Kongen i statsråd», «Direktøren for domstoladministrasjonen»,
    // «Norges vassdrags- og energidirektorat».
    private static readonly HashSet<string> ForbudteOrd = new(StringComparer.OrdinalIgnoreCase)
    {
        "som", "kan", "skal", "må", "bør", "er", "ikke", "har", "blir", "ble", "var", "etter", "ved",
        "når", "dersom", "hvis", "om", "at", "med", "til", "fra", "av", "på", "under", "uten", "der",
        "her", "slik", "også", "likevel", "bare", "kun", "enten", "både", "gjennom", "mot", "innen",
        "hvor", "hva", "hvem", "hvilke", "hvilken", "jf", "nr", "loven", "lov", "forskrift", "forskriften",
        "paragraf", "ledd", "bokstav", "punktum", "vedtak", "enkeltvedtak", "å",
    };

    // Ord som ikke kan være en aktør ALENE (pronomen, determinativ). Som første ord i en lengre frase er
    // «Det»/«Den»/«De» en artikkel («Det regionale helseforetaket», «Den kliniske etikkomiteen») og godtas.
    private static readonly HashSet<string> IkkeAlene = new(StringComparer.OrdinalIgnoreCase)
    {
        "det", "den", "de", "dette", "denne", "disse", "han", "hun", "ingen", "enhver", "alle", "noen",
        "man", "en", "et", "ei", "andre", "annen", "annet", "hver", "hvert", "begge", "selv", "dem",
        "deres", "sin", "sitt", "sine", "seg",
        // Tallord: «avgjøres av fem dommere» — tallet er ikke aktøren.
        "to", "tre", "fire", "fem", "seks", "sju", "syv", "åtte", "ni", "ti", "elleve", "tolv",
    };

    // Ord som ikke kan stå først i en aktørfrase (preposisjon/konjunksjon/adverb).
    private static readonly HashSet<string> IkkeForst = new(StringComparer.OrdinalIgnoreCase)
    {
        "i", "for", "og", "eller", "men", "så", "da", "nå", "dessuten", "videre", "dette", "denne",
        "disse", "slike", "slikt", "ethvert", "enhver",
    };

    // «og»/«eller» deler sideordnede aktører — men ikke når venstre side ender på bindestrek
    // («Kommunal- og regionaldepartementet», «Norges vassdrags- og energidirektorat»).
    private static readonly Regex Sideordning = new(@"(?<!-)\s+(?:og|eller)\s+", RegexOptions.CultureInvariant);

    /// <summary>
    /// Tolker <paramref name="raa"/> som én eller flere sideordnede aktørfraser. Returnerer en tom liste
    /// når frasen ikke kan være en aktør — aldri en «nærmeste» tolkning.
    /// </summary>
    public static IReadOnlyList<string> Tolk(string? raa)
    {
        var frase = Rens(raa);
        if (frase is null) return [];

        var deler = Sideordning.Split(frase).Select(Rens).ToList();
        if (deler.Count > 1 && deler.All(d => d is not null && ErGyldig(d)))
        {
            return deler.Select(d => d!).ToList();
        }

        // Kan ikke deles i gyldige deler: hele frasen som ett navn, hvis den holder alene
        // («Sogn og Fjordane tingrett» er ett navn; «Statsforvalteren og» er det ikke).
        return ErGyldig(frase) ? [frase] : [];
    }

    /// <summary>
    /// Er <paramref name="frase"/> et navn på et område/en enhet slik det står i en inndelingsliste
    /// («Vestre Finnmark tingrett», «Østre valgkrets/Nuortaguovllu válgabiire», «Hålogaland lagdømme»)?
    /// Strengere på form enn <see cref="Tolk"/> (stor forbokstav påkrevd), men uten deling på «og» —
    /// i inndelingslister er «Sogn og Fjordane» og «Møre og Romsdal» ett navn.
    /// </summary>
    public static bool ErNavn(string frase)
    {
        var ord = frase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return ord.Length is > 0 and <= 12
            && char.IsUpper(ord[0][0])
            && !frase.Any(char.IsDigit)
            && !frase.Contains('§')
            && !ord.Any(o => ForbudteOrd.Contains(o.Trim(',', '.')));
    }

    /// <summary>Fjerner genitiv-s fra et enkeltord («Kongens» → «Kongen»), eller null om ordet ikke ender på s.</summary>
    public static string? UtenGenitiv(string ord)
    {
        ord = ord.Trim();
        return ord.Length > 2 && ord.EndsWith('s') ? ord[..^1] : null;
    }

    private static string? Rens(string? raa)
    {
        if (string.IsNullOrWhiteSpace(raa)) return null;
        var frase = Regex.Replace(raa.Trim(), @"\s+", " ");
        frase = frase.TrimStart('[', '(', '«', ' ').TrimEnd('.', ',', ';', ':', ')', '»', ']', ' ');
        if (frase.EndsWith(" selv", StringComparison.Ordinal)) frase = frase[..^5];
        return frase.Length == 0 ? null : frase;
    }

    private static bool ErGyldig(string frase)
    {
        var ord = frase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (ord.Length is 0 or > MaksOrd) return false;
        if (frase.Any(char.IsDigit) || frase.Contains('§')) return false;
        if (!char.IsLetter(ord[0][0])) return false;
        if (IkkeForst.Contains(ord[0])) return false;
        if (ord.Length == 1 && IkkeAlene.Contains(ord[0])) return false;
        if (ord.Any(o => ForbudteOrd.Contains(o.Trim(',', '.')))) return false;
        var siste = ord[^1];
        if (siste is "og" or "eller" or "i" or "for" or "det" or "den" or "de") return false;
        // Et siste ord på bindestrek er et avkortet sammensatt ord uten etterledd — ikke et helt navn.
        return !siste.EndsWith('-');
    }
}
