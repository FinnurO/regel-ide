using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// [Ny, 2026-08-30, departement-virksomhet-lenke] Seeder de norske departementene som
/// <see cref="Virksomhet"/>-rader, slik at <see cref="RettskildeEntitet.AnsvarligDepartement"/> faktisk
/// har noe å kobles til (se RettskildeRepository.FinnVirksomhetIdForNavnAsync/RettskilderAnsvarligForAsync).
///
/// <para>
/// 4 av 17 departementer fantes ALLEREDE via <see cref="OrganisasjonsregisterSeed"/> — Johanns
/// 2026-08-14-eksport (Seed/organisasjoner-norge.json) inneholdt tilfeldigvis nettopp Energidepartementet,
/// Nærings- og fiskeridepartementet, Justis- og beredskapsdepartementet og Utenriksdepartementet (de 4
/// bekreftet direkte mot en kjørende <c>GET /api/virksomheter</c> før denne runden startet). De
/// resterende 13 legges til her.
/// </para>
///
/// <para>
/// **Kilde for selve departement-listen** (verifisert 2026-08-30, ikke gjettet): regjeringen.no sin
/// egen medlemsoversikt + forvaltningsdatabasen.sikt.no/data/departement (periode 01.01.2026 — "17
/// treff"). Begge kilder er samstemte: 17 departementer, inkludert Statsministerens kontor.
/// </para>
///
/// <para>
/// **Organisasjonsnummer** — slått opp ETT FOR ETT mot Brreg sitt offentlige Enhetsregister-API
/// (<c>data.brreg.no/enhetsregisteret/api/enheter?navn=…</c> for eksakt fullt navn, deretter bekreftet
/// med et direkte <c>/enheter/{orgnr}</c>-oppslag: alle 13 er <c>organisasjonsform.kode="STAT"</c> og
/// har ingen <c>slettedato</c>, dvs. aktive). IKKE et navnesøk på bare "departement" — det traff aldri
/// noe ekte departement i praksis (bekreftet empirisk av Johann, se oppgavebeskrivelsen), trolig fordi
/// Brregs enkle navnesøk her ikke fant substring-treff i akkurat disse radene fra første side. Et EKSAKT
/// fullt navnesøk traff derimot alltid nøyaktig log på topp, med korrekt <c>organisasjonsform.kode</c>.
/// Denne appen bruker likevel IKKE <see cref="BrregKlient"/> til å slå opp på nytt ved hver oppstart —
/// orgnumrene er verifiserte engangsverdier, hardkodet akkurat som <see cref="AgderFylkeskommuneSeed"/>/
/// <see cref="BergenKorpusSeed"/> allerede gjør for sine respektive virksomheter.
/// </para>
///
/// <para>
/// **Navn** er BEVISST hardkodet i korrekt norsk tittelkasing ("Kunnskapsdepartementet"), IKKE hentet
/// rått fra Brregs eget navn-felt (som er STORE BOKSTAVER, og for Digitaliserings- og
/// forvaltningsdepartementet inkluderer i tillegg en "(DFD)"-forkortelse Brreg selv har lagt til).
/// Dette er avgjørende for selve koblingen: <see cref="RettskildeEntitet.AnsvarligDepartement"/> kommer
/// fra Lovdatas eget "ministry"-metadatafelt, som ALLTID er skrevet i nøyaktig denne tittelkasingen uten
/// forkortelse (se LovdataHtmlParser.cs/AlkohollovenKonverteringTests.cs: "Helse- og
/// omsorgsdepartementet") — kun et treff mot akkurat DEN formen kobler rettskilden til riktig
/// virksomhet. Samme "ingen automatisk tittelkasing-algoritme"-begrunnelse som
/// <see cref="OrganisasjonsregisterSeed.FormaterNavnEnkelt"/> dokumenterer for sine 451 rader —
/// forskjellen er at her er det bare 13 navn, alle kjent på forhånd, så et manuelt, korrekt navn er
/// billigere og tryggere enn en algoritme som garantert ville bommet på minst én av dem.
/// </para>
///
/// <para>
/// <see cref="Virksomhet.Forvaltningsniva"/>="stat" — dokumentert gyldig verdi i KL-FORVALTNINGSNIVA
/// (docs/20 §2.1, se feltets egen klassekommentar), ikke gjettet: et departement er utvetydig en
/// statlig virksomhet, samme sikkerhet som gjør KOMM/FYLK entydige fra orgForm i OrganisasjonsregisterSeed.
/// <see cref="Virksomhet.Aktiv"/>=false — samme policy OrganisasjonsregisterSeed gir til alt annet enn
/// Bergen/Agder/Testkommunen: til stede i katalogen og fullt koblingsbar (Aktiv gater kun UI-VELGERE for
/// nytt arbeid, ikke lesetilgang/kobling — se <see cref="Virksomhet.Aktiv"/>), men ikke valgbar i en
/// velger ennå. Ingen instruks fra Johann om å gjøre dem aktive — «ingen gjettet fallback» gjelder også her.
/// </para>
///
/// <para>
/// Idempotent: matcher på Organisasjonsnummer (samme mønster som OrganisasjonsregisterSeed). Kjøres
/// ETTER OrganisasjonsregisterSeed i Program.cs, slik at de 4 allerede seedede departementene
/// gjenkjennes på orgnr og IKKE dupliseres.
/// </para>
/// </summary>
public static class DepartementSeed
{
    private sealed record DepartementEntry(string Organisasjonsnummer, string Navn);

    // Verifisert 2026-08-30 mot data.brreg.no/enhetsregisteret/api/enheter/{orgnr} — alle
    // organisasjonsform.kode="STAT", ingen slettedato. Departement-listen selv: regjeringen.no +
    // forvaltningsdatabasen.sikt.no/data/departement (periode 01.01.2026, "17 treff" — de 4 IKKE
    // listet her (Energidepartementet, Nærings- og fiskeridepartementet, Justis- og
    // beredskapsdepartementet, Utenriksdepartementet) fantes allerede via OrganisasjonsregisterSeed.
    private static readonly DepartementEntry[] Departementer =
    [
        new("983887457", "Arbeids- og inkluderingsdepartementet"),
        new("972417793", "Barne- og familiedepartementet"),
        new("932384469", "Digitaliserings- og forvaltningsdepartementet"),
        new("972417807", "Finansdepartementet"),
        new("972417823", "Forsvarsdepartementet"),
        new("983887406", "Helse- og omsorgsdepartementet"),
        new("972417882", "Klima- og miljødepartementet"),
        new("972417858", "Kommunal- og distriktsdepartementet"),
        new("972417866", "Kultur- og likestillingsdepartementet"),
        new("872417842", "Kunnskapsdepartementet"),
        new("972417874", "Landbruks- og matdepartementet"),
        new("972417904", "Samferdselsdepartementet"),

        // Ikke et ordinært departement (utsteder aldri egne lover/forskrifter — vil derfor aldri
        // faktisk forekomme som AnsvarligDepartement), men del av regjeringen.no/Forvaltningsdatabasens
        // offisielle "17 departementer"-liste. Tatt med for en komplett, korrekt katalog — harmløst at
        // koblingen aldri treffer for denne ene raden.
        new("972417777", "Statsministerens kontor"),
    ];

    public static async Task SeedAsync(RegelIdeDbContext db, CancellationToken ct = default)
    {
        var kjenteOrgnr = new HashSet<string>(
            await db.Virksomheter.Where(v => v.Organisasjonsnummer != null)
                .Select(v => v.Organisasjonsnummer!).ToListAsync(ct),
            StringComparer.Ordinal);

        foreach (var d in Departementer)
        {
            if (kjenteOrgnr.Contains(d.Organisasjonsnummer)) continue;

            db.Virksomheter.Add(new Virksomhet
            {
                Id = Guid.NewGuid(),
                Navn = d.Navn,
                Organisasjonsnummer = d.Organisasjonsnummer,
                Forvaltningsniva = "stat",
                Aktiv = false,
                OpprettetTidspunkt = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
