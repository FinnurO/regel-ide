namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #310 «Strukturmodell 5: nodetype-akse», 2026-10-07] ÉN kilde for de lukkede
/// vokabularene nodetype-aksen innfører — speilet av CHECK-constraintene
/// <c>ck_begreper_begrepskategori</c>, <c>ck_navnekandidater_kategori</c> og
/// <c>ck_virksomheter_aktortype</c> i <see cref="RegelIdeDbContext"/>. Endres den ene, må den andre
/// endres i samme migrasjon (samme regel som <see cref="VirksomhetsbegrepTjeneste.Navneformgrunner"/>).
/// <para>
/// <b>Bakgrunn</b> (docs/33 §3 funn 3 og §4.1–4.2, gren <c>strukturmodell-fasit</c>):
/// <c>Begrepskategori = 'gruppe'</c> dekket fire ting med ulik oppførsel — klasse, rolle, område og
/// organ. Johanns spørsmål 2026-10-07 («grupper med en type-attributt, eller de ekte begrepene med
/// gruppefunksjon?») ble besvart med det siste: de ekte begrepene er TYPENE, og gruppefunksjonen
/// (medlemskap/tildeling) er en EVNE alle typene har, ikke en egen type. Selve kanten konsolideres i
/// #311 — her er det bare typeaksen.
/// </para>
/// </summary>
public static class Nodetyper
{
    /// <summary>Utfases (issue #310). Står fortsatt i CHECK-constrainten fordi andre miljøer kan ha
    /// rader som ikke er reklassifisert ennå — se migrasjonen <c>InnforNodetypeakse</c>. Ingen NYE
    /// begrep opprettes med denne verdien; på en NAVNEKANDIDAT betyr den «generisk aktøromtale,
    /// nodetypen er ikke avgjort ennå» (se <see cref="Kandidatkategorier"/>).</summary>
    public const string Gruppe = "gruppe";

    /// <summary>Ekstensjonal (listet med hjemmel, «språkutviklingskommuner») eller intensjonal
    /// (kriterium, «kommunene»). Medlemskap ARVES: det som gjelder klassen, gjelder hvert medlem.</summary>
    public const string Klasse = "klasse";

    /// <summary>Innehas av en aktør, alltid avgrenset til paragraf/ledd («reguleringsmyndighet»,
    /// «departementet»). Innehav arves IKKE (docs/33 §4.2).</summary>
    public const string Rolle = "rolle";

    /// <summary>Territorium («forvaltningsområdet for samiske språk», «Troms»). Erstatter
    /// <c>'administrativ_inndeling'</c> (issue #310: «går inn i omrade»).</summary>
    public const string Omrade = "omrade";

    /// <summary>
    /// Et organ loven omtaler, som ennå IKKE finnes som <see cref="Virksomhet"/>-rad («Kongen i
    /// statsråd», «stortinget»). docs/33 §4.1 sier at organer bor i <see cref="Virksomhet"/> (med
    /// <see cref="Virksomhet.Aktortype"/> = <c>'organ'</c>), men reklassifiseringen i #310 skal ikke
    /// OPPRETTE virksomhetsrader med gjettede data (CLAUDE.md §8) — et organ-begrep er derfor en
    /// mellomtilstand for de radene Johann godkjente som «organ». Bevisst IKKE i
    /// <see cref="Valgbare"/>: et nytt organ går gjennom veiviserens virksomhet-vei.
    /// </summary>
    public const string Organ = "organ";

    /// <summary>Nodetypene et menneske (eller KI-en, som forslag) kan velge for et NYTT begrep fra en
    /// navnekandidat — veiviseren, «Behandle gruppen» og godkjenningsendepunktene.</summary>
    public static readonly string[] Valgbare = [Klasse, Rolle, Omrade];

    /// <summary>Nodetypene et menneske kan SETTE på et eksisterende begrep med gruppefunksjon
    /// (<c>PUT /api/gruppebegrep/{id}/nodetype</c>) — <see cref="Valgbare"/> pluss
    /// <see cref="Organ"/>, slik at gjenværende <c>'gruppe'</c>-rader i andre miljøer kan
    /// reklassifiseres for hånd uten en ny migrasjon.</summary>
    public static readonly string[] Settbare = [Klasse, Rolle, Omrade, Organ];

    /// <summary>
    /// Alle <see cref="BegrepEntitet.Begrepskategori"/>-verdier som bærer GRUPPEFUNKSJONEN — kan være
    /// mål for <see cref="MyndighetstildelingEntitet"/> og begge ender av
    /// <see cref="GruppeMedlemskapEntitet"/>, og deler den faste/lovspesifikke identiteten fra #298.
    /// Inkluderer <see cref="Gruppe"/> slik at ikke-reklassifiserte rader fortsatt virker.
    /// <c>string[]</c>, ikke et sett: EF Core oversetter <c>Contains</c> på en array til
    /// <c>= ANY (...)</c> i SQL.
    /// </summary>
    public static readonly string[] MedGruppefunksjon = [Gruppe, Klasse, Rolle, Omrade, Organ];

    /// <summary>Gyldige <see cref="NavnekandidatEntitet.Kategori"/>-verdier. <see cref="Gruppe"/> =
    /// «generisk aktøromtale, nodetype ikke avgjort» — det deterministiske sveipet kan ikke se forskjell
    /// på klasse/rolle/område uten å gjette (CLAUDE.md §8), så mennesket velger ved godkjenning.</summary>
    public static readonly string[] Kandidatkategorier = ["virksomhet", Gruppe, Klasse, Rolle, Omrade];

    public static bool ErValgbar(string? verdi) => verdi is not null && Valgbare.Contains(verdi);
    public static bool ErSettbar(string? verdi) => verdi is not null && Settbare.Contains(verdi);
    public static bool HarGruppefunksjon(string? begrepskategori) =>
        begrepskategori is not null && MedGruppefunksjon.Contains(begrepskategori);

    /// <summary>Visningsnavn brukt i feilmeldinger — samme ord som UI-et (BegrepskategoriTag.tsx).</summary>
    public static string Visningsnavn(string? begrepskategori) => begrepskategori switch
    {
        Klasse => "klasse",
        Rolle => "rolle",
        Omrade => "område",
        Organ => "organ",
        Gruppe => "gruppe (uavklart type)",
        "virksomhet" => "virksomhet-navneform",
        null => "ordinært begrep",
        _ => begrepskategori,
    };

    // ---------- Aktørtype på Virksomhet (docs/33 §4.1) ----------

    /// <summary>Gyldige <see cref="Virksomhet.Aktortype"/>-verdier. NULL (uavklart) er gyldig og står
    /// bevisst ikke i settet.</summary>
    public static readonly string[] Aktortyper = ["rettssubjekt", "organ", "organisatorisk_enhet"];

    public static bool ErGyldigAktortype(string? verdi) => verdi is null || Aktortyper.Contains(verdi);

    /// <summary>
    /// Den ENESTE automatiske utledningen (issue #310 AC1, samme prinsipp som docs/20 §7.2 for
    /// forvaltningsnivå): Brreg-organisasjonsform <c>KOMM</c>/<c>FYLK</c> → <c>'rettssubjekt'</c>.
    /// Alt annet → NULL, settes av et menneske. Se <see cref="Virksomhet.Aktortype"/> for hvorfor
    /// <paramref name="forvaltningsniva"/> også leses (målt 2026-10-07: 0 rader har
    /// <c>organisasjonsform_kode</c> KOMM/FYLK i lokal base — seeden skriver orgForm inn i
    /// forvaltningsnivået, ikke i organisasjonsform-feltet).
    /// </summary>
    public static string? UtledAktortypeAutomatisk(string? organisasjonsformKode, string? forvaltningsniva) =>
        organisasjonsformKode is "KOMM" or "FYLK" || forvaltningsniva is "kommune" or "fylkeskommune"
            ? "rettssubjekt"
            : null;
}
