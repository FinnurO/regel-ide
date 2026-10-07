using System.Text.RegularExpressions;

namespace RegelIde.Data.Strukturkonvertering;

/// <summary>
/// [Ny, #308 strukturmodell-KI, 2026-10-07] De lukkede verdilistene i
/// <c>data/fasit/strukturmodell/FORMAT.md</c> som kode: kategorier og typer per kategori, entitetstyper
/// og oppløsningsmåter. Brukes av <see cref="KiStrukturkonverterer"/> både i systeminstruksen (det KI-en
/// får se) og i den harde valideringen av svaret (det som slipper gjennom) — samme liste begge steder,
/// så instruksen og valideringen ikke kan drifte fra hverandre.
/// <para>
/// FORMAT.md er sannheten. <c>StrukturkontraktTests</c> leser «Typer per kategori» i FORMAT.md og
/// feiler hvis lista her og der er ulike — en ny type i FORMAT.md skal ikke stille bli avvist her.
/// </para>
/// <para>
/// <b>Type per kategori, ikke type på tvers [LÅST, #308 «Hard validering»]:</b> en type er gyldig bare
/// for kategorien FORMAT.md lister den under (eller som <c>annet:&lt;x&gt;</c>). <c>del_av</c> står under
/// både <c>relasjon</c> og <c>sammensetning_omrade</c> og er gyldig i begge. Fasiten selv har ett avvik
/// fra dette (1 av 1865: <c>organsammensetning / ledes_av</c>), som ville blitt avvist her — det er
/// en fasitsak for #309, ikke en grunn til å løsne valideringen.
/// </para>
/// </summary>
public static partial class Strukturkontrakt
{
    /// <summary>Kategori → gyldige typer, i FORMAT.md-rekkefølge.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> TyperPerKategori { get; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["relasjon"] =
            [
                "klageinstans_for", "administrativt_underordnet", "instruksjon", "omgjoring", "tilsyn_med_aktor",
                "sekretariat_for", "rapporterer_til", "oppnevner", "velger", "ledes_av", "eies_av", "etterfolger",
                "bistar", "samarbeider_med", "radgir", "del_av", "delegerer_til",
            ],
            ["kompetanse"] =
            [
                "forskriftskompetanse", "vedtakskompetanse", "klagekompetanse", "tilsynskompetanse",
                "delegeringsfullmakt", "oppnevningskompetanse", "instruksjonskompetanse", "utpekingskompetanse",
            ],
            ["medlemskap"] = ["medlem_av", "inngar_i"],
            ["sammensetning_omrade"] = ["bestar_av", "del_av"],
            ["ansvarsomrade"] = ["har_ansvarsomrade", "har_jurisdiksjon", "har_sete_i"],
            ["konstituerende"] = ["oppretter", "avvikler", "skal_finnes"],
            ["organsammensetning"] = ["har_medlemmer", "har_organ"],
        };

    /// <summary>FORMAT.md <c>aktorer.entitetstype</c> (i tillegg til <c>annet:&lt;x&gt;</c>).</summary>
    public static IReadOnlyList<string> Entitetstyper { get; } =
        ["rettssubjekt", "organ", "organisatorisk_enhet", "rolle", "person", "omrade", "klasse"];

    /// <summary>FORMAT.md <c>aktorer.oppløsning</c>.</summary>
    public static IReadOnlyList<string> Opplosninger { get; } =
        ["tekstlig", "lovens_departement", "foregaende_ledd", "omrade", "saksforhold", "forskrift_utenfor", "ukjent"];

    /// <summary>FORMAT.md <c>utsagn.polaritet</c>.</summary>
    public static IReadOnlyList<string> Polariteter { get; } = ["positiv", "negativ"];

    /// <summary>FORMAT.md <c>utsagn.sikkerhet</c>.</summary>
    public static IReadOnlyList<string> Sikkerheter { get; } = ["hoy", "middels", "lav"];

    /// <summary>
    /// <c>annet:&lt;kort_navn&gt;</c>: små bokstaver, sifre og understrek etter kolon. Alle 366
    /// <c>annet:*</c>-verdiene i fasiten følger denne formen (målt 2026-10-07).
    /// </summary>
    [GeneratedRegex(@"^annet:[\p{Ll}\p{Nd}_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AnnetForm();

    /// <summary>Er verdien en velformet <c>annet:&lt;x&gt;</c>?</summary>
    public static bool ErAnnet(string? verdi) => verdi is not null && AnnetForm().IsMatch(verdi);

    /// <summary>Kategorien er en av de sju i FORMAT.md, eller <c>annet:&lt;x&gt;</c>.</summary>
    public static bool ErGyldigKategori(string? kategori) =>
        kategori is not null && (TyperPerKategori.ContainsKey(kategori) || ErAnnet(kategori));

    /// <summary>
    /// Typen står i lista for <paramref name="kategori"/>, eller er <c>annet:&lt;x&gt;</c>. For en
    /// <c>annet:*</c>-kategori finnes ingen liste, så bare <c>annet:&lt;x&gt;</c> er gyldig.
    /// </summary>
    public static bool ErGyldigType(string kategori, string? type) =>
        type is not null
        && (ErAnnet(type) || (TyperPerKategori.TryGetValue(kategori, out var typer) && typer.Contains(type)));

    /// <summary>Entitetstypen er i lista eller <c>annet:&lt;x&gt;</c>. Null er gyldig (ikke avgjort).</summary>
    public static bool ErGyldigEntitetstype(string? verdi) => verdi is null || Entitetstyper.Contains(verdi) || ErAnnet(verdi);
}
