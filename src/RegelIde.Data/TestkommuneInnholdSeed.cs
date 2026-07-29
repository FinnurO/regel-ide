using Microsoft.EntityFrameworkCore;

namespace RegelIde.Data;

/// <summary>
/// Forfatter Testkommunens egne lokale rettskilder — docs/06-veikart.md: "Testkommunens egne lokale
/// rettskilder skal også inn i biblioteket... må faktisk importeres/forfattes for Testkommunen, ikke
/// bare nevnes." To reelle referansedokumenter (Vennesla og Tønsberg kommuners faktisk vedtatte
/// alkoholpolitiske retningslinjer, `data/kilder/referanser/`) er brukt som INSPIRASJON og kildegrunnlag
/// — dette forfatter en FIKTIV Testkommune-versjon, ikke en ordrett import av noen av de to ekte
/// kommunenes dokumenter under Testkommunens navn (se `data/kilder/referanser/README.md`, som eksplisitt
/// sier de ekte PDF-ene er sammenligningsgrunnlag, ikke noe som skal importeres som Testkommunens egne
/// rettskilder).
///
/// Kjøres idempotent ved oppstart (RegelIde.Api/Program.cs), samme mønster som Brukere/
/// TaggKindKonfigurasjon-seedingen der — guardet på om en rettskilde med riktig tittel+kildetype
/// allerede finnes, ikke bare "er databasen tom" (som de andre seedingsblokkene bruker), siden denne
/// kjører etter at Lov/Forskrift allerede kan være importert.
/// </summary>
public static class TestkommuneInnholdSeed
{
    private const string SeedBruker = "Kari Jurist";

    public static async Task SeedAsync(RegelIdeDbContext db, CancellationToken ct = default)
    {
        var testkommunen = await db.Virksomheter.FirstOrDefaultAsync(v => v.Navn == "Testkommunen", ct);
        if (testkommunen is null) return; // Brukere-seedingen (som oppretter Testkommunen) kjører før denne — se Program.cs

        var tjeneste = new HandbokForfatterTjeneste(db);
        await SeedLokalForskriftAsync(db, tjeneste, testkommunen.Id, ct);
        await SeedAlkoholpolitiskeRetningslinjerAsync(db, tjeneste, testkommunen.Id, ct);
    }

    /// <summary>
    /// "Forskrift om salgs- og skjenketider for alkoholholdig drikk, Testkommune" — reelt eksempel:
    /// Tønsberg kommunes egen forskrift (LF/forskrift/2020-12-09-2924, ikke tilgjengelig som fil, se
    /// `06-veikart.md`), men de KONKRETE salgs-/skjenketidene er hentet ordrett fra Tønsberg-kommunens
    /// alkoholpolitiske retningslinjer §1.6.1/§1.6.2 (`data/kilder/referanser/tonsberg-kommune-
    /// retningslinjer-2024-2028.pdf`), som selv siterer forskriften sin — samme tall, re-attribuert til
    /// den fiktive Testkommunen.
    /// </summary>
    private static async Task SeedLokalForskriftAsync(RegelIdeDbContext db, HandbokForfatterTjeneste tjeneste, Guid virksomhetId, CancellationToken ct)
    {
        const string tittel = "Forskrift om salgs- og skjenketider for alkoholholdig drikk, Testkommune";
        if (await db.Rettskilder.AnyAsync(r => r.Tittel == tittel && r.Kildetype == "Forskrift", ct)) return;

        var forskrift = await tjeneste.OpprettHandbokAsync(tittel, virksomhetId, SeedBruker, kildetype: "Forskrift", doctype: "act", ct: ct);

        var salgstider = await tjeneste.OpprettKapittelNodeAsync(forskrift.Id, null, "1", "Salgstider", SeedBruker, ct);
        await tjeneste.OpprettBladNodeAsync(forskrift.Id, salgstider.Id, "ledd", "§ 1-1",
            "Salgstider for alkoholholdig drikk gruppe 1",
            "Salg og utlevering av alkoholholdig drikk med høyst 4,7 volumprosent alkohol kan skje i " +
            "utsalgsstedets åpningstid, innenfor følgende salgs- og utleveringstider: fra kl. 08.00 til " +
            "20.00 hverdager og dag før Kristi Himmelfartsdag, og fra kl. 08.00 til 18.00 dager før søn- " +
            "og helligdager. Salg og utlevering av alkoholholdig drikk skal ikke skje på søn- og " +
            "helligdager samt 1. og 17. mai.",
            SeedBruker, ct);

        var skjenketider = await tjeneste.OpprettKapittelNodeAsync(forskrift.Id, null, "2", "Skjenketider", SeedBruker, ct);
        await tjeneste.OpprettBladNodeAsync(forskrift.Id, skjenketider.Id, "ledd", "§ 2-1",
            "Skjenketider for alkoholholdig drikk gruppe 1 og 2",
            "Skjenking av alkoholholdig drikk med lavere alkoholinnhold enn 22 volumprosent alkohol kan " +
            "skje alle dager fra 1. september til 14. mai fra kl. 08.00 til 02.00, og fra 15. mai til " +
            "31. august fra kl. 08.00 til 03.00.",
            SeedBruker, ct);
        await tjeneste.OpprettBladNodeAsync(forskrift.Id, skjenketider.Id, "ledd", "§ 2-2",
            "Skjenketider for alkoholholdig drikk gruppe 3",
            "Skjenking av alkoholholdig drikk med 22 volumprosent eller mer kan skje alle dager fra " +
            "1. september til 14. mai fra kl. 13.00 til 02.00, og fra 15. mai til 31. august fra " +
            "kl. 13.00 til 03.00.",
            SeedBruker, ct);
    }

    /// <summary>
    /// "Alkoholpolitiske retningslinjer for Testkommunen 2024-2028" — et representativt utvalg
    /// (ikke hele det 8-12 siders lange dokumentet), syntetisert fra STRUKTUR og INNHOLD i begge ekte
    /// referansedokumenter (Vennesla og Tønsberg kommuner), ikke en kopi av noen av dem.
    /// </summary>
    private static async Task SeedAlkoholpolitiskeRetningslinjerAsync(RegelIdeDbContext db, HandbokForfatterTjeneste tjeneste, Guid virksomhetId, CancellationToken ct)
    {
        const string tittel = "Alkoholpolitiske retningslinjer for Testkommunen 2024-2028";
        if (await db.Rettskilder.AnyAsync(r => r.Tittel == tittel && r.Kildetype == "Virksomhetsdokument", ct)) return;

        var retningslinjer = await tjeneste.OpprettHandbokAsync(tittel, virksomhetId, SeedBruker, kildetype: "Virksomhetsdokument", doctype: "internal", ct: ct);

        var generelt = await tjeneste.OpprettKapittelNodeAsync(retningslinjer.Id, null, "1", "Generelle bestemmelser", SeedBruker, ct);
        await tjeneste.OpprettBladNodeAsync(retningslinjer.Id, generelt.Id, "ledd", "1.1", "Aldersgrenser (alkoholloven § 1-5)",
            "Alkoholholdig drikke gruppe 1 og 2 har aldersgrense 18 år. Alkoholholdig drikke gruppe 3 har " +
            "aldersgrense 20 år. Aldersgrensene gjelder for gjester, kunder og betjening.",
            SeedBruker, ct);

        var konsept = await tjeneste.OpprettKapittelNodeAsync(retningslinjer.Id, null, "2", "Kommunale konseptbegrensninger", SeedBruker, ct);
        await tjeneste.OpprettBladNodeAsync(retningslinjer.Id, konsept.Id, "ledd", "2.1", "Steder det ikke gis bevilling til",
            "Det gis ikke skjenkebevilling til steder beregnet kun for barn og ungdom, ungdomsklubber " +
            "eller lignende, til virksomheter lokalisert i tilknytning til idrettsanlegg, eller til " +
            "virksomheter lokalisert i boligområder med mindre særlige grunner tilsier det.",
            SeedBruker, ct);

        var skjenketider = await tjeneste.OpprettKapittelNodeAsync(retningslinjer.Id, null, "3", "Skjenketider", SeedBruker, ct);
        await tjeneste.OpprettBladNodeAsync(retningslinjer.Id, skjenketider.Id, "ledd", "3.1", "Fastsatte skjenketider",
            "Skjenking av alkoholholdig drikk skal skje i henhold til Testkommunens forskrift om " +
            "salgs- og skjenketider for alkoholholdig drikk. Det kan vedtas innskrenkninger i " +
            "skjenketiden for det enkelte skjenkested ut fra en skjønnsmessig vurdering av lokale forhold.",
            SeedBruker, ct);

        var kontroll = await tjeneste.OpprettKapittelNodeAsync(retningslinjer.Id, null, "4", "Kontroll og reaksjoner", SeedBruker, ct);
        await tjeneste.OpprettBladNodeAsync(retningslinjer.Id, kontroll.Id, "ledd", "4.1", "Salgs- og skjenkekontroll",
            "Testkommunen har inngått avtale med privat skjenkekontrollør for gjennomføring av kontroller " +
            "etter reglene om offentlig anskaffelser. Reaksjoner ved overtredelse følger alkoholforskriftens " +
            "kapittel 10 om prikktildeling og inndragning.",
            SeedBruker, ct);
    }
}
