using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, issue #294] Kobler <see cref="KommuneTjenesteHenter"/>s og <see cref="TjenestelisteImporter"/>s
/// allerede høstede skjema-/tjenestekataloger (<see cref="EksternKildeEntitet"/>, kildetypene
/// <see cref="KommuneTjenesteHenter.Kildetype"/>/<see cref="TjenestelisteImporter.FylkeskommuneDialog"/>/
/// <see cref="TjenestelisteImporter.Statsforvalter"/>) inn i domenemodellen som ekte
/// <see cref="HandlingEntitet"/>-rader — samme gap <see cref="OppgaveregisterHandlingSeed"/> lukket for
/// Oppgaveregisteret (issue #290/PR #292), nå for disse tre strukturelt identiske kildene. Leser fra
/// <see cref="EksternKildeEntitet"/>, SKRIVER ALDRI til den.
///
/// <para>
/// **Én delt klasse, ikke tre** — til forskjell fra HØSTE-laget (der <see cref="KommuneTjenesteHenter"/>
/// er en EGEN klasse fra <see cref="TjenestelisteImporter"/> fordi TOPPNIVÅ-JSON-formen er ulik, se
/// <see cref="KommuneTjenesteHenter"/>s klassekommentar) er PER-RECORD-formen som faktisk havner i
/// <see cref="EksternKildeEntitet.RaaJson"/> IDENTISK for alle tre kildene, empirisk verifisert mot den
/// ekte, kjørende dev-databasen 2026-10-01: <c>{url, kategori|tema, tilbys_av[{organisasjon,
/// organisasjonsnummer}], beskrivelse, tjenestenavn}</c> (kommune har i tillegg et <c>kilder</c>-felt
/// denne klassen ikke trenger). Selve KONVERTERINGEN kan derfor være én klasse, parametrisert på
/// <c>kildetype</c> — samme generalisering <see cref="TjenestelisteImporter"/> selv allerede gjorde ett
/// lag ned.
/// </para>
///
/// <para>
/// **(a) Ingen <c>lovhjemler[]</c> — ingen regelverksreferanse-logikk her** — til forskjell fra
/// Oppgaveregisteret har INGEN av disse tre kildene noe dokument-/paragrafnivå-hjemmelsfelt å matche
/// deterministisk mot (bekreftet ved faktisk inspeksjon av rå-JSON-formen, se issue #294). Denne
/// klassen oppretter derfor ALDRI en <see cref="HandlingRegelverksreferanseEntitet"/> — de ~16 000 nye
/// tjenestene lander med null regelverksreferanser, og plukkes AUTOMATISK opp av den allerede
/// eksisterende <see cref="TjenesteRegelverksreferanseforslagTjeneste"/> (PR #289, bygget nettopp for
/// "tjenester uten regelverksreferanser" uavhengig av hvor tjenesten kom fra) — ingen ny
/// KI-forslagstjeneste er bygget eller trengs for dette.
/// </para>
///
/// <para>
/// **(b) Statsforvalter-duplisering — IKKE en master/instans-relasjon** — en tjeneste kan tilbys av
/// FLERE statsforvalter-embeter samtidig (opptil 10, se <see cref="TjenestelisteImporter"/>s
/// klassekommentar), og det spørsmålet er eksplisitt parkert av Johann ("la oss vente litt med
/// master") — IKKE løst her. Løsningen i DENNE runden: løkken under går over HVERT element i
/// <c>tilbys_av[]</c> (for kommune/fylkeskommune alltid nøyaktig ett element, empirisk verifisert — for
/// statsforvalter opptil ti) og oppretter/finner EN Handling PER kjent tilbyder-virksomhet, under DENS
/// EGEN plassholder-Tjeneste — samme kilderad (<see cref="EksternKildeEntitet.Id"/>) kan dermed lovlig
/// gi opphav til FLERE <see cref="HandlingEntitet"/>-rader, én per tilbyder. Dette er grunnen til at
/// <c>ux_handlinger_ekstern_kilde</c>-indeksen (RegelIdeDbContext.cs) ble utvidet fra UNIK på
/// <see cref="HandlingEntitet.EksternKildeId"/> ALENE til UNIK på (EksternKildeId, TjenesteId) i samme
/// PR — uten den utvidelsen ville andre tilbyder-oppføring for samme kilderad kollidert i databasen.
/// Idempotens-nøkkelen (se punkt (e)) er derfor PARET (EksternKildeId, TjenesteId), ikke EksternKildeId
/// alene — <see cref="OppgaveregisterHandlingSeed"/> ble samtidig rettet (samme PR) til å SKOPE sin egen
/// idempotens-oppslag til kun sin egen kildetype, se dens klassekommentar/kildekommentar, ellers ville
/// en uskopet <c>ToDictionaryAsync</c> der kastet på nøyaktig denne dupliseringen.
/// </para>
///
/// <para>
/// **(c) Virksomhet-matching — EKSAKT, per tilbyder** — samme "ingen gjettet fallback"-prinsipp som
/// <see cref="OppgaveregisterHandlingSeed"/> punkt (a): <c>tilbys_av[].organisasjonsnummer</c> matches
/// EKSAKT mot <see cref="Virksomhet.Organisasjonsnummer"/> (allerede en streng i kilden, ingen
/// tall→D9-konvertering nødvendig, til forskjell fra Oppgaveregisterets <c>eier.organisasjonsnummer</c>
/// som er et JSON-tall). En tilbyder UTEN kjent orgnr-treff telles
/// (<see cref="EksternTjenestelisteHandlingSeedResultat.HoppetOverUsikkerVirksomhet"/>) og hoppes over —
/// men KUN for DEN tilbyderen, ikke for hele raden: har en statsforvalter-tjeneste to tilbydere der kun
/// én er kjent, opprettes Handlingen likevel under DEN kjente tilbyderens plassholder.
/// </para>
///
/// <para>
/// **(d) Tjeneste-design — egne, kildetype-navngitte plassholdere** — EN samlende
/// <see cref="TjenesteEntitet"/> per (kildetype, eiende virksomhet), find-or-create på
/// (VirksomhetId, Tittel), SAMME begrunnelse som <see cref="OppgaveregisterHandlingSeed"/> punkt (b) —
/// men med EGEN, tydelig tittelprefiks PER kildetype (<see cref="TittelPrefiksPerKildetype"/>), IKKE
/// samme plassholder som Oppgaveregisteret (Johanns eksplisitte krav, issue #294) — en kommunes
/// Oppgaveregister-skjemaer og dens kommune.no-skjemaer skal ikke blandes i samme grove samlerad, selv
/// om begge til syvende og sist er "innsendte skjemaer" for samme virksomhet.
/// </para>
///
/// <para>
/// **(e) Handlingstype/Bruksomraade/UtfortAv — ingen kildefelt å klassifisere fra** — til forskjell fra
/// Oppgaveregisterets <c>bruksomraader[].navn</c> (som determinerisk mappes til Handlingstype, se
/// <see cref="OppgaveregisterHandlingSeed"/> punkt (d)) har INGEN av disse tre kildene noe tilsvarende
/// felt. <see cref="HandlingregisterTjeneste.GyldigeHandlingstyper"/>s "annet" brukes derfor UNIFORMT —
/// en eksplisitt, dokumentert "ingen informasjon å klassifisere fra, ikke gjettet"-holdning, ikke en
/// forglemmelse. <see cref="HandlingEntitet.Bruksomraade"/> forblir <c>null</c> av samme grunn.
/// <see cref="HandlingEntitet.UtfortAv"/> = <c>"soker"</c> (hardkodet, samme begrunnelse som
/// Oppgaveregisteret punkt (a) sin implisitte konstant — alle tre kildene er skjema/tjenester en
/// innbygger/virksomhet selv sender INN til myndigheten, aldri omvendt).
/// </para>
///
/// <para>
/// **(f) Merknad — beskrivelse, med kategori/tema som fallback** — <c>beskrivelse</c> brukes direkte når
/// utfylt (trimmet). Kommune-kildens <c>beskrivelse</c> er DOKUMENTERT ALLTID tom streng i alle ~15 300
/// rader (data/kilder/kommune-skraping/README.md, "Kjente svakheter") — for DEN kildetypen faller
/// Merknad derfor automatisk tilbake til <c>kategori</c> (100 % utfylt i stikkprøven, f.eks. "Helse og
/// omsorg", "Næring"), i stedet for å lande som <c>null</c> for samtlige ~15 300 rader. Statsforvalter
/// bruker <c>tema</c> i stedet for <c>kategori</c> for samme konsept (bekreftet ulikt feltnavn ved
/// faktisk inspeksjon av kilden, ikke antatt) — begge leses, førstnevnte ikke-blanke vinner. Se
/// PR-beskrivelsen for den manuelle utvalgs-testen (AC3) som vurderer om dette faktisk monner i
/// praksis, ikke bare i teorien.
/// </para>
///
/// <para>
/// **(g) Idempotens** — matcher på (<see cref="HandlingEntitet.EksternKildeId"/>,
/// <see cref="HandlingEntitet.TjenesteId"/>) PARET, se punkt (b) for hvorfor paret og ikke
/// EksternKildeId alene. Re-kjøring med uendrede kildedata er en no-op. Trigges på forespørsel
/// (<c>POST /api/eksterne-kilder/{kildetype}/koble-til-handlinger</c>), IKKE ved oppstart.
/// </para>
///
/// <para>
/// **(h) Massesletting** — <see cref="SlettKonverterteAsync"/> (<c>DELETE
/// /api/eksterne-kilder/{kildetype}/konverterte-tjenester</c>) fjerner ALLE Handling-rader denne seeden
/// har opprettet for den gitte kildetypen (matcher via EksternKilde.Kildetype → Handling.EksternKildeId),
/// og rydder deretter bort plassholder-Tjenester (kun DENNE kildetypens tittelprefiks) som blir tomme
/// etterpå. <see cref="EksternKildeEntitet"/>-radene selv røres ALDRI (proveniens, ikke noe å kaste) —
/// kun de avledede domeneradene, slik at en ny konverteringskjøring kan starte helt på nytt. Finnes for
/// når selve KONVERTERINGSLOGIKKEN endres og gamle rader må bort — selve konverteringen er allerede
/// idempotent uavhengig av dette.
/// </para>
/// </summary>
public static class EksternTjenestelisteHandlingSeed
{
    private static readonly JsonSerializerOptions JsonInnstillinger = new() { PropertyNameCaseInsensitive = true };

    /// <summary>De tre kildetypene denne klassen kjenner til, og tittelprefikset den bruker for den
    /// kildetypens plassholder-Tjenester — se klassekommentaren punkt (d). Et ukjent kildetype-argument
    /// til <see cref="SeedAsync"/>/<see cref="SlettKonverterteAsync"/> kaster <see cref="ArgumentException"/>,
    /// ingen gjettet fallback.</summary>
    internal static readonly Dictionary<string, string> TittelPrefiksPerKildetype = new(StringComparer.Ordinal)
    {
        [KommuneTjenesteHenter.Kildetype] = "Kommunale skjema — ",
        [TjenestelisteImporter.FylkeskommuneDialog] = "Fylkeskommunal dialogtjeneste — ",
        [TjenestelisteImporter.Statsforvalter] = "Statsforvalter-tjeneste — ",
    };

    private static readonly Dictionary<string, string> PlassholderBeskrivelsePerKildetype = new(StringComparer.Ordinal)
    {
        [KommuneTjenesteHenter.Kildetype] =
            "Samleside for skjemaer hentet fra kommunens egne nettsider (kommune.no-skraping, Johanns eget " +
            "eksterne skript, se data/kilder/kommune-skraping/README.md) — automatisk seedet, grov plassholder. " +
            "Hver handling under bør etter hvert flyttes til sin egentlige Tjeneste når en fagperson har " +
            "vurdert den.",
        [TjenestelisteImporter.FylkeskommuneDialog] =
            "Samleside for dialog-/kontaktskjemaer hentet fra fylkeskommunens egen dialogtjeneste-oversikt " +
            "(Johanns eget eksterne skript) — automatisk seedet, grov plassholder. Hver handling under bør " +
            "etter hvert flyttes til sin egentlige Tjeneste når en fagperson har vurdert den.",
        [TjenestelisteImporter.Statsforvalter] =
            "Samleside for skjemaer og tjenester hentet fra Statsforvalterens egen skjema-/tjenesteoversikt " +
            "(Johanns eget eksterne skript) — automatisk seedet, grov plassholder. Merk: samme kilderad kan " +
            "opptre under FLERE statsforvalter-embeters plassholdere når tjenesten tilbys av mer enn ett " +
            "embete (ingen master/instans-modellering, se TjenestelisteImporter sin klassekommentar). Hver " +
            "handling under bør etter hvert flyttes til sin egentlige Tjeneste når en fagperson har vurdert den.",
    };

    /// <summary>Kun feltene denne koblingen faktisk bruker — resten er allerede bevart verbatim i
    /// <see cref="EksternKildeEntitet.RaaJson"/>. Se klassekommentaren for hvorfor formen er IDENTISK på
    /// tvers av alle tre kildetypene.</summary>
    private sealed record TjenesteRecordJson(
        [property: JsonPropertyName("tjenestenavn")] string? Tjenestenavn,
        [property: JsonPropertyName("beskrivelse")] string? Beskrivelse,
        [property: JsonPropertyName("kategori")] string? Kategori,
        [property: JsonPropertyName("tema")] string? Tema,
        [property: JsonPropertyName("tilbys_av")] List<TilbyderJson>? TilbysAv);

    private sealed record TilbyderJson(
        [property: JsonPropertyName("organisasjonsnummer")] string? Organisasjonsnummer);

    private static string ValiderOgHentTittelPrefiks(string kildetype)
    {
        if (!TittelPrefiksPerKildetype.TryGetValue(kildetype, out var prefiks))
        {
            throw new ArgumentException(
                $"Ukjent kildetype '{kildetype}'. Gyldige verdier: {string.Join(", ", TittelPrefiksPerKildetype.Keys)}.");
        }
        return prefiks;
    }

    public static async Task<EksternTjenestelisteHandlingSeedResultat> SeedAsync(
        RegelIdeDbContext db, string kildetype, CancellationToken ct = default)
    {
        var tittelPrefiks = ValiderOgHentTittelPrefiks(kildetype);
        var opprettetAv = kildetype + "-import";

        var kildeRader = await db.EksterneKilder.Where(k => k.Kildetype == kildetype).ToListAsync(ct);
        var kildeIdSett = kildeRader.Select(k => k.Id).ToHashSet();

        var tjenesteregister = new TjenesteregisterTjeneste(db);
        var handlingregister = new HandlingregisterTjeneste(db);

        var virksomheterPerOrgnr = await db.Virksomheter
            .Where(v => v.Organisasjonsnummer != null)
            .ToDictionaryAsync(v => v.Organisasjonsnummer!, v => v.Id, StringComparer.Ordinal, ct);
        var virksomhetNavnPerId = await db.Virksomheter.ToDictionaryAsync(v => v.Id, v => v.Navn, ct);

        // Plassholder-Tjeneste-cache, scopet til DENNE kildetypens eget tittelprefiks — se klassekommentaren
        // punkt (d). To ulike kildetyper for SAMME virksomhet skal ALDRI dele plassholder.
        var plassholderTjenestePerVirksomhet = await db.Tjenester
            .Where(t => t.Entitetsstatus == "gjeldende" && t.Tittel.StartsWith(tittelPrefiks))
            .ToDictionaryAsync(t => t.VirksomhetId, t => t.Id, ct);

        // Idempotens: (EksternKildeId, TjenesteId) PARET, se klassekommentaren punkt (b)/(g) for hvorfor
        // EksternKildeId alene IKKE er trygt (statsforvalter-duplisering per tilbyder-embete).
        var eksisterendeHandlinger = await db.Handlinger
            .Where(h => h.EksternKildeId != null && h.Entitetsstatus == "gjeldende" && kildeIdSett.Contains(h.EksternKildeId.Value))
            .ToDictionaryAsync(h => (h.EksternKildeId!.Value, h.TjenesteId), ct);

        var nyeHandlinger = 0;
        var oppdaterteHandlinger = 0;
        var uendretHandlinger = 0;
        var hoppetOverUsikkerVirksomhet = 0;
        var nyeTjenester = 0;
        var tilbydereTotalt = 0;

        foreach (var kilderad in kildeRader)
        {
            ct.ThrowIfCancellationRequested();

            var record = JsonSerializer.Deserialize<TjenesteRecordJson>(kilderad.RaaJson, JsonInnstillinger);
            if (record is null || string.IsNullOrWhiteSpace(record.Tjenestenavn)) continue; // ingen gjettet fallback.

            var beskrivelse = string.IsNullOrWhiteSpace(record.Beskrivelse) ? null : record.Beskrivelse.Trim();
            var kategoriEllerTema = !string.IsNullOrWhiteSpace(record.Kategori) ? record.Kategori
                : !string.IsNullOrWhiteSpace(record.Tema) ? record.Tema
                : null;
            // Se klassekommentaren punkt (f) — fallback til kategori/tema kun når beskrivelse mangler.
            var merknad = beskrivelse ?? kategoriEllerTema?.Trim();

            foreach (var tilbyder in record.TilbysAv ?? [])
            {
                tilbydereTotalt++;

                var orgnr = tilbyder.Organisasjonsnummer;
                if (string.IsNullOrWhiteSpace(orgnr) || !virksomheterPerOrgnr.TryGetValue(orgnr, out var virksomhetId))
                {
                    hoppetOverUsikkerVirksomhet++;
                    continue; // se klassekommentaren punkt (c) — ingen kjent eier for DENNE tilbyderen.
                }

                if (!plassholderTjenestePerVirksomhet.TryGetValue(virksomhetId, out var tjenesteId))
                {
                    var virksomhetNavn = virksomhetNavnPerId[virksomhetId];
                    var tjeneste = await tjenesteregister.OpprettAsync(
                        virksomhetId, tittelPrefiks + virksomhetNavn,
                        beskrivelse: PlassholderBeskrivelsePerKildetype[kildetype],
                        kompetentMyndighet: null, output: null, tjenestetype: null, malgruppe: null, kanaler: null,
                        kostnad: null, behandlingstid: null, kontaktpunkt: null, konsekvensVedBrudd: null, sprak: null,
                        opprettetAv, ct);
                    tjenesteId = tjeneste.Id;
                    plassholderTjenestePerVirksomhet[virksomhetId] = tjenesteId;
                    nyeTjenester++;
                }

                var nokkel = (kilderad.Id, tjenesteId);
                if (eksisterendeHandlinger.TryGetValue(nokkel, out var eksisterende))
                {
                    var endret = eksisterende.Navn != record.Tjenestenavn || eksisterende.Merknad != merknad;
                    if (endret)
                    {
                        eksisterende.Navn = record.Tjenestenavn;
                        eksisterende.Merknad = merknad;
                        eksisterende.SistEndretAv = opprettetAv;
                        eksisterende.SistEndretTidspunkt = DateTimeOffset.UtcNow;
                        eksisterende.Versjon++;
                        oppdaterteHandlinger++;
                    }
                    else
                    {
                        uendretHandlinger++;
                    }
                }
                else
                {
                    var handling = await handlingregister.OpprettAsync(
                        virksomhetId, tjenesteId, record.Tjenestenavn, "annet", bruksomraade: null, utfortAv: "soker",
                        kanaler: null, behandlingstid: null, kostnad: null, vedlegg: null, veiledningstekst: null,
                        arsaker: null, resultat: null, merknad, opprettetAv, ct);
                    handling.EksternKildeId = kilderad.Id;
                    eksisterendeHandlinger[nokkel] = handling;
                    nyeHandlinger++;
                }
            }
        }

        await db.SaveChangesAsync(ct);

        return new EksternTjenestelisteHandlingSeedResultat(
            kildetype, kildeRader.Count, tilbydereTotalt, nyeHandlinger, oppdaterteHandlinger, uendretHandlinger,
            hoppetOverUsikkerVirksomhet, nyeTjenester);
    }

    /// <summary>Se klassekommentaren punkt (h). Hard-sletter (ikke en entitetsstatus-markering, samme
    /// <c>ExecuteDeleteAsync</c>-konvensjon som <see cref="TjenesteregisterTjeneste.SlettForslagAsync"/>)
    /// — rydder Proveniens-radene FØR domeneradene selv (Proveniens er en polymorf referanse uten
    /// ekte FK, se samme mønster der), i én transaksjon. Rører ALDRI <see cref="EksternKildeEntitet"/>.</summary>
    public static async Task<EksternTjenestelisteSlettResultat> SlettKonverterteAsync(
        RegelIdeDbContext db, string kildetype, CancellationToken ct = default)
    {
        var tittelPrefiks = ValiderOgHentTittelPrefiks(kildetype);

        var eksterneKildeIder = await db.EksterneKilder.Where(k => k.Kildetype == kildetype).Select(k => k.Id).ToListAsync(ct);

        var handlingIder = await db.Handlinger
            .Where(h => h.EksternKildeId != null && eksterneKildeIder.Contains(h.EksternKildeId.Value))
            .Select(h => h.Id)
            .ToListAsync(ct);

        var paavirkedeTjenesteIder = await db.Handlinger
            .Where(h => handlingIder.Contains(h.Id))
            .Select(h => h.TjenesteId)
            .Distinct()
            .ToListAsync(ct);

        await using var transaksjon = await db.Database.BeginTransactionAsync(ct);

        // Proveniens er en polymorf referanse (entitet_type/entitet_id), ingen ekte FK — må ryddes
        // manuelt, samme mønster som TjenesteregisterTjeneste.SlettForslagAsync. HandlingRegelverksreferanse/
        // -forslag og TjenesteRegelverksreferanse/-forslag er derimot ekte FK-er med ON DELETE CASCADE
        // (RegelIdeDbContext.cs) og ryddes derfor AUTOMATISK av Postgres når Handling/Tjeneste slettes
        // under, ingen manuell opprydding av dem her.
        await db.Proveniens.Where(p => p.EntitetType == "handling" && handlingIder.Contains(p.EntitetId)).ExecuteDeleteAsync(ct);
        var antallSlettedeHandlinger = await db.Handlinger.Where(h => handlingIder.Contains(h.Id)).ExecuteDeleteAsync(ct);

        // Kun plassholder-Tjenester (DENNE kildetypens eget tittelprefiks) som nå står UTEN handlinger —
        // se klassekommentaren punkt (h). En virksomhet kan i teorien ha lagt egne, håndskrevne
        // handlinger under sin plassholder i mellomtiden; den skal IKKE slettes da.
        var tommeTjenesteIder = await db.Tjenester
            .Where(t => paavirkedeTjenesteIder.Contains(t.Id) && t.Tittel.StartsWith(tittelPrefiks))
            .Where(t => !db.Handlinger.Any(h => h.TjenesteId == t.Id))
            .Select(t => t.Id)
            .ToListAsync(ct);

        await db.Proveniens.Where(p => p.EntitetType == "tjeneste" && tommeTjenesteIder.Contains(p.EntitetId)).ExecuteDeleteAsync(ct);
        var antallSlettedeTjenester = await db.Tjenester.Where(t => tommeTjenesteIder.Contains(t.Id)).ExecuteDeleteAsync(ct);

        await transaksjon.CommitAsync(ct);

        return new EksternTjenestelisteSlettResultat(kildetype, antallSlettedeHandlinger, antallSlettedeTjenester);
    }
}

/// <summary>Sammendrag av én <see cref="EksternTjenestelisteHandlingSeed.SeedAsync"/>-kjøring for ÉN
/// kildetype — speil av <see cref="OppgaveregisterHandlingSeedResultat"/>. <see cref="TilbydereTotalt"/>
/// (ikke <see cref="KildeRaderTotalt"/> alene) er nevneren <see cref="NyeHandlinger"/>+
/// <see cref="OppdaterteHandlinger"/>+<see cref="UendretHandlinger"/>+<see cref="HoppetOverUsikkerVirksomhet"/>
/// faktisk summerer til — se klassekommentaren punkt (b): én kilderad kan ha flere tilbydere.</summary>
public sealed record EksternTjenestelisteHandlingSeedResultat(
    string Kildetype,
    int KildeRaderTotalt,
    int TilbydereTotalt,
    int NyeHandlinger,
    int OppdaterteHandlinger,
    int UendretHandlinger,
    int HoppetOverUsikkerVirksomhet,
    int NyeTjenester);

/// <summary>Sammendrag av én <see cref="EksternTjenestelisteHandlingSeed.SlettKonverterteAsync"/>-kjøring.</summary>
public sealed record EksternTjenestelisteSlettResultat(string Kildetype, int SlettedeHandlinger, int SlettedeTjenester);
