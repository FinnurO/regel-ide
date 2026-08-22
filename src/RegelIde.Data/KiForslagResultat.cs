namespace RegelIde.Data;

/// <summary>
/// Resultatet av ett "Identifiser X"-agentkjør (byggesteg 5 runde 3) — forslagene som faktisk ble
/// opprettet, KI-klientens rapporterte token-forbruk (se <see cref="KiSvar"/>), og en eksplisitt
/// <see cref="Melding"/> når agenten svarte men fant null forslag i valgt kontekst. Uten denne
/// meldingen skiller ingenting "kjøringen fullførte, KI fant ingenting" fra stillhet som lett
/// mistolkes som at noe gikk feil (observert live, byggesteg 5 runde 3).
/// </summary>
public sealed record KiForslagResultat<T>(IReadOnlyList<T> Opprettede, int? InputTokens, int? OutputTokens, string? Melding);

/// <summary>
/// Ett element i resultatet av <see cref="TjenesteforslagTjeneste.KjorFullForslagAsync"/> (omfang
/// "full", handlingsforslag-ki-omfang-runden) — den nyopprettede Tjenesten pluss Handlingene KI-en
/// foreslo UNDER den i SAMME kall. Egen record (ikke et løst tuppel) siden formen krysser API-grensen
/// til <c>RegelIde.Api</c> sin DTO-mapping, samme rolle som <see cref="KiForslagResultat{T}"/> selv.
/// </summary>
public sealed record TjenesteMedHandlingerResultat(TjenesteEntitet Tjeneste, IReadOnlyList<HandlingEntitet> Handlinger);
