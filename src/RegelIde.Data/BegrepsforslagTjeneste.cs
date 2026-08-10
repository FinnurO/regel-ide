using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace RegelIde.Data;

/// <summary>
/// «Identifiser begrep» (byggesteg 5 runde 1, docs/06-veikart.md) — rent rettskilde-drevet, ingen
/// kobling til Tjeneste. Kaller <see cref="IKiAgentKlient"/> med de valgte rettskildenes faktiske
/// tekst som kontekst og oppretter forslag via <see cref="BegrepsregisterTjeneste.OpprettForslagFraKiAsync"/>.
/// </summary>
public sealed class BegrepsforslagTjeneste(RegelIdeDbContext db, IKiAgentKlient kiKlient, BegrepsregisterTjeneste begrepsregister, IConfiguration config)
{
    // Byggesteg 5 runde 3: kun "stub-v1" er faktisk riktig når stubben kjører. Med en ekte
    // IKiAgentKlient bak grensesnittet ville en fast konstant lyve om proveniensen — den vises rått
    // som "KI-versjon" i kø-UI-et.
    private string AiForslagVersjon =>
        config["RegelIde:KiAgent:Leverandor"] == "OpenAiKompatibel"
            ? $"OpenAiKompatibel:{config["RegelIde:KiAgent:Modell"]}"
            : "stub-v1";

    private const string SystemInstruks =
        """
        Du er en juridisk assistent som identifiserer SKOS-begreper i norsk lovtekst.

        Konteksten under er lovtekst der hver paragraf/ledd er merket med en [eId]-tag foran teksten
        (f.eks. "[§1-5] Alkoholholdig drikk...").

        Svar KUN med en ren JSON-array, ingen markdown-kodeblokk (```), ingen forklaringstekst før
        eller etter. Hvert element skal ha NØYAKTIG disse feltene:
        - "Term": begrepet selv, slik det brukes i lovteksten (streng)
        - "Definisjon": en kort definisjon basert på det lovteksten faktisk sier (streng)
        - "Begrepstype": enten "faktabegrep" (beskriver en tilstand/egenskap/objekt) eller
          "handlingsbegrep" (beskriver en handling/prosess) — INGEN andre verdier er tillatt
        - "LovreferanseEid": den eksakte [eId]-taggen (uten hakeparentesene) der begrepet er
          definert/brukt, eller null hvis det ikke er tydelig fra én bestemt paragraf

        Returner en tom array [] hvis du ikke finner noen tydelige begreper. Dikt ikke opp begreper
        som ikke faktisk finnes i teksten, og gjett ikke en LovreferanseEid du er usikker på.
        """;

    private sealed record BegrepForslagJson(string Term, string Definisjon, string Begrepstype, string? LovreferanseEid);

    public async Task<KiForslagResultat<BegrepEntitet>> KjorForslagAsync(
        Guid virksomhetId, IReadOnlyList<Guid> rettskildeIder, string opprettetAv, CancellationToken ct = default)
    {
        var kontekst = await RettskildeKontekstHjelper.ByggKontekstAsync(db, rettskildeIder, ct);
        var svar = await kiKlient.GenererAsync(SystemInstruks, kontekst, ct);

        List<BegrepForslagJson>? forslag;
        try
        {
            forslag = JsonSerializer.Deserialize<List<BegrepForslagJson>>(
                JsonSvarHjelper.StrimleKodeblokk(svar.Innhold), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"KI-klienten returnerte ugyldig JSON for begrepsforslag: {ex.Message}", ex);
        }
        if (forslag is null || forslag.Count == 0)
        {
            // Modellen SVARTE — dette er ikke en feil i rørledningen — men fant ingenting å foreslå i
            // valgt kontekst (observert live: kan også skje som et enkeltstående, ikke-reproduserbart
            // avkortet svar fra modellen selv ved stor kontekst). Meldingen skiller "kjørte, fant
            // ingenting" fra stillhet UI-et ellers ikke kan skille fra en feil.
            return new KiForslagResultat<BegrepEntitet>(
                [], svar.InputTokens, svar.OutputTokens, "KI-agenten svarte, men fant ingen begrep å foreslå i valgt kontekst.");
        }

        var kildeReferanserJson = JsonSerializer.Serialize(new { rettskildeIder });
        var opprettede = new List<BegrepEntitet>();
        foreach (var f in forslag)
        {
            // Ekte modeller ekkoer ikke alltid [eId]-taggen ordrett tilbake (observert live: en full
            // ELI-URL i konteksten kom tilbake som en bar fragment-streng). BegrepsregisterTjeneste
            // validerer strengt og kaster hvis den ikke finnes — riktig når EN bruker skriver inn en
            // referanse manuelt, men her ville det avbrutt HELE batchen og kastet vekk resten av
            // forslagene KI-en fant, bare fordi ett sitat ikke traff. Dropp i stedet den enkelte,
            // uverifiserbare referansen (sett til null) — det er ikke en gjettet fallback-VERDI, det er
            // et bevisst valg om å ikke lagre et sitat vi ikke kan bekrefte peker på en faktisk node.
            var lovreferanseEid = f.LovreferanseEid;
            if (lovreferanseEid is not null && !await db.RettskildeNoder.AnyAsync(n => n.Eid == lovreferanseEid, ct))
            {
                lovreferanseEid = null;
            }

            var begrep = await begrepsregister.OpprettForslagFraKiAsync(
                virksomhetId, f.Term, f.Definisjon, lovreferanseEid, gjelderFor: null, kodelisteReferanseId: null,
                skosUrl: null, f.Begrepstype, opprettetAv, AiForslagVersjon, kildeReferanserJson, ct);
            opprettede.Add(begrep);
        }
        return new KiForslagResultat<BegrepEntitet>(opprettede, svar.InputTokens, svar.OutputTokens, null);
    }
}
