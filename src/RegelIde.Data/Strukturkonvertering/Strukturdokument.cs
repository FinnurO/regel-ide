using System.Text.Json.Serialization;

namespace RegelIde.Data.Strukturkonvertering;

// [Ny, #307 strukturmodell-mønster, 2026-10-07] Records som speiler fasit-formatet i
// data/fasit/strukturmodell/FORMAT.md. docs/33 §5.1 gjør det formatet til KONTRAKTEN for automatisk
// konvertering: mønsterlaget (denne saken) og KI-laget (#308) skal begge gi ut nøyaktig samme JSON som
// fasiten, slik at de kan måles mot den uten database og sammenlignes på like vilkår. Feltnavnene er
// derfor fasitens (snake_case, og «oppløsning» med ø) — ikke C#-konvensjon — og settes eksplisitt med
// [JsonPropertyName], så en omdøping av en C#-egenskap aldri stille bryter kontrakten.
//
// Felt som er påkrevd i FORMAT.md (entitetstype, navngitt, sikkerhet …) er likevel nullable her: en
// konverterer som ikke kan avgjøre en verdi fra teksten skal la den være null, ikke velge den mest
// sannsynlige (CLAUDE.md §8). Fasiten fyller dem alltid; mønsterlaget lar de fleste stå tomme.

/// <summary>Ett dokument i fasit-formatet: én hovedrettskilde med ledsagende forskrifter.</summary>
public sealed record Strukturdokument(
    [property: JsonPropertyName("rettskilde")] string Rettskilde,
    [property: JsonPropertyName("eli")] string Eli,
    [property: JsonPropertyName("ledsagende")] IReadOnlyList<StrukturLedsagendeKilde> Ledsagende,
    [property: JsonPropertyName("annotert_av")] string AnnotertAv,
    [property: JsonPropertyName("noder_lest")] int NoderLest,
    [property: JsonPropertyName("aktorer")] IReadOnlyList<StrukturAktor> Aktorer,
    [property: JsonPropertyName("utsagn")] IReadOnlyList<StrukturUtsagn> Utsagn);

/// <summary>En ledsagende rettskilde (typisk forskrift eller delegeringsvedtak) som hører til hovedkilden.</summary>
public sealed record StrukturLedsagendeKilde(
    [property: JsonPropertyName("tittel")] string Tittel,
    [property: JsonPropertyName("eli")] string Eli);

/// <summary>
/// Én DISTINKT omtaleform av en aktør i kilden (ikke én per forekomst), jf. FORMAT.md «aktorer».
/// </summary>
public sealed record StrukturAktor(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("tekstform")] string Tekstform,
    [property: JsonPropertyName("varianter")] IReadOnlyList<string> Varianter,
    [property: JsonPropertyName("eid_eksempler")] IReadOnlyList<string> EidEksempler,
    [property: JsonPropertyName("antall_forekomster")] int AntallForekomster,
    [property: JsonPropertyName("entitetstype")] string? Entitetstype,
    [property: JsonPropertyName("navngitt")] bool? Navngitt,
    [property: JsonPropertyName("referent")] string? Referent,
    [property: JsonPropertyName("oppløsning")] string? Opplosning,
    [property: JsonPropertyName("distributiv")] bool? Distributiv,
    [property: JsonPropertyName("kommentar")] string? Kommentar)
{
    /// <summary>[Ny, issue #312] Samme som <see cref="StrukturUtsagn.VerifisertAv"/>, for aktører fasitrettelsen endret.</summary>
    [JsonPropertyName("verifisert_av")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VerifisertAv { get; init; }
}

/// <summary>
/// Ett strukturelt utsagn, jf. FORMAT.md «utsagn». <see cref="Fra"/>/<see cref="Til"/> er aktør-id-er
/// (<c>a1</c> …) i samme dokument, eller null når teksten ikke avgjør endepunktet.
/// </summary>
public sealed record StrukturUtsagn(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("eid")] string Eid,
    [property: JsonPropertyName("sitat")] string Sitat,
    [property: JsonPropertyName("kategori")] string Kategori,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("fra")] string? Fra,
    [property: JsonPropertyName("til")] string? Til,
    [property: JsonPropertyName("objekt")] string? Objekt,
    [property: JsonPropertyName("polaritet")] string Polaritet,
    [property: JsonPropertyName("avgrensning")] string? Avgrensning,
    [property: JsonPropertyName("betinget")] bool? Betinget,
    [property: JsonPropertyName("kilde_utenfor_korpus")] bool? KildeUtenforKorpus,
    [property: JsonPropertyName("sikkerhet")] string? Sikkerhet,
    [property: JsonPropertyName("kommentar")] string? Kommentar)
{
    /// <summary>
    /// [Ny, #307, 2026-10-07] Proveniens: <c>monster:&lt;id&gt;</c> for mønsterlaget, senere
    /// <c>ki:&lt;modell&gt;</c> (#308). Blir <c>OppdagelsesKilde</c> når resultatet lagres som forslag
    /// (#313, docs/33 §4.3). Fasiten (manuell annotasjon) har ikke feltet, derfor utelates det fra JSON
    /// når det er null — et tillegg til FORMAT.md, ikke en endring av eksisterende felt.
    /// </summary>
    [JsonPropertyName("oppdagelseskilde")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Oppdagelseskilde { get; init; }

    /// <summary>
    /// [Ny, issue #312, 2026-10-08] Hvem som har verifisert/rettet raden i fasiten — første bruk er Johanns systemiske
    /// rettelse av domstollovens inndelingsdel (FORMAT.md). Konverteringen setter det aldri; utelates når null.
    /// </summary>
    [JsonPropertyName("verifisert_av")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VerifisertAv { get; init; }
}

/// <summary>
/// Én node slik den ligger i <c>data/fasit/strukturmodell/noder/&lt;kilde&gt;.json</c> (eksportert fra
/// <c>rettskilde_noder</c>, gjeldende versjon, ikke opphevede noder). Inndata til konverteringen.
/// </summary>
public sealed record Strukturnode(
    [property: JsonPropertyName("rettskilde")] string Rettskilde,
    [property: JsonPropertyName("eli")] string Eli,
    [property: JsonPropertyName("eid")] string Eid,
    [property: JsonPropertyName("nodeType")] string NodeType,
    [property: JsonPropertyName("overskrift")] string? Overskrift,
    [property: JsonPropertyName("tekst")] string? Tekst);

/// <summary>
/// Inndata til <see cref="IStrukturkonverterer"/>: hovedkilden (tittel + ELI) og alle nodene for den og
/// dens ledsagende kilder, i dokumentrekkefølge. Noder med en annen ELI enn hovedkilden blir
/// <see cref="Strukturdokument.Ledsagende"/>.
/// </summary>
public sealed record Strukturkonverteringsgrunnlag(
    string Rettskilde,
    string Eli,
    IReadOnlyList<Strukturnode> Noder);
