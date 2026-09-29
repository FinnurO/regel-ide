using System.Text.RegularExpressions;

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

    // handlingsforslag-ki-omfang-runden — "soke" er en gyldig HandlingregisterTjeneste.GyldigeHandlingstyper-
    // verdi, slik at stub-svaret faktisk går gjennom Valider() uendret (samme "bevis rørledningen"-rolle
    // som de to andre faste svarene over).
    private const string HandlingSvar =
        """[{"Navn": "Stub-handling (KI-forslag)", "Handlingstype": "soke"}]""";

    private const string FullSvar =
        """[{"Tjeneste": {"Tittel": "Stub-tjeneste (KI-forslag, full)"}, "Handlinger": [{"Navn": "Stub-handling (KI-forslag, full)", "Handlingstype": "soke"}]}]""";

    // [Ny, issue #286] Til forskjell fra de fire faste svarene over MÅ dette svaret faktisk peke på en
    // eId som finnes i konteksten (TjenesteRegelverksreferanseforslagTjeneste dropper stille enhver
    // "Eid" utenfor det gitte kandidatsettet, samme validering som en ekte KI-leverandør ville blitt
    // utsatt for) — et fast, oppdiktet svar ville derfor ALDRI blitt til et forslag, og "bevis
    // rørledningen"-rollen ville falt bort. Løsningen: les FØRSTE "[eId]"-tag i den faktiske
    // konteksten og ekko den tilbake, samme prinsipp som EmbeddedPostgresApiFixture sin
    // AltErInstitusjonHandler ekkoer søketermen tilbake i SNL-stubben.
    private static readonly Regex FoersteEidTag = new(@"\[([^\]]+)\]");

    private static string RegelverksreferanseforslagSvar(string kontekst)
    {
        var treff = FoersteEidTag.Match(kontekst);
        if (!treff.Success) return "[]";
        var eid = treff.Groups[1].Value;
        return $$"""
            [{"Eid": "{{eid}}", "Begrunnelse": "STUB-forslag – ingen ekte KI er koblet til. Peker på første kandidatparagraf i konteksten for å bevise rørledningen."}]
            """;
    }

    public Task<KiSvar> GenererAsync(string systemInstruks, string kontekst, CancellationToken ct = default) =>
        Task.FromResult(new KiSvar(VelgSvar(systemInstruks, kontekst), InputTokens: null, OutputTokens: null));

    // Skiller agent-system-instruksene på tekst den ALLEREDE har, ikke en egen enum/parameter — samme
    // "ett fast, tydelig merket eksempelforslag per agenttype"-rolle klassekommentaren beskriver, nå
    // utvidet til fem agenttyper (issue #286 la til regelverksreferanseforslag-grenen). Rekkefølgen er
    // bevisst: de mer spesifikke frasene sjekkes FØR de mer generiske, men ingen av dem overlapper i
    // praksis.
    private static string VelgSvar(string systemInstruks, string kontekst) => systemInstruks switch
    {
        _ when systemInstruks.Contains("begrep", StringComparison.OrdinalIgnoreCase) => BegrepSvar,
        _ when systemInstruks.Contains("i ÉTT kall") => FullSvar,
        _ when systemInstruks.Contains("EKSISTERENDE tjeneste") => HandlingSvar,
        _ when systemInstruks.Contains("en gitt offentlig tjeneste") => RegelverksreferanseforslagSvar(kontekst),
        _ => TjenesteSvar,
    };
}
