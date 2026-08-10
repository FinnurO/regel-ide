namespace RegelIde.Data;

/// <summary>
/// Abstraksjon over en ekstern KI-tjeneste (byggesteg 5 runde 1, docs/06-veikart.md
/// "Byggesteg 5 — AI-forslag"). Leverandør er UBESTEMT — målet er at et fremtidig leverandørvalg
/// (Anthropic/OpenAI/lokal modell) kun krever en ny klasse bak dette interfacet + en
/// konfigurasjonsendring i Program.cs, ALDRI en endring i agent-logikken
/// (<see cref="BegrepsforslagTjeneste"/>/<see cref="TjenesteforslagTjeneste"/>) som konsumerer det.
/// </summary>
public interface IKiAgentKlient
{
    Task<KiSvar> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default);
}

/// <summary>
/// Svaret fra ett KI-kall (byggesteg 5 runde 3) — selve innholdet pluss leverandørens rapporterte
/// token-forbruk, hvis den rapporterer det (<see cref="KiAgentKlientStub"/> gjør ikke det — den er
/// ikke et ekte kall, derfor <c>null</c>, ikke oppdiktede tall).
/// </summary>
public sealed record KiSvar(string Innhold, int? InputTokens, int? OutputTokens);

/// <summary>
/// STUB (byggesteg 5 runde 1) — returnerer ett fast, tydelig merket eksempelforslag per agenttype.
/// Ingen ekte resonnering over <paramref name="kontekst"/> i <see cref="GenererAsync"/> — finnes kun
/// for å bevise rørledningen (kø-oppføring vises, godkjenn/avvis/proveniens virker) uten å late som
/// KI. Skal erstattes av en ekte leverandør-implementasjon bak <see cref="IKiAgentKlient"/> i en
/// senere runde, se doc-comment der.
/// </summary>
public sealed class KiAgentKlientStub : IKiAgentKlient
{
    private const string BegrepSvar =
        """[{"Term": "Uklanderlig vandel (stub)", "Definisjon": "STUB-forslag – ingen ekte KI er koblet til. Eksempeltekst generert for å bevise rørledningen.", "Begrepstype": "faktabegrep"}]""";

    private const string TjenesteSvar =
        """[{"Tittel": "Stub-tjeneste (KI-forslag)", "KortBeskrivelse": "STUB-forslag – ingen ekte KI er koblet til. Eksempeltekst generert for å bevise rørledningen."}]""";

    public Task<KiSvar> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default) =>
        Task.FromResult(new KiSvar(
            systemInstruks.Contains("begrep", StringComparison.OrdinalIgnoreCase) ? BegrepSvar : TjenesteSvar,
            InputTokens: null, OutputTokens: null));
}
