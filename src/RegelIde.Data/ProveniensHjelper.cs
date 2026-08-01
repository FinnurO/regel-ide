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
}
