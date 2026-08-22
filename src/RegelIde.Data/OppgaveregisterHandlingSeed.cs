using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using RegelIde.Kildekonvertering;

namespace RegelIde.Data;

/// <summary>
/// Kobler <see cref="OppgaveregisterHenter"/>s rått høstede skjemaer (<see cref="EksternKildeEntitet"/>,
/// <see cref="OppgaveregisterHenter.Kildetype"/>) inn i domenemodellen som ekte <see cref="HandlingEntitet"/>-
/// rader — det steget <see cref="OppgaveregisterHenter"/>s egen klassekommentar eksplisitt sier
/// IKKE er gjort ("ren råtekst-høsting, intet mer"). Leser fra <see cref="EksternKildeEntitet"/>,
/// SKRIVER ALDRI til den (dette er en ren nedstrøms-forbruker av høstelaget, samme todeling som
/// <see cref="TjenesteforslagTjeneste"/> mot KI-forslag).
///
/// <para>
/// **Faktisk JSON-form** (empirisk verifisert mot live <c>data.brreg.no/oppgaveregisteret/api/skjema/alle.json</c>,
/// 2026-08-22, 903 skjemaer): hvert skjema har <c>navn</c>, <c>guid</c>, <c>eier.organisasjonsnummer</c>
/// (tall, alltid 9 sifre, ALLTID til stede i alle 903 rader) + <c>eier.etatsnavn</c>,
/// <c>bruksomraader[].navn</c> (nøyaktig 3 kjente verdier — se <see cref="BruksomraadeKode"/>, 876/903
/// har akkurat én, 27 har to), <c>lovhjemler[]</c> (1067 rader totalt, hver med <c>dato</c> i Lovdatas
/// eget "LOV-ÅÅÅÅ-MM-DD-NN"-datokodeformat — SAMME format <see cref="LovdataIdentifikatorer.AvledEliFraDatokode"/>
/// allerede konsumerer for <see cref="RettskildeEntitet.Eli"/> — og et fritekst <c>henvisning</c>-felt
/// ("§ 42", "§§ 21-4, 22-3, ...", "Kapittel 5", <c>null</c>, osv., se punkt (c) under), og nøstede
/// <c>forskrifter[]</c> med samme <c>dato</c>-form. <c>formaal.fritekst</c> gir en kort forklarende tekst.
/// </para>
///
/// <para>
/// **(a) Virksomhet-matching** — EKSAKT på <see cref="Virksomhet.Organisasjonsnummer"/> (9-sifret,
/// nullpadded), INGEN fuzzy navnematch (etatsnavn er versaler/forkortet — "ARBEIDS- OG VELFERDSETATEN"
/// vs. et katalogoppført "NAV" ville krevd et gjettet aliasoppslag). Ingen treff ⇒ skjemaet HOPPES OVER
/// i sin helhet (telles i <see cref="OppgaveregisterHandlingSeedResultat.HoppetOverUsikkerVirksomhet"/>) —
/// uten en kjent eier kan vi ikke avgjøre hvilken Tjeneste/virksomhet handlingen skal høre til (se punkt (b)),
/// og en gjettet plassering ville vært verre enn ingen plassering.
/// </para>
///
/// <para>
/// **(b) Tjeneste-design, EKSPLISITT valg** — Oppgaveregisteret gir INGEN "hvilken tjeneste"-gruppering
/// (det er en flat skjemaliste, ikke en tjenestekatalog). Valgt design: ÉN samlende
/// <see cref="TjenesteEntitet"/> PER eiende virksomhet ("Innsendte skjemaer — {virksomhet}"),
/// find-or-create på (VirksomhetId, Tittel) — ALLE denne virksomhetens skjemaer blir Handlinger under
/// samme rad. Alternativet (en egen Tjeneste per skjema) ble vurdert og forkastet: et Oppgaveregister-
/// skjema er ofte KUN én innsendingskanal for en rettighet som allerede er/bør være modellert som en
/// egen Tjeneste (søknad+klage+kontroll hører sammen) — å opprette 900 løsrevne, ekvivalente
/// enkelt-handling-tjenester ville dupliert nettopp den strukturen Rettighet/Handling-modellen (docs/17/18)
/// ble bygget for å unngå. Denne samletjenesten er derfor en EKSPLISITT, GROV plassholder ("utkast",
/// ingen Type/Formal — skjemaene under den spenner ulike rettighetstyper) — ikke ment som et ferdig,
/// redigert Tjeneste-kort, kun et sted Handlingene har en gyldig hjemplass til en fagperson vurderer
/// hver av dem inn i sin egentlige Tjeneste.
/// </para>
///
/// <para>
/// **(c) Rettskilde-matching, KUN dokumentnivå** — <c>lovhjemler[].henvisning</c> (og forskriftenes
/// tilsvarende felt) er ustrukturert fritekst i vidt varierende form (enkeltparagraf, kommaseparerte
/// lister, spenn, "jfr."-kryssreferanser, romertall/kapittelbetegnelser, eller <c>null</c>) — å tolke
/// dette til en spesifikk <see cref="RettskildeNodeEntitet.Eid"/> uten å gjette er IKKE gjort (samme
/// "ingen gjettet fallback"-prinsipp som <see cref="LovdataUrlTolker"/>s dokumenterte begrensning).
/// I stedet matches KUN på selve loven/forskriften: <c>dato</c> → <see cref="LovdataIdentifikatorer.AvledEliFraDatokode"/>
/// → eksakt streng-match mot en EKSISTERENDE, gjeldende <see cref="RettskildeEntitet.Eli"/>. Ingen treff
/// (loven/forskriften er ikke importert i DENNE kjørende instansen) ⇒ hoppes over stille, telles i
/// <see cref="OppgaveregisterHandlingSeedResultat.RettskildematcherIkkeFunnet"/> — IKKE en feil, kun et
/// mål på hvor mye av Lovdata-korpuset som faktisk er importert akkurat nå (på-forespørsel, se
/// <c>LovdataKatalogTjeneste</c>). Reelle koblinger lagres som <see cref="HandlingRegelverksreferanseEntitet"/>
/// (samme rolle for Handling som <see cref="TjenesteRegelverksreferanseEntitet"/> har for Tjeneste),
/// <see cref="HandlingRegelverksreferanseEntitet.TilEid"/> = rettskildens egen Eli (dokumentnivå, se over).
/// </para>
///
/// <para>
/// **(d) Bruksomraade → Handlingstype, dokumentert forenkling** — de tre kjente <c>bruksomraader[].navn</c>-
/// verdiene ("Periodisk rapportering"/"Hendelsesrapportering"/"Søknad / registrering") mappes DETERMINISTISK
/// til <see cref="HandlingregisterTjeneste.GyldigeHandlingstyper"/>: de to rapporteringskategoriene til
/// "rapportere" (uambiguøst), "Søknad / registrering" til "registrere" — en eksplisitt, dokumentert
/// forenkling (kategorien selv slår sammen to konsepter GyldigeHandlingstyper skiller mellom; Oppgaveregisteret
/// gir ingen måte å avgjøre hvilket for et gitt skjema UTEN å gjette, så "registrere" er valgt som den
/// mer generiske av de to, IKKE et forsøk på å gjette riktig per skjema). De 27 skjemaene med to
/// bruksomraader bruker kun det FØRSTE (samme "ingen kunstig presisjon"-begrunnelse — <see cref="HandlingEntitet.Bruksomraade"/>
/// er ett enkelt felt, ikke en liste).
/// </para>
///
/// <para>
/// **Idempotens** — matcher på <see cref="HandlingEntitet.EksternKildeId"/> (IKKE navn — et skjemanavn
/// kan endre seg mellom to høstinger av samme <see cref="EksternKildeEntitet.EksternId"/>), samme
/// "stabil ekstern nøkkel, ikke innhold" som høstelaget selv ett nivå ned. Re-kjøring med uendrede
/// kildedata er en no-op (ingen SaveChanges-skriving utover det som faktisk endret seg). Trigges på
/// forespørsel (<c>POST /api/eksterne-kilder/oppgaveregister/koble-til-handlinger</c>), IKKE ved
/// oppstart — samme begrunnelse som <see cref="OppgaveregisterHenter"/> selv, og forutsetter at denne
/// allerede har kjørt (leser <see cref="EksternKildeEntitet"/>, henter ALDRI selv over nett).
/// </para>
/// </summary>
public static class OppgaveregisterHandlingSeed
{
    private static readonly JsonSerializerOptions JsonInnstillinger = new() { PropertyNameCaseInsensitive = true };

    private const string OpprettetAv = "oppgaveregister-import";

    /// <summary>Kun feltene denne koblingen faktisk bruker — resten av skjemaobjektet er ALLEREDE
    /// bevart verbatim i <see cref="EksternKildeEntitet.RaaJson"/>, se <see cref="OppgaveregisterHenter"/>.</summary>
    private sealed record SkjemaJson(
        [property: JsonPropertyName("guid")] string? Guid,
        [property: JsonPropertyName("navn")] string? Navn,
        [property: JsonPropertyName("eier")] SkjemaEierJson? Eier,
        [property: JsonPropertyName("formaal")] SkjemaFormaalJson? Formaal,
        [property: JsonPropertyName("bruksomraader")] List<SkjemaBruksomraadeJson>? Bruksomraader,
        [property: JsonPropertyName("lovhjemler")] List<SkjemaLovhjemmelJson>? Lovhjemler);

    private sealed record SkjemaEierJson(
        [property: JsonPropertyName("organisasjonsnummer")] long? Organisasjonsnummer,
        [property: JsonPropertyName("etatsnavn")] string? Etatsnavn);

    private sealed record SkjemaFormaalJson([property: JsonPropertyName("fritekst")] string? Fritekst);

    private sealed record SkjemaBruksomraadeJson([property: JsonPropertyName("navn")] string? Navn);

    private sealed record SkjemaLovhjemmelJson(
        [property: JsonPropertyName("dato")] string? Dato,
        [property: JsonPropertyName("henvisning")] string? Henvisning,
        [property: JsonPropertyName("forskrifter")] List<SkjemaForskriftJson>? Forskrifter);

    private sealed record SkjemaForskriftJson([property: JsonPropertyName("dato")] string? Dato);

    /// <summary>De tre eneste kjente <c>bruksomraader[].navn</c>-verdiene, se klassekommentaren punkt (d).
    /// Ukjent verdi ⇒ <c>null</c> — ingen gjettet fallback.</summary>
    private static string? BruksomraadeKode(string navn) => navn switch
    {
        "Periodisk rapportering" => "periodisk_rapportering",
        "Hendelsesrapportering" => "hendelsesrapportering",
        "Søknad / registrering" => "soknad_registrering",
        _ => null,
    };

    /// <summary>Deterministisk, dokumentert forenkling — se klassekommentaren punkt (d).</summary>
    private static string HandlingstypeForBruksomraade(string? bruksomraadeKode) => bruksomraadeKode switch
    {
        "periodisk_rapportering" or "hendelsesrapportering" => "rapportere",
        "soknad_registrering" => "registrere",
        _ => "annet",
    };

    // 2026-08-22, Johanns tilbakemelding: "Oppgaveregisteret — X" gjør det tydeligere at dette er en
    // AUTOMATISK KILDE-plassholder (ikke en fagperson-redigert tjeneste), samme "kilde i navnet"-
    // signal som f.eks. Lovdata-importerte rettskilder allerede gir gjennom sin egen Kildetype/Eli.
    private const string AggregertTjenesteTittelPrefiks = "Oppgaveregisteret — ";

    public static async Task<OppgaveregisterHandlingSeedResultat> SeedAsync(RegelIdeDbContext db, CancellationToken ct = default)
    {
        var kildeRader = await db.EksterneKilder
            .Where(k => k.Kildetype == OppgaveregisterHenter.Kildetype)
            .ToListAsync(ct);

        var tjenesteregister = new TjenesteregisterTjeneste(db);
        var handlingregister = new HandlingregisterTjeneste(db);

        // Kun de virksomhetene/rettskildene som faktisk finnes NÅ — se klassekommentaren punkt (a)/(c)
        // for hvorfor kun eksakt match er lov. Lest ÉN gang, ikke ett spørring per skjema (900 skjemaer).
        var virksomheterPerOrgnr = await db.Virksomheter
            .Where(v => v.Organisasjonsnummer != null)
            .ToDictionaryAsync(v => v.Organisasjonsnummer!, v => v.Id, StringComparer.Ordinal, ct);
        var rettskilderPerEli = await db.Rettskilder
            .Where(r => r.Eli != null && r.Entitetsstatus == "gjeldende")
            .ToDictionaryAsync(r => r.Eli!, r => r.Id, StringComparer.Ordinal, ct);

        // Aggregert Tjeneste-cache per virksomhet — se klassekommentaren punkt (b). Forhåndslest for å
        // unngå én "finnes den allerede"-spørring per skjema.
        var aggregertTjenestePerVirksomhet = await db.Tjenester
            .Where(t => t.Entitetsstatus == "gjeldende" && t.Tittel.StartsWith(AggregertTjenesteTittelPrefiks))
            .ToDictionaryAsync(t => t.VirksomhetId, t => t.Id, ct);

        // Eksisterende handlinger fra en tidligere kjøring av DENNE seeden — se "Idempotens" over.
        var eksisterendeHandlinger = await db.Handlinger
            .Where(h => h.EksternKildeId != null && h.Entitetsstatus == "gjeldende")
            .ToDictionaryAsync(h => h.EksternKildeId!.Value, ct);
        var eksisterendeRegelverksreferanser = await db.HandlingRegelverksreferanser
            .Select(r => new { r.HandlingId, r.TilRettskildeId, r.TilEid })
            .ToListAsync(ct);
        var regelverksreferanseNokler = new HashSet<(Guid HandlingId, Guid TilRettskildeId, string TilEid)>(
            eksisterendeRegelverksreferanser.Select(r => (r.HandlingId, r.TilRettskildeId, r.TilEid)));

        var nyeHandlinger = 0;
        var oppdaterteHandlinger = 0;
        var uendretHandlinger = 0;
        var hoppetOverUsikkerVirksomhet = 0;
        var nyeTjenester = 0;
        var lovhjemlerTotalt = 0;
        var rettskildematcherFunnet = 0;
        var rettskildematcherIkkeFunnet = 0;

        foreach (var kilderad in kildeRader)
        {
            ct.ThrowIfCancellationRequested();

            var skjema = JsonSerializer.Deserialize<SkjemaJson>(kilderad.RaaJson, JsonInnstillinger);
            if (skjema is null || string.IsNullOrWhiteSpace(skjema.Navn)) continue; // ingen gjettet fallback.

            var orgnr = skjema.Eier?.Organisasjonsnummer?.ToString("D9");
            if (orgnr is null || !virksomheterPerOrgnr.TryGetValue(orgnr, out var virksomhetId))
            {
                hoppetOverUsikkerVirksomhet++;
                continue; // se klassekommentaren punkt (a) — ingen kjent eier, ingen plassering.
            }

            if (!aggregertTjenestePerVirksomhet.TryGetValue(virksomhetId, out var tjenesteId))
            {
                var virksomhetNavn = await db.Virksomheter.Where(v => v.Id == virksomhetId).Select(v => v.Navn).SingleAsync(ct);
                var tjeneste = await tjenesteregister.OpprettAsync(
                    virksomhetId, AggregertTjenesteTittelPrefiks + virksomhetNavn,
                    beskrivelse: "Samleside for skjemaer virksomheten er registrert som eier av i Oppgaveregisteret " +
                        "(Brønnøysundregistrene) — automatisk seedet, grov plassholder. Hver handling under bør " +
                        "etter hvert flyttes til sin egentlige Tjeneste når en fagperson har vurdert den (se " +
                        "OppgaveregisterHandlingSeed sin klassekommentar punkt (b)).",
                    kompetentMyndighet: null, output: null, tjenestetype: null, malgruppe: null, kanaler: null,
                    kostnad: null, behandlingstid: null, kontaktpunkt: null, konsekvensVedBrudd: null, sprak: null,
                    OpprettetAv, ct);
                tjenesteId = tjeneste.Id;
                aggregertTjenestePerVirksomhet[virksomhetId] = tjenesteId;
                nyeTjenester++;
            }

            var bruksomraadeKode = skjema.Bruksomraader is [{ Navn: { } forsteNavn }, ..] ? BruksomraadeKode(forsteNavn) : null;
            var handlingstype = HandlingstypeForBruksomraade(bruksomraadeKode);
            var merknad = string.IsNullOrWhiteSpace(skjema.Formaal?.Fritekst) ? null : skjema.Formaal.Fritekst.Trim();

            HandlingEntitet handling;
            if (eksisterendeHandlinger.TryGetValue(kilderad.Id, out var eksisterende))
            {
                var endret = eksisterende.Navn != skjema.Navn || eksisterende.Handlingstype != handlingstype ||
                    eksisterende.Bruksomraade != bruksomraadeKode || eksisterende.TjenesteId != tjenesteId ||
                    eksisterende.Merknad != merknad;
                if (endret)
                {
                    eksisterende.Navn = skjema.Navn;
                    eksisterende.Handlingstype = handlingstype;
                    eksisterende.Bruksomraade = bruksomraadeKode;
                    eksisterende.TjenesteId = tjenesteId;
                    eksisterende.Merknad = merknad;
                    eksisterende.SistEndretAv = OpprettetAv;
                    eksisterende.SistEndretTidspunkt = DateTimeOffset.UtcNow;
                    eksisterende.Versjon++;
                    oppdaterteHandlinger++;
                }
                else
                {
                    uendretHandlinger++;
                }
                handling = eksisterende;
            }
            else
            {
                handling = await handlingregister.OpprettAsync(
                    virksomhetId, tjenesteId, skjema.Navn, handlingstype, bruksomraadeKode, utfortAv: "soker",
                    kanaler: null, behandlingstid: null, kostnad: null, vedlegg: null, veiledningstekst: null,
                    arsaker: null, resultat: null, merknad, OpprettetAv, ct);
                handling.EksternKildeId = kilderad.Id;
                eksisterendeHandlinger[kilderad.Id] = handling;
                nyeHandlinger++;
            }

            // Se klassekommentaren punkt (c) — kun dokumentnivå, kun eksakt Eli-match.
            foreach (var eli in AlleDokumentEli(skjema.Lovhjemler))
            {
                lovhjemlerTotalt++;
                if (!rettskilderPerEli.TryGetValue(eli, out var rettskildeId))
                {
                    rettskildematcherIkkeFunnet++;
                    continue;
                }
                rettskildematcherFunnet++;

                var nokkel = (handling.Id, rettskildeId, eli);
                if (!regelverksreferanseNokler.Add(nokkel)) continue; // allerede koblet i en tidligere kjøring.

                db.HandlingRegelverksreferanser.Add(new HandlingRegelverksreferanseEntitet
                {
                    Id = Guid.NewGuid(), HandlingId = handling.Id, TilRettskildeId = rettskildeId, TilEid = eli,
                });
            }
        }

        await db.SaveChangesAsync(ct);

        return new OppgaveregisterHandlingSeedResultat(
            kildeRader.Count, nyeHandlinger, oppdaterteHandlinger, uendretHandlinger, hoppetOverUsikkerVirksomhet,
            nyeTjenester, lovhjemlerTotalt, rettskildematcherFunnet, rettskildematcherIkkeFunnet);
    }

    /// <summary>Alle datokoder som kan representere et EKTE dokumentnivå-Eli-kandidat for dette skjemaet
    /// — selve lovhjemmelen OG dens nøstede forskrifter (begge bruker samme LOV|FOR-datokodeform, se
    /// <see cref="LovdataIdentifikatorer.AvledEliFraDatokode"/>). Ugyldig/uventet datokodeform kastes IKKE
    /// videre her (skulle i teorien aldri skje, se klassens JSON-form-verifisering) — behandles som "ingen
    /// kandidat" i stedet for å velte hele kjøringen, samme forsvarslinje som resten av høstelaget.</summary>
    private static IEnumerable<string> AlleDokumentEli(List<SkjemaLovhjemmelJson>? lovhjemler)
    {
        foreach (var lovhjemmel in lovhjemler ?? [])
        {
            if (TryAvledEli(lovhjemmel.Dato, out var eli)) yield return eli;
            foreach (var forskrift in lovhjemmel.Forskrifter ?? [])
            {
                if (TryAvledEli(forskrift.Dato, out var forskriftEli)) yield return forskriftEli;
            }
        }
    }

    private static bool TryAvledEli(string? datokode, out string eli)
    {
        eli = "";
        if (string.IsNullOrWhiteSpace(datokode)) return false;
        try
        {
            eli = LovdataIdentifikatorer.AvledEliFraDatokode(datokode, out _);
            return true;
        }
        catch (FormatException)
        {
            return false; // uventet datokodeform — ingen gjettet fallback, se klassekommentaren.
        }
    }
}

/// <summary>Sammendrag av én <see cref="OppgaveregisterHandlingSeed.SeedAsync"/>-kjøring — se
/// klassekommentaren for hva hvert felt teller og hvorfor lave rettskilde-/virksomhet-treffrater er
/// FORVENTET (avhenger av hvor mye av Lovdata- og virksomhetsregisteret som faktisk er importert i
/// akkurat denne kjørende instansen).</summary>
public sealed record OppgaveregisterHandlingSeedResultat(
    int SkjemaTotalt,
    int NyeHandlinger,
    int OppdaterteHandlinger,
    int UendretHandlinger,
    int HoppetOverUsikkerVirksomhet,
    int NyeTjenester,
    int LovhjemlerTotalt,
    int RettskildematcherFunnet,
    int RettskildematcherIkkeFunnet);
