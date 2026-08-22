using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Seeder ALLE virksomheter fra <c>Seed/organisasjoner-norge.json</c> (Johanns eksport, 2026-08-14)
/// inn som <see cref="Virksomhet"/>-rader, og styrer <see cref="Virksomhet.Aktiv"/> for hele registeret
/// etter Johanns eksplisitte instruks: "La oss ikke gjøre alle aktive. Behold Bergen, Agder
/// fylkeskommune og Testkommune [aktive]." — alt annet forblir Aktiv=false: til stede i registeret,
/// men ikke valgbart for nytt arbeid ennå (se <see cref="Virksomhet.Aktiv"/>s egen klassekommentar).
///
/// <para>
/// [ENDRET — virksomhetskatalog-runden, 2026-08-22, docs/20 §4, `[LÅST]`] Var avgrenset til
/// `KOMM`/`FYLK` (357+14 av 451 rader) — de 80 andre radene (`STAT`/`ORGL`/`SF`/`AS`/`STI`/`FLI`/
/// `ANNA`/`SÆR`) hoppes IKKE lenger over. Eksplisitt instruert: "du kan ikke vurdere hva som er reelle
/// virksomheter [som] gjør offentlige oppgaver bare basert på navnet" — ALLE 451 rader seedes uendret,
/// ingen navnemønster- eller `orgForm`-basert filtrering.
/// </para>
///
/// <para>
/// **Matching, i rekkefølge**: (1) eksisterende <see cref="Virksomhet"/> med samme
/// <see cref="Virksomhet.Organisasjonsnummer"/>, ellers (2) eksisterende virksomhet med
/// case-ufølsomt likt <see cref="Virksomhet.Navn"/> (fanger Agder fylkeskommune/Bærum kommune/
/// Tønsberg kommune, som andre seeds allerede har opprettet UTEN organisasjonsnummer). Finnes ingen
/// av delene, opprettes en ny rad. Kun NULL-felter (Organisasjonsnummer/Forvaltningsniva) fylles inn
/// på et treff — <see cref="Virksomhet.Kommunenummer"/> røres ALDRI her, siden kildefilen ikke har det
/// (ingen gjettet fallback, se feltets egen klassekommentar: "statlige/regionale ... har ingen").
/// </para>
///
/// <para>
/// **Navneformatering, v1-forenkling** (samme prinsipp som
/// <c>BrukerveiledningImportTjeneste.MinimalAknPlassholder</c>s dokumenterte forenkling): kildenavnene
/// er rene ASCII/Unicode-VERSALER ("AGDER FYLKESKOMMUNE", en del med samisk dobbeltnavn, skråstreker,
/// "OG"-konnektorer). Ekte norsk tittel-kasing av disse pålitelig er ikke noe man kan gjøre
/// algoritmisk uten å garantert bomme på noen — derfor IKKE forsøkt. I stedet: stor forbokstav, resten
/// small caps ("AGDER FYLKESKOMMUNE" → "Agder fylkeskommune") — nøyaktig samme kasing det eksisterende
/// "Agder fylkeskommune"-navnet fra <see cref="AgderFylkeskommuneSeed"/> allerede har, så et JSON-treff
/// på den raden aldri ville trengt reformatering uansett.
/// </para>
///
/// <para>
/// **Aktiv-idempotens** — det ENESTE feltet som ikke kan styres med en enkel "kun NULL"-backfill
/// (bool, ikke nullable). Bergen kommune/Agder fylkeskommune tvinges til <c>Aktiv=true</c> på HVER
/// kjøring — det er selve, varige policyen, ikke en tilstand som skal kunne drifte bort. For alle
/// andre rader settes <c>Aktiv=false</c> KUN i det øyeblikket raden først matches (organisasjonsnummer
/// var NULL fra før — se matching over) eller opprettes — aldri på senere kjøringer, hvor raden
/// allerede har organisasjonsnummer fra forrige kjøring. Uten dette skillet ville seeden ved hver
/// oppstart nullstilt enhver fremtidig manuell aktivering av en sovende kommune (§"presenting for
/// fremtidig aktivering" i feltets egen kommentar) — nøyaktig den klobring-risikoen resten av
/// kodebasens seeds unngår ved kun å skrive når noe faktisk mangler.
/// </para>
///
/// <para>
/// Kjøres idempotent ved oppstart (RegelIde.Api/Program.cs), etter alle andre seeds som oppretter
/// Virksomhet-rader (Testkommunen/Agder/Bærum+Tønsberg/Bergen) — slik at matching mot dem faktisk
/// finner noe i stedet for å opprette duplikater.
/// </para>
/// </summary>
public static class OrganisasjonsregisterSeed
{
    private const string SeedFilnavn = "Seed/organisasjoner-norge.json";

    /// <summary>Bergen kommune — eneste ekte kommune Johann ba om å beholde aktiv.</summary>
    private const string BergenOrgnr = "964338531";

    /// <summary>Agder fylkeskommune — eneste fylkeskommune Johann ba om å beholde aktiv.</summary>
    private const string AgderOrgnr = "921707134";

    private const string TestkommunenNavn = "Testkommunen";

    private static readonly JsonSerializerOptions JsonInnstillinger = new() { PropertyNameCaseInsensitive = true };

    private sealed record OrganisasjonJsonEntry(
        [property: JsonPropertyName("organisasjonsnummer")] string Organisasjonsnummer,
        [property: JsonPropertyName("navn")] string Navn,
        [property: JsonPropertyName("orgForm")] string OrgForm);

    public static async Task SeedAsync(RegelIdeDbContext db, CancellationToken ct = default)
    {
        var filsti = Path.Combine(AppContext.BaseDirectory, SeedFilnavn);
        if (!File.Exists(filsti)) return; // ingen gjettet fallback — samme skip-mønster som BergenKorpusSeed ved manglende kildemappe.

        var alle = JsonSerializer.Deserialize<List<OrganisasjonJsonEntry>>(
            await File.ReadAllTextAsync(filsti, ct), JsonInnstillinger) ?? [];

        var eksisterende = await db.Virksomheter.ToListAsync(ct);
        var perOrgnr = new Dictionary<string, Virksomhet>(StringComparer.Ordinal);
        var perNavn = new Dictionary<string, Virksomhet>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in eksisterende)
        {
            if (v.Organisasjonsnummer is not null) perOrgnr.TryAdd(v.Organisasjonsnummer, v);
            perNavn.TryAdd(v.Navn, v);
        }

        Virksomhet? bergen = null;

        foreach (var entry in alle)
        {
            // [ENDRET, docs/20 §4 `[LÅST]`] KOMM/FYLK er entydig fra Brregs orgForm og settes derfor
            // automatisk — alt annet starter NULL og fylles inn manuelt (docs/20 §7.2, `[LÅST]`: ikke
            // gjett Forvaltningsniva fra orgForm/sektorkode/navn for de resterende kategoriene, selv
            // der et riktig gjett (f.eks. "statsforvalter") ville vært opplagt).
            var forvaltningsniva = entry.OrgForm switch
            {
                "FYLK" => "fylkeskommune",
                "KOMM" => "kommune",
                _ => null,
            };
            var skalAlltidVaereAktiv = entry.Organisasjonsnummer is BergenOrgnr or AgderOrgnr;

            var matchetPaOrgnr = perOrgnr.TryGetValue(entry.Organisasjonsnummer, out var funnetPaOrgnr);
            var match = matchetPaOrgnr ? funnetPaOrgnr : perNavn.GetValueOrDefault(entry.Navn);
            var forsteGangDenneRaden = !matchetPaOrgnr && match is not null; // matchet kun på navn ⇒ orgnr var NULL fra før.

            if (match is null)
            {
                match = new Virksomhet
                {
                    Id = Guid.NewGuid(),
                    Navn = FormaterNavnEnkelt(entry.Navn),
                    Organisasjonsnummer = entry.Organisasjonsnummer,
                    Forvaltningsniva = forvaltningsniva,
                    Aktiv = skalAlltidVaereAktiv, // false for alle nye, sovende kommuner/fylkeskommuner — se klassekommentaren.
                    OpprettetTidspunkt = DateTimeOffset.UtcNow,
                };
                db.Virksomheter.Add(match);
                perOrgnr[entry.Organisasjonsnummer] = match;
                perNavn.TryAdd(match.Navn, match);
            }
            else
            {
                match.Organisasjonsnummer ??= entry.Organisasjonsnummer;
                match.Forvaltningsniva ??= forvaltningsniva;
                perOrgnr.TryAdd(entry.Organisasjonsnummer, match);

                if (skalAlltidVaereAktiv) match.Aktiv = true; // Bergen/Agder — tvunget sant på HVER kjøring, se klassekommentaren.
                else if (forsteGangDenneRaden) match.Aktiv = false; // kun ved første treff — se Aktiv-idempotens over.
            }

            if (entry.Organisasjonsnummer == BergenOrgnr) bergen = match;
        }

        var testkommunen = perNavn.GetValueOrDefault(TestkommunenNavn);
        if (testkommunen is not null) testkommunen.Aktiv = true; // ikke i JSON-en (fiktiv) — defensivt eksplisitt, se Virksomhet.Aktiv.

        await db.SaveChangesAsync(ct);

        await SeedBergenBrukereAsync(db, bergen, ct);
        if (testkommunen is not null) await SeedForvaltningsnivaKodelisteAsync(db, testkommunen.Id, ct);
    }

    /// <summary>
    /// [Ny, virksomhetskatalog-runden, docs/20] Forvaltningsniva var et fritt-streng-felt uten noen
    /// redigerbar/synlig verdiliste — Johann påpekte dette rett etter at UI-et var bygget: "burde ikke
    /// forvaltningsnivå vært en kodeliste?" Ja. Samme mønster som KL-HANDLINGSTYPE m.fl. i
    /// ServeringsbevillingModellSeed.cs — Type="teknisk", eid av Testkommunen (samme eier-konvensjon
    /// som de andre tekniske kodelistene, selv om verdisettet reelt er delt/nasjonalt).
    /// </summary>
    private static async Task SeedForvaltningsnivaKodelisteAsync(RegelIdeDbContext db, Guid virksomhetId, CancellationToken ct)
    {
        const string kode = "KL-FORVALTNINGSNIVA";
        if (await db.Kodelister.AnyAsync(k => k.Kode == kode, ct)) return;

        var register = new KodelisteregisterTjeneste(db);
        var kodeliste = await register.OpprettAsync(
            virksomhetId, kode, "Forvaltningsnivå", "teknisk", juridiskGrunnlagEid: null, eksternKildeUri: null,
            eksternKildeVersjon: null, "system", ct);

        // Verdisettet fra docs/20 §2.1 — se Virksomhet.Forvaltningsniva sin klassekommentar.
        (string Kode, string Term)[] verdier =
        [
            ("stat", "Stat"), ("kommune", "Kommune"), ("fylkeskommune", "Fylkeskommune"),
            ("statsforvalter", "Statsforvalter"), ("tingrett", "Tingrett"),
            ("lagmannsrett", "Lagmannsrett"), ("jordskifterett", "Jordskifterett"),
        ];
        foreach (var (verdiKode, term) in verdier)
        {
            await register.LeggTilKodeAsync(kodeliste.Id, verdiKode, term, definisjon: null, gyldigFra: null, gyldigTil: null, ct);
        }
    }

    /// <summary>
    /// Lukker et mindre, relatert gap: uten dette finnes ingen testbruker som kan "opptre på vegne av
    /// Bergen kommune" i identitetsvelgeren, selv om Bergen kommune eier ekte innhold (hele Bergen-
    /// brukerveiledningskorpuset, se <see cref="BergenKorpusSeed"/>). Samme mønster som
    /// <see cref="AgderFylkeskommuneSeed"/> sin "Silje Jurist" — direkte <c>db.Brukere.Add</c>, ikke
    /// <c>BrukerregisterTjeneste</c> (samme begrunnelse: dette er seed-tidspunkt, ikke en ekte
    /// bruker-opprettelses-forespørsel). Guardet på om Bergen-virksomheten allerede har NOEN brukere —
    /// ikke et navnematch — siden dette skal kunne kjøre uansett om Bergen-raden kom fra
    /// <see cref="BergenKorpusSeed"/> eller ble opprettet her (test-miljøer uten data/kilder-mappen).
    /// </summary>
    private static async Task SeedBergenBrukereAsync(RegelIdeDbContext db, Virksomhet? bergen, CancellationToken ct)
    {
        if (bergen is null) return; // JSON-en manglet uventet Bergen-raden — ingen gjettet fallback.
        if (await db.Brukere.AnyAsync(b => b.VirksomhetId == bergen.Id, ct)) return;

        db.Brukere.AddRange(
            new Bruker { Id = Guid.NewGuid(), Navn = "Mari Fagansvarlig", VirksomhetId = bergen.Id, Rolle = "Fagansvarlig" },
            new Bruker { Id = Guid.NewGuid(), Navn = "Jonas Saksbehandler", VirksomhetId = bergen.Id, Rolle = "Saksbehandler" });
        await db.SaveChangesAsync(ct);
    }

    private static string FormaterNavnEnkelt(string kildeNavn)
    {
        var lav = kildeNavn.ToLowerInvariant();
        return lav.Length == 0 ? lav : char.ToUpperInvariant(lav[0]) + lav[1..];
    }
}
