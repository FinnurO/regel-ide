namespace RegelIde.Data;

/// <summary>
/// Delt hjelper for å opprette en <see cref="ProveniensEntitet"/>-rad (§1.14 i domenemodellen).
/// Trukket ut 2026-07-26 fra <see cref="RettskildeImportTjeneste"/> (der den lå som en privat
/// <c>NyProveniensrad</c>-metode) idet <see cref="HandbokForfatterTjeneste"/> begynte å trenge samme
/// mønster — jf. docs/08-byggesteg1-teknisk-design.md §2.2: "NyProveniensrad-mønsteret ... bør trekkes
/// ut til en delt intern hjelper når to tjenester begynner å bruke det".
/// </summary>
internal static class ProveniensHjelper
{
    public static ProveniensEntitet NyRad(
        string entitetType, Guid entitetId, Guid? virksomhetId, string handling, string endretAv) => new()
    {
        Id = Guid.NewGuid(),
        VirksomhetId = virksomhetId,
        EntitetType = entitetType,
        EntitetId = entitetId,
        EndretAv = endretAv,
        Dato = DateTimeOffset.UtcNow,
        Handling = handling,
    };

    /// <summary>
    /// AI-forslag-variant (byggesteg 5 runde 1) — setter Handling="foreslatt_av_ai" og fyller
    /// AiForslagVersjon/KildeReferanserJson, som <see cref="NyRad"/> aldri gjør. Additiv: eksisterende
    /// kallere av NyRad er upåvirket.
    /// </summary>
    public static ProveniensEntitet NyForslagRad(
        string entitetType, Guid entitetId, Guid? virksomhetId, string endretAv, string aiForslagVersjon,
        string? kildeReferanserJson = null) => new()
    {
        Id = Guid.NewGuid(),
        VirksomhetId = virksomhetId,
        EntitetType = entitetType,
        EntitetId = entitetId,
        EndretAv = endretAv,
        Dato = DateTimeOffset.UtcNow,
        Handling = "foreslatt_av_ai",
        AiForslagVersjon = aiForslagVersjon,
        KildeReferanserJson = kildeReferanserJson,
    };

    /// <summary>
    /// Tverr-virksomhet-forslag-variant (2026-08-28, import-wizard-runden) — samme idé som
    /// <see cref="NyForslagRad"/>, men kilden er en ANNEN virksomhets import, ikke KI.
    /// <paramref name="malVirksomhetId"/> er raden sin egen/eier-virksomhet (samme rolle som
    /// <paramref name="virksomhetId"/> i <see cref="NyRad"/>); <paramref name="forslagFraVirksomhetId"/>
    /// er virksomheten som faktisk kjørte importen.
    /// </summary>
    public static ProveniensEntitet NyTverrVirksomhetForslagRad(
        string entitetType, Guid entitetId, Guid malVirksomhetId, string endretAv, Guid forslagFraVirksomhetId) => new()
    {
        Id = Guid.NewGuid(),
        VirksomhetId = malVirksomhetId,
        EntitetType = entitetType,
        EntitetId = entitetId,
        EndretAv = endretAv,
        Dato = DateTimeOffset.UtcNow,
        Handling = "foreslatt_av_annen_virksomhet",
        ForeslattAvVirksomhetId = forslagFraVirksomhetId,
    };
}
